using System.ComponentModel;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class TradePlanTools
{
    [McpServerTool(Name = "list_trade_plans", ReadOnly = true)]
    [Description("Liệt kê kế hoạch giao dịch. activeOnly = true chỉ lấy kế hoạch đang hiệu lực.")]
    public static async Task<IEnumerable<TradePlanDto>> ListTradePlans(
        [Description("Chỉ lấy kế hoạch đang hoạt động.")] bool activeOnly,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTradePlansQuery { UserId = http.GetUserId(), ActiveOnly = activeOnly }, ct);

    [McpServerTool(Name = "get_trade_plan", ReadOnly = true)]
    [Description("Lấy chi tiết một kế hoạch giao dịch theo id. Null nếu không tồn tại/không thuộc chủ khóa.")]
    public static async Task<TradePlanDto?> GetTradePlan(
        [Description("ID kế hoạch.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTradePlanByIdQuery { Id = id, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "create_trade_plan", Destructive = true)]
    [Description("Tạo kế hoạch giao dịch mới. Luôn tạo ở trạng thái Nháp (Draft) — agent không tự khớp lệnh.")]
    public static async Task<string> CreateTradePlan(
        CreateTradePlanCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.UserId = http.GetUserId();
        command.Status = null;   // ép Draft (ADR-0004)
        command.TradeId = null;
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "update_trade_plan", Destructive = true)]
    [Description("Cập nhật một kế hoạch giao dịch theo id.")]
    public static async Task<string> UpdateTradePlan(
        [Description("ID kế hoạch.")] string id,
        UpdateTradePlanCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.Id = id;
        command.UserId = http.GetUserId();
        await mediator.Send(command, ct);
        return "ok";
    }

    [McpServerTool(Name = "set_trade_plan_status", Destructive = true)]
    [Description("Đổi trạng thái kế hoạch. 'restore' bị chặn qua MCP.")]
    public static async Task<string> SetTradePlanStatus(
        [Description("ID kế hoạch.")] string id,
        [Description("Trạng thái mới (vd: executed, cancelled).")] string status,
        [Description("ID lệnh liên kết nếu chuyển executed (tùy chọn).")] string? tradeId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (string.Equals(status, "restore", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("restore không được phép qua MCP surface.");
        await mediator.Send(new UpdateTradePlanStatusCommand
        {
            Id = id, UserId = http.GetUserId(), Status = status, TradeId = tradeId
        }, ct);
        return "ok";
    }
}
