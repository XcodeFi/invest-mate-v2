# Giao dịch

> Hướng dẫn ghi nhận giao dịch mua/bán cổ phiếu trong Investment Mate.

---

## Tạo giao dịch (`/trades/create`)

### Các bước thực hiện

1. Vào **Giao dịch** → nhấn **"Tạo mới"**
2. **Chọn danh mục**: Dropdown hiển thị tên + tổng vốn
3. **Nhập mã cổ phiếu**: Gõ mã (VD: FPT) — tự động viết HOA
4. **Chọn loại**: Mua hoặc Bán
5. **Nhập giá và số lượng**: Phí giao dịch + thuế tự tính
6. Nhấn **Lưu**

### Auto-suggest thông minh

- **Chọn danh mục → gợi ý cổ phiếu**: Hiện chips các mã đang có vị thế — nhấn để chọn nhanh
- **Nhập cổ phiếu → gợi ý danh mục**: Tự chọn danh mục chứa vị thế (nếu chỉ có 1), highlight "Có vị thế" (nếu nhiều)
- **Bán**: Chỉ hiện mã có số lượng > 0. Cảnh báo đỏ nếu mã không khớp danh mục

### Lệnh MUA lưu ý

- Bắt buộc **bội số 100** (lô chẵn)
- Giá trị lệnh không được vượt tiền còn lại trong danh mục

### Lệnh BÁN lưu ý

- Hiển thị thông tin vị thế: số CP đang giữ, giá trung bình, P&L hiện tại
- Cảnh báo nếu bán mã không có trong danh mục đã chọn

---

## Wizard giao dịch (`/trade-wizard`)

Flow 5 bước dẫn dắt giao dịch có kỷ luật — bắt buộc hoàn thành từng bước:

| Bước | Nội dung | Ghi chú |
|:---:|---------|---------|
| 1 | **Chọn Chiến lược** | Tùy chọn, có thể bỏ qua |
| 2 | **Lập Kế hoạch** | Entry, SL, TP, Position Sizing tự tính |
| 3 | **Checklist** | 8 mục kiểm tra (5 bắt buộc) → GO/NO-GO |
| 4 | **Xác nhận & Ghi GD** | Tóm tắt toàn bộ → tạo giao dịch |
| 5 | **Nhật ký** | Tự điền từ thông tin ở bước trên |

**Lưu ý**: Không thể sang bước tiếp nếu chưa đủ điều kiện. Đây là thiết kế có chủ đích giúp bạn giao dịch có kỷ luật.

---

## Lịch sử giao dịch (`/trades`)

- **Bảng giao dịch**: Symbol, loại (Mua/Bán), giá, số lượng, ngày, P&L
- **Lọc nhanh**: Nhấn vào mã CP trong bảng → tự động filter theo mã đó
- **Gắn kế hoạch**: Nút "Gắn KH" cho giao dịch chưa liên kết Trade Plan
- **Phân trang**: Duyệt lịch sử giao dịch theo trang

---

## Import CSV (`/trades/import`)

Nhập giao dịch hàng loạt từ file CSV:

1. Nhấn **"Import CSV"** trên trang Giao dịch
2. Upload file CSV (hỗ trợ dấu phẩy, chấm phẩy, tab)
3. Xem trước dữ liệu — hệ thống validate từng dòng
4. Hiện số dòng hợp lệ / lỗi
5. Nhấn **Import** → kết quả hiển thị chi tiết

---

## Xem vị thế đang mở (`/positions`)

- Gom nhóm theo danh mục
- Mỗi vị thế hiện: mã, số lượng, giá TB, giá hiện tại, P&L (xanh/đỏ)
- **Thanh SL/TP**: Gradient từ SL → TP với marker giá hiện tại
- **Sắp xếp**: Theo giá trị, lãi/lỗ, %, hoặc mã CK
