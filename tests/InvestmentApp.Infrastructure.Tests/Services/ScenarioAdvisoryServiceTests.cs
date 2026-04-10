using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class ScenarioAdvisoryServiceTests
{
    private readonly Mock<ITradePlanRepository> _tradePlanRepo;
    private readonly Mock<IMarketDataProvider> _marketDataProvider;
    private readonly ScenarioAdvisoryService _sut;

    public ScenarioAdvisoryServiceTests()
    {
        _tradePlanRepo = new Mock<ITradePlanRepository>();
        _marketDataProvider = new Mock<IMarketDataProvider>();
        _sut = new ScenarioAdvisoryService(_tradePlanRepo.Object, _marketDataProvider.Object);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static TradePlan CreateAdvancedInProgressPlan(
        string userId,
        string symbol,
        decimal entryPrice,
        List<ScenarioNode> nodes)
    {
        var plan = new TradePlan(userId, symbol, "Buy", entryPrice, entryPrice * 0.9m, entryPrice * 1.2m, 100);
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(nodes);
        plan.MarkReady();
        plan.MarkInProgress();
        return plan;
    }

    private static StockPriceData MakePrice(string symbol, decimal close) => new StockPriceData
    {
        Symbol = symbol,
        Date = DateTime.UtcNow,
        Open = close,
        High = close,
        Low = close,
        Close = close,
        Volume = 1_000_000
    };

    // =========================================================================
    // Test 1: PriceAbove condition met → returns advisory with Vietnamese message
    // =========================================================================

    [Fact]
    public async Task GetAdvisoriesAsync_PriceAboveConditionMet_ReturnsAdvisoryWithVietnameseMessage()
    {
        // Arrange
        const string userId = "user-1";
        const string symbol = "HPG";
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "node-1",
                ParentId = null,
                Order = 0,
                Label = "Chốt lời 30%",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 80_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m,
                Status = ScenarioNodeStatus.Pending
            }
        };
        var plan = CreateAdvancedInProgressPlan(userId, symbol, 70_000m, nodes);

        _tradePlanRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });

        _marketDataProvider.Setup(m => m.GetCurrentPriceAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePrice(symbol, 82_500m)); // price is above 80,000

        // Act
        var advisories = await _sut.GetAdvisoriesAsync(userId);

        // Assert
        advisories.Should().ContainSingle();
        var advisory = advisories[0];
        advisory.TradePlanId.Should().Be(plan.Id);
        advisory.Symbol.Should().Be(symbol);
        advisory.CurrentPrice.Should().Be(82_500m);
        advisory.NodeId.Should().Be("node-1");
        advisory.NodeLabel.Should().Be("Chốt lời 30%");
        advisory.ConditionDescription.Should().Be("Giá ≥ 80,000");
        advisory.ActionDescription.Should().Be("xem xét bán 30%");
        advisory.Message.Should().Be("HPG đang ở 82,500 (vùng ≥ 80,000) — xem xét bán 30%");
    }

    // =========================================================================
    // Test 2: PriceBelow condition met → advisory for stop loss scenario
    // =========================================================================

    [Fact]
    public async Task GetAdvisoriesAsync_PriceBelowConditionMet_ReturnsAdvisoryForStopLoss()
    {
        // Arrange
        const string userId = "user-2";
        const string symbol = "VNM";
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "node-sl",
                ParentId = null,
                Order = 0,
                Label = "Cắt lỗ toàn bộ",
                ConditionType = ScenarioConditionType.PriceBelow,
                ConditionValue = 75_000m,
                ActionType = ScenarioActionType.SellAll,
                Status = ScenarioNodeStatus.Pending
            }
        };
        var plan = CreateAdvancedInProgressPlan(userId, symbol, 80_000m, nodes);

        _tradePlanRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });

        _marketDataProvider.Setup(m => m.GetCurrentPriceAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePrice(symbol, 74_000m)); // below 75,000

        // Act
        var advisories = await _sut.GetAdvisoriesAsync(userId);

        // Assert
        advisories.Should().ContainSingle();
        var advisory = advisories[0];
        advisory.Symbol.Should().Be(symbol);
        advisory.CurrentPrice.Should().Be(74_000m);
        advisory.ConditionDescription.Should().Be("Giá ≤ 75,000");
        advisory.ActionDescription.Should().Be("xem xét bán toàn bộ");
        advisory.Message.Should().Be("VNM đang ở 74,000 (vùng ≤ 75,000) — xem xét bán toàn bộ");
    }

    // =========================================================================
    // Test 3: Price outside all zones → returns empty advisory list
    // =========================================================================

    [Fact]
    public async Task GetAdvisoriesAsync_PriceOutsideAllZones_ReturnsEmpty()
    {
        // Arrange
        const string userId = "user-3";
        const string symbol = "TCB";
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "node-tp",
                ParentId = null,
                Order = 0,
                Label = "Chốt lời",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 50_000m, // not met: price is 45,000
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 50m,
                Status = ScenarioNodeStatus.Pending
            },
            new()
            {
                NodeId = "node-sl",
                ParentId = null,
                Order = 1,
                Label = "Cắt lỗ",
                ConditionType = ScenarioConditionType.PriceBelow,
                ConditionValue = 38_000m, // not met: price is 45,000
                ActionType = ScenarioActionType.SellAll,
                Status = ScenarioNodeStatus.Pending
            }
        };
        var plan = CreateAdvancedInProgressPlan(userId, symbol, 45_000m, nodes);

        _tradePlanRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });

        _marketDataProvider.Setup(m => m.GetCurrentPriceAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePrice(symbol, 45_000m)); // neither above 50,000 nor below 38,000

        // Act
        var advisories = await _sut.GetAdvisoriesAsync(userId);

        // Assert
        advisories.Should().BeEmpty();
    }

    // =========================================================================
    // Test 4: Only evaluates Pending nodes (not Triggered/Skipped)
    // =========================================================================

    [Fact]
    public async Task GetAdvisoriesAsync_SkipsNonPendingNodes_OnlyEvaluatesPending()
    {
        // Arrange
        const string userId = "user-4";
        const string symbol = "FPT";
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "node-already-triggered",
                ParentId = null,
                Order = 0,
                Label = "Đã chốt lời rồi",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 80_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 30m,
                Status = ScenarioNodeStatus.Triggered, // already triggered
                TriggeredAt = DateTime.UtcNow.AddHours(-2)
            },
            new()
            {
                NodeId = "node-skipped",
                ParentId = null,
                Order = 1,
                Label = "Đã bỏ qua",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 85_000m,
                ActionType = ScenarioActionType.SellPercent,
                ActionValue = 20m,
                Status = ScenarioNodeStatus.Skipped // skipped
            },
            new()
            {
                NodeId = "node-pending",
                ParentId = null,
                Order = 2,
                Label = "Mục tiêu cuối",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 90_000m,
                ActionType = ScenarioActionType.SellAll,
                Status = ScenarioNodeStatus.Pending // this one is pending but not met (price is 95k above 90k)
            }
        };
        var plan = CreateAdvancedInProgressPlan(userId, symbol, 75_000m, nodes);

        _tradePlanRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });

        _marketDataProvider.Setup(m => m.GetCurrentPriceAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePrice(symbol, 95_000m)); // above 90,000 — only the pending node should be checked

        // Act
        var advisories = await _sut.GetAdvisoriesAsync(userId);

        // Assert
        advisories.Should().ContainSingle();
        advisories[0].NodeId.Should().Be("node-pending");
        // Triggered and Skipped nodes must not appear
        advisories.Should().NotContain(a => a.NodeId == "node-already-triggered");
        advisories.Should().NotContain(a => a.NodeId == "node-skipped");
    }

    // =========================================================================
    // Test 5: Advisory message wording uses "Xem xét...", never "Đã..."
    // =========================================================================

    [Fact]
    public async Task GetAdvisoriesAsync_MessageWording_UsesAdvisoryLanguageNotConfirmation()
    {
        // Arrange
        const string userId = "user-5";
        const string symbol = "MWG";
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "node-add",
                ParentId = null,
                Order = 0,
                Label = "Mua thêm vùng hỗ trợ",
                ConditionType = ScenarioConditionType.PriceBelow,
                ConditionValue = 60_000m,
                ActionType = ScenarioActionType.AddPosition,
                ActionValue = 25m,
                Status = ScenarioNodeStatus.Pending
            }
        };
        var plan = CreateAdvancedInProgressPlan(userId, symbol, 65_000m, nodes);

        _tradePlanRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });

        _marketDataProvider.Setup(m => m.GetCurrentPriceAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePrice(symbol, 58_000m)); // below 60,000

        // Act
        var advisories = await _sut.GetAdvisoriesAsync(userId);

        // Assert
        advisories.Should().ContainSingle();
        var advisory = advisories[0];

        // Message must use advisory wording ("xem xét" / "cân nhắc"), NOT confirmation ("Đã", "Cần phải")
        advisory.Message.Should().NotContain("Đã");
        advisory.Message.Should().NotContain("Cần phải");
        advisory.ActionDescription.Should().Contain("xem xét");
        advisory.Message.Should().Contain("xem xét");
        advisory.Message.Should().Be("MWG đang ở 58,000 (vùng ≤ 60,000) — xem xét mua thêm 25%");
    }

    // =========================================================================
    // Test 6: No plans → empty list
    // =========================================================================

    [Fact]
    public async Task GetAdvisoriesAsync_NoPlans_ReturnsEmpty()
    {
        // Arrange
        const string userId = "user-empty";

        _tradePlanRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan>());

        // Act
        var advisories = await _sut.GetAdvisoriesAsync(userId);

        // Assert
        advisories.Should().BeEmpty();
    }

    // =========================================================================
    // Test 7: SendNotification action → skipped (no advisory)
    // =========================================================================

    [Fact]
    public async Task GetAdvisoriesAsync_SendNotificationAction_SkipsAdvisory()
    {
        // Arrange
        const string userId = "user-notif";
        const string symbol = "VIC";
        var nodes = new List<ScenarioNode>
        {
            new()
            {
                NodeId = "node-notif",
                ParentId = null,
                Order = 0,
                Label = "Thông báo vùng kháng cự",
                ConditionType = ScenarioConditionType.PriceAbove,
                ConditionValue = 55_000m,
                ActionType = ScenarioActionType.SendNotification,
                Status = ScenarioNodeStatus.Pending
            }
        };
        var plan = CreateAdvancedInProgressPlan(userId, symbol, 50_000m, nodes);

        _tradePlanRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });

        _marketDataProvider.Setup(m => m.GetCurrentPriceAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePrice(symbol, 57_000m)); // above 55,000

        // Act
        var advisories = await _sut.GetAdvisoriesAsync(userId);

        // Assert — SendNotification produces no advisory
        advisories.Should().BeEmpty();
    }
}
