using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Common.Interfaces;

public interface IScenarioConsultantService
{
    Task<ScenarioSuggestion> SuggestAsync(string symbol, decimal entryPrice, TimeHorizon timeHorizon, CancellationToken ct = default);
}

public enum TimeHorizon { Short, Medium, Long }

public class ScenarioSuggestion
{
    public string Symbol { get; set; } = null!;
    public decimal EntryPrice { get; set; }
    public TimeHorizon TimeHorizon { get; set; }
    public TechnicalBasis TechnicalBasis { get; set; } = new();
    public List<SuggestedNode> Nodes { get; set; } = new();
}

public class TechnicalBasis
{
    public decimal? Ema20 { get; set; }
    public decimal? Ema50 { get; set; }
    public decimal? Ema200 { get; set; }
    public decimal? Rsi14 { get; set; }
    public decimal? BollingerUpper { get; set; }
    public decimal? BollingerLower { get; set; }
    public List<decimal> SupportLevels { get; set; } = new();
    public List<decimal> ResistanceLevels { get; set; } = new();
    public FibonacciLevels? Fibonacci { get; set; }
    public decimal? Atr14 { get; set; }
}

public class SuggestedNode
{
    public string NodeId { get; set; } = Guid.NewGuid().ToString();
    public string? ParentId { get; set; }
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ConditionType { get; set; } = "PriceAbove";  // "PriceAbove", "PriceBelow", "TimeElapsed"
    public decimal? ConditionValue { get; set; }
    public string ActionType { get; set; } = "SellPercent";    // "SellPercent", "SellAll", "AddPosition"
    public decimal? ActionValue { get; set; }
    public string Reasoning { get; set; } = string.Empty;      // Why this node — indicator sources
    public string Category { get; set; } = string.Empty;       // "TakeProfit", "StopLoss", "AddPosition", "Sideway"
}
