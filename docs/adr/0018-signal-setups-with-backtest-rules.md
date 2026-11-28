# ADR-0018 — Per-stock signal setups use discrete rules with a realistic backtest, not indicator vote counting

- **Status:** Accepted
- **Date:** 2026-11-28
- **Related plan:** (none in the repo; this ADR is the record of the S1 design and of the decision to ship it in a lean form after measuring the sample size)
- **Affected layers:** Infrastructure / Application / Api / Frontend

## Context

The Market page already has an "overall signal" built by counting how many of 10 indicators lean bullish or bearish, plus a Confluence score. A count of votes says nothing about what happened *after* such a reading on a given stock, and it mixes correlated indicators as if they were independent. We want to answer a more honest question: "when this setup appeared on this stock in the past, what happened next, under realistic Vietnamese market rules?"

Constraints: the price source gives about 3 years of adjusted daily bars (~740 sessions); Vietnamese rules matter for the outcome (price limits by exchange, T+2.5 settlement so shares bought on day 0 cannot be sold before day 3, floor-locked sessions where nothing can be sold, no stop orders on HOSE, fees, sell tax); and the user is a single retail investor who may anchor on a handful of past outcomes.

## Options Considered

### Option A — Keep voting, add a "historical accuracy" number

- **Pros:** no new concepts; reuses the existing signal.
- **Cons:** the signal is a vote over correlated indicators with no defined entry or exit, so there is nothing to backtest. Any "accuracy" would need invented rules after the fact.

### Option B — Discrete setups with fixed, written rules and a trade simulation

- **Pros:** each setup has an exact trigger, entry, stop, target and expiry that can be replayed on history; the rules can be checked for look-ahead (truncating the series at t must not change trades closed by t); the same rules are used for every stock, so nothing is tuned per stock.
- **Cons:** a new engine (`PullbackSetup`, `SetupBacktester`, `MarketRules`); sample sizes per stock are small; modelling choices (T+2.5, floor lock, stop fill) must be documented as conservative or optimistic.

### Option C — Pool many stocks to get a larger sample

- **Pros:** much larger n.
- **Cons:** trades on different stocks in the same period are strongly dependent (same market regime), so a plain Wilson interval on pooled trades is far too narrow; survivorship bias in the pooled universe; needs data we do not persist (only symbols the user has looked up are stored).

## Decision

**We choose Option B, shipped in a lean form.** The rules are fixed and identical for every stock: Pullback = EMA20 > EMA50 and ADX14 ≥ 20, low within 1 ATR of EMA20, close above EMA20, RSI14 in 40–55, first session only, signals only after a 100-session warm-up. Entry at the next session's open, stop = entry − 2×ATR, target = entry + 2R, 20-session expiry, no overlapping trades, T+2.5, floor-lock delay, costs of 0.15% per side + 0.1% sell tax + 0.1% slippage per side. Ceiling/floor comparisons use a tolerance of one tick over price because adjusted prices are not on ticks.

Win/loss and expectancy are reported only with at least 30 completed trades: win = net R > 0, win rate with a Wilson 95% interval, EV in R with a t-based 95% interval (the standard deviation comes from the sample, so the normal quantile 1.96 would be too narrow at n = 30). Below 30 the API, MCP and UI return only the counts and the sentence "Chưa đủ mẫu để thống kê riêng mã này (cần ≥ 30)". The UI leads with the currently active signal and does not list individual trades, because a list of outcomes lets the reader count a win rate from a handful of trades.

## Consequences

**Measured sample size.** The read-only count tool was run on the persisted adjusted series on 2026-11-28. Only two series were persisted: VNM had 745 bars, 16 signals and 9 completed trades; FPT had 745 bars, 11 signals and 7 completed trades. So per stock n is about 7–9 over three years, and n ≥ 30 per stock is practically unreachable. The statistics branch exists for the pooled case and will rarely show on a single stock today. Rules were NOT loosened to chase n: that would be tuning on outcomes.

**Positive:**

- Every number can be reproduced from fixed written rules; look-ahead is tested; the no-data path (unadjusted, fewer than 250 bars, last bar more than 10 calendar days old) is the default and returns a reason instead of numbers.
- The active-signal block is useful on its own (planned entry, stop, target) without any statistical claim.

**Negative / Trade-offs:**

- Most stocks will show only "đang kích hoạt" and a count line.
- Disclosed biases that remain: prices are adjusted (old levels differ from what traded), delisted stocks are absent (survivorship), HOSE has no stop orders so filling at the stop price is optimistic, and T+2.5 is modelled conservatively (exit at the d3 open, not the afternoon of d2).
- HNX/UPCOM tick is a flat 100 VND because the codebase has no tick table for them; an unknown exchange uses the 7% limit.

**Follow-ups (if any):**

- A pooled base rate (for example VN30) is a separate later step. Its conditions: a confidence interval that accounts for dependence (date-clustered block bootstrap or an effective n, never a raw Wilson interval on pooled trades); labelled as a base rate, never as the stock's own evidence; shown only for stocks in the pooled universe; survivorship and in-sample disclosures; kept out of the plan form until the anti-anchoring rule applies to it; and organic snapshots only unless the user explicitly agrees to a background crawl.
- The other setups are out of scope here.

## References

- Docs: `docs/business-domain.md` (rule 5d), `docs/features.md`, `docs/architecture.md` (IndicatorMath / Services/Signals), Help topic `phan-tich-thi-truong`
- Code: `Services/Signals/` (`MarketRules`, `PullbackSetup`, `SetupBacktester`, `PullbackBacktest`, `SetupStatistics`, `ActiveSignal`, `SignalSetupService`); count tool `tests/.../Tools/SignalSetupCountTool.cs`
- PR: fill in after merge
