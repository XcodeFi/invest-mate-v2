using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class BacktestRepository : IBacktestRepository
{
    private readonly IMongoCollection<Backtest> _collection;

    public BacktestRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Backtest>("backtests");

        var userIdx = Builders<Backtest>.IndexKeys.Combine(
            Builders<Backtest>.IndexKeys.Ascending(b => b.UserId),
            Builders<Backtest>.IndexKeys.Descending(b => b.CreatedAt));
        _collection.Indexes.CreateOne(new CreateIndexModel<Backtest>(userIdx));

        var statusIdx = Builders<Backtest>.IndexKeys.Ascending(b => b.Status);
        _collection.Indexes.CreateOne(new CreateIndexModel<Backtest>(statusIdx));
    }

    public async Task<Backtest?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.Find(b => b.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<Backtest>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<IEnumerable<Backtest>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => await _collection.Find(b => b.UserId == userId)
            .SortByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IEnumerable<Backtest>> GetPendingAsync(CancellationToken cancellationToken = default)
        => await _collection.Find(b => b.Status == BacktestStatus.Pending).ToListAsync(cancellationToken);

    public async Task AddAsync(Backtest entity, CancellationToken cancellationToken = default)
        => await _collection.InsertOneAsync(entity, null, cancellationToken);

    public async Task UpdateAsync(Backtest entity, CancellationToken cancellationToken = default)
        => await _collection.ReplaceOneAsync(b => b.Id == entity.Id, entity, cancellationToken: cancellationToken);

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.DeleteOneAsync(b => b.Id == id, cancellationToken);
}
