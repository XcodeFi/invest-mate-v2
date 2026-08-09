# ADR-0005 — Auto-resolve portfolio & fee/tax on the AI-agent create-trade surface

- **Status:** Accepted
- **Date:** 2026-07-23
- **Related plan:** `docs/plans/done/p3-agent-position-portfolio-fee.md`
- **Affected layers:** Api

## Context

The AI-agent surface (`/api/v1/ai/agent`, ApiKey scheme — see ADR-0004) lets NPU/Claude write trades. But `CreateTradeCommand` **requires** `portfolioId`, while `TradePlan.PortfolioId` is nullable and the surface offered **no way to list portfolios** — so when the agent executed a plan it had to interrogate the user for the portfolio every time. Fee/tax had the same gap: `CreateTradeCommand.Fee/Tax` default to `0`, and the fee engine (`FeeCalculationService`, `FeesController`) was not exposed on the agent surface, so the agent either sent `0` (wrong) or guessed. The app already owns both capabilities (`GetAllPortfoliosQuery`, `FeeCalculationService`); the question is only *where* to make the agent self-sufficient. Constraint: the solo user almost always has exactly one portfolio, and the JWT create-trade path must not change behavior.

## Options Considered

### Option A — Auto-resolve in the agent controller (chosen)

- **Pros:**
  - JWT `CreateTradeCommand` path untouched (still requires explicit `portfolioId`/`fee`/`tax`).
  - New convenience logic is confined to the ApiKey surface, easy to test at the controller.
  - Reuses `GetAllPortfoliosQuery` + `FeeCalculationService` — no new Domain/Application code.
- **Cons:**
  - The agent controller now holds a little resolution logic (mild deviation from the "agent controller = thin re-dispatch" idiom noted on `AiAgentController`).

### Option B — Add auto-resolve flags to the shared `CreateTradeCommand`

- **Pros:** Single code path for JWT + ApiKey.
- **Cons:** Invasive — changes a shared command consumed by the FE; risks behavior drift on the JWT path; couples domain command to agent-convenience concerns.

### Option C — Require `PortfolioId` at plan creation (make it non-nullable)

- **Pros:** Portfolio decided once, up front.
- **Cons:** Breaks existing nullable plans; forces the user to pick a portfolio at planning time even before deciding; doesn't solve fee/tax; larger blast radius.

## Decision

**We choose Option A.** Confining resolution to the agent controller keeps the shared command and the FE untouched while making the agent self-sufficient. On `POST /ai/agent/trades`: `portfolioId` omitted → auto-pick the caller's single portfolio (0 or >1 → `400`, the latter listing `{id,name}`); `fee`/`tax` omitted → auto-compute via `FeeCalculationService` (matching how the FE fills them); an explicit value (including `0`) is respected. Two read/calc endpoints (`GET /portfolios`, `POST /fees/calculate`) are added so the agent can also resolve/preview explicitly.

## Consequences

**Positive:**

- Agent executes a plan end-to-end without interrogating the user when one portfolio exists.
- Fee/tax computed consistently with the manual UI path (`fee = transactionFee + VAT + TNCN`, `tax = TNCN 0.1% on SELL`).
- Ownership double-fenced: the portfolio list is filtered by `sub`, and `CreateTradeCommandHandler` still re-asserts `portfolio.UserId == sub`.

**Negative / Trade-offs:**

- `AiAgentController` is no longer a pure thin re-dispatcher; it depends on `IFeeCalculationService`.
- Fee/tax parity relies on `FeeCalculationService`; the agent path uses `CalculateSecuritiesTax` (config-driven) rather than the hard-coded `0.001m` in `FeesController` — identical today (config = 0.001) and drift-resistant.

**Follow-ups (if any):**

- Pre-existing: `FeesController` has `[Authorize]` commented out ("temporarily disabled for testing") — re-enable in a separate cleanup PR.
- Pre-existing (flagged, not fixed here): the FE stores `fee = totalFees` (which already includes TNCN) *and* `tax` separately, so `GetAllPortfolios` sell math (`Qty*Price − Fee − Tax`) appears to double-subtract TNCN. The agent path matches the FE deliberately; a fix belongs in its own ticket.

## References

- Plan: `docs/plans/done/p3-agent-position-portfolio-fee.md`
- Spec: `docs/superpowers/specs/2026-07-23-agent-position-open-portfolio-fee-design.md`
- Extends: ADR-0004 (AI-agent write surface via ApiKey)
- PR: #TBD (fill in after merge)
