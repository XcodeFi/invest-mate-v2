using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

public class StrategyPerformanceService : IStrategyPerformanceService
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public StrategyPerformanceService(
        IStrategyRepository strategyRepository,
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository)
    {
        _strategyRepository = strategyRepository;
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<StrategyPerformanceSummary> GetPerformanceAsync(string strategyId, string userId, CancellationToken cancellationToken = default)
    {
        var strategy = await _strategyRepository.GetByIdAsync(strategyId, cancellationToken)
            ?? throw new Exception($"Strategy {strategyId} not found");

        // Get all portfolios for user, then all trades with this strategy
        var portfolios = await _portfolioRepository.GetByUserIdAsync(userId, cancellationToken);
        var allTrades = new List<Trade>();
        foreach (var portfolio in portfolios)
        {
            var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
            allTrades.AddRange(trades.Where(t => t.StrategyId == strategyId));
        }

        return CalculatePerformance(strategyId, strategy.Name, allTrades);
    }

    public async Task<IEnumerable<StrategyComparisonItem>> CompareStrategiesAsync(IEnumerable<string> strategyIds, string userId, CancellationToken cancellationToken = default)
    {
        var results = new List<StrategyComparisonItem>();

        foreach (var strategyId in strategyIds)
        {
            var perf = await GetPerformanceAsync(strategyId, userId, cancellationToken);
            results.Add(new StrategyComparisonItem
            {
                StrategyId = perf.StrategyId,
                StrategyName = perf.StrategyName,
                TotalTrades = perf.TotalTrades,
                WinRate = perf.WinRate,
                TotalPnL = perf.TotalPnL,
                ProfitFactor = perf.ProfitFactor
            });
        }

        return results;
    }

    private StrategyPerformanceSummary CalculatePerformance(string strategyId, string strategyName, List<Trade> trades)
    {
        var summary = new StrategyPerformanceSummary
        {
            StrategyId = strategyId,
            StrategyName = strategyName,
            TotalTrades = 0
        };

        if (trades.Count == 0) return summary;

        // Group trades by symbol to calculate P&L
        var tradesBySymbol = trades.GroupBy(t => t.Symbol);
        decimal totalPnL = 0;
        var pnlPerTrade = new List<decimal>();

        foreach (var group in tradesBySymbol)
        {
            var buys = group.Where(t => t.TradeType == TradeType.BUY).OrderBy(t => t.TradeDate).ToList();
            var sells = group.Where(t => t.TradeType == TradeType.SELL).OrderBy(t => t.TradeDate).ToList();

            if (buys.Count == 0 || sells.Count == 0) continue;

            decimal totalBuyQty = buys.Sum(b => b.Quantity);
            decimal totalBuyCost = buys.Sum(b => b.Quantity * b.Price + b.Fee + b.Tax);
            decimal avgCost = totalBuyCost / totalBuyQty;

            foreach (var sell in sells)
            {
                decimal pnl = sell.Quantity * (sell.Price - avgCost) - sell.Fee - sell.Tax;
                pnlPerTrade.Add(pnl);
                totalPnL += pnl;
            }
        }

        summary.TotalTrades = pnlPerTrade.Count;
        if (pnlPerTrade.Count == 0) return summary;

        var wins = pnlPerTrade.Where(p => p > 0).ToList();
        var losses = pnlPerTrade.Where(p => p <= 0).ToList();

        summary.WinningTrades = wins.Count;
        summary.LosingTrades = losses.Count;
        summary.WinRate = summary.TotalTrades > 0 ? (decimal)wins.Count / summary.TotalTrades * 100 : 0;
        summary.TotalPnL = totalPnL;
        summary.AveragePnL = totalPnL / pnlPerTrade.Count;
        summary.AverageWin = wins.Count > 0 ? wins.Average() : 0;
        summary.AverageLoss = losses.Count > 0 ? losses.Average() : 0;
        summary.LargestWin = wins.Count > 0 ? wins.Max() : 0;
        summary.LargestLoss = losses.Count > 0 ? losses.Min() : 0;

        decimal grossProfit = wins.Count > 0 ? wins.Sum() : 0;
        decimal grossLoss = losses.Count > 0 ? Math.Abs(losses.Sum()) : 0;
        summary.ProfitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? decimal.MaxValue : 0;

        return summary;
    }
}
