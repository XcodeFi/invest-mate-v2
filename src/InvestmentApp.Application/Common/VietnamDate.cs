namespace InvestmentApp.Application.Common;

/// <summary>
/// Quy đổi mốc thời gian UTC sang ngày lịch Việt Nam.
///
/// Cộng cứng +07:00 thay vì dùng <see cref="TimeZoneInfo"/>: Việt Nam không có giờ mùa hè,
/// và tên múi giờ khác nhau giữa Windows ("SE Asia Standard Time") với Linux ("Asia/Ho_Chi_Minh")
/// nên tra theo tên là một đường gãy khi container đổi nền. Cùng cách làm với ADR-0011 D7.
/// </summary>
public static class VietnamDate
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    /// <summary>Ngày lịch VN của một mốc UTC, dạng <c>"YYYY-MM-DD"</c>.</summary>
    public static string ToDateKey(DateTime utc) => ToLocal(utc).ToString("yyyy-MM-dd");

    /// <summary>
    /// Ngày lịch VN của một mốc bất kỳ, phần giờ bằng 0. Dùng cho mốc đọc từ DB:
    /// nửa đêm giờ VN được Mongo lưu thành 17:00Z hôm trước, đọc <c>.Date</c> trần là lùi một ngày.
    /// </summary>
    public static DateTime DayOf(DateTime utc) => ToLocal(utc).Date;

    /// <summary>Ngày lịch VN của hiện tại.</summary>
    public static DateTime Today(DateTime utcNow) => DayOf(utcNow);

    /// <summary>
    /// Số ngày lịch VN giữa hai mốc. Đếm theo ngày lịch chứ không theo số giờ trôi qua:
    /// 23:00 hôm nay và 01:00 sáng mai cách nhau 1 ngày, không phải 0.
    /// Mốc trước nằm ở tương lai thì trả 0 — không có "đã chờ −3 ngày".
    /// </summary>
    public static int DaysBetween(DateTime earlierUtc, DateTime laterUtc)
    {
        var days = (ToLocal(laterUtc).Date - ToLocal(earlierUtc).Date).Days;
        return Math.Max(0, days);
    }

    private static DateTime ToLocal(DateTime utc) => utc.Add(Offset);
}
