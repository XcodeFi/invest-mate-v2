# Hợp đồng MCP tự mô tả — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Đưa tập giá trị hợp lệ của mọi tham số MCP vào chính `inputSchema` agent nhận được, và làm mọi lỗi phía tool đi xuyên qua được lớp che của SDK.

**Architecture:** Đổi các field có tập giá trị hữu hạn từ `string` sang enum miền thật (SDK tự sinh `"enum":[…]` trong schema) kèm `[Description]` mang ngữ nghĩa. Thêm một helper dịch exception dùng chung cho mọi write tool, thay `McpDossierGate` đang tồn tại riêng lẻ. Bỏ câu `if` phòng thủ đang khiến luật domain không bao giờ chạy tới.

**Tech Stack:** .NET 9, `ModelContextProtocol.AspNetCore` 2.0.0-rc.1, MediatR, FluentValidation, xUnit + FluentAssertions + Moq.

**Spec:** [`docs/superpowers/specs/done/2026-08-11-mcp-self-describing-contract-design.md`](../../specs/done/2026-08-11-mcp-self-describing-contract-design.md)

## Global Constraints

- Nhánh: `fix/mcp-scenario-contract`, cắt từ `origin/master` (đã tạo). Không commit thẳng lên `master`.
- Backend-only. **Không** sửa file nào trong `frontend/`. Kiểu TypeScript của FE khai `string`; enum tuần tự hóa thành chuỗi nên hợp đồng dây không đổi.
- Commit message tiếng Việt có dấu đầy đủ, giữ prefix conventional-commit tiếng Anh.
- Không có `Co-Authored-By` trailer.
- Tham số MCP tùy chọn **bắt buộc** khai `= null`. Thiếu `= null` thì SDK đẩy tham số vào `"required"` của schema.
- Field mang **quyết định** (`actionType`, `conditionType`) khai nullable + validator `NotNull`. Field mang **đơn vị đo** (`method`) khai nullable + mặc định trong handler.
- Không mở rộng `ScenarioActionType`. Enum đã đủ diễn đạt mọi ý định.
- Chạy `dotnet test` trước mỗi commit.

## File Structure

| File | Trách nhiệm | Thay đổi |
|---|---|---|
| `src/InvestmentApp.Api/Mcp/McpErrorTranslator.cs` | Dịch mọi exception của write tool sang `McpException` | **Tạo mới** |
| `src/InvestmentApp.Api/Mcp/McpDossierGate.cs` | Mô tả lỗi cổng hồ sơ | Rút còn phần `Describe`, `GuardAsync` chuyển sang translator |
| `src/InvestmentApp.Api/Mcp/TradePlanTools.cs` | Khai báo tool kế hoạch giao dịch | Đổi kiểu tham số phẳng, thêm `[Description]`/`[AllowedValues]`, bọc translator |
| `src/InvestmentApp.Application/TradePlans/Queries/GetTradePlans/GetTradePlansQuery.cs` | Định nghĩa `ScenarioNodeDto`, `TrailingStopConfigDto`, `ExitTargetDto`, `PlanLotDto` + mapping ra DTO | Đổi kiểu property sang enum, thêm `[Description]`, bỏ `.ToString()` |
| `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommand.cs` | `InvalidationRuleDto`, `MapToScenarioNode`, handler tạo | Bỏ `Enum.Parse`, bỏ nhánh im lặng |
| `src/InvestmentApp.Application/TradePlans/Commands/UpdateTradePlan/UpdateTradePlanCommand.cs` | Handler sửa | Bỏ `Enum.Parse`, bỏ vế `&& Advanced` |
| `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommandValidator.cs` | Luật server-side | Thêm rule `NotNull` cho `actionType`/`conditionType` |
| `tests/InvestmentApp.Api.Tests/Mcp/McpErrorSurfaceTests.cs` | Chứng minh lỗi đi xuyên qua SDK | **Tạo mới** |
| `tests/InvestmentApp.Api.Tests/Mcp/McpSchemaContractTests.cs` | Chứng minh schema tự mô tả | **Tạo mới** |

---

### Task 1: Filter dịch lỗi cấp server

Lý do đi trước: mọi task sau đều cần message đi ra được ngoài mới kiểm chứng được.

> **Đã hiệu chỉnh khi thực thi.** Bản đầu của task này chỉ bọc thân từng tool. Sau khi Task 3 siết
> kiểu, giá trị enum sai vỡ ở bước SDK marshal tham số (`AIFunctionFactory.GetParameterMarshaller`),
> tức trước khi thân tool chạy — helper bọc-thân không nằm trên đường đi. Điểm móc đúng là
> `McpServerOptions.Filters.Request.CallToolFilters`, đăng ký qua `AddMcpServer(McpErrorTranslator.Configure)`.
> Chữ ký: `McpRequestFilter<CallToolRequestParams, CallToolResult>` = `next → handler`.
> Helper bọc thân vẫn giữ cho lỗi phát sinh trong thân tool.

**Files:**
- Create: `src/InvestmentApp.Api/Mcp/McpErrorTranslator.cs`
- Modify: `src/InvestmentApp.Api/Mcp/McpDossierGate.cs`
- Modify: `src/InvestmentApp.Api/Mcp/TradePlanTools.cs`
- Test: `tests/InvestmentApp.Api.Tests/Mcp/McpErrorSurfaceTests.cs`

**Interfaces:**
- Produces: `InvestmentApp.Api.Mcp.McpErrorTranslator.RunAsync<T>(Func<Task<T>> action)` → `Task<T>`; `McpErrorTranslator.RunAsync(Func<Task> action)` → `Task`. Task 2–5 gọi hai overload này thay cho `McpDossierGate.GuardAsync`.

- [ ] **Step 1: Viết test đỏ**

Tạo `tests/InvestmentApp.Api.Tests/Mcp/McpErrorSurfaceTests.cs`:

```csharp
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using InvestmentApp.Api.Mcp;
using ModelContextProtocol;
using Xunit;

namespace InvestmentApp.Api.Tests.Mcp;

public class McpErrorSurfaceTests
{
    [Fact]
    public async Task ValidationException_Becomes_McpException_Keeping_Message()
    {
        var failure = new ValidationFailure("ScenarioNodes[0].ActionType",
            "actionType bắt buộc, một trong: SellPercent, SellAll");

        var act = () => McpErrorTranslator.RunAsync<string>(
            () => throw new ValidationException(new[] { failure }));

        var ex = await act.Should().ThrowAsync<McpException>();
        ex.And.Message.Should().Contain("ScenarioNodes[0].ActionType");
        ex.And.Message.Should().Contain("SellPercent");
    }

    [Fact]
    public async Task JsonException_Becomes_McpException_Keeping_Path()
    {
        var act = () => McpErrorTranslator.RunAsync<string>(
            () => throw new System.Text.Json.JsonException(
                "The JSON value could not be converted to ScenarioActionType. Path: $.scenarioNodes[0].actionType"));

        var ex = await act.Should().ThrowAsync<McpException>();
        ex.And.Message.Should().Contain("$.scenarioNodes[0].actionType");
    }

    [Fact]
    public async Task InvalidOperationException_Becomes_McpException_Keeping_Message()
    {
        var act = () => McpErrorTranslator.RunAsync<string>(
            () => throw new InvalidOperationException("Cannot set scenario nodes in Simple mode"));

        var ex = await act.Should().ThrowAsync<McpException>();
        ex.And.Message.Should().Contain("Simple mode");
    }

    [Fact]
    public async Task Success_Passes_Through_Untouched()
    {
        var result = await McpErrorTranslator.RunAsync(() => Task.FromResult("ok"));
        result.Should().Be("ok");
    }
}
```

- [ ] **Step 2: Chạy để xác nhận đỏ**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter "FullyQualifiedName~McpErrorSurfaceTests"`
Expected: FAIL — `error CS0103: The name 'McpErrorTranslator' does not exist`.

- [ ] **Step 3: Hiện thực tối thiểu**

Tạo `src/InvestmentApp.Api/Mcp/McpErrorTranslator.cs`:

```csharp
using System.Text.Json;
using FluentValidation;
using InvestmentApp.Application.CompanyDossiers.Gate;
using ModelContextProtocol;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// SDK che mọi exception của tool thành "An error occurred invoking '&lt;tên tool&gt;'." — chỉ
/// <see cref="McpException"/> đi xuyên qua được. Agent chỉ giao tiếp qua MCP nên message bị che
/// là mất hoàn toàn đường tự chữa.
/// </summary>
internal static class McpErrorTranslator
{
    internal static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not McpException)
        {
            throw new McpException(Describe(ex));
        }
    }

    internal static async Task RunAsync(Func<Task> action)
        => await RunAsync<object?>(async () => { await action(); return null; });

    private static string Describe(Exception ex) => ex switch
    {
        DossierGateException gate => McpDossierGate.Describe(gate),
        ValidationException ve => "Dữ liệu không hợp lệ: " + string.Join(" | ",
            ve.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")),
        JsonException je => "Không đọc được tham số: " + je.Message,
        ArgumentException ae => "Tham số sai: " + ae.Message,
        InvalidOperationException ioe => ioe.Message,
        _ => ex.Message
    };
}
```

- [ ] **Step 4: Chuyển `McpDossierGate` thành nơi mô tả, không còn tự bắt**

Trong `src/InvestmentApp.Api/Mcp/McpDossierGate.cs`: xóa `GuardAsync`, đổi `private static string Describe` thành `internal static string Describe`. Giữ nguyên toàn bộ nội dung `Describe` và hằng `ReCheck`.

Cập nhật XML doc của lớp thành:

```csharp
/// <summary>
/// Mô tả <see cref="DossierGateException"/> thành câu chỉ đúng cách chữa cho agent.
/// Việc bắt và dịch sang <see cref="McpException"/> do <see cref="McpErrorTranslator"/> lo.
/// </summary>
```

- [ ] **Step 5: Đổi chỗ gọi trong `TradePlanTools`**

Trong `src/InvestmentApp.Api/Mcp/TradePlanTools.cs` đổi `McpDossierGate.GuardAsync(` thành `McpErrorTranslator.RunAsync(` ở cả `CreateTradePlan` và `UpdateTradePlan`. Bọc thêm `SetTradePlanStatus`:

```csharp
        if (string.Equals(status, "restore", StringComparison.OrdinalIgnoreCase))
            throw new McpException("restore không được phép qua MCP surface.");
        await McpErrorTranslator.RunAsync(() => mediator.Send(new UpdateTradePlanStatusCommand
        {
            Id = id, UserId = http.GetUserId(), Status = status, TradeId = tradeId
        }, ct));
        return "ok";
```

Thêm `using ModelContextProtocol;` vào đầu file nếu chưa có.

- [ ] **Step 6: Tìm mọi chỗ gọi `GuardAsync` còn lại và đổi hết**

Run: `grep -rn "McpDossierGate.GuardAsync" src/ --include=*.cs`
Expected: không còn kết quả nào sau khi đổi. Nếu còn (ví dụ `CompanyDossierTools.cs`), đổi sang `McpErrorTranslator.RunAsync`.

- [ ] **Step 7: Chạy test**

Run: `dotnet test tests/InvestmentApp.Api.Tests`
Expected: PASS, không hồi quy test cổng hồ sơ nào.

- [ ] **Step 8: Commit**

```bash
git add src/InvestmentApp.Api/Mcp/ tests/InvestmentApp.Api.Tests/Mcp/McpErrorSurfaceTests.cs
git commit -m "fix(mcp): gỡ lớp che lỗi, mọi exception của tool ra được message thật"
```

---

### Task 2: Bỏ guard đang bóp chết luật domain

**Files:**
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/UpdateTradePlan/UpdateTradePlanCommand.cs:183`
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommand.cs:163-171`
- Test: `tests/InvestmentApp.Application.Tests/TradePlans/Commands/ScenarioNodeModeGuardTests.cs`

**Interfaces:**
- Consumes: `McpErrorTranslator.RunAsync` từ Task 1 (để message `InvalidOperationException` ra tới agent).
- Produces: không có API mới.

- [ ] **Step 1: Viết test đỏ**

Tạo `tests/InvestmentApp.Application.Tests/TradePlans/Commands/ScenarioNodeModeGuardTests.cs`:

```csharp
using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

public class ScenarioNodeModeGuardTests
{
    private static TradePlan SimplePlan()
        => new("u1", "ANV", "Buy", 20000m, 18000m, 26000m, 800, "Trending", 5);

    private static ScenarioNodeDto Node() => new()
    {
        NodeId = "n1",
        Order = 1,
        Label = "Chốt 1/2",
        ConditionType = ScenarioConditionType.PriceAbove,
        ConditionValue = 24000m,
        ActionType = ScenarioActionType.SellPercent,
        ActionValue = 50m
    };

    [Fact]
    public async Task Sending_Nodes_To_Simple_Plan_Throws_Instead_Of_Dropping_Silently()
    {
        var plan = SimplePlan();
        var repo = new Mock<ITradePlanRepository>();
        repo.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        var handler = new UpdateTradePlanCommandHandler(repo.Object, Mock.Of<ICompanyDossierGate>());

        var act = () => handler.Handle(new UpdateTradePlanCommand
        {
            Id = "p1", UserId = "u1", ScenarioNodes = new List<ScenarioNodeDto> { Node() }
        }, default);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .And.Message.Should().Contain("Simple");
    }

    [Fact]
    public async Task Plan_Already_Advanced_Still_Accepts_Nodes_Alone()
    {
        var plan = SimplePlan();
        plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
        var repo = new Mock<ITradePlanRepository>();
        repo.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        var handler = new UpdateTradePlanCommandHandler(repo.Object, Mock.Of<ICompanyDossierGate>());

        await handler.Handle(new UpdateTradePlanCommand
        {
            Id = "p1", UserId = "u1", ScenarioNodes = new List<ScenarioNodeDto> { Node() }
        }, default);

        plan.ScenarioNodes.Should().HaveCount(1);
    }
}
```

**Lưu ý cho người hiện thực:** chữ ký constructor của `TradePlan` và của `UpdateTradePlanCommandHandler` phải đọc từ file thật trước khi chạy — nếu lệch, sửa test theo chữ ký thật, **không** sửa production code cho khớp test.

- [ ] **Step 2: Chạy để xác nhận đỏ**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~ScenarioNodeModeGuardTests"`
Expected: test thứ nhất FAIL — không có exception nào được ném (đây chính là bug: rơi im lặng). Test thứ hai PASS.

- [ ] **Step 3: Bỏ vế bóp chết luật ở đường sửa**

Trong `UpdateTradePlanCommand.cs`, đổi:

```csharp
        if (request.ScenarioNodes != null && plan.ExitStrategyMode == ExitStrategyMode.Advanced)
```

thành:

```csharp
        // Không tự lọc theo chế độ: TradePlan.SetScenarioNodes đã có luật "Simple thì không nhận
        // node". Thêm vế lọc ở đây khiến luật đó không bao giờ chạy tới, nodes rơi im lặng mà
        // tool vẫn trả "ok".
        if (request.ScenarioNodes != null)
```

- [ ] **Step 4: Bỏ nhánh im lặng ở đường tạo**

Trong `CreateTradePlanCommand.cs` đổi khối Scenario Playbook thành:

```csharp
        // Scenario Playbook
        if (request.ExitStrategyMode?.Equals("Advanced", StringComparison.OrdinalIgnoreCase) == true)
            plan.SetExitStrategyMode(ExitStrategyMode.Advanced);

        if (request.ScenarioNodes is { Count: > 0 })
            plan.SetScenarioNodes(request.ScenarioNodes.Select(MapToScenarioNode).ToList());
```

- [ ] **Step 5: Chạy test**

Run: `dotnet test tests/InvestmentApp.Application.Tests`
Expected: PASS toàn bộ. Nếu có test cũ dựa vào hành vi rơi im lặng, đọc kỹ test đó — nếu nó khẳng định "gửi nodes ở chế độ Simple thì bị bỏ qua" thì nó đang ghim chính cái bug này, sửa nó thành khẳng định ném lỗi.

- [ ] **Step 6: Commit**

```bash
git add src/InvestmentApp.Application/TradePlans/Commands/ tests/InvestmentApp.Application.Tests/TradePlans/Commands/ScenarioNodeModeGuardTests.cs
git commit -m "fix(trade-plan): scenarioNodes ở chế độ Simple báo lỗi thay vì rơi im lặng"
```

---

### Task 3: `ScenarioNodeDto` + `TrailingStopConfigDto` dùng enum thật

Đây là task chữa đúng sự cố đã xảy ra.

**Files:**
- Modify: `src/InvestmentApp.Application/TradePlans/Queries/GetTradePlans/GetTradePlansQuery.cs:249-274` (định nghĩa DTO) và `:105-125` (mapping ra DTO)
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommand.cs:248-266` (`MapToScenarioNode`)
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommandValidator.cs`
- Modify: `src/InvestmentApp.Application/TradePlans/Queries/GetScenarioHistory/GetScenarioHistoryQuery.cs:68-73`
- Modify: `src/InvestmentApp.Application/TradePlans/Queries/GetScenarioTemplates/GetScenarioTemplatesQuery.cs:58-66`
- Modify: `src/InvestmentApp.Application/TradePlans/Queries/GetScenarioSuggestion/GetScenarioSuggestionQuery.cs:72-74`
- Test: `tests/InvestmentApp.Api.Tests/Mcp/McpSchemaContractTests.cs` (tạo mới)

**Interfaces:**
- Produces: `ScenarioNodeDto.ActionType` kiểu `ScenarioActionType?`; `ScenarioNodeDto.ConditionType` kiểu `ScenarioConditionType?`; `ScenarioNodeDto.Status` kiểu `ScenarioNodeStatus`; `TrailingStopConfigDto.Method` kiểu `TrailingStopMethod?`. Task 4 và 6 dựa vào đúng các tên/kiểu này.

- [ ] **Step 1: Viết test schema đỏ**

Tạo `tests/InvestmentApp.Api.Tests/Mcp/McpSchemaContractTests.cs`:

```csharp
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

    private static JsonElement ScenarioNodeProp(string toolName, string property)
        => SchemaOf(toolName)
            .GetProperty("properties").GetProperty("scenarioNodes")
            .GetProperty("items").GetProperty("properties").GetProperty(property);

    [Theory]
    [InlineData("create_trade_plan")]
    [InlineData("update_trade_plan")]
    public void ActionType_Lists_All_Seven_Values_In_Schema(string toolName)
    {
        var values = ScenarioNodeProp(toolName, "actionType")
            .GetProperty("enum").EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString())
            .ToArray();

        values.Should().BeEquivalentTo(
            "SellPercent", "SellAll", "MoveStopLoss", "MoveStopToBreakeven",
            "ActivateTrailingStop", "AddPosition", "SendNotification");
    }

    [Theory]
    [InlineData("create_trade_plan")]
    [InlineData("update_trade_plan")]
    public void ConditionType_Lists_All_Five_Values_In_Schema(string toolName)
    {
        var values = ScenarioNodeProp(toolName, "conditionType")
            .GetProperty("enum").EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString())
            .ToArray();

        values.Should().BeEquivalentTo(
            "PriceAbove", "PriceBelow", "PricePercentChange", "TrailingStopHit", "TimeElapsed");
    }

    [Fact]
    public void ActionType_Carries_A_Human_Description()
    {
        ScenarioNodeProp("create_trade_plan", "actionType")
            .GetProperty("description").GetString()
            .Should().Contain("AddPosition");
    }
}
```

- [ ] **Step 2: Chạy để xác nhận đỏ**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter "FullyQualifiedName~McpSchemaContractTests"`
Expected: FAIL — `KeyNotFoundException` vì property `actionType` hiện là `string` trần, không có khóa `enum`.

- [ ] **Step 3: Đổi kiểu DTO**

Trong `GetTradePlansQuery.cs`, thay hai lớp:

```csharp
public class ScenarioNodeDto
{
    public string NodeId { get; set; } = null!;
    public string? ParentId { get; set; }
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;

    [Description("Điều kiện kích hoạt nhánh. PriceAbove/PriceBelow so với conditionValue (VND); " +
                 "PricePercentChange so với giá vào (%); TimeElapsed tính theo số ngày.")]
    public ScenarioConditionType? ConditionType { get; set; }

    public decimal? ConditionValue { get; set; }
    public string? ConditionNote { get; set; }

    [Description("Hành động khi điều kiện chạm. SellPercent = bán actionValue% vị thế; " +
                 "SellAll = bán toàn bộ; MoveStopLoss = dời cắt lỗ tới actionValue (VND); " +
                 "MoveStopToBreakeven = dời cắt lỗ về giá hòa vốn; " +
                 "ActivateTrailingStop = bật trailing theo trailingStopConfig; " +
                 "AddPosition = mua thêm actionValue% vị thế; SendNotification = chỉ báo, không lệnh. " +
                 "Bắt buộc — không có giá trị mặc định.")]
    public ScenarioActionType? ActionType { get; set; }

    public decimal? ActionValue { get; set; }
    public TrailingStopConfigDto? TrailingStopConfig { get; set; }
    public ScenarioNodeStatus Status { get; set; } = ScenarioNodeStatus.Pending;
    public DateTime? TriggeredAt { get; set; }
    public string? TradeId { get; set; }
}

public class TrailingStopConfigDto
{
    [Description("Cách tính khoảng trailing (bỏ trống = Percentage).")]
    public TrailingStopMethod? Method { get; set; }

    [Description("Độ rộng trailing: % nếu Method=Percentage, số lần ATR nếu ATR, VND nếu FixedAmount.")]
    public decimal TrailValue { get; set; }

    public decimal? ActivationPrice { get; set; }
    public decimal? StepSize { get; set; }
    public decimal? CurrentTrailingStop { get; set; }
    public decimal? HighestPrice { get; set; }
}
```

Thêm vào đầu file: `using System.ComponentModel;` và `using InvestmentApp.Domain.Entities;` nếu chưa có.

- [ ] **Step 4: Bỏ `Enum.Parse` trong `MapToScenarioNode`**

Trong `CreateTradePlanCommand.cs`:

```csharp
    internal static ScenarioNode MapToScenarioNode(ScenarioNodeDto dto) => new()
    {
        NodeId = dto.NodeId,
        ParentId = dto.ParentId,
        Order = dto.Order,
        Label = dto.Label,
        // Validator đã chặn null trước khi tới đây; ?? chỉ để thỏa trình biên dịch.
        ConditionType = dto.ConditionType ?? ScenarioConditionType.PriceAbove,
        ConditionValue = dto.ConditionValue,
        ConditionNote = dto.ConditionNote,
        ActionType = dto.ActionType ?? ScenarioActionType.SendNotification,
        ActionValue = dto.ActionValue,
        TrailingStopConfig = dto.TrailingStopConfig != null ? new TrailingStopConfig
        {
            // Đơn vị đo được phép có mặc định; hành động thì không.
            Method = dto.TrailingStopConfig.Method ?? TrailingStopMethod.Percentage,
            TrailValue = dto.TrailingStopConfig.TrailValue,
            ActivationPrice = dto.TrailingStopConfig.ActivationPrice,
            StepSize = dto.TrailingStopConfig.StepSize
        } : null
    };
```

Chọn `SendNotification` làm giá trị lấp chỗ vì nó là hành động **vô hại nhất** — nếu validator có lỗ thủng thì hậu quả là một thông báo thừa, không phải một lệnh bán ngoài ý muốn.

- [ ] **Step 5: Bỏ `.ToString()` ở mọi chỗ dựng DTO**

Trong `GetTradePlansQuery.cs` (khối `MapScenarioNode` quanh dòng 105–125): đổi `ConditionType = n.ConditionType.ToString()` → `ConditionType = n.ConditionType`, tương tự cho `ActionType`, `Status`, và `Method = n.TrailingStopConfig.Method.ToString()` → `Method = n.TrailingStopConfig.Method`.

Làm y hệt tại `GetScenarioHistoryQuery.cs:68-73` và `GetScenarioTemplatesQuery.cs:58-66`.

Tại `GetScenarioSuggestionQuery.cs:72-74` hiện đã gán trực tiếp — kiểm tra kiểu nguồn và sửa nếu trình biên dịch báo lỗi.

- [ ] **Step 6: Thêm rule validator bắt `NotNull`**

Trong `CreateTradePlanCommandValidator.cs`, thêm vào constructor:

```csharp
        RuleForEach(x => x.ScenarioNodes!)
            .ChildRules(ScenarioNodeChild)
            .When(x => x.ScenarioNodes != null && x.ScenarioNodes.Count > 0);
```

và thêm phương thức dùng chung (Update validator sẽ tái sử dụng ở Task 4):

```csharp
    /// <summary>
    /// Hành động là quyết định nên không được có mặc định ngầm: node thiếu actionType từng
    /// im lặng trở thành "bán 50% vị thế". Method là đơn vị đo nên vẫn được phép bỏ trống.
    /// </summary>
    public static void ScenarioNodeChild(InlineValidator<ScenarioNodeDto> rule)
    {
        rule.RuleFor(n => n.ActionType)
            .NotNull()
            .WithMessage("actionType bắt buộc — một trong: SellPercent, SellAll, MoveStopLoss, " +
                         "MoveStopToBreakeven, ActivateTrailingStop, AddPosition, SendNotification");

        rule.RuleFor(n => n.ConditionType)
            .NotNull()
            .WithMessage("conditionType bắt buộc — một trong: PriceAbove, PriceBelow, " +
                         "PricePercentChange, TrailingStopHit, TimeElapsed");
    }
```

Thêm `using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;` nếu chưa có.

- [ ] **Step 7: Chạy toàn bộ test**

Run: `dotnet test`
Expected: PASS. Test hiện có dùng `ActionType = "SellPercent"` dạng chuỗi sẽ **không biên dịch được** — sửa chúng thành `ScenarioActionType.SellPercent`. Đây là lỗi biên dịch chứ không phải lỗi chạy, nên không sót chỗ nào.

- [ ] **Step 8: Commit**

```bash
git add src/InvestmentApp.Application/TradePlans/ tests/
git commit -m "feat(mcp): scenarioNodes khai enum thật, schema tự liệt kê giá trị hợp lệ"
```

---

### Task 4: `ExitTargetDto`, `PlanLotDto`, `InvalidationRuleDto` dùng enum thật

**Files:**
- Modify: `src/InvestmentApp.Application/TradePlans/Queries/GetTradePlans/GetTradePlansQuery.cs:215-239` (`PlanLotDto`, `ExitTargetDto`) và `:49,77,85` (mapping)
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommand.cs:61-68` (`InvalidationRuleDto`), `:108-115`, `:145-156`
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/UpdateTradePlan/UpdateTradePlanCommand.cs:128`, `:168`
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommandValidator.cs`
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/UpdateTradePlan/UpdateTradePlanCommandValidator.cs`
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/AbortTradePlan/AbortTradePlanCommand.cs:72-73`
- Test: `tests/InvestmentApp.Api.Tests/Mcp/McpSchemaContractTests.cs` (bổ sung)

**Interfaces:**
- Consumes: `CreateTradePlanCommandValidator.ScenarioNodeChild` từ Task 3.
- Produces: `ExitTargetDto.ActionType` kiểu `ExitActionType?`; `InvalidationRuleDto.Trigger` kiểu `InvalidationTrigger?`; `PlanLotDto.Status` kiểu `LotStatus` (đọc tên enum thật từ `TradePlan.cs` trước khi viết).

- [ ] **Step 1: Bổ sung test đỏ**

Thêm vào `McpSchemaContractTests.cs`:

```csharp
    [Fact]
    public void ExitTarget_ActionType_Lists_Its_Own_Four_Values()
    {
        var values = SchemaOf("create_trade_plan")
            .GetProperty("properties").GetProperty("exitTargets")
            .GetProperty("items").GetProperty("properties").GetProperty("actionType")
            .GetProperty("enum").EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString())
            .ToArray();

        values.Should().BeEquivalentTo("TakeProfit", "CutLoss", "TrailingStop", "PartialExit");
    }

    [Fact]
    public void InvalidationRule_Trigger_Lists_Its_Values()
    {
        var values = SchemaOf("create_trade_plan")
            .GetProperty("properties").GetProperty("invalidationCriteria")
            .GetProperty("items").GetProperty("properties").GetProperty("trigger")
            .GetProperty("enum").EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString())
            .ToArray();

        values.Should().Contain("EarningsMiss").And.Contain("TrendBreak");
    }
```

- [ ] **Step 2: Chạy để xác nhận đỏ**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter "FullyQualifiedName~McpSchemaContractTests"`
Expected: hai test mới FAIL, các test của Task 3 vẫn PASS.

- [ ] **Step 3: Đổi kiểu ba DTO**

`ExitTargetDto.ActionType` → `ExitActionType?` với:

```csharp
    [Description("Hành động tại mốc. TakeProfit = chốt lời; CutLoss = cắt lỗ; " +
                 "TrailingStop = chuyển sang trailing; PartialExit = thoát một phần. " +
                 "Bắt buộc. Lưu ý: tập giá trị này KHÁC với actionType của scenarioNodes.")]
    public ExitActionType? ActionType { get; set; }
```

`InvalidationRuleDto.Trigger` → `InvalidationTrigger?` với:

```csharp
    [Description("Sự kiện phủ định luận điểm. Bắt buộc.")]
    public InvalidationTrigger? Trigger { get; set; }
```

`PlanLotDto.Status` → kiểu enum lot thật (đọc tên từ `TradePlan.cs`), giữ giá trị khởi tạo `Pending`.

- [ ] **Step 4: Bỏ `Enum.Parse` và `.ToString()` tương ứng**

`CreateTradePlanCommand.cs:110` → `Trigger = r.Trigger ?? InvalidationTrigger.Manual` (validator đã chặn null).
`CreateTradePlanCommand.cs:149` và `UpdateTradePlanCommand.cs:168` → `ActionType = e.ActionType ?? ExitActionType.TakeProfit`.
`UpdateTradePlanCommand.cs:128` → như dòng 110.
`GetTradePlansQuery.cs:49,77,85` → bỏ `.ToString()`.
`AbortTradePlanCommand.cs:73` → bỏ `.ToString()` nếu trường đích đã là enum.

- [ ] **Step 5: Siết validator**

Trong `CreateTradePlanCommandValidator.InvalidationRuleChild`, thay rule `Trigger` hiện tại bằng:

```csharp
        rule.RuleFor(r => r.Trigger)
            .NotNull()
            .WithMessage("trigger bắt buộc — một trong: EarningsMiss, TrendBreak, NewsShock, " +
                         "ThesisTimeout, Manual");
```

Xóa phương thức `BeValidTrigger` — enum đã tự bảo đảm, giữ lại là code chết.

Trong `UpdateTradePlanCommandValidator`, thêm:

```csharp
        RuleForEach(x => x.ScenarioNodes!)
            .ChildRules(CreateTradePlanCommandValidator.ScenarioNodeChild)
            .When(x => x.ScenarioNodes != null && x.ScenarioNodes.Count > 0);
```

- [ ] **Step 6: Chạy toàn bộ test**

Run: `dotnet test`
Expected: PASS. Sửa mọi test không biên dịch được sang dạng enum.

- [ ] **Step 7: Commit**

```bash
git add src/ tests/
git commit -m "feat(mcp): exitTargets, lots, invalidationCriteria khai enum thật trong schema"
```

---

### Task 5: Tham số phẳng — enum thật, hoặc `[AllowedValues]` khi không có enum miền

**Files:**
- Modify: `src/InvestmentApp.Api/Mcp/TradePlanTools.cs`
- Test: `tests/InvestmentApp.Api.Tests/Mcp/McpSchemaContractTests.cs` (bổ sung)

**Interfaces:**
- Produces: chữ ký tool đổi — `timeHorizon` thành `TimeHorizon? timeHorizon = null`, `entryMode` thành `EntryMode? entryMode = null`, `exitStrategyMode` thành `ExitStrategyMode? exitStrategyMode = null`.

- [ ] **Step 1: Bổ sung test đỏ**

```csharp
    [Fact]
    public void TimeHorizon_Param_Lists_Values_And_Stays_Optional()
    {
        var schema = SchemaOf("update_trade_plan");

        var values = schema.GetProperty("properties").GetProperty("timeHorizon")
            .GetProperty("enum").EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString())
            .ToArray();
        values.Should().BeEquivalentTo("ShortTerm", "MediumTerm", "LongTerm");

        // Tham số enum thiếu "= null" bị SDK đẩy vào required — bẫy đã gặp lúc dựng thiết kế.
        var required = schema.TryGetProperty("required", out var r)
            ? r.EnumerateArray().Select(v => v.GetString()).ToArray()
            : Array.Empty<string?>();
        required.Should().NotContain("timeHorizon");
    }

    [Fact]
    public void Direction_Has_No_Domain_Enum_So_It_Documents_Values_In_Description()
    {
        SchemaOf("create_trade_plan")
            .GetProperty("properties").GetProperty("direction")
            .GetProperty("description").GetString()
            .Should().Contain("Buy").And.Contain("Sell");
    }
```

- [ ] **Step 2: Chạy để xác nhận đỏ**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter "FullyQualifiedName~McpSchemaContractTests"`
Expected: `TimeHorizon_Param_Lists_Values_And_Stays_Optional` FAIL.

- [ ] **Step 3: Đổi ba tham số có enum miền**

Trong `TradePlanTools.cs`, ở **cả** `CreateTradePlan` và `UpdateTradePlan`:

```csharp
        [Description("Kiểu vào lệnh nhiều lô (bỏ trống = vào một lần).")] EntryMode? entryMode = null,
        [Description("Kiểu chiến lược thoát. Advanced mới dùng được scenarioNodes.")] ExitStrategyMode? exitStrategyMode = null,
        [Description("Tầm nhìn nắm giữ (bỏ trống = không đặt).")] TimeHorizon? timeHorizon = null,
```

Chỗ dựng command: `EntryMode = entryMode?.ToString()`, `ExitStrategyMode = exitStrategyMode?.ToString()`, `TimeHorizon = timeHorizon?.ToString()` — giữ nguyên kiểu `string?` trên command để không phải sửa handler ở task này.

- [ ] **Step 4: Thêm `[AllowedValues]` cho tham số không có enum miền**

```csharp
        [Description("Chiều lệnh (bỏ trống = Buy).")]
        [System.ComponentModel.DataAnnotations.AllowedValues("Buy", "Sell")] string? direction = null,

        [Description("Bối cảnh thị trường (bỏ trống = Trending).")]
        [System.ComponentModel.DataAnnotations.AllowedValues("Trending", "Ranging", "Volatile")] string? marketCondition = null,
```

Và trong `SetTradePlanStatus`:

```csharp
        [Description("Trạng thái mới. 'restore' bị chặn qua MCP.")]
        [System.ComponentModel.DataAnnotations.AllowedValues("ready", "executed", "cancelled")] string status,
```

**Ghi rõ trong PR:** `[AllowedValues]` ở SDK 2.0.0-rc.1 chỉ phục vụ `completion/complete`, **không** sinh ràng buộc `enum` trong schema. Với các field này, thứ thực sự tới được agent là `[Description]` — `[AllowedValues]` là phần thêm cho client tương tác.

- [ ] **Step 5: Chạy toàn bộ test**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/InvestmentApp.Api/Mcp/TradePlanTools.cs tests/InvestmentApp.Api.Tests/Mcp/McpSchemaContractTests.cs
git commit -m "feat(mcp): tham số phẳng khai enum hoặc liệt kê giá trị trong mô tả"
```

---

### Task 6: Test nghiệm thu đầu-cuối, chốt chặn hồi quy, đồng bộ tài liệu

**Files:**
- Modify: `tests/InvestmentApp.Api.Tests/Mcp/McpErrorSurfaceTests.cs`
- Modify: `tests/InvestmentApp.Api.Tests/Mcp/McpToolDiscoveryTests.cs`
- Modify: `src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md`
- Modify: `docs/business-domain.md`, `docs/architecture.md`
- Modify: `frontend/src/assets/CHANGELOG.md`
- Create: `docs/adr/00NN-mcp-schema-carries-allowed-values.md`

- [ ] **Step 1: Viết test nghiệm thu đầu-cuối**

Thêm vào `McpErrorSurfaceTests.cs`. Test này buộc cả bốn task chứng minh lẫn nhau — dùng lại khuôn `CallAsync` trong `McpToolArgumentBindingTests.cs` (sao chép helper vào file này hoặc tách ra dùng chung):

```csharp
    [Fact]
    public async Task Bad_ActionType_Error_Names_All_Valid_Values()
    {
        // Đúng lời gọi đã làm hỏng phiên 11/08: "Buy" không tồn tại trong ScenarioActionType.
        var result = await InvokeRawAsync("update_trade_plan",
            """
            {"id":"p1","exitStrategyMode":"Advanced",
             "scenarioNodes":[{"nodeId":"n1","order":1,"label":"x",
               "conditionType":"PriceAbove","conditionValue":24000,"actionType":"Buy"}]}
            """);

        result.IsError.Should().BeTrue();
        var text = string.Join(" ", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

        text.Should().NotContain("An error occurred invoking",
            "message bị che là mất hoàn toàn đường tự chữa của agent");
        text.Should().Contain("AddPosition", "agent phải đọc được tập giá trị hợp lệ từ chính lỗi");
    }
```

`InvokeRawAsync` là biến thể của `CallAsync` **không** khẳng định `IsError == false` — viết nó cạnh test, trả thẳng `CallToolResult`.

- [ ] **Step 2: Chạy và xác nhận xanh**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter "FullyQualifiedName~McpErrorSurfaceTests"`
Expected: PASS. Nếu đỏ ở khẳng định `NotContain("An error occurred invoking")` thì Task 1 chưa phủ hết đường đi — quay lại Task 1, đừng nới lỏng khẳng định.

- [ ] **Step 3: Chốt chặn hồi quy toàn mặt tiếp xúc**

Thêm vào `McpToolDiscoveryTests.cs`:

```csharp
    /// <summary>
    /// Không dựa vào việc người viết tool tiếp theo nhớ ra: mọi property tên "*Type", "trigger",
    /// "status", "method" trong schema phải mang enum hoặc description có liệt kê giá trị.
    /// </summary>
    [Fact]
    public void Every_Finite_Value_Property_Declares_Its_Values()
    {
        var offenders = new List<string>();
        foreach (var tool in Tools())
        {
            var raw = tool.ProtocolTool.InputSchema.GetRawText();
            using var doc = JsonDocument.Parse(raw);
            Walk(doc.RootElement, tool.ProtocolTool.Name, offenders);
        }

        offenders.Should().BeEmpty(
            "tham số có tập giá trị hữu hạn phải tự khai tập đó — agent không đọc tài liệu");
    }
```

Viết `Walk` duyệt đệ quy `properties`/`items`, chọn property có tên khớp `Type$|^trigger$|^status$|^method$` (không phân biệt hoa thường) và báo vi phạm khi property đó **không** có khóa `enum` **và** description của nó không chứa dấu phẩy liệt kê. Chạy, xem danh sách vi phạm; nếu có tool ngoài phạm vi kế hoạch này lọt vào, **không** sửa nó ở đây — ghi vào phần "còn treo" của PR.

- [ ] **Step 4: Đồng bộ tài liệu**

- `src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md`: dòng 44–48 vẫn đúng nội dung nhưng ghi chú thêm rằng tập giá trị nay do schema tự khai; sửa `exitTargets.actionType` từ `TakeProfit|CutLoss|TrailingStop|PartialExit` nếu tên enum thật khác.
- `docs/business-domain.md`: mục cây kịch bản — ghi `actionType`/`conditionType` là bắt buộc, không còn mặc định ngầm.
- `docs/architecture.md`: thêm `McpErrorTranslator` vào danh sách thành phần lớp Api.
- `frontend/src/assets/CHANGELOG.md`: thêm mục fix. Đây là file trong `frontend/` nhưng là tài liệu, không phải mã — vẫn nằm trong phạm vi.

- [ ] **Step 5: Viết ADR**

Tạo `docs/adr/00NN-mcp-schema-carries-allowed-values.md` theo `docs/adr/template.md` (đọc số thứ tự kế tiếp từ thư mục). Quyết định: *tập giá trị hợp lệ của tham số MCP sống trong `inputSchema`, không sống trong tài liệu*. Trong phần lựa chọn đã cân nhắc, ghi rõ `AllowedValuesAttribute` chỉ phục vụ `completion/complete` nên không thay thế được enum thật — đây là sự thật về SDK, nếu SDK đổi thì ADR phải sửa.

- [ ] **Step 6: Chạy toàn bộ và quét bí mật**

```bash
dotnet test
git diff origin/master --unified=0 | grep '^+' | grep -Ei '(api[_-]?key|secret|password|token|mongodb\+srv|Bearer [A-Za-z0-9._-]{20,})'
```
Expected: test PASS; lệnh quét **không** ra dòng nào. Có dòng nào thì dừng, không commit.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "docs(mcp): ghi hợp đồng schema tự mô tả vào tài liệu và ADR"
```

---

## Self-Review

**Phủ spec:** mục 1 → Task 3/4/5; mục 2 → Task 3 (step 3, 6) + Task 4 (step 5); mục 3 → Task 1; mục 4 → Task 2; tiêu chí nghiệm thu mục 5 → Task 6 step 1–3; phần "cố ý không làm" → không task nào chạm tới; ghi chú treo mục 7 → Task 4 step 3 ghi cảnh báo hai enum trùng tên vào description.

**Điểm cần đọc file thật trước khi gõ:** chữ ký constructor `TradePlan` và `UpdateTradePlanCommandHandler` (Task 2), tên enum trạng thái lô (Task 4), số thứ tự ADR kế tiếp (Task 6). Kế hoạch cố ý không đoán những giá trị này.
