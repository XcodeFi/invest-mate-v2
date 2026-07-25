using System.ComponentModel;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
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
}
