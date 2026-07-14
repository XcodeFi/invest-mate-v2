using FluentValidation;

namespace InvestmentApp.Application.ApiKeys.Commands.CreateApiKey;

public class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên khóa API là bắt buộc.")
            .MaximumLength(100).WithMessage("Tên khóa API tối đa 100 ký tự.");

        RuleFor(x => x.ExpiresInDays)
            .InclusiveBetween(1, 365).WithMessage("Thời hạn phải từ 1 đến 365 ngày.");
    }
}
