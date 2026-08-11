# Architecture — Investment Mate v2

## Directory Structure

```
project/
├── src/
│   ├── InvestmentApp.Domain/           # Entities, Value Objects, Events (zero dependencies)
│   │   ├── Entities/                   # 24 aggregate roots + nested classes (incl. FinancialProfile, ApiKey)
│   │   ├── ValueObjects/               # Money, StockSymbol, Position, WatchlistItem, RoutineItem, ScenarioNode, TrailingStopConfig
│   │   └── Events/                     # 14 domain event types
│   │
│   ├── InvestmentApp.Application/      # CQRS handlers, interfaces, DTOs (depends on Domain)
│   │   ├── {Feature}/Commands/         # Write operations (MediatR IRequestHandler)
│   │   ├── {Feature}/Queries/          # Read operations
│   │   ├── Common/                     # Hàm thuần dùng chung: PortfolioCashCalculator, PositionBuilder (nguồn DUY NHẤT dựng vị thế — mọi service cần giá vốn/số lượng phải gọi vào đây, không tự GroupBy trên Trade thô), CorporateActionAdjuster (điều chỉnh giá ngưỡng tại thời điểm đọc), TradePlanPriceAdjuster (quy giá trên TradePlan về mặt bằng hiện tại). Xem ADR-0010
│   │   ├── Common/Interfaces/          # Service interfaces (AI, Risk, Performance, Market, ComprehensiveStockData, ScenarioEvaluation, IApiKeyTokenService)
│   │   ├── Common/Behaviors/           # MediatR pipeline behaviors (ValidationBehavior<,>)
│   │   ├── RepositoryInterfaces.cs     # All repository interfaces (~23, incl. IApiKeyRepository)
│   │   └── Services/                   # FeeCalculationService (app-level)
│   │
│   ├── InvestmentApp.Infrastructure/   # Implementations (depends on Application + Domain)
│   │   ├── Services/                   # 20+ service implementations
│   │   │   ├── Hmoney/                 # 24hmoney market data, comprehensive stock data + gold price crawler
│   │   │   │   ├── HmoneyComprehensiveDataProvider.cs  # Comprehensive stock analysis data
│   │   │   │   ├── HmoneyComprehensiveApiModels.cs     # API response models
│   │   │   │   └── HmoneyGoldPriceProvider.cs          # Vàng Miếng/Nhẫn scrape (AngleSharp HTML parse)
│   │   │   └── Tcbs/                   # TCBS fundamental data provider
│   │   └── Repositories/              # 25 MongoDB repositories (incl. FinancialProfileRepository, ApiKeyRepository)
│   │
│   └── InvestmentApp.Api/              # Controllers, DI, middleware (depends on all)
│       ├── Controllers/               # 38 API controllers (incl. PersonalFinanceController, InternalJobsController, ApiKeysController, AiDigestController, AiAgentController + 7 AiAgent* expose controllers on AiAgentControllerBase: Positions, Watchlists, JournalEntries, Journals, Symbols, Portfolios, Fees)
│       ├── Mcp/                        # MCP server: 11 [McpServerToolType] classes = 46 tools (29 mirroring AiAgent* surface + 8 P0 decision/risk + 1 daily digest + 8 P1 analytics; mapped at /mcp, ApiKey scheme)
│       ├── Auth/                      # SchedulerEmailAllowlist, GcpOidcExtensions (Cloud Scheduler OIDC), ApiKeyAuthExtensions (per-user PAT scheme)
│       ├── Authorization/             # RequireAdminAttribute
│       ├── Middleware/                # ImpersonationValidationMiddleware, CorrelationId, Exception
│       ├── Services/                  # BacktestQueueService (in-process queue, replaces Worker poll)
│       └── Program.cs                 # DI registration, middleware pipeline
│
├── frontend/                           # Angular 18 SPA
│   └── src/app/
│       ├── core/services/              # 26 Angular services (HTTP clients, incl. api-keys.service.ts)
│       ├── features/                   # 30 page components (standalone, inline templates)
│       │   ├── dashboard/              # Investor Cockpit (main page + Personal Finance widget)
│       │   ├── trade-wizard/           # 5-step disciplined trading flow
│       │   ├── trade-plan/             # Entry/SL/TP planning with checklist
│       │   ├── market-data/            # Stock detail + technical analysis
│       │   ├── analytics/              # Performance metrics, equity curve
│       │   ├── campaign-analytics/     # Cross-plan campaign review analytics (P0.7)
│       │   ├── risk-dashboard/         # Risk score, drawdown, VaR
│       │   ├── personal-finance/       # Net worth + Gold/Savings tracking + health score (Tier 3)
│       │   ├── api-keys/               # Personal Access Tokens management (route /api-keys)
│       │   ├── company-dossier/        # Hồ sơ công ty — gate chặn tạo trade plan (routes /company-dossier, /company-dossier/:symbol)
│       │   └── ...                     # (19 more feature pages)
│       └── shared/
│           ├── components/             # AiChatPanel, Header, PwaInstallBanner, etc.
│           ├── directives/             # UppercaseDirective, NumMaskDirective, SymbolLinkDirective
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
    ├── adr/
    │   └── 0003-per-user-api-keys.md       # Per-user Personal Access Tokens (PAT) cho non-interactive API access
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
| ApiKey | Per-user Personal Access Token. Lưu `KeyHash` (SHA-256 of plaintext token — plaintext chỉ trả về 1 lần lúc tạo), `UserId`, `Name`, `CreatedAt`, `LastUsedAt`, `IsRevoked`. Ownership-checked trên revoke. |
| CompanyDossier | **Hồ sơ công ty — gate chặn tạo trade plan (2026-08-10, ADR-0011).** Khóa `(UserId, Symbol)`, sống độc lập với `TradePlan` — viết một lần cho một mã, mọi plan sau cho mã đó dùng lại. 4 khối: `BusinessModel` (string), `Moats` (`List<MoatItem>`), `RiskFactors` (`List<RiskFactor>`, rank dense 1..N, mỗi cái bắt buộc `ObservableSignal`, tối đa 1 `IsDealBreaker`), `Notes` (tự do, không gate). Hai phương thức sửa riêng biệt `UpdateByOwner`/`UpdateByAgent` (không dùng cờ `isAgent`) — chỉ `UpdateByAgent` xóa `ConfirmedAt`. `Confirm()` là **phương thức duy nhất** đẩy đồng hồ hạn tươi (`ReviewedAt`) — sửa nội dung, kể cả qua UI, không chạm nó. `GetFreshness()` trả enum `Unconfirmed`/`Fresh` (<90 ngày)/`NeedsReview` (90-179)/`Expired` (≥180), tính theo ngày lịch VN offset cố định `+07:00` (không dùng `TimeZoneInfo` — xem ADR-0011 D7). |

## Key Services (Infrastructure Layer)

| Service | Responsibility | Key Dependencies |
|---------|---------------|-----------------|
| PnLService | P&L realized + unrealized. **Từ 2026-08-08 (ADR-0010) tính qua `PositionBuilder`** — không còn tự gộp `Trade` thô, không còn hard-code `"USD"`, có tính phí/thuế. Trả thêm `SettledQuantity`/`PendingQuantity`/`DividendNet`/`PendingDividend`/`TotalPnLWithDividend` | ITradeRepository, IStockPriceService, **ICorporateActionRepository** |
| RiskCalculationService | VaR(95%), max drawdown, position sizing, correlation matrix, portfolio optimization (concentration/sector/correlation), trailing stop alerts, hạn mức rủi ro ngày, stress test. **Giá vào/cắt lỗ/mục tiêu điều chỉnh qua `CorporateActionAdjuster` tại thời điểm đọc**; số lượng và giá vốn lấy từ `PositionBuilder` — cả 4 method đã đấu nối (ADR-0010) | IPnLService, ISnapshotRepo, IRiskProfileRepo, IFundamentalDataProvider, **ICorporateActionRepository** |
| PerformanceMetricsService | CAGR, Sharpe, Sortino, win rate, profit factor, equity curve | ISnapshotRepo, ITradeRepo |
| PositionSizingService | 5 position sizing models: Fixed Risk, ATR-Based, Kelly Criterion (Half-Kelly, 25% cap), Turtle (1-unit entry), Volatility-Adjusted (ATR% scaling). Pure calculation, no DB dependencies | None (stateless) |
| TechnicalIndicatorService | 10 indicators: EMA(20/21/50/200), RSI(14), MACD(12,26,9), Stochastic(14,3,3), ADX(14)+DI, OBV, MFI(14), Bollinger(20,2), ATR(14), Volume ratio. S/R, Fibonacci, 10-indicator voting signal, Confluence Score (0-100 weighted), Market Condition Classifier (ADX-based), Divergence Detection (RSI/MACD vs Price) | IMarketDataProvider |
| AiAssistantService | AI prompt building for 13 use cases (incl. **portfolio-critique** 2026-05-04 — adversarial HLV phản biện coach role, replace daily-briefing trên Dashboard, ép 3 điểm phản biện + động từ mệnh lệnh, KHÔNG khen). Streaming responses + non-streaming `BuildContextAsync`. `BuildPortfolioCritiqueSystemPrompt` public static để test lock content. **`BuildDailyDigestAsync` (2026-07-15, ADR-0003)** cho endpoint ApiKey `daily-digest`; `BuildDailyBriefingContext` được bổ sung section `<cash_and_net_worth>` (từ `FinancialProfile` domain methods + portfolio value làm securities) + position-sizing cho pending plans (`IPositionSizingService`, chỉ khi entry/SL/vốn hợp lệ). Helpers public static `ShouldComputeSizing` / `BuildPlanSizingRequest` / `FormatCashNetWorthSection` để test. **Bản tin mở rộng (2026-07-26, ADR-0007):** `<cash_and_net_worth>` nay tách `<portfolio_cash>` (tiền trong TK chứng khoán, tính bằng `PortfolioCashCalculator`) khỏi `<idle_cash>` (hồ sơ tài chính) và **luôn in** kể cả khi chưa có hồ sơ — trước đây cả block bị bọc trong `if (profile != null)` nên tiền bán vô hình, khiến position sizing tính trên nền vốn thiếu. Thêm `<portfolio_overview>` bóc theo từng danh mục + `<realized_pnl>`, `<positions>` (danh mục/KL/giá vốn/%DM/khoảng cách SL), `<recent_trades>` (14 ngày), `<decision_queue>` (qua `IMediator`), `<risk_alerts>` theo luật rủi ro (xuyên SL / sát SL ≤3% / tập trung ≥30% / chưa đặt SL / lỗ ≤−15%), `<drill_down>`. Hai chỉ số lợi nhuận tách mẫu số: `<unrealized_return>` trên giá vốn phần đang nắm, `<total_return>` trên tổng tiền đã mua. Luật cứng: giá trị chưa fetch được in `n/a`, không bao giờ in `0`. Formatter đều là static thuần → unit-test trực tiếp; thêm `AiAssistantServiceDigestWiringTests` dựng service thật với repo mock (endpoint thật dùng ApiKey scheme nên không verify được bằng JWT). | 12+ repos and services (+ IFinancialProfileRepository, IPositionSizingService, **ICapitalFlowRepository**, **IMediator** — chỗ đầu tiên Infrastructure dùng MediatR, chọn có ý thức để MCP tool và REST endpoint không thể lệch số liệu) |
| HmoneyComprehensiveDataProvider | Comprehensive stock data from 24hmoney (financials, reports, dividends, foreign trading, recommendations) | HttpClient, IMemoryCache |
| HmoneyMarketDataProvider | Real-time prices from 24hmoney.vn (prices ×1000 scaling) | HttpClient, IMemoryCache |
| HmoneyGoldPriceProvider | Vàng Miếng + Nhẫn từ `24hmoney.vn/gia-vang` (HTML scrape với AngleSharp, không có JSON API). Filter 4 brand × 2 type, values là full VND (không scale). Two-tier cache: fresh 5 phút + stale 6h fallback khi 24hmoney down | HttpClient, IMemoryCache |
| HmoneyBankRateProvider | **So sánh với tiết kiệm (2026-04-24)** — top lãi suất theo kỳ hạn (1/3/6/9/12 tháng) từ `24hmoney.vn/lai-suat-gui-ngan-hang` (SSR HTML, AngleSharp). Ưu tiên table online (cao hơn quầy 0.2-0.8%). Two-tier cache: fresh 6h + stale 24h. Env var `BankRateProvider__PageUrl` bắt buộc set trước deploy. Startup warning nếu placeholder chưa resolve | HttpClient, IMemoryCache |
| HypotheticalSavingsReturnService | Pure math — "nếu cash flows của portfolio đã gửi tiết kiệm @ r, số dư cuối là?". Running-balance iterative, monthly compound `(1+r/12)^months`. Caller filter Deposit/Withdraw (loại Dividend/Interest/Fee tránh double-count). No DI dependencies | None (stateless) |
| TcbsFundamentalDataProvider | P/E, EPS, ROE from TCBS API | HttpClient, IMemoryCache |
| SnapshotService | Daily portfolio snapshots with position weights | IPnLService |
| AlertEvaluationService | Price/drawdown/portfolio value alerts | ISnapshotRepo, IStockPriceRepo |
| ScenarioEvaluationService | Auto-evaluate scenario playbooks every 15 min, trigger actions, create AlertHistory. **Giá trên kế hoạch quy về mặt bằng hiện tại qua `TradePlanPriceAdjuster`** (mốc `TradePlan.PricesSetAt`); trạng thái trượt `HighestPrice`/`CurrentTrailingStop` được rebase một lần và đánh dấu bằng `TrailingStopConfig.PriceBasisAt` | ITradePlanRepo, IStockPriceService, **ICorporateActionRepository** |
| BehavioralAnalysisService | Detect FOMO, panic sell, revenge trading, overtrading patterns | JournalEntry, Trade data |
| CampaignReviewService | Auto-calculate P&L metrics for campaign review (amount, %, VND/ngày, annualized return, target achievement) | ITradeRepository, IPnLService |
| VietstockEventProvider | Crawl news + corporate events from Vietstock API (CSRF token flow) | HttpClient |
| DisciplineScoreCalculator | **Vin-discipline widget backend (2026-04-23)** — tính điểm kỷ luật thesis hybrid: SL-Integrity 50% + Plan Quality 30% + Review Timeliness 20%. Stop-Honor Rate primitive (trades lỗ đã đóng với exitPrice ≥ plannedSL / tổng lỗ). Null-safe re-normalize khi sub-metric thiếu denominator. Multi-lot per-lot matching theo `TradeIds`. Cache 5 phút, invalidate on `TradeClosedEvent`/`PlanReviewedEvent`/`TradePlanThesisInvalidatedEvent` | ITradePlanRepository, ITradeRepository, IMemoryCache |
| ApiKeyTokenService | Implements `IApiKeyTokenService`. Generate cryptographically random plaintext token, hash via SHA-256, return both (plaintext returned to caller once, only hash persisted). Verify incoming token by hash lookup. | IApiKeyRepository |

## API Endpoints (29 Controllers)

| Controller | Base Route | Key Operations |
|-----------|-----------|----------------|
| Auth | `/api/v1/auth` | Google OAuth, JWT token |
| Portfolios | `/api/v1/portfolios` | CRUD, list by user |
| Trades | `/api/v1/trades` | CRUD, bulk create, link to plan/strategy |
| TradePlans | `/api/v1/trade-plans` | CRUD, status transitions, lot execution, scenario node trigger, scenario templates, **campaign review (P0.7)**: close with auto-metrics, preview, update lessons, pending-review list, cross-plan analytics, **abort với thesis invalidation (Vin-discipline, 2026-04-23)**: `POST /{id}/abort { trigger, detail }` → `AbortTradePlanCommand` → raise `TradePlanThesisInvalidatedEvent` |
| Discipline | `/api/v1/me/discipline-score` | **Vin-discipline widget (2026-04-23)** — `GET ?days=7|30|90|365` (default 90). Query `GetDisciplineScoreQuery` → `IDisciplineScoreCalculator`. Cache 5 min. |
| MarketData | `/api/v1/market` | Price, batch prices, search, overview, top fluctuation |
| PnL | `/api/v1/pnl` | Portfolio/position P&L |
| Risk | `/api/v1/risk` | Summary, drawdown, VaR, correlation, stop-loss targets, **stress-test (P2)**, **budget (P4)**, **sector-exposure (ADR-0012)** |
| Analytics | `/api/v1/analytics` | Performance, equity curve, monthly returns, **vs-savings comparison (2026-04-24)** — `GET /portfolio/{id}/vs-savings?savingsRate=&asOf=` + `GET /bank-rates` (top 12T từ 24hmoney), **household CAGR (2026-05-03)** — `GET /household/performance` returns aggregated TWR + CAGR across all of caller's portfolios with `isStable` flag (true ⇔ snapshot window ≥ 365 ngày) |
| Ai | `/api/v1/ai` | Build context, stream responses, daily briefing, comprehensive analysis |
| AiSettings | `/api/v1/ai-settings` | Provider/key management |
| Alerts | `/api/v1/alerts` | Rules CRUD, history, unread count |
| Watchlists | `/api/v1/watchlists` | CRUD, items, VN30 import |
| Strategies | `/api/v1/strategies` | CRUD, performance, templates |
| Journals | `/api/v1/journals` | CRUD, link to trade |
| CapitalFlows | `/api/v1/capital-flows` | Record, history, adjusted returns (TWR/MWR) |
| CorporateActions | `/api/v1/corporate-actions` | Sự kiện quyền: list theo danh mục, tạo, xác nhận đã về (`/{id}/settle`), xoá. Ownership check theo chuỗi portfolio → action (ADR-0010) |
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
| ApiKeys | `/api/v1/api-keys` | **Personal Access Tokens (ADR-0003)** — JWT-authed. `POST /` create (201, returns plaintext token once only); `GET /` list caller's keys; `DELETE /{id}` revoke (204, ownership-checked). |
| AiDigest | `/api/v1/ai/daily-digest` | **First ApiKey-scheme opt-in endpoint (ADR-0003, 2026-07-15)** — `[Authorize(Scheme=ApiKey)]` (header `X-Api-Key`, KHÔNG JWT). `POST` → `{ systemPrompt, userMessage }` = daily-briefing context + cash/net-worth + position-sizing + market-context (VN-Index/breadth/foreign, qua `IMarketDataProvider`) + watchlist (giá + distance-to-target). Controller riêng `AiDigestController` vì gộp vào `AiController` (JWT class-level) sẽ khiến 2 `[Authorize]` khác scheme cộng dồn (AND). Scope theo `sub` = owner của khóa. |
| AiAgent | `/api/v1/ai/agent` | **ApiKey-scheme write-surface (ADR-0004, 2026-07-21)** — `[Authorize(Scheme=ApiKey)]`. Re-dispatches existing MediatR commands để NPU/Claude có thể lập/sửa/chuyển-trạng-thái/thực-hiện trade plans + ghi trade programmatically. Endpoints: `GET trade-plans`, `GET trade-plans/{id}`, `POST trade-plans` (forces Draft), `PUT trade-plans/{id}`, `PATCH trade-plans/{id}/status` (blocks `restore` → 400), `POST trades`, `GET doc` (embedded API reference, ETag=docVersion). Audit marker `Source=AI_AGENT` trong `Metadata`. Controller riêng (`AiAgentController`) — same pattern as `AiDigestController`. IDOR fix: `CreateTradeCommand` + `BulkCreateTradesCommand` handlers now assert `portfolio.UserId == sub` (ownership check, not just existence). Docs: `src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md`. |
| AiAgent (expose) | `/api/v1/ai/agent/{positions,watchlists,journal-entries,journals,symbols}` | **ApiKey-scheme read/write expansion (extends ADR-0004, 2026-07-23)** — 5 sibling controllers (`AiAgentPositionsController`, `AiAgentWatchlistsController`, `AiAgentJournalEntriesController`, `AiAgentJournalsController`, `AiAgentSymbolsController`) sharing `AiAgentControllerBase` (`IMediator` + `GetUserId()`). 21 routes mirroring the JWT `PositionsController`/`WatchlistsController`/`JournalEntriesController`/`JournalsController`/`SymbolTimelineController` — re-dispatch existing MediatR, `UserId` from `sub`, zero new business logic. Response codes mirror source; POST `Created` Location → agent surface. Watchlist/journal writes are low-stakes (no "chốt" gate). Doc: same embedded `AI-Agent-TradePlan-API.md` (+5 sections). |
| MCP | `/mcp` | **Model Context Protocol server (2026-07-24)** — co-hosted in `InvestmentApp.Api` via `ModelContextProtocol.AspNetCore` (`AddMcpServer().WithHttpTransport(Stateless).WithToolsFromAssembly()` + `app.MapMcp("/mcp").RequireAuthorization(ApiKey)`). Streamable HTTP, **stateless** (survives Cloud Run multi-instance). Exposes **46 schema-typed tools** (11 `[McpServerToolType]` classes in `Mcp/`: TradePlan, Trade, Portfolio, Symbol, Watchlist, Journal, JournalEntry, Decision, Risk, Digest, Analytics) — each `[McpServerTool]` re-dispatches the same MediatR command/query; `UserId` from `sub` via `IHttpContextAccessor`. 29 tools mirror the `AiAgent*Controller` surface; **8 P0 read-only decision/risk tools (2026-07-25)** expose queries with no REST agent equivalent: `get_decision_queue`, `get_discipline_score`, `get_discipline_streak`, `get_pending_thesis_reviews` (`DecisionTools`) + `get_portfolio_risk`, `get_stop_loss_targets`, `get_trailing_stop_alerts`, `get_scenario_advisories` (`RiskTools`); **`get_daily_digest` (`DigestTools`, 2026-07-26)** — thin wrapper trên `IAiAssistantService.BuildDailyDigestAsync` (cùng payload REST `POST /ai/daily-digest`), `ErrorMessage` → `McpException`; bước Phase B để NPU `/stock` agent bỏ curl; **8 P1 read-only analytics tools (2026-07-26, `AnalyticsTools`)**: `get_performance`, `get_equity_curve`, `get_monthly_returns`, `get_savings_comparison`, `get_campaign_analytics`, `get_net_worth_summary`, `get_flow_history`, `get_adjusted_return` (TWR/MWR) — 6/8 per-portfolio (required `portfolioId`, ownership in handlers). Read tools carry `ReadOnly`, writes carry `Destructive` (host prompts confirm). **Tool params phải FLAT (2026-07-28, ADR-0008)** — không nhận trực tiếp MediatR command làm tham số, vì SDK sẽ sinh schema bọc `{"command":{…}}` và caller gửi args phẳng bị `ArgumentException: missing … required parameter 'command'`. Optional params đặt sau `ct` với default `= null` để không lọt vào `required`. Guard: `McpToolDiscoveryTests.No_Tool_Wraps_Its_Args_In_A_Command_Object` + `McpToolArgumentBindingTests` (invoke qua SDK binder, không gọi trực tiếp static method). Additive — REST `/ai/agent/*` unchanged; MCP replaces the markdown `/doc` with `tools/list` discovery. |
| CompanyDossiers | `/api/v1/company-dossiers` | **Hồ sơ công ty (2026-08-10, ADR-0011)** — JWT. `GET` list, `GET /{symbol}`, `PUT /{symbol}` upsert (luôn `ByAgent=false`), `POST /{symbol}/confirm` (chỉ đường duy nhất đặt `ConfirmedAt`), `GET /{symbol}/gate-status` (pre-flight check trước khi tạo plan; `quantity`/`entryPrice`/`accountBalance` **bắt buộc**, thiếu → 400 — thiếu 1 trong 3 mà đoán bằng 0 sẽ chấm nhầm bậc so với `POST /trade-plans` thật). |
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
- **Header nav "Quản lý" group** includes: ..., "Khóa API" → `/api-keys`

## PWA

- **Service Worker:** `@angular/service-worker` (ngsw), enabled in production + staging builds
- **Manifest:** `frontend/src/manifest.webmanifest` — display: standalone, theme: #2563eb
- **Icons:** `frontend/src/assets/icons/` — SVG icons 72→512px
- **Caching strategy:** App shell prefetch; API data groups with freshness/performance strategies
- **ngsw-config:** `frontend/ngsw-config.json`

## Database

- **MongoDB** (Atlas cloud in production)
- Repositories use generic `IRepository<T>` base with entity-specific extensions
- **Indexes:** Compound indexes on (portfolioId + symbol), (userId + date), unique constraints on snapshots; `api_keys` collection: unique index on `KeyHash` + index on `UserId`
- **Soft delete** pattern: `IsDeleted` flag, filtered in queries

## Testing

- **Backend:** xUnit + FluentAssertions + Moq (~1596 tests: Domain, Application, Infrastructure, Api)
- **Frontend:** Karma + Jasmine (152 tests)
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

**Tập trung ngành — chỉ hiển thị, không chặn (ADR-0012, 2026-08-10):** `RiskProfile.MaxSectorExposurePercent` (mặc định 40%) trước đây là **luật chết**: `RiskCalculationService` tra ngành qua `IFundamentalDataProvider`, mà interface đó được đăng ký là `NoOpFundamentalDataProvider` (`Program.cs` — dòng TCBS thật bị comment) nên luôn trả `null`; mọi vị thế rơi vào rổ "Không xác định" và rổ đó hardcode `IsOverweight = false`. Nay ngành đọc qua `IComprehensiveStockDataProvider` (`.Company.Industry`, 24hmoney `GroupName`, cache 5 phút) — provider này **đã** được đăng ký và **đã** được inject sẵn vào service, nên không cần chạm DI. Rổ "Không xác định" cũng được so hạn mức.

- `GET /api/v1/risk/portfolio/{portfolioId}/sector-exposure?symbol=&addValue=` → `SectorExposureForPlan { Symbol, Sector?, CurrentPercent?, ProjectedPercent?, LimitPercent, SameSectorSymbols[] }`. `symbol` và `addValue` **bắt buộc**. Query handler kiểm quyền sở hữu portfolio rồi gọi `IRiskCalculationService.GetSectorExposureForPlanAsync` — phép tính đặt ở service, **không** ở handler, để công thức `totalValue` chỉ tồn tại một chỗ.
- **Hai bất biến dễ phá:** (1) `ProjectedPercent = (sectorValue + addValue) / totalValue` — mẫu số **không** cộng `addValue`, vì `totalValue = Math.Max(giá trị vị thế + tiền mặt, giá trị vị thế)` đã gồm tiền mặt nên mua bằng tiền trong danh mục không làm tổng đổi. (2) Không tra được ngành hoặc `totalValue ≤ 0` ⇒ trả **`null`**, UI hiện "n/a" — không trả `0`, vì `0%` nghĩa là "chưa giữ gì ngành này".
- Dùng chung một chỗ: `ComputeTotalValue(...)` và `ResolveIndustryAsync(...)` trong `RiskCalculationService`, cả đường optimization lẫn đường sector-for-plan đều gọi.
- FE: `RiskService.getSectorExposureForPlan`, hiện một dòng trong **đúng** khối cảnh báo kiểm-trước của `trade-plan.component.ts` (cùng `forkJoin` với `gate-status`, cùng debounce 500ms). Khung màu trung tính có chủ đích — khối vàng/đỏ ở trên là cảnh báo chặn, khối này không phải và **không disable nút nào**.
- `IFundamentalDataProvider` sau thay đổi này không còn được `RiskCalculationService` dùng tới nhưng vẫn nằm trong constructor (bỏ ra là sửa chữ ký + 5 harness test, cố ý để lại).

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

## Dashboard Decision Engine (V1.1 — 2026-05-04, in-progress)

Plan: [`docs/plans/done/dashboard-decision-engine.md`](plans/done/dashboard-decision-engine.md). Hybrid sau review 2 sub-agent (UX + Architect), adopt 3 / bác 5 đề xuất từ layout V2 brainstorm. Roadmap 5 phase ship trong 3 PR (~2.5 tuần solo).

**Tín hiệu phía vào lệnh (2026-08-09, [ADR-0009](adr/0009-decision-queue-entry-side-signals.md)):** `GetDecisionQueueQuery` từ **3 → 5 nguồn**. Handler nhận thêm `IWatchlistRepository` + `IStockPriceService` (cả hai `AddScoped`, `Program.cs:125,137`), task cơ hội chạy song song trong `Task.WhenAll` sẵn có.

- `MissingStopLoss` (Warning) — tái dùng `PortfolioRiskSummary` đã fetch, không tốn thêm I/O. Guard `CurrentPrice <= 0` **dời lên trước** nhánh null-SL để giá fail-fetch không bị đọc thành "thiếu SL".
- `BuyOpportunity` (Info, `PortfolioId` rỗng → thoát dedupe) — chỉ fetch giá cho mã có `TargetBuyPrice > 0`; `IStockPriceService.GetCurrentPricesAsync` **không nhận `CancellationToken`** nên bọc `WaitAsync(5s, ct)` thủ công; lỗi/timeout → trả rỗng theo idiom `catch { skip }` của `AiAssistantService`, không làm hỏng queue. Dictionary giá bọc lại `StringComparer.OrdinalIgnoreCase` vì provider có thể trả key khác hoa/thường.
- **Suppression vá bug có sẵn:** `LoadResolvedTodayAsync` trả thêm tập thứ ba `(Symbol, Type)` dựng từ tag `trigger:{Type}`, chỉ nhận journal có **cả** `PortfolioId` lẫn `TradePlanId` null. Trước đó `HandleHoldWithJournalAsync` nhánh symbol-only để `portfolioId` null còn `LoadResolvedTodayAsync` lại lọc bỏ đúng entry đó → resolve `StopLossHit` xong card hiện lại ngay. Hai tập cũ giữ nguyên nên toàn bộ test suppression cũ vẫn xanh không sửa dòng nào.
- Frontend: `typeLabel`/`getActionRoute` chuyển từ chuỗi `if` fallthrough sang `Record<DecisionType, …>` — thêm type mới mà quên nhãn là **lỗi biên dịch**, kèm fallback runtime (`?? 'Khác'` / `/symbol-timeline`) cho trường hợp FE cache cũ gặp API mới.

**PR-3 (P4 + P5) shipped 2026-05-04:**

- `src/InvestmentApp.Domain/Entities/JournalEntry.cs` — thêm `JournalEntryType.Decision` enum value (additive, no migration). Dùng cho `HoldWithJournal` flow trong P4.
- `src/InvestmentApp.Application/Decisions/Commands/ResolveDecision/ResolveDecisionCommand.cs` — command + validator + handler. Hai action:
  - `ExecuteSell`: load `TradePlan` (validate UserId match), load `Portfolio` (defense-in-depth: validate UserId match), tính quantity (single-lot = `plan.Quantity`, multi-lot = sum `lot.PlannedQuantity` của Executed lots), lấy giá hiện tại qua `IStockPriceService.GetCurrentPriceAsync`, tạo Trade SELL + `LinkTradePlan(planId)` + `portfolio.AddTrade(trade)` + `_portfolioRepository.UpdateAsync`. Throw nếu position đã đóng (qty ≤ 0) hoặc giá fail-fetch.
  - `HoldWithJournal`: validate note ≥ 20 chars (Trim), tạo `JournalEntry` với `EntryType=Decision`, `Title="Quyết định giữ — {symbol}"`, `Content=note`, `Tags=["decision-hold", "trigger:{decisionType}"]`, link plan nếu có. Fallback dùng `request.Symbol` khi không có plan (StopLossHit).
- `src/InvestmentApp.Api/Controllers/DecisionsController.cs` — thêm `POST /api/v1/decisions/{id}/resolve` với body `ResolveDecisionRequest { Action, TradePlanId, Symbol, Note }` (PascalCase JSON keys). UserId từ JWT claim.
- `frontend/src/app/core/services/decision.service.ts` — thêm `resolve(decisionId, request)` method gọi POST endpoint. Body PascalCase keys (per `learning_toolquirk_api_pascalcase_required.md`). Types `DecisionAction`, `ResolveDecisionRequest`, `ResolveDecisionResult`.
- `frontend/src/app/features/dashboard/widgets/decision-queue.component.ts` — thêm inline action UI:
  - `🔪 BÁN THEO KẾ HOẠCH` button: chỉ hiện khi `item.tradePlanId` non-empty (StopLossHit từ DTO không carry tradePlanId, dùng "Xử lý →" link điều hướng `/risk-dashboard` thay). `window.confirm` trước khi POST resolve. Optimistic remove khỏi list sau khi success.
  - `✋ GIỮ + GHI LÝ DO` button: expand inline note textarea (≥ 20 chars để enable submit). Counter hiển thị real-time `{{n}}/20 ký tự`. Hủy clear draft.
  - Per-item error map (`resolveErrors: Record<string, string>`) — hiện error cho cả BÁN lẫn GIỮ flow tại item-level.
- `frontend/src/app/features/dashboard/dashboard.component.ts` — XÓA 3 widget noise (P5):
  - **Market Index strip** (~20 dòng template + `marketOverview` field + `loadMarketOverview` method). Đã có ở `/market-data`.
  - **Mini Equity Curve chart** (~20 dòng template + `@ViewChild('miniEquityCanvas')` + `miniEquityChart` Chart instance + `selectedRange` + `equityRanges` array + `setEquityRange` method + `renderMiniEquityChart` method ~100 LOC). Full version ở `/analytics`. **GIỮ `equityCurveData` + `loadEquityCurve`** vì period stats badge ở timeframe selector vẫn phụ thuộc.
  - **Quick Actions row** (~52 dòng template với 4 link Wizard/Market/Journals/Risk). Trùng với header menu + bottom-nav.
  - Bỏ orphan imports `MarketOverview`, `ViewChild`, `ElementRef`. Net delete ~237 LOC.

**Tests PR-3:** 11 xUnit (`ResolveDecisionCommandHandlerTests` — single-lot/multi-lot/short-note/link-plan/user-isolation/portfolio-ownership/plan-not-found/no-executed-lots/symbol-fallback/validator) + 7 Karma (BÁN call API + cancel confirm + expand note form + disabled short note + optimistic remove + hide BÁN no plan + show BÁN error). 191/191 Application + 729/729 Domain + 30/30 dashboard widget Karma pass.

**Plan deviations từ spec gốc:**

- Domain field naming khác plan: `Trade.Price` (không phải `EntryPrice`), `Trade.TradePlanId` set qua `LinkTradePlan()` method (không qua constructor), `JournalEntry.EntryType`+`Title`+`Content` (không phải `Type`+`Body`), `TradeType.BUY/SELL` uppercase, `PlanLotStatus` (không phải `LotStatus`), `PlanLot.PlannedQuantity` (multi-lot sum này thay vì `Quantity`).
- Single-lot quantity dùng `plan.Quantity` (không phải `plan.PlannedQuantity` — plan dùng property tên khác).
- `ExecuteSell` chỉ enable khi item có `tradePlanId` — StopLossHit không carry tradePlanId trong DTO hiện tại nên chỉ có "Xử lý →" + GIỮ. Spec cũ giả định mọi action đều available; deviation align với "BÁN THEO KẾ HOẠCH" yêu cầu plan thật sự.
- `equityCurveData` + `loadEquityCurve` GIỮ trên dashboard (plan §7 nói xóa) vì period stats badge ở timeframe selector (lines ~280-310) phụ thuộc data này. Chỉ chart visualization được xóa.
- Defense-in-depth: thêm portfolio ownership check sau khi load plan (sub-agent review surface — plan owner và portfolio owner không nhất thiết cùng user).

**PR-2 (P3 — Decision Queue read-only) shipped 2026-05-04:**

- `src/InvestmentApp.Application/Decisions/DTOs/DecisionItemDto.cs` — `DecisionItemDto`, `DecisionQueueDto`, enums `DecisionType` (StopLossHit / ScenarioTrigger / ThesisReviewDue), `DecisionSeverity` (Critical / Warning / Info). View-model thuần — không persist; Id là composite `{type}:{sourceId}`.
- `src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs` — handler aggregate 3 nguồn: (1) per-portfolio `IRiskCalculationService.GetPortfolioRiskSummaryAsync` filter `DistanceToStopLossPercent ≤ 2%` (≤ 0 = Critical, ≤ 2 = Warning), (2) `IScenarioAdvisoryService.GetAdvisoriesAsync` (Warning), (3) `GetPendingThesisReviewsQuery` qua MediatR (DaysOverdue ≥ 3 = Critical, else Warning). Dedupe theo (Symbol, PortfolioId) giữ severity cao nhất, tie-break ưu tiên StopLossHit. Sort severity desc → DueAt asc. 3 source query song song qua `Task.WhenAll`.
- `src/InvestmentApp.Application/Discipline/Queries/GetDisciplineStreakQuery.cs` — handler tính `daysWithoutViolation` cho empty state positive. Logic: số ngày kể từ exit gần nhất của closed loss trade KHÔNG tôn trọng plan SL (Buy: avgExit < SL; Sell: avgExit > SL — mirror logic của `DisciplineScoreCalculator.ComputeSlIntegrityAndStopHonor`). Nếu chưa có violation → days kể từ plan đầu tiên. `HasData = false` khi user chưa có plan nào — UI ẩn streak badge nhưng vẫn show empty state.
- `src/InvestmentApp.Api/Controllers/DecisionsController.cs` — `GET /api/v1/decisions/queue` → `DecisionQueueDto`. JWT-authorized.
- `src/InvestmentApp.Api/Controllers/DisciplineController.cs` — thêm `GET /api/v1/me/discipline-score/streak` → `DisciplineStreakDto`.
- `frontend/src/app/core/services/decision.service.ts` — `getQueue()` gọi `/decisions/queue`. Interface `DecisionItemDto` + `DecisionQueueDto` + types `DecisionType` / `DecisionSeverity`.
- `frontend/src/app/core/services/discipline.service.ts` — thêm `getStreak()` + interface `DisciplineStreakDto`.
- `frontend/src/app/features/dashboard/widgets/decision-queue.component.ts` — standalone widget vị trí #1 trên Home. Empty state positive (v1.1): khi 0 alert hiển thị `✅ Hôm nay đang kỷ luật + 🔥 X ngày` thay vì biến mất. Active queue cap 5 items với overflow link `/risk-dashboard`. Severity badge tiếng Việt (Khẩn cấp / Lưu ý / Thông tin), type label (Stop-loss / Kịch bản / Review thesis). Inline action route theo type (StopLossHit → `/risk-dashboard`, ScenarioTrigger → `/trade-plan?loadPlan=...`, ThesisReviewDue → `/symbol-timeline`). Inline BÁN/GIỮ buttons để PR-3 (P4).
- `frontend/src/app/features/dashboard/dashboard.component.ts` — XÓA HẲN 3 widget cũ: Risk Alert Banner (~29 dòng template + `RiskAlert` interface + `riskAlerts` field + `bannerDismissed` field + `hasDangerAlert` getter + `loadRiskAlerts` method 65 LOC), Advisory Widget (~33 dòng template + `advisories` field + `loadAdvisories` method), Pending Review section (~26 dòng template + `pendingReviewTrades` field + `loadPendingReview` method). Bỏ orphan imports `JournalEntryService`, `TradePlanService`, `PendingReviewTrade`, `ScenarioAdvisoryDto`, `PortfolioRiskSummary`, `PositionRiskItem`. Mount `<app-decision-queue>` ở vị trí #1 (top of main content). Tổng net delete ~180 LOC.

**Tests:** 10 xUnit handler tests mới (8 GetDecisionQueueQueryHandlerTests + 6 GetDisciplineStreakQueryHandlerTests = 14 total) + 10 Karma DecisionQueueComponent (empty state with/without streak, hides streak khi hasData=false, sort critical-first, cap 5 + overflow link, Vietnamese labels, action route helpers). 178/178 Application + 24/24 Karma dashboard pass.

**Plan deviations từ spec gốc:**

- Plan giả định `IRiskService.GetStopLossAlertsAsync` — thực tế là `IRiskCalculationService.GetPortfolioRiskSummaryAsync` per-portfolio (mirror logic dashboard frontend trước đó). User-level aggregation = iterate tất cả portfolios.
- Plan tên `GetActiveAdvisoriesQuery` — thực tế là `GetScenarioAdvisoriesQuery` đã tồn tại. Reuse trực tiếp.
- DTO dùng class (không record) để match convention codebase hiện có.
- Empty state streak chuyển từ stored snapshot lịch sử sang derived-on-demand (chưa có collection snapshot daily). Future PR có thể migrate sang stored daily nếu performance trở thành vấn đề (hiện N+1 trade query per closed plan, chấp nhận cho solo-user 1-3 plan/tháng).

**PR-1 (P1+P2) shipped 2026-05-04:**

- `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` — thêm use-case `portfolio-critique` (adversarial coach role thay daily-briefing trên Dashboard). `BuildPortfolioCritiqueSystemPrompt` public static để test lock content (3 điểm phản biện, mệnh lệnh, KHÔNG khen, KHÔNG động viên). `BuildPortfolioCritiqueContext` reuse data aggregation từ `BuildDailyBriefingContext`. Use-case `daily-briefing` giữ nguyên cho backend reuse, không expose trên Dashboard nữa.
- `frontend/src/app/features/dashboard/widgets/networth-summary.component.ts` — standalone widget compact 3-line ở vị trí #2 trên Home, hiển thị Net Worth + Reality Gap CAGR (điểm % so với target 15%). Coexist với Personal Finance widget existing (full breakdown).
- `frontend/src/app/features/dashboard/dashboard.component.ts` — `cagrTargetSet=true` default (Reality Gap luôn hiển thị từ first load, không cần click "Đặt mục tiêu"). Reality Gap label đổi sang "điểm %" thay vì tỉ lệ. AI button rebrand "🥊 AI phản biện danh mục" + use-case `portfolio-critique`.
- `frontend/src/app/core/services/ai.service.ts` — thêm method `streamPortfolioCritique(question?)`.
- `frontend/src/app/shared/components/ai-chat-panel/ai-chat-panel.component.ts` — thêm route case `'portfolio-critique'`.

**Tests:** 6 xUnit (AiAssistantServicePortfolioCritiqueTests — lock prompt content adversarial, không drift sang supportive) + 9 Karma (NetWorthSummaryComponent — render/hide/gap label/boundary cases incl. negative CAGR). 295/295 Infrastructure + 14/14 Karma pass.

## Hồ sơ công ty — gate chặn tạo trade plan (chặng 1, 2026-08-10)

Không cho tạo trade plan mới cho một mã khi chưa có hồ sơ hiểu doanh nghiệp đã ký và còn hiệu lực. Quyết định thiết kế đầy đủ: [ADR-0011](adr/0011-company-dossier-gate-at-plan-creation.md). Spec Q1-Q15: [`docs/superpowers/specs/2026-08-09-company-dossier-design.md`](superpowers/specs/2026-08-09-company-dossier-design.md). Plan 3 chặng: [`docs/superpowers/plans/done/2026-08-09-company-dossier-guard.md`](superpowers/plans/done/2026-08-09-company-dossier-guard.md) — **cả ba chặng done**: chặng 1 (entity + gate + trang hồ sơ), chặng 2 (5 tool MCP + dịch lỗi cổng + endpoint fundamentals + panel số liệu), chặng 3 (đề xuất InvalidationRule từ Top-3 rủi ro + mục "Hồ sơ cần soát lại" + badge dashboard).

**Gate — vị trí bắn (Application layer, đọc `ICompanyDossierRepository` nên không đặt trong entity `TradePlan`):**

| Điểm bắn | Điều kiện |
|---|---|
| `CreateTradePlanCommandHandler` — đầu `Handle`, trước mọi lookup khác | Luôn chạy, kể cả `Status="Executed"` (auto-transition nằm sau điểm bắn) |
| `UpdateTradePlanCommandHandler` — trước khi áp field mới lên plan | Chỉ khi **tỷ lệ cũ < 5% và tỷ lệ mới ≥ 5%**, hoặc khi `Symbol` đổi (bất kể size) |

Ngưỡng phản chiếu đúng `TradePlan.LargeTierThreshold` (`= 0.05m`, một nguồn duy nhất cho cả 2 gate) và guard `AccountBalance > 0` của `EnsureDisciplineGate`. `AccountBalance` null hoặc ≤ 0 ở **cả hai** thời điểm so sánh ⇒ tầng nhỏ (không có ngưỡng nào để vượt).

| | Tầng nhỏ (`size < 5%` hoặc không biết số dư) | Tầng lớn (`size ≥ 5%`) |
|---|---|---|
| `BusinessModel` | không rỗng | ≥ 30 ký tự |
| `Moats` | ≥ 1 | ≥ 1, có ít nhất 1 `Description` ≥ 30 ký tự |
| `RiskFactors` | ≥ 1, có `ObservableSignal` | ≥ 3, mỗi `ObservableSignal` ≥ 20 ký tự |
| Trạng thái hồ sơ | đã ký (`ConfirmedAt` set), chưa `Expired` | như trên |

**Key files:**

- `src/InvestmentApp.Domain/Entities/CompanyDossier.cs` — aggregate + `MoatItem`, `RiskFactor` (value object), enum `DossierFreshness`.
- `src/InvestmentApp.Application/Common/Interfaces/ICompanyDossierRepository.cs` — file rời (không thừa kế `IRepository<T>`, khóa `(UserId, Symbol)` chứ không phải `Id`).
- `src/InvestmentApp.Infrastructure/Repositories/CompanyDossierRepository.cs` — collection `company_dossiers`, index unique `ux_user_symbol` trên `(UserId, Symbol)`.
- `src/InvestmentApp.Application/CompanyDossiers/Gate/{ICompanyDossierGate,CompanyDossierGate}.cs` — `EvaluateAsync`/`EnsureAsync`, trả/throw `DossierGateResult`/`DossierGateException`.
- `src/InvestmentApp.Application/CompanyDossiers/{Commands,Queries,DTOs}/*` — `UpsertCompanyDossierCommand` (cờ `ByAgent` chọn `UpdateByAgent` vs `UpdateByOwner`), `ConfirmCompanyDossierCommand`, `GetCompanyDossierQuery`, `ListCompanyDossiersQuery`, `GetDossierGateStatusQuery`, `CompanyDossierDto`/`DossierGateStatusDto`.
- `src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs` — JWT-only, 5 route (xem bảng API Endpoints).
- `src/InvestmentApp.Api/Middleware/ExceptionMiddleware.cs` — nhánh `DossierGateException → 400` (`{code, symbol, reason, missing[]}`) đặt **trước** switch chung, vì switch map `InvalidOperationException → 409` (mà `DossierGateException` kế thừa, để nhánh bị xóa vẫn thoái về 409 chứ không 500).
- `frontend/src/app/core/services/company-dossier.service.ts` — `list/get/upsert/confirm/gateStatus`; hằng số Việt hóa một chỗ duy nhất: `GATE_REASON_TEXT` (câu cho `missing`/`unconfirmed`/`expired` — `insufficient` hiển thị theo `missing[]` backend trả), `INVALIDATION_TRIGGER_LABELS`, `dossierFreshnessLabel`/`dossierFreshnessBadgeClass`.
- `frontend/src/app/features/company-dossier/{company-dossier-list,company-dossier-detail}.component.ts` — route `/company-dossier` (danh sách + trạng thái tươi) và `/company-dossier/:symbol` (chi tiết: ô business model với đếm ký tự + chỉ báo tầng, danh sách moat, danh sách risk factor với nút ▲▼ + dropdown `SuggestedTrigger` + checkbox deal-breaker duy nhất, nút ký ở cuối trang).
- `dossier-view.component.ts` — bản ĐỌC của hồ sơ (2026-08-11). `CompanyDossierDetailComponent` giữ `mode: 'view' | 'edit'`: `view` khi `GET` 200, `edit` khi 404 hoặc có `?edit=1`/`returnTo=trade-plan` (bị cổng đá sang đây là để viết, không phải để đọc). `canSign()` chặn khi `isDirty()` — `confirm()` đóng dấu vào bản đang nằm trên server, nên ký lúc màn hình hiện nội dung khác là ký một thứ mình không đọc.
- `dossier-clipboard.ts` — cầu nối với AI **không** nối MCP: `buildAiPrompt()` gói hồ sơ + số liệu + schema JSON; `parseAiPayload()` đọc khối ```json cuối cùng. Shape trùng đúng tham số `upsert_company_dossier` — cố ý không đẻ format thứ hai. Dán chỉ đổ vào form, **không tự Lưu và không tự Ký** (ADR-0011 D2 giữ nguyên); `symbol` lệch mã đang mở thì **chặn cứng**.
- Modified: `TradePlan.cs` (`LargeTierThreshold` hợp nhất), `CreateTradePlanCommand.cs`, `UpdateTradePlanCommand.cs`, `trade-plan.component.ts` (banner chặn 400 + cảnh báo kiểm-trước gọi `gate-status` debounce 500ms, không disable nút), `market-data.component.ts` (điều hướng sang trang hồ sơ khi chưa có hồ sơ đã ký, giữ entry/SL/TP qua `sessionStorage` với `returnTo=trade-plan`), `app.routes.ts`, `Program.cs` (DI `ICompanyDossierRepository` + `ICompanyDossierGate`).

**Hợp đồng `gate-status` (pre-flight, không phải chỗ để đoán):** `DossierGateStatusDto { Symbol, Passed, Reason, Missing[], Freshness }`. `quantity`/`entryPrice`/`accountBalance` là query param **bắt buộc** — thiếu bất kỳ cái nào trả 400, vì thay thiếu bằng 0 sẽ chấm ở bậc nhỏ trong khi `POST /trade-plans` (nhận `AccountBalance` trong body) chấm ở bậc lớn, khiến bước kiểm-trước nói đỗ rồi lệnh tạo thật vẫn 400. `Freshness` bắt buộc có vì `NeedsReview` (90-179 ngày) **đỗ** gate — không có field này thì hồ sơ 4 tháng trả `passed=true` mà UI không có gì để nhắc soát lại (nhắc là việc của UI, không phải của gate).

**Bất biến khi sửa hai handler (ADR-0011 D9) — năm cửa hậu đã phải vá đều vi phạm đúng chỗ này:** giá trị gate chấm phải bằng giá trị plan **thực sự lưu xuống**. Cụ thể: `willApplyLots` là **một biến duy nhất** tính một lần trong handler, dùng cho cả điểm bắn gate lẫn lệnh `plan.SetLots` (không viết lại điều kiện ở chỗ thứ hai — điều kiện hai đường khác nhau: đường tạo có `Count > 0`, đường sửa không); `ResolveEffectiveGateInputs` trả thẳng `PlanSize` để không caller nào nhân lại theo cách riêng; và khi có lots thì size lấy **mức lớn hơn** giữa `tổng(lô × giá lô)` và `tổng lô × giá header`. Thêm bất kỳ mutator nào chạy sau điểm bắn mà chạm `Quantity`/`EntryPrice`/`Symbol` là phải soi lại danh sách này — kể cả mutator nằm trong file entity không đổi, ngoài diff (đó là lý do cửa hậu thứ tư và thứ năm sống sót qua nhiều vòng review).

**MCP — hồ sơ công ty (5 tool, `src/InvestmentApp.Api/Mcp/CompanyDossierTools.cs`):** `list_company_dossiers`, `get_company_dossier`, `get_company_fundamentals` (số liệu 24hmoney làm nguyên liệu — không nhận `UserId` vì là dữ liệu thị trường chung), `get_dossier_gate_status` (3 tham số bắt buộc, cùng hợp đồng với endpoint REST) và `upsert_company_dossier` (`ByAgent = true` → kéo theo phải ký lại theo Q10). **Cố ý KHÔNG có tool ký/xác nhận** — `ConfirmedAt` chỉ đặt được qua endpoint JWT, tức chỉ con người (ADR-0011 D2). Guard `No_Mcp_Tool_Can_Sign_A_Company_Dossier` trong `McpToolDiscoveryTests` đỏ nếu ai đó thêm tool tên chứa `confirm`/`sign` hoặc phơi `ConfirmedAt` ra schema. Đây là bản vá cho hệ quả D6: trước đó agent bị cổng chặn ở mọi mã mà không có tool nào để tự soạn hồ sơ.

**`McpDossierGate` (`src/InvestmentApp.Api/Mcp/McpDossierGate.cs`) — mọi tool MCP ghi trade plan phải đi qua nó.** Qua MCP, exception thường bị che thành `"An error occurred invoking '<tên tool>'."`, nên `DossierGateException` tới agent là một câu trần: mất cả `reason` lẫn `missing[]`, và agent không biết đường tự chữa dù chính nó vừa gây ra việc bị chặn (`ExceptionMiddleware` chỉ phục vụ đường REST). `McpDossierGate.GuardAsync` dịch riêng `DossierGateException` sang `McpException` — loại duy nhất giữ được nguyên văn — kèm câu chỉ dẫn theo từng `reason` và nhắc rõ **agent không ký được**. Chỉ bọc đúng exception của cổng: bọc rộng hơn là che mọi lỗi khác dưới một câu đẹp đẽ. Thêm tool MCP nào gọi `CreateTradePlanCommand`/`UpdateTradePlanCommand` thì phải bọc bằng `GuardAsync`.

**Số liệu doanh nghiệp làm nguyên liệu (chặng 2, 2026-08-10):**

- `GetCompanyFundamentalsQuery` (`src/InvestmentApp.Application/MarketData/Queries/GetCompanyFundamentals/`) gói `IComprehensiveStockDataProvider` — nằm ở `MarketData/Queries/` theo đúng convention sẵn có, không phải `Market/Queries/` như plan viết.
- `GET /api/v1/market/stock/{symbol}/fundamentals` (JWT) trong `MarketDataController`.
- **`unavailableSections[]` là điểm sống còn.** Provider gộp ~9 lệnh gọi HTTP 24hmoney và phần nào hỏng thì trả rỗng chứ không báo lỗi, nên rỗng không phân biệt được với "bằng không". Query liệt kê tên từng phần không lấy được; cả `Company` và `Indicators` đều **rỗng nội dung** thì trả **404** thay vì 200 rỗng — 200 rỗng khiến agent viết hồ sơ từ khoảng trống rồi qua cổng bằng nội dung bịa. Chấm theo **nội dung, không theo null-ness**: provider trả về đủ hai object với mọi field null cho mã sai, nên `== null` biến cửa 404 thành code chết (`HasAnyValue` dùng reflection để một chỉ số mới thêm vào không làm danh sách kiểm mục ruỗng).
- `FundamentalsPanelComponent` (`frontend/src/app/features/company-dossier/fundamentals-panel.component.ts`) đứng cạnh ô viết trong grid `lg:grid-cols-5` (form `col-span-3`, panel `col-span-2` — chia đôi thì ô nhập rủi ro chỉ còn ~480px, ngắn hơn một câu tả dấu hiệu quan sát); bốn khối dài gập được, mặc định chỉ mở khối doanh thu, và khối **không lấy được dữ liệu thì không gập** để câu báo thiếu luôn nhìn thấy. Điều kiện render mỗi khối là `hasSection()` — vừa không bị đánh dấu thiếu **vừa thật sự có payload**; chỉ tin `unavailableSections` là để một lần lệch giữa danh sách và body deref null, và một deref null làm sập cả vòng change detection nên các khối khác biến mất im lặng.
- Cache `IMemoryCache` **15 phút** theo mã (`fundamentals:{symbol}`), chỉ cache ca lấy được. Ngắn hơn nhiều so với 6 giờ của nhãn ngành ở `RiskCalculationService` vì P/E và vốn hóa đổi trong ngày, còn nhãn ngành gần như không đổi. Một lần gọi provider là ~9 request HTTP, và cả panel lẫn tool MCP đều vào cùng endpoint này.
- Giới hạn đã biết của `HasAnyValue`: với property KHÔNG nullable (`Shareholder.Percentage`, `ForeignTradingDay.BuyVolume`) thì 0 và "không có" là cùng một bit, nên giá trị **mặc định** được coi là "không có thông tin". Chọn phía này vì một số 0 thật bị báo "không lấy được" chỉ mất một dòng, còn một vỏ rỗng được coi là dữ liệu thì bịa ra cả một khối cổ đông.
- Số liệu **không** tính vào điều kiện chặn của cổng; panel nói rõ điều đó bằng một dòng chữ nhỏ.

**Chặng 3 — trả lại thời gian và nhắc trước (2026-08-10):**

- `GetSuggestedInvalidationRulesQuery` → `GET /company-dossiers/{symbol}/suggested-rules`. Lấy 3 `RiskFactor` hạng cao nhất, ghép `Description — dấu hiệu: ObservableSignal`, `Trigger = SuggestedTrigger ?? Manual`. `MeetsMinLength` chấm theo ngưỡng 20 ký tự của gate kỷ luật; đề xuất **không đạt vẫn trả về** kèm cờ, vì lặng lẽ bỏ đi thì người dùng không biết là có gợi ý.
- `GetDossiersNeedingReviewQuery` → `GET /company-dossiers/needing-review`. Lọc bỏ `Fresh`, xếp `Expired` → `Unconfirmed` → `NeedsReview` rồi `DaysOverdue` giảm dần. Route literal khai báo **trước** `{symbol}` — literal thắng tham số nên "needing-review" không bị hiểu thành một mã (đã kiểm bằng curl: trả `[]` 200 thay vì 404 của đường `Get`).
- `DaysOverdue` đếm từ mốc **90 ngày** theo ngày lịch VN, không đếm từ ngày soát. Hồ sơ `Unconfirmed` = **0** — đồng hồ hạn tươi chưa chạy, hiện một con số quá hạn ở đó là bịa.
- FE: khối "Từ hồ sơ công ty" trong section "Điều kiện lý do sai" của `trade-plan.component.ts` (stream riêng, chỉ cần symbol nên không đi cùng pipeline cổng vốn đòi đủ 4 số); section "Hồ sơ công ty cần soát lại" ở `/pending-reviews`; badge ở `discipline-score-widget`, **ẩn hoàn toàn khi bằng 0**.
- **Đề xuất, không tự áp.** Không tick sẵn, không tự thêm khi tải — người dùng bấm mới vào plan. Một plan tự đầy điều kiện mà chưa ai đọc lại chúng thì gate kỷ luật đo được chữ, không đo được ý.

**Đã biết, không phải bug:**

- Lệnh trade plan **đầu tiên sau khi deploy** bị chặn với mọi mã, kể cả mã đang giữ — không có grandfathering (ADR-0011 D5).
- Đường ghi trade plan của agent (ApiKey `AiAgentController` + MCP) **đã mở lại**: `upsert_company_dossier` cho agent soạn nội dung, `get_dossier_gate_status` cho nó biết còn thiếu gì trước khi thử tạo plan, và `McpDossierGate` giữ nguyên `reason` + `missing[]` trong thông báo lỗi (`ExceptionMiddleware` chỉ phục vụ đường REST). Agent vẫn **không ký được** — hệ quả D6 của ADR-0011 chỉ còn đúng ở phần đó. Trong khoảng thời gian chặng 1 đã live mà chặng 2 chưa có, đường này bị khoá cứng trên prod; xem mục "Đã biết" bên dưới.

**Tests:** verify thật trên DB prod (tài khoản test, mã HPG) 2 lượt — API 8/8 (chưa có hồ sơ → 400 `missing` → viết → 400 `unconfirmed` → ký → `Fresh` → tạo plan 201 → xóa plan) và browser 22 mục. Báo cáo browser ở `scratch/qa-reports/` (gitignored).

## Per-User API Keys (Personal Access Tokens)

Feature cho phép non-interactive API access (e.g., local NPU assistant pulling daily digest). ADR: [`docs/adr/0003-per-user-api-keys.md`](adr/0003-per-user-api-keys.md).

**Key files:**

- `src/InvestmentApp.Domain/Entities/ApiKey.cs` — aggregate entity (`KeyHash`, `UserId`, `Name`, `CreatedAt`, `LastUsedAt`, `IsRevoked`)
- `src/InvestmentApp.Application/ApiKeys/Commands/CreateApiKeyCommand.cs` — tạo token; handler gọi `IApiKeyTokenService.GenerateAsync`, persist hash, trả về plaintext token (một lần duy nhất)
- `src/InvestmentApp.Application/ApiKeys/Commands/RevokeApiKeyCommand.cs` — revoke theo id; handler ownership-check `UserId` trước khi set `IsRevoked=true`
- `src/InvestmentApp.Application/ApiKeys/Queries/GetApiKeysQuery.cs` — list keys của caller (không expose hash)
- `src/InvestmentApp.Application/Common/Interfaces/IApiKeyTokenService.cs` — contract (generate + verify)
- `src/InvestmentApp.Application/RepositoryInterfaces.cs` — `IApiKeyRepository` (thêm vào file chung ~23 interfaces)
- `src/InvestmentApp.Application/Common/Behaviors/ValidationBehavior.cs` — MediatR pipeline behavior; `AddValidatorsFromAssembly` + remove `AddFluentValidationAutoValidation()` (validators run after controller sets server-side fields like `UserId`)
- `src/InvestmentApp.Infrastructure/Repositories/ApiKeyRepository.cs` — Mongo collection `api_keys`; unique index `KeyHash`, index `UserId`
- `src/InvestmentApp.Infrastructure/Services/ApiKeyTokenService.cs` — implements `IApiKeyTokenService`; cryptographic random token, SHA-256 hash
- `src/InvestmentApp.Api/Controllers/ApiKeysController.cs` — JWT-authed; `POST /api/v1/api-keys` (201) + `GET /api/v1/api-keys` + `DELETE /api/v1/api-keys/{id}` (204)
- `src/InvestmentApp.Api/Auth/ApiKeyAuthExtensions.cs` — `ApiKey` authentication scheme (`X-Api-Key` header → SHA-256 hash → `GetByHashAsync` → `IsActive` check → principal with `sub`=UserId; persists `LastUsedAt`, tolerating write failures). Opt-in only via `[Authorize(AuthenticationSchemes="ApiKey")]`; registered in `Program.cs` but never a default scheme
- `frontend/src/app/core/services/api-keys.service.ts` — HTTP client + TS DTOs
- `frontend/src/app/features/api-keys/api-keys.component.ts` — standalone page, route `/api-keys`; tạo / revoke token, hiển thị plaintext token inline một lần sau create

**ApiKey-scheme opt-in endpoints (ADR-0003 → ADR-0004):**

| Controller | Route | Scope |
|-----------|-------|-------|
| `AiDigestController` | `/api/v1/ai/daily-digest` | Read-only — daily digest context |
| `AiAgentController` | `/api/v1/ai/agent` | **Curated write** — lập/sửa/thực-hiện trade plans + ghi trade; IDOR-safe (ownership assert trên mọi command). `POST trades` auto-resolve `portfolioId` (auto-pick khi 1 danh mục) + `fee`/`tax` (tự tính khi bỏ trống) — ADR-0005 |
| `AiAgentPortfoliosController` / `AiAgentFeesController` | `/api/v1/ai/agent/{portfolios,fees/calculate}` | **Agent self-service (ADR-0005)** — `GET portfolios` (mirror GetAllPortfoliosQuery) + `POST fees/calculate` (mirror FeesController, inject IFeeCalculationService). Giúp agent lấy portfolioId + tính phí/thuế trước khi ghi trade |
