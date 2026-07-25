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
        CreateJournalEntryCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "update_journal_entry", Destructive = true)]
    [Description("Cập nhật một mục nhật ký theo id. Trả về false nếu không tìm thấy.")]
    public static async Task<bool> UpdateJournalEntry(
        [Description("ID mục nhật ký.")] string id,
        UpdateJournalEntryCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.Id = id;
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

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
