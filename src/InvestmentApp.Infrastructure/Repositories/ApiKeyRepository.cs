using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly IMongoCollection<ApiKey> _collection;

    public ApiKeyRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ApiKey>("api_keys");

        _collection.Indexes.CreateOne(new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(k => k.KeyHash),
            new CreateIndexOptions { Unique = true }));
        _collection.Indexes.CreateOne(new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(k => k.UserId)));
    }

    public async Task<ApiKey?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(k => k.Id == id).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<ApiKey>> GetAllAsync(CancellationToken ct = default)
    {
        return await _collection.Find(_ => true).ToListAsync(ct);
    }

    public async Task AddAsync(ApiKey entity, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task UpdateAsync(ApiKey entity, CancellationToken ct = default)
    {
        await _collection.ReplaceOneAsync(k => k.Id == entity.Id, entity, cancellationToken: ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _collection.DeleteOneAsync(k => k.Id == id, ct);
    }

    public async Task<IEnumerable<ApiKey>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(k => k.UserId == userId).ToListAsync(ct);
    }

    public async Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default)
    {
        return await _collection.Find(k => k.KeyHash == keyHash).FirstOrDefaultAsync(ct);
    }
}
