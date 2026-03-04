using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class AlertHistoryRepository : IAlertHistoryRepository
{
    private readonly IMongoCollection<AlertHistory> _collection;

    public AlertHistoryRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AlertHistory>("alert_history");

        var userIndex = Builders<AlertHistory>.IndexKeys.Ascending(h => h.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<AlertHistory>(userIndex));

        var unreadIndex = Builders<AlertHistory>.IndexKeys.Combine(
            Builders<AlertHistory>.IndexKeys.Ascending(h => h.UserId),
            Builders<AlertHistory>.IndexKeys.Ascending(h => h.IsRead)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<AlertHistory>(unreadIndex));

        var dateIndex = Builders<AlertHistory>.IndexKeys.Descending(h => h.TriggeredAt);
        _collection.Indexes.CreateOne(new CreateIndexModel<AlertHistory>(dateIndex));
    }

    public async Task<AlertHistory?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(h => h.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<AlertHistory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).SortByDescending(h => h.TriggeredAt).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AlertHistory>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(h => h.UserId == userId)
            .SortByDescending(h => h.TriggeredAt)
            .Limit(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AlertHistory>> GetUnreadByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(h => h.UserId == userId && !h.IsRead)
            .SortByDescending(h => h.TriggeredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        return (int)await _collection.CountDocumentsAsync(h => h.UserId == userId && !h.IsRead, cancellationToken: cancellationToken);
    }

    public async Task AddAsync(AlertHistory entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(AlertHistory entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(h => h.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(h => h.Id == id, cancellationToken);
    }
}
