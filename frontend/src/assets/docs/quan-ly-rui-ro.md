# Quản lý rủi ro

> Thiết lập hồ sơ rủi ro, theo dõi drawdown, cảnh báo, và tính position size.

---

## Risk Profile (`/risk`)

Thiết lập giới hạn rủi ro cá nhân:

| Thông số | Mô tả | Giá trị gợi ý |
|----------|-------|----------------|
| **% rủi ro mỗi lệnh** | Tối đa bao nhiêu % vốn có thể mất nếu lệnh thua | 1–2% |
| **% tối đa một vị thế** | Giá trị 1 cổ phiếu không vượt bao nhiêu % danh mục | 10–20% |
| **Drawdown tối đa** | Ngưỡng thua lỗ tích luỹ cần dừng giao dịch | 10–15% |
| **Số vị thế tối đa** | Đa dạng hoá — không nắm quá nhiều mã cùng lúc | 4–8 |

Hệ thống sẽ cảnh báo khi bạn vi phạm các giới hạn này.

---

## Risk Dashboard (`/risk-dashboard`)

Bảng điều khiển rủi ro tổng quan:

### Chỉ số chính

| Chỉ số | Ý nghĩa |
|--------|---------|
| **VaR (95%)** | Value at Risk — số tiền có thể mất trong 1 ngày với xác suất 95% |
| **Max Drawdown** | Mức thua lỗ lớn nhất từ đỉnh đến đáy trong lịch sử |
| **Win Rate** | Tỷ lệ giao dịch có lãi / tổng giao dịch |
| **Profit Factor** | Tổng lãi / Tổng lỗ — > 1.5 là tốt |

### Cảnh báo tự động

Dashboard hiển thị cảnh báo khi:

- **Stop-loss gần ngưỡng**: ≤ 5% → warning (vàng), ≤ 2% → danger (đỏ)
- **Drawdown vượt mức**: > 10% → warning, > 20% → danger
- **Tập trung danh mục**: Một vị thế chiếm quá nhiều % → cảnh báo

---

## Position Sizing

Tính số cổ phiếu nên mua dựa trên rủi ro:

### Công thức Fixed % Risk

```
Rủi ro mỗi lệnh = Tổng vốn × % Rủi ro
Số CP = Rủi ro mỗi lệnh ÷ (Giá vào - Stop-Loss)
```

**Ví dụ:** Vốn 100 triệu, rủi ro 2%, Entry = 50,000đ, SL = 47,000đ
- Rủi ro = 100tr × 2% = 2 triệu
- Số CP = 2,000,000 ÷ 3,000 = 666 CP → làm tròn 600 CP (lô chẵn)

### Position size tự tính trong Trade Plan

Khi bạn nhập Entry và SL trong Kế hoạch GD, hệ thống tự tính:
- Số CP gợi ý
- Giá trị lệnh
- % danh mục
- R:R ratio

---

## Stop-Loss

### Nguyên tắc vàng

> **Không bao giờ giao dịch mà không có Stop-Loss**

### Các phương pháp đặt SL

| Phương pháp | Cách tính | Phù hợp |
|-------------|-----------|---------|
| **Fixed %** | SL = Entry × (1 - X%) | Người mới (3–7%) |
| **ATR** | SL = Entry - k × ATR(14) | Swing Trading (k=2) |
| **Swing Low** | SL dưới đáy gần nhất | Price Action |
| **EMA** | SL dưới EMA(21) hoặc SMA(50) | Trend Following |

### Trailing Stop

Bảo vệ lợi nhuận bằng cách nâng SL theo giá:
- Khi lệnh có lời → nâng SL lên
- Không bao giờ hạ SL ngược lại
- Chấp nhận bị stopped out → giữ phần lớn lợi nhuận

---

## R:R Ratio (Risk:Reward)

Tỷ lệ lời/lỗ tiềm năng:

```
R:R = (Target - Entry) / (Entry - Stop-Loss)
```

| R:R | Ý nghĩa | Win Rate cần thiết |
|-----|---------|-------------------|
| 1:1 | Lời = Lỗ | > 50% |
| 1:2 | Lời gấp đôi lỗ | > 33% |
| 1:3 | Lời gấp ba lỗ | > 25% |

**Quy tắc**: Chỉ vào lệnh khi R:R ≥ 1:2.

---

## Correlation Matrix

Ma trận tương quan giữa các cổ phiếu trong danh mục:
- Tương quan cao (>0.7) = rủi ro tập trung
- Nên đa dạng hoá bằng các mã có tương quan thấp

---

## Mẹo quản lý rủi ro

1. **Không quá 5% rủi ro tổng danh mục cùng lúc**
2. **Phân bổ 4–8 cổ phiếu khác ngành**
3. **Nếu lỗ 6–10% trong tháng → dừng giao dịch, xem lại chiến lược**
4. **Luôn xác định SL trước khi vào lệnh**
5. **Position size tự động → tin tưởng hệ thống, không tự ý tăng size**
