using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Alerts.Commands.UpdateAlertRule;

public class UpdateAlertRuleCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
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
    private readonly IPortfolioRepository _portfolioRepository;

    public UpdateAlertRuleCommandHandler(IAlertRuleRepository alertRuleRepository, IPortfolioRepository portfolioRepository)
    {
        _alertRuleRepository = alertRuleRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<Unit> Handle(UpdateAlertRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _alertRuleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Alert rule {request.Id} not found");

        if (rule.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized");

        await LinkOwnership.EnsurePortfolioAsync(request.PortfolioId, request.UserId, _portfolioRepository, cancellationToken);

        rule.Update(request.Name, request.AlertType, request.Condition,
            request.Threshold, request.Channel, request.IsActive,
            request.Symbol, request.PortfolioId);

        await _alertRuleRepository.UpdateAsync(rule, cancellationToken);
        return Unit.Value;
    }
}
