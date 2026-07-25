using System.ComponentModel;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.TradePlans.Queries.GetActivePositions;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class PortfolioTools
{
    [McpServerTool(Name = "list_portfolios", ReadOnly = true)]
    [Description("Liệt kê danh mục đầu tư của chủ khóa API (id + tên). Dùng để lấy portfolioId trước khi ghi lệnh.")]
    public static async Task<List<PortfolioSummaryDto>> ListPortfolios(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetAllPortfoliosQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "list_positions", ReadOnly = true)]
    [Description("Liệt kê vị thế (holdings) đang mở. portfolioId tùy chọn để lọc theo danh mục.")]
    public static async Task<List<ActivePositionDto>> ListPositions(
        [Description("ID danh mục cần lọc (bỏ trống = tất cả).")] string? portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetActivePositionsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);
}
