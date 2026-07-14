using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// A per-user personal access token for non-interactive API access (e.g. the local NPU
/// assistant pulling the daily digest). Only the SHA-256 hash of the token is stored — the
/// plaintext is shown to the user exactly once at creation. Accepted only on endpoints that
/// opt into the ApiKey scheme, never blanket the API.
/// </summary>
public class ApiKey : AggregateRoot
{
    public string UserId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string KeyHash { get; private set; } = null!;
    public string Prefix { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    [BsonConstructor]
    public ApiKey() { }

    public static ApiKey Create(string userId, string name, string keyHash, string prefix, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(keyHash)) throw new ArgumentException("KeyHash is required.", nameof(keyHash));
        if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("Prefix is required.", nameof(prefix));

        var now = DateTime.UtcNow;
        if (expiresAt <= now) throw new ArgumentException("ExpiresAt must be in the future.", nameof(expiresAt));

        return new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = name.Trim(),
            KeyHash = keyHash,
            Prefix = prefix,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Active = not revoked and not past expiry.</summary>
    public bool IsActive(DateTime asOf) => RevokedAt is null && asOf < ExpiresAt;

    public void Revoke()
    {
        if (RevokedAt is not null) return;
        RevokedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkUsed(DateTime asOf)
    {
        LastUsedAt = asOf;
    }
}
