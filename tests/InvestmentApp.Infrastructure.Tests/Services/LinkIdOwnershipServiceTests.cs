using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Hai dịch vụ nhận id từ chỗ khác: bộ đánh giá cảnh báo (đọc snapshot theo rule.PortfolioId)
/// và hiệu suất chiến lược. Cả hai phải đối chiếu chủ sở hữu thay vì tin id.
/// </summary>
public class LinkIdOwnershipServiceTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    // ---------- AlertEvaluationService ----------

    private readonly Mock<IAlertRuleRepository> _ruleRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IPortfolioSnapshotRepository> _snapshotRepo = new();

    private readonly Portfolio _myPortfolio = new(Me, "Của tôi", 100_000_000m);
    private readonly Portfolio _foreignPortfolio = new(Other, "Của người khác", 100_000_000m);

    private AlertEvaluationService Evaluator()
    {
        foreach (var p in new[] { _myPortfolio, _foreignPortfolio })
            _portfolioRepo.Setup(r => r.GetByIdAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        return new AlertEvaluationService(
            _ruleRepo.Object, _portfolioRepo.Object, _snapshotRepo.Object,
            Mock.Of<IStockPriceRepository>(), Mock.Of<ITradeRepository>());
    }

    private static PortfolioSnapshotEntity Snapshot(string portfolioId, decimal total, int daysAgo = 0) =>
        new(portfolioId, DateTime.UtcNow.AddDays(-daysAgo), total, 0, total, 0, 0, 0, 0);

    private void SetupRule(string alertType, string portfolioId, string condition = "Above", decimal threshold = 1m)
    {
        var rule = new AlertRule(Me, "n", alertType, condition, threshold, "InApp", portfolioId);
        _ruleRepo.Setup(r => r.GetActiveByUserIdAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { rule });
    }

    [Fact]
    public async Task PortfolioValueAlert_ForeignPortfolio_NotTriggered_SnapshotNeverRead()
    {
        SetupRule("PortfolioValue", _foreignPortfolio.Id);
        _snapshotRepo.Setup(r => r.GetLatestByPortfolioIdAsync(_foreignPortfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snapshot(_foreignPortfolio.Id, 999_000_000m));

        var results = await Evaluator().EvaluateRulesAsync(Me);

        results.Should().BeEmpty();
        _snapshotRepo.Verify(r => r.GetLatestByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DrawdownAlert_ForeignPortfolio_NotTriggered_SnapshotsNeverRead()
    {
        SetupRule("DrawdownAlert", _foreignPortfolio.Id, threshold: 1m);
        _snapshotRepo.Setup(r => r.GetByPortfolioIdAsync(_foreignPortfolio.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Snapshot(_foreignPortfolio.Id, 100m, 2), Snapshot(_foreignPortfolio.Id, 50m, 1) });

        var results = await Evaluator().EvaluateRulesAsync(Me);

        results.Should().BeEmpty();
        _snapshotRepo.Verify(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PortfolioValueAlert_OwnPortfolio_StillTriggers()
    {
        SetupRule("PortfolioValue", _myPortfolio.Id);
        _snapshotRepo.Setup(r => r.GetLatestByPortfolioIdAsync(_myPortfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snapshot(_myPortfolio.Id, 5m));

        var results = (await Evaluator().EvaluateRulesAsync(Me)).ToList();

        results.Should().ContainSingle().Which.IsTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task DrawdownAlert_OwnPortfolio_StillTriggers()
    {
        SetupRule("DrawdownAlert", _myPortfolio.Id, threshold: 1m);
        _snapshotRepo.Setup(r => r.GetByPortfolioIdAsync(_myPortfolio.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Snapshot(_myPortfolio.Id, 100m, 2), Snapshot(_myPortfolio.Id, 50m, 1) });

        var results = (await Evaluator().EvaluateRulesAsync(Me)).ToList();

        results.Should().ContainSingle().Which.IsTriggered.Should().BeTrue();
    }

    // ---------- StrategyPerformanceService ----------

    private readonly Mock<IStrategyRepository> _strategyRepo = new();

    private StrategyPerformanceService Performance()
    {
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _myPortfolio });
        return new StrategyPerformanceService(
            _strategyRepo.Object, Mock.Of<ITradeRepository>(), _portfolioRepo.Object, Mock.Of<IStockPriceService>());
    }

    private Strategy AddStrategy(string owner)
    {
        var s = new Strategy(owner, $"Chiến lược của {owner}", "d", "e", "x", "r", "D1", "Trending");
        _strategyRepo.Setup(r => r.GetByIdAsync(s.Id, It.IsAny<CancellationToken>())).ReturnsAsync(s);
        return s;
    }

    [Fact]
    public async Task GetPerformance_ForeignStrategy_Throws_NameNotReturned()
    {
        var foreign = AddStrategy(Other);

        var act = () => Performance().GetPerformanceAsync(foreign.Id, Me);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetPerformance_OwnStrategy_ReturnsSummary()
    {
        var own = AddStrategy(Me);

        var summary = await Performance().GetPerformanceAsync(own.Id, Me);

        summary.StrategyName.Should().Be(own.Name);
    }

    [Fact]
    public async Task CompareStrategies_ForeignStrategy_Throws()
    {
        var own = AddStrategy(Me);
        var foreign = AddStrategy(Other);

        var act = () => Performance().CompareStrategiesAsync(new[] { own.Id, foreign.Id }, Me);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
