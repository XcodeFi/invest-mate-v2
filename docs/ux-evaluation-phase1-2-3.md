# UX Evaluation & Implementation Report (2026-03-12)

## Tong quan

Danh gia UX toan dien cho Investment Mate v2 (Angular 19 + .NET 9). Phat hien 7 loi nghiem trong va 10 de xuat cai tien, chia lam 3 phase thuc hien.

---

## Phase 1 — Fix loi nghiem trong

**Branch:** `fix/phase1-critical-bugs`
**Status:** Da merge vao master (PR #3)
**Thay doi:** 10 files, +218 / -172 dong

### 1.1 Sua tieng Viet khong dau (~150 chuoi)

Cac trang bi anh huong:
- `features/risk-dashboard/risk-dashboard.component.ts` — ~40 chuoi (vd: "Suc khoe rui ro" → "Sức khỏe rủi ro")
- `features/position-sizing/position-sizing.component.ts` — ~30 chuoi
- `features/trade-plan/trade-plan.component.ts` — ~65 chuoi (template, checklist, hints, status)
- `features/backtesting/backtesting.component.ts` — ~15 chuoi

### 1.2 Dashboard Total Value = 0đ

**Nguyen nhan goc:** Property name mismatch giua backend C# va frontend TypeScript.
- Backend tra ve `TotalPortfolioValue` → ASP.NET Core serialize thanh `totalPortfolioValue`
- Frontend mong doi `totalMarketValue`

**Files sua:**
- `InvestmentApp.Api/Controllers/PnLController.cs` — Doi ten property:
  - `TotalPortfolioValue` → `TotalMarketValue`
  - `TotalReturnPercentage` → `TotalPnLPercent`
  - Them `InitialCapital`, `TotalInitialCapital`, `Positions`
- `InvestmentApp.Application/Portfolios/Queries/PnLModels.cs` — Them computed properties:
  - `PositionPnL`: them `RealizedPnL`, `TotalCost`, `TotalPnL`, `TotalPnLPercent`
  - `PortfolioPnLSummary`: them `Positions` list
- `InvestmentApp.Infrastructure/Services/PnLService.cs`:
  - Fix realized P&L: `positionPnLs.Sum(p => 0m)` → `positionPnLs.Sum(p => p.RealizedPnL)`
  - Them `Positions = positionPnLs` vao return value

### 1.3 Them co phieu Viet Nam vao mock data

`InvestmentApp.Infrastructure/Services/StockPriceService.cs` — Them 20 ma:
```
VIC (42,380), VNM (72,500), FPT (128,000), VCB (89,000), HPG (25,800),
MWG (52,600), TCB (24,500), VHM (39,500), MSN (75,000), VRE (27,800),
SSI (28,500), ACB (24,200), MBB (19,800), BID (47,500), CTG (32,000),
GAS (78,500), PLX (38,000), SAB (56,000), PNJ (78,000), REE (57,000)
```

### 1.4 Strategy template loading vo han

- `core/services/template.service.ts` — Them timeout 10s:
  ```typescript
  import { Observable, throwError, timeout } from 'rxjs';
  return this.http.get<StrategyTemplate[]>(...).pipe(timeout(10000), catchError(this.handleError));
  ```
- `features/strategies/strategies.component.ts` — Them `templateError` flag + retry UI

### 1.5 Position Sizing reset khi doi portfolio

- Giu nguyen gia vao/SL/TP khi user chon portfolio khac
- Chi cap nhat `accountBalance` va `riskPercent` tu risk profile

---

## Phase 2 — Gop trang UX

**Branch:** `feature/phase2-ux-consolidation`
**Status:** Da commit & push. Can tao PR vao master.
**Thay doi:** 4 files, +732 / -389 dong

### 2.1 Gop Position Sizing vao Trade Plan

**File:** `features/trade-plan/trade-plan.component.ts`

Tinh nang them:
- Portfolio selector voi auto-fill `accountBalance`
- Tu dong tinh so co phieu toi uu trong `recalculate()`
- Bang tham chieu nhanh (0.5%, 1%, 1.5%, 2%, 3%, 5%)
- Hien thi so lo (100 CP/lo)
- Properties moi: `accountBalance`, `riskPercent`, `maxPositionPercent`, `optimalShares`, `quickRefTable`

### 2.2 Gop Advanced Analytics vao Analytics

**File:** `features/analytics/analytics.component.ts`

Tinh nang them:
- 4 tabs: "Tổng quan", "Thống kê GD", "Equity Curve", "Theo tháng"
- Import `AdvancedAnalyticsService`, `PortfolioService`
- Methods: `loadAdvancedData()`, `getMonthlyReturn()`, `onPortfolioChangeNew()`

### 2.3 Route redirects

**File:** `app.routes.ts`
- `/position-sizing` → redirect `/trade-plan`
- `/advanced-analytics` → redirect `/analytics`

### 2.4 Navigation cleanup

**File:** `shared/components/header/header.component.ts`
- Xoa "Tính vị thế" va "Phân tích nâng cao" khoi menu

---

## Phase 3 — Tinh nang moi

**Branch:** `feature/phase3-new-features`
**Status:** Da commit & push. Can tao PR vao master.
**Thay doi:** 4 files, +1126 / -64 dong

### 3.1 Trade Wizard (`/trade-wizard`)

**File moi:** `features/trade-wizard/trade-wizard.component.ts`

5 buoc:
1. **Chon Chien luoc** — Chon strategy, hien thi rules
2. **Lap Ke hoach** — Nhap ma CK, gia vao/SL/TP, auto position sizing
3. **Checklist** — Pre-trade checklist 13 items (7 bat buoc)
4. **Xac nhan & Ghi GD** — Review + submit trade
5. **Nhat ky** — Ghi nhat ky giao dich

Services tich hop: StrategyService, PortfolioService, RiskService, TradeService, JournalService

### 3.2 Dashboard Cockpit

**File sua:** `features/dashboard/dashboard.component.ts`

Tinh nang moi:
- Portfolio allocation bars (% danh muc)
- Risk alerts (canh bao rui ro)
- Quick action buttons
- Performance progress bars

### 3.3 Route va navigation

- `app.routes.ts` — Them `/trade-wizard`
- `header.component.ts` — Them "Wizard GD" nav link

---

## Luu y ky thuat

### Property name alignment (Frontend ↔ Backend)
ASP.NET Core serialize C# PascalCase → camelCase (mac dinh). Phai dam bao:
- C#: `TotalMarketValue` → JS: `totalMarketValue`
- C#: `TotalPnLPercent` → JS: `totalPnLPercent`

### RxJS imports (Angular 19)
Import `timeout` tu `rxjs` (khong phai `rxjs/operators`):
```typescript
import { Observable, throwError, timeout } from 'rxjs';
```

### Cong thuc Position Sizing
```
Max Risk Amount = Account Balance × Risk%
Risk Per Share = |Entry Price - Stop Loss|
Optimal Shares = floor(Max Risk Amount / Risk Per Share / 100) × 100
Position Value = Optimal Shares × Entry Price
Max Position Value = Account Balance × Max Position%
```

---

## Phase 4 — Bieu do & Lien ket trang

**Branch:** `feature/phase4-charts-and-links`
**Status:** Da commit & push. Can tao PR vao master.
**Thay doi:** 6 files, +645 / -44 dong
**Dependency moi:** `chart.js v4.5`

### 4.1 Bieu do Chart.js trong Analytics

**File:** `features/analytics/analytics.component.ts`

4 bieu do duoc them:
- **P&L Bar Chart** — Bieu do cot Lai/Lo theo tung co phieu (xanh = lai, do = lo), thay the placeholder cu
- **Pie Allocation (Doughnut)** — Bieu do phan bo danh muc theo % gia tri thi truong, legend ben phai
- **Equity Curve (Line)** — Bieu do duong gia tri danh muc theo thoi gian, fill gradient, hien thi khi co du lieu snapshot
- **Monthly Returns Bar** — Bieu do cot loi nhuan theo thang (xanh/do), sap xep theo thoi gian

Ky thuat:
- Import `Chart, registerables` tu `chart.js`, goi `Chart.register(...registerables)`
- Dung `@ViewChild` de tham chieu canvas: `pnlBarCanvas`, `pieCanvas`, `equityCurveCanvas`, `monthlyBarCanvas`
- Render chart khi data load va khi chuyen tab (`onTabChange()`)
- Destroy charts khi component bi huy (`ngOnDestroy`)
- Tooltip format VND: `formatVnd()` (1.2 ty, 500 tr, 25k)

### 4.2 Mini Equity Curve tren Dashboard

**File:** `features/dashboard/dashboard.component.ts`

Tinh nang:
- Bieu do duong mini (h-48) hien thi giua Row 2 (Risk Alerts) va Row 3 (Quick Actions)
- 4 nut chon khoang thoi gian: 30D / 90D / 1Y / All
- Mau xanh khi gia tri tang, do khi giam
- Chi hien thi khi co >= 2 diem du lieu equity curve
- Goi `AdvancedAnalyticsService.getEquityCurve()` cho portfolio dau tien

### 4.3 Tinh toan CAGR thuc te

**File:** `features/dashboard/dashboard.component.ts`

2 phuong phap tinh:
1. **calculateCagr()** — Tinh nhanh tu tong von dau tu vs gia tri hien tai (fallback, mac dinh 1 nam)
2. **calculateCagrFromCurve()** — Tinh chinh xac tu equity curve:
   ```
   years = (endDate - startDate) / 365.25
   CAGR = (lastValue / firstValue)^(1/years) - 1
   ```
   Uu tien phuong phap 2 khi co du lieu equity curve.

Hien thi: Card CAGR tren Dashboard hien gia tri thuc (vd: +12.5%) thay vi "--"

### 4.4 Lien ket Trade Plan → Wizard

**File:** `features/trade-plan/trade-plan.component.ts`

Them 2 nut hanh dong sau checklist Go/No-Go:
- **"Thuc hien qua Wizard"** — Link den `/trade-wizard`, bi lam mo (opacity-50) khi chua du dieu kien
- **"Thuc hien ngay →"** — Link den `/trades/create` voi queryParams pre-fill:
  `symbol, direction, price, quantity, portfolioId, stopLoss, takeProfit`

Import them `RouterModule` de su dung `routerLink` va `queryParams`.

### 4.5 Dashboard Quick Action → Wizard

**File:** `features/dashboard/dashboard.component.ts`

- Doi link quick action tu `/trade-plan` → `/trade-wizard`
- Doi label tu "Lap ke hoach GD" → "Wizard Giao dich"

---

## Tong ket tien do

| Phase | Branch | Status | PR |
|-------|--------|--------|----|
| 1. Bug fixes | `fix/phase1-critical-bugs` | Da merge | PR #3, #4 |
| 2. Gop trang | `feature/phase2-ux-consolidation` | Da merge | PR #5 |
| 3. Tinh nang moi | `feature/phase3-new-features` | Da merge | PR #6 |
| 4. Bieu do & lien ket | `feature/phase4-charts-and-links` | Da push | Can tao PR |
