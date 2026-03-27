using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;

namespace InvestmentApp.Infrastructure.Repositories;

public class TradeRepository : ITradeRepository
{
    private readonly IMongoCollection<Trade> _collection;

    public TradeRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Trade>("trades");

        // Create indexes
        var portfolioIndex = Builders<Trade>.IndexKeys.Ascending(t => t.PortfolioId);
        _collection.Indexes.CreateOne(new CreateIndexModel<Trade>(portfolioIndex));

        var symbolIndex = Builders<Trade>.IndexKeys.Ascending(t => t.Symbol);
        _collection.Indexes.CreateOne(new CreateIndexModel<Trade>(symbolIndex));

        var dateIndex = Builders<Trade>.IndexKeys.Ascending(t => t.TradeDate);
        _collection.Indexes.CreateOne(new CreateIndexModel<Trade>(dateIndex));

        // Compound index for portfolio + symbol
        var compoundIndex = Builders<Trade>.IndexKeys.Combine(
            Builders<Trade>.IndexKeys.Ascending(t => t.PortfolioId),
            Builders<Trade>.IndexKeys.Ascending(t => t.Symbol)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<Trade>(compoundIndex));
    }

    public async Task<Trade?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(t => t.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Trade>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Trade>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(t => t.PortfolioId == portfolioId).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Trade>> GetByPortfolioIdAndSymbolAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Trade>.Filter.And(
            Builders<Trade>.Filter.Eq(t => t.PortfolioId, portfolioId),
            Builders<Trade>.Filter.Eq(t => t.Symbol, symbol)
        );
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Trade>> GetByPortfolioIdAndDateRangeAsync(
        string portfolioId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Trade>.Filter.And(
            Builders<Trade>.Filter.Eq(t => t.PortfolioId, portfolioId),
            Builders<Trade>.Filter.Gte(t => t.TradeDate, from),
            Builders<Trade>.Filter.Lt(t => t.TradeDate, to.AddDays(1))
        );
        return await _collection.Find(filter)
            .SortBy(t => t.TradeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Trade>> GetByUserPortfoliosAndSymbolAsync(
        IEnumerable<string> portfolioIds, string symbol, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Trade>.Filter.And(
            Builders<Trade>.Filter.In(t => t.PortfolioId, portfolioIds),
            Builders<Trade>.Filter.Eq(t => t.Symbol, symbol.ToUpper().Trim()));
        return await _collection.Find(filter)
            .SortBy(t => t.TradeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Trade entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(Trade entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Trade>.Filter.Eq(t => t.Id, entity.Id);
        await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(t => t.Id == id, cancellationToken);
    }
}