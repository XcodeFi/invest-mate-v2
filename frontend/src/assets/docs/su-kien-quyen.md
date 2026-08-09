# Sự kiện quyền

Cổ tức tiền mặt, cổ tức cổ phiếu và chia tách cổ phiếu. Nhập những sự kiện này để app tính đúng giá vốn và lãi/lỗ của bạn.

## Vì sao cần nhập

Ngày **giao dịch không hưởng quyền (GDKHQ)**, sàn tự động điều chỉnh giá tham chiếu xuống. Nhưng cổ phiếu hoặc tiền chỉ về tài khoản sau đó 1–2 tháng.

Nếu app không biết về sự kiện, trong khoảng thời gian đó danh mục của bạn sẽ **trông như lỗ nặng** dù thực tế không mất gì:

| Mốc | App thấy | Giá | Giá trị | Hiển thị |
|---|---|---|---|---|
| Trước GDKHQ | 1.000 CP | 30.000 | 30,0tr | +20% ✅ |
| Ngày GDKHQ | 1.000 CP | 23.077 | 23,1tr | −7,7% ❌ |
| Sau ~40 ngày | 1.300 CP | 23.077 | 30,0tr | +20% ✅ |

Kèm theo đó, cảnh báo cắt lỗ sẽ **kích hoạt nhầm** vì giá thị trường mới thấp hơn ngưỡng bạn đặt trước đó.

## Ba loại sự kiện

### Cổ tức tiền mặt

Doanh nghiệp trả tiền vào tài khoản.

**Lưu ý quan trọng về đơn vị:** "cổ tức 5%" nghĩa là 5% của **mệnh giá 10.000đ** = **500đ mỗi cổ phiếu** — không phải 5% giá thị trường. Với mã giá 55.000, đó chỉ là 0,91%.

- Giá tham chiếu bị trừ đi 500đ.
- **Giá vốn của bạn KHÔNG đổi** — vì bạn đã nhận tiền ra ngoài. Đây là thu nhập, không phải giảm vốn.
- Bị khấu trừ **thuế TNCN 5%** tại nguồn → thực nhận 475đ mỗi cổ phiếu.

Vì giá vốn không đổi mà giá thị trường bị trừ, mã trả cổ tức đều sẽ trông như lỗ dần qua năm tháng nếu chỉ nhìn cột "% lãi/lỗ". Hãy nhìn cột **"Tổng lãi/lỗ gồm cổ tức"**.

### Cổ tức cổ phiếu

Doanh nghiệp trả thêm cổ phiếu thay vì tiền.

- Tỷ lệ 30% nghĩa là "cứ 10 CP cũ nhận thêm 3 CP".
- Giá tham chiếu chia cho 1,3 → **giảm 23,08%**, không phải 30%.
- Số lượng tăng, **tổng vốn không đổi**, nên giá vốn giảm tương ứng.
- Cổ phiếu lẻ bị huỷ (137 × 1,3 = 178,1 → nhận 178 CP).

### Chia tách cổ phiếu

Cùng phép toán với cổ tức cổ phiếu. Chia tách 1:2 là "cứ 1 CP nhận thêm 1 CP" → số lượng gấp đôi, giá vốn giảm một nửa.

## Cách nhập

1. Vào **Sự kiện quyền** trên thanh điều hướng.
2. Chọn danh mục, bấm **Thêm sự kiện quyền**.
3. Nhập mã, chọn loại, điền **ngày GDKHQ** và **ngày về dự kiến**.
4. Nhập tỷ lệ:
   - Cổ tức tiền mặt → gõ % theo mệnh giá (VD: `5`).
   - Cổ tức cổ phiếu / chia tách → gõ "cứ mỗi **10** CP nhận thêm **3** CP".
5. Xem khối **xem trước** để kiểm tra số lượng và giá vốn sau điều chỉnh trước khi lưu.

## Trạng thái "chờ về"

Ngay khi lưu, app áp dụng điều chỉnh vào giá vốn và lãi/lỗ — nên bạn không bao giờ thấy lỗ giả. Nhưng phần tăng thêm được đánh dấu **"chờ về"**:

- Màn hình vị thế hiển thị `1.000 CP (+300 chờ về)`.
- Con số `1.000` là số khớp với sổ công ty chứng khoán, dùng để đối chiếu.
- Mọi phép tính lãi/lỗ, rủi ro, biểu đồ đều dùng tổng `1.300`.

Khi cổ phiếu hoặc tiền thực sự về tài khoản, bấm **Xác nhận đã về**. Badge biến mất, không con số nào thay đổi. Với cổ tức tiền mặt, app tự tạo dòng tiền tương ứng trong mục Dòng tiền, ghi rõ mã chứng khoán.

## Nhập sai thì sao

Xoá sự kiện là được — mọi con số tự tính lại như chưa từng có. Dữ liệu giao dịch gốc của bạn không bao giờ bị sửa. Nếu sự kiện cổ tức tiền mặt đã xác nhận và đã sinh dòng tiền, dòng tiền đó được xoá theo luôn.

Một ngoại lệ nhỏ: nếu kế hoạch giao dịch của bạn đang chạy trailing stop, **đỉnh giá** mà trailing stop ghi nhận đã được hạ theo sự kiện và không quay lại được khi xoá. Con số này tự phục hồi ngay khi giá lập đỉnh mới.

## Ảnh hưởng tới kế hoạch và cảnh báo

Bạn không phải sửa gì bằng tay. Sau khi nhập sự kiện, app tự quy các mức giá sau về cùng mặt bằng với giá thị trường mới:

- Giá vào, cắt lỗ, mục tiêu trong kế hoạch giao dịch.
- Ngưỡng của từng nhánh kịch bản thoát lệnh, giá kích hoạt và biên trượt của trailing stop.
- Ngưỡng cảnh báo cắt lỗ.
- Hạn mức lỗ theo ngày — không còn bị khoá giao dịch chỉ vì giá vừa được điều chỉnh.

Điều kiện dạng phần trăm ("giảm 10% thì cắt") và dạng số ngày giữ nguyên, vì chúng vốn không phụ thuộc mặt bằng giá.

## Chưa hỗ trợ

- Tự động lấy sự kiện từ nguồn dữ liệu bên ngoài — hiện phải nhập tay.
- Quyền mua ưu đãi, sáp nhập, hoán đổi cổ phiếu.
- Thống kê dài hạn (backtest, hiệu quả chiến lược, điểm kỷ luật) chưa tính sự kiện quyền.
