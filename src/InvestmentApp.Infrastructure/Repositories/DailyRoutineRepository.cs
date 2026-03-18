using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class DailyRoutineRepository : IDailyRoutineRepository
{
    private readonly IMongoCollection<DailyRoutine> _collection;

    public DailyRoutineRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<DailyRoutine>("daily_routines");

        // Drop old unique index if it exists (was created with Unique=true, incompatible with soft-delete)
        try { _collection.Indexes.DropOne("UserId_1_Date_1"); } catch { /* already gone */ }

        // Compound index for fast lookup by user + date (uniqueness enforced in application layer)
        var compoundIndex = Builders<DailyRoutine>.IndexKeys.Combine(
            Builders<DailyRoutine>.IndexKeys.Ascending(r => r.UserId),
            Builders<DailyRoutine>.IndexKeys.Ascending(r => r.Date));
        _collection.Indexes.CreateOne(new CreateIndexModel<DailyRoutine>(compoundIndex));

        // UserId index for history queries
        var userIndex = Builders<DailyRoutine>.IndexKeys.Ascending(r => r.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<DailyRoutine>(userIndex));
    }

    public async Task<DailyRoutine?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(r => r.Id == id && !r.IsDeleted).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<DailyRoutine>> GetAllAsync(CancellationToken ct = default)
    {
        return await _collection.Find(r => !r.IsDeleted).ToListAsync(ct);
    }

    public async Task AddAsync(DailyRoutine entity, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task UpdateAsync(DailyRoutine entity, CancellationToken ct = default)
    {
        await _collection.ReplaceOneAsync(r => r.Id == entity.Id, entity, cancellationToken: ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var update = Builders<DailyRoutine>.Update
            .Set(r => r.IsDeleted, true)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(r => r.Id == id, update, cancellationToken: ct);
    }

    public async Task<DailyRoutine?> GetByUserIdAndDateAsync(string userId, DateTime date, CancellationToken ct = default)
    {
        var dateOnly = date.Date;
        return await _collection.Find(r => r.UserId == userId && r.Date == dateOnly && !r.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DailyRoutine?> GetAnyByUserIdAndDateAsync(string userId, DateTime date, CancellationToken ct = default)
    {
        var dateOnly = date.Date;
        return await _collection.Find(r => r.UserId == userId && r.Date == dateOnly)
            .FirstOrDefaultAsync(ct);
    }

    public async Task HardDeleteAsync(string id, CancellationToken ct = default)
    {
        await _collection.DeleteOneAsync(r => r.Id == id, cancellationToken: ct);
    }

    public async Task<IEnumerable<DailyRoutine>> GetByUserIdRangeAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var fromDate = from.Date;
        var toDate = to.Date;
        return await _collection.Find(r => r.UserId == userId && r.Date >= fromDate && r.Date <= toDate && !r.IsDeleted)
            .SortByDescending(r => r.Date)
            .ToListAsync(ct);
    }

    public async Task<DailyRoutine?> GetLatestByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(r => r.UserId == userId && !r.IsDeleted)
            .SortByDescending(r => r.Date)
            .FirstOrDefaultAsync(ct);
    }
}
