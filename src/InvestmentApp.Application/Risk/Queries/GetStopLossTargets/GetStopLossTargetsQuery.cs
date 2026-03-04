using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Risk.Queries.GetStopLossTargets;

public class GetStopLossTargetsQuery : IRequest<StopLossTargetsDto>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class StopLossTargetsDto
{
    public string PortfolioId { get; set; } = null!;
    public List<StopLossTargetItemDto> Items { get; set; } = new();
}

public class StopLossTargetItemDto
{
    public string Id { get; set; } = null!;
    public string TradeId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public decimal EntryPrice { get; set; }
    public decimal StopLossPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public decimal? TrailingStopPercent { get; set; }
    public decimal? TrailingStopPrice { get; set; }
    public bool IsStopLossTriggered { get; set; }
    public bool IsTargetTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public decimal RiskRewardRatio { get; set; }
    public decimal RiskPerShare { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetStopLossTargetsQueryHandler : IRequestHandler<GetStopLossTargetsQuery, StopLossTargetsDto>
{
    private readonly IStopLossTargetRepository _stopLossTargetRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetStopLossTargetsQueryHandler(
        IStopLossTargetRepository stopLossTargetRepository,
        IPortfolioRepository portfolioRepository)
    {
        _stopLossTargetRepository = stopLossTargetRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<StopLossTargetsDto> Handle(GetStopLossTargetsQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        var targets = await _stopLossTargetRepository.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken);

        return new StopLossTargetsDto
        {
            PortfolioId = request.PortfolioId,
            Items = targets.Select(t => new StopLossTargetItemDto
            {
                Id = t.Id,
                TradeId = t.TradeId,
                Symbol = t.Symbol,
                EntryPrice = t.EntryPrice,
                StopLossPrice = t.StopLossPrice,
                TargetPrice = t.TargetPrice,
                TrailingStopPercent = t.TrailingStopPercent,
                TrailingStopPrice = t.TrailingStopPrice,
                IsStopLossTriggered = t.IsStopLossTriggered,
                IsTargetTriggered = t.IsTargetTriggered,
                TriggeredAt = t.TriggeredAt,
                RiskRewardRatio = t.GetRiskRewardRatio(),
                RiskPerShare = t.GetRiskPerShare(),
                CreatedAt = t.CreatedAt
            }).ToList()
        };
    }
}
