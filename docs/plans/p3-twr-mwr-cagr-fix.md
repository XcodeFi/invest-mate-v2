# P3 — Fix TWR / MWR / CAGR calculations

**Status:** Planned, not started
**Discovered:** 2026-04-19 during PR #70 review (Capital hero cards)
**Related:** [`p2-capital-current-vs-initial.md`](done/p2-capital-current-vs-initial.md) — seed flow exclusion was introduced but reveals these deeper bugs

---

## 1. Problem

Dashboard đang hiển thị các chỉ số return không tin cậy:

| Metric | Current display | Expected (with +4.09% total return, ~252M capital) |
|---|---|---|
| TWR | **+8,994,746.32%** | ballpark vài % / năm |
| CAGR | **−21.5%** | positive (portfolio đang có lãi) |
| MWR | chưa check, nhưng cùng service → nghi ngờ sai |

User portfolio thực tế: Vốn hiện tại 252.967.000đ, Tổng tài sản 263.313.350đ (+10.346.350đ, +4.09%). Không cách nào TWR 8.9M% hay CAGR −21.5% đúng.

---

## 2. Root-cause hypotheses

### TWR — `CashFlowAdjustedReturnService.CalculateTWRAsync`

File: [`src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs:37-75`](../../src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs#L37)

**Công thức:** `TWR = Π(1 + R_i) − 1` với `R_i = (V_i − V_{i-1} − C_i) / V_{i-1}`

**Nghi ngờ:**

1. **Division by near-zero `V_{i-1}`** (line 67 chỉ guard `!= 0`, không guard threshold). Nếu một snapshot có `TotalValue ≈ 0` (VD: portfolio mới tạo, seed Deposit nhưng chưa có snapshot nào phản ánh), period return sẽ blow up. Một period với (V − V_prev − C) / ε → huge number → nhân vào TWR → bùng nổ.

2. **Flow attribution boundary**:
   - Filter `f.FlowDate > snapshots[i-1].SnapshotDate && f.FlowDate <= snapshots[i].SnapshotDate`.
   - Flows **trước snapshot đầu tiên** không được count vào bất kỳ period nào → coi như "magic appearance" trong V₀.
   - Kết hợp với seed Deposit giờ bị exclude (Phase 3): seed = 100M được thêm vào snapshot (vì snapshot dùng `InitialCapital + totalFlows` bao gồm seed), nhưng TWR coi seed như giá trị "tự nhiên" có sẵn ở V₀ thay vì cash flow. Thật ra đúng (vì V₀ phải phản ánh opening balance). Nhưng nếu user có **deposit thêm** sớm, trước khi snapshot đầu tiên chạy, khoản deposit đó bị drop khỏi cả V₀ (snapshot lúc đó chưa tồn tại) lẫn filter flow → inflate period return sau đó.

3. **Snapshot staleness / non-trading days**: giá CP cached, snapshot có thể có `TotalValue` đồng nhất giữa các ngày nghỉ → periodReturn = 0 → không sao. Nhưng nếu 1 snapshot fail silently và lưu 0, thì V_prev = 0 → bug #1.

### MWR — `CashFlowAdjustedReturnService.CalculateMWRAsync`

File: [`src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs:81-148`](../../src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs#L81)

**Newton-Raphson IRR** với `npv = −InitialCapital + Σ(−signedFlow/factor) + currentValue/endFactor`.

**Nghi ngờ:**
- `cashBalance = InitialCapital + totalFlows - pnl.TotalInvested` dùng `pnl.TotalInvested` = open-position cost (no fees), cùng bug như dashboard trước fix. Nên `currentValue` lệch bằng đúng số fee.
- 100 iteration có thể không converge với bad initial guess → rate phân kỳ.

### CAGR — `dashboard.component.ts:calculateCagrFromCurve`

File: [`frontend/src/app/features/dashboard/dashboard.component.ts:1340-1360`](../../frontend/src/app/features/dashboard/dashboard.component.ts#L1340)

**Công thức:** `CAGR = (last.portfolioValue / first.portfolioValue)^(1/years) − 1`

**Bug chắc chắn:**
- Không account cho **flows** giữa first và last snapshot. Classic "unadjusted return" problem.
- Ví dụ: first=100M, sau đó deposit +150M, last=250M. Calc ra cagr = 150%/năm. Sai — actual return 0%.
- Ngược lại nếu user rút nhiều, last < first → CAGR âm giả tạo. Giải thích cho **CAGR −21.5%** ở production.

Dashboard nên dùng TWR (đã time-weighted) thay vì tự tính CAGR thô từ endpoints.

### Backend CAGR fallback

File: [`loadBackendCagr()` trong dashboard.component.ts`]

Nếu FE calc fail → gọi backend. Chưa check backend nhưng nếu dùng cùng kiểu last/first → cùng bug.

---

## 3. Investigation plan (trước khi fix)

1. **Dump snapshots** của portfolio đang test:
   ```
   db.PortfolioSnapshots.find({ portfolioId: "..." }).sort({ snapshotDate: 1 })
   ```
   Tìm:
   - Snapshot nào có `TotalValue < 10000đ` (near-zero) → likely culprit cho TWR blow-up.
   - Gap giữa portfolio creation date và first snapshot → flow nào rơi vào gap.

2. **Dump flows** (tất cả) cùng portfolio:
   - Sort by FlowDate. Xem có flow nào FlowDate < first snapshot date không → attribution bug.
   - Check có đúng 1 seed Deposit (isSeedDeposit=true) hay không.

3. **Trace TWR step-by-step**: Thêm logging tạm thời vào `CalculateTWRAsync` — in ra `i, prevValue, currentValue, periodFlows, periodReturn` cho mỗi iteration. Chạy với portfolio bug, thấy ngay period nào sai.

4. **Reproduce in test**: Viết xUnit test với fixture portfolio:
   - InitialCapital 100M, 1 seed flow.
   - Snapshot day 1: value = 100M (cash, no trades).
   - Snapshot day 7: value = 0đ (snapshot fail scenario) — BUG reproducible.
   - Snapshot day 14: value = 105M.
   - Expected: TWR nên robust, không blow up.

---

## 4. Fix approach

### Phase 3.1 — Robust TWR

1. **Threshold guard** thay vì `!= 0`:
   ```csharp
   const decimal MIN_SNAPSHOT_VALUE = 1000m; // below this, likely a bad snapshot
   if (prevValue < MIN_SNAPSHOT_VALUE) continue; // skip this period
   ```

2. **Cap period return** để 1 outlier không phá cả chain:
   ```csharp
   var periodReturn = (currentValue - prevValue - periodFlows) / prevValue;
   if (Math.Abs(periodReturn) > 5m) continue; // >500% single-period = data issue, skip
   ```

3. **Include pre-first-snapshot flows** trong V₀ baseline (không phải làm period return):
   - Khi khởi tạo TWR loop, set `V₀ = snapshots[0].TotalValue − Σ(flows trước snapshot[0])`. Snapshots phản ánh cả user flows đến ngày đó, nên cần subtract để có V₀ "thuần đầu tư" cho period-return formula.
   - Hoặc đơn giản hơn: **skip periods cho đến khi snapshot đầu tiên sau first flow** (buffer period để data settle).

### Phase 3.2 — MWR consistency

1. Dùng `portfolioSummary.TotalInvested/TotalSold` gross (có fee) cho `currentValue` calc, giống fix dashboard.
2. Hoặc: đổi `currentValue = totalAssets` lấy từ tính toán đã có (giống hero card).
3. Newton-Raphson: thêm bounds checking, nếu không converge thì trả 0 + log warning.

### Phase 3.3 — CAGR frontend rewrite

1. **Bỏ `calculateCagrFromCurve`** — không tự tính.
2. **Dùng TWR** (sau khi Phase 3.1 fix) làm CAGR display. Vì TWR đã time-weighted (loại flow effect), chia cho years sẽ cho annualized return đúng.
3. Nếu TWR là total-period return, convert sang annualized:
   ```ts
   const years = diffDays / 365.25;
   const totalReturn = adjustedReturn.timeWeightedReturn / 100; // as fraction
   this.cagrValue = (Math.pow(1 + totalReturn, 1 / years) - 1) * 100;
   ```
   (Tùy vào TWR backend return "total period" hay "annualized" — cần check.)

### Phase 3.4 — Backend CAGR fallback
- Investigate endpoint đang dùng (có endpoint `analytics/.../cagr` không?). Nếu có cùng bug → fix hoặc bỏ.

---

## 5. Validation / acceptance

**Unit tests cần thêm:**
- `CashFlowAdjustedReturnServiceTests.CalculateTWR_NearZeroSnapshot_DoesNotBlowUp`
- `CashFlowAdjustedReturnServiceTests.CalculateTWR_FlowBeforeFirstSnapshot_DoesNotDistortReturn`
- `CashFlowAdjustedReturnServiceTests.CalculateTWR_SingleOutlierPeriod_ClampedOrSkipped`
- `CashFlowAdjustedReturnServiceTests.CalculateTWR_NormalPath_MatchesExpected` (sanity check không regress case bình thường)

**Manual verification:**
- Portfolio đang bị bug: TWR nên hiển thị giá trị hợp lý (say −50%..+200%/năm depending on actual perf), không còn 8.9M%.
- CAGR khớp hướng với P&L (nếu portfolio lãi → CAGR > 0, nếu lỗ → CAGR < 0).
- So với `(totalAssets − currentCapital) / currentCapital` annualized — TWR không nhất thiết bằng, nhưng cùng dấu.

---

## 6. Scope / risks

**Blast radius:**
- Backend service thay đổi, tests mới. Low risk (isolated service).
- Frontend CAGR logic đơn giản hóa. Low risk.
- Có khả năng thay đổi số TWR/CAGR trên dashboard ↔ đúng mục tiêu, không phải regression.

**Rủi ro:**
- Nếu threshold/cap quá aggressive → mất thông tin legitimate (VD: 1 period thực sự +800% do TSLA kiểu gì đó bị skip). Cần test với data thực.
- Snapshot infrastructure có thể lấm lỗi gốc rễ (stale/zero snapshot) — nên cân nhắc fix `SnapshotService` để không ghi snapshot value 0 khi calc fail, thay vì chỉ chống đỡ ở TWR.

**Ước tính effort:** 1-2 ngày (investigation + fix + tests + validation).

---

## 7. Files likely touched

- `src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs` — TWR + MWR math
- `src/InvestmentApp.Infrastructure/Services/SnapshotService.cs` — possibly, if snapshot writes bad values
- `tests/InvestmentApp.Infrastructure.Tests/Services/CashFlowAdjustedReturnServiceTests.cs` — mới hoặc extend
- `frontend/src/app/features/dashboard/dashboard.component.ts` — `calculateCagrFromCurve` rewrite / deprecate
- `docs/business-domain.md` — update công thức TWR/CAGR nếu đổi semantic
- `frontend/src/assets/CHANGELOG.md`

---

## 8. Out of scope

- Thay đổi snapshot schema (add TotalAssets, allocation, etc.) — scope P4 nếu cần.
- Portfolio-level vs aggregate TWR display — hiện tại dashboard chỉ hiện 1 portfolio's TWR (firstPortfolio); có thể cần aggregate TWR sau.
- Backtesting validation — sanity check TWR against well-known periods (covid crash, etc.).
