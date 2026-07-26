# MCP Tools Expansion — Prioritized Roadmap

**Ngày:** 2026-07-25
**Trạng thái:** **P0–P4 done** (2026-07-25 → 26) — P0 ở PR #131, P1–P4 ở PR #133/#134/#135/#136 (stacked), chi tiết + checkpoint ở [`docs/plans/mcp-p1-p4-tools-expansion.md`](../../plans/mcp-p1-p4-tools-expansion.md). Tổng 70 tool. **P5 chờ** (làm khi cần) + 1 PR backfill optional-param idiom.
**Tiền đề:** PR #129 đã ship 29 tool (parity với `AiAgent*` REST surface). Đây là bước "thêm API mới" — expose các MediatR handler **chưa** có ở agent surface, **ưu tiên theo giá trị**.
**Liên quan:** [`2026-07-24-mcp-server-implementation.md`](2026-07-24-mcp-server-implementation.md) (đã done), ADR-0003/0004/0005.

## 1. Bối cảnh & nguyên tắc ưu tiên

Inventory: **137 MediatR handler**, 28 đã expose, **~109 chưa**. Không expose hết — chọn theo **giá trị cho AI investor-assistant** (host MCP như Claude Desktop giúp user quyết định **rủi ro & kế hoạch** — đúng north star của app: risk/planning, solo user).

Thang điểm giá trị (cao → thấp):
1. **Decision-relevance** — có trực tiếp trả lời "hôm nay nên làm gì / đang rủi ro cỡ nào" không?
2. **READ trước WRITE** — read an toàn, tần suất cao, hợp phân tích; write cần confirm, stakes cao hơn → làm sau.
3. **Uniqueness** — thông tin agent chưa suy ra được từ 29 tool hiện có.
4. **Risk/planning fit** — khớp trọng tâm app.

Pattern thực hiện **giống hệt** 29 tool đã làm (thin wrapper → MediatR dispatch, `UserId` từ `sub`, `McpUserContext.GetUserId`, static `[McpServerToolType]` class theo nhóm, test helper `McpTestContext.Capture`, schema-leak regression test). ⇒ Mỗi slice là cơ học, rủi ro thấp.

## 2. ⚠️ Loại trừ vĩnh viễn (không bao giờ expose cho agent ApiKey-scoped)

| Handler | Lý do |
|---|---|
| `SearchUsersQuery`, `GetUsersOverviewQuery` | **Admin, không user-scoped** — rò dữ liệu mọi user |
| `StartImpersonationCommand`, `StopImpersonationCommand` | **Privilege escalation** — agent giả danh user khác |
| `CreateApiKeyCommand`, `RevokeApiKeyCommand`, `GetApiKeysQuery` | Agent tự quản khóa auth = **escalation**; mint key mới ngoài tầm kiểm soát |
| `CrawlVietstockEventsCommand` | Side-effect scrape nặng, không phù hợp tool gọi tùy ý |

`GetAiSettingsQuery`/`SaveAiSettingsCommand`: low-value, defer vô thời hạn (không nằm roadmap).

## 3. Thứ tự ưu tiên (P0 → P5)

### 🥇 P0 — Decision & Risk Intelligence (READ) — **LÀM TRƯỚC**

Lý do làm đầu: **cú nhảy năng lực lớn nhất** — cho agent "situational awareness" để thực sự tư vấn rủi ro/kỷ luật/quyết định. Toàn bộ READ ⇒ an toàn, không friction confirm. Đây là phần đắt giá nhất theo north star.

| Tool | MediatR → return | Ghi chú |
|---|---|---|
| `get_decision_queue` | `GetDecisionQueueQuery` → `DecisionQueueDto` | ⭐ Feed gộp alert (StopLoss + Scenario + Thesis-review) đã dedupe + sort severity — "hôm nay cần quyết gì" |
| `get_discipline_score` | `GetDisciplineScoreQuery` → `DisciplineScoreDto` | Điểm kỷ luật 0–100 + 3 thành phần; query `days` (7/30/90/365) |
| `get_discipline_streak` | `GetDisciplineStreakQuery` → `DisciplineStreakDto` | Chuỗi ngày không vi phạm SL |
| `get_portfolio_risk` | `GetPortfolioRiskQuery` → `PortfolioRiskSummary` | Volatility/VaR/Sharpe/max-drawdown (UserId + PortfolioId) |
| `get_pending_thesis_reviews` | `GetPendingThesisReviewsQuery` → `List<PendingThesisReviewDto>` | Plan quá hạn review thesis + days-overdue |
| `get_stop_loss_targets` | `GetStopLossTargetsQuery` → `StopLossTargetsDto` | SL target đang hoạt động toàn danh mục |
| `get_trailing_stop_alerts` | `GetTrailingStopAlertsQuery` → `TrailingStopAlertsResult` | Vị thế sát ngưỡng trailing stop |
| `get_scenario_advisories` | `GetScenarioAdvisoriesQuery` → `List<ScenarioAdvisory>` | Cảnh báo kịch bản đang vi phạm |

→ **8 tool, 1 class `RiskTools` + `DecisionTools`** (hoặc gộp `SituationTools`). Ước lượng ~1 slice.

### Checkpoint — P0 (done 2026-07-25)
- **Decisions**: 2 class `DecisionTools` (queue/score/streak/thesis-reviews) + `RiskTools` (risk/SL-targets/trailing/advisories). Deviation vs bảng trên: `get_stop_loss_targets` + `get_trailing_stop_alerts` là **per-portfolio** (query yêu cầu `PortfolioId`, ownership check ở handler) — tool nhận `portfolioId` required giống `get_portfolio_risk`. `get_discipline_score` nhận `days` nullable → default 90.
- **Files changed**: `src/InvestmentApp.Api/Mcp/{DecisionTools,RiskTools}.cs` (new), `tests/.../Mcp/{DecisionToolsTests,RiskToolsTests}.cs` (new), `McpToolDiscoveryTests.cs` (37 tool + 3 schema-leak assertions cho tool có `portfolioId`), plan chi tiết `docs/plans/mcp-p0-risk-decision-tools.md`.
- **Tests**: +9 unit, suite 1.451 pass.
- **Affected layers**: Api only (+ Api.Tests).
- **Next**: P1 Performance & Wealth Analytics (8 read tool, §P1) — cùng pattern, thêm class `AnalyticsTools`. Verify signature từng query trước khi scaffold (một số có `PortfolioId`/date-range/`days`). Đọc checkpoint này + `PortfolioTools.cs` là đủ context.

### 🥈 P1 — Performance & Wealth Analytics (READ)

"Tôi đang làm ăn thế nào" — cho agent đánh giá hiệu suất + của cải.

| Tool | MediatR → return |
|---|---|
| `get_performance` | `GetPerformanceQuery` → `PerformanceSummary` (total/MTD/YTD) |
| `get_equity_curve` | `GetEquityCurveQuery` → `EquityCurveData` |
| `get_monthly_returns` | `GetMonthlyReturnsQuery` → `MonthlyReturnsData` |
| `get_savings_comparison` | `GetSavingsComparisonQuery` → `SavingsComparisonDto` (alpha vs gửi tiết kiệm) |
| `get_campaign_analytics` | `GetCampaignAnalyticsQuery` → `CampaignAnalyticsDto` (win rate, PnL distribution) |
| `get_net_worth_summary` | `GetNetWorthSummaryQuery` → `NetWorthSummaryDto` |
| `get_flow_history` | `GetFlowHistoryQuery` → `CapitalFlowHistoryDto` |
| `get_adjusted_return` | `GetAdjustedReturnQuery` → `AdjustedReturnDto` (TWR) |

### 🥉 P2 — Market Research (READ)

"Nghiên cứu giúp tôi mã này" — cho agent tra cứu thị trường trước khi tư vấn.

`get_stock_detail`, `get_stock_price`, `get_stock_price_history`, `get_technical_analysis` (MA/RSI/MACD/Bollinger), `search_stocks`, `get_market_overview`, `get_top_fluctuation`, `get_batch_prices`.

### P3 — Portfolio & Trade Management (READ + WRITE)

- READ: `get_portfolio` (detail), `get_trades_by_portfolio`.
- WRITE (Destructive): `create_portfolio`, `update_portfolio`, `delete_portfolio`, `delete_trade`, `link_trade_to_plan`, `bulk_create_trades`.

### P4 — Plan Execution & Discipline Actions (WRITE, Destructive, stakes cao)

Agent hành động theo kế hoạch — cần host confirm. Ownership + guard đã có ở handler.

`resolve_decision` (`ResolveDecisionCommand` — ExecuteSell / HoldWithJournal), `review_trade_plan`, `abort_trade_plan`, `execute_lot`, `update_stop_loss`, `trigger_exit_target`, `set_stop_loss_target`, `set_risk_profile`.

### P5 — Breadth (làm khi cần)

- **Risk mở rộng (READ):** `get_correlation`, `get_drawdown`, `get_portfolio_optimization`, `get_stress_test`, `get_risk_budget`, `get_risk_profile`.
- **Snapshots:** `take_snapshot`, `get_snapshot_at_date`, `get_snapshot_range`, `compare_snapshots`.
- **Strategies:** get/list + performance + create/update/delete + link-trade.
- **Alerts:** rules CRUD + history + mark-read.
- **Daily routines:** today/templates/history + complete-item + template CRUD.
- **Backtest:** `get_backtests`, `get_backtest`, `run_backtest`.
- **Personal finance (WRITE):** upsert profile/account/debt, remove account/debt.
- **Scenario:** suggestions, templates (get/save), history.
- **Misc:** `get_market_events` + create, `get_exchange_rates`, `convert_currency`, `get_gold_prices`.

## 4. Khuyến nghị

**Làm P0 trước** (8 read tool). Lý do: (1) đúng north star risk/planning; (2) toàn READ → an toàn, không cần confirm-gate; (3) unlock agent từ "ghi chép hộ" (29 tool hiện tại thiên về CRUD) thành "**cố vấn được**" — đọc được decision queue + risk + discipline là điều kiện cần để agent đưa lời khuyên có căn cứ. Sau đó **P1** (analytics) để agent đánh giá hiệu suất.

P2 (market research) làm khi muốn agent tự tra cứu. P3–P4 (quản lý + hành động ghi) làm sau khi tin tưởng phần đọc. P5 mở rộng theo nhu cầu.

## 5. Thực hiện (khi chọn slice)

Lặp lại **đúng pattern PR #129** — không có gì mới về kiến trúc:
1. Thêm class `[McpServerToolType]` theo nhóm (vd `RiskTools`, `AnalyticsTools`, `MarketTools`) trong `src/InvestmentApp.Api/Mcp/`. Đăng ký tự động qua `WithToolsFromAssembly()` (không cần đụng Program.cs).
2. Mỗi tool: inject `IMediator` + `IHttpContextAccessor` (+ service khác nếu handler cần), set `UserId = http.GetUserId()`, dispatch query/command sẵn có. READ → `ReadOnly = true`; WRITE → `Destructive = true`.
3. **Verify signature ở scaffold** (bài học verify-plan-APIs): return type + param (nhiều query có `PortfolioId`/`Symbol`/`days`/date-range/`profileId` — lấy đúng từ class query). Một số return type phức tạp (`PortfolioRiskSummary`, `DecisionQueueDto`) — không cần biết nội tại, chỉ cần đúng type ở method signature.
4. Test: mỗi tool 1 unit test `Capture<TResponse, TQuery>` assert `UserId` (+ param) set đúng; cập nhật `McpToolDiscoveryTests` (tên + annotation + **schema không lộ DI param**).
5. `dotnet test` xanh → docs (architecture.md bảng `/mcp`, business-domain.md, CHANGELOG) → PR → code review.

## 6. Ngoài phạm vi roadmap này

- OAuth 2.1 (chỉ khi host bắt buộc) — vẫn như plan trước.
- MCP **prompts/resources** (khác tools — vd resource "portfolio snapshot", prompt "review vị thế mở") — spec riêng nếu muốn.
- Handler ở mục §2 (admin/apikey/crawl) — không expose.
