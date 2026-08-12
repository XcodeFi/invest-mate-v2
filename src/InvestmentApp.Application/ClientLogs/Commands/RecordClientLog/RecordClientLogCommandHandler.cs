using MediatR;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Application.ClientLogs.Commands.RecordClientLog;

/// <summary>
/// Chỉ ghi một dòng mức Error rồi thôi — không lưu gì, không trạng thái. Serilog quyết định
/// dòng đó đi đâu (console, file, và Telegram nếu đã cấu hình).
/// </summary>
public class RecordClientLogCommandHandler : IRequestHandler<RecordClientLogCommand, Unit>
{
    private readonly ILogger<RecordClientLogCommandHandler> _logger;

    public RecordClientLogCommandHandler(ILogger<RecordClientLogCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task<Unit> Handle(RecordClientLogCommand request, CancellationToken cancellationToken)
    {
        var stack = Truncate(request.Stack, RecordClientLogCommandValidator.MaxStackLength);

        // Chỉ dùng placeholder có tên. Nội suy chuỗi ở đây là đường để một object bị gọi ToString()
        // rồi đổ nguyên nội dung vào tin nhắn gửi ra ngoài.
        _logger.LogError(
            "LỖI TRÌNH DUYỆT | User:{UserId} | Url:{Url} | Context:{Context} | ClientTime:{ClientTimestamp}\n{ClientMessage}\n{ClientStack}",
            request.UserId,
            request.Url,
            request.Context ?? "—",
            request.Timestamp,
            request.Message,
            stack ?? "(không có stack)");

        return Task.FromResult(Unit.Value);
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
