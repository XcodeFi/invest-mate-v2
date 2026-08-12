using MediatR;

namespace InvestmentApp.Application.ClientLogs.Commands.RecordClientLog;

/// <summary>
/// Lỗi chưa bắt được ở trình duyệt, đẩy về server để lọt vào cùng một đường log với lỗi backend.
/// Frontend KHÔNG gọi Telegram trực tiếp: bot token sẽ nằm trong bundle JS mà ai cũng đọc được.
/// </summary>
public class RecordClientLogCommand : IRequest<Unit>
{
    /// <summary>Server tự gán từ JWT, client không gửi lên.</summary>
    public string UserId { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Stack { get; set; }

    /// <summary>Chỉ đường dẫn, không kèm query string.</summary>
    public string Url { get; set; } = string.Empty;

    public string? UserAgent { get; set; }

    public string Timestamp { get; set; } = string.Empty;

    /// <summary>Nhãn do lập trình viên đặt, VD "Angular ErrorHandler". Không bao giờ chứa nội dung người dùng.</summary>
    public string? Context { get; set; }
}
