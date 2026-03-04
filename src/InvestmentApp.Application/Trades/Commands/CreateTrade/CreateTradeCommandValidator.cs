using FluentValidation;

namespace InvestmentApp.Application.Trades.Commands.CreateTrade;

public class CreateTradeCommandValidator : AbstractValidator<CreateTradeCommand>
{
    public CreateTradeCommandValidator()
    {
        RuleFor(x => x.PortfolioId)
            .NotEmpty().WithMessage("Portfolio ID is required");

        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Stock symbol is required")
            .MaximumLength(10).WithMessage("Stock symbol must not exceed 10 characters");

        RuleFor(x => x.TradeType)
            .NotEmpty().WithMessage("Trade type is required")
            .Must(type => type.ToUpper() == "BUY" || type.ToUpper() == "SELL")
            .WithMessage("Trade type must be BUY or SELL");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be positive");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be positive");

        RuleFor(x => x.Fee)
            .GreaterThanOrEqualTo(0).WithMessage("Fee must be non-negative");

        RuleFor(x => x.Tax)
            .GreaterThanOrEqualTo(0).WithMessage("Tax must be non-negative");
    }
}