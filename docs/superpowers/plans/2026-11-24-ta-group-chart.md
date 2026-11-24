# Biểu đồ chỉ báo theo nhóm — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **Lane mode:** dev lane `Group_indication - dev` làm task-by-task trong session của mình; review lane review cả nhánh trước commit cuối. Không spawn sub-agent review.

**Goal:** Bấm tiêu đề một khối chỉ báo trên panel Phân tích kỹ thuật (trang Tra cứu giá) thì biểu đồ nến vẽ các chỉ báo của nhóm đó.

**Architecture:** Backend thêm `GetSeriesAsync` trả 18 chuỗi chỉ báo theo từng phiên. Các scalar hiện có được tính lại từ phần tử cuối của chuỗi, nên thẻ và đường dùng chung một công thức. Frontend có một hàm thuần ánh xạ nhóm sang lớp vẽ, một component biểu đồ nhiều tầng (lightweight-charts v4, mỗi tầng một instance, đồng bộ trục thời gian), và phần nối vào `market-data.component.ts`.

**Tech Stack:** .NET 9 + xUnit/FluentAssertions/Moq · Angular 19 standalone, inline template, Tailwind · Karma + Jasmine · `lightweight-charts@^4.2.2`.

**Spec:** `docs/superpowers/specs/2026-11-24-ta-group-chart-design.md`.

## Global Constraints

- Nhánh `feat/ta-group-chart` tạo từ `master` **sau khi** security PR B merge; làm ở main tree, không worktree.
- `lightweight-charts` giữ `^4.2.2`, không nâng v5. Không vẽ EMA200.
- Payload `GET /api/v1/market/stock/{symbol}/analysis` **không đổi**. Mọi test TA hiện có phải xanh, không sửa số kỳ vọng.
- Mỗi điểm của chuỗi được làm tròn **đúng như scalar tương ứng đang làm** (ví dụ EMA/Bollinger `Math.Round(x, 0)`), để phần tử cuối == scalar.
- Service frontend tự gắn `getHeaders()` (không có interceptor auth).
- UI text tiếng Việt có dấu đầy đủ. Không dùng backtick trong HTML comment của inline template.
- Commit message tiếng Việt có dấu, prefix conventional-commit tiếng Anh, không trailer attribution.
- Lệnh test FE (cwd `frontend/`): `npm test -- --watch=false --browsers=ChromeHeadless --include <spec>`.
- Lệnh test BE: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter <Class>`; cuối nhánh chạy full `dotnet test`.
- `ITechnicalIndicatorService.cs` nằm trong `Application/Common/Interfaces` nhưng **grep `^namespace` trước** khi thêm type (namespace có thể không khớp thư mục).

## Review Focus

1. **Đổi mã khi chuỗi đang tải.** Response muộn của mã cũ không được vẽ lên mã mới. Test ở Task 6: `late response for previous symbol is ignored`.
2. **Mã dưới 20 phiên hoặc chuỗi toàn `null`.** Biểu đồ vẫn có nến, mỗi chỉ báo thiếu hiện "Không đủ dữ liệu", không ném lỗi. Test ở Task 1 (backend), Task 4 (ánh xạ) và Task 5 (component).
3. **Request chuỗi lỗi (500/timeout).** Thẻ vẫn dùng được, vùng biểu đồ hiện "Không tải được dữ liệu biểu đồ", bỏ chọn nhóm. Test ở Task 6: `series error shows message and clears selection`.
4. **Nút "Tạo Trade Plan" trong khối Tổng hợp không bị cướp click.** Chỉ tiêu đề `h4[data-group]` là vùng bấm. Test ở Task 6: `clicking create-plan button does not toggle group`.
5. **Rời trang hoặc resize.** Chart và ResizeObserver được dispose, không lỗi console. Test ở Task 5: `ngOnDestroy removes all chart instances`.

---

### Task 0: Chuẩn bị nhánh và spec

- [ ] Tạo nhánh `feat/ta-group-chart` từ `master` (sau khi PR B merge).
- [ ] Chép spec vào `docs/superpowers/specs/2026-11-24-ta-group-chart-design.md` và chép plan này vào `docs/superpowers/plans/2026-11-24-ta-group-chart.md`, sửa link tương đối nếu cần. Commit `docs(market-data): spec và plan biểu đồ chỉ báo theo nhóm`.

### Task 1: Chuỗi chỉ báo ở backend + parity với scalar

**Files:**
- Modify: `src/InvestmentApp.Application/Common/Interfaces/ITechnicalIndicatorService.cs`, thêm method + DTO
- Modify: `src/InvestmentApp.Infrastructure/Services/TechnicalIndicatorService.cs`
- Test (create): `tests/InvestmentApp.Infrastructure.Tests/Services/TechnicalIndicatorServiceSeriesTests.cs`. Dùng lại cách mock `IMarketDataProvider` + `GeneratePriceHistory` của `TechnicalIndicatorServiceTests.cs`.

**Interfaces (Produces):**
```csharp
Task<TechnicalSeriesResult> GetSeriesAsync(string symbol, int months = 12, CancellationToken cancellationToken = default);

public class TechnicalSeriesResult {
  public string Symbol { get; set; } = null!;
  public List<DateTime> Dates { get; set; } = new();
  public List<decimal> Open, High, Low, Close;     // = new()
  public List<long> Volume;                        // = new()
  public List<decimal?> Ema20, Ema50, BollingerUpper, BollingerMiddle, BollingerLower,
    Rsi14, StochK, StochD, MacdLine, MacdSignal, MacdHistogram,
    Adx, PlusDi, MinusDi, Atr14, Obv, Mfi14, VolumeSma20;   // 18 chuỗi, = new()
}
```
Mọi mảng dài bằng `Dates`. Điểm chưa đủ warm-up là `null`. Dưới 20 phiên: nến đầy đủ, 18 chuỗi toàn `null`.

- [ ] **Step 1: Viết test fail**
  - `Series_LastPoint_Equals_Scalar_For_All_18_Indicators`: cùng một bộ giá 150 phiên cố định, gọi `AnalyzeAsync` và `GetSeriesAsync`; với mỗi cặp (Ema20↔Ema20, Ema50, BollingerUpper/Middle/Lower, Rsi14, StochK/D↔StochasticK/D, MacdLine/Signal/Histogram, Adx, PlusDi, MinusDi, Atr14↔Atr, Obv, Mfi14↔Mfi, VolumeSma20↔AvgVolume20), assert `series.Last() == scalar`. Dev grep tên property scalar thật trong `TechnicalAnalysisResult` rồi ghép cặp.
  - `Series_Arrays_Have_Same_Length_As_Dates`.
  - `Ema20_Is_Null_Before_Warmup_And_Set_At_Index_19`.
  - `Under_20_Bars_Returns_Candles_And_All_Null_Indicators`: 15 phiên → `Close.Count == 15`, mọi chuỗi chỉ báo `All(x => x == null)`.
  - `GetSeriesAsync_Uses_Same_Date_Range_As_AnalyzeAsync`: verify mock `GetHistoricalPricesAsync` được gọi với `from` ≈ `UtcNow.AddMonths(-12)`.
- [ ] **Step 2: Chạy, xác nhận FAIL** (method chưa tồn tại → lỗi biên dịch là đỏ hợp lệ).
- [ ] **Step 3: Implement.** Với mỗi `CalculateX` đang trả giá trị cuối, thêm `CalculateXSeries(...)` trả `List<decimal?>` dài bằng input. Scalar cũ trở thành `CalculateXSeries(...).Last()` (giữ chữ ký private cũ để code gọi không đổi). `CalculateMacdAt`, logic giao cắt, chuỗi xu hướng OBV và các tín hiệu chữ **giữ nguyên**. `VolumeSma20` dùng đúng công thức của `AvgVolume20`.
- [ ] **Step 4: Chạy test mới + toàn bộ `TechnicalIndicatorService*Tests`, tất cả PASS, không sửa số kỳ vọng cũ.**
- [ ] **Step 5: Commit** `feat(market-data): tính chuỗi chỉ báo kỹ thuật theo phiên, scalar lấy từ điểm cuối chuỗi`.

### Task 2: Endpoint `analysis/series`

**Files:**
- Create: `src/InvestmentApp.Application/MarketData/Queries/GetTechnicalSeries/GetTechnicalSeriesQuery.cs` (query + handler, cùng kiểu với `GetTechnicalAnalysisQuery`: `Symbol.ToUpper().Trim()`)
- Modify: `src/InvestmentApp.Api/Controllers/MarketDataController.cs`, thêm `[HttpGet("stock/{symbol}/analysis/series")]` cạnh `GetTechnicalAnalysis`
- Test: `tests/InvestmentApp.Application.Tests/MarketData/GetTechnicalSeriesQueryHandlerTests.cs`

**Interfaces:** Consumes `ITechnicalIndicatorService.GetSeriesAsync` (Task 1). Produces route `GET /api/v1/market/stock/{symbol}/analysis/series` → `TechnicalSeriesResult` (JSON camelCase như endpoint `analysis`).

- [ ] **Step 1: Test fail** `Handle_Normalizes_Symbol_And_Calls_GetSeriesAsync`: `" vnm "` → service được gọi với `"VNM"`, trả đúng object.
- [ ] **Step 2: FAIL. Step 3: Implement. Step 4: PASS.**
- [ ] **Step 5: Curl local** (API đã chạy, JWT test account theo skill `qa-verify`): `GET /api/v1/market/stock/FPT/analysis/series` → 200, `dates.length == close.length`, `ema20` có giá trị cuối khớp `ema20` của `/analysis`. Dán output vào note của PR.
- [ ] **Step 6: Commit** `feat(market-data): endpoint chuỗi chỉ báo kỹ thuật`.

### Task 3: Service + model frontend

**Files:**
- Modify: `frontend/src/app/core/services/market-data.service.ts`
- Test: `frontend/src/app/core/services/market-data.service.spec.ts` (tạo nếu chưa có; `provideHttpClient()` + `provideHttpClientTesting()`)

**Interfaces (Produces):**
```ts
export interface TechnicalSeries {
  symbol: string; dates: string[];
  open: number[]; high: number[]; low: number[]; close: number[]; volume: number[];
  ema20: (number|null)[]; ema50: (number|null)[];
  bollingerUpper: (number|null)[]; bollingerMiddle: (number|null)[]; bollingerLower: (number|null)[];
  rsi14: (number|null)[]; stochK: (number|null)[]; stochD: (number|null)[];
  macdLine: (number|null)[]; macdSignal: (number|null)[]; macdHistogram: (number|null)[];
  adx: (number|null)[]; plusDi: (number|null)[]; minusDi: (number|null)[];
  atr14: (number|null)[]; obv: (number|null)[]; mfi14: (number|null)[]; volumeSma20: (number|null)[];
}
getTechnicalSeries(symbol: string): Observable<TechnicalSeries>
```
- [ ] **Step 1: Test fail** `getTechnicalSeries calls analysis/series with auth header`: expectOne URL `${API_URL}/stock/FPT/analysis/series`, `req.request.headers.has('Authorization')` true.
- [ ] **Step 2–4: FAIL → implement (giống `getTechnicalAnalysis`, kèm `catchError(this.handleError)`) → PASS.**
- [ ] **Step 5: Commit** `feat(market-data): service lấy chuỗi chỉ báo`.

### Task 4: Ánh xạ nhóm sang lớp vẽ (hàm thuần)

**Files:**
- Create: `frontend/src/app/features/market-data/ta-group-layers.ts`
- Test: `frontend/src/app/features/market-data/ta-group-layers.spec.ts`

**Interfaces (Produces):**
```ts
export type TaGroup = 'tong-hop' | 'xu-huong' | 'dong-luong' | 'bien-dong' | 'dong-tien' | 'moc-gia';
export interface LineLayer { label: string; values: (number|null)[]; color?: string; kind?: 'line' | 'histogram'; }
export interface PaneLayer { title: string; lines: LineLayer[]; guides?: number[]; }        // guides: vạch ngang 30/70, 20/80, 25
export interface PriceLineLayer { label: string; price: number; }
export interface GroupLayers { overlays: LineLayer[]; panes: PaneLayer[]; priceLines: PriceLineLayer[]; notes: string[]; }
export function buildGroupLayers(group: TaGroup | null, s: TechnicalSeries, a: TechnicalAnalysis): GroupLayers
```
Bảng ánh xạ y như mục "Nhóm → cái được vẽ" trong spec. `notes` chứa `"<Tên chỉ báo>: Không đủ dữ liệu"` cho mỗi chuỗi toàn `null`, và `"Chưa có gợi ý giao dịch"` khi nhóm Tổng hợp mà `a.suggestedEntry` không có. Fibonacci: 5 mức thoái lui + đỉnh/đáy swing, nhãn `Fib 23.6%` … `Fib 78.6%`, `Đỉnh swing`, `Đáy swing`. `group === null` → mọi mảng rỗng.

- [ ] **Step 1: Test fail** (một `it` cho mỗi nhóm, kiểm số overlay/pane và nhãn; thêm):
  - `xu-huong → overlays EMA20, EMA50; pane ADX with guides [25]`
  - `dong-luong → 3 panes RSI [30,70], MACD, Stochastic [20,80]; no overlays`
  - `moc-gia → priceLines include every support, resistance and 7 fibonacci labels`
  - `tong-hop without suggestedEntry → no priceLines, note 'Chưa có gợi ý giao dịch'`
  - `all-null ema50 → note 'EMA50: Không đủ dữ liệu'`
  - `null group → empty layers`
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `feat(market-data): ánh xạ nhóm chỉ báo sang lớp vẽ biểu đồ`.

### Task 5: Component biểu đồ nhiều tầng (tái dùng được)

**Files:**
- Create: `frontend/src/app/shared/components/indicator-chart/indicator-chart.component.ts`
- Test: `frontend/src/app/shared/components/indicator-chart/indicator-chart.component.spec.ts`

**Interfaces:**
- Consumes: `GroupLayers`, `LineLayer`, `PaneLayer`, `PriceLineLayer` (Task 4). Chuyển các type này ra `shared/` nếu dev thấy hợp lý; khi đó cập nhật import ở Task 4.
- Produces: `<app-indicator-chart [dates] [candles] [layers]>`, trong đó `dates: string[]`, `candles: {open:number[],high:number[],low:number[],close:number[]}`, `layers: GroupLayers`. Tầng giá cao 300px, mỗi pane dưới 120px. Đồng bộ `timeScale().subscribeVisibleLogicalRangeChange` và crosshair giữa các instance. Có ResizeObserver. `notes` hiển thị dưới biểu đồ.

- [ ] **Step 1: Test fail**
  - `renders 1 + panes.length chart containers`
  - `rebuilds panes when layers change` (từ 3 pane xuống 1 pane → còn 2 container)
  - `shows notes text` (gồm "Không đủ dữ liệu")
  - `ngOnDestroy removes all chart instances` (spy `remove()` của mỗi chart, hoặc đếm container rỗng sau destroy; không có lỗi console)
  - `skips null points without throwing` (chuỗi xen `null`)
- [ ] **Step 2–4: FAIL → implement (theo mẫu `symbol-timeline.component.ts`: `createChart`, `addCandlestickSeries`, `addLineSeries`, `addHistogramSeries`, `createPriceLine`; điểm `null` bỏ khỏi data của series) → PASS.**
- [ ] **Step 5: Commit** `feat(shared): component biểu đồ nhiều tầng đồng bộ trục thời gian`.

### Task 6: Nối vào panel Phân tích kỹ thuật + tài liệu

**Files:**
- Modify: `frontend/src/app/features/market-data/market-data.component.ts` (6 `h4[data-group]` tại ~212/279/345/439/484/541, state, template biểu đồ ngay dưới header panel)
- Test: `frontend/src/app/features/market-data/market-data.component.spec.ts` (đã có; giữ setup provider hiện tại)
- Docs: `frontend/src/assets/CHANGELOG.md` (phiên bản minor mới), `docs/features.md`, `docs/architecture.md` (component shared mới + endpoint mới), `docs/business-domain.md` (API mới), user guide trong `frontend/src/assets/docs/*.md` có mục Tra cứu giá (thêm đoạn "Bấm tên nhóm để xem biểu đồ").

**Interfaces:** Consumes `getTechnicalSeries`, `getTechnicalAnalysis` (Task 3), `buildGroupLayers` (Task 4), `<app-indicator-chart>` (Task 5). State trong component: `selectedGroup: TaGroup | null`, `seriesCache: Map<string, TechnicalSeries>`, `chartError: string | null`, `chartLoading: boolean`.

- [ ] **Step 1: Test fail**
  - `clicking a group header selects it and sets aria-pressed=true`
  - `before any click shows hint 'Bấm tên nhóm để xem biểu đồ' and no chart`
  - `clicking the selected header again clears selection and keeps candles-only chart`
  - `first click loads series and analysis together (forkJoin) and replaces analysis` (spy cả hai method, mỗi cái được gọi 1 lần)
  - `second click on another group uses cache` (`getTechnicalSeries` vẫn chỉ gọi 1 lần)
  - `Enter key on header toggles group`
  - `late response for previous symbol is ignored` (đổi mã trước khi Subject của mã cũ emit → không vẽ, cache không có mã cũ cho mã mới)
  - `series error shows message and clears selection` (text "Không tải được dữ liệu biểu đồ"; thẻ vẫn render)
  - `clicking create-plan button does not toggle group`
  - Toàn bộ test cũ trong spec vẫn PASS.
- [ ] **Step 2: FAIL. Step 3: Implement.** Tiêu đề thêm `role="button" tabindex="0" [attr.aria-pressed]` và `(click)`/`(keydown.enter)`/`(keydown.space)`, class nổi bật khi được chọn. Khối Tổng hợp chỉ đặt handler trên `h4`, không đặt trên `div` bao ngoài. Đổi mã thì xoá `selectedGroup`, `chartError`; dùng `switchMap` hoặc kiểm `symbol` khi response về.
- [ ] **Step 4: PASS + `npm run build` xanh.**
- [ ] **Step 5: Kiểm trên browser** theo skill `qa-verify`: bấm lần lượt 6 nhóm với một mã nhiều dữ liệu và một mã ít dữ liệu, chụp màn hình; xác nhận điểm cuối EMA20 khớp thẻ; nút "Tạo Trade Plan" vẫn mở form.
- [ ] **Step 6: Cập nhật docs** như danh sách Files ở trên.
- [ ] **Step 7: Commit** `feat(market-data): bấm nhóm chỉ báo để xem biểu đồ, kèm tài liệu`.

### Hoàn tất nhánh
- [ ] Full `dotnet test` + full FE test file liên quan; gửi review lane cả nhánh. Sau APPROVE: root chạy challenger, dev push và mở PR bằng `/pr`, root merge khi CI xanh.
