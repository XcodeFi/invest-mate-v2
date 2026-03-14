using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetActivePositions;

public class GetActivePositionsQuery : IRequest<List<ActivePositionDto>>
{
    public string UserId { get; set; } = null!;
    public string? PortfolioId { get; set; }
}

public class ActivePositionDto
{
    public string Symbol { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public string PortfolioName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal UnrealizedPnLPercent { get; set; }
    public decimal RealizedPnL { get; set; }
    public TradePlanDto? LinkedPlan { get; set; }
    public List<TradeSummaryDto> RecentTrades { get; set; } = new();
    public string? NextAction { get; set; }
}

public class TradeSummaryDto
{
    public string Id { get; set; } = null!;
    public string TradeType { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime TradeDate { get; set; }
}

public class GetActivePositionsQueryHandler : IRequestHandler<GetActivePositionsQuery, List<ActivePositionDto>>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IPnLService _pnlService;
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;

    public GetActivePositionsQueryHandler(
        IPortfolioRepository portfolioRepository,
        IPnLService pnlService,
        ITradePlanRepository tradePlanRepository,
        ITradeRepository tradeRepository)
    {
        _portfolioRepository = portfolioRepository;
        _pnlService = pnlService;
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<List<ActivePositionDto>> Handle(GetActivePositionsQuery request, CancellationToken cancellationToken)
    {
        var portfolios = (await _portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken)).ToList();

        if (!string.IsNullOrEmpty(request.PortfolioId))
            portfolios = portfolios.Where(p => p.Id == request.PortfolioId).ToList();

        var result = new List<ActivePositionDto>();

        foreach (var portfolio in portfolios)
        {
            PortfolioPnLSummary pnl;
            try
            {
                pnl = await _pnlService.CalculatePortfolioPnLAsync(portfolio.Id, cancellationToken);
            }
            catch
            {
                continue;
            }

            var openPositions = pnl.Positions.Where(p => p.Quantity > 0);

            foreach (var pos in openPositions)
            {
                // Find linked trade plan
                var plan = await _tradePlanRepository.GetActiveByPortfolioAndSymbolAsync(
                    portfolio.Id, pos.Symbol, cancellationToken);

                // Get recent trades
                var trades = (await _tradeRepository.GetByPortfolioIdAndSymbolAsync(
                    portfolio.Id, pos.Symbol, cancellationToken))
                    .OrderByDescending(t => t.TradeDate)
                    .Take(10)
                    .Select(t => new TradeSummaryDto
                    {
                        Id = t.Id,
                        TradeType = t.TradeType.ToString(),
                        Quantity = t.Quantity,
                        Price = t.Price,
                        TradeDate = t.TradeDate
                    }).ToList();

                // Compute next action suggestion
                string? nextAction = null;
                if (plan != null)
                {
                    var pendingLot = plan.Lots?.FirstOrDefault(l => l.Status == Domain.Entities.PlanLotStatus.Pending);
                    if (pendingLot != null)
                        nextAction = $"Mua thêm lô {pendingLot.LotNumber} tại {pendingLot.PlannedPrice:N0}đ";
                    else if (plan.ExitTargets?.Any(e => !e.IsTriggered) == true)
                    {
                        var nextExit = plan.ExitTargets.First(e => !e.IsTriggered);
                        nextAction = $"{nextExit.ActionType}: {nextExit.Price:N0}đ";
                    }
                }

                result.Add(new ActivePositionDto
                {
                    Symbol = pos.Symbol,
                    PortfolioId = portfolio.Id,
                    PortfolioName = portfolio.Name,
                    Quantity = pos.Quantity,
                    AverageCost = pos.AverageCost,
                    CurrentPrice = pos.CurrentPrice,
                    MarketValue = pos.MarketValue,
                    UnrealizedPnL = pos.UnrealizedPnL,
                    UnrealizedPnLPercent = pos.UnrealizedPnLPercentage,
                    RealizedPnL = pos.RealizedPnL,
                    LinkedPlan = plan != null ? GetTradePlansQueryHandler.MapToDto(plan) : null,
                    RecentTrades = trades,
                    NextAction = nextAction
                });
            }
        }

        return result;
    }
}
