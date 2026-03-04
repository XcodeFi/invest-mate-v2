using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class StockPriceRepository : IStockPriceRepository
{
    private readonly IMongoCollection<StockPrice> _collection;

    public StockPriceRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<StockPrice>("stock_prices");

        // Compound index for symbol + date queries
        var compoundIndex = Builders<StockPrice>.IndexKeys.Combine(
            Builders<StockPrice>.IndexKeys.Ascending(s => s.Symbol),
            Builders<StockPrice>.IndexKeys.Descending(s => s.Date)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<StockPrice>(compoundIndex, new CreateIndexOptions { Unique = true }));
    }

    public async Task<StockPrice?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<StockPrice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<StockPrice?> GetBySymbolAndDateAsync(string symbol, DateTime date, CancellationToken cancellationToken = default)
    {
        var filter = Builders<StockPrice>.Filter.And(
            Builders<StockPrice>.Filter.Eq(s => s.Symbol, symbol.ToUpper()),
            Builders<StockPrice>.Filter.Eq(s => s.Date, date.Date)
        );
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<StockPrice>> GetBySymbolAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var filter = Builders<StockPrice>.Filter.And(
            Builders<StockPrice>.Filter.Eq(s => s.Symbol, symbol.ToUpper()),
            Builders<StockPrice>.Filter.Gte(s => s.Date, from.Date),
            Builders<StockPrice>.Filter.Lte(s => s.Date, to.Date)
        );
        return await _collection.Find(filter).SortBy(s => s.Date).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<StockPrice>> GetLatestPricesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.Select(s => s.ToUpper()).ToList();
        var results = new List<StockPrice>();

        foreach (var symbol in symbolList)
        {
            var filter = Builders<StockPrice>.Filter.Eq(s => s.Symbol, symbol);
            var latest = await _collection.Find(filter)
                .SortByDescending(s => s.Date)
                .Limit(1)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest != null)
                results.Add(latest);
        }

        return results;
    }

    public async Task AddAsync(StockPrice entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(StockPrice entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(s => s.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(s => s.Id == id, cancellationToken);
    }

    public async Task UpsertAsync(StockPrice stockPrice, CancellationToken cancellationToken = default)
    {
        var filter = Builders<StockPrice>.Filter.And(
            Builders<StockPrice>.Filter.Eq(s => s.Symbol, stockPrice.Symbol),
            Builders<StockPrice>.Filter.Eq(s => s.Date, stockPrice.Date)
        );

        var existing = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            existing.UpdatePrice(stockPrice.Open, stockPrice.High, stockPrice.Low, stockPrice.Close, stockPrice.Volume);
            await _collection.ReplaceOneAsync(filter, existing, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.InsertOneAsync(stockPrice, null, cancellationToken);
        }
    }
}
