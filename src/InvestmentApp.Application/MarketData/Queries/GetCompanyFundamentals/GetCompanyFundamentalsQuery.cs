using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace InvestmentApp.Application.MarketData.Queries.GetCompanyFundamentals;

public class GetCompanyFundamentalsQuery : IRequest<CompanyFundamentalsDto>
{
    public string Symbol { get; set; } = null!;
}

/// <summary>
/// Nguyên liệu để viết hồ sơ công ty. Phơi trực tiếp các POCO của
/// <see cref="ComprehensiveStockData"/> — chúng đã nằm ở tầng Application, map lại chỉ thêm chỗ lệch.
/// </summary>
public class CompanyFundamentalsDto
{
    public string Symbol { get; set; } = null!;
    public CompanyOverview? Company { get; set; }
    public FinanceIndicators? Indicators { get; set; }
    public List<IncomeStatementItem> IncomeStatements { get; set; } = new();
    public List<PeerStock> Peers { get; set; } = new();
    public List<DividendEvent> DividendEvents { get; set; } = new();
    public CompanyPlan? BusinessPlan { get; set; }
    public List<AnalystReport> AnalystReports { get; set; } = new();
    public ForeignTradingSummary? ForeignTrading { get; set; }

    /// <summary>
    /// Các so sánh kỳ tính sẵn ở server (cùng quý năm trước, TTM, cả năm khi đủ 4 quý, biên lợi nhuận, vị trí trong vùng giá 52 tuần).
    /// Giao diện và MCP đọc cùng khối này. Null khi không có quý nào và không có vùng giá 52 tuần (khi đó "trend" nằm trong UnavailableSections).
    /// </summary>
    public FundamentalsTrendDto? Trend { get; set; }

    /// <summary>
    /// Tên các phần provider không lấy được. Rỗng KHÔNG có nghĩa là bằng không — người đọc (và
    /// agent) phải phân biệt được "không có dữ liệu" với "giá trị bằng 0".
    /// </summary>
    public List<string> UnavailableSections { get; set; } = new();
}

public class GetCompanyFundamentalsQueryHandler
    : IRequestHandler<GetCompanyFundamentalsQuery, CompanyFundamentalsDto>
{
    // Một lần gọi provider là ~9 request HTTP ra 24hmoney. TTL ngắn hơn nhiều so với 6 giờ của nhãn
    // ngành ở RiskCalculationService: nhãn ngành gần như không đổi, còn P/E và vốn hóa đổi trong ngày.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private const string CacheKeyPrefix = "fundamentals:";

    private readonly IComprehensiveStockDataProvider _provider;
    private readonly IMemoryCache _cache;
    private readonly IMarketDataProvider _prices;

    public GetCompanyFundamentalsQueryHandler(IComprehensiveStockDataProvider provider, IMemoryCache cache, IMarketDataProvider prices)
    {
        _provider = provider;
        _cache = cache;
        _prices = prices;
    }

    public async Task<CompanyFundamentalsDto> Handle(
        GetCompanyFundamentalsQuery request, CancellationToken cancellationToken)
    {
        var symbol = (request.Symbol ?? string.Empty).Trim().ToUpperInvariant();
        if (_cache.TryGetValue<CompanyFundamentalsDto>(CacheKeyPrefix + symbol, out var cached) && cached != null)
            return cached;

        var data = await _provider.GetComprehensiveDataAsync(symbol, cancellationToken);

        if (data == null)
            throw new KeyNotFoundException($"Không tìm thấy mã {symbol}");

        // Provider KHÔNG trả null cho mã sai — nó trả về đủ hai object mà mọi field đều null. Vì vậy
        // "có dữ liệu" phải chấm theo NỘI DUNG, không theo null-ness; chấm theo null thì cửa 404
        // dưới đây là code chết và mã sai trả 200 với hồ sơ trống.
        var company = HasAnyValue(data.Company) ? data.Company : null;
        var indicators = HasAnyValue(data.Indicators) ? data.Indicators : null;

        // Cả hai phần lõi trống nghĩa là provider chưa cấu hình hoặc mã sai. Trả 200 với mọi phần
        // rỗng thì không phân biệt được với "doanh nghiệp không có số liệu", nên báo 404.
        if (company == null && indicators == null)
            throw new KeyNotFoundException($"Provider không lấy được dữ liệu doanh nghiệp cho {symbol}");

        // Provider có trả về phần tử rỗng hẳn — HPG cho ra 10 sự kiện cổ tức mà mọi field đều null.
        // Đếm theo Count thì section coi như có dữ liệu và UI render 10 dòng gạch ngang: trông như
        // dữ liệu nhưng không mang gì. Bỏ phần tử rỗng TRƯỚC khi chấm phần nào lấy được.
        var dto = new CompanyFundamentalsDto
        {
            Symbol = symbol,
            Company = company,
            Indicators = indicators,
            // Một vị từ duy nhất cho mọi phần. Viết tay điều kiện "có nội dung" cho từng loại là mở
            // sẵn chỗ để một loại bị bỏ sót một field, rồi lệch với chính cách chấm Company/Indicators.
            IncomeStatements = data.IncomeStatements.Where(HasAnyValue).ToList(),
            Peers = data.Peers.Where(HasAnyValue).ToList(),
            DividendEvents = data.DividendEvents.Where(HasAnyValue).ToList(),
            BusinessPlan = HasAnyValue(data.BusinessPlan) ? data.BusinessPlan : null,
            AnalystReports = data.AnalystReports.Where(HasAnyValue).ToList(),
            ForeignTrading = HasAnyValue(data.ForeignTrading) ? data.ForeignTrading : null
        };

        // Giá hiện tại chỉ cần cho vị trí trong vùng 52 tuần: không có đáy/đỉnh thì khỏi gọi nguồn giá. Nguồn giá lỗi không làm hỏng hồ sơ.
        decimal? priceThousands = null;
        if (indicators?.Min52W != null && indicators.Max52W != null)
            priceThousands = await GetPriceThousandsAsync(symbol, cancellationToken);
        dto.Trend = FundamentalsTrendCalculator.Compute(dto.IncomeStatements, dto.Indicators, priceThousands);

        if (dto.Company == null) dto.UnavailableSections.Add("company");
        if (dto.Indicators == null) dto.UnavailableSections.Add("indicators");
        if (dto.IncomeStatements.Count == 0) dto.UnavailableSections.Add("incomeStatements");
        if (dto.Peers.Count == 0) dto.UnavailableSections.Add("peers");
        if (dto.DividendEvents.Count == 0) dto.UnavailableSections.Add("dividendEvents");
        if (dto.BusinessPlan == null) dto.UnavailableSections.Add("businessPlan");
        if (dto.AnalystReports.Count == 0) dto.UnavailableSections.Add("analystReports");
        if (dto.ForeignTrading == null) dto.UnavailableSections.Add("foreignTrading");
        if (dto.Trend == null) dto.UnavailableSections.Add("trend");

        // Chỉ cache ca lấy được. Cache cả ca 404/lỗi là đóng băng một lỗi mạng nhất thời thành
        // "mã không có dữ liệu" suốt TTL — cùng lý do đã ghi ở cache nhãn ngành.
        _cache.Set(CacheKeyPrefix + symbol, dto, CacheTtl);
        return dto;
    }

    /// <summary>
    /// Giá hiện tại theo NGHÌN đồng (cùng đơn vị với đáy/đỉnh 52 tuần của nguồn: FPT 43,9–68,2). Nguồn giá trả VND nên chia 1000.
    /// Lỗi hay giá không dương thì trả null: vị trí 52 tuần để trống thay vì làm hỏng cả hồ sơ.
    /// </summary>
    private async Task<decimal?> GetPriceThousandsAsync(string symbol, CancellationToken ct)
    {
        try
        {
            var price = await _prices.GetCurrentPriceAsync(symbol, ct);
            return price is { Close: > 0 } ? price.Close / 1000m : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Object có mang thông tin nào không. Dùng reflection thay vì liệt kê tay 21 property của
    /// <c>FinanceIndicators</c>: danh sách gõ tay sẽ mục ruỗng ngay lần ai đó thêm chỉ số mới, và
    /// một property bị bỏ sót lại đưa vỏ rỗng đi tiếp đúng như lỗi này.
    /// </summary>
    private static bool HasAnyValue(object? obj)
    {
        if (obj == null) return false;
        if (obj is string str) return !string.IsNullOrWhiteSpace(str);

        var type = obj.GetType();
        // Số/bool/DateTime: coi giá trị MẶC ĐỊNH là "không có thông tin". Với property không nullable
        // (`Shareholder.Percentage`, `Shareholder.Quantity`) thì 0 và "không có" là cùng một
        // bit — không có cách nào phân biệt, nên phải chọn một phía. Chọn phía này vì hậu quả lệch
        // nhẹ hơn: một số 0 thật bị báo "không lấy được" chỉ làm mất một dòng, còn một vỏ rỗng được
        // coi là dữ liệu thì bịa ra cả một khối cổ đông. Phải đặt trước nhánh reflection: `decimal`
        // không có property public nào nên soi property sẽ kết luận sai là rỗng.
        if (type.IsValueType) return !Equals(obj, Activator.CreateInstance(type));

        // Danh sách toàn phần tử rỗng vẫn là rỗng: một list 5 cổ đông mà mọi field đều null làm cả
        // khối công ty được coi là "có dữ liệu" rồi hiện 5 dòng gạch ngang.
        if (obj is System.Collections.IEnumerable seq)
        {
            foreach (var item in seq) if (HasAnyValue(item)) return true;
            return false;
        }

        foreach (var prop in type.GetProperties())
            if (HasAnyValue(prop.GetValue(obj))) return true;
        return false;
    }
}
