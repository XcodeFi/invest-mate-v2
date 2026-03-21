# Changelog — Investment Mate v2

---

## [v2.18.0] — 2026-03-21 · Enhance AI Prompts & Deep Integration (11 Use Cases)

**Branch:** `feature/enhance-ai-prompts`

### Thêm mới

- **5 AI use case mới** — tổng cộng 11 use case, tích hợp sâu vào mọi trang chính:
  - **AI Risk Assessment** (`/risk-dashboard`): Phân tích sức khỏe rủi ro — health score 0-100, vi phạm giới hạn, correlation risk, drawdown, 3 hành động giảm rủi ro cụ thể
  - **AI Position Advisor** (`/positions`): Tư vấn vị thế — vị thế nguy hiểm, cơ hội chốt lời, kế hoạch bị thiếu, hành động ưu tiên
  - **AI Trade Analysis** (`/trades`): Phân tích giao dịch — win rate & expectancy, hiệu suất theo mã, kỷ luật theo kế hoạch, pattern hành vi
  - **AI Watchlist Scanner** (`/watchlist`): Quét watchlist — cơ hội mua gần giá mục tiêu, tín hiệu kỹ thuật, xếp hạng ưu tiên, action plan top 3
  - **AI Daily Briefing** (`/dashboard`): Bản tin hôm nay — tóm tắt buổi sáng, hành động khẩn cấp, cơ hội hôm nay, cảnh báo rủi ro, checklist

### Cải tiến

- **Enriched prompts cho 6 use case hiện có** — cross-reference data giữa các domain:
  - **Trade Plan Advisor**: + market data real-time, technical signals (RSI/MACD/EMA/S&R), risk compliance, historical trades trên cùng mã
  - **Portfolio Review**: + risk profile, risk summary, active trade plans count
  - **Monthly Summary**: + performance metrics (win/loss/win rate/realized P&L), so sánh tháng trước, per-symbol P&L
  - **Journal Review**: + thống kê journal (avg confidence, avg rating, emotion distribution), portfolio context, tăng từ 5→10 entries
  - **Chat Assistant**: + active positions (top 5), watchlist summary, current date
  - **Stock Evaluation**: + user position nếu đang nắm giữ, watchlist target prices, active trade plan

### Backend

- `AiAssistantService`: thêm 3 dependencies (`IRiskCalculationService`, `IRiskProfileRepository`, `IWatchlistRepository`), 5 context builders mới, 5 streaming methods mới, enhance 6 builders hiện có
- `IAiAssistantService`: 5 method signatures mới + `watchlistId` parameter cho `BuildContextAsync`
- `AiController`: 5 endpoints mới (risk-assessment, position-advisor, trade-analysis, watchlist-scanner, daily-briefing) + 5 Request DTOs

### Frontend

- `AiService`: 5 stream methods mới (`streamRiskAssessment`, `streamPositionAdvisor`, `streamTradeAnalysis`, `streamWatchlistScanner`, `streamDailyBriefing`)
- `AiChatPanelComponent`: 5 cases mới trong `getStream()` switch
- Tích hợp `AiChatPanelComponent` vào 5 trang: risk-dashboard, positions, trades, watchlist, dashboard — mỗi trang có nút AI và sliding panel

---

## [v2.17.0] — 2026-03-21 · AI Đánh giá Nhanh Mã + Copy Prompt + XML Tagging

**Branch:** `feature/ai-context-copy`

### Thêm mới

- **AI Đánh giá Nhanh Mã (use case #6)**: Đánh giá toàn diện cổ phiếu kết hợp phân tích cơ bản (P/E, EPS, ROE, D/E) + kỹ thuật (EMA/RSI/MACD/S&R)
  - Nút "✨ AI Đánh giá" trên trang `/market-data` (cạnh "Tạo Trade Plan từ gợi ý")
  - Tích hợp **TCBS API** (`apipubaws.tcbs.com.vn`) cho dữ liệu fundamental: P/E, P/B, EPS, ROE, ROA, Nợ/Vốn, tăng trưởng doanh thu & lợi nhuận, vốn hóa, cổ tức, SHNN
  - Interface `IFundamentalDataProvider` + `TcbsFundamentalDataProvider` (cache 5 phút)
- **Copy Prompt to Clipboard**: Nút 📋 trong AI panel header → tạo prompt hoàn chỉnh (system prompt + user message + XML-tagged data) → copy vào clipboard
  - Dùng với Claude Max / Gemini client app bên ngoài, **không cần API key**
  - Endpoint: `POST /api/v1/ai/build-context` → JSON (không SSE)
  - Hoạt động cho tất cả 6 use cases

### Cải tiến

- **XML Tagging cho tất cả prompt**: Áp dụng XML tags (`<portfolio>`, `<positions>`, `<fundamental_metrics>`, `<technical_signals>`, `<trade_plan>`, `<trade_journals>`, etc.) + markdown tables → AI parse dữ liệu chính xác hơn
- **Refactor `AiAssistantService`**: Tách thành private context builders cho mỗi use case, dùng chung cho cả streaming lẫn copy-prompt
- **Model selector trong AI panel**: Dropdown chọn model (Sonnet/Opus/Gemini) trực tiếp trong header chat panel

### Backend

- `IFundamentalDataProvider` interface + `StockFundamentalData` DTO (Application layer)
- `TcbsFundamentalDataProvider` — TCBS API integration, `TcbsApiModels` response DTOs
- `AiContextResult` DTO — `{ SystemPrompt, UserMessage, ErrorMessage }`
- `AiAssistantService` refactored: 6 private `BuildXxxContext()` methods + public `BuildContextAsync` dispatcher + `EvaluateStockAsync` streaming
- `AiController`: thêm `POST /ai/stock-evaluation` (SSE) + `POST /ai/build-context` (JSON)
- DI: register `TcbsFundamentalDataProvider` với HttpClient

### Frontend

- `AiService`: thêm `streamStockEvaluation()`, `buildContext()`
- `AiChatPanelComponent`: nút 📋 Copy Prompt, `stock-evaluation` case
- `MarketDataComponent`: import `AiChatPanelComponent`, nút "✨ AI Đánh giá", `isAiOpen` state

---

## [v2.16.0] — 2026-03-20 · Thêm Google Gemini — Hỗ trợ đa nhà cung cấp AI

**Branch:** `feature/ai-integration`

### Thêm mới

- **Google Gemini (nhà cung cấp AI thứ 2)**: Hỗ trợ đa nhà cung cấp AI — Claude (Anthropic) + Gemini (Google) trong cùng hệ thống
  - **Provider tabs**: Chuyển đổi giữa Claude / Gemini trên trang `/ai-settings`
  - **Dual API key**: Lưu trữ API key riêng cho từng provider (mã hóa, BsonElement backward compat)
  - **Gemini models**: `gemini-2.0-flash`, `gemini-2.5-flash`, `gemini-2.5-pro`
  - **Factory pattern**: `IAiChatServiceFactory` resolve đúng service theo provider (`ClaudeApiService` | `GeminiApiService`)

### Backend

- `AiSettings` entity: thêm `Provider` ("claude" | "gemini"), đổi tên `EncryptedApiKey` → `EncryptedClaudeApiKey` (BsonElement backward compat), thêm `EncryptedGeminiApiKey` (nullable)
- `AiSettings` methods mới: `UpdateProvider()`, `UpdateClaudeApiKey()`, `UpdateGeminiApiKey()`, `GetActiveEncryptedApiKey()`
- `GeminiApiService` — gọi Google Gemini streaming API, role mapping "assistant" → "model", SSE format
- `IAiChatServiceFactory` + `AiChatServiceFactory` — factory pattern resolve đúng provider
- DI: `AddHttpClient` riêng cho từng provider (Anthropic + Google), factory registration
- Chi phí token tính theo provider

### Frontend

- Provider tabs UI trên `/ai-settings`: chuyển đổi Claude / Gemini, nhập API key riêng, model dropdown theo provider
- `AiService` cập nhật: hỗ trợ provider field trong settings CRUD

---

## [v2.15.0] — 2026-03-20 · Tích hợp AI Claude

**Branch:** `feature/ai-integration`

### Thêm mới

- **Trợ lý AI Claude**: Tích hợp 5 use case AI streaming (SSE) vào ứng dụng
  - **AI Journal Review**: Phân tích nhật ký giao dịch — nhận diện tâm lý (FOMO, revenge trading), đánh giá kỷ luật, gợi ý cải thiện
  - **AI Portfolio Review**: Đánh giá danh mục — đa dạng hóa, hiệu suất, rủi ro, gợi ý cân bằng
  - **AI Trade Plan Advisor**: Tư vấn kế hoạch giao dịch — chấm điểm entry/SL/TP, position sizing, R:R
  - **AI Chat Assistant**: Trợ lý tổng hợp — chiến lược, phân tích kỹ thuật, quản lý rủi ro (nút AI trên header)
  - **AI Monthly Summary**: Tổng kết hiệu suất tháng — giao dịch nổi bật, pattern, gợi ý tháng tới
- **Trang `/ai-settings`**: Cấu hình AI — nhập API key Anthropic (mã hóa), chọn model (Sonnet/Opus), test kết nối, xem thống kê sử dụng (tokens + chi phí USD)
- **AI Chat Panel**: Component tái sử dụng — sliding panel từ phải, markdown rendering, follow-up questions, token usage display

### Backend

- `AiSettings` entity (Domain) — lưu API key mã hóa, model, token usage per user
- `AiKeyEncryptionService` — mã hóa API key bằng ASP.NET Data Protection
- `ClaudeApiService` — gọi Anthropic Messages API với streaming SSE
- `AiAssistantService` — orchestrate 5 use cases: gather context, build Vietnamese system prompts, track usage
- `AiSettingsController` (`api/v1/ai-settings`) — GET/PUT/DELETE + test connection
- `AiController` (`api/v1/ai`) — 5 SSE streaming endpoints

### Frontend

- `AiService` — CRUD settings (HttpClient) + streaming (fetch + ReadableStream → Observable)
- `AiChatPanelComponent` — reusable sliding panel, markdown (marked), auto-start + follow-up
- `AiSettingsComponent` — settings page: API key, model select, usage stats, danger zone
- Integration: nút AI trên journals, portfolio-detail, trade-plan, monthly-review, header

---

## [v2.14.0] — 2026-03-20 · Smart Trade Signals

**Branch:** `feature/smart-signals`

### Thêm mới

- **Phân tích kỹ thuật tự động**: Tra cứu cổ phiếu → tự động chạy phân tích EMA(20/50), RSI(14), MACD(12,26,9), Volume ratio, hỗ trợ/kháng cự
- **Tín hiệu tổng hợp**: Mua mạnh / Mua / Chờ / Bán / Bán mạnh — dựa trên 4 chỉ báo kỹ thuật
- **Gợi ý giao dịch**: Entry (hỗ trợ gần nhất), Stop-loss, Target (kháng cự gần nhất), Risk:Reward ratio
- **"Tạo Trade Plan từ gợi ý"**: 1 click tạo Trade Plan từ kết quả phân tích kỹ thuật (pre-fill entry/SL/TP)
- **Watchlist signal column**: Tín hiệu kỹ thuật hiển thị trên bảng watchlist (top 10 mã)

### Backend

- `ITechnicalIndicatorService` + `TechnicalIndicatorService` — engine phân tích kỹ thuật
- `GetTechnicalAnalysisQuery` — CQRS query via MediatR
- API endpoint: `GET /api/v1/market/stock/{symbol}/analysis`
- Indicators: EMA, RSI (Wilder's smoothed), MACD with crossover, Volume ratio, Swing High/Low (5-window), Level clustering (2%)

### Frontend

- `TechnicalAnalysis` interface + `getTechnicalAnalysis()` method in `MarketDataService`
- Analysis UI section in `MarketDataComponent`: indicators grid, S&R levels, trade suggestion card
- Signal column in `WatchlistComponent` (desktop table + mobile cards)

---

## [v2.13.0] — 2026-03-20 · Watchlist Thông minh

**Branch:** `feature/watchlist`

### Thêm mới

- **Trang `/watchlist`**: Theo dõi cổ phiếu quan tâm — bảng giá live, ghi chú, giá mục tiêu mua/bán, deep link đến Trade Plan
- **CRUD Watchlist**: Tạo/sửa/xoá nhiều danh sách với emoji tuỳ chỉnh
- **Import VN30**: Nhập 30 mã VN30 bằng 1 click
- **Symbol autocomplete**: Tìm kiếm mã qua 24hmoney API (debounced)
- **Dashboard widget**: Top 5 mã từ watchlist hiển thị ngay trên Tổng quan
- **Navigation**: Header (Phân tích group) + Bottom nav (moreItems)

### Backend

- Domain entities: `Watchlist` (AggregateRoot), `WatchlistItem` (ValueObject embedded)
- API: `WatchlistsController` (`api/v1/watchlists`) — 9 endpoints
- MongoDB: `watchlists` (compound index UserId)
- CQRS: 7 commands + 2 queries

### Frontend

- `WatchlistService` — 9 API methods
- `WatchlistComponent` — standalone, inline template, Tailwind CSS
- Dashboard widget tích hợp trong `DashboardComponent`
- Responsive: desktop table + mobile cards

---

## [v2.12.0] — 2026-03-18 · Trade Plan UX — Tạo kế hoạch từ trang giao dịch

**Branch:** `feature/trade-plan-enhancements`

### Thêm mới

- **Nút "Tạo kế hoạch"** trên trang Lịch sử giao dịch: khi mã CP chưa có kế hoạch nào, hiện nút tạo KH thay vì chỉ hiện text "Không có KH" — navigate đến `/trade-plan?symbol=XXX`
- **Pre-fill symbol** trên trang Kế hoạch: nhận query param `?symbol=` → tự điền mã CP + fetch giá hiện tại
- **Nút lưu trong sidebar**: "Lưu nháp" và "Lưu & Sẵn sàng" chuyển từ header xuống cột phải (sidebar), đúng luồng UX cuộn xuống

### Cải thiện

- Mobile: thay text tĩnh "Chưa gắn KH" bằng link actionable "+ Tạo KH" / "Gắn KH"
- Phân tách rõ khu vực Lưu kế hoạch vs Thực hiện giao dịch trong sidebar

---

## [v2.12.0] — 2026-03-18 · Trader's Daily Todo List & Routine Templates

**Branch:** `feature/trader-daily-todo`

### Thêm mới

- **Daily Routine Widget** trên Dashboard: hiển thị tiến độ nhiệm vụ hôm nay, streak badge (🔥), next uncompleted items với deep links
- **Trang `/daily-routine`**: quản lý nhiệm vụ hàng ngày đầy đủ — checklist theo 3 nhóm (Sáng / Trong phiên / Cuối ngày), progress bar, streak counter
- **5 Built-in Templates**: Swing Trading (12 bước), DCA (8 bước), Research (10 bước), Onboarding (8 bước), Crisis Checklist (8 bước)
- **Auto-suggest**: Tự gợi ý template dựa trên ngữ cảnh (ngày DCA, cuối tuần, thị trường biến động, lần đầu sử dụng)
- **Streak Gamification**: Đếm ngày liên tiếp hoàn thành, kỷ lục cá nhân, thông điệp động lực (3, 5, 10, 30 ngày)
- **Custom Templates**: Tạo/sửa/xoá mẫu riêng với form dynamic items
- **History Heatmap**: Lịch sử 30 ngày gần nhất (xanh/vàng/xám)
- **Deep Links**: Mỗi item có link navigate thẳng đến trang liên quan

### Backend

- Domain entities: `DailyRoutine`, `RoutineTemplate`, `RoutineItem`, `RoutineItemTemplate`
- API: `DailyRoutinesController` (`api/v1/daily-routines`) — 10 endpoints
- MongoDB: `daily_routines` (compound index UserId+Date, soft-delete cleanup trước insert), `routine_templates`
- Seed data: 5 built-in templates (Vietnamese có dấu đầy đủ)

### Frontend

- `DailyRoutineService` — 11 API methods
- `DailyRoutineComponent` — standalone, inline template, Tailwind CSS
- Dashboard widget tích hợp trong `DashboardComponent`
- Navigation: Header (Quản lý group) + Bottom nav (moreItems)

---

## [v2.11.0] — 2026-03-18 · Mobile Responsive — Tối ưu giao diện di động

**Branch:** `feature/b1-mobile-responsive`

### Thêm mới

- **Bottom Navigation** (`BottomNavComponent`): Thanh điều hướng cố định ở đáy màn hình trên mobile (< 768px) với 5 mục: Tổng quan, Giao dịch, Kế hoạch, Rủi ro, Thêm
- **Mobile card layout**: 14 bảng dữ liệu (trades, trade-plan, risk, analytics, portfolio-detail, portfolio-trades, portfolio-analytics, capital-flows, market-data, snapshots) chuyển sang dạng card trên mobile
- **Scrollable tabs**: Tab navigation cuộn ngang với ẩn scrollbar trên mobile (analytics, risk, snapshots)

### Cải thiện

- Grid summary cards xếp 1 cột trên mobile nhỏ (`grid-cols-1 sm:grid-cols-2`) — ~15 components
- Page header xếp dọc trên mobile (trades, dashboard, portfolios, portfolio-detail, portfolio-trades)
- Tooltip không bị tràn trên màn hình nhỏ (`max-width: calc(100vw - 2rem)`)
- Content padding `pb-14` trên mobile tránh bị bottom nav che

---

## [v2.10.0] — 2026-03-17 · Trade Replay — Xem lại giao dịch trên biểu đồ giá

**Branch:** `feature/m2-and-enhancements`

### Thêm mới

- **Trade Replay** (`/trade-replay/:id`): Visualize toàn bộ vòng đời kế hoạch giao dịch trên biểu đồ giá thực từ 24hmoney API
  - Biểu đồ giá đóng cửa (Chart.js) với overlay: vào lệnh (▲ xanh), thoát lệnh (▼ đỏ), tạo KH (★ xanh), stop-loss (nét đứt đỏ), mục tiêu (nét đứt xanh)
  - Summary cards: Giá vào lệnh (KH/TT), Lãi/Lỗ, R:R (KH/TT), Phí GD
  - Dòng thời gian sự kiện: Tạo KH → Vào lệnh → Điều chỉnh SL → Thoát lệnh → Hoàn thành
  - Entry point: Nút "Xem replay" trên bảng kế hoạch cho status Executed/Reviewed
- **Symbol Autocomplete real-time**: Thay thế file JSON tĩnh (58 mã) bằng `MarketDataService.searchStocks()` (API 24hmoney), debounce 300ms, hiển thị tên công ty + sàn

---

## [v2.9.0] — 2026-03-17 · Tích hợp 24hmoney API — Dữ liệu thị trường real-time

**Branch:** `feature/m2-and-enhancements`

### Thêm mới

- **24hmoney.vn API Provider**: `HmoneyMarketDataProvider` — nguồn dữ liệu thị trường chứng khoán Việt Nam real-time, thay thế toàn bộ mock data
- **5 API endpoints mới**: Stock detail (`/market/stock/{symbol}/detail`), Market overview (`/market/overview`), Search (`/market/search`), Top fluctuation (`/market/top-fluctuation`), Trading summary (`/market/stock/{symbol}/summary`)
- **IStockInfoProvider interface**: Interface mới cho stock detail, search, top fluctuation, trading summary
- **Trang Thị trường nâng cao**: Overview 4 chỉ số (VN-INDEX, VN30, HNX, UPCOM), tra cứu cổ phiếu chi tiết với order book 3 mức, tìm kiếm autocomplete (debounce 300ms), top biến động theo sàn (HOSE/HNX/UPCOM tabs), biến động giá 1D/1W/1M/3M/6M
- **Dashboard Market Overview**: Strip 4 index cards ở đầu dashboard — giá, %, KL

### Sửa lỗi

- **StockPriceService mock → real API**: Xoá toàn bộ giá cổ phiếu mock hardcoded (~20 mã), delegate sang `IMarketDataProvider` (24hmoney). P&L, Risk, Positions, Strategy Performance giờ dùng giá thật VND thay vì giá giả USD
- **Worker mock → real API**: Worker background jobs (PriceSnapshot, BacktestJob) giờ dùng `HmoneyMarketDataProvider` thay vì `MockMarketDataProvider`

### Cải thiện

- **IMemoryCache**: Cache giá cổ phiếu (15s), chỉ số (15s), danh sách công ty (30 phút) — configurable qua `appsettings.json`
- **Price ×1000 scaling**: API 24hmoney trả giá ÷1000, tự động nhân lại khi mapping. Chỉ số index giữ nguyên
- **Shared raw cache**: `GetCurrentPriceAsync` và `GetStockDetailAsync` dùng chung cache raw response — cùng 1 mã chỉ gọi API 1 lần trong 15s
- **MarketIndexData enriched**: Thêm foreign trading, advance/decline, prior close cho dữ liệu chỉ số
- **BaseUrl configurable**: URL API 24hmoney đọc từ config/env var, không hardcode

---

## [v2.8.0] — 2026-03-14 · M2 Fix + 6 Feature Enhancements

**Branch:** `feature/m2-and-enhancements`

### Bug fix

- **M2: Cột KẾ HOẠCH toàn "---"**: Thêm backend `LinkTradeToPlanCommand` + API `PATCH /trades/{id}/link-plan`, frontend hiện nút "Gắn KH" cho trade chưa liên kết, dropdown chọn kế hoạch theo mã CK

### Thêm mới

- **Import CSV**: Trang `/trades/import` — upload file CSV, preview dữ liệu, validate, bulk import giao dịch vào danh mục. Backend `BulkCreateTrades` API
- **Journal tự động**: Wizard step 4 auto-create journal entry khi ghi nhận giao dịch, step 5 update thay vì tạo mới nếu đã tồn tại
- **Dashboard vị thế nổi bật**: Widget "Vị thế nổi bật" hiện top 6 positions theo giá trị, P&L%, link đến trang Vị thế

### Cải thiện

- **Phiếu lệnh nâng cao**: Thêm nút In (print), hiện Danh mục + Giá trị lệnh trong phiếu, filter dòng trống
- **Vị thế — SL/TP distance**: Thanh gradient SL→TP với marker giá hiện tại, % khoảng cách đến SL/TP, cảnh báo màu khi gần SL
- **Vị thế — Sắp xếp**: Dropdown sắp xếp theo Giá trị / Lãi-Lỗ / % / Mã CK

---

## [v2.7.0] — 2026-03-14 · Phase 7 (tiếp): Bug fix Round 6

**Branch:** `feature/phase7-improvements`

### Sửa lỗi

- **H1: Fix DCA mode**: tách UI riêng cho DCA — giao diện mới với Số tiền/lần, Tần suất (tuần/2 tuần/tháng), Số kỳ, Ngày bắt đầu, Khoảng giá, Lịch mua dự kiến với bảng schedule
- **H2: Fix CAGR mismatch**: Dashboard và Analytics hiện dùng cùng nguồn CAGR (equity curve hoặc backend AdvancedAnalytics) — bỏ phép tính sai dùng `years=1` hardcoded
- **M1: Fix vị thế lớn nhất > 100%**: sửa backend `RiskCalculationService` dùng `Math.Max(netWorth, totalMarketValue)` làm mẫu số cho position sizing — tránh % vượt 100% khi tiền mặt âm
- **M3: Fix giá 0 trên trade-plan**: thêm `[emptyWhenZero]` directive vào NumMask — các trường Giá vào, Stop-Loss, Take-Profit, Số lượng hiện placeholder thay vì "0" khi chưa nhập

### Cải thiện

- **NumMaskDirective**: thêm `@Input() emptyWhenZero` — khi `true`, hiện empty thay vì "0" trong display mode
- **DCA form**: summary card (tổng vốn, thời gian, tần suất) + schedule table với cumulative amount
- **Trade Plan placeholders**: placeholder text gợi ý cho các trường giá ("Nhập giá dự kiến", "Mức cắt lỗ", "Mức chốt lời")

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
