# TOÀN BỘ PHÂN TÍCH KỸ THUẬT CHO GIAO DỊCH NGẮN HẠN CHỨNG KHOÁN

---

## MỤC LỤC

1. [Tổng quan về Phân tích Kỹ thuật](#1-tổng-quan)
2. [Nhóm 1: Phân tích Xu hướng (Trend)](#2-phân-tích-xu-hướng)
3. [Nhóm 2: Chỉ báo Động lượng (Momentum)](#3-chỉ-báo-động-lượng)
4. [Nhóm 3: Chỉ báo Khối lượng (Volume)](#4-chỉ-báo-khối-lượng)
5. [Nhóm 4: Chỉ báo Biến động (Volatility)](#5-chỉ-báo-biến-động)
6. [Nhóm 5: Mô hình Nến Nhật (Candlestick Patterns)](#6-mô-hình-nến-nhật)
7. [Nhóm 6: Mô hình Giá (Chart Patterns)](#7-mô-hình-giá)
8. [Nhóm 7: Hỗ trợ & Kháng cự (Support/Resistance)](#8-hỗ-trợ-kháng-cự)
9. [Nhóm 8: Fibonacci](#9-fibonacci)
10. [Nhóm 9: Sóng Elliott](#10-sóng-elliott)
11. [Nhóm 10: Ichimoku Kinko Hyo](#11-ichimoku)
12. [Nhóm 11: Phân tích Dòng tiền & Market Breadth](#12-dòng-tiền)
13. [Nhóm 12: Phân kỳ (Divergence)](#13-phân-kỳ)
14. [Nhóm 13: Kỹ thuật Price Action](#14-price-action)
15. [Bảng tổng hợp: Kỹ thuật nào phù hợp khung thời gian nào](#15-bảng-tổng-hợp)
16. [Kết hợp các kỹ thuật — Hệ thống giao dịch hoàn chỉnh](#16-kết-hợp)

---

## 1. TỔNG QUAN VỀ PHÂN TÍCH KỸ THUẬT

Phân tích kỹ thuật (Technical Analysis - TA) là phương pháp dự đoán hướng đi của giá dựa trên dữ liệu lịch sử về giá và khối lượng giao dịch. Nó dựa trên 3 giả định cốt lõi:

- **Giá phản ánh tất cả**: Mọi thông tin (cơ bản, tâm lý, tin tức) đều đã được phản ánh vào giá.
- **Giá vận động theo xu hướng**: Một khi xu hướng hình thành, nó có xu hướng tiếp tục cho đến khi có tín hiệu đảo chiều.
- **Lịch sử lặp lại**: Các mô hình giá có xu hướng lặp lại do tâm lý thị trường không thay đổi.

**Dữ liệu đầu vào cốt lõi**: Giá mở cửa (Open), Giá cao nhất (High), Giá thấp nhất (Low), Giá đóng cửa (Close), Khối lượng (Volume), và Thời gian (Time).

---

## 2. PHÂN TÍCH XU HƯỚNG (TREND ANALYSIS)

### 2.1 Đường trung bình động (Moving Averages)

#### a) SMA — Simple Moving Average (Trung bình động đơn giản)

- **Công thức**: SMA(n) = (P₁ + P₂ + ... + Pₙ) / n
- **Cách dùng**: SMA(5), SMA(10) cho ngắn hạn; SMA(20), SMA(50) cho trung hạn; SMA(100), SMA(200) cho dài hạn.
- **Tín hiệu MUA**: Giá cắt lên trên SMA; SMA ngắn hạn cắt lên SMA dài hạn (Golden Cross).
- **Tín hiệu BÁN**: Giá cắt xuống dưới SMA; SMA ngắn hạn cắt xuống SMA dài hạn (Death Cross).
- **Phụ thuộc**: Chu kỳ (n), loại giá (Close, Typical, Weighted).
- **Hạn chế**: Phản ứng chậm vì tất cả các phiên có trọng số bằng nhau.

#### b) EMA — Exponential Moving Average (Trung bình động hàm mũ)

- **Công thức**: EMA(t) = Giá(t) × k + EMA(t-1) × (1-k), trong đó k = 2/(n+1)
- **Cách dùng**: Tương tự SMA nhưng phản ứng nhanh hơn với giá gần đây.
- **Phổ biến cho ngắn hạn**: EMA(9), EMA(12), EMA(21), EMA(26).
- **Phụ thuộc**: Chu kỳ (n). Cho trọng số lớn hơn cho dữ liệu gần nhất.
- **Ưu điểm so với SMA**: Phản ứng nhanh hơn, phù hợp hơn cho giao dịch ngắn hạn.

#### c) WMA — Weighted Moving Average (Trung bình động có trọng số)

- **Công thức**: WMA = (n×Pₙ + (n-1)×Pₙ₋₁ + ... + 1×P₁) / (n + (n-1) + ... + 1)
- **Phụ thuộc**: Chu kỳ (n), trọng số giảm tuyến tính.

#### d) DEMA, TEMA — Double/Triple EMA

- Giảm thêm độ trễ so với EMA. DEMA = 2×EMA - EMA(EMA). TEMA = 3×EMA - 3×EMA(EMA) + EMA(EMA(EMA)).
- **Phụ thuộc**: Chu kỳ (n), tính toán đệ quy phức tạp hơn.

#### e) VWAP — Volume Weighted Average Price

- **Công thức**: VWAP = Σ(Giá × Khối lượng) / Σ(Khối lượng)
- **Cách dùng**: Giá trên VWAP → xu hướng tăng trong ngày; Giá dưới VWAP → xu hướng giảm.
- **Phụ thuộc**: Giá và khối lượng intraday; reset mỗi ngày.
- **Đặc biệt phù hợp**: Giao dịch trong ngày (Day Trading).

### 2.2 Đường xu hướng (Trendline)

- **Cách vẽ**: Nối 2+ đáy cao dần (uptrend) hoặc 2+ đỉnh thấp dần (downtrend).
- **Tín hiệu**: Phá vỡ đường xu hướng = tín hiệu đảo chiều tiềm năng.
- **Phụ thuộc**: Khung thời gian, số điểm tiếp xúc (càng nhiều càng mạnh), góc nghiêng.
- **Quy tắc**: Cần ít nhất 2 điểm để vẽ, 3 điểm để xác nhận.

### 2.3 Kênh giá (Channel)

- **Kênh tăng**: Đường xu hướng tăng + đường song song phía trên.
- **Kênh giảm**: Đường xu hướng giảm + đường song song phía dưới.
- **Kênh ngang**: Giá dao động trong vùng sideway.
- **Cách giao dịch**: Mua ở cận dưới kênh, bán ở cận trên kênh.

### 2.4 ADX — Average Directional Index

- **Công thức**: Dựa trên +DI và -DI (Directional Indicators), rồi tính ADX = SMA(DX, 14).
- **Cách đọc**: ADX > 25 → xu hướng mạnh; ADX < 20 → thị trường sideway; +DI > -DI → xu hướng tăng; -DI > +DI → xu hướng giảm.
- **Phụ thuộc**: Chu kỳ (thường 14), giá High/Low/Close.
- **Lưu ý**: ADX chỉ đo sức mạnh xu hướng, KHÔNG cho biết hướng xu hướng (cần +DI/-DI).

### 2.5 Parabolic SAR

- **Công thức**: SAR(t+1) = SAR(t) + AF × (EP - SAR(t)), trong đó AF = Acceleration Factor (0.02 → 0.20), EP = Extreme Point.
- **Tín hiệu**: Các chấm dưới giá → xu hướng tăng (giữ MUA); Các chấm trên giá → xu hướng giảm (giữ BÁN).
- **Phụ thuộc**: AF khởi đầu, AF tối đa, giá High/Low.
- **Phù hợp**: Xác định điểm dừng lỗ trailing stop.

---

## 3. CHỈ BÁO ĐỘNG LƯỢNG (MOMENTUM INDICATORS)

### 3.1 RSI — Relative Strength Index

- **Công thức**: RSI = 100 - [100 / (1 + RS)], trong đó RS = Trung bình tăng / Trung bình giảm (thường 14 phiên).
- **Cách đọc**: RSI > 70 → quá mua (overbought), có thể sắp giảm; RSI < 30 → quá bán (oversold), có thể sắp tăng.
- **Tín hiệu mạnh**: Phân kỳ RSI với giá (xem phần Phân kỳ).
- **Phụ thuộc**: Chu kỳ (14 mặc định), ngưỡng quá mua/quá bán (70/30 hoặc 80/20).
- **Biến thể**: Stochastic RSI = RSI áp dụng công thức Stochastic.

### 3.2 MACD — Moving Average Convergence Divergence

- **Thành phần**:
  - Đường MACD = EMA(12) - EMA(26)
  - Đường Signal = EMA(9) của MACD
  - Histogram = MACD - Signal
- **Tín hiệu MUA**: MACD cắt lên Signal; Histogram chuyển từ âm sang dương.
- **Tín hiệu BÁN**: MACD cắt xuống Signal; Histogram chuyển từ dương sang âm.
- **Phụ thuộc**: 3 tham số (12, 26, 9 mặc định), giá Close.
- **Mạnh khi**: Kết hợp với phân kỳ.

### 3.3 Stochastic Oscillator

- **Công thức**: %K = [(C - L14) / (H14 - L14)] × 100, trong đó C = giá đóng cửa, L14 = giá thấp nhất 14 phiên, H14 = giá cao nhất 14 phiên. %D = SMA(3) của %K.
- **Cách đọc**: %K > 80 → quá mua; %K < 20 → quá bán; %K cắt lên %D → MUA; %K cắt xuống %D → BÁN.
- **Phụ thuộc**: Chu kỳ %K (14), chu kỳ làm mượt %D (3).
- **Biến thể**: Fast Stochastic (nhạy hơn), Slow Stochastic (mượt hơn, ít nhiễu).

### 3.4 Williams %R

- **Công thức**: %R = [(H14 - C) / (H14 - L14)] × (-100)
- **Cách đọc**: %R từ -80 đến -100 → quá bán; %R từ 0 đến -20 → quá mua.
- **Phụ thuộc**: Chu kỳ (14 mặc định).
- **Tương tự**: Ngược lại Stochastic %K.

### 3.5 CCI — Commodity Channel Index

- **Công thức**: CCI = (Typical Price - SMA(TP)) / (0.015 × Mean Deviation), trong đó Typical Price = (H + L + C) / 3.
- **Cách đọc**: CCI > +100 → quá mua / xu hướng tăng mạnh; CCI < -100 → quá bán / xu hướng giảm mạnh.
- **Phụ thuộc**: Chu kỳ (20 mặc định), hằng số 0.015.

### 3.6 ROC — Rate of Change

- **Công thức**: ROC = [(C hiện tại - C n phiên trước) / C n phiên trước] × 100
- **Cách đọc**: ROC > 0 → động lượng tăng; ROC < 0 → động lượng giảm; ROC đảo chiều tại cực trị → tín hiệu đảo chiều.
- **Phụ thuộc**: Chu kỳ (n = 12 mặc định).

### 3.7 Momentum Indicator

- **Công thức**: Momentum = C hiện tại - C n phiên trước
- **Cách đọc**: Tương tự ROC nhưng không tính phần trăm.
- **Phụ thuộc**: Chu kỳ n.

---

## 4. CHỈ BÁO KHỐI LƯỢNG (VOLUME INDICATORS)

### 4.1 OBV — On-Balance Volume

- **Công thức**: Nếu C hôm nay > C hôm qua → OBV = OBV trước + Volume; Nếu C hôm nay < C hôm qua → OBV = OBV trước - Volume.
- **Cách đọc**: OBV tăng cùng giá → xác nhận xu hướng; OBV phân kỳ với giá → cảnh báo đảo chiều.
- **Phụ thuộc**: Giá Close, Khối lượng hàng ngày.

### 4.2 A/D Line — Accumulation/Distribution Line

- **Công thức**: CLV = [(C - L) - (H - C)] / (H - L); A/D = A/D trước + CLV × Volume.
- **Cách đọc**: A/D tăng → tích lũy (mua vào); A/D giảm → phân phối (bán ra).
- **Phụ thuộc**: OHLC và Volume.

### 4.3 CMF — Chaikin Money Flow

- **Công thức**: CMF = Σ(CLV × Volume, 20 phiên) / Σ(Volume, 20 phiên)
- **Cách đọc**: CMF > 0 → áp lực mua; CMF < 0 → áp lực bán.
- **Phụ thuộc**: Chu kỳ (20), OHLCV.

### 4.4 MFI — Money Flow Index (RSI có khối lượng)

- **Công thức**: MFI = 100 - [100 / (1 + Money Flow Ratio)], trong đó Money Flow = Typical Price × Volume.
- **Cách đọc**: MFI > 80 → quá mua; MFI < 20 → quá bán.
- **Phụ thuộc**: Chu kỳ (14), OHLCV.
- **So với RSI**: MFI thêm yếu tố khối lượng nên phản ánh dòng tiền thực tế hơn.

### 4.5 VWAP (đã mô tả ở mục 2.1e)

### 4.6 Volume Profile

- **Cách dùng**: Biểu đồ histogram ngang cho thấy khối lượng giao dịch tại từng mức giá.
- **Khái niệm quan trọng**: POC (Point of Control) = mức giá có khối lượng lớn nhất; Value Area = vùng chứa 70% khối lượng.
- **Phụ thuộc**: Dữ liệu OHLCV chi tiết (tick/phút).
- **Ứng dụng**: Xác định vùng hỗ trợ/kháng cự mạnh dựa trên khối lượng.

---

## 5. CHỈ BÁO BIẾN ĐỘNG (VOLATILITY INDICATORS)

### 5.1 Bollinger Bands

- **Thành phần**: Dải giữa = SMA(20); Dải trên = SMA(20) + 2σ; Dải dưới = SMA(20) - 2σ (σ = độ lệch chuẩn).
- **Cách đọc**: Giá chạm dải trên → quá mua tiềm năng; Giá chạm dải dưới → quá bán tiềm năng; Dải bóp hẹp (Squeeze) → sắp có breakout mạnh; Giá đi ngoài dải → xu hướng rất mạnh, KHÔNG nên vội đảo chiều.
- **Phụ thuộc**: Chu kỳ SMA (20), số độ lệch chuẩn (2), giá Close.
- **Kỹ thuật đặc biệt**: Bollinger Squeeze → khi dải hẹp nhất → chuẩn bị cho breakout.

### 5.2 ATR — Average True Range

- **Công thức**: TR = Max(H-L, |H-C trước|, |L-C trước|); ATR = SMA(TR, 14).
- **Cách dùng**: KHÔNG cho tín hiệu mua/bán, mà đo mức độ biến động. Dùng để đặt Stop-Loss: SL = Giá vào - k × ATR (k thường = 1.5-3).
- **Phụ thuộc**: Chu kỳ (14), giá HLC.
- **Ứng dụng chính**: Quản lý rủi ro, sizing position, đặt trailing stop.

### 5.3 Keltner Channel

- **Thành phần**: Dải giữa = EMA(20); Dải trên = EMA(20) + 2 × ATR(10); Dải dưới = EMA(20) - 2 × ATR(10).
- **So với Bollinger**: Dùng ATR thay vì độ lệch chuẩn → mượt hơn, ít co giãn đột ngột.
- **Phụ thuộc**: Chu kỳ EMA, chu kỳ ATR, hệ số nhân ATR.

### 5.4 Donchian Channel

- **Thành phần**: Dải trên = Highest High (n phiên); Dải dưới = Lowest Low (n phiên); Dải giữa = trung bình.
- **Cách dùng**: Breakout trên dải trên → MUA; Breakdown dưới dải dưới → BÁN.
- **Phụ thuộc**: Chu kỳ (20 mặc định).
- **Lịch sử**: Cơ sở của hệ thống Turtle Trading nổi tiếng.

### 5.5 Chỉ số VIX (cho thị trường chung)

- **Bản chất**: Đo kỳ vọng biến động của S&P 500 trong 30 ngày tới.
- **Cách đọc**: VIX > 30 → thị trường sợ hãi (có thể là đáy); VIX < 15 → thị trường tự mãn (cẩn trọng đỉnh).
- **Phụ thuộc**: Giá quyền chọn (options) trên S&P 500.

---

## 6. MÔ HÌNH NẾN NHẬT (CANDLESTICK PATTERNS)

### 6.1 Mô hình đảo chiều tăng (Bullish Reversal)

| Mô hình | Mô tả | Độ tin cậy |
|---------|-------|-------------|
| **Hammer (Búa)** | Thân nhỏ ở trên, bóng dưới dài ≥ 2× thân, xuất hiện cuối downtrend | Trung bình |
| **Inverted Hammer** | Thân nhỏ ở dưới, bóng trên dài, cuối downtrend | Thấp (cần xác nhận) |
| **Bullish Engulfing** | Nến tăng bao trùm hoàn toàn nến giảm trước đó | Cao |
| **Piercing Line** | Nến tăng mở dưới Low nến trước, đóng trên 50% thân nến trước | Trung bình |
| **Morning Star** | 3 nến: giảm dài + thân nhỏ (gap xuống) + tăng dài | Cao |
| **Three White Soldiers** | 3 nến tăng liên tiếp, mỗi nến mở trong thân nến trước và đóng cao hơn | Cao |
| **Bullish Harami** | Nến tăng nhỏ nằm trong thân nến giảm lớn trước đó | Trung bình |
| **Tweezer Bottom** | 2 nến có Low bằng nhau, nến 1 giảm, nến 2 tăng | Trung bình |

### 6.2 Mô hình đảo chiều giảm (Bearish Reversal)

| Mô hình | Mô tả | Độ tin cậy |
|---------|-------|-------------|
| **Shooting Star (Sao băng)** | Thân nhỏ ở dưới, bóng trên dài, cuối uptrend | Trung bình |
| **Hanging Man** | Giống Hammer nhưng xuất hiện cuối uptrend | Trung bình |
| **Bearish Engulfing** | Nến giảm bao trùm hoàn toàn nến tăng trước đó | Cao |
| **Dark Cloud Cover** | Nến giảm mở trên High nến trước, đóng dưới 50% thân nến trước | Trung bình |
| **Evening Star** | 3 nến: tăng dài + thân nhỏ (gap lên) + giảm dài | Cao |
| **Three Black Crows** | 3 nến giảm liên tiếp, mỗi nến mở trong thân nến trước và đóng thấp hơn | Cao |
| **Bearish Harami** | Nến giảm nhỏ nằm trong thân nến tăng lớn | Trung bình |
| **Tweezer Top** | 2 nến có High bằng nhau, nến 1 tăng, nến 2 giảm | Trung bình |

### 6.3 Mô hình tiếp diễn (Continuation)

| Mô hình | Mô tả |
|---------|-------|
| **Doji** | Giá mở = giá đóng → thị trường do dự. Các biến thể: Long-legged Doji, Dragonfly Doji, Gravestone Doji |
| **Spinning Top** | Thân nhỏ, bóng 2 bên dài → do dự |
| **Rising/Falling Three Methods** | 1 nến dài + 3 nến nhỏ ngược chiều + 1 nến dài cùng chiều ban đầu |
| **Marubozu** | Nến không bóng → lực mua/bán áp đảo hoàn toàn |

**Phụ thuộc chung**: Vị trí trong xu hướng (rất quan trọng), khối lượng xác nhận, và khung thời gian.

---

## 7. MÔ HÌNH GIÁ (CHART PATTERNS)

### 7.1 Mô hình đảo chiều

| Mô hình | Mô tả | Đo target |
|---------|-------|-----------|
| **Head & Shoulders (Đầu & Vai)** | Đỉnh trái + đỉnh giữa cao hơn + đỉnh phải. Break neckline → BÁN | Target = Neckline - (Đỉnh đầu - Neckline) |
| **Inverse Head & Shoulders** | Ngược lại H&S. Break neckline → MUA | Target = Neckline + (Neckline - Đáy đầu) |
| **Double Top (2 đỉnh)** | Giá chạm kháng cự 2 lần không vượt được. Break support → BÁN | Target = Support - (Đỉnh - Support) |
| **Double Bottom (2 đáy)** | Giá chạm hỗ trợ 2 lần không phá được. Break resistance → MUA | Target = Resistance + (Resistance - Đáy) |
| **Triple Top / Triple Bottom** | Tương tự Double nhưng 3 lần test → mạnh hơn | Tương tự Double |
| **Rounding Bottom (Đáy tròn)** | Đảo chiều từ từ, hình chữ U | Khó đo chính xác |

### 7.2 Mô hình tiếp diễn

| Mô hình | Mô tả |
|---------|-------|
| **Flag (Cờ)** | Xu hướng mạnh + consolidation hình hình bình hành nhỏ ngược chiều + tiếp tục |
| **Pennant (Cờ đuôi nheo)** | Tương tự Flag nhưng consolidation hình tam giác nhỏ |
| **Ascending Triangle** | Kháng cự ngang + đáy cao dần → thường breakout lên |
| **Descending Triangle** | Hỗ trợ ngang + đỉnh thấp dần → thường breakdown xuống |
| **Symmetrical Triangle** | Đỉnh thấp dần + đáy cao dần → breakout theo hướng xu hướng trước đó |
| **Wedge (Nêm)** | Rising Wedge (giảm giá) / Falling Wedge (tăng giá) |
| **Rectangle** | Giá dao động giữa hỗ trợ và kháng cự ngang |
| **Cup & Handle** | Hình chữ U (cup) + pullback nhỏ (handle) → breakout lên |

**Phụ thuộc chung**: Khối lượng xác nhận breakout (Volume tăng đột biến khi break), khung thời gian, ngữ cảnh xu hướng trước đó.

---

## 8. HỖ TRỢ & KHÁNG CỰ (SUPPORT / RESISTANCE)

### 8.1 Hỗ trợ/Kháng cự ngang

- **Hỗ trợ**: Mức giá mà lực mua đủ mạnh để ngăn giá giảm thêm.
- **Kháng cự**: Mức giá mà lực bán đủ mạnh để ngăn giá tăng thêm.
- **Xác định**: Dựa trên đỉnh/đáy lịch sử, vùng giá có khối lượng lớn, số tròn (round numbers).
- **Quy tắc**: Khi bị phá vỡ, hỗ trợ trở thành kháng cự và ngược lại (Role Reversal).

### 8.2 Hỗ trợ/Kháng cự động

- **Đường trung bình**: MA(20), MA(50), MA(200) thường đóng vai trò hỗ trợ/kháng cự động.
- **Bollinger Bands**: Dải trên/dưới là kháng cự/hỗ trợ động.
- **Đường xu hướng**: Trendline cũng là hỗ trợ/kháng cự động.

### 8.3 Pivot Points

- **Công thức cơ bản**: PP = (H + L + C) / 3; R1 = 2×PP - L; S1 = 2×PP - H; R2 = PP + (H - L); S2 = PP - (H - L); R3 = H + 2×(PP - L); S3 = L - 2×(H - PP).
- **Biến thể**: Fibonacci Pivot, Camarilla Pivot, Woodie Pivot.
- **Phụ thuộc**: Dữ liệu HLC của phiên trước.
- **Phù hợp nhất**: Giao dịch trong ngày (intraday).

---

## 9. FIBONACCI

### 9.1 Fibonacci Retracement (Thoái lui)

- **Các mức chính**: 23.6%, 38.2%, 50%, 61.8%, 78.6%
- **Cách dùng**: Sau một sóng tăng/giảm, giá thường thoái lui về các mức Fibonacci trước khi tiếp tục xu hướng.
- **Mức quan trọng nhất**: 38.2% (thoái lui nông), 50% (trung bình), 61.8% (thoái lui sâu — "golden ratio").
- **Phụ thuộc**: Xác định đúng điểm Swing High và Swing Low.

### 9.2 Fibonacci Extension (Mở rộng)

- **Các mức chính**: 127.2%, 161.8%, 200%, 261.8%
- **Cách dùng**: Xác định mục tiêu giá (target) sau khi giá vượt qua vùng Fibonacci Retracement.
- **Phụ thuộc**: 3 điểm: Swing High, Swing Low, và điểm thoái lui.

### 9.3 Fibonacci Fan, Arc, Time Zones

- **Fan**: Đường thẳng từ điểm bắt đầu qua các mức Fibonacci trên đường thẳng đứng → hỗ trợ/kháng cự xiên.
- **Arc**: Đường cong bán nguyệt tại các mức Fibonacci → hỗ trợ/kháng cự theo cả giá và thời gian.
- **Time Zones**: Các đường thẳng đứng tại khoảng cách Fibonacci (1, 1, 2, 3, 5, 8, 13...) → dự đoán thời điểm đảo chiều.

---

## 10. SÓNG ELLIOTT (ELLIOTT WAVE)

### 10.1 Nguyên lý cơ bản

- **Cấu trúc**: Một chu kỳ hoàn chỉnh gồm 5 sóng đẩy (impulse: 1-2-3-4-5) + 3 sóng hiệu chỉnh (corrective: A-B-C).
- **Quy tắc bất biến**: Sóng 2 không thoái lui quá đầu sóng 1; Sóng 3 không bao giờ là sóng ngắn nhất trong 3 sóng đẩy (1, 3, 5); Sóng 4 không chồng lấn vùng giá sóng 1.

### 10.2 Các loại sóng hiệu chỉnh

- Zigzag (5-3-5), Flat (3-3-5), Triangle (3-3-3-3-3), Complex (kết hợp nhiều loại).

### 10.3 Kết hợp với Fibonacci

- Sóng 2 thường thoái lui 50-61.8% sóng 1.
- Sóng 3 thường = 161.8% sóng 1.
- Sóng 4 thường thoái lui 38.2% sóng 3.
- Sóng 5 thường = sóng 1 hoặc 61.8% sóng 1.

**Phụ thuộc**: Kinh nghiệm đếm sóng (chủ quan), kết hợp Fibonacci, khung thời gian đa lớp.

---

## 11. ICHIMOKU KINKO HYO

### 11.1 Thành phần (5 đường)

| Đường | Công thức | Ý nghĩa |
|-------|-----------|---------|
| **Tenkan-sen** (Conversion) | (Highest High 9 + Lowest Low 9) / 2 | Xu hướng ngắn hạn |
| **Kijun-sen** (Base) | (Highest High 26 + Lowest Low 26) / 2 | Xu hướng trung hạn |
| **Senkou Span A** (Leading A) | (Tenkan + Kijun) / 2, vẽ lệch 26 phiên về phía trước | Cạnh trên/dưới mây |
| **Senkou Span B** (Leading B) | (Highest High 52 + Lowest Low 52) / 2, vẽ lệch 26 phiên về phía trước | Cạnh trên/dưới mây |
| **Chikou Span** (Lagging) | Giá Close hiện tại, vẽ lùi 26 phiên | Xác nhận xu hướng |

### 11.2 Cách đọc

- **Giá trên mây** (Kumo) → Xu hướng tăng; **Giá dưới mây** → Xu hướng giảm; **Giá trong mây** → Sideway.
- **Mây xanh** (Span A > Span B) → Bullish; **Mây đỏ** (Span A < Span B) → Bearish.
- **Tenkan cắt lên Kijun** → MUA; **Tenkan cắt xuống Kijun** → BÁN.
- **Chikou Span trên giá** → Xác nhận tăng.
- **Tín hiệu mạnh nhất**: Tất cả 5 điều kiện cùng lúc đồng thuận.

**Phụ thuộc**: Các chu kỳ 9, 26, 52 (gốc từ lịch giao dịch Nhật Bản).

---

## 12. PHÂN TÍCH DÒNG TIỀN & MARKET BREADTH

### 12.1 Advance/Decline Line (A/D Line thị trường)

- **Công thức**: A/D = (Số CP tăng - Số CP giảm) + A/D hôm trước.
- **Cách đọc**: A/D Line tăng cùng index → xác nhận; A/D Line phân kỳ với index → cảnh báo.

### 12.2 McClellan Oscillator

- **Công thức**: Dựa trên EMA(19) và EMA(39) của chênh lệch (Advance - Decline).
- **Cách đọc**: > 0 → thị trường rộng mạnh; < 0 → thị trường rộng yếu.

### 12.3 TRIN (Arms Index)

- **Công thức**: TRIN = (Số CP tăng / Số CP giảm) / (Vol CP tăng / Vol CP giảm)
- **Cách đọc**: TRIN < 1 → áp lực mua; TRIN > 1 → áp lực bán; TRIN cực cao → oversold (sắp phục hồi).

### 12.4 Put/Call Ratio

- **Công thức**: Put/Call = Tổng Put Volume / Tổng Call Volume.
- **Cách đọc**: Ratio cao → bi quan (contrarian: có thể MUA); Ratio thấp → lạc quan (contrarian: cẩn trọng).
- **Phụ thuộc**: Dữ liệu quyền chọn (options market).

---

## 13. PHÂN KỲ (DIVERGENCE)

### 13.1 Phân kỳ thường (Regular Divergence) — Tín hiệu ĐẢO CHIỀU

| Loại | Giá | Chỉ báo | Tín hiệu |
|------|-----|---------|-----------|
| **Bullish Regular** | Đáy mới thấp hơn | Đáy mới cao hơn | Sắp đảo chiều TĂNG |
| **Bearish Regular** | Đỉnh mới cao hơn | Đỉnh mới thấp hơn | Sắp đảo chiều GIẢM |

### 13.2 Phân kỳ ẩn (Hidden Divergence) — Tín hiệu TIẾP DIỄN

| Loại | Giá | Chỉ báo | Tín hiệu |
|------|-----|---------|-----------|
| **Bullish Hidden** | Đáy mới cao hơn | Đáy mới thấp hơn | Xu hướng TĂNG tiếp tục |
| **Bearish Hidden** | Đỉnh mới thấp hơn | Đỉnh mới cao hơn | Xu hướng GIẢM tiếp tục |

**Áp dụng với**: RSI, MACD, Stochastic, CCI, OBV, MFI — bất kỳ oscillator nào.

**Phụ thuộc**: Cần xác nhận bằng price action hoặc volume; phân kỳ trên khung thời gian lớn hơn mạnh hơn.

---

## 14. KỸ THUẬT PRICE ACTION

### 14.1 Khái niệm

Price Action là phương pháp giao dịch dựa thuần túy trên hành động giá (nến, mô hình, hỗ trợ/kháng cự) mà KHÔNG dùng chỉ báo kỹ thuật.

### 14.2 Các setup phổ biến

- **Pin Bar**: Nến có bóng dài, thân nhỏ — tín hiệu rejection tại vùng quan trọng.
- **Inside Bar**: Nến nằm hoàn toàn trong range nến trước → chuẩn bị breakout.
- **Outside Bar (Engulfing)**: Nến bao trùm nến trước → tín hiệu đảo chiều mạnh.
- **Fakey (False Breakout)**: Breakout giả của Inside Bar → tín hiệu đảo chiều rất mạnh.
- **Break of Structure (BOS)**: Khi giá phá vỡ đỉnh/đáy trước đó → xác nhận thay đổi xu hướng.
- **Change of Character (CHOCH)**: Tín hiệu đầu tiên cho thấy xu hướng có thể đổi.

### 14.3 Smart Money Concepts (SMC)

- **Order Block**: Nến cuối cùng trước khi giá tạo chuyển động mạnh → vùng institutional entry.
- **Fair Value Gap (FVG/Imbalance)**: Khoảng trống giữa 3 nến liên tiếp → giá thường quay lại lấp.
- **Liquidity Sweep**: Giá quét qua đỉnh/đáy trước để lấy thanh khoản rồi đảo chiều.
- **Premium/Discount Zone**: Dùng Fibonacci 50% chia range thành vùng Premium (trên) và Discount (dưới).

**Phụ thuộc**: Kỹ năng đọc nến, hiểu cấu trúc thị trường, kinh nghiệm.

---

## 15. BẢNG TỔNG HỢP: KỸ THUẬT NÀO PHÙ HỢP KHUNG THỜI GIAN NÀO

| Khung thời gian | Kỹ thuật phù hợp nhất | Ghi chú |
|-----------------|----------------------|---------|
| **Scalping (1-5 phút)** | VWAP, EMA(9/21), Stochastic, Volume Profile, Price Action, Tape Reading | Cần tốc độ cao, spread thấp |
| **Day Trading (15-60 phút)** | EMA, MACD, RSI, Bollinger, Pivot Points, VWAP, Candlestick Patterns | Không giữ qua đêm |
| **Swing Trading (4H - Daily)** | SMA/EMA, MACD, RSI, Fibonacci, Chart Patterns, Ichimoku, ADX | Giữ vài ngày - vài tuần |
| **Position Trading (Daily - Weekly)** | SMA(50/200), Elliott Wave, Fibonacci, Chart Patterns, ADX | Giữ vài tuần - vài tháng |

---

## 16. KẾT HỢP CÁC KỸ THUẬT — HỆ THỐNG GIAO DỊCH HOÀN CHỈNH

### 16.1 Nguyên tắc kết hợp

Không bao giờ dùng chỉ một kỹ thuật duy nhất. Nên kết hợp theo nguyên tắc:

1. **Xác định xu hướng lớn**: MA dài hạn, ADX, Ichimoku → "nên mua hay bán?"
2. **Tìm vùng vào lệnh**: Hỗ trợ/Kháng cự, Fibonacci, Bollinger Bands → "vào lệnh ở đâu?"
3. **Xác nhận timing**: RSI, MACD, Stochastic, Candlestick → "vào lệnh khi nào?"
4. **Xác nhận bằng khối lượng**: OBV, MFI, Volume Profile → "dòng tiền có ủng hộ không?"
5. **Quản lý rủi ro**: ATR, Parabolic SAR → "đặt stop-loss ở đâu?"

### 16.2 Ví dụ hệ thống Swing Trading

- **Bước 1**: EMA(50) hướng lên + giá trên EMA(50) → xu hướng tăng, chỉ tìm MUA.
- **Bước 2**: Giá pullback về Fibonacci 38.2-61.8% hoặc EMA(21).
- **Bước 3**: RSI < 40 sau đó quay lên > 40 + nến Bullish Engulfing → MUA.
- **Bước 4**: Khối lượng tăng khi nến xác nhận → thêm tin cậy.
- **Bước 5**: Stop-Loss = Swing Low gần nhất hoặc Entry - 2×ATR(14).
- **Bước 6**: Target = Fibonacci Extension 127.2% hoặc Risk:Reward ≥ 1:2.

### 16.3 Phân tích đa khung thời gian (Multi-Timeframe Analysis)

- **Khung lớn** (Daily/Weekly): Xác định xu hướng chính.
- **Khung trung** (4H): Xác định vùng giao dịch.
- **Khung nhỏ** (1H/15min): Tìm điểm vào lệnh chính xác.
- **Quy tắc**: Chỉ giao dịch theo hướng khung thời gian lớn hơn.

---

## TỔNG KẾT CÁC YẾU TỐ PHỤ THUỘC CHUNG

Mọi kỹ thuật phân tích đều phụ thuộc vào các yếu tố sau:

1. **Dữ liệu đầu vào**: OHLCV (Open, High, Low, Close, Volume) — chất lượng dữ liệu quyết định chất lượng phân tích.
2. **Khung thời gian**: Cùng một chỉ báo cho tín hiệu khác nhau trên các khung khác nhau.
3. **Tham số (Parameters)**: Chu kỳ, ngưỡng, hệ số — cần tối ưu cho từng thị trường/cổ phiếu.
4. **Thanh khoản**: Phân tích kỹ thuật hoạt động tốt hơn trên cổ phiếu có thanh khoản cao.
5. **Điều kiện thị trường**: Trending vs Sideway — mỗi loại chỉ báo phù hợp với một điều kiện.
6. **Tâm lý thị trường**: Phân tích kỹ thuật phản ánh hành vi đám đông; khi có tin tức bất ngờ, kỹ thuật có thể thất bại.
7. **Quản lý vốn & rủi ro**: Không có kỹ thuật nào đúng 100%. Luôn cần Stop-Loss và Position Sizing.

---

*Lưu ý: Tài liệu này mang tính giáo dục. Giao dịch chứng khoán luôn có rủi ro. Không có phương pháp phân tích kỹ thuật nào đảm bảo lợi nhuận 100%. Hãy luôn kết hợp với quản lý rủi ro nghiêm ngặt và cân nhắc tham khảo ý kiến chuyên gia tài chính trước khi đưa ra quyết định đầu tư.*
