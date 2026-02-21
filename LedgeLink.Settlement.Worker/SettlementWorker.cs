using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LedgeLink.Settlement.Worker.Application.Interfaces;
using LedgeLink.Settlement.Worker.Application.Services;
using LedgeLink.Shared.Application.Interfaces;
using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Settlement.Worker;

/// <summary>
/// Hosted service entry point — infrastructure plumbing only.
///
/// Responsibilities (thin):
///   - Set up Service Bus processor on trade.validated
///   - Deserialise message → TradeToken
///   - Hand to SettleTradeService (application layer)
///   - Complete / Abandon
///
/// Zero settlement business logic here.
/// </summary>
public sealed class SettlementWorker : BackgroundService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly SettleTradeService _settlementService;
    private readonly ISettlementPublisher _publisher;
    private readonly ILogger<SettlementWorker> _logger;
    private ServiceBusProcessor? _processor;

    public SettlementWorker(
        ServiceBusClient serviceBusClient,
        SettleTradeService settlementService,
        ISettlementPublisher publisher,
        ILogger<SettlementWorker> logger)
    {
        _serviceBusClient   = serviceBusClient;
        _settlementService  = settlementService;
        _publisher          = publisher;
        _logger             = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Settlement.Worker starting...");

        await _publisher.EnsureTopologyAsync(stoppingToken);

        _processor = _serviceBusClient.CreateProcessor(QueueNames.TradeValidated, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("Settlement.Worker listening on '{Queue}'", QueueNames.TradeValidated);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        TradeToken? trade = null;
        try
        {
            var json = args.Message.Body.ToString();
            trade = JsonSerializer.Deserialize<TradeToken>(json);

            if (trade is null)
            {
                _logger.LogError("Deserialisation returned null. Discarding message.");
                await args.DeadLetterMessageAsync(args.Message, cancellationToken: args.CancellationToken);
                return;
            }

            await _settlementService.SettleAsync(trade, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error settling {ExternalOrderId}", trade?.ExternalOrderId ?? "?");
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus error: {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(ct);
            await _processor.DisposeAsync();
        }
        await base.StopAsync(ct);
    }

    public override void Dispose()
    {
        _processor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
