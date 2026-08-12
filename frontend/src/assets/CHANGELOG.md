# Changelog — Investment Mate v2

---

## [v2.83.0] — 2026-08-12 · Tiền bán chờ về T+2

### Tính năng

**⏳ Tiền bán chưa về không còn bị tính là tiền đang có.** Chứng khoán Việt Nam thanh toán T+2 — bán hôm nay thì tiền về sau 2 phiên giao dịch. Trước đây app cộng tiền bán vào "Tiền mặt khả dụng" ngay tại ngày khớp lệnh, nên số hiển thị cao hơn thực tế tới 2 phiên, đúng con số bạn dùng để quyết định vào lệnh mới. Giờ thẻ tiền mặt hiện `120.000.000 đ` kèm dòng `trong đó 30.000.000 đ chờ về — dự kiến 24/02`. Số lớn vẫn khớp sổ công ty chứng khoán để đối chiếu.

**📅 Lịch nghỉ giao dịch nhập được qua trợ lý AI.** Lịch nghỉ đổi mỗi năm và HOSE thường công bố lẻ từng đợt, nên nó nằm trong cơ sở dữ liệu chứ không nằm trong code — không phải chờ bản cập nhật app. Nhập một ngày, một đợt lễ, hay cả năm trong cùng một lần; sửa và xoá được từng ngày. Lịch 2026 đã có sẵn 12 ngày. Thứ Bảy và Chủ nhật tự biết, không cần nhập.

**🤖 Bản tin hằng ngày trừ phần tiền chưa về khi gợi ý khối lượng.** Trước đây trợ lý tính khối lượng vị thế trên toàn bộ số dư, kể cả phần chưa về ví. Bản tin nay có thêm `portfolio_cash_pending` và mốc `market_closures_known_through` để bạn biết lịch nghỉ đã nhập tới đâu.

**⚠️ Cửa sổ ghi lệnh MUA nhắc khi vượt phần tiền đã về.** Dòng vàng *"Vượt tiền đã về X — cần ứng trước tiền bán"*. Đây là nhắc, **không chặn**: cửa sổ đó ghi lệnh đã khớp, mà lệnh thật có thể đã dùng dịch vụ ứng trước tiền bán.

### Kỹ thuật

- Entity `MarketClosure` + collection `market_closures` (unique index `ux_user_date`), 3 endpoint JWT + 3 sibling ApiKey + 3 tool MCP (`list_market_closures`, `add_market_closures`, `remove_market_closure`).
- `SettlementCalculator` là hàm thuần — tập ngày nghỉ do caller nạp và truyền vào. Không sửa `PortfolioCashCalculator` (bị ADR-0007 ghim, dùng chung với TWR).
- "Hôm nay" tính theo ngày lịch VN qua `VietnamDate.Today`, không phải `UtcNow.Date`.
- Golden test lấy thẳng từ thông báo HOSE: lệnh 12/02/2026 thanh toán 23/02, lệnh 13/02 thanh toán 24/02.
- Script seed `scripts/migrations/2026-08-12-market-closures-2026.mongo.js` — **chưa chạy trên môi trường nào**, cần `USER_ID`.
- Quyết định kiến trúc: [ADR-0016](../../../docs/adr/0016-t2-settlement-pending-cash.md).

## [v2.82.0] — 2026-08-11 · Trợ lý AI ghi được cây kịch bản

### Kỹ thuật

**🧭 Trợ lý AI biết trước giá trị nào hợp lệ.** Trước đây khi trợ lý ghi cây kịch bản cho kế hoạch giao dịch, nó phải **đoán** tên hành động — vì phần mô tả tham số nó nhận được không hề nói `actionType` chấp nhận gì. Đoán sai thì chỉ nhận lại một câu vô nghĩa: *"An error occurred invoking 'update_trade_plan'"*. Giờ mọi tập giá trị hữu hạn nằm ngay trong mô tả tham số, nên nó gọi đúng từ lần đầu.

**💬 Lỗi nói rõ sai ở đâu và gửi gì cho đúng.** Nếu vẫn sai, thông báo nêu tên trường, vị trí, và liệt kê đủ giá trị hợp lệ — thay vì một câu chung chung. Áp cho toàn bộ tool, không riêng kế hoạch giao dịch.

**🔇 Hết cảnh "báo xong nhưng chẳng ghi gì".** Gửi cây kịch bản cho kế hoạch đang ở chế độ Đơn giản trước đây bị bỏ đi im lặng mà vẫn trả về `ok` — trợ lý báo hoàn thành trong khi kế hoạch vẫn trống. Giờ nó báo lỗi kèm cách chữa.

**🚫 Hành động không còn giá trị mặc định ngầm.** Một nhánh kịch bản gửi thiếu `actionType` từng âm thầm thành *"bán 50% vị thế"*. Giờ thiếu là lỗi. Đơn vị đo của trailing stop thì vẫn được phép bỏ trống.

## [v2.81.0] — 2026-08-12 · Lỗi tự báo về điện thoại

### Kỹ thuật

**🔔 Lỗi production bắn thẳng về Telegram.** Trước đây app hỏng thì phải tự phát hiện — hoặc tệ hơn, không phát hiện. Giờ lỗi hệ thống ở cả máy chủ lẫn trình duyệt đều tự nhắn về một kênh riêng tư trong khoảng 15 giây, kèm mã người dùng, đường dẫn và stack trace.

**🤫 Chỉ báo lỗi thật.** Người dùng gõ sai form, tìm mã không tồn tại, hay bị từ chối quyền — những cái đó **không** báo. Chỉ lỗi từ 500 trở lên mới nhắn. Một kênh báo cả lỗi nhập liệu sẽ bị tắt sau vài ngày, và lúc đó còn tệ hơn không có vì cứ tưởng mình đang được giám sát.

**🔒 Không đặt token trong mã nguồn.** Chưa cấu hình thì app chạy bình thường, chỉ in một dòng cảnh báo lúc khởi động và không bắn gì. Frontend cũng không tự gọi Telegram — nó đẩy lỗi về máy chủ, vì mọi khoá đặt trong bundle JS đều đọc được.

---

## [v2.81.0] — 2026-08-12 · Mã này mua được tối đa bao nhiêu

### Tính năng

**📊 Trần khối lượng theo biến động danh mục.** Khi bạn điền kế hoạch mua, khối kiểm-trước có thêm một dòng: mã này mua tối đa bao nhiêu cổ mà danh mục vẫn nằm trong ngân sách biến động của bạn. Vượt trần thì hiện nút **Dùng N cổ** để điền thẳng vào ô khối lượng.

Đây là ràng buộc đầu tiên trong app nhìn vào **quan hệ giữa các mã**, không chỉ quy mô từng mã. Mua thêm một mã chạy cùng nhịp với vị thế lớn nhất của bạn, và mua một mã chạy độc lập, là hai việc rủi ro khác hẳn nhau — trước giờ app coi chúng như nhau miễn tỷ trọng vốn bằng nhau.

**🧭 Gánh bao nhiêu rủi ro so với chiếm bao nhiêu vốn.** Panel hiện cả hai cạnh nhau, kiểu "gánh 22% rủi ro · chiếm 14% vốn". Chênh lệch giữa hai số đó là thứ tỷ trọng vốn đơn thuần không nói được.

**💡 Ngân sách suy từ ngưỡng sụt giảm bạn đã đặt** — không thêm ô cấu hình nào. Panel luôn ghi rõ nó suy ra từ đâu, để bạn tự phán đoán chứ không phải tin một con số từ trên trời.

**⚠️ Cảnh báo, không chặn.** Con số dựa trên ước lượng thống kê từ khoảng 65 phiên, nên nó không có quyền cấm bạn lưu kế hoạch. Vẫn lưu được bình thường.

**🤖 Agent AI đọc được trần này.** Thêm công cụ `get_volatility_sizing` — trước đây agent lập được kế hoạch nhưng không có đường nào biết trần, tức lan can chỉ tồn tại trên đường bạn tự bấm.

**🔔 Nhờ agent lập kế hoạch cũng nhận được cảnh báo.** Trước đó công cụ trên chỉ là tuỳ chọn, và lời dặn "gọi nó trước" lại nằm trong chính cái tuỳ chọn ấy — agent đi thẳng vào tạo kế hoạch thì không có gì bắn ra. Giờ lệnh mua có gắn danh mục sẽ tự kiểm, và kết quả trả về kèm dòng *"khối lượng 500 vượt trần 198 cổ"*. Kế hoạch vẫn được tạo bình thường.

### Sửa lỗi

**🔌 "Chưa lấy được dữ liệu" không còn bị nói nhầm thành "chưa đủ dữ liệu".** Khi nguồn giá lỗi, panel trước đây báo *"Chưa đủ lịch sử giá cho FPT"* — một câu sai, vì FPT có thừa lịch sử. Giờ hai tình huống có hai câu khác nhau.

**🧮 Giá chưa điều chỉnh sự kiện quyền không còn thổi phồng độ biến động.** Một phiên chia tách chưa điều chỉnh làm giá rơi một nửa, và nếu tính thẳng thì mã đó trông như biến động gấp đôi thực tế. Giờ những phiên như vậy bị loại và panel nói rõ mã nào đã bị loại.

---

## [v2.80.0] — 2026-08-11 · Gửi hồ sơ công ty cho người khác

### Tính năng

**🤝 Chia sẻ hồ sơ công ty.** Nút **Chia sẻ** ở trang hồ sơ chép toàn bộ phần bạn viết ra clipboard để bạn tự gửi qua Zalo, email hay bất cứ đâu. Người nhận mở trang **cùng mã**, bấm "Dán nội dung", đọc lại rồi tự quyết định lưu và ký.

Nó **không gửi đi đâu cả** — không có chuyện người kia tự dưng nhận được thứ gì.

**🙈 Tên bạn được che sẵn, và sửa được.** Mặc định là email đã che bớt: `minh.tran@gmail.com` thành `min***@gmail.com`. Giữ phần sau `@` để người quen nhận ra là bạn, nhưng không đủ để ai gửi thư tới. Muốn để tên gọi thường ngày thì sửa, muốn ẩn hẳn thì xoá trắng. Lần sau tự dùng lại lựa chọn của bạn.

**👀 Xem trước trước khi chép.** Hộp thoại hiện đúng nội dung sắp rời khỏi máy. Đây là thứ đi sang tay người khác, nên thấy trước là mức tối thiểu.

**📌 Người nhận biết nội dung tới từ đâu.** Dòng `Nhận từ … ngày …` được chèn lên đầu ô Ghi chú khi dán. Đây là vệt duy nhất còn lại — nằm ngay chỗ phải đọc trước khi ký. Xoá được, vì đó là ghi chú của bạn. Dán lại lần nữa không mọc thêm dòng thứ hai.

**📉 Bản chia sẻ không kèm số liệu doanh nghiệp.** Chỉ có phần bạn viết. Máy bên kia tự lấy P/E, doanh thu, cổ đông được rồi.

### Thay đổi

**✏️ Nút `Dán từ AI` đổi tên thành `Dán nội dung`.** Giờ nó nhận cả câu trả lời của AI lẫn hồ sơ người khác chia sẻ, nên giữ nhãn cũ là nói sai.

---

## [v2.79.1] — 2026-08-11 · Panel số liệu trong hồ sơ công ty đã có dữ liệu trở lại

### Sửa lỗi

**📊 Sáu khối số liệu bị trống đã hiện lại.** Trong hồ sơ công ty, các khối **Doanh thu theo quý**, **Doanh nghiệp cùng ngành**, **Cổ tức & sự kiện**, **Kế hoạch kinh doanh**, **Thông tin doanh nghiệp** và **Khối ngoại** đều báo "không lấy được dữ liệu" với mọi mã. Nguồn số liệu đã đổi cấu trúc mà app vẫn đọc theo cấu trúc cũ.

Không có lỗi nào hiện lên vì mỗi khối hỏng đều lặng lẽ trả về rỗng — nhìn giống hệt "công ty này không có số liệu đó". Bản tóm tắt của trợ lý AI cũng ăn cùng nguồn này, nên nó cũng đang phân tích trên dữ liệu thiếu.

**📈 Doanh thu theo quý giờ đúng là theo quý.** Trước đây khối này lấy nhầm số liệu **cả năm** dù tiêu đề ghi theo quý.

**📋 Kế hoạch kinh doanh có thêm tiến độ thực hiện.** Ngoài chỉ tiêu đặt ra, giờ hiện cả **đã thực hiện được bao nhiêu** và **đạt bao nhiêu %** tính tới quý gần nhất. Chỉ tiêu vượt 75% được tô xanh. Đổi lại, kế hoạch cổ tức không còn — nguồn đã bỏ.

**💱 Khối ngoại đổi từ khối lượng sang giá trị.** Nguồn không còn trả số cổ phiếu mua/bán, chỉ còn giá trị (tỷ VND) theo hôm nay – tuần – tháng. Con số trước đây gắn nhãn "cổ phiếu" giờ là "tỷ VND", nên đừng so hai bản với nhau.

**📅 Ngày chốt quyền và ngày trả cổ tức lệch một ngày.** Nguồn trả ngày theo mốc giờ Việt Nam nhưng app quy đổi theo giờ quốc tế, làm mọi ngày lùi lại một hôm.

---

## [v2.79.0] — 2026-08-11 · Trang chủ có một khoảng lặng trước khi bạn bấm bất cứ thứ gì

### Tính năng

**🎣 Màn tĩnh tâm ở đầu trang chủ.** Mở app ra không còn rơi thẳng vào danh sách việc cần làm. Trên cùng giờ là mặt hồ và một người ngồi câu — và cảnh đó **phản ánh số ngày bạn chưa đặt lệnh**: vừa giao dịch xong thì nước còn động, sóng gấp, trời xám; càng lâu không động tay, sóng càng lặng và trời càng ngả hoàng hôn. Số ngày hiển thị luôn là số thật, chưa có lệnh nào thì nói thẳng "Chưa có lệnh nào" chứ không bịa.

Kèm một câu châm ngôn — Buffett, Munger, Graham, Livermore, Lynch, và vài câu ẩn dụ người câu — đổi theo trạng thái bạn tự chấm, cùng một dòng không bao giờ đổi: *Tiền là con số trên màn hình. Mất tiền là thật.*

**✋ Luật dừng: khi cảm xúc vào thì không hành động.** Mỗi ngày một câu hỏi — *Giờ anh đang thế nào?* Bình tĩnh / FOMO (sợ bỏ lỡ) / Sợ / Cay cú.

Chấm khác Bình tĩnh thì **Việc cần xử lý hôm nay bị phủ mờ**, kèm câu *"Danh sách này tối nay vẫn ở đây."* Bạn vẫn xem được — nhưng phải bấm thêm một lần. Không ai cấm bạn tiêu tiền của mình; chỉ là bắt bạn dừng nửa giây trước khi làm.

Ba đường lách đã bịt sẵn: bấm **đổi** chỉ mở lại bảng chọn chứ không gỡ lớp phủ; chấm Bình tĩnh rồi quay lại FOMO thì phải bấm qua lớp phủ lại từ đầu; và lúc app còn đang hỏi server bạn đang thế nào thì danh sách chưa hiện ra — không có kẽ hở nào để lọt.

Tâm trạng lưu theo tài khoản nên mở máy khác vẫn thấy, xoá cache không mất, sang ngày mới thì hỏi lại.

### Thay đổi

**🗑️ Gỡ widget "Giao dịch nhanh" khỏi trang chủ.** Ít khi dùng, và nó là lối đặt lệnh nhanh nhất trên trang chủ — đúng thứ mà bản này muốn bớt đi. Đường tới Kế hoạch giao dịch vẫn còn nguyên ở menu.

### Kỹ thuật

- Hoạt hoạ chạy **thuần CSS, không có vòng lặp JavaScript**; tôn trọng `prefers-reduced-motion` (tắt chuyển động, giữ ảnh tĩnh đúng mức tĩnh lặng đó).
- Endpoint mới: `GET /api/v1/trades/last-activity` và `/api/v1/mood` (`today` / ghi / `override`).
- Ngày lịch Việt Nam tính ở server; khoá ngày lưu dạng chuỗi để không bị lệch một ngày khi đi qua database.
- 1846 test backend + 316 test frontend, tất cả pass. Chi tiết quyết định: ADR-0013.

---

## [v2.78.0] — 2026-08-11 · Hồ sơ công ty mở ra là đọc được, sửa thì dễ gõ hơn

### Tính năng

**📖 Trang hồ sơ có chế độ đọc.** Trước bản này, mở `/company-dossier/{mã}` là rơi thẳng vào một bức tường ô nhập — kể cả khi bạn chỉ muốn đọc lại luận điểm trước khi vào lệnh. Giờ mặc định là bản đọc: mô hình kinh doanh là đoạn văn, moat là các thẻ, rủi ro xếp theo hạng với **hạng 1 nổi bật nhất** và dấu hiệu quan sát in ngay dưới mô tả. Bấm **Sửa** mới ra form; **Hủy** hỏi lại nếu bạn đã gõ dở.

Mã chưa có hồ sơ, hoặc bị cổng chặn lệnh đá sang, vẫn vào thẳng form — lúc đó không có gì để đọc.

**🤖 Sao chép cho AI · Dán từ AI.** Hai nút cho trợ lý *không* nối MCP (ChatGPT web, Gemini). Sao chép gói sẵn hồ sơ + số liệu doanh nghiệp + khuôn JSON; dán lại thì đổ vào form để bạn đọc lại. **Không tự lưu, không tự ký.** Dán nhầm nội dung của mã khác vào trang đang mở thì bị chặn thẳng.

### Sửa

**✍️ Form không còn khó gõ.** Bốn thứ đổi cùng lúc:

- Mô tả rủi ro và dấu hiệu quan sát chuyển từ ô một dòng sang **ô nhiều dòng** — trước đây câu dài trôi ngang, gõ xong không đọc lại được cả câu.
- Cột viết rộng **3/5** thay vì một nửa, nên ô nhập không còn bị bóp.
- Mọi ô có **nhãn thật** phía trên, không chỉ placeholder biến mất khi gõ.
- Dòng đỏ "bắt buộc có dấu hiệu quan sát" **chỉ hiện sau khi bạn rời ô hoặc sau lần bấm Lưu đầu tiên** — trước đây nó bật ngay lúc vừa thêm yếu tố, báo sai trước khi kịp làm gì.

Thêm dòng đếm cạnh nút Lưu: "Còn N yếu tố thiếu dấu hiệu quan sát" — biết vì sao chưa ký được mà không phải cuộn cả trang đi tìm.

**🔒 Không ký được khi form còn thay đổi chưa lưu.** Chữ ký đóng dấu vào bản đang nằm trên server, nên ký lúc màn hình hiện nội dung khác là ký một thứ mình không đọc.

**📊 Panel số liệu dễ đọc hơn** — giá trị to đậm, nhãn nhỏ; bốn khối dài gập lại được, mặc định chỉ mở khối doanh thu. Khối *không lấy được dữ liệu* thì không gập, để câu báo thiếu luôn nhìn thấy.

**🔗 Link "← Danh sách hồ sơ"** chuyển từ bên phải (sau nhóm nút hành động) lên góc trái trên tiêu đề, đúng chỗ của một đường lùi.

**🏢 Từ dòng thời gian của mã sang hồ sơ công ty.** Nút cạnh tiêu đề trang `/symbol-timeline/{mã}`, luôn hiện. Trước bản này chỉ có đường đi khi hồ sơ đã từng được ký — mã chưa có hồ sơ thì không có lối nào, đúng lúc cần viết nhất.

### Kỹ thuật

- Tách `DossierViewComponent` và module thuần `dossier-clipboard.ts` (dựng prompt + parse JSON dán vào). Shape dán trùng đúng tham số MCP `upsert_company_dossier` — một hợp đồng duy nhất cho cả hai đường.
- Frontend tests: **221 → 273** (52 test mới).

---

## [v2.77.0] — 2026-08-10 · Bấm vào mã ở bất kỳ đâu là ra dòng thời gian của nó

### Tính năng

**🔗 Mã chứng khoán bấm được ở 41 chỗ trên 19 màn hình.** Bảng kế hoạch, thẻ vị thế, hàng lệnh, danh sách cảnh báo, kết quả tìm kiếm, bảng ảnh chụp danh mục — bấm vào mã là sang thẳng dòng thời gian của mã đó. Trước bản này chỉ Watchlist và Trades có icon 📊 riêng, còn lại phải tự tìm đường.

Bấm Enter cũng đi được, không chỉ chuột. Mã rỗng thì không giả vờ là link.

**Chỗ nào cố ý không gắn** — để bạn biết đó không phải bỏ sót:

- Mã nằm trong câu văn ("Không tìm thấy vị thế HPG trong danh mục") — bấm vào giữa câu là hành vi lạ.
- Tiêu đề của chính trang/panel đang mở — link trỏ về nơi bạn đang đứng.
- `VNINDEX` và các chỉ số ở trang thị trường — chúng không phải mã cổ phiếu, không có dòng thời gian.
- Watchlist và Trades — hai trang này **đã có** lời giải riêng từ trước: mã trỏ sang trang khác, cạnh nó có icon 📊 trỏ sang dòng thời gian. Gắn thêm là tạo hai đường đi khác nhau cho cùng một chữ.
- **Mã nằm trong hàng/thẻ mà cú bấm cả hàng là hành động chính** — bảng Kế hoạch (bấm hàng để nạp kế hoạch), kết quả tìm mã và ô giá ở trang Thị trường (bấm để tra cứu), chip vị thế ở màn ghi lệnh (bấm để chọn mã). Gắn vào đó là **cướp mất** chính hành động bạn đang cần.

**📋 Dòng thời gian của mã có thêm mốc hồ sơ công ty.** Nằm chung với nhật ký, lệnh và cảnh báo, sắp theo đúng thứ tự thời gian: *"Ký hồ sơ công ty"* và *"Trợ lý AI sửa hồ sơ — chờ bạn ký lại"*. Có ô lọc riêng ở trên, và xuất CSV cũng có dòng cho nó.

**Nói thẳng về giới hạn:** đây là **tối đa 2 mốc gần nhất**, không phải lịch sử tiến hoá luận điểm — hồ sơ chưa lưu bản chụp theo từng lần ký. Và sau khi trợ lý AI sửa hồ sơ thì **mốc ký cũ biến mất**, vì chính việc sửa đó đã xoá chữ ký và hệ thống không giữ lại thời điểm ký trước. Bạn chỉ thấy đủ cả hai mốc khi trợ lý soạn trước rồi bạn ký sau.

### Nội bộ

- Test: **1.811 backend** (Domain 787, Application 416, Infrastructure 389, Api 219) + **219 frontend**.
- `ICompanyDossierRepository` vào timeline handler để **bắt buộc**, không optional — tham số optional mà hỏng đăng ký DI sẽ làm mốc âm thầm không bao giờ hiện.

---

## [v2.76.2] — 2026-08-10 · Bấm Lưu hai lần không còn ra lỗi, và lô rỗng không xoá mất số lượng

### Sửa

**🔁 Bấm "Lưu" hồ sơ công ty hai lần thật nhanh không còn báo lỗi.** Lưu hồ sơ là *tìm trước, chưa có thì tạo*. Hai lần bấm sát nhau cùng thấy "chưa có", cùng tạo, và cái thứ hai đâm vào ràng buộc "một hồ sơ cho mỗi mã" của cơ sở dữ liệu — bạn nhận về lỗi hệ thống dù chẳng làm gì sai.

Giờ lần thứ hai tự nhận ra mình thua cuộc, tìm lại bản vừa được tạo và cập nhật lên đó. Đúng **một lần** thử lại — nếu vẫn không thấy thì báo lỗi thật, vì thử lại mãi chỉ đổi một lỗi hiện ra ngay thành một request treo không biết bao giờ xong.

Bản thử lại đi qua đúng đường cập nhật thường ngày: **trợ lý AI sửa thì hồ sơ vẫn mất chữ ký** và bạn vẫn phải ký lại. Không có cửa sau nào mở ra ở đây.

**🧮 Sửa kế hoạch với danh sách lô rỗng không còn làm số lượng về 0.** Đường sửa thiếu một điều kiện mà đường tạo có: gửi lên danh sách lô rỗng thì nó vẫn chạy phần ghi lô và gán số lượng = tổng của rỗng = 0. Cổng kỷ luật đã chấm theo số lượng cũ trước đó nên **không có lệnh lớn nào lọt** — nhưng kế hoạch còn lại là số 0 vô nghĩa.

Đánh đổi đã chọn có ý thức: **vẫn chưa có cách xoá sạch toàn bộ lô qua API**. Màn hình kế hoạch không gửi danh sách rỗng bao giờ nên không ai mất đường nào; thêm một cờ "xoá hết lô" là thêm một đường ghi phải canh, để khi nào có nhu cầu thật.

### Nội bộ

- Test: **1.804 pass** (Domain 787, Application 409, Infrastructure 389, Api 219).
- Lỗi trùng khoá được đổi thành kiểu riêng và phân biệt bằng **tên index**, không bằng chuỗi lỗi chung — collection này sau có thêm ràng buộc unique khác thì lỗi của nó không bị đội lốt.

---

## [v2.76.1] — 2026-08-10 · Câu báo thiếu của cổng hồ sơ đọc thành một khối

### Sửa

**Câu nhắc `riskFactors` thiếu mô tả giờ cùng khuôn với các câu còn lại.** Danh sách "còn thiếu" của cổng hồ sơ theo mẫu *"cần X, đang có Y"* — riêng câu về mô tả rủi ro trước đây chỉ có một mệnh đề (*"mô tả không được để trống ở hạng 1"*), đọc lệch hẳn khỏi khối. Giờ là *"riskFactors: cần mô tả ở mọi yếu tố, đang để trống ở hạng 1, 3"*.

Không đổi điều kiện chặn — chỉ đổi cách nói. Bạn bị chặn ở đúng những trường hợp như trước.

### Nội bộ

- Skill `qa-verify` ghi sai email tài khoản kiểm thử ở 2 chỗ, chạy nguyên văn sẽ fail ở bước mint JWT. Đã sửa cho khớp `StableJwtMint.ALLOWED_EMAILS`.
- Test: 403 pass ở `InvestmentApp.Application.Tests` (+1 test mới ghim cách nối nhiều hạng).

---

## [v2.76.0] — 2026-08-10 · Hồ sơ công ty trả lại thời gian, và nhắc trước khi nó hết hạn

### Tính năng

**♻️ Rủi ro đã viết trong hồ sơ giờ tự thành điều kiện "lý do sai" của kế hoạch.** Khi form kế hoạch có mã, khối *"Từ hồ sơ công ty {mã}"* hiện 3 rủi ro hạng cao nhất, mỗi cái đã ghép sẵn thành câu dùng được: *mô tả — dấu hiệu: dấu hiệu quan sát được*. Bấm "+ Thêm" là vào kế hoạch.

- **Chỉ đề xuất, không tự áp.** Không tick sẵn, không tự thêm. Một kế hoạch tự đầy điều kiện mà bạn chưa đọc lại chúng thì cổng kỷ luật chỉ đo được chữ, không đo được ý.
- Đề xuất chưa đủ 20 ký tự vẫn thêm được, có nhãn vàng *"cần bổ sung cho đủ 20 ký tự"* — chặn ở đây thì bạn mất luôn nội dung gợi ý và phải gõ lại từ đầu.
- Ngày kiểm chứng để trống: hồ sơ không biết bạn định kiểm khi nào.

**🔔 Trang "Lý do đầu tư cần review" có thêm mục "Hồ sơ công ty cần soát lại".** Hết hạn (đỏ) → chưa ký (xám) → nên soát lại (vàng); trong mỗi nhóm, quá hạn nhiều xếp trước. Hai loại đầu ghi rõ **"đang chặn lập kế hoạch"**.

Trước bản này, cổng hồ sơ chỉ lên tiếng **đúng lúc bạn đang muốn mua** — tức lúc tệ nhất để phải ngồi đọc lại hồ sơ. Giờ bạn biết trước.

Dashboard có badge số lượng, **ẩn hoàn toàn khi bằng 0** — một dòng "0 hồ sơ" mỗi ngày sẽ dạy bạn bỏ qua chỗ đó, rồi hôm có số thật cũng bỏ qua luôn.

### Files chính

- `src/InvestmentApp.Application/CompanyDossiers/Queries/GetSuggestedInvalidationRules/`, `.../GetDossiersNeedingReview/`, `src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs` (2 endpoint mới).
- `frontend/src/app/features/trade-plan/trade-plan.component.ts`, `frontend/src/app/features/pending-reviews/pending-reviews.component.ts`, `frontend/src/app/features/dashboard/widgets/discipline-score-widget.component.ts`, `frontend/src/app/core/services/company-dossier.service.ts`.
- Tests: 1797 test backend + 204 test frontend pass, không regression (baseline v2.75.0: 1784 + 189).

---

## [v2.75.0] — 2026-08-10 · Số liệu doanh nghiệp nằm ngay cạnh ô viết hồ sơ

### Tính năng

**📄 Trang hồ sơ công ty giờ có số liệu doanh nghiệp ở cột bên phải.** P/E, P/B, ROE, ROA, EPS, vốn hóa, Beta, đỉnh/đáy 52 tuần, đơn vị kiểm toán (kèm cờ Big4), ngành, số cổ phiếu lưu hành, free float, cổ đông lớn, ban lãnh đạo, doanh thu và lợi nhuận từng quý, cổ phiếu cùng ngành, cổ tức, kế hoạch kinh doanh. Không phải mở tab khác để tra khi đang trả lời "doanh nghiệp này kiếm tiền bằng gì".

- **Số liệu là nguyên liệu, không phải điều kiện.** Số liệu đẹp không làm hồ sơ đủ điều kiện — cổng vẫn chỉ đọc những gì bạn tự viết. Panel ghi rõ điều đó.
- **Phần nào chưa tra được thì ghi "không lấy được dữ liệu", không hiện số 0.** Nguồn (24hmoney) đôi lúc thiếu một vài phần; một bảng doanh thu trống render thành 0 sẽ đọc thành doanh nghiệp không có doanh thu.
- Trên điện thoại, khối số liệu xếp xuống dưới ô viết.

**🤖 Trợ lý AI dùng cùng số liệu đó** qua công cụ `get_company_fundamentals` — để soạn nháp hồ sơ từ số thật chứ không từ suy đoán. Trợ lý vẫn **không ký được**: chữ ký là của con người, nguyên tắc không đổi.

### Chi tiết đáng nhắc

Với HPG, nguồn trả về 10 sự kiện cổ tức mà **mọi trường đều rỗng**. Nếu chỉ đếm số dòng thì hệ thống coi như "có dữ liệu" và hiện 10 dòng gạch ngang — trông như dữ liệu nhưng không mang thông tin nào. Nay các dòng rỗng bị bỏ trước khi chấm phần nào lấy được, nên khối đó hiện đúng "không lấy được dữ liệu". **Gõ sai mã cũng từng trả về "thành công" với hồ sơ trống.** Nguồn không báo lỗi cho mã không tồn tại — nó trả về đủ cấu trúc với mọi ô rỗng. Nay mã sai trả về đúng "không tìm thấy", nên trợ lý AI không thể soạn hồ sơ từ một mã gõ nhầm.

Cả hai lỗi chỉ lộ ra khi gọi nguồn thật, không lộ trong test.

Số liệu của một mã được **nhớ 15 phút** nên mở lại trang hoặc trợ lý gọi nhiều lần không bắn thêm hàng loạt request ra nguồn ngoài. Ca lấy không được thì **không** nhớ — một lỗi mạng nhất thời không bị đóng băng thành "mã này không có dữ liệu".

### Files chính

- `src/InvestmentApp.Application/MarketData/Queries/GetCompanyFundamentals/`, `src/InvestmentApp.Api/Controllers/MarketDataController.cs` (endpoint `GET /market/stock/{symbol}/fundamentals`), `src/InvestmentApp.Api/Mcp/CompanyDossierTools.cs` (tool thứ 5).
- `frontend/src/app/features/company-dossier/fundamentals-panel.component.ts` (mới), `company-dossier-detail.component.ts` (chia 2 cột), `frontend/src/app/core/services/market-data.service.ts`.
- `docs/adr/0011-company-dossier-gate-at-plan-creation.md` (đóng follow-up chặng 2), `frontend/src/assets/docs/ho-so-cong-ty.md`.
- Tests: 1784 test backend + 189 test frontend pass, không regression (baseline v2.74.0: 1767 + 182).

---

## [v2.74.0] — 2026-08-10 · Tỷ trọng ngành — hạn mức 40% bắt đầu hoạt động

### Sửa lỗi

**⚠️ Hạn mức tập trung ngành 40% trước đây chưa từng cảnh báo lần nào.** Trang Rủi ro vẫn hiện mục "Tỷ trọng ngành" kèm hạn mức bên cạnh, nên trông như đã được canh — nhưng nguồn tra ngành bị nối vào một dịch vụ trả về rỗng, khiến mọi mã rơi vào rổ "Không xác định", và rổ đó được lập trình để **không bao giờ** báo vượt. Nay đã nối đúng nguồn (24hmoney) và rổ "Không xác định" cũng bị so hạn mức — vì không biết mình đang dồn vào đâu là điều đáng cảnh báo, không phải đáng bỏ qua.

**Nếu bạn từng nhìn trang Rủi ro và thấy không có cảnh báo ngành nào, đừng coi đó là bằng chứng danh mục đã phân bổ tốt.** Hãy mở lại trang Rủi ro sau bản này.

### Tính năng

**📊 Form lập kế hoạch hiện tỷ trọng ngành ngay lúc bạn đang quyết định quy mô lệnh.** Trong khối kiểm-trước (chỗ đang nhắc về hồ sơ công ty), thêm một dòng: ngành của mã, tỷ trọng **đang giữ**, tỷ trọng **sau lệnh này**, hạn mức, và các mã cùng ngành đang giữ.

- **Chỉ là thông tin, không chặn gì** — không nút nào bị khoá. Khung màu xám trung tính có chủ đích để phân biệt với khung cảnh báo chặn màu vàng/đỏ ngay phía trên.
- Chỉ hiện khi đã chọn danh mục và đã đủ mã + số lượng + giá vào + số dư. Chưa đủ thì không đoán bằng 0.
- Mã mà nguồn dữ liệu không trả về ngành thì **ẩn hẳn dòng ngành**, không hiện "0%" — "0%" nghĩa là chưa giữ gì ngành đó, khác hẳn "chưa biết ngành". Trường hợp biết ngành mà chưa chia được tỷ trọng thì hiện **"n/a"**.
- **Chỉ hiện với lệnh MUA.** Lệnh bán làm tỷ trọng giảm, trong khi phép chiếu cộng quy mô lệnh nên sẽ báo tăng — thà không hiện còn hơn hiện một con số sai dấu ngay trong khối cảnh báo rủi ro.
- Nhãn ngành được nhớ 6 giờ nên gõ form không còn bắn hàng loạt request ra nguồn dữ liệu ngoài mỗi lần ngừng gõ.

### Files chính

- `src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs`, `src/InvestmentApp.Application/Risk/Queries/GetSectorExposureForPlan/`, `src/InvestmentApp.Api/Controllers/RiskController.cs`.
- `frontend/src/app/core/services/risk.service.ts`, `frontend/src/app/features/trade-plan/trade-plan.component.ts`.
- `docs/adr/0012-sector-concentration-display-only.md` (mới).
- Tests: 1767 test backend + 182 test frontend pass, không regression (baseline v2.73.0: 1754 + 174).

---

## [v2.73.0] — 2026-08-10 · Trợ lý AI soạn được hồ sơ công ty, và có menu để vào

### Sửa lỗi

**🔓 Trợ lý AI lập lại được kế hoạch giao dịch.** Từ bản v2.71.0, mọi kế hoạch mới đều cần hồ sơ công ty đã ký — nhưng trợ lý **không có công cụ nào để soạn hồ sơ**, nên nó bị chặn ở mọi mã và không có cách tự sửa. Nay đã có 4 công cụ MCP cho hồ sơ công ty, trong đó `upsert_company_dossier` để soạn nội dung và `get_dossier_gate_status` để biết chính xác còn thiếu gì trước khi thử tạo kế hoạch.

**Trợ lý soạn được nhưng vẫn KHÔNG ký được** — nguyên tắc không đổi: chữ ký là của con người. Sau khi trợ lý soạn, hồ sơ về trạng thái chờ bạn đọc và ký, và cổng vẫn chặn cho tới lúc đó.

**🧭 Đã có menu vào trang hồ sơ công ty** (Quản lý → Hồ sơ công ty), ngay trước "Kế hoạch GD". Trước đây trang danh sách hồ sơ không có đường vào nào từ giao diện — chỉ tới được trang chi tiết của một mã qua banner cảnh báo.

**💬 Lỗi khi lưu hồ sơ nay nói rõ lý do.** Trước đây mọi thất bại đều hiện "Không thể lưu hồ sơ", trong khi máy chủ đã trả về câu cụ thể như *"Mỗi yếu tố rủi ro phải có dấu hiệu quan sát được"*. Nay hiện đúng câu đó, nên bạn biết ô nào đang chặn mình.

**🗣 Trợ lý AI bị cổng chặn nay biết vì sao.** Tìm ra khi gọi thử công cụ thật: khi cổng hồ sơ chặn, trợ lý chỉ nhận được *"An error occurred invoking 'create_trade_plan'"* — không mã, không lý do, không danh sách còn thiếu, nên nó không có đường tự sửa. Nay thông báo nói rõ mã nào, lý do gì, còn thiếu những gì, và nhắc rằng **chữ ký phải do bạn thực hiện** trên trang hồ sơ.

### Files chính

- `src/InvestmentApp.Api/Mcp/CompanyDossierTools.cs` (mới) — 4 công cụ MCP, **cố ý không có công cụ ký**.
- `src/InvestmentApp.Api/Mcp/McpDossierGate.cs` (mới) — dịch lỗi cổng sang thông báo trợ lý đọc được.
- `frontend/src/app/shared/components/header/header.component.ts`, `frontend/src/app/features/company-dossier/company-dossier-detail.component.ts`.
- Tests: 1754 test backend + 174 test frontend pass. Có test chặn việc thêm công cụ ký qua MCP về sau, test chặn việc bọc rộng làm che các lỗi khác, và test ghim mọi công cụ hồ sơ đều lấy danh tính từ khoá API chứ không từ tham số người gọi truyền vào.

---

## [v2.71.0] — 2026-08-10 · Hồ sơ công ty — chặn lập kế hoạch khi chưa hiểu doanh nghiệp

### Tính năng

**📋 Không lập được kế hoạch mua cho một mã khi chưa có hồ sơ hiểu doanh nghiệp đã ký.** Trước đây "Lý do đầu tư" (Thesis) chỉ ép đủ **độ dài** câu chữ, không ép được **hiểu biết** — "HPG đầu ngành thép, triển vọng tốt" đủ 30 ký tự và không kiểm chứng được gì. Hồ sơ công ty mới ép trả lời trước khi xuống tiền: doanh nghiệp kiếm tiền bằng gì, lợi thế bền ở đâu, rủi ro nào đáng ngại và **biết nó đang xảy ra bằng dấu hiệu gì** — viết một lần cho một mã, dùng lại cho mọi lần mua mã đó.

- Trang **`/company-dossier`** (danh sách theo mã, kèm trạng thái tươi) và **`/company-dossier/:symbol`** (chi tiết: mô hình kinh doanh, danh sách moat, danh sách rủi ro xếp hạng 1..N với dấu hiệu quan sát được bắt buộc + tối đa 1 đánh dấu "hủy diệt", nút ký ở cuối trang).
- Ngưỡng đủ nội dung theo **size lệnh** — cùng công thức 5% tài khoản với gate "Lý do đầu tư": lệnh nhỏ chỉ cần business model không rỗng + 1 rủi ro có dấu hiệu; lệnh ≥ 5% tài khoản cần đủ cả 4 khối với độ dài tối thiểu.
- Hồ sơ có **hạn tươi**: còn mới (< 90 ngày kể từ lần ký) → cần soát lại (90-179 ngày, vẫn qua được) → hết hạn (≥ 180 ngày, **chặn**). Chỉ nút ký mới đẩy được đồng hồ này — sửa nội dung, kể cả tự sửa, không tính.
- Form Trade Plan có **cảnh báo kiểm-trước không chặn**: khi đã điền đủ mã + số lượng + giá vào + số dư, form tự kiểm tra và hiện trạng thái ngay trước khi bấm lưu.
- Nút "Tạo Trade Plan từ gợi ý" ở trang thị trường: với mã chưa có hồ sơ đã ký, điều hướng sang trang hồ sơ và giữ nguyên entry/SL/TP đã auto-fill, quay lại không mất gì.

### ⚠️ Lưu ý khi triển khai

- **Lệnh đầu tiên sau khi deploy sẽ bị chặn ở mọi mã**, kể cả mã đang giữ nhiều tháng — không có ngoại lệ chuyển tiếp cho tính năng này. Viết hồ sơ (một câu là đủ cho lệnh nhỏ) rồi ký trước khi lập lệnh đầu tiên.
- **Trợ lý AI (NPU/Claude) tạm thời không lập được trade plan mới cho bất kỳ mã nào** cho tới khi bản cập nhật kế tiếp bổ sung công cụ soạn hồ sơ qua MCP. Đây là hệ quả tạm thời của gate mới, không phải lỗi.

### Files chính

- `src/InvestmentApp.Domain/Entities/CompanyDossier.cs`, `src/InvestmentApp.Application/CompanyDossiers/`, `src/InvestmentApp.Infrastructure/Repositories/CompanyDossierRepository.cs`, `src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs`.
- `frontend/src/app/features/company-dossier/`, `frontend/src/app/core/services/company-dossier.service.ts`.
- `docs/adr/0011-company-dossier-gate-at-plan-creation.md` (mới).
- Tests: verify thật DB prod (tài khoản test, mã HPG) 8/8 qua API + 22 mục qua browser. 1.742 test backend + 171 test frontend pass, không regression (số đo tại thời điểm mở PR).

---

## [v2.70.1] — 2026-08-09 · Hạn mức rủi ro và kịch bản thoát lệnh cũng hiểu sự kiện quyền

Bản v2.70.0 dạy app biết về cổ tức và chia tách, nhưng còn hai chỗ ra quyết định **tự động** vẫn tính theo giá cũ. Bản này nối nốt.

### Sửa lỗi

**🔒 Khoá giao dịch oan sau ngày GDKHQ.** Nếu bạn đặt hạn mức lỗ theo ngày, app sẽ tự khoá khi chạm ngưỡng. Phép tính lãi/lỗ trong ngày trước đây lấy **trung bình các lệnh mua mà không tính khối lượng** — mua 100 CP giá 20.000 rồi 900 CP giá 30.000 thì nó coi giá vốn là 25.000 thay vì 29.000. Nó cũng bỏ qua phí, thuế và cổ phiếu thưởng: bán 130 CP HPG sau khi nhận cổ tức cổ phiếu 30% bị tính thành lỗ 910.000đ trong khi thực tế gần như hoà vốn — đủ để khoá giao dịch cả ngày. Nay lãi/lỗ trong ngày tính từ đúng giá vốn bình quân gia quyền, có phí/thuế.

**📉 Kịch bản thoát lệnh tự bắn sai.** Kế hoạch giao dịch nâng cao chạy nền mỗi 15 phút và tự kích hoạt hành động khi giá chạm điều kiện. Sau ngày GDKHQ, giá thị trường đã điều chỉnh còn giá trong kế hoạch thì chưa — một cây kịch bản đặt "giảm 10% thì cắt" sẽ bắn ngay hôm điều chỉnh, dù giá thực tế không đổi. Nay giá nhập, ngưỡng giá và trailing stop trong kế hoạch đều được quy về cùng mặt bằng với thị trường. Mức trượt đang chạy cũng được hạ theo, nên không còn chuyện trailing stop cũ cắt lỗ oan ngay hôm giá bị điều chỉnh.

**⚠️ Kiểm thử sức chịu đựng thiếu cổ phiếu thưởng.** Kịch bản "thị trường giảm 10%" trước đây bỏ sót phần cổ phiếu nhận từ sự kiện quyền, nên báo mức thiệt hại thấp hơn thực tế.

**📅 Sự kiện nhập sớm bị áp trước hạn.** Bạn thường nhập sự kiện ngay khi doanh nghiệp công bố, tức trước ngày GDKHQ vài tuần. App trước đây điều chỉnh giá kế hoạch ngay lúc nhập, trong khi giá thị trường phải tới ngày GDKHQ mới đổi — lệch mặt bằng suốt khoảng chờ đó. Nay chỉ áp từ đúng ngày GDKHQ.

**🔁 Bán đúng ngày GDKHQ bị mất quyền.** Quyền được chốt theo danh sách cổ đông cuối ngày liền trước, nên bán trong ngày GDKHQ vẫn được hưởng cổ tức. App tính ngược lại: bán 500 CP hôm đó thì phần bán ra dùng giá vốn chưa điều chỉnh, ra lỗ giả gấp mấy chục lần. Ngược lại, mua trong ngày GDKHQ thì **không** được hưởng — chỗ này cũng đã sửa.

### Kỹ thuật

1.640 test backend pass. Chi tiết quyết định: [ADR-0010](../../docs/adr/0010-corporate-actions-position-projection.md) — phần bổ sung 2026-08-09.

---

## [v2.70.0] — 2026-08-09 · Cổ tức và chia tách không còn làm danh mục trông như lỗ

### Tính năng

**🎁 Sự kiện quyền.** Trang mới `/corporate-actions` để nhập cổ tức tiền mặt, cổ tức cổ phiếu và chia tách cổ phiếu. Nhập xong, app tính lại giá vốn và lãi/lỗ cho đúng.

Điểm khó nhất của bài toán này là **khoảng chờ**: ngày giao dịch không hưởng quyền (GDKHQ) sàn đã điều chỉnh giá xuống, nhưng cổ phiếu hoặc tiền chỉ về tài khoản sau đó 1–2 tháng. Với HPG trả cổ tức cổ phiếu 30%, giá tham chiếu giảm 23,08% ngay lập tức trong khi 300 cổ phiếu chưa về — danh mục trông như **bốc hơi 23%** dù không mất gì.

Cách xử lý: ghi nhận ngay tại ngày GDKHQ nhưng đánh dấu phần tăng thêm là **"chờ về"**. Màn hình vị thế hiển thị `1.000 CP (+300 chờ về)` — con số 1.000 vẫn khớp sổ công ty chứng khoán để đối chiếu, còn mọi phép tính lãi/lỗ dùng tổng 1.300. Khi cổ phiếu về thật, bấm "Xác nhận đã về", không con số nào nhảy.

**💰 Cổ tức tiền mặt gắn với từng mã.** Thêm hai cột trên màn hình vị thế: **Cổ tức đã nhận** và **Tổng lãi/lỗ gồm cổ tức**. Cổ tức tiền mặt không làm giảm giá vốn (đúng bản chất và đúng cơ sở tính thuế TNCN 5%), nên nếu chỉ nhìn cột "% lãi/lỗ" thì mã trả cổ tức đều như SAB sẽ trông như lỗ dần qua năm tháng. Cột mới trả lại bức tranh thật.

Lưu ý về đơn vị: "cổ tức 5%" nghĩa là 5% của **mệnh giá 10.000đ** = 500đ mỗi cổ phiếu, không phải 5% giá thị trường. Form nhập có ô xem trước để bạn kiểm tra trước khi lưu.

### Sửa lỗi

**🚨 Cảnh báo cắt lỗ kích hoạt nhầm sau ngày GDKHQ.** Ngưỡng cắt lỗ lưu giá tuyệt đối tại lúc đặt. Sau khi giá bị điều chỉnh giảm 23%, mọi ngưỡng cũ bị xuyên thủng ngay dù vị thế vẫn đang lãi. Nay ngưỡng cắt lỗ, mục tiêu và giá vào đều được điều chỉnh theo cùng hệ số.

**🧮 Giá vốn tính sai ở màn hình lãi/lỗ.** Phép tính cũ lấy trung bình toàn bộ lệnh mua kể cả phần đã bán hết, bỏ qua phí và thuế, và gắn nhãn tiền tệ USD. Nay tính lại đúng theo phần đang nắm giữ, có phí/thuế, đơn vị VND.

---

## [v2.69.0] — 2026-08-09 · Hàng đợi quyết định bắt được cả cơ hội mua

### Tính năng

**🎯 Hàng đợi quyết định nay nhìn được cả hai chiều.** Trước đây hàng đợi chỉ có tín hiệu phòng thủ — chạm stop-loss, kịch bản kích hoạt, thesis đến hạn review. Nó tự nhận là "hôm nay cần quyết gì" nhưng về cấu trúc thì không thể chứa một cơ hội mua. Nay thêm hai loại:

- **Cơ hội mua** — mã trong danh sách theo dõi có giá về ≤ "Mục tiêu mua" bạn đặt. Xếp **dưới** mọi cảnh báo rủi ro, vì nên dọn vị thế đang lỗ trước khi mua thêm. Muốn nhận thẻ này, hãy đặt "Mục tiêu mua" cho mã trong danh sách theo dõi — bỏ trống thì hệ thống không có mốc nào để so.
- **Thiếu stop-loss** — vị thế đang mở mà chưa đặt stop-loss.

### Sửa lỗi

**🕳️ Vị thế chưa đặt stop-loss bị bỏ qua hoàn toàn.** Hàng đợi trước đây lặng lẽ bỏ qua mọi vị thế chưa có stop-loss, nên hàng đợi rỗng đọc như "danh mục an toàn" trong khi thực tế là "rủi ro chưa đo được" — chính những vị thế nguy hiểm nhất lại vô hình.

**🔁 Bấm "Giữ + ghi lý do" xong thẻ hiện lại ngay.** Với thẻ không gắn kế hoạch giao dịch (điển hình là cảnh báo stop-loss), việc ghi nhận quyết định không được lưu đúng chỗ nên lần làm mới kế tiếp thẻ cũ quay lại y nguyên. Nay đã im đúng đến hết ngày.
---

## [v2.68.1] — 2026-07-28 · Sửa lỗi trợ lý AI không ghi được nhật ký

### Sửa lỗi

**📝 Nhờ trợ lý AI ghi nhật ký thị trường luôn thất bại.** Lệnh tạo nhật ký độc lập (và 9 lệnh ghi khác qua trợ lý AI) khai báo tham số sai hình dạng: trợ lý phải bọc toàn bộ dữ liệu trong một lớp `command` mới gọi được, nhưng không có gì trong mô tả cho biết điều đó. Kết quả: mọi lần gọi đều báo lỗi giống nhau bất kể sửa nội dung, nên trông như lỗi phía server — trong khi lệnh chỉ-đọc như xem danh sách nhật ký vẫn chạy bình thường.

- 10 lệnh ghi qua trợ lý AI nay nhận dữ liệu trực tiếp, không cần lớp bọc: tạo/sửa nhật ký độc lập, tạo/sửa nhật ký theo lệnh, tạo/sửa danh sách theo dõi, thêm/sửa mã trong danh sách, nhập rổ VN30, tạo/sửa kế hoạch giao dịch.
- Mỗi trường nay có mô tả tiếng Việt và ghi rõ giá trị mặc định khi bỏ trống, nên trợ lý chỉ cần gửi phần nó biết thay vì phải điền đủ mọi trường.
- Kế hoạch giao dịch tạo qua trợ lý AI nay **không thể** nhận trạng thái/mã lệnh từ bên ngoài — luôn ở trạng thái Nháp.

---

## [v2.68.0] — 2026-07-26 · Sửa lỗi bản tin hằng ngày báo sai tiền mặt

### Sửa lỗi

**💰 Bản tin báo "không có tiền mặt" dù tài khoản còn số dư.** Bản tin chỉ đọc tiền mặt từ hồ sơ tài chính cá nhân, nên tiền thu về từ các lệnh bán bị bỏ sót hoàn toàn. Hậu quả nặng hơn con số hiển thị: đó cũng là nền vốn dùng để tính khối lượng gợi ý, nên **mọi gợi ý khối lượng mua đều thấp hơn thực tế**.

- Bản tin nay tách rõ **tiền trong tài khoản chứng khoán** (gồm tiền vừa bán) và **tiền nhàn rỗi ngoài tài khoản**. Phần tiền trong tài khoản luôn được hiển thị, kể cả khi chưa lập hồ sơ tài chính.
- Sửa chỉ số lợi nhuận bị lệch: trước đây cộng lãi/lỗ đã thực hiện vào tử số nhưng mẫu số chỉ là giá vốn phần đang nắm, làm con số âm nặng hơn thực tế. Nay tách thành **lợi nhuận chưa thực hiện** (trên giá vốn đang nắm) và **tổng lợi nhuận** (trên tổng tiền đã mua).
- Giá trị chưa lấy được nay hiển thị `n/a` thay vì `0` — trước đây một số liệu thiếu bị trình bày như một sự thật.

### Tính năng

**🧭 Bản tin có đủ dữ kiện để ra quyết định.** Bổ sung vào bản tin hằng ngày:

- Bóc số liệu **theo từng danh mục** + **lãi/lỗ đã thực hiện** (trước đây gộp chung nên không biết nên xử lý ở danh mục nào).
- Bảng **vị thế đầy đủ**: tên danh mục, khối lượng, giá vốn, % tỷ trọng danh mục, khoảng cách tới điểm cắt lỗ. Mã chưa đặt cắt lỗ được ghi rõ "chưa đặt".
- **Lệnh 14 ngày gần nhất** — để không nhận định một vị thế mà bỏ qua việc vừa bán bớt.
- **Hàng đợi quyết định** hôm nay (chạm cắt lỗ / scenario trigger / đến hạn review luận điểm), sắp theo mức nghiêm trọng.
- **Cảnh báo rủi ro theo rủi ro thật** thay cho ngưỡng "lỗ hơn 5%" cũ: xuyên cắt lỗ, sát cắt lỗ, tập trung quá mức, chưa đặt cắt lỗ, lỗ nặng.

---

## [v2.67.0] — 2026-07-26 · MCP P1: 8 tool Performance & Wealth Analytics (chỉ đọc)

### Tính năng

**📈 8 MCP read tool Analytics — "tôi đang làm ăn thế nào"** (slice P1 của roadmap mở rộng MCP): agent đánh giá được hiệu suất + của cải thay vì chỉ đọc trạng thái rủi ro.

- `AnalyticsTools`: `get_performance` (total/MTD/YTD), `get_equity_curve`, `get_monthly_returns`, `get_savings_comparison` (alpha vs gửi tiết kiệm, param `annualRate`/`asOf`), `get_campaign_analytics` (win rate + best/worst, lọc `timeHorizon`), `get_net_worth_summary` (health score), `get_flow_history` (nạp/rút/cổ tức, lọc `from`/`to`), `get_adjusted_return` (TWR + MWR).
- 6/8 tool per-portfolio (`portfolioId` required, ownership check ở handler). Toàn bộ `ReadOnly = true`. Tổng tool: **38 → 46**.
- Tests: +9 unit + discovery 46 tool + schema assertions cho optional params (`annualRate`, `from`/`to`).

---

## [v2.66.0] — 2026-07-26 · MCP: tool `get_daily_digest` (Phase B daily digest)

### Tính năng

**📰 MCP tool `get_daily_digest` (chỉ đọc)** — bản tin hằng ngày cho AI advisor (bối cảnh danh mục, số dư tiền mặt, gợi ý sizing) giờ lấy được qua MCP thay vì REST `POST /ai/daily-digest`, mở đường cho NPU `/stock` agent bỏ hẳn `curl`.

- Thin wrapper trên `IAiAssistantService.BuildDailyDigestAsync` — không args, không business logic mới; lỗi (`ErrorMessage`) → `McpException` để MCP client thấy tool error rõ ràng.
- Tổng tool: **37 → 38**. REST endpoint giữ nguyên (additive).
- Tests: +2 unit (passthrough userId + error path) + discovery 38 tool + schema-leak guard.

---

## [v2.65.0] — 2026-07-25 · MCP P0: 8 tool Decision & Risk Intelligence (chỉ đọc)

### Tính năng

**🧭 8 MCP read tool mới — "situational awareness" cho AI advisor** (slice P0 của roadmap mở rộng MCP): agent giờ đọc được trạng thái rủi ro/kỷ luật/quyết định thay vì chỉ CRUD hộ.

- `DecisionTools`: `get_decision_queue` (hàng đợi quyết định hôm nay — gộp alert StopLoss + Scenario + Thesis-review, dedupe + sort severity), `get_discipline_score` (0–100, 3 thành phần, param `days` 7/30/90/365), `get_discipline_streak` (chuỗi ngày không vi phạm SL), `get_pending_thesis_reviews` (plan quá hạn review thesis + days-overdue).
- `RiskTools`: `get_portfolio_risk` (volatility/VaR/Sharpe/max-drawdown theo danh mục), `get_stop_loss_targets`, `get_trailing_stop_alerts` (đều per-portfolio, ownership check ở handler), `get_scenario_advisories` (kịch bản đang vi phạm).
- Toàn bộ `ReadOnly = true` — host MCP không cần confirm. Re-dispatch MediatR query sẵn có, không thêm business logic. Tổng tool: **29 → 37**.
- Tests: +9 unit (UserId/param mapping) + discovery 37 tool + schema-leak guard cho 3 tool có `portfolioId`. Suite: **1.451 pass** (Domain 740, Api 167, Application 230, Infrastructure 314).

---

## [v2.64.0] — 2026-07-24 · MCP server: mở toàn bộ bề mặt agent qua Model Context Protocol

### Tính năng

**🔌 MCP server (co-host trong `InvestmentApp.Api`)** — cho phép nhiều MCP client (Claude Desktop, IDE, NPU, host khác) cắm thẳng vào Invest Mate qua **tool có schema**, thay vì đọc doc markdown rồi tự dựng `curl`.

- Endpoint `/mcp` (streamable HTTP, **stateless** — hợp Cloud Run multi-instance), sau **ApiKey scheme** hiện có (`UserId` = claim `sub`).
- **29 tool** = full parity với bề mặt agent REST: trade plans (list/get/create/update/set-status), trades (create + calculate_fees), portfolios, positions, symbol timeline, watchlists (CRUD + items + import-vn30), journals (CRUD), journal-entries (CRUD + pending-review + by-symbol).
- Mỗi tool re-dispatch đúng MediatR command/query có sẵn — **không thêm business logic**. Tool đọc gắn `ReadOnly`, tool ghi gắn `Destructive` → host MCP tự hỏi xác nhận.
- REST `/ai/agent/*` giữ nguyên (additive). MCP thay doc markdown bằng `tools/list` discovery.
- Package `ModelContextProtocol.AspNetCore 2.0.0-rc.1`. 35 test MCP pass (unit 29 tool + discovery annotations). Xem plan `docs/superpowers/plans/2026-07-24-mcp-server-implementation.md`.
- **Còn lại (manual):** verify kết nối 1 host thật + quyết định ApiKey-vs-OAuth cho từng host.

---

## [v2.63.1] — 2026-07-24 · Sửa lỗi trừ thuế TNCN 2 lần khi bán (P/L danh mục)

### Sửa lỗi

**🐛 Thuế TNCN bị trừ 2 lần trên lệnh BÁN** — mọi phép tính net (P/L danh mục, campaign review, hiệu suất, chiến lược, realized-PnL) đều dùng `- Fee - Tax`, coi Fee và Tax là 2 khoản **tách biệt**. Nhưng khâu tạo lệnh lại lưu `Fee = totalFees` (đã gồm cả TNCN), còn `Tax` lưu TNCN lần nữa → lệnh BÁN bị trừ thuế 2 lần, sai proceeds + P/L. (Lệnh MUA không ảnh hưởng — TNCN = 0.)

- **Fix producers** (giữ nguyên 7 consumer): lưu `Fee = phí giao dịch + VAT` (bỏ TNCN); `Tax` giữ TNCN riêng.
  - FE `trade-create.component.ts` (`onSubmit`).
  - Agent `AiAgentController.CreateTrade` (auto-fill dùng `transactionFee + vat`, không dùng `TotalFees`).
- `AgentTradeFeeCalculator.TotalFees` / preview `/fees/calculate` giữ nguyên (tổng all-in đúng cho hiển thị).
- Tests: cập nhật `CreateTrade_NullFeeTax_Sell_AutoComputes` (Fee=165k không gồm thuế) + `trade-create.component.spec.ts` onSubmit payload. Api **121 pass**, FE **138 pass**.
- Xem [ADR-0006](../../../docs/adr/0006-trade-fee-excludes-tax.md). **⚠️ Cần migration** cho lệnh BÁN cũ (`Fee = Fee - Tax` khi `Tax > 0`) — chạy riêng, có xác nhận.

---

## [v2.63.0] — 2026-07-23 · AI Agent: đủ thông tin khi mở/đóng vị thế (portfolio + phí/thuế)

### Tính năng mới

**🤖 Agent tự đủ thông tin khi ghi trade** — trước đây NPU/Claude khi ghi trade phải hỏi lại người dùng `portfolioId` (bắt buộc, không có cách lấy) và `fee`/`tax` (mặc định 0 → sai). Mở rộng ADR-0005; tái dùng 100% `GetAllPortfoliosQuery` + `FeeCalculationService`, không thêm business logic ở Domain/Application.

- **`GET /api/v1/ai/agent/portfolios`** — liệt kê danh mục của chủ khóa (lấy `portfolioId`).
- **`POST /api/v1/ai/agent/fees/calculate`** — tính phí/thuế cho một giao dịch dự kiến (mirror `FeesController`); TNCN 0.1% chỉ khi SELL.
- **`POST /api/v1/ai/agent/trades` nới lỏng:** `portfolioId` bỏ trống → auto-pick khi user có đúng 1 danh mục (0 hoặc >1 → `400` kèm hướng dẫn); `fee`/`tax` bỏ trống → tự tính (khớp cách app tính khi nhập tay), gửi giá trị kể cả `0` → giữ nguyên.
- **Kiến trúc:** logic resolve/auto-compute nằm ở agent controller layer — JWT `CreateTradeCommand` không đổi; new logic khu trú trên agent surface. Ownership double-fence: list lọc theo `sub` + handler re-assert `portfolio.UserId == sub`.
- **Doc tự cập nhật:** thêm mục Portfolios + Fees + ghi chú create-trade optional vào `Docs/AI-Agent-TradePlan-API.md` (serve qua `GET /ai/agent/doc` ETag/304). Guard test chống quên doc.

### Files chính

- `src/InvestmentApp.Api/Controllers/`: `AiAgentPortfoliosController.cs`, `AiAgentFeesController.cs`, `AgentTradeFeeCalculator.cs`, `AgentCreateTradeRequest.cs` (mới); `AiAgentController.cs` (enhance `CreateTrade` + inject `IFeeCalculationService`).
- `src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md` — +2 mục + ghi chú create-trade.
- `docs/adr/0005-agent-surface-auto-resolution.md` (mới).
- Tests: 11 xUnit mới (Api) — portfolio resolve 4 case, fee/tax null-vs-0, auto-resolve cả hai path, fees BUY/SELL, doc guard. **122 Api pass**, không regression.

---

## [v2.62.0] — 2026-07-23 · AI Agent: mở rộng bề mặt sang watchlist, danh mục & nhật ký (backend)

### Tính năng mới

**🤖 5 nhóm endpoint mới trên `api/v1/ai/agent/*`** (xác thực Khóa API) — để trợ lý NPU/Claude đọc danh mục thật, quản watchlist, ghi/đọc nhật ký và xem sự kiện theo mã trực tiếp như tool, không phải đi vòng qua bản tin `daily-digest`. Mở rộng ADR-0004; tái dùng 100% command/query sẵn có (không thêm business logic).

- **Positions** (đọc): `GET /positions` — holdings thật (qty, giá vốn, P/L).
- **Watchlist** (CRUD đầy đủ, 9 route): list/detail/create/update/delete + thêm/sửa/xóa mã + import VN30.
- **Journal Entries** (nhật ký theo mã, 5 route): create/update/delete + pending-review + tra theo mã.
- **Journals** (nhật ký theo trade, 5 route): list + theo trade + create/update/delete.
- **Symbol timeline** (đọc): `GET /symbols/{symbol}/timeline`.
- **Kiến trúc:** 5 controller anh em nhỏ, cùng kế thừa `AiAgentControllerBase` (giữ `IMediator` + `GetUserId()`), mỗi controller pin scheme `ApiKey` — theo precedent một-controller-một-scheme. Ownership khóa theo `sub` = chủ khóa ở tầng handler (đã test ở Application layer). Giữ nguyên mã trạng thái nguồn; POST `Created` trỏ Location về agent surface.
- **Doc tự cập nhật:** thêm 5 mục vào `Docs/AI-Agent-TradePlan-API.md` (embedded, serve qua `GET /ai/agent/doc` ETag/304) → NPU re-fetch thấy endpoint mới. Guard test chống quên cập nhật doc.
- **🔒 Vá IDOR (`CreateJournal`):** handler trước chỉ kiểm trade tồn tại, không kiểm chủ sở hữu → nay assert `portfolio.UserId == sub` (qua `IPortfolioRepository`) — chặn ghi nhật ký lên trade của người khác. Cùng pattern IDOR fix của PR #122; áp cho mọi caller (JWT lẫn ApiKey).

### Files chính

- `src/InvestmentApp.Api/Controllers/AiAgentControllerBase.cs` (mới) + 5 controller: `AiAgentPositionsController`, `AiAgentWatchlistsController`, `AiAgentJournalEntriesController`, `AiAgentJournalsController`, `AiAgentSymbolsController`.
- `src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md` — +5 mục (route + shape DTO + ví dụ).
- Tests: 37 xUnit mới (Api) — wiring/UserId-injection/route-binding/status-code + doc guard. 111 Api pass, không regression.

---

## [v2.61.0] — 2026-07-21 · AI Agent: bề mặt ghi trade plan qua Khóa API (backend)

### Tính năng mới

**🤖 Endpoint `api/v1/ai/agent/*`** — xác thực bằng Khóa API (`X-Api-Key`), mở rộng ADR-0003 sang thao tác **ghi**: để trợ lý NPU/Claude lập, sửa, chuyển trạng thái và thực hiện kế hoạch giao dịch từ chat sau khi người dùng "chốt". Re-dispatch các command sẵn có (không thêm business logic), tách controller riêng theo precedent `AiDigestController`.

- **Curated + guard ở adapter:** tạo plan luôn ép Draft (bỏ `status`/`tradeId`), chặn `restore` (400), gán `UserId`/`Origin` server-side. Loại các thao tác phá huỷ (delete/abort/review/bulk) khỏi bề mặt agent.
- **🔒 Vá IDOR:** `CreateTrade` và `BulkCreateTrades` trước đây chỉ kiểm portfolio tồn tại, không kiểm chủ sở hữu → nay assert `portfolio.UserId == sub`. Audit thêm dấu `Source=AI_AGENT`.
- **Tài liệu cho Claude:** embedded `Docs/AI-Agent-TradePlan-API.md`, serve qua `GET /ai/agent/doc` với `ETag=docVersion` (NPU cache local, conditional GET 304 — chỉ tải lại khi deploy version mới). Drift test đảm bảo doc không lệch command.

### Files chính

- `src/InvestmentApp.Api/Controllers/AiAgentController.cs` (mới) — controller ApiKey mỏng + serve doc.
- `src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md` (mới, embedded).
- `src/InvestmentApp.Application/.../CreateTrade` + `BulkCreateTrades` — thêm `UserId` + ownership assert + audit `Source`.
- `docs/adr/0004-ai-agent-write-surface-via-apikey.md` (mới, Accepted).
- Tests: xUnit mới (IDOR ownership ×2, adapter guards, ETag/304, doc drift) — Application 228 + Api 77 pass, không regression.

---

## [v2.60.0] — 2026-07-16 · Bản tin: bối cảnh thị trường + watchlist (backend)

### Tính năng mới

**📈 Bản tin đầu tư nay có bối cảnh thị trường & watchlist đầy đủ** — bổ sung vào `BuildDailyBriefingContext` (dùng chung cho endpoint `daily-digest` của NPU và chat bản tin trong app).

- **`<market_context>`** — VN-Index (điểm + %), độ rộng (mã tăng/giảm/trần/sàn), khối ngoại mua-bán ròng (tỷ VND). Giúp AI phân biệt "cả thị trường giảm" vs "chỉ mã của mình yếu" khi tư vấn tái cơ cấu danh mục.
- **`<watchlist>`** — bảng mã đang theo dõi: giá, %ngày, khoảng cách tới mục tiêu mua, kèm tín hiệu 📉/📈 khi chạm mục tiêu. Thay cho cảnh báo chỉ hiện khi đã chạm giá → luôn thấy cơ hội.
- Lấy dữ liệu song song, chịu timeout chung; thị trường/giá lỗi thì bỏ qua section, không vỡ bản tin. Contract endpoint không đổi.

### Files chính

- `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` — 2 helper `FormatMarketContextSection` / `FormatWatchlistSection` + orchestration + inject `IMarketDataProvider`.
- Tests: 5 xUnit mới (Infrastructure) — 314 pass, không regression.

---

## [v2.59.0] — 2026-07-15 · Daily digest endpoint cho trợ lý NPU (backend)

### Tính năng mới

**📰 Endpoint `POST /api/v1/ai/daily-digest`** — xác thực bằng Khóa API (`X-Api-Key`), là endpoint opt-in đầu tiên dùng ApiKey scheme (không cần JWT). Trả bản tin đầu tư hằng ngày để trợ lý NPU kéo theo cron rồi đẩy vào Claude phân tích timing.

- Bản tin nay bổ sung **vốn khả dụng & net-worth** (`<cash_and_net_worth>`: chứng khoán + tiền nhàn rỗi, net worth, tổng nợ, điểm sức khỏe tài chính) và **gợi ý khối lượng vị thế** (position-sizing) cho các kế hoạch giao dịch đang chờ.
- Mỗi khóa chỉ đọc được dữ liệu của chủ khóa. Bản tin trong app (daily-briefing) cũng được làm giàu tương ứng.

### Files chính

- `src/InvestmentApp.Api/Controllers/AiDigestController.cs` (mới).
- `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` — `BuildDailyDigestAsync` + bổ sung cash/net-worth & position-sizing vào `BuildDailyBriefingContext`.
- Tests: 8 xUnit mới (Infrastructure) — tổng backend 1313 pass.

---

## [v2.58.0] — 2026-07-15 · Khóa API cá nhân (Personal Access Token)

### Tính năng mới

**🔑 Trang "Khóa API"** (menu Quản lý) — cho phép user tạo và thu hồi Personal Access Token để công cụ ngoài (VD: trợ lý NPU, script tự động) gọi API thay mặt tài khoản mà không cần đăng nhập.

- **Tạo khóa**: đặt tên + chọn thời hạn 1–365 ngày → token đầy đủ hiển thị **đúng một lần** duy nhất khi tạo (không thể xem lại). Sau đó chỉ lưu hash bcrypt — không lưu token gốc.
- **Thu hồi**: xóa khóa bất kỳ lúc nào; token tương ứng mất hiệu lực ngay lập tức.
- **Danh sách khóa hiện có**: hiển thị tên, ngày tạo, ngày hết hạn, lần sử dụng cuối.

### Files chính

- `src/InvestmentApp.Domain/Entities/PersonalAccessToken.cs` (mới) — entity + hash logic.
- `src/InvestmentApp.Application/ApiKeys/...` — `CreateApiKey` / `RevokeApiKey` / `GetApiKeys` commands+queries.
- `src/InvestmentApp.Api/Controllers/ApiKeysController.cs` — 3 endpoints mới dưới `/api/v1/me/api-keys`.
- `frontend/src/app/features/management/api-keys/api-keys.component.ts` (mới) — trang quản lý + one-time reveal modal.

---

## [v2.57.0] — 2026-05-04 · Dashboard Decision Engine PR-3: Inline BÁN/GIỮ + đơn giản hóa Home

### Mục tiêu

Phase cuối của roadmap "Dashboard Decision Engine" (plan: [`docs/plans/dashboard-decision-engine.md`](../../../docs/plans/dashboard-decision-engine.md)). PR-3 ship 2 việc cùng lúc:
- **P4 — Inline action buttons** trong Decision Queue: mỗi alert có thể resolve ngay tại chỗ, không cần điều hướng.
- **P5 — Đơn giản hóa Home**: xóa 3 widget noise (Market Index strip, Mini Equity Curve, Quick Actions) khỏi Dashboard.

### UX mới

- **🔪 BÁN THEO KẾ HOẠCH** — trong mỗi item của Decision Queue, khi item có gắn TradePlan, hiện button đỏ. Click → confirm dialog `"Xác nhận BÁN {symbol} theo plan?"` → tạo Trade SELL với quantity tính từ plan + giá hiện tại. Item tự động biến mất khỏi list (optimistic remove).
- **✋ GIỮ + GHI LÝ DO** — button vàng mở textarea inline. **Bắt buộc nhập ≥ 20 ký tự** (counter `{{n}}/20 ký tự` real-time, button disabled cho đến khi đủ) — buộc bạn nghĩ kỹ trước khi bỏ qua tín hiệu cảnh báo. Sau submit, JournalEntry được tạo với loại `Decision` + tag `decision-hold` để tra cứu lại.
- **Error feedback rõ ràng** — nếu BÁN/GIỮ thất bại (e.g. plan đã bị xóa, position đã đóng), hiện banner đỏ ngay dưới item kèm lý do thay vì silent fail.
- **StopLossHit không có plan** — nếu alert là stop-loss thuần (không link plan), button BÁN ẩn; user dùng "Xử lý →" để tới `/risk-dashboard` xử lý thủ công.

### Đã xóa khỏi Home (đơn giản hóa)

- **Market Index strip** (4 ô VNINDEX/HNX/UPCOM/VN30) — không liên quan quyết định cá nhân, chỉ là noise macro. Đã có ở `/market-data`.
- **Mini Equity Curve chart** (~100 LOC chart logic + range buttons 30D/90D/1Y/All) — dùng để review post-hoc, không phải quyết định ngay. Full version đã có ở `/analytics`.
- **Quick Actions row** (4 link Wizard/Market/Journals/Risk) — trùng với menu header + bottom-nav.
- **GIỮ Watchlist** — vẫn ở Home để phá pre-trade routine (kỷ luật entry).

### Tests

- **11 xUnit mới** trong `ResolveDecisionCommandHandlerTests`: ExecuteSell single-lot/multi-lot/user isolation/portfolio ownership defense/plan-not-found/no-executed-lots, HoldWithJournal short note/link plan/symbol fallback/user isolation, validator. 191/191 Application + 729/729 Domain pass.
- **7 Karma mới** trong `DecisionQueueComponent`: BÁN call API + cancel confirm + expand note form + disabled short note + optimistic remove + hide BÁN no plan + show BÁN error. 30/30 dashboard widget tests pass.

### Files chính

- `src/InvestmentApp.Application/Decisions/Commands/ResolveDecision/ResolveDecisionCommand.cs` (mới, ~225 LOC) — command + validator + handler.
- `src/InvestmentApp.Domain/Entities/JournalEntry.cs` — thêm `JournalEntryType.Decision` enum value (additive, no migration).
- `src/InvestmentApp.Api/Controllers/DecisionsController.cs` — thêm `POST /api/v1/decisions/{id}/resolve`.
- `frontend/src/app/core/services/decision.service.ts` — thêm `resolve()` method + types.
- `frontend/src/app/features/dashboard/widgets/decision-queue.component.ts` — inline BÁN/GIỮ buttons + per-item error map.
- `frontend/src/app/features/dashboard/dashboard.component.ts` — xóa Market Index + Mini Equity Curve + Quick Actions (~237 LOC net delete).

---

## [v2.56.0] — 2026-05-04 · Dashboard Decision Engine PR-2: Decision Queue + Empty State Positive

### Mục tiêu

Phase thứ 2 của roadmap "Dashboard Decision Engine" (plan: [`docs/plans/dashboard-decision-engine.md`](../../../docs/plans/dashboard-decision-engine.md)). PR-2 ship P3 — gộp 3 widget alert rời (Risk Alert Banner + Advisory Widget + Pending Review section) thành **1 Decision Queue duy nhất** ở vị trí #1 trên Home, kèm empty state positive khi đang kỷ luật.

### UX mới

- **Decision Queue ở vị trí #1 trên Home** — gộp 3 nguồn alert thành 1 widget duy nhất với badge severity tiếng Việt (Khẩn cấp / Lưu ý / Thông tin). Cap 5 items hiển thị + overflow link "Xem tất cả → /risk-dashboard". Sort theo severity (Critical đầu tiên).
- **Empty state positive** — khi 0 alert → hiển thị `✅ Hôm nay đang kỷ luật` + 🔥 streak X ngày (số ngày liên tiếp gần nhất không vi phạm SL). Thay thế hành vi cũ "widget biến mất" → app không còn rỗng. Streak ẩn khi user chưa có plan.
- **Headline rõ ràng cho từng loại alert:**
  - StopLossHit Critical: `FPT đã thủng SL 89.5 (giá 89.4)`
  - StopLossHit Warning: `VNM cách SL 1.5% (SL 80,000)`
  - ScenarioTrigger: dùng message gốc của scenario advisory
  - ThesisReviewDue: `VNM thesis quá hạn review 5 ngày` hoặc `FPT đến hạn review thesis`
- **Dedupe thông minh** — cùng (Symbol, PortfolioId) xuất hiện ở cả StopLossHit + ScenarioTrigger → giữ Critical, drop Warning. Không spam user.

### Đã xóa khỏi Home

- Risk Alert Banner (29 dòng template + properties + `loadRiskAlerts` method 65 LOC).
- Advisory Widget (33 dòng template + properties + `loadAdvisories` method).
- Pending Review section (26 dòng template + `loadPendingReview` method).
- 3 widget này được Decision Queue thay thế hoàn toàn — không giữ duplicate UI để tránh confusion.

### Tests

- **14 xUnit mới**: 8 trong `GetDecisionQueueQueryHandlerTests` (aggregate 3 sources, dedupe, sort, multi-portfolio) + 6 trong `GetDisciplineStreakQueryHandlerTests` (no plans, no violations, SL violation detection mirror DisciplineScoreCalculator). 178/178 Application pass.
- **10 Karma mới** trong `DecisionQueueComponent` — empty state with/without streak, hides streak khi `hasData=false`, render N items, cap 5 + overflow, Vietnamese severity/type labels, action route helpers per type. 24/24 dashboard widget tests pass.

### Files chính

- `src/InvestmentApp.Application/Decisions/{DTOs,Queries}/...` — `DecisionItemDto`, `GetDecisionQueueQueryHandler` aggregate 3 sources qua `Task.WhenAll` + dedupe + sort.
- `src/InvestmentApp.Application/Discipline/Queries/GetDisciplineStreakQuery.cs` — handler tính `daysWithoutViolation` reuse logic SL-violation từ `DisciplineScoreCalculator`.
- `src/InvestmentApp.Api/Controllers/DecisionsController.cs` — `GET /api/v1/decisions/queue`. `DisciplineController` thêm `GET /me/discipline-score/streak`.
- `frontend/src/app/core/services/decision.service.ts` (mới) + extend `discipline.service.ts` với `getStreak()`.
- `frontend/src/app/features/dashboard/widgets/decision-queue.component.ts` (mới) — widget mount ở top of main content.
- `frontend/src/app/features/dashboard/dashboard.component.ts` — net delete ~180 LOC (3 widget cũ + orphan imports).

### Decisions chốt cho PR-2

- **Plan deviation #1**: plan giả định `IRiskService.GetStopLossAlertsAsync` không tồn tại — thực tế là per-portfolio `IRiskCalculationService.GetPortfolioRiskSummaryAsync`. User-level aggregation = iterate user portfolios. Mirror đúng logic dashboard frontend trước đó.
- **Plan deviation #2**: tên thực tế là `GetScenarioAdvisoriesQuery` (không phải `GetActiveAdvisoriesQuery`). Reuse trực tiếp.
- **DTO style**: dùng class (không record) để match convention codebase hiện có.
- **Streak algorithm**: derived-on-demand (chưa có collection daily snapshot). Logic mirror `DisciplineScoreCalculator.ComputeSlIntegrityAndStopHonor` để đảm bảo consistency với composite Discipline Score. Future PR có thể migrate sang stored snapshot nếu performance trở thành vấn đề.
- **Inline action (BÁN/GIỮ)** chuyển sang PR-3 (P4) — PR-2 chỉ render link "Xử lý →" tới page detail.

---

## [v2.55.0] — 2026-05-04 · Dashboard Decision Engine PR-1: Reality Gap CAGR + AI Phản biện

### Mục tiêu

Phase đầu tiên trong roadmap "Dashboard Decision Engine" — biến Dashboard từ "hiển thị trạng thái" thành "ép quyết định kỷ luật". Plan chi tiết: [`docs/plans/dashboard-decision-engine.md`](../../../docs/plans/dashboard-decision-engine.md). PR-1 ship 2 phase đầu (P1+P2), còn 3 phase tiếp theo (Decision Queue, Inline Actions, Remove noise widgets).

### UX mới

- **Reality Gap CAGR luôn hiển thị từ lần đầu mở Dashboard** — không cần click "Đặt mục tiêu" trước. Default target 15%/năm. Khi user lệch nhiều (progress < 50%) → hiển thị label đỏ `⚠️ Lệch X.X điểm % so với mục tiêu`.
- **NetWorth widget compact ở vị trí #2 trên Home** — block ngắn 3 dòng (Net Worth + Reality Gap CAGR), giúp user thấy "đang on-track không?" ngay khi mở app, không cần scroll xuống Compound Growth Tracker. Personal Finance widget existing giữ nguyên cho deep-dive.
- **AI rebrand "Bản tin Hôm nay" → "Phản biện danh mục"** — đổi vai AI từ news-reader (passive) → HLV phản biện (adversarial coach). Button label đổi `🤖 AI Bản tin Hôm nay` → `🥊 AI phản biện danh mục`. Prompt mới ép AI đưa ra **chính xác 3 điểm sai/yếu/lệch kỷ luật** + dùng động từ mệnh lệnh ('cắt', 'review', 'giảm') + KHÔNG khen, KHÔNG động viên. Ưu tiên thứ tự: vi phạm SL > thesis hết hạn > concentration > drawdown.

### Sửa lỗi nhỏ

- **Inconsistency label "Lệch X%"**: trước đây dùng tỉ lệ `(100 - progress)%`, đổi sang **điểm phần trăm** `(target - cagrValue) điểm %` — đồng nhất với NetWorth widget mới và đúng ngữ nghĩa hơn. Ví dụ: CAGR 5%, target 15% → label cũ "Lệch 67%" → label mới "Lệch 10.0 điểm %".

### Tests

- **6 xUnit mới** trong `AiAssistantServicePortfolioCritiqueTests` — lock prompt content (adversarial framing, không drift sang supportive theo update). 295/295 Infrastructure pass.
- **9 Karma mới** trong `NetWorthSummaryComponent` — render/hide/gap label/boundary cases (cagrValue = target, negative CAGR). 14/14 Karma widget tests pass.

### Files chính

- `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` — thêm `BuildPortfolioCritiqueSystemPrompt` (public static) + `BuildPortfolioCritiqueContext` (delegate data từ daily-briefing).
- `frontend/src/app/features/dashboard/widgets/networth-summary.component.ts` — standalone widget mới.
- `frontend/src/app/features/dashboard/dashboard.component.ts` — `cagrTargetSet=true` default, mount NetWorth widget, đổi gap label sang điểm %, AI rebrand.
- `frontend/src/app/core/services/ai.service.ts` + `frontend/src/app/shared/components/ai-chat-panel/ai-chat-panel.component.ts` — wire `portfolio-critique` use-case.

### Decisions chốt cho PR-1

- Use-case `daily-briefing` **giữ nguyên** trong service (không deprecate) — `BuildPortfolioCritiqueContext` reuse data aggregation logic của nó. Dashboard không expose `daily-briefing` button nữa.
- Personal Finance widget existing **không xóa** — coexist với NetWorth widget compact mới. Top widget = quick signal, mid widget = full breakdown.
- Không tạo `dashboard.component.spec.ts` lớn (mock 15+ services) — value/cost không xứng. Coverage qua widget-level spec + manual QA.

---

## [v2.54.0] — 2026-05-03 · Household CAGR + cảnh báo cửa sổ ngắn

### Sửa lỗi cốt lõi

**Headline CAGR trên Cockpit không còn lừa user.** Trước đây ô "CAGR hiện tại" lấy từ portfolio đầu tiên trong list (thường là portfolio nhỏ nhất, ví dụ 6.5% weight) → user có 2 danh mục, danh mục lớn (+25%/năm) bị "ẩn" sau danh mục nhỏ (−3%/năm) → headline đỏ trong khi tài sản thật đang lãi.

**Fix:** Thêm endpoint `GET /api/v1/analytics/household/performance` tính CAGR trên **toàn bộ danh mục của user**:
- Backend: `ICashFlowAdjustedReturnService.GetHouseholdReturnSummaryAsync(userId)` — gộp snapshot tất cả portfolio vào 1 series tổng (sum `TotalValue` mỗi ngày, carry-forward giữa các ngày miss), apply công thức TWR như per-portfolio (chia sẻ pattern `lastValidDate / lastValidValue / baselineEstablished` từ v2.53.1 — robust trước flow boundary và corrupt snapshots).
- **Late-join attribution**: portfolio mới tham gia sau ngày đầu của series → first-snapshot value tính như cash flow, không bị đọc nhầm thành "tăng trưởng" nửa-vốn.
- Annualize TWR → CAGR. Cùng guard cũ (`MinSnapshotValue=1000đ`, `MaxAbsPeriodReturn=500%`) để không bị 1 outlier phá cả chain.

### UX mới

- **Label đổi**: "CAGR hiện tại" → **"CAGR (toàn bộ N danh mục)"** khi N > 1.
- **Badge xám "⚠️ X ngày · chưa đủ 1 năm"** hiển thị khi `daysSpanned < 365` — tránh user lầm CAGR ngoại suy từ vài tuần là tốc độ thực. Stable threshold = 365 ngày (1 năm tròn).
- **Branch UI 3 nhánh** thay vì cryptic "--":
  - `daysSpanned ≥ 30` → CAGR annualized + badge "chưa đủ 1 năm" nếu `< 365`.
  - `1 ≤ daysSpanned < 30` → label đổi thành **"Tăng trưởng X ngày"**, hiển thị **TWR thô** (không annualize) + badge "Cần ≥ 30 ngày để có CAGR". Annualize từ 7 ngày sẽ ra số kỳ quặc; raw period return là honest hơn.
  - `daysSpanned < 1` (chưa có snapshot) → "--" + hint "Chưa có danh mục" hoặc "Chưa đủ snapshot" thay vì im lặng.
- Top header chip "Tổng tài sản" cũng dùng household CAGR (consistent).

### Tests

5 xUnit mới trong `CashFlowAdjustedReturnServiceTests`: empty user, single-portfolio = per-portfolio TWR, two aligned portfolios, late-join portfolio không inflate return, 365 ngày → `IsStable=true`.

### Files chính

- `src/InvestmentApp.Application/Common/Interfaces/ICashFlowAdjustedReturnService.cs` — thêm method + `HouseholdReturnSummary` DTO.
- `src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs` — implementation aggregate.
- `src/InvestmentApp.Api/Controllers/AdvancedAnalyticsController.cs` — endpoint `household/performance`.
- `frontend/src/app/features/dashboard/dashboard.component.ts` — bỏ `calculateCagrFromCurve` / `loadBackendCagr` / `calculateCagr` (per-portfolio), thay bằng `loadHouseholdCagr`.
- `tests/InvestmentApp.Infrastructure.Tests/Services/CashFlowAdjustedReturnServiceTests.cs` — 5 tests mới.

---

## [v2.53.1] — 2026-05-03 · Fix TWR — flow attribution across skipped periods

### Bug

Một user (truong.pham@evizi.com) hiển thị TWR `+139.26%` trên 55 ngày, vô lý so với P&L thực tế (+0.7%). Truy ngược: snapshot đầu tiên ngày 2026-03-09 có `TotalValue = -39.9M` (corrupt — bug PnLService cũ), kèm 1 flow Deposit 200M cùng đúng ngày 2026-03-09T00:00:00Z. Filter `f.FlowDate > snap[0].SnapshotDate` loại bỏ flow này khỏi MỌI period, trong khi snapshot 2026-03-11 đã reflect deposit. Period (03-11, 04-26] vì thế đọc value-jump 145M → 345M = +137% như "tăng trưởng" → cap `MaxAbsPeriodReturn=500%` không chặn vì 137% < 500%.

### Fix

`CashFlowAdjustedReturnService.CalculateTWRAsync` chuyển sang track `lastValidDate` + `lastValidValue`:

1. **Skip không advance baseline.** Khi period bị skip (corrupt prev value HOẶC outlier-capped), `lastValidDate` giữ nguyên ở snapshot trước đó. Period kế dùng nó làm boundary → flow window kéo dài cover skip-range, flows không biến mất.
2. **Boundary inclusive cho first valid period.** Khi baseline chưa established (chỉ qua skip), filter dùng `>= lastValidDate` thay vì `> lastValidDate`. Trường hợp truong.pham: flow 03-09T00:00 == snap[0].date → giờ nằm trong period (03-09, 04-26] và được attribute đúng.

### Verify

- truong.pham@evizi.com "Đầu tư tăng trưởng": TWR `139.26%` → `0.80%` ✓
- 4 tests mới + 1 test strengthened trong `CashFlowAdjustedReturnServiceTests` (boundary inclusion, flow during skipped first period, flow during outlier period, outlier strengthened to assert exact value 5%). Tất cả 1193 backend tests pass.

### Files

- `src/InvestmentApp.Infrastructure/Services/CashFlowAdjustedReturnService.cs` — refactor `CalculateTWRAsync` với pattern `lastValidDate / lastValidValue / baselineEstablished`
- `tests/InvestmentApp.Infrastructure.Tests/Services/CashFlowAdjustedReturnServiceTests.cs` — 3 tests mới + 1 strengthened

### Out-of-scope

- Snapshot data corruption (`SnapshotService` ghi V<0) — bug riêng, sẽ cleanup `db.portfolio_snapshots` thủ công và fix root cause sau.
- Household CAGR (`/analytics/household/performance`) — đã queue trong PR khác (cần fix này merge trước).

---

## [v2.53.0] — 2026-04-26 · Worker → Cloud Scheduler migration (free-tier friendly)

### Hạ tầng (không có thay đổi UI)

**Mục tiêu:** đưa workload background của app vào diện free tier Cloud Run sau khi `invest-mate-worker` always-on burns ~2,6M vCPU-seconds/tháng (vượt 360K free tier ~7×).

**Thay đổi chính:**
- ❌ **Xoá hẳn** service `invest-mate-worker` khỏi codebase (`src/InvestmentApp.Worker/`, `Dockerfile.worker`, `cloudbuild.yaml` worker steps).
- ✅ **API endpoints mới** `/internal/jobs/{prices,snapshot,exchange-rate,scenario-eval}` (controller `InternalJobsController`) trigger từ Cloud Scheduler qua OIDC ID token.
- ✅ **Auth scheme `GcpOidc`** + policy `GcpScheduler` (validate Google issuer/audience, email_verified=true, email ∈ `Jobs:AllowedSchedulerSAs` allowlist) — fail-closed nếu env var rỗng.
- ✅ **`BacktestQueueService`** singleton in-process queue trong API (thay polling `BacktestJob`); recover Pending backtests on startup → không mất việc khi Cloud Run scale-down giữa chừng.
- ✅ Logic `PriceSnapshotJob` extract sang `IPriceSnapshotJobService` (Infrastructure) — testable, reusable từ controller.

**Tests mới:** 8 PriceSnapshotJobService + 8 SchedulerEmailAllowlist + 4 InternalJobsController + 3 BacktestQueueService = **23 tests mới**.

**Deploy steps cuối (manual gcloud, owner thực hiện):** xem [docs/plans/done/worker-to-scheduler-migration.md](../../../docs/plans/done/worker-to-scheduler-migration.md) Phase 5 — tạo scheduler service account, 3 cron jobs, set `Jobs__AllowedSchedulerSAs` + `Jobs__ExpectedAudience` env vars trên API.

**Kết quả mong đợi:** Cloud Run cost từ overrun ~$X/tháng → $0 (in free tier). Không thay đổi UX user-facing.

ADR: [docs/adr/0001-worker-to-scheduler.md](../../../docs/adr/0001-worker-to-scheduler.md)

---

## [v2.52.0] — 2026-04-24 · So sánh hiệu suất đầu tư với tiết kiệm

### Tính năng mới

**📊 Tab mới "So sánh với tiết kiệm"** trong `/analytics` — trả lời câu hỏi kinh điển: _"Nếu tôi gửi tiết kiệm cùng số tiền đó, đã được bao nhiêu rồi?"_.

**Rate picker 3 preset:**
- **Sổ của tôi** (default): tính trung bình lãi suất các sổ tiết kiệm user đã nhập (weighted theo số dư). Hiển thị disclose "N/M sổ có nhập lãi suất" để minh bạch.
- **Cao nhất thị trường**: top lãi suất 12T từ `24hmoney.vn/lai-suat-gui-ngan-hang` (scraper mới `HmoneyBankRateProvider`, kênh online). Có ⚠ tooltip "Chỉ tham khảo" — vì user thực tế chỉ được lãi này nếu đã chọn đúng NH đó.
- **Tự nhập**: slider 0-30%/năm, tự clamp nếu vượt range.
- Fallback 5%/năm nếu user chưa có Savings hoặc chưa nhập lãi suất nào.

**Client-side recompute**: đổi rate → không round-trip server. Backend trả `flows[]` + `actualCurve[]` 1 lần, FE tự tính hypothetical qua JS `Math.pow` — responsive tức thì.

**Metrics hiển thị:**
- Danh mục thực tế | Nếu gửi tiết kiệm | Chênh lệch (VND + %)
- Neutral gray khi |Δ| ≤ 2% (tránh red-on-every-dip gây anxiety)
- **Chênh lệch hiệu suất năm** chỉ hiện khi danh mục ≥ 365 ngày; dưới 1 năm chỉ hiện period diff (tránh CAGR variance lớn)
- Disclaimer "Không phải lời khuyên đầu tư" ở footer

### Backend mới

**`HmoneyBankRateProvider`** — scrape 24hmoney, AngleSharp parser, dual-tier cache (6h fresh + 24h stale). Ưu tiên table online (cao hơn quầy 0.2-0.8%). Skip cells `-` (không công bố). 2 endpoints mới:
- `GET /api/v1/analytics/portfolio/{id}/vs-savings?savingsRate=&asOf=` → `SavingsComparisonDto`
- `GET /api/v1/analytics/bank-rates` → top rate per term (1/3/6/9/12 tháng)

**`HypotheticalSavingsReturnService`** — pure math service. Running-balance iterative + monthly compound. Filter Deposit/Withdraw only (bỏ Dividend/Interest/Fee tránh double-count).

### Bug-catchers đã cover (từ critical review)

- **Withdrawal compounding**: deposit 100M → rút 100M sau 180 ngày @ 6% phải cho ≈ 3M (lãi đã sinh trước khi rút), KHÔNG phải 0.
- **Dividend double-count**: chỉ Deposit/Withdraw mới tính vào "cơ hội cất vào ngân hàng".
- **asOf normalize**: `.Date` để tránh partial-day compound leak khi `asOf = UtcNow`.
- **OpportunityCostPercent null**: khi hypothetical ≤ 0 (withdraw-heavy), percent undefined thay vì giả 0.
- **Leap year**: actual day count, không hardcode 365.

### Env-var cần set trước deploy

```
BankRateProvider__PageUrl=https://24hmoney.vn/lai-suat-gui-ngan-hang
```

Startup log warn nếu env var chưa resolve.

### Tests

- +11 scraper (`HmoneyBankRateProviderTests` + fixture `hmoney_lai_suat_page.html` 210KB, captured 2026-03-25)
- +7 hypothetical (`HypotheticalSavingsReturnServiceTests` — 2 bug-catchers)
- +11 query handler (`GetSavingsComparisonQueryHandlerTests`)
- +6 FE spec (`analytics.component.spec.ts` — client-side recompute, preset switching)
- **Full solution: 1,163 backend pass** + 6 FE new

Chi tiết: [`docs/plans/done/investment-vs-savings-comparison.md`](plans/done/investment-vs-savings-comparison.md).

---

## [v2.51.0] — 2026-04-24 · Sổ tiết kiệm có kỳ hạn

### Tính năng mới

**📅 Ngày mở sổ + ngày đáo hạn cho sổ tiết kiệm.** Khi thêm/sửa tài khoản loại **Tiết kiệm**, user có thể nhập thêm 2 trường ngày optional để track sổ có kỳ hạn (fixed-term deposit):
- **Ngày mở sổ** (tùy chọn) — ngày bắt đầu gửi.
- **Ngày đáo hạn** (tùy chọn) — ngày sổ hết kỳ hạn.
- Hàng chip **kỳ hạn chuẩn**: `[1T] [3T] [6T] [12T] [24T] [Tùy chỉnh]` — nhập ngày mở sổ rồi bấm chip, ngày đáo hạn tự tính (+1/3/6/12/24 tháng). UTC math để không bị TZ drift.
- Card tài khoản hiển thị "📅 dd/MM/yyyy → dd/MM/yyyy" (chỉ khi có).

**Áp dụng cho làn sóng V1.2 tiếp theo:** 2 trường này là foundation cho tính năng **"So sánh hiệu suất đầu tư với tiết kiệm"** sắp tới (opportunity cost vs. bank rate).

### Bug fix & domain

- **Fix pre-existing leak**: `onTypeChange()` không null `formInterestRate` khi đổi type khỏi Savings → state rác leak lên backend. Giờ null cả 3 field Savings-only (lãi suất + 2 date) cùng lúc.
- **Domain enforce**: `DepositDate`/`MaturityDate` chỉ áp dụng cho `Type=Savings` (giống pattern `InterestRate`). Khi cả 2 set → `Maturity >= Deposit` (fat-finger guard).
- **UTC normalization**: handler normalize 2 date về UTC midnight như `Debt.MaturityDate` (FE gửi "YYYY-MM-DD" → tránh TZ drift 1 ngày).
- **CreatedAt** thêm cho `FinancialAccount` (đã có trên `Debt` — xóa bất đối xứng). Immutable sau Create. Docs Mongo cũ không có field này → default `DateTime.MinValue`, chấp nhận (không migration).

### Tests

- +11 Domain tests (`FinancialProfileTests.cs`)
- +4 Application tests (`UpsertFinancialAccountCommandHandlerTests.cs`)
- +7 Frontend spec (`personal-finance.component.spec.ts` — **mới tạo**, chưa có trước đó)
- **Full solution: 1,140 tests pass** (Domain 729 / Application 150 / Infrastructure 249 / Api 5 + 7 FE).

### Process

2-agent review plan (trước code) + 1-agent code review (sau code, fixed 1 Major UTC math finding trước commit). Chi tiết: [`docs/plans/done/savings-term-dates.md`](plans/done/savings-term-dates.md).

---

## [v2.50.0] — 2026-04-23 · Vin-discipline V2.1 — Pending reviews page + locale vi-VN

**PR #94 merged (squash `304421dc`)** — 4 commits: `1f8998a` query+endpoint+page → `d01aee6` Việt hóa UI + hide widget → `ec257dc` review fixes (timezone + perf + flash) → `160c0f8` locale vi-VN global.

### Tính năng mới

**🔔 Trang `/pending-reviews`** — liệt kê plan đang chạy (Ready/InProgress) có **lý do đầu tư cần review**: `InvalidationRule.CheckDate` sắp tới (±2 ngày VN local) hoặc `ExpectedReviewDate` đã quá. Card hiển thị urgency color (amber 0-2 ngày / đỏ ≥ 3 ngày) + badge trigger cụ thể (KQKD lệch / Gãy trend kỹ thuật / Tin tức đột biến / Quá hạn / Tự nhận xét / Review định kỳ). Sort theo `DaysOverdue` DESC — urgent nhất lên đầu. "Mở plan →" link về form Trade Plan đã load sẵn.

**Dashboard widget — count badge:** "🔔 [N] Plan cần review lý do đầu tư →" trong Discipline widget footer. Widget **ẩn khi chưa có plan nào** (`totalPlans === 0`) hoặc đang lỗi — tránh spam "Chưa đủ dữ liệu" cho user mới.

**API:** `GET /api/v1/me/thesis-reviews/pending` (DisciplineController). Dùng `GetActiveByUserIdAsync` DB-level filter (bỏ Cancelled/Reviewed), skip `LegacyExempt`, exclude rule đã triggered.

### Locale vi-VN globally

Đăng ký `vi-VN` locale trong `main.ts` — toàn bộ `DatePipe`/`CurrencyPipe` tự format kiểu Việt Nam (dd/MM/yyyy, dấu chấm ngăn hàng nghìn). Sửa 1 chỗ `'yyyy-MM-dd'` legacy. **Lưu ý:** `<input type="date">` vẫn theo OS locale browser (Angular không control được).

### Việt hóa "Thesis" → "Lý do đầu tư"

Feedback retail VN: "Thesis" quá xa lạ, "lý do đầu tư" gần hơn. Đổi xuyên suốt UI 4 files (widget / trade-plan form / pending-reviews / trade-replay). Giữ TypeScript identifiers không đổi (`thesis` property, `ThesisTimeout` enum, `PendingThesisReviewDto`, API route `/thesis-reviews/pending`).

### Review fixes trước merge (3-agent review)

1. **Timezone VN UTC+7:** `DaysOverdue` dùng `TimeZoneInfo` chuyển về VN local date trước khi compare → tránh off-by-one cho user Vietnam (lúc 09:00 VN thấy "Còn 1 ngày" cho plan due today).
2. **Perf:** chuyển sang `GetActiveByUserIdAsync` (DB filter Cancelled/Reviewed + IsDeleted) thay vì load toàn bộ plan history rồi filter in-memory.
3. **Widget flash:** `onPeriodChange()` reset `score = null` trước fetch → không hiện stale period cũ.
4. **Skip LegacyExempt:** plan cũ được migrate không có thesis thật, không nên nag user review.
5. **Badge chi tiết hơn:** thay "Điều kiện sắp tới hạn" chung chung → "KQKD lệch" / "Gãy trend kỹ thuật" cụ thể.

### Tests

- 10 handler tests mới (`GetPendingThesisReviewsQueryHandlerTests`) cover: empty, no review date, CheckDate due/far-future, ExpectedReviewDate past, triggered excluded, multi-reason aggregation, urgency sort, Draft/Executed excluded.
- Total: 146 Application + 718 Domain + 249 Infrastructure pass.

### V2+ Roadmap — **trial window 1-2 tuần**

User đang thử nghiệm UX thực tế trước khi invest thêm. Deferred: V2.2 cron worker, V2.3 behavioral pattern handler, V3 drill-down report, V4 Core/Satellite, V5 drawdown escalation. Chi tiết: [`docs/project-context.md`](../../docs/project-context.md).

---

## [v2.49.0] — 2026-04-23 · Kỷ luật Thesis kiểu Vin (Vin-discipline) — V1 Backend

**Branch:** `fix/post-trade-review-tradeid-wiring` (2 commits: d7a4bda domain/application/API/migration + 8fd0e8b discipline widget backend)

Áp kỷ luật **thesis-driven** vào quy trình lập Trade Plan, lấy cảm hứng từ Vinpearl Air 2019-2020 — Vingroup dám rút khỏi dự án đã đầu tư sâu khi thesis gốc không còn đúng. App trước giờ chỉ ép kỷ luật **giá** (SL, Risk Budget) mà bỏ trống kỷ luật **thesis** — user có thể tạo plan `Reason = null` hoặc `"mua"` không falsifiable. Phiên bản này thêm 3 thay đổi chính:

### 1. Rename `Reason` → `Thesis` + thêm InvalidationCriteria / ExpectedReviewDate / LegacyExempt

- **`TradePlan.Thesis`** (rename từ `Reason`) — không còn free-form "mua" / `null`. Thesis phải falsifiable (nêu con số/điều kiện cụ thể: EPS, ROE, MA, volume...).
- **`TradePlan.InvalidationCriteria`** — `List<InvalidationRule>?` với 5 trigger cố định (`EarningsMiss` / `TrendBreak` / `NewsShock` / `ThesisTimeout` / `Manual`). Mỗi rule có `Detail` falsifiable ≥ 20 ký tự + optional `CheckDate` (ngày dự kiến verify, vd ngày công bố BCTC).
- **`TradePlan.ExpectedReviewDate`** — ngày dự kiến review lại thesis (V2 sẽ dùng cho nudge auto).
- **`TradePlan.LegacyExempt`** — `true` cho plan tạo trước migration; graduated deprecation T+0 → T+1 → T+3 → T+6 tháng.
- **Migration-first deploy gate**: `scripts/migrations/2026-04-23-tradeplan-thesis-rename.mongo.js` rename `reason → thesis` + init field mới, 2-step idempotent. Chạy **trước** khi deploy code mới (MongoDB driver 3.6.0 không hỗ trợ BsonElement alias → nếu deploy trước migration sẽ silent data loss).
- Backend command `CreateTradePlan`/`UpdateTradePlan` giữ deprecation shim field `Reason` 1 release để client cũ không vỡ.

### 2. Size-based discipline gate + Abort workflow

**Gate cứng khi transition Draft → Ready / Draft → InProgress** (fold vào `MarkReady()` và `MarkInProgress()`):

- Plan size **≥ 5% tài khoản** (`Quantity × EntryPrice ≥ 5% AccountBalance`) → bắt buộc Thesis ≥ 30 ký tự + ≥ 1 invalidation rule với Detail ≥ 20 ký tự.
- Plan size nhỏ hơn (hoặc AccountBalance null) → chỉ cần Thesis ≥ 15 ký tự, rule optional.
- Object fact từ form input → không cheatable như self-attestation bucket.
- Throw `InvalidOperationException` → HTTP 400 với code `DISCIPLINE_GATE_FAILED` + field name để frontend highlight.

**Mid-flight abort (`AbortWithThesisInvalidation`)** — method mới cho phép đóng plan khi thesis sai, áp cho state `Ready | InProgress | Executed` (multi-lot partial-executed vẫn abort được). Khác với `Cancel()` — ép ghi `trigger + detail` để tạo learning loop. Raise `TradePlanThesisInvalidatedEvent` (phục vụ P7 Behavioral Pattern Detection: `DisciplinedAbort` vs `SunkCostHold`). `Restore()` sau abort tự động clear `IsTriggered` flags.

- **Endpoint mới:** `POST /api/v1/trade-plans/{id}/abort` body `{ trigger, detail }` → `AbortTradePlanCommand`.

### 3. Discipline Score widget backend — "Điểm Kỷ luật Thesis"

- **Endpoint mới:** `GET /api/v1/me/discipline-score?days=90` (dropdown 7/30/90/365, default **90 ngày** để giảm noise cho solo user 5-15 lệnh/tháng).
- **Composite 0-100** = weighted avg 3 sub-metric: **SL-Integrity 50%** (stop-honor rate trừ sl-widened-rate) + **Plan Quality 30%** (% plan pass gate) + **Review Timeliness 20%** (% plan review thesis đúng hạn).
- **Primitive hiển thị:** **Stop-Honor Rate** dạng "87% (13/15 lệnh)" — trades lỗ đã đóng với exitPrice ≥ plannedSL / tổng trades lỗ đã đóng.
- **Multi-lot matching per-lot** theo `TradeIds`. **Sell direction flip sign** (kiểm tra `exitPrice ≤ plannedSL`).
- **Null-safe re-normalize**: sub-metric có denominator = 0 → null, weighted avg chỉ tính trên sub-metric non-null. Cả 3 null → overall = null, label "Chưa đủ dữ liệu".
- **Label color-code:** ≥ 80 xanh "Kỷ luật Vin" / 60-79 vàng "Cần cải thiện" / < 60 đỏ "Trôi dạt".
- **Cache 5 phút** (`IMemoryCache`), invalidate on `TradeClosedEvent` / `PlanReviewedEvent` / `TradePlanThesisInvalidatedEvent`.

### Tests

Tổng **1106 tests pass** (tăng 43 so với v2.48.0). V1 thêm:

- **Domain +23:** `TradePlanAbortTests.cs` (abort flow cho Ready/InProgress/Executed/Reviewed/Cancelled + restore clear IsTriggered) + `TradePlanDisciplineGateTests.cs` (size-based gate 8 cases + Vietnamese diacritic thesis + Sell direction flip) + updates cho `TradePlanTests`/`TradePlanScenarioTests`/`TradePlanReviewTests`.
- **Application +6:** Discipline score calculator tests + `AbortTradePlanCommandHandler` + update `GetScenarioHistoryQuery` handler tests.
- **Infrastructure +14:** `DisciplineScoreCalculator` integration tests + update `CampaignReviewService`/`ScenarioAdvisoryService`/`ScenarioEvaluationService` tests.

### Scope V1 (Backend only) — FE & V2 defer

- V1 ship **backend + migration** để unblock deploy prod. Frontend form thay đổi (bind `reason` → `thesis`, thêm section "Điều kiện thesis sai", Dashboard widget "Kỷ luật Thesis" cạnh Risk Alert) **chưa trong commit này**, sẽ ship ở patch tiếp theo.
- V2 roadmap: `ThesisReviewService` (hosted cron daily) + endpoint `/me/thesis-reviews/pending` + Dashboard nudge "N thesis cần review hôm nay" + behavioral pattern handler.

### Docs

- Plan đầy đủ: [`docs/plans/plan-creation-vin-discipline.md`](docs/plans/plan-creation-vin-discipline.md) — 4 vòng refinement + multi-agent review, graduated deprecation matrix.
- `business-domain.md` + `architecture.md` cập nhật TradePlan entity + `InvalidationRule` VO + `TradePlanThesisInvalidatedEvent` + rule #16 size-based gate + 2 endpoint mới.
- `features.md` thêm section "Thesis-driven Plan Discipline (Vin-style)".
- `project-context.md` tick V1 backend done + pitfall BsonElement alias.

---

## [v2.48.1] — 2026-04-22 · Fix: không đánh giá được sau khi bán

**Branch:** `fix/post-trade-review-tradeid-wiring`

Từ trang Giao dịch hoặc Dashboard, bấm icon bút chì "Chưa đánh giá" trên một lệnh BÁN sẽ mở `/symbol-timeline?symbol=XXX` — nhưng page không biết đang review lệnh nào và form "+ Ghi nhật ký" không gán `tradeId` nên backend không đánh dấu được lệnh đã đánh giá. Kết quả: bấm mãi vẫn "Chưa đánh giá", user không có đường vào chức năng review.

### Fix
- **Backend `GetSymbolTimelineQuery`**: thêm `TradeId` vào journal DTO projection để frontend phát hiện được journal đã gắn với trade nào (trước đó bị thiếu → banner "Đã đánh giá" là dead code).
- **Trades page + Dashboard card** truyền thêm `tradeId` vào query params khi điều hướng sang Symbol Timeline.
- **Symbol Timeline**:
  - Nhận `?tradeId=...` → tự mở form nhật ký với `entryType = PostTrade`, prefill giá + thời điểm từ lệnh gốc, tiêu đề gợi ý "Đánh giá giao dịch BÁN {symbol} — {ngày}".
  - Hiển thị **banner cam** phía trên form: "Đang đánh giá giao dịch BÁN X cp {symbol} @ giá — ngày". Nếu đã đánh giá rồi → banner xanh "Giao dịch này đã được đánh giá" (không mở form để tránh tạo review trùng).
  - Guard SELL-only: link tay `?tradeId=<buy>` không kích hoạt review mode (post-trade review chỉ áp cho lệnh đóng vị thế).
  - Khi ở review mode, mặc định mở rộng khoảng thời gian sang **12 tháng** để tìm được các lệnh cũ từ danh sách "Chờ đánh giá" của Dashboard.
  - `createJournalEntry()` gửi kèm `tradeId` → backend `GetTradesPendingReviewQuery` cross-reference đúng → lệnh chuyển sang trạng thái "Đã đánh giá" (dấu tick xanh).
  - Sau khi lưu, xóa `tradeId` khỏi URL để refresh không mở lại form.
  - Nút trong form sắp xếp lại: `[Hủy] → [Lưu đánh giá]` theo convention primary-right.

---

## [v2.48.0] — 2026-04-22 · Tài chính cá nhân: Khoản nợ + Net Worth

**Branches:** `docs/personal-finance-debt-plan` (Phase 1 + plan), `feat/personal-finance-debt-application` (Phase 2), `feat/personal-finance-debt-api-frontend` (Phase 3-5)

Mở rộng Personal Finance để track các khoản nợ (thẻ tín dụng, vay ngân hàng, vay mua nhà, trả góp, …) → **Net Worth = Tài sản − Nợ** làm chỉ số chính thay Total Assets. Thêm health rule 4 bảo vệ nhà đầu tư khỏi nợ tiêu dùng lãi cao — trả nợ thẻ tín dụng 24-36% trước khi mua cổ phiếu thường là "khoản đầu tư" lãi kép tốt nhất.

### Domain

- **`Debt`** entity embedded trong `FinancialProfile.Debts[]`, 6 loại (`DebtType` enum: CreditCard/PersonalLoan/Mortgage/Auto/Installment/Other). Fields: `Principal` (required), `InterestRate`, `MonthlyPayment`, `MaturityDate`, `Note`, timestamps.
- **`FinancialProfile.UpsertDebt` / `RemoveDebt`** symmetric với rule account: Principal ≥ 0, không xóa được khi Principal > 0 (chống xóa nhầm dữ liệu thật).
- **`GetTotalDebt`, `GetNetWorth(securitiesValue)`, `HasHighInterestConsumerDebt()`** — API domain mới. Net Worth có thể âm (nợ > tài sản) — không throw, để user thấy thực tế.
- **Health score rule 4**: −20 cứng (binary) khi có `CreditCard`/`PersonalLoan` với `InterestRate > 20%/năm` (strict). Ngưỡng cutoff theo thực tế VN. `Mortgage`/`Auto`/`Installment` không áp — lãi thấp và có bảo đảm.

### Application

- `UpsertDebtCommand` + `RemoveDebtCommand` (MediatR), persist chỉ sau khi domain validate pass (no persist-on-throw).
- `GetNetWorthSummaryQuery` DTO mở rộng: `TotalDebt`, `NetWorth`, `HasHighInterestConsumerDebt`, `Debts[]`. `BuildRuleChecks` thêm rule 4 `HighInterestDebt` với encoding binary (CurrentValue=1 khi vi phạm, 0 khi đạt).
- `PersonalFinanceMapper.ToDto(Debt)` + `FinancialProfileDto.Debts[]`.

### API

- **`PUT /api/v1/personal-finance/debts`** — upsert khoản nợ, domain exception → 400 với Vietnamese message.
- **`DELETE /api/v1/personal-finance/debts/{debtId}`** — throw 400 nếu `Principal > 0` hoặc debt không tồn tại.

### Frontend

- Trang `/personal-finance`:
  - **Net Worth card** (gradient emerald/red theo âm/dương) thay đổi dạng nổi bật, Total Assets + Total Debt làm sub-metric.
  - **Banner đỏ high-interest debt** inline khi `HasHighInterestConsumerDebt`.
  - Section **"Khoản nợ"** dưới Accounts: click toàn card mở edit modal, empty state "Chưa có khoản nợ — không nợ là lợi thế khi đầu tư 🎯".
  - Debt form modal: 6 option dropdown, fields Principal/InterestRate/MonthlyPayment/MaturityDate/Note.
  - ESC đóng modal. Nút layout theo convention mới: `[Hủy] → [Xóa (conditional)] → [Lưu flex-1]`.
  - Overlay `z-[60]` (fix header cover bug).
- Dashboard widget "Tài chính cá nhân":
  - Đổi primary display từ `TotalAssets` → `NetWorth` (màu theo dấu).
  - Sub-line hiển thị "Tổng tài sản X − Nợ Y".
  - Banner đỏ warning khi high-interest consumer debt.
- `PersonalFinanceService`: thêm `upsertDebt()`, `removeDebt()`, types `DebtDto`/`DebtType`/`UpsertDebtRequest`, helpers `debtTypeLabel`/`debtTypeIcon`.

### Tests

- **Domain (+25)**: UpsertDebt/RemoveDebt CRUD + GetTotalDebt/GetNetWorth + HasHighInterestConsumerDebt edge cases (boundary 20%, 20.01%, null interest, non-consumer types) + health rule 4 interactions (clamp at 0, score 80 vs 100).
- **Application (+11)**: 5 Upsert (happy, update existing, no-profile, negative principal, bad DebtId) + 3 Remove + 2 summary extensions + rule count update 3→4.

**Total**: 1055 tests pass (687 Domain / 128 Application / 235 Infrastructure / 5 Api).

### Docs

- Plan archived: [`docs/plans/done/personal-finance-debt.md`](docs/plans/done/personal-finance-debt.md).
- Guide `tai-chinh-ca-nhan.md` pending update (next pass) — dashboard widget + debt section UX.
- `business-domain.md` + `architecture.md` + `project-context.md` updated với Debt entity, endpoints mới, rule 4, convention modal z-index + button order.

---

## [v2.47.2] — 2026-04-22 · Fix: Gold auto-calc dùng BuyPrice thay SellPrice

Sửa logic định giá vàng trong Personal Finance. Trước đây Balance = quantity × **SellPrice** (giá tiệm bán ra) — đó là giá user phải **trả khi đi mua thêm**, không phải giá tài sản đang giữ. Giờ đổi sang **BuyPrice** (giá tiệm mua vào = giá user bán được nếu thanh khoản ngay), phản ánh đúng giá trị tài sản thực tế, không cộng ảo phần spread mua–bán (1–3 triệu/lượng tùy loại) vào tổng tài sản.

- Backend `UpsertFinancialAccountCommandHandler.ResolveBalanceAsync`: `price.SellPrice` → `price.BuyPrice`.
- Frontend preview label "Giá Bán ra hiện tại" → "Giá mua vào hiện tại", `goldPreviewSellPrice` → `goldPreviewBuyPrice`.
- Test `Handle_Gold_AutoCalcBalance_FromProvider` update expectation 2 × 169,500,000 → 2 × 167,000,000.
- Docs cập nhật (`business-domain.md`, `architecture.md`, `project-context.md`, `tai-chinh-ca-nhan.md`).

---

## [v2.47.1] — 2026-04-22 · Fix: Tài chính cá nhân — Securities sync + UX redesign

**Branch:** `fix/personal-finance-securities-and-ux`

Fix bug user report: card "Chứng khoán" top (389.310.000đ live) khác với card Chứng khoán trong Tài khoản list (0đ stored). Đồng thời redesign UX tài khoản theo feedback: nút Sửa/Xóa quá gần nhau, nên gộp vào popup edit, kèm bảo vệ chống xóa nhầm.

### Bug fix

- **DTO projection override**: `GetNetWorthSummaryQuery` giờ set `Balance` của Securities account trong list `Accounts` = live `securitiesValue` tính từ portfolios, thay vì trả stored 0. Top card và list card đồng nhất.

### Domain rules mới

- **Securities không tạo thủ công**: `FinancialProfile.UpsertAccount` reject khi thêm account thứ 2 type=Securities (profile đã auto-provision 1 khi Create). Edit by-id vẫn OK.
- **Securities không xóa thủ công**: `RemoveAccount` luôn reject Securities (trước đây chỉ reject khi là last).
- **Không xóa tài khoản có dữ liệu**: `RemoveAccount` reject mọi account có `Balance > 0`. User phải set balance=0 trước khi xóa — chống xóa nhầm.

### Frontend UX

- **Card tài khoản**: toàn card clickable (non-Securities) → mở popup edit. Hiện hint "Sửa ›" bên phải để làm rõ affordance.
- **Securities card**: không clickable, hiển thị nhãn "Auto-sync" — không sửa/xóa được.
- **Dropdown loại tài khoản**: bỏ option "Chứng khoán" (chỉ hiện 4 loại: Tiết kiệm, Dự phòng, Nhàn rỗi, Vàng).
- **Nút Xóa**: di chuyển từ card vào trong popup edit, kèm điều kiện `Balance = 0`. Hiện message nhắc khi bị disable.
- **Phím ESC**: đóng popup edit (HostListener `document:keydown.escape`).

### Tests

- Domain: +3 tests (UpsertAccount_AddingSecondSecurities, RemoveAccount_Securities_ShouldAlwaysThrow, RemoveAccount_NonSecuritiesWithPositiveBalance, RemoveAccount_GoldWithPositiveBalance, RemoveAccount_GoldWithZeroBalance, UpsertAccount_UpdatingExistingSecurities). Update 2 existing tests để dùng balance=0.
- Application: +1 test (Securities DTO balance = live securitiesValue). Update 1 existing test.
- Total: 1024 pass (665 Domain + 119 Application + 235 Infrastructure + 5 Api).

### Docs

- `frontend/src/assets/docs/tai-chinh-ca-nhan.md` — cập nhật phần "Thêm tài khoản" + section mới "Sửa / Xóa tài khoản" mô tả flow mới.

---

## [v2.47.0] — 2026-04-22 · Admin: Tổng quan user + activity stats

**Branch:** `feat/admin-user-overview`

Mở rộng admin tool (B1 Phase 2) — thêm trang tổng quan toàn bộ user với thống kê hoạt động; restructure `/admin` thành layout có left sidebar để sau này thêm menu mới chỉ cần thêm 1 entry.

### Tính năng

- **Trang mới `/admin/users/overview`** — bảng paginated hiển thị toàn bộ user + stats:
  - Role (Admin/User), # Portfolio, # Trade, giao dịch cuối, đăng nhập cuối, impersonate cuối.
  - Pagination (20/50/100) với total count.
  - Nút "Xem như" impersonate inline cho từng row.
- **Admin layout mới** `/admin` với left sidebar (2 menu: "Tổng quan user", "Tìm & Impersonate") — mặc định redirect sang overview. Extensible: thêm feature admin mới chỉ việc push thêm item vào `menu[]` và thêm child route.
- **`User.LastLoginAt`** — track timestamp đăng nhập gần nhất. Cập nhật trong `AuthController.GoogleCallback` cho cả new user và existing user. Không cập nhật khi refresh token hay impersonate.

### Backend

- **Domain** — `User.LastLoginAt` (nullable) + method `RecordLogin()` (3 unit tests: default null, sets UtcNow, idempotent overwrite).
- **Application** — `GetUsersOverviewQuery` + `UsersOverviewResult` + `UserOverviewDto`. Handler verify role=Admin, gọi `IUserRepository.GetPagedAsync`, aggregate cross portfolio/trade/impersonation repos. 3 unit tests (unauthorized, happy path với stats, empty page).
- **Repository interfaces mới:**
  - `IUserRepository.GetPagedAsync(page, pageSize)` — sort theo CreatedAt desc, clamp pageSize ≤ 200.
  - `IPortfolioRepository.GetIdsByUserIdsAsync(userIds)` — batch lookup, return dict {userId → portfolioIds}.
  - `ITradeRepository.GetStatsByPortfolioIdsAsync(portfolioIds)` — batch aggregate {portfolioId → (count, lastTradeAt)}.
  - `IImpersonationAuditRepository.GetLatestStartedAtByTargetAsync(targetUserId)` — tận dụng index `{ targetUserId, startedAt desc }` đã có.
- **Api** — `GET /api/v1/admin/users/overview?page=&pageSize=` với `[RequireAdmin]`.

### Frontend

- `core/services/admin.service.ts` — thêm `getUsersOverview()` + types `UserOverviewDto`/`UsersOverviewResult`.
- `features/admin/admin-layout.component.ts` — standalone layout với sidebar + `<router-outlet>`.
- `features/admin/users-overview.component.ts` — bảng stats + pagination + "Xem như" modal (tái dùng flow impersonate hiện có).
- Route tree: `/admin` → `AdminLayoutComponent` với children `users/overview` (default), `users/search` (existing). `/admin/users` redirect → `users/overview` để giữ backward compat.
- Header ADMIN link đổi target `/admin/users` → `/admin`.

### Tests

- Backend: 1019 green (Domain 661, Application 118, Api 5, Infrastructure 235). Thêm 3 domain + 3 application tests mới.
- Frontend: ng build OK.

---

## [v2.46.0] — 2026-04-22 · Tài chính cá nhân + Gold Price Crawler (Tier 3)

**Branches:** 6 PR — `feat/personal-finance-{domain,application,gold-crawler,api,frontend,docs}` (PR #77–#82 + docs PR)

Feature Tier 3 từ improvement plan — tổng quan tài sản cá nhân (CK + vàng + tiết kiệm + dự phòng + nhàn rỗi), nguyên tắc tài chính với health score 0-100, và crawler giá vàng live từ 24hmoney. Ship qua 6 phase nhỏ để review/rollback dễ.

### Tính năng chính

- **Trang mới `/personal-finance`**: onboarding form → 5-card net worth → health score bar + 3 rule checks → accounts CRUD → settings.
- **Dashboard widget** "Tài chính cá nhân" clickable với breakdown + health bar + onboarding variant.
- **Gold form auto-calc**: user chọn SJC/DOJI/PNJ/Other + Miếng/Nhẫn + nhập quantity (lượng) → FE fetch live price → hiển thị Balance preview real-time. Fallback nhập tay nếu không dùng auto-calc.
- **Health score 0-100** với 3 rules (điểm trừ tỷ lệ vi phạm):
  - Quỹ dự phòng ≥ 6 tháng chi tiêu (-40 max)
  - Đầu tư (CK + Vàng) ≤ 50% tổng tài sản (-30 max)
  - Tiết kiệm ≥ 30% tổng tài sản (-30 max)
  - Vàng cộng vào investment (cùng CK) theo định nghĩa user "vàng cũng là mục đầu tư". Không cộng vào savings.
- **Securities auto-sync** giá trị từ `IPnLService.CalculatePortfolioPnLAsync(...).TotalMarketValue`, aggregate across all user portfolios — không cần nhập tay.

### Backend

- **Domain** — `FinancialProfile` aggregate (per-user 1:1, unique UserId) + `FinancialAccount` embedded + `FinancialRules` value object + 3 enums (`FinancialAccountType`, `GoldBrand`, `GoldType`). Methods: `Create`/`UpdateMonthlyExpense`/`UpdateRules`/`UpsertAccount`/`RemoveAccount`/`GetTotalAssets`/`CalculateHealthScore`. Guard "last Securities không được xóa".
- **Application** — 3 commands (UpsertFinancialProfile, UpsertFinancialAccount with Gold auto-calc, RemoveFinancialAccount) + 3 queries (GetFinancialProfile, GetNetWorthSummary, GetGoldPrices). `IGoldPriceProvider` interface + `PersonalFinanceMapper`. `UpsertFinancialAccountCommandHandler.ResolveBalanceAsync` xử lý Gold auto-calc: 3 fields đủ → fetch price → `Balance = quantity × sellPrice`. Provider null → throw 400 (không silent fallback).
- **Infrastructure** — `HmoneyGoldPriceProvider` crawler giá vàng từ `24hmoney.vn/gia-vang` bằng AngleSharp 1.3.0. Không có JSON API nên scrape SSR HTML. Filter chỉ Miếng + Nhẫn (skip nữ trang/trang sức). **Quirk**: giá HTML là full VND (167,200,000) mặc dù UI label nói "triệu VNĐ/lượng" — không scale ×1000. Two-tier cache: fresh 5 phút + stale 6h fallback. `FinancialProfileRepository` Mongo với unique index UserId, narrow catch `MongoCommandException when (ex.Code is 85 or 86)` để defensive với index conflict.
- **Api** — `PersonalFinanceController` với 6 endpoints JWT-authed: GET / (profile, 404 nếu absent), GET /summary (net worth + `hasProfile` flag), GET /gold-prices, PUT / (upsert profile), PUT /accounts (upsert với Gold auto-calc), DELETE /accounts/{id}.
- **Config** — `appsettings.json` thêm section `GoldPriceProvider` với placeholder `{GoldPriceProvider__PageUrl}` theo convention. Env var bắt buộc set trước deploy: `GoldPriceProvider__PageUrl=https://24hmoney.vn/gia-vang`.

### Frontend

- **`core/services/personal-finance.service.ts`** — HTTP client + TypeScript DTOs + 3 enums match backend numeric serialization (comment warning nếu BE đổi sang `JsonStringEnumConverter`) + static label helpers. `getProfile()` convert 404 → null qua `catchError + of(null)`.
- **`features/personal-finance/personal-finance.component.ts`** — standalone ~620 lines inline template. Onboarding form + 5-card net worth grid + health bar color-coded + 3 rule check rows + accounts cards grid (Edit/Delete, Securities không Edit) + collapsible settings + account form modal với Gold auto-calc. Cache gold prices invalidate mỗi lần mở form (tránh 7h-stale).
- **Dashboard widget** + onboarding variant, silent UI on error + `console.error` để dev diagnose.
- **Header nav**: "💰 Tài chính cá nhân" dưới group "Quản lý".

### Tests

| Layer | Test files | Tests |
|-------|-----------|:-----:|
| Domain | `FinancialProfileTests.cs` | 39 |
| Application | Commands + Queries | 22 |
| Infrastructure | `HmoneyGoldPriceProviderTests.cs` + `LiveSmoke.cs` | 17 |
| **Tổng feature** | | **78 mới** |

**Tổng solution: 1016 tests green** (Domain 661, Application 115, Infrastructure 235, Api 5). FE không thêm unit tests (consistent với precedent project).

### Deploy note

**⚠️ Trước khi deploy staging/prod, set env var:**
```
GoldPriceProvider__PageUrl=https://24hmoney.vn/gia-vang
```
Pattern giống `MarketDataProvider__BaseUrl`. Nếu quên, app không crash lúc startup — fail silently khi request `/gold-prices` đầu tiên với DNS error. Xem Section 11 Deploy checklist trong plan đã archive.

### Archived plan

Plan `docs/plans/personal-finance.md` move sang `docs/plans/done/personal-finance.md` sau khi verify full E2E.

---

## [v2.45.0] — 2026-04-21 · Admin Impersonation (debug tooling) — B1 Phase 1

**Branch:** `feat/capital-current-vs-initial`

Công cụ debug cho phép admin đăng nhập dưới tư cách user cụ thể để tái hiện bug dữ liệu theo UI mà user đó thấy. Hoàn toàn read-only ở MVP, có audit trail Mongo đầy đủ.

### Backend
- **Domain** — thêm `UserRole { User, Admin }` (default `User`), method `User.PromoteToAdmin()` / `DemoteToUser()`. Entity mới `ImpersonationAudit` (append-only, không phải AggregateRoot): `AdminUserId, TargetUserId, Reason, IpAddress, UserAgent, StartedAt, EndedAt?, IsRevoked`, method `Revoke()` set cả `IsRevoked=true` và `EndedAt`.
- **Application** — `IImpersonationAuditRepository`, `StartImpersonationCommand` (verify admin role + target tồn tại + không self-impersonate → tạo audit → gọi `IJwtService.CreateImpersonationToken` → log `AuditEntry`), `StopImpersonationCommand` (chỉ admin gốc mới stop được, gọi `audit.Revoke()`). Mở rộng `IJwtService` với `CreateImpersonationToken(adminId, target, impersonationId)`.
- **Infrastructure** — `ImpersonationAuditRepository` (collection `impersonationAudits`, indexes theo `adminUserId`/`targetUserId`/`isRevoked`). `JwtService.GenerateToken` thêm claim `role`. Token impersonate có claims `sub=target, actor=admin, impersonation_id, amr=impersonate`, TTL cố định 1h. `AdminBootstrapHostedService` đọc `Admin:AllowEmails` khi startup và `PromoteToAdmin()` idempotent (không override role đã có, try/catch tránh fail startup).
- **Api** — `[RequireAdmin]` attribute (chặn non-admin + chặn nested impersonate qua `amr` claim). `AdminController` với `POST /api/v1/admin/impersonate` + `POST /api/v1/admin/impersonate/stop`. `ImpersonationValidationMiddleware` chạy giữa `UseAuthentication` và `UseAuthorization`: validate `IsRevoked` (401 + `X-Impersonation-Revoked: true`), block mutation POST/PUT/DELETE/PATCH (403 + `MUTATION_BLOCKED_DURING_IMPERSONATION`) trừ khi `Admin:AllowImpersonateMutations=true` hoặc gọi stop endpoint, set header `X-Impersonating: true`.
- **Config** — `appsettings.json` thêm section `Admin:AllowEmails` (CSV string, placeholder `{Admin__AllowEmails}` giống các key khác) + `Admin:AllowImpersonateMutations` (default `false`). Giá trị thật set ở `appsettings.Development.json` cho local hoặc env var `Admin__AllowEmails="a@x.com,b@x.com"` cho Cloud Run — 1 env var duy nhất, dễ set hơn mảng. Bootstrap service tự skip nếu placeholder chưa được thay.

### Frontend
- **`core/services/impersonation.service.ts`** — `startImpersonate()` backup `auth_token`→`admin_auth_token`, `stopImpersonate(skipApiCall?)` restore. Decode JWT lấy target email/name.
- **`core/interceptors/impersonation-revoked.interceptor.ts`** — functional interceptor catch 401 + `X-Impersonation-Revoked` → auto-restore admin token + toast warning. Đăng ký qua `withInterceptors([...])` ở `main.ts`.
- **`shared/components/impersonation-banner/`** — sticky red bar full-width ở trên cùng, hiển thị email target + nút "Thoát impersonate". Mount trước `<app-header>` trong `app.component.ts`.

### Tests
- Domain: `UserRoleTests` (4) + `ImpersonationAuditTests` (6) — tổng 10 tests mới.
- Application: `StartImpersonationCommandHandlerTests` (4) + `StopImpersonationCommandHandlerTests` (3) — tổng 7 tests mới.
- Infrastructure: `JwtServiceImpersonationTests` (4) — role claim trên login token + 3 claim của impersonate token + TTL 1h.
- **Tổng suite: 926 tests green (trước: ~907).**

### Admin UI (Phase 2 follow-up, same PR)
- **`GET /api/v1/admin/users?email=<q>`** — search user theo email (partial, case-insensitive), limit 10, exclude caller. `SearchUsersQuery` + handler + `IUserRepository.SearchByEmailAsync` (Mongo regex).
- **`AdminGuard`** (`core/guards/admin.guard.ts`) — check JWT `role=Admin` + chặn khi đang impersonate (`amr=impersonate`).
- **`AdminService`** (`core/services/admin.service.ts`) — gọi API search.
- **`/admin/users` page** (`features/admin/admin-users.component.ts`) — input email, list results, modal nhập reason, bấm "Xem như user này" → gọi `ImpersonationService.startImpersonate()` → reload.
- **Header link `ADMIN`** — chỉ hiện khi admin login (và không đang impersonate).

### Security notes
- Bootstrap admin thông qua env `Admin__AllowEmails__0=admin@example.com` → tránh phụ thuộc code deploy để grant admin.
- Nested impersonate bị chặn ở 2 tầng: `[RequireAdmin]` attribute (controller) và middleware không cho phép nested token.
- Mutation block là default — admin phải có lý do cụ thể mới bật `Admin__AllowImpersonateMutations=true`.
- Audit trail Mongo không bao giờ xoá (append-only), mỗi phiên impersonate = 1 document.

### Docs
- `docs/architecture.md` — thêm section "Admin Impersonation (Debug Tooling)" + Admin controller trong bảng endpoints + cập nhật folder `Authorization/` và `Middleware/`.
- `docs/business-domain.md` — thêm UserRole + ImpersonationAudit vào entity map + Admin vào API endpoints + rule #10 về impersonation flow.
- `docs/plans/multi-user-access-plan.md` — Phần 2 B1 Phase 1 marked implemented (plan gốc đã có spec `§2.7`).

---

## [v2.44.1] — 2026-04-20 · Backend version on /health endpoints

**Branch:** `feat/capital-current-vs-initial`

### CI / CD
- **`Dockerfile.api`** — added `ARG APP_VERSION=dev` + `ENV APP_VERSION=${APP_VERSION}` in runtime stage so the image carries its build identity.
- **`cloudbuild.yaml` (active Cloud Run path)** — API build step now passes `--build-arg APP_VERSION=$SHORT_SHA`. Cloud Build's built-in `$SHORT_SHA` substitution (7-char commit SHA) gets baked into the image at build time.
- **`.github/workflows/cd.yml` (GHCR path, not the live deploy)** — mirrored the same wiring: "Compute short SHA" step + `build-args: APP_VERSION=...` on the API `docker/build-push-action`. Included for parity so future use of the GHCR/self-hosted deploy path stays in sync.

### Backend
- **`src/InvestmentApp.Api/Program.cs`** — `/health`, `/health/live`, `/health/ready` all return a new `version` field sourced from `APP_VERSION` env (`"dev"` fallback when unset/empty). Lets `curl /health` after deploy confirm which commit is actually running.

### Bug fix during rollout
- First attempt shipped only the `cd.yml` edit — `/health` still returned `"version":"dev"` in prod because the live deploy goes through Cloud Build (`cloudbuild.yaml` → Cloud Run), not GitHub Actions. Added the missing `--build-arg` to `cloudbuild.yaml` in the same PR.

### Docs
- `docs/architecture.md` — documented `version` field on health endpoints.

---

## [v2.44.0] — 2026-04-19 · Fix TWR / MWR / CAGR (P3)

**Branch:** `feat/capital-current-vs-initial`

### Bug fixes — math
- **Backend `CashFlowAdjustedReturnService.CalculateTWRAsync`**: period return `(V_i − V_{i-1} − C_i) / V_{i-1}` blew up (observed +8.9M%) when a snapshot had near-zero `TotalValue` or a single period had extreme return. Added `MinSnapshotValue = 1000đ` guard (skip period) and `MaxAbsPeriodReturn = 5.0` cap (skip >500% single-period outlier). One bad snapshot no longer corrupts the product chain.
- **Backend `CashFlowAdjustedReturnService.CalculateMWRAsync` + `GetAdjustedReturnSummaryAsync`**: `currentValue` used `cashBalance = InitialCapital + flows − pnl.TotalInvested`. But `pnl.TotalInvested` is cost basis of **currently open positions** — diverges from gross historical buys after any position is closed (same bug fixed in v2.43.0 for the capital-flows page). Now uses gross `Σ(BUY qty×price+fee+tax) − Σ(SELL qty×price−fee−tax)` from `ITradeRepository`, matching the `/capital-flows` hero math.
- **Backend MWR Newton-Raphson**: added divergence guard (rate ∈ [−0.999, 100]) + warning log when it fails to converge; returns 0 instead of garbage.
- **Backend `PerformanceMetricsService.CalculateCAGRAsync`** (analytics endpoint `/analytics/portfolio/{id}/performance` — used as FE fallback): snapshot path was `(V_last/V_first)^(1/years) − 1`, same flow-agnostic bug as the FE CAGR. Now delegates to `ICashFlowAdjustedReturnService.CalculateTWRAsync` then annualizes `(1 + TWR)^(1/years) − 1`. Trade-path fallback (when no snapshots exist) was using `pnl.TotalInvested` (open-position cost) — now uses gross `Σ(BUY …) − Σ(SELL …)` + `InitialCapital + netFlow` formula, consistent with MWR.
- **Backend `PerformanceMetricsService.GetFullPerformanceSummaryAsync.totalReturn`**: same raw-endpoint bug on the period-total return. Now returns flow-adjusted TWR directly (falls back to gross PnL % only when no snapshots).
- **Frontend `dashboard.component.ts: calculateCagrFromCurve`**: was `(V_last / V_first)^(1/years) − 1` — ignores flows between first and last snapshot. A net-deposit would show fake huge CAGR; a net-withdraw (the observed case) produced **CAGR −21.5%** on a portfolio that's actually +4.09%. Now annualizes backend TWR (flow-adjusted) → `(1 + TWR)^(1/years) − 1`. Falls back to endpoint ratio only if TWR unavailable.

### Backend
- `CashFlowAdjustedReturnService` ctor now takes `ITradeRepository` + `ILogger<>`.
- `PerformanceMetricsService` ctor now takes `ICashFlowAdjustedReturnService` (no circular dep; adjusted-return service does not depend on metrics).

### Tests
- `CashFlowAdjustedReturnServiceTests` (new) — 8 tests: no-portfolio, <2-snapshot, normal TWR, TWR with flow, near-zero snapshot doesn't blow up, outlier period skipped, MWR flat-portfolio ≈0, MWR uses gross trade values for cash balance (closed-position regression case).
- `PerformanceMetricsServiceCagrTests` (new) — 7 tests: CAGR uses annualized TWR (not raw endpoints), negative TWR annualizes, short window returns 0, TWR<-100% doesn't crash, no-snapshot trade fallback uses gross totals (closed-position regression), no-snapshot-no-trade returns 0, full-summary `TotalReturn` = TWR.
- All 904 backend tests pass.

### Docs
- `docs/plans/p3-twr-mwr-cagr-fix.md` → moved to `done/` with status update
- `CHANGELOG.md` v2.44.0

---

## [v2.43.0] — 2026-04-19 · Capital-flows — Hero cards (aggregate + per-portfolio)

**Branch:** `feat/capital-current-vs-initial`

### Frontend
- Trang `/capital-flows` thêm **2 tầng hero**:
  - **Tổng quan ({{ n }} danh mục)** — luôn hiện ở trên cùng, aggregate qua tất cả danh mục (cash + market value + return + allocation + breakdown). Fetch từ `/pnl/summary`.
  - **Chi tiết: {tên danh mục}** — hiện khi chọn 1 danh mục từ dropdown, cùng cấu trúc layout nhưng data riêng cho portfolio đó.
- Thay vì chỉ có flow aggregates (Tổng nạp/rút/cổ tức/dòng ròng), user giờ thấy được **bức tranh tổng quát** ngay khi mở page + drill-down khi cần
- Mỗi hero gồm: Tổng tài sản + % return vs Vốn hiện tại, allocation bar (Giá trị thị trường vs Tiền mặt), breakdown (Vốn ban đầu / Dòng vốn ròng / L/L chưa TH / đã TH)
- Reload `OverallPnLSummary` + `portfolios` sau record/delete flow để không stale
- Switch portfolio → clear `portfolioPnL` / `flowHistory` / `adjustedReturn` ngay để tránh hiển thị data lẫn lộn
- Inject `PnlService` để lấy market value + realized/unrealized P&L

### Bug fixes (từ code review)
- **Allocation bar overflow** khi `cashBalance < 0` (overbought/margin edge case): bar widths clamp [0, 100] qua getter `marketBarWidth` / `cashBarWidth`
- **Double-fire `loadFlowData`** khi user đến page qua `?portfolioId=xyz` rồi record/delete flow: `loadPortfolios` giờ chỉ auto-select nếu chưa có portfolio đang chọn
- **Dấu âm hiển thị đôi** ở totalReturn (pipe đã thêm `-` + template prefix `↘ `): dùng `absTotalReturn` với explicit sign prefix

### Bug fixes (aggregate math)
- **Backend `PnLController.GetOverallPnL`**: `totalNetCashFlow += netCashFlow` bị kẹt trong try block → portfolio không có trade làm PnL throw → skip luôn netCashFlow. Nhưng `totalInitialCapital` lấy tất cả portfolio → `totalCurrentCapital` bị lệch. Đã move ra ngoài try.
- **Frontend `overallView` cashBalance**: trước dùng `OverallPnLSummary.totalInvested` (= cost basis of OPEN positions từ PnLService) → sai sau khi đóng vị thế. Ví dụ: mua 100M, bán hết 120M → `open cost = 0` → cash bị tính thừa 100M. Fix: dùng `portfolios.reduce((s,p) => s + p.totalInvested)` — gross historical từ `PortfolioSummary`.

### Tests
- `capital-flows.component.spec.ts` (new) — 18 tests: per-portfolio getters (13 — normal / overbought / zero capital / loss / no-selection / partly invested) + `overallView` aggregate (5 — null-guards, 2-portfolio sum, totalSold aggregation, overbought clamp)
- Fix existing `trade-create.component.spec.ts` mock data (thêm `netCashFlow` + `currentCapital` fields từ Phase 1 interface update)

### Docs
- `CHANGELOG.md` v2.43.0

---

## [v2.42.0] — 2026-04-18 · Capital — Auto seed Deposit flow (Phase 3)

**Branch:** `feat/capital-current-vs-initial`

### Domain
- `CapitalFlow.IsSeedDeposit: bool` — đánh dấu flow tự sinh khi tạo portfolio (default false, backward compat cho data cũ)
- Constructor: thêm optional param `isSeedDeposit = false`

### Application
- `CreatePortfolioCommandHandler`: sau khi tạo Portfolio, tự sinh `CapitalFlow` type `Deposit` với `IsSeedDeposit=true`, note "Vốn ban đầu khi tạo danh mục", flowDate = portfolio.CreatedAt. Chỉ tạo khi InitialCapital > 0. Inject `ICapitalFlowRepository`.
- `GetFlowHistoryQueryHandler`: aggregates (`TotalDeposits/Withdrawals/Dividends/NetCashFlow`) **exclude** seed flow. `Flows` list vẫn include để audit trail đầy đủ.
- `CapitalFlowItemDto`: thêm `IsSeedDeposit: bool`
- `DeleteCapitalFlowCommandHandler`: chặn xoá seed flow (return false) — seed là opening balance, không được remove

### Infrastructure
- `CapitalFlowRepository.GetTotalFlowByPortfolioIdAsync`: exclude seed khi sum → giữ Phase 1 formula `CurrentCapital = InitialCapital + NetCashFlow` đúng cho cả portfolio cũ (không có seed) và mới (có seed)
- `CashFlowAdjustedReturnService.CalculateTWRAsync / CalculateMWRAsync / GetAdjustedReturnSummaryAsync`: exclude seed khỏi flow stream → fix **bug double-count** (seed được dùng làm NPV baseline qua `-portfolio.InitialCapital`, không phải cash flow bổ sung)

### Frontend
- `CapitalFlowItem` interface: thêm `isSeedDeposit: boolean`
- Capital-flows history table (desktop + mobile): seed row hiển thị badge "Vốn ban đầu" (bg-blue), ẩn nút Xoá, hiện text "Khoá"

### Không cần data migration
- Portfolio cũ: không có seed flow → `GetTotalFlow` trả Σ các flow thực → Phase 1 formula `InitialCapital + NetCashFlow` = đúng
- Portfolio mới: có seed flow → `GetTotalFlow` exclude seed → trả chỉ các flow thực → formula vẫn đúng

### Tests
- `CapitalFlowTests`: +1 test cho `IsSeedDeposit` property
- `CreatePortfolioCommandHandlerTests`: +2 tests (seed flow được tạo với đúng attrs; InitialCapital=0 không tạo flow)
- `DeleteCapitalFlowCommandHandlerTests`: 3 tests new (user flow xoá được, seed bị chặn, wrong user bị chặn)
- `GetFlowHistoryQueryHandlerTests`: 1 test new (seed trong Flows list, exclude khỏi aggregates)
- Backend: Domain 609 (+1), Application 81 (+4), Infrastructure 199 → **889 tests pass**

### Docs
- `CHANGELOG.md` v2.42.0, plan checkpoint

---

## [v2.41.0] — 2026-04-18 · Capital — Lock InitialCapital (Phase 2)

**Branch:** `feat/capital-current-vs-initial`

### Backend
- `UpdatePortfolioCommand`: xoá field `InitialCapital` — chỉ cho update `Name`
- `UpdatePortfolioCommandHandler`: xoá call `portfolio.UpdateInitialCapital(...)` — vốn không còn sửa được qua update endpoint
- `UpdatePortfolioCommandValidator`: xoá rule cho `InitialCapital`
- `Portfolio.UpdateInitialCapital()` domain method giữ lại nhưng không còn caller ở Application layer (có thể dùng cho data migration hoặc admin ops trong tương lai)

### Frontend
- `UpdatePortfolioRequest` interface: xoá `initialCapital`
- `portfolio-edit.onSubmit()`: chỉ gửi `{ name }` (trước đây gửi cả initialCapital)

### Quyết định domain
- Vốn danh mục chỉ đổi qua `CapitalFlow` (Deposit/Withdraw/Dividend/Interest/Fee). Không cho "sửa sổ sách" trực tiếp trên `InitialCapital` nữa → single source of truth, audit trail qua flow history.

### TWR/MWR NetCashFlow
- Đã verify: `CashFlowAdjustedReturnService.NetCashFlow = totalDeposits(all inflows) - totalWithdrawals(all outflows)` — mathematically đã bằng `Σ SignedAmount` dù tên biến hơi gây hiểu nhầm. Không cần sửa.

### Tests
- `UpdatePortfolioCommandHandlerTests` (3 tests) — new: name-only update, wrong-user, not-found
- Backend: 75/75 Application tests pass (+3)

### Docs
- `CHANGELOG.md` v2.41.0

---

## [v2.40.0] — 2026-04-18 · Capital — Vốn hiện tại vs Vốn ban đầu (Phase 1)

**Branch:** `feat/capital-current-vs-initial`

### Backend
- `PortfolioSummaryDto` + `PortfolioDto` thêm `NetCashFlow` và `CurrentCapital` (= InitialCapital + NetCashFlow)
- `GetAllPortfoliosQueryHandler` + `GetPortfolioQueryHandler`: inject `ICapitalFlowRepository`, gọi `GetTotalFlowByPortfolioIdAsync` per portfolio
- `PnLController.GetOverallPnL`: mỗi portfolio trả thêm `NetCashFlow`, `CurrentCapital`; tổng level thêm `TotalNetCashFlow`, `TotalCurrentCapital`
- Catch block chỉ wrap PnL calculation — flow fetch giờ nằm ngoài try (không silent-swallow DB error)

### Frontend
- `PortfolioSummary` + `PortfolioDetail` + `PortfolioPnL` + `OverallPnLSummary` thêm `currentCapital`, `netCashFlow`
- Dropdowns (5): capital-flows, position-sizing, trade-wizard, trade-plan, trade-create → hiển thị `currentCapital` thay `initialCapital`
- List/detail/dashboard card: hiển thị "Vốn hiện tại" làm primary, "Vốn ban đầu" làm secondary (nhỏ, gray)
- Dashboard `cashBalance` dùng `currentCapital - totalInvested` (thay cho initial+flow)
- Dashboard `getPerformancePercent` dùng `currentCapital` làm denominator

### Bug fix — Position sizing
- **4 trang risk** (position-sizing, trade-wizard, trade-plan, trade-create) trước đây dùng `portfolio.initialCapital` làm `accountBalance` → khi user đã nạp/rút thêm, tính size lệnh sai. Giờ dùng `portfolio.currentCapital`.
- `trade-create` `remainingCash` tính từ `currentCapital - totalInvested + totalSold` (đủ vốn đã nạp thêm).

### Tests
- `GetAllPortfoliosQueryHandlerTests` (3 tests) — new, cover inflow/outflow/no-flow cases
- `GetPortfolioQueryHandlerTests` (3 tests) — new, cover happy/wrong-user/not-found paths
- Backend: 72/72 Application tests pass (+3 tests from 69)

### Docs
- `docs/plans/p2-capital-current-vs-initial.md` — plan với checkpoints
- `docs/business-domain.md` §3.1 — update công thức

---

## [v2.39.0] — 2026-04-18 · Trade Plan Form Editability Matrix (Strict)

**Branch:** `feat/trade-plan-state-machine-and-ux`

### Frontend — Trade Plan
- Áp dụng matrix phân quyền chỉnh sửa form theo trạng thái (Option A — strict lock):
  - **Draft/Ready**: chỉnh sửa tự do
  - **InProgress**: chỉ được tighten SL + sửa lot chưa khớp + cập nhật ghi chú/context
  - **Executed/Reviewed/Cancelled**: read-only, chỉ sửa được ghi chú (trừ Cancelled)
- State banner mới ở đầu form — thông báo rõ state hiện tại + gợi ý thao tác tiếp theo
- Tighten-SL gate: chặn nới SL trong InProgress (Long: newSl ≥ currentSl; Short: newSl ≤ currentSl)
- Readonly affordance: input locked đổi sang `bg-gray-50 text-gray-600 cursor-not-allowed`
- Save buttons hiện theo state (Draft: Nháp+Ready; Ready: Cập nhật; InProgress: Cập nhật SL/lot/ghi chú; terminal: view-only)
- Template panel ("Tải/Lưu template") ẩn khi chỉnh sửa plan non-Draft (tránh overwrite trường đã khoá)
- Hide "Thực hiện qua Wizard" / "Thực hiện ngay" khi plan terminal
- Wire lock cho: Entry Info, DCA inputs, Scenario nodes (all fields + add/remove/save-template buttons), Exit Targets, Risk Context, Checklist, Notes
- Risk-override button ẩn khi plan non-Draft

### Tests
- Frontend spec: `trade-plan.component.spec.ts` — 45 tests pass, cover toàn bộ matrix + tighten-SL gate + state banner + edge cases (null `loadedCurrentSl`)

### Docs
- `docs/project-context.md`: ghi nhận quyết định matrix
- `docs/plans/done/p2-trade-plan-editability.md`: plan chi tiết

---

## [v2.38.0] — 2026-04-17 · Trade Plan State Machine + Multi-lot UX

**Branch:** `feat/trade-plan-state-machine-and-ux`

### Domain
- Strict sequential state machine: Draft → Ready → InProgress → Executed → Reviewed
- `MarkReady()` idempotent (gọi trên plan đã Ready không throw)
- `Execute()` yêu cầu plan ở InProgress (trước đây không guard)
- Thêm `Restore()` cho Cancelled → Draft (clear `TradeId`, `TradeIds`, `ExecutedAt`)
- `ExecuteLot()` guard Executed/Reviewed/Cancelled

### Application
- `CreateTradePlanCommand` auto-chain Draft → Ready → InProgress → Executed khi status=Executed
- `UpdateTradePlanStatusCommand` auto-chain khi gọi inprogress/executed từ Draft/Ready
- Thêm case `restore` cho status update
- `KeyNotFoundException` thay vì `Exception` cho plan not found (trả 404 thay 500)

### Api
- `ExceptionMiddleware` map `InvalidOperationException` → 409 Conflict (trước là 500)

### Frontend — Trade Plan
- Fix bug: "Lưu & Sẵn sàng" trên plan đã Ready không trigger updateStatus nữa (tránh 500)
- "Thực hiện ngay" / "Wizard" từ multi-lot plan giờ execute đúng từng lô (không nhảy thẳng Executed)
- Nút xoá chỉ hiện cho Cancelled plans (tránh misclick)
- Thêm nút "Hoàn tác huỷ" cho Cancelled → Draft
- Enum timeHorizon fix: Medium → MediumTerm, Short → ShortTerm, Long → LongTerm
- Bỏ dropdown "Kỳ vọng" trùng lặp — gợi ý kịch bản dùng `plan.timeHorizon`
- Auto-load plan qua `?loadPlan=<id>`
- Panel "Đóng chiến dịch" auto-scroll vào view

### Frontend — Dashboard / Journals / Misc
- Advisory widget chuyển xuống ngay dưới banner cảnh báo rủi ro
- Form nhật ký: dropdown chọn trade theo portfolio thay vì input ID thô
- Route `/symbol-timeline` hỗ trợ cả path param và query param
- Bỏ hint `vndCurrency` thừa dưới input `appNumMask` (trade-create, alerts, capital-flows)

### Shared
- `TIME_HORIZON_OPTIONS` + `DEFAULT_TIME_HORIZON` constants dùng chung cho 3 dropdown
- Thống nhất nhãn theo docs: Ngắn hạn (< 3 tháng) / Trung hạn (3-12 tháng) / Dài hạn (> 1 năm)

### Tests
- 873 tests pass (Domain: 608, Application: 66, Infrastructure: 199)

### Docs
- `docs/trade-plans.md §2.2`: bảng chuyển trạng thái chi tiết, auto-chain logic, multi-lot flow, quy tắc xoá
- `docs/business-domain.md`: bổ sung link tham chiếu state lifecycle

---

## [v2.37.0] — 2026-04-11 · Dynamic Trading Checklist (P6) — Hoàn thành Roadmap TA 6 Phase

**Branch:** `feat/p1-expand-technical-indicators`

### Dynamic Checklist theo Strategy
- Checklist thay đổi theo timeFrame: Scalping (VWAP, Stochastic, Volume), DayTrading (EMA, RSI, MACD, Bollinger), Swing (ADX, Fibonacci, OBV), Position (SMA50/200, ADX weekly, MACD weekly)
- Tự động regenerate khi chọn chiến lược khác

### Multi-Timeframe Gate
- DayTrading: bắt buộc xác nhận xu hướng Daily
- Swing: bắt buộc xác nhận xu hướng Weekly
- Position: bắt buộc xác nhận xu hướng Monthly
- Scalping: không yêu cầu (quá nhanh)

### Weighted Scoring
- Weight 3 (●3 đỏ): bắt buộc — SL, R:R, Multi-TF gate, indicator chính
- Weight 2 (●2 vàng): quan trọng — indicator phụ, position sizing, accept loss
- Weight 1: tham khảo — journal, tâm lý, portfolio risk
- GO threshold: tất cả ●3 items checked + tổng điểm ≥ 70%
- Progress bar trực quan + chi tiết thiếu

### Roadmap hoàn thành ✅
Plan `technical-analysis-features.md` archived → `docs/plans/done/`

### Tests
- 868 tests pass (Domain: 603, Application: 65, Infrastructure: 199, Api: 1)

---

## [v2.36.0] — 2026-04-11 · Strategy Template Library — 7 chiến lược kỹ thuật (P5)

**Branch:** `feat/p1-expand-technical-indicators`

### Strategy Template Enhancement
- 5 fields mới trên StrategyTemplate: `SuggestedSlPercent`, `SuggestedRrRatio`, `SuggestedSlMethod`, `SuggestedAtrMultiplier`, `SuggestedSizingModel`
- 7 chiến lược kỹ thuật cập nhật đầy đủ P5 data:
  - **Scalping**: SL 1.5%, R:R 1.5, Manual SL, Fixed Risk sizing
  - **Day Trading** (mới): ATR×1.5, R:R 2, ATR-Based sizing
  - **Swing Trading**: SL 5%, R:R 2, Support-based SL, ATR-Based sizing
  - **Position Trading** (mới): SL 10%, R:R 3, Chandelier Exit, Turtle sizing
  - **Breakout**: SL 5%, R:R 2, Support-based SL, ATR-Based sizing
  - **Mean Reversion**: SL 5%, R:R 1.5, ATR×1.5, Volatility-Adjusted sizing
  - **Momentum**: SL 8%, R:R 2, MA Trailing, ATR-Based sizing

### Frontend
- Template detail hiển thị badges: R:R, SL%, SL method, sizing model
- Chọn template → tạo Strategy có đầy đủ SL method → Trade Plan auto-fill
- Trade Plan: tự động chọn SL method pill khi chiến lược có `suggestedSlMethod`

### Tests
- 868 tests pass (Domain: 603, Application: 65, Infrastructure: 199, Api: 1)

---

## [v2.35.0] — 2026-04-11 · Advanced Stop Loss & SL Method Selector (P4)

**Branch:** `feat/p1-expand-technical-indicators`

### 5 phương pháp Stop Loss
- **Cố định (nhập tay)**: Nhập SL trực tiếp (có sẵn)
- **ATR Stop Loss**: `Entry ∓ k × ATR(14)`, k = 1.5/2.0/3.0 (ngắn/trung/dài hạn)
- **Chandelier Exit**: `HH(22) - 3×ATR` (mua) / `LL(22) + 3×ATR` (bán)
- **MA Trailing**: EMA(21) làm SL floor
- **Hỗ trợ/Kháng cự gần nhất**: Swing low (mua) / Swing high (bán)

### Backend
- 3 trường mới trong TechnicalAnalysisResult: `Ema21`, `HighestHigh22`, `LowestLow22`
- Tính toán trong TechnicalIndicatorService

### Frontend
- Pill selector dưới ô Stop-Loss, hỗ trợ cả Buy/Sell direction
- ATR multiplier selector (1.5×/2×/3×) với gợi ý ngắn/trung/dài hạn
- SL pills auto-cập nhật khi thay đổi giá vào lệnh
- Auto-fetch technical analysis khi tra cứu mã CP

### Tests
- 868 tests pass (Domain: 603, Application: 65, Infrastructure: 199, Api: 1)
- 9 test mới cho Ema21, HighestHigh22, LowestLow22

---

## [v2.34.0] — 2026-04-11 · Advanced Position Sizing Calculator (P3)

**Branch:** `feat/p1-expand-technical-indicators`

### 5 mô hình Position Sizing
- **Cố định % rủi ro** (có sẵn): `Size = (Vốn × %Risk) / RiskPerShare`
- **Theo ATR**: `Size = (Vốn × %Risk) / (N × ATR)` — tự điều chỉnh theo biến động thị trường
- **Kelly Criterion**: Half-Kelly, cap 25% — sizing tối ưu dựa trên win rate, avg win/loss
- **Turtle (1 unit)**: `1 Unit = 1% Vốn / ATR` — thêm tối đa 3 unit khi lời
- **Điều chỉnh biến động**: Scale Fixed Risk theo ATR% (baseline 2%, clamp 0.5x-1.5x)

### Backend
- Mới: `IPositionSizingService` + `PositionSizingService` (stateless, Singleton)
- API endpoint: `POST /api/v1/risk/position-sizing`

### Frontend
- Bảng so sánh mô hình trong Trade Plan: số CP, % danh mục, trạng thái giới hạn
- Click chọn mô hình → auto-fill số lượng cổ phiếu
- Auto-fetch ATR khi tra cứu mã CP, truyền vào API sizing

### Tests
- 859 tests pass (Domain: 603, Application: 65, Infrastructure: 190, Api: 1)
- 21 test mới cho 5 mô hình sizing + edge cases

---

## [v2.33.0] — 2026-04-11 · Confluence Score, Market Condition, Divergence Detection (P2)

**Branch:** `feat/p1-expand-technical-indicators`

### Confluence Score (Điểm tổng hợp 0-100)
- Trọng số 5 nhóm: Xu hướng 30%, Động lượng 25%, Khối lượng 20%, Biến động 15%, Vị trí giá 10%
- Progress bar trực quan + đánh giá: Tín hiệu tích cực / Tiêu cực / Trung tính

### Market Condition Classifier (Trạng thái thị trường)
- Phân loại tự động dựa trên ADX: Xu hướng rất mạnh (≥40) / Có xu hướng (≥25) / Đi ngang (<25)
- Gợi ý chiến lược phù hợp: Trend Following / Mean Reversion

### Divergence Detection (Phát hiện phân kỳ)
- Auto-detect phân kỳ RSI và MACD vs giá (swing highs/lows)
- Phân kỳ tăng (bullish divergence) + Phân kỳ giảm (bearish divergence)
- Bộ lọc: min 5 bar giữa swing points + min 0.5% chênh lệch giá (giảm false positive)

### Frontend
- 3 card mới trên Smart Signals: Điểm Confluence (gauge + progress bar), Trạng thái thị trường (badge + chiến lược), Phân kỳ (alert card chi tiết RSI/MACD)

### Tests
- 838 tests pass (Domain: 603, Application: 65, Infrastructure: 169, Api: 1)
- 18 test mới cho Confluence Score, Market Condition, Divergence Detection

---

## [v2.32.0] — 2026-04-10 · Help Center — Hướng dẫn sử dụng

**Branch:** `feat/p1-expand-technical-indicators`

### Trang Hướng dẫn sử dụng (`/help`)
- **8 chủ đề**: Bắt đầu, Giao dịch, Kế hoạch GD, Phân tích thị trường, Quản lý rủi ro, Phân tích hiệu suất, Công cụ hỗ trợ, Chiến lược giao dịch
- **Full-text search**: Tìm kiếm toàn văn tiếng Việt, hỗ trợ gõ không dấu (VD: "giao dich" → "Giao dịch")
- **Markdown rendering**: Đọc nội dung từ file `.md` trong `assets/docs/`, render bằng `marked`
- **Highlight kết quả**: Snippet 120 ký tự với match được highlight `<mark>`
- **Navigation**: Nút "Hướng dẫn" trên header + bottom nav mobile

---

## [v2.31.0] — 2026-04-10 · Mở rộng Technical Indicators — Stochastic, ADX, OBV, MFI

**Branch:** `feat/p1-expand-technical-indicators`

### Chỉ báo kỹ thuật mới (4 indicators)
- **Stochastic Oscillator (14,3,3):** Slow Stochastic %K/%D, tín hiệu quá mua (>80) / quá bán (<20)
- **ADX (14) + Directional Indicators:** Đo sức mạnh xu hướng (trending >25 / strong >40 / sideway <20), +DI/-DI xác định hướng
- **OBV (On-Balance Volume):** Theo dõi dòng tiền tích lũy, tín hiệu rising/falling
- **MFI (14) — Money Flow Index:** RSI có volume, quá mua (>80) / quá bán (<20)

### Cải thiện hệ thống tín hiệu
- **Voting system:** Nâng từ 6 lên 10 chỉ báo tham gia bỏ phiếu (EMA, RSI, MACD, Volume, Bollinger, ATR, Stochastic, ADX+DI, OBV, MFI)
- **Signal thresholds:** Điều chỉnh ngưỡng cho 10 indicators (strong_buy ≥6, buy ≥4, sell ≥4, strong_sell ≥6)

### Frontend
- 4 indicator cards mới trong Smart Signals grid: Stochastic, ADX (+DI/-DI), OBV (dòng tiền), MFI
- OBV formatting: Hỗ trợ giá trị âm (e.g., -45M)

### Tests
- 820 tests pass (Domain: 603, Application: 65, Infrastructure: 151, Api: 1)
- 24 test mới cho Stochastic, ADX, OBV, MFI, voting system

---

## [v2.30.0] — 2026-04-10 · Auto-suggest 2 chiều Portfolio ↔ Symbol

**Branch:** `fix/user-feedback-updates`

### Trade Create UX Improvements
- **Auto-suggest danh mục → cổ phiếu:** Chọn danh mục → hiện chips các cổ phiếu đang có vị thế (click chọn nhanh)
- **Auto-suggest cổ phiếu → danh mục:** Chọn/nhập symbol → auto-select danh mục chứa vị thế (nếu duy nhất), highlight "Có vị thế" trong dropdown (nếu nhiều)
- **BÁN — mismatch detection:** Alert banner đỏ nổi bật + disable nút bán khi symbol không có vị thế trong danh mục đã chọn
- **BÁN — smart filtering:** Chips chỉ hiện cổ phiếu có quantity > 0 (bán được)
- **MUA — convenience:** Chips hiện tất cả cổ phiếu trong danh mục (tiện mua thêm), không giới hạn mã mới

### Code Quality
- Fix: impure method call trong `*ngFor` → cache `matchingPortfolioIds` dạng `Set<string>`
- Fix: symbol blur handler — trigger auto-suggest khi user gõ trực tiếp
- Fix: loại bỏ redundant `.toUpperCase()` theo convention `appUppercase` directive

### Tests
- 22 frontend tests (Jasmine/Karma) covering bidirectional auto-suggest logic
- Fix `tsconfig.spec.json` — thêm `polyfills.ts` cho Karma test runner

---

## [v2.29.0] — 2026-04-10 · P0.7 Campaign Review — Đóng chiến dịch & Phân tích hiệu suất

**Branch:** `feat/p7-improvements`

### P0.7 — Campaign Review (đóng chiến dịch với auto-metrics)
- **CampaignReviewService:** Auto-calculate P&L metrics từ trades thực tế (P&L amount, %, VND/ngày, annualized return, target achievement)
- **TimeHorizon:** Dropdown tầm nhìn đầu tư (Ngắn hạn / Trung hạn / Dài hạn) trên TradePlan
- **Review workflow:** Preview metrics → Confirm → Đóng chiến dịch (Executed → Reviewed)
- **Update lessons:** Cập nhật bài học rút ra sau review
- **Pending review:** Danh sách plans Executed chờ review
- **Campaign Analytics page:** `/campaign-analytics` — summary cards, comparison table, best/worst plan, lessons feed
- **API endpoints:**
  - `POST /api/v1/trade-plans/{id}/review` — đóng chiến dịch
  - `GET /api/v1/trade-plans/{id}/review/preview` — xem trước metrics
  - `PATCH /api/v1/trade-plans/{id}/review/lessons` — cập nhật bài học
  - `GET /api/v1/trade-plans/pending-review` — danh sách chờ review
  - `GET /api/v1/trade-plans/campaign-analytics?timeHorizon=ShortTerm` — phân tích cross-plan

### Domain Changes
- **TimeHorizon enum:** ShortTerm / MediumTerm / LongTerm
- **CampaignReviewData value object:** Embedded trong TradePlan
- **MarkReviewed(CampaignReviewData):** Bắt buộc truyền review data khi đóng
- **PlanReviewedEvent:** Domain event mới

### Tests
- 796 tests pass (Domain: 603, Application: 65, Infrastructure: 127)
- 24 new domain tests (TradePlanReviewTests.cs)
- 9 new infrastructure tests (CampaignReviewServiceTests.cs)

---

## [v2.28.0] — 2026-04-10 · P0 Phase 4 — Scenario Consultant & Advisory System

**Branch:** `feat/p0-phase4-advisory`

### P0.6 — Scenario Consultant (gợi ý kịch bản có cơ sở kỹ thuật)
- **ScenarioConsultantService:** Phân tích kỹ thuật → gợi ý kịch bản chốt lời, cắt lỗ, mua thêm, sideway
- **Confluence scoring:** Vùng có ≥ 2 indicator hội tụ (S/R + Fibonacci + EMA + Bollinger) → ưu tiên cao hơn
- **Tầm nhìn đầu tư:** Dropdown Ngắn hạn / Trung hạn / Dài hạn — tự động fill mốc thời gian
- **Preview + chọn lọc:** Xem gợi ý kèm reasoning, checkbox từng node, nút "Áp dụng gợi ý" / "Tạo kế hoạch từ gợi ý"
- **API:** `GET /api/v1/trade-plans/scenario-suggestion?symbol=HPG&entryPrice=75000&timeHorizon=Medium`

### P0.5 — Gợi ý hành động theo vùng giá
- **ScenarioAdvisoryService:** Quét giá hiện tại vs kịch bản active → gợi ý hành động on-demand
- **Dashboard widget:** "Gợi ý hành động" hiển thị khi giá vào vùng trigger
- **Wording advisory:** "Xem xét bán 30%", "Xem xét cắt lỗ" — không dùng "Đã..." hay "Cần phải..."
- **API:** `GET /api/v1/trade-plans/advisories`

### Code Review Fixes
- Input validation trên endpoint (symbol + entryPrice)
- UserId scoping cho scenario-suggestion
- N+1 → batch parallel fetch giá (deduplicate symbols)
- Category mismatch backend/frontend (AddPosition)
- Nullable RSI explicit check
- CancellationToken propagation
- trackBy cho ngFor suggestion list

### Tests
- 768 tests pass (Domain: 584, Application: 65, Infrastructure: 118, Api: 1)

---

## [v2.27.0] — 2026-04-10 · P0 Phase 2+3 — Flowchart, Fibonacci, Candlestick Chart

**Branch:** `feat/p0-phase2-3-improvements`

### P0.3 — Visual Flowchart Tree UI
- **Connector lines:** CSS-only vertical/horizontal connectors giữa parent → children
- **Status colors:** Xanh (Đã kích hoạt), Vàng (Chờ), Xám (Bỏ qua)
- **Collapsible branches:** Thu gọn/mở rộng nhánh kịch bản con

### P0.6a — Fibonacci Retracement/Extension + EMA200
- **Fibonacci levels:** 23.6%, 38.2%, 50%, 61.8%, 78.6% retracement + 127.2%, 161.8% extension
- **EMA200:** Thêm EMA 200 phiên cho phân tích trung/dài hạn
- **Auto-detect swing points:** Sử dụng lại logic support/resistance hiện có

### P0.6c — Mở rộng Price History
- **Default 12 tháng** (thay vì 6 tháng) — đủ cho EMA200 (~200 phiên)
- **Tham số `months`** cho phép tùy chỉnh khoảng thời gian phân tích

### P0.6b — Candlestick Chart + Overlays
- **Candlestick:** Thay line chart bằng nến Nhật (OHLC) — xanh/đỏ
- **EMA overlays:** EMA20 (xanh), EMA50 (cam), EMA200 (tím) — đường ngang
- **S/R overlays:** Hỗ trợ (xanh nét đứt), Kháng cự (đỏ nét đứt)
- **Fibonacci overlays:** Các mức Fib màu vàng amber trên biểu đồ
- **Toggle toolbar:** 4 nút bật/tắt: Nến, EMA, S/R, Fibonacci

### Tests
- 755 tests pass (Domain: 584, Application: 65, Infrastructure: 105, Api: 1)

---

## [v2.26.0] — 2026-04-10 · P0 Phase 1 — Scenario Playbook Improvements

**Branch:** `feat/p0-phase1-improvements`

### P0.1 — Scenario History & Status Dashboard
- **Lịch sử kích hoạt:** Hiển thị trạng thái từng node (Đã kích hoạt / Chờ / Bỏ qua) với thời gian + giá
- **Timeline panel:** Bên dưới tree editor khi plan đang InProgress
- **API:** `GET /api/v1/trade-plans/{id}/scenario-history`

### P0.2 — User Custom Templates (Save/Load)
- **Lưu mẫu kịch bản:** Nút "Lưu mẫu kịch bản" tạo template tùy chỉnh từ tree hiện tại
- **Dropdown phân loại:** Mẫu hệ thống (3 preset) | Mẫu của tôi (user templates)
- **Xoá mẫu:** Nút xoá kèm xác nhận
- **API:** `POST /DELETE /api/v1/trade-plans/scenario-templates`

### P0.4 — ATR Trailing Stop thực tế
- **Fix placeholder:** Thay `entryPrice × 0.02` bằng ATR(14) thực tế từ `TechnicalIndicatorService`
- **Fallback:** Giữ proxy cũ khi thiếu dữ liệu ATR + log warning
- **Lazy fetch:** Chỉ gọi `AnalyzeAsync` khi gặp ATR trailing stop node, cache kết quả

### Code Review Fixes
- Fix Enum.Parse → TryParse cho input validation (trả 400 thay vì 500)
- Fix sync index creation → async trong ScenarioTemplateRepository
- Fix thiếu try/catch 404 cho DELETE endpoint
- Fix alert-node matching: dùng TriggeredAt timestamp thay vì label string
- Thêm confirm dialog khi xoá mẫu kịch bản

### Tests
- 747 tests pass (Domain: 584, Application: 65, Infrastructure: 97, Api: 1)

---

## [v2.25.1] — 2026-04-09 · P7 Bugfix & Chart UX Polish

**Branch:** `feat/p7-improvements`

### Vietstock Crawl Fix
- **Fix CSRF token parsing:** Regex fallback 3 tầng cho unquoted HTML attributes
- **Fix 403:** Thêm User-Agent, CookieContainer, Referer, X-Requested-With headers
- **Fix URL bài viết:** Dùng `vietstock.vn` thay vì `finance.vietstock.vn`

### Chart UX Improvements
- **Sắp xếp timeline newest-first** (mới nhất lên đầu)
- **Mở rộng chart đến ngày hiện tại** sử dụng giá real-time
- **Thay emoji markers bằng ký tự ngắn** (T/J/E/A) kèm số lượng
- **Crosshair tooltip:** Hiển thị chi tiết sự kiện khi hover
- **Sanitize tooltip innerHTML** chống XSS từ dữ liệu API
- **Refactor nested subscribe → switchMap** (RxJS best practice)

### Tests
- 732 tests pass (Domain: 584, Application: 54, Infrastructure: 94)

---

## [v2.25.0] — 2026-03-27 · P7 Symbol Timeline Improvements

**Branch:** `feat/p7-improvements`

### P7.1: Emotion ↔ P&L Correlation
- **Correlation cảm xúc → kết quả GD:** Tính trung bình P&L %, win rate cho mỗi cảm xúc
- **Insight text:** Highlight cảm xúc tốt nhất/tệ nhất với win rate và P&L TB

### P7.2: Confidence Calibration
- **Hiệu chuẩn mức tự tin:** So sánh confidence level ranges (Low/Med/High/Very High) với win rate thực tế
- **Calibration widget:** Thanh ngang với trạng thái Phù hợp/Quá tự tin/Chưa tự tin

### P7.3: Behavioral Pattern Detection
- **4 patterns:** FOMO Entry, Panic Sell, Revenge Trading, Overtrading
- **Pattern alerts panel:** Cards severity (Critical/Warning) + mô tả + ngày
- **IBehavioralAnalysisService** tích hợp vào timeline response

### P7.4: Chart UX Enhancements
- **Chuyển sang LineSeries:** Thay CandlestickSeries bằng LineSeries (match thực tế hiển thị)

### P7.5: Dedicated AI Timeline Review
- **Rich AI context:** Gồm correlation, calibration, behavioral patterns, full journal/trade history
- **Prompt template** chuyên biệt cho trading psychology coach

### P7.6: Emotion Trend Over Time
- **Xu hướng cảm xúc theo tháng:** Stacked bar chart, dominant emotion, average confidence
- **Trend insight:** So sánh tháng gần nhất vs tháng trước

### P7.7: Export Timeline
- **Xuất CSV:** Tải file CSV với tất cả timeline items (Ngày, Loại, Tiêu đề, Cảm xúc...)
- **Sao chép tóm tắt:** Copy text summary vào clipboard

### P7.8: Vietstock Event Crawl
- **Auto-crawl tin tức + sự kiện DN** từ Vietstock API (GetNews + EventsTypeData)
- **CSRF token flow**, `/Date(ms)/` parser, ChannelID → MarketEventType mapping
- **Dedup:** Bỏ qua events trùng (Symbol + Title + Date)
- **Nút "Cập nhật tin tức"** trên Symbol Timeline page
- **API:** `POST /api/v1/market-events/crawl`

---

## [v2.24.0] — 2026-03-27 · P1-P4 Improvements

**Branch:** `feat/p1-post-trade-review`

### P1: Post-Trade Review Workflow

- **Pending review query**: Lấy SELL trades chưa có JournalEntry PostTrade
- **Dashboard widget "Chờ đánh giá"**: Hiện SELL trades chưa review, click → Symbol Timeline
- **Trades list cột "Nhật ký"**: Icon check (đã review) / pencil (chưa review) cho mỗi SELL trade
- Endpoint: `GET /api/v1/journal-entries/pending-review`

### P2: Stress Test — Dynamic Beta

- **Dynamic beta**: Lấy beta từ API, fallback tính từ correlation VN-INDEX, fallback cuối 1.0
- Thay thế `estimatedBetas` hardcoded (~20 mã) bằng API call
- Endpoint: `POST /api/v1/risk/portfolio/{id}/stress-test`

### P3: Technical Indicators — Bollinger Bands + ATR

- **Bollinger Bands(20, 2)**: Upper, middle (SMA20), lower, bandwidth, %B, signal (squeeze/breakout)
- **ATR(14)**: Giá trị ATR, ATR% (% giá hiện tại)
- Signal scoring mở rộng: 6 indicators (thêm Bollinger + ATR)
- 2 indicator cards mới trong market-data component

### P4: Risk Budgeting — Daily Trade Limits

- **RiskProfile mở rộng**: `MaxDailyTrades`, `DailyLossLimitPercent`
- **Risk budget card**: "Ngân sách rủi ro hôm nay" — trades/limit, P&L, trạng thái khóa
- **Risk profile form**: 2 fields mới (số lệnh tối đa/ngày, giới hạn lỗ/ngày)
- `ITradeRepository.GetByPortfolioIdAndDateRangeAsync` — filter trades theo ngày
- Endpoint: `GET /api/v1/risk/portfolio/{id}/budget`

### Tests

- 702 tests pass (Domain: 584, Application: 39, Infrastructure: 78, Api: 1)
- P1: 5 test cases cho GetTradesPendingReviewQueryHandler
- P2: 5 test cases cho CalculateStressTestAsync
- P3: 8 test cases cho Bollinger Bands + ATR
- P4: 10 test cases cho RiskProfile entity + CheckRiskBudget

---

## [v2.23.0] — 2026-03-27 · Symbol Timeline (P7)

**Branch:** `feat/p7-symbol-timeline`

### Thêm mới

- **Symbol Timeline**: Trang dòng thời gian cho mỗi mã CK — biểu đồ nến + nhật ký + giao dịch + sự kiện trên cùng 1 timeline
- **JournalEntry (standalone)**: Ghi nhật ký bất kỳ lúc nào gắn với symbol — không cần có giao dịch (5 loại: Quan sát / Trước GD / Đang GD / Sau GD / Tổng kết)
- **MarketEvent**: Thêm sự kiện thị trường (KQKD, cổ tức, tin tức, vĩ mô...) hiển thị trên biểu đồ
- **Candlestick chart**: Biểu đồ nến với lightweight-charts, markers cho nhật ký/giao dịch/sự kiện/cảnh báo
- **Emotion Ribbon**: Sub-chart cảm xúc bên dưới biểu đồ nến — màu theo cảm xúc, độ cao theo mức tự tin
- **Emotion Summary**: Phân tích phân bố cảm xúc, tự tin trung bình, cảm xúc chính
- **AI Timeline Review**: AI phân tích pattern cảm xúc ↔ giao dịch ↔ kết quả
- **Unified Timeline API**: Gom nhật ký + giao dịch + sự kiện + cảnh báo, tính holding periods + emotion summary
- **Quick-add forms**: Ghi nhật ký và thêm sự kiện inline trên trang timeline
- **Timeline links**: Nút 📊 trên Watchlist, Positions, Trades → navigate đến Symbol Timeline

### Backend

- Entity: `JournalEntry` (Domain) — 5 loại, cảm xúc, snapshot giá, tags, rating
- Entity: `MarketEvent` (Domain) — 7 loại sự kiện thị trường
- Repository: `JournalEntryRepository`, `MarketEventRepository` (MongoDB)
- CQRS: Create/Update/Delete JournalEntry, GetBySymbol, GetSymbolTimeline
- CQRS: CreateMarketEvent, GetMarketEvents
- API: `/api/v1/journal-entries`, `/api/v1/symbols/{symbol}/timeline`, `/api/v1/market-events`

### Frontend

- Component: `SymbolTimelineComponent` (`/symbol-timeline/:symbol`)
- Services: `JournalEntryService`, `MarketEventService`
- Dependency: `lightweight-charts` v4.2.2

### Cải thiện (Code Review)

- Fix memory leak: ResizeObserver disconnect khi destroy component
- Fix race condition: takeUntil cleanup cho tất cả HTTP subscriptions
- Fix bảo mật: thêm `rel="noopener noreferrer"` cho link ngoài
- Fix hiệu năng: gộp N+1 trade query → 1 query `GetByUserPortfoliosAndSymbolAsync`
- Fix hiệu năng: alert history filter tại DB thay vì load toàn bộ vào memory
- Fix logic: BUY đầu tiên giờ xuất hiện trong HoldingPeriod.Changes
- Fix type: `decimal` thay `int` cho Quantity trong holding period DTOs
- Fix casing: normalize PascalCase → camelCase 1 lần khi nhận data, xóa 25+ fallback patterns
- Thêm soft delete + UpdatedAt cho MarketEvent entity
- Thêm validation null cho `symbol` query param (trả 400 thay vì 500)
- Tách SymbolTimelineController ra file riêng

### Tests

- Domain: 47 tests (JournalEntry: 30, MarketEvent: 17)
- Application: 9 tests (CreateJournalEntryCommandHandler: 5, GetSymbolTimelineQueryHandler: 4)

---

## [v2.22.0] — 2026-03-27 · Scenario Playbook

**Branch:** `feat/capital-flows-visibility`

### Thêm mới

- **Scenario Playbook**: Chế độ nâng cao cho Trade Plan — cây quyết định (decision tree) với điều kiện + hành động liên kết
- **2 chế độ thoát lệnh**: Toggle Cơ bản (exit targets cũ) / Nâng cao (scenario tree) — backward compatible
- **5 loại điều kiện**: Giá >=, Giá <=, Thay đổi %, Chạm trailing stop, Sau N ngày
- **7 loại hành động**: Bán %, Bán tất cả, Dời SL, SL về hòa vốn, Bật trailing stop, Thêm vị thế, Thông báo
- **Trailing Stop chi tiết**: 3 phương pháp (%, ATR ước tính, Cố định VNĐ) + giá kích hoạt + bước tối thiểu
- **3 mẫu kịch bản**: An toàn, Cân bằng, Tích cực — áp dụng 1 click
- **Tự động đánh giá**: Worker mỗi 15 phút evaluate scenarios + tạo AlertHistory thông báo

### Backend

- `TradePlan.cs` — thêm ExitStrategyMode, ScenarioNodes, TrailingStopConfig + 3 domain methods + ScenarioNodeTriggeredEvent
- `ScenarioEvaluationService.cs` — tự động evaluate conditions, update trailing stops, tạo alert
- `TradePlanRepository.cs` — thêm `GetAdvancedInProgressAsync` (filtered tại MongoDB)
- `TradePlansController.cs` — 2 endpoints mới: trigger scenario node, get preset templates
- `Worker.cs` — thêm `EvaluateScenarioPlaybooksAsync`

### Frontend

- `trade-plan.component.ts` — toggle Cơ bản/Nâng cao, scenario tree editor (recursive ng-template), preset selector, trailing stop config inline
- `trade-plan.service.ts` — thêm interfaces ScenarioNodeDto, TrailingStopConfigDto, ScenarioPreset + 2 API methods

### Tests

- 33 tests mới: Domain (20) + Application (3) + Infrastructure (10)

---

## [v2.21.0] — 2026-03-26 · Capital Flows Visibility

**Branch:** `feat/capital-flows-visibility`

### Thêm mới

- **Dashboard — Tiền mặt khả dụng**: Card mới hiển thị cash balance (Vốn ban đầu + Dòng vốn ròng - Đã đầu tư), link đến `/capital-flows`
- **Dashboard — TWR dưới Lãi/Lỗ**: Hiển thị Time-Weighted Return % bên dưới card Tổng Lãi/Lỗ, cho thấy hiệu suất chiến lược thực sự
- **Analytics — TWR vs MWR card**: So sánh TWR (kỹ năng đầu tư) với MWR (lợi nhuận thực tế), giải thích tự động khi TWR ≠ MWR (timing nạp/rút tiền)
- **Equity Curve — Flow markers**: Điểm tam giác xanh ▲ (nạp tiền/cổ tức) và đỏ ▼ (rút tiền/phí) overlay trên biểu đồ equity curve ở cả Dashboard và Analytics
- **Smart Nudge**: Banner gợi ý ghi nhận dòng vốn khi phát hiện giá trị danh mục thay đổi >20% mà không có giao dịch tương ứng

### Frontend

- `dashboard.component.ts` — thêm cash balance card, TWR, flow markers trên mini equity chart, smart nudge banner; inject `CapitalFlowService`
- `analytics.component.ts` — thêm TWR/MWR comparison card, flow markers trên equity curve chart; inject `CapitalFlowService`

---

## [v2.20.0] — 2026-03-25 · Portfolio Optimizer & Risk Dashboard Improvements

**Branch:** `feat/portfolio-optimizer-risk-dashboard`

### Thêm mới

- **Portfolio Optimizer** — phân tích tối ưu hóa danh mục trên trang `/risk-dashboard`:
  - **Cảnh báo tập trung**: cảnh báo khi vị thế vượt giới hạn MaxPositionSizePercent (warning/danger)
  - **Phân bổ theo ngành**: nhóm vị thế theo ngành từ `IFundamentalDataProvider`, cảnh báo khi vượt MaxSectorExposurePercent
  - **Cặp tương quan cao**: cảnh báo cặp CP tương quan >0.5 (medium) / >0.7 (high)
  - **Điểm đa dạng hóa**: score 0-100 dựa trên concentration, sector, correlation, số vị thế
  - **Gợi ý tối ưu**: khuyến nghị giảm tỷ trọng, đa dạng hóa ngành
- **Trailing Stop Monitoring** — giám sát trailing stop real-time trên `/risk-dashboard`:
  - Cảnh báo theo severity: danger (≤2%), warning (≤5%), safe (>5%)
  - Gợi ý nâng trailing stop khi giá tăng cao hơn mức cũ
- **PositionRiskItem mở rộng** — thêm `sector`, `beta`, `positionVaR` cho từng vị thế

### Backend

- `GetPortfolioOptimizationQuery` + handler (CQRS) — phân tích tối ưu hóa danh mục
- `GetTrailingStopAlertsQuery` + handler (CQRS) — cảnh báo trailing stop
- `RiskCalculationService` — thêm `GetPortfolioOptimizationAsync()`, `GetTrailingStopAlertsAsync()`; inject thêm `IRiskProfileRepository`, `IFundamentalDataProvider`
- API mới: `GET /api/v1/risk/portfolio/{id}/optimization`, `GET /api/v1/risk/portfolio/{id}/trailing-stop-alerts`

### Frontend

- `RiskService` — 7 interfaces mới + 2 methods (`getPortfolioOptimization`, `getTrailingStopAlerts`)
- `RiskDashboardComponent` — 2 sections mới: Tối ưu hóa danh mục + Giám sát Trailing Stop

### Tests

- 8 application handler tests (optimization + trailing stop queries)
- 13 infrastructure service tests (concentration, sector, correlation, diversification score, trailing stop alerts)

---

## [v2.19.0] — 2026-03-25 · Progressive Web App (PWA)

**Branch:** `feat/pwa`

### Thêm mới

- **PWA support** — cài đặt ứng dụng lên màn hình chính trên mobile/desktop
  - `manifest.webmanifest` — app metadata, icons, shortcuts (Dashboard, Danh mục)
  - `@angular/service-worker` — service worker caching với ngsw
  - **Offline caching** — shell app cache tự động; API cache theo nhóm:
    - Market data: 15 giây (freshness)
    - Portfolio/Positions/PnL: 1 phút (freshness)
    - Analytics/Risk/Snapshots: 5 phút (performance)
    - Watchlist/Strategies/Journals: 2 phút (freshness)
  - **Tự động cập nhật** — banner thông báo khi có phiên bản mới
  - **Banner cài đặt** — gợi ý cài đặt ứng dụng (có thể bỏ qua, nhớ lựa chọn)
  - **App icons** — SVG icons cho tất cả kích thước (72→512px)
  - **Meta tags** — theme-color, apple-mobile-web-app, viewport-fit=cover

### Frontend

- `PwaService` — quản lý install prompt, lắng nghe SW update events
- `PwaInstallBannerComponent` — banner cài đặt + banner cập nhật
- `app.component.ts` — thêm `PwaInstallBannerComponent`
- `main.ts` — `provideServiceWorker` (chỉ bật ở production/staging)
- `angular.json` — `serviceWorker: ngsw-config.json` cho production + staging

---

## [v2.19.0] — 2026-03-24 · Comprehensive Stock Analysis (12th AI Use Case)

**Branch:** `feature/comprehensive-stock-analysis`

### Thêm mới

- **AI Comprehensive Stock Analysis (use case #12)**: Phân tích toàn diện cổ phiếu kết hợp đa nguồn dữ liệu từ 24hmoney — chỉ số tài chính, báo cáo tài chính, kế hoạch kinh doanh, cổ tức, cổ phiếu cùng ngành, giao dịch nước ngoài, báo cáo phân tích từ CTCK
  - Nút "🤖 AI Phân tích Toàn diện" trên trang `/market-data`
  - Endpoint: `POST /api/v1/ai/comprehensive-analysis` (SSE streaming)

### Backend

- `IComprehensiveStockDataProvider` interface (Application layer) — định nghĩa contract cho dữ liệu phân tích toàn diện
- `HmoneyComprehensiveDataProvider` (Infrastructure/Services/Hmoney/) — tích hợp 8 endpoint 24hmoney:
  - `/v2/ios/companies/index` — chỉ số tài chính: P/E, P/B, ROE, ROA, EPS, Beta, MarketCap
  - `/api/v2/web/company/detail` — thông tin chi tiết công ty
  - `/api/v2/web/company/financial-report` — báo cáo tài chính (BCTC)
  - `/api/v2/web/company/plan` — kế hoạch kinh doanh
  - `/api/v2/web/announcement/dividend-events` — sự kiện cổ tức
  - `/api/v2/web/stock-recommend/get_stock_related_bussiness` — cổ phiếu cùng ngành
  - `/api/v2/web/stock/foreign-trading-series` — chuỗi giao dịch nước ngoài
  - `/api/v2/web/announcement/report-analytics` — báo cáo phân tích từ CTCK
- `HmoneyComprehensiveApiModels.cs` — response DTOs cho các endpoint trên
- `AiAssistantService`: thêm context builder + streaming method cho comprehensive-analysis (nâng tổng lên 12 use cases)
- `AiController`: thêm endpoint `POST /ai/comprehensive-analysis` (SSE)

---

## [v2.18.0] — 2026-03-21 · Enhance AI Prompts & Deep Integration (11 Use Cases)

**Branch:** `feature/enhance-ai-prompts`

### Thêm mới

- **5 AI use case mới** — tổng cộng 11 use case, tích hợp sâu vào mọi trang chính:
  - **AI Risk Assessment** (`/risk-dashboard`): Phân tích sức khỏe rủi ro — health score 0-100, vi phạm giới hạn, correlation risk, drawdown, 3 hành động giảm rủi ro cụ thể
  - **AI Position Advisor** (`/positions`): Tư vấn vị thế — vị thế nguy hiểm, cơ hội chốt lời, kế hoạch bị thiếu, hành động ưu tiên
  - **AI Trade Analysis** (`/trades`): Phân tích giao dịch — win rate & expectancy, hiệu suất theo mã, kỷ luật theo kế hoạch, pattern hành vi
  - **AI Watchlist Scanner** (`/watchlist`): Quét watchlist — cơ hội mua gần giá mục tiêu, tín hiệu kỹ thuật, xếp hạng ưu tiên, action plan top 3
  - **AI Daily Briefing** (`/dashboard`): Bản tin hôm nay — tóm tắt buổi sáng, hành động khẩn cấp, cơ hội hôm nay, cảnh báo rủi ro, checklist

### Cải tiến

- **Enriched prompts cho 6 use case hiện có** — cross-reference data giữa các domain:
  - **Trade Plan Advisor**: + market data real-time, technical signals (RSI/MACD/EMA/S&R), risk compliance, historical trades trên cùng mã
  - **Portfolio Review**: + risk profile, risk summary, active trade plans count
  - **Monthly Summary**: + performance metrics (win/loss/win rate/realized P&L), so sánh tháng trước, per-symbol P&L
  - **Journal Review**: + thống kê journal (avg confidence, avg rating, emotion distribution), portfolio context, tăng từ 5→10 entries
  - **Chat Assistant**: + active positions (top 5), watchlist summary, current date
  - **Stock Evaluation**: + user position nếu đang nắm giữ, watchlist target prices, active trade plan

### Backend

- `AiAssistantService`: thêm 3 dependencies (`IRiskCalculationService`, `IRiskProfileRepository`, `IWatchlistRepository`), 5 context builders mới, 5 streaming methods mới, enhance 6 builders hiện có
- `IAiAssistantService`: 5 method signatures mới + `watchlistId` parameter cho `BuildContextAsync`
- `AiController`: 5 endpoints mới (risk-assessment, position-advisor, trade-analysis, watchlist-scanner, daily-briefing) + 5 Request DTOs

### Frontend

- `AiService`: 5 stream methods mới (`streamRiskAssessment`, `streamPositionAdvisor`, `streamTradeAnalysis`, `streamWatchlistScanner`, `streamDailyBriefing`)
- `AiChatPanelComponent`: 5 cases mới trong `getStream()` switch
- Tích hợp `AiChatPanelComponent` vào 5 trang: risk-dashboard, positions, trades, watchlist, dashboard — mỗi trang có nút AI và sliding panel

---

## [v2.17.0] — 2026-03-21 · AI Đánh giá Nhanh Mã + Copy Prompt + XML Tagging

**Branch:** `feature/ai-context-copy`

### Thêm mới

- **AI Đánh giá Nhanh Mã (use case #6)**: Đánh giá toàn diện cổ phiếu kết hợp phân tích cơ bản (P/E, EPS, ROE, D/E) + kỹ thuật (EMA/RSI/MACD/S&R)
  - Nút "✨ AI Đánh giá" trên trang `/market-data` (cạnh "Tạo Trade Plan từ gợi ý")
  - Tích hợp **TCBS API** (`apipubaws.tcbs.com.vn`) cho dữ liệu fundamental: P/E, P/B, EPS, ROE, ROA, Nợ/Vốn, tăng trưởng doanh thu & lợi nhuận, vốn hóa, cổ tức, SHNN
  - Interface `IFundamentalDataProvider` + `TcbsFundamentalDataProvider` (cache 5 phút)
- **Copy Prompt to Clipboard**: Nút 📋 trong AI panel header → tạo prompt hoàn chỉnh (system prompt + user message + XML-tagged data) → copy vào clipboard
  - Dùng với Claude Max / Gemini client app bên ngoài, **không cần API key**
  - Endpoint: `POST /api/v1/ai/build-context` → JSON (không SSE)
  - Hoạt động cho tất cả 6 use cases

### Cải tiến

- **XML Tagging cho tất cả prompt**: Áp dụng XML tags (`<portfolio>`, `<positions>`, `<fundamental_metrics>`, `<technical_signals>`, `<trade_plan>`, `<trade_journals>`, etc.) + markdown tables → AI parse dữ liệu chính xác hơn
- **Refactor `AiAssistantService`**: Tách thành private context builders cho mỗi use case, dùng chung cho cả streaming lẫn copy-prompt
- **Model selector trong AI panel**: Dropdown chọn model (Sonnet/Opus/Gemini) trực tiếp trong header chat panel

### Backend

- `IFundamentalDataProvider` interface + `StockFundamentalData` DTO (Application layer)
- `TcbsFundamentalDataProvider` — TCBS API integration, `TcbsApiModels` response DTOs
- `AiContextResult` DTO — `{ SystemPrompt, UserMessage, ErrorMessage }`
- `AiAssistantService` refactored: 6 private `BuildXxxContext()` methods + public `BuildContextAsync` dispatcher + `EvaluateStockAsync` streaming
- `AiController`: thêm `POST /ai/stock-evaluation` (SSE) + `POST /ai/build-context` (JSON)
- DI: register `TcbsFundamentalDataProvider` với HttpClient

### Frontend

- `AiService`: thêm `streamStockEvaluation()`, `buildContext()`
- `AiChatPanelComponent`: nút 📋 Copy Prompt, `stock-evaluation` case
- `MarketDataComponent`: import `AiChatPanelComponent`, nút "✨ AI Đánh giá", `isAiOpen` state

---

## [v2.16.0] — 2026-03-20 · Thêm Google Gemini — Hỗ trợ đa nhà cung cấp AI

**Branch:** `feature/ai-integration`

### Thêm mới

- **Google Gemini (nhà cung cấp AI thứ 2)**: Hỗ trợ đa nhà cung cấp AI — Claude (Anthropic) + Gemini (Google) trong cùng hệ thống
  - **Provider tabs**: Chuyển đổi giữa Claude / Gemini trên trang `/ai-settings`
  - **Dual API key**: Lưu trữ API key riêng cho từng provider (mã hóa, BsonElement backward compat)
  - **Gemini models**: `gemini-2.0-flash`, `gemini-2.5-flash`, `gemini-2.5-pro`
  - **Factory pattern**: `IAiChatServiceFactory` resolve đúng service theo provider (`ClaudeApiService` | `GeminiApiService`)

### Backend

- `AiSettings` entity: thêm `Provider` ("claude" | "gemini"), đổi tên `EncryptedApiKey` → `EncryptedClaudeApiKey` (BsonElement backward compat), thêm `EncryptedGeminiApiKey` (nullable)
- `AiSettings` methods mới: `UpdateProvider()`, `UpdateClaudeApiKey()`, `UpdateGeminiApiKey()`, `GetActiveEncryptedApiKey()`
- `GeminiApiService` — gọi Google Gemini streaming API, role mapping "assistant" → "model", SSE format
- `IAiChatServiceFactory` + `AiChatServiceFactory` — factory pattern resolve đúng provider
- DI: `AddHttpClient` riêng cho từng provider (Anthropic + Google), factory registration
- Chi phí token tính theo provider

### Frontend

- Provider tabs UI trên `/ai-settings`: chuyển đổi Claude / Gemini, nhập API key riêng, model dropdown theo provider
- `AiService` cập nhật: hỗ trợ provider field trong settings CRUD

---

## [v2.15.0] — 2026-03-20 · Tích hợp AI Claude

**Branch:** `feature/ai-integration`

### Thêm mới

- **Trợ lý AI Claude**: Tích hợp 5 use case AI streaming (SSE) vào ứng dụng
  - **AI Journal Review**: Phân tích nhật ký giao dịch — nhận diện tâm lý (FOMO, revenge trading), đánh giá kỷ luật, gợi ý cải thiện
  - **AI Portfolio Review**: Đánh giá danh mục — đa dạng hóa, hiệu suất, rủi ro, gợi ý cân bằng
  - **AI Trade Plan Advisor**: Tư vấn kế hoạch giao dịch — chấm điểm entry/SL/TP, position sizing, R:R
  - **AI Chat Assistant**: Trợ lý tổng hợp — chiến lược, phân tích kỹ thuật, quản lý rủi ro (nút AI trên header)
  - **AI Monthly Summary**: Tổng kết hiệu suất tháng — giao dịch nổi bật, pattern, gợi ý tháng tới
- **Trang `/ai-settings`**: Cấu hình AI — nhập API key Anthropic (mã hóa), chọn model (Sonnet/Opus), test kết nối, xem thống kê sử dụng (tokens + chi phí USD)
- **AI Chat Panel**: Component tái sử dụng — sliding panel từ phải, markdown rendering, follow-up questions, token usage display

### Backend

- `AiSettings` entity (Domain) — lưu API key mã hóa, model, token usage per user
- `AiKeyEncryptionService` — mã hóa API key bằng ASP.NET Data Protection
- `ClaudeApiService` — gọi Anthropic Messages API với streaming SSE
- `AiAssistantService` — orchestrate 5 use cases: gather context, build Vietnamese system prompts, track usage
- `AiSettingsController` (`api/v1/ai-settings`) — GET/PUT/DELETE + test connection
- `AiController` (`api/v1/ai`) — 5 SSE streaming endpoints

### Frontend

- `AiService` — CRUD settings (HttpClient) + streaming (fetch + ReadableStream → Observable)
- `AiChatPanelComponent` — reusable sliding panel, markdown (marked), auto-start + follow-up
- `AiSettingsComponent` — settings page: API key, model select, usage stats, danger zone
- Integration: nút AI trên journals, portfolio-detail, trade-plan, monthly-review, header

---

## [v2.14.0] — 2026-03-20 · Smart Trade Signals

**Branch:** `feature/smart-signals`

### Thêm mới

- **Phân tích kỹ thuật tự động**: Tra cứu cổ phiếu → tự động chạy phân tích EMA(20/50), RSI(14), MACD(12,26,9), Volume ratio, hỗ trợ/kháng cự
- **Tín hiệu tổng hợp**: Mua mạnh / Mua / Chờ / Bán / Bán mạnh — dựa trên 4 chỉ báo kỹ thuật
- **Gợi ý giao dịch**: Entry (hỗ trợ gần nhất), Stop-loss, Target (kháng cự gần nhất), Risk:Reward ratio
- **"Tạo Trade Plan từ gợi ý"**: 1 click tạo Trade Plan từ kết quả phân tích kỹ thuật (pre-fill entry/SL/TP)
- **Watchlist signal column**: Tín hiệu kỹ thuật hiển thị trên bảng watchlist (top 10 mã)

### Backend

- `ITechnicalIndicatorService` + `TechnicalIndicatorService` — engine phân tích kỹ thuật
- `GetTechnicalAnalysisQuery` — CQRS query via MediatR
- API endpoint: `GET /api/v1/market/stock/{symbol}/analysis`
- Indicators: EMA, RSI (Wilder's smoothed), MACD with crossover, Volume ratio, Swing High/Low (5-window), Level clustering (2%)

### Frontend

- `TechnicalAnalysis` interface + `getTechnicalAnalysis()` method in `MarketDataService`
- Analysis UI section in `MarketDataComponent`: indicators grid, S&R levels, trade suggestion card
- Signal column in `WatchlistComponent` (desktop table + mobile cards)

---

## [v2.13.0] — 2026-03-20 · Watchlist Thông minh

**Branch:** `feature/watchlist`

### Thêm mới

- **Trang `/watchlist`**: Theo dõi cổ phiếu quan tâm — bảng giá live, ghi chú, giá mục tiêu mua/bán, deep link đến Trade Plan
- **CRUD Watchlist**: Tạo/sửa/xoá nhiều danh sách với emoji tuỳ chỉnh
- **Import VN30**: Nhập 30 mã VN30 bằng 1 click
- **Symbol autocomplete**: Tìm kiếm mã qua 24hmoney API (debounced)
- **Dashboard widget**: Top 5 mã từ watchlist hiển thị ngay trên Tổng quan
- **Navigation**: Header (Phân tích group) + Bottom nav (moreItems)

### Backend

- Domain entities: `Watchlist` (AggregateRoot), `WatchlistItem` (ValueObject embedded)
- API: `WatchlistsController` (`api/v1/watchlists`) — 9 endpoints
- MongoDB: `watchlists` (compound index UserId)
- CQRS: 7 commands + 2 queries

### Frontend

- `WatchlistService` — 9 API methods
- `WatchlistComponent` — standalone, inline template, Tailwind CSS
- Dashboard widget tích hợp trong `DashboardComponent`
- Responsive: desktop table + mobile cards

---

## [v2.12.0] — 2026-03-18 · Trade Plan UX — Tạo kế hoạch từ trang giao dịch

**Branch:** `feature/trade-plan-enhancements`

### Thêm mới

- **Nút "Tạo kế hoạch"** trên trang Lịch sử giao dịch: khi mã CP chưa có kế hoạch nào, hiện nút tạo KH thay vì chỉ hiện text "Không có KH" — navigate đến `/trade-plan?symbol=XXX`
- **Pre-fill symbol** trên trang Kế hoạch: nhận query param `?symbol=` → tự điền mã CP + fetch giá hiện tại
- **Nút lưu trong sidebar**: "Lưu nháp" và "Lưu & Sẵn sàng" chuyển từ header xuống cột phải (sidebar), đúng luồng UX cuộn xuống

### Cải thiện

- Mobile: thay text tĩnh "Chưa gắn KH" bằng link actionable "+ Tạo KH" / "Gắn KH"
- Phân tách rõ khu vực Lưu kế hoạch vs Thực hiện giao dịch trong sidebar

---

## [v2.12.0] — 2026-03-18 · Trader's Daily Todo List & Routine Templates

**Branch:** `feature/trader-daily-todo`

### Thêm mới

- **Daily Routine Widget** trên Dashboard: hiển thị tiến độ nhiệm vụ hôm nay, streak badge (🔥), next uncompleted items với deep links
- **Trang `/daily-routine`**: quản lý nhiệm vụ hàng ngày đầy đủ — checklist theo 3 nhóm (Sáng / Trong phiên / Cuối ngày), progress bar, streak counter
- **5 Built-in Templates**: Swing Trading (12 bước), DCA (8 bước), Research (10 bước), Onboarding (8 bước), Crisis Checklist (8 bước)
- **Auto-suggest**: Tự gợi ý template dựa trên ngữ cảnh (ngày DCA, cuối tuần, thị trường biến động, lần đầu sử dụng)
- **Streak Gamification**: Đếm ngày liên tiếp hoàn thành, kỷ lục cá nhân, thông điệp động lực (3, 5, 10, 30 ngày)
- **Custom Templates**: Tạo/sửa/xoá mẫu riêng với form dynamic items
- **History Heatmap**: Lịch sử 30 ngày gần nhất (xanh/vàng/xám)
- **Deep Links**: Mỗi item có link navigate thẳng đến trang liên quan

### Backend

- Domain entities: `DailyRoutine`, `RoutineTemplate`, `RoutineItem`, `RoutineItemTemplate`
- API: `DailyRoutinesController` (`api/v1/daily-routines`) — 10 endpoints
- MongoDB: `daily_routines` (compound index UserId+Date, soft-delete cleanup trước insert), `routine_templates`
- Seed data: 5 built-in templates (Vietnamese có dấu đầy đủ)

### Frontend

- `DailyRoutineService` — 11 API methods
- `DailyRoutineComponent` — standalone, inline template, Tailwind CSS
- Dashboard widget tích hợp trong `DashboardComponent`
- Navigation: Header (Quản lý group) + Bottom nav (moreItems)

---

## [v2.11.0] — 2026-03-18 · Mobile Responsive — Tối ưu giao diện di động

**Branch:** `feature/b1-mobile-responsive`

### Thêm mới

- **Bottom Navigation** (`BottomNavComponent`): Thanh điều hướng cố định ở đáy màn hình trên mobile (< 768px) với 5 mục: Tổng quan, Giao dịch, Kế hoạch, Rủi ro, Thêm
- **Mobile card layout**: 14 bảng dữ liệu (trades, trade-plan, risk, analytics, portfolio-detail, portfolio-trades, portfolio-analytics, capital-flows, market-data, snapshots) chuyển sang dạng card trên mobile
- **Scrollable tabs**: Tab navigation cuộn ngang với ẩn scrollbar trên mobile (analytics, risk, snapshots)

### Cải thiện

- Grid summary cards xếp 1 cột trên mobile nhỏ (`grid-cols-1 sm:grid-cols-2`) — ~15 components
- Page header xếp dọc trên mobile (trades, dashboard, portfolios, portfolio-detail, portfolio-trades)
- Tooltip không bị tràn trên màn hình nhỏ (`max-width: calc(100vw - 2rem)`)
- Content padding `pb-14` trên mobile tránh bị bottom nav che

---

## [v2.10.0] — 2026-03-17 · Trade Replay — Xem lại giao dịch trên biểu đồ giá

**Branch:** `feature/m2-and-enhancements`

### Thêm mới

- **Trade Replay** (`/trade-replay/:id`): Visualize toàn bộ vòng đời kế hoạch giao dịch trên biểu đồ giá thực từ 24hmoney API
  - Biểu đồ giá đóng cửa (Chart.js) với overlay: vào lệnh (▲ xanh), thoát lệnh (▼ đỏ), tạo KH (★ xanh), stop-loss (nét đứt đỏ), mục tiêu (nét đứt xanh)
  - Summary cards: Giá vào lệnh (KH/TT), Lãi/Lỗ, R:R (KH/TT), Phí GD
  - Dòng thời gian sự kiện: Tạo KH → Vào lệnh → Điều chỉnh SL → Thoát lệnh → Hoàn thành
  - Entry point: Nút "Xem replay" trên bảng kế hoạch cho status Executed/Reviewed
- **Symbol Autocomplete real-time**: Thay thế file JSON tĩnh (58 mã) bằng `MarketDataService.searchStocks()` (API 24hmoney), debounce 300ms, hiển thị tên công ty + sàn

---

## [v2.9.0] — 2026-03-17 · Tích hợp 24hmoney API — Dữ liệu thị trường real-time

**Branch:** `feature/m2-and-enhancements`

### Thêm mới

- **24hmoney.vn API Provider**: `HmoneyMarketDataProvider` — nguồn dữ liệu thị trường chứng khoán Việt Nam real-time, thay thế toàn bộ mock data
- **5 API endpoints mới**: Stock detail (`/market/stock/{symbol}/detail`), Market overview (`/market/overview`), Search (`/market/search`), Top fluctuation (`/market/top-fluctuation`), Trading summary (`/market/stock/{symbol}/summary`)
- **IStockInfoProvider interface**: Interface mới cho stock detail, search, top fluctuation, trading summary
- **Trang Thị trường nâng cao**: Overview 4 chỉ số (VN-INDEX, VN30, HNX, UPCOM), tra cứu cổ phiếu chi tiết với order book 3 mức, tìm kiếm autocomplete (debounce 300ms), top biến động theo sàn (HOSE/HNX/UPCOM tabs), biến động giá 1D/1W/1M/3M/6M
- **Dashboard Market Overview**: Strip 4 index cards ở đầu dashboard — giá, %, KL

### Sửa lỗi

- **StockPriceService mock → real API**: Xoá toàn bộ giá cổ phiếu mock hardcoded (~20 mã), delegate sang `IMarketDataProvider` (24hmoney). P&L, Risk, Positions, Strategy Performance giờ dùng giá thật VND thay vì giá giả USD
- **Worker mock → real API**: Worker background jobs (PriceSnapshot, BacktestJob) giờ dùng `HmoneyMarketDataProvider` thay vì `MockMarketDataProvider`

### Cải thiện

- **IMemoryCache**: Cache giá cổ phiếu (15s), chỉ số (15s), danh sách công ty (30 phút) — configurable qua `appsettings.json`
- **Price ×1000 scaling**: API 24hmoney trả giá ÷1000, tự động nhân lại khi mapping. Chỉ số index giữ nguyên
- **Shared raw cache**: `GetCurrentPriceAsync` và `GetStockDetailAsync` dùng chung cache raw response — cùng 1 mã chỉ gọi API 1 lần trong 15s
- **MarketIndexData enriched**: Thêm foreign trading, advance/decline, prior close cho dữ liệu chỉ số
- **BaseUrl configurable**: URL API 24hmoney đọc từ config/env var, không hardcode

---

## [v2.8.0] — 2026-03-14 · M2 Fix + 6 Feature Enhancements

**Branch:** `feature/m2-and-enhancements`

### Bug fix

- **M2: Cột KẾ HOẠCH toàn "---"**: Thêm backend `LinkTradeToPlanCommand` + API `PATCH /trades/{id}/link-plan`, frontend hiện nút "Gắn KH" cho trade chưa liên kết, dropdown chọn kế hoạch theo mã CK

### Thêm mới

- **Import CSV**: Trang `/trades/import` — upload file CSV, preview dữ liệu, validate, bulk import giao dịch vào danh mục. Backend `BulkCreateTrades` API
- **Journal tự động**: Wizard step 4 auto-create journal entry khi ghi nhận giao dịch, step 5 update thay vì tạo mới nếu đã tồn tại
- **Dashboard vị thế nổi bật**: Widget "Vị thế nổi bật" hiện top 6 positions theo giá trị, P&L%, link đến trang Vị thế

### Cải thiện

- **Phiếu lệnh nâng cao**: Thêm nút In (print), hiện Danh mục + Giá trị lệnh trong phiếu, filter dòng trống
- **Vị thế — SL/TP distance**: Thanh gradient SL→TP với marker giá hiện tại, % khoảng cách đến SL/TP, cảnh báo màu khi gần SL
- **Vị thế — Sắp xếp**: Dropdown sắp xếp theo Giá trị / Lãi-Lỗ / % / Mã CK

---

## [v2.7.0] — 2026-03-14 · Phase 7 (tiếp): Bug fix Round 6

**Branch:** `feature/phase7-improvements`

### Sửa lỗi

- **H1: Fix DCA mode**: tách UI riêng cho DCA — giao diện mới với Số tiền/lần, Tần suất (tuần/2 tuần/tháng), Số kỳ, Ngày bắt đầu, Khoảng giá, Lịch mua dự kiến với bảng schedule
- **H2: Fix CAGR mismatch**: Dashboard và Analytics hiện dùng cùng nguồn CAGR (equity curve hoặc backend AdvancedAnalytics) — bỏ phép tính sai dùng `years=1` hardcoded
- **M1: Fix vị thế lớn nhất > 100%**: sửa backend `RiskCalculationService` dùng `Math.Max(netWorth, totalMarketValue)` làm mẫu số cho position sizing — tránh % vượt 100% khi tiền mặt âm
- **M3: Fix giá 0 trên trade-plan**: thêm `[emptyWhenZero]` directive vào NumMask — các trường Giá vào, Stop-Loss, Take-Profit, Số lượng hiện placeholder thay vì "0" khi chưa nhập

### Cải thiện

- **NumMaskDirective**: thêm `@Input() emptyWhenZero` — khi `true`, hiện empty thay vì "0" trong display mode
- **DCA form**: summary card (tổng vốn, thời gian, tần suất) + schedule table với cumulative amount
- **Trade Plan placeholders**: placeholder text gợi ý cho các trường giá ("Nhập giá dự kiến", "Mức cắt lỗ", "Mức chốt lời")

---

## [v2.6.0] — 2026-03-14 · Phase 7 (tiếp): Trade UX, Positions, Multi-lot Plan

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **Trang Vị thế đang mở** (`/positions`): hiển thị open positions gom nhóm theo danh mục, mỗi nhóm có tổng giá trị & P&L, expand giao dịch gần nhất cho từng mã
- **Trade Plan multi-lot**: hỗ trợ nhập lệnh chia lô (ScalingIn/DCA), exit targets (TP1/TP2/CutLoss), theo dõi stop-loss history, phiếu lệnh (order sheet) copy clipboard
- **Trade Plan saved plans**: danh sách kế hoạch đã lưu với filter trạng thái, lot progress bar, nút thực hiện từng lot
- **Positions API** (`GET /api/v1/positions`): backend query tổng hợp vị thế đang mở từ PnL + linked plan
- **TradePlan backend**: entity mới với lifecycle Draft→Ready→InProgress→Executed→Reviewed→Cancelled, CRUD API, commands ExecuteLot/UpdateStopLoss/TriggerExitTarget

### Cải thiện

- **TradeType enum dùng chung**: refactor toàn bộ project (6 components) sử dụng `TradeType` enum + utility functions từ `shared/constants/trade-types.ts` thay vì hardcode string
- **CAGR overflow fix**: sửa lỗi hiển thị `3.1e+260%` — thêm ngưỡng tối thiểu 30 ngày + clamp giá trị [-99.99%, 9999.99%] cả frontend và backend
- **Risk Dashboard tiếng Việt**: dịch toàn bộ text tiếng Anh còn sót sang tiếng Việt
- **Trades pagination**: sửa lỗi không nhấn được nút "Sau" (nextPage reset về trang 1)
- **Trades filter by symbol**: click vào mã CK trong bảng → tự fill ô filter, có nút × clear filter
- **Trade Create — lô chẵn**: lệnh MUA bắt buộc số lượng là bội số 100
- **Trade Create — kiểm tra số dư**: giá trị lệnh MUA không được vượt quá tiền còn lại của danh mục (initialCapital - totalInvested + totalSold)
- **Trade Create — hiện vốn danh mục**: dropdown danh mục hiển thị thêm tổng vốn bên cạnh tên
- **Trade Wizard**: dùng shared TradeType, pre-fill journal từ thông tin trade plan
- **Backtesting**: dùng shared `getTradeTypeDisplay`/`getTradeTypeClass`

### Sửa lỗi

- Fix webpack `Cannot access before initialization` error khi vào trang Risk và Strategies (cache corruption)
- Fix CAGR backend (`PerformanceMetricsService.cs`): clamp giá trị, minimum years 0.08

---

## [v2.5.0] — 2026-03-14 · Phase 7 (tiếp): NumMask, PnL & Journal enhancements

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **NumMaskDirective**: format số với dấu phân cách hàng nghìn trong input fields, áp dụng trên backtesting và strategies
- **Journal enhancements**: unsaved changes prompt, trade linkage improvements

### Cải thiện

- **PnL calculations**: cải thiện tính toán và xử lý lỗi trong PerformanceMetricsService
- **Error handling**: cải thiện middleware exception handling

---

## [v2.4.0] — 2026-03-13 · Phase 7 (tiếp): Tooltip Analytics & Glossary UX

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **Tooltip thuật ngữ trang Phân tích** (`/analytics`): hover vào icon `ⓘ` cạnh tên chỉ số để xem giải thích tại chỗ — không cần cuộn xuống glossary card
  - Header cards: CAGR, Sharpe Ratio, Sortino Ratio, Max Drawdown, Win Rate
  - Section "Chỉ số rủi ro": Win Rate, Profit Factor, Value at Risk (95%), Expectancy
  - Tab Equity Curve: tiêu đề, cột Lợi nhuận ngày, cột Lợi nhuận tích luỹ
- **CSS `.tooltip-trigger` / `.tooltip-box`** trong `styles.css`: component tooltip dùng chung toàn project — dark popup, mũi tên chỉ xuống, fade-in 0.15s

### Cải thiện

- **Glossary footnote style**: đổi từ ký tự Unicode `¹²³` sang chữ số thường `1 2 3` + CSS `::before`/`::after` tự thêm dấu ngoặc → hiển thị **(1) (2) (3)** nhất quán mọi nơi
- **SVG info icon**: thay thế ký tự `ⓘ` Unicode bằng Heroicons `information-circle` SVG — sắc nét, scale tốt mọi độ phân giải
- **Glossary footnote size**: `font-size: 0.85em`, `vertical-align: super` — dễ đọc hơn

### Gợi ý cho lần release tiếp theo

- [ ] Áp dụng `.tooltip-trigger` / `.tooltip-box` cho các trang khác (Risk Dashboard, Trade Plan) thay thế glossary card tĩnh
- [ ] Tooltip delay ~200ms để tránh hiện khi hover qua nhanh

---

## [v2.3.0] — 2026-03-13 · Phase 7 (tiếp): Thuật ngữ chuyên ngành & Strategy Auto-fill

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **Glossary thuật ngữ chuyên ngành** toàn project: mỗi thuật ngữ hiển thị số mũ nhỏ `¹²³` → giải thích đầy đủ ở cuối form, áp dụng đồng bộ trên tất cả trang:
  - **Risk Dashboard**: VaR 95%, Max Drawdown, Win Rate, Profit Factor, Beta, Tương quan (Correlation)
  - **Trade Wizard** (step 2 + step 5): Stop-Loss, Take-Profit, % Rủi ro/lệnh, R:R, Thiết lập kỹ thuật, FOMO
  - **Monthly Review**: Win Rate, P&L (Profit & Loss), Max Drawdown
  - **Journals**: Setup kỹ thuật, Trạng thái cảm xúc/FOMO, Mức tự tin, Post-trade Review
  - **Strategies** (form tạo + tab Hiệu suất): Khung thời gian (Scalping/Day Trading/Swing/Position), Win Rate, P&L, Profit Factor
- **Strategy auto-fill SL/TP** trong Trade Plan: chọn chiến lược có `SuggestedSlPercent` / `SuggestedRrRatio` → tự động tính và điền Stop-Loss & Take-Profit, hiển thị badge "✓ Tự động điền từ chiến lược"
- **`SuggestedSlPercent` và `SuggestedRrRatio`** trên Strategy entity (backend + frontend): 2 trường mới lưu gợi ý SL% dưới giá vào và R:R ratio, expose qua CQRS commands/queries và REST API

### Cải thiện

- Form tạo chiến lược: thêm input "SL gợi ý (%)" và "R:R gợi ý" với giải thích inline
- `onStrategyChange()` trong Trade Plan: tính SL/TP từ entry price × strategy hints, chỉ tự điền khi ô đang trống (không ghi đè nếu người dùng đã nhập)
- Glossary card dùng màu nhất quán: đỏ=SL, xanh lá=TP, xanh dương=R:R, cam=Drawdown, tím=Beta, hổ phách=Rủi ro

### Gợi ý cho lần release tiếp theo

- [ ] Glossary dạng tooltip hover thay vì card tĩnh cuối form (tiết kiệm không gian hơn)
- [ ] Trade Plan: nút "Reset về gợi ý chiến lược" khi SL/TP đã bị sửa tay
- [ ] Strategy Performance: thêm chart đường cong lãi/lỗ tích lũy theo thời gian

---

## [v2.2.0] — 2026-03-13 · Phase 7: Quick Trade, positionSize Template, Multi-timeframe

**Branch:** `feature/phase7-improvements`

### Thêm mới

- **Quick Trade widget** trên Dashboard: nhập mã CP (auto-fill giá), chiều, entry, SL → tính position size từ Risk Profile tại chỗ → "Mở trong Trade Plan" với dữ liệu đã điền sẵn
- **Multi-timeframe switcher** trên Dashboard: tab Hôm nay / Tuần này / Tháng này / Năm nay / Toàn bộ → hiển thị period return % và period P&L từ Equity Curve
- **`positionSize` trong Trade Plan Template**: lưu số lượng CP khi save template, tự điền lại khi load template

### Cải thiện

- Quick Trade collapsible panel — không chiếm không gian khi không dùng
- Multi-timeframe tính từ equity curve data đã có sẵn — không cần API call thêm
- Template save/load đầy đủ hơn: symbol, direction, giá, SL, target, chiến lược, lý do, notes, **số lượng CP**

### Gợi ý cho lần release tiếp theo

- [ ] Quick Trade: thêm ô Target → tính và hiển thị R:R ratio
- [ ] Multi-timeframe: thêm trade count và win rate trong kỳ (cần fetch trades theo date range)
- [ ] Risk Score badge tự refresh mỗi 5 phút (hiện chỉ load 1 lần lúc login)
- [ ] Keyboard shortcuts: `Ctrl+T` → Trade Plan, `Ctrl+W` → Wizard, `Ctrl+D` → Dashboard

---

Tất cả thay đổi đáng kể của dự án được ghi lại ở đây.
Format theo [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [v2.1.0] — 2026-03-13 · Phase 5 & 6: Auto-fill, Risk, Templates, Changelog

**Branch:** `feature/phase5-autofill-risk-compound`

### Thêm mới
- **Auto-fill giá cổ phiếu** trong Trade Wizard: nhập mã CP → blur → tự fetch giá hiện tại và điền vào Entry Price
- **Concentration Alert**: cảnh báo khi 1 cổ phiếu vượt giới hạn `maxPositionSizePercent` trong Risk Profile, hiển thị trực tiếp trên Dashboard
- **Trade Plan Template save/load**: lưu kế hoạch GD thành template, tải lại với 1 click, xóa template không cần nữa
- **Trang Changelog** (`/changelog`): developer changelog đọc từ file `.md`, accessible không cần đăng nhập
- **DEV badge** trên header: link nhanh đến `/changelog` từ mọi trang

### Sửa lỗi
- Fix lỗi `Cannot access 'MarketDataComponent' before initialization` do 2 `import` viết trên 1 dòng trong `market-data.service.ts`
- Fix Risk Alert Banner sort: hiển thị cảnh báo nghiêm trọng nhất lên đầu (descending)

### Cải thiện
- Dashboard load risk alert chạy song song với `forkJoin` thay vì tuần tự
- `docs/features.md`: tài liệu tính năng đầy đủ theo từng phase
- `docs/getting-started.md`: thêm mục "Build vs Deploy" với lệnh cụ thể cho dự án

### Gợi ý cho lần release tiếp theo
- [ ] Quick Trade widget ngay trên Dashboard (nhập CP + Mua/Bán + SL → tính Position Size tại chỗ)
- [ ] Risk Score badge trên header tự refresh mỗi 5 phút (hiện chỉ load 1 lần khi login)
- [ ] Thêm field `positionSize` vào Trade Plan Template để save/load luôn số lượng CP

---

## [v2.0.0] — 2026-03-12 · Phase 3 & 4: Charts, Risk Dashboard, Compound Tracker

**Branch:** `feature/phase4-charts-and-links`

### Thêm mới
- **Equity Curve chart** (Chart.js): line chart tăng trưởng vốn theo ngày, range filter 30D/90D/1Y/All
- **Monthly Returns Matrix**: hiệu suất theo năm × tháng, color-coded xanh/đỏ
- **CAGR thực tế**: tính từ capital flows + daily snapshots, hiển thị trên Dashboard
- **Compound Growth Tracker**: card "Lãi kép" trên Dashboard — CAGR thực tế, ước tính 5/10/20 năm, so sánh vs mục tiêu
- **Risk Alert Banner** trên Dashboard: stop-loss proximity, drawdown alert
- **Risk Dashboard** (`/risk-dashboard`): tổng quan sức khỏe rủi ro, bảng position, stress test 5 kịch bản VNINDEX
- **Risk Score badge** trên Header: badge màu động (xanh/vàng/đỏ) link đến Risk Dashboard
- **Monthly Review** (`/monthly-review`): báo cáo tháng tự động — win rate, P&L, drawdown, best/worst trade

### Cải thiện
- Analytics: thay placeholder bằng biểu đồ thực tế (bar chart P&L, donut phân bổ danh mục)
- Dashboard 4 Summary Cards: Tổng giá trị, Vốn đầu tư, P&L, CAGR

### Gợi ý đã xử lý ở phase sau
- ~~Concentration Alert~~ → Done v2.1.0
- ~~Auto-fill giá~~ → Done v2.1.0

---

## [v1.5.0] — 2026-03-10 · Phase 2: Wizard Flow & Risk Profile

**Branch:** `feature/phase2-wizard-flow`

### Thêm mới
- **Trade Wizard 5 bước** (`/trade-wizard`): Chiến lược → Kế hoạch → Checklist → Giao dịch → Nhật ký
- **GO/NO-GO enforcement**: checklist bắt buộc, không thể skip qua bước Giao dịch nếu chưa đạt ≥80%
- **Risk Profile** (`/risk`): thiết lập max position%, max risk/lệnh, R:R tối thiểu, max drawdown alert
- **Position Sizing tự động**: nhập Entry + SL → tính ngay số lượng CP dựa trên Risk Profile
- **Risk violations enforcement**: cảnh báo đỏ + yêu cầu xác nhận khi Trade Plan vi phạm Risk Profile

### Cải thiện
- Trade Plan: thêm các field SL, Target, Risk/Reward calculation
- Strategies: load từ system templates (14 chiến lược mẫu), filter theo category/difficulty/timeframe

---

## [v1.0.0] — 2026-03-05 · Phase 1: Nền tảng

**Branch:** `feature/phase1-foundation`

### Thêm mới
- **Google OAuth 2.0** login
- **Portfolio CRUD**: tạo/sửa/xóa danh mục đầu tư
- **Trade CRUD**: ghi nhận giao dịch Mua/Bán
- **P&L theo Average Cost Method**: Realized + Unrealized P&L
- **Capital Flows**: theo dõi dòng vốn vào/ra
- **Daily Snapshots**: lưu giá trị danh mục mỗi ngày cho Equity Curve
- **Journals** (`/journals`): nhật ký giao dịch
- **Alerts** (`/alerts`): cảnh báo giá, stop-loss
- **Market Data** (`/market-data`): tra cứu giá cổ phiếu từ API bên ngoài
- **Backtesting** (`/backtesting`): backtest chiến lược cơ bản

### Kiến trúc
- Clean Architecture: Domain → Application → Infrastructure → API
- CQRS + MediatR
- MongoDB 7.0
- .NET 8 + Angular 19
- JWT authentication
- Background Worker cho P&L calculations

---

## [v0.1.0] — 2026-02-20 · Khởi tạo dự án

### Thêm mới
- Khởi tạo solution `.NET 8` với Clean Architecture
- Khởi tạo Angular 19 frontend với Tailwind CSS
- Cấu hình MongoDB connection
- Cấu hình JWT authentication
- Docker Compose cho local development
