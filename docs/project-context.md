# Project Context — Investment Mate v2

## Project Owner

- **XcodeFi** — Vietnamese investor, full-stack developer
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
9. **Vin-discipline V1 Backend** — ✅ Done 2026-04-23: Ép kỷ luật **thesis-driven** vào Trade Plan (Vinpearl Air 2020 inspiration). Rename `TradePlan.Reason` → `Thesis`; thêm `InvalidationCriteria` (5 trigger: EarningsMiss/TrendBreak/NewsShock/ThesisTimeout/Manual) + `ExpectedReviewDate` + `LegacyExempt`. **Size-based gate** fold vào `MarkReady`/`MarkInProgress`: plan ≥ 5% tài khoản ép Thesis ≥ 30 chars + ≥ 1 rule Detail ≥ 20 chars; nhỏ hơn chỉ cần 15 chars. **Mid-flight abort** (`AbortWithThesisInvalidation`) áp Ready/InProgress/Executed → raise `TradePlanThesisInvalidatedEvent`. **Discipline Score widget backend** (`GET /api/v1/me/discipline-score`): SL-Integrity 50% + Plan Quality 30% + Review Timeliness 20% + Stop-Honor Rate primitive, cache 5 phút. Migration-first deploy gate 2-step idempotent. 43 tests mới, 1106 total pass. Chi tiết: [`docs/plans/plan-creation-vin-discipline.md`](plans/plan-creation-vin-discipline.md).
10. **Vin-discipline V2.1 — Pending reviews page + locale vi-VN** — ✅ Done 2026-04-23: `GetPendingThesisReviewsQuery` + `GET /api/v1/me/thesis-reviews/pending` (filter Ready/InProgress, exclude LegacyExempt + triggered, sort DESC theo DaysOverdue với VN-local timezone UTC+7). Frontend: trang `/pending-reviews` với urgency color cards + badge trigger type cụ thể (KQKD lệch / Gãy trend / Tin tức đột biến / Quá hạn / Tự nhận xét / Review định kỳ); Dashboard widget thêm count badge "🔔 [N] Plan cần review lý do đầu tư →"; widget ẩn khi `totalPlans === 0`. **Locale vi-VN** đăng ký global trong `main.ts` — DatePipe/CurrencyPipe format kiểu VN mặc định. **Việt hóa** "Thesis" → "Lý do đầu tư" xuyên suốt UI 4 files (widget, trade-plan form, pending-reviews page, trade-replay). Giữ TypeScript identifiers không đổi. **Review fixes** từ 3-agent (architect + UX + risk) trước merge: timezone VN day-granularity, `GetActiveByUserIdAsync` thay `GetByUserIdAsync` (perf), widget flash reset, skip LegacyExempt. 10 handler tests mới (#1-10), 146 Application tests pass. Merged PR #94 (squash `304421dc`).

### V2+ Roadmap — **trial window 1-2 tuần trước khi invest tiếp**

**2026-07-21 — AI Agent write surface landed (ADR-0004):** NPU/Claude có thể lập/sửa/chuyển-trạng-thái/ghi-trade qua `POST /api/v1/ai/agent/*` (ApiKey auth, ownership-enforced). IDOR trong `CreateTradeCommand` + `BulkCreateTradesCommand` đã được đóng (`portfolio.UserId == sub` assert).

User đang dùng thực tế để cảm nhận UX trước khi quyết đầu tư V2.2+. Deferred items:

- **V2.2 — ThesisReviewService cron:** hosted daily 07:00 Asia/Ho_Chi_Minh → tạo Notification/AlertHistory cho plan due. User mở app thấy nudge. Effort ~2 ngày.
- **V2.3 — P7 BehavioralPattern handler:** `TradePlanThesisInvalidatedEvent` → detect `DisciplinedAbort` (trigger match thực tế) vs `SunkCostHold` (drawdown sâu không abort). Effort ~1 ngày.
- **V3 — /discipline-report drill-down:** cost-of-violations (Tradervue-inspired), period-over-period compare. Effort ~3 ngày.
- **V4 — Core/Satellite:** `Portfolio.CoreTargetPercent` default 70%, size-based gate extension.
- **V5 — Drawdown escalation ladder:** 5% / 15% / 30% → force modal review thesis.

### UX polish backlog (3-agent review PR #94)

- Pending reviews card inline action "Vẫn giữ (reset review date)" / "Cắt ngay" (modal abort in-place) thay vì điều hướng `/trade-plan`
- Form "Kỷ luật mua" accordion trên mobile (3 section stack quá dày trên 360px)
- Empty state `/pending-reviews` CTA "Tạo plan mới" / "Xem tất cả plan"
- Portfolio name badge trên card pending-review (khi user có > 1 portfolio)
- Terminology revisit "Lý do đầu tư" vs "Luận điểm mua" — retail VN test coverage chưa đủ để chốt

### Tài chính cá nhân — Goals/Forecast/Actual (Personal Fund layer)

**Framing tổng:** vận hành tài chính cá nhân như **một quỹ tài chính quy mô cá nhân** (personal fund / one-person family office) — kết hợp lập báo cáo tài chính + kế hoạch năm như một công ty. User là "CFO của bản thân": đầu năm lập kế hoạch (mục tiêu + ngân sách phân bổ), trong năm theo dõi thực tế vs kế hoạch (variance analysis), cuối kỳ ra báo cáo (đạt/lệch bao nhiêu, vì sao). Phần "Tài chính cá nhân" hiện tại đã có **5 loại tài khoản (CK / Tiết kiệm / Dự phòng / Nhàn rỗi / Vàng) + phân bổ theo Financial Rules** — đó là phần *asset allocation* của quỹ. Goals/Forecast/Actual hoàn thiện thêm 3 trụ còn thiếu để ghép thành quỹ đầy đủ: **Plan → Forecast → Actual**.

📋 **Plan đầy đủ:** [`docs/plans/personal-finance-goals.md`](plans/personal-finance-goals.md) — chia 4 phase V1→V4, ~14 file mới + 6 modify, ~75 tests, ~8.6 person-days. Hợp nhất từ 3-agent review (Architect / UX / Domain-Risk) ngày 2026-05-05.

Tóm tắt 3 trụ:

- **V1 — Mục tiêu (Goals)** — entity `Goal` embedded trong `FinancialProfile`, hard partition `Allocations[].AllocationVnd` per account (chống double-count), state machine Active/Achieved/Expired/Abandoned. Modal CRUD trên `/personal-finance` chèn giữa "Sức khỏe tài chính" và "Tài khoản".
- **V2 — Snapshot cron + Actual history** — collection `goal_progress_snapshots`, monthly cron `/internal/jobs/networth-snapshot`, chart Actual % vs Linear expected % qua tháng (lightweight-charts).
- **V3 — Forecast** — `IGoalForecastService` với 30-day SMA inputs (chống fluctuation Securities/Gold), accumulation rate split contribution vs market gains (winsorize p10/p90), blended CAGR per-account-type (CK 8% / Savings = user avg / Gold 6% / Emergency+IdleCash 0%), reuse `RateSource` enum + `[-10%, +50%]` caps từ `GetSavingsComparisonQuery`.
- **V4 — Rule-conflict gate** — `GOAL_RULE_CONFLICT` 400 khi goal vi phạm `MaxInvestmentPercent ≤ 50%`, copy pattern `DISCIPLINE_GATE_FAILED` từ Vin-discipline V1. Inline banner gợi "trả nợ tiêu dùng lãi cao trước, mục tiêu sau" khi `HasHighInterestConsumerDebt() == true`.

**Lưu ý lịch:** Plan này KHÔNG thay V2.2 (ThesisReviewService cron) đang là next-up của Vin-discipline. Hai cron job độc lập, không conflict. Nếu chốt làm Goals trước, V2.2 lùi 1-2 tuần.

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

- **"Cổ tức 5%" là 5% MỆNH GIÁ, không phải 5% giá thị trường** — mệnh giá CP niêm yết VN cố định 10.000đ, nên 5% = 500đ/CP. Với mã giá 55.000 thì đó chỉ là 0,91%. Entity `CorporateAction` bắt buộc lưu `AmountPerShare` đã quy đổi ra đồng, không lưu số `5` trần — nếu để trần, sớm muộn sẽ có chỗ nhân nhầm vào giá thị trường. Đối xứng: cổ tức **cổ phiếu** 30% thì giá giảm `1/1,3` = **23,08%**, không phải 30%.
- **Ngưỡng giá tuyệt đối phải điều chỉnh theo sự kiện quyền** — `StopLossTarget` lưu `EntryPrice`/`StopLossPrice`/`TargetPrice`/`TrailingStopPrice` là giá tuyệt đối tại lúc đặt. Sau ngày GDKHQ giá thị trường bị điều chỉnh giảm 23% → ngưỡng cũ bị xuyên thủng ngay lập tức dù vị thế vẫn lãi, cắt lỗ bắn nhầm hàng loạt. Điều chỉnh **tại thời điểm đọc** qua `CorporateActionAdjuster`, không sửa dữ liệu (xoá sự kiện thì ngưỡng tự quay về cũ). Cùng bẫy áp cho mọi field giá tuyệt đối lưu kèm timestamp.
- **Toán vị thế bị nhân bản ~15 service** — trước ADR-0010, mỗi service tự `GroupBy(t => t.Symbol)` trên `Trade` thô. Thêm bất kỳ thứ gì làm lệch số lượng/giá vốn (sự kiện quyền, lot matching, phí) là mỗi màn hình ra một con số khác nhau. Nguồn duy nhất giờ là `PositionBuilder.Build(trades, actions, asOf)`. Trước khi viết logic tính giá vốn mới, kiểm tra service đó đã gọi `PositionBuilder` hay `IPnLService` chưa — nhiều service đã đi qua `IPnLService` nên tự đúng, sửa thêm là thừa.
- **Điều chỉnh khi đọc không dùng được cho giá trị bị ghi đè trở lại** — mẫu "điều chỉnh tại thời điểm đọc, không sửa dữ liệu" chỉ đúng với ngưỡng do người dùng đặt. `TrailingStopConfig.HighestPrice`/`CurrentTrailingStop` là quan sát thị trường rồi được ghi đè lại entity: điều chỉnh khi đọc sẽ hạ chồng lần vì lần ghi kế tiếp lưu giá ở mặt bằng mới. Với loại giá trị này phải quy đổi **đúng một lần** và lưu mốc (`PriceBasisAt`) để biết đã quy đổi rồi. Dấu hiệu nhận biết: cùng một field vừa được đọc để so sánh, vừa bị gán lại trong cùng một vòng.
- **Không phải field số nào cạnh giá cũng là giá** — trong `ScenarioNode`, `ConditionValue` là giá với `PriceAbove`/`PriceBelow` nhưng là phần trăm với `PricePercentChange` và số ngày với `TimeElapsed`; `TrailValue` chỉ là tiền khi `Method = FixedAmount`, còn lại là phần trăm hoặc bội số ATR. Áp điều chỉnh giá lên tất cả sẽ làm hỏng những cái không phải giá mà không có test nào đỏ. Kiểm tra `ConditionType`/`Method` trước khi đụng vào. Thêm nữa, **khoảng cách** giá (biên trượt, bước nhảy) chỉ chia hệ số, không trừ cổ tức tiền mặt.
- **Trung bình không trọng số trên trade thô lệch được cả dấu** — `buys.Average(b => b.Price)` coi lệnh 100 CP và lệnh 900 CP như nhau. Mua 100 @ 20.000 rồi 900 @ 30.000 cho giá vốn 25.000 thay vì 29.000; bán ở 28.000 hiện ra **lãi** trong khi thực tế **lỗ** 1 triệu. Nguy hiểm nhất khi con số đó nuôi cơ chế khoá giao dịch. Lãi/lỗ theo ngày nên lấy hiệu hai lần dựng vị thế (`asOf` hôm nay trừ `asOf` hôm qua) thay vì tự khớp lệnh mua với lệnh bán.
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
- **MCP tool nhận MediatR command làm param → schema bọc `{"command":{…}}`** — SDK coi complex-type param là một object lồng, nên caller gửi args phẳng bị `ArgumentException: missing a value for the required parameter 'command'` **giống nhau ở mọi lần thử**, bất kể sửa field nào → rất dễ chẩn đoán sai thành "server lỗi" (tool read-only vẫn chạy bình thường càng làm lệch hướng). Tool param phải FLAT, optional đặt sau `ct` với `= null`. Bẫy thứ hai: unit test gọi **trực tiếp** static method (`Tools.CreateX(new XCommand{…}, …)`) bỏ qua hoàn toàn tầng binder — chỗ duy nhất bug tồn tại — nên test xanh mà production vẫn chết. Test phải invoke qua `McpServerTool.InvokeAsync` với args phẳng. Xem ADR-0008, guard `No_Tool_Wraps_Its_Args_In_A_Command_Object`.
- **BsonElement alias không hoạt động trên MongoDB driver 3.6.0** — driver chỉ hỗ trợ **1 key per property** trong BsonClassMap, không có dual-key alias để "đọc cả `reason` lẫn `thesis`" trong deserializer. Kết quả: rename field Mongo phải dùng **migration-first deploy gate** — chạy script `$rename reason thesis` **trước** khi deploy container code mới, nếu deploy code trước sẽ silent data loss (docs cũ deserialize với `Thesis = null`). Phát hiện khi làm Vin-discipline 2026-04-23. Gồm: (1) backup collection qua Mongo Atlas snapshot; (2) chạy migration idempotent 2-step (filter `legacyExempt: { $exists: false }` step 1, `thesis: ""` độc lập step 2); (3) deploy; (4) post-deploy smoke test.
