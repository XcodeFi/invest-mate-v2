namespace InvestmentApp.Domain.Entities;

public class ImpersonationAudit
{
    public string Id { get; private set; } = null!;
    public string AdminUserId { get; private set; } = null!;
    public string TargetUserId { get; private set; } = null!;
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string Reason { get; private set; } = null!;
    public string IpAddress { get; private set; } = null!;
    public string UserAgent { get; private set; } = null!;
    public bool IsRevoked { get; private set; }

    // For MongoDB deserialization
    public ImpersonationAudit() { }

    public ImpersonationAudit(
        string adminUserId,
        string targetUserId,
        string reason,
        string ipAddress,
        string userAgent)
    {
        if (adminUserId == null) throw new ArgumentNullException(nameof(adminUserId));
        if (targetUserId == null) throw new ArgumentNullException(nameof(targetUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required for impersonation audit", nameof(reason));

        Id = Guid.NewGuid().ToString();
        AdminUserId = adminUserId;
        TargetUserId = targetUserId;
        Reason = reason;
        IpAddress = ipAddress ?? string.Empty;
        UserAgent = userAgent ?? string.Empty;
        StartedAt = DateTime.UtcNow;
        EndedAt = null;
        IsRevoked = false;
    }

    public void Revoke()
    {
        if (IsRevoked) return;
        IsRevoked = true;
        EndedAt = DateTime.UtcNow;
    }
}
