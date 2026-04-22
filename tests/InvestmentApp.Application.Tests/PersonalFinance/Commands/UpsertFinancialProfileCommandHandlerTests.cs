using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Commands.UpsertFinancialProfile;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.PersonalFinance.Commands;

public class UpsertFinancialProfileCommandHandlerTests
{
    private readonly Mock<IFinancialProfileRepository> _repo = new();
    private readonly UpsertFinancialProfileCommandHandler _handler;

    public UpsertFinancialProfileCommandHandlerTests()
    {
        _handler = new UpsertFinancialProfileCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_NewUser_CreatesWithDefaults()
    {
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);
        _repo.Setup(r => r.GetByUserIdIncludingDeletedAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);

        var cmd = new UpsertFinancialProfileCommand { UserId = "u1", MonthlyExpense = 15_000_000m };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.UserId.Should().Be("u1");
        result.MonthlyExpense.Should().Be(15_000_000m);
        result.Accounts.Should().HaveCount(4); // 4 default accounts
        result.Rules.MaxInvestmentPercent.Should().Be(50m);
        _repo.Verify(r => r.AddAsync(It.IsAny<FinancialProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NewUser_MissingMonthlyExpense_Throws()
    {
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);
        _repo.Setup(r => r.GetByUserIdIncludingDeletedAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);

        var cmd = new UpsertFinancialProfileCommand { UserId = "u1" }; // no monthly expense
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_ExistingUser_UpdatesMonthlyExpense()
    {
        var existing = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var cmd = new UpsertFinancialProfileCommand { UserId = "u1", MonthlyExpense = 20_000_000m };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.MonthlyExpense.Should().Be(20_000_000m);
        _repo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.AddAsync(It.IsAny<FinancialProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingUser_PartialRuleUpdate()
    {
        var existing = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var cmd = new UpsertFinancialProfileCommand { UserId = "u1", MaxInvestmentPercent = 60m };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Rules.MaxInvestmentPercent.Should().Be(60m);
        result.Rules.EmergencyFundMonths.Should().Be(6); // unchanged
        result.Rules.MinSavingsPercent.Should().Be(30m); // unchanged
    }

    [Fact]
    public async Task Handle_SoftDeletedProfile_RestoresAndUpdates()
    {
        var deleted = FinancialProfile.Create("u1", 10_000_000m);
        deleted.SoftDelete();
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);
        _repo.Setup(r => r.GetByUserIdIncludingDeletedAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(deleted);

        var cmd = new UpsertFinancialProfileCommand { UserId = "u1", MonthlyExpense = 25_000_000m };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        deleted.IsDeleted.Should().BeFalse();
        result.MonthlyExpense.Should().Be(25_000_000m);
        _repo.Verify(r => r.UpdateAsync(deleted, It.IsAny<CancellationToken>()), Times.Once);
    }
}
