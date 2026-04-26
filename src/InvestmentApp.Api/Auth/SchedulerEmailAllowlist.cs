namespace InvestmentApp.Api.Auth;

/// <summary>
/// Holds the set of Google service-account emails that are allowed to invoke
/// <c>/internal/jobs/*</c> endpoints. Sourced from <c>Jobs:AllowedSchedulerSAs</c>
/// (comma-separated). Empty config = fail-closed (no caller is allowed).
/// </summary>
public sealed class SchedulerEmailAllowlist
{
    private readonly HashSet<string> _emails;

    public SchedulerEmailAllowlist(string? config)
    {
        _emails = (config ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public int Count => _emails.Count;

    public bool IsAllowed(string? email)
        => !string.IsNullOrEmpty(email) && _emails.Contains(email);
}
