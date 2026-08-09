using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class CorporateActionRepository : ICorporateActionRepository
{
    private readonly IMongoCollection<CorporateAction> _collection;

    public CorporateActionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CorporateAction>("corporate_actions");

        var portfolioIndex = Builders<CorporateAction>.IndexKeys.Ascending(c => c.PortfolioId);
        _collection.Indexes.CreateOne(new CreateIndexModel<CorporateAction>(portfolioIndex));

        var compoundIndex = Builders<CorporateAction>.IndexKeys.Combine(
            Builders<CorporateAction>.IndexKeys.Ascending(c => c.PortfolioId),
            Builders<CorporateAction>.IndexKeys.Ascending(c => c.Symbol),
            Builders<CorporateAction>.IndexKeys.Ascending(c => c.ExDate)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<CorporateAction>(compoundIndex));
    }

    public async Task<CorporateAction?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.Find(c => c.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<CorporateAction>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public async Task AddAsync(CorporateAction entity, CancellationToken cancellationToken = default)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(CorporateAction entity, CancellationToken cancellationToken = default)
        => await _collection.ReplaceOneAsync(c => c.Id == entity.Id, entity, cancellationToken: cancellationToken);

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.DeleteOneAsync(c => c.Id == id, cancellationToken);

    public async Task<IEnumerable<CorporateAction>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
        => await _collection.Find(c => c.PortfolioId == portfolioId)
            .SortByDescending(c => c.ExDate)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<CorporateAction>> GetByPortfolioIdAndSymbolAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToUpper().Trim();
        return await _collection.Find(c => c.PortfolioId == portfolioId && c.Symbol == normalized)
            .SortBy(c => c.ExDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CorporateAction>> GetByPortfolioIdsAsync(IEnumerable<string> portfolioIds, CancellationToken cancellationToken = default)
    {
        var ids = portfolioIds.ToList();
        if (ids.Count == 0) return Array.Empty<CorporateAction>();

        var filter = Builders<CorporateAction>.Filter.In(c => c.PortfolioId, ids);
        return await _collection.Find(filter).SortBy(c => c.ExDate).ToListAsync(cancellationToken);
    }
}
