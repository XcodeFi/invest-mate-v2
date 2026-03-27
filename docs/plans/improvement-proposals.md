# Đề xuất cải tiến Investment Mate v2 — Dựa trên thực tế code

**Ngày lập:** 26/03/2026
**Phương pháp:** So sánh báo cáo đánh giá bên ngoài với code thực tế

---

## Context

Báo cáo đánh giá bên ngoài cho điểm 8.1/10 và đề xuất 6 cải tiến. Sau khi kiểm tra code thực tế, phát hiện:
- **2/6 sai thực tế**: Backtesting đã tồn tại (chỉ thiếu strategy DSL), Stress test có UI frontend nhưng beta hardcoded
- **1/6 đúng 50%**: Post-trade review có model + form nhưng thiếu workflow
- **3/6 đúng**: Push notification, Risk budgeting, Technical indicators

Plan này sắp xếp lại thứ tự ưu tiên dựa trên **giá trị thực / effort** và **infrastructure có sẵn**.

---

## Bảng so sánh: Báo cáo vs Thực tế

| # | Báo cáo đề xuất | Thực tế code | Kết luận |
|---|---| --- | --- |
| 1 | Chưa có backtesting lịch sử | Đã có đầy đủ: BacktestEngine, BacktestJob, UI 3 tab, 5 API, 65 tests. Chỉ thiếu strategy DSL (hiện buy-and-hold) | **Báo cáo sai** |
| 2 | Chưa có push notification | Đúng. Chỉ có in-app toast. AlertRule.Channel="Email" chưa implement. Worker không gọi AlertEvaluationService | **Báo cáo đúng** |
| 3 | Stress test chỉ dựa beta | Sai. Stress test có UI frontend nhưng beta **hardcoded** ~20 mã, không có backend. Thực tế tệ hơn báo cáo đánh giá | **Báo cáo sai** |
| 4 | Thiếu post-trade review | TradeJournal có PostTradeReview, LessonsLearned, Rating, Tags + UI form. Thiếu: workflow tự động, Plan vs Actual | **Đúng 50%** |
| 5 | Chưa có risk budgeting | Đúng. RiskProfile chỉ có position/sector/drawdown limits. Không có daily trade limits | **Báo cáo đúng** |
| 6 | Thiếu Bollinger, Ichimoku | Đúng. Chỉ có 5 indicator: EMA20/50, RSI14, MACD, Volume, S/R | **Báo cáo đúng** |

---

## Tổng quan đề xuất (xếp theo ưu tiên giá trị/effort)

| # | Đề xuất | Size | Trạng thái | Lý do ưu tiên |
|---|---------|------|:---:|---------------|
| **P0** | **Scenario Playbook (Trade Plan)** | **M** | **✅ Done** | **Decision tree + auto-evaluation + trailing stop** |
| P1 | Post-Trade Review Workflow | S-M | Pending | Model + API có sẵn, chỉ thêm workflow frontend |
| P2 | Stress Test — Dynamic Beta | M | Pending | UI có sẵn, chỉ thêm backend endpoint + thay hardcoded betas |
| P3 | Technical Indicators (Bollinger, ATR) | S-M | Pending | Architecture extensible, follow pattern có sẵn |
| P4 | Risk Budgeting (daily limits) | M | Pending | Mở rộng RiskProfile entity có sẵn |
| P5 | Push Notifications (Web Push) | L | Pending | Cần entity mới, service mới, SW integration |
| P6 | Backtesting Strategy Rules | L | Pending | DSL design phức tạp, buy-and-hold vẫn có giá trị |
| P7 | Symbol Timeline — Nhật ký trên Biểu đồ Giá | L-XL | Pending | Biến journal thành timeline trực quan gắn giá + cảm xúc + sự kiện |

---

## P1: Post-Trade Review Workflow (S-M)

**Vấn đề:** `TradeJournal` entity có `PostTradeReview`, `LessonsLearned`, `Rating`, `Tags` — nhưng không có workflow tự động. User phải tự navigate đến `/journals` và tự tạo. Trades list không link đến journal.

### Backend
1. **New query** `GetTradesWithoutJournalQuery` — lấy SELL trades chưa có journal
   - File: `src/InvestmentApp.Application/Journals/Queries/GetTradesPendingReview/GetTradesPendingReviewQuery.cs`
   - Logic: lấy trades (type=SELL) → cross-reference `ITradeJournalRepository.GetByTradeIdAsync()` → trả về trades chưa có journal
   - Test: `tests/InvestmentApp.Application.Tests/Journals/GetTradesPendingReviewQueryHandlerTests.cs`

2. **New endpoint** `GET /api/v1/journals/pending-review?portfolioId={id}`
   - File: `src/InvestmentApp.Api/Controllers/JournalsController.cs`

### Frontend
3. **Dashboard widget "Chờ đánh giá"** — hiển thị số SELL trades chưa có journal + link
   - File: `frontend/src/app/features/dashboard/dashboard.component.ts`

4. **Trades list — cột "Nhật ký"** — icon check/pencil, click mở journal
   - File: `frontend/src/app/features/trades/trades.component.ts`

5. **Plan vs Actual** — khi journal có TradePlanId, hiển thị so sánh planned vs actual entry/exit/R:R
   - File: `frontend/src/app/features/journals/journals.component.ts`

### TDD
- Test: handler trả về đúng trades SELL chưa có journal
- Test: handler trả về empty khi tất cả trades đã có journal
- Test: handler filter đúng theo portfolioId

---

## P2: Stress Test — Dynamic Beta (M)

**Vấn đề:** UI stress test hoạt động tại `risk-dashboard.component.ts:769-799` nhưng beta hardcoded cho ~20 mã. Default 1.0 cho mã không có trong list → kết quả sai.

### Backend
1. **New method** `CalculateStressTestAsync(portfolioId, marketChangePercent)` trong `IRiskCalculationService`
   - File: `src/InvestmentApp.Application/Common/Interfaces/IRiskCalculationService.cs` — thêm method + DTO `StressTestResult`
   - File: `src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs` — implement:
     - Lấy positions từ portfolio
     - Lấy beta từ `IComprehensiveStockDataProvider.GetComprehensiveDataAsync()` (field `Indicators.Beta`)
     - Fallback: tính beta từ correlation với VN-INDEX (reuse pattern của `CalculateCorrelationMatrixAsync`)
     - Fallback cuối: beta = 1.0
     - Tính impact = marketValue × (change% / 100) × beta
   - Test: `tests/InvestmentApp.Infrastructure.Tests/Services/RiskCalculationServiceStressTestTests.cs`

2. **New endpoint** `POST /api/v1/risk/portfolio/{id}/stress-test`
   - File: `src/InvestmentApp.Api/Controllers/RiskController.cs`
   - Body: `{ marketChangePercent: number }`
   - Returns: `StressTestResult { positions[], totalImpact, totalImpactPercent }`

### Frontend
3. **Replace hardcoded betas** — gọi API thay vì dùng `estimatedBetas` dictionary
   - File: `frontend/src/app/features/risk-dashboard/risk-dashboard.component.ts` — xóa `estimatedBetas`, gọi `riskService.stressTest()`
   - File: `frontend/src/app/core/services/risk.service.ts` — thêm `stressTest()` method

### TDD
- Test: impact đúng với beta đã biết
- Test: dùng beta từ API khi có, fallback tính từ price history khi không có
- Test: fallback beta = 1.0 khi không có dữ liệu
- Test: market change âm → impact âm cho beta > 0

---

## P3: Technical Indicators — Bollinger Bands + ATR (S-M)

**Vấn đề:** Chỉ có 5 indicator (EMA20/50, RSI14, MACD, Volume, S/R). Thiếu Bollinger Bands (volatility) và ATR (stop-loss sizing).

### Backend
1. **Bollinger Bands(20, 2)** — method `CalculateBollingerBands(closes, period, multiplier)`
   - File: `src/InvestmentApp.Infrastructure/Services/TechnicalIndicatorService.cs`
   - Returns: upper, middle (SMA20), lower, bandwidth, %B
   - Signal: squeeze (bandwidth thấp), breakout_up, breakout_down, neutral

2. **ATR(14)** — method `CalculateAtr(highs, lows, closes, period)`
   - File: `src/InvestmentApp.Infrastructure/Services/TechnicalIndicatorService.cs`
   - Returns: ATR value, ATR% (% of current price)
   - Cần fetch High/Low data (đã có trong `IMarketDataProvider` response)

3. **Extend TechnicalAnalysisResult** — thêm fields mới
   - File: `src/InvestmentApp.Application/Common/Interfaces/ITechnicalIndicatorService.cs`
   - Fields: `BollingerUpper`, `BollingerMiddle`, `BollingerLower`, `BollingerBandwidth`, `BollingerPercentB`, `BollingerSignal`, `Atr14`, `AtrPercent`

4. **Update signal scoring** — thêm Bollinger + ATR vào bullish/bearish count
   - File: `TechnicalIndicatorService.cs` lines 124-153

### Frontend
5. **Display mới** trong market-data component
   - File: `frontend/src/app/features/market-data/market-data.component.ts`
   - Thêm rows cho Bollinger Bands + ATR theo pattern hiện tại

### TDD
- Test: Bollinger middle = SMA(20) cho dữ liệu đã biết
- Test: upper = middle + 2×stddev, lower = middle - 2×stddev
- Test: ATR(14) đúng cho sample OHLC
- Test: signal "squeeze" khi bandwidth < threshold
- Test: trả về null khi dữ liệu không đủ

---

## P4: Risk Budgeting — Daily Trade Limits (M)

**Vấn đề:** `RiskProfile` có 5 fields (max position, sector, drawdown, R:R, portfolio risk) nhưng không giới hạn tần suất giao dịch.

### Backend
1. **Extend RiskProfile** — thêm fields nullable (backward compatible)
   - File: `src/InvestmentApp.Domain/Entities/RiskProfile.cs`
   - Fields: `MaxDailyTrades` (int?), `DailyLossLimitPercent` (decimal?), `MaxOpenPositions` (int?)
   - Update `Update()` method

2. **New method** `CheckRiskBudgetAsync(portfolioId)` trong `IRiskCalculationService`
   - File: `src/InvestmentApp.Application/Common/Interfaces/IRiskCalculationService.cs` — thêm DTO `RiskBudgetStatus`
   - File: `src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs`
   - Logic: đếm trades hôm nay, tính daily P&L, so với limits
   - Returns: `{ tradesToday, maxDaily, dailyPnL, dailyLossLimit, isLocked }`

3. **New endpoint** `GET /api/v1/risk/portfolio/{id}/budget`
   - File: `src/InvestmentApp.Api/Controllers/RiskController.cs`

### Frontend
4. **Risk budget card** — "Ngân sách rủi ro hôm nay" trên risk dashboard
   - File: `frontend/src/app/features/risk-dashboard/risk-dashboard.component.ts`
   - Hiển thị: trades used/limit, daily P&L vs limit, color status

5. **Risk profile form** — thêm fields mới
   - File: `frontend/src/app/features/risk-dashboard/risk-dashboard.component.ts` (form section)

### TDD
- Test: RiskProfile constructor chấp nhận nullable params, defaults null
- Test: `Update()` cập nhật đúng fields mới
- Test: budget trả về đúng trade count cho ngày hôm nay
- Test: isLocked = true khi daily loss vượt limit
- Test: isLocked = false khi limits null (unlimited)

---

## P5: Push Notifications — Web Push (L)

**Vấn đề:** Chỉ có in-app toast. `AlertEvaluationService` tồn tại nhưng Worker.cs **không gọi nó**. `AlertRule.Channel` có "Email" nhưng chưa implement.

### Backend
1. **Wire alerts vào Worker** — thêm `ProcessAlertsAsync()`
   - File: `src/InvestmentApp.Worker/Worker.cs`
   - Gọi `IAlertEvaluationService.EvaluateRulesAsync()` cho tất cả users
   - Tạo `AlertHistory` records cho triggered alerts

2. **New entity** `PushSubscription` — { UserId, Endpoint, P256dh, Auth }
   - File: `src/InvestmentApp.Domain/Entities/PushSubscription.cs`
   - File: `src/InvestmentApp.Application/Interfaces/IPushSubscriptionRepository.cs`

3. **Web Push sender** — dùng NuGet `WebPush`
   - File: `src/InvestmentApp.Infrastructure/Services/WebPushNotificationSender.cs`
   - VAPID keys trong `appsettings.json`

4. **New controller** — subscribe/unsubscribe endpoints
   - File: `src/InvestmentApp.Api/Controllers/NotificationsController.cs`
   - `POST /api/v1/notifications/subscribe`
   - `DELETE /api/v1/notifications/unsubscribe`

### Frontend
5. **Push service** — request permission, subscribe via Angular `SwPush`
   - File: `frontend/src/app/core/services/push-notification.service.ts`

6. **UI toggle** — "Nhận thông báo đẩy" trong alerts page
   - File: `frontend/src/app/features/alerts/` (component)

### TDD
- Test: `PushSubscription` entity validates required fields
- Test: Worker gọi alert evaluation và tạo history records
- Test: Push notification gửi khi alert triggers + subscription tồn tại
- Test: Không gửi khi user không có subscription

---

## P6: Backtesting Strategy Rules (L)

**Vấn đề:** `BacktestEngine` chỉ chạy buy-and-hold. Symbols hardcoded. Chưa có strategy rule evaluation.

### Backend (Phase 1: Preset templates, chưa cần DSL)
1. **Rule templates** — 4 templates thay vì full DSL
   - MA Crossover: mua khi EMA20 > EMA50, bán khi EMA20 < EMA50
   - RSI Bounce: mua khi RSI < 30, bán khi RSI > 70
   - MACD Signal: mua khi MACD cross-up, bán khi cross-down
   - Breakout: mua trên resistance, bán dưới support

2. **Extend Backtest entity** — thêm `Symbols` list, `RuleTemplate` field
   - File: `src/InvestmentApp.Domain/Entities/Backtest.cs`

3. **Rewrite BacktestEngine** — thay buy-and-hold bằng rule evaluation loop
   - File: `src/InvestmentApp.Infrastructure/Services/BacktestEngine.cs`
   - Reuse static methods từ `TechnicalIndicatorService`

### Frontend
4. **Rule template selector** — dropdown chọn template thay vì free-text
   - File: `frontend/src/app/features/backtesting/backtesting.component.ts`

### TDD
- Test: MA Crossover tạo trades tại crossover points
- Test: RSI Bounce mua tại oversold, bán tại overbought
- Test: symbols từ user input thay vì hardcoded
- Test: không tạo trades khi không có signal trong date range

---

## P7: Symbol Timeline — Nhật ký trên Biểu đồ Giá (L-XL)

**Tầm nhìn:** Biến nhật ký từ ghi chép rời rạc thành **dòng thời gian trực quan gắn liền với biểu đồ giá**. Trader nhìn lại được: tại thời điểm giá X, mình đã nghĩ gì, cảm xúc ra sao, quyết định gì — và kết quả thế nào. Mục tiêu cuối cùng: **cảm xúc không còn chi phối quyết định giao dịch**.

**Vấn đề hiện tại:**

- `TradeJournal` bắt buộc có `TradeId` — chỉ ghi được khi đã có giao dịch
- Không ghi được nhật ký khi đang theo dõi (watchlist), đang phân tích, hoặc đang nắm giữ mà chưa bán
- Không có biểu đồ giá candlestick — chỉ có Chart.js cho analytics (P&L, allocation)
- Nhật ký hiển thị dạng danh sách text, không gắn với dòng thời gian giá
- Không có event/news overlay, không thấy mối liên hệ giá ↔ tin tức ↔ cảm xúc

### Tổng quan kiến trúc

```
┌─────────────────────────────────────────────────────────────┐
│  VNM - Vinamilk                              [1M][3M][1Y]  │
│                                                             │
│  ════════════ Candlestick Price Chart ════════════════════  │
│  │                                                        │ │
│  │    📓          📓                📓                    │ │ ← Journal markers
│  │    ▼ Quan sát  ▼ Mua vào        ▼ Review              │ │
│  │                                                        │ │
│  │         ╔══════════════════════╗                        │ │ ← Holding period
│  │         ║  Nắm giữ 500 cp     ║─────────►             │ │    (highlighted zone)
│  │         ╚══════════════════════╝                        │ │
│  │    ▲ BUY 500       ▲ SELL 200        ▲ SELL 300       │ │ ← Trade markers
│  │    @ 72,000        @ 78,500          @ 81,000         │ │
│  │                                                        │ │
│  │         📰 KQKD Q1    📰 Cổ tức    ⚠️ Alert          │ │ ← Events layer
│  │                                                        │ │
│  │  ── Emotion ribbon ──────────────────────────────────  │ │
│  │  😰 Sợ hãi   😌 Bình tĩnh      😨 FOMO              │ │ ← Emotion timeline
│  │  ─────────────────────────────────────────────────────  │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  ── Timeline Detail (click marker để xem chi tiết) ──────  │
│                                                             │
│  [15/03/2024] 📓 Quan sát — "RSI oversold + test hỗ trợ"  │
│    Cảm xúc: Lo lắng (5/10) · Thị trường: VNI sideway      │
│                                                             │
│  [18/03/2024] 📓 Mua 500 VNM @ 72,000                      │
│    Cảm xúc: Tự tin (8/10) · Lý do: RSI bounce + hỗ trợ    │
│    Kế hoạch: TP 80k / SL 68k · R:R = 1:2                   │
│                                                             │
│  [02/04/2024] 📰 Vinamilk KQKD Q1 — doanh thu +12%        │
│                                                             │
│  [10/04/2024] 📓 Bán 200 VNM @ 78,500 (partial)            │
│    Cảm xúc: Tham lam → Bình tĩnh (7/10)                    │
│    Bài học: Chốt lời theo kế hoạch, không tham              │
│                                                             │
│  [25/04/2024] 📓 Review tổng kết — Rating: ⭐⭐⭐⭐          │
│    Bài học: Discipline tốt, entry đúng vùng hỗ trợ         │
│    Cải thiện: Nên scaling-in thay vì all-in                 │
└─────────────────────────────────────────────────────────────┘
```

### Phase 7A: Standalone Journal Entry + Candlestick Chart (M-L)

**Mục tiêu:** Ghi nhật ký bất kỳ lúc nào, gắn với symbol — không cần có giao dịch. Hiển thị trên biểu đồ giá candlestick.

#### 7A.1 — Domain: Entity `JournalEntry` mới (tách khỏi Trade dependency)

**File:** `src/InvestmentApp.Domain/Entities/JournalEntry.cs`

```csharp
public class JournalEntry : AggregateRoot
{
    // === Gắn kết linh hoạt ===
    public string UserId { get; private set; }           // required
    public string Symbol { get; private set; }           // required — mã CK
    public string? PortfolioId { get; private set; }     // optional
    public string? TradeId { get; private set; }         // optional — gắn trade cụ thể
    public string? TradePlanId { get; private set; }     // optional — gắn kế hoạch

    // === Loại entry ===
    public JournalEntryType EntryType { get; private set; }
    // Observation  — đang theo dõi, ghi nhận hiện tượng
    // PreTrade     — trước khi vào lệnh (phân tích, lý do)
    // DuringTrade  — đang nắm giữ (cập nhật, điều chỉnh)
    // PostTrade    — sau giao dịch (kết quả, bài học)
    // Review       — tổng kết định kỳ (tuần/tháng)

    // === Nội dung ===
    public string Title { get; private set; }            // tiêu đề ngắn
    public string Content { get; private set; }          // nội dung chi tiết (free-form)
    public string? MarketContext { get; private set; }   // bối cảnh thị trường

    // === Cảm xúc & tâm lý ===
    public string? EmotionalState { get; private set; }  // Tự tin/Sợ hãi/Tham lam/FOMO/Bình tĩnh/Lo lắng/Hào hứng
    public int? ConfidenceLevel { get; private set; }    // 1-10, nullable

    // === Snapshot giá tại thời điểm ghi ===
    public decimal? PriceAtTime { get; private set; }    // giá CK lúc ghi entry
    public decimal? VnIndexAtTime { get; private set; }  // VN-Index lúc ghi entry
    public DateTime Timestamp { get; private set; }      // thời điểm sự kiện (≠ CreatedAt)

    // === Meta ===
    public List<string> Tags { get; private set; }
    public int? Rating { get; private set; }             // 0-5 stars (cho Review type)
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}

public enum JournalEntryType
{
    Observation,    // Theo dõi, ghi nhận
    PreTrade,       // Trước giao dịch
    DuringTrade,    // Trong khi nắm giữ
    PostTrade,      // Sau giao dịch
    Review          // Tổng kết
}
```

**So sánh `TradeJournal` (hiện tại) vs `JournalEntry` (mới):**

| Khía cạnh | TradeJournal (giữ nguyên) | JournalEntry (mới) |
|---| --- | --- |
| TradeId | **Required** | Optional |
| Symbol | Qua Trade.Symbol (gián tiếp) | **Trực tiếp trên entity** |
| Ghi khi nào | Chỉ sau khi có Trade | **Bất kỳ lúc nào** |
| Loại | Cố định (pre/during/post) | **5 loại** linh hoạt |
| Snapshot giá | Không có | **PriceAtTime + VnIndexAtTime** |
| Timestamp | CreatedAt only | **Timestamp** riêng (có thể backdate) |

**Backward compatible:** Giữ nguyên `TradeJournal` + API `/journals` hiện tại. `JournalEntry` là entity song song, phục vụ Symbol Timeline. Khi feature ổn định, có thể migrate `TradeJournal` → `JournalEntry` (TradeId required → optional migration).

#### 7A.2 — Backend: Repository + CQRS + API

**Repository:**

- File: `src/InvestmentApp.Application/Common/Interfaces/IJournalEntryRepository.cs`
- File: `src/InvestmentApp.Infrastructure/Repositories/JournalEntryRepository.cs`
- MongoDB collection: `journal_entries`
- Indexes: `{ UserId, Symbol, Timestamp }`, `{ UserId, Symbol, IsDeleted }`, `{ TradeId }` (sparse)

**Commands:**

- `CreateJournalEntryCommand` — tạo entry, auto-fill PriceAtTime từ market data
- `UpdateJournalEntryCommand` — cập nhật nội dung
- `DeleteJournalEntryCommand` — soft delete

**Queries:**

- `GetJournalEntriesBySymbolQuery(userId, symbol, from?, to?)` — lấy entries theo symbol + date range
- `GetSymbolTimelineQuery(userId, symbol, from?, to?)` — **aggregation**: gom journal entries + trades + alerts thành timeline thống nhất

**API Endpoints:**

- `POST   /api/v1/journal-entries` — tạo entry
- `PUT    /api/v1/journal-entries/{id}` — cập nhật
- `DELETE /api/v1/journal-entries/{id}` — soft delete
- `GET    /api/v1/journal-entries?symbol={s}&from={d}&to={d}` — list by symbol
- `GET    /api/v1/symbols/{symbol}/timeline?from={d}&to={d}` — **unified timeline**

**Unified Timeline Response:**

```json
{
  "symbol": "VNM",
  "from": "2024-03-01",
  "to": "2024-04-30",
  "items": [
    {
      "type": "journal",
      "timestamp": "2024-03-15T09:30:00",
      "data": { /* JournalEntry */ }
    },
    {
      "type": "trade",
      "timestamp": "2024-03-18T10:00:00",
      "data": { "tradeType": "BUY", "quantity": 500, "price": 72000 }
    },
    {
      "type": "alert",
      "timestamp": "2024-04-01T14:00:00",
      "data": { "alertType": "PriceAlert", "message": "VNM vượt 77,000" }
    }
  ],
  "holdingPeriods": [
    {
      "startDate": "2024-03-18",
      "endDate": null,
      "startQuantity": 500,
      "currentQuantity": 300,
      "changes": [
        { "date": "2024-04-10", "type": "SELL", "quantity": 200, "remaining": 300 }
      ]
    }
  ]
}
```

#### 7A.3 — Frontend: Candlestick Chart với lightweight-charts

**Thêm dependency:**

```
npm install lightweight-charts
```

> **Lý do chọn lightweight-charts (TradingView open-source):**
>
> - Hỗ trợ candlestick chart native
> - Markers API: đặt icon/label tại timestamp cụ thể trên chart
> - Time range highlighting: tô vùng holding period
> - Customizable series: thêm emotion ribbon
> - Nhẹ (~45KB gzipped), không cần license
> - Chart.js không hỗ trợ candlestick + annotation overlay tốt

**New component: SymbolTimelineComponent**

- File: `frontend/src/app/features/symbol-timeline/symbol-timeline.component.ts`
- Route: `/symbol-timeline/:symbol`

**Chức năng:**

| Layer | Hiển thị | Data source |
|---| --- | --- |
| Candlestick chart | OHLCV giá theo ngày | `MarketDataService.getPriceHistory()` |
| Journal markers | 📓 icons trên chart tại timestamp | `JournalEntryService.getBySymbol()` |
| Trade markers | ▲ BUY / ▼ SELL với giá + quantity | Trades từ timeline API |
| Holding zones | Highlighted background vùng nắm giữ | `holdingPeriods` từ timeline API |
| Alert markers | ⚠️ icons khi alert triggered | `AlertHistory` từ timeline API |
| Timeline detail | Scrollable list bên dưới chart | Tất cả items sorted by timestamp |

**Interaction:**

- Click marker trên chart → scroll đến detail tương ứng bên dưới
- Click item trong timeline → highlight marker trên chart
- Hover marker → tooltip ngắn (title + emotion + confidence)
- Nút "Ghi nhật ký" → mở form tạo `JournalEntry` với symbol + PriceAtTime pre-filled
- Date range selector: 1M / 3M / 6M / 1Y / Custom

**Entry points (navigate đến Symbol Timeline):**

- Từ Watchlist: click symbol → "Xem timeline"
- Từ Positions (đang nắm giữ): click symbol → timeline
- Từ Trades list: click symbol → timeline
- Từ Journals: click symbol → timeline
- Search bar: nhập symbol → option "Timeline"

#### 7A.4 — Frontend: JournalEntry Service + Quick-add Form

**Service:**

- File: `frontend/src/app/core/services/journal-entry.service.ts`
- Methods: `create()`, `update()`, `delete()`, `getBySymbol()`, `getTimeline()`

**Quick-add form** (inline trên Symbol Timeline page):

- Symbol: pre-filled, readonly
- EntryType: dropdown (Quan sát / Trước GD / Đang GD / Sau GD / Tổng kết)
- Title: text input
- Content: textarea
- EmotionalState: dropdown 7 options
- ConfidenceLevel: slider 1-10
- MarketContext: textarea (optional)
- Tags: comma-separated input
- PriceAtTime: auto-filled từ current price, editable
- Timestamp: default now, cho phép chọn ngày khác (backdate)

#### 7A.5 — TDD

**Backend tests:**

- File: `tests/InvestmentApp.Domain.Tests/Entities/JournalEntryTests.cs`
  - Entity tạo đúng với Symbol required, TradeId optional
  - PriceAtTime snapshot chính xác
  - ConfidenceLevel clamp 1-10
  - Rating clamp 0-5
  - Soft delete set IsDeleted = true
  - EntryType enum validation

- File: `tests/InvestmentApp.Application.Tests/JournalEntries/CreateJournalEntryCommandHandlerTests.cs`
  - Tạo entry thành công với symbol + type
  - Auto-fill PriceAtTime từ market data service
  - Từ chối tạo khi symbol rỗng

- File: `tests/InvestmentApp.Application.Tests/JournalEntries/GetSymbolTimelineQueryHandlerTests.cs`
  - Timeline gom đúng journal entries + trades + alerts
  - Sorted by timestamp ascending
  - Filter đúng date range
  - HoldingPeriods tính đúng từ BUY/SELL trades

---

### Phase 7B: Event/News Overlay (M)

**Mục tiêu:** Hiển thị sự kiện công ty và tin tức trên biểu đồ giá, thấy mối liên hệ giá ↔ tin tức ↔ cảm xúc.

#### 7B.1 — Domain: Entity `MarketEvent`

**File:** `src/InvestmentApp.Domain/Entities/MarketEvent.cs`

```csharp
public class MarketEvent : AggregateRoot
{
    public string Symbol { get; private set; }          // "VNM" hoặc "VNINDEX" cho tin thị trường
    public MarketEventType EventType { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public string? Source { get; private set; }         // URL nguồn tin
    public DateTime EventDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public enum MarketEventType
{
    Earnings,        // KQKD — Kết quả kinh doanh
    Dividend,        // Cổ tức
    RightsIssue,     // Phát hành thêm
    ShareholderMtg,  // ĐHCĐ — Đại hội cổ đông
    InsiderTrade,    // Giao dịch nội bộ
    News,            // Tin tức chung
    Macro            // Tin vĩ mô (lãi suất, tỷ giá...)
}
```

#### 7B.2 — Backend: Event Ingestion

**Hai nguồn dữ liệu:**

1. **Auto-fetch** — Worker job fetch events từ market data provider
   - File: `src/InvestmentApp.Worker/Jobs/MarketEventFetchJob.cs`
   - Tần suất: 1 lần/ngày (sau giờ đóng cửa)
   - Parse từ `IMarketDataProvider` nếu API hỗ trợ, hoặc từ RSS feeds phổ biến (cafef, vietstock)

2. **Manual add** — User tự thêm event quan trọng
   - Endpoint: `POST /api/v1/market-events`
   - Use case: trader ghi lại sự kiện mà auto-fetch chưa có

**Query:**

- `GET /api/v1/market-events?symbol={s}&from={d}&to={d}`
- Kết quả merge vào `GetSymbolTimelineQuery` response

#### 7B.3 — Frontend: Event Markers trên Chart

- Markers dạng icon theo EventType: 📊 Earnings, 💰 Dividend, 📰 News, 🏦 Macro
- Hover: tooltip với title + source link
- Click: expand detail trong timeline list
- Toggle on/off từng loại event (filter checkboxes)

#### 7B.4 — TDD

- Test: MarketEvent entity validates required fields
- Test: Timeline merge events đúng thứ tự timestamp
- Test: Filter by event type hoạt động

---

### Phase 7C: Emotion Analytics & AI Review (M)

**Mục tiêu:** Tổng hợp dữ liệu cảm xúc theo thời gian, phát hiện pattern, AI coaching.

#### 7C.1 — Emotion Ribbon trên Chart

- Sub-chart bên dưới candlestick (giống volume bar)
- Mỗi journal entry tạo một data point: color = emotion, height = confidence level
- Color mapping: Tự tin=🟢, Bình tĩnh=🔵, Hào hứng=🟡, Lo lắng=🟠, Sợ hãi=🔴, Tham lam=🟣, FOMO=⚫
- Nhìn ribbon → thấy ngay pattern: "mỗi lần giá giảm mạnh → emotion chuyển đỏ → sau đó lại bán panic"

#### 7C.2 — Emotion Summary Panel

- Tổng hợp cho symbol hoặc toàn portfolio:
  - Distribution: pie chart cảm xúc (bao nhiêu % entries là FOMO, bao nhiêu % Bình tĩnh)
  - Correlation: cảm xúc nào thường dẫn đến trade lỗ, cảm xúc nào dẫn đến trade lãi
  - Trend: cảm xúc thay đổi thế nào theo thời gian (đang cải thiện hay xấu đi)

#### 7C.3 — AI Timeline Review

- Extend AI chat panel với use case `"timeline-review"`
- Context: gửi toàn bộ timeline (entries + trades + emotions + P&L) cho AI
- AI phân tích:
  - "Bạn hay ghi nhật ký với cảm xúc FOMO khi VNIndex tăng 3 phiên liên tiếp → 70% trades sau đó bị lỗ"
  - "Khi confidence level > 7 và emotion = Bình tĩnh, win rate của bạn là 78%"
  - "Bạn có xu hướng bán panic (emotion = Sợ hãi) trong 2 ngày đầu sau khi mua → nên đặt SL trước và không nhìn giá"
- Coaching suggestions dựa trên data thực tế của trader

#### 7C.4 — TDD

- Test: Emotion distribution tính đúng từ journal entries
- Test: Correlation P&L ↔ emotion đúng logic
- Test: AI context builder gom đúng timeline data

---

### Tổng hợp files cần tạo/sửa

**Backend — Tạo mới:**

| File | Mô tả |
| --- | --- |
| `Domain/Entities/JournalEntry.cs` | Entity mới, standalone journal |
| `Domain/Entities/MarketEvent.cs` | Entity sự kiện thị trường |
| `Application/Interfaces/IJournalEntryRepository.cs` | Repository interface |
| `Application/Interfaces/IMarketEventRepository.cs` | Repository interface |
| `Application/JournalEntries/Commands/Create..` | CQRS commands |
| `Application/JournalEntries/Commands/Update..` | CQRS commands |
| `Application/JournalEntries/Commands/Delete..` | CQRS commands |
| `Application/JournalEntries/Queries/GetBySymbol..` | Query by symbol |
| `Application/JournalEntries/Queries/GetSymbolTimeline..` | Unified timeline |
| `Infrastructure/Repositories/JournalEntryRepository.cs` | MongoDB implementation |
| `Infrastructure/Repositories/MarketEventRepository.cs` | MongoDB implementation |
| `Api/Controllers/JournalEntriesController.cs` | REST endpoints |
| `Api/Controllers/MarketEventsController.cs` | REST endpoints |
| `Worker/Jobs/MarketEventFetchJob.cs` | Auto-fetch events |

**Backend — Sửa:**

| File | Thay đổi |
| --- | --- |
| `Api/Program.cs` | Register DI cho services mới |
| `Application/DependencyInjection.cs` | Register MediatR handlers |

**Frontend — Tạo mới:**

| File | Mô tả |
| --- | --- |
| `features/symbol-timeline/symbol-timeline.component.ts` | Trang chính Symbol Timeline |
| `core/services/journal-entry.service.ts` | HTTP service cho JournalEntry |
| `core/services/market-event.service.ts` | HTTP service cho MarketEvent |

**Frontend — Sửa:**

| File | Thay đổi |
| --- | --- |
| `package.json` | Thêm `lightweight-charts` dependency |
| `app.routes.ts` | Thêm route `/symbol-timeline/:symbol` |
| `features/watchlist/watchlist.component.ts` | Thêm link "Timeline" per symbol |
| `features/positions/positions.component.ts` | Thêm link "Timeline" per symbol |
| `features/trades/trades.component.ts` | Thêm link "Timeline" per symbol |

---

### Dependencies & Risks

| Risk | Mitigation |
| --- | --- |
| lightweight-charts bundle size | ~45KB gzipped, acceptable. Lazy-load route |
| Market event data quality | Phase 7B có thể skip auto-fetch, chỉ manual add trước |
| TradeJournal migration | Giữ cả hai entity song song. Migrate sau khi JournalEntry ổn định |
| Price history API rate limit | Cache `StockPrice` collection đã có sẵn, reuse |
| AI context token limit | Giới hạn timeline window (3-6 tháng) khi gửi cho AI |

### Sub-phase implementation order

```
Phase 7A (M-L) — Foundation:
  7A.1  JournalEntry entity + enum           ← backend domain
  7A.2  Repository + CQRS + API              ← backend application/infra/api
  7A.3  Candlestick chart + markers          ← frontend, cần lightweight-charts
  7A.4  JournalEntry service + quick-add     ← frontend service + form
  7A.5  Tests                                ← TDD cho tất cả trên

Phase 7B (M) — Events layer:
  7B.1  MarketEvent entity                   ← backend domain
  7B.2  Event ingestion (manual + auto)      ← backend + worker
  7B.3  Event markers trên chart             ← frontend overlay
  7B.4  Tests

Phase 7C (M) — Insights:
  7C.1  Emotion ribbon trên chart            ← frontend sub-chart
  7C.2  Emotion summary panel                ← frontend analytics
  7C.3  AI timeline review                   ← AI integration
  7C.4  Tests
```

**Điều kiện tiên quyết:** P1 (Post-Trade Review Workflow) nên hoàn thành trước vì P7 mở rộng từ journal concept.

---

## Implementation Order

```text
Phase A (song song — ưu tiên cao, effort thấp):
  [P1] Post-Trade Review Workflow    ← model có sẵn, frontend-heavy
  [P2] Stress Test Dynamic Beta      ← UI có sẵn, backend-heavy
  [P3] Bollinger Bands + ATR         ← pattern có sẵn, self-contained

Phase B (ưu tiên trung bình):
  [P4] Risk Budgeting                ← extend RiskProfile + RiskCalcService

Phase C (ưu tiên trung bình, effort cao):
  [P5] Push Notifications            ← new entity + service + SW integration

Phase D (ưu tiên thấp, effort cao):
  [P6] Backtesting Strategy Rules    ← benefits from P3 new indicators

Phase E (sau P1 — effort cao, giá trị chiến lược):
  [P7] Symbol Timeline               ← 3 sub-phases: 7A Foundation → 7B Events → 7C Insights
       Phụ thuộc: P1 (journal workflow) nên xong trước
       Khuyến nghị: P3 (indicators) xong trước giúp chart phong phú hơn
```

## Verification (sau mỗi feature)

- `dotnet test` — all backend tests pass
- `ng test` — all frontend tests pass
- `ng build` — build thành công
- Update docs: `features.md`, `architecture.md`, `business-domain.md`, `CHANGELOG.md`
- Manual test trên browser: verify UI hoạt động đúng
