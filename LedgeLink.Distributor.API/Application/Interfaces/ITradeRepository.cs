using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Distributor.API.Application.Interfaces;

/// <summary>
/// Application layer contract for trade persistence.
/// The controller and use cases depend ONLY on this interface — never on MongoDB directly.
/// Infrastructure layer provides the concrete implementation.
/// </summary>
public interface ITradeRepository
{
    /// <summary>Returns null if the ExternalOrderId has never been seen (new trade).</summary>
    Task<TradeToken?> FindByExternalOrderIdAsync(string externalOrderId, CancellationToken ct = default);

    Task<TradeToken?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TradeToken>> GetRecentAsync(int limit = 50, CancellationToken ct = default);

    Task InsertAsync(TradeToken trade, CancellationToken ct = default);
}
