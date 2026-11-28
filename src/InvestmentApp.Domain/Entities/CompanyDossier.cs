using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>Hồ sơ hiểu biết về một doanh nghiệp. Sống theo mã, không theo lệnh.</summary>
public class CompanyDossier : AggregateRoot
{
    /// <summary>Asia/Ho_Chi_Minh là offset cố định, không có DST.</summary>
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private const int NeedsReviewAfterDays = 90;
    private const int ExpiresAfterDays = 180;

    public string UserId { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;
    public string BusinessModel { get; private set; } = null!;
    public List<MoatItem> Moats { get; private set; } = new();
    public List<RiskFactor> RiskFactors { get; private set; } = new();
    public string? Notes { get; private set; }
    public DateTime ReviewedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? AgentDraftedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    private CompanyDossier() { }

    public CompanyDossier(string userId, string symbol, string businessModel,
        List<MoatItem> moats, List<RiskFactor> riskFactors, string? notes = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = Require(userId, "Mã người dùng");
        Symbol = Require(symbol, "Mã cổ phiếu").ToUpperInvariant();
        BusinessModel = businessModel?.Trim() ?? string.Empty;
        Moats = moats ?? new();
        RiskFactors = Normalize(riskFactors ?? new());
        Notes = notes;
        CreatedAt = UpdatedAt = ReviewedAt = DateTime.UtcNow;
    }

    public void UpdateByOwner(string businessModel, List<MoatItem> moats,
        List<RiskFactor> riskFactors, string? notes)
    {
        Apply(businessModel, moats, riskFactors, notes);
    }

    public void UpdateByAgent(string businessModel, List<MoatItem> moats,
        List<RiskFactor> riskFactors, string? notes)
    {
        Apply(businessModel, moats, riskFactors, notes);
        AgentDraftedAt = DateTime.UtcNow;
        ConfirmedAt = null;   // người dùng chưa đọc bản mới
    }

    public void Confirm()
    {
        var now = DateTime.UtcNow;
        ReviewedAt = now;
        ConfirmedAt = now;
        UpdatedAt = now;
        IncrementVersion();
    }

    /// <summary>
    /// Số ngày quá mốc "nên soát lại" (90 ngày), theo ngày lịch VN. Chưa ký thì 0 — đồng hồ hạn tươi
    /// chưa chạy nên một con số quá hạn ở đó là bịa. Sống ở entity để ngưỡng 90 ngày và phép lệch giờ
    /// VN chỉ có MỘT bản: nhánh này đã một lần phải gộp bản sao thứ tư của ngưỡng 5% (66acadb).
    /// </summary>
    public int DaysOverdueForReview(DateTime utcNow)
    {
        if (ConfirmedAt is null) return 0;
        var days = (utcNow.Add(VnOffset).Date - ReviewedAt.Add(VnOffset).Date).TotalDays;
        return (int)Math.Max(0, days - NeedsReviewAfterDays);
    }

    public DossierFreshness GetFreshness(DateTime utcNow)
    {
        if (ConfirmedAt is null) return DossierFreshness.Unconfirmed;

        var days = (utcNow.Add(VnOffset).Date - ReviewedAt.Add(VnOffset).Date).TotalDays;

        return FreshnessForDays(days);
    }

    /// <summary>MỘT bản duy nhất của thang 90/180 ngày: dùng cho GetFreshness (tại hôm nay) và DescribeAtTrade (tại ngày lệnh).</summary>
    private static DossierFreshness FreshnessForDays(double days)
    {
        if (days >= ExpiresAfterDays) return DossierFreshness.Expired;
        if (days >= NeedsReviewAfterDays) return DossierFreshness.NeedsReview;
        return DossierFreshness.Fresh;
    }

    /// <summary>
    /// Hồ sơ "lúc đó" của một giao dịch, chỉ từ ReviewedAt/ConfirmedAt HIỆN TẠI (Confirm() ghi đè ReviewedAt, lịch sử các lần ký không được lưu).
    /// Chưa ký: "unsigned". Lệnh trước lần ký hiện tại (ngày VN của lệnh &lt; ngày VN của ReviewedAt): "resigned-after", KHÔNG có phán quyết
    /// vì không biết lần ký trước đó. Còn lại: "signed" + số ngày từ lần ký tới lệnh và mức tươi theo đúng hai ngưỡng 90/180 của entity (một bản duy nhất).
    /// </summary>
    public DossierAtTrade DescribeAtTrade(DateTime tradeUtc)
    {
        if (ConfirmedAt is null) return new DossierAtTrade("unsigned", null, null, null);

        var tradeDay = tradeUtc.Add(VnOffset).Date;
        var signedDay = ReviewedAt.Add(VnOffset).Date;
        if (tradeDay < signedDay) return new DossierAtTrade("resigned-after", null, ReviewedAt, null);

        var days = (int)(tradeDay - signedDay).TotalDays;
        var freshness = FreshnessForDays(days) switch
        {
            DossierFreshness.Expired => "expired",
            DossierFreshness.NeedsReview => "needs-review",
            _ => "fresh"
        };
        return new DossierAtTrade("signed", days, null, freshness);
    }

    private void Apply(string businessModel, List<MoatItem> moats,
        List<RiskFactor> riskFactors, string? notes)
    {
        BusinessModel = businessModel?.Trim() ?? string.Empty;
        Moats = moats ?? new();
        RiskFactors = Normalize(riskFactors ?? new());
        Notes = notes;
        // KHÔNG chạm ReviewedAt — chỉ Confirm() đẩy đồng hồ hạn tươi. Nếu sửa
        // nội dung cũng đẩy, hồ sơ Expired chỉ cần sửa một ký tự là hồi sinh.
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private static string Require(string value, string label)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{label} không được rỗng")
            : value.Trim();

    private static List<RiskFactor> Normalize(List<RiskFactor> factors)
    {
        foreach (var f in factors)
        {
            if (string.IsNullOrWhiteSpace(f.ObservableSignal))
                throw new ArgumentException(
                    "Mỗi yếu tố rủi ro phải có dấu hiệu quan sát được");
        }

        if (factors.Count(f => f.IsDealBreaker) > 1)
            throw new InvalidOperationException(
                "Chỉ được đánh dấu tối đa một yếu tố hủy diệt");

        var ordered = factors.OrderBy(f => f.Rank).ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].Rank = i + 1;
        return ordered;
    }
}

public class MoatItem
{
    public string Description { get; set; } = string.Empty;
}

public class RiskFactor
{
    /// <summary>1 = nguy hiểm nhất. Entity tự chuẩn hóa về dense 1..N.</summary>
    public int Rank { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>"Biết nó đang xảy ra bằng gì" — bắt buộc, không có thì không phải rủi ro.</summary>
    public string ObservableSignal { get; set; } = string.Empty;

    /// <summary>Xảy ra thì bán hết, không phải chỉ cắt một lệnh. Tối đa 1 mỗi hồ sơ.</summary>
    public bool IsDealBreaker { get; set; }

    public InvalidationTrigger? SuggestedTrigger { get; set; }
}

public enum DossierFreshness
{
    Unconfirmed,
    Fresh,
    NeedsReview,
    Expired
}

/// <summary>Huy hiệu "hồ sơ lúc đó" của một giao dịch. State: unsigned | resigned-after | signed; Freshness (chỉ khi signed): fresh | needs-review | expired.</summary>
public sealed record DossierAtTrade(string State, int? DaysSigned, DateTime? ResignedAt, string? Freshness);
