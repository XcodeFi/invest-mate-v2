using FluentAssertions;
using InvestmentApp.Application.Analytics.Queries.GetSavingsComparison;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Moq;

namespace InvestmentApp.Application.Tests.Analytics.Queries;

public class GetSavingsComparisonQueryHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IFinancialProfileRepository> _profileRepo = new();
    private readonly Mock<ICapitalFlowRepository> _flowRepo = new();
    private readonly Mock<IPnLService> _pnlService = new();
    private readonly Mock<IPerformanceMetricsService> _perfService = new();
    private readonly Mock<IHypotheticalSavingsReturnService> _hypotheticalService = new();

    private readonly GetSavingsComparisonQueryHandler _handler;

    public GetSavingsComparisonQueryHandlerTests()
    {
        var portfolio = new Portfolio("u1", "Test", 0m);
        // Force the Id to match what tests expect
        var portfolioIdField = typeof(Portfolio).GetProperty("Id")!;
        portfolioIdField.SetValue(portfolio, "p1");

        _portfolioRepo.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(portfolio);
        _pnlService.Setup(s => s.CalculatePortfolioPnLAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary { TotalPortfolioValue = 150_000_000m });
        _perfService.Setup(s => s.GetEquityCurveAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EquityCurveData { PortfolioId = "p1", Points = new() });
        _flowRepo.Setup(r => r.GetByPortfolioIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CapitalFlow>());
        _hypotheticalService.Setup(s => s.CalculateEndValue(It.IsAny<IReadOnlyList<CapitalFlow>>(), It.IsAny<decimal>(), It.IsAny<DateTime>()))
            .Returns(120_000_000m);

        _handler = new GetSavingsComparisonQueryHandler(
            _portfolioRepo.Object,
            _profileRepo.Object,
            _flowRepo.Object,
            _pnlService.Object,
            _perfService.Object,
            _hypotheticalService.Object);
    }

    [Fact]
    public async Task Handle_PortfolioNotFound_Throws()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "missing" };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_PortfolioOfDifferentUser_Throws()
    {
        var cmd = new GetSavingsComparisonQuery { UserId = "u-other", PortfolioId = "p1" };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*access denied*");
    }

    [Fact]
    public async Task Handle_UserSuppliedRate_UsesItAsManualSource()
    {
        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1", AnnualRate = 0.065m };

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.UsedRate.Should().Be(0.065m);
        result.RateSource.Should().Be("manual");
    }

    [Fact]
    public async Task Handle_NoRateProvided_NoSavingsAccounts_UsesFallback5Percent()
    {
        // profile null or has no savings
        _profileRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialProfile?)null);

        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.UsedRate.Should().Be(0.05m);
        result.RateSource.Should().Be("fallback-5");
    }

    [Fact]
    public async Task Handle_NoRateProvided_ComputesWeightedAvgFromSavingsAccounts()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 100_000_000m, interestRate: 6m);  // 100M @ 6%
        profile.UpsertAccount(null, FinancialAccountType.Savings, "Another", 50_000_000m, interestRate: 7m);           // 50M @ 7%
        // Weighted avg: (100*6 + 50*7) / 150 = 950/150 = 6.333...%

        _profileRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.RateSource.Should().Be("user-savings-avg");
        // Expected ~0.06333 decimal
        result.UsedRate.Should().BeApproximately(0.06333m, 0.001m);
        result.SavingsAccountsCounted.Should().Be(2);
        result.SavingsAccountsTotal.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SavingsAccountsButAllRateNull_UsesFallback5Percent_DiscloseZeroOfTotal()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 100_000_000m);  // no interestRate

        _profileRepo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.RateSource.Should().Be("fallback-5");
        result.SavingsAccountsCounted.Should().Be(0);
        result.SavingsAccountsTotal.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NegativeRate_ThrowsValidation()
    {
        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1", AnnualRate = -0.2m };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*sanity range*");
    }

    [Fact]
    public async Task Handle_RateAbove50Percent_ThrowsValidation()
    {
        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1", AnnualRate = 0.6m };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*sanity range*");
    }

    [Fact]
    public async Task Handle_WithFlows_FlowsReturned_ForClientSideRecompute()
    {
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow>
        {
            new("p1", "u1", CapitalFlowType.Deposit, 100_000_000m, "VND", null, day0),
            new("p1", "u1", CapitalFlowType.Withdraw, 20_000_000m, "VND", null, day0.AddDays(90)),
            new("p1", "u1", CapitalFlowType.Dividend, 5_000_000m, "VND", null, day0.AddDays(60)),  // filtered
        };
        _flowRepo.Setup(r => r.GetByPortfolioIdAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(flows);

        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1", AnnualRate = 0.05m, AsOf = day0.AddDays(365) };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Flows.Should().HaveCount(2);  // Dividend filtered
        result.Flows.Should().Contain(f => f.SignedAmount == 100_000_000m);
        result.Flows.Should().Contain(f => f.SignedAmount == -20_000_000m);
        result.FirstFlowDate.Should().Be(day0);
    }

    [Fact]
    public async Task Handle_DaysLessThan365_AlphaIsNull_PeriodDiffPresent()
    {
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow>
        {
            new("p1", "u1", CapitalFlowType.Deposit, 100_000_000m, "VND", null, day0),
        };
        _flowRepo.Setup(r => r.GetByPortfolioIdAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(flows);

        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1", AnnualRate = 0.05m, AsOf = day0.AddDays(180) };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.AlphaAnnualized.Should().BeNull();
        result.PeriodReturnDiff.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_DaysGreaterThan365_AlphaPresent_PeriodDiffNull()
    {
        var day0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var flows = new List<CapitalFlow>
        {
            new("p1", "u1", CapitalFlowType.Deposit, 100_000_000m, "VND", null, day0),
        };
        _flowRepo.Setup(r => r.GetByPortfolioIdAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(flows);

        var cmd = new GetSavingsComparisonQuery { UserId = "u1", PortfolioId = "p1", AnnualRate = 0.05m, AsOf = day0.AddDays(730) };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.AlphaAnnualized.Should().NotBeNull();
        result.PeriodReturnDiff.Should().BeNull();
        result.CagrActual.Should().NotBeNull();
    }
}
