using FluentValidation;
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
    }

    /// <summary>
    /// Reused by Update validator. Public so test code (and other commands editing
    /// invalidation rules in the future) share the same rule.
    /// </summary>
    public static void InvalidationRuleChild(InlineValidator<InvalidationRuleDto> rule)
    {
        rule.RuleFor(r => r.Trigger)
            .NotEmpty()
            .Must(BeValidTrigger)
            .WithMessage("Trigger không hợp lệ — phải là một trong: " +
                         "EarningsMiss, TrendBreak, NewsShock, ThesisTimeout, Manual");

        rule.RuleFor(r => r.Detail)
            .NotEmpty()
            .WithMessage("Mô tả điều kiện không được rỗng")
            .Must(d => d != null && d.Trim().Length >= MinInvalidationDetailLength)
            .WithMessage($"Mô tả điều kiện phải có ít nhất {MinInvalidationDetailLength} ký tự (sau Trim) " +
                         "để có thể chứng minh sai");
    }

    private static bool BeValidTrigger(string? trigger)
        => !string.IsNullOrWhiteSpace(trigger) && Enum.TryParse<InvalidationTrigger>(trigger, ignoreCase: true, out _);
}
