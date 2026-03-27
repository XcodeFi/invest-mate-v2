using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class JournalEntryRepository : IJournalEntryRepository
{
    private readonly IMongoCollection<JournalEntry> _collection;

    public JournalEntryRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<JournalEntry>("journal_entries");

        var compoundIndex = Builders<JournalEntry>.IndexKeys.Combine(
            Builders<JournalEntry>.IndexKeys.Ascending(e => e.UserId),
            Builders<JournalEntry>.IndexKeys.Ascending(e => e.Symbol),
            Builders<JournalEntry>.IndexKeys.Ascending(e => e.Timestamp));
        _collection.Indexes.CreateOne(new CreateIndexModel<JournalEntry>(compoundIndex));

        var tradeIndex = Builders<JournalEntry>.IndexKeys.Ascending(e => e.TradeId);
        _collection.Indexes.CreateOne(new CreateIndexModel<JournalEntry>(tradeIndex,
            new CreateIndexOptions { Sparse = true }));
    }

    public async Task<JournalEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(e => e.Id == id && !e.IsDeleted).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<JournalEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(e => !e.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<JournalEntry>> GetByUserIdAndSymbolAsync(
        string userId, string symbol, DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<JournalEntry>.Filter.And(
            Builders<JournalEntry>.Filter.Eq(e => e.UserId, userId),
            Builders<JournalEntry>.Filter.Eq(e => e.Symbol, symbol.ToUpper().Trim()),
            Builders<JournalEntry>.Filter.Eq(e => e.IsDeleted, false));

        if (from.HasValue)
            filter &= Builders<JournalEntry>.Filter.Gte(e => e.Timestamp, from.Value);
        if (to.HasValue)
            filter &= Builders<JournalEntry>.Filter.Lte(e => e.Timestamp, to.Value);

        return await _collection.Find(filter)
            .SortBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<JournalEntry>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(e => e.UserId == userId && !e.IsDeleted)
            .SortByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<JournalEntry>> GetByTradeIdAsync(string tradeId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(e => e.TradeId == tradeId && !e.IsDeleted)
            .SortBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(JournalEntry entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(JournalEntry entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var update = Builders<JournalEntry>.Update
            .Set(e => e.IsDeleted, true)
            .Set(e => e.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(e => e.Id == id, update, cancellationToken: cancellationToken);
    }
}
