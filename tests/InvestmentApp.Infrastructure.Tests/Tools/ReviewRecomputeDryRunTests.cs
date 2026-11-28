using FluentAssertions;
using InvestmentApp.Domain.Entities;
using Xunit.Abstractions;

namespace InvestmentApp.Infrastructure.Tests.Tools;

/// <summary>
/// Công cụ CHỈ ĐỌC (WS2b): tính lại mọi kế hoạch đã review bằng <c>CampaignReviewService</c> mới rồi in bảng cũ → mới.
/// Không có chế độ ghi. Chạy trên DB thật chỉ khi đặt biến môi trường (xem <see cref="Live_dry_run_reads_reviewed_plans_when_configured"/>).
/// </summary>
public class ReviewRecomputeDryRunTests
{
    private readonly ITestOutputHelper _output;
    public ReviewRecomputeDryRunTests(ITestOutputHelper output) => _output = output;

    private const string Portfolio = "port-1";

    private static TradePlan ReviewedPlan(CampaignReviewData stored, string symbol = "VNM")
    {
        var plan = new TradePlan("user-1", symbol, "Buy", 30_000m, 27_000m, 36_000m, 1000,
            thesis: "Mua mẫu cho công cụ chạy thử review — luận điểm đủ dài tối thiểu");
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("t1");
        plan.MarkReviewed(stored);
        return plan;
    }

    private static Trade Buy(decimal qty, decimal price, DateTime d, string symbol = "VNM") => new(Portfolio, symbol, TradeType.BUY, qty, price, 0, 0, d);
    private static Trade Sell(decimal qty, decimal price, DateTime d, string symbol = "VNM") => new(Portfolio, symbol, TradeType.SELL, qty, price, 0, 0, d);
    private static readonly DateTime Jan1 = new(2026, 1, 1);
    private static readonly DateTime Apr1 = new(2026, 4, 1);

    [Fact]
    public void partial_sell_review_stored_with_old_formula_is_flagged_and_recomputed()
    {
        // Công thức cũ: bán 500 @33.000 so với toàn bộ vốn 30tr → −13,5tr, R −4,5 (1R = 3tr).
        var plan = ReviewedPlan(new CampaignReviewData { PnLAmount = -13_500_000m, RealizedR = -4.5m, RiskBasis = "chot-ke-hoach" });
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(500, 33_000m, Apr1) };

        var row = ReviewRecomputeDryRun.Compute(plan, trades, Array.Empty<CorporateAction>());

        row.OldPnL.Should().Be(-13_500_000m);
        row.NewPnL.Should().Be(1_500_000m);
        row.OldR.Should().Be(-4.5m);
        row.NewR.Should().Be(1.0m);
        row.OpenQuantity.Should().Be(500m);
        row.Changed.Should().BeTrue();
    }

    [Fact]
    public void fully_sold_review_without_events_is_unchanged()
    {
        var plan = ReviewedPlan(new CampaignReviewData { PnLAmount = 3_000_000m, RealizedR = 1.0m, RiskBasis = "chot-ke-hoach" });
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 33_000m, Apr1) };

        var row = ReviewRecomputeDryRun.Compute(plan, trades, Array.Empty<CorporateAction>());

        row.Changed.Should().BeFalse();
        row.OpenQuantity.Should().Be(0m);
        row.DividendIncome.Should().Be(0m);
    }

    [Fact]
    public void a_stale_pnl_alone_is_enough_to_flag_a_row()
    {
        // Không có 1R (SL = giá vào) nên R luôn n/a: chỉ PnL lệch mới làm dòng này "changed".
        var plan = new TradePlan("user-1", "VNM", "Buy", 30_000m, 30_000m, 36_000m, 1000,
            thesis: "Mua mẫu cho công cụ chạy thử review — luận điểm đủ dài tối thiểu");
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = 3_100_000m });
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 33_000m, Apr1) };   // PnL thật 3,0tr

        var row = ReviewRecomputeDryRun.Compute(plan, trades, Array.Empty<CorporateAction>());

        row.OldR.Should().BeNull();
        row.NewR.Should().BeNull();
        row.Changed.Should().BeTrue();
    }

    [Fact]
    public void cash_dividend_makes_a_fully_sold_review_changed()
    {
        var plan = ReviewedPlan(new CampaignReviewData { PnLAmount = 0m, RealizedR = 0m, RiskBasis = "chot-ke-hoach" });
        var trades = new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 30_000m, Apr1) };
        var cash = CorporateAction.CashDividend(Portfolio, "user-1", "VNM", 10m, new DateTime(2026, 3, 1), null, 5m);

        var row = ReviewRecomputeDryRun.Compute(plan, trades, new[] { cash });

        row.DividendIncome.Should().Be(950_000m);
        row.NewPnL.Should().Be(950_000m);
        row.Changed.Should().BeTrue();
    }

    private static ReviewRecomputeDryRun.Row RowFor(CampaignReviewData stored, IReadOnlyList<Trade> trades, params CorporateAction[] actions)
        => ReviewRecomputeDryRun.Compute(ReviewedPlan(stored), trades, actions);

    [Fact]
    public void reason_open_qty_when_part_is_still_held()
        => RowFor(new CampaignReviewData { PnLAmount = -13_500_000m, RealizedR = -4.5m },
                  new[] { Buy(1000, 30_000m, Jan1), Sell(500, 33_000m, Apr1) }).Reason.Should().Be("open-qty");

    [Fact]
    public void reason_fees_on_open_when_nothing_was_sold()
        => RowFor(new CampaignReviewData { PnLAmount = -4_610_000m, RealizedR = -20m },
                  new[] { new Trade(Portfolio, "VNM", TradeType.BUY, 100, 46_000m, 10_000m, 0, Jan1) }).Reason.Should().Be("fees-on-open");

    [Fact]
    public void reason_dividend_when_a_cash_dividend_was_added()
        => RowFor(new CampaignReviewData { PnLAmount = 0m, RealizedR = 0m },
                  new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 30_000m, Apr1) },
                  CorporateAction.CashDividend(Portfolio, "user-1", "VNM", 10m, new DateTime(2026, 3, 1), null, 5m)).Reason.Should().Be("dividend");

    [Fact]
    public void reason_symbol_filter_when_the_plan_has_trades_of_another_symbol()
        => RowFor(new CampaignReviewData { PnLAmount = 2_000_000m, RealizedR = 0.67m },
                  new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 33_000m, Apr1), Buy(100, 10_000m, Jan1, "FPT") }).Reason.Should().Be("symbol-filter");

    [Fact]
    public void reason_other_oversell_when_sold_exceeds_bought()
        => RowFor(new CampaignReviewData { PnLAmount = 9_600_000m, RealizedR = 3.2m },
                  new[] { Buy(1000, 30_000m, Jan1), Sell(1200, 33_000m, Apr1) }).Reason.Should().Be("other:oversell");

    [Fact]
    public void reason_is_dash_when_the_row_is_unchanged()
        => RowFor(new CampaignReviewData { PnLAmount = 3_000_000m, RealizedR = 1.0m },
                  new[] { Buy(1000, 30_000m, Jan1), Sell(1000, 33_000m, Apr1) }).Reason.Should().Be("-");

    [Fact]
    public void table_has_a_reason_column()
    {
        var row = RowFor(new CampaignReviewData { PnLAmount = -13_500_000m, RealizedR = -4.5m },
                         new[] { Buy(1000, 30_000m, Jan1), Sell(500, 33_000m, Apr1) });

        var table = ReviewRecomputeDryRun.FormatTable(new[] { row });

        table.Should().Contain("reason").And.Contain("open-qty");
    }

    [Fact]
    public void trades_listing_prints_only_date_side_qty_price_and_short_portfolio()
    {
        var buy = new Trade("portfolio-abcdef123", "HHV", TradeType.BUY, 29_000, 12_345m, 10_000m, 5_000m, new DateTime(2026, 5, 4, 9, 15, 30));
        var sell = new Trade("portfolio-abcdef123", "HHV", TradeType.SELL, 1_000, 15_000m, 99m, 77m, new DateTime(2026, 6, 1));

        var text = ReviewRecomputeDryRun.FormatTrades(new[] { sell, buy });

        text.Should().Contain("2026-05-04 09:15:30").And.Contain("BUY").And.Contain("29,000").And.Contain("12,345").And.Contain("portfoli");
        text.Should().NotContain("portfolio-abcdef123", "chỉ in id rút gọn của danh mục");
        text.Should().NotContain(buy.Id).And.NotContain("10,000", "không in phí, thuế, id giao dịch");
        text.IndexOf("BUY", StringComparison.Ordinal).Should().BeLessThan(text.IndexOf("SELL", StringComparison.Ordinal), "sắp theo thời gian");
    }

    [Fact]
    public void table_uses_short_ids_and_never_prints_user_ids()
    {
        var plan = ReviewedPlan(new CampaignReviewData { PnLAmount = -13_500_000m, RealizedR = -4.5m });
        var row = ReviewRecomputeDryRun.Compute(plan, new[] { Buy(1000, 30_000m, Jan1), Sell(500, 33_000m, Apr1) }, Array.Empty<CorporateAction>());

        var table = ReviewRecomputeDryRun.FormatTable(new[] { row });

        table.Should().Contain(plan.Id[..8]).And.NotContain(plan.Id);
        table.Should().Contain("VNM").And.Contain("-13,500,000").And.Contain("1,500,000");
        table.Should().NotContain("user-1");
    }

    [Fact]
    public void aggregate_has_counts_only_no_symbols_or_ids()
    {
        var plan = ReviewedPlan(new CampaignReviewData { PnLAmount = -13_500_000m, RealizedR = -4.5m }, symbol: "FPT");
        var row = ReviewRecomputeDryRun.Compute(plan, new[] { Buy(1000, 30_000m, Jan1, "FPT"), Sell(500, 33_000m, Apr1, "FPT") }, Array.Empty<CorporateAction>());

        var text = ReviewRecomputeDryRun.FormatAggregate(new[] { row, row });

        text.Should().Contain("2").And.Contain("changed");
        text.Should().NotContain("FPT").And.NotContain(plan.Id[..8]);
    }

    [Fact]
    public void tool_source_contains_no_mongo_write_api()
    {
        // Rào chắn tĩnh: công cụ chỉ đọc. Nếu ai thêm đường ghi, test này đỏ.
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Tools", "ReviewRecomputeDryRun.cs");
        var source = File.ReadAllText(path);

        foreach (var forbidden in new[] { "InsertOne", "InsertMany", "UpdateOne", "UpdateMany", "ReplaceOne", "DeleteOne", "DeleteMany",
                                          "BulkWrite", "FindOneAndUpdate", "FindOneAndReplace", "FindOneAndDelete", "Indexes", "DropCollection",
                                          "RunCommand", "UserRepository", "TradePlanRepository" })
            source.Should().NotContain(forbidden, $"công cụ chạy thử không được có đường ghi ({forbidden})");
    }

    [Fact]
    public async Task Live_dry_run_reads_reviewed_plans_when_configured()
    {
        var conn = Environment.GetEnvironmentVariable("DRYRUN_MONGO_CONN");
        var db = Environment.GetEnvironmentVariable("DRYRUN_MONGO_DB");
        var mode = Environment.GetEnvironmentVariable("DRYRUN_MODE") ?? "count";   // count | test | others | aggregate
        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(db))
        {
            _output.WriteLine("Skipped — set DRYRUN_MONGO_CONN, DRYRUN_MONGO_DB (+ DRYRUN_MODE=count|test|others|aggregate, DRYRUN_TEST_EMAIL) to run read-only.");
            return;
        }

        var output = await ReviewRecomputeDryRun.RunLiveAsync(conn, db, mode, Environment.GetEnvironmentVariable("DRYRUN_TEST_EMAIL"), Environment.GetEnvironmentVariable("DRYRUN_PLAN_ID"));
        _output.WriteLine(output);
    }
}
