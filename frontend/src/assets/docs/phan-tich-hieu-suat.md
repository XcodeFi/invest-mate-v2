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
- **Tỉ lệ thắng và trung bình chỉ tính chiến dịch ĐÃ ĐÓNG**: chiến dịch còn cổ phiếu chưa bán, hoặc bán nhiều hơn số mua đã gắn vào kế hoạch, chỉ có lãi/lỗ của phần đã chốt (có thể bằng 0) nên không đại diện cả chiến dịch. Chúng bị loại khỏi **Win Rate, TB Lãi/Lỗ và chiến dịch tốt nhất/tệ nhất**, và trang ghi một dòng "N chiến dịch còn mở/thiếu lệnh mua không tính vào tỉ lệ thắng". **Tổng tích lũy và đường lũy kế vẫn gồm mọi chiến dịch**, vì tiền đã chốt là tiền thật. Chưa có chiến dịch nào đã đóng thì Win Rate và TB Lãi/Lỗ hiện **n/a** (không phải 0%).

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
