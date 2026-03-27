namespace InvestmentApp.Application.Common.Interfaces;

public interface IScenarioEvaluationService
{
    Task<List<ScenarioEvaluationResult>> EvaluateAllAsync(CancellationToken cancellationToken = default);
}

public class ScenarioEvaluationResult
{
    public string TradePlanId { get; set; } = null!;
    public string NodeId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string ActionType { get; set; } = null!;
    public string Label { get; set; } = null!;
    public decimal? CurrentPrice { get; set; }
    public decimal? ConditionValue { get; set; }
}
