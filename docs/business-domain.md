# Investment Mate v2 — Bản đồ Nghiệp vụ

> Tài liệu tham chiếu nhanh cho AI agents và developers mới.
> Cập nhật lần cuối: 2026-03-24

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
 ├── RoutineTemplate (N)    ← Mẫu routine (5 built-in + custom)
 │    └── RoutineItemTemplate (N)
 │
 ├── Watchlist (N)           ← Danh sách theo dõi cổ phiếu
 │    └── WatchlistItem (N)  ← Mã CP + ghi chú + giá mục tiêu (embedded)
 │
 └── AiSettings (1)          ← Cấu hình AI đa nhà cung cấp (Claude + Gemini)
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

### 3.12. External Data Providers

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
| Market Data | `/api/v1/market` | Price, history, batch, index, overview, stock detail, search, top fluctuation, trading summary, **technical analysis** |
| Backtests | `/api/v1/backtests` | Queue, list, detail |
| Positions | `/api/v1/positions` | Active positions |
| P&L | `/api/v1/pnl` | Lãi/lỗ calculations |
| Fees | `/api/v1/fees` | Phí giao dịch |
| AI Settings | `/api/v1/ai-settings` | CRUD cấu hình AI (provider, API keys, model, usage) |
| AI | `/api/v1/ai` | Streaming SSE: journal-review, portfolio-review, trade-plan-advisor, chat, monthly-summary, stock-evaluation, **risk-assessment**, **position-advisor**, **trade-analysis**, **watchlist-scanner**, **daily-briefing**, **comprehensive-analysis** + JSON: build-context (copy prompt) |

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
| `/market-data` | Thị trường | Chỉ số thị trường, tra cứu cổ phiếu chi tiết, **phân tích kỹ thuật (EMA/RSI/MACD/Volume/S&R)**, **AI đánh giá nhanh mã (fundamental + technical)**, tìm kiếm mã, top biến động, bảng giá nhanh, lịch sử giá |
| `/backtesting` | Kiểm thử | Mô phỏng chiến lược |
| `/monthly-review` | Tổng kết tháng | Review hiệu suất hàng tháng |
| `/ai-settings` | Cài đặt AI | Provider (Claude/Gemini), API keys, model, thống kê sử dụng |

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
