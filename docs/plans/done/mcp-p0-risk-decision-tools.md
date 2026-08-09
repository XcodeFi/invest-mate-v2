# MCP P0 — Decision & Risk Intelligence Tools (8 READ tools)

**Ngày:** 2026-07-25
**Nguồn:** [`docs/superpowers/plans/2026-07-25-mcp-tools-expansion-roadmap.md`](../../superpowers/plans/2026-07-25-mcp-tools-expansion-roadmap.md) — slice P0.
**ADR:** not required — lặp lại đúng pattern PR #129 (ADR-0003/0004/0005), không có contract/schema/trade-off mới.

## What

Expose 8 MediatR query sẵn có thành MCP READ tools (`ReadOnly = true`), cho agent "situational awareness" về rủi ro/kỷ luật/quyết định.

## Signatures đã verify (2026-07-25)

| Tool | Query (namespace) | Params ngoài UserId | Return |
|---|---|---|---|
| `get_decision_queue` | `Decisions.Queries.GetDecisionQueue.GetDecisionQueueQuery` | — | `DecisionQueueDto` (`Decisions.DTOs`) |
| `get_discipline_score` | `Discipline.Queries.GetDisciplineScoreQuery` | `Days` int (default 90) | `DisciplineScoreDto` |
| `get_discipline_streak` | `Discipline.Queries.GetDisciplineStreakQuery` | — | `DisciplineStreakDto` |
| `get_pending_thesis_reviews` | `TradePlans.Queries.GetPendingThesisReviews.GetPendingThesisReviewsQuery` | — | `List<PendingThesisReviewDto>` |
| `get_portfolio_risk` | `Risk.Queries.GetPortfolioRisk.GetPortfolioRiskQuery` | `PortfolioId` **required** | `PortfolioRiskSummary` (`Application.Interfaces`) |
| `get_stop_loss_targets` | `Risk.Queries.GetStopLossTargets.GetStopLossTargetsQuery` | `PortfolioId` **required** | `StopLossTargetsDto` |
| `get_trailing_stop_alerts` | `Risk.Queries.GetTrailingStopAlerts.GetTrailingStopAlertsQuery` | `PortfolioId` **required** | `TrailingStopAlertsResult` |
| `get_scenario_advisories` | `TradePlans.Queries.GetScenarioAdvisories.GetScenarioAdvisoriesQuery` | — | `List<ScenarioAdvisory>` (`Application.Common.Interfaces`) |

**Deviation vs roadmap:** roadmap ghi `get_stop_loss_targets` là "toàn danh mục" — thực tế query yêu cầu `PortfolioId` (per-portfolio, ownership check trong handler). Tool nhận `portfolioId` required, giống `get_portfolio_risk`. Tương tự `get_trailing_stop_alerts`.

## Where

- `src/InvestmentApp.Api/Mcp/DecisionTools.cs` — new: `get_decision_queue`, `get_discipline_score`, `get_discipline_streak`, `get_pending_thesis_reviews`
- `src/InvestmentApp.Api/Mcp/RiskTools.cs` — new: `get_portfolio_risk`, `get_stop_loss_targets`, `get_trailing_stop_alerts`, `get_scenario_advisories`
- Đăng ký tự động qua `WithToolsFromAssembly()` — không đụng Program.cs.

## Tests

- `tests/InvestmentApp.Api.Tests/Mcp/DecisionToolsTests.cs` — 4 tests `Capture` assert UserId (+ Days)
- `tests/InvestmentApp.Api.Tests/Mcp/RiskToolsTests.cs` — 4 tests assert UserId + PortfolioId
- `McpToolDiscoveryTests` — thêm 8 tên vào `ReadTools`, tổng 29 → 37, giữ schema-leak assertions

## Risks

- Thấp — thin wrapper, ownership check đã nằm trong handler (`GetPortfolioRiskQuery` throw khi portfolio không thuộc user).
- Schema-leak: tool chỉ inject `IMediator` + `IHttpContextAccessor` (đều đã DI-registered) → discovery test bắt regression.
