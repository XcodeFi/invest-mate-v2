using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.JournalEntries.Commands;

/// <summary>
/// Loại nhật ký mà người dùng và agent được phép tự đặt.
///
/// <see cref="JournalEntryType.Decision"/> bị loại: nó là <b>cờ dập cảnh báo</b> —
/// <c>GetDecisionQueueQuery</c> gom mọi mục loại này trong ngày VN rồi lọc thẻ tương ứng ra khỏi
/// Hàng đợi quyết định. Nó chỉ được sinh từ <c>ResolveDecisionCommand</c>, nơi có luật "GIỮ phải
/// ghi lý do ≥ 20 ký tự". Mở loại này cho đường tạo nhật ký thường là mở một cửa tắt cảnh báo
/// không cần lý do.
///
/// Vị từ đặt ở một chỗ dùng cho cả create lẫn update: nhân bản điều kiện ở hai nơi là mở sẵn chỗ
/// cho lệch.
/// </summary>
public static class JournalEntryTypeRule
{
    public static readonly string[] Allowed =
    {
        nameof(JournalEntryType.Observation),
        nameof(JournalEntryType.PreTrade),
        nameof(JournalEntryType.DuringTrade),
        nameof(JournalEntryType.PostTrade),
        nameof(JournalEntryType.Review)
    };

    public static readonly string Message =
        $"EntryType phải là một trong: {string.Join(", ", Allowed)}. "
        + "Loại Decision chỉ sinh từ hành động xử lý trong Hàng đợi quyết định.";

    /// <summary>
    /// So khớp theo <b>tên</b> trong allowlist, không phải "parse được thì cho qua".
    /// <c>Enum.Parse</c> chấp cả chuỗi số (<c>"5"</c> ra <see cref="JournalEntryType.Decision"/>)
    /// và bỏ khoảng trắng hai đầu, nên lọc kiểu "khác Decision là được" vẫn bị lách.
    /// </summary>
    public static bool IsAllowed(string? value)
        => value != null
           && Allowed.Any(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));
}
