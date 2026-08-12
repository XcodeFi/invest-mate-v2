namespace InvestmentApp.Application.Common;

/// <summary>
/// Toán cho trần khối lượng theo ngân sách biến động (ADR-0014). Thuần, không I/O.
/// Mọi độ biến động ở biên công khai là <b>phần trăm mỗi năm</b> (19,4 = 19,4%/năm); lợi suất là
/// phân số (0,01 = 1%).
/// </summary>
public static class VolatilityBudgetCalculator
{
    public const int TradingDaysPerYear = 252;

    /// <summary>Chân trời diễn giải ngưỡng sụt giảm — 21 phiên ≈ một tháng giao dịch. Xem ADR-0014.</summary>
    public const int BudgetHorizonDays = 21;

    /// <summary>Phân vị 95% một phía. Cùng hằng số <c>CalculateValueAtRiskAsync</c> đang dùng.</summary>
    public const decimal ConfidenceZ95 = 1.645m;

    /// <summary>
    /// Biên độ sàn cao nhất là ±15% (UPCoM). Vượt ngưỡng này trong một phiên không phải biến động
    /// thị trường mà là sự kiện quyền hoặc lỗi dữ liệu.
    /// </summary>
    public const decimal AbnormalReturnThreshold = 0.15m;

    /// <summary>Số quan sát tối thiểu để ước lượng có nghĩa. Trần thực tế lấy được là 65.</summary>
    public const int MinimumObservations = 40;

    /// <summary>
    /// Lợi suất kèm NGÀY. Ngày là bắt buộc, không phải trang trí: hai mã có thể lệch tập phiên
    /// (tạm ngừng, không khớp lệnh), và chính <see cref="FilterAbnormalReturns"/> cũng bỏ quan sát
    /// ở GIỮA chuỗi. Ghép theo vị trí sau đó là ghép lệch ngày trong im lặng.
    /// <para>
    /// <b>Bất biến:</b> một chuỗi có tối đa MỘT dòng cho mỗi ngày. <see cref="ToReturns"/> bảo đảm
    /// điều đó vì nó sinh đúng một lợi suất cho mỗi thanh giá. <see cref="WeightedSeries"/> cộng
    /// dồn theo ngày nên nếu ai đó nạp chuỗi có ngày trùng, đóng góp của ngày ấy sẽ bị tính hai
    /// lần — im lặng. Phá bất biến này thì phải khử trùng ngày ở đầu vào.
    /// </para>
    /// </summary>
    public readonly record struct DatedReturn(DateTime Date, decimal Value);

    public static IReadOnlyList<DatedReturn> ToReturns(IReadOnlyList<(DateTime Date, decimal Close)> bars)
    {
        if (bars.Count < 2) return Array.Empty<DatedReturn>();

        var returns = new List<DatedReturn>(bars.Count - 1);
        for (var i = 1; i < bars.Count; i++)
        {
            if (bars[i - 1].Close > 0)
                returns.Add(new DatedReturn(
                    bars[i].Date.Date,
                    (bars[i].Close - bars[i - 1].Close) / bars[i - 1].Close));
        }
        return returns;
    }

    public static (IReadOnlyList<DatedReturn> Kept, int RemovedCount) FilterAbnormalReturns(
        IReadOnlyList<DatedReturn> returns)
    {
        var kept = returns.Where(r => Math.Abs(r.Value) <= AbnormalReturnThreshold).ToList();
        return (kept, returns.Count - kept.Count);
    }

    public static decimal AnnualizedVolatilityPercent(IReadOnlyList<DatedReturn> returns)
    {
        if (returns.Count < 2) return 0m;
        return StandardDeviation(Values(returns)) * Sqrt(TradingDaysPerYear) * 100m;
    }

    public static decimal Covariance(IReadOnlyList<DatedReturn> a, IReadOnlyList<DatedReturn> b)
    {
        var (x, y) = AlignByDate(a, b);
        if (x.Count < 2) return 0m;

        var meanX = x.Average();
        var meanY = y.Average();
        var sum = 0m;
        for (var i = 0; i < x.Count; i++)
            sum += (x[i] - meanX) * (y[i] - meanY);
        return sum / x.Count;
    }

    public static decimal Correlation(IReadOnlyList<DatedReturn> a, IReadOnlyList<DatedReturn> b)
    {
        var (x, y) = AlignByDate(a, b);
        if (x.Count < 2) return 0m;

        var sdX = StandardDeviation(x);
        var sdY = StandardDeviation(y);
        // Chuỗi phẳng làm mẫu số bằng 0. Trả 0 — "không đo được quan hệ" — chứ không phải NaN.
        if (sdX == 0m || sdY == 0m) return 0m;

        var meanX = x.Average();
        var meanY = y.Average();
        var cov = 0m;
        for (var i = 0; i < x.Count; i++)
            cov += (x[i] - meanX) * (y[i] - meanY);

        return cov / x.Count / (sdX * sdY);
    }

    public static IReadOnlyList<decimal> Values(IReadOnlyList<DatedReturn> returns) =>
        returns.Select(r => r.Value).ToList();

    /// <summary>
    /// Số quan sát THỰC SỰ được ghép cặp giữa hai chuỗi. Khác <c>Math.Min</c> của hai độ dài: hai
    /// chuỗi cùng số phiên vẫn có thể lệch tập ngày, và báo con số dài hơn là nói quá độ tin cậy
    /// của chính ước lượng đang hiển thị.
    /// </summary>
    public static int AlignedObservationCount(
        IReadOnlyList<DatedReturn> a, IReadOnlyList<DatedReturn> b)
        => AlignByDate(a, b).X.Count;

    /// <summary>
    /// Chuỗi lợi suất của danh mục, gộp các vị thế thành một tài sản tổng hợp có trọng số theo
    /// giá trị. Nhờ đó phép tính trần chỉ còn là bài toán hai tài sản — không cần dựng và nghịch
    /// đảo ma trận hiệp phương sai đầy đủ (ADR-0014).
    /// </summary>
    public static IReadOnlyList<DatedReturn> WeightedSeries(
        IReadOnlyList<decimal> values, IReadOnlyList<IReadOnlyList<DatedReturn>> series)
    {
        if (values.Count == 0 || values.Count != series.Count) return Array.Empty<DatedReturn>();

        var total = values.Sum();
        if (total <= 0m) return Array.Empty<DatedReturn>();

        // Cửa sổ chung là giao của các tập NGÀY, không phải phần đuôi có cùng độ dài.
        var common = series[0].Select(r => r.Date).ToHashSet();
        foreach (var s in series.Skip(1))
            common.IntersectWith(s.Select(r => r.Date));
        if (common.Count == 0) return Array.Empty<DatedReturn>();

        var dates = common.OrderBy(d => d).ToList();
        var accumulator = dates.ToDictionary(d => d, _ => 0m);

        for (var i = 0; i < series.Count; i++)
        {
            var weight = values[i] / total;
            foreach (var r in series[i])
            {
                if (accumulator.ContainsKey(r.Date))
                    accumulator[r.Date] += weight * r.Value;
            }
        }

        return dates.Select(d => new DatedReturn(d, accumulator[d])).ToList();
    }

    /// <summary>
    /// Ngưỡng sụt giảm hiểu là mức lỗ ở độ tin cậy 95% trong <see cref="BudgetHorizonDays"/> phiên,
    /// quy về biến động năm. Chọn chân trời 21 phiên chứ không phải 1 năm là quyết định có đánh đổi
    /// — xem ADR-0014: diễn giải theo năm cho ngân sách 6,1%/năm, thấp hơn hẳn danh mục thật nên
    /// trần sẽ luôn bằng 0.
    /// </summary>
    public static decimal DrawdownToVolatilityBudgetPercent(decimal maxDrawdownPercent)
    {
        if (maxDrawdownPercent <= 0m) return 0m;

        var horizonFraction = Sqrt((decimal)BudgetHorizonDays / TradingDaysPerYear);
        return maxDrawdownPercent / (ConfidenceZ95 * horizonFraction);
    }

    /// <summary>
    /// Biến động danh mục sau khi giải ngân <paramref name="allocation"/> đồng vào một mã có
    /// biến động <paramref name="symbolVolPercent"/> và tương quan <paramref name="correlation"/>
    /// với danh mục hiện tại.
    /// </summary>
    public static decimal ProjectedVolatilityPercent(
        decimal portfolioValue, decimal portfolioVolPercent,
        decimal allocation, decimal symbolVolPercent, decimal correlation)
    {
        var total = portfolioValue + allocation;
        if (total <= 0m) return 0m;

        var p = portfolioValue * portfolioVolPercent;
        var x = allocation * symbolVolPercent;
        var variance = p * p + 2m * correlation * p * x + x * x;
        if (variance <= 0m) return 0m;

        return Sqrt(variance) / total;
    }

    /// <summary>
    /// Số tiền tối đa giải ngân được mà biến động danh mục vẫn ≤ ngân sách.
    /// <para><c>null</c> = không bị ràng buộc bởi biến động (mua bao nhiêu cũng không vượt).</para>
    /// <para><c>0</c> = không mua thêm được đồng nào.</para>
    /// Hai giá trị này khác nhau và người gọi phải phân biệt — gộp chúng lại là biến "thoải mái"
    /// thành "cấm".
    /// </summary>
    public static decimal? SolveMaxAllocation(
        decimal portfolioValue, decimal portfolioVolPercent,
        decimal symbolVolPercent, decimal correlation, decimal budgetVolPercent)
    {
        if (budgetVolPercent <= 0m) return 0m;

        // Ràng buộc S(a) ≤ (V + a)·σ_b, bình phương hai vế rồi gom theo a:
        //   A·a² + B·a + C ≤ 0
        var budgetSq = budgetVolPercent * budgetVolPercent;
        var a = symbolVolPercent * symbolVolPercent - budgetSq;
        var b = 2m * correlation * portfolioValue * portfolioVolPercent * symbolVolPercent
                - 2m * portfolioValue * budgetSq;
        var c = portfolioValue * portfolioValue
                * (portfolioVolPercent * portfolioVolPercent - budgetSq);

        // Danh mục đã vượt ngân sách trước khi thêm gì: C > 0 nên bất phương trình sai ngay tại a = 0.
        if (c > 0m) return 0m;

        // A ≤ 0 nghĩa là σ_mã ≤ σ_ngân sách. Kèm C ≤ 0 (σ_danh mục ≤ σ_ngân sách) thì bất phương
        // trình đúng với mọi a ≥ 0: trộn hai tài sản với ρ ≤ 1 cho biến động không vượt quá
        // max(σ_danh mục, σ_mã), mà max đó đã ≤ ngân sách. Không có trần hữu hạn.
        // Nhánh bậc nhất (A = 0) cũng rơi vào đây: nghiệm hữu hạn đòi B > 0, tức ρ·σ_danh mục >
        // σ_ngân sách ≥ σ_danh mục, tức ρ > 1 — bất khả.
        if (a <= 0m) return null;

        var discriminant = b * b - 4m * a * c;
        if (discriminant < 0m) return 0m;

        var root = (-b + Sqrt(discriminant)) / (2m * a);
        return root > 0m ? root : 0m;
    }

    /// <summary>
    /// Phần trăm rủi ro danh mục mà một vị thế gánh: <c>w · cov(r_i, r_p) / σ²_p</c>. So với phần
    /// trăm vốn nó chiếm để lộ ra chênh lệch.
    /// </summary>
    public static decimal MarginalRiskContributionPercent(
        decimal weight, decimal covarianceWithPortfolio, decimal portfolioVariance)
    {
        if (portfolioVariance <= 0m) return 0m;
        return weight * covarianceWithPortfolio / portfolioVariance * 100m;
    }

    public static decimal StandardDeviation(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2) return 0m;

        var mean = values.Average();
        var sumSq = 0m;
        foreach (var v in values)
            sumSq += (v - mean) * (v - mean);
        return Sqrt(sumSq / values.Count);
    }

    /// <summary>
    /// Ghép hai chuỗi theo NGÀY, giữ phần giao. Bản trước ghép theo đuôi cùng độ dài, và điều đó
    /// sai ngay ở ca thường gặp nhất: một mã bị loại quan sát bất thường ở giữa chuỗi thì mọi cặp
    /// TRƯỚC điểm đó bị ghép lệch một phiên, làm hỏng tương quan mà không gì báo hiệu.
    /// </summary>
    private static (IReadOnlyList<decimal> X, IReadOnlyList<decimal> Y) AlignByDate(
        IReadOnlyList<DatedReturn> a, IReadOnlyList<DatedReturn> b)
    {
        var byDate = b.GroupBy(r => r.Date).ToDictionary(g => g.Key, g => g.Last().Value);

        var x = new List<decimal>(Math.Min(a.Count, b.Count));
        var y = new List<decimal>(x.Capacity);
        foreach (var r in a.OrderBy(r => r.Date))
        {
            if (byDate.TryGetValue(r.Date, out var other))
            {
                x.Add(r.Value);
                y.Add(other);
            }
        }
        return (x, y);
    }

    private static decimal Sqrt(decimal value) =>
        value <= 0m ? 0m : (decimal)Math.Sqrt((double)value);
}
