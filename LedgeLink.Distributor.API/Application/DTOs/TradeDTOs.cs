namespace LedgeLink.Distributor.API.Application.DTOs;

/// <summary>
/// Inbound HTTP request from the client (e.g. Hargreaves Lansdown system).
/// The API layer maps this to a domain TradeToken before passing to the use case.
/// </summary>
public sealed record SubmitTradeRequest
{
    /// <example>HL-998877</example>
    public string  ExternalOrderId { get; init; } = string.Empty;

    /// <example>50000.00</example>
    public decimal Amount          { get; init; }

    /// <example>Schroders</example>
    public string? AssetManager    { get; init; }
}

/// <summary>
/// Outbound response. Always returned — whether new (201) or duplicate (200).
/// </summary>
public sealed record SubmitTradeResponse
{
    public Guid     TradeId         { get; init; }
    public string   ExternalOrderId { get; init; } = string.Empty;
    public string   Status          { get; init; } = string.Empty;
    public DateTime Timestamp       { get; init; }
    public string   Message         { get; init; } = string.Empty;
    public bool     IsDuplicate     { get; init; }
}
