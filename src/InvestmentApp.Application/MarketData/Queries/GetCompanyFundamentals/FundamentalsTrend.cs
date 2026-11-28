using System.Text.RegularExpressions;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.MarketData.Queries.GetCompanyFundamentals;

/// <summary>
/// Các so sánh kỳ của số liệu doanh nghiệp, tính MỘT LẦN ở server cho cả giao diện và MCP (agent đọc cùng bức tranh với người dùng).
/// Số liệu doanh nghiệp theo QUÝ: không có số liệu tháng. Giá trị không có thì null, không bao giờ là 0.
/// Đơn vị tiền: tỷ đồng. Phần trăm làm tròn 1 chữ số (chiều tăng/giảm dùng số chính xác, ngưỡng đi ngang 1%).
/// </summary>
public class FundamentalsTrendDto
{
    public const string FinancialsNote = "Doanh nghiệp báo cáo theo quý; không có số liệu tháng";

    /// <summary>Từng quý, MỚI NHẤT TRƯỚC, mỗi quý so với cùng quý năm trước (null khi quý đó không có trong dữ liệu).</summary>
    public List<QuarterTrend> Quarters { get; set; } = new();

    /// <summary>4 quý gần nhất liên tiếp so với 4 quý liền trước; null khi chưa có quý nào.</summary>
    public TtmTrend? Ttm { get; set; }

    /// <summary>Từng năm có mặt trong dữ liệu, mới nhất trước. Số cả năm chỉ có khi đủ 4 quý.</summary>
    public List<AnnualTrend> Annual { get; set; } = new();

    public Range52WDto? Range52W { get; set; }

    public string Note { get; set; } = FinancialsNote;
}

public class QuarterTrend
{
    /// <summary>Dạng "Q2/2026".</summary>
    public string Period { get; set; } = null!;
    public int Year { get; set; }
    public int Quarter { get; set; }

    public decimal? Revenue { get; set; }
    public decimal? RevenueSameQuarterLastYear { get; set; }
    public decimal? RevenueYoyPct { get; set; }
    /// <summary><c>up</c> | <c>down</c> | <c>flat</c> (|Δ| dưới 1%); null khi không có kỳ so sánh.</summary>
    public string? RevenueDirection { get; set; }
    public string? RevenueNote { get; set; }

    public decimal? NetProfit { get; set; }
    public decimal? NetProfitSameQuarterLastYear { get; set; }
    /// <summary>Null khi một trong hai kỳ là lỗ hoặc bằng 0 (phần trăm trên cơ sở âm/bằng không là vô nghĩa); khi đó đọc <c>NetProfitDirection</c> và <c>NetProfitNote</c>.</summary>
    public decimal? NetProfitYoyPct { get; set; }
    public string? NetProfitDirection { get; set; }
    /// <summary>"từ lỗ sang lãi", "từ lãi sang lỗ", "lỗ thu hẹp", "lỗ tăng", "kỳ so sánh bằng 0".</summary>
    public string? NetProfitNote { get; set; }

    /// <summary>Lợi nhuận ròng / doanh thu, %. Null khi doanh thu bằng 0, âm hoặc thiếu.</summary>
    public decimal? NetMargin { get; set; }
    public decimal? NetMarginSameQuarterLastYear { get; set; }
    /// <summary>Chênh lệch biên so với cùng quý năm trước, điểm phần trăm.</summary>
    public decimal? NetMarginChangePp { get; set; }
}

public class TtmTrend
{
    /// <summary>Quý mới nhất của cửa sổ 4 quý, ví dụ "Q2/2026".</summary>
    public string Period { get; set; } = null!;

    public decimal? Revenue { get; set; }
    public decimal? PrevRevenue { get; set; }
    public decimal? RevenueYoyPct { get; set; }
    public string? RevenueDirection { get; set; }
    public string? RevenueNote { get; set; }

    public decimal? NetProfit { get; set; }
    public decimal? PrevNetProfit { get; set; }
    public decimal? NetProfitYoyPct { get; set; }
    public string? NetProfitDirection { get; set; }
    public string? NetProfitNote { get; set; }

    public decimal? NetMargin { get; set; }
    public decimal? PrevNetMargin { get; set; }
}

public class AnnualTrend
{
    public int Year { get; set; }
    /// <summary>Số quý của năm có mặt trong dữ liệu (0–4).</summary>
    public int QuartersPresent { get; set; }
    public bool Complete4Quarters { get; set; }

    public decimal? Revenue { get; set; }
    public decimal? PrevYearRevenue { get; set; }
    public decimal? RevenueYoyPct { get; set; }
    public string? RevenueDirection { get; set; }
    public string? RevenueNote { get; set; }

    public decimal? NetProfit { get; set; }
    public decimal? PrevYearNetProfit { get; set; }
    public decimal? NetProfitYoyPct { get; set; }
    public string? NetProfitDirection { get; set; }
    public string? NetProfitNote { get; set; }
}

/// <summary>Vị trí giá hiện tại trong vùng đáy–đỉnh 52 tuần. Giá và đáy/đỉnh cùng đơn vị (nghìn đồng, như nguồn trả về).</summary>
public class Range52WDto
{
    public const string PriceUnit = "nghìn đồng";

    public decimal? Low { get; set; }
    public decimal? High { get; set; }
    /// <summary>Giá hiện tại tính theo nghìn đồng; null khi chưa lấy được.</summary>
    public decimal? Price { get; set; }
    /// <summary>0 = đúng đáy, 100 = đúng đỉnh; kẹp trong 0–100. Null khi thiếu giá hoặc vùng không hợp lệ.</summary>
    public decimal? PositionPct { get; set; }
    /// <summary>Giá nằm ngoài vùng đáy–đỉnh đã công bố (số 52 tuần của nguồn có thể cũ).</summary>
    public bool OutsideRange { get; set; }
    public string Unit { get; set; } = PriceUnit;
}

/// <summary>Tính <see cref="FundamentalsTrendDto"/> từ các quý của báo cáo kết quả kinh doanh và chỉ số 52 tuần. Hàm thuần, không gọi nguồn.</summary>
public static class FundamentalsTrendCalculator
{
    private static readonly Regex QuarterPeriod = new(@"^Q([1-4])/(\d{4})\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const decimal FlatThresholdPct = 1m;

    private readonly record struct Qtr(int Year, int Quarter, decimal? Revenue, decimal? NetProfit)
    {
        /// <summary>Số thứ tự quý tuyệt đối để so sánh và trừ quý (4 quý trước = trừ 4).</summary>
        public int Index => Year * 4 + (Quarter - 1);
        public string Label => $"Q{Quarter}/{Year}";
    }

    public static FundamentalsTrendDto? Compute(IReadOnlyList<IncomeStatementItem> items, FinanceIndicators? indicators, decimal? priceThousands)
    {
        var quarters = Parse(items);
        var range = BuildRange(indicators, priceThousands);
        if (quarters.Count == 0 && range == null) return null;

        var byIndex = quarters.ToDictionary(q => q.Index);   // đã loại trùng ở Parse
        var dto = new FundamentalsTrendDto { Range52W = range };

        foreach (var q in quarters.OrderByDescending(q => q.Index))
        {
            byIndex.TryGetValue(q.Index - 4, out var prev);
            var hasPrev = byIndex.ContainsKey(q.Index - 4);
            var rev = Compare(q.Revenue, hasPrev ? prev.Revenue : null);
            var profit = Compare(q.NetProfit, hasPrev ? prev.NetProfit : null);
            var margin = Margin(q.NetProfit, q.Revenue);
            var prevMargin = hasPrev ? Margin(prev.NetProfit, prev.Revenue) : null;
            dto.Quarters.Add(new QuarterTrend
            {
                Period = q.Label, Year = q.Year, Quarter = q.Quarter,
                Revenue = q.Revenue, RevenueSameQuarterLastYear = hasPrev ? prev.Revenue : null,
                RevenueYoyPct = rev.Pct, RevenueDirection = rev.Direction, RevenueNote = rev.Note,
                NetProfit = q.NetProfit, NetProfitSameQuarterLastYear = hasPrev ? prev.NetProfit : null,
                NetProfitYoyPct = profit.Pct, NetProfitDirection = profit.Direction, NetProfitNote = profit.Note,
                NetMargin = margin, NetMarginSameQuarterLastYear = prevMargin,
                NetMarginChangePp = margin is { } m && prevMargin is { } p ? Math.Round(m - p, 2) : null
            });
        }

        if (quarters.Count > 0) dto.Ttm = BuildTtm(quarters, byIndex);
        dto.Annual = BuildAnnual(quarters);
        return dto;
    }

    private static List<Qtr> Parse(IReadOnlyList<IncomeStatementItem> items)
    {
        var seen = new HashSet<int>();
        var result = new List<Qtr>();
        foreach (var item in items)
        {
            var m = item.Period == null ? null : QuarterPeriod.Match(item.Period.Trim());
            if (m is not { Success: true }) continue;   // số cả năm của nguồn ("2025") và chuỗi lạ không phải quý: bỏ qua, không đoán
            var q = new Qtr(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value), item.Revenue, item.NetProfit);
            if (seen.Add(q.Index)) result.Add(q);   // trùng quý: giữ cái đầu tiên
        }
        return result;
    }

    private static TtmTrend BuildTtm(List<Qtr> quarters, Dictionary<int, Qtr> byIndex)
    {
        var latest = quarters.Max(q => q.Index);
        var latestQ = byIndex[latest];

        // Tổng 4 quý liên tiếp [end-3 .. end]; thiếu một quý hoặc một giá trị thì cả tổng là null (không cộng phần còn lại).
        decimal? Sum(int end, Func<Qtr, decimal?> pick)
        {
            decimal total = 0;
            for (var i = end - 3; i <= end; i++)
            {
                if (!byIndex.TryGetValue(i, out var q) || pick(q) is not { } v) return null;
                total += v;
            }
            return total;
        }

        var rev = Sum(latest, q => q.Revenue);
        var prevRev = Sum(latest - 4, q => q.Revenue);
        var profit = Sum(latest, q => q.NetProfit);
        var prevProfit = Sum(latest - 4, q => q.NetProfit);
        var revCmp = Compare(rev, prevRev);
        var profitCmp = Compare(profit, prevProfit);
        return new TtmTrend
        {
            Period = latestQ.Label,
            Revenue = rev, PrevRevenue = prevRev, RevenueYoyPct = revCmp.Pct, RevenueDirection = revCmp.Direction, RevenueNote = revCmp.Note,
            NetProfit = profit, PrevNetProfit = prevProfit, NetProfitYoyPct = profitCmp.Pct, NetProfitDirection = profitCmp.Direction, NetProfitNote = profitCmp.Note,
            NetMargin = Margin(profit, rev), PrevNetMargin = Margin(prevProfit, prevRev)
        };
    }

    private static List<AnnualTrend> BuildAnnual(List<Qtr> quarters)
    {
        var years = quarters.GroupBy(q => q.Year).ToDictionary(g => g.Key, g => g.ToList());

        // Số cả năm chỉ khi đủ 4 quý (và mỗi quý có giá trị của chỉ tiêu đó).
        decimal? Total(int year, Func<Qtr, decimal?> pick)
        {
            if (!years.TryGetValue(year, out var list) || list.Count != 4) return null;
            decimal total = 0;
            foreach (var q in list)
            {
                if (pick(q) is not { } v) return null;
                total += v;
            }
            return total;
        }

        return years.Keys.OrderByDescending(y => y).Select(year =>
        {
            var rev = Total(year, q => q.Revenue);
            var prevRev = Total(year - 1, q => q.Revenue);
            var profit = Total(year, q => q.NetProfit);
            var prevProfit = Total(year - 1, q => q.NetProfit);
            var revCmp = Compare(rev, prevRev);
            var profitCmp = Compare(profit, prevProfit);
            return new AnnualTrend
            {
                Year = year, QuartersPresent = years[year].Count, Complete4Quarters = years[year].Count == 4,
                Revenue = rev, PrevYearRevenue = prevRev, RevenueYoyPct = revCmp.Pct, RevenueDirection = revCmp.Direction, RevenueNote = revCmp.Note,
                NetProfit = profit, PrevYearNetProfit = prevProfit, NetProfitYoyPct = profitCmp.Pct, NetProfitDirection = profitCmp.Direction, NetProfitNote = profitCmp.Note
            };
        }).ToList();
    }

    private static decimal? Margin(decimal? profit, decimal? revenue)
        => profit is { } p && revenue is > 0 ? Math.Round(p / revenue.Value * 100m, 2) : null;

    private readonly record struct Comparison(decimal? Pct, string? Direction, string? Note);

    /// <summary>
    /// So giá trị hiện tại với kỳ so sánh. Thiếu một trong hai: không có gì (null hết). Cơ sở dương và giá trị dương: phần trăm thật.
    /// Đổi dấu hoặc cơ sở bằng 0 hoặc lỗ → lỗ: không có phần trăm (vô nghĩa), chỉ có chiều và ghi chú.
    /// </summary>
    private static Comparison Compare(decimal? current, decimal? previous)
    {
        if (current is not { } cur || previous is not { } prev) return new(null, null, null);

        if (prev > 0 && cur >= 0)
        {
            var exact = (cur - prev) / prev * 100m;
            var direction = Math.Abs(exact) < FlatThresholdPct ? "flat" : exact > 0 ? "up" : "down";
            return new(Math.Round(exact, 1), direction, null);
        }
        if (prev == 0)
            return new(null, cur > 0 ? "up" : cur < 0 ? "down" : "flat", cur == 0 ? null : "kỳ so sánh bằng 0");
        if (prev < 0 && cur >= 0) return new(null, "up", "từ lỗ sang lãi");
        if (prev > 0 && cur < 0) return new(null, "down", "từ lãi sang lỗ");
        // lỗ → lỗ
        return cur > prev ? new(null, "up", "lỗ thu hẹp") : cur < prev ? new(null, "down", "lỗ tăng") : new(null, "flat", null);
    }

    private static Range52WDto? BuildRange(FinanceIndicators? ind, decimal? priceThousands)
    {
        if (ind == null || (ind.Min52W == null && ind.Max52W == null)) return null;

        var dto = new Range52WDto { Low = ind.Min52W, High = ind.Max52W, Price = priceThousands };
        if (ind.Min52W is { } low && ind.Max52W is { } high && high > low && priceThousands is { } price)
        {
            dto.OutsideRange = price < low || price > high;
            dto.PositionPct = Math.Round(Math.Clamp((price - low) / (high - low) * 100m, 0m, 100m), 1);
        }
        return dto;
    }
}
