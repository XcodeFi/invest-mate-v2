# Kế hoạch: Tài chính cá nhân (Personal Finance)

> **Trạng thái:** 📋 Planned — sẵn sàng ship
> **Ưu tiên:** Cao — Tier 3
> **Dự kiến:** ~28 files (20 new, 5 modified, 4 docs), chia **6 ship cycle** (xem Section 8)
> **Scope highlight:** Có tích hợp crawler giá vàng 24hmoney (Phase 3) — rủi ro cao nhất, tách riêng

---

## Bối cảnh

App hiện chỉ quản lý dòng vốn **trong** danh mục đầu tư (Capital Flows). Thiếu bức tranh tổng thể:
- Tổng tài sản bao nhiêu? Bao nhiêu đang đầu tư, bao nhiêu gửi ngân hàng?
- Quỹ dự phòng đã đủ 6 tháng chi tiêu chưa?
- Tiền nhàn rỗi có thể đầu tư thêm là bao nhiêu?
- Có đang vi phạm nguyên tắc phân bổ tài sản không?

**Scope:** Tổng quan tài sản + Nguyên tắc tài chính (không quản lý chi tiêu)
**Vị trí:** Widget trên Dashboard + trang riêng `/personal-finance`

---

## 1. Domain Entity

**File mới:** `src/InvestmentApp.Domain/Entities/FinancialProfile.cs`

```
FinancialProfile : AggregateRoot (per-user, 1:1)
├── UserId (unique)
├── MonthlyExpense (decimal, >0) — chi tiêu trung bình/tháng
├── Accounts: List<FinancialAccount> (embedded)
│   ├── Id (GUID), Type, Name, Balance, InterestRate?, Note?, UpdatedAt
│   ├── Type: Securities | Savings | Emergency | IdleCash | Gold
│   └── Gold-only fields (nullable): GoldBrand, GoldType, GoldQuantity (lượng)
│       ├── GoldBrand enum: SJC | DOJI | PNJ | Other
│       └── GoldType  enum: Mieng | Nhan   (chỉ 2 loại — tài sản tích trữ)
├── Rules: FinancialRules (embedded value object)
│   ├── EmergencyFundMonths (default: 6)
│   ├── MaxInvestmentPercent (default: 50%) — bao gồm CK + Vàng
│   └── MinSavingsPercent (default: 30%)
├── CreatedAt, UpdatedAt
└── Methods:
    ├── Create(userId, monthlyExpense) → tạo 4 tài khoản mặc định (Gold tạo on-demand khi user có nhu cầu)
    ├── UpdateMonthlyExpense(amount)
    ├── UpdateRules(partial update)
    ├── UpsertAccount(...) — thêm/sửa tài khoản
    ├── RemoveAccount(id) — không cho xóa tài khoản Securities cuối cùng
    ├── GetTotalAssets(securitiesValue) → tổng = sum(balances) + securitiesValue
    └── CalculateHealthScore(securitiesValue) → 0-100 điểm
```

### 5 loại tài khoản

| Type | Tên tiếng Việt | Mô tả | Ghi chú |
|------|---------------|-------|---------|
| Securities | Chứng khoán | Giá trị danh mục đầu tư | Auto-sync từ PnLService, không nhập tay |
| Savings | Tiết kiệm | Gửi ngân hàng | Có lãi suất (interestRate) |
| Emergency | Dự phòng | Quỹ khẩn cấp | Không đụng vào trừ khẩn cấp |
| IdleCash | Nhàn rỗi | Tiền sẵn sàng đầu tư | Có thể chuyển vào portfolio |
| Gold | Vàng | Tài sản tích trữ — chỉ 2 loại: **Vàng miếng** (SJC/DOJI/PNJ/Other) và **Vàng nhẫn** (SJC/DOJI/PNJ/Other). Bỏ qua nữ trang/trang sức | User nhập `GoldQuantity` (lượng) + chọn `GoldBrand` + `GoldType` → backend auto-calc `Balance = quantity × giá Bán ra` từ `IGoldPriceProvider` (crawl 24hmoney). Fallback: nếu để trống 3 field Gold thì nhập tay `Balance`. User có thể tạo nhiều Gold account (mỗi combo brand+type = 1 account) |

### Health Score (0-100)

| Nguyên tắc | Default | Điểm trừ tối đa | Công thức |
|------------|---------|-----------------|-----------|
| Quỹ dự phòng ≥ N tháng chi tiêu | 6 tháng | -40 | `emergencyTotal >= monthlyExpense × N` |
| Đầu tư ≤ N% tổng tài sản | 50% | -30 | `(securitiesValue + goldTotal) <= totalAssets × N%` — vàng tính vào đầu tư theo định nghĩa user. Default 50% (trước là 40%) để phản ánh thực tế người Việt thường giữ vàng song song CK |
| Tiết kiệm ≥ N% tổng tài sản | 30% | -30 | `savingsTotal >= totalAssets × N%` — chỉ bank savings, không gộp vàng |

Điểm trừ **tỷ lệ thuận** với mức vi phạm (thiếu 50% quỹ dự phòng → trừ 20/40 điểm). Clamp [0, 100].

Pattern tham chiếu: `AiSettings.cs` (per-user 1:1), `RiskProfile.cs` (configurable thresholds)

---

## 2. Application Layer (CQRS)

### Repository Interface
**Sửa:** `src/InvestmentApp.Application/RepositoryInterfaces.cs`
```csharp
public interface IFinancialProfileRepository : IRepository<FinancialProfile>
{
    Task<FinancialProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task UpsertAsync(FinancialProfile profile, CancellationToken ct = default);
}
```

### DTOs
**Mới:** `src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceDto.cs`
- `FinancialProfileDto`, `FinancialAccountDto`, `FinancialRulesDto`
- `NetWorthSummaryDto`: totalAssets, securitiesValue, goldTotal, savingsTotal, emergencyTotal, idleCashTotal, monthlyExpense, healthScore, ruleChecks[], accounts[]
- `RuleCheckResultDto`: ruleName, isPassing, description, currentValue, requiredValue

### Commands

| File | Input | Logic |
|------|-------|-------|
| `UpsertFinancialProfileCommand.cs` | monthlyExpense, rules? | Get-or-create pattern, upsert |
| `UpsertFinancialAccountCommand.cs` | accountId?, type, name, balance?, interestRate?, goldBrand?, goldType?, goldQuantity? | Load profile → nếu Type=Gold + đủ 3 gold fields → fetch price → auto-calc Balance → UpsertAccount(). Else dùng Balance truyền vào. |
| `RemoveFinancialAccountCommand.cs` | accountId | Load profile → RemoveAccount() |

### Queries

| File | Output | Dependencies |
|------|--------|-------------|
| `GetFinancialProfileQuery.cs` | FinancialProfileDto? | IFinancialProfileRepository |
| `GetNetWorthSummaryQuery.cs` | NetWorthSummaryDto | IFinancialProfileRepository, IPortfolioRepository, IPnLService |

`GetNetWorthSummary` handler: fetch all user portfolios → sum TotalMarketValue via PnLService → pass to entity `CalculateHealthScore()`

---

## 3. Infrastructure + API

### Repository
**Mới:** `src/InvestmentApp.Infrastructure/Repositories/FinancialProfileRepository.cs`
- Collection: `financial_profiles`
- Unique index on UserId
- Pattern: giống `AiSettingsRepository.cs`

### Gold Price Provider (mới)

**Interface (Application):** `src/InvestmentApp.Application/Common/Interfaces/IGoldPriceProvider.cs`

```csharp
public interface IGoldPriceProvider
{
    Task<IReadOnlyList<GoldPriceDto>> GetPricesAsync(CancellationToken ct = default);
    Task<GoldPriceDto?> GetPriceAsync(GoldBrand brand, GoldType type, CancellationToken ct = default);
}

public record GoldPriceDto(GoldBrand Brand, GoldType Type, decimal BuyPrice, decimal SellPrice, DateTime UpdatedAt);
```

**Implementation (Infrastructure):** `src/InvestmentApp.Infrastructure/Services/Hmoney/HmoneyGoldPriceProvider.cs`

- Nguồn: 24hmoney, trang `24hmoney.vn/gia-vang`
- **⚠️ Investigation needed khi implement**: inspect Network tab để tìm JSON endpoint thực tế (nhiều khả năng dạng `api-finance-t19.24hmoney.vn/...`). Nếu không có JSON API → HTML scrape fallback (AngleSharp).
- Filter: **chỉ parse `vàng miếng` + `vàng nhẫn`**. Bỏ qua `vàng nữ trang`, `vàng trang sức`.
- Brand mapping: `SJC`, `DOJI`, `PNJ` → enum tương ứng; `Bảo Tín Minh Châu` và các hãng khác → `Other`.
- Unit: API 24hmoney có thể trả giá ở đơn vị khác (triệu VNĐ/lượng vs VND/lượng). **Kiểm tra và normalize về VND/lượng** — tương tự quirk ×1000 của giá CP 24hmoney.
- Cache: `IMemoryCache`, TTL **5 phút** (vàng update chậm).
- Pattern tham chiếu: `HmoneyMarketDataProvider.cs` (HttpClient + cache + exception handling).
- Fallback: crawler fail → return last cached value + log warning. User vẫn có thể nhập tay Balance.

**Balance auto-calc logic (trong `UpsertFinancialAccount` handler):**
```
if (Type == Gold && GoldQuantity.HasValue && GoldBrand.HasValue && GoldType.HasValue):
    price = await goldPriceProvider.GetPriceAsync(GoldBrand, GoldType)
    Balance = GoldQuantity × price.SellPrice   // giá Bán ra = giá user nhận nếu thanh lý
else:
    Balance = user-provided value (manual)
```

### Controller
**Mới:** `src/InvestmentApp.Api/Controllers/PersonalFinanceController.cs`
- Route: `api/v1/personal-finance`, JWT auth

| Method | Endpoint | Handler |
|--------|----------|---------|
| GET | `/` | GetFinancialProfileQuery |
| GET | `/summary` | GetNetWorthSummaryQuery |
| GET | `/gold-prices` | GetGoldPricesQuery → `IGoldPriceProvider.GetPricesAsync()` (public reference, dùng hiển thị dropdown + live price khi user tạo Gold account) |
| PUT | `/` | UpsertFinancialProfileCommand |
| PUT | `/accounts` | UpsertFinancialAccountCommand |
| DELETE | `/accounts/{accountId}` | RemoveFinancialAccountCommand |

### DI Registration
**Sửa:** `src/InvestmentApp.Api/Program.cs` — thêm `IFinancialProfileRepository`, `IGoldPriceProvider` → `HmoneyGoldPriceProvider`

---

## 4. Frontend

### Service
**Mới:** `frontend/src/app/core/services/personal-finance.service.ts`

Interfaces: `FinancialProfile`, `FinancialAccount` (thêm optional `goldBrand`, `goldType`, `goldQuantity`), `FinancialRules`, `NetWorthSummary`, `RuleCheckResult`, `GoldPrice` (brand, type, buyPrice, sellPrice, updatedAt)

Methods:
- `getProfile(): Observable<FinancialProfile | null>`
- `getSummary(): Observable<NetWorthSummary>`
- `getGoldPrices(): Observable<GoldPrice[]>` — cho dropdown chọn brand+type trong form Gold account
- `upsertProfile(data): Observable<FinancialProfile>`
- `upsertAccount(data): Observable<FinancialProfile>`
- `removeAccount(accountId): Observable<void>`

### Dashboard Widget
**Sửa:** `frontend/src/app/features/dashboard/dashboard.component.ts`

```
┌─────────────────────────────────────────────────┐
│ 💰 Tài chính cá nhân         Xem chi tiết →    │
│ ───────────────────────────────────────────────  │
│ Tổng tài sản: 500,000,000 VND                  │
│                                                  │
│ [======== Health Bar 75/100 ========]            │
│                                                  │
│ 📈 CK: 200tr  🪙 Vàng: 80tr                    │
│ 🏦 TK: 150tr  🛡️ DP: 100tr  💵 NR: 50tr         │
└─────────────────────────────────────────────────┘
```

Nếu chưa có profile → hiện "Thiết lập quản lý tài chính cá nhân" + nút [Bắt đầu]

Pattern: giống Daily Routine widget trên Dashboard

### Trang riêng `/personal-finance`
**Mới:** `frontend/src/app/features/personal-finance/personal-finance.component.ts`

| Section | Nội dung |
|---------|----------|
| Header | "Tài chính cá nhân" |
| Net Worth Cards (grid-cols-5) | Tổng tài sản, Chứng khoán (auto), Vàng, Tiết kiệm+Dự phòng, Nhàn rỗi |
| Health Score Bar | 0-100, xanh/vàng/đỏ (pattern risk-dashboard) |
| Rule Compliance | Progress bars cho từng nguyên tắc, pass/fail |
| Accounts Management | CRUD cards, inline edit balance/interestRate. **Form Gold**: dropdown Brand (SJC/DOJI/PNJ/Other) + Type (Miếng/Nhẫn) + input Quantity (lượng) → hiển thị live giá Bán ra + Balance auto-calc. Toggle "Nhập tay Balance" để bypass auto-calc nếu cần. |
| Settings | Chi phí hàng tháng + ngưỡng nguyên tắc |

### Navigation
- **Sửa:** `frontend/src/app/app.routes.ts` — thêm route `/personal-finance`
- **Sửa:** header component — thêm "Tài chính cá nhân" vào group Quản lý

---

## 5. Liên kết với app hiện tại

```
Personal Finance
  ├── Dashboard → Widget "Tổng tài sản ròng" + Health Score
  ├── Trade Plan → Context: "Tiền nhàn rỗi sẵn sàng đầu tư: X VND"
  ├── Risk Dashboard → Cảnh báo "Đang đầu tư > 40% tổng tài sản"
  ├── Capital Flows → Nạp tiền vào portfolio = chuyển từ "Nhàn rỗi" → "Chứng khoán"
  └── AI Prompts → Context tài chính tổng thể cho AI advisor
```

---

## 6. TDD Test Plan

### Phase 1: Domain Tests (viết TRƯỚC)
**Mới:** `tests/InvestmentApp.Domain.Tests/Entities/FinancialProfileTests.cs`
- Create: defaults (4 accounts, rules), null userId throw, unique IDs
- UpdateMonthlyExpense: valid/invalid, version increment
- UpdateRules: partial update
- UpsertAccount: new/existing, Savings có interestRate, Gold không có interestRate (reject nếu set)
- UpsertAccount Gold: hợp lệ với 3 field (brand+type+quantity) hoặc với Balance thuần. Thiếu 1 trong 3 Gold field mà không có Balance → throw.
- RemoveAccount: valid/invalid/last Securities throws, Gold xóa được bất kỳ lúc nào
- CalculateHealthScore: all pass=100, each fail deducts proportionally, clamp [0,100]
- CalculateHealthScore với Gold: gold cộng dồn vào investment total (cùng Securities), KHÔNG cộng vào savings total
- **HmoneyGoldPriceProvider parse test**: feed fixture HTML/JSON copy từ 24hmoney → verify filter đúng (Miếng + Nhẫn only, Nữ trang/Trang sức bị loại), brand mapping đúng, unit normalize đúng (VND/lượng)

### Phase 2: Application Tests
- `UpsertFinancialProfileCommandTests.cs` — new/existing user
- `GetNetWorthSummaryQueryTests.cs` — with profile + multi-portfolio, without profile
- `UpsertFinancialAccountCommandTests.cs` — new/existing account

---

## 7. Files Summary

| Type | Count |
|------|-------|
| New backend | 12 (entity, 3 commands, 3 queries incl. GetGoldPrices, DTOs, repo, controller, IGoldPriceProvider interface, HmoneyGoldPriceProvider impl) |
| New frontend | 2 (service, component) |
| New tests | 5 (domain + 3 application + HmoneyGoldPriceProvider parse test with fixture HTML/JSON) |
| Modified | 5 (RepositoryInterfaces, Program.cs, routes, dashboard, header) |
| Docs | 4 (architecture, business-domain, features, CHANGELOG) |
| **Total** | **~28 files** |

---

## 8. Shipping Phases (6 ship cycles)

Mỗi phase = **1 ship cycle riêng** = **1 PR độc lập** (merge vào master trước khi phase kế tiếp start). Giữa các phase → viết checkpoint ở Section 9.

Lý do chia 6: (1) mỗi PR review được trong <30 phút, (2) phase sau tái dùng artifact phase trước qua git, (3) rủi ro cô lập — nếu crawler fail thì FE đã có data mock tests, v.v.

---

### Phase 1 — Domain

**Scope:** Entity `FinancialProfile` + `FinancialAccount` + `FinancialRules` + 3 enums + Domain tests. Pure logic, không touch DB/API.

**Files (~7):**
- `src/InvestmentApp.Domain/Entities/FinancialProfile.cs` — aggregate root
- `src/InvestmentApp.Domain/Entities/FinancialAccount.cs` — embedded (đã thêm Gold fields)
- `src/InvestmentApp.Domain/Entities/FinancialRules.cs` — value object
- `src/InvestmentApp.Domain/Entities/FinancialAccountType.cs` — enum 5 values
- `src/InvestmentApp.Domain/Entities/GoldBrand.cs` — enum SJC/DOJI/PNJ/Other
- `src/InvestmentApp.Domain/Entities/GoldType.cs` — enum Mieng/Nhan
- `tests/InvestmentApp.Domain.Tests/Entities/FinancialProfileTests.cs` — ~18 tests

**Done criteria:**
- [ ] `dotnet test tests/InvestmentApp.Domain.Tests` all pass
- [ ] Test coverage: Create defaults, UpdateMonthlyExpense validation, UpdateRules partial, UpsertAccount (5 types incl. Gold 3-field validation), RemoveAccount (last Securities throw), CalculateHealthScore (pass=100, proportional deduct, clamp [0,100], gold cộng vào investment)

**Dependencies:** None
**Branch:** `feat/personal-finance-domain` off `master`

---

### Phase 2 — Application

**Scope:** CQRS handlers + DTOs + interfaces + Application tests. Không touch Infrastructure thực — chỉ mock.

**Files (~12):**
- `src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceDto.cs` — `FinancialProfileDto`, `FinancialAccountDto`, `FinancialRulesDto`, `NetWorthSummaryDto`, `RuleCheckResultDto`, `GoldPriceDto`
- `src/InvestmentApp.Application/PersonalFinance/Commands/UpsertFinancialProfileCommand.cs`
- `src/InvestmentApp.Application/PersonalFinance/Commands/UpsertFinancialAccountCommand.cs` — handler chứa Gold auto-calc (fetch `IGoldPriceProvider` khi có đủ 3 Gold field)
- `src/InvestmentApp.Application/PersonalFinance/Commands/RemoveFinancialAccountCommand.cs`
- `src/InvestmentApp.Application/PersonalFinance/Queries/GetFinancialProfileQuery.cs`
- `src/InvestmentApp.Application/PersonalFinance/Queries/GetNetWorthSummaryQuery.cs`
- `src/InvestmentApp.Application/PersonalFinance/Queries/GetGoldPricesQuery.cs` — proxy tới `IGoldPriceProvider.GetPricesAsync()`
- `src/InvestmentApp.Application/Common/Interfaces/IGoldPriceProvider.cs` — interface
- `src/InvestmentApp.Application/RepositoryInterfaces.cs` — thêm `IFinancialProfileRepository`
- `tests/InvestmentApp.Application.Tests/PersonalFinance/UpsertFinancialProfileCommandTests.cs`
- `tests/InvestmentApp.Application.Tests/PersonalFinance/UpsertFinancialAccountCommandTests.cs` — mock `IGoldPriceProvider`, verify Balance auto-calc + manual fallback
- `tests/InvestmentApp.Application.Tests/PersonalFinance/GetNetWorthSummaryQueryTests.cs` — multi-portfolio, no-profile cases

**Done criteria:**
- [ ] `dotnet test tests/InvestmentApp.Application.Tests` all pass
- [ ] Gold auto-calc test: Mock `IGoldPriceProvider` → `Balance = quantity × sellPrice`
- [ ] Manual fallback test: không có 3-Gold-field → dùng `Balance` truyền vào
- [ ] NetWorthSummary: gold cộng vào `securitiesValue + goldTotal` cho rule MaxInvestment

**Dependencies:** Phase 1 merged
**Branch:** `feat/personal-finance-application` off `master`

---

### Phase 3 — Infrastructure: Gold Price Crawler

**Scope:** 24hmoney crawler (investigation + impl + parse test). Tách riêng vì có rủi ro cao nhất (external API, format unknown).

**Pre-work (BẮT BUỘC trước khi code):**
1. Chrome DevTools → nav `24hmoney.vn/gia-vang` → Network tab → filter Fetch/XHR
2. Tìm JSON endpoint (expected pattern: `api-finance-t19.24hmoney.vn/v2/...gold...`)
3. Nếu không có JSON → save full HTML response, chuẩn bị AngleSharp (NuGet package)
4. Check response: giá là `VND/lượng` hay `triệu VND/lượng` — normalize về VND/lượng
5. Save sample response vào `tests/InvestmentApp.Infrastructure.Tests/Fixtures/hmoney_gold_response.json` (hoặc `.html`)
6. Commit fixture file first → sau đó code provider

**Files (~3):**
- `src/InvestmentApp.Infrastructure/Services/Hmoney/HmoneyGoldPriceProvider.cs`
- `tests/InvestmentApp.Infrastructure.Tests/Services/HmoneyGoldPriceProviderTests.cs`
- `tests/InvestmentApp.Infrastructure.Tests/Fixtures/hmoney_gold_response.{json,html}`

**Done criteria:**
- [ ] `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter HmoneyGoldPriceProvider` pass
- [ ] Parse test: filter chỉ Miếng+Nhẫn (reject Nữ trang/Trang sức), brand mapping (SJC/DOJI/PNJ/Other), unit normalized VND/lượng
- [ ] Smoke test bằng tay: chạy provider hit live 24hmoney, log kết quả, verify 4 brand × 2 type = 8 entries (tối thiểu SJC+DOJI+PNJ có data)
- [ ] Cache TTL 5 phút verify (gọi 2 lần liên tiếp → HTTP call 1 lần)

**Dependencies:** Phase 2 merged (cần `IGoldPriceProvider` interface)
**Branch:** `feat/personal-finance-gold-crawler` off `master`

---

### Phase 4 — Infrastructure: Repository + API Controller + DI

**Scope:** MongoDB repo + API controller + Program.cs wiring. Nhỏ nhưng critical — nơi hợp toàn bộ backend.

**Files (~3 + 1 modify):**
- `src/InvestmentApp.Infrastructure/Repositories/FinancialProfileRepository.cs` — collection `financial_profiles`, unique index UserId
- `src/InvestmentApp.Api/Controllers/PersonalFinanceController.cs` — 6 endpoints
- `src/InvestmentApp.Api/Program.cs` — DI: `IFinancialProfileRepository`, `IGoldPriceProvider` → `HmoneyGoldPriceProvider`

**Done criteria:**
- [ ] 6 endpoints hoạt động qua Swagger với JWT auth: `GET /`, `GET /summary`, `GET /gold-prices`, `PUT /`, `PUT /accounts`, `DELETE /accounts/{id}`
- [ ] Unique index UserId enforced (tạo 2 profile cho cùng user → second throw)
- [ ] Manual E2E smoke: create profile → `PUT /accounts` với GoldBrand=SJC+GoldType=Mieng+GoldQuantity=2 → `GET /summary` cho thấy `goldTotal = 2 × liveSellPrice` cộng vào `(securitiesValue + goldTotal)` của rule MaxInvestment
- [ ] **Backend cut line**: sau phase này backend xong, FE có thể bắt đầu độc lập

**Dependencies:** Phase 3 merged (cần `HmoneyGoldPriceProvider` để DI)
**Branch:** `feat/personal-finance-api` off `master`

---

### Phase 5 — Frontend

**Scope:** Angular service + Dashboard widget (read) + trang `/personal-finance` (CRUD) + nav. Split được thành 5a/5b nếu phình.

**Files (~5):**
- `frontend/src/app/core/services/personal-finance.service.ts` — HTTP client + TypeScript interfaces
- `frontend/src/app/features/dashboard/dashboard.component.ts` — modify: thêm widget Tài chính cá nhân
- `frontend/src/app/features/personal-finance/personal-finance.component.ts` — full page standalone + inline template + Gold form
- `frontend/src/app/app.routes.ts` — modify: thêm route
- `frontend/src/app/shared/components/header/header.component.ts` — modify: menu item "Tài chính cá nhân"

**Done criteria:**
- [ ] Dashboard widget hiển thị Net Worth + Health Score, có card 🪙 Vàng khi user có Gold account
- [ ] `/personal-finance`: CRUD accounts bao gồm **Gold form**: dropdown `GoldBrand` + `GoldType`, input `GoldQuantity` (lượng), hiển thị live giá Bán ra từ `GET /gold-prices`, Balance tự tính realtime
- [ ] Toggle "Nhập tay Balance" bypass auto-calc (fallback manual)
- [ ] Update rules → score tự tính lại
- [ ] Tiếng Việt có dấu đầy đủ (visual inspection Chrome DevTools)
- [ ] No console errors, no 4xx/5xx trong Network tab khi happy path

**Dependencies:** Phase 4 merged
**Branch:** `feat/personal-finance-frontend` off `master`

**Split option nếu phình:**
- Phase 5a: Service + Dashboard widget (read-only summary) — nhanh, có value ngay
- Phase 5b: `/personal-finance` page + Gold form + navigation — phần CRUD

---

### Phase 6 — Docs + CHANGELOG + Archive Plan

**Scope:** Đồng bộ tài liệu với code thực tế, archive plan.

**Files (~4 + plan move):**
- `docs/business-domain.md` — thêm entity, API endpoints
- `docs/features.md` — thêm section Personal Finance
- `docs/architecture.md` — thêm service/repo/controller/page entries
- `frontend/src/assets/CHANGELOG.md` — version bump + entry
- Move: `docs/plans/personal-finance.md` → `docs/plans/done/personal-finance.md`

**Done criteria:**
- [ ] `git diff --name-only` so với Phase 1-5 → tất cả entity/service/controller mới đều có entry trong docs
- [ ] CHANGELOG có version + ngày + test counts
- [ ] Plan file đã move sang `done/`

**Dependencies:** Phase 5 merged + manual verification xong
**Branch:** `docs/personal-finance-wrap-up` off `master`

---

## 9. Checkpoints (viết giữa các phase)

Sau khi merge mỗi phase vào master, thêm checkpoint block bên dưới trước khi start phase kế tiếp. Mục đích: cycle sau đọc checkpoint để khỏi re-read toàn bộ context.

**Template:**
```markdown
### Checkpoint — Phase N (merged [commit-sha] — YYYY-MM-DD)
- **Decisions:** key design decisions trong phase này (ngoài plan)
- **Files changed:** list file mới/sửa (git diff --name-only)
- **Tests:** N tests added, all pass
- **Surprises:** edge case/pitfall phát hiện khi implement (dành cho learning capture)
- **Next (Phase N+1):** file nào cần đọc, dependency state, anything that shifted
```

<!-- Checkpoints sẽ được append vào đây khi mỗi phase hoàn thành -->

### Checkpoint — Phase 1 Domain (merged `f87bc3c` — 2026-04-22)

- **Decisions:**
  - `FinancialRules` là class mutable (không record) để tương thích MongoDB BsonConstructor — match pattern của `Money`, `TrailingStopConfig`.
  - `UpsertAccount` validate theo nguyên tắc "all-or-nothing" cho 3 Gold fields — partial throw. Domain không biết về crawler, không tự tính Balance (Application layer sẽ handle auto-calc ở Phase 2).
  - `CalculateHealthScore` dùng formula "deficit/excess so với target của rule" cho cả 3 rules (consistent semantics). Comment trong code nói rõ nghĩa này.
  - Giữ nguyên property naming `GoldBrand`/`GoldType` trùng enum type — C# resolve cleanly, không thêm suffix `Value`.
- **Files changed:**
  - New: `src/InvestmentApp.Domain/Entities/FinancialProfile.cs`, `FinancialAccount.cs`, `FinancialRules.cs`, `FinancialAccountType.cs`, `GoldBrand.cs`, `GoldType.cs`
  - New: `tests/InvestmentApp.Domain.Tests/Entities/FinancialProfileTests.cs`
  - Modified: `docs/plans/personal-finance.md` (this section)
- **Tests:** 39 test cases added (29 methods + Theory expansions), all pass. Full Domain suite: 658/658 pass — no regression.
- **Surprises:**
  - Pre-existing uncommitted `UserTests.cs` local changes blocked build — reverted to master. Không liên quan Phase 1.
  - Review flag `GoldType.Mieng/Nhan` thiếu dấu — C# identifier không hỗ trợ dấu, nhớ map sang "Miếng"/"Nhẫn" ở Application/Frontend.
- **Next (Phase 2 — Application):**
  - Đọc file: `src/InvestmentApp.Application/RepositoryInterfaces.cs` (extend với `IFinancialProfileRepository`), `src/InvestmentApp.Application/Common/Interfaces/` (thêm `IGoldPriceProvider`), sample command pattern từ `TradePlan/Commands/` hoặc `AiSettings/Commands/`.
  - `UpsertFinancialAccountCommand` handler cần mock `IGoldPriceProvider` trong test để verify auto-calc Balance = quantity × sellPrice path.
  - `GetNetWorthSummaryQuery` cần `IPnLService` + `IPortfolioRepository` để sum securitiesValue từ tất cả portfolios của user.

### Checkpoint — Phase 2 Application (merged `56bbf69` — 2026-04-22)

- **Decisions:**
  - **Get-or-create pattern với soft-delete restore** trên `UpsertFinancialProfileCommand` — theo pattern `SaveAiSettingsCommand`. Check Active → Check IncludingDeleted → Create new. MonthlyExpense chỉ required khi tạo mới.
  - **Gold auto-calc ở Application layer**: `UpsertFinancialAccountCommandHandler.ResolveBalanceAsync` detect Type=Gold + 3 Gold fields đều set → fetch `IGoldPriceProvider.GetPriceAsync` → `Balance = quantity × sellPrice`. Provider trả null → throw (không silent fallback). Domain stay provider-agnostic.
  - **Balance validation tighter**: non-Gold-autocalc + non-Securities → Balance bắt buộc (throw nếu null). Trước đó coerce về 0 silently, tạo rủi ro UX data quality.
  - **N+1 ở GetNetWorthSummary** chấp nhận được cho solo user (~5 portfolios). Comment inline để không ai refactor nhầm.
  - **RuleCheck descriptions dùng `CultureInfo.InvariantCulture`** để tránh "50,5%" render trên VN server.
  - **PersonalFinanceMapper** `internal static` — tái dùng cho tất cả commands/queries. Không cần test riêng, cover indirect.
- **Files changed:**
  - New: `src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceDto.cs` (6 DTOs), `PersonalFinanceMapper.cs`
  - New: `src/InvestmentApp.Application/Common/Interfaces/IGoldPriceProvider.cs`
  - New: 3 Commands (UpsertFinancialProfile, UpsertFinancialAccount, RemoveFinancialAccount) + 3 Queries (GetFinancialProfile, GetNetWorthSummary, GetGoldPrices) — mỗi cái 1 file chứa cả command/query + handler theo pattern dự án
  - Modified: `src/InvestmentApp.Application/RepositoryInterfaces.cs` (thêm `IFinancialProfileRepository`)
  - New tests: 6 test classes (UpsertFinancialProfile, UpsertFinancialAccount, RemoveFinancialAccount, GetFinancialProfile, GetNetWorthSummary, GetGoldPrices) — tổng 21 tests
- **Tests:** 21 Application tests added (114 total pass). Full suite: 995/995 pass across Domain (658) + Application (114) + Infrastructure (218) + Api (5).
- **Surprises:**
  - Pre-existing broken `UserTests.cs` tiếp tục trên branch mới — đã loại bỏ khi checkout Phase 2.
  - `PortfolioPnLSummary.TotalMarketValue` là computed alias cho `TotalPortfolioValue` — mock phải set `TotalPortfolioValue` (handler đọc `TotalMarketValue`). Đã comment rõ trong test để lần sau không confuse.
- **Next (Phase 3 — Infrastructure Gold Crawler):**
  - **PRE-WORK bắt buộc**: mở Chrome DevTools → `24hmoney.vn/gia-vang` → Network tab (Fetch/XHR) → tìm JSON endpoint thực (nhiều khả năng `api-finance-t19.24hmoney.vn/...`). Nếu không có JSON → HTML scrape fallback với AngleSharp.
  - Save sample response làm fixture trong `tests/InvestmentApp.Infrastructure.Tests/Fixtures/hmoney_gold_response.{json,html}` — commit fixture TRƯỚC khi code provider.
  - **Đơn vị**: kiểm tra giá trả về là VND/lượng hay triệu VNĐ/lượng → normalize về VND/lượng (tương tự quirk ×1000 của giá CP).
  - Pattern tham chiếu: `src/InvestmentApp.Infrastructure/Services/Hmoney/HmoneyMarketDataProvider.cs` (HttpClient + IMemoryCache TTL 5 phút + exception handling).
  - Filter: chỉ parse Miếng + Nhẫn, reject Nữ trang/Trang sức.
  - Brand mapping: SJC/DOJI/PNJ → enum; "Bảo Tín Minh Châu" + khác → `Other`.

### Checkpoint — Phase 3 Infrastructure Gold Crawler (merged `34098a0` — 2026-04-22)

- **Decisions:**
  - **KHÔNG có JSON endpoint riêng cho gold** — data render SSR trong HTML table.gold-table của page `24hmoney.vn/gia-vang`. Đi theo hướng HTML scrape với **AngleSharp 1.3.0** (NuGet). Investigation qua Chrome DevTools (list_network_requests filter Fetch/XHR — không có endpoint vàng nào).
  - **Giá trị là full VND** (167,200,000), KHÔNG phải triệu VNĐ — mặc dù UI label nói "Đơn vị: triệu VNĐ/lượng". Không nhân 1000.
  - **Filter theo `div.brand-region`** ("vàng miếng"/"vàng nhẫn" keep; "vàng nữ trang"/"vàng trang sức" skip).
  - **Brand detection qua section divider** (`tr.divider-row`): "SJC" → SJC, "DOJI" → DOJI, "PNJ" → PNJ, "KHÁC" → Other.
  - **Other là bucket multi-vendor** (BTMC/BTMH/Ngọc Hải/Mi Hồng) → `GetPriceAsync(Other, X)` trả entry đầu tiên trong HTML. Document inline.
  - **Stale cache fallback** (TTL 6h) — nếu 24hmoney down, serve dữ liệu cũ + log warning. Tốt hơn là 500 error vì vàng update chậm (5-10 lần/ngày).
  - **Timeout defensive** (30s default) — apply trong `FetchHtmlAsync` qua linked CTS, không phụ thuộc HttpClient.Timeout trong DI.
  - **`ParseHtmlAsync` public static** — cho fixture test access trực tiếp, tránh `[InternalsVisibleTo]` (pattern chưa có trong project).
- **Files changed:**
  - New: `src/InvestmentApp.Infrastructure/Services/Hmoney/HmoneyGoldPriceProvider.cs`
  - New: `tests/InvestmentApp.Infrastructure.Tests/Services/HmoneyGoldPriceProviderTests.cs` (15 tests fixture-based)
  - New: `tests/InvestmentApp.Infrastructure.Tests/Services/HmoneyGoldPriceProviderLiveSmoke.cs` (opt-in live test, gate `HMONEY_GOLD_SMOKE=1`)
  - New: `tests/InvestmentApp.Infrastructure.Tests/Fixtures/hmoney_gia_vang_page.html` (143KB live capture 2026-04-22)
  - Modified: `src/InvestmentApp.Infrastructure/InvestmentApp.Infrastructure.csproj` (thêm AngleSharp 1.3.0)
  - Modified: `tests/InvestmentApp.Infrastructure.Tests/InvestmentApp.Infrastructure.Tests.csproj` (CopyToOutputDirectory cho Fixtures)
- **Tests:** 17 Infrastructure tests added (15 fixture + 1 live smoke + 1 stale fallback + 1 cancellation). Full suite: 1012/1012 pass.
- **Surprises:**
  - UI label "triệu VNĐ/lượng" MISLEADING — HTML values là full VND. Verify qua fixture test `PricesAreFullVND_NotScaledBy1000` (range 100M-200M).
  - `Configuration` namespace conflict: project có `InvestmentApp.Infrastructure.Configuration` che mất `AngleSharp.Configuration` → phải fully qualify `AngleSharp.Configuration.Default`.
  - Live site fetch: user-agent "Mozilla/5.0" OK, không cần auth hay cookie. 8 entries trả về match fixture.
- **Next (Phase 4 — Infrastructure: Repository + API Controller + DI):**
  - Đọc: `src/InvestmentApp.Infrastructure/Repositories/AiSettingsRepository.cs` (pattern 1:1 per-user, unique index UserId, soft-delete).
  - DI trong `src/InvestmentApp.Api/Program.cs`: pattern `AddHttpClient<HmoneyGoldPriceProvider>` giống `HmoneyMarketDataProvider`. Config section `GoldPriceProvider` cần thêm vào `appsettings.json`.
  - Controller: 6 endpoints (GET /, /summary, /gold-prices, PUT /, PUT /accounts, DELETE /accounts/{id}). Auth `[Authorize]` — UserId lấy từ JWT.
  - Unique index Mongo: `financial_profiles.UserId` ascending unique. Pattern tham chiếu `AiSettingsRepository` + mongo collection registration.
  - Post-Phase-4: backend hoàn chỉnh, có thể start FE độc lập.

### Checkpoint — Phase 4 API + Repo + DI (PR #TBD — 2026-04-22)

- **Decisions:**
  - **`FinancialProfileRepository` mirror `AiSettingsRepository`** pattern: 1:1 per-user, unique index trên `UserId` (explicit name `financial_profiles_userId_unique` để easier debug nếu conflict), soft-delete flag. `UpsertAsync` dùng `ReplaceOneAsync(IsUpsert=true)` — caller không cần check existence trước.
  - **6 endpoints** trong `PersonalFinanceController`: 3 read (GET /, /summary, /gold-prices) + 3 write (PUT /, PUT /accounts, DELETE /accounts/{id}). JWT auth global, UserId lấy từ `User.FindFirst("sub")` match `AiSettingsController` pattern.
  - **404 vs 200-empty asymmetry**: `GET /` returns 404 nếu profile null (clear signal "chưa setup"); `GET /summary` returns 200 với `HasProfile=false` để FE dashboard widget có thể render onboarding UI mà không cần round-trip 404. Bổ sung field `HasProfile` vào `NetWorthSummaryDto` sau review — FE phân biệt "chưa tạo profile" vs "profile fail hết rules".
  - **Exception → 400 mapping**: Domain `InvalidOperationException` + `ArgumentOutOfRangeException` caught trong PUT endpoints, return `{ message: ex.Message }`. Vietnamese error messages từ Domain đã user-facing.
  - **Impersonation mutation-block applied globally** qua `ImpersonationValidationMiddleware` — PUT/DELETE bị block khi impersonate (config `AllowImpersonateMutations=false`). Không cần per-controller annotation.
  - **DI registration**: `IFinancialProfileRepository` vào Repositories block (line 124) cho consistency. `HmoneyGoldPriceProvider` đăng ký qua `AddHttpClient<T>` với timeout default 30s, User-Agent "invest-mate-gold-crawler".
  - **Config**: `GoldPriceProvider` section thêm vào `appsettings.json` + `appsettings.Development.json` (PageUrl, TimeoutSeconds, CacheTtlMinutes). Production inherits defaults + env var override.
- **Files changed:**
  - New: `src/InvestmentApp.Infrastructure/Repositories/FinancialProfileRepository.cs`
  - New: `src/InvestmentApp.Api/Controllers/PersonalFinanceController.cs`
  - Modified: `src/InvestmentApp.Api/Program.cs` (DI: `IFinancialProfileRepository`, `IGoldPriceProvider` + `HmoneyGoldPriceProvider` HTTP client + `GoldPriceProviderOptions` bind)
  - Modified: `src/InvestmentApp.Api/appsettings.json`, `appsettings.Development.json` (GoldPriceProvider section)
  - Modified (review fixes): `src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceDto.cs` (thêm `HasProfile`), `GetNetWorthSummaryQuery.cs` (set flag), tests (+1 assertion `HasProfile`)
- **Tests:** 1 new Application test (`Handle_ProfileExists_SetsHasProfileTrue`); full suite 1013/1013 pass. Không thêm repo/controller unit tests — coverage từ Domain (658) + Application (115) + Infrastructure (235 — gồm crawler) đã đủ; integration test sẽ cần `WebApplicationFactory` + Testcontainers MongoDB (scope creep cho Phase 4, skip).
- **Manual E2E via real HTTP** (dev user-gen JWT, Mongo Atlas dev):
  - `GET /gold-prices` → 8 entries live từ 24hmoney (SJC Miếng/Nhẫn + DOJI Nhẫn + PNJ Nhẫn + 4 × Other)
  - `GET /` → 404 khi chưa tạo; `PUT /` với `monthlyExpense=20M` → profile tạo với 4 accounts + default rules
  - `PUT /accounts` Gold với `brand=0 (SJC), type=0 (Mieng), qty=2` → `Balance=339,400,000` (auto-calc từ sell price 169,700,000 live)
  - `GET /summary` sau khi add Gold → `totalAssets=339.4M, goldTotal=339.4M, healthScore=0` (clamped vì 3 rules đều fail — 100% investment, 0 savings, 0 emergency)
  - Validation: missing Balance cho Savings → 400; Non-Gold + Gold fields → 400 với message Vietnamese
  - `DELETE /accounts/{gold-id}` → 204; `DELETE` last Securities → 400 "Không thể xóa tài khoản Chứng khoán cuối cùng"
- **Surprises:**
  - Không thấy `Now listening on` trong dotnet run stdout — app vẫn ready; chỉ có seed-complete log. Dùng `curl /health` để detect readiness thay vì string match.
  - Enum serialization FE sẽ receive như số nguyên (`brand: 0, type: 0`) — cần Enum-as-string JSON config nếu FE muốn `"SJC"`. Để FE handle lúc Phase 5 tùy pattern.
- **Next (Phase 5 — Frontend):**
  - Đọc `docs/plans/personal-finance.md` Phase 5 scope (service + dashboard widget + trang `/personal-finance` + navigation).
  - Reference pattern: `frontend/src/app/features/ai-settings/` cho page CRUD + service, `frontend/src/app/features/dashboard/` cho widget integration.
  - `PersonalFinanceService` cần TypeScript interfaces match DTOs đã định nghĩa Phase 2.
  - Gold form UI: dropdown brand/type + input quantity (lượng) + hiển thị live `GET /gold-prices` price + Balance auto-calc preview. Toggle "Nhập tay" để bypass.
  - Vietnamese text có dấu đầy đủ (rule chính của project).
  - Split option (5a/5b) nếu scope phình — service + widget trước, trang CRUD sau.

---

## 10. End-to-end Verification (sau Phase 5)

Chạy trước khi move sang Phase 6 (Docs).

- [ ] `dotnet test` — tất cả backend tests pass
- [ ] 6 API endpoints hoạt động qua Swagger với JWT
- [ ] Dashboard widget hiển thị Net Worth + Health Score (có card Vàng nếu tồn tại)
- [ ] `/personal-finance` → CRUD accounts, edit rules, score thay đổi
- [ ] Securities value auto-sync từ portfolio (không cần nhập tay)
- [ ] **Gold account auto-calc**: chọn SJC Miếng + nhập 2 lượng → Balance = 2 × giá Bán ra SJC Miếng hiện tại, cập nhật realtime khi đổi dropdown
- [ ] **Gold price crawler**: `GET /api/v1/personal-finance/gold-prices` trả về data từ 24hmoney, chỉ có Miếng + Nhẫn, 4 brand (SJC/DOJI/PNJ/Other)
- [ ] Nguyên tắc tài chính hiển thị đúng pass/fail (Vàng cộng vào % đầu tư)
