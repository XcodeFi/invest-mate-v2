namespace InvestmentApp.Application.Common.Interfaces;

/// <summary>
/// In-process queue used to hand a freshly persisted backtest to the
/// BacktestQueueService background loop. Decouples the command handler
/// (which only needs to fire-and-forget the id) from the consumer.
/// </summary>
public interface IBacktestQueue
{
    ValueTask EnqueueAsync(string backtestId, CancellationToken cancellationToken = default);
}
