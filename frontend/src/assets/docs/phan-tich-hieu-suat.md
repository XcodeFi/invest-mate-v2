# Phân tích hiệu suất

> Theo dõi hiệu suất đầu tư qua equity curve, win rate, và báo cáo tháng.

---

## Analytics (`/analytics`)

### Tab Overview

- **Biểu đồ P&L theo cổ phiếu**: Bar chart so sánh lãi/lỗ từng mã
- **Phân bổ danh mục**: Donut chart tỷ trọng các vị thế
- **Top Holdings**: Bảng các vị thế lớn nhất
- **Risk Metrics**: VaR, Drawdown, Correlation tóm tắt

### Tab Trade Statistics

| Chỉ số | Ý nghĩa | Giá trị tốt |
|--------|---------|-------------|
| **Win Rate** | % giao dịch có lãi | > 50% |
| **Profit Factor** | Tổng lãi / Tổng lỗ | > 1.5 |
| **Expectancy** | Kỳ vọng lãi trung bình mỗi lệnh | > 0 |
| **Gross P&L** | Tổng lãi/lỗ chưa trừ phí | — |

### Tab Equity Curve

Biểu đồ tăng trưởng vốn theo thời gian:
- Line chart giá trị danh mục theo ngày
- Bảng daily return và cumulative return
- Filter: 30D / 90D / 1Y / All

### Tab Monthly Returns

Ma trận Năm × Tháng — color-coded:
- Xanh = tháng có lãi
- Đỏ = tháng thua lỗ
- Đậm hơn = biên độ lớn hơn

---

## Báo cáo tháng (`/monthly-review`)

Tổng kết hiệu suất giao dịch tự động theo tháng:

- Số giao dịch (thắng / thua)
- Win rate, P&L tuyệt đối và %
- Max drawdown trong tháng
- So sánh với tháng trước
- **AI tổng kết**: Nút "AI Tổng kết" → AI phân tích xu hướng, điểm mạnh/yếu, gợi ý cải thiện

---

## Campaign Analytics (`/campaign-analytics`)

Phân tích hiệu suất các chiến dịch giao dịch (Trade Plan đã review):

- **Summary cards**: Tổng P&L, số plan đã review, best/worst plan
- **Bảng so sánh**: Mỗi plan với P&L, %, VND/ngày, annualized return
- **Bài học rút ra**: Feed lessons learned từ tất cả plan đã review
- **Filter theo tầm nhìn**: Ngắn hạn / Trung hạn / Dài hạn

---

## Dashboard Equity Curve

Mini equity curve trên Dashboard:

- Biểu đồ thu nhỏ Chart.js
- Filter: 30D / 90D / 1Y / All
- **Multi-timeframe**: Tab Hôm nay / Tuần này / Tháng này / Năm nay / Toàn bộ

---

## Compound Growth Tracker

Theo dõi tăng trưởng kép trên Dashboard:

1. Đặt mục tiêu CAGR + kỳ hạn
2. Hệ thống so sánh Thực tế vs Mục tiêu
3. Ước tính vốn sau 5/10/20 năm
4. Progress bar % đạt mục tiêu

**CAGR hiển thị là CAGR toàn bộ danh mục của bạn** (household-level — gộp snapshot mọi danh mục thành 1 series tổng rồi annualize), không phải CAGR của 1 danh mục lẻ. Khi cửa sổ dữ liệu < 1 năm, ô CAGR kèm badge "⚠️ X ngày · chưa đủ 1 năm" — số đó là **ngoại suy** từ kỳ ngắn nên dao động lớn, đừng dùng làm cam kết dài hạn.

---

## Snapshots (`/snapshots`)

Ảnh chụp danh mục hàng ngày:

- Ghi lại giá trị, vị thế, P&L tại mỗi thời điểm
- So sánh 2 snapshot → xem thay đổi
- Dữ liệu snapshot phục vụ equity curve và analytics
