using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Signals;

namespace InvestmentApp.Infrastructure.Tests.Services.Signals;

public class FormingBarTests
{
    private static readonly DateTime Day = new(2026, 11, 30);

    // giờ VN = UTC + 7
    [Theory]
    [InlineData(2026, 11, 30, 2, 0, 0, true)]      // 09:00 VN
    [InlineData(2026, 11, 30, 7, 59, 59, true)]    // 14:59:59 VN
    [InlineData(2026, 11, 30, 8, 0, 0, false)]     // 15:00:00 VN đúng mốc: đã đóng
    [InlineData(2026, 11, 30, 10, 0, 0, false)]    // 17:00 VN
    [InlineData(2026, 11, 29, 17, 30, 0, true)]    // 00:30 VN ngày 30: vẫn là "hôm nay" theo giờ VN, chưa tới 15:00
    public void a_bar_dated_today_in_VN_is_forming_only_before_1500(int y, int mo, int d, int h, int mi, int s, bool expected)
    {
        FormingBar.IsForming(Day, new DateTimeOffset(y, mo, d, h, mi, s, TimeSpan.Zero)).Should().Be(expected);
    }

    [Fact]
    public void a_bar_of_another_day_is_never_forming()
    {
        var now = new DateTimeOffset(2026, 11, 30, 5, 0, 0, TimeSpan.Zero);

        FormingBar.IsForming(Day.AddDays(-1), now).Should().BeFalse();
        FormingBar.IsForming(Day.AddDays(1), now).Should().BeFalse();
    }

    [Fact]
    public void drops_only_the_last_bar_and_only_when_forming()
    {
        var bars = new[] { Day.AddDays(-2), Day.AddDays(-1), Day };
        var midday = new DateTimeOffset(2026, 11, 30, 5, 0, 0, TimeSpan.Zero);
        var evening = new DateTimeOffset(2026, 11, 30, 10, 0, 0, TimeSpan.Zero);

        FormingBar.DropIfForming(bars, b => b, midday).Should().Equal(Day.AddDays(-2), Day.AddDays(-1));
        FormingBar.DropIfForming(bars, b => b, evening).Should().Equal(bars);
        FormingBar.DropIfForming(Array.Empty<DateTime>(), b => b, midday).Should().BeEmpty();
    }
}
