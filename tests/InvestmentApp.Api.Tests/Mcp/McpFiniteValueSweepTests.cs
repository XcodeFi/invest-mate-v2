using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Không dựa vào việc người viết tool tiếp theo nhớ ra. Mọi tham số có tập giá trị hữu hạn phải
/// tự khai tập đó trong inputSchema — bằng mảng "enum", hoặc bằng description có liệt kê. Agent
/// chỉ giao tiếp qua MCP: cái gì không nằm trong schema thì với nó là không tồn tại.
/// </summary>
public class McpFiniteValueSweepTests
{
    private readonly ITestOutputHelper _out;
    public McpFiniteValueSweepTests(ITestOutputHelper output) => _out = output;

    /// <summary>Tên property gợi ý một tập giá trị hữu hạn.</summary>
    private static readonly Regex FiniteValueName =
        new("(Type|Mode|Status|Trigger|Method|Horizon|Direction|Condition)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Không phải tập hữu hạn dù tên khớp — liệt kê tường minh để khỏi phải nới luật.</summary>
    private static readonly HashSet<string> NotFiniteValued = new(StringComparer.OrdinalIgnoreCase)
    {
        "conditionNote",   // văn bản tự do
        "conditionValue"   // số
    };

    /// <summary>
    /// Service tiêm vào, không phải tham số của tool. Bỏ qua cả nhánh con: dựng container MCP
    /// thứ hai trong cùng process làm SDK sinh schema có nhồi đồ thị HttpContext vào đây, nên
    /// đi vào nhánh này là để một quirk trạng thái tĩnh quyết định kết quả guard. Việc "service
    /// không được lọt vào schema" đã có guard riêng trong McpToolDiscoveryTests.
    /// </summary>
    private static readonly HashSet<string> InjectedServiceParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "mediator", "feeService", "ct", "aiAssistant"
    };

    private static IEnumerable<McpServerTool> Tools()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IMediator>());
        services.AddSingleton(Mock.Of<IFeeCalculationService>());
        services.AddSingleton(Mock.Of<IAiAssistantService>());
        services.AddSingleton(Mock.Of<IHttpContextAccessor>());
        services.AddMcpServer().WithToolsFromAssembly(typeof(PortfolioTools).Assembly);
        return services.BuildServiceProvider().GetServices<McpServerTool>();
    }

    [Fact]
    public void Every_Finite_Value_Property_Declares_Its_Values()
    {
        var offenders = new List<string>();

        foreach (var tool in Tools())
        {
            using var doc = JsonDocument.Parse(tool.ProtocolTool.InputSchema.GetRawText());
            Walk(doc.RootElement, tool.ProtocolTool.Name, "", offenders);
        }

        foreach (var offender in offenders) _out.WriteLine(offender);

        offenders.Should().BeEmpty(
            "tham số có tập giá trị hữu hạn phải tự khai tập đó — agent không đọc tài liệu");
    }

    private static void Walk(JsonElement node, string tool, string path, List<string> offenders)
    {
        if (node.ValueKind != JsonValueKind.Object) return;

        if (node.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (InjectedServiceParams.Contains(property.Name)) continue;

                var childPath = path.Length == 0 ? property.Name : $"{path}.{property.Name}";

                if (FiniteValueName.IsMatch(property.Name)
                    && !NotFiniteValued.Contains(property.Name)
                    && !Declares(property.Value))
                {
                    offenders.Add($"{tool}: {childPath} — không có \"enum\" và description không liệt kê giá trị");
                }

                Walk(property.Value, tool, childPath, offenders);
            }
        }

        if (node.TryGetProperty("items", out var items)) Walk(items, tool, path, offenders);
    }

    /// <summary>
    /// Có mảng enum (dạng mạnh, máy đọc được), hoặc description liệt kê giá trị. Bản liệt kê
    /// bằng văn xuôi chỉ là heuristic — nhận đúng ba dấu phân tách mà repo đang dùng thật
    /// (`,` `/` `hoặc`) chứ không đoán rộng hơn.
    /// </summary>
    private static bool Declares(JsonElement property)
    {
        if (property.ValueKind != JsonValueKind.Object) return false;
        if (property.TryGetProperty("enum", out _)) return true;

        return property.TryGetProperty("description", out var description)
               && description.GetString() is { } text
               && (text.Contains(',') || text.Contains('/') || text.Contains(" hoặc "));
    }
}
