# Changelog — Investment Mate v2

---

## [v2.47.2] — 2026-04-22 · Fix: Gold auto-calc dùng BuyPrice thay SellPrice

Sửa logic định giá vàng trong Personal Finance. Trước đây Balance = quantity × **SellPrice** (giá tiệm bán ra) — đó là giá user phải **trả khi đi mua thêm**, không phải giá tài sản đang giữ. Giờ đổi sang **BuyPrice** (giá tiệm mua vào = giá user bán được nếu thanh khoản ngay), phản ánh đúng giá trị tài sản thực tế, không cộng ảo phần spread mua–bán (1–3 triệu/lượng tùy loại) vào tổng tài sản.

- Backend `UpsertFinancialAccountCommandHandler.ResolveBalanceAsync`: `price.SellPrice` → `price.BuyPrice`.
- Frontend preview label "Giá Bán ra hiện tại" → "Giá mua vào hiện tại", `goldPreviewSellPrice` → `goldPreviewBuyPrice`.
- Test `Handle_Gold_AutoCalcBalance_FromProvider` update expectation 2 × 169,500,000 → 2 × 167,000,000.
- Docs cập nhật (`business-domain.md`, `architecture.md`, `project-context.md`, `tai-chinh-ca-nhan.md`).

---

## [v2.47.1] — 2026-04-22 · Fix: Tài chính cá nhân — Securities sync + UX redesign

**Branch:** `fix/personal-finance-securities-and-ux`

Fix bug user report: card "Chứng khoán" top (389.310.000đ live) khác với card Chứng khoán trong Tài khoản list (0đ stored). Đồng thời redesign UX tài khoản theo feedback: nút Sửa/Xóa quá gần nhau, nên gộp vào popup edit, kèm bảo vệ chống xóa nhầm.

### Bug fix

- **DTO projection override**: `GetNetWorthSummaryQuery` giờ set `Balance` của Securities account trong list `Accounts` = live `securitiesValue` tính từ portfolios, thay vì trả stored 0. Top card và list card đồng nhất.

### Domain rules mới

- **Securities không tạo thủ công**: `FinancialProfile.UpsertAccount` reject khi thêm account thứ 2 type=Securities (profile đã auto-provision 1 khi Create). Edit by-id vẫn OK.
- **Securities không xóa thủ công**: `RemoveAccount` luôn reject Securities (trước đây chỉ reject khi là last).
- **Không xóa tài khoản có dữ liệu**: `RemoveAccount` reject mọi account có `Balance > 0`. User phải set balance=0 trước khi xóa — chống xóa nhầm.

### Frontend UX

- **Card tài khoản**: toàn card clickable (non-Securities) → mở popup edit. Hiện hint "Sửa ›" bên phải để làm rõ affordance.
- **Securities card**: không clickable, hiển thị nhãn "Auto-sync" — không sửa/xóa được.
- **Dropdown loại tài khoản**: bỏ option "Chứng khoán" (chỉ hiện 4 loại: Tiết kiệm, Dự phòng, Nhàn rỗi, Vàng).
- **Nút Xóa**: di chuyển từ card vào trong popup edit, kèm điều kiện `Balance = 0`. Hiện message nhắc khi bị disable.
- **Phím ESC**: đóng popup edit (HostListener `document:keydown.escape`).

### Tests

- Domain: +3 tests (UpsertAccount_AddingSecondSecurities, RemoveAccount_Securities_ShouldAlwaysThrow, RemoveAccount_NonSecuritiesWithPositiveBalance, RemoveAccount_GoldWithPositiveBalance, RemoveAccount_GoldWithZeroBalance, UpsertAccount_UpdatingExistingSecurities). Update 2 existing tests để dùng balance=0.
- Application: +1 test (Securities DTO balance = live securitiesValue). Update 1 existing test.
- Total: 1024 pass (665 Domain + 119 Application + 235 Infrastructure + 5 Api).

### Docs

- `frontend/src/assets/docs/tai-chinh-ca-nhan.md` — cập nhật phần "Thêm tài khoản" + section mới "Sửa / Xóa tài khoản" mô tả flow mới.

---

## [v2.47.0] — 2026-04-22 · Admin: Tổng quan user + activity stats

**Branch:** `feat/admin-user-overview`

Mở rộng admin tool (B1 Phase 2) — thêm trang tổng quan toàn bộ user với thống kê hoạt động; restructure `/admin` thành layout có left sidebar để sau này thêm menu mới chỉ cần thêm 1 entry.

### Tính năng

- **Trang mới `/admin/users/overview`** — bảng paginated hiển thị toàn bộ user + stats:
  - Role (Admin/User), # Portfolio, # Trade, giao dịch cuối, đăng nhập cuối, impersonate cuối.
  - Pagination (20/50/100) với total count.
  - Nút "Xem như" impersonate inline cho từng row.
- **Admin layout mới** `/admin` với left sidebar (2 menu: "Tổng quan user", "Tìm & Impersonate") — mặc định redirect sang overview. Extensible: thêm feature admin mới chỉ việc push thêm item vào `menu[]` và thêm child route.
- **`User.LastLoginAt`** — track timestamp đăng nhập gần nhất. Cập nhật trong `AuthController.GoogleCallback` cho cả new user và existing user. Không cập nhật khi refresh token hay impersonate.

### Backend

- **Domain** — `User.LastLoginAt` (nullable) + method `RecordLogin()` (3 unit tests: default null, sets UtcNow, idempotent overwrite).
- **Application** — `GetUsersOverviewQuery` + `UsersOverviewResult` + `UserOverviewDto`. Handler verify role=Admin, gọi `IUserRepository.GetPagedAsync`, aggregate cross portfolio/trade/impersonation repos. 3 unit tests (unauthorized, happy path với stats, empty page).
- **Repository interfaces mới:**
  - `IUserRepository.GetPagedAsync(page, pageSize)` — sort theo CreatedAt desc, clamp pageSize ≤ 200.
  - `IPortfolioRepository.GetIdsByUserIdsAsync(userIds)` — batch lookup, return dict {userId → portfolioIds}.
  - `ITradeRepository.GetStatsByPortfolioIdsAsync(portfolioIds)` — batch aggregate {portfolioId → (count, lastTradeAt)}.
  - `IImpersonationAuditRepository.GetLatestStartedAtByTargetAsync(targetUserId)` — tận dụng index `{ targetUserId, startedAt desc }` đã có.
- **Api** — `GET /api/v1/admin/users/overview?page=&pageSize=` với `[RequireAdmin]`.

### Frontend

- `core/services/admin.service.ts` — thêm `getUsersOverview()` + types `UserOverviewDto`/`UsersOverviewResult`.
- `features/admin/admin-layout.component.ts` — standalone layout với sidebar + `<router-outlet>`.
- `features/admin/users-overview.component.ts` — bảng stats + pagination + "Xem như" modal (tái dùng flow impersonate hiện có).
- Route tree: `/admin` → `AdminLayoutComponent` với children `users/overview` (default), `users/search` (existing). `/admin/users` redirect → `users/overview` để giữ backward compat.
- Header ADMIN link đổi target `/admin/users` → `/admin`.

### Tests

- Backend: 1019 green (Domain 661, Application 118, Api 5, Infrastructure 235). Thêm 3 domain + 3 application tests mới.
- Frontend: ng build OK.

---

## [v2.46.0] — 2026-04-22 · Tài chính cá nhân + Gold Price Crawler (Tier 3)

**Branches:** 6 PR — `feat/personal-finance-{domain,application,gold-crawler,api,frontend,docs}` (PR #77–#82 + docs PR)

Feature Tier 3 từ improvement plan — tổng quan tài sản cá nhân (CK + vàng + tiết kiệm + dự phòng + nhàn rỗi), nguyên tắc tài chính với health score 0-100, và crawler giá vàng live từ 24hmoney. Ship qua 6 phase nhỏ để review/rollback dễ.

### Tính năng chính

- **Trang mới `/personal-finance`**: onboarding form → 5-card net worth → health score bar + 3 rule checks → accounts CRUD → settings.
- **Dashboard widget** "Tài chính cá nhân" clickable với breakdown + health bar + onboarding variant.
- **Gold form auto-calc**: user chọn SJC/DOJI/PNJ/Other + Miếng/Nhẫn + nhập quantity (lượng) → FE fetch live price → hiển thị Balance preview real-time. Fallback nhập tay nếu không dùng auto-calc.
- **Health score 0-100** với 3 rules (điểm trừ tỷ lệ vi phạm):
  - Quỹ dự phòng ≥ 6 tháng chi tiêu (-40 max)
  - Đầu tư (CK + Vàng) ≤ 50% tổng tài sản (-30 max)
  - Tiết kiệm ≥ 30% tổng tài sản (-30 max)
  - Vàng cộng vào investment (cùng CK) theo định nghĩa user "vàng cũng là mục đầu tư". Không cộng vào savings.
- **Securities auto-sync** giá trị từ `IPnLService.CalculatePortfolioPnLAsync(...).TotalMarketValue`, aggregate across all user portfolios — không cần nhập tay.

### Backend

- **Domain** — `FinancialProfile` aggregate (per-user 1:1, unique UserId) + `FinancialAccount` embedded + `FinancialRules` value object + 3 enums (`FinancialAccountType`, `GoldBrand`, `GoldType`). Methods: `Create`/`UpdateMonthlyExpense`/`UpdateRules`/`UpsertAccount`/`RemoveAccount`/`GetTotalAssets`/`CalculateHealthScore`. Guard "last Securities không được xóa".
- **Application** — 3 commands (UpsertFinancialProfile, UpsertFinancialAccount with Gold auto-calc, RemoveFinancialAccount) + 3 queries (GetFinancialProfile, GetNetWorthSummary, GetGoldPrices). `IGoldPriceProvider` interface + `PersonalFinanceMapper`. `UpsertFinancialAccountCommandHandler.ResolveBalanceAsync` xử lý Gold auto-calc: 3 fields đủ → fetch price → `Balance = quantity × sellPrice`. Provider null → throw 400 (không silent fallback).
- **Infrastructure** — `HmoneyGoldPriceProvider` crawler giá vàng từ `24hmoney.vn/gia-vang` bằng AngleSharp 1.3.0. Không có JSON API nên scrape SSR HTML. Filter chỉ Miếng + Nhẫn (skip nữ trang/trang sức). **Quirk**: giá HTML là full VND (167,200,000) mặc dù UI label nói "triệu VNĐ/lượng" — không scale ×1000. Two-tier cache: fresh 5 phút + stale 6h fallback. `FinancialProfileRepository` Mongo với unique index UserId, narrow catch `MongoCommandException when (ex.Code is 85 or 86)` để defensive với index conflict.
- **Api** — `PersonalFinanceController` với 6 endpoints JWT-authed: GET / (profile, 404 nếu absent), GET /summary (net worth + `hasProfile` flag), GET /gold-prices, PUT / (upsert profile), PUT /accounts (upsert với Gold auto-calc), DELETE /accounts/{id}.
- **Config** — `appsettings.json` thêm section `GoldPriceProvider` với placeholder `{GoldPriceProvider__PageUrl}` theo convention. Env var bắt buộc set trước deploy: `GoldPriceProvider__PageUrl=https://24hmoney.vn/gia-vang`.

### Frontend

- **`core/services/personal-finance.service.ts`** — HTTP client + TypeScript DTOs + 3 enums match backend numeric serialization (comment warning nếu BE đổi sang `JsonStringEnumConverter`) + static label helpers. `getProfile()` convert 404 → null qua `catchError + of(null)`.
- **`features/personal-finance/personal-finance.component.ts`** — standalone ~620 lines inline template. Onboarding form + 5-card net worth grid + health bar color-coded + 3 rule check rows + accounts cards grid (Edit/Delete, Securities không Edit) + collapsible settings + account form modal với Gold auto-calc. Cache gold prices invalidate mỗi lần mở form (tránh 7h-stale).
- **Dashboard widget** + onboarding variant, silent UI on error + `console.error` để dev diagnose.
- **Header nav**: "💰 Tài chính cá nhân" dưới group "Quản lý".

### Tests

| Layer | Test files | Tests |
|-------|-----------|:-----:|
| Domain | `FinancialProfileTests.cs` | 39 |
| Application | Commands + Queries | 22 |
| Infrastructure | `HmoneyGoldPriceProviderTests.cs` + `LiveSmoke.cs` | 17 |
| **Tổng feature** | | **78 mới** |

**Tổng solution: 1016 tests green** (Domain 661, Application 115, Infrastructure 235, Api 5). FE không thêm unit tests (consistent với precedent project).

### Deploy note

**⚠️ Trước khi deploy staging/prod, set env var:**
```
GoldPriceProvider__PageUrl=https://24hmoney.vn/gia-vang
```
Pattern giống `MarketDataProvider__BaseUrl`. Nếu quên, app không crash lúc startup — fail silently khi request `/gold-prices` đầu tiên với DNS error. Xem Section 11 Deploy checklist trong plan đã archive.

### Archived plan

Plan `docs/plans/personal-finance.md` move sang `docs/plans/done/personal-finance.md` sau khi verify full E2E.

---

## [v2.45.0] — 2026-04-21 · Admin Impersonation (debug tooling) — B1 Phase 1

**Branch:** `feat/capital-current-vs-initial`

Công cụ debug cho phép admin đăng nhập dưới tư cách user cụ thể để tái hiện bug dữ liệu theo UI mà user đó thấy. Hoàn toàn read-only ở MVP, có audit trail Mongo đầy đủ.

### Backend
- **Domain** — thêm `UserRole { User, Admin }` (default `User`), method `User.PromoteToAdmin()` / `DemoteToUser()`. Entity mới `ImpersonationAudit` (append-only, không phải AggregateRoot): `AdminUserId, TargetUserId, Reason, IpAddress, UserAgent, StartedAt, EndedAt?, IsRevoked`, method `Revoke()` set cả `IsRevoked=true` và `EndedAt`.
- **Application** — `IImpersonationAuditRepository`, `StartImpersonationCommand` (verify admin role + target tồn tại + không self-impersonate → tạo audit → gọi `IJwtService.CreateImpersonationToken` → log `AuditEntry`), `StopImpersonationCommand` (chỉ admin gốc mới stop được, gọi `audit.Revoke()`). Mở rộng `IJwtService` với `CreateImpersonationToken(adminId, target, impersonationId)`.
- **Infrastructure** — `ImpersonationAuditRepository` (collection `impersonationAudits`, indexes theo `adminUserId`/`targetUserId`/`isRevoked`). `JwtService.GenerateToken` thêm claim `role`. Token impersonate có claims `sub=target, actor=admin, impersonation_id, amr=impersonate`, TTL cố định 1h. `AdminBootstrapHostedService` đọc `Admin:AllowEmails` khi startup và `PromoteToAdmin()` idempotent (không override role đã có, try/catch tránh fail startup).
- **Api** — `[RequireAdmin]` attribute (chặn non-admin + chặn nested impersonate qua `amr` claim). `AdminController` với `POST /api/v1/admin/impersonate` + `POST /api/v1/admin/impersonate/stop`. `ImpersonationValidationMiddleware` chạy giữa `UseAuthentication` và `UseAuthorization`: validate `IsRevoked` (401 + `X-Impersonation-Revoked: true`), block mutation POST/PUT/DELETE/PATCH (403 + `MUTATION_BLOCKED_DURING_IMPERSONATION`) trừ khi `Admin:AllowImpersonateMutations=true` hoặc gọi stop endpoint, set header `X-Impersonating: true`.
- **Config** — `appsettings.json` thêm section `Admin:AllowEmails` (CSV string, placeholder `{Admin__AllowEmails}` giống các key khác) + `Admin:AllowImpersonateMutations` (default `false`). Giá trị thật set ở `appsettings.Development.json` cho local hoặc env var `Admin__AllowEmails="a@x.com,b@x.com"` cho Cloud Run — 1 env var duy nhất, dễ set hơn mảng. Bootstrap service tự skip nếu placeholder chưa được thay.

### Frontend
- **`core/services/impersonation.service.ts`** — `startImpersonate()` backup `auth_token`→`admin_auth_token`, `stopImpersonate(skipApiCall?)` restore. Decode JWT lấy target email/name.
- **`core/interceptors/impersonation-revoked.interceptor.ts`** — functional interceptor catch 401 + `X-Impersonation-Revoked` → auto-restore admin token + toast warning. Đăng ký qua `withInterceptors([...])` ở `main.ts`.
- **`shared/components/impersonation-banner/`** — sticky red bar full-width ở trên cùng, hiển thị email target + nút "Thoát impersonate". Mount trước `<app-header>` trong `app.component.ts`.

### Tests
- Domain: `UserRoleTests` (4) + `ImpersonationAuditTests` (6) — tổng 10 tests mới.
- Application: `StartImpersonationCommandHandlerTests` (4) + `StopImpersonationCommandHandlerTests` (3) — tổng 7 tests mới.
- Infrastructure: `JwtServiceImpersonationTests` (4) — role claim trên login token + 3 claim của impersonate token + TTL 1h.
- **Tổng suite: 926 tests green (trước: ~907).**

### Admin UI (Phase 2 follow-up, same PR)
- **`GET /api/v1/admin/users?email=<q>`** — search user theo email (partial, case-insensitive), limit 10, exclude caller. `SearchUsersQuery` + handler + `IUserRepository.SearchByEmailAsync` (Mongo regex).
- **`AdminGuard`** (`core/guards/admin.guard.ts`) — check JWT `role=Admin` + chặn khi đang impersonate (`amr=impersonate`).
- **`AdminService`** (`core/services/admin.service.ts`) — gọi API search.
- **`/admin/users` page** (`features/admin/admin-users.component.ts`) — input email, list results, modal nhập reason, bấm "Xem như user này" → gọi `ImpersonationService.startImpersonate()` → reload.
- **Header link `ADMIN`** — chỉ hiện khi admin login (và không đang impersonate).

### Security notes
- Bootstrap admin thông qua env `Admin__AllowEmails__0=admin@example.com` → tránh phụ thuộc code deploy để grant admin.
- Nested impersonate bị chặn ở 2 tầng: `[RequireAdmin]` attribute (controller) và middleware không cho phép nested token.
- Mutation block là default — admin phải có lý do cụ thể mới bật `Admin__AllowImpersonateMutations=true`.
- Audit trail Mongo không bao giờ xoá (append-only), mỗi phiên impersonate = 1 document.

### Docs
- `docs/architecture.md` — thêm section "Admin Impersonation (Debug Tooling)" + Admin controller trong bảng endpoints + cập nhật folder `Authorization/` và `Middleware/`.
- `docs/business-domain.md` — thêm UserRole + ImpersonationAudit vào entity map + Admin vào API endpoints + rule #10 về impersonation flow.
- `docs/plans/multi-user-access-plan.md` — Phần 2 B1 Phase 1 marked implemented (plan gốc đã có spec `§2.7`).

---

## [v2.44.1] — 2026-04-20 · Backend version on /health endpoints

**Branch:** `feat/capital-current-vs-initial`

### CI / CD
- **`Dockerfile.api`** — added `ARG APP_VERSION=dev` + `ENV APP_VERSION=${APP_VERSION}` in runtime stage so the image carries its build identity.
- **`cloudbuild.yaml` (active Cloud Run path)** — API build step now passes `--build-arg APP_VERSION=$SHORT_SHA`. Cloud Build's built-in `$SHORT_SHA` substitution (7-char commit SHA) gets baked into the image at build time.
- **`.github/workflows/cd.yml` (GHCR path, not the live deploy)** — mirrored the same wiring: "Compute short SHA" step + `build-args: APP_VERSION=...` on the API `docker/build-push-action`. Included for parity so future use of the GHCR/self-hosted deploy path stays in sync.

### Backend
- **`src/InvestmentApp.Api/Program.cs`** — `/health`, `/health/live`, `/health/ready` all return a new `version` field sourced from `APP_VERSION` env (`"dev"` fallback when unset/empty). Lets `curl /health` after deploy confirm which commit is actually running.

### Bug fix during rollout
- First attempt shipped only the `cd.yml` edit — `/health` still returned `"version":"dev"` in prod because the live deploy goes through Cloud Build (`cloudbuild.yaml` → Cloud Run), not GitHub Actions. Added the missing `--build-arg` to `cloudbuild.yaml` in the same PR.

### Docs
- `docs/architecture.md` — documented `version` field on health endpoints.

---

## [v2.44.0] — 2026-04-19 · Fix TWR / MWR / CAGR (P3)

**Branch:** `feat/capital-current-vs-initial`

### Bug fixes — math
- **Backend `CashFlowAdjustedReturnService.CalculateTWRAsync`**: period return `(V_i − V_{i-1} − C_i) / V_{i-1}` blew up (observed +8.9M%) when a snapshot had near-zero `TotalValue` or a single period had extreme return. Added `MinSnapshotValue = 1000đ` guard (skip period) and `MaxAbsPeriodReturn = 5.0` cap (skip >500% single-period outlier). One bad snapshot no longer corrupts the product chain.
- **Backend `CashFlowAdjustedReturnService.CalculateMWRAsync` + `GetAdjustedReturnSummaryAsync`**: `currentValue` used `cashBalance = InitialCapital + flows − pnl.TotalInvested`. But `pnl.TotalInvested` is cost basis of **currently open positions** — diverges from gross historical buys after any position is closed (same bug fixed in v2.43.0 for the capital-flows page). Now uses gross `Σ(BUY qty×price+fee+tax) − Σ(SELL qty×price−fee−tax)` from `ITradeRepository`, matching the `/capital-flows` hero math.
- **Backend MWR Newton-Raphson**: added divergence guard (rate ∈ [−0.999, 100]) + warning log when it fails to converge; returns 0 instead of garbage.
- **Backend `PerformanceMetricsService.CalculateCAGRAsync`** (analytics endpoint `/analytics/portfolio/{id}/performance` — used as FE fallback): snapshot path was `(V_last/V_first)^(1/years) − 1`, same flow-agnostic bug as the FE CAGR. Now delegates to `ICashFlowAdjustedReturnService.CalculateTWRAsync` then annualizes `(1 + TWR)^(1/years) − 1`. Trade-path fallback (when no snapshots exist) was using `pnl.TotalInvested` (open-position cost) — now uses gross `Σ(BUY …) − Σ(SELL …)` + `InitialCapital + netFlow` formula, consistent with MWR.
- **Backend `PerformanceMetricsService.GetFullPerformanceSummaryAsync.totalReturn`**: same raw-endpoint bug on the period-total return. Now returns flow-adjusted TWR directly (falls back to gross PnL % only when no snapshots).
- **Frontend `dashboard.component.ts: calculateCagrFromCurve`**: was `(V_last / V_first)^(1/years) − 1` — ignores flows between first and last snapshot. A net-deposit would show fake huge CAGR; a net-withdraw (the observed case) produced **CAGR −21.5%** on a portfolio that's actually +4.09%. Now annualizes backend TWR (flow-adjusted) → `(1 + TWR)^(1/years) − 1`. Falls back to endpoint ratio only if TWR unavailable.

### Backend
- `CashFlowAdjustedReturnService` ctor now takes `ITradeRepository` + `ILogger<>`.
- `PerformanceMetricsService` ctor now takes `ICashFlowAdjustedReturnService` (no circular dep; adjusted-return service does not depend on metrics).

### Tests
- `CashFlowAdjustedReturnServiceTests` (new) — 8 tests: no-portfolio, <2-snapshot, normal TWR, TWR with flow, near-zero snapshot doesn't blow up, outlier period skipped, MWR flat-portfolio ≈0, MWR uses gross trade values for cash balance (closed-position regression case).
- `PerformanceMetricsServiceCagrTests` (new) — 7 tests: CAGR uses annualized TWR (not raw endpoints), negative TWR annualizes, short window returns 0, TWR<-100% doesn't crash, no-snapshot trade fallback uses gross totals (closed-position regression), no-snapshot-no-trade returns 0, full-summary `TotalReturn` = TWR.
- All 904 backend tests pass.

### Docs
- `docs/plans/p3-twr-mwr-cagr-fix.md` → moved to `done/` with status update
- `CHANGELOG.md` v2.44.0

---

## [v2.43.0] — 2026-04-19 · Capital-flows — Hero cards (aggregate + per-portfolio)

**Branch:** `feat/capital-current-vs-initial`

### Frontend
- Trang `/capital-flows` thêm **2 tầng hero**:
  - **Tổng quan ({{ n }} danh mục)** — luôn hiện ở trên cùng, aggregate qua tất cả danh mục (cash + market value + return + allocation + breakdown). Fetch từ `/pnl/summary`.
  - **Chi tiết: {tên danh mục}** — hiện khi chọn 1 danh mục từ dropdown, cùng cấu trúc layout nhưng data riêng cho portfolio đó.
- Thay vì chỉ có flow aggregates (Tổng nạp/rút/cổ tức/dòng ròng), user giờ thấy được **bức tranh tổng quát** ngay khi mở page + drill-down khi cần
- Mỗi hero gồm: Tổng tài sản + % return vs Vốn hiện tại, allocation bar (Giá trị thị trường vs Tiền mặt), breakdown (Vốn ban đầu / Dòng vốn ròng / L/L chưa TH / đã TH)
- Reload `OverallPnLSummary` + `portfolios` sau record/delete flow để không stale
- Switch portfolio → clear `portfolioPnL` / `flowHistory` / `adjustedReturn` ngay để tránh hiển thị data lẫn lộn
- Inject `PnlService` để lấy market value + realized/unrealized P&L

### Bug fixes (từ code review)
- **Allocation bar overflow** khi `cashBalance < 0` (overbought/margin edge case): bar widths clamp [0, 100] qua getter `marketBarWidth` / `cashBarWidth`
- **Double-fire `loadFlowData`** khi user đến page qua `?portfolioId=xyz` rồi record/delete flow: `loadPortfolios` giờ chỉ auto-select nếu chưa có portfolio đang chọn
- **Dấu âm hiển thị đôi** ở totalReturn (pipe đã thêm `-` + template prefix `↘ `): dùng `absTotalReturn` với explicit sign prefix

### Bug fixes (aggregate math)
- **Backend `PnLController.GetOverallPnL`**: `totalNetCashFlow += netCashFlow` bị kẹt trong try block → portfolio không có trade làm PnL throw → skip luôn netCashFlow. Nhưng `totalInitialCapital` lấy tất cả portfolio → `totalCurrentCapital` bị lệch. Đã move ra ngoài try.
- **Frontend `overallView` cashBalance**: trước dùng `OverallPnLSummary.totalInvested` (= cost basis of OPEN positions từ PnLService) → sai sau khi đóng vị thế. Ví dụ: mua 100M, bán hết 120M → `open cost = 0` → cash bị tính thừa 100M. Fix: dùng `portfolios.reduce((s,p) => s + p.totalInvested)` — gross historical từ `PortfolioSummary`.

### Tests
- `capital-flows.component.spec.ts` (new) — 18 tests: per-portfolio getters (13 — normal / overbought / zero capital / loss / no-selection / partly invested) + `overallView` aggregate (5 — null-guards, 2-portfolio sum, totalSold aggregation, overbought clamp)
- Fix existing `trade-create.component.spec.ts` mock data (thêm `netCashFlow` + `currentCapital` fields từ Phase 1 interface update)

### Docs
- `CHANGELOG.md` v2.43.0

---

## [v2.42.0] — 2026-04-18 · Capital — Auto seed Deposit flow (Phase 3)

**Branch:** `feat/capital-current-vs-initial`

### Domain
- `CapitalFlow.IsSeedDeposit: bool` — đánh dấu flow tự sinh khi tạo portfolio (default false, backward compat cho data cũ)
- Constructor: thêm optional param `isSeedDeposit = false`

### Application
- `CreatePortfolioCommandHandler`: sau khi tạo Portfolio, tự sinh `CapitalFlow` type `Deposit` với `IsSeedDeposit=true`, note "Vốn ban đầu khi tạo danh mục", flowDate = portfolio.CreatedAt. Chỉ tạo khi InitialCapital > 0. Inject `ICapitalFlowRepository`.
- `GetFlowHistoryQueryHandler`: aggregates (`TotalDeposits/Withdrawals/Dividends/NetCashFlow`) **exclude** seed flow. `Flows` list vẫn include để audit trail đầy đủ.
- `CapitalFlowItemDto`: thêm `IsSeedDeposit: bool`
- `DeleteCapitalFlowCommandHandler`: chặn xoá seed flow (return false) — seed là opening balance, không được remove

### Infrastructure
- `CapitalFlowRepository.GetTotalFlowByPortfolioIdAsync`: exclude seed khi sum → giữ Phase 1 formula `CurrentCapital = InitialCapital + NetCashFlow` đúng cho cả portfolio cũ (không có seed) và mới (có seed)
- `CashFlowAdjustedReturnService.CalculateTWRAsync / CalculateMWRAsync / GetAdjustedReturnSummaryAsync`: exclude seed khỏi flow stream → fix **bug double-count** (seed được dùng làm NPV baseline qua `-portfolio.InitialCapital`, không phải cash flow bổ sung)

### Frontend
- `CapitalFlowItem` interface: thêm `isSeedDeposit: boolean`
- Capital-flows history table (desktop + mobile): seed row hiển thị badge "Vốn ban đầu" (bg-blue), ẩn nút Xoá, hiện text "Khoá"

### Không cần data migration
- Portfolio cũ: không có seed flow → `GetTotalFlow` trả Σ các flow thực → Phase 1 formula `InitialCapital + NetCashFlow` = đúng
- Portfolio mới: có seed flow → `GetTotalFlow` exclude seed → trả chỉ các flow thực → formula vẫn đúng

### Tests
- `CapitalFlowTests`: +1 test cho `IsSeedDeposit` property
- `CreatePortfolioCommandHandlerTests`: +2 tests (seed flow được tạo với đúng attrs; InitialCapital=0 không tạo flow)
- `DeleteCapitalFlowCommandHandlerTests`: 3 tests new (user flow xoá được, seed bị chặn, wrong user bị chặn)
- `GetFlowHistoryQueryHandlerTests`: 1 test new (seed trong Flows list, exclude khỏi aggregates)
- Backend: Domain 609 (+1), Application 81 (+4), Infrastructure 199 → **889 tests pass**

### Docs
- `CHANGELOG.md` v2.42.0, plan checkpoint

---

## [v2.41.0] — 2026-04-18 · Capital — Lock InitialCapital (Phase 2)

**Branch:** `feat/capital-current-vs-initial`

### Backend
- `UpdatePortfolioCommand`: xoá field `InitialCapital` — chỉ cho update `Name`
- `UpdatePortfolioCommandHandler`: xoá call `portfolio.UpdateInitialCapital(...)` — vốn không còn sửa được qua update endpoint
- `UpdatePortfolioCommandValidator`: xoá rule cho `InitialCapital`
- `Portfolio.UpdateInitialCapital()` domain method giữ lại nhưng không còn caller ở Application layer (có thể dùng cho data migration hoặc admin ops trong tương lai)

### Frontend
- `UpdatePortfolioRequest` interface: xoá `initialCapital`
- `portfolio-edit.onSubmit()`: chỉ gửi `{ name }` (trước đây gửi cả initialCapital)

### Quyết định domain
- Vốn danh mục chỉ đổi qua `CapitalFlow` (Deposit/Withdraw/Dividend/Interest/Fee). Không cho "sửa sổ sách" trực tiếp trên `InitialCapital` nữa → single source of truth, audit trail qua flow history.

### TWR/MWR NetCashFlow
- Đã verify: `CashFlowAdjustedReturnService.NetCashFlow = totalDeposits(all inflows) - totalWithdrawals(all outflows)` — mathematically đã bằng `Σ SignedAmount` dù tên biến hơi gây hiểu nhầm. Không cần sửa.

### Tests
- `UpdatePortfolioCommandHandlerTests` (3 tests) — new: name-only update, wrong-user, not-found
- Backend: 75/75 Application tests pass (+3)

### Docs
- `CHANGELOG.md` v2.41.0

---

## [v2.40.0] — 2026-04-18 · Capital — Vốn hiện tại vs Vốn ban đầu (Phase 1)

**Branch:** `feat/capital-current-vs-initial`

### Backend
- `PortfolioSummaryDto` + `PortfolioDto` thêm `NetCashFlow` và `CurrentCapital` (= InitialCapital + NetCashFlow)
- `GetAllPortfoliosQueryHandler` + `GetPortfolioQueryHandler`: inject `ICapitalFlowRepository`, gọi `GetTotalFlowByPortfolioIdAsync` per portfolio
- `PnLController.GetOverallPnL`: mỗi portfolio trả thêm `NetCashFlow`, `CurrentCapital`; tổng level thêm `TotalNetCashFlow`, `TotalCurrentCapital`
- Catch block chỉ wrap PnL calculation — flow fetch giờ nằm ngoài try (không silent-swallow DB error)

### Frontend
- `PortfolioSummary` + `PortfolioDetail` + `PortfolioPnL` + `OverallPnLSummary` thêm `currentCapital`, `netCashFlow`
- Dropdowns (5): capital-flows, position-sizing, trade-wizard, trade-plan, trade-create → hiển thị `currentCapital` thay `initialCapital`
- List/detail/dashboard card: hiển thị "Vốn hiện tại" làm primary, "Vốn ban đầu" làm secondary (nhỏ, gray)
- Dashboard `cashBalance` dùng `currentCapital - totalInvested` (thay cho initial+flow)
- Dashboard `getPerformancePercent` dùng `currentCapital` làm denominator

### Bug fix — Position sizing
- **4 trang risk** (position-sizing, trade-wizard, trade-plan, trade-create) trước đây dùng `portfolio.initialCapital` làm `accountBalance` → khi user đã nạp/rút thêm, tính size lệnh sai. Giờ dùng `portfolio.currentCapital`.
- `trade-create` `remainingCash` tính từ `currentCapital - totalInvested + totalSold` (đủ vốn đã nạp thêm).

### Tests
- `GetAllPortfoliosQueryHandlerTests` (3 tests) — new, cover inflow/outflow/no-flow cases
- `GetPortfolioQueryHandlerTests` (3 tests) — new, cover happy/wrong-user/not-found paths
- Backend: 72/72 Application tests pass (+3 tests from 69)

### Docs
- `docs/plans/p2-capital-current-vs-initial.md` — plan với checkpoints
- `docs/business-domain.md` §3.1 — update công thức

---

## [v2.39.0] — 2026-04-18 · Trade Plan Form Editability Matrix (Strict)

**Branch:** `feat/trade-plan-state-machine-and-ux`

### Frontend — Trade Plan
- Áp dụng matrix phân quyền chỉnh sửa form theo trạng thái (Option A — strict lock):
  - **Draft/Ready**: chỉnh sửa tự do
  - **InProgress**: chỉ được tighten SL + sửa lot chưa khớp + cập nhật ghi chú/context
  - **Executed/Reviewed/Cancelled**: read-only, chỉ sửa được ghi chú (trừ Cancelled)
- State banner mới ở đầu form — thông báo rõ state hiện tại + gợi ý thao tác tiếp theo
- Tighten-SL gate: chặn nới SL trong InProgress (Long: newSl ≥ currentSl; Short: newSl ≤ currentSl)
- Readonly affordance: input locked đổi sang `bg-gray-50 text-gray-600 cursor-not-allowed`
- Save buttons hiện theo state (Draft: Nháp+Ready; Ready: Cập nhật; InProgress: Cập nhật SL/lot/ghi chú; terminal: view-only)
- Template panel ("Tải/Lưu template") ẩn khi chỉnh sửa plan non-Draft (tránh overwrite trường đã khoá)
- Hide "Thực hiện qua Wizard" / "Thực hiện ngay" khi plan terminal
- Wire lock cho: Entry Info, DCA inputs, Scenario nodes (all fields + add/remove/save-template buttons), Exit Targets, Risk Context, Checklist, Notes
- Risk-override button ẩn khi plan non-Draft

### Tests
- Frontend spec: `trade-plan.component.spec.ts` — 45 tests pass, cover toàn bộ matrix + tighten-SL gate + state banner + edge cases (null `loadedCurrentSl`)

### Docs
- `docs/project-context.md`: ghi nhận quyết định matrix
- `docs/plans/done/p2-trade-plan-editability.md`: plan chi tiết

---

## [v2.38.0] — 2026-04-17 · Trade Plan State Machine + Multi-lot UX

**Branch:** `feat/trade-plan-state-machine-and-ux`

### Domain
- Strict sequential state machine: Draft → Ready → InProgress → Executed → Reviewed
- `MarkReady()` idempotent (gọi trên plan đã Ready không throw)
- `Execute()` yêu cầu plan ở InProgress (trước đây không guard)
- Thêm `Restore()` cho Cancelled → Draft (clear `TradeId`, `TradeIds`, `ExecutedAt`)
- `ExecuteLot()` guard Executed/Reviewed/Cancelled

### Application
- `CreateTradePlanCommand` auto-chain Draft → Ready → InProgress → Executed khi status=Executed
- `UpdateTradePlanStatusCommand` auto-chain khi gọi inprogress/executed từ Draft/Ready
- Thêm case `restore` cho status update
- `KeyNotFoundException` thay vì `Exception` cho plan not found (trả 404 thay 500)

### Api
- `ExceptionMiddleware` map `InvalidOperationException` → 409 Conflict (trước là 500)

### Frontend — Trade Plan
- Fix bug: "Lưu & Sẵn sàng" trên plan đã Ready không trigger updateStatus nữa (tránh 500)
- "Thực hiện ngay" / "Wizard" từ multi-lot plan giờ execute đúng từng lô (không nhảy thẳng Executed)
- Nút xoá chỉ hiện cho Cancelled plans (tránh misclick)
- Thêm nút "Hoàn tác huỷ" cho Cancelled → Draft
- Enum timeHorizon fix: Medium → MediumTerm, Short → ShortTerm, Long → LongTerm
- Bỏ dropdown "Kỳ vọng" trùng lặp — gợi ý kịch bản dùng `plan.timeHorizon`
- Auto-load plan qua `?loadPlan=<id>`
- Panel "Đóng chiến dịch" auto-scroll vào view

### Frontend — Dashboard / Journals / Misc
- Advisory widget chuyển xuống ngay dưới banner cảnh báo rủi ro
- Form nhật ký: dropdown chọn trade theo portfolio thay vì input ID thô
- Route `/symbol-timeline` hỗ trợ cả path param và query param
- Bỏ hint `vndCurrency` thừa dưới input `appNumMask` (trade-create, alerts, capital-flows)

### Shared
- `TIME_HORIZON_OPTIONS` + `DEFAULT_TIME_HORIZON` constants dùng chung cho 3 dropdown
- Thống nhất nhãn theo docs: Ngắn hạn (< 3 tháng) / Trung hạn (3-12 tháng) / Dài hạn (> 1 năm)

### Tests
- 873 tests pass (Domain: 608, Application: 66, Infrastructure: 199)

### Docs
- `docs/trade-plans.md §2.2`: bảng chuyển trạng thái chi tiết, auto-chain logic, multi-lot flow, quy tắc xoá
- `docs/business-domain.md`: bổ sung link tham chiếu state lifecycle

---

## [v2.37.0] — 2026-04-11 · Dynamic Trading Checklist (P6) — Hoàn thành Roadmap TA 6 Phase

**Branch:** `feat/p1-expand-technical-indicators`

### Dynamic Checklist theo Strategy
- Checklist thay đổi theo timeFrame: Scalping (VWAP, Stochastic, Volume), DayTrading (EMA, RSI, MACD, Bollinger), Swing (ADX, Fibonacci, OBV), Position (SMA50/200, ADX weekly, MACD weekly)
- Tự động regenerate khi chọn chiến lược khác

### Multi-Timeframe Gate
- DayTrading: bắt buộc xác nhận xu hướng Daily
- Swing: bắt buộc xác nhận xu hướng Weekly
- Position: bắt buộc xác nhận xu hướng Monthly
- Scalping: không yêu cầu (quá nhanh)

### Weighted Scoring
- Weight 3 (●3 đỏ): bắt buộc — SL, R:R, Multi-TF gate, indicator chính
- Weight 2 (●2 vàng): quan trọng — indicator phụ, position sizing, accept loss
- Weight 1: tham khảo — journal, tâm lý, portfolio risk
- GO threshold: tất cả ●3 items checked + tổng điểm ≥ 70%
- Progress bar trực quan + chi tiết thiếu

### Roadmap hoàn thành ✅
Plan `technical-analysis-features.md` archived → `docs/plans/done/`

### Tests
- 868 tests pass (Domain: 603, Application: 65, Infrastructure: 199, Api: 1)

---

## [v2.36.0] — 2026-04-11 · Strategy Template Library — 7 chiến lược kỹ thuật (P5)

**Branch:** `feat/p1-expand-technical-indicators`

### Strategy Template Enhancement
- 5 fields mới trên StrategyTemplate: `SuggestedSlPercent`, `SuggestedRrRatio`, `SuggestedSlMethod`, `SuggestedAtrMultiplier`, `SuggestedSizingModel`
- 7 chiến lược kỹ thuật cập nhật đầy đủ P5 data:
  - **Scalping**: SL 1.5%, R:R 1.5, Manual SL, Fixed Risk sizing
  - **Day Trading** (mới): ATR×1.5, R:R 2, ATR-Based sizing
  - **Swing Trading**: SL 5%, R:R 2, Support-based SL, ATR-Based sizing
  - **Position Trading** (mới): SL 10%, R:R 3, Chandelier Exit, Turtle sizing
  - **Breakout**: SL 5%, R:R 2, Support-based SL, ATR-Based sizing
  - **Mean Reversion**: SL 5%, R:R 1.5, ATR×1.5, Volatility-Adjusted sizing
  - **Momentum**: SL 8%, R:R 2, MA Trailing, ATR-Based sizing

### Frontend
- Template detail hiển thị badges: R:R, SL%, SL method, sizing model
- Chọn template → tạo Strategy có đầy đủ SL method → Trade Plan auto-fill
- Trade Plan: tự động chọn SL method pill khi chiến lược có `suggestedSlMethod`

### Tests
- 868 tests pass (Domain: 603, Application: 65, Infrastructure: 199, Api: 1)

---

## [v2.35.0] — 2026-04-11 · Advanced Stop Loss & SL Method Selector (P4)

**Branch:** `feat/p1-expand-technical-indicators`

### 5 phương pháp Stop Loss
- **Cố định (nhập tay)**: Nhập SL trực tiếp (có sẵn)
- **ATR Stop Loss**: `Entry ∓ k × ATR(14)`, k = 1.5/2.0/3.0 (ngắn/trung/dài hạn)
- **Chandelier Exit**: `HH(22) - 3×ATR` (mua) / `LL(22) + 3×ATR` (bán)
- **MA Trailing**: EMA(21) làm SL floor
- **Hỗ trợ/Kháng cự gần nhất**: Swing low (mua) / Swing high (bán)

### Backend
- 3 trường mới trong TechnicalAnalysisResult: `Ema21`, `HighestHigh22`, `LowestLow22`
- Tính toán trong TechnicalIndicatorService

### Frontend
- Pill selector dưới ô Stop-Loss, hỗ trợ cả Buy/Sell direction
- ATR multiplier selector (1.5×/2×/3×) với gợi ý ngắn/trung/dài hạn
- SL pills auto-cập nhật khi thay đổi giá vào lệnh
- Auto-fetch technical analysis khi tra cứu mã CP

### Tests
- 868 tests pass (Domain: 603, Application: 65, Infrastructure: 199, Api: 1)
- 9 test mới cho Ema21, HighestHigh22, LowestLow22

---

## [v2.34.0] — 2026-04-11 · Advanced Position Sizing Calculator (P3)

**Branch:** `feat/p1-expand-technical-indicators`

### 5 mô hình Position Sizing
- **Cố định % rủi ro** (có sẵn): `Size = (Vốn × %Risk) / RiskPerShare`
- **Theo ATR**: `Size = (Vốn × %Risk) / (N × ATR)` — tự điều chỉnh theo biến động thị trường
- **Kelly Criterion**: Half-Kelly, cap 25% — sizing tối ưu dựa trên win rate, avg win/loss
- **Turtle (1 unit)**: `1 Unit = 1% Vốn / ATR` — thêm tối đa 3 unit khi lời
- **Điều chỉnh biến động**: Scale Fixed Risk theo ATR% (baseline 2%, clamp 0.5x-1.5x)

### Backend
- Mới: `IPositionSizingService` + `PositionSizingService` (stateless, Singleton)
- API endpoint: `POST /api/v1/risk/position-sizing`

### Frontend
- Bảng so sánh mô hình trong Trade Plan: số CP, % danh mục, trạng thái giới hạn
- Click chọn mô hình → auto-fill số lượng cổ phiếu
- Auto-fetch ATR khi tra cứu mã CP, truyền vào API sizing

### Tests
- 859 tests pass (Domain: 603, Application: 65, Infrastructure: 190, Api: 1)
- 21 test mới cho 5 mô hình sizing + edge cases

---

## [v2.33.0] — 2026-04-11 · Confluence Score, Market Condition, Divergence Detection (P2)

**Branch:** `feat/p1-expand-technical-indicators`

### Confluence Score (Điểm tổng hợp 0-100)
- Trọng số 5 nhóm: Xu hướng 30%, Động lượng 25%, Khối lượng 20%, Biến động 15%, Vị trí giá 10%
- Progress bar trực quan + đánh giá: Tín hiệu tích cực / Tiêu cực / Trung tính

### Market Condition Classifier (Trạng thái thị trường)
- Phân loại tự động dựa trên ADX: Xu hướng rất mạnh (≥40) / Có xu hướng (≥25) / Đi ngang (<25)
- Gợi ý chiến lược phù hợp: Trend Following / Mean Reversion

### Divergence Detection (Phát hiện phân kỳ)
- Auto-detect phân kỳ RSI và MACD vs giá (swing highs/lows)
- Phân kỳ tăng (bullish divergence) + Phân kỳ giảm (bearish divergence)
- Bộ lọc: min 5 bar giữa swing points + min 0.5% chênh lệch giá (giảm false positive)

### Frontend
- 3 card mới trên Smart Signals: Điểm Confluence (gauge + progress bar), Trạng thái thị trường (badge + chiến lược), Phân kỳ (alert card chi tiết RSI/MACD)

### Tests
- 838 tests pass (Domain: 603, Application: 65, Infrastructure: 169, Api: 1)
- 18 test mới cho Confluence Score, Market Condition, Divergence Detection

---

## [v2.32.0] — 2026-04-10 · Help Center — Hướng dẫn sử dụng

**Branch:** `feat/p1-expand-technical-indicators`

### Trang Hướng dẫn sử dụng (`/help`)
- **8 chủ đề**: Bắt đầu, Giao dịch, Kế hoạch GD, Phân tích thị trường, Quản lý rủi ro, Phân tích hiệu suất, Công cụ hỗ trợ, Chiến lược giao dịch
- **Full-text search**: Tìm kiếm toàn văn tiếng Việt, hỗ trợ gõ không dấu (VD: "giao dich" → "Giao dịch")
- **Markdown rendering**: Đọc nội dung từ file `.md` trong `assets/docs/`, render bằng `marked`
- **Highlight kết quả**: Snippet 120 ký tự với match được highlight `<mark>`
- **Navigation**: Nút "Hướng dẫn" trên header + bottom nav mobile

---

## [v2.31.0] — 2026-04-10 · Mở rộng Technical Indicators — Stochastic, ADX, OBV, MFI

**Branch:** `feat/p1-expand-technical-indicators`

### Chỉ báo kỹ thuật mới (4 indicators)
- **Stochastic Oscillator (14,3,3):** Slow Stochastic %K/%D, tín hiệu quá mua (>80) / quá bán (<20)
- **ADX (14) + Directional Indicators:** Đo sức mạnh xu hướng (trending >25 / strong >40 / sideway <20), +DI/-DI xác định hướng
- **OBV (On-Balance Volume):** Theo dõi dòng tiền tích lũy, tín hiệu rising/falling
- **MFI (14) — Money Flow Index:** RSI có volume, quá mua (>80) / quá bán (<20)

### Cải thiện hệ thống tín hiệu
- **Voting system:** Nâng từ 6 lên 10 chỉ báo tham gia bỏ phiếu (EMA, RSI, MACD, Volume, Bollinger, ATR, Stochastic, ADX+DI, OBV, MFI)
- **Signal thresholds:** Điều chỉnh ngưỡng cho 10 indicators (strong_buy ≥6, buy ≥4, sell ≥4, strong_sell ≥6)

### Frontend
- 4 indicator cards mới trong Smart Signals grid: Stochastic, ADX (+DI/-DI), OBV (dòng tiền), MFI
- OBV formatting: Hỗ trợ giá trị âm (e.g., -45M)

### Tests
- 820 tests pass (Domain: 603, Application: 65, Infrastructure: 151, Api: 1)
- 24 test mới cho Stochastic, ADX, OBV, MFI, voting system

---

## [v2.30.0] — 2026-04-10 · Auto-suggest 2 chiều Portfolio ↔ Symbol

**Branch:** `fix/user-feedback-updates`

### Trade Create UX Improvements
- **Auto-suggest danh mục → cổ phiếu:** Chọn danh mục → hiện chips các cổ phiếu đang có vị thế (click chọn nhanh)
- **Auto-suggest cổ phiếu → danh mục:** Chọn/nhập symbol → auto-select danh mục chứa vị thế (nếu duy nhất), highlight "Có vị thế" trong dropdown (nếu nhiều)
- **BÁN — mismatch detection:** Alert banner đỏ nổi bật + disable nút bán khi symbol không có vị thế trong danh mục đã chọn
- **BÁN — smart filtering:** Chips chỉ hiện cổ phiếu có quantity > 0 (bán được)
- **MUA — convenience:** Chips hiện tất cả cổ phiếu trong danh mục (tiện mua thêm), không giới hạn mã mới

### Code Quality
- Fix: impure method call trong `*ngFor` → cache `matchingPortfolioIds` dạng `Set<string>`
- Fix: symbol blur handler — trigger auto-suggest khi user gõ trực tiếp
- Fix: loại bỏ redundant `.toUpperCase()` theo convention `appUppercase` directive

### Tests
- 22 frontend tests (Jasmine/Karma) covering bidirectional auto-suggest logic
- Fix `tsconfig.spec.json` — thêm `polyfills.ts` cho Karma test runner

---

## [v2.29.0] — 2026-04-10 · P0.7 Campaign Review — Đóng chiến dịch & Phân tích hiệu suất

**Branch:** `feat/p7-improvements`

### P0.7 — Campaign Review (đóng chiến dịch với auto-metrics)
- **CampaignReviewService:** Auto-calculate P&L metrics từ trades thực tế (P&L amount, %, VND/ngày, annualized return, target achievement)
- **TimeHorizon:** Dropdown tầm nhìn đầu tư (Ngắn hạn / Trung hạn / Dài hạn) trên TradePlan
- **Review workflow:** Preview metrics → Confirm → Đóng chiến dịch (Executed → Reviewed)
- **Update lessons:** Cập nhật bài học rút ra sau review
- **Pending review:** Danh sách plans Executed chờ review
- **Campaign Analytics page:** `/campaign-analytics` — summary cards, comparison table, best/worst plan, lessons feed
- **API endpoints:**
  - `POST /api/v1/trade-plans/{id}/review` — đóng chiến dịch
  - `GET /api/v1/trade-plans/{id}/review/preview` — xem trước metrics
  - `PATCH /api/v1/trade-plans/{id}/review/lessons` — cập nhật bài học
  - `GET /api/v1/trade-plans/pending-review` — danh sách chờ review
  - `GET /api/v1/trade-plans/campaign-analytics?timeHorizon=ShortTerm` — phân tích cross-plan

### Domain Changes
- **TimeHorizon enum:** ShortTerm / MediumTerm / LongTerm
- **CampaignReviewData value object:** Embedded trong TradePlan
- **MarkReviewed(CampaignReviewData):** Bắt buộc truyền review data khi đóng
- **PlanReviewedEvent:** Domain event mới

### Tests
- 796 tests pass (Domain: 603, Application: 65, Infrastructure: 127)
- 24 new domain tests (TradePlanReviewTests.cs)
- 9 new infrastructure tests (CampaignReviewServiceTests.cs)

---

## [v2.28.0] — 2026-04-10 · P0 Phase 4 — Scenario Consultant & Advisory System

**Branch:** `feat/p0-phase4-advisory`

### P0.6 — Scenario Consultant (gợi ý kịch bản có cơ sở kỹ thuật)
- **ScenarioConsultantService:** Phân tích kỹ thuật → gợi ý kịch bản chốt lời, cắt lỗ, mua thêm, sideway
- **Confluence scoring:** Vùng có ≥ 2 indicator hội tụ (S/R + Fibonacci + EMA + Bollinger) → ưu tiên cao hơn
- **Tầm nhìn đầu tư:** Dropdown Ngắn hạn / Trung hạn / Dài hạn — tự động fill mốc thời gian
- **Preview + chọn lọc:** Xem gợi ý kèm reasoning, checkbox từng node, nút "Áp dụng gợi ý" / "Tạo kế hoạch từ gợi ý"
- **API:** `GET /api/v1/trade-plans/scenario-suggestion?symbol=HPG&entryPrice=75000&timeHorizon=Medium`

### P0.5 — Gợi ý hành động theo vùng giá
- **ScenarioAdvisoryService:** Quét giá hiện tại vs kịch bản active → gợi ý hành động on-demand
- **Dashboard widget:** "Gợi ý hành động" hiển thị khi giá vào vùng trigger
- **Wording advisory:** "Xem xét bán 30%", "Xem xét cắt lỗ" — không dùng "Đã..." hay "Cần phải..."
- **API:** `GET /api/v1/trade-plans/advisories`

### Code Review Fixes
- Input validation trên endpoint (symbol + entryPrice)
- UserId scoping cho scenario-suggestion
- N+1 → batch parallel fetch giá (deduplicate symbols)
- Category mismatch backend/frontend (AddPosition)
- Nullable RSI explicit check
- CancellationToken propagation
- trackBy cho ngFor suggestion list

### Tests
- 768 tests pass (Domain: 584, Application: 65, Infrastructure: 118, Api: 1)

---

## [v2.27.0] — 2026-04-10 · P0 Phase 2+3 — Flowchart, Fibonacci, Candlestick Chart

**Branch:** `feat/p0-phase2-3-improvements`

### P0.3 — Visual Flowchart Tree UI
- **Connector lines:** CSS-only vertical/horizontal connectors giữa parent → children
- **Status colors:** Xanh (Đã kích hoạt), Vàng (Chờ), Xám (Bỏ qua)
- **Collapsible branches:** Thu gọn/mở rộng nhánh kịch bản con

### P0.6a — Fibonacci Retracement/Extension + EMA200
- **Fibonacci levels:** 23.6%, 38.2%, 50%, 61.8%, 78.6% retracement + 127.2%, 161.8% extension
- **EMA200:** Thêm EMA 200 phiên cho phân tích trung/dài hạn
- **Auto-detect swing points:** Sử dụng lại logic support/resistance hiện có

### P0.6c — Mở rộng Price History
- **Default 12 tháng** (thay vì 6 tháng) — đủ cho EMA200 (~200 phiên)
- **Tham số `months`** cho phép tùy chỉnh khoảng thời gian phân tích

### P0.6b — Candlestick Chart + Overlays
- **Candlestick:** Thay line chart bằng nến Nhật (OHLC) — xanh/đỏ
- **EMA overlays:** EMA20 (xanh), EMA50 (cam), EMA200 (tím) — đường ngang
- **S/R overlays:** Hỗ trợ (xanh nét đứt), Kháng cự (đỏ nét đứt)
- **Fibonacci overlays:** Các mức Fib màu vàng amber trên biểu đồ
- **Toggle toolbar:** 4 nút bật/tắt: Nến, EMA, S/R, Fibonacci

### Tests
- 755 tests pass (Domain: 584, Application: 65, Infrastructure: 105, Api: 1)

---

## [v2.26.0] — 2026-04-10 · P0 Phase 1 — Scenario Playbook Improvements

**Branch:** `feat/p0-phase1-improvements`

### P0.1 — Scenario History & Status Dashboard
- **Lịch sử kích hoạt:** Hiển thị trạng thái từng node (Đã kích hoạt / Chờ / Bỏ qua) với thời gian + giá
- **Timeline panel:** Bên dưới tree editor khi plan đang InProgress
- **API:** `GET /api/v1/trade-plans/{id}/scenario-history`

### P0.2 — User Custom Templates (Save/Load)
- **Lưu mẫu kịch bản:** Nút "Lưu mẫu kịch bản" tạo template tùy chỉnh từ tree hiện tại
- **Dropdown phân loại:** Mẫu hệ thống (3 preset) | Mẫu của tôi (user templates)
- **Xoá mẫu:** Nút xoá kèm xác nhận
- **API:** `POST /DELETE /api/v1/trade-plans/scenario-templates`

### P0.4 — ATR Trailing Stop thực tế
- **Fix placeholder:** Thay `entryPrice × 0.02` bằng ATR(14) thực tế từ `TechnicalIndicatorService`
- **Fallback:** Giữ proxy cũ khi thiếu dữ liệu ATR + log warning
- **Lazy fetch:** Chỉ gọi `AnalyzeAsync` khi gặp ATR trailing stop node, cache kết quả

### Code Review Fixes
- Fix Enum.Parse → TryParse cho input validation (trả 400 thay vì 500)
- Fix sync index creation → async trong ScenarioTemplateRepository
- Fix thiếu try/catch 404 cho DELETE endpoint
- Fix alert-node matching: dùng TriggeredAt timestamp thay vì label string
- Thêm confirm dialog khi xoá mẫu kịch bản

### Tests
- 747 tests pass (Domain: 584, Application: 65, Infrastructure: 97, Api: 1)

---

## [v2.25.1] — 2026-04-09 · P7 Bugfix & Chart UX Polish

**Branch:** `feat/p7-improvements`

### Vietstock Crawl Fix
- **Fix CSRF token parsing:** Regex fallback 3 tầng cho unquoted HTML attributes
- **Fix 403:** Thêm User-Agent, CookieContainer, Referer, X-Requested-With headers
- **Fix URL bài viết:** Dùng `vietstock.vn` thay vì `finance.vietstock.vn`

### Chart UX Improvements
- **Sắp xếp timeline newest-first** (mới nhất lên đầu)
- **Mở rộng chart đến ngày hiện tại** sử dụng giá real-time
- **Thay emoji markers bằng ký tự ngắn** (T/J/E/A) kèm số lượng
- **Crosshair tooltip:** Hiển thị chi tiết sự kiện khi hover
- **Sanitize tooltip innerHTML** chống XSS từ dữ liệu API
- **Refactor nested subscribe → switchMap** (RxJS best practice)

### Tests
- 732 tests pass (Domain: 584, Application: 54, Infrastructure: 94)

---

## [v2.25.0] — 2026-03-27 · P7 Symbol Timeline Improvements

**Branch:** `feat/p7-improvements`

### P7.1: Emotion ↔ P&L Correlation
- **Correlation cảm xúc → kết quả GD:** Tính trung bình P&L %, win rate cho mỗi cảm xúc
- **Insight text:** Highlight cảm xúc tốt nhất/tệ nhất với win rate và P&L TB

### P7.2: Confidence Calibration
- **Hiệu chuẩn mức tự tin:** So sánh confidence level ranges (Low/Med/High/Very High) với win rate thực tế
- **Calibration widget:** Thanh ngang với trạng thái Phù hợp/Quá tự tin/Chưa tự tin

### P7.3: Behavioral Pattern Detection
- **4 patterns:** FOMO Entry, Panic Sell, Revenge Trading, Overtrading
- **Pattern alerts panel:** Cards severity (Critical/Warning) + mô tả + ngày
- **IBehavioralAnalysisService** tích hợp vào timeline response

### P7.4: Chart UX Enhancements
- **Chuyển sang LineSeries:** Thay CandlestickSeries bằng LineSeries (match thực tế hiển thị)

### P7.5: Dedicated AI Timeline Review
- **Rich AI context:** Gồm correlation, calibration, behavioral patterns, full journal/trade history
- **Prompt template** chuyên biệt cho trading psychology coach

### P7.6: Emotion Trend Over Time
- **Xu hướng cảm xúc theo tháng:** Stacked bar chart, dominant emotion, average confidence
- **Trend insight:** So sánh tháng gần nhất vs tháng trước

### P7.7: Export Timeline
- **Xuất CSV:** Tải file CSV với tất cả timeline items (Ngày, Loại, Tiêu đề, Cảm xúc...)
- **Sao chép tóm tắt:** Copy text summary vào clipboard

### P7.8: Vietstock Event Crawl
- **Auto-crawl tin tức + sự kiện DN** từ Vietstock API (GetNews + EventsTypeData)
- **CSRF token flow**, `/Date(ms)/` parser, ChannelID → MarketEventType mapping
- **Dedup:** Bỏ qua events trùng (Symbol + Title + Date)
- **Nút "Cập nhật tin tức"** trên Symbol Timeline page
- **API:** `POST /api/v1/market-events/crawl`

---

## [v2.24.0] — 2026-03-27 · P1-P4 Improvements

**Branch:** `feat/p1-post-trade-review`

### P1: Post-Trade Review Workflow

- **Pending review query**: Lấy SELL trades chưa có JournalEntry PostTrade
- **Dashboard widget "Chờ đánh giá"**: Hiện SELL trades chưa review, click → Symbol Timeline
- **Trades list cột "Nhật ký"**: Icon check (đã review) / pencil (chưa review) cho mỗi SELL trade
- Endpoint: `GET /api/v1/journal-entries/pending-review`

### P2: Stress Test — Dynamic Beta

- **Dynamic beta**: Lấy beta từ API, fallback tính từ correlation VN-INDEX, fallback cuối 1.0
- Thay thế `estimatedBetas` hardcoded (~20 mã) bằng API call
- Endpoint: `POST /api/v1/risk/portfolio/{id}/stress-test`

### P3: Technical Indicators — Bollinger Bands + ATR

- **Bollinger Bands(20, 2)**: Upper, middle (SMA20), lower, bandwidth, %B, signal (squeeze/breakout)
- **ATR(14)**: Giá trị ATR, ATR% (% giá hiện tại)
- Signal scoring mở rộng: 6 indicators (thêm Bollinger + ATR)
- 2 indicator cards mới trong market-data component

### P4: Risk Budgeting — Daily Trade Limits

- **RiskProfile mở rộng**: `MaxDailyTrades`, `DailyLossLimitPercent`
- **Risk budget card**: "Ngân sách rủi ro hôm nay" — trades/limit, P&L, trạng thái khóa
- **Risk profile form**: 2 fields mới (số lệnh tối đa/ngày, giới hạn lỗ/ngày)
- `ITradeRepository.GetByPortfolioIdAndDateRangeAsync` — filter trades theo ngày
- Endpoint: `GET /api/v1/risk/portfolio/{id}/budget`

### Tests

- 702 tests pass (Domain: 584, Application: 39, Infrastructure: 78, Api: 1)
- P1: 5 test cases cho GetTradesPendingReviewQueryHandler
- P2: 5 test cases cho CalculateStressTestAsync
- P3: 8 test cases cho Bollinger Bands + ATR
- P4: 10 test cases cho RiskProfile entity + CheckRiskBudget

---

## [v2.23.0] — 2026-03-27 · Symbol Timeline (P7)

**Branch:** `feat/p7-symbol-timeline`

### Thêm mới

- **Symbol Timeline**: Trang dòng thời gian cho mỗi mã CK — biểu đồ nến + nhật ký + giao dịch + sự kiện trên cùng 1 timeline
- **JournalEntry (standalone)**: Ghi nhật ký bất kỳ lúc nào gắn với symbol — không cần có giao dịch (5 loại: Quan sát / Trước GD / Đang GD / Sau GD / Tổng kết)
- **MarketEvent**: Thêm sự kiện thị trường (KQKD, cổ tức, tin tức, vĩ mô...) hiển thị trên biểu đồ
- **Candlestick chart**: Biểu đồ nến với lightweight-charts, markers cho nhật ký/giao dịch/sự kiện/cảnh báo
- **Emotion Ribbon**: Sub-chart cảm xúc bên dưới biểu đồ nến — màu theo cảm xúc, độ cao theo mức tự tin
- **Emotion Summary**: Phân tích phân bố cảm xúc, tự tin trung bình, cảm xúc chính
- **AI Timeline Review**: AI phân tích pattern cảm xúc ↔ giao dịch ↔ kết quả
- **Unified Timeline API**: Gom nhật ký + giao dịch + sự kiện + cảnh báo, tính holding periods + emotion summary
- **Quick-add forms**: Ghi nhật ký và thêm sự kiện inline trên trang timeline
- **Timeline links**: Nút 📊 trên Watchlist, Positions, Trades → navigate đến Symbol Timeline

### Backend

- Entity: `JournalEntry` (Domain) — 5 loại, cảm xúc, snapshot giá, tags, rating
- Entity: `MarketEvent` (Domain) — 7 loại sự kiện thị trường
- Repository: `JournalEntryRepository`, `MarketEventRepository` (MongoDB)
- CQRS: Create/Update/Delete JournalEntry, GetBySymbol, GetSymbolTimeline
- CQRS: CreateMarketEvent, GetMarketEvents
- API: `/api/v1/journal-entries`, `/api/v1/symbols/{symbol}/timeline`, `/api/v1/market-events`

### Frontend

- Component: `SymbolTimelineComponent` (`/symbol-timeline/:symbol`)
- Services: `JournalEntryService`, `MarketEventService`
- Dependency: `lightweight-charts` v4.2.2

### Cải thiện (Code Review)

- Fix memory leak: ResizeObserver disconnect khi destroy component
- Fix race condition: takeUntil cleanup cho tất cả HTTP subscriptions
- Fix bảo mật: thêm `rel="noopener noreferrer"` cho link ngoài
- Fix hiệu năng: gộp N+1 trade query → 1 query `GetByUserPortfoliosAndSymbolAsync`
- Fix hiệu năng: alert history filter tại DB thay vì load toàn bộ vào memory
- Fix logic: BUY đầu tiên giờ xuất hiện trong HoldingPeriod.Changes
- Fix type: `decimal` thay `int` cho Quantity trong holding period DTOs
- Fix casing: normalize PascalCase → camelCase 1 lần khi nhận data, xóa 25+ fallback patterns
- Thêm soft delete + UpdatedAt cho MarketEvent entity
- Thêm validation null cho `symbol` query param (trả 400 thay vì 500)
- Tách SymbolTimelineController ra file riêng

### Tests

- Domain: 47 tests (JournalEntry: 30, MarketEvent: 17)
- Application: 9 tests (CreateJournalEntryCommandHandler: 5, GetSymbolTimelineQueryHandler: 4)

---

## [v2.22.0] — 2026-03-27 · Scenario Playbook

**Branch:** `feat/capital-flows-visibility`

### Thêm mới

- **Scenario Playbook**: Chế độ nâng cao cho Trade Plan — cây quyết định (decision tree) với điều kiện + hành động liên kết
- **2 chế độ thoát lệnh**: Toggle Cơ bản (exit targets cũ) / Nâng cao (scenario tree) — backward compatible
- **5 loại điều kiện**: Giá >=, Giá <=, Thay đổi %, Chạm trailing stop, Sau N ngày
- **7 loại hành động**: Bán %, Bán tất cả, Dời SL, SL về hòa vốn, Bật trailing stop, Thêm vị thế, Thông báo
- **Trailing Stop chi tiết**: 3 phương pháp (%, ATR ước tính, Cố định VNĐ) + giá kích hoạt + bước tối thiểu
- **3 mẫu kịch bản**: An toàn, Cân bằng, Tích cực — áp dụng 1 click
- **Tự động đánh giá**: Worker mỗi 15 phút evaluate scenarios + tạo AlertHistory thông báo

### Backend

- `TradePlan.cs` — thêm ExitStrategyMode, ScenarioNodes, TrailingStopConfig + 3 domain methods + ScenarioNodeTriggeredEvent
- `ScenarioEvaluationService.cs` — tự động evaluate conditions, update trailing stops, tạo alert
- `TradePlanRepository.cs` — thêm `GetAdvancedInProgressAsync` (filtered tại MongoDB)
- `TradePlansController.cs` — 2 endpoints mới: trigger scenario node, get preset templates
- `Worker.cs` — thêm `EvaluateScenarioPlaybooksAsync`

### Frontend

- `trade-plan.component.ts` — toggle Cơ bản/Nâng cao, scenario tree editor (recursive ng-template), preset selector, trailing stop config inline
- `trade-plan.service.ts` — thêm interfaces ScenarioNodeDto, TrailingStopConfigDto, ScenarioPreset + 2 API methods

### Tests

- 33 tests mới: Domain (20) + Application (3) + Infrastructure (10)

---

## [v2.21.0] — 2026-03-26 · Capital Flows Visibility

**Branch:** `feat/capital-flows-visibility`

### Thêm mới

- **Dashboard — Tiền mặt khả dụng**: Card mới hiển thị cash balance (Vốn ban đầu + Dòng vốn ròng - Đã đầu tư), link đến `/capital-flows`
- **Dashboard — TWR dưới Lãi/Lỗ**: Hiển thị Time-Weighted Return % bên dưới card Tổng Lãi/Lỗ, cho thấy hiệu suất chiến lược thực sự
- **Analytics — TWR vs MWR card**: So sánh TWR (kỹ năng đầu tư) với MWR (lợi nhuận thực tế), giải thích tự động khi TWR ≠ MWR (timing nạp/rút tiền)
- **Equity Curve — Flow markers**: Điểm tam giác xanh ▲ (nạp tiền/cổ tức) và đỏ ▼ (rút tiền/phí) overlay trên biểu đồ equity curve ở cả Dashboard và Analytics
- **Smart Nudge**: Banner gợi ý ghi nhận dòng vốn khi phát hiện giá trị danh mục thay đổi >20% mà không có giao dịch tương ứng

### Frontend

- `dashboard.component.ts` — thêm cash balance card, TWR, flow markers trên mini equity chart, smart nudge banner; inject `CapitalFlowService`
- `analytics.component.ts` — thêm TWR/MWR comparison card, flow markers trên equity curve chart; inject `CapitalFlowService`

---

## [v2.20.0] — 2026-03-25 · Portfolio Optimizer & Risk Dashboard Improvements

**Branch:** `feat/portfolio-optimizer-risk-dashboard`

### Thêm mới

- **Portfolio Optimizer** — phân tích tối ưu hóa danh mục trên trang `/risk-dashboard`:
  - **Cảnh báo tập trung**: cảnh báo khi vị thế vượt giới hạn MaxPositionSizePercent (warning/danger)
  - **Phân bổ theo ngành**: nhóm vị thế theo ngành từ `IFundamentalDataProvider`, cảnh báo khi vượt MaxSectorExposurePercent
  - **Cặp tương quan cao**: cảnh báo cặp CP tương quan >0.5 (medium) / >0.7 (high)
  - **Điểm đa dạng hóa**: score 0-100 dựa trên concentration, sector, correlation, số vị thế
  - **Gợi ý tối ưu**: khuyến nghị giảm tỷ trọng, đa dạng hóa ngành
- **Trailing Stop Monitoring** — giám sát trailing stop real-time trên `/risk-dashboard`:
  - Cảnh báo theo severity: danger (≤2%), warning (≤5%), safe (>5%)
  - Gợi ý nâng trailing stop khi giá tăng cao hơn mức cũ
- **PositionRiskItem mở rộng** — thêm `sector`, `beta`, `positionVaR` cho từng vị thế

### Backend

- `GetPortfolioOptimizationQuery` + handler (CQRS) — phân tích tối ưu hóa danh mục
- `GetTrailingStopAlertsQuery` + handler (CQRS) — cảnh báo trailing stop
- `RiskCalculationService` — thêm `GetPortfolioOptimizationAsync()`, `GetTrailingStopAlertsAsync()`; inject thêm `IRiskProfileRepository`, `IFundamentalDataProvider`
- API mới: `GET /api/v1/risk/portfolio/{id}/optimization`, `GET /api/v1/risk/portfolio/{id}/trailing-stop-alerts`

### Frontend

- `RiskService` — 7 interfaces mới + 2 methods (`getPortfolioOptimization`, `getTrailingStopAlerts`)
- `RiskDashboardComponent` — 2 sections mới: Tối ưu hóa danh mục + Giám sát Trailing Stop

### Tests

- 8 application handler tests (optimization + trailing stop queries)
- 13 infrastructure service tests (concentration, sector, correlation, diversification score, trailing stop alerts)

---

## [v2.19.0] — 2026-03-25 · Progressive Web App (PWA)

**Branch:** `feat/pwa`

### Thêm mới

- **PWA support** — cài đặt ứng dụng lên màn hình chính trên mobile/desktop
  - `manifest.webmanifest` — app metadata, icons, shortcuts (Dashboard, Danh mục)
  - `@angular/service-worker` — service worker caching với ngsw
  - **Offline caching** — shell app cache tự động; API cache theo nhóm:
    - Market data: 15 giây (freshness)
    - Portfolio/Positions/PnL: 1 phút (freshness)
    - Analytics/Risk/Snapshots: 5 phút (performance)
    - Watchlist/Strategies/Journals: 2 phút (freshness)
  - **Tự động cập nhật** — banner thông báo khi có phiên bản mới
  - **Banner cài đặt** — gợi ý cài đặt ứng dụng (có thể bỏ qua, nhớ lựa chọn)
  - **App icons** — SVG icons cho tất cả kích thước (72→512px)
  - **Meta tags** — theme-color, apple-mobile-web-app, viewport-fit=cover

### Frontend

- `PwaService` — quản lý install prompt, lắng nghe SW update events
- `PwaInstallBannerComponent` — banner cài đặt + banner cập nhật
- `app.component.ts` — thêm `PwaInstallBannerComponent`
- `main.ts` — `provideServiceWorker` (chỉ bật ở production/staging)
- `angular.json` — `serviceWorker: ngsw-config.json` cho production + staging

---

## [v2.19.0] — 2026-03-24 · Comprehensive Stock Analysis (12th AI Use Case)

**Branch:** `feature/comprehensive-stock-analysis`

### Thêm mới

- **AI Comprehensive Stock Analysis (use case #12)**: Phân tích toàn diện cổ phiếu kết hợp đa nguồn dữ liệu từ 24hmoney — chỉ số tài chính, báo cáo tài chính, kế hoạch kinh doanh, cổ tức, cổ phiếu cùng ngành, giao dịch nước ngoài, báo cáo phân tích từ CTCK
  - Nút "🤖 AI Phân tích Toàn diện" trên trang `/market-data`
  - Endpoint: `POST /api/v1/ai/comprehensive-analysis` (SSE streaming)

### Backend

- `IComprehensiveStockDataProvider` interface (Application layer) — định nghĩa contract cho dữ liệu phân tích toàn diện
- `HmoneyComprehensiveDataProvider` (Infrastructure/Services/Hmoney/) — tích hợp 8 endpoint 24hmoney:
  - `/v2/ios/companies/index` — chỉ số tài chính: P/E, P/B, ROE, ROA, EPS, Beta, MarketCap
  - `/api/v2/web/company/detail` — thông tin chi tiết công ty
  - `/api/v2/web/company/financial-report` — báo cáo tài chính (BCTC)
  - `/api/v2/web/company/plan` — kế hoạch kinh doanh
  - `/api/v2/web/announcement/dividend-events` — sự kiện cổ tức
  - `/api/v2/web/stock-recommend/get_stock_related_bussiness` — cổ phiếu cùng ngành
  - `/api/v2/web/stock/foreign-trading-series` — chuỗi giao dịch nước ngoài
  - `/api/v2/web/announcement/report-analytics` — báo cáo phân tích từ CTCK
- `HmoneyComprehensiveApiModels.cs` — response DTOs cho các endpoint trên
- `AiAssistantService`: thêm context builder + streaming method cho comprehensive-analysis (nâng tổng lên 12 use cases)
- `AiController`: thêm endpoint `POST /ai/comprehensive-analysis` (SSE)

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
