# Kế hoạch — Thêm `DepositDate` + `MaturityDate` cho sổ tiết kiệm

> Tài liệu kế hoạch — bổ sung 2 trường ngày (ngày mở sổ + ngày đáo hạn) cho `FinancialAccount` khi `Type == Savings`, mirror pattern có sẵn của `Debt.MaturityDate`.
> Ngày tạo: 2026-04-23
> Trạng thái: **Đã review bởi 2 agent + chốt quyết định, SẴN SÀNG implement**
> Session khác sẽ pick up tài liệu này để triển khai.

---

## Bối cảnh

Tại phần "Tài chính cá nhân", `Debt` (nợ) có `MaturityDate` (ngày đáo hạn) + `CreatedAt`, nhưng `FinancialAccount` kiểu `Savings` (tiết kiệm lãi suất) **không có** trường ngày nào.

Thiết kế hiện tại xem `FinancialAccount` như "ảnh chụp tài sản hiện tại" (số dư + lãi suất danh nghĩa), còn `Debt` như "hợp đồng có vòng đời". Nhưng sổ tiết kiệm **có kỳ hạn** (fixed-term deposit) là hợp đồng có vòng đời — thiếu ngày gửi/đáo hạn nghĩa là:

- Không tính được **lãi dự thu** (accrued interest) tới hôm nay
- Không nhắc được **sắp đáo hạn** để tái tục hoặc rút
- Không phân biệt được sổ **không kỳ hạn** (demand) vs **có kỳ hạn** (term)

## Phạm vi PR này

- Thêm 2 trường optional `DepositDate` + `MaturityDate` (cả 2 đều `DateTime?`) cho `FinancialAccount`.
- Thêm `CreatedAt` cho `FinancialAccount` (hiện thiếu, bất đối xứng với `Debt`).
- Validation: 3 trường này **chỉ áp dụng khi `Type == Savings`** (mirror pattern `InterestRate`/Gold* fields).
- Business guard: nếu cả 2 date đều set → `MaturityDate >= DepositDate` (fat-finger catcher).
- UI: 2 `<input type="date">` + **hàng preset chips "Kỳ hạn" [1T][3T][6T][12T][24T][Tùy chỉnh]** để auto-compute maturity từ deposit.
- Card display: `📅 dd/MM/yyyy → dd/MM/yyyy` dưới lãi suất (chỉ khi có).
- **Bonus fix** (pre-existing bug cùng vùng code): `onTypeChange()` không null `formInterestRate` khi chuyển type → date mới sẽ leak tương tự nếu không fix. Fix luôn 3 field.

## Không làm trong PR này (defer)

- Tính lãi dự thu / hiển thị "lãi hiện tại" trên card.
- Badge "còn X ngày đáo hạn" với color coding (red khi ≤ 7 ngày).
- Notification/reminder khi sắp đáo hạn.
- Migration data: existing sổ tiết kiệm không có date → user nhập bổ sung khi edit.

## 3 quyết định đã chốt (từ review round)

| # | Quyết định | Status |
|---|---|---|
| Q1 | **Preset kỳ hạn chips [1T][3T][6T][12T][24T][Tùy chỉnh]** auto-compute maturity từ deposit date | ✅ Làm luôn (UX rất tốt, low cost ~15 dòng) |
| Q2 | Fix pre-existing bug leak `formInterestRate` trong `onTypeChange()` | ✅ Làm luôn (đang chạm đúng chỗ, fix 1 PR gộp sạch hơn) |
| Q3 | Thêm `CreatedAt` cho `FinancialAccount` | ✅ Làm luôn (touch cost thấp nhất bây giờ, xoá bất đối xứng với Debt) |

## Tổng hợp review 2 agent

### Backend reviewer findings

| # | Verdict | Vấn đề | Fix áp dụng |
|---|---|---|---|
| 1 | ⚠️ | FinancialAccount thiếu `CreatedAt` | Thêm trong PR này |
| 2 | ✅ | Validation ở `FinancialProfile` đúng chỗ | — |
| 3 | ✅ | UTC normalize ở handler OK | — |
| 4 | ⚠️ | 2 `DateTime?` adjacent ở tail → dễ swap silent | **Bắt buộc named arguments** + test `Handle_PassesDates_InCorrectOrder` |
| 5 | ✅ | Mongo auto-serialize OK (Program.cs:36-43 có `IgnoreExtraElementsConvention`) | — |
| 6 | ⚠️ | Thiếu test: clear-to-null on Update, non-Savings+dates on Update path, null passthrough | Thêm |
| 7 | ⚠️ | `Maturity < Deposit` khi cả 2 set → vô lý | Guard 1 dòng ở Domain |
| 8 | ✅ | Back-compat với document cũ OK (missing BSON element → null) | — |

### Frontend reviewer findings

| # | Verdict | Vấn đề | Fix áp dụng |
|---|---|---|---|
| 1 | ⚠️ | 2 date input trần — VN savings 99% là standard term | **Thêm preset chips** (Q1 ✅) |
| 2 | ⚠️ | "Ngày gửi" không phải banking term | **Đổi thành "Ngày mở sổ"** (VCB/BIDV/ACB dùng chuẩn) |
| 3 | ⚠️ | Card format trộn granularity dd/MM + dd/MM/yyyy | Cùng format `dd/MM/yyyy → dd/MM/yyyy`, dùng arrow |
| 4 | ❌ | `onTypeChange()` không null form date fields → leak | **Fix luôn + fix bug cũ `formInterestRate`** (Q2 ✅) |
| 5 | ❌ | `depositDate?.substring(0,10)` trả `undefined`, mismatch type `string \| null` | Pattern: `account.depositDate ? account.depositDate.substring(0,10) : null` |
| 6 | ⚠️ | `frontend/src/assets/docs/tai-chinh-ca-nhan.md` tồn tại → MEMORY rule bắt update | **Bắt buộc update**, không "maybe" |
| 7 | ⚠️ | Chưa có `personal-finance.component.spec.ts` → vi phạm TDD rule | **Tạo spec tối thiểu 3 test** |
| 8 | ⚠️ | Chưa spec layout 2 input trên mobile | `grid grid-cols-1 sm:grid-cols-2 gap-2` (mirror gold brand/type line 310) |

---

## Phase 1 — Domain layer (TDD)

### 1.1 Viết tests TRƯỚC (Red)

File: `tests/InvestmentApp.Domain.Tests/FinancialAccountTests.cs` (extend hoặc tạo mới)

Test cases:
- `Create_Savings_WithDepositAndMaturityDate_StoresBoth` — assert 2 date + CreatedAt = UtcNow
- `Create_SetsCreatedAt_ToUtcNow` — verify CreatedAt khác default
- `Update_Savings_ReplacesDepositAndMaturityDate` — update từ (null, null) → (date1, date2), rồi (date1, date2) → (null, null), assert clear đúng
- `Update_DoesNotMutate_CreatedAt` — CreatedAt phải immutable sau Create

File: `tests/InvestmentApp.Domain.Tests/FinancialProfileTests.cs` (extend)

Test cases:
- `UpsertAccount_NonSavings_WithDepositDate_Throws` — type=IdleCash + depositDate set → `InvalidOperationException`
- `UpsertAccount_NonSavings_WithMaturityDate_Throws` — tương tự
- `UpsertAccount_NonSavingsOnUpdate_WithDates_Throws` — update path cũng phải reject (reviewer điểm 6)
- `UpsertAccount_Savings_MaturityBeforeDeposit_Throws` — cả 2 set + Maturity < Deposit → `InvalidOperationException`
- `UpsertAccount_Savings_WithOnlyDepositDate_OK` — partial nhập OK (1 set, 1 null)
- `UpsertAccount_Savings_WithOnlyMaturityDate_OK` — partial nhập OK
- `UpsertAccount_Savings_BothDatesNull_OK` — không kỳ hạn OK

Verify: `dotnet test tests/InvestmentApp.Domain.Tests` → Red (tất cả test mới fail do chưa có field).

### 1.2 Implement (Green)

#### `src/InvestmentApp.Domain/Entities/FinancialAccount.cs`

```csharp
public class FinancialAccount
{
    public string Id { get; private set; } = null!;
    public FinancialAccountType Type { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Balance { get; private set; }
    public decimal? InterestRate { get; private set; }
    public DateTime? DepositDate { get; private set; }    // NEW
    public DateTime? MaturityDate { get; private set; }   // NEW
    public string? Note { get; private set; }
    public GoldBrand? GoldBrand { get; private set; }
    public GoldType? GoldType { get; private set; }
    public decimal? GoldQuantity { get; private set; }
    public DateTime CreatedAt { get; private set; }        // NEW
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public FinancialAccount() { }

    internal static FinancialAccount Create(
        FinancialAccountType type,
        string name,
        decimal balance,
        decimal? interestRate = null,
        string? note = null,
        GoldBrand? goldBrand = null,
        GoldType? goldType = null,
        decimal? goldQuantity = null,
        DateTime? depositDate = null,        // NEW — tail-append
        DateTime? maturityDate = null)        // NEW — tail-append
    {
        var now = DateTime.UtcNow;
        return new FinancialAccount
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            Balance = balance,
            InterestRate = interestRate,
            DepositDate = depositDate,
            MaturityDate = maturityDate,
            Note = note,
            GoldBrand = goldBrand,
            GoldType = goldType,
            GoldQuantity = goldQuantity,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    internal void Update(
        FinancialAccountType type,
        string name,
        decimal balance,
        decimal? interestRate,
        string? note,
        GoldBrand? goldBrand,
        GoldType? goldType,
        decimal? goldQuantity,
        DateTime? depositDate,               // NEW
        DateTime? maturityDate)               // NEW
    {
        Type = type;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Balance = balance;
        InterestRate = interestRate;
        DepositDate = depositDate;
        MaturityDate = maturityDate;
        Note = note;
        GoldBrand = goldBrand;
        GoldType = goldType;
        GoldQuantity = goldQuantity;
        UpdatedAt = DateTime.UtcNow;
        // CreatedAt intentionally NOT mutated
    }
}
```

#### `src/InvestmentApp.Domain/Entities/FinancialProfile.cs`

**`UpsertAccount` signature** — tail-append 2 param:

```csharp
public FinancialAccount UpsertAccount(
    string? accountId,
    FinancialAccountType type,
    string name,
    decimal balance,
    decimal? interestRate = null,
    string? note = null,
    GoldBrand? goldBrand = null,
    GoldType? goldType = null,
    decimal? goldQuantity = null,
    DateTime? depositDate = null,         // NEW
    DateTime? maturityDate = null)         // NEW
{
    ValidateAccountFields(type, balance, interestRate, goldBrand, goldType, goldQuantity, depositDate, maturityDate);

    FinancialAccount account;
    if (accountId is null)
    {
        if (type == FinancialAccountType.Securities && Accounts.Any(a => a.Type == FinancialAccountType.Securities))
            throw new InvalidOperationException("Tài khoản Chứng khoán được tự động tạo khi khởi tạo profile — không cho phép tạo thêm");
        // Named args BẮT BUỘC để tránh silent swap của 2 DateTime? cuối
        account = FinancialAccount.Create(
            type: type,
            name: name,
            balance: balance,
            interestRate: interestRate,
            note: note,
            goldBrand: goldBrand,
            goldType: goldType,
            goldQuantity: goldQuantity,
            depositDate: depositDate,
            maturityDate: maturityDate);
        Accounts.Add(account);
    }
    else
    {
        account = Accounts.FirstOrDefault(a => a.Id == accountId)
            ?? throw new InvalidOperationException($"Không tìm thấy tài khoản với id {accountId}");
        account.Update(
            type: type,
            name: name,
            balance: balance,
            interestRate: interestRate,
            note: note,
            goldBrand: goldBrand,
            goldType: goldType,
            goldQuantity: goldQuantity,
            depositDate: depositDate,
            maturityDate: maturityDate);
    }

    UpdatedAt = DateTime.UtcNow;
    IncrementVersion();
    return account;
}
```

**`ValidateAccountFields` — thêm rule:**

```csharp
private static void ValidateAccountFields(
    FinancialAccountType type,
    decimal balance,
    decimal? interestRate,
    GoldBrand? goldBrand,
    GoldType? goldType,
    decimal? goldQuantity,
    DateTime? depositDate,               // NEW
    DateTime? maturityDate)               // NEW
{
    if (balance < 0m)
        throw new ArgumentOutOfRangeException(nameof(balance), "Balance phải >= 0");

    if (type != FinancialAccountType.Savings && interestRate.HasValue)
        throw new InvalidOperationException("InterestRate chỉ áp dụng cho tài khoản Savings");

    // NEW — DepositDate/MaturityDate chỉ áp dụng Savings
    if (type != FinancialAccountType.Savings && (depositDate.HasValue || maturityDate.HasValue))
        throw new InvalidOperationException("DepositDate/MaturityDate chỉ áp dụng cho tài khoản Savings");

    // NEW — cả 2 set thì Maturity >= Deposit
    if (depositDate.HasValue && maturityDate.HasValue && maturityDate.Value < depositDate.Value)
        throw new InvalidOperationException("MaturityDate phải >= DepositDate");

    // … existing gold validation giữ nguyên
}
```

Gọi `ValidateAccountFields` trong `UpsertAccount` phải pass thêm 2 param mới.

### 1.3 Verify

```bash
dotnet test tests/InvestmentApp.Domain.Tests
```

→ All green.

---

## Phase 2 — Application layer (TDD)

### 2.1 Viết test TRƯỚC (Red)

File: `tests/InvestmentApp.Application.Tests/PersonalFinance/Commands/UpsertFinancialAccountCommandHandlerTests.cs`

Test cases mới:
- `Handle_Savings_DepositAndMaturityDate_NormalizedToUtcMidnight` — gửi `2025-01-15T14:30:00+07:00` → assert stored là `2025-01-15T00:00:00Z` (mirror `Handle_MaturityDate_NormalizedToUtcMidnight` của Debt)
- `Handle_Savings_PassesDates_InCorrectOrder` — set `DepositDate=2025-01-01`, `MaturityDate=2026-01-01` → assert round-trip đúng property (chống silent swap reviewer điểm 4)
- `Handle_Savings_BothDatesNull_PassesThrough` — null stays null
- `Handle_UpdateSavings_ClearDates_ToNull` — account có dates → command gửi null → dates cleared

Verify: `dotnet test tests/InvestmentApp.Application.Tests` → Red.

### 2.2 Implement (Green)

#### `src/InvestmentApp.Application/PersonalFinance/Commands/UpsertFinancialAccount/UpsertFinancialAccountCommand.cs`

Thêm 2 property:

```csharp
public DateTime? DepositDate { get; set; }
public DateTime? MaturityDate { get; set; }
```

Handler `Handle()` — UTC-normalize (mirror UpsertDebt lines 40-45) + named args:

```csharp
var depositDate = request.DepositDate.HasValue
    ? DateTime.SpecifyKind(request.DepositDate.Value.Date, DateTimeKind.Utc)
    : (DateTime?)null;

var maturityDate = request.MaturityDate.HasValue
    ? DateTime.SpecifyKind(request.MaturityDate.Value.Date, DateTimeKind.Utc)
    : (DateTime?)null;

var account = profile.UpsertAccount(
    accountId: request.AccountId,
    type: request.Type,
    name: request.Name,
    balance: balance,
    interestRate: request.InterestRate,
    note: request.Note,
    goldBrand: request.GoldBrand,
    goldType: request.GoldType,
    goldQuantity: request.GoldQuantity,
    depositDate: depositDate,               // NEW
    maturityDate: maturityDate);             // NEW
```

#### `src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceDto.cs`

Thêm vào `FinancialAccountDto`:

```csharp
public DateTime? DepositDate { get; set; }
public DateTime? MaturityDate { get; set; }
public DateTime CreatedAt { get; set; }
```

#### `src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceMapper.cs`

Update `ToDto(FinancialAccount)`:

```csharp
public static FinancialAccountDto ToDto(FinancialAccount account, decimal? balanceOverride = null) => new()
{
    Id = account.Id,
    Type = account.Type,
    Name = account.Name,
    Balance = balanceOverride ?? account.Balance,
    InterestRate = account.InterestRate,
    DepositDate = account.DepositDate,         // NEW
    MaturityDate = account.MaturityDate,       // NEW
    Note = account.Note,
    GoldBrand = account.GoldBrand,
    GoldType = account.GoldType,
    GoldQuantity = account.GoldQuantity,
    CreatedAt = account.CreatedAt,             // NEW
    UpdatedAt = account.UpdatedAt,
};
```

### 2.3 Verify

```bash
dotnet test tests/InvestmentApp.Application.Tests
```

→ All green. Chạy full `dotnet test` để đảm bảo không regression cross-suite.

---

## Phase 3 — Frontend (TDD)

### 3.1 DTO + Request interface

File: `frontend/src/app/core/services/personal-finance.service.ts`

**`FinancialAccountDto`** (line 47-58) — thêm:

```typescript
export interface FinancialAccountDto {
  id: string;
  type: FinancialAccountType;
  name: string;
  balance: number;
  interestRate?: number | null;
  depositDate?: string | null;        // NEW (ISO string)
  maturityDate?: string | null;       // NEW (ISO string)
  note?: string | null;
  goldBrand?: GoldBrand | null;
  goldType?: GoldType | null;
  goldQuantity?: number | null;
  createdAt?: string;                  // NEW
  updatedAt: string;
}
```

**`UpsertFinancialAccountRequest`** (line 124-134) — thêm:

```typescript
export interface UpsertFinancialAccountRequest {
  accountId?: string | null;
  type: FinancialAccountType;
  name: string;
  balance?: number | null;
  interestRate?: number | null;
  depositDate?: string | null;        // NEW (YYYY-MM-DD or ISO)
  maturityDate?: string | null;       // NEW
  note?: string | null;
  goldBrand?: GoldBrand | null;
  goldType?: GoldType | null;
  goldQuantity?: number | null;
}
```

### 3.2 Viết spec TRƯỚC (Red)

File: `frontend/src/app/features/personal-finance/personal-finance.component.spec.ts` **(TẠO MỚI — chưa tồn tại)**

Minimal spec — 3-4 test:

```typescript
describe('PersonalFinanceComponent — savings term dates', () => {
  let component: PersonalFinanceComponent;
  // setup TestBed ...

  it('onTypeChange: chuyển Savings → IdleCash nulls deposit/maturity/interest', () => {
    component.formType = FinancialAccountType.Savings;
    component.formDepositDate = '2025-01-15';
    component.formMaturityDate = '2026-01-15';
    component.formInterestRate = 6.5;
    component.formType = FinancialAccountType.IdleCash;
    component.onTypeChange();
    expect(component.formDepositDate).toBeNull();
    expect(component.formMaturityDate).toBeNull();
    expect(component.formInterestRate).toBeNull();
  });

  it('submitAccountForm: chỉ gửi dates khi Type === Savings', () => {
    // spy service.upsertAccount, set type=IdleCash + formDepositDate='2025-01-15'
    // assert payload không có depositDate/maturityDate
  });

  it('openEditAccountForm: handle null depositDate safely', () => {
    const account = { ...mockAccount, depositDate: null, maturityDate: null };
    component.openEditAccountForm(account);
    expect(component.formDepositDate).toBeNull();
    expect(component.formMaturityDate).toBeNull();
  });

  it('setTermMonths(12): auto-computes maturity = deposit + 12 tháng', () => {
    component.formDepositDate = '2025-01-15';
    component.setTermMonths(12);
    expect(component.formMaturityDate).toBe('2026-01-15');
  });

  it('setTermMonths: no-op khi formDepositDate chưa nhập', () => {
    component.formDepositDate = null;
    component.setTermMonths(6);
    expect(component.formMaturityDate).toBeNull();
  });
});
```

Verify: `ng test --watch=false` → Red.

### 3.3 Implement (Green)

File: `frontend/src/app/features/personal-finance/personal-finance.component.ts`

#### Form state (class properties, area lines 512-546)

```typescript
formDepositDate: string | null = null;
formMaturityDate: string | null = null;
```

#### Template — savings section (lines 365-379)

Replace block hiện tại với (Vietnamese có dấu đầy đủ, labels đúng banking term):

```html
<div *ngIf="formType === FinancialAccountType.Savings" class="space-y-3">
  <div>
    <label class="text-xs text-gray-400 block mb-1">Lãi suất (%/năm, tùy chọn)</label>
    <input type="number" min="0" max="30" step="0.1" [(ngModel)]="formInterestRate"
           class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2" />
  </div>

  <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
    <div>
      <label class="text-xs text-gray-400 block mb-1">Ngày mở sổ (tùy chọn)</label>
      <input type="date" [(ngModel)]="formDepositDate"
             class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2" />
    </div>
    <div>
      <label class="text-xs text-gray-400 block mb-1">Ngày đáo hạn (tùy chọn)</label>
      <input type="date" [(ngModel)]="formMaturityDate"
             class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2" />
    </div>
  </div>

  <div *ngIf="formDepositDate" class="flex items-center gap-1 flex-wrap">
    <span class="text-xs text-gray-400 mr-1">Kỳ hạn:</span>
    <button type="button" *ngFor="let m of [1, 3, 6, 12, 24]"
            (click)="setTermMonths(m)"
            class="text-xs px-2 py-1 rounded bg-gray-700 hover:bg-gray-600 text-white">
      {{ m }}T
    </button>
    <button type="button" (click)="formMaturityDate = null"
            class="text-xs px-2 py-1 rounded bg-gray-700 hover:bg-gray-600 text-gray-300">
      Tùy chỉnh
    </button>
  </div>
</div>
```

#### `setTermMonths()` method (thêm mới)

```typescript
setTermMonths(months: number): void {
  if (!this.formDepositDate) return;
  const d = new Date(this.formDepositDate);
  if (isNaN(d.getTime())) return;
  d.setMonth(d.getMonth() + months);
  // Format YYYY-MM-DD (HTML date input format)
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  this.formMaturityDate = `${yyyy}-${mm}-${dd}`;
}
```

#### `onTypeChange()` — fix leak + thêm null date

Location hiện tại ~lines 683-689. Thêm vào (ngay cạnh gold reset):

```typescript
onTypeChange(): void {
  // existing gold reset ...
  if (this.formType !== FinancialAccountType.Savings) {
    this.formInterestRate = null;       // FIX pre-existing bug
    this.formDepositDate = null;         // NEW
    this.formMaturityDate = null;        // NEW
  }
  // existing gold logic ...
}
```

#### `submitAccountForm()` (lines 731-775)

Khi build payload, chỉ gửi date khi Savings:

```typescript
const payload: UpsertFinancialAccountRequest = {
  accountId: this.editingAccountId,
  type: this.formType,
  name: this.formName,
  balance: ...,
  interestRate: this.formType === FinancialAccountType.Savings ? this.formInterestRate : null,
  depositDate: this.formType === FinancialAccountType.Savings ? this.formDepositDate : null,    // NEW
  maturityDate: this.formType === FinancialAccountType.Savings ? this.formMaturityDate : null,  // NEW
  note: this.formNote,
  // gold fields ...
};
```

#### `openNewAccountForm()` / `openEditAccountForm()`

`openNewAccountForm()` — reset mới:

```typescript
this.formDepositDate = null;
this.formMaturityDate = null;
```

`openEditAccountForm(account)` — null-safe prefill:

```typescript
this.formDepositDate = account.depositDate ? account.depositDate.substring(0, 10) : null;
this.formMaturityDate = account.maturityDate ? account.maturityDate.substring(0, 10) : null;
```

#### Card display (lines 174-175)

Replace dòng hiện tại với:

```html
<div *ngIf="account.type === FinancialAccountType.Savings && account.interestRate"
     class="text-[10px] text-gray-500 mt-0.5">
  Lãi suất: {{ account.interestRate }}%/năm
</div>
<div *ngIf="account.type === FinancialAccountType.Savings && (account.depositDate || account.maturityDate)"
     class="text-[10px] text-gray-500 mt-0.5">
  📅
  <span *ngIf="account.depositDate">{{ account.depositDate | date:'dd/MM/yyyy' }}</span>
  <span *ngIf="account.depositDate && account.maturityDate"> → </span>
  <span *ngIf="account.maturityDate">{{ account.maturityDate | date:'dd/MM/yyyy' }}</span>
</div>
```

### 3.4 Verify

```bash
ng test --watch=false
```

→ All green. Manual: chạy `ng serve`, mở modal thêm sổ tiết kiệm, test flow:
1. Chọn type Savings → thấy lãi suất + 2 date input + preset chips
2. Nhập ngày mở sổ → bấm [12T] → maturity tự fill
3. Chuyển type sang IdleCash → date fields ẩn, form state nulled
4. Save → card hiển thị "📅 01/01/2025 → 01/01/2026"
5. Edit lại → form prefill đúng

---

## Phase 4 — Documentation (bắt buộc trước commit)

### 4.1 `docs/business-domain.md`

Tìm section `FinancialAccount` entity, thêm 3 field:
- `DepositDate?: DateTime` — ngày mở sổ (chỉ Savings)
- `MaturityDate?: DateTime` — ngày đáo hạn (chỉ Savings)
- `CreatedAt: DateTime` — thời điểm tạo (auto-set, immutable)

Business rule: DepositDate/MaturityDate chỉ áp dụng khi Type=Savings; khi cả 2 set phải Maturity >= Deposit.

### 4.2 `docs/features.md`

Tìm section Personal Finance → Savings, thêm bullet:
- Hỗ trợ kỳ hạn sổ tiết kiệm: nhập Ngày mở sổ + Ngày đáo hạn (optional)
- Preset chips 1/3/6/12/24 tháng để auto-compute maturity
- Card hiển thị range ngày

### 4.3 `frontend/src/assets/CHANGELOG.md`

Version entry mới (bump minor version):

```markdown
## [vX.Y.Z] — 2026-04-XX

### Thêm mới
- Sổ tiết kiệm hỗ trợ **kỳ hạn**: thêm Ngày mở sổ + Ngày đáo hạn (tùy chọn)
- Preset chips kỳ hạn chuẩn (1T/3T/6T/12T/24T) tự động tính ngày đáo hạn
- FinancialAccount thêm `CreatedAt` để tracking

### Sửa lỗi
- Fix leak: chuyển loại tài khoản từ Savings sang type khác không reset lãi suất cũ
```

### 4.4 `frontend/src/assets/docs/tai-chinh-ca-nhan.md`

**Bắt buộc** (MEMORY rule: always update user guide). Thêm section hướng dẫn:
- Khi nào nên nhập ngày mở sổ/đáo hạn (sổ có kỳ hạn vs không kỳ hạn)
- Cách dùng preset chips
- Giải thích sẽ dùng ngày này cho tính năng nhắc đáo hạn + tính lãi dự thu trong các bản sau

Check `frontend/src/app/core/services/help.service.ts` (hoặc tương đương) — nếu có topic registry, đảm bảo entry "tai-chinh-ca-nhan" không cần thêm mới (đã có). Chỉ cần update nội dung file.

### 4.5 `CLAUDE.md` — không cần update (không thêm convention/directive/pipe mới)

### 4.6 `docs/architecture.md` — cập nhật nếu schema entity liệt kê chi tiết field (grep tìm `FinancialAccount` → thêm 3 field nếu có)

---

## Checklist tổng (copy-paste cho session implement)

### Backend
- [ ] Tests Domain mới đã Red (chạy `dotnet test tests/InvestmentApp.Domain.Tests`)
- [ ] `FinancialAccount.cs` — thêm DepositDate, MaturityDate, CreatedAt + Create/Update signature tail-append
- [ ] `FinancialProfile.cs` — UpsertAccount signature + gọi với named args + ValidateAccountFields thêm 2 rule
- [ ] Tests Domain all green
- [ ] Tests Application mới đã Red
- [ ] `UpsertFinancialAccountCommand.cs` — thêm 2 property + handler UTC-normalize + named args
- [ ] `FinancialAccountDto.cs` — thêm 3 field
- [ ] `PersonalFinanceMapper.cs` — map 3 field
- [ ] Tests Application all green
- [ ] Full `dotnet test` green

### Frontend
- [ ] `personal-finance.service.ts` — DTO + Request thêm 3 field
- [ ] `personal-finance.component.spec.ts` — TẠO MỚI, 4-5 test Red
- [ ] `personal-finance.component.ts`:
  - [ ] form state formDepositDate/formMaturityDate
  - [ ] template savings section thêm 2 date input + preset chips
  - [ ] `setTermMonths()` method mới
  - [ ] `onTypeChange()` null 3 field khi != Savings (fix leak luôn)
  - [ ] `openNewAccountForm()` reset 2 field
  - [ ] `openEditAccountForm()` prefill null-safe
  - [ ] `submitAccountForm()` conditional send
  - [ ] card display update format "dd/MM/yyyy → dd/MM/yyyy"
- [ ] `ng test --watch=false` all green
- [ ] Manual test golden path trong browser

### Docs
- [ ] `docs/business-domain.md` — update entity
- [ ] `docs/features.md` — update feature list
- [ ] `docs/architecture.md` — update nếu có liệt kê field
- [ ] `frontend/src/assets/CHANGELOG.md` — thêm version entry
- [ ] `frontend/src/assets/docs/tai-chinh-ca-nhan.md` — thêm hướng dẫn user

### Commit
- [ ] Commit message English: `feat(personal-finance): add deposit + maturity dates to savings accounts`
- [ ] Body mô tả: 3 backend field, preset chips UX, pre-existing interestRate leak fix, CreatedAt addition
- [ ] Tất cả docs đồng bộ với code trước khi commit (rule CLAUDE.md)

---

## Lưu ý migration data

- Documents FinancialAccount cũ trong Mongo không có `DepositDate`/`MaturityDate`/`CreatedAt` → `IgnoreExtraElementsConvention` + nullable → deserialize thành `null`/`default(DateTime)`.
- `CreatedAt` của document cũ = `DateTime.MinValue` (0001-01-01). Chấp nhận được vì:
  - Không có trường đó trong UI hiện tại
  - Nếu cần, có thể chạy migration sau: backfill `CreatedAt = UpdatedAt` cho docs cũ (ngoài phạm vi PR này)

## Tham chiếu file

Backend:
- [src/InvestmentApp.Domain/Entities/FinancialAccount.cs](../../src/InvestmentApp.Domain/Entities/FinancialAccount.cs)
- [src/InvestmentApp.Domain/Entities/FinancialProfile.cs](../../src/InvestmentApp.Domain/Entities/FinancialProfile.cs) (method UpsertAccount line 68, ValidateAccountFields line 267)
- [src/InvestmentApp.Domain/Entities/Debt.cs](../../src/InvestmentApp.Domain/Entities/Debt.cs) (reference pattern)
- [src/InvestmentApp.Application/PersonalFinance/Commands/UpsertFinancialAccount/UpsertFinancialAccountCommand.cs](../../src/InvestmentApp.Application/PersonalFinance/Commands/UpsertFinancialAccount/UpsertFinancialAccountCommand.cs)
- [src/InvestmentApp.Application/PersonalFinance/Commands/UpsertDebt/UpsertDebtCommand.cs](../../src/InvestmentApp.Application/PersonalFinance/Commands/UpsertDebt/UpsertDebtCommand.cs) (UTC normalize reference line 40-45)
- [src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceDto.cs](../../src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceDto.cs)
- [src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceMapper.cs](../../src/InvestmentApp.Application/PersonalFinance/Dtos/PersonalFinanceMapper.cs)

Backend tests:
- [tests/InvestmentApp.Domain.Tests/](../../tests/InvestmentApp.Domain.Tests/)
- [tests/InvestmentApp.Application.Tests/PersonalFinance/Commands/UpsertFinancialAccountCommandHandlerTests.cs](../../tests/InvestmentApp.Application.Tests/PersonalFinance/Commands/UpsertFinancialAccountCommandHandlerTests.cs)
- [tests/InvestmentApp.Application.Tests/PersonalFinance/Commands/UpsertDebtCommandHandlerTests.cs](../../tests/InvestmentApp.Application.Tests/PersonalFinance/Commands/UpsertDebtCommandHandlerTests.cs) (reference `Handle_MaturityDate_NormalizedToUtcMidnight`)

Frontend:
- [frontend/src/app/features/personal-finance/personal-finance.component.ts](../../frontend/src/app/features/personal-finance/personal-finance.component.ts)
- [frontend/src/app/core/services/personal-finance.service.ts](../../frontend/src/app/core/services/personal-finance.service.ts)

Docs cần update:
- [docs/business-domain.md](../business-domain.md)
- [docs/features.md](../features.md)
- [frontend/src/assets/CHANGELOG.md](../../frontend/src/assets/CHANGELOG.md)
- [frontend/src/assets/docs/tai-chinh-ca-nhan.md](../../frontend/src/assets/docs/tai-chinh-ca-nhan.md)
