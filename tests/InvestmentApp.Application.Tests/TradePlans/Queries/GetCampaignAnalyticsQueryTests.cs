using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetCampaignAnalytics;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

/// <summary>
/// Thống kê chiến dịch: tỉ lệ thắng, lãi/lỗ trung bình và tốt nhất/tệ nhất chỉ tính review ĐÃ ĐÓNG (không còn cổ phiếu chưa bán, không bán vượt
/// số mua đã gắn); review cũ chưa có hai trường đó (null) tính là đã đóng. Tổng tích lũy và đường lũy kế giữ MỌI review: tiền đã chốt là tiền thật.
/// Review còn mở chưa bán gì có P&amp;L 0 nên đang bị đếm là "không thắng" và kéo tỉ lệ thắng xuống.
/// </summary>
public class GetCampaignAnalyticsQueryTests
{
    private readonly Mock<ITradePlanRepository> _plans = new();
    private int _n;

    private TradePlan Reviewed(decimal pnl, decimal pct, decimal perDay, int days = 10, decimal? openQty = null, decimal? oversold = null, string symbol = "VNM")
    {
        var plan = new TradePlan("u1", symbol, "Buy", 100m, 90m, 130m, 100, thesis: "Luận điểm mẫu đủ dài cho test thống kê chiến dịch");
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("t" + _n);
        plan.MarkReviewed(new CampaignReviewData
        {
            PnLAmount = pnl, PnLPercent = pct, PnLPerDay = perDay, HoldingDays = days,
            ReviewedAt = new DateTime(2026, 1, 1).AddDays(_n++),
            OpenQuantity = openQty, OversoldQuantity = oversold
        });
        return plan;
    }

    private async Task<CampaignAnalyticsDto> Run(params TradePlan[] plans)
    {
        _plans.Setup(r => r.GetReviewedByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(plans.ToList());
        return await new GetCampaignAnalyticsQueryHandler(_plans.Object)
            .Handle(new GetCampaignAnalyticsQuery { UserId = "u1" }, CancellationToken.None);
    }

    [Fact]
    public async Task open_review_excluded_from_winrate_but_in_cumulative()
    {
        var win = Reviewed(1_000_000m, 10m, 100_000m, openQty: 0m);
        var loss = Reviewed(-500_000m, -5m, -50_000m, openQty: 0m);
        var openNothingSold = Reviewed(0m, 0m, 0m, openQty: 100m);                 // còn mở, chưa bán gì: P&L 0
        var openPartial = Reviewed(300_000m, 3m, 30_000m, openQty: 50m);          // còn mở, đã chốt một phần

        var dto = await Run(win, loss, openNothingSold, openPartial);

        dto.WinningCampaigns.Should().Be(1);
        dto.LosingCampaigns.Should().Be(1);
        dto.WinRate.Should().Be(50m, "1 thắng / 2 đã đóng, không kéo xuống bởi lệnh còn mở");
        dto.AveragePnLPercent.Should().Be(2.5m, "(10 − 5) / 2");
        dto.AveragePnLPerDay.Should().Be(25_000m, "(100.000 − 50.000) / 2");
        dto.TotalCampaigns.Should().Be(4, "tổng số chiến dịch đã review gồm cả còn mở");
        dto.ExcludedOpenReviews.Should().Be(2);
        dto.TotalAccumulatedPnL.Should().Be(800_000m, "tiền đã chốt của review còn mở vẫn tính: 1.000.000 − 500.000 + 0 + 300.000");
        dto.Trend.Should().HaveCount(4);
        dto.Trend[^1].CumulativePnL.Should().Be(800_000m);
    }

    [Fact]
    public async Task best_and_worst_use_closed_reviews_only()
    {
        var closedBest = Reviewed(1_000_000m, 10m, 100_000m, openQty: 0m, symbol: "AAA");
        var closedWorst = Reviewed(-500_000m, -5m, -50_000m, openQty: 0m, symbol: "BBB");
        var openHuge = Reviewed(9_000_000m, 90m, 900_000m, openQty: 10m, symbol: "OPEN");       // sẽ thành "tốt nhất" nếu không loại
        var openAwful = Reviewed(-9_000_000m, -90m, -900_000m, oversold: 5m, symbol: "OVER");    // sẽ thành "tệ nhất" nếu không loại

        var dto = await Run(closedBest, closedWorst, openHuge, openAwful);

        dto.BestCampaign!.Symbol.Should().Be("AAA");
        dto.WorstCampaign!.Symbol.Should().Be("BBB");
    }

    [Fact]
    public async Task oversold_review_excluded()
    {
        var win = Reviewed(1_000_000m, 10m, 100_000m, openQty: 0m);
        var oversold = Reviewed(0m, 0m, 0m, openQty: 0m, oversold: 14_500m);   // chỉ có lệnh bán, P&L khớp được = 0

        var dto = await Run(win, oversold);

        dto.WinRate.Should().Be(100m, "review bán vượt không phải một chiến dịch thua");
        dto.ExcludedOpenReviews.Should().Be(1);
        dto.WinningCampaigns.Should().Be(1);
        dto.LosingCampaigns.Should().Be(0);
        dto.TotalAccumulatedPnL.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task legacy_review_counts_as_closed()
    {
        // review cũ: OpenQuantity và OversoldQuantity đều null → hành vi hiện tại giữ nguyên
        var win = Reviewed(1_000_000m, 10m, 100_000m);
        var loss = Reviewed(-500_000m, -5m, -50_000m);

        var dto = await Run(win, loss);

        dto.WinRate.Should().Be(50m);
        dto.ExcludedOpenReviews.Should().Be(0);
        dto.TotalCampaigns.Should().Be(2);
        dto.AveragePnLPercent.Should().Be(2.5m);
    }

    [Fact]
    public async Task all_open_gives_null_rates()
    {
        var a = Reviewed(0m, 0m, 0m, openQty: 100m);
        var b = Reviewed(200_000m, 2m, 20_000m, openQty: 30m);
        var c = Reviewed(0m, 0m, 0m, oversold: 10m);

        var dto = await Run(a, b, c);

        dto.WinRate.Should().BeNull("không có chiến dịch đã đóng thì không có tỉ lệ thắng, không phải 0%");
        dto.AveragePnLPercent.Should().BeNull();
        dto.AveragePnLPerDay.Should().BeNull();
        dto.BestCampaign.Should().BeNull();
        dto.WorstCampaign.Should().BeNull();
        dto.WinningCampaigns.Should().Be(0);
        dto.LosingCampaigns.Should().Be(0);
        dto.ExcludedOpenReviews.Should().Be(3);
        dto.TotalCampaigns.Should().Be(3);
        dto.TotalAccumulatedPnL.Should().Be(200_000m, "tiền đã chốt vẫn tính");
        dto.Trend.Should().HaveCount(3);
    }

    [Fact]
    public async Task no_reviews_at_all_gives_an_empty_result_with_null_rates()
    {
        var dto = await Run();

        dto.TotalCampaigns.Should().Be(0);
        dto.WinRate.Should().BeNull();
        dto.ExcludedOpenReviews.Should().Be(0);
    }

    [Fact]
    public async Task a_review_with_open_quantity_zero_is_closed()
    {
        var closed = Reviewed(100m, 1m, 10m, openQty: 0m, oversold: 0m);

        var dto = await Run(closed);

        dto.ExcludedOpenReviews.Should().Be(0);
        dto.WinRate.Should().Be(100m);
    }

    [Fact]
    public async Task a_closed_review_with_zero_pnl_is_a_loss_not_a_win()
    {
        // hoà vốn thật (đã đóng, P&L 0) vẫn là "không thắng" như cũ; chỉ review còn mở/bán vượt bị loại
        var flat = Reviewed(0m, 0m, 0m, openQty: 0m);
        var win = Reviewed(100m, 1m, 10m, openQty: 0m);

        var dto = await Run(flat, win);

        dto.WinningCampaigns.Should().Be(1);
        dto.LosingCampaigns.Should().Be(1);
        dto.WinRate.Should().Be(50m);
    }
}
