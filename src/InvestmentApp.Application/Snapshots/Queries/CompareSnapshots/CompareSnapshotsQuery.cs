using MediatR;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Snapshots.Queries.GetSnapshotAtDate;

namespace InvestmentApp.Application.Snapshots.Queries.CompareSnapshots;

public class CompareSnapshotsQuery : IRequest<SnapshotComparisonDto>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public DateTime Date1 { get; set; }
    public DateTime Date2 { get; set; }
}

public class SnapshotComparisonDto
{
    public string PortfolioId { get; set; } = null!;
    public SnapshotDto? Snapshot1 { get; set; }
    public SnapshotDto? Snapshot2 { get; set; }
    public decimal ValueChange { get; set; }
    public decimal ValueChangePercent { get; set; }
    public decimal ReturnDifference { get; set; }
}

public class CompareSnapshotsQueryHandler : IRequestHandler<CompareSnapshotsQuery, SnapshotComparisonDto>
{
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public CompareSnapshotsQueryHandler(
        IPortfolioSnapshotRepository snapshotRepository,
        IPortfolioRepository portfolioRepository)
    {
        _snapshotRepository = snapshotRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<SnapshotComparisonDto> Handle(CompareSnapshotsQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        var snapshot1 = await _snapshotRepository.GetByPortfolioIdAndDateAsync(request.PortfolioId, request.Date1, cancellationToken);
        var snapshot2 = await _snapshotRepository.GetByPortfolioIdAndDateAsync(request.PortfolioId, request.Date2, cancellationToken);

        var s1Dto = snapshot1 != null ? MapToDto(snapshot1) : null;
        var s2Dto = snapshot2 != null ? MapToDto(snapshot2) : null;

        var valueChange = (s2Dto?.TotalValue ?? 0) - (s1Dto?.TotalValue ?? 0);
        var valueChangePercent = (s1Dto?.TotalValue ?? 0) > 0
            ? (valueChange / s1Dto!.TotalValue) * 100
            : 0;

        return new SnapshotComparisonDto
        {
            PortfolioId = request.PortfolioId,
            Snapshot1 = s1Dto,
            Snapshot2 = s2Dto,
            ValueChange = valueChange,
            ValueChangePercent = Math.Round(valueChangePercent, 2),
            ReturnDifference = (s2Dto?.CumulativeReturn ?? 0) - (s1Dto?.CumulativeReturn ?? 0)
        };
    }

    private static SnapshotDto MapToDto(Domain.Entities.PortfolioSnapshotEntity snapshot)
    {
        return new SnapshotDto
        {
            Id = snapshot.Id,
            PortfolioId = snapshot.PortfolioId,
            SnapshotDate = snapshot.SnapshotDate,
            TotalValue = snapshot.TotalValue,
            CashBalance = snapshot.CashBalance,
            InvestedValue = snapshot.InvestedValue,
            UnrealizedPnL = snapshot.UnrealizedPnL,
            RealizedPnL = snapshot.RealizedPnL,
            DailyReturn = snapshot.DailyReturn,
            CumulativeReturn = snapshot.CumulativeReturn,
            Positions = snapshot.Positions?.Select(p => new PositionSnapshotDto
            {
                Symbol = p.Symbol,
                Quantity = p.Quantity,
                AverageCost = p.AverageCost,
                MarketPrice = p.MarketPrice,
                MarketValue = p.MarketValue,
                UnrealizedPnL = p.UnrealizedPnL,
                Weight = p.Weight
            }).ToList() ?? new()
        };
    }
}
