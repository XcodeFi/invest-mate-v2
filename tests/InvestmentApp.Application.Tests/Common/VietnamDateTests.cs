using FluentAssertions;
using InvestmentApp.Application.Common;

namespace InvestmentApp.Application.Tests.Common;

/// <summary>
/// Ranh giới ngày VN. Lưu mốc ngày dạng DateTime rồi so sánh theo UTC là chỗ dự án đã trượt
/// một ngày mà unit test vẫn xanh — nên hàm quy đổi phải được ghim riêng ở đây.
/// </summary>
public class VietnamDateTests
{
    [Fact]
    public void ToDateKey_JustAfterVietnamMidnight_ReturnsTheNewVietnamDay()
    {
        // 17:30 UTC = 00:30 giờ VN ngày hôm sau
        var utc = new DateTime(2026, 8, 11, 17, 30, 0, DateTimeKind.Utc);

        VietnamDate.ToDateKey(utc).Should().Be("2026-08-12");
    }

    [Fact]
    public void ToDateKey_JustBeforeVietnamMidnight_StillReturnsTheSameVietnamDay()
    {
        // 16:59 UTC = 23:59 giờ VN cùng ngày
        var utc = new DateTime(2026, 8, 11, 16, 59, 59, DateTimeKind.Utc);

        VietnamDate.ToDateKey(utc).Should().Be("2026-08-11");
    }

    [Fact]
    public void ToDateKey_MidnightUtc_ReturnsSameVietnamDay()
    {
        // 00:00 UTC = 07:00 giờ VN cùng ngày
        var utc = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

        VietnamDate.ToDateKey(utc).Should().Be("2026-08-11");
    }

    [Fact]
    public void ToDateKey_CrossesMonthBoundary()
    {
        var utc = new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc);

        VietnamDate.ToDateKey(utc).Should().Be("2026-08-01");
    }

    [Fact]
    public void DaysBetween_CountsVietnamCalendarDays_NotElapsedHours()
    {
        // 12 ngày trước theo lịch VN, dù giờ trong ngày lệch nhau
        var earlier = new DateTime(2026, 7, 30, 2, 0, 0, DateTimeKind.Utc);   // 09:00 VN 30/07
        var later = new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc);    // 23:00 VN 11/08

        VietnamDate.DaysBetween(earlier, later).Should().Be(12);
    }

    [Fact]
    public void DaysBetween_SameVietnamDay_IsZero()
    {
        var earlier = new DateTime(2026, 8, 11, 1, 0, 0, DateTimeKind.Utc);   // 08:00 VN
        var later = new DateTime(2026, 8, 11, 16, 0, 0, DateTimeKind.Utc);    // 23:00 VN

        VietnamDate.DaysBetween(earlier, later).Should().Be(0);
    }

    [Fact]
    public void DaysBetween_FutureDate_ClampsToZero_NeverNegative()
    {
        // Lệnh ghi ngày tương lai không được biến thành "đã chờ -3 ngày"
        var earlier = new DateTime(2026, 8, 14, 3, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 8, 11, 3, 0, 0, DateTimeKind.Utc);

        VietnamDate.DaysBetween(earlier, later).Should().Be(0);
    }
}
