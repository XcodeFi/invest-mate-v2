# ADR-0004 — AI-agent write surface via the ApiKey scheme (extends ADR-0003)

- **Status:** Accepted
- **Date:** 2026-07-21
- **Related plan:** `docs/superpowers/plans/2026-07-21-ai-agent-tradeplan-api.md`; spec `docs/superpowers/specs/2026-07-21-ai-agent-plans-routines-api-design.md`
- **Extends:** ADR-0003 (per-user API keys)
- **Affected layers:** Application / Api

## Context

ADR-0003 introduced per-user API keys and explicitly scoped them to **opt-in read endpoints** (the daily digest), with the accepted security property stated as *"Leaked key limited to opt-in read endpoints; can't mutate trades."*

The AI-agent flow reverses that scope. In the local NPU chat, after the digest, Claude proposes decisions/plans; the user **"chốt"** (confirms) in that chat; then Claude must **write** to the app on the user's behalf — create a trade plan, update it, change status, and record a trade (full-execute). That requires the ApiKey scheme to reach **mutating** endpoints, which ADR-0003 deliberately excluded.

App reality that reframes the risk: invest-mate-v2 is a **journal/tracker** — it records trade history + decisions, does **not** place broker orders, does **not** move real money, and all records are **editable/deletable**. So a wrong write is a fixable data-tidiness issue, not a financial loss.

## Options Considered

### Option A — Dedicated ApiKey controller re-dispatching existing commands (chosen)

A thin `AiAgentController` pinned to the `ApiKey` scheme (mirroring `AiDigestController`), route `api/v1/ai/agent`, re-dispatching the existing MediatR commands (`CreateTradePlanCommand`, `UpdateTradePlanCommand`, `UpdateTradePlanStatusCommand`, `CreateTradeCommand`). Adapter-level guards curate the surface (force Draft on create, 400 on `restore`, set `UserId`/`Origin`). No new business logic.

- **Pros:** matches the accepted ADR-0003 pattern (one controller = one scheme); reuses all domain/application logic; curated blast radius; consistent with the 30 existing single-scheme controllers.
- **Cons:** a second controller surface mirroring some existing routes.

### Option B — Add `ApiKey` to existing JWT controllers via comma-separated schemes

`[Authorize(AuthenticationSchemes = "Bearer,ApiKey")]` on `TradePlansController`/`TradesController`.

- **Pros:** no new controller.
- **Cons:** no precedent in the codebase (all 30 controllers pin exactly one scheme); with `DefaultChallengeScheme = Google`, a failed API auth 302-redirects to Google instead of 401; exposes every endpoint (delete/abort/review) to the key.

### Option C — Keep ApiKey read-only (no AI writes)

- **Pros:** preserves ADR-0003's property unchanged.
- **Cons:** does not meet the requirement — the user explicitly wants Claude to lập/sửa/execute plan from the NPU chat.

## Decision

**We choose Option A**, accepting the reversal of ADR-0003's "can't mutate trades" property.

The reversal is acceptable because the app is a journal (no real money, editable), the write surface is curated (destructive ops — delete/abort/review/restore — stay off the agent surface), ownership is enforced, and a human "chốt" gates every write in the chat. The dedicated-controller shape keeps the change consistent with the codebase's proven single-scheme pattern.

## Consequences

**Positive:**
- Reuses existing commands/handlers — no duplicated business logic.
- Curated, auditable AI write surface; ownership enforced at the handler.

**Negative / Trade-offs (vs ADR-0003):**
- A leaked key can now **mutate** plans/trades (journal records), not just read. Mitigated by: curated surface (no delete/abort/restore/bulk), per-user ownership checks, human "chốt" confirm, recommended short key expiry, and an `AI_AGENT` audit marker. Confirm gate is a **trust-model** control (behavioral, on the NPU side), not server-enforced — acceptable for a single-user journal.

**Follow-ups:**
- **Surface expansion (2026-07-23):** the same Option-A pattern was extended to 5 more domains — positions (read), watchlist (CRUD), journal-entries, journals, symbol timeline — via sibling controllers sharing a new `AiAgentControllerBase`. Same decision, no new architectural trade-off. Spec: `docs/superpowers/specs/2026-07-23-agent-watchlist-positions-expose-design.md`.
- IDOR fix: `CreateTradeCommand` gains a server-set `UserId` + handler ownership assert (closes a pre-existing gap on the JWT surface too).
- Deferred (v1.1): ownership verification of `StrategyId`/`tradeId` passed into plan commands; server-side approval if this ever handles non-journal data.
- Tests: handler ownership (403), controller adapter guards (Draft/restore/Origin), doc drift, doc-serve ETag/304.
- Docs: `architecture.md`, `business-domain.md`, `features.md`, changelog; update plan `p1-daily-digest-api-keys.md`.

## References

- Extends: `docs/adr/0003-per-user-api-keys.md`
- Spec: `docs/superpowers/specs/2026-07-21-ai-agent-plans-routines-api-design.md`
- PR: #XX (fill in after merge)
