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

## Kỳ vọng theo R của từng chiến lược

Trang **Chiến lược** (tab Hiệu suất) và trang **Phân tích** (tab Thống kê giao dịch) có khối "Kỳ vọng theo R (chiến dịch đã đóng)". Mỗi dòng là một chiến lược (kế hoạch không gắn chiến lược nằm ở dòng riêng):

- **Mẫu (n):** số chiến dịch Mua đã review có R. Dưới 10 thì khối ghi "chưa đủ mẫu để tin": đừng rút kết luận từ vài lệnh.
- **% thắng (95%):** thắng khi R > +0,1, thua khi R < −0,1; lệnh hoà nằm ngoài mẫu số và hiện riêng. Khoảng trong ngoặc là khoảng tin cậy 95%: mẫu càng nhỏ khoảng càng rộng.
- **Kỳ vọng R:** R trung bình của mọi chiến dịch đã đóng, kể cả hoà. Dương nghĩa là về lâu dài mỗi lệnh kỳ vọng lãi trung bình bấy nhiêu lần mức rủi ro ban đầu.
- **PF (R):** tổng R thắng ÷ tổng R thua; n/a khi chưa có lệnh thua.
- Các chỉ số tiền ở phía trên (tỷ lệ thắng, P&L, Profit Factor theo tiền) tính **theo lệnh** và gồm cả lãi/lỗ chưa chốt, nên không so trực tiếp với khối này.

## Độ khớp ước lượng

Khi lập kế hoạch Mua, bạn có thể nhập các kịch bản và xác suất (tổng 100%) để hệ thống tính % thắng ngầm định. Khối **Độ khớp ước lượng** (cùng chỗ với khối kỳ vọng theo R) hỏi ngược lại: những lần bạn tự tin 70%, thực tế thắng bao nhiêu?

- **4 mức ước lượng:** dưới 40%, 40–55%, 55–70%, 70–100%. Mỗi dòng cho ước lượng trung bình, tỉ lệ thắng thật và số mẫu n. Dòng bị làm mờ và ghi "chưa đủ mẫu" khi n dưới 5: đừng đọc con số ở đó.
- **Chỉ tính** kế hoạch Mua đã review và có ước lượng được **khoá khi khớp lệnh đầu tiên**; chiến dịch hoà (|R| ≤ 0,1) không tính vì không có đúng hay sai để chấm. Kế hoạch có ước lượng nhưng chưa khớp lệnh thì chưa vào.
- **Brier score** (chỉ hiện khi có ít nhất 15 chiến dịch thắng/thua): trung bình bình phương sai lệch giữa xác suất bạn nêu và kết quả (thắng = 1, thua = 0). 0 là hoàn hảo; **0,25 tương đương luôn đoán 50/50**, nên Brier trên 0,25 nghĩa là ước lượng chưa hơn việc tung đồng xu. Càng thấp càng tốt.
- **Nhận xét lạc quan/bi quan:** chênh lệch giữa ước lượng trung bình và tỉ lệ thắng thật, tính bằng điểm %. Dương là bạn hay ước cao hơn thực tế.
- Chưa có kế hoạch nào đủ điều kiện thì khối báo trạng thái rỗng: cần kế hoạch có kịch bản xác suất và đã review.

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
