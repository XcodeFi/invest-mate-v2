# Architecture — Investment Mate v2

## Directory Structure

```
project/
├── src/
│   ├── InvestmentApp.Domain/           # Entities, Value Objects, Events (zero dependencies)
│   │   ├── Entities/                   # 23 aggregate roots + nested classes (incl. FinancialProfile)
│   │   ├── ValueObjects/               # Money, StockSymbol, Position, WatchlistItem, RoutineItem, ScenarioNode, TrailingStopConfig
│   │   └── Events/                     # 14 domain event types
│   │
│   ├── InvestmentApp.Application/      # CQRS handlers, interfaces, DTOs (depends on Domain)
│   │   ├── {Feature}/Commands/         # Write operations (MediatR IRequestHandler)
│   │   ├── {Feature}/Queries/          # Read operations
│   │   ├── Common/Interfaces/          # Service interfaces (AI, Risk, Performance, Market, ComprehensiveStockData, ScenarioEvaluation)
│   │   ├── RepositoryInterfaces.cs     # All repository interfaces (~22)
│   │   └── Services/                   # FeeCalculationService (app-level)
│   │
│   ├── InvestmentApp.Infrastructure/   # Implementations (depends on Application + Domain)
│   │   ├── Services/                   # 20+ service implementations
│   │   │   ├── Hmoney/                 # 24hmoney market data, comprehensive stock data + gold price crawler
│   │   │   │   ├── HmoneyComprehensiveDataProvider.cs  # Comprehensive stock analysis data
│   │   │   │   ├── HmoneyComprehensiveApiModels.cs     # API response models
│   │   │   │   └── HmoneyGoldPriceProvider.cs          # Vàng Miếng/Nhẫn scrape (AngleSharp HTML parse)
│   │   │   └── Tcbs/                   # TCBS fundamental data provider
│   │   └── Repositories/              # 24 MongoDB repositories (incl. FinancialProfileRepository)
│   │
│   ├── InvestmentApp.Api/              # Controllers, DI, middleware (depends on all)
│   │   ├── Controllers/               # 27 API controllers (incl. PersonalFinanceController)
│   │   ├── Authorization/             # RequireAdminAttribute
│   │   ├── Middleware/                # ImpersonationValidationMiddleware, CorrelationId, Exception
│   │   └── Program.cs                 # DI registration, middleware pipeline
│   │
│   └── InvestmentApp.Worker/           # Background jobs (snapshots, alerts, scenario evaluation)
│
├── frontend/                           # Angular 18 SPA
│   └── src/app/
│       ├── core/services/              # 25 Angular services (HTTP clients)
│       ├── features/                   # 29 page components (standalone, inline templates)
│       │   ├── dashboard/              # Investor Cockpit (main page + Personal Finance widget)
│       │   ├── trade-wizard/           # 5-step disciplined trading flow
│       │   ├── trade-plan/             # Entry/SL/TP planning with checklist
│       │   ├── market-data/            # Stock detail + technical analysis
│       │   ├── analytics/              # Performance metrics, equity curve
│       │   ├── campaign-analytics/     # Cross-plan campaign review analytics (P0.7)
│       │   ├── risk-dashboard/         # Risk score, drawdown, VaR
│       │   ├── personal-finance/       # Net worth + Gold/Savings tracking + health score (Tier 3)
│       │   └── ...                     # (21 more feature pages)
│       └── shared/
│           ├── components/             # AiChatPanel, Header, PwaInstallBanner, etc.
│           ├── directives/             # UppercaseDirective, NumMaskDirective
│           └── pipes/                  # VndCurrencyPipe
│
├── tests/
│   ├── InvestmentApp.Domain.Tests/     # 661 tests (xUnit + FluentAssertions)
│   ├── InvestmentApp.Application.Tests/# 115 tests (+ Moq)
│   └── InvestmentApp.Infrastructure.Tests/ # 235 tests
│
└── docs/
    ├── architecture.md                 # This file
    ├── business-domain.md              # Entity map, business rules, API endpoints
    ├── features.md                     # Feature list by phase
    ├── project-context.md              # Project goals, decisions, improvement plan
    ├── plans/
    │   └── technical-analysis-features.md  # Lộ trình mở rộng TA & chiến lược (6 phases)
    └── references/                     # Tài liệu tham chiếu kiến thức giao dịch
        ├── README.md                   # Index + hướng dẫn sử dụng
        ├── Phan-Loai-Chi-Bao-Muc-Dich-Cach-Dung.md      # 10 nhóm chỉ báo kỹ thuật
        ├── Chien-Luoc-Giao-Dich-Va-Quan-Ly-Rui-Ro.md    # 7 chiến lược + quản lý rủi ro
        └── Phan-Tich-Ky-Thuat-Giao-Dich-Ngan-Han.md     # Công thức chi tiết + hệ thống hoàn chỉnh
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
| TradePlan | State machine (Draft→Ready→InProgress→Executed→Reviewed), multi-lot entry, exit targets, SL history, scenario playbook (Simple/Advanced mode, ScenarioNodes decision tree) |
| CapitalFlow | SignedAmount (Deposit/Dividend=+, Withdraw/Fee=-) |
| Watchlist | Duplicate detection, bulk import, target prices |
| DailyRoutine | Streak tracking, completion management, template-based creation |
| StopLossTarget | R:R ratio calculation, trailing stop |
| AiSettings | Multi-provider (Claude/Gemini), encrypted API keys, token usage tracking |
| RiskProfile | Position size limits, drawdown alerts, sector exposure |
| JournalEntry | Standalone journal (không cần Trade), 5 loại entry, cảm xúc, snapshot giá |
| MarketEvent | Sự kiện thị trường (7 loại: Earnings/Dividend/News/Macro...) |
| FinancialProfile | Per-user 1:1. 5 loại account (Securities/Savings/Emergency/IdleCash/Gold) + **Debts[]** (6 loại: CreditCard/PersonalLoan/Mortgage/Auto/Installment/Other) + FinancialRules (emergency months, max investment %, min savings %). Health score 0-100 với **4 rules** (rule 4: `-20` cứng khi có consumer debt lãi > 20%/năm). **Net Worth = Assets − Debt**. Gold account: brand + type + quantity → auto-calc Balance qua provider. Debts không xóa được khi Principal > 0 |

## Key Services (Infrastructure Layer)

| Service | Responsibility | Key Dependencies |
|---------|---------------|-----------------|
| PnLService | FIFO P&L calculation (realized + unrealized) | ITradeRepository, IStockPriceService |
| RiskCalculationService | VaR(95%), max drawdown, position sizing, correlation matrix, portfolio optimization (concentration/sector/correlation), trailing stop alerts | IPnLService, ISnapshotRepo, IRiskProfileRepo, IFundamentalDataProvider |
| PerformanceMetricsService | CAGR, Sharpe, Sortino, win rate, profit factor, equity curve | ISnapshotRepo, ITradeRepo |
| PositionSizingService | 5 position sizing models: Fixed Risk, ATR-Based, Kelly Criterion (Half-Kelly, 25% cap), Turtle (1-unit entry), Volatility-Adjusted (ATR% scaling). Pure calculation, no DB dependencies | None (stateless) |
| TechnicalIndicatorService | 10 indicators: EMA(20/21/50/200), RSI(14), MACD(12,26,9), Stochastic(14,3,3), ADX(14)+DI, OBV, MFI(14), Bollinger(20,2), ATR(14), Volume ratio. S/R, Fibonacci, 10-indicator voting signal, Confluence Score (0-100 weighted), Market Condition Classifier (ADX-based), Divergence Detection (RSI/MACD vs Price) | IMarketDataProvider |
| AiAssistantService | AI prompt building for 12 use cases, streaming responses | 12+ repos and services |
| HmoneyComprehensiveDataProvider | Comprehensive stock data from 24hmoney (financials, reports, dividends, foreign trading, recommendations) | HttpClient, IMemoryCache |
| HmoneyMarketDataProvider | Real-time prices from 24hmoney.vn (prices ×1000 scaling) | HttpClient, IMemoryCache |
| HmoneyGoldPriceProvider | Vàng Miếng + Nhẫn từ `24hmoney.vn/gia-vang` (HTML scrape với AngleSharp, không có JSON API). Filter 4 brand × 2 type, values là full VND (không scale). Two-tier cache: fresh 5 phút + stale 6h fallback khi 24hmoney down | HttpClient, IMemoryCache |
| TcbsFundamentalDataProvider | P/E, EPS, ROE from TCBS API | HttpClient, IMemoryCache |
| SnapshotService | Daily portfolio snapshots with position weights | IPnLService |
| AlertEvaluationService | Price/drawdown/portfolio value alerts | ISnapshotRepo, IStockPriceRepo |
| ScenarioEvaluationService | Auto-evaluate scenario playbooks every 15 min, trigger actions, create AlertHistory | ITradePlanRepo, IStockPriceService |
| BehavioralAnalysisService | Detect FOMO, panic sell, revenge trading, overtrading patterns | JournalEntry, Trade data |
| CampaignReviewService | Auto-calculate P&L metrics for campaign review (amount, %, VND/ngày, annualized return, target achievement) | ITradeRepository, IPnLService |
| VietstockEventProvider | Crawl news + corporate events from Vietstock API (CSRF token flow) | HttpClient |

## API Endpoints (27 Controllers)

| Controller | Base Route | Key Operations |
|-----------|-----------|----------------|
| Auth | `/api/v1/auth` | Google OAuth, JWT token |
| Portfolios | `/api/v1/portfolios` | CRUD, list by user |
| Trades | `/api/v1/trades` | CRUD, bulk create, link to plan/strategy |
| TradePlans | `/api/v1/trade-plans` | CRUD, status transitions, lot execution, scenario node trigger, scenario templates, **campaign review (P0.7)**: close with auto-metrics, preview, update lessons, pending-review list, cross-plan analytics |
| MarketData | `/api/v1/market` | Price, batch prices, search, overview, top fluctuation |
| PnL | `/api/v1/pnl` | Portfolio/position P&L |
| Risk | `/api/v1/risk` | Summary, drawdown, VaR, correlation, stop-loss targets, **stress-test (P2)**, **budget (P4)** |
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
| JournalEntries | `/api/v1/journal-entries` | CRUD standalone journal entries, **pending-review (P1)** |
| SymbolTimeline | `/api/v1/symbols/{symbol}/timeline` | Unified timeline (journals + trades + events + alerts) |
| MarketEvents | `/api/v1/market-events` | CRUD market events per symbol, crawl from Vietstock |
| Admin | `/api/v1/admin` | **Impersonation (debug tooling)**: start/stop user impersonation. Restricted via `[RequireAdmin]` (role=Admin + no `amr=impersonate`). Mutation blocked during impersonation unless `Admin:AllowImpersonateMutations=true`. |
| PersonalFinance | `/api/v1/personal-finance` | **Net worth tracking (Tier 3)**: GET `/` (profile, 404 if absent) + GET `/summary` (net worth + health score 0-100 + 4 rule checks + debts + `HasHighInterestConsumerDebt` flag) + GET `/gold-prices` (live from 24hmoney, cached 5 min) + PUT `/` (upsert profile) + PUT `/accounts` + DELETE `/accounts/{id}` (bảo vệ last Securities) + **PUT `/debts` (upsert debt)** + **DELETE `/debts/{id}` (reject nếu Principal > 0)** |

## Health Endpoints (Minimal API, unauthenticated)

| Route | Checks | Response fields |
|-------|--------|-----------------|
| `/health` | Mongo ping | `status`, `db`, `version`, `timestamp` (503 on db failure) |
| `/health/live` | Process alive only | `status`, `version`, `timestamp` |
| `/health/ready` | Mongo ping | `status`, `version`, `timestamp` (503 on db failure) |

`version` is read from `APP_VERSION` env (fallback `"dev"` when unset or empty). CI/CD bakes the short git SHA into the image via `APP_VERSION` build-arg → `curl /health` after deploy confirms which commit is running.

## External Integrations

| Provider | Base URL | Purpose | Cache TTL |
|----------|---------|---------|-----------|
| 24hmoney | `api-finance-t19.24hmoney.vn` | Real-time prices, history, company list | 15s prices, 30min companies |
| 24hmoney gold | `24hmoney.vn/gia-vang` (HTML page) | Gold prices (Miếng + Nhẫn, 4 brand) — no JSON API, SSR HTML scrape with AngleSharp. Env var: `GoldPriceProvider__PageUrl` | 5min fresh + 6h stale fallback |
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

- **Backend:** xUnit + FluentAssertions + Moq (1019 tests: Domain 661, Application 118, Infrastructure 235, Api 5)
- **Frontend:** Karma + Jasmine (configured, tests pending)
- Run `dotnet test` before commit

## Admin Area (Debug Tooling)

Feature B1 (2026-04-21) + Phase 2 users overview (2026-04-22) — cho phép admin debug data của user cụ thể bằng cách xem UI như user đó, và xem toàn bộ user + activity stats.

### Layout

`/admin` → `AdminLayoutComponent` với left sidebar + `<router-outlet>`. Menu mục hiện có:
- **Tổng quan user** (`users/overview`, default) — bảng paginated toàn bộ user + stats.
- **Tìm & Impersonate** (`users/search`) — search email để impersonate (Phase 1 flow).

Mở rộng: thêm feature admin mới = thêm 1 entry vào `menu[]` + thêm child route. Guard `AdminGuard` áp ở level parent route.

### Users Overview

- Endpoint: `GET /api/v1/admin/users/overview?page=&pageSize=` (default page=1, pageSize=20, max 200).
- Handler: `GetUsersOverviewQueryHandler` verify role=Admin → `IUserRepository.GetPagedAsync` sort CreatedAt desc → batch lookup portfolios theo userIds → batch stats trades theo portfolioIds → per-user lookup `ImpersonationAudit.GetLatestStartedAtByTargetAsync`.
- DTO trả về: `{ id, email, name, role, createdAt, lastLoginAt, portfolioCount, tradeCount, lastTradeAt, lastImpersonatedAt }`.
- `User.LastLoginAt` được cập nhật trong `AuthController.GoogleCallback` (cả new user + existing) qua `User.RecordLogin()`. Không cập nhật khi refresh token hay impersonate.

### Impersonation flow

Feature B1 (2026-04-21) — cho phép admin debug data của user cụ thể bằng cách xem UI như user đó.

**Flow:**
1. Admin đăng nhập bình thường (Google OAuth → JWT chứa `role=Admin`, set qua `Admin:AllowEmails` config).
2. Gọi `POST /api/v1/admin/impersonate { targetUserId, reason }` → nhận JWT impersonate (TTL 1h) với claims: `sub=targetId`, `actor=adminId`, `impersonation_id`, `amr=impersonate`.
3. FE lưu admin token ở `localStorage.admin_auth_token`, set impersonate token vào `auth_token`, reload. Banner đỏ hiển thị.
4. Mọi request qua `ImpersonationValidationMiddleware`:
   - Validate `impersonation_id` chưa bị revoke (Mongo lookup). Nếu revoked → 401 + header `X-Impersonation-Revoked: true`.
   - Block POST/PUT/DELETE/PATCH (403 + `MUTATION_BLOCKED_DURING_IMPERSONATION`) trừ khi `Admin:AllowImpersonateMutations=true` hoặc path là `/admin/impersonate/stop`.
   - Set header `X-Impersonating: true`.
5. Stop: `POST /api/v1/admin/impersonate/stop` (gọi bằng impersonate token) → set `IsRevoked=true` trên `ImpersonationAudit`.
6. FE interceptor `impersonation-revoked.interceptor.ts` tự động catch 401 + revoked header → khôi phục admin token.

**Key files:**
- `Authorization/RequireAdminAttribute.cs` — chặn non-admin + chặn token impersonate start impersonation lồng
- `Middleware/ImpersonationValidationMiddleware.cs` — validate + mutation-block, đặt giữa `UseAuthentication` và `UseAuthorization`
- `Infrastructure/Services/AdminBootstrapHostedService.cs` — promote user từ `Admin:AllowEmails` khi startup (idempotent)
- `frontend/src/app/core/services/impersonation.service.ts` — start/stop, backup `auth_token` sang `admin_auth_token`
- `frontend/src/app/shared/components/impersonation-banner/` — sticky red banner top

**Config (`appsettings.json`):**
```json
"Admin": {
  "AllowEmails": "admin@example.com,other-admin@example.com",
  "AllowImpersonateMutations": false
}
```
CSV string (not array) — 1 env var đủ: `Admin__AllowEmails="a@x.com,b@x.com"`, `Admin__AllowImpersonateMutations=true`.

## Personal Finance (Tier 3)

Feature cross-cutting tổng quan tài sản + nguyên tắc tài chính + crawler giá vàng. Shipped 2026-04-22 qua 6 PR (77, 78, 79/80, 81, 82, this).

**Flow:**
1. User thiết lập profile với `MonthlyExpense` → backend tạo `FinancialProfile` với 4 default accounts (Securities/Savings/Emergency/IdleCash) + `FinancialRules` defaults (6 tháng dự phòng / cap đầu tư 50% / sàn tiết kiệm 30%).
2. User thêm Gold account qua form FE: chọn brand + type + quantity (lượng) → FE fetch `GET /personal-finance/gold-prices` → hiển thị live price + Balance auto-calc preview.
3. Backend `UpsertFinancialAccountCommand` detect 3 Gold fields set → gọi `IGoldPriceProvider.GetPriceAsync(brand, type)` → `Balance = quantity × BuyPrice` (giá tiệm mua vào = giá user bán được). Provider null → throw 400 (không silent fallback).
4. `GET /summary` aggregate securities value từ tất cả portfolios của user qua `IPnLService` → tính health score 0-100 với 3 rules:
   - **Emergency**: `emergencyTotal ≥ monthlyExpense × EmergencyFundMonths` (trừ tối đa 40)
   - **Investment cap**: `(securitiesValue + goldTotal) ≤ totalAssets × MaxInvestmentPercent%` (trừ tối đa 30)
   - **Savings floor**: `savingsTotal ≥ totalAssets × MinSavingsPercent%` (trừ tối đa 30)
   - **High-interest consumer debt**: `-20` cứng (binary) nếu có `CreditCard`/`PersonalLoan` với `InterestRate > 20%/năm` (strict)
   - Rules 1-3 tỷ lệ thuận với vi phạm so với **target của rule**. Rule 4 binary.
5. FE dashboard widget + trang `/personal-finance` hiển thị breakdown + **Net Worth card** + health bar + rule checks pass/fail + **high-interest debt banner** + debts section.

**Key files:**
- `src/InvestmentApp.Domain/Entities/FinancialProfile.cs` — aggregate, + `FinancialAccount` + `Debt` + `FinancialRules` + 4 enums (`FinancialAccountType`, `GoldBrand`, `GoldType`, `DebtType`)
- `src/InvestmentApp.Application/PersonalFinance/` — 5 commands (UpsertProfile, Upsert/RemoveAccount, **Upsert/RemoveDebt**), 3 queries, DTOs, `PersonalFinanceMapper`
- `src/InvestmentApp.Application/Common/Interfaces/IGoldPriceProvider.cs` — provider contract
- `src/InvestmentApp.Infrastructure/Services/Hmoney/HmoneyGoldPriceProvider.cs` — HTML scrape impl
- `src/InvestmentApp.Infrastructure/Repositories/FinancialProfileRepository.cs` — Mongo repo, unique index UserId
- `src/InvestmentApp.Api/Controllers/PersonalFinanceController.cs` — **8 endpoints** (2 debts + 6 existing)
- `frontend/src/app/core/services/personal-finance.service.ts` — HTTP client + TS DTOs + label helpers (incl. `DebtType`)
- `frontend/src/app/features/personal-finance/personal-finance.component.ts` — standalone page với Gold form + **Debts section với click-to-edit + ESC close + Net Worth card**

**Config (`appsettings.json`):**
```json
"GoldPriceProvider": {
  "PageUrl": "{GoldPriceProvider__PageUrl}",
  "TimeoutSeconds": 30,
  "CacheTtlMinutes": 5
}
```
Placeholder `{GoldPriceProvider__PageUrl}` — env var bắt buộc set trước deploy: `GoldPriceProvider__PageUrl=https://24hmoney.vn/gia-vang`. Nếu không set, provider sẽ DNS-fail khi serve request đầu tiên (`appsettings.Development.json` có URL thật, gitignored).

**Key quirks (documented for future maintenance):**
- 24hmoney page label nói "Đơn vị: triệu VNĐ/lượng" nhưng HTML values là **full VND** (167,200,000) — không scale ×1000 như giá CP. Fixture test `PricesAreFullVND_NotScaledBy1000` lock behavior.
- `AngleSharp.Configuration` bị shadow bởi project's `InvestmentApp.Infrastructure.Configuration` namespace → phải fully qualify `AngleSharp.Configuration.Default`.
- Mongo index creation trong repository constructor catch narrow 2 codes (85/86) only — các exception khác (permissions, network) re-throw để không silent mask bug.
