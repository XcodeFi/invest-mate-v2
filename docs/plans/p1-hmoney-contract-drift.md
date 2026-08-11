# Sửa lệch hợp đồng API 24hmoney (panel số liệu hồ sơ công ty)

**Ngày:** 2026-08-11
**Tầng chạm:** Infrastructure → Application → Frontend
**Nhánh:** `fix/hmoney-contract-drift`

## Thuật ngữ

| Viết tắt | Tên đầy đủ | Nghĩa ở đây |
|---|---|---|
| DTO | Data Transfer Object | Lớp C# hứng JSON từ 24hmoney |
| contract drift | — | Bên cung cấp đổi cấu trúc JSON, code vẫn khai theo cấu trúc cũ |
| section | — | Một khối dữ liệu trong panel số liệu (`peers`, `businessPlan`, …) |
| ICB | Industry Classification Benchmark | Chuẩn phân ngành, 24hmoney trả trong `group_industry` |

## Vì sao

Nhà cung cấp `api-finance-t19.24hmoney.vn` đã đổi cấu trúc response. Code khai theo cấu trúc cũ nên **6 trong 8 section hỏng**, và hỏng **âm thầm**: mỗi hàm fetch đều `catch (Exception) → LogWarning → return null`, nên lệch hợp đồng trông giống hệt "mã này không có dữ liệu".

Ảnh hưởng không chỉ panel hồ sơ công ty — `AiAssistantService` cũng ăn cùng nguồn dữ liệu này, nghĩa là bản tóm tắt AI đang suy luận trên dữ liệu thiếu.

**Vì sao test xanh mà production hỏng:** `HmoneyComprehensiveDataProviderTests.cs` tự bịa fixture theo đúng cấu trúc **cũ** (`plan_revenue`, `buy_foreign_qtty`, `event_type`…). Fixture bịa thì ghim lại chính cái contract mà upstream đã bỏ — test không bao giờ phát hiện được drift. Đây là phần phải sửa tận gốc, không chỉ sửa mapping.

## Hiện trạng đo được (HAH, 2026-08-11)

| Section | Endpoint | Code khai | Thực tế | Hậu quả |
|---|---|---|---|---|
| `indicators` | `/v2/ios/companies/index` | các khoá phẳng | đủ cả (60 khoá) | ✅ chạy |
| `analystReports` | `/v1/ios/announcement/report-analytics` | `title,source,publish_date,summary` | đủ cả | ✅ chạy |
| `company` | `/v1/ios/company/detail` | `company_name, short_name, floor, major_share_holder[], company_leaders[]` | `ownership[]`, `leadership[]`, `intro`, `address`… — **không còn khoá nào trùng** | ⚠️ parse được, **mọi field null** → guard bắt được, thành unavailable |
| `dividendEvents` | `/v1/ios/announcement/dividend-events` | `event_type, description, ex_right_date, pay_date, value` | `type, title, exright_date, payout_date, record_date, published_date` (epoch giây) | ⚠️ parse được, **15 dòng field null** → guard bắt được, thành unavailable |
| `incomeStatements` | `/v1/ios/company/financial-report` | `header: string[]`, `data: row[]` | `headers: {year,quarter,type}[]`, `rows: {key,level,name,values}[]` | ❌ null → unavailable |
| `peers` | `/v1/ios/stock-recommend/get_stock_related_bussiness` | `data.data[]` | `data.all.data[]` (+ `hose`/`hnx`/`upcom`) | ❌ null → unavailable |
| `businessPlan` | `/v1/ios/company/plan` | mảng `{year, plan_revenue, plan_profit, plan_dividend}` | object `{year, quarter, plan:[{label, expect, current, percent}]}` | ❌ JsonException → unavailable |
| `foreignTrading` | `/v1/ios/stock/foreign-trading-series` | mảng theo ngày `{trading_date, buy_foreign_qtty, sell_foreign_qtty}` | object: 6 số tổng hợp + `data_time[]` **intraday 5 phút** (96 điểm) | ❌ JsonException → unavailable |

Hai kiểu hỏng khác nhau về cơ chế. `❌` là ném `JsonException` ngay khi đọc. `⚠️` thì parse trót lọt và trả về **vỏ rỗng** — đủ số hàng, mọi field null.

Vỏ rỗng lẽ ra là kiểu hỏng nguy hiểm hơn (UI hiện bảng có hàng nhưng ô nào cũng trống, người đọc hiểu thành "công ty này không có cổ đông lớn"). Ở đây nó **không** gây ra chuyện đó: `GetCompanyFundamentalsQuery.HasAnyValue` — thêm từ một lần sửa trước — chấm theo nội dung chứ không theo null-ness, nên hai section này rơi vào `unavailableSections` chứ không render hàng trống. Guard làm đúng việc của nó. Dữ liệu vẫn mất, nhưng người dùng được báo là mất chứ không bị lừa.

## Quyết định

**Q1 — Bỏ `List<ForeignTradingDay>`, thay bằng một bản tổng hợp.**
Endpoint này đổi bản chất chứ không chỉ đổi tên field: nó **không còn là chuỗi theo ngày**. Response trả 96 điểm `data_time[]` cách nhau 5 phút — tức là diễn biến **trong phiên hôm nay** — kèm 6 số tổng hợp `today/week/month × buy/sell` (tỷ VND).

Đổ 96 điểm intraday vào một bảng gắn nhãn "20 ngày gần nhất" là hiện **dữ liệu sai dưới một cái nhãn trông đúng** — tệ hơn là không hiện gì. Hồ sơ công ty cũng không cần tick 5 phút. Vậy:

```csharp
ForeignTradingSummary { decimal? TodayBuyValue, TodaySellValue, WeekBuyValue,
                        WeekSellValue, MonthBuyValue, MonthSellValue }   // tỷ VND
```

Bỏ hẳn `ForeignTradingDay` và chuỗi intraday. Frontend đổi từ bảng theo ngày sang 3 dòng mua/bán/ròng theo hôm nay – tuần – tháng.

**Q1b — Epoch trong response là mốc nửa đêm giờ VN.**
`exright_date = 1783962000` = `2026-07-13T17:00Z` = đúng `2026-07-14T00:00+07`. Quy đổi bằng giờ UTC sẽ ra **lùi một ngày**. Mọi chỗ đổi epoch sang chuỗi ngày phải cộng UTC+7 trước khi format.

**Q1c — `financial-report` phải gọi `period=2`.**
Code đang gọi `period=1` kèm chú thích "quarterly", nhưng `period=1` trả **theo năm** (`quarter: 0`); `period=2` mới trả theo quý. Model cũng ghi `Period` ví dụ `"Q1/2025"`. Sửa sang `period=2` cho khớp ý định. Vẫn giữ nhánh `quarter == 0 ⇒ "{year}"` để không vỡ nếu upstream đổi tiếp.

**Q1d — So khớp tên dòng phải bỏ qua hoa thường.**
Dòng lợi nhuận ròng thực tế tên là `"LỢI NHUẬN SAU THUẾ TNDN"` (viết hoa). Code đang `Contains("Lợi nhuận sau thuế")` và `Contains("lợi nhuận sau thuế")` — **không** khớp bản viết hoa. Dùng `StringComparison.OrdinalIgnoreCase`.

**Q2 — `CompanyPlan` theo cấu trúc nhãn, không ép về 3 field cũ.**
Cấu trúc mới trả một mảng chỉ tiêu có nhãn tiếng Việt kèm **tiến độ thực hiện** (`current`, `percent`) — thứ trước đây không có. Ép nó về `RevenuePlan/ProfitPlan/DividendPlan` vừa mất dữ liệu vừa vỡ khi upstream đổi nhãn. Mô hình mới:

```csharp
CompanyPlan       { int? Year; int? Quarter; List<CompanyPlanTarget> Targets }
CompanyPlanTarget { string? Label; decimal? Planned; decimal? Actual; decimal? PercentComplete }
```

`plan_dividend` không còn nguồn ⇒ bỏ hẳn, không giữ field luôn null.

**Q3 — Tên công ty + sàn lấy từ `/v1/ios/stock/detail`.**
`/company/detail` không còn trả `company_name`/`floor`. Endpoint `/stock/detail` đang chạy tốt (app lấy giá qua đó) và có đủ `company_name`, `short_name`, `stock_exchange`. Dùng lại, không thêm nguồn mới.

**Q4 — Fixture test là response thật đã bắt được, không phải fixture bịa.**
Lưu JSON thật vào `tests/InvestmentApp.Infrastructure.Tests/TestData/Hmoney/*.json` và cho test đọc từ đó. Fixture bịa là nguyên nhân gốc khiến lỗi này sống sót; sửa mapping mà giữ fixture bịa thì lần drift sau lặp lại y hệt.

**Q5 — `DividendEvent.Value` để null.**
Cấu trúc mới không còn field số; giá trị nằm trong câu tiêu đề ("tỉ lệ 0.2 (2,000 đồng/CP)"). Bóc số từ câu tiếng Việt là mong manh. Để null và hiện tiêu đề đầy đủ thì trung thực hơn.

**Q6 — Chưa đụng vào `catch → return null`.**
Nuốt lỗi âm thầm là vấn đề thật, nhưng sửa nó là đổi hành vi lỗi trên toàn provider — việc riêng, PR riêng. Ghi vào "Việc còn nợ".

## Các bước

### Bước 1 — Bắt response thật thành fixture
Lưu 8 file vào `tests/InvestmentApp.Infrastructure.Tests/TestData/Hmoney/` (`plan.json`, `foreign.json`, `finreport.json`, `peers.json`, `detail.json`, `dividend.json`, `indicators.json`, `stockdetail.json`), đặt `CopyToOutputDirectory`.

→ verify: `dotnet build` xong thấy file trong `bin/Debug/net9.0/TestData/Hmoney/`.

### Bước 2 — RED
Viết lại `HmoneyComprehensiveDataProviderTests.cs` đọc fixture thật. 6 test phải **đỏ**, mỗi test một section lệch. Riêng `company` và `dividendEvents` phải khẳng định **field có giá trị**, không chỉ khẳng định `Count > 0` — đó chính là kiểu khẳng định để lọt lỗi vỏ rỗng.

→ verify: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter HmoneyComprehensive` → 6 đỏ.

### Bước 3 — GREEN: DTO
Sửa `HmoneyComprehensiveApiModels.cs` theo cấu trúc thật. Xoá DTO không còn nguồn (`HmoneyCompanyPlan` cũ, `HmoneyForeignTradingItem` cũ).

### Bước 4 — GREEN: mapping
Sửa `HmoneyComprehensiveDataProvider.cs`:
- `GetBusinessPlanAsync` — object, map `plan[]` sang `Targets`
- `GetForeignTradingAsync` — 6 số tổng hợp, bỏ chuỗi intraday (Q1)
- `GetIncomeStatementAsync` — `period=2`, `headers`/`rows`, nhãn kỳ `"Q2/2026"`, so khớp tên dòng bỏ qua hoa thường
- `GetPeersAsync` — `data.all.data`
- `GetCompanyDetailAsync` — `ownership`/`leadership`; ghép tên + sàn từ `/stock/detail`
- `GetDividendEventsAsync` — `type`/`title`/`exright_date`/`payout_date`, epoch giây → `dd/MM/yyyy`

→ verify: 6 test xanh.

### Bước 5 — Lan lên Application + Frontend
`ForeignTradingDay` và `CompanyPlan` đổi shape ⇒ sửa `GetCompanyFundamentalsQuery` và `fundamentals-panel.component.ts` (nhãn cột, cách hiện chỉ tiêu kế hoạch). Đây là lúc dễ để sót nhất: đổi model backend mà quên nhãn frontend thì UI hiện tiền VND dưới tiêu đề "khối lượng".

→ verify: `dotnet test` toàn bộ + `npx ng test --watch=false --browsers=ChromeHeadless`.

### Bước 6 — Verify thật
`/qa-verify` trên `/company-dossier/HAH`: 6 section hiện dữ liệu thật; `unavailableSections` rỗng hoặc chỉ còn section thật sự không có dữ liệu.

⚠️ `appsettings.Development` trỏ DB **`InvestmentApp_prod`**. Chỉ đọc — không bấm Lưu, không bấm Ký.

### Bước 7 — Review, docs, PR
Code review bắt buộc + **review lại chính phần vừa sửa** ([`/code-review` Step 4.3](../../.claude/commands/code-review/references/review-workflow.md)). Cập nhật `docs/architecture.md` (bảng endpoint) và `frontend/src/assets/CHANGELOG.md`.

## Rủi ro

| Rủi ro | Xử lý |
|---|---|
| Upstream đổi tiếp sau khi bắt fixture | Fixture thật chỉ chụp một thời điểm. Không tự phát hiện được drift lần sau — Q6 (bỏ nuốt lỗi âm thầm) mới là lời giải, đã ghi nợ |
| Nhãn chỉ tiêu kế hoạch đổi ("Doanh thu" → khác) | Không so khớp nhãn trong code; hiện nguyên nhãn upstream trả |
| Số liệu chỉ thử trên HAH | Bước 6 thử thêm ≥ 2 mã khác sàn (HPG–HOSE, SHS–HNX) trước khi mở PR |

## Việc còn nợ (không làm trong PR này)

- `catch (Exception) → return null` khiến lệch hợp đồng không phân biệt được với "không có dữ liệu". Cần tách thành lỗi phân loại được + cảnh báo khi tỷ lệ section hỏng vượt ngưỡng.
- `AiAssistantService` dùng cùng nguồn — sau khi sửa cần rà lại prompt digest xem có chỗ nào đang mô tả sai field không.
