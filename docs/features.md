# Investment Mate v2 — Tài liệu Tính năng

> **Cập nhật lần cuối:** 2026-03-17
> **Trạng thái:** Phase 7 đang tiếp tục + Tích hợp 24hmoney API

---

## Tổng quan các Phase

| Phase | Tên | Trạng thái | Branch |
|:---:|-----|:---:|--------|
| 1–2 | Nền tảng & Workflow | ✅ Done | `master` |
| 3–4 | Charts & Links | ✅ Done | `master` |
| 5 | Auto-fill, Risk & Compound | ✅ Done | `feature/phase5-autofill-risk-compound` |
| 6 | Trade Plan Template | ✅ Done | `feature/phase6-trade-plan-template` |
| 7 | UX Improvements & Thuật ngữ | 🔄 In Progress | `feature/phase7-improvements` |

---

## Phase 1–2: Nền tảng & Workflow

### Wizard Giao dịch (`/trade-wizard`)

Flow 5 bước dẫn dắt giao dịch có kỷ luật:

| Bước | Nội dung | Ghi chú |
|:---:|---------|---------|
| 1 | Chọn Chiến lược | Tùy chọn, bỏ qua được |
| 2 | Lập Kế hoạch | Entry/SL/TP + Position Sizing realtime |
| 3 | Checklist | 8 mục (5 bắt buộc) → GO/NO-GO |
| 4 | Xác nhận & Ghi GD | Tóm tắt + gọi API tạo trade |
| 5 | Nhật ký | Pre-fill từ thông tin bước trên |

**Enforcement:** Không thể sang bước tiếp nếu chưa đủ điều kiện.
**Component:** `trade-wizard.component.ts`
**Services dùng:** `StrategyService`, `TradeService`, `RiskService`, `JournalService`, `MarketDataService`

---

### Dashboard Cockpit (`/dashboard`)

**Summary cards:** Tổng Giá trị, Đã Đầu tư, Tổng Lãi/Lỗ, CAGR

**Compound Growth Tracker:**
- CAGR hiện tại (tính từ equity curve hoặc ước tính từ P&L)
- Đặt mục tiêu CAGR + kỳ hạn → so sánh Thực tế vs Mục tiêu
- Ước tính vốn sau 5/10/20 năm
- Progress bar % đạt mục tiêu

**Mini Equity Curve:**
- Line chart Chart.js, hiển thị khi có đủ snapshot data
- Range filter: 30D / 90D / 1Y / All

**Risk Alert Banner:**
- Stop-loss proximity (≤5% → warning, ≤2% → danger)
- Drawdown vượt ngưỡng (>10% → warning, >20% → danger)
- **Cảnh báo tập trung danh mục** (positionSizePercent > maxPositionSizePercent từ Risk Profile)

**Market Overview Strip (v2.9):** 4 index cards (VN-INDEX, VN30, HNX, UPCOM) — giá, %, KL — dữ liệu real-time từ 24hmoney

**Vị thế nổi bật (v2.8):** Top 6 positions theo giá trị, hiện symbol/SL/qty/P&L%, link đến `/positions`

**Portfolio List:** Vốn ban đầu, giá trị hiện tại, P&L, performance progress bar

---

### Trade Plan (`/trade-plan`)

Form lập kế hoạch đầy đủ với:
- Auto-fill giá khi nhập mã CP (debounce 500ms → MarketDataService)
- Mini stock info card (Open/High/Low/Close/Volume)
- Position Sizing tự động từ Risk Profile
- Checklist 13 mục (4 danh mục: Phân tích, Quản lý rủi ro, Tâm lý, Xác nhận)
- Risk violations enforcement với override confirmation
- Quick Reference Table (0.5% → 5% risk levels)
- **Template save/load** (Phase 6)

---

## Phase 3–4: Charts & Links

### Analytics (`/analytics`)

Tabs:
1. **Overview** — Bar chart P&L theo cổ phiếu, Donut chart phân bổ, Top Holdings table, Risk Metrics
2. **Trade Statistics** — Win rate, Profit Factor, Expectancy, Gross P&L
3. **Equity Curve** — Line chart ngày/giá trị, bảng daily return/cumulative return
4. **Monthly Returns** — Matrix năm × tháng, color-coded

**Services:** `PnlService`, `AnalyticsService`, `AdvancedAnalyticsService`

---

### Market Data (`/market-data`)

**Nguồn dữ liệu:** 24hmoney.vn API (real-time, cache IMemoryCache TTL 15s configurable)

- **Chỉ số thị trường**: Overview 4 index (VN-INDEX, VN30, HNX, UPCOM) — giá, thay đổi, KL, GT, NN mua/bán
- **Tra cứu cổ phiếu chi tiết**: Company info, giá OHLC, trần/sàn/tham chiếu, order book 3 mức (bid/ask), giao dịch nước ngoài, biến động giá (1D/1W/1M/3M/6M)
- **Tìm kiếm mã**: Autocomplete từ danh sách ~1800 công ty (cache 30 phút), debounce 300ms
- **Top biến động**: Tab HOSE/HNX/UPCOM, bảng mã biến động mạnh nhất
- **Bảng giá nhanh**: Nhập nhiều mã, xem giá close + KL
- **Lịch sử giá**: Khoảng thời gian tùy chọn, bảng OHLCV

**Quy tắc giá:** API 24hmoney trả giá cổ phiếu ÷1000 → nhân ×1000 khi mapping. Chỉ số index giữ nguyên.

**API endpoints:**

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET` | `/market/price/{symbol}` | Giá hiện tại (OHLCV) |
| `GET` | `/market/price/{symbol}/history` | Lịch sử giá |
| `GET` | `/market/prices?symbols=` | Bảng giá nhanh (batch) |
| `GET` | `/market/index/{symbol}` | Chi tiết chỉ số (VN-INDEX, VN30, HNX, UPCOM) |
| `GET` | `/market/overview` | Tổng quan tất cả chỉ số |
| `GET` | `/market/stock/{symbol}/detail` | Chi tiết cổ phiếu + order book |
| `GET` | `/market/search?keyword=` | Tìm kiếm mã/tên công ty |
| `GET` | `/market/top-fluctuation?floor=` | Top biến động (10=HOSE, 02=HNX, 03=UPCOM) |
| `GET` | `/market/stock/{symbol}/summary` | Biến động giá 1D/1W/1M/3M/6M |

**Services:** `MarketDataService` (FE), `HmoneyMarketDataProvider` (BE — implements `IMarketDataProvider` + `IStockInfoProvider`)

---

## Phase 5: Auto-fill, Risk & Compound

### Auto-fill giá cổ phiếu trong Trade Wizard

**File:** `trade-wizard.component.ts`

Khi blur khỏi ô "Mã chứng khoán":
- Gọi `MarketDataService.getCurrentPrice(symbol)`
- Spinner trong ô khi đang fetch
- Hiển thị "Giá hiện tại: X đ" bên dưới
- Tự điền `entryPrice` nếu đang trống → trigger `calculate()`

---

### Fix lỗi module market-data

**File:** `market-data.service.ts` dòng 5–6

Hai `import` bị viết dính trên 1 dòng, gây webpack chunk loading error khi vào `/market-data`. Đã tách thành 2 dòng riêng.

---

### Cảnh báo tập trung danh mục

**File:** `dashboard.component.ts` — `loadRiskAlerts()`

Logic mới trong `loadRiskAlerts()`:
1. Song song fetch `getPortfolioRiskSummary()` + `getRiskProfile()` bằng `forkJoin`
2. Với từng position: so sánh `positionSizePercent` vs `profile.maxPositionSizePercent`
3. Vượt giới hạn → warning; vượt 1.5× giới hạn → danger
4. Alert hiển thị: "Tập trung quá mức: X% danh mục (giới hạn Y%)"

---

## Phase 6: Trade Plan Template

### Tổng quan

Cho phép lưu kế hoạch giao dịch thành template tái sử dụng. Chọn template → điền sẵn toàn bộ form.

---

### Backend

**Entity:** `TradePlanTemplate.cs` (`InvestmentApp.Domain/Entities/`)

```
Id, UserId, Name, Symbol?, Direction, EntryPrice?, StopLoss?,
Target?, StrategyId?, MarketCondition, Reason?, Notes?,
CreatedAt, UpdatedAt
```

**API Endpoints** (`TemplatesController.cs` — yêu cầu JWT):

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET` | `/api/v1/templates/trade-plans` | Danh sách templates của user |
| `POST` | `/api/v1/templates/trade-plans` | Tạo template mới |
| `DELETE` | `/api/v1/templates/trade-plans/{id}` | Xoá template |

**Auth:** `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
**Scoping:** UserId từ JWT claim `sub` — user chỉ thấy template của mình.

---

### Frontend

**Service:** `TradePlanTemplateService` (`core/services/trade-plan-template.service.ts`)

```typescript
getAll(): Observable<TradePlanTemplate[]>
create(request): Observable<TradePlanTemplate>
delete(id): Observable<void>
```

**UI trong `/trade-plan`** (trên đầu trang, full-width panel):

```
[ Tải template: [-- Chọn --  ▼]  [Tải]  [Xoá] ]   [ + Lưu làm template ]
                                                      ↓ khi click:
                                              [ Tên template... ] [Lưu] [✕]
```

**Hành vi:**
- **Load:** Điền symbol, direction, entry/SL/TP, strategyId, marketCondition, reason, notes vào form → `recalculate()` tự động
- **Save:** Đặt tên → gọi API → prepend vào dropdown ngay lập tức
- **Delete:** Xoá khỏi API + xoá khỏi dropdown; reset `selectedTemplateId`

---

## Phase 7: UX Improvements & Thuật ngữ

### Quick Trade Widget (Dashboard)

**File:** `dashboard.component.ts`

Panel thu gọn được (collapsible) trên Dashboard:

- Nhập mã CP → blur → auto-fetch giá hiện tại (MarketDataService)
- Chọn hướng Mua/Bán, Entry Price, Stop-Loss → tính Position Size từ Risk Profile
- Hiển thị: Rủi ro/lệnh, Số CP gợi ý, % danh mục, R:R
- Nút "Mở trong Trade Plan" → navigate đến `/trade-plan` với queryParams đã điền sẵn

---

### Multi-timeframe Switcher (Dashboard)

**File:** `dashboard.component.ts`

Tab row: **Hôm nay / Tuần này / Tháng này / Năm nay / Toàn bộ**

- Lọc equity curve data theo cutoff date của từng kỳ
- Hiển thị period return % và period P&L
- Không cần API call thêm — tính từ snapshot data đã có sẵn

---

### positionSize trong Trade Plan Template

**File:** `TradePlanTemplate.cs`, `TemplatesController.cs`, `trade-plan-template.service.ts`

- Thêm field `PositionSize?: int` vào entity và API
- `saveAsTemplate()` trong Trade Plan: lưu luôn `quantity` vào template
- `applyTemplate()`: điền lại số lượng CP + set `manualQuantity = true`

---

### Strategy SuggestedSlPercent & SuggestedRrRatio

**Backend:** `Strategy.cs` (Domain entity), `CreateStrategyCommand`, `UpdateStrategyCommand`, `GetStrategiesQuery` (CQRS), `StrategyDto`

**Frontend:** `strategy.service.ts`, `strategies.component.ts`

Hai trường mới trên Strategy:

- `SuggestedSlPercent`: % Stop-Loss gợi ý dưới giá vào (VD: 5 → SL = Entry × 0.95)
- `SuggestedRrRatio`: R:R gợi ý (VD: 2 → TP = Entry + 2 × Risk)

Form tạo chiến lược hiển thị 2 input với giải thích inline.

---

### Strategy Auto-fill SL/TP trong Trade Plan

**File:** `trade-plan.component.ts` — `onStrategyChange()`

Khi chọn chiến lược trong Trade Plan:

1. Nếu `suggestedSlPercent` tồn tại và entry price đã nhập → tính SL = Entry × (1 ± SL%) theo hướng Buy/Sell
2. Nếu `suggestedRrRatio` tồn tại và SL đã có → tính TP = Entry ± Risk × R:R
3. Chỉ điền khi ô đang trống (không ghi đè nếu người dùng đã tự nhập)
4. Hiển thị badge "✓ Tự động điền từ chiến lược" + gợi ý giá kể cả khi không tự điền

---

### Glossary thuật ngữ chuyên ngành

Hai cơ chế song song trong project:

**1. Footnote badge** — số thứ tự `(1)(2)(3)` trên nhãn field → glossary card tĩnh ở cuối form/trang:

| Component | Thuật ngữ được giải thích |
|-----------|--------------------------|
| `risk-dashboard` | VaR 95%(1), Max Drawdown(2), Win Rate(3), Profit Factor(4), Beta(5), Correlation(6) |
| `trade-wizard` | Stop-Loss(1), Take-Profit(2), % Rủi ro(3), R:R(4), Thiết lập kỹ thuật(5), FOMO(6) |
| `monthly-review` | Win Rate(1), P&L(2), Max Drawdown(3) |
| `journals` | Setup kỹ thuật(1), Trạng thái cảm xúc/FOMO(2), Mức tự tin(3), Post-trade Review(4) |
| `strategies` | Khung thời gian(*), Win Rate(1), P&L(2), Profit Factor(3) |
| `trade-plan` | Stop-Loss(1), Take-Profit(2), Số lượng(3), Mức tự tin(4), R:R(5) |

**CSS:** `sup[class*="text-"]` với `::before`/`::after` tự thêm dấu ngoặc — không cần thay đổi HTML.

**2. Hover tooltip** — icon `ⓘ` (Heroicons SVG) cạnh tên chỉ số → dark popup xuất hiện khi hover:

| Component | Thuật ngữ có tooltip |
| --------- | -------------------- |
| `analytics` (header cards) | CAGR, Sharpe, Sortino, Max Drawdown, Win Rate |
| `analytics` (Chỉ số rủi ro) | Win Rate, Profit Factor, Value at Risk (95%), Expectancy |
| `analytics` (Equity Curve) | Equity Curve, Lợi nhuận ngày, Lợi nhuận tích luỹ |

**CSS:** `.tooltip-trigger` + `.tooltip-box` trong `styles.css` — dùng lại được toàn project.

---

### Vị thế đang mở (`/positions`)

**File:** `positions.component.ts`, `positions.service.ts`

- Gom nhóm theo danh mục (portfolio): mỗi nhóm hiện tên, số vị thế, tổng giá trị, tổng P&L
- Mỗi vị thế: symbol, số lượng, giá TB, giá hiện tại, P&L (xanh/đỏ), linked plan
- Expand giao dịch gần nhất cho từng mã
- Dùng shared TradeType utilities cho hiển thị Mua/Bán
- **SL/TP distance bar (v2.8)**: thanh gradient SL→TP với marker giá hiện tại, cảnh báo khi gần SL
- **Sắp xếp (v2.8)**: dropdown sort theo Giá trị / Lãi-Lỗ / % / Mã CK

---

### Trade Plan Multi-lot & Order Sheet (`/trade-plan`)

**File:** `trade-plan.component.ts`, `trade-plan.service.ts`

- **Entry mode**: Một lần / Chia lô (ScalingIn) / DCA — mỗi mode có UI riêng
- **Lot editor** (ScalingIn): bảng dynamic add/remove lot, preset phân bổ (40/30/30, 50/50, equal)
- **DCA editor**: số tiền/lần, tần suất (tuần/2 tuần/tháng), số kỳ, ngày bắt đầu, khoảng giá, bảng lịch mua dự kiến với tích luỹ
- **Exit targets**: TP1, TP2, CutLoss, Trailing Stop với giá + % vị thế
- **Stop-loss history**: ghi nhận lịch sử thay đổi SL
- **Phiếu lệnh (Order Sheet)**: panel toggle hiển thị tóm tắt lệnh, nút copy clipboard, nút In (print), hiện danh mục + giá trị lệnh
- **Saved plans**: danh sách kế hoạch đã lưu, filter trạng thái, lot progress bar, thực hiện từng lot
- **Empty-when-zero placeholders**: các trường Giá vào, SL, TP, Số lượng hiện placeholder khi chưa nhập (không hiện "0")
- **Query param pre-fill**: nhận `?symbol=XXX` → tự điền mã CP + fetch giá (liên kết từ trang trades)
- **Save buttons trong sidebar**: nút "Lưu nháp" / "Lưu & Sẵn sàng" nằm ở cột phải (sidebar) thay vì header, đúng luồng UX cuộn xuống

**Backend:** `TradePlan.cs` entity, `TradePlansController.cs`, lifecycle Draft→Ready→InProgress→Executed→Reviewed→Cancelled

---

### Trade Create Improvements (`/trades/create`)

**File:** `trade-create.component.ts`

- **Lô chẵn**: lệnh MUA bắt buộc bội số 100
- **Kiểm tra số dư**: giá trị lệnh không vượt tiền còn lại danh mục
- **Dropdown danh mục**: hiện thêm tổng vốn bên cạnh tên
- **Position info**: hiện thông tin vị thế khi bán (đang nắm giữ, giá TB, P&L)
- **Fee auto-calculation**: tự tính phí + thuế từ FeeService

---

### Trades History Improvements (`/trades`)

**File:** `trades.component.ts`

- **Click symbol filter**: nhấn vào mã CK → tự fill ô filter, nút × clear
- **Pagination fix**: sửa lỗi nextPage/previousPage reset về trang 1
- Dùng shared TradeType utilities
- **Link KH (v2.8)**: nút "Gắn KH" cho trade chưa liên kết plan, dropdown chọn KH theo symbol. Nếu chưa có KH cho mã đó → nút "Tạo kế hoạch" navigate đến `/trade-plan?symbol=XXX`
- **Import CSV (v2.8)**: nút "Import CSV" → trang `/trades/import` — upload, preview, validate, bulk import

### Import CSV (`/trades/import`) — v2.8

**File:** `trade-import.component.ts`, `trade.service.ts`

- Upload file CSV, tự detect header
- Hỗ trợ separator: dấu phẩy, chấm phẩy, tab
- Preview table với validation (mã CK, loại, số lượng, giá)
- Hiện số dòng hợp lệ / lỗi
- Bulk import via `POST /api/v1/trades/bulk`
- Kết quả: số thành công / thất bại + chi tiết lỗi

### Journal tự động (Wizard) — v2.8

**File:** `trade-wizard.component.ts`

- Step 4 (Record Trade) tự động tạo journal entry với thông tin pre-fill
- Step 5 (Journal) update journal đã tạo thay vì tạo mới (tránh duplicate)
- `goToDashboard()` prompt save nếu chưa lưu

---

### Shared TradeType Enum

**File:** `shared/constants/trade-types.ts`

Refactor toàn bộ project (6+ components) sử dụng:
- `TradeType.BUY` / `TradeType.SELL` thay vì hardcode string
- `isBuyTrade()`, `isSellTrade()` — so sánh case-insensitive
- `getTradeTypeDisplay()` — trả về 'Mua'/'Bán'
- `getTradeTypeClass()` — trả về Tailwind CSS classes

Components đã refactor: `trade-plan`, `trade-wizard`, `trade-create`, `backtesting`, `positions`, `trades`

---

### Trade Replay (`/trade-replay/:id`)

Visualize toàn bộ vòng đời kế hoạch giao dịch trên biểu đồ giá thực:

| Thành phần | Mô tả |
|------------|-------|
| Biểu đồ giá | Chart.js line chart giá đóng cửa (từ 24hmoney API), phạm vi tự động từ ngày tạo KH đến ngày hoàn thành |
| Sự kiện vào lệnh | Scatter points tam giác xanh ▲ tại ngày/giá thực hiện từng lô |
| Sự kiện thoát lệnh | Scatter points tam giác đỏ ▼ tại ngày/giá trigger exit target |
| Stop-Loss | Đường ngang nét đứt đỏ, hiển thị lịch sử điều chỉnh SL theo thời gian |
| Mục tiêu | Đường ngang nét đứt xanh tại giá target |
| Tạo kế hoạch | Scatter point ngôi sao xanh ★ tại ngày tạo plan |
| Summary cards | Giá vào lệnh (KH/TT), Lãi/Lỗ, R:R (KH/TT), Phí GD |
| Timeline | Dòng thời gian sự kiện theo thứ tự: Tạo KH → Vào lệnh → Điều chỉnh SL → Thoát lệnh → Hoàn thành |

**Entry point:** Nút "Xem replay" (icon film) trên bảng kế hoạch đã lưu — chỉ hiện cho status `Executed` / `Reviewed`.

**Dữ liệu kết hợp từ:** `TradePlanService`, `MarketDataService.getPriceHistory()`, `PortfolioService.getTrades()`

---

### Mobile Responsive (B1)

**Mô tả:** Tối ưu toàn bộ giao diện cho mobile (320px–768px).

**Thành phần:**

| Cải tiến | Chi tiết |
|----------|----------|
| Bottom Navigation | `BottomNavComponent` — fixed bar 5 mục (Tổng quan, GD, Kế hoạch, Rủi ro, Thêm), hiện trên `< md` |
| Mobile card layout | 14 bảng dữ liệu chuyển sang card layout trên mobile (`hidden md:block` table + `md:hidden` cards) |
| Grid stacking | `grid-cols-2` → `grid-cols-1 sm:grid-cols-2` trên ~15 component |
| Tab overflow | Tab navigation cuộn ngang với `scrollbar-hide` trên mobile |
| Header stacking | Page header xếp dọc trên mobile (`flex-col sm:flex-row`) |
| Tooltip responsive | `max-width: calc(100vw - 2rem)` cho tooltip không bị tràn |
| Content padding | `pb-14 md:pb-0` trên `<main>` tránh nội dung bị bottom nav che |

**Components mới:** `shared/components/bottom-nav/bottom-nav.component.ts`

**Files đã sửa:** ~20 component files + `styles.css` + `app.component.ts`

---

## API Endpoints tổng hợp (Frontend → Backend)

| Module | Endpoint | Auth |
|--------|----------|:----:|
| Auth | `POST /api/v1/auth/google` | — |
| Portfolios | `GET/POST/PUT/DELETE /api/v1/portfolios` | ✅ |
| Trades | `GET/POST /api/v1/portfolios/{id}/trades` | ✅ |
| P&L | `GET /api/v1/pnl/summary` | ✅ |
| Analytics | `GET /api/v1/analytics/portfolio/{id}/performance` | ✅ |
| Advanced Analytics | `GET /api/v1/analytics/portfolio/{id}/equity-curve` | ✅ |
| Risk | `GET/POST /api/v1/risk/portfolio/{id}/profile` | ✅ |
| Risk | `GET /api/v1/risk/portfolio/{id}/summary` | ✅ |
| Market Data | `GET /api/v1/market/price/{symbol}` | ✅ |
| Market Data | `GET /api/v1/market/prices?symbols=...` | ✅ |
| Market Data | `GET /api/v1/market/index/{symbol}` | ✅ |
| Strategies | `GET/POST/PUT/DELETE /api/v1/strategies` | ✅ |
| Journals | `GET/POST /api/v1/journals` | ✅ |
| Snapshots | `GET/POST /api/v1/snapshots` | ✅ |
| Templates (system) | `GET /api/v1/templates/strategies` | — |
| Templates (system) | `GET /api/v1/templates/risk-profiles` | — |
| **Templates (user)** | `GET/POST/DELETE /api/v1/templates/trade-plans` | ✅ |
| **Trade Plans** | `GET/POST/PUT/DELETE /api/v1/trade-plans` | ✅ |
| **Trade Plans** | `PATCH /api/v1/trade-plans/{id}/lots/{lotNumber}/execute` | ✅ |
| **Trade Plans** | `PATCH /api/v1/trade-plans/{id}/stop-loss` | ✅ |
| **Trade Plans** | `PATCH /api/v1/trade-plans/{id}/exit-targets/{level}/trigger` | ✅ |
| **Positions** | `GET /api/v1/positions` | ✅ |

---

## Frontend Routes

| Path | Component | Mô tả |
|------|-----------|-------|
| `/dashboard` | `DashboardComponent` | Investor Cockpit |
| `/trade-wizard` | `TradeWizardComponent` | Wizard 5 bước |
| `/trade-plan` | `TradePlanComponent` | Lập kế hoạch + Template |
| `/market-data` | `MarketDataComponent` | Dữ liệu thị trường |
| `/analytics` | `AnalyticsComponent` | Phân tích danh mục |
| `/risk` | `RiskComponent` | Thiết lập Risk Profile |
| `/risk-dashboard` | `RiskDashboardComponent` | Risk monitoring |
| `/monthly-review` | `MonthlyReviewComponent` | Báo cáo tháng tự động |
| `/strategies` | `StrategiesComponent` | Thư viện chiến lược |
| `/journals` | `JournalsComponent` | Nhật ký giao dịch |
| `/snapshots` | `SnapshotsComponent` | Lịch sử snapshot |
| `/capital-flows` | `CapitalFlowsComponent` | Dòng vốn |
| `/portfolios` | `PortfoliosRoutes` | Quản lý danh mục |
| `/positions` | `PositionsComponent` | Vị thế đang mở |
| `/trades` | `TradesComponent` | Lịch sử giao dịch |
| `/trades/create` | `TradeCreateComponent` | Tạo giao dịch mới |
| `/trade-replay/:id` | `TradeReplayComponent` | Replay kế hoạch giao dịch trên biểu đồ giá |

---

## Backlog (chưa implement)

| # | Tính năng | Độ ưu tiên |
|---|-----------|:---:|
| ~~B1~~ | ~~Mobile responsive~~ | ✅ Done v2.11.0 |
| B2 | Equity Curve vs Target CAGR overlay | Trung bình |
| B3 | Export PDF/Excel | Trung bình |
| B4 | Keyboard shortcuts | Thấp |
| B5 | Dark mode | Thấp |
| B6 | ~~Multi-timeframe Dashboard~~ | ✅ Done v2.2.0 |
