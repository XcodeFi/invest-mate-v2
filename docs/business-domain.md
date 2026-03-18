# Investment Mate v2 — Bản đồ Nghiệp vụ

> Tài liệu tham chiếu nhanh cho AI agents và developers mới.
> Cập nhật lần cuối: 2026-03-15

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
 │    ├── CapitalFlow (N)   ← Nạp/rút/cổ tức
 │    ├── RiskProfile (1)   ← Cấu hình rủi ro
 │    └── Snapshot (N)      ← Ảnh chụp trạng thái theo ngày
 │
 ├── TradePlan (N)          ← Kế hoạch giao dịch
 │    ├── PlanLot (N)       ← Các lô mua (ScalingIn/DCA)
 │    ├── ExitTarget (N)    ← Mục tiêu thoát (TP/CL/Trailing)
 │    └── Checklist (N)     ← Danh sách kiểm tra
 │
 ├── Strategy (N)           ← Chiến lược giao dịch
 ├── TradeJournal (N)       ← Nhật ký giao dịch
 ├── AlertRule (N)          ← Cảnh báo giá/rủi ro
 ├── Backtest (N)           ← Kiểm thử chiến lược
 │
 ├── DailyRoutine (N)       ← Nhiệm vụ hàng ngày (1 per user per day)
 │    └── RoutineItem (N)   ← Các bước trong routine (embedded)
 │
 └── RoutineTemplate (N)    ← Mẫu routine (5 built-in + custom)
      └── RoutineItemTemplate (N)
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

---

## 3. Các nghiệp vụ chính

### 3.1. Quản lý Danh mục (Portfolio)
- Tạo danh mục với vốn ban đầu (`initialCapital`)
- Nạp/rút tiền qua `CapitalFlow` (Deposit, Withdraw, Dividend, Interest, Fee)
- Tính **cash còn lại** = initialCapital + tổng flows - tổng giá trị mua + tổng giá trị bán

### 3.2. Kế hoạch Giao dịch (TradePlan)
Trạng thái: `Draft → Ready → InProgress → Executed → Reviewed | Cancelled`

**3 chế độ vào lệnh (EntryMode):**

| Mode | Mô tả |
|------|--------|
| Single | Mua 1 lần duy nhất |
| ScalingIn | Chia nhiều lô, mỗi lô có giá/số lượng/% phân bổ |
| DCA | Mua định kỳ (weekly/biweekly/monthly) với số tiền cố định |

**Mục tiêu thoát (ExitTarget):**
- TakeProfit, CutLoss, TrailingStop, PartialExit
- Mỗi target có `price`, `percentOfPosition`, `isTriggered`

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
- **RiskProfile**: maxPositionSize%, maxDrawdownAlert%, defaultRR
- **Position Sizing**: `positionSize = accountBalance × riskPercent / (entry - stopLoss)`
- **Stop-loss tracking**: lịch sử thay đổi SL, cảnh báo khi giá gần SL
- **Correlation matrix**: tương quan giữa các cổ phiếu trong danh mục

### 3.6. Phân tích Hiệu suất (Analytics)
- **CAGR**: tính từ equity curve (ưu tiên) hoặc backend AdvancedAnalytics
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

---

## 4. API Endpoints (tóm tắt)

| Module | Route prefix | Chức năng |
|--------|-------------|-----------|
| Auth | `/api/v1/auth` | Đăng nhập, đăng ký, JWT |
| Portfolios | `/api/v1/portfolios` | CRUD danh mục |
| Trades | `/api/v1/trades` | CRUD giao dịch, bulk import, link plan |
| TradePlans | `/api/v1/trade-plans` | CRUD kế hoạch, execute lot, update SL |
| Strategies | `/api/v1/strategies` | CRUD chiến lược, performance |
| Journals | `/api/v1/journals` | CRUD nhật ký |
| Risk | `/api/v1/risk` | Profile, summary, drawdown, correlation |
| Alerts | `/api/v1/alerts` | CRUD rules, history |
| Analytics | `/api/v1/analytics` | Performance, equity curve, monthly returns |
| Capital Flows | `/api/v1/capital-flows` | Record, history, TWR/MWR |
| Snapshots | `/api/v1/snapshots` | Take, range, compare |
| Market Data | `/api/v1/market` | Price, history, batch, index, overview, stock detail, search, top fluctuation, trading summary |
| Backtests | `/api/v1/backtests` | Queue, list, detail |
| Positions | `/api/v1/positions` | Active positions |
| P&L | `/api/v1/pnl` | Lãi/lỗ calculations |
| Fees | `/api/v1/fees` | Phí giao dịch |

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
| `/snapshots` | Lịch sử | Ảnh chụp & so sánh danh mục |
| `/market-data` | Thị trường | Chỉ số thị trường, tra cứu cổ phiếu chi tiết, tìm kiếm mã, top biến động, bảng giá nhanh, lịch sử giá |
| `/backtesting` | Kiểm thử | Mô phỏng chiến lược |
| `/monthly-review` | Tổng kết tháng | Review hiệu suất hàng tháng |

---

## 6. Quy tắc Nghiệp vụ Quan trọng

1. **Lô chẵn**: Mua cổ phiếu phải là bội của 100 (quy định sàn HOSE)
2. **Không mua vượt cash**: Giá trị lệnh mua ≤ cash còn lại trong danh mục
3. **Symbol uppercase**: Luôn normalize thành uppercase (VNM, FPT, VCB)
4. **CAGR đơn nguồn**: Ưu tiên equity curve, fallback backend — không tính riêng
5. **Position size ≤ 100%**: Mẫu số dùng `Math.Max(netWorth, totalMarketValue)`
6. **Soft delete**: Entities dùng `isDeleted` flag, không xóa vĩnh viễn
7. **Tiền tệ**: Mặc định VND, format bằng `VndCurrencyPipe`
8. **Ngôn ngữ UI**: Tiếng Việt có dấu đầy đủ
