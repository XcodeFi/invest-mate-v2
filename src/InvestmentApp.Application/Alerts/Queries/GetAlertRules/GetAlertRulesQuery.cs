using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Alerts.Queries.GetAlertRules;

public class GetAlertRulesQuery : IRequest<IEnumerable<AlertRuleDto>>
{
    public string UserId { get; set; } = null!;
}

public class AlertRuleDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string AlertType { get; set; } = null!;
    public string Condition { get; set; } = null!;
    public decimal Threshold { get; set; }
    public string? PortfolioId { get; set; }
    public string? Symbol { get; set; }
    public string Channel { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetAlertRulesQueryHandler : IRequestHandler<GetAlertRulesQuery, IEnumerable<AlertRuleDto>>
{
    private readonly IAlertRuleRepository _alertRuleRepository;

    public GetAlertRulesQueryHandler(IAlertRuleRepository alertRuleRepository)
    {
        _alertRuleRepository = alertRuleRepository;
    }

    public async Task<IEnumerable<AlertRuleDto>> Handle(GetAlertRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _alertRuleRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return rules.Select(r => new AlertRuleDto
        {
            Id = r.Id,
            Name = r.Name,
            AlertType = r.AlertType,
            Condition = r.Condition,
            Threshold = r.Threshold,
            PortfolioId = r.PortfolioId,
            Symbol = r.Symbol,
            Channel = r.Channel,
            IsActive = r.IsActive,
            LastTriggeredAt = r.LastTriggeredAt,
            CreatedAt = r.CreatedAt
        });
    }
}
