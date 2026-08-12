# Trần khối lượng theo ngân sách biến động — kế hoạch triển khai

Spec: [`docs/superpowers/specs/2026-08-11-efficient-frontier-plan-sizing-design.md`](../superpowers/specs/2026-08-11-efficient-frontier-plan-sizing-design.md)
Nhánh: `feat/volatility-budget-plan-sizing` · ADR: 0014 (bắt buộc — xem §ADR)

## Mục tiêu

Trong form lập kế hoạch, cạnh khối kiểm-trước đã có (cổng hồ sơ + tỷ trọng ngành), thêm một dòng thứ ba: **khối lượng tối đa của mã này mà biến động danh mục vẫn nằm trong ngân sách**, kèm nút áp trần.

Chỉ dùng hiệp phương sai. Không lợi nhuận kỳ vọng, không nghịch đảo Σ.

## Tái sử dụng, không nhân bản

| Cần | Lấy từ | Vì sao không tự tính |
|---|---|---|
| Giá trị từng vị thế + tổng danh mục | `IRiskCalculationService.GetPortfolioRiskSummaryAsync` | Toán vị thế đã bị nhân bản ~15 service trước ADR-0010; đường này đã đi qua `PositionBuilder` |
| Lịch sử giá | `IStockPriceRepository.GetBySymbolAsync` → thiếu thì `IMarketDataProvider.GetHistoricalPricesAsync` | Đã có sẵn cả hai |
| Hạn mức | `RiskProfile.MaxDrawdownAlertPercent` | Đ3 — không thêm trường mới |
| Khuôn endpoint + handler + kiểm quyền | `GetSectorExposureForPlan` | Anh em sinh đôi, ADR-0012 |
| Khuôn gọi FE | `forkJoin` + `debounceTime(500)` tại [trade-plan.component.ts:2623](../../frontend/src/app/features/trade-plan/trade-plan.component.ts#L2623) | Thêm khóa thứ ba vào đúng forkJoin đang có, không mở luồng mới |

## Phạm vi theo tầng

### Application

| Tệp | Việc |
|---|---|
| `Common/VolatilityBudgetCalculator.cs` | **Mới.** Tĩnh, thuần, không I/O. Toàn bộ toán |
| `Common/Interfaces/IVolatilityBudgetService.cs` | **Mới.** Interface + `VolatilitySizingResult` + enum `VolatilityDataQuality` |
| `Risk/Queries/GetVolatilitySizingForPlan/GetVolatilitySizingForPlanQuery.cs` | **Mới.** Copy khuôn `GetSectorExposureForPlan`: kiểm `portfolio.UserId == request.UserId` rồi gọi service |

**Không** thêm method vào `IRiskCalculationService` — cài đặt đã 929 dòng, 9 trách nhiệm.

### Infrastructure

| Tệp | Việc |
|---|---|
| `Services/VolatilityBudgetService.cs` | **Mới.** Lấy dữ liệu, bổ khuyết lịch sử, đệm 15 phút, gọi calculator |
| `DependencyInjection.cs` | Đăng ký `IVolatilityBudgetService` |

### Api

| Tệp | Việc |
|---|---|
| `Controllers/RiskController.cs` | `GET portfolio/{portfolioId}/volatility-sizing?symbol=&entryPrice=&quantity=`. Cả ba `decimal?`/`int?` bắt buộc — thiếu thì 400, không bind thành 0 (bài học ngay dòng 177 của chính file này) |

### Frontend

| Tệp | Việc |
|---|---|
| `core/services/risk.service.ts` | `VolatilitySizingResult` interface + `getVolatilitySizingForPlan()` |
| `features/trade-plan/trade-plan.component.ts` | Khóa thứ ba trong `forkJoin` hiện có; panel 4 trạng thái; nút áp trần gán `plan.quantity` + `manualQuantity = true` |

## Test

| Nơi | Số ca | Nội dung |
|---|---|---|
| `Application.Tests/Common/VolatilityBudgetCalculatorTests.cs` | ~22 | σ, Σ, σ danh mục, giải trần + kiểm tra ngược, 5 ca biên §4.5, quy đổi ghim nguyên văn 21,1, lọc bất thường, mọi ca chia 0 |
| `Application.Tests/Risk/GetVolatilitySizingForPlanQueryHandlerTests.cs` | 3 | Portfolio người khác → ném; không tồn tại → ném; hợp lệ → gọi service đúng tham số |
| `Infrastructure.Tests/Services/VolatilityBudgetServiceTests.cs` | ~7 | Đủ dữ liệu cục bộ không gọi provider; thiếu thì gọi + ghi lại; provider rỗng → `Insufficient` không ném; 1/5 mã thiếu → `Partial`; đệm gọi provider đúng 1 lần; chuỗi VHM thật → σ hợp lý + nằm trong `AdjustedSymbols` |
| `Api.Tests` | 3 | Thiếu `symbol`/`entryPrice`/`quantity` → 400 với `code` riêng |
| `trade-plan.component.spec.ts` | ~6 | 4 trạng thái panel; nút áp trần gán đúng; debounce không bắn mỗi phím |

Ca chống hồi quy trọng tâm: **nạp đúng chuỗi giá VHM có phiên −49,6%, khẳng định σ ra ~49% chứ không phải ~109%.**

## Rủi ro

| Rủi ro | Xử lý |
|---|---|
| Ánh xạ `type` của provider trả thanh 3 ngày thay vì thanh ngày | Gọi thẳng `type=3` qua đường riêng; test ghim điều đó |
| Sự kiện quyền thổi σ | Lọc \|lợi suất\| > 15%, đếm và khai báo trong `AdjustedSymbols` |
| Endpoint bị gọi mỗi nhịp 500ms | Đệm Σ theo danh mục 15 phút; mã ứng viên tính riêng ngoài đệm |
| Mã mới chưa có lịch sử → lần gọi đầu chậm | Chấp nhận; ghi vào `stock_prices` nên chỉ chậm một lần |
| Ngân sách suy ra quá chặt → trần luôn 0 | Chân trời 1 tháng (§4.4 spec); σ ngân sách **luôn hiện trên UI** kèm cách suy ra |

## ADR

**Bắt buộc.** Trigger khớp: ≥ 2 tầng với contract mới, endpoint công khai mới, và đánh đổi thật giữa ≥ 2 phương án.

ADR-0014 ghi ba quyết định:
1. Không dùng lợi nhuận kỳ vọng, không nghịch đảo Σ (loại bỏ đường cong frontier khỏi V1)
2. Cảnh báo + nút áp trần, không phải cổng cứng
3. Diễn giải lại `MaxDrawdownAlertPercent` theo chân trời 1 tháng thay vì thêm trường ngân sách mới

Quyết định 2 kế thừa nguyên lý ADR-0012: **nguồn dữ liệu quyết định quyền chặn.** Ở đây nguồn là ước lượng thống kê từ 65 quan sát — yếu hơn cả nhãn ngành của provider, nên càng không được chặn.

Quyết định 3 là quyết định nặng nhất: nó gán nghĩa mới cho một trường đang tồn tại. Phải ghi rõ để người sau không đọc `MaxDrawdownAlertPercent` theo nghĩa cũ.

## Không làm

Theo §5 spec: đường cong frontier, GMV, tái cân bằng tự động, co giãn Ledoit–Wolf, trường ngân sách riêng, sửa ánh xạ `type` cũ, dựng lại giá điều chỉnh.

Thêm: **chỉ lệnh MUA**, giống ADR-0012 §Phạm vi. Đường bán không gọi endpoint.
