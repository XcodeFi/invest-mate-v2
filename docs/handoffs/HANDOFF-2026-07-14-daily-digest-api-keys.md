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

1. Slice 1c: `ApiKeysController` (JWT-authed — POST create / GET list / DELETE revoke) + Angular management UI. Validation already wired; controller sets `UserId` from principal then sends the command. Consider a runtime smoke of a `UserId`-setting endpoint to confirm pipeline validation timing end-to-end.
3. Slice 2: `ApiKey` authentication handler (validate presented key → resolve userId; accepted only on opt-in endpoints).
4. Slice 3: `daily-digest` context (add cash via `GetNetWorthSummary` deps + position-sizing via `IPositionSizingService`) + endpoint on ApiKey scheme.
5. Slice 4 (separate repo `npu-assistant`): cron pull → `claude -p` headless → TTS/notify.

## Blockers / gotchas

- **Git state:** on branch `fix/decision-queue-suppress-resolved-today`, **NOTHING committed**. At commit time: create `feature/daily-digest-api-keys` off `master` with `--no-track` (avoids the upstream=master → VS Code pushes-to-master pitfall). User approved **including** the two pre-existing modified files in this feature: `.claude/commands/pr/SKILL.md` + `docs/project-context.md`.
- **Stray me-shop files:** an unrelated me-shop scaffolding tree (`Operations/` under Domain/Application/Api) was untracked in the working tree and broke the Domain build (`Shelf : Entity`, no such base). Moved out to `d:\invest-mate-v2\_stray-meshop-backup\` (reversible). Do NOT re-add to invest-mate.
- **FluentValidation:** grep did not find `AddValidatorsFromAssembly`. Verify FV auto-validation is registered before relying on `CreateApiKeyCommandValidator` (else it's dead code — the me-shop pitfall). Check when wiring the controller (1c).
- **Stop-loss source** for position-sizing (slice 3): TradePlan SL if linked → else ATR-based → else skip sizing for that symbol.

## Conventions locked (see plan)

Token `imk_` + base64url(32B), store SHA-256 hex + 12-char display prefix. Expiry required, 1–365 days (default 90). Multiple named keys/user. ApiKey scheme only on opt-in endpoints.
