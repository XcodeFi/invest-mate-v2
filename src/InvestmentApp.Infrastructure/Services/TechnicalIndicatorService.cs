using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

public class TechnicalIndicatorService : ITechnicalIndicatorService
{
    private readonly IMarketDataProvider _marketData;

    public TechnicalIndicatorService(IMarketDataProvider marketData)
    {
        _marketData = marketData;
    }

    public async Task<TechnicalAnalysisResult> AnalyzeAsync(string symbol, int months = 12, CancellationToken ct = default)
    {
        // Fetch configurable months of data (default 12 for reliable EMA200 + MACD calculation)
        var from = DateTime.UtcNow.AddMonths(-months);
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
        result.Ema200 = CalculateEma(closes, 200);

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

        // --- Fibonacci Retracement / Extension ---
        result.Fibonacci = CalculateFibonacciLevels(supports, resistances);

        // --- Bollinger Bands(20, 2) ---
        var (bbMiddle, bbUpper, bbLower) = CalculateBollingerBands(closes, 20, 2m);
        result.BollingerMiddle = bbMiddle;
        result.BollingerUpper = bbUpper;
        result.BollingerLower = bbLower;

        if (bbMiddle.HasValue && bbUpper.HasValue && bbLower.HasValue)
        {
            var bandwidth = bbMiddle.Value > 0
                ? (bbUpper.Value - bbLower.Value) / bbMiddle.Value * 100
                : 0;
            result.BollingerBandwidth = Math.Round(bandwidth, 2);

            var range = bbUpper.Value - bbLower.Value;
            result.BollingerPercentB = range > 0
                ? Math.Round((current.Close - bbLower.Value) / range, 3)
                : 0.5m;

            // Signal: squeeze when bandwidth < 4%, breakout when price outside bands
            if (bandwidth < 4m)
                result.BollingerSignal = "squeeze";
            else if (current.Close > bbUpper.Value)
                result.BollingerSignal = "breakout_up";
            else if (current.Close < bbLower.Value)
                result.BollingerSignal = "breakout_down";
            else
                result.BollingerSignal = "neutral";
        }

        // --- ATR(14) ---
        var highs = prices.Select(p => p.High).ToList();
        var lows = prices.Select(p => p.Low).ToList();
        var atr = CalculateAtr(highs, lows, closes, 14);
        result.Atr14 = atr;

        if (atr.HasValue && current.Close > 0)
            result.AtrPercent = Math.Round(atr.Value / current.Close * 100, 2);

        // --- EMA(21) for MA Trailing Stop ---
        result.Ema21 = CalculateEma(closes, 21);

        // --- Highest High / Lowest Low (22-period, for Chandelier Exit) ---
        if (highs.Count >= 22)
        {
            var last22Highs = highs.TakeLast(22);
            var last22Lows = lows.TakeLast(22);
            result.HighestHigh22 = last22Highs.Max();
            result.LowestLow22 = last22Lows.Min();
        }

        // --- Stochastic Oscillator (14, 3, 3) ---
        var (stochK, stochD) = CalculateStochastic(highs, lows, closes, 14, 3);
        result.StochasticK = stochK;
        result.StochasticD = stochD;

        if (stochK.HasValue)
        {
            result.StochasticSignal = stochK.Value switch
            {
                <= 20 => "oversold",
                >= 80 => "overbought",
                _ => "neutral"
            };
        }

        // --- ADX (14) + DI ---
        var (adx, plusDi, minusDi) = CalculateAdx(highs, lows, closes, 14);
        result.Adx14 = adx;
        result.PlusDi = plusDi;
        result.MinusDi = minusDi;

        if (adx.HasValue)
        {
            result.AdxSignal = adx.Value switch
            {
                >= 40 => "strong_trend",
                >= 25 => "trending",
                < 20 => "sideway",
                _ => "neutral"
            };
        }

        // --- OBV (On-Balance Volume) ---
        var (obv, obvTrend) = CalculateObv(closes, volumes);
        result.Obv = obv;
        result.ObvSignal = obvTrend ?? "neutral";

        // --- MFI (14) ---
        result.Mfi14 = CalculateMfi(highs, lows, closes, volumes, 14);
        if (result.Mfi14.HasValue)
        {
            result.MfiSignal = result.Mfi14.Value switch
            {
                <= 20 => "oversold",
                >= 80 => "overbought",
                _ => "neutral"
            };
        }

        // --- Market Condition Classifier (ADX-based) ---
        var (marketCond, marketCondVi, suggestedStrategy) = ClassifyMarketCondition(result.Adx14, result.AdxSignal);
        result.MarketCondition = marketCond;
        result.MarketConditionVi = marketCondVi;
        result.SuggestedStrategy = suggestedStrategy;

        // --- Divergence Detection (RSI & MACD vs Price) ---
        var (rsiDiv, macdDiv) = DetectDivergence(closes, highs, lows, volumes, 50);
        result.RsiDivergence = rsiDiv;
        result.MacdDivergence = macdDiv;

        // Composite divergence signal: RSI takes priority, then MACD
        var primaryDiv = rsiDiv ?? macdDiv;
        if (primaryDiv != null)
        {
            result.DivergenceSignal = primaryDiv == "bullish" ? "bullish_divergence" : "bearish_divergence";
            result.DivergenceSignalVi = primaryDiv == "bullish" ? "Phân kỳ tăng" : "Phân kỳ giảm";
        }

        // --- Overall Signal (10 indicators) ---
        int bullish = 0, bearish = 0, neutral = 0;

        // 1. EMA trend
        if (result.EmaTrend == "bullish") bullish++; else if (result.EmaTrend == "bearish") bearish++; else neutral++;

        // 2. RSI
        if (result.RsiSignal == "oversold") bullish++; else if (result.RsiSignal == "overbought") bearish++; else neutral++;

        // 3. MACD
        if (result.MacdSignal == "buy") bullish++; else if (result.MacdSignal == "sell") bearish++; else neutral++;

        // 4. Volume (high volume confirms trend direction)
        if (result.VolumeSignal is "spike" or "high")
        {
            if (result.PriceChangePercent > 0) bullish++; else bearish++;
        }
        else neutral++;

        // 5. Bollinger Bands
        if (result.BollingerSignal == "breakout_up") bullish++;
        else if (result.BollingerSignal == "breakout_down") bearish++;
        else neutral++;

        // 6. ATR — high volatility (ATR% > 3%) is bearish (risky), low is neutral
        if (result.AtrPercent.HasValue && result.AtrPercent > 3m) bearish++;
        else neutral++;

        // 7. Stochastic
        if (result.StochasticSignal == "oversold") bullish++;
        else if (result.StochasticSignal == "overbought") bearish++;
        else neutral++;

        // 8. ADX + DI direction (ADX measures strength, DI determines direction)
        if (result.AdxSignal is "trending" or "strong_trend" && result.PlusDi.HasValue && result.MinusDi.HasValue)
        {
            if (result.PlusDi > result.MinusDi) bullish++; else bearish++;
        }
        else neutral++;

        // 9. OBV
        if (result.ObvSignal == "rising") bullish++;
        else if (result.ObvSignal == "falling") bearish++;
        else neutral++;

        // 10. MFI
        if (result.MfiSignal == "oversold") bullish++;
        else if (result.MfiSignal == "overbought") bearish++;
        else neutral++;

        result.BullishCount = bullish;
        result.BearishCount = bearish;
        result.NeutralCount = neutral;

        (result.OverallSignal, result.OverallSignalVi) = (bullish, bearish) switch
        {
            ( >= 6, _) => ("strong_buy", "Mua mạnh"),
            ( >= 4, _) when bearish <= 3 => ("buy", "Mua"),
            (_, >= 6) => ("strong_sell", "Bán mạnh"),
            (_, >= 4) when bullish <= 3 => ("sell", "Bán"),
            _ => ("hold", "Chờ")
        };

        // --- Confluence Score (weighted multi-indicator) ---
        result.ConfluenceScore = CalculateConfluenceScore(result);

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

    private static (decimal? middle, decimal? upper, decimal? lower) CalculateBollingerBands(
        List<decimal> data, int period, decimal multiplier)
    {
        if (data.Count < period) return (null, null, null);

        var last = data.TakeLast(period).ToList();
        var mean = last.Average();
        var sma = Math.Round(mean, 0);

        var variance = last.Average(p => (p - mean) * (p - mean));
        var stddev = (decimal)Math.Sqrt((double)variance);

        var upper = Math.Round(sma + multiplier * stddev, 0);
        var lower = Math.Round(sma - multiplier * stddev, 0);

        return (sma, upper, lower);
    }

    private static decimal? CalculateAtr(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period)
    {
        if (highs.Count < period + 1) return null;

        // Calculate True Range series
        var trueRanges = new List<decimal>();
        for (int i = 1; i < highs.Count; i++)
        {
            var highLow = highs[i] - lows[i];
            var highPrevClose = Math.Abs(highs[i] - closes[i - 1]);
            var lowPrevClose = Math.Abs(lows[i] - closes[i - 1]);
            trueRanges.Add(Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose)));
        }

        if (trueRanges.Count < period) return null;

        // Initial ATR = simple average of first `period` true ranges
        decimal atr = trueRanges.Take(period).Average();

        // Smoothed ATR
        for (int i = period; i < trueRanges.Count; i++)
        {
            atr = (atr * (period - 1) + trueRanges[i]) / period;
        }

        return Math.Round(atr, 0);
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

    // --- Stochastic Oscillator (%K, %D) — Slow Stochastic ---
    private static (decimal? k, decimal? d) CalculateStochastic(
        List<decimal> highs, List<decimal> lows, List<decimal> closes, int kPeriod, int dPeriod)
    {
        // Need kPeriod + 2*(dPeriod-1) data points: kPeriod for first raw%K, dPeriod-1 for slow%K, dPeriod-1 for %D
        int minRequired = kPeriod + 2 * dPeriod - 1;
        if (closes.Count < minRequired) return (null, null);

        // Calculate raw %K series over a window large enough for both slow %K and %D
        int rawKCount = 2 * dPeriod - 1; // e.g. 5 values for dPeriod=3
        var rawKs = new List<decimal>();
        for (int i = closes.Count - rawKCount; i < closes.Count; i++)
        {
            var highestHigh = decimal.MinValue;
            var lowestLow = decimal.MaxValue;
            for (int j = i - kPeriod + 1; j <= i; j++)
            {
                if (highs[j] > highestHigh) highestHigh = highs[j];
                if (lows[j] < lowestLow) lowestLow = lows[j];
            }

            var range = highestHigh - lowestLow;
            rawKs.Add(range > 0 ? (closes[i] - lowestLow) / range * 100 : 50m);
        }

        // Slow %K series = SMA(dPeriod) of raw %K
        var slowKs = new List<decimal>();
        for (int i = 0; i <= rawKs.Count - dPeriod; i++)
        {
            slowKs.Add(rawKs.Skip(i).Take(dPeriod).Average());
        }

        // %K = last slow %K value
        var percentK = Math.Round(slowKs.Last(), 1);
        // %D = SMA(dPeriod) of slow %K series
        var percentD = Math.Round(slowKs.TakeLast(dPeriod).Average(), 1);

        return (percentK, percentD);
    }

    // --- ADX (Average Directional Index) + DI ---
    private static (decimal? adx, decimal? plusDi, decimal? minusDi) CalculateAdx(
        List<decimal> highs, List<decimal> lows, List<decimal> closes, int period)
    {
        // Need at least 2 * period + 1 candles (period for DI smoothing + period for ADX smoothing)
        if (highs.Count < 2 * period + 1) return (null, null, null);

        // Step 1: Calculate +DM, -DM, and TR series
        var plusDmSeries = new List<decimal>();
        var minusDmSeries = new List<decimal>();
        var trSeries = new List<decimal>();

        for (int i = 1; i < highs.Count; i++)
        {
            var upMove = highs[i] - highs[i - 1];
            var downMove = lows[i - 1] - lows[i];

            var plusDm = (upMove > downMove && upMove > 0) ? upMove : 0;
            var minusDm = (downMove > upMove && downMove > 0) ? downMove : 0;

            plusDmSeries.Add(plusDm);
            minusDmSeries.Add(minusDm);

            var highLow = highs[i] - lows[i];
            var highPrevClose = Math.Abs(highs[i] - closes[i - 1]);
            var lowPrevClose = Math.Abs(lows[i] - closes[i - 1]);
            trSeries.Add(Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose)));
        }

        if (trSeries.Count < 2 * period) return (null, null, null);

        // Step 2: Smooth +DM, -DM, TR using Wilder's smoothing (first period = SMA, then smoothed)
        decimal smoothPlusDm = plusDmSeries.Take(period).Sum();
        decimal smoothMinusDm = minusDmSeries.Take(period).Sum();
        decimal smoothTr = trSeries.Take(period).Sum();

        var dxSeries = new List<decimal>();

        for (int i = period; i < trSeries.Count; i++)
        {
            if (i > period)
            {
                smoothPlusDm = smoothPlusDm - (smoothPlusDm / period) + plusDmSeries[i];
                smoothMinusDm = smoothMinusDm - (smoothMinusDm / period) + minusDmSeries[i];
                smoothTr = smoothTr - (smoothTr / period) + trSeries[i];
            }

            if (smoothTr == 0) continue;

            var plusDiVal = smoothPlusDm / smoothTr * 100;
            var minusDiVal = smoothMinusDm / smoothTr * 100;
            var diSum = plusDiVal + minusDiVal;
            var dx = diSum > 0 ? Math.Abs(plusDiVal - minusDiVal) / diSum * 100 : 0;
            dxSeries.Add(dx);
        }

        if (dxSeries.Count < period) return (null, null, null);

        // Step 3: ADX = smoothed average of DX
        decimal adx = dxSeries.Take(period).Average();
        for (int i = period; i < dxSeries.Count; i++)
        {
            adx = (adx * (period - 1) + dxSeries[i]) / period;
        }

        // Final +DI and -DI values
        var finalPlusDi = smoothTr > 0 ? smoothPlusDm / smoothTr * 100 : 0;
        var finalMinusDi = smoothTr > 0 ? smoothMinusDm / smoothTr * 100 : 0;

        return (Math.Round(adx, 1), Math.Round(finalPlusDi, 1), Math.Round(finalMinusDi, 1));
    }

    // --- OBV (On-Balance Volume) ---
    private static (decimal? obv, string? signal) CalculateObv(List<decimal> closes, List<decimal> volumes)
    {
        if (closes.Count < 20) return (null, null);

        decimal obv = 0;
        for (int i = 1; i < closes.Count; i++)
        {
            if (closes[i] > closes[i - 1])
                obv += volumes[i];
            else if (closes[i] < closes[i - 1])
                obv -= volumes[i];
        }

        // Determine recent trend: net OBV change over last 10 periods
        decimal obvRecent = 0;
        int startIdx = closes.Count - 10;
        for (int i = startIdx + 1; i < closes.Count; i++)
        {
            if (closes[i] > closes[i - 1])
                obvRecent += volumes[i];
            else if (closes[i] < closes[i - 1])
                obvRecent -= volumes[i];
        }

        string signal = obvRecent > 0 ? "rising" : obvRecent < 0 ? "falling" : "neutral";

        return (obv, signal);
    }

    // --- MFI (Money Flow Index) ---
    private static decimal? CalculateMfi(
        List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes, int period)
    {
        if (closes.Count < period + 1) return null;

        // Typical Price = (H + L + C) / 3
        // Raw Money Flow = TP * Volume
        // Positive MF = sum of raw MF when TP > previous TP
        // Negative MF = sum of raw MF when TP < previous TP
        // MFI = 100 - (100 / (1 + MF Ratio))

        decimal posFlow = 0, negFlow = 0;

        for (int i = closes.Count - period; i < closes.Count; i++)
        {
            var tp = (highs[i] + lows[i] + closes[i]) / 3;
            var prevTp = (highs[i - 1] + lows[i - 1] + closes[i - 1]) / 3;
            var rawMf = tp * volumes[i];

            if (tp > prevTp)
                posFlow += rawMf;
            else if (tp < prevTp)
                negFlow += rawMf;
        }

        if (negFlow == 0) return 100;
        var mfRatio = posFlow / negFlow;
        return Math.Round(100 - (100 / (1 + mfRatio)), 1);
    }

    // --- Confluence Score (0-100, weighted across 5 categories) ---
    private static decimal? CalculateConfluenceScore(TechnicalAnalysisResult r)
    {
        // Need at least EMA + RSI to compute a meaningful score
        if (!r.Ema20.HasValue || !r.Rsi14.HasValue) return null;

        // Trend (30%): EMA trend + ADX direction
        decimal trendScore = 50;
        if (r.EmaTrend == "bullish") trendScore += 15;
        else if (r.EmaTrend == "bearish") trendScore -= 15;

        if (r.AdxSignal is "trending" or "strong_trend" && r.PlusDi.HasValue && r.MinusDi.HasValue)
        {
            if (r.PlusDi > r.MinusDi) trendScore += 20;
            else trendScore -= 20;
        }
        trendScore = Math.Clamp(trendScore, 0, 100);

        // Momentum (25%): RSI + MACD + Stochastic — use raw values for directional strength
        var momentumScores = new List<decimal>();

        if (r.Rsi14.HasValue)
            momentumScores.Add(r.Rsi14.Value); // RSI 0-100 directly maps to bearish-bullish

        if (r.MacdSignal != null)
        {
            momentumScores.Add(r.MacdSignal switch
            {
                "buy" => 75,
                "sell" => 25,
                _ => 50
            });
        }
        if (r.StochasticK.HasValue)
            momentumScores.Add(r.StochasticK.Value); // %K 0-100 directly maps

        decimal momentumScore = momentumScores.Count > 0 ? momentumScores.Average() : 50;

        // Volume (20%): OBV + MFI + Volume ratio
        var volumeScores = new List<decimal>();

        if (r.ObvSignal != null)
        {
            volumeScores.Add(r.ObvSignal switch
            {
                "rising" => 80,
                "falling" => 20,
                _ => 50
            });
        }
        if (r.Mfi14.HasValue)
            volumeScores.Add(r.Mfi14.Value); // MFI 0-100 directly maps to bearish-bullish
        if (r.VolumeSignal != null)
        {
            if (r.VolumeSignal is "spike" or "high")
                volumeScores.Add(r.PriceChangePercent > 0 ? 75 : 25);
            else if (r.VolumeSignal == "low")
                volumeScores.Add(40);
            else
                volumeScores.Add(50);
        }
        decimal volumeScore = volumeScores.Count > 0 ? volumeScores.Average() : 50;

        // Volatility (15%): Bollinger + ATR
        var volatilityScores = new List<decimal>();

        if (r.BollingerSignal != null)
        {
            volatilityScores.Add(r.BollingerSignal switch
            {
                "breakout_up" => 75,
                "breakout_down" => 25,
                _ => 50
            });
        }
        if (r.AtrPercent.HasValue)
        {
            volatilityScores.Add(r.AtrPercent.Value switch
            {
                > 3m => 30, // high volatility = risky
                > 2m => 50,
                _ => 60    // low volatility = favorable
            });
        }
        decimal volatilityScore = volatilityScores.Count > 0 ? volatilityScores.Average() : 50;

        // Price Position (10%): Bollinger %B — high = bullish (near upper band), low = bearish
        decimal positionScore = 50;
        if (r.BollingerPercentB.HasValue)
        {
            // Map %B (0-1) to score (0-100): direct correlation with bullish strength
            positionScore = Math.Clamp(r.BollingerPercentB.Value * 100, 0, 100);
        }

        // Weighted total
        var score = trendScore * 0.30m
                  + momentumScore * 0.25m
                  + volumeScore * 0.20m
                  + volatilityScore * 0.15m
                  + positionScore * 0.10m;

        return Math.Round(Math.Clamp(score, 0, 100), 1);
    }

    // --- Market Condition Classifier ---
    private static (string condition, string conditionVi, string strategy) ClassifyMarketCondition(
        decimal? adx, string? adxSignal)
    {
        if (!adx.HasValue)
            return ("unknown", "Chưa xác định", "");

        return adx.Value switch
        {
            >= 40 => ("strong_trend", "Xu hướng rất mạnh", "Trend Following (mạnh)"),
            >= 25 => ("trending", "Có xu hướng", "Trend Following"),
            _ => ("sideway", "Đi ngang", "Mean Reversion")
        };
    }

    // --- Divergence Detection (RSI & MACD vs Price) ---
    private static (string? rsiDivergence, string? macdDivergence) DetectDivergence(
        List<decimal> closes, List<decimal> highs, List<decimal> lows, List<decimal> volumes, int lookback)
    {
        if (closes.Count < lookback + 14) return (null, null); // Need enough data for RSI/MACD + lookback

        // Calculate RSI series for the lookback window
        var rsiSeries = CalculateRsiSeries(closes, 14, lookback);

        // Calculate MACD histogram series for the lookback window
        var macdHistSeries = CalculateMacdHistogramSeries(closes, 12, 26, 9, lookback);

        // Find swing lows and swing highs in the lookback window
        var recentCloses = closes.TakeLast(lookback).ToList();
        var swingLows = FindSwingPoints(recentCloses, isLow: true);
        var swingHighs = FindSwingPoints(recentCloses, isLow: false);

        // Filter: swing points must be at least 5 bars apart and have meaningful price difference (>= 0.5%)
        swingLows = FilterSwingPoints(swingLows, recentCloses);
        swingHighs = FilterSwingPoints(swingHighs, recentCloses);

        string? rsiDiv = null;
        string? macdDiv = null;

        // RSI Divergence
        if (rsiSeries.Count >= lookback && swingLows.Count >= 2)
        {
            var (lo1Idx, lo1Price) = swingLows[swingLows.Count - 2];
            var (lo2Idx, lo2Price) = swingLows[swingLows.Count - 1];

            // Bullish: price lower low, RSI higher low
            if (lo2Price < lo1Price && rsiSeries[lo2Idx] > rsiSeries[lo1Idx])
                rsiDiv = "bullish";
        }

        if (rsiDiv == null && rsiSeries.Count >= lookback && swingHighs.Count >= 2)
        {
            var (hi1Idx, hi1Price) = swingHighs[swingHighs.Count - 2];
            var (hi2Idx, hi2Price) = swingHighs[swingHighs.Count - 1];

            // Bearish: price higher high, RSI lower high
            if (hi2Price > hi1Price && rsiSeries[hi2Idx] < rsiSeries[hi1Idx])
                rsiDiv = "bearish";
        }

        // MACD Divergence
        if (macdHistSeries.Count >= lookback && swingLows.Count >= 2)
        {
            var (lo1Idx, lo1Price) = swingLows[swingLows.Count - 2];
            var (lo2Idx, lo2Price) = swingLows[swingLows.Count - 1];

            if (lo2Price < lo1Price && macdHistSeries[lo2Idx] > macdHistSeries[lo1Idx])
                macdDiv = "bullish";
        }

        if (macdDiv == null && macdHistSeries.Count >= lookback && swingHighs.Count >= 2)
        {
            var (hi1Idx, hi1Price) = swingHighs[swingHighs.Count - 2];
            var (hi2Idx, hi2Price) = swingHighs[swingHighs.Count - 1];

            if (hi2Price > hi1Price && macdHistSeries[hi2Idx] < macdHistSeries[hi1Idx])
                macdDiv = "bearish";
        }

        return (rsiDiv, macdDiv);
    }

    /// <summary>Filter swing points: min 5 bars apart and min 0.5% price difference from neighbors.</summary>
    private static List<(int index, decimal price)> FilterSwingPoints(
        List<(int index, decimal price)> points, List<decimal> data)
    {
        if (points.Count < 2) return points;

        var filtered = new List<(int index, decimal price)> { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            var prev = filtered.Last();
            // Min 5 bars apart
            if (points[i].index - prev.index < 5) continue;
            // Min 0.5% price difference from average price
            var avgPrice = (prev.price + points[i].price) / 2;
            if (avgPrice > 0 && Math.Abs(points[i].price - prev.price) / avgPrice < 0.005m) continue;
            filtered.Add(points[i]);
        }

        return filtered;
    }

    /// <summary>Calculate RSI values for the last N bars of close data.</summary>
    private static List<decimal> CalculateRsiSeries(List<decimal> closes, int period, int lookback)
    {
        var result = new List<decimal>();
        int startFrom = closes.Count - lookback;
        if (startFrom < period + 1) return result;

        for (int end = startFrom; end < closes.Count; end++)
        {
            var subset = closes.Take(end + 1).ToList();
            var rsi = CalculateRsi(subset, period);
            result.Add(rsi ?? 50m);
        }

        return result;
    }

    /// <summary>Calculate MACD histogram values for the last N bars.</summary>
    private static List<decimal> CalculateMacdHistogramSeries(
        List<decimal> closes, int fast, int slow, int signal, int lookback)
    {
        var result = new List<decimal>();
        int startFrom = closes.Count - lookback;
        if (startFrom < slow + signal) return result;

        for (int end = startFrom; end < closes.Count; end++)
        {
            var subset = closes.Take(end + 1).ToList();
            var (_, _, hist) = CalculateMacdAt(subset, fast, slow, signal);
            result.Add(hist ?? 0m);
        }

        return result;
    }

    /// <summary>Find swing low/high points in a price series using 5-bar window.</summary>
    private static List<(int index, decimal price)> FindSwingPoints(List<decimal> data, bool isLow)
    {
        var points = new List<(int index, decimal price)>();
        if (data.Count < 5) return points;

        for (int i = 2; i < data.Count - 2; i++)
        {
            var center = data[i];
            bool isSwing = isLow
                ? center <= data[i - 1] && center <= data[i - 2] && center <= data[i + 1] && center <= data[i + 2]
                : center >= data[i - 1] && center >= data[i - 2] && center >= data[i + 1] && center >= data[i + 2];

            if (isSwing)
                points.Add((i, center));
        }

        return points;
    }

    private static FibonacciLevels? CalculateFibonacciLevels(
        List<decimal> supports, List<decimal> resistances)
    {
        // Need at least one swing low and one swing high
        if (supports.Count == 0 || resistances.Count == 0)
            return null;

        var swingLow = supports.Min();
        var swingHigh = resistances.Max();

        // Swing high must be meaningfully greater than swing low (at least 1% range)
        if (swingHigh <= swingLow)
            return null;

        var rangePercent = (swingHigh - swingLow) / swingHigh * 100;
        if (rangePercent < 1m)
            return null;

        var range = swingHigh - swingLow;

        return new FibonacciLevels
        {
            SwingHigh = Math.Round(swingHigh, 0),
            SwingLow = Math.Round(swingLow, 0),
            Retracement236 = Math.Round(swingLow + range * 0.236m, 0),
            Retracement382 = Math.Round(swingLow + range * 0.382m, 0),
            Retracement500 = Math.Round(swingLow + range * 0.500m, 0),
            Retracement618 = Math.Round(swingLow + range * 0.618m, 0),
            Retracement786 = Math.Round(swingLow + range * 0.786m, 0),
            Extension1272 = Math.Round(swingHigh + range * 0.272m, 0),
            Extension1618 = Math.Round(swingHigh + range * 0.618m, 0)
        };
    }
}
