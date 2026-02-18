namespace LedgeLink.Shared.Application.Interfaces;

/// <summary>
/// Centralised Service Bus queue/topic name constants.
/// All producers and consumers reference these — never hardcode queue name strings.
/// </summary>
public static class QueueNames
{
    public const string Exchange       = "ledgelink.exchange";
    public const string TradeRequested = "trade.requested";
    public const string TradeValidated = "trade.validated";
    public const string TradeRejected  = "trade.rejected";
    public const string TradeSettled   = "trade.settled";

    public static readonly string[] All =
    [
        TradeRequested,
        TradeValidated,
        TradeRejected,
        TradeSettled
    ];
}
