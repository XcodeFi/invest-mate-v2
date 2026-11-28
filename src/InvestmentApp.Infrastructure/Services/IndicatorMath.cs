namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Các hàm chỉ báo theo TỪNG PHIÊN dùng chung cho <see cref="TechnicalIndicatorService"/> (biểu đồ, thẻ chỉ báo) và bộ máy tín hiệu
/// (setup Pullback + backtest). Một nơi duy nhất để mọi chỗ ra cùng giá trị, kể cả cách làm tròn (EMA/ATR về đồng, RSI/ADX 1 chữ số).
/// Hàm thuần, không phụ thuộc dữ liệu tương lai: giá trị ở chỉ số i chỉ dùng dữ liệu từ 0..i.
/// </summary>
internal static class IndicatorMath
{
    internal static List<decimal?> NullSeries(int n) => Enumerable.Repeat<decimal?>(null, n).ToList();

    /// <summary>EMA từng phiên (seed = SMA của `period` điểm đầu); null trước khi đủ warm-up. Làm tròn như scalar.</summary>
    internal static List<decimal?> EmaSeries(List<decimal> data, int period)
    {
        var result = NullSeries(data.Count);
        if (data.Count < period) return result;

        decimal multiplier = 2.0m / (period + 1);
        // SMA as seed
        decimal ema = data.Take(period).Average();
        result[period - 1] = Math.Round(ema, 0);

        for (int i = period; i < data.Count; i++)
        {
            ema = (data[i] - ema) * multiplier + ema;
            result[i] = Math.Round(ema, 0);
        }

        return result;
    }

    internal static decimal RsiValue(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0) return 100;
        var rs = avgGain / avgLoss;
        return Math.Round(100 - (100 / (1 + rs)), 1);
    }

    /// <summary>RSI(Wilder) từng phiên; null trước khi có `period + 1` điểm.</summary>
    internal static List<decimal?> RsiSeriesFull(List<decimal> data, int period)
    {
        var result = NullSeries(data.Count);
        if (data.Count < period + 1) return result;

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
        result[period] = RsiValue(avgGain, avgLoss);

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
            result[i] = RsiValue(avgGain, avgLoss);
        }

        return result;
    }

    internal static List<decimal?> AtrSeries(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period)
    {
        var result = NullSeries(highs.Count);
        if (highs.Count < period + 1) return result;

        // True Range: trueRanges[j] ứng với phiên (j + 1)
        var trueRanges = new List<decimal>();
        for (int i = 1; i < highs.Count; i++)
        {
            var highLow = highs[i] - lows[i];
            var highPrevClose = Math.Abs(highs[i] - closes[i - 1]);
            var lowPrevClose = Math.Abs(lows[i] - closes[i - 1]);
            trueRanges.Add(Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose)));
        }

        // Initial ATR = simple average of first `period` true ranges
        decimal atr = trueRanges.Take(period).Average();
        result[period] = Math.Round(atr, 0);

        // Smoothed ATR
        for (int i = period; i < trueRanges.Count; i++)
        {
            atr = (atr * (period - 1) + trueRanges[i]) / period;
            result[i + 1] = Math.Round(atr, 0);
        }

        return result;
    }

    internal static (List<decimal?> adx, List<decimal?> plusDi, List<decimal?> minusDi) AdxSeries(
        List<decimal> highs, List<decimal> lows, List<decimal> closes, int period)
    {
        var adxResult = NullSeries(highs.Count);
        var plusResult = NullSeries(highs.Count);
        var minusResult = NullSeries(highs.Count);

        // Need at least 2 * period + 1 candles (period for DI smoothing + period for ADX smoothing)
        if (highs.Count < 2 * period + 1) return (adxResult, plusResult, minusResult);

        // Step 1: +DM, -DM, TR (phần tử j ứng với phiên j + 1)
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

        // Step 2: Wilder smoothing (period đầu là tổng, sau đó làm mượt)
        decimal smoothPlusDm = plusDmSeries.Take(period).Sum();
        decimal smoothMinusDm = minusDmSeries.Take(period).Sum();
        decimal smoothTr = trSeries.Take(period).Sum();

        // Step 3: ADX = trung bình DX `period` điểm đầu, sau đó làm mượt
        int dxCount = 0;
        decimal dxSum = 0, adx = 0;

        for (int i = period; i < trSeries.Count; i++)
        {
            if (i > period)
            {
                smoothPlusDm = smoothPlusDm - (smoothPlusDm / period) + plusDmSeries[i];
                smoothMinusDm = smoothMinusDm - (smoothMinusDm / period) + minusDmSeries[i];
                smoothTr = smoothTr - (smoothTr / period) + trSeries[i];
            }

            if (smoothTr != 0)
            {
                var plusDiVal = smoothPlusDm / smoothTr * 100;
                var minusDiVal = smoothMinusDm / smoothTr * 100;
                var diSum = plusDiVal + minusDiVal;
                var dx = diSum > 0 ? Math.Abs(plusDiVal - minusDiVal) / diSum * 100 : 0;

                dxCount++;
                if (dxCount <= period)
                {
                    dxSum += dx;
                    if (dxCount == period) adx = dxSum / period;
                }
                else
                {
                    adx = (adx * (period - 1) + dx) / period;
                }
            }

            if (dxCount < period) continue;

            var index = i + 1;
            adxResult[index] = Math.Round(adx, 1);
            plusResult[index] = Math.Round(smoothTr > 0 ? smoothPlusDm / smoothTr * 100 : 0, 1);
            minusResult[index] = Math.Round(smoothTr > 0 ? smoothMinusDm / smoothTr * 100 : 0, 1);
        }

        return (adxResult, plusResult, minusResult);
    }
}
