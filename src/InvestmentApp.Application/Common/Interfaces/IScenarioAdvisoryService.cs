namespace InvestmentApp.Application.Common.Interfaces;

public interface IScenarioAdvisoryService
{
    Task<List<ScenarioAdvisory>> GetAdvisoriesAsync(string userId, CancellationToken ct = default);
}

public class ScenarioAdvisory
{
    public string TradePlanId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public decimal CurrentPrice { get; set; }
    public string NodeId { get; set; } = null!;
    public string NodeLabel { get; set; } = null!;
    public string ConditionDescription { get; set; } = null!;  // "Giá ≥ 80,000"
    public string ActionDescription { get; set; } = null!;     // "xem xét bán 30%"
    public string Message { get; set; } = null!;               // "HPG đang ở 82,500 (vùng ≥ 80,000) — xem xét bán 30%"
}
