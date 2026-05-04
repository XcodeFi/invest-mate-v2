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
│   └── InvestmentApp.Api/              # Controllers, DI, middleware (depends on all)
│       ├── Controllers/               # 28 API controllers (incl. PersonalFinanceController, InternalJobsController)
│       ├── Auth/                      # SchedulerEmailAllowlist, GcpOidcExtensions (Cloud Scheduler OIDC)
│       ├── Authorization/             # RequireAdminAttribute
│       ├── Middleware/                # ImpersonationValidationMiddleware, CorrelationId, Exception
│       ├── Services/                  # BacktestQueueService (in-process queue, replaces Worker poll)
│       └── Program.cs                 # DI registration, middleware pipeline
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
```

Background jobs that used to live in a separate `InvestmentApp.Worker` Cloud Run service
are now **in-process** in the API:

- **Snapshot / prices / exchange-rate / scenario-eval** → triggered externally by Cloud
  Scheduler hitting `/internal/jobs/*` (OIDC-authenticated, see ADR-0001).
- **BacktestQueueService** → singleton `BackgroundService` that drains an in-memory
  `Channel<string>` queue. `RunBacktestCommandHandler` enqueues the id after persist;
  the loop runs `BacktestEngine` in a fresh DI scope. Recovers `Pending` backtests on
  startup so a Cloud Run scale-down doesn't lose work.

## Key Entities (Domain Layer)

| Entity | Key Business Logic |
|--------|-------------------|
| Portfolio | Trade management, domain events |
| Trade | Symbol normalization (ToUpper), fee/tax tracking |
| TradePlan | State machine (Draft→Ready→InProgress→Executed→Reviewed), multi-lot entry, exit targets, SL history, scenario playbook (Simple/Advanced mode, ScenarioNodes decision tree), **thesis-driven discipline (Vin-discipline, 2026-04-23)**: `Thesis` (rename từ `Reason`) + `InvalidationCriteria` (List<InvalidationRule>) + `ExpectedReviewDate` + `LegacyExempt`, size-based gate fold vào `MarkReady`/`MarkInProgress`, `AbortWithThesisInvalidation` raise `TradePlanThesisInvalidatedEvent` |
| InvalidationRule (VO) | Value object trên TradePlan — `Trigger` (enum `InvalidationTrigger`: EarningsMiss/TrendBreak/NewsShock/ThesisTimeout/Manual) + `Detail` + `CheckDate` + `IsTriggered` + `TriggeredAt`. Falsifiable điều kiện phá thesis (§D2 plan Vin-discipline) |
| CapitalFlow | SignedAmount (Deposit/Dividend=+, Withdraw/Fee=-) |
| Watchlist | Duplicate detection, bulk import, target prices |
| DailyRoutine | Streak tracking, completion management, template-based creation |
| StopLossTarget | R:R ratio calculation, trailing stop |
| AiSettings | Multi-provider (Claude/Gemini), encrypted API keys, token usage tracking |
| RiskProfile | Position size limits, drawdown alerts, sector exposure |
| JournalEntry | Standalone journal (không cần Trade), 5 loại entry, cảm xúc, snapshot giá |
| MarketEvent | Sự kiện thị trường (7 loại: Earnings/Dividend/News/Macro...) |
| FinancialProfile | Per-user 1:1. 5 loại account (Securities/Savings/Emergency/IdleCash/Gold) + **Debts[]** (6 loại: CreditCard/PersonalLoan/Mortgage/Auto/Installment/Other) + FinancialRules (emergency months, max investment %, min savings %). Health score 0-100 với **4 rules** (rule 4: `-20` cứng khi có consumer debt lãi > 20%/năm). **Net Worth = Assets − Debt**. Gold account: brand + type + quantity → auto-calc Balance qua provider. Savings account có thêm `DepositDate` + `MaturityDate` optional cho sổ có kỳ hạn (2026-04-24); cả 2 set → enforce `Maturity >= Deposit`. `FinancialAccount.CreatedAt` immutable sau Create. Debts không xóa được khi Principal > 0 |

## Key Services (Infrastructure Layer)

| Service | Responsibility | Key Dependencies |
|---------|---------------|-----------------|
| PnLService | FIFO P&L calculation (realized + unrealized) | ITradeRepository, IStockPriceService |
| RiskCalculationService | VaR(95%), max drawdown, position sizing, correlation matrix, portfolio optimization (concentration/sector/correlation), trailing stop alerts | IPnLService, ISnapshotRepo, IRiskProfileRepo, IFundamentalDataProvider |
| PerformanceMetricsService | CAGR, Sharpe, Sortino, win rate, profit factor, equity curve | ISnapshotRepo, ITradeRepo |
| PositionSizingService | 5 position sizing models: Fixed Risk, ATR-Based, Kelly Criterion (Half-Kelly, 25% cap), Turtle (1-unit entry), Volatility-Adjusted (ATR% scaling). Pure calculation, no DB dependencies | None (stateless) |
| TechnicalIndicatorService | 10 indicators: EMA(20/21/50/200), RSI(14), MACD(12,26,9), Stochastic(14,3,3), ADX(14)+DI, OBV, MFI(14), Bollinger(20,2), ATR(14), Volume ratio. S/R, Fibonacci, 10-indicator voting signal, Confluence Score (0-100 weighted), Market Condition Classifier (ADX-based), Divergence Detection (RSI/MACD vs Price) | IMarketDataProvider |
| AiAssistantService | AI prompt building for 13 use cases (incl. **portfolio-critique** 2026-05-04 — adversarial HLV phản biện coach role, replace daily-briefing trên Dashboard, ép 3 điểm phản biện + động từ mệnh lệnh, KHÔNG khen). Streaming responses + non-streaming `BuildContextAsync`. `BuildPortfolioCritiqueSystemPrompt` public static để test lock content. | 12+ repos and services |
| HmoneyComprehensiveDataProvider | Comprehensive stock data from 24hmoney (financials, reports, dividends, foreign trading, recommendations) | HttpClient, IMemoryCache |
| HmoneyMarketDataProvider | Real-time prices from 24hmoney.vn (prices ×1000 scaling) | HttpClient, IMemoryCache |
| HmoneyGoldPriceProvider | Vàng Miếng + Nhẫn từ `24hmoney.vn/gia-vang` (HTML scrape với AngleSharp, không có JSON API). Filter 4 brand × 2 type, values là full VND (không scale). Two-tier cache: fresh 5 phút + stale 6h fallback khi 24hmoney down | HttpClient, IMemoryCache |
| HmoneyBankRateProvider | **So sánh với tiết kiệm (2026-04-24)** — top lãi suất theo kỳ hạn (1/3/6/9/12 tháng) từ `24hmoney.vn/lai-suat-gui-ngan-hang` (SSR HTML, AngleSharp). Ưu tiên table online (cao hơn quầy 0.2-0.8%). Two-tier cache: fresh 6h + stale 24h. Env var `BankRateProvider__PageUrl` bắt buộc set trước deploy. Startup warning nếu placeholder chưa resolve | HttpClient, IMemoryCache |
| HypotheticalSavingsReturnService | Pure math — "nếu cash flows của portfolio đã gửi tiết kiệm @ r, số dư cuối là?". Running-balance iterative, monthly compound `(1+r/12)^months`. Caller filter Deposit/Withdraw (loại Dividend/Interest/Fee tránh double-count). No DI dependencies | None (stateless) |
| TcbsFundamentalDataProvider | P/E, EPS, ROE from TCBS API | HttpClient, IMemoryCache |
| SnapshotService | Daily portfolio snapshots with position weights | IPnLService |
| AlertEvaluationService | Price/drawdown/portfolio value alerts | ISnapshotRepo, IStockPriceRepo |
| ScenarioEvaluationService | Auto-evaluate scenario playbooks every 15 min, trigger actions, create AlertHistory | ITradePlanRepo, IStockPriceService |
| BehavioralAnalysisService | Detect FOMO, panic sell, revenge trading, overtrading patterns | JournalEntry, Trade data |
| CampaignReviewService | Auto-calculate P&L metrics for campaign review (amount, %, VND/ngày, annualized return, target achievement) | ITradeRepository, IPnLService |
| VietstockEventProvider | Crawl news + corporate events from Vietstock API (CSRF token flow) | HttpClient |
| DisciplineScoreCalculator | **Vin-discipline widget backend (2026-04-23)** — tính điểm kỷ luật thesis hybrid: SL-Integrity 50% + Plan Quality 30% + Review Timeliness 20%. Stop-Honor Rate primitive (trades lỗ đã đóng với exitPrice ≥ plannedSL / tổng lỗ). Null-safe re-normalize khi sub-metric thiếu denominator. Multi-lot per-lot matching theo `TradeIds`. Cache 5 phút, invalidate on `TradeClosedEvent`/`PlanReviewedEvent`/`TradePlanThesisInvalidatedEvent` | ITradePlanRepository, ITradeRepository, IMemoryCache |

## API Endpoints (28 Controllers)

| Controller | Base Route | Key Operations |
|-----------|-----------|----------------|
| Auth | `/api/v1/auth` | Google OAuth, JWT token |
| Portfolios | `/api/v1/portfolios` | CRUD, list by user |
| Trades | `/api/v1/trades` | CRUD, bulk create, link to plan/strategy |
| TradePlans | `/api/v1/trade-plans` | CRUD, status transitions, lot execution, scenario node trigger, scenario templates, **campaign review (P0.7)**: close with auto-metrics, preview, update lessons, pending-review list, cross-plan analytics, **abort với thesis invalidation (Vin-discipline, 2026-04-23)**: `POST /{id}/abort { trigger, detail }` → `AbortTradePlanCommand` → raise `TradePlanThesisInvalidatedEvent` |
| Discipline | `/api/v1/me/discipline-score` | **Vin-discipline widget (2026-04-23)** — `GET ?days=7|30|90|365` (default 90). Query `GetDisciplineScoreQuery` → `IDisciplineScoreCalculator`. Cache 5 min. |
| MarketData | `/api/v1/market` | Price, batch prices, search, overview, top fluctuation |
| PnL | `/api/v1/pnl` | Portfolio/position P&L |
| Risk | `/api/v1/risk` | Summary, drawdown, VaR, correlation, stop-loss targets, **stress-test (P2)**, **budget (P4)** |
| Analytics | `/api/v1/analytics` | Performance, equity curve, monthly returns, **vs-savings comparison (2026-04-24)** — `GET /portfolio/{id}/vs-savings?savingsRate=&asOf=` + `GET /bank-rates` (top 12T từ 24hmoney), **household CAGR (2026-05-03)** — `GET /household/performance` returns aggregated TWR + CAGR across all of caller's portfolios with `isStable` flag (true ⇔ snapshot window ≥ 365 ngày) |
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
| InternalJobs | `/internal/jobs` | **Cloud Scheduler triggers (ADR-0001, 2026-04-26)**: POST `/snapshot` (TakeAllSnapshotsAsync) + POST `/prices` (PriceSnapshotJobService — fetch prices, refresh indices, check stop-loss/target) + POST `/exchange-rate` (RefreshRatesAsync) + POST `/scenario-eval` (EvaluateAllAsync). Auth: `[Authorize(Scheme=GcpOidc, Policy=GcpScheduler)]` — Google-issued OIDC ID token, email_verified=true, email ∈ `Jobs:AllowedSchedulerSAs` allowlist. Idempotent. |

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
| 24hmoney bank rates | `24hmoney.vn/lai-suat-gui-ngan-hang` (HTML page) | Top VN bank savings rates by term. Env var: `BankRateProvider__PageUrl` | 6h fresh + 24h stale fallback |
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

- **Backend:** xUnit + FluentAssertions + Moq (1019 tests: Domain 661, Application 118, Infrastructure 235+, Api 5)
- **Frontend:** Karma + Jasmine (configured, tests pending)
- Run `dotnet test` before commit

### MintStableJwt — AI verify-before-merge tool

`tests/InvestmentApp.Infrastructure.Tests/Tools/MintStableJwtTests.cs` is a self-executing xUnit test that mints a 30-day JWT for a hardcoded allowlisted test email (`investmate.support@gmail.com`). Used by AI to verify user-data-dependent flows on dev + prod when Google login blocks the AI browser.

- Allowlist is hardcoded in `StableJwtMint.ALLOWED_EMAILS` — adding emails requires a PR.
- Test 3 silently passes if `MINT_*` env vars are unset → CI-safe.
- Run: `MINT_EMAIL=... MINT_MONGO_CONN=... MINT_MONGO_DB=... MINT_JWT_KEY=... MINT_JWT_ISSUER=... MINT_JWT_AUDIENCE=... dotnet test --filter "FullyQualifiedName~MintStableJwt" --logger "console;verbosity=detailed"`
- One-time prereq: login Google with the test email once on each environment to seed the user record.

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

## Thesis-driven Plan Discipline (Vin-discipline)

Feature shipped 2026-04-23 (2 commits trên `fix/post-trade-review-tradeid-wiring`: d7a4bda domain/application/API/migration + 8fd0e8b discipline widget backend). Triết lý Vinpearl Air 2020 — dám dừng khi thesis bị phá vỡ. Chi tiết kế hoạch: [`docs/plans/plan-creation-vin-discipline.md`](plans/plan-creation-vin-discipline.md).

**Key files:**

- `src/InvestmentApp.Domain/Entities/TradePlan.cs` — rename `Reason` → `Thesis`; thêm `InvalidationCriteria`/`ExpectedReviewDate`/`LegacyExempt`; methods `SetThesis`/`SetInvalidationCriteria`/`SetExpectedReviewDate`/`AbortWithThesisInvalidation`; private `EnsureDisciplineGate()` fold vào `MarkReady()` + `MarkInProgress()`; `Restore()` clear `IsTriggered` flags.
- `src/InvestmentApp.Domain/Entities/InvalidationRule.cs` — value object + enum `InvalidationTrigger` (5 loại).
- `src/InvestmentApp.Domain/Events/TradePlanThesisInvalidatedEvent.cs` — domain event.
- `src/InvestmentApp.Application/TradePlans/Commands/AbortTradePlan/AbortTradePlanCommand.cs` — command + handler + `AbortTradePlanResult`.
- `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/*` + `UpdateTradePlan/*` — thêm `Thesis`/`InvalidationCriteria`/`ExpectedReviewDate`; giữ `Reason` deprecation shim 1 release.
- `src/InvestmentApp.Application/Discipline/Queries/GetDisciplineScoreQuery.cs` + DTOs (`DisciplineScoreDto`, `DisciplineComponents`, `DisciplinePrimitives`, `StopHonorRateDto`, `DisciplineSampleSize`).
- `src/InvestmentApp.Application/Discipline/Services/IDisciplineScoreCalculator.cs` — interface.
- `src/InvestmentApp.Infrastructure/Services/DisciplineScoreCalculator.cs` — implementation (hybrid formula + cache).
- `src/InvestmentApp.Api/Controllers/TradePlansController.cs` — endpoint `POST /api/v1/trade-plans/{id}/abort`.
- `src/InvestmentApp.Api/Controllers/DisciplineController.cs` — `GET /api/v1/me/discipline-score?days=90`.
- `src/InvestmentApp.Api/Program.cs` — DI registration (`IDisciplineScoreCalculator` + `IMemoryCache`).

**Migration:**

- `scripts/migrations/2026-04-23-tradeplan-thesis-rename.mongo.js` — **migration-first deploy gate**. Step 1: `$rename reason → thesis` + init `invalidationCriteria: []` + `expectedReviewDate: null` + `legacyExempt: true` cho mọi doc chưa migrated (filter `legacyExempt: { $exists: false }`). Step 2 idempotent: fill placeholder text cho `thesis: ""` rỗng. **Không dùng BsonElement alias** (MongoDB driver 3.6.0 chỉ hỗ trợ 1 key per property) — code mới deploy sau migration, nếu deploy trước sẽ silent data loss.

**Size-based discipline gate:** `Quantity × EntryPrice ≥ 5% AccountBalance` → bắt buộc `Thesis ≥ 30 chars` + ≥ 1 invalidation rule với `Detail ≥ 20 chars`; else `Thesis ≥ 15 chars`, rule optional. Object fact (không cheatable self-attestation như AllocationBucket).

**Discipline Score formula (hybrid):** SL-Integrity 50% (stop-honor rate − sl-widened-rate) + Plan Quality 30% (% plan pass gate) + Review Timeliness 20% (% plan review đúng hạn). Null sub-metric → re-normalize weights. Primitive: Stop-Honor Rate = trades lỗ đã đóng với `exitPrice ≥ plannedSL / tổng trades lỗ`. Rolling 90 ngày default.

**Tests (1106 total pass):** 23 Domain (TradePlanAbortTests + TradePlanDisciplineGateTests + TradePlanTests/TradePlanScenarioTests/TradePlanReviewTests updates) + 6 Application (DisciplineScoreCalculator + Abort handler) + 14 Infrastructure (DisciplineScoreCalculator integration + CampaignReview/Scenario service tests updates).

### V2.1 — Pending reviews page + locale vi-VN (merged PR #94 squash `304421dc`)

- `src/InvestmentApp.Application/TradePlans/Queries/GetPendingThesisReviews/GetPendingThesisReviewsQuery.cs` — query handler + DTOs (`PendingThesisReviewDto`, `PendingReviewReason`). Logic: iterate `GetActiveByUserIdAsync` results, filter Ready/InProgress, skip LegacyExempt, detect `InvalidationRule.CheckDate ≤ today+2` (VN local) OR `ExpectedReviewDate ≤ today`. Sort DESC theo `DaysOverdue`. `TimeZoneInfo` VN fallback chain: `SE Asia Standard Time` → `Asia/Ho_Chi_Minh` → UTC.
- `src/InvestmentApp.Api/Controllers/DisciplineController.cs` — thêm `GET /api/v1/me/thesis-reviews/pending`.
- `frontend/src/app/features/pending-reviews/pending-reviews.component.ts` — standalone component inline template, urgency color card (amber 0-2 ngày / red ≥ 3 ngày), `triggerTypeLabel()` helper map enum → Việt.
- `frontend/src/app/core/services/discipline.service.ts` — thêm `getPendingReviews()` + `PendingThesisReviewDto` type.
- `frontend/src/app/features/dashboard/widgets/discipline-score-widget.component.ts` — `shouldShow()` ẩn widget khi `totalPlans === 0`, reset score = null on period change (fix flash), load pending count → badge `🔔 [N] Plan cần review lý do đầu tư →`.
- `frontend/src/app/app.routes.ts` — route `/pending-reviews`.
- `frontend/src/main.ts` — register locale `vi-VN` (`registerLocaleData(localeVi, 'vi-VN', localeViExtra)`) + `{ provide: LOCALE_ID, useValue: 'vi-VN' }` — DatePipe/CurrencyPipe format kiểu VN default.
- Việt hóa 4 files UI: "Thesis" → "Lý do đầu tư" (widget + pending-reviews + trade-plan form + trade-replay). TypeScript identifiers giữ nguyên (`thesis` property, `ThesisTimeout` enum, route).

**Post-review fixes** (3-agent review trước merge): timezone VN day-granularity (tránh off-by-one UTC+7), `GetActiveByUserIdAsync` thay `GetByUserIdAsync` (DB-level filter), widget flash reset, skip `LegacyExempt`, badge hiển thị trigger type cụ thể (thay `reasonLabel` sinh "Điều kiện sắp tới hạn" chung chung).

**Tests:** 10 handler tests mới (`GetPendingThesisReviewsQueryHandlerTests`). 146/146 Application + 718/718 Domain + 249/249 Infrastructure pass.

## Dashboard Decision Engine (V1.1 P1+P2 — 2026-05-04, in-progress)

Plan: [`docs/plans/dashboard-decision-engine.md`](plans/dashboard-decision-engine.md). Hybrid sau review 2 sub-agent (UX + Architect), adopt 3 / bác 5 đề xuất từ layout V2 brainstorm. Roadmap 5 phase ship trong 3 PR (~2.5 tuần solo).

**PR-1 (P1+P2) shipped 2026-05-04:**

- `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` — thêm use-case `portfolio-critique` (adversarial coach role thay daily-briefing trên Dashboard). `BuildPortfolioCritiqueSystemPrompt` public static để test lock content (3 điểm phản biện, mệnh lệnh, KHÔNG khen, KHÔNG động viên). `BuildPortfolioCritiqueContext` reuse data aggregation từ `BuildDailyBriefingContext`. Use-case `daily-briefing` giữ nguyên cho backend reuse, không expose trên Dashboard nữa.
- `frontend/src/app/features/dashboard/widgets/networth-summary.component.ts` — standalone widget compact 3-line ở vị trí #2 trên Home, hiển thị Net Worth + Reality Gap CAGR (điểm % so với target 15%). Coexist với Personal Finance widget existing (full breakdown).
- `frontend/src/app/features/dashboard/dashboard.component.ts` — `cagrTargetSet=true` default (Reality Gap luôn hiển thị từ first load, không cần click "Đặt mục tiêu"). Reality Gap label đổi sang "điểm %" thay vì tỉ lệ. AI button rebrand "🥊 AI phản biện danh mục" + use-case `portfolio-critique`.
- `frontend/src/app/core/services/ai.service.ts` — thêm method `streamPortfolioCritique(question?)`.
- `frontend/src/app/shared/components/ai-chat-panel/ai-chat-panel.component.ts` — thêm route case `'portfolio-critique'`.

**Tests:** 6 xUnit (AiAssistantServicePortfolioCritiqueTests — lock prompt content adversarial, không drift sang supportive) + 9 Karma (NetWorthSummaryComponent — render/hide/gap label/boundary cases incl. negative CAGR). 295/295 Infrastructure + 14/14 Karma pass.
