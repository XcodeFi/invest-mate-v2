using FluentAssertions;
using Moq;
using InvestmentApp.Application.Admin.Commands.StopImpersonation;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Admin;

public class StopImpersonationCommandHandlerTests
{
    private readonly Mock<IImpersonationAuditRepository> _auditRepo;
    private readonly Mock<IAuditService> _auditService;
    private readonly StopImpersonationCommandHandler _handler;

    public StopImpersonationCommandHandlerTests()
    {
        _auditRepo = new Mock<IImpersonationAuditRepository>();
        _auditService = new Mock<IAuditService>();
        _handler = new StopImpersonationCommandHandler(_auditRepo.Object, _auditService.Object);
    }

    [Fact]
    public async Task Handle_ValidStop_MarksAuditRevoked()
    {
        var audit = new ImpersonationAudit("admin-1", "target-1", "Debug", "ip", "ua");
        _auditRepo.Setup(r => r.GetByIdAsync(audit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(audit);

        await _handler.Handle(new StopImpersonationCommand
        {
            ImpersonationId = audit.Id,
            AdminUserId = "admin-1"
        }, CancellationToken.None);

        audit.IsRevoked.Should().BeTrue();
        audit.EndedAt.Should().NotBeNull();
        _auditRepo.Verify(r => r.UpdateAsync(audit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AuditNotFound_ThrowsArgumentException()
    {
        _auditRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ImpersonationAudit?)null);

        var act = () => _handler.Handle(new StopImpersonationCommand
        {
            ImpersonationId = "missing",
            AdminUserId = "admin-1"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_DifferentAdminTriesToStop_ThrowsUnauthorized()
    {
        var audit = new ImpersonationAudit("admin-1", "target-1", "Debug", "ip", "ua");
        _auditRepo.Setup(r => r.GetByIdAsync(audit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(audit);

        var act = () => _handler.Handle(new StopImpersonationCommand
        {
            ImpersonationId = audit.Id,
            AdminUserId = "admin-2"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        audit.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidStop_LogsAuditEntry()
    {
        var audit = new ImpersonationAudit("admin-1", "target-1", "Debug #123", "ip", "ua");
        _auditRepo.Setup(r => r.GetByIdAsync(audit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(audit);

        AuditEntry? captured = null;
        _auditService
            .Setup(s => s.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => captured = entry)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new StopImpersonationCommand
        {
            ImpersonationId = audit.Id,
            AdminUserId = "admin-1"
        }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be("admin-1");
        captured.Action.Should().Be("ImpersonationStopped");
        captured.EntityId.Should().Be(audit.Id);
        captured.EntityType.Should().Be("ImpersonationAudit");
        captured.Description.Should().Contain("admin-1");
        captured.Description.Should().Contain("target-1");
    }
}
