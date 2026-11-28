# ADR-0020 — The market screener reads two unofficial public sources, applies thresholds in memory, and never ranks

- **Status:** Accepted
- **Date:** 2026-11-28
- **Related plan:** market-wide opportunity screener, v1 (PR1 = backend + MCP, PR2 = page + Help + quick-board entry point)
- **Affected layers:** Infrastructure / Application / Api (MCP). The page ships in PR2.

## Context

The user asked for a way to scan the hundreds of listed symbols, keep the promising ones, and skip speculative, delisting and trading-restricted names. There is no official data feed in the project. Two public, unauthenticated sources carry what is needed:

1. The 24hmoney `technical-filter` endpoint (same API family and the same request shape as `HmoneyMarketDataProvider`). It returns the whole market in about four pages of 500.
2. The HOSE exchange's own public JSON (`status-list`, `stock-status`, `cancel`) for the special-monitoring lists and the delisting announcements. HNX and UPCOM have no known list source yet.

Both are unofficial in the sense that nothing guarantees their shape or availability, and the WAF in front of 24hmoney has blocked this project before. The risk class is the same as the existing 24hmoney usage.

## Probe record (2026-11-28, from the dev machine)

- `technical-filter` with an empty `param=` returns `data: []`. The permissive filter `market_cap:0:999999999|` returns the whole market: 1,827 rows (HOSE 420, HNX 314, UPCOM 1,093), `total_page` 4 at `per_page=500`, HTTP 200. Rows with no market cap are not returned. 26 of the 1,827 rows have `market_cap` 0 and are dropped as unusable.
- Units, pinned by a fixture test taken from the real sample shape: prices (`match_price`, `match_price_10`) are in thousand VND; `market_cap` and `profit_after_tax` are in billion VND; `avg_trading_vol_10` is in shares. `eps4Q` looked inconsistent and is not used. `the_beta` is `0.0` for 223 of 1,827 rows (a missing value, not a zero beta) and can be negative (minimum −8.37).
- **`rs1m`, `rs3m` and `rs52w` are percentile ranks 1–99 across the whole market, not returns.** Over the 1,587 rows that have them: minimum 1, maximum 99, median 49, none negative, none above 99. 240 rows are null. The first draft of the plan assumed percentage returns ("rs3m > 0 means stronger than the market", "|rs1m| > 40%"); both rules would have been meaningless (always true, never true) and were replaced.
- The three HOSE endpoints answer 200 **without** the extra `type` header the HOSE page's own script sends. No header is added. The delisting list for a year mixes in covered warrants; only 3-character stock codes are kept.

## Options Considered

### Option A — Fetch per request with the user's thresholds in the query

- **Pros:** the upstream filter does the work.
- **Cons:** every threshold combination is a separate upstream call, so a user (or an agent) can drive an unbounded number of calls into a source that already blocks us; the cache key explodes; a partial failure returns a partial universe.

### Option B — Fetch the raw universe once, cache it, apply thresholds in memory (chosen)

- **Pros:** a fixed number of upstream calls (about 4 + 16 per six hours), thresholds are free, one cache key, easy all-or-nothing refresh, easy to test.
- **Cons:** the whole universe is held in memory (about 1,800 small rows); freshness is up to six hours.

### Option C — Score and rank the passed symbols

- **Pros:** a short "best first" list.
- **Cons:** any score is an invented expected-return model. A ranked list anchors the reader on the top names and reads as a recommendation. The inputs are relative ranks from an unofficial source.

## Decision

**Option B, with no score and no ranking.**

- **Fetch.** The raw universe (all floors) is one cache entry, the HOSE lists are another. Each lives six hours with a single-flight refresh lock. `per_page` is fixed on the server. Thresholds never enter the fetch key. After a failed refresh the source is not called again for five minutes.
- **All-or-nothing refresh with a quality gate.** Any page failure, or a failed sanity check, keeps the last good result. Sanity checks: at least one row; no value outside 1–99 in any `rs*` field (the meaning of the field changed); at most 10% unusable rows (missing symbol or floor, price ≤ 0, market cap ≤ 0 or above 5,000,000 billion VND); the row count within ±20% of the last good run, or at least 300 rows when there is no last good run. The last good result is served with `status = "stale"`, `stale = true` and its `asOf`, for at most 48 hours. After that the result is `status = "unavailable"` with the reason "Dữ liệu sàng lọc quá cũ". The flags are machine-readable in the API and in MCP.
- **HOSE fails closed.** If the status lists cannot be read (and no list younger than 48 hours exists), HOSE symbols are not put in `passed`. Those that pass every other rule go to `unverifiedHose`, shown separately. HNX and UPCOM rows carry a per-row note that their warning lists were not checked.
- **Layers.** Layer 1 (exclusions): HOSE special-monitoring lists (ids 33–38 and 47–53), HOSE delisting announcements for this year and next, average 10-session trading value ≥ 5 billion VND (volume × 10-session average price), price ≥ 5,000 VND, market cap ≥ 1,000 billion VND. Layer 2 (quality, can be skipped): profit after tax > 0 and ROE ≥ 12%; a P/E cap is optional and off by default. Layer 3 (trend): `rs3m` ≥ 50, configurable, worded as "xếp hạng sức mạnh 3 tháng ≥ P50 (trong toàn bộ mã niêm yết)", never as "stronger than the market". A null `rs3m` is its own exclusion bucket, "Thiếu dữ liệu xu hướng", counted in the exclusion counts and the per-sector counts, never dropped silently, and never part of the "good trend" trade-off bucket. UPCOM is opt-in.
- **Each symbol has one final state** (`passed`, `unverified_hose` or `excluded` with the first failing rule). Counts per reason, counts per ICB sector (so sector bias is visible) and a trade-off count ("loại vì định giá/chất lượng nhưng xu hướng tốt": excluded only by the quality layer or the optional P/E cap) are returned. A lookup by symbol explains why a symbol is missing.
- **Speculation is a flag, not an exclusion.** "Cờ biến động" show their value: `RS1T n/99, vốn hoá nhỏ` (rs1m ≥ 95 and market cap < 5,000 billion), `beta cao (n)` (beta > 1.8; zero is treated as missing), `RS52T n/99, vốn hoá nhỏ` (rs52w ≥ 90 and market cap < 5,000 billion), and `tăng nóng rồi gãy` (rs52w ≥ 90 and rs1m ≤ 20). The disclaimer says the flags rest on relative ranks and are not a risk assessment.
- **No score, no ranking.** The default order is alphabetical; the user may sort by any column in the page. There is no field named score, rank or potential. The disclaimer is always present: "Danh sách lọc theo tiêu chí, không phải khuyến nghị mua. Dữ liệu từ nguồn công khai không chính thức, có thể trễ. Cờ dựa trên xếp hạng tương đối, không phải đánh giá rủi ro."
- **Interface.** `GET /api/v1/market/screener` and MCP `screen_stocks` (read-only) share one query. Numeric parameters are clamped, `floors` accepts only HOSE, HNX, UPCOM, `includeExcluded` is paged and capped at 200 per page. Reason strings from HOSE are HTML-stripped on the server.

## Consequences

**Positive:**

- The upstream call count does not depend on how many people or agents query, or with what thresholds.
- A blocked or changed source degrades to "old data, flagged" or "no result, with the reason", never to a silent partial list.
- The percentile-rank meaning of `rs*` is pinned by a fixture test with a null row, and a change of that meaning fails the refresh.

**Negative / Trade-offs:**

- Data can be six hours old (and up to 48 hours old when flagged stale).
- Survivorship: delisted symbols are absent from the universe. HNX and UPCOM have no official warning list in v1, so those rows cannot be checked for special monitoring.
- Rows without a market cap are not returned by the source filter and so cannot be screened.
- Thresholds such as ROE ≥ 12%, ADTV ≥ 5 billion and rs3m ≥ 50 are judgement calls. The quality layer favours some sectors; per-sector counts are returned so the bias is visible, and the layer can be skipped.
- The Pullback state of a passed symbol (S1) is not shown in v1: reading it without extra history fetches needs a snapshot-only read of the adjusted store, which is a follow-up.

**Follow-ups (if any):**

- HNX and UPCOM warning lists once a source is found.
- A snapshot-only Pullback state for passed symbols, without new history fetches.

## References

- Docs: `docs/business-domain.md` (market screener), `docs/features.md`, `docs/architecture.md`
- Code: `Application/MarketData/Screener/` (`ScreenerRules`, `ScreenerModels`), `Application/MarketData/Queries/ScreenStocks/`, `Infrastructure/Services/Screener/` (`HmoneyScreenerDataSource`, `TechnicalFilterParser`, `HoseStatusParser`, `MarketScreenerService`), `Api/Mcp/MarketDataTools.cs` (`screen_stocks`)
- Config: `MarketDataProvider:BaseUrl` (the technical-filter host, shared with the existing provider), `Screener:HsxBaseUrl` (HOSE host, default is the public exchange API)
- Live check: `SCREENER_LIVE=1 dotnet test --filter ScreenerLiveSmoke`
