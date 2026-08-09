# MCP Tool `get_daily_digest` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Trong repo này plan được thực thi qua skill `/ship`.

**Goal:** Expose bản tin daily digest (đang là `POST /api/v1/ai/daily-digest`, ApiKey scheme) thành MCP tool read-only `get_daily_digest` — bước Phase B.1 của plan `p1-invest-mate-mcp.md` bên npu-assistant, để `/stock` agent bỏ được `Bash(curl:*)`.

**Architecture:** Thin wrapper — 1 static tool class mới trong `src/InvestmentApp.Api/Mcp/`, inject `IAiAssistantService` (đã DI-registered, `AiDigestController` đang dùng) + `IHttpContextAccessor`, gọi thẳng `BuildDailyDigestAsync(userId, ct)`. `ErrorMessage != null` → throw `ModelContextProtocol.McpException` (mirror semantics 400 của controller). Không args, không sửa `Program.cs` (`WithToolsFromAssembly` tự pick up), không thêm business logic.

**Tech Stack:** .NET 9, ModelContextProtocol.Core 2.0.0-rc.1, xUnit + FluentAssertions + Moq.

## Global Constraints

- Base branch: **`origin/master` mới nhất** — PR #131 (P0 tools) đã squash-merge; KHÔNG branch từ `feature/mcp-p0-risk-decision-tools` (stale, chứa phantom commit).
- Branch mới tạo với `--no-track` (tránh upstream=master → VS Code Sync đẩy nhầm lên master).
- Mọi `git commit` / `push` / PR cần Truong xác nhận từng lần (per-change confirmation).
- Description của tool viết **tiếng Việt có dấu đầy đủ**.
- TDD: Red → Green, chạy `dotnet test tests/InvestmentApp.Api.Tests` sau mỗi bước xanh.
- Code review sub-agent bắt buộc trước commit cuối; secret scan diff trước PR (hard gate).
- Tool đếm: 37 → **38**; số class `[McpServerToolType]`: 9 → **10**.

---

### Task 1: Branch setup

**Files:** không đổi code.

- [ ] **Step 1: Tạo branch sạch từ master**

```bash
git fetch origin
git checkout master && git pull --ff-only
git checkout -b feature/mcp-daily-digest-tool --no-track
```

Lưu ý: file untracked `docs/handoffs/HANDOFF-2026-07-25-mcp-p0-tools.md` (nếu còn) không chặn checkout — để nguyên. Nếu `git status` báo file plan này (`docs/plans/done/mcp-daily-digest-tool.md`) untracked thì nó sẽ được commit cùng Task 4.

---

### Task 2: `DigestTools` + unit tests

**Files:**
- Create: `src/InvestmentApp.Api/Mcp/DigestTools.cs`
- Test: `tests/InvestmentApp.Api.Tests/Mcp/DigestToolsTests.cs`

**Interfaces:**
- Consumes: `IAiAssistantService.BuildDailyDigestAsync(string userId, CancellationToken ct)` → `Task<AiContextResult>` (namespace `InvestmentApp.Application.Common.Interfaces`; `AiContextResult` có `SystemPrompt`, `UserMessage`, `ErrorMessage?` — định nghĩa trong `IAiChatService.cs`); `McpUserContext.GetUserId(this IHttpContextAccessor)` (claim `sub`, throw `UnauthorizedAccessException` nếu thiếu); helper test `McpTestContext.WithUser(string userId)` → `IHttpContextAccessor`.
- Produces: MCP tool `get_daily_digest` (ReadOnly, no-args) trả `AiContextResult`; class `InvestmentApp.Api.Mcp.DigestTools` (static).

- [ ] **Step 1: Viết failing tests**

```csharp
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Common.Interfaces;
using ModelContextProtocol;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class DigestToolsTests
{
    private readonly Mock<IAiAssistantService> _aiAssistant = new();

    [Fact]
    public async Task GetDailyDigest_PassesUserId_AndReturnsResult()
    {
        var expected = new AiContextResult { SystemPrompt = "sys", UserMessage = "msg" };
        _aiAssistant.Setup(s => s.BuildDailyDigestAsync("u-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await DigestTools.GetDailyDigest(
            _aiAssistant.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        result.Should().BeSameAs(expected);
        _aiAssistant.Verify(s => s.BuildDailyDigestAsync("u-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDailyDigest_ErrorMessage_ThrowsMcpException()
    {
        _aiAssistant.Setup(s => s.BuildDailyDigestAsync("u-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiContextResult { ErrorMessage = "Chưa có danh mục nào." });

        var act = () => DigestTools.GetDailyDigest(
            _aiAssistant.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        await act.Should().ThrowAsync<McpException>().WithMessage("Chưa có danh mục nào.");
    }
}
```

- [ ] **Step 2: Chạy test, xác nhận FAIL**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter DigestToolsTests`
Expected: compile error `DigestTools` chưa tồn tại.

- [ ] **Step 3: Implement `DigestTools.cs`**

```csharp
using System.ComponentModel;
using InvestmentApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class DigestTools
{
    [McpServerTool(Name = "get_daily_digest", ReadOnly = true)]
    [Description("Bản tin hằng ngày cho AI advisor: bối cảnh danh mục, số dư tiền mặt, gợi ý sizing — cùng payload (systemPrompt + userMessage) với POST /api/v1/ai/daily-digest.")]
    public static async Task<AiContextResult> GetDailyDigest(
        IAiAssistantService aiAssistant, IHttpContextAccessor http, CancellationToken ct)
    {
        var result = await aiAssistant.BuildDailyDigestAsync(http.GetUserId(), ct);
        if (result.ErrorMessage != null)
            throw new McpException(result.ErrorMessage);
        return result;
    }
}
```

- [ ] **Step 4: Chạy test, xác nhận PASS**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter DigestToolsTests`
Expected: 2 PASS.

---

### Task 3: Discovery + schema-leak guard (37 → 38)

**Files:**
- Modify: `tests/InvestmentApp.Api.Tests/Mcp/McpToolDiscoveryTests.cs`

**Interfaces:**
- Consumes: `DigestTools` (Task 2); `Tools()` helper trong chính file này (build DI container, phải đăng ký MỌI service mà tool inject — thiếu là service leak vào inputSchema).

- [ ] **Step 1: Sửa test trước (Red)** — 3 chỗ:

1. Thêm `"get_daily_digest"` vào cuối mảng `ReadTools` (sau block P0, thêm comment `// Phase B — daily digest`).
2. Trong `Registers_All_37_Tools`: đổi assertion `.Should().Be(37)` → `.Should().Be(38)`; đổi tên method → `Registers_All_38_Tools`.
3. Trong `Tools()`: thêm `services.AddSingleton(Mock.Of<IAiAssistantService>());` cạnh các `Mock.Of` hiện có (cần `using InvestmentApp.Application.Common.Interfaces;`).
4. Trong `Tool_Schemas_Exclude_Injected_Services_And_Include_Real_Args` thêm:

```csharp
// get_daily_digest — no-args tool; injected services must not leak.
schema["get_daily_digest"].Should().NotContain("aiAssistant").And.NotContain("http");
```

- [ ] **Step 2: Chạy, xác nhận trạng thái**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter McpToolDiscoveryTests`
Expected: PASS ngay (DigestTools đã tồn tại từ Task 2 — Red thật sự chỉ xảy ra nếu chạy trước Task 2; giá trị của test là khóa count 38 + guard schema-leak về sau).

- [ ] **Step 3: Chạy toàn bộ Api tests**

Run: `dotnet test tests/InvestmentApp.Api.Tests`
Expected: tất cả PASS, không regression.

---

### Task 4: Docs sync + full suite + commit + PR

**Files:**
- Modify: `docs/architecture.md` (dòng ~32 và ~175)
- Modify: `docs/business-domain.md` (dòng ~387)
- Modify: `frontend/src/assets/CHANGELOG.md` (prepend entry mới)
- Commit kèm: `docs/plans/done/mcp-daily-digest-tool.md` (file plan này)

- [ ] **Step 1: `docs/architecture.md`**

Dòng ~32 (tree comment `Mcp/`): `9 [McpServerToolType] classes = 37 tools (29 mirroring ... + 8 P0 decision/risk read tools; ...)` → `10 [McpServerToolType] classes = 38 tools (29 mirroring AiAgent* surface + 8 P0 decision/risk read tools + 1 daily digest; mapped at /mcp, ApiKey scheme)`.

Dòng ~175 (bảng route, hàng `MCP | /mcp`): `**37 schema-typed tools** (9 ... classes ...: TradePlan, Trade, Portfolio, Symbol, Watchlist, Journal, JournalEntry, Decision, Risk)` → `**38 schema-typed tools** (10 ... classes ...: TradePlan, Trade, Portfolio, Symbol, Watchlist, Journal, JournalEntry, Decision, Risk, Digest)`; cuối câu liệt kê 8 P0 tools, nối thêm: `; **\`get_daily_digest\` (DigestTools, 2026-07-26)** — thin wrapper trên \`IAiAssistantService.BuildDailyDigestAsync\` (cùng payload REST \`POST /ai/daily-digest\`), \`ErrorMessage\` → \`McpException\`; bước Phase B để NPU /stock agent bỏ curl.`

- [ ] **Step 2: `docs/business-domain.md`**

Dòng ~387 (hàng `MCP (ApiKey scheme)`): `**37 tool** (9 lớp ...)` → `**38 tool** (10 lớp ...)`; sau câu về 8 tool P0 thêm: `**+1 tool \`get_daily_digest\` (2026-07-26)** — bản tin hằng ngày (danh mục + số dư + sizing) dạng MCP tool, thay cho REST \`POST /ai/daily-digest\` phía agent.`

- [ ] **Step 3: `frontend/src/assets/CHANGELOG.md`** — prepend sau dòng `---` đầu:

```markdown
## [v2.66.0] — 2026-07-26 · MCP: tool `get_daily_digest` (Phase B daily digest)

### Tính năng

**📰 MCP tool `get_daily_digest` (chỉ đọc)** — bản tin hằng ngày cho AI advisor (bối cảnh danh mục, số dư tiền mặt, gợi ý sizing) giờ lấy được qua MCP thay vì REST `POST /ai/daily-digest`, mở đường cho NPU `/stock` agent bỏ hẳn `curl`.

- Thin wrapper trên `IAiAssistantService.BuildDailyDigestAsync` — không args, không business logic mới; lỗi (`ErrorMessage`) → `McpException` để MCP client thấy tool error rõ ràng.
- Tổng tool: **37 → 38**. REST endpoint giữ nguyên (additive).
- Tests: +2 unit (passthrough userId + error path) + discovery 38 tool + schema-leak guard.

---
```

- [ ] **Step 4: Full suite**

Run: `dotnet test`
Expected: toàn bộ pass (baseline trước PR #131: 1.451). Nếu fail → STOP, báo Truong.

- [ ] **Step 5: Code review + secret scan (hard gate)**

Chạy review sub-agent trên diff (`/code-review` flow); scan diff không có key/token/URL prod. Blocker → fix trước.

- [ ] **Step 6: Commit + push + PR (hỏi Truong xác nhận trước từng bước)**

```bash
git add src/InvestmentApp.Api/Mcp/DigestTools.cs tests/InvestmentApp.Api.Tests/Mcp/DigestToolsTests.cs tests/InvestmentApp.Api.Tests/Mcp/McpToolDiscoveryTests.cs docs/architecture.md docs/business-domain.md frontend/src/assets/CHANGELOG.md docs/plans/done/mcp-daily-digest-tool.md
git commit -m "feat(mcp): add get_daily_digest tool (Phase B daily digest)"
git push -u origin feature/mcp-daily-digest-tool
gh pr create --base master --title "feat(mcp): get_daily_digest tool (Phase B daily digest)" --body "Exposes the daily digest (portfolio context + cash + sizing) as read-only MCP tool \`get_daily_digest\` — thin wrapper over \`IAiAssistantService.BuildDailyDigestAsync\`, \`ErrorMessage\` → \`McpException\`, no new business logic. Tool count 37 → 38. Phase B.1 of the npu-assistant MCP migration (lets the /stock agent drop \`Bash(curl:*)\`). Tests: +2 unit, discovery 38, schema-leak guard. REST \`POST /ai/daily-digest\` unchanged (additive)."
```

Live verify (tùy chọn, cần Truong đưa API key — classifier chặn tự mint): `tools/list` qua `/mcp` local phải chứa `get_daily_digest`.

---

### Follow-up (ngoài repo này — KHÔNG làm trong PR này)

- `C:\Users\a\npu-assistant\docs\plans\p1-invest-mate-mcp.md`: đánh dấu Phase B step 1 done + checkpoint; step 2 (cutover `agent_api.py`, bỏ `Bash(curl:*)`, sửa docstring "29 tools" → 38) làm ở workspace npu-assistant.
