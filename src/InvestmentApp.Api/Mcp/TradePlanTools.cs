using System.ComponentModel;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class TradePlanTools
{
    [McpServerTool(Name = "list_trade_plans", ReadOnly = true)]
    [Description("Liệt kê kế hoạch giao dịch. activeOnly = true chỉ lấy kế hoạch đang hiệu lực.")]
    public static async Task<IEnumerable<TradePlanDto>> ListTradePlans(
        [Description("Chỉ lấy kế hoạch đang hoạt động.")] bool activeOnly,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTradePlansQuery { UserId = http.GetUserId(), ActiveOnly = activeOnly }, ct);

    [McpServerTool(Name = "get_trade_plan", ReadOnly = true)]
    [Description("Lấy chi tiết một kế hoạch giao dịch theo id. Null nếu không tồn tại/không thuộc chủ khóa.")]
    public static async Task<TradePlanDto?> GetTradePlan(
        [Description("ID kế hoạch.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTradePlanByIdQuery { Id = id, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "create_trade_plan", Destructive = true)]
    [Description("Tạo kế hoạch giao dịch mới. Luôn tạo ở trạng thái Nháp (Draft) — agent không tự khớp lệnh. "
               + "Với lệnh MUA có gắn danh mục, kết quả trả về có thể kèm cảnh báo trần khối lượng theo ngân sách "
               + "biến động; kế hoạch vẫn được tạo, hãy chuyển tiếp cảnh báo đó cho người dùng.")]
    public static async Task<string> CreateTradePlan(
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Giá vào dự kiến, VND.")] decimal entryPrice,
        [Description("Giá cắt lỗ, VND.")] decimal stopLoss,
        [Description("Giá mục tiêu, VND.")] decimal target,
        [Description("Khối lượng dự kiến.")] int quantity,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("ID danh mục (bỏ trống = không gắn danh mục).")] string? portfolioId = null,
        [Description("Chiều lệnh: Buy hoặc Sell (bỏ trống = Buy).")] string? direction = null,
        [Description("ID chiến lược (bỏ trống = không gắn chiến lược).")] string? strategyId = null,
        [Description("Bối cảnh thị trường: Trending/Ranging/Volatile… (bỏ trống = Trending).")] string? marketCondition = null,
        [Description("Luận điểm đầu tư (bỏ trống = không ghi nhận).")] string? thesis = null,
        [Description("Ghi chú thêm (bỏ trống = không ghi nhận).")] string? notes = null,
        [Description("Mức độ tự tin 1–10 (bỏ trống = 5).")] int? confidenceLevel = null,
        [Description("% tài khoản chấp nhận rủi ro (bỏ trống = không tính).")] decimal? riskPercent = null,
        [Description("Giá trị tài khoản dùng để tính rủi ro, VND (bỏ trống = không tính).")] decimal? accountBalance = null,
        [Description("Tỷ lệ lời/lỗ kỳ vọng (bỏ trống = không ghi nhận, hệ thống KHÔNG tự tính).")] decimal? riskRewardRatio = null,
        [Description("Ngày dự kiến review ISO-8601 (bỏ trống = không đặt).")] DateTime? expectedReviewDate = null,
        [Description("Tầm nhìn: ShortTerm/MediumTerm/LongTerm (bỏ trống = không đặt).")] string? timeHorizon = null,
        [Description("Điều kiện phủ định luận điểm (bỏ trống = không có).")] List<InvalidationRuleDto>? invalidationCriteria = null,
        [Description("Checklist trước khi vào lệnh (bỏ trống = không có).")] List<ChecklistItemDto>? checklist = null,
        [Description("Kiểu vào lệnh nhiều lô: Single/Scaled (bỏ trống = vào một lần).")] string? entryMode = null,
        [Description("Các lô vào lệnh, chỉ dùng khi entryMode = Scaled (bỏ trống = không chia lô).")] List<PlanLotDto>? lots = null,
        [Description("Các mốc chốt lời (bỏ trống = không đặt).")] List<ExitTargetDto>? exitTargets = null,
        [Description("Kiểu chiến lược thoát: Simple/Advanced (bỏ trống = Simple).")] string? exitStrategyMode = null,
        [Description("Cây kịch bản, chỉ dùng khi exitStrategyMode = Advanced (bỏ trống = không có).")] List<ScenarioNodeDto>? scenarioNodes = null)
    {
        // Status/TradeId cố tình không mở ra MCP — kế hoạch luôn tạo ở Draft (ADR-0004).
        var id = await McpDossierGate.GuardAsync(() => mediator.Send(new CreateTradePlanCommand
        {
            UserId = http.GetUserId(),
            Symbol = symbol,
            EntryPrice = entryPrice,
            StopLoss = stopLoss,
            Target = target,
            Quantity = quantity,
            PortfolioId = portfolioId,
            Direction = direction ?? "Buy",
            StrategyId = strategyId,
            MarketCondition = marketCondition ?? "Trending",
            Thesis = thesis,
            Notes = notes,
            ConfidenceLevel = confidenceLevel ?? 5,
            RiskPercent = riskPercent,
            AccountBalance = accountBalance,
            RiskRewardRatio = riskRewardRatio,
            ExpectedReviewDate = expectedReviewDate,
            TimeHorizon = timeHorizon,
            InvalidationCriteria = invalidationCriteria,
            Checklist = checklist,
            EntryMode = entryMode,
            Lots = lots,
            ExitTargets = exitTargets,
            ExitStrategyMode = exitStrategyMode,
            ScenarioNodes = scenarioNodes,
            Status = null,
            TradeId = null
        }, ct));

        // Trần biến động chỉ có nghĩa cho lệnh MUA gắn danh mục. Không gắn danh mục thì không có gì
        // để chiếu lên — im lặng, không phải "chưa kiểm được".
        if (string.IsNullOrWhiteSpace(portfolioId) || (direction ?? "Buy") != "Buy")
            return id;

        VolatilitySizingResult? sizing = null;
        try
        {
            sizing = await mediator.Send(new GetVolatilitySizingForPlanQuery
            {
                UserId = http.GetUserId(),
                PortfolioId = portfolioId,
                Symbol = symbol,
                EntryPrice = entryPrice,
                Quantity = quantity
            }, ct);
        }
        catch (Exception)
        {
            // Kế hoạch ĐÃ tạo xong. Truy vấn tham khảo hỏng không được phép làm lời gọi thất bại và
            // khiến agent tưởng chưa tạo được rồi tạo lại. Describe(null) sẽ nói "chưa kiểm được".
        }

        var notice = McpVolatilityNotice.Describe(sizing, quantity);
        return notice is null ? id : $"{id}\n\n{notice}";
    }

    [McpServerTool(Name = "update_trade_plan", Destructive = true)]
    [Description("Cập nhật một kế hoạch giao dịch theo id. Chỉ trường được truyền mới bị thay đổi.")]
    public static async Task<string> UpdateTradePlan(
        [Description("ID kế hoạch.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Mã chứng khoán (bỏ trống = giữ nguyên).")] string? symbol = null,
        [Description("ID danh mục (bỏ trống = giữ nguyên).")] string? portfolioId = null,
        [Description("Chiều lệnh: Buy hoặc Sell (bỏ trống = giữ nguyên).")] string? direction = null,
        [Description("Giá vào dự kiến, VND (bỏ trống = giữ nguyên).")] decimal? entryPrice = null,
        [Description("Giá cắt lỗ, VND (bỏ trống = giữ nguyên).")] decimal? stopLoss = null,
        [Description("Giá mục tiêu, VND (bỏ trống = giữ nguyên).")] decimal? target = null,
        [Description("Khối lượng dự kiến (bỏ trống = giữ nguyên).")] int? quantity = null,
        [Description("ID chiến lược (bỏ trống = giữ nguyên).")] string? strategyId = null,
        [Description("Bối cảnh thị trường (bỏ trống = giữ nguyên).")] string? marketCondition = null,
        [Description("Luận điểm đầu tư (bỏ trống = giữ nguyên).")] string? thesis = null,
        [Description("Ghi chú thêm (bỏ trống = giữ nguyên).")] string? notes = null,
        [Description("Mức độ tự tin 1–10 (bỏ trống = giữ nguyên).")] int? confidenceLevel = null,
        [Description("% tài khoản chấp nhận rủi ro (bỏ trống = giữ nguyên).")] decimal? riskPercent = null,
        [Description("Giá trị tài khoản dùng để tính rủi ro, VND (bỏ trống = giữ nguyên).")] decimal? accountBalance = null,
        [Description("Tỷ lệ lời/lỗ kỳ vọng (bỏ trống = giữ nguyên).")] decimal? riskRewardRatio = null,
        [Description("Ngày dự kiến review ISO-8601 (bỏ trống = giữ nguyên).")] DateTime? expectedReviewDate = null,
        [Description("Điều kiện phủ định luận điểm, ghi đè toàn bộ (bỏ trống = giữ nguyên).")] List<InvalidationRuleDto>? invalidationCriteria = null,
        [Description("Checklist, ghi đè toàn bộ (bỏ trống = giữ nguyên).")] List<ChecklistItemDto>? checklist = null,
        [Description("Kiểu vào lệnh nhiều lô: Single/Scaled (bỏ trống = giữ nguyên).")] string? entryMode = null,
        [Description("Các lô vào lệnh, ghi đè toàn bộ (bỏ trống = giữ nguyên).")] List<PlanLotDto>? lots = null,
        [Description("Các mốc chốt lời, ghi đè toàn bộ (bỏ trống = giữ nguyên).")] List<ExitTargetDto>? exitTargets = null,
        [Description("Kiểu chiến lược thoát: Simple/Advanced (bỏ trống = giữ nguyên).")] string? exitStrategyMode = null,
        [Description("Cây kịch bản, ghi đè toàn bộ (bỏ trống = giữ nguyên).")] List<ScenarioNodeDto>? scenarioNodes = null,
        [Description("Tầm nhìn: ShortTerm/MediumTerm/LongTerm (bỏ trống = giữ nguyên).")] string? timeHorizon = null)
    {
        await McpDossierGate.GuardAsync(() => mediator.Send(new UpdateTradePlanCommand
        {
            Id = id,
            UserId = http.GetUserId(),
            Symbol = symbol,
            PortfolioId = portfolioId,
            Direction = direction,
            EntryPrice = entryPrice,
            StopLoss = stopLoss,
            Target = target,
            Quantity = quantity,
            StrategyId = strategyId,
            MarketCondition = marketCondition,
            Thesis = thesis,
            Notes = notes,
            ConfidenceLevel = confidenceLevel,
            RiskPercent = riskPercent,
            AccountBalance = accountBalance,
            RiskRewardRatio = riskRewardRatio,
            ExpectedReviewDate = expectedReviewDate,
            InvalidationCriteria = invalidationCriteria,
            Checklist = checklist,
            EntryMode = entryMode,
            Lots = lots,
            ExitTargets = exitTargets,
            ExitStrategyMode = exitStrategyMode,
            ScenarioNodes = scenarioNodes,
            TimeHorizon = timeHorizon
        }, ct));
        return "ok";
    }

    [McpServerTool(Name = "set_trade_plan_status", Destructive = true)]
    [Description("Đổi trạng thái kế hoạch. 'restore' bị chặn qua MCP.")]
    public static async Task<string> SetTradePlanStatus(
        [Description("ID kế hoạch.")] string id,
        [Description("Trạng thái mới (vd: executed, cancelled).")] string status,
        [Description("ID lệnh liên kết nếu chuyển executed (tùy chọn).")] string? tradeId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (string.Equals(status, "restore", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("restore không được phép qua MCP surface.");
        await mediator.Send(new UpdateTradePlanStatusCommand
        {
            Id = id, UserId = http.GetUserId(), Status = status, TradeId = tradeId
        }, ct);
        return "ok";
    }
}
