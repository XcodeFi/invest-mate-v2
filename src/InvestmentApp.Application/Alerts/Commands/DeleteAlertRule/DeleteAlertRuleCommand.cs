using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Alerts.Commands.DeleteAlertRule;

public class DeleteAlertRuleCommand : IRequest<Unit>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class DeleteAlertRuleCommandHandler : IRequestHandler<DeleteAlertRuleCommand, Unit>
{
    private readonly IAlertRuleRepository _alertRuleRepository;

    public DeleteAlertRuleCommandHandler(IAlertRuleRepository alertRuleRepository)
    {
        _alertRuleRepository = alertRuleRepository;
    }

    public async Task<Unit> Handle(DeleteAlertRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _alertRuleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Alert rule {request.Id} not found");

        if (rule.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized");

        rule.SoftDelete();
        await _alertRuleRepository.UpdateAsync(rule, cancellationToken);
        return Unit.Value;
    }
}
