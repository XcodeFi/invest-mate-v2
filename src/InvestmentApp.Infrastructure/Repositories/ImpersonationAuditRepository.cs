using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class ImpersonationAuditRepository : IImpersonationAuditRepository
{
    private readonly IMongoCollection<ImpersonationAudit> _collection;

    public ImpersonationAuditRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ImpersonationAudit>("impersonationAudits");

        var adminIndex = Builders<ImpersonationAudit>.IndexKeys
            .Ascending(a => a.AdminUserId)
            .Descending(a => a.StartedAt);
        _collection.Indexes.CreateOne(new CreateIndexModel<ImpersonationAudit>(adminIndex));

        var targetIndex = Builders<ImpersonationAudit>.IndexKeys
            .Ascending(a => a.TargetUserId)
            .Descending(a => a.StartedAt);
        _collection.Indexes.CreateOne(new CreateIndexModel<ImpersonationAudit>(targetIndex));

        var revokedIndex = Builders<ImpersonationAudit>.IndexKeys
            .Ascending(a => a.Id)
            .Ascending(a => a.IsRevoked);
        _collection.Indexes.CreateOne(new CreateIndexModel<ImpersonationAudit>(revokedIndex));
    }

    public async Task<ImpersonationAudit?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ImpersonationAudit>.Filter.Eq(a => a.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<ImpersonationAudit>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(Builders<ImpersonationAudit>.Filter.Empty).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ImpersonationAudit>> GetActiveByAdminAsync(string adminUserId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ImpersonationAudit>.Filter.And(
            Builders<ImpersonationAudit>.Filter.Eq(a => a.AdminUserId, adminUserId),
            Builders<ImpersonationAudit>.Filter.Eq(a => a.IsRevoked, false),
            Builders<ImpersonationAudit>.Filter.Eq(a => a.EndedAt, null)
        );
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ImpersonationAudit entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(ImpersonationAudit entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ImpersonationAudit>.Filter.Eq(a => a.Id, entity.Id);
        await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ImpersonationAudit>.Filter.Eq(a => a.Id, id);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }
}
