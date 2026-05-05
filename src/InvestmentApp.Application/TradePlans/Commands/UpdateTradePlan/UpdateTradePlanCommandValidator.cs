using FluentValidation;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;

namespace InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;

/// <summary>
/// Server-side gate for PUT /api/v1/trade-plans/{id}. Most fields are nullable — only validate
/// what client sent. Reuses InvalidationRule child validator from CreateTradePlanCommandValidator
/// so both endpoints enforce the same Detail ≥ 20 chars rule.
/// </summary>
public class UpdateTradePlanCommandValidator : AbstractValidator<UpdateTradePlanCommand>
{
    public UpdateTradePlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();

        // EntryPrice / Quantity / StopLoss validated only when client touches them. Property path
        // stays at the field name (`StopLoss`) so test/error consumers see consistent paths.
        RuleFor(x => x.EntryPrice)
            .GreaterThan(0).WithMessage("Entry price phải lớn hơn 0")
            .When(x => x.EntryPrice.HasValue);

        RuleFor(x => x.StopLoss)
            .GreaterThan(0)
            .WithMessage("Stop-loss phải lớn hơn 0 (0 đồng nghĩa rủi ro 100%)")
            .When(x => x.StopLoss.HasValue);

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .When(x => x.Quantity.HasValue);

        RuleForEach(x => x.InvalidationCriteria!)
            .ChildRules(CreateTradePlanCommandValidator.InvalidationRuleChild)
            .When(x => x.InvalidationCriteria != null && x.InvalidationCriteria.Count > 0);
    }
}
