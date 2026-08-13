using System.ComponentModel;
using InvestmentApp.Application.Decisions.Commands.ResolveDecision;
using InvestmentApp.Application.Decisions.DTOs;
using InvestmentApp.Application.Decisions.Queries.GetDecisionQueue;
using InvestmentApp.Application.Discipline.Queries;
using InvestmentApp.Application.TradePlans.Queries.GetPendingThesisReviews;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class DecisionTools
{
    [McpServerTool(Name = "get_decision_queue", ReadOnly = true)]
    [Description("Hàng đợi quyết định hôm nay — gộp StopLoss + Scenario + Thesis-review + Cơ hội mua (watchlist chạm mục tiêu) + Vị thế thiếu stop-loss, đã dedupe, sort theo severity. Trả lời câu 'hôm nay cần quyết gì'.")]
    public static async Task<DecisionQueueDto> GetDecisionQueue(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetDecisionQueueQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_discipline_score", ReadOnly = true)]
    [Description("Điểm kỷ luật 0–100 + 3 thành phần (SL integrity, plan quality, review timeliness) trong khoảng thời gian chọn.")]
    public static async Task<DisciplineScoreDto> GetDisciplineScore(
        [Description("Số ngày thống kê: 7/30/90/365 (bỏ trống = 90).")] int? days,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetDisciplineScoreQuery { UserId = http.GetUserId(), Days = days ?? 90 }, ct);

    [McpServerTool(Name = "get_discipline_streak", ReadOnly = true)]
    [Description("Chuỗi ngày liên tiếp không vi phạm stop-loss (kỷ luật streak).")]
    public static async Task<DisciplineStreakDto> GetDisciplineStreak(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetDisciplineStreakQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "hold_decision", Destructive = true)]
    [Description("Xử lý một việc trong Hàng đợi quyết định theo hướng GIỮ: ghi lý do vào nhật ký và "
               + "thẻ đó thôi hiện cho hết ngày (giờ Việt Nam). Lý do BẮT BUỘC ≥ 20 ký tự — luật này "
               + "tồn tại để người dùng nghĩ thật thay vì bấm cho qua, nên đừng tự bịa lý do: hỏi "
               + "người dùng vì sao giữ rồi ghi đúng ý họ. "
               + "Truyền tradePlanId và portfolioId đúng như get_decision_queue trả về, nếu không "
               + "phạm vi dập cảnh báo bị rộng hơn thẻ đang xử lý. "
               + "KHÔNG có đường BÁN qua MCP: dùng create_trade sau khi đã thống nhất khối lượng với "
               + "người dùng, vì bán bao nhiêu là quyết định của họ chứ không phải mặc định theo kế hoạch.")]
    public static async Task<string> HoldDecision(
        [Description("ID việc cần xử lý, lấy nguyên văn từ get_decision_queue.")] string decisionId,
        [Description("Lý do giữ, ≥ 20 ký tự. Ghi theo ý người dùng.")] string note,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("ID kế hoạch của thẻ (bỏ trống nếu thẻ không gắn kế hoạch).")] string? tradePlanId = null,
        [Description("Mã chứng khoán của thẻ. Bắt buộc khi không có tradePlanId.")] string? symbol = null,
        [Description("ID danh mục của thẻ. Truyền khi thẻ thuộc một danh mục, để không dập cảnh báo cùng mã ở danh mục khác.")] string? portfolioId = null)
    {
        // Action cố định HoldWithJournal, KHÔNG mở ra tham số. ExecuteSell tạo lệnh bán với khối
        // lượng lấy cứng từ kế hoạch — đường đó vừa bị bỏ khỏi giao diện vì không cho sửa số lượng,
        // mở lại cho agent là dựng lại đúng cái vừa tháo.
        var result = await McpErrorTranslator.RunAsync(() => mediator.Send(new ResolveDecisionCommand
        {
            DecisionId = decisionId,
            Action = DecisionAction.HoldWithJournal,
            Note = note,
            TradePlanId = tradePlanId,
            Symbol = symbol,
            PortfolioId = portfolioId,
            UserId = http.GetUserId()
        }, ct));
        return result.Message;
    }

    [McpServerTool(Name = "get_pending_thesis_reviews", ReadOnly = true)]
    [Description("Danh sách trade plan đang active quá hạn review thesis, kèm số ngày overdue và lý do (invalidation check / periodic review).")]
    public static async Task<List<PendingThesisReviewDto>> GetPendingThesisReviews(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetPendingThesisReviewsQuery { UserId = http.GetUserId() }, ct);
}
