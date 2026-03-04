using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class TradeJournalRepository : ITradeJournalRepository
{
    private readonly IMongoCollection<TradeJournal> _collection;

    public TradeJournalRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TradeJournal>("trade_journals");

        var tradeIndex = new CreateIndexModel<TradeJournal>(
            Builders<TradeJournal>.IndexKeys.Ascending(j => j.TradeId),
            new CreateIndexOptions { Unique = true }
        );
        _collection.Indexes.CreateOne(tradeIndex);

        var userIndex = Builders<TradeJournal>.IndexKeys.Ascending(j => j.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<TradeJournal>(userIndex));

        var portfolioIndex = Builders<TradeJournal>.IndexKeys.Ascending(j => j.PortfolioId);
        _collection.Indexes.CreateOne(new CreateIndexModel<TradeJournal>(portfolioIndex));
    }

    public async Task<TradeJournal?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(j => j.Id == id && !j.IsDeleted).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<TradeJournal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(j => !j.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task<TradeJournal?> GetByTradeIdAsync(string tradeId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(j => j.TradeId == tradeId && !j.IsDeleted).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<TradeJournal>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(j => j.UserId == userId && !j.IsDeleted)
            .SortByDescending(j => j.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TradeJournal>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(j => j.PortfolioId == portfolioId && !j.IsDeleted)
            .SortByDescending(j => j.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TradeJournal entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(TradeJournal entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(j => j.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var update = Builders<TradeJournal>.Update
            .Set(j => j.IsDeleted, true)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(j => j.Id == id, update, cancellationToken: cancellationToken);
    }
}
