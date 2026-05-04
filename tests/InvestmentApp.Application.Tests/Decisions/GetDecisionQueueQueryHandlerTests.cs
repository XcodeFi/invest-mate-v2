using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Decisions.DTOs;
using InvestmentApp.Application.Decisions.Queries.GetDecisionQueue;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetPendingThesisReviews;
using InvestmentApp.Domain.Entities;
using MediatR;
using Moq;

namespace InvestmentApp.Application.Tests.Decisions;

/// <summary>
/// Tests P3 Decision Queue aggregator — gộp 3 nguồn (StopLossHit / ScenarioTrigger / ThesisReviewDue)
/// thành 1 list duy nhất với dedupe + sort theo severity.
/// </summary>
public class GetDecisionQueueQueryHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<ITradePlanRepository> _planRepo = new();
    private readonly Mock<IRiskCalculationService> _riskService = new();
    private readonly Mock<IScenarioAdvisoryService> _advisoryService = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly GetDecisionQueueQueryHandler _handler;
    private const string UserId = "user-1";

    public GetDecisionQueueQueryHandlerTests()
    {
        _handler = new GetDecisionQueueQueryHandler(
            _portfolioRepo.Object, _planRepo.Object, _riskService.Object, _advisoryService.Object, _mediator.Object);

        // Defaults: tất cả nguồn empty — từng test override khi cần.
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Portfolio>());
        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradePlan>());
        _advisoryService.Setup(s => s.GetAdvisoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScenarioAdvisory>());
        _mediator.Setup(m => m.Send(It.IsAny<GetPendingThesisReviewsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingThesisReviewDto>());
    }

    // ---------------------------------------------------------------
    // Empty state
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_AllSourcesEmpty_ReturnsEmptyQueue()
    {
        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // Source 1: stop-loss
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_PositionAtStopLoss_AddsCriticalStopLossItem()
    {
        var portfolio = MakePortfolio("p1", "Main");
        SetupPortfolios(portfolio);
        SetupRiskSummary(portfolio.Id, MakePosition("FPT", stopLossPrice: 89.5m, currentPrice: 89.4m, distanceToSlPercent: -0.1m));

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        var item = result.Items[0];
        item.Type.Should().Be(DecisionType.StopLossHit);
        item.Severity.Should().Be(DecisionSeverity.Critical);
        item.Symbol.Should().Be("FPT");
        item.PortfolioId.Should().Be("p1");
        item.PortfolioName.Should().Be("Main");
        item.Headline.Should().Contain("thủng SL");
    }

    [Fact]
    public async Task Handle_PositionWithin2PercentOfSL_AddsWarningStopLossItem()
    {
        var portfolio = MakePortfolio("p1", "Main");
        SetupPortfolios(portfolio);
        SetupRiskSummary(portfolio.Id, MakePosition("VNM", stopLossPrice: 80m, currentPrice: 81m, distanceToSlPercent: 1.5m));

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Severity.Should().Be(DecisionSeverity.Warning);
        result.Items[0].Headline.Should().Contain("cách SL");
    }

    [Fact]
    public async Task Handle_PositionFarFromSL_NotIncluded()
    {
        var portfolio = MakePortfolio("p1", "Main");
        SetupPortfolios(portfolio);
        SetupRiskSummary(portfolio.Id, MakePosition("HPG", stopLossPrice: 25m, currentPrice: 30m, distanceToSlPercent: 16m));

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PositionWithoutSL_NotIncluded()
    {
        var portfolio = MakePortfolio("p1", "Main");
        SetupPortfolios(portfolio);
        SetupRiskSummary(portfolio.Id, MakePosition("MWG", stopLossPrice: null, currentPrice: 50m, distanceToSlPercent: 0m));

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Source 2: scenario advisories
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_ScenarioAdvisoryActive_AddsWarningItem()
    {
        var portfolio = MakePortfolio("p1", "Main");
        SetupPortfolios(portfolio);
        _advisoryService.Setup(s => s.GetAdvisoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScenarioAdvisory>
            {
                new()
                {
                    TradePlanId = "plan-1",
                    Symbol = "HPG",
                    CurrentPrice = 82_500m,
                    NodeId = "node-1",
                    NodeLabel = "Vùng chốt lời 1",
                    ConditionDescription = "Giá ≥ 80,000",
                    ActionDescription = "xem xét bán 30%",
                    Message = "HPG đang ở 82,500 (vùng ≥ 80,000) — xem xét bán 30%"
                }
            });

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Type.Should().Be(DecisionType.ScenarioTrigger);
        result.Items[0].Severity.Should().Be(DecisionSeverity.Warning);
        result.Items[0].Symbol.Should().Be("HPG");
        result.Items[0].TradePlanId.Should().Be("plan-1");
        result.Items[0].Headline.Should().Contain("82,500");
    }

    // ---------------------------------------------------------------
    // Source 3: pending thesis reviews
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_ThesisReviewOverdue3Days_AddsCriticalItem()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPendingThesisReviewsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingThesisReviewDto>
            {
                new()
                {
                    PlanId = "plan-2",
                    Symbol = "VNM",
                    DaysOverdue = 5,
                    Thesis = "EPS Q1 +20%",
                    Reasons = new List<PendingReviewReason>
                    {
                        new() { Kind = "PeriodicReview", Detail = "Review định kỳ", DueDate = DateTime.UtcNow.AddDays(-5), DaysOverdue = 5 }
                    }
                }
            });

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Type.Should().Be(DecisionType.ThesisReviewDue);
        result.Items[0].Severity.Should().Be(DecisionSeverity.Critical);
        result.Items[0].TradePlanId.Should().Be("plan-2");
    }

    [Fact]
    public async Task Handle_ThesisReviewOverdue1Day_AddsWarningItem()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPendingThesisReviewsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingThesisReviewDto>
            {
                new()
                {
                    PlanId = "plan-3",
                    Symbol = "FPT",
                    DaysOverdue = 1,
                    Reasons = new List<PendingReviewReason>
                    {
                        new() { Kind = "PeriodicReview", Detail = "Review", DueDate = DateTime.UtcNow.AddDays(-1), DaysOverdue = 1 }
                    }
                }
            });

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items[0].Severity.Should().Be(DecisionSeverity.Warning);
    }

    // ---------------------------------------------------------------
    // Aggregate + sort
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_AggregatesAllThreeSources_SortedCriticalFirst()
    {
        var portfolio = MakePortfolio("p1", "Main");
        SetupPortfolios(portfolio);
        SetupRiskSummary(portfolio.Id, MakePosition("FPT", 89.5m, 89.4m, -0.1m));  // Critical SL
        _advisoryService.Setup(s => s.GetAdvisoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScenarioAdvisory>
            {
                new() { TradePlanId = "p2", Symbol = "HPG", NodeId = "n1", Message = "msg",
                        ConditionDescription = "cond", ActionDescription = "act", NodeLabel = "lbl", CurrentPrice = 80m }
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetPendingThesisReviewsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingThesisReviewDto>
            {
                new() { PlanId = "p3", Symbol = "VNM", DaysOverdue = 1,
                        Reasons = new() { new() { Kind = "PeriodicReview", Detail = "x", DueDate = DateTime.UtcNow, DaysOverdue = 1 } } }
            });

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().HaveCount(3);
        result.Items[0].Severity.Should().Be(DecisionSeverity.Critical);  // FPT SL
        result.Items[0].Symbol.Should().Be("FPT");
        result.Items.Skip(1).Select(i => i.Severity).Should().AllBeEquivalentTo(DecisionSeverity.Warning);
    }

    // ---------------------------------------------------------------
    // Dedupe
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_SameSymbolHasStopLossAndAdvisory_DedupesKeepingStopLoss()
    {
        var portfolio = MakePortfolio("p1", "Main");
        SetupPortfolios(portfolio);
        SetupRiskSummary(portfolio.Id, MakePosition("FPT", 89.5m, 89.4m, -0.1m));
        // Plan với PortfolioId="p1" để advisory resolve được portfolio thật → dedupe đúng với SL
        SetupPlans(MakePlanForPortfolio("plan-fpt", "FPT", "p1"));
        _advisoryService.Setup(s => s.GetAdvisoriesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScenarioAdvisory>
            {
                new() { TradePlanId = "plan-fpt", Symbol = "FPT", NodeId = "n1", Message = "m",
                        ConditionDescription = "c", ActionDescription = "a", NodeLabel = "lbl", CurrentPrice = 89.4m }
            });

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Type.Should().Be(DecisionType.StopLossHit);
    }

    [Fact]
    public async Task Handle_PositionWithZeroCurrentPrice_NotIncluded()
    {
        // Guard: RiskCalculationService trả DistanceToStopLossPercent=0 khi CurrentPrice<=0
        // (giá fail fetch). Không được render thành Critical "thủng SL".
        var portfolio = MakePortfolio("p1", "Main");
        SetupPortfolios(portfolio);
        SetupRiskSummary(portfolio.Id, MakePosition("FPT", stopLossPrice: 89.5m, currentPrice: 0m, distanceToSlPercent: 0m));

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Multiple portfolios
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_MultiplePortfolios_AggregatesAcrossAll()
    {
        var p1 = MakePortfolio("p1", "Main");
        var p2 = MakePortfolio("p2", "VCB Trading");
        SetupPortfolios(p1, p2);
        SetupRiskSummary(p1.Id, MakePosition("FPT", 89.5m, 89.4m, -0.1m));
        SetupRiskSummary(p2.Id, MakePosition("HPG", 25m, 24.8m, -0.8m));

        var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(i => i.PortfolioName == "Main");
        result.Items.Should().Contain(i => i.PortfolioName == "VCB Trading");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private static Portfolio MakePortfolio(string id, string name)
    {
        var p = new Portfolio(UserId, name, 100_000_000m);
        // Override Id để test deterministic (matches pattern in CashFlowAdjustedReturnServiceTests).
        typeof(Portfolio).GetProperty("Id")!.SetValue(p, id);
        return p;
    }

    private static PositionRiskItem MakePosition(string symbol, decimal? stopLossPrice, decimal currentPrice, decimal distanceToSlPercent)
        => new()
        {
            Symbol = symbol,
            Quantity = 100,
            CurrentPrice = currentPrice,
            MarketValue = currentPrice * 100,
            PositionSizePercent = 10m,
            StopLossPrice = stopLossPrice,
            DistanceToStopLossPercent = distanceToSlPercent
        };

    private void SetupPortfolios(params Portfolio[] portfolios)
    {
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolios);
    }

    private void SetupPlans(params TradePlan[] plans)
    {
        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);
    }

    private static TradePlan MakePlanForPortfolio(string planId, string symbol, string portfolioId)
    {
        var plan = new TradePlan(UserId, symbol, "Buy", 100m, 95m, 120m, 100,
            portfolioId: portfolioId, accountBalance: 100_000_000m);
        typeof(TradePlan).GetProperty("Id")!.SetValue(plan, planId);
        return plan;
    }

    private void SetupRiskSummary(string portfolioId, params PositionRiskItem[] positions)
    {
        _riskService.Setup(s => s.GetPortfolioRiskSummaryAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioRiskSummary
            {
                PortfolioId = portfolioId,
                Positions = positions.ToList(),
                PositionCount = positions.Length
            });
    }
}
