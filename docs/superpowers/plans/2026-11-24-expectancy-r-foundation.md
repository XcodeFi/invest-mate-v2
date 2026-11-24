# Kỳ vọng theo R — Đợt 1 "Nền R" Implementation Plan

> **For agentic workers:** dev lane `Group_indication - dev` làm task-by-task; review lane review cả nhánh. Steps dùng checkbox.

**Goal:** Mọi kế hoạch có 1R cố định, R:R do hệ thống tính, review lưu R thực tế, và có thống kê kỳ vọng theo R (chỉ lệnh đã đóng) theo chiến lược.

**Architecture:** Hàm thuần `RMath` ở Domain; ảnh chụp `InitialRisk` trên `TradePlan` (Mongo embedded); `RealizedR` trong `CampaignReviewData`; query mới `GetStrategyExpectancyQuery` dựa trên plan đã review. Chỉ số tiền cũ giữ nguyên, chỉ thêm nhãn.

**Tech Stack:** .NET 9 + xUnit/FluentAssertions/Moq · Angular 19 inline template · MongoDB (embedded value object).

**Challenger REVISE đã áp dụng:** đọc mục "Sửa sau challenger" trong spec; mục đó THẮNG mọi chỗ mâu thuẫn bên dưới (snapshot rời Draft/khoá ở lần khớp đầu/xoá khi Restore; R:R tính khi đọc, không ghi field; ExpectancyR gồm cả hoà; RealizedR tính khi đọc chỉ trong DTO).

**Spec:** `docs/superpowers/specs/2026-11-24-expectancy-decision-design.md`.

## Global Constraints
- Nhánh `feat/expectancy-r-foundation` tạo từ master **sau khi** MCP đợt 1 merge; main tree.
- Vùng hoà ±0.1R; thắng R > 0.1; thua R < −0.1; % thắng = thắng / (thắng + thua).
- Chỉ plan `Direction == "Buy"` vào R/EV; Sell bỏ qua.
- Thiếu dữ liệu thì `null` và UI "n/a", không trả 0 (memory `learning_pitfall_ratio_null_when_denominator_undefined`).
- Mọi query lọc theo `request.UserId`.
- Không đổi nghĩa chỉ số cũ; chỉ thêm nhãn "theo lệnh, gồm chưa chốt".
- TDD, tiếng Việt có dấu, commit tiếng Việt, không trailer.

## Review Focus
1. **Plan cũ không có `InitialRisk`:** fallback theo `StopLossHistory[0].OldPrice` rồi tới SL hiện tại, kèm `RiskBasis = "uoc-tinh"`; Entry = SL thì R null, không chia cho 0. Test ở Task 1 và Task 3.
2. **SL dời sau Ready:** 1R không đổi. Test ở Task 1: `moving stop after ready keeps initial risk`.
3. **Chiến lược có 0 lệnh thua:** PF null, expectancy vẫn tính. Test ở Task 4.
4. **n nhỏ (0, 1, 2):** Wilson không NaN; n = 0 thì mọi chỉ số null. Test ở Task 4.
5. **Plan Sell hoặc chưa review:** không lọt vào thống kê. Test ở Task 4.

---

### Task 0: Nhánh + spec
- [ ] Tạo nhánh, chép spec vào repo, commit `docs(trade-plans): thiết kế quyết định theo kỳ vọng R`.

### Task 1: `RMath` + ảnh chụp `InitialRisk` (Domain)
**Files:** Create `src/InvestmentApp.Domain/Services/RMath.cs`; Modify `src/InvestmentApp.Domain/Entities/TradePlan.cs` (thêm `InitialRiskSnapshot? InitialRisk`, chụp ở lần Draft→Ready đầu tiên); Test `tests/InvestmentApp.Domain.Tests/...RMathTests.cs`, `TradePlanInitialRiskTests.cs`.
**Produces:**
```csharp
public record InitialRiskSnapshot(decimal Entry, decimal StopLoss, decimal Quantity, decimal RiskAmount, string RiskBasis); // "chot-ke-hoach" | "uoc-tinh"
public static class RMath {
  public static decimal? RiskReward(decimal entry, decimal stopLoss, decimal exitPrice);            // (exit-entry)/(entry-sl), null nếu entry<=sl
  public static decimal? BlendedExitPrice(IReadOnlyList<ExitTarget> targets, decimal? fallbackTarget); // bình quân theo PercentOfPosition
  public static decimal? RealizedR(decimal netPnL, decimal riskAmount);                             // null nếu riskAmount<=0
  public static TradeOutcome Classify(decimal r);                                                    // Win/Loss/Draw theo ±0.1
  public static InitialRiskSnapshot? EstimateLegacy(TradePlan plan);                                 // fallback cho plan cũ
}
```
- [ ] Test fail: `RiskReward_long_basic` (100/90/120 → 2.0), `RiskReward_null_when_stop_not_below_entry`, `BlendedExitPrice_weights_by_percent` (50%@110 + 50%@120 → 115), `Classify_band` (0.1 → Draw, 0.11 → Win, −0.11 → Loss), `first_exit_from_draft_captures_snapshot_via_ready_or_execute_lot`, `snapshot_refreshes_on_update_while_ready`, `snapshot_locks_at_first_fill`, `restore_clears_snapshot`, `zero_old_price_treated_missing`, `lot_entry_is_weighted_average`, `moving stop after ready keeps initial risk`, `EstimateLegacy_uses_first_stop_history_old_price`.
- [ ] FAIL → implement → PASS → commit `feat(trade-plans): hàm R và ảnh chụp rủi ro ban đầu`.

### Task 2: R:R do hệ thống tính
**Files:** Create/Update Trade plan handlers (`CreateTradePlanCommand.cs`, `UpdateTradePlanCommand.cs`) để KHÔNG ghi đè field; thêm `EffectiveRiskReward` trong DTO đọc = RMath khi đầu vào hợp lệ, ngược lại giá trị đã lưu; AI dùng giá trị hiệu lực; `src/InvestmentApp.Api/Mcp/TradePlanTools.cs` sửa description; FE form kế hoạch hiện R:R chỉ đọc (`trade-plan.component.ts` quanh `getRR`).
- [ ] Test fail: `effective_rr_computed_when_inputs_valid`, `effective_rr_falls_back_to_stored_for_sell_or_missing_target`, `stored_rr_not_overwritten`; FE spec `rr field is read-only and shows computed value`.
- [ ] FAIL → implement → PASS → commit `feat(trade-plans): R:R do hệ thống tính từ entry, cắt lỗ, chốt lời`.

### Task 3: `RealizedR` khi review
**Files:** `CampaignReviewData` thêm `decimal? RealizedR`, `string? RiskBasis`; `ReviewTradePlanCommand.cs` / `CampaignReviewService.cs` tính từ PnL ròng / `InitialRisk.RiskAmount` (hoặc `EstimateLegacy`); query đọc plan cũ đã review mà thiếu `RealizedR` thì tính khi đọc, không ghi lại.
- [ ] Test fail: `review_sets_realized_r_from_initial_risk`, `review_uses_legacy_estimate_and_marks_basis`, `review_realized_r_null_when_no_risk`, `read_computes_missing_realized_r_in_dto_only` (verify repository UpdateAsync never called; entity.ReviewData.RealizedR stays null).
- [ ] FAIL → implement → PASS → commit `feat(trade-plans): lưu R thực tế khi review chiến dịch`.

### Task 4: Thống kê kỳ vọng theo R
**Files:** Create `src/InvestmentApp.Application/TradePlans/Queries/GetStrategyExpectancy/GetStrategyExpectancyQuery.cs`; controller route `GET /api/v1/trade-plans/expectancy` (cạnh các route TradePlans); FE: khối "Kỳ vọng theo R (lệnh đã đóng)" trên trang Chiến lược và trang Hiệu suất; nhãn cho chỉ số cũ.
**Produces:**
```csharp
public record ExpectancyStats(string? StrategyId, string StrategyName, int N, int Wins, int Losses, int Draws,
  decimal? WinRate, decimal? WinRateLow95, decimal? WinRateHigh95, decimal? AvgWinR, decimal? AvgLossR,
  decimal? ExpectancyR, decimal? ProfitFactorR);
// Query trả: List<ExpectancyStats> (mỗi chiến lược + "Không gắn chiến lược") + 1 dòng tổng
```
ExpectancyR = trung bình R ròng của mọi lệnh đã đóng (kể cả hoà); WinRate = thắng/(thắng+thua), Wilson 95% (z = 1.96).
- [ ] Test fail: `only_reviewed_buy_plans_of_caller_count`, `groups_by_strategy_with_none_bucket`, `pf_null_when_no_losses`, `n0_all_null`, `wilson_bounds_n1_and_n2_finite`, `expectancy_formula` (W=3 @+2R, L=2 @−1R, D=1 @0R → winRate 0.6, E = (6−2+0)/6 = 0.667R); FE spec hiển thị n và khoảng tin cậy, `n<10 shows 'chưa đủ mẫu để tin'`.
- [ ] FAIL → implement → PASS → commit `feat(strategies): thống kê kỳ vọng theo R từ chiến dịch đã đóng`.

### Task 5: Tài liệu + kiểm tay
- [ ] CHANGELOG (minor), `docs/features.md`, `docs/business-domain.md` (field + endpoint mới), `docs/architecture.md`, hướng dẫn người dùng mục Kế hoạch / Chiến lược.
- [ ] Kiểm trên browser bằng tài khoản test (`qa-verify`): tạo plan → Ready → khớp → review → thấy RealizedR và khối kỳ vọng.
- [ ] Full `dotnet test` + FE spec liên quan. Commit `docs: kỳ vọng theo R`.
