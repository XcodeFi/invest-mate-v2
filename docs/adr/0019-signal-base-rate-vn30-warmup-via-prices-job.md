# ADR-0019 — Pooled VN30 base rate for the Pullback setup, month-block bootstrap, and a warm-up that piggybacks on the prices job

- **Status:** Accepted
- **Date:** 2026-11-28
- **Related plan:** S3 plan (root handoff) with its "Revisions after challenger" section; follows ADR-0018 (S1)
- **Affected layers:** Domain / Application / Infrastructure / Api / Frontend

## Context

ADR-0018 measured the per-stock sample: about 7–9 completed Pullback trades per stock over three years (FPT 7, VNM 9, HHV 4). Per-stock statistics are practically unreachable, and ADR-0018 listed pooling as the remaining way to get a number at all, with two warnings: trades on different stocks in the same period are strongly dependent, so a plain Wilson interval on pooled trades is far too narrow; and the pooled universe is survivor-biased.

Pooling needs adjusted daily series for the whole VN30 basket, but the app only stores series for symbols someone has looked at. Getting them means calling the price source (24hmoney UDF) for stocks nobody asked about. The source has no published API and a WAF; the provider already has per-symbol gates, a daily budget of one UDF call per symbol, per-symbol and global cooldowns, and a shared failure streak that pauses *user charts* after three source-wide failures. The user approved background warm-up within hard limits: at most 30 calls per day, at least one minute apart, no WAF evasion.

## Options Considered

### Pooling

- **A — Show nothing until a single stock reaches 30 trades.** Honest, but the feature would essentially never show.
- **B — Pool the basket and report a Wilson interval on the pooled trades.** Looks rigorous and is wrong: it treats trades from the same month as independent.
- **C — Pool the basket and bootstrap by calendar month (block bootstrap).** Resample months with replacement, recompute the pooled win rate and mean R over all trades of the resampled months (a ratio estimator), months with no trades kept as clusters, 2.5/97.5 percentiles, fixed seed.

### Warm-up

- **A — A new Cloud Scheduler job.** Free tier allows three jobs and they are in use; a new one is also new infrastructure to deploy and monitor.
- **B — Piggyback on the existing `prices` job** (every 15 minutes, 09:00–14:45 VN time on weekdays, 24 triggers a day): after its own work it asks the warm-up for at most one symbol.
- **C — Fetch on demand when the base rate is requested.** A request would trigger up to 30 source calls and block the user.

## Decision

**Pooling: C.** Gates: at least 15 symbols with usable data, at least 30 completed trades, at least 24 months containing a trade. Below any gate there is no number, only the reason ("Chưa đủ mã VN30 có dữ liệu (7/30, cần từ 15)"). The interval is labelled "xấp xỉ" (approximate) and carries a disclosure: trades can last several months and share one market regime, so the interval can still be narrower than reality. 2,000 resamples, seed fixed, with an own SplitMix64 generator so results do not depend on the .NET version's `Random`. Win means net R > 0 (as in ADR-0018). The basket is the *current* VN30 (single source `Domain/Constants/Vn30Basket`), disclosed as survivor-biased; there is no historical membership table. The VN30 is all HOSE, so the whole basket uses HOSE price limits and no per-stock exchange lookup is made (each lookup would be another source call). The base rate is shown only for VN30 members, below the stock's own counts line, never in the plan form (anchoring), and never as the stock's own evidence.

**Warm-up: B**, with these limits, all on the provider side so they hold no matter who calls:

- **Calls, not symbols, are capped:** at most 30 UDF calls per Vietnam day, counted including failures, persisted in a tiny `job_state` document per day (`signal-warmup:yyyy-MM-dd`, atomic `$inc` guarded by `Calls < cap`; instances restart, an in-memory counter would reset to zero).
- **No graph fallback in warm-up:** an unadjusted series must never be stored as if adjusted.
- **No harm to user charts:** warm-up skips *without calling* when the global cooldown is on or the shared failure streak is at least 1. A warm-up failure never touches `tv:fail-streak` or the per-symbol cooldown that user charts use; warm-up has its own pause (source-wide failure pauses warm-up for the failure cooldown) and its own per-symbol rest until end of day (symbol-only failures such as `no_data`).
- **Picker matches the provider:** missing snapshots first (basket order), then the stale snapshot with the oldest `FetchedAt`; a snapshot the provider's own freshness rule calls fresh is skipped, so no slot is spent on a call the cache would answer.
- **Isolated and bounded:** runs after the prices work, including when the job has zero symbols; 20 s linked token; exceptions are logged and swallowed except cancellation of the request itself; `PriceSnapshotJobResult` is unchanged.
- **Rate:** one symbol per trigger is at most 24 calls a day, spaced by the 15-minute schedule, below the approved ceiling. Kill switch: `SignalWarmup__Enabled=false`; `SignalWarmup__SymbolsPerRun` and `MarketDataProvider__WarmupMaxCallsPerDay` tune it.

No new scheduler job and no infrastructure change are needed because the warm-up rides on `prices`.

## Consequences

- The basket fills over about two trading days (30 symbols, one per 15 minutes, 24 triggers a day); until k reaches 15 the base rate shows its reason. The count tool has a `pooled` mode (read-only) that prints k, n, effective months and the bootstrap result from the persisted series; root reads it after deploy plus about two days of warm-up and tells the user.
- A run during market hours can persist a snapshot with a partial bar for today. This is harmless: the S1 engine and the base rate both drop a forming bar (before 15:00 VN) and only completed trades count.
- The result is cached per day until 15:10 VN (or end of day when computed later); the "not enough" result is cached the same way, so it can lag the warm-up by up to a day boundary of 15:10.
- Not covered by an automated test: the Mongo atomic budget (`WarmupCallBudgetStore.TryConsumeAsync`) needs a real server; its decision logic is covered through a fake of `IWarmupCallBudget`.
- Other setups, plan-form integration, indices other than VN30 and a historical membership table are out of scope.
