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

Branch: `feature/mcp-p3-portfolio-trade-mgmt`. Signatures verified 2026-07-26:

| Tool | Command/Query | Params ngoài UserId | Return |
|---|---|---|---|
| `get_portfolio` (R) | `Portfolios.Queries.GetPortfolio` | `Id` **req** | `PortfolioDto?` |
| `get_trades_by_portfolio` (R) | `Trades.Queries.GetTradesByPortfolio` | `PortfolioId` **req**, `Symbol?`, `TradeType?`, `Page`=1, `PageSize`=20 | `TradeListDto` |
| `create_portfolio` (W) | `Portfolios.Commands.CreatePortfolio` | `Name`, `InitialCapital` | `string` (id) |
| `update_portfolio` (W) | `Portfolios.Commands.UpdatePortfolio` | `Id`, `Name` | `bool` |
| `delete_portfolio` (W) | `Portfolios.Commands.DeletePortfolio` | `Id` | `bool` |
| `delete_trade` (W) | `Trades.Commands.DeleteTrade` | `TradeId` | `bool` |
| `link_trade_to_plan` (W) | `Trades.Commands.LinkTradeToPlan` | `TradeId`, `PlanId` | `bool` |
| `bulk_create_trades` (W) | `Trades.Commands.BulkCreateTrades` | `PortfolioId`, `Trades: List<BulkTradeItem>` (Symbol/TradeType/Quantity/Price/Fee/Tax/TradeDate?) | `BulkCreateTradesResult` |

Lưu ý: `CreatePortfolioCommand.UserId` và `UpdatePortfolioCommand.Id`/`UserId` là `string?` (nullable) — set như bình thường. `BulkCreateTrades` ownership qua `UnauthorizedAccessException`; partial-success (`SuccessCount`/`FailedCount`/`Errors`) → mô tả rõ trong `[Description]` để agent không hiểu nhầm là all-or-nothing. Áp dụng idiom optional-param (default) cho `symbol`/`tradeType`/`page`/`pageSize`.

## Slice P4 — Plan Execution & Discipline Actions (8 WRITE) — class `PlanActionTools`

Branch: `feature/mcp-p4-plan-actions`. Tất cả `Destructive = true`. Signatures verified 2026-07-26:

| Tool | Command | Params ngoài UserId | Return |
|---|---|---|---|
| `resolve_decision` | `Decisions.Commands.ResolveDecision` | `DecisionId`, `Action` (enum `DecisionAction`: ExecuteSell/HoldWithJournal), `TradePlanId?` (req cho ExecuteSell), `Symbol?`, `Note?` (req ≥20 ký tự cho HoldWithJournal — FluentValidation) | `ResolveDecisionResult` |
| `review_trade_plan` | `TradePlans.Commands.ReviewTradePlan` | `PlanId`, `LessonsLearned?` | `CampaignReviewDto` |
| `abort_trade_plan` | `TradePlans.Commands.AbortTradePlan` | `PlanId`, `Trigger` (EarningsMiss/TrendBreak/NewsShock/ThesisTimeout/Manual), `Detail` (≥20 ký tự) | `AbortTradePlanResult` |
| `execute_lot` | `TradePlans.Commands.ExecuteLot` | `PlanId`, `LotNumber` (int), `TradeId`, `ActualPrice` | `Unit` |
| `update_stop_loss` | `TradePlans.Commands.UpdateStopLoss` | `PlanId`, `NewStopLoss`, `Reason?` | `Unit` |
| `trigger_exit_target` | `TradePlans.Commands.TriggerExitTarget` | `PlanId`, `Level` (int), `TradeId` | `Unit` |
| `set_stop_loss_target` | `Risk.Commands.SetStopLossTarget` | `TradeId`, `PortfolioId`, `Symbol`, `EntryPrice`, `StopLossPrice`, `TargetPrice`, `TrailingStopPercent?` | `string` (id) |
| `set_risk_profile` | `Risk.Commands.SetRiskProfile` | `PortfolioId` + 7 field nullable (MaxPositionSizePercent, MaxSectorExposurePercent, MaxDrawdownAlertPercent, DefaultRiskRewardRatio, MaxPortfolioRiskPercent, MaxDailyTrades int?, DailyLossLimitPercent) | `string` (id) |

Lưu ý: `Unit` (MediatR) làm return type của MCP tool — cân nhắc trả `bool`/string message thay vì `Unit` cho host dễ đọc; quyết định lúc scaffold. `LotNumber`/`Level` là `[JsonIgnore]` route param ở REST → thành param tường minh ở tool. `DecisionAction` là enum → xác nhận SDK sinh schema enum (string) chứ không phải int.

## Follow-up (ngoài 4 slice)

- **Optional-param idiom backfill:** P1 thiết lập idiom mới — optional tool param phải có C# default (`= null`, đặt sau `ct`) để rơi khỏi `required` trong schema; guard bằng test `Optional_Params_Are_Not_Required_In_Schema`. Các tool cũ (`get_discipline_score.days`, `list_positions.portfolioId`, watchlist/journal optionals…) vẫn nằm trong `required` — backfill 1 PR riêng sau P4.

## Checkpoints

### Checkpoint — Slice P1 (done 2026-07-26)
- **Decisions**: (1) `AnnualRate` giữ semantics phân số thập phân của handler (0.05 = 5%) — chỉ reword description, không convert ở wrapper; (2) idiom mới optional params (xem Follow-up); (3) `timeHorizon` sai giá trị → handler trả tất cả (note trong description, không thêm validation ở wrapper).
- **Files changed**: `src/InvestmentApp.Api/Mcp/AnalyticsTools.cs` (new), `tests/.../Mcp/AnalyticsToolsTests.cs` (new, 9 tests), `McpToolDiscoveryTests.cs` (46 tool + required-array test), docs (architecture/business-domain/CHANGELOG v2.67.0).
- **Tests**: Api 179 pass, suite 1.463.
- **Review**: 1 sub-agent (fable) — 3 findings: unit-mismatch `annualRate` (90, fixed), optional-in-required (80, fixed bằng idiom mới), timeHorizon silent fallback (60, note description).
- **Affected layers**: Api only.
- **Next (đã làm)**: Slice P2 `MarketTools` — signatures ĐÃ verify (xem §P2): các query **không có UserId** → tool không inject `IHttpContextAccessor`; `get_batch_prices` nhận `List<string> symbols`; `get_stock_price_history` `from`/`to` **required** (DateTime); `get_top_fluctuation` `floor` optional default "10". Áp dụng idiom optional-param mới. Cần xác định ns của `TechnicalAnalysisResult` + các DTO (grep `^namespace` file query).

### Checkpoint — Slice P2 (done 2026-07-26)
- **Decisions**: (1) market queries **không mang UserId** → tool chỉ inject `IMediator` + `ct`, không `IHttpContextAccessor` (verify: 8 handler chỉ chạm provider/repo global, không rò dữ liệu user); (2) symbol chuẩn hóa `ToUpperInvariant().Trim()` tại wrapper — lệch REST (pass raw) nhưng khớp quy ước symbol dự án + `GetTechnicalAnalysisQueryHandler` vốn đã ToUpper; (3) `get_stock_price_history` mặc định 3 tháng gần nhất, mirror `MarketDataController.GetPriceHistory`.
- **Files changed**: `src/InvestmentApp.Api/Mcp/MarketTools.cs` (new), `tests/.../Mcp/MarketToolsTests.cs` (new, 12 tests), `McpToolDiscoveryTests.cs` (54 tool + 3 schema assertion P2), docs (architecture/business-domain/CHANGELOG v2.68.0).
- **Tests**: Api 191 pass.
- **Review**: 1 sub-agent (fable) — 3 findings P2, fix cả 3: lọc entry rỗng/null trong `get_batch_prices` (tránh NRE), guard keyword rỗng ở `search_stocks` (mirror 400 của REST), thêm schema-regression assertion cho tool P2.
- **Affected layers**: Api only.
- **Next**: Slice P3 `PortfolioTools`/`TradeTools` mở rộng — signatures ĐÃ verify (xem §P3). WRITE tool đầu tiên của đợt này → `Destructive = true`; mô tả rõ partial-success của `bulk_create_trades`.

### Checkpoint — Slice P3 (done 2026-07-26)
- **Decisions**: (1) mở rộng `PortfolioTools`/`TradeTools` sẵn có thay vì tạo class mới (tool cùng nhóm nghiệp vụ); (2) `bulk_create_trades` chuẩn hóa symbol + reject row thiếu symbol tại wrapper (handler chỉ NRE per-row, UX agent kém); (3) mô tả tool nói đúng bản chất xóa: `delete_trade` = **hard delete**, `delete_portfolio` = **soft delete** (review bắt được mô tả ban đầu bị ngược).
- **Files changed**: `Mcp/PortfolioTools.cs` + `Mcp/TradeTools.cs` (mở rộng), `tests/.../Mcp/PortfolioTradeMgmtToolsTests.cs` (new, 11 tests), `McpToolDiscoveryTests.cs` (62 tool), **3 handler test mới** ở Application.Tests (DeleteTrade/LinkTradeToPlan/DeletePortfolio — trước đó không có regression guard cho nhánh từ chối cross-user), docs + CHANGELOG v2.69.0.
- **Tests**: Api 202, Application 240, suite 1.496 pass.
- **Review**: 1 sub-agent (fable) — ownership PASS cả 6 write (đã trace handler; `link_trade_to_plan` check cả trade→portfolio VÀ plan.UserId). 5 findings, fix 3: thiếu handler test (P1/85), mô tả xóa ngược (P2/80), symbol null trong bulk (P2/70).
- **Phát hiện khi viết test**: `link_trade_to_plan` vào plan chưa InProgress → handler gọi `plan.Execute()` → **throw** `InvalidOperationException` thay vì trả false. Là behavior sẵn có, nay phơi ra cho agent. Đã ghi test document + cảnh báo trong `[Description]`; **không** sửa handler (ngoài scope slice).
- **Affected layers**: Api + Application.Tests.
- **Follow-up chưa fix (chờ quyết định)**:
  - Sau `delete_portfolio` (soft), các lệnh con vẫn tồn tại nhưng vĩnh viễn không thao tác được (`GetByIdAsync` lọc `IsDeleted`) → `delete_trade`/`link` trả `false` im lặng. Cần quyết định cascade/cleanup.
  - `BulkTradeItem.Fee`/`Tax` là `decimal` non-nullable → host bỏ trống = ghi 0, lệch P/L. Cân nhắc đổi `decimal?` + đưa row thiếu vào `Errors`.
- **Next**: Slice P4 `PlanActionTools` — signatures ĐÃ verify (§P4). Nhớ: quyết định return type thay `Unit`; `DecisionAction` enum → check schema sinh string; state-machine guard tương tự bug link ở trên (test cả nhánh sai trạng thái).
