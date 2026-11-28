using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetCompanyFundamentals;

namespace InvestmentApp.Application.Tests.MarketData;

/// <summary>
/// Mọi phép so sánh kỳ nằm ở server (một nguồn sự thật cho giao diện và MCP): cùng quý năm trước, TTM so với TTM trước, cả năm khi đủ 4 quý,
/// chiều tăng/giảm với ngưỡng 1%, biên lợi nhuận ròng và vị trí trong vùng giá 52 tuần. Giá trị thiếu là null, không bao giờ là 0.
/// </summary>
public class FundamentalsTrendCalculatorTests
{
    private static IncomeStatementItem Q(int year, int quarter, decimal? revenue, decimal? profit)
        => new() { Period = $"Q{quarter}/{year}", Revenue = revenue, NetProfit = profit };

    /// <summary>8 quý thật của một mã (đo 2026-11-28), mới nhất trước, đúng thứ tự nguồn trả về.</summary>
    private static List<IncomeStatementItem> EightQuarters() => new()
    {
        Q(2026, 2, 13788.5035m, 2570.3391m), Q(2026, 1, 12479.9972m, 2476.7898m),
        Q(2025, 4, 20225.4504m, 2988.1486m), Q(2025, 3, 17204.5212m, 2901.5458m),
        Q(2025, 2, 16624.7130m, 2740.2714m), Q(2025, 1, 16058.1409m, 2595.5575m),
        Q(2024, 4, 17607.8178m, 2500.8465m), Q(2024, 3, 15902.8217m, 2478.5976m),
    };

    private static FundamentalsTrendDto Compute(IEnumerable<IncomeStatementItem> items, FinanceIndicators? ind = null, decimal? priceThousands = null)
        => FundamentalsTrendCalculator.Compute(items.ToList(), ind, priceThousands)!;

    private static QuarterTrend Quarter(FundamentalsTrendDto t, string period) => t.Quarters.Single(q => q.Period == period);

    // ---------------- Cùng quý năm trước

    [Fact]
    public void yoy_compares_each_quarter_with_the_same_quarter_last_year()
    {
        var t = Compute(EightQuarters());

        var q = Quarter(t, "Q2/2026");
        q.Revenue.Should().Be(13788.5035m);
        q.RevenueSameQuarterLastYear.Should().Be(16624.7130m);
        q.RevenueYoyPct.Should().Be(-17.1m);
        q.RevenueDirection.Should().Be("down");
        q.NetProfit.Should().Be(2570.3391m);
        q.NetProfitSameQuarterLastYear.Should().Be(2740.2714m);
        q.NetProfitYoyPct.Should().Be(-6.2m);
        q.NetProfitDirection.Should().Be("down");
    }

    [Fact]
    public void quarters_are_listed_newest_first_whatever_order_the_source_used()
    {
        var shuffled = EightQuarters().OrderBy(i => i.Period).ToList();

        var t = Compute(shuffled);

        t.Quarters.Select(q => q.Period).Should().Equal("Q2/2026", "Q1/2026", "Q4/2025", "Q3/2025", "Q2/2025", "Q1/2025", "Q4/2024", "Q3/2024");
    }

    [Fact]
    public void no_arrow_when_no_comparable_period()
    {
        // Q3/2024 là quý cũ nhất trong cửa sổ 8 quý: không có Q3/2023 để so, nên mọi trường so sánh là null (không phải 0, không có chiều).
        var q = Quarter(Compute(EightQuarters()), "Q3/2024");

        q.Revenue.Should().Be(15902.8217m);
        q.RevenueSameQuarterLastYear.Should().BeNull();
        q.RevenueYoyPct.Should().BeNull();
        q.RevenueDirection.Should().BeNull();
        q.NetProfitSameQuarterLastYear.Should().BeNull();
        q.NetProfitYoyPct.Should().BeNull();
        q.NetProfitDirection.Should().BeNull();
    }

    [Fact]
    public void yoy_needs_exactly_the_quarter_four_quarters_back_not_the_neighbour()
    {
        // Thiếu Q2/2025 (có Q1 và Q3): Q2/2026 không có đối chiếu, không được lấy quý lân cận thế vào.
        var items = EightQuarters().Where(i => i.Period != "Q2/2025").ToList();

        var q = Quarter(Compute(items), "Q2/2026");

        q.RevenueSameQuarterLastYear.Should().BeNull();
        q.RevenueYoyPct.Should().BeNull();
        q.RevenueDirection.Should().BeNull();
    }

    [Theory]
    [InlineData(100, 101, "up")]        // +1,0% đúng ngưỡng: coi là tăng
    [InlineData(100, 100.99, "flat")]   // +0,99%: đi ngang
    [InlineData(100, 100, "flat")]
    [InlineData(100, 99.01, "flat")]    // −0,99%
    [InlineData(100, 99, "down")]       // −1,0%: giảm
    [InlineData(100, 150, "up")]
    [InlineData(100, 50, "down")]
    public void yoy_arrow_up_down_flat_threshold_1pct(double previous, double current, string direction)
    {
        var items = new List<IncomeStatementItem> { Q(2026, 1, (decimal)current, (decimal)current), Q(2025, 1, (decimal)previous, (decimal)previous) };

        var q = Quarter(Compute(items), "Q1/2026");

        q.RevenueDirection.Should().Be(direction);
        q.NetProfitDirection.Should().Be(direction);
    }

    [Fact]
    public void percentages_are_rounded_to_one_decimal_but_the_threshold_uses_the_exact_value()
    {
        // +0,96% làm tròn thành 1,0 nhưng vẫn là đi ngang.
        var items = new List<IncomeStatementItem> { Q(2026, 1, 100.96m, 1m), Q(2025, 1, 100m, 1m) };

        var q = Quarter(Compute(items), "Q1/2026");

        q.RevenueYoyPct.Should().Be(1.0m);
        q.RevenueDirection.Should().Be("flat");
    }

    // ---------------- Lỗ và cơ sở bằng không

    [Fact]
    public void loss_to_profit_has_no_percentage_but_direction_up_and_a_note()
    {
        var items = new List<IncomeStatementItem> { Q(2026, 1, 500m, 80m), Q(2025, 1, 400m, -50m) };

        var q = Quarter(Compute(items), "Q1/2026");

        q.NetProfitYoyPct.Should().BeNull("phần trăm trên cơ sở âm là vô nghĩa");
        q.NetProfitDirection.Should().Be("up");
        q.NetProfitNote.Should().Be("từ lỗ sang lãi");
        q.NetProfitSameQuarterLastYear.Should().Be(-50m);
    }

    [Fact]
    public void profit_to_loss_has_no_percentage_but_direction_down_and_a_note()
    {
        var items = new List<IncomeStatementItem> { Q(2026, 1, 500m, -20m), Q(2025, 1, 400m, 50m) };

        var q = Quarter(Compute(items), "Q1/2026");

        q.NetProfitYoyPct.Should().BeNull();
        q.NetProfitDirection.Should().Be("down");
        q.NetProfitNote.Should().Be("từ lãi sang lỗ");
    }

    [Theory]
    [InlineData(-30, "up", "lỗ thu hẹp")]      // lỗ −50 → −30
    [InlineData(-80, "down", "lỗ tăng")]       // lỗ −50 → −80
    public void loss_to_loss_gives_direction_and_a_note_but_no_percentage(double current, string direction, string note)
    {
        var items = new List<IncomeStatementItem> { Q(2026, 1, 500m, (decimal)current), Q(2025, 1, 400m, -50m) };

        var q = Quarter(Compute(items), "Q1/2026");

        q.NetProfitYoyPct.Should().BeNull();
        q.NetProfitDirection.Should().Be(direction);
        q.NetProfitNote.Should().Be(note);
    }

    [Fact]
    public void a_zero_base_has_no_percentage()
    {
        var items = new List<IncomeStatementItem> { Q(2026, 1, 500m, 10m), Q(2025, 1, 0m, 0m) };

        var q = Quarter(Compute(items), "Q1/2026");

        q.RevenueYoyPct.Should().BeNull();
        q.RevenueDirection.Should().Be("up");
        q.RevenueNote.Should().Be("kỳ so sánh bằng 0");
    }

    [Fact]
    public void a_null_value_is_never_turned_into_zero()
    {
        var items = new List<IncomeStatementItem> { Q(2026, 1, null, null), Q(2025, 1, 400m, 50m) };

        var q = Quarter(Compute(items), "Q1/2026");

        q.Revenue.Should().BeNull();
        q.RevenueYoyPct.Should().BeNull();
        q.RevenueDirection.Should().BeNull();
        q.NetProfit.Should().BeNull();
        q.NetMargin.Should().BeNull();
        q.RevenueSameQuarterLastYear.Should().Be(400m);
    }

    // ---------------- Biên lợi nhuận ròng

    [Fact]
    public void margin_is_profit_over_revenue_in_percent_and_compared_with_the_same_quarter_last_year()
    {
        var q = Quarter(Compute(EightQuarters()), "Q2/2026");

        q.NetMargin.Should().BeApproximately(18.64m, 0.01m);                 // 2570,3391 / 13788,5035
        q.NetMarginSameQuarterLastYear.Should().BeApproximately(16.48m, 0.01m);
        q.NetMarginChangePp.Should().BeApproximately(2.16m, 0.02m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void margin_null_when_revenue_zero_or_missing(int revenue)
    {
        var q = Quarter(Compute(new[] { Q(2026, 1, revenue, 10m) }), "Q1/2026");

        q.NetMargin.Should().BeNull();
    }

    [Fact]
    public void a_loss_gives_a_negative_margin()
    {
        var q = Quarter(Compute(new[] { Q(2026, 1, 200m, -50m) }), "Q1/2026");

        q.NetMargin.Should().Be(-25m);
    }

    // ---------------- TTM

    [Fact]
    public void ttm_vs_previous_ttm()
    {
        var ttm = Compute(EightQuarters()).Ttm!;

        ttm.Period.Should().Be("Q2/2026");
        ttm.Revenue.Should().BeApproximately(63698.4723m, 0.001m);          // Q2/26 + Q1/26 + Q4/25 + Q3/25
        ttm.PrevRevenue.Should().BeApproximately(66193.4934m, 0.001m);      // Q2/25 + Q1/25 + Q4/24 + Q3/24
        ttm.RevenueYoyPct.Should().BeApproximately(-3.8m, 0.05m);
        ttm.RevenueDirection.Should().Be("down");
        ttm.NetProfit.Should().BeApproximately(10936.8233m, 0.001m);
        ttm.PrevNetProfit.Should().BeApproximately(10315.2730m, 0.001m);
        ttm.NetProfitYoyPct.Should().BeApproximately(6.0m, 0.05m);
        ttm.NetProfitDirection.Should().Be("up");
        ttm.NetMargin.Should().BeApproximately(17.17m, 0.01m);
        ttm.PrevNetMargin.Should().BeApproximately(15.58m, 0.01m);
    }

    [Fact]
    public void ttm_needs_four_consecutive_quarters_and_never_sums_a_partial_window()
    {
        // 7 quý (thiếu Q3/2024): TTM hiện tại vẫn đủ 4 quý, TTM trước thiếu một quý nên không có số trước, không cộng phần còn lại.
        var items = EightQuarters().Where(i => i.Period != "Q3/2024").ToList();

        var ttm = Compute(items).Ttm!;

        ttm.Revenue.Should().NotBeNull();
        ttm.PrevRevenue.Should().BeNull();
        ttm.RevenueYoyPct.Should().BeNull();
        ttm.RevenueDirection.Should().BeNull();
    }

    [Fact]
    public void a_gap_inside_the_latest_four_quarters_leaves_ttm_null()
    {
        var items = EightQuarters().Where(i => i.Period != "Q4/2025").ToList();

        var ttm = Compute(items).Ttm;

        ttm!.Revenue.Should().BeNull();
        ttm.NetProfit.Should().BeNull();
    }

    [Fact]
    public void ttm_profit_loss_to_profit_has_a_note_and_no_percentage()
    {
        var items = new List<IncomeStatementItem>
        {
            Q(2026, 1, 100, 10), Q(2025, 4, 100, 10), Q(2025, 3, 100, 10), Q(2025, 2, 100, 10),
            Q(2025, 1, 100, -10), Q(2024, 4, 100, -10), Q(2024, 3, 100, -10), Q(2024, 2, 100, -10),
        };

        var ttm = Compute(items).Ttm!;

        ttm.NetProfit.Should().Be(40m);
        ttm.PrevNetProfit.Should().Be(-40m);
        ttm.NetProfitYoyPct.Should().BeNull();
        ttm.NetProfitDirection.Should().Be("up");
        ttm.NetProfitNote.Should().Be("từ lỗ sang lãi");
    }

    [Fact]
    public void ttm_is_absent_when_there_are_no_quarters()
    {
        FundamentalsTrendCalculator.Compute(new List<IncomeStatementItem>(), null, null).Should().BeNull();
    }

    // ---------------- Cả năm

    [Fact]
    public void annual_view_requires_4_quarters()
    {
        var annual = Compute(EightQuarters()).Annual;

        annual.Select(a => a.Year).Should().Equal(2026, 2025, 2024);
        var y2025 = annual.Single(a => a.Year == 2025);
        y2025.Complete4Quarters.Should().BeTrue();
        y2025.QuartersPresent.Should().Be(4);
        y2025.Revenue.Should().BeApproximately(70112.8255m, 0.001m);
        y2025.NetProfit.Should().BeApproximately(11225.5233m, 0.001m);

        foreach (var partial in annual.Where(a => a.Year != 2025))
        {
            partial.Complete4Quarters.Should().BeFalse();
            partial.Revenue.Should().BeNull("năm chưa đủ 4 quý thì không có số cả năm, không cộng phần đã có");
            partial.NetProfit.Should().BeNull();
        }
        annual.Single(a => a.Year == 2026).QuartersPresent.Should().Be(2);
        annual.Single(a => a.Year == 2024).QuartersPresent.Should().Be(2);
    }

    [Fact]
    public void annual_yoy_only_when_both_years_have_four_quarters()
    {
        // Chỉ 2025 đủ 4 quý nên chưa có năm trước để so.
        Compute(EightQuarters()).Annual.Single(a => a.Year == 2025).RevenueYoyPct.Should().BeNull();

        var twoYears = new List<IncomeStatementItem>();
        foreach (var q in new[] { 1, 2, 3, 4 })
        {
            twoYears.Add(Q(2025, q, 100m, 10m));
            twoYears.Add(Q(2024, q, 80m, 20m));
        }

        var y2025 = Compute(twoYears).Annual.Single(a => a.Year == 2025);

        y2025.Revenue.Should().Be(400m);
        y2025.PrevYearRevenue.Should().Be(320m);
        y2025.RevenueYoyPct.Should().Be(25.0m);
        y2025.RevenueDirection.Should().Be("up");
        y2025.NetProfit.Should().Be(40m);
        y2025.PrevYearNetProfit.Should().Be(80m);
        y2025.NetProfitYoyPct.Should().Be(-50.0m);
        y2025.NetProfitDirection.Should().Be("down");
    }

    [Fact]
    public void a_year_with_a_missing_value_in_one_quarter_is_not_complete_for_that_metric()
    {
        var items = new List<IncomeStatementItem> { Q(2025, 1, 100, 10), Q(2025, 2, 100, 10), Q(2025, 3, 100, null), Q(2025, 4, 100, 10) };

        var y = Compute(items).Annual.Single();

        y.Complete4Quarters.Should().BeTrue("cả bốn quý đều có mặt");
        y.Revenue.Should().Be(400m);
        y.NetProfit.Should().BeNull("một quý thiếu lợi nhuận thì không có lợi nhuận cả năm");
    }

    // ---------------- Ghi chú và dữ liệu lạ

    [Fact]
    public void no_monthly_data_for_financials_note()
    {
        Compute(EightQuarters()).Note.Should().Be("Doanh nghiệp báo cáo theo quý; không có số liệu tháng");
    }

    [Fact]
    public void periods_that_are_not_quarters_are_ignored_not_misread()
    {
        var items = EightQuarters();
        items.Add(new IncomeStatementItem { Period = "2025", Revenue = 99999m, NetProfit = 99999m });   // số cả năm của nguồn
        items.Add(new IncomeStatementItem { Period = null, Revenue = 1m, NetProfit = 1m });
        items.Add(new IncomeStatementItem { Period = "Q5/2025", Revenue = 1m, NetProfit = 1m });

        var t = Compute(items);

        t.Quarters.Should().HaveCount(8);
        t.Annual.Single(a => a.Year == 2025).Revenue.Should().BeApproximately(70112.8255m, 0.001m);
    }

    [Fact]
    public void a_duplicated_quarter_keeps_the_first_one()
    {
        var items = EightQuarters();
        items.Add(Q(2026, 2, 1m, 1m));

        Quarter(Compute(items), "Q2/2026").Revenue.Should().Be(13788.5035m);
    }

    // ---------------- Vùng giá 52 tuần

    [Fact]
    public void range_52w_positions_the_current_price_between_low_and_high()
    {
        var ind = new FinanceIndicators { Min52W = 43.9m, Max52W = 68.2m };

        var r = Compute(EightQuarters(), ind, 44.6m).Range52W!;

        r.Low.Should().Be(43.9m);
        r.High.Should().Be(68.2m);
        r.Price.Should().Be(44.6m);
        r.PositionPct.Should().BeApproximately(2.9m, 0.05m);   // (44,6 − 43,9) / (68,2 − 43,9)
        r.OutsideRange.Should().BeFalse();
        r.Unit.Should().Be("nghìn đồng");
    }

    [Theory]
    [InlineData(43.9, 0)]
    [InlineData(68.2, 100)]
    [InlineData(56.05, 50)]
    public void range_52w_edges_and_middle(double price, double position)
    {
        var r = Compute(EightQuarters(), new FinanceIndicators { Min52W = 43.9m, Max52W = 68.2m }, (decimal)price).Range52W!;

        r.PositionPct.Should().BeApproximately((decimal)position, 0.05m);
    }

    [Theory]
    [InlineData(40.0, 0)]
    [InlineData(70.0, 100)]
    public void price_outside_the_range_is_clamped_and_flagged(double price, double position)
    {
        var r = Compute(EightQuarters(), new FinanceIndicators { Min52W = 43.9m, Max52W = 68.2m }, (decimal)price).Range52W!;

        r.PositionPct.Should().Be((decimal)position);
        r.OutsideRange.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, 68.2, 50.0)]
    [InlineData(43.9, null, 50.0)]
    [InlineData(50.0, 50.0, 50.0)]   // đỉnh bằng đáy: không có vùng
    [InlineData(60.0, 50.0, 55.0)]   // đỉnh nhỏ hơn đáy: dữ liệu hỏng
    public void range_52w_is_null_position_when_the_range_is_unusable(double? low, double? high, double price)
    {
        var ind = new FinanceIndicators { Min52W = (decimal?)low, Max52W = (decimal?)high };

        var r = Compute(EightQuarters(), ind, (decimal)price).Range52W;

        (r?.PositionPct).Should().BeNull();
    }

    [Fact]
    public void range_52w_without_a_current_price_keeps_low_and_high_with_a_null_position()
    {
        var r = Compute(EightQuarters(), new FinanceIndicators { Min52W = 43.9m, Max52W = 68.2m }, null).Range52W!;

        r.Low.Should().Be(43.9m);
        r.High.Should().Be(68.2m);
        r.Price.Should().BeNull();
        r.PositionPct.Should().BeNull();
    }

    [Fact]
    public void trend_exists_from_the_range_alone_when_there_are_no_quarters_and_is_null_when_nothing_is_computable()
    {
        FundamentalsTrendCalculator.Compute(new List<IncomeStatementItem>(), new FinanceIndicators { Min52W = 10m, Max52W = 20m }, 15m)!
            .Quarters.Should().BeEmpty();
        FundamentalsTrendCalculator.Compute(new List<IncomeStatementItem>(), new FinanceIndicators(), 15m).Should().BeNull();
    }
}
