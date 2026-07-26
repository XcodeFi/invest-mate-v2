using System.ComponentModel;
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
    [Description("Hàng đợi quyết định hôm nay — gộp cảnh báo StopLoss + Scenario + Thesis-review đã dedupe, sort theo severity. Trả lời câu 'hôm nay cần quyết gì'.")]
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

    [McpServerTool(Name = "get_pending_thesis_reviews", ReadOnly = true)]
    [Description("Danh sách trade plan đang active quá hạn review thesis, kèm số ngày overdue và lý do (invalidation check / periodic review).")]
    public static async Task<List<PendingThesisReviewDto>> GetPendingThesisReviews(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetPendingThesisReviewsQuery { UserId = http.GetUserId() }, ct);
}
