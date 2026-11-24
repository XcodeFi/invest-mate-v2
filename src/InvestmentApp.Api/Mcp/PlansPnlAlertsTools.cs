using System.ComponentModel;
using InvestmentApp.Application.Alerts.Queries.GetAlertHistory;
using InvestmentApp.Application.Alerts.Queries.GetAlertRules;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Application.Portfolios.Queries.GetPnL;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetExecutedPlansForReview;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioHistory;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioSuggestion;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioTemplates;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Application.TradePlans.Queries.PreviewPlanReview;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

// Đợt 3 MCP (chỉ đọc): kế hoạch, lãi/lỗ, cảnh báo. UserId CHỈ lấy từ người gọi (http.GetUserId()), không bao giờ từ tham số tool.

[McpServerToolType]
public static class PlanInsightTools
{
    [McpServerTool(Name = "get_scenario_suggestion", ReadOnly = true)]
    [Description("Gợi ý kịch bản (cây điều kiện → hành động) cho một mã theo chỉ báo kỹ thuật: EMA, RSI, Bollinger, hỗ trợ/kháng cự, Fibonacci. Chỉ tham khảo, không tạo kế hoạch.")]
    public static async Task<ScenarioSuggestionDto> GetScenarioSuggestion(
        [Description("Mã chứng khoán, tối đa 10 ký tự, ví dụ HPG.")] string symbol,
        [Description("Giá vào lệnh dự kiến, phải lớn hơn 0.")] decimal entryPrice,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Tầm nhìn, đúng 1 trong: ShortTerm (dưới 3 tháng)/MediumTerm (3-12 tháng)/LongTerm (trên 1 năm). Bỏ trống = MediumTerm.")] string? timeHorizon = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Cần nhập mã chứng khoán (symbol), ví dụ HPG.", nameof(symbol));
        var sym = symbol.ToUpper().Trim();
        if (sym.Length > 10)
            throw new ArgumentException("Mã chứng khoán (symbol) tối đa 10 ký tự.", nameof(symbol));
        if (entryPrice <= 0)
            throw new ArgumentException("Giá vào lệnh (entryPrice) phải lớn hơn 0.", nameof(entryPrice));

        var horizon = TimeHorizon.MediumTerm;
        if (!string.IsNullOrWhiteSpace(timeHorizon))
        {
            var raw = timeHorizon.Trim();
            // Chỉ chấp nhận tên; số ("1") hay giá trị lạ không được lọt qua Enum.TryParse
            if (!Enum.TryParse(raw, ignoreCase: true, out horizon) || !Enum.GetNames<TimeHorizon>()
                    .Any(n => string.Equals(n, raw, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Tầm nhìn (timeHorizon) chỉ nhận ShortTerm, MediumTerm hoặc LongTerm.", nameof(timeHorizon));
        }

        return await mediator.Send(new GetScenarioSuggestionQuery
        {
            UserId = http.GetUserId(),
            Symbol = sym,
            EntryPrice = entryPrice,
            TimeHorizon = horizon
        }, ct);
    }

    [McpServerTool(Name = "list_scenario_templates", ReadOnly = true)]
    [Description("Danh sách mẫu kịch bản: 3 mẫu dựng sẵn (thận trọng, cân bằng, tích cực) cùng các mẫu tự lưu của người dùng.")]
    public static async Task<List<ScenarioPresetDto>> ListScenarioTemplates(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetScenarioTemplatesQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_plan_scenario_history", ReadOnly = true)]
    [Description("Lịch sử kích hoạt các nút kịch bản của một kế hoạch: nút nào đã kích hoạt, lúc nào, giá lúc kích hoạt.")]
    public static async Task<List<ScenarioHistoryDto>> GetPlanScenarioHistory(
        [Description("ID kế hoạch (lấy từ list_trade_plans).")] string planId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetScenarioHistoryQuery { UserId = http.GetUserId(), TradePlanId = planId }, ct);

    [McpServerTool(Name = "preview_plan_review", ReadOnly = true)]
    [Description("Xem trước kết quả review chiến dịch của một kế hoạch đã thực hiện (P/L, R, điểm kỷ luật) mà không đổi trạng thái kế hoạch.")]
    public static async Task<CampaignReviewDto> PreviewPlanReview(
        [Description("ID kế hoạch (lấy từ list_trade_plans).")] string planId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new PreviewPlanReviewQuery { UserId = http.GetUserId(), PlanId = planId }, ct);

    [McpServerTool(Name = "list_plans_pending_review", ReadOnly = true)]
    [Description("Danh sách kế hoạch đã thực hiện nhưng chưa review.")]
    public static async Task<List<TradePlanDto>> ListPlansPendingReview(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetExecutedPlansForReviewQuery { UserId = http.GetUserId() }, ct);
}

[McpServerToolType]
public static class PnLTools
{
    [McpServerTool(Name = "get_portfolio_pnl", ReadOnly = true)]
    [Description("Lãi/lỗ của một danh mục: tổng đầu tư, giá trị thị trường, lãi/lỗ đã chốt và chưa chốt, kèm từng vị thế.")]
    public static async Task<PortfolioPnLDto> GetPortfolioPnL(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetPortfolioPnLQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_position_pnl", ReadOnly = true)]
    [Description("Lãi/lỗ của một mã trong danh mục: số lượng, giá vốn, giá hiện tại, lãi/lỗ đã chốt và chưa chốt, cổ tức.")]
    public static async Task<PositionPnL> GetPositionPnL(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        [Description("Mã chứng khoán, ví dụ HPG.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Cần nhập mã chứng khoán (symbol), ví dụ HPG.", nameof(symbol));
        return await mediator.Send(new GetPositionPnLQuery
        {
            UserId = http.GetUserId(),
            PortfolioId = portfolioId,
            Symbol = symbol.ToUpper().Trim()
        }, ct);
    }

    [McpServerTool(Name = "get_pnl_summary", ReadOnly = true)]
    [Description("Tổng hợp lãi/lỗ qua mọi danh mục của người dùng: vốn, dòng tiền ròng, tổng đầu tư, lãi/lỗ, kèm từng danh mục. Danh mục chưa có giao dịch không có dòng riêng.")]
    public static async Task<OverallPnLDto> GetPnLSummary(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetOverallPnLQuery { UserId = http.GetUserId() }, ct);
}

[McpServerToolType]
public static class AlertTools
{
    [McpServerTool(Name = "list_alert_rules", ReadOnly = true)]
    [Description("Danh sách quy tắc cảnh báo của người dùng.")]
    public static async Task<IEnumerable<AlertRuleDto>> ListAlertRules(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetAlertRulesQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_alert_history", ReadOnly = true)]
    [Description("Lịch sử cảnh báo đã kích hoạt, kèm số chưa đọc.")]
    public static async Task<AlertHistoryResult> GetAlertHistory(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("true = chỉ cảnh báo chưa đọc (bỏ trống = tất cả).")] bool? unreadOnly = null)
        => await mediator.Send(new GetAlertHistoryQuery { UserId = http.GetUserId(), UnreadOnly = unreadOnly ?? false }, ct);
}
