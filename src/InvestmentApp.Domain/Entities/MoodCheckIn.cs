using System.Text.RegularExpressions;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public enum MoodState
{
    Calm,
    Fomo,
    Fear,
    Revenge
}

/// <summary>
/// Người dùng tự chấm trạng thái của mình một lần mỗi ngày. Chấm khác <see cref="MoodState.Calm"/>
/// thì trang chủ phủ mờ Hàng đợi quyết định; đi tiếp được nhưng phải bấm qua lớp phủ, và cú bấm đó
/// đọng lại ở <see cref="OverrodeAt"/> — đó là thước đo duy nhất cho biết luật dừng có tác dụng thật không.
/// </summary>
public class MoodCheckIn : AggregateRoot
{
    private static readonly Regex DateKeyFormat = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    public string UserId { get; private set; } = null!;

    /// <summary>Ngày lịch VN dạng <c>"YYYY-MM-DD"</c>. Chuỗi chứ không phải DateTime — xem ADR-0013.</summary>
    public string DateKey { get; private set; } = null!;

    public MoodState Mood { get; private set; }
    public DateTime CheckedAt { get; private set; }
    public DateTime? OverrodeAt { get; private set; }

    [BsonConstructor]
    public MoodCheckIn() { }

    public static MoodCheckIn Create(string userId, string dateKey, MoodState mood, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(dateKey) || !DateKeyFormat.IsMatch(dateKey))
            throw new ArgumentException("DateKey must be in YYYY-MM-DD format.", nameof(dateKey));

        return new MoodCheckIn
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            DateKey = dateKey,
            Mood = mood,
            CheckedAt = now,
        };
    }

    /// <summary>
    /// Đổi sang trạng thái khác thì xoá dấu vết đã bỏ qua lớp phủ. Giữ lại thì chấm Bình tĩnh
    /// rồi chấm lại FOMO là mở khoá vĩnh viễn mà chưa lần nào phải bấm qua lớp phủ.
    /// Chấm lại đúng trạng thái cũ không phải là "đổi" nên không đụng tới dấu đó.
    /// </summary>
    public void SetMood(MoodState mood, DateTime now)
    {
        var changed = Mood != mood;
        Mood = mood;
        CheckedAt = now;
        if (changed) OverrodeAt = null;
        IncrementVersion();
    }

    public void MarkOverridden(DateTime now)
    {
        if (OverrodeAt is not null) return;
        OverrodeAt = now;
        IncrementVersion();
    }
}
