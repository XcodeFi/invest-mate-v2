using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class ScenarioEvaluationServiceTests
{
    private readonly Mock<ITradePlanRepository> _tradePlanRepo;
    private readonly Mock<IStockPriceRepository> _stockPriceRepo;
    private readonly Mock<IAlertHistoryRepository> _alertHistoryRepo;
    private readonly Mock<ITechnicalIndicatorService> _technicalIndicatorService;
    private readonly Mock<ILogger<ScenarioEvaluationService>> _logger;
    private readonly ScenarioEvaluationService _sut;

    public ScenarioEvaluationServiceTests()
    {
        _tradePlanRepo = new Mock<ITradePlanRepository>();
        _stockPriceRepo = new Mock<IStockPriceRepository>();
        _alertHistoryRepo = new Mock<IAlertHistoryRepository>();
        _technicalIndicatorService = new Mock<ITechnicalIndicatorService>();
        _logger = new Mock<ILogger<ScenarioEvaluationService>>();
        _sut = new ScenarioEvaluationService(
            _tradePlanRepo.Object,
            _stockPriceRepo.Object,
            _alertHistoryRepo.Object,
            _technicalIndicatorService.Object,
            _logger.Object);
    }

    private static TradePlan CreateInProgressPlanWithScenarios(
        string symbol = "VNM",
        decimal entryPrice = 80_000m,
        decimal stopLoss = 75_000m,
        decimal target = 90_000m,
        List<ScenarioNode>? nodes = null)
    {
        var plan = new TradePlan("user-1", symbol, "Buy", entryPrice, stopLoss, target, 100);
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(nodes ?? new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "Chốt lời",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m
            },
            new()
            {
                NodeId = "root-2",
                ParentId = null,
                Order = 1,
                Label = "Cắt lỗ",
                ConditionType = ScenarioConditionType.PriceBelow,
                ConditionValue = 75_000m,
                ActionType = ScenarioActionType.SellAll
            }
        });
        // Move to InProgress
        plan.MarkReady();
        plan.MarkInProgress();
        return plan;
    }

    private static StockPrice CreateStockPrice(string symbol, decimal close)
    {
        return new StockPrice(symbol, DateTime.UtcNow, close, close, close, close, 1000, "Test");
    }

    private void SetupPlansAndPrices(TradePlan plan, decimal currentPrice)
    {
        _tradePlanRepo.Setup(r => r.GetAdvancedInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });
        _stockPriceRepo.Setup(r => r.GetLatestPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockPrice> { CreateStockPrice(plan.Symbol, currentPrice) });
    }

    // =====================================================================
    // PriceAbove
    // =====================================================================

    [Fact]
    public async Task PriceAbove_WhenMet_ShouldTriggerNode()
    {
        var plan = CreateInProgressPlanWithScenarios();
        SetupPlansAndPrices(plan, 86_000m); // above 85,000

        var results = await _sut.EvaluateAllAsync();

        results.Should().ContainSingle();
        results[0].NodeId.Should().Be("root-1");
        results[0].ActionType.Should().Be("SellPercent");
        plan.ScenarioNodes![0].Status.Should().Be(ScenarioNodeStatus.Triggered);
    }

    [Fact]
    public async Task PriceAbove_WhenNotMet_ShouldNotTrigger()
    {
        var plan = CreateInProgressPlanWithScenarios();
        SetupPlansAndPrices(plan, 83_000m); // below 85,000

        var results = await _sut.EvaluateAllAsync();

        results.Should().BeEmpty();
        plan.ScenarioNodes![0].Status.Should().Be(ScenarioNodeStatus.Pending);
    }

    // =====================================================================
    // PriceBelow
    // =====================================================================

    [Fact]
    public async Task PriceBelow_WhenMet_ShouldTriggerNode()
    {
        var plan = CreateInProgressPlanWithScenarios();
        SetupPlansAndPrices(plan, 74_000m); // below 75,000

        var results = await _sut.EvaluateAllAsync();

        results.Should().ContainSingle();
        results[0].NodeId.Should().Be("root-2");
        results[0].ActionType.Should().Be("SellAll");
    }

    // =====================================================================
    // PricePercentChange
    // =====================================================================

    [Fact]
    public async Task PricePercentChange_Positive_ShouldTrigger()
    {
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "Tăng 10%",
                ConditionType = ScenarioConditionType.PricePercentChange,
                ConditionValue = 10m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m
            }
        };
        var plan = CreateInProgressPlanWithScenarios(entryPrice: 100_000m, nodes: nodes);
        SetupPlansAndPrices(plan, 112_000m); // +12%

        var results = await _sut.EvaluateAllAsync();

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task PricePercentChange_Negative_ShouldTrigger()
    {
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "Giảm 5%",
                ConditionType = ScenarioConditionType.PricePercentChange,
                ConditionValue = -5m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 50m
            }
        };
        var plan = CreateInProgressPlanWithScenarios(entryPrice: 100_000m, nodes: nodes);
        SetupPlansAndPrices(plan, 94_000m); // -6%

        var results = await _sut.EvaluateAllAsync();

        results.Should().ContainSingle();
    }

    // =====================================================================
    // Parent not triggered → child skipped
    // =====================================================================

    [Fact]
    public async Task ChildNode_ParentNotTriggered_ShouldNotEvaluate()
    {
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "Root",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 90_000m, // not met
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m
            },
            new()
            {
                NodeId = "child-1",
                ParentId = "root-1",
                Order = 0,
                Label = "Child",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 80_000m, // would be met if evaluated
                ActionType = ScenarioActionType.MoveStopToBreakeven
            }
        };
        var plan = CreateInProgressPlanWithScenarios(nodes: nodes);
        SetupPlansAndPrices(plan, 85_000m); // root not met (90k), child would be met (80k)

        var results = await _sut.EvaluateAllAsync();

        results.Should().BeEmpty();
    }

    // =====================================================================
    // Creates AlertHistory
    // =====================================================================

    [Fact]
    public async Task Trigger_ShouldCreateAlertHistory()
    {
        var plan = CreateInProgressPlanWithScenarios();
        SetupPlansAndPrices(plan, 86_000m);

        await _sut.EvaluateAllAsync();

        _alertHistoryRepo.Verify(r => r.AddAsync(
            It.Is<AlertHistory>(a => a.AlertType == "ScenarioPlaybook"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // =====================================================================
    // TrailingStop updates highest price
    // =====================================================================

    [Fact]
    public async Task TrailingStop_ShouldUpdateHighestPrice()
    {
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "Trailing",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.ActivateTrailingStop,
                TrailingStopConfig = new TrailingStopConfig
                {
                    Method = TrailingStopMethod.Percentage,
                    TrailValue = 5m
                }
            },
            new()
            {
                NodeId = "child-1",
                ParentId = "root-1",
                Order = 0,
                Label = "Trailing hit",
                ConditionType = ScenarioConditionType.TrailingStopHit,
                ActionType = ScenarioActionType.SellAll
            }
        };
        var plan = CreateInProgressPlanWithScenarios(nodes: nodes);
        SetupPlansAndPrices(plan, 90_000m); // triggers root, then updates trailing

        var results = await _sut.EvaluateAllAsync();

        // Root should be triggered
        results.Should().ContainSingle(r => r.NodeId == "root-1");
        var config = plan.ScenarioNodes!.First(n => n.NodeId == "root-1").TrailingStopConfig!;
        config.HighestPrice.Should().Be(90_000m);
        config.CurrentTrailingStop.Should().Be(85_500m); // 90,000 * 0.95
    }

    // =====================================================================
    // No plans → empty results
    // =====================================================================

    [Fact]
    public async Task NoPlan_ShouldReturnEmpty()
    {
        _tradePlanRepo.Setup(r => r.GetAdvancedInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan>());

        var results = await _sut.EvaluateAllAsync();

        results.Should().BeEmpty();
    }

    // =====================================================================
    // Simple mode plans should be skipped
    // =====================================================================

    [Fact]
    public async Task SimpleModesPlan_ShouldBeSkipped()
    {
        var plan = new TradePlan("user-1", "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100);
        // Default Simple mode, no scenario nodes
        plan.MarkReady();
        plan.MarkInProgress();
        _tradePlanRepo.Setup(r => r.GetAdvancedInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });

        var results = await _sut.EvaluateAllAsync();

        results.Should().BeEmpty();
    }

    // =====================================================================
    // ATR Trailing Stop — real ATR(14) instead of placeholder
    // =====================================================================

    [Fact]
    public async Task AtrTrailingStop_WhenAtrAvailable_ShouldUseRealAtr()
    {
        // Arrange: ATR(14) = 1_500, trailValue = 2 (multiplier)
        // Expected trailing stop = highestPrice - trailValue * ATR = 90_000 - 2 * 1_500 = 87_000
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "ATR Trailing",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.ActivateTrailingStop,
                TrailingStopConfig = new TrailingStopConfig
                {
                    Method = TrailingStopMethod.ATR,
                    TrailValue = 2m // ATR multiplier
                }
            },
            new()
            {
                NodeId = "child-1",
                ParentId = "root-1",
                Order = 0,
                Label = "ATR trailing hit",
                ConditionType = ScenarioConditionType.TrailingStopHit,
                ActionType = ScenarioActionType.SellAll
            }
        };
        var plan = CreateInProgressPlanWithScenarios(nodes: nodes);
        SetupPlansAndPrices(plan, 90_000m);

        _technicalIndicatorService
            .Setup(s => s.AnalyzeAsync("VNM", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TechnicalAnalysisResult
            {
                Symbol = "VNM",
                Atr14 = 1_500m
            });

        // Act
        var results = await _sut.EvaluateAllAsync();

        // Assert: trailing stop = 90_000 - 2 * 1_500 = 87_000
        results.Should().ContainSingle(r => r.NodeId == "root-1");
        var config = plan.ScenarioNodes!.First(n => n.NodeId == "root-1").TrailingStopConfig!;
        config.HighestPrice.Should().Be(90_000m);
        config.CurrentTrailingStop.Should().Be(87_000m);
    }

    [Fact]
    public async Task AtrTrailingStop_WhenAtrNull_ShouldFallbackToProxy()
    {
        // Arrange: ATR(14) is null (no data), should fallback to entryPrice * 0.02 * trailValue
        // Expected trailing stop = highestPrice - trailValue * (entryPrice * 0.02) = 90_000 - 2 * (80_000 * 0.02) = 90_000 - 3_200 = 86_800
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "ATR Trailing fallback",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.ActivateTrailingStop,
                TrailingStopConfig = new TrailingStopConfig
                {
                    Method = TrailingStopMethod.ATR,
                    TrailValue = 2m
                }
            },
            new()
            {
                NodeId = "child-1",
                ParentId = "root-1",
                Order = 0,
                Label = "ATR trailing hit",
                ConditionType = ScenarioConditionType.TrailingStopHit,
                ActionType = ScenarioActionType.SellAll
            }
        };
        var plan = CreateInProgressPlanWithScenarios(nodes: nodes);
        SetupPlansAndPrices(plan, 90_000m);

        _technicalIndicatorService
            .Setup(s => s.AnalyzeAsync("VNM", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TechnicalAnalysisResult
            {
                Symbol = "VNM",
                Atr14 = null // no data
            });

        // Act
        var results = await _sut.EvaluateAllAsync();

        // Assert: fallback = 90_000 - 2 * (80_000 * 0.02) = 86_800
        results.Should().ContainSingle(r => r.NodeId == "root-1");
        var config = plan.ScenarioNodes!.First(n => n.NodeId == "root-1").TrailingStopConfig!;
        config.HighestPrice.Should().Be(90_000m);
        config.CurrentTrailingStop.Should().Be(86_800m);
    }

    [Fact]
    public async Task AtrTrailingStop_WhenPriceUpdates_ShouldRecalculateWithRealAtr()
    {
        // Arrange: Simulate node already triggered with highestPrice and then a higher price comes in
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "root-1",
                ParentId = null,
                Order = 0,
                Label = "ATR Trailing update",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.ActivateTrailingStop,
                TrailingStopConfig = new TrailingStopConfig
                {
                    Method = TrailingStopMethod.ATR,
                    TrailValue = 2m,
                    HighestPrice = 90_000m, // previously set
                    CurrentTrailingStop = 87_000m // previously computed
                },
                Status = ScenarioNodeStatus.Triggered,
                TriggeredAt = DateTime.UtcNow.AddHours(-1)
            },
            new()
            {
                NodeId = "child-1",
                ParentId = "root-1",
                Order = 0,
                Label = "ATR trailing hit",
                ConditionType = ScenarioConditionType.TrailingStopHit,
                ActionType = ScenarioActionType.SellAll
            }
        };
        var plan = CreateInProgressPlanWithScenarios(nodes: nodes);
        // New price is higher than previous highestPrice
        SetupPlansAndPrices(plan, 95_000m);

        _technicalIndicatorService
            .Setup(s => s.AnalyzeAsync("VNM", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TechnicalAnalysisResult
            {
                Symbol = "VNM",
                Atr14 = 1_500m
            });

        // Act
        var results = await _sut.EvaluateAllAsync();

        // Assert: trailing stop = 95_000 - 2 * 1_500 = 92_000
        var config = plan.ScenarioNodes!.First(n => n.NodeId == "root-1").TrailingStopConfig!;
        config.HighestPrice.Should().Be(95_000m);
        config.CurrentTrailingStop.Should().Be(92_000m);
    }
}
