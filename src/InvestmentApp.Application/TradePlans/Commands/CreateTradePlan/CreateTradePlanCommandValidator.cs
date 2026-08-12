using FluentValidation;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;

/// <summary>
/// Server-side gate for POST /api/v1/trade-plans. Last line of defense — FE has parallel
/// validators but cannot be trusted (curl-able / extension-modifiable).
///
/// Key gates:
///   - <see cref="CreateTradePlanCommand.StopLoss"/> &gt; 0 (zero implies risk = 100% of position;
///     downstream Risk service throws "lt" comparison error that gets swallowed silently).
///   - Each <see cref="InvalidationRuleDto"/> in <see cref="CreateTradePlanCommand.InvalidationCriteria"/>
///     must have <see cref="InvalidationRuleDto.Detail"/> ≥ 20 chars (Trim) and a parseable
///     <see cref="InvalidationTrigger"/>. An empty Detail produces a rule that can never trigger
///     and is therefore worse than no rule at all.
/// </summary>
public class CreateTradePlanCommandValidator : AbstractValidator<CreateTradePlanCommand>
{
    public const int MinInvalidationDetailLength = 20;

    public CreateTradePlanCommandValidator()
    {
        RuleFor(x => x.Symbol).NotEmpty().WithMessage("Symbol không được rỗng");
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.EntryPrice)
            .GreaterThan(0).WithMessage("Entry price phải lớn hơn 0");

        RuleFor(x => x.StopLoss)
            .GreaterThan(0)
            .WithMessage("Stop-loss phải lớn hơn 0 (0 đồng nghĩa rủi ro 100% — không có biên an toàn)");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity phải lớn hơn 0");

        RuleForEach(x => x.InvalidationCriteria!)
            .ChildRules(InvalidationRuleChild)
            .When(x => x.InvalidationCriteria != null && x.InvalidationCriteria.Count > 0);

        RuleForEach(x => x.ScenarioNodes!)
            .ChildRules(ScenarioNodeChild)
            .When(x => x.ScenarioNodes != null && x.ScenarioNodes.Count > 0);

        RuleForEach(x => x.ExitTargets!)
            .ChildRules(ExitTargetChild)
            .When(x => x.ExitTargets != null && x.ExitTargets.Count > 0);
    }

    /// <summary>
    /// Dùng chung với validator của Update. Cùng lý do với <see cref="ScenarioNodeChild"/>: mốc
    /// thoát thiếu actionType từng âm thầm thành TakeProfit, trong khi người gọi có thể đang định
    /// khai CutLoss. Tập giá trị ở đây KHÁC tập của scenarioNodes dù trùng tên trường.
    /// </summary>
    public static void ExitTargetChild(InlineValidator<ExitTargetDto> rule)
    {
        rule.RuleFor(e => e.ActionType)
            .NotNull()
            .WithMessage("actionType bắt buộc — một trong: TakeProfit, CutLoss, TrailingStop, PartialExit");
    }

    /// <summary>
    /// Dùng chung với validator của Update. Hành động là quyết định nên không được có mặc định
    /// ngầm: node thiếu actionType từng im lặng trở thành "bán 50% vị thế". Method là đơn vị đo
    /// nên vẫn được phép bỏ trống.
    /// </summary>
    public static void ScenarioNodeChild(InlineValidator<ScenarioNodeDto> rule)
    {
        rule.RuleFor(n => n.ActionType)
            .NotNull()
            .WithMessage("actionType bắt buộc — một trong: SellPercent, SellAll, MoveStopLoss, " +
                         "MoveStopToBreakeven, ActivateTrailingStop, AddPosition, SendNotification");

        rule.RuleFor(n => n.ConditionType)
            .NotNull()
            .WithMessage("conditionType bắt buộc — một trong: PriceAbove, PriceBelow, " +
                         "PricePercentChange, TrailingStopHit, TimeElapsed");
    }

    /// <summary>
    /// Reused by Update validator. Public so test code (and other commands editing
    /// invalidation rules in the future) share the same rule.
    /// </summary>
    public static void InvalidationRuleChild(InlineValidator<InvalidationRuleDto> rule)
    {
        // Trigger là enum thật nên giá trị lạ đã bị chặn ở tầng deserialize; ở đây chỉ còn
        // phải chặn "không gửi gì".
        rule.RuleFor(r => r.Trigger)
            .NotNull()
            .WithMessage("trigger bắt buộc — một trong: " +
                         "EarningsMiss, TrendBreak, NewsShock, ThesisTimeout, Manual");

        rule.RuleFor(r => r.Detail)
            .NotEmpty()
            .WithMessage("Mô tả điều kiện không được rỗng")
            .Must(d => d != null && d.Trim().Length >= MinInvalidationDetailLength)
            .WithMessage($"Mô tả điều kiện phải có ít nhất {MinInvalidationDetailLength} ký tự (sau Trim) " +
                         "để có thể chứng minh sai");
    }

}
