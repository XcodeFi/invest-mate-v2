using System.ComponentModel;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetBatchPrices;
using InvestmentApp.Application.MarketData.Queries.GetMarketOverview;
using InvestmentApp.Application.MarketData.Queries.GetStockDetail;
using InvestmentApp.Application.MarketData.Queries.GetStockPrice;
using InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;
using InvestmentApp.Application.MarketData.Queries.GetTopFluctuation;
using InvestmentApp.Application.MarketData.Queries.SearchStocks;
using MediatR;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// Dữ liệu thị trường là công khai — các query không mang UserId nên tool không cần IHttpContextAccessor.
/// Symbol chuẩn hóa uppercase tại wrapper theo quy ước symbol toàn dự án.
/// </summary>
[McpServerToolType]
public static class MarketTools
{
    /// <summary>Mặc định khoảng lịch sử giá — khớp MarketDataController.GetPriceHistory.</summary>
    private const int DefaultHistoryMonths = 3;

    private static string Normalize(string symbol) => symbol.ToUpperInvariant().Trim();

    [McpServerTool(Name = "get_stock_detail", ReadOnly = true)]
    [Description("Thông tin chi tiết doanh nghiệp niêm yết theo mã: tên công ty, sàn, ngành, chỉ số cơ bản.")]
    public static async Task<StockDetailDto> GetStockDetail(
        [Description("Mã chứng khoán, vd FPT.")] string symbol,
        IMediator mediator, CancellationToken ct)
        => await mediator.Send(new GetStockDetailQuery { Symbol = Normalize(symbol) }, ct);

    [McpServerTool(Name = "get_stock_price", ReadOnly = true)]
    [Description("Giá hiện tại của một mã chứng khoán (OHLC + khối lượng phiên gần nhất).")]
    public static async Task<StockPriceDto> GetStockPrice(
        [Description("Mã chứng khoán, vd HPG.")] string symbol,
        IMediator mediator, CancellationToken ct)
        => await mediator.Send(new GetStockPriceQuery { Symbol = Normalize(symbol) }, ct);

    [McpServerTool(Name = "get_stock_price_history", ReadOnly = true)]
    [Description("Lịch sử giá theo ngày của một mã trong khoảng thời gian.")]
    public static async Task<List<StockPriceHistoryDto>> GetStockPriceHistory(
        [Description("Mã chứng khoán, vd VNM.")] string symbol,
        IMediator mediator, CancellationToken ct,
        [Description("Từ ngày ISO-8601 (bỏ trống = 3 tháng trước).")] DateTime? from = null,
        [Description("Đến ngày ISO-8601 (bỏ trống = hiện tại).")] DateTime? to = null)
        => await mediator.Send(new GetStockPriceHistoryQuery
        {
            Symbol = Normalize(symbol),
            From = from ?? DateTime.UtcNow.AddMonths(-DefaultHistoryMonths),
            To = to ?? DateTime.UtcNow
        }, ct);

    [McpServerTool(Name = "get_technical_analysis", ReadOnly = true)]
    [Description("Phân tích kỹ thuật của một mã: MA, RSI, MACD, Bollinger Bands và tín hiệu tổng hợp.")]
    public static async Task<TechnicalAnalysisResult> GetTechnicalAnalysis(
        [Description("Mã chứng khoán, vd MWG.")] string symbol,
        IMediator mediator, CancellationToken ct)
        => await mediator.Send(new GetTechnicalAnalysisQuery { Symbol = Normalize(symbol) }, ct);

    [McpServerTool(Name = "search_stocks", ReadOnly = true)]
    [Description("Tìm mã chứng khoán theo từ khóa (mã hoặc tên công ty).")]
    public static async Task<List<StockSearchDto>> SearchStocks(
        [Description("Từ khóa tìm kiếm: mã hoặc tên công ty.")] string keyword,
        IMediator mediator, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("Từ khóa tìm kiếm là bắt buộc.", nameof(keyword));

        return await mediator.Send(new SearchStocksQuery { Keyword = keyword }, ct);
    }

    [McpServerTool(Name = "get_market_overview", ReadOnly = true)]
    [Description("Tổng quan thị trường: các chỉ số chính (VNINDEX, VN30, HNX...) kèm thay đổi và thanh khoản.")]
    public static async Task<List<MarketOverviewDto>> GetMarketOverview(
        IMediator mediator, CancellationToken ct)
        => await mediator.Send(new GetMarketOverviewQuery(), ct);

    [McpServerTool(Name = "get_top_fluctuation", ReadOnly = true)]
    [Description("Top cổ phiếu biến động mạnh nhất theo sàn.")]
    public static async Task<List<TopFluctuationDto>> GetTopFluctuation(
        IMediator mediator, CancellationToken ct,
        [Description("Mã sàn: \"10\" = HOSE (mặc định), \"02\" = HNX, \"03\" = UPCOM.")] string? floor = null)
        => await mediator.Send(new GetTopFluctuationQuery { Floor = floor ?? "10" }, ct);

    [McpServerTool(Name = "get_batch_prices", ReadOnly = true)]
    [Description("Giá hiện tại của nhiều mã cùng lúc — dùng thay vì gọi get_stock_price nhiều lần.")]
    public static async Task<List<BatchPriceDto>> GetBatchPrices(
        [Description("Danh sách mã chứng khoán, vd [\"FPT\",\"HPG\"].")] List<string> symbols,
        IMediator mediator, CancellationToken ct)
        => await mediator.Send(new GetBatchPricesQuery
        {
            Symbols = symbols.Where(s => !string.IsNullOrWhiteSpace(s)).Select(Normalize).ToList()
        }, ct);
}
