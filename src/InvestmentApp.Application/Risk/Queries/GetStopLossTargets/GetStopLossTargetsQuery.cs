using MediatR;
using InvestmentApp.Application.Common;
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
    private readonly ICorporateActionRepository _corporateActionRepository;

    public GetStopLossTargetsQueryHandler(
        IStopLossTargetRepository stopLossTargetRepository,
        IPortfolioRepository portfolioRepository,
        ICorporateActionRepository corporateActionRepository)
    {
        _stopLossTargetRepository = stopLossTargetRepository;
        _portfolioRepository = portfolioRepository;
        _corporateActionRepository = corporateActionRepository;
    }

    public async Task<StopLossTargetsDto> Handle(GetStopLossTargetsQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        var targets = await _stopLossTargetRepository.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken);

        // Ngưỡng lưu giá tuyệt đối tại lần sửa gần nhất. Phải điều chỉnh giống
        // RiskCalculationService, không thì hai bề mặt đọc lệch nhau sau ngày GDKHQ.
        var actionsBySymbol = (await _corporateActionRepository
                .GetByPortfolioIdAsync(request.PortfolioId, cancellationToken))
            .ToLookup(a => a.Symbol, StringComparer.OrdinalIgnoreCase);

        return new StopLossTargetsDto
        {
            PortfolioId = request.PortfolioId,
            Items = targets.Select(t =>
            {
                var actions = actionsBySymbol[t.Symbol];
                var entry = CorporateActionAdjuster.AdjustPrice(t.EntryPrice, t.UpdatedAt, actions);
                var stopLoss = CorporateActionAdjuster.AdjustPrice(t.StopLossPrice, t.UpdatedAt, actions);
                var target = CorporateActionAdjuster.AdjustPrice(t.TargetPrice, t.UpdatedAt, actions);
                var riskPerShare = entry - stopLoss;

                return new StopLossTargetItemDto
                {
                    Id = t.Id,
                    TradeId = t.TradeId,
                    Symbol = t.Symbol,
                    EntryPrice = entry,
                    StopLossPrice = stopLoss,
                    TargetPrice = target,
                    TrailingStopPercent = t.TrailingStopPercent,
                    TrailingStopPrice = t.TrailingStopPrice.HasValue
                        ? CorporateActionAdjuster.AdjustPrice(t.TrailingStopPrice.Value, t.UpdatedAt, actions)
                        : null,
                    IsStopLossTriggered = t.IsStopLossTriggered,
                    IsTargetTriggered = t.IsTargetTriggered,
                    TriggeredAt = t.TriggeredAt,
                    RiskRewardRatio = riskPerShare > 0 ? (target - entry) / riskPerShare : 0,
                    RiskPerShare = riskPerShare,
                    CreatedAt = t.CreatedAt
                };
            }).ToList()
        };
    }
}
