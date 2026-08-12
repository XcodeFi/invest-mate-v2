# ADR-0014 — Trần khối lượng dựng trên hiệp phương sai, không dùng lợi nhuận kỳ vọng

- **Status:** Accepted
- **Date:** 2026-08-11
- **Related plan:** `docs/plans/volatility-budget-plan-sizing.md`
- **Affected layers:** Application / Infrastructure / Api / Frontend

## Context

Form lập kế hoạch hiện kiểm ba thứ trước khi cho lưu: cổng hồ sơ công ty (ADR-0011), tỷ trọng ngành (ADR-0012), và tỷ lệ lãi/lỗ. Cả ba đều tính **trên từng mã riêng lẻ**. Không có ràng buộc nào nhìn vào quan hệ giữa mã sắp mua và những mã đang giữ — mua một mã tương quan 0,6 với vị thế lớn nhất và mua một mã tương quan 0,0 là hai hành động rủi ro khác hẳn nhau, nhưng hôm nay hệ thống coi chúng như nhau miễn tỷ trọng vốn bằng nhau.

Lý thuyết danh mục hiện đại (MPT) lấp đúng khoảng trống đó. Nhưng áp nguyên bản Markowitz vào danh mục 8–15 mã với dữ liệu Việt Nam là đi thẳng vào điểm yếu đã biết của nó, nên câu hỏi cần quyết không phải "có dùng MPT không" mà là **dùng phần nào của MPT**.

Ràng buộc dữ liệu đo được ngày 2026-08-11 (chi tiết §3 spec): endpoint graph của 24hmoney chỉ cho tối đa **65 quan sát ngày**; giá **không được điều chỉnh theo sự kiện quyền** (VHM có phiên −49,6% làm σ nhảy từ 48,9% lên 109,0%); và không có lịch sử VNINDEX qua đường này.

## Options Considered

### Option A — Chỉ hiệp phương sai, ra trần khối lượng

Tính Σ, ra σ danh mục trước/sau, đóng góp rủi ro biên, và giải trần khối lượng từ một phương trình bậc hai một biến.

- **Pros:**
  - Không có lợi nhuận kỳ vọng ở bất kỳ đâu, nên không thừa hưởng sai số dự báo.
  - **Không nghịch đảo Σ.** Chỉ cần σ_p = √(wᵀΣw) — một phép nhân. Với 65 quan sát và 10 mã, Σ đủ ổn định để nhân nhưng quá mỏng để nghịch đảo.
  - Ra một con số gắn thẳng vào ô khối lượng người dùng đang điền.
  - Nghiệm đóng, không cần bộ tối ưu hay thư viện ngoài.
- **Cons:**
  - Không có đường cong đường biên hiệu quả — người dùng đọc về khái niệm này sẽ không thấy hình vẽ quen thuộc.
  - Không nói được "nên mua mã nào", chỉ nói "mã này được bao nhiêu".

### Option B — Đường biên hiệu quả đầy đủ, μ từ trung bình lịch sử

- **Pros:**
  - Đúng sách vở, có đường cong, có danh mục Sharpe cao nhất.
- **Cons:**
  - Đây chính là cấu hình bị gọi là "error maximizer". Với 65 quan sát, sai số chuẩn của trung bình lớn hơn chênh lệch giữa các mã, nên bộ tối ưu dồn vốn vào mã tình cờ tăng mạnh gần đây.
  - Cần Σ⁻¹, và nghịch đảo khuếch đại sai số ước lượng.
  - Đầu ra là bộ tỷ trọng cho toàn danh mục — không trả lời được câu hỏi đang hỏi ("mã này mua bao nhiêu").

### Option C — Đường biên hiệu quả với μ lấy từ mục tiêu giá trong kế hoạch của người dùng

Dùng `(Target − EntryPrice) / EntryPrice` làm lợi nhuận kỳ vọng, `ConfidenceLevel` làm trọng số — tức cấu trúc "quan điểm" kiểu Black–Litterman.

- **Pros:**
  - Né được vấn đề ước lượng μ: không dự báo thị trường, chỉ lấy điều người dùng đã tự tuyên bố.
  - Biến frontier thành tấm gương soi tính nhất quán của chính người dùng.
- **Cons:**
  - Vẫn cần Σ⁻¹.
  - Chỉ có nghĩa khi người dùng có ≥ 4 kế hoạch nháp cùng lúc — điều kiện chưa xảy ra.
  - Xây bây giờ là xây cho một tình huống chưa tồn tại.

## Decision

**We choose Option A.**

Ranh giới quyết định là **phép nhân so với phép nghịch đảo**. Toàn bộ tai tiếng của MPT nằm ở Σ⁻¹, nơi sai số ước lượng nhỏ nở thành tỷ trọng vô lý; σ_p = √(wᵀΣw) không đi qua đó. Với 65 quan sát — trần cứng do nhà cung cấp dữ liệu áp, không phải lựa chọn của ta — Option A là phần MPT duy nhất còn đứng vững, và nó tình cờ cũng là phần trả lời đúng câu hỏi người dùng đang hỏi khi tay đang ở ô khối lượng.

Option C được ghi làm hướng V2 trong §7 spec, kèm điều kiện kích hoạt rõ ràng.

Hai quyết định kèm theo:

**Cảnh báo, không phải cổng cứng.** Kế thừa nguyên lý ADR-0012: *nguồn dữ liệu quyết định quyền chặn*. Cổng hồ sơ chặn được vì đọc thứ người dùng tự viết. Tỷ trọng ngành không được chặn vì đọc nhãn của provider ngoài. Trần khối lượng đọc **ước lượng thống kê từ 65 quan sát** — yếu hơn cả nhãn ngành, nên càng không được chặn. Một cổng chặn oan sẽ bị học cách vô hiệu hoá, và nó kéo theo mất niềm tin vào cổng hồ sơ đang hoạt động tốt.

**`MaxDrawdownAlertPercent` mang nghĩa thứ hai thay vì thêm trường mới.** Xem §Consequences — đây là đánh đổi nặng nhất của ADR này.

## Consequences

**Positive:**

- Lần đầu có một ràng buộc nhìn vào **quan hệ** giữa các vị thế, không chỉ quy mô từng vị thế.
- Đóng góp rủi ro biên hiện cạnh tỷ trọng vốn, lộ ra chênh lệch kiểu "chiếm 14% vốn nhưng gánh 22% rủi ro" — con số dạy được nhiều nhất trên panel và gần như miễn phí vì Σ đã tính rồi.
- Bộ lọc lợi suất bất thường (§4.3 spec) chặn được một lớp lỗi im lặng mà `CorporateActionAdjuster` không với tới: nó chỉ chạy trên sự kiện quyền người dùng tự nhập cho danh mục mình, còn mã ứng viên chưa từng mua thì không có dữ liệu đó.
- Toán học nằm trong một lớp tĩnh thuần, kiểm thử bằng mảng số, không cần Moq.

**Negative / Trade-offs:**

- **`MaxDrawdownAlertPercent` giờ có hai nghĩa.** Risk-dashboard đọc nó theo nghĩa cũ ("báo khi tôi xuống 10% từ đỉnh"); tính năng này đọc nó là *ngưỡng lỗ ở mức tin cậy 95% trong 21 phiên*, quy ra ngân sách σ năm qua `MaxDD / (1,645 × √(21/252))`. Hai cách đọc cùng hướng — đặt thấp là thận trọng ở cả hai nơi — nhưng người đọc code sau này sẽ không tự suy ra được vì sao chia cho hằng số đó. Đây là lý do ADR này tồn tại.
- **Chân trời 21 phiên là suy ngược từ dữ liệu, không phải từ ý định người dùng.** Nếu diễn giải theo năm, giá trị mặc định 10% cho ngân sách 6,1%/năm trong khi danh mục 7 mã thật đo được 19,4%/năm — trần sẽ luôn bằng 0 và panel thành tiếng ồn ngay ngày đầu. Chọn 21 phiên vì nó cho 21,1%/năm, sát mức thật nên trần thỉnh thoảng mới chạm. Con số này cần xem lại sau vài tuần dùng thật.
- Vẫn tạo được lệnh vượt trần. Cảnh báo chỉ là chữ, giống ADR-0012.
- Bộ lọc 15% **không** dựng lại giá đúng, chỉ bỏ quan sát. Mã có sự kiện quyền mất một quan sát trong cửa sổ vốn đã chỉ 65 — chấp nhận, vì thay thế là xây đường điều chỉnh giá cho mọi mã trên sàn.
- Chỉ lệnh MUA, giống ADR-0012 §Phạm vi. Lệnh bán không gọi endpoint.
- Ước lượng Σ từ 65 quan sát có sai số thật. Panel hiện số quan sát để người dùng tự chiết khấu độ tin cậy.

**Follow-ups (if any):**

- Migration to run: không có. Không đổi schema.
- Nợ kỹ thuật tách riêng: ánh xạ `days → type` trong `HmoneyMarketDataProvider` sai một bậc (yêu cầu 90 ngày nhận về thanh 3 ngày). Tính năng này đi đường riêng gọi thẳng `type=3` và có test ghim; **không** sửa ánh xạ cũ vì nó phục vụ biểu đồ hiển thị.
- `GetDailyHistoryAsync` cố ý **không** nuốt ngoại lệ, khác 8 hàm còn lại cùng file: người gọi phải phân biệt "nguồn hỏng" (`FetchFailedSymbols`) với "mã chưa có lịch sử" (`MissingSymbols`), vì gộp lại là nói sai sự thật về mã đó trên giao diện. Khi `docs/plans/p1-provider-fail-loudly.md` triển khai phân loại lỗi diện rộng, hàm này nên chuyển sang cùng taxonomy thay vì giữ cách riêng.
- Xem lại chân trời 21 phiên sau 2–4 tuần dùng thật. Nếu trần chạm quá thường xuyên hoặc không bao giờ chạm, tách `MaxPortfolioVolatilityPercent` thành trường riêng.
- Docs to update: `architecture.md`, `business-domain.md`, `project-context.md`, CHANGELOG, hướng dẫn người dùng.

## References

- Spec: `docs/superpowers/specs/2026-08-11-efficient-frontier-plan-sizing-design.md`
- Plan: `docs/plans/volatility-budget-plan-sizing.md`
- ADR liên quan: [ADR-0012](0012-sector-concentration-display-only.md) — nguyên lý "nguồn dữ liệu quyết định quyền chặn" mà ADR này kế thừa; [ADR-0011](0011-company-dossier-gate-at-plan-creation.md) — cổng chặn duy nhất, cố ý không nhân bản; [ADR-0010](0010-corporate-actions-position-projection.md) — `PositionBuilder` là nguồn duy nhất của toán vị thế, tính năng này gọi lại qua `GetPortfolioRiskSummaryAsync` thay vì tự dựng
- PR: #XX (điền sau khi merge)
