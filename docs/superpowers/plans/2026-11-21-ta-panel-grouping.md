# Sắp xếp panel Phân tích kỹ thuật theo nhóm — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **Lane mode:** lane dev tự thực hiện task-by-task trong session của mình; review do lane review đảm nhận (không spawn sub-agent review).

**Ghi chú:** Task 2 phần EMA200 đã gỡ sau khi thực hiện (nguồn chỉ còn 67 ngày lịch sử), xem mục Sửa quyết định trong spec.

**Goal:** Panel phân tích kỹ thuật trang Tra cứu giá hiển thị theo 6 khối có tiêu đề (Tổng hợp lên đầu) và bổ sung Fibonacci + EMA200 đã tính sẵn.

**Architecture:** Chỉ sửa inline template của `market-data.component.ts` (giải thể lưới 3 cột đầu panel, xếp lại 10 thẻ + 2 bổ sung theo nhóm, giữ nguyên mọi guard điều kiện); không đổi TS logic trừ khi template cần alias. Không đụng backend/DTO.

**Tech Stack:** Angular 19 standalone + inline template, Tailwind, Karma + Jasmine (TestBed, By.css — pattern như `company-dossier-list.component.spec.ts`).

**Spec:** [docs/superpowers/specs/2026-11-21-ta-panel-grouping-design.md](../specs/2026-11-21-ta-panel-grouping-design.md)

## Global Constraints

- Làm trong worktree riêng, nhánh mới `feat/ta-panel-grouping` tạo từ `master`.
- UI text tiếng Việt có dấu đầy đủ; commit message tiếng Việt, prefix conventional-commit tiếng Anh; **không** thêm trailer attribution (quy ước user).
- Không dùng backtick trong HTML comment của inline template (TS1005 cascade).
- Mọi guard điều kiện hiện có giữ nguyên trừ chỗ spec nói rõ (S/R bỏ wrapper ẩn-khi-rỗng).
- Chạy test bằng: `npm test -- --watch=false --browsers=ChromeHeadless --include src/app/features/market-data/market-data.component.spec.ts` (cwd `frontend/`).

## Review Focus

1. Mã < 200 phiên (`ema200` undefined) → thẻ EMA vẫn hiện EMA20/50, dòng `EMA200: —` (test Task 2).
2. `fibonacci` undefined → thẻ Fibonacci hiện "Không đủ dữ liệu", không nổ (test Task 2).
3. `confluenceScore == null` VÀ `suggestedEntry` undefined → tiêu đề "🎯 Tổng hợp" ẩn, 5 nhóm còn lại vẫn hiện (test Task 1).
4. `supportLevels`/`resistanceLevels` rỗng → 2 thẻ vẫn render với "Chưa xác định" (test Task 1).
5. `marketCondition === 'unknown'` → thẻ Trạng thái ẩn nhưng nhóm Xu hướng vẫn hiện EMA + ADX (test Task 1).

---

### Task 1: Xếp lại template thành 6 khối + spec mới

**Files:**
- Modify: `frontend/src/app/features/market-data/market-data.component.ts` (khối template ~192–548)
- Test: `frontend/src/app/features/market-data/market-data.component.spec.ts` (tạo mới)

**Interfaces:**
- Produces: mỗi tiêu đề khối là element có attribute `data-group` với giá trị theo thứ tự: `tong-hop`, `xu-huong`, `dong-luong`, `bien-dong`, `dong-tien`, `moc-gia`; text tương ứng `🎯 Tổng hợp`, `📈 Xu hướng`, `⚡ Động lượng`, `📏 Biến động`, `💰 Dòng tiền`, `🧱 Mốc giá`. Task 2 dựa vào các nhóm này.

- [ ] **Step 1: Viết spec mới với các test fail**

Setup: spy `MarketDataService` (`getMarketOverview`, `getBatchPrices`, `getTopFluctuation` trả `of` dữ liệu rỗng/trung tính), spy `NotificationService`, spy `CompanyDossierService`, `provideRouter([])`, **`provideHttpClient()` + `provideHttpClientTesting()`** (bắt buộc — component import `AiChatPanelComponent` → `AiService` inject `HttpClient`, thiếu là `TestBed.createComponent` nổ NullInjectorError; pattern như company-dossier-list.component.spec.ts:35-36). Helper `fullTa(): TechnicalAnalysis` trả stub đủ mọi trường (kể cả `fibonacci`, `ema200`). Gán thẳng `component.analysis = ...` rồi `detectChanges()`.

```ts
it('6 tiêu đề khối render đúng thứ tự', ...)
  // queryAll(By.css('[data-group]')) → map getAttribute('data-group')
  // toEqual(['tong-hop','xu-huong','dong-luong','bien-dong','dong-tien','moc-gia'])

it('không có analysis thì không có tiêu đề khối nào', ...)
  // analysis = null → queryAll('[data-group]').length === 0

it('confluence null và không có suggestedEntry thì ẩn khối Tổng hợp', ...)
  // fullTa() nhưng confluenceScore = undefined, suggestedEntry = undefined
  // '[data-group="tong-hop"]' không tồn tại; 5 nhóm kia vẫn đủ

it('hỗ trợ/kháng cự rỗng vẫn hiện 2 thẻ với Chưa xác định', ...)
  // supportLevels: [], resistanceLevels: [] → textContent chứa 'Chưa xác định' ×2

it('marketCondition unknown thì ẩn thẻ Trạng thái nhưng nhóm Xu hướng vẫn còn', ...)
  // '[data-group="xu-huong"]' tồn tại; text 'Trạng thái thị trường' không có
```

- [ ] **Step 2: Chạy test, xác nhận FAIL** — `npm test -- --watch=false --browsers=ChromeHeadless --include src/app/features/market-data/market-data.component.spec.ts`, expect fail vì chưa có `[data-group]`.

- [ ] **Step 3: Sửa template**

Thứ tự trong khối Technical Analysis: header panel + badge giữ nguyên → tiêu đề `tong-hop` + Confluence (guard `confluenceScore != null`) + khối Gợi ý giao dịch nguyên trạng (guard `suggestedEntry`, kèm nút Tạo Trade Plan); tiêu đề Tổng hợp có guard `@if (analysis.confluenceScore != null || analysis.suggestedEntry)` → nhóm `xu-huong`: thẻ EMA + thẻ ADX + thẻ Trạng thái thị trường (chuyển từ lưới 3 cột cũ, giữ guard) → nhóm `dong-luong`: RSI · Stochastic · MACD · thẻ Phân kỳ (chuyển xuống, giữ cả `@else`) → nhóm `bien-dong`: Bollinger · ATR → nhóm `dong-tien`: Khối lượng · OBV · MFI → nhóm `moc-gia`: 2 thẻ Hỗ trợ/Kháng cự bỏ wrapper `*ngIf` ẩn-khi-rỗng (fallback "Chưa xác định" bên trong đã có sẵn). Lưới 3 cột đầu panel xoá hẳn. Mỗi nhóm vẫn là grid thẻ như cũ.

- [ ] **Step 4: Chạy test, xác nhận PASS** (test Fibonacci/EMA200 chưa viết — chưa liên quan).
- [ ] **Step 5: Commit** — `refactor(market-data): panel phân tích kỹ thuật xếp theo 6 khối chỉ báo`

### Task 2: Dòng EMA200 + thẻ Fibonacci

**Files:**
- Modify: `frontend/src/app/features/market-data/market-data.component.ts` (thẻ EMA trong `xu-huong`; thẻ mới trong `moc-gia`)
- Test: `frontend/src/app/features/market-data/market-data.component.spec.ts`

**Interfaces:**
- Consumes: nhóm `xu-huong`, `moc-gia` từ Task 1; `analysis.ema200`, `analysis.fibonacci` từ `TechnicalAnalysis` ([market-data.service.ts:149-163](../../../frontend/src/app/core/services/market-data.service.ts#L149-L163)).

- [ ] **Step 1: Viết test fail**

```ts
it('có ema200 thì thẻ EMA hiện giá trị EMA200', ...)
  // assert bằng toContain('Xu hướng (EMA20/50/200)') — KHÔNG toEqual, tiêu đề thật có emoji 📊 phía trước
  // text chứa 'EMA200:' và không chứa 'EMA200: —'

it('thiếu ema200 thì hiện EMA200: —', ...)

it('có fibonacci thì thẻ Fibonacci hiện 5 mốc retracement', ...)
  // queryAll('[data-fib-level]').length === 5; nhãn '23.6%','38.2%','50%','61.8%','78.6%'

it('thiếu fibonacci thì thẻ hiện Không đủ dữ liệu', ...)
```

- [ ] **Step 2: Chạy test, xác nhận FAIL.**
- [ ] **Step 3: Sửa template** — thẻ EMA đổi tiêu đề "📊 Xu hướng (EMA20/50/200)", thêm dòng `@if (analysis.ema200) { EMA200: {{ ... | vndCurrency }} } @else { EMA200: — }` dưới EMA50. Thẻ Fibonacci mới trong `moc-gia`: `@if (analysis.fibonacci; as fib)` → 5 dòng `data-fib-level` (nhãn % + giá trị `vndCurrency`), swing high/low chữ nhỏ bên dưới; `@else` "Không đủ dữ liệu".
- [ ] **Step 4: Chạy test, xác nhận PASS toàn file spec.**
- [ ] **Step 5: Commit** — `feat(market-data): hiện Fibonacci và EMA200 đã tính sẵn trên panel kỹ thuật`

### Task 3: Đồng bộ tài liệu

**Files:**
- Modify: `frontend/src/assets/CHANGELOG.md` (mục mới: panel xếp theo 6 khối + Fibonacci/EMA200)
- Modify: `frontend/src/assets/docs/phan-tich-thi-truong.md` (mô tả 6 khối, 2 chỉ báo mới)

- [ ] **Step 1: Cập nhật 2 file theo nội dung spec.**
- [ ] **Step 2: Xác nhận** — đọc lại, tên nhóm/emoji khớp đúng template Task 1.
- [ ] **Step 3: Commit** — `docs(market-data): hướng dẫn panel kỹ thuật theo nhóm chỉ báo`

---

## Self-review (đã chạy)

- Spec coverage: Goals 1–3, Constraints 1–4, Test section → Task 1–2; mục "Tài liệu phải cập nhật" → Task 3. Không còn mục nào chưa có task.
- Types/tên nhất quán: `data-group` 6 giá trị khai ở Task 1, Task 2 tiêu thụ đúng tên.
- Review Focus 5 mục đều có test được ghim vào task sở hữu.
- Proportion: plan ngắn hơn component được sửa; body code chỉ ở chỗ quyết định (guard, nhãn, thứ tự).
