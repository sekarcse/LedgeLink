using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Settlement.Worker.Application.Interfaces;

/// <summary>
/// Application layer contract for ledger writes in the Settlement service.
/// Only the Settlement.Worker is allowed to set Status=Settled in MongoDB.
/// </summary>
public interface ITradeSettlementRepository
{
    /// <summary>
    /// Atomically sets Status=Settled, SharedHash, SettledAt, BlockchainTxHash and increments Version.
    /// Returns false if no document was matched (already settled or missing).
    /// </summary>
    Task<bool> MarkSettledAsync(Guid internalId, string hash, DateTime settledAt, string? txHash = null, CancellationToken ct = default);
}
