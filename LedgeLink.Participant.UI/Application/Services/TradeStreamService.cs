using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Participant.UI.Application.Services;

/// <summary>
/// Application service: manages the in-memory snapshot of all trades and
/// dispatches real-time change notifications to subscribed Blazor components.
///
/// No longer depends on MongoDB or Change Streams.
/// Updates are pushed in by ServiceBusTradeListener.
/// </summary>
public sealed class TradeStreamService
{
    private readonly ILogger<TradeStreamService> _logger;

    private readonly List<TradeToken> _snapshot = [];
    private readonly Lock _lock = new();

    public event Func<TradeToken, Task>? OnTradeChanged;

    public IReadOnlyList<TradeToken> Snapshot
    {
        get { lock (_lock) { return [.. _snapshot]; } }
    }

    public TradeStreamService(ILogger<TradeStreamService> logger)
    {
        _logger = logger;
    }

    public async Task UpdateTrade(TradeToken trade)
    {
        lock (_lock)
        {
            var idx = _snapshot.FindIndex(t => t.InternalId == trade.InternalId);
            if (idx >= 0)
                _snapshot[idx] = trade;
            else
                _snapshot.Insert(0, trade); // newest first
        }

        _logger.LogInformation(
            "Trade updated: {ExternalOrderId} → {Status}", trade.ExternalOrderId, trade.Status);

        if (OnTradeChanged is not null)
            await OnTradeChanged.Invoke(trade);
    }
}