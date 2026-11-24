# Kỳ vọng theo R — Đợt 2 "Bảng kịch bản + cổng EV" Implementation Plan

> Dev lane làm task-by-task; review lane review cả nhánh. **Trước khi code, dev đọc lại chữ ký thật** (`TradePlan.MarkReady`, `UpdateTradePlanStatusCommand`, `RMath.Capture`, `ExpectancyCalculator`, form `trade-plan.component.ts`) và ghi lệch so với plan vào note commit đầu (memory `learning_pattern_verify_plan_apis_before_scaffolding`).

**Goal:** Mỗi kế hoạch Buy có bảng kịch bản kết quả (xác suất + giá thoát bình quân), app tính EV theo R (đã trừ phí ước tính) và % thắng ngầm định. Hiện tỉ lệ nền của chiến lược bên cạnh. Chuyển Ready khi EV ≤ 0 hoặc chưa nhập xác suất thì bắt buộc ghi lý do. Ước lượng được chụp lại, khoá ở lần khớp đầu.

**Spec:** `docs/superpowers/specs/2026-11-24-expectancy-decision-design.md` (Goals 2–4; quyết định 4, 6; "Sửa sau challenger" mục 4).

## Ghi chú lệch so với chữ ký thật (dev đọc lại trước khi code, 2026-11-24)

Plan gốc giả định vài chữ ký khác thực tế. Các điểm lệch và cách xử lý (đã báo root):

1. **`MarkReady()` có 85 chỗ gọi trong test và các đường nối tự động.** `UpdateTradePlanStatusCommand` tự chuỗi Draft → Ready → InProgress/Executed (ghi lại lệnh đã khớp), `CreateTradePlanCommand` chuỗi như vậy khi `Status = "Executed"`, và `ExecuteLot` đi thẳng từ Draft. Nếu `MarkReady(string? evGateReason = null)` tự chặn khi EV rỗng thì mọi đường ghi lại sau sự kiện cũng bị chặn, và 85 test hiện có vỡ. **Xử lý:** `MarkReady()` giữ nguyên, không cổng. Thêm `MarkReadyWithEvGate(string? evGateReason, Func<decimal, decimal>? feeAtExit)` chạy cổng rồi gọi `MarkReady()`; chỉ hai đường quyết định mới dùng: `status = "ready"` tường minh trong `UpdateTradePlanStatusCommand` và tạo plan với `Status = "Ready"`. Các đường ghi lại sau sự kiện (Executed/InProgress chuỗi) không qua cổng, vì không còn quyết định vào lệnh nào để cảnh báo.
2. **`InvalidOperationException` đang map sang 409 Conflict** trong `ExceptionMiddleware`. Cổng dùng `EvGateException : InvalidOperationException` (Domain), middleware map riêng sang 400 kèm `code = "EV_GATE_REASON_REQUIRED"` (cùng kiểu `DossierGateException`); MCP đã chuyển `InvalidOperationException` thành message nên agent đọc được câu tiếng Việt cần `evGateReason`.
3. **Phí:** `IFeeCalculationService` nhận `Money` và tách `CalculateTransactionFee(Money, isBuy, ...)` với `CalculateSecuritiesTax(Money, SecurityType, isBuy)`, không có hàm "khứ hồi". Domain không biết service này nên nhận `Func<decimal, decimal> feeAtExit` (phí mua + phí bán + thuế bán tại một giá thoát, tính cả vị thế); lớp Application dựng hàm đó từ service. Không có service (hoặc lỗi) thì phí 0 kèm ghi chú.
4. **EV tính mới khi cổng chạy và khi đọc**, không tin con số lưu trong ảnh chụp, vì Entry/SL/số lượng còn sửa được ở Ready. Ảnh chụp lưu `EvR` và `ImpliedWinProbability` tại thời điểm chụp để dành cho hiệu chỉnh (đợt 3); từ lần khớp đầu thì khoá.
5. **`OutcomeScenario` và `EstimateSnapshot` là class có setter** (không phải record vị trí) cho an toàn với MongoDB, giống `InitialRiskSnapshot`.
6. **Ảnh chụp ước lượng khoá cùng chỗ `InitialRisk` khoá** (`LockInitialRisk`), tức lần khớp đầu từ Draft/Ready, không chụp lại khi đã InProgress (bài học lỗi `ExecuteLot` ở đợt 1).

## Global Constraints
- Nhánh `feat/expectancy-scenarios` từ master `df1626d`; main tree.
- Chỉ plan `Direction == "Buy"`. Plan Sell ẩn bảng, không chặn gì.
- Ô xác suất **không** điền sẵn từ tỉ lệ nền (chống neo). Tổng xác suất phải = 100% ± 0.5 thì mới tính EV.
- Field mới nullable, chỉ cộng thêm. Không cập nhật hàng loạt document cũ. Đọc không ghi.
- Cổng là **cảnh báo + lý do**, không chặn cứng. Lý do lưu trên plan, hiện ở review.
- MCP `create_trade_plan`/`update_trade_plan`: tham số `scenarios` tuỳ chọn, đặt sau `ct` với `= null`; tham số chuyển trạng thái nhận `evGateReason` tuỳ chọn. Cập nhật discovery test nếu schema đổi.
- Phí ước tính: dùng service phí sẵn có (`calculate_fees` / FeeService) cho mua + bán ở mỗi giá thoát. Không có thì 0, kèm ghi chú.
- TDD, tiếng Việt có dấu, commit tiếng Việt, không trailer.

## Review Focus
1. **Tổng xác suất ≠ 100:** không tính EV, form báo "Tổng xác suất phải là 100%", và cổng coi như "chưa nhập".
2. **Sửa ước lượng sau khi đã khớp:** ảnh chụp ước lượng không đổi (đã khoá). Test ở Task 1.
3. **Plan cũ không có kịch bản, chuyển Ready qua MCP không gửi lý do:** lỗi rõ ràng, tiếng Việt, nói cần `evGateReason`. Không 500.
4. **Plan Sell:** không có bảng, không đòi lý do.
5. **Giá thoát ≤ 0 hoặc thiếu 1R** (Entry ≤ SL): EV null, không chia cho 0.

---

### Task 1: Domain: kịch bản, EV, ảnh chụp ước lượng
**Files:** `TradePlan.cs` (thêm `List<OutcomeScenario>? OutcomeScenarios`, `EstimateSnapshot? Estimate`, `string? EvGateReason`); `Domain/Services/RMath.cs` hoặc file mới `ExpectedValue.cs`; tests Domain.
**Produces:**
```csharp
public record OutcomeScenario(string Label, decimal ProbabilityPercent, decimal ExitPrice);
public record EstimateSnapshot(IReadOnlyList<OutcomeScenario> Scenarios, decimal? EvR, decimal? ImpliedWinProbability, DateTime CapturedAt, DateTime? LockedAt);
public static class ExpectedValue {
  public static decimal? EvR(IReadOnlyList<OutcomeScenario> s, decimal entry, decimal stopLoss, decimal quantity, Func<decimal, decimal> roundTripFeeAtExit); // null nếu tổng≠100±0.5 hoặc 1R<=0
  public static decimal? ImpliedWinProbability(IReadOnlyList<OutcomeScenario> s, decimal entry, decimal stopLoss, Func<decimal, decimal> fee); // tổng xác suất kịch bản có R ròng > +0.1
  public static IReadOnlyList<OutcomeScenario> DefaultScenarios(TradePlan plan); // 3 dòng: Đạt kế hoạch / Cắt lỗ / Thoát sớm–hoà; xác suất 0
}
// TradePlan:
public void SetOutcomeScenarios(IReadOnlyList<OutcomeScenario>? s);   // chụp lại Estimate nếu chưa khoá
public void MarkReady(string? evGateReason = null);                  // EV null hoặc <=0 và reason trống → throw InvalidOperationException("Kỳ vọng âm hoặc chưa ước lượng — cần ghi lý do"); Buy only
// Estimate khoá cùng chỗ InitialRisk khoá (lần khớp đầu từ Draft/Ready)
```
- [ ] Test fail:
  - `ev_example`: Entry 100, SL 90, qty 100, phí 0; kịch bản 35%@120, 15%@134, 50%@90 → R = +2, +3.4, −1 → EV = 0.7 + 0.51 − 0.5 = **0.71R**; ImpliedWin = 50%.
  - `ev_null_when_probabilities_not_100`
  - `ev_subtracts_fees`
  - `mark_ready_requires_reason_when_ev_not_positive`
  - `mark_ready_ok_with_reason_and_stores_it`
  - `mark_ready_ok_without_reason_when_ev_positive`
  - `sell_plan_mark_ready_never_requires_reason`
  - `estimate_locks_at_first_fill_and_ignores_later_edits`
  - `restore_clears_estimate`
  - `default_scenarios_three_rows_zero_probability`
- [ ] FAIL → implement → PASS → mutation (bỏ cổng → đỏ; bỏ khoá → đỏ) → commit `feat(trade-plans): kịch bản kết quả, EV theo R và cổng kỳ vọng`.

### Task 2: Application + API + MCP
**Files:** `CreateTradePlanCommand`, `UpdateTradePlanCommand` (nhận `Scenarios`), `UpdateTradePlanStatusCommand` (nhận `EvGateReason`, truyền vào `MarkReady`); DTO đọc thêm `Scenarios`, `EvR`, `ImpliedWinProbability`, `Estimate`, `EvGateReason`; `TradePlanTools.cs` tham số tuỳ chọn; discovery test.
- [ ] Test fail: `create_with_scenarios_persists_and_returns_ev`, `status_ready_without_reason_and_negative_ev_returns_clear_error` (qua McpErrorTranslator/ExceptionMiddleware ra 400 hoặc thông điệp rõ, không 500), `mcp_scenarios_param_optional_not_required`.
- [ ] FAIL → implement → PASS → commit `feat(trade-plans): API và MCP nhận kịch bản kết quả, lý do vượt cổng kỳ vọng`.

### Task 3: Frontend: khối "Kỳ vọng" trên form kế hoạch
**Files:** `trade-plan.component.ts` (+ spec); service gọi `GET /trade-plans/expectancy` (đợt 1) lấy tỉ lệ nền theo `strategyId`.
- Khối gồm: bảng 3 dòng mặc định (nhãn, xác suất %, giá thoát; tính sẵn R từng dòng), nút thêm/xoá dòng, tổng xác suất, EV (R và VND), % thắng ngầm định. Cạnh đó là tỉ lệ nền: "Chiến lược X: thắng 42% (19 lệnh, 95%: 22–64%) · EV +0.3R"; n < 10 thì "chưa đủ mẫu để tin"; không có chiến lược thì dùng dòng tổng.
- Chuyển Ready khi EV ≤ 0 hoặc chưa nhập: mở modal hỏi lý do (nút chính bên phải, memory `feedback_primary_button_right`; overlay z-[60]).
- Khi Estimate đã khoá: hiện "Ước lượng đã khoá lúc khớp lệnh đầu" và vẫn cho sửa bản hiện hành.
- [ ] Test fail: `shows default three rows with empty probabilities`, `ev updates when probabilities sum to 100`, `warns when sum != 100`, `base rate shows n and CI, and 'chưa đủ mẫu để tin' when n<10`, `ready with negative ev opens reason modal and sends reason`, `sell plan hides expectancy block`.
- [ ] FAIL → implement → PASS → build xanh → commit `feat(trade-plans): khối kỳ vọng trên form kế hoạch`.

### Task 4: Review hiển thị ước lượng vs thực tế + tài liệu + QA
- Trang review chiến dịch: "Ước lượng: EV +0.71R, thắng 50% · Thực tế: +1.7R" và lý do vượt cổng nếu có.
- Docs: CHANGELOG minor, features, business-domain, architecture, hướng dẫn người dùng (Kế hoạch giao dịch: cách ước lượng xác suất, vì sao không điền sẵn).
- QA browser (tài khoản test): tạo plan có 3 kịch bản → EV dương → Ready không hỏi; sửa EV âm → Ready hỏi lý do; khớp → sửa xác suất → ảnh chụp không đổi; review → so ước lượng/thực tế.
- [ ] Full dotnet test + FE specs → commit `docs(trade-plans): kỳ vọng theo kịch bản`.
