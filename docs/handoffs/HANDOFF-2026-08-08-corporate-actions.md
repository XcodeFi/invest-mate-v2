# Handoff — 2026-08-08 — Sự kiện quyền (cổ tức & chia tách)

## Đã làm

Brainstorm → spec → plan cho tính năng cổ tức tiền mặt, cổ tức cổ phiếu, chia tách cổ phiếu và tính lại giá vốn / lãi lỗ.

- Spec: `docs/superpowers/specs/2026-08-08-corporate-actions-design.md`
- Plan: `docs/superpowers/plans/2026-08-08-corporate-actions.md` — 17 task, TDD từng bước
- **Chưa viết dòng code sản phẩm nào.**

## Quyết định đã chốt

| # | Quyết định |
|---|---|
| Phạm vi | Tính đúng từ giờ + nhập sự kiện lịch sử bằng tay. **Không** script backfill tự động. |
| Khe hở GDKHQ → ngày về | Hybrid "chờ về" — ghi nhận tại ngày GDKHQ, tách `PendingQuantity` / `SettledQuantity`. |
| Cổ tức tiền mặt | Là **thu nhập**, KHÔNG giảm giá vốn. Bắt buộc có cột "Tổng lãi/lỗ gồm cổ tức". |
| Cổ tức cổ phiếu / chia tách | CÓ giảm giá vốn (tổng vốn không đổi, số lượng tăng). |
| Kiến trúc | `CorporateAction` bất biến + một `PositionBuilder` duy nhất. `Trade` không bao giờ bị sửa. |
| `TradePlan` | Chỉ cảnh báo, không tự sửa. `StopLossTarget` thì tự điều chỉnh tại thời điểm đọc. |
| Cổ phiếu lẻ | Làm tròn xuống, phần lẻ huỷ. |

## Việc còn treo

1. **Git chưa chạy gì.** Hai file doc đang untracked trên nhánh `feature/decision-queue-buy-opportunity` (nhánh này có 7 file WIP của việc khác). Cần:
   ```
   git stash → git fetch → git checkout -b feature/corporate-actions origin/master --no-track
   → commit 2 file doc → git stash pop
   ```
2. **Thực hiện:** `/ship docs/superpowers/plans/2026-08-08-corporate-actions.md`

## Phát hiện phụ cần xử lý trong Task 9

`PnLService` đang hard-code tiền tệ `"USD"` (`PnLService.cs:113`), tính giá vốn bình quân trên toàn bộ lệnh mua kể cả đã bán hết, và bỏ qua phí/thuế. Task 9 viết lại hẳn. Test cũ kỳ vọng `"USD"` (nếu có) là bug đang được sửa, không phải regression.
