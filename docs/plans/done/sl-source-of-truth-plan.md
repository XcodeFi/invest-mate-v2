# Kế hoạch — Stop-loss lấy từ kế hoạch, và dời được theo từng giai đoạn

## Thuật ngữ

| Viết tắt | Tên đầy đủ | Nghĩa ở đây |
|---|---|---|
| SL | Stop-loss | Ngưỡng cắt lỗ |
| R:R | Risk : Reward ratio | Tỷ lệ lời/lỗ kỳ vọng |
| ADR | Architectural Decision Record | Biên bản quyết định kiến trúc |
| DTO | Data Transfer Object | Đối tượng truyền dữ liệu qua API |

## Vấn đề

Dashboard báo "MWG chưa đặt stop-loss" trong khi kế hoạch MWG có `StopLoss = 64.700`, trạng thái `Executed`.

Nguyên nhân đã xác minh trên dữ liệu prod:

1. Cảnh báo đọc `pos.StopLossPrice` ([GetDecisionQueueQuery.cs:198](../../../src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs#L198)), nguồn duy nhất là `_stopLossTargetRepository` ([RiskCalculationService.cs:95-129](../../../src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs#L95-L129)). Không chỗ nào đọc `TradePlan.StopLoss`.
2. `stop_loss_targets` chỉ được ghi từ 2 nơi: bước SL của trade-wizard và form đặt tay ở trang rủi ro. Thực thi kế hoạch **không** đẩy SL sang.
3. Prod chỉ còn 6 bản ghi, mới nhất **16/03/2026** — collection đã bị bỏ hoang 5 tháng. Kế hoạch vẫn có SL đủ.

Hệ quả rộng hơn cảnh báo: `RiskRewardRatio`, `RiskPerShare`, `RiskAmount`, `DistanceToStopLossPercent` của mọi vị thế mở đều null/0.

## Vấn đề thứ hai (chặn hướng A nếu làm một mình)

Nếu chỉ làm phần đọc, kế hoạch thành nguồn SL duy nhất — nhưng SL trong kế hoạch **đóng băng ngay khi khớp lệnh**:

- `isTerminal()` xếp `Executed` cùng nhóm với `Reviewed`/`Cancelled` → `canEditStopLoss` = false ([trade-plan.component.ts:2264-2277](../../../frontend/src/app/features/trade-plan/trade-plan.component.ts#L2264-L2277)).
- Nút sửa kế hoạch cũng ẩn với plan `Executed` ([trade-plan.component.ts:220](../../../frontend/src/app/features/trade-plan/trade-plan.component.ts#L220)).
- Ma trận trong [docs/trade-plans.md:123](../../trade-plans.md) ghi rõ Stop-Loss ở Executed là 🔒.

Nhưng `Executed` **không phải trạng thái đã đóng**: nút "Đóng chiến dịch" chỉ hiện cho plan `Executed` ([trade-plan.component.ts:216](../../../frontend/src/app/features/trade-plan/trade-plan.component.ts#L216)), và nó chuyển sang `Reviewed`. Tức `Executed` = **đang giữ vị thế**. Chính playbook MWG cũng yêu cầu dời SL sau khi đã khớp: "dời SL cả cụm lên 71.000" sau pyramid, "dời SL phần còn lại lên 78.000" sau khi chốt 50%. Cả hai đều không làm được.

Đã có sẵn cửa cho việc này, chỉ chưa đấu nối:

| Có sẵn | Vị trí | Trạng thái |
|---|---|---|
| `PATCH /api/v1/trade-plans/{id}/stop-loss` | [TradePlansController.cs:222](../../../src/InvestmentApp.Api/Controllers/TradePlansController.cs#L222) | Không chặn theo status |
| `UpdateStopLossCommand` → `UpdateStopLossWithHistory` | [UpdateStopLossCommand.cs:34](../../../src/InvestmentApp.Application/TradePlans/Commands/UpdateStopLoss/UpdateStopLossCommand.cs#L34) | Ghi `StopLossHistory`, dời `PricesSetAt` |
| `tradePlanService.updateStopLoss()` | [trade-plan.service.ts:381](../../../frontend/src/app/core/services/trade-plan.service.ts#L381) | **0 nơi gọi** |
| Chấm điểm kỷ luật khi nới SL | [DisciplineScoreCalculator.cs:137-151](../../../src/InvestmentApp.Infrastructure/Services/DisciplineScoreCalculator.cs#L137-L151) | Đã đếm `widenedCount` từ `StopLossHistory` |

Cửa `UpdateStopLoss` cố tình đi vòng `TradePlan.Update()` (bản đó chặn Executed ở [TradePlan.cs:140](../../../src/InvestmentApp.Domain/Entities/TradePlan.cs#L140)) — thiết kế ban đầu đã tách "dời SL" ra khỏi "sửa kế hoạch". Chỉ thiếu giao diện.

## Phạm vi

Hai phase, **ship cùng nhau**. Ship riêng phase 1 sẽ tạo trạng thái nửa vời: kế hoạch thành nguồn SL nhưng vẫn đóng băng — người dùng thấy SL rồi đâm vào tường ngay hôm sau.

### Phase 1 — Đọc SL từ kế hoạch khi không có bản ghi riêng

**Where**

| Layer | File | Việc |
|---|---|---|
| Application | `RepositoryInterfaces.cs` | Thêm `ITradePlanRepository.GetOpenByPortfolioIdAsync` |
| Infrastructure | `Repositories/TradePlanRepository.cs` | Cài đặt: `Ready`/`InProgress`/`Executed`, `!IsDeleted` |
| Infrastructure | `Services/RiskCalculationService.cs` | Thêm `ITradePlanRepository`; khi thiếu `slTarget` thì lấy `EntryPrice`/`StopLoss`/`Target` từ kế hoạch |

**Quy tắc**

- Ưu tiên: có bản ghi `stop_loss_targets` thì bản ghi đó thắng. Kế hoạch chỉ là đường lùi. Lý do: form ở trang rủi ro ghi trực tiếp vào đó, người dùng vừa sửa ở đấy phải thắng.
- Trạng thái kế hoạch được tính: `Ready`, `InProgress`, `Executed`. **Loại `Draft`** — kế hoạch nháp không được làm im cảnh báo cho một vị thế thật đang hở. Loại `Reviewed`/`Cancelled` — đã đóng.
- Nhiều kế hoạch cùng mã: lấy bản `UpdatedAt` mới nhất.
- Lấy **cả ba giá từ cùng một kế hoạch** — chỉ lấy SL mà bỏ entry thì `RiskPerShare`/`R:R` vẫn null.
- `Target = 0` coi như không đặt.
- Điều chỉnh theo sự kiện quyền dùng mốc `TradePlanPriceAdjuster.PriceAnchor(plan)` (= `PricesSetAt ?? CreatedAt`), **không** dùng `UpdatedAt` — sửa ghi chú cũng đổi `UpdatedAt` thì việc điều chỉnh bị vô hiệu.

**Tests** (`tests/InvestmentApp.Infrastructure.Tests/Services/`)

1. Không có SL target + kế hoạch `Executed` có SL → `StopLossPrice` = SL kế hoạch (đây là ca đỏ tái hiện bug MWG).
2. Không có SL target + kế hoạch `Executed` → `RiskPerShare`, `RiskAmount`, `R:R` có giá trị (không null).
3. Có SL target **và** kế hoạch → SL target thắng.
4. Chỉ có kế hoạch `Draft` → `StopLossPrice` vẫn null (cảnh báo vẫn nổ).
5. Chỉ có kế hoạch `Cancelled`/`Reviewed` → vẫn null.
6. Hai kế hoạch cùng mã → lấy bản `UpdatedAt` mới nhất.
7. Kế hoạch có SL + sự kiện quyền sau `PricesSetAt` → SL được điều chỉnh theo hệ số.
8. `Target = 0` → `TargetPrice` null, `R:R` null, nhưng `StopLossPrice` vẫn có.

### Phase 2 — Dời SL trên kế hoạch đang chạy, ở cả ba mặt

Người dùng chọn mở nút ở **cả ba** nơi. Điều này buộc tầng đọc phải nói rõ SL đang lấy từ đâu và từ kế hoạch nào — nếu không, nút ở trang Quản lý rủi ro sẽ ghi vào `stop_loss_targets` và dựng thêm một nguồn cạnh tranh, đúng cái ADR-0017 muốn tránh.

**Where**

| Layer | File | Việc |
|---|---|---|
| Application | `Common/Interfaces/IRiskCalculationService.cs` | `PositionRiskItem` thêm `StopLossSource` (`"Target"`/`"Plan"`/null) + `TradePlanId` |
| Infrastructure | `Services/RiskCalculationService.cs` | Gán hai trường trên theo nguồn đã phân giải ở phase 1 |
| Application | `Decisions/DTOs/DecisionItemDto.cs` | Điền `TradePlanId` cho item `StopLossHit` (hiện hardcode null) |
| Application | `Decisions/Queries/.../GetDecisionQueueQuery.cs` | Truyền `pos.TradePlanId` vào item `StopLossHit` |
| Frontend | `features/trade-plan/trade-plan.component.ts` | Nút "Dời SL" cho plan `InProgress`/`Executed`, cạnh "Đóng chiến dịch"; modal SL mới + lý do |
| Frontend | `features/dashboard/widgets/decision-queue.component.ts` | Nút "Dời SL" trên thẻ `StopLossHit` (có `tradePlanId`) |
| Frontend | `features/risk/risk.component.ts` | Nút "Dời SL" trên dòng vị thế khi `stopLossSource === 'Plan'` → gọi endpoint kế hoạch, **không** ghi `stop_loss_targets` |
| Frontend | `shared/` | Modal dời SL dùng chung cho cả ba chỗ (một component, ba nơi gọi) |
| Docs | `docs/trade-plans.md`, `docs/features.md`, `docs/project-context.md` | Cập nhật ma trận: Executed Stop-Loss 🔒 → ⚠️ dời-có-lý-do |

**Quy tắc**

- Không mở lại toàn bộ form sửa cho plan `Executed` — chỉ thêm một hành động "Dời SL" riêng, đi qua `PATCH /api/v1/trade-plans/{id}/stop-loss`. Các nhóm trường khác giữ 🔒 nguyên.
- Siết SL (Buy: lên): cho tự do, lý do tuỳ chọn.
- Nới SL (Buy: xuống): **cho phép** nhưng bắt buộc nhập lý do, kèm cảnh báo rõ trong modal là lần nới này sẽ bị đếm vào điểm kỷ luật. Không chặn cứng — cơ chế răn đe đã là điểm kỷ luật, không phải cái khoá. Gate lý do nằm trong modal dùng chung nên cả ba mặt cùng chịu một luật.
- Mỗi lần dời ghi một dòng `StopLossHistory` (đã có sẵn) → replay và chấm điểm tự có dữ liệu.
- Trang Quản lý rủi ro: form `stop_loss_targets` cũ **giữ nguyên** cho vị thế không có kế hoạch nào (`stopLossSource` null). Không thêm đường ghi mới vào collection đó.
- Thẻ `MissingStopLoss` trên dashboard không có nút dời — theo định nghĩa nó là vị thế không có SL ở đâu cả, nên không có kế hoạch để dời. Đích của nó vẫn là `/risk-dashboard`.
- Thứ tự nút trong modal: [Hủy] → [Dời SL].

**Tests**

`trade-plan.component.spec.ts`:
1. Plan `Executed` → nút "Dời SL" hiện.
2. Plan `Reviewed`/`Cancelled` → không hiện.
3. Plan `Executed` → các nhóm trường khác vẫn `readonly` (không regression ma trận).

Modal dùng chung (`.spec.ts` của component modal):
4. Nới SL mà lý do rỗng → chặn save + hiện lỗi.
5. Nới SL có lý do → phát ra đúng `newStopLoss` + `reason`.
6. Siết SL không lý do → vẫn phát ra được.

`risk.component.spec.ts`:
7. `stopLossSource === 'Plan'` → nút "Dời SL" hiện, submit gọi `tradePlanService.updateStopLoss`, **không** gọi `riskService.setStopLossTarget`.
8. `stopLossSource` null → giữ form `stop_loss_targets` cũ.

`decision-queue.component.spec.ts`:
9. Item `StopLossHit` có `tradePlanId` → nút "Dời SL" hiện.
10. Item `MissingStopLoss` → không có nút "Dời SL".

Backend (`GetDecisionQueueQueryHandlerTests`, `RiskCalculationService` tests):
11. `PositionRiskItem.StopLossSource` = `"Plan"` khi lùi về kế hoạch, `"Target"` khi có bản ghi riêng, null khi không có gì.
12. Item `StopLossHit` mang `TradePlanId` của kế hoạch đã phân giải.

## Rủi ro

| Rủi ro | Xử lý |
|---|---|
| Nới SL trở nên dễ dàng → xói kỷ luật | Bắt buộc lý do + cảnh báo trong modal + đã đếm vào điểm kỷ luật sẵn |
| Nguồn SL nhập nhằng hai chỗ | ADR ghi rõ thứ tự ưu tiên; `stop_loss_targets` không thêm đường ghi mới |
| Kế hoạch `Ready` (chưa khớp) làm im cảnh báo của một vị thế vào bằng đường khác | Chấp nhận — kế hoạch có SL vẫn là ý định đã tuyên bố của người dùng cho mã đó |
| Thêm 1 query Mongo mỗi danh mục mỗi lần đọc rủi ro | Một query gộp theo danh mục, không phải theo từng mã |

## ADR

Cần — đổi nguồn sự thật cross-layer **và** đi ngược ma trận editability đã ghi trong tài liệu. Xem `docs/adr/0017-stop-loss-source-of-truth.md`.
