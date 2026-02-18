using LedgeLink.Participant.UI.Application.Interfaces;
using LedgeLink.Shared.Domain.Models;
using MongoDB.Driver;

namespace LedgeLink.Participant.UI.Infrastructure.Persistence;

/// <summary>
/// Infrastructure layer: MongoDB implementation of ITradeStreamRepository.
/// The ONLY class in Participant.UI that imports MongoDB.Driver.
/// Implements both the initial query and the real-time Change Stream watch.
/// </summary>
public sealed class MongoTradeStreamRepository : ITradeStreamRepository
{
    private readonly IMongoCollection<TradeToken> _collection;
    private readonly ILogger<MongoTradeStreamRepository> _logger;

    public MongoTradeStreamRepository(IMongoClient mongoClient, ILogger<MongoTradeStreamRepository> logger)
    {
        _logger     = logger;
        var db      = mongoClient.GetDatabase("ledgelink");
        _collection = db.GetCollection<TradeToken>("trades");
    }

    public async Task<IReadOnlyList<TradeToken>> GetRecentAsync(int limit = 200, CancellationToken ct = default)
    {
        var list = await _collection
            .Find(_ => true)
            .SortByDescending(t => t.Timestamp)
            .Limit(limit)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task WatchAsync(Func<TradeToken, Task> onChanged, CancellationToken ct)
    {
        var pipeline = new EmptyPipelineDefinition<ChangeStreamDocument<TradeToken>>()
            .Match(c =>
                c.OperationType == ChangeStreamOperationType.Insert  ||
                c.OperationType == ChangeStreamOperationType.Update  ||
                c.OperationType == ChangeStreamOperationType.Replace);

        var options = new ChangeStreamOptions
        {
            FullDocument = ChangeStreamFullDocumentOption.UpdateLookup
        };

        using var cursor = await _collection.WatchAsync(pipeline, options, ct);

        await cursor.ForEachAsync(async change =>
        {
            if (change.FullDocument is { } trade)
                await onChanged(trade);
        }, ct);
    }
}
