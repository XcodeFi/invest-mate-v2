namespace InvestmentApp.Application.Interfaces;

public interface IPositionSizingService
{
    /// <summary>Calculate position size using multiple models for comparison.</summary>
    Task<PositionSizingResult> CalculateAsync(PositionSizingRequest request, CancellationToken ct = default);
}

public class PositionSizingRequest
{
    public decimal AccountBalance { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal RiskPercent { get; set; } = 2m;
    public decimal MaxPositionPercent { get; set; } = 20m;

    // ATR data (from technical analysis)
    public decimal? Atr { get; set; }
    public decimal AtrMultiplier { get; set; } = 2m; // N in ATR-based formula

    // Kelly data (from trade history)
    public decimal? WinRate { get; set; }       // 0-1 (e.g., 0.55 = 55%)
    public decimal? AverageWin { get; set; }    // average win amount
    public decimal? AverageLoss { get; set; }   // average loss amount (positive number)

    // Volatility-adjusted data
    public decimal? AtrPercent { get; set; }    // ATR as % of price (from analysis)
}

public class PositionSizingResult
{
    public List<SizingModelResult> Models { get; set; } = new();
    public string RecommendedModel { get; set; } = "fixed_risk";
}

public class SizingModelResult
{
    public string Model { get; set; } = null!;   // "fixed_risk" | "atr_based" | "kelly" | "turtle" | "volatility_adjusted"
    public string ModelVi { get; set; } = null!;  // Vietnamese label
    public int Shares { get; set; }
    public decimal PositionValue { get; set; }
    public decimal PositionPercent { get; set; }
    public decimal RiskAmount { get; set; }
    public bool WithinLimit { get; set; }
    public string? Note { get; set; }             // Explanation or warning
}
