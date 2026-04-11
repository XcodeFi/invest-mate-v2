# Plan: Mở rộng Phân tích Kỹ thuật & Chiến lược Giao dịch

> **Ngày tạo:** 2026-04-10
> **Trạng thái:** P1-P6 Done ✅ — Roadmap hoàn thành
> **Reference:** [`docs/references/`](../references/README.md) — 3 tài liệu kiến thức nền tảng

---

## Bối cảnh

Hiện tại `TechnicalIndicatorService` chỉ cung cấp 4 chỉ báo cơ bản (EMA 20/50, RSI 14, MACD 12/26/9, Volume ratio + S/R). Theo tài liệu tham chiếu, một hệ thống giao dịch hoàn chỉnh cần **tối thiểu 3-5 chỉ báo kết hợp** theo flow:

```
Xu hướng → Vùng vào lệnh → Timing → Xác nhận Volume → Quản lý Rủi ro
```

Ngoài ra, Trade Plan chỉ hỗ trợ Fixed % Risk cho position sizing và basic SL — thiếu nhiều mô hình quan trọng.

---

## Gap Analysis: Hiện có vs Cần có

| Khía cạnh | Hiện có | Cần bổ sung |
|-----------|---------|-------------|
| **Trend Indicators** | EMA(20/50) | Bollinger Bands, ADX, Parabolic SAR |
| **Momentum** | RSI(14), MACD(12,26,9) | Stochastic, Williams %R, CCI |
| **Volume** | Volume ratio | OBV, MFI, CMF |
| **Volatility** | ATR (backend, chưa expose FE) | Bollinger Bands, Keltner Channel |
| **Signal** | Đơn lẻ (Mua/Bán/Chờ) | Confluence scoring, divergence detection |
| **Position Sizing** | Fixed % Risk | Kelly Criterion, ATR-Based, Turtle |
| **Stop Loss** | Basic SL + Trailing | ATR SL, Structure SL, MA SL, Chandelier Exit |
| **Strategy** | Manual selection | Pre-built templates (7 chiến lược), auto-suggest |

---

## Lộ trình 6 Phase

### P1 — Mở rộng Technical Indicators

> **Mục tiêu:** Tăng từ 4 lên 9+ chỉ báo, đủ cover 4 nhóm chính (Trend, Momentum, Volume, Volatility)
> **Effort:** Medium | **Ref:** Tài liệu #2 (Phân loại chỉ báo), #3 (Công thức)

| Feature | Mô tả | Backend | Frontend |
|---------|--------|---------|----------|
| **Bollinger Bands** | SMA(20) ± 2σ, Squeeze detection (bandwidth < threshold) | `TechnicalIndicatorService` | Hiển thị dải + alert Squeeze |
| **Stochastic Oscillator** | %K(14,3), %D(3), quá mua(>80)/quá bán(<20) | `TechnicalIndicatorService` | Card trong Smart Signals |
| **ADX + DI** | ADX(14), +DI, -DI → trending (>25) vs sideway (<20) | `TechnicalIndicatorService` | Badge "Trending"/"Sideway" |
| **OBV** | Tích lũy volume theo hướng giá, phát hiện phân kỳ | `TechnicalIndicatorService` | Card + divergence alert |
| **MFI** | RSI có volume, MFI(14), quá mua(>80)/quá bán(<20) | `TechnicalIndicatorService` | Card trong Smart Signals |

**Dependencies:** Cần history data (OHLCV ≥ 50 phiên) từ `HmoneyMarketDataProvider`.

---

### P2 — Multi-Indicator Signal Scoring (Confluence)

> **Mục tiêu:** Kết hợp nhiều chỉ báo thành 1 điểm tổng hợp, phát hiện phân kỳ tự động
> **Effort:** Large | **Ref:** Tài liệu #1 (Phần II — flow 5 bước), #3 (Phần 16 — kết hợp kỹ thuật)

| Feature | Mô tả | Effort |
|---------|--------|--------|
| **Confluence Score** | Tổng hợp 5+ indicators → điểm 0-100 theo trọng số. Thay thế signal đơn lẻ hiện tại | Large |
| **Market Condition Classifier** | ADX < 20 → Sideway → gợi ý Mean Reversion; ADX > 25 → Trending → gợi ý Trend Following | Medium |
| **Divergence Detection** | Auto-detect Regular + Hidden divergence trên RSI/MACD vs Price | Medium |
| **Multi-Timeframe View** | Hiển thị signal trên 3 khung (Weekly/Daily/H4) cùng lúc | Medium |

**Flow kết hợp (từ tài liệu):**
1. Xu hướng (EMA/ADX/Ichimoku) → "Nên mua hay bán?"
2. Vùng vào lệnh (S/R, Fibonacci, Bollinger) → "Vào ở đâu?"
3. Timing (RSI, MACD, Stochastic, Nến) → "Vào khi nào?"
4. Xác nhận Volume (OBV, MFI) → "Dòng tiền có ủng hộ?"
5. Risk (ATR) → "SL ở đâu, size bao nhiêu?"

---

### P3 — Advanced Position Sizing Calculator

> **Mục tiêu:** Cung cấp nhiều mô hình sizing, tự động điều chỉnh theo biến động
> **Effort:** Medium | **Ref:** Tài liệu #1 (Phần I.1 — 5 mô hình)

| Feature | Mô tả | Công thức |
|---------|--------|-----------|
| **Fixed % Risk** (có sẵn) | Mỗi lệnh rủi ro tối đa X% vốn | `Size = (Vốn × %Risk) / (Entry - SL)` |
| **ATR-Based Sizing** | Tự điều chỉnh theo biến động | `Size = (Vốn × %Risk) / (N × ATR)` |
| **Kelly Criterion** | Sizing tối ưu dựa trên trade history | `f* = (bp - q) / b` → dùng Half-Kelly |
| **Turtle Sizing** | Unit-based với giới hạn exposure | `1 Unit = 1% vốn / (N × ATR)`, max 4 units/CP |
| **Volatility-Adjusted** | ATR cao → giảm size, ATR thấp → tăng size | Auto-scale based on ATR percentile |

**UI:** Dropdown chọn model trong Trade Plan → auto-calculate → hiển thị so sánh kết quả các model.

---

### P4 — Advanced Stop Loss & Trailing Stop

> **Mục tiêu:** Đa dạng hóa phương pháp SL, trailing stop bảo vệ lợi nhuận
> **Effort:** Medium | **Ref:** Tài liệu #1 (Phần I.2 — 6 loại SL, I.2.5 — 5 loại trailing)

| Feature | Mô tả | Logic |
|---------|--------|-------|
| **SL Method Selector** | Chọn Fixed/ATR/Structure/MA trong Trade Plan | Dropdown → auto-calc SL price |
| **ATR Stop Loss** | `SL = Entry - k × ATR(14)`, k = 1.5 (ngắn hạn) / 2.0 (trung hạn) / 3.0 (dài hạn) | Cần ATR data |
| **Chandelier Exit** | `SL = Highest High(22) - 3 × ATR(22)` | Trailing variant |
| **MA Trailing** | SL = EMA(21) hoặc SMA(50) | Thoát khi giá đóng dưới MA |
| **Time Stop Alert** | Cảnh báo nếu lệnh không lời sau X phiên | Worker job |

**Tích hợp:** Trailing stop types feed vào `RiskCalculationService.GetTrailingStopAlerts()` hiện có.

---

### P5 — Strategy Template Library

> **Mục tiêu:** Pre-built 7 chiến lược với indicator combos, R:R, SL method, checklist
> **Effort:** Large | **Ref:** Tài liệu #1 (Phần II — 7 chiến lược chi tiết)

| Strategy | Indicators | R:R | SL Method | Timeframe |
|----------|-----------|-----|-----------|-----------|
| **Scalping** | EMA(9/21), VWAP, Stochastic(5,3,3) | ≥ 1:1.5 | Fixed 0.1-0.3% | 1-5 phút |
| **Day Trading** | EMA(20/50), VWAP, RSI(14), MACD, Pivot Points, Bollinger | ≥ 1:2 | ATR × 1.5 | 15-60 phút |
| **Swing Trading** | SMA(50/200), ADX, Fibonacci, RSI, MACD, OBV | ≥ 1:2-3 | ATR × 2 hoặc Swing Low | Daily |
| **Position Trading** | SMA(50/200), ADX Weekly, Ichimoku, MACD Weekly | ≥ 1:3-5 | ATR × 3 Weekly | Weekly |
| **Breakout** | Bollinger Squeeze, Donchian, ADX, Volume spike | ≥ 1:2 | Dưới vùng breakout | Multi |
| **Mean Reversion** | Bollinger, RSI < 20, CCI, Stochastic, ADX < 25 | 1:1-2 | ATR × 1.5 | Multi |
| **Momentum** | ROC(12), RSI 60-80, MACD Histogram, ADX > 30 | ≥ 1:2 | EMA(10) trailing | Multi |

**UI Flow:** Chọn Strategy → auto-fill trong Trade Plan: indicator combo, R:R, SL method, checklist items, position sizing model.

---

### P6 — Dynamic Trading Checklist

> **Mục tiêu:** Checklist thay đổi theo strategy type + bắt buộc multi-timeframe confirmation
> **Effort:** Medium | **Ref:** Tài liệu #1 (Kế hoạch giao dịch từng chiến lược)

| Feature | Mô tả |
|---------|--------|
| **Strategy-Based Checklist** | Swing → check Fibonacci + Weekly trend + OBV; Day → check VWAP + Pivot + MFI |
| **Multi-Timeframe Gate** | Bắt buộc xác nhận xu hướng khung lớn trước khi approve lệnh khung nhỏ |
| **Pre-trade Scoring** | Mỗi checklist item có điểm → tổng điểm quyết định GO/NO-GO threshold |

---

## Dependency Graph

```
P1 (Indicators)  ──→  P2 (Confluence + Divergence)  ──→  P5 (Strategy Templates)
       │                                                          │
       └──→  P3 (Position Sizing)  ──→  P4 (SL/Trailing)  ──→  P6 (Dynamic Checklist)
```

- **P1 là nền tảng** — tất cả phase sau phụ thuộc vào indicator data
- **P2 cần P1** — confluence scoring cần nhiều indicators
- **P3-P4 song song** với P2, chỉ cần ATR từ P1
- **P5-P6 cuối** — cần tất cả building blocks phía trước

---

## Technical Notes

- **Backend:** Mở rộng `TechnicalIndicatorService` + `ITechnicalIndicatorService` interface
- **Data requirement:** Cần OHLCV history ≥ 200 phiên cho SMA(200), ≥ 52 phiên cho Ichimoku
- **API:** Mở rộng `/market/stock/{symbol}/analysis` response
- **Frontend:** Mở rộng Smart Signals section trong `market-data.component.ts`
- **Testing:** Mỗi indicator cần unit test với known data → expected output (sử dụng TDD)

---

*Tài liệu tham chiếu chi tiết: [`docs/references/`](../references/README.md)*
