using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using InvestmentApp.Application.MarketData.Queries.GetMarketOverview;
using InvestmentApp.Application.MarketData.Queries.GetStockPrice;
using InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalSeries;
using InvestmentApp.Application.MarketData.Queries.SearchStocks;
using MediatR;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// Dữ liệu thị trường công khai và phân tích kỹ thuật cho agent. Mỗi tool bọc đúng query mà
/// <c>MarketDataController</c> đang dùng, không thêm nghiệp vụ. Dữ liệu không gắn với người dùng
/// nên không cần <c>UserId</c>; vẫn đi sau ApiKey scheme của <c>/mcp</c>.
/// </summary>
[McpServerToolType]
public static class MarketDataTools
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int DefaultSeriesLength = 30;
    private const int MinSeriesLength = 5;
    private const int MaxSeriesLength = 120;

    [McpServerTool(Name = "get_stock_price", ReadOnly = true)]
    [Description("Giá hiện tại của một mã chứng khoán (giá, thay đổi, khối lượng, trần/sàn/tham chiếu).")]
    public static async Task<StockPriceDto> GetStockPrice(
        [Description("Mã chứng khoán, ví dụ FPT.")] string symbol,
        IMediator mediator, CancellationToken ct)
        => await mediator.Send(new GetStockPriceQuery { Symbol = NormalizeSymbol(symbol) }, ct);

    [McpServerTool(Name = "get_price_history", ReadOnly = true)]
    [Description("Lịch sử giá theo ngày (mở, cao, thấp, đóng, khối lượng) của một mã. Bỏ trống from/to thì lấy 3 tháng gần nhất. Mỗi dòng có adjusted: true là giá đã điều chỉnh theo sự kiện quyền (cổ tức, thưởng, chia tách), false là chưa.")]
    public static async Task<List<StockPriceHistoryDto>> GetPriceHistory(
        [Description("Mã chứng khoán, ví dụ FPT.")] string symbol,
        IMediator mediator, CancellationToken ct,
        // from/to phải nằm SAU ct và có `= null`: nullable một mình không đủ để schema đánh dấu optional.
        [Description("Từ ngày, dạng YYYY-MM-DD (tùy chọn, mặc định 3 tháng trước).")] string? from = null,
        [Description("Đến ngày, dạng YYYY-MM-DD (tùy chọn, mặc định hôm nay).")] string? to = null)
    {
        var toDate = to is null ? DateTime.UtcNow : ParseDate(to, nameof(to));
        var fromDate = from is null ? toDate.AddMonths(-3) : ParseDate(from, nameof(from));
        return await mediator.Send(new GetStockPriceHistoryQuery
        {
            Symbol = NormalizeSymbol(symbol), From = fromDate, To = toDate
        }, ct);
    }

    [McpServerTool(Name = "get_market_overview", ReadOnly = true)]
    [Description("Tổng quan thị trường: các chỉ số chính (VN-INDEX, HNX-INDEX, UPCOM-INDEX) với điểm số, thay đổi và thanh khoản.")]
    public static async Task<List<MarketOverviewDto>> GetMarketOverview(
        IMediator mediator, CancellationToken ct)
        => await mediator.Send(new GetMarketOverviewQuery(), ct);

    [McpServerTool(Name = "search_stocks", ReadOnly = true)]
    [Description("Tìm mã chứng khoán theo mã hoặc tên công ty.")]
    public static async Task<List<StockSearchDto>> SearchStocks(
        [Description("Từ khoá: một phần mã hoặc tên công ty, ví dụ \"fpt\" hoặc \"sữa\".")] string keyword,
        IMediator mediator, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException(
                "Cần nhập từ khoá tìm kiếm (một phần mã hoặc tên công ty), ví dụ \"fpt\".", nameof(keyword));
        return await mediator.Send(new SearchStocksQuery { Keyword = keyword.Trim() }, ct);
    }

    [McpServerTool(Name = "get_technical_analysis", ReadOnly = true)]
    [Description("Phân tích kỹ thuật một mã: EMA, RSI, MACD, Bollinger, ADX, khối lượng, hỗ trợ/kháng cự, Fibonacci, tín hiệu tổng hợp và gợi ý giao dịch. Có EMA200 khi nguồn đủ 200 phiên. Trường adjusted cho biết giá đã điều chỉnh theo sự kiện quyền (true) hay đang dùng nguồn dự phòng chưa điều chỉnh (false).")]
    public static async Task<JsonNode> GetTechnicalAnalysis(
        [Description("Mã chứng khoán, ví dụ FPT.")] string symbol,
        IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTechnicalAnalysisQuery { Symbol = NormalizeSymbol(symbol) }, ct);
        // Trường null (ví dụ ema200 khi chưa đủ 200 phiên) bị bỏ do tuỳ chọn JSON của MCP, không cần lọc riêng.
        return JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions)!.AsObject();
    }

    [McpServerTool(Name = "get_technical_series", ReadOnly = true)]
    [Description("Chuỗi chỉ báo kỹ thuật theo từng phiên (ngày, mở/cao/thấp/đóng/khối lượng và 19 chuỗi: EMA20/50/200, Bollinger, RSI, Stochastic, MACD, ADX, +DI/-DI, ATR, OBV, MFI, SMA20 khối lượng). Mọi mảng cùng độ dài; phiên chưa đủ dữ liệu khởi động là null. Chỉ trả lastN phiên cuối.")]
    public static async Task<JsonNode> GetTechnicalSeries(
        [Description("Mã chứng khoán, ví dụ FPT.")] string symbol,
        IMediator mediator, CancellationToken ct,
        [Description("Số phiên gần nhất cần lấy (tùy chọn, mặc định 30, nằm trong 5–120).")] int? lastN = null)
    {
        var count = Math.Clamp(lastN ?? DefaultSeriesLength, MinSeriesLength, MaxSeriesLength);
        var result = await mediator.Send(new GetTechnicalSeriesQuery { Symbol = NormalizeSymbol(symbol) }, ct);
        var node = JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions)!.AsObject();
        foreach (var key in node.Where(kv => kv.Value is JsonArray).Select(kv => kv.Key).ToList())
        {
            var source = node[key]!.AsArray();
            var skip = Math.Max(0, source.Count - count);
            var trimmed = new JsonArray();
            // Nhân bản từng phần tử: một JsonNode chỉ có một cha nên không thể gắn trực tiếp sang mảng mới.
            foreach (var item in source.Skip(skip)) trimmed.Add(item?.DeepClone());
            node[key] = trimmed;
        }
        return node;
    }

    private static string NormalizeSymbol(string symbol) => symbol.ToUpper().Trim();

    // Lỗi phải nói rõ phải gửi gì cho đúng, không chỉ nói là sai.
    private static DateTime ParseDate(string value, string paramName)
        => DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : throw new ArgumentException(
                $"Ngày \"{value}\" không đúng định dạng. Cần dạng YYYY-MM-DD, ví dụ 2026-01-31.", paramName);
}
