# ADR-0012 — Tỷ trọng ngành chỉ hiển thị, không chặn lập kế hoạch

- **Status:** Accepted
- **Date:** 2026-08-10
- **Related plan:** `docs/superpowers/plans/2026-08-10-sector-concentration.md`
- **Affected layers:** Application / Infrastructure / Api / Frontend

## Context

Hạn mức tập trung ngành `RiskProfile.MaxSectorExposurePercent` (mặc định 40%) đã tồn tại trong entity, đã được `RiskCalculationService` tính, đã hiện trên risk-dashboard — và **chưa từng bắn lần nào**. Nguyên nhân là một chuỗi ba mắt: service tra ngành qua `IFundamentalDataProvider`; interface đó được đăng ký là `NoOpFundamentalDataProvider` (luôn trả `null`, dòng đăng ký TCBS thật bị comment ở `Program.cs:197`); nên mọi vị thế rơi vào rổ "Không xác định", và rổ đó bị hardcode `IsOverweight = false`.

Đây là hình dạng tệ hơn "chưa làm tính năng": màn hình có mục tỷ trọng ngành, có con số, có hạn mức ghi bên cạnh, nên người đọc tin là đã được canh. Test `GetPortfolioOptimizationAsync_SectorOverweight_ReturnsSectorAlert` cũng **đang xanh**, vì helper của nó mock đúng cái provider bị vô hiệu hoá.

Song song, cổng hồ sơ công ty (ADR-0011) chặn theo size **từng lệnh** và không biết gì về tập trung ngành: viết năm hồ sơ cho năm công ty thép, ký cả năm, thì cả năm lệnh đều qua cổng. Câu hỏi cần quyết là tập trung ngành nên trở thành điều kiện chặn thứ hai, hay chỉ là thông tin.

## Options Considered

### Option A — Chỉ hiện số, không chặn

- **Pros:**
  - Không phụ thuộc chất lượng provider ngoài để cho phép hay từ chối một hành động.
  - Rẻ: dùng lại đúng nguồn dữ liệu và hạn mức đang có, không thêm khái niệm mới.
  - Giữ nguyên một cổng chặn duy nhất (hồ sơ công ty), người dùng không phải học luật thứ hai.
- **Cons:**
  - Không ngăn được việc dồn ngành, chỉ làm nó nhìn thấy được.
  - Người dùng có thể bỏ qua con số, đúng như đang bỏ qua nó trên risk-dashboard.

### Option B — Chặn cứng như cổng hồ sơ

- **Pros:**
  - Mạnh nhất: không tạo được lệnh đẩy ngành vượt hạn mức.
  - Nhất quán về hình thức với cổng hồ sơ đang chạy.
- **Cons:**
  - Nhãn ngành đến từ 24hmoney. Provider trả sai nhãn, đổi taxonomy, hoặc chết là **chặn oan** — khác hẳn cổng hồ sơ, nơi dữ liệu do chính người dùng viết nên luôn có và luôn đúng theo định nghĩa.
  - Một cổng chặn oan sẽ bị vô hiệu hoá, và nó kéo theo mất niềm tin vào cổng hồ sơ đang hoạt động tốt.
  - Ngành không phải thước đo đúng của "cùng chết vì một lý do": thép và xây dựng khác ngành mà chung một cú.

### Option C — Bắt viết lý do khi vượt hạn mức

- **Pros:**
  - Cùng triết lý với hồ sơ công ty: không đo độ dài, đo việc đã nghĩ tới.
  - Để lại câu trả lời trong plan, lúc review còn đọc được.
- **Cons:**
  - Vẫn là một đường chặn dựa trên dữ liệu provider — thừa hưởng nguyên nhược điểm của Option B, chỉ nhẹ hơn.
  - Thêm một ô bắt buộc nữa vào form vốn đã dài.

## Decision

**We choose Option A.**

Điều quyết định không phải "chặn mạnh hơn thì tốt hơn", mà là **nguồn dữ liệu quyết định được quyền chặn**. Cổng hồ sơ chặn được vì nó đọc thứ người dùng tự viết; tỷ trọng ngành đọc nhãn của provider ngoài, nên biến nó thành đường chặn là đặt quyền phủ quyết vào tay một dịch vụ mà ta không kiểm soát — và cái giá khi nó sai không chỉ là một lệnh bị chặn oan, mà là người dùng học được cách vô hiệu hoá các cổng. Hiện số ở đúng lúc đang quyết định size đã lấy được phần lớn giá trị với gần như không có rủi ro.

Kèm theo, rổ "Không xác định" **cũng** được so hạn mức: không biết mình đang dồn vào đâu là một thông tin, không phải sự vắng mặt của thông tin.

**Phạm vi: chỉ lệnh MUA.** Phép chiếu cộng quy mô lệnh vào giá trị ngành, nên với lệnh BÁN nó báo tỷ trọng tăng đúng lúc lệnh đó làm giảm — một con số sai dấu nằm trong khối cảnh báo rủi ro còn tệ hơn không có số. Đường bán không gọi endpoint. Muốn hỗ trợ bán thì phải truyền hướng lệnh xuống query/endpoint/service và xử lý riêng ca bán quá số đang giữ; chưa làm.

## Consequences

**Positive:**

- Hạn mức ngành 40% bắn được lần đầu; risk-dashboard có số thật mà không cần thay đổi UI nào.
- Form lập kế hoạch hiện tỷ trọng hiện tại và sau lệnh ngay trong khối kiểm-trước đã có, cùng một lần debounce 500ms.
- Công thức `totalValue` và phép tra ngành mỗi thứ chỉ còn một chỗ (`ComputeTotalValue`, `ResolveIndustryAsync`), dùng chung giữa đường optimization và đường mới.

**Negative / Trade-offs:**

- Vẫn tạo được lệnh dồn ngành. Cảnh báo chỉ là chữ.
- `IFundamentalDataProvider` sau thay đổi này **không còn được `RiskCalculationService` dùng tới**, nhưng vẫn nằm trong constructor. Cố ý để lại: bỏ nó ra là sửa chữ ký constructor và 5 harness test, ngoài phạm vi.
- Mẫu số phép chiếu **không** cộng `addValue` (vì `totalValue` đã gồm tiền mặt, mua bằng tiền trong danh mục chỉ chuyển tiền mặt thành giá trị vị thế). Đúng nhưng phản trực giác, nên có test ghim đúng ca phân biệt 53,33% với 49,61%.
- Số liệu ngành phụ thuộc 24hmoney: provider không trả ngành thì **ẩn hẳn khối**, không hiện 0%. "n/a" dành cho ca khác — biết ngành nhưng `totalValue ≤ 0` nên không chia được tỷ trọng. Dù ẩn hay "n/a", người dùng vẫn không có ngành cho mã đó.
- Mỗi lần tra ngành là 9 request HTTP song song sang 24hmoney (`GetComprehensiveDataAsync` lấy cả báo cáo tài chính, peers, cổ tức, khối ngoại) chỉ để đọc một chuỗi. Endpoint bị gọi lại mỗi nhịp debounce 500ms nên phải cache: `ResolveIndustryAsync` cache nhãn ngành 6 giờ trong `IMemoryCache`, và **không cache ca lỗi/rỗng** — cache một lần provider timeout là đóng băng mã đó thành "không rõ ngành" suốt TTL, tức một lỗi mạng nhất thời làm im cảnh báo tập trung nhiều giờ. Mã đang gõ không tra lại trong vòng lặp vì ngành của nó đã có sẵn.

**Follow-ups (if any):**

- Migration to run: không có. Không đổi schema.
- Tests to add: đã có 6 test backend + 6 test frontend cho vòng này.
- Docs to update: `architecture.md`, `business-domain.md`, `project-context.md`, CHANGELOG, hướng dẫn người dùng (đã làm cùng PR).
- Chặng sau, đã ghi trong plan: hiện sàn giao dịch trong thông tin công ty; hardcode danh sách nhóm ngành thành dropdown **sau khi** dò 24hmoney lấy taxonomy thật (không bịa). Việc thứ hai đảo lại spec Q6 nên phải sửa Q6 tại chỗ khi làm.

## References

- Plan: `docs/superpowers/plans/2026-08-10-sector-concentration.md`
- Spec: `docs/superpowers/specs/2026-08-10-sector-concentration-design.md` (Q1–Q6)
- ADR liên quan: [ADR-0011](0011-company-dossier-gate-at-plan-creation.md) — cổng hồ sơ công ty, đường chặn duy nhất mà ADR này cố ý không nhân bản
- PR: #148
