namespace InvestmentApp.Application.Interfaces;

public interface ITechnicalIndicatorService
{
    Task<TechnicalAnalysisResult> AnalyzeAsync(string symbol, int months = 12, CancellationToken cancellationToken = default);
}

public class TechnicalAnalysisResult
{
    public string Symbol { get; set; } = null!;
    public DateTime AnalyzedAt { get; set; }
    public int DataPoints { get; set; }

    // Current price
    public decimal CurrentPrice { get; set; }
    public decimal PriceChange { get; set; }
    public decimal PriceChangePercent { get; set; }
    public long CurrentVolume { get; set; }

    // Moving Averages
    public decimal? Ema20 { get; set; }
    public decimal? Ema50 { get; set; }
    public string EmaTrend { get; set; } = "neutral"; // "bullish" | "bearish" | "neutral"

    // RSI
    public decimal? Rsi14 { get; set; }
    public string RsiSignal { get; set; } = "neutral"; // "oversold" | "overbought" | "neutral"

    // MACD
    public decimal? MacdLine { get; set; }
    public decimal? SignalLine { get; set; }
    public decimal? MacdHistogram { get; set; }
    public string MacdSignal { get; set; } = "neutral"; // "buy" | "sell" | "neutral"

    // Volume
    public decimal? AvgVolume20 { get; set; }
    public decimal? VolumeRatio { get; set; } // current / avg20
    public string VolumeSignal { get; set; } = "normal"; // "spike" | "high" | "normal" | "low"

    // Support / Resistance (swing high/low based)
    public List<decimal> SupportLevels { get; set; } = new();
    public List<decimal> ResistanceLevels { get; set; } = new();

    // Overall Signal
    public string OverallSignal { get; set; } = "hold"; // "strong_buy" | "buy" | "hold" | "sell" | "strong_sell"
    public string OverallSignalVi { get; set; } = "Chờ"; // Vietnamese label
    public int BullishCount { get; set; }
    public int BearishCount { get; set; }
    public int NeutralCount { get; set; }

    // Bollinger Bands(20, 2)
    public decimal? BollingerUpper { get; set; }
    public decimal? BollingerMiddle { get; set; }
    public decimal? BollingerLower { get; set; }
    public decimal? BollingerBandwidth { get; set; }
    public decimal? BollingerPercentB { get; set; }
    public string? BollingerSignal { get; set; } // "squeeze" | "breakout_up" | "breakout_down" | "neutral"

    // ATR(14)
    public decimal? Atr14 { get; set; }
    public decimal? AtrPercent { get; set; } // ATR as % of current price

    // Stochastic Oscillator (14,3,3)
    public decimal? StochasticK { get; set; }
    public decimal? StochasticD { get; set; }
    public string StochasticSignal { get; set; } = "neutral"; // "oversold" | "overbought" | "neutral"

    // ADX (14) + Directional Indicators
    public decimal? Adx14 { get; set; }
    public decimal? PlusDi { get; set; }
    public decimal? MinusDi { get; set; }
    public string AdxSignal { get; set; } = "neutral"; // "trending" | "strong_trend" | "sideway" | "neutral"

    // OBV (On-Balance Volume)
    public decimal? Obv { get; set; }
    public string ObvSignal { get; set; } = "neutral"; // "rising" | "falling" | "neutral"

    // MFI (Money Flow Index, 14)
    public decimal? Mfi14 { get; set; }
    public string MfiSignal { get; set; } = "neutral"; // "oversold" | "overbought" | "neutral"

    // EMA200
    public decimal? Ema200 { get; set; }

    // Fibonacci Retracement / Extension
    public FibonacciLevels? Fibonacci { get; set; }

    // Trade suggestion
    public decimal? SuggestedEntry { get; set; }
    public decimal? SuggestedStopLoss { get; set; }
    public decimal? SuggestedTarget { get; set; }
    public decimal? RiskRewardRatio { get; set; }
}

public class FibonacciLevels
{
    public decimal SwingHigh { get; set; }
    public decimal SwingLow { get; set; }
    public decimal Retracement236 { get; set; }
    public decimal Retracement382 { get; set; }
    public decimal Retracement500 { get; set; }
    public decimal Retracement618 { get; set; }
    public decimal Retracement786 { get; set; }
    public decimal Extension1272 { get; set; }
    public decimal Extension1618 { get; set; }
}
