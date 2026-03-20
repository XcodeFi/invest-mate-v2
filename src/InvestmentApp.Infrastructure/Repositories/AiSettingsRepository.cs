using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class AiSettingsRepository : IAiSettingsRepository
{
    private readonly IMongoCollection<AiSettings> _collection;

    public AiSettingsRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AiSettings>("ai_settings");

        var userIndex = Builders<AiSettings>.IndexKeys.Ascending(s => s.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<AiSettings>(
            userIndex, new CreateIndexOptions { Unique = true }));
    }

    public async Task<AiSettings?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(s => s.Id == id && !s.IsDeleted).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<AiSettings>> GetAllAsync(CancellationToken ct = default)
    {
        return await _collection.Find(s => !s.IsDeleted).ToListAsync(ct);
    }

    public async Task AddAsync(AiSettings entity, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task UpdateAsync(AiSettings entity, CancellationToken ct = default)
    {
        await _collection.ReplaceOneAsync(s => s.Id == entity.Id, entity, cancellationToken: ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var update = Builders<AiSettings>.Update
            .Set(s => s.IsDeleted, true)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(s => s.Id == id, update, cancellationToken: ct);
    }

    public async Task<AiSettings?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(s => s.UserId == userId && !s.IsDeleted).FirstOrDefaultAsync(ct);
    }
}
