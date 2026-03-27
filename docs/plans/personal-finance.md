# Kế hoạch: Tài chính cá nhân (Personal Finance)

> **Trạng thái:** 📋 Planned
> **Ưu tiên:** Cao — Tier 3
> **Dự kiến:** ~24 files (16 new, 5 modified, 4 docs)

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
│   └── Type: Securities | Savings | Emergency | IdleCash
├── Rules: FinancialRules (embedded value object)
│   ├── EmergencyFundMonths (default: 6)
│   ├── MaxInvestmentPercent (default: 40%)
│   └── MinSavingsPercent (default: 30%)
├── CreatedAt, UpdatedAt
└── Methods:
    ├── Create(userId, monthlyExpense) → tạo 4 tài khoản mặc định
    ├── UpdateMonthlyExpense(amount)
    ├── UpdateRules(partial update)
    ├── UpsertAccount(...) — thêm/sửa tài khoản
    ├── RemoveAccount(id) — không cho xóa tài khoản Securities cuối cùng
    ├── GetTotalAssets(securitiesValue) → tổng = sum(balances) + securitiesValue
    └── CalculateHealthScore(securitiesValue) → 0-100 điểm
```

### 4 loại tài khoản

| Type | Tên tiếng Việt | Mô tả | Ghi chú |
|------|---------------|-------|---------|
| Securities | Chứng khoán | Giá trị danh mục đầu tư | Auto-sync từ PnLService, không nhập tay |
| Savings | Tiết kiệm | Gửi ngân hàng | Có lãi suất (interestRate) |
| Emergency | Dự phòng | Quỹ khẩn cấp | Không đụng vào trừ khẩn cấp |
| IdleCash | Nhàn rỗi | Tiền sẵn sàng đầu tư | Có thể chuyển vào portfolio |

### Health Score (0-100)

| Nguyên tắc | Default | Điểm trừ tối đa | Công thức |
|------------|---------|-----------------|-----------|
| Quỹ dự phòng ≥ N tháng chi tiêu | 6 tháng | -40 | `emergencyTotal >= monthlyExpense × N` |
| Đầu tư ≤ N% tổng tài sản | 40% | -30 | `securitiesValue <= totalAssets × N%` |
| Tiết kiệm ≥ N% tổng tài sản | 30% | -30 | `savingsTotal >= totalAssets × N%` |

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
- `NetWorthSummaryDto`: totalAssets, securitiesValue, savingsTotal, emergencyTotal, idleCashTotal, monthlyExpense, healthScore, ruleChecks[], accounts[]
- `RuleCheckResultDto`: ruleName, isPassing, description, currentValue, requiredValue

### Commands

| File | Input | Logic |
|------|-------|-------|
| `UpsertFinancialProfileCommand.cs` | monthlyExpense, rules? | Get-or-create pattern, upsert |
| `UpsertFinancialAccountCommand.cs` | accountId?, type, name, balance, interestRate? | Load profile → UpsertAccount() |
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

### Controller
**Mới:** `src/InvestmentApp.Api/Controllers/PersonalFinanceController.cs`
- Route: `api/v1/personal-finance`, JWT auth

| Method | Endpoint | Handler |
|--------|----------|---------|
| GET | `/` | GetFinancialProfileQuery |
| GET | `/summary` | GetNetWorthSummaryQuery |
| PUT | `/` | UpsertFinancialProfileCommand |
| PUT | `/accounts` | UpsertFinancialAccountCommand |
| DELETE | `/accounts/{accountId}` | RemoveFinancialAccountCommand |

### DI Registration
**Sửa:** `src/InvestmentApp.Api/Program.cs` — thêm `IFinancialProfileRepository`

---

## 4. Frontend

### Service
**Mới:** `frontend/src/app/core/services/personal-finance.service.ts`

Interfaces: `FinancialProfile`, `FinancialAccount`, `FinancialRules`, `NetWorthSummary`, `RuleCheckResult`

Methods:
- `getProfile(): Observable<FinancialProfile | null>`
- `getSummary(): Observable<NetWorthSummary>`
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
│ 📈 CK: 200tr  🏦 TK: 150tr                     │
│ 🛡️ DP: 100tr  💵 NR: 50tr                      │
└─────────────────────────────────────────────────┘
```

Nếu chưa có profile → hiện "Thiết lập quản lý tài chính cá nhân" + nút [Bắt đầu]

Pattern: giống Daily Routine widget trên Dashboard

### Trang riêng `/personal-finance`
**Mới:** `frontend/src/app/features/personal-finance/personal-finance.component.ts`

| Section | Nội dung |
|---------|----------|
| Header | "Tài chính cá nhân" |
| Net Worth Cards (grid-cols-4) | Tổng tài sản, Chứng khoán (auto), Tiết kiệm+Dự phòng, Nhàn rỗi |
| Health Score Bar | 0-100, xanh/vàng/đỏ (pattern risk-dashboard) |
| Rule Compliance | Progress bars cho từng nguyên tắc, pass/fail |
| Accounts Management | CRUD cards, inline edit balance/interestRate |
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
- UpsertAccount: new/existing, Savings có interestRate
- RemoveAccount: valid/invalid/last Securities throws
- CalculateHealthScore: all pass=100, each fail deducts proportionally, clamp [0,100]

### Phase 2: Application Tests
- `UpsertFinancialProfileCommandTests.cs` — new/existing user
- `GetNetWorthSummaryQueryTests.cs` — with profile + multi-portfolio, without profile
- `UpsertFinancialAccountCommandTests.cs` — new/existing account

---

## 7. Files Summary

| Type | Count |
|------|-------|
| New backend | 9 (entity, 3 commands, 2 queries, DTOs, repo, controller) |
| New frontend | 2 (service, component) |
| New tests | 4 (domain + 3 application) |
| Modified | 5 (RepositoryInterfaces, Program.cs, routes, dashboard, header) |
| Docs | 4 (architecture, business-domain, features, CHANGELOG) |
| **Total** | **~24 files** |

---

## 8. Thứ tự implement

1. Domain entity + tests (TDD) → `dotnet test Domain.Tests`
2. Application layer + tests → `dotnet test Application.Tests`
3. Infrastructure repo + API controller + DI
4. Frontend service
5. Dashboard widget
6. Full page component + navigation
7. Docs + CHANGELOG

---

## 9. Verification

- [ ] `dotnet test` — all backend tests pass
- [ ] Backend Swagger → test 5 API endpoints
- [ ] Dashboard widget hiển thị Net Worth + Health Score
- [ ] `/personal-finance` → CRUD accounts, edit rules, score thay đổi
- [ ] Securities value auto-sync từ portfolio (không cần nhập tay)
- [ ] Nguyên tắc tài chính hiển thị đúng pass/fail
