# Investment Mate v2 — Tài liệu Tính năng

> **Cập nhật lần cuối:** 2026-04-10
> **Trạng thái:** Phase 7 đang tiếp tục + P0.7 (Campaign Review) + P1-P4 (Post-Trade Review, Stress Test, Bollinger/ATR, Risk Budget) + Symbol Timeline (P7)
> **Xem thêm:** [AI Integration — Tài liệu kỹ thuật chi tiết](ai-integration.md)

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

> **Tài liệu chi tiết:** [Trade Plans — Tài liệu chi tiết](trade-plans.md)

Module lập kế hoạch giao dịch đầy đủ: Auto-fill giá, Position Sizing, Checklist 13 mục, Multi-lot (ScalingIn/DCA), Exit Targets, **Scenario Playbook** (kịch bản nâng cao với decision tree + tự động đánh giá), Template save/load, Risk enforcement, Order Sheet.

**Advanced Position Sizing (5 mô hình):**
- **Cố định % rủi ro** (có sẵn): `Size = (Vốn × %Risk) / RiskPerShare`
- **Theo ATR**: `Size = (Vốn × %Risk) / (N × ATR)` — tự điều chỉnh theo biến động
- **Kelly Criterion**: Half-Kelly, cap 25% — sizing tối ưu dựa trên win rate và avg win/loss
- **Turtle (1 unit)**: `1 Unit = 1% Vốn / ATR` — thêm tối đa 3 unit nếu lời
- **Điều chỉnh biến động**: Scale theo ATR percentile (ATR thấp → tăng size, ATR cao → giảm size)
- Bảng so sánh tất cả mô hình: số CP, % danh mục, rủi ro. Click chọn để áp dụng.
- API: `POST /api/v1/risk/position-sizing`

**Advanced Stop Loss (5 phương pháp):**
- **Cố định (nhập tay)**: Nhập SL trực tiếp
- **ATR Stop Loss**: `Entry ∓ k × ATR(14)`, k = 1.5 (ngắn hạn) / 2.0 (trung hạn) / 3.0 (dài hạn)
- **Chandelier Exit**: `HighestHigh(22) - 3×ATR` (mua) hoặc `LowestLow(22) + 3×ATR` (bán)
- **MA Trailing**: EMA(21) làm SL floor
- **Hỗ trợ/Kháng cự gần nhất**: Swing low (mua) hoặc Swing high (bán)
- Pill selector dưới ô SL, hỗ trợ cả Buy/Sell direction
- ATR multiplier selector: 1.5×/2×/3× với gợi ý ngắn/trung/dài hạn

**Strategy Templates (16 mẫu, 7 có P5 data):**
- 7 chiến lược kỹ thuật có đầy đủ: R:R, SL%, SL method, ATR multiplier, sizing model
- Scalping (SL 1.5%, R:R 1.5), Day Trading (ATR×1.5, R:R 2), Swing Trading (Hỗ trợ, R:R 2), Position Trading (Chandelier, R:R 3, Turtle), Breakout (Hỗ trợ, R:R 2), Mean Reversion (ATR×1.5, Volatility), Momentum (MA Trailing, R:R 2)
- Chọn template → tự động set SL method, R:R, SL% trong Trade Plan
- Template detail hiển thị badges: R:R, SL%, SL method, sizing model

**Dynamic Checklist (P6):**
- Checklist thay đổi theo `timeFrame` của chiến lược (Scalping/DayTrading/Swing/Position)
- Mỗi timeFrame có checklist items khác nhau cho phân tích kỹ thuật
- **Multi-Timeframe Gate**: Bắt buộc xác nhận xu hướng khung lớn (Daily/Weekly/Monthly) tùy strategy
- **Weighted scoring**: item weight 1-3 (●3 bắt buộc, ●2 quan trọng, ●1 tham khảo)
- GO/NO-GO: cần tất cả ●3 items + tổng điểm ≥ 70%
- Progress bar + chi tiết thiếu khi chưa đủ điều kiện

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
- **Phân tích kỹ thuật (Smart Signals)**: Auto phân tích 10 chỉ báo:
  - **Xu hướng:** EMA(20/50/200), ADX(14) + DI (trending/sideway/strong_trend)
  - **Động lượng:** RSI(14), MACD(12,26,9), Stochastic(14,3,3)
  - **Khối lượng:** Volume ratio, OBV (dòng tiền vào/ra), MFI(14) (quá mua/quá bán có volume)
  - **Biến động:** Bollinger Bands(20,2) (squeeze/breakout), ATR(14)
  - **Hỗ trợ/Kháng cự:** Swing high/low, Fibonacci Retracement/Extension
  - Tín hiệu tổng hợp 10 chỉ báo: Mua mạnh/Mua/Chờ/Bán/Bán mạnh. Gợi ý giao dịch (entry, SL, TP, R:R). Link "Tạo Trade Plan từ gợi ý". Tín hiệu cũng hiển thị trên Watchlist.
- **Điểm Confluence (0-100)**: Tổng hợp trọng số 5 nhóm: Xu hướng 30%, Động lượng 25%, Khối lượng 20%, Biến động 15%, Vị trí giá 10%. Hiển thị progress bar + đánh giá tích cực/tiêu cực/trung tính.
- **Trạng thái thị trường**: Phân loại tự động dựa trên ADX — Xu hướng rất mạnh (≥40) / Có xu hướng (≥25) / Đi ngang (<25). Gợi ý chiến lược: Trend Following hoặc Mean Reversion.
- **Phát hiện phân kỳ (Divergence)**: Auto-detect phân kỳ RSI và MACD so với giá. Phân kỳ tăng (bullish) = giá thấp hơn nhưng RSI/MACD cao hơn. Phân kỳ giảm (bearish) = ngược lại. Hiển thị alert card với chi tiết RSI/MACD.

**Cách đọc 10 chỉ báo:**

| Card | Giá trị | Ý nghĩa |
|------|---------|---------|
| **EMA (20/50)** | Xu hướng TĂNG / GIẢM | EMA20 > EMA50 = uptrend, ngược lại = downtrend |
| **RSI (14)** | 0–100 | < 30 = quá bán (cơ hội mua), > 70 = quá mua (cẩn trọng) |
| **MACD (12,26,9)** | Tín hiệu MUA / BÁN | MACD cắt lên Signal = mua, cắt xuống = bán |
| **Khối lượng** | Đột biến / Cao / Thấp | Volume spike xác nhận tín hiệu, thấp = thiếu dòng tiền |
| **Bollinger (20,2)** | Nén / Phá lên / Phá xuống | Squeeze = sắp biến động mạnh, phá dải = xu hướng mạnh |
| **ATR (14)** | Biến động cao / TB / thấp | Dùng tính SL: SL = Entry ± k × ATR (k=1.5–3) |
| **Stochastic (14,3,3)** | %K / %D, Quá mua / Quá bán | < 20 = quá bán, > 80 = quá mua. Nhạy hơn RSI, timing vào lệnh |
| **ADX (14)** | Trending / Đi ngang / Rất mạnh | > 25 = có xu hướng rõ, < 20 = sideway. +DI > -DI = hướng tăng |
| **OBV** | Dòng tiền vào / ra | OBV tăng = smart money mua, OBV giảm = smart money bán |
| **MFI (14)** | 0–100, Quá mua / Quá bán | Giống RSI nhưng tính cả volume → phản ánh dòng tiền thực tế hơn |

**Flow đọc kết hợp (khuyến nghị):**

```
Bước 1: ADX → Thị trường có xu hướng hay đi ngang?
  └─ ADX > 25 → Có xu hướng → dùng chiến lược Trend Following
  └─ ADX < 20 → Đi ngang → dùng chiến lược Mean Reversion

Bước 2: EMA + ADX DI → Hướng đi?
  └─ EMA20 > EMA50 + (+DI > -DI) → Xu hướng TĂNG → tìm MUA
  └─ EMA20 < EMA50 + (-DI > +DI) → Xu hướng GIẢM → tìm BÁN

Bước 3: RSI / Stochastic → Timing vào lệnh?
  └─ RSI pullback về 40–50 rồi bật lên (uptrend) → MUA
  └─ Stochastic < 20 + %K cắt lên %D → MUA

Bước 4: OBV / MFI → Dòng tiền có ủng hộ?
  └─ OBV rising + MFI chưa quá mua → Xác nhận ✅
  └─ OBV falling hoặc MFI > 80 → Cẩn trọng ⚠️

Bước 5: ATR → Đặt SL ở đâu?
  └─ SL = Entry - 2 × ATR (swing trading)
  └─ Bollinger Squeeze → chuẩn bị breakout, chờ hướng rồi vào
```

**Ví dụ thực tế:** Tra mã FPT, kết quả:
- ADX = 35 (trending) + +DI > -DI → **hướng tăng, xu hướng rõ**
- EMA20 > EMA50 → **xác nhận uptrend**
- RSI = 45 → **chưa quá mua, còn room tăng**
- OBV rising + MFI = 55 → **dòng tiền vào, chưa quá nóng** ✅
- ATR = 1,200đ → SL = Entry - 2 × 1,200 = Entry - 2,400đ
- → Tín hiệu tổng hợp: **"Mua"** → Click "Tạo Trade Plan từ gợi ý" → auto-fill Entry/SL/TP

**Tham chiếu công thức & chiến lược:** [`docs/references/`](references/README.md) — 3 tài liệu kiến thức nền tảng

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
| `GET` | `/market/stock/{symbol}/analysis` | Phân tích kỹ thuật (EMA, RSI, MACD, Stochastic, ADX, OBV, MFI, Bollinger, ATR, Volume, S&R, Fibonacci, Signal, Confluence Score, Market Condition, Divergence) |

**Services:** `MarketDataService` (FE), `HmoneyMarketDataProvider` (BE — implements `IMarketDataProvider` + `IStockInfoProvider`), `TechnicalIndicatorService` (BE — implements `ITechnicalIndicatorService`)

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

**Form Editability Matrix (v2.39, strict state-based locking):**
- Draft/Ready: chỉnh sửa tự do
- InProgress: chỉ được **tighten SL** (Long: newSl ≥ currentSl; Short: newSl ≤ currentSl) + sửa lot chưa khớp + cập nhật ghi chú/context
- Executed/Reviewed/Cancelled: read-only (Cancelled khoá cả ghi chú)
- State banner đầu form + readonly affordance (`bg-gray-50 cursor-not-allowed`) + save buttons theo state
- Template panel "Tải/Lưu template" ẩn khi editing non-Draft plan (tránh overwrite field khoá)
- Chi tiết matrix: [`docs/plans/done/p2-trade-plan-editability.md`](plans/done/p2-trade-plan-editability.md)

---

### Trade Create Improvements (`/trades/create`)

**File:** `trade-create.component.ts`

- **Lô chẵn**: lệnh MUA bắt buộc bội số 100
- **Kiểm tra số dư**: giá trị lệnh không vượt tiền còn lại danh mục
- **Dropdown danh mục**: hiện thêm tổng vốn bên cạnh tên
- **Position info**: hiện thông tin vị thế khi bán (đang nắm giữ, giá TB, P&L)
- **Fee auto-calculation**: tự tính phí + thuế từ FeeService
- **Auto-suggest 2 chiều Portfolio ↔ Symbol**: chọn danh mục → hiện chips cổ phiếu có vị thế, chọn symbol → auto-select danh mục chứa vị thế. BÁN: chỉ hiện CP có quantity > 0, disable nút bán + alert banner nếu symbol không khớp danh mục

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

## Trader's Daily Todo List & Routine Templates

### Tổng quan

Widget ngay trên Dashboard (dưới Risk Alert Banner) + trang riêng `/daily-routine`.

**5 Templates sẵn có:**

| Template | Emoji | Thời gian | Bước | Mô tả |
|----------|:-----:|:---------:|:----:|-------|
| Swing Trading | 🌅 | ~30 phút | 12 | Sáng review → Trong phiên thực thi → Cuối ngày nhật ký |
| DCA | 📈 | ~15 phút | 8 | Ngày mua DCA: kiểm tra giá → đặt lệnh → ghi nhận |
| Research | 🔍 | ~45 phút | 10 | Cuối tuần: review hiệu suất → tìm mã mới → kế hoạch |
| Onboarding | 🚀 | ~20 phút | 8 | Lần đầu: tạo danh mục → Risk Profile → chiến lược |
| Crisis | ⚠️ | ~15 phút | 8 | Thị trường giảm mạnh: SL → drawdown → cắt lỗ → tâm lý |

### Điểm đặc biệt

- **Deep links**: Mỗi item có link đến trang tương ứng (click → navigate thẳng)
- **Auto-suggest**: Template gợi ý dựa trên ngữ cảnh:
  1. First-time user → Onboarding
  2. VN-Index ≤ -3% → Crisis
  3. Weekend (Sat/Sun) → Research
  4. DCA day (Monday default) → DCA
  5. Default → Swing Trading
- **Streak counter**: Gamification đếm ngày liên tiếp hoàn thành (🔥 3, 5, 10, 30 ngày)
- **Custom templates**: User tạo mẫu riêng, CRUD đầy đủ
- **3 nhóm thời gian**: Sáng / Trong phiên / Cuối ngày
- **History heatmap**: 30 ngày gần nhất (xanh=hoàn thành, vàng=một phần, xám=chưa làm)

### Backend

- **Domain:** `DailyRoutine` (AggregateRoot), `RoutineTemplate`, `RoutineItem`, `RoutineItemTemplate` (ValueObjects)
- **Collection:** `daily_routines` (compound index UserId+Date, non-unique — soft-deleted docs are hard-deleted before insert), `routine_templates`
- **CQRS:** GetOrCreateTodayRoutine, CompleteRoutineItem, SwitchTemplate, CreateCustomTemplate, UpdateCustomTemplate, DeleteCustomTemplate
- **Queries:** GetTodayRoutine, GetRoutineHistory, GetRoutineTemplates, GetSuggestedTemplate
- **Seed:** 5 built-in templates trong `routine_templates.json`

### Frontend

- **Service:** `daily-routine.service.ts`
- **Full page:** `features/daily-routine/daily-routine.component.ts`
- **Dashboard widget:** Compact card trong `dashboard.component.ts` (progress bar + next items + streak badge)
- **Navigation:** Header (Quản lý group) + Bottom nav (moreItems)

---

## Watchlist Thông minh

> **Branch:** `feature/watchlist` | **Trạng thái:** ✅ Done

Theo dõi cổ phiếu quan tâm trước khi tạo Trade Plan — cầu nối Market Data → Trade Plan.

### Tính năng chính

- **CRUD watchlist**: Tạo/sửa/xoá nhiều danh sách (VD: "Cổ phiếu theo dõi", "Chờ mua", "VN30")
- **Thêm/xoá mã**: Tìm kiếm symbol autocomplete (24hmoney API), thêm nhanh vào danh sách
- **Giá realtime**: Batch price lookup hiển thị giá, % thay đổi, khối lượng cho mỗi mã
- **Import VN30**: Nhập 30 mã VN30 bằng 1 click (tạo watchlist mới hoặc thêm vào watchlist hiện tại)
- **Ghi chú & giá mục tiêu**: Note + target buy/sell price cho từng mã
- **Deep link đến Trade Plan**: Nút [Tạo Plan] → `/trade-plan?symbol=X` pre-filled
- **Dashboard widget**: Top 5 mã từ watchlist đầu tiên hiển thị trên Dashboard

### Backend

- **Entities:** `Watchlist` (AggregateRoot), `WatchlistItem` (ValueObject embedded)
- **Collection:** `watchlists` (compound index UserId)
- **API:** `WatchlistsController` (`api/v1/watchlists`) — 9 endpoints (CRUD + items + import-vn30)
- **CQRS:** 7 commands (Create/Update/Delete watchlist, Add/Update/Remove item, ImportVn30) + 2 queries

### Frontend

- **Service:** `watchlist.service.ts`
- **Full page:** `features/watchlist/watchlist.component.ts`
- **Dashboard widget:** Top movers grid trong `dashboard.component.ts`
- **Navigation:** Header (Phân tích group) + Bottom nav (moreItems)

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
| **Risk** | `GET /api/v1/risk/portfolio/{id}/optimization` | ✅ |
| **Risk** | `GET /api/v1/risk/portfolio/{id}/trailing-stop-alerts` | ✅ |
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
| **Trade Plans** | `POST /api/v1/trade-plans/{id}/review` | ✅ |
| **Trade Plans** | `GET /api/v1/trade-plans/{id}/review/preview` | ✅ |
| **Trade Plans** | `PATCH /api/v1/trade-plans/{id}/review/lessons` | ✅ |
| **Trade Plans** | `GET /api/v1/trade-plans/pending-review` | ✅ |
| **Trade Plans** | `GET /api/v1/trade-plans/campaign-analytics` | ✅ |
| **Trade Plans** | `PATCH /api/v1/trade-plans/{id}/lots/{lotNumber}/execute` | ✅ |
| **Trade Plans** | `PATCH /api/v1/trade-plans/{id}/stop-loss` | ✅ |
| **Trade Plans** | `PATCH /api/v1/trade-plans/{id}/exit-targets/{level}/trigger` | ✅ |
| **Trade Plans** | `PATCH /api/v1/trade-plans/{id}/scenario-nodes/{nodeId}/trigger` | ✅ |
| **Trade Plans** | `GET /api/v1/trade-plans/scenario-templates` | ✅ |
| **Positions** | `GET /api/v1/positions` | ✅ |
| **Daily Routines** | `GET/POST /api/v1/daily-routines` | ✅ |
| **Daily Routines** | `PATCH /api/v1/daily-routines/{id}/items/{index}` | ✅ |
| **Daily Routines** | `POST /api/v1/daily-routines/switch-template` | ✅ |
| **Daily Routines** | `GET /api/v1/daily-routines/history` | ✅ |
| **Daily Routines** | `GET/POST/PUT/DELETE /api/v1/daily-routines/templates` | ✅ |
| **Daily Routines** | `GET /api/v1/daily-routines/templates/suggest` | ✅ |
| **Watchlists** | `GET/POST /api/v1/watchlists` | ✅ |
| **Watchlists** | `GET/PUT/DELETE /api/v1/watchlists/{id}` | ✅ |
| **Watchlists** | `POST /api/v1/watchlists/{id}/items` | ✅ |
| **Watchlists** | `PUT/DELETE /api/v1/watchlists/{id}/items/{symbol}` | ✅ |
| **Watchlists** | `POST /api/v1/watchlists/import-vn30` | ✅ |
| **AI Settings** | `GET/PUT/DELETE /api/v1/ai-settings` | ✅ |
| **AI Settings** | `POST /api/v1/ai-settings/test` | ✅ |
| **AI** | `POST /api/v1/ai/journal-review` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/portfolio-review` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/trade-plan-advisor` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/chat` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/monthly-summary` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/stock-evaluation` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/risk-assessment` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/position-advisor` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/trade-analysis` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/watchlist-scanner` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/daily-briefing` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/comprehensive-analysis` (SSE) | ✅ |
| **AI** | `POST /api/v1/ai/build-context` (JSON) | ✅ |

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
| `/daily-routine` | `DailyRoutineComponent` | Nhiệm vụ hàng ngày & Routine Templates |
| `/watchlist` | `WatchlistComponent` | Theo dõi cổ phiếu & tìm cơ hội giao dịch |
| `/ai-settings` | `AiSettingsComponent` | Cấu hình AI đa nhà cung cấp (Claude/Gemini, API keys, model, usage) |
| `/campaign-analytics` | `CampaignAnalyticsComponent` | Phân tích chiến dịch cross-plan: summary, so sánh, best/worst, lessons (P0.7) |
| `/help` | `HelpComponent` | Hướng dẫn sử dụng: 8 chủ đề, full-text search tiếng Việt (không dấu), markdown rendering |

---

## Tích hợp AI Claude + Gemini (Multi-provider)

> **Branch:** `feature/ai-integration`, `feature/enhance-ai-prompts` | **Trạng thái:** ✅ Done

Tích hợp AI làm trợ lý thông minh trong app — hỗ trợ đa nhà cung cấp: **Claude (Anthropic)** + **Gemini (Google)**. 12 use case, streaming SSE, mỗi user tự quản API key (mã hóa, mỗi provider riêng). Hỗ trợ **Copy Prompt** để dùng với Claude/Gemini client app (không cần API key). Prompt được làm giàu với cross-referencing data: market data, technical signals, risk profile, historical trades, comprehensive stock data.

### Multi-provider Architecture

- **Provider support:** Claude (Anthropic) + Gemini (Google)
- **Provider tabs trong settings UI:** chuyển đổi giữa Claude / Gemini, lưu API key riêng cho từng provider
- **Dual API key storage:** `EncryptedClaudeApiKey` + `EncryptedGeminiApiKey` (mã hóa, mỗi provider riêng)
- **Factory pattern:** `IAiChatServiceFactory` resolve đúng service theo provider đang chọn (`ClaudeApiService` | `GeminiApiService`)
- **Gemini models:** `gemini-2.0-flash`, `gemini-2.5-flash`, `gemini-2.5-pro`

### 12 Use Cases

| # | Use Case | Trigger | Dữ liệu context |
|---|----------|---------|------------------|
| 1 | **AI Journal Review** | Nút "🤖 AI Phân tích" trên `/journals` | 20 nhật ký gần nhất + trades liên quan + thống kê (avg confidence, avg rating, emotion distribution) + portfolio context |
| 2 | **AI Portfolio Review** | Nút "🤖 AI Đánh giá" trên portfolio detail | Vị thế, P&L, risk metrics + risk profile + risk summary + active trade plans count |
| 3 | **AI Trade Plan Advisor** | Nút "🤖 AI Tư vấn" trên `/trade-plan` | Full plan (entry/SL/TP/lots/exits) + portfolio balance + **current market data** + **technical signals** (RSI/MACD/EMA/S&R) + **risk compliance** + **historical trades on symbol** |
| 4 | **AI Chat Assistant** | Nút "AI" trên header | Active positions (top 5) + watchlist summary + portfolio summary + conversation history + current date |
| 5 | **AI Monthly Summary** | Nút "🤖 AI Tổng kết" trên `/monthly-review` | Trades in month + **performance metrics** (win/loss/win rate/realized P&L) + **previous month comparison** + **per-symbol P&L** |
| 6 | **AI Stock Evaluation** | Nút "✨ AI Đánh giá" trên `/market-data` | Fundamental (P/E, EPS, ROE, D/E từ TCBS) + Technical (EMA/RSI/MACD/S&R) + Stock detail + **user position** + **watchlist target prices** + **active trade plan** |
| 7 | **AI Risk Assessment** | Nút "🤖 AI Phân tích Rủi ro" trên `/risk-dashboard` | Portfolio risk summary, drawdown, correlation, risk profile, compliance violations |
| 8 | **AI Position Advisor** | Nút "🤖 AI Tư vấn" trên `/positions` | Active positions với PnL, linked trade plans, technical signals cho top 5 |
| 9 | **AI Trade Analysis** | Nút "🤖 AI Phân tích" trên `/trades` | Trades grouped by symbol, win/loss stats, profit factor, plan adherence |
| 10 | **AI Watchlist Scanner** | Nút "🤖 AI Quét Watchlist" trên `/watchlist` | Watchlist items + current prices + technical signals (top 15) + fundamentals (top 10) |
| 11 | **AI Daily Briefing** | Nút "🤖 AI Bản tin Hôm nay" trên `/dashboard` | Overall PnL, top positions, risk alerts, pending trade plans, watchlist alerts |
| 12 | **AI Comprehensive Analysis** | Nút "🤖 AI Phân tích Toàn diện" trên `/market-data` | Chỉ số tài chính (P/E, P/B, ROE, ROA, EPS, Beta, MarketCap), BCTC, kế hoạch kinh doanh, cổ tức, cổ phiếu cùng ngành, giao dịch NN, báo cáo phân tích từ CTCK — dữ liệu toàn diện từ 24hmoney |

### Backend

- **Entity:** `AiSettings` — UserId, Provider ("claude" | "gemini"), EncryptedClaudeApiKey, EncryptedGeminiApiKey (nullable), Model, TotalInputTokens, TotalOutputTokens, EstimatedCostUsd
- **Encryption:** ASP.NET Data Protection (`AiKeyEncryptionService`)
- **Factory:** `IAiChatServiceFactory` → resolve `ClaudeApiService` hoặc `GeminiApiService` theo provider
- **Low-level Claude:** `ClaudeApiService` — gọi Anthropic Messages API (`stream: true`), parse SSE events
- **Low-level Gemini:** `GeminiApiService` — gọi Gemini streaming API, role mapping "assistant" → "model", SSE format
- **High-level:** `AiAssistantService` — 12 use cases, gather context, build Vietnamese system prompts with XML tagging, track token usage. Enriched prompts with cross-referencing data (market data, technicals, risk profile, historical trades, comprehensive stock data)
- **Context builders:** Refactored — mỗi use case có private `BuildXxxContext()` method trả về `AiContextResult` (systemPrompt + userMessage), dùng chung cho cả streaming lẫn copy-prompt
- **New dependencies:** `IRiskCalculationService`, `IRiskProfileRepository`, `IWatchlistRepository` — cho phép cross-reference data giữa các domain
- **XML tagging:** Tất cả prompt dùng XML tags (`<portfolio>`, `<positions>`, `<fundamental_metrics>`, `<technical_signals>`, `<trade_plan>`, etc.) + markdown tables cho dữ liệu có cấu trúc → AI parse chính xác hơn
- **Fundamental data:** `IFundamentalDataProvider` + `TcbsFundamentalDataProvider` — lấy P/E, P/B, EPS, ROE, ROA, D/E, revenue growth, net profit growth từ TCBS API (`apipubaws.tcbs.com.vn`)
- **Comprehensive stock data:** `IComprehensiveStockDataProvider` + `HmoneyComprehensiveDataProvider` — lấy chỉ số tài chính (P/E, P/B, ROE, ROA, EPS, Beta, MarketCap), BCTC, kế hoạch kinh doanh, cổ tức, cổ phiếu cùng ngành, giao dịch NN, báo cáo phân tích từ 24hmoney API. Models: `HmoneyComprehensiveApiModels.cs`
- **API:** `AiSettingsController` (CRUD) + `AiController` (12 SSE streaming endpoints + 1 JSON build-context endpoint)
- **Claude models:** `claude-sonnet-4-6-20250514` (mặc định), `claude-opus-4-6-20250514`
- **Gemini models:** `gemini-2.0-flash`, `gemini-2.5-flash`, `gemini-2.5-pro`

### Frontend

- **Service:** `ai.service.ts` — CRUD settings (HttpClient) + streaming (fetch + ReadableStream → Observable)
- **Reusable panel:** `AiChatPanelComponent` — sliding panel từ phải, markdown rendering (marked), follow-up questions, token usage display, model selector dropdown, **📋 Copy Prompt button**
- **Copy Prompt (clipboard):** Nút 📋 trong AI panel header → gọi `POST /ai/build-context` → format system prompt + user message → copy vào clipboard. **Không cần API key** — dùng với Claude Max / Gemini client app bên ngoài
- **Settings page:** `/ai-settings` — provider tabs (Claude / Gemini), nhập/thay đổi API key cho từng provider, chọn model, test kết nối, xem thống kê sử dụng, xóa key
- **Integration points:** journals, portfolio-detail, trade-plan, monthly-review, **market-data** (stock evaluation), header (global chat), **risk-dashboard**, **positions**, **trades**, **watchlist**, **dashboard** (daily briefing)

### Chi phí token

**Claude (Anthropic):**

| Model | Input | Output |
|-------|-------|--------|
| Sonnet 4.6 | $3/M tokens | $15/M tokens |
| Opus 4.6 | $15/M tokens | $75/M tokens |

**Gemini (Google):** Chi phí tính theo pricing của Google AI Studio, khác nhau theo model và tier.

---

## Capital Flows Visibility

> **Branch:** `feat/capital-flows-visibility` | **Trạng thái:** ✅ Done

Nâng cao vai trò của Capital Flows — từ trang riêng lẻ thành dữ liệu hiển thị xuyên suốt app.

### Dashboard Integration

- **Card "Tiền mặt khả dụng"**: Grid 5 cột, hiển thị cash balance = InitialCapital + NetCashFlow - TotalInvested
- **TWR % dưới Lãi/Lỗ**: So sánh P&L thô với hiệu suất điều chỉnh dòng vốn
- **Flow markers trên Equity Curve**: Scatter points tam giác xanh ▲ (Deposit/Dividend/Interest) và đỏ ▼ (Withdraw/Fee)
- **Smart Nudge banner**: Tự động phát hiện thay đổi giá trị >20% mà không có flow gần đây → gợi ý ghi nhận

### Analytics Integration

- **TWR vs MWR card**: So sánh song song, giải thích context (TWR > MWR = timing kém, MWR > TWR = timing tốt)
- **Flow markers trên Equity Curve**: Cùng logic với Dashboard, kích thước marker lớn hơn

### Components sửa đổi

| Component                | Thay đổi                                                          |
|--------------------------|-------------------------------------------------------------------|
| `dashboard.component.ts` | +CapitalFlowService, +cashBalance card, +TWR, +flow markers, +nudge |
| `analytics.component.ts` | +CapitalFlowService, +TWR/MWR card, +flow markers                 |

---

## Portfolio Optimizer & Risk Dashboard Improvements

> **Branch:** `feat/portfolio-optimizer-risk-dashboard` | **Trạng thái:** ✅ Done

### Portfolio Optimizer (`/risk-dashboard`)

Phân tích tối ưu hóa danh mục tích hợp trong trang Risk Dashboard:

- **Concentration Alerts**: Cảnh báo khi vị thế vượt giới hạn MaxPositionSizePercent (từ Risk Profile). Severity: warning (<1.5× limit) / danger (≥1.5× limit)
- **Sector Diversification**: Nhóm vị thế theo ngành (từ `IFundamentalDataProvider`), tính exposure %, cảnh báo khi vượt MaxSectorExposurePercent
- **Correlation Warnings**: Hiển thị cặp cổ phiếu tương quan cao (>0.5), phân loại high (>0.7) / medium (>0.5)
- **Diversification Score**: Điểm 0-100 dựa trên concentration, sector diversity, correlation, số vị thế
- **Recommendations**: Gợi ý giảm tỷ trọng, đa dạng hóa ngành, cảnh báo tương quan cao

### Risk Dashboard Improvements

- **PositionRiskItem mở rộng**: Thêm `sector`, `beta`, `positionVaR` cho từng vị thế
- **Trailing Stop Monitoring**: Giám sát trailing stop real-time, cảnh báo khi giá gần trigger (danger ≤2%, warning ≤5%), gợi ý nâng trailing stop khi giá tăng

### Backend

- **CQRS:** `GetPortfolioOptimizationQuery`, `GetTrailingStopAlertsQuery`
- **Service:** `RiskCalculationService.GetPortfolioOptimizationAsync()`, `GetTrailingStopAlertsAsync()`
- **Dependencies mới:** `IRiskProfileRepository`, `IFundamentalDataProvider` (inject vào RiskCalculationService)

**API Endpoints:**

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET` | `/api/v1/risk/portfolio/{portfolioId}/optimization` | Phân tích tối ưu hóa danh mục |
| `GET` | `/api/v1/risk/portfolio/{portfolioId}/trailing-stop-alerts` | Cảnh báo trailing stop real-time |

### Frontend

- **Service:** `RiskService` — thêm `getPortfolioOptimization()`, `getTrailingStopAlerts()`
- **UI:** Tích hợp trong `risk-dashboard.component.ts` — section Tối ưu hóa danh mục + Giám sát Trailing Stop

---

## Scenario Playbook cho Trade Plan

> **Branch:** `feat/capital-flows-visibility` | **Trạng thái:** ✅ Done

Mở rộng Trade Plan với chế độ kịch bản nâng cao — cây quyết định tự động đánh giá và kích hoạt hành động dựa trên điều kiện thị trường.

### Hai chế độ thoát lệnh (ExitStrategyMode)

| Chế độ | Mô tả |
|--------|--------|
| **Cơ bản (Simple)** | Mục tiêu thoát hiện tại (ExitTarget: TP/CL/Trailing) — không thay đổi |
| **Nâng cao (Advanced)** | Cây kịch bản (ScenarioNodes) với điều kiện + hành động tự động |

### ScenarioNode — Cây quyết định

Mỗi node gồm:
- **Condition**: Điều kiện kích hoạt — `PriceAbove`, `PriceBelow`, `PricePercentChange`, `TrailingStopHit`, `TimeElapsed`
- **Action**: Hành động thực thi — `SellPercent`, `SellAll`, `MoveStopLoss`, `MoveStopToBreakeven`, `ActivateTrailingStop`, `AddPosition`, `SendNotification`
- **Children**: Các node con (đệ quy) — tạo thành cây quyết định nhiều tầng

### TrailingStopConfig

| Thuộc tính | Mô tả |
|------------|--------|
| Method | `Percentage`, `ATR`, `FixedAmount` |
| TrailValue | Giá trị trail (%, ATR multiplier, hoặc số tiền cố định) |
| ActivationPrice | Giá kích hoạt trailing stop |
| StepSize | Bước nhảy tối thiểu khi nâng stop |

### 3 Preset Templates

| Template | Mô tả |
|----------|--------|
| **An toàn** | Ưu tiên bảo toàn vốn, SL chặt, chốt lời sớm |
| **Cân bằng** | Cân đối rủi ro/lợi nhuận, trailing stop vừa phải |
| **Tích cực** | Chấp nhận rủi ro cao hơn, trailing rộng, chốt lời muộn |

### Tự động đánh giá (Worker)

- `ScenarioEvaluationService` chạy trong Worker mỗi **15 phút**
- Duyệt tất cả TradePlan có `ExitStrategyMode = Advanced` và `Status = InProgress`
- So sánh giá hiện tại với điều kiện từng ScenarioNode
- Khi điều kiện khớp → kích hoạt hành động + tạo `AlertHistory` entry
- Phát domain event `ScenarioNodeTriggeredEvent`

### Backend

- **Domain:** `TradePlan` entity — thêm `ExitStrategyMode`, `ScenarioNodes` properties + methods `SetExitStrategyMode()`, `SetScenarioNodes()`, `TriggerScenarioNode()`
- **Domain Events:** `ScenarioNodeTriggeredEvent`
- **Value Objects:** `ScenarioNode` (Condition, Action, Children), `TrailingStopConfig` (Method, TrailValue, ActivationPrice, StepSize)
- **CQRS:** `TriggerScenarioNodeCommand`, `GetScenarioTemplatesQuery`, `GetScenarioHistoryQuery`, `SaveScenarioTemplateCommand`, `DeleteScenarioTemplateCommand`
- **Service:** `IScenarioEvaluationService` + `ScenarioEvaluationService` — đánh giá cây kịch bản (dùng ATR(14) thực tế cho trailing stop)
- **Entity:** `ScenarioTemplate` — user-scoped custom scenario templates

**API Endpoints:**

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `PATCH` | `/api/v1/trade-plans/{id}/scenario-nodes/{nodeId}/trigger` | Kích hoạt scenario node thủ công |
| `GET` | `/api/v1/trade-plans/scenario-templates` | Lấy danh sách templates (preset + user custom) |
| `POST` | `/api/v1/trade-plans/scenario-templates` | Lưu mẫu kịch bản tùy chỉnh |
| `DELETE` | `/api/v1/trade-plans/scenario-templates/{id}` | Xoá mẫu kịch bản |
| `GET` | `/api/v1/trade-plans/{id}/scenario-history` | Lịch sử kích hoạt scenario nodes |

### Frontend

- **Toggle mode:** Chuyển đổi Cơ bản / Nâng cao trong form Trade Plan
- **Tree editor:** Giao diện soạn cây kịch bản (thêm/xoá node, chọn condition/action)
- **Preset templates:** Chọn template An toàn / Cân bằng / Tích cực → điền sẵn cây kịch bản
- **Custom templates:** Lưu/tải mẫu kịch bản tùy chỉnh (Mẫu hệ thống | Mẫu của tôi)
- **Scenario history panel:** Hiển thị trạng thái + lịch sử kích hoạt từng node (Đã kích hoạt / Chờ / Bỏ qua)

### Tests

| Test file | Số test |
|-----------|:-------:|
| `TradePlanScenarioTests.cs` (Domain) | 20 |
| `TriggerScenarioNodeCommandHandlerTests.cs` (Application) | 3 |
| `GetScenarioHistoryQueryHandlerTests.cs` (Application) | 3 |
| `GetScenarioTemplatesQueryHandlerTests.cs` (Application) | 3 |
| `SaveScenarioTemplateCommandHandlerTests.cs` (Application) | 2 |
| `DeleteScenarioTemplateCommandHandlerTests.cs` (Application) | 3 |
| `ScenarioEvaluationServiceTests.cs` (Infrastructure) | 13 |
| `TechnicalIndicatorServiceFibonacciEma200Tests.cs` (Infrastructure) | 6 |
| `ScenarioConsultantServiceTests.cs` (Infrastructure) | 6 |
| `ScenarioAdvisoryServiceTests.cs` (Infrastructure) | 7 |

---

## P7: Symbol Timeline — Nhật ký trên Biểu đồ Giá

**Branch:** `feat/p7-symbol-timeline`

### Phase 7A: Standalone Journal Entry + Candlestick Chart

**Backend:**
- `JournalEntry` entity — nhật ký standalone gắn symbol, không cần Trade
  - 5 loại: Observation / PreTrade / DuringTrade / PostTrade / Review
  - Cảm xúc (7 trạng thái) + ConfidenceLevel (1-10) + snapshot giá + VN-Index
  - Optional link: TradeId, TradePlanId, PortfolioId
- `IJournalEntryRepository` + MongoDB implementation (collection: `journal_entries`)
- CQRS: CreateJournalEntry, UpdateJournalEntry, DeleteJournalEntry
- Query: GetJournalEntriesBySymbol, GetSymbolTimeline (unified timeline)
- API: `POST/PUT/DELETE /api/v1/journal-entries`, `GET /api/v1/symbols/{symbol}/timeline`

**Frontend:**
- `JournalEntryService` — HTTP service cho JournalEntry + Timeline
- `SymbolTimelineComponent` (`/symbol-timeline/:symbol`) — trang chính:
  - Biểu đồ nến (lightweight-charts v4) với OHLCV data
  - Journal markers (📓), Trade markers (▲/▼), Alert markers (⚠️) trên chart
  - Holding period zones tính từ BUY/SELL trades
  - Quick-add form: nhật ký inline với auto-fill giá
  - Date range selector: 1T / 3T / 6T / 1N
  - Timeline detail list phía dưới chart

### Phase 7B: Event/News Overlay

**Backend:**
- `MarketEvent` entity — sự kiện thị trường (7 loại: Earnings, Dividend, RightsIssue, ShareholderMtg, InsiderTrade, News, Macro)
- `IMarketEventRepository` + MongoDB (collection: `market_events`)
- API: `POST /api/v1/market-events`, `GET /api/v1/market-events?symbol=...`
- Events được merge vào Unified Timeline API response

**Frontend:**
- `MarketEventService` — HTTP service cho sự kiện
- Event markers trên chart (📊/💰/📰/🏦) với icon theo loại
- Quick-add event form
- Filter checkboxes: toggle Nhật ký / Giao dịch / Sự kiện / Cảnh báo

### Phase 7C: Emotion Analytics & AI Review

**Emotion Ribbon (7C.1):**
- Histogram sub-chart bên dưới candlestick
- Color mapping: Tự tin=🟢, Bình tĩnh=🔵, Hào hứng=🟡, Lo lắng=🟠, Sợ hãi=🔴, Tham lam=🟣, FOMO=⚫
- Height = confidence level

**Emotion Summary Panel (7C.2):**
- Tổng nhật ký, Tự tin TB, Cảm xúc chính
- Distribution bars theo cảm xúc

**AI Timeline Review (7C.3):**
- AI chat panel use case `timeline-review`
- Context: journals + trades + emotion distribution + holding periods
- AI phân tích pattern cảm xúc → giao dịch → kết quả

**Entry points:** Watchlist 📊, Positions 📊, Trades 📊 → navigate đến Symbol Timeline

### P7 Improvements (feat/p7-improvements)

**P7.1: Emotion ↔ P&L Correlation:**
- Tính correlation giữa cảm xúc tại thời điểm giao dịch và kết quả P&L
- EmotionCorrelationDto: emotion, tradeCount, averagePnlPercent, winRate, totalPnl
- Bảng correlation với color-coded win rate + insight text

**P7.2: Confidence Calibration:**
- So sánh mức tự tin với win rate thực tế (Low/Medium/High/Very High)
- ConfidenceCalibrationDto: range, winRate, isCalibrated
- Widget thanh ngang hiển thị calibration state (Phù hợp/Quá tự tin/Chưa tự tin)

**P7.3: Behavioral Pattern Detection:**
- IBehavioralAnalysisService detect 4 patterns: FOMO, PanicSell, RevengeTrading, Overtrading
- Pattern alerts panel với severity (Critical/Warning) + mô tả + ngày xảy ra
- Tích hợp vào SymbolTimelineDto.behavioralPatterns

**P7.4: Chart UX Enhancements:**
- Chuyển từ CandlestickSeries sang LineSeries (match thực tế hiển thị)

**P7.5: Dedicated AI Timeline Review:**
- AI context phong phú: correlation + calibration + behavioral patterns + full journal/trade history
- Prompt template chuyên biệt cho trading psychology coach

**P7.6: Emotion Trend Over Time:**
- EmotionTrendDto: group theo tháng, dominant emotion, average confidence
- Stacked bar chart theo tháng + trend insight text

**P7.7: Export Timeline:**
- Xuất CSV (Ngày, Loại, Tiêu đề, Cảm xúc, Confidence, Giá, Ghi chú)
- Sao chép tóm tắt vào clipboard

**P7.8: Vietstock Event Crawl:**
- IVietstockEventProvider: GetNews + GetEvents từ Vietstock API
- CSRF token flow, /Date(ms)/ parser, ChannelID → MarketEventType mapping
- CrawlVietstockEventsCommand: on-demand crawl với dedup (Symbol + Title + Date)
- Nút "Cập nhật tin tức" trên Symbol Timeline page
- API: POST /api/v1/market-events/crawl

---

## P1: Post-Trade Review Workflow

**Branch:** `feat/p1-post-trade-review`

**Backend:**

- `GetTradesPendingReviewQuery` — lấy SELL trades chưa có JournalEntry PostTrade
- Endpoint: `GET /api/v1/journal-entries/pending-review?portfolioId={id}`

**Frontend:**

- Dashboard widget "Chờ đánh giá" — hiện SELL trades chưa review, click → Symbol Timeline
- Trades list cột "Nhật ký" — icon check/pencil cho mỗi SELL trade

---

## P2: Stress Test — Dynamic Beta

**Branch:** `feat/p1-post-trade-review`

**Backend:**

- `CalculateStressTestAsync` trong `IRiskCalculationService` — dynamic beta từ API, fallback tính từ price correlation, fallback cuối 1.0
- Endpoint: `POST /api/v1/risk/portfolio/{id}/stress-test`

**Frontend:**

- Thay thế `estimatedBetas` hardcoded bằng API call `riskService.stressTest()`

---

## P3: Technical Indicators — Bollinger Bands + ATR

**Branch:** `feat/p1-post-trade-review`

**Backend:**

- Bollinger Bands(20, 2): upper, middle (SMA20), lower, bandwidth, %B, signal (squeeze/breakout)
- ATR(14): giá trị, ATR% (% giá hiện tại)
- Signal scoring: 6 indicators (EMA, RSI, MACD, Volume, Bollinger, ATR)

**Frontend:**

- 2 indicator cards mới trong market-data component

---

## P4: Risk Budgeting — Daily Trade Limits

**Branch:** `feat/p1-post-trade-review`

**Backend:**

- RiskProfile mở rộng: `MaxDailyTrades`, `DailyLossLimitPercent`
- `CheckRiskBudgetAsync` — đếm trades hôm nay, tính daily P&L
- Endpoint: `GET /api/v1/risk/portfolio/{id}/budget`
- `ITradeRepository.GetByPortfolioIdAndDateRangeAsync` — filter trades theo ngày

**Frontend:**

- Risk budget card "Ngân sách rủi ro hôm nay" — trades/limit, P&L, trạng thái khóa
- Risk profile form: 2 fields mới (số lệnh/ngày, giới hạn lỗ/ngày)

---

## P0.7: Campaign Review — Đóng chiến dịch & Phân tích hiệu suất

**Branch:** `feat/p7-improvements` | **Trạng thái:** ✅ Done

Cho phép đóng (review) TradePlan đã Executed với auto-calculated P&L metrics, xem trước kết quả, cập nhật bài học, và phân tích cross-plan theo tầm nhìn đầu tư.

### Domain

- **TimeHorizon enum:** `ShortTerm` (< 3 tháng) / `MediumTerm` (3-12 tháng) / `LongTerm` (> 1 năm) — gán cho TradePlan
- **CampaignReviewData value object:** Embedded trong TradePlan khi review — chứa P&L amount, P&L %, VND/ngày, annualized return, target achievement %, lessons
- **MarkReviewed(CampaignReviewData):** Thay thế MarkReviewed() cũ — bắt buộc truyền review data
- **SetTimeHorizon():** Gán tầm nhìn đầu tư cho plan
- **UpdateReviewLessons():** Cập nhật bài học sau review
- **PlanReviewedEvent:** Domain event mới khi plan được review

### Backend

- **Service:** `ICampaignReviewService` + `CampaignReviewService` — auto-calculate P&L metrics từ trades thực tế
- **Repositories mới:**
  - `ITradePlanRepository`: `GetExecutedByUserIdAsync`, `GetReviewedByUserIdAsync`, `GetReviewedByUserIdAndTimeHorizonAsync`
  - `ITradeRepository`: `GetByTradePlanIdAsync`
- **Commands:** `ReviewTradePlanCommand` (đóng chiến dịch), `UpdateReviewLessonsCommand` (sửa bài học)
- **Queries:** `PreviewPlanReviewQuery`, `GetExecutedPlansForReviewQuery`, `GetCampaignAnalyticsQuery`

**API Endpoints:**

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `POST` | `/api/v1/trade-plans/{id}/review` | Đóng chiến dịch với auto-metrics |
| `GET` | `/api/v1/trade-plans/{id}/review/preview` | Xem trước metrics trước khi đóng |
| `PATCH` | `/api/v1/trade-plans/{id}/review/lessons` | Cập nhật bài học rút ra |
| `GET` | `/api/v1/trade-plans/pending-review` | Danh sách plans chờ review (Executed) |
| `GET` | `/api/v1/trade-plans/campaign-analytics?timeHorizon=ShortTerm` | Phân tích cross-plan theo tầm nhìn |

### Frontend

- **trade-plan.service.ts:** Thêm interfaces (CampaignReviewData, CampaignAnalytics, TimeHorizon) + methods cho 5 endpoints
- **trade-plan.component.ts:**
  - Dropdown TimeHorizon trên form
  - Review panel cho plans Executed — preview + confirm
  - Hiển thị review data (P&L, lessons) cho plans Reviewed
- **campaign-analytics.component.ts:** Trang `/campaign-analytics` mới:
  - Summary cards (tổng plans, win rate, avg P&L, avg holding days)
  - Comparison table (từng plan đã review)
  - Best/Worst plan highlight
  - Lessons feed (bài học từ tất cả campaigns)
- **app.routes.ts:** Route `/campaign-analytics` → `CampaignAnalyticsComponent`

### Tests

| Test file | Số test |
|-----------|:-------:|
| `TradePlanReviewTests.cs` (Domain) | 24 |
| `CampaignReviewServiceTests.cs` (Infrastructure) | 9 |

Tổng: 796 tests pass (Domain: 603, Application: 65, Infrastructure: 127)

---

## Backlog (chưa implement)

| # | Tính năng | Độ ưu tiên | Kế hoạch |
|---|-----------|:---:|----------|
| ~~B1~~ | ~~Mobile responsive~~ | ✅ Done v2.11.0 | |
| B2 | Equity Curve vs Target CAGR overlay | Trung bình | |
| B3 | Export PDF/Excel | Trung bình | |
| B4 | Keyboard shortcuts | Thấp | |
| B5 | Dark mode | Thấp | |
| ~~B6~~ | ~~Multi-timeframe Dashboard~~ | ✅ Done v2.2.0 | |
| **B7** | **Tài chính cá nhân** | **Cao** | [`docs/plans/personal-finance.md`](plans/personal-finance.md) — Net Worth overview, Financial Rules compliance, Health Score, Dashboard widget + `/personal-finance` |
