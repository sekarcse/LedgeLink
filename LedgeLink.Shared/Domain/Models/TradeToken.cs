using LedgeLink.Shared.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LedgeLink.Shared.Domain.Models;

/// <summary>
/// The canonical domain entity representing a single trade instruction.
/// This object flows through every service — as a MongoDB document, as a
/// Service Bus message body (JSON), and as the UI view model.
///
/// Domain layer: no business logic here — only data shape + invariants.
/// </summary>
public class TradeToken
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid InternalId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Client-supplied idempotency key. Must be unique across all trades.
    /// A unique index on this field enforces the guarantee at the DB level.
    /// </summary>
    [BsonElement("externalOrderId")]
    public string ExternalOrderId { get; init; } = string.Empty;

    [BsonElement("distributor")]
    public string Distributor { get; init; } = string.Empty;

    [BsonElement("assetManager")]
    public string AssetManager { get; init; } = string.Empty;

    [BsonElement("amount")]
    public decimal Amount { get; init; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public TradeStatus Status { get; set; } = TradeStatus.Pending;

    /// <summary>
    /// SHA-256 hash of (ExternalOrderId + Amount + Timestamp).
    /// Null until Settlement.Worker processes the trade.
    /// </summary>
    [BsonElement("sharedHash")]
    public string? SharedHash { get; set; }

    /// <summary>Optimistic concurrency version — incremented by Settlement.Worker.</summary>
    [BsonElement("version")]
    public int Version { get; set; } = 1;

    /// <summary>UTC — set by Distributor.API at the moment of receipt.</summary>
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>UTC — set by Settlement.Worker when the trade is sealed.</summary>
    [BsonElement("settledAt")]
    public DateTime? SettledAt { get; set; }

    /// <summary>Set by Validator.Worker when a business rule is violated.</summary>
    [BsonElement("rejectionReason")]
    public string? RejectionReason { get; set; }

    /// <summary>Ethereum transaction hash — set by Settlement.Worker after anchoring.</summary>
    [BsonElement("txHash")]
    public string? BlockchainTxHash { get; set; }
}
