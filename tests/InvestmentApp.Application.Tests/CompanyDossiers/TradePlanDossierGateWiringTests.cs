using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
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

        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out _);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");
        command.Status = "Executed";
        command.TradeId = "trade-1";

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
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
    }
}
