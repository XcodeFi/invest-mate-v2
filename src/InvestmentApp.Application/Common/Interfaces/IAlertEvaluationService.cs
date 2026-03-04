namespace InvestmentApp.Application.Common.Interfaces;

/// <summary>
/// Service for evaluating alert rules and generating alerts.
/// </summary>
public interface IAlertEvaluationService
{
    Task<IEnumerable<AlertEvaluationResult>> EvaluateRulesAsync(string userId, CancellationToken cancellationToken = default);
}

public class AlertEvaluationResult
{
    public string AlertRuleId { get; set; } = null!;
    public string AlertType { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string? Symbol { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? ThresholdValue { get; set; }
    public bool IsTriggered { get; set; }
}
