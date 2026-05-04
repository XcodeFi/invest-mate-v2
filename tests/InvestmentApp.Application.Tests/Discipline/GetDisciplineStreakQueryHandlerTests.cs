using FluentAssertions;
using InvestmentApp.Application.Discipline.Queries;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Discipline;

/// <summary>
/// Tests P3 (v1.1) — Decision Queue empty state positive streak.
/// Streak = số ngày liên tiếp gần nhất không có SL violation.
/// </summary>
public class GetDisciplineStreakQueryHandlerTests
{
    private readonly Mock<ITradePlanRepository> _planRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly GetDisciplineStreakQueryHandler _handler;
    private const string UserId = "user-1";

    public GetDisciplineStreakQueryHandlerTests()
    {
        _handler = new GetDisciplineStreakQueryHandler(_planRepo.Object, _tradeRepo.Object);
    }

    [Fact]
    public async Task Handle_NoPlans_ReturnsZeroAndHasDataFalse()
    {
        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradePlan>());

        var result = await _handler.Handle(new GetDisciplineStreakQuery { UserId = UserId }, CancellationToken.None);

        result.DaysWithoutViolation.Should().Be(0);
        result.HasData.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoViolations_ReturnsDaysSinceFirstPlan()
    {
        var firstPlanCreatedAt = DateTime.UtcNow.AddDays(-12);
        var plan = MakeReadyPlan(createdAt: firstPlanCreatedAt);
        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());

        var result = await _handler.Handle(new GetDisciplineStreakQuery { UserId = UserId }, CancellationToken.None);

        result.HasData.Should().BeTrue();
        result.DaysWithoutViolation.Should().BeInRange(11, 12); // tolerance for date boundary
    }

    [Fact]
    public async Task Handle_HasSlViolationLossTrade_ReturnsDaysSinceViolation()
    {
        var planCreated = DateTime.UtcNow.AddDays(-30);
        var violationDate = DateTime.UtcNow.AddDays(-7);
        var plan = MakeExecutedPlan("p1", "FPT", entryPrice: 100m, stopLoss: 95m, createdAt: planCreated);

        var entry = MakeTrade(plan.Id, TradeType.BUY, qty: 100, price: 100m, date: planCreated);
        // Exit at 90 < SL 95 → violation
        var exit = MakeTrade(plan.Id, TradeType.SELL, qty: 100, price: 90m, date: violationDate);

        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry, exit });

        var result = await _handler.Handle(new GetDisciplineStreakQuery { UserId = UserId }, CancellationToken.None);

        result.HasData.Should().BeTrue();
        result.DaysWithoutViolation.Should().BeInRange(6, 7);
    }

    [Fact]
    public async Task Handle_LossTradeRespectsSL_NotCountedAsViolation()
    {
        var planCreated = DateTime.UtcNow.AddDays(-30);
        var plan = MakeExecutedPlan("p1", "VNM", entryPrice: 100m, stopLoss: 95m, createdAt: planCreated);
        var entry = MakeTrade(plan.Id, TradeType.BUY, qty: 100, price: 100m, date: planCreated);
        // Exit at 96 — loss but ≥ SL → honored
        var exit = MakeTrade(plan.Id, TradeType.SELL, qty: 100, price: 96m, date: DateTime.UtcNow.AddDays(-3));

        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry, exit });

        var result = await _handler.Handle(new GetDisciplineStreakQuery { UserId = UserId }, CancellationToken.None);

        result.HasData.Should().BeTrue();
        // No violation → streak = days since plan created
        result.DaysWithoutViolation.Should().BeInRange(29, 31);
    }

    [Fact]
    public async Task Handle_WinningTrade_NotCountedAsViolation()
    {
        var planCreated = DateTime.UtcNow.AddDays(-15);
        var plan = MakeExecutedPlan("p1", "HPG", entryPrice: 25m, stopLoss: 23m, createdAt: planCreated);
        var entry = MakeTrade(plan.Id, TradeType.BUY, qty: 100, price: 25m, date: planCreated);
        // Exit at 30 > entry → win
        var exit = MakeTrade(plan.Id, TradeType.SELL, qty: 100, price: 30m, date: DateTime.UtcNow.AddDays(-2));

        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry, exit });

        var result = await _handler.Handle(new GetDisciplineStreakQuery { UserId = UserId }, CancellationToken.None);

        result.HasData.Should().BeTrue();
        result.DaysWithoutViolation.Should().BeInRange(14, 16);
    }

    [Fact]
    public async Task Handle_ViolationToday_StreakIsZero()
    {
        var planCreated = DateTime.UtcNow.AddDays(-30);
        var plan = MakeExecutedPlan("p1", "FPT", entryPrice: 100m, stopLoss: 95m, createdAt: planCreated);
        var entry = MakeTrade(plan.Id, TradeType.BUY, qty: 100, price: 100m, date: planCreated);
        // Exit hôm nay tại 90 < SL → violation right now → streak = 0.
        var exit = MakeTrade(plan.Id, TradeType.SELL, qty: 100, price: 90m, date: DateTime.UtcNow);

        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry, exit });

        var result = await _handler.Handle(new GetDisciplineStreakQuery { UserId = UserId }, CancellationToken.None);

        result.HasData.Should().BeTrue();
        result.DaysWithoutViolation.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MostRecentViolationDeterminesStreak()
    {
        // 2 plans, both with SL violation. Streak = days since the most recent one.
        var p1 = MakeExecutedPlan("p1", "FPT", 100m, 95m, DateTime.UtcNow.AddDays(-60));
        var p2 = MakeExecutedPlan("p2", "VNM", 80m, 76m, DateTime.UtcNow.AddDays(-40));

        var p1Entry = MakeTrade(p1.Id, TradeType.BUY, 100, 100m, DateTime.UtcNow.AddDays(-60));
        var p1Exit = MakeTrade(p1.Id, TradeType.SELL, 100, 88m, DateTime.UtcNow.AddDays(-30)); // older violation

        var p2Entry = MakeTrade(p2.Id, TradeType.BUY, 100, 80m, DateTime.UtcNow.AddDays(-40));
        var p2Exit = MakeTrade(p2.Id, TradeType.SELL, 100, 70m, DateTime.UtcNow.AddDays(-5));  // recent violation

        _planRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { p1, p2 });
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(p1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { p1Entry, p1Exit });
        _tradeRepo.Setup(r => r.GetByTradePlanIdAsync(p2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { p2Entry, p2Exit });

        var result = await _handler.Handle(new GetDisciplineStreakQuery { UserId = UserId }, CancellationToken.None);

        result.DaysWithoutViolation.Should().BeInRange(4, 6);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------
    private static TradePlan MakeReadyPlan(DateTime? createdAt = null)
    {
        var plan = new TradePlan(UserId, "VNM", "Buy", 80m, 75m, 90m, 100, accountBalance: 100_000_000m);
        plan.SetThesis("EPS Q1 +22% YoY");
        plan.MarkReady();
        if (createdAt.HasValue)
            typeof(TradePlan).GetProperty("CreatedAt")!.SetValue(plan, createdAt.Value);
        return plan;
    }

    private static TradePlan MakeExecutedPlan(string id, string symbol, decimal entryPrice, decimal stopLoss, DateTime createdAt)
    {
        var plan = new TradePlan(UserId, symbol, "Buy", entryPrice, stopLoss, entryPrice * 1.2m, 100, accountBalance: 100_000_000m);
        plan.SetThesis("Test thesis " + symbol);
        plan.MarkReady();
        // Force Executed status via reflection (no public Executed-with-trade convenience).
        typeof(TradePlan).GetProperty("Status")!.SetValue(plan, TradePlanStatus.Executed);
        typeof(TradePlan).GetProperty("Id")!.SetValue(plan, id);
        typeof(TradePlan).GetProperty("CreatedAt")!.SetValue(plan, createdAt);
        return plan;
    }

    private static Trade MakeTrade(string planId, TradeType type, decimal qty, decimal price, DateTime date)
    {
        var t = new Trade("portfolio-1", "FPT", type, qty, price, tradeDate: date);
        t.LinkTradePlan(planId);
        return t;
    }
}
