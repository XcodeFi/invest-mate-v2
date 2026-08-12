using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetPortfolioRisk;
using InvestmentApp.Application.Risk.Queries.GetStopLossTargets;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioAdvisories;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class RiskToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetPortfolioRisk_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<PortfolioRiskSummary, GetPortfolioRiskQuery>(
            _mediator, out var sent, new PortfolioRiskSummary());
        await RiskTools.GetPortfolioRisk("p1", _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.PortfolioId.Should().Be("p1");
    }

    [Fact]
    public async Task GetStopLossTargets_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<StopLossTargetsDto, GetStopLossTargetsQuery>(
            _mediator, out var sent, new StopLossTargetsDto());
        await RiskTools.GetStopLossTargets("p2", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-2");
        sent()!.PortfolioId.Should().Be("p2");
    }

    [Fact]
    public async Task GetTrailingStopAlerts_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<TrailingStopAlertsResult, GetTrailingStopAlertsQuery>(
            _mediator, out var sent, new TrailingStopAlertsResult());
        await RiskTools.GetTrailingStopAlerts("p3", _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-3");
        sent()!.PortfolioId.Should().Be("p3");
    }

    [Fact]
    public async Task GetVolatilitySizing_PassesAllFourArgs_AndUserId()
    {
        McpTestContext.Capture<VolatilitySizingResult, GetVolatilitySizingForPlanQuery>(
            _mediator, out var sent, new VolatilitySizingResult { Symbol = "FPT" });

        await RiskTools.GetVolatilitySizing("p5", "FPT", 100_000m, 250,
            _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);

        sent()!.UserId.Should().Be("u-5", "danh tính lấy từ ApiKey, không từ tham số");
        sent()!.PortfolioId.Should().Be("p5");
        sent()!.Symbol.Should().Be("FPT");
        sent()!.EntryPrice.Should().Be(100_000m);
        sent()!.Quantity.Should().Be(250);
    }

    [Fact]
    public async Task GetScenarioAdvisories_SetsUserId()
    {
        McpTestContext.Capture<List<ScenarioAdvisory>, GetScenarioAdvisoriesQuery>(
            _mediator, out var sent, new List<ScenarioAdvisory>());
        await RiskTools.GetScenarioAdvisories(_mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-4");
    }
}
