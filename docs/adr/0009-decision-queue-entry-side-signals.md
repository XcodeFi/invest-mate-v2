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

**Chọn B + C kết hợp.** (Sửa lại sau review lần hai — xem §Sửa lại bên dưới.)

Option A đánh đổi một ngữ nghĩa đang đúng để lấy sự gọn gàng — không xứng.

Bản đầu chọn **B đơn thuần**, lập luận rằng Option C "không phủ được `BuyOpportunity`". Lập luận đó sai ở chỗ đặt vấn đề: C **không cần** phủ `BuyOpportunity` — loại đó vốn không thuộc danh mục nào, nên tập `symType` của B là đường đúng cho nó. Hai option giải hai nửa khác nhau của bài toán và bổ sung cho nhau chứ không loại trừ nhau.

Nên quyết định cuối là:

- **C** — `ResolveDecisionCommand` nhận thêm `PortfolioId`, FE gửi `item.portfolioId`, handler ghi vào journal. Nhờ đó `symPort` bắt được `StopLossHit` và `MissingStopLoss` **có phạm vi danh mục**.
- **B** — tập `symType` giữ lại, nhưng **chỉ áp cho item có `PortfolioId` rỗng**. Đây là loại duy nhất `symPort` không thể phục vụ.

### Sửa lại sau review lần hai

Bản B đơn thuần có một lỗi hồi quy nghiêm trọng mà review lần một không bắt được:

`StopLossHit` luôn có `TradePlanId = null`, FE lại chỉ gửi `tradePlanId` + `symbol`, nên `HandleHoldWithJournalAsync` luôn đi nhánh symbol-only và ghi journal **không có** `PortfolioId`. Journal đó rơi thẳng vào `symType`, khiến resolve `StopLossHit` của FPT ở danh mục A **giấu luôn** cảnh báo `StopLossHit` của FPT ở danh mục B suốt ngày hôm đó.

Trước thay đổi, journal ấy không khớp tập nào (chính là bug §1.3) nên thẻ chỉ hiện lại — phiền nhưng an toàn. Sau thay đổi, nó **giấu một cảnh báo stop-loss** — đúng loại tác hại mà ADR này sinh ra để xoá bỏ. Đổi một bug gây phiền lấy một bug gây nguy hiểm là đi lùi.

Test `Handle_StopLossHitWithDecisionJournalForDifferentPortfolio_NotSuppressed` vẫn xanh suốt vì nó dựng journal có `portfolioId` — hình dạng mà production **không thể** tạo ra cho `StopLossHit`. Bản ADR đầu có ghi nhận test này "kiểm thử một trạng thái không đạt tới được", nhưng không truy tiếp hệ quả: nếu production không tạo được hình dạng đó, thì đường production thật đang đi qua `symType` và tràn danh mục.

Bài học: khi phát hiện một test kiểm trạng thái không đạt tới được, phải hỏi tiếp **đường thật đi đâu**, chứ không dừng ở việc ghi chú rằng test đó rỗng nghĩa.

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

- Ba tập suppression song song — nợ nhận thức. `symType` giờ có phạm vi hẹp (chỉ item không danh mục) nên ranh giới rõ hơn, nhưng vẫn phải đọc cả ba mới nắm hết luật.
- `DecisionSeverity.Info` từng mang chú thích *"reserved cho V2"* nay được kích hoạt; chú thích đó đã sửa.
- `ResolveDecisionCommand` có thêm trường `PortfolioId` — thay đổi contract API, nhưng **thuần cộng thêm** và nullable nên client cũ không vỡ (chỉ mất phạm vi danh mục khi suppress, đúng bằng hành vi trước đây).
- Dedupe nay có bảng `DedupeRank` tường minh. `MissingStopLoss` xếp thấp nhất, nên vị thế vừa thiếu SL vừa có kịch bản đã trigger sẽ hiện thẻ kịch bản. Trước khi có bảng này, kẻ thắng do thứ tự `Concat` quyết định và advisory bị nuốt im lặng — không ai chọn hành vi đó cả.
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
