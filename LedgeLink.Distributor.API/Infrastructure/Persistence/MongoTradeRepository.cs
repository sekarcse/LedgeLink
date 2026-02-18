using LedgeLink.Distributor.API.Application.Interfaces;
using LedgeLink.Shared.Domain.Models;
using MongoDB.Driver;

namespace LedgeLink.Distributor.API.Infrastructure.Persistence;

/// <summary>
/// Infrastructure layer: MongoDB implementation of ITradeRepository.
///
/// This is the ONLY class in Distributor.API that knows about MongoDB.
/// The application layer never imports MongoDB.Driver — it talks to ITradeRepository.
/// </summary>
public sealed class MongoTradeRepository : ITradeRepository
{
    private readonly IMongoCollection<TradeToken> _collection;
    private readonly ILogger<MongoTradeRepository> _logger;

    public MongoTradeRepository(IMongoClient mongoClient, ILogger<MongoTradeRepository> logger)
    {
        _logger     = logger;
        var db      = mongoClient.GetDatabase("ledgelink");
        _collection = db.GetCollection<TradeToken>("trades");
    }

    /// <summary>
    /// Ensures a unique index on ExternalOrderId exists.
    /// Creates it once at startup — idempotent (safe to call multiple times).
    /// </summary>
    public async Task EnsureIndexesAsync()
    {
        var model = new CreateIndexModel<TradeToken>(
            Builders<TradeToken>.IndexKeys.Ascending(t => t.ExternalOrderId),
            new CreateIndexOptions { Unique = true, Name = "idx_externalOrderId_unique" }
        );
        await _collection.Indexes.CreateOneAsync(model);
        _logger.LogInformation("MongoDB unique index on ExternalOrderId ensured.");
    }

    public async Task<TradeToken?> FindByExternalOrderIdAsync(string externalOrderId, CancellationToken ct = default)
        => await _collection
            .Find(t => t.ExternalOrderId == externalOrderId)
            .FirstOrDefaultAsync(ct);

    public async Task<TradeToken?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _collection
            .Find(t => t.InternalId == id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TradeToken>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
    {
        var list = await _collection
            .Find(_ => true)
            .SortByDescending(t => t.Timestamp)
            .Limit(limit)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task InsertAsync(TradeToken trade, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(trade, cancellationToken: ct);
        _logger.LogInformation(
            "Inserted trade {Id} ({ExternalOrderId})", trade.InternalId, trade.ExternalOrderId);
    }
}
