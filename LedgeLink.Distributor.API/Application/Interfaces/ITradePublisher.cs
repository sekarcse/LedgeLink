using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Distributor.API.Application.Interfaces;

/// <summary>
/// Application layer contract for publishing domain events to the message bus.
/// Use cases depend on this abstraction — not on Azure.Messaging.ServiceBus directly.
/// </summary>
public interface ITradePublisher
{
    Task PublishTradeRequestedAsync(TradeToken trade, CancellationToken ct = default);
}
