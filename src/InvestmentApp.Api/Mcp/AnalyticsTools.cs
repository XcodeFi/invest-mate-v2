using System.ComponentModel;
using InvestmentApp.Application.Analytics.Queries.GetEquityCurve;
using InvestmentApp.Application.Analytics.Queries.GetMonthlyReturns;
using InvestmentApp.Application.Analytics.Queries.GetPerformance;
using InvestmentApp.Application.Analytics.Queries.GetSavingsComparison;
using InvestmentApp.Application.CapitalFlows.Queries.GetAdjustedReturn;
using InvestmentApp.Application.CapitalFlows.Queries.GetFlowHistory;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Application.PersonalFinance.Queries.GetNetWorthSummary;
using InvestmentApp.Application.TradePlans.Queries.GetCampaignAnalytics;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class AnalyticsTools
{
    [McpServerTool(Name = "get_performance", ReadOnly = true)]
    [Description("Tổng quan hiệu suất danh mục: tổng P/L, MTD, YTD.")]
    public static async Task<PerformanceSummary> GetPerformance(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetPerformanceQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_equity_curve", ReadOnly = true)]
    [Description("Đường cong giá trị danh mục (equity curve) theo thời gian.")]
    public static async Task<EquityCurveData> GetEquityCurve(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetEquityCurveQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_monthly_returns", ReadOnly = true)]
    [Description("Lợi nhuận theo từng tháng của danh mục.")]
    public static async Task<MonthlyReturnsData> GetMonthlyReturns(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetMonthlyReturnsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_savings_comparison", ReadOnly = true)]
    [Description("So sánh giá trị danh mục thực tế với kịch bản gửi tiết kiệm (alpha vs savings).")]
    public static async Task<SavingsComparisonDto> GetSavingsComparison(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Lãi suất tiết kiệm/năm dạng thập phân: 0.05 = 5%/năm (bỏ trống = trung bình các tài khoản tiết kiệm của user, fallback 5%).")] decimal? annualRate = null,
        [Description("Mốc thời gian tính toán ISO-8601 (bỏ trống = hiện tại).")] DateTime? asOf = null)
        => await mediator.Send(new GetSavingsComparisonQuery
        {
            UserId = http.GetUserId(),
            PortfolioId = portfolioId,
            AnnualRate = annualRate,
            AsOf = asOf
        }, ct);

    [McpServerTool(Name = "get_campaign_analytics", ReadOnly = true)]
    [Description("Thống kê chiến dịch đã review: win rate, P/L trung bình, best/worst campaign, trend.")]
    public static async Task<CampaignAnalyticsDto> GetCampaignAnalytics(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Lọc theo tầm nhìn, đúng 1 trong: ShortTerm/MediumTerm/LongTerm (bỏ trống = tất cả; giá trị sai cũng trả tất cả).")] string? timeHorizon = null)
        => await mediator.Send(new GetCampaignAnalyticsQuery { UserId = http.GetUserId(), TimeHorizon = timeHorizon }, ct);

    [McpServerTool(Name = "get_net_worth_summary", ReadOnly = true)]
    [Description("Tổng quan tài sản ròng cá nhân: các tài khoản, nợ, health score.")]
    public static async Task<NetWorthSummaryDto> GetNetWorthSummary(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetNetWorthSummaryQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_flow_history", ReadOnly = true)]
    [Description("Lịch sử dòng tiền nạp/rút/cổ tức của danh mục, kèm tổng hợp.")]
    public static async Task<CapitalFlowHistoryDto> GetFlowHistory(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Từ ngày ISO-8601 (bỏ trống = không giới hạn).")] DateTime? from = null,
        [Description("Đến ngày ISO-8601 (bỏ trống = không giới hạn).")] DateTime? to = null)
        => await mediator.Send(new GetFlowHistoryQuery
        {
            UserId = http.GetUserId(),
            PortfolioId = portfolioId,
            From = from,
            To = to
        }, ct);

    [McpServerTool(Name = "get_adjusted_return", ReadOnly = true)]
    [Description("Lợi nhuận hiệu chỉnh dòng tiền: TWR (time-weighted) + MWR (money-weighted) của danh mục.")]
    public static async Task<AdjustedReturnDto> GetAdjustedReturn(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetAdjustedReturnQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);
}
