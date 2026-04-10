# Tài liệu Tham chiếu — Phân tích Kỹ thuật & Chiến lược Giao dịch

Thư mục chứa tài liệu kiến thức nền tảng về phân tích kỹ thuật, chiến lược giao dịch và quản lý rủi ro. Dùng làm **reference** khi thiết kế và implement các tính năng technical analysis trong Investment Mate.

## Danh sách tài liệu

| # | File | Nội dung | Dùng khi |
|---|------|----------|----------|
| 1 | [Phan-Loai-Chi-Bao-Muc-Dich-Cach-Dung.md](Phan-Loai-Chi-Bao-Muc-Dich-Cach-Dung.md) | Phân loại 10 nhóm chỉ báo kỹ thuật — mục đích, cách dùng, tham số, chiến lược phù hợp | Implement indicator mới, thiết kế signal scoring |
| 2 | [Chien-Luoc-Giao-Dich-Va-Quan-Ly-Rui-Ro.md](Chien-Luoc-Giao-Dich-Va-Quan-Ly-Rui-Ro.md) | 5 mô hình position sizing, 6 loại stop loss, 7 chiến lược giao dịch, bảng ma trận chỉ báo × chiến lược | Implement strategy templates, position sizing, SL methods |
| 3 | [Phan-Tich-Ky-Thuat-Giao-Dich-Ngan-Han.md](Phan-Tich-Ky-Thuat-Giao-Dich-Ngan-Han.md) | Công thức chi tiết từng chỉ báo, Elliott Wave, Ichimoku, multi-timeframe, hệ thống giao dịch hoàn chỉnh | Reference công thức khi code indicator, validation logic |

## Cách sử dụng

- **Khi implement indicator:** Tra công thức tại tài liệu #3, tra mục đích + tham số tại tài liệu #2
- **Khi thiết kế strategy template:** Tra bảng chỉ báo × chiến lược tại tài liệu #1 (Phần III)
- **Khi implement position sizing / stop loss:** Tra mô hình tại tài liệu #1 (Phần I)
- **Khi thiết kế signal scoring:** Tra flow kết hợp chỉ báo tại tài liệu #3 (Phần 16)
