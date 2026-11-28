namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>
/// Trong giờ giao dịch nguồn giá trả cả nến của hôm nay khi nó còn đang hình thành. Nến đó chưa phải một phiên đã đóng:
/// đưa vào bộ quét thì tạo tín hiệu ma với "vào lệnh ước tính = Close(t)". Quy tắc: bỏ nến cuối nếu ngày của nó là hôm nay (giờ VN)
/// và bây giờ chưa tới 15:00 giờ VN. Một chỗ dùng chung cho thống kê lịch sử, tín hiệu đang kích hoạt và công cụ đếm.
/// </summary>
public static class FormingBar
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly TimeSpan Close = TimeSpan.FromHours(15);

    /// <summary>Nến có ngày <paramref name="barDate"/> còn đang hình thành tại thời điểm <paramref name="nowUtc"/>?</summary>
    public static bool IsForming(DateTime barDate, DateTimeOffset nowUtc)
    {
        var vn = nowUtc + VietnamOffset;
        return barDate.Date == vn.Date && vn.TimeOfDay < Close;
    }

    /// <summary>Bỏ nến cuối của chuỗi đã sắp theo ngày tăng dần nếu nó còn đang hình thành; không thì trả nguyên chuỗi.</summary>
    public static List<T> DropIfForming<T>(IReadOnlyList<T> sortedAscending, Func<T, DateTime> date, DateTimeOffset nowUtc)
    {
        var list = sortedAscending.ToList();
        if (list.Count > 0 && IsForming(date(list[^1]), nowUtc)) list.RemoveAt(list.Count - 1);
        return list;
    }
}
