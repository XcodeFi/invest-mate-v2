# Handoff — 2026-07-14 — Daily digest + per-user API keys

## Now (done this session)

- **Feature start:** NPU daily digest pull + per-user API keys (PAT). Plan: [`docs/plans/p1-daily-digest-api-keys.md`](../plans/p1-daily-digest-api-keys.md). Decision: [`ADR-0003`](../adr/0003-per-user-api-keys.md) (Proposed).
- **Slice 1a done:** `ApiKey` domain entity (`src/InvestmentApp.Domain/Entities/ApiKey.cs`) + 11 tests.
- **Slice 1b-i done:** `ApiKeyTokenService` (Infrastructure) + `IApiKeyTokenService` + `IApiKeyRepository` iface + `CreateApiKeyCommand` (+ handler + validator). 6 + 2 tests.
- **Total: 19 tests green.** Build green.

## Done — Slice 1b-ii (this session, 2026-07-14 PM)

- Mongo `ApiKeyRepository` — unique index on `KeyHash` + non-unique index on `UserId`.
- `GetApiKeysQuery` + `ApiKeyDto` (no hash; test asserts DTO has no `KeyHash` property).
- `RevokeApiKeyCommand` (not-found → throw, wrong owner → `UnauthorizedAccessException`).
- DI: `IApiKeyRepository` + `IApiKeyTokenService` registered (Program.cs ~128–132).
- **Validation foundation (was dead-code app-wide):** added MediatR `ValidationBehavior<,>` (`Application/Common/Behaviors/`) + `AddValidatorsFromAssembly` + `AddOpenBehavior`. **Removed** MVC `AddFluentValidationAutoValidation()` — it fires validators BEFORE controllers set `UserId` from the principal (ResolveDecision/CreateTradePlan/UpdateTradePlan validators check `UserId`). Validators now run only in the pipeline. `CreateApiKeyCommandValidator` is now live.
- **Verify:** 1298 tests green (Domain 740 / App 224 / Infra 301 / **Api 33**), 0 fail. Full solution build green.

## Next (resume here)

1. **Slice 1c — controller DONE (uncommitted), UI remaining.** `ApiKeysController` scaffolded at `src/InvestmentApp.Api/Controllers/ApiKeysController.cs` (JWT scheme, `sub` claim → UserId, POST create→201 / GET list / DELETE `{id}` revoke→204). Builds green. **NOT committed** (hard-stop 21:30). Remaining for 1c: (a) controller integration tests in `InvestmentApp.Api.Tests`; (b) Angular management UI (Angular 19 standalone, inline template, VN có dấu — create key modal showing plaintext ONCE, list with prefix/expiry/last-used, revoke button primary-right); (c) runtime smoke of a `UserId`-setting endpoint to confirm pipeline validation timing; (d) commit + PR.
3. Slice 2: `ApiKey` authentication handler (validate presented key → resolve userId; accepted only on opt-in endpoints).
4. Slice 3: `daily-digest` context (add cash via `GetNetWorthSummary` deps + position-sizing via `IPositionSizingService`) + endpoint on ApiKey scheme.
5. Slice 4 (separate repo `npu-assistant`): cron pull → `claude -p` headless → TTS/notify.

## Blockers / gotchas

- **Git state (updated 21:26):** Slices 1a/1b committed on `feature/daily-digest-api-keys`, **PR MERGED to master**. Local checkout is STILL on the now-merged `feature/daily-digest-api-keys` — do NOT force-push/reset it. **Tomorrow: `git checkout master && git pull --ff-only`, then new branch `feature/daily-digest-api-keys-ui` (or 1c) off updated master.** Uncommitted in working tree: `ApiKeysController.cs` (feature — carry it over), plus non-feature `.claude/commands/ship/SKILL.md`, `.claude/settings.json`, `docs/plans/personal-finance-goals.md`, `docs/plans/plan-personal-tool-audit.md` (leave out of the PR as before).
- **Stray me-shop files:** an unrelated me-shop scaffolding tree (`Operations/` under Domain/Application/Api) was untracked in the working tree and broke the Domain build (`Shelf : Entity`, no such base). Moved out to `d:\invest-mate-v2\_stray-meshop-backup\` (reversible). Do NOT re-add to invest-mate.
- **FluentValidation: RESOLVED this session.** Was dead code app-wide; now wired via MediatR `ValidationBehavior<,>` + `AddValidatorsFromAssembly`, MVC auto-validation removed. `CreateApiKeyCommandValidator` is live. (Merged in the PR.)
- **Stop-loss source** for position-sizing (slice 3): TradePlan SL if linked → else ATR-based → else skip sizing for that symbol.

## Conventions locked (see plan)

Token `imk_` + base64url(32B), store SHA-256 hex + 12-char display prefix. Expiry required, 1–365 days (default 90). Multiple named keys/user. ApiKey scheme only on opt-in endpoints.
