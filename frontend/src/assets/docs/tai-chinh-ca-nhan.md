# Tài chính cá nhân

> Tổng quan tài sản gộp (chứng khoán + vàng + tiết kiệm + dự phòng + nhàn rỗi), sức khỏe tài chính theo 3 nguyên tắc, và kho vàng tự cập nhật giá.

---

## Tổng quan (`/personal-finance`)

Trang Tài chính cá nhân giúp bạn nhìn bức tranh tài sản ngoài phạm vi chứng khoán, đảm bảo danh mục đầu tư đang nằm trong một cấu trúc tài chính lành mạnh.

Ứng dụng quản lý **5 loại tài khoản**:

| Loại | Biểu tượng | Mô tả | Nhập số dư |
|------|------------|-------|------------|
| **Chứng khoán** | 📈 | Giá trị danh mục đầu tư cổ phiếu | Tự đồng bộ từ Portfolios |
| **Tiết kiệm** | 🏦 | Tiền gửi ngân hàng có kỳ hạn | Nhập tay (VND) |
| **Dự phòng** | 🛡️ | Quỹ khẩn cấp — tiền có thể rút ngay | Nhập tay (VND) |
| **Nhàn rỗi** | 💵 | Tiền mặt / tài khoản thanh toán | Nhập tay (VND) |
| **Vàng** | 🪙 | Vàng miếng / vàng nhẫn tích trữ | Tự tính từ giá 24hmoney hoặc nhập tay |

---

## Bước 1: Thiết lập lần đầu (Onboarding)

Lần đầu vào trang, hệ thống yêu cầu bạn nhập **chi tiêu trung bình mỗi tháng**. Đây là cơ sở để tính quỹ dự phòng cần thiết (theo mặc định = 6 tháng chi tiêu).

1. Vào **Tài chính cá nhân** (`/personal-finance`)
2. Nhập số tiền chi tiêu trung bình/tháng (VD: `20.000.000`)
3. Nhấn **Bắt đầu**

Sau khi thiết lập, bạn có thể thêm các tài khoản.

---

## Bước 2: Thêm tài khoản

Nhấn **+ Thêm tài khoản** → chọn loại và điền thông tin.

### Tiết kiệm / Dự phòng / Nhàn rỗi

- **Tên hiển thị**: VD "Tiết kiệm VCB 12 tháng"
- **Số dư (VND)**: nhập tay
- **Lãi suất (%/năm)**: optional, chỉ áp dụng với Tiết kiệm
- **Ghi chú**: optional

### Chứng khoán

Chỉ cần tạo tài khoản đại diện — **số dư tự đồng bộ** từ tổng giá trị các danh mục đầu tư (positions × giá hiện tại + cash balance). Không cần nhập tay.

### Vàng 🪙

Hỗ trợ **2 chế độ**:

#### Chế độ 1: Tự tính Balance từ giá vàng 24hmoney (khuyến nghị)

Bật toggle "Tự tính Balance từ giá vàng 24hmoney", rồi chọn:

- **Thương hiệu**: SJC / DOJI / PNJ / Khác (BTMC, BTMH, …)
- **Loại**: Vàng miếng hoặc Vàng nhẫn
- **Số lượng**: nhập theo đơn vị **lượng** (VD: `2`, `0.5`)

Ứng dụng sẽ crawl giá **Bán ra** mới nhất từ 24hmoney và hiển thị preview:

```
Giá Bán ra hiện tại: 75.500.000 / lượng
Số dư tự tính:     151.000.000
```

Mỗi lần bạn tải lại trang hoặc mở lại form, giá được làm mới tự động (cache 5 phút fresh → 6 giờ stale fallback).

#### Chế độ 2: Nhập số dư tay

Nếu bạn có loại vàng không phổ biến hoặc muốn tự định giá (VD: giá mua vào thực tế), tắt toggle và nhập số dư VND trực tiếp.

> ⚠️ **Lưu ý**: 24hmoney label "Đơn vị: triệu VNĐ/lượng" nhưng giá ứng dụng hiển thị đã là **VND đầy đủ** — không cần nhân thêm.

---

## Bước 3: Theo dõi Sức khỏe tài chính

Mỗi lần có thay đổi tài sản, hệ thống tự đánh giá qua **3 nguyên tắc** (mặc định theo tài chính cá nhân cổ điển):

| Nguyên tắc | Mặc định | Ý nghĩa |
|------------|----------|---------|
| **Quỹ dự phòng** | ≥ 6 tháng chi tiêu | Tổng (Dự phòng + Nhàn rỗi) phải đủ trang trải ít nhất 6 tháng |
| **Đầu tư tối đa** | ≤ 50% tổng tài sản | (Chứng khoán + Vàng) không nên vượt 50% — phần còn lại để phòng thủ |
| **Tiết kiệm tối thiểu** | ≥ 30% tổng tài sản | Tiết kiệm ngân hàng + Dự phòng nên chiếm ít nhất 30% |

### Health Score

Điểm tổng hợp 0–100:

- **80–100** 🟢 Lành mạnh — cả 3 nguyên tắc đạt
- **50–79** 🟡 Chú ý — 1–2 nguyên tắc chưa đạt
- **< 50** 🔴 Rủi ro — nhiều nguyên tắc vi phạm, cần điều chỉnh cơ cấu tài sản

Mỗi dòng rule hiển thị `giá trị hiện tại / giá trị yêu cầu` và ✓ (đạt) hoặc ✗ (chưa đạt) để bạn biết thiếu gì.

---

## Bước 4: Tinh chỉnh ngưỡng nguyên tắc

Mặc định 6/50/30 là khuyến nghị chung. Bạn có thể điều chỉnh theo hoàn cảnh cá nhân:

1. Mở **⚙️ Thiết lập** (dưới phần Tài khoản)
2. Chỉnh các giá trị:
   - **Chi tiêu trung bình/tháng** — cập nhật khi thu nhập/lối sống đổi
   - **Quỹ dự phòng (tháng)** — 3–12 tùy mức độ ổn định công việc
   - **Đầu tư tối đa (%)** — giảm xuống nếu gần nghỉ hưu; tăng lên nếu còn trẻ chịu được rủi ro
   - **Tiết kiệm tối thiểu (%)** — tăng lên khi có mục tiêu lớn sắp tới (mua nhà, …)
3. Nhấn **Lưu thiết lập**

Health score sẽ tính lại theo ngưỡng mới.

---

## Widget Dashboard

Trên Dashboard (`/dashboard`), nếu đã có profile tài chính, widget **"Tài chính cá nhân"** hiển thị:

- Tổng tài sản
- Health score màu (xanh / vàng / đỏ)
- Deep link sang trang chi tiết

Nếu chưa thiết lập, widget hiện CTA "Thiết lập tài chính cá nhân" để dẫn sang onboarding.

---

## Mẹo sử dụng

- **Vàng tự cập nhật**: Không cần sửa tay mỗi lần giá vàng đổi — hệ thống luôn hiển thị theo giá thị trường mới nhất khi bạn mở form hoặc tải trang.
- **Chứng khoán không cần nhập**: Giá trị chứng khoán lấy từ danh mục đầu tư hiện có — chỉ cần giao dịch bình thường, tài chính cá nhân tự cập nhật.
- **Một thương hiệu nhiều tài khoản**: Bạn có thể tạo nhiều tài khoản vàng khác nhau (VD: "SJC vợ giữ", "PNJ quà cưới") để tách mục đích sử dụng.
- **Ngưỡng linh hoạt**: Nếu bạn là người trẻ, chấp nhận rủi ro cao → có thể tăng **Đầu tư tối đa** lên 60–70% và giảm **Tiết kiệm tối thiểu** xuống 20%.
- **Quỹ dự phòng = Dự phòng + Nhàn rỗi**: Hệ thống cộng cả 2 loại này khi kiểm tra nguyên tắc quỹ dự phòng — cho phép bạn chia nhỏ (VD: 3 tháng trong Dự phòng gửi tiết kiệm online, 3 tháng Nhàn rỗi để rút ngay).
- **Xóa vs Sửa**: Chứng khoán chỉ xóa được (không sửa) vì số dư tự đồng bộ; các loại khác đều sửa được số dư, ghi chú, lãi suất.
