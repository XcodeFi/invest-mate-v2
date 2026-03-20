using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

public class TechnicalIndicatorService : ITechnicalIndicatorService
{
    private readonly IMarketDataProvider _marketData;

    public TechnicalIndicatorService(IMarketDataProvider marketData)
    {
        _marketData = marketData;
    }

    public async Task<TechnicalAnalysisResult> AnalyzeAsync(string symbol, CancellationToken ct = default)
    {
        // Fetch ~6 months of data for reliable EMA50 + MACD calculation
        var from = DateTime.UtcNow.AddMonths(-6);
        var to = DateTime.UtcNow;
        var prices = await _marketData.GetHistoricalPricesAsync(symbol, from, to, ct);

        if (prices.Count < 20)
        {
            return new TechnicalAnalysisResult
            {
                Symbol = symbol,
                AnalyzedAt = DateTime.UtcNow,
                DataPoints = prices.Count,
                OverallSignal = "hold",
                OverallSignalVi = "Không đủ dữ liệu"
            };
        }

        var closes = prices.Select(p => p.Close).ToList();
        var volumes = prices.Select(p => (decimal)p.Volume).ToList();
        var current = prices.Last();

        var result = new TechnicalAnalysisResult
        {
            Symbol = symbol,
            AnalyzedAt = DateTime.UtcNow,
            DataPoints = prices.Count,
            CurrentPrice = current.Close,
            CurrentVolume = current.Volume
        };

        // Price change (vs previous day)
        if (prices.Count >= 2)
        {
            var prev = prices[prices.Count - 2].Close;
            result.PriceChange = current.Close - prev;
            result.PriceChangePercent = prev != 0 ? (result.PriceChange / prev) * 100 : 0;
        }

        // --- EMA ---
        result.Ema20 = CalculateEma(closes, 20);
        result.Ema50 = CalculateEma(closes, 50);

        if (result.Ema20.HasValue && result.Ema50.HasValue)
        {
            result.EmaTrend = result.Ema20 > result.Ema50 ? "bullish" : "bearish";
        }

        // --- RSI(14) ---
        result.Rsi14 = CalculateRsi(closes, 14);
        if (result.Rsi14.HasValue)
        {
            result.RsiSignal = result.Rsi14 switch
            {
                <= 30 => "oversold",
                >= 70 => "overbought",
                _ => "neutral"
            };
        }

        // --- MACD(12, 26, 9) ---
        var (macdLine, signalLine, histogram) = CalculateMacd(closes, 12, 26, 9);
        result.MacdLine = macdLine;
        result.SignalLine = signalLine;
        result.MacdHistogram = histogram;

        if (macdLine.HasValue && signalLine.HasValue)
        {
            // Check for crossover
            var prevMacd = CalculateMacdAt(closes.Take(closes.Count - 1).ToList(), 12, 26, 9);
            if (prevMacd.macd.HasValue && prevMacd.signal.HasValue)
            {
                bool wasBelowSignal = prevMacd.macd < prevMacd.signal;
                bool isAboveSignal = macdLine > signalLine;

                if (wasBelowSignal && isAboveSignal)
                    result.MacdSignal = "buy";
                else if (!wasBelowSignal && !isAboveSignal)
                    result.MacdSignal = "sell";
                else
                    result.MacdSignal = macdLine > signalLine ? "buy" : "sell";
            }
        }

        // --- Volume ---
        if (volumes.Count >= 20)
        {
            var last20 = volumes.Skip(volumes.Count - 20).Take(20).ToList();
            result.AvgVolume20 = last20.Average();

            if (result.AvgVolume20 > 0)
            {
                result.VolumeRatio = (decimal)current.Volume / result.AvgVolume20.Value;
                result.VolumeSignal = result.VolumeRatio switch
                {
                    >= 2.0m => "spike",
                    >= 1.3m => "high",
                    >= 0.7m => "normal",
                    _ => "low"
                };
            }
        }

        // --- Support / Resistance (swing high/low) ---
        var (supports, resistances) = CalculateSwingLevels(closes);
        result.SupportLevels = supports.Where(s => s < current.Close).OrderByDescending(s => s).Take(3).ToList();
        result.ResistanceLevels = resistances.Where(r => r > current.Close).OrderBy(r => r).Take(3).ToList();

        // --- Overall Signal ---
        int bullish = 0, bearish = 0, neutral = 0;

        // EMA trend
        if (result.EmaTrend == "bullish") bullish++; else if (result.EmaTrend == "bearish") bearish++; else neutral++;

        // RSI
        if (result.RsiSignal == "oversold") bullish++; else if (result.RsiSignal == "overbought") bearish++; else neutral++;

        // MACD
        if (result.MacdSignal == "buy") bullish++; else if (result.MacdSignal == "sell") bearish++; else neutral++;

        // Volume (high volume confirms trend direction)
        if (result.VolumeSignal is "spike" or "high")
        {
            if (result.PriceChangePercent > 0) bullish++; else bearish++;
        }
        else neutral++;

        result.BullishCount = bullish;
        result.BearishCount = bearish;
        result.NeutralCount = neutral;

        (result.OverallSignal, result.OverallSignalVi) = (bullish, bearish) switch
        {
            ( >= 3, _) => ("strong_buy", "Mua mạnh"),
            ( >= 2, 0) => ("buy", "Mua"),
            (_, >= 3) => ("strong_sell", "Bán mạnh"),
            (0, >= 2) => ("sell", "Bán"),
            _ => ("hold", "Chờ")
        };

        // --- Trade Suggestion ---
        if (result.SupportLevels.Count > 0 && result.ResistanceLevels.Count > 0)
        {
            var nearestSupport = result.SupportLevels[0];
            var nearestResistance = result.ResistanceLevels[0];
            result.SuggestedEntry = nearestSupport;
            result.SuggestedStopLoss = result.SupportLevels.Count > 1
                ? result.SupportLevels[1]
                : nearestSupport * 0.95m; // 5% below support
            result.SuggestedTarget = nearestResistance;

            var risk = result.SuggestedEntry.Value - result.SuggestedStopLoss.Value;
            var reward = result.SuggestedTarget.Value - result.SuggestedEntry.Value;
            result.RiskRewardRatio = risk > 0 ? Math.Round(reward / risk, 1) : null;
        }

        return result;
    }

    // --- Indicator Calculations ---

    private static decimal? CalculateEma(List<decimal> data, int period)
    {
        if (data.Count < period) return null;

        decimal multiplier = 2.0m / (period + 1);
        // SMA as seed
        decimal ema = data.Take(period).Average();

        for (int i = period; i < data.Count; i++)
        {
            ema = (data[i] - ema) * multiplier + ema;
        }

        return Math.Round(ema, 0);
    }

    private static decimal? CalculateRsi(List<decimal> data, int period)
    {
        if (data.Count < period + 1) return null;

        decimal avgGain = 0, avgLoss = 0;

        // First period: simple average
        for (int i = 1; i <= period; i++)
        {
            var change = data[i] - data[i - 1];
            if (change >= 0) avgGain += change;
            else avgLoss += Math.Abs(change);
        }
        avgGain /= period;
        avgLoss /= period;

        // Smoothed RSI
        for (int i = period + 1; i < data.Count; i++)
        {
            var change = data[i] - data[i - 1];
            if (change >= 0)
            {
                avgGain = (avgGain * (period - 1) + change) / period;
                avgLoss = (avgLoss * (period - 1)) / period;
            }
            else
            {
                avgGain = (avgGain * (period - 1)) / period;
                avgLoss = (avgLoss * (period - 1) + Math.Abs(change)) / period;
            }
        }

        if (avgLoss == 0) return 100;
        var rs = avgGain / avgLoss;
        return Math.Round(100 - (100 / (1 + rs)), 1);
    }

    private static (decimal? macd, decimal? signal, decimal? histogram) CalculateMacd(
        List<decimal> data, int fastPeriod, int slowPeriod, int signalPeriod)
    {
        return CalculateMacdAt(data, fastPeriod, slowPeriod, signalPeriod);
    }

    private static (decimal? macd, decimal? signal, decimal? histogram) CalculateMacdAt(
        List<decimal> data, int fastPeriod, int slowPeriod, int signalPeriod)
    {
        if (data.Count < slowPeriod + signalPeriod) return (null, null, null);

        // Calculate MACD line series (fast EMA - slow EMA) for signal line EMA
        var macdSeries = new List<decimal>();
        for (int i = slowPeriod; i <= data.Count; i++)
        {
            var subset = data.Take(i).ToList();
            var fastEma = CalculateEma(subset, fastPeriod);
            var slowEma = CalculateEma(subset, slowPeriod);
            if (fastEma.HasValue && slowEma.HasValue)
                macdSeries.Add(fastEma.Value - slowEma.Value);
        }

        if (macdSeries.Count < signalPeriod) return (null, null, null);

        var macdLine = macdSeries.Last();
        var signalLine = CalculateEma(macdSeries, signalPeriod);

        if (!signalLine.HasValue) return (macdLine, null, null);

        var histogram = macdLine - signalLine.Value;
        return (Math.Round(macdLine, 0), Math.Round(signalLine.Value, 0), Math.Round(histogram, 0));
    }

    private static (List<decimal> supports, List<decimal> resistances) CalculateSwingLevels(List<decimal> closes)
    {
        var supports = new List<decimal>();
        var resistances = new List<decimal>();

        if (closes.Count < 5) return (supports, resistances);

        // Use window of 5 to find local min/max
        for (int i = 2; i < closes.Count - 2; i++)
        {
            var center = closes[i];
            var left1 = closes[i - 1];
            var left2 = closes[i - 2];
            var right1 = closes[i + 1];
            var right2 = closes[i + 2];

            // Swing low (support)
            if (center <= left1 && center <= left2 && center <= right1 && center <= right2)
                supports.Add(center);

            // Swing high (resistance)
            if (center >= left1 && center >= left2 && center >= right1 && center >= right2)
                resistances.Add(center);
        }

        // Deduplicate (cluster nearby levels within 2%)
        supports = ClusterLevels(supports);
        resistances = ClusterLevels(resistances);

        return (supports, resistances);
    }

    private static List<decimal> ClusterLevels(List<decimal> levels)
    {
        if (levels.Count == 0) return levels;

        var sorted = levels.OrderBy(l => l).ToList();
        var clustered = new List<decimal> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var last = clustered.Last();
            // If within 2%, merge (take average)
            if (last > 0 && Math.Abs(sorted[i] - last) / last < 0.02m)
            {
                clustered[clustered.Count - 1] = (last + sorted[i]) / 2;
            }
            else
            {
                clustered.Add(sorted[i]);
            }
        }

        return clustered;
    }
}
