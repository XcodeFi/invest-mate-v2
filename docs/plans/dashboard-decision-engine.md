# Kế hoạch — Dashboard "Decision Engine" (xoay trục từ display → ép action)

> Tài liệu kế hoạch — biến Dashboard từ "Investor Cockpit hiển thị trạng thái" thành "Decision Engine ép user xử lý quyết định kỷ luật".
> Ngày tạo: 2026-05-04
> Cập nhật: 2026-05-04 (v1.1 — Hybrid sau review 2 sub-agent, adopt 3 mục từ layout V2 brainstorm)
> Trạng thái: **Đã chốt scope v1.1, chưa triển khai**
> Người chốt: tpham (owner solo-user app)

---

## 0. Bối cảnh & Lý do

### USP của app

> "Ngăn user phá kỷ luật đầu tư" — không phải "trình bày data".

### Hiện trạng (đã verify với code)

Dashboard hiện tại có **16 widget** xếp dọc trong [`dashboard.component.ts`](../../frontend/src/app/features/dashboard/dashboard.component.ts) (1653 LOC):

1. Header + AI button
2. Market index strip (VNINDEX, HNX...)
3. Risk Alert Banner (stop-loss/drawdown)
4. Discipline Score widget
5. Advisory widget (P0.5 — scenario triggers)
6. Capital Flow nudge
7. Watchlist widget (grid)
8. Daily Routine widget (grid)
9. Personal Finance / Net Worth
10. Timeframe switcher
11. Tổng tài sản (CAGR + TWR chips)
12. Compound Growth Tracker (CAGR projections + target)
13. Allocation + Top Positions
14. Mini Equity Curve
15. Quick Trade Widget (inline position sizer)
16. Pending Review trades
17. Quick Actions (4 link tĩnh)

→ Quá nhiều thứ cạnh tranh attention. User scan → không biết focus đâu → bỏ qua.

### Vấn đề cụ thể (đã verify)

| # | Vấn đề | Vị trí code | Mức độ |
|---|--------|-------------|--------|
| 1 | Decision triggers scattered ở 3 banner rời (Risk Alert + Advisory + Pending Review) | [dashboard.component.ts:93-160](../../frontend/src/app/features/dashboard/dashboard.component.ts#L93-L160), [:794-819](../../frontend/src/app/features/dashboard/dashboard.component.ts#L794-L819) | 🔴 Critical |
| 2 | Inline action không có — chỉ navigate qua trang khác | [dashboard.component.ts:114-119](../../frontend/src/app/features/dashboard/dashboard.component.ts#L114-L119), [:152-155](../../frontend/src/app/features/dashboard/dashboard.component.ts#L152-L155), [:807-815](../../frontend/src/app/features/dashboard/dashboard.component.ts#L807-L815) | 🔴 Critical |
| 3 | Reality Gap CAGR ẩn mặc định (`cagrTargetSet = false`) | [dashboard.component.ts:551](../../frontend/src/app/features/dashboard/dashboard.component.ts#L551), [:905](../../frontend/src/app/features/dashboard/dashboard.component.ts#L905) | 🟡 Medium |
| 4 | Discipline warning generic ("hãy review") không nói plan nào | [discipline-score-widget.component.ts:96-101](../../frontend/src/app/features/dashboard/widgets/discipline-score-widget.component.ts#L96-L101) | 🟡 Medium |
| 5 | CAGR check `!== 0` sai khi CAGR thật sự = 0% | [dashboard.component.ts:502](../../frontend/src/app/features/dashboard/dashboard.component.ts#L502) | 🟢 Low (edge case) |
| 6 | AI button positioning passive ("Bản tin") | [dashboard.component.ts:60-63](../../frontend/src/app/features/dashboard/dashboard.component.ts#L60-L63) | 🟡 Medium |
| 7 | Quick Actions tĩnh, không context-aware | [dashboard.component.ts:822-873](../../frontend/src/app/features/dashboard/dashboard.component.ts#L822-L873) | 🟢 Low |

---

## 1. Quyết định đã chốt (2026-05-04)

### V1 — Quyết định ban đầu

| # | Câu hỏi | Quyết định |
|---|---------|------------|
| Q1 | Decision Queue replace 3 widget cũ — gradual deprecate hay xóa hẳn? | ✅ **Xóa hẳn ngay**. Không giữ duplicate UI để tránh confusion. Risk Alert Banner + Advisory Widget + Pending Review section bị remove khỏi `dashboard.component.ts` cùng commit ship Decision Queue. |
| Q2 | `ExecuteSell` quantity = full position hay từ TradePlan? | ✅ **Quantity từ TradePlan**. Multi-lot: dùng tổng quantity của các lot đã `Executed` (= position đang nắm theo plan). Plan single-lot dùng `PlannedQuantity`. |
| Q3 | AI rebrand — giữ "Bản tin" làm option phụ hay thay hẳn? | ✅ **Thay hẳn**. Đổi cả label + use-case + prompt content. Use-case `daily-briefing` deprecate (giữ trong service nhưng không expose trên Dashboard nữa — có thể dùng cho route/page khác sau). |

### V1.1 — Hybrid sau review 2 sub-agent (Product/UX + Architect)

Layout V2 brainstorm có 8 đề xuất. Sau review độc lập bởi 2 sub-agent, **adopt 3 / bác 5**:

| # | Đề xuất V2 | Quyết định | Lý do |
|---|---|---|---|
| Q4 | Tách `<networth-summary />` thành widget riêng (vị trí #2 sau Decision Queue) | ✅ **Adopt** — gộp vào P1 | Backend đã sẵn `/personal-finance/summary`, chỉ là re-arrangement UI. 0 risk từ 2 agent. |
| Q5 | Empty state Decision Queue "kể câu chuyện thắng" (`✅ Hôm nay đang kỷ luật + streak X ngày`) | ✅ **Adopt** — gộp vào P3 | Fix Critical risk #4 từ Product/UX agent: 0-alert day = thắng, không phải app rỗng. Cost rất thấp. |
| Q6 | Remove khỏi Home: Market Index strip + Equity Curve + Quick Actions (**KHÔNG remove Watchlist**) | ✅ **Adopt** — gộp vào P5 (mới) | 3 widget có route riêng (`/market-data`, `/analytics`, header menu). Watchlist GIỮ vì agent UX flag mạnh: phá pre-trade routine (kỷ luật entry). |
| Q7 | Discipline format violation-based (`3/5 lệnh không theo plan`, `2.3x kế hoạch`) | ❌ **Bác** | Cần entity migration `PlannedHoldDays` + 90% legacy null. Solo user 1-3 trade/tháng → metric noisy (1 sai = 20%). Baseline self-reference skewed. Effort +L. |
| Q8 | AI mini-inline 1 câu thay panel chatbot | ❌ **Bác** | `AiAssistantService` toàn streaming `IAsyncEnumerable` → cần kiến trúc mới (cache + cron broadcast). Cost AI auto-generate × 6 lần/ngày. Stale khi VN-Index lao dốc giữa cron. Effort +M. |
| Q9 | Action `[CHỐT 1 PHẦN]` (Partial Sell) | ⏸ **Defer V2** | Duplicate ExitTarget (TP1/TP2 đã là partial sell). `ExitTargets` chưa expose ra frontend DTO. Refactor `ResolveDecisionCommand` enum → discriminated union. |
| Q10 | Rename `DashboardComponent` → `HomeComponent` | ❌ **Bác** | 25+ touchpoints (`/dashboard` xuất hiện trong 20 file: routes, header, auth callback, manifest, user guides). Effort +S, value ≈ 0. |
| Q11 | Commands `OverrideDecision(reason)` + `UpdateThesis` | ❌ **Bác** | `OverrideDecision` ≡ `HoldWithJournal` về data layer. `UpdateThesis` đã có endpoint `PUT /api/v1/trade-plans/{id}` — duplicate. `JournalEntryType.Decision` chưa tồn tại trong enum domain (hiện chỉ `Observation/PreTrade/DuringTrade/PostTrade/Review`). |

→ Effort delta hybrid: **+1.5 ngày** so với plan V1 (~12 ngày). Tổng = **~13.5 ngày (~2.5 tuần)**.

---

## 2. Scope — 5 surgical changes (v1.1 Hybrid)

Không phá kiến trúc. Mỗi change có TDD rõ ràng.

| # | Change | Effort | Impact | Dep |
|---|--------|--------|--------|-----|
| P1 | Reality Gap CAGR + **tách `<networth-summary />` widget riêng** | S — FE only, ~150 LOC | Medium | None |
| P2 | AI rebrand "Bản tin" → "Phản biện danh mục" | S — 1 prompt + 2 string change | Medium | None |
| P3 | Decision Queue + **empty state positive** (gộp 3 alert sources thành 1 widget) | M — 1 query handler + 1 widget | **High** | None |
| P4 | Inline action buttons (BÁN theo plan / GIỮ + ghi lý do) | M — 1 command handler + UI | **High** | P3 |
| P5 | **Remove 3 widget khỏi Home** (Market Index + Equity Curve + Quick Actions). **GIỮ Watchlist** | XS — FE only, ~80 LOC xóa | Low | None |

**Thứ tự ship:** P1 → P2 → P3 → P4 → P5. P1+P2 ship được trong 1.5 ngày, P3+P4 cần 4-5 ngày, P5 ship cuối với P4 (cùng PR).

---

## 3. P1 — Reality Gap CAGR + NetWorth widget tách riêng

### Mục tiêu

1. User mở Dashboard lần đầu → thấy ngay "đang lệch X% so với mục tiêu CAGR" mà không cần action gì.
2. **(v1.1)** Tách `<networth-summary />` thành widget độc lập ngắn gọn ở vị trí #2 (sau Decision Queue), thay vì nhúng trong block Compound Growth Tracker dài.

### Layout sau P1

```
[ Decision Queue (P3) ]
[ 💎 NetWorth Summary widget — block ngắn 3 dòng ]
   Tổng TS: 535M (+1.1%)
   🔴 Lệch -4.2% so với mục tiêu CAGR
   [Xem chi tiết →]
[ Compound Growth Tracker — giữ ở vị trí cũ giữa page (full version với projections + target editor) ]
```

→ NetWorth widget ngắn ở top giúp user thấy gap ngay. Compound Growth Tracker đầy đủ vẫn còn ở giữa page cho ai muốn deep-dive.

### TDD — Frontend (chỉ FE, không cần backend)

**File spec mới:** `frontend/src/app/features/dashboard/dashboard.component.spec.ts` (chưa có)
**File spec mới:** `frontend/src/app/features/dashboard/widgets/networth-summary.component.spec.ts` (mới, cho widget tách ra)

#### Test mới (Red): NetWorth widget render với gap label

```typescript
it('should render compact networth widget with reality gap', () => {
  component.netWorthSummary = mockNetWorth({ totalAssets: 535_000_000, dailyChangePercent: 1.1 });
  component.cagrValue = 11; // gap = 4% so với target 15
  fixture.detectChanges();

  const widget = fixture.debugElement.query(By.css('app-networth-summary'));
  expect(widget).toBeTruthy();
  expect(widget.nativeElement.textContent).toContain('535');
  expect(widget.nativeElement.textContent).toContain('Lệch');
});
```

#### Test 1 (Red): default state hiển thị progress bar

```typescript
it('should show CAGR target progress bar by default with target=15%', () => {
  // Given: fresh component, no user interaction
  component.cagrValue = 8;
  fixture.detectChanges();

  // Then: progress bar phải visible
  const bar = fixture.debugElement.query(By.css('[data-test="cagr-progress-bar"]'));
  expect(bar).toBeTruthy();
  expect(component.cagrTargetSet).toBeTrue();
  expect(component.cagrTarget).toBe(15);
});
```

#### Test 2 (Red): hiển thị label đỏ khi gap > 50%

```typescript
it('should display red gap label when behind target by > 50%', () => {
  component.cagrValue = 5; // 5/15 = 33% of target → gap 67%
  fixture.detectChanges();

  const label = fixture.debugElement.query(By.css('[data-test="cagr-gap-label"]'));
  expect(label.nativeElement.textContent).toContain('Lệch');
  expect(label.nativeElement.classList).toContain('text-red-600');
});
```

#### Test 3 (Red): fix CAGR === 0 edge case

```typescript
it('should show TWR branch when cagrValue=0 but cagrTwrValue is non-null', () => {
  component.cagrValue = 0;
  component.cagrTwrValue = 2.5;
  component.cagrDaysSpanned = 15;
  fixture.detectChanges();

  // Branch 2 should activate (raw TWR display), not Branch 3 ("Chưa đủ snapshot")
  const twrLabel = fixture.debugElement.query(By.css('[data-test="cagr-short-window"]'));
  expect(twrLabel).toBeTruthy();
});
```

### Implementation (Green)

**[dashboard.component.ts:905](../../frontend/src/app/features/dashboard/dashboard.component.ts#L905):**
```typescript
cagrTargetSet = true;  // was: false
```

**[dashboard.component.ts:551](../../frontend/src/app/features/dashboard/dashboard.component.ts#L551):**
```html
<!-- Was: *ngIf="cagrTargetSet && cagrValue !== 0" -->
<div *ngIf="cagrValue !== 0" class="mt-2">
```

**[dashboard.component.ts:521,539](../../frontend/src/app/features/dashboard/dashboard.component.ts#L521):**
```html
<!-- Branch 2 condition - was: cagrValue === 0 && cagrTwrValue !== null && ... -->
<ng-container *ngIf="!cagrIsStable && cagrTwrValue !== null && cagrDaysSpanned >= 1 && cagrDaysSpanned < 30">
```

**Thêm gap label sau progress bar:**
```html
<div *ngIf="cagrTargetSet && cagrValue !== 0 && getTargetProgress() < 50"
     data-test="cagr-gap-label"
     class="text-xs font-medium text-red-600 mt-1">
  ⚠️ Lệch {{ (100 - getTargetProgress()).toFixed(0) }}% so với mục tiêu
</div>
```

### Implementation NetWorth widget tách (v1.1)

**File mới:** `frontend/src/app/features/dashboard/widgets/networth-summary.component.ts`

```typescript
@Component({
  selector: 'app-networth-summary',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe],
  template: `
    <a *ngIf="summary?.hasProfile" routerLink="/personal-finance"
       class="block bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6 hover:border-blue-300 transition-colors">
      <div class="flex items-center justify-between mb-1">
        <span class="text-xs font-semibold text-gray-700">💎 Tổng tài sản</span>
        <span class="text-xs text-blue-600">Xem chi tiết →</span>
      </div>
      <div class="flex items-baseline justify-between mb-1">
        <span class="text-2xl font-bold"
              [class.text-emerald-700]="summary!.netWorth >= 0"
              [class.text-red-700]="summary!.netWorth < 0">
          {{ summary!.netWorth | vndCurrency }}
        </span>
        <span *ngIf="dailyChangePercent !== null" class="text-sm font-medium"
              [class.text-emerald-600]="dailyChangePercent! >= 0"
              [class.text-red-600]="dailyChangePercent! < 0">
          {{ dailyChangePercent! >= 0 ? '+' : '' }}{{ dailyChangePercent!.toFixed(2) }}%
        </span>
      </div>
      <div *ngIf="cagrGap !== null && cagrGap < 0" class="text-xs text-red-600 font-medium">
        🔴 Lệch {{ Math.abs(cagrGap).toFixed(1) }}% so với mục tiêu CAGR {{ cagrTarget }}%
      </div>
    </a>
  `
})
export class NetWorthSummaryComponent {
  @Input() summary: NetWorthSummaryDto | null = null;
  @Input() dailyChangePercent: number | null = null;
  @Input() cagrValue: number = 0;
  @Input() cagrTarget: number = 15;
  Math = Math;

  get cagrGap(): number | null {
    return this.cagrValue === 0 ? null : this.cagrValue - this.cagrTarget;
  }
}
```

**Mount vào dashboard.component.ts** ngay sau `<app-decision-queue>` (P3):
```html
<app-networth-summary
  [summary]="netWorthSummary"
  [cagrValue]="cagrValue"
  [cagrTarget]="cagrTarget">
</app-networth-summary>
```

**Compound Growth Tracker** giữ nguyên ở vị trí cũ giữa page (line 465 trong dashboard hiện tại) — full version với projections + target editor.

### Files thay đổi (v1.1)

- `frontend/src/app/features/dashboard/dashboard.component.ts` (~15 LOC: cagrTargetSet=true + ngIf fix + mount widget)
- `frontend/src/app/features/dashboard/dashboard.component.spec.ts` (mới, ~100 LOC)
- `frontend/src/app/features/dashboard/widgets/networth-summary.component.ts` (mới, ~80 LOC)
- `frontend/src/app/features/dashboard/widgets/networth-summary.component.spec.ts` (mới, ~60 LOC)

---

## 4. P2 — AI rebrand "Bản tin" → "Phản biện danh mục"

### Mục tiêu

Đổi vai AI từ news-reader (passive) → coach phản biện (adversarial).

### TDD — Backend trước

**File spec mới:** `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServicePortfolioCritiqueTests.cs`

#### Test 1 (Red): use-case mới được register

```csharp
[Fact]
public async Task BuildPrompt_PortfolioCritique_IncludesDisciplineScore()
{
    // Given: user có discipline score 45/100 + 3 vị thế lỗ
    var ctx = BuildContextWithLowDiscipline();

    // When
    var prompt = await _service.BuildPromptAsync("portfolio-critique", ctx);

    // Then
    prompt.Should().Contain("Kỷ luật:");
    prompt.Should().Contain("45");
    prompt.Should().Contain("phản biện");
    prompt.Should().NotContain("bản tin");
}
```

#### Test 2 (Red): prompt frame adversarial

```csharp
[Fact]
public void BuildPrompt_PortfolioCritique_FramesAdversarial_NotSupportive()
{
    var prompt = _service.BuildSystemPrompt("portfolio-critique");

    // Phải có chỉ thị "tìm điểm sai/yếu", không có "khen"
    prompt.Should().Contain("phản biện");
    prompt.Should().Contain("điểm sai");
    prompt.Should().NotContain("động viên");
    prompt.Should().NotContainEquivalentOf("encourage");
}
```

#### Test 3 (Red): output format 3 điểm ngắn gọn

```csharp
[Fact]
public void BuildPrompt_PortfolioCritique_RequiresExactlyThreePoints()
{
    var prompt = _service.BuildSystemPrompt("portfolio-critique");

    prompt.Should().Contain("3 điểm");
    prompt.Should().Contain("1 câu");
}
```

### Implementation (Green)

**File:** `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs`

Thêm use-case `portfolio-critique`:

```csharp
case "portfolio-critique":
    return $@"Bạn là HLV phản biện đầu tư cho user solo. Vai trò: chỉ ra điểm SAI / YẾU / LỆCH KỶ LUẬT trong danh mục, KHÔNG khen, KHÔNG động viên.

Quy tắc:
- Tìm chính xác 3 điểm.
- Mỗi điểm 1 câu (≤ 25 từ).
- Ưu tiên thứ tự: vi phạm SL > thesis hết hạn chưa review > position concentration > drawdown bất thường.
- Nếu kỷ luật < 60 → câu đầu tiên PHẢI nói rõ điểm yếu cụ thể (sub-metric nào tệ nhất).
- Không dùng từ chung chung như 'cân nhắc', 'có thể'. Dùng động từ mệnh lệnh: 'cắt', 'review', 'giảm'.

Context user:
{contextJson}

Output: 3 dòng. Format mỗi dòng: '{{số}}. {{câu phản biện}}'";
```

### TDD — Frontend

**File spec:** `frontend/src/app/features/dashboard/dashboard.component.spec.ts` (extend P1 spec)

#### Test 4 (Red): button label + use-case

```typescript
it('should render AI button with critique label and pass critique use-case to panel', () => {
  fixture.detectChanges();

  const aiButton = fixture.debugElement.query(By.css('[data-test="ai-button"]'));
  expect(aiButton.nativeElement.textContent).toContain('phản biện');

  // Trigger panel open
  aiButton.nativeElement.click();
  fixture.detectChanges();

  const panel = fixture.debugElement.query(By.directive(AiChatPanelComponent));
  expect(panel.componentInstance.useCase).toBe('portfolio-critique');
});
```

### Implementation (Green)

**[dashboard.component.ts:60-63](../../frontend/src/app/features/dashboard/dashboard.component.ts#L60-L63):**
```html
<button (click)="showAiPanel = true"
  data-test="ai-button"
  class="bg-purple-600 hover:bg-purple-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200 flex items-center gap-1">
  🥊 AI phản biện danh mục
</button>
```

**[dashboard.component.ts:878-883](../../frontend/src/app/features/dashboard/dashboard.component.ts#L878-L883):**
```html
<app-ai-chat-panel
  [(isOpen)]="showAiPanel"
  title="Phản biện danh mục"
  useCase="portfolio-critique"
  [contextData]="emptyContext">
</app-ai-chat-panel>
```

### Files thay đổi

- `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` (~30 LOC mới)
- `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServicePortfolioCritiqueTests.cs` (mới, ~100 LOC)
- `frontend/src/app/features/dashboard/dashboard.component.ts` (~5 LOC)
- `frontend/src/app/features/dashboard/dashboard.component.spec.ts` (~30 LOC)

---

## 5. P3 — Decision Queue + Empty State Positive (gộp 3 nguồn alert thành 1)

### Mục tiêu

1. 3 widget rời (Risk Alert + Advisory + Pending Review) → 1 "Việc cần xử lý hôm nay" duy nhất ở top, sort theo severity.
2. **(v1.1)** Khi 0 alert → hiển thị empty state **positive**: `✅ Hôm nay đang kỷ luật + streak X ngày` thay vì widget biến mất hoàn toàn (tránh app trở nên rỗng — fix Critical risk #4 từ Product/UX agent).

### Backend — Domain & Application

**File mới:** `src/InvestmentApp.Application/Decisions/DTOs/DecisionItemDto.cs`

```csharp
public enum DecisionType { StopLossHit, ScenarioTrigger, ThesisReviewDue, ConcentrationRisk }
public enum DecisionSeverity { Critical, Warning, Info }

public record DecisionItemDto(
    string Id,                       // composite: "{type}:{sourceId}"
    DecisionType Type,
    string Symbol,
    string PortfolioId,
    string PortfolioName,
    string Headline,                 // "FPT chạm SL 89.4 (mua 90.2, kế hoạch 89.5)"
    string ThesisOrReason,
    DecisionSeverity Severity,
    decimal? CurrentPrice,
    decimal? PlannedExitPrice,
    string? TradePlanId,             // null nếu source không có plan
    DateTime DueAt,
    DateTime CreatedAt
);

public record DecisionQueueDto(IReadOnlyList<DecisionItemDto> Items, int TotalCount);
```

**File mới:** `src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs`

Aggregate logic:
1. Risk alerts từ `IRiskService.GetStopLossAlertsAsync` → map thành `DecisionType.StopLossHit` (severity Critical)
2. Scenario advisories từ `ITradePlanRepository.GetActiveAdvisoriesAsync` → `DecisionType.ScenarioTrigger` (severity = Critical nếu node = exit, Warning nếu là partial)
3. Pending thesis reviews từ existing `GetPendingThesisReviewsQuery` → `DecisionType.ThesisReviewDue` (severity Warning nếu < 3 ngày, Critical nếu overdue ≥ 3)
4. Dedupe: nếu cùng `(symbol, portfolioId)` xuất hiện cả StopLossHit + ScenarioTrigger → giữ Critical (StopLossHit), drop ScenarioTrigger
5. Sort: `(Severity desc, DueAt asc)`

### TDD — Backend

**File spec mới:** `tests/InvestmentApp.Application.Tests/Decisions/GetDecisionQueueQueryHandlerTests.cs`

#### Test 1 (Red): aggregate 3 sources

```csharp
[Fact]
public async Task Handle_AggregatesAllThreeSources()
{
    // Given: 1 stop-loss + 2 advisories + 1 thesis review pending
    SetupRiskAlerts(count: 1);
    SetupAdvisories(count: 2);
    SetupPendingReviews(count: 1);

    // When
    var result = await _handler.Handle(new GetDecisionQueueQuery(_userId), default);

    // Then
    result.Items.Should().HaveCount(4);
    result.Items.Should().Contain(i => i.Type == DecisionType.StopLossHit);
    result.Items.Should().Contain(i => i.Type == DecisionType.ScenarioTrigger);
    result.Items.Should().Contain(i => i.Type == DecisionType.ThesisReviewDue);
}
```

#### Test 2 (Red): dedupe symbol trùng

```csharp
[Fact]
public async Task Handle_DeduplicatesSameSymbolKeepsCritical()
{
    SetupRiskAlerts(symbol: "FPT", count: 1);
    SetupAdvisories(symbol: "FPT", count: 1);

    var result = await _handler.Handle(new GetDecisionQueueQuery(_userId), default);

    result.Items.Should().HaveCount(1);
    result.Items[0].Type.Should().Be(DecisionType.StopLossHit);
}
```

#### Test 3 (Red): sort severity desc

```csharp
[Fact]
public async Task Handle_SortsBySeverityCriticalFirst()
{
    SetupPendingReviews(count: 1);  // Warning
    SetupRiskAlerts(count: 1);       // Critical

    var result = await _handler.Handle(new GetDecisionQueueQuery(_userId), default);

    result.Items[0].Severity.Should().Be(DecisionSeverity.Critical);
    result.Items[1].Severity.Should().Be(DecisionSeverity.Warning);
}
```

#### Test 4 (Red): user isolation

```csharp
[Fact]
public async Task Handle_OnlyReturnsItemsForCallerUserId()
{
    SetupRiskAlertsForOtherUser();

    var result = await _handler.Handle(new GetDecisionQueueQuery(_userId), default);

    result.Items.Should().BeEmpty();
}
```

### Backend — API

**File mới:** `src/InvestmentApp.Api/Controllers/DecisionsController.cs`

```csharp
[ApiController]
[Route("api/v1/decisions")]
[Authorize]
public class DecisionsController : ControllerBase
{
    [HttpGet("queue")]
    public async Task<DecisionQueueDto> GetQueue(CancellationToken ct)
        => await _mediator.Send(new GetDecisionQueueQuery(GetUserId()), ct);
}
```

### Frontend — Decision Queue Widget

**File mới:** `frontend/src/app/features/dashboard/widgets/decision-queue.component.ts`

```typescript
@Component({
  selector: 'app-decision-queue',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe],
  template: `
    <!-- Empty state — kể câu chuyện thắng (v1.1) -->
    <div *ngIf="!loading && items.length === 0 && streakDays !== null"
         data-test="decision-queue-empty"
         class="bg-emerald-50 rounded-xl border-2 border-emerald-200 shadow-sm p-4 mb-6">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-sm font-bold text-emerald-900 flex items-center gap-2">
            ✅ Hôm nay đang kỷ luật
          </h2>
          <p class="text-xs text-emerald-700 mt-1">
            Không có SL bị chạm, không có thesis quá hạn review.
          </p>
        </div>
        <div *ngIf="streakDays > 0" class="text-right">
          <div class="text-2xl">🔥</div>
          <div class="text-xs font-semibold text-emerald-700">{{ streakDays }} ngày</div>
        </div>
      </div>
      <a routerLink="/discipline/report" class="text-xs text-emerald-700 hover:text-emerald-900 font-medium mt-2 inline-block">
        Xem báo cáo kỷ luật →
      </a>
    </div>

    <!-- Active queue — có alert -->
    <div *ngIf="items.length > 0" class="bg-white rounded-xl border-2 border-red-200 shadow-md mb-6">
      <div class="px-4 py-3 bg-red-50 border-b border-red-200 rounded-t-xl flex items-center justify-between">
        <h2 class="text-base font-bold text-red-900 flex items-center gap-2">
          🚨 Việc cần xử lý hôm nay
          <span class="bg-red-600 text-white text-xs font-bold px-2 py-0.5 rounded-full">{{ items.length }}</span>
        </h2>
      </div>
      <div class="divide-y divide-gray-100">
        <div *ngFor="let item of items; trackBy: trackById"
             class="p-4 hover:bg-gray-50 transition-colors"
             [class.bg-red-50]="item.severity === 'Critical'">
          <!-- Headline -->
          <div class="flex items-start justify-between gap-3 mb-2">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-1">
                <span class="font-bold text-gray-900">{{ item.symbol }}</span>
                <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                      [ngClass]="severityBadgeClass(item.severity)">
                  {{ severityLabel(item.severity) }}
                </span>
                <span class="text-xs text-gray-500">{{ typeLabel(item.type) }}</span>
              </div>
              <div class="text-sm text-gray-800 mb-1">{{ item.headline }}</div>
              <div *ngIf="item.thesisOrReason" class="text-xs text-gray-500 italic">
                Lý do gốc: {{ item.thesisOrReason }}
              </div>
            </div>
          </div>
          <!-- Action buttons (P4 sẽ wire lên) -->
          <ng-content select="[itemActions]" *ngIf="false"></ng-content>
          <div class="flex gap-2 mt-2">
            <ng-container *ngTemplateOutlet="actionButtons; context: {item: item}"></ng-container>
          </div>
        </div>
      </div>
      <div *ngIf="items.length > maxVisible" class="px-4 py-2 text-xs text-center text-gray-500 border-t">
        Hiển thị {{ maxVisible }}/{{ items.length }} · <a [routerLink]="['/decisions']" class="text-blue-600">Xem tất cả →</a>
      </div>
    </div>
    <ng-template #actionButtons let-item="item">
      <!-- Slot — P4 sẽ thay bằng inline action buttons -->
      <a [routerLink]="getActionRoute(item)" [queryParams]="getActionParams(item)"
         class="px-3 py-1.5 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded-md font-medium">
        Xử lý →
      </a>
    </ng-template>
  `
})
export class DecisionQueueComponent implements OnInit {
  private decisionService = inject(DecisionService);
  items: DecisionItemDto[] = [];
  maxVisible = 5;

  ngOnInit(): void {
    this.load();
    this.loadStreak();   // v1.1 — empty state positive
  }

  load(): void {
    this.decisionService.getQueue().subscribe({
      next: (q) => this.items = q.items.slice(0, this.maxVisible),
      error: () => this.items = []
    });
  }

  // v1.1 — streak cho empty state
  streakDays: number | null = null;
  private loadStreak(): void {
    this.disciplineService.getStreak().subscribe({
      next: (s) => this.streakDays = s.daysWithoutViolation,
      error: () => this.streakDays = 0
    });
  }

  // ... helper methods
}
```

**Streak endpoint (v1.1):** `GET /api/v1/me/discipline-score/streak` → `{ daysWithoutViolation: number }`. Tính từ snapshot lịch sử của discipline score: số ngày liên tiếp gần nhất không có SL hit + không có thesis review overdue.

### TDD — Frontend Decision Queue

**File spec mới:** `frontend/src/app/features/dashboard/widgets/decision-queue.component.spec.ts`

#### Test 1 (Red): render N items

```typescript
it('should render items grouped by severity with critical at top', () => {
  service.getQueue.and.returnValue(of({
    items: [
      { id: '1', severity: 'Warning', type: 'ThesisReviewDue', symbol: 'VNM', ... },
      { id: '2', severity: 'Critical', type: 'StopLossHit', symbol: 'FPT', ... }
    ],
    totalCount: 2
  }));
  fixture.detectChanges();

  const cards = fixture.debugElement.queryAll(By.css('[data-test="decision-item"]'));
  expect(cards.length).toBe(2);
  expect(cards[0].nativeElement.textContent).toContain('FPT'); // Critical first
});
```

#### Test 2 (Red): empty state positive với streak (v1.1)

```typescript
it('should render empty state with streak when 0 alerts', () => {
  service.getQueue.and.returnValue(of({ items: [], totalCount: 0 }));
  disciplineService.getStreak.and.returnValue(of({ daysWithoutViolation: 7 }));
  fixture.detectChanges();

  const empty = fixture.debugElement.query(By.css('[data-test="decision-queue-empty"]'));
  expect(empty).toBeTruthy();
  expect(empty.nativeElement.textContent).toContain('Hôm nay đang kỷ luật');
  expect(empty.nativeElement.textContent).toContain('7 ngày');
});

it('should NOT show streak badge when streakDays = 0', () => {
  service.getQueue.and.returnValue(of({ items: [], totalCount: 0 }));
  disciplineService.getStreak.and.returnValue(of({ daysWithoutViolation: 0 }));
  fixture.detectChanges();

  const badge = fixture.debugElement.query(By.css('[data-test="streak-badge"]'));
  expect(badge).toBeFalsy();
});
```

#### Test 3 (Red): max 5 items + "xem tất cả"

```typescript
it('should cap at 5 visible items and show overflow link when > 5', () => {
  const items = Array.from({length: 8}, (_, i) => mockItem({id: `${i}`}));
  service.getQueue.and.returnValue(of({ items, totalCount: 8 }));
  fixture.detectChanges();

  const cards = fixture.debugElement.queryAll(By.css('[data-test="decision-item"]'));
  expect(cards.length).toBe(5);

  const overflow = fixture.debugElement.query(By.css('[data-test="overflow-link"]'));
  expect(overflow.nativeElement.textContent).toContain('8');
});
```

### Frontend — XÓA HẲN 3 widget cũ

Theo Q1 đã chốt — **xóa hoàn toàn** trong cùng commit:

| Widget | Vị trí xóa |
|--------|-----------|
| Risk Alert Banner | [dashboard.component.ts:93-121](../../frontend/src/app/features/dashboard/dashboard.component.ts#L93-L121) (29 dòng template) + property `riskAlerts`, `bannerDismissed`, `hasDangerAlert` getter, logic load risk alerts |
| Advisory Widget | [dashboard.component.ts:128-160](../../frontend/src/app/features/dashboard/dashboard.component.ts#L128-L160) (33 dòng template) + property `advisories`, method `loadAdvisories()` |
| Pending Review section | [dashboard.component.ts:794-819](../../frontend/src/app/features/dashboard/dashboard.component.ts#L794-L819) (26 dòng template) + property `pendingReviewTrades`, method `loadPendingReview()` |

**Add:** `<app-decision-queue></app-decision-queue>` ở vị trí ngay sau header (trên cả Discipline widget).

### Files thay đổi

**Backend:**
- `src/InvestmentApp.Application/Decisions/DTOs/DecisionItemDto.cs` (mới, ~50 LOC)
- `src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs` (mới, ~150 LOC)
- `src/InvestmentApp.Application/Discipline/Queries/GetDisciplineStreakQuery.cs` (mới v1.1, ~80 LOC)
- `src/InvestmentApp.Api/Controllers/DecisionsController.cs` (mới, ~40 LOC)
- `src/InvestmentApp.Api/Controllers/DisciplineController.cs` (thêm endpoint `/streak` v1.1, ~15 LOC)
- `tests/InvestmentApp.Application.Tests/Decisions/GetDecisionQueueQueryHandlerTests.cs` (mới, ~250 LOC)
- `tests/InvestmentApp.Application.Tests/Discipline/GetDisciplineStreakQueryHandlerTests.cs` (mới v1.1, ~100 LOC)

**Frontend:**
- `frontend/src/app/core/services/decision.service.ts` (mới, ~40 LOC)
- `frontend/src/app/core/services/discipline.service.ts` (thêm `getStreak()` v1.1, ~15 LOC)
- `frontend/src/app/features/dashboard/widgets/decision-queue.component.ts` (mới, ~250 LOC, bao gồm empty state)
- `frontend/src/app/features/dashboard/widgets/decision-queue.component.spec.ts` (mới, ~180 LOC)
- `frontend/src/app/features/dashboard/dashboard.component.ts` (xóa ~90 dòng template + ~30 dòng TS, add 1 dòng widget)

---

## 6. P4 — Inline action buttons (BÁN theo plan / GIỮ + ghi lý do)

### Mục tiêu

Mỗi DecisionItem có 2 button resolve in-place. Không navigate.

### Backend — Command

**File mới:** `src/InvestmentApp.Application/Decisions/Commands/ResolveDecision/ResolveDecisionCommand.cs`

```csharp
public enum DecisionAction { ExecuteSell, HoldWithJournal }

public record ResolveDecisionCommand(
    string DecisionId,           // composite "{type}:{sourceId}"
    DecisionAction Action,
    string? Note,                 // required khi HoldWithJournal
    string UserId
) : IRequest<ResolveDecisionResult>;

public record ResolveDecisionResult(string ResultId, string Message);
```

**Handler logic:**

#### `ExecuteSell` flow

1. Parse `DecisionId` → resolve `tradePlanId`
2. Load `TradePlan` → tính quantity:
   - Single-lot: `plan.PlannedQuantity`
   - Multi-lot: `plan.Lots.Where(l => l.Status == LotStatus.Executed).Sum(l => l.Quantity)` (= position thực sự đang nắm theo plan)
3. Lấy current price từ `IStockPriceService.GetLatestPriceAsync(symbol)`
4. Tạo `Trade` với `Type = Sell`, quantity tính ở step 2, `EntryPrice = currentPrice`, `LinkedTradePlanId = tradePlanId`
5. Persist trade qua `ITradeRepository.AddAsync`
6. Trigger snapshot refresh

#### `HoldWithJournal` flow

1. Parse `DecisionId` → resolve `tradePlanId` + `symbol`
2. Validate `Note` non-empty (≥ 20 chars để force user suy nghĩ)
3. Tạo `JournalEntry` với:
   - `Type = JournalEntryType.Decision`
   - `Symbol = symbol`
   - `LinkedTradePlanId = tradePlanId`
   - `Body = note`
   - `Tags = [ "decision-hold", $"trigger:{decisionType}" ]`
4. Persist qua `IJournalEntryRepository.AddAsync`

### TDD — Backend

**File spec mới:** `tests/InvestmentApp.Application.Tests/Decisions/ResolveDecisionCommandHandlerTests.cs`

#### Test 1 (Red): ExecuteSell single-lot dùng PlannedQuantity

```csharp
[Fact]
public async Task Handle_ExecuteSell_SingleLot_UsesPlannedQuantity()
{
    var plan = TestData.SingleLotPlan(plannedQuantity: 100);
    SetupPlan(plan);
    SetupCurrentPrice("FPT", 89.5m);

    var cmd = new ResolveDecisionCommand("StopLossHit:plan1", DecisionAction.ExecuteSell, null, _userId);
    await _handler.Handle(cmd, default);

    _tradeRepoMock.Verify(r => r.AddAsync(
        It.Is<Trade>(t => t.Quantity == 100 && t.Type == TradeType.Sell && t.EntryPrice == 89.5m),
        It.IsAny<CancellationToken>()
    ), Times.Once);
}
```

#### Test 2 (Red): ExecuteSell multi-lot chỉ sum Executed lots

```csharp
[Fact]
public async Task Handle_ExecuteSell_MultiLot_SumsOnlyExecutedLots()
{
    var plan = TestData.MultiLotPlan(lots: new[] {
        (qty: 50, status: LotStatus.Executed),
        (qty: 30, status: LotStatus.Executed),
        (qty: 20, status: LotStatus.Pending)  // không tính
    });
    SetupPlan(plan);

    var cmd = new ResolveDecisionCommand("ScenarioTrigger:plan2", DecisionAction.ExecuteSell, null, _userId);
    await _handler.Handle(cmd, default);

    _tradeRepoMock.Verify(r => r.AddAsync(
        It.Is<Trade>(t => t.Quantity == 80),  // 50 + 30
        It.IsAny<CancellationToken>()
    ), Times.Once);
}
```

#### Test 3 (Red): HoldWithJournal yêu cầu note ≥ 20 chars

```csharp
[Fact]
public async Task Handle_HoldWithJournal_RejectsShortNote()
{
    var cmd = new ResolveDecisionCommand("StopLossHit:plan1", DecisionAction.HoldWithJournal, "ngắn", _userId);

    var act = () => _handler.Handle(cmd, default);

    await act.Should().ThrowAsync<ValidationException>()
        .WithMessage("*ít nhất 20 ký tự*");
}
```

#### Test 4 (Red): HoldWithJournal link tới TradePlan

```csharp
[Fact]
public async Task Handle_HoldWithJournal_LinksJournalToOriginatingPlan()
{
    var cmd = new ResolveDecisionCommand("ThesisReviewDue:plan3", DecisionAction.HoldWithJournal,
        "Thesis vẫn còn nguyên, earnings tiếp theo confirm trong 2 tuần nữa", _userId);

    await _handler.Handle(cmd, default);

    _journalRepoMock.Verify(r => r.AddAsync(
        It.Is<JournalEntry>(j => j.LinkedTradePlanId == "plan3" && j.Type == JournalEntryType.Decision),
        It.IsAny<CancellationToken>()
    ), Times.Once);
}
```

#### Test 5 (Red): user isolation

```csharp
[Fact]
public async Task Handle_RejectsResolveForOtherUserPlan()
{
    SetupPlan(TestData.PlanForUser("other-user"));

    var cmd = new ResolveDecisionCommand("StopLossHit:plan1", DecisionAction.ExecuteSell, null, _userId);

    var act = () => _handler.Handle(cmd, default);
    await act.Should().ThrowAsync<UnauthorizedAccessException>();
}
```

### Backend — API endpoint

`src/InvestmentApp.Api/Controllers/DecisionsController.cs` thêm:

```csharp
[HttpPost("{id}/resolve")]
public async Task<ResolveDecisionResult> Resolve(string id, [FromBody] ResolveDecisionRequest req, CancellationToken ct)
    => await _mediator.Send(new ResolveDecisionCommand(id, req.Action, req.Note, GetUserId()), ct);

public record ResolveDecisionRequest(DecisionAction Action, string? Note);
```

### Frontend — Inline action UI

Update `decision-queue.component.ts` — thay slot button placeholder bằng inline form:

```typescript
template: `
  ...
  <div class="flex gap-2 mt-3" *ngIf="!expandedNoteFor(item.id)">
    <button (click)="onExecuteSell(item)"
            data-test="btn-sell"
            class="px-3 py-1.5 text-xs bg-red-600 hover:bg-red-700 text-white rounded-md font-bold">
      🔪 BÁN THEO KẾ HOẠCH
    </button>
    <button (click)="expandNote(item.id)"
            data-test="btn-hold"
            class="px-3 py-1.5 text-xs bg-amber-100 hover:bg-amber-200 text-amber-900 border border-amber-300 rounded-md font-medium">
      ✋ GIỮ + GHI LÝ DO
    </button>
  </div>
  <!-- Inline note form -->
  <div *ngIf="expandedNoteFor(item.id)" class="mt-3 space-y-2">
    <textarea [(ngModel)]="noteDrafts[item.id]"
              data-test="note-textarea"
              placeholder="Vì sao giữ? Ít nhất 20 ký tự — buộc bạn nghĩ kỹ."
              rows="3"
              class="w-full text-sm border border-amber-300 rounded-md px-3 py-2 focus:ring-2 focus:ring-amber-400"></textarea>
    <div class="text-xs text-gray-500">
      {{ (noteDrafts[item.id] || '').length }}/20 ký tự
    </div>
    <div class="flex gap-2">
      <button (click)="submitHold(item)"
              [disabled]="(noteDrafts[item.id] || '').length < 20"
              data-test="btn-submit-hold"
              class="px-3 py-1.5 text-xs bg-amber-600 hover:bg-amber-700 disabled:bg-gray-300 text-white rounded-md font-medium">
        Lưu lý do + Giữ
      </button>
      <button (click)="cancelNote(item.id)"
              class="px-3 py-1.5 text-xs text-gray-600 hover:text-gray-900">
        Hủy
      </button>
    </div>
  </div>
`
```

### TDD — Frontend inline actions

#### Test 1 (Red): nút BÁN gọi POST resolve với ExecuteSell

```typescript
it('should call resolve API with ExecuteSell when user clicks BÁN', () => {
  service.resolve.and.returnValue(of({ resultId: 't1', message: 'OK' }));
  component.items = [mockCriticalItem({id: 'StopLossHit:p1'})];
  fixture.detectChanges();

  fixture.debugElement.query(By.css('[data-test="btn-sell"]')).nativeElement.click();

  expect(service.resolve).toHaveBeenCalledWith('StopLossHit:p1', { action: 'ExecuteSell', note: null });
});
```

#### Test 2 (Red): nút GIỮ expand note form

```typescript
it('should expand inline note form when GIỮ clicked', () => {
  component.items = [mockItem({id: 'i1'})];
  fixture.detectChanges();

  fixture.debugElement.query(By.css('[data-test="btn-hold"]')).nativeElement.click();
  fixture.detectChanges();

  expect(fixture.debugElement.query(By.css('[data-test="note-textarea"]'))).toBeTruthy();
});
```

#### Test 3 (Red): submit disabled khi note < 20 chars

```typescript
it('should disable submit button when note shorter than 20 chars', () => {
  component.items = [mockItem({id: 'i1'})];
  component.noteDrafts['i1'] = 'ngắn';
  component.expandNote('i1');
  fixture.detectChanges();

  const btn = fixture.debugElement.query(By.css('[data-test="btn-submit-hold"]'));
  expect(btn.nativeElement.disabled).toBeTrue();
});
```

#### Test 4 (Red): item bị remove khỏi list sau resolve thành công (optimistic)

```typescript
it('should remove item from list after successful resolve', () => {
  service.resolve.and.returnValue(of({ resultId: 't1', message: 'OK' }));
  component.items = [mockItem({id: 'i1'}), mockItem({id: 'i2'})];
  fixture.detectChanges();

  component.onExecuteSell(component.items[0]);
  fixture.detectChanges();

  expect(component.items.length).toBe(1);
  expect(component.items[0].id).toBe('i2');
});
```

### Files thay đổi

**Backend:**
- `src/InvestmentApp.Application/Decisions/Commands/ResolveDecision/ResolveDecisionCommand.cs` (mới, ~150 LOC)
- `src/InvestmentApp.Application/Decisions/Commands/ResolveDecision/ResolveDecisionCommandValidator.cs` (mới, ~30 LOC)
- `src/InvestmentApp.Api/Controllers/DecisionsController.cs` (thêm ~20 LOC)
- `tests/InvestmentApp.Application.Tests/Decisions/ResolveDecisionCommandHandlerTests.cs` (mới, ~300 LOC)

**Frontend:**
- `frontend/src/app/core/services/decision.service.ts` (thêm `resolve()` method)
- `frontend/src/app/features/dashboard/widgets/decision-queue.component.ts` (~80 LOC inline action UI)
- `frontend/src/app/features/dashboard/widgets/decision-queue.component.spec.ts` (~120 LOC test mới)

---

## 7. P5 — Remove 3 widget khỏi Home (v1.1)

### Mục tiêu

Giảm noise trên Home. 3 widget đều có route riêng → unlink khỏi Home, không xóa feature.

### Quyết định cụ thể

| Widget | Action | Lý do | Đi đâu |
|--------|--------|-------|--------|
| Market Index strip (VNINDEX, HNX, UPCOM, VN30) | **Remove khỏi Home** | Không liên quan quyết định cá nhân, chỉ là noise macro | Đã có ở `/market-data` overview |
| Mini Equity Curve | **Remove khỏi Home** | Dùng để review post-hoc, không phải quyết định ngay | Full version đã có ở `/analytics` |
| Quick Actions (4 link tĩnh: Wizard/Market/Journals/Risk) | **Remove khỏi Home** | Trùng với menu header + bottom-nav | Header menu giữ nguyên |
| ~~Watchlist widget~~ | **GIỮ trên Home** | Agent UX flag mạnh: phá pre-trade routine (kỷ luật entry) | — |

### Implementation

**[dashboard.component.ts](../../frontend/src/app/features/dashboard/dashboard.component.ts):**

| Vị trí xóa | LOC ước tính |
|-----------|--------------|
| Market Index strip [:73-91](../../frontend/src/app/features/dashboard/dashboard.component.ts#L73-L91) + property `marketOverview: MarketOverview[]` + method `loadMarketOverview()` | ~30 LOC |
| Mini Equity Curve [:678-696](../../frontend/src/app/features/dashboard/dashboard.component.ts#L678-L696) + `@ViewChild('miniEquityCanvas')` + `miniEquityChart` + `equityCurveData` + `selectedRange` + `equityRanges` + chart init/destroy logic | ~50 LOC |
| Quick Actions [:822-873](../../frontend/src/app/features/dashboard/dashboard.component.ts#L822-L873) | ~52 LOC template |

### Side-effects cần check

- `getBatchPrices()` còn dùng ở 9 file khác → safe to remove khỏi dashboard.
- `manifest.webmanifest` có shortcut `/dashboard` không cần đổi (không có fragment trỏ vào widget cụ thể).
- User guide `assets/docs/bat-dau-su-dung.md` có nhắc widget nào → grep + sync nếu có.
- Notification deep link: verify không trỏ `/dashboard#market-overview` hay `/dashboard#equity-curve`.
- Chart.js import (`Chart.register(...registerables)`) → giữ vì widget khác có thể dùng (CompoundGrowthTracker, projection charts).

### TDD

Không cần test mới — chỉ là deletion. Verify spec hiện tại không reference widget xóa.

### Files thay đổi

- `frontend/src/app/features/dashboard/dashboard.component.ts` (xóa ~130 LOC: 3 widget templates + properties + methods)
- `frontend/src/assets/docs/bat-dau-su-dung.md` (sync nếu có nhắc widget xóa)
- `frontend/src/assets/CHANGELOG.md` (entry user-facing: "Đã đơn giản hóa Home để tập trung quyết định")

---

## 8. Out of scope (cố tình bỏ — tránh scope creep)

| Hạng mục | Lý do bỏ |
|----------|----------|
| Discipline format violation-based (Q7 V2) | Cần entity migration `PlannedHoldDays` + 90% legacy null + noisy với 5 lệnh. Effort +L. Defer V3. |
| AI mini-inline 1 câu thay panel (Q8 V2) | Streaming arch không fit + cost AI auto-generate × 6 lần/ngày + stale context. Defer V3. |
| Partial Sell action `[CHỐT 1 PHẦN]` (Q9 V2) | Duplicate ExitTarget. Cần expose `ExitTargets` DTO trước. Defer V2. |
| Rename `DashboardComponent` → `HomeComponent` (Q10 V2) | 25+ touchpoints, gain ≈ 0. Bác hẳn. |
| Commands `OverrideDecision` + `UpdateThesis` (Q11 V2) | Duplicate `HoldWithJournal` + endpoint `PUT /trade-plans/{id}` đã có. Bác hẳn. |
| Discipline widget show plan cụ thể | Vấn đề #4 — đã có pending-reviews link, đủ xài |
| Watchlist remove khỏi Home | Agent UX flag: phá pre-trade routine (kỷ luật entry). GIỮ. |
| Daily Routine grid restructure | Đang ở vị trí giữa, không cản decision flow |

---

## 9. Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| `ExecuteSell` tạo trade sai nếu plan dữ liệu không nhất quán (e.g. multi-lot có lot Executed nhưng position thật đã đóng) | Validate `position.Quantity > 0` trước khi tạo Trade. Nếu position = 0 → return error "Vị thế đã đóng, refresh queue" |
| User click BÁN nhầm | Confirm dialog trước khi POST với headline + quantity sẽ bán ("Xác nhận BÁN 100 FPT @ 89.5?") |
| Decision Queue endpoint chậm vì aggregate 3 sources | Cache 60s per-user. 3 source query song song qua `Task.WhenAll` |
| AI prompt mới ra response không-Vietnamese | Test 1 ở P2 lock prompt phải có "phản biện" — fail nếu prompt drift |
| TDD spec dashboard.component.spec.ts chưa tồn tại → set up Karma fixture lần đầu | Bootstrap với spec đơn giản P1 trước, các spec sau extend |
| **(v1.1)** Streak count tính sai khi user mới (chưa có snapshot lịch sử) | Default `daysWithoutViolation = 0` khi sample size < 1 day. Empty state vẫn hiển thị nhưng không show streak badge |
| **(v1.1)** P5 xóa Mini Equity Curve có thể làm user complain mất feature | Rollback dễ (chỉ là deletion FE, no BE change). Verify với CHANGELOG entry rõ ràng + link sang `/analytics` thay thế |
| **(v1.1)** P1 tách NetWorth widget có thể duplicate với Compound Growth Tracker hiện có | Compound Growth Tracker giữ nguyên position cũ giữa page. NetWorth widget mới ở top chỉ hiển thị 3 dòng (NetWorth + change% + reality gap), không trùng nội dung |

---

## 10. Success criteria

| Tiêu chí | Cách verify |
|---------|-------------|
| User mở Dashboard → thấy ngay "việc cần làm" ở top, không scroll | Manual QA + screenshot |
| Click BÁN → trade được tạo trong < 2 giây, không rời page | Browser verify với MintStableJwt |
| Click GIỮ → buộc nhập ≥ 20 chars → JournalEntry được tạo | Browser verify |
| Default Reality Gap CAGR hiển thị mà không cần config | Karma spec + manual QA |
| AI button mở panel với prompt "phản biện" → response chỉ ra ≥ 3 điểm sai | Manual QA với data realistic |
| **(v1.1)** Empty state Decision Queue khi 0 alert hiển thị `✅ kỷ luật + streak X ngày` thay vì biến mất | Karma spec + browser verify |
| **(v1.1)** NetWorth widget tách riêng ngắn gọn (≤ 3 dòng), Compound Growth Tracker vẫn còn full ở giữa page | Manual QA + screenshot |
| **(v1.1)** Home không còn 4 widget noise (Risk Alert + Advisory + Pending Review + Market Index + Equity Curve + Quick Actions) — chỉ còn block ép action | Visual diff trước/sau |
| Tất cả test xanh: `dotnet test` + `ng test` | CI green |

---

## 11. Update tài liệu khi ship

Theo CLAUDE.md, mỗi PR phải sync docs:

- `docs/architecture.md` — thêm `DecisionsController`, `GetDecisionQueueQueryHandler`, `ResolveDecisionCommandHandler`, `GetDisciplineStreakQueryHandler`, `NetWorthSummaryComponent`, AI use-case `portfolio-critique`
- `docs/business-domain.md` — thêm `DecisionItem` aggregate (ảo, view-model only) + workflow Resolve + endpoint `/me/discipline-score/streak`
- `docs/features.md` — thêm Phase mới "Dashboard Decision Engine v1.1"
- `docs/project-context.md` — note quyết định pivot Dashboard từ display → action engine + ghi Q4-Q11 hybrid review
- `frontend/src/assets/CHANGELOG.md` — entry user-facing: "Đã đơn giản hóa Home để tập trung quyết định kỷ luật"
- `frontend/src/assets/docs/` — guide mới về Decision Queue + xử lý alert + empty state positive
- ADR mới — quyết định "Dashboard Decision Engine v1.1" thay 3 widget bằng 1 + remove 3 widget khỏi Home + reject 5 đề xuất V2 (cross-layer change đáng record)

---

## 12. Lịch trình đề xuất (v1.1 ~ 13.5 ngày = 2.5 tuần)

| Day | Task |
|-----|------|
| Day 1 (sáng) | **P1** — Reality Gap CAGR FE-only (TDD red → green) |
| Day 1 (chiều) | **P1** (v1.1) — Tách NetWorth widget riêng + spec |
| Day 2 (sáng) | **P2** — AI rebrand backend prompt + test |
| Day 2 (chiều) | **P2** — Frontend AI button + spec, ship cả P1+P2 trong 1 PR |
| Day 3-4 | **P3** — Backend DecisionQueue query handler + tests + controller |
| Day 5 | **P3** (v1.1) — Backend `GetDisciplineStreakQuery` + tests |
| Day 6 | **P3** — Frontend Decision Queue widget + empty state positive + spec |
| Day 7 | **P3** — Xóa Risk Alert + Advisory + Pending Review widgets cũ + ship P3 trong 1 PR |
| Day 8-9 | **P4** — Backend ResolveDecision command + validator + tests |
| Day 10 | **P4** — Frontend inline actions (BÁN/GIỮ) + spec |
| Day 11 | **P5** (v1.1) — Remove Market Index + Equity Curve + Quick Actions khỏi Home + sync docs |
| Day 12 | Manual QA browser verify với MintStableJwt (P3 + P4 + P5) |
| Day 13 | Ship P4 + P5 trong 1 PR + update docs + ADR + CHANGELOG |

**Total:** ~13.5 ngày solo (2.5 tuần). Buffer 0.5 ngày cho debugging Karma fixture đầu tiên.

---

## 13. Notes về thứ tự PR

Plan v1.1 ship trong **3 PR** riêng để dễ rollback:

| PR | Scope | Lý do tách | Status |
|----|-------|-----------|--------|
| PR-1 | P1 + P2 | FE-only CAGR + AI rebrand string change. Risk thấp, ship sớm để test phản hồi user. | ✅ **Shipped 2026-05-04** (xem checkpoint dưới) |
| PR-2 | P3 (Decision Queue read-only) | Có backend mới + xóa 3 widget cũ. Cần manual QA kỹ. | 🔄 Pending |
| PR-3 | P4 + P5 | Inline action ghi data + xóa 3 widget noise. Ship cuối khi đã verify P3 ổn. | 🔄 Pending |

---

## Checkpoint — PR-1 (P1 + P2) shipped 2026-05-04

### Decisions

- Branch 2 condition cho CAGR=0 edge case: **REVERTED** keep `cagrValue === 0` (ban đầu) thay vì `!cagrIsStable` — sub-agent review flag overlap Branch 1+2 khi `cagrValue !== 0 && days < 30 && !cagrIsStable`. Edge case "stable + CAGR=0" lý thuyết khá hiếm và có thể chấp nhận rơi vào Branch 3 ("--").
- Reality Gap label format: **đổi sang điểm % tuyệt đối** (`target - cagrValue`), không dùng tỉ lệ (`100 - progress`). Đồng nhất 2 widget (Compound Growth Tracker + NetWorth) — cùng nói "Lệch X.X điểm %".
- `daily-briefing` use-case **giữ nguyên** trong service — `BuildPortfolioCritiqueContext` delegate vào nó cho data aggregation. KHÔNG deprecate.
- Personal Finance widget existing **KHÔNG xóa** — coexist với NetWorth widget compact mới (top widget = quick signal, mid widget = full breakdown).
- Skip `dashboard.component.spec.ts` lớn cho PR-1 — mock 15+ services value/cost không xứng. Coverage qua widget-level spec (NetWorthSummaryComponent 9 tests) + manual QA.

### Files changed

**Backend:**
- `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` — thêm `BuildPortfolioCritiqueSystemPrompt` (public static, ~17 LOC prompt) + `BuildPortfolioCritiqueContext` (private, delegate data từ daily-briefing) + case `"portfolio-critique"` trong switch.
- `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServicePortfolioCritiqueTests.cs` (mới, ~75 LOC, **6 tests**).

**Frontend:**
- `frontend/src/app/features/dashboard/widgets/networth-summary.component.ts` (mới, ~55 LOC).
- `frontend/src/app/features/dashboard/widgets/networth-summary.component.spec.ts` (mới, ~145 LOC, **9 tests** incl. boundary cagrValue===target + negative CAGR).
- `frontend/src/app/features/dashboard/dashboard.component.ts` — `cagrTargetSet=true` default + Reality Gap label điểm % + mount `<app-networth-summary>` + AI button rebrand.
- `frontend/src/app/core/services/ai.service.ts` — thêm `streamPortfolioCritique`.
- `frontend/src/app/shared/components/ai-chat-panel/ai-chat-panel.component.ts` — case `'portfolio-critique'`.

**Docs:**
- `docs/architecture.md` — section "Dashboard Decision Engine V1.1 P1+P2".
- `docs/business-domain.md` — AI endpoint table thêm `portfolio-critique`.
- `frontend/src/assets/CHANGELOG.md` — entry v2.55.0.
- `docs/plans/dashboard-decision-engine.md` — checkpoint này.

### Tests

- **6 xUnit** mới — lock prompt content adversarial. 295/295 Infrastructure pass.
- **9 Karma** mới — NetWorth widget render/hide/gap/boundary. 14/14 widget tests pass (NetWorth 9 + Discipline 5 existing).

### Affected layers

- Infrastructure (AiAssistantService — additive use-case, no contract change)
- Frontend Dashboard + AI panel + AI service

### Sub-agent review (sonnet)

Surface 7 findings (1 Critical, 4 Warning, 2 Minor). Triage:
- **Fixed in PR-1:** Finding 1 (Branch 2 overlap), 2 (label inconsistency), 3 (intentional dual-display comment), 6 (boundary tests), 7 (prompt safeguard 1 line)
- **Verified safe (defer):** Finding 4 (init flicker — pre-existing, low impact), 5 (`daily-briefing` orphan — verified NOT orphan, internal reuse cho `BuildPortfolioCritiqueContext`)

### Next — PR-2 (P3 Decision Queue + Empty State Positive)

**Read trước khi bắt đầu:**
- `docs/plans/dashboard-decision-engine.md` section "5. P3" (cho full TDD spec)
- `src/InvestmentApp.Application/Risk/` cho `IRiskService.GetStopLossAlertsAsync` pattern
- `src/InvestmentApp.Application/TradePlans/Queries/GetActiveAdvisories/` (P0.5 advisory)
- `src/InvestmentApp.Application/TradePlans/Queries/GetPendingThesisReviews/` (existing pattern reuse)

**Build:**
- Backend: `DecisionItemDto` + `GetDecisionQueueQuery` + `DecisionsController`. Aggregate 3 sources (StopLoss + Advisory + ThesisReviewDue) với dedupe + sort.
- Backend: `GetDisciplineStreakQuery` cho empty state positive (`daysWithoutViolation`).
- Frontend: `DecisionQueueComponent` widget với empty state `✅ Hôm nay đang kỷ luật + streak X ngày`.
- **Xóa hẳn 3 widget cũ** trong `dashboard.component.ts`: Risk Alert Banner + Advisory Widget + Pending Review section.
- Mount `<app-decision-queue>` ở vị trí #1 (top, trước NetWorth widget).

**Effort estimate:** 4-5 ngày solo.

**Branch:** Tạo từ master mới: `feat/dashboard-pr2-decision-queue`.
