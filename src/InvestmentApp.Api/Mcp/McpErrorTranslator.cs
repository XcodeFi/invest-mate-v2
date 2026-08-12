using System.Text.Json;
using FluentValidation;
using InvestmentApp.Application.CompanyDossiers.Gate;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// SDK che mọi exception của tool thành "An error occurred invoking '&lt;tên tool&gt;'." — chỉ
/// <see cref="McpException"/> đi xuyên qua được. Agent chỉ giao tiếp qua MCP: nó không đọc tài
/// liệu, nên message bị che là mất hoàn toàn đường tự chữa và chỉ còn cách đoán.
///
/// <para>
/// Điểm móc là filter cấp server, KHÔNG phải bọc thân từng tool: giá trị enum sai vỡ ở bước SDK
/// marshal tham số (<c>AIFunctionFactory.GetParameterMarshaller</c>), tức trước khi thân tool
/// chạy. Bọc thân tool không nằm trên đường đi của đúng ca lỗi cần đỡ nhất.
/// </para>
/// </summary>
internal static class McpErrorTranslator
{
    /// <summary>
    /// Gọi từ <c>AddMcpServer</c>. Là phương thức có tên (không phải lambda tại chỗ) để test
    /// khẳng định được việc đăng ký mà không cần dựng host.
    /// </summary>
    internal static void Configure(McpServerOptions options)
        => options.Filters.Request.CallToolFilters.Add(CallToolFilter);

    internal static McpRequestHandler<CallToolRequestParams, CallToolResult> CallToolFilter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (context, ct) =>
        {
            try
            {
                return await next(context, ct);
            }
            // Huỷ không phải lỗi của tool: bọc nó thành McpException khiến agent đọc là "gọi thất
            // bại" rồi gọi lại, dù thao tác có thể đã xong.
            catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
            {
                throw new McpException(Describe(ex));
            }
        };

    internal static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
        {
            throw new McpException(Describe(ex));
        }
    }

    internal static async Task RunAsync(Func<Task> action)
        => await RunAsync<object?>(async () => { await action(); return null; });

    private const string InfrastructureFailure =
        "Lỗi hạ tầng phía máy chủ, không phải do tham số. Thử lại sau; nếu lặp lại thì báo người dùng.";

    private static string Describe(Exception ex) => ex switch
    {
        DossierGateException gate => McpDossierGate.Describe(gate),
        ValidationException ve => "Dữ liệu không hợp lệ: " + string.Join(" | ",
            ve.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")),
        JsonException je => DescribeJson(je),
        ArgumentException ae => "Tham số sai: " + ae.Message,
        InvalidOperationException ioe => ioe.Message,
        _ when IsInfrastructure(ex) => InfrastructureFailure,
        _ => ex.Message
    };

    /// <summary>
    /// Lỗi hạ tầng mang chi tiết nội bộ (host, cluster, cổng) và agent không làm gì được với nó.
    /// Mặc định vẫn là trả nguyên văn message, vì nhiều lỗi nghiệp vụ hữu ích được ném bằng
    /// <see cref="Exception"/> trần (ví dụ "Trade plan &lt;id&gt; not found") — bịt hết là bịt luôn
    /// thông tin agent cần. Kiểm theo namespace để không phải tham chiếu driver từ lớp Api.
    /// </summary>
    private static bool IsInfrastructure(Exception ex)
        => ex is TimeoutException
           || ex.GetType().Namespace?.StartsWith("MongoDB", StringComparison.Ordinal) == true;

    /// <summary>
    /// STJ chỉ nói "không chuyển được sang &lt;type&gt;" — đủ để biết sai ở đâu, không đủ để biết
    /// gửi gì cho đúng. Enum nào đọc được tên thì nối luôn tập giá trị vào, để agent tự sửa
    /// trong một lượt thay vì đoán từng tên một.
    /// </summary>
    private static string DescribeJson(JsonException ex)
    {
        var text = "Không đọc được tham số: " + ex.Message;
        var values = AllowedValuesFor(ex.Message);
        return values == null ? text : $"{text} Giá trị hợp lệ: {values}.";
    }

    private static string? AllowedValuesFor(string message)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            message, @"converted to (?:System\.Nullable`1\[)?([A-Za-z0-9_.]+)");
        if (!match.Success) return null;

        var type = typeof(Domain.Entities.TradePlan).Assembly.GetType(match.Groups[1].Value);
        return type is { IsEnum: true } ? string.Join(", ", Enum.GetNames(type)) : null;
    }
}
