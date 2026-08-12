# Tiền bán chờ về (T+2)

## Vấn đề

Bán cổ phiếu xong, tiền **không về tài khoản ngay**. Chứng khoán Việt Nam thanh toán theo chu kỳ **T+2**: tiền về sau **2 phiên giao dịch** kể từ ngày khớp lệnh.

Ví dụ: bán thứ Năm 11/6 → T+1 là thứ Sáu 12/6 → T+2 là **thứ Hai 15/6**. Cuối tuần không phải phiên giao dịch nên không được tính.

Nếu quãng đó có nghỉ lễ thì ngày về đẩy ra xa hơn. Bán 12/02/2026, nghỉ Tết 16–20/02 → tiền về **23/02**, tức là 11 ngày sau khi bán.

## Ứng dụng hiển thị thế nào

Thẻ **Tiền mặt khả dụng** trên Tổng quan và Dòng vốn hiện:

```
Tiền mặt khả dụng
120.000.000 ₫
trong đó 30.000.000 ₫ chờ về — dự kiến 24/02
```

- **Số lớn** là tổng tiền — con số này khớp sổ công ty chứng khoán để bạn đối chiếu.
- **Dòng chờ về** là phần chưa thật sự dùng được. Ngày ghi bên cạnh là ngày về **xa nhất** trong các lệnh đang chờ, tức mốc mà toàn bộ tiền đã về đủ.
- Không còn gì chờ về thì dòng này ẩn hẳn.

Khi ghi lệnh MUA vượt phần tiền đã về, cửa sổ ghi lệnh hiện dòng vàng:

> Vượt tiền đã về 10.000.000 ₫ — cần ứng trước tiền bán.

Đây là **nhắc, không phải chặn**. Bạn vẫn lưu được lệnh, vì cửa sổ này ghi lại lệnh **đã khớp** — có thể bạn đã dùng dịch vụ ứng trước tiền bán của công ty chứng khoán.

## Bạn cần làm gì: nhập lịch nghỉ giao dịch

Để tính đúng, ứng dụng cần biết những ngày sàn **đóng cửa vì nghỉ lễ**. Thứ Bảy và Chủ nhật thì tự biết, không cần nhập.

**Mỗi khi HOSE công bố lịch nghỉ, nhập qua trợ lý AI.** Nói với trợ lý đại ý: *"nhập lịch nghỉ giao dịch ngày 30/04/2026 và 01/05/2026, ghi chú lễ 30/4"* — trợ lý sẽ gọi công cụ `add_market_closures`.

Nhập được một ngày, một đợt lễ, hay cả năm trong cùng một lần. Nhập lại ngày đã có thì không sao, không bị trùng. Xem lại bằng `list_market_closures` (trả về theo từng tháng), xoá một ngày nhập nhầm bằng `remove_market_closure`.

Lịch nghỉ **2026 đã có sẵn** 12 ngày: 01/01; 16–20/02 (Tết Bính Ngọ); 27/04 (Giỗ Tổ Hùng Vương); 30/04–01/05; 31/08–02/09.

## Lưu ý quan trọng

Nếu **quên nhập** ngày nghỉ của quãng thời gian đang tính, ứng dụng sẽ tính thiếu ngày nghỉ và báo tiền chờ về **ít hơn thực tế** — tức là báo bạn có nhiều tiền hơn số thật. Ứng dụng không tự phát hiện được điều này, vì "chưa nhập" và "không nghỉ" trông giống nhau.

Vì vậy có hai chỗ để bạn tự soát:

1. **Ngày về dự kiến** luôn hiện cạnh số tiền chờ về — thấy ngày rơi vào tuần nghỉ lễ mà vẫn tính như ngày thường thì biết là thiếu.
2. **Bản tin hằng ngày** in `market_closures_known_through` — ngày nghỉ xa nhất bạn đã nhập. Mốc này cũ đi so với hôm nay là dấu hiệu cần nhập tiếp.

## Chưa làm

**Cổ phiếu mua chờ về** chưa được mô hình hoá — mua hôm nay thì ứng dụng vẫn cho ghi lệnh bán trong ngày, dù thực tế cổ phiếu cũng về theo T+2. Ứng dụng là sổ ghi nhận sau khi khớp, không phải hệ thống chặn lệnh.
