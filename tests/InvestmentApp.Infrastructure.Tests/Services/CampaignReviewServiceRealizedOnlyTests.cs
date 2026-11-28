using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Review chỉ tính phần ĐÃ CHỐT: giá vốn bình quân × số đã bán, cộng cổ tức tiền mặt theo quyền hưởng của riêng plan.
/// Quy ước các test: mua 1.000 @30.000, SL 27.000 → 1R = 3.000.000.
/// </summary>
public class CampaignReviewServiceRealizedOnlyTests
{
    private const string Portfolio = "port-1";
    private readonly CampaignReviewService _sut = new();

    private static TradePlan NewPlan(string symbol = "VNM", int qty = 1000)
    {
        var plan = new TradePlan("user-1", symbol, "Buy", 30_000m, 27_000m, 36_000m, qty,
            thesis: "Mua mẫu cho test review phần đã chốt — luận điểm đủ dài tối thiểu");
        plan.MarkReady();
        return plan;
    }

    private static Trade Buy(decimal qty, decimal price, DateTime date, decimal fee = 0, decimal tax = 0, string symbol = "VNM")
        => new(Portfolio, symbol, TradeType.BUY, qty, price, fee, tax, date);

    private static Trade Sell(decimal qty, decimal price, DateTime date, decimal fee = 0, decimal tax = 0, string symbol = "VNM")
        => new(Portfolio, symbol, TradeType.SELL, qty, price, fee, tax, date);

    private static CorporateAction Bonus30(DateTime exDate, string symbol = "VNM")
        => CorporateAction.StockDividend(Portfolio, "user-1", symbol, 100m, 130m, exDate, null);

    /// <summary>Cổ tức 10% mệnh giá = 1.000đ/CP, thuế 5% → ròng 950đ/CP.</summary>
    private static CorporateAction Cash10(DateTime exDate, DateTime? settled = null, string symbol = "VNM")
    {
        var a = CorporateAction.CashDividend(Portfolio, "user-1", symbol, 10m, exDate, null, 5m);
        if (settled.HasValue) a.MarkSettled(settled.Value);
        return a;
    }

    private static readonly DateTime Jan1 = new(2026, 1, 1);
    private static readonly DateTime Mar1 = new(2026, 3, 1);
    private static readonly DateTime Apr1 = new(2026, 4, 1);

    private static readonly CorporateAction[] NoActions = Array.Empty<CorporateAction>();

    [Fact]
    public void partial_sell_realized_only()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(500, 33_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, NoActions);

        r.PnLAmount.Should().Be(1_500_000m);           // 500 × (33k − 30k), KHÔNG phải −13,5tr
        r.RealizedR.Should().Be(1.0m, "1R của phần đã bán = 3tr × 500/1000 = 1,5tr");
        r.OpenQuantity.Should().Be(500m);
        r.RealizedCost.Should().Be(15_000_000m);
        r.PnLPercent.Should().Be(10m);
    }

    [Fact]
    public void full_sell_unchanged_vs_old_formula()
    {
        var trades = new[]
        {
            Buy(33, 80_123m, Jan1, fee: 1_234m, tax: 12m),
            Buy(67, 79_001m, Jan1.AddDays(10), fee: 2_345m),
            Sell(40, 85_500m, Mar1, fee: 999m, tax: 3_420m),
            Sell(60, 86_250m, Apr1, fee: 1_500m, tax: 5_175m),
        };
        var oldInvested = trades.Where(t => t.TradeType == TradeType.BUY).Sum(t => t.Quantity * t.Price + t.Fee + t.Tax);
        var oldReturned = trades.Where(t => t.TradeType == TradeType.SELL).Sum(t => t.Quantity * t.Price - t.Fee - t.Tax);

        var r = _sut.CalculateMetrics(NewPlan(), trades, NoActions);

        r.PnLAmount.Should().BeApproximately(oldReturned - oldInvested, 0.01m);
        r.OpenQuantity.Should().Be(0m);
        r.TotalInvested.Should().Be(oldInvested);
        r.TotalReturned.Should().Be(oldReturned);
        r.DividendIncome.Should().Be(0m);
    }

    [Fact]
    public void stock_dividend_then_sell_all()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1300, 25_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, new[] { Bonus30(Mar1) });

        r.PnLAmount.Should().Be(2_500_000m);          // 32,5tr − 30tr
        r.OpenQuantity.Should().Be(0m);
        r.RealizedR.Should().BeApproximately(0.8333m, 0.0001m);
    }

    [Fact]
    public void stock_dividend_partial_sell_divides_risk_by_adjusted_quantity()
    {
        // Giữ 1.300 CP (vốn 30tr → 23.076,92/CP). Bán 650 @25.000: lãi 650 × 1.923,08 = 1,25tr.
        // 1R phần đã bán = 3tr × 650/1.300 = 1,5tr → 0,8333R. Chia theo 1.000 (gốc) sẽ ra 0,6410R.
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(650, 25_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, new[] { Bonus30(Mar1) });

        r.OpenQuantity.Should().Be(650m);
        r.PnLAmount.Should().BeApproximately(1_250_000m, 1m);
        r.RealizedR.Should().BeApproximately(0.8333m, 0.0001m);
    }

    [Fact]
    public void cash_dividend_entitlement_added()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 30_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, new[] { Cash10(Mar1) });   // chưa về tài khoản vẫn tính

        r.DividendIncome.Should().Be(950_000m);
        r.PnLAmount.Should().Be(950_000m);
        r.RealizedR.Should().BeApproximately(0.3167m, 0.0001m);
    }

    [Fact]
    public void cash_dividend_settled_is_not_counted_twice()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 30_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, new[] { Cash10(Mar1, settled: Mar1.AddDays(20)) });

        r.DividendIncome.Should().Be(950_000m);
    }

    [Fact]
    public void cash_dividend_outside_window_ignored()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 30_000m, Apr1) };
        var actions = new[]
        {
            Cash10(new DateTime(2025, 12, 15)),     // GDKHQ trước ngày mua đầu: chưa sở hữu
            Cash10(new DateTime(2026, 5, 10)),      // sau ngày bán cuối: đã hết cổ phiếu
        };

        var r = _sut.CalculateMetrics(NewPlan(), trades, actions);

        r.DividendIncome.Should().Be(0m);
        r.PnLAmount.Should().Be(0m);
    }

    [Fact]
    public void cash_dividend_other_symbol_ignored()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 30_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, new[] { Cash10(Mar1, symbol: "FPT") });

        r.DividendIncome.Should().Be(0m);
    }

    [Fact]
    public void two_plans_same_symbol_no_double_count()
    {
        var action = new[] { Cash10(Mar1) };   // cùng một sự kiện cho cả hai plan
        var planA = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 30_000m, Apr1) };
        var planB = new[] { Buy(500, 31_000m, Jan1.AddDays(5)), Sell(500, 31_000m, Apr1) };

        var a = _sut.CalculateMetrics(NewPlan(), planA, action);
        var b = _sut.CalculateMetrics(NewPlan(), planB, action);

        a.DividendIncome.Should().Be(950_000m);       // 1.000 CP của riêng A
        b.DividendIncome.Should().Be(475_000m);       // 500 CP của riêng B
        (a.DividendIncome + b.DividendIncome).Should().Be(1_425_000m);   // = 1.500 CP × 950, không phải 2 × 1.425.000
    }

    [Fact]
    public void cash_dividend_counts_only_quantity_still_held_at_ex_date()
    {
        // Bán 400 trước GDKHQ: chỉ 600 CP hưởng cổ tức.
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(400, 30_000m, Mar1.AddDays(-5)), Sell(600, 30_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, new[] { Cash10(Mar1) });

        r.DividendIncome.Should().Be(570_000m);
    }

    [Fact]
    public void nothing_sold_yet_has_no_realized_pnl_and_no_r()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1, fee: 45_000m) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, NoActions);

        r.PnLAmount.Should().Be(0m, "phí mua của phần chưa bán nằm trong giá vốn còn giữ, chưa phải lỗ");
        r.OpenQuantity.Should().Be(1000m);
        r.PnLPercent.Should().Be(0m);
        r.RealizedR.Should().BeNull("chưa chốt phần nào thì không có R");
    }

    [Fact]
    public void same_day_round_trip_is_matched_even_when_the_sell_is_listed_before_the_buy()
    {
        // PositionBuilder xử lý theo thứ tự đầu vào trong cùng một ngày; lệnh bán đứng trước lệnh mua bị coi là
        // "bán khi chưa có gì" và bị bỏ → PnL 0 và còn 1.000 CP giả. Service phải xếp mua trước bán trong ngày.
        var trades = new[] { Sell(1000, 33_000m, Apr1), Buy(1000, 30_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, NoActions);

        r.PnLAmount.Should().Be(3_000_000m);
        r.OpenQuantity.Should().Be(0m);
    }

    [Fact]
    public void trades_of_another_symbol_linked_to_the_plan_are_ignored()
    {
        // Công thức cũ cộng mọi trade gắn vào plan, kể cả mã khác (dữ liệu gắn nhầm). Giờ chỉ tính mã của plan.
        var trades = new[]
        {
            Buy(1000, 30_000m, Jan1), Sell(1000, 33_000m, Apr1),
            Buy(100, 10_000m, Jan1, symbol: "FPT"),
        };

        var r = _sut.CalculateMetrics(NewPlan(), trades, NoActions);

        r.PnLAmount.Should().Be(3_000_000m, "cũ: 3tr − 1tr vốn FPT chưa bán = 2tr");
        r.OpenQuantity.Should().Be(0m);
    }

    [Fact]
    public void selling_more_than_held_is_clamped_to_the_held_quantity()
    {
        // Nhập thiếu một lệnh mua: bán 1.200 trong khi chỉ có 1.000. Phần vượt không có giá vốn nên không tính lãi.
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1200, 33_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, NoActions);

        r.PnLAmount.Should().Be(3_000_000m, "cũ: 39,6tr − 30tr = 9,6tr, tính cả 200 CP không có giá vốn");
    }

    [Fact]
    public void oversold_matched_only_flag_r_null()
    {
        // Hình dạng b4ad04c1: kế hoạch chỉ có lệnh BÁN 14.500 @9.950, lệnh mua nằm ở kế hoạch khác.
        var plan = NewPlan("HHV", 29_000);
        var trades = new[] { Sell(14_500, 9_950m, Apr1, symbol: "HHV") };

        var r = _sut.CalculateMetrics(plan, trades, NoActions);

        r.PnLAmount.Should().Be(0m, "không có lệnh mua gắn vào thì không có giá vốn để khớp");
        r.OversoldQuantity.Should().Be(14_500m);
        r.OpenQuantity.Should().Be(0m);
        r.RealizedR.Should().BeNull("R = null, không bao giờ là 0");
        r.Flags.Should().Contain(InvestmentApp.Domain.Services.ReviewFlags.MissingBuyLink);
    }

    [Fact]
    public void partially_oversold_keeps_matched_pnl_but_r_is_null()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1200, 33_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, NoActions);

        r.PnLAmount.Should().Be(3_000_000m);
        r.OversoldQuantity.Should().Be(200m);
        r.RealizedR.Should().BeNull();
        r.Flags.Should().Contain(InvestmentApp.Domain.Services.ReviewFlags.MissingBuyLink);
    }

    [Fact]
    public void normal_review_has_no_oversold_and_no_flag()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 33_000m, Apr1) };

        var r = _sut.CalculateMetrics(NewPlan(), trades, NoActions);

        r.OversoldQuantity.Should().Be(0m);
        r.Flags.Should().BeNullOrEmpty();
        r.RealizedR.Should().Be(1.0m);
    }

    [Fact]
    public void two_arg_overload_behaves_like_no_corporate_actions()
    {
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 33_000m, Apr1) };

        _sut.CalculateMetrics(NewPlan(), trades).PnLAmount.Should().Be(3_000_000m);
    }
}
