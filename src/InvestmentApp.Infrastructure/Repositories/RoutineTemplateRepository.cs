using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class RoutineTemplateRepository : IRoutineTemplateRepository
{
    private readonly IMongoCollection<RoutineTemplate> _collection;

    public RoutineTemplateRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<RoutineTemplate>("routine_templates");

        var sortIndex = Builders<RoutineTemplate>.IndexKeys.Ascending(t => t.SortOrder);
        _collection.Indexes.CreateOne(new CreateIndexModel<RoutineTemplate>(sortIndex));
    }

    public async Task<RoutineTemplate?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(t => t.Id == id && !t.IsDeleted).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<RoutineTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        return await _collection.Find(t => !t.IsDeleted).SortBy(t => t.SortOrder).ToListAsync(ct);
    }

    public async Task AddAsync(RoutineTemplate entity, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task UpdateAsync(RoutineTemplate entity, CancellationToken ct = default)
    {
        await _collection.ReplaceOneAsync(t => t.Id == entity.Id, entity, cancellationToken: ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var update = Builders<RoutineTemplate>.Update
            .Set(t => t.IsDeleted, true)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(t => t.Id == id, update, cancellationToken: ct);
    }

    public async Task<IEnumerable<RoutineTemplate>> GetAllForUserAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(t => (t.UserId == null || t.UserId == userId) && !t.IsDeleted)
            .SortBy(t => t.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<RoutineTemplate>> GetBuiltInAsync(CancellationToken ct = default)
    {
        return await _collection.Find(t => t.UserId == null && !t.IsDeleted)
            .SortBy(t => t.SortOrder)
            .ToListAsync(ct);
    }
}
