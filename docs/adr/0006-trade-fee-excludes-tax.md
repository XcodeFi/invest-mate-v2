# ADR-0006 — Trade.Fee stores broker cost only; TNCN tax stays in Trade.Tax

- **Status:** Accepted
- **Date:** 2026-07-24
- **Related plan:** follow-up to ADR-0005 (agent create-trade); bug found in `/code-review` of PR #124
- **Affected layers:** Frontend, Api (agent surface), Infrastructure/Application (data — migration)

## Context

Every net-value consumer in the codebase treats a trade's `Fee` and `Tax` as **separate, non-overlapping** cost components: buys add `Quantity*Price + Fee + Tax`, sells subtract `Quantity*Price - Fee - Tax`. This holds in `GetAllPortfoliosQuery`, `CampaignReviewService`, `CashFlowAdjustedReturnService`, `PerformanceMetricsService`, `StrategyPerformanceService`, `AiAssistantService` (realized-PnL), and `GetTradesByPortfolio`.

But both **producers** stored `Fee = totalFees`, where `totalFees = transactionFee + VAT + TNCN`. On a SELL, `totalFees` includes the TNCN tax **and** the same tax is stored again in `Tax`. Every consumer therefore subtracts TNCN twice → understated sell proceeds and wrong portfolio P/L. (BUY is unaffected: TNCN = 0 on buys.)

Producers with the bug: FE `trade-create.component.ts` (`onSubmit`) and backend agent `AiAgentController.CreateTrade` (auto-fill via `AgentTradeFeeCalculator.TotalFees`).

## Options Considered

### Option A — Fix producers: `Fee = transactionFee + VAT`, keep `Tax` separate

- **Pros:** Matches the canonical semantic shared by all 7 consumers; no consumer changes; `Fee`/`Tax` become truly orthogonal. Preview endpoints keep showing a correct all-in `totalFees`.
- **Cons:** Existing SELL trades keep the wrong `Fee` until a data migration runs.

### Option B — Fix the aggregations to subtract `Fee` only (treat `Fee` as all-in)

- **Pros:** No producer change; historical rows "self-correct".
- **Cons:** Must edit **7** consumers consistently and make `Tax` redundant/ambiguous; BUY math (`+Fee+Tax`) also needs rework; high blast radius; `Tax` column becomes meaningless. Fragile.

## Decision

**We choose Option A.** The separate-components semantic is already the de-facto contract in 7 places; the defect is two producers violating it. Fixing the producers is the smallest change that makes every consumer correct at once, and keeps `Fee`/`Tax` meaningful. `AgentTradeFeeCalculator.TotalFees` stays as-is (a correct all-in total for the `/fees/calculate` preview); only the value **persisted** to `Trade.Fee` changes to `transactionFee + VAT`.

## Consequences

**Positive:**
- New trades (FE + agent) record `Fee`/`Tax` without overlap → correct portfolio P/L, campaign review, performance, strategy, realized-PnL everywhere.
- No consumer code touched; low regression risk.

**Negative / Trade-offs:**
- **Historical SELL trades created before this fix still double-count** until migrated. Read models will keep showing slightly-off sell proceeds for those rows.

**Follow-ups:**
- **Data migration (separate, gated):** for existing SELL trades where `Tax > 0` and `Fee` still includes it, set `Fee = Fee - Tax`. Must be dry-run first on prod (`InvestmentApp_prod`), scoped to SELL + `Tax > 0`, idempotent (guard against re-running). Decision on running it is tracked separately.
- Tests added: `AiAgentControllerTests.CreateTrade_NullFeeTax_Sell_AutoComputes` (Fee = broker+VAT), `trade-create.component.spec.ts` onSubmit payload.

## References

- Plan: bug surfaced in PR #124 `/code-review`; ADR-0005 flagged it as follow-up.
- PR: #(fill after merge)
