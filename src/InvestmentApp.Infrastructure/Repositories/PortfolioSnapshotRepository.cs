using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class PortfolioSnapshotRepository : IPortfolioSnapshotRepository
{
    private readonly IMongoCollection<PortfolioSnapshotEntity> _collection;

    public PortfolioSnapshotRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PortfolioSnapshotEntity>("portfolio_snapshots");

        // Compound index for time-travel queries
        var compoundIndex = Builders<PortfolioSnapshotEntity>.IndexKeys.Combine(
            Builders<PortfolioSnapshotEntity>.IndexKeys.Ascending(s => s.PortfolioId),
            Builders<PortfolioSnapshotEntity>.IndexKeys.Descending(s => s.SnapshotDate)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<PortfolioSnapshotEntity>(compoundIndex, new CreateIndexOptions { Unique = true }));
    }

    public async Task<PortfolioSnapshotEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<PortfolioSnapshotEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<PortfolioSnapshotEntity?> GetByPortfolioIdAndDateAsync(string portfolioId, DateTime date, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PortfolioSnapshotEntity>.Filter.And(
            Builders<PortfolioSnapshotEntity>.Filter.Eq(s => s.PortfolioId, portfolioId),
            Builders<PortfolioSnapshotEntity>.Filter.Eq(s => s.SnapshotDate, date.Date)
        );
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<PortfolioSnapshotEntity>> GetByPortfolioIdAsync(string portfolioId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PortfolioSnapshotEntity>.Filter.And(
            Builders<PortfolioSnapshotEntity>.Filter.Eq(s => s.PortfolioId, portfolioId),
            Builders<PortfolioSnapshotEntity>.Filter.Gte(s => s.SnapshotDate, from.Date),
            Builders<PortfolioSnapshotEntity>.Filter.Lte(s => s.SnapshotDate, to.Date)
        );
        return await _collection.Find(filter).SortBy(s => s.SnapshotDate).ToListAsync(cancellationToken);
    }

    public async Task<PortfolioSnapshotEntity?> GetLatestByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.PortfolioId == portfolioId)
            .SortByDescending(s => s.SnapshotDate)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(PortfolioSnapshotEntity entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(PortfolioSnapshotEntity entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(s => s.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(s => s.Id == id, cancellationToken);
    }

    public async Task UpsertAsync(PortfolioSnapshotEntity snapshot, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PortfolioSnapshotEntity>.Filter.And(
            Builders<PortfolioSnapshotEntity>.Filter.Eq(s => s.PortfolioId, snapshot.PortfolioId),
            Builders<PortfolioSnapshotEntity>.Filter.Eq(s => s.SnapshotDate, snapshot.SnapshotDate)
        );

        var existing = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            await _collection.ReplaceOneAsync(filter, snapshot, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.InsertOneAsync(snapshot, null, cancellationToken);
        }
    }
}
