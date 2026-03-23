using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.Tests.Entities;

public class WatchlistTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateWatchlist()
    {
        // Arrange
        var userId = "user-1";
        var name = "My Watchlist";

        // Act
        var watchlist = new Watchlist(userId, name);

        // Assert
        watchlist.Id.Should().NotBeNullOrEmpty();
        watchlist.UserId.Should().Be(userId);
        watchlist.Name.Should().Be(name);
        watchlist.Emoji.Should().Be("⭐");
        watchlist.IsDefault.Should().BeFalse();
        watchlist.SortOrder.Should().Be(0);
        watchlist.Items.Should().BeEmpty();
        watchlist.IsDeleted.Should().BeFalse();
        watchlist.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        watchlist.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithAllParameters_ShouldCreateWatchlist()
    {
        // Act
        var watchlist = new Watchlist("user-1", "VN30 Stocks", "🔥", true, 5);

        // Assert
        watchlist.Name.Should().Be("VN30 Stocks");
        watchlist.Emoji.Should().Be("🔥");
        watchlist.IsDefault.Should().BeTrue();
        watchlist.SortOrder.Should().Be(5);
    }

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Watchlist(null!, "Name");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullName_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Watchlist("user-1", null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    #endregion

    #region UpdateInfo

    [Fact]
    public void UpdateInfo_ValidParameters_ShouldUpdateWatchlist()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "Old Name");
        var initialVersion = watchlist.Version;

        // Act
        watchlist.UpdateInfo("New Name", "🚀", 3);

        // Assert
        watchlist.Name.Should().Be("New Name");
        watchlist.Emoji.Should().Be("🚀");
        watchlist.SortOrder.Should().Be(3);
        watchlist.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        watchlist.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void UpdateInfo_NullName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "Name");

        // Act
        var action = () => watchlist.UpdateInfo(null!, "⭐", 0);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    #endregion

    #region AddItem

    [Fact]
    public void AddItem_ValidSymbol_ShouldAddItemToWatchlist()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.AddItem("VNM", "Blue chip", 80000m, 95000m);

        // Assert
        watchlist.Items.Should().HaveCount(1);
        var item = watchlist.Items.First();
        item.Symbol.Should().Be("VNM");
        item.Note.Should().Be("Blue chip");
        item.TargetBuyPrice.Should().Be(80000m);
        item.TargetSellPrice.Should().Be(95000m);
        item.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AddItem_LowercaseSymbol_ShouldNormalizeToUpperCase()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.AddItem("vnm");

        // Assert
        watchlist.Items.First().Symbol.Should().Be("VNM");
    }

    [Fact]
    public void AddItem_SymbolWithWhitespace_ShouldTrimAndNormalize()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.AddItem("  fpt  ");

        // Assert
        watchlist.Items.First().Symbol.Should().Be("FPT");
    }

    [Fact]
    public void AddItem_DuplicateSymbol_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM");

        // Act
        var action = () => watchlist.AddItem("VNM");

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddItem_DuplicateSymbolDifferentCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM");

        // Act
        var action = () => watchlist.AddItem("vnm");

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddItem_ShouldIncrementVersion()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        var initialVersion = watchlist.Version;

        // Act
        watchlist.AddItem("VNM");

        // Assert
        watchlist.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void AddItem_WithNullOptionalParams_ShouldAddItemSuccessfully()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.AddItem("FPT");

        // Assert
        var item = watchlist.Items.First();
        item.Symbol.Should().Be("FPT");
        item.Note.Should().BeNull();
        item.TargetBuyPrice.Should().BeNull();
        item.TargetSellPrice.Should().BeNull();
    }

    #endregion

    #region UpdateItem

    [Fact]
    public void UpdateItem_ExistingSymbol_ShouldUpdateItem()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM", "Old note", 80000m, 90000m);

        // Act
        watchlist.UpdateItem("VNM", "New note", 75000m, 100000m);

        // Assert
        var item = watchlist.Items.First();
        item.Note.Should().Be("New note");
        item.TargetBuyPrice.Should().Be(75000m);
        item.TargetSellPrice.Should().Be(100000m);
    }

    [Fact]
    public void UpdateItem_CaseInsensitiveSymbol_ShouldUpdateItem()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM");

        // Act
        watchlist.UpdateItem("vnm", "Updated note", null, null);

        // Assert
        watchlist.Items.First().Note.Should().Be("Updated note");
    }

    [Fact]
    public void UpdateItem_NonExistentSymbol_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        var action = () => watchlist.UpdateItem("VNM", "Note", null, null);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateItem_ShouldIncrementVersion()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM");
        var versionAfterAdd = watchlist.Version;

        // Act
        watchlist.UpdateItem("VNM", "note", null, null);

        // Assert
        watchlist.Version.Should().Be(versionAfterAdd + 1);
    }

    #endregion

    #region RemoveItem

    [Fact]
    public void RemoveItem_ExistingSymbol_ShouldRemoveItem()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM");
        watchlist.AddItem("FPT");

        // Act
        watchlist.RemoveItem("VNM");

        // Assert
        watchlist.Items.Should().HaveCount(1);
        watchlist.Items.First().Symbol.Should().Be("FPT");
    }

    [Fact]
    public void RemoveItem_CaseInsensitiveSymbol_ShouldRemoveItem()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM");

        // Act
        watchlist.RemoveItem("vnm");

        // Assert
        watchlist.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_NonExistentSymbol_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        var action = () => watchlist.RemoveItem("VNM");

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveItem_ShouldIncrementVersion()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM");
        var versionAfterAdd = watchlist.Version;

        // Act
        watchlist.RemoveItem("VNM");

        // Assert
        watchlist.Version.Should().Be(versionAfterAdd + 1);
    }

    #endregion

    #region AddBulkItems

    [Fact]
    public void AddBulkItems_ValidSymbols_ShouldAddAllItems()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.AddBulkItems(new[] { "VNM", "FPT", "MWG" });

        // Assert
        watchlist.Items.Should().HaveCount(3);
        watchlist.Items.Select(i => i.Symbol).Should().BeEquivalentTo("VNM", "FPT", "MWG");
    }

    [Fact]
    public void AddBulkItems_WithDuplicatesInInput_ShouldSkipDuplicates()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.AddBulkItems(new[] { "VNM", "FPT", "vnm" });

        // Assert
        watchlist.Items.Should().HaveCount(2);
        watchlist.Items.Select(i => i.Symbol).Should().BeEquivalentTo("VNM", "FPT");
    }

    [Fact]
    public void AddBulkItems_WithExistingItems_ShouldSkipExisting()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        watchlist.AddItem("VNM", "Existing note");

        // Act
        watchlist.AddBulkItems(new[] { "VNM", "FPT", "MWG" });

        // Assert
        watchlist.Items.Should().HaveCount(3);
        // Existing item should retain its original data
        watchlist.Items.First(i => i.Symbol == "VNM").Note.Should().Be("Existing note");
    }

    [Fact]
    public void AddBulkItems_NormalizesSymbols_ShouldUpperCaseAndTrim()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.AddBulkItems(new[] { " vnm ", "fpt", " MWG" });

        // Assert
        watchlist.Items.Select(i => i.Symbol).Should().BeEquivalentTo("VNM", "FPT", "MWG");
    }

    [Fact]
    public void AddBulkItems_ShouldIncrementVersion()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        var initialVersion = watchlist.Version;

        // Act
        watchlist.AddBulkItems(new[] { "VNM", "FPT" });

        // Assert
        watchlist.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region MarkAsDeleted

    [Fact]
    public void MarkAsDeleted_ShouldSetIsDeletedToTrue()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.MarkAsDeleted();

        // Assert
        watchlist.IsDeleted.Should().BeTrue();
        watchlist.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkAsDeleted_ShouldIncrementVersion()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");
        var initialVersion = watchlist.Version;

        // Act
        watchlist.MarkAsDeleted();

        // Assert
        watchlist.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void MarkAsDeleted_CalledTwice_ShouldRemainDeleted()
    {
        // Arrange
        var watchlist = new Watchlist("user-1", "My List");

        // Act
        watchlist.MarkAsDeleted();
        watchlist.MarkAsDeleted();

        // Assert
        watchlist.IsDeleted.Should().BeTrue();
    }

    #endregion
}
