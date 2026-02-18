using LedgeLink.Settlement.Worker.Application.Interfaces;
using LedgeLink.Shared.Application.Services;
using LedgeLink.Shared.Domain.Enums;
using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Settlement.Worker.Application.Services;

/// <summary>
/// Application service: orchestrates the settlement of a single validated trade.
///
/// Steps:
///   1. Compute SHA-256 cryptographic hash via HashService (domain service)
///   2. Write Settled status + hash to MongoDB via ITradeSettlementRepository
///   3. Publish trade.settled event via ISettlementPublisher
///
/// Zero knowledge of MongoDB.Driver, RabbitMQ.Client, bytes, or channels.
/// </summary>
public sealed class SettleTradeService
{
    private readonly ITradeSettlementRepository _repository;
    private readonly ISettlementPublisher       _publisher;
    private readonly ILogger<SettleTradeService> _logger;

    public SettleTradeService(
        ITradeSettlementRepository repository,
        ISettlementPublisher publisher,
        ILogger<SettleTradeService> logger)
    {
        _repository = repository;
        _publisher  = publisher;
        _logger     = logger;
    }

    public async Task SettleAsync(TradeToken trade, CancellationToken ct)
    {
        _logger.LogInformation(
            "Settling {ExternalOrderId} | Amount: £{Amount:N2} | InternalId: {Id}",
            trade.ExternalOrderId, trade.Amount, trade.InternalId);

        // ── 1. Compute immutability seal ─────────────────────────────────────
        var hash      = HashService.ComputeHash(trade);
        var settledAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Hash computed for {ExternalOrderId}: {HashPreview}...",
            trade.ExternalOrderId, hash[..16]);

        // ── 2. Persist to ledger ─────────────────────────────────────────────
        var updated = await _repository.MarkSettledAsync(trade.InternalId, hash, settledAt, ct);

        if (!updated)
        {
            _logger.LogWarning(
                "No document updated for InternalId {Id} — may already be settled.", trade.InternalId);
            return;
        }

        _logger.LogInformation(
            "Trade SETTLED in ledger: {ExternalOrderId} | SettledAt: {SettledAt}",
            trade.ExternalOrderId, settledAt);

        // ── 3. Publish confirmation event ────────────────────────────────────
        trade.Status     = TradeStatus.Settled;
        trade.SharedHash = hash;
        trade.SettledAt  = settledAt;

        await _publisher.PublishTradeSettledAsync(trade, ct);
    }
}
