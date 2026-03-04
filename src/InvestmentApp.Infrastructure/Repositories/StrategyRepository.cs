using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class StrategyRepository : IStrategyRepository
{
    private readonly IMongoCollection<Strategy> _collection;

    public StrategyRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Strategy>("strategies");

        var userIndex = Builders<Strategy>.IndexKeys.Ascending(s => s.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<Strategy>(userIndex));

        var activeIndex = Builders<Strategy>.IndexKeys.Combine(
            Builders<Strategy>.IndexKeys.Ascending(s => s.UserId),
            Builders<Strategy>.IndexKeys.Ascending(s => s.IsActive),
            Builders<Strategy>.IndexKeys.Ascending(s => s.IsDeleted)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<Strategy>(activeIndex));
    }

    public async Task<Strategy?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.Id == id && !s.IsDeleted).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Strategy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => !s.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Strategy>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.UserId == userId && !s.IsDeleted)
            .SortByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Strategy>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.UserId == userId && s.IsActive && !s.IsDeleted)
            .SortByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Strategy entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(Strategy entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(s => s.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        // Soft delete
        var update = Builders<Strategy>.Update
            .Set(s => s.IsDeleted, true)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(s => s.Id == id, update, cancellationToken: cancellationToken);
    }
}
