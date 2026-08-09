using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using InvestmentApp.Api.Middleware;
using InvestmentApp.Application.CompanyDossiers.Gate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestmentApp.Api.Tests.Middleware;

/// <summary>
/// Tests the exception → HTTP status mapping. Critical: FluentValidation's
/// <see cref="ValidationException"/> must map to 400 (not 500), so that explicit
/// <c>validator.ValidateAndThrow()</c> calls inside handlers (e.g. ResolveDecisionCommand)
/// surface as proper 400 ValidationProblemDetails instead of generic 500.
///
/// Bug context: smoke test of POST /api/v1/decisions/{id}/resolve with note &lt; 20 chars
/// returned 500 with detail "Validation failed: ...". FE error toast shows the embedded
/// validation message but the response shape is wrong (missing structured `errors` map).
/// </summary>
public class ExceptionMiddlewareTests
{
    private static async Task<(int Status, string Body)> InvokeAsync(Exception toThrow)
    {
        var middleware = new ExceptionMiddleware(_ => throw toThrow, NullLogger<ExceptionMiddleware>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body, Encoding.UTF8).ReadToEndAsync();
        return (ctx.Response.StatusCode, body);
    }

    [Fact]
    public async Task ValidationException_Returns_400_With_Errors_Dict()
    {
        var failures = new[]
        {
            new ValidationFailure("Note", "Note phải có ít nhất 20 ký tự"),
            new ValidationFailure("Action", "Action không hợp lệ")
        };
        var (status, body) = await InvokeAsync(new ValidationException(failures));

        status.Should().Be((int)HttpStatusCode.BadRequest);

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(400);

        var errors = doc.RootElement.GetProperty("errors");
        errors.GetProperty("Note")[0].GetString().Should().Contain("ít nhất 20 ký tự");
        errors.GetProperty("Action")[0].GetString().Should().Contain("không hợp lệ");
    }

    [Fact]
    public async Task UnauthorizedAccessException_Returns_401()
    {
        var (status, _) = await InvokeAsync(new UnauthorizedAccessException("nope"));
        status.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ArgumentException_Returns_400()
    {
        var (status, _) = await InvokeAsync(new ArgumentException("bad arg"));
        status.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task KeyNotFoundException_Returns_404()
    {
        var (status, _) = await InvokeAsync(new KeyNotFoundException("not found"));
        status.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidOperationException_Returns_409_Conflict()
    {
        var (status, _) = await InvokeAsync(new InvalidOperationException("conflict"));
        status.Should().Be((int)HttpStatusCode.Conflict);
    }

    /// <summary>
    /// DossierGateException kế thừa InvalidOperationException có chủ đích (fail-safe: nếu nhánh
    /// riêng bị xóa thì thoái về 409, không phải 500) — nên nhánh riêng PHẢI đứng trước switch
    /// chung để không rơi vào case InvalidOperationException ở trên. Test này cùng với
    /// <see cref="InvalidOperationException_Returns_409_Conflict"/> chứng minh nhánh mới không
    /// nuốt case InvalidOperationException thường (409 vẫn đúng cho plain InvalidOperationException).
    /// </summary>
    [Fact]
    public async Task DossierGateException_Returns_400_With_StructuredBody()
    {
        var result = new DossierGateResult(false, "insufficient",
            new List<string> { "moats: cần ≥ 1, đang có 0" });
        var (status, body) = await InvokeAsync(new DossierGateException("HPG", result));

        status.Should().Be((int)HttpStatusCode.BadRequest);

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetString().Should().Be("DOSSIER_GATE_FAILED");
        doc.RootElement.GetProperty("symbol").GetString().Should().Be("HPG");
        doc.RootElement.GetProperty("reason").GetString().Should().Be("insufficient");
        doc.RootElement.GetProperty("missing")[0].GetString().Should().Contain("moats");
    }

    [Fact]
    public async Task UnknownException_Returns_500()
    {
        var (status, _) = await InvokeAsync(new ApplicationException("?"));
        status.Should().Be((int)HttpStatusCode.InternalServerError);
    }
}
