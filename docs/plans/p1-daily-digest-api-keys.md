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
| 1c | `ApiKeysController` (JWT-authed) + management UI | Api, Frontend | todo |
| 2 | `ApiKey` authentication handler (validate → resolve userId, opt-in endpoints only) | Api | todo |
| 3 | `daily-digest` context (add cash + position-sizing) + endpoint wired to ApiKey scheme | Infrastructure, Api | todo |
| 4 | NPU side: cron pull → `claude -p` headless → TTS/notify (repo `npu-assistant`, separate ship) | external | todo |

## Conventions locked

- Token: `imk_` + base64url(32 random bytes). Store SHA-256 hex + `Prefix` (`imk_` + first 8) for display. Plaintext shown once.
- Expiry required (no never-expire). Creation options 30/90/180/365 days, default 90, max 365 (enforced in command validator).
- Multiple named keys per user allowed.
- ApiKey scheme accepted ONLY on endpoints that opt in — never blanket the API.
