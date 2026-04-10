# Chiến lược giao dịch

> Tổng hợp 7 chiến lược giao dịch phổ biến và cách kết hợp chỉ báo kỹ thuật.

---

## 7 chiến lược chính

### 1. Scalping (1–5 phút)

**Mục tiêu:** Lợi nhuận nhỏ, tần suất cao.

| Vai trò | Chỉ báo |
|---------|---------|
| Xu hướng | EMA(9/21), VWAP |
| Timing | Stochastic(5,3,3), Pin Bar |
| Xác nhận | Volume đột biến |
| R:R | ≥ 1:1.5 |
| SL | 0.1–0.3% |

---

### 2. Day Trading (15–60 phút)

**Mục tiêu:** Đóng tất cả lệnh trong ngày.

| Vai trò | Chỉ báo |
|---------|---------|
| Xu hướng | EMA(20/50), VWAP |
| Vùng vào | Pivot Points, Bollinger, Fibonacci |
| Timing | RSI(14), MACD, Candlestick |
| Volume | MFI(14), Volume Profile |
| R:R | ≥ 1:2 |
| SL | ATR × 1.5 |

---

### 3. Swing Trading (Daily) — Phổ biến nhất

**Mục tiêu:** Bắt sóng trung hạn, giữ 5–20 ngày.

| Vai trò | Chỉ báo |
|---------|---------|
| Xu hướng | SMA(50/200), ADX(14), Ichimoku |
| Vùng vào | Fibonacci 38.2–61.8%, EMA(21) |
| Timing | RSI pullback 40–50, MACD cross, nến đảo chiều |
| Xác nhận | OBV, CMF(20) |
| R:R | ≥ 1:2 đến 1:3 |
| SL | ATR × 2 hoặc Swing Low |

**Kế hoạch mẫu:**
1. Cuối tuần: scan CP uptrend (SMA 50/200, ADX > 25)
2. Hàng ngày: tìm pullback về Fibonacci/EMA(21)
3. Tín hiệu: RSI bật, MACD cross, nến đảo chiều + volume
4. Vào lệnh → SL dưới Swing Low → TP tại Fibo Extension

---

### 4. Position Trading (Weekly)

**Mục tiêu:** Bắt xu hướng lớn, giữ 1–6 tháng.

| Vai trò | Chỉ báo |
|---------|---------|
| Xu hướng | SMA(50/200), ADX Weekly, Ichimoku |
| Vùng vào | Fibonacci Weekly, SMA(50) |
| Timing | MACD Weekly, RSI Monthly |
| R:R | ≥ 1:3 đến 1:5 |
| SL | ATR × 3 Weekly |

---

### 5. Breakout Trading

**Mục tiêu:** Vào lệnh khi giá phá vùng tích luỹ.

| Vai trò | Chỉ báo |
|---------|---------|
| Tích luỹ | Bollinger Squeeze, Triangle/Flag |
| Breakout | Nến Marubozu phá kháng cự |
| Xác nhận | Volume ≥ 150% trung bình, ADX tăng |
| SL | Dưới vùng breakout |
| Target | Chiều cao vùng tích luỹ × 1–2 |

---

### 6. Mean Reversion (Hồi quy trung bình)

**Mục tiêu:** Mua khi giá quá xa trung bình, kỳ vọng quay về.

| Vai trò | Chỉ báo |
|---------|---------|
| Quá xa | Bollinger dải dưới, RSI < 20, CCI < -200 |
| Xác nhận | Stochastic cross, nến đảo chiều |
| **Bắt buộc** | ADX < 25 (chỉ hiệu quả khi sideway) |
| Target | Quay về MA(20) hoặc Bollinger giữa |
| R:R | 1:1 đến 1:2 |

---

### 7. Momentum Trading

**Mục tiêu:** Mua CP có momentum mạnh nhất.

| Vai trò | Chỉ báo |
|---------|---------|
| Đo momentum | ROC(12), RSI 60–80, MACD Histogram |
| Xác nhận | ADX > 30, giá trên EMA(10/20) |
| Thoát | MACD histogram giảm 3 phiên hoặc RSI phân kỳ |
| Trailing | EMA(10) hoặc ATR trailing |

---

## Bảng chỉ báo × chiến lược

| Chỉ báo | Scalp | Day | Swing | Position | Breakout | Reversion | Momentum |
|---------|:-----:|:---:|:-----:|:--------:|:--------:|:---------:|:--------:|
| EMA ngắn | ★★★ | ★★★ | ★★ | — | ★ | — | ★★ |
| SMA dài | — | ★ | ★★★ | ★★★ | ★★ | — | ★★ |
| RSI | ★ | ★★★ | ★★★ | ★★ | ★ | ★★★ | ★★★ |
| MACD | — | ★★★ | ★★★ | ★★★ | ★★ | ★ | ★★★ |
| Stochastic | ★★★ | ★★ | ★★★ | ★ | — | ★★★ | — |
| Bollinger | ★ | ★★★ | ★★★ | ★ | ★★★ | ★★★ | — |
| ADX | — | ★★ | ★★★ | ★★★ | ★★★ | ★★★ | ★★★ |
| OBV/MFI | ★ | ★★ | ★★★ | ★★★ | ★★★ | ★★ | ★★★ |
| ATR | ★★ | ★★★ | ★★★ | ★★★ | ★★★ | ★★ | ★★ |

★★★ = Rất phù hợp · ★★ = Phù hợp · ★ = Có thể dùng · — = Không phù hợp

---

## 10 nguyên tắc vàng

1. **Không giao dịch không có Stop-Loss** — quy tắc sống còn
2. **Xu hướng là bạn** — giao dịch theo khung thời gian lớn hơn
3. **Volume xác nhận tất cả** — breakout không volume = false breakout
4. **R:R tối thiểu 1:2** — thắng 40% vẫn có lãi
5. **Không quá 5% rủi ro cùng lúc** — bảo toàn vốn là số 1
6. **Phân tích đa khung thời gian** — nhìn bức tranh lớn trước
7. **Ghi nhật ký** — ghi lại mọi giao dịch để cải thiện
8. **Backtest trước** — ≥ 100 giao dịch demo trước khi dùng tiền thật
9. **Kỷ luật tâm lý** — tuân thủ kế hoạch, không FOMO
10. **Đơn giản hoá** — chọn 3–5 chỉ báo phù hợp và thành thạo chúng
