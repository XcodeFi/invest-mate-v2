using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Events;
using MediatR;

namespace InvestmentApp.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPortfolioSnapshotsAsync(stoppingToken);
                await FetchMarketPricesAsync(stoppingToken);
                await EvaluateScenarioPlaybooksAsync(stoppingToken);
                await CleanupExpiredTokensAsync(stoppingToken);

                // Run every 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker execution");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Takes daily snapshots of all portfolios using the SnapshotService.
    /// </summary>
    private async Task ProcessPortfolioSnapshotsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        try
        {
            var snapshotService = scope.ServiceProvider.GetRequiredService<ISnapshotService>();
            await snapshotService.TakeAllSnapshotsAsync(cancellationToken);
            _logger.LogInformation("Portfolio snapshots completed at {time}", DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing portfolio snapshots");
        }
    }

    /// <summary>
    /// Fetches current market prices and stores them in the database.
    /// </summary>
    private async Task FetchMarketPricesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        try
        {
            var portfolioRepository = scope.ServiceProvider.GetRequiredService<IPortfolioRepository>();
            var tradeRepository = scope.ServiceProvider.GetRequiredService<ITradeRepository>();
            var marketDataProvider = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();
            var stockPriceRepository = scope.ServiceProvider.GetRequiredService<IStockPriceRepository>();

            // Get all unique symbols from active portfolios
            var portfolios = await portfolioRepository.GetAllAsync(cancellationToken);
            var allSymbols = new HashSet<string>();

            foreach (var portfolio in portfolios)
            {
                var trades = await tradeRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
                foreach (var trade in trades)
                {
                    allSymbols.Add(trade.Symbol.ToUpper());
                }
            }

            if (!allSymbols.Any())
            {
                _logger.LogDebug("No symbols to fetch prices for");
                return;
            }

            // Batch fetch prices
            var prices = await marketDataProvider.GetBatchPricesAsync(allSymbols, cancellationToken);

            foreach (var (symbol, priceData) in prices)
            {
                var stockPrice = new Domain.Entities.StockPrice(
                    symbol: symbol,
                    date: priceData.Date,
                    open: priceData.Open,
                    high: priceData.High,
                    low: priceData.Low,
                    close: priceData.Close,
                    volume: priceData.Volume,
                    source: "MockProvider"
                );

                await stockPriceRepository.UpsertAsync(stockPrice, cancellationToken);
            }

            _logger.LogInformation("Fetched prices for {Count} symbols at {time}",
                prices.Count, DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market prices");
        }
    }

    /// <summary>
    /// Evaluates scenario playbook nodes for active trade plans.
    /// </summary>
    private async Task EvaluateScenarioPlaybooksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        try
        {
            var evaluator = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IScenarioEvaluationService>();
            var results = await evaluator.EvaluateAllAsync(cancellationToken);
            if (results.Count > 0)
                _logger.LogInformation("Scenario evaluation triggered {Count} nodes", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating scenario playbooks");
        }
    }

    private Task CleanupExpiredTokensAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Cleanup expired tokens check completed");
        return Task.CompletedTask;
    }
}
