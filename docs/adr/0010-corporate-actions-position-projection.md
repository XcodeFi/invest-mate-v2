# ADR-0010 — Chiếu vị thế qua `PositionBuilder` thay vì sửa `Trade` khi có sự kiện quyền

- **Status:** Accepted
- **Date:** 2026-08-08
- **Related plan:** `docs/superpowers/plans/done/2026-08-08-corporate-actions.md`
- **Affected layers:** Domain / Application / Infrastructure / Api / Frontend

## Context

Danh mục thật có mã trả cổ tức đều (HPG ~30% cổ phiếu/năm, SAB ~5% tiền mặt/năm) nhưng app không biết đến sự kiện quyền. Hệ quả nặng nhất là **lỗ giả tại ngày GDKHQ**: giá tham chiếu bị điều chỉnh giảm ngay (30% cổ tức cổ phiếu → giá giảm 23,08% vì hệ số là `1/1,3`), trong khi cổ phiếu chỉ về tài khoản sau 1–2 tháng. Trong khoảng đó giá trị vị thế bốc hơi 23% dù không có gì xảy ra, kéo theo snapshot, drawdown, VaR sai và **cảnh báo cắt lỗ kích hoạt nhầm**.

Ràng buộc quan trọng: `Trade` ép `Quantity > 0` **và** `Price > 0`, nên cổ tức cổ phiếu (giá 0) và chia tách không thể biểu diễn bằng một `Trade`. Đồng thời toán dựng vị thế đang bị nhân bản ở khoảng 15 service — mỗi service tự `GroupBy(t => t.Symbol)` trên `Trade` thô.

## Options Considered

### Option A — `CorporateAction` bất biến + một `PositionBuilder` duy nhất

Entity mới là nguồn sự thật; một hàm thuần `PositionBuilder.Build(trades, actions, asOf)` dựng vị thế đã điều chỉnh; mọi service gọi vào đó. `Trade` không bao giờ bị sửa.

- **Pros:**
  - Toán nằm một nơi → không lệch số giữa các màn hình; test được độc lập, không cần Moq.
  - `Trade` vẫn là bản ghi gốc khớp sổ công ty chứng khoán → bật/tắt điều chỉnh để đối chiếu được.
  - Xoá sự kiện nhập sai = xoá một bản ghi, mọi con số tự tính lại.
  - Lưu được `ExDate` tách khỏi `SettlementDate` → biểu diễn được trạng thái "chờ về".
- **Cons:**
  - Phải sửa khoảng 15 call-site. Rủi ro lệch số giữa các màn hình trong lúc chuyển đổi dở dang.

### Option B — Sửa thẳng `Trade` cũ (rewrite history)

Khi nhập sự kiện, chạy script cập nhật `Quantity` / `Price` của các trade cũ.

- **Pros:**
  - Không phải sửa service nào; mọi con số đúng ngay lập tức.
- **Cons:**
  - Mất dữ liệu gốc, không rollback được, không xoá được sự kiện nhập sai.
  - Lệch vĩnh viễn với sổ công ty chứng khoán và với cơ sở tính thuế.

### Option C — Synthetic `Trade` (nới ràng buộc `Price > 0`)

Cổ tức cổ phiếu = một `Trade` BUY giá 0; chia tách = SELL toàn bộ + BUY lại.

- **Pros:**
  - Rẻ nhất; khoảng 15 service tự đúng mà không cần sửa call-site nào.
- **Cons:**
  - Mất lớp validate `Price > 0` cho trade thật.
  - `PortfolioCashCalculator` phải loại trừ các trade này — sai một chỗ là tiền mặt sai.
  - **Không có chỗ lưu ngày GDKHQ tách khỏi ngày thanh toán** → không biểu diễn được trạng thái "chờ về", tức là không giải quyết được chính vấn đề đã kích hoạt ADR này.

## Decision

**Chọn Option A.**

Option C rẻ hơn hẳn nhưng không làm được trạng thái "chờ về" — mà đó chính là vấn đề gốc, không phải một tính năng phụ. Option B đổi tính đúng đắn lấy chi phí thấp theo cách không thể hoàn tác. Cái giá của A là sửa dần call-site, và đó là chi phí trả một lần, chia nhỏ được: phase 1 chỉ đấu nối 5 điểm dùng số để ra quyết định (P&L, cắt lỗ, snapshot, rủi ro, tiền mặt), phần còn lại để sau.

Hai quyết định phụ đi kèm:

- **Cổ tức tiền mặt là thu nhập, không giảm giá vốn**; cổ tức cổ phiếu và chia tách thì giảm. Đúng bản chất (nhận tiền ra ngoài vs. tổng vốn không đổi) và khớp cơ sở tính thuế TNCN 5%.
- **Giá ngưỡng (cắt lỗ, mục tiêu) điều chỉnh tại thời điểm đọc** qua `CorporateActionAdjuster`, không sửa dữ liệu — nên xoá sự kiện thì ngưỡng tự quay về giá trị cũ.

## Consequences

**Positive:**

- Lãi/lỗ đúng ngay tại ngày GDKHQ, không còn khoảng 1–2 tháng hiển thị lỗ giả.
- Cảnh báo cắt lỗ hết kích hoạt nhầm sau ngày giao dịch không hưởng quyền.
- `PositionBuilder` là hàm thuần, không I/O → test bằng dữ liệu thật, không cần mock repository.
- Cổ tức tiền mặt gắn được với mã → trả lời được "lãi thực của SAB gồm cả cổ tức là bao nhiêu".

**Negative / Trade-offs:**

- Trong lúc chuyển đổi, service đã đấu nối và service chưa đấu nối sẽ ra số khác nhau. Giảm thiểu bằng cách mỗi call-site một commit riêng và chạy lại toàn bộ test sau từng bước.
- `SettledQuantity` lệch với sổ công ty chứng khoán trong 1–2 tháng chờ về — chấp nhận, và bù bằng cách hiển thị tách bạch `1.000 (+300 chờ về)`.
- Thêm một bước nhập liệu thủ công cho người dùng; tự động lấy sự kiện từ 24hmoney để phase 2.

**Follow-ups:**

- Migration: không có. Dữ liệu cũ giữ nguyên; sự kiện lịch sử nhập tay.
- Tests: `PositionBuilderTests`, `CorporateActionAdjusterTests`, và test đấu nối cho từng service trong 5 điểm phase 1.
- Docs: `docs/business-domain.md` (entity + quy tắc nghiệp vụ), `docs/architecture.md` (ghi rõ mọi service cần giá vốn phải gọi `PositionBuilder`), `docs/features.md`, `docs/project-context.md`, hướng dẫn người dùng `frontend/src/assets/docs/su-kien-quyen.md`.
- Còn lại (thống kê, không ra quyết định): `BacktestEngine`, `BehavioralAnalysisService`, `StrategyPerformanceService`, `CampaignReviewService`, `DisciplineScoreCalculator`, `GetSymbolTimelineQuery`, `GetAllPortfoliosQuery.TotalInvested`, nhánh dự phòng dựng vị thế từ trade thô trong `AiAssistantService`.

## Amendment 2026-08-09 — đấu nối nốt hai đường ra quyết định tự động (PR #146)

Phần "Positive" ở trên từng nói `RiskCalculationService` đã được phủ; đúng ra ở PR #145 chỉ `GetPortfolioRiskSummaryAsync` và `GetTrailingStopAlertsAsync` được phủ. ADR cũng xếp `TradePlan` vào diện "chỉ cảnh báo" — đánh giá thiếu, vì giá kế hoạch còn nuôi máy đánh giá kịch bản chạy nền. Hai chỗ đó nay đã đấu nối:

1. **`CheckRiskBudgetAsync` / `CalculateStressTestAsync`** — trước đây tự dựng vị thế từ trade thô; `avgBuyPrice = buys.Average(b => b.Price)` là trung bình *không trọng số* nên lệch được cả dấu lãi/lỗ, và `IsLocked = true` **khoá giao dịch**. Nay lãi/lỗ trong ngày = `RealizedPnL` đến hôm nay trừ đi đến hết hôm qua, cả hai qua `PositionBuilder`; số lượng stress test lấy `TotalQuantity`.
2. **`ScenarioEvaluationService` / `ScenarioAdvisoryService`** — quy giá kế hoạch về mặt bằng hiện tại qua `TradePlanPriceAdjuster` trước khi so với giá thị trường.

Cả hai **không phải regression do ADR này gây ra** — giá thị trường vẫn bị điều chỉnh dù app có biết đến sự kiện quyền hay không.

### Quyết định phụ: giá người dùng đặt thì điều chỉnh khi đọc, giá thị trường ghi nhận thì rebase một lần

Nguyên tắc "điều chỉnh tại thời điểm đọc, không sửa dữ liệu" ở trên **không áp được** cho `TrailingStopConfig.HighestPrice` / `CurrentTrailingStop`: hai giá trị này là quan sát thị trường và được **ghi đè trở lại** entity. Điều chỉnh khi đọc sẽ hạ chồng lần — lần ghi kế tiếp lưu giá ở mặt bằng mới, lần đọc sau lại chia tiếp.

Đã cân nhắc ba hướng: (a) rebase ngay khi tạo `CorporateAction` — đi ngược nguyên tắc bất biến và phải dò ngược mọi kế hoạch liên quan; (b) bỏ qua — để nguyên mức trượt cũ thì đúng hôm điều chỉnh giá sẽ **cắt lỗ oan**, đây là rủi ro tiền thật; (c) rebase **lười, đúng một lần tại thời điểm đọc**, đánh dấu bằng `TrailingStopConfig.PriceBasisAt`.

**Chọn (c).** Ranh giới là bản chất của con số, không phải layer: *ngưỡng do người dùng đặt* mang ý định cần giữ nguyên bản → điều chỉnh khi đọc, xoá sự kiện thì tự quay về; *giá thị trường đã ghi nhận* không mang ý định gì → quy đổi một lần rồi lưu. Đánh đổi: xoá sự kiện quyền **không** khôi phục được `HighestPrice` cũ. Chấp nhận, vì con số đó tự phục hồi ngay khi có đỉnh mới.

Mốc thời gian đi kèm: `TradePlan.PricesSetAt` thay cho `UpdatedAt` — `UpdatedAt` nhảy mỗi lần một nhánh kịch bản kích hoạt, làm các nhánh còn lại thôi được điều chỉnh. Ban đầu định dùng **một** mốc chung cho cả giá kế hoạch lẫn ngưỡng node; review chỉ ra đó là lỗi thật chứ không phải đánh đổi chấp nhận được: `SetScenarioNodes` là endpoint riêng, sửa nhánh kịch bản không hề hiện hay đặt lại giá nhập, nhưng lại dời mốc chung và làm giá nhập thôi được điều chỉnh. Nên tách `ScenarioPricesSetAt` (null → lùi về `PricesSetAt`).

### Sửa kèm: hai lỗi cùng lớp phát hiện khi review

- **Chặn trên theo ngày GDKHQ.** `CorporateActionAdjuster` chỉ lọc `ExDate > setAt`, không có chặn trên — trong khi `PositionBuilder` vốn đã có `ExDate <= asOf`. Sự kiện nhập lúc công bố (trước ngày GDKHQ vài tuần) làm ngưỡng bị chia hệ số ngay, còn giá thị trường thì chưa. Nguy hiểm nhất là `RebaseTrailingState` sẽ ghi đè vĩnh viễn một con số sai. Nay cả `AdjustPrice`, `AdjustDelta` và `RebaseTrailingState` đều chặn trên bằng hôm nay.
- **Thứ tự trong ngày GDKHQ.** `PositionBuilder` xếp `Trade` trước `CorporateAction` cùng ngày. Ngược với luật: quyền chốt theo danh sách cổ đông cuối ngày liền trước, nên người bán trong ngày GDKHQ vẫn hưởng còn người mua hôm đó thì không. Đổi thành sự kiện quyền chạy trước lệnh khớp cùng ngày.

Cả hai đã tồn tại từ PR #145; PR này làm chúng nặng thêm vì đưa `PositionBuilder` vào đường khoá giao dịch.

Hai DTO tạo/lưu kịch bản dựng `TrailingStopConfig` mới và không mang `PriceBasisAt`, nhưng `SetScenarioNodes` luôn đặt lại `PricesSetAt` nên mốc dự phòng thành "bây giờ" — không có sự kiện nào sau đó, không rebase nhầm.

## References

- Spec: `docs/superpowers/specs/done/2026-08-08-corporate-actions-design.md`
- Plan: `docs/superpowers/plans/done/2026-08-08-corporate-actions.md`
- PR: #145 (lõi tính năng), #146 (đấu nối hạn mức rủi ro + kịch bản thoát lệnh)
