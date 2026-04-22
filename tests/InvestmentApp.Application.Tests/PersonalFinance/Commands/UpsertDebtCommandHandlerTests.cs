using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Commands.UpsertDebt;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.PersonalFinance.Commands;

public class UpsertDebtCommandHandlerTests
{
    private readonly Mock<IFinancialProfileRepository> _repo = new();
    private readonly UpsertDebtCommandHandler _handler;

    public UpsertDebtCommandHandlerTests()
    {
        _handler = new UpsertDebtCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ProfileNotExist_Throws()
    {
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);

        var cmd = new UpsertDebtCommand
        {
            UserId = "u1",
            Type = DebtType.CreditCard,
            Name = "x",
            Principal = 1m,
        };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FinancialProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NewDebt_AddsAndPersists()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new UpsertDebtCommand
        {
            UserId = "u1",
            Type = DebtType.CreditCard,
            Name = "VCB Platinum",
            Principal = 15_000_000m,
            InterestRate = 28m,
            MonthlyPayment = 500_000m,
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Name.Should().Be("VCB Platinum");
        result.Principal.Should().Be(15_000_000m);
        result.InterestRate.Should().Be(28m);
        profile.Debts.Should().HaveCount(1);
        _repo.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateExistingDebt_UpdatesInPlace()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        var existing = profile.UpsertDebt(null, DebtType.Mortgage, "Vay nhà", 2_000_000_000m, interestRate: 9m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new UpsertDebtCommand
        {
            UserId = "u1",
            DebtId = existing.Id,
            Type = DebtType.Mortgage,
            Name = "Vay nhà BIDV",
            Principal = 1_800_000_000m,
            InterestRate = 9.5m,
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Id.Should().Be(existing.Id);
        result.Principal.Should().Be(1_800_000_000m);
        profile.Debts.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NegativePrincipal_ThrowsAndDoesNotPersist()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new UpsertDebtCommand
        {
            UserId = "u1",
            Type = DebtType.CreditCard,
            Name = "x",
            Principal = -1m,
        };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FinancialProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UpdateNonExistentDebtId_ThrowsAndDoesNotPersist()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        profile.UpsertDebt(null, DebtType.CreditCard, "Thật", 1_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new UpsertDebtCommand
        {
            UserId = "u1",
            DebtId = "does-not-exist",
            Type = DebtType.CreditCard,
            Name = "x",
            Principal = 1m,
        };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FinancialProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
