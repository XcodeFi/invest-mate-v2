using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Admin.Commands.StartImpersonation;

public class StartImpersonationCommandHandler : IRequestHandler<StartImpersonationCommand, StartImpersonationResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IImpersonationAuditRepository _auditRepository;
    private readonly IJwtService _jwtService;
    private readonly IAuditService _auditService;

    public StartImpersonationCommandHandler(
        IUserRepository userRepository,
        IImpersonationAuditRepository auditRepository,
        IJwtService jwtService,
        IAuditService auditService)
    {
        _userRepository = userRepository;
        _auditRepository = auditRepository;
        _jwtService = jwtService;
        _auditService = auditService;
    }

    public async Task<StartImpersonationResult> Handle(StartImpersonationCommand request, CancellationToken cancellationToken)
    {
        // Defense-in-depth ordering: verify admin role BEFORE any check that could
        // confirm/deny info about other users (self-impersonate, target existence).
        var admin = await _userRepository.GetByIdAsync(request.AdminUserId, cancellationToken);
        if (admin == null || admin.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("Caller is not an admin");

        if (request.AdminUserId == request.TargetUserId)
            throw new ArgumentException("Cannot impersonate self");

        var target = await _userRepository.GetByIdAsync(request.TargetUserId, cancellationToken);
        if (target == null)
            throw new ArgumentException("Target user not found");

        var audit = new ImpersonationAudit(
            adminUserId: admin.Id,
            targetUserId: target.Id,
            reason: request.Reason,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent);

        await _auditRepository.AddAsync(audit, cancellationToken);

        var token = _jwtService.CreateImpersonationToken(admin.Id, target, audit.Id);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = admin.Id,
            Action = "ImpersonationStarted",
            EntityId = audit.Id,
            EntityType = "ImpersonationAudit",
            Description = $"Admin {admin.Email} started impersonating {target.Email}. Reason: {request.Reason}"
        }, cancellationToken);

        return new StartImpersonationResult
        {
            Token = token,
            ImpersonationId = audit.Id,
            TargetEmail = target.Email,
            TargetName = target.Name,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
    }
}
