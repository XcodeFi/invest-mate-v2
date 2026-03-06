using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly IMongoCollection<ExchangeRate> _collection;

    public ExchangeRateRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ExchangeRate>("exchange_rates");

        var idx = Builders<ExchangeRate>.IndexKeys.Combine(
            Builders<ExchangeRate>.IndexKeys.Ascending(r => r.BaseCurrency),
            Builders<ExchangeRate>.IndexKeys.Ascending(r => r.TargetCurrency),
            Builders<ExchangeRate>.IndexKeys.Descending(r => r.Date)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<ExchangeRate>(idx));
    }

    public async Task<ExchangeRate?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.Find(r => r.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<ExchangeRate>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<ExchangeRate?> GetLatestAsync(string baseCurrency, string targetCurrency, CancellationToken cancellationToken = default)
        => await _collection
            .Find(r => r.BaseCurrency == baseCurrency.ToUpperInvariant() && r.TargetCurrency == targetCurrency.ToUpperInvariant())
            .SortByDescending(r => r.Date)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<ExchangeRate>> GetAllLatestAsync(string baseCurrency, CancellationToken cancellationToken = default)
    {
        var base_ = baseCurrency.ToUpperInvariant();
        var pipeline = _collection.Aggregate()
            .Match(r => r.BaseCurrency == base_)
            .SortByDescending(r => r.Date)
            .Group(r => r.TargetCurrency, g => new { Target = g.Key, Latest = g.First() })
            .Project(g => g.Latest);
        return await pipeline.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ExchangeRate entity, CancellationToken cancellationToken = default)
        => await _collection.InsertOneAsync(entity, null, cancellationToken);

    public async Task UpdateAsync(ExchangeRate entity, CancellationToken cancellationToken = default)
        => await _collection.ReplaceOneAsync(r => r.Id == entity.Id, entity, cancellationToken: cancellationToken);

    public async Task UpsertAsync(ExchangeRate rate, CancellationToken cancellationToken = default)
    {
        var existing = await GetLatestAsync(rate.BaseCurrency, rate.TargetCurrency, cancellationToken);
        if (existing != null && existing.Date == rate.Date)
        {
            existing.UpdateRate(rate.Rate, rate.Source);
            await UpdateAsync(existing, cancellationToken);
        }
        else
        {
            await AddAsync(rate, cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.DeleteOneAsync(r => r.Id == id, cancellationToken);
}
