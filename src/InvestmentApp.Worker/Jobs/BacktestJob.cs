using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Worker.Jobs;

/// <summary>
/// Picks up Pending backtests and runs them in the background.
/// Polls every 10 seconds. When a real message queue (e.g. Redis, RabbitMQ)
/// is available, replace polling with a queue consumer.
/// </summary>
public class BacktestJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BacktestJob> _logger;
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

    public BacktestJob(IServiceProvider services, ILogger<BacktestJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BacktestJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingAsync(stoppingToken);
            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var backtestRepo = scope.ServiceProvider.GetRequiredService<IBacktestRepository>();
        var strategyRepo = scope.ServiceProvider.GetRequiredService<IStrategyRepository>();
        var engine = scope.ServiceProvider.GetRequiredService<BacktestEngine>();

        var pending = await backtestRepo.GetPendingAsync(cancellationToken);

        foreach (var backtest in pending)
        {
            try
            {
                _logger.LogInformation("Processing backtest {Id}", backtest.Id);
                backtest.MarkRunning();
                await backtestRepo.UpdateAsync(backtest, cancellationToken);

                var strategy = await strategyRepo.GetByIdAsync(backtest.StrategyId, cancellationToken);
                if (strategy == null)
                {
                    backtest.Fail("Strategy not found");
                    await backtestRepo.UpdateAsync(backtest, cancellationToken);
                    continue;
                }

                var (result, trades) = await engine.RunAsync(backtest, strategy, cancellationToken);
                backtest.Complete(result, trades);
                await backtestRepo.UpdateAsync(backtest, cancellationToken);

                _logger.LogInformation("Backtest {Id} completed: TotalReturn={Return}%", backtest.Id, result.TotalReturn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backtest {Id} failed", backtest.Id);
                backtest.Fail(ex.Message);
                await backtestRepo.UpdateAsync(backtest, cancellationToken);
            }
        }
    }
}
