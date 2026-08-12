using FluentValidation;

namespace InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;

/// <summary>
/// Body thiếu hẳn key `dates` thì `Dates` bind ra null (không có 400 tự động vì
/// `SuppressModelStateInvalidFilter = true`). Không có validator vẫn ra 400 — `Select` trên
/// null ném `ArgumentNullException` và middleware map `ArgumentException` sang 400 — nhưng
/// thân lỗi là "Value cannot be null. (Parameter 'source')", người gọi không biết thiếu gì.
/// Validator đổi nó thành `errors` có cấu trúc, nói rõ phải gửi gì.
/// </summary>
public class AddMarketClosuresCommandValidator : AbstractValidator<AddMarketClosuresCommand>
{
    public AddMarketClosuresCommandValidator()
    {
        // NotEmpty một mình phủ cả null và mảng rỗng — thêm NotNull nữa là bắn hai lỗi trùng.
        RuleFor(x => x.Dates)
            .NotEmpty().WithMessage("Phải gửi ít nhất một ngày trong `dates`, dạng YYYY-MM-DD");
    }
}
