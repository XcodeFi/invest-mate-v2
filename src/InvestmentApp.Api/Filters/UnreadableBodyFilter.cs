using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InvestmentApp.Api.Filters;

/// <summary>
/// Trả 400 khi input formatter KHÔNG đọc được body, thay vì để tham số <c>[FromBody]</c> về null
/// rồi action deref nó và trả 500 "Object reference not set" — thông báo đó không nói được sai ở đâu.
///
/// <para>
/// Không bật lại <c>SuppressModelStateInvalidFilter</c>: cờ đó tồn tại vì <c>UserId</c>/<c>Id</c>
/// là non-nullable trên command nhưng do controller gán từ JWT/route sau khi bind, nên 400 tự động
/// sẽ chặn mọi request. Filter này chỉ nhận đúng ca formatter thất bại.
/// </para>
///
/// <para>
/// Nhận qua KHOÁ ModelState bắt đầu bằng <c>$.</c> — quy ước JSON path của input formatter. Không
/// nhận qua <see cref="ModelError.Exception"/>: formatter ghi lỗi JSON thành chuỗi
/// <see cref="ModelError.ErrorMessage"/> và để <c>Exception</c> null. Cũng không nhận qua
/// <c>ActionArguments</c>: từ điển đó còn rỗng ở thời điểm này.
/// </para>
/// </summary>
public class UnreadableBodyFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var detail = context.ModelState
            .Where(entry => entry.Key.StartsWith("$", StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        if (detail == null) return;

        context.Result = new BadRequestObjectResult(new ProblemDetails
        {
            Type = "https://httpstatuses.com/400",
            Title = "Không đọc được nội dung yêu cầu",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail,
            Instance = context.HttpContext.Request.Path
        });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
