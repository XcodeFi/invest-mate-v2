# ADR-0009 — Đưa tín hiệu phía vào lệnh vào Decision Queue, và vá suppression bằng tập thứ ba

- **Status:** Accepted
- **Date:** 2026-08-09
- **Related plan:** `docs/superpowers/plans/2026-07-29-decision-queue-buy-opportunity.md`
- **Affected layers:** Application / Api / Frontend

## Context

Decision Queue (ADR-0002) ra đời với ba nguồn — `StopLossHit`, `ScenarioTrigger`, `ThesisReviewDue` — **toàn bộ là phòng thủ/thoát hàng**. MCP tool `get_decision_queue` tự mô tả là *"trả lời câu hôm nay cần quyết gì"*, nhưng về mặt cấu trúc nó không thể chứa một cơ hội mua. Cơ hội chỉ tồn tại dưới dạng một dòng văn bản trong bản tin hằng ngày, nên không được dedupe, không suppress theo ngày, không resolve được.

Song song đó, hai lỗ hổng lộ ra khi rà code:

1. Vị thế **chưa đặt stop-loss** bị `continue` bỏ qua hoàn toàn. Queue rỗng đọc như *"danh mục an toàn"* trong khi thực tế là *"rủi ro chưa đo được"* — sai theo đúng hướng nguy hiểm nhất.
2. `ResolveDecisionCommand.HandleHoldWithJournalAsync` khi không có `TradePlanId` để `portfolioId` null, còn `LoadResolvedTodayAsync` lại lọc bỏ đúng những entry đó khi dựng suppression set. `StopLossHit` luôn có `TradePlanId = null` nên **mọi lần "GIỮ + ghi lý do" trên card stop-loss đều không suppress được** — card hiện lại ngay sau refresh, đúng thứ mà suppression sinh ra để ngăn.

Ràng buộc: không đổi signature MCP tool (tránh regression `inputSchema`), và không phá ngữ nghĩa suppression theo từng danh mục đang được test khoá.

## Options Considered

### Option A — Thay khoá suppression bằng `(symbol, type)`

Bỏ `suppressedPlanIds`, dựng một tập duy nhất `(Symbol, DecisionType)` từ tag `trigger:{Type}` mà `ResolveDecision` đã ghi sẵn.

- **Pros:**
  - Một đường suppression duy nhất, không phải giữ đồng bộ nhiều tập.
  - Không phụ thuộc `PortfolioId` nên tự động vá được bug entry symbol-only.
- **Cons:**
  - Phá `Handle_StopLossHitWithDecisionJournalForDifferentPortfolio_NotSuppressed` — test cố ý giữ ngữ nghĩa *"cùng mã ở hai danh mục là hai quyết định khác nhau"*.
  - Resolve stop-loss ở danh mục A sẽ làm im cảnh báo cùng mã ở danh mục B. Mất thông tin thật.

### Option B — Thêm tập suppression thứ ba, chỉ cho entry symbol-only

Giữ nguyên `planIds` và `symPort`. Thêm `symType` chỉ nhận entry có **cả** `PortfolioId` **và** `TradePlanId` null — đúng và chỉ đúng tập entry đang rơi mất.

- **Pros:**
  - Cộng thêm thuần: không test suppression nào hiện có phải sửa một dòng.
  - Vá đúng bug, không đụng vào ngữ nghĩa per-portfolio đang đúng.
  - Không đổi schema, không thêm field vào `ResolveDecisionCommand`, FE không phải gửi thêm gì.
- **Cons:**
  - Ba tập suppression song song, phải đọc cả ba mới hiểu hết luật.

### Option C — Thêm `PortfolioId` vào `ResolveDecisionCommand`

Sửa tận gốc: FE gửi kèm `portfolioId`, handler ghi vào journal, `symPort` bắt được như thường.

- **Pros:**
  - Sửa đúng nguyên nhân gốc thay vì bù ở phía đọc.
  - Vẫn chỉ hai tập suppression.
- **Cons:**
  - Đổi contract API + FE, rộng hơn phạm vi cần thiết.
  - **Không giải quyết được `BuyOpportunity`** — cơ hội đến từ watchlist, vốn không thuộc danh mục nào, nên `PortfolioId` rỗng theo bản chất chứ không phải do thiếu dữ liệu.
  - Journal đã ghi trong quá khứ vẫn thiếu `portfolioId` → cần backfill.

## Decision

**Chọn Option B.**

Option A đánh đổi một ngữ nghĩa đang đúng để lấy sự gọn gàng — không xứng. Option C sửa gốc nhưng không phủ được `BuyOpportunity`, mà đó lại chính là lý do ta đụng tới suppression ngay từ đầu. Option B vá đúng tập entry đang hỏng, không chạm gì tới phần đang chạy tốt, và mọi test suppression hiện có phải vẫn xanh mà không sửa — đó là tiêu chí nghiệm thu chứ không phải kỳ vọng.

Kèm theo, thêm hai `DecisionType`:

| Type | Điều kiện | Severity | Lý do severity |
|---|---|---|---|
| `BuyOpportunity` | watchlist có `TargetBuyPrice > 0`, giá > 0 và ≤ mục tiêu | `Info` | Cơ hội phải xếp **dưới** mọi rủi ro. Dọn vị thế đang chảy máu trước, mua thêm sau. |
| `MissingStopLoss` | vị thế mở, `StopLossPrice == null`, giá > 0 | `Warning` | Giữ `Critical` mang nghĩa "hành động ngay". Vài vị thế thiếu SL đội lên `Critical` sẽ làm loãng tín hiệu thật. |

## Consequences

**Positive:**

- Queue trả lời được đúng câu nó tự nhận: gồm cả việc nên mua lẫn việc nên phòng thủ.
- Vị thế không có stop-loss không còn vô hình — rủi ro lớn nhất thôi ẩn mình.
- Bug suppression có sẵn được vá cho `StopLossHit`, không chỉ cho type mới.
- Frontend chuyển `typeLabel`/`getActionRoute` sang `Record<DecisionType, …>`: thêm type mới về sau mà quên nhãn sẽ **lỗi biên dịch** thay vì âm thầm dán nhãn sai như bản `if`-fallthrough cũ.

**Negative / Trade-offs:**

- Ba tập suppression song song — nợ nhận thức. Nếu sau này Option C được làm, nên gộp lại còn hai.
- `DecisionSeverity.Info` từng mang chú thích *"reserved cho V2"* nay được kích hoạt; chú thích đó đã sửa.
- `MissingStopLoss` cùng mã ở hai danh mục: resolve một cái sẽ suppress cả hai trong ngày (vì đi qua `symType`). Chấp nhận — nhắc "đi đặt SL" là hành động trên **mã**, và nó quay lại ngày hôm sau nếu vẫn chưa đặt.
- `BuyOpportunity` thêm một lượt gọi giá cho watchlist. Đã giới hạn: chỉ fetch mã **có** mục tiêu mua, batch một lần, có timeout — hỏng thì phần cơ hội vắng mặt, queue vẫn trả.

**Follow-ups:**

- Migration: không có. `ResolveDecisionCommand` đã ghi tag `trigger:{Type}` từ trước nên journal trong prod đã mang sẵn tag — không cần backfill.
- Tests: phủ cả hai type mới, thứ tự sort `Info` xuống dưới, `BuyOpportunity` không dedupe nuốt `StopLossHit` cùng mã, và giá lỗi thì queue vẫn trả các nguồn khác.
- Docs: `architecture.md` (3 → 5 nguồn), `business-domain.md` (luật sinh item), `features.md`, CHANGELOG, hướng dẫn người dùng về "Mục tiêu mua".
- Sau khi ship: đặt `TargetBuyPrice` cho VNM/MSN/BVH và stop-loss cho MWG/HPG/HHV — con số là quyết định đầu tư, code không suy ra được.

## References

- Plan: `docs/superpowers/plans/2026-07-29-decision-queue-buy-opportunity.md`
- Spec: `docs/superpowers/specs/2026-07-29-decision-queue-buy-opportunity-design.md`
- Tiền nhiệm: ADR-0002 (Dashboard Decision Queue) — ADR này mở rộng, không thay thế
- PR: #XX (điền sau khi merge)
