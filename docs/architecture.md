# Architecture — Investment Mate v2

## Directory Structure

```
project/
├── src/
│   ├── InvestmentApp.Domain/           # Entities, Value Objects, Events (zero dependencies)
│   │   ├── Entities/                   # 20 aggregate roots + nested classes
│   │   ├── ValueObjects/               # Money, StockSymbol, Position, WatchlistItem, RoutineItem
│   │   └── Events/                     # 12 domain event types
│   │
│   ├── InvestmentApp.Application/      # CQRS handlers, interfaces, DTOs (depends on Domain)
│   │   ├── {Feature}/Commands/         # Write operations (MediatR IRequestHandler)
│   │   ├── {Feature}/Queries/          # Read operations
│   │   ├── Common/Interfaces/          # Service interfaces (AI, Risk, Performance, Market, ComprehensiveStockData)
│   │   ├── RepositoryInterfaces.cs     # All repository interfaces (~20)
│   │   └── Services/                   # FeeCalculationService (app-level)
│   │
│   ├── InvestmentApp.Infrastructure/   # Implementations (depends on Application + Domain)
│   │   ├── Services/                   # 20+ service implementations
│   │   │   ├── Hmoney/                 # 24hmoney market data + comprehensive stock data provider
│   │   │   │   ├── HmoneyComprehensiveDataProvider.cs  # Comprehensive stock analysis data
│   │   │   │   └── HmoneyComprehensiveApiModels.cs     # API response models
│   │   │   └── Tcbs/                   # TCBS fundamental data provider
│   │   └── Repositories/              # 20 MongoDB repositories
│   │
│   ├── InvestmentApp.Api/              # Controllers, DI, middleware (depends on all)
│   │   ├── Controllers/               # 22 API controllers
│   │   └── Program.cs                 # DI registration, middleware pipeline
│   │
│   └── InvestmentApp.Worker/           # Background jobs (snapshots, alerts)
│
├── frontend/                           # Angular 18 SPA
│   └── src/app/
│       ├── core/services/              # 23 Angular services (HTTP clients)
│       ├── features/                   # 25 page components (standalone, inline templates)
│       │   ├── dashboard/              # Investor Cockpit (main page)
│       │   ├── trade-wizard/           # 5-step disciplined trading flow
│       │   ├── trade-plan/             # Entry/SL/TP planning with checklist
│       │   ├── market-data/            # Stock detail + technical analysis
│       │   ├── analytics/              # Performance metrics, equity curve
│       │   ├── risk-dashboard/         # Risk score, drawdown, VaR
│       │   └── ...                     # (20 more feature pages)
│       └── shared/
│           ├── components/             # AiChatPanel, Header, PwaInstallBanner, etc.
│           ├── directives/             # UppercaseDirective, NumMaskDirective
│           └── pipes/                  # VndCurrencyPipe
│
├── tests/
│   ├── InvestmentApp.Domain.Tests/     # 512 tests (xUnit + FluentAssertions)
│   ├── InvestmentApp.Application.Tests/# 14 tests (+ Moq)
│   └── InvestmentApp.Infrastructure.Tests/ # 29 tests
│
└── docs/
    ├── architecture.md                 # This file
    ├── business-domain.md              # Entity map, business rules, API endpoints
    ├── features.md                     # Feature list by phase
    └── project-context.md              # Project goals, decisions, improvement plan
```

## Layer Dependencies

```
Domain (zero deps) ← Application ← Infrastructure ← Api
                                                    ← Worker
```

## Key Entities (Domain Layer)

| Entity | Key Business Logic |
|--------|-------------------|
| Portfolio | Trade management, domain events |
| Trade | Symbol normalization (ToUpper), fee/tax tracking |
| TradePlan | State machine (Draft→Ready→InProgress→Executed→Reviewed), multi-lot entry, exit targets, SL history |
| CapitalFlow | SignedAmount (Deposit/Dividend=+, Withdraw/Fee=-) |
| Watchlist | Duplicate detection, bulk import, target prices |
| DailyRoutine | Streak tracking, completion management, template-based creation |
| StopLossTarget | R:R ratio calculation, trailing stop |
| AiSettings | Multi-provider (Claude/Gemini), encrypted API keys, token usage tracking |
| RiskProfile | Position size limits, drawdown alerts, sector exposure |

## Key Services (Infrastructure Layer)

| Service | Responsibility | Key Dependencies |
|---------|---------------|-----------------|
| PnLService | FIFO P&L calculation (realized + unrealized) | ITradeRepository, IStockPriceService |
| RiskCalculationService | VaR(95%), max drawdown, position sizing, correlation matrix | IPnLService, ISnapshotRepo |
| PerformanceMetricsService | CAGR, Sharpe, Sortino, win rate, profit factor, equity curve | ISnapshotRepo, ITradeRepo |
| TechnicalIndicatorService | EMA(20/50), RSI(14), MACD(12,26,9), support/resistance | IMarketDataProvider |
| AiAssistantService | AI prompt building for 12 use cases, streaming responses | 12+ repos and services |
| HmoneyComprehensiveDataProvider | Comprehensive stock data from 24hmoney (financials, reports, dividends, foreign trading, recommendations) | HttpClient, IMemoryCache |
| HmoneyMarketDataProvider | Real-time prices from 24hmoney.vn (prices ×1000 scaling) | HttpClient, IMemoryCache |
| TcbsFundamentalDataProvider | P/E, EPS, ROE from TCBS API | HttpClient, IMemoryCache |
| SnapshotService | Daily portfolio snapshots with position weights | IPnLService |
| AlertEvaluationService | Price/drawdown/portfolio value alerts | ISnapshotRepo, IStockPriceRepo |

## API Endpoints (22 Controllers)

| Controller | Base Route | Key Operations |
|-----------|-----------|----------------|
| Auth | `/api/v1/auth` | Google OAuth, JWT token |
| Portfolios | `/api/v1/portfolios` | CRUD, list by user |
| Trades | `/api/v1/trades` | CRUD, bulk create, link to plan/strategy |
| TradePlans | `/api/v1/trade-plans` | CRUD, status transitions, lot execution |
| MarketData | `/api/v1/market` | Price, batch prices, search, overview, top fluctuation |
| PnL | `/api/v1/pnl` | Portfolio/position P&L |
| Risk | `/api/v1/risk` | Summary, drawdown, VaR, correlation, stop-loss targets |
| Analytics | `/api/v1/analytics` | Performance, equity curve, monthly returns |
| Ai | `/api/v1/ai` | Build context, stream responses, daily briefing, comprehensive analysis |
| AiSettings | `/api/v1/ai-settings` | Provider/key management |
| Alerts | `/api/v1/alerts` | Rules CRUD, history, unread count |
| Watchlists | `/api/v1/watchlists` | CRUD, items, VN30 import |
| Strategies | `/api/v1/strategies` | CRUD, performance, templates |
| Journals | `/api/v1/journals` | CRUD, link to trade |
| CapitalFlows | `/api/v1/capital-flows` | Record, history, adjusted returns (TWR/MWR) |
| Positions | `/api/v1/positions` | Active positions with P&L |
| DailyRoutines | `/api/v1/daily-routines` | Today routine, complete item, templates |
| Snapshots | `/api/v1/snapshots` | Take, range query, compare |
| Fees | `/api/v1/fees` | Fee calculation, summary |
| Currency | `/api/v1/currency` | Exchange rates, conversion |
| Backtests | `/api/v1/backtests` | Run, list, results |
| Templates | `/api/v1/templates` | Strategy templates, risk profile templates |

## External Integrations

| Provider | Base URL | Purpose | Cache TTL |
|----------|---------|---------|-----------|
| 24hmoney | `api-finance-t19.24hmoney.vn` | Real-time prices, history, company list | 15s prices, 30min companies |
| TCBS | `apipubaws.tcbs.com.vn` | Fundamentals (P/E, ROE, EPS) | 5min |
| Anthropic | `api.anthropic.com` | Claude AI streaming | None |
| Google | `generativelanguage.googleapis.com` | Gemini AI streaming | None |

## Frontend Architecture

- **Standalone components** with inline templates (`template: \`...\``)
- **Template-driven forms** with ngModel (not reactive forms)
- **Tailwind CSS** for styling
- **Services** in `core/services/` call backend API via HttpClient
- **AiChatPanel** shared component used on multiple pages with different use cases
- **PwaInstallBannerComponent** — install prompt + update notification banner
- **PwaService** (`core/services/pwa.service.ts`) — install prompt management, SW update detection
- **Key directives:** `appUppercase` (symbol input), `appNumMask` (number formatting)
- **Key pipes:** `VndCurrencyPipe` (format tiền VND)

## PWA

- **Service Worker:** `@angular/service-worker` (ngsw), enabled in production + staging builds
- **Manifest:** `frontend/src/manifest.webmanifest` — display: standalone, theme: #2563eb
- **Icons:** `frontend/src/assets/icons/` — SVG icons 72→512px
- **Caching strategy:** App shell prefetch; API data groups with freshness/performance strategies
- **ngsw-config:** `frontend/ngsw-config.json`

## Database

- **MongoDB** (Atlas cloud in production)
- Repositories use generic `IRepository<T>` base with entity-specific extensions
- **Indexes:** Compound indexes on (portfolioId + symbol), (userId + date), unique constraints on snapshots
- **Soft delete** pattern: `IsDeleted` flag, filtered in queries

## Testing

- **Backend:** xUnit + FluentAssertions + Moq (556 tests)
- **Frontend:** Karma + Jasmine (configured, tests pending)
- Run `dotnet test` before commit
