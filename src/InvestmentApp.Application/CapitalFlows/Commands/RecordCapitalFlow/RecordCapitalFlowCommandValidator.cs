using FluentValidation;

namespace InvestmentApp.Application.CapitalFlows.Commands.RecordCapitalFlow;

public class RecordCapitalFlowCommandValidator : AbstractValidator<RecordCapitalFlowCommand>
{
    private static readonly string[] ValidTypes = { "Deposit", "Withdraw", "Dividend", "Interest", "Fee" };

    public RecordCapitalFlowCommandValidator()
    {
        RuleFor(x => x.PortfolioId)
            .NotEmpty().WithMessage("Portfolio ID is required");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Flow type is required")
            .Must(t => ValidTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Flow type must be one of: Deposit, Withdraw, Dividend, Interest, Fee");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .MaximumLength(5).WithMessage("Currency code must not exceed 5 characters");
    }
}
