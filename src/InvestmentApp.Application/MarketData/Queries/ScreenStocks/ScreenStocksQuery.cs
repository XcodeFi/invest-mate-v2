using System.Text.RegularExpressions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Screener;
using MediatR;

namespace InvestmentApp.Application.MarketData.Queries.ScreenStocks;

/// <summary>
/// Sàng lọc cổ phiếu toàn thị trường theo tiêu chí. Mọi tham số đều tuỳ chọn (null = mặc định trong ADR-0020) và được kẹp về khoảng hợp lý;
/// ngưỡng chỉ được áp trong bộ nhớ lên kho dữ liệu đã đệm, không bao giờ quyết định việc gọi nguồn.
/// </summary>
public class ScreenStocksQuery : IRequest<ScreenerResult>
{
    /// <summary>Danh sách sàn cách nhau bởi dấu phẩy, chỉ nhận HOSE, HNX, UPCOM. Mặc định HOSE,HNX.</summary>
    public string? Floors { get; set; }
    public bool? IncludeUpcom { get; set; }
    public decimal? MinAdtv { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MinCap { get; set; }
    public decimal? MinRoe { get; set; }
    public decimal? MaxPe { get; set; }
    public int? MinRs3m { get; set; }
    public bool? SkipQualityLayer { get; set; }
    public bool? IncludeExcluded { get; set; }
    public int? ExcludedSkip { get; set; }
    public int? ExcludedTake { get; set; }
    public string? LookupSymbol { get; set; }
}

public class ScreenStocksQueryHandler : IRequestHandler<ScreenStocksQuery, ScreenerResult>
{
    private static readonly Regex ValidSymbol = new(@"^[A-Z0-9]{1,10}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IMarketScreenerService _service;

    public ScreenStocksQueryHandler(IMarketScreenerService service) => _service = service;

    public async Task<ScreenerResult> Handle(ScreenStocksQuery request, CancellationToken ct)
    {
        var defaults = new ScreenerCriteria();
        string? lookup = null;
        if (!string.IsNullOrWhiteSpace(request.LookupSymbol))
        {
            lookup = request.LookupSymbol.Trim().ToUpperInvariant();
            if (!ValidSymbol.IsMatch(lookup))
                throw new ArgumentException("Mã tra cứu không hợp lệ: chỉ gồm chữ và số, tối đa 10 ký tự, ví dụ FPT.", nameof(request));
        }

        // Kiểm tham số TRƯỚC khi chạm tới nguồn: tham số sai không được gây ra lần làm mới nào.
        var criteria = new ScreenerCriteria
        {
            Floors = ScreenerCriteria.ParseFloors(request.Floors),
            IncludeUpcom = request.IncludeUpcom ?? defaults.IncludeUpcom,
            MinAdtvBn = request.MinAdtv ?? defaults.MinAdtvBn,
            MinPriceVnd = request.MinPrice ?? defaults.MinPriceVnd,
            MinMarketCapBn = request.MinCap ?? defaults.MinMarketCapBn,
            MinRoe = request.MinRoe ?? defaults.MinRoe,
            MaxPe = request.MaxPe,
            MinRs3m = request.MinRs3m ?? defaults.MinRs3m,
            SkipQualityLayer = request.SkipQualityLayer ?? false,
            IncludeExcluded = request.IncludeExcluded ?? false,
            ExcludedSkip = request.ExcludedSkip ?? 0,
            ExcludedTake = request.ExcludedTake ?? ScreenerCriteria.DefaultExcludedTake,
            LookupSymbol = lookup
        }.Clamped();

        var snapshot = await _service.GetSnapshotAsync(ct);
        return ScreenerRules.Evaluate(snapshot, criteria);
    }
}
