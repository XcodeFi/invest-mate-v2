using System.ComponentModel;
using InvestmentApp.Application.Decisions.Commands.ResolveDecision;
using InvestmentApp.Application.Risk.Commands.SetRiskProfile;
using InvestmentApp.Application.Risk.Commands.SetStopLossTarget;
using InvestmentApp.Application.TradePlans.Commands.AbortTradePlan;
using InvestmentApp.Application.TradePlans.Commands.ExecuteLot;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.TradePlans.Commands.TriggerExitTarget;
using InvestmentApp.Application.TradePlans.Commands.UpdateStopLoss;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// P4 — hành động thực thi kế hoạch. Toàn bộ là write (Destructive) nên host MCP sẽ hỏi xác nhận.
/// Enum nhận dạng chuỗi + validate tại wrapper để agent nhận lỗi tiếng Việt rõ ràng thay vì
/// lỗi parse của tầng dưới. Command trả <see cref="Unit"/> được đổi thành câu xác nhận đọc được.
/// </summary>
[McpServerToolType]
public static class PlanActionTools
{
    private static string Normalize(string symbol) => symbol.ToUpperInvariant().Trim();

    private static TEnum ParseEnum<TEnum>(string value, string paramName) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                $"Giá trị '{value}' không hợp lệ. Chọn một trong: {string.Join(", ", Enum.GetNames<TEnum>())}.",
                paramName);
        return parsed;
    }

    [McpServerTool(Name = "resolve_decision", Destructive = true)]
    [Description("Xử lý một mục trong hàng đợi quyết định (lấy từ get_decision_queue). ExecuteSell = BÁN theo kế hoạch, cần tradePlanId. HoldWithJournal = GIỮ và ghi nhật ký, cần note ≥ 20 ký tự. ExecuteSell tạo lệnh bán thật — luôn hỏi người dùng trước.")]
    public static async Task<ResolveDecisionResult> ResolveDecision(
        [Description("ID mục quyết định, lấy nguyên văn từ get_decision_queue.")] string decisionId,
        [Description("Hành động: ExecuteSell hoặc HoldWithJournal.")] string action,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("ID kế hoạch giao dịch — bắt buộc cho ExecuteSell.")] string? tradePlanId = null,
        [Description("Mã chứng khoán — dùng cho HoldWithJournal khi mục không gắn kế hoạch.")] string? symbol = null,
        [Description("Lý do giữ, tối thiểu 20 ký tự — bắt buộc cho HoldWithJournal.")] string? note = null)
        => await mediator.Send(new ResolveDecisionCommand
        {
            UserId = http.GetUserId(),
            DecisionId = decisionId,
            Action = ParseEnum<DecisionAction>(action, nameof(action)),
            TradePlanId = tradePlanId,
            Symbol = string.IsNullOrWhiteSpace(symbol) ? null : Normalize(symbol),
            Note = note
        }, ct);

    [McpServerTool(Name = "review_trade_plan", Destructive = true)]
    [Description("Chốt review một kế hoạch đã thực thi xong: tính P/L, số ngày nắm giữ, mức đạt mục tiêu và chuyển kế hoạch sang trạng thái Reviewed. Kế hoạch phải đang ở trạng thái Executed — trạng thái khác sẽ báo lỗi.")]
    public static async Task<CampaignReviewDto> ReviewTradePlan(
        [Description("ID kế hoạch giao dịch.")] string planId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Bài học rút ra (tùy chọn).")] string? lessonsLearned = null)
        => await mediator.Send(new ReviewTradePlanCommand
        {
            UserId = http.GetUserId(), PlanId = planId, LessonsLearned = lessonsLearned
        }, ct);

    [McpServerTool(Name = "abort_trade_plan", Destructive = true)]
    [Description("Hủy kế hoạch vì luận điểm đầu tư đã sai (thesis invalidated). Dùng khi điều kiện phá thesis đã xảy ra, không dùng để hủy vì đổi ý.")]
    public static async Task<AbortTradePlanResult> AbortTradePlan(
        [Description("ID kế hoạch giao dịch.")] string planId,
        [Description("Nguyên nhân: EarningsMiss, TrendBreak, NewsShock, ThesisTimeout hoặc Manual.")] string trigger,
        [Description("Mô tả thesis sai ở đâu, tối thiểu 20 ký tự.")] string detail,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        var parsed = ParseEnum<InvalidationTrigger>(trigger, nameof(trigger));
        return await mediator.Send(new AbortTradePlanCommand
        {
            UserId = http.GetUserId(), PlanId = planId, Trigger = parsed.ToString(), Detail = detail
        }, ct);
    }

    [McpServerTool(Name = "execute_lot", Destructive = true)]
    [Description("Đánh dấu một lô (lot) của kế hoạch nhiều lô đã khớp, gắn với lệnh đã ghi và giá khớp thực tế. Chỉ dùng cho kế hoạch nhiều lô đang InProgress và lô còn Pending — sai điều kiện sẽ báo lỗi. Lệnh phải thuộc danh mục của chính bạn.")]
    public static async Task<string> ExecuteLot(
        [Description("ID kế hoạch giao dịch.")] string planId,
        [Description("Số thứ tự lô, bắt đầu từ 1.")] int lotNumber,
        [Description("ID lệnh đã ghi tương ứng với lô này.")] string tradeId,
        [Description("Giá khớp thực tế (VND).")] decimal actualPrice,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        await mediator.Send(new ExecuteLotCommand
        {
            UserId = http.GetUserId(), PlanId = planId,
            LotNumber = lotNumber, TradeId = tradeId, ActualPrice = actualPrice
        }, ct);
        return $"Đã ghi nhận lô {lotNumber} khớp ở giá {actualPrice:N0} cho kế hoạch {planId}.";
    }

    [McpServerTool(Name = "update_stop_loss", Destructive = true)]
    [Description("Dời mức cắt lỗ của kế hoạch. Mỗi lần dời đều được lưu vào lịch sử SL và ảnh hưởng điểm kỷ luật nếu nới rộng SL.")]
    public static async Task<string> UpdateStopLoss(
        [Description("ID kế hoạch giao dịch.")] string planId,
        [Description("Mức cắt lỗ mới (VND).")] decimal newStopLoss,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Lý do dời SL (tùy chọn nhưng nên có).")] string? reason = null)
    {
        if (newStopLoss <= 0)
            throw new ArgumentException("Mức cắt lỗ phải lớn hơn 0.", nameof(newStopLoss));

        await mediator.Send(new UpdateStopLossCommand
        {
            UserId = http.GetUserId(), PlanId = planId, NewStopLoss = newStopLoss, Reason = reason
        }, ct);
        return $"Đã dời cắt lỗ của kế hoạch {planId} về {newStopLoss:N0}.";
    }

    [McpServerTool(Name = "trigger_exit_target", Destructive = true)]
    [Description("Đánh dấu một mốc chốt lời (exit target) của kế hoạch đã đạt, gắn với lệnh bán đã ghi. Không idempotent — gọi lại cùng một mốc sẽ ghi trùng, chỉ gọi một lần cho mỗi mốc. Lệnh phải thuộc danh mục của chính bạn.")]
    public static async Task<string> TriggerExitTarget(
        [Description("ID kế hoạch giao dịch.")] string planId,
        [Description("Cấp mốc chốt lời, bắt đầu từ 1.")] int level,
        [Description("ID lệnh bán đã ghi.")] string tradeId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        await mediator.Send(new TriggerExitTargetCommand
        {
            UserId = http.GetUserId(), PlanId = planId, Level = level, TradeId = tradeId
        }, ct);
        return $"Đã đánh dấu mốc chốt lời {level} của kế hoạch {planId}.";
    }

    [McpServerTool(Name = "set_stop_loss_target", Destructive = true)]
    [Description("Đặt mức cắt lỗ / chốt lời cho một lệnh đang nắm giữ, để hệ thống theo dõi và cảnh báo. symbol và entryPrice phải khớp với lệnh thật — hệ thống không tự đối chiếu, nhập sai sẽ theo dõi nhầm mã.")]
    public static async Task<string> SetStopLossTarget(
        [Description("ID lệnh đã ghi.")] string tradeId,
        [Description("ID danh mục chứa lệnh.")] string portfolioId,
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Giá vào lệnh (VND).")] decimal entryPrice,
        [Description("Giá cắt lỗ (VND).")] decimal stopLossPrice,
        [Description("Giá mục tiêu (VND).")] decimal targetPrice,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Phần trăm trailing stop, vd 7 = 7% (bỏ trống = không dùng trailing).")] decimal? trailingStopPercent = null)
        => await mediator.Send(new SetStopLossTargetCommand
        {
            UserId = http.GetUserId(),
            TradeId = tradeId,
            PortfolioId = portfolioId,
            Symbol = Normalize(symbol),
            EntryPrice = entryPrice,
            StopLossPrice = stopLossPrice,
            TargetPrice = targetPrice,
            TrailingStopPercent = trailingStopPercent
        }, ct);

    [McpServerTool(Name = "set_risk_profile", Destructive = true)]
    [Description("Đặt hạn mức rủi ro cho một danh mục. Bỏ trống một hạn mức = giữ nguyên giá trị hiện tại, hoặc nhận giá trị mặc định nếu danh mục chưa có hồ sơ rủi ro.")]
    public static async Task<string> SetRiskProfile(
        [Description("ID danh mục.")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Tỷ trọng tối đa mỗi vị thế (%).")] decimal? maxPositionSizePercent = null,
        [Description("Tỷ trọng tối đa mỗi ngành (%).")] decimal? maxSectorExposurePercent = null,
        [Description("Ngưỡng cảnh báo sụt giảm vốn (%).")] decimal? maxDrawdownAlertPercent = null,
        [Description("Tỷ lệ lãi/lỗ kỳ vọng tối thiểu, vd 2 = 2:1.")] decimal? defaultRiskRewardRatio = null,
        [Description("Rủi ro tối đa toàn danh mục (%).")] decimal? maxPortfolioRiskPercent = null,
        [Description("Số lệnh tối đa mỗi ngày.")] int? maxDailyTrades = null,
        [Description("Giới hạn lỗ trong ngày (%).")] decimal? dailyLossLimitPercent = null)
        => await mediator.Send(new SetRiskProfileCommand
        {
            UserId = http.GetUserId(),
            PortfolioId = portfolioId,
            MaxPositionSizePercent = maxPositionSizePercent,
            MaxSectorExposurePercent = maxSectorExposurePercent,
            MaxDrawdownAlertPercent = maxDrawdownAlertPercent,
            DefaultRiskRewardRatio = defaultRiskRewardRatio,
            MaxPortfolioRiskPercent = maxPortfolioRiskPercent,
            MaxDailyTrades = maxDailyTrades,
            DailyLossLimitPercent = dailyLossLimitPercent
        }, ct);
}
