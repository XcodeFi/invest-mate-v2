using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Alerts.Queries.GetAlertHistory;
using InvestmentApp.Application.Alerts.Queries.GetAlertRules;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Application.Portfolios.Queries.GetPnL;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetExecutedPlansForReview;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioHistory;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioSuggestion;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioTemplates;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Application.TradePlans.Queries.PreviewPlanReview;
using InvestmentApp.Domain.Entities;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>Đợt 3: UserId CHỈ lấy từ người gọi; đầu vào của get_scenario_suggestion được kiểm trước khi gọi query.</summary>
public class PlansPnlAlertsToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    // ---------- UserId từ người gọi, mỗi tool ----------

    [Fact]
    public async Task ListScenarioTemplates_SetsUserId_FromTheCaller()
    {
        McpTestContext.Capture<List<ScenarioPresetDto>, GetScenarioTemplatesQuery>(_mediator, out var sent, new());
        await PlanInsightTools.ListScenarioTemplates(_mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task GetPlanScenarioHistory_SetsUserId_FromTheCaller_AndPlanId()
    {
        McpTestContext.Capture<List<ScenarioHistoryDto>, GetScenarioHistoryQuery>(_mediator, out var sent, new());
        await PlanInsightTools.GetPlanScenarioHistory("plan-9", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-2");
        sent()!.TradePlanId.Should().Be("plan-9");
    }

    [Fact]
    public async Task PreviewPlanReview_SetsUserId_FromTheCaller_AndPlanId()
    {
        McpTestContext.Capture<CampaignReviewDto, PreviewPlanReviewQuery>(_mediator, out var sent, new CampaignReviewDto());
        await PlanInsightTools.PreviewPlanReview("plan-7", _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-3");
        sent()!.PlanId.Should().Be("plan-7");
    }

    [Fact]
    public async Task ListPlansPendingReview_SetsUserId_FromTheCaller()
    {
        McpTestContext.Capture<List<TradePlanDto>, GetExecutedPlansForReviewQuery>(_mediator, out var sent, new());
        await PlanInsightTools.ListPlansPendingReview(_mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-4");
    }

    [Fact]
    public async Task GetPortfolioPnL_SetsUserId_FromTheCaller_AndPortfolioId()
    {
        McpTestContext.Capture<PortfolioPnLDto, GetPortfolioPnLQuery>(_mediator, out var sent, new PortfolioPnLDto());
        await PnLTools.GetPortfolioPnL("p-1", _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-5");
        sent()!.PortfolioId.Should().Be("p-1");
    }

    [Fact]
    public async Task GetPositionPnL_SetsUserId_FromTheCaller_AndNormalizesSymbol()
    {
        McpTestContext.Capture<PositionPnL, GetPositionPnLQuery>(_mediator, out var sent, new PositionPnL());
        await PnLTools.GetPositionPnL("p-1", " hpg ", _mediator.Object, McpTestContext.WithUser("u-6"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-6");
        sent()!.PortfolioId.Should().Be("p-1");
        sent()!.Symbol.Should().Be("HPG");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetPositionPnL_BlankSymbol_IsRefused(string symbol)
    {
        var act = () => PnLTools.GetPositionPnL("p-1", symbol, _mediator.Object, McpTestContext.WithUser("u-6"), CancellationToken.None);
        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("symbol");
        _mediator.Verify(m => m.Send(It.IsAny<GetPositionPnLQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPnLSummary_SetsUserId_FromTheCaller()
    {
        McpTestContext.Capture<OverallPnLDto, GetOverallPnLQuery>(_mediator, out var sent, new OverallPnLDto());
        await PnLTools.GetPnLSummary(_mediator.Object, McpTestContext.WithUser("u-7"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-7");
    }

    [Fact]
    public async Task ListAlertRules_SetsUserId_FromTheCaller()
    {
        McpTestContext.Capture<IEnumerable<AlertRuleDto>, GetAlertRulesQuery>(_mediator, out var sent, new List<AlertRuleDto>());
        await AlertTools.ListAlertRules(_mediator.Object, McpTestContext.WithUser("u-8"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-8");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GetAlertHistory_SetsUserId_FromTheCaller_AndPassesUnreadOnly(bool? unreadOnly, bool expected)
    {
        McpTestContext.Capture<AlertHistoryResult, GetAlertHistoryQuery>(_mediator, out var sent, new AlertHistoryResult());
        await AlertTools.GetAlertHistory(_mediator.Object, McpTestContext.WithUser("u-9"), CancellationToken.None, unreadOnly);
        sent()!.UserId.Should().Be("u-9");
        sent()!.UnreadOnly.Should().Be(expected);
    }

    // ---------- get_scenario_suggestion ----------

    private Task<ScenarioSuggestionDto> Suggest(string symbol, decimal price, string? horizon = null)
        => PlanInsightTools.GetScenarioSuggestion(symbol, price, _mediator.Object, McpTestContext.WithUser("u-10"),
            CancellationToken.None, horizon);

    [Fact]
    public async Task GetScenarioSuggestion_SetsUserId_NormalizesSymbol_AndDefaultsToMediumTerm()
    {
        McpTestContext.Capture<ScenarioSuggestionDto, GetScenarioSuggestionQuery>(_mediator, out var sent, new ScenarioSuggestionDto());
        await Suggest(" fpt ", 100_000m);
        sent()!.UserId.Should().Be("u-10");
        sent()!.Symbol.Should().Be("FPT");
        sent()!.EntryPrice.Should().Be(100_000m);
        sent()!.TimeHorizon.Should().Be(TimeHorizon.MediumTerm);
    }

    [Theory]
    [InlineData("ShortTerm", TimeHorizon.ShortTerm)]
    [InlineData("longterm", TimeHorizon.LongTerm)]
    [InlineData(" MediumTerm ", TimeHorizon.MediumTerm)]
    public async Task GetScenarioSuggestion_AcceptsTheThreeHorizonNames_CaseInsensitively(string horizon, TimeHorizon expected)
    {
        McpTestContext.Capture<ScenarioSuggestionDto, GetScenarioSuggestionQuery>(_mediator, out var sent, new ScenarioSuggestionDto());
        await Suggest("FPT", 1m, horizon);
        sent()!.TimeHorizon.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABCDEFGHIJK")]            // 11 ký tự
    public async Task GetScenarioSuggestion_BadSymbol_IsRefused_AndNothingIsSent(string symbol)
    {
        var act = () => Suggest(symbol, 100m);
        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("symbol");
        _mediator.Verify(m => m.Send(It.IsAny<GetScenarioSuggestionQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetScenarioSuggestion_TenCharSymbol_IsAccepted()
    {
        McpTestContext.Capture<ScenarioSuggestionDto, GetScenarioSuggestionQuery>(_mediator, out var sent, new ScenarioSuggestionDto());
        await Suggest("ABCDEFGHIJ", 100m);
        sent()!.Symbol.Should().Be("ABCDEFGHIJ");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetScenarioSuggestion_NonPositiveEntryPrice_IsRefused(int price)
    {
        var act = () => Suggest("FPT", price);
        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("entryPrice");
        _mediator.Verify(m => m.Send(It.IsAny<GetScenarioSuggestionQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("1")]                      // số của enum không phải tên hợp lệ
    [InlineData("99")]
    [InlineData("Daily")]
    [InlineData("Short,Long")]
    public async Task GetScenarioSuggestion_UnknownHorizon_IsRefused(string horizon)
    {
        var act = () => Suggest("FPT", 100m, horizon);
        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("timeHorizon");
        _mediator.Verify(m => m.Send(It.IsAny<GetScenarioSuggestionQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
