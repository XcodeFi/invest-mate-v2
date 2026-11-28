using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using InvestmentApp.Application.MarketData.Queries.GetMarketOverview;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetSignalSetups;
using InvestmentApp.Application.MarketData.Queries.GetStockPrice;
using InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalSeries;
using InvestmentApp.Application.MarketData.Queries.ScreenStocks;
using InvestmentApp.Application.MarketData.Queries.SearchStocks;
using InvestmentApp.Application.MarketData.Screener;
using MediatR;
using Microsoft.AspNetCore.Http;
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
    [Description("Phân tích kỹ thuật một mã: EMA, RSI, MACD, Bollinger, ADX, khối lượng, hỗ trợ/kháng cự, Fibonacci, tín hiệu tổng hợp và gợi ý giao dịch (vào lệnh, cắt lỗ, mục tiêu, riskRewardRatio; suggestedStopLossMethod cho biết cắt lỗ (luôn cách giá vào 1–2×ATR khi có ATR) tính theo support2 = hỗ trợ thứ 2, atr2x = vào lệnh − 2×ATR14, atr1x = hỗ trợ thứ 2 sát giá vào hơn 1 ATR nên cắt lỗ bị kéo ra vào lệnh − 1×ATR14, hoặc percent5 = −5% dưới hỗ trợ khi thiếu ATR; không có gợi ý nếu cắt lỗ không hợp lệ; riskRewardRatio dưới 1 là không đủ hấp dẫn). Có EMA200 khi nguồn đủ 200 phiên. Trường adjusted cho biết giá đã điều chỉnh theo sự kiện quyền (true) hay đang dùng nguồn dự phòng chưa điều chỉnh (false). Trường patternCandidates liệt kê ứng viên mô hình giá (hiện có cup-with-handle: cốc tay cầm, tối đa một; rỗng khi không thấy hoặc giá chưa điều chỉnh): ngày đỉnh trái/đáy/vành phải, độ dài và độ sâu cốc, độ dài và độ sâu tay cầm, pivotPrice, distanceToPivotPct (dương = còn dưới pivot), volumeVsAvg50, từng điều kiện (criteria: rule, value, passed, required) và state (forming, near-pivot, broke-out-unconfirmed, broke-out-with-volume); kèm phần nếu sai: stopPrice (ngay dưới đáy tay cầm), riskPct (% mất từ pivot tới cắt lỗ), targetPrice (pivot cộng độ sâu cốc) và rewardRisk (theo R). Chỉ là ứng viên theo ngưỡng mặc định có thể chỉnh, KHÔNG có điểm số hay xác suất, không phải tín hiệu mua; chưa vượt pivot kèm khối lượng thì chưa xác nhận. Trường trapEvents liệt kê các mốc bẫy đã được xác nhận hồi tố (tối đa 12 mốc gần nhất, cũ trước; không có mốc cho 5 phiên gần nhất; rỗng khi giá chưa điều chỉnh): type failed-breakout (đóng cửa vượt đỉnh 20 phiên rồi đóng cửa trở lại dưới pivot trong 5 phiên) hoặc bear-trap (đóng cửa thủng đáy 20 phiên rồi đóng cửa trở lại trên hỗ trợ trong 5 phiên), breakoutDate (nến vượt hoặc thủng), returnDate, confirmedDate (t+5), levelPrice, phần nếu sai gồm stopPrice (cắt lỗ quy ước) và riskPct (% từ mức tới cắt lỗ), và excursionPct (% giá đi xuyên qua mức, đo được). Mốc là xác nhận sau khi đã xảy ra, không phải dự báo; KHÔNG có tần suất, tỉ lệ hay xác suất. Cấu trúc giá dài hạn: sma50/sma150/sma200 (trung bình ĐƠN GIẢN, khác EMA), week52High/week52Low (đỉnh/đáy của 250 phiên gần nhất; vắng khi chưa đủ 250 phiên), pctBelowWeek52High (giá thấp hơn đỉnh 52 tuần bao nhiêu %) và pctAboveWeek52Low (cao hơn đáy bao nhiêu %), kênh Donchian donchian20High/donchian20Low/donchian55High/donchian55Low (đỉnh/đáy 20 và 55 phiên TRƯỚC phiên hiện tại; giá phá đỉnh là phá vỡ, thủng donchian20Low hoặc exitLow10 là mức thoát theo Turtle). Trường nào chưa đủ dữ liệu thì VẮNG MẶT, không phải 0. trendTemplate liệt kê các tiêu chí Trend Template của Mark Minervini (criteria: key, rule, value = khoảng cách %, threshold, met, detail khi thiếu dữ liệu; tiêu chí rs_rating luôn không tính vì app chưa có xếp hạng RS theo chuẩn IBD): từng dòng đứng riêng, KHÔNG có kết luận tổng đạt/không đạt và không phải tín hiệu mua; SMA200 dốc lên cần khoảng 221 phiên.")]
    public static async Task<JsonNode> GetTechnicalAnalysis(
        [Description("Mã chứng khoán, ví dụ FPT.")] string symbol,
        IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTechnicalAnalysisQuery { Symbol = NormalizeSymbol(symbol) }, ct);
        // Trường null (ví dụ ema200 khi chưa đủ 200 phiên) bị bỏ do tuỳ chọn JSON của MCP, không cần lọc riêng.
        return JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions)!.AsObject();
    }

    [McpServerTool(Name = "get_signal_setups", ReadOnly = true)]
    [Description("Lịch sử setup Pullback (xu hướng tăng, giá hồi về sát EMA20, RSI 40–55) trên MỘT mã trong khoảng 3 năm giá đã điều chỉnh: số lần xuất hiện, số lệnh mô phỏng đã đóng/đang mở/bị bỏ qua (theo luật thị trường Việt Nam: vào ở giá mở phiên sau, T+2,5, biên độ, khoá sàn, phí và thuế), và tín hiệu đang kích hoạt (activePlan: vào lệnh, cắt lỗ, mục tiêu; entryIsEstimate = true là vào lệnh ước tính theo đóng cửa vì chưa tới phiên vào). Các mức này theo luật của setup (vào giá mở cửa phiên sau, cắt lỗ 2×ATR, mục tiêu 2R), KHÁC với gợi ý giao dịch của get_technical_analysis vốn dựa trên hỗ trợ/kháng cự: hai phương pháp, đừng trình bày như mâu thuẫn. CHỈ khi đủ 30 lệnh đã đóng (sampleSufficient = true) mới có winRate, evR (kèm khoảng tin cậy 95%); dưới 30 thì các trường đó VẮNG MẶT, không phải 0 — hãy nói với người dùng là chưa đủ mẫu thay vì suy ra tỉ lệ thắng từ vài lệnh. reasonUnavailable có giá trị nghĩa là chuỗi giá chưa đủ tốt (chưa điều chỉnh, dưới 250 phiên hoặc đã cũ) nên không có số liệu. Với mã thuộc VN30, mỗi setup có thể kèm baseRate (\"Tỉ lệ nền VN30\": k mã, n lệnh gộp, effectiveMonths, winRate và evR kèm khoảng 95% XẤP XỈ theo bootstrap khối tháng). Đó là tỉ lệ nền của CẢ RỔ VN30 hiện tại, KHÔNG phải bằng chứng riêng của mã đang hỏi; số liệu mô tả quá khứ (trong mẫu) và rổ dùng thành viên hiện tại nên có thiên lệch người sống sót; hãy nói rõ điều này khi trình bày, đừng dùng nó như tỉ lệ thắng của mã. baseRate vắng thì baseRateReason nêu lý do (mã ngoài VN30, hoặc rổ chưa đủ dữ liệu): đừng tự suy ra con số. Luôn kèm disclaimer và notes. Không có danh sách từng lệnh, không xếp hạng, không phải khuyến nghị.")]
    public static async Task<SignalSetupsDto> GetSignalSetups(
        [Description("Mã chứng khoán, ví dụ FPT.")] string symbol,
        IMediator mediator, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Cần nhập mã chứng khoán (symbol), ví dụ FPT.", nameof(symbol));
        return await mediator.Send(new GetSignalSetupsQuery { Symbol = NormalizeSymbol(symbol) }, ct);
    }

    [McpServerTool(Name = "screen_stocks", ReadOnly = true)]
    [Description("Sàng lọc cổ phiếu toàn thị trường theo tiêu chí (không phải khuyến nghị mua, không có điểm số hay thứ hạng; danh sách xếp theo bảng chữ cái). Ba tầng: (1) loại mã thuộc diện đặc biệt/hủy niêm yết của HOSE, thanh khoản thấp (GTGD bình quân 10 phiên dưới minAdtv tỷ), giá dưới minPrice đồng, vốn hoá dưới minCap tỷ; (2) chất lượng: lãi ròng dương và ROE từ minRoe (maxPe tuỳ chọn, mặc định TẮT; skipQualityLayer bỏ tầng này); (3) xu hướng: xếp hạng sức mạnh 3 tháng từ P(minRs3m) trong toàn bộ mã niêm yết. LƯU Ý: rs1m/rs3m/rs52w là xếp hạng phân vị 1–99, KHÔNG phải tỉ suất sinh lời. Mặc định HOSE+HNX; UPCOM chỉ khi includeUpcom (UPCOM và HNX chưa có danh sách cảnh báo chính thức nên mỗi dòng có ghi chú). Kết quả: passed[] (kèm lý do đạt, flags = cờ biến động dựa trên xếp hạng tương đối, notes), unverifiedHose[] (mã HOSE qua mọi tầng nhưng chưa kiểm được danh sách diện đặc biệt: KHÔNG coi là đạt), excludedCounts{lý do:số mã} và sectorCounts, tradeoffCount (loại vì chất lượng/P/E nhưng xu hướng và thanh khoản đạt). Trường máy đọc: status = ok | stale | unavailable, stale (dữ liệu cũ vì làm mới lỗi, tối đa 48 giờ), asOf, reason khi unavailable ('Dữ liệu sàng lọc quá cũ' nghĩa là không có kết quả, đừng tự suy ra). lookupSymbol trả lý do một mã đạt hoặc bị loại; includeExcluded thêm excluded[] (phân trang excludedSkip/excludedTake, tối đa 200). Luôn kèm disclaimer.")]
    public static async Task<ScreenerResult> ScreenStocks(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        // Mọi tham số phải nằm SAU ct và có `= null`: nullable một mình không đủ để schema đánh dấu optional.
        [Description("Các sàn, cách nhau bởi dấu phẩy, chỉ nhận HOSE, HNX, UPCOM (tuỳ chọn, mặc định HOSE,HNX).")] string? floors = null,
        [Description("Bật UPCOM (tuỳ chọn, mặc định false).")] bool? includeUpcom = null,
        [Description("GTGD bình quân 10 phiên tối thiểu, tỷ đồng (tuỳ chọn, mặc định 5).")] decimal? minAdtv = null,
        [Description("Giá tối thiểu, đồng (tuỳ chọn, mặc định 5000).")] decimal? minPrice = null,
        [Description("Vốn hoá tối thiểu, tỷ đồng (tuỳ chọn, mặc định 1000).")] decimal? minCap = null,
        [Description("ROE tối thiểu, % (tuỳ chọn, mặc định 12).")] decimal? minRoe = null,
        [Description("Trần P/E (tuỳ chọn, mặc định TẮT: không đặt thì không loại theo P/E).")] decimal? maxPe = null,
        [Description("Xếp hạng sức mạnh 3 tháng tối thiểu, 1–99 (tuỳ chọn, mặc định 50 = trung vị).")] int? minRs3m = null,
        [Description("Bỏ qua tầng chất lượng (tuỳ chọn, mặc định false).")] bool? skipQualityLayer = null,
        [Description("Trả thêm danh sách mã bị loại kèm lý do (tuỳ chọn, mặc định false).")] bool? includeExcluded = null,
        [Description("Bỏ qua bấy nhiêu mã bị loại đầu tiên (tuỳ chọn, mặc định 0).")] int? excludedSkip = null,
        [Description("Số mã bị loại tối đa mỗi trang, 1–200 (tuỳ chọn, mặc định 100).")] int? excludedTake = null,
        [Description("Tra lý do một mã có hoặc không có trong danh sách, ví dụ FPT (tuỳ chọn).")] string? lookupSymbol = null)
        => await mediator.Send(new ScreenStocksQuery
        {
            Floors = floors, IncludeUpcom = includeUpcom, MinAdtv = minAdtv, MinPrice = minPrice, MinCap = minCap, MinRoe = minRoe,
            MaxPe = maxPe, MinRs3m = minRs3m, SkipQualityLayer = skipQualityLayer, IncludeExcluded = includeExcluded,
            ExcludedSkip = excludedSkip, ExcludedTake = excludedTake, LookupSymbol = lookupSymbol,
            UserId = http.GetUserId()   // chủ khoá API, để mỗi dòng có cờ inWatchlist; không nhận từ đối số
        }, ct);

    [McpServerTool(Name = "get_technical_series", ReadOnly = true)]
    [Description("Chuỗi chỉ báo kỹ thuật theo từng phiên (ngày, mở/cao/thấp/đóng/khối lượng và 27 chuỗi: EMA20/50/200, Bollinger, RSI, Stochastic, MACD, ADX, +DI/-DI, ATR, OBV, MFI, SMA20 khối lượng, SMA50/150/200 (trung bình ĐƠN GIẢN của giá đóng cửa, khác EMA) và kênh Donchian: donchian20High/donchian20Low/donchian55High/donchian55Low = đỉnh cao nhất/đáy thấp nhất của 20 và 55 phiên TRƯỚC phiên hiện tại (không tính phiên hiện tại), exitLow10 = đáy thấp nhất 10 phiên trước). Mọi mảng cùng độ dài; phiên chưa đủ dữ liệu khởi động là null. Chỉ trả lastN phiên cuối.")]
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

    private static string NormalizeSymbol(string symbol) => symbol.ToUpperInvariant().Trim();

    // Lỗi phải nói rõ phải gửi gì cho đúng, không chỉ nói là sai.
    private static DateTime ParseDate(string value, string paramName)
        => DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : throw new ArgumentException(
                $"Ngày \"{value}\" không đúng định dạng. Cần dạng YYYY-MM-DD, ví dụ 2026-01-31.", paramName);
}
