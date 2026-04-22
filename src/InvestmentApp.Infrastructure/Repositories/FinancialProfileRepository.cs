using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

/// <summary>
/// MongoDB repo cho FinancialProfile (per-user 1:1). Unique index trên UserId đảm bảo không có duplicate profile.
/// Soft-delete pattern: filter `IsDeleted=false` trong query mặc định; `GetByUserIdIncludingDeletedAsync` trả cả deleted
/// để flow Upsert có thể restore thay vì insert mới (vi phạm unique index).
/// </summary>
public class FinancialProfileRepository : IFinancialProfileRepository
{
    private readonly IMongoCollection<FinancialProfile> _collection;

    public FinancialProfileRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<FinancialProfile>("financial_profiles");

        // Auto-generated name ("UserId_1") để idempotent với existing index nếu đã có.
        // Defensive: wrap try/catch — nếu index đã tồn tại với options khác (VD: non-unique cũ), log + bỏ qua
        // thay vì crash startup. Admin dev phải drop index cũ manual nếu cần upgrade.
        try
        {
            var userIndex = Builders<FinancialProfile>.IndexKeys.Ascending(p => p.UserId);
            _collection.Indexes.CreateOne(new CreateIndexModel<FinancialProfile>(
                userIndex, new CreateIndexOptions { Unique = true }));
        }
        catch (MongoCommandException ex) when (ex.Code is 85 or 86)
        {
            // Narrow to IndexOptionsConflict (85) / IndexKeySpecsConflict (86) only.
            // Existing index preserved; admin phải drop/recreate manual nếu thật sự cần upgrade schema.
            // Re-throw mọi exception khác (permissions, network, etc.) để không silent mask bug.
        }
    }

    public async Task<FinancialProfile?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _collection.Find(p => p.Id == id && !p.IsDeleted).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<FinancialProfile>> GetAllAsync(CancellationToken ct = default)
    {
        return await _collection.Find(p => !p.IsDeleted).ToListAsync(ct);
    }

    public async Task AddAsync(FinancialProfile entity, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task UpdateAsync(FinancialProfile entity, CancellationToken ct = default)
    {
        await _collection.ReplaceOneAsync(p => p.Id == entity.Id, entity, cancellationToken: ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        // Soft delete: chỉ set IsDeleted + UpdatedAt. Không xóa hẳn vì unique index trên UserId có thể block
        // user tạo lại profile mới.
        var update = Builders<FinancialProfile>.Update
            .Set(p => p.IsDeleted, true)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(p => p.Id == id, update, cancellationToken: ct);
    }

    public async Task<FinancialProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(p => p.UserId == userId && !p.IsDeleted).FirstOrDefaultAsync(ct);
    }

    public async Task<FinancialProfile?> GetByUserIdIncludingDeletedAsync(string userId, CancellationToken ct = default)
    {
        return await _collection.Find(p => p.UserId == userId).FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(FinancialProfile profile, CancellationToken ct = default)
    {
        // Atomic replace-or-insert dựa trên UserId. Dùng cho flows không chắc entity đã tồn tại.
        var filter = Builders<FinancialProfile>.Filter.Eq(p => p.UserId, profile.UserId);
        await _collection.ReplaceOneAsync(
            filter, profile,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken: ct);
    }
}
