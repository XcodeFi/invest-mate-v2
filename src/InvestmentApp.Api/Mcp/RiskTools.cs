using System.ComponentModel;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetPortfolioRisk;
using InvestmentApp.Application.Risk.Queries.GetStopLossTargets;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioAdvisories;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class RiskTools
{
    [McpServerTool(Name = "get_portfolio_risk", ReadOnly = true)]
    [Description("Tổng quan rủi ro danh mục: volatility, VaR, Sharpe, max drawdown + chi tiết từng vị thế (khoảng cách tới SL).")]
    public static async Task<PortfolioRiskSummary> GetPortfolioRisk(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetPortfolioRiskQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_stop_loss_targets", ReadOnly = true)]
    [Description("Các mức stop-loss / target đang đặt cho một danh mục, kèm trạng thái triggered và risk-reward ratio.")]
    public static async Task<StopLossTargetsDto> GetStopLossTargets(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetStopLossTargetsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_trailing_stop_alerts", ReadOnly = true)]
    [Description("Cảnh báo vị thế sát ngưỡng trailing stop trong một danh mục (severity: danger ≤2%, warning ≤5%).")]
    public static async Task<TrailingStopAlertsResult> GetTrailingStopAlerts(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTrailingStopAlertsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_volatility_sizing", ReadOnly = true)]
    [Description("Trần khối lượng theo ngân sách biến động cho một lệnh MUA dự kiến: biến động danh mục trước/sau, tương quan với danh mục, đóng góp rủi ro biên, và số cổ tối đa còn nằm trong ngân sách. Gọi TRƯỚC khi create_trade_plan để không đặt khối lượng vượt trần.")]
    public static async Task<VolatilitySizingResult> GetVolatilitySizing(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        [Description("Mã chứng khoán dự kiến mua, ví dụ FPT.")] string symbol,
        [Description("Giá vào lệnh dự kiến, đơn vị đồng.")] decimal entryPrice,
        [Description("Số cổ dự kiến mua.")] int quantity,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetVolatilitySizingForPlanQuery
        {
            UserId = http.GetUserId(),
            PortfolioId = portfolioId,
            Symbol = symbol,
            EntryPrice = entryPrice,
            Quantity = quantity
        }, ct);

    [McpServerTool(Name = "get_scenario_advisories", ReadOnly = true)]
    [Description("Cảnh báo kịch bản (scenario) đang vi phạm điều kiện trên các trade plan active.")]
    public static async Task<List<ScenarioAdvisory>> GetScenarioAdvisories(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetScenarioAdvisoriesQuery { UserId = http.GetUserId() }, ct);
}
