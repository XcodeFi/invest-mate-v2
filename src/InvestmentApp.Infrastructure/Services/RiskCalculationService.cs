using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Service for calculating position-level and portfolio-level risk metrics.
/// </summary>
public class RiskCalculationService : IRiskCalculationService
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IStockPriceService _stockPriceService;
    private readonly IStopLossTargetRepository _stopLossTargetRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IStockPriceRepository _stockPriceRepository;
    private readonly IPnLService _pnlService;
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly IRiskProfileRepository _riskProfileRepository;
    private readonly IFundamentalDataProvider _fundamentalDataProvider;
    private readonly ILogger<RiskCalculationService> _logger;

    public RiskCalculationService(
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository,
        IStockPriceService stockPriceService,
        IStopLossTargetRepository stopLossTargetRepository,
        IPortfolioSnapshotRepository snapshotRepository,
        IStockPriceRepository stockPriceRepository,
        IPnLService pnlService,
        ICapitalFlowRepository capitalFlowRepository,
        IRiskProfileRepository riskProfileRepository,
        IFundamentalDataProvider fundamentalDataProvider,
        ILogger<RiskCalculationService> logger)
    {
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
        _stockPriceService = stockPriceService;
        _stopLossTargetRepository = stopLossTargetRepository;
        _snapshotRepository = snapshotRepository;
        _stockPriceRepository = stockPriceRepository;
        _pnlService = pnlService;
        _capitalFlowRepository = capitalFlowRepository;
        _riskProfileRepository = riskProfileRepository;
        _fundamentalDataProvider = fundamentalDataProvider;
        _logger = logger;
    }

    public async Task<decimal> CalculatePositionSizePercentAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default)
    {
        var pnlSummary = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, cancellationToken);
        if (pnlSummary.TotalPortfolioValue <= 0) return 0;

        try
        {
            var positionPnl = await _pnlService.CalculatePositionPnLAsync(portfolioId, new StockSymbol(symbol), cancellationToken);
            return (positionPnl.MarketValue / pnlSummary.TotalPortfolioValue) * 100;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<PortfolioRiskSummary> GetPortfolioRiskSummaryAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        if (portfolio == null)
            throw new ArgumentException("Portfolio not found", nameof(portfolioId));

        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var tradesBySymbol = trades.GroupBy(t => t.Symbol).ToList();
        var stopLossTargets = await _stopLossTargetRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var slTargetMap = stopLossTargets
            .GroupBy(s => s.Symbol)
            .ToDictionary(g => g.Key, g => g.Last());

        // Calculate total portfolio value
        var pnlSummary = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, cancellationToken);
        var totalFlows = await _capitalFlowRepository.GetTotalFlowByPortfolioIdAsync(portfolioId, cancellationToken);
        var cashBalance = portfolio.InitialCapital + totalFlows - pnlSummary.TotalInvested;
        // Use the larger of net worth or total market value as denominator for position sizing,
        // ensuring position percentages never exceed 100%
        var totalValue = Math.Max(pnlSummary.TotalPortfolioValue + cashBalance, pnlSummary.TotalPortfolioValue);

        var positions = new List<PositionRiskItem>();
        foreach (var symbolGroup in tradesBySymbol)
        {
            try
            {
                var positionPnl = await _pnlService.CalculatePositionPnLAsync(
                    portfolioId, new StockSymbol(symbolGroup.Key), cancellationToken);

                if (positionPnl.Quantity <= 0) continue;

                var positionSizePercent = totalValue > 0 ? (positionPnl.MarketValue / totalValue) * 100 : 0;
                slTargetMap.TryGetValue(symbolGroup.Key, out var slTarget);

                var item = new PositionRiskItem
                {
                    Symbol = symbolGroup.Key,
                    Quantity = positionPnl.Quantity,
                    CurrentPrice = positionPnl.CurrentPrice,
                    MarketValue = positionPnl.MarketValue,
                    PositionSizePercent = positionSizePercent,
                    StopLossPrice = slTarget?.StopLossPrice,
                    TargetPrice = slTarget?.TargetPrice,
                    RiskRewardRatio = slTarget?.GetRiskRewardRatio(),
                    RiskPerShare = slTarget?.GetRiskPerShare(),
                    RiskAmount = slTarget != null ? slTarget.GetRiskPerShare() * positionPnl.Quantity : null,
                    DistanceToStopLossPercent = slTarget != null && positionPnl.CurrentPrice > 0
                        ? ((positionPnl.CurrentPrice - slTarget.StopLossPrice) / positionPnl.CurrentPrice) * 100 : 0,
                    DistanceToTargetPercent = slTarget != null && positionPnl.CurrentPrice > 0
                        ? ((slTarget.TargetPrice - positionPnl.CurrentPrice) / positionPnl.CurrentPrice) * 100 : 0
                };
                positions.Add(item);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating risk for {Symbol} in portfolio {PortfolioId}",
                    symbolGroup.Key, portfolioId);
            }
        }

        // Get drawdown and VaR
        var drawdown = await CalculateMaxDrawdownAsync(portfolioId, cancellationToken);
        decimal var95 = 0;
        try { var95 = await CalculateValueAtRiskAsync(portfolioId, cancellationToken); } catch { }

        return new PortfolioRiskSummary
        {
            PortfolioId = portfolioId,
            TotalValue = totalValue,
            Positions = positions,
            MaxDrawdown = drawdown.MaxDrawdownPercent,
            ValueAtRisk95 = var95,
            LargestPositionPercent = positions.Any() ? positions.Max(p => p.PositionSizePercent) : 0,
            PositionCount = positions.Count
        };
    }

    public async Task<DrawdownResult> CalculateMaxDrawdownAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.UtcNow.AddYears(-5), DateTime.UtcNow, cancellationToken);
        var sortedSnapshots = snapshots.OrderBy(s => s.SnapshotDate).ToList();

        var result = new DrawdownResult { PortfolioId = portfolioId };

        if (sortedSnapshots.Count < 2) return result;

        decimal peak = sortedSnapshots[0].TotalValue;
        DateTime peakDate = sortedSnapshots[0].SnapshotDate;
        decimal maxDrawdown = 0;
        DateTime? maxDrawdownPeakDate = null;
        decimal? maxDrawdownPeakValue = null;
        DateTime? maxDrawdownTroughDate = null;
        decimal? maxDrawdownTroughValue = null;

        foreach (var snapshot in sortedSnapshots)
        {
            if (snapshot.TotalValue > peak)
            {
                peak = snapshot.TotalValue;
                peakDate = snapshot.SnapshotDate;
            }

            var drawdown = peak > 0 ? ((peak - snapshot.TotalValue) / peak) * 100 : 0;
            result.DrawdownSeries.Add(new DrawdownPoint
            {
                Date = snapshot.SnapshotDate,
                Value = snapshot.TotalValue,
                DrawdownPercent = drawdown
            });

            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
                maxDrawdownPeakDate = peakDate;
                maxDrawdownPeakValue = peak;
                maxDrawdownTroughDate = snapshot.SnapshotDate;
                maxDrawdownTroughValue = snapshot.TotalValue;
            }
        }

        // Current drawdown
        var lastSnapshot = sortedSnapshots.Last();
        var currentDrawdown = peak > 0 ? ((peak - lastSnapshot.TotalValue) / peak) * 100 : 0;

        result.MaxDrawdownPercent = maxDrawdown;
        result.CurrentDrawdownPercent = currentDrawdown;
        result.PeakDate = maxDrawdownPeakDate;
        result.PeakValue = maxDrawdownPeakValue;
        result.TroughDate = maxDrawdownTroughDate;
        result.TroughValue = maxDrawdownTroughValue;

        return result;
    }

    public async Task<decimal> CalculateValueAtRiskAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        // VaR (95%) = μ - 1.645σ (parametric method)
        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            portfolioId, DateTime.UtcNow.AddYears(-1), DateTime.UtcNow, cancellationToken);
        var sortedSnapshots = snapshots.OrderBy(s => s.SnapshotDate).ToList();

        if (sortedSnapshots.Count < 10) return 0;

        // Calculate daily returns
        var dailyReturns = new List<decimal>();
        for (int i = 1; i < sortedSnapshots.Count; i++)
        {
            if (sortedSnapshots[i - 1].TotalValue > 0)
            {
                var dailyReturn = (sortedSnapshots[i].TotalValue - sortedSnapshots[i - 1].TotalValue) / sortedSnapshots[i - 1].TotalValue;
                dailyReturns.Add(dailyReturn);
            }
        }

        if (dailyReturns.Count < 5) return 0;

        var mean = dailyReturns.Average();
        var variance = dailyReturns.Sum(r => (r - mean) * (r - mean)) / dailyReturns.Count;
        var stdDev = (decimal)Math.Sqrt((double)variance);

        // VaR at 95% confidence = μ - 1.645σ (as percentage)
        var var95 = (mean - 1.645m * stdDev) * 100;
        return Math.Abs(var95);
    }

    public async Task<CorrelationMatrix> CalculateCorrelationMatrixAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var symbols = trades.Select(t => t.Symbol).Distinct().ToList();

        var result = new CorrelationMatrix
        {
            PortfolioId = portfolioId,
            Symbols = symbols
        };

        if (symbols.Count < 2) return result;

        // Get price history for each symbol (last 90 days)
        var from = DateTime.UtcNow.AddDays(-90);
        var to = DateTime.UtcNow;
        var priceHistories = new Dictionary<string, List<decimal>>();

        foreach (var symbol in symbols)
        {
            var prices = await _stockPriceRepository.GetBySymbolAsync(symbol, from, to, cancellationToken);
            var priceList = prices.OrderBy(p => p.Date).Select(p => p.Close).ToList();
            if (priceList.Count > 1)
            {
                // Convert to returns
                var returns = new List<decimal>();
                for (int i = 1; i < priceList.Count; i++)
                {
                    if (priceList[i - 1] > 0)
                        returns.Add((priceList[i] - priceList[i - 1]) / priceList[i - 1]);
                }
                priceHistories[symbol] = returns;
            }
        }

        // Calculate correlation pairs
        var pairs = new List<CorrelationPair>();
        for (int i = 0; i < symbols.Count; i++)
        {
            for (int j = i + 1; j < symbols.Count; j++)
            {
                if (priceHistories.TryGetValue(symbols[i], out var returns1) &&
                    priceHistories.TryGetValue(symbols[j], out var returns2))
                {
                    var correlation = CalculateCorrelation(returns1, returns2);
                    pairs.Add(new CorrelationPair
                    {
                        Symbol1 = symbols[i],
                        Symbol2 = symbols[j],
                        Correlation = correlation
                    });
                }
            }
        }

        result.Pairs = pairs;
        return result;
    }

    public async Task<PortfolioOptimizationResult> GetPortfolioOptimizationAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var result = new PortfolioOptimizationResult { PortfolioId = portfolioId };

        // Get risk profile (or use defaults)
        var riskProfile = await _riskProfileRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var maxPositionSize = riskProfile?.MaxPositionSizePercent ?? 20m;
        var maxSectorExposure = riskProfile?.MaxSectorExposurePercent ?? 40m;

        // Get trades and build position data
        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var symbols = trades.Select(t => t.Symbol).Distinct().ToList();

        if (!symbols.Any())
        {
            result.DiversificationScore = 0m;
            return result;
        }

        // Calculate portfolio value
        var pnlSummary = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, cancellationToken);
        var totalFlows = await _capitalFlowRepository.GetTotalFlowByPortfolioIdAsync(portfolioId, cancellationToken);
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        var cashBalance = (portfolio?.InitialCapital ?? 0) + totalFlows - pnlSummary.TotalInvested;
        var totalValue = Math.Max(pnlSummary.TotalPortfolioValue + cashBalance, pnlSummary.TotalPortfolioValue);
        result.TotalValue = totalValue;

        if (totalValue <= 0)
        {
            result.DiversificationScore = 0m;
            return result;
        }

        // Build position data with sector info
        var positionData = new List<(string Symbol, decimal MarketValue, decimal PositionPercent, string? Sector)>();
        foreach (var symbol in symbols)
        {
            try
            {
                var positionPnl = await _pnlService.CalculatePositionPnLAsync(
                    portfolioId, new StockSymbol(symbol), cancellationToken);
                if (positionPnl.Quantity <= 0) continue;

                var positionPercent = (positionPnl.MarketValue / totalValue) * 100;

                // Fetch sector data
                string? sector = null;
                try
                {
                    var fundamentals = await _fundamentalDataProvider.GetFundamentalsAsync(symbol, cancellationToken);
                    sector = fundamentals?.Industry;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch fundamentals for {Symbol}", symbol);
                }

                positionData.Add((symbol, positionPnl.MarketValue, positionPercent, sector));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating optimization for {Symbol}", symbol);
            }
        }

        // 1. Concentration alerts
        foreach (var pos in positionData)
        {
            if (pos.PositionPercent > maxPositionSize)
            {
                var severity = pos.PositionPercent > maxPositionSize * 1.5m ? "danger" : "warning";
                result.ConcentrationAlerts.Add(new ConcentrationAlert
                {
                    Symbol = pos.Symbol,
                    PositionPercent = Math.Round(pos.PositionPercent, 2),
                    Limit = maxPositionSize,
                    Severity = severity
                });
            }
        }

        // 2. Sector diversification
        var sectorGroups = positionData
            .Where(p => !string.IsNullOrEmpty(p.Sector))
            .GroupBy(p => p.Sector!)
            .ToList();

        foreach (var group in sectorGroups)
        {
            var sectorValue = group.Sum(p => p.MarketValue);
            var exposurePercent = (sectorValue / totalValue) * 100;
            result.SectorExposures.Add(new SectorExposure
            {
                Sector = group.Key,
                Symbols = group.Select(p => p.Symbol).ToList(),
                TotalValue = sectorValue,
                ExposurePercent = Math.Round(exposurePercent, 2),
                Limit = maxSectorExposure,
                IsOverweight = exposurePercent > maxSectorExposure
            });
        }

        // Add "Không xác định" sector for positions without sector data
        var unknownSector = positionData.Where(p => string.IsNullOrEmpty(p.Sector)).ToList();
        if (unknownSector.Any())
        {
            var sectorValue = unknownSector.Sum(p => p.MarketValue);
            result.SectorExposures.Add(new SectorExposure
            {
                Sector = "Không xác định",
                Symbols = unknownSector.Select(p => p.Symbol).ToList(),
                TotalValue = sectorValue,
                ExposurePercent = Math.Round((sectorValue / totalValue) * 100, 2),
                Limit = maxSectorExposure,
                IsOverweight = false
            });
        }

        // 3. Correlation warnings
        var correlationMatrix = await CalculateCorrelationMatrixAsync(portfolioId, cancellationToken);
        foreach (var pair in correlationMatrix.Pairs)
        {
            if (Math.Abs(pair.Correlation) > 0.5m)
            {
                result.CorrelationWarnings.Add(new CorrelationWarning
                {
                    Symbol1 = pair.Symbol1,
                    Symbol2 = pair.Symbol2,
                    Correlation = Math.Round(pair.Correlation, 4),
                    RiskLevel = Math.Abs(pair.Correlation) > 0.7m ? "high" : "medium"
                });
            }
        }

        // 4. Diversification score (0-100)
        result.DiversificationScore = CalculateDiversificationScore(
            positionData, sectorGroups.Count, result.CorrelationWarnings.Count,
            result.ConcentrationAlerts.Count, result.SectorExposures.Count(s => s.IsOverweight));

        // 5. Recommendations
        result.Recommendations = GenerateRecommendations(result, maxPositionSize, maxSectorExposure);

        return result;
    }

    public async Task<TrailingStopAlertsResult> GetTrailingStopAlertsAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var result = new TrailingStopAlertsResult { PortfolioId = portfolioId };

        var slTargets = await _stopLossTargetRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);

        // Filter to active trailing stops only
        var activeTrailingStops = slTargets
            .Where(s => s.TrailingStopPercent.HasValue && s.TrailingStopPercent > 0
                        && !s.IsStopLossTriggered && !s.IsTargetTriggered)
            .ToList();

        result.TotalActiveTrailingStops = activeTrailingStops.Count;

        foreach (var target in activeTrailingStops)
        {
            try
            {
                var currentPriceMoney = await _stockPriceService.GetCurrentPriceAsync(
                    new StockSymbol(target.Symbol));
                var currentPrice = currentPriceMoney.Amount;

                if (currentPrice <= 0 || !target.TrailingStopPrice.HasValue) continue;

                var trailingStopPrice = target.TrailingStopPrice.Value;
                var distancePercent = currentPrice > 0
                    ? ((currentPrice - trailingStopPrice) / currentPrice) * 100
                    : 0;

                // Check if price has risen → suggest new trailing stop
                var newTrailingStopPrice = currentPrice * (1 - target.TrailingStopPercent!.Value / 100);
                var shouldUpdate = newTrailingStopPrice > trailingStopPrice;

                var severity = distancePercent <= 2 ? "danger" : distancePercent <= 5 ? "warning" : "safe";

                result.Alerts.Add(new TrailingStopAlert
                {
                    Symbol = target.Symbol,
                    TradeId = target.TradeId,
                    EntryPrice = target.EntryPrice,
                    CurrentPrice = currentPrice,
                    TrailingStopPercent = target.TrailingStopPercent.Value,
                    TrailingStopPrice = trailingStopPrice,
                    DistancePercent = Math.Round(distancePercent, 2),
                    Severity = severity,
                    ShouldUpdatePrice = shouldUpdate,
                    NewTrailingStopPrice = shouldUpdate ? Math.Round(newTrailingStopPrice, 0) : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking trailing stop for {Symbol}", target.Symbol);
            }
        }

        result.AlertCount = result.Alerts.Count(a => a.Severity != "safe");
        return result;
    }

    private static decimal CalculateDiversificationScore(
        List<(string Symbol, decimal MarketValue, decimal PositionPercent, string? Sector)> positions,
        int sectorCount, int highCorrelationCount, int concentrationAlertCount, int overweightSectorCount)
    {
        if (!positions.Any()) return 0m;

        decimal score = 100m;

        // Penalize for concentration (each alert costs 15 points)
        score -= concentrationAlertCount * 15m;

        // Penalize for sector overweight (each costs 10 points)
        score -= overweightSectorCount * 10m;

        // Penalize for high correlation (each costs 5 points)
        score -= highCorrelationCount * 5m;

        // Bonus for sector diversity (more unique sectors = better)
        if (sectorCount >= 4) score = Math.Min(score + 10m, 100m);
        else if (sectorCount >= 3) score = Math.Min(score + 5m, 100m);

        // Bonus for number of positions (3-8 is ideal)
        if (positions.Count >= 3 && positions.Count <= 8) score = Math.Min(score + 5m, 100m);

        // Penalize for too few positions
        if (positions.Count == 1) score -= 20m;

        return Math.Max(0m, Math.Min(100m, Math.Round(score, 1)));
    }

    private static List<string> GenerateRecommendations(
        PortfolioOptimizationResult result, decimal maxPositionSize, decimal maxSectorExposure)
    {
        var recommendations = new List<string>();

        foreach (var alert in result.ConcentrationAlerts)
        {
            recommendations.Add(
                $"Giảm tỷ trọng {alert.Symbol} ({alert.PositionPercent:F1}% > giới hạn {maxPositionSize:F0}%)");
        }

        foreach (var sector in result.SectorExposures.Where(s => s.IsOverweight))
        {
            recommendations.Add(
                $"Ngành {sector.Sector} chiếm {sector.ExposurePercent:F1}% danh mục (giới hạn {maxSectorExposure:F0}%), cân nhắc đa dạng hóa");
        }

        foreach (var warning in result.CorrelationWarnings.Where(w => w.RiskLevel == "high"))
        {
            recommendations.Add(
                $"{warning.Symbol1} và {warning.Symbol2} tương quan cao ({warning.Correlation:F2}), rủi ro tập trung");
        }

        if (result.DiversificationScore >= 80)
            recommendations.Add("Danh mục đa dạng hóa tốt");

        return recommendations;
    }

    private static decimal CalculateCorrelation(List<decimal> x, List<decimal> y)
    {
        var n = Math.Min(x.Count, y.Count);
        if (n < 5) return 0;

        var xSubset = x.Take(n).ToList();
        var ySubset = y.Take(n).ToList();

        var meanX = xSubset.Average();
        var meanY = ySubset.Average();

        decimal covXY = 0, varX = 0, varY = 0;
        for (int i = 0; i < n; i++)
        {
            var dx = xSubset[i] - meanX;
            var dy = ySubset[i] - meanY;
            covXY += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        if (varX == 0 || varY == 0) return 0;
        return covXY / ((decimal)Math.Sqrt((double)(varX * varY)));
    }
}
