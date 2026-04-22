using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Queries.GetNetWorthSummary;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.PersonalFinance.Queries;

public class GetNetWorthSummaryQueryHandlerTests
{
    private readonly Mock<IFinancialProfileRepository> _profileRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IPnLService> _pnl = new();
    private readonly GetNetWorthSummaryQueryHandler _handler;

    public GetNetWorthSummaryQueryHandlerTests()
    {
        _handler = new GetNetWorthSummaryQueryHandler(_profileRepo.Object, _portfolioRepo.Object, _pnl.Object);
    }

    [Fact]
    public async Task Handle_ProfileNotExist_ReturnsEmptySummary_WithHasProfileFalse()
    {
        _profileRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);

        var result = await _handler.Handle(new GetNetWorthSummaryQuery { UserId = "u1" }, CancellationToken.None);

        result.Should().NotBeNull();
        result.HasProfile.Should().BeFalse();
        result.TotalAssets.Should().Be(0m);
        result.HealthScore.Should().Be(0);
        result.Accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProfileExists_SetsHasProfileTrue()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _profileRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Portfolio>());

        var result = await _handler.Handle(new GetNetWorthSummaryQuery { UserId = "u1" }, CancellationToken.None);

        result.HasProfile.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ProfileWithPortfolios_SumsSecuritiesValue()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _profileRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var p1 = new Portfolio("u1", "P1", 100_000_000m);
        var p2 = new Portfolio("u1", "P2", 200_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { p1, p2 });

        // Note: PortfolioPnLSummary.TotalMarketValue is a computed alias for TotalPortfolioValue (see PnLModels.cs).
        // Handler reads TotalMarketValue; setting TotalPortfolioValue is the way to mock this.
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync(p1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary { TotalPortfolioValue = 150_000_000m });
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync(p2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary { TotalPortfolioValue = 250_000_000m });

        var result = await _handler.Handle(new GetNetWorthSummaryQuery { UserId = "u1" }, CancellationToken.None);

        result.SecuritiesValue.Should().Be(400_000_000m); // 150 + 250
        // Default profile has all 4 seeded accounts with 0 balance → totalAssets = 0 + securitiesValue
        result.TotalAssets.Should().Be(400_000_000m);
        // Securities 400/400 = 100% vượt cap 50% + savings 0/120 (30%) + emergency 0/60 → score bị trừ → score >= 0 but < 100
        result.HealthScore.Should().BeLessThan(100);
    }

    [Fact]
    public async Task Handle_ProfileWithGoldAccount_IncludesGoldInTotal()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        profile.UpsertAccount(null, FinancialAccountType.Gold, "SJC", 340_000_000m,
            goldBrand: GoldBrand.SJC, goldType: GoldType.Mieng, goldQuantity: 2m);
        _profileRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Portfolio>());

        var result = await _handler.Handle(new GetNetWorthSummaryQuery { UserId = "u1" }, CancellationToken.None);

        result.GoldTotal.Should().Be(340_000_000m);
        result.SecuritiesValue.Should().Be(0m);
        result.TotalAssets.Should().Be(340_000_000m);
    }

    [Fact]
    public async Task Handle_ReturnsRuleChecks()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _profileRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Portfolio>());

        var result = await _handler.Handle(new GetNetWorthSummaryQuery { UserId = "u1" }, CancellationToken.None);

        result.RuleChecks.Should().HaveCount(3); // Emergency + Investment + Savings
        result.RuleChecks.Select(r => r.RuleName).Should().Contain(new[] { "EmergencyFund", "InvestmentCap", "SavingsFloor" });
    }
}
