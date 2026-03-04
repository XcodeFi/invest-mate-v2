using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class CapitalFlowRepository : ICapitalFlowRepository
{
    private readonly IMongoCollection<CapitalFlow> _collection;

    public CapitalFlowRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CapitalFlow>("capital_flows");

        // Indexes
        var portfolioIndex = Builders<CapitalFlow>.IndexKeys.Ascending(c => c.PortfolioId);
        _collection.Indexes.CreateOne(new CreateIndexModel<CapitalFlow>(portfolioIndex));

        var compoundIndex = Builders<CapitalFlow>.IndexKeys.Combine(
            Builders<CapitalFlow>.IndexKeys.Ascending(c => c.PortfolioId),
            Builders<CapitalFlow>.IndexKeys.Descending(c => c.FlowDate)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<CapitalFlow>(compoundIndex));

        var userIndex = Builders<CapitalFlow>.IndexKeys.Ascending(c => c.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<CapitalFlow>(userIndex));
    }

    public async Task<CapitalFlow?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(c => c.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<CapitalFlow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CapitalFlow>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(c => c.PortfolioId == portfolioId)
            .SortByDescending(c => c.FlowDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CapitalFlow>> GetByPortfolioIdAsync(string portfolioId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CapitalFlow>.Filter.And(
            Builders<CapitalFlow>.Filter.Eq(c => c.PortfolioId, portfolioId),
            Builders<CapitalFlow>.Filter.Gte(c => c.FlowDate, from.Date),
            Builders<CapitalFlow>.Filter.Lte(c => c.FlowDate, to.Date)
        );
        return await _collection.Find(filter).SortBy(c => c.FlowDate).ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalFlowByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var flows = await GetByPortfolioIdAsync(portfolioId, cancellationToken);
        return flows.Sum(f => f.SignedAmount);
    }

    public async Task AddAsync(CapitalFlow entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(CapitalFlow entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(c => c.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(c => c.Id == id, cancellationToken);
    }
}
