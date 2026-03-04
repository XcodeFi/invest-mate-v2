using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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

        var statusCode = exception switch
        {
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var problem = new ProblemDetails
        {
            Type = statusCode == 401
                ? "https://tools.ietf.org/html/rfc7235#section-3.1"
                : "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = statusCode == 401 ? "Unauthorized" : "An error occurred",
            Detail = exception.Message,
            Instance = context.Request.Path,
            Status = statusCode
        };

        context.Response.StatusCode = problem.Status.Value;

        await context.Response.WriteAsJsonAsync(problem);
    }
}