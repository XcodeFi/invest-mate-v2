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

## Vòng đời kế hoạch

```
Nháp → Sẵn sàng → Đang thực hiện → Đã thực hiện → Đã review → Huỷ
```

Sau khi thực hiện xong, vào **Campaign Analytics** (`/campaign-analytics`) để review hiệu suất: P&L thực tế, % đạt target, VND/ngày, bài học rút ra.

---

## Trade Replay (`/trade-replay`)

Xem lại toàn bộ vòng đời kế hoạch trên biểu đồ giá thực:

- Biểu đồ giá với điểm vào/ra lệnh
- Đường SL/TP
- Timeline sự kiện
- So sánh kế hoạch vs thực tế
