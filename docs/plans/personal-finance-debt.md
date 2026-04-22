# Personal Finance — Debt (Nợ)

> Extend Personal Finance để track các khoản nợ, tính **Net Worth = Assets − Debt**, và thêm health rule bảo vệ nhà đầu tư khỏi nợ tiêu dùng lãi cao.

---

## 1. Motivation

Hiện tại Personal Finance chỉ track **tài sản** (5 loại account) — không phản ánh Net Worth thực. Với một nhà đầu tư kỷ luật, nợ là biến số quan trọng:

- "Không đầu tư khi còn nợ thẻ tín dụng lãi 24-36%/năm" — khoản nợ này ăn mòn lợi nhuận nhanh hơn bất kỳ cổ phiếu nào có thể tạo ra
- Net Worth = true wealth; TotalAssets đơn thuần có thể che giấu tình trạng tài chính thực
- Health score chưa reflect debt discipline

## 2. Design decision: Separate entity (Option B)

Đã cân nhắc:

| Option | Mô tả | Quyết định |
|---|---|---|
| A | Thêm `FinancialAccountType.Liability`, reuse FinancialAccount với Balance âm | ❌ Trộn assets/liabilities trong cùng model, field conditional lộn xộn |
| B | **Entity riêng `Debt`**, embedded list trong FinancialProfile | ✅ Clean separation; debt có metadata riêng (interest, maturity, payment) |
| C | Separate collection Mongo, có repository riêng | ❌ Overkill — solo user, ≤20 debts realistic |

**Chốt Option B**: `Debt` là value object/entity embedded trong `FinancialProfile.Debts[]`, tương tự cách `FinancialAccount` đang làm.

## 3. Domain model

### `Debt` entity (embedded)

| Field | Type | Required | Mô tả |
|---|---|---|---|
| `Id` | string (GUID) | ✅ | |
| `Type` | `DebtType` enum | ✅ | CreditCard=0, PersonalLoan=1, Mortgage=2, Auto=3, Installment=4, Other=99 |
| `Name` | string | ✅ | VD "Thẻ tín dụng VCB", "Vay mua nhà BIDV" |
| `Principal` | decimal | ✅ | Số gốc còn lại (VND) |
| `InterestRate` | decimal? | ⚠️ khuyến nghị | %/năm |
| `MonthlyPayment` | decimal? | | VND/tháng |
| `MaturityDate` | DateTime? | | Ngày đáo hạn |
| `Note` | string? | | |
| `CreatedAt`, `UpdatedAt` | DateTime | ✅ | |

### Domain rules

- **`FinancialProfile.UpsertDebt(...)`**: giống UpsertAccount, validate Principal > 0, InterestRate ≥ 0
- **`FinancialProfile.RemoveDebt(debtId)`**: chỉ xóa được khi `Principal = 0` (đã trả hết) — chống xóa nhầm khoản nợ thật, consistent với rule account
- **`FinancialProfile.GetTotalDebt()`**: sum `Debts[].Principal`
- **`FinancialProfile.GetNetWorth(securitiesValue)`**: `GetTotalAssets(securitiesValue) - GetTotalDebt()`
- **`FinancialProfile.HasHighInterestConsumerDebt()`**: true nếu có debt với `Type in {CreditCard, PersonalLoan}` AND `InterestRate.GetValueOrDefault() > 20m`

### Health score update

Current: 3 rules (Emergency 40pt / Investment 30pt / Savings 30pt) — **giữ nguyên tính theo TotalAssets** để không break existing user data.

Thêm **Rule 4**: "Không nợ tiêu dùng lãi cao" — nếu `HasHighInterestConsumerDebt()` → trừ **−20 điểm cứng** (không scale theo amount vì bản chất binary: có nợ lãi cao = fail). Điểm max vẫn 100, min clamp 0.

Ngưỡng 20% lãi/năm là cutoff hợp lý cho VN (thẻ tín dụng 24-36%, vay người quen <20% chấp nhận được).

## 4. Application layer

### Commands mới

- `UpsertDebtCommand(UserId, DebtId?, Type, Name, Principal, InterestRate?, MonthlyPayment?, MaturityDate?, Note?)` → `DebtDto`
- `RemoveDebtCommand(UserId, DebtId)` → void

### Query extensions

`GetNetWorthSummaryQuery` response thêm:
- `TotalDebt: decimal`
- `NetWorth: decimal` (= TotalAssets − TotalDebt)
- `Debts: DebtDto[]`
- `HasHighInterestConsumerDebt: bool`
- `RuleChecks` thêm entry thứ 4: `{ RuleName: "HighInterestDebt", IsPassing, Description, CurrentValue (principal lãi cao), RequiredValue (0) }`

## 5. API endpoints

```
POST   /api/v1/personal-finance/debts       → upsert (accountId=null create, otherwise update)
DELETE /api/v1/personal-finance/debts/:id   → remove
```

`GET /api/v1/personal-finance/summary` payload mở rộng — không cần endpoint mới.

## 6. Frontend

### Top summary cards

Thêm 2 cards (bên cạnh 5 asset cards):
- **💳 Nợ** — đỏ, hiển thị `totalDebt`
- **💎 Net Worth** — nhấn mạnh (viền vàng/xanh lá), `netWorth`

Row layout: `grid-cols-2 md:grid-cols-7` (5 assets + 1 debt + 1 net worth) — hoặc tách 2 rows cho mobile friendly.

### Section "Khoản nợ"

Dưới section "Tài khoản", layout tương tự (card grid, click-to-edit, ESC close, nút Lưu bên phải). Reuse convention đã thiết lập.

Form fields:
- Loại (dropdown 6 option)
- Tên
- Số gốc còn lại (VND)
- Lãi suất (%/năm) — optional
- Payment hàng tháng — optional
- Ngày đáo hạn — optional (date picker)
- Ghi chú

### Health score rule 4

Thêm dòng rule trong section Sức khỏe tài chính:
- ✓/✗ "Không nợ tiêu dùng lãi cao (>20%/năm)"
- CurrentValue: tổng principal của nợ lãi cao (hiển thị lý do fail)

### Dashboard widget

Đổi từ TotalAssets → **NetWorth** làm số chính; TotalDebt làm sub-line nhỏ. Nếu `HasHighInterestConsumerDebt` → icon ⚠️.

### Banner cảnh báo (Trade Wizard)

Khi có high-interest consumer debt, banner đỏ ở bước Strategy/Plan:
> ⚠️ Bạn đang có nợ thẻ tín dụng/tiêu dùng lãi suất cao. Trả nợ này thường là khoản "đầu tư" lãi kép tốt nhất trước khi mua cổ phiếu.

Có thể dismiss trong session, không block trade.

## 7. Phases

### Phase 1 — Domain + tests

- Enum `DebtType`
- Entity `Debt` (embedded)
- Extend `FinancialProfile`: `Debts` list, `UpsertDebt`, `RemoveDebt`, `GetTotalDebt`, `GetNetWorth`, `HasHighInterestConsumerDebt`
- Update `CalculateHealthScore` với rule 4
- Tests trong `FinancialProfileTests.cs` + `DebtTests.cs` (~15-20 tests)

### Phase 2 — Application + tests

- Commands: `UpsertDebtCommand`, `RemoveDebtCommand`
- Extend `GetNetWorthSummaryQuery` DTO + handler
- Update `PersonalFinanceMapper` for `DebtDto`
- Tests (~8-10 tests)

### Phase 3 — API

- 2 endpoints mới trong `PersonalFinanceController`
- Domain exception → 400 mapping
- Integration test cho endpoints

### Phase 4 — Frontend

- Extend `PersonalFinanceService` với debts methods + types
- Update `personal-finance.component.ts` — new section, new cards
- Update Dashboard widget
- Add banner trong Trade Wizard (feature `/trade-wizard`)
- Update guide `tai-chinh-ca-nhan.md`

### Phase 5 — Docs + Changelog

- `docs/business-domain.md` — thêm Debt entity mapping
- `docs/architecture.md` — nếu thêm service/repository
- `docs/features.md` — Net Worth feature
- `docs/project-context.md` — Tier 3 improvement note
- `CHANGELOG.md` — v2.48.0 entry

## 8. Migration concerns

- Existing `FinancialProfile` documents không có field `Debts` → Mongo driver sẽ deserialize thành empty list (default value of `List<Debt>`). An toàn, không cần migration script.
- Existing summary API consumers (mobile app, AI prompts) — response chỉ thêm field mới, không break backward compat.

## 9. Deploy checklist

- Không có env var mới (không external service)
- Không có new index (embedded trong profile doc)
- Không có breaking API change — FE chỉ cần đọc field mới nếu FE version mới; old FE ignore được

## 10. Out of scope (future)

- Amortization schedule (tính từng tháng gốc/lãi)
- Debt payment tracking — link giao dịch trả nợ với debt
- Debt snowball / avalanche strategy suggestion
- Credit score integration
- Auto-import từ Mobile Banking API

---

## Tracking

- Branch suggestion: `feat/personal-finance-debt`
- Estimated effort: 5 phases × ~1 ship cycle each ≈ 1-2 sessions
- Dependencies: none (self-contained extension)
