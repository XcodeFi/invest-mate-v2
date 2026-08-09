using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Sự kiện quyền của một mã trong danh mục. Bất biến — sửa = xoá và tạo lại.
/// </summary>
public class CorporateAction : AggregateRoot
{
    /// <summary>Mệnh giá cổ phiếu niêm yết VN — gốc để quy đổi "cổ tức 5%" ra đồng/CP.</summary>
    public const decimal ParValue = 10_000m;

    public string PortfolioId { get; private set; } = null!;
    public string UserId { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;
    public CorporateActionType Type { get; private set; }
    public DateTime ExDate { get; private set; }
    public DateTime? SettlementDate { get; private set; }
    public DateTime? SettledAt { get; private set; }
    public decimal? AmountPerShare { get; private set; }
    public decimal? TaxRatePercent { get; private set; }
    public decimal? RatioOld { get; private set; }
    public decimal? RatioNew { get; private set; }
    public string DeclaredText { get; private set; } = string.Empty;
    public string? CapitalFlowId { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }

    [BsonConstructor]
    public CorporateAction() { } // MongoDB

    private CorporateAction(string portfolioId, string userId, string symbol,
        CorporateActionType type, DateTime exDate, DateTime? settlementDate, string declaredText, string? note)
    {
        Id = Guid.NewGuid().ToString();
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Symbol = symbol?.ToUpper().Trim() ?? throw new ArgumentNullException(nameof(symbol));
        Type = type;
        ExDate = exDate.Date;
        if (settlementDate.HasValue && settlementDate.Value.Date < ExDate)
            throw new ArgumentException("Ngày về không được trước ngày GDKHQ", nameof(settlementDate));
        SettlementDate = settlementDate?.Date;
        DeclaredText = declaredText;
        Note = note;
        CreatedAt = DateTime.UtcNow;
    }

    public static CorporateAction CashDividend(string portfolioId, string userId, string symbol,
        decimal percentOfPar, DateTime exDate, DateTime? settlementDate, decimal taxRatePercent,
        string? note = null)
    {
        if (percentOfPar <= 0) throw new ArgumentException("Tỷ lệ cổ tức phải lớn hơn 0", nameof(percentOfPar));
        if (taxRatePercent < 0 || taxRatePercent >= 100)
            throw new ArgumentException("Thuế suất phải trong khoảng [0, 100)", nameof(taxRatePercent));

        return new CorporateAction(portfolioId, userId, symbol, CorporateActionType.CashDividend,
            exDate, settlementDate, $"{percentOfPar:0.##}%", note)
        {
            AmountPerShare = percentOfPar / 100m * ParValue,
            TaxRatePercent = taxRatePercent
        };
    }

    public static CorporateAction StockDividend(string portfolioId, string userId, string symbol,
        decimal ratioOld, decimal ratioNew, DateTime exDate, DateTime? settlementDate, string? note = null)
        => FromRatio(portfolioId, userId, symbol, CorporateActionType.StockDividend,
            ratioOld, ratioNew, exDate, settlementDate, note);

    public static CorporateAction StockSplit(string portfolioId, string userId, string symbol,
        decimal ratioOld, decimal ratioNew, DateTime exDate, DateTime? settlementDate, string? note = null)
        => FromRatio(portfolioId, userId, symbol, CorporateActionType.StockSplit,
            ratioOld, ratioNew, exDate, settlementDate, note);

    private static CorporateAction FromRatio(string portfolioId, string userId, string symbol,
        CorporateActionType type, decimal ratioOld, decimal ratioNew,
        DateTime exDate, DateTime? settlementDate, string? note)
    {
        if (ratioOld <= 0) throw new ArgumentException("Tỷ lệ cũ phải lớn hơn 0", nameof(ratioOld));
        if (ratioNew <= ratioOld)
            throw new ArgumentException("Tỷ lệ mới phải lớn hơn tỷ lệ cũ", nameof(ratioNew));

        return new CorporateAction(portfolioId, userId, symbol, type, exDate, settlementDate,
            $"{ratioOld:0.##}:{ratioNew:0.##}", note)
        {
            RatioOld = ratioOld,
            RatioNew = ratioNew
        };
    }

    /// <summary>Hệ số nhân số lượng cổ phiếu. Cổ tức tiền mặt = 1.</summary>
    public decimal Multiplier =>
        RatioOld.HasValue && RatioNew.HasValue && RatioOld.Value > 0
            ? RatioNew.Value / RatioOld.Value
            : 1m;

    /// <summary>Tiền cổ tức thực nhận trên mỗi cổ phiếu, sau thuế TNCN.</summary>
    public decimal NetPerShare =>
        AmountPerShare.HasValue
            ? AmountPerShare.Value * (1m - (TaxRatePercent ?? 0m) / 100m)
            : 0m;

    public void MarkSettled(DateTime settledAt)
    {
        if (settledAt.Date < ExDate)
            throw new ArgumentException("Ngày về không được trước ngày GDKHQ", nameof(settledAt));
        SettledAt = settledAt.Date;
        IncrementVersion();
    }

    public void LinkCapitalFlow(string capitalFlowId)
    {
        CapitalFlowId = capitalFlowId ?? throw new ArgumentNullException(nameof(capitalFlowId));
        IncrementVersion();
    }
}

public enum CorporateActionType
{
    CashDividend,
    StockDividend,
    StockSplit
}
