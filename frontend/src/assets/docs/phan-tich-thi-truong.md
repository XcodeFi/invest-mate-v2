# Phân tích thị trường

> Tra cứu giá cổ phiếu, phân tích kỹ thuật 10 chỉ báo, tín hiệu mua/bán tổng hợp.

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

## Smart Signals — 10 chỉ báo kỹ thuật

Khi tra cứu mã CP, hệ thống tự động phân tích 10 chỉ báo:

### Nhóm Xu hướng

| Chỉ báo | Đọc thế nào |
|---------|-------------|
| **EMA (20/50)** | EMA20 > EMA50 = xu hướng TĂNG; ngược lại = GIẢM |
| **ADX (14)** | > 25 = có xu hướng rõ; < 20 = đi ngang; > 40 = rất mạnh. +DI > -DI = hướng tăng |

### Nhóm Động lượng

| Chỉ báo | Đọc thế nào |
|---------|-------------|
| **RSI (14)** | < 30 = quá bán (cơ hội mua); > 70 = quá mua (cẩn trọng) |
| **MACD** | MACD cắt lên Signal = tín hiệu MUA; cắt xuống = BÁN |
| **Stochastic** | < 20 = quá bán; > 80 = quá mua. Nhạy hơn RSI |

### Nhóm Khối lượng

| Chỉ báo | Đọc thế nào |
|---------|-------------|
| **Volume** | Đột biến (>2x TB) = xác nhận tín hiệu; Thấp = thiếu dòng tiền |
| **OBV** | Tăng = smart money mua; Giảm = smart money bán |
| **MFI (14)** | Giống RSI nhưng tính cả volume → phản ánh dòng tiền thực tế |

### Nhóm Biến động

| Chỉ báo | Đọc thế nào |
|---------|-------------|
| **Bollinger** | Nén (Squeeze) = sắp biến động mạnh; Phá dải = xu hướng mạnh |
| **ATR (14)** | Đo biến động — dùng tính SL: SL = Entry ± 2 × ATR |

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
- **Fibonacci**: Retracement (23.6%–78.6%) + Extension (127.2%, 161.8%)

---

## Gợi ý giao dịch

Khi đủ dữ liệu, hệ thống gợi ý:
- **Entry**: Mức hỗ trợ gần nhất
- **Stop-Loss**: Mức hỗ trợ thứ 2 hoặc -5%
- **Target**: Mức kháng cự gần nhất
- **R:R**: Tỷ lệ reward/risk

Nhấn **"Tạo Trade Plan từ gợi ý"** → chuyển đến trang Kế hoạch với thông tin đã điền sẵn.
