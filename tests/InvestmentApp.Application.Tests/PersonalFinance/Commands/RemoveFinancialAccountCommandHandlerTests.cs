using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Commands.RemoveFinancialAccount;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.PersonalFinance.Commands;

public class RemoveFinancialAccountCommandHandlerTests
{
    private readonly Mock<IFinancialProfileRepository> _repo = new();
    private readonly RemoveFinancialAccountCommandHandler _handler;

    public RemoveFinancialAccountCommandHandlerTests()
    {
        _handler = new RemoveFinancialAccountCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ValidRemoval_RemovesAndPersists()
    {
        // Only accounts with zero balance can be deleted per domain rule.
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        var goldAccount = profile.UpsertAccount(null, FinancialAccountType.Gold, "SJC Miếng", 0m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        await _handler.Handle(new RemoveFinancialAccountCommand { UserId = "u1", AccountId = goldAccount.Id }, CancellationToken.None);

        profile.Accounts.Should().NotContain(a => a.Id == goldAccount.Id);
        _repo.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProfileNotExist_Throws()
    {
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);

        var act = () => _handler.Handle(new RemoveFinancialAccountCommand { UserId = "u1", AccountId = "any" }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FinancialProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
