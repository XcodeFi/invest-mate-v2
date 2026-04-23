# Project Context — Investment Mate v2

## Project Owner

- **Trường Phạm Văn** — Vietnamese investor, full-stack developer
- Focus: disciplined investing workflow (Strategy → Plan → Trade → Risk → Compound Growth)
- Communication: Vietnamese language, technical terms in English
- Values: UX workflow continuity, Vietnamese language quality (diacritics critical)

## Project Vision

Transform from "trade recorder" to "opportunity finder":
- Phase 1-6: Foundation (CRUD, analytics, market data, trade wizard)
- Phase 7+: Intelligence (AI integration, smart signals, portfolio optimization)

## Key UX Decisions (từ UX evaluation 2026-03-12)

1. **Consolidated workflow** — Merged fragmented 16 pages into natural flows (Position Sizing → Trade Plan, Advanced Analytics → Analytics)
2. **Trade Wizard** — 5-step disciplined flow: Strategy → Plan → Checklist → Confirm → Journal
3. **Dashboard Cockpit** — Single-page overview: portfolio summary, equity curve, risk alerts, market indices, watchlist
4. **AI integration** — 11 use cases, supports "copy prompt" (no API key required) + streaming (with key)
5. **Vietnamese-first** — All UI text in Vietnamese with proper diacritics (dấu), commit messages in English

## Current Improvement Plan (Round 9, score 9.4/10 → target 10/10)

### Tier 1 — Done

1. **Watchlist** — ✅ CRUD, batch live prices, VN30 import, target prices, deep link to Trade Plan
2. **Smart Trade Signals** — ✅ EMA/RSI/MACD/Volume analysis, support/resistance, signal summary
3. **Portfolio Optimizer** — ✅ Concentration alerts, sector diversification (via IFundamentalDataProvider), correlation warnings, diversification score, recommendations

### Tier 2 — Done

4. AI Prompt Enhancement — ✅ Richer context for all 12 AI use cases
5. Risk Dashboard improvements — ✅ Position-level risk (beta, sector, positionVaR), trailing stop monitoring with real-time alerts

### Tier 3 — Planned

6. **Capital Flows Visibility** — 🔄 In Progress: TWR/MWR trên Dashboard + Analytics, flow markers trên equity curve, smart nudge, cash balance card
7. **Tài chính cá nhân** — ✅ Done 2026-04-22: Net Worth overview với **5 loại tài khoản** (CK/Tiết kiệm/Dự phòng/Nhàn rỗi + **Vàng tích trữ**), Financial Rules compliance (quỹ dự phòng 6 tháng, đầu tư ≤50%, tiết kiệm ≥30%), health scorecard 0-100, Dashboard widget + trang `/personal-finance`. **HmoneyGoldPriceProvider** crawler giá vàng từ 24hmoney (HTML scrape, 2-tier cache), Gold auto-calc Balance = quantity × live BuyPrice (giá tiệm mua vào = giá user bán được, định giá theo thanh khoản thực tế). 78 tests mới, 1013 total pass. Chi tiết: [`docs/plans/done/personal-finance.md`](plans/done/personal-finance.md)
8. **Tài chính cá nhân — Debt + Net Worth** — ✅ Done 2026-04-22: Entity `Debt` embedded trong FinancialProfile, 6 loại (CreditCard/PersonalLoan/Mortgage/Auto/Installment/Other), **Net Worth = Assets − Debt** card ở Dashboard widget + trang PF, **health rule 4** (−20 cứng khi consumer debt lãi > 20%/năm), banner cảnh báo nợ tiêu dùng lãi cao. Section "Khoản nợ" với click-to-edit + ESC + nút Lưu bên phải theo convention mới. 41 tests mới, 1055 total pass. Chi tiết: [`docs/plans/done/personal-finance-debt.md`](plans/done/personal-finance-debt.md)
9. **Vin-discipline V1 Backend** — ✅ Done 2026-04-23: Ép kỷ luật **thesis-driven** vào Trade Plan (Vinpearl Air 2020 inspiration). Rename `TradePlan.Reason` → `Thesis`; thêm `InvalidationCriteria` (5 trigger: EarningsMiss/TrendBreak/NewsShock/ThesisTimeout/Manual) + `ExpectedReviewDate` + `LegacyExempt`. **Size-based gate** fold vào `MarkReady`/`MarkInProgress`: plan ≥ 5% tài khoản ép Thesis ≥ 30 chars + ≥ 1 rule Detail ≥ 20 chars; nhỏ hơn chỉ cần 15 chars. **Mid-flight abort** (`AbortWithThesisInvalidation`) áp Ready/InProgress/Executed → raise `TradePlanThesisInvalidatedEvent`. **Discipline Score widget backend** (`GET /api/v1/me/discipline-score`): SL-Integrity 50% + Plan Quality 30% + Review Timeliness 20% + Stop-Honor Rate primitive, cache 5 phút. Migration-first deploy gate 2-step idempotent. 43 tests mới, 1106 total pass. Chi tiết: [`docs/plans/plan-creation-vin-discipline.md`](plans/plan-creation-vin-discipline.md). **V2 (deferred):** `ThesisReviewService` hosted cron + pending-review endpoint + Dashboard widget FE + behavioral pattern handler.

### Improvement Proposals (P1-P4) — Done

1. **P1: Post-Trade Review Workflow** — ✅ Pending review query, dashboard widget, trades journal column (dùng JournalEntry thay TradeJournal)
2. **P2: Stress Test Dynamic Beta** — ✅ Dynamic beta từ API, thay hardcoded estimatedBetas
3. **P3: Bollinger Bands + ATR** — ✅ 2 indicator mới, signal scoring 6 votes
4. **P4: Risk Budgeting** — ✅ MaxDailyTrades, DailyLossLimitPercent, budget card, form fields
5. **P3: TWR / MWR / CAGR fix (2026-04-19)** — ✅ TWR guards against near-zero snapshots + outlier periods; MWR uses gross trade totals for cash balance + divergence guard; FE CAGR annualizes backend TWR instead of raw endpoint ratio. Chi tiết: [`docs/plans/done/p3-twr-mwr-cagr-fix.md`](plans/done/p3-twr-mwr-cagr-fix.md)

### P2 Trade Plan Form Editability Matrix — Done (2026-04-18)

1. **P2: Editability Matrix (Strict, Option A)** — ✅ Form Trade Plan phân quyền sửa theo trạng thái:
   - Entry Info (symbol, direction, entry, qty, strategy, portfolio, entryMode, DCA) — chỉ Draft/Ready
   - Stop-Loss — Draft/Ready đầy đủ; InProgress chỉ được tighten (Long: newSl ≥ currentSl; Short: newSl ≤ currentSl); terminal read-only
   - Take-Profit, Exit Targets, Scenario Playbook — chỉ Draft/Ready
   - Risk Context (market/horizon/confidence), Checklist — Draft/Ready/InProgress; terminal read-only
   - Reason, Notes — sửa được mọi state trừ Cancelled
   - Campaign Review (lessons) — chỉ Reviewed
   - State banner ở đầu form thông báo rõ state + thao tác cho phép
   - Save buttons hiện theo state; Template panel ẩn khi non-Draft
   - Tighten-SL gate enforce ở `validateTightenSl()` gọi trước mutation
   - 45 frontend tests mới trong `trade-plan.component.spec.ts`
   - Chi tiết: [`docs/plans/done/p2-trade-plan-editability.md`](plans/done/p2-trade-plan-editability.md)

### P0.7 Campaign Review — Done

1. **P0.7: Campaign Review** — ✅ Đóng chiến dịch (TradePlan Executed → Reviewed) với auto-calculated P&L metrics, preview trước khi đóng, update lessons, pending-review list, cross-plan analytics page (`/campaign-analytics`), TimeHorizon enum, CampaignReviewData value object, CampaignReviewService, 33 new tests

### P7 Symbol Timeline Improvements — Done

1. **P7.1: Emotion ↔ P&L Correlation** — ✅ Correlation table, insight text
2. **P7.2: Confidence Calibration** — ✅ Calibration widget (Phù hợp/Quá tự tin/Chưa tự tin)
3. **P7.3: Behavioral Pattern Detection** — ✅ FOMO, PanicSell, RevengeTrading, Overtrading
4. **P7.4: Chart UX** — ✅ LineSeries thay CandlestickSeries
5. **P7.5: AI Timeline Review** — ✅ Rich context (correlation + calibration + patterns)
6. **P7.6: Emotion Trend** — ✅ Stacked bar theo tháng, trend insight
7. **P7.7: Export Timeline** — ✅ CSV export, clipboard copy
8. **P7.8: Vietstock Event Crawl** — ✅ Auto-crawl news + events, CSRF flow, dedup

## Common Pitfalls (từ past bugs)

- **`[contextData]="{}"` in Angular templates** — Creates new object reference every change detection cycle, causes infinite loop. Use `readonly emptyContext = {}` as stable reference.
- **`CancelAfter()` in .NET** — Only cancels the token, doesn't stop tasks that don't check it. Use `.WaitAsync(TimeSpan)` which throws `TimeoutException` independently.
- **24hmoney prices** — API returns prices in units of 1,000 VND. Must multiply by 1,000.
- **MongoDB Atlas** — Seed/connection takes ~16s on cold start. Backend launch uses `--launch-profile https` for port 5000.
- **`appsettings.json` placeholders** — .NET doesn't interpolate `{PlaceholderName}` in JSON. Must use real URLs as defaults or environment variables.
- **Money/StockSymbol equality** — `other != null` in `Equals()` triggers custom `!=` operator → `StackOverflowException`. Use `other is not null`.
- **24hmoney gold price format** — UI label nói "Đơn vị: triệu VNĐ/lượng" nhưng HTML values thật là **full VND** (167,200,000). Ngược với giá CP (÷1000 trong API). Fixture test `PricesAreFullVND_NotScaledBy1000` lock behavior khi mở rộng crawler.
- **Mongo index rename conflict** — Thêm `Name` explicit vào `CreateIndexOptions` cho index đã có auto-name trước đó → Mongo throw `createIndexes failed: Index already exists with a different name`. Fix: bỏ Name OR wrap catch narrow `MongoCommandException when (ex.Code is 85 or 86)` (IndexOptionsConflict/IndexKeySpecsConflict).
- **AngleSharp namespace conflict** — Project có `InvestmentApp.Infrastructure.Configuration` namespace shadow `AngleSharp.Configuration` → phải fully qualify `AngleSharp.Configuration.Default` khi dùng.
- **Modal overlay z-index must be ≥ [60]** — Header dùng `sticky top-0 z-50` → overlay `z-50` tie → header không bị overlay che. Dùng `z-[60]` trở lên cho fullscreen modal. Áp dụng cả cho debt form modal và account form modal.
- **Primary button on the right** — Convention toàn project cho modal: order `[Hủy] → [destructive?] → [primary Lưu/Xác nhận]`. Primary button thường `flex-1` để chiếm độ rộng. Muscle-memory thumb reach — tránh user vô tình cancel thay vì save.
- **appsettings.json convention** — URL + secret không commit thật, dùng placeholder `{Section__Key}` + inject env var lúc deploy. Reference pattern: `MarketDataProvider__BaseUrl`, `GoldPriceProvider__PageUrl`. Nếu quên set env var, app không crash startup — fail silently ở request đầu tiên với DNS error.
- **BsonElement alias không hoạt động trên MongoDB driver 3.6.0** — driver chỉ hỗ trợ **1 key per property** trong BsonClassMap, không có dual-key alias để "đọc cả `reason` lẫn `thesis`" trong deserializer. Kết quả: rename field Mongo phải dùng **migration-first deploy gate** — chạy script `$rename reason thesis` **trước** khi deploy container code mới, nếu deploy code trước sẽ silent data loss (docs cũ deserialize với `Thesis = null`). Phát hiện khi làm Vin-discipline 2026-04-23. Gồm: (1) backup collection qua Mongo Atlas snapshot; (2) chạy migration idempotent 2-step (filter `legacyExempt: { $exists: false }` step 1, `thesis: ""` độc lập step 2); (3) deploy; (4) post-deploy smoke test.
