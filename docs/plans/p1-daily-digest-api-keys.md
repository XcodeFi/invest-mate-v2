# Plan — NPU daily digest + per-user API keys

**Goal:** The local NPU assistant pulls a daily digest (holdings + status + watchlist + cash + position-sizing) from the API on a cron using a per-user API key, then feeds it to Claude for buy/sell timing analysis.

**Decisions:** [`ADR-0003`](../adr/0003-per-user-api-keys.md) — per-user API keys (PAT) with expiry.

- **Hybrid analysis:** server pre-computes position-sizing (`PositionSizingService`); Claude (via NPU) does qualitative timing/judgment.
- **Endpoint:** new `POST /api/v1/ai/daily-digest`, accepts the `ApiKey` scheme only (leaves `build-context` JWT-only untouched).
- **Stop-loss source for sizing:** TradePlan SL if the position has a linked plan; else ATR-based; else skip sizing for that symbol (no guessing).
- **Reuse:** `BuildDailyBriefingContext` already assembles portfolio overview, top positions, risk alerts, pending plans, watchlist alerts. Missing pieces to add: **cash/net-worth** (`GetNetWorthSummary` deps) + **position-sizing numbers**.

## Slices

| # | Slice | Layers | Status |
|---|---|---|---|
| 1a | `ApiKey` domain entity (create / expiry / revoke / mark-used) | Domain | **done** — 11 tests green |
| 1b-i | Token service (generate/hash) + `IApiKeyRepository` iface + `CreateApiKeyCommand` | Application, Infrastructure | **done** — 8 tests green |
| 1b-ii | Mongo `ApiKeyRepository` + `GetApiKeysQuery` + `RevokeApiKeyCommand` + DI (Program.cs:122) | Application, Infrastructure | **done** — 5 new tests (7 in ApiKeys), build green |
| 1c | `ApiKeysController` (JWT-authed) + management UI | Api, Frontend | **done** — PR #117 merged |
| 2 | `ApiKey` authentication handler (validate → resolve userId, opt-in endpoints only) | Api | **done** — 7 handler tests green, 1305 total |
| 3 | `daily-digest` context (add cash + position-sizing) + endpoint wired to ApiKey scheme | Infrastructure, Api | **done** — 8 tests green, 1313 total; live-verified vs prod |
| 4 | NPU side: cron pull → `claude -p` headless → TTS/notify (repo `npu-assistant`, separate ship) | external | todo |

## Conventions locked

- Token: `imk_` + base64url(32 random bytes). Store SHA-256 hex + `Prefix` (`imk_` + first 8) for display. Plaintext shown once.
- Expiry required (no never-expire). Creation options 30/90/180/365 days, default 90, max 365 (enforced in command validator).
- Multiple named keys per user allowed.
- ApiKey scheme accepted ONLY on endpoints that opt in — never blanket the API.

### Checkpoint — Slice 2 (done)

- **Decisions**: token presented in `X-Api-Key` header (not `Authorization: Bearer` — unambiguous vs JWT). Scheme `"ApiKey"` registered via `AddApiKey()` in `Program.cs` but never a default → opt-in only. Handler emits `sub`=UserId so controllers read the user identically to the JWT scheme. `LastUsedAt` written on every successful auth; write failure is caught + logged (audit-only, must not 500 a valid request). Follows codebase `DateTime.UtcNow` convention (no `TimeProvider`).
- **Files changed**: `src/InvestmentApp.Api/Auth/ApiKeyAuthExtensions.cs` (new — `ApiKeyAuthenticationDefaults`, `AddApiKey()`, `ApiKeyAuthenticationHandler`); `src/InvestmentApp.Api/Program.cs` (+`.AddApiKey()`); `tests/InvestmentApp.Api.Tests/Auth/ApiKeyAuthenticationHandlerTests.cs` (new — 7 tests).
- **Tests**: 7 handler tests (missing/empty header → NoResult; unknown/revoked/expired → Fail; valid → Success+sub claim; LastUsedAt persisted). Full suite 1305 green.
- **Affected layers**: Api.
- **Next (Slice 3)**: add `daily-digest` context — extend `BuildDailyBriefingContext` with cash/net-worth (`GetNetWorthSummary` deps) + position-sizing (`IPositionSizingService`; stop-loss source = TradePlan SL if linked → else ATR-based → else skip). Add `POST /api/v1/ai/daily-digest` guarded by `[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]` — the FIRST opt-in endpoint; read `sub` via `User.FindFirst("sub")`. Read: `src/InvestmentApp.Infrastructure/Services/` (AI context builder), `IPositionSizingService`, `GetNetWorthSummary`. Then full-stack verify (mint key → curl digest endpoint with `X-Api-Key`).

### Checkpoint — Slice 3 (done)

- **Decisions**: injected `IFinancialProfileRepository` (NOT MediatR `ISender`) — reuse `FinancialProfile` domain methods with the already-computed portfolio value as securities, avoiding a duplicate PnL pass that `GetNetWorthSummaryQuery` would incur. **Investable capital = securities market value + idle cash** (excludes gold/savings/debt); fallback = securities value when no financial profile. Enriched the **shared** `BuildDailyBriefingContext` (in-app daily-briefing chat gains cash+sizing too — intentional). Position-sizing computed only for pending (Draft/Ready) plans via `ShouldComputeSizing` guard (skip when SL≤0, SL==entry, or capital≤0 — all would produce a misleading 100cp fallback). Endpoint returns `AiContextResult` (prompt pair) like `build-context`. **Separate `AiDigestController`** (not a 2nd `[Authorize]` on `AiController`) because different-scheme `[Authorize]` attributes stack as AND.
- **Files changed**: `AiAssistantService.cs` (+3 public static helpers, enriched briefing builder, `BuildDailyDigestAsync`); `IAiAssistantService.cs` (+signature); `AiDigestController.cs` (new); `AiAssistantServiceDailyDigestTests.cs` (new, 8 tests). No DI change — both deps already registered.
- **Tests**: 8 new (ShouldComputeSizing ×4, BuildPlanSizingRequest ×3, FormatCashNetWorthSection ×1); full suite 1313 green. Live-verified vs prod: 200 with `X-Api-Key`, 401 without, `<cash_and_net_worth>` rendered with correct investable=securities+idle_cash; test key revoked after.
- **Affected layers**: Infrastructure, Api.
- **Next (Slice 4 — separate repo `npu-assistant`)**: cron pull `POST /api/v1/ai/daily-digest` with stored `X-Api-Key` → feed `{systemPrompt,userMessage}` to `claude -p` headless → TTS/notify. Not in this repo. Flip nothing here — ADR-0003 already Accepted.
