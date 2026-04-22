using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Commands.RemoveDebt;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.PersonalFinance.Commands;

public class RemoveDebtCommandHandlerTests
{
    private readonly Mock<IFinancialProfileRepository> _repo = new();
    private readonly RemoveDebtCommandHandler _handler;

    public RemoveDebtCommandHandlerTests()
    {
        _handler = new RemoveDebtCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ValidRemoval_RemovesAndPersists()
    {
        // Only debts with Principal = 0 (paid off) can be deleted per domain rule.
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        var debt = profile.UpsertDebt(null, DebtType.CreditCard, "Đã trả xong", 0m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        await _handler.Handle(new RemoveDebtCommand { UserId = "u1", DebtId = debt.Id }, CancellationToken.None);

        profile.Debts.Should().NotContain(d => d.Id == debt.Id);
        _repo.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPositivePrincipal_ThrowsAndDoesNotPersist()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        var debt = profile.UpsertDebt(null, DebtType.CreditCard, "Còn nợ", 5_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var act = () => _handler.Handle(new RemoveDebtCommand { UserId = "u1", DebtId = debt.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FinancialProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProfileNotExist_Throws()
    {
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);

        var act = () => _handler.Handle(new RemoveDebtCommand { UserId = "u1", DebtId = "any" }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FinancialProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
