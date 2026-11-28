using FluentAssertions;
using InvestmentApp.Application.MarketData.Trend;

namespace InvestmentApp.Application.Tests.MarketData.Trend;

/// <summary>Thẻ Trend Template (Minervini): từng tiêu chí có giá trị và khoảng cách; KHÔNG có kết luận tổng "đạt". Thiếu dữ liệu thì null kèm lý do, không bao giờ 0.</summary>
public class TrendTemplateBuilderTests
{
    private static (List<decimal> Close, List<decimal> High, List<decimal> Low) Linear(int n, decimal start, decimal step)
    {
        var close = Enumerable.Range(0, n).Select(i => start + step * i).ToList();
        return (close, close.Select(c => c + 1).ToList(), close.Select(c => c - 1).ToList());
    }

    private static TrendTemplateCriterion Row(TrendTemplateInfo t, string key) => t.Criteria.Single(c => c.Key == key);

    [Fact]
    public void An_uptrend_with_a_full_year_meets_every_computable_criterion_with_numbers()
    {
        var (c, h, l) = Linear(260, 100m, 1m);

        var t = TrendTemplateBuilder.Build(c, h, l);

        t.Sessions.Should().Be(260);
        foreach (var key in new[] { "price_above_sma150", "price_above_sma200", "sma150_above_sma200", "sma200_rising", "sma50_above_sma150", "sma50_above_sma200", "price_above_sma50", "above_52w_low", "near_52w_high" })
        {
            Row(t, key).Met.Should().BeTrue(key);
            Row(t, key).Value.Should().NotBeNull(key);
        }
    }

    [Fact]
    public void Each_criterion_reports_its_distance_in_percent()
    {
        var (c, h, l) = Linear(260, 100m, 1m);   // đóng cuối 359; SMA50 = 334,5; đỉnh 52 tuần 360; đáy 52 tuần = đáy phiên 10 = 109

        var t = TrendTemplateBuilder.Build(c, h, l);

        Row(t, "price_above_sma50").Value.Should().Be(7.32m);       // 359 / 334,5 - 1
        Row(t, "near_52w_high").Value.Should().Be(0.28m);           // (360 - 359) / 360
        Row(t, "above_52w_low").Value.Should().Be(229.36m);         // 359 / 109 - 1
        Row(t, "price_above_sma50").Unit.Should().Be("%");
        Row(t, "above_52w_low").Threshold.Should().Be(30m);
        Row(t, "near_52w_high").Threshold.Should().Be(25m);
    }

    [Fact]
    public void A_downtrend_fails_the_price_and_average_criteria()
    {
        var (c, h, l) = Linear(260, 400m, -1m);

        var t = TrendTemplateBuilder.Build(c, h, l);

        Row(t, "price_above_sma150").Met.Should().BeFalse();
        Row(t, "price_above_sma200").Met.Should().BeFalse();
        Row(t, "sma200_rising").Met.Should().BeFalse();
        Row(t, "sma200_rising").Value.Should().BeLessThan(0m);
    }

    [Fact]
    public void The_rising_200_needs_about_221_sessions_and_says_so_when_it_is_short()
    {
        var (c, h, l) = Linear(220, 100m, 1m);   // SMA200 có, nhưng chưa có SMA200 của 21 phiên trước

        var t = TrendTemplateBuilder.Build(c, h, l);

        Row(t, "price_above_sma200").Met.Should().BeTrue();
        var rising = Row(t, "sma200_rising");
        rising.Value.Should().BeNull();
        rising.Met.Should().BeNull();
        rising.Detail.Should().Contain("Không đủ dữ liệu");
    }

    [Fact]
    public void Under_250_sessions_the_52_week_criteria_are_null_not_zero_and_not_passed()
    {
        var (c, h, l) = Linear(240, 100m, 1m);

        var t = TrendTemplateBuilder.Build(c, h, l);

        foreach (var key in new[] { "above_52w_low", "near_52w_high" })
        {
            Row(t, key).Value.Should().BeNull(key);
            Row(t, key).Met.Should().BeNull(key);
            Row(t, key).Detail.Should().Contain("Không đủ dữ liệu");
        }
    }

    [Fact]
    public void With_only_150_sessions_nothing_that_needs_the_200_is_passed()
    {
        var (c, h, l) = Linear(150, 100m, 1m);

        var t = TrendTemplateBuilder.Build(c, h, l);

        foreach (var key in new[] { "price_above_sma200", "sma150_above_sma200", "sma200_rising", "sma50_above_sma200" })
        {
            Row(t, key).Value.Should().BeNull(key);
            Row(t, key).Met.Should().BeNull(key);
        }
        Row(t, "price_above_sma150").Met.Should().BeTrue();
    }

    [Fact]
    public void The_relative_strength_criterion_is_listed_but_never_computed_because_the_app_has_no_IBD_style_rating()
    {
        var (c, h, l) = Linear(260, 100m, 1m);

        var rs = Row(TrendTemplateBuilder.Build(c, h, l), "rs_rating");

        rs.Value.Should().BeNull();
        rs.Met.Should().BeNull();
        rs.Detail.Should().Contain("IBD");
    }

    [Fact]
    public void There_is_no_overall_pass_field_only_the_rows()
    {
        typeof(TrendTemplateInfo).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(new[] { "Sessions", "Criteria", "Note" });
        typeof(TrendTemplateCriterion).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(new[] { "Key", "Rule", "Value", "Unit", "Threshold", "Met", "Detail" });
    }

    [Fact]
    public void The_note_says_this_is_a_filter_not_a_buy_signal()
    {
        var (c, h, l) = Linear(260, 100m, 1m);

        TrendTemplateBuilder.Build(c, h, l).Note.Should().Contain("không phải tín hiệu mua");
    }

    [Fact]
    public void Empty_input_gives_rows_without_numbers()
    {
        var t = TrendTemplateBuilder.Build(new List<decimal>(), new List<decimal>(), new List<decimal>());

        t.Sessions.Should().Be(0);
        t.Criteria.Should().OnlyContain(r => r.Value == null && r.Met == null);
    }

    private static (List<decimal> C, List<decimal> H, List<decimal> L) Flat(int n, decimal close, decimal high, decimal low)
        => (Enumerable.Repeat(close, n).ToList(), Enumerable.Repeat(high, n).ToList(), Enumerable.Repeat(low, n).ToList());

    [Fact]
    public void A_comparison_exactly_at_zero_is_not_above()
    {
        var (c, h, l) = Flat(260, 100m, 101m, 99m);   // giá bằng mọi đường trung bình

        var t = TrendTemplateBuilder.Build(c, h, l);

        Row(t, "price_above_sma150").Value.Should().Be(0m);
        Row(t, "price_above_sma150").Met.Should().BeFalse();
        Row(t, "sma50_above_sma200").Met.Should().BeFalse();
        Row(t, "sma200_rising").Met.Should().BeFalse();
    }

    [Fact]
    public void Exactly_30_percent_above_the_52_week_low_meets_the_threshold_and_just_under_does_not()
    {
        var (c, h, l) = Flat(250, 130m, 131m, 129m);
        l[100] = 100m;   // đáy 52 tuần = 100, giá 130: đúng +30%

        var exact = Row(TrendTemplateBuilder.Build(c, h, l), "above_52w_low");

        exact.Value.Should().Be(30m);
        exact.Met.Should().BeTrue();

        var (c2, h2, l2) = Flat(250, 129.99m, 131m, 129m);
        l2[100] = 100m;

        var below = Row(TrendTemplateBuilder.Build(c2, h2, l2), "above_52w_low");

        below.Value.Should().Be(29.99m);
        below.Met.Should().BeFalse();
    }

    [Fact]
    public void Exactly_25_percent_below_the_52_week_high_meets_the_threshold_and_just_over_does_not()
    {
        var (c, h, l) = Flat(250, 120m, 121m, 119m);
        h[100] = 160m;   // đỉnh 52 tuần = 160, giá 120: đúng cách đỉnh 25%

        var exact = Row(TrendTemplateBuilder.Build(c, h, l), "near_52w_high");

        exact.Value.Should().Be(25m);
        exact.Met.Should().BeTrue();

        var (c2, h2, l2) = Flat(250, 119.99m, 121m, 119m);
        h2[100] = 160m;

        var over = Row(TrendTemplateBuilder.Build(c2, h2, l2), "near_52w_high");

        over.Value.Should().Be(25.01m);
        over.Met.Should().BeFalse();
    }

    [Fact]
    public void The_verdict_is_decided_on_the_unrounded_number_and_rounding_is_only_for_display()
    {
        // 29,996% hiển thị là 30 nhưng chưa đạt ngưỡng 30
        var (c, h, l) = Flat(250, 129.996m, 131m, 129m);
        l[100] = 100m;

        var almost = Row(TrendTemplateBuilder.Build(c, h, l), "above_52w_low");

        almost.Value.Should().Be(30m);
        almost.Met.Should().BeFalse();

        // 0,000009% trên SMA150 hiển thị là 0 nhưng giá vẫn nằm trên
        var closes = Enumerable.Repeat(100m, 259).Append(100.001m).ToList();
        var highs = closes.Select(x => x + 1).ToList();
        var lows = closes.Select(x => x - 1).ToList();

        var above = Row(TrendTemplateBuilder.Build(closes, highs, lows), "price_above_sma150");

        above.Value.Should().Be(0m);
        above.Met.Should().BeTrue();
    }

    [Fact]
    public void An_unadjusted_series_gets_rows_without_numbers_and_with_the_reason()
    {
        var t = TrendTemplateBuilder.Unadjusted(123);

        t.Sessions.Should().Be(123);
        t.Criteria.Where(r => r.Key != "rs_rating").Should().OnlyContain(r => r.Value == null && r.Met == null && r.Detail!.Contains("chưa điều chỉnh"));
        Row(t, "rs_rating").Detail.Should().Contain("IBD");
        t.Note.Should().Contain("không phải tín hiệu mua");
    }
}
