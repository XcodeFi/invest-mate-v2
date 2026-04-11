# Investment Mate v2 — Hướng phát triển tiếp theo

**Mục tiêu xuyên suốt:** Giảm thời gian thao tác, tăng tính tự động, giúp nhà đầu tư ra quyết định nhanh hơn và tuân thủ kỷ luật tốt hơn.

---

## PHASE 1: HOÀN THIỆN NỀN TẢNG (ưu tiên cao nhất)

### 1.1 Wizard Flow — Quy trình dẫn dắt từng bước

Đây là thay đổi quan trọng nhất. Hiện tại các trang độc lập nhau, người dùng phải tự nhớ mình cần làm gì tiếp theo. Cần tạo một flow liên tục:

**Chiến lược → Kế hoạch GD → Checklist → Giao dịch → Nhật ký**

Cách triển khai: Khi hoàn thành một bước, hiện nút "Bước tiếp theo →" dẫn sang bước kế. Ví dụ sau khi lưu Trade Plan và checklist đạt ≥80%, hiện nút "Thực hiện giao dịch" tự điền sẵn thông tin từ kế hoạch. Sau khi giao dịch xong, popup "Ghi nhật ký cho giao dịch này?" với mã CP và ngày đã điền sẵn.

Lý do: Giảm 50% thao tác vì không phải nhập lại dữ liệu, và quan trọng hơn — buộc người dùng đi đúng quy trình kỷ luật.

### 1.2 Biểu đồ thực tế (thay placeholder)

Trang Phân tích vẫn hiển thị placeholder cho biểu đồ. Cần implement:

- Equity Curve (đường tăng trưởng vốn theo thời gian) — quan trọng nhất vì giúp nhìn thấy lãi kép
- Pie chart phân bổ danh mục (hiện chỉ có text, cần biểu đồ tròn)
- Bar chart P&L theo tháng
- So sánh hiệu suất danh mục vs VNINDEX

Dùng Recharts hoặc Chart.js — cả hai đều nhẹ và phù hợp React.

### 1.3 Tính CAGR thực tế

CAGR hiện hiển thị "--". Công thức: CAGR = (Giá trị hiện tại / Vốn đầu tư)^(1/số năm) - 1. Cần tính dựa trên dòng vốn (capital flows) và giá trị danh mục theo thời gian. Nếu chưa đủ 1 năm dữ liệu, hiển thị "CAGR ước tính (annualized)" dựa trên hiệu suất hiện tại.

---

## PHASE 2: TIẾT KIỆM THỜI GIAN (tự động hóa)

### 2.1 Auto-fill từ mã cổ phiếu

Khi nhập mã CP ở Trade Plan (VD: "VNM"), app tự động điền giá hiện tại vào ô "Giá vào lệnh". Hiện tại người dùng phải mở Market Data tra giá, rồi quay lại Trade Plan nhập tay — mất 30 giây mỗi lần mà hoàn toàn có thể tự động.

Mở rộng: Khi nhập mã CP, hiển thị mini-card bên cạnh cho thấy giá hiện tại, KL giao dịch, % thay đổi ngày — để người dùng không cần rời trang.

### 2.2 Template kế hoạch giao dịch

Cho phép lưu kế hoạch GD đã tạo thành template. VD: "Mua VN30 khi RSI < 30, SL -5%, TP +15%". Lần sau chỉ cần chọn template → đổi mã CP → xong. Tiết kiệm 80% thời gian nhập liệu cho các giao dịch cùng chiến lược.

### 2.3 Tính Position Sizing tự động từ Risk Profile

Hiện Position Sizing yêu cầu nhập % rủi ro mỗi lần. Nếu đã có Risk Profile (VD: max risk 2%/lệnh, max 6% tổng), app tự tính số lượng CP tối đa dựa trên vốn hiện tại và risk profile. Người dùng chỉ cần nhập Entry + SL → ra ngay số lượng CP nên mua.

### 2.4 Quick Trade từ Dashboard

Thêm ô "Giao dịch nhanh" ngay trên Dashboard. Nhập: Mã CP + Mua/Bán + Giá + SL → app tính Position Size, hiển thị R:R, và cho phép lưu/thực hiện ngay. Không cần navigate qua 3 trang.

---

## PHASE 3: QUẢN LÝ RỦI RO THÔNG MINH

### 3.1 Cảnh báo realtime chủ động

Hiện hệ thống cảnh báo chỉ lưu quy tắc — chưa rõ có push notification không. Cần:

- Cảnh báo khi giá CP gần stop-loss (VD: VNM đang ở 20.6% đến SL — đã có dữ liệu, cần biến thành notification nổi bật)
- Cảnh báo khi danh mục drawdown vượt ngưỡng (VD: drawdown > 10%)
- Cảnh báo khi tỷ trọng 1 CP vượt 30% danh mục (tập trung quá cao — đang xảy ra: VNM chiếm 63%)
- Hiển thị banner cảnh báo trên Dashboard, không chỉ trong trang Alerts

### 3.2 Risk Score tổng hợp trên mọi trang

Risk Dashboard có "Sức khỏe rủi ro 80/100" — rất hay, nhưng nên hiển thị con số này như badge trên thanh navigation, để nhà đầu tư luôn thấy sức khỏe danh mục dù đang ở trang nào. Khi điểm giảm dưới 60, badge đổi màu đỏ.

### 3.3 Stress Test / What-If

Thêm chức năng mô phỏng: "Nếu VNINDEX giảm 15%, danh mục tôi sẽ bị ảnh hưởng bao nhiêu?". Dùng beta/correlation của các CP trong danh mục để ước tính. Giúp nhà đầu tư chuẩn bị tâm lý và kịch bản trước khi thị trường biến động.

### 3.4 Risk Profile enforcement

Đã có "Tuân thủ Risk Profile" trên Risk Dashboard nhưng hiện "Chưa thiết lập". Cần cho người dùng thiết lập quy tắc cứng: max risk/lệnh, max drawdown cho phép, max tỷ trọng/CP. Khi Trade Plan vi phạm bất kỳ quy tắc nào → cảnh báo đỏ + không cho lưu (hoặc yêu cầu xác nhận "Tôi biết tôi đang vi phạm").

---

## PHASE 4: THEO DÕI LÃI KÉP TRỰC QUAN

### 4.1 Mục tiêu lãi kép cá nhân

Cho người dùng đặt mục tiêu: "Tôi muốn đạt CAGR 15%/năm trong 10 năm". App tính: vốn hiện tại 20M → mục tiêu = 80.9M sau 10 năm. Hiển thị đường mục tiêu (target curve) vs đường thực tế trên Equity Curve.

### 4.2 Compound Growth Tracker trên Dashboard

Thêm card "Lãi kép" trên Dashboard hiển thị:
- CAGR thực tế (hiện tại)
- Tiến độ so với mục tiêu (VD: "Đạt 72% mục tiêu năm")
- Ước tính giá trị vốn sau 5/10/20 năm nếu giữ CAGR hiện tại

### 4.3 Monthly review tự động

Đầu mỗi tháng, app tự tổng hợp: số GD thắng/thua, P&L tháng, drawdown max, chiến lược nào hiệu quả nhất. Hiển thị như "Báo cáo tháng 3/2026" mà người dùng chỉ cần đọc — không cần tự tính. Đây là chìa khóa để cải thiện liên tục.

---

## PHASE 5: TRẢI NGHIỆM NÂNG CAO

### 5.1 Mobile-first responsive

Nhà đầu tư thường check danh mục trên điện thoại. Dashboard Cockpit cần responsive hoàn hảo: 1 cột trên mobile, swipe giữa các card, bottom navigation. Trade Plan form cần dạng vertical trên mobile.

### 5.2 Keyboard shortcuts

Cho power user: Ctrl+T = mở Trade Plan, Ctrl+M = Market Data, Ctrl+D = Dashboard. Tiết kiệm 5-10 giây mỗi lần thay vì click menu.

### 5.3 Dark mode

Nhiều trader xem biểu đồ ban đêm. Dark mode giảm mỏi mắt và phổ biến trong các app tài chính.

### 5.4 Export báo cáo PDF/Excel

Xuất báo cáo danh mục, lịch sử GD, phân tích hiệu suất ra PDF hoặc Excel. Hữu ích cho báo cáo thuế hoặc review với mentor.

### 5.5 Multi-timeframe Dashboard

Cho phép chuyển đổi nhanh: Hôm nay / Tuần này / Tháng này / Năm nay / Toàn bộ. Mỗi timeframe hiển thị P&L, số GD, win rate tương ứng.

---

## THỨ TỰ TRIỂN KHAI ĐỀ XUẤT

| Thứ tự | Tính năng | Công sức | Tác động |
|:---:|-----------|:---:|:---:|
| 1 | Wizard Flow (1.1) | Trung bình | Rất cao |
| 2 | Biểu đồ thực tế (1.2) | Trung bình | Rất cao |
| 3 | Auto-fill giá từ mã CP (2.1) | Thấp | Cao |
| 4 | Risk Profile enforcement (3.4) | Trung bình | Rất cao |
| 5 | CAGR thực tế (1.3) | Thấp | Cao |
| 6 | Compound Growth Tracker (4.2) | Trung bình | Cao |
| 7 | Cảnh báo realtime (3.1) | Trung bình | Cao |
| 8 | Template GD (2.2) | Thấp | Trung bình |
| 9 | Quick Trade (2.4) | Trung bình | Trung bình |
| 10 | Monthly review (4.3) | Trung bình | Cao |
| 11 | Stress Test (3.3) | Cao | Trung bình |
| 12 | Mobile responsive (5.1) | Cao | Cao |
| 13 | Export PDF/Excel (5.4) | Trung bình | Trung bình |

---

## TÓM TẮT

Ba thay đổi có ROI cao nhất (ít công sức, nhiều tác động):

1. **Auto-fill giá từ mã CP** — tiết kiệm 30s mỗi GD, implement chỉ cần gọi API giá đã có sẵn
2. **Wizard Flow** — biến app từ "bộ công cụ rời rạc" thành "hệ thống quy trình", buộc kỷ luật
3. **Risk Profile enforcement** — tự động ngăn vi phạm quy tắc rủi ro, bảo vệ vốn

Ba thay đổi có giá trị dài hạn lớn nhất:

1. **Biểu đồ Equity Curve + so sánh mục tiêu lãi kép** — nhìn thấy = tin tưởng = kiên nhẫn
2. **Monthly review tự động** — cải thiện liên tục không tốn thời gian
3. **Cảnh báo chủ động trên Dashboard** — phòng ngừa > chữa trị
