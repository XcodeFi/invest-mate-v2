using MediatR;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Snapshots.Queries.GetSnapshotAtDate;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Snapshots.Queries.GetSnapshotRange;

public class GetSnapshotRangeQuery : IRequest<List<SnapshotDto>>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class GetSnapshotRangeQueryHandler : IRequestHandler<GetSnapshotRangeQuery, List<SnapshotDto>>
{
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetSnapshotRangeQueryHandler(
        IPortfolioSnapshotRepository snapshotRepository,
        IPortfolioRepository portfolioRepository)
    {
        _snapshotRepository = snapshotRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<List<SnapshotDto>> Handle(GetSnapshotRangeQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            return new();

        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            request.PortfolioId, request.From, request.To, cancellationToken);

        return snapshots.Select(s => new SnapshotDto
        {
            Id = s.Id,
            PortfolioId = s.PortfolioId,
            SnapshotDate = s.SnapshotDate,
            TotalValue = s.TotalValue,
            CashBalance = s.CashBalance,
            InvestedValue = s.InvestedValue,
            UnrealizedPnL = s.UnrealizedPnL,
            RealizedPnL = s.RealizedPnL,
            DailyReturn = s.DailyReturn,
            CumulativeReturn = s.CumulativeReturn,
            Positions = s.Positions?.Select(p => new PositionSnapshotDto
            {
                Symbol = p.Symbol,
                Quantity = p.Quantity,
                AverageCost = p.AverageCost,
                MarketPrice = p.MarketPrice,
                MarketValue = p.MarketValue,
                UnrealizedPnL = p.UnrealizedPnL,
                Weight = p.Weight
            }).ToList() ?? new()
        }).ToList();
    }
}
