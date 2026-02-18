using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Validator.Worker.Domain.Rules;

/// <summary>
/// Domain layer: base contract for a single business validation rule.
/// Each rule is independently testable. Rules compose via TradeValidationService.
/// </summary>
public abstract class ValidationRule
{
    public abstract string RuleName { get; }

    /// <summary>
    /// Returns null on pass, or a human-readable rejection reason on fail.
    /// </summary>
    public abstract string? Validate(TradeToken trade);
}

// ── Concrete Rules ─────────────────────────────────────────────────────────

public sealed class ExternalOrderIdRequiredRule : ValidationRule
{
    public override string RuleName => "ExternalOrderId_Required";
    public override string? Validate(TradeToken t)
        => string.IsNullOrWhiteSpace(t.ExternalOrderId)
            ? "ExternalOrderId must not be empty."
            : null;
}

public sealed class AmountPositiveRule : ValidationRule
{
    public override string RuleName => "Amount_MustBePositive";
    public override string? Validate(TradeToken t)
        => t.Amount <= 0
            ? $"Amount must be greater than zero. Received: {t.Amount}"
            : null;
}

public sealed class AmountMaximumRule : ValidationRule
{
    private const decimal MaxSingleTrade = 100_000_000m;
    public override string RuleName => "Amount_BelowMaximum";
    public override string? Validate(TradeToken t)
        => t.Amount > MaxSingleTrade
            ? $"Amount {t.Amount:C} exceeds the single-trade maximum of {MaxSingleTrade:C}."
            : null;
}

public sealed class DistributorRequiredRule : ValidationRule
{
    public override string RuleName => "Distributor_Required";
    public override string? Validate(TradeToken t)
        => string.IsNullOrWhiteSpace(t.Distributor)
            ? "Distributor name must not be empty."
            : null;
}

public sealed class AssetManagerRequiredRule : ValidationRule
{
    public override string RuleName => "AssetManager_Required";
    public override string? Validate(TradeToken t)
        => string.IsNullOrWhiteSpace(t.AssetManager)
            ? "AssetManager name must not be empty."
            : null;
}

public sealed class TimestampNotFutureRule : ValidationRule
{
    public override string RuleName => "Timestamp_NotInFuture";
    public override string? Validate(TradeToken t)
        => t.Timestamp > DateTime.UtcNow.AddMinutes(5)
            ? $"Trade timestamp {t.Timestamp:O} is more than 5 minutes in the future."
            : null;
}
