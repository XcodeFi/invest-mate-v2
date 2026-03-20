using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Stores user's AI assistant configuration: encrypted API key, preferred model, usage stats.
/// </summary>
public class AiSettings : AggregateRoot
{
    public string UserId { get; private set; }
    public string EncryptedApiKey { get; private set; }
    public string Model { get; private set; }
    public long TotalInputTokens { get; private set; }
    public long TotalOutputTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public AiSettings() { }

    public static AiSettings Create(string userId, string encryptedApiKey, string model = "claude-sonnet-4-6-20250514")
    {
        return new AiSettings
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId ?? throw new ArgumentNullException(nameof(userId)),
            EncryptedApiKey = encryptedApiKey ?? throw new ArgumentNullException(nameof(encryptedApiKey)),
            Model = model,
            TotalInputTokens = 0,
            TotalOutputTokens = 0,
            EstimatedCostUsd = 0m,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateApiKey(string encryptedApiKey)
    {
        EncryptedApiKey = encryptedApiKey ?? throw new ArgumentNullException(nameof(encryptedApiKey));
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

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
