# Plan — Expose read/write endpoints onto the AI-agent surface

**Design spec:** [`docs/superpowers/specs/done/2026-07-23-agent-watchlist-positions-expose-design.md`](../../superpowers/specs/done/2026-07-23-agent-watchlist-positions-expose-design.md)
**Branch:** `feature/ai-agent-expose-watchlist-positions`
**Scope:** `InvestmentApp.Api` only (5 new controllers + shared base + embedded doc + tests). No Application/Domain/Frontend changes.
**ADR:** Not required — extends accepted ADR-0004 (agent write-surface via ApiKey) with more domains under the same decision. Sibling-controller design (approach B) captured in the spec. A one-line pointer added to ADR-0004 Consequences.

## What

21 thin-wiring routes under `/api/v1/ai/agent` (scheme `ApiKey`), mirroring 5 existing JWT controllers. Re-dispatch existing MediatR commands/queries; `UserId` from `sub` claim. Zero new business logic.

## Where

| File | Purpose |
|---|---|
| `Controllers/AiAgentControllerBase.cs` | Abstract base: `IMediator _mediator` + `GetUserId()`. New controllers only — existing `AiAgentController` untouched. |
| `Controllers/AiAgentPositionsController.cs` | GET `/positions?portfolioId=` (1) |
| `Controllers/AiAgentWatchlistsController.cs` | Watchlist full CRUD (9) |
| `Controllers/AiAgentJournalEntriesController.cs` | JournalEntries (5) |
| `Controllers/AiAgentJournalsController.cs` | Journals (5) |
| `Controllers/AiAgentSymbolsController.cs` | GET `/symbols/{symbol}/timeline` (1) |
| `Docs/AI-Agent-TradePlan-API.md` | +5 sections (route + DTO shape + example), update Mục lục |
| `docs/adr/0004-*.md` | +1-line pointer to this expansion in Consequences |

## Tests (`InvestmentApp.Api.Tests`, mirror `AiAgentControllerTests`)

- UserId injected from `sub` claim (per controller)
- Watchlist item ops bind `WatchlistId`/`Symbol` from route
- JournalEntries: missing `symbol` → 400; update/delete `false` → 404
- Journals: `by-trade` null → 404
- POST `Created` Location → agent surface (`/api/v1/ai/agent/...`)
- Doc guard: doc contains all 5 new section anchors

## Response contract

Mirror source controllers exactly (Ok / Created / NoContent / 404 / 400). Only `Created` Location strings rewritten to the agent surface (`CreatePlan` precedent).

## Success criteria (from spec §10)

1. Agent `GET /ai/agent/positions` returns real holdings.
2. 9 watchlist + 5 journal-entries + 5 journals + timeline all work, key-owner-scoped.
3. `GET /ai/agent/doc` lists all 5 new groups.
4. No route leaks other users' data (ownership at handler level, already tested).
5. No new business logic; diff confined to `InvestmentApp.Api` + doc + test.
