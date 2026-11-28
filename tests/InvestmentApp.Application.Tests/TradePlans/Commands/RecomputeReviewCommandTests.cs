using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.RecomputeReview;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.TradePlans.Queries.PreviewPlanReview;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>"Tính lại review" do người dùng bấm + cờ chéo giữa các kế hoạch (cùng danh mục, cùng mã).</summary>
public class RecomputeReviewCommandTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    private readonly Mock<ITradePlanRepository> _plans = new();
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ICorporateActionRepository> _actions = new();
    private readonly Mock<ICampaignReviewService> _service = new();
    private readonly Portfolio _mine = new(Me, "Của tôi", 100_000_000m);

    public RecomputeReviewCommandTests()
    {
        _portfolios.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { _mine });
        _trades.Setup(r => r.GetByTradePlanIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Trade>());
    }

    private RecomputeReviewCommandHandler Sut()
        => new(_plans.Object, _trades.Object, _portfolios.Object, _actions.Object, _service.Object);

    private TradePlan ReviewedPlan(string userId = Me, string lessons = "Bài học của người dùng")
    {
        var plan = new TradePlan(userId, "HHV", "Buy", 12_000m, 11_000m, 14_000m, 1000,
            thesis: "Mua mẫu cho test tính lại review — luận điểm đủ dài tối thiểu");
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = 143_900_000m, PnLPercent = 99m, RealizedR = 20m, LessonsLearned = lessons, ReviewedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) });
        _plans.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return plan;
    }

    private void ServiceReturns(CampaignReviewData data)
        => _service.Setup(s => s.CalculateMetrics(It.IsAny<TradePlan>(), It.IsAny<IEnumerable<Trade>>(), It.IsAny<IEnumerable<CorporateAction>>()))
            .Returns(data);

    [Fact]
    public async Task recompute_keeps_user_text_and_stores_legacy_once()
    {
        var plan = ReviewedPlan();
        ServiceReturns(new CampaignReviewData { PnLAmount = 0m, PnLPercent = 0m, RealizedR = null, OversoldQuantity = 14_500m, Flags = new List<string> { ReviewFlags.MissingBuyLink } });

        var dto = await Sut().Handle(new RecomputeReviewCommand { PlanId = plan.Id, UserId = Me }, default);

        dto.PnLAmount.Should().Be(0m);
        dto.RealizedR.Should().BeNull();
        dto.LessonsLearned.Should().Be("Bài học của người dùng");
        dto.Legacy!.PnLAmount.Should().Be(143_900_000m);
        dto.Legacy.RealizedR.Should().Be(20m);
        dto.Flags.Should().Contain(ReviewFlags.MissingBuyLink);
        _plans.Verify(r => r.UpdateAsync(plan, It.IsAny<CancellationToken>()), Times.Once);

        // Lần hai: số liệu đổi tiếp nhưng Legacy vẫn là bản ghi đầu tiên
        ServiceReturns(new CampaignReviewData { PnLAmount = 5_000_000m, PnLPercent = 4m, RealizedR = 0.8m });
        var again = await Sut().Handle(new RecomputeReviewCommand { PlanId = plan.Id, UserId = Me }, default);

        again.PnLAmount.Should().Be(5_000_000m);
        again.Legacy!.PnLAmount.Should().Be(143_900_000m);
    }

    [Fact]
    public async Task recompute_foreign_or_missing_plan_same_404()
    {
        var foreign = ReviewedPlan(userId: Other);
        _plans.Setup(r => r.GetByIdAsync("khong-co", It.IsAny<CancellationToken>())).ReturnsAsync((TradePlan?)null);

        Exception? foreignEx = null, missingEx = null;
        try { await Sut().Handle(new RecomputeReviewCommand { PlanId = foreign.Id, UserId = Me }, default); } catch (Exception e) { foreignEx = e; }
        try { await Sut().Handle(new RecomputeReviewCommand { PlanId = "khong-co", UserId = Me }, default); } catch (Exception e) { missingEx = e; }

        foreignEx.Should().BeOfType<KeyNotFoundException>();
        missingEx.Should().BeOfType<KeyNotFoundException>();
        foreignEx!.Message.Replace(foreign.Id, "X").Should().Be(missingEx!.Message.Replace("khong-co", "X"));
        _plans.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task recompute_not_reviewed_plan_400()
    {
        var plan = new TradePlan(Me, "HHV", "Buy", 12_000m, 11_000m, 14_000m, 1000,
            thesis: "Mua mẫu cho test tính lại review — luận điểm đủ dài tối thiểu");
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("t1");   // Executed, chưa review
        _plans.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var act = () => Sut().Handle(new RecomputeReviewCommand { PlanId = plan.Id, UserId = Me }, default);

        await act.Should().ThrowAsync<ArgumentException>("ArgumentException được middleware đổi thành 400");
        _plans.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Cờ chéo giữa các kế hoạch ----------

    private (TradePlan Plan, Trade Buy) BuyOnlyPlan()
    {
        var plan = new TradePlan(Me, "HHV", "Buy", 12_000m, 11_000m, 14_000m, 1000,
            thesis: "Mua mẫu cho test cờ chéo kế hoạch — luận điểm đủ dài tối thiểu");
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("buy-1");
        var buy = new Trade(_mine.Id, "HHV", TradeType.BUY, 29_000, 12_400m);
        buy.LinkTradePlan(plan.Id);
        _trades.Setup(r => r.GetByIdAsync("buy-1", It.IsAny<CancellationToken>())).ReturnsAsync(buy);
        _trades.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { buy });
        _plans.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return (plan, buy);
    }

    [Fact]
    public async Task buy_only_with_sells_in_other_plan_flagged()
    {
        var (plan, buy) = BuyOnlyPlan();
        var sellElsewhere = new Trade(_mine.Id, "HHV", TradeType.SELL, 14_500, 9_950m);
        sellElsewhere.LinkTradePlan("plan-khac");
        _trades.Setup(r => r.GetByPortfolioIdAndSymbolAsync(_mine.Id, "HHV", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { buy, sellElsewhere });
        ServiceReturns(new CampaignReviewData { OpenQuantity = 29_000m, OversoldQuantity = 0m });

        var preview = await new PreviewPlanReviewQueryHandler(_plans.Object, _trades.Object, _portfolios.Object, _service.Object, _actions.Object)
            .Handle(new PreviewPlanReviewQuery { PlanId = plan.Id, UserId = Me }, default);

        preview.Flags.Should().Contain(ReviewFlags.SellsInOtherPlan);
        preview.OpenQuantity.Should().Be(29_000m);
    }

    [Fact]
    public async Task buy_only_without_sells_anywhere_is_not_flagged()
    {
        var (plan, buy) = BuyOnlyPlan();
        _trades.Setup(r => r.GetByPortfolioIdAndSymbolAsync(_mine.Id, "HHV", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { buy });
        ServiceReturns(new CampaignReviewData { OpenQuantity = 29_000m });

        var preview = await new PreviewPlanReviewQueryHandler(_plans.Object, _trades.Object, _portfolios.Object, _service.Object, _actions.Object)
            .Handle(new PreviewPlanReviewQuery { PlanId = plan.Id, UserId = Me }, default);

        preview.Flags.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task sells_of_this_very_plan_do_not_trigger_the_cross_plan_flag()
    {
        var (plan, buy) = BuyOnlyPlan();
        var ownSell = new Trade(_mine.Id, "HHV", TradeType.SELL, 1_000, 13_000m);
        ownSell.LinkTradePlan(plan.Id);
        _trades.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { buy, ownSell });
        _trades.Setup(r => r.GetByPortfolioIdAndSymbolAsync(_mine.Id, "HHV", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { buy, ownSell });
        ServiceReturns(new CampaignReviewData { OpenQuantity = 28_000m });

        var preview = await new PreviewPlanReviewQueryHandler(_plans.Object, _trades.Object, _portfolios.Object, _service.Object, _actions.Object)
            .Handle(new PreviewPlanReviewQuery { PlanId = plan.Id, UserId = Me }, default);

        preview.Flags.Should().BeNullOrEmpty();
    }
}
