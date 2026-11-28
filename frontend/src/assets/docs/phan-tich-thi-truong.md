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
- **Cách đọc:** cắt lỗ là mức giá nếu chạm thì nhận sai và thoát. Chốt lời xa giá vào hơn cắt lỗ thường được xem là đáng cân nhắc: vào 100, cắt lỗ 95 (rủi ro 5), chốt lời 110 (lãi 10) là lãi gấp đôi rủi ro.
- **Điểm Confluence:** con số 0 đến 100 cho biết bao nhiêu chỉ báo cùng nghiêng về một phía. Trên 60 nghiêng về mua, dưới 40 nghiêng về bán, giữa là chưa đồng thuận. Ví dụ 72 điểm: nhiều chỉ báo đang cùng chiều.
- **Bẫy hay gặp:** đây là gợi ý để so với kế hoạch của bạn, không phải lệnh phải làm theo; điểm cao chỉ nói các chỉ báo đồng thuận trên dữ liệu quá khứ.

### 📈 Xu hướng

**EMA 20 và EMA 50 (đường trung bình)**
- **Là gì:** đường trung bình của giá đóng cửa 20 và 50 phiên gần nhất, phiên gần đây được tính nặng hơn. Nó làm mượt biến động hằng ngày để thấy hướng đi chung.
- **Trên biểu đồ:** hai đường đè lên nến: EMA 20 màu xanh dương (nhanh hơn), EMA 50 màu cam (chậm hơn).
- **Cách đọc:** EMA 20 nằm trên EMA 50 và cả hai dốc lên là xu hướng ngắn hạn đang tăng; ngược lại là đang giảm. Giá nằm trên cả hai đường là phe mua đang giữ thế. Hai đường cắt nhau là lúc xu hướng có thể đổi chiều.
- **Bẫy hay gặp:** khi giá đi ngang, hai đường cắt nhau liên tục và cho tín hiệu giả; xem thêm ADX trước khi tin.

**ADX 14, +DI và −DI**
- **Là gì:** ADX đo xu hướng mạnh hay yếu (không cho biết hướng). +DI là lực của phe mua, −DI là lực của phe bán.
- **Trên biểu đồ:** một tầng riêng: ADX màu tím, +DI xanh lá, −DI đỏ, kèm vạch mờ ở mức 25 "Có xu hướng".
- **Cách đọc:** ADX từ 25 trở lên là có xu hướng rõ; dưới 25 là đi ngang hoặc chưa rõ hướng (ví dụ ADX 18). +DI trên −DI là lực tăng trội hơn, ngược lại là lực giảm trội hơn. ADX cao vẫn có thể là xu hướng giảm: nhìn +DI và −DI để biết hướng.
- **Bẫy hay gặp:** ADX là chỉ báo chậm; lên cao thường đã đi được một đoạn, và ADX giảm không có nghĩa là giá đảo chiều ngay.

Thẻ **Trạng thái thị trường** phân loại trend hay đi ngang kèm chiến lược gợi ý (xem mục Trạng thái thị trường).

### ⚡ Động lượng

**RSI 14**
- **Là gì:** con số từ 0 đến 100 so sánh lực tăng và lực giảm của 14 phiên gần nhất. Cao nghĩa là giá vừa tăng mạnh, thấp nghĩa là vừa giảm mạnh.
- **Trên biểu đồ:** một tầng riêng, đường tím giữa hai vạch mờ: 70 "Quá mua" ở trên, 30 "Quá bán" ở dưới.
- **Cách đọc:** trên 70 là giá đã tăng nóng, dễ điều chỉnh (ví dụ RSI 72). Dưới 30 là giá đã giảm sâu, dễ hồi. Từ 30 đến 70 là chưa quá nóng cũng chưa quá lạnh. Nên chờ RSI quay đầu khỏi vùng nóng/lạnh rồi mới coi là tín hiệu.
- **Bẫy hay gặp:** trong xu hướng tăng mạnh RSI có thể ở trên 70 rất lâu; "quá mua" không có nghĩa là phải bán ngay.

**MACD 12 26 9**
- **Là gì:** so sánh hai đường trung bình (nhanh và chậm) để thấy đà tăng hay giảm đang mạnh lên hay yếu đi.
- **Trên biểu đồ:** một tầng riêng: đường MACD xanh dương, đường tín hiệu (Signal) cam, và các cột xám (Histogram) là khoảng cách giữa hai đường.
- **Cách đọc:** MACD cắt lên trên đường tín hiệu là đà tăng đang lên; cắt xuống dưới là đà giảm đang lên. Cột Histogram cao dần là đà mạnh lên, thấp dần về 0 là đà yếu đi. MACD trên mức 0 là xu hướng chung nghiêng về tăng.
- **Bẫy hay gặp:** MACD phản ứng chậm hơn giá và hay cho tín hiệu giả khi giá đi ngang.

**Stochastic 14 3**
- **Là gì:** cho biết giá đóng cửa đang nằm ở đâu trong biên độ cao-thấp của 14 phiên gần nhất, từ 0 (sát đáy) đến 100 (sát đỉnh).
- **Trên biểu đồ:** một tầng riêng: %K xanh dương (nhanh), %D cam (chậm, là trung bình của %K), hai vạch mờ ở 80 "Quá mua" và 20 "Quá bán".
- **Cách đọc:** trên 80 là giá sát đỉnh biên độ gần đây, dưới 20 là sát đáy. %K cắt lên trên %D ở vùng dưới 20 thường được xem là tín hiệu hồi.
- **Bẫy hay gặp:** rất nhạy, hay đổi chiều liên tục; chỉ dùng để xác nhận cùng RSI hoặc xu hướng.

**Phân kỳ:** giá và RSI/MACD lệch hướng nhau là tín hiệu đảo chiều (xem mục Phát hiện phân kỳ).

### 📏 Biến động

**Bollinger 20 2**
- **Là gì:** một dải gồm đường trung bình 20 phiên ở giữa, cùng hai đường cách đều hai bên (2 lần độ lệch chuẩn). Phần lớn thời gian giá nằm trong dải.
- **Trên biểu đồ:** ba đường xám đè lên nến: đường giữa đậm hơn, hai đường ngoài nhạt hơn.
- **Cách đọc:** dải hẹp lại là giá đang đi hẹp, thường báo trước một nhịp chạy mạnh (chưa biết lên hay xuống). Dải mở rộng là biến động đang lớn. Giá chạm hoặc vượt đường trên là đang đi xa mức trung bình, dễ chững lại hoặc quay về giữa dải; đường dưới thì ngược lại.
- **Bẫy hay gặp:** khi xu hướng tăng mạnh, giá bám dọc đường trên là bình thường; chạm đường trên không tự động là tín hiệu bán.

**ATR 14**
- **Là gì:** biên độ dao động trung bình mỗi phiên trong 14 phiên gần nhất, tính bằng đồng (không cho biết hướng).
- **Trên biểu đồ:** một tầng riêng, đường xanh ngọc; số càng lớn thì giá càng dao động mạnh.
- **Cách đọc:** ví dụ ATR là 2.000đ thì mỗi phiên giá thường chạy cỡ 2.000đ, cắt lỗ sát hơn mức đó dễ bị quét nhầm. Cắt lỗ thường đặt cách giá vào khoảng 1,5 đến 2 lần ATR (SL = Entry − 2 × ATR). ATR tăng nhanh là thị trường đang biến động hơn, nhiều người thường cân nhắc giảm khối lượng mua.
- **Bẫy hay gặp:** ATR chỉ nói dao động rộng bao nhiêu, không nói giá sẽ lên hay xuống.

### 💰 Dòng tiền

**Khối lượng và KL SMA 20**
- **Là gì:** số cổ phiếu khớp mỗi phiên. KL SMA 20 là khối lượng trung bình 20 phiên gần nhất, dùng làm mốc so sánh.
- **Trên biểu đồ:** một tầng riêng: cột xám là khối lượng từng phiên, đường cam là trung bình 20 phiên.
- **Cách đọc:** cột cao hơn hẳn đường cam (ví dụ gấp 2 lần) là phiên đó có nhiều người quan tâm. Giá tăng kèm khối lượng cao là đà tăng có nhiều tiền ủng hộ; giá tăng nhưng khối lượng thấp là đà tăng yếu, dễ không bền.
- **Bẫy hay gặp:** khối lượng đột biến vào ngày đáo hạn, cơ cấu chỉ số hoặc tin lớn có thể không phản ánh dòng tiền bình thường.

**OBV**
- **Là gì:** cộng dồn khối lượng: phiên giá tăng thì cộng khối lượng vào, phiên giá giảm thì trừ đi. Cho thấy dòng tiền tích luỹ đi vào hay đi ra.
- **Trên biểu đồ:** một tầng riêng, đường xanh dương; điều quan trọng là hướng đi, không phải con số tuyệt đối.
- **Cách đọc:** OBV đi lên cùng giá là dòng tiền ủng hộ xu hướng tăng. Giá tăng nhưng OBV đi ngang hoặc giảm là tiền không đi theo, đà tăng có thể yếu.
- **Bẫy hay gặp:** con số OBV phụ thuộc điểm bắt đầu tính nên không so giữa các mã, chỉ so hướng đi của từng mã.

**MFI 14**
- **Là gì:** giống RSI nhưng có tính cả khối lượng, từ 0 đến 100: cao nghĩa là tiền đang đổ vào mạnh, thấp nghĩa là tiền đang rút ra.
- **Trên biểu đồ:** một tầng riêng, đường tím, hai vạch mờ ở 80 "Quá mua" và 20 "Quá bán".
- **Cách đọc:** trên 80 là tiền vào đã rất mạnh, dễ quá nóng; dưới 20 là tiền rút mạnh, dễ có nhịp hồi. MFI quay đầu khỏi vùng 80 hoặc 20 là lúc đáng chú ý, giống cách đọc RSI.
- **Bẫy hay gặp:** cũng như RSI, có thể ở vùng trên 80 rất lâu khi xu hướng tăng mạnh.

### 🧱 Mốc giá

**Hỗ trợ và kháng cự**
- **Là gì:** hỗ trợ là vùng giá bên dưới nơi giá từng dừng giảm và bật lên; kháng cự là vùng giá bên trên nơi giá từng dừng tăng và quay xuống. Hệ thống lấy các mức swing gần nhất; rỗng thì hiện "Chưa xác định".
- **Trên biểu đồ:** đường ngang đứt nét: xanh lá là hỗ trợ, đỏ là kháng cự, mỗi đường có nhãn (Hỗ trợ 1, Kháng cự 1...) và giá ở danh sách dưới biểu đồ.
- **Cách đọc:** giá tiến sát hỗ trợ có thể bật lên, và là chỗ tham khảo để đặt cắt lỗ ngay bên dưới. Giá tiến sát kháng cự có thể gặp cản, là chỗ tham khảo để chốt lời một phần. Giá vượt hẳn kháng cự với khối lượng lớn thì kháng cự đó thường trở thành hỗ trợ mới.
- **Bẫy hay gặp:** mốc giá là vùng chứ không phải một con số chính xác; giá có thể xuyên qua rồi quay lại.

**Fibonacci (đường thoái lui)**
- **Là gì:** các mức cắt đoạn đi từ đáy đến đỉnh gần nhất theo tỉ lệ 23,6%, 38,2%, 50%, 61,8%, 78,6%, thường là chỗ giá chững lại khi điều chỉnh (xem mục Hỗ trợ / Kháng cự & Fibonacci).
- **Trên biểu đồ:** các đường ngang đứt nét màu cam có nhãn Fib và giá, cùng hai đường xám là Đỉnh swing và Đáy swing.
- **Cách đọc:** giá đang điều chỉnh sau nhịp tăng thì các mức 38,2%, 50%, 61,8% là chỗ thường được chú ý để cân nhắc mua lại. Giữ được mức 61,8% rồi bật lên là nhịp điều chỉnh còn khỏe; thủng hẳn mức 78,6% là điều chỉnh sâu.
- **Bẫy hay gặp:** các mức này không có gì huyền bí: càng nhiều người cùng nhìn thì càng có tác dụng, thường chỉ được dùng khi trùng với hỗ trợ hoặc kháng cự khác.

---

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
- **Stop-Loss**: Mức hỗ trợ thứ 2 hoặc -5%
- **Target**: Mức kháng cự gần nhất
- **R:R**: Tỷ lệ reward/risk

Nhấn **"Tạo Trade Plan từ gợi ý"** → chuyển đến trang Kế hoạch với thông tin đã điền sẵn.
