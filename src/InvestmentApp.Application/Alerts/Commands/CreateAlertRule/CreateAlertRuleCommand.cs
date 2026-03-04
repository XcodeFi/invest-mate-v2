using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Alerts.Commands.CreateAlertRule;

public class CreateAlertRuleCommand : IRequest<string>
{
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string AlertType { get; set; } = null!;
    public string Condition { get; set; } = "Exceeds";
    public decimal Threshold { get; set; }
    public string Channel { get; set; } = "InApp";
    public string? PortfolioId { get; set; }
    public string? Symbol { get; set; }
}

public class CreateAlertRuleCommandHandler : IRequestHandler<CreateAlertRuleCommand, string>
{
    private readonly IAlertRuleRepository _alertRuleRepository;

    public CreateAlertRuleCommandHandler(IAlertRuleRepository alertRuleRepository)
    {
        _alertRuleRepository = alertRuleRepository;
    }

    public async Task<string> Handle(CreateAlertRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = new AlertRule(
            request.UserId, request.Name, request.AlertType,
            request.Condition, request.Threshold, request.Channel,
            request.PortfolioId, request.Symbol
        );

        await _alertRuleRepository.AddAsync(rule, cancellationToken);
        return rule.Id;
    }
}
