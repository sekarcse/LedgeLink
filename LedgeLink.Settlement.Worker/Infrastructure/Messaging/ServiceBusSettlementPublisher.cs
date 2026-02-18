using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LedgeLink.Settlement.Worker.Application.Interfaces;
using LedgeLink.Shared.Application.Interfaces;
using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Settlement.Worker.Infrastructure.Messaging;

/// <summary>
/// Infrastructure layer: Azure Service Bus implementation of ISettlementPublisher.
/// Only this class knows about Service Bus in the Settlement service.
/// </summary>
public sealed class ServiceBusSettlementPublisher : ISettlementPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusSettlementPublisher> _logger;
    private ServiceBusSender? _sender;
    private bool _topologyReady;

    public ServiceBusSettlementPublisher(ServiceBusClient client, ILogger<ServiceBusSettlementPublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task EnsureTopologyAsync(CancellationToken ct = default)
    {
        if (_topologyReady) return;

        // Service Bus automatically creates queues on first access, no topology setup needed
        _sender = _client.CreateSender(QueueNames.TradeSettled);
        
        _topologyReady = true;
        _logger.LogInformation("Settlement Service Bus sender ready for queue: {QueueName}", QueueNames.TradeSettled);
        await Task.CompletedTask;
    }

    public async Task PublishTradeSettledAsync(TradeToken trade, CancellationToken ct = default)
    {
        if (_sender is null)
            throw new InvalidOperationException("Sender not initialized. Call EnsureTopologyAsync first.");

        var json = JsonSerializer.Serialize(trade);
        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = trade.InternalId.ToString(),
            Subject = QueueNames.TradeSettled
        };

        await _sender.SendMessageAsync(message, ct);

        _logger.LogInformation("Published trade.settled for {ExternalOrderId}", trade.ExternalOrderId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null)
            await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
