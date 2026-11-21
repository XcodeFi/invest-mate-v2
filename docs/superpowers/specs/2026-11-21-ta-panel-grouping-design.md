# Sắp xếp panel Phân tích kỹ thuật theo nhóm chỉ báo

**Ngày:** 2026-11-21
**Trạng thái:** Đã triển khai
**Trang liên quan:** `/market-data` — [market-data.component.ts](../../../frontend/src/app/features/market-data/market-data.component.ts) (khối Technical Analysis, dòng 192–548)

## Ý định & lý do

Panel phân tích kỹ thuật hiện là **lưới phẳng 10 thẻ** (EMA, RSI, MACD, Khối lượng, Bollinger, ATR, Stochastic, ADX, OBV, MFI) — người đọc phải tự nhóm trong đầu "cái nào nói về xu hướng, cái nào về dòng tiền". Sắp xếp lại thành **6 khối** (1 khối Tổng hợp + 5 nhóm chỉ báo) theo **câu hỏi mà chỉ báo trả lời** để soi nhanh một mã trước khi quyết định:

| Nhóm | Trả lời câu hỏi |
|---|---|
| 🎯 Tổng hợp | Chốt lại: làm gì bây giờ? |
| 📈 Xu hướng | Trend hay sideway? Hướng nào, mạnh không? |
| ⚡ Động lượng | Lực còn không, sắp đảo chiều chưa? |
| 📏 Biến động | Dao động rộng cỡ nào → stop bao xa? |
| 💰 Dòng tiền | Tiền vào thật hay chỉ kéo giá? |
| 🧱 Mốc giá | Vào/ra ở vùng nào? |

Đồng thời phát hiện backend đã tính sẵn nhưng panel **chưa hiển thị**: `fibonacci`, `ema200`, `ema21`, `highestHigh22/lowestLow22` ([market-data.service.ts:149-163](../../../frontend/src/app/core/services/market-data.service.ts#L149-L163)). Lần này bổ sung **Fibonacci + EMA200** (đã chốt qua popup); EMA21 và đỉnh/đáy 22 phiên để sau.

## Goals

1. **Giải thể lưới 3 cột đầu panel** (Confluence / Trạng thái thị trường / Phân kỳ) — mỗi ô về nhóm của nó như bảng dưới. Xếp lại thành 6 khối có tiêu đề, khối **Tổng hợp đưa lên đầu**, nằm **ngay dưới hàng header panel** (header + badge tín hiệu tổng `overallSignalVi` giữ nguyên trên cùng):
   - **🎯 Tổng hợp:** Confluence score (giữ guard `confluenceScore != null`) + khối Gợi ý giao dịch (Entry/SL/Target, R:R, nút "Tạo Trade Plan từ gợi ý" — giữ guard `suggestedEntry`). Cả hai cùng ẩn thì ẩn luôn tiêu đề nhóm.
   - **📈 Xu hướng:** thẻ EMA (thêm dòng EMA200) · thẻ ADX ±DI · thẻ Trạng thái thị trường (kèm chiến lược gợi ý, giữ guard `marketCondition !== 'unknown'`).
   - **⚡ Động lượng:** RSI · Stochastic · MACD · thẻ Phân kỳ (giữ cả nhánh `@else` "Không phát hiện phân kỳ").
   - **📏 Biến động:** Bollinger Bands · ATR.
   - **💰 Dòng tiền:** Khối lượng (KL/TB20) · OBV · MFI.
   - **🧱 Mốc giá:** Hỗ trợ · Kháng cự · **thẻ Fibonacci mới** (swing high/low + retracement 23.6 / 38.2 / 50 / 61.8 / 78.6, format `vndCurrency` như Hỗ trợ/Kháng cự). Hỗ trợ/Kháng cự bỏ wrapper "ẩn khi cả hai rỗng" — chuyển sang pattern thẻ-luôn-hiện với fallback "Chưa xác định" như các thẻ khác, để nhóm không bao giờ chỉ còn mỗi Fibonacci.
2. Nội dung từng thẻ giữ nguyên (trừ các bổ sung trên); không đổi cách tính, không đổi API. Mọi guard điều kiện hiện có giữ nguyên trừ chỗ nói rõ ở trên.
3. Thẻ EMA: đổi tiêu đề thành "Xu hướng (EMA20/50/200)"; dòng EMA200 có guard riêng `@if (analysis.ema200)`, thiếu thì hiện `EMA200: —` (mã < 200 phiên); format `vndCurrency` như EMA20/50.

## Non-goals / Anti-goals

- **Không** thêm logic gộp verdict theo nhóm, không collapse/tab (hướng đã loại khi chốt approach).
- **Không** đụng backend, không đụng `TechnicalAnalysis` DTO.
- **Không** hiển thị EMA21, đỉnh/đáy 22 phiên, extension Fibonacci (1272/1618) — để PR sau nếu cần.
- **Không** đụng symbol-timeline, trade-plan, dashboard (các nơi khác cũng đọc TA nhưng ngoài phạm vi).
- Thất bại nếu: panel dài ra đáng kể mà không dễ đọc hơn, hoặc thẻ nào đó mất dữ liệu đang hiển thị.

## Constraints

1. Chỉ sửa **inline template** + (nếu cần) helper hiển thị trong `market-data.component.ts`; giữ pattern Tailwind + `@if`/`*ngIf` hiện có.
2. `ema200`, `fibonacci` là optional — thiếu dữ liệu thì thẻ/dòng hiện "Không đủ dữ liệu" như các thẻ khác (mã mới niêm yết < 200 phiên sẽ không có EMA200).
3. UI text tiếng Việt có dấu đầy đủ (CLAUDE.md).
4. Không dùng backtick trong HTML comment của inline template (pitfall đã ghi nhận).
5. Nhánh làm việc: tạo nhánh mới từ `master` (branch hiện tại `fix/hmoney-summary-null-timeframes` đang dở việc khác, không gộp chung).

## Approaches đã cân nhắc

| Hướng | Trade-off | Kết quả |
|---|---|---|
| **Tiêu đề nhóm, giữ nguyên thẻ** | Chỉ đổi template, PR nhỏ nhất, không logic mới | ✅ **Được chọn** (popup 2026-11-21) |
| Thẻ nhóm thu gọn + verdict từng nhóm | Gọn trên mobile nhưng cần logic gộp verdict frontend, PR lớn hơn | ❌ Loại |
| Tab theo nhóm | Giấu mất cái nhìn tổng quan một lượt — trái mục đích soi nhanh | ❌ Loại |

Quyết định kèm theo (popup cùng ngày): bổ sung **Fibonacci + EMA200** ngay trong lần này; các trường ẩn còn lại để sau.

**Sửa quyết định (2026-11-21, sau Task 2):** **bỏ EMA200**. Từ v2.86.1, nguồn 24hmoney chỉ trả 67 ngày lịch sử (CHANGELOG v2.86.1: "EMA200 không còn khả dụng"), nên dòng EMA200 sẽ luôn hiện "—", tức là UI chết. User chọn bỏ hẳn qua popup. Các mục EMA200 trong Goals 3, Constraints 2 và Test bên dưới không còn hiệu lực. Thẻ EMA giữ tiêu đề "Xu hướng (EMA20/50)".

## Test

- Tạo `market-data.component.spec.ts` mới (hiện chưa có): stub `TechnicalAnalysis` đủ trường → assert 6 tiêu đề khối render đúng thứ tự, thẻ Fibonacci hiện 5 mốc retracement, dòng EMA200 hiện giá trị; stub thiếu `ema200` → hiện `EMA200: —`, thiếu `fibonacci` → thẻ hiện "Không đủ dữ liệu", không nổ; stub `supportLevels`/`resistanceLevels` rỗng → thẻ vẫn hiện với "Chưa xác định". Lưu ý harness không có zone.js/testing — tránh `fakeAsync`, tránh dựa vào `detectChanges()` theo quirk đã ghi.

## Tài liệu phải cập nhật khi ship

- [frontend/src/assets/CHANGELOG.md](../../../frontend/src/assets/CHANGELOG.md)
- User guide [frontend/src/assets/docs/phan-tich-thi-truong.md](../../../frontend/src/assets/docs/phan-tich-thi-truong.md) — mô tả 6 nhóm + 2 chỉ báo mới hiển thị.

## Để lại cho người thực hiện

- Class Tailwind cụ thể của tiêu đề nhóm (theo style thẻ hiện có), responsive khi xuống 1 cột.
- Vị trí dòng EMA200 trong thẻ EMA (đề xuất: dưới EMA50).
- Bố cục thẻ Fibonacci (đề xuất: 5 dòng mốc, swing high/low chữ nhỏ bên dưới; dùng `@if (analysis.fibonacci; as fib)` để khỏi lặp `!`).
