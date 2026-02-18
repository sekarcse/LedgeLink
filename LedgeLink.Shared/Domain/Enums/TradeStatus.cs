namespace LedgeLink.Shared.Domain.Enums;

/// <summary>
/// The state machine for a TradeToken as it flows through the settlement pipeline.
/// Only the Settlement.Worker may set Settled. Only the Validator.Worker may set Rejected.
/// </summary>
public enum TradeStatus
{
    Pending,    // Set by Distributor.API on receipt
    Validated,  // Set by Validator.Worker — passes all business rules
    Rejected,   // Set by Validator.Worker — fails a business rule
    Settled     // Set by Settlement.Worker — cryptographic hash applied
}
