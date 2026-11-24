# Biểu đồ chỉ báo theo nhóm trên panel Phân tích kỹ thuật — Design

**Ngày:** 2026-11-24
**Trạng thái:** User đã duyệt 2026-11-24. Sau khi duyệt, dev lane chép file này vào `docs/superpowers/specs/2026-11-24-ta-group-chart-design.md` trên nhánh feature.
**Trang:** `/market-data` (Tra cứu giá), [market-data.component.ts](../../../frontend/src/app/features/market-data/market-data.component.ts), panel "Phân tích kỹ thuật"

## Ý định

Panel phân tích kỹ thuật đã chia 6 khối (PR #172): Tổng hợp, Xu hướng, Động lượng, Biến động, Dòng tiền, Mốc giá. Nhưng mỗi thẻ chỉ hiện **một con số của phiên cuối**. Người dùng muốn **bấm vào một nhóm để thấy các chỉ báo của nhóm đó vẽ thành đường trên biểu đồ giá**, tức là thấy cả diễn biến chứ không chỉ một điểm.

## Quyết định của user (2026-11-24)

1. **Chuỗi số liệu do backend trả về.** Frontend không tự tính lại. Lý do: điểm cuối của mỗi đường phải khớp con số trên thẻ, vì cùng một công thức. AI/MCP sau này có thể dùng lại.
2. **Mỗi lần chỉ một nhóm.** Bấm nhóm nào thì biểu đồ vẽ nhóm đó; nến giá luôn có.

## Approaches đã cân nhắc

| Cách | Đánh đổi | Kết quả |
|---|---|---|
| A. Backend trả chuỗi | Một nguồn sự thật, đường khớp thẻ. Tốn công tách hàm tính hiện chỉ trả giá trị cuối | **Chọn** |
| B. Frontend tự tính (`indicators.ts`, như spec Nhật ký 2026-08-13) | Nhanh, không đụng backend. Nhưng công thức lệch backend (seed EMA, Wilder RSI) làm đường khác thẻ, mất niềm tin | Loại |
| C. Lát mỏng: chỉ nến + Mốc giá | Rẻ, nhưng không trả lời được yêu cầu chính | Loại |

## Goals

- Bấm tiêu đề một khối (`[data-group]`) thì biểu đồ nến phía trên các khối vẽ chỉ báo của khối đó. Bấm lại khối đang chọn thì về chỉ còn nến.
- Điểm cuối mỗi đường = giá trị trên thẻ cùng tên (sai số làm tròn hiển thị).
- Chỉ báo nằm trên thang giá (EMA, Bollinger, mốc giá) vẽ đè lên nến. Chỉ báo có thang riêng (RSI, MACD, ADX, ATR, OBV, MFI, Stochastic) vẽ ở tầng dưới, đồng bộ trục thời gian và crosshair với tầng giá.

## Non-goals

- Không vẽ EMA200 (nguồn 24hmoney chỉ khoảng 67 ngày lịch sử, xem CHANGELOG và memory `learning_pitfall_dto_field_exists_but_source_starves_it`).
- Không bật nhiều nhóm cùng lúc.
- Không nâng `lightweight-charts` lên v5.
- Không đổi payload endpoint phân tích hiện có (`GET /stock/{symbol}/analysis`). Thẻ, MCP và AI giữ nguyên.
- Không làm biểu đồ cho trang Nhật ký ở việc này. Phần lõi vẽ nhiều tầng nên viết sao cho trang đó dùng lại được sau.

## Anti-goals (làm vậy là hỏng dù chạy được)

- Đường vẽ ra lệch con số trên thẻ.
- Biểu đồ trống trơn hoặc nổ khi mã có ít dữ liệu. Phải hiện "Không đủ dữ liệu" cho từng chỉ báo thiếu điểm.
- Bấm tiêu đề khối làm hỏng hành động khác trong khối. Nút "Tạo Trade Plan" trong khối Tổng hợp phải vẫn bấm được (xem luật stopPropagation trong CLAUDE.md).

## Ràng buộc

1. `lightweight-charts` v4 (đã là dependency) chưa có pane gốc, nên mỗi tầng là một instance chart xếp dọc, đồng bộ `timeScale` và crosshair. Code mẫu: [symbol-timeline.component.ts](../../../frontend/src/app/features/symbol-timeline/symbol-timeline.component.ts) (`createChart`, `addCandlestickSeries`, `ResizeObserver`).
2. Dữ liệu khoảng 45–50 phiên. Chuỗi chỉ báo cần warm-up (EMA50 cần 50 điểm, MACD cần 26+9) nên đầu chuỗi là `null`. Chỉ báo nào không có điểm nào thì tầng/đường đó hiện "Không đủ dữ liệu".
3. Backend hiện tính trong [TechnicalIndicatorService.cs](../../../src/InvestmentApp.Infrastructure/Services/TechnicalIndicatorService.cs). Các hàm `CalculateEma`, `CalculateRsi`, `CalculateBollingerBands`, `CalculateAtr`, `CalculateStochastic`, `CalculateAdx`, `CalculateObv`, `CalculateMfi` chỉ trả giá trị cuối. Đã có sẵn `CalculateRsiSeries` và `CalculateMacdHistogramSeries`.
4. Frontend không có HTTP interceptor auth. Service phải tự gắn `getHeaders()` (memory `learning_pitfall_no_global_auth_interceptor`).
5. TDD bắt buộc. UI text tiếng Việt có dấu.

## Thiết kế

### Backend: endpoint chuỗi chỉ báo
- **Endpoint mới** `GET /api/v1/market/stock/{symbol}/analysis/series`, cùng controller với `analysis`, cùng nguồn giá (`GetHistoricalPricesAsync`, cùng khoảng `months`). Trả về:
  ```
  { symbol, dates[], open[], high[], low[], close[], volume[],
    ema20[], ema50[], bollingerUpper[], bollingerMiddle[], bollingerLower[],
    rsi14[], stochK[], stochD[], macdLine[], macdSignal[], macdHistogram[],
    adx[], plusDi[], minusDi[], atr14[], obv[], mfi14[], volumeSma20[] }
  ```
  Các mảng dài bằng `dates`; điểm chưa đủ warm-up là `null`.
- **Một nguồn công thức.** Tách mỗi hàm `CalculateX` thành `CalculateXSeries` trả cả chuỗi. Hàm scalar cũ lấy phần tử cuối của chuỗi. Như vậy thẻ và đường cùng một code path.
- **Test parity (bắt buộc).** Với cùng một bộ giá cố định, phần tử cuối mỗi chuỗi phải bằng giá trị scalar mà `AnalyzeAsync` trả (đúng với cả 16 chỉ báo). Toàn bộ test TA hiện có phải vẫn xanh, không đổi số.
- Mốc giá (hỗ trợ/kháng cự, Fibonacci) và Tổng hợp (entry/SL/TP gợi ý) lấy từ endpoint `analysis` sẵn có, vì chúng là đường ngang, không phải chuỗi.

### Frontend
- **Biểu đồ** đặt ngay dưới header panel, trên 6 khối. Chỉ tải `analysis/series` khi người dùng bấm nhóm lần đầu (lazy), và cache theo mã.
- **Tiêu đề khối** thành nút chọn nhóm (có `role="button"`, `aria-pressed`, Enter/Space). Khối đang chọn được tô nổi. Mặc định chưa có biểu đồ. Chỉ có dòng gợi ý "Bấm tên nhóm để xem biểu đồ", vì chuỗi được tải lazy. Bấm nhóm lần đầu thì tải và hiện biểu đồ. Bấm lại nhóm đang chọn thì biểu đồ còn mỗi nến.
- **Nhóm → cái được vẽ:**

| Nhóm | Đè lên nến | Tầng dưới |
|---|---|---|
| 🎯 Tổng hợp | Đường ngang Giá vào / Cắt lỗ / Chốt lời (từ `suggestedEntry`) | — |
| 📈 Xu hướng | EMA20, EMA50 | ADX + DI+/DI− (vạch 25) |
| ⚡ Động lượng | — | RSI (vạch 30/70), MACD (line/signal/histogram), Stochastic %K/%D (vạch 20/80) |
| 📏 Biến động | Bollinger trên/giữa/dưới | ATR14 |
| 💰 Dòng tiền | — | Khối lượng + SMA20 khối lượng, OBV, MFI (vạch 20/80) |
| 🧱 Mốc giá | Đường ngang hỗ trợ, kháng cự, các mức Fibonacci (có nhãn) | — |

- **Phần lõi tái dùng:** một component nhận "danh sách tầng + series" rồi dựng các chart xếp chồng có đồng bộ. Trang Nhật ký dùng lại về sau.

## Quyết định của root sau builder check (user có thể bác)

1. **Dưới 20 phiên** (ngưỡng mà `AnalyzeAsync` trả kết quả rỗng, TechnicalIndicatorService.cs:21-31): endpoint series vẫn trả 200 kèm nến; mọi mảng chỉ báo toàn `null`. Frontend vẽ nến và báo "Không đủ dữ liệu" theo **từng** chỉ báo, không ẩn cả biểu đồ.
2. **Đường ngang:** `analysis` đã có sẵn trong state của component (panel chỉ render khi `analysis` có), nên không gọi lại.
   - Tổng hợp không có `suggestedEntry` thì không vẽ đường; ghi chú nhỏ "Chưa có gợi ý giao dịch".
   - Fibonacci vẽ 5 mức thoái lui (23.6/38.2/50/61.8/78.6) và đỉnh/đáy swing, có nhãn. Không vẽ mức mở rộng.
3. **Cùng thời điểm:** khi người dùng bấm nhóm lần đầu, frontend gọi **song song** `analysis/series` và `analysis` (forkJoin), rồi thay `analysis` trong state bằng bản mới. Thẻ và đường vì vậy luôn cùng một lần lấy giá. Cache theo mã, xoá khi đổi mã. Parity công thức được ghim ở test backend trên cùng bộ giá cố định.
4. **Khoảng dữ liệu:** endpoint series dùng đúng `months` mặc định và cùng cách lấy giá như `AnalyzeAsync`. EMA50 chỉ có 0–1 điểm là **chấp nhận được**; khi đó hiện "Không đủ dữ liệu" cho EMA50.
5. **Phạm vi refactor:** chỉ những giá trị được vẽ mới cần hàm series (ema20, ema50, bollinger ×3, rsi14, stochK/D, macd line/signal/histogram, adx, +DI, −DI, atr14, obv, mfi14, volumeSma20), tổng cộng 18 chuỗi. Logic giao cắt (`CalculateMacdAt` cho phiên trước), chuỗi xu hướng OBV và các tín hiệu chữ giữ nguyên. `volumeSma20` phải bằng `AvgVolume20` (có test parity).

## Để builder tự quyết
- Màu đường, độ cao mỗi tầng, chữ legend, vị trí nút đóng.
- Cách đồng bộ crosshair giữa các instance chart.
- Cách tách component và đặt tên file.
- Định dạng ngày trong DTO (theo convention của endpoint lịch sử giá hiện có).
