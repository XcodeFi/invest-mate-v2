# Tài chính cá nhân

> Tổng quan tài sản (chứng khoán + vàng + tiết kiệm + dự phòng + nhàn rỗi), **khoản nợ**, **Net Worth = Tài sản − Nợ**, sức khỏe tài chính theo **4 nguyên tắc**, và kho vàng tự cập nhật giá.

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

Khi hoàn thành onboarding, hệ thống **tự động tạo 4 tài khoản mặc định**: Chứng khoán, Tiết kiệm, Quỹ dự phòng, Tiền nhàn rỗi (số dư 0đ). Vàng tạo khi có nhu cầu.

Nhấn **+ Thêm tài khoản** → chọn loại và điền thông tin (dropdown chỉ hiện Tiết kiệm / Dự phòng / Nhàn rỗi / Vàng — Chứng khoán không tạo thủ công được).

### Tiết kiệm / Dự phòng / Nhàn rỗi

- **Tên hiển thị**: VD "Tiết kiệm VCB 12 tháng"
- **Số dư (VND)**: nhập tay
- **Lãi suất (%/năm)**: optional, chỉ áp dụng với Tiết kiệm
- **Ngày mở sổ** (chỉ Tiết kiệm, optional): ngày bắt đầu gửi sổ
- **Ngày đáo hạn** (chỉ Tiết kiệm, optional): ngày sổ hết kỳ hạn
- **Ghi chú**: optional

#### Chip kỳ hạn chuẩn

Sau khi nhập Ngày mở sổ, xuất hiện hàng chip nhanh: `[1T] [3T] [6T] [12T] [24T] [Tùy chỉnh]`. Bấm chip để auto-tính Ngày đáo hạn (Ngày mở sổ + N tháng). Bấm "Tùy chỉnh" để xóa ngày đáo hạn và nhập tay (cho kỳ hạn lạ, VD 9 tháng).

**Tại sao nên nhập?** Chuẩn bị cho tính năng sắp tới: nhắc đáo hạn để tái tục hoặc rút, và so sánh hiệu suất đầu tư với tiết kiệm (opportunity cost). Nếu là **sổ không kỳ hạn**, bỏ trống cả 2 là OK.

### Chứng khoán

Tài khoản Chứng khoán được **tự động tạo** khi khởi tạo profile — **số dư tự đồng bộ** từ tổng giá trị các danh mục đầu tư (positions × giá hiện tại + cash balance). Không thể tạo thủ công, không thể xóa, không thể sửa. Card hiển thị nhãn "Auto-sync" để nhận biết.

### Vàng 🪙

Hỗ trợ **2 chế độ**:

#### Chế độ 1: Tự tính Balance từ giá vàng 24hmoney (khuyến nghị)

Bật toggle "Tự tính Balance từ giá vàng 24hmoney", rồi chọn:

- **Thương hiệu**: SJC / DOJI / PNJ / Khác (BTMC, BTMH, …)
- **Loại**: Vàng miếng hoặc Vàng nhẫn
- **Số lượng**: nhập theo đơn vị **lượng** (VD: `2`, `0.5`)

Ứng dụng sẽ crawl giá **Mua vào** mới nhất từ 24hmoney (giá tiệm mua vào = giá bạn bán được nếu thanh khoản ngay) và hiển thị preview:

```
Giá mua vào hiện tại: 74.000.000 / lượng
Số dư tự tính:      148.000.000
```

> Vì sao dùng giá mua vào? Để định giá **tài sản đang giữ** theo giá có thể thanh lý thực tế, không phải giá đi mua thêm. Chênh lệch mua–bán (spread) 1–3 triệu/lượng sẽ không bị cộng ảo vào tổng tài sản.

Mỗi lần bạn tải lại trang hoặc mở lại form, giá được làm mới tự động (cache 5 phút fresh → 6 giờ stale fallback).

#### Chế độ 2: Nhập số dư tay

Nếu bạn có loại vàng không phổ biến hoặc muốn tự định giá (VD: giá mua vào thực tế), tắt toggle và nhập số dư VND trực tiếp.

> ⚠️ **Lưu ý**: 24hmoney label "Đơn vị: triệu VNĐ/lượng" nhưng giá ứng dụng hiển thị đã là **VND đầy đủ** — không cần nhân thêm.

---

## Bước 3: Theo dõi khoản nợ

Section "Khoản nợ" dưới phần Tài khoản cho phép track các khoản nợ ảnh hưởng đến Net Worth. Nhấn **+ Thêm khoản nợ** → chọn 1 trong 6 loại:

| Loại | Biểu tượng | Ví dụ | Có bị tính vào rule "nợ tiêu dùng"? |
|------|-----------|-------|------------------------------------|
| **Thẻ tín dụng** | 💳 | Thẻ VCB Platinum | ✅ (lãi thường 24-36%) |
| **Vay tiêu dùng** | 💸 | Vay tín chấp, vay người quen | ✅ (lãi 15-25%) |
| **Vay mua nhà** | 🏠 | Vay BIDV 20 năm | ❌ (có bảo đảm, lãi thấp) |
| **Vay mua xe** | 🚗 | Vay mua ô tô | ❌ |
| **Trả góp / BNPL** | 📱 | Trả góp 0% điện thoại, đồ điện tử | ❌ |
| **Khác** | 📄 | Các khoản nợ khác | ❌ |

Fields:
- **Số gốc còn lại (Principal)**: bắt buộc, VND
- **Lãi suất (%/năm)**: optional nhưng **nên** nhập — dùng cho rule nợ lãi cao
- **Trả hàng tháng**: optional, giúp bạn track cash flow
- **Ngày đáo hạn**: optional, dùng date picker
- **Ghi chú**: optional

### Net Worth = Tài sản − Nợ

Card nổi bật ở đầu trang hiển thị Net Worth (màu xanh lá khi dương, đỏ khi âm). Dashboard widget cũng đổi từ Tổng tài sản sang Net Worth làm số chính — số tiền thực sự bạn "sở hữu" sau khi trừ nợ.

### Xóa khoản nợ

Tương tự tài khoản: nhấn vào thẻ → mở popup → nút **Xóa** chỉ hiện khi **Principal = 0** (đã trả hết). Logic này chống xóa nhầm dữ liệu nợ thật — muốn xóa tất toán xong thì set số gốc về 0 → Lưu → mở lại → Xóa.

---

## Bước 4: Theo dõi Sức khỏe tài chính

Mỗi lần có thay đổi tài sản hoặc nợ, hệ thống tự đánh giá qua **4 nguyên tắc**:

| Nguyên tắc | Mặc định | Ý nghĩa | Điểm trừ tối đa |
|------------|----------|---------|-----------------|
| **Quỹ dự phòng** | ≥ 6 tháng chi tiêu | Tổng (Dự phòng + Nhàn rỗi) phải đủ trang trải ít nhất 6 tháng | −40 |
| **Đầu tư tối đa** | ≤ 50% tổng tài sản | (Chứng khoán + Vàng) không nên vượt 50% | −30 |
| **Tiết kiệm tối thiểu** | ≥ 30% tổng tài sản | Tiết kiệm ngân hàng + Dự phòng nên chiếm ít nhất 30% | −30 |
| **Không nợ tiêu dùng lãi cao** | Không có CC/vay tiêu dùng lãi > 20%/năm | Trả nợ lãi cao trước khi đầu tư — nợ 28% ăn mòn lợi nhuận nhanh hơn nhiều cổ phiếu tạo ra | −20 (binary) |

3 rule đầu trừ điểm tỷ lệ thuận với mức vi phạm. Rule 4 là binary — vi phạm trừ hẳn 20, không vi phạm trừ 0. Nếu score đạt 100 nhưng có CC lãi 28% → tự động xuống 80/100 🟡.

Khi vi phạm rule 4, trang PF hiện **banner đỏ** + Dashboard widget cũng hiện cảnh báo để nhắc bạn:
> ⚠️ Bạn có nợ thẻ tín dụng / tiêu dùng lãi > 20%/năm. Trả nợ này thường là khoản "đầu tư" lãi kép tốt nhất trước khi mua cổ phiếu.

### Health Score

Điểm tổng hợp 0–100:

- **80–100** 🟢 Lành mạnh — cả 3 nguyên tắc đạt
- **50–79** 🟡 Chú ý — 1–2 nguyên tắc chưa đạt
- **< 50** 🔴 Rủi ro — nhiều nguyên tắc vi phạm, cần điều chỉnh cơ cấu tài sản

Mỗi dòng rule hiển thị `giá trị hiện tại / giá trị yêu cầu` và ✓ (đạt) hoặc ✗ (chưa đạt) để bạn biết thiếu gì.

---

## Bước 5: Tinh chỉnh ngưỡng nguyên tắc

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

## Sửa / Xóa tài khoản

- **Nhấn vào thẻ (card) tài khoản** → mở popup chỉnh sửa (áp dụng cho Tiết kiệm / Dự phòng / Nhàn rỗi / Vàng).
- Trong popup: nút **Lưu** lưu thay đổi, nút **Xóa** xóa tài khoản, nút **Hủy** (hoặc phím **ESC**) đóng popup.
- **Điều kiện xóa**: chỉ xóa được khi **số dư = 0đ**. Nếu còn tiền, popup hiển thị nhắc "Đặt số dư về 0 trước" — đây là lớp bảo vệ chống xóa nhầm dữ liệu thật.
- **Thẻ Chứng khoán không bấm được** (hiển thị "Auto-sync" bên phải): không sửa, không xóa thủ công.

## Mẹo sử dụng

- **Vàng tự cập nhật**: Không cần sửa tay mỗi lần giá vàng đổi — hệ thống luôn hiển thị theo giá thị trường mới nhất khi bạn mở form hoặc tải trang.
- **Chứng khoán không cần nhập**: Giá trị chứng khoán lấy từ danh mục đầu tư hiện có — chỉ cần giao dịch bình thường, tài chính cá nhân tự cập nhật.
- **Một thương hiệu nhiều tài khoản**: Bạn có thể tạo nhiều tài khoản vàng khác nhau (VD: "SJC vợ giữ", "PNJ quà cưới") để tách mục đích sử dụng.
- **Ngưỡng linh hoạt**: Nếu bạn là người trẻ, chấp nhận rủi ro cao → có thể tăng **Đầu tư tối đa** lên 60–70% và giảm **Tiết kiệm tối thiểu** xuống 20%.
- **Quỹ dự phòng = Dự phòng + Nhàn rỗi**: Hệ thống cộng cả 2 loại này khi kiểm tra nguyên tắc quỹ dự phòng — cho phép bạn chia nhỏ (VD: 3 tháng trong Dự phòng gửi tiết kiệm online, 3 tháng Nhàn rỗi để rút ngay).
- **Muốn xóa tài khoản không dùng**: mở popup → đặt số dư về 0 → **Lưu**, sau đó mở lại popup → nhấn **Xóa**.
