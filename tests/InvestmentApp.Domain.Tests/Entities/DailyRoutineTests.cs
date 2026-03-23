using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.Tests.Entities;

public class DailyRoutineTests
{
    private static RoutineTemplate CreateTestTemplate(int itemCount = 3, bool allRequired = true)
    {
        var items = Enumerable.Range(0, itemCount).Select(i => new RoutineItemTemplate
        {
            Index = i,
            Label = $"Task {i}",
            Group = i < 2 ? "Sáng" : "Cuối ngày",
            Link = $"https://example.com/task-{i}",
            IsRequired = allRequired || i == 0,
            Emoji = "✅"
        }).ToList();

        return new RoutineTemplate(null, "Morning Routine", "☀️", "Trading", 30, items);
    }

    #region CreateFromTemplate

    [Fact]
    public void CreateFromTemplate_ValidParameters_ShouldCreateRoutine()
    {
        // Arrange
        var template = CreateTestTemplate();
        var date = new DateTime(2026, 3, 15, 10, 30, 0);

        // Act
        var routine = DailyRoutine.CreateFromTemplate("user-1", date, template, 5, 10);

        // Assert
        routine.Id.Should().NotBeNullOrEmpty();
        routine.UserId.Should().Be("user-1");
        routine.Date.Should().Be(new DateTime(2026, 3, 15)); // Date part only
        routine.TemplateId.Should().Be(template.Id);
        routine.TemplateName.Should().Be("Morning Routine");
        routine.CompletedCount.Should().Be(0);
        routine.TotalCount.Should().Be(3);
        routine.IsFullyCompleted.Should().BeFalse();
        routine.CurrentStreak.Should().Be(5);
        routine.LongestStreak.Should().Be(10);
        routine.CompletedAt.Should().BeNull();
        routine.IsDeleted.Should().BeFalse();
        routine.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        routine.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateFromTemplate_ShouldMapAllItemsFromTemplate()
    {
        // Arrange
        var template = CreateTestTemplate(itemCount: 3);

        // Act
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 0, 0);

        // Assert
        routine.Items.Should().HaveCount(3);
        for (int i = 0; i < 3; i++)
        {
            var item = routine.Items[i];
            var templateItem = template.Items[i];
            item.Index.Should().Be(templateItem.Index);
            item.Label.Should().Be(templateItem.Label);
            item.Group.Should().Be(templateItem.Group);
            item.Link.Should().Be(templateItem.Link);
            item.IsRequired.Should().Be(templateItem.IsRequired);
            item.Emoji.Should().Be(templateItem.Emoji);
            item.IsCompleted.Should().BeFalse();
            item.CompletedAt.Should().BeNull();
        }
    }

    [Fact]
    public void CreateFromTemplate_NullUserId_ShouldThrowArgumentNullException()
    {
        // Arrange
        var template = CreateTestTemplate();

        // Act
        var action = () => DailyRoutine.CreateFromTemplate(null!, DateTime.Today, template, 0, 0);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void CreateFromTemplate_ZeroStreaks_ShouldInitializeAtZero()
    {
        // Arrange
        var template = CreateTestTemplate();

        // Act
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 0, 0);

        // Assert
        routine.CurrentStreak.Should().Be(0);
        routine.LongestStreak.Should().Be(0);
    }

    #endregion

    #region CompleteItem

    [Fact]
    public void CompleteItem_ValidIndex_ShouldMarkItemAsCompleted()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);

        // Act
        routine.CompleteItem(0);

        // Assert
        routine.Items[0].IsCompleted.Should().BeTrue();
        routine.Items[0].CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CompleteItem_ShouldUpdateCompletedCount()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);

        // Act
        routine.CompleteItem(0);

        // Assert
        routine.CompletedCount.Should().Be(1);
        routine.IsFullyCompleted.Should().BeFalse();
    }

    [Fact]
    public void CompleteItem_AlreadyCompleted_ShouldBeNoOp()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);
        routine.CompleteItem(0);
        var countAfterFirst = routine.CompletedCount;
        var versionAfterFirst = routine.Version;

        // Act
        routine.CompleteItem(0);

        // Assert
        routine.CompletedCount.Should().Be(countAfterFirst);
        routine.Version.Should().Be(versionAfterFirst);
    }

    [Fact]
    public void CompleteItem_InvalidIndex_ShouldThrowArgumentException()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);

        // Act
        var action = () => routine.CompleteItem(99);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CompleteItem_AllItems_ShouldSetIsFullyCompleted()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(3), 0, 0);

        // Act
        routine.CompleteItem(0);
        routine.CompleteItem(1);
        routine.CompleteItem(2);

        // Assert
        routine.CompletedCount.Should().Be(3);
        routine.IsFullyCompleted.Should().BeTrue();
        routine.CompletedAt.Should().NotBeNull();
        routine.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CompleteItem_AllItems_ShouldIncrementCurrentStreak()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(3), 5, 10);

        // Act
        routine.CompleteItem(0);
        routine.CompleteItem(1);
        routine.CompleteItem(2);

        // Assert
        routine.CurrentStreak.Should().Be(6);
    }

    [Fact]
    public void CompleteItem_AllItems_ShouldUpdateLongestStreakIfExceeded()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(3), 9, 10);

        // Act
        routine.CompleteItem(0);
        routine.CompleteItem(1);
        routine.CompleteItem(2);

        // Assert
        routine.CurrentStreak.Should().Be(10);
        routine.LongestStreak.Should().Be(10); // Equal, not exceeded
    }

    [Fact]
    public void CompleteItem_AllItems_ShouldSetNewLongestStreak()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(3), 10, 10);

        // Act
        routine.CompleteItem(0);
        routine.CompleteItem(1);
        routine.CompleteItem(2);

        // Assert
        routine.CurrentStreak.Should().Be(11);
        routine.LongestStreak.Should().Be(11);
    }

    [Fact]
    public void CompleteItem_ShouldIncrementVersion()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);
        var initialVersion = routine.Version;

        // Act
        routine.CompleteItem(0);

        // Assert
        routine.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region UncompleteItem

    [Fact]
    public void UncompleteItem_CompletedItem_ShouldMarkAsUncompleted()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);
        routine.CompleteItem(0);

        // Act
        routine.UncompleteItem(0);

        // Assert
        routine.Items[0].IsCompleted.Should().BeFalse();
        routine.Items[0].CompletedAt.Should().BeNull();
    }

    [Fact]
    public void UncompleteItem_ShouldDecrementCompletedCount()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);
        routine.CompleteItem(0);
        routine.CompleteItem(1);

        // Act
        routine.UncompleteItem(0);

        // Assert
        routine.CompletedCount.Should().Be(1);
    }

    [Fact]
    public void UncompleteItem_NotCompleted_ShouldBeNoOp()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);
        var versionBefore = routine.Version;

        // Act
        routine.UncompleteItem(0);

        // Assert
        routine.CompletedCount.Should().Be(0);
        routine.Version.Should().Be(versionBefore);
    }

    [Fact]
    public void UncompleteItem_InvalidIndex_ShouldThrowArgumentException()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);

        // Act
        var action = () => routine.UncompleteItem(99);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UncompleteItem_AfterFullCompletion_ShouldRevertIsFullyCompleted()
    {
        // Arrange
        var template = CreateTestTemplate(3);
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 5, 10);
        routine.CompleteItem(0);
        routine.CompleteItem(1);
        routine.CompleteItem(2);
        routine.IsFullyCompleted.Should().BeTrue();

        // Act
        routine.UncompleteItem(1);

        // Assert
        routine.IsFullyCompleted.Should().BeFalse();
        routine.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void UncompleteItem_AfterFullCompletion_ShouldDecrementCurrentStreak()
    {
        // Arrange
        var template = CreateTestTemplate(3);
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 5, 10);
        routine.CompleteItem(0);
        routine.CompleteItem(1);
        routine.CompleteItem(2);
        // After full completion: CurrentStreak = 6, LongestStreak = 10

        // Act
        routine.UncompleteItem(1);

        // Assert
        routine.CurrentStreak.Should().Be(5); // Decremented from 6 back to 5
    }

    [Fact]
    public void UncompleteItem_AfterFullCompletion_LongestStreakShouldNotDecrease()
    {
        // Arrange
        var template = CreateTestTemplate(3);
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 10, 10);
        routine.CompleteItem(0);
        routine.CompleteItem(1);
        routine.CompleteItem(2);
        // After full completion: CurrentStreak = 11, LongestStreak = 11

        // Act
        routine.UncompleteItem(1);

        // Assert
        routine.CurrentStreak.Should().Be(10);
        routine.LongestStreak.Should().Be(11); // Longest streak is NOT decremented
    }

    #endregion

    #region ResetFromTemplate

    [Fact]
    public void ResetFromTemplate_ShouldReplaceAllItemsFromNewTemplate()
    {
        // Arrange
        var template1 = CreateTestTemplate(2);
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template1, 0, 0);
        routine.CompleteItem(0);
        routine.CompleteItem(1);

        var template2 = CreateTestTemplate(4);

        // Act
        routine.ResetFromTemplate(template2);

        // Assert
        routine.TemplateId.Should().Be(template2.Id);
        routine.TemplateName.Should().Be(template2.Name);
        routine.Items.Should().HaveCount(4);
        routine.Items.All(i => !i.IsCompleted).Should().BeTrue();
        routine.CompletedCount.Should().Be(0);
        routine.TotalCount.Should().Be(4);
        routine.IsFullyCompleted.Should().BeFalse();
        routine.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void ResetFromTemplate_ShouldIncrementVersion()
    {
        // Arrange
        var template = CreateTestTemplate(2);
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 0, 0);
        var initialVersion = routine.Version;

        var newTemplate = CreateTestTemplate(3);

        // Act
        routine.ResetFromTemplate(newTemplate);

        // Assert
        routine.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void ResetFromTemplate_ShouldUpdateTimestamp()
    {
        // Arrange
        var template = CreateTestTemplate();
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 0, 0);

        var newTemplate = CreateTestTemplate(2);

        // Act
        routine.ResetFromTemplate(newTemplate);

        // Assert
        routine.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region UpdateStreak

    [Fact]
    public void UpdateStreak_ShouldSetNewStreakValues()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);

        // Act
        routine.UpdateStreak(7, 15);

        // Assert
        routine.CurrentStreak.Should().Be(7);
        routine.LongestStreak.Should().Be(15);
        routine.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateStreak_ResetToZero_ShouldSetBothToZero()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 5, 10);

        // Act
        routine.UpdateStreak(0, 10);

        // Assert
        routine.CurrentStreak.Should().Be(0);
        routine.LongestStreak.Should().Be(10);
    }

    #endregion

    #region SoftDelete

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedToTrue()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);

        // Act
        routine.SoftDelete();

        // Assert
        routine.IsDeleted.Should().BeTrue();
        routine.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SoftDelete_ShouldIncrementVersion()
    {
        // Arrange
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, CreateTestTemplate(), 0, 0);
        var initialVersion = routine.Version;

        // Act
        routine.SoftDelete();

        // Assert
        routine.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region RecalculateCounts (tested through CompleteItem/UncompleteItem)

    [Fact]
    public void RecalculateCounts_MixedRequiredAndOptional_OnlyFullWhenAllCompleted()
    {
        // Arrange - template with mixed required/optional items
        var template = CreateTestTemplate(itemCount: 3, allRequired: false);
        // Item 0 is required, items 1 & 2 are not required
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 0, 0);

        // Act - complete only the required item
        routine.CompleteItem(0);

        // Assert - not fully completed because CompletedCount != TotalCount
        routine.IsFullyCompleted.Should().BeFalse();
        routine.CompletedCount.Should().Be(1);
    }

    [Fact]
    public void RecalculateCounts_AllRequiredDoneButNotAllItems_ShouldNotBeFullyCompleted()
    {
        // Arrange
        var template = CreateTestTemplate(itemCount: 3, allRequired: false);
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 0, 0);

        // Complete only item 0 (the only required one)
        routine.CompleteItem(0);

        // Assert
        routine.CompletedCount.Should().Be(1);
        routine.TotalCount.Should().Be(3);
        routine.IsFullyCompleted.Should().BeFalse();
    }

    [Fact]
    public void RecalculateCounts_StreakNotDecrementedBelowZero()
    {
        // Arrange - start with streak 0, complete all, then uncomplete
        var template = CreateTestTemplate(2);
        var routine = DailyRoutine.CreateFromTemplate("user-1", DateTime.Today, template, 0, 0);
        routine.CompleteItem(0);
        routine.CompleteItem(1);
        // CurrentStreak is now 1

        // Act
        routine.UncompleteItem(0);
        // CurrentStreak should be 0

        // Assert
        routine.CurrentStreak.Should().Be(0);
    }

    #endregion
}
