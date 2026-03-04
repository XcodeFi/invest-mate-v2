using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;

namespace InvestmentApp.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IMongoCollection<AuditEntry> _collection;

    public AuditService(IMongoDatabase database)
    {
        _collection = database.GetCollection<AuditEntry>("auditLogs");

        // Create indexes
        var userIndex = Builders<AuditEntry>.IndexKeys.Ascending(a => a.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<AuditEntry>(userIndex));

        var timestampIndex = Builders<AuditEntry>.IndexKeys.Descending(a => a.Timestamp);
        _collection.Indexes.CreateOne(new CreateIndexModel<AuditEntry>(timestampIndex));
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entry, null, cancellationToken);
    }
}