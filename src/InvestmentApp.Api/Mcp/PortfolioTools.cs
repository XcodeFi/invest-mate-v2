using System.ComponentModel;
using InvestmentApp.Application.Portfolios.Commands.CreatePortfolio;
using InvestmentApp.Application.Portfolios.Commands.DeletePortfolio;
using InvestmentApp.Application.Portfolios.Commands.UpdatePortfolio;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.Portfolios.Queries.GetPortfolio;
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

    [McpServerTool(Name = "get_portfolio", ReadOnly = true)]
    [Description("Chi tiết một danh mục: vốn ban đầu/hiện tại, tiền mặt còn lại, giá trị thị trường, P/L.")]
    public static async Task<PortfolioDto?> GetPortfolio(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetPortfolioQuery { UserId = http.GetUserId(), Id = portfolioId }, ct);

    [McpServerTool(Name = "create_portfolio", Destructive = true)]
    [Description("Tạo danh mục đầu tư mới. Trả về ID danh mục vừa tạo.")]
    public static async Task<string> CreatePortfolio(
        [Description("Tên danh mục.")] string name,
        [Description("Vốn ban đầu (VND).")] decimal initialCapital,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new CreatePortfolioCommand
        {
            UserId = http.GetUserId(), Name = name, InitialCapital = initialCapital
        }, ct);

    [McpServerTool(Name = "update_portfolio", Destructive = true)]
    [Description("Đổi tên danh mục. Không đổi được vốn ban đầu (dùng nạp/rút vốn thay thế).")]
    public static async Task<bool> UpdatePortfolio(
        [Description("ID danh mục.")] string portfolioId,
        [Description("Tên mới.")] string name,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new UpdatePortfolioCommand
        {
            UserId = http.GetUserId(), Id = portfolioId, Name = name
        }, ct);

    [McpServerTool(Name = "delete_portfolio", Destructive = true)]
    [Description("Ẩn một danh mục (soft delete — dữ liệu còn trong DB nhưng danh mục biến mất khỏi mọi truy vấn, và các lệnh thuộc nó không thao tác được nữa). Hỏi người dùng trước khi gọi.")]
    public static async Task<bool> DeletePortfolio(
        [Description("ID danh mục cần xóa.")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new DeletePortfolioCommand { UserId = http.GetUserId(), Id = portfolioId }, ct);
}
