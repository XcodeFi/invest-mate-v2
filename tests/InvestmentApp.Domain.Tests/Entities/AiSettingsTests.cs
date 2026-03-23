using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class AiSettingsTests
{
    #region Create

    [Fact]
    public void Create_ForClaude_ShouldSetClaudeApiKey()
    {
        // Act
        var settings = AiSettings.Create("user-1", "encrypted-key-claude", "claude", "claude-sonnet-4-6-20250514");

        // Assert
        settings.Id.Should().NotBeNullOrEmpty();
        settings.UserId.Should().Be("user-1");
        settings.Provider.Should().Be("claude");
        settings.Model.Should().Be("claude-sonnet-4-6-20250514");
        settings.EncryptedClaudeApiKey.Should().Be("encrypted-key-claude");
        settings.EncryptedGeminiApiKey.Should().BeNull();
        settings.TotalInputTokens.Should().Be(0);
        settings.TotalOutputTokens.Should().Be(0);
        settings.EstimatedCostUsd.Should().Be(0m);
        settings.IsDeleted.Should().BeFalse();
        settings.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        settings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ForGemini_ShouldSetGeminiApiKey()
    {
        // Act
        var settings = AiSettings.Create("user-1", "encrypted-key-gemini", "gemini", "gemini-2.0-flash");

        // Assert
        settings.Provider.Should().Be("gemini");
        settings.Model.Should().Be("gemini-2.0-flash");
        settings.EncryptedGeminiApiKey.Should().Be("encrypted-key-gemini");
        settings.EncryptedClaudeApiKey.Should().BeNull();
    }

    [Fact]
    public void Create_DefaultParameters_ShouldUseClaudeDefaults()
    {
        // Act
        var settings = AiSettings.Create("user-1", "encrypted-key");

        // Assert
        settings.Provider.Should().Be("claude");
        settings.Model.Should().Be("claude-sonnet-4-6-20250514");
        settings.EncryptedClaudeApiKey.Should().Be("encrypted-key");
    }

    [Fact]
    public void Create_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => AiSettings.Create(null!, "encrypted-key");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Create_NullEncryptedApiKey_ForClaude_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => AiSettings.Create("user-1", null!, "claude");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("encryptedApiKey");
    }

    [Fact]
    public void Create_NullEncryptedApiKey_ForGemini_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => AiSettings.Create("user-1", null!, "gemini");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("encryptedApiKey");
    }

    #endregion

    #region UpdateProvider

    [Fact]
    public void UpdateProvider_ValidProvider_ShouldUpdateProviderAndTimestamp()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "encrypted-key");

        // Act
        settings.UpdateProvider("gemini");

        // Assert
        settings.Provider.Should().Be("gemini");
        settings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateProvider_ShouldIncrementVersion()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "encrypted-key");
        var initialVersion = settings.Version;

        // Act
        settings.UpdateProvider("gemini");

        // Assert
        settings.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void UpdateProvider_NullProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "encrypted-key");

        // Act
        var action = () => settings.UpdateProvider(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("provider");
    }

    #endregion

    #region UpdateClaudeApiKey

    [Fact]
    public void UpdateClaudeApiKey_ValidKey_ShouldUpdateKey()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "old-key");

        // Act
        settings.UpdateClaudeApiKey("new-encrypted-key");

        // Assert
        settings.EncryptedClaudeApiKey.Should().Be("new-encrypted-key");
        settings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateClaudeApiKey_ShouldIncrementVersion()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "old-key");
        var initialVersion = settings.Version;

        // Act
        settings.UpdateClaudeApiKey("new-key");

        // Assert
        settings.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void UpdateClaudeApiKey_NullKey_ShouldThrowArgumentNullException()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "old-key");

        // Act
        var action = () => settings.UpdateClaudeApiKey(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("encryptedApiKey");
    }

    #endregion

    #region UpdateGeminiApiKey

    [Fact]
    public void UpdateGeminiApiKey_ValidKey_ShouldUpdateKey()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "claude-key");

        // Act
        settings.UpdateGeminiApiKey("gemini-encrypted-key");

        // Assert
        settings.EncryptedGeminiApiKey.Should().Be("gemini-encrypted-key");
        settings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateGeminiApiKey_ShouldIncrementVersion()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "claude-key");
        var initialVersion = settings.Version;

        // Act
        settings.UpdateGeminiApiKey("gemini-key");

        // Assert
        settings.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void UpdateGeminiApiKey_NullKey_ShouldThrowArgumentNullException()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "claude-key");

        // Act
        var action = () => settings.UpdateGeminiApiKey(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("encryptedApiKey");
    }

    #endregion

    #region GetActiveEncryptedApiKey

    [Fact]
    public void GetActiveEncryptedApiKey_ClaudeProvider_ShouldReturnClaudeKey()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "claude-key", "claude");

        // Act
        var activeKey = settings.GetActiveEncryptedApiKey();

        // Assert
        activeKey.Should().Be("claude-key");
    }

    [Fact]
    public void GetActiveEncryptedApiKey_GeminiProvider_ShouldReturnGeminiKey()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "gemini-key", "gemini");

        // Act
        var activeKey = settings.GetActiveEncryptedApiKey();

        // Assert
        activeKey.Should().Be("gemini-key");
    }

    [Fact]
    public void GetActiveEncryptedApiKey_SwitchProvider_ShouldReturnCorrectKey()
    {
        // Arrange — start as claude, add gemini key, switch to gemini
        var settings = AiSettings.Create("user-1", "claude-key", "claude");
        settings.UpdateGeminiApiKey("gemini-key");
        settings.UpdateProvider("gemini");

        // Act
        var activeKey = settings.GetActiveEncryptedApiKey();

        // Assert
        activeKey.Should().Be("gemini-key");
    }

    #endregion

    #region UpdateModel

    [Fact]
    public void UpdateModel_ValidModel_ShouldUpdateModel()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");

        // Act
        settings.UpdateModel("claude-opus-4-6-20250514");

        // Assert
        settings.Model.Should().Be("claude-opus-4-6-20250514");
        settings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateModel_ShouldIncrementVersion()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");
        var initialVersion = settings.Version;

        // Act
        settings.UpdateModel("claude-opus-4-6-20250514");

        // Assert
        settings.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void UpdateModel_NullModel_ShouldThrowArgumentNullException()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");

        // Act
        var action = () => settings.UpdateModel(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    #endregion

    #region AddTokenUsage

    [Fact]
    public void AddTokenUsage_ShouldAccumulateTokensAndCost()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");

        // Act
        settings.AddTokenUsage(1000, 500, 0.05m);

        // Assert
        settings.TotalInputTokens.Should().Be(1000);
        settings.TotalOutputTokens.Should().Be(500);
        settings.EstimatedCostUsd.Should().Be(0.05m);
    }

    [Fact]
    public void AddTokenUsage_MultipleCalls_ShouldAccumulate()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");

        // Act
        settings.AddTokenUsage(1000, 500, 0.05m);
        settings.AddTokenUsage(2000, 800, 0.10m);
        settings.AddTokenUsage(500, 200, 0.02m);

        // Assert
        settings.TotalInputTokens.Should().Be(3500);
        settings.TotalOutputTokens.Should().Be(1500);
        settings.EstimatedCostUsd.Should().Be(0.17m);
    }

    [Fact]
    public void AddTokenUsage_ShouldUpdateTimestamp()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");

        // Act
        settings.AddTokenUsage(100, 50, 0.01m);

        // Assert
        settings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region SoftDelete and Restore

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrue()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");

        // Act
        settings.SoftDelete();

        // Assert
        settings.IsDeleted.Should().BeTrue();
        settings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SoftDelete_ShouldIncrementVersion()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");
        var initialVersion = settings.Version;

        // Act
        settings.SoftDelete();

        // Assert
        settings.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void Restore_ShouldSetIsDeletedFalse()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");
        settings.SoftDelete();

        // Act
        settings.Restore();

        // Assert
        settings.IsDeleted.Should().BeFalse();
        settings.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Restore_ShouldIncrementVersion()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");
        settings.SoftDelete();
        var versionAfterDelete = settings.Version;

        // Act
        settings.Restore();

        // Assert
        settings.Version.Should().Be(versionAfterDelete + 1);
    }

    [Fact]
    public void SoftDeleteAndRestore_FullCycle_ShouldReturnToOriginalState()
    {
        // Arrange
        var settings = AiSettings.Create("user-1", "key");
        settings.IsDeleted.Should().BeFalse();

        // Act
        settings.SoftDelete();
        settings.IsDeleted.Should().BeTrue();

        settings.Restore();

        // Assert
        settings.IsDeleted.Should().BeFalse();
        settings.Version.Should().Be(2); // SoftDelete +1, Restore +1
    }

    #endregion
}
