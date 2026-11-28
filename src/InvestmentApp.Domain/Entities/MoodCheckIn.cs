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

/// <summary>Một lần đổi tâm trạng trong ngày: lúc nào, sang gì, từ đâu (<c>web</c> hoặc <c>mcp</c>).</summary>
public sealed record MoodChange(DateTime At, MoodState Mood, string Source)
{
    public const string SourceWeb = "web";
    public const string SourceMcp = "mcp";
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

    /// <summary>Tối đa số lần đổi được giữ lại trong ngày; quá thì bỏ mục cũ nhất. <see cref="Mood"/> mới là sự thật hiện tại.</summary>
    public const int MaxChanges = 20;

    /// <summary>
    /// Lịch sử đổi trong ngày, cũ đến mới. Trường tuỳ chọn: tài liệu tạo trước khi có trường này không có mục nào (không cần migration).
    /// </summary>
    public List<MoodChange> Changes { get; private set; } = new();

    /// <summary>Nguồn của lần đổi gần nhất; null với tài liệu cũ chưa có lịch sử.</summary>
    [BsonIgnore]
    public string? LatestSource => Changes.Count > 0 ? Changes[^1].Source : null;

    [BsonConstructor]
    public MoodCheckIn() { }

    public static MoodCheckIn Create(string userId, string dateKey, MoodState mood, DateTime now, string source = MoodChange.SourceWeb)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(dateKey) || !DateKeyFormat.IsMatch(dateKey))
            throw new ArgumentException("DateKey must be in YYYY-MM-DD format.", nameof(dateKey));

        var checkIn = new MoodCheckIn
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            DateKey = dateKey,
            Mood = mood,
            CheckedAt = now,
        };
        checkIn.Changes.Add(new MoodChange(now, mood, source));
        return checkIn;
    }

    /// <summary>
    /// Đổi sang trạng thái khác thì xoá dấu vết đã bỏ qua lớp phủ. Giữ lại thì chấm Bình tĩnh
    /// rồi chấm lại FOMO là mở khoá vĩnh viễn mà chưa lần nào phải bấm qua lớp phủ.
    /// Chấm lại đúng trạng thái cũ không phải là "đổi" nên không đụng tới dấu đó.
    /// </summary>
    /// <remarks>
    /// Nguồn <c>mcp</c> KHÔNG BAO GIỜ đặt hay xoá <see cref="OverrodeAt"/>: dấu đó chỉ do chính người dùng tạo (bấm qua lớp phủ) và chỉ
    /// bị xoá bởi một lần đổi do chính họ làm. Lặp lại đúng trạng thái hiện tại không thêm mục vào <see cref="Changes"/>.
    /// </remarks>
    public void SetMood(MoodState mood, DateTime now, string source = MoodChange.SourceWeb)
    {
        var changed = Mood != mood;
        Mood = mood;
        CheckedAt = now;
        if (changed && source != MoodChange.SourceMcp) OverrodeAt = null;
        if (changed || (Changes.Count > 0 && Changes[^1].Mood != mood))
        {
            Changes.Add(new MoodChange(now, mood, source));
            if (Changes.Count > MaxChanges) Changes.RemoveRange(0, Changes.Count - MaxChanges);
        }
        IncrementVersion();
    }

    public void MarkOverridden(DateTime now)
    {
        if (OverrodeAt is not null) return;
        OverrodeAt = now;
        IncrementVersion();
    }
}
