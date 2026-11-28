using InvestmentApp.Application.Interfaces;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

/// <summary>Bộ đếm nhỏ theo khoá trong collection <c>job_state</c>: mỗi ngày Việt Nam một bản ghi đếm số lần gọi nguồn của việc lấy trước chuỗi giá.</summary>
public class JobStateDocument
{
    [BsonId]
    public string Id { get; set; } = null!;

    public int Calls { get; set; }
}

public class WarmupCallBudgetStore : IWarmupCallBudget
{
    public const string CollectionName = "job_state";
    private const int DuplicateKey = 11000;

    private readonly IMongoCollection<JobStateDocument> _collection;

    public WarmupCallBudgetStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<JobStateDocument>(CollectionName);
    }

    public static string KeyFor(DateTime vnDay) => $"signal-warmup:{vnDay:yyyy-MM-dd}";

    /// <summary>
    /// Một lệnh nguyên tử: tăng bộ đếm của ngày nếu còn dưới trần. Ngày chưa có bản ghi thì tạo (upsert). Bản ghi đã chạm trần không khớp bộ lọc,
    /// nên upsert thử tạo trùng <c>_id</c> và Mongo báo lỗi khoá trùng: đó chính là "hết ngân sách", không phải lỗi.
    /// </summary>
    public async Task<bool> TryConsumeAsync(DateTime vnDay, int dailyCap, CancellationToken cancellationToken = default)
    {
        if (dailyCap <= 0) return false;

        var id = KeyFor(vnDay);
        var filter = Builders<JobStateDocument>.Filter.And(
            Builders<JobStateDocument>.Filter.Eq(d => d.Id, id),
            Builders<JobStateDocument>.Filter.Lt(d => d.Calls, dailyCap));
        var update = Builders<JobStateDocument>.Update.Inc(d => d.Calls, 1);

        try
        {
            var doc = await _collection.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<JobStateDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, cancellationToken);
            return doc != null;
        }
        catch (MongoCommandException ex) when (ex.Code == DuplicateKey)
        {
            return false;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
