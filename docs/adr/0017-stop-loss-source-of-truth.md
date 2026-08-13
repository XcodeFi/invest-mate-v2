# ADR-0017 — Kế hoạch là nguồn sự thật của stop-loss, và dời được khi vị thế còn mở

- **Status:** Accepted
- **Date:** 2026-08-13
- **Related plan:** `docs/plans/done/sl-source-of-truth-plan.md`
- **Affected layers:** Application / Infrastructure / Frontend

## Thuật ngữ

| Viết tắt | Tên đầy đủ | Nghĩa ở đây |
|---|---|---|
| SL | Stop-loss | Ngưỡng cắt lỗ |
| R:R | Risk : Reward ratio | Tỷ lệ lời/lỗ kỳ vọng |

## Context

Ngưỡng cắt lỗ đang sống ở hai chỗ độc lập: entity `StopLossTarget` (collection `stop_loss_targets`, khoá theo `TradeId`) và trường `TradePlan.StopLoss`. Mọi số rủi ro ở tầng đọc — cảnh báo "Thiếu stop-loss" trong Decision Queue, `R:R`, `RiskPerShare`, `RiskAmount`, `DistanceToStopLossPercent` — chỉ đọc `StopLossTarget`.

`StopLossTarget` chỉ được ghi từ bước SL của trade-wizard và form đặt tay ở trang rủi ro. Thực thi kế hoạch không đẩy SL sang. Trên prod, collection này còn 6 bản ghi, mới nhất 16/03/2026; trong khi kế hoạch vẫn có SL đầy đủ. Kết quả: vị thế có SL rõ ràng trong kế hoạch vẫn bị báo "chưa đặt stop-loss", và toàn bộ số rủi ro trả về null.

Ràng buộc thứ hai: `StopLossTarget` khoá theo `TradeId`. Xoá hoặc nhập lại lệnh là bản ghi thành mồ côi. Kế hoạch thì khoá theo `PortfolioId` + `Symbol` — bền hơn qua các lần sửa lệnh.

Ràng buộc thứ ba: một kế hoạch là **chiến dịch tổng hợp**, không phải một lệnh. Nó đi qua nhiều giai đoạn — vào từng phần, pyramid, chốt từng mức — và SL phải dời theo từng giai đoạn. Ma trận editability hiện tại (ADR-less, ghi ở `docs/trade-plans.md`) khoá SL ngay khi plan sang `Executed`, nên chính playbook của người dùng ("pyramid → dời SL cả cụm lên 71.000") không thực hiện được.

## Options Considered

### Option A — Tầng đọc lùi về `TradePlan.StopLoss` khi thiếu bản ghi riêng

- **Pros:**
  - Sửa được cả cảnh báo lẫn toàn bộ số rủi ro trong một chỗ.
  - Không cần chuyển đổi dữ liệu cũ; 6 bản ghi `stop_loss_targets` còn lại vẫn hoạt động.
  - Khoá theo `PortfolioId` + `Symbol` nên không mồ côi khi sửa/xoá lệnh.
- **Cons:**
  - SL tồn tại ở hai chỗ, phải định nghĩa thứ tự ưu tiên rõ ràng.
  - Vô nghĩa nếu SL trong kế hoạch vẫn đóng băng ở `Executed` — buộc phải làm kèm phần mở dời SL.

### Option B — Khi kế hoạch sang `Executed` thì tự tạo `StopLossTarget` từ kế hoạch

- **Pros:**
  - Giữ nguyên một nguồn đọc; tầng đọc không phải sửa.
  - Trailing-stop (đang sống trong `StopLossTarget`) dùng được ngay.
- **Cons:**
  - Vẫn khoá theo `TradeId` → xoá lệnh là mồ côi lại, đúng lỗi đang gặp.
  - Phải backfill dữ liệu cũ, và phải đồng bộ hai chiều mỗi lần dời SL.
  - Nhân đôi trạng thái cho một khái niệm.

### Option C — Bỏ hẳn `StopLossTarget`, chỉ dùng kế hoạch

- **Pros:**
  - Một nguồn duy nhất, hết nhập nhằng.
- **Cons:**
  - Trailing-stop và cờ `IsStopLossTriggered`/`IsTargetTriggered` đang sống trong entity đó; phải chuyển hết sang kế hoạch.
  - Phạm vi lớn, rủi ro cao, không cần thiết để sửa lỗi đang gặp.

## Decision

**Chọn Option A**, kèm điều kiện bắt buộc: mở đường dời SL cho kế hoạch đang giữ vị thế, ship cùng release.

Kế hoạch mới là nơi người dùng thực sự nhập SL — bằng chứng là `stop_loss_targets` đã chết 5 tháng trong khi kế hoạch vẫn có SL đủ. Option A đi theo thực tế đó với thay đổi nhỏ nhất, và khoá theo `PortfolioId` + `Symbol` nên bền hơn `TradeId` qua các lần sửa lệnh. Option B nhân đôi trạng thái mà vẫn giữ đúng điểm yếu `TradeId`; Option C đúng về lâu dài nhưng phải di trú cả trailing-stop, không cần cho lỗi này.

Thứ tự ưu tiên: **`StopLossTarget` thắng khi có bản ghi**, kế hoạch là đường lùi. Lý do: form ở trang rủi ro ghi trực tiếp vào entity đó, nên người dùng vừa sửa ở đấy phải thắng. Không thêm đường ghi mới nào vào `stop_loss_targets`.

Trạng thái kế hoạch được tính là "SL đang có hiệu lực": `Ready`, `InProgress`, `Executed`. Loại `Draft` — kế hoạch nháp không được làm im cảnh báo cho vị thế thật đang hở. Loại `Reviewed`/`Cancelled` — đã đóng.

Về ma trận editability: `Executed` **không phải trạng thái đã đóng** — nút "Đóng chiến dịch" chỉ hiện cho plan `Executed` và nó chuyển sang `Reviewed`. Việc `isTerminal()` xếp `Executed` cùng `Reviewed`/`Cancelled` là phân loại sai một trạng thái đang sống. Mở dời SL cho `Executed` là sửa phân loại đó, không phải nới kỷ luật. Cửa backend đã có sẵn và cố tình đi vòng `TradePlan.Update()`: `PATCH /api/v1/trade-plans/{id}/stop-loss` không chặn theo status và ghi `StopLossHistory`.

Nới SL (Buy: dời xuống) **được phép** nhưng bắt buộc nhập lý do và có cảnh báo trong giao diện. Không chặn cứng, vì `DisciplineScoreCalculator` đã đếm mỗi lần nới từ `StopLossHistory` — cơ chế răn đe là điểm kỷ luật, không phải cái khoá. Chặn cứng chỉ đẩy người dùng sang huỷ kế hoạch rồi tạo lại, mất luôn dấu vết.

## Consequences

**Positive:**

- Vị thế có SL trong kế hoạch không còn bị báo "chưa đặt stop-loss".
- `RiskPerShare`, `RiskAmount`, `DistanceToStopLossPercent` có giá trị thật cho mọi vị thế mở. `R:R` chỉ có khi kế hoạch cũng đặt `Target` — `Target = 0` thì vẫn null, đúng ý "chưa đặt" thay vì đọc thành 0.
- SL dời theo từng giai đoạn của chiến dịch mà vẫn nằm trong cùng một kế hoạch — replay và chấm điểm kỷ luật có dữ liệu liên tục.
- Xoá/nhập lại lệnh không làm mất SL nữa.

**Negative / Trade-offs:**

- SL có hai nguồn đọc; ai đọc code phải biết thứ tự ưu tiên. Giảm nhẹ bằng một helper duy nhất trong `RiskCalculationService` và ADR này.
- Kế hoạch `Ready` (chưa khớp) sẽ làm im cảnh báo cho vị thế vào bằng đường khác. Chấp nhận: kế hoạch có SL vẫn là ý định đã tuyên bố cho mã đó.
- Nới SL dễ hơn trước. Đổi lại có lý do bắt buộc + dấu vết + điểm kỷ luật, thay vì bị chặn rồi lách bằng cách huỷ plan.
- Thêm một query Mongo mỗi danh mục mỗi lần đọc rủi ro (gộp theo danh mục, không theo từng mã).

**Follow-ups:**

- Migration: không cần.
- `PositionRiskItem` phải nói rõ nguồn: thêm `StopLossSource` (`"Target"`/`"Plan"`/null) + `TradePlanId`. Không có hai trường này thì nút "Dời SL" ở trang Quản lý rủi ro sẽ ghi vào `stop_loss_targets` và dựng thêm nguồn cạnh tranh.
- `DecisionItemDto.TradePlanId` đang hardcode null cho item stop-loss — phải điền để thẻ trên dashboard dời được SL.
- Tests: 8 ca ở `RiskCalculationService` (ưu tiên nguồn, lọc trạng thái, điều chỉnh sự kiện quyền), 12 ca ở tầng frontend + decision queue (nút dời SL ở ba mặt, gate lý do, không regression ma trận).
- Docs: cập nhật ma trận editability ở `docs/trade-plans.md`, `docs/features.md:429`, `docs/project-context.md:93`.
- Chưa làm: trailing-stop vẫn cần `StopLossTarget`; `GetTrailingStopAlertsAsync` không đổi.

## References

- Plan: `docs/plans/done/sl-source-of-truth-plan.md`
- Liên quan: ADR-0009 (Decision Queue entry-side signals), ADR-0010 (corporate actions — mốc điều chỉnh giá)
- PR: #167
