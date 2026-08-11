# Hồ sơ công ty

> Chặn mua theo tin — viết hồ sơ hiểu doanh nghiệp trước khi được phép lập kế hoạch mua.

---

## Vì sao có tính năng này

"Lý do đầu tư" (Thesis) trả lời **vì sao mua LÚC NÀY** — nó gắn với một lệnh, sống vài tuần. Còn "doanh nghiệp này kiếm tiền bằng gì, lợi thế bền ở đâu, cái gì phá được nó" sống theo **mã** và theo **quý** — nếu viết vào Thesis thì mua lại mã cũ lần thứ năm vẫn phải gõ lại từ đầu, và thực tế sẽ thành copy-paste.

Gate cũ (`Thesis ≥ 30 ký tự`) chỉ đếm được độ dài, không đếm được hiểu biết: "HPG đầu ngành thép, triển vọng tốt, kỳ vọng tăng giá" đủ 45 ký tự và không chứa một thông tin kiểm chứng được nào. Hồ sơ công ty ép bạn trả lời trước khi xuống tiền — không phải sau khi đã quyết mua rồi mới tìm lý do.

**Nguyên tắc cốt lõi: mỗi rủi ro phải có dấu hiệu quan sát được** — thứ bạn sẽ thực sự thấy nếu rủi ro đó đang xảy ra. "Rủi ro cạnh tranh" là một cảm giác. "Biên gộp giảm quá 3 điểm % trong 2 quý liên tiếp" là một dấu hiệu — bạn kiểm được nó có xảy ra hay không, không cần đoán.

---

## Mở hồ sơ ra là để đọc

Hồ sơ viết xong thì phần lớn thời gian bạn mở nó ra **để đọc lại trước khi vào lệnh**, không phải để sửa. Nên `/company-dossier/{mã}` mặc định hiện **bản đọc**: mô hình kinh doanh là đoạn văn, moat là các thẻ, rủi ro xếp theo hạng với hạng 1 nổi bật nhất và dấu hiệu quan sát in ngay dưới mô tả.

Muốn sửa thì bấm **Sửa** ở góc trên bên phải. Đang sửa mà đổi ý thì bấm **Hủy** — nếu bạn đã gõ gì đó, hệ thống hỏi lại trước khi bỏ. Lưu xong thì trang tự về bản đọc.

Hai trường hợp vào thẳng form, không qua bản đọc:

- **Mã chưa có hồ sơ** — không có gì để đọc.
- **Bạn bị cổng chặn lệnh đá sang đây** — lúc đó việc cần làm là viết.

---

## Viết hồ sơ cho một mã

Vào **`/company-dossier`** → chọn mã (hoặc bấm "Tạo Trade Plan từ gợi ý" ở trang thị trường cho mã chưa có hồ sơ — hệ thống sẽ đưa bạn sang đây, giữ nguyên entry/SL/TP đã điền).

**Số liệu doanh nghiệp nằm ngay bên phải ô viết** — P/E, P/B, ROE, ROA, EPS, vốn hóa, đỉnh/đáy 52 tuần, đơn vị kiểm toán, doanh thu và lợi nhuận từng quý, cổ phiếu cùng ngành, cổ tức, kế hoạch kinh doanh, cổ đông lớn, ban lãnh đạo. Bạn không cần mở tab khác để tra.

Hai điều cần biết về khối số liệu này:

- **Nó là nguyên liệu, không phải điều kiện.** Số liệu đẹp không làm hồ sơ đủ điều kiện — hệ thống chỉ đọc những gì bạn tự viết.
- **Phần nào ghi "không lấy được dữ liệu" nghĩa là chưa tra được, không phải bằng 0.** Nguồn số liệu (24hmoney) đôi lúc thiếu một vài phần. Đừng kết luận doanh nghiệp không có doanh thu chỉ vì bảng doanh thu trống.

### 1. Doanh nghiệp này kiếm tiền bằng gì?

Nêu **sản phẩm/dịch vụ** và **ai trả tiền**. "Tiềm năng", "đầu ngành", "triển vọng tốt" **không phải câu trả lời** — đó là cảm giác, không phải sự thật kiểm chứng được.

- Tốt: "Bán thép xây dựng và thép cuộn cán nóng (HRC) cho nhà thầu xây dựng và nhà sản xuất nội địa."
- Không đạt: "Doanh nghiệp đầu ngành thép, triển vọng tốt trong dài hạn."

Lệnh nhỏ (< 5% tài khoản) chỉ cần một câu không rỗng. Lệnh ≥ 5% tài khoản cần ≥ 30 ký tự.

### 2. Lợi thế bền (Moat)

Cái gì khiến đối thủ khó cướp khách hàng hoặc khó hạ giá của doanh nghiệp này? Viết cụ thể, không viết khẩu hiệu.

- Tốt: "Lò cao công suất lớn nhất nội địa, chi phí sản xuất/tấn thấp hơn đối thủ 10-15% nhờ quy mô."
- Không đạt: "Thương hiệu mạnh, uy tín lâu năm."

### 3. Rủi ro xếp hạng, mỗi rủi ro kèm dấu hiệu quan sát được

Liệt kê rủi ro **trước khi quyết mua**, không phải sau. Xếp hạng 1..N theo mức nguy hiểm — hạng 1 là rủi ro đáng ngại nhất. Mỗi rủi ro **bắt buộc** có một câu "biết nó đang xảy ra bằng gì" (Observable Signal). Không có dấu hiệu thì đó không phải rủi ro, chỉ là một nỗi lo mơ hồ.

| Rủi ro | Dấu hiệu quan sát được (ví dụ) |
|---|---|
| Giá HRC Trung Quốc giảm mạnh, ép giá bán nội địa | "Giá HRC Trung Quốc giảm quá 10% trong 1 tháng trên các sàn giao dịch hàng hóa" |
| KQKD không đạt kỳ vọng | "BCTC quý có EPS tăng trưởng dưới 20% YoY, hoặc trích lập dự phòng cao hơn 2 lần quý trước" |
| Gãy xu hướng kỹ thuật | "Đóng cửa dưới MA200 kèm khối lượng giao dịch trên 2 lần trung bình 20 phiên" |
| Tin tức thay đổi bản chất doanh nghiệp | "Lãnh đạo bị khởi tố, doanh nghiệp chậm thanh toán trái phiếu, hoặc UBCKNN xử phạt thao túng giá" |
| Giữ lâu mà thesis không thể hiện | "Giữ quá 90 ngày mà giá đi ngang ± 3% kèm thanh khoản dưới 50% trung bình năm" |

Bạn có thể chọn **loại kích hoạt gợi ý** (Suggested Trigger) cho mỗi rủi ro — một trong 5 loại có sẵn (bảng trên tương ứng với thứ tự các loại). Khi lập kế hoạch mua cho mã đã có hồ sơ, hệ thống sẽ đề xuất sẵn các điều kiện hủy thesis dựa trên Top-3 rủi ro nguy hiểm nhất — bạn tick cái nào muốn dùng, không tự động thêm.

**Yếu tố hủy diệt (Deal-breaker):** đánh dấu **tối đa 1** rủi ro là "hủy diệt" — cái mà nếu xảy ra thì bán hết, không phải chỉ cắt một phần. Đánh dấu nhiều hơn một thì từ "hủy diệt" mất nghĩa.

Lệnh nhỏ cần ≥ 1 rủi ro có dấu hiệu. Lệnh ≥ 5% tài khoản cần ≥ 3 rủi ro, mỗi dấu hiệu ≥ 20 ký tự.

### 4. Ghi chú tự do

Chỗ ghi thêm — cơ cấu cổ đông, ban lãnh đạo, pha loãng, tập trung khách hàng, đòn bẩy, dòng tiền... bất cứ gì bạn thấy cần lưu. **Không ảnh hưởng điều kiện chặn** — viết hay không viết, dài hay ngắn đều không tính vào việc hồ sơ có "đủ" hay không.

---

## Ký xác nhận

Nút ký nằm ở **cuối trang**, sau toàn bộ nội dung — không đặt cạnh nút Lưu, để không ai bấm theo phản xạ mà chưa đọc lại từ đầu.

Nhãn nút thay đổi theo tình huống:

| Tình huống | Nhãn nút |
|---|---|
| Chưa từng ký | "Tôi đã đọc và chịu trách nhiệm" |
| Đã ký, còn hiệu lực | "Vẫn đúng" |
| Đã hết hạn (≥ 180 ngày) | "Đã cập nhật tin mới và xác nhận" |

**Chỉ bấm nút ký mới tính là đã xác nhận.** Sửa nội dung — kể cả bạn tự sửa — không tự động xác nhận lại. Nếu hồ sơ đã hết hạn, sửa nội dung xong vẫn hết hạn cho tới khi bạn bấm ký.

**Còn thay đổi chưa lưu thì chưa ký được.** Nút ký khoá lại kèm dòng nhắc bấm Lưu trước. Lý do: chữ ký đóng dấu vào **bản đang nằm trên server**, nên nếu màn hình đang hiện nội dung khác thì bạn sẽ ký một thứ mình không đọc.

### Khi trợ lý AI cập nhật hồ sơ

Trợ lý AI (NPU/Claude) có thể tra dữ liệu và soạn hộ nội dung hồ sơ, nhưng **không có cách nào để trợ lý tự ký**. Nếu trợ lý vừa cập nhật một hồ sơ đã ký trước đó, hồ sơ tụt về trạng thái "chưa xác nhận" và trang sẽ hiện dòng "Agent đã cập nhật lúc … — chưa xác nhận". Bạn phải mở trang, đọc lại nội dung mới, rồi mới ký. Đây là thiết kế có chủ đích: một hồ sơ mà trợ lý tự viết và tự ký thì không đo được việc bạn — người bỏ tiền — có hiểu doanh nghiệp hay không.

---

## Hỏi một trợ lý AI khác

Trợ lý nối được MCP (Claude qua NPU) thì đã sửa hồ sơ trực tiếp được. Với trợ lý **không** nối MCP — ChatGPT trên web, Gemini — trang có hai nút ở góc trên bên phải:

**Sao chép cho AI** — chép vào clipboard toàn bộ hồ sơ hiện tại cộng số liệu doanh nghiệp, kèm sẵn câu hỏi và khuôn JSON để trợ lý trả lời đúng định dạng. Dán thẳng vào ChatGPT là xong, không phải tự gõ lại gì.

**Dán từ AI** — dán nguyên văn câu trả lời vào ô, bấm "Đổ vào form". Hệ thống đọc khối JSON cuối cùng trong đó (phần giải thích dài dòng phía trước cứ để nguyên).

Ba điều hệ thống làm giúp khi dán:

- **Nội dung của mã khác thì bị chặn.** Dán bản soạn cho VNM vào trang HPG sẽ báo lỗi chứ không đổ vào — đây là loại nhầm mà sửa xong ký luôn thì về sau không ai phát hiện được.
- **Chỉ giữ một yếu tố hủy diệt**, thứ tự rủi ro được đánh lại 1, 2, 3 theo đúng thứ tự trợ lý trả về.
- **Kịch bản vô hiệu hoá lạ** (không thuộc danh sách có sẵn) được bỏ trống thay vì nhận bừa.

**Dán không lưu và không ký.** Nội dung chỉ nằm trong form để bạn đọc lại — muốn giữ thì tự bấm Lưu, muốn xác nhận thì tự bấm Ký. Rời trang mà chưa lưu là mất bản dán, đúng như mọi form khác.

---

## Hạn tươi của hồ sơ

| Trạng thái | Điều kiện | Ảnh hưởng khi lập kế hoạch mua |
|---|---|---|
| **Chưa xác nhận** | Chưa từng ký | Bị chặn — coi như chưa có hồ sơ |
| **Còn mới** | Đã ký, dưới 90 ngày | Bình thường |
| **Cần soát lại** | Đã ký, 90-179 ngày | **Vẫn lập được kế hoạch** — chỉ là lời nhắc nên xem lại tin tức mới |
| **Đã hết hạn** | Đã ký, từ 180 ngày | Bị chặn — phải cập nhật và ký lại |

---

## Ngưỡng đủ nội dung theo size lệnh

Kế hoạch mua nhỏ (dưới 5% tài khoản, hoặc chưa nhập số dư tài khoản) chỉ cần hồ sơ ở mức tối thiểu. Kế hoạch từ 5% tài khoản trở lên cần đủ cả 4 khối với độ dài tối thiểu — cùng ngưỡng 5% với gate "Lý do đầu tư" đã có từ trước.

| | Lệnh nhỏ | Lệnh ≥ 5% tài khoản |
|---|---|---|
| Doanh nghiệp kiếm tiền bằng gì | Không rỗng | ≥ 30 ký tự |
| Lợi thế bền | ≥ 1 | ≥ 1, có ít nhất 1 mô tả ≥ 30 ký tự |
| Rủi ro | ≥ 1, có dấu hiệu | ≥ 3, mỗi dấu hiệu ≥ 20 ký tự |

Khi form lập kế hoạch đã có đủ mã + số lượng + giá vào + số dư tài khoản, form sẽ tự kiểm tra và hiện ngay trạng thái hồ sơ — biết trước có bị chặn hay không, khỏi phải bấm Lưu rồi mới thấy báo lỗi. Cảnh báo này **không** khóa nút Lưu; nó chỉ báo trước.

---

## Hồ sơ hiện trên dòng thời gian của mã

Mở dòng thời gian của một mã (bấm thẳng vào mã ở bất kỳ danh sách nào trong app) sẽ thấy mốc hồ sơ nằm chung với nhật ký, lệnh và cảnh báo:

| Mốc | Nghĩa |
|---|---|
| 📋 **Ký hồ sơ công ty** | Lần bạn xác nhận đã đọc và chịu trách nhiệm về nội dung hồ sơ |
| 📋 **Trợ lý AI sửa hồ sơ — chờ bạn ký lại** | Lần trợ lý soạn lại nội dung. Từ lúc này hồ sơ mất chữ ký và **đang chặn lập kế hoạch** cho tới khi bạn đọc và ký lại |

Bỏ tick ô **📋 Hồ sơ công ty** ở bộ lọc phía trên để ẩn các mốc này.

**Hai điều cần biết về giới hạn hiển thị:**

- Đây là **tối đa 2 mốc gần nhất**, không phải toàn bộ lịch sử. Hồ sơ chỉ lưu trạng thái hiện tại chứ không lưu bản chụp mỗi lần ký, nên không xem lại được luận điểm của bạn đã thay đổi thế nào qua thời gian.
- Sau khi trợ lý AI sửa hồ sơ, **mốc ký cũ biến mất** khỏi dòng thời gian. Không phải lỗi hiển thị: việc trợ lý sửa đã xoá chữ ký cũ đi, và hệ thống không giữ lại thời điểm ký trước đó. Bạn chỉ thấy đủ cả hai mốc trong trường hợp trợ lý soạn trước rồi bạn ký sau.

---

## Điều cần biết

- Kế hoạch **đang chạy** không bị soi lại dù hồ sơ liên quan hết hạn — chỉ kế hoạch **mới** hoặc kế hoạch sửa vượt ngưỡng 5% mới bị kiểm tra.
- Đổi mã khi sửa kế hoạch luôn kiểm tra lại theo **mã mới**, bất kể size — vì đổi mã là mở vị thế ở một công ty khác.
- Không có ngoại lệ cho mã đã giữ lâu — mọi kế hoạch mới, kể cả cho mã bạn đã giữ nhiều tháng, đều cần hồ sơ đã ký.
