using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class AlertRuleRepository : IAlertRuleRepository
{
    private readonly IMongoCollection<AlertRule> _collection;

    public AlertRuleRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AlertRule>("alert_rules");

        var userIndex = Builders<AlertRule>.IndexKeys.Ascending(r => r.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<AlertRule>(userIndex));

        var activeIndex = Builders<AlertRule>.IndexKeys.Combine(
            Builders<AlertRule>.IndexKeys.Ascending(r => r.IsActive),
            Builders<AlertRule>.IndexKeys.Ascending(r => r.IsDeleted)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<AlertRule>(activeIndex));
    }

    public async Task<AlertRule?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.Id == id && !r.IsDeleted).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => !r.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AlertRule>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.UserId == userId && !r.IsDeleted)
            .SortByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AlertRule>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.UserId == userId && r.IsActive && !r.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AlertRule>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.IsActive && !r.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AlertRule entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(AlertRule entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(r => r.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var update = Builders<AlertRule>.Update
            .Set(r => r.IsDeleted, true)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(r => r.Id == id, update, cancellationToken: cancellationToken);
    }
}
