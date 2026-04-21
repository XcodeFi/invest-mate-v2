using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class ImpersonationAuditTests
{
    [Fact]
    public void Constructor_ValidParameters_ShouldInitializeFields()
    {
        var audit = new ImpersonationAudit(
            adminUserId: "admin-1",
            targetUserId: "user-2",
            reason: "Debug issue #123",
            ipAddress: "127.0.0.1",
            userAgent: "Mozilla/5.0");

        audit.Id.Should().NotBeNullOrEmpty();
        audit.AdminUserId.Should().Be("admin-1");
        audit.TargetUserId.Should().Be("user-2");
        audit.Reason.Should().Be("Debug issue #123");
        audit.IpAddress.Should().Be("127.0.0.1");
        audit.UserAgent.Should().Be("Mozilla/5.0");
        audit.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        audit.EndedAt.Should().BeNull();
        audit.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullAdminUserId_ShouldThrow()
    {
        var act = () => new ImpersonationAudit(null!, "user-2", "reason", "ip", "ua");

        act.Should().Throw<ArgumentNullException>().WithParameterName("adminUserId");
    }

    [Fact]
    public void Constructor_NullTargetUserId_ShouldThrow()
    {
        var act = () => new ImpersonationAudit("admin-1", null!, "reason", "ip", "ua");

        act.Should().Throw<ArgumentNullException>().WithParameterName("targetUserId");
    }

    [Fact]
    public void Constructor_EmptyReason_ShouldThrow()
    {
        var act = () => new ImpersonationAudit("admin-1", "user-2", "", "ip", "ua");

        act.Should().Throw<ArgumentException>().WithParameterName("reason");
    }

    [Fact]
    public void Revoke_ActiveAudit_ShouldSetIsRevokedAndEndedAt()
    {
        var audit = new ImpersonationAudit("admin-1", "user-2", "Debug", "ip", "ua");

        audit.Revoke();

        audit.IsRevoked.Should().BeTrue();
        audit.EndedAt.Should().NotBeNull();
        audit.EndedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Revoke_AlreadyRevoked_ShouldBeIdempotent()
    {
        var audit = new ImpersonationAudit("admin-1", "user-2", "Debug", "ip", "ua");
        audit.Revoke();
        var firstEndedAt = audit.EndedAt;

        audit.Revoke();

        audit.IsRevoked.Should().BeTrue();
        audit.EndedAt.Should().Be(firstEndedAt);
    }
}
