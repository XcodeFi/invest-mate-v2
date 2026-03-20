using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Stores user's AI assistant configuration: encrypted API keys (Claude + Gemini), preferred provider/model, usage stats.
/// </summary>
public class AiSettings : AggregateRoot
{
    public string UserId { get; private set; }
    public string Provider { get; private set; } = "claude";

    /// <summary>Claude API key. BsonElement keeps backward compat with existing "EncryptedApiKey" field in MongoDB.</summary>
    [BsonElement("EncryptedApiKey")]
    public string EncryptedClaudeApiKey { get; private set; }

    public string? EncryptedGeminiApiKey { get; private set; }
    public string Model { get; private set; }
    public long TotalInputTokens { get; private set; }
    public long TotalOutputTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public AiSettings() { }

    public static AiSettings Create(string userId, string encryptedApiKey, string provider = "claude", string model = "claude-sonnet-4-6-20250514")
    {
        var settings = new AiSettings
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId ?? throw new ArgumentNullException(nameof(userId)),
            Provider = provider,
            Model = model,
            TotalInputTokens = 0,
            TotalOutputTokens = 0,
            EstimatedCostUsd = 0m,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (provider == "gemini")
            settings.EncryptedGeminiApiKey = encryptedApiKey ?? throw new ArgumentNullException(nameof(encryptedApiKey));
        else
            settings.EncryptedClaudeApiKey = encryptedApiKey ?? throw new ArgumentNullException(nameof(encryptedApiKey));

        return settings;
    }

    public void UpdateProvider(string provider)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateClaudeApiKey(string encryptedApiKey)
    {
        EncryptedClaudeApiKey = encryptedApiKey ?? throw new ArgumentNullException(nameof(encryptedApiKey));
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateGeminiApiKey(string encryptedApiKey)
    {
        EncryptedGeminiApiKey = encryptedApiKey ?? throw new ArgumentNullException(nameof(encryptedApiKey));
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public string? GetActiveEncryptedApiKey() =>
        Provider == "gemini" ? EncryptedGeminiApiKey : EncryptedClaudeApiKey;

    public void UpdateModel(string model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void AddTokenUsage(long inputTokens, long outputTokens, decimal costUsd)
    {
        TotalInputTokens += inputTokens;
        TotalOutputTokens += outputTokens;
        EstimatedCostUsd += costUsd;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
