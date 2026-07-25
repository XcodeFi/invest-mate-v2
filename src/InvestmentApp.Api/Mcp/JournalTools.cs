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
    [Description("Tạo nhật ký cho một lệnh.")]
    public static async Task<string> CreateJournal(
        CreateJournalCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "update_journal", Destructive = true)]
    [Description("Cập nhật nhật ký theo id.")]
    public static async Task<string> UpdateJournal(
        [Description("ID nhật ký.")] string id,
        UpdateJournalCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.Id = id;
        command.UserId = http.GetUserId();
        await mediator.Send(command, ct);
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
