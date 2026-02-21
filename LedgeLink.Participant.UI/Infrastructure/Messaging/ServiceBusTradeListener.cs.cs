using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LedgeLink.Participant.UI.Application.Services;
using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Participant.UI.Infrastructure.Messaging;

public sealed class ServiceBusTradeListener : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly TradeStreamService _tradeStream;
    private readonly ILogger<ServiceBusTradeListener> _logger;
    private readonly IConfiguration _config;

    public ServiceBusTradeListener(
        ServiceBusClient client,
        TradeStreamService tradeStream,
        ILogger<ServiceBusTradeListener> logger,
        IConfiguration config)
    {
        _client = client;
        _tradeStream = tradeStream;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Each participant listens to its own subscription on the trade.settled topic
        var participantName = _config["PARTICIPANT_NAME"] ?? "Observer";
        var subscriptionName = participantName.Replace(" ", "").ToLower(); // e.g. "schroders", "hargreaveslansdown"

        var processor = _client.CreateProcessor(
            "trade.settled",
            subscriptionName,
            new ServiceBusProcessorOptions { AutoCompleteMessages = false });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var trade = JsonSerializer.Deserialize<TradeToken>(args.Message.Body);
                if (trade is not null)
                {
                    _tradeStream.UpdateTrade(trade);
                    _logger.LogInformation("Received settled trade: {ExternalOrderId}", trade.ExternalOrderId);
                }
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process trade message");
                await args.AbandonMessageAsync(args.Message);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Service Bus processor error");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(ct);
        await Task.Delay(Timeout.Infinite, ct);
    }
}