namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Generates and hashes API-key tokens. The plaintext is returned to the caller exactly once
/// (at creation); only <see cref="GeneratedApiKey.Hash"/> is persisted.
/// </summary>
public interface IApiKeyTokenService
{
    GeneratedApiKey Generate();

    /// <summary>SHA-256 hex of a presented plaintext token, for lookup during authentication.</summary>
    string ComputeHash(string plaintext);
}

public record GeneratedApiKey(string Plaintext, string Hash, string Prefix);
