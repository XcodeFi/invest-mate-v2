using FluentAssertions;
using FluentValidation;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Decisions.Commands.ResolveDecision;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Moq;

namespace InvestmentApp.Application.Tests.Decisions;

/// <summary>
/// Tests P4 ResolveDecision command — inline action BÁN/GIỮ trên Decision Queue widget.
/// Xem `docs/plans/dashboard-decision-engine.md` §6.
/// </summary>
public class ResolveDecisionCommandHandlerTests
{
    private readonly Mock<ITradePlanRepository> _planRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IJournalEntryRepository> _journalRepo = new();
    private readonly Mock<IStockPriceService> _priceService = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly ResolveDecisionCommandHandler _handler;
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    public ResolveDecisionCommandHandlerTests()
    {
        _handler = new ResolveDecisionCommandHandler(
            _planRepo.Object, _tradeRepo.Object, _portfolioRepo.Object,
            _journalRepo.Object, _priceService.Object, _auditService.Object);
    }

    // ----------------------------------------------------------------
    // ExecuteSell
    // ----------------------------------------------------------------
    [Fact]
    public async Task Handle_ExecuteSell_SingleLot_UsesPlanQuantityAndCurrentPrice()
    {
        var plan = MakeSingleLotPlan(planId: "plan1", symbol: "FPT", portfolioId: "p1", quantity: 100);
        SetupPlan(plan);
        SetupPortfolio("p1", UserId);
        SetupCurrentPrice("FPT", 89.5m);

        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "StopLossHit:p1:FPT",
            Action = DecisionAction.ExecuteSell,
            TradePlanId = "plan1",
            UserId = UserId
        };

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.ResultType.Should().Be("Trade");
        _tradeRepo.Verify(r => r.AddAsync(
            It.Is<Trade>(t => t.Quantity == 100 && t.TradeType == TradeType.SELL
                              && t.Price == 89.5m && t.Symbol == "FPT"
                              && t.TradePlanId == "plan1"),
            It.IsAny<CancellationToken>()), Times.Once);
        _portfolioRepo.Verify(r => r.UpdateAsync(It.IsAny<Portfolio>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExecuteSell_MultiLot_SumsOnlyExecutedLots()
    {
        var plan = MakeMultiLotPlan(planId: "plan2", symbol: "VNM", portfolioId: "p1", lots: new[]
        {
            (qty: 50, status: PlanLotStatus.Executed),
            (qty: 30, status: PlanLotStatus.Executed),
            (qty: 20, status: PlanLotStatus.Pending)   // không tính
        });
        SetupPlan(plan);
        SetupPortfolio("p1", UserId);
        SetupCurrentPrice("VNM", 75m);

        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "ScenarioTrigger:plan2:n1",
            Action = DecisionAction.ExecuteSell,
            TradePlanId = "plan2",
            UserId = UserId
        };

        await _handler.Handle(cmd, CancellationToken.None);

        _tradeRepo.Verify(r => r.AddAsync(
            It.Is<Trade>(t => t.Quantity == 80m),    // 50 + 30, không tính 20 pending
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExecuteSell_RejectsForOtherUserPlan()
    {
        var plan = MakeSingleLotPlan("plan1", "FPT", "p1", 100, ownerUserId: OtherUserId);
        SetupPlan(plan);

        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "StopLossHit:p1:FPT",
            Action = DecisionAction.ExecuteSell,
            TradePlanId = "plan1",
            UserId = UserId
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tradeRepo.Verify(r => r.AddAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExecuteSell_PortfolioOwnedByOtherUser_RejectsEvenIfPlanOwnerMatches()
    {
        // Defense in depth: plan.UserId == request.UserId, but plan.PortfolioId points to another user's portfolio.
        // Crafted plan would otherwise let attacker AddTrade to victim's portfolio.
        var plan = MakeSingleLotPlan("plan1", "FPT", "p-other", 100);  // plan owned by UserId
        SetupPlan(plan);
        SetupPortfolio("p-other", OtherUserId);                          // portfolio owned by OtherUserId
        SetupCurrentPrice("FPT", 89.5m);

        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "StopLossHit:p-other:FPT",
            Action = DecisionAction.ExecuteSell,
            TradePlanId = "plan1",
            UserId = UserId
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tradeRepo.Verify(r => r.AddAsync(It.IsAny<Trade>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExecuteSell_PlanNotFound_Throws()
    {
        _planRepo.Setup(r => r.GetByIdAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradePlan?)null);

        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "x",
            Action = DecisionAction.ExecuteSell,
            TradePlanId = "nope",
            UserId = UserId
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_ExecuteSell_MultiLotNoExecutedLots_ThrowsPositionClosed()
    {
        var plan = MakeMultiLotPlan("plan3", "HPG", "p1", new[]
        {
            (qty: 100, status: PlanLotStatus.Pending),    // không có executed nào
        });
        SetupPlan(plan);
        SetupPortfolio("p1", UserId);

        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "ScenarioTrigger:plan3:n1",
            Action = DecisionAction.ExecuteSell,
            TradePlanId = "plan3",
            UserId = UserId
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã đóng*");
    }

    // ----------------------------------------------------------------
    // HoldWithJournal
    // ----------------------------------------------------------------
    [Fact]
    public async Task Handle_HoldWithJournal_RejectsShortNote()
    {
        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "StopLossHit:p1:FPT",
            Action = DecisionAction.HoldWithJournal,
            TradePlanId = "plan1",
            Note = "ngắn",   // < 20 chars
            UserId = UserId
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*ít nhất 20 ký tự*");
        _journalRepo.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HoldWithJournal_LinksToPlanWhenProvided()
    {
        var plan = MakeSingleLotPlan("plan-fpt", "FPT", "p1", 100);
        SetupPlan(plan);

        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "ThesisReviewDue:plan-fpt",
            Action = DecisionAction.HoldWithJournal,
            TradePlanId = "plan-fpt",
            Note = "Thesis vẫn còn nguyên, earnings tiếp theo confirm trong 2 tuần nữa",
            UserId = UserId
        };

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.ResultType.Should().Be("JournalEntry");
        _journalRepo.Verify(r => r.AddAsync(
            It.Is<JournalEntry>(j =>
                j.TradePlanId == "plan-fpt"
                && j.EntryType == JournalEntryType.Decision
                && j.Symbol == "FPT"
                && j.Tags.Contains("decision-hold")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HoldWithJournal_WithoutPlan_UsesSymbolFallback()
    {
        // StopLossHit có thể không có TradePlanId trong DTO — vẫn phải cho phép HoldWithJournal.
        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "StopLossHit:p1:HPG",
            Action = DecisionAction.HoldWithJournal,
            TradePlanId = null,
            Symbol = "HPG",
            Note = "Tin xấu chỉ là ngắn hạn, hold tiếp theo dõi 1 tuần nữa",
            UserId = UserId
        };

        await _handler.Handle(cmd, CancellationToken.None);

        _journalRepo.Verify(r => r.AddAsync(
            It.Is<JournalEntry>(j =>
                j.Symbol == "HPG"
                && j.TradePlanId == null
                && j.EntryType == JournalEntryType.Decision),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HoldWithJournal_RejectsForOtherUserPlan()
    {
        var plan = MakeSingleLotPlan("plan1", "FPT", "p1", 100, ownerUserId: OtherUserId);
        SetupPlan(plan);

        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "StopLossHit:p1:FPT",
            Action = DecisionAction.HoldWithJournal,
            TradePlanId = "plan1",
            Note = "Thesis vẫn nguyên, hold thêm 1 tuần để theo dõi earnings tiếp theo",
            UserId = UserId
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ExecuteSell_WithoutTradePlanId_ValidatorRejects()
    {
        var cmd = new ResolveDecisionCommand
        {
            DecisionId = "StopLossHit:p1:FPT",
            Action = DecisionAction.ExecuteSell,
            TradePlanId = null,
            UserId = UserId
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*TradePlanId*");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private void SetupPlan(TradePlan plan)
    {
        _planRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
    }

    private void SetupPortfolio(string portfolioId, string ownerUserId)
    {
        var portfolio = new Portfolio(ownerUserId, "Main", 100_000_000m);
        typeof(Portfolio).GetProperty("Id")!.SetValue(portfolio, portfolioId);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
    }

    private void SetupCurrentPrice(string symbol, decimal amount)
    {
        _priceService.Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == symbol)))
            .ReturnsAsync(new Money(amount, "VND"));
    }

    private static TradePlan MakeSingleLotPlan(string planId, string symbol, string portfolioId, int quantity, string ownerUserId = UserId)
    {
        var plan = new TradePlan(ownerUserId, symbol, "Buy",
            entryPrice: 100m, stopLoss: 95m, target: 120m,
            quantity: quantity, portfolioId: portfolioId,
            accountBalance: 100_000_000m,
            thesis: "Test thesis ≥ 15 chars for discipline gate");
        typeof(TradePlan).GetProperty("Id")!.SetValue(plan, planId);
        return plan;
    }

    private static TradePlan MakeMultiLotPlan(
        string planId, string symbol, string portfolioId,
        (int qty, PlanLotStatus status)[] lots, string ownerUserId = UserId)
    {
        var plan = MakeSingleLotPlan(planId, symbol, portfolioId, quantity: lots.Sum(l => l.qty), ownerUserId: ownerUserId);
        var planLots = lots.Select((t, i) => new PlanLot
        {
            LotNumber = i + 1,
            PlannedPrice = 100m,
            PlannedQuantity = t.qty,
            Status = t.status
        }).ToList();
        // Bypass entity gate (entity throws if Status=Executed/Reviewed); set via reflection.
        typeof(TradePlan).GetProperty("Lots")!.SetValue(plan, planLots);
        typeof(TradePlan).GetProperty("EntryMode")!.SetValue(plan, EntryMode.ScalingIn);
        return plan;
    }
}
