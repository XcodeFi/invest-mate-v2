using System.ComponentModel;
using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class SymbolTools
{
    [McpServerTool(Name = "get_symbol_timeline", ReadOnly = true)]
    [Description("Dòng thời gian sự kiện (trade/nhật ký) theo mã chứng khoán, trong khoảng from–to tùy chọn.")]
    public static async Task<SymbolTimelineDto> GetSymbolTimeline(
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Từ ngày (tùy chọn).")] DateTime? from,
        [Description("Đến ngày (tùy chọn).")] DateTime? to,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetSymbolTimelineQuery
        {
            UserId = http.GetUserId(), Symbol = symbol, From = from, To = to
        }, ct);
}
