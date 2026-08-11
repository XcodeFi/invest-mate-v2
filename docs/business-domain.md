# Investment Mate v2 — Bản đồ Nghiệp vụ

> Tài liệu tham chiếu nhanh cho AI agents và developers mới.
> Cập nhật lần cuối: 2026-08-10

---

## 1. Tổng quan

Investment Mate v2 là hệ thống **quản lý danh mục đầu tư chứng khoán** hướng đến nhà đầu tư cá nhân tại Việt Nam. Trọng tâm: **kỷ luật giao dịch** — lập kế hoạch trước, thực thi theo kế hoạch, ghi nhật ký sau.

**Luồng chính:**
```
Chiến lược → Kế hoạch GD → Checklist → Thực thi → Nhật ký → Phân tích → Cải thiện
```

---

## 2. Domain Entities & Quan hệ

```
User (1)
 ├── Portfolio (N)          ← Danh mục đầu tư
 │    ├── Trade (N)         ← Giao dịch mua/bán
 │    ├── CapitalFlow (N)   ← Nạp/rút/cổ tức (có Symbol khi sinh từ CorporateAction)
 │    ├── CorporateAction (N) ← Sự kiện quyền: cổ tức tiền/cổ phiếu, chia tách
 │    ├── RiskProfile (1)   ← Cấu hình rủi ro
 │    └── Snapshot (N)      ← Ảnh chụp trạng thái theo ngày
 │
 ├── TradePlan (N)          ← Kế hoạch giao dịch
 │    ├── PlanLot (N)       ← Các lô mua (ScalingIn/DCA)
 │    ├── ExitTarget (N)    ← Mục tiêu thoát (TP/CL/Trailing)
 │    ├── ScenarioNode (N)  ← Cây kịch bản (Advanced mode)
 │    ├── Checklist (N)     ← Danh sách kiểm tra
 │    ├── CampaignReviewData (0..1) ← Kết quả review chiến dịch (P0.7, embedded)
 │    └── TimeHorizon       ← Tầm nhìn đầu tư: ShortTerm / MediumTerm / LongTerm
 │
 ├── Strategy (N)           ← Chiến lược giao dịch
 ├── TradeJournal (N)       ← Nhật ký giao dịch
 ├── AlertRule (N)          ← Cảnh báo giá/rủi ro
 ├── Backtest (N)           ← Kiểm thử chiến lược
 │
 ├── DailyRoutine (N)       ← Nhiệm vụ hàng ngày (1 per user per day)
 │    └── RoutineItem (N)   ← Các bước trong routine (embedded)
 │
 ├── RoutineTemplate (N)    ← Mẫu routine (5 built-in + custom)
 │    └── RoutineItemTemplate (N)
 │
 ├── Watchlist (N)           ← Danh sách theo dõi cổ phiếu
 │    └── WatchlistItem (N)  ← Mã CP + ghi chú + giá mục tiêu (embedded)
 │
 ├── JournalEntry (N)        ← Nhật ký symbol (standalone, không cần Trade)
 │                              6 loại: Observation/PreTrade/DuringTrade/PostTrade/Review/Decision (PR-3)
 │
 ├── MarketEvent (N)         ← Sự kiện thị trường (KQKD, cổ tức, tin tức...)
 │                              7 loại: Earnings/Dividend/RightsIssue/ShareholderMtg/InsiderTrade/News/Macro
 │
 ├── AiSettings (1)          ← Cấu hình AI đa nhà cung cấp (Claude + Gemini)
 │
 ├── FinancialProfile (1)    ← Tổng quan tài chính cá nhân (Tier 3)
 │    ├── Accounts (N)       ← 5 loại: Securities/Savings/Emergency/IdleCash/Gold (embedded)
 │    │    └── Gold fields   ← Brand (SJC/DOJI/PNJ/Other) + Type (Miếng/Nhẫn) + Quantity (lượng)
 │    └── Rules              ← EmergencyFundMonths (6) + MaxInvestmentPercent (50%) + MinSavingsPercent (30%)
 │
 ├── Role                     ← UserRole enum: User (default) / Admin (debug tooling)
 ├── LastLoginAt (nullable)   ← Timestamp của lần login Google OAuth gần nhất (không cập nhật khi refresh/impersonate)
 ├── ApiKey (N)               ← Personal access token (non-interactive API access, xem ADR-0003)
 └── CompanyDossier (N)       ← Hồ sơ hiểu doanh nghiệp, khóa (UserId, Symbol) — gate chặn tạo TradePlan (ADR-0011)
      ├── MoatItem (N)         ← Value object, embedded
      └── RiskFactor (N)       ← Value object, embedded, rank dense 1..N, tối đa 1 IsDealBreaker

ImpersonationAudit (independent, append-only)
 ├── AdminUserId, TargetUserId, Reason, IpAddress, UserAgent
 ├── StartedAt, EndedAt?, IsRevoked
 └── 1 bản ghi = 1 phiên impersonate; revoke bằng set IsRevoked=true
```

### Liên kết giữa entities

| Từ | Đến | Quan hệ | Ghi chú |
|----|-----|---------|---------|
| Trade | Portfolio | N:1 | Bắt buộc |
| Trade | TradePlan | N:1 | Tùy chọn, link qua `tradePlanId` |
| Trade | Strategy | N:1 | Tùy chọn, link qua `strategyId` |
| TradeJournal | Trade | 1:1 | Link qua `tradeId` |
| TradePlan | Portfolio | N:1 | Tùy chọn |
| TradePlan | Strategy | N:1 | Tùy chọn |
| RiskProfile | Portfolio | 1:1 | Mỗi danh mục 1 profile |
| CapitalFlow | Portfolio | N:1 | Bắt buộc |
| Snapshot | Portfolio | N:1 | Ảnh chụp hàng ngày |
| DailyRoutine | User | N:1 | 1 routine/user/ngày |
| DailyRoutine | RoutineTemplate | N:1 | Tạo từ template |
| RoutineTemplate | User | N:1 | null = built-in, non-null = custom |
| Watchlist | User | N:1 | Nhiều watchlist per user |
| WatchlistItem | Watchlist | N:1 | Embedded, symbol + note + target prices |
| AiSettings | User | 1:1 | 1 cấu hình AI per user (multi-provider: Claude + Gemini) |
| FinancialProfile | User | 1:1 | Tổng quan tài chính cá nhân, unique index UserId, soft-delete |
| FinancialAccount | FinancialProfile | N:1 | Embedded, 5 loại. Securities cuối cùng không cho xóa (guard domain) |
| Debt | FinancialProfile | N:1 | Embedded, 6 loại (CreditCard/PersonalLoan/Mortgage/Auto/Installment/Other). Xóa chỉ được khi Principal=0 |
| JournalEntry | User+Symbol | N:1 | Standalone, optional link Trade/TradePlan/Portfolio |
| MarketEvent | Symbol | N:1 | Sự kiện thị trường (manual + auto) |
| ApiKey | User | N:1 | Nhiều named key per user; collection `api_keys`, unique index `KeyHash`, index `UserId` |
| CompanyDossier | TradePlan | N:0 (gián tiếp qua Symbol) | Không FK trực tiếp — gate đọc `CompanyDossier(UserId, Symbol)` tại thời điểm tạo/sửa `TradePlan`, không lưu tham chiếu ngược. Một hồ sơ áp cho mọi plan cùng mã của cùng user |

> **`Trade.Fee` vs `Trade.Tax` (bất biến quan trọng):** hai khoản **tách biệt, không chồng nhau**. `Fee` = phí giao dịch + VAT (chi phí môi giới); `Tax` = thuế TNCN (0.1%, chỉ lệnh BÁN). Mọi phép tính net dùng `Quantity*Price + Fee + Tax` (mua) / `- Fee - Tax` (bán) — KHÔNG được gộp thuế vào `Fee` (sẽ trừ 2 lần). Xem [ADR-0006](adr/0006-trade-fee-excludes-tax.md).

---

## 3. Các nghiệp vụ chính

### 3.1. Quản lý Danh mục (Portfolio)
- Tạo danh mục với **vốn ban đầu** (`InitialCapital`) — snapshot lúc tạo, không đổi theo thời gian
- Nạp/rút tiền qua `CapitalFlow` (Deposit, Withdraw, Dividend, Interest, Fee)
- **Vốn hiện tại** (`CurrentCapital`) = `InitialCapital + Σ SignedAmount` — đây là giá trị "vốn ròng" hiện tại của danh mục, phản ánh mọi nạp/rút/cổ tức/phí đã xảy ra
- **Cash còn lại** = `CurrentCapital − TotalInvested + TotalSold` — tiền mặt khả dụng để vào lệnh mới
- **Quy tắc:** Mọi chỗ tính position sizing, account balance, allocation % phải dùng `CurrentCapital` (không dùng `InitialCapital`) để phản ánh đúng vốn user đang có.

### 3.1b. Sự kiện quyền (CorporateAction)

Bản ghi **bất biến** — sửa = xoá và tạo lại. `Trade` không bao giờ bị sửa theo sự kiện quyền.

| Quy tắc | Chi tiết |
|---|---|
| **Đơn vị cổ tức tiền mặt** | "5%" = 5% × **mệnh giá 10.000đ** = 500đ/CP. **Không** phải 5% giá thị trường. Entity lưu `AmountPerShare` đã quy đổi ra đồng. |
| **Thuế** | TNCN 5% khấu trừ tại nguồn → `NetPerShare = AmountPerShare × 0,95`. Giá tham chiếu trừ theo số **trước** thuế. |
| **Hệ số cổ phiếu** | `Multiplier = RatioNew / RatioOld`, trong đó `RatioNew` là **tổng** sau sự kiện (30% → `100:130` → 1,3). Giá điều chỉnh `P / Multiplier` → giảm 23,08%, không phải 30%. |
| **Giá vốn** | Cổ tức cổ phiếu và chia tách **giảm** giá vốn (`TotalCost` không đổi, số lượng tăng). Cổ tức tiền mặt **không** đổi giá vốn — là thu nhập. |
| **Cùng ngày GDKHQ** | Áp tiền mặt trước (tính trên số lượng cũ), rồi mới nhân hệ số: `P_adj = (P − CashPerShare) / Multiplier`. |
| **Thứ tự trong ngày GDKHQ** | Sự kiện quyền chạy **trước** lệnh khớp cùng ngày. Quyền chốt theo danh sách cổ đông cuối ngày liền trước, nên người **bán** trong ngày GDKHQ vẫn hưởng, người **mua** hôm đó thì không. |
| **Chưa tới ngày GDKHQ thì chưa điều chỉnh** | Sự kiện thường được công bố trước vài tuần. Mọi phép điều chỉnh giá đều chặn trên bằng `ExDate ≤ hôm nay` — áp sớm là so giá kế hoạch đã hạ với giá thị trường chưa hạ. |
| **`SettlementDate` ≠ `SettledAt`** | `SettlementDate` là ngày dự kiến khi nhập; `SettledAt` chỉ có khi người dùng bấm xác nhận. `IsSettled` xét `SettledAt`. |
| **Cổ phiếu lẻ** | `floor()` — phần lẻ bị huỷ (137 × 1,3 = 178). |
| **Chờ về** | Tại `ExDate` áp ngay vào giá vốn và tổng số lượng; phần tăng thêm nằm ở `PendingQuantity` cho tới khi người dùng bấm xác nhận (`SettledAt`). `TotalQuantity = Settled + Pending` dùng cho **mọi** phép tính. |
| **Ngưỡng giá** | Giá tuyệt đối do người dùng đặt (`StopLossTarget`, `TradePlan.EntryPrice`, ngưỡng node kịch bản, giá kích hoạt trượt) → điều chỉnh **tại thời điểm đọc**, không sửa dữ liệu, nên xoá sự kiện thì ngưỡng tự quay về cũ. Mốc so sánh: `StopLossTarget.UpdatedAt`, `TradePlan.PricesSetAt`. |
| **Giá vs. khoảng cách giá** | Mức giá điều chỉnh bằng `AdjustPrice` (trừ cổ tức tiền mặt, rồi chia hệ số). Khoảng cách giá — biên trượt `FixedAmount`, `StepSize` — dùng `AdjustDelta`: **chỉ chia hệ số**, vì cổ tức tiền mặt dịch cả mặt bằng chứ không làm khoảng cách hẹp lại. |
| **Không phải số nào cũng là giá** | `ScenarioNode.ConditionValue` là giá với `PriceAbove`/`PriceBelow`, là **phần trăm** với `PricePercentChange`, là **số ngày** với `TimeElapsed`. `TrailValue` chỉ là tiền khi `Method = FixedAmount`. `ActionValue` chỉ là giá với `MoveStopLoss`. |
| **Quan sát thị trường thì rebase, không điều chỉnh khi đọc** | `TrailingStopConfig.HighestPrice` / `CurrentTrailingStop` là giá ghi nhận từ thị trường rồi **ghi đè trở lại** entity. Điều chỉnh khi đọc sẽ hạ chồng lần: lần ghi kế tiếp lưu giá ở mặt bằng mới, lần đọc sau lại chia tiếp. Vì vậy quy đổi **đúng một lần** rồi đánh dấu bằng `PriceBasisAt`. |
| **Đóng mốc ở MỌI lần ghi, không chỉ khi rebase** | Giá ghi trực tiếp từ thị trường cũng đã ở mặt bằng sau mọi sự kiện đã qua ngày GDKHQ. Không đóng `PriceBasisAt` ngay lúc đó thì lần rebase sau lùi về mốc kế hoạch và chia lại các sự kiện đã phản ánh. Bẫy nằm ở nhánh **ghi lần đầu**, khi hàm rebase thoát sớm vì chưa có giá trị nào để quy đổi. |

**Nguồn duy nhất dựng vị thế:** `PositionBuilder.Build(trades, actions, asOf)` (`Application/Common`). Mọi service cần giá vốn / số lượng phải gọi vào đây, không tự `GroupBy` trên `Trade` thô. Xem [ADR-0010](adr/0010-corporate-actions-position-projection.md).

**Lãi/lỗ trong ngày** (hạn mức rủi ro, có thể khoá giao dịch): `RealizedPnL` tính đến hôm nay trừ đi `RealizedPnL` tính đến hết hôm qua — cả hai từ `PositionBuilder`. Không tự tính từ trade thô: trung bình không trọng số của các lệnh mua lệch tới mức **đổi dấu** lãi/lỗ.

### 3.2. Kế hoạch Giao dịch (TradePlan)
Trạng thái: `Draft → Ready → InProgress → Executed → Reviewed | Cancelled → Restore → Draft`
Chuyển tuần tự, không nhảy cóc. Backend auto-chain khi cần (VD: client gọi `executed` từ Ready → tự chain qua InProgress). Chi tiết: [`docs/trade-plans.md` §2.2](trade-plans.md)

**TimeHorizon (P0.7):** `ShortTerm` (< 3 tháng) / `MediumTerm` (3-12 tháng) / `LongTerm` (> 1 năm)

**Hai mốc giá (2026-08-09):** mốc để điều chỉnh giá theo sự kiện quyền. Cố ý **không** dùng `UpdatedAt` — nó nhảy mỗi lần một nhánh kịch bản kích hoạt, làm các nhánh còn lại thôi được điều chỉnh.

| Field | Bao trùm | Dời khi |
|---|---|---|
| `PricesSetAt` | `EntryPrice`, `StopLoss`, `Target` | ctor; `Update()` khi giá **thực sự đổi**; `UpdateStopLossWithHistory()` |
| `ScenarioPricesSetAt` | ngưỡng node, `ActivationPrice`, `TrailValue`, `StepSize`, `ActionValue` của `MoveStopLoss` | `SetScenarioNodes()` khi chữ ký giá của cây kịch bản **thực sự đổi** |

Tách hai mốc vì sửa nhánh kịch bản không đặt lại giá nhập — dùng chung một mốc thì thao tác đó sẽ vô hiệu hoá việc điều chỉnh giá nhập. Null → `ScenarioPricesSetAt` lùi về `PricesSetAt`, `PricesSetAt` lùi về `CreatedAt`.

**Bắt buộc so sánh giá trị, không chỉ kiểm tra "có gửi lên hay không".** Form sửa kế hoạch gửi lại **toàn bộ** trường mỗi lần lưu — cả `entryPrice`/`stopLoss`/`target` lẫn `scenarioNodes` — nên `entryPrice.HasValue` luôn đúng. Dời mốc theo đó thì sửa mỗi ghi chú cũng đặt lại mặt bằng giá và huỷ việc điều chỉnh theo sự kiện quyền đã xảy ra.

**CampaignReviewData (P0.7):** Value object embedded trong TradePlan khi chuyển sang Reviewed — chứa auto-calculated metrics: P&L amount, P&L %, VND/ngày, annualized return, target achievement %, lessons learned

**Thesis-driven discipline (Vin-discipline, 2026-04-23):** Plan-level fields ép kỷ luật "tại sao mua / sai ở đâu thì bán / giữ bao lâu" theo triết lý Vinpearl Air 2020 (dám dừng khi thesis bị phá vỡ).

| Field | Kiểu | Ràng buộc |
|-------|------|-----------|
| `Thesis` | `string?` | (Rename từ `Reason`). Required khi rời Draft. Size-based gate: ≥ 30 ký tự nếu `Quantity × EntryPrice ≥ 5% AccountBalance`; ≥ 15 ký tự nếu plan size nhỏ hơn. |
| `InvalidationCriteria` | `List<InvalidationRule>?` | Required ≥ 1 rule (mỗi rule `Detail` ≥ 20 ký tự) khi plan size ≥ 5% account; optional khi nhỏ hơn. |
| `ExpectedReviewDate` | `DateTime?` | Ngày dự kiến review lại thesis (V2 dùng cho nudge). |
| `LegacyExempt` | `bool` | `true` cho plan tạo trước migration 2026-04-23. Graduated deprecation T+0 → T+6 tháng. |

**InvalidationRule (value object):**
- `Trigger`: enum `InvalidationTrigger` (5 loại cố định).
- `Detail`: string falsifiable, min 20 ký tự khi size ≥ 5% account.
- `CheckDate`: ngày dự kiến verify (vd: ngày công bố BCTC).
- `IsTriggered` + `TriggeredAt`: set true khi user mark thesis invalidated.

**InvalidationTrigger enum (5 loại):**

| Trigger | Ý nghĩa |
|---------|---------|
| `EarningsMiss` | KQKD không đạt kỳ vọng |
| `TrendBreak` | Gãy trend kỹ thuật (mất MA200, volume cao đỏ, khối ngoại xả ròng) |
| `NewsShock` | Tin tức thay đổi bản chất (CEO resign, scandal, regulation, UBCKNN xử phạt) |
| `ThesisTimeout` | Quá hạn mà thesis chưa thể hiện (giữ lâu vẫn sideways) |
| `Manual` | User tự nhận xét thesis sai (escape hatch) |

**Mid-flight abort (`AbortWithThesisInvalidation`):** method mới trên aggregate, áp cho state `Ready | InProgress | Executed` (multi-lot partial-executed vẫn abort được). Khác với `Cancel()` — `Abort` ép ghi trigger + detail để tạo learning loop. Raise domain event `TradePlanThesisInvalidatedEvent` + `Restore()` sau abort sẽ clear `IsTriggered` flags.

**3 chế độ vào lệnh (EntryMode):**

| Mode | Mô tả |
|------|--------|
| Single | Mua 1 lần duy nhất |
| ScalingIn | Chia nhiều lô, mỗi lô có giá/số lượng/% phân bổ |
| DCA | Mua định kỳ (weekly/biweekly/monthly) với số tiền cố định |

**Mục tiêu thoát (ExitTarget):**
- TakeProfit, CutLoss, TrailingStop, PartialExit
- Mỗi target có `price`, `percentOfPosition`, `isTriggered`

**Chế độ thoát lệnh (ExitStrategyMode):**

| Mode | Mô tả |
|------|--------|
| Simple | Dùng ExitTarget truyền thống (TP/CL/Trailing) |
| Advanced | Dùng cây kịch bản ScenarioNodes |

**Kịch bản nâng cao (ScenarioNode):**
- Cây quyết định đệ quy: mỗi node có Condition, Action, Children
- **Condition (enum):** `PriceAbove`, `PriceBelow`, `PricePercentChange`, `TrailingStopHit`, `TimeElapsed`
- **Action (enum):** `SellPercent`, `SellAll`, `MoveStopLoss`, `MoveStopToBreakeven`, `ActivateTrailingStop`, `AddPosition`, `SendNotification`
- Worker tự động đánh giá mỗi 15 phút → kích hoạt hành động + tạo AlertHistory
- Domain event: `ScenarioNodeTriggeredEvent`

**TrailingStopConfig (Value Object):**
- `Method`: `Percentage` | `ATR` | `FixedAmount`
- `TrailValue`: giá trị trail tương ứng method
- `ActivationPrice`: giá kích hoạt trailing stop
- `StepSize`: bước nhảy tối thiểu khi nâng stop

**3 preset templates:** An toàn (bảo toàn vốn), Cân bằng (cân đối rủi ro/lợi nhuận), Tích cực (chấp nhận rủi ro cao)

### 3.3. Giao dịch (Trade)
- Loại: BUY / SELL (sử dụng shared `TradeType` enum)
- Khi BUY: số lượng phải là **bội của 100** (lô chẵn HOSE)
- Giá trị mua **không vượt quá cash còn lại** của danh mục
- Symbol luôn **UPPERCASE** (normalize qua `UppercaseDirective` + backend `ToUpper()`)

### 3.4. Tính toán Lãi/Lỗ (P&L)
- **Average Cost Method**: giá vốn bình quân gia quyền
- **Unrealized P&L** = (giá hiện tại - giá vốn) × số lượng — giá hiện tại lấy từ 24hmoney API (real-time)
- **Realized P&L** = tổng (giá bán - giá vốn) × số lượng bán
- **TWR** (Time-Weighted Return): loại bỏ ảnh hưởng nạp/rút tiền
- **MWR** (Money-Weighted Return / IRR): tính cả dòng tiền

### 3.5. Quản lý Rủi ro (Risk)

- **RiskProfile**: maxPositionSize%, maxDrawdownAlert%, defaultRR, maxDailyTrades, dailyLossLimitPercent (P4), **maxSectorExposurePercent** (mặc định 40% — có hiệu lực từ 2026-08-10, trước đó là luật chết vì provider tra ngành được đăng ký là no-op; xem ADR-0012). Tập trung ngành **chỉ cảnh báo, không chặn** lập kế hoạch: nhãn ngành đến từ provider ngoài nên không được cầm quyền phủ quyết, khác cổng hồ sơ công ty vốn đọc dữ liệu do người dùng tự viết. Rổ "Không xác định" cũng bị so hạn mức. Tỷ trọng sau một lệnh dự kiến tính theo `(giá trị ngành + quy mô lệnh) / tổng giá trị danh mục` — mẫu số **không** cộng quy mô lệnh vì tổng đã gồm tiền mặt; không tính được thì trả `null`, không trả `0` (không tra được ngành → UI ẩn hẳn khối; biết ngành mà tổng ≤ 0 → hiện "n/a"). **Chỉ áp cho lệnh MUA** — phép chiếu cộng quy mô lệnh nên với lệnh bán nó báo tăng đúng lúc tỷ trọng giảm. Nhãn ngành cache 6 giờ; ca tra lỗi/rỗng **không** cache để một lỗi mạng không đóng băng mã đó thành "không rõ ngành".
- **Position Sizing**: `positionSize = accountBalance × riskPercent / (entry - stopLoss)`
- **Stop-loss tracking**: lịch sử thay đổi SL, cảnh báo khi giá gần SL
- **Correlation matrix**: tương quan giữa các cổ phiếu trong danh mục
- **Stress Test (P2)**: dynamic beta từ API, fallback correlation VN-INDEX, fallback 1.0
- **Risk Budget (P4)**: giới hạn số lệnh/ngày, giới hạn lỗ/ngày (%)

### 3.6. Phân tích Hiệu suất (Analytics)
- **CAGR (household-level, 2026-05-03)**: headline trên Cockpit lấy từ `GET /api/v1/analytics/household/performance` — backend gộp snapshot tất cả portfolio của user vào 1 series tổng (sum `TotalValue` mỗi ngày, carry-forward; portfolio gia nhập muộn → first-snapshot value attribute như cash flow), apply công thức TWR rồi annualize. Có flag `isStable` (true ⇔ window ≥ 365 ngày) để FE render badge "chưa đủ 1 năm" nếu cửa sổ ngắn. CAGR per-portfolio vẫn còn ở `GET /portfolio/{id}/performance` cho các view chi tiết.
- **Sharpe Ratio, Sortino Ratio**: cần có closed trades
- **Max Drawdown**: mức sụt giảm lớn nhất từ đỉnh
- **Win Rate, Profit Factor**: tỷ lệ thắng, hệ số lợi nhuận
- **Monthly Returns Heatmap**: lãi/lỗ theo tháng

### 3.7. Wizard Giao dịch (5 bước)
```
Bước 1: Chọn chiến lược (tùy chọn)
Bước 2: Lập kế hoạch (entry/SL/TP + position sizing)
Bước 3: Checklist (GO/NO-GO)
Bước 4: Xác nhận & tạo giao dịch + tự động tạo journal
Bước 5: Nhật ký (update journal đã tạo)
```

### 3.8. Cảnh báo (Alert)
- **PriceAlert**: giá cổ phiếu vượt/dưới ngưỡng
- **DrawdownAlert**: drawdown vượt ngưỡng
- **StopLossAlert**: giá gần SL
- Kênh: InApp / Email

### 3.9. Kiểm thử Chiến lược (Backtest)
- Chạy mô phỏng chiến lược trên dữ liệu lịch sử
- Trả về equity curve, simulated trades, metrics

### 3.10. Đánh giá Nhanh Mã Cổ phiếu (Stock Evaluation)
- Kết hợp **phân tích cơ bản** (P/E, P/B, EPS, ROE, ROA, D/E, tăng trưởng DT/LN) + **phân tích kỹ thuật** (EMA/RSI/MACD/Volume/S&R)
- Dữ liệu cơ bản từ **TCBS API** (`apipubaws.tcbs.com.vn`), cache 5 phút
- Dữ liệu kỹ thuật từ **24hmoney API** (đã có sẵn)
- Prompt AI dùng **XML tagging** + **markdown tables** cho dữ liệu có cấu trúc → AI parse chính xác hơn
- Hỗ trợ 2 mode: **Gửi AI** (streaming SSE, cần API key) hoặc **Copy Prompt** (clipboard, dùng client app)

### 3.11. Copy AI Prompt (Clipboard)
- Tạo prompt hoàn chỉnh (system prompt + user message + context data) cho bất kỳ use case nào
- **Không cần API key** — chỉ đọc data từ app, format thành prompt
- User paste vào Claude Max / Gemini client app bên ngoài
- Endpoint: `POST /api/v1/ai/build-context` → trả JSON `{ systemPrompt, userMessage }`

### 3.11b. Daily digest cho NPU (ApiKey-authed, ADR-0003)
- Endpoint: `POST /api/v1/ai/daily-digest` — **xác thực bằng ApiKey scheme** (header `X-Api-Key`), KHÔNG dùng JWT. Endpoint opt-in đầu tiên dùng scheme này.
- Trả JSON `{ systemPrompt, userMessage }` giống build-context, nhưng context là bản tin hằng ngày đã bổ sung **cash/net-worth** + **position-sizing** cho các kế hoạch chờ (pending plans) + **bối cảnh thị trường** `<market_context>` (VN-Index, độ rộng tăng/giảm/trần/sàn, khối ngoại mua-bán ròng tỷ VND — để quyết định tái cơ cấu) + **watchlist** `<watchlist>` đầy đủ (giá + %ngày + khoảng cách mục tiêu mua + tín hiệu 📉/📈 — để săn cơ hội).
- Trợ lý NPU kéo digest theo cron rồi đẩy thẳng vào Claude phân tích timing (server tính sẵn số, Claude nhận định định tính).
- Scope theo owner của khóa (`sub` claim = UserId của khóa) — mỗi khóa chỉ đọc được dữ liệu của chủ khóa.

**Hai loại tiền mặt — không được lẫn (2026-07-26, ADR-0007):**

| Khái niệm | Nghĩa | Nguồn |
|---|---|---|
| `portfolio_cash` | Tiền **chưa giải ngân trong tài khoản chứng khoán** — gồm cả tiền vừa thu về từ lệnh bán | Suy ra: `InitialCapital + nạp/rút (đã loại seed) − tổng tiền mua (gồm phí+thuế) + tổng tiền bán (trừ phí+thuế)`, qua `PortfolioCashCalculator` |
| `idle_cash` | Tiền nhàn rỗi **ngoài** tài khoản chứng khoán — user tự khai | `FinancialProfile.Accounts` loại `IdleCash` (§3.12) |
| `investable_capital` | Nền vốn cho position sizing | `giá trị thị trường + portfolio_cash + idle_cash` |

Trước 2026-07-26 bản tin chỉ đọc `idle_cash`, nên tiền thu từ lệnh bán vô hình: bản tin báo "không có tiền mặt" và khối lượng gợi ý thấp hơn thực tế. `portfolio_cash` nay **luôn được in**, kể cả khi user chưa lập hồ sơ tài chính.

**Các section khác đã bổ sung cùng dịp:** `<portfolio_overview>` bóc theo từng danh mục + `<realized_pnl>`; `<positions>` (tên danh mục, KL, giá vốn, %DM, khoảng cách tới SL — ô SL ghi "chưa đặt" nghĩa là user chưa có stop-loss, khác với `n/a` là chưa lấy được dữ liệu); `<recent_trades>` 14 ngày; `<decision_queue>`; `<risk_alerts>` theo luật rủi ro thay cho ngưỡng lỗ −5% cũ; `<drill_down>` liệt kê tool tra sâu. Hai chỉ số lợi nhuận có mẫu số riêng: `<unrealized_return>` (trên giá vốn phần đang nắm) và `<total_return>` (trên tổng tiền đã mua) — không trộn vào một con số.

### 3.12. Tài chính cá nhân (Tier 3, 2026-04-22)

Tổng quan tài sản + nguyên tắc tài chính + tracking vàng tích trữ. Scope solo user không quản lý chi tiêu.

**5 loại tài khoản** (`FinancialAccountType`):

| Type | Label | Ghi chú |
|------|-------|---------|
| Securities | Chứng khoán | Balance auto-sync từ `IPnLService.CalculatePortfolioPnLAsync(...).TotalMarketValue`, không nhập tay. Không được xóa tài khoản cuối cùng |
| Savings | Tiết kiệm | Balance + InterestRate (%/năm, optional) + DepositDate + MaturityDate (optional, cả 2 hoặc từng cái — cho sổ có kỳ hạn) |
| Emergency | Quỹ dự phòng | Balance thuần |
| IdleCash | Tiền nhàn rỗi | Balance thuần |
| Gold | Vàng | Brand (SJC/DOJI/PNJ/Other) + Type (Mieng/Nhan) + Quantity (lượng) → auto-calc Balance. Fallback manual Balance nếu không set 3 Gold fields |

**Gold brand bucket `Other`**: gom BTMC/BTMH/Ngọc Hải/Mi Hồng... Các vendor khác ngoài SJC/DOJI/PNJ. `GetPriceAsync(Other, type)` trả entry đầu tiên trong HTML 24hmoney (documented).

**3 nguyên tắc tài chính** (`FinancialRules`, điểm health score 0-100):

| Rule | Default | Điểm trừ tối đa | Công thức |
|------|---------|-----------------|-----------|
| Quỹ dự phòng ≥ N tháng chi tiêu | 6 tháng | -40 | `deficit/requiredEmergency × 40` |
| Đầu tư (CK + Vàng) ≤ N% tổng tài sản | 50% | -30 | `excess/maxInvestment × 30` |
| Tiết kiệm ≥ N% tổng tài sản | 30% | -30 | `deficit/requiredSavings × 30` |

Vàng cộng dồn vào investment total (cùng Securities) cho rule MaxInvestment. Không cộng vào savings — chỉ bank savings tính tiết kiệm.

`totalAssets = Σ balance (non-Securities accounts) + securitiesValue (live từ PnLService)`.

**Quy tắc nghiệp vụ:**
- Profile per-user 1:1 (unique index UserId).
- `MonthlyExpense` bắt buộc khi tạo profile lần đầu; optional khi update.
- Upsert profile flow: get active → get soft-deleted → create new (pattern giống AiSettings để tránh unique index violation).
- UpsertAccount enforces: non-Savings không có InterestRate, **non-Savings không có DepositDate/MaturityDate**, non-Gold không có Gold fields, Gold fields all-or-nothing, GoldQuantity > 0, Balance ≥ 0. Khi cả DepositDate + MaturityDate set, MaturityDate phải ≥ DepositDate (fat-finger guard).
- FinancialAccount có `CreatedAt` (immutable sau Create) + `UpdatedAt`. Docs cũ trong Mongo không có field này sẽ default `DateTime.MinValue` — chấp nhận, không migrate.

**Analytics — So sánh với tiết kiệm (2026-04-24, V1.2):**

- `GET /api/v1/analytics/portfolio/{id}/vs-savings` trả `SavingsComparisonDto` với `ActualValue`, `HypotheticalValue`, `OpportunityCost`, `OpportunityCostPercent`, `AlphaAnnualized`/`PeriodReturnDiff`, `Flows[]` (cho FE recompute), `ActualCurve[]`, `UsedRate`, `RateSource` enum ("user-savings-avg" / "fallback-5" / "manual").
- Math: **running-balance iterative** + **monthly compound** `(1 + r/12)^months`. Filter `Deposit`/`Withdraw` flows only (loại Dividend/Interest/Fee).
- Rate resolution: query param `savingsRate` > weighted avg của `Savings` accounts có `InterestRate` (theo balance) > fallback 5%/năm.
- Sanity cap: rate ∈ [-10%, +50%]/năm → else `InvalidOperationException`.
- `asOf` normalize `.Date` — tránh drift partial-day compound.
- `OpportunityCostPercent = null` khi `HypotheticalValue ≤ 0` (denominator undefined).
- `AlphaAnnualized` chỉ non-null khi `days ≥ 365` (CAGR dưới 1 năm bị variance).
- `GET /api/v1/analytics/bank-rates` trả `BankRateSnapshot` (scrape 24hmoney online table, top rate per term 1/3/6/9/12 tháng).
- UpsertFinancialAccountCommand handler tự fetch price qua `IGoldPriceProvider.GetPriceAsync(brand, type)` khi 3 Gold fields đủ; provider null → throw 400 (không silent fallback).
- RemoveAccount bảo vệ Securities cuối cùng (throw `InvalidOperationException`).

**Gold price source** (`HmoneyGoldPriceProvider`):
- Scrape `24hmoney.vn/gia-vang` (không có JSON API, SSR HTML).
- Parse với AngleSharp: `<table class="gold-table">` → filter `div.brand-region` keep "vàng miếng"/"vàng nhẫn" only.
- Giá là **full VND** (167,200,000) mặc dù UI label nói "triệu VNĐ/lượng".
- Two-tier cache: fresh 5 phút + stale 6h fallback khi 24hmoney down.

**Deploy config:** env var `GoldPriceProvider__PageUrl=https://24hmoney.vn/gia-vang` bắt buộc set trước deploy prod/staging (placeholder `{GoldPriceProvider__PageUrl}` trong `appsettings.json` sẽ DNS-fail nếu env thiếu).

### 3.13. External Data Providers

| Provider | URL | Dữ liệu | Interface | Cache |
|----------|-----|----------|-----------|-------|
| **24hmoney** | `api-finance-t19.24hmoney.vn` | Giá real-time, lịch sử giá, chỉ số thị trường, order book, NN, top biến động | `IMarketDataProvider` + `IStockInfoProvider` | 15-30s |
| **24hmoney (comprehensive)** | `api-finance-t19.24hmoney.vn` | Chỉ số tài chính (P/E, P/B, ROE, ROA, EPS, Beta, MarketCap), BCTC, kế hoạch kinh doanh, cổ tức, cổ phiếu cùng ngành, giao dịch NN, báo cáo phân tích | `IComprehensiveStockDataProvider` | 5 phút |
| **TCBS** | `apipubaws.tcbs.com.vn` | Fundamental: P/E, P/B, EPS, ROE, ROA, D/E, doanh thu, lợi nhuận, vốn hóa | `IFundamentalDataProvider` | 5 phút |

**24hmoney Comprehensive Endpoints:**

| Endpoint | Mô tả |
|----------|--------|
| `/v2/ios/companies/index` | Chỉ số tài chính: P/E, P/B, ROE, ROA, EPS, Beta, MarketCap |
| `/api/v2/web/company/detail` | Thông tin chi tiết công ty |
| `/api/v2/web/company/financial-report` | Báo cáo tài chính (BCTC) |
| `/api/v2/web/company/plan` | Kế hoạch kinh doanh |
| `/api/v2/web/announcement/dividend-events` | Sự kiện cổ tức |
| `/api/v2/web/stock-recommend/get_stock_related_bussiness` | Cổ phiếu cùng ngành |
| `/api/v2/web/stock/foreign-trading-series` | Chuỗi giao dịch nước ngoài |
| `/api/v2/web/announcement/report-analytics` | Báo cáo phân tích từ CTCK |

### 3.14. API Key (Personal Access Token, ADR-0003)

Token dạng `imk_` + base64url(32 random bytes) — cho phép truy cập API theo cơ chế non-interactive (CI/CD, script cá nhân). Plaintext token **chỉ trả về một lần duy nhất** khi tạo; hệ thống chỉ lưu `KeyHash` (SHA-256 hex). `Prefix` (`imk_` + vài ký tự đầu) dùng để hiển thị trong danh sách mà không lộ token.

**Fields:**

| Field | Kiểu | Ghi chú |
|-------|------|---------|
| `Id` | ObjectId | PK |
| `UserId` | ObjectId | FK → User |
| `Name` | string | Tên gợi nhớ do user đặt |
| `KeyHash` | string | SHA-256 hex của plaintext token; unique index |
| `Prefix` | string | `imk_` + vài ký tự đầu token (hiển thị only) |
| `CreatedAt` | DateTime | Immutable |
| `ExpiresAt` | DateTime | Bắt buộc; 1–365 ngày từ ngày tạo, default 90 ngày |
| `LastUsedAt` | DateTime? | Cập nhật mỗi request xác thực thành công |
| `RevokedAt` | DateTime? | Set khi revoke (soft-delete) |

**Quy tắc nghiệp vụ:**
- Token = `imk_` + base64url(32 random bytes). Plaintext hiển thị **một lần duy nhất** khi tạo (response 201); các lần sau chỉ thấy `Prefix`.
- Expiry bắt buộc — không hỗ trợ "không bao giờ hết hạn". Khoảng [1, 365] ngày, default 90.
- Một user có thể có nhiều named key cùng lúc.
- Revoke là soft state: set `RevokedAt`, key vẫn xuất hiện trong danh sách nhưng không dùng được.
- `IsActive` = `RevokedAt == null && DateTime.UtcNow < ExpiresAt`.
- ApiKey auth scheme **chỉ được chấp nhận trên các endpoint opt-in** — không tự động áp dụng toàn API.

### 3.15. Hồ sơ công ty & điều kiện chặn lập kế hoạch (2026-08-10, ADR-0011)

Không cho tạo `TradePlan` mới cho một mã khi chưa có `CompanyDossier` cho mã đó đã được người dùng **ký** và còn hiệu lực. Mục đích: gate `Thesis` hiện có chỉ đếm được độ dài câu chữ, không đếm được hiểu doanh nghiệp — hồ sơ ép trả lời "kiếm tiền bằng gì / moat ở đâu / rủi ro nào và biết nó đang xảy ra bằng dấu hiệu gì" **trước khi** xuống tiền, sống theo mã (viết một lần, dùng cho mọi lần mua mã đó) chứ không theo từng lệnh như `Thesis`.

**Nội dung hồ sơ — 4 khối:**

| Khối | Gate? | Ghi chú |
|---|:---:|---|
| `BusinessModel` (doanh nghiệp kiếm tiền bằng gì) | ✅ | ≥ 30 ký tự ở tầng lớn, không rỗng ở tầng nhỏ |
| `Moats` (lợi thế bền) | ✅ | ≥ 1 moat, tầng lớn cần 1 cái `Description` ≥ 30 ký tự |
| `RiskFactors` (rủi ro xếp hạng 1..N, rank 1 = nguy hiểm nhất) | ✅ | Mỗi rủi ro **bắt buộc** `ObservableSignal` — "biết nó đang xảy ra bằng gì". Tối đa 1 được đánh dấu `IsDealBreaker`. Tầng lớn cần ≥ 3, mỗi `ObservableSignal` ≥ 20 ký tự |
| `Notes` (ghi chú tự do) | ❌ | Không ảnh hưởng điều kiện chặn |

**Ngưỡng đủ theo size** — cùng công thức 5% tài khoản (`TradePlan.LargeTierThreshold`) với gate kỷ luật thesis hiện có. Riêng khi request có áp lots (`EntryMode` + `Lots` đủ để `SetLots` chạy), size chấm theo **mức lớn hơn** giữa `tổng(lô × giá lô)` và `tổng lô × giá header`, vì `SetLots` ghi `Quantity` theo tổng lô nhưng không chạm `EntryPrice` — chấm theo một vế là mở đường hạ bậc bằng cách bỏ trống vế còn lại (xem ADR-0011 D9):

| | Tầng nhỏ (`Quantity × EntryPrice < 5% AccountBalance` hoặc không biết số dư) | Tầng lớn (`≥ 5%`) |
|---|---|---|
| `BusinessModel` | không rỗng | ≥ 30 ký tự |
| `Moats` | ≥ 1 | ≥ 1, có 1 cái ≥ 30 ký tự |
| `RiskFactors` | ≥ 1, có `ObservableSignal` | ≥ 3, mỗi `ObservableSignal` ≥ 20 ký tự |

**Hạn tươi (`DossierFreshness`)** — tính theo ngày lịch VN offset cố định +07:00:

| Trạng thái | Điều kiện | Chặn gate? |
|---|---|---|
| `Unconfirmed` | Chưa từng ký (`ConfirmedAt == null`) | ✅ Chặn |
| `Fresh` | Đã ký, < 90 ngày kể từ lần ký gần nhất | Đỗ |
| `NeedsReview` | Đã ký, 90-179 ngày | Đỗ — chỉ nhắc soát lại (`/pending-reviews`, chặng 3, chưa làm) |
| `Expired` | Đã ký, ≥ 180 ngày | ✅ Chặn |

Chỉ hành động **ký** (`Confirm()`, qua `POST /company-dossiers/{symbol}/confirm`, JWT) đẩy đồng hồ hạn tươi. Sửa nội dung — kể cả người dùng tự sửa qua UI — không chạm nó; nếu chạm thì một hồ sơ `Expired` chỉ cần sửa một ký tự ở ô ghi chú là "hồi sinh" mà không ai đọc tin mới.

**Ai sửa quyết định `ConfirmedAt`, không phải có sửa hay không:**

| Ai sửa | `ConfirmedAt` | Vì sao |
|---|---|---|
| Người dùng, qua UI (`PUT`) | Giữ nguyên | Đang đọc chính cái mình viết, không cần ký lại |
| Agent, qua MCP (`upsert_company_dossier`) | Về `null` (`AgentDraftedAt` set) | Người dùng chưa đọc bản mới — nếu giữ nguyên thì đây là cửa hậu của quy tắc "agent không ký được" |

**Agent viết được, không ký được** — điểm tựa của toàn bộ thiết kế: không có MCP tool nào đặt được `ConfirmedAt`. Agent có `get_company_fundamentals` để lấy số liệu doanh nghiệp làm nguyên liệu trước khi soạn, nhưng số liệu đó **không** nằm trong điều kiện của cổng: cổng vẫn đòi mô hình kinh doanh, moat, yếu tố rủi ro và chữ ký của người dùng. Nếu agent vừa viết vừa xác nhận thì gate đo "agent đã điền gì đó", không đo hiểu biết của người bỏ tiền. Chi tiết + 7 quyết định đi cùng: [ADR-0011](adr/0011-company-dossier-gate-at-plan-creation.md).

**Điểm bắn gate:**

- Tạo `TradePlan` mới — luôn chạy, đầu `Handle`, trước cả nhánh auto-transition khi tạo với `Status="Executed"`.
- Sửa `TradePlan` — chỉ khi **tỷ lệ** cũ `< 5%` và tỷ lệ mới `≥ 5%` (so hai thời điểm, mỗi vế dùng số dư của chính thời điểm đó), **hoặc** khi `Symbol` đổi (bất kể size — đổi mã là mở vị thế ở công ty khác, không phải điều chỉnh size).
- Plan đang chạy (Ready/InProgress/Executed) **không** bị soi lại dù hồ sơ liên quan hết hạn — gate chỉ áp cho đường tạo mới và đường sửa-vượt-ngưỡng.

**Không có grandfathering:** từ lúc deploy, mọi plan **mới** đều cần hồ sơ, kể cả mã đã giữ nhiều tháng. Lệnh đầu tiên sau deploy chắc chắn bị chặn ở mọi mã.

---

## 4. API Endpoints (tóm tắt)

| Module | Route prefix | Chức năng |
|--------|-------------|-----------|
| Auth | `/api/v1/auth` | Đăng nhập, đăng ký, JWT |
| Portfolios | `/api/v1/portfolios` | CRUD danh mục |
| Trades | `/api/v1/trades` | CRUD giao dịch, bulk import, link plan |
| TradePlans | `/api/v1/trade-plans` | CRUD kế hoạch, execute lot, update SL, scenario node trigger, scenario templates, **campaign review (P0.7)**: close + preview + update lessons + pending-review + analytics, **abort với thesis invalidation (Vin-discipline, 2026-04-23)** `POST {id}/abort` |
| Discipline | `/api/v1/me/discipline-score` | **Điểm Kỷ luật Thesis (Vin-discipline, 2026-04-23)** — GET với query `days` (7/30/90/365, default 90). Trả composite 0-100 (SL-Integrity 50% / Plan Quality 30% / Review Timeliness 20%) + Stop-Honor Rate primitive + sample size + trend. Cache 5 phút (IMemoryCache). |
| Discipline | `/api/v1/me/thesis-reviews/pending` | **Pending thesis reviews (V2.1, 2026-04-23)** — GET list plan Ready/InProgress có `InvalidationRule.CheckDate ≤ today+2` (VN UTC+7 local day granularity) HOẶC `ExpectedReviewDate ≤ today`, chưa triggered, không legacy-exempt. Sort DESC theo `DaysOverdue`. Response `PendingThesisReviewDto[]` với reasons list + trigger type + due date. |
| Discipline | `/api/v1/me/discipline-score/streak` | **Discipline streak (PR-2, 2026-05-04)** — GET số ngày liên tiếp gần nhất user không có SL violation. Cho empty state Decision Queue: khi 0 alert hiển thị `✅ Hôm nay đang kỷ luật + 🔥 X ngày`. Response `DisciplineStreakDto { daysWithoutViolation, hasData }`. Logic mirror `DisciplineScoreCalculator`: closed loss trade với `avgExit < SL` (Buy) là violation. `hasData = false` khi user chưa có plan. |
| Decisions | `/api/v1/decisions/queue` | **Decision Queue (PR-2 P3, 2026-05-04; mở rộng 2026-08-09 ADR-0009)** — GET aggregate **5 nguồn** alert thành 1 list duy nhất. Ba nguồn phòng thủ: (1) StopLossHit từ `IRiskCalculationService` filter `DistanceToStopLossPercent ≤ 2%` (≤ 0 = Critical, ≤ 2 = Warning), (2) ScenarioTrigger từ `IScenarioAdvisoryService` (Warning), (3) ThesisReviewDue từ `GetPendingThesisReviewsQuery` (DaysOverdue ≥ 3 = Critical, else Warning). Hai nguồn phía vào lệnh: (4) **MissingStopLoss** — vị thế mở có `StopLossPrice == null` và giá > 0 (Warning); trước đây bị `continue` bỏ qua nên queue rỗng đọc nhầm thành "an toàn". (5) **BuyOpportunity** — mã watchlist có `TargetBuyPrice > 0` và giá hiện tại ≤ mục tiêu (Info, `PortfolioId` rỗng); chỉ fetch giá cho mã CÓ mục tiêu, batch một lần qua `IStockPriceService.GetCurrentPricesAsync`, timeout 5s → hỏng thì phần cơ hội vắng mặt chứ không làm hỏng queue. Dedupe theo (Symbol, PortfolioId) giữ severity cao nhất, ưu tiên StopLossHit; item `PortfolioId` rỗng thoát dedupe nên cơ hội mua và cảnh báo rủi ro cùng mã không nuốt nhau. Sort severity desc → DueAt asc, nên Info luôn nằm dưới mọi rủi ro. Response `DecisionQueueDto { Items, TotalCount }`. Item Id composite `{type}:{sourceId}`. |
| Decisions | `/api/v1/decisions/{id}/resolve` | **Resolve Decision (PR-3 P4, 2026-05-04)** — POST inline resolve cho 1 DecisionItem. Body `{ Action, TradePlanId, Symbol, Note }` PascalCase. Action `ExecuteSell`: validate plan + portfolio ownership, tính quantity (single-lot = `plan.Quantity`, multi-lot = sum `lot.PlannedQuantity` của Executed lots), tạo Trade SELL với giá hiện tại từ `IStockPriceService`, link plan, update portfolio. Action `HoldWithJournal`: validate Note ≥ 20 chars (Trim), tạo `JournalEntry` với `EntryType=Decision`, Tags `["decision-hold", "trigger:{type}"]`. Response `ResolveDecisionResult { ResultId, Message, ResultType: "Trade"\|"JournalEntry" }`. JWT-authorized. |
| Strategies | `/api/v1/strategies` | CRUD chiến lược, performance |
| Journals | `/api/v1/journals` | CRUD nhật ký |
| Risk | `/api/v1/risk` | Profile, summary, drawdown, correlation, position-sizing (5 models) |
| Alerts | `/api/v1/alerts` | CRUD rules, history |
| Analytics | `/api/v1/analytics` | Performance, equity curve, monthly returns |
| Capital Flows | `/api/v1/capital-flows` | Record, history, TWR/MWR |
| Snapshots | `/api/v1/snapshots` | Take, range, compare |
| Market Data | `/api/v1/market` | Price, history, batch, index, overview, stock detail, search, top fluctuation, trading summary, **technical analysis**, **`GET /stock/{symbol}/fundamentals`** (số liệu doanh nghiệp 24hmoney làm nguyên liệu viết hồ sơ công ty — kèm `unavailableSections[]`, phần có tên trong đó là KHÔNG lấy được chứ không phải bằng 0; cả `company` và `indicators` đều rỗng nội dung → 404, kể cả khi provider trả về object có đủ field null) |
| Backtests | `/api/v1/backtests` | Queue, list, detail |
| Positions | `/api/v1/positions` | Active positions |
| P&L | `/api/v1/pnl` | Lãi/lỗ calculations |
| Fees | `/api/v1/fees` | Phí giao dịch |
| AI Settings | `/api/v1/ai-settings` | CRUD cấu hình AI (provider, API keys, model, usage) |
| AI | `/api/v1/ai` | Streaming SSE: journal-review, portfolio-review, trade-plan-advisor, chat, monthly-summary, stock-evaluation, **risk-assessment**, **position-advisor**, **trade-analysis**, **watchlist-scanner**, **daily-briefing**, **comprehensive-analysis**, **portfolio-critique** (2026-05-04, adversarial coach role thay daily-briefing trên Dashboard) + JSON: build-context (copy prompt) |
| Admin | `/api/v1/admin` | **Debug tooling (admin-only)**: `impersonate` bắt đầu phiên xem-như-user, `impersonate/stop` kết thúc. Chặn nested impersonate + block mutation theo config. |
| ApiKeys | `/api/v1/api-keys` | **Personal access tokens (ADR-0003)**: `POST` tạo key → 201 (trả plaintext một lần); `GET` danh sách (DTO không chứa hash/token); `DELETE /{id}` revoke → 204. Tất cả JWT-authed, owner-scoped. |
| AI (ApiKey scheme) | `/api/v1/ai/daily-digest` | **Endpoint opt-in đầu tiên xác thực bằng ApiKey** (`X-Api-Key`, KHÔNG JWT): `POST` → JSON `{ systemPrompt, userMessage }` = bản tin hằng ngày + cash/net-worth + position-sizing. Controller riêng `AiDigestController` (không gộp vào `AiController` JWT-only vì 2 `[Authorize]` khác scheme cộng dồn AND). |
| AI Agent (ApiKey scheme) | `/api/v1/ai/agent` | **Write-surface cho NPU/Claude (ADR-0004, 2026-07-21)** — `[Authorize(Scheme=ApiKey)]`. `GET trade-plans`, `GET trade-plans/{id}`, `POST trade-plans` (forces Draft), `PUT trade-plans/{id}`, `PATCH trade-plans/{id}/status` (blocks `restore`), `POST trades` (**`portfolioId`/`fee`/`tax` optional — auto-resolve, ADR-0005**), `GET doc`. Audit: `Source=AI_AGENT` trong Metadata. Ownership-enforced trên mọi command (assert `portfolio.UserId == sub`). |
| AI Agent expose (ApiKey scheme) | `/api/v1/ai/agent/{positions,watchlists,journal-entries,journals,symbols}` | **Đọc/ghi mở rộng cho NPU/Claude (extends ADR-0004, 2026-07-23)** — 21 route mirror 5 controller JWT (positions đọc; watchlist CRUD 9; journal-entries 5; journals 5; symbol timeline đọc). 5 controller anh em cùng base `AiAgentControllerBase`, re-dispatch MediatR sẵn có, `UserId` từ `sub`, không thêm business logic. Watchlist/journal write low-stakes (không gate "chốt"). Doc gộp vào `GET /ai/agent/doc`. |
| AI Agent portfolio+fee (ApiKey scheme) | `/api/v1/ai/agent/{portfolios,fees/calculate}` | **Đủ thông tin khi mở/đóng vị thế (ADR-0005, 2026-07-23)** — `GET portfolios` (mirror `GetAllPortfoliosQuery`) để lấy `portfolioId`; `POST fees/calculate` (mirror `FeesController`, inject `IFeeCalculationService`) để tính phí/thuế. Cùng với `POST trades` nới lỏng: `portfolioId` bỏ trống → auto-pick khi user có đúng 1 danh mục (0/>1 → `400`); `fee`/`tax` bỏ trống → tự tính (fee = phí giao dịch + VAT + TNCN; TNCN 0.1% chỉ SELL). Resolve nằm ở agent controller, JWT `CreateTradeCommand` không đổi. |
| MCP (ApiKey scheme) | `/mcp` | **Model Context Protocol server (2026-07-24)** — cùng toàn bộ bề mặt agent trên nhưng dạng **tool có schema** cho MCP client (Claude Desktop/IDE/NPU). Streamable HTTP, stateless, sau ApiKey scheme (`UserId` = `sub`). **46 tool** (11 lớp `[McpServerToolType]` trong `Mcp/`) re-dispatch đúng MediatR command/query — không thêm business logic. 29 tool mirror bề mặt `AiAgent*Controller`; **8 tool P0 Decision & Risk (chỉ đọc, 2026-07-25)** mở query chưa có ở REST agent: decision queue (gộp alert SL + scenario + thesis review), discipline score/streak, portfolio risk (VaR/Sharpe/drawdown), stop-loss targets, trailing-stop alerts, pending thesis reviews, scenario advisories. **+1 tool `get_daily_digest` (2026-07-26)** — bản tin hằng ngày (danh mục + số dư + sizing) dạng MCP tool, thay cho REST `POST /ai/daily-digest` phía agent. **8 tool P1 Analytics (chỉ đọc, 2026-07-26)** — "tôi đang làm ăn thế nào": performance (total/MTD/YTD), equity curve, monthly returns, savings comparison (alpha vs gửi tiết kiệm, param `annualRate`/`asOf`), campaign analytics (win rate, lọc `timeHorizon`), net worth summary, flow history (nạp/rút/cổ tức, lọc `from`/`to`), adjusted return (TWR/MWR) — 6/8 per-portfolio, ownership check ở handler. Read → `ReadOnly`, write → `Destructive` (host tự hỏi xác nhận). Additive: REST `/ai/agent/*` giữ nguyên; MCP thay `/doc` markdown bằng `tools/list`. |
| PersonalFinance | `/api/v1/personal-finance` | **Tài chính cá nhân (Tier 3)**: profile, net worth summary với health score 0-100, live gold prices từ 24hmoney, CRUD accounts với Gold auto-calc, **CRUD debts + Net Worth + rule 4 cảnh báo nợ tiêu dùng lãi cao** |
| CompanyDossiers | `/api/v1/company-dossiers` | **Hồ sơ công ty — gate chặn tạo trade plan (2026-08-10, ADR-0011)**: `GET` list, `GET /{symbol}`, `PUT /{symbol}` upsert (JWT luôn `ByAgent=false`), `POST /{symbol}/confirm` (đường duy nhất đặt `ConfirmedAt`), `GET /{symbol}/gate-status` pre-flight (`quantity`/`entryPrice`/`accountBalance` bắt buộc, thiếu → 400), `GET /{symbol}/suggested-rules` (3 rủi ro hạng cao nhất ghép thành câu cho `InvalidationRule`, kèm cờ `meetsMinLength`), `GET /needing-review` (hồ sơ Expired/Unconfirmed/NeedsReview, xếp cái đang chặn lên trước) |

---

## 5. Frontend Pages

| Route | Trang | Mô tả |
|-------|-------|-------|
| `/dashboard` | Dashboard | Tổng quan: P&L, CAGR, equity chart, vị thế nổi bật |
| `/portfolios` | Danh mục | Danh sách & chi tiết danh mục |
| `/trades` | Giao dịch | Lịch sử giao dịch, lọc, import CSV |
| `/trades/create` | Tạo GD | Form tạo giao dịch mua/bán |
| `/trades/import` | Import | Nhập giao dịch hàng loạt từ CSV |
| `/trade-plan` | Kế hoạch | Lập & quản lý kế hoạch giao dịch |
| `/trade-wizard` | Wizard | Flow 5 bước giao dịch có kỷ luật |
| `/positions` | Vị thế | Các vị thế đang mở, SL/TP bar |
| `/strategies` | Chiến lược | CRUD chiến lược giao dịch |
| `/journals` | Nhật ký | Nhật ký giao dịch |
| `/analytics` | Phân tích | Hiệu suất, Sharpe, Sortino, etc. |
| `/risk` | Rủi ro | Profile, SL targets, correlation |
| `/risk-dashboard` | Dashboard RR | Tổng quan sức khỏe rủi ro |
| `/alerts` | Cảnh báo | Rules & lịch sử cảnh báo |
| `/capital-flows` | Dòng tiền | Nạp/rút/cổ tức |
| `/corporate-actions` | Sự kiện quyền | Cổ tức tiền mặt, cổ tức cổ phiếu, chia tách |
| `/snapshots` | Lịch sử | Ảnh chụp & so sánh danh mục |
| `/market-data` | Thị trường | Chỉ số thị trường, tra cứu cổ phiếu chi tiết, **phân tích kỹ thuật (EMA/RSI/MACD/Volume/S&R)**, **AI đánh giá nhanh mã (fundamental + technical)**, tìm kiếm mã, top biến động, bảng giá nhanh, lịch sử giá |
| `/backtesting` | Kiểm thử | Mô phỏng chiến lược |
| `/monthly-review` | Tổng kết tháng | Review hiệu suất hàng tháng |
| `/ai-settings` | Cài đặt AI | Provider (Claude/Gemini), API keys, model, thống kê sử dụng |
| `/campaign-analytics` | Phân tích chiến dịch | Tổng hợp hiệu suất cross-plan: summary cards, so sánh, best/worst, lessons feed (P0.7) |
| `/personal-finance` | Tài chính cá nhân | Net worth cards + **Net Worth = Assets − Debt** card + health score 0-100 (4 rules incl. high-interest consumer debt) + accounts CRUD (incl. Gold auto-calc) + **debts CRUD** + settings (Tier 3) |
| `/company-dossier` | Hồ sơ công ty | Danh sách hồ sơ theo mã, kèm badge trạng thái tươi (Fresh/NeedsReview/Expired/Unconfirmed) |
| `/company-dossier/:symbol` | Chi tiết hồ sơ | Mặc định là **bản đọc**; bấm Sửa ra form (business model + moats + risk factors ▲▼, dấu hiệu quan sát bắt buộc, tối đa 1 deal-breaker). Nút ký ở cuối trang, hiện ở cả hai chế độ nhưng **khoá khi form còn thay đổi chưa lưu**. Có sao chép/dán nội dung với AI ngoài |

---

## 6. Quy tắc Nghiệp vụ Quan trọng

1. **Lô chẵn**: Mua cổ phiếu phải là bội của 100 (quy định sàn HOSE)
2. **Không mua vượt cash**: Giá trị lệnh mua ≤ cash còn lại trong danh mục
3. **Symbol uppercase**: Luôn normalize thành uppercase (VNM, FPT, VCB)
4. **CAGR đơn nguồn**: Headline Cockpit lấy household CAGR từ `/analytics/household/performance` — không weighted-average từ per-portfolio (sai về cấu trúc cho returns nhân tính), không lấy `portfolios[0]` (lừa user khi danh mục lớn không phải đầu list).
5. **Position size ≤ 100%**: Mẫu số dùng `Math.Max(netWorth, totalMarketValue)`
6. **Soft delete**: Entities dùng `isDeleted` flag, không xóa vĩnh viễn
7. **Tiền tệ**: Mặc định VND, format bằng `VndCurrencyPipe`
8. **Ngôn ngữ UI**: Tiếng Việt có dấu đầy đủ
9. **MarkReviewed requires CampaignReviewData**: Chuyển TradePlan sang Reviewed phải kèm `CampaignReviewData` với auto-calculated metrics (P&L, %, VND/ngày, annualized return). Không cho phép review "trống" — `CampaignReviewService` tính toán tự động từ trades thực tế
10. **Admin impersonation**: JWT impersonate có `sub=targetId, actor=adminId, amr=impersonate`, TTL 1h. `ImpersonationAudit.IsRevoked=true` → token coi như hết hạn (check ở `ImpersonationValidationMiddleware`). Cấm nested impersonate (`[RequireAdmin]` reject token có `amr=impersonate`). Mutation POST/PUT/DELETE/PATCH bị chặn khi impersonate trừ khi `Admin:AllowImpersonateMutations=true`.
11. **Gold auto-calc**: Khi user thêm Gold account với 3 field đủ (brand + type + quantity), Application layer fetch `IGoldPriceProvider.GetPriceAsync(brand, type)` → Balance = quantity × **BuyPrice** (giá tiệm mua vào = giá user bán được, định giá tài sản theo thanh khoản thực tế, không dùng SellPrice). Provider null → throw 400 với Vietnamese message (không silent fallback). Domain không phụ thuộc provider — pattern provider-agnostic.
12. **Last Securities protection**: Không cho phép xóa tài khoản Securities cuối cùng trong FinancialProfile (`FinancialProfile.RemoveAccount` throw `InvalidOperationException` với message "Không thể xóa tài khoản Chứng khoán cuối cùng"). Gold và các loại khác xóa được bất kỳ lúc nào.
13. **Full VND vs triệu VND quirk**: Giá vàng 24hmoney trả full VND (167,200,000) mặc dù UI label nói "triệu VNĐ/lượng" — khác pattern giá CP 24hmoney (÷1000 trong API). Không scale ×1000. Fixture test `PricesAreFullVND_NotScaledBy1000` lock behavior.
14. **Debt delete requires paid-off**: `FinancialProfile.RemoveDebt` throws `InvalidOperationException` khi `Principal > 0` — user phải đặt `Principal=0` trước khi xóa. Chống xóa nhầm dữ liệu thật, đối xứng với rule account.
15. **High-interest consumer debt rule**: Health score rule 4 trừ **−20 điểm cứng (binary)** khi có debt type `CreditCard` hoặc `PersonalLoan` với `InterestRate > 20%/năm` (strict). Ngưỡng cutoff theo thực tế VN (CC ~24-36%, vay tín chấp ~15-25%). `Mortgage/Auto/Installment` không áp rule này. Null interest = 0 (không trigger).
16. **Trade creation ownership check (AI Agent IDOR fix, 2026-07-21)**: `CreateTradeCommand` và `BulkCreateTradesCommand` handlers phải assert `portfolio.UserId == sub` sau khi load portfolio — không chỉ kiểm tra portfolio tồn tại. Áp dụng cho mọi caller (JWT lẫn ApiKey). Audit marker `Source=AI_AGENT` được ghi vào `Metadata` khi request đến từ `AiAgentController`.
17. **Size-based thesis discipline gate (Vin-discipline, 2026-04-23)**: TradePlan muốn chuyển `Draft → Ready` hoặc `Draft → InProgress` phải pass gate theo **size**: nếu `Quantity × EntryPrice ≥ 5% AccountBalance` → **bắt buộc** `Thesis.Length ≥ 30` + `InvalidationCriteria.Count ≥ 1` (mỗi rule `Detail.Length ≥ 20`); nếu plan size nhỏ hơn hoặc `AccountBalance` null → chỉ cần `Thesis.Length ≥ 15`, rule optional. Gate fold vào `MarkReady()` và `MarkInProgress()`, throw `InvalidOperationException` → controller map HTTP 400 code `DISCIPLINE_GATE_FAILED`. Plan có `LegacyExempt=true` được miễn gate khi edit Draft (nhưng vẫn bị gate khi transition).
18. **Company dossier gate chặn ngay lúc tạo plan (2026-08-10, ADR-0011)**: `TradePlan` không tạo được cho một mã chưa có `CompanyDossier` đã ký và còn hiệu lực (`Fresh`/`NeedsReview`) — chặn ở lúc **tạo**, không phải lúc `Draft → Ready` như gate #17. Sửa plan cũng chạy gate khi tỷ lệ vượt ngưỡng 5% hoặc khi `Symbol` đổi. Không có `LegacyExempt` tương đương — mọi plan mới đều cần hồ sơ, không có ngoại lệ chuyển tiếp. Throw `DossierGateException` (kế thừa `InvalidOperationException`) → HTTP 400 code `DOSSIER_GATE_FAILED` kèm `missing[]`. Chi tiết §3.15.
