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

    public DossierFreshness GetFreshness(DateTime utcNow)
    {
        if (ConfirmedAt is null) return DossierFreshness.Unconfirmed;

        var days = (utcNow.Add(VnOffset).Date - ReviewedAt.Add(VnOffset).Date).TotalDays;

        if (days >= ExpiresAfterDays) return DossierFreshness.Expired;
        if (days >= NeedsReviewAfterDays) return DossierFreshness.NeedsReview;
        return DossierFreshness.Fresh;
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
