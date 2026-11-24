# Quyết định trước giao dịch theo kỳ vọng (EV): Design

**Ngày:** 2026-11-24
**Trạng thái:** Draft. Hướng (C) đã chốt.

## Ý định (lời user)
"Không có quyết định nào chắc thắng 100%, chỉ có quyết định tỉ lệ thắng cao hơn thua, tức là lãi nhiều hơn khả năng lỗ, và tiền lãi nhiều hơn tiền lỗ." Trước mỗi giao dịch, app phải giúp ước lượng và gợi ý theo **kỳ vọng**: EV = Σ (xác suất kịch bản × kết quả kịch bản). Các tính năng hiện có được chỉnh theo hướng đó.

## Hiện trạng (khảo sát 2026-11-24)
- `TradePlan.RiskRewardRatio` do người dùng gõ tay; hệ thống không tính (`TradePlanTools.cs:56` ghi rõ). `ConfidenceLevel` 1–10 không bao giờ thành xác suất.
- Cổng trước giao dịch (dossier, discipline gate: luận điểm/invalidation, pre-check biến động/ngân sách) không cổng nào xét kỳ vọng. Chỉ AI kiểm R:R ≥ `RiskProfile.DefaultRiskRewardRatio` (2.0).
- `PerformanceMetricsService` tính Expectancy theo **tiền**, đếm mỗi lệnh bán là một "trade", gộp cả lãi/lỗ **chưa chốt**, không gắn kế hoạch.
- `StrategyPerformanceService` và campaign analytics có win rate nhưng **không có expectancy**, và không hiện lúc lập kế hoạch.
- Kelly có trong `PositionSizingService` nhưng **không UI nào truyền winRate/avgWin/avgLoss**.
- `CampaignReviewData` có PnL, %, số ngày giữ, mức đạt target, nhưng **không lưu R thực tế** (R đạt được / R rủi ro ban đầu).
- Confluence score và R:R gợi ý của TA chưa đối chiếu với kết quả thật.
- Mẫu lịch sử của một người dùng mỏng (vài chục chiến dịch), chia theo chiến lược càng mỏng.

## Approaches
| Cách | Đánh đổi | Kết quả |
|---|---|---|
| A. Người dùng tự ước lượng xác suất từng kịch bản | Có ngay, nhưng dễ lạc quan, không có gì đối chứng | Một phần của C |
| B. Xác suất lấy từ lịch sử (theo chiến lược/setup) | Khách quan, nhưng mẫu mỏng nên số dao động mạnh, dễ tạo cảm giác chính xác giả | Một phần của C |
| **C. Kết hợp** | Người dùng bắt buộc ước lượng. App hiện tỉ lệ nền lịch sử kèm số mẫu và khoảng tin cậy. Sau khi đóng lệnh, app chấm độ khớp ước lượng so với kết quả | **Chọn (root, trong cửa sổ tự quyết)** |
| D. Mô hình dự đoán xác suất từ chỉ báo | Không đủ dữ liệu, dễ overfit, khó giải thích | Loại |

## Goals
1. **R chuẩn hoá:** mọi kết quả quy về bội số của rủi ro ban đầu, 1R = |Entry − SL| × số lượng (cộng phí nếu có sẵn). R:R của kế hoạch do hệ thống tính, không gõ tay.
2. **Bảng kỳ vọng trên kế hoạch:** danh sách kịch bản kết quả {nhãn, xác suất %, giá thoát}, tự dựng sẵn từ các mức chốt lời (`ExitTargets`/`Target`) và cắt lỗ (`StopLoss`). Người dùng nhập xác suất, tổng phải = 100%. App tính EV theo R và theo VND, cùng % thắng ngầm định = tổng xác suất các kịch bản R > 0.
3. **Tỉ lệ nền:** cạnh bảng hiện % thắng, R thắng trung bình, R thua trung bình, kỳ vọng R của **các chiến dịch đã đóng** cùng chiến lược (không có thì toàn bộ), kèm số mẫu n và khoảng tin cậy Wilson 95% cho % thắng. Khi n < 10 thì ghi rõ "chưa đủ mẫu để tin".
4. **Cổng kỳ vọng (cảnh báo, không chặn cứng):** EV ≤ 0, hoặc chưa nhập xác suất, thì chuyển Draft → Ready phải nhập lý do. Lý do được lưu và hiện trong review. Cách này cùng kiểu với discipline gate hiện có.
5. **R thực tế khi review:** `CampaignReviewData` thêm `RealizedR` (PnL ròng / số tiền rủi ro ban đầu) và `EstimatedWinProbability` (ảnh chụp lúc chuyển Ready).
6. **Kỳ vọng theo R:** thống kê chiến lược và hiệu suất có thêm Expectancy(R), PF, avgWinR/avgLossR, **chỉ tính chiến dịch đã đóng**. Chỉ số tiền cũ **giữ nguyên** nhưng được gắn nhãn rõ "gồm cả lãi/lỗ chưa chốt", không lặng lẽ đổi nghĩa.
7. **Hiệu chỉnh (calibration):** trang/khối "Độ khớp ước lượng": chia các lệnh đã đóng theo mức % thắng đã ước lượng (0–40, 40–55, 55–70, 70+), so tỉ lệ thắng thật, Brier score, câu tóm tắt "bạn thường lạc quan +x điểm %".
8. **Kelly dùng dữ liệu thật:** định cỡ vị thế lấy % thắng và avgWin/avgLoss (theo R) từ lịch sử chiến lược. Dùng half-Kelly, trần 25% như hiện có. **Chỉ hiện khi n ≥ 20**, kèm cảnh báo mẫu.

## Non-goals
- Không có mô hình máy học dự đoán xác suất. Confluence score chưa đổi thành xác suất (chỉ xét lại khi calibration có đủ dữ liệu).
- Không chặn cứng lệnh EV ≤ 0. App cá nhân nên tôn trọng quyền quyết định, nhưng phải để lại dấu vết lý do.
- Không sửa nghĩa các con số cũ một cách âm thầm.
- Không đụng nhánh kịch bản động (`ScenarioNodes` trigger) ở đợt này. Bảng kỳ vọng là kịch bản **kết quả**, tách biệt với cây hành động.

## Anti-goals
- **Neo (anchoring):** ô xác suất **không** được điền sẵn từ tỉ lệ nền, để người dùng ước lượng trước rồi mới so.
- **Chính xác giả:** con số lịch sử không bao giờ hiện thiếu n và khoảng tin cậy.
- Thêm ma sát mà không đem lại thông tin. Bảng kỳ vọng phải nhập được trong khoảng 20 giây với 2–3 kịch bản dựng sẵn.

## Ràng buộc
- TDD. Domain tính R/EV là hàm thuần có test. Tiếng Việt có dấu.
- Kế hoạch cũ (không có xác suất) vẫn hợp lệ. Review cũ không có `RealizedR` thì tính lại khi đọc nếu đủ Entry/SL/Quantity, không thì để null và hiển thị "n/a" (memory `learning_pitfall_ratio_null_when_denominator_undefined`).
- MCP: `create_trade_plan`/`update_trade_plan` nhận thêm danh sách kịch bản (tuỳ chọn, đặt sau `ct`). Mô tả tool cập nhật: R:R do hệ thống tính.
- Mọi query thống kê lọc theo user gọi (không mở lại các lỗ ownership vừa vá).

## Chia đợt (mỗi đợt một PR)
1. **Nền R:** R:R tự tính; `RealizedR` khi review (kèm tính lại khi đọc); Expectancy(R) + PF + avgWinR/avgLossR cho chiến lược và hiệu suất (chỉ lệnh đã đóng); nhãn rõ cho chỉ số tiền cũ.
2. **Bảng kỳ vọng + cổng:** kịch bản kết quả trên kế hoạch, EV, tỉ lệ nền (Wilson), cổng cảnh báo kèm lý do, ảnh chụp `EstimatedWinProbability`. MCP nhận kịch bản.
3. **Hiệu chỉnh + Kelly:** khối độ khớp ước lượng, Brier; Kelly lấy dữ liệu lịch sử khi n ≥ 20.

## Quyết định của root sau builder check (2026-11-24 12:40, trong cửa sổ tự quyết)
1. **1R cố định tại thời điểm chốt kế hoạch.** Lần đầu Draft → Ready, lưu ảnh chụp `InitialRisk` {Entry, StopLoss, Quantity (tổng lot dự tính), RiskAmount = |Entry − SL| × Quantity}. SL dời sau đó hoặc lot khớp lệch giá **không** đổi 1R. Kế hoạch cũ không có ảnh chụp thì lấy `StopLossHistory` mục đầu (`OldPrice`) nếu có, không thì SL hiện tại, kèm cờ `RiskBasis = "uoc-tinh"`. Thiếu dữ liệu thì để null và hiện "n/a".
2. **Thống kê R theo chiến lược là query MỚI theo kế hoạch.** Nguồn là các plan đã review, nhóm theo `plan.StrategyId`; plan không gắn chiến lược thì vào nhóm "Không gắn chiến lược" và vẫn tính trong tổng. n = số plan đã review có `RealizedR`. Số liệu cũ theo trade **giữ nguyên**, gắn nhãn "theo lệnh, gồm chưa chốt".
3. **Thắng/thua tính theo R ròng (đã trừ phí, thuế), có vùng hoà.** R > +0.1 là thắng, R < −0.1 là thua, còn lại là hoà. % thắng = thắng / (thắng + thua). Số lệnh hoà hiện riêng.
4. **Kịch bản là kết quả của CẢ vị thế, với giá thoát bình quân.** Dựng sẵn 3 dòng: "Đạt kế hoạch" (giá = bình quân các `ExitTargets` theo `PercentOfPosition`, không có thì `Target`), "Cắt lỗ" (giá = SL), "Thoát sớm / hoà" (giá = Entry). Người dùng sửa giá, thêm dòng được. Như vậy EV và `RealizedR` cùng là R bình quân của cả vị thế, so được với nhau.
5. **Chỉ lệnh mua (Buy).** Kế hoạch Sell ẩn khối kỳ vọng và không vào thống kê R, kèm ghi chú "chưa hỗ trợ lệnh bán".
6. **Ảnh chụp ước lượng khoá khi khớp lệnh đầu tiên.** Sửa xác suất trong Ready vẫn cập nhật ảnh chụp. Từ lúc khớp lot hoặc lệnh đầu tiên (InProgress) thì ảnh chụp khoá; sửa sau đó chỉ lưu bản hiện hành, calibration vẫn dùng ảnh chụp đã khoá.
7. **Calibration:** chỉ hiện Brier khi tổng n ≥ 15. Mức nào có n < 5 thì làm mờ và ghi "chưa đủ mẫu".
8. **Toán:**
   - Kelly dùng **cận dưới Wilson** của % thắng và b = avgWinR / |avgLossR|, chỉ khi n ≥ 20. `null` (không có lợi thế) thì hiện "Không có lợi thế — không nên vào lệnh theo Kelly".
   - PF theo R khi không có lệnh thua thì là null ("n/a"), không dùng 999.99.
   - Lý do vượt cổng EV được truyền từ command vào method domain chuyển trạng thái.


## Sửa sau challenger (2026-11-24 ~12:50) — thay thế các điểm tương ứng phía trên
1. **Thời điểm chụp 1R:** chụp ở lần đầu RỜI Draft, sang Ready **hoặc** sang InProgress qua `ExecuteLot` (đường này không đi qua Ready, `TradePlan.cs` ~421). Còn ở Ready thì chụp lại mỗi lần `Update()`. **Khoá** ở lần khớp đầu tiên (`MarkInProgress`/`ExecuteLot`). `Restore()` về Draft thì xoá ảnh chụp.
   - Entry của lệnh vào nhiều lot = bình quân giá lot theo khối lượng dự tính.
   - `StopLossHistory[0].OldPrice == 0` nghĩa là chưa đặt, coi như thiếu.
   - Khớp một phần thì 1R vẫn theo khối lượng dự tính, kèm cờ `RiskBasis` ghi "khớp một phần". Không tự co 1R.
2. **R:R:** **tính khi đọc** (DTO/hiển thị), **không ghi đè** field lưu. Đầu vào hợp lệ (Buy, entry > SL > 0, có target hoặc exit targets) thì hiện số tính. Không hợp lệ (Sell, thiếu target) thì hiện giá trị người dùng đã gõ. MCP vẫn nhận `riskRewardRatio`, description ghi rõ "hệ thống tự tính khi đủ dữ liệu, giá trị gửi lên chỉ dùng khi không tính được". AI so ngưỡng bằng giá trị hiệu lực (tính được thì dùng số tính). Không cập nhật hàng loạt document cũ.
3. **ExpectancyR = trung bình R ròng của MỌI lệnh đã đóng, kể cả hoà.** % thắng vẫn = thắng / (thắng + thua) để hiển thị, số hoà hiện riêng.
4. Kịch bản (đợt 2): EV trừ phí ước tính để so với `RealizedR` ròng. Người dùng thêm được dòng, ví dụ "chạm TP1 rồi về cắt lỗ".
5. Kelly (đợt 3): trình bày là "cỡ vị thế thận trọng", không phải lời khuyên vào lệnh.
6. **Không ghi dữ liệu khi đọc:** `RealizedR` tính khi đọc chỉ nằm trong DTO, không gán vào entity. `UpdateReviewLessons` lưu cả `ReviewData` nên gán vào entity là tự ghi xuống prod. Mongo đã có `IgnoreExtraElementsConvention(true)` (Program.cs:43), nên rollback code cũ vẫn đọc được document có field mới.

## Để builder tự quyết
- Tên field/DTO cụ thể, chỗ đặt khối UI trong form kế hoạch, màu sắc.
- Cách tính phí vào 1R (nếu phí có sẵn trên plan thì cộng, không thì bỏ qua và ghi chú).
- Lưu kịch bản như value object trong `TradePlan` (Mongo embedded), tương tự `ExitTargets`.
