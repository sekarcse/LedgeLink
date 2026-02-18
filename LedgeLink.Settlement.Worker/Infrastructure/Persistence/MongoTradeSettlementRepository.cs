using LedgeLink.Settlement.Worker.Application.Interfaces;
using LedgeLink.Shared.Domain.Enums;
using LedgeLink.Shared.Domain.Models;
using MongoDB.Driver;

namespace LedgeLink.Settlement.Worker.Infrastructure.Persistence;

/// <summary>
/// Infrastructure layer: MongoDB implementation of ITradeSettlementRepository.
/// Only this class imports MongoDB.Driver in the Settlement service.
/// </summary>
public sealed class MongoTradeSettlementRepository : ITradeSettlementRepository
{
    private readonly IMongoCollection<TradeToken> _collection;
    private readonly ILogger<MongoTradeSettlementRepository> _logger;

    public MongoTradeSettlementRepository(IMongoClient mongoClient, ILogger<MongoTradeSettlementRepository> logger)
    {
        _logger     = logger;
        var db      = mongoClient.GetDatabase("ledgelink");
        _collection = db.GetCollection<TradeToken>("trades");
    }

    public async Task<bool> MarkSettledAsync(Guid internalId, string hash, DateTime settledAt, CancellationToken ct = default)
    {
        var filter = Builders<TradeToken>.Filter.Eq(t => t.InternalId, internalId);
        var update  = Builders<TradeToken>.Update
            .Set(t => t.Status,     TradeStatus.Settled)
            .Set(t => t.SharedHash, hash)
            .Set(t => t.SettledAt,  settledAt)
            .Inc(t => t.Version,    1);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);

        _logger.LogInformation(
            "MongoDB update for {Id}: matched={M} modified={Mod}",
            internalId, result.MatchedCount, result.ModifiedCount);

        return result.ModifiedCount > 0;
    }
}
