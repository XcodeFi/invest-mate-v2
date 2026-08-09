using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Kịch bản thoát lệnh chạy qua job nền và tự kích hoạt hành động, nên giá trên kế hoạch
/// phải được quy về cùng mặt bằng với giá thị trường sau ngày GDKHQ.
/// Ví dụ dùng xuyên suốt: HPG cổ tức cổ phiếu 10:13 → giá thị trường chia 1,3.
/// </summary>
public class ScenarioCorporateActionTests
{
    private readonly Mock<ITradePlanRepository> _tradePlanRepo = new();
    private readonly Mock<IStockPriceRepository> _stockPriceRepo = new();
    private readonly Mock<IAlertHistoryRepository> _alertHistoryRepo = new();
    private readonly Mock<ITechnicalIndicatorService> _technicalIndicatorService = new();
    private readonly Mock<ICorporateActionRepository> _corporateActionRepo = new();
    private readonly Mock<ILogger<ScenarioEvaluationService>> _logger = new();
    private readonly ScenarioEvaluationService _sut;

    private static readonly DateTime PlanCreated = DateTime.UtcNow.AddDays(-60);
    private static readonly DateTime ExDate = DateTime.UtcNow.AddDays(-30);

    public ScenarioCorporateActionTests()
    {
        _corporateActionRepo
            .Setup(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CorporateAction>());

        _sut = new ScenarioEvaluationService(
            _tradePlanRepo.Object, _stockPriceRepo.Object, _alertHistoryRepo.Object,
            _technicalIndicatorService.Object, _corporateActionRepo.Object, _logger.Object);
    }

    private static TradePlan PlanWith(params ScenarioNode[] nodes)
    {
        var plan = new TradePlan("user-1", "HPG", "Buy", 30_000m, 27_000m, 39_000m, 100,
            portfolioId: "port-1",
            thesis: "Luận điểm đủ dài để qua được ràng buộc tối thiểu của entity");
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        plan.SetScenarioNodes(nodes.ToList());
        plan.MarkReady();
        plan.MarkInProgress();

        // Kế hoạch lập TRƯỚC ngày GDKHQ — cả hai mốc giá phải nằm trước sự kiện.
        // SetScenarioNodes vừa dời mốc kịch bản về "bây giờ", lùi lại cho đúng bối cảnh test.
        typeof(TradePlan).GetProperty(nameof(TradePlan.PricesSetAt))!.SetValue(plan, PlanCreated);
        typeof(TradePlan).GetProperty(nameof(TradePlan.ScenarioPricesSetAt))!.SetValue(plan, PlanCreated);
        return plan;
    }

    private void SetupPlan(TradePlan plan, decimal currentPrice, params CorporateAction[] actions)
    {
        _tradePlanRepo.Setup(r => r.GetAdvancedInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { plan });
        _stockPriceRepo.Setup(r => r.GetLatestPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockPrice>
            {
                new(plan.Symbol, DateTime.UtcNow, currentPrice, currentPrice, currentPrice, currentPrice, 1000, "Test")
            });
        _corporateActionRepo
            .Setup(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions);
    }

    private static CorporateAction StockDividend30() =>
        CorporateAction.StockDividend("port-1", "user-1", "HPG", 10, 13, ExDate, ExDate.AddDays(20));

    private static ScenarioNode Node(ScenarioConditionType type, decimal value,
        ScenarioActionType action = ScenarioActionType.SellAll) => new()
        {
            NodeId = "n1",
            ParentId = null,
            Order = 0,
            Label = "Nhánh test",
            ConditionType = type,
            ConditionValue = value,
            ActionType = action
        };

    // ─── PricePercentChange ──────────────────────────────────────────────

    [Fact]
    public async Task PricePercentChange_DoesNotFire_WhenTheDropIsOnlyTheAdjustment()
    {
        // Giá 23.100 so với giá nhập gốc 30.000 là −23% → cắt lỗ oan.
        // Quy giá nhập về 23.076,92 thì thực tế mới +0,1%.
        var plan = PlanWith(Node(ScenarioConditionType.PricePercentChange, -10m));
        SetupPlan(plan, 23_100m, StockDividend30());

        var results = await _sut.EvaluateAllAsync();

        results.Should().BeEmpty();
        plan.ScenarioNodes![0].Status.Should().Be(ScenarioNodeStatus.Pending);
    }

    [Fact]
    public async Task PricePercentChange_StillFires_OnARealDrop()
    {
        // 20.000 so với giá nhập đã quy đổi 23.076,92 là −13,3% → cắt lỗ thật.
        var plan = PlanWith(Node(ScenarioConditionType.PricePercentChange, -10m));
        SetupPlan(plan, 20_000m, StockDividend30());

        var results = await _sut.EvaluateAllAsync();

        results.Should().ContainSingle();
    }

    // ─── PriceAbove / PriceBelow ─────────────────────────────────────────

    [Fact]
    public async Task PriceAbove_FiresAtTheAdjustedThreshold()
    {
        // Ngưỡng chốt lời 39.000 đặt trước sự kiện = 30.000 sau khi điều chỉnh.
        var plan = PlanWith(Node(ScenarioConditionType.PriceAbove, 39_000m, ScenarioActionType.SellPercent));
        SetupPlan(plan, 30_100m, StockDividend30());

        var results = await _sut.EvaluateAllAsync();

        results.Should().ContainSingle();
        results[0].ConditionValue.Should().BeApproximately(30_000m, 0.01m);
    }

    [Fact]
    public async Task PriceBelow_DoesNotFire_JustBecauseThePriceWasAdjusted()
    {
        // Cắt lỗ đặt ở 27.000; sau điều chỉnh tương đương 20.769. Giá 23.100 chưa thủng.
        var plan = PlanWith(Node(ScenarioConditionType.PriceBelow, 27_000m));
        SetupPlan(plan, 23_100m, StockDividend30());

        var results = await _sut.EvaluateAllAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task PlanCreatedAfterTheExDate_IsNotAdjustedAgain()
    {
        var plan = PlanWith(Node(ScenarioConditionType.PriceBelow, 23_000m));
        typeof(TradePlan).GetProperty(nameof(TradePlan.PricesSetAt))!
            .SetValue(plan, ExDate.AddDays(1));
        typeof(TradePlan).GetProperty(nameof(TradePlan.ScenarioPricesSetAt))!
            .SetValue(plan, ExDate.AddDays(1));
        SetupPlan(plan, 22_900m, StockDividend30());

        var results = await _sut.EvaluateAllAsync();

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task ActionsOfAnotherSymbol_DoNotTouchThePlan()
    {
        var plan = PlanWith(Node(ScenarioConditionType.PriceBelow, 27_000m));
        SetupPlan(plan, 26_900m,
            CorporateAction.StockDividend("port-1", "user-1", "VNM", 10, 13, ExDate, null));

        var results = await _sut.EvaluateAllAsync();

        results.Should().ContainSingle();
    }

    // ─── Trailing stop ───────────────────────────────────────────────────

    [Fact]
    public async Task TrailingStopState_IsRebasedOnce_AndThePlanIsSaved()
    {
        var trailing = new ScenarioNode
        {
            NodeId = "trail",
            Order = 0,
            Label = "Trailing",
            ConditionType = ScenarioConditionType.PriceAbove,
            ConditionValue = 32_000m,
            ActionType = ScenarioActionType.ActivateTrailingStop,
            Status = ScenarioNodeStatus.Triggered,
            TrailingStopConfig = new TrailingStopConfig
            {
                Method = TrailingStopMethod.Percentage,
                TrailValue = 8m,
                HighestPrice = 39_000m,
                CurrentTrailingStop = 35_880m
            }
        };
        var child = new ScenarioNode
        {
            NodeId = "hit",
            ParentId = "trail",
            Order = 1,
            Label = "Trượt chạm",
            ConditionType = ScenarioConditionType.TrailingStopHit,
            ActionType = ScenarioActionType.SellAll
        };

        var plan = PlanWith(trailing, child);
        // SetScenarioNodes reset Status → đặt lại sau khi dựng.
        plan.ScenarioNodes![0].Status = ScenarioNodeStatus.Triggered;
        SetupPlan(plan, 29_000m, StockDividend30());

        var results = await _sut.EvaluateAllAsync();

        // Đỉnh 39.000 và mức trượt 35.880 quy về 30.000 / 27.600 — giá 29.000 chưa chạm.
        plan.ScenarioNodes[0].TrailingStopConfig!.HighestPrice.Should().BeApproximately(30_000m, 0.01m);
        plan.ScenarioNodes[0].TrailingStopConfig!.CurrentTrailingStop.Should().BeApproximately(27_600m, 0.01m);
        plan.ScenarioNodes[0].TrailingStopConfig!.PriceBasisAt.Should().NotBeNull();
        results.Should().BeEmpty();

        // Phải lưu lại, nếu không lần chạy sau sẽ hạ thêm một lần nữa.
        _tradePlanRepo.Verify(r => r.UpdateAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrailingStopState_IsNotRebasedTwice()
    {
        var trailing = new ScenarioNode
        {
            NodeId = "trail",
            Order = 0,
            Label = "Trailing",
            ConditionType = ScenarioConditionType.PriceAbove,
            ConditionValue = 32_000m,
            ActionType = ScenarioActionType.ActivateTrailingStop,
            TrailingStopConfig = new TrailingStopConfig
            {
                Method = TrailingStopMethod.Percentage,
                TrailValue = 8m,
                HighestPrice = 30_000m,
                CurrentTrailingStop = 27_600m,
                PriceBasisAt = DateTime.UtcNow.AddDays(-1) // đã quy đổi sau ngày GDKHQ
            }
        };
        var plan = PlanWith(trailing);
        plan.ScenarioNodes![0].Status = ScenarioNodeStatus.Triggered;
        SetupPlan(plan, 29_000m, StockDividend30());

        await _sut.EvaluateAllAsync();

        plan.ScenarioNodes[0].TrailingStopConfig!.HighestPrice.Should().Be(30_000m);
        plan.ScenarioNodes[0].TrailingStopConfig!.CurrentTrailingStop.Should().Be(27_600m);
    }
}
