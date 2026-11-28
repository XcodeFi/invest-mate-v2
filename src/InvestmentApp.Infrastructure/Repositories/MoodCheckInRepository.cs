using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class MoodCheckInRepository : IMoodCheckInRepository
{
    private const string UniqueIndexName = "ux_user_datekey";

    private readonly IMongoCollection<MoodCheckIn> _collection;

    public MoodCheckInRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MoodCheckIn>("mood_check_ins");

        // Một bản ghi cho mỗi (user, ngày VN) — chấm lại trong ngày là cập nhật, không đẻ bản thứ hai.
        // Đặt tên rõ để AddAsync phân biệt được đúng index này, xem chú thích ở dưới.
        _collection.Indexes.CreateOne(new CreateIndexModel<MoodCheckIn>(
            Builders<MoodCheckIn>.IndexKeys
                .Ascending(m => m.UserId)
                .Ascending(m => m.DateKey),
            new CreateIndexOptions { Unique = true, Name = UniqueIndexName }));
    }

    public async Task<MoodCheckIn?> GetByUserAndDateAsync(string userId, string dateKey, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(m => m.UserId == userId && m.DateKey == dateKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MoodCheckIn>> GetByUserAndDateRangeAsync(string userId, string fromDateKey, string toDateKey, CancellationToken cancellationToken = default)
    {
        // DateKey là chuỗi "YYYY-MM-DD" nên so sánh chuỗi cho đúng thứ tự ngày; luôn kèm UserId (chỉ dữ liệu của chính người gọi).
        var filter = Builders<MoodCheckIn>.Filter.And(
            Builders<MoodCheckIn>.Filter.Eq(m => m.UserId, userId),
            Builders<MoodCheckIn>.Filter.Gte(m => m.DateKey, fromDateKey),
            Builders<MoodCheckIn>.Filter.Lte(m => m.DateKey, toDateKey));
        return await _collection.Find(filter).SortBy(m => m.DateKey).ToListAsync(cancellationToken);
    }

    // Phân biệt bằng TÊN index, không bằng "có phải DuplicateKey không" — collection này sau
    // có thêm index unique thứ hai thì lỗi của nó không được đội lốt trùng (UserId, DateKey),
    // vì caller sẽ thử lại bằng find và tìm không ra.
    public async Task AddAsync(MoodCheckIn entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (
            ex.WriteError?.Category == ServerErrorCategory.DuplicateKey
            && ex.WriteError.Message.Contains(UniqueIndexName))
        {
            throw new DuplicateMoodCheckInException(entity.UserId, entity.DateKey, ex);
        }
    }

    public async Task UpdateAsync(MoodCheckIn entity, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(m => m.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }
}
