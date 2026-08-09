# Plan: Personal Tool Audit + Indicator Tier Cleanup (v2)

> Status: Draft v2 (pivoted after critique)
> Author: XcodeFi
> Created: 2026-05-05
> Updated: 2026-05-05 (pivot — skip heavy instrumentation, cut deadwood ngay hôm nay)
> Roadmap parent: Personal infrastructure maturity (post v2.57.0)

---

## 0. Pivot summary (đọc trước)

Plan v1 (gốc) định build telemetry pipeline (Domain entity + CQRS + repo + Mongo index + REST controller + admin SPA + 9-15 tests) để chứng minh "tôi build quá nhiều." 3 agent reviewer độc lập đều ra cùng kết luận: **plan v1 là chính cái bệnh nó định chữa.**

Pivot v2:

| Thay đổi | v1 | v2 |
|---|---|---|
| Phase 1 (Instrument) | 12 file mới + 9-15 test, 1 tuần build | ~50 LOC localStorage tracker, 1 buổi |
| Cut deadwood | Phase 4, sau 30 ngày data | **Hôm nay** — `git rm` 5 feature gut đã gọi tên |
| KPI chính | Feature usage count (per-page click) | BUILD hours vs INVEST hours (manual end-of-day log) |
| Feature gate | 5-question self-judged | 5-question + 1 external reviewer (hard gate) |
| Cut window | 0 usage / 30 ngày → cut | 0 usage / **rolling 90 ngày** → cut (tránh false positive với quarterly-legitimate) |
| Icebox | 30-day cooling, ý tưởng vào ngay | **Pain log** — ý tưởng chỉ được ghi sau lần thứ 3 đau trong journal |
| No-ship week | Negative lever ("don't ship") | Positive substitute list (1 BCTC + 1 macro talk + 1 thesis + 1 portfolio review) |
| Daily Routine streak | Giữ widget có streak gamification | **Bỏ streak ngay** — cùng pattern dopamine bị audit |

**One sharp question** (lưu lại để self-check khi tay ngứa muốn build): *"Nếu một người bạn không biết codebase bảo: 'skip toàn bộ instrument — 30 ngày tới chỉ track BUILD hours vs INVEST hours trong sổ tay, nhờ 1 người duyệt feature trước merge', mình sẽ mất gì? Mất đó là về returns đầu tư, hay về không được build audit tool?"*

---

## 1. Mục tiêu

Sau 57 phiên bản trong 2.5 tháng, Invest Mate đã có 50+ feature lớn. Vì sản phẩm phục vụ riêng cá nhân (đầu tư cả đời), không scale lên thị trường:

- **Cắt redundancy**: nhiều indicator/feature chồng tín hiệu, không thêm giá trị quyết định
- **Tách Execution mode vs R&D mode**: UI chính chỉ hiện những gì ra quyết định thật, R&D feature sau toggle riêng
- **Đo BUILD/INVEST ratio thật** (không phải feature usage) — KPI gốc của plan này
- **Đảm bảo INVEST > BUILD**: hiện ~70/30, mục tiêu invert sang 25/75

Out-of-scope: monetization, multi-user, B2B pivot, tracker pipeline, feature-flag framework. Tool này phục vụ 1 user.

---

## 2. Bối cảnh

### 2.1 Triệu chứng quan sát được

Từ changelog v2.0 → v2.57:

| Cluster | Số feature | Likely usage |
|---|---|---|
| Technical Indicators | 12+ (EMA, RSI, MACD, Bollinger, ATR, Stochastic, ADX, OBV, MFI, Fibonacci, Confluence Score, Divergence) | 4-5 dùng thật, còn lại R&D |
| AI Use Cases | 12 (Risk, Position, Trade, Watchlist, Daily Brief, Plan Advisor, Portfolio Review, Monthly, Journal, Stock Eval, Comprehensive, Chat) | 3-5 dùng thật |
| Position Sizing | 5 models (Fixed, Kelly, ATR, Turtle, Volatility-Adjusted) | 1 dùng thật |
| Stop Loss Methods | 5 (Manual, ATR, Chandelier, MA Trailing, S/R) | 1-2 dùng thật |
| Strategy Templates | 7 strategies với P5 fields đầy đủ | 1-2 dùng thật |

Bảng trên đã đủ tự tin — không cần build telemetry để xác nhận.

### 2.2 Cognitive load + redundancy

| Nhóm | Đo gì | Indicators trong project | Chồng chéo |
|---|---|---|---|
| Trend | Hướng giá | EMA20, EMA50, EMA200, ADX | EMA crossover ≈ ADX rising |
| Momentum | Tốc độ thay đổi | RSI, MACD, Stochastic, MFI | 4 cái cho cùng tín hiệu 80% thời gian |
| Volatility | Biên độ | Bollinger Bands, ATR | Bollinger width ≈ ATR |
| Volume flow | Dòng tiền | OBV, MFI, Volume ratio | OBV ≈ Volume ratio cumulative |
| Price level | Vị trí vs S/R | Fibonacci, S/R levels, Bollinger %B | Bollinger %B = price/(upper-lower) |

→ **4 momentum indicator** maintained nhưng chỉ cần 1-2 cho quyết định. Confluence Score che đậy redundancy chứ không xoá.

### 2.3 Time allocation tension

Build-mode 2.5 tháng = ~28 features/tháng. Hiện estimate BUILD 70%, INVEST 25%. Mục tiêu invert. Đo bằng **manual end-of-day log** (notebook hoặc 1 spreadsheet 2 cột), không bằng app.

---

## 3. Quyết định chốt

### Q1: Tier hoá indicator theo Execution vs Research

**Execution Tier** (mặc định trên `/market-data`):
- Volume + Volume ratio
- EMA20 + EMA50 + EMA200
- RSI(14)
- MACD(12,26,9)
- S/R levels

→ 7 signal đủ 95% quyết định retail (xem Appendix B — không top trader public nào dùng >5).

**Research Tier** (sau toggle "Phân tích nâng cao", FE-only conditional render):
- Bollinger Bands, ATR, Stochastic, ADX+DI, OBV, MFI, Fibonacci, Confluence Score, Market Condition Classifier, Divergence Detection

→ Code giữ nguyên, FE chỉ ẩn UI mặc định. **Chấp nhận** BE vẫn compute full payload — không build conditional fetching (đó là build project khác). Nếu sau 90 ngày không bật toggle lần nào → `git rm` luôn (không freeze 60 ngày).

### Q2: Position Sizing — chốt 1 default + 4 alternative

- Default: **Fixed Risk %**
- 4 còn lại (Kelly, ATR, Turtle, Volatility-Adjusted): collapsed sau "So sánh mô hình" button (đã có)
- Review sau 90 ngày — nếu localStorage counter <3 lần/90d → `git rm`

### Q3: AI Use Cases — gom theo intent

12 use case → 4 group (entry-point thay đổi, code giữ nguyên):

| Group | Use cases gốc | Entry point |
|---|---|---|
| Trước trade | Stock Evaluation + Trade Plan Advisor + Comprehensive Stock Analysis | `/market-data` + `/trade-plan` |
| Trong trade | Position Advisor + Risk Assessment | `/positions` + `/risk-dashboard` |
| Sau trade | Trade Analysis + Monthly Summary + Journal Review + Portfolio Review | `/symbol-timeline` + `/monthly-review` + `/journals` |
| Hằng ngày | Daily Briefing + Chat + Watchlist Scanner | Header AI button + `/dashboard` |

### Q4: Daily Routine — bỏ streak ngay

Streak gamification là cùng pattern dopamine đẩy build-mode. Tự audit nó bằng tracker streak khác là contradiction.

- **Hôm nay**: bỏ streak counter khỏi widget Daily Routine (giữ checklist, không giữ "đã 12 ngày liên tục")
- Sau 60 ngày: nếu localStorage không bump → `git rm` widget khỏi dashboard

### Q5: Build cadence + external accountability

- **No-ship week 1 lần/tháng** — định nghĩa positively: tuần đó phải xong **1 BCTC + 1 macro talk/podcast + 1 thesis post + 1 portfolio review**. Không phải "đừng ship" — phải có substitute.
- **Feature gate 5-question** (Section 6) + **1 external reviewer**: trước merge, 1 người ngoài (vợ/bạn/coach/Twitter post) phải confirm. Self-judged gate là pass-rate ~90% — không phải gate.
- **Pain log** thay icebox: ý tưởng chỉ được ghi vào `docs/icebox.md` sau khi pain xuất hiện **3 lần** trong daily journal. Không capture trước.

---

## 4. Phase breakdown

### Phase A — Hôm nay, 1 buổi (~3-4h)

**Mục tiêu**: cut deadwood ngay, tracker tối thiểu, KPI gốc đo được.

**Việc 1 — Cut 5 feature (30 phút)**:
- Chọn 5 feature gut đã gọi tên (Section 2.1) — VD: 2 momentum indicator dư (Stochastic, MFI), 1 stop-loss method (Chandelier), 1-2 AI use case rủi ro overlap.
- `git rm` (không feature flag, không freeze 60 ngày). Tag `pre-cut-2026-05-05` trước để có `git revert` nếu miss.
- Document trong `docs/plans/audits/cuts-2026-q2.md` — 1 dòng/feature: tên + lý do.

**Việc 2 — LocalStorage usage tracker (~50 LOC, 30 phút)**:

```typescript
// frontend/src/app/core/services/usage-tracker.service.ts (~30 LOC)
@Injectable({ providedIn: 'root' })
export class UsageTrackerService {
  private key = 'usage_v1';
  constructor(router: Router) {
    router.events.pipe(filter(e => e instanceof NavigationEnd))
      .subscribe((e: NavigationEnd) => this.bump(e.urlAfterRedirects));
  }
  bump(featureKey: string) {
    const data = JSON.parse(localStorage.getItem(this.key) ?? '{}');
    const e = data[featureKey] ?? { count: 0, last: null, first: null };
    e.count++; e.last = Date.now(); e.first ??= Date.now();
    data[featureKey] = e;
    localStorage.setItem(this.key, JSON.stringify(data));
  }
}
```

```typescript
// frontend/src/app/features/admin/usage-dashboard.component.ts (~20 LOC)
@Component({ standalone: true, template: `
  <table><tr *ngFor="let row of rows"><td>{{row.k}}</td><td>{{row.count}}</td><td>{{row.last|date}}</td></tr></table>
  <button (click)="export()">Export JSON</button>` })
export class UsageDashboardComponent {
  rows = Object.entries(JSON.parse(localStorage.getItem('usage_v1') ?? '{}'))
    .map(([k,v]: any) => ({ k, ...v })).sort((a,b) => b.count - a.count);
  export() { navigator.clipboard.writeText(localStorage.getItem('usage_v1') ?? ''); }
}
```

- Route `/admin/usage`. 1 smoke test: click → row xuất hiện. Không Domain/Application/Infrastructure/API/test.

**Việc 3 — Tier UI toggle (~1h)**:
- `MarketDataComponent`: thêm toggle "Phân tích nâng cao" (default off, persist `localStorage`).
- Khi off: render Execution Tier (7 signal), ẩn 10 Research card.
- 1 Karma test (toggle hide/show + persist).

**Việc 4 — KPI tracker (5 phút)**:
- Tạo `docs/personal/build-vs-invest-log.md` với template 2 cột BUILD hours / INVEST hours, end-of-day log.
- Cách đo: notebook giấy hoặc 1 dòng commit message cuối ngày, không build app.
- **Tuyệt đối không** build "BUILDvInvestTrackerService" — đó là Phase 1 v1 quay lại.

### Phase B — Tuần 1, ~1-2h (markdown only, 0 LOC)

**Mục tiêu**: kỷ luật bằng văn bản + external accountability.

- `docs/icebox.md` — template **pain log** (Appendix D). Ý tưởng chỉ ghi sau 3 lần pain trong journal.
- `docs/personal/investing-log.md` — daily template: invest hours, decisions, learnings, build-urge events.
- `docs/personal/no-ship-week-template.md` — checklist 4 substitute task (1 BCTC + 1 macro talk + 1 thesis + 1 portfolio review).
- `CONTRIBUTING.md` thêm section "Feature Gate" (Section 6) với 5 câu + **external reviewer requirement**.
- Chốt 1 external reviewer (vợ/bạn/coach hoặc public post Twitter/Substack trước mỗi ship). Ghi tên người đó vào CONTRIBUTING.md.

### Phase C — Sau 30 ngày: re-evaluate

**Pre-condition**: 30 ngày data từ localStorage tracker + 30 ngày BUILD/INVEST log.

**Decision rule** (rolling **90 ngày**, không 30):
- 0 usage / 90 ngày → `git rm` (không feature flag, không freeze)
- <3 usage / 90 ngày → freeze (không refine, không feature mới liên quan)
- ≥3 usage / 90 ngày → keep, consider polish

90 ngày tránh false positive cho quarterly-legitimate features (tax review, monthly summary, year-end rollup).

**Acceptance Phase C**:
- Đọc localStorage `/admin/usage` JSON
- Cut tất cả feature 0 usage (estimate 7-12)
- Verify BUILD/INVEST log: nếu chưa shift về <50% BUILD → mandatory 2-week ship freeze
- Document trong `docs/plans/audits/2026-q2-audit.md`

### Phase D — Tương lai (sau 90 ngày, có data)

Re-read this plan. Nếu BUILD ratio vẫn >40% → escalate accountability (Beeminder $50/day forfeit, hoặc paid coach).

---

## 5. UX changes

### Smart Signals trên `/market-data` (Phase A Việc 3)

**Trước**: 12 indicator cards xếp hàng grid, scroll dài.

**Sau**:
```
┌─ Tín hiệu giao dịch ──────────────────────┐
│ [strong_buy / buy / hold / sell / strong_sell]│
│ Gợi ý: Entry / SL / TP / R:R              │
└────────────────────────────────────────────┘

┌─ Cốt lõi (Execution) ─────────────────────┐
│ [EMA20] [EMA50] [EMA200]                  │
│ [RSI(14)] [MACD(12,26,9)]                │
│ [Volume + ratio]                          │
│ [Hỗ trợ / Kháng cự]                       │
└────────────────────────────────────────────┘

[Toggle: Phân tích nâng cao ▼]
   ↓ chỉ hiện khi bật
┌─ Nâng cao (Research) ─────────────────────┐
│ [Bollinger] [ATR] [Stochastic]            │
│ [ADX +DI/-DI] [OBV] [MFI]                 │
│ [Fibonacci] [Confluence Score]            │
│ [Market Condition] [Divergence]           │
└────────────────────────────────────────────┘
```

### AI grouped by intent

Header AI button → dropdown 4 group thay vì list 12 phẳng.

### Dashboard widget tier

- Giữ tier S widget (Decision Queue, NetWorth, Quick Trade, Watchlist top 5)
- Daily Routine widget: **bỏ streak ngay** (Q4)
- Compound Growth Tracker → "More" collapsible

---

## 6. Feature gate (5 câu + external reviewer)

Trước khi ship feature mới, trả lời 5 câu (lưu trong CONTRIBUTING.md):

1. **Pain test**: 7 ngày qua tôi đã GẶP pain này bao nhiêu lần? (Cần ≥3 lần — verify trong journal, không phải tự nhớ)
2. **Workaround test**: Có thể giải quyết bằng Excel/Notion/sticky-note 5 phút không?
3. **DRY test**: Feature này thay thế / merge cái cũ hay chỉ thêm?
4. **Maintenance test**: 6 tháng tới feature này cost bao nhiêu giờ maintain?
5. **Joy vs Need test**: Tôi build vì THỰC SỰ CẦN hay vì BUILD THÌ SƯỚNG?

→ Pass ≥4/5 + **external reviewer confirm** mới ship. Còn lại → icebox sau khi pain xuất hiện 3 lần.

---

## 7. Acceptance criteria

**Phase A done when** (hôm nay):
- [ ] 5 feature đã `git rm` + tagged + documented
- [ ] LocalStorage tracker live, 1 row xuất hiện sau click test
- [ ] Tier UI toggle hoạt động + persist
- [ ] `build-vs-invest-log.md` có entry ngày đầu tiên

**Phase B done when** (tuần 1):
- [ ] icebox.md, investing-log.md, no-ship-week-template.md tồn tại với template
- [ ] CONTRIBUTING.md có Feature Gate section + tên external reviewer
- [ ] 7 ngày đầu fill investing-log đầy đủ

**Phase C done when** (sau 30 ngày):
- [ ] Cut thêm 5+ feature có 0 usage / rolling 90 ngày
- [ ] BUILD ratio đo được, có trend line 30 ngày
- [ ] Document audit trong `docs/plans/audits/2026-q2-audit.md`

---

## 8. Test plan

| Layer | Test type | Phase |
|---|---|---|
| Frontend | usage-tracker localStorage smoke (click → row) | A |
| Frontend | market-data toggle persist + render | A |

Total: **2 tests**. (v1 plan: 15-20 tests cho personal tracker — disproportionate.)

---

## 9. Risks & mitigations

| Risk | Mức | Mitigation |
|---|---|---|
| Cut nhầm feature → cần restore | Low | Git tag `pre-cut-2026-05-05` + commit history; `git revert` đủ |
| Toggle ẩn UI khiến quên indicator có sẵn | Low | Nhỏ — research mode bật được dễ; không phải vấn đề lớn |
| Audit bias (tự dối lòng dùng nhiều) | High | localStorage data hard-stop (90d); BUILD/INVEST log không tự edit lại |
| Build-urge quay lại sau 1-2 tuần | High | External reviewer + no-ship week với positive substitutes; **không** build framework chống build-urge |
| Plan v2 này tự thành build project (lặp v1) | Medium | Phase A chốt deadline 1 buổi; nếu vượt 4h → STOP, ship phần đã xong |
| BUILD/INVEST log drop-off sau 3 tuần | Medium | External reviewer cũng review weekly log; Beeminder fallback nếu drop |

---

## 10. Files affected

### Mới (Phase A — code)

- `frontend/src/app/core/services/usage-tracker.service.ts` (~30 LOC)
- `frontend/src/app/features/admin/usage-dashboard.component.ts` (~20 LOC)

### Mới (Phase B — markdown only)

- `docs/icebox.md` (pain-log template)
- `docs/personal/investing-log.md`
- `docs/personal/build-vs-invest-log.md`
- `docs/personal/no-ship-week-template.md`
- `docs/plans/audits/2026-q2-audit.md`
- `docs/plans/audits/cuts-2026-q2.md`
- `CONTRIBUTING.md` (append Feature Gate section)

### Sửa

- `frontend/src/app/features/market-data/market-data.component.ts` (toggle + conditional render)
- `frontend/src/app/features/dashboard/dashboard.component.ts` (bỏ streak Daily Routine + collapsible Compound Growth)
- `frontend/src/app/shared/components/ai-chat-panel/ai-chat-panel.component.ts` (group AI use cases)
- `frontend/src/app/app.routes.ts` (route /admin/usage)

### Xoá (Phase A — `git rm`)

5 feature gut-call (chốt lúc thực hiện, không pre-commit ở đây):
- 1-2 momentum indicator dư (candidates: Stochastic, MFI)
- 1 stop-loss method ít dùng (candidate: Chandelier)
- 1-2 AI use case overlap (candidates: comprehensive-stock-analysis nếu trùng stock-evaluation)

### KHÔNG mới (so với v1)

- ~~`UsageEvent.cs`~~ — không build Domain entity
- ~~`TrackUsageBatchCommand.cs`~~ — không build CQRS
- ~~`UsageEventRepository.cs`~~ — không build Mongo repo
- ~~`InternalUsageController.cs`~~ — không build API
- ~~`Features__EnableXxx` env var~~ — không build feature-flag framework
- ~~9-15 tests cho UsageTracker~~ — 1 smoke test đủ

---

## 11. Decisions chốt cho phase này (v2)

1. **Cut ngay, không freeze** — `git rm` 5 feature hôm nay; git history là rollback, không cần feature flag.
2. **LocalStorage > backend telemetry** cho 1 user — Domain/CQRS/Repo/API/admin SPA là overkill cho counter.
3. **BUILD/INVEST hours là KPI gốc**, không phải feature usage — đo bằng notebook, không build app.
4. **External reviewer là hard gate** — self-judged 5-question rate fail.
5. **Pain log thay icebox 30-day cooling** — capture sau 3 lần pain thật, không capture trước.
6. **No-ship week có positive substitute** — pre-committed 4 task, không phải "đừng ship" trống.
7. **Bỏ streak Daily Routine ngay** (Q4 fix) — không audit dopamine bằng dopamine.
8. **Cut window = rolling 90 ngày** không 30 — tránh false positive quarterly-legitimate.
9. **Phase A deadline cứng 1 buổi** — nếu vượt → STOP, plan này tự thành build project.

---

## 12. Out-of-scope

- Backend telemetry pipeline (Domain entity, CQRS, repo, API, admin SPA)
- Feature-flag framework (`Features__Enable*` env var)
- Multi-user, role-based feature flag
- Export data to external analytics (Mixpanel, PostHog)
- A/B testing framework
- Retroactive usage estimate từ git log / logs hiện có
- Conditional fetching ở backend (BE vẫn compute full payload, FE chỉ ẩn)

---

## 13. Future considerations (Q3+ 2026, có data thật)

- Nếu BUILD/INVEST log shift về 25/75 → keep current cadence
- Nếu vẫn >40% BUILD → escalate accountability (Beeminder $50/day forfeit hoặc paid coach)
- Returns attribution: trade nào dùng indicator nào → ROI per indicator (CHỈ làm khi đã có 100+ trade và localStorage tracker show clear top 3)
- Tool ROI calculation: Sharpe của portfolio vs hours spent on tool

---

## Appendix A — Indicator redundancy matrix

| Indicator A | Indicator B | Correlation | Khuyến nghị |
|---|---|---|---|
| RSI | MFI | High (~0.85) | Pick 1 — dùng MFI nếu volume quan trọng, RSI nếu đơn giản |
| RSI | Stochastic | High (~0.80) | Pick 1 — RSI phổ biến hơn |
| MACD | RSI | Medium (~0.65) | Keep cả 2 — MACD trend, RSI overbought |
| EMA crossover | ADX | Medium (~0.60) | Keep cả 2 — EMA cho hướng, ADX cho strength |
| Bollinger %B | Bollinger Band position | Identical | Same metric, drop 1 |
| Bollinger width | ATR | High (~0.75) | Pick 1 — ATR đơn giản hơn |
| OBV | Volume ratio cumulative | High (~0.85) | Pick 1 — Volume ratio dễ hiểu |

→ Có thể tinh gọn 12 → 7 không mất signal đáng kể.

---

## Appendix B — Top retail VN reference

Pattern indicator của các nhân vật public trong cộng đồng đầu tư VN:

| Nhân vật | Style | Indicators chính |
|---|---|---|
| Đinh Thế Hiển | Macro + BCTC | Volume + MA cơ bản |
| Phan Lê Thành Long | CFA, fundamental + technical | Price action + Volume + MA |
| Nguyễn Hồng Điệp | Trend follower | MA + Volume + RSI |
| Lê Hữu Hoàng (Stocktraders) | Trend + Volume profile | MA50/200 + RSI + Volume |
| Mai Trang Trader | Swing | Bollinger + RSI + Volume |

Pattern lặp: **Volume + 1-2 MA + 1 momentum**. Hết.

Quốc tế: Stan Weinstein (MA30 weekly + Volume), William O'Neil (Price + Volume + RS + Earnings), Mark Minervini (MA50/150/200 + Volume + RS), Linda Raschke (2-3 indicator max), Ed Seykota (MA + Price action).

→ Không một top trader public nào dùng >5 indicator để ra quyết định thật.

---

## Appendix C — Time allocation target (KPI gốc)

| Hoạt động | Mục tiêu % | Ví dụ 40h/tuần |
|---|---|---|
| Research mã/ngành/macro | 25-30% | 10-12h |
| Ra quyết định + execute trades | 5-10% | 2-4h |
| Review portfolio + journal | 10-15% | 4-6h |
| Đọc/học (sách, podcast, BCTC) | 15-20% | 6-8h |
| **INVEST total** | **55-75%** | **22-30h** |
| Maintain tool (bug fix) | 5-10% | 2-4h |
| Build feature mới (Tier S/A only) | 5-15% | 2-6h |
| **BUILD total** | **10-25%** | **4-10h** |
| Buffer | 10% | 4h |

Hiện tại estimate: BUILD 70%, INVEST 25%. Đo bằng `docs/personal/build-vs-invest-log.md` end-of-day, không bằng app.

---

## Appendix D — Pain log template (cho icebox)

```markdown
## Idea: [Tên feature]

### Pain occurrences (cần ≥3 trước khi ghi vào đây)
1. 2026-MM-DD: [mô tả lần pain 1]
2. 2026-MM-DD: [mô tả lần pain 2]
3. 2026-MM-DD: [mô tả lần pain 3]

### Gate (5 questions)
1. Pain test (≥3 lần/tuần — tham chiếu journal): Y/N
2. Workaround test (Excel/Notion/sticky-note làm được?): Y/N
3. DRY test (replace cũ hay chỉ thêm?): R/A
4. Maintenance test (6 tháng cost?): __ giờ
5. Joy vs Need test: Need / Joy

Score: __/5
External reviewer: [tên + ngày confirm]
Decision: SHIP / ICEBOX / DROP
Re-review date: 2026-MM-DD (sau 30 ngày nếu ICEBOX)
```

---

## Changelog plan

- **2026-05-05 v1**: Original plan — full Phase 1 telemetry pipeline (Domain entity + CQRS + repo + API + admin SPA + 9-15 tests)
- **2026-05-05 v2** (current): Pivoted after 3-agent critique converged on "plan v1 is the disease cosplaying as cure"
  - Phase 1 → ~50 LOC localStorage (-95% scope)
  - Cut deadwood from "after 30 days data" → today
  - KPI from feature usage → BUILD/INVEST hours (the actual goal)
  - Self-judged gate → +external reviewer
  - 30-day cut window → 90-day rolling (avoid false positive)
  - Icebox 30-day cooling → pain log (3 occurrences before capture)
  - No-ship week from negative lever → positive substitute list
  - Daily Routine streak: removed (was the dopamine pattern under audit)
