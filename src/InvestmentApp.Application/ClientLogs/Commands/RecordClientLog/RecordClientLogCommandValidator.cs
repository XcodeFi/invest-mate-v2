using FluentValidation;

namespace InvestmentApp.Application.ClientLogs.Commands.RecordClientLog;

/// <summary>
/// Chốt cuối trước khi nội dung do trình duyệt gửi lên đi vào log — mà log thì chuyển tiếp ra
/// một dịch vụ bên ngoài (Telegram). Người gửi là chính chủ tài khoản, nên đây không phải hàng
/// rào chống kẻ tấn công; nó chặn ca frontend lỡ dump nguyên header hoặc object cấu hình vào
/// message, thứ mà sau đó không thu hồi được.
/// </summary>
public class RecordClientLogCommandValidator : AbstractValidator<RecordClientLogCommand>
{
    public const int MaxMessageLength = 500;
    public const int MaxUrlLength = 500;
    public const int MaxStackLength = 1000;

    /// <summary>
    /// Chặn theo TÊN TRƯỜNG, không phải theo giá trị. Che giá trị bằng regex thì tỷ lệ báo nhầm
    /// cao hơn lợi ích; còn tên trường xuất hiện trong một câu lỗi gần như luôn nghĩa là có ai đó
    /// đang in cả object ra.
    /// </summary>
    private static readonly string[] ForbiddenTerms =
    {
        "password", "passwd", "token", "secret", "apikey", "api_key",
        "authorization", "email", "pin",
    };

    public RecordClientLogCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Nội dung lỗi không được rỗng")
            .MaximumLength(MaxMessageLength);

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Đường dẫn không được rỗng")
            .MaximumLength(MaxUrlLength);

        RuleFor(x => x.Timestamp).NotEmpty();
        RuleFor(x => x.UserAgent).MaximumLength(200);
        RuleFor(x => x.Context).MaximumLength(100);

        // Stack chỉ cảnh báo độ dài ở đây cho rõ ý; handler vẫn cắt lần nữa vì đó mới là chỗ
        // duy nhất chắc chắn chạy trước khi ghi log.
        RuleFor(x => x.Stack)
            .MaximumLength(20_000)
            .When(x => x.Stack != null);

        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            var haystack = $"{cmd.Message} {cmd.Url}";
            var hit = ForbiddenTerms.FirstOrDefault(t =>
                haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
                ctx.AddFailure("Message", $"Nội dung chứa từ khoá không được phép ({hit})");
        });
    }
}
