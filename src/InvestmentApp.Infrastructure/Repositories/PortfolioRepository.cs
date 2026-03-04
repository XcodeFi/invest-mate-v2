using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;

namespace InvestmentApp.Infrastructure.Repositories;

public class PortfolioRepository : IPortfolioRepository
{
    private readonly IMongoCollection<Portfolio> _collection;

    public PortfolioRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Portfolio>("portfolios");

        // Create indexes
        var indexKeys = Builders<Portfolio>.IndexKeys.Ascending(p => p.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<Portfolio>(indexKeys));

        var deletedIndex = Builders<Portfolio>.IndexKeys.Ascending(p => p.IsDeleted);
        _collection.Indexes.CreateOne(new CreateIndexModel<Portfolio>(deletedIndex));
    }

    public async Task<Portfolio?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Portfolio>.Filter.And(
            Builders<Portfolio>.Filter.Eq(p => p.Id, id),
            Builders<Portfolio>.Filter.Eq(p => p.IsDeleted, false)
        );
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Portfolio>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<Portfolio>.Filter.Eq(p => p.IsDeleted, false);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Portfolio>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Portfolio>.Filter.And(
            Builders<Portfolio>.Filter.Eq(p => p.UserId, userId),
            Builders<Portfolio>.Filter.Eq(p => p.IsDeleted, false)
        );
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<Portfolio?> GetByIdWithTradesAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Portfolio>.Filter.And(
            Builders<Portfolio>.Filter.Eq(p => p.Id, id),
            Builders<Portfolio>.Filter.Eq(p => p.IsDeleted, false)
        );
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Portfolio entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(Portfolio entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Portfolio>.Filter.Eq(p => p.Id, entity.Id);
        await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Portfolio>.Filter.Eq(p => p.Id, id);
        var update = Builders<Portfolio>.Update.Set(p => p.IsDeleted, true);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}