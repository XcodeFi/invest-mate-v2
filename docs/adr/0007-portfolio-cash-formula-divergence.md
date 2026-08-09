# ADR-0007 — Bản tin hằng ngày dùng công thức tiền mặt có tính lãi/lỗ đã thực hiện, chưa hợp nhất với risk/snapshot

- **Status:** Accepted
- **Date:** 2026-07-26
- **Related plan:** `docs/superpowers/plans/done/2026-07-26-daily-digest-decision-context.md`
- **Affected layers:** Application / Infrastructure

## Context

Bản tin hằng ngày (`get_daily_digest` MCP + `POST /api/v1/ai/daily-digest`) báo tiền mặt = 0 dù danh mục đang giữ tiền: nó chỉ đọc `FinancialProfile.Accounts` loại `IdleCash` (hồ sơ tài chính cá nhân nhập tay) và **không bao giờ** đọc tiền trong tài khoản chứng khoán. Sự cố thực tế: user bán 14.500 cp HHV thu ~143,9tr, bản tin vẫn báo `idle_cash = 0`, advisor kết luận "không còn dư địa xoay xở". Nặng hơn: con số đó là account balance của position sizing nên **mọi khối lượng gợi ý đều thấp hơn thực tế**.

Khi truy vết để sửa, phát hiện codebase đang có **hai công thức tiền mặt danh mục khác nhau**, và chúng cho ra số khác nhau ngay khi có vị thế đã chốt:

| Nơi | Công thức | Tính lãi/lỗ đã thực hiện? |
|---|---|---|
| `CashFlowAdjustedReturnService.cs:432` | `InitialCapital + flows − grossBuys + grossSells` | ✅ có |
| `RiskCalculationService.cs:91` | `InitialCapital + flows − TotalInvested` | ❌ không |
| `SnapshotService.cs:53` | `InitialCapital + flows − TotalInvested` | ❌ không |

`TotalInvested` chỉ phản ánh giá vốn của **vị thế đang mở**, nên nhóm thứ hai bỏ mất toàn bộ lãi/lỗ đã thực hiện.

Đã verify và **loại trừ** nghi vấn đếm hai lần vốn ban đầu: `CapitalFlowRepository.cs:63` lọc `!f.IsSeedDeposit`, nên `InitialCapital + GetTotalFlowByPortfolioIdAsync(...)` là đúng, không cộng đôi seed deposit.

## Options Considered

### Option A — Bản tin dùng công thức có realized; không đụng risk/snapshot; ghi ADR

- **Pros:**
  - Sửa được bug với phạm vi nhỏ, review gọn, revert dễ.
  - Bản tin — nơi advisor ra quyết định tiền thật — dùng con số chính xác nhất.
  - Không làm đổi số liệu risk đang hiển thị trên UI, không làm lệch snapshot lịch sử đã lưu.
- **Cons:**
  - Trong thời gian chưa hợp nhất, `portfolio_cash` của bản tin **lệch** so với cash mà API risk trả về, với danh mục đã có vị thế chốt.
  - Người đọc code về sau có thể tưởng đó là bug mới.

### Option B — Hợp nhất cả ba nơi về một công thức ngay trong PR này

- **Pros:**
  - Hết phân kỳ, một nguồn chân lý duy nhất.
- **Cons:**
  - Đổi số liệu risk hiển thị trên UI và ảnh hưởng snapshot lịch sử đã persist — cần bộ test riêng cho dữ liệu snapshot cũ.
  - Trộn hai việc không liên quan vào một PR: sửa bug bản tin và thay đổi số liệu risk/snapshot.
  - Rủi ro cao hơn nhiều so với giá trị thu được ngay lúc này.

### Option C — Bản tin dùng luôn công thức `− TotalInvested` cho khớp risk/snapshot

- **Pros:**
  - Nhất quán ngay, không phân kỳ.
- **Cons:**
  - Chọn con số **sai** để đổi lấy sự nhất quán: bỏ mất lãi/lỗ đã thực hiện, tức là bỏ đúng phần tiền vừa về từ lệnh bán — chính là dữ liệu mà bug này cần.

## Decision

**Chọn Option A.**

Bản tin dùng `PortfolioCashCalculator.Compute` (`InitialCapital + flows − grossBuys + grossSells`) — công thức có tính lãi/lỗ đã thực hiện, khớp với hero card capital-flows trên UI. Không sửa `RiskCalculationService` / `SnapshotService` trong PR này.

Trade-off chấp nhận: **tạm thời có hai con số cash cùng tồn tại**. Lý do chấp nhận là bản tin điều khiển quyết định mua/bán tiền thật, nên độ chính xác ở đó có giá trị hơn sự nhất quán hình thức; còn việc đổi số liệu risk/snapshot là thay đổi có ảnh hưởng lịch sử, đáng có PR và bộ test riêng.

## Consequences

**Positive:**

- Bản tin không còn báo "hết tiền" khi tài khoản còn số dư.
- Position sizing tính trên nền vốn đúng — khối lượng gợi ý không còn thấp hơn thực tế.
- Công thức được trích thành hàm thuần `PortfolioCashCalculator`, unit-test được, dùng lại được khi hợp nhất sau này.

**Negative / Trade-offs:**

- Với danh mục đã có vị thế chốt, `portfolio_cash` trong bản tin **sẽ khác** cash trong response của API risk. Đây là **chấp nhận có ý thức, không phải bug** — đừng "sửa" bằng cách làm bản tin khớp lại với risk.
- Người đọc code phải biết có hai công thức; ADR này là nơi ghi nhận.

**Follow-ups (if any):**

- Migration to run: không có.
- Tests to add: khi hợp nhất, cần test cho snapshot lịch sử đã persist trước khi chuyển `SnapshotService` sang `PortfolioCashCalculator`.
- Docs to update: `docs/architecture.md`, `docs/business-domain.md` (ngữ nghĩa `portfolio_cash` vs `idle_cash`) — đã làm trong PR này.
- PR riêng sau: chuyển `RiskCalculationService` + `SnapshotService` sang `PortfolioCashCalculator`, rồi cập nhật ADR này thành `Superseded`.

## References

- Spec: `docs/superpowers/specs/done/2026-07-26-daily-digest-decision-context-design.md`
- Plan: `docs/superpowers/plans/done/2026-07-26-daily-digest-decision-context.md`
- PR: #XX (fill in after merge)
