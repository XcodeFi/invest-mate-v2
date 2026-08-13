# Plan — Tính R:R từ mốc chốt lời đầu tiên khi kế hoạch không đặt Take-Profit

- **Trạng thái:** Chờ implement (chưa khởi động)
- **Ngày lập:** 2026-08-13
- **Nguồn:** phát hiện lúc verify PR bỏ dấu `*` của Take-Profit — xem `docs/trade-plans.md` §2.1 và `scratch/qa-reports/qa-verify-sell-from-plan-screen-20260813-0955z.md`
- **Tầng ảnh hưởng:** Frontend (trade-plan form) + Infrastructure (`RiskCalculationService`)

## Thuật ngữ

| Viết tắt | Tên đầy đủ | Nghĩa ở đây |
|---|---|---|
| SL | Stop-loss | Ngưỡng cắt lỗ |
| TP | Take-profit | Mức chốt lời |
| R:R | Risk : Reward ratio | Tỷ lệ lời/lỗ kỳ vọng = (khoảng tới TP) / (khoảng tới SL) |

## Vấn đề

`Target` là **không bắt buộc** (2026-08-13): kế hoạch có thể đặt đường ra bằng `ExitTargets` (nhiều mốc chốt lời từng phần) hoặc `ScenarioNodes` (cây kịch bản) thay cho một mức giá duy nhất. Nhưng R:R ở cả hai tầng chỉ đọc `Target`:

| Chỗ | Code hiện tại | Khi `Target = 0` |
|---|---|---|
| Form kế hoạch | `frontend/.../trade-plan.component.ts` `recalculate()`:<br>`this.rr = riskPerShare > 0 && target > 0 ? Math.abs(target - entryPrice) / riskPerShare : 0` | `rr = 0` |
| Ô R:R trên form | hiển thị `rr` | hiện 0 |
| Mục checklist "R:R ratio >= 2:1" | `rrItem.checked = this.rr >= 2` — **tự tick**, không tick tay | **không bao giờ tick được** |
| Vị thế ở trang rủi ro | `RiskCalculationService` `RiskRewardRatio = adjustedReward / adjustedRiskPerShare` với `adjustedTarget` lấy từ `plan.Target` | `null` (đúng, đã xử ở ADR-0017) |

Hệ quả nặng nhất là **mục checklist**: nó mang `critical: true`, `weight: 3`, và tick bằng máy chứ không bằng tay — nên kế hoạch dùng `ExitTargets` sẽ mãi mãi thiếu một mục bắt buộc mà người dùng không có đường nào thoả mãn. Cùng hình dạng với các lỗi đã ghi ở `docs/project-context.md`: **một luật hiện đủ trên giao diện nhưng không có đường để đáp ứng.**

## Định nghĩa "mốc chốt lời đầu tiên"

Không phải `ExitTargets[0]`, và cũng không phải `Level` nhỏ nhất — `Level 1` có thể là `CutLoss`.

```
firstTakeProfit =
    ExitTargets
      .Where(t => t.ActionType == TakeProfit || t.ActionType == PartialExit)
      .Where(t => t.Price > 0)
      .Where(t => direction == "Sell" ? t.Price < entryPrice : t.Price > entryPrice)
      .OrderBy(t => direction == "Sell" ? -t.Price : t.Price)
      .FirstOrDefault()
```

Bốn quyết định trong đó, cần chốt trước khi code:

1. **`PartialExit` có tính là chốt lời?** Đề xuất **có** — bán một phần ở giá cao hơn giá vào là hiện thực hoá lợi nhuận. `TrailingStop` và `CutLoss` thì **không**.
2. **Chọn theo giá, không theo `Level`.** `Level` là thứ tự người dùng nhập, không bảo đảm tăng theo giá. R:R "của mốc đầu tiên" phải là mốc **gần giá vào nhất về phía có lời** — đó là con số bảo thủ nhất, đúng tinh thần dùng R:R làm cổng kỷ luật.
3. **Lọc theo chiều.** Với `Direction = "Sell"` (short) mốc chốt lời nằm **dưới** giá vào. Không lọc thì một mốc đặt sai phía sẽ cho R:R âm hoặc vô nghĩa.
4. **Mốc đã `IsTriggered` có tính?** Đề xuất **có** — R:R là số của kế hoạch lúc lập, không phải số còn lại. Nếu sau này muốn "R:R phần còn lại" thì đó là chỉ số khác, tên khác.

## Phạm vi

**Trong phạm vi:**
- `ExitTargets` → R:R ở form kế hoạch (`recalculate()`) và ở `RiskCalculationService`.
- Thứ tự ưu tiên: `Target > 0` thắng; không có thì lùi về mốc chốt lời đầu tiên; không có cả hai thì R:R **null/trống**, không phải 0.
- Mục checklist "R:R ratio >= 2:1" tick được khi R:R lùi-về-mốc đạt ≥ 2.

**Ngoài phạm vi (cần quyết định riêng):**
- `ScenarioNodes` (Advanced). Cây kịch bản không có "mốc giá chốt lời" phẳng — ngưỡng nằm trong `ConditionType`/`ActionValue` của từng nhánh, và một nhánh có thể là `MoveStopLoss` chứ không phải bán. Suy ra một con số R:R từ cây là bài toán khác; kế hoạch Advanced tạm giữ nguyên hành vi hiện tại (R:R trống nếu không đặt `Target`).
- Đổi ý nghĩa `RiskRewardRatio` **snapshot** trên entity (`plan.RiskRewardRatio`). Đó là giá trị người dùng tự nhập lúc lập kế hoạch, không phải giá trị tính — không chạm.
- Trường mới trên DTO để nói "R:R này lấy từ đâu". Nếu cần thì làm sau, và làm giống `StopLossSource` của ADR-0017.

## Các bước (TDD)

| # | Bước | Verify |
|---|---|---|
| 1 | Helper thuần ở Domain: `TradePlan.FirstTakeProfitPrice()` (hoặc extension) theo định nghĩa trên | Test Domain: `TakeProfit` thấp nhất phía có lời được chọn · `CutLoss`/`TrailingStop` bị loại · `Level` không quyết định · chiều `Sell` chọn mốc thấp nhất · mốc sai phía bị loại · `PartialExit` được tính · rỗng → null |
| 2 | `RiskCalculationService`: `adjustedTarget` lùi về `FirstTakeProfitPrice()` khi `plan.Target = 0`, giữ nguyên mốc điều chỉnh sự kiện quyền (`TradePlanPriceAdjuster.PriceAnchor`) | Test Infrastructure: `Target = 0` + có `ExitTargets` → `RiskRewardRatio` có giá trị · `Target > 0` vẫn thắng · không có cả hai → vẫn `null` (**ca đối chứng, không được thành 0**) |
| 3 | `recalculate()` ở form: cùng thứ tự ưu tiên, dùng cùng định nghĩa "mốc đầu tiên" | Test FE: ô R:R hiện số khi chỉ có `ExitTargets` · mục checklist "R:R ratio >= 2:1" tick được · không có gì → R:R trống chứ không phải 0 |
| 4 | Mutation check bước 2 và 3 | Bỏ nhánh lùi → test tương ứng phải đỏ; in số dòng diff, đòi > 0 |

Bước 1 làm helper ở Domain để **hai tầng dùng chung một định nghĩa**. Nhân bản vị từ ở hai nơi là mở cửa hậu cho lệch — đúng bẫy đã ghi ở `learning_pitfall_refactor_unified_values_not_predicate`.

## Rủi ro

- **R:R của kế hoạch cũ đổi giá trị.** Kế hoạch đang có `Target = 0` + `ExitTargets` hiện R:R trống/0, sau khi sửa sẽ hiện một số thật. Không phải regression, nhưng cần một dòng trong changelog để người dùng không tưởng dữ liệu bị đổi.
- **Điểm checklist tăng.** Mục `critical` weight 3 vốn không bao giờ tick nay tick được → điểm checklist của các kế hoạch đó tăng. Đúng ý muốn, nhưng nếu có báo cáo nào so sánh điểm theo thời gian thì con số sẽ có bậc nhảy tại ngày deploy.
- **`ExitTargets` đặt sai phía.** Dữ liệu cũ có thể có mốc "chốt lời" đặt dưới giá vào cho lệnh Buy (nhập nhầm). Bộ lọc theo chiều biến ca đó thành "không có mốc" → R:R trống, không phải một con số âm gây hiểu sai. Đây là hành vi mong muốn, ghi rõ trong test.

## ADR?

**Không cần.** Không đổi schema, không đổi contract cross-layer, không đi ngược convention nào. Đây là lấp đúng cái lỗ mà ADR-0017 và quyết định "Target không bắt buộc" để lại. Ghi chú thứ tự ưu tiên vào `docs/trade-plans.md` §2.1 là đủ.
