using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Admin.Commands.StopImpersonation;

public class StopImpersonationCommandHandler : IRequestHandler<StopImpersonationCommand, Unit>
{
    private readonly IImpersonationAuditRepository _auditRepository;
    private readonly IAuditService _auditService;

    public StopImpersonationCommandHandler(
        IImpersonationAuditRepository auditRepository,
        IAuditService auditService)
    {
        _auditRepository = auditRepository;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(StopImpersonationCommand request, CancellationToken cancellationToken)
    {
        var audit = await _auditRepository.GetByIdAsync(request.ImpersonationId, cancellationToken);
        if (audit == null)
            throw new ArgumentException("Impersonation session not found");

        if (audit.AdminUserId != request.AdminUserId)
            throw new UnauthorizedAccessException("Only the initiating admin can stop this impersonation");

        audit.Revoke();
        await _auditRepository.UpdateAsync(audit, cancellationToken);

        var durationSeconds = (int)((audit.EndedAt ?? DateTime.UtcNow) - audit.StartedAt).TotalSeconds;
        await _auditService.LogAsync(new AuditEntry
        {
            UserId = audit.AdminUserId,
            Action = "ImpersonationStopped",
            EntityId = audit.Id,
            EntityType = "ImpersonationAudit",
            Description = $"Admin {audit.AdminUserId} stopped impersonating target {audit.TargetUserId} after {durationSeconds}s"
        }, cancellationToken);

        return Unit.Value;
    }
}
