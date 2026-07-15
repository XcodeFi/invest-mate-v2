# Kế hoạch: Mục tiêu tài chính + Forecast + Actual (Personal Fund — Plan/Forecast/Actual)

> **Trạng thái:** 📋 Planned — chưa triển khai
> **Ngày tạo:** 2026-05-05
> **Ưu tiên:** Tier 3 (mở rộng "Tài chính cá nhân" đã hoàn thành 2026-04-22)
> **Dự kiến:** ~14 file mới + 6 file modify, ~65 tests, ~8.6 person-days, chia **4 phase** (V1 → V4)
> **Framing:** Vận hành tài chính cá nhân như **một quỹ tài chính quy mô cá nhân** (one-person family office). User là CFO của bản thân. Flow: **Plan → Forecast → Actual**.
>
> Kế hoạch này hợp nhất từ **3-agent review** (Architect / UX / Domain-Risk) ngày 2026-05-05.
> Quyết định đã chốt:
>
> - **Q1:** Embed `Goals: List<Goal>` trong `FinancialProfile` (precedent từ `Debts`); snapshot tách riêng collection `goal_progress_snapshots` ✅
> - **Q2:** Multi-goal contribution overlap → **hard partition** bằng `AllocationVnd` per account, sum-≤-balance enforced tại save (chống double-count) ✅
> - **Q3:** Phase split V1 (MVP) → V2 (snapshot + history) → V3 (forecast) → V4 (rule gates) — mỗi phase có trial window 1 tuần ✅
> - **Q4:** Rule-conflict gate copy pattern `DISCIPLINE_GATE_FAILED` 400 từ Vin-discipline V1 ✅
> - **Q5:** CAGR per-account-type mặc định (CK 8% / Savings = user avg fallback 5% / Gold 6% / Emergency+IdleCash 0%), goal blend từ tỷ trọng contributing accounts — user không nhập số, system reveal số ✅

---

## Bối cảnh

### Tại sao cần Goals layer?

Phần "Tài chính cá nhân" hiện tại đã trả lời được câu **"hôm nay tôi đang đứng ở đâu?"** — Net Worth, asset allocation, health score, debt. Nhưng còn 3 câu chưa trả lời:

1. **"Đi đâu?"** — Mục tiêu tài chính rõ ràng (mua nhà 2028, hưu trí 2045, học phí con 2030).
2. **"Bao giờ tới?"** — Dự đoán hoàn thành (forecast) dựa trên balance + tốc độ tích lũy + CAGR giả định.
3. **"Đang đúng tiến độ chưa?"** — Thực tế % đạt vs % "đáng lẽ phải đạt" theo lịch tuyến tính.

3 câu này là khung tiêu chuẩn của **báo cáo tài chính + kế hoạch năm** mà mọi quỹ/công ty đều có. App đang có asset allocation (= phần "đứng ở đâu"); thiếu Plan → Forecast → Actual để ghép thành quỹ đầy đủ.

### Pain point của state hiện tại

Soi `src/InvestmentApp.Domain/Entities/FinancialProfile.cs`:

| Hiện trạng | Vấn đề |
|---|---|
| Có `Accounts: List<FinancialAccount>` (5 loại) + `Debts: List<Debt>` | Không có entity nào trả lời "muốn đạt cái gì, bao giờ" |
| `CalculateHealthScore` chỉ dựa rule compliance hiện tại | Không có chiều thời gian — không trả lời "đang đi đúng hướng không" |
| Chưa có lịch sử Net Worth (chỉ có `portfolio_snapshots` cho Securities slice) | Không có dữ liệu cho forecast accumulation rate |
| Chưa có cron job snapshot toàn FinancialProfile | Phải thêm mới |

**Kết luận:** App đang là "ảnh chụp hôm nay"; cần thêm **trục thời gian + đích đến** để thành "lộ trình".

### 3 phát hiện quan trọng từ 3-agent review

1. **Securities + Gold balance fluctuate liên tục** → forecast inputs phải dùng **30-day SMA**, không raw spot. Nếu raw, user thấy "sớm 4 tháng" sáng nay, "trễ 2 tháng" mai sáng (gold rally). Family-office software (Addepar, Black Diamond) mark-to-market cho reporting nhưng dùng smoothed values cho projection.
2. **Cần monthly cron mới** (`/internal/jobs/networth-snapshot`) — pattern `InternalJobsController` + Cloud Scheduler đã có. Đây là PRE-REQUISITE cho V3 Forecast.
3. **Reuse triệt để** [GetSavingsComparisonQuery.cs](../../src/InvestmentApp.Application/Analytics/Queries/GetSavingsComparison/GetSavingsComparisonQuery.cs) cho CAGR resolution + `RateSource` enum + `[-10%, +50%]` sanity caps. Pattern đã prove trong production, đừng reinvent.

---

## 1. Domain — Entity `Goal`

**File mới:** `src/InvestmentApp.Domain/Entities/Goal.cs`
**File modify:** `src/InvestmentApp.Domain/Entities/FinancialProfile.cs` (thêm `Goals: List<Goal>`)

```
Goal : Entity (embedded trong FinancialProfile.Goals)
├── Id (GUID)
├── Name (string, required) — "Quỹ mua nhà"
├── Purpose (GoalPurpose enum) — House | Education | Retirement | Travel | Emergency | Other
├── TargetAmount (decimal, > 0)
├── Deadline (DateTime, UTC date)
├── Allocations: List<GoalAccountAllocation> (embedded)
│   ├── AccountId (GUID, ref FinancialAccount.Id)
│   ├── AllocationVnd (decimal, ≥ 0) — số tiền THỰC TẾ phân bổ
│   └── Note?
├── CagrAssumption (decimal?) — null = fallback to per-account-type defaults
├── Status (GoalStatus enum) — Active | Achieved | Expired | Abandoned
├── CompletedAt (DateTime?) — set khi Achieved/Abandoned
├── Priority (int, default 0) — không dùng V1, dự phòng V2 cho cascade allocation
├── CreatedAt, UpdatedAt
└── Methods:
    ├── GetCurrentAmount(IDictionary<accountId, balance>) → sum(allocations matching)
    ├── GetProgressPercent(currentAmount) → currentAmount / TargetAmount × 100
    ├── GetLinearExpectedPercent(now) → monthsElapsed / totalMonths × 100
    ├── IsExpired(now) → now > Deadline && Status == Active
    ├── MarkAchieved(now), MarkAbandoned(now), Reactivate()
    └── ValidateAllocations(profile) → throw nếu sum(allocationVnd per accountId) > account.Balance
```

### Status state machine

```
Active ──(deadline passed)──> Expired (auto-flip on read)
Active ──(currentAmount ≥ target)──> Achieved (auto-suggest, user confirm hoặc cron flip)
Active ──(user explicit)──> Abandoned
Expired ──(user extend deadline)──> Active
```

**Auto-flip rule (trong `GetGoalsQuery` handler):** `IsExpired()` → mark Expired in result DTO **nhưng không persist**; persist chỉ khi user explicit action (click "Gia hạn / Bỏ"). Pattern này tránh background mutation, copy từ Vin-discipline `GetPendingThesisReviewsQuery`.

### Multi-goal contribution overlap — hard partition

Field `Allocations: List<GoalAccountAllocation>` chứa **AccountId cụ thể** (không phải AccountType). Constraint:

> Với mỗi `accountId`, tổng `AllocationVnd` qua tất cả Goal của user **≤ FinancialAccount.Balance** tại thời điểm save.

Validate trong `FinancialProfile.UpsertGoal(...)` — throw `DomainException` với message tiếng Việt: *"Tổng phân bổ cho tài khoản 'TK VCB' (X tr) vượt quá số dư hiện tại (Y tr). Giảm phân bổ ở Mục tiêu khác hoặc tăng số dư tài khoản."*

**Lý do chọn hard partition** (vs allow overlap với warning):
- Project memory đã có rule "primary button right" / "destructive cứng" → phong cách project là **strict gates over soft warnings**.
- Allow overlap = silent double-count → user thấy "tổng tiến độ goals = 150% net worth" → mất niềm tin.
- 2/3 agent (Architect + Domain) recommend hard partition. UX agent worries phức tạp, mitigate bằng default = "Tự động phân bổ đều giữa các Goal share cùng tài khoản" tại UI layer.

### Edge cases trong domain

- **Goal đã Achieved nhưng deadline còn xa** → freeze `Allocations` snapshot khi MarkAchieved (không trừ tự động). Hiển thị 90 ngày rồi auto-archive. Không tự xóa.
- **Goal đã Expired nhưng chưa đạt** → giữ status, vẫn compute forecast (hiển thị "trễ X tháng so với hạn đã qua"), user explicit Gia hạn / Bỏ.
- **User xóa account đang có Allocation** → block xóa (`RemoveAccount` throws nếu account còn allocation), ép user rebalance trước.

---

## 2. Application — Commands, Queries, Forecast Service

### Commands & Queries (dạng CQRS, mirror PF cũ)

| Class | Type | Tóm tắt |
|---|---|---|
| `UpsertGoalCommand` | Command | Create + update (`goalId? null = create`); validate sum-allocation ≤ balance |
| `RemoveGoalCommand` | Command | Delete; chặn nếu Status == Achieved (require explicit Abandon trước) |
| `MarkGoalAchievedCommand` | Command | User explicit "đánh dấu hoàn thành" |
| `AbandonGoalCommand` | Command | User explicit "bỏ mục tiêu" với reason text |
| `ExtendGoalDeadlineCommand` | Command | Expired → Active với deadline mới |
| `GetGoalsQuery` | Query | Returns goals + forecast inline (single round-trip cho list view) |
| `GetGoalQuery` | Query | Single goal full detail + forecast breakdown |
| `GetGoalSnapshotsQuery` | Query | Lịch sử snapshot per goal cho Actual chart, filter `from/to` |
| `GetGoalsSummaryQuery` | Query | Lightweight: `{ activeCount, onTrackCount, behindCount, achievedCount }` cho Dashboard widget |

### `IGoalForecastService` (interface)

**File mới:** `src/InvestmentApp.Application/Common/Interfaces/IGoalForecastService.cs`

```csharp
public interface IGoalForecastService
{
    Task<GoalForecast> ComputeAsync(Goal goal, FinancialProfile profile, CancellationToken ct);
}

public record GoalForecast(
    decimal CurrentAmount,
    decimal TargetAmount,
    decimal ProgressPercent,
    decimal LinearExpectedPercent,
    decimal? VarianceFromLinear,        // ProgressPercent - LinearExpectedPercent
    DateTime? ForecastedCompletionDate, // null nếu InsufficientHistory hoặc NegativeAccumulation
    int? MonthsAheadOrBehind,            // negative = behind
    decimal BlendedCagr,
    decimal MonthlyAccumulationRate,
    GoalForecastStatus Status,           // Active | Achieved | Expired | InsufficientHistory | NegativeAccumulation
    string? RateSource                    // "user-savings-avg" / "fallback-CK-8" / "blended-from-allocations"
);
```

**Implementation** (V3): `src/InvestmentApp.Infrastructure/Services/GoalForecastService.cs`

### Forecast formula

Inputs:
1. **Current allocated balance** = sum của `Allocations[].AllocationVnd` (đã hard-partitioned). Cho account type Securities, dùng `IPnLService.CalculatePortfolioPnLAsync().TotalMarketValue × allocation.weight`. Smooth bằng **30-day SMA** trên `goal_progress_snapshots`.
2. **Monthly accumulation rate** = mean của Δ contribution-portion per month qua window 6 tháng gần nhất, **min 3 tháng** sample. Tách contribution (CapitalFlow Deposit/Withdraw) khỏi market gains (delta của Securities slice). Reuse logic từ [GetSavingsComparisonQuery](../../src/InvestmentApp.Application/Analytics/Queries/GetSavingsComparison/GetSavingsComparisonQuery.cs). Winsorize p10/p90 để khử windfall outliers.
3. **CAGR blended** từ `Allocations`:
   - Securities portion: 8% (haircut từ 11% VN-Index 10y nominal)
   - Savings portion: weighted-avg user `InterestRate` qua các Savings account, fallback `DefaultFallbackRate = 0.05m`
   - Gold portion: 6%
   - Emergency + IdleCash: 0%
   - User override: `Goal.CagrAssumption` thắng nếu set
4. Solve `t` cho: `principal × (1 + cagr/12)^t + monthlyContrib × annuityFactor(cagr, t) >= target`. Nếu `cagr=0` → linear `t = (target − principal) / monthlyContrib`.

**Status returned:**
- `InsufficientHistory` nếu `goal_progress_snapshots` < 3 entries → UI hiển thị "Đang thu thập dữ liệu (X/3 tháng), dự báo sẵn sàng từ MM/YYYY".
- `NegativeAccumulation` nếu monthly rate ≤ 0 → UI hiển thị "Không đủ xu hướng tăng để dự báo. Xem lại đóng góp định kỳ".
- `Achieved` / `Expired` / `Active` theo state machine.

### Reuse pattern bắt buộc

| Existing | Reuse cho Goals |
|---|---|
| `GetSavingsComparisonQueryHandler` `DefaultFallbackRate=0.05m` + `RateSource` enum + `[-10%, +50%]` sanity caps | CAGR resolution + disclaimer |
| `FinancialProfile.GetTotalAssets(securitiesValue)` + `IPnLService` plumbing | Securities current value (KHÔNG dùng `Accounts.Sum(a => a.Balance)` — sẽ undercount CK) |
| `FinancialProfile.HasHighInterestConsumerDebt()` | V4 debt-vs-goal nudge |
| `FinancialRules.MaxInvestmentPercent` | V4 rule-conflict gate |
| `PortfolioSnapshotEntity` daily snapshot pattern | `GoalProgressSnapshotEntity` monthly snapshot |
| Vin-discipline `DISCIPLINE_GATE_FAILED` 400 pattern | V4 `GOAL_RULE_CONFLICT` gate |
| Debt section click-to-edit + ESC + Lưu phải | Goal CRUD modal |

---

## 3. Infrastructure — Snapshot Repo + Cron Job

### File mới

| File | Mục đích |
|---|---|
| `src/InvestmentApp.Domain/Entities/GoalProgressSnapshotEntity.cs` | Entity cho monthly snapshot |
| `src/InvestmentApp.Domain/Repositories/IGoalProgressSnapshotRepository.cs` | Interface |
| `src/InvestmentApp.Infrastructure/Repositories/GoalProgressSnapshotRepository.cs` | Mongo impl, collection `goal_progress_snapshots`, compound unique index `(UserId, GoalId, SnapshotDate)` |
| `src/InvestmentApp.Infrastructure/Services/GoalForecastService.cs` | V3 — implementation `IGoalForecastService` |
| `src/InvestmentApp.Infrastructure/Services/NetWorthSnapshotJobService.cs` | Monthly cron — snapshot toàn `FinancialProfile` + per-goal progress |

### Snapshot schema

```
goal_progress_snapshots (collection)
{
  _id: ObjectId,
  Id: string GUID,
  UserId: string,
  GoalId: string,
  SnapshotDate: DateTime (date-only, UTC midnight, day-1 of month),
  CurrentAmount: decimal,
  TargetAmount: decimal,
  ProgressPercent: decimal,
  LinearExpectedPercent: decimal,
  ForecastedCompletionDate: DateTime?,
  MonthlyAccumulationRate: decimal,
  BlendedCagr: decimal,
  CreatedAt: DateTime
}
```

**Index:** `{ UserId: 1, GoalId: 1, SnapshotDate: 1 }` unique. Defensive `try/catch` code 85/86 trong constructor (precedent từ `FinancialProfileRepository`).

### Cron job

- **Endpoint:** `POST /internal/jobs/networth-snapshot` trên [InternalJobsController](../../src/InvestmentApp.Api/Controllers/InternalJobsController.cs)
- **Trigger:** Cloud Scheduler, monthly tại `0 0 1 * *` (00:00 ngày 1 hàng tháng, Asia/Ho_Chi_Minh)
- **OIDC auth:** giống các internal jobs hiện có
- **Idempotent:** nếu `(UserId, GoalId, SnapshotDate)` đã tồn tại → upsert (replace), không double-insert
- **Logic:** loop tất cả `FinancialProfile` → cho mỗi `Goal` Active → compute `GoalForecast` → lưu snapshot
- **Logging:** số user processed, số goal snapshot, errors. Pattern từ existing `/internal/jobs/snapshot` (portfolio).

**Cloud Scheduler config:** `cloud-scheduler.yaml` thêm 1 entry. Nhắc trong checklist deploy.

### Migration

**File mới:** `scripts/migrations/2026-MM-DD-financial-profile-goals-init.mongo.js` (idempotent, copy template từ [tradeplan-thesis-rename](../../scripts/migrations/2026-04-23-tradeplan-thesis-rename.mongo.js))

```javascript
// 1. Add Goals: [] field (idempotent)
db.financial_profiles.updateMany(
  { Goals: { $exists: false } },
  { $set: { Goals: [] } }
);

// 2. Add Rules.DefaultCagrAssumption (V3 prerequisite)
db.financial_profiles.updateMany(
  { "Rules.DefaultCagrAssumption": { $exists: false } },
  { $set: { "Rules.DefaultCagrAssumption": NumberDecimal("8.0") } }
);

// 3. Create goal_progress_snapshots collection + index (handled by repo constructor, but manual fallback)
db.goal_progress_snapshots.createIndex(
  { UserId: 1, GoalId: 1, SnapshotDate: 1 },
  { unique: true, name: "uniq_user_goal_date" }
);
```

C# driver auto-deserialize missing field → empty list (precedent: Debt addition không cần migration). Script chạy trước cũng OK.

---

## 4. API — Extend `PersonalFinanceController`

**File modify:** `src/InvestmentApp.Api/Controllers/PersonalFinanceController.cs` (giữ prefix `/api/v1/personal-finance`, JWT auth, `User.FindFirst("sub")`)

| Method | Endpoint | Handler |
|---|---|---|
| GET | `/goals` | `GetGoalsQuery` — list + forecast inline |
| GET | `/goals/{goalId}` | `GetGoalQuery` |
| PUT | `/goals` | `UpsertGoalCommand` (goalId? null = create) |
| PATCH | `/goals/{goalId}/achieved` | `MarkGoalAchievedCommand` |
| PATCH | `/goals/{goalId}/abandoned` | `AbandonGoalCommand` (body: reason) |
| PATCH | `/goals/{goalId}/extend` | `ExtendGoalDeadlineCommand` (body: newDeadline) |
| DELETE | `/goals/{goalId}` | `RemoveGoalCommand` |
| GET | `/goals/{goalId}/snapshots?from=&to=` | `GetGoalSnapshotsQuery` |
| GET | `/goals/summary` | `GetGoalsSummaryQuery` — Dashboard widget |

**File modify:** `src/InvestmentApp.Api/Controllers/InternalJobsController.cs` — thêm action `POST /internal/jobs/networth-snapshot`.

### Error mapping

- `GOAL_ALLOCATION_EXCEEDS_BALANCE` → 400 với message Vietnamese
- `GOAL_RULE_CONFLICT` (V4) → 400 với detail object `{ rule, currentValue, requiredValue }` để FE confirm override
- `GOAL_DEADLINE_PASSED` (khi cố tạo goal với deadline trong quá khứ) → 400
- `GOAL_NOT_FOUND` → 404

---

## 5. Frontend — `/personal-finance` page extension

**File modify:** `frontend/src/app/features/personal-finance/personal-finance.component.ts`

### Vị trí section "Mục tiêu"

Insert giữa **#6 Sức khỏe tài chính** và **#7 Tài khoản**. Logic: trang đọc top-down theo flow **state → diagnosis → action**:

```
Header
Onboarding (conditional)
Net Worth row
High-interest debt banner
Asset breakdown (4 cards)
Sức khỏe tài chính
🆕 Mục tiêu  ◀── insert here
Tài khoản
Khoản nợ
Settings (collapsed)
```

Goals nằm sau Health Score (= "đang khoẻ chưa") và trước Accounts (= "raw line items"), khớp với mental model "purpose trước ledger".

### Goal card layout (one per goal)

```
┌──────────────────────────────────────────────────────┐
│ 🎯 Mua nhà tại Q.7                          ⚙️       │
│ 2.500.000.000 đ          Hạn: 12/2030 (4y 8m left)  │
│                                                       │
│ Đã đạt: 450.000.000 đ (18%)                          │
│ [████████░░░░░░░░░░░░░░░░░░] 18% actual              │
│        ▲ 22% theo lịch                               │
│                                                       │
│ 🔴 Chậm 4% so với kế hoạch                           │
│ Dự kiến hoàn thành: 03/2031 (trễ 3 tháng)           │
│                                                       │
│ Đóng góp: 🏦 TK VCB (200tr) · 💵 Nhàn rỗi (50tr)    │
│           · 📈 CK (200tr)                            │
│                                                       │
│ [Xem lịch sử ▾]                                      │
└──────────────────────────────────────────────────────┘
```

**Single bar với secondary tick-marker** cho linear-schedule expected % (cleaner than two stacked bars on mobile). Color:
- Emerald: variance ≥ 0 (đúng/sớm)
- Amber: -5% < variance < 0
- Red: variance ≤ -5%

Click card → modal edit. Click "Xem lịch sử" → expand inline lightweight-charts canvas (180px height, sparkline-style, no legend) với 3 line:
- Solid line: Actual % qua tháng
- Dashed line: Linear expected %
- Dotted line projection: Forecast extrapolated tới deadline

### Empty state

Match gradient card ở `personal-finance.component.ts:43-62` (onboarding style):

> 🎯 **Đặt mục tiêu tài chính đầu tiên**
> Mua nhà, học phí, hưu trí — nhập đích đến để theo dõi tiến độ.
> [Tạo mục tiêu]

### Goal CRUD modal

Pattern: **fixed inset-0 bg-black/70 z-[60]**, click-outside-to-close, ESC key close, button order [Hủy] → [Xóa?] → [Lưu] với primary right (memory rule). Copy nguyên từ Account modal hoặc Debt modal.

Fields required:
- Tên mục tiêu (text, max 80 chars)
- Mục đích (dropdown enum)
- Số tiền mục tiêu (`appNumMask`, > 0)
- Hạn (input type="date", default = today + 5 years, must be in future)
- Phân bổ tài khoản (table: AccountId × AllocationVnd, mỗi row có "%" của account balance để gợi ý)

Helper text: *"Số tiền phân bổ là phần thực tế của mỗi tài khoản dành cho mục tiêu này. Tổng phân bổ qua các mục tiêu không được vượt số dư tài khoản."*

Advanced disclosure (`<details>`):
- CAGR override (default ẩn, hiển thị blended CAGR đã tính)
- Priority (V2)

### Nudge text examples (Vietnamese)

| Status | Message |
|---|---|
| On-track (variance ±5%) | ✅ Đúng tiến độ |
| Slightly behind | 🟡 Chậm 3% so với kế hoạch |
| Behind | 🔴 Chậm 8% — cần thêm ~5tr/tháng để kịp hạn |
| Ahead | 🚀 Sớm 3 tháng so với hạn — dự kiến hoàn thành 09/2030 |
| Achieved | 🎉 Đạt mục tiêu! Hoàn thành 02/2030 (trước hạn 10 tháng) |
| Expired | ⚠️ Quá hạn — chưa đạt. [Gia hạn] [Đánh dấu bỏ] |
| Insufficient history | ⏳ Đang thu thập dữ liệu (2/3 tháng) — dự báo sẵn sàng từ 07/2026 |
| Negative accumulation | 📉 Giai đoạn này chưa có xu hướng tăng — xem lại đóng góp định kỳ |

### Dashboard widget update

**File modify:** `frontend/src/app/features/dashboard/dashboard.component.ts` (PF widget block lines 271-319)

Thêm **1 dòng** trên breakdown:
```
🎯 2/3 mục tiêu đúng tiến độ →
```
Clickable, route tới `/personal-finance#goals`. Ẩn nếu user có 0 goals.

KHÔNG thêm widget riêng — PF widget đã info-dense, 1 dòng là right increment.

---

## 6. Tests

| Layer | Test file | Số tests |
|---|---|---|
| Domain | `Goal.cs` state machine, `ValidateAllocations`, `MarkAchieved/Abandoned/Reactivate` | ~12 |
| Domain | `FinancialProfile.UpsertGoal/RemoveGoal` validation + cross-account allocation check | ~8 |
| Domain | `GoalProgressSnapshotEntity` validation | ~5 |
| Application | 5 commands × happy path + 2-3 error paths | ~20 |
| Application | 4 queries × happy path + edge (insufficient history, negative accumulation) | ~10 |
| Infrastructure | `GoalForecastService` formula correctness (linear, compound, edge cases) | ~10 |
| Infrastructure | `NetWorthSnapshotJobService` idempotency + multi-user loop | ~5 |
| API | Controller × 9 endpoints integration smoke | ~5 |
| **Total backend** | | **~75 tests** |
| Frontend | Spec smoke (page render, modal open/close, form validation) | tuỳ — defer if PF page chưa có FE specs |

---

## 7. Phase split (V1 → V4)

Mirror PF original 6-cycle precedent. Mỗi phase = 1 PR reviewable < 30 phút, có **trial window 1 tuần** trước phase tiếp theo (memory: `feedback_trial_window_pattern.md`).

### Phase V1 — Goals MVP (~3 ngày)

**Goal:** User tạo được Goal + thấy progress đơn giản (current allocated / target). KHÔNG forecast, KHÔNG history chart, KHÔNG cron.

Scope:
- Domain: `Goal` entity (chưa có cron-related fields), state machine, `Allocations` validation
- Application: `UpsertGoal/RemoveGoal/MarkGoalAchieved/AbandonGoal/ExtendGoalDeadline`, `GetGoalsQuery` (forecast=null), `GetGoalsSummaryQuery`
- API: 7 endpoints (skip `/snapshots`, `/internal/jobs/networth-snapshot`)
- Frontend: section + modal + simple progress bar (no secondary marker, no chart)
- Migration script step 1 only (`Goals: []` field add)

**Success criteria:**
- User tạo 3 goals, allocation hợp lệ, progress hiển thị đúng
- Hard partition validation throws đúng tiếng Việt
- 1055 + ~30 = ~1085 tests pass

**Trial window 1 tuần** — user dùng thực tế, ghi chú UX friction.

### Phase V2 — Snapshot cron + Actual history chart (~2 ngày)

**Goal:** Có lịch sử Actual % qua tháng để vẽ chart "đường thực tế vs đường tuyến tính".

Scope:
- Domain: `GoalProgressSnapshotEntity`
- Application: `GetGoalSnapshotsQuery`
- Infrastructure: `GoalProgressSnapshotRepository` + `NetWorthSnapshotJobService` (compute current + linear, không forecast)
- API: `GET /goals/{id}/snapshots` + `POST /internal/jobs/networth-snapshot`
- Cloud Scheduler: thêm entry monthly
- Frontend: "Xem lịch sử" expand + lightweight-charts (Solid Actual + Dashed Linear, no Forecast yet)

**Success criteria:**
- Cron chạy thủ công lần đầu, snapshot tất cả Active goals của test user
- Chart hiển thị đúng 2 đường, mobile responsive
- Idempotent re-run không double-insert

**Trial window 1 tuần** — chờ ít nhất 1 tháng để có > 1 snapshot mới đẹp data.

### Phase V3 — Forecast (~2 ngày)

**Goal:** Compute projected completion date với 30-day SMA + accumulation rate split + blended CAGR.

Scope:
- Application: `IGoalForecastService` interface
- Infrastructure: `GoalForecastService` impl
  - Plug `IPnLService` cho Securities live value
  - Plug `ICapitalFlowRepository` cho contribution rate (tách khỏi market gains)
  - 30-day SMA trên `goal_progress_snapshots`
  - Winsorize p10/p90 cho contribution rate
  - Blended CAGR per-account-type
  - `[-10%, +50%]` sanity caps copy từ SavingsComparison
- Migration step 2: `Rules.DefaultCagrAssumption = 8.0`
- Application: `GetGoalsQuery` returns forecast inline (replace null)
- API: forecast in response DTOs
- Frontend: nudge text (8 status messages), secondary tick-marker, dotted forecast line on chart

**Success criteria:**
- Forecast formula tests pass (linear cagr=0, compound cagr>0, insufficient history, negative accumulation)
- `RateSource` enum hiển thị trên UI khi click "ⓘ" — user thấy được CAGR tính từ đâu
- 30-day SMA stabilize gold/CK fluctuation (test với mock price spike)

**Trial window 1-2 tuần** — quan sát forecast có nhảy bậy không khi thị trường biến động.

### Phase V4 — Rule-conflict gate + debt-vs-goal nudge (~0.5 ngày)

**Goal:** Bảo vệ user khỏi tự tạo goal vi phạm Financial Rules đã chốt.

Scope:
- Application: `UpsertGoalCommand` thêm validation:
  - Compute % allocated to Securities = sum(Allocations cho Securities accounts) / GetTotalAssets
  - Nếu vượt `FinancialRules.MaxInvestmentPercent` → throw `GOAL_RULE_CONFLICT` 400 với detail object
  - User confirm override (FE pass `acceptRuleOverride: true` trong request body)
- API: 400 response shape `{ code: "GOAL_RULE_CONFLICT", rule, currentValue, requiredValue }` để FE hiển thị modal confirm
- Frontend: modal confirm pattern copy Vin-discipline `DISCIPLINE_GATE_FAILED`
- Frontend: Inline banner trong Goal modal khi `HasHighInterestConsumerDebt() == true`:
  > "Bạn đang nợ tiêu dùng lãi > 20%. Trả nợ này thường là 'mục tiêu' lãi kép tốt nhất — cân nhắc tạo Mục tiêu trả nợ trước."

**Success criteria:**
- Test: tạo goal vi phạm rule → 400 với code đúng
- Test: confirm override → 200, log override
- UI test (manual): banner hiển thị khi user có nợ tiêu dùng lãi cao

---

## 8. Risk register

| Risk | Mitigation |
|---|---|
| Securities/Gold spot fluctuate → forecast nhảy bậy | 30-day SMA bắt buộc cho V3 inputs (giữ raw spot cho card "current value" header thôi) |
| User có < 3 tháng history → forecast = garbage | Status `InsufficientHistory`, hiển thị "đang thu thập (X/3)", KHÔNG fake forecast |
| 2 goal share 1 account → double-count | Hard partition `Allocations[].AllocationVnd` per account, validate sum-≤-balance tại save |
| Goal expired không xử lý | Auto-flip `Expired` on read, user explicit "Gia hạn" / "Bỏ" — không silent mutation |
| User xóa account còn allocation | `RemoveAccount` throw, ép user rebalance trước |
| Inflation: 2 tỷ 2028 ≠ 2 tỷ today | Defer V5 — toggle "Điều chỉnh theo lạm phát 4%/năm" áp lên target |
| CAGR optimistic → user trust giả | `RateSource` enum hiển thị trên UI; `[-10%, +50%]` caps; per-account-type defaults haircut từ historical |
| Cron job fail silently | OIDC + structured log + alert pattern từ existing `/internal/jobs/*` |
| Migration prod → dev divergence | Idempotent script chạy trước khi deploy code (precedent từ Vin-discipline migration-first gate) |

---

## 9. Definition of Done

V1 done khi:
- [ ] All ~30 tests pass
- [ ] Migration script chạy trên prod replica → no errors, idempotent
- [ ] User tạo 3 goals, edit, mark achieved, abandon, extend — flow mượt
- [ ] Hard partition error message tiếng Việt đúng
- [ ] PR review qua 3-agent (architect + ux + risk)
- [ ] [docs/architecture.md](../architecture.md) update với 5 file mới + endpoint
- [ ] [docs/business-domain.md](../business-domain.md) update entity map (Goal embedded)
- [ ] [docs/features.md](../features.md) update phase entry
- [ ] [docs/project-context.md](../project-context.md) move backlog item → Done với link plan này
- [ ] [frontend/src/assets/CHANGELOG.md](../../frontend/src/assets/CHANGELOG.md) entry
- [ ] User-facing help doc trong `frontend/src/assets/docs/` + register Help topic (memory rule: `feedback_always_update_all_docs.md`)

V2/V3/V4 done khi same checklist + scope-specific success criteria.

---

## 10. Tham chiếu

- [3-agent review 2026-05-05] Architect / UX / Domain-Risk — quyết định trong header file này.
- Existing reuse:
  - [FinancialProfile.cs](../../src/InvestmentApp.Domain/Entities/FinancialProfile.cs) — invariants + `HasHighInterestConsumerDebt`
  - [GetSavingsComparisonQuery.cs](../../src/InvestmentApp.Application/Analytics/Queries/GetSavingsComparison/GetSavingsComparisonQuery.cs) — CAGR pattern + `RateSource`
  - [PortfolioSnapshotEntity.cs](../../src/InvestmentApp.Domain/Entities/PortfolioSnapshotEntity.cs) — snapshot precedent
  - [InternalJobsController.cs](../../src/InvestmentApp.Api/Controllers/InternalJobsController.cs) — cron pattern
  - [PersonalFinanceController.cs](../../src/InvestmentApp.Api/Controllers/PersonalFinanceController.cs) — controller extension target
  - [PersonalFinance done plan](done/personal-finance.md), [Debt done plan](done/personal-finance-debt.md) — predecessor plans
  - [Vin-discipline plan](plan-creation-vin-discipline.md) — `DISCIPLINE_GATE_FAILED` pattern + migration-first deploy gate

---

> **Ghi chú quan trọng:** Plan này KHÔNG thay V2.2 (ThesisReviewService cron) đang là next-up của Vin-discipline. Hai cron job độc lập, không conflict. Nếu chốt làm Goals trước, V2.2 lùi 1-2 tuần.
