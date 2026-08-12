using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Lời gọi đã làm hỏng phiên 11/08: actionType "Buy" không tồn tại trong ScenarioActionType.
/// Agent chỉ giao tiếp qua MCP nên message nhận được PHẢI đủ để nó tự sửa.
/// </summary>
public class McpBadEnumValueEndToEndTests
{
    private readonly ITestOutputHelper _out;
    public McpBadEnumValueEndToEndTests(ITestOutputHelper output) => _out = output;

    private static async Task<CallToolResult> InvokeRawAsync(string toolName, string argsJson)
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-123") }))
            }
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IMediator>());
        services.AddSingleton(Mock.Of<IFeeCalculationService>());
        services.AddSingleton(Mock.Of<IAiAssistantService>());
        services.AddSingleton<IHttpContextAccessor>(http);
        services.AddMcpServer().WithToolsFromAssembly(typeof(PortfolioTools).Assembly);
        var sp = services.BuildServiceProvider();

        var tool = sp.GetServices<McpServerTool>().First(t => t.ProtocolTool.Name == toolName);
        var ctx = new RequestContext<CallToolRequestParams>(
            Mock.Of<McpServer>(), new JsonRpcRequest { Method = "tools/call" })
        {
            Services = sp,
            Params = new CallToolRequestParams
            {
                Name = toolName,
                Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsJson)
            }
        };

        // Đi qua filter đúng như server: lỗi marshal tham số vỡ TRƯỚC thân tool, gọi
        // InvokeAsync trần sẽ không chạm tới lớp dịch lỗi nào.
        var wrapped = McpErrorTranslator.CallToolFilter((c, t) => tool.InvokeAsync(c, t));
        return await wrapped(ctx, CancellationToken.None);
    }

    [Fact]
    public void Program_Registers_The_Filter_So_The_Pipeline_Is_Not_Bypassed()
    {
        var options = new ModelContextProtocol.Server.McpServerOptions();
        McpErrorTranslator.Configure(options);

        options.Filters.Request.CallToolFilters.Should().HaveCount(1,
            "test trên chỉ chứng minh filter hoạt động; cái này chứng minh nó được cắm vào");
    }

    [Fact]
    public async Task Bad_ActionType_Error_Reaches_The_Agent_Not_The_Generic_Mask()
    {
        // McpException là loại duy nhất SDK giữ nguyên message khi trả về client.
        var act = () => InvokeRawAsync("update_trade_plan",
            """
            {"id":"p1","exitStrategyMode":"Advanced",
             "scenarioNodes":[{"nodeId":"n1","order":1,"label":"x",
               "conditionType":"PriceAbove","conditionValue":24000,"actionType":"Buy"}]}
            """);

        var ex = await act.Should().ThrowAsync<McpException>();
        _out.WriteLine(ex.And.Message);

        ex.And.Message.Should().NotContain("An error occurred invoking",
            "message bị che là mất hoàn toàn đường tự chữa của agent");
        ex.And.Message.Should().Contain("actionType", "agent phải biết field nào sai");
        ex.And.Message.Should().Contain("AddPosition", "và phải biết gửi gì cho đúng");
    }
}
