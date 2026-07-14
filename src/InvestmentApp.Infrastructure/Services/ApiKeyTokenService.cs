using System.Security.Cryptography;
using System.Text;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Token format: <c>imk_</c> + base64url(32 random bytes). Stored as SHA-256 hex; the display
/// prefix is <c>imk_</c> + the first 8 characters of the random part, enough to identify a key
/// in a list without revealing it.
/// </summary>
public class ApiKeyTokenService : IApiKeyTokenService
{
    private const string TokenPrefix = "imk_";
    private const int RandomByteLength = 32;
    private const int DisplayPrefixLength = 12; // "imk_" (4) + 8 chars

    public GeneratedApiKey Generate()
    {
        var random = Base64UrlEncode(RandomNumberGenerator.GetBytes(RandomByteLength));
        var plaintext = TokenPrefix + random;
        var hash = ComputeHash(plaintext);
        var displayPrefix = plaintext[..DisplayPrefixLength];
        return new GeneratedApiKey(plaintext, hash, displayPrefix);
    }

    public string ComputeHash(string plaintext)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
