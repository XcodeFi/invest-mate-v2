using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Analytics.Queries.GetEquityCurve;
using InvestmentApp.Application.Analytics.Queries.GetMonthlyReturns;
using InvestmentApp.Application.Analytics.Queries.GetPerformance;
using InvestmentApp.Application.Analytics.Queries.GetSavingsComparison;
using InvestmentApp.Application.CapitalFlows.Queries.GetAdjustedReturn;
using InvestmentApp.Application.CapitalFlows.Queries.GetFlowHistory;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Application.PersonalFinance.Queries.GetNetWorthSummary;
using InvestmentApp.Application.TradePlans.Queries.GetCampaignAnalytics;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class AnalyticsToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetPerformance_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<PerformanceSummary, GetPerformanceQuery>(
            _mediator, out var sent, new PerformanceSummary());
        await AnalyticsTools.GetPerformance("p1", _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.PortfolioId.Should().Be("p1");
    }

    [Fact]
    public async Task GetEquityCurve_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<EquityCurveData, GetEquityCurveQuery>(
            _mediator, out var sent, new EquityCurveData());
        await AnalyticsTools.GetEquityCurve("p2", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-2");
        sent()!.PortfolioId.Should().Be("p2");
    }

    [Fact]
    public async Task GetMonthlyReturns_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<MonthlyReturnsData, GetMonthlyReturnsQuery>(
            _mediator, out var sent, new MonthlyReturnsData());
        await AnalyticsTools.GetMonthlyReturns("p3", _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-3");
        sent()!.PortfolioId.Should().Be("p3");
    }

    [Fact]
    public async Task GetSavingsComparison_SetsUserId_PortfolioId_AndOptionalParams()
    {
        var asOf = new DateTime(2026, 07, 01, 0, 0, 0, DateTimeKind.Utc);
        McpTestContext.Capture<SavingsComparisonDto, GetSavingsComparisonQuery>(
            _mediator, out var sent, new SavingsComparisonDto());
        // AnnualRate là phân số thập phân (0.065 = 6.5%/năm) — handler sanity range −10%..50%.
        await AnalyticsTools.GetSavingsComparison("p4", _mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None, 0.065m, asOf);
        sent()!.UserId.Should().Be("u-4");
        sent()!.PortfolioId.Should().Be("p4");
        sent()!.AnnualRate.Should().Be(0.065m);
        sent()!.AsOf.Should().Be(asOf);
    }

    [Fact]
    public async Task GetSavingsComparison_OmittedOptionals_StayNull()
    {
        McpTestContext.Capture<SavingsComparisonDto, GetSavingsComparisonQuery>(
            _mediator, out var sent, new SavingsComparisonDto());
        await AnalyticsTools.GetSavingsComparison("p5", _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);
        sent()!.AnnualRate.Should().BeNull();
        sent()!.AsOf.Should().BeNull();
    }

    [Fact]
    public async Task GetCampaignAnalytics_SetsUserId_AndTimeHorizon()
    {
        McpTestContext.Capture<CampaignAnalyticsDto, GetCampaignAnalyticsQuery>(
            _mediator, out var sent, new CampaignAnalyticsDto());
        await AnalyticsTools.GetCampaignAnalytics(_mediator.Object, McpTestContext.WithUser("u-6"), CancellationToken.None, "ShortTerm");
        sent()!.UserId.Should().Be("u-6");
        sent()!.TimeHorizon.Should().Be("ShortTerm");
    }

    [Fact]
    public async Task GetNetWorthSummary_SetsUserId()
    {
        McpTestContext.Capture<NetWorthSummaryDto, GetNetWorthSummaryQuery>(
            _mediator, out var sent, new NetWorthSummaryDto());
        await AnalyticsTools.GetNetWorthSummary(_mediator.Object, McpTestContext.WithUser("u-7"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-7");
    }

    [Fact]
    public async Task GetFlowHistory_SetsUserId_PortfolioId_AndDateRange()
    {
        var from = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 06, 30, 0, 0, 0, DateTimeKind.Utc);
        McpTestContext.Capture<CapitalFlowHistoryDto, GetFlowHistoryQuery>(
            _mediator, out var sent, new CapitalFlowHistoryDto());
        await AnalyticsTools.GetFlowHistory("p6", _mediator.Object, McpTestContext.WithUser("u-8"), CancellationToken.None, from, to);
        sent()!.UserId.Should().Be("u-8");
        sent()!.PortfolioId.Should().Be("p6");
        sent()!.From.Should().Be(from);
        sent()!.To.Should().Be(to);
    }

    [Fact]
    public async Task GetAdjustedReturn_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<AdjustedReturnDto, GetAdjustedReturnQuery>(
            _mediator, out var sent, new AdjustedReturnDto());
        await AnalyticsTools.GetAdjustedReturn("p7", _mediator.Object, McpTestContext.WithUser("u-9"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-9");
        sent()!.PortfolioId.Should().Be("p7");
    }
}
