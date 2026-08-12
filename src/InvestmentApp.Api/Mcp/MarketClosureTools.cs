using System.ComponentModel;
using System.Globalization;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class MarketClosureTools
{
    private const string DateFormat = "yyyy-MM-dd";

    [McpServerTool(Name = "list_market_closures", ReadOnly = true)]
    [Description("Liệt kê ngày nghỉ giao dịch của một năm, nhóm theo tháng. Thứ Bảy và Chủ nhật không nằm trong danh sách vì đã được suy ra tự động.")]
    public static async Task<MarketClosureYearDto> ListMarketClosures(
        [Description("Năm cần xem, ví dụ 2026.")] int year,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetMarketClosuresQuery(http.GetUserId(), year), ct);

    [McpServerTool(Name = "add_market_closures", Destructive = true)]
    [Description("Nhập ngày nghỉ giao dịch (nghỉ lễ). Gửi được một ngày, một đợt lễ, hay cả năm trong cùng một lần gọi. Nhập lại ngày đã có là no-op. Thứ Bảy/Chủ nhật gửi lên sẽ bị bỏ qua vì đã là ngày nghỉ.")]
    public static async Task<AddMarketClosuresResult> AddMarketClosures(
        [Description("Danh sách ngày, mỗi phần tử dạng YYYY-MM-DD. Ví dụ [\"2026-04-30\",\"2026-05-01\"].")] string[] dates,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        // note phải nằm SAU ct và có `= null`: nullable một mình không đủ để schema
        // đánh dấu optional, agent sẽ buộc phải gửi mới gọi được.
        [Description("Ghi chú chung cho cả đợt, ví dụ \"Tết Bính Ngọ\".")] string? note = null)
        => await mediator.Send(new AddMarketClosuresCommand(http.GetUserId(), Parse(dates), note), ct);

    [McpServerTool(Name = "remove_market_closure", Destructive = true)]
    [Description("Xoá một ngày nghỉ giao dịch đã nhập. Dùng khi lịch nghỉ được điều chỉnh hoặc nhập nhầm.")]
    public static async Task<bool> RemoveMarketClosure(
        [Description("Ngày cần xoá, dạng YYYY-MM-DD.")] string date,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new RemoveMarketClosureCommand(http.GetUserId(), ParseOne(date)), ct);

    private static IReadOnlyList<DateTime> Parse(string[] dates)
        => dates.Select(ParseOne).ToList();

    // Lỗi phải nói rõ phải gửi gì cho đúng, không chỉ nói là sai.
    private static DateTime ParseOne(string value)
        => DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : throw new ArgumentException(
                $"Ngày \"{value}\" không đúng định dạng. Cần dạng YYYY-MM-DD, ví dụ 2026-04-30.", nameof(value));
}
