# Changelog — Investment Mate v2

---

## [v2.6.0] — 2026-03-14 · Phase 7 (tiếp): Trade UX, Positions, Multi-lot Plan

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **Trang Vị thế đang mở** (`/positions`): hiển thị open positions gom nhóm theo danh mục, mỗi nhóm có tổng giá trị & P&L, expand giao dịch gần nhất cho từng mã
- **Trade Plan multi-lot**: hỗ trợ nhập lệnh chia lô (ScalingIn/DCA), exit targets (TP1/TP2/CutLoss), theo dõi stop-loss history, phiếu lệnh (order sheet) copy clipboard
- **Trade Plan saved plans**: danh sách kế hoạch đã lưu với filter trạng thái, lot progress bar, nút thực hiện từng lot
- **Positions API** (`GET /api/v1/positions`): backend query tổng hợp vị thế đang mở từ PnL + linked plan
- **TradePlan backend**: entity mới với lifecycle Draft→Ready→InProgress→Executed→Reviewed→Cancelled, CRUD API, commands ExecuteLot/UpdateStopLoss/TriggerExitTarget

### Cải thiện

- **TradeType enum dùng chung**: refactor toàn bộ project (6 components) sử dụng `TradeType` enum + utility functions từ `shared/constants/trade-types.ts` thay vì hardcode string
- **CAGR overflow fix**: sửa lỗi hiển thị `3.1e+260%` — thêm ngưỡng tối thiểu 30 ngày + clamp giá trị [-99.99%, 9999.99%] cả frontend và backend
- **Risk Dashboard tiếng Việt**: dịch toàn bộ text tiếng Anh còn sót sang tiếng Việt
- **Trades pagination**: sửa lỗi không nhấn được nút "Sau" (nextPage reset về trang 1)
- **Trades filter by symbol**: click vào mã CK trong bảng → tự fill ô filter, có nút × clear filter
- **Trade Create — lô chẵn**: lệnh MUA bắt buộc số lượng là bội số 100
- **Trade Create — kiểm tra số dư**: giá trị lệnh MUA không được vượt quá tiền còn lại của danh mục (initialCapital - totalInvested + totalSold)
- **Trade Create — hiện vốn danh mục**: dropdown danh mục hiển thị thêm tổng vốn bên cạnh tên
- **Trade Wizard**: dùng shared TradeType, pre-fill journal từ thông tin trade plan
- **Backtesting**: dùng shared `getTradeTypeDisplay`/`getTradeTypeClass`

### Sửa lỗi

- Fix webpack `Cannot access before initialization` error khi vào trang Risk và Strategies (cache corruption)
- Fix CAGR backend (`PerformanceMetricsService.cs`): clamp giá trị, minimum years 0.08

---

## [v2.5.0] — 2026-03-14 · Phase 7 (tiếp): NumMask, PnL & Journal enhancements

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **NumMaskDirective**: format số với dấu phân cách hàng nghìn trong input fields, áp dụng trên backtesting và strategies
- **Journal enhancements**: unsaved changes prompt, trade linkage improvements

### Cải thiện

- **PnL calculations**: cải thiện tính toán và xử lý lỗi trong PerformanceMetricsService
- **Error handling**: cải thiện middleware exception handling

---

## [v2.4.0] — 2026-03-13 · Phase 7 (tiếp): Tooltip Analytics & Glossary UX

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **Tooltip thuật ngữ trang Phân tích** (`/analytics`): hover vào icon `ⓘ` cạnh tên chỉ số để xem giải thích tại chỗ — không cần cuộn xuống glossary card
  - Header cards: CAGR, Sharpe Ratio, Sortino Ratio, Max Drawdown, Win Rate
  - Section "Chỉ số rủi ro": Win Rate, Profit Factor, Value at Risk (95%), Expectancy
  - Tab Equity Curve: tiêu đề, cột Lợi nhuận ngày, cột Lợi nhuận tích luỹ
- **CSS `.tooltip-trigger` / `.tooltip-box`** trong `styles.css`: component tooltip dùng chung toàn project — dark popup, mũi tên chỉ xuống, fade-in 0.15s

### Cải thiện

- **Glossary footnote style**: đổi từ ký tự Unicode `¹²³` sang chữ số thường `1 2 3` + CSS `::before`/`::after` tự thêm dấu ngoặc → hiển thị **(1) (2) (3)** nhất quán mọi nơi
- **SVG info icon**: thay thế ký tự `ⓘ` Unicode bằng Heroicons `information-circle` SVG — sắc nét, scale tốt mọi độ phân giải
- **Glossary footnote size**: `font-size: 0.85em`, `vertical-align: super` — dễ đọc hơn

### Gợi ý cho lần release tiếp theo

- [ ] Áp dụng `.tooltip-trigger` / `.tooltip-box` cho các trang khác (Risk Dashboard, Trade Plan) thay thế glossary card tĩnh
- [ ] Tooltip delay ~200ms để tránh hiện khi hover qua nhanh

---

## [v2.3.0] — 2026-03-13 · Phase 7 (tiếp): Thuật ngữ chuyên ngành & Strategy Auto-fill

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **Glossary thuật ngữ chuyên ngành** toàn project: mỗi thuật ngữ hiển thị số mũ nhỏ `¹²³` → giải thích đầy đủ ở cuối form, áp dụng đồng bộ trên tất cả trang:
  - **Risk Dashboard**: VaR 95%, Max Drawdown, Win Rate, Profit Factor, Beta, Tương quan (Correlation)
  - **Trade Wizard** (step 2 + step 5): Stop-Loss, Take-Profit, % Rủi ro/lệnh, R:R, Thiết lập kỹ thuật, FOMO
  - **Monthly Review**: Win Rate, P&L (Profit & Loss), Max Drawdown
  - **Journals**: Setup kỹ thuật, Trạng thái cảm xúc/FOMO, Mức tự tin, Post-trade Review
  - **Strategies** (form tạo + tab Hiệu suất): Khung thời gian (Scalping/Day Trading/Swing/Position), Win Rate, P&L, Profit Factor
- **Strategy auto-fill SL/TP** trong Trade Plan: chọn chiến lược có `SuggestedSlPercent` / `SuggestedRrRatio` → tự động tính và điền Stop-Loss & Take-Profit, hiển thị badge "✓ Tự động điền từ chiến lược"
- **`SuggestedSlPercent` và `SuggestedRrRatio`** trên Strategy entity (backend + frontend): 2 trường mới lưu gợi ý SL% dưới giá vào và R:R ratio, expose qua CQRS commands/queries và REST API

### Cải thiện

- Form tạo chiến lược: thêm input "SL gợi ý (%)" và "R:R gợi ý" với giải thích inline
- `onStrategyChange()` trong Trade Plan: tính SL/TP từ entry price × strategy hints, chỉ tự điền khi ô đang trống (không ghi đè nếu người dùng đã nhập)
- Glossary card dùng màu nhất quán: đỏ=SL, xanh lá=TP, xanh dương=R:R, cam=Drawdown, tím=Beta, hổ phách=Rủi ro

### Gợi ý cho lần release tiếp theo

- [ ] Glossary dạng tooltip hover thay vì card tĩnh cuối form (tiết kiệm không gian hơn)
- [ ] Trade Plan: nút "Reset về gợi ý chiến lược" khi SL/TP đã bị sửa tay
- [ ] Strategy Performance: thêm chart đường cong lãi/lỗ tích lũy theo thời gian

---

## [v2.2.0] — 2026-03-13 · Phase 7: Quick Trade, positionSize Template, Multi-timeframe

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **Quick Trade widget** trên Dashboard: nhập mã CP (auto-fill giá), chiều, entry, SL → tính position size từ Risk Profile tại chỗ → "Mở trong Trade Plan" với dữ liệu đã điền sẵn
- **Multi-timeframe switcher** trên Dashboard: tab Hôm nay / Tuần này / Tháng này / Năm nay / Toàn bộ → hiển thị period return % và period P&L từ Equity Curve
- **`positionSize` trong Trade Plan Template**: lưu số lượng CP khi save template, tự điền lại khi load template

### Cải thiện

- Quick Trade collapsible panel — không chiếm không gian khi không dùng
- Multi-timeframe tính từ equity curve data đã có sẵn — không cần API call thêm
- Template save/load đầy đủ hơn: symbol, direction, giá, SL, target, chiến lược, lý do, notes, **số lượng CP**

### Gợi ý cho lần release tiếp theo

- [ ] Quick Trade: thêm ô Target → tính và hiển thị R:R ratio
- [ ] Multi-timeframe: thêm trade count và win rate trong kỳ (cần fetch trades theo date range)
- [ ] Risk Score badge tự refresh mỗi 5 phút (hiện chỉ load 1 lần lúc login)
- [ ] Keyboard shortcuts: `Ctrl+T` → Trade Plan, `Ctrl+W` → Wizard, `Ctrl+D` → Dashboard

---

Tất cả thay đổi đáng kể của dự án được ghi lại ở đây.
Format theo [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [v2.1.0] — 2026-03-13 · Phase 5 & 6: Auto-fill, Risk, Templates, Changelog

**Branch:** `feature/phase5-autofill-risk-compound`

### Thêm mới
- **Auto-fill giá cổ phiếu** trong Trade Wizard: nhập mã CP → blur → tự fetch giá hiện tại và điền vào Entry Price
- **Concentration Alert**: cảnh báo khi 1 cổ phiếu vượt giới hạn `maxPositionSizePercent` trong Risk Profile, hiển thị trực tiếp trên Dashboard
- **Trade Plan Template save/load**: lưu kế hoạch GD thành template, tải lại với 1 click, xóa template không cần nữa
- **Trang Changelog** (`/changelog`): developer changelog đọc từ file `.md`, accessible không cần đăng nhập
- **DEV badge** trên header: link nhanh đến `/changelog` từ mọi trang

### Sửa lỗi
- Fix lỗi `Cannot access 'MarketDataComponent' before initialization` do 2 `import` viết trên 1 dòng trong `market-data.service.ts`
- Fix Risk Alert Banner sort: hiển thị cảnh báo nghiêm trọng nhất lên đầu (descending)

### Cải thiện
- Dashboard load risk alert chạy song song với `forkJoin` thay vì tuần tự
- `docs/features.md`: tài liệu tính năng đầy đủ theo từng phase
- `docs/getting-started.md`: thêm mục "Build vs Deploy" với lệnh cụ thể cho dự án

### Gợi ý cho lần release tiếp theo
- [ ] Quick Trade widget ngay trên Dashboard (nhập CP + Mua/Bán + SL → tính Position Size tại chỗ)
- [ ] Risk Score badge trên header tự refresh mỗi 5 phút (hiện chỉ load 1 lần khi login)
- [ ] Thêm field `positionSize` vào Trade Plan Template để save/load luôn số lượng CP

---

## [v2.0.0] — 2026-03-12 · Phase 3 & 4: Charts, Risk Dashboard, Compound Tracker

**Branch:** `feature/phase4-charts-and-links`

### Thêm mới
- **Equity Curve chart** (Chart.js): line chart tăng trưởng vốn theo ngày, range filter 30D/90D/1Y/All
- **Monthly Returns Matrix**: hiệu suất theo năm × tháng, color-coded xanh/đỏ
- **CAGR thực tế**: tính từ capital flows + daily snapshots, hiển thị trên Dashboard
- **Compound Growth Tracker**: card "Lãi kép" trên Dashboard — CAGR thực tế, ước tính 5/10/20 năm, so sánh vs mục tiêu
- **Risk Alert Banner** trên Dashboard: stop-loss proximity, drawdown alert
- **Risk Dashboard** (`/risk-dashboard`): tổng quan sức khỏe rủi ro, bảng position, stress test 5 kịch bản VNINDEX
- **Risk Score badge** trên Header: badge màu động (xanh/vàng/đỏ) link đến Risk Dashboard
- **Monthly Review** (`/monthly-review`): báo cáo tháng tự động — win rate, P&L, drawdown, best/worst trade

### Cải thiện
- Analytics: thay placeholder bằng biểu đồ thực tế (bar chart P&L, donut phân bổ danh mục)
- Dashboard 4 Summary Cards: Tổng giá trị, Vốn đầu tư, P&L, CAGR

### Gợi ý đã xử lý ở phase sau
- ~~Concentration Alert~~ → Done v2.1.0
- ~~Auto-fill giá~~ → Done v2.1.0

---

## [v1.5.0] — 2026-03-10 · Phase 2: Wizard Flow & Risk Profile

**Branch:** `feature/phase2-wizard-flow`

### Thêm mới
- **Trade Wizard 5 bước** (`/trade-wizard`): Chiến lược → Kế hoạch → Checklist → Giao dịch → Nhật ký
- **GO/NO-GO enforcement**: checklist bắt buộc, không thể skip qua bước Giao dịch nếu chưa đạt ≥80%
- **Risk Profile** (`/risk`): thiết lập max position%, max risk/lệnh, R:R tối thiểu, max drawdown alert
- **Position Sizing tự động**: nhập Entry + SL → tính ngay số lượng CP dựa trên Risk Profile
- **Risk violations enforcement**: cảnh báo đỏ + yêu cầu xác nhận khi Trade Plan vi phạm Risk Profile

### Cải thiện
- Trade Plan: thêm các field SL, Target, Risk/Reward calculation
- Strategies: load từ system templates (14 chiến lược mẫu), filter theo category/difficulty/timeframe

---

## [v1.0.0] — 2026-03-05 · Phase 1: Nền tảng

**Branch:** `feature/phase1-foundation`

### Thêm mới
- **Google OAuth 2.0** login
- **Portfolio CRUD**: tạo/sửa/xóa danh mục đầu tư
- **Trade CRUD**: ghi nhận giao dịch Mua/Bán
- **P&L theo Average Cost Method**: Realized + Unrealized P&L
- **Capital Flows**: theo dõi dòng vốn vào/ra
- **Daily Snapshots**: lưu giá trị danh mục mỗi ngày cho Equity Curve
- **Journals** (`/journals`): nhật ký giao dịch
- **Alerts** (`/alerts`): cảnh báo giá, stop-loss
- **Market Data** (`/market-data`): tra cứu giá cổ phiếu từ API bên ngoài
- **Backtesting** (`/backtesting`): backtest chiến lược cơ bản

### Kiến trúc
- Clean Architecture: Domain → Application → Infrastructure → API
- CQRS + MediatR
- MongoDB 7.0
- .NET 8 + Angular 19
- JWT authentication
- Background Worker cho P&L calculations

---

## [v0.1.0] — 2026-02-20 · Khởi tạo dự án

### Thêm mới
- Khởi tạo solution `.NET 8` với Clean Architecture
- Khởi tạo Angular 19 frontend với Tailwind CSS
- Cấu hình MongoDB connection
- Cấu hình JWT authentication
- Docker Compose cho local development
