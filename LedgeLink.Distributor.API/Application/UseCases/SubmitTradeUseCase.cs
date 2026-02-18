using LedgeLink.Distributor.API.Application.DTOs;
using LedgeLink.Distributor.API.Application.Interfaces;
using LedgeLink.Shared.Domain.Enums;
using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Distributor.API.Application.UseCases;

/// <summary>
/// Use Case: Submit a trade instruction.
///
/// Responsibilities (in order):
///   1. Idempotency check — return existing record if ExternalOrderId already seen
///   2. Build domain entity (TradeToken) with Status = Pending
///   3. Persist to MongoDB via ITradeRepository
///   4. Publish domain event via ITradePublisher
///
/// This class has ZERO knowledge of HTTP, MongoDB, or Service Bus.
/// It depends only on Application-layer interfaces (dependency inversion).
/// </summary>
public sealed class SubmitTradeUseCase
{
    private readonly ITradeRepository _repository;
    private readonly ITradePublisher  _publisher;
    private readonly ILogger<SubmitTradeUseCase> _logger;
    private readonly IConfiguration _config;

    public SubmitTradeUseCase(
        ITradeRepository repository,
        ITradePublisher  publisher,
        ILogger<SubmitTradeUseCase> logger,
        IConfiguration config)
    {
        _repository = repository;
        _publisher  = publisher;
        _logger     = logger;
        _config     = config;
    }

    public async Task<SubmitTradeResult> ExecuteAsync(SubmitTradeRequest request, CancellationToken ct)
    {
        // ── 1. Idempotency ───────────────────────────────────────────────────
        var existing = await _repository.FindByExternalOrderIdAsync(request.ExternalOrderId, ct);

        if (existing is not null)
        {
            _logger.LogWarning(
                "Idempotency hit — ExternalOrderId {ExternalOrderId} already exists as {TradeId}",
                request.ExternalOrderId, existing.InternalId);

            return SubmitTradeResult.Duplicate(existing);
        }

        // ── 2. Build domain entity ───────────────────────────────────────────
        var distributorName = _config["DISTRIBUTOR_NAME"] ?? "Hargreaves Lansdown";

        var trade = new TradeToken
        {
            ExternalOrderId = request.ExternalOrderId,
            Distributor     = distributorName,
            AssetManager    = request.AssetManager ?? "Schroders",
            Amount          = request.Amount,
            Status          = TradeStatus.Pending,
            Timestamp       = DateTime.UtcNow
        };

        // ── 3. Persist (write-first guarantees the record exists before the message) ──
        await _repository.InsertAsync(trade, ct);
        _logger.LogInformation(
            "Trade persisted: {TradeId} | {ExternalOrderId} | £{Amount:N2} | Status: Pending",
            trade.InternalId, trade.ExternalOrderId, trade.Amount);

        // ── 4. Publish domain event ──────────────────────────────────────────
        await _publisher.PublishTradeRequestedAsync(trade, ct);
        _logger.LogInformation(
            "Event published: trade.requested for {ExternalOrderId}", trade.ExternalOrderId);

        return SubmitTradeResult.New(trade);
    }
}

/// <summary>
/// Discriminated union result from SubmitTradeUseCase.
/// Avoids using exceptions for flow control (new vs duplicate).
/// </summary>
public sealed record SubmitTradeResult
{
    public TradeToken Trade       { get; init; } = null!;
    public bool       IsNew       { get; init; }
    public bool       IsDuplicate => !IsNew;

    public static SubmitTradeResult New(TradeToken t)       => new() { Trade = t, IsNew = true  };
    public static SubmitTradeResult Duplicate(TradeToken t) => new() { Trade = t, IsNew = false };
}
