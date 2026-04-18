using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.CapitalFlows.Commands.DeleteCapitalFlow;

public class DeleteCapitalFlowCommand : IRequest<bool>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class DeleteCapitalFlowCommandHandler : IRequestHandler<DeleteCapitalFlowCommand, bool>
{
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly IAuditService _auditService;

    public DeleteCapitalFlowCommandHandler(
        ICapitalFlowRepository capitalFlowRepository,
        IAuditService auditService)
    {
        _capitalFlowRepository = capitalFlowRepository;
        _auditService = auditService;
    }

    public async Task<bool> Handle(DeleteCapitalFlowCommand request, CancellationToken cancellationToken)
    {
        var flow = await _capitalFlowRepository.GetByIdAsync(request.Id, cancellationToken);
        if (flow == null || flow.UserId != request.UserId)
            return false;

        // Seed deposits (auto-created at portfolio creation) are immutable —
        // removing would orphan the portfolio's opening balance from audit trail.
        if (flow.IsSeedDeposit)
            return false;

        await _capitalFlowRepository.DeleteAsync(request.Id, cancellationToken);

        await _auditService.LogAsync(new Domain.Entities.AuditEntry
        {
            UserId = request.UserId,
            Action = "DeletedCapitalFlow",
            EntityId = request.Id,
            EntityType = "CapitalFlow",
            Description = $"Deleted {flow.Type} of {flow.Amount:N0} {flow.Currency}"
        }, cancellationToken);

        return true;
    }
}
