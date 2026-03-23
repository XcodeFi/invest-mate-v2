using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class TradeJournalTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateTradeJournal()
    {
        // Arrange
        var tradeId = "trade-1";
        var userId = "user-1";
        var portfolioId = "portfolio-1";
        var entryReason = "Breakout above resistance";
        var marketContext = "Bullish trend";
        var technicalSetup = "Golden cross";
        var emotionalState = "Confident";
        var confidenceLevel = 7;

        // Act
        var journal = new TradeJournal(tradeId, userId, portfolioId,
            entryReason, marketContext, technicalSetup, emotionalState, confidenceLevel);

        // Assert
        journal.Id.Should().NotBeNullOrEmpty();
        journal.TradeId.Should().Be(tradeId);
        journal.UserId.Should().Be(userId);
        journal.PortfolioId.Should().Be(portfolioId);
        journal.EntryReason.Should().Be(entryReason);
        journal.MarketContext.Should().Be(marketContext);
        journal.TechnicalSetup.Should().Be(technicalSetup);
        journal.EmotionalState.Should().Be(emotionalState);
        journal.ConfidenceLevel.Should().Be(7);
        journal.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        journal.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        journal.IsDeleted.Should().BeFalse();
        journal.TradePlanId.Should().BeNull();
    }

    [Fact]
    public void Constructor_ValidParameters_ShouldInitializePostTradeFieldsToDefaults()
    {
        // Act
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Assert
        journal.PostTradeReview.Should().BeEmpty();
        journal.LessonsLearned.Should().BeEmpty();
        journal.Rating.Should().Be(0);
        journal.Tags.Should().BeEmpty();
    }

    #endregion

    #region Constructor — Null String Defaults

    [Fact]
    public void Constructor_NullEntryReason_ShouldDefaultToEmpty()
    {
        // Act
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            null!, null!, null!, null!, 5);

        // Assert
        journal.EntryReason.Should().BeEmpty();
        journal.MarketContext.Should().BeEmpty();
        journal.TechnicalSetup.Should().BeEmpty();
        journal.EmotionalState.Should().BeEmpty();
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullTradeId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new TradeJournal(null!, "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("tradeId");
    }

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new TradeJournal("trade-1", null!, "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullPortfolioId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new TradeJournal("trade-1", "user-1", null!,
            "reason", "context", "setup", "calm", 5);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("portfolioId");
    }

    #endregion

    #region Constructor — Confidence Level Clamping

    [Fact]
    public void Constructor_ConfidenceLevelBelowMin_ShouldClampTo1()
    {
        // Act
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", -5);

        // Assert
        journal.ConfidenceLevel.Should().Be(1);
    }

    [Fact]
    public void Constructor_ConfidenceLevelZero_ShouldClampTo1()
    {
        // Act
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 0);

        // Assert
        journal.ConfidenceLevel.Should().Be(1);
    }

    [Fact]
    public void Constructor_ConfidenceLevelAboveMax_ShouldClampTo10()
    {
        // Act
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 15);

        // Assert
        journal.ConfidenceLevel.Should().Be(10);
    }

    [Fact]
    public void Constructor_ConfidenceLevelAtMin_ShouldKeep1()
    {
        // Act
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 1);

        // Assert
        journal.ConfidenceLevel.Should().Be(1);
    }

    [Fact]
    public void Constructor_ConfidenceLevelAtMax_ShouldKeep10()
    {
        // Act
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 10);

        // Assert
        journal.ConfidenceLevel.Should().Be(10);
    }

    #endregion

    #region Update — Partial Updates

    [Fact]
    public void Update_OnlyEntryReason_ShouldUpdateOnlyThatField()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "original reason", "context", "setup", "calm", 5);
        var originalConfidence = journal.ConfidenceLevel;
        var originalMarketContext = journal.MarketContext;

        // Act
        journal.Update(entryReason: "updated reason");

        // Assert
        journal.EntryReason.Should().Be("updated reason");
        journal.MarketContext.Should().Be(originalMarketContext);
        journal.ConfidenceLevel.Should().Be(originalConfidence);
    }

    [Fact]
    public void Update_MultipleFields_ShouldUpdateAllProvided()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.Update(
            marketContext: "new context",
            technicalSetup: "new setup",
            emotionalState: "anxious",
            postTradeReview: "went well",
            lessonsLearned: "be patient");

        // Assert
        journal.MarketContext.Should().Be("new context");
        journal.TechnicalSetup.Should().Be("new setup");
        journal.EmotionalState.Should().Be("anxious");
        journal.PostTradeReview.Should().Be("went well");
        journal.LessonsLearned.Should().Be("be patient");
    }

    [Fact]
    public void Update_Tags_ShouldReplaceTagsList()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);
        var newTags = new List<string> { "breakout", "momentum" };

        // Act
        journal.Update(tags: newTags);

        // Assert
        journal.Tags.Should().BeEquivalentTo(newTags);
    }

    [Fact]
    public void Update_ShouldUpdateTimestampAndIncrementVersion()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);
        var initialVersion = journal.Version;

        // Act
        journal.Update(entryReason: "updated");

        // Assert
        journal.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        journal.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region Update — Confidence Level Clamping

    [Fact]
    public void Update_ConfidenceLevelBelowMin_ShouldClampTo1()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.Update(confidenceLevel: -3);

        // Assert
        journal.ConfidenceLevel.Should().Be(1);
    }

    [Fact]
    public void Update_ConfidenceLevelAboveMax_ShouldClampTo10()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.Update(confidenceLevel: 20);

        // Assert
        journal.ConfidenceLevel.Should().Be(10);
    }

    #endregion

    #region Update — Rating Clamping

    [Fact]
    public void Update_RatingWithinRange_ShouldSetRating()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.Update(rating: 4);

        // Assert
        journal.Rating.Should().Be(4);
    }

    [Fact]
    public void Update_RatingBelowMin_ShouldClampTo0()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.Update(rating: -2);

        // Assert
        journal.Rating.Should().Be(0);
    }

    [Fact]
    public void Update_RatingAboveMax_ShouldClampTo5()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.Update(rating: 10);

        // Assert
        journal.Rating.Should().Be(5);
    }

    [Fact]
    public void Update_RatingAtZero_ShouldKeep0()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.Update(rating: 0);

        // Assert
        journal.Rating.Should().Be(0);
    }

    [Fact]
    public void Update_RatingAtMax_ShouldKeep5()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.Update(rating: 5);

        // Assert
        journal.Rating.Should().Be(5);
    }

    #endregion

    #region LinkTradePlan

    [Fact]
    public void LinkTradePlan_ShouldSetTradePlanId()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.LinkTradePlan("plan-1");

        // Assert
        journal.TradePlanId.Should().Be("plan-1");
    }

    [Fact]
    public void LinkTradePlan_ShouldUpdateTimestamp()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.LinkTradePlan("plan-1");

        // Assert
        journal.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LinkTradePlan_CalledTwice_ShouldOverwriteWithLatest()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.LinkTradePlan("plan-1");
        journal.LinkTradePlan("plan-2");

        // Assert
        journal.TradePlanId.Should().Be("plan-2");
    }

    #endregion

    #region SoftDelete

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedToTrue()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);

        // Act
        journal.SoftDelete();

        // Assert
        journal.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_ShouldUpdateTimestampAndIncrementVersion()
    {
        // Arrange
        var journal = new TradeJournal("trade-1", "user-1", "portfolio-1",
            "reason", "context", "setup", "calm", 5);
        var initialVersion = journal.Version;

        // Act
        journal.SoftDelete();

        // Assert
        journal.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        journal.Version.Should().Be(initialVersion + 1);
    }

    #endregion
}
