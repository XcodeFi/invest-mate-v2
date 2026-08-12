using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace InvestmentApp.Api.Logging;

/// <summary>
/// Gắn id người dùng (claim <c>sub</c>) vào mỗi dòng log. Cố ý chỉ lấy id mờ, KHÔNG lấy email —
/// log ở đây được chuyển tiếp ra một dịch vụ bên ngoài.
///
/// Mọi thứ bọc trong try/catch: đường ghi log không bao giờ được ném, vì nó hay được gọi từ
/// bên trong khối xử lý lỗi — ném ở đó là biến một lỗi đọc được thành một lỗi khác che mất nó.
/// </summary>
public class UserIdEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _accessor;

    public UserIdEnricher(IHttpContextAccessor accessor) => _accessor = accessor;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        string userId;
        try
        {
            userId = _accessor.HttpContext?.User?.FindFirst("sub")?.Value ?? "—";
        }
        catch
        {
            userId = "—";
        }

        logEvent.AddPropertyIfAbsent(factory.CreateProperty("UserId", userId));
    }
}

/// <summary>
/// Gắn method + đường dẫn. KHÔNG đọc query string (có thể mang mã, id, tham số lọc) và KHÔNG
/// đọc body.
/// </summary>
public class RequestPathEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _accessor;

    public RequestPathEnricher(IHttpContextAccessor accessor) => _accessor = accessor;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        string method = "—", path = "—";
        try
        {
            var request = _accessor.HttpContext?.Request;
            if (request != null)
            {
                method = request.Method;
                path = request.Path.Value ?? "—";
            }
        }
        catch
        {
            // giữ giá trị mặc định
        }

        logEvent.AddPropertyIfAbsent(factory.CreateProperty("HttpMethod", method));
        logEvent.AddPropertyIfAbsent(factory.CreateProperty("HttpPath", path));
    }
}
