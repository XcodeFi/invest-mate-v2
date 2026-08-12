# Handoff 2026-08-12 — Tiền bán chờ về T+2

**Nhánh:** `feature/t2-settlement-pending-cash` (tách từ `origin/master` tại `dacf311`, chưa có upstream)
**Trạng thái:** spec + plan đã chốt và commit. **Chưa chạm một dòng code nào.**

## Đã xong

| Commit | Nội dung |
|---|---|
| `aa21cfe` | Spec [`docs/superpowers/specs/2026-08-12-t2-settlement-pending-cash-design.md`](../superpowers/specs/2026-08-12-t2-settlement-pending-cash-design.md) |
| `1c6252e` | Plan [`docs/superpowers/plans/2026-08-12-t2-settlement-pending-cash.md`](../superpowers/plans/2026-08-12-t2-settlement-pending-cash.md) — 11 task, TDD từng bước |

## Vào lại từ đâu

Chạy `/ship` trên plan, **Mốc 1 = Task 1-5**: entity `MarketClosure` → repository → command/query → endpoint + 3 tool MCP → script seed 12 ngày nghỉ 2026. Mốc này tự chạy và tự kiểm được, không để lại nửa vời.

Task 6-11 (SettlementCalculator, DTO, hero card, cảnh báo lệnh mua, bản tin AI, ADR + tài liệu) để phiên sau.

## Bốn quyết định đã chốt, đừng mở lại

1. **Không sửa `PortfolioCashCalculator`** — ADR-0007 ghim nó, `CashFlowAdjustedReturnService` dùng chung, đổi là lệch TWR. Thêm đại lượng `PendingSettlementCash` riêng, số tổng giữ nguyên.
2. **Lịch nghỉ trong DB, nhập theo từng ngày** — một bản ghi cho một ngày, gửi được cả mảng, xoá được từng ngày. T7/CN không lưu, suy ra từ `DayOfWeek`. Không có khái niệm "bảng theo năm".
3. **Cửa sổ ghi lệnh MUA chỉ cảnh báo, không chặn** — form ghi lệnh đã khớp; chặn cứng thì không ghi được lệnh thật dùng dịch vụ ứng trước tiền bán. Phải dùng field `settlementWarning` riêng, **không** nhồi vào `quantityError` (chuỗi đó chặn lưu).
4. **Ngoài phạm vi:** cổ phiếu mua chờ về T+2; `RiskCalculationService`/`SnapshotService` (dùng `− TotalInvested`, vốn dĩ không cộng tiền bán).

## Hai con số đã đối chiếu nguồn ngoài

- Lịch nghỉ HOSE 2026 = **12 phiên**: 01/01 · 16–20/02 · 27/04 · 30/04–01/05 · 31/08–02/09.
- Golden test lấy từ thông báo HOSE: giao dịch **12/02/2026 → thanh toán 23/02**, **13/02 → 24/02**. Quy tắc "+2 phiên giao dịch" khớp chính xác.

## Cần biết khi thi hành

- `Application/Common/VietnamDate.cs` đã có sẵn — Task 6 chỉ thêm `Today(utcNow)`. Đừng viết lại helper múi giờ; `GetPendingThesisReviewsQuery` đang tự tra `TimeZoneInfo` là code có trước, không sửa.
- Khuôn idempotent-upsert dùng lại `MoodCheckInRepository`: unique index **có tên** + phân biệt `DuplicateKey` theo tên index.
- Tool MCP tự nạp qua `.WithToolsFromAssembly()` ở `Program.cs:426`, không đăng ký tay. Tham số optional phải nằm **sau** `ct` và có `= null`.
- `dotnet test` âm thầm bỏ project có DLL bị khoá mà tổng vẫn báo Passed — đếm số project trong output sau mỗi lần chạy.

## Chưa làm, có chủ ý

- Chưa push, chưa PR.
- Hai file untracked của phiên khác, **không** stage: `.claude/settings.json`, `docs/superpowers/specs/2026-08-12-thesis-review-action-design.md`.
