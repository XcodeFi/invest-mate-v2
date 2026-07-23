# Plan p3 — Agent tự đủ thông tin khi mở/đóng vị thế (portfolio + fee/tax)

**Spec:** `docs/superpowers/specs/2026-07-23-agent-position-open-portfolio-fee-design.md`
**ADR:** `docs/adr/0005-agent-surface-auto-resolution.md`
**Scope:** `InvestmentApp.Api` only (controllers + agent doc + tests). Reuse `GetAllPortfoliosQuery` + `FeeCalculationService` — no new Domain/Application logic.

## Mục tiêu

NPU/Claude ghi trade mà không phải hỏi lại người dùng `portfolioId` + `fee`/`tax`.

1. `GET /ai/agent/portfolios` — mirror `GetAllPortfoliosQuery`.
2. `POST /ai/agent/fees/calculate` — mirror `FeesController.CalculateFees` (inject `IFeeCalculationService`).
3. Enhance `AiAgentController.CreateTrade`: `portfolioId`/`fee`/`tax` optional → auto-pick single portfolio + auto-compute fee/tax; explicit values (incl. 0) respected.

## Checkpoint — Phase (done)

- **Decisions**: auto-resolve nằm ở agent controller (không đụng `CreateTradeCommand` chung) — ADR-0005. Auto-pick chỉ khi user có đúng 1 portfolio (0/>1 → 400). Fee/tax dùng `FeeCalculationService.CalculateSecuritiesTax` (config-driven) — khớp FE, drift-resistant.
- **Files changed**:
  - Mới: `Controllers/AiAgentPortfoliosController.cs`, `Controllers/AiAgentFeesController.cs`, `Controllers/AgentTradeFeeCalculator.cs`, `Controllers/AgentCreateTradeRequest.cs`, `docs/adr/0005-agent-surface-auto-resolution.md`.
  - Sửa: `Controllers/AiAgentController.cs` (CreateTrade + inject IFeeCalculationService), `Docs/AI-Agent-TradePlan-API.md`, `docs/business-domain.md`, `docs/architecture.md`, `frontend/src/assets/CHANGELOG.md` (v2.63.0).
  - Tests: `AiAgentControllerTests.cs` (+create-trade resolve/compute + doc guard), `AiAgentExposeControllersTests.cs` (+portfolios +fees).
- **Tests**: +10 xUnit; **121 Api pass**, 0 fail, no regression.
- **Verify**: live smoke (localhost→prod DB) — fees/calculate SELL tax 0.1% ✓, portfolios count=1 ✓, create-trade no-portfolioId/fee/tax → 201 auto-resolve ✓, trade deleted + key revoked ✓.
- **Affected layers**: Api.
- **Next**: none — feature complete. Post-merge: move this file to `docs/plans/done/`, fill ADR-0005 PR#. Optional follow-ups (separate tickets): re-enable `[Authorize]` on `FeesController`; fix FE fee/tax double-subtract in `GetAllPortfolios` sell math.
