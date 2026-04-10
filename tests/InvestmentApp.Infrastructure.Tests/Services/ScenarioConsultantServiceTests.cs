using FluentAssertions;
using Moq;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class ScenarioConsultantServiceTests
{
    private readonly Mock<ITechnicalIndicatorService> _technicalSvc;
    private readonly ScenarioConsultantService _service;

    public ScenarioConsultantServiceTests()
    {
        _technicalSvc = new Mock<ITechnicalIndicatorService>();
        _service = new ScenarioConsultantService(_technicalSvc.Object);
    }

    // ── Shared test data ────────────────────────────────────────────────────

    private static TechnicalAnalysisResult BuildResult(string symbol = "HPG") => new()
    {
        Symbol = symbol,
        AnalyzedAt = DateTime.UtcNow,
        DataPoints = 120,
        CurrentPrice = 75_000m,
        Ema20 = 73_000m,
        Ema50 = 70_000m,
        Ema200 = 65_000m,
        Rsi14 = 55m,
        RsiSignal = "neutral",
        MacdLine = 500m,
        SignalLine = 300m,
        MacdSignal = "buy",
        BollingerUpper = 82_000m,
        BollingerMiddle = 75_000m,
        BollingerLower = 68_000m,
        Atr14 = 2_500m,
        SupportLevels = new List<decimal> { 72_000m, 68_000m, 65_000m },
        ResistanceLevels = new List<decimal> { 80_000m, 85_000m, 90_000m },
        Fibonacci = new FibonacciLevels
        {
            SwingHigh = 92_000m,
            SwingLow = 60_000m,
            Retracement236 = 84_432m,
            Retracement382 = 79_752m,
            Retracement500 = 76_000m,
            Retracement618 = 72_248m,
            Retracement786 = 66_808m,
            Extension1272 = 100_704m,
            Extension1618 = 111_816m
        },
        OverallSignal = "buy"
    };

    // ── Test 1: Short-term → TakeProfit node at nearest resistance ──────────

    [Fact]
    public async Task SuggestAsync_Short_GeneratesTakeProfitAtNearestResistance()
    {
        // Arrange
        var result = BuildResult();
        _technicalSvc.Setup(s => s.AnalyzeAsync("HPG", 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var suggestion = await _service.SuggestAsync("HPG", 75_000m, TimeHorizon.ShortTerm);

        // Assert
        suggestion.Symbol.Should().Be("HPG");
        suggestion.EntryPrice.Should().Be(75_000m);
        suggestion.TimeHorizon.Should().Be(TimeHorizon.ShortTerm);

        var takeProfitNodes = suggestion.Nodes
            .Where(n => n.Category == "TakeProfit")
            .OrderBy(n => n.Order)
            .ToList();

        takeProfitNodes.Should().NotBeEmpty("short-term should produce at least one TP node");

        // First TP node should be at the nearest resistance above entry (80,000)
        var firstTp = takeProfitNodes.First();
        firstTp.ConditionType.Should().Be("PriceAbove");
        firstTp.ConditionValue.Should().Be(80_000m);
        firstTp.ActionType.Should().Be("SellPercent");
        firstTp.ActionValue.Should().Be(30m);

        // Reasoning must mention at least one indicator source
        firstTp.Reasoning.Should().NotBeNullOrEmpty();
        firstTp.Reasoning.Should().ContainAny("kháng cự", "resistance", "Fib", "Bollinger", "EMA");
    }

    // ── Test 2: Short-term → StopLoss node below nearest support ────────────

    [Fact]
    public async Task SuggestAsync_Short_GeneratesStopLossBelowNearestSupport()
    {
        // Arrange
        var result = BuildResult();
        _technicalSvc.Setup(s => s.AnalyzeAsync("HPG", 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var suggestion = await _service.SuggestAsync("HPG", 75_000m, TimeHorizon.ShortTerm);

        // Assert
        var stopLossNode = suggestion.Nodes.FirstOrDefault(n => n.Category == "StopLoss");
        stopLossNode.Should().NotBeNull("should always have a stop-loss node");

        stopLossNode!.ConditionType.Should().Be("PriceBelow");
        // Stop loss should be below the nearest support (72,000)
        stopLossNode.ConditionValue.Should().BeLessThan(75_000m);
        stopLossNode.ActionType.Should().Be("SellAll");

        // Reasoning should describe the support / structure break
        stopLossNode.Reasoning.Should().NotBeNullOrEmpty();
        stopLossNode.Reasoning.Should().ContainAny("hỗ trợ", "support", "Fib", "EMA200", "cấu trúc");
    }

    // ── Test 3: Medium-term uses EMA200 + Fibonacci for confluence ──────────

    [Fact]
    public async Task SuggestAsync_Medium_UsesEma200AndFibonacciForConfluence()
    {
        // Arrange
        var result = BuildResult();
        _technicalSvc.Setup(s => s.AnalyzeAsync("HPG", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var suggestion = await _service.SuggestAsync("HPG", 75_000m, TimeHorizon.MediumTerm);

        // Assert
        suggestion.TechnicalBasis.Ema200.Should().Be(65_000m);
        suggestion.TechnicalBasis.Fibonacci.Should().NotBeNull();
        suggestion.TechnicalBasis.Fibonacci!.Extension1618.Should().Be(111_816m);

        // Medium-term should have at least one node mentioning Fib extension or EMA200
        var nodesMentioningFibOrEma = suggestion.Nodes
            .Where(n => n.Reasoning.Contains("Fib") || n.Reasoning.Contains("EMA200") || n.Reasoning.Contains("EMA 200"))
            .ToList();
        nodesMentioningFibOrEma.Should().NotBeEmpty("medium-term nodes should reference Fibonacci or EMA200");
    }

    // ── Test 4: Each node has non-empty reasoning with at least 1 indicator ─

    [Fact]
    public async Task SuggestAsync_AllNodes_HaveReasoningWithIndicatorSource()
    {
        // Arrange
        var result = BuildResult();
        _technicalSvc.Setup(s => s.AnalyzeAsync("HPG", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var suggestion = await _service.SuggestAsync("HPG", 75_000m, TimeHorizon.ShortTerm);

        // Assert
        suggestion.Nodes.Should().NotBeEmpty();
        foreach (var node in suggestion.Nodes)
        {
            node.Reasoning.Should().NotBeNullOrWhiteSpace(
                $"node '{node.Label}' (category: {node.Category}) must have non-empty reasoning");

            // At least one indicator keyword must appear in reasoning
            var hasIndicator = node.Reasoning.ContainsAny(
                "kháng cự", "hỗ trợ", "Fib", "EMA", "RSI", "Bollinger",
                "ATR", "MACD", "resistance", "support", "confluence", "thời gian");
            hasIndicator.Should().BeTrue(
                $"node '{node.Label}' reasoning must reference at least one indicator: '{node.Reasoning}'");
        }
    }

    // ── Test 5: Confluence scoring — zone with ≥ 2 aligned indicators ───────

    [Fact]
    public async Task SuggestAsync_ConfluenceZone_HasHigherPriorityOrder()
    {
        // Arrange: resistance at 80,000 and Bollinger upper at 80,000 = confluence
        var result = BuildResult();
        result.ResistanceLevels = new List<decimal> { 80_000m, 85_000m };
        result.BollingerUpper = 80_500m;  // Bollinger near first resistance → confluence
        result.Fibonacci = new FibonacciLevels
        {
            SwingHigh = 92_000m,
            SwingLow = 60_000m,
            Retracement236 = 84_432m,
            Retracement382 = 79_800m,  // Fib 38.2% near 80,000 → triple confluence
            Retracement500 = 76_000m,
            Retracement618 = 72_248m,
            Retracement786 = 66_808m,
            Extension1272 = 100_704m,
            Extension1618 = 111_816m
        };

        _technicalSvc.Setup(s => s.AnalyzeAsync("HPG", 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var suggestion = await _service.SuggestAsync("HPG", 75_000m, TimeHorizon.ShortTerm);

        // Assert: the confluence TP node (80k zone) should come first (Order = 0)
        var tpNodes = suggestion.Nodes
            .Where(n => n.Category == "TakeProfit")
            .OrderBy(n => n.Order)
            .ToList();

        tpNodes.Should().NotBeEmpty();
        var firstTp = tpNodes.First();

        // The confluence zone (80k) should be first TP
        firstTp.ConditionValue.Should().BeInRange(79_000m, 81_000m,
            "confluence zone (resistance + Bollinger + Fib) should rank first");

        // Reasoning should mention multiple indicators (confluence)
        var indicatorCount = new[]
            { "kháng cự", "Bollinger", "Fib", "EMA", "hỗ trợ", "resistance" }
            .Count(kw => firstTp.Reasoning.Contains(kw, StringComparison.OrdinalIgnoreCase));
        indicatorCount.Should().BeGreaterThanOrEqualTo(2,
            $"confluence node should reference ≥ 2 indicators. Reasoning: '{firstTp.Reasoning}'");
    }

    // ── Test 6: Fallback when no technical data available ───────────────────

    [Fact]
    public async Task SuggestAsync_NoTechnicalData_ReturnsFallbackWithEmptyNodes()
    {
        // Arrange: AnalyzeAsync returns null (simulating no data)
        _technicalSvc.Setup(s => s.AnalyzeAsync("XYZ", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TechnicalAnalysisResult)null!);

        // Act
        var suggestion = await _service.SuggestAsync("XYZ", 50_000m, TimeHorizon.ShortTerm);

        // Assert
        suggestion.Should().NotBeNull();
        suggestion.Symbol.Should().Be("XYZ");
        suggestion.Nodes.Should().BeEmpty("no technical data → no suggestions");

        // TechnicalBasis should reflect empty state
        suggestion.TechnicalBasis.SupportLevels.Should().BeEmpty();
        suggestion.TechnicalBasis.ResistanceLevels.Should().BeEmpty();
    }
}

// ── String extension helper ─────────────────────────────────────────────────

internal static class StringTestExtensions
{
    internal static bool ContainsAny(this string text, params string[] keywords)
        => keywords.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
}
