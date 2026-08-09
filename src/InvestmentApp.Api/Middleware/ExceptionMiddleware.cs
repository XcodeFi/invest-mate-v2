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
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
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
            return;
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
            return;
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
    }
}