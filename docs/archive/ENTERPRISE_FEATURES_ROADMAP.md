# Investment Mate v2 - Enterprise Features Roadmap

> Tài liệu phát triển chi tiết, đưa Investment Mate lên mức **Semi-Quant Trading Platform**.
> **Last updated:** March 6, 2026 | Branch: `feat/phase4-enterprise`
> Legend: ✅ Implemented | 🚧 In Progress | 📋 Planned

## Mục lục

### Phase 1-3 — Completed ✅

1. [Risk Management](#1-risk-management) ✅
2. [Advanced Analytics](#2-advanced-analytics) ✅
3. [Strategy Management](#3-strategy-management) ✅
4. [Market Data Integration](#5-market-data-integration) ✅
5. [Journal & Trade Notes](#6-journal--trade-notes) ✅
6. [Smart Alert System](#8-smart-alert-system) ✅
7. [Capital Flow Tracking](#10-capital-flow-tracking) ✅
8. [Snapshot & Time Travel Data](#11-snapshot--time-travel-data) ✅

### Phase 4 — Completed ✅

1. [Backtesting Engine](#4-backtesting-engine) ✅
2. [Multi-Currency Support](#7-multi-currency-support) ✅
3. [Price Feed Worker](#12-price-feed-worker) ✅
4. [Health Endpoint & Monitoring](#13-health-endpoint--monitoring) ✅

### Phase 5 — Planned 📋

1. [Multi-User / SaaS Features](#9-multi-user--saas-features) 📋

---

## 1. Risk Management

> **Priority: 🔴 Critical** | Tính năng quan trọng nhất để chuyên nghiệp hóa platform.

### 1.1 Position Risk Metrics

#### Domain Entities & Value Objects

```csharp
// Value Object
public class PositionRisk : IEquatable<PositionRisk>
{
    public decimal PositionSizePercent { get; }    // % vốn dành cho vị thế
    public decimal RiskPerTrade { get; }           // Số tiền rủi ro / trade
    public decimal RiskRewardRatio { get; }        // R:R ratio
    public decimal MaxSectorExposure { get; }      // Giới hạn phơi nhiễm ngành
}
```

#### Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Entity | `Domain/Entities/RiskProfile.cs` | Risk settings per portfolio |
| Value Object | `Domain/ValueObjects/PositionRisk.cs` | Position risk calculations |
| Service | `Infrastructure/Services/RiskCalculationService.cs` | Core risk computation |
| Command | `Application/Risk/Commands/SetRiskProfile/` | Set risk parameters |
| Query | `Application/Risk/Queries/GetPositionRisk/` | Get position risk metrics |
| Controller | `Api/Controllers/RiskController.cs` | REST endpoints |

#### API Endpoints

```
GET    /api/v1/risk/portfolio/{portfolioId}/positions     → Position risk metrics
GET    /api/v1/risk/portfolio/{portfolioId}/summary       → Portfolio risk summary
POST   /api/v1/risk/portfolio/{portfolioId}/profile       → Set risk profile
PUT    /api/v1/risk/portfolio/{portfolioId}/profile       → Update risk profile
```

#### Calculations

```
Position Size % = (Position Value / Total Portfolio Value) × 100
Risk Per Trade = Position Size × (Entry Price - Stop Loss) / Entry Price
Risk/Reward = (Target Price - Entry Price) / (Entry Price - Stop Loss)
Max Sector Exposure = Σ(Position Values in Sector) / Total Portfolio Value
```

### 1.2 Portfolio Risk Metrics

#### Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Service | `Infrastructure/Services/PortfolioRiskService.cs` | Portfolio-level risk |
| Query | `Application/Risk/Queries/GetMaxDrawdown/` | Max drawdown calculation |
| Query | `Application/Risk/Queries/GetValueAtRisk/` | VaR calculation |
| Query | `Application/Risk/Queries/GetCorrelation/` | Symbol correlation matrix |

#### Calculations

```
Max Drawdown = (Peak Value - Trough Value) / Peak Value × 100
VaR (95%) = μ - 1.645σ (Historical method hoặc parametric)
Beta = Cov(Rp, Rm) / Var(Rm)    // Rp = portfolio return, Rm = market return
Correlation = Cov(Ri, Rj) / (σi × σj)
```

#### MongoDB Collections

```json
// risk_profiles collection
{
  "_id": "uuid",
  "portfolioId": "uuid",
  "maxPositionSizePercent": 20,
  "maxSectorExposurePercent": 40,
  "maxDrawdownAlertPercent": 10,
  "defaultRiskRewardRatio": 2.0,
  "createdAt": "2026-03-01T00:00:00Z",
  "updatedAt": "2026-03-01T00:00:00Z"
}
```

### 1.3 Stop-Loss & Target Tracking

#### Domain Entity

```csharp
public class StopLossTarget : Entity
{
    public string TradeId { get; private set; }
    public decimal StopLossPrice { get; private set; }
    public decimal TargetPrice { get; private set; }
    public decimal? TrailingStopPercent { get; private set; }
    public decimal? TrailingStopPrice { get; private set; }
    public bool IsStopLossTriggered { get; private set; }
    public bool IsTargetTriggered { get; private set; }
    public DateTime? TriggeredAt { get; private set; }
}
```

#### API Endpoints

```
POST   /api/v1/trades/{tradeId}/stop-loss       → Set stop-loss
PUT    /api/v1/trades/{tradeId}/stop-loss        → Update stop-loss
POST   /api/v1/trades/{tradeId}/target           → Set target price
PUT    /api/v1/trades/{tradeId}/target           → Update target
GET    /api/v1/trades/{tradeId}/sl-tp            → Get SL/TP status
GET    /api/v1/risk/alerts/near-sl               → Trades near stop-loss
GET    /api/v1/risk/alerts/near-tp               → Trades near target
```

#### Frontend Components

```
frontend/src/app/features/risk/
├── risk.component.ts                    // Risk dashboard
├── risk-profile/
│   └── risk-profile.component.ts        // Risk settings form
├── position-risk/
│   └── position-risk.component.ts       // Position risk table
├── portfolio-risk/
│   └── portfolio-risk.component.ts      // Portfolio risk metrics
├── drawdown-chart/
│   └── drawdown-chart.component.ts      // Drawdown visualization
├── correlation-matrix/
│   └── correlation-matrix.component.ts  // Heatmap correlation
└── stop-loss-tracker/
    └── stop-loss-tracker.component.ts   // SL/TP monitoring
```

---

## 2. Advanced Analytics

> **Priority: 🔴 Critical** | Quant-level performance metrics.

### 2.1 Performance Metrics

#### Service Interface

```csharp
public interface IPerformanceMetricsService
{
    Task<decimal> CalculateCAGR(string portfolioId, DateTime startDate, DateTime endDate);
    Task<decimal> CalculateSharpeRatio(string portfolioId, decimal riskFreeRate = 0.05m);
    Task<decimal> CalculateSortinoRatio(string portfolioId, decimal riskFreeRate = 0.05m);
    Task<decimal> CalculateWinRate(string portfolioId);
    Task<decimal> CalculateProfitFactor(string portfolioId);
    Task<decimal> CalculateExpectancy(string portfolioId);
    Task<PerformanceSummary> GetFullPerformanceSummary(string portfolioId);
}
```

#### Calculations

```
CAGR = (End Value / Start Value)^(1/Years) - 1
Sharpe Ratio = (Rp - Rf) / σp
Sortino Ratio = (Rp - Rf) / σd          // σd = downside deviation only
Win Rate = Winning Trades / Total Trades × 100
Profit Factor = Gross Profit / Gross Loss
Expectancy = (Win% × Avg Win) - (Loss% × Avg Loss)
```

#### API Endpoints

```
GET    /api/v1/analytics/portfolio/{id}/performance   → Full performance metrics
GET    /api/v1/analytics/portfolio/{id}/cagr           → CAGR
GET    /api/v1/analytics/portfolio/{id}/sharpe          → Sharpe Ratio
GET    /api/v1/analytics/portfolio/{id}/sortino         → Sortino Ratio
GET    /api/v1/analytics/portfolio/{id}/win-rate        → Win Rate
GET    /api/v1/analytics/portfolio/{id}/equity-curve    → Equity curve data
```

### 2.2 Equity Curve

#### Data Model

```csharp
public class EquityCurvePoint
{
    public DateTime Date { get; set; }
    public decimal PortfolioValue { get; set; }
    public decimal BenchmarkValue { get; set; }    // VNINDEX
    public decimal DailyReturn { get; set; }
    public decimal CumulativeReturn { get; set; }
}
```

#### MongoDB Collection

```json
// equity_snapshots collection
{
  "_id": "uuid",
  "portfolioId": "uuid",
  "date": "2026-03-01",
  "totalValue": 150000000,
  "cashBalance": 20000000,
  "investedValue": 130000000,
  "dailyReturn": 0.015,
  "cumulativeReturn": 0.25,
  "benchmarkValue": 1250.5,    // VNINDEX
  "positions": [
    { "symbol": "VNM", "quantity": 100, "marketPrice": 80000, "value": 8000000 }
  ]
}
```

#### Frontend Components

```
frontend/src/app/features/analytics/
├── equity-curve/
│   └── equity-curve.component.ts        // Line chart equity vs benchmark
├── performance-metrics/
│   └── performance-metrics.component.ts // Metric cards
├── return-distribution/
│   └── return-distribution.component.ts // Histogram daily returns
└── monthly-returns/
    └── monthly-returns.component.ts     // Heatmap monthly returns
```

---

## 3. Strategy Management

> **Priority: 🟡 Medium** | Phân loại và đánh giá performance theo chiến lược.

### 3.1 Strategy Entity

```csharp
public class Strategy : AggregateRoot
{
    public string UserId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string EntryRules { get; private set; }     // Markdown format
    public string ExitRules { get; private set; }
    public string RiskRules { get; private set; }
    public string TimeFrame { get; private set; }      // Scalping, Swing, Position
    public string MarketCondition { get; private set; } // Trending, Ranging, Volatile
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    // Navigation
    public IReadOnlyCollection<Trade> Trades => _trades.AsReadOnly();
}
```

### 3.2 Trade-Strategy Link

```csharp
// Extend Trade entity
public class Trade : Entity
{
    // ... existing properties
    public string? StrategyId { get; private set; }
    
    public void LinkStrategy(string strategyId)
    {
        StrategyId = strategyId;
        AddDomainEvent(new TradeLinkedToStrategyEvent(Id, strategyId));
    }
}
```

### 3.3 Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Entity | `Domain/Entities/Strategy.cs` | Strategy aggregate root |
| Repository | `Infrastructure/Repositories/StrategyRepository.cs` | Data access |
| Command | `Application/Strategies/Commands/CreateStrategy/` | Create strategy |
| Command | `Application/Strategies/Commands/UpdateStrategy/` | Update strategy |
| Command | `Application/Strategies/Commands/LinkTrade/` | Link trade to strategy |
| Query | `Application/Strategies/Queries/GetStrategy/` | Get strategy details |
| Query | `Application/Strategies/Queries/GetStrategyPerformance/` | Strategy P&L |
| Controller | `Api/Controllers/StrategiesController.cs` | REST endpoints |

#### API Endpoints

```
GET    /api/v1/strategies                              → List all strategies
GET    /api/v1/strategies/{id}                         → Get strategy detail
POST   /api/v1/strategies                              → Create strategy
PUT    /api/v1/strategies/{id}                         → Update strategy
DELETE /api/v1/strategies/{id}                         → Delete strategy
POST   /api/v1/strategies/{id}/trades/{tradeId}       → Link trade
DELETE /api/v1/strategies/{id}/trades/{tradeId}       → Unlink trade
GET    /api/v1/strategies/{id}/performance             → Strategy performance
GET    /api/v1/strategies/compare                      → Compare strategies
```

#### MongoDB Collection

```json
// strategies collection
{
  "_id": "uuid",
  "userId": "uuid",
  "name": "Breakout Trading",
  "description": "Trade breakout from consolidation zones",
  "entryRules": "- Price breaks above resistance\n- Volume > 150% avg",
  "exitRules": "- Target: 2R\n- Time stop: 5 days",
  "riskRules": "- Max 5% capital per trade\n- Stop loss below breakout candle",
  "timeFrame": "Swing",
  "marketCondition": "Trending",
  "isActive": true,
  "isDeleted": false,
  "createdAt": "2026-03-01T00:00:00Z"
}
```

#### Frontend Components

```
frontend/src/app/features/strategies/
├── strategies.component.ts              // Strategy list
├── strategy-create/
│   └── strategy-create.component.ts     // Create/edit form
├── strategy-detail/
│   └── strategy-detail.component.ts     // Detail + linked trades
├── strategy-performance/
│   └── strategy-performance.component.ts // Performance metrics
└── strategy-compare/
    └── strategy-compare.component.ts    // Side-by-side comparison
```

---

## 4. Backtesting Engine

> **Priority: 🟢 Low (Enterprise)** | Nâng lên mức semi-quant platform.

### 4.1 Architecture

```
┌─────────────────────────────────────────────────┐
│                 Backtesting Engine               │
├─────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────────┐  │
│  │ Data     │  │ Strategy │  │ Execution    │  │
│  │ Provider │→ │ Engine   │→ │ Simulator    │  │
│  └──────────┘  └──────────┘  └──────────────┘  │
│       ↓              ↓              ↓           │
│  ┌──────────────────────────────────────────┐   │
│  │           Results Analyzer               │   │
│  │  • Equity Curve  • Drawdown  • Metrics   │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
```

### 4.2 Domain Models

```csharp
public class Backtest : AggregateRoot
{
    public string UserId { get; private set; }
    public string StrategyId { get; private set; }
    public string Name { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public decimal InitialCapital { get; private set; }
    public BacktestStatus Status { get; private set; }   // Pending, Running, Completed, Failed
    public BacktestResult? Result { get; private set; }
    public List<SimulatedTrade> SimulatedTrades { get; private set; }
}

public class SimulatedTrade
{
    public string Symbol { get; set; }
    public TradeType Type { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Quantity { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime ExitDate { get; set; }
    public decimal PnL { get; set; }
    public decimal ReturnPercent { get; set; }
}

public class BacktestResult
{
    public decimal FinalValue { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal CAGR { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public int TotalTrades { get; set; }
    public List<EquityCurvePoint> EquityCurve { get; set; }
}
```

### 4.3 Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Entity | `Domain/Entities/Backtest.cs` | Backtest aggregate |
| Service | `Infrastructure/Services/BacktestEngine.cs` | Core engine |
| Service | `Infrastructure/Services/HistoricalDataService.cs` | Price data provider |
| Worker | `Worker/Jobs/BacktestJob.cs` | Background processing |
| Command | `Application/Backtests/Commands/RunBacktest/` | Start backtest |
| Query | `Application/Backtests/Queries/GetResult/` | Get results |
| Controller | `Api/Controllers/BacktestsController.cs` | REST endpoints |

#### API Endpoints

```
POST   /api/v1/backtests                         → Run new backtest
GET    /api/v1/backtests                         → List backtests
GET    /api/v1/backtests/{id}                    → Get backtest result
GET    /api/v1/backtests/{id}/equity-curve       → Equity curve data
GET    /api/v1/backtests/{id}/trades             → Simulated trades
DELETE /api/v1/backtests/{id}                    → Delete backtest
POST   /api/v1/backtests/compare                 → Compare multiple backtests
```

---

## 5. Market Data Integration

> **Priority: 🔴 Critical** | Nền tảng cho tất cả tính năng khác.

### 5.1 Price Service Architecture

```csharp
public interface IMarketDataProvider
{
    Task<StockPrice> GetCurrentPrice(string symbol);
    Task<List<StockPrice>> GetHistoricalPrices(string symbol, DateTime from, DateTime to);
    Task<List<StockPrice>> GetBatchPrices(IEnumerable<string> symbols);
    Task<MarketIndex> GetIndexData(string indexSymbol);  // VNINDEX
}

// Multiple provider support
public class SSIMarketDataProvider : IMarketDataProvider { }
public class FiinTradeMarketDataProvider : IMarketDataProvider { }
public class FallbackMarketDataProvider : IMarketDataProvider { }
```

### 5.2 Price Snapshot Job (Worker)

```csharp
// Worker/Jobs/PriceSnapshotJob.cs
public class PriceSnapshotJob : BackgroundService
{
    // Runs every 15 minutes during market hours (9:00 - 15:00 VN time)
    // 1. Fetch all unique symbols from active portfolios
    // 2. Batch fetch current prices
    // 3. Save to price_snapshots collection
    // 4. Update unrealized PnL for affected portfolios
    // 5. Check stop-loss/target triggers
    // 6. Send alerts if needed
}
```

### 5.3 MongoDB Collections

```json
// stock_prices collection (historical)
{
  "_id": "uuid",
  "symbol": "VNM",
  "date": "2026-03-01",
  "open": 78000,
  "high": 82000,
  "low": 77500,
  "close": 80000,
  "volume": 1500000,
  "source": "SSI",
  "fetchedAt": "2026-03-01T15:00:00Z"
}

// market_indices collection
{
  "_id": "uuid",
  "indexSymbol": "VNINDEX",
  "date": "2026-03-01",
  "open": 1240.5,
  "high": 1255.0,
  "low": 1238.0,
  "close": 1250.5,
  "volume": 850000000,
  "change": 10.5,
  "changePercent": 0.85
}
```

### 5.4 Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Interface | `Application/Common/Interfaces/IMarketDataProvider.cs` | Provider interface |
| Service | `Infrastructure/Services/SSIMarketDataProvider.cs` | SSI API integration |
| Service | `Infrastructure/Services/FiinTradeProvider.cs` | FiinTrade integration |
| Service | `Infrastructure/Services/MarketDataAggregator.cs` | Multi-source aggregation |
| Worker Job | `Worker/Jobs/PriceSnapshotJob.cs` | Scheduled price updates |
| Worker Job | `Worker/Jobs/MarketIndexJob.cs` | Index data sync |
| Repository | `Infrastructure/Repositories/StockPriceRepository.cs` | Price data access |
| Controller | `Api/Controllers/MarketDataController.cs` | REST endpoints |

#### API Endpoints

```
GET    /api/v1/market/price/{symbol}                → Current price
GET    /api/v1/market/price/{symbol}/history         → Historical prices
GET    /api/v1/market/prices?symbols=VNM,FPT,VCB    → Batch prices
GET    /api/v1/market/index/{symbol}                 → Market index data
GET    /api/v1/market/index/{symbol}/history         → Index history
```

---

## 6. Journal & Trade Notes

> **Priority: 🟡 Medium** | Yếu tố quan trọng cho trader chuyên nghiệp.

### 6.1 Trade Journal Entity

```csharp
public class TradeJournal : Entity
{
    public string TradeId { get; private set; }
    public string UserId { get; private set; }
    
    // Pre-trade
    public string EntryReason { get; private set; }      // Lý do vào lệnh
    public string MarketContext { get; private set; }     // Bối cảnh thị trường
    public string TechnicalSetup { get; private set; }    // Setup kỹ thuật
    
    // During trade
    public string EmotionalState { get; private set; }    // Tâm lý
    public int ConfidenceLevel { get; private set; }      // 1-10
    
    // Post-trade
    public string PostTradeReview { get; private set; }   // Đánh giá sau
    public string LessonsLearned { get; private set; }    // Bài học
    public int Rating { get; private set; }               // 1-5 stars
    
    // Attachments
    public List<JournalAttachment> Attachments { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}

public class JournalAttachment
{
    public string FileName { get; set; }
    public string FileUrl { get; set; }          // Cloud storage URL
    public string ContentType { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
}
```

### 6.2 Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Entity | `Domain/Entities/TradeJournal.cs` | Journal entity |
| Repository | `Infrastructure/Repositories/JournalRepository.cs` | Data access |
| Service | `Infrastructure/Services/FileStorageService.cs` | Image upload |
| Command | `Application/Journals/Commands/CreateJournal/` | Create journal |
| Command | `Application/Journals/Commands/UploadAttachment/` | Upload image |
| Query | `Application/Journals/Queries/GetJournal/` | Get journal |
| Query | `Application/Journals/Queries/GetJournalsByTrade/` | Journals by trade |
| Controller | `Api/Controllers/JournalsController.cs` | REST endpoints |

#### API Endpoints

```
POST   /api/v1/journals                          → Create journal entry
GET    /api/v1/journals                          → List journal entries
GET    /api/v1/journals/{id}                     → Get journal detail
PUT    /api/v1/journals/{id}                     → Update journal
DELETE /api/v1/journals/{id}                     → Delete journal
POST   /api/v1/journals/{id}/attachments         → Upload image
DELETE /api/v1/journals/{id}/attachments/{fileId} → Remove image
GET    /api/v1/trades/{tradeId}/journal          → Get journal for trade
```

#### MongoDB Collection

```json
// trade_journals collection
{
  "_id": "uuid",
  "tradeId": "uuid",
  "userId": "uuid",
  "entryReason": "Breakout from ascending triangle, volume spike",
  "marketContext": "VNINDEX uptrend, sector rotation into banking",
  "technicalSetup": "Price above MA20, RSI 55, MACD bullish crossover",
  "emotionalState": "Confident, no FOMO",
  "confidenceLevel": 8,
  "postTradeReview": "Good entry, held too long on exit",
  "lessonsLearned": "Set trailing stop after 1R profit",
  "rating": 4,
  "attachments": [
    {
      "fileName": "chart-vn30-breakout.png",
      "fileUrl": "https://storage.example.com/journals/...",
      "contentType": "image/png",
      "fileSize": 245760,
      "description": "VN30 breakout chart setup"
    }
  ],
  "createdAt": "2026-03-01T00:00:00Z",
  "updatedAt": "2026-03-01T00:00:00Z"
}
```

#### Frontend Components

```
frontend/src/app/features/journal/
├── journal.component.ts                 // Journal list/timeline
├── journal-create/
│   └── journal-create.component.ts      // Create/edit form
├── journal-detail/
│   └── journal-detail.component.ts      // Full journal view
├── journal-stats/
│   └── journal-stats.component.ts       // Emotion/confidence analytics
└── journal-calendar/
    └── journal-calendar.component.ts    // Calendar view
```

---

## 7. Multi-Currency Support

> **Priority: 🟢 Low** | Cần khi mở rộng sang US stocks, Crypto, ETFs.

### 7.1 Currency Service

```csharp
public interface ICurrencyService
{
    Task<decimal> GetExchangeRate(string fromCurrency, string toCurrency);
    Task<Money> Convert(Money amount, string targetCurrency);
    Task<Dictionary<string, decimal>> GetAllRates(string baseCurrency);
}

// Extended Money value object
public class Money : IEquatable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }     // VND, USD, USDT, EUR
    
    public Money ConvertTo(string targetCurrency, decimal rate)
    {
        return new Money(Amount * rate, targetCurrency);
    }
}
```

### 7.2 Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Service | `Infrastructure/Services/CurrencyService.cs` | Exchange rate service |
| Worker Job | `Worker/Jobs/ExchangeRateJob.cs` | Daily rate sync |
| Value Object | `Domain/ValueObjects/Money.cs` | Extended Money with currency |

#### API Endpoints

```
GET    /api/v1/currency/rates                    → All exchange rates
GET    /api/v1/currency/convert?from=USD&to=VND&amount=100 → Convert
```

#### MongoDB Collection

```json
// exchange_rates collection
{
  "_id": "uuid",
  "baseCurrency": "USD",
  "targetCurrency": "VND",
  "rate": 25450,
  "date": "2026-03-01",
  "source": "exchangeratesapi",
  "updatedAt": "2026-03-01T08:00:00Z"
}
```

---

## 8. Smart Alert System

> **Priority: 🟡 Medium** | Rule-based alerts và digest reports.

### 8.1 Alert Rules Engine

```csharp
public class AlertRule : Entity
{
    public string UserId { get; private set; }
    public string Name { get; private set; }
    public AlertType Type { get; private set; }
    public AlertCondition Condition { get; private set; }
    public decimal Threshold { get; private set; }
    public AlertChannel Channel { get; private set; }    // InApp, Email, SMS
    public bool IsActive { get; private set; }
    public DateTime? LastTriggeredAt { get; private set; }
}

public enum AlertType
{
    PortfolioDrawdown,          // Portfolio drawdown > X%
    PositionSizeExceeded,       // Single position > X% capital
    ConsecutiveLosses,          // N consecutive losing trades
    PriceAlert,                 // Price above/below threshold
    StopLossNear,               // Price within X% of stop-loss
    TargetNear,                 // Price within X% of target
    DailyPnLThreshold,         // Daily P&L exceeds ±X%
    VolumeSpike                 // Volume > X× average
}

public enum AlertChannel
{
    InApp,
    Email,
    SMS,
    Push
}
```

### 8.2 Digest Reports

```csharp
public class DigestReport
{
    public string UserId { get; set; }
    public DigestType Type { get; set; }          // Daily, Weekly
    public DateTime GeneratedAt { get; set; }
    
    // Content
    public decimal PortfolioValue { get; set; }
    public decimal DailyPnL { get; set; }
    public decimal WeeklyPnL { get; set; }
    public List<PositionAlert> Alerts { get; set; }
    public List<TopMover> TopMovers { get; set; }
    public string MarketSummary { get; set; }
}
```

### 8.3 Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Entity | `Domain/Entities/AlertRule.cs` | Alert rule entity |
| Entity | `Domain/Entities/AlertHistory.cs` | Alert log |
| Service | `Infrastructure/Services/AlertEngine.cs` | Rule evaluation engine |
| Service | `Infrastructure/Services/DigestReportService.cs` | Report generation |
| Service | `Infrastructure/Services/EmailService.cs` | Email sending |
| Worker Job | `Worker/Jobs/AlertCheckJob.cs` | Periodic alert check |
| Worker Job | `Worker/Jobs/DailyDigestJob.cs` | Daily report 9h sáng |
| Worker Job | `Worker/Jobs/WeeklyDigestJob.cs` | Weekly summary |
| Controller | `Api/Controllers/AlertsController.cs` | REST endpoints |

#### API Endpoints

```
GET    /api/v1/alerts/rules                      → List alert rules
POST   /api/v1/alerts/rules                      → Create alert rule
PUT    /api/v1/alerts/rules/{id}                 → Update rule
DELETE /api/v1/alerts/rules/{id}                 → Delete rule
GET    /api/v1/alerts/history                    → Alert history
GET    /api/v1/alerts/digest/daily               → Today's digest
GET    /api/v1/alerts/digest/weekly              → Weekly digest
PUT    /api/v1/alerts/{id}/read                  → Mark as read
```

#### Frontend Components

```
frontend/src/app/features/alerts/
├── alerts.component.ts                  // Alert center
├── alert-rules/
│   └── alert-rules.component.ts         // Manage rules
├── alert-rule-create/
│   └── alert-rule-create.component.ts   // Create/edit rule
├── alert-history/
│   └── alert-history.component.ts       // Alert log
└── digest-report/
    └── digest-report.component.ts       // Digest view
```

---

## 9. Multi-User / SaaS Features

> **Priority: 🟢 Low** | Khi mở rộng thành SaaS platform.

### 9.1 Organization & Team

```csharp
public class Organization : AggregateRoot
{
    public string Name { get; private set; }
    public string OwnerId { get; private set; }
    public SubscriptionPlan Plan { get; private set; }
    public List<OrganizationMember> Members { get; private set; }
}

public class OrganizationMember
{
    public string UserId { get; set; }
    public MemberRole Role { get; set; }       // Owner, Admin, Manager, Viewer
    public List<string> PortfolioAccess { get; set; }  // Shared portfolio IDs
    public DateTime JoinedAt { get; set; }
}

public enum MemberRole
{
    Owner,      // Full access + billing
    Admin,      // Full access except billing
    Manager,    // Can trade + manage portfolios
    Viewer      // Read-only access
}

public enum SubscriptionPlan
{
    Free,           // 1 portfolio, 50 trades/month
    Professional,   // 10 portfolios, unlimited trades
    Enterprise      // Unlimited + team features
}
```

### 9.2 Admin Panel

#### API Endpoints

```
# Organization
POST   /api/v1/organizations                    → Create org
GET    /api/v1/organizations/{id}               → Get org
PUT    /api/v1/organizations/{id}               → Update org
POST   /api/v1/organizations/{id}/members       → Add member
DELETE /api/v1/organizations/{id}/members/{uid}  → Remove member
PUT    /api/v1/organizations/{id}/members/{uid}  → Update role

# Admin (super admin only)
GET    /api/v1/admin/users                       → List all users
GET    /api/v1/admin/users/{id}                  → User detail
PUT    /api/v1/admin/users/{id}/status           → Enable/disable user
GET    /api/v1/admin/stats                       → Platform stats
GET    /api/v1/admin/subscriptions               → Subscription overview
```

#### Frontend Components

```
frontend/src/app/features/admin/
├── admin-dashboard/
│   └── admin-dashboard.component.ts     // Admin overview
├── user-management/
│   └── user-management.component.ts     // User list + actions
├── organization-management/
│   └── org-management.component.ts      // Org management
└── subscription-management/
    └── subscription.component.ts        // Plans + billing

frontend/src/app/features/team/
├── team.component.ts                    // Team overview
├── team-members/
│   └── team-members.component.ts        // Member management
└── shared-portfolios/
    └── shared-portfolios.component.ts   // Shared portfolio access
```

---

## 10. Capital Flow Tracking

> **Priority: 🔴 Critical** | Không có → P&L data sẽ sai khi nạp/rút tiền.

### 10.1 Capital Flow Entity

```csharp
public class CapitalFlow : Entity
{
    public string PortfolioId { get; private set; }
    public string UserId { get; private set; }
    public CapitalFlowType Type { get; private set; }    // Deposit, Withdraw
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string Note { get; private set; }
    public DateTime FlowDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public enum CapitalFlowType
{
    Deposit,      // Nạp tiền
    Withdraw,     // Rút tiền
    Dividend,     // Cổ tức
    Interest,     // Lãi tiền gửi
    Fee           // Phí quản lý
}
```

### 10.2 Cash-Flow Adjusted Return

```
TWR (Time-Weighted Return):
  TWR = Π(1 + Ri) - 1
  where Ri = (Vi - Vi-1 - Ci) / Vi-1
  Ci = net cash flow in period i

MWR (Money-Weighted Return / IRR):
  0 = -C0 + Σ(Ci / (1+r)^ti) + VN / (1+r)^N
  Solve for r using Newton-Raphson method
```

### 10.3 Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Entity | `Domain/Entities/CapitalFlow.cs` | Capital flow entity |
| Repository | `Infrastructure/Repositories/CapitalFlowRepository.cs` | Data access |
| Service | `Infrastructure/Services/CashFlowAdjustedReturnService.cs` | TWR/MWR calc |
| Command | `Application/CapitalFlows/Commands/RecordDeposit/` | Record deposit |
| Command | `Application/CapitalFlows/Commands/RecordWithdraw/` | Record withdrawal |
| Query | `Application/CapitalFlows/Queries/GetFlowHistory/` | Flow history |
| Query | `Application/CapitalFlows/Queries/GetAdjustedReturn/` | Adjusted returns |
| Controller | `Api/Controllers/CapitalFlowsController.cs` | REST endpoints |

#### API Endpoints

```
POST   /api/v1/capital-flows                     → Record flow (deposit/withdraw)
GET    /api/v1/capital-flows/portfolio/{id}       → Flow history
GET    /api/v1/capital-flows/portfolio/{id}/summary → Flow summary
GET    /api/v1/capital-flows/portfolio/{id}/twr   → Time-weighted return
GET    /api/v1/capital-flows/portfolio/{id}/mwr   → Money-weighted return
DELETE /api/v1/capital-flows/{id}                 → Delete flow record
```

#### MongoDB Collection

```json
// capital_flows collection
{
  "_id": "uuid",
  "portfolioId": "uuid",
  "userId": "uuid",
  "type": "Deposit",
  "amount": 50000000,
  "currency": "VND",
  "note": "Nạp thêm vốn tháng 3",
  "flowDate": "2026-03-01T00:00:00Z",
  "createdAt": "2026-03-01T10:00:00Z"
}
```

#### Frontend Components

```
frontend/src/app/features/capital-flows/
├── capital-flows.component.ts           // Flow history list
├── capital-flow-create/
│   └── capital-flow-create.component.ts // Record deposit/withdraw
├── capital-flow-summary/
│   └── capital-flow-summary.component.ts // Summary cards
└── adjusted-return/
    └── adjusted-return.component.ts     // TWR vs MWR comparison
```

---

## 11. Snapshot & Time Travel Data

> **Priority: 🔴 Critical** | Cực kỳ quan trọng để có dữ liệu chính xác theo thời gian.

### 11.1 Portfolio Snapshot

```csharp
public class PortfolioSnapshot : Entity
{
    public string PortfolioId { get; private set; }
    public DateTime SnapshotDate { get; private set; }
    public decimal TotalValue { get; private set; }
    public decimal CashBalance { get; private set; }
    public decimal InvestedValue { get; private set; }
    public decimal UnrealizedPnL { get; private set; }
    public decimal RealizedPnL { get; private set; }
    public decimal DailyReturn { get; private set; }
    public decimal CumulativeReturn { get; private set; }
    public List<PositionSnapshot> Positions { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public class PositionSnapshot
{
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal MarketPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal Weight { get; set; }         // % of portfolio
}
```

### 11.2 Time Travel Query

```csharp
public interface ISnapshotService
{
    Task<PortfolioSnapshot> GetSnapshotAtDate(string portfolioId, DateTime date);
    Task<List<PortfolioSnapshot>> GetSnapshotRange(
        string portfolioId, DateTime from, DateTime to);
    Task<PortfolioComparison> CompareSnapshots(
        string portfolioId, DateTime date1, DateTime date2);
    Task TakeSnapshot(string portfolioId);  // Manual snapshot
}
```

### 11.3 Backend Implementation

| Component | File Path | Description |
|-----------|-----------|-------------|
| Entity | `Domain/Entities/PortfolioSnapshot.cs` | Snapshot entity |
| Repository | `Infrastructure/Repositories/SnapshotRepository.cs` | Data access |
| Service | `Infrastructure/Services/SnapshotService.cs` | Snapshot logic |
| Worker Job | `Worker/Jobs/DailySnapshotJob.cs` | Daily snapshot (EOD) |
| Query | `Application/Snapshots/Queries/GetSnapshotAtDate/` | Time travel |
| Query | `Application/Snapshots/Queries/CompareSnapshots/` | Compare periods |
| Controller | `Api/Controllers/SnapshotsController.cs` | REST endpoints |

#### API Endpoints

```
GET    /api/v1/snapshots/portfolio/{id}/at/{date}      → Snapshot at date
GET    /api/v1/snapshots/portfolio/{id}/range           → Snapshot range
GET    /api/v1/snapshots/portfolio/{id}/compare         → Compare 2 dates
POST   /api/v1/snapshots/portfolio/{id}/take            → Manual snapshot
GET    /api/v1/snapshots/portfolio/{id}/timeline        → Timeline view
```

#### MongoDB Collection

```json
// portfolio_snapshots collection
{
  "_id": "uuid",
  "portfolioId": "uuid",
  "snapshotDate": "2026-03-01",
  "totalValue": 155000000,
  "cashBalance": 25000000,
  "investedValue": 130000000,
  "unrealizedPnL": 5000000,
  "realizedPnL": 12000000,
  "dailyReturn": 0.012,
  "cumulativeReturn": 0.155,
  "positions": [
    {
      "symbol": "VNM",
      "quantity": 1000,
      "averageCost": 75000,
      "marketPrice": 80000,
      "marketValue": 80000000,
      "unrealizedPnL": 5000000,
      "weight": 0.516
    }
  ],
  "createdAt": "2026-03-01T15:30:00Z"
}
```

#### Indexing Strategy

```csharp
// Compound index for time-travel queries
collection.Indexes.CreateOne(
    Builders<PortfolioSnapshot>.IndexKeys.Combine(
        Builders<PortfolioSnapshot>.IndexKeys.Ascending(s => s.PortfolioId),
        Builders<PortfolioSnapshot>.IndexKeys.Descending(s => s.SnapshotDate)
    )
);
```

#### Frontend Components

```
frontend/src/app/features/snapshots/
├── snapshots.component.ts               // Snapshot timeline
├── snapshot-detail/
│   └── snapshot-detail.component.ts     // Snapshot at specific date
├── snapshot-compare/
│   └── snapshot-compare.component.ts    // Side-by-side comparison
└── time-travel/
    └── time-travel.component.ts         // Date picker + portfolio view
```

---

## 12. Price Feed Worker

> **Priority: Critical** | Replaces MockMarketDataProvider with scheduled real-time price collection.

### Worker Job

| Component  | File Path                          | Description                                              |
|------------|------------------------------------|----------------------------------------------------------|
| Worker Job | `Worker/Jobs/PriceSnapshotJob.cs`  | Runs every 15 min during market hours (09:00-15:00 ICT)  |

### Behavior

```
Every 15 minutes (09:00–15:00 ICT, Mon–Fri):
1. Collect unique symbols from all trades in DB
2. Batch fetch current prices via IMarketDataProvider
3. Upsert to stock_prices collection
4. Refresh VNINDEX / VN30 in market_indices collection
5. Check stop-loss / target triggers → update StopLossTarget
```

### Market Hours Check

```csharp
// UTC+7 (ICT): 09:00–15:00 → UTC: 02:00–08:00
private static readonly TimeOnly _marketOpen  = new(2, 0);
private static readonly TimeOnly _marketClose = new(8, 0);
```

---

## 13. Health Endpoint & Monitoring

> **Priority: Medium** | Liveness/readiness probes for Docker and Kubernetes.

### API Endpoints

```
GET  /health        → Full check: MongoDB ping. Returns 503 if DB unreachable.
GET  /health/live   → Liveness probe: always 200 if process is running.
GET  /health/ready  → Readiness probe: 200 only when DB is connected.
```

### Response Format

```json
// GET /health — healthy
{ "status": "healthy", "db": "connected", "timestamp": "2026-03-06T10:00:00Z" }

// GET /health — unhealthy (503)
{ "status": "unhealthy", "db": "disconnected", "error": "...", "timestamp": "..." }
```

### Docker Integration

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1
```

### docker-compose Integration

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 30s
```

---

## 📊 Implementation Priority & Timeline

### Phase 1: Foundation — ✅ Done

| Feature                  | Status | Branch          |
|--------------------------|--------|-----------------|
| Market Data Integration  | ✅     | master          |
| Capital Flow Tracking    | ✅     | master          |
| Snapshot & Time Travel   | ✅     | master          |

### Phase 2: Risk & Analytics — ✅ Done

| Feature              | Status | Branch          |
|----------------------|--------|-----------------|
| Risk Management      | ✅     | master          |
| Advanced Analytics   | ✅     | master          |

### Phase 3: Trading Tools — ✅ Done

| Feature               | Status | Branch          |
|-----------------------|--------|-----------------|
| Strategy Management   | ✅     | master          |
| Journal & Trade Notes | ✅     | master          |
| Smart Alert System    | ✅     | master          |

### Phase 4: Enterprise — ✅ Done

| Feature                    | Status | Branch                    |
|----------------------------|--------|---------------------------|
| Backtesting Engine         | ✅     | feat/phase4-enterprise    |
| Multi-Currency Support     | ✅     | feat/phase4-enterprise    |
| Price Feed Worker          | ✅     | feat/phase4-enterprise    |
| Health Endpoint & Monitor  | ✅     | feat/phase4-enterprise    |

### Phase 5: SaaS — 📋 Planned

| Feature                | Status | Notes                              |
|------------------------|--------|------------------------------------|
| Multi-User / SaaS      | 📋     | Organization, roles, subscriptions |

---

## Tech Stack Additions

| Technology            | Purpose                         | Status     |
|-----------------------|---------------------------------|------------|
| ng2-charts / Chart.js | Equity curves, drawdown charts  | 📋 Planned |
| SignalR               | Real-time price updates, alerts | 📋 Planned |
| Azure Blob Storage    | Journal image uploads           | 📋 Planned |
| SendGrid / Mailgun    | Email alerts & digests          | 📋 Planned |
| Redis                 | Price cache, rate limiting      | 📋 Planned |

---

## MongoDB Collections Summary

| Collection           | Feature            | Status |
|----------------------|--------------------|--------|
| `risk_profiles`      | Risk Management    | ✅     |
| `stop_loss_targets`  | Risk Management    | ✅     |
| `equity_snapshots`   | Advanced Analytics | ✅     |
| `strategies`         | Strategy Mgmt      | ✅     |
| `backtests`          | Backtesting        | ✅     |
| `stock_prices`       | Market Data        | ✅     |
| `market_indices`     | Market Data        | ✅     |
| `trade_journals`     | Journal            | ✅     |
| `exchange_rates`     | Multi-Currency     | ✅     |
| `alert_rules`        | Smart Alerts       | ✅     |
| `alert_history`      | Smart Alerts       | ✅     |
| `capital_flows`      | Capital Flow       | ✅     |
| `portfolio_snapshots`| Snapshots          | ✅     |
| `organizations`      | SaaS               | 📋     |
| `digest_reports`     | Smart Alerts       | 📋     |

---

> **Phases 1-4 completed.** Next: Phase 5 (Multi-User/SaaS) on a new branch.
> Recommended team: 2 Backend + 1 Frontend + 1 QA
