# Handoff — 2026-07-29 — Decision Queue: cơ hội mua & thiếu stop-loss

**Nhánh:** `feature/decision-queue-buy-opportunity` (off `origin/master` @ `bc4047b`, không upstream)
**Trạng thái:** Task 1/5 xong, code **chưa commit**. Task 2–5 chưa bắt đầu.

## Đã làm

- **Spec:** [`docs/superpowers/specs/2026-07-29-decision-queue-buy-opportunity-design.md`](../superpowers/specs/2026-07-29-decision-queue-buy-opportunity-design.md)
- **Plan:** [`docs/superpowers/plans/2026-07-29-decision-queue-buy-opportunity.md`](../superpowers/plans/2026-07-29-decision-queue-buy-opportunity.md) — 5 task, code cụ thể từng bước
- **Task 1 — sửa bug suppression** (code xong, test xanh, chưa commit)

## Task 1 — chi tiết

**Bug:** `ResolveDecisionCommand.HandleHoldWithJournalAsync` khi không có `TradePlanId` để `portfolioId` null, còn `LoadResolvedTodayAsync` lại lọc bỏ đúng entry đó → `StopLossHit` resolve xong hiện lại ngay sau refresh.

**Cách sửa:** thêm tập suppression thứ ba `(Symbol, Type)` build từ tag `trigger:{Type}` mà `ResolveDecision` đã ghi sẵn, chỉ nhận entry có **cả** `PortfolioId` **và** `TradePlanId` null. Cộng thêm thuần — hai tập cũ giữ nguyên.

**File đã sửa (chưa commit):**
- `src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs` — thêm hằng `TriggerTagPrefix`, `LoadResolvedTodayAsync` trả 3-tuple, thêm mệnh đề filter
- `tests/InvestmentApp.Application.Tests/Decisions/GetDecisionQueueQueryHandlerTests.cs` — helper `MakeDecisionJournal` nhận thêm `triggerType`, thêm 2 test

**Kiểm chứng:** `dotnet test tests/InvestmentApp.Application.Tests` → **238/238 pass**. Ba test suppression cũ vẫn xanh mà không sửa dòng nào. Secret scan dòng thêm mới: sạch.

**Commit định dùng:**
```bash
git add src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs tests/InvestmentApp.Application.Tests/Decisions/GetDecisionQueueQueryHandlerTests.cs
git commit -m "fix(decision): suppress được item resolve theo symbol không gắn plan"
```

## Việc tiếp theo

Chạy `/ship` từ **Task 2** trong plan. Thứ tự còn lại:

| Task | Nội dung | Ghi chú |
|---|---|---|
| 2 | `MissingStopLoss` (Warning) | ⚠️ Phải **viết lại** `Handle_PositionWithoutSL_NotIncluded` — test đó khẳng định đúng hành vi ta đổi |
| 3 | `BuyOpportunity` (Info) | Handler nhận thêm `IWatchlistRepository` + `IStockPriceService`; nhớ kiểm `WatchlistRepository` có lọc `IsDeleted` không |
| 4 | Frontend | Chuyển `typeLabel`/`getActionRoute` sang `Record<DecisionType, …>` để bỏ fallthrough |
| 5 | MCP description + docs + ADR | Signature MCP không đổi |

## Sau khi ship — việc người dùng phải làm

Queue sẽ chưa sinh `BuyOpportunity` nào cho tới khi có mục tiêu mua. Con số là quyết định đầu tư, không phải kỹ thuật:

- Đặt `TargetBuyPrice` cho **VNM, MSN, BVH** (hiện đều trống) — qua `update_watchlist_item`
- Đặt stop-loss cho **MWG, HPG, HHV** — qua trade plan / risk dashboard

## Ghi chú khác

- **`stash@{0}` là WIP của user**: `USER WIP: HANDOFF-2026-07-14 deletion (restore onto fix/watchlist-dedupe-target)` — treo từ 14/07, chưa xử lý. Chín stash còn lại là rác cũ từ các PR đã xong, nên dọn khi rảnh.
- MCP Invest Mate **không nối** trong session Claude Code — không gọi được `get_watchlist` / `update_watchlist_item` từ đây. Việc đặt mục tiêu mua phải làm ở phía agent chat có MCP hoặc trên UI.
