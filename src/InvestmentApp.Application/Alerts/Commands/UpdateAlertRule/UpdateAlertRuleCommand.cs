using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Alerts.Commands.UpdateAlertRule;

public class UpdateAlertRuleCommand : IRequest<Unit>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string? Name { get; set; }
    public string? AlertType { get; set; }
    public string? Condition { get; set; }
    public decimal? Threshold { get; set; }
    public string? Channel { get; set; }
    public bool? IsActive { get; set; }
    public string? Symbol { get; set; }
    public string? PortfolioId { get; set; }
}

public class UpdateAlertRuleCommandHandler : IRequestHandler<UpdateAlertRuleCommand, Unit>
{
    private readonly IAlertRuleRepository _alertRuleRepository;

    public UpdateAlertRuleCommandHandler(IAlertRuleRepository alertRuleRepository)
    {
        _alertRuleRepository = alertRuleRepository;
    }

    public async Task<Unit> Handle(UpdateAlertRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _alertRuleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Alert rule {request.Id} not found");

        if (rule.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized");

        rule.Update(request.Name, request.AlertType, request.Condition,
            request.Threshold, request.Channel, request.IsActive,
            request.Symbol, request.PortfolioId);

        await _alertRuleRepository.UpdateAsync(rule, cancellationToken);
        return Unit.Value;
    }
}
