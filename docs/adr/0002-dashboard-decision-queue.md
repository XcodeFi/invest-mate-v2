# ADR-0002 — Gộp 3 alert widget thành 1 Decision Queue trên Dashboard

- **Status:** Accepted
- **Date:** 2026-05-04
- **Related plan:** `docs/plans/dashboard-decision-engine.md` (PR-2 / P3)
- **Affected layers:** Application, Api, Frontend

## Context

Dashboard trước PR-2 có **3 widget alert rời** ép user phải scan nhiều vị trí khác nhau để biết "việc cần làm hôm nay":

1. **Risk Alert Banner** ở top — stop-loss proximity + concentration + drawdown alerts (frontend tự tổng hợp từ `IRiskCalculationService.GetPortfolioRiskSummaryAsync` per-portfolio).
2. **Advisory Widget** dưới Discipline — scenario node trigger từ `IScenarioAdvisoryService`.
3. **Pending Review section** giữa page — thesis review overdue từ `GetPendingThesisReviewsQuery`.

Mỗi widget có severity / styling riêng, không sort cross-source, không dedupe khi cùng symbol xuất hiện ở nhiều nguồn (e.g. FPT chạm SL VÀ scenario trigger sẽ render 2 lần với 2 nội dung khác nhau). User scan → không biết focus đâu → bỏ qua. Plan v1.1 đã chốt "USP của app = ngăn user phá kỷ luật, không phải trình bày data" → Dashboard phải pivot thành "Decision Engine" ép action.

## Options Considered

### Option A — Soft consolidation: giữ 3 widget, thêm 1 "Top action" banner kèm

- **Pros:**
  - Backwards-compatible với muscle memory user.
  - Rollback đơn giản (chỉ ẩn banner mới).
- **Cons:**
  - Vi phạm nguyên tắc "không giữ duplicate UI" (Q1 plan đã chốt).
  - Tăng noise thay vì giảm — user thấy cùng alert ở 2 nơi.
  - Dedupe phức tạp hơn (giữ widget cũ thì dedupe ở banner khó đảm bảo).

### Option B — Replace 3 widget bằng 1 Decision Queue duy nhất (chosen)

- **Pros:**
  - 1 vị trí duy nhất ở top → user biết chính xác cần focus đâu.
  - Sort cross-source theo severity (Critical first).
  - Dedupe theo (Symbol, PortfolioId) → cùng FPT chạm SL + scenario trigger chỉ render 1 lần (giữ Critical).
  - Empty state positive khi 0 alert → app không trở nên rỗng (`✅ Hôm nay đang kỷ luật + 🔥 streak`).
  - Aligned với USP "ép quyết định kỷ luật".
- **Cons:**
  - Xóa hẳn 3 widget cũ (~180 LOC delete trong `dashboard.component.ts`) — không có graceful fallback nếu Decision Queue endpoint regress.
  - Cần backend mới (`/api/v1/decisions/queue` + `/me/discipline-score/streak`).
  - Streak algorithm derived-on-demand (N+1 trade query per closed plan) — chấp nhận cho solo-user 1-3 plan/tháng nhưng cần migrate sang stored snapshot nếu scale lên.

### Option C — Defer: giữ 3 widget cũ, chờ user feedback

- **Pros:** Risk thấp.
- **Cons:** Chậm validate USP. Plan đã review 2 sub-agent (UX + Architect), 1 năm tới Dashboard vẫn ở trạng thái "cockpit hiển thị" thay vì "decision engine".

## Decision

**We choose Option B — replace 3 widget bằng Decision Queue duy nhất.**

Aligned với USP và plan v1.1 đã chốt sau review 2 sub-agent. Risk rollback acceptable: backend additive (không sửa contract cũ), frontend xóa code có thể revert qua git. Streak derived-on-demand đủ cho solo-user hiện tại; có path migration rõ ràng (stored daily snapshot) khi performance trở thành vấn đề.

## Consequences

**Positive:**

- 1 vị trí canonical cho "việc cần làm hôm nay" — giảm cognitive load scan.
- Sort cross-source theo severity → Critical luôn lên đầu, Warning xếp sau, không bị dilute.
- Dedupe → user không bị spam cùng symbol ở nhiều nguồn.
- Empty state positive đảo trải nghiệm "0 alert = widget biến mất" (rỗng) thành "🔥 streak tích cực".
- Backend được tổ chức rõ ràng: `Decisions` namespace mới, không lẫn vào `Risk` / `TradePlans` / `Discipline` namespaces hiện có.
- Inline action (BÁN/GIỮ) ở PR-3 sẽ chỉ cần update DecisionQueueComponent — không touch dashboard.component.ts thêm.

**Negative / Trade-offs:**

- Mất feature concentration alert + drawdown alert từ Risk Alert Banner (không map vào DecisionType nào trong V1). Workaround: vẫn có ở `/risk-dashboard`.
- Mất link "Đánh giá" từ Pending Review section (link tới `/symbol-timeline` với `tradeId`). Decision Queue chỉ dùng `planId`. User có thể bị mất context "trade nào của plan này" khi click "Xử lý →".
- Streak algorithm phải mirror logic của `DisciplineScoreCalculator.ComputeSlIntegrityAndStopHonor` — nếu sửa SL violation logic 1 chỗ phải nhớ sửa chỗ kia. Documented trong xUnit comment để dev future biết.
- N+1 trade query trong streak — chấp nhận cho V1 (solo-user low-volume), benchmark trước khi scale.

**Follow-ups:**

- PR-3: thêm `ResolveDecisionCommand` (`ExecuteSell` / `HoldWithJournal`) + `JournalEntryType.Decision` enum value mới.
- PR-3: xóa Market Index strip + Mini Equity Curve + Quick Actions khỏi Home (P5).
- Nếu user feedback flag mất concentration alert → mapping `ConcentrationRisk` enum value đã reserved trong `DecisionType` — chỉ cần extend handler.
- Manual QA browser verify với MintStableJwt sau merge.

## References

- Plan: `docs/plans/dashboard-decision-engine.md` (PR-2 / P3 + Checkpoint PR-2)
- PR: #TBD (fill after merge)
- Related ADR: ADR-0001 (worker-to-scheduler — establishes "additive backend, frontend cleanup" pattern as low-risk)
