using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetPortfolio;
using MediatR;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;

public class GetAllPortfoliosQuery : IRequest<List<PortfolioSummaryDto>>
{
    public string UserId { get; set; } = null!;
}

public class GetAllPortfoliosQueryHandler : IRequestHandler<GetAllPortfoliosQuery, List<PortfolioSummaryDto>>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly IMarketClosureRepository _marketClosureRepository;
    private readonly int _settlementSessions;

    public GetAllPortfoliosQueryHandler(
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository,
        ICapitalFlowRepository capitalFlowRepository,
        IMarketClosureRepository marketClosureRepository,
        IOptions<SettlementOptions> settlementOptions)
    {
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
        _capitalFlowRepository = capitalFlowRepository;
        _marketClosureRepository = marketClosureRepository;
        _settlementSessions = settlementOptions.Value.Sessions;
    }

    public async Task<List<PortfolioSummaryDto>> Handle(GetAllPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var portfolios = await _portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var result = new List<PortfolioSummaryDto>();

        // Nạp MỘT lần cho mọi danh mục — tập ngày nghỉ không phụ thuộc danh mục.
        // Cửa sổ ±30 ngày: T+2 vắt qua cả đợt nghỉ Tết dài nhất vẫn dưới 20 ngày lịch.
        // Ngày nghỉ nằm ngoài cửa sổ này bị coi như phiên giao dịch, nên nếu sau này
        // cấu hình chu kỳ dài (gần mức 10 phiên) thì phải nới cửa sổ theo.
        var todayVn = VietnamDate.Today(DateTime.UtcNow);
        var closures = await _marketClosureRepository.GetByUserAndRangeAsync(
            request.UserId, todayVn.AddDays(-30), todayVn.AddDays(30), cancellationToken);
        var closedDates = closures.Select(c => DateOnly.FromDateTime(c.Date)).ToHashSet();

        foreach (var portfolio in portfolios)
        {
            var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
            var tradeList = trades.ToList();

            // Calculate basic portfolio metrics
            var totalInvested = tradeList
                .Where(t => t.TradeType == Domain.Entities.TradeType.BUY)
                .Sum(t => t.Quantity * t.Price + t.Fee + t.Tax);

            var totalSold = tradeList
                .Where(t => t.TradeType == Domain.Entities.TradeType.SELL)
                .Sum(t => t.Quantity * t.Price - t.Fee - t.Tax);

            var uniqueSymbols = tradeList.Select(t => t.Symbol).Distinct().Count();

            var (pendingCash, pendingArrival) = SettlementCalculator.PendingSellProceeds(
                tradeList, todayVn, closedDates, _settlementSessions);

            var netCashFlow = await _capitalFlowRepository.GetTotalFlowByPortfolioIdAsync(portfolio.Id, cancellationToken);

            result.Add(new PortfolioSummaryDto
            {
                Id = portfolio.Id,
                Name = portfolio.Name,
                InitialCapital = portfolio.InitialCapital,
                NetCashFlow = netCashFlow,
                CurrentCapital = portfolio.InitialCapital + netCashFlow,
                CreatedAt = portfolio.CreatedAt,
                TradeCount = tradeList.Count,
                UniqueSymbols = uniqueSymbols,
                TotalInvested = totalInvested,
                TotalSold = totalSold,
                PendingSettlementCash = pendingCash,
                PendingSettlementArrivalDate = pendingArrival
            });
        }

        return result;
    }
}

public class PortfolioSummaryDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal InitialCapital { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal CurrentCapital { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TradeCount { get; set; }
    public int UniqueSymbols { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalSold { get; set; }

    /// <summary>
    /// Tiền bán chưa về ví theo chu kỳ thanh toán đang cấu hình (<see cref="SettlementOptions"/>,
    /// mặc định T+2). Đã nằm TRONG <see cref="TotalSold"/>.
    /// </summary>
    public decimal PendingSettlementCash { get; set; }

    /// <summary>Ngày về xa nhất trong các lệnh còn chờ. <c>null</c> khi không còn gì chờ.</summary>
    public DateTime? PendingSettlementArrivalDate { get; set; }
}
