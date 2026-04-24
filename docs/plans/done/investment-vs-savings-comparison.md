# Kế hoạch — So sánh hiệu suất đầu tư với tài khoản tiết kiệm

> Tài liệu kế hoạch — thêm tính năng "So sánh hiệu suất danh mục đầu tư với sổ tiết kiệm lãi suất" (opportunity cost vs. savings benchmark).
> Ngày tạo: 2026-04-23
> Trạng thái: **Plan v2 — đã qua 2 review (critical + doc compliance), fix 2 bug toán + 1 UX trap. Session khác pick up implement.**
> Tìm trước plan cũ (3 agent search): **không tồn tại** — design mới.

---

## Lịch sử revise

| Version | Ngày | Thay đổi |
|---|---|---|
| v1 | 2026-04-23 | Draft đầu — defer bank rate scraper, dùng default rate config |
| **v2** | 2026-04-23 | **+ Bank rate scraper từ 24hmoney** (user yêu cầu). Fix 2 bug toán: (1) withdrawal compounding (running balance iterative), (2) double-count dividend (chỉ dùng Deposit/Withdraw). Fix UX: "best rate" là toggle preset, không phải default. Monthly compound. Perf: client-side recompute. Thêm disclaimer + tax note. Thêm hook MaturityDate + VNINDEX future. |

---

## Bối cảnh & lý do

User là retail investor solo. Câu hỏi kinh điển mỗi kỳ review:

> "Nếu tôi gửi tiết kiệm cùng số tiền đó thì đã được bao nhiêu rồi? Đầu tư có thực sự đáng công không?"

Đây là **opportunity cost**. Có 3 benchmark khả dĩ:

1. **Lãi suất trung bình của Savings accounts user đang có** — cá nhân hóa, ổn định theo thời gian, không bi quan.
2. **Lãi suất cao nhất trên thị trường VN hiện tại** (scrape 24hmoney) — "nếu tôi khôn ngoan chọn ngân hàng cao nhất thì sao?". Có tính tham khảo, không nên làm default.
3. **Lãi suất do user nhập trực tiếp** — sandbox "tôi giả sử 5%/năm thì sao?".

MVP hỗ trợ **cả 3** qua 1 rate picker với 3 preset.

## Trạng thái hạ tầng

| Thành phần | Có sẵn? | File |
|---|---|---|
| Cash flow có date + signed amount | ✅ | `CapitalFlow` entity |
| Portfolio snapshot time-series (daily) | ✅ | `PortfolioSnapshotEntity` + `portfolio_snapshots` collection |
| TWR / MWR / CAGR calc | ✅ | `CashFlowAdjustedReturnService`, `PerformanceMetricsService` |
| Equity curve API + UI | ✅ | `AdvancedAnalyticsController.GetEquityCurve` + Chart.js trên dashboard |
| FinancialAccount có `InterestRate` | ✅ | optional field |
| **Scraper pattern 24hmoney** | ✅ | `HmoneyGoldPriceProvider` — mirror 1-1 cho bank rates |
| Hypothetical savings calc | ❌ | **Cần build** |
| Bank rate scraper từ 24hmoney | ❌ | **Cần build — copy pattern gold scraper** |
| VNINDEX benchmark | ❌ | Out of scope (roadmap khác) — nhưng plan này phải không khóa cứng DTO 2-line để tương thích |

## Phạm vi MVP

1. **Backend** — 3 service mới:
   - `IBankRateScraperService` — scrape 24hmoney, trả top rate theo term (1T/3T/6T/9T/12T).
   - `IHypotheticalSavingsReturnService` — tính running balance iterative theo cash flow + rate.
   - `GetSavingsComparisonQuery` — handler orchestrate.
2. **API endpoints mới**:
   - `GET /api/v1/analytics/bank-rates` → top rate cho mỗi term + update timestamp.
   - `GET /api/v1/analytics/portfolio/{portfolioId}/vs-savings?savingsRate=...&asOf=...` → `SavingsComparisonDto`.
3. **Frontend Analytics page** — panel "So sánh với tiết kiệm":
   - Rate picker 3 preset: **"Sổ của tôi"** (avg user's savings, default) / **"Cao nhất thị trường"** (top 12T từ scraper, có disclaimer) / **"Tự nhập"** (slider).
   - Line chart overlay: actual portfolio + hypothetical savings — **client-side recompute** khi đổi rate (không round-trip server).
   - Số liệu: end value, chênh lệch VND + %, Chênh lệch hiệu suất năm (nếu ≥ 1 năm).
4. **Dashboard badge**: "vs tiết kiệm: +12.4%" (neutral gray khi |Δ| < 2%, xanh/đỏ khi vượt).

## Không làm trong MVP (defer có chủ đích)

- Tax on savings interest — **VN cá nhân miễn thuế TNCN với lãi tiết kiệm** (Luật Thuế TNCN Art. 4 khoản 7). Không defer, khẳng định luôn.
- VNINDEX benchmark — roadmap khác, nhưng DTO thiết kế sẵn để extend.
- Inflation adjustment — nominal-vs-nominal đủ fair (2 bên cùng chịu).
- So sánh per-position (chỉ tổng portfolio).
- Kỳ hạn 24T — 24hmoney master table không có, defer.

## Quyết định thiết kế

| # | Quyết định | Lý do |
|---|---|---|
| D1 | **Monthly compound** `(1 + r/12)^months` | Gần thực tế VN hơn daily (~0.015%/năm chênh lệch ít, nhưng đỡ bias) |
| D2 | **Default rate** = weighted avg theo balance của user's Savings có `InterestRate != null`. UI disclose "N/M sổ có nhập lãi suất". Nếu không có savings hoặc all null → fallback **5%/năm** (config `SavingsComparison:DefaultFallbackRate`). | Conservative personal anchor, không anxiety-inducing |
| D3 | **Best rate scrape** là toggle preset, KHÔNG default. Có tooltip "Chỉ tham khảo — bạn không thực sự được lãi suất này trừ khi đã chọn NH đó". | Devil's advocate review: cherry-pick mà làm default sẽ misleading |
| D4 | **Cash flow filter**: chỉ `Type ∈ {Deposit, Withdraw}`. Bỏ Dividend/Interest/Fee. | Dividend là return của đầu tư — tính vào hypothetical = double-count |
| D5 | **Running balance iterative** (KHÔNG compound từng flow độc lập) | Fix bug review: `withdrawal × (1+r)^days` zeros out interest đã sinh trước rút |
| D6 | **Client-side recompute curve** khi đổi rate | Server chỉ trả flows + snapshots 1 lần; rate là pure math → 0 lý do round-trip |
| D7 | **Alpha chỉ show khi `days ≥ 365`** (annualized mới có nghĩa). Dưới 1 năm: chỉ hiện period return diff. | Dưới 1 năm annualize → variance lớn, bị chiếu quỳ |
| D8 | **Panel primary home = Analytics page**, dashboard chỉ badge nhỏ | Phân tích sâu ở Analytics, badge dashboard link sang |
| D9 | **Scraper cache dual-tier** (fresh 6h + stale 24h) | Mirror gold scraper pattern. 24hmoney refresh daily midnight |
| D10 | **Hook tương lai**: DTO có field `benchmarks: Map<name, CurvePoint[]>` thay vì `savingsCurve: CurvePoint[]` riêng → dễ add VNINDEX sau | Tránh khóa cứng 2-line |
| D11 | **Hook MaturityDate**: Khi `savings-term-dates.md` land, rate picker thêm option "Chỉ tính sổ chưa đáo hạn" | Liên kết 2 plan anh em |

### Math spec (fixed bugs từ review)

**Compound formula (D1 + D5):**

```
balance = 0
prev_date = null
for flow in flows_filtered.order_by(date):   # chỉ Deposit/Withdraw
    if prev_date != null:
        months = (flow.date - prev_date).days / (365 / 12)
        balance *= (1 + r/12)^months
    balance += flow.signed_amount
    prev_date = flow.date

# Final roll to AsOf
if prev_date != null:
    months = (asof - prev_date).days / (365 / 12)
    balance *= (1 + r/12)^months

return balance
```

**Verify với test case review yêu cầu**:
- 100M day 0, -100M day 180, r=6% → month 6 factor = `(1.005)^6 ≈ 1.03038` → balance sau deposit = 100M, sau 6 tháng = 103.038M, rút 100M → **3.038M ≠ 0** ✅ bug đã fix.

**Opportunity cost:**
```
opportunity_cost = actual_portfolio_value(asof) − hypothetical_balance(asof)
```

**Period return (< 1 năm)**:
```
actual_return_pct = (actual_value − sum(deposits)) / sum(deposits)
savings_return_pct = (hypothetical − sum(deposits)) / sum(deposits)
return_diff_pct = actual_return_pct − savings_return_pct
```

**Alpha annualized (≥ 1 năm)**:
```
cagr_actual = (actual_value / sum(deposits))^(365/days) − 1
alpha = cagr_actual − r
```

---

## Phase 1 — Backend: Bank Rate Scraper (TDD)

### 1.1 Viết test TRƯỚC (Red)

**File**: `tests/InvestmentApp.Infrastructure.Tests/Services/HmoneyBankRateProviderTests.cs` (tạo mới)

```csharp
public class HmoneyBankRateProviderTests
{
    [Fact]
    public async Task ParseHtml_Fixture_ReturnsTopRatePerTerm() { ... }

    [Fact]
    public async Task ParseHtml_MalformedTable_ThrowsOrReturnsEmpty() { ... }

    [Fact]
    public async Task GetTopRatesAsync_UsesCacheOnSecondCall() { ... }

    [Fact]
    public async Task GetTopRatesAsync_HttpFailure_FallsBackToStaleCache() { ... }

    [Fact]
    public async Task ParseHtml_PrefersOnlineTableOverCounter_WhenBothPresent() { ... }

    [Fact]
    public async Task ParseHtml_SkipsRowsWithDashRate() { ... }
}
```

Fixture HTML: download 1 snapshot từ `https://24hmoney.vn/lai-suat-gui-ngan-hang` → save vào `tests/InvestmentApp.Infrastructure.Tests/Fixtures/24hmoney-bank-rates.html`. Parse test đi qua `ParseHtml` public static method.

### 1.2 Implement (Green)

**Interface** — `src/InvestmentApp.Application/Common/Interfaces/IBankRateProvider.cs`:

```csharp
namespace InvestmentApp.Application.Common.Interfaces;

public interface IBankRateProvider
{
    /// <summary>
    /// Trả lãi suất cao nhất theo kỳ hạn từ 24hmoney (kênh online).
    /// Keys: 1, 3, 6, 9, 12 (tháng). Values: decimal (0.072 = 7.2%/năm).
    /// </summary>
    Task<BankRateSnapshot> GetTopRatesAsync(CancellationToken ct = default);
}

public record BankRateSnapshot(
    IReadOnlyDictionary<int, BankRateEntry> TopByTerm,
    DateTime SourceTimestamp,   // timestamp từ trang (dòng "23:59:59 25/03/2026")
    DateTime FetchedAt);

public record BankRateEntry(int TermMonths, decimal RatePercent, string BankName);
```

**Implementation** — `src/InvestmentApp.Infrastructure/Services/Hmoney/HmoneyBankRateProvider.cs`:

Mirror pattern `HmoneyGoldPriceProvider`:
- `HttpClient` injected qua `AddHttpClient<T>()` (UA `invest-mate-bank-rate-crawler/1.0`, timeout 30s).
- `IMemoryCache` dual-tier: fresh 6h, stale 24h.
- AngleSharp parse — 2 `<table>` (counter + online). Ưu tiên online (cao hơn 0.2-0.8%).
- Regex `\d{2}\s*tháng` extract term. Skip cells value `-`.
- `ParseHtmlAsync(string html)` public static → test được qua fixture.
- Error → fallback stale cache, log warning.

**Config** — `src/InvestmentApp.Api/appsettings.json`:

```json
"BankRateProvider": {
  "PageUrl": "{BankRateProvider__PageUrl}",
  "TimeoutSeconds": 30,
  "FreshCacheHours": 6,
  "StaleCacheHours": 24,
  "UserAgent": "invest-mate-bank-rate-crawler/1.0"
}
```

→ Deploy phải set env-var `BankRateProvider__PageUrl=https://24hmoney.vn/lai-suat-gui-ngan-hang`.

**DI registration** — `src/InvestmentApp.Api/Program.cs`:

```csharp
builder.Services.Configure<BankRateProviderOptions>(
    builder.Configuration.GetSection("BankRateProvider"));
builder.Services.AddHttpClient<HmoneyBankRateProvider>(client =>
{
    var cfg = builder.Configuration.GetSection("BankRateProvider");
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
    client.DefaultRequestHeaders.Add("User-Agent",
        cfg.GetValue<string>("UserAgent", "invest-mate-bank-rate-crawler/1.0"));
    client.Timeout = TimeSpan.FromSeconds(cfg.GetValue<int>("TimeoutSeconds", 30));
});
builder.Services.AddScoped<IBankRateProvider>(sp =>
    sp.GetRequiredService<HmoneyBankRateProvider>());
```

**Controller endpoint** — `src/InvestmentApp.Api/Controllers/AdvancedAnalyticsController.cs`:

```csharp
[HttpGet("bank-rates")]
[ProducesResponseType(typeof(BankRateSnapshot), StatusCodes.Status200OK)]
public async Task<IActionResult> GetBankRates(CancellationToken ct)
{
    var snapshot = await _bankRateProvider.GetTopRatesAsync(ct);
    return Ok(snapshot);
}
```

### 1.3 Verify

```bash
dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "HmoneyBankRateProviderTests"
```

---

## Phase 2 — Backend: Hypothetical Savings + Comparison Query (TDD)

### 2.1 Viết test TRƯỚC (Red)

**File**: `tests/InvestmentApp.Infrastructure.Tests/Services/HypotheticalSavingsReturnServiceTests.cs`

Tests (đã bổ sung theo critical review):

- `CalculateEndValue_SingleSeedDeposit_NoOtherFlows` — baseline trivial
- `CalculateEndValue_SingleDeposit_OneYear_FivePercent_MonthlyCompound` — assert `(1+0.05/12)^12 × 100M ≈ 105.116M`
- `CalculateEndValue_MultipleDeposits_RunningBalanceIterative` — 50M day 0, 50M day 182 → assert đúng compound iterative
- `CalculateEndValue_DepositThenFullWithdrawal_InterestEarnedNotZeroedOut` — **BUG-CATCHER**: 100M day 0, -100M day 180 at 6% → expect ≈ 3.03M (NOT 0)
- `CalculateEndValue_ZeroRate_EqualsSumOfFlows` — r=0 → end = sum(signed_amount)
- `CalculateEndValue_OnlyDividendFlows_ReturnsZero` — **BUG-CATCHER**: flows chỉ có Dividend → hypothetical = 0 (filter đúng)
- `CalculateEndValue_MixedFlowTypes_UsesOnlyDepositAndWithdraw` — assert filter đúng
- `CalculateEndValue_LeapYear_UsesCorrectDayCount` — 2024 có 366 ngày
- `CalculateEndValue_FlowOnSameDayAsAsOf_CountsWithZeroCompound` — boundary
- `CalculateEndValue_AsOfBetweenFlows_FutureFlowsIgnored`
- `BuildCurve_ReturnsPointsAtSnapshotDates_RunningBalance`

**File**: `tests/InvestmentApp.Application.Tests/Analytics/Queries/GetSavingsComparisonQueryHandlerTests.cs`

- `Handle_ValidPortfolio_ReturnsComparison`
- `Handle_UserSuppliedRate_OverridesDefault`
- `Handle_NoRateProvided_ComputesWeightedAvg`
- `Handle_NoSavingsAccountsOrAllNullRate_UsesFallback5Percent`
- `Handle_DaysLessThan365_AlphaIsNull_PeriodDiffPresent`
- `Handle_DaysGreaterThan365_AlphaNonNull`
- `Handle_NegativeRate_ThrowsValidation`
- `Handle_RateAbove50Percent_ThrowsValidation`
- `Handle_PortfolioOfDifferentUser_Throws`

### 2.2 Implement (Green)

**Interface** — `src/InvestmentApp.Application/Common/Interfaces/IHypotheticalSavingsReturnService.cs`:

```csharp
public interface IHypotheticalSavingsReturnService
{
    Task<decimal> CalculateEndValueAsync(
        string portfolioId, decimal annualRate, DateTime asOf, CancellationToken ct = default);
}
```

**Implementation** — `src/InvestmentApp.Infrastructure/Services/HypotheticalSavingsReturnService.cs`:

Dependencies: `ICapitalFlowRepository`.

```csharp
public async Task<decimal> CalculateEndValueAsync(
    string portfolioId, decimal annualRate, DateTime asOf, CancellationToken ct = default)
{
    var flows = (await _flowRepo.GetByPortfolioIdAsync(portfolioId, ct))
        .Where(f => f.FlowDate <= asOf
                 && (f.Type == CapitalFlowType.Deposit || f.Type == CapitalFlowType.Withdraw))   // D4
        .OrderBy(f => f.FlowDate)
        .ToList();

    if (flows.Count == 0) return 0m;

    decimal balance = 0m;
    DateTime? prevDate = null;
    var monthlyRate = annualRate / 12m;

    foreach (var flow in flows)
    {
        if (prevDate.HasValue)
        {
            var months = (decimal)((flow.FlowDate - prevDate.Value).TotalDays / (365.0 / 12.0));
            balance *= DecimalPow(1m + monthlyRate, months);
        }
        balance += flow.SignedAmount;
        prevDate = flow.FlowDate;
    }

    // roll đến asOf
    if (prevDate.HasValue)
    {
        var months = (decimal)((asOf - prevDate.Value).TotalDays / (365.0 / 12.0));
        balance *= DecimalPow(1m + monthlyRate, months);
    }

    return balance;
}

private static decimal DecimalPow(decimal baseVal, decimal exponent) =>
    (decimal)Math.Pow((double)baseVal, (double)exponent);
```

**Query** — `src/InvestmentApp.Application/Analytics/Queries/GetSavingsComparison/GetSavingsComparisonQuery.cs`:

```csharp
public class GetSavingsComparisonQuery : IRequest<SavingsComparisonDto>
{
    public string UserId { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public decimal? AnnualRate { get; set; }
    public DateTime? AsOf { get; set; }
}

public class SavingsComparisonDto
{
    public decimal ActualValue { get; set; }
    public decimal HypotheticalValue { get; set; }
    public decimal OpportunityCost { get; set; }
    public decimal OpportunityCostPercent { get; set; }
    public decimal UsedRate { get; set; }
    public string RateSource { get; set; } = null!;  // "user-savings-avg" / "fallback-5" / "manual" / "market-top-12m"
    public int SavingsAccountsCounted { get; set; }   // N in "N/M accounts có rate"
    public int SavingsAccountsTotal { get; set; }
    public IReadOnlyList<CurvePoint> ActualCurve { get; set; } = Array.Empty<CurvePoint>();

    // D10 — không khóa cứng: map hỗ trợ future VNINDEX
    public IReadOnlyList<FlowEvent> Flows { get; set; } = Array.Empty<FlowEvent>();  // cho FE recompute
    public decimal? CagrActual { get; set; }
    public decimal? AlphaAnnualized { get; set; }  // null khi days < 365
    public decimal? PeriodReturnDiff { get; set; }  // dùng khi days < 365
    public DateTime AsOf { get; set; }
    public DateTime FirstFlowDate { get; set; }
}

public record CurvePoint(DateTime Date, decimal Value);
public record FlowEvent(DateTime Date, decimal SignedAmount);  // FE recompute savings curve from this
```

**D6 — client-side recompute**: handler trả `Flows` + `ActualCurve` + `FirstFlowDate`. FE tự tính savingsCurve khi user kéo rate slider. Không gọi lại API.

**Default rate resolution** (handler):

```csharp
decimal usedRate;
string source;
int counted = 0, total = 0;

if (request.AnnualRate.HasValue) {
    usedRate = request.AnnualRate.Value;
    source = "manual";
}
else {
    var profile = await _profileRepo.GetByUserIdAsync(request.UserId, ct);
    var savings = profile?.Accounts
        .Where(a => a.Type == FinancialAccountType.Savings && a.Balance > 0m)
        .ToList() ?? new();
    total = savings.Count;
    var withRate = savings.Where(a => a.InterestRate.HasValue).ToList();
    counted = withRate.Count;
    var totalBalance = withRate.Sum(a => a.Balance);

    if (totalBalance > 0m) {
        usedRate = withRate.Sum(a => a.Balance * a.InterestRate!.Value / 100m) / totalBalance;
        source = "user-savings-avg";
    } else {
        usedRate = _config.GetValue<decimal>("SavingsComparison:DefaultFallbackRate", 0.05m);
        source = "fallback-5";
    }
}

if (usedRate < -0.1m || usedRate > 0.5m)
    throw new InvalidOperationException("Rate out of sanity range (-10% → 50%)");
```

### 2.3 Verify

```bash
dotnet test tests/InvestmentApp.Infrastructure.Tests
dotnet test tests/InvestmentApp.Application.Tests
dotnet test  # full, check no regression
```

---

## Phase 3 — Frontend (TDD)

### 3.1 Service — `frontend/src/app/core/services/advanced-analytics.service.ts`

```typescript
export interface SavingsComparisonDto {
  actualValue: number;
  hypotheticalValue: number;
  opportunityCost: number;
  opportunityCostPercent: number;
  usedRate: number;
  rateSource: 'user-savings-avg' | 'fallback-5' | 'manual' | 'market-top-12m';
  savingsAccountsCounted: number;
  savingsAccountsTotal: number;
  actualCurve: CurvePoint[];
  flows: FlowEvent[];
  cagrActual: number | null;
  alphaAnnualized: number | null;
  periodReturnDiff: number | null;
  asOf: string;
  firstFlowDate: string;
}

export interface BankRateSnapshot {
  topByTerm: Record<number, BankRateEntry>;   // key "12" → entry
  sourceTimestamp: string;
  fetchedAt: string;
}
export interface BankRateEntry { termMonths: number; ratePercent: number; bankName: string; }

getSavingsComparison(portfolioId: string, savingsRate?: number, asOf?: Date) { ... }
getBankRates(): Observable<BankRateSnapshot> { ... }
```

### 3.2 Component — Analytics page panel

**File**: `frontend/src/app/features/analytics/analytics.component.ts`

**Spec TRƯỚC** — extend/create `analytics.component.spec.ts`:

- `presetSelection "sổ của tôi" triggers reload without rate param`
- `presetSelection "cao nhất thị trường" calls getBankRates then applies 12T rate`
- `presetSelection "tự nhập" shows slider and defers network call until user confirms`
- `rateSliderChange: recomputes curve CLIENT-SIDE (no http call)` — verify no spy on http
- `alpha hidden when days < 365, periodReturnDiff shown instead`
- `opportunityCost between −2% and +2% shows neutral gray (NOT red/green)`
- `when rateSource === 'user-savings-avg': disclosure "N/M sổ có nhập lãi suất" visible`
- `disclaimer "Không phải lời khuyên đầu tư" always present in panel footer`

**Template** (inline, tiếng Việt có dấu đầy đủ):

```html
<section class="mt-6 bg-gray-800 rounded-xl p-4">
  <div class="flex items-center justify-between mb-3 flex-wrap gap-2">
    <h3 class="text-white text-sm font-semibold">So sánh với tiết kiệm</h3>

    <div class="flex items-center gap-1 text-xs">
      <button (click)="usePreset('my-savings')"
              [class.bg-blue-600]="ratePreset === 'my-savings'"
              class="px-3 py-1 rounded bg-gray-700 text-white">
        Sổ của tôi
      </button>
      <button (click)="usePreset('market-top')"
              [class.bg-blue-600]="ratePreset === 'market-top'"
              class="px-3 py-1 rounded bg-gray-700 text-white">
        Cao nhất thị trường
      </button>
      <button (click)="usePreset('manual')"
              [class.bg-blue-600]="ratePreset === 'manual'"
              class="px-3 py-1 rounded bg-gray-700 text-white">
        Tự nhập
      </button>
    </div>
  </div>

  <!-- Rate display + manual input -->
  <div class="flex items-center gap-3 mb-3 text-xs text-gray-300">
    <span>Lãi suất đang dùng:
      <span class="text-white font-semibold">{{ (displayRate * 100) | number:'1.2-2' }}%/năm</span>
    </span>
    <input *ngIf="ratePreset === 'manual'" type="number" min="0" max="30" step="0.1"
           [(ngModel)]="manualRatePercent"
           (ngModelChange)="onManualRateChange($event)"
           class="w-20 bg-gray-700 text-white rounded px-2 py-1" />
    <span *ngIf="rateSource === 'user-savings-avg'" class="text-gray-400">
      ({{ accountsCounted }}/{{ accountsTotal }} sổ có nhập lãi suất)
    </span>
    <span *ngIf="ratePreset === 'market-top'" class="text-amber-400" title="Chỉ tham khảo — bạn không thực sự được lãi suất này trừ khi chọn ngân hàng đó">
      ⚠ Chỉ tham khảo
    </span>
  </div>

  <!-- Metrics grid -->
  <div *ngIf="comparison as c" class="grid grid-cols-3 gap-3 mb-4">
    <div class="bg-gray-700 rounded p-3">
      <div class="text-[10px] text-gray-400">Danh mục thực tế</div>
      <div class="text-white text-lg font-semibold">{{ c.actualValue | vndCurrency }}</div>
    </div>
    <div class="bg-gray-700 rounded p-3">
      <div class="text-[10px] text-gray-400">Nếu gửi tiết kiệm</div>
      <div class="text-white text-lg font-semibold">{{ hypotheticalValue | vndCurrency }}</div>
    </div>
    <div class="rounded p-3"
         [ngClass]="diffColorClass(c.opportunityCostPercent)">
      <div class="text-[10px] text-gray-300">Chênh lệch</div>
      <div class="text-white text-lg font-semibold">
        {{ opportunityCost >= 0 ? '+' : '' }}{{ opportunityCost | vndCurrency }}
      </div>
      <div class="text-[10px] text-gray-300">
        ({{ opportunityCostPercent | number:'1.1-1' }}%)
      </div>
    </div>
  </div>

  <!-- Alpha (≥ 1 năm) HOẶC period diff (< 1 năm) -->
  <div *ngIf="c.alphaAnnualized !== null" class="text-xs text-gray-300 mb-3">
    <span title="Chênh lệch hiệu suất hàng năm giữa danh mục và tiết kiệm">
      Chênh lệch hiệu suất năm ⓘ:
    </span>
    <span class="font-semibold"
          [class.text-green-400]="(c.alphaAnnualized ?? 0) > 0.02"
          [class.text-red-400]="(c.alphaAnnualized ?? 0) < -0.02"
          [class.text-gray-400]="Math.abs(c.alphaAnnualized ?? 0) <= 0.02">
      {{ (c.alphaAnnualized ?? 0) * 100 | number:'1.1-1' }}%/năm
    </span>
  </div>
  <div *ngIf="c.alphaAnnualized === null && c.periodReturnDiff !== null" class="text-xs text-gray-400 mb-3">
    Dưới 1 năm — chỉ hiện chênh lệch tổng kỳ: {{ (c.periodReturnDiff ?? 0) * 100 | number:'1.1-1' }}%
  </div>

  <canvas #comparisonChart class="w-full" style="max-height: 280px"></canvas>

  <div class="text-[10px] text-gray-500 mt-2 italic">
    * Đây không phải là lời khuyên đầu tư. Kết quả mang tính tham khảo.
  </div>
</section>
```

**Client-side recompute** (D6):

```typescript
recomputeSavingsCurve(): void {
  // Tính savingsCurve từ this.comparison.flows + this.displayRate tại mỗi actualCurve date
  if (!this.comparison) return;
  const flows = this.comparison.flows;
  const monthlyRate = this.displayRate / 12;
  const depositWithdrawOnly = flows; // backend đã filter

  this.savingsCurve = this.comparison.actualCurve.map(pt => {
    const asOf = new Date(pt.date);
    let balance = 0;
    let prevDate: Date | null = null;
    for (const f of depositWithdrawOnly) {
      const fDate = new Date(f.date);
      if (fDate > asOf) break;
      if (prevDate) {
        const months = (fDate.getTime() - prevDate.getTime()) / (1000 * 60 * 60 * 24) / (365/12);
        balance *= Math.pow(1 + monthlyRate, months);
      }
      balance += f.signedAmount;
      prevDate = fDate;
    }
    if (prevDate) {
      const months = (asOf.getTime() - prevDate.getTime()) / (1000 * 60 * 60 * 24) / (365/12);
      balance *= Math.pow(1 + monthlyRate, months);
    }
    return { date: pt.date, value: balance };
  });

  this.hypotheticalValue = this.savingsCurve.length ? this.savingsCurve[this.savingsCurve.length - 1].value : 0;
  this.opportunityCost = this.comparison.actualValue - this.hypotheticalValue;
  this.opportunityCostPercent = this.hypotheticalValue > 0
    ? (this.opportunityCost / this.hypotheticalValue) * 100 : 0;

  this.renderChart();
}

diffColorClass(pct: number): string {
  if (Math.abs(pct) <= 2) return 'bg-gray-700';  // neutral khi |Δ| ≤ 2%
  return pct > 0 ? 'bg-green-900' : 'bg-red-900';
}
```

### 3.3 Dashboard badge

File: `frontend/src/app/features/dashboard/dashboard.component.ts`

```html
<div *ngIf="vsSavings as vs" class="mt-2 text-xs">
  <span class="text-gray-400">vs. tiết kiệm ({{ vs.usedRate * 100 | number:'1.1-1' }}%):</span>
  <span class="font-semibold ml-1"
        [class.text-green-400]="vs.opportunityCostPercent > 2"
        [class.text-red-400]="vs.opportunityCostPercent < -2"
        [class.text-gray-300]="Math.abs(vs.opportunityCostPercent) <= 2">
    {{ vs.opportunityCostPercent > 0 ? '+' : '' }}{{ vs.opportunityCostPercent | number:'1.1-1' }}%
  </span>
  <a [routerLink]="['/analytics']" [queryParams]="{scrollTo: 'savings-comparison'}"
     class="ml-2 text-blue-400 hover:underline">Chi tiết →</a>
</div>
```

### 3.4 Verify

- `ng test --watch=false` all green.
- Manual golden path:
  1. `/analytics` → panel render với preset "Sổ của tôi" default.
  2. Click "Cao nhất thị trường" → hiện ⚠ tooltip + rate đổi sang top 12T từ scraper.
  3. Click "Tự nhập" → slider hiện, đổi rate → **chart cập nhật tức thì, không loading spinner** (client-side).
  4. Portfolio < 1 năm → Alpha ẩn, period diff hiện.
  5. Opportunity cost ≈ 0 → badge gray, không red/green.
  6. Dashboard → badge hiển thị đúng chiều, click link → scroll tới panel.

---

## Phase 4 — Documentation (bắt buộc trước commit)

- [ ] `docs/business-domain.md` — thêm section "Analytics / Savings Comparison" + entity `BankRateSnapshot`
- [ ] `docs/features.md` — bullet mới dưới Analytics
- [ ] `docs/architecture.md` — thêm 2 service (`HmoneyBankRateProvider`, `HypotheticalSavingsReturnService`) + 2 endpoint
- [ ] `docs/project-context.md` — **log D2/D3/D8** (default rate anchor, best-rate is toggle-only, analytics primary home) — **thiếu trong v1, thêm v2**
- [ ] `frontend/src/assets/CHANGELOG.md` — version entry
- [ ] `frontend/src/assets/docs/phan-tich-hieu-suat.md` (check exists, else tạo) — hướng dẫn đọc alpha, ý nghĩa opportunity cost, disclaimer
- [ ] `help.service.ts` topic registry — register topic "so-sanh-voi-tiet-kiem" nếu pattern cần
- [ ] `CLAUDE.md` — **không cần** (không thêm directive/pipe/convention)

### Env-var cần setup trước deploy (memory: `feedback_config_placeholder_convention.md`)

```
BankRateProvider__PageUrl=https://24hmoney.vn/lai-suat-gui-ngan-hang
SavingsComparison__DefaultFallbackRate=0.05
```

Cloud Build / Cloud Run deploy cần inject 2 biến này. Thêm vào runbook deploy.

---

## Checklist tổng (cho session implement)

### Backend — Phase 1 (Scraper)
- [ ] Fixture `tests/.../Fixtures/24hmoney-bank-rates.html` (download snapshot)
- [ ] Test `HmoneyBankRateProviderTests` Red
- [ ] `IBankRateProvider` + `BankRateSnapshot` record
- [ ] `HmoneyBankRateProvider` (mirror `HmoneyGoldPriceProvider`)
- [ ] `BankRateProviderOptions` + appsettings placeholder `{BankRateProvider__PageUrl}`
- [ ] DI registration (HttpClient + scoped service)
- [ ] Endpoint `GET /api/v1/analytics/bank-rates`
- [ ] Tests green

### Backend — Phase 2 (Hypothetical + Query)
- [ ] Test `HypotheticalSavingsReturnServiceTests` Red — BUG-CATCHERs phải có
- [ ] `IHypotheticalSavingsReturnService` + impl (running balance, monthly compound, filter Deposit/Withdraw)
- [ ] Test `GetSavingsComparisonQueryHandlerTests` Red
- [ ] Query + DTO (bao gồm `Flows[]` cho FE recompute) + Handler
- [ ] Endpoint `GET /api/v1/analytics/portfolio/{portfolioId}/vs-savings`
- [ ] Full `dotnet test` green

### Frontend
- [ ] Service methods `getSavingsComparison` + `getBankRates` + DTOs
- [ ] `analytics.component.spec.ts` Red — 8 cases đã list
- [ ] Panel template với 3-preset rate picker
- [ ] Client-side `recomputeSavingsCurve()` method
- [ ] Chart.js 2-line overlay reuse pattern
- [ ] Alpha hidden khi < 365 ngày, period diff thay thế
- [ ] Neutral gray khi |Δ| ≤ 2%
- [ ] Disclosure N/M accounts
- [ ] Disclaimer footer
- [ ] Dashboard badge
- [ ] `ng test` green + manual golden path

### Docs
- [ ] 6 file docs update (list Phase 4)
- [ ] Env-var 2 biến ghi vào runbook deploy

### Commit & review
- [ ] Run `/code-review` sub-agent trước push — **bắt buộc** (memory `feedback_code_review_mandatory.md`). Triage Critical/Major, fix rồi mới tạo PR.
- [ ] Commit message: `feat(analytics): add investment vs savings opportunity-cost comparison with 24hmoney bank rate scraper`
- [ ] PR body: liệt kê 3 phase + disclaimer features + env-vars cần setup

---

## Edge cases & bug-catchers (từ critical review)

| Case | Expected behavior | Bug nó catch |
|---|---|---|
| Deposit 100M day 0, withdraw 100M day 180, r=6% | Hypothetical ≈ 3.03M (NOT 0) | Running balance iterative (D5) |
| Flow type = Dividend only | Hypothetical = 0 | Filter Deposit/Withdraw (D4) |
| Leap year (2024 = 366 days) | Dùng actual day count | Day-count edge |
| Flow cùng ngày AsOf | Compound factor = 1, count vào | Boundary |
| Flow sau AsOf | Bỏ qua | Time filter |
| `days < 365` | Alpha = null, PeriodReturnDiff hiện | D7 alpha rule |
| All Savings rate null | Fallback 5%, disclose 0/M | D2 fallback |
| Rate > 50%/năm | 400 validation | Sanity cap |
| `|Δ| ≤ 2%` | Badge neutral gray | UX anti-anxiety |

---

## Hooks tương lai (D10 + D11)

- **Thêm VNINDEX benchmark** (roadmap): DTO đã có pattern `flows` + `actualCurve`; thêm method `buildVnindexCurve()` ở FE + endpoint backend trả VNINDEX time series — không cần đổi DTO.
- **Tích hợp với `savings-term-dates.md`**: khi MaturityDate land, rate picker thêm preset thứ 4 "Đến ngày đáo hạn gần nhất" — auto-set `asOf = min(savings.maturityDate)`.
- **Multi-rate bands**: nếu cần so sánh theo kỳ hạn khác nhau, backend có thể trả `rateSchedule: [{from, to, rate}]` thay vì scalar — DTO vẫn tương thích.

---

## Tham chiếu file

Pattern reference:
- [src/InvestmentApp.Infrastructure/Services/Hmoney/HmoneyGoldPriceProvider.cs](../../src/InvestmentApp.Infrastructure/Services/Hmoney/HmoneyGoldPriceProvider.cs) — copy pattern
- [src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs](../../src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs) — pattern math service
- [src/InvestmentApp.Infrastructure/Services/PerformanceMetricsService.cs](../../src/InvestmentApp.Infrastructure/Services/PerformanceMetricsService.cs) — pattern CAGR/Sharpe

Target files:
- [src/InvestmentApp.Domain/Entities/CapitalFlow.cs](../../src/InvestmentApp.Domain/Entities/CapitalFlow.cs)
- [src/InvestmentApp.Domain/Entities/PortfolioSnapshotEntity.cs](../../src/InvestmentApp.Domain/Entities/PortfolioSnapshotEntity.cs)
- [src/InvestmentApp.Api/Controllers/AdvancedAnalyticsController.cs](../../src/InvestmentApp.Api/Controllers/AdvancedAnalyticsController.cs)
- [src/InvestmentApp.Api/Program.cs](../../src/InvestmentApp.Api/Program.cs) — DI registration
- [src/InvestmentApp.Api/appsettings.json](../../src/InvestmentApp.Api/appsettings.json) — new section
- [frontend/src/app/features/analytics/analytics.component.ts](../../frontend/src/app/features/analytics/analytics.component.ts)
- [frontend/src/app/features/dashboard/dashboard.component.ts](../../frontend/src/app/features/dashboard/dashboard.component.ts) — lines 638-657 area
- [frontend/src/app/core/services/advanced-analytics.service.ts](../../frontend/src/app/core/services/advanced-analytics.service.ts)

Plan liên quan:
- [savings-term-dates.md](savings-term-dates.md) — hook D11
- [done/p3-twr-mwr-cagr-fix.md](done/p3-twr-mwr-cagr-fix.md) — math conventions
- [done/personal-finance.md](done/personal-finance.md) — context Personal Finance

External source (user confirmed):
- https://24hmoney.vn/lai-suat-gui-ngan-hang — primary scrape target, SSR daily refresh

Memory references áp dụng:
- `feedback_always_update_all_docs.md` — Phase 4 covers user guide
- `feedback_config_placeholder_convention.md` — env-var setup trước deploy
- `feedback_code_review_mandatory.md` — `/code-review` trước commit
- `learning_toolquirk_24hmoney_gold_format.md` — same host, same pattern
