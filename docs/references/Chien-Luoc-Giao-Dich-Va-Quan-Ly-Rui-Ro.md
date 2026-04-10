# CHIẾN LƯỢC GIAO DỊCH, QUẢN LÝ RỦI RO & PHÂN BỔ CHỈ BÁO THEO TỪNG PHONG CÁCH

---

## PHẦN I: CÁC MÔ HÌNH QUẢN LÝ RỦI RO

---

### 1. POSITION SIZING — Quản lý kích thước lệnh

#### 1.1 Mô hình Fixed Percentage Risk (% Rủi ro Cố định)

- **Nguyên tắc**: Mỗi lệnh chỉ được rủi ro tối đa X% tổng vốn (thường 1-2%).
- **Công thức**: Số cổ phiếu = (Vốn × %Rủi ro) / (Giá vào - Stop Loss)
- **Ví dụ**: Vốn 100 triệu, rủi ro 2%, Entry = 50.000đ, SL = 47.000đ → Số CP = (100tr × 2%) / 3.000đ = 666 CP.
- **Phù hợp**: Mọi chiến lược — đây là mô hình nền tảng nhất.
- **Phụ thuộc**: Tổng vốn, mức stop loss, tỷ lệ rủi ro chấp nhận.

#### 1.2 Mô hình Fixed Dollar (Số tiền Cố định)

- **Nguyên tắc**: Mỗi lệnh rủi ro một số tiền cố định (VD: 2 triệu đồng/lệnh).
- **Phù hợp**: Người mới bắt đầu, tâm lý ổn định hơn.
- **Hạn chế**: Không tự động điều chỉnh theo vốn tăng/giảm.

#### 1.3 Mô hình Kelly Criterion

- **Công thức**: f* = (bp - q) / b, trong đó b = tỷ lệ lời/lỗ trung bình, p = xác suất thắng, q = 1 - p.
- **Cách dùng**: Cho ra % vốn tối ưu để đặt cược. Thực tế nên dùng Half-Kelly (f*/2) để giảm biến động.
- **Phù hợp**: Trader có dữ liệu backtesting đầy đủ.
- **Phụ thuộc**: Win rate, Average Win/Loss ratio — cần ít nhất 100+ giao dịch lịch sử.

#### 1.4 Mô hình ATR-Based Position Sizing

- **Công thức**: Số CP = (Vốn × %Rủi ro) / (N × ATR), trong đó N = hệ số nhân ATR (thường 1.5-3).
- **Ưu điểm**: Tự động điều chỉnh theo biến động thị trường — khi biến động cao thì giảm size, khi biến động thấp thì tăng size.
- **Phù hợp**: Swing Trading, Position Trading.
- **Phụ thuộc**: ATR(14), hệ số nhân N.

#### 1.5 Mô hình Volatility-Adjusted (Turtle Trading)

- **Nguyên tắc**: 1 Unit = 1% vốn / (N × Point Value), trong đó N = ATR(20).
- **Giới hạn**: Tối đa 4 Units/cổ phiếu, 10 Units/ngành, 12 Units/toàn danh mục.
- **Phù hợp**: Position Trading, Trend Following.

---

### 2. STOP LOSS — Các phương pháp đặt dừng lỗ

#### 2.1 Fixed Stop Loss (SL cố định)

- **Cách đặt**: SL cách Entry một khoảng cố định (VD: -3%, -5%, -7%).
- **Phù hợp**: Người mới, giao dịch ngắn hạn.
- **Hạn chế**: Không tính đến biến động thực tế của cổ phiếu.

#### 2.2 ATR Stop Loss (SL theo biến động)

- **Công thức**: SL = Entry - k × ATR(14), với k = 1.5 (ngắn hạn) đến 3 (dài hạn).
- **Ưu điểm**: Tự thích ứng với mức biến động — CP biến động mạnh có SL xa hơn.
- **Phù hợp**: Swing Trading, Day Trading.

#### 2.3 Structure Stop Loss (SL theo cấu trúc giá)

- **Cách đặt**: SL dưới Swing Low gần nhất (lệnh mua) hoặc trên Swing High gần nhất (lệnh bán).
- **Ưu điểm**: Dựa trên logic thị trường — nếu giá phá vỡ cấu trúc thì tín hiệu đã sai.
- **Phù hợp**: Mọi chiến lược, đặc biệt Price Action.

#### 2.4 Moving Average Stop Loss

- **Cách đặt**: SL khi giá đóng dưới MA quan trọng (EMA 21 cho ngắn hạn, SMA 50 cho trung hạn).
- **Phù hợp**: Trend Following.

#### 2.5 Trailing Stop Loss (SL di động)

| Phương pháp | Cách hoạt động | Phù hợp |
|-------------|----------------|---------|
| **Fixed Trailing** | SL di chuyển theo giá, giữ khoảng cách cố định | Day Trading |
| **ATR Trailing** | SL = Highest High - k × ATR | Swing Trading |
| **Parabolic SAR** | Dùng SAR làm SL trailing tự động | Trend Following |
| **MA Trailing** | SL = đường MA (EMA 21 hoặc SMA 50) | Swing/Position |
| **Chandelier Exit** | SL = Highest High(22) - 3 × ATR(22) | Swing Trading |

#### 2.6 Time Stop (SL theo thời gian)

- **Nguyên tắc**: Nếu lệnh không có lời sau X phiên → thoát.
- **Phù hợp**: Day Trading (cuối ngày), Swing (3-5 phiên).
- **Logic**: Thời gian cũng là chi phí cơ hội.

---

### 3. RISK:REWARD & TARGET

#### 3.1 Risk:Reward Ratio (R:R)

- **Nguyên tắc**: Chỉ vào lệnh khi R:R ≥ 1:2 (lời tiềm năng gấp 2 lần lỗ tiềm năng).
- **Scalping**: R:R ≥ 1:1.5
- **Day Trading**: R:R ≥ 1:2
- **Swing Trading**: R:R ≥ 1:2 đến 1:3
- **Position Trading**: R:R ≥ 1:3 đến 1:5

#### 3.2 Cách xác định Target

| Phương pháp | Mô tả | Phù hợp |
|-------------|-------|---------|
| **Fibonacci Extension** | Target tại 127.2%, 161.8%, 200% | Swing, Position |
| **Chart Pattern Target** | Đo chiều cao mô hình, chiếu từ điểm breakout | Mọi chiến lược |
| **Kháng cự tiếp theo** | Target tại vùng kháng cự quan trọng kế tiếp | Mọi chiến lược |
| **ATR Target** | Target = Entry + k × ATR (k = 2-4) | Day, Swing |
| **R:R cố định** | Target = Entry + (Entry - SL) × R | Mọi chiến lược |
| **Partial Take Profit** | Chốt 50% tại Target 1, chốt 50% tại Target 2 | Swing, Position |

---

### 4. QUẢN LÝ DANH MỤC

#### 4.1 Đa dạng hóa (Diversification)

- **Nguyên tắc**: Không bỏ quá 20-25% vốn vào một cổ phiếu; Phân bổ 4-8 cổ phiếu khác ngành.
- **Phù hợp**: Swing Trading, Position Trading.

#### 4.2 Tương quan (Correlation)

- **Nguyên tắc**: Tránh giữ nhiều CP có tương quan cao (cùng ngành, cùng xu hướng).
- **Công cụ**: Ma trận tương quan (Correlation Matrix).

#### 4.3 Maximum Drawdown Limit

- **Nguyên tắc**: Nếu danh mục lỗ quá X% (thường 6-10% trong tháng) → ngừng giao dịch, xem lại chiến lược.
- **Phù hợp**: Mọi chiến lược.

#### 4.4 Equity Curve Trading

- **Nguyên tắc**: Theo dõi đường vốn (equity curve). Khi đường vốn dưới MA(20 phiên) → giảm size hoặc ngừng giao dịch.
- **Logic**: Khi hệ thống đang không hiệu quả, nên giảm rủi ro.

---

## PHẦN II: PHÂN BỔ CHỈ BÁO THEO TỪNG CHIẾN LƯỢC GIAO DỊCH

---

### CHIẾN LƯỢC 1: SCALPING (1 phút - 15 phút)

**Mục tiêu**: Lợi nhuận nhỏ, tần suất cao, giữ lệnh vài giây đến vài phút.

| Vai trò | Chỉ báo / Kỹ thuật | Cách dùng cụ thể |
|---------|---------------------|-------------------|
| **Xu hướng** | EMA(9), EMA(21) trên M1/M5 | Giá trên EMA → chỉ MUA; Giá dưới EMA → chỉ BÁN |
| **Xu hướng** | VWAP | Giá trên VWAP → thiên MUA; dưới VWAP → thiên BÁN |
| **Timing vào lệnh** | Stochastic (5,3,3) | Quá bán + %K cắt lên %D → MUA; ngược lại → BÁN |
| **Timing vào lệnh** | Price Action (Pin Bar, Engulfing) | Tại vùng EMA hoặc VWAP |
| **Xác nhận** | Volume (thanh volume) | Volume tăng đột biến khi breakout → xác nhận |
| **Xác nhận** | Tape Reading / Level 2 | Đọc sổ lệnh mua bán |
| **Quản lý rủi ro** | Fixed SL 0.1-0.3% | Hoặc SL dưới nến tín hiệu |
| **Target** | R:R 1:1.5, Scalp 0.2-0.5% | Chốt nhanh, không tham |

**Kế hoạch giao dịch Scalping**:
1. Mở chart M1 hoặc M5
2. Xác định VWAP + EMA(9/21) hướng → xác định thiên hướng mua/bán
3. Chờ giá pullback về VWAP hoặc EMA
4. Stochastic cho tín hiệu quá bán/quá mua + nến đảo chiều
5. Vào lệnh, SL dưới nến tín hiệu, TP theo R:R 1:1.5
6. Tổng thời gian giữ lệnh: 30 giây - 5 phút

---

### CHIẾN LƯỢC 2: DAY TRADING (15 phút - 1 giờ)

**Mục tiêu**: Lợi nhuận vừa phải, đóng tất cả lệnh trước khi thị trường đóng cửa.

| Vai trò | Chỉ báo / Kỹ thuật | Cách dùng cụ thể |
|---------|---------------------|-------------------|
| **Xu hướng chính** | EMA(20), EMA(50) trên H1 | Golden Cross / Death Cross xác định xu hướng ngày |
| **Xu hướng chính** | VWAP | Anchor point cho cả ngày giao dịch |
| **Vùng vào lệnh** | Pivot Points (S1, S2, R1, R2) | Hỗ trợ/kháng cự intraday |
| **Vùng vào lệnh** | Bollinger Bands (20,2) | Mua tại dải dưới, bán tại dải trên (khi sideway); Breakout dải (khi trending) |
| **Vùng vào lệnh** | Fibonacci Retracement | Pullback 38.2%-61.8% của sóng sáng/chiều |
| **Timing** | RSI(14) trên M15/M30 | RSI < 30 + đảo chiều → MUA; RSI > 70 + đảo chiều → BÁN |
| **Timing** | MACD (12,26,9) | MACD cắt Signal + Histogram đổi chiều |
| **Timing** | Candlestick Patterns | Engulfing, Hammer, Shooting Star tại vùng quan trọng |
| **Xác nhận volume** | MFI(14) | MFI xác nhận dòng tiền cùng chiều tín hiệu |
| **Xác nhận volume** | Volume Profile | Vào lệnh tại vùng POC hoặc Value Area edge |
| **Quản lý rủi ro** | ATR SL: Entry ± 1.5×ATR | Hoặc SL dưới/trên Pivot Point gần nhất |
| **Target** | R:R ≥ 1:2; Pivot tiếp theo | Partial TP tại R1, full TP tại R2 |
| **Trailing Stop** | Parabolic SAR hoặc EMA(9) | Di chuyển SL theo SAR/EMA khi lệnh có lời |

**Kế hoạch giao dịch Day Trading**:
1. **Trước phiên (8:30-9:00)**: Tính Pivot Points, xác định vùng VWAP, check tin tức
2. **Đầu phiên (9:00-9:30)**: Quan sát, KHÔNG vào lệnh (quá biến động)
3. **Phiên sáng (9:30-11:00)**: Xác định xu hướng bằng EMA + VWAP → tìm pullback về Pivot/Fibonacci → RSI + MACD xác nhận → vào lệnh
4. **Phiên chiều (13:00-14:30)**: Giao dịch theo xu hướng đã xác lập, trailing stop
5. **Cuối phiên (14:30-15:00)**: Đóng tất cả vị thế, đánh giá kết quả

---

### CHIẾN LƯỢC 3: SWING TRADING (4 giờ - Daily)

**Mục tiêu**: Bắt sóng trung hạn, giữ lệnh vài ngày đến vài tuần.

| Vai trò | Chỉ báo / Kỹ thuật | Cách dùng cụ thể |
|---------|---------------------|-------------------|
| **Xu hướng lớn (Weekly)** | SMA(50), SMA(200) | Giá trên SMA(200) → uptrend dài hạn; Golden/Death Cross |
| **Xu hướng lớn** | ADX(14) | ADX > 25 → xu hướng đáng giao dịch; +DI/-DI xác định hướng |
| **Xu hướng lớn** | Ichimoku (Daily) | Giá trên mây + Tenkan > Kijun → bullish mạnh |
| **Vùng vào lệnh** | Fibonacci Retracement | Pullback về 38.2%, 50%, 61.8% của sóng trước |
| **Vùng vào lệnh** | EMA(21) trên Daily | Hỗ trợ động — mua khi giá test lại EMA(21) |
| **Vùng vào lệnh** | Hỗ trợ/Kháng cự lịch sử | Vùng đỉnh/đáy cũ, số tròn |
| **Vùng vào lệnh** | Bollinger Bands (20,2) | Bollinger Squeeze → chuẩn bị breakout mạnh |
| **Timing** | RSI(14) trên Daily | RSI pullback về 40-50 rồi bật lên (uptrend); Phân kỳ RSI |
| **Timing** | MACD (12,26,9) | MACD cắt Signal; Histogram đổi chiều từ âm sang dương |
| **Timing** | Stochastic (14,3,3) | %K cắt %D tại vùng quá bán (< 20) |
| **Timing** | Candlestick (Daily) | Morning Star, Bullish Engulfing, Hammer tại Fibonacci/Support |
| **Xác nhận** | OBV | OBV tăng cùng chiều giá → xác nhận |
| **Xác nhận** | CMF(20) | CMF > 0 → dòng tiền ủng hộ mua |
| **Mô hình giá** | Flag, Pennant, Triangle | Breakout mô hình tiếp diễn + volume tăng |
| **Mô hình giá** | Cup & Handle, Inv H&S | Breakout mô hình đảo chiều tăng |
| **Quản lý rủi ro** | ATR SL: Entry - 2×ATR(14) | Hoặc SL dưới Swing Low |
| **Position Sizing** | 1-2% risk per trade | ATR-based sizing |
| **Target** | Fibonacci Ext 127.2%, 161.8% | R:R ≥ 1:2; Partial TP |
| **Trailing Stop** | Chandelier Exit hoặc EMA(21) | Nâng SL khi giá tạo đáy cao hơn |

**Kế hoạch giao dịch Swing Trading**:
1. **Cuối tuần**: Scan thị trường trên khung Weekly → lọc CP uptrend (trên SMA 50/200, ADX > 25)
2. **Hàng ngày**: Trên Daily, tìm CP đang pullback về Fibonacci 38.2-61.8% hoặc EMA(21)
3. **Tín hiệu vào**: RSI bật từ 40-50, MACD histogram đổi chiều, nến đảo chiều tại support
4. **Xác nhận**: OBV/CMF xác nhận dòng tiền, Volume tăng khi nến tín hiệu
5. **Vào lệnh**: Buy tại Open ngày hôm sau nến tín hiệu
6. **SL**: Dưới Swing Low hoặc Entry - 2×ATR
7. **TP**: 50% tại Fibo Ext 127.2%, 50% tại 161.8%, trailing stop bằng EMA(21)
8. **Thời gian giữ**: 5-20 phiên

---

### CHIẾN LƯỢC 4: POSITION TRADING / TREND FOLLOWING (Daily - Weekly)

**Mục tiêu**: Bắt xu hướng lớn, giữ lệnh vài tuần đến vài tháng.

| Vai trò | Chỉ báo / Kỹ thuật | Cách dùng cụ thể |
|---------|---------------------|-------------------|
| **Xu hướng** | SMA(50) + SMA(200) | Golden Cross → MUA và giữ; Death Cross → BÁN |
| **Xu hướng** | ADX(14) Weekly | ADX > 25 trên Weekly → xu hướng dài hạn mạnh |
| **Xu hướng** | Ichimoku (Weekly) | Giá trên mây Weekly → uptrend dài hạn bền vững |
| **Xu hướng** | Sóng Elliott | Đếm sóng để xác định đang ở sóng mấy → còn bao nhiêu upside |
| **Vùng vào lệnh** | Fibonacci Retracement (Weekly) | Pullback về 38.2-50% trên khung Weekly |
| **Vùng vào lệnh** | SMA(50) trên Daily | Mua khi giá test lại SMA(50) và bật lên |
| **Vùng vào lệnh** | Donchian Channel (20 tuần) | Breakout Donchian → MUA (Turtle system) |
| **Timing** | MACD Weekly | MACD cắt Signal trên Weekly → tín hiệu rất mạnh |
| **Timing** | RSI Monthly | RSI thoát oversold trên Monthly → sóng dài hạn |
| **Xác nhận** | A/D Line thị trường | A/D Line tăng cùng index → uptrend bền vững |
| **Xác nhận** | OBV dài hạn | OBV trending → smart money tích lũy |
| **Mô hình giá** | Head & Shoulders, Double Bottom | Mô hình đảo chiều dài hạn trên Weekly |
| **Mô hình giá** | Rounding Bottom, Cup & Handle | Mô hình tích lũy dài hạn |
| **Quản lý rủi ro** | ATR SL: Entry - 3×ATR(14) Weekly | SL rộng vì khung thời gian lớn |
| **Position Sizing** | Turtle: 1% vốn / N | Giảm size khi biến động cao |
| **Target** | Fibonacci Ext 161.8%, 261.8% | Hoặc chờ Death Cross mới thoát |
| **Trailing Stop** | SMA(50) hoặc Parabolic SAR Weekly | Thoát khi giá đóng dưới SMA(50) 2 tuần liên tiếp |

**Kế hoạch giao dịch Position Trading**:
1. **Hàng tháng**: Phân tích Monthly chart → xác định xu hướng siêu dài hạn
2. **Hàng tuần**: Phân tích Weekly → SMA(50/200) + Ichimoku + ADX → lọc CP uptrend mạnh
3. **Tín hiệu vào**: Pullback về SMA(50) + RSI Weekly bật từ 40 + MACD Weekly bullish cross
4. **Vào lệnh**: Mua từ từ (phân bổ 2-3 lần vào)
5. **SL**: Dưới SMA(200) hoặc Entry - 3×ATR Weekly
6. **Trailing**: Nâng SL lên dưới SMA(50) mỗi tuần
7. **Thoát**: Khi giá đóng dưới SMA(50) 2 tuần hoặc Death Cross
8. **Thời gian giữ**: 1-6 tháng

---

### CHIẾN LƯỢC 5: BREAKOUT TRADING

**Mục tiêu**: Vào lệnh ngay khi giá phá vỡ vùng tích lũy.

| Vai trò | Chỉ báo / Kỹ thuật | Cách dùng cụ thể |
|---------|---------------------|-------------------|
| **Xác định vùng tích lũy** | Bollinger Bands Squeeze | Bandwidth thấp nhất 6 tháng → chuẩn bị breakout |
| **Xác định vùng tích lũy** | Keltner Channel + Bollinger | BB nằm trong Keltner → Squeeze cực mạnh (TTM Squeeze) |
| **Xác định vùng tích lũy** | Rectangle, Triangle, Flag | Mô hình chart pattern cho thấy consolidation |
| **Xác định vùng tích lũy** | Donchian Channel | Kênh hẹp dần → sắp breakout |
| **Tín hiệu breakout** | Price Action | Nến Marubozu phá vỡ vùng kháng cự/hỗ trợ |
| **Xác nhận** | Volume tăng ≥ 150% trung bình | Breakout không có volume → dễ là false breakout |
| **Xác nhận** | ADX | ADX bắt đầu tăng từ < 20 lên > 25 → xu hướng mới đang hình thành |
| **Xác nhận** | MACD | Histogram tăng dần → momentum đang tăng |
| **Quản lý rủi ro** | SL ngay dưới vùng breakout | Nếu giá quay lại vùng tích lũy → breakout thất bại |
| **Target** | Chiều cao vùng tích lũy × 1-2 | Chiếu từ điểm breakout |

---

### CHIẾN LƯỢC 6: MEAN REVERSION (Hồi quy trung bình)

**Mục tiêu**: Giao dịch khi giá lệch quá xa khỏi giá trị trung bình, kỳ vọng quay về.

| Vai trò | Chỉ báo / Kỹ thuật | Cách dùng cụ thể |
|---------|---------------------|-------------------|
| **Xác định "quá xa"** | Bollinger Bands | Giá chạm/vượt dải dưới + nến đảo chiều → MUA |
| **Xác định "quá xa"** | RSI < 20 hoặc > 80 | Cực đoan → kỳ vọng quay về |
| **Xác định "quá xa"** | CCI < -200 hoặc > +200 | Quá xa trung bình |
| **Xác định "quá xa"** | Khoảng cách giá vs MA | Giá cách EMA(20) > 2×ATR → quá xa |
| **Xác nhận đảo chiều** | Stochastic | %K cắt %D tại vùng cực đoan |
| **Xác nhận đảo chiều** | Candlestick | Hammer, Bullish Engulfing tại dải dưới Bollinger |
| **Xác nhận đảo chiều** | Phân kỳ RSI/MACD | Phân kỳ dương tại vùng quá bán → đảo chiều mạnh |
| **Điều kiện bắt buộc** | ADX < 25 | Mean reversion CHỈ hoạt động trong thị trường sideway |
| **Target** | Về lại MA(20) hoặc dải giữa Bollinger | R:R thường 1:1 đến 1:2 |
| **SL** | Dưới đáy mới hoặc Entry - 1.5×ATR | Chặt vì ngược xu hướng nếu trending |

---

### CHIẾN LƯỢC 7: MOMENTUM TRADING

**Mục tiêu**: Mua CP đang có momentum mạnh nhất, bán CP momentum yếu nhất.

| Vai trò | Chỉ báo / Kỹ thuật | Cách dùng cụ thể |
|---------|---------------------|-------------------|
| **Đo momentum** | ROC(12) hoặc ROC(26) | Xếp hạng CP theo ROC → mua top 10-20% |
| **Đo momentum** | RSI(14) | RSI 60-80 → momentum tăng mạnh (KHÔNG phải overbought trong context trending) |
| **Đo momentum** | MACD Histogram | Histogram tăng dần → momentum đang tăng tốc |
| **Đo momentum** | ADX > 30 | Xu hướng rất mạnh → momentum đáng giao dịch |
| **Xác nhận** | Price trên EMA(10/20) | Xu hướng tăng ngắn hạn rõ ràng |
| **Xác nhận** | Volume tăng theo giá | Smart money đang tham gia |
| **Xác nhận** | Relative Strength vs Index | CP mạnh hơn thị trường chung |
| **Quản lý rủi ro** | Trailing Stop EMA(10) hoặc ATR | Thoát khi momentum suy yếu |
| **Thoát** | MACD histogram giảm 3 phiên | Hoặc RSI phân kỳ âm |

---

## PHẦN III: BẢNG MA TRẬN TỔNG HỢP — CHỈ BÁO × CHIẾN LƯỢC

| Chỉ báo / Kỹ thuật | Scalping | Day Trading | Swing Trading | Position Trading | Breakout | Mean Reversion | Momentum |
|---------------------|----------|-------------|---------------|------------------|----------|----------------|----------|
| **EMA ngắn (9/12/21)** | ★★★ | ★★★ | ★★☆ | ☆☆☆ | ★☆☆ | ☆☆☆ | ★★☆ |
| **SMA dài (50/200)** | ☆☆☆ | ★☆☆ | ★★★ | ★★★ | ★★☆ | ☆☆☆ | ★★☆ |
| **VWAP** | ★★★ | ★★★ | ☆☆☆ | ☆☆☆ | ☆☆☆ | ☆☆☆ | ☆☆☆ |
| **RSI** | ★☆☆ | ★★★ | ★★★ | ★★☆ | ★☆☆ | ★★★ | ★★★ |
| **MACD** | ☆☆☆ | ★★★ | ★★★ | ★★★ | ★★☆ | ★☆☆ | ★★★ |
| **Stochastic** | ★★★ | ★★☆ | ★★★ | ★☆☆ | ☆☆☆ | ★★★ | ☆☆☆ |
| **Bollinger Bands** | ★☆☆ | ★★★ | ★★★ | ★☆☆ | ★★★ | ★★★ | ☆☆☆ |
| **ADX** | ☆☆☆ | ★★☆ | ★★★ | ★★★ | ★★★ | ★★★(ngược) | ★★★ |
| **Ichimoku** | ☆☆☆ | ★☆☆ | ★★★ | ★★★ | ★★☆ | ☆☆☆ | ★★☆ |
| **Fibonacci** | ★☆☆ | ★★☆ | ★★★ | ★★★ | ★☆☆ | ★☆☆ | ★☆☆ |
| **Pivot Points** | ★★★ | ★★★ | ☆☆☆ | ☆☆☆ | ☆☆☆ | ☆☆☆ | ☆☆☆ |
| **OBV/CMF/MFI** | ★☆☆ | ★★☆ | ★★★ | ★★★ | ★★★ | ★★☆ | ★★★ |
| **ATR** | ★★☆ | ★★★ | ★★★ | ★★★ | ★★★ | ★★☆ | ★★☆ |
| **Candlestick** | ★★★ | ★★★ | ★★★ | ★★☆ | ★★☆ | ★★★ | ★☆☆ |
| **Chart Patterns** | ☆☆☆ | ★★☆ | ★★★ | ★★★ | ★★★ | ☆☆☆ | ★☆☆ |
| **Elliott Wave** | ☆☆☆ | ☆☆☆ | ★★☆ | ★★★ | ☆☆☆ | ☆☆☆ | ☆☆☆ |
| **Volume Profile** | ★★★ | ★★★ | ★★☆ | ★☆☆ | ★★★ | ★★☆ | ☆☆☆ |
| **Parabolic SAR** | ★☆☆ | ★★☆ | ★★☆ | ★★★ | ☆☆☆ | ☆☆☆ | ★★☆ |
| **Donchian Channel** | ☆☆☆ | ☆☆☆ | ★★☆ | ★★★ | ★★★ | ☆☆☆ | ★★☆ |
| **Price Action/SMC** | ★★★ | ★★★ | ★★★ | ★★☆ | ★★★ | ★★★ | ★☆☆ |

*★★★ = Rất phù hợp | ★★☆ = Phù hợp | ★☆☆ = Có thể dùng | ☆☆☆ = Không phù hợp*

---

## PHẦN IV: LỘ TRÌNH LẬP KẾ HOẠCH THEO TỪNG KHUNG THỜI GIAN

---

### KẾ HOẠCH DÀI HẠN (3-12 tháng) — "Bức tranh lớn"

| Bước | Hành động | Công cụ |
|------|-----------|---------|
| 1 | Xác định chu kỳ kinh tế (expansion/recession) | Macro indicators, VIX, Bond yields |
| 2 | Xác định xu hướng thị trường chung | Index trên/dưới SMA(200) Weekly |
| 3 | Chọn ngành mạnh nhất | Sector Rotation, Relative Strength |
| 4 | Lọc CP uptrend dài hạn | SMA(50) > SMA(200), giá trên cả 2 MA |
| 5 | Phân bổ vốn chiến lược | Turtle Position Sizing, Kelly Criterion |
| 6 | Đặt SL chiến lược | Dưới SMA(200) hoặc 3×ATR Weekly |
| 7 | Review hàng tháng | Ichimoku Weekly, Elliott Wave count |

### KẾ HOẠCH TRUNG HẠN (2-8 tuần) — "Sóng swing"

| Bước | Hành động | Công cụ |
|------|-----------|---------|
| 1 | Xác nhận xu hướng trung hạn | EMA(50) Daily + ADX > 25 |
| 2 | Tìm CP đang pullback | Fibonacci 38.2-61.8% + RSI 40-50 |
| 3 | Chờ tín hiệu kỹ thuật | MACD cross + Candlestick + Volume |
| 4 | Tính position size | 1-2% risk, ATR-based |
| 5 | Đặt entry, SL, TP cụ thể | SL dưới Swing Low; TP tại Fibo Extension |
| 6 | Quản lý lệnh hàng ngày | Trailing Stop = EMA(21) hoặc Chandelier |
| 7 | Review cuối tuần | Cập nhật SL, đánh giá R:R còn lại |

### KẾ HOẠCH NGẮN HẠN (Trong ngày) — "Chiến thuật hàng ngày"

| Bước | Hành động | Công cụ |
|------|-----------|---------|
| 1 | Pre-market: Check tin tức, gap | News, pre-market data |
| 2 | Tính Pivot Points cho ngày | PP, R1, R2, S1, S2 |
| 3 | Xác định VWAP + EMA intraday | VWAP, EMA(9/21) trên M15 |
| 4 | Đợi setup tại vùng quan trọng | Pivot + VWAP + Price Action |
| 5 | Xác nhận bằng momentum | RSI, Stochastic, MACD trên M15 |
| 6 | Vào lệnh với SL chặt | SL dưới nến tín hiệu hoặc 1.5×ATR |
| 7 | Quản lý lệnh realtime | Trailing Stop, TP theo R:R ≥ 1:2 |
| 8 | Đóng tất cả trước 14:45 | Time Stop |
| 9 | Cuối ngày: Journaling | Ghi lại lý do vào/ra, kết quả, bài học |

---

## PHẦN V: NGUYÊN TẮC VÀNG

1. **Không bao giờ giao dịch mà không có Stop Loss** — đây là quy tắc sống còn.
2. **Xu hướng là bạn** — giao dịch theo xu hướng khung thời gian lớn hơn.
3. **Khối lượng xác nhận tất cả** — breakout không có volume = false breakout.
4. **R:R tối thiểu 1:2** — chỉ cần thắng 40% vẫn có lợi nhuận.
5. **Không quá 5% rủi ro tổng danh mục cùng lúc** — bảo toàn vốn là ưu tiên số 1.
6. **Phân tích đa khung thời gian** — luôn nhìn bức tranh lớn trước khi zoom vào chi tiết.
7. **Journaling** — ghi lại mọi giao dịch để cải thiện hệ thống.
8. **Backtesting** — test chiến lược trên dữ liệu lịch sử ít nhất 100 giao dịch trước khi dùng tiền thật.
9. **Tâm lý** — tuân thủ kế hoạch, không để cảm xúc chi phối (FOMO, sợ hãi, revenge trading).
10. **Đơn giản hóa** — không cần dùng tất cả chỉ báo, chọn 3-5 chỉ báo phù hợp phong cách và thành thạo chúng.

---

*Lưu ý: Tài liệu này mang tính giáo dục và tổng hợp kiến thức. Giao dịch chứng khoán luôn có rủi ro mất vốn. Không có hệ thống nào đúng 100%. Hãy luôn thực hành trên tài khoản demo trước khi giao dịch thực, và cân nhắc tham khảo ý kiến chuyên gia tài chính.*
