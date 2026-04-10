# PHÂN LOẠI CHỈ BÁO KỸ THUẬT — MỤC ĐÍCH — CÁCH SỬ DỤNG

---

## LOẠI 1: CHỈ BÁO XÁC ĐỊNH XU HƯỚNG (Trend Indicators)

**Mục đích chung**: Trả lời câu hỏi "Thị trường đang đi lên, đi xuống, hay đi ngang?"

---

### SMA (Simple Moving Average)

- **Mục đích**: Xác định xu hướng chính, lọc nhiễu giá.
- **Cách sử dụng**: Giá trên SMA → uptrend, dưới SMA → downtrend. Golden Cross (SMA ngắn cắt lên SMA dài) → MUA. Death Cross → BÁN.
- **Tham số phổ biến**: SMA(20) ngắn hạn, SMA(50) trung hạn, SMA(200) dài hạn.
- **Chiến lược phù hợp**: Swing Trading, Position Trading.

### EMA (Exponential Moving Average)

- **Mục đích**: Xác định xu hướng với phản ứng nhanh hơn SMA.
- **Cách sử dụng**: Tương tự SMA nhưng phản ứng nhạy hơn với giá gần. EMA(9) cắt lên EMA(21) → MUA ngắn hạn.
- **Tham số phổ biến**: EMA(9), EMA(12), EMA(21), EMA(26).
- **Chiến lược phù hợp**: Scalping, Day Trading, Swing Trading.

### VWAP (Volume Weighted Average Price)

- **Mục đích**: Xác định giá trung bình có trọng số khối lượng trong ngày — "fair value" intraday.
- **Cách sử dụng**: Giá trên VWAP → phe mua kiểm soát, thiên MUA. Giá dưới VWAP → phe bán kiểm soát, thiên BÁN. VWAP là nam châm — giá thường quay lại test.
- **Tham số**: Tự động tính, reset mỗi phiên.
- **Chiến lược phù hợp**: Scalping, Day Trading.

### ADX (Average Directional Index)

- **Mục đích**: Đo SỨC MẠNH xu hướng (không phải hướng).
- **Cách sử dụng**: ADX > 25 → xu hướng mạnh, dùng chiến lược Trend Following. ADX < 20 → sideway, dùng chiến lược Mean Reversion. +DI > -DI → hướng tăng; -DI > +DI → hướng giảm.
- **Tham số**: ADX(14).
- **Chiến lược phù hợp**: Swing, Position, Breakout (lọc), Mean Reversion (lọc ngược).

### Parabolic SAR

- **Mục đích**: Xác định xu hướng hiện tại + cung cấp trailing stop tự động.
- **Cách sử dụng**: Chấm dưới giá → uptrend (giữ MUA). Chấm trên giá → downtrend (giữ BÁN). Khi chấm đổi bên → tín hiệu đảo chiều. Dùng chấm SAR làm điểm stop loss.
- **Tham số**: AF bắt đầu 0.02, AF tối đa 0.20.
- **Chiến lược phù hợp**: Trend Following, Position Trading (trailing stop).

### Ichimoku Kinko Hyo

- **Mục đích**: Hệ thống "tất cả trong một" — xu hướng, hỗ trợ/kháng cự, momentum, tín hiệu mua/bán.
- **Cách sử dụng**: Giá trên mây → tăng, dưới mây → giảm, trong mây → sideway. Tenkan cắt Kijun → tín hiệu mua/bán. Mây xanh → bullish, mây đỏ → bearish. Chikou trên giá → xác nhận.
- **Tham số**: 9, 26, 52.
- **Chiến lược phù hợp**: Swing Trading, Position Trading.

### Donchian Channel

- **Mục đích**: Xác định xu hướng qua breakout đỉnh/đáy.
- **Cách sử dụng**: Giá phá dải trên (Highest High 20 phiên) → MUA. Giá phá dải dưới (Lowest Low 20 phiên) → BÁN. Nền tảng của hệ thống Turtle Trading.
- **Tham số**: 20 phiên.
- **Chiến lược phù hợp**: Position Trading, Breakout Trading.

---

## LOẠI 2: CHỈ BÁO ĐỘNG LƯỢNG (Momentum / Oscillators)

**Mục đích chung**: Trả lời câu hỏi "Xu hướng đang mạnh lên hay yếu đi? Giá đã quá cao hay quá thấp?"

---

### RSI (Relative Strength Index)

- **Mục đích**: Đo tốc độ và biên độ thay đổi giá → xác định quá mua/quá bán.
- **Cách sử dụng**:
  - **Trong thị trường sideway**: RSI > 70 → quá mua (BÁN); RSI < 30 → quá bán (MUA).
  - **Trong thị trường trending**: RSI 40-50 là vùng hỗ trợ (uptrend); RSI 50-60 là vùng kháng cự (downtrend).
  - **Phân kỳ**: Giá tạo đỉnh mới nhưng RSI không → sắp đảo chiều giảm.
- **Tham số**: RSI(14).
- **Chiến lược phù hợp**: Day Trading, Swing Trading, Mean Reversion, Momentum.

### MACD (Moving Average Convergence Divergence)

- **Mục đích**: Đo momentum xu hướng + tín hiệu mua/bán dựa trên sự hội tụ/phân kỳ MA.
- **Cách sử dụng**: MACD cắt lên Signal → MUA. MACD cắt xuống Signal → BÁN. Histogram tăng → momentum tăng. Histogram giảm → momentum yếu. Phân kỳ MACD với giá → cảnh báo đảo chiều.
- **Tham số**: (12, 26, 9).
- **Chiến lược phù hợp**: Day Trading, Swing Trading, Position Trading, Momentum.

### Stochastic Oscillator

- **Mục đích**: So sánh giá đóng cửa với phạm vi giá trong n phiên → xác định quá mua/quá bán chính xác hơn.
- **Cách sử dụng**: %K < 20 + cắt lên %D → MUA (quá bán hồi). %K > 80 + cắt xuống %D → BÁN (quá mua hạ). Trong trending mạnh, Stochastic có thể "dính" ở vùng quá mua/bán lâu → dùng để timing pullback.
- **Tham số**: (14, 3, 3) cho Slow Stochastic; (5, 3, 3) cho Scalping.
- **Chiến lược phù hợp**: Scalping, Swing Trading, Mean Reversion.

### Williams %R

- **Mục đích**: Giống Stochastic nhưng ngược chiều, đo vị trí giá so với range.
- **Cách sử dụng**: %R từ -80 đến -100 → quá bán → MUA. %R từ 0 đến -20 → quá mua → BÁN.
- **Tham số**: (14).
- **Chiến lược phù hợp**: Day Trading, Swing Trading.

### CCI (Commodity Channel Index)

- **Mục đích**: Đo độ lệch của giá so với trung bình thống kê → phát hiện cực đoan.
- **Cách sử dụng**: CCI > +100 → xu hướng tăng mạnh hoặc quá mua. CCI < -100 → xu hướng giảm mạnh hoặc quá bán. CCI vượt +100 rồi quay xuống → BÁN. CCI thoát -100 lên → MUA.
- **Tham số**: CCI(20).
- **Chiến lược phù hợp**: Mean Reversion, Swing Trading.

### ROC (Rate of Change)

- **Mục đích**: Đo phần trăm thay đổi giá → xếp hạng momentum giữa các CP.
- **Cách sử dụng**: ROC dương và tăng → momentum tăng. ROC âm và giảm → momentum giảm. Dùng để xếp hạng CP: mua top ROC, bán bottom ROC (Momentum Strategy).
- **Tham số**: ROC(12) hoặc ROC(26).
- **Chiến lược phù hợp**: Momentum Trading, Position Trading.

---

## LOẠI 3: CHỈ BÁO KHỐI LƯỢNG (Volume Indicators)

**Mục đích chung**: Trả lời câu hỏi "Dòng tiền có ủng hộ xu hướng hiện tại không?"

---

### OBV (On-Balance Volume)

- **Mục đích**: Theo dõi dòng tiền tích lũy qua khối lượng — phát hiện smart money.
- **Cách sử dụng**: OBV tăng cùng giá → xác nhận uptrend (dòng tiền ủng hộ). OBV giảm trong khi giá tăng → phân kỳ âm (smart money đang thoát, cẩn trọng). OBV tăng trong khi giá giảm → phân kỳ dương (smart money tích lũy, sắp tăng). OBV breakout trước giá → tín hiệu sớm.
- **Tham số**: Không có (tính tích lũy).
- **Chiến lược phù hợp**: Swing Trading, Position Trading, Momentum.

### MFI (Money Flow Index)

- **Mục đích**: "RSI có khối lượng" — đo áp lực mua bán dựa trên cả giá và volume.
- **Cách sử dụng**: MFI > 80 → quá mua (dòng tiền quá nóng). MFI < 20 → quá bán (dòng tiền rút cạn). Phân kỳ MFI mạnh hơn phân kỳ RSI vì có thêm yếu tố volume.
- **Tham số**: MFI(14).
- **Chiến lược phù hợp**: Day Trading, Swing Trading, Mean Reversion.

### CMF (Chaikin Money Flow)

- **Mục đích**: Đo áp lực mua/bán dựa trên vị trí giá đóng cửa trong range và khối lượng.
- **Cách sử dụng**: CMF > 0 (20 phiên) → áp lực mua chiếm ưu thế → ủng hộ MUA. CMF < 0 → áp lực bán chiếm ưu thế → ủng hộ BÁN. CMF càng xa 0 càng mạnh.
- **Tham số**: CMF(20).
- **Chiến lược phù hợp**: Swing Trading, xác nhận breakout.

### A/D Line (Accumulation/Distribution)

- **Mục đích**: Theo dõi tích lũy/phân phối dựa trên vị trí giá đóng cửa trong range.
- **Cách sử dụng**: A/D tăng → tích lũy (smart money mua). A/D giảm → phân phối (smart money bán). Phân kỳ A/D vs giá → cảnh báo sớm đảo chiều.
- **Tham số**: Không có.
- **Chiến lược phù hợp**: Swing, Position Trading.

### Volume Profile

- **Mục đích**: Cho thấy MỨC GIÁ NÀO có khối lượng giao dịch nhiều nhất → vùng hỗ trợ/kháng cự mạnh nhất.
- **Cách sử dụng**: POC (Point of Control) = giá có volume lớn nhất → hỗ trợ/kháng cự cực mạnh. Value Area (70% volume) = vùng giá "hợp lý". Giá ngoài Value Area → không bền vững, có xu hướng quay lại. HVN (High Volume Node) → hỗ trợ/kháng cự. LVN (Low Volume Node) → giá dễ chạy nhanh qua.
- **Tham số**: Session, Weekly, hoặc Custom range.
- **Chiến lược phù hợp**: Scalping, Day Trading, Breakout.

---

## LOẠI 4: CHỈ BÁO BIẾN ĐỘNG (Volatility Indicators)

**Mục đích chung**: Trả lời câu hỏi "Thị trường đang biến động mạnh hay yếu? Sắp có biến động lớn không?"

---

### Bollinger Bands

- **Mục đích**: Đo biến động giá + xác định vùng quá mua/bán động.
- **Cách sử dụng**:
  - **Mean Reversion (sideway)**: Giá chạm dải dưới → MUA; chạm dải trên → BÁN.
  - **Breakout (trending)**: Dải bóp hẹp (Squeeze) → chuẩn bị breakout mạnh. Giá phá dải trên + dải mở rộng → xu hướng tăng mạnh, KHÔNG bán.
  - **Kết hợp Keltner**: BB nằm trong Keltner Channel = TTM Squeeze → breakout cực mạnh sắp xảy ra.
- **Tham số**: SMA(20), 2 độ lệch chuẩn.
- **Chiến lược phù hợp**: Day Trading, Swing Trading, Mean Reversion, Breakout.

### ATR (Average True Range)

- **Mục đích**: Đo mức biến động trung bình → dùng để đặt SL và sizing. KHÔNG cho tín hiệu mua/bán.
- **Cách sử dụng**:
  - **Stop Loss**: SL = Entry ± k × ATR (k = 1.5 ngắn hạn, 2.0 trung hạn, 3.0 dài hạn).
  - **Position Sizing**: Khi ATR cao (biến động lớn) → giảm size. Khi ATR thấp → tăng size.
  - **Đánh giá điều kiện**: ATR tăng → thị trường sôi động. ATR giảm → thị trường trầm lắng, sắp có big move.
  - **Filter**: Chỉ giao dịch khi ATR đủ lớn để cover chi phí giao dịch.
- **Tham số**: ATR(14).
- **Chiến lược phù hợp**: TẤT CẢ (công cụ quản lý rủi ro nền tảng).

### Keltner Channel

- **Mục đích**: Tương tự Bollinger nhưng dùng ATR nên mượt hơn, ít giật.
- **Cách sử dụng**: Giá trên dải trên → uptrend mạnh. Giá dưới dải dưới → downtrend mạnh. Kết hợp với Bollinger → phát hiện Squeeze.
- **Tham số**: EMA(20), 2 × ATR(10).
- **Chiến lược phù hợp**: Breakout Trading, Trend Following.

---

## LOẠI 5: MÔ HÌNH NẾN NHẬT (Candlestick Patterns)

**Mục đích chung**: Trả lời câu hỏi "Tâm lý thị trường đang thay đổi như thế nào? Phe mua hay phe bán đang chiếm ưu thế?"

---

### Mô hình đảo chiều TĂNG

- **Mục đích**: Phát hiện phe mua đang giành lại quyền kiểm soát sau xu hướng giảm.
- **Cách sử dụng**: CHỈ có giá trị khi xuất hiện tại vùng hỗ trợ (Fibonacci, MA, Support ngang). Cần xác nhận bằng nến tiếp theo hoặc volume.
  - **Hammer** → Mua khi giá phá đỉnh nến Hammer, SL dưới bóng Hammer.
  - **Bullish Engulfing** → Mua ngay hoặc đợi pullback nhẹ, SL dưới đáy nến Engulfing.
  - **Morning Star** → Mua khi nến thứ 3 đóng cửa, SL dưới nến thứ 2.
- **Chiến lược phù hợp**: Tất cả, đặc biệt Swing Trading, Price Action.

### Mô hình đảo chiều GIẢM

- **Mục đích**: Phát hiện phe bán đang giành quyền kiểm soát sau xu hướng tăng.
- **Cách sử dụng**: CHỈ có giá trị tại vùng kháng cự. Cần xác nhận.
  - **Shooting Star** → Bán khi giá phá đáy nến, SL trên bóng nến.
  - **Bearish Engulfing** → Bán ngay hoặc đợi pullback, SL trên đỉnh nến.
  - **Evening Star** → Bán khi nến thứ 3 đóng cửa, SL trên nến thứ 2.
- **Chiến lược phù hợp**: Tất cả, đặc biệt Swing Trading.

### Mô hình do dự (Indecision)

- **Mục đích**: Phát hiện thị trường đang phân vân → chuẩn bị cho biến động tiếp theo.
- **Cách sử dụng**: Doji, Spinning Top tại vùng quan trọng → CHƯA vào lệnh, chờ nến xác nhận tiếp theo. Inside Bar → đặt pending order 2 bên, breakout bên nào vào bên đó.
- **Chiến lược phù hợp**: Breakout Trading, Price Action.

---

## LOẠI 6: MÔ HÌNH GIÁ (Chart Patterns)

**Mục đích chung**: Trả lời câu hỏi "Cấu trúc giá đang báo hiệu điều gì sắp xảy ra? Target giá ở đâu?"

---

### Mô hình đảo chiều (Head & Shoulders, Double Top/Bottom, Triple Top/Bottom)

- **Mục đích**: Phát hiện sự kết thúc của xu hướng hiện tại và bắt đầu xu hướng mới.
- **Cách sử dụng**:
  - Nhận diện mô hình → Chờ break neckline/support/resistance → Vào lệnh theo hướng break.
  - **Đo target**: Lấy chiều cao mô hình, chiếu từ điểm breakout.
  - **Volume**: Phải tăng đáng kể khi breakout → xác nhận.
  - **Retest**: Thường giá quay lại test neckline → cơ hội vào lệnh an toàn hơn.
- **Chiến lược phù hợp**: Swing Trading, Position Trading.

### Mô hình tiếp diễn (Flag, Pennant, Triangle, Wedge)

- **Mục đích**: Xác nhận xu hướng hiện tại sẽ tiếp tục sau giai đoạn nghỉ.
- **Cách sử dụng**:
  - Xu hướng mạnh + hình thành mô hình consolidation → Chờ breakout cùng chiều xu hướng → MUA/BÁN.
  - Flag/Pennant: Target = chiều dài "cán cờ" chiếu từ breakout.
  - Triangle: Target = chiều rộng nhất tam giác chiếu từ breakout.
  - **Volume**: Giảm dần trong consolidation, tăng đột biến khi breakout.
- **Chiến lược phù hợp**: Swing Trading, Breakout Trading, Momentum.

---

## LOẠI 7: HỖ TRỢ / KHÁNG CỰ & FIBONACCI

**Mục đích chung**: Trả lời câu hỏi "Giá sẽ dừng ở đâu? Nên vào lệnh ở mức giá nào?"

---

### Hỗ trợ / Kháng cự ngang

- **Mục đích**: Xác định mức giá mà cung/cầu tập trung → giá khó vượt qua.
- **Cách sử dụng**: Vẽ đường ngang tại các đỉnh/đáy lịch sử, vùng volume cao, số tròn. MUA tại hỗ trợ + nến đảo chiều. BÁN tại kháng cự + nến đảo chiều. Hỗ trợ bị phá → trở thành kháng cự (và ngược lại).
- **Chiến lược phù hợp**: TẤT CẢ.

### Pivot Points

- **Mục đích**: Hỗ trợ/kháng cự tính toán sẵn cho giao dịch trong ngày.
- **Cách sử dụng**: Giá trên PP → thiên MUA, target R1 rồi R2. Giá dưới PP → thiên BÁN, target S1 rồi S2. Giá phản ứng mạnh tại R1/S1 → vùng đảo chiều intraday.
- **Tham số**: Dựa trên HLC phiên trước.
- **Chiến lược phù hợp**: Scalping, Day Trading.

### Fibonacci Retracement

- **Mục đích**: Dự đoán vùng giá pullback (thoái lui) → điểm vào lệnh theo xu hướng.
- **Cách sử dụng**: Vẽ từ Swing Low đến Swing High (uptrend). Chờ giá pullback về 38.2% (nông), 50% (vừa), 61.8% (sâu). Vào lệnh MUA tại Fibonacci + nến xác nhận + volume.
- **Chiến lược phù hợp**: Swing Trading, Position Trading.

### Fibonacci Extension

- **Mục đích**: Xác định mức giá mục tiêu (target) → điểm chốt lời.
- **Cách sử dụng**: Sau khi vào lệnh, đặt TP tại 127.2% (conservative), 161.8% (moderate), 200%+ (aggressive). Partial TP: 50% tại 127.2%, 50% tại 161.8%.
- **Chiến lược phù hợp**: Swing Trading, Position Trading.

---

## LOẠI 8: PHÂN KỲ (Divergence)

**Mục đích chung**: Trả lời câu hỏi "Xu hướng hiện tại có đang suy yếu bên trong không?"

---

### Phân kỳ thường (Regular Divergence) → ĐẢO CHIỀU

- **Mục đích**: Phát hiện xu hướng sắp đảo chiều khi giá đi một hướng nhưng chỉ báo đi hướng ngược.
- **Cách sử dụng**:
  - **Bullish**: Giá tạo đáy THẤP hơn, RSI/MACD tạo đáy CAO hơn → sắp tăng. Vào MUA khi chỉ báo xác nhận (RSI vượt 30, MACD cắt Signal).
  - **Bearish**: Giá tạo đỉnh CAO hơn, RSI/MACD tạo đỉnh THẤP hơn → sắp giảm. Vào BÁN khi chỉ báo xác nhận.
- **Áp dụng với**: RSI, MACD, Stochastic, OBV, MFI, CCI.
- **Chiến lược phù hợp**: Swing Trading, Mean Reversion.

### Phân kỳ ẩn (Hidden Divergence) → TIẾP DIỄN

- **Mục đích**: Xác nhận xu hướng hiện tại sẽ tiếp tục sau pullback.
- **Cách sử dụng**:
  - **Bullish Hidden**: Giá tạo đáy CAO hơn, RSI tạo đáy THẤP hơn → uptrend tiếp tục. Vào MUA theo xu hướng.
  - **Bearish Hidden**: Giá tạo đỉnh THẤP hơn, RSI tạo đỉnh CAO hơn → downtrend tiếp tục.
- **Chiến lược phù hợp**: Trend Following, Swing Trading.

---

## LOẠI 9: PRICE ACTION & SMART MONEY

**Mục đích chung**: Trả lời câu hỏi "Institutional traders (cá mập) đang làm gì?"

---

### Price Action thuần túy

- **Mục đích**: Giao dịch chỉ dựa trên hành vi giá, không dùng chỉ báo.
- **Cách sử dụng**:
  - **Pin Bar tại S/R** → Vào lệnh ngược hướng pin, SL qua đuôi pin.
  - **Inside Bar** → Pending order 2 bên, breakout bên nào vào bên đó.
  - **Fakey** → False breakout của Inside Bar → tín hiệu đảo chiều cực mạnh.
  - **Break of Structure** → Xác nhận thay đổi xu hướng.
- **Chiến lược phù hợp**: Tất cả, đặc biệt Scalping, Day Trading.

### Smart Money Concepts (SMC)

- **Mục đích**: Đọc hành vi của institutional traders qua dấu vết trên chart.
- **Cách sử dụng**:
  - **Order Block** → Vùng nến cuối trước big move = vùng institutional entry → giá thường quay lại test → vào lệnh.
  - **Fair Value Gap (FVG)** → Khoảng trống giữa 3 nến = imbalance → giá sẽ quay lại lấp → vào lệnh tại FVG.
  - **Liquidity Sweep** → Giá quét qua đỉnh/đáy cũ rồi đảo chiều → trap retail traders.
  - **Premium/Discount** → Mua ở vùng Discount (dưới 50% Fibo), bán ở vùng Premium (trên 50%).
- **Chiến lược phù hợp**: Day Trading, Swing Trading.

---

## LOẠI 10: QUẢN LÝ RỦI RO (Risk Management)

**Mục đích chung**: Trả lời câu hỏi "Nên mua bao nhiêu? Dừng lỗ ở đâu? Chốt lời ở đâu?"

---

### ATR-Based Stop Loss & Position Sizing

- **Mục đích**: Đặt SL và tính size dựa trên biến động thực tế của CP.
- **Cách sử dụng**: SL = Entry ± k×ATR. Size = (Vốn × %Risk) / (k × ATR). Khi ATR cao → SL xa hơn → size nhỏ hơn (tự động điều chỉnh).
- **Chiến lược**: TẤT CẢ.

### Fixed % Risk

- **Mục đích**: Giới hạn thua lỗ mỗi lệnh ở mức cố định so với tổng vốn.
- **Cách sử dụng**: Mỗi lệnh tối đa lỗ 1-2% vốn. Tổng exposure tối đa 5-6% cùng lúc. Nếu lỗ 6-10% trong tháng → dừng lại.
- **Chiến lược**: TẤT CẢ.

### Trailing Stop (Parabolic SAR, EMA, Chandelier Exit)

- **Mục đích**: Bảo vệ lợi nhuận đang có bằng cách di chuyển SL theo giá.
- **Cách sử dụng**: Khi lệnh có lời → nâng SL lên theo SAR/EMA/Chandelier. Không bao giờ hạ SL ngược lại. Chấp nhận bị stopped out → giữ được phần lớn lợi nhuận.
- **Chiến lược**: Swing, Position, Trend Following.

### R:R Ratio

- **Mục đích**: Đảm bảo mỗi lệnh có kỳ vọng dương (expected value > 0).
- **Cách sử dụng**: Chỉ vào lệnh khi R:R ≥ 1:2. Với win rate 40% + R:R 1:3 → vẫn có lợi nhuận. Tính: R:R = (Target - Entry) / (Entry - SL).
- **Chiến lược**: TẤT CẢ.

---

## BẢNG TỔNG HỢP NHANH

| LOẠI | CHỈ BÁO | MỤC ĐÍCH CHÍNH | KHI NÀO DÙNG |
|------|---------|----------------|---------------|
| Xu hướng | SMA, EMA | Xác định hướng đi | Luôn luôn — bước đầu tiên |
| Xu hướng | VWAP | Fair value intraday | Day Trading, Scalping |
| Xu hướng | ADX | Đo sức mạnh trend | Lọc: trade trend hay mean reversion |
| Xu hướng | Ichimoku | All-in-one | Swing, Position |
| Momentum | RSI | Quá mua/quá bán | Timing vào lệnh |
| Momentum | MACD | Momentum + tín hiệu | Xác nhận xu hướng + timing |
| Momentum | Stochastic | Quá mua/quá bán nhạy | Scalping, mean reversion |
| Volume | OBV, CMF | Dòng tiền | Xác nhận — bước cuối trước vào lệnh |
| Volume | MFI | RSI + volume | Thay thế RSI khi cần thêm volume |
| Volume | Volume Profile | Vùng giá quan trọng | S/R dựa trên volume thực |
| Biến động | Bollinger | Biến động + S/R động | Mean reversion + Breakout |
| Biến động | ATR | Đo biến động | SL, position sizing — LUÔN DÙNG |
| Nến Nhật | Patterns | Tâm lý thị trường | Timing vào lệnh tại vùng quan trọng |
| Chart | Patterns | Cấu trúc giá | Xác định target + hướng |
| S/R | Fibonacci | Vùng pullback + target | Swing, Position |
| S/R | Pivot Points | S/R intraday | Day Trading |
| Phân kỳ | RSI/MACD div | Cảnh báo sớm | Bắt đảo chiều |
| Price Action | SMC, Pin Bar | Hành vi institutional | Mọi chiến lược |
| Rủi ro | ATR SL, % Risk | Bảo toàn vốn | LUÔN DÙNG — quan trọng nhất |

---

*Tài liệu mang tính giáo dục. Giao dịch chứng khoán luôn có rủi ro. Hãy backtest kỹ trước khi áp dụng với tiền thật.*
