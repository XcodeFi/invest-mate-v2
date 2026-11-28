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
    [Description("Dòng thời gian sự kiện (trade/nhật ký) theo mã chứng khoán, trong khoảng from–to tùy chọn. Mỗi mục trade có dossierAtTrade: hồ sơ công ty lúc đó (state unsigned|resigned-after|signed, daysSigned, resignedAt, freshness fresh|needs-review|expired; null nếu mã chưa có hồ sơ). Chỉ dựa vào lần ký HIỆN TẠI: lệnh trước lần ký đó chỉ báo resigned-after, không có phán quyết.")]
    public static async Task<SymbolTimelineDto> GetSymbolTimeline(
        [Description("Mã chứng khoán.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Từ ngày (tùy chọn).")] DateTime? from = null,
        [Description("Đến ngày (tùy chọn).")] DateTime? to = null)
        => await mediator.Send(new GetSymbolTimelineQuery
        {
            UserId = http.GetUserId(), Symbol = symbol, From = from, To = to
        }, ct);
}
