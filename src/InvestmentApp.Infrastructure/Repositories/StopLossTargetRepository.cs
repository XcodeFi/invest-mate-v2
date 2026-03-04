using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class StopLossTargetRepository : IStopLossTargetRepository
{
    private readonly IMongoCollection<StopLossTarget> _collection;

    public StopLossTargetRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<StopLossTarget>("stop_loss_targets");

        // Unique index on TradeId
        var tradeIndex = new CreateIndexModel<StopLossTarget>(
            Builders<StopLossTarget>.IndexKeys.Ascending(s => s.TradeId),
            new CreateIndexOptions { Unique = true }
        );
        _collection.Indexes.CreateOne(tradeIndex);

        var portfolioIndex = Builders<StopLossTarget>.IndexKeys.Ascending(s => s.PortfolioId);
        _collection.Indexes.CreateOne(new CreateIndexModel<StopLossTarget>(portfolioIndex));

        var userIndex = Builders<StopLossTarget>.IndexKeys.Ascending(s => s.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<StopLossTarget>(userIndex));

        // Compound index for untriggered queries
        var untriggeredIndex = Builders<StopLossTarget>.IndexKeys.Combine(
            Builders<StopLossTarget>.IndexKeys.Ascending(s => s.IsStopLossTriggered),
            Builders<StopLossTarget>.IndexKeys.Ascending(s => s.IsTargetTriggered)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<StopLossTarget>(untriggeredIndex));
    }

    public async Task<StopLossTarget?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<StopLossTarget>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<StopLossTarget>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.PortfolioId == portfolioId).ToListAsync(cancellationToken);
    }

    public async Task<StopLossTarget?> GetByTradeIdAsync(string tradeId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.TradeId == tradeId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<StopLossTarget>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<StopLossTarget>.Filter.And(
            Builders<StopLossTarget>.Filter.Eq(s => s.UserId, userId),
            Builders<StopLossTarget>.Filter.Eq(s => s.IsStopLossTriggered, false),
            Builders<StopLossTarget>.Filter.Eq(s => s.IsTargetTriggered, false)
        );
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<StopLossTarget>> GetUntriggeredAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<StopLossTarget>.Filter.And(
            Builders<StopLossTarget>.Filter.Eq(s => s.IsStopLossTriggered, false),
            Builders<StopLossTarget>.Filter.Eq(s => s.IsTargetTriggered, false)
        );
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(StopLossTarget entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(StopLossTarget entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(s => s.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(s => s.Id == id, cancellationToken);
    }
}
