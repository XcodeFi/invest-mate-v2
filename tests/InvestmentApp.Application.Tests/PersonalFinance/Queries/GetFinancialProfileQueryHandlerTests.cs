using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Queries.GetFinancialProfile;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.PersonalFinance.Queries;

public class GetFinancialProfileQueryHandlerTests
{
    private readonly Mock<IFinancialProfileRepository> _repo = new();
    private readonly GetFinancialProfileQueryHandler _handler;

    public GetFinancialProfileQueryHandlerTests()
    {
        _handler = new GetFinancialProfileQueryHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ProfileExists_ReturnsDto()
    {
        var profile = FinancialProfile.Create("u1", 15_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var result = await _handler.Handle(new GetFinancialProfileQuery { UserId = "u1" }, CancellationToken.None);

        result.Should().NotBeNull();
        result!.UserId.Should().Be("u1");
        result.MonthlyExpense.Should().Be(15_000_000m);
        result.Accounts.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_ProfileNotExist_ReturnsNull()
    {
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);

        var result = await _handler.Handle(new GetFinancialProfileQuery { UserId = "u1" }, CancellationToken.None);

        result.Should().BeNull();
    }
}
