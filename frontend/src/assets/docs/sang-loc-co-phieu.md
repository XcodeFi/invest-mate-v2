# Sàng lọc cổ phiếu

## Trang này làm gì

Giữa hàng trăm mã trên sàn, trang **Sàng lọc cổ phiếu** (mở từ trang Dữ liệu Thị trường, hoặc chip **Danh sách lọc (không xếp hạng)** ở Bảng giá nhanh) quét toàn thị trường theo tiêu chí và cho ra danh sách các mã **thoả tiêu chí**.

> **Đây là danh sách lọc theo tiêu chí, không phải khuyến nghị mua.** Không có điểm số, không có thứ hạng "tiềm năng nhất". Danh sách xếp theo bảng chữ cái; bạn tự bấm vào tiêu đề cột để sắp theo thanh khoản, vốn hoá, ROE, P/E hay sức mạnh 3 tháng.

## Ba tầng lọc

**Tầng 1 — loại bỏ** (mã bị loại ghi rõ lý do đầu tiên không đạt):

| Luật | Mặc định |
|---|---|
| Thuộc diện đặc biệt của HOSE (cảnh báo, kiểm soát, hạn chế giao dịch, tạm ngừng, đình chỉ) | loại, kèm tên danh sách và lý do HOSE nêu |
| HOSE đã công bố hủy niêm yết (năm nay hoặc năm sau) | loại |
| Thanh khoản: giá trị giao dịch bình quân 10 phiên | từ 5 tỷ đồng/phiên |
| Giá | từ 5.000 đ |
| Vốn hoá | từ 1.000 tỷ đồng |

**Tầng 2 — chất lượng** (có thể tắt bằng **Bỏ qua tầng chất lượng**):

- Lợi nhuận sau thuế dương.
- ROE từ 12%.
- **Trần P/E** là tuỳ chọn và **mặc định tắt**: P/E cao không phải lý do loại ngầm định. Chỉ khi bạn nhập một số vào ô "Trần P/E" thì mới lọc theo P/E.

**Tầng 3 — xu hướng:** xếp hạng sức mạnh giá 3 tháng từ **P50** trở lên **trong toàn bộ mã niêm yết**. Con số này là **xếp hạng phân vị 1–99** (99 = mạnh hơn 99% số mã), **không phải tỉ suất sinh lời** và không phải "mạnh hơn VN-Index". Chỉnh ngưỡng ở ô "Xếp hạng sức mạnh 3 tháng tối thiểu". Mã nguồn không có xếp hạng này bị loại với lý do **Thiếu dữ liệu xu hướng** (không bị bỏ qua lặng lẽ).

Mặc định quét **HOSE và HNX**. **UPCOM** chỉ khi bạn tick, vì nhiều mã thanh khoản thấp, hay đầu cơ và chưa có danh sách cảnh báo chính thức.

## Cờ biến động

Mã đạt có thể kèm **cờ biến động**. Cờ **chỉ để bạn cân nhắc, không loại mã**, và mỗi cờ ghi giá trị:

- `RS1T 96/99, vốn hoá nhỏ`: xếp hạng sức mạnh 1 tháng từ 95 và vốn hoá dưới 5.000 tỷ.
- `beta cao (2.1)`: beta trên 1,8.
- `RS52T 95/99, vốn hoá nhỏ`: xếp hạng sức mạnh 52 tuần từ 90 và vốn hoá dưới 5.000 tỷ.
- `tăng nóng rồi gãy`: xếp hạng 52 tuần từ 90 nhưng 1 tháng chỉ từ 20 trở xuống.

Cờ dựa trên **xếp hạng tương đối**, không phải đánh giá rủi ro.

## Đọc kết quả

- **Mã đạt tiêu chí**: bảng chính. Bấm **Vì sao đạt** để xem từng tầng đã qua. Dòng HNX/UPCOM có ghi chú *chưa kiểm được danh sách cảnh báo*.
- **Chưa kiểm được trạng thái (HOSE)**: nếu không đọc được danh sách diện đặc biệt của HOSE, các mã HOSE qua mọi tầng được tách riêng ở đây và **không** xếp vào danh sách đạt, vì chưa biết chúng có bị cảnh báo hay không.
- **Mã bị loại theo lý do** và **theo ngành**: để thấy tiêu chí đang loại nhiều nhất ở đâu, và có nghiêng về ngành nào không (ví dụ ROE ≥ 12% loại nhiều mã ngành xây dựng, bất động sản).
- Dòng **"N mã bị loại vì định giá/chất lượng nhưng xu hướng tốt"**: những mã chỉ trượt tầng chất lượng hoặc trần P/E. Muốn xem thì bật "Bỏ qua tầng chất lượng" hoặc nới ROE.
- **Vì sao mã X không có?**: nhập một mã để biết nó đạt, bị loại (kèm lý do) hay không có trong dữ liệu.

## Gửi mã sang Bảng giá nhanh

Tick các mã muốn xem (tối đa 30) rồi bấm **Gửi N mã sang Bảng giá nhanh**. Trang Thị trường mở ra với đúng những mã đó và nhớ cho lần sau. **Danh sách này thay thế danh sách Bảng giá nhanh bạn đã nhớ trước đó.** Không có gì tự điền. Nếu trình duyệt chặn lưu trữ (chế độ riêng tư), trang sẽ báo và hiện danh sách để bạn tự gõ sang.

## Thêm mã vào Watchlist

Tick các mã (tối đa 30), chọn watchlist (mặc định là watchlist mặc định của bạn) rồi bấm **⭐ Thêm N mã vào watchlist**. Mã nào **đã có sẵn** trong watchlist đó thì được bỏ qua, không lỗi và không đổi ghi chú cũ; trang báo **"Đã thêm N, bỏ qua M (đã có)"**.

- Mã mới thêm có ghi chú tự điền, ngắn, dạng **"Từ sàng lọc 28/11 · ROE ≥ 12 · Sức mạnh 3T ≥ P50"** (đúng tiêu chí của lần lọc đang xem). Bạn sửa ghi chú ở trang Watchlist như mọi mã khác.
- Mã đã nằm trong **bất kỳ** watchlist nào của bạn có **⭐** cạnh mã ngay từ khi trang mở.
- Chưa có watchlist nào thì nút bị khoá; tạo một watchlist ở trang Watchlist trước.
- Nút này chỉ **thêm**, không bao giờ xoá hay sửa mã đã có.

### Để trợ lý AI thêm giúp mỗi ngày

Trợ lý AI nối qua khoá API có công cụ `add_watchlist_items` (thêm tối đa 30 mã một lần, mã đã có thì bỏ qua) và `screen_stocks` (mỗi dòng đạt có cờ `inWatchlist`). Gợi ý cách chạy: **sau giờ đóng cửa (khoảng 16:00)**, khi dữ liệu đã phản ánh phiên vừa xong, trợ lý gọi `screen_stocks`, **bỏ qua lần chạy nếu `status` khác `ok` hoặc `stale` là true** (và báo lại), tìm hoặc tạo watchlist riêng tên **"🔎 Từ sàng lọc"**, rồi thêm các mã đạt vào đó với ghi chú "Từ sàng lọc dd/MM". Đầu phiên sáng dữ liệu vẫn là của phiên trước, nên lúc đó chỉ nên xem lại danh sách. Trợ lý chỉ thêm vào watchlist riêng của nó, không xoá gì và không ghi vào danh sách bạn tự quản lý. Việc đặt lịch chạy là việc của bạn với trợ lý (ứng dụng không tự chạy lịch).

## Giới hạn cần biết

- **Nguồn không chính thức**: dữ liệu lấy từ 24hmoney và danh sách công khai của HOSE, có thể trễ hoặc đổi dạng. Dữ liệu được lưu tạm tối đa 6 giờ.
- **Dữ liệu cũ**: nếu làm mới lỗi, trang dùng bản tốt gần nhất và báo **Dữ liệu cũ** kèm giờ; quá 48 giờ thì không hiện kết quả và nói rõ lý do.
- **HNX và UPCOM** chưa có danh sách cảnh báo chính thức để đối chiếu.
- **Sống sót**: mã đã hủy niêm yết không còn trong dữ liệu, nên không thể kiểm tra "mã này từng ra sao".
- Mã thiếu giá hoặc vốn hoá ở nguồn không quét được.
- Các ngưỡng (ROE 12%, thanh khoản 5 tỷ, P50...) là lựa chọn có chủ ý, không phải chân lý. Mọi ngưỡng chỉnh được.
