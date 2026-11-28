using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

/// <summary>
/// "Hồ sơ lúc đó" của một giao dịch. Chỉ có ReviewedAt và ConfirmedAt HIỆN TẠI (Confirm() ghi đè ReviewedAt, lịch sử các lần ký không được lưu),
/// nên lệnh trước lần ký gần nhất không có phán quyết: chỉ biết là hồ sơ đã được ký lại sau lệnh.
/// </summary>
public class CompanyDossierAtTradeTests
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    private static CompanyDossier Signed()
    {
        var d = new CompanyDossier("user-1", "HPG", "Bán thép xây dựng cho nhà thầu",
            new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành" } },
            new List<RiskFactor> { new() { Rank = 1, Description = "Giá HRC", ObservableSignal = "HRC giảm quá 10%" } });
        d.Confirm();
        return d;
    }

    /// <summary>Thời điểm UTC ứng với giờ <paramref name="h"/>:<paramref name="m"/> VN của ngày VN cách ngày ký <paramref name="daysAfterSigned"/> ngày.</summary>
    private static DateTime VnTime(CompanyDossier d, int daysAfterSigned, int h = 12, int m = 0)
        => d.ReviewedAt.Add(Vn).Date.AddDays(daysAfterSigned).AddHours(h).AddMinutes(m) - Vn;

    [Fact]
    public void Unconfirmed_dossier_is_unsigned_whatever_the_trade_date()
    {
        var d = new CompanyDossier("user-1", "HPG", "abc", new(), new());

        var badge = d.DescribeAtTrade(DateTime.UtcNow.AddDays(-30));

        badge.State.Should().Be("unsigned");
        badge.DaysSigned.Should().BeNull();
        badge.Freshness.Should().BeNull();
    }

    [Fact]
    public void A_dossier_unsigned_by_agent_edit_is_unsigned_again()
    {
        var d = Signed();
        d.UpdateByAgent("mô hình mới", new(), new(), null);

        d.DescribeAtTrade(VnTime(d, 10)).State.Should().Be("unsigned");
    }

    [Fact]
    public void Trade_before_the_current_signature_has_no_verdict_only_the_resign_date()
    {
        var d = Signed();

        var badge = d.DescribeAtTrade(VnTime(d, -3));

        badge.State.Should().Be("resigned-after");
        badge.ResignedAt.Should().Be(d.ReviewedAt);
        badge.DaysSigned.Should().BeNull("lần ký trước đó không được lưu nên không có số ngày");
        badge.Freshness.Should().BeNull();
    }

    [Fact]
    public void Trade_on_the_same_vn_day_as_the_signature_counts_as_signed_zero_days_before()
    {
        var d = Signed();

        var badge = d.DescribeAtTrade(VnTime(d, 0, 0, 1));   // 00:01 giờ VN cùng ngày VN với lần ký

        badge.State.Should().Be("signed");
        badge.DaysSigned.Should().Be(0);
        badge.Freshness.Should().Be("fresh");
    }

    [Theory]
    [InlineData(1, "fresh")]
    [InlineData(89, "fresh")]
    [InlineData(90, "needs-review")]
    [InlineData(179, "needs-review")]
    [InlineData(180, "expired")]
    [InlineData(400, "expired")]
    public void Signed_badge_follows_the_90_and_180_day_boundaries_exactly(int days, string freshness)
    {
        var d = Signed();

        var badge = d.DescribeAtTrade(VnTime(d, days));

        badge.State.Should().Be("signed");
        badge.DaysSigned.Should().Be(days);
        badge.Freshness.Should().Be(freshness);
    }

    [Fact]
    public void Freshness_agrees_with_the_entity_own_GetFreshness_at_the_trade_date()
    {
        // một bản duy nhất của ngưỡng: kết luận cho lệnh phải trùng kết luận GetFreshness tại ngày lệnh
        var d = Signed();
        foreach (var days in new[] { 0, 89, 90, 179, 180, 300 })
        {
            var t = VnTime(d, days);
            var expected = d.GetFreshness(t) switch
            {
                DossierFreshness.Fresh => "fresh", DossierFreshness.NeedsReview => "needs-review", _ => "expired"
            };
            d.DescribeAtTrade(t).Freshness.Should().Be(expected, $"day {days}");
        }
    }

    [Fact]
    public void Vn_date_boundary_23_30_and_00_30_land_on_different_days()
    {
        var d = Signed();

        // 23:30 VN ngày +89 → 89 ngày (fresh); 00:30 VN ngày +90 → 90 ngày (needs-review). Cách nhau một giờ nhưng khác ngày lịch VN.
        var late = d.DescribeAtTrade(VnTime(d, 89, 23, 30));
        var early = d.DescribeAtTrade(VnTime(d, 90, 0, 30));

        late.DaysSigned.Should().Be(89);
        late.Freshness.Should().Be("fresh");
        early.DaysSigned.Should().Be(90);
        early.Freshness.Should().Be("needs-review");
    }

    [Fact]
    public void Trade_at_23_30_vn_the_day_before_the_signature_is_still_before_it()
    {
        var d = Signed();

        d.DescribeAtTrade(VnTime(d, -1, 23, 30)).State.Should().Be("resigned-after");
        d.DescribeAtTrade(VnTime(d, 0, 0, 30)).State.Should().Be("signed");
    }
}
