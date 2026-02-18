using LedgeLink.Shared.Domain.Models;

namespace LedgeLink.Participant.UI.Application.Interfaces;

/// <summary>
/// Application layer contract for reading trade data.
/// The Blazor components and application services depend on this — never on MongoDB directly.
/// </summary>
public interface ITradeStreamRepository
{
    Task<IReadOnlyList<TradeToken>> GetRecentAsync(int limit = 200, CancellationToken ct = default);

    /// <summary>
    /// Starts a long-running Change Stream watch.
    /// Calls <paramref name="onChanged"/> every time a trade is inserted or updated.
    /// </summary>
    Task WatchAsync(Func<TradeToken, Task> onChanged, CancellationToken ct);
}
