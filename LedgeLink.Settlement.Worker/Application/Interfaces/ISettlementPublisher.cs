using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Settlement.Worker.Application.Interfaces;

public interface ISettlementPublisher
{
    Task PublishTradeSettledAsync(TradeToken trade, CancellationToken ct = default);
}
