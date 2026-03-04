using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class RiskProfileRepository : IRiskProfileRepository
{
    private readonly IMongoCollection<RiskProfile> _collection;

    public RiskProfileRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<RiskProfile>("risk_profiles");

        // Unique index on PortfolioId (one profile per portfolio)
        var portfolioIndex = new CreateIndexModel<RiskProfile>(
            Builders<RiskProfile>.IndexKeys.Ascending(r => r.PortfolioId),
            new CreateIndexOptions { Unique = true }
        );
        _collection.Indexes.CreateOne(portfolioIndex);

        var userIndex = Builders<RiskProfile>.IndexKeys.Ascending(r => r.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<RiskProfile>(userIndex));
    }

    public async Task<RiskProfile?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<RiskProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<RiskProfile?> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.PortfolioId == portfolioId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(RiskProfile riskProfile, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RiskProfile>.Filter.Eq(r => r.PortfolioId, riskProfile.PortfolioId);
        await _collection.ReplaceOneAsync(filter, riskProfile, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task AddAsync(RiskProfile entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(RiskProfile entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(r => r.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(r => r.Id == id, cancellationToken);
    }
}
