using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LedgeLink.Distributor.API.Application.Interfaces;
using LedgeLink.Shared.Application.Interfaces;
using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Distributor.API.Infrastructure.Messaging;

/// <summary>
/// Infrastructure layer: Azure Service Bus implementation of ITradePublisher.
///
/// This is the ONLY class in Distributor.API that knows about Service Bus.
/// The application layer never imports Azure.Messaging.ServiceBus — it talks to ITradePublisher.
/// </summary>
public sealed class ServiceBusTradePublisher : ITradePublisher, IAsyncDisposable
{
    private readonly ILogger<ServiceBusTradePublisher> _logger;
    private readonly ServiceBusClient _client;
    private ServiceBusSender? _sender;
    private bool _topologyReady;

    public ServiceBusTradePublisher(ServiceBusClient client, ILogger<ServiceBusTradePublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    private async Task EnsureTopologyAsync()
    {
        if (_topologyReady) return;

        _sender = _client.CreateSender(QueueNames.TradeRequested);
        
        _topologyReady = true;
        _logger.LogInformation("Service Bus sender ready for queue: {QueueName}", QueueNames.TradeRequested);
        await Task.CompletedTask; // Placeholder for any future topology setup (e.g., creating queues/topics if needed) 
    }

    public async Task PublishTradeRequestedAsync(TradeToken trade, CancellationToken ct = default)
    {
        await EnsureTopologyAsync();

        if (_sender is null)
            throw new InvalidOperationException("Sender not initialized.");

        var json = JsonSerializer.Serialize(trade);
        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = trade.InternalId.ToString(),
            Subject = QueueNames.TradeRequested
        };

        await _sender.SendMessageAsync(message, ct);

        _logger.LogInformation(
            "Published trade.requested — ExternalOrderId: {Id}", trade.ExternalOrderId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null)
            await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
