using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LedgeLink.Shared.Application.Interfaces;
using LedgeLink.Shared.Domain.Models;
using LedgeLink.Validator.Worker.Application.Interfaces;

namespace LedgeLink.Validator.Worker.Infrastructure.Messaging;

/// <summary>
/// Infrastructure layer: Azure Service Bus implementation of IMessagePublisher.
/// Only this class knows about Service Bus in the Validator service.
/// </summary>
public sealed class ServiceBusMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusMessagePublisher> _logger;
    private readonly Dictionary<string, ServiceBusSender> _senders = new();
    private bool _topologyReady;

    public ServiceBusMessagePublisher(ServiceBusClient client, ILogger<ServiceBusMessagePublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task EnsureTopologyAsync(CancellationToken ct = default)
    {
        if (_topologyReady) return;

        // Create senders for all queue names
        foreach (var queueName in QueueNames.All)
        {
            _senders[queueName] = _client.CreateSender(queueName);
        }

        _topologyReady = true;
        _logger.LogInformation("Service Bus senders ready for {QueueCount} queues", QueueNames.All.Length);
        await Task.CompletedTask;
    }

    public async Task PublishAsync(TradeToken trade, string routingKey, CancellationToken ct = default)
    {
        if (!_topologyReady)
            throw new InvalidOperationException("Senders not initialized. Call EnsureTopologyAsync first.");

        if (!_senders.TryGetValue(routingKey, out var sender))
            throw new InvalidOperationException($"Sender not found for routing key: {routingKey}");

        var json = JsonSerializer.Serialize(trade);
        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = trade.InternalId.ToString(),
            Subject = routingKey
        };

        await sender.SendMessageAsync(message, ct);

        _logger.LogInformation("Published {RoutingKey} for {ExternalOrderId}", routingKey, trade.ExternalOrderId);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
        await _client.DisposeAsync();
    }
}
