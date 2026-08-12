using System.Text.Json;
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

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Agent chỉ giao tiếp qua MCP — nó không đọc tài liệu. Tập giá trị hợp lệ phải nằm trong
/// inputSchema mà nó đã nhận ở tools/list, nếu không nó chỉ còn cách đoán.
/// </summary>
public class McpSchemaContractTests
{
    private static JsonElement SchemaOf(string toolName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IMediator>());
        services.AddSingleton(Mock.Of<IFeeCalculationService>());
        services.AddSingleton(Mock.Of<IAiAssistantService>());
        services.AddSingleton(Mock.Of<IHttpContextAccessor>());
        services.AddMcpServer().WithToolsFromAssembly(typeof(PortfolioTools).Assembly);
        var sp = services.BuildServiceProvider();

        var tool = sp.GetServices<McpServerTool>().First(t => t.ProtocolTool.Name == toolName);
        return JsonSerializer.Deserialize<JsonElement>(tool.ProtocolTool.InputSchema.GetRawText());
    }

    private static string?[] EnumValuesOf(JsonElement property)
        => property.GetProperty("enum").EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString())
            .ToArray();

    private static JsonElement ItemProp(string toolName, string arrayParam, string property)
        => SchemaOf(toolName)
            .GetProperty("properties").GetProperty(arrayParam)
            .GetProperty("items").GetProperty("properties").GetProperty(property);

    [Theory]
    [InlineData("create_trade_plan")]
    [InlineData("update_trade_plan")]
    public void ScenarioNode_ActionType_Lists_All_Seven_Values(string toolName)
    {
        EnumValuesOf(ItemProp(toolName, "scenarioNodes", "actionType"))
            .Should().BeEquivalentTo(
                "SellPercent", "SellAll", "MoveStopLoss", "MoveStopToBreakeven",
                "ActivateTrailingStop", "AddPosition", "SendNotification");
    }

    [Theory]
    [InlineData("create_trade_plan")]
    [InlineData("update_trade_plan")]
    public void ScenarioNode_ConditionType_Lists_All_Five_Values(string toolName)
    {
        EnumValuesOf(ItemProp(toolName, "scenarioNodes", "conditionType"))
            .Should().BeEquivalentTo(
                "PriceAbove", "PriceBelow", "PricePercentChange", "TrailingStopHit", "TimeElapsed");
    }

    [Fact]
    public void TrailingStop_Method_Lists_Its_Values()
    {
        EnumValuesOf(ItemProp("create_trade_plan", "scenarioNodes", "trailingStopConfig")
                .GetProperty("properties").GetProperty("method"))
            .Should().BeEquivalentTo("Percentage", "ATR", "FixedAmount");
    }

    [Fact]
    public void ExitTarget_ActionType_Lists_Its_Own_Four_Values()
    {
        EnumValuesOf(ItemProp("create_trade_plan", "exitTargets", "actionType"))
            .Should().BeEquivalentTo("TakeProfit", "CutLoss", "TrailingStop", "PartialExit");
    }

    [Fact]
    public void InvalidationRule_Trigger_Lists_Its_Values()
    {
        EnumValuesOf(ItemProp("create_trade_plan", "invalidationCriteria", "trigger"))
            .Should().Contain("EarningsMiss").And.Contain("TrendBreak");
    }

    [Fact]
    public void PlanLot_Status_Lists_Its_Values()
    {
        EnumValuesOf(ItemProp("create_trade_plan", "lots", "status"))
            .Should().BeEquivalentTo("Pending", "Executed", "Cancelled");
    }

    [Theory]
    [InlineData("create_trade_plan", "timeHorizon", "ShortTerm", "MediumTerm", "LongTerm")]
    [InlineData("update_trade_plan", "timeHorizon", "ShortTerm", "MediumTerm", "LongTerm")]
    [InlineData("create_trade_plan", "exitStrategyMode", "Simple", "Advanced")]
    [InlineData("update_trade_plan", "exitStrategyMode", "Simple", "Advanced")]
    public void Flat_Enum_Param_Lists_Values_And_Stays_Optional(
        string toolName, string param, params string[] expected)
    {
        var schema = SchemaOf(toolName);

        EnumValuesOf(schema.GetProperty("properties").GetProperty(param))
            .Should().BeEquivalentTo(expected);

        // Tham số enum thiếu "= null" bị SDK đẩy vào required — bẫy đã gặp lúc dựng thiết kế.
        var required = schema.TryGetProperty("required", out var r)
            ? r.EnumerateArray().Select(v => v.GetString()).ToArray()
            : Array.Empty<string?>();
        required.Should().NotContain(param);
    }

    [Theory]
    [InlineData("direction", "Buy", "Sell")]
    [InlineData("marketCondition", "Trending", "Ranging")]
    public void String_Param_Without_Domain_Enum_Documents_Values_In_Description(
        string param, params string[] expected)
    {
        // direction/marketCondition là string thuần trên TradePlan — không có enum miền để dùng.
        // AllowedValues chỉ phục vụ completion/complete nên KHÔNG tới được agent đọc schema;
        // thứ thực sự tới được là description.
        var description = SchemaOf("create_trade_plan")
            .GetProperty("properties").GetProperty(param)
            .GetProperty("description").GetString();

        foreach (var value in expected) description.Should().Contain(value);
    }

    [Fact]
    public void ActionType_Carries_A_Human_Description()
    {
        ItemProp("create_trade_plan", "scenarioNodes", "actionType")
            .GetProperty("description").GetString()
            .Should().Contain("AddPosition");
    }

    // --- Lịch nghỉ giao dịch (T+2) ---

    private static string?[] RequiredOf(string toolName)
    {
        var schema = SchemaOf(toolName);
        return schema.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(v => v.GetString()).ToArray()
            : Array.Empty<string?>();
    }

    [Fact]
    public void AddMarketClosures_Chi_Bat_Buoc_dates_Con_note_La_Tuy_Chon()
    {
        // Tham số nullable KHÔNG tự thành optional trong schema: phải nằm sau ct và có `= null`.
        // Thiếu điều đó thì agent buộc phải gửi note mới gọi được.
        RequiredOf("add_market_closures").Should().BeEquivalentTo("dates");
    }

    [Fact]
    public void AddMarketClosures_Nhan_Mang_Chuoi_Chu_Khong_Phai_Object_Boc_Ngoai()
    {
        var dates = SchemaOf("add_market_closures").GetProperty("properties").GetProperty("dates");

        dates.GetProperty("type").GetString().Should().Be("array");
        dates.GetProperty("items").GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void AddMarketClosures_Description_Noi_Ro_Dinh_Dang_Ngay()
    {
        SchemaOf("add_market_closures")
            .GetProperty("properties").GetProperty("dates")
            .GetProperty("description").GetString()
            .Should().Contain("YYYY-MM-DD");
    }

    [Theory]
    [InlineData("list_market_closures", "year")]
    [InlineData("remove_market_closure", "date")]
    public void Tool_Lich_Nghi_Bat_Buoc_Dung_Mot_Tham_So(string toolName, string param)
    {
        RequiredOf(toolName).Should().BeEquivalentTo(param);
    }
}
