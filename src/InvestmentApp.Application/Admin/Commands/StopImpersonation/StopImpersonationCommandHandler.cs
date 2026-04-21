using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Admin.Commands.StopImpersonation;

public class StopImpersonationCommandHandler : IRequestHandler<StopImpersonationCommand, Unit>
{
    private readonly IImpersonationAuditRepository _auditRepository;

    public StopImpersonationCommandHandler(IImpersonationAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
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

        return Unit.Value;
    }
}
