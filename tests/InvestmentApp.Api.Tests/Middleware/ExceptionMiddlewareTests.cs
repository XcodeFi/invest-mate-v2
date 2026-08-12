using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using InvestmentApp.Api.Middleware;
using InvestmentApp.Application.CompanyDossiers.Gate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Bảng tổng: mọi loại exception middleware map, kèm status kỳ vọng, trong MỘT chỗ.
    /// Commit 886fd00 phải chèn nhánh riêng cho ValidationException TRƯỚC switch chung vì nó
    /// không kế thừa ArgumentException — phát hiện bằng curl tay, không phải test. File giờ có
    /// 2 nhánh riêng thứ tự được bảo vệ chỉ bằng comment. Bảng này enumerate đủ để một exception
    /// type mới rơi lọt qua nhánh riêng và bị switch chung "nuốt" sai case sẽ lộ ngay, không cần
    /// nhớ viết thêm 1 test riêng.
    /// </summary>
    public static IEnumerable<object[]> ExceptionToStatusCodeMap()
    {
        yield return new object[] { new UnauthorizedAccessException("nope"), (int)HttpStatusCode.Unauthorized };
        yield return new object[] { new ArgumentException("bad arg"), (int)HttpStatusCode.BadRequest };
        yield return new object[] { new KeyNotFoundException("not found"), (int)HttpStatusCode.NotFound };
        // Cặp InvalidOperationException / DossierGateException chứng minh nhánh riêng của
        // DossierGateException (đứng trước switch) không nuốt luôn case InvalidOperationException
        // thường — DossierGateException kế thừa InvalidOperationException có chủ đích.
        yield return new object[] { new InvalidOperationException("conflict"), (int)HttpStatusCode.Conflict };
        yield return new object[]
        {
            new DossierGateException("HPG", DossierGateResult.Fail("insufficient")),
            (int)HttpStatusCode.BadRequest
        };
        // ValidationException: case đã regress một lần (886fd00) vì không kế thừa ArgumentException.
        yield return new object[]
        {
            new ValidationException(new[] { new ValidationFailure("Note", "quá ngắn") }),
            (int)HttpStatusCode.BadRequest
        };
        yield return new object[] { new ApplicationException("?"), (int)HttpStatusCode.InternalServerError };
    }

    [Theory]
    [MemberData(nameof(ExceptionToStatusCodeMap))]
    public async Task Exception_MapsToExpectedStatusCode(Exception toThrow, int expectedStatus)
    {
        var (status, _) = await InvokeAsync(toThrow);
        status.Should().Be(expectedStatus);
    }
}

/// <summary>
/// Mức log quyết định lỗi nào bắn ra Telegram: sink chỉ lấy từ Error trở lên.
///
/// Trước bản sửa, mọi exception đều `LogError`, nên một người dùng gõ sai form cũng đẩy một
/// tin nhắn kèm nguyên stack trace vào kênh cảnh báo. Một kênh báo cả lỗi nhập liệu sẽ bị tắt
/// sau vài ngày — và lúc đó còn tệ hơn không có, vì ta tưởng mình đang được giám sát.
/// </summary>
public class ExceptionMiddlewareLogLevelTests
{
    private sealed class CapturingLogger : ILogger<ExceptionMiddleware>
    {
        public readonly List<LogLevel> Levels = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    private static async Task<(int Status, List<LogLevel> Levels)> InvokeAsync(Exception toThrow)
    {
        var logger = new CapturingLogger();
        var middleware = new ExceptionMiddleware(_ => throw toThrow, logger);
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx);

        return (ctx.Response.StatusCode, logger.Levels);
    }

    [Fact]
    public async Task ValidationException_ghi_muc_Warning_chu_khong_phai_Error()
    {
        var ex = new ValidationException(new[] { new ValidationFailure("Message", "sai rồi") });

        var (status, levels) = await InvokeAsync(ex);

        status.Should().Be(400);
        levels.Should().NotContain(LogLevel.Error);
        levels.Should().Contain(LogLevel.Warning);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), 401)]
    [InlineData(typeof(KeyNotFoundException), 404)]
    [InlineData(typeof(ArgumentException), 400)]
    [InlineData(typeof(InvalidOperationException), 409)]
    public async Task Moi_loi_4xx_deu_la_Warning(Type exceptionType, int expectedStatus)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "lỗi phía người gọi")!;

        var (status, levels) = await InvokeAsync(ex);

        status.Should().Be(expectedStatus);
        levels.Should().NotContain(LogLevel.Error);
    }

    [Fact]
    public async Task Loi_5xx_van_ghi_muc_Error_de_bay_len_Telegram()
    {
        // Đây mới là thứ kênh cảnh báo sinh ra để bắt — không được im.
        var (status, levels) = await InvokeAsync(new NotSupportedException("hỏng thật"));

        status.Should().Be(500);
        levels.Should().Contain(LogLevel.Error);
    }
}

/// <summary>
/// `SuppressModelStateInvalidFilter = true` trong Program.cs nghĩa là body hỏng KHÔNG tự thành 400
/// — nó bind ra null. Controller không chặn thì thành NullReferenceException, tức 500, tức một
/// request méo cũng đủ đẩy một tin nhắn vào kênh cảnh báo.
/// </summary>
public class ClientLogsControllerNullBodyTests
{
    [Fact]
    public async Task Body_null_tra_400_chu_khong_no_thanh_500()
    {
        var controller = new InvestmentApp.Api.Controllers.ClientLogsController(
            new Moq.Mock<MediatR.IMediator>().Object);

        var result = await controller.Record(null, default);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
    }
}
