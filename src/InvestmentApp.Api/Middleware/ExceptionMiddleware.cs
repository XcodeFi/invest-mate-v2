using FluentValidation;
using InvestmentApp.Application.CompanyDossiers.Gate;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace InvestmentApp.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var status = await HandleExceptionAsync(context, ex);

            // Mức log bám theo LỚP MÃ TRẠNG THÁI, không bám theo kiểu exception — cùng một phép
            // ánh xạ đã dùng để trả lời client, nên hai chỗ không thể lệch nhau.
            //
            // 4xx là người gọi gửi sai: đã trả lời họ rồi, không còn việc gì để ai làm. Ghi mức
            // Error là đẩy nó lên kênh cảnh báo, và một kênh báo cả lỗi nhập liệu sẽ bị tắt sau
            // vài ngày — lúc đó còn tệ hơn không có, vì ta tưởng mình đang được giám sát.
            if (status >= 500)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
            }
            else
            {
                _logger.LogWarning(
                    "Request rejected with {StatusCode}: {ExceptionType} — {ExceptionMessage}",
                    status, ex.GetType().Name, ex.Message);
            }
        }
    }

    /// <returns>Mã trạng thái đã ghi vào response — để chỗ gọi chọn mức log theo đúng nó.</returns>
    private static async Task<int> HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // Đặt TRƯỚC switch chung: switch map InvalidOperationException → 409,
        // còn gate cần 400 kèm body có cấu trúc để FE liệt kê được thiếu gì.
        if (exception is DossierGateException dge)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "DOSSIER_GATE_FAILED",
                symbol = dge.Symbol,
                reason = dge.Result.Reason,
                missing = dge.Result.Missing
            });
            return context.Response.StatusCode;
        }

        // FluentValidation ValidationException: surface as 400 ValidationProblemDetails so FE can
        // parse the structured `errors` map. Keep this branch BEFORE the generic switch since
        // ValidationException inherits from Exception, not from ArgumentException.
        if (exception is ValidationException ve)
        {
            var errors = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var validationProblem = new ValidationProblemDetails(errors)
            {
                Type = "https://httpstatuses.com/400",
                Title = "Validation Failed",
                Status = (int)HttpStatusCode.BadRequest,
                Instance = context.Request.Path
            };

            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsJsonAsync(validationProblem);
            return context.Response.StatusCode;
        }

        var statusCode = exception switch
        {
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            InvalidOperationException => (int)HttpStatusCode.Conflict,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var title = exception switch
        {
            UnauthorizedAccessException => "Unauthorized",
            ArgumentException => "Bad Request",
            KeyNotFoundException => "Not Found",
            InvalidOperationException => "Conflict",
            _ => "An error occurred"
        };

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path,
            Status = statusCode
        };

        context.Response.StatusCode = problem.Status.Value;

        await context.Response.WriteAsJsonAsync(problem);
        return context.Response.StatusCode;
    }
}
