using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class MarketClosureRepository : IMarketClosureRepository
{
    private const string UniqueIndexName = "ux_user_date";

    private readonly IMongoCollection<MarketClosure> _collection;

    public MarketClosureRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MarketClosure>("market_closures");

        // Một bản ghi cho mỗi (user, ngày) — nhập lại cùng ngày là no-op, không đẻ bản thứ hai.
        _collection.Indexes.CreateOne(new CreateIndexModel<MarketClosure>(
            Builders<MarketClosure>.IndexKeys
                .Ascending(c => c.UserId)
                .Ascending(c => c.Date),
            new CreateIndexOptions { Unique = true, Name = UniqueIndexName }));
    }

    public async Task<IEnumerable<MarketClosure>> GetByUserAndRangeAsync(
        string userId, DateTime fromInclusive, DateTime toInclusive, CancellationToken cancellationToken = default)
    {
        var from = DateTime.SpecifyKind(fromInclusive.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toInclusive.Date, DateTimeKind.Utc);

        return await _collection
            .Find(c => c.UserId == userId && c.Date >= from && c.Date <= to)
            .SortBy(c => c.Date)
            .ToListAsync(cancellationToken);
    }

    // Phân biệt bằng TÊN index, không bằng "có phải DuplicateKey không": collection này sau có
    // thêm index unique thứ hai thì lỗi của nó không được đội lốt trùng (UserId, Date).
    public async Task<bool> TryAddAsync(MarketClosure entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (
            ex.WriteError?.Category == ServerErrorCategory.DuplicateKey
            && ex.WriteError.Message.Contains(UniqueIndexName))
        {
            return false;
        }
    }

    public async Task<bool> DeleteByDateAsync(string userId, DateTime date, CancellationToken cancellationToken = default)
    {
        var target = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var result = await _collection.DeleteOneAsync(
            c => c.UserId == userId && c.Date == target, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<DateTime?> GetLatestDateAsync(string userId, CancellationToken cancellationToken = default)
    {
        var latest = await _collection
            .Find(c => c.UserId == userId)
            .SortByDescending(c => c.Date)
            .FirstOrDefaultAsync(cancellationToken);
        return latest?.Date;
    }
}
