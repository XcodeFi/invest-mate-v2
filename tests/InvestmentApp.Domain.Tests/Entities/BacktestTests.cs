using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class BacktestTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateBacktest()
    {
        // Arrange
        var userId = "user-1";
        var strategyId = "strategy-1";
        var name = "MA Crossover Backtest";
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var initialCapital = 100_000_000m;

        // Act
        var backtest = new Backtest(userId, strategyId, name, startDate, endDate, initialCapital);

        // Assert
        backtest.Id.Should().NotBeNullOrEmpty();
        backtest.UserId.Should().Be(userId);
        backtest.StrategyId.Should().Be(strategyId);
        backtest.Name.Should().Be(name);
        backtest.StartDate.Should().Be(startDate);
        backtest.EndDate.Should().Be(endDate);
        backtest.InitialCapital.Should().Be(initialCapital);
        backtest.Status.Should().Be(BacktestStatus.Pending);
        backtest.Result.Should().BeNull();
        backtest.SimulatedTrades.Should().BeEmpty();
        backtest.ErrorMessage.Should().BeNull();
        backtest.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        backtest.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Backtest(null!, "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullStrategyId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Backtest("user-1", null!, "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("strategyId");
    }

    [Fact]
    public void Constructor_NullName_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Backtest("user-1", "strategy-1", null!,
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_EndDateBeforeStartDate_ShouldThrowArgumentException()
    {
        // Arrange
        var startDate = new DateTime(2025, 12, 31);
        var endDate = new DateTime(2025, 1, 1);

        // Act
        var action = () => new Backtest("user-1", "strategy-1", "Test",
            startDate, endDate, 100_000_000m);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EndDateEqualToStartDate_ShouldThrowArgumentException()
    {
        // Arrange
        var sameDate = new DateTime(2025, 6, 15);

        // Act
        var action = () => new Backtest("user-1", "strategy-1", "Test",
            sameDate, sameDate, 100_000_000m);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ZeroInitialCapital_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Backtest("user-1", "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 0m);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeInitialCapital_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Backtest("user-1", "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), -50_000_000m);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region MarkRunning

    [Fact]
    public void MarkRunning_ShouldSetStatusToRunning()
    {
        // Arrange
        var backtest = new Backtest("user-1", "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);

        // Act
        backtest.MarkRunning();

        // Assert
        backtest.Status.Should().Be(BacktestStatus.Running);
    }

    [Fact]
    public void MarkRunning_ShouldUpdateTimestampAndIncrementVersion()
    {
        // Arrange
        var backtest = new Backtest("user-1", "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);
        var initialVersion = backtest.Version;

        // Act
        backtest.MarkRunning();

        // Assert
        backtest.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        backtest.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region Complete

    [Fact]
    public void Complete_ShouldSetResultAndTrades()
    {
        // Arrange
        var backtest = new Backtest("user-1", "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);
        backtest.MarkRunning();

        var result = new BacktestResult
        {
            FinalValue = 120_000_000m,
            TotalReturn = 0.20m,
            WinRate = 0.65m,
            TotalTrades = 50,
            WinningTrades = 33,
            LosingTrades = 17
        };
        var trades = new List<SimulatedTrade>
        {
            new SimulatedTrade
            {
                Symbol = "VNM",
                EntryPrice = 80000m,
                ExitPrice = 90000m,
                Quantity = 100,
                PnL = 1_000_000m
            }
        };

        // Act
        backtest.Complete(result, trades);

        // Assert
        backtest.Status.Should().Be(BacktestStatus.Completed);
        backtest.Result.Should().BeSameAs(result);
        backtest.SimulatedTrades.Should().HaveCount(1);
        backtest.SimulatedTrades[0].Symbol.Should().Be("VNM");
    }

    [Fact]
    public void Complete_ShouldUpdateTimestampAndIncrementVersion()
    {
        // Arrange
        var backtest = new Backtest("user-1", "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);
        backtest.MarkRunning();
        var versionBeforeComplete = backtest.Version;

        // Act
        backtest.Complete(new BacktestResult(), new List<SimulatedTrade>());

        // Assert
        backtest.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        backtest.Version.Should().Be(versionBeforeComplete + 1);
    }

    #endregion

    #region Fail

    [Fact]
    public void Fail_ShouldSetErrorMessageAndStatusToFailed()
    {
        // Arrange
        var backtest = new Backtest("user-1", "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);
        backtest.MarkRunning();

        // Act
        backtest.Fail("Insufficient data for the given period");

        // Assert
        backtest.Status.Should().Be(BacktestStatus.Failed);
        backtest.ErrorMessage.Should().Be("Insufficient data for the given period");
    }

    [Fact]
    public void Fail_ShouldUpdateTimestampAndIncrementVersion()
    {
        // Arrange
        var backtest = new Backtest("user-1", "strategy-1", "Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);
        backtest.MarkRunning();
        var versionBeforeFail = backtest.Version;

        // Act
        backtest.Fail("Error occurred");

        // Assert
        backtest.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        backtest.Version.Should().Be(versionBeforeFail + 1);
    }

    #endregion

    #region Full Lifecycle

    [Fact]
    public void FullLifecycle_PendingToRunningToCompleted_ShouldTransitionCorrectly()
    {
        // Arrange
        var backtest = new Backtest("user-1", "strategy-1", "MA Cross Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);
        backtest.Status.Should().Be(BacktestStatus.Pending);

        // Act — Mark Running
        backtest.MarkRunning();
        backtest.Status.Should().Be(BacktestStatus.Running);

        // Act — Complete
        var result = new BacktestResult { FinalValue = 110_000_000m, TotalReturn = 0.10m };
        backtest.Complete(result, new List<SimulatedTrade>());

        // Assert
        backtest.Status.Should().Be(BacktestStatus.Completed);
        backtest.Result.Should().NotBeNull();
        backtest.ErrorMessage.Should().BeNull();
        backtest.Version.Should().Be(2); // MarkRunning + Complete
    }

    [Fact]
    public void FullLifecycle_PendingToRunningToFailed_ShouldTransitionCorrectly()
    {
        // Arrange
        var backtest = new Backtest("user-1", "strategy-1", "MA Cross Test",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), 100_000_000m);

        // Act
        backtest.MarkRunning();
        backtest.Fail("Timeout");

        // Assert
        backtest.Status.Should().Be(BacktestStatus.Failed);
        backtest.ErrorMessage.Should().Be("Timeout");
        backtest.Result.Should().BeNull();
        backtest.Version.Should().Be(2); // MarkRunning + Fail
    }

    #endregion
}
