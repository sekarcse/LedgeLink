using LedgeLink.Participant.UI.Application.Interfaces;
using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Participant.UI.Application.Services;

/// <summary>
/// Application service: manages the in-memory snapshot of all trades and
/// dispatches real-time change notifications to subscribed Blazor components.
///
/// Knows about: ITradeStreamRepository (application interface), TradeToken (domain).
/// Does NOT know about: MongoDB, Change Streams, Blazor, SignalR.
/// </summary>
public sealed class TradeStreamService
{
    private readonly ITradeStreamRepository _repository;
    private readonly ILogger<TradeStreamService> _logger;

    // Thread-safe in-memory snapshot — newest first
    private readonly List<TradeToken> _snapshot = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Blazor components subscribe to this event to receive live pushes.
    /// Fired on every insert or update from the Change Stream.
    /// </summary>
    public event Func<TradeToken, Task>? OnTradeChanged;

    public IReadOnlyList<TradeToken> Snapshot
    {
        get { lock (_lock) { return [.. _snapshot]; } }
    }

    public TradeStreamService(ITradeStreamRepository repository, ILogger<TradeStreamService> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    /// <summary>
    /// Loads the initial snapshot from MongoDB, then starts the Change Stream loop.
    /// This is a long-running task — call it via _ = service.StartAsync(ct) from Program.cs.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        // Load existing trades before opening the stream
        try
        {
            var existing = await _repository.GetRecentAsync(200, ct);
            lock (_lock) { _snapshot.AddRange(existing); }
            _logger.LogInformation("Snapshot loaded: {Count} trades.", existing.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial trade snapshot.");
        }

        // Start Change Stream — reconnects automatically on failure
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Opening MongoDB Change Stream...");
                await _repository.WatchAsync(HandleChangeAsync, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Change Stream disconnected. Reconnecting in 5s...");
                await Task.Delay(5_000, ct);
            }
        }
    }

    private async Task HandleChangeAsync(TradeToken trade)
    {
        // Update in-memory snapshot
        lock (_lock)
        {
            var idx = _snapshot.FindIndex(t => t.InternalId == trade.InternalId);
            if (idx >= 0)
                _snapshot[idx] = trade;
            else
                _snapshot.Insert(0, trade); // newest first
        }

        _logger.LogInformation(
            "Change Stream: {ExternalOrderId} → {Status}", trade.ExternalOrderId, trade.Status);

        // Notify all subscribers (Blazor components)
        if (OnTradeChanged is not null)
            await OnTradeChanged.Invoke(trade);
    }
}
