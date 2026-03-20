# Investment Mate v2 — Kế hoạch Cải tiến (Round 9)

> **Ngày lập:** 2026-03-20
> **Phiên bản hiện tại:** v2.12.0 | **Điểm:** 9.4/10
> **Mục tiêu:** 10/10 — biến app từ "ghi chép" → "trợ lý phân tích & tìm cơ hội"

---

## Thứ tự triển khai

| # | Feature | Effort | Impact | Sẵn có % | Status |
|---|---|---|---|---|---|
| **1** | **Watchlist Thông minh** | Trung bình | Rất cao | 100% | ✅ Done |
| **2** | **Smart Trade Signals** (phase 1) | Cao | Rất cao | 30% | ⏳ Planned |
| **3** | **Portfolio Optimizer** (basic) | Cao | Cao | 40% | ⏳ Planned |
| 4 | Push Notifications | Trung bình | Cao | 80% | ⏳ Planned |
| 5 | PWA | Thấp | Trung bình | 70% | ⏳ Planned |
| 6 | Keyboard Shortcuts | Thấp | Trung bình | 0% | ⏳ Planned |
| 7 | Backtesting polish | Đã có | — | 100% | ⏳ Verify |
| 8 | Export PDF/Excel | Trung bình | Trung bình | 20% | ⏳ Planned |
| 9 | Multi-broker Import | Trung bình | Trung bình | 50% | ⏳ Planned |
| 10 | AI Journal Summary | Rất cao | Thấp | 10% | ❌ Defer |

---

## TIER 1 — AI-Powered Features (Ưu tiên cao nhất)

### 1. Watchlist Thông minh

**Branch:** `feature/watchlist`

**Mô tả:** Trang theo dõi cổ phiếu quan tâm trước khi tạo Trade Plan. Cầu nối Market Data → Trade Plan.

**Tính năng:**
- CRUD watchlist (nhiều danh sách: "Theo dõi", "Chờ mua", "VN30"...)
- Thêm/xoá symbol vào watchlist
- Hiển thị giá realtime, % thay đổi, volume
- Import nhanh VN30 (30 mã) bằng 1 click
- Ghi chú + giá mục tiêu cho từng mã
- Nút [Tạo Plan] → navigate `/trade-plan?symbol=X` pre-filled
- Dashboard widget: top movers trong watchlist

**Backend:**
- Entity: `Watchlist` (UserId, Name, IsDefault, Items[])
- ValueObject: `WatchlistItem` (Symbol, Note, TargetBuyPrice, TargetSellPrice, AddedAt)
- Collection: `watchlists` (compound index UserId)
- API: `WatchlistsController` — CRUD + add/remove items
- Reuse: `MarketDataService.getBatchPrices()` cho live prices

**Frontend:**
- Route: `/watchlist`
- Component: `WatchlistComponent` — bảng giá live, nút thêm/xoá, filter
- Service: `WatchlistService`
- Navigation: Header + Bottom nav

---

### 2. Smart Trade Signals — Phân tích kỹ thuật tự động

**Branch:** `feature/smart-signals` (sau Watchlist)

**Mô tả:** Khi tra cứu mã CP → hiện section phân tích kỹ thuật tự động + gợi ý entry/SL/TP.

**Phase 1 (đơn giản):**
- EMA20/EMA50 crossover → xu hướng tăng/giảm
- RSI(14) → oversold/overbought/neutral
- MACD signal line crossover → tín hiệu mua/bán
- Volume so với TB 20 phiên → volume đột biến
- Support/Resistance bằng pivot points (High/Low/Close)
- Tổng hợp signal: 🟢 MUA / 🟡 CHỜ / 🔴 BÁN
- Nút "Tạo Trade Plan từ gợi ý" → pre-fill entry/SL/TP

**Backend:**
- Service: `TechnicalIndicatorService` (EMA, RSI, MACD, Volume, Pivot Points)
- Query: `GetTechnicalAnalysis(symbol)` → DTO với tất cả indicators
- Reuse: `HmoneyMarketDataProvider.GetPriceHistory()` cho OHLCV data
- Reuse: `BacktestEngine` logic nếu có thể

**Frontend:**
- Section mới trong `market-data.component.ts` (hoặc component riêng)
- Card hiển thị indicators + signal tổng hợp
- Nút navigate đến Trade Plan pre-filled
- Tích hợp vào Watchlist: cột "Signal" cho mỗi mã

---

### 3. Portfolio Optimizer — Gợi ý cân bằng danh mục

**Branch:** `feature/portfolio-optimizer` (sau Smart Signals)

**Mô tả:** Cảnh báo tập trung + gợi ý đa dạng hóa danh mục.

**Phase 1 (basic — cảnh báo):**
- Cảnh báo khi 1 mã chiếm > 30% danh mục
- Cảnh báo khi chỉ có 1 ngành
- Hiển thị correlation matrix giữa các mã đang hold
- Gợi ý thêm mã tương quan thấp (dựa trên VN30)

**Phase 2 (advanced — tối ưu):**
- Sector classification (mapping mã → ngành)
- Mean-Variance optimization
- Efficient frontier chart
- Rebalancing suggestions

**Backend:**
- Reuse: `RiskCalculationService` (correlation đã có)
- Reuse: `PnLService` (position weight %)
- Mới: `PortfolioOptimizerService` (concentration alerts, diversification suggestions)
- Mới: Sector mapping data (có thể từ 24hmoney company info)

**Frontend:**
- Section mới trong Dashboard hoặc Risk Dashboard
- Concentration alert cards
- Correlation heatmap
- Suggested symbols list

---

## TIER 2 — Enhanced Analytics

### 4. Push Notifications (Alert system)
- **Sẵn có 80%:** AlertRule entity, AlertEvaluationService, AlertHistory, full CRUD UI
- **Cần thêm:** Service Worker, Web Push subscription, Background job poll giá mỗi 30s-1m
- **Kết hợp:** Watchlist price alerts

### 5. Trade Journal AI Summary
- **Defer** — effort rất cao, cần Claude/OpenAI API, chi phí + privacy concerns
- **Alternative đơn giản:** Thống kê cảm xúc (đếm tag FOMO/Greed/Fear) không cần AI

### 6. Backtesting
- **Đã có!** BacktestEngine + `/backtesting` route tồn tại
- **Cần:** Verify UI, thêm chiến lược mẫu, polish UX

---

## TIER 3 — UX/Quality of Life

### 7. PWA (Mobile Install)
- Thêm `manifest.json` + `ngsw-config.json` + Service Worker
- Mobile responsive đã xong (v2.11)
- Offline fallback page

### 8. Keyboard Shortcuts
- `@HostListener('document:keydown')` trên AppComponent
- N → New Plan, T → Trades, D → Dashboard, / → Search, ? → Help
- Help overlay component

### 9. Export PDF/Excel Reports
- jsPDF + ExcelJS libraries
- Monthly/quarterly/yearly trade reports
- Tax summary (Realized P&L, phí, thuế, net profit)

### 10. Multi-broker Import
- CSV templates cho VPS, SSI, VNDS, HSC
- Auto-detect format khi upload
- Reuse `BulkCreateTrades` command

---

## Bugs cần fix (Round 9)

| # | Severity | Mô tả | Status |
|---|---|---|---|
| 1 | Info | Risk Dashboard 100/100 vs Dashboard header 95/100 — khác scope tính toán | ⏳ |
| 2 | Low | "Tuân thủ hồ sơ rủi ro" hiện "Chưa thiết lập" dù đã có Risk Profile | ⏳ |
| 3 | Info | Switch template không có confirm dialog | ⏳ |
| 4 | Low | History heatmap 30 ngày tất cả xám — cần tooltip giải thích | ⏳ |
