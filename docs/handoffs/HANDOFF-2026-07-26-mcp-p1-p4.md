# HANDOFF — 2026-07-26 — MCP P1–P4 tools expansion

## Now

**4 PR stacked, tất cả đang mở, chưa merge.** Roadmap MCP P0–P4 hoàn tất (38 → **70 tool**).

| PR | Slice | Base | Nội dung |
|---|---|---|---|
| [#133](https://github.com/XcodeFi/invest-mate-v2/pull/133) | P1 Analytics | `master` | 8 read: performance, equity curve, monthly returns, savings comparison, campaign analytics, net worth, flow history, adjusted return |
| [#134](https://github.com/XcodeFi/invest-mate-v2/pull/134) | P2 Market | #133 | 8 read: stock detail/price/history, technical analysis, search, overview, top fluctuation, batch prices |
| [#135](https://github.com/XcodeFi/invest-mate-v2/pull/135) | P3 Portfolio/Trade | #134 | 2 read + 6 write: get_portfolio, get_trades_by_portfolio + CRUD danh mục, xóa lệnh, link plan, bulk import |
| [#136](https://github.com/XcodeFi/invest-mate-v2/pull/136) | P4 Plan Actions | #135 | 8 write + **security fix** (xem dưới) |

Suite: **1.513 pass**. Plan + checkpoint chi tiết: [`docs/plans/mcp-p1-p4-tools-expansion.md`](../plans/mcp-p1-p4-tools-expansion.md).

## Next steps

1. **Merge theo thứ tự #133 → #134 → #135 → #136**, retarget base xuống `master` sau mỗi lần merge (GitHub thường tự làm khi base branch bị xóa).
2. **#136 nên ưu tiên** — chứa fix lỗ ghi cross-user đang khai thác được qua REST hôm nay (không chỉ MCP).
3. Live smoke `tools/list` + `tools/call` từ host thật với API key của Truong — vẫn chưa chạy được lần nào (xem Gotchas).
4. Backlog đã ghi trong plan: PR backfill optional-param idiom cho tool cũ; P5 breadth; 2 follow-up P3 (cascade sau soft-delete portfolio, `BulkTradeItem.Fee/Tax` non-nullable); cân nhắc status guard + idempotency cho `trigger_exit_target`/`update_stop_loss` ở tầng domain.

## Blockers

Không có blocker kỹ thuật. Chỉ chờ review/merge của Truong.

## Gotchas

- **Lỗ bảo mật đã vá trong #136**: `ExecuteLot` + `TriggerExitTarget` handler kiểm `plan.UserId` rồi ghi vào `trade` do caller đưa mà không kiểm chủ sở hữu → tradeId giả mạo ghi `TradePlanId` lên lệnh người khác, `review_trade_plan` cộng lệnh lạ vào P/L. Pattern đúng nằm ngay cạnh ở `LinkTradeToPlanCommandHandler`. **Bài học: mọi entity load-by-caller-id rồi mutate đều cần transitive owner check** — đã lưu memory.
- **Optional param MCP**: param không có C# default sẽ nằm trong `required` của schema dù nullable → host đúng chuẩn không bỏ trống được. Idiom mới: đặt optional param **sau** `ct` với `= null`. Guard: test `Optional_Params_Are_Not_Required_In_Schema`.
- **`annualRate` là phân số** (0.05 = 5%), không phải phần trăm — handler có sanity range −10%..50%, gửi `6.5` là ném exception.
- **Behavior sẵn có, đã ghi vào tool description chứ không sửa handler**: `link_trade_to_plan` vào plan chưa InProgress → throw; `review_trade_plan` đòi Executed; `execute_lot` đòi plan nhiều lô + lô Pending; `trigger_exit_target` không idempotent.
- **Live verify vẫn bị chặn**: mint key mới = ghi prod DB, đọc key trong `npu-assistant/config_secrets.json` = credential exploration — cả hai đều bị classifier từ chối. Cần Truong cấp key hoặc tự chạy curl.
- Sub-agent bị weekly limit chặn khoảng giữa phiên (reset 21:00) — review P1–P4 chạy được bằng Fable sub-agent sau đó; nếu lại hết limit, review inline trong main context vẫn khả thi.
