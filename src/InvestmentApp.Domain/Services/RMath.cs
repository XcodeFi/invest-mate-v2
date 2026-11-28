using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Services;

public enum TradeOutcome
{
    Win,
    Loss,
    Draw
}

/// <summary>
/// Toán thuần cho kỳ vọng theo R: 1R = số tiền rủi ro ban đầu = |Entry − SL| × số lượng.
/// Không đọc/ghi dữ liệu, không biết gì về nguồn gốc con số; thiếu dữ liệu thì trả null chứ không trả 0
/// (0 lẫn với "hoà vốn", còn null nghĩa là "không tính được").
/// </summary>
/// <summary>Kết quả R thực tế kèm cơ sở tính 1R (null/null nếu không tính được).</summary>
public readonly record struct RealizedRResult(decimal? R, string? Basis);

public static class RMath
{
    /// <summary>Ảnh chụp chốt khi chốt kế hoạch.</summary>
    public const string RiskBasisPlanned = "chot-ke-hoach";

    /// <summary>Kế hoạch cũ chưa có ảnh chụp: suy ra từ dữ liệu còn lại.</summary>
    public const string RiskBasisEstimated = "uoc-tinh";

    /// <summary>Có lot chưa khớp lúc review: 1R vẫn theo khối lượng dự tính.</summary>
    public const string RiskBasisPartialFill = "khop-mot-phan";

    /// <summary>Vùng hoà: |R| ≤ 0,1 không tính thắng hay thua.</summary>
    public const decimal DrawBand = 0.1m;

    /// <summary>(Giá thoát − Entry) / (Entry − SL). Null nếu SL không thấp hơn Entry (lệnh mua).</summary>
    public static decimal? RiskReward(decimal entry, decimal stopLoss, decimal exitPrice)
        => entry > stopLoss ? (exitPrice - entry) / (entry - stopLoss) : null;

    /// <summary>
    /// Giá thoát bình quân của cả vị thế: bình quân các mức chốt lời theo <c>PercentOfPosition</c>
    /// (chuẩn hoá khi tổng không bằng 100). Cắt lỗ không tính. Không có mức nào dùng được thì lấy <paramref name="fallbackTarget"/>.
    /// </summary>
    public static decimal? BlendedExitPrice(IReadOnlyList<ExitTarget>? targets, decimal? fallbackTarget)
    {
        var usable = (targets ?? Array.Empty<ExitTarget>())
            .Where(t => t.ActionType != ExitActionType.CutLoss
                        && t.Price > 0m
                        && t.PercentOfPosition is > 0m)
            .ToList();

        if (usable.Count > 0)
        {
            var totalPercent = usable.Sum(t => t.PercentOfPosition!.Value);
            return usable.Sum(t => t.Price * t.PercentOfPosition!.Value) / totalPercent;
        }

        return fallbackTarget is > 0m ? fallbackTarget : null;
    }

    /// <summary>
    /// R:R do hệ thống tính từ dữ liệu kế hoạch: (giá thoát bình quân − Entry) / (Entry − SL hiện tại), làm tròn 2 chữ số.
    /// Chỉ lệnh mua, Entry &gt; SL &gt; 0 và có giá thoát (các mức chốt lời hoặc Target); ngược lại null.
    /// </summary>
    public static decimal? ComputeRiskReward(TradePlan plan)
    {
        var snapshot = Build(plan, plan.StopLoss, RiskBasisPlanned);
        if (snapshot == null) return null;
        var exit = BlendedExitPrice(plan.ExitTargets, plan.Target);
        if (exit == null) return null;
        var rr = RiskReward(snapshot.Entry, snapshot.StopLoss, exit.Value);
        return rr == null ? null : Math.Round(rr.Value, 2);
    }

    /// <summary>
    /// R:R hiệu lực: số tính được thì dùng số tính, không tính được (lệnh bán, thiếu mục tiêu...) thì dùng giá trị người dùng
    /// đã gõ. Tính khi đọc, không bao giờ ghi đè field đã lưu.
    /// </summary>
    public static decimal? EffectiveRiskReward(TradePlan plan)
        => ComputeRiskReward(plan) ?? plan.RiskRewardRatio;

    /// <summary>
    /// R thực tế của một kế hoạch đang được review với PnL ròng <paramref name="pnl"/>. 1R lấy từ ảnh chụp
    /// <see cref="TradePlan.InitialRisk"/>, không có (kế hoạch cũ) thì <see cref="EstimateLegacy"/>. Có lot chưa bao giờ khớp thì
    /// cờ cơ sở là <see cref="RiskBasisPartialFill"/>: 1R vẫn theo khối lượng dự tính, không tự co lại.
    /// </summary>
    public static RealizedRResult ResolveRealizedR(TradePlan plan, decimal pnl, decimal riskFraction = 1m)
    {
        var risk = plan.InitialRisk ?? EstimateLegacy(plan);
        if (risk == null) return new RealizedRResult(null, null);

        // Review phần đã chốt: PnL chỉ của phần đã bán thì 1R cũng chỉ của phần đó
        // (= RiskAmount × số đã bán ÷ tổng số lượng đã điều chỉnh theo sự kiện quyền).
        var r = RealizedR(pnl, risk.RiskAmount * riskFraction);
        if (r == null) return new RealizedRResult(null, null);

        var basis = risk.RiskBasis == RiskBasisPlanned && plan.Lots?.Any(l => l.Status == PlanLotStatus.Pending) == true
            ? RiskBasisPartialFill
            : risk.RiskBasis;
        return new RealizedRResult(Math.Round(r.Value, 4), basis);
    }

    /// <summary>
    /// R thực tế của kế hoạch ĐÃ review. Dùng giá trị đã lưu nếu có; kế hoạch cũ thiếu thì tính lại từ PnL đã lưu. Chỉ
    /// đọc: kết quả không được gán ngược vào entity (<c>UpdateReviewLessons</c> lưu cả <c>ReviewData</c> xuống DB).
    /// </summary>
    public static RealizedRResult ResolveRealizedR(TradePlan plan)
    {
        var review = plan.ReviewData;
        if (review == null || plan.Status != TradePlanStatus.Reviewed) return new RealizedRResult(null, null);
        if (review.RealizedR != null) return new RealizedRResult(review.RealizedR, review.RiskBasis);
        return ResolveRealizedR(plan, review.PnLAmount);
    }

    /// <summary>
    /// R thực tế dùng cho THỐNG KÊ (kỳ vọng, độ khớp ước lượng, Kelly): null nếu review còn cổ phiếu chưa bán
    /// (<see cref="CampaignReviewData.OpenQuantity"/> &gt; 0), vì chiến dịch chưa đóng hết thì kết quả chưa phải của cả chiến dịch.
    /// Review cũ (OpenQuantity null) giữ nguyên hành vi.
    /// </summary>
    public static decimal? ClosedRealizedR(TradePlan plan)
        => plan.ReviewData?.OpenQuantity is > 0m ? null : ResolveRealizedR(plan).R;

    /// <summary>PnL ròng / số tiền rủi ro ban đầu. Null nếu không có rủi ro (không chia cho 0).</summary>
    public static decimal? RealizedR(decimal netPnL, decimal riskAmount)
        => riskAmount > 0m ? netPnL / riskAmount : null;

    public static TradeOutcome Classify(decimal r)
        => r > DrawBand ? TradeOutcome.Win : r < -DrawBand ? TradeOutcome.Loss : TradeOutcome.Draw;

    /// <summary>
    /// Dựng ảnh chụp 1R từ dữ liệu kế hoạch. Chỉ lệnh mua, Entry &gt; SL &gt; 0 và có số lượng. Entry của lệnh nhiều lot
    /// là bình quân giá dự tính theo khối lượng dự tính.
    /// </summary>
    internal static InitialRiskSnapshot? Build(TradePlan plan, decimal stopLoss, string basis)
    {
        if (string.Equals(plan.Direction, "Sell", StringComparison.OrdinalIgnoreCase)) return null;

        decimal entry = plan.EntryPrice;
        decimal quantity = plan.Quantity;
        var lots = plan.Lots?.Where(l => l.Status != PlanLotStatus.Cancelled && l.PlannedQuantity > 0).ToList();
        if (lots is { Count: > 0 })
        {
            quantity = lots.Sum(l => (decimal)l.PlannedQuantity);
            entry = lots.Sum(l => l.PlannedPrice * l.PlannedQuantity) / quantity;
        }

        if (stopLoss <= 0m || entry <= stopLoss || quantity <= 0m) return null;

        return new InitialRiskSnapshot
        {
            Entry = entry,
            StopLoss = stopLoss,
            Quantity = quantity,
            RiskAmount = (entry - stopLoss) * quantity,
            RiskBasis = basis,
            CapturedAt = DateTime.UtcNow
        };
    }

    /// <summary>Ảnh chụp cho kế hoạch từ lúc chốt: theo SL hiện tại của kế hoạch.</summary>
    internal static InitialRiskSnapshot? Capture(TradePlan plan) => Build(plan, plan.StopLoss, RiskBasisPlanned);

    /// <summary>
    /// Kế hoạch cũ chưa có ảnh chụp: dùng SL đầu tiên trong <c>StopLossHistory</c> (OldPrice &gt; 0; 0 nghĩa là chưa đặt)
    /// nếu có, không thì SL hiện tại. Gắn cờ <see cref="RiskBasisEstimated"/>.
    /// </summary>
    public static InitialRiskSnapshot? EstimateLegacy(TradePlan plan)
    {
        var firstStop = plan.StopLossHistory?.FirstOrDefault()?.OldPrice ?? 0m;
        return Build(plan, firstStop > 0m ? firstStop : plan.StopLoss, RiskBasisEstimated);
    }
}
