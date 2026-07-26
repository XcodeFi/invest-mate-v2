using System.ComponentModel;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.Trades.Commands.BulkCreateTrades;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using InvestmentApp.Application.Trades.Commands.DeleteTrade;
using InvestmentApp.Application.Trades.Commands.LinkTradeToPlan;
using InvestmentApp.Application.Trades.Queries.GetTradesByPortfolio;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class TradeTools
{
    [McpServerTool(Name = "calculate_fees", ReadOnly = true)]
    [Description("Ước tính phí + thuế cho một lệnh (BUY/SELL). Không ghi dữ liệu.")]
    public static FeeCalculationResponse CalculateFees(
        [Description("Loại lệnh: BUY hoặc SELL.")] string tradeType,
        [Description("Khối lượng.")] decimal quantity,
        [Description("Giá mỗi cổ phiếu (VND).")] decimal price,
        IFeeCalculationService feeService)
        => AgentTradeFeeCalculator.Calculate(feeService, tradeType, quantity, price);

    [McpServerTool(Name = "create_trade", Destructive = true)]
    [Description("Ghi một lệnh thật. Bỏ trống portfolioId để tự chọn (khi chỉ có 1 danh mục); bỏ trống fee/tax để tự tính (fee KHÔNG gồm thuế).")]
    public static async Task<string> CreateTrade(
        [Description("ID danh mục. Bỏ trống = tự chọn nếu chỉ 1 danh mục.")] string? portfolioId,
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Loại lệnh: BUY hoặc SELL.")] string tradeType,
        [Description("Khối lượng.")] decimal quantity,
        [Description("Giá mỗi cổ phiếu (VND).")] decimal price,
        [Description("Phí môi giới (VND). Bỏ trống = tự tính.")] decimal? fee,
        [Description("Thuế TNCN (VND). Bỏ trống = tự tính.")] decimal? tax,
        [Description("Ngày giao dịch (bỏ trống = hôm nay).")] DateTime? tradeDate,
        IMediator mediator, IFeeCalculationService feeService, IHttpContextAccessor http, CancellationToken ct)
    {
        var userId = http.GetUserId();

        if (string.IsNullOrWhiteSpace(portfolioId))
        {
            var portfolios = await mediator.Send(new GetAllPortfoliosQuery { UserId = userId }, ct);
            if (portfolios.Count == 0)
                throw new InvalidOperationException("Chưa có danh mục nào — tạo danh mục trước khi ghi lệnh.");
            if (portfolios.Count > 1)
                throw new InvalidOperationException(
                    "Có nhiều danh mục — cần chỉ định portfolioId. Danh mục: " +
                    string.Join(", ", portfolios.Select(p => $"{p.Name} ({p.Id})")));
            portfolioId = portfolios[0].Id;
        }

        var resolvedFee = fee ?? 0m;
        var resolvedTax = tax ?? 0m;
        if (fee is null || tax is null)
        {
            var calc = AgentTradeFeeCalculator.Calculate(feeService, tradeType, quantity, price);
            // Fee = broker cost only (TransactionFee + VAT); Tax stored SEPARATELY (ADR-0006) — no double-count.
            if (fee is null) resolvedFee = calc.TransactionFee + calc.Vat;
            if (tax is null) resolvedTax = calc.Breakdown.Tax;
        }

        return await mediator.Send(new CreateTradeCommand
        {
            UserId = userId, Origin = "AI_AGENT", PortfolioId = portfolioId,
            Symbol = symbol, TradeType = tradeType, Quantity = quantity, Price = price,
            Fee = resolvedFee, Tax = resolvedTax, TradeDate = tradeDate
        }, ct);
    }

    [McpServerTool(Name = "get_trades_by_portfolio", ReadOnly = true)]
    [Description("Danh sách lệnh đã ghi của một danh mục, có phân trang và lọc theo mã / loại lệnh.")]
    public static async Task<TradeListDto> GetTradesByPortfolio(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Lọc theo mã chứng khoán (bỏ trống = tất cả).")] string? symbol = null,
        [Description("Lọc theo loại lệnh: BUY hoặc SELL (bỏ trống = cả hai).")] string? tradeType = null,
        [Description("Trang, bắt đầu từ 1 (mặc định 1).")] int? page = null,
        [Description("Số lệnh mỗi trang (mặc định 20).")] int? pageSize = null)
        => await mediator.Send(new GetTradesByPortfolioQuery
        {
            UserId = http.GetUserId(),
            PortfolioId = portfolioId,
            Symbol = string.IsNullOrWhiteSpace(symbol) ? null : symbol.ToUpperInvariant().Trim(),
            TradeType = tradeType,
            Page = page ?? 1,
            PageSize = pageSize ?? 20
        }, ct);

    [McpServerTool(Name = "delete_trade", Destructive = true)]
    [Description("Xóa vĩnh viễn một lệnh đã ghi — KHÔNG khôi phục được. Số liệu danh mục sẽ được tính lại. Hỏi người dùng trước khi gọi.")]
    public static async Task<bool> DeleteTrade(
        [Description("ID lệnh cần xóa.")] string tradeId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new DeleteTradeCommand { UserId = http.GetUserId(), TradeId = tradeId }, ct);

    [McpServerTool(Name = "link_trade_to_plan", Destructive = true)]
    [Description("Gắn một lệnh đã ghi vào kế hoạch giao dịch, để lệnh được tính vào kết quả của kế hoạch đó. Kế hoạch chưa có lệnh nào phải đang ở trạng thái InProgress — nếu còn Draft/Ready thì gọi sẽ lỗi.")]
    public static async Task<bool> LinkTradeToPlan(
        [Description("ID lệnh.")] string tradeId,
        [Description("ID kế hoạch giao dịch.")] string planId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new LinkTradeToPlanCommand
        {
            UserId = http.GetUserId(), TradeId = tradeId, PlanId = planId
        }, ct);

    [McpServerTool(Name = "bulk_create_trades", Destructive = true)]
    [Description("Ghi nhiều lệnh cùng lúc vào một danh mục (vd nhập lịch sử giao dịch). Kết quả có thể thành công một phần: xem successCount/failedCount/errors. Khác create_trade — fee/tax KHÔNG tự tính; bỏ trống = 0 (dùng calculate_fees trước nếu muốn số đúng).")]
    public static async Task<BulkCreateTradesResult> BulkCreateTrades(
        [Description("ID danh mục nhận các lệnh.")] string portfolioId,
        [Description("Danh sách lệnh: symbol, tradeType (BUY/SELL), quantity, price, fee, tax, tradeDate (tùy chọn).")] List<BulkTradeItem> trades,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (trades.Count == 0)
            throw new ArgumentException("Danh sách lệnh trống.", nameof(trades));

        for (int i = 0; i < trades.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(trades[i].Symbol))
                throw new ArgumentException($"Lệnh thứ {i + 1} thiếu mã chứng khoán.", nameof(trades));
            trades[i].Symbol = trades[i].Symbol.ToUpperInvariant().Trim();
        }

        return await mediator.Send(new BulkCreateTradesCommand
        {
            UserId = http.GetUserId(), PortfolioId = portfolioId, Trades = trades
        }, ct);
    }
}
