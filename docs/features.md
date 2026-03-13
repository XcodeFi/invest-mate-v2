# Investment Mate v2 — Tài liệu Tính năng

> **Cập nhật lần cuối:** 2026-03-13
> **Trạng thái:** Phase 6 hoàn thành

---

## Tổng quan các Phase

| Phase | Tên | Trạng thái | Branch |
|:---:|-----|:---:|--------|
| 1–2 | Nền tảng & Workflow | ✅ Done | `master` |
| 3–4 | Charts & Links | ✅ Done | `master` |
| 5 | Auto-fill, Risk & Compound | ✅ Done | `feature/phase5-autofill-risk-compound` |
| 6 | Trade Plan Template | ✅ Done | `feature/phase6-trade-plan-template` |

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

- Tra cứu giá cổ phiếu đơn lẻ (Open/High/Low/Close/Volume)
- Lịch sử giá theo khoảng thời gian tùy chọn
- Bảng giá nhanh nhiều mã (watchlist)
- Chỉ số thị trường: VNINDEX, VN30, HNX

**API:** `GET /api/v1/market/price/{symbol}`, `/history`, `/prices`, `/index/{symbol}`

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

---

## Backlog (chưa implement)

| # | Tính năng | Độ ưu tiên |
|---|-----------|:---:|
| B1 | Mobile responsive | Cao |
| B2 | Equity Curve vs Target CAGR overlay | Trung bình |
| B3 | Export PDF/Excel | Trung bình |
| B4 | Keyboard shortcuts | Thấp |
| B5 | Dark mode | Thấp |
| B6 | Multi-timeframe Dashboard | Thấp |
