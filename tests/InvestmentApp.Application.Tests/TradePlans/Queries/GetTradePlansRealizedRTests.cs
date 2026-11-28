using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

/// <summary>R thực tế của chiến dịch đã review: kế hoạch cũ thiếu giá trị thì tính khi đọc, chỉ trong DTO.</summary>
public class GetTradePlansRealizedRTests
{
    private static TradePlan Reviewed(decimal pnl, decimal? storedR = null, string? basis = null, bool clearSnapshot = false)
    {
        var plan = new TradePlan("u1", "VNM", "Buy", 100m, 90m, 130m, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");   // 1R = 1.000
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = pnl, RealizedR = storedR, RiskBasis = basis });
        if (clearSnapshot)
            typeof(TradePlan).GetProperty(nameof(TradePlan.InitialRisk))!.SetValue(plan, null);
        return plan;
    }

    private static async Task<(TradePlanDto Dto, Mock<ITradePlanRepository> Repo)> Read(TradePlan plan)
    {
        var repo = new Mock<ITradePlanRepository>();
        repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradePlan> { plan });
        var dto = (await new GetTradePlansQueryHandler(repo.Object)
            .Handle(new GetTradePlansQuery { UserId = "u1" }, CancellationToken.None)).Single();
        return (dto, repo);
    }

    [Fact]
    public async Task read_computes_missing_realized_r_in_dto_only()
    {
        var plan = Reviewed(pnl: 3_000m, clearSnapshot: true);

        var (dto, repo) = await Read(plan);

        dto.ReviewData!.RealizedR.Should().Be(3m);
        dto.ReviewData.RiskBasis.Should().Be("uoc-tinh");
        plan.ReviewData!.RealizedR.Should().BeNull("không gán vào entity: UpdateReviewLessons lưu cả ReviewData xuống prod");
        repo.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task read_of_an_open_review_with_nothing_sold_exposes_r_null_not_zero()
    {
        // Kế hoạch 8d9ef87d sau Tính lại: còn mở, chưa bán gì, RealizedR lưu null. API không được suy ra 0R giả.
        var plan = Reviewed(pnl: 0m);
        plan.ReviewData!.OpenQuantity = 100m;

        var (dto, _) = await Read(plan);

        dto.ReviewData!.RealizedR.Should().BeNull();
        dto.ReviewData.RiskBasis.Should().BeNull();
        dto.ReviewData.OpenQuantity.Should().Be(100m);
    }

    [Fact]
    public async Task read_returns_the_stored_realized_r_when_present()
    {
        var (dto, _) = await Read(Reviewed(pnl: 5_000m, storedR: 1.5m, basis: "chot-ke-hoach"));

        dto.ReviewData!.RealizedR.Should().Be(1.5m);
        dto.ReviewData.RiskBasis.Should().Be("chot-ke-hoach");
    }

    [Fact]
    public async Task read_leaves_realized_r_null_when_the_risk_cannot_be_known()
    {
        var plan = new TradePlan("u1", "VNM", "Buy", 100m, 100m, 130m, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = 500m });

        var (dto, _) = await Read(plan);

        dto.ReviewData!.RealizedR.Should().BeNull();
        dto.ReviewData.RiskBasis.Should().BeNull();
    }
}
