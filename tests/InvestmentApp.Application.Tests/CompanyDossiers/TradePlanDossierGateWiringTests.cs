using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

public class TradePlanDossierGateWiringTests
{
    private readonly Mock<ICompanyDossierGate> _gate = new();

    [Fact]
    public async Task Create_WhenGateBlocks_ShouldThrowBeforePersisting()
    {
        _gate.Setup(g => g.EnsureAsync("user-1", "HPG", It.IsAny<decimal>(),
                It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HPG",
                DossierGateResult.Fail("missing")));

        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out var repo);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
        repo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithStatusExecuted_ShouldStillRunGateFirst()
    {
        _gate.Setup(g => g.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HPG",
                DossierGateResult.Fail("missing")));

        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out var repo);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");
        command.Status = "Executed";
        command.TradeId = "trade-1";

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
        // Assert "có throw" là không đủ: mock gate luôn throw nên test pass y như nhau
        // dù gate chạy trước hay sau khối auto-transition. Phải chứng minh plan chưa
        // bao giờ được ghi xuống, đó mới là bằng chứng gate chặn TRƯỚC.
        repo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenGatePasses_ShouldPassPlanSizeAndBalance()
    {
        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out _);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");
        command.Quantity = 100;
        command.EntryPrice = 80_000m;
        command.AccountBalance = 100_000_000m;

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "HPG",
            8_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenSizeCrossesThresholdUpward_ShouldRunGateWithNewSize()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 120, entryPrice: 100_000m);

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "HPG",
            12_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenSizeStaysBelowThreshold_ShouldNotRunGate()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 30, entryPrice: 100_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_WhenPlanAlreadyAboveThreshold_ShouldNotReRunGate()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 120, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 130, entryPrice: 100_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_WhenAccountBalanceNull_ShouldNotRunGateRegardlessOfSize()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: null);
        var command = TestFactory.UpdateCommand(quantity: 1000, entryPrice: 1_000_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    private static class TestFactory
    {
        public static CreateTradePlanCommandHandler CreateTradePlanHandler(
            ICompanyDossierGate gate, out Mock<ITradePlanRepository> repository)
        {
            repository = new Mock<ITradePlanRepository>();
            return new CreateTradePlanCommandHandler(
                repository.Object,
                Mock.Of<ITradeRepository>(),
                gate);
        }

        public static CreateTradePlanCommand CreateCommand(string userId, string symbol) => new()
        {
            UserId = userId,
            Symbol = symbol,
            Direction = "Buy",
            EntryPrice = 10_000m,
            StopLoss = 9_000m,
            Target = 12_000m,
            Quantity = 100
        };

        public static UpdateTradePlanCommandHandler UpdateTradePlanHandler(
            ICompanyDossierGate gate, int existingQuantity, decimal existingEntryPrice, decimal? accountBalance)
        {
            var repository = new Mock<ITradePlanRepository>();
            var plan = new TradePlan("user-1", "HPG", "Buy",
                existingEntryPrice, existingEntryPrice * 0.9m, existingEntryPrice * 1.2m, existingQuantity,
                accountBalance: accountBalance,
                thesis: "Thesis đủ dài để không vướng gate kỷ luật thesis khi test luồng sửa plan.");
            repository.Setup(r => r.GetByIdAsync("plan-1", It.IsAny<CancellationToken>())).ReturnsAsync(plan);
            return new UpdateTradePlanCommandHandler(repository.Object, gate);
        }

        public static UpdateTradePlanCommand UpdateCommand(int quantity, decimal entryPrice) => new()
        {
            Id = "plan-1",
            UserId = "user-1",
            Quantity = quantity,
            EntryPrice = entryPrice
        };
    }
}
