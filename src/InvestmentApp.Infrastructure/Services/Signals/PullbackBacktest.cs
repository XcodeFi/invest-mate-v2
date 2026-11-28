namespace InvestmentApp.Infrastructure.Services.Signals;

public sealed record PullbackBacktestResult(int BarCount, IReadOnlyList<int> Signals, IReadOnlyList<SimulatedTrade> Trades)
{
    public BacktestSummary Summary => SetupBacktester.Summarize(Trades);
}

/// <summary>Chuỗi nến → chỉ báo → tín hiệu Pullback → mô phỏng lệnh. Một đường duy nhất cho truy vấn, công cụ đếm và test.</summary>
public static class PullbackBacktest
{
    public static PullbackBacktestResult Run(IReadOnlyList<Bar> bars, MarketRules rules)
    {
        var series = IndicatorSeries.Build(bars);
        var signals = PullbackSetup.FindSignals(bars, series);
        var trades = SetupBacktester.Simulate(bars, signals, series.Atr14, rules);
        return new PullbackBacktestResult(bars.Count, signals, trades);
    }
}
