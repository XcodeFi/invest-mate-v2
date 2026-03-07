using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Logging;
using EquityCurvePoint = InvestmentApp.Domain.Entities.EquityCurvePoint;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Simple backtesting engine. Replays historical prices against a strategy's
/// entry/exit rules expressed as buy-and-hold over the requested period.
///
/// TODO: extend with real rule parsing when Strategy rules DSL is defined.
/// Currently implements a basic buy-and-hold simulation for the strategy's
/// associated symbols derived from existing trades.
/// </summary>
public class BacktestEngine
{
    private readonly IStockPriceRepository _priceRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly ILogger<BacktestEngine> _logger;

    public BacktestEngine(
        IStockPriceRepository priceRepository,
        ITradeRepository tradeRepository,
        ILogger<BacktestEngine> logger)
    {
        _priceRepository = priceRepository;
        _tradeRepository = tradeRepository;
        _logger = logger;
    }

    public async Task<(BacktestResult result, List<SimulatedTrade> trades)> RunAsync(
        Backtest backtest, Strategy strategy, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running backtest {Id} for strategy {Strategy}", backtest.Id, strategy.Name);

        // Derive symbols from the strategy name (simplification)
        // In a full implementation, strategy rules would encode the symbol universe
        var symbols = await GetStrategySymbolsAsync(strategy, cancellationToken);

        var simulatedTrades = new List<SimulatedTrade>();
        decimal capital = backtest.InitialCapital;
        var equityCurve = new List<EquityCurvePoint>();

        foreach (var symbol in symbols)
        {
            var prices = (await _priceRepository.GetBySymbolAsync(
                symbol, backtest.StartDate, backtest.EndDate, cancellationToken)).ToList();

            if (prices.Count < 2) continue;

            var entry = prices.First();
            var exit = prices.Last();
            var quantity = Math.Floor(capital * 0.1m / entry.Close); // 10% capital per position
            if (quantity <= 0) continue;

            var cost = quantity * entry.Close;
            var proceeds = quantity * exit.Close;
            var pnl = proceeds - cost;
            var returnPct = cost > 0 ? pnl / cost : 0;

            simulatedTrades.Add(new SimulatedTrade
            {
                Symbol = symbol,
                Type = TradeType.BUY,
                EntryPrice = entry.Close,
                ExitPrice = exit.Close,
                Quantity = quantity,
                EntryDate = entry.Date,
                ExitDate = exit.Date,
                PnL = Math.Round(pnl, 2),
                ReturnPercent = Math.Round(returnPct * 100, 4)
            });
        }

        // Build equity curve (daily)
        equityCurve = BuildEquityCurve(backtest.InitialCapital, simulatedTrades, backtest.StartDate, backtest.EndDate);
        var result = ComputeResult(backtest.InitialCapital, simulatedTrades, equityCurve, backtest.StartDate, backtest.EndDate);

        return (result, simulatedTrades);
    }

    private async Task<List<string>> GetStrategySymbolsAsync(Strategy strategy, CancellationToken cancellationToken)
    {
        // Placeholder: use common VN blue chips; replace with strategy rule parsing
        return new List<string> { "VNM", "FPT", "VCB", "HPG", "MWG" };
    }

    private static List<EquityCurvePoint> BuildEquityCurve(
        decimal initialCapital, List<SimulatedTrade> trades, DateTime start, DateTime end)
    {
        var points = new List<EquityCurvePoint>();
        decimal cumReturn = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            var dayPnl = trades
                .Where(t => t.EntryDate.Date <= date && t.ExitDate.Date >= date)
                .Sum(t => t.PnL / Math.Max(1, (t.ExitDate - t.EntryDate).Days));

            var dailyReturn = initialCapital > 0 ? dayPnl / initialCapital : 0;
            cumReturn += dailyReturn;

            points.Add(new EquityCurvePoint
            {
                Date = date,
                PortfolioValue = Math.Round(initialCapital * (1 + cumReturn), 2),
                DailyReturn = Math.Round(dailyReturn * 100, 4),
                CumulativeReturn = Math.Round(cumReturn * 100, 4)
            });
        }

        return points;
    }

    private static BacktestResult ComputeResult(
        decimal initialCapital, List<SimulatedTrade> trades,
        List<EquityCurvePoint> curve, DateTime start, DateTime end)
    {
        var totalPnl = trades.Sum(t => t.PnL);
        var finalValue = initialCapital + totalPnl;
        var totalReturn = initialCapital > 0 ? totalPnl / initialCapital : 0;
        var years = Math.Max((end - start).TotalDays / 365.25, 0.01);
        var cagr = (double)finalValue / (double)initialCapital > 0
            ? (decimal)(Math.Pow((double)(finalValue / initialCapital), 1.0 / years) - 1)
            : 0;

        var winners = trades.Where(t => t.PnL > 0).ToList();
        var losers = trades.Where(t => t.PnL <= 0).ToList();
        var grossProfit = winners.Sum(t => t.PnL);
        var grossLoss = Math.Abs(losers.Sum(t => t.PnL));
        var profitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? 100m : 0m;

        // Max drawdown from equity curve
        var maxDrawdown = 0m;
        var peak = 0m;
        foreach (var pt in curve)
        {
            if (pt.PortfolioValue > peak) peak = pt.PortfolioValue;
            var dd = peak > 0 ? (peak - pt.PortfolioValue) / peak : 0;
            if (dd > maxDrawdown) maxDrawdown = dd;
        }

        // Sharpe (simplified, risk-free = 5% annual)
        var dailyReturns = curve.Select(p => p.DailyReturn / 100m).ToList();
        var avgDaily = dailyReturns.Any() ? dailyReturns.Average() : 0;
        var stdDaily = dailyReturns.Count > 1
            ? (decimal)Math.Sqrt((double)dailyReturns.Average(r => (r - avgDaily) * (r - avgDaily)))
            : 0;
        var riskFreeDaily = 0.05m / 252;
        var sharpe = stdDaily > 0 ? (avgDaily - riskFreeDaily) / stdDaily * (decimal)Math.Sqrt(252) : 0;

        return new BacktestResult
        {
            FinalValue = Math.Round(finalValue, 2),
            TotalReturn = Math.Round(totalReturn * 100, 4),
            CAGR = Math.Round(cagr * 100, 4),
            SharpeRatio = Math.Round(sharpe, 4),
            MaxDrawdown = Math.Round(maxDrawdown * 100, 4),
            WinRate = trades.Count > 0 ? Math.Round((decimal)winners.Count / trades.Count * 100, 2) : 0,
            ProfitFactor = Math.Round(profitFactor, 4),
            TotalTrades = trades.Count,
            WinningTrades = winners.Count,
            LosingTrades = losers.Count,
            EquityCurve = curve
        };
    }
}
