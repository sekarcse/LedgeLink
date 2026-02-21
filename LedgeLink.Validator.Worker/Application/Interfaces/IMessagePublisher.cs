using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Validator.Worker.Application.Interfaces;

/// <summary>Application layer messaging contract for the Validator worker.</summary>
public interface IMessagePublisher
{
    Task EnsureTopologyAsync(CancellationToken ct = default);
    Task PublishAsync(TradeToken trade, string routingKey, CancellationToken ct = default);
}
