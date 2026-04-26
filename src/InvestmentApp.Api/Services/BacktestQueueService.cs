using System.Threading.Channels;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Api.Services;

/// <summary>
/// Singleton background service that consumes a fire-and-forget queue of backtest ids
/// and runs each through <see cref="BacktestEngine"/> in a fresh DI scope.
///
/// On startup it recovers any backtests left in <c>Pending</c> from a previous instance
/// (e.g. Cloud Run scale-down mid-run) so no work is lost.
///
/// Replaces the polling-based <c>BacktestJob</c> that ran in the separate Worker service —
/// in-process queue is sufficient for a solo-user deployment and lets us remove the
/// always-on Worker container (free-tier friendly).
/// </summary>
public class BacktestQueueService : BackgroundService, IBacktestQueue
{
    private readonly Channel<string> _channel;
    private readonly IServiceProvider _services;
    private readonly ILogger<BacktestQueueService> _logger;

    public BacktestQueueService(IServiceProvider services, ILogger<BacktestQueueService> logger)
    {
        _services = services;
        _logger = logger;
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(string backtestId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(backtestId, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BacktestQueueService started");

        await RecoverPendingAsync(stoppingToken);

        await foreach (var backtestId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunSingleAsync(backtestId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backtest {Id} failed", backtestId);
            }
        }
    }

    private async Task RecoverPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var backtestRepo = scope.ServiceProvider.GetRequiredService<IBacktestRepository>();
            var pending = await backtestRepo.GetPendingAsync(cancellationToken);
            var count = 0;
            foreach (var b in pending)
            {
                await EnqueueAsync(b.Id, cancellationToken);
                count++;
            }
            if (count > 0)
                _logger.LogInformation("Recovered {Count} Pending backtests on startup", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BacktestQueueService recovery failed");
        }
    }

    private async Task RunSingleAsync(string backtestId, CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var backtestRepo = scope.ServiceProvider.GetRequiredService<IBacktestRepository>();
        var strategyRepo = scope.ServiceProvider.GetRequiredService<IStrategyRepository>();
        var engine = scope.ServiceProvider.GetRequiredService<BacktestEngine>();

        var backtest = await backtestRepo.GetByIdAsync(backtestId, cancellationToken);
        if (backtest == null)
        {
            _logger.LogWarning("Backtest {Id} not found, skipping", backtestId);
            return;
        }
        if (backtest.Status != BacktestStatus.Pending)
        {
            _logger.LogInformation("Backtest {Id} already in status {Status}, skipping",
                backtestId, backtest.Status);
            return;
        }

        backtest.MarkRunning();
        await backtestRepo.UpdateAsync(backtest, cancellationToken);

        var strategy = await strategyRepo.GetByIdAsync(backtest.StrategyId, cancellationToken);
        if (strategy == null)
        {
            backtest.Fail("Strategy not found");
            await backtestRepo.UpdateAsync(backtest, cancellationToken);
            return;
        }

        try
        {
            var (result, trades) = await engine.RunAsync(backtest, strategy, cancellationToken);
            backtest.Complete(result, trades);
            await backtestRepo.UpdateAsync(backtest, cancellationToken);
            _logger.LogInformation("Backtest {Id} completed: TotalReturn={Return}%",
                backtest.Id, result.TotalReturn);
        }
        catch (Exception ex)
        {
            backtest.Fail(ex.Message);
            await backtestRepo.UpdateAsync(backtest, cancellationToken);
            throw;
        }
    }
}
