# P2 — Capital: Vốn hiện tại vs Vốn ban đầu

**Branch:** `feat/capital-current-vs-initial`
**Started:** 2026-04-18

## Mục đích

Phân biệt rõ 2 khái niệm "vốn" trong hệ thống và fix bug position sizing:
- **Vốn ban đầu** (InitialCapital) — vốn khai báo lúc tạo portfolio, immutable
- **Vốn hiện tại** (CurrentCapital) = InitialCapital + NetCashFlow — vốn thực tại ở thời điểm hiện tại, phản ánh mọi nạp/rút/cổ tức/phí

## Vấn đề hiện tại

1. **Bug sizing nghiêm trọng**: 4 trang risk (position-sizing, trade-wizard, trade-plan, trade-create) dùng `portfolio.initialCapital` làm `accountBalance` → tính rủi ro sai khi user đã nạp/rút thêm.
2. **UX confusing**: UI lúc hiện initialCapital, lúc hiện initial+flow, không thống nhất khái niệm.
3. **Không single source of truth**: InitialCapital có thể update qua API → user có thể sửa "sổ sách" không qua CapitalFlow.
4. **TWR/MWR subtle bug**: `CashFlowAdjustedReturnService.NetCashFlow = deposits - withdrawals` bỏ qua Dividend/Interest/Fee.
5. **Không có initial Deposit flow**: tạo portfolio với 100M không sinh CapitalFlow → lịch sử "100M tự nhiên xuất hiện".

## Phase 1 — UX + fix bug sizing (commit 1)

### Backend
- [ ] TDD: `tests/InvestmentApp.Application.Tests/Portfolios/Queries/GetAllPortfoliosQueryHandlerTests.cs`
  - `Handle_PortfolioWithFlows_ReturnsCurrentCapitalIncludingNetFlows`
  - `Handle_PortfolioWithNoFlows_ReturnsCurrentCapitalEqualToInitial`
- [ ] Add `CurrentCapital`, `NetCashFlow` to `PortfolioSummaryDto`
- [ ] Inject `ICapitalFlowRepository` into `GetAllPortfoliosQueryHandler`
- [ ] Compute: `NetCashFlow = GetTotalFlowByPortfolioIdAsync`, `CurrentCapital = InitialCapital + NetCashFlow`

### Frontend
- [ ] `PortfolioSummary` interface + `currentCapital`, `netCashFlow`
- [ ] Dropdowns (5): change `p.initialCapital` → `p.currentCapital`
  - capital-flows:28, position-sizing:41, trade-wizard:139, trade-plan:419, trade-create:61
- [ ] List/detail/dashboard: primary "Vốn hiện tại" + secondary "Vốn ban đầu"
  - portfolios:107, portfolio-detail:65, dashboard:540
- [ ] **Fix bug sizing**: 4 trang dùng `portfolio.currentCapital` làm `accountBalance`
  - position-sizing:229, trade-wizard:648, trade-plan:2329, trade-create:482

## Phase 2 — Siết kỷ luật (commit 2)

### Backend
- [ ] Remove `InitialCapital` from `UpdatePortfolioCommand` + validator
- [ ] Remove `UpdateInitialCapital` call from `UpdatePortfolioCommandHandler`
- [ ] Fix `CashFlowAdjustedReturnService.NetCashFlow` → use `Σ SignedAmount`
- [ ] Update/add tests

### Frontend
- [ ] `UpdatePortfolioRequest` type: remove `initialCapital`
- [ ] `portfolio-edit.submit()` only sends `{ name }`

## Phase 3 — Refactor domain (commit 3)

### Backend
- [ ] TDD: `CreatePortfolioCommandHandler` auto-creates `Deposit` CapitalFlow with `InitialCapital` amount
- [ ] Inject `ICapitalFlowRepository` into handler
- [ ] Keep `Portfolio.InitialCapital` field (don't drop — existing calculations depend on it)
- [ ] Data migration: existing portfolios without initial Deposit flow are left as-is (documented as edge case)

## Risks

- **Breaking change**: `PortfolioSummaryDto` API response gains 2 fields. Additive → safe for existing FE.
- **Behavior change**: Risk sizing now uses currentCapital → result may differ vs. before when user had flows. This is a **fix**, not regression.
- **Phase 2 breaking**: FE update payload changes. Old clients sending `initialCapital` still work (FluentValidation ignores removed fields silently? verify). Backend will just ignore if field removed from command.
- **Phase 3**: If a portfolio is created via tests that don't mock `ICapitalFlowRepository`, tests break. Add mock.

## Docs to update

- `docs/business-domain.md` §3.1 — formula, add CurrentCapital concept
- `docs/features.md` — portfolio capital management
- `frontend/src/assets/CHANGELOG.md`

## Checkpoints

### Checkpoint — Phase 1 (done) — 2026-04-18

**Decisions:**
- Add `CurrentCapital` + `NetCashFlow` to both `PortfolioSummaryDto` (GetAllPortfolios) and `PortfolioDto` (GetPortfolio), plus to PnL summary (`/pnl/summary` anonymous object) for dashboard consumption
- Keep `InitialCapital` as immutable "snapshot lúc tạo"; `CurrentCapital` is the authoritative "vốn ròng hiện tại"
- Fix position sizing bug: 4 risk pages switched from `initialCapital` → `currentCapital`
- Dashboard `cashBalance` simplified: `currentCapital - totalInvested` (was `initialCapital + netCashFlow - totalInvested`)
- `PnLController` catch now wraps only PnL calc — flow fetch moved outside try

**Files changed:**
- Backend: `GetAllPortfoliosQuery.cs`, `GetPortfolioQuery.cs`, `PnLController.cs`
- Tests (new): `GetAllPortfoliosQueryHandlerTests.cs` (3), `GetPortfolioQueryHandlerTests.cs` (3)
- Frontend: `portfolio.service.ts`, `pnl.service.ts`, 5 dropdowns, portfolios list, portfolio-detail, portfolio-edit, dashboard
- Docs: `business-domain.md` §3.1, `CHANGELOG.md` v2.40.0

**Tests:** 72/72 Application tests pass (+3 new tests)

**Affected layers:** Application, Api, Frontend

**Next (Phase 2):**
- Remove `InitialCapital` from `UpdatePortfolioCommand` + validator + handler. Frontend `UpdatePortfolioRequest` loses field; `portfolio-edit` submit sends only `{ name }`.
- Fix `CashFlowAdjustedReturnService.NetCashFlow` → use `Σ SignedAmount` (include Dividend/Interest/Fee) — currently only `deposits - withdrawals`.
- Read: `src/InvestmentApp.Application/Portfolios/Commands/UpdatePortfolio/`, `src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs`, existing `tests/` for these paths.

### Checkpoint — Phase 2 (done) — 2026-04-18

**Decisions:**
- Remove `InitialCapital` from `UpdatePortfolioCommand` entirely (command, validator, handler). FE `UpdatePortfolioRequest` loses field; `portfolio-edit.onSubmit` sends only `{ name }`.
- Domain method `Portfolio.UpdateInitialCapital()` kept (no callers at Application layer; may still be useful for admin/migration tooling).
- TWR/MWR `NetCashFlow` fix: **skipped** — verified the formula `totalDeposits - totalWithdrawals` in `CashFlowAdjustedReturnService` is mathematically equivalent to `Σ SignedAmount` despite misleading variable names (`totalDeposits` actually sums Deposit+Dividend+Interest; `totalWithdrawals` sums Withdraw+Fee). No correctness issue.

**Files changed:**
- Backend: `UpdatePortfolioCommand.cs`, `UpdatePortfolioCommandValidator.cs`
- Tests (new): `UpdatePortfolioCommandHandlerTests.cs` (3 tests)
- Frontend: `portfolio.service.ts`, `portfolio-edit.component.ts`
- Docs: `CHANGELOG.md` v2.41.0

**Tests:** 75/75 Application tests pass (+3 new)

**Affected layers:** Application, Frontend

**Next (Phase 3):**
- Auto-create `Deposit` `CapitalFlow` record inside `CreatePortfolioCommandHandler` when portfolio is created, matching `InitialCapital` amount. TDD: update existing `CreatePortfolioCommandHandlerTests` to verify flow creation (add `ICapitalFlowRepository` mock).
- Keep `Portfolio.InitialCapital` stored field unchanged (immutable snapshot) — don't drop; blast radius too wide.
- Read: `src/InvestmentApp.Application/Portfolios/Commands/CreatePortfolio/`, `tests/InvestmentApp.Application.Tests/Portfolios/Commands/CreatePortfolioCommandHandlerTests.cs`.

### Known follow-ups (from Phase 1 review)

- N+1 query in `GetAllPortfoliosQueryHandler` + `PnLController`: each portfolio triggers separate `GetTotalFlowByPortfolioIdAsync`. Acceptable at current scale; consider server-side `$sum` aggregation if users grow portfolios > 20.
