using System.ComponentModel;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.DeleteJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Application.JournalEntries.Queries.GetJournalEntriesBySymbol;
using InvestmentApp.Application.Journals.Queries.GetTradesPendingReview;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class JournalEntryTools
{
    [McpServerTool(Name = "create_journal_entry", Destructive = true)]
    [Description("Tạo một mục nhật ký theo mã (standalone, không gắn lệnh).")]
    public static async Task<string> CreateJournalEntry(
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Loại mục, đúng 1 trong: Observation/PreTrade/DuringTrade/PostTrade/Review.")] string entryType,
        [Description("Tiêu đề ngắn.")] string title,
        [Description("Nội dung ghi chú.")] string content,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("ID danh mục liên quan (bỏ trống = không gắn danh mục).")] string? portfolioId = null,
        [Description("ID lệnh liên quan (bỏ trống = không gắn lệnh).")] string? tradeId = null,
        [Description("ID kế hoạch liên quan (bỏ trống = không gắn kế hoạch).")] string? tradePlanId = null,
        [Description("Trạng thái cảm xúc lúc ghi (bỏ trống = không ghi nhận).")] string? emotionalState = null,
        [Description("Mức độ tự tin 1–10 (bỏ trống = không ghi nhận).")] int? confidenceLevel = null,
        [Description("Giá tại thời điểm ghi, VND (bỏ trống = tự lấy giá hiện tại).")] decimal? priceAtTime = null,
        [Description("Bối cảnh thị trường (bỏ trống = không ghi nhận).")] string? marketContext = null,
        [Description("Danh sách tag (bỏ trống = không gắn tag).")] List<string>? tags = null,
        [Description("Thời điểm ghi ISO-8601 (bỏ trống = hiện tại).")] DateTime? timestamp = null)
        => await mediator.Send(new CreateJournalEntryCommand
        {
            UserId = http.GetUserId(),
            Symbol = symbol,
            EntryType = entryType,
            Title = title,
            Content = content,
            PortfolioId = portfolioId,
            TradeId = tradeId,
            TradePlanId = tradePlanId,
            EmotionalState = emotionalState,
            ConfidenceLevel = confidenceLevel,
            PriceAtTime = priceAtTime,
            MarketContext = marketContext,
            Tags = tags,
            Timestamp = timestamp
        }, ct);

    [McpServerTool(Name = "update_journal_entry", Destructive = true)]
    [Description("Cập nhật một mục nhật ký theo id. Chỉ trường được truyền mới bị thay đổi. Trả về false nếu không tìm thấy.")]
    public static async Task<bool> UpdateJournalEntry(
        [Description("ID mục nhật ký.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Tiêu đề mới (bỏ trống = giữ nguyên).")] string? title = null,
        [Description("Nội dung mới (bỏ trống = giữ nguyên).")] string? content = null,
        [Description("Loại mục mới: Observation/PreTrade/DuringTrade/PostTrade/Review (bỏ trống = giữ nguyên).")] string? entryType = null,
        [Description("Trạng thái cảm xúc mới (bỏ trống = giữ nguyên).")] string? emotionalState = null,
        [Description("Mức độ tự tin 1–10 (bỏ trống = giữ nguyên).")] int? confidenceLevel = null,
        [Description("Bối cảnh thị trường mới (bỏ trống = giữ nguyên).")] string? marketContext = null,
        [Description("Danh sách tag mới, ghi đè toàn bộ (bỏ trống = giữ nguyên).")] List<string>? tags = null,
        [Description("Đánh giá 1–5 (bỏ trống = giữ nguyên).")] int? rating = null)
        => await mediator.Send(new UpdateJournalEntryCommand
        {
            Id = id,
            UserId = http.GetUserId(),
            Title = title,
            Content = content,
            EntryType = entryType,
            EmotionalState = emotionalState,
            ConfidenceLevel = confidenceLevel,
            MarketContext = marketContext,
            Tags = tags,
            Rating = rating
        }, ct);

    [McpServerTool(Name = "delete_journal_entry", Destructive = true)]
    [Description("Xóa một mục nhật ký theo id. Trả về false nếu không tìm thấy.")]
    public static async Task<bool> DeleteJournalEntry(
        [Description("ID mục nhật ký.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new DeleteJournalEntryCommand { Id = id, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "list_trades_pending_review", ReadOnly = true)]
    [Description("Liệt kê các lệnh chưa có nhật ký (cần review). portfolioId tùy chọn để lọc.")]
    public static async Task<List<PendingReviewTradeDto>> ListTradesPendingReview(
        [Description("ID danh mục cần lọc (tùy chọn).")] string? portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTradesPendingReviewQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "list_journal_entries_by_symbol", ReadOnly = true)]
    [Description("Liệt kê mục nhật ký theo mã, trong khoảng from–to tùy chọn. Bắt buộc có symbol.")]
    public static async Task<List<JournalEntryDto>> ListJournalEntriesBySymbol(
        [Description("Mã chứng khoán (bắt buộc).")] string symbol,
        [Description("Từ ngày (tùy chọn).")] DateTime? from,
        [Description("Đến ngày (tùy chọn).")] DateTime? to,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new InvalidOperationException("symbol là bắt buộc.");
        return await mediator.Send(new GetJournalEntriesBySymbolQuery
        {
            UserId = http.GetUserId(), Symbol = symbol, From = from, To = to
        }, ct);
    }
}
