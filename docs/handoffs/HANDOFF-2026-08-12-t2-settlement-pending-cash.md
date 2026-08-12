# Handoff 2026-08-12 — Tiền bán chờ về T+2

**Nhánh:** `feature/t2-settlement-pending-cash` (tách từ `origin/master` tại `dacf311`, chưa có upstream)
**Trạng thái:** Task 1-11 XONG HẾT. PR [#165](https://github.com/XcodeFi/invest-mate-v2/pull/165) đã mở, chưa merge.

## Đã xong

| Commit | Nội dung |
|---|---|
| `aa21cfe` | Spec [thiết kế](../superpowers/specs/2026-08-12-t2-settlement-pending-cash-design.md) |
| `1c6252e` | Plan [11 task](../superpowers/plans/2026-08-12-t2-settlement-pending-cash.md) |
| `ea1c440` | Task 1 — entity `MarketClosure` (4 test) |
| `5be95fb` | Task 2 — `IMarketClosureRepository` + Mongo impl + DI |
| `7a900af` | Task 3 — add/remove command + get query (7 test) |
| `2a97902` | Task 4 — 3 tool MCP + controller JWT + sibling ApiKey (13 test) |
| `85b9b64` | Task 5 — seed 12 ngày nghỉ 2026 + test ghim hai chiều |
| `a1c98dd` | Vá review vòng 1 — validator nói rõ phải gửi gì |
| `1a97538` | Task 6 — `SettlementCalculator` + `VietnamDate.Today` (12 test, golden HOSE) |
| `f309206` | Task 7 — `PendingSettlementCash` vào DTO + interface FE |
| `79e2925` | Task 8 — dòng chờ về trên hero card (dashboard + capital-flows) |
| `347016f` | Task 9 — cảnh báo mềm ở cửa sổ ghi lệnh MUA |
| `1e16c69` | Task 10 — bản tin AI `<portfolio_cash_pending>` + `known_through` |
| `d0ac016` | Task 11 — ADR-0016 + đồng bộ tài liệu + CHANGELOG + user guide |
| `1c161fa` | Vá review vòng cuối — nhãn ISO + pending null cùng điều kiện cash |

## Vào lại từ đâu — 3 việc treo trước khi merge #165

1. **`/qa-verify` trên browser chưa chạy.** Mở `/dashboard` + `/capital-flows`, xác nhận dòng `trong đó X ₫ chờ về — dự kiến DD/MM` hiển thị đúng tiếng Việt có dấu và số khớp. Cần có lệnh SELL trong 2 phiên gần nhất để dòng đó hiện; không có thì ghi lệnh thử trên dev, chụp, rồi xoá.
2. **Script seed chưa chạy trên môi trường nào.** `mongosh "<conn>/<db>" --eval 'var USER_ID="<userId>"' scripts/migrations/2026-08-12-market-closures-2026.mongo.js`. Chưa nhập thì T+2 chỉ bỏ T7/CN, tức tính thiếu ngày nghỉ lễ.
3. **Re-review diff bản vá cuối** (`1c161fa`): 1 điều kiện null + 2 chỗ `.slice(0, 10)` + test.

## Kết quả

- **17+1 commit.** Backend **2061 pass / 0 fail** (4 project). Frontend **373 pass / 0 fail**. `npm run build` 0 error.
- Hai vòng code review, **4 finding thật đã vá**. Chi tiết trong body PR #165.
- Bài học đã lưu: tool MCP phải khai vào `ReadTools`/`WriteTools` + `Destructive = true`; `DateTime?` trên dây là ISO có giờ nên FE cắt chuỗi phải `slice(0,10)`.

## Hai chỗ tiền đề sai bị bắt trong phiên (đáng nhớ)

1. Review vòng 1 báo lỗi **500 NullReference** cho body thiếu `dates`. Kiểm lại: `Enumerable.Select` trên null ném `ArgumentNullException`, kế thừa `ArgumentException`, và `ExceptionMiddleware` map sang **400**. Chưa từng có 500 — validator vẫn giữ nhưng vì lý do khác (thân lỗi nói rõ thiếu gì). Đã sửa lại comment/tên test viết theo lý do sai.
2. Test `ThrowAsync` không `await` trong method `void` → assertion fire-and-forget, không bao giờ đỏ được. Chính nó che luôn tiền đề sai ở trên.
