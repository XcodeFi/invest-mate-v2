using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Services;

/// <summary>
/// Phí khứ hồi (mua + bán + thuế bán) của CẢ vị thế khi vào ở <paramref name="entry"/> và thoát ở <paramref name="exitPrice"/>.
/// Domain không biết dịch vụ phí; lớp Application dựng hàm này từ <c>IFeeCalculationService</c>.
/// </summary>
public delegate decimal RoundTripFee(decimal entry, decimal exitPrice, decimal quantity);

/// <summary>Kỳ vọng của một kế hoạch từ bảng kịch bản. Mọi trường null khi chưa tính được.</summary>
public sealed record PlanExpectation(decimal? EvR, decimal? ImpliedWinPercent, decimal? EvAmount);

/// <summary>
/// Hàm thuần: EV = Σ (xác suất kịch bản × R ròng của kịch bản). R ròng = (lãi/lỗ gộp − phí khứ hồi) / 1R, với 1R =
/// (Entry − SL) × số lượng. Chỉ tính khi tổng xác suất = 100% (± <see cref="ProbabilityTolerance"/>), mọi giá thoát &gt; 0
/// và 1R &gt; 0; ngược lại null (không bao giờ ra con số trông như thật từ dữ liệu sai).
/// </summary>
public static class ExpectedValue
{
    public const decimal ProbabilityTolerance = 0.5m;

    public static PlanExpectation ForPlan(TradePlan plan, RoundTripFee? fee = null)
    {
        var none = new PlanExpectation(null, null, null);
        var scenarios = plan.OutcomeScenarios;
        if (scenarios == null || scenarios.Count == 0) return none;

        var risk = RMath.Build(plan, plan.StopLoss, RMath.RiskBasisPlanned);   // Buy, Entry > SL > 0, số lượng > 0
        if (risk == null) return none;

        if (Math.Abs(scenarios.Sum(s => s.ProbabilityPercent) - 100m) > ProbabilityTolerance) return none;
        if (scenarios.Any(s => s.ProbabilityPercent < 0m || s.ExitPrice <= 0m)) return none;

        decimal ev = 0m, win = 0m;
        foreach (var s in scenarios)
        {
            var gross = (s.ExitPrice - risk.Entry) * risk.Quantity;
            var fees = fee?.Invoke(risk.Entry, s.ExitPrice, risk.Quantity) ?? 0m;
            var r = (gross - fees) / risk.RiskAmount;
            ev += s.ProbabilityPercent / 100m * r;
            if (RMath.Classify(r) == TradeOutcome.Win) win += s.ProbabilityPercent;
        }

        return new PlanExpectation(ev, win, ev * risk.RiskAmount);
    }

    /// <summary>
    /// Ba dòng dựng sẵn cho người dùng điền xác suất: đạt kế hoạch (giá thoát bình quân của các mốc chốt lời, không có thì
    /// Target), cắt lỗ, thoát sớm / hoà (giá vào). Xác suất để 0: không điền sẵn từ tỉ lệ nền để tránh neo.
    /// Kế hoạch bán không có bảng.
    /// </summary>
    public static IReadOnlyList<OutcomeScenario> DefaultScenarios(TradePlan plan)
    {
        var risk = RMath.Build(plan, plan.StopLoss, RMath.RiskBasisPlanned);
        if (risk == null) return Array.Empty<OutcomeScenario>();

        var planned = RMath.BlendedExitPrice(plan.ExitTargets, plan.Target) ?? plan.Target;
        return new[]
        {
            new OutcomeScenario { Label = "Đạt kế hoạch", ProbabilityPercent = 0m, ExitPrice = planned },
            new OutcomeScenario { Label = "Cắt lỗ", ProbabilityPercent = 0m, ExitPrice = plan.StopLoss },
            new OutcomeScenario { Label = "Thoát sớm / hoà", ProbabilityPercent = 0m, ExitPrice = risk.Entry },
        };
    }
}
