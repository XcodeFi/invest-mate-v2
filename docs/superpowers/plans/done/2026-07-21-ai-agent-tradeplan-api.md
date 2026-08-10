# AI-Agent TradePlan API — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cho Claude (qua chat NPU local) một bề mặt API `X-Api-Key` để lập / sửa / chuyển trạng thái / thực hiện (ghi Trade + Executed) trade plan trên backend invest-mate-v2, kèm tài liệu local có versioning.

**Architecture:** Một controller mỏng `AiAgentController` pin scheme `ApiKey` (mô phỏng `AiDigestController`), **re-dispatch các MediatR command sẵn có** — không viết lại business logic. Adapter chèn 3 guard (ép Draft, chặn `restore`, gán `UserId`). Vá IDOR ở handler `CreateTrade`. Doc là embedded resource, serve qua endpoint có `ETag=docVersion`, có drift test.

**Tech Stack:** .NET 9, ASP.NET Core, MediatR, MongoDB.Driver; test bằng xUnit + Moq + FluentAssertions.

## Global Constraints

- **Bản chất app:** nhật ký/tracker — KHÔNG đặt lệnh sàn, KHÔNG tiền thật, dữ liệu sửa được. Guard/confirm là trust-model, không phải security cấp prod.
- **Auth:** dùng scheme sẵn có `ApiKeyAuthenticationDefaults.Scheme` (header `X-Api-Key`), phát claim `sub`. KHÔNG comma multi-scheme (codebase không dùng); dùng controller riêng pin một scheme.
- **Reuse:** re-dispatch command sẵn có; KHÔNG thêm business logic mới ở controller.
- **Confirm ("chốt") + inject key/base_url + đọc doc = phía NPU (F1)** — NGOÀI phạm vi plan này.
- **Git:** branch trước (không commit thẳng `master`); commit từng task; xin phép trước mỗi commit/push.
- **Deferred sang v1.1 (nêu rõ, không làm trong plan này):** (a) verify ownership của `StrategyId`/`tradeId` truyền vào plan command — gap có sẵn, low-stakes single-user, đụng nhiều handler; (b) mọi thay đổi phía npu-assistant (F1).

---

### Task 1: Vá IDOR `CreateTrade` + dấu audit AI

**Files:**
- Modify: `src/InvestmentApp.Application/Trades/Commands/CreateTrade/CreateTradeCommand.cs`
- Modify: `src/InvestmentApp.Api/Controllers/TradesController.cs:34-38`
- Test: `tests/InvestmentApp.Application.Tests/Trades/Commands/CreateTradeCommandHandlerTests.cs`

**Interfaces:**
- Produces: `CreateTradeCommand.UserId` (string, `[JsonIgnore]`, server-set), `CreateTradeCommand.Origin` (string?, `[JsonIgnore]`) — Task 2 sets these.
- Handler now throws `UnauthorizedAccessException` khi `portfolio.UserId != request.UserId`.

- [ ] **Step 1: Cập nhật test helper + thêm test IDOR (failing)**

Trong `CreateTradeCommandHandlerTests.cs`, sửa helper để set `UserId` khớp chủ portfolio, và thêm 2 test:

```csharp
// trong CreatePortfolioAndCommand(...), sau khi tạo command, thêm:
command.UserId = portfolio.UserId; // "user1"

// thêm 2 test mới:
[Fact]
public async Task Handle_PortfolioOwnedByAnotherUser_ThrowsUnauthorized()
{
    var (_, command) = CreatePortfolioAndCommand();
    command.UserId = "someone-else";

    var act = () => _handler.Handle(command, CancellationToken.None);

    await act.Should().ThrowAsync<UnauthorizedAccessException>();
}

[Fact]
public async Task Handle_OriginSet_WritesSourceToAuditMetadata()
{
    var (_, command) = CreatePortfolioAndCommand();
    command.Origin = "AI_AGENT";
    AuditEntry? captured = null;
    _auditService.Setup(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
        .Callback<AuditEntry, CancellationToken>((e, _) => captured = e)
        .Returns(Task.CompletedTask);

    await _handler.Handle(command, CancellationToken.None);

    captured!.Metadata!.ToString().Should().Contain("AI_AGENT");
}
```

- [ ] **Step 2: Chạy test → phải FAIL**

Run: `cd project && dotnet test tests/InvestmentApp.Application.Tests --filter CreateTradeCommandHandlerTests`
Expected: FAIL — `UserId`/`Origin` chưa tồn tại (compile error) hoặc assert Unauthorized không xảy ra.

- [ ] **Step 3: Thêm field vào command**

Trong `CreateTradeCommand.cs`, thêm `using System.Text.Json.Serialization;` và 2 field:

```csharp
public class CreateTradeCommand : IRequest<string>
{
    [JsonIgnore] public string UserId { get; set; } = null!;   // server-set từ sub
    [JsonIgnore] public string? Origin { get; set; }           // "AI_AGENT" khi qua agent
    public string PortfolioId { get; set; } = null!;
    // ... giữ nguyên các field còn lại
}
```

- [ ] **Step 4: Assert ownership + ghi Origin trong handler**

Trong `CreateTradeCommandHandler.Handle`, ngay sau null-check portfolio:

```csharp
if (portfolio == null)
    throw new InvalidOperationException("Portfolio not found");

if (portfolio.UserId != request.UserId)
    throw new UnauthorizedAccessException("Not authorized to create a trade in this portfolio");
```

Và trong `AuditEntry.Metadata`, thêm `Source`:

```csharp
Metadata = new
{
    request.Symbol, request.TradeType, request.Quantity,
    request.Price, request.Fee, request.Tax,
    Source = request.Origin ?? "USER"
}
```

- [ ] **Step 5: Set `UserId` ở TradesController (JWT path) để không vỡ**

`TradesController.CreateTrade`:

```csharp
public async Task<IActionResult> CreateTrade([FromBody] CreateTradeCommand command)
{
    command.UserId = GetUserId();
    var tradeId = await _mediator.Send(command);
    return CreatedAtAction(nameof(GetTrade), new { id = tradeId }, new { id = tradeId });
}
```

- [ ] **Step 6: Chạy lại toàn bộ test của file → PASS**

Run: `cd project && dotnet test tests/InvestmentApp.Application.Tests --filter CreateTradeCommandHandlerTests`
Expected: PASS (cả test cũ đã sửa helper lẫn 2 test mới).

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Application/Trades/Commands/CreateTrade/CreateTradeCommand.cs \
        src/InvestmentApp.Api/Controllers/TradesController.cs \
        tests/InvestmentApp.Application.Tests/Trades/Commands/CreateTradeCommandHandlerTests.cs
git commit -m "fix(trades): enforce portfolio ownership on CreateTrade + audit source marker"
```

---

### Task 2: `AiAgentController` (scheme ApiKey) + adapter guards

**Files:**
- Create: `src/InvestmentApp.Api/Controllers/AiAgentController.cs`
- Test: `tests/InvestmentApp.Api.Tests/Controllers/AiAgentControllerTests.cs`

**Interfaces:**
- Consumes: `IMediator`; các command/query sẵn có (`GetTradePlansQuery`, `GetTradePlanByIdQuery`, `CreateTradePlanCommand`, `UpdateTradePlanCommand`, `UpdateTradePlanStatusCommand`, `CreateTradeCommand` với `UserId`/`Origin` từ Task 1).
- Produces: route base `api/v1/ai/agent`; các action dưới đây (Task 4 thêm `GET doc` vào cùng controller).

- [ ] **Step 1: Viết test controller (failing)**

Tạo `AiAgentControllerTests.cs`:

```csharp
using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class AiAgentControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private AiAgentController Sut(string userId = "user-1")
    {
        var controller = new AiAgentController(_mediator.Object);
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId) }, "ApiKey");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task CreatePlan_NullsStatusAndTradeId_AndSetsUserId()
    {
        CreateTradePlanCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (CreateTradePlanCommand)c)
            .ReturnsAsync("plan-1");

        var cmd = new CreateTradePlanCommand { Symbol = "VNM", Status = "Executed", TradeId = "t-x" };
        await Sut().CreatePlan(cmd);

        sent!.Status.Should().BeNull();
        sent.TradeId.Should().BeNull();
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task UpdateStatus_Restore_ReturnsBadRequest_AndDoesNotDispatch()
    {
        var result = await Sut().UpdateStatus("plan-1",
            new UpdateTradePlanStatusCommand { Status = "restore" });

        result.Should().BeOfType<BadRequestObjectResult>();
        _mediator.Verify(m => m.Send(It.IsAny<UpdateTradePlanStatusCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTrade_SetsOriginAiAgent_AndUserId()
    {
        CreateTradeCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (CreateTradeCommand)c)
            .ReturnsAsync("trade-1");

        await Sut().CreateTrade(new CreateTradeCommand { PortfolioId = "p1", Symbol = "VNM",
            TradeType = "BUY", Quantity = 100, Price = 50000 });

        sent!.UserId.Should().Be("user-1");
        sent.Origin.Should().Be("AI_AGENT");
    }
}
```

- [ ] **Step 2: Chạy test → FAIL**

Run: `cd project && dotnet test tests/InvestmentApp.Api.Tests --filter AiAgentControllerTests`
Expected: FAIL — `AiAgentController` chưa tồn tại (compile error).

- [ ] **Step 3: Viết controller**

Tạo `AiAgentController.cs`:

```csharp
using InvestmentApp.Api.Auth;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Bề mặt agent (scheme ApiKey) cho NPU/Claude ghi trade plan + trade. Re-dispatch command sẵn có;
/// adapter chèn guard (ép Draft, chặn restore, gán UserId/Origin). Không chứa business logic.
/// </summary>
[ApiController]
[Route("api/v1/ai/agent")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentController : ControllerBase
{
    private readonly IMediator _mediator;
    public AiAgentController(IMediator mediator) => _mediator = mediator;

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet("trade-plans")]
    public async Task<IActionResult> GetPlans([FromQuery] bool activeOnly = false)
        => Ok(await _mediator.Send(new GetTradePlansQuery { UserId = GetUserId(), ActiveOnly = activeOnly }));

    [HttpGet("trade-plans/{id}")]
    public async Task<IActionResult> GetPlan(string id)
    {
        var result = await _mediator.Send(new GetTradePlanByIdQuery { Id = id, UserId = GetUserId() });
        return result == null ? NotFound(new { message = "Trade plan not found" }) : Ok(result);
    }

    [HttpPost("trade-plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateTradePlanCommand command)
    {
        command.UserId = GetUserId();
        command.Status = null;   // ép Draft — agent không one-shot execute
        command.TradeId = null;
        var id = await _mediator.Send(command);
        return Created($"/api/v1/ai/agent/trade-plans/{id}", new { id });
    }

    [HttpPut("trade-plans/{id}")]
    public async Task<IActionResult> UpdatePlan(string id, [FromBody] UpdateTradePlanCommand command)
    {
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("trade-plans/{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateTradePlanStatusCommand command)
    {
        if (string.Equals(command.Status, "restore", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "restore không được phép qua agent surface" });
        command.Id = id;
        command.UserId = GetUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPost("trades")]
    public async Task<IActionResult> CreateTrade([FromBody] CreateTradeCommand command)
    {
        command.UserId = GetUserId();
        command.Origin = "AI_AGENT";
        var id = await _mediator.Send(command);
        return Created($"/api/v1/trades/{id}", new { id });
    }
}
```

- [ ] **Step 4: Chạy test → PASS**

Run: `cd project && dotnet test tests/InvestmentApp.Api.Tests --filter AiAgentControllerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Api/Controllers/AiAgentController.cs \
        tests/InvestmentApp.Api.Tests/Controllers/AiAgentControllerTests.cs
git commit -m "feat(ai-agent): ApiKey controller re-dispatching plan/trade commands with adapter guards"
```

---

### Task 3: Tài liệu API (embedded resource, có mục lục)

**Files:**
- Create: `src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md`
- Modify: `src/InvestmentApp.Api/InvestmentApp.Api.csproj` (đánh dấu EmbeddedResource)

**Interfaces:**
- Produces: embedded resource tên `InvestmentApp.Api.Docs.AI-Agent-TradePlan-API.md` — Task 4 serve, Task 5 drift-test.

- [ ] **Step 1: Viết tài liệu**

Tạo `src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md` với nội dung (điền đủ, không placeholder):

````markdown
# Invest Mate — AI Agent API (Trade Plan)

Tài liệu authoritative cho Claude. Base URL và `X-Api-Key` do NPU cấp lúc gọi (không có ở đây).
Trước MỌI ghi: tóm tắt một dòng, chờ người dùng "chốt" trong chat, rồi mới gọi.

## Mục lục (ý định → mục)
- Lập plan → [Create Plan](#create-plan)
- Sửa plan → [Update Plan](#update-plan)
- Chuyển trạng thái → [Status](#status)
- Ghi trade / thực hiện → [Create Trade](#create-trade) + [Full-execute](#full-execute)
- Đọc plan → [Read](#read)
- Quy tắc bắt buộc → [Rules](#rules)

## Auth & lỗi
- Header: `X-Api-Key: {ApiKey}`. Fail auth có thể trả 302 (Google) — coi như chưa xác thực.
- 400 = validation sai; 401 = thiếu/sai key; 404 = không sở hữu/không tồn tại.

## <a id="rules"></a>Rules (BẮT BUỘC)
1. Discipline gate (chặn Draft→Ready): `thesis` ≥15 ký tự; nếu `quantity*entryPrice ≥ 5%*accountBalance` → `thesis` ≥30 ký tự VÀ ≥1 `invalidationCriteria` với `detail` ≥20 ký tự.
2. Gửi kèm `accountBalance` khi tạo plan (thiếu nó gate chỉ còn 15 ký tự).
3. `entryPrice/stopLoss/quantity > 0`. `invalidation.trigger ∈ {EarningsMiss, TrendBreak, NewsShock, ThesisTimeout, Manual}`.
4. Trade: `tradeType ∈ {BUY, SELL}`, `quantity/price > 0`, `fee/tax ≥ 0`, `symbol ≤10`.
5. Không sửa plan `Executed`/`Reviewed`. Ghi Trade TRƯỚC khi mark Executed.

## <a id="read"></a>Read
- `GET /api/v1/ai/agent/trade-plans?activeOnly=true|false`
- `GET /api/v1/ai/agent/trade-plans/{id}`

## <a id="create-plan"></a>Create Plan
`POST /api/v1/ai/agent/trade-plans` — luôn tạo ở trạng thái Draft (server bỏ qua status/tradeId nếu gửi).
Body (các field chính): `symbol`, `direction` (Buy|Sell), `entryPrice`, `stopLoss`, `target`, `quantity`,
`portfolioId?`, `strategyId?`, `marketCondition`, `thesis`, `notes`, `confidenceLevel` (1-10),
`riskPercent?`, `accountBalance?`, `riskRewardRatio?`, `timeHorizon` (ShortTerm|MediumTerm|LongTerm),
`expectedReviewDate?`, `invalidationCriteria[]` `{trigger, detail}`, `checklist[]` `{label,category,checked,critical,hint}`,
`entryMode` (Single|ScalingIn|DCA) + `lots[]` `{lotNumber,plannedPrice,plannedQuantity,allocationPercent?,label?}`,
`exitTargets[]` `{level,actionType(TakeProfit|CutLoss|TrailingStop|PartialExit),price,quantity?,percentOfPosition?,label?}`,
`exitStrategyMode` (Simple|Advanced) + `scenarioNodes[]` `{nodeId,parentId?,order,label,conditionType,conditionValue?,actionType,actionValue?,trailingStopConfig?}`.
Enums scenario: conditionType ∈ {PriceAbove,PriceBelow,PricePercentChange,TrailingStopHit,TimeElapsed};
actionType ∈ {SellPercent,SellAll,MoveStopLoss,MoveStopToBreakeven,ActivateTrailingStop,AddPosition,SendNotification};
trailingStop.method ∈ {Percentage,ATR,FixedAmount}.

Ví dụ tối thiểu:
```json
{ "symbol":"VNM","direction":"Buy","entryPrice":50,"stopLoss":47,"target":60,"quantity":100,
  "accountBalance":100000,"thesis":"Breakout khỏi nền tích luỹ, volume xác nhận",
  "invalidationCriteria":[{"trigger":"TrendBreak","detail":"Đóng cửa dưới 47 hai phiên liên tiếp"}] }
```
Trả về: `201 { "id": "<planId>" }`.

## <a id="update-plan"></a>Update Plan
`PUT /api/v1/ai/agent/trade-plans/{id}` — cùng bộ field (đều optional). Không dùng được nếu plan Executed/Reviewed.

## <a id="status"></a>Status
`PATCH /api/v1/ai/agent/trade-plans/{id}/status` body `{ "status": "ready|inprogress|executed|cancelled", "tradeId?": "..." }`.
`restore` bị chặn (400). `executed` cần `tradeId` (tạo trade trước).

## <a id="create-trade"></a>Create Trade
`POST /api/v1/ai/agent/trades` body `{ "portfolioId","symbol","tradeType":"BUY|SELL","quantity","price","fee","tax","tradeDate?" }`.
Trả về `201 { "id": "<tradeId>" }`. Portfolio phải thuộc bạn.

## <a id="full-execute"></a>Full-execute (thực hiện)
1. `POST trade-plans` → planId. 2. `POST trades` → tradeId. 3. `PATCH trade-plans/{planId}/status {status:"executed", tradeId}`.
Nếu bước 3 lỗi sau khi đã tạo trade: báo rõ `planId` + `tradeId` để dọn tay.
````

- [ ] **Step 2: Đánh dấu EmbeddedResource**

Trong `InvestmentApp.Api.csproj`, thêm:

```xml
<ItemGroup>
  <EmbeddedResource Include="Docs/AI-Agent-TradePlan-API.md" />
</ItemGroup>
```

- [ ] **Step 3: Build để xác nhận resource nhúng được**

Run: `cd project && dotnet build src/InvestmentApp.Api`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md src/InvestmentApp.Api/InvestmentApp.Api.csproj
git commit -m "docs(ai-agent): add embedded Trade Plan API reference for Claude"
```

---

### Task 4: Serve tài liệu với `ETag=docVersion` (304)

**Files:**
- Modify: `src/InvestmentApp.Api/Controllers/AiAgentController.cs` (thêm action `GetDoc` + helper đọc resource)
- Test: `tests/InvestmentApp.Api.Tests/Controllers/AiAgentControllerTests.cs`

**Interfaces:**
- Consumes: embedded resource từ Task 3.
- Produces: `GET /api/v1/ai/agent/doc` → 200 (text/markdown, header `ETag`) hoặc 304 nếu `If-None-Match` khớp.

- [ ] **Step 1: Thêm test (failing)**

Thêm vào `AiAgentControllerTests.cs`:

```csharp
[Fact]
public void GetDoc_Returns200_WithETagAndBody()
{
    var sut = Sut();
    var result = sut.GetDoc() as ContentResult;

    result!.StatusCode.Should().Be(200);
    result.Content.Should().Contain("Mục lục");
    sut.Response.Headers.ETag.ToString().Should().NotBeNullOrEmpty();
}

[Fact]
public void GetDoc_MatchingIfNoneMatch_Returns304()
{
    var sut = Sut();
    var version = AiAgentController.DocVersion;
    sut.ControllerContext.HttpContext.Request.Headers.IfNoneMatch = $"\"{version}\"";

    var result = sut.GetDoc() as StatusCodeResult;

    result!.StatusCode.Should().Be(StatusCodes.Status304NotModified);
}
```

- [ ] **Step 2: Chạy → FAIL**

Run: `cd project && dotnet test tests/InvestmentApp.Api.Tests --filter AiAgentControllerTests`
Expected: FAIL — `GetDoc`/`DocVersion` chưa có.

- [ ] **Step 3: Thêm action + helper vào controller**

Thêm vào `AiAgentController` (cần `using System.Reflection;`):

```csharp
public static readonly string DocVersion =
    Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "0";

private const string DocResource = "InvestmentApp.Api.Docs.AI-Agent-TradePlan-API.md";

[HttpGet("doc")]
public IActionResult GetDoc()
{
    var etag = $"\"{DocVersion}\"";
    if (Request.Headers.IfNoneMatch.ToString() == etag)
        return StatusCode(StatusCodes.Status304NotModified);

    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DocResource)
        ?? throw new InvalidOperationException($"Embedded doc {DocResource} not found");
    using var reader = new StreamReader(stream);
    var md = reader.ReadToEnd();

    Response.Headers.ETag = etag;
    return new ContentResult { Content = md, ContentType = "text/markdown", StatusCode = 200 };
}
```

> Nếu tên manifest resource khác (namespace/thư mục), lấy đúng tên bằng `Assembly.GetManifestResourceNames()` rồi sửa `DocResource`.

- [ ] **Step 4: Chạy → PASS**

Run: `cd project && dotnet test tests/InvestmentApp.Api.Tests --filter AiAgentControllerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Api/Controllers/AiAgentController.cs \
        tests/InvestmentApp.Api.Tests/Controllers/AiAgentControllerTests.cs
git commit -m "feat(ai-agent): serve versioned API doc with ETag/304"
```

---

### Task 5: Drift test — field command ↔ tài liệu

**Files:**
- Test: `tests/InvestmentApp.Api.Tests/Docs/AgentDocDriftTests.cs`

**Interfaces:**
- Consumes: embedded doc (Task 3), `CreateTradePlanCommand` + `CreateTradeCommand` (public props trừ `[JsonIgnore]`).

- [ ] **Step 1: Viết drift test (failing nếu doc thiếu field)**

Tạo `AgentDocDriftTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json.Serialization;
using FluentAssertions;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.Trades.Commands.CreateTrade;

namespace InvestmentApp.Api.Tests.Docs;

public class AgentDocDriftTests
{
    private static string LoadDoc()
    {
        var asm = typeof(InvestmentApp.Api.Controllers.AiAgentController).Assembly;
        using var s = asm.GetManifestResourceStream("InvestmentApp.Api.Docs.AI-Agent-TradePlan-API.md")!;
        return new StreamReader(s).ReadToEnd();
    }

    public static IEnumerable<object[]> DocumentedFields()
    {
        foreach (var t in new[] { typeof(CreateTradePlanCommand), typeof(CreateTradeCommand) })
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue; // UserId/Origin/Id...
                if (p.GetCustomAttribute<ObsoleteAttribute>() != null) continue;    // Reason shim
                yield return new object[] { p.Name };
            }
    }

    [Theory]
    [MemberData(nameof(DocumentedFields))]
    public void EveryCommandField_IsMentionedInDoc(string fieldName)
    {
        var doc = LoadDoc();
        var camel = char.ToLowerInvariant(fieldName[0]) + fieldName[1..];
        doc.Should().MatchRegex($"(?i)\\b{camel}\\b",
            $"field '{camel}' phải được nhắc trong tài liệu agent (nếu vừa thêm field, cập nhật doc)");
    }
}
```

- [ ] **Step 2: Chạy → PASS (hoặc lộ field thiếu)**

Run: `cd project && dotnet test tests/InvestmentApp.Api.Tests --filter AgentDocDriftTests`
Expected: PASS. Nếu FAIL → bổ sung field còn thiếu vào `AI-Agent-TradePlan-API.md` rồi chạy lại.

- [ ] **Step 3: Commit**

```bash
git add tests/InvestmentApp.Api.Tests/Docs/AgentDocDriftTests.cs
git commit -m "test(ai-agent): drift test ensuring doc covers all command fields"
```

---

### Task 6: Xác minh toàn cục + doc dev

**Files:**
- Modify: `docs/` (README/architecture nếu có mục API list) — thêm 1 dòng trỏ tới bề mặt agent.

- [ ] **Step 1: Chạy full test suite**

Run: `cd project && dotnet test`
Expected: tất cả PASS (không hồi quy).

- [ ] **Step 2: Smoke thủ công trên prod (mã throwaway) — chỉ khi có key thật**

Với key trong biến môi trường (không để trong argv):
```bash
export IMK="$(cat ~/.imk-agent-key)"   # key do bạn tạo trong app
BASE="https://<cloud-run-host>/api/v1/ai/agent"
curl -s -H "X-Api-Key: $IMK" "$BASE/trade-plans?activeOnly=true" | head
# tạo → trade → executed → rồi cancel + xoá trade để dọn (xem Full-execute trong doc)
```
Expected: 200 đọc; chuỗi ghi trả 201/204; `restore` trả 400.

- [ ] **Step 3: Cập nhật doc dev + commit**

Thêm một dòng vào tài liệu API-list của repo (nếu có) trỏ `GET/POST /api/v1/ai/agent/*` + link tới `Docs/AI-Agent-TradePlan-API.md`.

```bash
git add docs/
git commit -m "docs: reference AI agent surface in API overview"
```

---

## Self-Review (đã chạy)

- **Spec coverage:** §5 ops → Task 2; §6 rules → doc Task 3; §7 doc local+version+ETag → Task 3+4; drift → Task 5; §8 IDOR handler + adapter guards + audit → Task 1+2; §11 auth scheme → Task 2. **Deferred rõ:** StrategyId/tradeId ownership + npu F1 (Global Constraints).
- **Placeholder scan:** không có TBD/“xử lý lỗi phù hợp”; mọi step có code/command thật.
- **Type consistency:** `CreateTradeCommand.UserId/Origin` (Task 1) dùng ở Task 2; `AiAgentController.DocVersion`/`GetDoc` (Task 4) khớp test; tên embedded resource `InvestmentApp.Api.Docs.AI-Agent-TradePlan-API.md` dùng nhất quán Task 3/4/5.
