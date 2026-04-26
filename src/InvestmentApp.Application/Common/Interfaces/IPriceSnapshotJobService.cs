namespace InvestmentApp.Application.Common.Interfaces;

/// <summary>
/// Encapsulates the price-snapshot job: fetch latest prices for symbols held across all
/// portfolios, persist them, refresh market indices, and evaluate stop-loss / target triggers.
///
/// Triggered on a schedule (Cloud Scheduler → /internal/jobs/prices) and idempotent within
/// a single market period — calling it twice in the same minute simply re-upserts the same
/// snapshot.
/// </summary>
public interface IPriceSnapshotJobService
{
    Task<PriceSnapshotJobResult> RunAsync(CancellationToken cancellationToken = default);
}

public sealed record PriceSnapshotJobResult(
    int SymbolsFetched,
    int PricesPersisted,
    int IndicesUpdated,
    int StopLossTriggered,
    int TargetsTriggered)
{
    public static PriceSnapshotJobResult Empty { get; } = new(0, 0, 0, 0, 0);
}
