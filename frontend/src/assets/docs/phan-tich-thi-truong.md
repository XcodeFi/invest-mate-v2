# Phân tích thị trường

> Tra cứu giá cổ phiếu, phân tích kỹ thuật xếp theo 6 khối chỉ báo, tín hiệu mua/bán tổng hợp.

---

## Trang Thị trường (`/market-data`)

### Chỉ số thị trường

4 index chính hiển thị realtime: **VN-INDEX**, **VN30**, **HNX**, **UPCOM** — giá, thay đổi %, khối lượng.

### Tra cứu cổ phiếu

1. Nhập mã CP (VD: FPT) vào ô tìm kiếm
2. Nhấn **Tra cứu** → hiện thông tin chi tiết:
   - Giá OHLC (Mở/Cao/Thấp/Đóng)
   - Trần/Sàn/Tham chiếu
   - Order book 3 mức (bid/ask)
   - Biến động 1D/1W/1M/3M/6M

### Bảng giá nhanh

Nhập nhiều mã cách nhau dấu phẩy (VD: VNM,FPT,VCB), bấm **Xem giá** để xem giá đóng cửa và khối lượng của từng mã.
- **Trang nhớ danh sách bạn xem lần trước** và tự tải lại khi bạn mở trang. Chỉ nhớ khi xem giá thành công; chưa có danh sách nào thì dùng danh sách mặc định (VNM, FPT, VCB, HPG, MWG, VIC). Danh sách được nhớ trong trình duyệt này, không đồng bộ sang máy khác.
- **Gợi ý xem nhanh hôm nay** nằm phía trên ô nhập, bấm một chip là điền mã vào ô, tải giá và nhớ luôn:
  - **Đang nắm giữ (N):** các mã bạn đang giữ trong các danh mục.
  - **Mã có kế hoạch mở (N):** số mã (không phải số kế hoạch) có kế hoạch giao dịch còn hiệu lực; một mã có nhiều kế hoạch chỉ tính một lần.
  - **Mỗi danh sách theo dõi một chip** (tên danh sách và số mã): mã của danh sách được tải khi bạn bấm.
  - Nguồn nào trống thì chip đó không hiện; nguồn nào không tải được thì chỉ chip đó biến mất, bảng giá vẫn dùng bình thường.

### Top biến động

Tab **HOSE/HNX/UPCOM** — bảng mã cổ phiếu biến động mạnh nhất trong ngày.

---

## Đọc biểu đồ cho người mới

Biểu đồ trong trang Thị trường gồm **nến** ở trên và, khi bạn chọn một nhóm chỉ báo, các **đường** và **tầng** bên dưới. Có bốn thứ cần phân biệt:

| Bạn thấy | Nghĩa là gì |
|----------|-------------|
| **Nến xanh** | Phiên giá tăng: giá đóng cửa cao hơn giá mở cửa |
| **Nến đỏ** | Phiên giá giảm: giá đóng cửa thấp hơn giá mở cửa |
| **Đường liền, có màu** | Một chỉ báo (ví dụ EMA, RSI). Ô màu nhỏ ở chú giải cho biết đường nào là chỉ báo nào |
| **Đường ngang đứt nét** | Một mốc giá (hỗ trợ, kháng cự, cắt lỗ, chốt lời, Fibonacci). Danh sách các mốc và giá nằm ngay dưới biểu đồ |
| **Vạch ngang mờ trong một tầng** | Ngưỡng tham chiếu của chỉ báo, ví dụ 70 "Quá mua" và 30 "Quá bán" của RSI |
| **Cột** (histogram) | Chỉ báo vẽ bằng cột thay vì đường, ví dụ khối lượng hoặc cột Histogram của MACD |

**Mỗi thân nến** đi từ giá mở cửa đến giá đóng cửa; **râu nến** kéo tới giá cao nhất và thấp nhất của phiên.

### Chú giải nằm ở đâu

- **Trên mỗi tầng** có một dòng chú giải: ô màu, tên chỉ báo kèm tham số (ví dụ "RSI 14", "MACD 12 26 9" nghĩa là tính theo 14 phiên, hay 12-26-9 phiên), và con số của phiên cuối. Trỏ chuột vào một phiên thì con số đổi theo phiên đó.
- **Tầng giá** có thêm hàng **Mở / Cao / Thấp / Đóng** kèm mức thay đổi so với phiên trước, và dòng giải thích màu nến.
- **Con số cuối của mỗi đường** hiện thành nhãn màu ở lề phải biểu đồ.
- **Dưới biểu đồ** là danh sách các đường ngang: vạch đứt nét đúng màu, tên mốc và giá (ví dụ "Hỗ trợ 1 · 85.200").
- **Dưới khối biểu đồ** có khối **📖 Cách đọc nhóm...**: câu "đọc nhanh" cho giá trị hiện tại và giải thích từng chỉ báo của nhóm đang chọn. Bấm để thu gọn hoặc mở ra; trang nhớ lựa chọn của bạn.

### Hai từ hay gặp

- **Quá mua**: giá đã tăng nhanh nhiều phiên liền, dễ chững lại hoặc điều chỉnh. Không có nghĩa là phải bán ngay.
- **Quá bán**: giá đã giảm nhanh nhiều phiên liền, dễ có nhịp hồi. Không có nghĩa là phải mua ngay.

> Chỉ báo mô tả quá khứ, không đảm bảo tương lai. Dùng kết hợp nhiều chỉ báo, đừng ra quyết định chỉ từ một chỉ báo.

---

## Smart Signals — panel 6 khối chỉ báo

Khi tra cứu mã CP, panel phân tích kỹ thuật hiển thị theo **6 khối**, mỗi khối trả lời một câu hỏi khi soi mã:

| Khối | Trả lời câu hỏi |
|------|-----------------|
| 🎯 **Tổng hợp** | Chốt lại: làm gì bây giờ? (Điểm Confluence + Gợi ý giao dịch) |
| 📈 **Xu hướng** | Trend hay sideway? Hướng nào, mạnh không? |
| ⚡ **Động lượng** | Lực còn không, sắp đảo chiều chưa? |
| 📏 **Biến động** | Dao động rộng cỡ nào → stop bao xa? |
| 💰 **Dòng tiền** | Tiền vào thật hay chỉ kéo giá? |
| 🧱 **Mốc giá** | Vào/ra ở vùng nào? |

Khối **🎯 Tổng hợp** nằm trên cùng (chỉ hiện khi có Điểm Confluence hoặc Gợi ý giao dịch — xem hai mục bên dưới). Các khối còn lại:

### 📊 Chọn nhóm để xem biểu đồ

Bấm một **nút nhóm** trên thanh ngay phía trên biểu đồ (🎯 Tổng hợp, 📈 Xu hướng, ⚡ Động lượng, 📏 Biến động, 💰 Dòng tiền, 🧱 Mốc giá) thì biểu đồ nến ngay dưới tiêu đề panel vẽ các chỉ báo của nhóm đó. Bấm lại nhóm đang chọn để chỉ còn nến. Mỗi lần xem một nhóm.

| Nhóm | Vẽ gì |
|------|-------|
| 🎯 Tổng hợp | Đường ngang Giá vào / Cắt lỗ / Chốt lời của gợi ý giao dịch |
| 📈 Xu hướng | EMA 20, EMA 50 đè lên nến; tầng dưới là ADX 14 cùng +DI, −DI (vạch 25 "Có xu hướng") |
| ⚡ Động lượng | Ba tầng: RSI 14 (vạch 30/70), MACD 12 26 9, Stoch 14 3 (vạch 20/80) |
| 📏 Biến động | Bollinger 20 2 trên/giữa/dưới đè lên nến; tầng dưới là ATR 14 |
| 💰 Dòng tiền | Khối lượng và đường KL SMA 20 (trung bình 20 phiên), OBV, MFI 14 (vạch 20/80) |
| 🧱 Mốc giá | Đường ngang hỗ trợ, kháng cự và các mốc Fibonacci có nhãn |

- **Gắn sao:** mỗi nút nhóm có nút **☆** riêng; bấm để gắn **★**. Nhóm có sao đứng đầu thanh, và nhóm có sao đầu tiên được mở sẵn khi vào trang (địa chỉ trang có chỉ định nhóm thì theo địa chỉ). Sao chỉ lưu trong trình duyệt này.
- **Nhà đầu tư lão luyện dùng thế nào:** ở từng chỉ báo bên dưới có mục này, mỗi dòng ghi ai, dùng thế nào, **vì sao** và **nguồn**. Tác giả chỉ báo chỉ được ghi là tác giả, không phải bằng chứng họ giỏi đầu tư. Cách dùng của họ có thể khác chỉ báo app vẽ (SMA hay EMA, N 20 ngày hay ATR 14...), mục đó ghi rõ.
- **Cơ sở và giới hạn:** nghiên cứu về quy tắc đường trung bình cho kết quả trái chiều (Brock, Lakonishok và LeBaron 1992 thấy có lợi thế; Sullivan, Timmermann và White 1999 không giữ được sau khi hiệu chỉnh việc thử nhiều quy tắc), nên hãy xem chúng như **bộ lọc rủi ro**, không phải công cụ dự báo. Ngưỡng RSI 70/30 dùng riêng có bằng chứng hạn chế. Ở Việt Nam, biên độ giá là ±7% (HOSE), ±10% (HNX), ±15% (UPCoM) và cổ phiếu mới mua về tài khoản vào chiều T+2: mức cắt lỗ là kế hoạch, không phải cam kết, vì giá có thể nhảy cóc qua mức đó.
- Các tầng cùng trục thời gian: kéo hoặc zoom một tầng thì các tầng khác đi theo. Trỏ chuột vào một tầng, các tầng còn lại hiện đường dọc tại cùng phiên và dòng chú giải của từng tầng ghi giá trị của phiên đó.
- Điểm cuối mỗi đường bằng đúng con số trên thẻ cùng tên.
- Giá trên biểu đồ là giá **đã điều chỉnh** theo sự kiện quyền (cổ tức tiền, cổ phiếu thưởng, chia tách): ngày giao dịch không hưởng quyền không còn hiện thành một cú tụt giả, nên EMA, RSI và các mức hỗ trợ không bị lệch. Vì vậy giá cũ trên biểu đồ có thể thấp hơn giá bạn thấy hồi đó.
- Nhóm Xu hướng vẽ thêm **đường EMA 200** (màu xanh ngọc, trung bình 200 phiên, nền dài hạn) khi mã đã có đủ 200 phiên.
- Biểu đồ lấy khoảng 3 năm nến thật (mở, cao, thấp, đóng), nên **EMA200 có lại** và các chỉ báo dùng râu nến (ATR, ADX, Stochastic, MFI) tính trên giá cao và thấp thật của từng phiên.
- Nếu nguồn dữ liệu chính tạm lỗi, trang tự dùng nguồn dự phòng và hiện dòng "Dữ liệu chưa điều chỉnh theo sự kiện quyền, nến chỉ có giá đóng cửa": lúc đó nến sẽ dẹt và quanh ngày không hưởng quyền chỉ báo có thể lệch.
- Mã mới có ít phiên sẽ thiếu một số chỉ báo: biểu đồ vẫn có nến, chỉ báo thiếu ghi "Không đủ dữ liệu".
- Nếu hiện "Không tải được dữ liệu biểu đồ" thì các thẻ vẫn dùng bình thường; bấm lại tên nhóm để thử tải lại.
- Đổi sang mã khác thì biểu đồ trở về trạng thái chưa chọn nhóm.

### 🎯 Tổng hợp trên biểu đồ

Khi bấm nhóm **🎯 Tổng hợp**, biểu đồ vẽ ba đường ngang đứt nét theo gợi ý giao dịch: **Giá vào** (xanh dương), **Cắt lỗ** (đỏ), **Chốt lời** (xanh lá).

- **Là gì:** ba mức giá hệ thống đề xuất dựa trên hỗ trợ, kháng cự và biến động gần đây.
- **Cách đọc:**
  - Cắt lỗ là mức giá nếu chạm thì nhận sai và thoát, để lỗ không vượt quá mức bạn chấp nhận. *Vì sao:* Một lệnh sai không có điểm thoát định sẵn sẽ dễ bị giữ lại theo cảm xúc, và lỗ sâu thì phải lãi lớn hơn nhiều mới bù lại được.
  - Chốt lời nằm xa giá vào hơn cắt lỗ thì thường được xem là đáng cân nhắc: ví dụ vào 100, cắt lỗ 95 (rủi ro 5), chốt lời 110 (lãi 10) là lãi gấp đôi rủi ro. *Vì sao:* Lãi gấp đôi rủi ro thì dù đúng ít hơn sai vẫn có thể có lãi; lãi nhỏ hơn rủi ro thì phải đúng rất nhiều mới hoà vốn.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - Chưa có nhà đầu tư nào có thành tích kiểm chứng được gắn với chỉ báo này; đây là phần ứng dụng tự tính.
- **Điểm Confluence:** con số 0 đến 100 cho biết bao nhiêu chỉ báo cùng nghiêng về một phía. Cách đọc:
  - Trên 60: đa số chỉ báo nghiêng về mua. Dưới 40: đa số nghiêng về bán. Từ 40 đến 60: các chỉ báo chưa đồng thuận. *Vì sao:* Một chỉ báo đơn lẻ hay báo giả; nhiều chỉ báo cùng chiều thì xác suất sai của từng cái giảm đi.
  - Ví dụ: 72 điểm nghĩa là nhiều chỉ báo đang cùng chiều, độ tin cậy cao hơn khi chỉ có một chỉ báo báo mua. *Vì sao:* Các chỉ báo đều tính từ cùng một chuỗi giá nên đồng thuận chưa phải bằng chứng độc lập; vì vậy chỉ coi là tham khảo.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - Chưa có nhà đầu tư nào có thành tích kiểm chứng được gắn với chỉ báo này; đây là phần ứng dụng tự tính.
- **Bẫy hay gặp:** đây là gợi ý để so với kế hoạch của bạn, không phải lệnh phải làm theo; điểm cao chỉ nói các chỉ báo đồng thuận trên dữ liệu quá khứ.

### 📈 Xu hướng

**EMA 20 và EMA 50 (đường trung bình)**
- **Là gì:** đường trung bình của giá đóng cửa 20 và 50 phiên gần nhất, phiên gần đây được tính nặng hơn. Nó làm mượt biến động hằng ngày để thấy hướng đi chung.
- **Trên biểu đồ:** hai đường đè lên nến: EMA 20 màu xanh dương (nhanh hơn), EMA 50 màu cam (chậm hơn).
- **Cách đọc:**
  - EMA 20 nằm trên EMA 50 và cả hai dốc lên: xu hướng ngắn hạn đang tăng. Ngược lại là đang giảm. *Vì sao:* Đường trung bình dốc lên nghĩa là giá đóng cửa gần đây trung bình cao hơn trước; đường nhanh trên đường chậm cho thấy đà đang đi cùng chiều.
  - Giá nằm trên cả hai đường: phe mua đang giữ thế. Giá chui xuống dưới cả hai: thế đã yếu đi. *Vì sao:* Người mua gần đây đang có lãi so với giá trung bình nên ít có lý do bán; khi giá xuống dưới, phần lớn người mua gần đây đang lỗ và dễ bán.
  - Hai đường cắt nhau là lúc xu hướng có thể đổi chiều. *Vì sao:* Đường nhanh cắt đường chậm nghĩa là giá gần đây đã đi ngược hướng của giá trung bình dài hơn.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Mark Minervini (nhà đầu tư, vô địch US Investing Championship 1997)**: Mẫu xu hướng (Trend Template): giá nằm trên các đường 150 và 200 ngày, đường 150 trên đường 200, đường 200 dốc lên ít nhất một tháng, đường 50 trên đường 150 và 200, giá trên đường 50. *Vì sao:* Cổ phiếu tăng mạnh thường nằm trong giai đoạn tăng giá rõ ràng; bộ lọc này loại các cổ phiếu đang đi xuống hoặc đi ngang. *Nguồn:* Mark Minervini, Trade Like a Stock Market Wizard (2013), nguồn thứ cấp về thành tích. *Lưu ý:* Ông dùng đường trung bình đơn giản (SMA) 50, 150, 200 ngày; ứng dụng vẽ EMA 20, 50, 200 nên không đối chiếu từng tiêu chí của ông được.
  - **Paul Tudor Jones (nhà quản lý quỹ)**: Chuyển sang phòng thủ khi giá nằm dưới đường trung bình 200 ngày của giá đóng cửa. *Vì sao:* Tránh phải chịu những đợt sụt giảm lớn: bảo toàn vốn được ưu tiên hơn việc bắt đáy. *Nguồn:* Phỏng vấn Paul Tudor Jones trong Tony Robbins, Money: Master the Game (2014), nguồn thứ cấp. *Lưu ý:* Ông dùng đường 200 ngày; ứng dụng vẽ EMA 200 khi mã có đủ 200 phiên. Các nghiên cứu về quy tắc đường trung bình cho kết quả trái chiều, nên dùng như bộ lọc rủi ro chứ không phải công cụ dự báo.
- **Bẫy hay gặp:** khi giá đi ngang, hai đường cắt nhau liên tục và cho tín hiệu giả; xem thêm ADX trước khi tin.

**ADX 14, +DI và −DI**
- **Là gì:** ADX đo xu hướng mạnh hay yếu (không cho biết hướng). +DI là lực của phe mua, −DI là lực của phe bán.
- **Trên biểu đồ:** một tầng riêng: ADX màu tím, +DI xanh lá, −DI đỏ, kèm vạch mờ ở mức 25 "Có xu hướng".
- **Cách đọc:**
  - ADX từ 25 trở lên: có xu hướng rõ. Dưới 25: giá đang đi ngang hoặc chưa rõ hướng, ví dụ ADX 18. *Vì sao:* Chiến lược theo xu hướng chỉ có lợi khi có xu hướng; khi đi ngang thì các tín hiệu cắt đường trung bình thường báo giả.
  - +DI nằm trên −DI: lực tăng trội hơn. −DI nằm trên +DI: lực giảm trội hơn. *Vì sao:* ADX không có hướng, nên cần so hai lực mua và bán để biết xu hướng mạnh đó đang đi lên hay đi xuống.
  - ADX càng cao thì xu hướng càng mạnh, nhưng ADX cao vẫn có thể là xu hướng giảm: nhìn +DI và −DI để biết hướng. *Vì sao:* Xu hướng giảm mạnh cũng làm ADX tăng, vì ADX chỉ đo độ mạnh của cú đi một chiều.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **J. Welles Wilder (tác giả chỉ báo)**: Giới thiệu ADX và +DI, −DI trong hệ thống chuyển động có hướng; ADX trên 25 là có xu hướng mạnh, dưới 20 là gần như không có xu hướng. *Vì sao:* Cần một thước đo tách bạch độ mạnh của xu hướng khỏi hướng của nó, để biết khi nào nên dùng chiến lược theo xu hướng. *Nguồn:* J. Welles Wilder, New Concepts in Technical Trading Systems (1978); StockCharts ChartSchool: ADX. *Lưu ý:* Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư. Mức 25 là quy ước, vùng 20 đến 25 là vùng xám.
- **Bẫy hay gặp:** ADX là chỉ báo chậm; lên cao thường đã đi được một đoạn, và ADX giảm không có nghĩa là giá đảo chiều ngay.

**SMA 50, 150, 200 và thẻ Trend Template**
- **Là gì:** Giống EMA nhưng là trung bình ĐƠN GIẢN của giá đóng cửa 50, 150 và 200 phiên: mọi phiên trong khoảng được tính ngang nhau, không nặng về phiên gần đây.
- **Trên biểu đồ:** Không vẽ lên biểu đồ (đã có EMA); giá trị và khoảng cách nằm ở thẻ Trend Template (Mark Minervini) ngay dưới biểu đồ khi chọn nhóm Xu hướng.
- **Cách đọc:**
  - Giá nằm trên SMA150 và SMA200, SMA150 trên SMA200, SMA200 dốc lên ít nhất một tháng: cổ phiếu đang ở giai đoạn tăng dài hạn. *Vì sao:* Đường dài hạn dốc lên nghĩa là giá đóng cửa trung bình của gần một năm đang cao dần; giá và các đường ngắn nằm trên nó cho thấy xu hướng tăng đã kéo dài, không chỉ là một nhịp hồi.
  - SMA50 trên SMA150 và SMA200, giá trên SMA50: nhịp tăng gần đây khớp với nền dài hạn. *Vì sao:* Đường ngắn nằm trên đường dài nghĩa là giá gần đây cao hơn mức trung bình của quãng dài hơn, tức đà vẫn đi cùng chiều xu hướng lớn.
  - Dòng ghi "Không đủ dữ liệu" (SMA200 cần 200 phiên, SMA200 dốc lên cần khoảng 221 phiên) nghĩa là chưa kiểm được, không phải không thoả. *Vì sao:* Mã mới niêm yết chưa đủ lịch sử; coi "chưa biết" là thoả hay không thoả đều sai.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Mark Minervini (nhà đầu tư, vô địch US Investing Championship 1997)**: Trend Template gồm tám tiêu chí: giá trên SMA150 và SMA200; SMA150 trên SMA200; SMA200 dốc lên ít nhất một tháng; SMA50 trên SMA150 và SMA200; giá trên SMA50; giá cao hơn đáy 52 tuần ít nhất 30%; giá cách đỉnh 52 tuần tối đa 25%; xếp hạng sức mạnh tương đối RS từ 70 (ưu tiên 80 trở lên). *Vì sao:* Cổ phiếu tăng mạnh thường đi qua giai đoạn tăng giá rõ ràng; tám tiêu chí loại các cổ phiếu đang đi xuống hoặc mới hồi nhẹ từ đáy. *Nguồn:* Mark Minervini, Trade Like a Stock Market Wizard (2013), nguồn thứ cấp về thành tích. *Lưu ý:* Ông dùng SMA, thẻ này cũng dùng SMA (khác EMA trên biểu đồ). Tiêu chí RS không tính được vì ứng dụng chưa có xếp hạng RS theo chuẩn IBD. Thẻ không có kết luận tổng đạt hay không đạt.
  - **Stan Weinstein (nhà phân tích)**: Phân tích giai đoạn: chỉ mua ở giai đoạn 2, khi giá phá lên trên đường trung bình 30 tuần đang dốc lên kèm khối lượng lớn; tránh giai đoạn 4, khi giá nằm dưới đường 30 tuần đang dốc xuống. *Vì sao:* Đứng cùng phía với xu hướng lớn và tránh những đợt giảm kéo dài. *Nguồn:* Stan Weinstein, Secrets for Profiting in Bull and Bear Markets (1988), nguồn thứ cấp. *Lưu ý:* Ông dùng đường 30 tuần trên biểu đồ tuần; SMA150 ngày chỉ xấp xỉ nó, và ứng dụng không vẽ đường này.
- **Bẫy hay gặp:** Đây là bộ lọc theo xu hướng của quá khứ: cổ phiếu thoả nhiều tiêu chí vẫn có thể giảm, và không tiêu chí nào cho biết lúc nên mua.

Thẻ **Trạng thái thị trường** phân loại trend hay đi ngang kèm chiến lược gợi ý (xem mục Trạng thái thị trường).

### ⚡ Động lượng

**RSI 14**
- **Là gì:** con số từ 0 đến 100 so sánh lực tăng và lực giảm của 14 phiên gần nhất. Cao nghĩa là giá vừa tăng mạnh, thấp nghĩa là vừa giảm mạnh.
- **Trên biểu đồ:** một tầng riêng, đường tím giữa hai vạch mờ: 70 "Quá mua" ở trên, 30 "Quá bán" ở dưới.
- **Cách đọc:**
  - Trên 70: giá đã tăng nóng, dễ điều chỉnh. Dưới 30: giá đã giảm sâu, dễ hồi. Ví dụ RSI 72 là vùng nóng. *Vì sao:* Sau một chuỗi phiên tăng liên tiếp, người mua đã có lãi và lực mua mới yếu dần, nên giá dễ chững lại; chiều giảm thì ngược lại.
  - Từ 30 đến 70: chưa quá nóng cũng chưa quá lạnh, không có tín hiệu riêng. *Vì sao:* Ở vùng giữa, lực tăng và giảm chưa lệch nhiều nên con số không cho thêm thông tin ngoài xu hướng đang có.
  - Nên chờ RSI quay đầu khỏi vùng nóng/lạnh rồi mới coi là tín hiệu, không chỉ vì vừa chạm 70 hay 30. *Vì sao:* Trong xu hướng mạnh RSI có thể nằm ở vùng nóng rất lâu; chỉ khi nó quay đầu mới cho thấy đà đã thật sự yếu đi.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **J. Welles Wilder (tác giả chỉ báo)**: Đưa ra RSI với chu kỳ 14 phiên để so mức tăng trung bình với mức giảm trung bình. *Vì sao:* Cần một con số cố định trong khoảng 0 đến 100 để so độ nóng của các giai đoạn và các mã với nhau. *Nguồn:* J. Welles Wilder, New Concepts in Technical Trading Systems (1978). *Lưu ý:* Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư. Ngưỡng 70 và 30 là quy ước phổ biến; dùng riêng nó, bằng chứng về hiệu quả còn hạn chế.
- **Bẫy hay gặp:** trong xu hướng tăng mạnh RSI có thể ở trên 70 rất lâu; "quá mua" không có nghĩa là phải bán ngay.

**MACD 12 26 9**
- **Là gì:** so sánh hai đường trung bình (nhanh và chậm) để thấy đà tăng hay giảm đang mạnh lên hay yếu đi.
- **Trên biểu đồ:** một tầng riêng: đường MACD xanh dương, đường tín hiệu (Signal) cam, và các cột xám (Histogram) là khoảng cách giữa hai đường.
- **Cách đọc:**
  - MACD cắt lên trên đường tín hiệu: đà tăng đang lên. Cắt xuống dưới: đà giảm đang lên. *Vì sao:* MACD là chênh lệch giữa đường trung bình nhanh và chậm; khi nó vượt đường làm mượt của chính nó, chênh lệch đang nở ra theo chiều đó.
  - Cột Histogram cao dần: đà đang mạnh lên; thấp dần về 0: đà đang yếu đi. *Vì sao:* Histogram là khoảng cách giữa MACD và đường tín hiệu, nên nó co lại trước khi hai đường cắt nhau và báo sớm hơn.
  - MACD trên mức 0 nghĩa là xu hướng chung nghiêng về tăng, dưới 0 là nghiêng về giảm. *Vì sao:* MACD bằng 0 nghĩa là đường nhanh bằng đường chậm; trên 0 nghĩa là giá gần đây đang cao hơn mức trung bình dài hơn.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Alexander Elder (nhà giao dịch, tác giả)**: Hệ thống Ba màn hình: màn hình tuần (độ dốc của histogram MACD tuần hoặc EMA 13 tuần) cho biết "thuỷ triều"; màn hình ngày chờ chỉ báo dao động kéo ngược chiều thuỷ triều rồi mới vào lệnh; rủi ro tối đa 2% vốn mỗi lệnh, ngừng giao dịch trong tháng khi lỗ 6%. *Vì sao:* Giao dịch thuận chiều xu hướng lớn thì thắng thế hơn, và giới hạn mức thiệt hại giữ cho một chuỗi sai không làm cạn vốn. *Nguồn:* Alexander Elder, Trading for a Living (1993), nguồn thứ cấp. *Lưu ý:* Màn hình đầu của ông là khung tuần; ứng dụng chỉ vẽ nến ngày nên chỉ tham khảo được màn hình thứ hai.
- **Bẫy hay gặp:** MACD phản ứng chậm hơn giá và hay cho tín hiệu giả khi giá đi ngang.

**Stochastic 14 3**
- **Là gì:** cho biết giá đóng cửa đang nằm ở đâu trong biên độ cao-thấp của 14 phiên gần nhất, từ 0 (sát đáy) đến 100 (sát đỉnh).
- **Trên biểu đồ:** một tầng riêng: %K xanh dương (nhanh), %D cam (chậm, là trung bình của %K), hai vạch mờ ở 80 "Quá mua" và 20 "Quá bán".
- **Cách đọc:**
  - Trên 80: giá đang sát đỉnh biên độ gần đây. Dưới 20: giá đang sát đáy. *Vì sao:* Giá đóng cửa sát đỉnh biên độ nghĩa là người mua đang thắng thế đến phút cuối phiên; sát đáy thì ngược lại.
  - %K cắt lên trên %D ở vùng dưới 20 thường được xem là tín hiệu hồi; cắt xuống ở vùng trên 80 là tín hiệu yếu đi. *Vì sao:* %D là đường làm mượt của %K nên %K vượt nó cho thấy đà ngắn hạn vừa đổi chiều, còn vùng 20 và 80 cho biết đổi chiều đó đang xảy ra ở vùng cực.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Alexander Elder (nhà giao dịch, tác giả)**: Dùng chỉ báo dao động như Stochastic ở màn hình ngày của hệ thống Ba màn hình: mua khi nó rơi xuống vùng thấp trong lúc xu hướng tuần đi lên. *Vì sao:* Chờ nhịp điều chỉnh ngắn trong xu hướng tăng để vào giá tốt hơn, thay vì đuổi theo giá. *Nguồn:* Alexander Elder, Trading for a Living (1993), nguồn thứ cấp. *Lưu ý:* Ông chỉ dùng nó khi màn hình tuần đã xác nhận xu hướng; ứng dụng không vẽ khung tuần.
- **Bẫy hay gặp:** rất nhạy, hay đổi chiều liên tục; chỉ dùng để xác nhận cùng RSI hoặc xu hướng.

**Phân kỳ:** giá và RSI/MACD lệch hướng nhau là tín hiệu đảo chiều (xem mục Phát hiện phân kỳ).

### 📏 Biến động

**Bollinger 20 2**
- **Là gì:** một dải gồm đường trung bình 20 phiên ở giữa, cùng hai đường cách đều hai bên (2 lần độ lệch chuẩn). Phần lớn thời gian giá nằm trong dải.
- **Trên biểu đồ:** ba đường xám đè lên nến: đường giữa đậm hơn, hai đường ngoài nhạt hơn.
- **Cách đọc:**
  - Dải hẹp lại: giá đang đi hẹp, thường báo trước một nhịp chạy mạnh (chưa biết lên hay xuống). *Vì sao:* Biến động có chu kỳ: sau giai đoạn yên lặng thường là giai đoạn biến động lớn, nhưng dải hẹp không cho biết hướng.
  - Dải mở rộng: biến động đang lớn. *Vì sao:* Dải được tính từ độ lệch chuẩn của giá, nên giá dao động rộng hơn thì dải nở ra.
  - Giá chạm hoặc vượt đường trên: đang đi xa mức trung bình, dễ chững lại hoặc quay về giữa dải. Đường dưới thì ngược lại. *Vì sao:* Hai đường ngoài cách mức trung bình 2 độ lệch chuẩn, nơi giá ít khi ở lâu; nhưng trong xu hướng mạnh giá có thể bám dọc đường ngoài.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **John Bollinger (tác giả chỉ báo)**: Giai đoạn nén: độ rộng dải (BandWidth) xuống thấp nhất trong khoảng 6 tháng thì biến động lớn thường theo sau; hướng đi được xác định khi giá phá ra khỏi dải; chạm dải trên một mình không phải tín hiệu bán. *Vì sao:* Biến động lên xuống theo chu kỳ, nên lúc thị trường nén chặt nhất là lúc cần chuẩn bị cho nhịp mở rộng, còn hướng thì phải chờ giá cho biết. *Nguồn:* John Bollinger, Bollinger on Bollinger Bands (2001); StockCharts ChartSchool: Bollinger BandWidth. *Lưu ý:* Cài đặt mặc định của ông là đường 20 phiên và 2 độ lệch chuẩn, giống ứng dụng. Ứng dụng chưa vẽ BandWidth nên chưa phát hiện được giai đoạn nén; đây là người tạo ra chỉ báo, không phải bằng chứng thành tích.
- **Bẫy hay gặp:** khi xu hướng tăng mạnh, giá bám dọc đường trên là bình thường; chạm đường trên không tự động là tín hiệu bán.

**ATR 14**
- **Là gì:** biên độ dao động trung bình mỗi phiên trong 14 phiên gần nhất, tính bằng đồng (không cho biết hướng).
- **Trên biểu đồ:** một tầng riêng, đường xanh ngọc; số càng lớn thì giá càng dao động mạnh.
- **Cách đọc:**
  - Ví dụ ATR là 2.000đ: mỗi phiên giá thường chạy cỡ 2.000đ, cắt lỗ sát hơn mức đó dễ bị quét nhầm. *Vì sao:* Cắt lỗ nằm trong biên độ dao động bình thường của một phiên sẽ bị chạm bởi nhiễu chứ không phải vì ý tưởng sai.
  - Cắt lỗ thường đặt cách giá vào khoảng 1,5 đến 2 lần ATR. *Vì sao:* Cách xa vài lần biên độ thường ngày để chỉ những biến động bất thường mới chạm, đồng thời vẫn có mức thoát xác định.
  - ATR tăng nhanh: thị trường đang biến động hơn, nhiều người thường cân nhắc giảm khối lượng mua. *Vì sao:* Cắt lỗ phải xa hơn thì cùng số tiền rủi ro chỉ mua được ít cổ phiếu hơn.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Các Turtle Traders của Richard Dennis và William Eckhardt**: Dùng N (biên độ dao động trung bình thật) để cắt lỗ ở 2N và đặt đơn vị vị thế bằng 1% vốn chia cho N. *Vì sao:* Mỗi vị thế chịu cùng một mức rủi ro bằng tiền dù cổ phiếu biến động mạnh hay yếu. *Nguồn:* Curtis Faith, Way of the Turtle (2007). *Lưu ý:* N của họ là trung bình 20 ngày, ứng dụng dùng ATR 14 của Wilder, nên con số hơi khác. Ở Việt Nam, biên độ ±7% (HOSE) làm giá có thể nhảy cóc qua mức cắt lỗ; cổ phiếu mua về tài khoản vào chiều T+2 nên lỗ trong 2 phiên đầu chưa thoát được.
  - **J. Welles Wilder (tác giả chỉ báo)**: Tạo ra biên độ thật (true range) gồm cả khoảng trống giá giữa hai phiên, rồi lấy trung bình 14 phiên làm ATR. *Vì sao:* Biên độ cao-thấp trong một phiên bỏ sót những cú nhảy cóc qua đêm, vốn là một phần rủi ro thật của người giữ cổ phiếu. *Nguồn:* J. Welles Wilder, New Concepts in Technical Trading Systems (1978). *Lưu ý:* Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư.
- **Bẫy hay gặp:** ATR chỉ nói dao động rộng bao nhiêu, không nói giá sẽ lên hay xuống.

### 💰 Dòng tiền

**Khối lượng và KL SMA 20**
- **Là gì:** số cổ phiếu khớp mỗi phiên. KL SMA 20 là khối lượng trung bình 20 phiên gần nhất, dùng làm mốc so sánh.
- **Trên biểu đồ:** một tầng riêng: cột xám là khối lượng từng phiên, đường cam là trung bình 20 phiên.
- **Cách đọc:**
  - Cột cao hơn hẳn đường cam (ví dụ gấp 2 lần): phiên đó có nhiều người quan tâm. *Vì sao:* So với mức trung bình của chính mã đó mới biết phiên này bất thường hay không; khối lượng tuyệt đối khác nhau rất nhiều giữa các mã.
  - Giá tăng kèm khối lượng cao: đà tăng có nhiều tiền ủng hộ. Giá tăng nhưng khối lượng thấp: đà tăng yếu, dễ không bền. *Vì sao:* Tổ chức lớn mua với khối lượng lớn nên để lại dấu vết trong khối lượng; giá nhích lên khi ít người mua thì dễ bị đảo ngược.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **William O’Neil (nhà đầu tư, người sáng lập Investor’s Business Daily)**: Mua khi giá phá điểm mua (pivot) với khối lượng cao hơn mức trung bình ít nhất 40 đến 50%, và cắt mọi lệnh lỗ ở mức 7 đến 8%. *Vì sao:* Khối lượng cao khi phá điểm mua cho thấy các tổ chức đang mua thật; mức lỗ nhỏ cố định giữ cho tài khoản còn sống để làm lại. *Nguồn:* William O’Neil, How to Make Money in Stocks (xuất bản lần đầu 1988, phương pháp CAN SLIM). *Lưu ý:* Ông so với khối lượng trung bình 50 ngày, ứng dụng dùng trung bình 20 ngày. Ở Việt Nam, biên độ ±7% (HOSE) có thể làm giá nhảy cóc qua mức cắt lỗ 7 đến 8%, và cổ phiếu mới mua chưa bán được trong 2 phiên đầu.
- **Bẫy hay gặp:** khối lượng đột biến vào ngày đáo hạn, cơ cấu chỉ số hoặc tin lớn có thể không phản ánh dòng tiền bình thường.

**OBV**
- **Là gì:** cộng dồn khối lượng: phiên giá tăng thì cộng khối lượng vào, phiên giá giảm thì trừ đi. Cho thấy dòng tiền tích luỹ đi vào hay đi ra.
- **Trên biểu đồ:** một tầng riêng, đường xanh dương; điều quan trọng là hướng đi, không phải con số tuyệt đối.
- **Cách đọc:**
  - OBV đi lên cùng giá: dòng tiền ủng hộ xu hướng tăng. *Vì sao:* Giá tăng mà khối lượng cộng dồn cũng tăng nghĩa là các phiên tăng có nhiều giao dịch hơn các phiên giảm.
  - Giá tăng nhưng OBV đi ngang hoặc giảm: tiền không đi theo, đà tăng có thể yếu. *Vì sao:* Nếu các phiên giảm có khối lượng lớn hơn các phiên tăng thì có người đang bán ra trong lúc giá nhích lên.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Joseph Granville (tác giả chỉ báo)**: Khối lượng đi trước giá: OBV tăng trước khi giá tăng, nên theo dõi OBV để thấy dòng tiền sớm. *Vì sao:* Dòng tiền lớn phải vào trước rồi giá mới phản ánh, nên khối lượng là dấu hiệu sớm hơn giá. *Nguồn:* Joseph Granville, Granville’s New Key to Stock Market Profits (1963). *Lưu ý:* Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư.
- **Bẫy hay gặp:** con số OBV phụ thuộc điểm bắt đầu tính nên không so giữa các mã, chỉ so hướng đi của từng mã.

**MFI 14**
- **Là gì:** giống RSI nhưng có tính cả khối lượng, từ 0 đến 100: cao nghĩa là tiền đang đổ vào mạnh, thấp nghĩa là tiền đang rút ra.
- **Trên biểu đồ:** một tầng riêng, đường tím, hai vạch mờ ở 80 "Quá mua" và 20 "Quá bán".
- **Cách đọc:**
  - Trên 80: tiền vào đã rất mạnh, dễ quá nóng. Dưới 20: tiền rút mạnh, dễ có nhịp hồi. *Vì sao:* Cùng logic với RSI nhưng có thêm khối lượng, nên phản ánh cả sức nặng của các phiên tăng hay giảm.
  - MFI quay đầu khỏi vùng 80 hoặc 20 là lúc đáng chú ý, giống cách đọc RSI. *Vì sao:* Ở vùng cực, chỉ khi con số quay đầu mới cho thấy đà đã đổi chiều; chạm vùng cực mà chưa quay thì chưa có thông tin mới.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Gene Quong và Avrum Soudack (tác giả chỉ báo)**: Đưa khối lượng vào công thức kiểu RSI (RSI có trọng số khối lượng) để so tiền vào với tiền ra. *Vì sao:* Một phiên tăng trên khối lượng rất lớn và một phiên tăng trên khối lượng rất nhỏ không có cùng ý nghĩa, nhưng RSI thường coi hai phiên như nhau. *Nguồn:* Quong, G. và Soudack, A., "Volume-weighted RSI: money flow", Technical Analysis of Stocks & Commodities 7(3), 1989, tr. 76-77. *Lưu ý:* Đây là người tạo ra chỉ báo, không phải bằng chứng về thành tích đầu tư.
- **Bẫy hay gặp:** cũng như RSI, có thể ở vùng trên 80 rất lâu khi xu hướng tăng mạnh.

### 🧱 Mốc giá

**Hỗ trợ và kháng cự**
- **Là gì:** hỗ trợ là vùng giá bên dưới nơi giá từng dừng giảm và bật lên; kháng cự là vùng giá bên trên nơi giá từng dừng tăng và quay xuống. Hệ thống lấy các mức swing gần nhất; rỗng thì hiện "Chưa xác định".
- **Trên biểu đồ:** đường ngang đứt nét: xanh lá là hỗ trợ, đỏ là kháng cự, mỗi đường có nhãn (Hỗ trợ 1, Kháng cự 1...) và giá ở danh sách dưới biểu đồ.
- **Cách đọc:**
  - Giá đang tiến sát hỗ trợ: có thể bật lên, và là chỗ tham khảo để đặt cắt lỗ ngay bên dưới. *Vì sao:* Đó là vùng người mua từng xuất hiện; nếu giá thủng nó thì ý tưởng "sẽ bật lên" đã sai nên có lý do rõ ràng để thoát.
  - Giá đang tiến sát kháng cự: có thể gặp cản, là chỗ tham khảo để chốt lời một phần. *Vì sao:* Đó là vùng người bán từng xuất hiện, và những người mua kẹt ở vùng này có thể bán khi giá quay lại hoà vốn.
  - Giá vượt hẳn kháng cự với khối lượng lớn thì kháng cự đó thường trở thành hỗ trợ mới. *Vì sao:* Khối lượng lớn cho thấy phe mua đã hấp thụ hết lượng bán ở vùng đó, nên vùng giá ấy đổi vai.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Jesse Livermore (nhà đầu cơ)**: Hành động ở các điểm then chốt (pivotal points): điểm đảo chiều khi một xu hướng mới bắt đầu và điểm tiếp diễn trong vùng tích luỹ của một xu hướng đang có; chờ giá vượt điểm đó mới vào. *Vì sao:* Chờ giá xác nhận bằng cách đi theo hướng mình dự tính, thay vì đoán đỉnh đáy. *Nguồn:* Jesse Livermore, How to Trade in Stocks (1940). *Lưu ý:* Ứng dụng chọn mốc theo đỉnh đáy gần nhất (swing), không phải theo định nghĩa điểm then chốt của ông.
  - **Nicolas Darvas (vũ công, nhà đầu tư)**: Lý thuyết hộp (box theory): coi giá đi trong các hộp; mép trên hộp là kháng cự, mép dưới là hỗ trợ. *Vì sao:* Xem mốc giá như vùng mà thị trường đã chấp nhận, thay vì dựa vào dự đoán giá trị của cổ phiếu. *Nguồn:* Nicolas Darvas, How I Made $2,000,000 in the Stock Market (1960). *Lưu ý:* Ứng dụng chưa tự dựng hộp Darvas; mốc hiển thị chỉ là hỗ trợ và kháng cự gần nhất.
- **Bẫy hay gặp:** mốc giá là vùng chứ không phải một con số chính xác; giá có thể xuyên qua rồi quay lại.

**Fibonacci (đường thoái lui)**
- **Là gì:** các mức cắt đoạn đi từ đáy đến đỉnh gần nhất theo tỉ lệ 23,6%, 38,2%, 50%, 61,8%, 78,6%, thường là chỗ giá chững lại khi điều chỉnh (xem mục Hỗ trợ / Kháng cự & Fibonacci).
- **Trên biểu đồ:** các đường ngang đứt nét màu cam có nhãn Fib và giá, cùng hai đường xám là Đỉnh swing và Đáy swing.
- **Cách đọc:**
  - Giá đang điều chỉnh sau nhịp tăng: các mức 38,2%, 50%, 61,8% là chỗ thường được chú ý để cân nhắc mua lại. *Vì sao:* Vì nhiều người cùng nhìn các mức này nên đôi khi có lệnh tập trung ở đó; bản thân tỉ lệ không có cơ sở thống kê vững.
  - Giá giữ được mức 61,8% rồi bật lên là dấu hiệu nhịp điều chỉnh còn khỏe; thủng hẳn mức 78,6% là điều chỉnh sâu. *Vì sao:* Điều chỉnh càng sâu thì phần lớn nhịp tăng trước đã bị xoá, nên khả năng xu hướng cũ còn tiếp diễn càng giảm.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - Chưa có nhà đầu tư có thành tích kiểm chứng dùng làm quy tắc cốt lõi; bằng chứng yếu.
- **Bẫy hay gặp:** các mức này không có gì huyền bí: càng nhiều người cùng nhìn thì càng có tác dụng, thường chỉ được dùng khi trùng với hỗ trợ hoặc kháng cự khác.

**Đỉnh và đáy 52 tuần**
- **Là gì:** Giá cao nhất và thấp nhất của khoảng 250 phiên gần nhất (khoảng một năm giao dịch), cùng khoảng cách của giá hiện tại tới hai mức đó theo %.
- **Trên biểu đồ:** Hai đường ngang đứt nét "Đỉnh 52 tuần" và "Đáy 52 tuần" cùng các mốc giá khác; chưa đủ 250 phiên thì không vẽ và thẻ Trend Template ghi "Không đủ dữ liệu".
- **Cách đọc:**
  - Giá cách đỉnh 52 tuần càng gần (ví dụ dưới 25%) thì cổ phiếu càng đang ở vùng giá cao nhất của năm. *Vì sao:* Nghiên cứu của George và Hwang (2004) trên thị trường Mỹ thấy mức gần đỉnh 52 tuần dự báo lợi suất tương lai tốt hơn lợi suất quá khứ; chưa có kiểm chứng tương tự cho Việt Nam.
  - Giá cao hơn đáy 52 tuần ít nhất 30%: cổ phiếu đã thoát khỏi vùng đáy. *Vì sao:* Quy tắc của Minervini nhằm loại cổ phiếu mới hồi nhẹ từ đáy, vì nền xu hướng tăng chưa đủ chắc.
  - Giá vượt hẳn đỉnh 52 tuần: phía trên không còn mốc cản nào của chính năm đó. *Vì sao:* Những người mua ở đỉnh trong năm và đang kẹt chờ hoà vốn đã hết, nên lượng bán chờ sẵn phía trên ít đi.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Mark Minervini (nhà đầu tư, vô địch US Investing Championship 1997)**: Chỉ xét cổ phiếu cao hơn đáy 52 tuần ít nhất 30% và cách đỉnh 52 tuần tối đa 25% (hai trong tám tiêu chí của Trend Template). *Vì sao:* Cổ phiếu mạnh thường nằm gần đỉnh năm và đã đi xa khỏi đáy; cổ phiếu xa đỉnh hoặc sát đáy thường đang ở giai đoạn yếu. *Nguồn:* Mark Minervini, Trade Like a Stock Market Wizard (2013), nguồn thứ cấp về thành tích. *Lưu ý:* Ông tính theo 52 tuần lịch; ứng dụng dùng 250 phiên giao dịch gần nhất, xấp xỉ.
  - **Thomas George và Chuan-Yang Hwang (nghiên cứu học thuật, không phải nhà đầu tư)**: Xếp cổ phiếu theo tỉ lệ giá hiện tại trên đỉnh 52 tuần: nhóm gần đỉnh nhất có lợi suất những tháng sau cao hơn nhóm xa đỉnh. *Vì sao:* Nhà đầu tư neo vào mức đỉnh và phản ứng chậm với thông tin tốt khi giá tiến gần nó. *Nguồn:* George, T. và Hwang, C., "The 52-Week High and Momentum Investing", Journal of Finance 59(5), 2004, tr. 2145-2176. *Lưu ý:* Số liệu thị trường Mỹ từ 1963 đến 2001 và đã có nghiên cứu sau đó tranh luận; không phải bằng chứng cho Việt Nam.
- **Bẫy hay gặp:** Đỉnh và đáy tính trên giá đã điều chỉnh theo sự kiện quyền, nên có thể khác mức bạn nhớ; mã chưa đủ 250 phiên không có hai mức này.

**Kênh Donchian 20 và 55 phiên**
- **Là gì:** Đỉnh cao nhất và đáy thấp nhất của 20 hoặc 55 phiên TRƯỚC phiên hiện tại (không tính phiên hiện tại), cộng thêm đáy thấp nhất của 10 phiên trước. Giá vượt đỉnh là phá vỡ; thủng đáy là mức thoát.
- **Trên biểu đồ:** Bốn đường đè lên nến: đỉnh 20 phiên (hồng), đỉnh 55 phiên (tím), đáy 10 phiên (cam) và đáy 20 phiên (xanh ngọc). Mã chưa đủ phiên thì đường đó không vẽ và ghi "Không đủ dữ liệu".
- **Cách đọc:**
  - Giá đóng cửa vượt đỉnh 20 phiên (hoặc 55 phiên): phá vỡ lên, đúng điều kiện vào lệnh của Turtle. *Vì sao:* Giá vượt mức cao nhất của cả quãng gần đây nghĩa là phe mua đã hấp thụ hết lượng bán ở mức đó; kênh 55 phiên dài hơn nên phá vỡ ít xảy ra hơn nhưng vượt qua một quãng lịch sử dài hơn.
  - Giá thủng đáy 10 phiên (hoặc đáy 20 phiên): mức thoát theo Turtle. *Vì sao:* Lệnh theo xu hướng sai khi giá tự làm đáy mới của quãng gần đây; thoát theo mức định sẵn tránh giữ lệnh lỗ vì hy vọng.
  - Kênh hẹp lại là giá đang đi ngang; kênh rộng ra là biên độ đang lớn. *Vì sao:* Khoảng cách giữa đỉnh và đáy của quãng gần đây chính là biên độ của giai đoạn đó.
- **Nhà đầu tư lão luyện dùng thế nào, vì sao:**
  - **Richard Donchian (người phát minh)**: Quy tắc 4 tuần: mua khi giá vượt đỉnh của 4 tuần trước, bán khi thủng đáy của 4 tuần trước (khoảng 20 phiên ngày). *Vì sao:* Đi theo xu hướng bằng một quy tắc cơ học dựa trên giá, không cần dự đoán. *Nguồn:* Richard L. Weissman, Mechanical Trading Systems (Wiley), mục Donchian's channel breakout; MarketCalls: Richard Donchian's 4-week rule; nguồn thứ cấp. *Lưu ý:* Đây là người phát minh, không phải bằng chứng về thành tích. Các nguồn mô tả cách thoát của ông không giống nhau.
  - **Turtle Traders của Richard Dennis và William Eckhardt (cách dùng)**: Hệ thống 1: vào khi giá vượt đỉnh 20 ngày, thoát khi thủng đáy 10 ngày. Hệ thống 2: vào ở đỉnh 55 ngày, thoát ở đáy 20 ngày. *Vì sao:* Phá vỡ kênh cho điểm vào và điểm thoát định sẵn mà không cần dự đoán; kết hợp với đơn vị vị thế theo ATR để mỗi lệnh chịu cùng một mức rủi ro. *Nguồn:* Curtis Faith, Way of the Turtle (2007); The Original Turtle Trading Rules (turtletrader.com). *Lưu ý:* Họ giao dịch hợp đồng tương lai, không phải cổ phiếu Việt Nam. Kênh của ứng dụng tính trên giá cao và thấp của các phiên trước, khớp cách xét phá vỡ của họ. Ở Việt Nam, biên độ ±7% (HOSE) có thể làm giá nhảy cóc qua mức vào hoặc thoát, và cổ phiếu mua về tài khoản vào chiều T+2 nên không thoát được ngay trong 2 phiên đầu.
- **Bẫy hay gặp:** Khi giá đi ngang, giá liên tục phá lên rồi quay lại (phá vỡ giả), và lệnh đặt ở mức phá vỡ có thể khớp ở giá xấu hơn vì khoảng trống hoặc biên độ giá.

---

## Ứng viên mô hình: Cốc tay cầm

Dưới biểu đồ chỉ báo (trang Thị trường và trang Dòng thời gian mã) có thể có thẻ **"Ứng viên mô hình: Cốc tay cầm"**. Đây là một **phép dò hình dạng giá**, không phải tín hiệu mua, và **không có điểm số hay "độ tin cậy %"**: không có con số nào như vậy được hiệu chỉnh.

**Hình dạng:** giá giảm từ một đỉnh (đỉnh trái) xuống một đáy rồi hồi dần về gần đỉnh cũ (vành phải), tạo thành chiếc cốc; sau đó có một nhịp lùi ngắn (tay cầm) rồi giá tiến sát mức vành phải. **Pivot** là đỉnh của vành phải, mức giá cần vượt để mô hình được coi là bắt đầu chạy.

**Thẻ cho bạn thấy gì**
- **Số đo:** cốc dài bao nhiêu tuần và sâu bao nhiêu %, tay cầm dài bao nhiêu phiên và sâu bao nhiêu %, pivot, giá còn cách pivot bao nhiêu %, khối lượng nến cuối so với trung bình 50 phiên.
- **Từng điều kiện đạt (✓) hay không (✗)** kèm giá trị đo được. Điều kiện bắt buộc phải đạt hết thì thẻ mới hiện; điều kiện ghi "tham khảo" (khối lượng) có thể ✗.
- **Trạng thái:** *Đang hình thành*, *Sát pivot* (cách pivot không quá 3%), *Vượt pivot, chưa có khối lượng xác nhận*, *Vượt pivot kèm khối lượng*. **Chưa vượt pivot kèm khối lượng thì chưa xác nhận.**
- **Nếu sai:** "−X% từ pivot tới điểm cắt lỗ". Cắt lỗ đặt **ngay dưới đáy tay cầm** (thấp hơn 1%). Kèm **mục tiêu đo** = pivot cộng độ sâu của cốc, và **lãi/rủi ro theo R** (R là khoảng cách pivot tới cắt lỗ). Đây là hình học xác định từ nến, **không phải dự báo** và không phải xác suất thắng.
- Ô **"Vẽ cốc, pivot, cắt lỗ và mục tiêu trên biểu đồ"**: vẽ đoạn cốc (xanh da trời), đoạn tay cầm (cam) và ba đường ngang có nhãn %. Biểu đồ chỉ có khi đã chọn một nhóm chỉ báo (trang Thị trường) hoặc ngay khi trang mở (Dòng thời gian).

**Ngưỡng mặc định (có thể chỉnh, KHÔNG phải luật của một tác giả nào)**

| Điều kiện | Mặc định | Độ chắc của nguồn |
|---|---|---|
| Độ dài cốc | 7–65 tuần | O'Neil qua bảng so sánh của Bulkowski: kiểm chứng một phần |
| Độ sâu cốc | 12–33% | cùng nguồn: kiểm chứng một phần (40–50% chỉ được chấp nhận ở thị trường giá xuống, ở đây không dùng) |
| Vành phải | hồi ≥ 90% đỉnh trái | tự đặt |
| Tay cầm | 5–25 phiên, nằm nửa trên của cốc | độ dài "thường 1–2 tuần" theo nguồn; còn lại tự đặt |
| Độ sâu tay cầm | 8–15% | **chưa kiểm chứng** |
| Khối lượng phá vỡ | từ 1,4 lần trung bình 50 phiên | **chưa kiểm chứng** (hay gặp là +40–50%) |
| Sát pivot | cách pivot ≤ 3% | **chưa kiểm chứng**, tự đặt |
| Đã chạy xa | trên pivot quá 5% thì không còn là ứng viên | **chưa kiểm chứng**, tự đặt |

**Ai dùng và vì sao**
- **William J. O'Neil** (người mô tả mô hình, sáng lập Investor's Business Daily). *Cách dùng:* mua khi giá vượt pivot của tay cầm sau một đợt tăng trước đó, với khối lượng tăng rõ. *Vì sao:* người mua trước đó đã được "dọn" ở nhịp cốc và tay cầm nên nguồn cung phía trên mỏng đi. *Nguồn:* William J. O'Neil, *How to Make Money in Stocks* (1988, nhiều lần tái bản); các số đo cốc 7–65 tuần và sâu 12–33% lấy qua bảng so sánh trong sách của Thomas Bulkowski (nguồn thứ cấp). *Lưu ý:* ứng dụng KHÔNG kiểm điều kiện "đã tăng mạnh trước đó" và không dùng bộ lọc cơ bản của ông; thị trường Việt Nam có biên độ ±7% (HOSE), T+2,5 và nhà đầu tư cá nhân chiếm nhiều, nên các ngưỡng gốc chưa chắc phù hợp.
- **Andrew Lo, Harry Mamaysky, Jiang Wang** (nghiên cứu học thuật). *Điều họ làm:* dùng thuật toán để dò các hình dạng giá thay vì nhìn bằng mắt. *Kết quả:* các hình dạng này mang thêm một lượng thông tin **khiêm tốn**, không phải lợi thế lớn. *Nguồn:* "Foundations of Technical Analysis", *Journal of Finance* 55(4), 2000, tr. 1705 đến 1765 (NBER Working Paper 7613). *Vì sao ghi ở đây:* để biết đây là cơ sở cho việc dò bằng thuật toán, không phải lời hứa về lợi nhuận.

**Giới hạn cần nhớ**
- **Không có thống kê tỉ lệ thành công hay thất bại** của mô hình này trong tài liệu này; chưa có phép kiểm lại (back-test) trên dữ liệu Việt Nam được công bố ở đây. Đừng đọc thẻ như một xác suất.
- Dò trên giá **đã điều chỉnh**; khi nguồn chỉ có giá chưa điều chỉnh (dự phòng) thì không hiện thẻ, vì chia tách hay thưởng cổ phiếu có thể tạo ra một "chiếc cốc" giả.
- Chỉ báo **tối đa một ứng viên** và chỉ nhìn các phiên đã đóng cửa (nến hôm nay trước 15:00 bị bỏ).
- Hình dạng là phán đoán chủ quan: người khác vẽ cốc khác. Thẻ chỉ nói "theo ngưỡng mặc định này thì có".

## Cách đọc kết hợp 5 bước

```
Bước 1: ADX → Thị trường có xu hướng hay đi ngang?
  └─ ADX > 25 → Có xu hướng → dùng Trend Following
  └─ ADX < 20 → Đi ngang → dùng Mean Reversion

Bước 2: EMA + DI → Hướng đi?
  └─ EMA20 > EMA50 + (+DI > -DI) → Xu hướng TĂNG
  └─ EMA20 < EMA50 + (-DI > +DI) → Xu hướng GIẢM

Bước 3: RSI / Stochastic → Timing?
  └─ RSI pullback về 40–50 rồi bật lên → MUA
  └─ Stochastic < 20 + %K cắt lên %D → MUA

Bước 4: OBV / MFI → Dòng tiền ủng hộ?
  └─ OBV tăng + MFI chưa quá mua → Xác nhận ✅
  └─ OBV giảm hoặc MFI > 80 → Cẩn trọng ⚠️

Bước 5: ATR → Đặt SL ở đâu?
  └─ SL = Entry - 2 × ATR (swing trading)
```

---

## Ví dụ thực tế

Tra mã **FPT**, kết quả:
- ADX = 35 (trending) + +DI > -DI → hướng tăng, xu hướng rõ
- EMA20 > EMA50 → xác nhận uptrend
- RSI = 45 → chưa quá mua, còn room tăng
- OBV rising + MFI = 55 → dòng tiền vào ✅
- ATR = 1,200đ → SL = Entry - 2,400đ

→ Tín hiệu tổng hợp: **"Mua"** → Click **"Tạo Trade Plan từ gợi ý"**

---

## Tín hiệu tổng hợp

10 chỉ báo cùng "bỏ phiếu":

| Tín hiệu | Điều kiện |
|-----------|-----------|
| **Mua mạnh** | ≥ 6 phiếu bullish |
| **Mua** | ≥ 4 phiếu bullish, ≤ 3 bearish |
| **Chờ** | Không rõ ràng |
| **Bán** | ≥ 4 phiếu bearish, ≤ 3 bullish |
| **Bán mạnh** | ≥ 6 phiếu bearish |

---

## Điểm Confluence (0-100)

Hệ thống tính điểm tổng hợp dựa trên trọng số 5 nhóm chỉ báo:

| Nhóm | Trọng số | Chỉ báo |
|------|----------|---------|
| Xu hướng | 30% | EMA trend, ADX + DI direction |
| Động lượng | 25% | RSI, MACD, Stochastic (giá trị raw) |
| Khối lượng | 20% | OBV, MFI, Volume ratio |
| Biến động | 15% | Bollinger signal, ATR% |
| Vị trí giá | 10% | Bollinger %B |

- **> 60**: Tín hiệu tích cực (bullish confluence)
- **< 40**: Tín hiệu tiêu cực (bearish confluence)
- **40-60**: Trung tính, chờ tín hiệu rõ hơn

---

## Trạng thái thị trường

Dựa trên ADX(14), hệ thống tự động phân loại:

| Trạng thái | ADX | Chiến lược gợi ý |
|------------|-----|-------------------|
| **Xu hướng rất mạnh** | ≥ 40 | Trend Following (mạnh) |
| **Có xu hướng** | 25-40 | Trend Following |
| **Đi ngang** | < 25 | Mean Reversion |

---

## Phát hiện phân kỳ (Divergence)

Hệ thống tự động phát hiện phân kỳ giữa giá và RSI/MACD:

- **Phân kỳ tăng (Bullish)**: Giá tạo đáy thấp hơn, nhưng RSI/MACD tạo đáy cao hơn → tín hiệu đảo chiều tăng
- **Phân kỳ giảm (Bearish)**: Giá tạo đỉnh cao hơn, nhưng RSI/MACD tạo đỉnh thấp hơn → tín hiệu đảo chiều giảm

Bộ lọc giảm tín hiệu sai: swing points cách nhau ≥ 5 phiên, chênh lệch giá ≥ 0.5%.

---

## Hỗ trợ / Kháng cự & Fibonacci

- **Hỗ trợ**: 3 mức swing low gần nhất dưới giá hiện tại
- **Kháng cự**: 3 mức swing high gần nhất trên giá hiện tại
- **Fibonacci**: thẻ trong khối 🧱 Mốc giá hiển thị 5 mốc retracement (23.6% / 38.2% / 50% / 61.8% / 78.6%) tính từ swing high/low gần nhất, kèm chính swing high/low bên dưới. Extension (127.2%, 161.8%) backend có tính nhưng chưa hiển thị. Thiếu dữ liệu thì thẻ ghi "Không đủ dữ liệu"

---

## Gợi ý giao dịch

Khi đủ dữ liệu, hệ thống gợi ý:
- **Entry**: Mức hỗ trợ gần nhất
- **Stop-Loss**: Lấy mức **cao hơn** (chặt hơn) giữa hỗ trợ thứ 2 và **Entry − 2 × ATR(14)**, rồi giữ khoảng cách tới giá vào trong **từ 1 đến 2 × ATR**: hỗ trợ thứ 2 sát giá vào hơn 1 ATR thì cắt lỗ bị kéo ra **Entry − 1 × ATR** (cắt lỗ quá sát sẽ cho R:R "đẹp" giả). Ngay dưới mức cắt lỗ có ghi cách tính: *theo hỗ trợ thứ 2*, *theo 2×ATR*, *tối thiểu 1×ATR* (hỗ trợ quá gần nên bị kéo ra) hoặc *−5% dưới hỗ trợ* (khi không có ATR và không có hỗ trợ thứ 2). Cắt lỗ không hợp lệ (không dương, hoặc không thấp hơn giá vào) thì **không có gợi ý**.
- **Target**: Mức kháng cự gần nhất
- **R:R**: Tỷ lệ reward/risk. Dưới 1:1 hệ thống ghi chú **"R:R thấp — không đủ hấp dẫn"**: rủi ro lớn hơn lợi nhuận kỳ vọng, cân nhắc bỏ qua hoặc đổi điểm vào.

Nhấn **"Tạo Trade Plan từ gợi ý"** → chuyển đến trang Kế hoạch với thông tin đã điền sẵn.

## Setup Pullback và lịch sử của nó

Dưới biểu đồ và khối phân tích có khối **"Setup Pullback trên <mã>"**. Nó trả lời một câu hỏi hẹp: *khi setup này xuất hiện trên mã này trong quá khứ, chuyện gì xảy ra sau đó, nếu áp dụng luật thị trường Việt Nam thực tế?* Đây không phải khuyến nghị và không xếp hạng mã.

**Setup Pullback là gì?** Trong xu hướng tăng (EMA20 nằm trên EMA50, ADX từ 20), giá hồi về sát EMA20 (thấp nhất phiên cách EMA20 không quá 1 ATR), đóng cửa trên EMA20 và RSI nằm trong 40–55. Chỉ tính **phiên đầu tiên** điều kiện đúng; luật giống hệt nhau cho mọi mã, không chỉnh theo từng mã.

**Trong giờ giao dịch:** nến của hôm nay còn đang hình thành nên bị bỏ qua cho tới 15:00 giờ Việt Nam (kết quả tính trước giờ đó được giữ tới khoảng 15:10, nên nến đã đóng có thể hiện chậm tới 10 phút); tín hiệu chỉ được tính trên các phiên đã đóng cửa, nên một nến dở dang không tạo ra tín hiệu "đang kích hoạt".

**Một lệnh mô phỏng diễn ra thế nào?**
- Vào ở **giá mở cửa phiên sau tín hiệu**. Bỏ qua lệnh nếu phiên tín hiệu đã đóng ở trần/sàn, hoặc giá mở phiên vào đã sát trần (mua không khớp được).
- Cắt lỗ = vào − 2 × ATR; mục tiêu = vào + 2R (R là khoảng cách vào–cắt lỗ). Hết **20 phiên** thì bán ở giá đóng cửa.
- **T+2,5:** cổ phiếu mua ở phiên đầu chưa bán được trong 3 phiên đầu. Nếu chạm cắt lỗ hoặc mục tiêu trong 3 phiên đó, mô phỏng thoát ở **giá mở phiên thứ 4** (thận trọng, không phải chiều phiên thứ 3).
- Từ phiên thứ 4: mở cửa đã dưới cắt lỗ thì bán ở giá mở; chạm cắt lỗ thì bán ở cắt lỗ; cùng phiên chạm cả cắt lỗ và mục tiêu thì tính **cắt lỗ**; chạm mục tiêu thì bán ở mục tiêu.
- Phiên **khoá sàn** (không bán được) thì dời tới giá mở phiên đầu tiên không khoá. Không chồng lệnh: đang giữ lệnh thì bỏ tín hiệu mới.
- Đã trừ **phí 0,15% mỗi chiều, thuế bán 0,1%, trượt giá 0,1% mỗi chiều**.

**Khối này hiện gì?**
- **Đang kích hoạt** (đứng đầu): tín hiệu trong 3 phiên gần nhất với vào lệnh, cắt lỗ, mục tiêu. Nếu chưa tới phiên vào lệnh, vào lệnh là **ước tính theo giá đóng cửa**; nếu lệnh mô phỏng đang mở thì ghi "đang mở, ngày thứ k". Các mức này theo **luật của setup** (vào giá mở cửa phiên sau, cắt lỗ 2×ATR, mục tiêu 2R) nên **khác với thẻ "Gợi ý giao dịch"** ở phần trên, vốn dựa trên hỗ trợ/kháng cự: hai phương pháp khác nhau, không phải mâu thuẫn.
- Một dòng đếm: *"N tín hiệu, M lệnh hoàn tất trong 3 năm."*
- **Tỉ lệ thắng và EV (R) chỉ hiện khi có từ 30 lệnh đã đóng.** Ở hai mã đã đo (VNM, FPT) chỉ có 9 và 7 lệnh trong 3 năm, nên nhiều mã sẽ ghi *"Chưa đủ mẫu để thống kê riêng mã này (cần ≥ 30)"*. Hãy xem đó là số lần xuất hiện để tham khảo, không phải xác suất. Khi đủ 30 lệnh: tỉ lệ thắng kèm khoảng Wilson 95%, EV kèm khoảng tin cậy t 95%; thắng nghĩa là R ròng > 0.
- **Tỉ lệ nền VN30** (chỉ cho mã thuộc VN30, dưới dòng đếm): vì mỗi mã chỉ có vài lệnh, hệ thống gộp lệnh đã đóng của **cả rổ VN30** thành một con số tham khảo: *"Tỉ lệ nền VN30 (k mã, n lệnh, m tháng): thắng X% (95% xấp xỉ: a – b%), kỳ vọng ±y R (95% xấp xỉ: c – d)"*. Đây là tỉ lệ của **rổ**, **không phải bằng chứng riêng của mã đang xem**: nó trả lời "setup này nhìn chung có kỳ vọng dương hay không", không phải "mã này sẽ thắng bao nhiêu". Khoảng 95% là **xấp xỉ**, tính bằng cách lấy mẫu lại theo từng tháng (các lệnh cùng tháng chịu chung bối cảnh thị trường nên không coi là độc lập); lệnh có thể kéo dài qua nhiều tháng nên khoảng vẫn có thể hẹp hơn thực tế. Số liệu mô tả quá khứ, rổ hiện tại gồm các mã còn niêm yết (thiên lệch người sống sót). Chỉ hiện khi rổ đủ dữ liệu (từ 15 mã, 30 lệnh, 24 tháng); chưa đủ thì ghi lý do, và rổ đầy dần sau vài ngày giao dịch đầu tiên. Số này **không được điền sẵn** vào form kế hoạch.
- Hệ thống **không liệt kê từng lệnh** để tránh nhìn vài kết quả rồi tự suy ra tỉ lệ thắng.
- Mã chưa có đủ lịch sử giá **đã điều chỉnh** (dưới 250 phiên, có nến chưa điều chỉnh, hoặc nến cuối cũ hơn 10 ngày) thì chỉ hiện một dòng lý do và không có số liệu.

**Giới hạn cần nhớ**
- Giá đã điều chỉnh theo sự kiện quyền nên mức giá cũ khác mức giao dịch thật lúc đó.
- Mã đã hủy niêm yết không có trong dữ liệu (thiên lệch người sống sót).
- HOSE không có lệnh dừng, nên khớp đúng giá cắt lỗ là lạc quan hơn thực tế.
- **Kết quả quá khứ, không đảm bảo tương lai.**
