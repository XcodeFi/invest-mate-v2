namespace InvestmentApp.Application.MarketData.Trend;

/// <summary>
/// Các tiêu chí "Trend Template" của Mark Minervini tính trên dữ liệu của mã: từng dòng có giá trị và khoảng cách. KHÔNG có kết luận tổng
/// "đạt/không đạt" (sẽ thành tín hiệu mua), không điểm số. Là bộ lọc theo xu hướng, không phải tín hiệu mua.
/// </summary>
public class TrendTemplateInfo
{
    /// <summary>Số phiên dữ liệu đã dùng.</summary>
    public int Sessions { get; set; }
    public List<TrendTemplateCriterion> Criteria { get; set; } = new();
    public string Note { get; set; } = null!;
}

public class TrendTemplateCriterion
{
    /// <summary>Khoá ổn định, ví dụ price_above_sma150.</summary>
    public string Key { get; set; } = null!;
    public string Rule { get; set; } = null!;
    /// <summary>Khoảng cách theo %; null khi chưa đủ dữ liệu (KHÔNG phải 0).</summary>
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    /// <summary>Ngưỡng của tiêu chí (30 cho "cao hơn đáy 52 tuần 30%", 25 cho "cách đỉnh 52 tuần tối đa 25%", 0 cho các so sánh); null khi không có ngưỡng.</summary>
    public decimal? Threshold { get; set; }
    /// <summary>Tiêu chí này có thoả không; null khi chưa đủ dữ liệu hoặc không tính được.</summary>
    public bool? Met { get; set; }
    /// <summary>Lý do khi <see cref="Value"/> là null.</summary>
    public string? Detail { get; set; }
}
