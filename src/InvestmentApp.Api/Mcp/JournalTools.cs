using System.ComponentModel;
using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Application.Journals.Commands.DeleteJournal;
using InvestmentApp.Application.Journals.Commands.UpdateJournal;
using InvestmentApp.Application.Journals.Queries.GetJournalByTrade;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class JournalTools
{
    [McpServerTool(Name = "list_journals", ReadOnly = true)]
    [Description("Liệt kê nhật ký giao dịch (journal) gắn với các lệnh. portfolioId tùy chọn để lọc.")]
    public static async Task<IEnumerable<JournalDto>> ListJournals(
        [Description("ID danh mục cần lọc (tùy chọn).")] string? portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetJournalsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_journal_by_trade", ReadOnly = true)]
    [Description("Lấy nhật ký của một lệnh theo tradeId. Null nếu chưa có nhật ký.")]
    public static async Task<JournalDto?> GetJournalByTrade(
        [Description("ID lệnh giao dịch.")] string tradeId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetJournalByTradeQuery { TradeId = tradeId, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "create_journal", Destructive = true)]
    [Description("Tạo nhật ký cho một lệnh. Cần nhật ký độc lập không gắn lệnh thì dùng create_journal_entry.")]
    public static async Task<string> CreateJournal(
        [Description("ID lệnh giao dịch.")] string tradeId,
        [Description("ID danh mục chứa lệnh.")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Lý do vào lệnh (bỏ trống = để rỗng).")] string? entryReason = null,
        [Description("Bối cảnh thị trường (bỏ trống = để rỗng).")] string? marketContext = null,
        [Description("Mô hình/kỹ thuật của setup (bỏ trống = để rỗng).")] string? technicalSetup = null,
        [Description("Trạng thái cảm xúc (bỏ trống = để rỗng).")] string? emotionalState = null,
        [Description("Mức độ tự tin 1–10 (bỏ trống = 5).")] int? confidenceLevel = null,
        [Description("ID kế hoạch liên quan (bỏ trống = không gắn kế hoạch).")] string? tradePlanId = null)
        => await mediator.Send(new CreateJournalCommand
        {
            UserId = http.GetUserId(),
            TradeId = tradeId,
            PortfolioId = portfolioId,
            EntryReason = entryReason ?? string.Empty,
            MarketContext = marketContext ?? string.Empty,
            TechnicalSetup = technicalSetup ?? string.Empty,
            EmotionalState = emotionalState ?? string.Empty,
            ConfidenceLevel = confidenceLevel ?? 5,
            TradePlanId = tradePlanId
        }, ct);

    [McpServerTool(Name = "update_journal", Destructive = true)]
    [Description("Cập nhật nhật ký theo id. Chỉ trường được truyền mới bị thay đổi.")]
    public static async Task<string> UpdateJournal(
        [Description("ID nhật ký.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Lý do vào lệnh (bỏ trống = giữ nguyên).")] string? entryReason = null,
        [Description("Bối cảnh thị trường (bỏ trống = giữ nguyên).")] string? marketContext = null,
        [Description("Mô hình/kỹ thuật của setup (bỏ trống = giữ nguyên).")] string? technicalSetup = null,
        [Description("Trạng thái cảm xúc (bỏ trống = giữ nguyên).")] string? emotionalState = null,
        [Description("Mức độ tự tin 1–10 (bỏ trống = giữ nguyên).")] int? confidenceLevel = null,
        [Description("Review sau khi đóng lệnh (bỏ trống = giữ nguyên).")] string? postTradeReview = null,
        [Description("Bài học rút ra (bỏ trống = giữ nguyên).")] string? lessonsLearned = null,
        [Description("Đánh giá 1–5 (bỏ trống = giữ nguyên).")] int? rating = null,
        [Description("Danh sách tag, ghi đè toàn bộ (bỏ trống = giữ nguyên).")] List<string>? tags = null)
    {
        await mediator.Send(new UpdateJournalCommand
        {
            Id = id,
            UserId = http.GetUserId(),
            EntryReason = entryReason,
            MarketContext = marketContext,
            TechnicalSetup = technicalSetup,
            EmotionalState = emotionalState,
            ConfidenceLevel = confidenceLevel,
            PostTradeReview = postTradeReview,
            LessonsLearned = lessonsLearned,
            Rating = rating,
            Tags = tags
        }, ct);
        return "ok";
    }

    [McpServerTool(Name = "delete_journal", Destructive = true)]
    [Description("Xóa nhật ký theo id.")]
    public static async Task<string> DeleteJournal(
        [Description("ID nhật ký.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        await mediator.Send(new DeleteJournalCommand { Id = id, UserId = http.GetUserId() }, ct);
        return "ok";
    }
}
