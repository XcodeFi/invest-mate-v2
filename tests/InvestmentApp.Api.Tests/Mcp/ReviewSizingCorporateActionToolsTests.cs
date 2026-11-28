using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.CorporateActions.Queries.ListMyCorporateActions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.CalculatePositionSizing;
using InvestmentApp.Application.TradePlans.Commands.RecomputeReview;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// recompute_plan_review / calculate_position_size / list_corporate_actions: UserId CHỈ từ người gọi, tool không thêm logic nghiệp vụ,
/// chỉ chuyển tiếp sang command/query dùng chung với REST.
/// </summary>
public class ReviewSizingCorporateActionToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    // ---------- recompute_plan_review ----------

    [Fact]
    public async Task RecomputePlanReview_SendsTheSharedCommand_WithTheCallerAndPlanId()
    {
        McpTestContext.Capture<CampaignReviewDto, RecomputeReviewCommand>(_mediator, out var sent, new CampaignReviewDto());

        await PlanReviewTools.RecomputePlanReview("plan-1", _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        sent()!.UserId.Should().Be("u-1");
        sent()!.PlanId.Should().Be("plan-1");
    }

    [Fact]
    public async Task RecomputePlanReview_ReturnsTheHandlerResult()
    {
        var dto = new CampaignReviewDto { RealizedR = null, OversoldQuantity = 14500m };
        McpTestContext.Capture<CampaignReviewDto, RecomputeReviewCommand>(_mediator, out _, dto);

        var result = await PlanReviewTools.RecomputePlanReview("plan-1", _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        result.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task RecomputePlanReview_PropagatesNotFound_SoMissingAndForeignLookTheSame()
    {
        _mediator.Setup(m => m.Send(It.IsAny<RecomputeReviewCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Trade plan x not found"));

        var act = () => PlanReviewTools.RecomputePlanReview("x", _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---------- calculate_position_size ----------

    [Fact]
    public async Task CalculatePositionSize_SendsTheSharedQuery_WithTheCallerAndTheFlags()
    {
        McpTestContext.Capture<PositionSizingResult, CalculatePositionSizingQuery>(_mediator, out var sent, new PositionSizingResult());

        await RiskTools.CalculatePositionSize(1_000_000_000m, 100_000m, 95_000m, _mediator.Object, McpTestContext.WithUser("u-2"),
            CancellationToken.None, useHistory: true, strategyId: "s1");

        sent()!.UserId.Should().Be("u-2");
        sent()!.UseHistory.Should().BeTrue();
        sent()!.StrategyId.Should().Be("s1");
        sent()!.Request.AccountBalance.Should().Be(1_000_000_000m);
        sent()!.Request.EntryPrice.Should().Be(100_000m);
        sent()!.Request.StopLoss.Should().Be(95_000m);
    }

    [Fact]
    public async Task CalculatePositionSize_OmittedOptionals_KeepTheRestApiDefaults()
    {
        McpTestContext.Capture<PositionSizingResult, CalculatePositionSizingQuery>(_mediator, out var sent, new PositionSizingResult());

        await RiskTools.CalculatePositionSize(1_000_000_000m, 100_000m, 95_000m, _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);

        var req = sent()!.Request;
        req.RiskPercent.Should().Be(2m);
        req.MaxPositionPercent.Should().Be(20m);
        req.AtrMultiplier.Should().Be(2m);
        req.Atr.Should().BeNull();
        req.WinRate.Should().BeNull();
        sent()!.UseHistory.Should().BeFalse("bỏ trống useHistory = hành vi cũ của REST");
        sent()!.StrategyId.Should().BeNull();
    }

    [Fact]
    public async Task CalculatePositionSize_PassesEveryOptionalInput()
    {
        McpTestContext.Capture<PositionSizingResult, CalculatePositionSizingQuery>(_mediator, out var sent, new PositionSizingResult());

        await RiskTools.CalculatePositionSize(1_000_000_000m, 100_000m, 95_000m, _mediator.Object, McpTestContext.WithUser("u-2"),
            CancellationToken.None, riskPercent: 1m, maxPositionPercent: 10m, atr: 2_000m, atrMultiplier: 3m,
            winRate: 0.55m, averageWin: 2_000_000m, averageLoss: 1_000_000m, atrPercent: 2.5m);

        var req = sent()!.Request;
        req.RiskPercent.Should().Be(1m);
        req.MaxPositionPercent.Should().Be(10m);
        req.Atr.Should().Be(2_000m);
        req.AtrMultiplier.Should().Be(3m);
        req.WinRate.Should().Be(0.55m);
        req.AverageWin.Should().Be(2_000_000m);
        req.AverageLoss.Should().Be(1_000_000m);
        req.AtrPercent.Should().Be(2.5m);
    }

    [Theory]
    [InlineData(0, 100, 95, 2, "accountBalance")]
    [InlineData(1000, 0, 95, 2, "entryPrice")]
    [InlineData(1000, 100, 0, 2, "stopLoss")]
    [InlineData(1000, 100, 95, 0, "riskPercent")]
    [InlineData(1000, 100, 95, 101, "riskPercent")]
    public async Task CalculatePositionSize_OutOfRangeInput_IsRefusedLikeTheRestApi_AndNothingIsSent(
        double balance, double entry, double stop, double risk, string field)
    {
        var act = () => RiskTools.CalculatePositionSize((decimal)balance, (decimal)entry, (decimal)stop, _mediator.Object,
            McpTestContext.WithUser("u-2"), CancellationToken.None, riskPercent: (decimal)risk);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain(field);
        _mediator.Verify(m => m.Send(It.IsAny<CalculatePositionSizingQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CalculatePositionSize_WinRateAbove1_IsRefused()
    {
        var act = () => RiskTools.CalculatePositionSize(1000m, 100m, 95m, _mediator.Object, McpTestContext.WithUser("u-2"),
            CancellationToken.None, winRate: 55m);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("winRate");
    }

    // ---------- list_corporate_actions ----------

    [Fact]
    public async Task ListCorporateActions_SendsTheCallerAndFilters()
    {
        McpTestContext.Capture<List<MyCorporateActionDto>, ListMyCorporateActionsQuery>(_mediator, out var sent, new());

        await CorporateActionTools.ListCorporateActions(_mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None,
            symbol: "hpg", portfolioId: "p-1");

        sent()!.UserId.Should().Be("u-3");
        sent()!.Symbol.Should().Be("hpg");
        sent()!.PortfolioId.Should().Be("p-1");
    }

    [Fact]
    public async Task ListCorporateActions_NoFilters_AsksForEverythingTheCallerOwns()
    {
        McpTestContext.Capture<List<MyCorporateActionDto>, ListMyCorporateActionsQuery>(_mediator, out var sent, new());

        await CorporateActionTools.ListCorporateActions(_mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);

        sent()!.PortfolioId.Should().BeNull();
        sent()!.Symbol.Should().BeNull();
    }
}
