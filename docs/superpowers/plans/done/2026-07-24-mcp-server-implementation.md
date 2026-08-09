# MCP Server (co-host trong InvestmentApp.Api) Implementation Plan

> **✅ DONE — merged as PR #129 (squash `e4973c1`, 2026-07-25).** 29 tools shipped (full parity with the AiAgent* surface), 36 MCP tests + 158 total pass. Follow-up (more/new tools): [`2026-07-25-mcp-tools-expansion-roadmap.md`](../2026-07-25-mcp-tools-expansion-roadmap.md).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose the **full** existing AI-agent operation surface (29 tools = 1:1 parity with every `AiAgent*Controller` endpoint) as schema-typed MCP tools over remote streamable HTTP, co-hosted inside `InvestmentApp.Api`, behind the existing ApiKey scheme, dispatching to the same MediatR handlers the REST agent controllers already use. Genuinely new operations are deferred to a later plan (see "Future work").

**Architecture:** New `Mcp/` folder in `InvestmentApp.Api` holds static tool classes (`[McpServerToolType]`), one per resource (mirroring one `AiAgent*Controller` each). Each `[McpServerTool]` method injects `IMediator`/`IFeeCalculationService` + `IHttpContextAccessor` via DI, resolves `UserId` from the `sub` claim (mirroring `AiAgentControllerBase.GetUserId()`), sets path-equivalent params, and re-dispatches an existing command/query. No new business logic, no new DTOs. MCP is additive — REST `/api/v1/ai/agent/*` is untouched. HTTP transport runs in **stateless mode** so it survives Cloud Run multi-instance scaling.

**Tech Stack:** .NET 9, `ModelContextProtocol.AspNetCore` SDK, MediatR, MongoDB, xUnit + FluentAssertions + Moq, `Microsoft.AspNetCore.Mvc.Testing` (new, for integration test).

## Global Constraints

- Target framework: `net9.0` (both `InvestmentApp.Api` and the test project).
- **No new business logic, command, query, or DTO** — every tool re-dispatches an existing MediatR handler (or `AgentTradeFeeCalculator` for fees). Reuse 100%. Each tool mirrors exactly one controller action already present in `src/InvestmentApp.Api/Controllers/AiAgent*Controller.cs`.
- **Every tool sets `UserId = sub` before dispatch** — same mechanism as `AiAgentControllerBase.GetUserId()` (`src/InvestmentApp.Api/Controllers/AiAgentControllerBase.cs:18`): `HttpContext.User.FindFirst("sub")`.
- **Commands passed as a whole tool argument:** `[JsonIgnore]`-marked properties (`UserId`, and `Id` on some commands) are excluded from the generated tool schema by the SDK's System.Text.Json metadata — the server sets them. Path-equivalent params (`id`, `symbol`) are taken as explicit tool params and assigned server-side, exactly like the `[Route]` params in the controllers.
- **JSON keys are PascalCase / case-sensitive** at the boundary (DTOs already are).
- **`create_trade` fee model = fee EXCLUDES tax (ADR-0006).** `Fee = TransactionFee + Vat`; `Tax = Breakdown.Tax` stored separately. Mirror `AiAgentController.CreateTrade` (`AiAgentController.cs:105-115`) verbatim.
- **Read tools carry `ReadOnly = true`; every mutating tool (create/update/delete/import/status) carries `Destructive = true`** so MCP hosts prompt for confirmation on writes (spec §7).
- `create_trade_plan` forces `Status = null` + `TradeId = null` (agent cannot one-shot execute — ADR-0004). `set_trade_plan_status` rejects `"restore"`. `list_journal_entries_by_symbol` requires a non-empty `symbol`.
- The REST `/doc` markdown endpoint is **not** mirrored — MCP `tools/list` is the discovery mechanism that replaces it.
- REST agent surface, frontend, and existing tests stay unchanged.
- SDK API churns fast — Task 1 pins + verifies the exact API surface before any tool is written.

---

## Tool Inventory (29 — full parity)

| # | Tool | MediatR type → return | Kind | Mirrors |
|---|------|----------------------|------|---------|
| 1 | `list_trade_plans` | `GetTradePlansQuery` → `IEnumerable<TradePlanDto>` | read | AiAgentController |
| 2 | `get_trade_plan` | `GetTradePlanByIdQuery` → `TradePlanDto?` | read | " |
| 3 | `create_trade_plan` | `CreateTradePlanCommand` → `string` | write | " |
| 4 | `update_trade_plan` | `UpdateTradePlanCommand` → `Unit` | write | " |
| 5 | `set_trade_plan_status` | `UpdateTradePlanStatusCommand` → `Unit` | write | " |
| 6 | `create_trade` | `CreateTradeCommand` → `string` | write | " |
| 7 | `list_portfolios` | `GetAllPortfoliosQuery` → `List<PortfolioSummaryDto>` | read | AiAgentPortfoliosController |
| 8 | `list_positions` | `GetActivePositionsQuery` → `List<ActivePositionDto>` | read | AiAgentPositionsController |
| 9 | `calculate_fees` | `AgentTradeFeeCalculator.Calculate` → `FeeCalculationResponse` | read | AiAgentFeesController |
| 10 | `get_symbol_timeline` | `GetSymbolTimelineQuery` → `SymbolTimelineDto` | read | AiAgentSymbolsController |
| 11 | `list_watchlists` | `GetWatchlistsQuery` → `List<WatchlistDto>` | read | AiAgentWatchlistsController |
| 12 | `get_watchlist` | `GetWatchlistDetailQuery` → `WatchlistDetailDto` | read | " |
| 13 | `create_watchlist` | `CreateWatchlistCommand` → `WatchlistDto` | write | " |
| 14 | `update_watchlist` | `UpdateWatchlistCommand` → `Unit` | write | " |
| 15 | `delete_watchlist` | `DeleteWatchlistCommand` → `Unit` | write | " |
| 16 | `add_watchlist_item` | `AddWatchlistItemCommand` → `WatchlistDetailDto` | write | " |
| 17 | `update_watchlist_item` | `UpdateWatchlistItemCommand` → `WatchlistDetailDto` | write | " |
| 18 | `remove_watchlist_item` | `RemoveWatchlistItemCommand` → `WatchlistDetailDto` | write | " |
| 19 | `import_vn30` | `ImportVn30Command` → `WatchlistDetailDto` | write | " |
| 20 | `list_journals` | `GetJournalsQuery` → `IEnumerable<JournalDto>` | read | AiAgentJournalsController |
| 21 | `get_journal_by_trade` | `GetJournalByTradeQuery` → `JournalDto?` | read | " |
| 22 | `create_journal` | `CreateJournalCommand` → `string` | write | " |
| 23 | `update_journal` | `UpdateJournalCommand` → `Unit` | write | " |
| 24 | `delete_journal` | `DeleteJournalCommand` → `Unit` | write | " |
| 25 | `create_journal_entry` | `CreateJournalEntryCommand` → `string` | write | AiAgentJournalEntriesController |
| 26 | `update_journal_entry` | `UpdateJournalEntryCommand` → `bool` | write | " |
| 27 | `delete_journal_entry` | `DeleteJournalEntryCommand` → `bool` | write | " |
| 28 | `list_trades_pending_review` | `GetTradesPendingReviewQuery` → `List<PendingReviewTradeDto>` | read | " |
| 29 | `list_journal_entries_by_symbol` | `GetJournalEntriesBySymbolQuery` → `List<JournalEntryDto>` | read | " |

**Verified signatures (namespace | IRequest<T> | settable path/user props):**
- `GetTradePlansQuery` (`…TradePlans.Queries.GetTradePlans`) `→ IEnumerable<TradePlanDto>` | UserId, ActiveOnly
- `GetTradePlanByIdQuery` (same ns) `→ TradePlanDto?` | Id, UserId
- `CreateTradePlanCommand` (`…TradePlans.Commands.CreateTradePlan`) `→ string` | UserId, Status, TradeId
- `UpdateTradePlanCommand` (`…TradePlans.Commands.UpdateTradePlan`) `→ Unit` | Id, UserId
- `UpdateTradePlanStatusCommand` (`…TradePlans.Commands.UpdateTradePlanStatus`) `→ Unit` | Id, UserId, Status, TradeId
- `CreateTradeCommand` (`…Trades.Commands.CreateTrade`) `→ string` | UserId, Origin, PortfolioId, Symbol, TradeType, Quantity, Price, Fee, Tax, TradeDate
- `GetAllPortfoliosQuery` (`…Portfolios.Queries.GetAllPortfolios`) `→ List<PortfolioSummaryDto>` | UserId
- `GetActivePositionsQuery` (`…TradePlans.Queries.GetActivePositions`) `→ List<ActivePositionDto>` | UserId, PortfolioId
- `GetSymbolTimelineQuery` (`…JournalEntries.Queries.GetSymbolTimeline`) `→ SymbolTimelineDto` | UserId, Symbol, From, To
- `GetWatchlistsQuery` (`…Watchlists.Queries.GetWatchlists`) `→ List<WatchlistDto>` | UserId
- `GetWatchlistDetailQuery` (`…Watchlists.Queries.GetWatchlistDetail`) `→ WatchlistDetailDto` | Id, UserId
- `CreateWatchlistCommand` (`…Watchlists.Commands.CreateWatchlist`) `→ WatchlistDto` (has `.Id`) | UserId
- `UpdateWatchlistCommand` (`…Watchlists.Commands.UpdateWatchlist`) `→ Unit` | Id, UserId
- `DeleteWatchlistCommand` (`…Watchlists.Commands.DeleteWatchlist`) `→ Unit` | Id, UserId
- `AddWatchlistItemCommand` (`…Watchlists.Commands.AddWatchlistItem`) `→ WatchlistDetailDto` | WatchlistId, UserId, Symbol
- `UpdateWatchlistItemCommand` (`…Watchlists.Commands.UpdateWatchlistItem`) `→ WatchlistDetailDto` | WatchlistId, Symbol, UserId
- `RemoveWatchlistItemCommand` (`…Watchlists.Commands.RemoveWatchlistItem`) `→ WatchlistDetailDto` | WatchlistId, Symbol, UserId
- `ImportVn30Command` (`…Watchlists.Commands.ImportVn30`) `→ WatchlistDetailDto` | UserId
- `GetJournalsQuery` (`…Journals.Queries.GetJournals`) `→ IEnumerable<JournalDto>` | UserId, PortfolioId
- `GetJournalByTradeQuery` (`…Journals.Queries.GetJournalByTrade`) `→ JournalDto?` | TradeId, UserId
- `CreateJournalCommand` (`…Journals.Commands.CreateJournal`) `→ string` | UserId
- `UpdateJournalCommand` (`…Journals.Commands.UpdateJournal`) `→ Unit` | Id, UserId
- `DeleteJournalCommand` (`…Journals.Commands.DeleteJournal`) `→ Unit` | Id, UserId
- `CreateJournalEntryCommand` (`…JournalEntries.Commands.CreateJournalEntry`) `→ string` | UserId
- `UpdateJournalEntryCommand` (`…JournalEntries.Commands.UpdateJournalEntry`) `→ bool` | Id, UserId
- `DeleteJournalEntryCommand` (`…JournalEntries.Commands.DeleteJournalEntry`) `→ bool` | Id, UserId
- `GetTradesPendingReviewQuery` (`…Journals.Queries.GetTradesPendingReview`) `→ List<PendingReviewTradeDto>` | UserId, PortfolioId
- `GetJournalEntriesBySymbolQuery` (`…JournalEntries.Queries.GetJournalEntriesBySymbol`) `→ List<JournalEntryDto>` | UserId, Symbol, From, To
- `AgentTradeFeeCalculator.Calculate(IFeeCalculationService, string? tradeType, decimal quantity, decimal price) → FeeCalculationResponse { TransactionFee, Vat, Breakdown.Tax }` (`src/InvestmentApp.Api/Controllers/AgentTradeFeeCalculator.cs`)

**Infra facts (verbatim):** ApiKey scheme `= "ApiKey"`, header `X-Api-Key`, claim `sub` = UserId (`src/InvestmentApp.Api/Auth/ApiKeyAuthExtensions.cs:16-20,73-79`). Middleware order (`Program.cs:456-460`): `UseAuthentication` → `ImpersonationValidationMiddleware` → `UseAuthorization` → `MapControllers`. Test-project is unit-only (no `WebApplicationFactory`); `Program` has no partial marker.

---

## File Structure

**Create (tools):** `src/InvestmentApp.Api/Mcp/`
- `McpUserContext.cs` — `GetUserId(this IHttpContextAccessor)` extension.
- `TradePlanTools.cs` — tools 1–5.
- `TradeTools.cs` — tools 6, 9 (create_trade + calculate_fees).
- `PortfolioTools.cs` — tools 7, 8 (portfolios + positions).
- `SymbolTools.cs` — tool 10.
- `WatchlistTools.cs` — tools 11–19.
- `JournalTools.cs` — tools 20–24.
- `JournalEntryTools.cs` — tools 25–29.

**Create (tests):** `tests/InvestmentApp.Api.Tests/Mcp/`
- `McpTestContext.cs` — fake `IHttpContextAccessor` with `sub` claim + a Moq dispatch-capture helper.
- one `*ToolsTests.cs` per tool class.
- `McpEndpointIntegrationTests.cs` — auth-401 + `tools/list` discovery.

**Modify:** `src/InvestmentApp.Api/InvestmentApp.Api.csproj` (+MCP pkg), `src/InvestmentApp.Api/Program.cs` (register 7 tool classes + `MapMcp` + `Program` marker), `tests/InvestmentApp.Api.Tests/InvestmentApp.Api.Tests.csproj` (+Mvc.Testing), `docs/architecture.md`, `docs/business-domain.md`, `frontend/src/assets/docs/*`, `frontend/src/assets/CHANGELOG.md`.

---

## Task 1: Wire the MCP endpoint (package + stateless HTTP + ApiKey auth)

Resolves risks: **Cloud Run multi-instance** (stateless), **auth binding** (explicit `RequireAuthorization`). Deliverable: `/mcp` mounted, 401 without a key.

**Files:** Modify `InvestmentApp.Api.csproj`, `Program.cs`, `InvestmentApp.Api.Tests.csproj`; Create `McpEndpointIntegrationTests.cs` + 7 empty tool-class shells.

**Interfaces:** Produces a mapped `/mcp` behind `ApiKey`, stateless transport, `IHttpContextAccessor` in DI, `public partial class Program;` marker.

- [ ] **Step 1: Verify + pin the SDK API surface** (learned lesson: verify plan APIs before scaffolding)

Run:
```bash
cd src/InvestmentApp.Api
dotnet add package ModelContextProtocol.AspNetCore --prerelease
```
Confirm these symbols in the resolved version; if names drifted, adapt + note in the checkpoint:
- `builder.Services.AddMcpServer()` chainable
- `.WithHttpTransport(Action<HttpServerTransportOptions>)` with a `Stateless` bool
- `.WithTools<T>()`
- `app.MapMcp(string)` returning an endpoint builder supporting `.RequireAuthorization(...)`
- attributes `[McpServerToolType]`, `[McpServerTool]` with bool props `ReadOnly` / `Destructive` / `Name`; `[System.ComponentModel.Description]` for descriptions

Record the resolved package version in the commit message.

- [ ] **Step 2: Add integration-test package + Program marker**

Add to `tests/InvestmentApp.Api.Tests/InvestmentApp.Api.Tests.csproj` inside the `PackageReference` `<ItemGroup>`:
```xml
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
```
Append to the end of `src/InvestmentApp.Api/Program.cs`:
```csharp

public partial class Program;
```

- [ ] **Step 3: Write the failing integration test**

Create `tests/InvestmentApp.Api.Tests/Mcp/McpEndpointIntegrationTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InvestmentApp.Api.Tests.Mcp;

public class McpEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public McpEndpointIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Mcp_Endpoint_Without_ApiKey_Returns_401()
    {
        var client = _factory.CreateClient();
        var body = new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } };
        var res = await client.PostAsJsonAsync("/mcp", body);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter Mcp_Endpoint_Without_ApiKey_Returns_401`
Expected: FAIL — `/mcp` not mapped (404).

- [ ] **Step 5: Register + map**

In `Program.cs` service section (after auth registration):
```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true) // Cloud Run: no cross-instance session state
    .WithTools<InvestmentApp.Api.Mcp.TradePlanTools>()
    .WithTools<InvestmentApp.Api.Mcp.TradeTools>()
    .WithTools<InvestmentApp.Api.Mcp.PortfolioTools>()
    .WithTools<InvestmentApp.Api.Mcp.SymbolTools>()
    .WithTools<InvestmentApp.Api.Mcp.WatchlistTools>()
    .WithTools<InvestmentApp.Api.Mcp.JournalTools>()
    .WithTools<InvestmentApp.Api.Mcp.JournalEntryTools>();
```
After `app.MapControllers();` (`Program.cs:460`):
```csharp
app.MapMcp("/mcp")
    .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
    {
        AuthenticationSchemes = InvestmentApp.Api.Auth.ApiKeyAuthenticationDefaults.Scheme
    });
```

- [ ] **Step 6: Add 7 empty tool-class shells so Program.cs compiles**

Create each of `TradePlanTools.cs`, `TradeTools.cs`, `PortfolioTools.cs`, `SymbolTools.cs`, `WatchlistTools.cs`, `JournalTools.cs`, `JournalEntryTools.cs` in `src/InvestmentApp.Api/Mcp/` with:
```csharp
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class TradePlanTools { }  // rename class per file
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter Mcp_Endpoint_Without_ApiKey_Returns_401`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/InvestmentApp.Api/ tests/InvestmentApp.Api.Tests/
git commit -m "feat(mcp): mount stateless MCP endpoint at /mcp behind ApiKey scheme"
```

---

## Task 2: UserId resolution + test infra + Portfolio/Position read tools (7, 8)

Resolves risk: **`HttpContext.User` populated inside a tool** — proven before scaling to 29 tools.

**Files:** Create `Mcp/McpUserContext.cs`, `tests/…/Mcp/McpTestContext.cs`, `tests/…/Mcp/PortfolioToolsTests.cs`; Modify `Mcp/PortfolioTools.cs`.

**Interfaces:** Produces `McpUserContext.GetUserId(this IHttpContextAccessor) → string` (throws `UnauthorizedAccessException` if no `sub`); `McpTestContext.WithUser(string) → IHttpContextAccessor` + `McpTestContext.Capture<TResponse,TConcrete>(mock, out getter, returns)` helper; `PortfolioTools.ListPortfolios`, `PortfolioTools.ListPositions`.

- [ ] **Step 1: Create the UserId extension**

`src/InvestmentApp.Api/Mcp/McpUserContext.cs`:
```csharp
using Microsoft.AspNetCore.Http;

namespace InvestmentApp.Api.Mcp;

/// <summary>Resolve UserId cho MCP tool từ claim "sub" — cùng cơ chế AiAgentControllerBase.GetUserId().</summary>
public static class McpUserContext
{
    public static string GetUserId(this IHttpContextAccessor accessor) =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Thiếu claim 'sub' — API key không hợp lệ.");
}
```

- [ ] **Step 2: Create the test helper**

`tests/InvestmentApp.Api.Tests/Mcp/McpTestContext.cs`:
```csharp
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>Fake IHttpContextAccessor mang claim "sub" (mirror AiAgentControllerTests.Sut()) + Moq capture helper.</summary>
public static class McpTestContext
{
    public static IHttpContextAccessor WithUser(string userId = "user-1")
    {
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId) }, "ApiKey");
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextAccessor { HttpContext = ctx };
    }

    /// <summary>Setup IMediator.Send to capture the dispatched command of type TConcrete and return `returns`.</summary>
    public static void Capture<TResponse, TConcrete>(
        Mock<IMediator> mock, out Func<TConcrete?> sent, TResponse returns)
        where TConcrete : class
    {
        TConcrete? captured = null;
        mock.Setup(m => m.Send(It.IsAny<TConcrete>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((c, _) => captured = c as TConcrete)
            .ReturnsAsync(returns);
        sent = () => captured;
    }
}
```
> Note: `Send(IRequest<T>, CancellationToken)` — the `It.IsAny<TConcrete>()` overload binds fine because `TConcrete : IRequest<TResponse>`. The `Callback<object, CancellationToken>` avoids the strict-generic pitfall (see memory `learning_toolquirk_moq_mediatr_send_callback`).

- [ ] **Step 3: Write the failing tests**

`tests/InvestmentApp.Api.Tests/Mcp/PortfolioToolsTests.cs`:
```csharp
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.TradePlans.Queries.GetActivePositions;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class PortfolioToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task ListPortfolios_SetsUserId()
    {
        McpTestContext.Capture<List<PortfolioSummaryDto>, GetAllPortfoliosQuery>(
            _mediator, out var sent, new List<PortfolioSummaryDto>());
        await PortfolioTools.ListPortfolios(_mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task ListPositions_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<List<ActivePositionDto>, GetActivePositionsQuery>(
            _mediator, out var sent, new List<ActivePositionDto>());
        await PortfolioTools.ListPositions("p1", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-2");
        sent()!.PortfolioId.Should().Be("p1");
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter PortfolioToolsTests`
Expected: FAIL — methods not defined.

- [ ] **Step 5: Implement PortfolioTools**

`src/InvestmentApp.Api/Mcp/PortfolioTools.cs`:
```csharp
using System.ComponentModel;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.TradePlans.Queries.GetActivePositions;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class PortfolioTools
{
    [McpServerTool(Name = "list_portfolios", ReadOnly = true)]
    [Description("Liệt kê danh mục đầu tư của chủ khóa API (id + tên). Dùng để lấy portfolioId trước khi ghi lệnh.")]
    public static async Task<List<PortfolioSummaryDto>> ListPortfolios(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetAllPortfoliosQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "list_positions", ReadOnly = true)]
    [Description("Liệt kê vị thế (holdings) đang mở. portfolioId tùy chọn để lọc theo danh mục.")]
    public static async Task<List<ActivePositionDto>> ListPositions(
        [Description("ID danh mục cần lọc (bỏ trống = tất cả).")] string? portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetActivePositionsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter PortfolioToolsTests`
Expected: PASS (2).

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Api/Mcp/McpUserContext.cs src/InvestmentApp.Api/Mcp/PortfolioTools.cs tests/InvestmentApp.Api.Tests/Mcp/
git commit -m "feat(mcp): UserId-from-sub resolution + list_portfolios/list_positions tools"
```

---

## Task 3: TradePlanTools (tools 1–5)

**Files:** Modify `Mcp/TradePlanTools.cs`; Create `tests/…/Mcp/TradePlanToolsTests.cs`.

**Interfaces:** Produces `ListTradePlans`, `GetTradePlan`, `CreateTradePlan`, `UpdateTradePlan`, `SetTradePlanStatus`.

- [ ] **Step 1: Write the failing tests**

`tests/InvestmentApp.Api.Tests/Mcp/TradePlanToolsTests.cs`:
```csharp
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class TradePlanToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task ListTradePlans_SetsUserId_AndActiveOnly()
    {
        McpTestContext.Capture<IEnumerable<TradePlanDto>, GetTradePlansQuery>(_mediator, out var sent, Array.Empty<TradePlanDto>());
        await TradePlanTools.ListTradePlans(true, _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.ActiveOnly.Should().BeTrue();
    }

    [Fact]
    public async Task GetTradePlan_SetsIdAndUserId()
    {
        McpTestContext.Capture<TradePlanDto?, GetTradePlanByIdQuery>(_mediator, out var sent, null);
        await TradePlanTools.GetTradePlan("plan-9", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.Id.Should().Be("plan-9");
        sent()!.UserId.Should().Be("u-2");
    }

    [Fact]
    public async Task CreateTradePlan_ForcesDraft_AndSetsUserId()
    {
        McpTestContext.Capture<string, CreateTradePlanCommand>(_mediator, out var sent, "plan-new");
        var id = await TradePlanTools.CreateTradePlan(
            new CreateTradePlanCommand { Symbol = "VNM", Status = "Executed", TradeId = "t-x" },
            _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);
        id.Should().Be("plan-new");
        sent()!.Status.Should().BeNull();
        sent()!.TradeId.Should().BeNull();
        sent()!.UserId.Should().Be("u-3");
    }

    [Fact]
    public async Task UpdateTradePlan_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, UpdateTradePlanCommand>(_mediator, out var sent, Unit.Value);
        await TradePlanTools.UpdateTradePlan("plan-1", new UpdateTradePlanCommand { Symbol = "SSI" },
            _mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None);
        sent()!.Id.Should().Be("plan-1");
        sent()!.UserId.Should().Be("u-4");
    }

    [Fact]
    public async Task SetTradePlanStatus_Restore_Throws()
    {
        var act = async () => await TradePlanTools.SetTradePlanStatus(
            "plan-1", "restore", null, _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetTradePlanStatus_Executed_Dispatches()
    {
        McpTestContext.Capture<Unit, UpdateTradePlanStatusCommand>(_mediator, out var sent, Unit.Value);
        await TradePlanTools.SetTradePlanStatus("plan-1", "executed", "t-1",
            _mediator.Object, McpTestContext.WithUser("u-6"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-6");
        sent()!.Id.Should().Be("plan-1");
        sent()!.Status.Should().Be("executed");
    }
}
```

- [ ] **Step 2: Run to verify fail** — `dotnet test tests/InvestmentApp.Api.Tests --filter TradePlanToolsTests` → FAIL.

- [ ] **Step 3: Implement TradePlanTools**

`src/InvestmentApp.Api/Mcp/TradePlanTools.cs`:
```csharp
using System.ComponentModel;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class TradePlanTools
{
    [McpServerTool(Name = "list_trade_plans", ReadOnly = true)]
    [Description("Liệt kê kế hoạch giao dịch. activeOnly = true chỉ lấy kế hoạch đang hiệu lực.")]
    public static async Task<IEnumerable<TradePlanDto>> ListTradePlans(
        [Description("Chỉ lấy kế hoạch đang hoạt động.")] bool activeOnly,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTradePlansQuery { UserId = http.GetUserId(), ActiveOnly = activeOnly }, ct);

    [McpServerTool(Name = "get_trade_plan", ReadOnly = true)]
    [Description("Lấy chi tiết một kế hoạch giao dịch theo id. Null nếu không tồn tại/không thuộc chủ khóa.")]
    public static async Task<TradePlanDto?> GetTradePlan(
        [Description("ID kế hoạch.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTradePlanByIdQuery { Id = id, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "create_trade_plan", Destructive = true)]
    [Description("Tạo kế hoạch giao dịch mới. Luôn tạo ở trạng thái Nháp (Draft) — agent không tự khớp lệnh.")]
    public static async Task<string> CreateTradePlan(
        CreateTradePlanCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.UserId = http.GetUserId();
        command.Status = null;   // ép Draft (ADR-0004)
        command.TradeId = null;
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "update_trade_plan", Destructive = true)]
    [Description("Cập nhật một kế hoạch giao dịch theo id.")]
    public static async Task<string> UpdateTradePlan(
        [Description("ID kế hoạch.")] string id,
        UpdateTradePlanCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.Id = id;
        command.UserId = http.GetUserId();
        await mediator.Send(command, ct);
        return "ok";
    }

    [McpServerTool(Name = "set_trade_plan_status", Destructive = true)]
    [Description("Đổi trạng thái kế hoạch. 'restore' bị chặn qua MCP.")]
    public static async Task<string> SetTradePlanStatus(
        [Description("ID kế hoạch.")] string id,
        [Description("Trạng thái mới (vd: executed, cancelled).")] string status,
        [Description("ID lệnh liên kết nếu chuyển executed (tùy chọn).")] string? tradeId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (string.Equals(status, "restore", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("restore không được phép qua MCP surface.");
        await mediator.Send(new UpdateTradePlanStatusCommand
        {
            Id = id, UserId = http.GetUserId(), Status = status, TradeId = tradeId
        }, ct);
        return "ok";
    }
}
```

- [ ] **Step 4: Run to verify pass** — `dotnet test tests/InvestmentApp.Api.Tests --filter TradePlanToolsTests` → PASS (6).

- [ ] **Step 5: Commit** — `git commit -m "feat(mcp): trade-plan tools (list/get/create/update/set-status)"`

---

## Task 4: TradeTools (tools 6, 9 — create_trade + calculate_fees)

**Files:** Modify `Mcp/TradeTools.cs`; Create `tests/…/Mcp/TradeToolsTests.cs`.

**Interfaces:** Consumes `AgentTradeFeeCalculator.Calculate`, `GetAllPortfoliosQuery`. Produces `CalculateFees`, `CreateTrade`.

- [ ] **Step 1: Write the failing tests**

`tests/InvestmentApp.Api.Tests/Mcp/TradeToolsTests.cs`:
```csharp
using InvestmentApp.Api.Controllers;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using InvestmentApp.Domain.ValueObjects;   // verify: match AiAgentControllerTests usings for Money/SecurityType/TradingFeesSummary
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class TradeToolsTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IFeeCalculationService> _fees = new();

    private void SetupFees()
    {
        _fees.Setup(f => f.GetFeesSummary(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(new TradingFeesSummary { TransactionFee = new Money(150000, "VND") });
        _fees.Setup(f => f.CalculateVAT(It.IsAny<Money>(), It.IsAny<string>())).Returns(new Money(15000, "VND"));
        _fees.Setup(f => f.CalculateSecuritiesTax(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>()))
            .Returns((Money amt, SecurityType _, bool isBuy) => new Money(isBuy ? 0m : amt.Amount * 0.001m, "VND"));
    }

    [Fact]
    public void CalculateFees_ReturnsBrokerCostAndTax_Separately()
    {
        SetupFees();
        var r = TradeTools.CalculateFees("SELL", 100, 1000000, _fees.Object);
        r.TransactionFee.Should().Be(150000);
        r.Vat.Should().Be(15000);
        r.Breakdown.Tax.Should().Be(100000);
    }

    [Fact]
    public async Task CreateTrade_SinglePortfolio_AutoResolves_FeeExclTax()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetAllPortfoliosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PortfolioSummaryDto> { new() { Id = "only-p", Name = "Chính" } });
        SetupFees();
        McpTestContext.Capture<string, CreateTradeCommand>(_mediator, out var sent, "trade-1");

        var id = await TradeTools.CreateTrade(null, "HHV", "SELL", 100, 1000000, null, null, null,
            _mediator.Object, _fees.Object, McpTestContext.WithUser("u-9"), CancellationToken.None);

        id.Should().Be("trade-1");
        sent()!.PortfolioId.Should().Be("only-p");
        sent()!.Origin.Should().Be("AI_AGENT");
        sent()!.Fee.Should().Be(165000);   // TransactionFee + Vat (excl tax)
        sent()!.Tax.Should().Be(100000);   // stored separately
        sent()!.UserId.Should().Be("u-9");
    }

    [Fact]
    public async Task CreateTrade_MultiplePortfolios_Throws()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetAllPortfoliosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PortfolioSummaryDto> { new() { Id = "a", Name = "A" }, new() { Id = "b", Name = "B" } });
        var act = async () => await TradeTools.CreateTrade(null, "HHV", "BUY", 100, 1000, null, null, null,
            _mediator.Object, _fees.Object, McpTestContext.WithUser("u-9"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run to verify fail** — `dotnet test tests/InvestmentApp.Api.Tests --filter TradeToolsTests` → FAIL.

- [ ] **Step 3: Implement TradeTools**

`src/InvestmentApp.Api/Mcp/TradeTools.cs`:
```csharp
using System.ComponentModel;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class TradeTools
{
    [McpServerTool(Name = "calculate_fees", ReadOnly = true)]
    [Description("Ước tính phí + thuế cho một lệnh (BUY/SELL). Không ghi dữ liệu.")]
    public static FeeCalculationResponse CalculateFees(
        [Description("Loại lệnh: BUY hoặc SELL.")] string tradeType,
        [Description("Khối lượng.")] decimal quantity,
        [Description("Giá mỗi cổ phiếu (VND).")] decimal price,
        IFeeCalculationService feeService)
        => AgentTradeFeeCalculator.Calculate(feeService, tradeType, quantity, price);

    [McpServerTool(Name = "create_trade", Destructive = true)]
    [Description("Ghi một lệnh thật. Bỏ trống portfolioId để tự chọn (khi chỉ có 1 danh mục); bỏ trống fee/tax để tự tính (fee KHÔNG gồm thuế).")]
    public static async Task<string> CreateTrade(
        [Description("ID danh mục. Bỏ trống = tự chọn nếu chỉ 1 danh mục.")] string? portfolioId,
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Loại lệnh: BUY hoặc SELL.")] string tradeType,
        [Description("Khối lượng.")] decimal quantity,
        [Description("Giá mỗi cổ phiếu (VND).")] decimal price,
        [Description("Phí môi giới (VND). Bỏ trống = tự tính.")] decimal? fee,
        [Description("Thuế TNCN (VND). Bỏ trống = tự tính.")] decimal? tax,
        [Description("Ngày giao dịch (bỏ trống = hôm nay).")] DateTime? tradeDate,
        IMediator mediator, IFeeCalculationService feeService, IHttpContextAccessor http, CancellationToken ct)
    {
        var userId = http.GetUserId();

        if (string.IsNullOrWhiteSpace(portfolioId))
        {
            var portfolios = await mediator.Send(new GetAllPortfoliosQuery { UserId = userId }, ct);
            if (portfolios.Count == 0)
                throw new InvalidOperationException("Chưa có danh mục nào — tạo danh mục trước khi ghi lệnh.");
            if (portfolios.Count > 1)
                throw new InvalidOperationException(
                    "Có nhiều danh mục — cần chỉ định portfolioId. Danh mục: " +
                    string.Join(", ", portfolios.Select(p => $"{p.Name} ({p.Id})")));
            portfolioId = portfolios[0].Id;
        }

        var resolvedFee = fee ?? 0m;
        var resolvedTax = tax ?? 0m;
        if (fee is null || tax is null)
        {
            var calc = AgentTradeFeeCalculator.Calculate(feeService, tradeType, quantity, price);
            // Fee = broker cost only (TransactionFee + VAT); Tax stored SEPARATELY (ADR-0006) — no double-count.
            if (fee is null) resolvedFee = calc.TransactionFee + calc.Vat;
            if (tax is null) resolvedTax = calc.Breakdown.Tax;
        }

        return await mediator.Send(new CreateTradeCommand
        {
            UserId = userId, Origin = "AI_AGENT", PortfolioId = portfolioId,
            Symbol = symbol, TradeType = tradeType, Quantity = quantity, Price = price,
            Fee = resolvedFee, Tax = resolvedTax, TradeDate = tradeDate
        }, ct);
    }
}
```

- [ ] **Step 4: Run to verify pass** — `dotnet test tests/InvestmentApp.Api.Tests --filter TradeToolsTests` → PASS (3).

- [ ] **Step 5: Commit** — `git commit -m "feat(mcp): create_trade (auto-resolve, fee excl tax) + calculate_fees tools"`

---

## Task 5: SymbolTools (tool 10 — get_symbol_timeline)

**Files:** Modify `Mcp/SymbolTools.cs`; Create `tests/…/Mcp/SymbolToolsTests.cs`.

**Interfaces:** Produces `GetSymbolTimeline`.

- [ ] **Step 1: Write the failing test**

`tests/InvestmentApp.Api.Tests/Mcp/SymbolToolsTests.cs`:
```csharp
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class SymbolToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetSymbolTimeline_SetsUserId_Symbol_Range()
    {
        McpTestContext.Capture<SymbolTimelineDto, GetSymbolTimelineQuery>(_mediator, out var sent, new SymbolTimelineDto());
        var from = new DateTime(2026, 1, 1);
        await SymbolTools.GetSymbolTimeline("VNM", from, null, _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.Symbol.Should().Be("VNM");
        sent()!.From.Should().Be(from);
    }
}
```
> If `SymbolTimelineDto` has no public parameterless ctor, replace `new SymbolTimelineDto()` with `null!` (return value is unused by the assertions).

- [ ] **Step 2: Run to verify fail** — `--filter SymbolToolsTests` → FAIL.

- [ ] **Step 3: Implement SymbolTools**

`src/InvestmentApp.Api/Mcp/SymbolTools.cs`:
```csharp
using System.ComponentModel;
using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class SymbolTools
{
    [McpServerTool(Name = "get_symbol_timeline", ReadOnly = true)]
    [Description("Dòng thời gian sự kiện (trade/nhật ký) theo mã chứng khoán, trong khoảng from–to tùy chọn.")]
    public static async Task<SymbolTimelineDto> GetSymbolTimeline(
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Từ ngày (tùy chọn).")] DateTime? from,
        [Description("Đến ngày (tùy chọn).")] DateTime? to,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetSymbolTimelineQuery
        {
            UserId = http.GetUserId(), Symbol = symbol, From = from, To = to
        }, ct);
}
```

- [ ] **Step 4: Run to verify pass** — `--filter SymbolToolsTests` → PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(mcp): get_symbol_timeline tool"`

---

## Task 6: WatchlistTools (tools 11–19)

**Files:** Modify `Mcp/WatchlistTools.cs`; Create `tests/…/Mcp/WatchlistToolsTests.cs`.

**Interfaces:** Produces `ListWatchlists`, `GetWatchlist`, `CreateWatchlist`, `UpdateWatchlist`, `DeleteWatchlist`, `AddWatchlistItem`, `UpdateWatchlistItem`, `RemoveWatchlistItem`, `ImportVn30`.

- [ ] **Step 1: Write the failing tests**

`tests/InvestmentApp.Api.Tests/Mcp/WatchlistToolsTests.cs`:
```csharp
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.DeleteWatchlist;
using InvestmentApp.Application.Watchlists.Commands.ImportVn30;
using InvestmentApp.Application.Watchlists.Commands.RemoveWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlistItem;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlistDetail;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlists;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class WatchlistToolsTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly IHttpContextAccessor _http = McpTestContext.WithUser("u-1");

    [Fact]
    public async Task ListWatchlists_SetsUserId()
    {
        McpTestContext.Capture<List<WatchlistDto>, GetWatchlistsQuery>(_mediator, out var sent, new List<WatchlistDto>());
        await WatchlistTools.ListWatchlists(_mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task GetWatchlist_SetsIdAndUserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, GetWatchlistDetailQuery>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.GetWatchlist("w1", _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("w1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task CreateWatchlist_SetsUserId()
    {
        McpTestContext.Capture<WatchlistDto, CreateWatchlistCommand>(_mediator, out var sent, new WatchlistDto());
        await WatchlistTools.CreateWatchlist(new CreateWatchlistCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task UpdateWatchlist_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, UpdateWatchlistCommand>(_mediator, out var sent, Unit.Value);
        await WatchlistTools.UpdateWatchlist("w1", new UpdateWatchlistCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("w1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task DeleteWatchlist_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, DeleteWatchlistCommand>(_mediator, out var sent, Unit.Value);
        await WatchlistTools.DeleteWatchlist("w1", _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("w1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task AddWatchlistItem_SetsWatchlistIdAndUserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, AddWatchlistItemCommand>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.AddWatchlistItem("w1", new AddWatchlistItemCommand { Symbol = "VNM" }, _mediator.Object, _http, CancellationToken.None);
        sent()!.WatchlistId.Should().Be("w1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task UpdateWatchlistItem_SetsWatchlistId_Symbol_UserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, UpdateWatchlistItemCommand>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.UpdateWatchlistItem("w1", "VNM", new UpdateWatchlistItemCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.WatchlistId.Should().Be("w1");
        sent()!.Symbol.Should().Be("VNM");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task RemoveWatchlistItem_SetsWatchlistId_Symbol_UserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, RemoveWatchlistItemCommand>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.RemoveWatchlistItem("w1", "VNM", _mediator.Object, _http, CancellationToken.None);
        sent()!.WatchlistId.Should().Be("w1");
        sent()!.Symbol.Should().Be("VNM");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task ImportVn30_SetsUserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, ImportVn30Command>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.ImportVn30(new ImportVn30Command(), _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }
}
```
> If any DTO/command lacks a public parameterless ctor, swap the `new X()` return placeholder for `null!` — assertions only touch the captured input command, not the return.

- [ ] **Step 2: Run to verify fail** — `--filter WatchlistToolsTests` → FAIL.

- [ ] **Step 3: Implement WatchlistTools**

`src/InvestmentApp.Api/Mcp/WatchlistTools.cs`:
```csharp
using System.ComponentModel;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.DeleteWatchlist;
using InvestmentApp.Application.Watchlists.Commands.ImportVn30;
using InvestmentApp.Application.Watchlists.Commands.RemoveWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlistItem;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlistDetail;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlists;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class WatchlistTools
{
    [McpServerTool(Name = "list_watchlists", ReadOnly = true)]
    [Description("Liệt kê danh sách theo dõi (watchlist) của chủ khóa API.")]
    public static async Task<List<WatchlistDto>> ListWatchlists(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetWatchlistsQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_watchlist", ReadOnly = true)]
    [Description("Chi tiết một watchlist theo id (gồm các mã bên trong).")]
    public static async Task<WatchlistDetailDto> GetWatchlist(
        [Description("ID watchlist.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetWatchlistDetailQuery { Id = id, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "create_watchlist", Destructive = true)]
    [Description("Tạo watchlist mới.")]
    public static async Task<WatchlistDto> CreateWatchlist(
        CreateWatchlistCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "update_watchlist", Destructive = true)]
    [Description("Cập nhật watchlist theo id (đổi tên, mô tả…).")]
    public static async Task<string> UpdateWatchlist(
        [Description("ID watchlist.")] string id,
        UpdateWatchlistCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.Id = id;
        command.UserId = http.GetUserId();
        await mediator.Send(command, ct);
        return "ok";
    }

    [McpServerTool(Name = "delete_watchlist", Destructive = true)]
    [Description("Xóa một watchlist theo id.")]
    public static async Task<string> DeleteWatchlist(
        [Description("ID watchlist.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        await mediator.Send(new DeleteWatchlistCommand { Id = id, UserId = http.GetUserId() }, ct);
        return "ok";
    }

    [McpServerTool(Name = "add_watchlist_item", Destructive = true)]
    [Description("Thêm một mã vào watchlist. Trả về watchlist sau khi cập nhật.")]
    public static async Task<WatchlistDetailDto> AddWatchlistItem(
        [Description("ID watchlist.")] string id,
        AddWatchlistItemCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.WatchlistId = id;
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "update_watchlist_item", Destructive = true)]
    [Description("Cập nhật một mã trong watchlist (ghi chú, mục tiêu…).")]
    public static async Task<WatchlistDetailDto> UpdateWatchlistItem(
        [Description("ID watchlist.")] string id,
        [Description("Mã chứng khoán.")] string symbol,
        UpdateWatchlistItemCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.WatchlistId = id;
        command.Symbol = symbol;
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "remove_watchlist_item", Destructive = true)]
    [Description("Bỏ một mã khỏi watchlist. Trả về watchlist sau khi cập nhật.")]
    public static async Task<WatchlistDetailDto> RemoveWatchlistItem(
        [Description("ID watchlist.")] string id,
        [Description("Mã chứng khoán.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new RemoveWatchlistItemCommand
        {
            WatchlistId = id, Symbol = symbol, UserId = http.GetUserId()
        }, ct);

    [McpServerTool(Name = "import_vn30", Destructive = true)]
    [Description("Nhập toàn bộ rổ VN30 vào watchlist.")]
    public static async Task<WatchlistDetailDto> ImportVn30(
        ImportVn30Command command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }
}
```

- [ ] **Step 4: Run to verify pass** — `--filter WatchlistToolsTests` → PASS (9).

- [ ] **Step 5: Commit** — `git commit -m "feat(mcp): watchlist CRUD + items + import-vn30 tools"`

---

## Task 7: JournalTools (tools 20–24)

**Files:** Modify `Mcp/JournalTools.cs`; Create `tests/…/Mcp/JournalToolsTests.cs`.

**Interfaces:** Produces `ListJournals`, `GetJournalByTrade`, `CreateJournal`, `UpdateJournal`, `DeleteJournal`.

- [ ] **Step 1: Write the failing tests**

`tests/InvestmentApp.Api.Tests/Mcp/JournalToolsTests.cs`:
```csharp
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Application.Journals.Commands.DeleteJournal;
using InvestmentApp.Application.Journals.Commands.UpdateJournal;
using InvestmentApp.Application.Journals.Queries.GetJournalByTrade;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class JournalToolsTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly IHttpContextAccessor _http = McpTestContext.WithUser("u-1");

    [Fact]
    public async Task ListJournals_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<IEnumerable<JournalDto>, GetJournalsQuery>(_mediator, out var sent, Array.Empty<JournalDto>());
        await JournalTools.ListJournals("p1", _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.PortfolioId.Should().Be("p1");
    }

    [Fact]
    public async Task GetJournalByTrade_SetsTradeIdAndUserId()
    {
        McpTestContext.Capture<JournalDto?, GetJournalByTradeQuery>(_mediator, out var sent, null);
        await JournalTools.GetJournalByTrade("t1", _mediator.Object, _http, CancellationToken.None);
        sent()!.TradeId.Should().Be("t1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task CreateJournal_SetsUserId()
    {
        McpTestContext.Capture<string, CreateJournalCommand>(_mediator, out var sent, "j1");
        var id = await JournalTools.CreateJournal(new CreateJournalCommand(), _mediator.Object, _http, CancellationToken.None);
        id.Should().Be("j1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task UpdateJournal_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, UpdateJournalCommand>(_mediator, out var sent, Unit.Value);
        await JournalTools.UpdateJournal("j1", new UpdateJournalCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("j1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task DeleteJournal_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, DeleteJournalCommand>(_mediator, out var sent, Unit.Value);
        await JournalTools.DeleteJournal("j1", _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("j1");
        sent()!.UserId.Should().Be("u-1");
    }
}
```
> `JournalDto` resolves from the `GetJournals` namespace. If the compiler reports it's also declared in `GetJournalByTrade` (ambiguous), keep only the `GetJournals` using and fully-qualify the other query.

- [ ] **Step 2: Run to verify fail** — `--filter JournalToolsTests` → FAIL.

- [ ] **Step 3: Implement JournalTools**

`src/InvestmentApp.Api/Mcp/JournalTools.cs`:
```csharp
using System.ComponentModel;
using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Application.Journals.Commands.DeleteJournal;
using InvestmentApp.Application.Journals.Commands.UpdateJournal;
using InvestmentApp.Application.Journals.Queries.GetJournalByTrade;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class JournalTools
{
    [McpServerTool(Name = "list_journals", ReadOnly = true)]
    [Description("Liệt kê nhật ký giao dịch (journal) gắn với các lệnh. portfolioId tùy chọn để lọc.")]
    public static async Task<IEnumerable<JournalDto>> ListJournals(
        [Description("ID danh mục cần lọc (tùy chọn).")] string? portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetJournalsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_journal_by_trade", ReadOnly = true)]
    [Description("Lấy nhật ký của một lệnh theo tradeId. Null nếu chưa có nhật ký.")]
    public static async Task<JournalDto?> GetJournalByTrade(
        [Description("ID lệnh giao dịch.")] string tradeId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetJournalByTradeQuery { TradeId = tradeId, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "create_journal", Destructive = true)]
    [Description("Tạo nhật ký cho một lệnh.")]
    public static async Task<string> CreateJournal(
        CreateJournalCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "update_journal", Destructive = true)]
    [Description("Cập nhật nhật ký theo id.")]
    public static async Task<string> UpdateJournal(
        [Description("ID nhật ký.")] string id,
        UpdateJournalCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.Id = id;
        command.UserId = http.GetUserId();
        await mediator.Send(command, ct);
        return "ok";
    }

    [McpServerTool(Name = "delete_journal", Destructive = true)]
    [Description("Xóa nhật ký theo id.")]
    public static async Task<string> DeleteJournal(
        [Description("ID nhật ký.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        await mediator.Send(new DeleteJournalCommand { Id = id, UserId = http.GetUserId() }, ct);
        return "ok";
    }
}
```

- [ ] **Step 4: Run to verify pass** — `--filter JournalToolsTests` → PASS (5).

- [ ] **Step 5: Commit** — `git commit -m "feat(mcp): journal CRUD tools"`

---

## Task 8: JournalEntryTools (tools 25–29)

**Files:** Modify `Mcp/JournalEntryTools.cs`; Create `tests/…/Mcp/JournalEntryToolsTests.cs`.

**Interfaces:** Produces `CreateJournalEntry`, `UpdateJournalEntry`, `DeleteJournalEntry`, `ListTradesPendingReview`, `ListJournalEntriesBySymbol`.

- [ ] **Step 1: Write the failing tests**

`tests/InvestmentApp.Api.Tests/Mcp/JournalEntryToolsTests.cs`:
```csharp
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.DeleteJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Application.JournalEntries.Queries.GetJournalEntriesBySymbol;
using InvestmentApp.Application.Journals.Queries.GetTradesPendingReview;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class JournalEntryToolsTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly IHttpContextAccessor _http = McpTestContext.WithUser("u-1");

    [Fact]
    public async Task CreateJournalEntry_SetsUserId()
    {
        McpTestContext.Capture<string, CreateJournalEntryCommand>(_mediator, out var sent, "e1");
        var id = await JournalEntryTools.CreateJournalEntry(new CreateJournalEntryCommand { Symbol = "VNM" }, _mediator.Object, _http, CancellationToken.None);
        id.Should().Be("e1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task UpdateJournalEntry_SetsIdAndUserId()
    {
        McpTestContext.Capture<bool, UpdateJournalEntryCommand>(_mediator, out var sent, true);
        await JournalEntryTools.UpdateJournalEntry("e1", new UpdateJournalEntryCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("e1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task DeleteJournalEntry_SetsIdAndUserId()
    {
        McpTestContext.Capture<bool, DeleteJournalEntryCommand>(_mediator, out var sent, true);
        await JournalEntryTools.DeleteJournalEntry("e1", _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("e1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task ListTradesPendingReview_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<List<PendingReviewTradeDto>, GetTradesPendingReviewQuery>(_mediator, out var sent, new List<PendingReviewTradeDto>());
        await JournalEntryTools.ListTradesPendingReview("p1", _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.PortfolioId.Should().Be("p1");
    }

    [Fact]
    public async Task ListJournalEntriesBySymbol_EmptySymbol_Throws()
    {
        var act = async () => await JournalEntryTools.ListJournalEntriesBySymbol("  ", null, null, _mediator.Object, _http, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ListJournalEntriesBySymbol_SetsUserId_Symbol()
    {
        McpTestContext.Capture<List<JournalEntryDto>, GetJournalEntriesBySymbolQuery>(_mediator, out var sent, new List<JournalEntryDto>());
        await JournalEntryTools.ListJournalEntriesBySymbol("VNM", null, null, _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.Symbol.Should().Be("VNM");
    }
}
```

- [ ] **Step 2: Run to verify fail** — `--filter JournalEntryToolsTests` → FAIL.

- [ ] **Step 3: Implement JournalEntryTools**

`src/InvestmentApp.Api/Mcp/JournalEntryTools.cs`:
```csharp
using System.ComponentModel;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.DeleteJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Application.JournalEntries.Queries.GetJournalEntriesBySymbol;
using InvestmentApp.Application.Journals.Queries.GetTradesPendingReview;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class JournalEntryTools
{
    [McpServerTool(Name = "create_journal_entry", Destructive = true)]
    [Description("Tạo một mục nhật ký theo mã (standalone, không gắn lệnh).")]
    public static async Task<string> CreateJournalEntry(
        CreateJournalEntryCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "update_journal_entry", Destructive = true)]
    [Description("Cập nhật một mục nhật ký theo id. Trả về false nếu không tìm thấy.")]
    public static async Task<bool> UpdateJournalEntry(
        [Description("ID mục nhật ký.")] string id,
        UpdateJournalEntryCommand command, IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        command.Id = id;
        command.UserId = http.GetUserId();
        return await mediator.Send(command, ct);
    }

    [McpServerTool(Name = "delete_journal_entry", Destructive = true)]
    [Description("Xóa một mục nhật ký theo id. Trả về false nếu không tìm thấy.")]
    public static async Task<bool> DeleteJournalEntry(
        [Description("ID mục nhật ký.")] string id,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new DeleteJournalEntryCommand { Id = id, UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "list_trades_pending_review", ReadOnly = true)]
    [Description("Liệt kê các lệnh chưa có nhật ký (cần review). portfolioId tùy chọn để lọc.")]
    public static async Task<List<PendingReviewTradeDto>> ListTradesPendingReview(
        [Description("ID danh mục cần lọc (tùy chọn).")] string? portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTradesPendingReviewQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "list_journal_entries_by_symbol", ReadOnly = true)]
    [Description("Liệt kê mục nhật ký theo mã, trong khoảng from–to tùy chọn. Bắt buộc có symbol.")]
    public static async Task<List<JournalEntryDto>> ListJournalEntriesBySymbol(
        [Description("Mã chứng khoán (bắt buộc).")] string symbol,
        [Description("Từ ngày (tùy chọn).")] DateTime? from,
        [Description("Đến ngày (tùy chọn).")] DateTime? to,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new InvalidOperationException("symbol là bắt buộc.");
        return await mediator.Send(new GetJournalEntriesBySymbolQuery
        {
            UserId = http.GetUserId(), Symbol = symbol, From = from, To = to
        }, ct);
    }
}
```

- [ ] **Step 4: Run to verify pass** — `--filter JournalEntryToolsTests` → PASS (6).

- [ ] **Step 5: Full regression** — `dotnet test tests/InvestmentApp.Api.Tests` → all green.

- [ ] **Step 6: Commit** — `git commit -m "feat(mcp): journal-entry tools + pending-review + by-symbol"`

---

## Task 9: Discovery integration test + real-host smoke + docs

Resolves risks: **annotation semantics** (which hint the host prompts on) + **real-host connectivity** (ApiKey-vs-OAuth go/no-go).

**Files:** Modify `McpEndpointIntegrationTests.cs`; docs.

- [ ] **Step 1: Write the discovery test (all 29 tools + annotations)**

Add to `McpEndpointIntegrationTests.cs` a test that adds a valid `X-Api-Key` header for a test user (reuse the existing key-provisioning path — search `tests/` and `src/InvestmentApp.Api/Auth/` for how a key is minted/seeded; mirror it), POSTs `initialize` then `tools/list`, and asserts all 29 tool names appear plus that `readOnlyHint` and `destructiveHint` are emitted:
```csharp
    [Fact]
    public async Task ToolsList_Returns_All29_Tools_WithAnnotations()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey.ValueForUser("user-1")); // reuse existing provisioning

        var req = new { jsonrpc = "2.0", id = 2, method = "tools/list", @params = new { } };
        var res = await client.PostAsJsonAsync("/mcp", req);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();

        foreach (var name in new[]
        {
            "list_trade_plans","get_trade_plan","create_trade_plan","update_trade_plan","set_trade_plan_status","create_trade",
            "list_portfolios","list_positions","calculate_fees","get_symbol_timeline",
            "list_watchlists","get_watchlist","create_watchlist","update_watchlist","delete_watchlist",
            "add_watchlist_item","update_watchlist_item","remove_watchlist_item","import_vn30",
            "list_journals","get_journal_by_trade","create_journal","update_journal","delete_journal",
            "create_journal_entry","update_journal_entry","delete_journal_entry",
            "list_trades_pending_review","list_journal_entries_by_symbol"
        })
            json.Should().Contain(name);

        json.Should().Contain("readOnlyHint");
        json.Should().Contain("destructiveHint");
    }
```
> If streamable HTTP frames the response as SSE and `PostAsJsonAsync` can't read it, use the `ModelContextProtocol.Client` SDK against `_factory.Server.CreateHandler()` (add the client pkg to the test csproj); keep the assertions identical. If the SDK emits different annotation key names, record the real keys in the checkpoint and adjust — **this is the annotation-semantics verification.**

- [ ] **Step 2: Run to verify fail → then pass** — `--filter ToolsList_Returns_All29_Tools_WithAnnotations`. Fix header/framing until PASS.

- [ ] **Step 3: Manual real-host smoke (go/no-go for multi-client goal)**

Run `dotnet run --project src/InvestmentApp.Api`, connect **one real MCP host** (Claude Desktop custom connector or `npx @modelcontextprotocol/inspector`) to `http://localhost:<port>/mcp` with header `X-Api-Key: <dev key>`. Confirm:
1. Host lists all 29 tools.
2. `list_positions` returns real data for the key owner.
3. `create_trade` triggers the host's confirmation prompt (destructive); `list_portfolios` runs without a prompt.

Paste the inspector output into the PR. **If the host cannot send a static `X-Api-Key` (demands OAuth), STOP and raise** — OAuth 2.1 must then move from "future" into a blocking follow-up before the multi-client goal works.

- [ ] **Step 4: Update docs**

- `docs/architecture.md` — add `/mcp` + `Mcp/` tool folder (7 classes, 29 tools) to the API surface / integrations section.
- `docs/business-domain.md` — MCP as additive agent surface over existing handlers.
- `frontend/src/assets/docs/*` — agent-integration help doc: 29 tools + ApiKey connection steps; register the Help topic if the index requires it.
- `frontend/src/assets/CHANGELOG.md` — prepend: MCP server (full agent-surface parity, 29 tools).
- If not covered by ADR-0003/0004, add `docs/adr/000X-mcp-co-host-stateless-apikey.md` (co-host vs service, stateless HTTP, ApiKey vs OAuth deferred) per `docs/adr/template.md`.

- [ ] **Step 5: Full regression + commit** — `dotnet test` → green.
```bash
git add docs/ frontend/src/assets/ tests/InvestmentApp.Api.Tests/Mcp/McpEndpointIntegrationTests.cs
git commit -m "test(mcp): tools/list discovery for all 29 tools + docs for MCP surface"
```

---

## Future work (thêm tiếp sau — genuinely new, out of scope here)

These are **not** in the current agent REST surface, so they'd be net-new (need new commands/queries/tests, not just a wrapper) — a separate spec + plan:
- OAuth 2.1 discovery-based auth (if a target host rejects static ApiKey).
- Prompts / resources (MCP has more than tools — e.g. expose a "portfolio snapshot" resource or a "review my open positions" prompt template).
- Bulk / analytical ops the REST agent surface doesn't have yet (e.g. portfolio P&L summary, TWR, risk metrics as tools).
- Streaming/long-running tool results if any op grows beyond a single request.

---

## Self-Review

**Spec coverage:** every `AiAgent*Controller` action → a tool (inventory table maps all 29 + names the mirrored controller). Stateless HTTP (Task 1), ApiKey auth binding (Task 1), UserId=sub (Task 2 + every tool), read/destructive hints (all tasks + verified Task 9), fee excl tax (Task 4), Draft/restore guards (Task 3), symbol-required guard (Task 8). Risks §10 → Task 1 (SDK verify, stateless, auth), Task 2 (HttpContext.User), Task 4 (calculate_fees non-MediatR), Task 9 (host smoke, annotation semantics). ✅

**Placeholder scan:** Task 9 Step 1/3 leave the exact key-mint helper + SSE-vs-JSON framing to implementation time — concrete verification instructions tied to the resolved SDK/provisioning, not hand-waves. DTO return placeholders in tests note the `null!` fallback. All tool code is complete.

**Type consistency:** `GetUserId()`, `McpTestContext.Capture<TResponse,TConcrete>`, and every `IRequest<T>` return type match the verified-signatures block. Path params (`id`, `symbol`, `tradeId`) assigned to the same command property names the controllers use.

---

## Implementation deviations (recorded during execution 2026-07-24)

- **SDK version:** resolved `ModelContextProtocol.AspNetCore 2.0.0-rc.1` (2.0 RC, newer than the 0.x preview assumed). API surface matched the plan: `AddMcpServer`, `WithHttpTransport(o => o.Stateless = true)`, `MapMcp`, `[McpServerTool(ReadOnly/Destructive)]`, `[McpServerToolType]` all present.
- **Registration:** used `.WithToolsFromAssembly()` instead of per-class `.WithTools<T>()` — `WithTools<T>` rejects `static` classes as type args (CS0718). Assembly scan picks up all `[McpServerToolType]` classes.
- **Watchlist DTOs:** live in `InvestmentApp.Application.Watchlists.Dtos` (not the query namespaces) — added that using.
- **Discovery test:** implemented at DI level (`McpToolDiscoveryTests`) resolving `IEnumerable<McpServerTool>` and asserting names + `ReadOnlyHint`/`DestructiveHint`, instead of a `WebApplicationFactory<Program>` boot. Rationale: booting the full app in a test spins up the real Mongo client, and this repo's config points localhost at the **prod** DB. The DI-level test verifies all 29 tools + annotations with zero infra/secret dependency. **Deferred to manual smoke (Task 9 Step 3):** the auth-401 HTTP test and the real-host connection (ApiKey-vs-OAuth go/no-go).
- **Result:** 35 MCP tests pass; API builds clean. Pre-existing unrelated failure found: `AiAgentControllerTests.CreateTrade_NoPortfolioId_AndNullFeeTax_ResolvesBoth` expects stale `Fee=265000` (tax folded) vs current fee-excl-tax `165000` (ADR-0006 / PR #127) — not caused by this work.

## Execution Handoff

**Two execution options:**

**1. Subagent-Driven (recommended)** — fresh subagent per task (Tasks 2–8 are near-identical mechanical mirrors, ideal for isolated subagents), review between tasks. SDK framing + annotation keys (Task 1, Task 9) are the live-adjustment spots.

**2. Inline Execution** — batch with checkpoints in this session.

Which approach? (Or trial-window — review the plan first, code later.)
