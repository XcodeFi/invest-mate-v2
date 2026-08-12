using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Một ngày Sở Giao dịch Chứng khoán đóng cửa vì nghỉ lễ. Bất biến — sửa = xoá và tạo lại.
/// T7/CN không lưu ở đây, suy ra từ <see cref="DayOfWeek"/>.
/// </summary>
public class MarketClosure : AggregateRoot
{
    public string UserId { get; private set; } = null!;

    // Ngày thuần. Thiếu attribute này thì Mongo ghi nửa đêm giờ local thành 17:00Z hôm trước,
    // đọc lên không còn là nửa đêm và phép đếm phiên lệch 1 ngày.
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Date { get; private set; }

    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }

    [BsonConstructor]
    public MarketClosure() { } // MongoDB

    public MarketClosure(string userId, DateTime date, string? note = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        if (Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            throw new ArgumentException("Cuối tuần đã là ngày nghỉ, không cần lưu", nameof(date));
        Note = note;
        CreatedAt = DateTime.UtcNow;
    }
}
