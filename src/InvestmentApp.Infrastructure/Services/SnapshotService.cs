using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Service for creating and managing portfolio snapshots.
/// Takes point-in-time snapshots of portfolio state for historical tracking.
/// </summary>
public class SnapshotService : ISnapshotService
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IPnLService _pnlService;
    private readonly ITradeRepository _tradeRepository;
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(
        IPortfolioRepository portfolioRepository,
        IPortfolioSnapshotRepository snapshotRepository,
        IPnLService pnlService,
        ITradeRepository tradeRepository,
        ICapitalFlowRepository capitalFlowRepository,
        ILogger<SnapshotService> logger)
    {
        _portfolioRepository = portfolioRepository;
        _snapshotRepository = snapshotRepository;
        _pnlService = pnlService;
        _tradeRepository = tradeRepository;
        _capitalFlowRepository = capitalFlowRepository;
        _logger = logger;
    }

    public async Task TakeSnapshotAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        if (portfolio == null)
        {
            _logger.LogWarning("Portfolio {PortfolioId} not found for snapshot", portfolioId);
            return;
        }

        try
        {
            // Calculate current P&L
            var pnlSummary = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, cancellationToken);

            // Get capital flows to calculate cash balance
            var totalFlows = await _capitalFlowRepository.GetTotalFlowByPortfolioIdAsync(portfolioId, cancellationToken);
            var cashBalance = portfolio.InitialCapital + totalFlows - pnlSummary.TotalInvested;

            // Calculate returns
            var totalCapital = portfolio.InitialCapital + totalFlows;
            var totalValue = pnlSummary.TotalPortfolioValue + cashBalance;

            // Get previous snapshot for daily return calculation
            var previousSnapshot = await _snapshotRepository.GetLatestByPortfolioIdAsync(portfolioId, cancellationToken);
            decimal dailyReturn = 0;
            decimal cumulativeReturn = totalCapital > 0 ? ((totalValue - totalCapital) / totalCapital) * 100 : 0;

            if (previousSnapshot != null && previousSnapshot.TotalValue > 0)
            {
                dailyReturn = ((totalValue - previousSnapshot.TotalValue) / previousSnapshot.TotalValue) * 100;
            }

            // Build position snapshots
            var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
            var positionSnapshots = new List<PositionSnapshotItem>();
            var tradesBySymbol = trades.GroupBy(t => t.Symbol);

            foreach (var symbolGroup in tradesBySymbol)
            {
                try
                {
                    var positionPnL = await _pnlService.CalculatePositionPnLAsync(
                        portfolioId,
                        new Domain.ValueObjects.StockSymbol(symbolGroup.Key),
                        cancellationToken);

                    if (positionPnL.Quantity > 0)
                    {
                        positionSnapshots.Add(new PositionSnapshotItem
                        {
                            Symbol = positionPnL.Symbol,
                            Quantity = positionPnL.Quantity,
                            AverageCost = positionPnL.AverageCost,
                            MarketPrice = positionPnL.CurrentPrice,
                            MarketValue = positionPnL.MarketValue,
                            UnrealizedPnL = positionPnL.UnrealizedPnL,
                            Weight = totalValue > 0 ? (positionPnL.MarketValue / totalValue) * 100 : 0
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error calculating position for {Symbol} in portfolio {PortfolioId}",
                        symbolGroup.Key, portfolioId);
                }
            }

            var snapshot = new PortfolioSnapshotEntity(
                portfolioId: portfolioId,
                snapshotDate: DateTime.UtcNow.Date,
                totalValue: Math.Round(totalValue, 2),
                cashBalance: Math.Round(cashBalance, 2),
                investedValue: Math.Round(pnlSummary.TotalInvested, 2),
                unrealizedPnL: Math.Round(pnlSummary.TotalUnrealizedPnL, 2),
                realizedPnL: Math.Round(pnlSummary.TotalRealizedPnL, 2),
                dailyReturn: Math.Round(dailyReturn, 4),
                cumulativeReturn: Math.Round(cumulativeReturn, 4),
                positions: positionSnapshots
            );

            await _snapshotRepository.UpsertAsync(snapshot, cancellationToken);

            _logger.LogInformation(
                "Snapshot taken for portfolio {PortfolioId}: Value={TotalValue}, Daily={DailyReturn}%",
                portfolioId, totalValue, dailyReturn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error taking snapshot for portfolio {PortfolioId}", portfolioId);
        }
    }

    public async Task TakeAllSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        var portfolios = await _portfolioRepository.GetAllAsync(cancellationToken);

        foreach (var portfolio in portfolios)
        {
            await TakeSnapshotAsync(portfolio.Id, cancellationToken);
        }

        _logger.LogInformation("Completed taking snapshots for all portfolios");
    }
}
