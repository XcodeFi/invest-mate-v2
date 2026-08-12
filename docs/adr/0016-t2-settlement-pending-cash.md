# ADR-0016 — Tiền bán chờ về T+2 là đại lượng riêng, lịch nghỉ giao dịch lưu trong DB

- **Status:** Accepted
- **Date:** 2026-08-12
- **Related plan:** `docs/superpowers/plans/2026-08-12-t2-settlement-pending-cash.md`
- **Affected layers:** Domain / Application / Infrastructure / Api / Frontend

## Context

Chứng khoán Việt Nam thanh toán theo chu kỳ **T+2**: bán hôm nay thì tiền về tài khoản sau 2 phiên giao dịch. App cộng tiền bán vào tiền mặt **ngay tại ngày khớp lệnh**, nên "Tiền mặt khả dụng" cao hơn thực tế tới 2 phiên — và đó chính là con số dùng để quyết định vào lệnh mới.

Bốn bề mặt cùng chịu ảnh hưởng: hero card `/dashboard` và `/capital-flows` (tính từ `PortfolioSummaryDto.TotalSold`), bản tin AI `<portfolio_cash>` (nền tính khối lượng vị thế), và cửa sổ ghi lệnh MUA (`remainingCash`). `RiskCalculationService` / `SnapshotService` **không** liên quan: chúng dùng `− TotalInvested`, vốn dĩ không cộng tiền bán, nên [ADR-0007](0007-portfolio-cash-formula-divergence.md) giữ nguyên hiệu lực.

Đây cùng hình dạng lỗi với sự cố lỗ giả 23% mà [ADR-0010](0010-corporate-actions-position-projection.md) đã xử lý cho sự kiện quyền: màn hình có con số, người đọc tin là đúng, và không có gì nói cho biết là chưa.

Ràng buộc: codebase **không có** helper lịch phiên nào, và lịch nghỉ giao dịch do HOSE công bố **đổi mỗi năm**, nhiều khi ra lẻ từng đợt chứ không chốt cả năm một lần.

## Options Considered

### Option A — Đại lượng riêng, hàm thuần, lịch nghỉ lưu DB nhập theo từng ngày

- **Pros:**
  - Không sửa `PortfolioCashCalculator` → không đụng công thức bị ADR-0007 ghim và dùng chung với `CashFlowAdjustedReturnService` (đổi nó là lệch TWR).
  - `SettlementCalculator` là hàm thuần, không I/O, unit-test được — cùng họ với `PositionBuilder` / `PortfolioCashCalculator`.
  - Lịch nghỉ đổi mỗi năm mà không cần deploy: nhập qua endpoint + MCP. Lưu theo từng ngày nên nhập lẻ được ngay khi có thông báo, vẫn nhập một lượt cả năm khi lịch đã ra đủ.
  - Số tổng giữ nguyên → không ai đang đọc con số cũ bị lệch.
- **Cons:**
  - Thêm entity + repository + 3 endpoint + 3 tool MCP cho một bảng dữ liệu nhỏ.
  - Quên nhập lịch nghỉ thì T+2 tính thiếu ngày nghỉ, và không có cơ chế nào tự phát hiện.

### Option B — Persist `SettlementDate` vào entity `Trade`

- **Pros:**
  - Đọc trực tiếp, không tính lại mỗi request.
- **Cons:**
  - Cần migration cho toàn bộ trade cũ.
  - `SettlementDate` suy ra được từ `TradeDate` → lưu là dữ liệu trùng lặp trong một aggregate đang được giữ khớp sổ công ty chứng khoán.
  - Sửa/bổ sung lịch nghỉ về sau **không hồi tố** được số đã ghi.

### Option C — Hardcode bảng ngày nghỉ theo năm trong code

- **Pros:**
  - Đơn giản nhất, ~12 dòng dữ liệu, không thêm tầng nào.
- **Cons:**
  - Mỗi năm một PR + một lần deploy chỉ để thêm dữ liệu.
  - Lịch cả năm nhiều khi chưa chốt khi cần dùng; nhập lẻ từng đợt thì phải deploy nhiều lần.
  - Một bản ghi/năm còn tính sai giao dịch cuối tháng 12, vì T+2 của lệnh 30/12 rơi sang năm sau.

## Decision

**Chọn Option A.**

Đại lượng riêng `PendingSettlementCash` thắng vì nó cộng thêm thông tin mà không đổi con số nào đang được đọc — rủi ro thấp nhất cho một thay đổi xuyên bốn bề mặt. Lịch nghỉ lưu DB theo **từng ngày** thắng vì nó khớp cách thông tin thực sự đến: HOSE công bố lẻ từng đợt, và một bản ghi cho một ngày thì vừa nhập lẻ được vừa nhập cả năm được, không có biên năm nào bị hụt.

Trade-off chấp nhận: nhiều bộ phận hơn Option C, và **quên nhập lịch nghỉ là sai theo hướng lạc quan** — đúng hướng mà tính năng này ra đời để chặn.

## Consequences

**Positive:**

- Hero card hiện `120.000.000 ₫` kèm `trong đó 30.000.000 ₫ chờ về — dự kiến 24/02`; con số tổng vẫn khớp sổ công ty chứng khoán để đối chiếu.
- Bản tin AI có `<portfolio_cash_pending>`, advisor trừ phần chưa về khi gợi ý khối lượng.
- Cửa sổ ghi lệnh MUA cảnh báo khi vượt phần đã về, nhưng **vẫn cho lưu** — form đó ghi lệnh đã khớp, có thể đã dùng dịch vụ ứng trước tiền bán; chặn cứng là app từ chối ghi nhận hiện thực.
- Bất biến `đã về + chờ về = TotalSold` được ghim bằng test.
- Lịch nghỉ 2026 (12 phiên) có script seed, và quy tắc "+2 phiên" được ghim bằng golden test lấy thẳng từ thông báo HOSE: lệnh 12/02/2026 → 23/02, lệnh 13/02 → 24/02.

**Negative / Trade-offs:**

- **Quên nhập lịch nghỉ** cho quãng đang tính thì tiền chờ về nhỏ hơn thực tế. Không cơ chế nào tự phát hiện: lưu theo từng ngày thì "chưa nhập" và "không nghỉ" trông giống nhau. Giảm thiểu, không xoá được: hiện ngày về dự kiến cạnh số tiền, và bản tin in `<market_closures_known_through>` để mốc đó tự lộ khi đã cũ.
- Lịch nghỉ gắn `UserId` như mọi entity khác, dù bản chất là dữ liệu dùng chung. Để global thì thành endpoint mà một người ghi làm đổi số của người khác.
- `MarketClosureRepository` không có unit test: `MongoWriteException` có constructor non-public nên không giả được. Luật nghiệp vụ được test ở tầng handler qua repository mock.
- Không cache lịch nghỉ — app một người dùng, document rất bé. Có ý thức, không phải sót.
- **Ngoài phạm vi:** cổ phiếu mua chờ về T+2 (vẫn cho ghi lệnh bán trong ngày mua); dịch vụ ứng trước tiền bán không được mô hình hoá.

**Follow-ups (if any):**

- Migration to run: `scripts/migrations/2026-08-12-market-closures-2026.mongo.js` (cần `USER_ID`). Chưa chạy trên môi trường nào.
- Tests to add: lịch nghỉ 2027 khi HOSE công bố (tháng 12/2026) — nhập qua `add_market_closures`, không cần sửa code.
- Docs to update: `docs/business-domain.md`, `docs/architecture.md`, `docs/features.md`, user guide `tien-ban-cho-ve.md` — đã làm trong PR này.

## References

- Spec: `docs/superpowers/specs/2026-08-12-t2-settlement-pending-cash-design.md`
- Plan: `docs/superpowers/plans/2026-08-12-t2-settlement-pending-cash.md`
- PR: #XX (fill in after merge)
- External: thông báo lịch nghỉ giao dịch năm 2026 của HOSE — 12 phiên (01/01; 16–20/02; 27/04; 30/04–01/05; 31/08–02/09).
