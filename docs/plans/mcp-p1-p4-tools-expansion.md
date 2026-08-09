# MCP P1–P4 — Tools Expansion (4 slices, multi-phase ship)

**Ngày:** 2026-07-26
**Nguồn:** [`docs/superpowers/plans/2026-07-25-mcp-tools-expansion-roadmap.md`](../superpowers/plans/2026-07-25-mcp-tools-expansion-roadmap.md) — slices P1 → P4 (P0 done ở PR #131, digest #132).
**ADR:** not required — lặp pattern PR #129/#131, không contract/schema mới. Riêng P3/P4 (WRITE) vẫn dùng `Destructive = true` như 17 write tool hiện có — không quyết định mới.
**Protocol:** mỗi P = 1 slice = 1 branch + 1 PR. Checkpoint append vào cuối file này sau mỗi slice. Signatures verify tại đầu mỗi slice (lesson: verify-plan-APIs-before-scaffolding).

## Slice P1 — Analytics (8 READ) — class `AnalyticsTools`

Branch: `feature/mcp-p1-analytics-tools`. Signatures verified 2026-07-26:

| Tool | Query (namespace) | Params ngoài UserId | Return |
|---|---|---|---|
| `get_performance` | `Analytics.Queries.GetPerformance` | `PortfolioId` **req** | `PerformanceSummary` (`Application.Interfaces`) |
| `get_equity_curve` | `Analytics.Queries.GetEquityCurve` | `PortfolioId` **req** | `EquityCurveData` (`Application.Interfaces`) |
| `get_monthly_returns` | `Analytics.Queries.GetMonthlyReturns` | `PortfolioId` **req** | `MonthlyReturnsData` (`Application.Interfaces`) |
| `get_savings_comparison` | `Analytics.Queries.GetSavingsComparison` | `PortfolioId` **req**, `AnnualRate` decimal?, `AsOf` DateTime? | `SavingsComparisonDto` (cùng ns) |
| `get_campaign_analytics` | `TradePlans.Queries.GetCampaignAnalytics` | `TimeHorizon` string? (ShortTerm/MediumTerm/LongTerm) | `CampaignAnalyticsDto` (cùng ns) |
| `get_net_worth_summary` | `PersonalFinance.Queries.GetNetWorthSummary` | — | `NetWorthSummaryDto` (`PersonalFinance.Dtos`) |
| `get_flow_history` | `CapitalFlows.Queries.GetFlowHistory` | `PortfolioId` **req**, `From`/`To` DateTime? | `CapitalFlowHistoryDto` (cùng ns) |
| `get_adjusted_return` | `CapitalFlows.Queries.GetAdjustedReturn` | `PortfolioId` **req** | `AdjustedReturnDto` (cùng ns) |

- **Deviation vs roadmap:** 6/8 tool per-portfolio (`PortfolioId` required) — ownership guard đã verify có trong cả 6 handler (`portfolio.UserId != request.UserId` → throw).
- Tests: `AnalyticsToolsTests` (8+ tests Capture UserId + params); `McpToolDiscoveryTests` 38 → 46, thêm schema assertion đại diện.

## Slice P2 — Market Research (8 READ) — class `MarketTools`

Branch: `feature/mcp-p2-market-tools`. Tools: `get_stock_detail`, `get_stock_price`, `get_stock_price_history`, `get_technical_analysis`, `search_stocks`, `get_market_overview`, `get_top_fluctuation`, `get_batch_prices`.
Lưu ý khi verify signature: market queries có thể **không có UserId** (dữ liệu công khai) — nếu vậy tool không cần `GetUserId()`; xác nhận từng query. Params dự kiến: `Symbol` (dùng chuẩn hóa ToUpper ở handler), date-range, `limit`.

## Slice P3 — Portfolio & Trade Management (2 READ + 6 WRITE) — mở rộng `PortfolioTools`/`TradeTools`

Branch: `feature/mcp-p3-portfolio-trade-mgmt`. READ: `get_portfolio`, `get_trades_by_portfolio`. WRITE (Destructive): `create_portfolio`, `update_portfolio`, `delete_portfolio`, `delete_trade`, `link_trade_to_plan`, `bulk_create_trades`.
Verify: ownership + soft-delete semantics của delete handlers; `bulk_create_trades` shape (list DTO).

## Slice P4 — Plan Execution & Discipline Actions (8 WRITE) — class `PlanActionTools`

Branch: `feature/mcp-p4-plan-actions`. Tools: `resolve_decision` (ExecuteSell/HoldWithJournal), `review_trade_plan`, `abort_trade_plan`, `execute_lot`, `update_stop_loss`, `trigger_exit_target`, `set_stop_loss_target`, `set_risk_profile`.
Stakes cao nhất — verify: state-machine guard trong handler (không nhảy cóc status), ownership, command required fields. Tất cả `Destructive = true`.

## Follow-up (ngoài 4 slice)

- **`CorporateActionTools` (chưa làm):** sự kiện quyền hiện chỉ nhập được qua UI. Cần tool MCP cho `list` / `create` (3 loại: cổ tức tiền mặt, cổ tức cổ phiếu, chia tách) / `settle` / `delete` — bọc các handler sẵn có trong `Application/CorporateActions/`. `create` và `delete` là `Destructive = true`; nhớ idiom optional-param bên dưới. Xem [ADR-0010](../adr/0010-corporate-actions-position-projection.md).
- **Optional-param idiom backfill:** P1 thiết lập idiom mới — optional tool param phải có C# default (`= null`, đặt sau `ct`) để rơi khỏi `required` trong schema; guard bằng test `Optional_Params_Are_Not_Required_In_Schema`. Các tool cũ (`get_discipline_score.days`, `list_positions.portfolioId`, watchlist/journal optionals…) vẫn nằm trong `required` — backfill 1 PR riêng sau P4.

## Checkpoints

### Checkpoint — Slice P1 (done 2026-07-26)
- **Decisions**: (1) `AnnualRate` giữ semantics phân số thập phân của handler (0.05 = 5%) — chỉ reword description, không convert ở wrapper; (2) idiom mới optional params (xem Follow-up); (3) `timeHorizon` sai giá trị → handler trả tất cả (note trong description, không thêm validation ở wrapper).
- **Files changed**: `src/InvestmentApp.Api/Mcp/AnalyticsTools.cs` (new), `tests/.../Mcp/AnalyticsToolsTests.cs` (new, 9 tests), `McpToolDiscoveryTests.cs` (46 tool + required-array test), docs (architecture/business-domain/CHANGELOG v2.67.0).
- **Tests**: Api 179 pass, suite 1.463.
- **Review**: 1 sub-agent (fable) — 3 findings: unit-mismatch `annualRate` (90, fixed), optional-in-required (80, fixed bằng idiom mới), timeHorizon silent fallback (60, note description).
- **Affected layers**: Api only.
- **Next**: Slice P2 `MarketTools` — signatures ĐÃ verify (xem §P2): các query **không có UserId** → tool không inject `IHttpContextAccessor`; `get_batch_prices` nhận `List<string> symbols`; `get_stock_price_history` `from`/`to` **required** (DateTime); `get_top_fluctuation` `floor` optional default "10". Áp dụng idiom optional-param mới. Cần xác định ns của `TechnicalAnalysisResult` + các DTO (grep `^namespace` file query).
