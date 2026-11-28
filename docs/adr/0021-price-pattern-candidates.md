# ADR-0021 — Ứng viên mô hình giá: hàm thuần, ngưỡng có thể chỉnh, không điểm số, chỉ trên giá đã điều chỉnh

- **Status:** Accepted
- **Date:** 2026-11-28
- **Related plan:** `group_indication-price-patterns-design.md` (PR A)
- **Affected layers:** Application / Infrastructure / Api (MCP) / Frontend

## Context

Người dùng muốn thấy các mô hình giá phổ biến (bắt đầu từ Cốc tay cầm) trên biểu đồ và để agent đọc được. Cơ sở: Lo, Mamaysky và Wang (2000) cho thấy dò mô hình bằng thuật toán có thêm thông tin khiêm tốn; các số đo cốc (7–65 tuần, sâu 12–33%) của O'Neil kiểm chứng được một phần qua bảng so sánh của Bulkowski; độ sâu tay cầm, ngưỡng khối lượng và các ngưỡng còn lại chưa kiểm chứng; không có tỉ lệ thất bại nào được kiểm chứng.

## Options Considered

### Option A — Điểm số hoặc "độ tin cậy %" cho mỗi ứng viên
- **Pros:** dễ đọc, dễ xếp hạng.
- **Cons:** không có con số nào được hiệu chỉnh nên đó là số bịa; dễ bị đọc như xác suất thắng.

### Option B — Ứng viên với số đo, từng điều kiện đạt/không, trạng thái và hình học "nếu sai"; ngưỡng ghi rõ là mặc định có thể chỉnh
- **Pros:** trung thực với bằng chứng; người dùng thấy điều kiện nào đạt; "nếu sai" (cắt lỗ dưới đáy tay cầm, mục tiêu đo theo độ sâu cốc, lãi/rủi ro theo R) là xác định từ nến nên kiểm lại được.
- **Cons:** không xếp hạng được các ứng viên; người dùng phải tự phán đoán.

## Decision

**Chọn Option B.** Bộ dò là hàm thuần (`CupWithHandleDetector.Detect`) trên nến ngày ĐÃ điều chỉnh, nhận `CupWithHandleOptions`; trả tối đa một ứng viên và chỉ khi mọi điều kiện bắt buộc đạt. Chuỗi chưa điều chỉnh (nguồn dự phòng) không được dò vì chia tách có thể tạo "chiếc cốc" giả; nến đang hình thành bị bỏ. Một test phản chiếu chặn mọi thành viên tên score, confidence hay probability trên `PatternCandidate`.

## Consequences

**Positive:**
- Không con số nào vượt quá bằng chứng; mọi ngưỡng chưa kiểm chứng được gắn nhãn như vậy trong Help.
- Cùng một đầu ra cho biểu đồ (REST) và agent (MCP `get_technical_analysis`), không thêm tool nên không đụng danh sách guard.

**Negative / Trade-offs:**
- Phát hiện chỉ một ứng viên (ứng viên hợp lệ đầu tiên, từ đỉnh trái gần nhất ngược về xa), có thể bỏ sót cốc khác.
- Chưa có thống kê thành công/thất bại và chưa có back-test trên bản lưu VN30: phải chạy trên dữ liệu thật trước khi đưa vào trang Sàng lọc (PR B, store-only).

**Follow-ups:**
- Back-test bộ dò trên chuỗi đã lưu của VN30 và ghi tỉ lệ vào `docs/project-context.md` (chưa làm: cần quyền đọc kho thật).
- PR B: cột ở trang Sàng lọc chỉ đọc `IAdjustedPriceSeriesStore.GetAsync`, không bao giờ gọi UDF.
