# Kế hoạch giao dịch

> Lập kế hoạch trước khi vào lệnh — entry, stop-loss, take-profit, position sizing, checklist.

---

## Tổng quan

Trang **Kế hoạch GD** (`/trade-plan`) giúp bạn lập kế hoạch đầy đủ trước khi giao dịch:

- Auto-fill giá hiện tại khi nhập mã CP
- Tính position size dựa trên Risk Profile
- Checklist 13 mục kiểm tra
- Hỗ trợ chia lô (Scaling In / DCA)
- Kịch bản nâng cao (Scenario Playbook)
- Lưu/tải template cho lần sau

---

## Tạo kế hoạch mới

### Thông tin cơ bản

1. **Mã cổ phiếu**: Nhập mã → tự động lấy giá hiện tại
2. **Hướng giao dịch**: Mua hoặc Bán
3. **Chiến lược**: Chọn từ thư viện (tùy chọn) — nếu chiến lược có SL%/R:R gợi ý → tự điền SL/TP
4. **Điều kiện thị trường**: Uptrend / Downtrend / Sideway
5. **Lý do vào lệnh**: Ghi rõ phân tích

### Giá Entry, Stop-Loss, Take-Profit

| Trường | Mô tả |
|--------|-------|
| **Giá vào lệnh** | Giá dự kiến mua/bán. Auto-fill từ giá hiện tại |
| **Stop-Loss** | Mức giá cắt lỗ. Tự tính từ chiến lược nếu có |
| **Take-Profit** | Mức giá chốt lời. Tự tính từ R:R gợi ý |

### Position Sizing (tự động)

Hệ thống tự tính dựa trên Risk Profile:

- **Rủi ro/lệnh** = Tổng vốn × % rủi ro (VD: 100 triệu × 2% = 2 triệu)
- **Số cổ phiếu** = Rủi ro/lệnh ÷ (Entry - SL)
- **% danh mục** = Giá trị lệnh ÷ Tổng vốn
- **R:R** = (TP - Entry) ÷ (Entry - SL)

---

## Chia lô (Multi-lot Entry)

3 chế độ vào lệnh:

### Một lần
Mua/bán toàn bộ tại 1 giá.

### Chia lô (Scaling In)
- Thêm nhiều lô với giá khác nhau
- Preset phân bổ: 40/30/30, 50/50, bằng nhau
- Mỗi lô: giá, số lượng, % phân bổ

### DCA (Dollar Cost Averaging)
- Số tiền mỗi lần, tần suất (tuần/2 tuần/tháng)
- Số kỳ, ngày bắt đầu
- Bảng lịch mua dự kiến với giá trị tích luỹ

---

## Checklist GO/NO-GO

13 mục kiểm tra trước khi vào lệnh. 5 mục bắt buộc phải đạt để được phê duyệt:

- Xu hướng thị trường phù hợp?
- Stop-loss đã xác định?
- Position size trong giới hạn?
- R:R tối thiểu 1:2?
- Tâm lý ổn định?

---

## Scenario Playbook (nâng cao)

Lập kịch bản "Nếu... thì..." cho mỗi kế hoạch:

- **Chế độ đơn giản**: Kịch bản tốt nhất / xấu nhất / trung tính
- **Chế độ nâng cao**: Decision tree — mỗi nút có điều kiện trigger + hành động
- **Tự động đánh giá**: Hệ thống kiểm tra giá thực tế mỗi 15 phút → trigger cảnh báo khi kịch bản xảy ra

---

## Template

Lưu kế hoạch thành template tái sử dụng:

1. Tạo xong kế hoạch → nhấn **"Lưu làm template"**
2. Đặt tên (VD: "Swing FPT") → Lưu
3. Lần sau: chọn template từ dropdown → tự điền toàn bộ form

---

## So sánh mô hình Position Sizing

Khi nhập mã CP và stop-loss, hệ thống tự động tính 5 mô hình:

| Mô hình | Công thức | Khi nào dùng |
|---------|-----------|--------------|
| **Cố định % rủi ro** | `Vốn × %Risk / RiskPerShare` | Mặc định, phổ biến nhất |
| **Theo ATR** | `Vốn × %Risk / (N × ATR)` | Muốn điều chỉnh theo biến động |
| **Kelly Criterion** | Half-Kelly, cap 25% | Có lịch sử giao dịch (win rate, avg W/L) |
| **Turtle (1 unit)** | `1% Vốn / ATR` | Chiến lược Turtle, thêm unit khi lời |
| **Điều chỉnh biến động** | Scale Risk × (2%/ATR%) | ATR thấp → tăng size, ATR cao → giảm |

Click vào mô hình trong bảng so sánh → auto-fill số cổ phiếu. Cột "%DM" hiển thị % danh mục, badge xanh/đỏ cho biết có vượt giới hạn không.

---

## Dynamic Checklist

Checklist thay đổi tự động theo chiến lược đã chọn:

| TimeFrame | Chỉ báo kiểm tra | Multi-TF Gate |
|-----------|-------------------|---------------|
| **Scalping** | VWAP, Stochastic, Volume spike | Không |
| **DayTrading** | EMA(20/50), RSI, MACD, Bollinger, Volume | Daily trend ủng hộ |
| **Swing** | ADX > 25, Fibonacci, RSI/MACD, OBV | Weekly trend ủng hộ |
| **Position** | SMA(50/200) weekly, ADX weekly, MACD weekly | Monthly trend ủng hộ |

### Weighted Scoring
- **●3 (đỏ)**: Bắt buộc — SL đã đặt, R:R ≥ 2:1, xu hướng khung lớn
- **●2 (vàng)**: Quan trọng — chỉ báo phụ, position sizing
- **●1**: Tham khảo — nhật ký, tâm lý

**GO/NO-GO**: Tất cả ●3 items phải checked + tổng điểm ≥ 70%.

---

## Vòng đời kế hoạch

```
Nháp → Sẵn sàng → Đang thực hiện → Đã thực hiện → Đã review → Huỷ
```

### R và kỳ vọng

- **1R** là số tiền bạn chấp nhận mất ở kế hoạch: |giá vào − cắt lỗ| × số lượng. Hệ thống ghi lại 1R khi bạn chuyển kế hoạch sang **Sẵn sàng** (hoặc khớp lot đầu tiên) và **khoá** từ lần khớp đầu tiên: dời cắt lỗ sau đó không làm đổi 1R, để kết quả luôn đo bằng đúng mức rủi ro ban đầu.
- **R:R** trong danh sách kế hoạch và trang Vị thế là số hệ thống tính từ giá vào, cắt lỗ và giá chốt lời (nhiều mốc chốt lời thì lấy bình quân theo tỷ lệ vị thế). Lệnh Bán hoặc thiếu mục tiêu thì hiện giá trị bạn đã nhập.
- Khi review, **R thực tế** = lãi/lỗ ròng ÷ 1R (ví dụ +1,6R: lãi gấp 1,6 lần mức rủi ro ban đầu). "(ước tính)" nghĩa là kế hoạch cũ chưa ghi 1R nên dùng cắt lỗ đầu tiên còn lưu; "n/a" nghĩa là không biết rủi ro ban đầu. Hiện chỉ áp dụng cho lệnh Mua.
- **Review chỉ tính phần đã chốt.** Lãi/lỗ = tiền bán − giá vốn bình quân của số cổ phiếu ĐÃ BÁN, cộng cổ tức tiền mặt bạn được hưởng. Nếu còn cổ phiếu chưa bán, app cảnh báo "Còn N CP chưa bán", phần còn giữ không bị coi là lỗ (phí mua của nó nằm trong giá vốn còn giữ), và 1R cũng chỉ tính theo phần đã bán. Chiến dịch còn cổ phiếu chưa bán **không được tính** vào kỳ vọng R, độ khớp ước lượng và Kelly; bán hết rồi mới vào thống kê. Ví dụ: mua 1.000 @30.000, bán 500 @33.000 → lãi +1,5 triệu (+10%), không phải −13,5 triệu.

### Ước lượng kỳ vọng trước khi vào lệnh (lệnh Mua)

Khối **Kỳ vọng của kế hoạch** trên form giúp bạn hỏi "kèo này có đáng vào không" bằng con số:

1. Mỗi dòng là một kết quả có thể xảy ra của **cả vị thế**: đạt kế hoạch, cắt lỗ, thoát sớm / hoà. Giá thoát được dựng sẵn từ kế hoạch, bạn sửa hoặc thêm dòng được (ví dụ "chạm TP1 rồi quay lại cắt lỗ").
2. Nhập **xác suất bạn tin** cho từng dòng, tổng phải là 100%. Ô xác suất cố ý để trống: nếu điền sẵn theo tỉ lệ thắng lịch sử, ước lượng của bạn sẽ bị neo vào con số đó thay vì đánh giá riêng kèo này. Tỉ lệ nền hiện bên dưới để bạn so **sau** khi đã nhập.
3. Form hiện **EV** (kỳ vọng) theo R và theo VND, và xác suất thắng ngầm định. EV dương nghĩa là về lâu dài kèo này kỳ vọng có lãi. Server tính lại EV đã trừ phí ước tính khi lưu.
4. Tỉ lệ nền ghi số lệnh và khoảng tin cậy 95%: dưới 10 lệnh thì "chưa đủ mẫu để tin", đừng coi nó là sự thật.
5. Nếu EV ≤ 0 hoặc bạn chưa nhập xác suất mà vẫn chuyển sang **Sẵn sàng**, hệ thống hỏi **lý do** (tối thiểu 10 ký tự) chứ không chặn. Lý do được lưu và hiện khi review để bạn nhìn lại quyết định.
6. Ước lượng được ghi lại khi bạn chốt kế hoạch và **khoá khi khớp lệnh đầu tiên**. Khi review, hệ thống đặt ước lượng đã khoá cạnh R thực tế. Nếu bạn vào lệnh mà kỳ vọng không dương hoặc chưa ước lượng và không có lý do, kế hoạch được đánh dấu "Vào lệnh không qua cổng kỳ vọng".

Sau khi thực hiện xong, vào **Campaign Analytics** (`/campaign-analytics`) để review hiệu suất: P&L thực tế, % đạt target, VND/ngày, bài học rút ra.

---

## Trade Replay (`/trade-replay`)

Xem lại toàn bộ vòng đời kế hoạch trên biểu đồ giá thực:

- Biểu đồ giá với điểm vào/ra lệnh
- Đường SL/TP
- Timeline sự kiện
- So sánh kế hoạch vs thực tế
