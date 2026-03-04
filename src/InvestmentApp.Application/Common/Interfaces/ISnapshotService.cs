namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Service interface for taking and querying portfolio snapshots.
/// </summary>
public interface ISnapshotService
{
    Task TakeSnapshotAsync(string portfolioId, CancellationToken cancellationToken = default);
    Task TakeAllSnapshotsAsync(CancellationToken cancellationToken = default);
}
