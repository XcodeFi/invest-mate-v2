using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class TradePlanRepository : ITradePlanRepository
{
    private readonly IMongoCollection<TradePlan> _collection;

    public TradePlanRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TradePlan>("trade_plans");

        var userIndex = Builders<TradePlan>.IndexKeys.Ascending(p => p.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<TradePlan>(userIndex));

        var statusIndex = Builders<TradePlan>.IndexKeys.Combine(
            Builders<TradePlan>.IndexKeys.Ascending(p => p.UserId),
            Builders<TradePlan>.IndexKeys.Ascending(p => p.Status),
            Builders<TradePlan>.IndexKeys.Ascending(p => p.IsDeleted)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<TradePlan>(statusIndex));

        var tradeIndex = Builders<TradePlan>.IndexKeys.Ascending(p => p.TradeId);
        _collection.Indexes.CreateOne(new CreateIndexModel<TradePlan>(tradeIndex));
    }

    public async Task<TradePlan?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(p => p.Id == id && !p.IsDeleted).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<TradePlan>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(p => !p.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TradePlan>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(p => p.UserId == userId && !p.IsDeleted)
            .SortByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<TradePlan?> GetByTradeIdAsync(string tradeId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(p => p.TradeId == tradeId && !p.IsDeleted).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<TradePlan>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(p =>
                p.UserId == userId && !p.IsDeleted &&
                p.Status != TradePlanStatus.Cancelled &&
                p.Status != TradePlanStatus.Reviewed)
            .SortByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<TradePlan?> GetActiveByPortfolioAndSymbolAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(p =>
                p.PortfolioId == portfolioId &&
                p.Symbol == symbol &&
                !p.IsDeleted &&
                (p.Status == TradePlanStatus.Ready || p.Status == TradePlanStatus.InProgress))
            .SortByDescending(p => p.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<TradePlan>> GetAdvancedInProgressAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(p =>
                p.ExitStrategyMode == ExitStrategyMode.Advanced &&
                p.Status == TradePlanStatus.InProgress &&
                !p.IsDeleted &&
                p.ScenarioNodes != null)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TradePlan entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(TradePlan entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(p => p.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var update = Builders<TradePlan>.Update
            .Set(p => p.IsDeleted, true)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(p => p.Id == id, update, cancellationToken: cancellationToken);
    }
}
