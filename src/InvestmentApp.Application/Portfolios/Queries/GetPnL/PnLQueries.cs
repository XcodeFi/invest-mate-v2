using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.ValueObjects;
using MediatR;

namespace InvestmentApp.Application.Portfolios.Queries.GetPnL;

// Ba query lãi/lỗ dùng chung cho REST (PnLController) và MCP. IPnLService không tự kiểm chủ sở hữu,
// nên việc kiểm nằm trong handler, trước khi chạm vào service.
// Danh mục không có hoặc của người khác cho cùng một phản hồi.

internal static class PnLOwnership
{
    public static async Task<Domain.Entities.Portfolio> RequireOwnedAsync(
        IPortfolioRepository portfolios, string portfolioId, string userId, CancellationToken ct)
    {
        var portfolio = await portfolios.GetByIdAsync(portfolioId, ct);
        if (portfolio == null || portfolio.UserId != userId)
            throw new KeyNotFoundException($"Portfolio {portfolioId} not found");
        return portfolio;
    }
}

// ---------- Lãi/lỗ một danh mục ----------

public class GetPortfolioPnLQuery : IRequest<PortfolioPnLDto>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class PortfolioPnLDto
{
    public string PortfolioId { get; set; } = null!;
    public string PortfolioName { get; set; } = null!;
    public decimal TotalInvested { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal TotalRealizedPnL { get; set; }
    public decimal TotalUnrealizedPnL { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal TotalPnLPercent { get; set; }
    public List<PositionPnL> Positions { get; set; } = new();
}

public class GetPortfolioPnLQueryHandler : IRequestHandler<GetPortfolioPnLQuery, PortfolioPnLDto>
{
    private readonly IPortfolioRepository _portfolios;
    private readonly IPnLService _pnl;

    public GetPortfolioPnLQueryHandler(IPortfolioRepository portfolios, IPnLService pnl)
    {
        _portfolios = portfolios;
        _pnl = pnl;
    }

    public async Task<PortfolioPnLDto> Handle(GetPortfolioPnLQuery request, CancellationToken ct)
    {
        var portfolio = await PnLOwnership.RequireOwnedAsync(_portfolios, request.PortfolioId, request.UserId, ct);
        var s = await _pnl.CalculatePortfolioPnLAsync(request.PortfolioId, ct);
        return new PortfolioPnLDto
        {
            PortfolioId = portfolio.Id,
            PortfolioName = portfolio.Name,
            TotalInvested = s.TotalInvested,
            TotalMarketValue = s.TotalPortfolioValue,
            TotalRealizedPnL = s.TotalRealizedPnL,
            TotalUnrealizedPnL = s.TotalUnrealizedPnL,
            TotalPnL = s.TotalPnL,
            TotalPnLPercent = s.TotalPnLPercent,
            Positions = s.Positions
        };
    }
}

// ---------- Lãi/lỗ một vị thế ----------

public class GetPositionPnLQuery : IRequest<PositionPnL>
{
    public string PortfolioId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetPositionPnLQueryHandler : IRequestHandler<GetPositionPnLQuery, PositionPnL>
{
    private readonly IPortfolioRepository _portfolios;
    private readonly IPnLService _pnl;

    public GetPositionPnLQueryHandler(IPortfolioRepository portfolios, IPnLService pnl)
    {
        _portfolios = portfolios;
        _pnl = pnl;
    }

    public async Task<PositionPnL> Handle(GetPositionPnLQuery request, CancellationToken ct)
    {
        await PnLOwnership.RequireOwnedAsync(_portfolios, request.PortfolioId, request.UserId, ct);
        // Mã không có giao dịch hoặc không có giá: IPnLService ném ArgumentException như REST
        return await _pnl.CalculatePositionPnLAsync(request.PortfolioId, new StockSymbol(request.Symbol), ct);
    }
}

// ---------- Tổng hợp mọi danh mục của người gọi ----------

public class GetOverallPnLQuery : IRequest<OverallPnLDto>
{
    public string UserId { get; set; } = null!;
}

public class OverallPnLPortfolioDto
{
    public string PortfolioId { get; set; } = null!;
    public string PortfolioName { get; set; } = null!;
    public decimal InitialCapital { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal CurrentCapital { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal TotalUnrealizedPnL { get; set; }
    public decimal TotalRealizedPnL { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal TotalPnLPercent { get; set; }
    public List<PositionPnL> Positions { get; set; } = new();
}

public class OverallPnLDto
{
    public int TotalPortfolios { get; set; }
    public decimal TotalInitialCapital { get; set; }
    public decimal TotalNetCashFlow { get; set; }
    public decimal TotalCurrentCapital { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal TotalUnrealizedPnL { get; set; }
    public decimal TotalRealizedPnL { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal TotalPnLPercent { get; set; }
    public List<OverallPnLPortfolioDto> Portfolios { get; set; } = new();
}

public class GetOverallPnLQueryHandler : IRequestHandler<GetOverallPnLQuery, OverallPnLDto>
{
    private readonly IPortfolioRepository _portfolios;
    private readonly ICapitalFlowRepository _flows;
    private readonly IPnLService _pnl;

    public GetOverallPnLQueryHandler(IPortfolioRepository portfolios, ICapitalFlowRepository flows, IPnLService pnl)
    {
        _portfolios = portfolios;
        _flows = flows;
        _pnl = pnl;
    }

    // Cùng ngữ nghĩa với PnLController.GetOverallPnL: dòng tiền ròng cộng cho MỌI danh mục (kể cả danh mục chưa có giao dịch),
    // danh mục tính lãi/lỗ lỗi (chưa có giao dịch) bị bỏ khỏi danh sách và khỏi các tổng lãi/lỗ.
    public async Task<OverallPnLDto> Handle(GetOverallPnLQuery request, CancellationToken ct)
    {
        var list = (await _portfolios.GetByUserIdAsync(request.UserId, ct)).ToList();

        decimal invested = 0, unrealized = 0, realized = 0, value = 0, netFlow = 0;
        var rows = new List<OverallPnLPortfolioDto>();
        var initial = list.Sum(p => p.InitialCapital);

        foreach (var p in list)
        {
            var flow = await _flows.GetTotalFlowByPortfolioIdAsync(p.Id);
            netFlow += flow;
            try
            {
                var pnl = await _pnl.CalculatePortfolioPnLAsync(p.Id, ct);
                invested += pnl.TotalInvested;
                unrealized += pnl.TotalUnrealizedPnL;
                realized += pnl.TotalRealizedPnL;
                value += pnl.TotalPortfolioValue;
                rows.Add(new OverallPnLPortfolioDto
                {
                    PortfolioId = p.Id,
                    PortfolioName = p.Name,
                    InitialCapital = p.InitialCapital,
                    NetCashFlow = flow,
                    CurrentCapital = p.InitialCapital + flow,
                    TotalInvested = pnl.TotalInvested,
                    TotalMarketValue = pnl.TotalPortfolioValue,
                    TotalUnrealizedPnL = pnl.TotalUnrealizedPnL,
                    TotalRealizedPnL = pnl.TotalRealizedPnL,
                    TotalPnL = pnl.TotalPnL,
                    TotalPnLPercent = pnl.TotalReturnPercentage,
                    Positions = pnl.Positions
                });
            }
            catch (OperationCanceledException) { throw; }
            catch { /* danh mục chưa có giao dịch: bỏ qua như REST */ }
        }

        return new OverallPnLDto
        {
            TotalPortfolios = list.Count,
            TotalInitialCapital = initial,
            TotalNetCashFlow = netFlow,
            TotalCurrentCapital = initial + netFlow,
            TotalInvested = invested,
            TotalMarketValue = value,
            TotalUnrealizedPnL = unrealized,
            TotalRealizedPnL = realized,
            TotalPnL = realized + unrealized,
            TotalPnLPercent = invested > 0 ? (realized + unrealized) / invested * 100 : 0,
            Portfolios = rows
        };
    }
}
