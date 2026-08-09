using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Quy các mức giá tuyệt đối trên <see cref="TradePlan"/> về mặt bằng giá hiện tại.
/// Kế hoạch lưu giá tại thời điểm đặt; sau ngày GDKHQ giá thị trường đã bị điều chỉnh
/// còn giá kế hoạch thì chưa, nên điều kiện kịch bản tự kích hoạt sai.
/// Chỉ đụng vào các trường mang GIÁ — ngưỡng phần trăm và số ngày giữ nguyên.
/// </summary>
public static class TradePlanPriceAdjuster
{
    /// <summary>
    /// Mốc thời gian của mặt bằng giá trên kế hoạch. Không dùng <c>UpdatedAt</c>:
    /// nó nhảy mỗi lần một nhánh kịch bản kích hoạt, làm các nhánh còn lại thôi được điều chỉnh.
    /// </summary>
    public static DateTime PriceAnchor(TradePlan plan) => plan.PricesSetAt ?? plan.CreatedAt;

    /// <summary>
    /// Mốc của ngưỡng nằm trong cây kịch bản. Riêng vì sửa nhánh kịch bản không đặt lại giá nhập.
    /// </summary>
    public static DateTime ScenarioAnchor(TradePlan plan) => plan.ScenarioPricesSetAt ?? PriceAnchor(plan);

    public static decimal AdjustedEntryPrice(TradePlan plan, IEnumerable<CorporateAction> actions)
        => CorporateActionAdjuster.AdjustPrice(plan.EntryPrice, PriceAnchor(plan), actions);

    /// <summary>
    /// Ngưỡng của node chỉ là giá với <c>PriceAbove</c> / <c>PriceBelow</c>;
    /// <c>PricePercentChange</c> mang phần trăm và <c>TimeElapsed</c> mang số ngày.
    /// </summary>
    public static decimal? AdjustedConditionValue(
        ScenarioNode node, TradePlan plan, IEnumerable<CorporateAction> actions)
    {
        if (!node.ConditionValue.HasValue) return null;
        if (node.ConditionType is not (ScenarioConditionType.PriceAbove or ScenarioConditionType.PriceBelow))
            return node.ConditionValue;

        return CorporateActionAdjuster.AdjustPrice(node.ConditionValue.Value, ScenarioAnchor(plan), actions);
    }

    /// <summary><c>ActionValue</c> chỉ mang giá với <c>MoveStopLoss</c>; còn lại là phần trăm.</summary>
    public static decimal? AdjustedActionValue(
        ScenarioNode node, TradePlan plan, IEnumerable<CorporateAction> actions)
        => node is { ActionType: ScenarioActionType.MoveStopLoss, ActionValue: { } value }
            ? CorporateActionAdjuster.AdjustPrice(value, ScenarioAnchor(plan), actions)
            : node.ActionValue;

    public static decimal? AdjustedActivationPrice(
        TrailingStopConfig config, TradePlan plan, IEnumerable<CorporateAction> actions)
        => config.ActivationPrice.HasValue
            ? CorporateActionAdjuster.AdjustPrice(config.ActivationPrice.Value, ScenarioAnchor(plan), actions)
            : null;

    /// <summary>Khoảng cách giá (biên trượt, bước nhảy) — co theo hệ số, không trừ cổ tức tiền mặt.</summary>
    public static decimal AdjustedDelta(decimal delta, TradePlan plan, IEnumerable<CorporateAction> actions)
        => CorporateActionAdjuster.AdjustDelta(delta, ScenarioAnchor(plan), actions);

    /// <summary>Chỉ <c>FixedAmount</c> mang số tiền tuyệt đối; percentage và ATR là bội số.</summary>
    public static decimal AdjustedTrailValue(
        TrailingStopConfig config, TradePlan plan, IEnumerable<CorporateAction> actions)
        => config.Method == TrailingStopMethod.FixedAmount
            ? AdjustedDelta(config.TrailValue, plan, actions)
            : config.TrailValue;

    /// <summary>
    /// Quy <c>HighestPrice</c> / <c>CurrentTrailingStop</c> về mặt bằng giá mới đúng một lần.
    /// Hai giá trị này được ghi đè trở lại vào kế hoạch nên không điều chỉnh tại thời điểm đọc
    /// được: lần ghi kế tiếp lưu giá ở mặt bằng mới, lần đọc sau lại hạ thêm một lần nữa.
    /// </summary>
    /// <returns><c>true</c> nếu có thay đổi và kế hoạch cần được lưu lại.</returns>
    public static bool RebaseTrailingState(
        TrailingStopConfig config, TradePlan plan, IEnumerable<CorporateAction> actions)
    {
        if (!config.HighestPrice.HasValue && !config.CurrentTrailingStop.HasValue) return false;

        var basis = config.PriceBasisAt ?? ScenarioAnchor(plan);
        var today = DateTime.UtcNow.Date;
        // Chặn trên theo hôm nay: sự kiện công bố trước mà chưa tới ngày GDKHQ thì giá thị
        // trường chưa điều chỉnh — rebase sớm là ghi đè vĩnh viễn một con số sai.
        var pending = actions.Where(a => a.ExDate.Date > basis.Date && a.ExDate.Date <= today).ToList();
        if (pending.Count == 0) return false;

        if (config.HighestPrice.HasValue)
            config.HighestPrice = CorporateActionAdjuster.AdjustPrice(config.HighestPrice.Value, basis, pending);
        if (config.CurrentTrailingStop.HasValue)
            config.CurrentTrailingStop = CorporateActionAdjuster.AdjustPrice(config.CurrentTrailingStop.Value, basis, pending);

        config.PriceBasisAt = DateTime.UtcNow;
        return true;
    }
}
