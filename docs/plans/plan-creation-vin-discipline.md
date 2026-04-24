# Kế hoạch — Plan Creation với kỷ luật "Vin-style" (Thesis & Invalidation)

> Tài liệu kế hoạch — bổ sung kỷ luật **thesis-driven** vào quy trình tạo Trade Plan, lấy cảm hứng từ case study Vinpearl Air 2019-2020 (Vingroup "dám dừng đúng lúc dù đã đầu tư sâu").
> Ngày tạo: 2026-04-23
> Trạng thái: **Đang thảo luận, chưa triển khai**
> Quyết định đã chốt (2026-04-23):
>
> - **Q1:** Viết plan trước, bàn chi tiết sau ✅
> - **Q2:** InvalidationTrigger enum = `{ EarningsMiss, TrendBreak, NewsShock, ThesisTimeout, Manual }` ✅
> - **Q3:** Plan thuộc `AllocationBucket.Satellite` **được bỏ qua** requirement viết InvalidationCriteria (scalping exemption) ✅
>
> Refinement round 2 (2026-04-23):
>
> - **R1:** KHÔNG giữ cả `Reason` và `Thesis` — rename `Reason` → `Thesis` (drop Reason entirely vì trùng ý nghĩa). Input thêm **helper text + placeholder gợi ý** ngay trong UI. Field `Notes` (đã tồn tại, dùng cho ghi chú tự do) giữ nguyên, không đụng.
> - **R2:** Chiến lược xử lý legacy plan (cờ `LegacyExempt`) cần phân tích sâu hơn — xem §"Phân tích sâu — Legacy plan strategy".
> - **R3:** Gate ở transition `Draft → Ready/InProgress` (giữ nguyên).
>
> Refinement round 3 (2026-04-23):
>
> - **R4:** Hard deadline gate edit **T+3 tháng** (rút từ T+6). Solo-app, user tự kiểm soát workflow → không cần grace period dài. Timeline cuối: T+0/T+1/T+3/T+6.
> - **R5:** **Thêm Dashboard widget "Kỷ luật Thesis"** — xem §D6 và §V1 Frontend.
> - **R6:** Nghiên cứu sâu hơn thị trường VN — mở rộng placeholder examples + thêm §"Tham chiếu case study thị trường Việt Nam".
>
> Refinement round 4 (2026-04-23, post multi-agent review):
>
> 3 sub-agents (Architect / Devil's Advocate / Risk Auditor) review plan → convergence trên nhiều điểm. Các quyết định mới:
>
> - **R7 (Hybrid scope):** Giữ widget V1 nhưng **đơn giản hoá**: drop Disposition/Odean sub-metric + Rule-Violation Count primitive với 5-flag. Giữ SL-Integrity + Plan Quality + Review Timeliness + Stop-Honor Rate primitive. Re-weight: SL-Integrity **50%** / Plan Quality **30%** / Review Timeliness **20%**.
> - **R8 (Size-based gate thay Bucket):** **Drop `AllocationBucket` enum**. Gate dùng **size-based formula**: require thesis ≥ 30 chars + ≥ 1 invalidation rule nếu `quantity × entryPrice ≥ 5% AccountBalance` (size = object fact, không cheatable); else thesis ≥ 15 chars, rule optional.
> - **R9 (Rolling 90 ngày default):** Solo user 5-15 lệnh/tháng → 30 ngày quá ít mẫu. Default 90 ngày, dropdown vẫn 7/30/90/365 để user tuỳ chỉnh.
> - **R10 (Must-fix from review):**
>   - **B1:** Migration-first deploy gate, **drop Bson alias** (driver không hỗ trợ dual key trên 1 property).
>   - **B2:** Migration idempotent — filter step 2 dùng `{ thesis: "" }` thay vì coupling với step 1.
>   - **B3:** `AbortWithThesisInvalidation` cho phép cả khi `Status == Executed` (multi-lot partial-exec case) — KHÔNG giới hạn Ready/InProgress.
>   - **M4:** Legacy `InProgress` + T+3 gate KHÔNG block risk-control methods (`UpdateStopLossWithHistory`, `TriggerScenarioNode`, `TriggerExitTarget`, `ExecuteLot`, `AbortWithThesisInvalidation`). Whitelist explicit.
>   - **M5:** Dùng `UpdateTradePlanStatusCommand` (đã tồn tại) thay `TransitionTradePlanStatusCommand` (không tồn tại).
>   - **M6:** Drop method `ReadyToArm()`; **fold gate vào `MarkReady()` và `MarkInProgress()`** (throw pattern matching entity convention hiện tại).
>   - **M7:** `IPortfolioSnapshotRepository` giờ không cần `GetByUserIdAsync` (vì bỏ Disposition) — giữ nguyên interface.
>   - **M8:** Multi-lot plan — SL-Integrity spec per-lot (match từng `TradeId` trong `TradeIds` với planned SL tại thời điểm lot-execute).
>   - **M10:** Scenario Playbook overlap — V1 ghi chú rõ; V2 thêm de-dup rule `ThesisReviewService` skip plan có `ExitStrategyMode=Advanced` + `TimeElapsed` scenario trong ±2 ngày của `CheckDate`.
>   - **M11:** Sub-metric divide-by-zero — return null khi denominator=0, weighted avg re-normalize weights.
>   - **M12:** Sell direction — tất cả formula có variant cho `Direction == "Sell"` (flip sign). Test case riêng.
>   - Timeline thực: **11-13 ngày** cho V1 (giảm từ 13-16 do drop Disposition + Rule-Violation).

---

## Bối cảnh

### Triết lý từ Vinpearl Air (2019-2020)

Vingroup đã đầu tư rất sâu vào Vinpearl Air (xin giấy phép, tuyển CEO hàng không, chuẩn bị đội bay, mở trường đào tạo phi công) nhưng vẫn **chủ động rút** đầu 2020 khi thấy:

- Thesis gốc (thị trường hàng không VN còn chỗ cho tân binh) không còn đúng.
- DNA không phù hợp (biên lợi nhuận thấp, phụ thuộc yếu tố ngoài tầm).
- Sunk cost không cứu được nếu giả định đã sai.

3 bài học áp vào Trade Plan:

1. **"Dám dừng dù đã đầu tư sâu"** — khi thesis bị phá vỡ, cắt bất kể P&L hiện tại hay chi phí đã bỏ.
2. **"Sunk cost không quyết định tương lai"** — `Reason` rỗng + SL chỉ dựa giá = rất dễ rơi vào vòng lặp "hy vọng".
3. **"Không phải cái gì làm được cũng nên làm"** — tự từ chối plan khi kỷ luật yêu cầu.

### Pain point của plan-creation hiện tại

Soi `src/InvestmentApp.Domain/Entities/TradePlan.cs`:

| Dòng               | Hiện trạng                                                         | Vấn đề                                                                                              |
| ------------------ | ------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------- |
| `:20`            | `Reason` là `string?` nullable, không validate                 | User có thể tạo plan `Reason = null` hoặc `"mua"` → không falsifiable. **→ Rename thành `Thesis` với ràng buộc chặt.** |
| `:21`            | `Notes` là `string?` nullable, free-form                           | OK — giữ nguyên làm ghi chú tự do tách khỏi thesis                                    |
| `:14-16`         | `EntryPrice`, `StopLoss`, `Target` là decimal bắt buộc         | SL là **định nghĩa "sai"** duy nhất → cắt vì giá, không cắt vì thesis              |
| `:41-42`         | `ExitStrategyMode` + `ScenarioNodes` chỉ hỗ trợ price/time   | Không có `EarningsMiss`, `ThesisBreak`, `NewsShock`                                      |
| `:45`            | `TimeHorizon` enum có, nhưng không binding vào lịch review      | "Giữ bao lâu" không ép nhắc review                                                          |
| `:190` (Cancel)  | `Cancel()` không nhận reason                                      | Cancel không có learning loop                                                                  |
| Campaign Review (P0.7) | Chỉ chạy sau `Executed → Reviewed`                            | Không có "mid-flight abort with thesis review"                                                 |

**Kết luận:** plan-creation hiện tại cho phép bỏ qua **4 câu bắt buộc** (tại sao mua / sai ở đâu thì bán / lỗ bao nhiêu / giữ bao lâu). App đang ép kỷ luật **giá** (P4 Risk Budget, Tighten-SL) nhưng bỏ trống kỷ luật **thesis**.

**Post multi-agent review (2026-04-23):** 3 sub-agents (Architect / Devil's Advocate / Risk Auditor) tìm ra:

- 3 BLOCKERS: BsonElement alias không hoạt động (driver chỉ cho 1 key/property) → migration-first; Migration filter step 2 sai → dùng `thesis: ""` độc lập; `AbortWithThesisInvalidation` phải cho phép state `Executed` (multi-lot partial).
- 6 MAJOR: Timeline optimistic 30%; `UpdateTradePlanStatusCommand` đã tồn tại (không tạo command mới); `ReadyToArm()` naming sai → fold vào `MarkReady/MarkInProgress`; Legacy gate T+3 phải whitelist risk-controls để user cứu được lệnh lỗ; Multi-lot SL-Integrity cần spec per-lot; `AllocationBucket` là self-attestation cheatable → thay bằng size-based gate.
- Sample size cho Disposition/Odean quá nhỏ ở solo-user → drop khỏi Hybrid. Đơn giản hoá widget.

---

## Phạm vi & mục tiêu

**Mục tiêu:** ép buộc user viết **thesis falsifiable** + **invalidation rules** trước khi bất kỳ plan nào rời Draft, và mở đường cho thesis-review nudge + mid-flight abort với learning loop.

**Non-goals (out of scope):**

- Không thay đổi logic SL hiện tại (giá + tighten-only trong InProgress vẫn giữ).
- Không đụng Campaign Review workflow hiện có (P0.7).
- Không tích hợp AI tự phát hiện "news shock" tự động (Phase 3+).
- **Không giữ cả `Reason` lẫn `Thesis` song song** (quyết định R1) — `Reason` được rename thẳng thành `Thesis`; field `Notes` (đã tồn tại) giữ nguyên cho ghi chú tự do.
- **Không thêm `AllocationBucket` enum** (R8) — gate phân loại theo size (object fact), không tạo bucket tự khai (cheatable).
- **Không tính Disposition/Odean ở V1** (R7) — sample size solo-user 5-15 lệnh/tháng quá nhỏ cho PGR/PLR reliable. Defer toàn bộ paper gain/loss path analysis sang V2+.
- **Không tính Rule-Violation Count 5-flag** (R7) — bỏ complexity khỏi V1. Nếu V2 cần, thêm lại sau.

---

## Quyết định thiết kế

### D1. Entity design — rename `Reason` → `Thesis` + thêm 3 field mới

```csharp
// TradePlan aggregate — RENAME
public string? Thesis { get; private set; }           // (was Reason) why buy; required khi rời Draft
// Notes giữ nguyên — field hiện có, dùng cho ghi chú tự do, không liên quan thesis

// TradePlan aggregate — NEW fields
public List<InvalidationRule>? InvalidationCriteria { get; private set; }  // ≥1 required khi size ≥ 5% account (xem §D3)
public DateTime? ExpectedReviewDate { get; private set; }         // ngày review thesis; optional nhưng khuyến nghị
public bool LegacyExempt { get; private set; } = false;           // seed true ở migration, xoá dần

// REMOVED: AllocationBucket — dùng size-based gate thay thế (§D3)

// New value object
public class InvalidationRule {
    public InvalidationTrigger Trigger { get; set; }   // enum (xem D2)
    public string Detail { get; set; } = "";           // falsifiable, tối thiểu 20 chars
    public DateTime? CheckDate { get; set; }           // ngày dự kiến verify (vd: earnings date)
    public bool IsTriggered { get; set; }              // set true khi user mark invalidated
    public DateTime? TriggeredAt { get; set; }
}

// New enum
public enum InvalidationTrigger {
    EarningsMiss,    // KQKD không đạt kỳ vọng
    TrendBreak,      // Gãy trend kỹ thuật (vd: mất MA200, volume cao đỏ)
    NewsShock,       // Tin tức thay đổi bản chất (CEO resign, scandal, regulation)
    ThesisTimeout,   // Quá hạn mà thesis chưa thể hiện (vd: giữ 3 tháng vẫn sideways)
    Manual           // User tự nhận xét thesis sai
}
```

### D2. InvalidationTrigger — 5 trigger cố định (Q2 chốt)

Lý do dùng **enum cứng** thay vì freeform:

- Dễ statistics về sau (pattern "user thường cắt vì EarningsMiss hay ThesisTimeout?").
- Dễ gợi ý UI (placeholder mẫu cho từng trigger).
- `Manual` giữ làm escape hatch khi không khớp 4 loại trên.
- Nếu sau này cần thêm (ví dụ `SectorDowngrade`, `MacroShift`) → append vào enum, backward compatible.

### D3. Gate cứng khi save — **size-based** (R8)

**Drop `ReadyToArm()` method** (M6 review). Thay vào đó, **fold gate vào `MarkReady()` và `MarkInProgress()`** — matching existing throw pattern tại `TradePlan.cs:169, 350`.

```csharp
public void MarkReady()  // hoặc tên hiện có — tra entity trước khi rename
{
    EnsureDisciplineGate();  // xem dưới
    Status = TradePlanStatus.Ready;
    // ... existing logic
}

public void MarkInProgress()
{
    EnsureDisciplineGate();
    Status = TradePlanStatus.InProgress;
    // ... existing logic
}

private void EnsureDisciplineGate()
{
    if (LegacyExempt && Status == TradePlanStatus.Draft) return;  // legacy exempt skip (tới T+3)

    decimal planSize = Quantity * EntryPrice;
    decimal threshold = (AccountBalance ?? 0m) * 0.05m;  // 5% account
    bool requireFullDiscipline = planSize >= threshold && AccountBalance.HasValue;

    if (requireFullDiscipline)
    {
        if (string.IsNullOrWhiteSpace(Thesis) || Thesis.Length < 30)
            throw new InvalidOperationException("Thesis ≥ 30 ký tự bắt buộc với plan size ≥ 5% tài khoản");
        if (InvalidationCriteria == null || InvalidationCriteria.Count == 0)
            throw new InvalidOperationException("Phải có ≥ 1 invalidation rule với plan size ≥ 5% tài khoản");
        if (InvalidationCriteria.Any(r => r.Detail.Length < 20))
            throw new InvalidOperationException("Mỗi invalidation rule phải có detail ≥ 20 ký tự");
    }
    else  // scalping / size nhỏ
    {
        if (string.IsNullOrWhiteSpace(Thesis) || Thesis.Length < 15)
            throw new InvalidOperationException("Thesis ≥ 15 ký tự bắt buộc (dù plan size nhỏ)");
        // InvalidationCriteria optional
    }
}
```

**Size-based logic (R8):**

- Threshold = `0.05 × AccountBalance` (nếu `AccountBalance` null → treat requireFullDiscipline = false, chỉ ép thesis ≥ 15).
- `Quantity × EntryPrice` = notional size của plan.
- Không cheatable vì size là object fact từ form input; khác với Bucket self-attestation.

**Exception type:** dùng `InvalidOperationException` (matching existing entity convention) — **không** tạo `TradePlanDisciplineException` mới (M6 architect review). Controller map `InvalidOperationException` sang HTTP 400 với error code `DISCIPLINE_GATE_FAILED` + field name để frontend highlight.

### D4. Mid-flight abort với reason — mở rộng scope (B3)

Thêm method `TradePlan.AbortWithThesisInvalidation(InvalidationTrigger trigger, string detail)`:

- **Áp cho state `Ready | InProgress | Executed`** (B3 review fix — multi-lot plan partial-executed có thể ở `Executed` với trade mở, vẫn cần quyền abort vì thesis sai). KHÔNG throw khi Executed — chỉ skip status change nếu plan đã terminal.
- Set `Status = Cancelled` (nếu Ready, không có trade nào mở) hoặc giữ status hiện tại + mark flag `ThesisInvalidated = true` (nếu đã có trade — service layer handle exit trades).
- Append `InvalidationRule { Trigger, Detail, IsTriggered=true, TriggeredAt=UtcNow }` vào `InvalidationCriteria`.
- Raise domain event `TradePlanThesisInvalidatedEvent { PlanId, Trigger, Detail, AbortedAt, TradeIds }`.
- Phục vụ P7 Behavioral Pattern Detection: pattern `DisciplinedAbort` (thesis sai → dừng) vs `SunkCostHold` (thesis sai nhưng vẫn giữ).
- Throw chỉ khi `Status == Reviewed || Cancelled` (đã terminal, abort vô nghĩa).

**Phân biệt với `Cancel()` hiện tại:**

- `Cancel()` = không muốn vào lệnh nữa, không ghi lý do thesis. Chỉ Ready/Draft.
- `AbortWithThesisInvalidation()` = thesis đã sai, log trigger + detail để học. Áp Ready/InProgress/Executed.
- UI 2 button phân biệt rõ (xem §Frontend).

**Restore() behavior sau Abort** (N-item fix):

- `Restore()` trên plan Cancelled-via-Abort → ngoài `TradeId/TradeIds/ExecutedAt` reset, **clear `InvalidationRule[*].IsTriggered = false`** + `TriggeredAt = null` cho các rule đã triggered. Giữ rule text (user không cần viết lại).

### D5. Thesis review nudge (V2)

`ThesisReviewService` chạy mỗi ngày (hosted service, cron daily 07:00 Asia/Ho_Chi_Minh):

1. Query tất cả `TradePlan` với `Status ∈ {Ready, InProgress}`, `InvalidationCriteria != null`.
2. Với mỗi rule có `CheckDate ≤ today + 2 days` và chưa `IsTriggered`: tạo `Notification` hoặc `AlertRule` kiểu `ThesisReviewDue`.
3. Với plan có `ExpectedReviewDate ≤ today`: tạo nudge "Đến ngày review thesis định kỳ của {Symbol}. Thesis còn đúng không?".
4. FE: badge ở header + trang `/pending-reviews` (tái dùng UI của post-trade review nếu được).

### D6. Dashboard widget "Kỷ luật Thesis" — **Hybrid** (post-review, 2026-04-23)

**Mục tiêu:** user tự giám sát kỷ luật mỗi ngày, nhìn 1 phát thấy mình đang tuân thủ hay trôi dạt. **Dùng công thức Hybrid** (R7) — đơn giản hoá từ v2: drop Disposition/Odean + Rule-Violation 5-flag để giảm complexity + sample-size risk cho solo-user.

**Discipline Score 0-100** (weighted avg, rolling **90 ngày** default — R9):

| Sub-metric                                | Weight | Công thức                                                                                                                                                                                                                          | Mục tiêu (bar)                                  |
| ----------------------------------------- | ------: | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------ |
| **SL-Integrity**                    | **50%** | `max(0, StopHonorRate − SlWidenedRate)` · 100<br>• `StopHonorRate` = trades lỗ đóng với `exitPrice ≥ plannedSL` / tổng trades lỗ đã đóng.<br>• `SlWidenedRate` = trades có entry trong `StopLossHistory` mà SL được nới xa hơn khi position underwater / tổng trades có SL history. Multi-lot: spec per-lot matching theo `TradeIds` (M8). | ≥ 85 → xanh; < 70 → vàng; < 50 → đỏ |
| **Plan Quality**                    | **30%** | `% plan Status=Ready|InProgress|Executed trong period pass gate tại thời điểm transition (thesis đủ length + InvalidationCriteria theo size-based rule)`                                                                         | ≥ 95 xanh; < 85 vàng; < 70 đỏ                |
| **Review Timeliness**               | **20%** | `% plan Ready/InProgress đã review thesis trong 3 ngày kể từ CheckDate/ExpectedReviewDate`                                                                                                                                     | ≥ 80 xanh; < 50 đỏ                            |

Overall score color-code: ≥ 80 xanh (**"Kỷ luật Vin"**); 60-79 vàng (**"Cần cải thiện"**); < 60 đỏ (**"Trôi dạt"**).

**Null handling (M11 fix):** bất kỳ sub-metric nào có denominator = 0 → return `null`. Weighted avg **re-normalize weights** chỉ trên sub-metric non-null. Nếu cả 3 null → overall = `null`, label = "Chưa đủ dữ liệu".

**Sell direction (M12 fix):** khi `Direction == "Sell"`, `StopHonorRate` check `exitPrice ≤ plannedSL` (flip sign); `SlWidenedRate` detect `NewSL > OldSL` khi position underwater tại thời điểm change. Test case riêng #xx.

**Chú thích quan trọng:**

- **Chạm SL pre-committed KHÔNG bị penalize.** Đây chính là "discipline honored" — đưa vào tử số của `StopHonorRate` với giá trị dương. Lý do: Thaler-Shefrin (1981) self-control model — pre-commitment device hoạt động đúng.
- **SL bị nới rộng khi underwater** → penalty. Detect qua `StopLossHistory`: entry `{ OldSL, NewSL, ChangedAt }` với `NewSL < OldSL` (long) hoặc `NewSL > OldSL` (short) **và** tại `ChangedAt` MTM < entry → flag `SlWidened = true`. Timestamp đã có trên `StopLossHistoryEntry`.
- **Drop Disposition/Odean** (R7): solo user sample size quá nhỏ (3-5 realized loss/30 ngày) → 1 lệnh outlier lật score = noise, not signal. Không đưa vào Hybrid.

**Widget UI (V1 — Hybrid):**

- Card kích thước vừa trên Dashboard Cockpit, **đặt cạnh Risk Alert hiện có** (chốt từ R5).
- Layout composite + 1 primitive (gọn hơn v2):
  ```
  ┌─────────────────────────────────────────┐
  │ Điểm Kỷ luật Thesis         [90 ngày ▼] │
  │                                         │
  │        87 / 100   🟢 Kỷ luật Vin        │
  │     ▁▂▂▃▅▆▇▇▇ (trend 90 ngày)          │
  │                                         │
  │ ├ SL-Integrity ────── 92%  ████▊     │
  │ ├ Plan Quality ─────── 94%  ████▋     │
  │ └ Review Timeliness ── 83%  ████░     │
  │                                         │
  │ 🎯 Stop-Honor Rate:  87% (13/15 lệnh)  │
  │   (mẫu 15 lệnh lỗ trong 90 ngày)       │
  │                                         │
  │ [Xem chi tiết →] (V2)                   │
  └─────────────────────────────────────────┘
  ```
- **Period filter:** 7 / 30 / 90 / 365 ngày, default **90** (R9 — giảm noise solo-user).
- **Trend line:** sparkline điểm composite qua các ngày.
- **Alert:** nếu composite < 60 → banner đỏ: "⚠ Kỷ luật trôi dạt — review lại các plan InProgress ngay."
- **Sample size display:** luôn hiển thị "mẫu X lệnh" để user tự đánh giá noise vs signal.

**1 Primitive (V1 — Hybrid):**

- **Stop-Honor Rate** = `trades lỗ đã đóng với exitPrice ≥ plannedSL / tổng trades lỗ đã đóng trong period`. Hiển thị dạng "87% (13/15 lệnh)". Retail-friendly, dễ hiểu ngay.
- (Drop Rule-Violation Count + 5-flag từ v2 — R7).

**Backend V1:**

- Endpoint `GET /api/v1/me/discipline-score?days=90` trả (Hybrid):
  ```json
  {
    "overall": 87,
    "label": "Kỷ luật Vin",
    "components": {
      "slIntegrity": 92,
      "planQuality": 94,
      "reviewTimeliness": 83
    },
    "primitives": {
      "stopHonorRate": { "value": 0.87, "hit": 13, "total": 15 }
    },
    "sampleSize": {
      "totalPlans": 28,
      "closedLossTrades": 15,
      "daysObserved": 90
    },
    "trend": [{ "date": "...", "score": 85 }, ...],
    "generatedAt": "..."
  }
  ```
- Query server-side, tái dùng repos có sẵn (`ITradePlanRepository`, `ITradeRepository`). **Không cần** `ISnapshotRepository` / `IRiskProfileRepository` ở Hybrid (do drop Disposition + Rule-Violation).
- Cache 5 phút + **invalidate on `TradeClosedEvent`, `PlanReviewedEvent`, `TradePlanThesisInvalidatedEvent`** (N13 fix).

**Backend V2 (deferred):**

- Trang `/discipline-report` với drill-down: list plan vi phạm mỗi sub-metric, list trades có `sl_widened_underwater`, Odean paper/realized breakdown.
- Export CSV 1 tuần/tháng/năm.
- So sánh period-over-period: "Tháng này 87 (+5 vs tháng trước)".
- Cost-of-violations (learning từ Tradervue): "Nới SL khi underwater đã làm tôi mất −12.5M VND trong 30 ngày qua".

---

## Phân pha triển khai

### Phase V1 — Foundation (5-7 ngày) — **MVP ship-ready**

**Mục tiêu:** mọi plan mới không thể rời Draft nếu thiếu Thesis + InvalidationCriteria (tuỳ Bucket).

**Scope cụ thể:**

Domain:

- `src/InvestmentApp.Domain/Entities/TradePlan.cs` — **MOD**
  - **Rename** property `Reason` → `Thesis`. **Drop BsonElement alias** (R10/B1) — MongoDB driver chỉ hỗ trợ 1 key per property. Thay bằng migration-first deploy gate (§Migration).
  - Thêm `InvalidationCriteria`, `ExpectedReviewDate`, `LegacyExempt` fields (property + setter private). **KHÔNG thêm `Bucket`** (R8 drop).
  - Mở rộng ctor: đổi param `reason` → `thesis`, thêm optional params khác.
  - Mở rộng `Update()`: đổi `reason` → `thesis`, thêm `invalidationCriteria`, `expectedReviewDate`.
  - Method mới `SetThesis(string)`, `SetInvalidationCriteria(List<InvalidationRule>)`, `SetExpectedReviewDate(DateTime?)`.
  - **Drop `ReadyToArm()`** (R10/M6). Thay bằng private `EnsureDisciplineGate()` — fold vào các method transition hiện có (`MarkReady`, `MarkInProgress`, hoặc tương đương). Tra entity để lấy tên method transition thực tế trước khi code.
  - Method mới `AbortWithThesisInvalidation(InvalidationTrigger, string)` (D4) — áp Ready/InProgress/**Executed**.
  - `Cancel()` hiện tại giữ nguyên cho case cancel non-thesis.
  - `Restore()` — mở rộng: clear `InvalidationRule[*].IsTriggered` khi restore plan từ Cancelled-via-Abort.
  - `Notes` không đụng — giữ nguyên semantic "ghi chú tự do".
- `src/InvestmentApp.Domain/Entities/InvalidationRule.cs` — **NEW** value object.
- `src/InvestmentApp.Domain/Entities/InvalidationTrigger.cs` — **NEW** enum (cùng file `TradePlan.cs` hoặc tách — follow existing convention).
- **Drop** `AllocationBucket.cs` (R8).
- **Drop** `TradePlanDisciplineException.cs` (R10/M6) — dùng `InvalidOperationException` matching existing entity convention.
- `src/InvestmentApp.Domain/Events/TradePlanThesisInvalidatedEvent.cs` — **NEW** domain event (V1 chỉ raise qua `AddDomainEvent`, V2 mới handle).

Application:

- `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlanCommand.cs` — **MOD** thêm field (rename `reason` → `thesis`, thêm invalidation, expectedReviewDate). Nhóm vào `ThesisDiscipline` nested DTO để tránh CreateCommand bloat (follow pattern `ChecklistItemDto`).
- `.../UpdateTradePlanCommand.cs` — **MOD** tương tự.
- `.../UpdateTradePlanStatusCommand.cs` — **MOD** (M5 fix: tên đúng, command đã tồn tại tại `TradePlansController.cs:180`). Handler gọi `MarkReady()/MarkInProgress()` — method đã có `EnsureDisciplineGate()` nội bộ (§D3).
- `.../AbortTradePlanCommand.cs` — **NEW** (D4). Return type: `AbortTradePlanResult { planId, status, tradeIdsAffected[] }`. Raise `TradePlanThesisInvalidatedEvent` qua `AggregateRoot.AddDomainEvent` (verify dispatcher pipeline đã có cho `PlanReviewedEvent`).
- `CreateTradePlanCommandHandler` — xem xét case auto-transition `Draft → Ready → InProgress → Executed` khi `Status == "Executed"` (logic hiện có tại `CreateTradePlanCommandHandler.cs:126`). Gate phải áp mỗi bước transition, không skip. (Architect's Top-3 risk #3.)
- Handlers tương ứng.

Api:

- `src/InvestmentApp.Api/Controllers/TradePlansController.cs` — **MOD**:
  - POST `/api/v1/trade-plans` — accept thesis + invalidation + bucket + expectedReviewDate.
  - PUT `/api/v1/trade-plans/{id}` — như trên.
  - PATCH `/api/v1/trade-plans/{id}/status` — gate check (đang có endpoint transition status, chỉ thêm gate check).
  - POST `/api/v1/trade-plans/{id}/abort` — **NEW** body `{ trigger, detail }`, gọi `AbortTradePlanCommand`.
- `InvalidOperationException` từ gate → HTTP 400 với `{ code: "DISCIPLINE_GATE_FAILED", field: "thesis"|"invalidationCriteria", message: "..." }`.

**Dashboard widget backend (§D6 Hybrid):**

- `src/InvestmentApp.Application/Discipline/Queries/GetDisciplineScoreQuery.cs` — **NEW**. Input: `{ userId, days }`. Output: structure như §D6 (3 components + 1 primitive + sampleSize + trend).
- `src/InvestmentApp.Application/Discipline/Services/IDisciplineScoreCalculator.cs` — **NEW** interface.
- `src/InvestmentApp.Infrastructure/Services/DisciplineScoreCalculator.cs` — **NEW** implementation, chia nhỏ thành **4 private methods** (giảm từ 6 của v2):
  1. `ComputeSlIntegrity(trades, plans)` — dùng `StopLossHistory` của `TradePlan` → detect `sl_widened_underwater`. Multi-lot matching per-lot theo `TradeIds` (M8). Sell direction flip (M12).
  2. `ComputePlanQuality(plans)` — % plan pass gate size-based.
  3. `ComputeReviewTimeliness(plans)` — % plan review đúng hạn.
  4. `ComputeStopHonorRate(trades)` — primitive, return `{ value, hit, total }`.
- `ComputeOverall(subMetrics)` — weighted avg với null re-normalization (M11).
- `src/InvestmentApp.Api/Controllers/DisciplineController.cs` — **NEW**. Route `/api/v1/me/discipline-score`. `[Authorize]`. Query param `days` (default **90** — R9, enum 7/30/90/365). Cache 5 phút + invalidate on `TradeClosedEvent/PlanReviewedEvent/TradePlanThesisInvalidatedEvent`.

**Dependencies inject vào calculator (giảm từ 5 xuống 3):**

- `ITradePlanRepository` (đã có)
- `ITradeRepository` (đã có)
- `IMemoryCache` (.NET built-in)

**Drop:** `ISnapshotRepository`, `IRiskProfileRepository`, `ViolationFlag` enum (không còn dùng ở Hybrid).

Infrastructure:

- Migration `scripts/migrations/2026-04-XX-add-tradeplan-thesis.mongo.js` — **NEW**:
  - Plans cũ backfill `Bucket = Core`, `Thesis = Reason ?? "(thesis chưa ghi lại)"`, `InvalidationCriteria = []`.
  - **Không ép** plan cũ phải có invalidation — chỉ plan mới (ngày migration trở đi) chịu gate. Cờ `LegacyExempt = true` (soft).
- MongoDB index: không cần mới ở V1.

Frontend:

- `frontend/src/app/features/trade-plan/trade-plan.component.ts` — **MOD**:
  - Đổi binding `reason` → `thesis` (cả `TradePlanForm` model, template, và submit payload).
  - Thêm section "Kỷ luật mua" (ở đầu form, trước Entry):
    - Textarea `Thesis` — min length tự động **theo size-based rule** (R8):
      - **Label trên input:** "Luận điểm mua (vì sao tin cổ phiếu này thắng?)".
      - **Placeholder bên trong:** "Ví dụ: Mua EVF vì EPS Q1 dự phóng +35% YoY, ROE > 18%, dư địa tín dụng bán lẻ còn lớn."
      - **Helper text dưới input** (màu xám, text nhỏ): "Viết thesis **có thể chứng minh sai được** (falsifiable). 'Tốt', 'tiềm năng' KHÔNG phải thesis. Phải nêu con số/điều kiện cụ thể."
      - Counter ký tự live + **size indicator**: "27/30 (Size 6.2% tài khoản — bắt buộc ≥ 30 chars)" hoặc "12/15 (Size 2.1% — thesis ngắn OK)". Live update khi Quantity/EntryPrice đổi.
    - **Drop Bucket toggle** (R8) — size tự quyết định strict vs loose mode.
    - Date picker `ExpectedReviewDate` với helper: "Ngày bạn dự kiến review lại thesis (mặc định theo TimeHorizon: Short 14 ngày, Medium 60, Long 180)."
  - Thêm section "Điều kiện thesis sai" (sau StopLoss, trước ExitTargets):
    - List InvalidationRule — row gồm dropdown Trigger + textarea Detail (min 20 chars) + optional date picker CheckDate.
    - Placeholder Detail theo từng trigger — **tất cả viết theo ngữ cảnh TTCK Việt Nam** (xem §"Tham chiếu case study thị trường Việt Nam" để hiểu nguồn):
      - `EarningsMiss` → "Vd: BCTC Q1/2026 EPS < 20% YoY, HOẶC trích lập dự phòng > 2× Q trước, HOẶC không chia cổ tức như nghị quyết ĐHCĐ."
      - `TrendBreak` → "Vd: Đóng cửa dưới MA200 kèm volume > 2× TB20, HOẶC khối ngoại bán ròng > 10 phiên liên tiếp, HOẶC tự doanh CTCK xả ròng > 50 tỷ/phiên."
      - `NewsShock` → "Vd: Chủ tịch/CEO bị khởi tố/bắt (case FLC, TNH, VTP); chậm thanh toán trái phiếu > 1 kỳ; UBCKNN xử phạt thao túng giá; kiểm toán từ chối xác nhận BCTC; mất hợp đồng chiến lược."
      - `ThesisTimeout` → "Vd: Giữ 90 ngày mà giá sideways ± 3% kèm thanh khoản < 50% trung bình 1 năm; ngành chính bước vào downcycle (thép HRC, hoá chất, BĐS cao tầng)."
      - `Manual` → "Vd: Tôi thấy mình đang hy vọng thay vì phân tích; tỷ trọng position đã vượt kế hoạch ban đầu do DCA xuống; quyết định dựa trên tin nhóm Zalo/Tele thay vì nghiên cứu cá nhân."
  - **Dashboard widget "Kỷ luật Thesis"** (§D6 Hybrid):
    - File mới `frontend/src/app/features/dashboard/widgets/discipline-score-widget.component.ts` (standalone, inline template).
    - Mount trong Dashboard Cockpit **cạnh Risk Alert hiện có** (chốt từ R5).
    - Gọi `DisciplineService.getScore(days)` → render:
      - Composite score 0-100 + label color-coded (Kỷ luật Vin / Cần cải thiện / Trôi dạt) hoặc "Chưa đủ dữ liệu" khi `overall = null`.
      - Trend sparkline composite.
      - **3 sub-bars:** SL-Integrity (50%) / Plan Quality (30%) / Review Timeliness (20%) (weight hiển thị tooltip on hover).
      - **1 primitive:** Stop-Honor Rate (dạng "87% (13/15 lệnh)").
      - **Sample size display**: "mẫu 15 lệnh lỗ trong 90 ngày" để user tự đánh giá signal vs noise (R9).
    - Period dropdown 7/30/90/365 ngày, **default 90** (R9).
    - Banner đỏ khi composite < 60.
    - Click "Xem chi tiết" → placeholder toast "Báo cáo chi tiết sẽ có ở V2" (disable button V1).
  - `frontend/src/app/core/services/discipline.service.ts` — **NEW** service: `getScore(days: number): Observable<DisciplineScoreDto>`.
  - Type definitions tương ứng với response backend §D6.
  - Banner cảnh báo khi vi phạm gate: "⚠ Chưa đủ kỷ luật để chuyển sang Ready. [3 lỗi]".
  - Button "Thesis sai, cắt" (chỉ hiện ở Ready/InProgress) — KHÁC với button "Huỷ plan" hiện có: modal nhập Trigger + Detail → gọi API abort.
- `frontend/src/app/core/services/trade-plan.service.ts` — **MOD**: đổi `reason` → `thesis` trong DTO, thêm `abort(id, payload)`, mở rộng create/update payload.

Tests (TDD order, viết **trước** implement — bắt buộc theo CLAUDE.md `Red → Green → Refactor`, chạy `dotnet test` sau mỗi bước):

| #  | Layer       | Test                                                                                | File                                                      |
| -- | ----------- | ----------------------------------------------------------------------------------- | --------------------------------------------------------- |
| 1  | Domain      | Ctor không nhận Thesis → plan ở `Draft` OK (backward compat)                    | `TradePlanTests.cs`                                     |
| 2  | Domain      | `SetThesis("...")` lưu đúng, throw nếu empty                                    | cùng file                                                |
| 3  | Domain      | Gate size-based — size ≥ 5% account + thesis < 30 → throw `InvalidOperationException` | `TradePlanDisciplineGateTests.cs` **NEW**             |
| 4  | Domain      | Gate — size ≥ 5% account + 0 rule → throw                                      | cùng file                                                |
| 5  | Domain      | Gate — size ≥ 5% account + rule detail < 20 → throw                            | cùng file                                                |
| 6  | Domain      | Gate — size ≥ 5% account + thesis ≥ 30 + 1 rule valid → pass                 | cùng file                                                |
| 7  | Domain      | Gate — size < 5% account + thesis ≥ 15 + 0 rule → pass                        | cùng file                                                |
| 8  | Domain      | Gate — size < 5% + thesis < 15 → throw                                          | cùng file                                                |
| 9  | Domain      | Gate — `AccountBalance` null → treat size < threshold, chỉ ép thesis ≥ 15   | cùng file                                                |
| 10 | Domain      | Gate — Vietnamese diacritic thesis "Mua Hoà Phát vì EPS Q1 +35% YoY" = 38 chars → pass (N12 fix) | cùng file                             |
| 11 | Domain      | `AbortWithThesisInvalidation` — từ Ready → Status=Cancelled, rule được append | `TradePlanAbortTests.cs` **NEW**                        |
| 12 | Domain      | `AbortWithThesisInvalidation` — từ InProgress → ThesisInvalidated=true, event raised | cùng file                                       |
| 13 | Domain      | `AbortWithThesisInvalidation` — từ **Executed** (multi-lot partial) → event raised, không throw (B3 fix) | cùng file                          |
| 14 | Domain      | `AbortWithThesisInvalidation` — từ Reviewed/Cancelled → throw                  | cùng file                                                |
| 15 | Domain      | `Restore()` sau Abort — clear `InvalidationRule[*].IsTriggered`                  | cùng file                                                |
| 16 | Domain      | Gate Sell direction — flip sign cho StopHonorRate check (M12 fix)                | cùng file                                                |
| 17 | Application | `CreateTradePlanCommandHandler` persist Thesis đúng                              | `CreateTradePlanCommandHandlerTests.cs`                 |
| 18 | Application | `AbortTradePlanCommandHandler` gọi `AbortWithThesisInvalidation`                | `AbortTradePlanCommandHandlerTests.cs` **NEW**          |
| 19 | Application | `UpdateTradePlanStatusCommandHandler` gọi `MarkReady/MarkInProgress` → gate áp dụng (M5 fix) | handler tương ứng                              |
| 20 | Application | Auto-transition khi `Status=Executed` ở CreateHandler → gate áp mỗi bước, không skip (Architect Top-3) | cùng file                           |
| 21 | Api         | POST `/trade-plans` thiếu Thesis → 400 `DISCIPLINE_GATE_FAILED`                | integration test                                          |
| 22 | Api         | POST `/trade-plans/{id}/abort` valid → 200 + Plan.Status=Cancelled               | integration test                                          |
| 23 | Api         | POST `/trade-plans` payload có key `reason` (legacy client) → accept, log warning | integration test (deprecation window)                  |
| 24 | Domain      | Legacy — `LegacyExempt=true` + Draft edit → KHÔNG throw                          | `TradePlanLegacyExemptTests.cs` **NEW**                 |
| 25 | Domain      | Legacy — `LegacyExempt=true` + transition Ready → InProgress → throw nếu thesis rỗng | cùng file                                           |
| 26 | Domain      | Legacy whitelist — `UpdateStopLossWithHistory` / `TriggerScenarioNode` / `ExecuteLot` / `AbortWithThesisInvalidation` KHÔNG bị T+3 gate chặn (M4 fix) | cùng file |
| 27 | Infrastructure | Migration idempotent — chạy 2 lần → result giống nhau (B2 fix)                | `TradePlanMigrationTests.cs` **NEW**                    |
| 28 | Application | `DisciplineScoreCalculator` — 0 trade/plan → `overall = null`, label = "Chưa đủ dữ liệu" | `DisciplineScoreCalculatorTests.cs` **NEW**    |
| 29 | Application | `ComputeSlIntegrity` — 10 lệnh lỗ, 9 exitPrice ≥ SL, 1 nới SL underwater → SlIntegrity = 80 | cùng file                              |
| 30 | Application | `ComputeSlIntegrity` — chạm SL đúng kế hoạch KHÔNG bị penalize (StopHonored) | cùng file                                              |
| 31 | Application | `ComputeSlIntegrity` — multi-lot (2 TradeIds): 1 lot chạm SL, 1 lot chưa đóng → chỉ tính lot đóng (M8 fix) | cùng file                  |
| 32 | Application | `ComputeSlIntegrity` — Sell direction flip sign (M12 fix)                       | cùng file                                               |
| 33 | Application | `ComputePlanQuality` — loại trừ plan `LegacyExempt=true`                        | cùng file                                               |
| 34 | Application | `ComputeStopHonorRate` — 13/15 lệnh lỗ exit ≥ SL → rate = 0.87                | cùng file                                               |
| 35 | Application | `ComputeOverall` — 1 sub-metric null → re-normalize weights trên 2 sub còn lại (M11 fix) | cùng file                                    |
| 36 | Application | `ComputeOverall` — cả 3 sub-metric null → overall = null, label = "Chưa đủ dữ liệu" | cùng file                                           |
| 37 | Application | Weighted avg = 0.50·SlInt + 0.30·PlanQ + 0.20·Timeliness, round int              | cùng file                                               |
| 38 | Application | Cache invalidate khi `TradeClosedEvent` raise — next call re-compute (N13 fix) | cùng file                                                |
| 39 | Api         | `GET /me/discipline-score?days=90` trả structure đúng (3 components + 1 primitive + sampleSize + trend) + cache hit lần 2 | integration test |
| 40 | Api         | `GET /me/discipline-score?days=0` → 400 validation error                          | integration test                                          |

Chạy `dotnet test` sau mỗi test — Red → Green → Refactor.

Frontend test: 3-5 spec cho form gate (Bucket=Core missing thesis → submit button disabled + toast). Karma.

Docs cập nhật cuối V1:

- `docs/architecture.md` — thêm `AbortTradePlanCommand`, `TradePlanDisciplineException`.
- `docs/business-domain.md` — update TradePlan entity map với Thesis/InvalidationCriteria/Bucket.
- `docs/features.md` — section mới "Thesis-driven Plan Creation".
- `frontend/src/assets/CHANGELOG.md` — v2.46.0 (hoặc version thực tế khi ship).
- `frontend/src/assets/docs/user-guide-*.md` — hướng dẫn viết thesis tốt + ví dụ 4 câu.
- `docs/project-context.md` — tick V1 done trong improvement plan.

### Phase V2 — Review nudge + non-price triggers (5-7 ngày)

**Mục tiêu:** app chủ động nhắc review thesis khi tới `CheckDate` / `ExpectedReviewDate`.

Scope (chia thành V2.1 done + V2.2/V2.3 defer):

**V2.1 — ✅ DONE 2026-04-23 (PR #94 squash `304421dc`):**

- ✅ Endpoint `GET /api/v1/me/thesis-reviews/pending` — `GetPendingThesisReviewsQuery` + DTOs. Filter Ready/InProgress + skip LegacyExempt + sort DESC theo DaysOverdue. Timezone VN UTC+7 day-granularity qua `TimeZoneInfo`.
- ✅ FE trang `/pending-reviews` — standalone component, urgency color cards (amber 0-2d / red ≥3d), badge trigger type Việt hóa.
- ✅ Dashboard widget link count badge "🔔 [N] Plan cần review lý do đầu tư →"; widget ẩn khi `totalPlans=0`.
- ✅ Locale vi-VN global (`main.ts` register `localeVi` + `LOCALE_ID` provider).
- ✅ Việt hóa "Thesis" → "Lý do đầu tư" trong UI 4 files (giữ TypeScript identifiers).
- ✅ 10 handler tests mới, 146 Application tests total pass. Review fixes từ 3-agent (timezone + perf + widget flash).

**V2.2 — defer (sau trial window 1-2 tuần):**

- `ThesisReviewService` Hosted Service cron daily 07:00 Asia/Ho_Chi_Minh → tạo `AlertHistory`/Notification cho plan due. User mở app thấy nudge mà không cần tự check `/pending-reviews`.
- Mở rộng `ScenarioNodeConditionType` với `ThesisCheckDue` (tuỳ chọn — tái dùng Scenario Playbook hay dùng AlertRule mới, quyết định sau).
- Tests TDD đầy đủ cho service.

**V2.3 — defer (sau V2.2):**

- Domain event handler `TradePlanThesisInvalidatedEvent` → ghi vào P7 timeline:
  - `BehavioralPattern = DisciplinedAbort` khi user abort đúng trigger (EarningsMiss thực sự miss, TrendBreak thực sự gãy).
  - `BehavioralPattern = SunkCostHold` khi plan drawdown > 15% mà chưa abort dù có rule due.
- Tests cho pattern detection.

### Phase V3 — Core/Satellite portfolio-level (3-4 ngày, defer)

Scope:

- `Portfolio.CoreTargetPercent` (default 70%).
- Dashboard widget "Core 72% / Satellite 28% — trong target" (kèm alert nếu vượt).
- Tác động lên `PositionSizingService`: Satellite mặc định SL chặt hơn (vd: 1%/capital) vs Core (3%).

### Phase V4 — Drawdown escalation + SunkCostHold pattern (3-4 ngày, defer)

Scope:

- Drawdown ladder 5% / 15% / 30% → force modal review thesis.
- P7 Behavioral Pattern: `SunkCostHold` (giữ lệnh lỗ sâu mà không review) + `DisciplinedAbort`.

---

## Migration

### Data migration (V1) — **Migration-first deploy gate** (R10/B1)

**Critical:** migration CHẠY TRƯỚC khi container code mới start. Không có Bson alias fallback — nếu code mới deploy trước migration, old docs với key `reason` sẽ deserialize `Thesis = null` silently → data loss.

**Deploy pipeline:**

1. Pre-deploy: backup `tradePlans` collection (Mongo Atlas snapshot).
2. Pre-deploy: run migration script (idempotent — xem dưới). Verify count (`db.tradePlans.countDocuments({ legacyExempt: { $exists: true } })` = tổng plans).
3. Deploy new code.
4. Post-deploy smoke: API `GET /api/v1/trade-plans` trả plan cũ, `thesis` field populate đúng.

Script `scripts/migrations/2026-04-XX-tradeplan-thesis-rename.mongo.js`:

```js
// STEP 1: rename reason -> thesis, add new fields, set legacy flag.
// Idempotent: chỉ chạy trên docs chưa migrated (thiếu `legacyExempt`).
db.tradePlans.updateMany(
  { legacyExempt: { $exists: false } },
  [
    {
      $set: {
        thesis: { $ifNull: ["$reason", ""] },
        invalidationCriteria: { $ifNull: ["$invalidationCriteria", []] },
        expectedReviewDate: { $ifNull: ["$expectedReviewDate", null] },
        legacyExempt: true
      }
    },
    { $unset: "reason" }
  ]
);

// STEP 2: placeholder cho thesis rỗng — filter ĐỘC LẬP step 1 (B2 fix).
// Re-run step 2 nếu step 1 crash giữa chừng vẫn đúng.
db.tradePlans.updateMany(
  { thesis: "" },
  { $set: { thesis: "(legacy — thesis không ghi khi tạo, cần bổ sung khi review)" } }
);
```

**Edge cases đã test:**

- Plan với `reason: null` → step 1 `$ifNull` → `thesis = ""` → step 2 fill placeholder. ✅
- Plan với `reason: ""` → step 1 → `thesis = ""` → step 2 fill placeholder. ✅
- Plan với ký tự đặc biệt / diacritic (tiếng Việt) → Mongo xử lý as UTF-8 string, không đụng. ✅
- Plan `isDeleted: true` — migration vẫn chạy (include soft-deleted để data consistency). ✅
- Migration crash giữa chừng rồi re-run — step 1 filter `legacyExempt: { $exists: false }` skip docs đã migrate; step 2 filter `thesis: ""` cover mọi residual rỗng. ✅ (B2 fix)

**Rollback plan:** nếu migration lỗi, restore từ Mongo backup (daily snapshot).

### Backward compatibility

- `Reason` field bị **drop** hoàn toàn ở Mongo và trong code. **KHÔNG có Bson alias** (R10/B1).
- `Notes` field giữ nguyên — free-form ghi chú, không đụng.
- API cũ: client gửi payload có key `reason` → **accept + map sang `thesis` trong 1 release** (deprecation warning log). Sau release tiếp theo chỉ accept `thesis`. Implement ở controller level, không Bson.
- Gate chỉ check khi transition state, tất cả field mới nullable ở DTO input cho plan mới — cho phép save Draft từ từ.

---

## Phân tích sâu — Legacy plan strategy

> Phần này giải đáp câu hỏi R2 (2026-04-23): xử lý plan đã tồn tại trước V1 ship sao cho không block workflow cũ nhưng vẫn đẩy được chất lượng data.

### Vấn đề

Mọi plan tạo trước ngày V1 deploy có `Thesis` rỗng hoặc rất ngắn (vì field gốc `Reason` không ràng buộc). 2 trường phái:

- **Strict từ V1:** mọi plan cũ muốn chuyển state đều phải fill thesis mới → chất lượng data sạch nhanh, nhưng user edit plan cũ lần đầu sau deploy = bị chặn đột ngột = friction cao.
- **Lax vĩnh viễn:** legacy exempt mãi → UX mượt, nhưng phá vỡ giả định "mọi plan đều có thesis" cho analytics/AI sau này.

### Đề xuất: **Graduated deprecation** (4 mốc thời gian — rút gọn theo R4)

| Mốc                       | Plan legacy (Draft/Ready/InProgress) | Plan mới     | Banner UI                                                                              |
| -------------------------- | ------------------------------------- | --------------- | -------------------------------------------------------------------------------------- |
| **T+0 (V1 ship)**    | Exempt khi edit Draft; gate khi transition | Gate đầy đủ | Banner vàng: "Plan legacy — thesis chưa đầy đủ. Bổ sung khi review."             |
| **T+1 tháng**        | Như trên + **banner đỏ soft warning**: "Còn 60 ngày để bổ sung thesis." | Như trên | Banner đỏ nhẹ.                                           |
| **T+3 tháng (hard)** | **Gate áp cho edit field plan-level** (Symbol, Direction, Entry, Qty, Target, Strategy, etc.). **KHÔNG chặn risk-control methods** (M4 fix whitelist): `UpdateStopLossWithHistory`, `TriggerScenarioNode`, `TriggerExitTarget`, `ExecuteLot`, `AbortWithThesisInvalidation` vẫn hoạt động để user cứu được lệnh đang lỗ. | Như trên | Modal cứng khi edit plan-level field; risk-control vẫn free. |
| **T+6 tháng**        | Xoá cờ `LegacyExempt`. Plan chưa fill thesis → mark `archived-no-thesis`, ẩn khỏi analytics mặc định. Dashboard widget filter out. | Như trên | (không hiện)                                           |

### Ma trận xử lý theo state × legacy flag (T+0 → T+3)

| State     | LegacyExempt=true                                                                         | LegacyExempt=false (plan mới)                     |
| --------- | ----------------------------------------------------------------------------------------- | -------------------------------------------------- |
| Draft     | **Không gate edit**. Transition → Ready/InProgress: **gate bình thường**, ép fill thesis. | Gate đầy đủ ngay từ edit.                       |
| Ready     | Không ép fill khi edit field khác; transition → InProgress: **gate bình thường**. Banner nhắc. | Gate đầy đủ.                                     |
| InProgress | Không chặn exit/abort/execute. Banner đỏ nhắc bổ sung thesis khi review.                  | (Plan mới không đến được InProgress mà thiếu thesis — đã gate ở Ready.) |
| Executed  | Read-only. Khi chuyển Campaign Review → có field "Lesson learned" (P0.7 đã có) — đủ learning loop. | (Tương tự.)                                     |
| Reviewed  | Read-only vĩnh viễn.                                                                     | Read-only vĩnh viễn.                             |
| Cancelled | Read-only.                                                                                | Read-only.                                         |

### Edge cases cần test

1. **Plan legacy Draft, user edit thesis nhưng không đủ 30 chars, save**: cho phép (chỉ save Draft, chưa transition). Banner vàng nhắc.
2. **Plan legacy Ready, user thử chuyển InProgress, thesis rỗng**: chặn (gate). Banner đỏ + toast.
3. **Plan legacy InProgress, user bấm Abort (thesis sai)**: cho phép, vì Abort là hành động học — không ép fill thesis gốc. Chỉ ép fill `InvalidationRule` lúc abort (trigger + detail).
4. **Plan legacy InProgress, user mở Campaign Review (Executed → Reviewed)**: cho phép review, campaign review có riêng "Lesson learned" — không cần fill thesis gốc.
5. **Plan legacy Reviewed đọc từ analytics sau T+12**: `archived-no-thesis`, mặc định filter out khỏi báo cáo "top thesis dẫn đến win/loss".

### Alternative considered (đã reject)

| Phương án                              | Ưu                              | Nhược                                                                                                                           | Lý do reject                                                      |
| ----------------------------------------- | -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| **A. Hard cutoff từ V1**          | Data sạch ngay, không mess   | Block user edit plan cũ lần đầu sau deploy → friction mạnh, user phẫn nộ                                              | Vi phạm triết lý "gate đúng lúc" — cũ bỗng bị phạt dù không vi phạm khi tạo. |
| **B. Lax vĩnh viễn**             | Zero friction                    | 6 tháng sau vẫn 40% plan thiếu thesis → analytics/pattern detection không dùng được; giả định "thesis đầy đủ" vỡ. | Không đạt mục tiêu long-term data quality.                           |
| **C. Graduated deprecation** (chọn) | Cân bằng, minh bạch timeline | Cần ghi rõ lịch + code cờ + banner — nhiều chi tiết                                                                      | Chọn vì rõ ràng cho user, có metric đo success.                   |
| **D. Migration prompt 1 lần khi login**  | Forced one-time fill           | UX kinh hoàng nếu user có 50+ plan legacy                                                                                      | Reject — không scalable.                                            |

### Đo lường thành công

- **Tháng 1:** % plan mới (post-migration) có `thesis.length ≥ 30` khi chuyển Ready — target **≥ 95%** (gate cứng nên gần 100%).
- **Tháng 2:** % plan legacy active (Draft/Ready/InProgress) đã được user chủ động fill thesis — target **≥ 60%** (rút gọn deadline → cần tăng tốc).
- **Tháng 3:** % plan legacy active chưa fill — target **≤ 20%** (còn lại là plan đã Reviewed/Cancelled, không cần fill). Hard gate kích hoạt.
- **Số lần user bị gate chặn transition** (log warning): nếu > 10% attempts trong tháng đầu → UX helper text chưa đủ rõ, cần cải thiện placeholder/example.
- **Số lần user dùng button "Thesis sai, cắt"** vs `Cancel()` thường — tỉ lệ Abort cao cho thấy user đang dùng kỷ luật thesis, KPI trung tâm của feature. Hiển thị trực tiếp trên Dashboard widget (§D6).

### Triển khai thực tế của cờ `LegacyExempt`

- Property `bool LegacyExempt { get; private set; } = false;` trên `TradePlan`.
- Migration set `true` cho mọi plan hiện có.
- `ReadyToArm()` check: `if (LegacyExempt && Status == Draft) return; // skip gate`.
- T+3 tháng: release cập nhật, đổi logic `ReadyToArm()` bỏ nhánh Draft exempt. Plan legacy khi mở form sẽ thấy modal "Bổ sung thesis để tiếp tục edit".
- T+6 tháng: migration thứ 2 — `db.tradePlans.updateMany({ legacyExempt: true, thesis: /^\(legacy/ }, { $set: { archivedNoThesis: true } })`. UI analytics filter mặc định `archivedNoThesis: false`. Dashboard widget (§D6) cũng filter out để không kéo điểm.

### Rủi ro của graduated deprecation

- **User bỏ ứng dụng ở T+3 khi bị gate đột ngột** → mitigation: banner đỏ từ T+1 + modal countdown 30/14/7/1 ngày trước deadline, link hướng dẫn viết thesis tốt. Vì solo-app (chính bạn là user), rủi ro này nhẹ hơn multi-user app.
- **Quên xoá cờ** (ở T+6) → không gây lỗi chức năng, nhưng analytics nhiễu. Mitigation: lên calendar reminder T+6 trong `docs/project-context.md` mục "Scheduled migrations".

---

## Rủi ro & lưu ý

- **Friction "gate cứng" quá nhiều** — user thấy phiền → viết thesis qua loa để pass. Mitigation:
  - Placeholder gợi ý (ví dụ thesis tốt): "Mua EVF vì EPS Q1 dự phóng +35% YoY, ROE > 18%, tín dụng bán lẻ tăng" → user copy-adjust.
  - Không validate chất lượng nội dung (quá khó tự động); chỉ validate độ dài + có trigger. Chất lượng là trách nhiệm user.
  - Bucket Satellite cho scalping — giữ plan nhẹ, không ép rule.
- **Legacy plan exempt vĩnh viễn** — đã giải quyết qua graduated deprecation (xem §"Phân tích sâu — Legacy plan strategy"). Timeline cụ thể T+0 → T+12 tháng.
- **Thesis timeout nhầm** — nếu user set `ExpectedReviewDate` quá gần → nudge liên tục, gây mệt. Mitigation: default 60 ngày theo TimeHorizon (Short=14, Medium=60, Long=180), user có thể override.
- **Xung đột với Scenario Playbook** — InvalidationCriteria (business condition) ≠ ScenarioNode (price/time condition). V1 giữ 2 cơ chế độc lập. **V2 de-dup rule** (M10 fix): `ThesisReviewService` skip plan có `ExitStrategyMode=Advanced` + `TimeElapsed` scenario trong ±2 ngày của `CheckDate` (tránh 2 notification cho cùng event). Race condition `SellAll` scenario + `AbortWithThesisInvalidation` đồng thời: resolve qua domain event ordering (scenario fire trước, abort fire sau nếu scenario chưa đóng trade hết).
- **AbortWithThesisInvalidation vs Cancel cũ** — 2 method khác intent:
  - `Cancel()` = không muốn vào lệnh nữa (đổi ý, không liên quan thesis).
  - `AbortWithThesisInvalidation()` = thesis đã sai, cần log trigger + detail để học.
  - UI phân biệt: button "Huỷ plan" (Draft/Ready, không ghi lý do) vs "Thesis sai, cắt" (Ready/InProgress/**Executed**, modal bắt buộc).
- **Size-based gate edge case** — `AccountBalance` null → treat strict mode OFF (chỉ ép thesis ≥ 15). Nếu user cố tình không nhập AccountBalance để né strict → thay thế bằng fallback: nếu `AccountBalance` null thì dùng portfolio value (query từ Portfolio snapshot gần nhất). V1 chấp nhận limitation.
- **Timezone `ThesisReviewService`** (V2 rủi ro) — cron 07:00 Asia/Ho_Chi_Minh. So sánh `CheckDate ≤ today + 2` phải dùng `TimeZoneInfo.ConvertTimeFromUtc` với `"SE Asia Standard Time"`, tránh off-by-one khi UTC tối hôm trước.

---

## Tiêu chí hoàn thành (Definition of Done — V1)

- [ ] 40 tests liệt kê §Phase V1 pass (Red → Green → Refactor, `dotnet test` sau mỗi test).
- [ ] Plan mới không thể rời Draft nếu vi phạm size-based gate (verify qua smoke test 5 scenario).
- [ ] API `POST /trade-plans/{id}/abort` hoạt động trên state Ready/InProgress/**Executed** (multi-lot partial), raise event.
- [ ] Migration script chạy idempotent trên DB dev + stage — chạy 2 lần → kết quả giống nhau.
- [ ] Migration-first deploy: verify Mongo đã rename `reason → thesis` TRƯỚC khi container code mới start.
- [ ] Legacy plan vẫn update được ở Draft (không bị gate chặn).
- [ ] Legacy plan T+3: gate block edit plan-level fields NHƯNG whitelist risk-control methods (`UpdateStopLossWithHistory`, `TriggerScenarioNode`, `ExecuteLot`, `AbortWithThesisInvalidation`).
- [ ] FE form hiển thị section "Kỷ luật mua" + size indicator live + placeholder gợi ý.
- [ ] FE form: input Qty/EntryPrice đổi → thesis min length update realtime (strict ≥30 khi size ≥ 5%, loose ≥15 khi nhỏ hơn).
- [ ] Button "Thesis sai, cắt" xuất hiện đúng state (Ready/InProgress/Executed), mở modal đúng.
- [ ] Docs đã cập nhật: `architecture.md`, `business-domain.md`, `features.md`, `CHANGELOG.md`, user guide.
- [ ] Manual verification: tạo 1 plan size ≥ 5% đầy đủ thesis + rule → Ready → Abort với `EarningsMiss` → kiểm tra event + audit pattern.
- [ ] Dashboard widget "Kỷ luật Thesis" hiển thị đúng composite + **3 sub-bars (SL-Integrity 50% / Plan Quality 30% / Review Timeliness 20%)** + **1 primitive Stop-Honor Rate** + trend sparkline + sample size display.
- [ ] Widget đặt cạnh Risk Alert trong Dashboard Cockpit (không đụng layout khác).
- [ ] Widget period dropdown đổi 7/30/90/365 ngày, data fetch đúng, **default 90**.
- [ ] Cache 5 phút hoạt động + invalidate khi `TradeClosedEvent/PlanReviewedEvent/TradePlanThesisInvalidatedEvent` raise.
- [ ] Chạm SL pre-committed KHÔNG bị penalize (StopHonorRate tăng khi có lệnh chạm SL đúng plan).
- [ ] Nới SL khi underwater BỊ penalize (tạo scenario test: edit SL khi đang lỗ → `SlWidenedRate` tăng → SL-Integrity giảm).
- [ ] Multi-lot plan: SL-Integrity tính per-lot theo từng `TradeId`, không average.
- [ ] Sell direction: formula flip sign hoạt động đúng.
- [ ] Null sub-metric: weighted avg re-normalize weights, không return NaN.
- [ ] Test manual cuối: tạo 3 plan mới với thesis đầy đủ + 1 abort đúng pre-committed SL → widget composite ≥ 80 sau 1 ngày.

---

## Thứ tự triển khai đề xuất

1. **V1 (11-13 ngày)** — Foundation + Hybrid Dashboard widget. Tách thành 4 sub-commit:
   - 1a. Domain + tests (3-4 ngày): entity rename, value object, enum, event, gate fold vào MarkReady/MarkInProgress, size-based logic, 16 domain tests (#1-16). **Pre-step:** đọc entity để lấy đúng tên method transition hiện có.
   - 1b. Application + Api + migration (2-3 ngày): command rename (thesis param, AbortTradePlanCommand), controller endpoint `/abort` + legacy-reason accept, migration-first idempotent script + rollback plan, 7 tests (#17-23, #27).
   - 1c. Dashboard widget backend Hybrid (**2 ngày**, giảm từ v2 do drop Disposition/Odean): `DisciplineScoreCalculator` với 4 private methods, multi-lot per-lot matching, Sell direction flip, cache invalidation on events, 11 tests (#28-38).
   - 1d. Frontend (2-3 ngày): form section (thesis + invalidation + size indicator + review date, drop Bucket toggle), abort modal, service, widget (composite + 3 sub-bars + 1 primitive + sample size), spec tests (3-5 Karma specs).

**Sub-commit dependencies:**

- 1a → 1b (migration script dựa trên entity schema).
- 1b → 1c (calculator cần repo có field `Thesis`).
- 1a/1b → 1d (frontend cần API shape final).

**Ship gate sub-commit:** chạy `dotnet test` sau mỗi sub-commit, không merge nếu test đỏ. Migration deploy-first trước khi bất kỳ code mới nào chạy production.
2. **V2 (5-7 ngày)** — sau khi V1 ship + user quen UX. Review lại tỉ lệ thesis được viết đầy đủ (> 80%?) qua widget trước khi đầu tư V2. V2 scope: thesis review nudge service + trang `/pending-reviews` + drill-down `/discipline-report`.
3. **V3 / V4** — defer, review nhu cầu sau 1 tháng dùng V1+V2.

**Tổng V1+V2:** ~2.5-3 tuần. V1 độc lập, có thể ship trước và tạm dừng giữa chừng.

---

## Research findings — Discipline Score v2 (2026-04-23)

> Hai sub-agent nghiên cứu độc lập về stop-loss discipline metrics (Academic vs Practical). Cả hai **hội tụ** vào cùng 1 flaw nghiêm trọng của công thức v1 (§D6).

### Đồng thuận của 2 agents

1. **"Held-to-SL" bị penalize SAI.** Chạm SL đã pre-commit ≠ thiếu kỷ luật, mà là **discipline đúng nghĩa** (pre-commitment device, Thaler-Shefrin self-control model). Công thức v1 đang train user **abort sớm trước khi giá chạm SL** để gỡ điểm → ngược với mục tiêu.
2. **Plan Completion 40% quá cao.** Đo "stated intent / documentation", không đo "revealed preference / behavior". Behavioral finance đo hành vi thực tế (Odean 1998).
3. **Thiếu asymmetry check.** Không phát hiện bias "bán lời sớm, giữ lỗ lâu" (disposition effect).

### Khác biệt của 2 agents

| Điểm | Agent A (Academic) | Agent B (Practical) |
| ----- | --------------------- | ---------------------- |
| Metric gốc đề xuất | Odean PGR−PLR (rigorous) | Stop-Honor Rate + Rule-Violation Count (retail-friendly) |
| Review Timeliness | Drop khỏi score, surface thành badge riêng | Keep — unique differentiator cho swing trader |
| Format hiển thị | Composite score duy nhất | Primitives (specific) > composite (abstract) |

### Công thức v2 (synthesis)

| Sub-metric                                        | Weight | Công thức / Logic                                                                                                          |
| --------------------------------------------------- | ------: | ------------------------------------------------------------------------------------------------------------------------ |
| **SL-Integrity** (replace "Abort/Cancel") | 35%    | `StopHonorRate` (exit ≥ planned SL) **trừ** `% trades có SL bị nới rộng khi underwater` (dùng `StopLossHistory` audit log) |
| **Disposition Asymmetry** (mới)           | 30%    | `50 + 50·(PLR − PGR)` clamped [0,100]; PLR = loss realized / (realized + paper), PGR ngược lại                         |
| **Plan Quality** (giảm từ 40%)           | 20%    | % plan có thesis + invalidation đầy đủ                                                                               |
| **Review Timeliness** (keep per Agent B) | 15%    | % plan review đúng hạn                                                                                                  |

**Thêm 2 primitives** hiển thị cạnh composite:

- **Stop-Honor Rate** — "87% lệnh lỗ tôn trọng SL gốc"
- **Rule-Violation Count (rolling 20 lệnh)** — sparkline "Tuần này 2 lần nới SL"

### Data requirements (đều có sẵn, không cần entity mới)

- `TradePlan.StopLossHistory` (đã tồn tại) — detect SL widened underwater.
- Portfolio snapshots (đã có) — compute paper gain/loss path cho Odean.
- Trade entry/exit timestamps — OK.

### Nguồn academic (Agent A)

- Odean, T. (1998). "Are Investors Reluctant to Realize Their Losses?" *Journal of Finance*, 53(5), 1775-1798.
- Weber, M. & Camerer, C. (1998). "The Disposition Effect in Securities Trading." *J. of Economic Behavior & Organization*.
- Shefrin, H. & Statman, M. (1985). "The Disposition to Sell Winners Too Early and Ride Losers Too Long." *Journal of Finance*.
- Thaler, R. & Shefrin, H. (1981). "An Economic Theory of Self-Control." *Journal of Political Economy* (pre-commitment device framework).
- Kaminski, K. & Lo, A. (2014). "When Do Stop-Loss Rules Stop Losses?" *Journal of Financial Markets*.

### Nguồn practical (Agent B)

- Edgewonk 2.0 — Entry/Exit/Trade Efficiency + Tiltmeter (user-tagged behavioral flags).
- Tradervue — Mistakes & Tags, "Cost of broken rules" (P&L impact per violation tag).
- TraderSync — Mistake Analysis + "Rule adherence %" + MAE vs planned stop.
- Trademetria, Chartlog, TradesViz — R-multiple distribution as stop-discipline proxy.
- r/Daytrading, SMB Capital, Brian Shannon YouTube — `% stop honored`, MAE/planned_stop, violations per week.

### Tình trạng

- [x] User chốt 2026-04-23 (round 3): weights v2 35/30/20/15, ship v2 ngay V1, add 2 primitives.
- [x] User chốt 2026-04-23 (round 4, post multi-agent review): **Hybrid** — drop Disposition/Odean + Rule-Violation 5-flag, re-weight **50/30/20**, default **90 ngày**, **size-based gate** thay Bucket.
- [x] §D6 đã rewrite với Hybrid formula (3 sub-metrics + 1 primitive).
- [x] Bảng tests đã cập nhật (40 tests total, bao gồm multi-lot + Sell direction + legacy whitelist + migration idempotency + cache invalidation).
- [x] Timeline V1 điều chỉnh **11-13 ngày** (hybrid: giảm complexity Disposition/Odean nhưng thêm fix 12 Must-fix items).
- [ ] Sẵn sàng bắt đầu V1.1a (TDD Domain tests).

---

## Tham chiếu case study thị trường Việt Nam

> Phần này để giải thích **vì sao các placeholder ví dụ được viết như vậy**, và cho user hiểu ngữ cảnh TTCK VN khi điền thesis/invalidation. Biên tập lại từ các sự kiện 2020-2025 mà nhà đầu tư cá nhân VN đều nhớ.

### R1. Case điển hình cho `EarningsMiss`

| Ticker / Sự kiện                              | Mô tả                                                                                   | Bài học cho InvalidationRule                                                            |
| --------------------------------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| **HPG 2022-2023** (thép)                | Quý 3/2022 lỗ đầu tiên sau 13 năm, giá HRC thế giới rơi mạnh. Lợi nhuận sụt > 80% YoY. | Thesis cyclical cần rule `EarningsMiss` gắn chặt giá HRC / biên lợi nhuận (gross margin).   |
| **NVL 2022-2023** (BĐS)                | Chậm thanh toán trái phiếu, trích lập dự phòng đột biến Q4/2022. Cổ phiếu −85%. | Thesis BĐS leverage cao phải có rule "nợ ngắn hạn / tổng nợ > 60%" hoặc "trái phiếu đáo hạn > tiền mặt". |
| **Ngân hàng đầu 2024**               | Nhiều bank trích lập dự phòng xử lý nợ xấu BĐS → lợi nhuận hụt consensus.      | Thesis bank nên có rule "NPL tăng > 50bps" + "chi phí dự phòng > 30% LN gộp".         |
| **VHM, PDR 2023**                         | Không chia cổ tức như nghị quyết ĐHCĐ, hoãn phát hành trái phiếu.                 | Rule "không chia cổ tức đúng nghị quyết" là invalidation quan trọng với BĐS.            |

### R2. Case điển hình cho `TrendBreak`

| Ticker / Sự kiện                               | Mô tả                                                                              | Bài học                                                                                                                 |
| ---------------------------------------------- | ----------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| **VN-Index T4/2022**                     | Break 1400 với volume cao, khởi đầu downtrend đến 1080. Tự doanh CTCK xả ròng. | Rule "VN-Index break 1200/1000 với volume > 2× TB" — dùng cho plan index-sensitive (BCs, CK).                      |
| **Khối ngoại bán ròng Q1-Q3/2024**     | Bán ròng liên tục trên sàn chính, áp lực bearish kéo dài.                  | Rule "khối ngoại bán ròng > 10 phiên liên tiếp kèm giá giảm > 10%" — mạnh hơn tín hiệu technical thuần.   |
| **VND, SSI Q2/2024**                      | Gãy MA200 kèm volume bán đột biến sau vụ VNDirect bị hack dịch vụ.        | Rule technical cần đi kèm catalyst thực, không chỉ giá rơi ngẫu nhiên.                                               |
| **Smallcap penny T4/2024**               | Breakdown vùng tích lũy 6 tháng, giảm 40-60% chỉ trong 3 tuần.                 | Rule "break vùng tích lũy dài hạn" + "volume bán phiên đầu > 3× TB" chốt cắt sớm ở penny.                  |

### R3. Case điển hình cho `NewsShock`

| Sự kiện                                             | Năm   | Ticker bị ảnh hưởng   | Bài học                                                                                                 |
| ----------------------------------------------------- | -------- | -------------------------- | ---------------------------------------------------------------------------------------------------------- |
| **Chủ tịch FLC, ROS bị khởi tố**            | 2022     | FLC, ROS, HAI, AMD, KLF    | Rule "chủ tịch / CEO bị điều tra/khởi tố" là red flag tuyệt đối với midcap.                    |
| **Tân Hoàng Minh bỏ cọc đất Thủ Thiêm** | 2022     | TNH, DIG, CEO, toàn BĐS   | Rule "chủ doanh nghiệp bị điều tra hình sự" + "cổ đông lớn bán phá giá trên sàn".            |
| **Vụ Vạn Thịnh Phát, SCB**                  | 2022-2023 | SCB, các bank liên quan | Rule "bị điều tra hệ thống" + "kiểm toán từ chối xác nhận BCTC" — khác với tin thông thường. |
| **VNDirect bị tấn công mạng**             | 2024     | VND, SSI, CTS             | Rule "sự cố vận hành > 2 ngày liên tiếp" cho công ty nền tảng công nghệ/tài chính.           |
| **Dầu khí — sự kiện địa chính trị**     | 2022     | GAS, PLX, PVD, PVS        | Rule "giá dầu Brent đảo chiều vượt ± 20%" cho thesis năng lượng.                                   |
| **Vinpearl Air dừng**                        | 2020     | VIC (nhẹ, short-term)      | Đây là case mà chính doanh nghiệp tự áp dụng kỷ luật — dùng làm philosophy anchor cho toàn plan.    |

### R4. Case điển hình cho `ThesisTimeout`

| Ticker / Chu kỳ                                | Mô tả                                                                                   | Bài học                                                                                      |
| ----------------------------------------------- | ---------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| **Ngành thép 2019-2020**                | Chu kỳ downcycle kéo dài 15 tháng, thanh khoản thấp, không có catalyst.     | Rule `ThesisTimeout` với ngành cyclical nên đặt 6-12 tháng, không phải 90 ngày.          |
| **Chứng khoán 2022-2023**                | Sau peak Q4/2021, CK broker mất nửa giá trị, sideways > 1 năm.                 | Thesis "phục hồi ngành CK" cần CheckDate 12-18 tháng, kèm rule "thanh khoản thị trường < X". |
| **Penny sideways bất tận**            | Nhiều mã penny sau pump 2021 rơi về vùng 1-3k, thanh khoản < 10% trước đây. | Plan penny phải có `ThesisTimeout` ngắn (30-60 ngày) — không cho thesis "chờ sóng về".     |

### R5. Case điển hình cho `Manual`

Các tình huống **tự nhận biết** (không đo tự động được):

- **FOMO cuối sóng:** VN-Index tăng 30% trong 3 tháng (như Q1/2024), bạn mua vì "không muốn bỏ lỡ" → rule manual self-check.
- **DCA xuống vô tội vạ:** position ban đầu 10% portfolio, sau 3 lần DCA thành 30% nhưng giá vẫn giảm → vượt plan size, phải self-invalidate.
- **Tin room Zalo/Telegram lấn át phân tích:** bạn bắt đầu check giá theo "hô" thay vì theo thesis gốc → dấu hiệu rõ ràng nên cắt.
- **Cảm xúc thay thế phân tích:** khi mở báo cáo công ty mà **không còn phân tích sâu** mà chỉ scroll tìm "tin tốt gì không" → đã mất thesis.
- **Không trả lời được 4 câu nếu hỏi lại:** sau 30 ngày, nếu bạn không tự trả lời được "tại sao mua?" mà không xem ghi chú → thesis đã mờ, nên đóng.

### R6. Ngữ cảnh macro đặc thù VN (dùng cho thesis market-wide)

| Ngữ cảnh                                        | Liên quan thesis                                                                      |
| ------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| **Lãi suất SBV**                           | Thesis ngân hàng / BĐS / chứng khoán phải có rule "SBV tăng lãi suất điều hành > 50bps". |
| **Room ngoại hết** (VN30, bluechip nhạy cảm) | Thesis "khối ngoại mua ròng duy trì" vỡ khi room hết.                              |
| **T+2 margin call pattern** (từ 2022)      | Với plan ngắn hạn, rule "margin call trong ngành dâng cao" là invalidation.              |
| **Upgrade/downgrade MSCI/FTSE EM**              | Thesis dài hạn bluechip cần rule "đánh giá index bị downgrade".                     |
| **Nghị quyết / chính sách đột biến**      | Rule "chính sách thuế/tín dụng thay đổi bản chất ngành" (VD: siết tín dụng BĐS 2022). |

### Kết luận

Đa số case VN có **catalyst rõ ràng** (khởi tố, trái phiếu vỡ, BCTC quý) → rất phù hợp với enum 5 trigger. `Manual` là escape hatch cho self-awareness — không đo được tự động nhưng quan trọng nhất. Placeholder UI ở frontend (§V1 Frontend) đã được calibrate theo các case trên.

**Mở rộng sau (V3+):**

- Tích hợp event crawl (đã có `VietstockEventProvider` từ P7.8) để tự động đề xuất `CheckDate` = earnings release date.
- AI suggest invalidation rules dựa trên thesis + ngành (vd: thesis banking → auto-gợi ý rule NPL/LDR). Cần prompt engineering kỹ để không lùa user viết rule giả tạo.

---

## Tham chiếu

- Case study nguồn: tin tức Vinpearl Air 2019-2020 (không cần link — dùng làm philosophy anchor).
- Triết lý gốc: [docs/project-context.md — Priority: risk management & planning](../project-context.md).
- Entity hiện tại: [src/InvestmentApp.Domain/Entities/TradePlan.cs](../../src/InvestmentApp.Domain/Entities/TradePlan.cs).
- Form hiện tại: [frontend/src/app/features/trade-plan/trade-plan.component.ts](../../frontend/src/app/features/trade-plan/trade-plan.component.ts).
- Tiền lệ plan structure: [docs/plans/multi-user-access-plan.md](multi-user-access-plan.md).
- Post-trade learning loop liên quan: [docs/features.md — P1 Post-Trade Review](../features.md).
