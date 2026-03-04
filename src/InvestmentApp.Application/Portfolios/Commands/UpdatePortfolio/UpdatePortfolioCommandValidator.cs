using FluentValidation;

namespace InvestmentApp.Application.Portfolios.Commands.UpdatePortfolio;

public class UpdatePortfolioCommandValidator : AbstractValidator<UpdatePortfolioCommand>
{
    public UpdatePortfolioCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Portfolio name is required")
            .MaximumLength(100).WithMessage("Portfolio name must not exceed 100 characters");

        RuleFor(x => x.InitialCapital)
            .GreaterThanOrEqualTo(0).WithMessage("Initial capital must be non-negative");
    }
}
