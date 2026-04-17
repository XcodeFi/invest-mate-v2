using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class TradePlanTests
{
    #region Helpers

    private static TradePlan CreateDefaultPlan(
        string userId = "user-1",
        string symbol = "VNM",
        string direction = "Buy",
        decimal entryPrice = 80_000m,
        decimal stopLoss = 75_000m,
        decimal target = 90_000m,
        int quantity = 100,
        int confidenceLevel = 5)
    {
        return new TradePlan(userId, symbol, direction,
            entryPrice, stopLoss, target, quantity,
            confidenceLevel: confidenceLevel);
    }

    private static TradePlan CreateInProgressPlan()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        return plan;
    }

    private static TradePlan CreateExecutedPlan(string tradeId = "trade-1")
    {
        var plan = CreateInProgressPlan();
        plan.Execute(tradeId);
        return plan;
    }

    private static TradePlan CreateReviewedPlan()
    {
        var plan = CreateExecutedPlan();
        plan.MarkReviewed(CreateReviewData());
        return plan;
    }

    private static CampaignReviewData CreateReviewData(decimal pnlAmount = 8_000_000m, decimal pnlPercent = 10m)
    {
        return new CampaignReviewData
        {
            PnLAmount = pnlAmount,
            PnLPercent = pnlPercent,
            HoldingDays = 30,
            PnLPerDay = pnlAmount / 30m,
            AnnualizedReturnPercent = 121.67m,
            TargetAchievementPercent = 80m,
            TotalInvested = 80_000_000m,
            TotalReturned = 88_000_000m,
            TotalFees = 200_000m,
            ReviewedAt = DateTime.UtcNow
        };
    }

    private static List<PlanLot> CreateLots(params (int lotNumber, decimal price, int qty)[] specs)
    {
        return specs.Select(s => new PlanLot
        {
            LotNumber = s.lotNumber,
            PlannedPrice = s.price,
            PlannedQuantity = s.qty,
            Status = PlanLotStatus.Pending
        }).ToList();
    }

    private static List<ExitTarget> CreateExitTargets(params (int level, decimal price, ExitActionType action)[] specs)
    {
        return specs.Select(s => new ExitTarget
        {
            Level = s.level,
            Price = s.price,
            ActionType = s.action
        }).ToList();
    }

    #endregion

    // =====================================================================
    // Constructor
    // =====================================================================

    #region Constructor

    [Fact]
    public void Constructor_ValidParameters_ShouldCreatePlan()
    {
        // Act
        var plan = new TradePlan("user-1", "vnm", "Buy",
            80_000m, 75_000m, 90_000m, 100,
            portfolioId: "port-1", strategyId: "strat-1",
            marketCondition: "Sideways", reason: "breakout",
            notes: "test", riskPercent: 2m,
            accountBalance: 100_000_000m, riskRewardRatio: 2m,
            confidenceLevel: 7);

        // Assert
        plan.Id.Should().NotBeNullOrEmpty();
        plan.UserId.Should().Be("user-1");
        plan.Symbol.Should().Be("VNM");
        plan.Direction.Should().Be("Buy");
        plan.EntryPrice.Should().Be(80_000m);
        plan.StopLoss.Should().Be(75_000m);
        plan.Target.Should().Be(90_000m);
        plan.Quantity.Should().Be(100);
        plan.PortfolioId.Should().Be("port-1");
        plan.StrategyId.Should().Be("strat-1");
        plan.MarketCondition.Should().Be("Sideways");
        plan.Reason.Should().Be("breakout");
        plan.Notes.Should().Be("test");
        plan.RiskPercent.Should().Be(2m);
        plan.AccountBalance.Should().Be(100_000_000m);
        plan.RiskRewardRatio.Should().Be(2m);
        plan.ConfidenceLevel.Should().Be(7);
        plan.Status.Should().Be(TradePlanStatus.Draft);
        plan.IsDeleted.Should().BeFalse();
        plan.Checklist.Should().BeEmpty();
        plan.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        plan.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        var action = () => new TradePlan(null!, "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100);

        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullSymbol_ShouldThrowArgumentNullException()
    {
        var action = () => new TradePlan("user-1", null!, "Buy", 80_000m, 75_000m, 90_000m, 100);

        action.Should().Throw<ArgumentNullException>().WithParameterName("symbol");
    }

    [Theory]
    [InlineData("vnm", "VNM")]
    [InlineData("  hpg  ", "HPG")]
    [InlineData("FpT", "FPT")]
    [InlineData("  mwg ", "MWG")]
    public void Constructor_SymbolNormalization_ShouldUpperCaseAndTrim(string input, string expected)
    {
        var plan = CreateDefaultPlan(symbol: input);

        plan.Symbol.Should().Be(expected);
    }

    [Fact]
    public void Constructor_NullDirection_ShouldDefaultToBuy()
    {
        var plan = new TradePlan("user-1", "VNM", null!, 80_000m, 75_000m, 90_000m, 100);

        plan.Direction.Should().Be("Buy");
    }

    [Fact]
    public void Constructor_NullMarketCondition_ShouldDefaultToTrending()
    {
        var plan = CreateDefaultPlan();

        plan.MarketCondition.Should().Be("Trending");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(15, 10)]
    [InlineData(100, 10)]
    public void Constructor_ConfidenceLevel_ShouldClampBetween1And10(int input, int expected)
    {
        var plan = CreateDefaultPlan(confidenceLevel: input);

        plan.ConfidenceLevel.Should().Be(expected);
    }

    [Fact]
    public void Constructor_StatusDefaultsToDraft()
    {
        var plan = CreateDefaultPlan();

        plan.Status.Should().Be(TradePlanStatus.Draft);
    }

    [Fact]
    public void Constructor_NullChecklist_ShouldDefaultToEmptyList()
    {
        var plan = CreateDefaultPlan();

        plan.Checklist.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Constructor_WithChecklist_ShouldPreserveItems()
    {
        var checklist = new List<ChecklistItem>
        {
            new() { Label = "Check trend", Category = "TA", Checked = true, Critical = true },
            new() { Label = "Check volume", Category = "TA", Checked = false }
        };

        var plan = new TradePlan("user-1", "VNM", "Buy",
            80_000m, 75_000m, 90_000m, 100, checklist: checklist);

        plan.Checklist.Should().HaveCount(2);
        plan.Checklist[0].Label.Should().Be("Check trend");
        plan.Checklist[0].Checked.Should().BeTrue();
        plan.Checklist[0].Critical.Should().BeTrue();
    }

    #endregion

    // =====================================================================
    // Update
    // =====================================================================

    #region Update

    [Fact]
    public void Update_DraftPlan_ShouldUpdateFields()
    {
        var plan = CreateDefaultPlan();
        var versionBefore = plan.Version;

        plan.Update(
            symbol: "hpg",
            direction: "Sell",
            entryPrice: 30_000m,
            stopLoss: 32_000m,
            target: 25_000m,
            quantity: 200,
            marketCondition: "Sideways",
            reason: "breakdown",
            notes: "updated",
            riskPercent: 3m,
            confidenceLevel: 9);

        plan.Symbol.Should().Be("HPG");
        plan.Direction.Should().Be("Sell");
        plan.EntryPrice.Should().Be(30_000m);
        plan.StopLoss.Should().Be(32_000m);
        plan.Target.Should().Be(25_000m);
        plan.Quantity.Should().Be(200);
        plan.MarketCondition.Should().Be("Sideways");
        plan.Reason.Should().Be("breakdown");
        plan.Notes.Should().Be("updated");
        plan.RiskPercent.Should().Be(3m);
        plan.ConfidenceLevel.Should().Be(9);
        plan.Version.Should().Be(versionBefore + 1);
    }

    [Fact]
    public void Update_WithConfidenceClamping_ShouldClamp()
    {
        var plan = CreateDefaultPlan();

        plan.Update(confidenceLevel: 20);
        plan.ConfidenceLevel.Should().Be(10);

        plan.Update(confidenceLevel: -3);
        plan.ConfidenceLevel.Should().Be(1);
    }

    [Fact]
    public void Update_ReadyPlan_ShouldSucceed()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();

        var action = () => plan.Update(notes: "refined");

        action.Should().NotThrow();
        plan.Notes.Should().Be("refined");
    }

    [Fact]
    public void Update_InProgressPlan_ShouldSucceed()
    {
        var plan = CreateInProgressPlan();

        var action = () => plan.Update(notes: "in flight");

        action.Should().NotThrow();
    }

    [Fact]
    public void Update_ExecutedPlan_ShouldThrow()
    {
        var plan = CreateExecutedPlan();

        var action = () => plan.Update(notes: "too late");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*executed or reviewed*");
    }

    [Fact]
    public void Update_ReviewedPlan_ShouldThrow()
    {
        var plan = CreateReviewedPlan();

        var action = () => plan.Update(notes: "too late");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*executed or reviewed*");
    }

    [Fact]
    public void Update_PartialFields_ShouldOnlyChangeSpecifiedFields()
    {
        var plan = CreateDefaultPlan();

        plan.Update(notes: "only notes");

        plan.Symbol.Should().Be("VNM");
        plan.Direction.Should().Be("Buy");
        plan.EntryPrice.Should().Be(80_000m);
        plan.Notes.Should().Be("only notes");
    }

    [Fact]
    public void Update_ShouldAdvanceUpdatedAt()
    {
        var plan = CreateDefaultPlan();
        var before = plan.UpdatedAt;

        plan.Update(notes: "change");

        plan.UpdatedAt.Should().BeOnOrAfter(before);
    }

    #endregion

    // =====================================================================
    // Status Transitions
    // =====================================================================

    #region MarkReady

    [Fact]
    public void MarkReady_FromDraft_ShouldSucceed()
    {
        var plan = CreateDefaultPlan();

        plan.MarkReady();

        plan.Status.Should().Be(TradePlanStatus.Ready);
    }

    [Fact]
    public void MarkReady_FromReady_ShouldBeIdempotent()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        var versionBefore = plan.Version;

        plan.MarkReady();

        plan.Status.Should().Be(TradePlanStatus.Ready);
        plan.Version.Should().Be(versionBefore);
    }

    [Fact]
    public void MarkReady_FromInProgress_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();

        var action = () => plan.MarkReady();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReady_FromExecuted_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");

        var action = () => plan.MarkReady();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReady_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var vBefore = plan.Version;

        plan.MarkReady();

        plan.Version.Should().Be(vBefore + 1);
    }

    #endregion

    #region MarkInProgress

    [Fact]
    public void MarkInProgress_FromDraft_ShouldThrow()
    {
        var plan = CreateDefaultPlan();

        var action = () => plan.MarkInProgress();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkInProgress_FromReady_ShouldSucceed()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();

        plan.MarkInProgress();

        plan.Status.Should().Be(TradePlanStatus.InProgress);
    }

    [Fact]
    public void MarkInProgress_FromInProgress_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();

        var action = () => plan.MarkInProgress();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkInProgress_FromExecuted_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");

        var action = () => plan.MarkInProgress();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkInProgress_FromCancelled_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.Cancel();

        var action = () => plan.MarkInProgress();

        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Execute

    [Fact]
    public void Execute_ShouldSetStatusAndTradeIdAndExecutedAt()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();

        plan.Execute("trade-42");

        plan.Status.Should().Be(TradePlanStatus.Executed);
        plan.TradeId.Should().Be("trade-42");
        plan.ExecutedAt.Should().NotBeNull();
        plan.ExecutedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Execute_FromDraft_ShouldThrow()
    {
        var plan = CreateDefaultPlan();

        var action = () => plan.Execute("trade-1");

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Execute_NullTradeId_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();

        var action = () => plan.Execute(null!);

        action.Should().Throw<ArgumentNullException>().WithParameterName("tradeId");
    }

    [Fact]
    public void Execute_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        var vBefore = plan.Version;

        plan.Execute("trade-1");

        plan.Version.Should().Be(vBefore + 1);
    }

    #endregion

    #region MarkReviewed

    [Fact]
    public void MarkReviewed_FromExecuted_ShouldSucceed()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");

        plan.MarkReviewed(CreateReviewData());

        plan.Status.Should().Be(TradePlanStatus.Reviewed);
    }

    [Fact]
    public void MarkReviewed_FromDraft_ShouldThrow()
    {
        var plan = CreateDefaultPlan();

        var action = () => plan.MarkReviewed(CreateReviewData());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*executed*");
    }

    [Fact]
    public void MarkReviewed_FromReady_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();

        var action = () => plan.MarkReviewed(CreateReviewData());

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReviewed_FromInProgress_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();

        var action = () => plan.MarkReviewed(CreateReviewData());

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReviewed_FromReviewed_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");
        plan.MarkReviewed(CreateReviewData());

        var action = () => plan.MarkReviewed(CreateReviewData());

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReviewed_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");
        var vBefore = plan.Version;

        plan.MarkReviewed(CreateReviewData());

        plan.Version.Should().Be(vBefore + 1);
    }

    #endregion

    #region Cancel

    [Fact]
    public void Cancel_FromDraft_ShouldSucceed()
    {
        var plan = CreateDefaultPlan();

        plan.Cancel();

        plan.Status.Should().Be(TradePlanStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromReady_ShouldSucceed()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();

        plan.Cancel();

        plan.Status.Should().Be(TradePlanStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromInProgress_ShouldSucceed()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();

        plan.Cancel();

        plan.Status.Should().Be(TradePlanStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromExecuted_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");

        var action = () => plan.Cancel();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*executed or reviewed*");
    }

    [Fact]
    public void Cancel_FromReviewed_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");
        plan.MarkReviewed(CreateReviewData());

        var action = () => plan.Cancel();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*executed or reviewed*");
    }

    [Fact]
    public void Cancel_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var vBefore = plan.Version;

        plan.Cancel();

        plan.Version.Should().Be(vBefore + 1);
    }

    [Fact]
    public void Restore_FromCancelled_ShouldReturnToDraft()
    {
        var plan = CreateDefaultPlan();
        plan.Cancel();

        plan.Restore();

        plan.Status.Should().Be(TradePlanStatus.Draft);
    }

    [Fact]
    public void Restore_FromDraft_ShouldThrow()
    {
        var plan = CreateDefaultPlan();

        var action = () => plan.Restore();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cancelled*");
    }

    #endregion

    // =====================================================================
    // SoftDelete
    // =====================================================================

    #region SoftDelete

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrue()
    {
        var plan = CreateDefaultPlan();

        plan.SoftDelete();

        plan.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var vBefore = plan.Version;

        plan.SoftDelete();

        plan.Version.Should().Be(vBefore + 1);
    }

    [Fact]
    public void SoftDelete_ShouldUpdateTimestamp()
    {
        var plan = CreateDefaultPlan();
        var before = plan.UpdatedAt;

        plan.SoftDelete();

        plan.UpdatedAt.Should().BeOnOrAfter(before);
    }

    #endregion

    // =====================================================================
    // SetLots
    // =====================================================================

    #region SetLots

    [Fact]
    public void SetLots_DraftPlan_ShouldSetLotsAndRecalculateQuantity()
    {
        var plan = CreateDefaultPlan(quantity: 100);
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 75), (3, 76_000m, 25));

        plan.SetLots(EntryMode.ScalingIn, lots);

        plan.EntryMode.Should().Be(EntryMode.ScalingIn);
        plan.Lots.Should().HaveCount(3);
        plan.Quantity.Should().Be(150); // 50 + 75 + 25
    }

    [Fact]
    public void SetLots_ReadyPlan_ShouldSucceed()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        var lots = CreateLots((1, 80_000m, 100));

        var action = () => plan.SetLots(EntryMode.Single, lots);

        action.Should().NotThrow();
    }

    [Fact]
    public void SetLots_InProgressPlan_ShouldSucceed()
    {
        var plan = CreateInProgressPlan();
        var lots = CreateLots((1, 80_000m, 100));

        var action = () => plan.SetLots(EntryMode.DCA, lots);

        action.Should().NotThrow();
    }

    [Fact]
    public void SetLots_ExecutedPlan_ShouldThrow()
    {
        var plan = CreateExecutedPlan();
        var lots = CreateLots((1, 80_000m, 100));

        var action = () => plan.SetLots(EntryMode.Single, lots);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*executed/reviewed*");
    }

    [Fact]
    public void SetLots_ReviewedPlan_ShouldThrow()
    {
        var plan = CreateReviewedPlan();
        var lots = CreateLots((1, 80_000m, 100));

        var action = () => plan.SetLots(EntryMode.Single, lots);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*executed/reviewed*");
    }

    [Fact]
    public void SetLots_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var vBefore = plan.Version;
        var lots = CreateLots((1, 80_000m, 100));

        plan.SetLots(EntryMode.Single, lots);

        plan.Version.Should().Be(vBefore + 1);
    }

    [Fact]
    public void SetLots_DCA_ShouldSetEntryModeToDCA()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 76_000m, 50));

        plan.SetLots(EntryMode.DCA, lots);

        plan.EntryMode.Should().Be(EntryMode.DCA);
    }

    #endregion

    // =====================================================================
    // ExecuteLot
    // =====================================================================

    #region ExecuteLot

    [Fact]
    public void ExecuteLot_ValidLot_ShouldMarkLotExecuted()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);

        plan.ExecuteLot(1, "trade-A", 80_500m);

        var lot = plan.Lots!.First(l => l.LotNumber == 1);
        lot.Status.Should().Be(PlanLotStatus.Executed);
        lot.ActualPrice.Should().Be(80_500m);
        lot.TradeId.Should().Be("trade-A");
        lot.ExecutedAt.Should().NotBeNull();
    }

    [Fact]
    public void ExecuteLot_ShouldAddTradeIdToTradeIds()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);

        plan.ExecuteLot(1, "trade-A", 80_500m);

        plan.TradeIds.Should().Contain("trade-A");
    }

    [Fact]
    public void ExecuteLot_FromDraft_ShouldAutoTransitionToInProgress()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);
        plan.Status.Should().Be(TradePlanStatus.Draft);

        plan.ExecuteLot(1, "trade-A", 80_500m);

        plan.Status.Should().Be(TradePlanStatus.InProgress);
    }

    [Fact]
    public void ExecuteLot_FromReady_ShouldAutoTransitionToInProgress()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);
        plan.MarkReady();

        plan.ExecuteLot(1, "trade-A", 80_500m);

        plan.Status.Should().Be(TradePlanStatus.InProgress);
    }

    [Fact]
    public void ExecuteLot_AllLotsExecuted_ShouldAutoTransitionToExecuted()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);

        plan.ExecuteLot(1, "trade-A", 80_500m);
        plan.ExecuteLot(2, "trade-B", 78_200m);

        plan.Status.Should().Be(TradePlanStatus.Executed);
        plan.ExecutedAt.Should().NotBeNull();
    }

    [Fact]
    public void ExecuteLot_AllLotsExecuted_TradeIdsShouldContainAll()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);

        plan.ExecuteLot(1, "trade-A", 80_500m);
        plan.ExecuteLot(2, "trade-B", 78_200m);

        plan.TradeIds.Should().HaveCount(2);
        plan.TradeIds.Should().ContainInOrder("trade-A", "trade-B");
    }

    [Fact]
    public void ExecuteLot_MixedExecutedAndCancelled_ShouldAutoTransitionToExecuted()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);

        // Cancel lot 2 manually, then execute lot 1
        plan.Lots![1].Status = PlanLotStatus.Cancelled;
        plan.ExecuteLot(1, "trade-A", 80_500m);

        // All lots are non-pending, so auto-transition
        plan.Status.Should().Be(TradePlanStatus.Executed);
    }

    [Fact]
    public void ExecuteLot_NoLots_ShouldThrow()
    {
        var plan = CreateDefaultPlan();

        var action = () => plan.ExecuteLot(1, "trade-A", 80_000m);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*no lots*");
    }

    [Fact]
    public void ExecuteLot_LotNotFound_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50));
        plan.SetLots(EntryMode.Single, lots);

        var action = () => plan.ExecuteLot(99, "trade-A", 80_000m);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*99*not found*");
    }

    [Fact]
    public void ExecuteLot_LotAlreadyExecuted_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);
        plan.ExecuteLot(1, "trade-A", 80_500m);

        var action = () => plan.ExecuteLot(1, "trade-B", 81_000m);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not pending*");
    }

    [Fact]
    public void ExecuteLot_LotCancelled_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 50));
        plan.SetLots(EntryMode.Single, lots);
        plan.Lots![0].Status = PlanLotStatus.Cancelled;

        var action = () => plan.ExecuteLot(1, "trade-A", 80_000m);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not pending*");
    }

    [Fact]
    public void ExecuteLot_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 100));
        plan.SetLots(EntryMode.Single, lots);
        var vBefore = plan.Version;

        plan.ExecuteLot(1, "trade-A", 80_000m);

        plan.Version.Should().Be(vBefore + 1);
    }

    [Fact]
    public void ExecuteLot_PartialExecution_ShouldRemainInProgress()
    {
        var plan = CreateDefaultPlan();
        var lots = CreateLots((1, 80_000m, 30), (2, 78_000m, 30), (3, 76_000m, 40));
        plan.SetLots(EntryMode.DCA, lots);

        plan.ExecuteLot(1, "trade-A", 80_000m);

        plan.Status.Should().Be(TradePlanStatus.InProgress);

        plan.ExecuteLot(2, "trade-B", 78_000m);

        // Lot 3 still pending
        plan.Status.Should().Be(TradePlanStatus.InProgress);
    }

    #endregion

    // =====================================================================
    // SetExitTargets & TriggerExitTarget
    // =====================================================================

    #region ExitTargets

    [Fact]
    public void SetExitTargets_ShouldStoreTargets()
    {
        var plan = CreateDefaultPlan();
        var targets = CreateExitTargets(
            (1, 85_000m, ExitActionType.PartialExit),
            (2, 90_000m, ExitActionType.TakeProfit));

        plan.SetExitTargets(targets);

        plan.ExitTargets.Should().HaveCount(2);
        plan.ExitTargets![0].Level.Should().Be(1);
        plan.ExitTargets![0].Price.Should().Be(85_000m);
        plan.ExitTargets![0].ActionType.Should().Be(ExitActionType.PartialExit);
        plan.ExitTargets![1].Level.Should().Be(2);
        plan.ExitTargets![1].ActionType.Should().Be(ExitActionType.TakeProfit);
    }

    [Fact]
    public void SetExitTargets_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var vBefore = plan.Version;

        plan.SetExitTargets(CreateExitTargets((1, 85_000m, ExitActionType.TakeProfit)));

        plan.Version.Should().Be(vBefore + 1);
    }

    [Fact]
    public void SetExitTargets_ShouldUpdateTimestamp()
    {
        var plan = CreateDefaultPlan();
        var before = plan.UpdatedAt;

        plan.SetExitTargets(CreateExitTargets((1, 85_000m, ExitActionType.TakeProfit)));

        plan.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void TriggerExitTarget_ValidLevel_ShouldMarkTriggered()
    {
        var plan = CreateDefaultPlan();
        plan.SetExitTargets(CreateExitTargets(
            (1, 85_000m, ExitActionType.PartialExit),
            (2, 90_000m, ExitActionType.TakeProfit)));

        plan.TriggerExitTarget(1, "trade-exit-1");

        var target = plan.ExitTargets!.First(t => t.Level == 1);
        target.IsTriggered.Should().BeTrue();
        target.TriggeredAt.Should().NotBeNull();
        target.TriggeredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        target.TradeId.Should().Be("trade-exit-1");
    }

    [Fact]
    public void TriggerExitTarget_ShouldAddToTradeIds()
    {
        var plan = CreateDefaultPlan();
        plan.SetExitTargets(CreateExitTargets((1, 85_000m, ExitActionType.TakeProfit)));

        plan.TriggerExitTarget(1, "trade-exit-1");

        plan.TradeIds.Should().Contain("trade-exit-1");
    }

    [Fact]
    public void TriggerExitTarget_LevelNotFound_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        plan.SetExitTargets(CreateExitTargets((1, 85_000m, ExitActionType.TakeProfit)));

        var action = () => plan.TriggerExitTarget(99, "trade-exit-1");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*99*not found*");
    }

    [Fact]
    public void TriggerExitTarget_NoExitTargetsSet_ShouldThrow()
    {
        var plan = CreateDefaultPlan();

        var action = () => plan.TriggerExitTarget(1, "trade-exit-1");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void TriggerExitTarget_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        plan.SetExitTargets(CreateExitTargets((1, 85_000m, ExitActionType.TakeProfit)));
        var vBefore = plan.Version;

        plan.TriggerExitTarget(1, "trade-exit-1");

        plan.Version.Should().Be(vBefore + 1);
    }

    [Fact]
    public void TriggerExitTarget_MultipleTargets_ShouldAccumulateTradeIds()
    {
        var plan = CreateDefaultPlan();
        plan.SetExitTargets(CreateExitTargets(
            (1, 85_000m, ExitActionType.PartialExit),
            (2, 90_000m, ExitActionType.TakeProfit)));

        plan.TriggerExitTarget(1, "trade-exit-1");
        plan.TriggerExitTarget(2, "trade-exit-2");

        plan.TradeIds.Should().HaveCount(2);
        plan.TradeIds.Should().ContainInOrder("trade-exit-1", "trade-exit-2");
    }

    #endregion

    // =====================================================================
    // UpdateStopLossWithHistory
    // =====================================================================

    #region UpdateStopLossWithHistory

    [Fact]
    public void UpdateStopLossWithHistory_ShouldChangeStopLoss()
    {
        var plan = CreateDefaultPlan(stopLoss: 75_000m);

        plan.UpdateStopLossWithHistory(77_000m, "trailing up");

        plan.StopLoss.Should().Be(77_000m);
    }

    [Fact]
    public void UpdateStopLossWithHistory_ShouldAddHistoryEntry()
    {
        var plan = CreateDefaultPlan(stopLoss: 75_000m);

        plan.UpdateStopLossWithHistory(77_000m, "trailing up");

        plan.StopLossHistory.Should().HaveCount(1);
        plan.StopLossHistory![0].OldPrice.Should().Be(75_000m);
        plan.StopLossHistory![0].NewPrice.Should().Be(77_000m);
        plan.StopLossHistory![0].Reason.Should().Be("trailing up");
        plan.StopLossHistory![0].ChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateStopLossWithHistory_MultipleCalls_ShouldAccumulateHistory()
    {
        var plan = CreateDefaultPlan(stopLoss: 75_000m);

        plan.UpdateStopLossWithHistory(77_000m, "first move");
        plan.UpdateStopLossWithHistory(78_000m, "second move");
        plan.UpdateStopLossWithHistory(79_000m);

        plan.StopLoss.Should().Be(79_000m);
        plan.StopLossHistory.Should().HaveCount(3);
        plan.StopLossHistory![0].OldPrice.Should().Be(75_000m);
        plan.StopLossHistory![0].NewPrice.Should().Be(77_000m);
        plan.StopLossHistory![1].OldPrice.Should().Be(77_000m);
        plan.StopLossHistory![1].NewPrice.Should().Be(78_000m);
        plan.StopLossHistory![2].OldPrice.Should().Be(78_000m);
        plan.StopLossHistory![2].NewPrice.Should().Be(79_000m);
        plan.StopLossHistory![2].Reason.Should().BeNull();
    }

    [Fact]
    public void UpdateStopLossWithHistory_NullReason_ShouldStoreNull()
    {
        var plan = CreateDefaultPlan(stopLoss: 75_000m);

        plan.UpdateStopLossWithHistory(77_000m);

        plan.StopLossHistory![0].Reason.Should().BeNull();
    }

    [Fact]
    public void UpdateStopLossWithHistory_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var vBefore = plan.Version;

        plan.UpdateStopLossWithHistory(77_000m, "reason");

        plan.Version.Should().Be(vBefore + 1);
    }

    [Fact]
    public void UpdateStopLossWithHistory_ShouldUpdateTimestamp()
    {
        var plan = CreateDefaultPlan();
        var before = plan.UpdatedAt;

        plan.UpdateStopLossWithHistory(77_000m);

        plan.UpdatedAt.Should().BeOnOrAfter(before);
    }

    #endregion

    // =====================================================================
    // Full Lifecycle Integration Tests
    // =====================================================================

    #region Lifecycle Integration

    [Fact]
    public void FullLifecycle_DraftToReviewedWithLots()
    {
        // Create plan
        var plan = CreateDefaultPlan();
        plan.Status.Should().Be(TradePlanStatus.Draft);

        // Set up multi-lot entry
        var lots = CreateLots((1, 80_000m, 50), (2, 78_000m, 50));
        plan.SetLots(EntryMode.ScalingIn, lots);
        plan.Quantity.Should().Be(100);

        // Set exit targets
        plan.SetExitTargets(CreateExitTargets(
            (1, 85_000m, ExitActionType.PartialExit),
            (2, 90_000m, ExitActionType.TakeProfit)));

        // Mark ready
        plan.MarkReady();
        plan.Status.Should().Be(TradePlanStatus.Ready);

        // Execute first lot -> auto InProgress
        plan.ExecuteLot(1, "trade-A", 80_200m);
        plan.Status.Should().Be(TradePlanStatus.InProgress);

        // Trail stop loss
        plan.UpdateStopLossWithHistory(77_000m, "support found");

        // Execute second lot -> auto Executed
        plan.ExecuteLot(2, "trade-B", 78_100m);
        plan.Status.Should().Be(TradePlanStatus.Executed);
        plan.ExecutedAt.Should().NotBeNull();

        // Trigger exit targets
        plan.TriggerExitTarget(1, "trade-exit-1");
        plan.TriggerExitTarget(2, "trade-exit-2");

        // Review
        plan.MarkReviewed(CreateReviewData());
        plan.Status.Should().Be(TradePlanStatus.Reviewed);

        // Verify complete state
        plan.TradeIds.Should().HaveCount(4); // 2 lots + 2 exits
        plan.StopLossHistory.Should().HaveCount(1);
        plan.Version.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FullLifecycle_DraftToCancelled()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.Cancel();

        plan.Status.Should().Be(TradePlanStatus.Cancelled);
    }

    [Fact]
    public void FullLifecycle_DirectExecutionWithoutLots()
    {
        var plan = CreateDefaultPlan();
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");

        plan.Status.Should().Be(TradePlanStatus.Executed);
        plan.TradeId.Should().Be("trade-1");

        plan.MarkReviewed(CreateReviewData());
        plan.Status.Should().Be(TradePlanStatus.Reviewed);
    }

    [Fact]
    public void VersionTracking_EachMutationIncrementsVersion()
    {
        var plan = CreateDefaultPlan();
        plan.Version.Should().Be(0);

        plan.Update(notes: "v1");
        plan.Version.Should().Be(1);

        plan.MarkReady();
        plan.Version.Should().Be(2);

        plan.MarkInProgress();
        plan.Version.Should().Be(3);

        plan.Execute("trade-1");
        plan.Version.Should().Be(4);

        plan.MarkReviewed(CreateReviewData());
        plan.Version.Should().Be(5);
    }

    #endregion
}
