# Trade Plans — Tài liệu chi tiết

> **Cập nhật lần cuối:** 2026-03-26
> **Component:** `trade-plan.component.ts` (~2000 dòng)
> **Service:** `trade-plan.service.ts`, `trade-plan-template.service.ts`

---

## 1. Tổng quan

Module Trade Plan cho phép lập kế hoạch giao dịch chi tiết trước khi vào lệnh. Gồm 3 phần chính:

1. **Lập kế hoạch** — Entry/SL/TP, position sizing, checklist
2. **Quản lý vị thế** — Multi-lot, DCA, exit targets
3. **Scenario Playbook** — Kịch bản hành động tự động (nâng cao)

---

## 2. Data Model

### 2.1 TradePlan Entity

**File:** `src/InvestmentApp.Domain/Entities/TradePlan.cs`

| Property | Type | Mô tả |
|----------|------|-------|
| UserId | string | Owner |
| PortfolioId | string? | Danh mục liên kết |
| Symbol | string | Mã CP (uppercase) |
| Direction | string | "Buy" / "Sell" |
| EntryPrice | decimal | Giá vào lệnh |
| StopLoss | decimal | Giá cắt lỗ |
| Target | decimal | Giá mục tiêu |
| Quantity | int | Số lượng CP |
| StrategyId | string? | Chiến lược liên kết |
| MarketCondition | string | "Trending" / "Ranging" / "Volatile" |
| Reason | string? | Lý do vào lệnh |
| Notes | string? | Ghi chú thêm |
| RiskPercent | decimal? | % rủi ro (snapshot) |
| AccountBalance | decimal? | Số dư tài khoản (snapshot) |
| RiskRewardRatio | decimal? | Tỷ lệ R:R (snapshot) |
| ConfidenceLevel | int | Mức tự tin 1-10 |
| Checklist | List\<ChecklistItem\> | Checklist trước lệnh |
| EntryMode | EntryMode? | Single / ScalingIn / DCA |
| Lots | List\<PlanLot\>? | Danh sách lô (multi-lot) |
| ExitTargets | List\<ExitTarget\>? | Mục tiêu thoát lệnh |
| StopLossHistory | List\<StopLossHistoryEntry\>? | Lịch sử thay đổi SL |
| **ExitStrategyMode** | ExitStrategyMode | **Simple / Advanced** |
| **ScenarioNodes** | List\<ScenarioNode\>? | **Cây kịch bản (nâng cao)** |
| Status | TradePlanStatus | Draft→Ready→InProgress→Executed→Reviewed / Cancelled |
| TradeId | string? | Liên kết trade đơn |
| TradeIds | List\<string\>? | Liên kết nhiều trades (multi-lot) |

### 2.2 Status Lifecycle

```
Draft → Ready → InProgress → Executed → Reviewed
  │       │         │
  └───────┴─────────┴── Cancelled
```

### 2.3 Value Objects

**ChecklistItem:** Label, Category, Checked, Critical, Hint
**PlanLot:** LotNumber, PlannedPrice, PlannedQuantity, AllocationPercent, Status (Pending/Executed/Cancelled)
**ExitTarget:** Level, ActionType (TakeProfit/CutLoss/TrailingStop/PartialExit), Price, Quantity, PercentOfPosition
**StopLossHistoryEntry:** OldPrice, NewPrice, Reason, ChangedAt

---

## 3. Entry Modes

### 3.1 Single (Mặc định)
Một lệnh duy nhất tại EntryPrice.

### 3.2 ScalingIn (Chia lô)
Nhiều lô vào tại các giá khác nhau. Presets: 40/30/30, 50/50, Equal.
- Weighted average tự động tính
- Từng lô execute riêng qua `PATCH /lots/{n}/execute`

### 3.3 DCA (Dollar Cost Averaging)
Mua định kỳ theo lịch. Config: amount/period, frequency (weekly/biweekly/monthly), numberOfPeriods, maxPrice/minPrice guards.

---

## 4. Exit Strategy

### 4.1 Chế độ Cơ bản (Simple)

Flat list các ExitTarget. Mỗi target có:
- ActionType: Chốt lời, Cắt lỗ, Trailing Stop, Bán một phần
- Price: Giá trigger
- PercentOfPosition: % vị thế

### 4.2 Chế độ Nâng cao — Scenario Playbook (Advanced)

Cây quyết định (decision tree) với các node liên kết. Mỗi node gồm:

#### Condition Types (Điều kiện)

| Type | Mô tả | ConditionValue |
|------|--------|----------------|
| PriceAbove | Giá >= X | Giá (VNĐ) |
| PriceBelow | Giá <= X | Giá (VNĐ) |
| PricePercentChange | Thay đổi % từ entry | % (dương = tăng, âm = giảm) |
| TrailingStopHit | Trailing stop bị chạm | N/A (tự động từ parent) |
| TimeElapsed | Sau N ngày | Số ngày |

#### Action Types (Hành động)

| Type | Mô tả | ActionValue |
|------|--------|-------------|
| SellPercent | Bán X% vị thế | % |
| SellAll | Bán toàn bộ | N/A |
| MoveStopLoss | Dời SL đến giá | Giá (VNĐ) |
| MoveStopToBreakeven | Dời SL về giá vốn | N/A |
| ActivateTrailingStop | Bật trailing stop | Cần TrailingStopConfig |
| AddPosition | Thêm vị thế | % |
| SendNotification | Chỉ gửi thông báo | N/A |

#### TrailingStopConfig

| Field | Mô tả |
|-------|--------|
| Method | Percentage / ATR / FixedAmount |
| TrailValue | Giá trị trail (%, ATR multiplier, VNĐ) |
| ActivationPrice | Giá bắt đầu trail (null = ngay lập tức) |
| StepSize | Bước tối thiểu để update (anti-whipsaw) |
| CurrentTrailingStop | Giá trailing hiện tại (runtime, tự động cập nhật) |
| HighestPrice | Đỉnh giá kể từ khi kích hoạt (runtime) |

#### Cấu trúc cây

Flat list với `ParentId` references (MongoDB-friendly):
- `ParentId = null` → root node
- `ParentId = "xxx"` → child node, chỉ evaluate khi parent đã Triggered
- Validation: phải có ít nhất 1 root, mọi parentId phải tồn tại

#### Ví dụ cây kịch bản

```
ROOT-1: Giá >= 85,000 → Bán 30%
  ├── Giá >= 85,000 → Dời SL về hòa vốn
  └── Giá >= 90,000 → Bán 50%
       └── Giá >= 90,000 → Bật trailing stop 5%
            └── Chạm trailing → Bán tất cả

ROOT-2: Giá <= 75,000 → Bán tất cả
```

#### Domain Event

`ScenarioNodeTriggeredEvent` — phát ra khi node bị trigger, chứa TradePlanId, NodeId, ActionType, UserId.

---

## 5. Preset Templates

3 mẫu kịch bản hardcoded (không cần DB):

### An toàn (Conservative)
- Chốt 50% tại nửa đường entry→target, chốt hết tại target
- Cắt lỗ toàn bộ tại SL

### Cân bằng (Balanced)
- Chốt 30% tại 60% đến target → Dời SL về hòa vốn
- Chốt 50% tại target → Bật trailing stop 5% → Chốt hết khi chạm trailing
- Cắt lỗ toàn bộ tại SL

### Tích cực (Aggressive)
- Chốt 30% tại target + trailing stop 7% → Chốt hết khi chạm trailing
- Cắt lỗ sớm 50% khi giảm 5%, cắt hết tại SL

---

## 6. Tự động đánh giá (Worker)

**File:** `src/InvestmentApp.Infrastructure/Services/ScenarioEvaluationService.cs`

`ScenarioEvaluationService` chạy trong Worker mỗi 15 phút:

1. Lấy tất cả TradePlan có `ExitStrategyMode == Advanced` và `Status == InProgress`
2. Fetch giá hiện tại cho mỗi symbol
3. Tìm "evaluable nodes": `Status == Pending` AND (root OR parent đã Triggered)
4. Đánh giá điều kiện từng node
5. Nếu trigger → gọi `plan.TriggerScenarioNode()` → lưu plan → tạo `AlertHistory`
6. Update trailing stop data (HighestPrice, CurrentTrailingStop) mỗi cycle

**Alert History:** Mỗi node trigger tạo 1 AlertHistory với `AlertType = "ScenarioPlaybook"`, hiển thị trong trang Cảnh báo.

---

## 7. API Endpoints

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET` | `/api/v1/trade-plans` | Danh sách plans (query: `activeOnly`) |
| `GET` | `/api/v1/trade-plans/{id}` | Chi tiết plan |
| `POST` | `/api/v1/trade-plans` | Tạo plan mới |
| `PUT` | `/api/v1/trade-plans/{id}` | Cập nhật plan |
| `PATCH` | `/api/v1/trade-plans/{id}/status` | Thay đổi status |
| `PATCH` | `/api/v1/trade-plans/{id}/lots/{n}/execute` | Execute lô |
| `PATCH` | `/api/v1/trade-plans/{id}/stop-loss` | Cập nhật SL (có history) |
| `PATCH` | `/api/v1/trade-plans/{id}/exit-targets/{level}/trigger` | Trigger exit target |
| `PATCH` | `/api/v1/trade-plans/{id}/scenario-nodes/{nodeId}/trigger` | Trigger scenario node |
| `GET` | `/api/v1/trade-plans/scenario-templates` | Lấy preset templates |

---

## 8. Frontend UI

### Layout 3 cột (desktop)

**Cột trái (2/3):**
- Thiết lập giao dịch (symbol, direction, entry/SL/TP, quantity, strategy, market condition)
- Lot Editor (khi ScalingIn)
- DCA Editor (khi DCA)
- **Chiến lược thoát lệnh** — toggle Cơ bản/Nâng cao
  - Cơ bản: flat exit targets (giữ nguyên cũ)
  - Nâng cao: scenario tree editor + preset selector
- Entry Reason, Confidence Level, Notes
- Mini Stock Info, Risk Profile, Strategy Rules

**Cột phải (1/3):**
- Position Sizing Results
- Quick Metrics (R:R, risk/share, potential P&L)
- Pre-trade Checklist (13 items, 4 categories, Go/No-Go)
- Save/Submit buttons
- Order Sheet (phiếu lệnh)

### Scenario Tree Editor

- Recursive `ng-template` (không tạo component riêng)
- Color scheme: **indigo** (phân biệt với violet của exit targets)
- Mỗi node: select condition + input value + select action + input actionValue + label
- Nút "+Con" để thêm child node
- Inline trailing stop config khi action = ActivateTrailingStop
- Preset template selector + "Áp dụng"

### Saved Plans Panel

- Filter tabs: Tất cả / Nháp / Sẵn sàng / Đang chờ / Đã thực hiện
- Desktop table + Mobile cards
- Actions per plan: AI Advisor, Delete, Mark Ready, View Replay, Mark Reviewed, Cancel

---

## 9. Backward Compatibility

- `ExitStrategyMode` mặc định = `Simple` → tất cả plan cũ không bị ảnh hưởng
- `ScenarioNodes` = null khi Simple → không tốn storage
- Create/Update API: fields mới là optional → client cũ không break
- Chuyển Simple ↔ Advanced **không xóa dữ liệu** mode kia

---

## 10. Tests

| Layer | File | Số test |
|-------|------|---------|
| Domain | `TradePlanTests.cs` | ~30 (existing) |
| Domain | `TradePlanScenarioTests.cs` | 20 |
| Application | `TriggerScenarioNodeCommandHandlerTests.cs` | 3 |
| Infrastructure | `ScenarioEvaluationServiceTests.cs` | 10 |
