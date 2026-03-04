using MediatR;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Snapshots.Queries.GetSnapshotAtDate;

public class GetSnapshotAtDateQuery : IRequest<SnapshotDto?>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public DateTime Date { get; set; }
}

public class SnapshotDto
{
    public string Id { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public DateTime SnapshotDate { get; set; }
    public decimal TotalValue { get; set; }
    public decimal CashBalance { get; set; }
    public decimal InvestedValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal DailyReturn { get; set; }
    public decimal CumulativeReturn { get; set; }
    public List<PositionSnapshotDto> Positions { get; set; } = new();
}

public class PositionSnapshotDto
{
    public string Symbol { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal MarketPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal Weight { get; set; }
}

public class GetSnapshotAtDateQueryHandler : IRequestHandler<GetSnapshotAtDateQuery, SnapshotDto?>
{
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetSnapshotAtDateQueryHandler(
        IPortfolioSnapshotRepository snapshotRepository,
        IPortfolioRepository portfolioRepository)
    {
        _snapshotRepository = snapshotRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<SnapshotDto?> Handle(GetSnapshotAtDateQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            return null;

        var snapshot = await _snapshotRepository.GetByPortfolioIdAndDateAsync(request.PortfolioId, request.Date, cancellationToken);
        if (snapshot == null)
            return null;

        return MapToDto(snapshot);
    }

    private static SnapshotDto MapToDto(PortfolioSnapshotEntity snapshot)
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
