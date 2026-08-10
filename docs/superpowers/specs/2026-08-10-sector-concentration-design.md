# Tỷ trọng ngành — làm sống luật 40% và hiện số lúc lập kế hoạch

**Ngày:** 2026-08-10 · **Trạng thái:** đã duyệt thiết kế, chưa thi hành

## 1. Vấn đề

Hạn mức tập trung ngành `RiskProfile.MaxSectorExposurePercent` (mặc định **40%**) đã tồn tại trong entity, đã được `RiskCalculationService` tính, đã hiện trên risk-dashboard — và **chưa từng bắn lần nào**.

Chuỗi nguyên nhân, đọc theo thứ tự:

| Bước | Sự thật | Bằng chứng |
|---|---|---|
| 1 | `RiskCalculationService` lấy ngành qua `IFundamentalDataProvider.GetFundamentalsAsync(symbol).Industry` | [RiskCalculationService.cs:380-381](../../../src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs#L380-L381) |
| 2 | Interface đó được đăng ký là `NoOpFundamentalDataProvider` — **luôn trả `null`**. Dòng đăng ký TCBS thật bị comment | [Program.cs:197-201](../../../src/InvestmentApp.Api/Program.cs#L197-L201) |
| 3 | Nên `sectorGroups` (lọc `!IsNullOrEmpty(Sector)`) luôn rỗng; mọi vị thế rơi vào rổ "Không xác định" | [RiskCalculationService.cs:413-416](../../../src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs#L413-L416) |
| 4 | Rổ "Không xác định" bị **hardcode `IsOverweight = false`** | [RiskCalculationService.cs:445](../../../src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs#L445) |
| 5 | ⇒ không có `SectorExposure` nào từng có `IsOverweight = true` | hệ quả của 3 + 4 |

Đây là hình dạng lỗi tệ hơn "chưa làm tính năng": màn hình có mục tỷ trọng ngành, có con số, có hạn mức ghi bên cạnh — nên người đọc tin là đã được canh, trong khi cảnh báo không có đường nào bắn được.

Song song đó, cổng hồ sơ công ty (PR #147) chặn theo **size từng lệnh**, không biết gì về tập trung ngành. Viết năm hồ sơ cho năm công ty thép, ký cả năm, thì cả năm lệnh đều qua cổng.

## 2. Nguồn ngành đang sống

Không cần provider mới. `IComprehensiveStockDataProvider` **đã được đăng ký** ([Program.cs:210](../../../src/InvestmentApp.Api/Program.cs#L210)) và `HmoneyComprehensiveDataProvider` điền ngành từ 24hmoney:

```
IComprehensiveStockDataProvider.GetComprehensiveDataAsync(symbol, ct)
  → ComprehensiveStockData?.Company.Industry      // = indicators.GroupName
```

Cache 5 phút ở tầng provider (xem `docs/business-domain.md`, bảng nguồn dữ liệu ngoài).

## 3. Quyết định

| # | Quyết định | Vì sao |
|---|---|---|
| Q1 | **Chỉ hiện số, không chặn.** Tỷ trọng ngành không trở thành điều kiện chặn lập kế hoạch. | Ngành đến từ provider ngoài. Cổng hồ sơ chặn được vì dữ liệu nó đọc là do người dùng tự viết và luôn có; nhãn ngành thì provider trả sai hoặc chết là chặn oan. Một cổng chặn oan sẽ bị vô hiệu hoá, rồi kéo theo mất niềm tin vào cổng hồ sơ đang hoạt động tốt. |
| Q2 | **Rổ "Không xác định" cũng được so hạn mức**, bỏ `IsOverweight = false` hardcode. | Không biết mình đang dồn vào đâu là một thông tin, không phải sự vắng mặt của thông tin. 60% danh mục ở "Không xác định" đáng hiện đỏ hơn là hiện xanh. |
| Q3 | **Mẫu số của phép chiếu KHÔNG cộng `planSize`.** | `totalValue = max(giá trị vị thế + tiền mặt, giá trị vị thế)` — **đã gồm tiền mặt** ([dòng 355](../../../src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs#L355)). Mua bằng tiền trong danh mục là chuyển tiền mặt thành giá trị vị thế, tổng không đổi. Cộng `planSize` vào mẫu số là làm mọi con số nhỏ đi một cách khó thấy. |
| Q4 | **`totalValue ≤ 0`, chưa chọn portfolio, hoặc provider trả null ⇒ trả `null`**, UI hiện "n/a". Không bao giờ hiện `0%`. | `0%` nói "ngành này anh chưa giữ gì", `null` nói "chưa tính được". Trả 0 là gộp hai câu khác nhau thành một. |
| Q5 | **Endpoint nhẹ riêng, không gọi `GetPortfolioOptimizationQuery`.** | Query đó lặp từng mã: một lần gọi provider + một lần tính P&L mỗi mã, cho toàn danh mục. Gắn vào form gõ-là-gọi (debounce 500ms) là mỗi lần sửa số lượng lại quét cả danh mục. |
| Q6 | **Chưa thêm field ngành gõ tay vào `CompanyDossier`.** | Hồ sơ là chỗ đúng để dự phòng khi provider trả null — nhưng chưa biết provider phủ thiếu tới đâu. Làm phần 1 trước, xem số thật vài tuần, rồi mới quyết. Thêm field bây giờ là đoán tỷ lệ thiếu. |

## 4. Phạm vi

### Phần 1 — làm sống luật 40% (độc lập, đáng làm dù không có phần 2)

- `RiskCalculationService` lấy ngành qua `IComprehensiveStockDataProvider` (`.Company.Industry`) thay vì `IFundamentalDataProvider`.
- Bỏ `IsOverweight = false` hardcode ở rổ "Không xác định" (Q2).
- Không đổi hợp đồng API, không đổi UI. Risk-dashboard tự có số thật.

### Phần 2 — dòng ngành trong khối kiểm-trước trên form lập kế hoạch

Endpoint mới:

```
GET /api/v1/risk/portfolio/{portfolioId}/sector-exposure?symbol=HPG&addValue=8000000
→ 200 { sector, currentPercent, projectedPercent, limitPercent, sameSectorSymbols[] }
```

Đặt trong `RiskController` ([route gốc `api/v1/risk`](../../../src/InvestmentApp.Api/Controllers/RiskController.cs#L21)), theo đúng nếp `portfolio/{portfolioId}/<tên>` của 8 endpoint đang có ở đó — không mở controller mới.

- `sector`, `currentPercent`, `projectedPercent` đều **nullable** (Q4).
- `projectedPercent = (sectorValue + addValue) / totalValue × 100` (Q3).
- `sameSectorSymbols` là các mã **đang giữ** cùng ngành, không gồm mã đang lập kế hoạch.

FE: thêm một dòng vào **đúng khối cảnh báo kiểm-trước đã có** trên `trade-plan.component.ts` (khối đang gọi `gate-status` debounce 500ms). Gọi thêm endpoint này trong cùng handler debounce, cùng điều kiện "đã đủ số để tính". **Không disable nút nào** — cùng nguyên tắc với cảnh báo kiểm-trước hiện tại.

### Ngoài phạm vi

- Không đưa tỷ trọng ngành vào `CompanyDossierGate` (Q1).
- Không thêm field vào `CompanyDossier` (Q6).
- Không sửa `MaxSectorExposurePercent` mặc định 40%.
- Không bật lại `TcbsFundamentalDataProvider` — nó bị comment vì provider không khả dụng; phần 1 đi đường khác nên không cần chạm vào.

## 5. Kiểm chứng

- **Phần 1 phải có test chứng minh luật từng chết.** Test dựng `IComprehensiveStockDataProvider` trả `Industry = "Tài nguyên cơ bản"` cho 3 mã chiếm 60% danh mục → assert có đúng một `SectorExposure` với `IsOverweight = true`. Test này chạy trên code cũ phải **đỏ**, đó là bằng chứng.
- Test riêng cho rổ "Không xác định" vượt hạn mức → `IsOverweight = true` (Q2). Mutation: trả lại `false` hardcode, test phải đỏ.
- Test `totalValue = 0` → `currentPercent` và `projectedPercent` là `null`, **không** phải `0` (Q4). Assert `null` tuyệt đối, vì `0` và `null` cùng render ra "0" nếu assert lỏng.
- Test công thức chiếu: `sectorValue = 32tr`, `totalValue = 100tr`, `addValue = 9tr` → `projectedPercent = 41`, **không** phải `37.6` (= mẫu số cộng thêm). Đây là ca phân biệt Q3 đúng/sai.
- Verify browser: form lập kế hoạch với mã có ngành → thấy dòng ngành; với mã provider không trả ngành → thấy "n/a", không thấy "0%".

## 6. Câu hỏi đã đóng

- *Chặn hay chỉ hiện?* → chỉ hiện (Q1).
- *Hiện ở đâu?* → khối kiểm-trước trên form (phần 2).
- *Ngành gõ tay trong hồ sơ?* → chưa, để cửa sổ dùng thử trả lời (Q6).
