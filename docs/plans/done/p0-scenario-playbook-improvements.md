# P0 Improvement: Scenario Playbook Nâng cao

**Ngày lập:** 27/03/2026
**Trạng thái:** Proposal
**Feature gốc:** P0 — Scenario Playbook (Trade Plan) — ✅ Done
**Phương pháp:** Phân tích code thực tế → xác định gaps → đề xuất cải tiến

---

## Context

P0 đã implement đầy đủ decision tree (ScenarioNode), 3 preset templates, on-demand evaluation (khi vào Dashboard), trailing stop. Tuy nhiên còn nhiều gap giữa "evaluation" và "gợi ý hành động", cùng UX chưa optimal cho việc quản lý scenario phức tạp.

### Hiện trạng code

| Thành phần | Trạng thái | File chính |
|---|:---:|---|
| ScenarioNode entity + enums | ✅ | `Domain/Entities/TradePlan.cs:435-456` |
| 3 preset templates (hardcoded) | ✅ | `Application/TradePlans/Queries/GetScenarioTemplates/` |
| ScenarioEvaluationService | ✅ | `Infrastructure/Services/ScenarioEvaluationService.cs` |
| Evaluation on Dashboard load | ✅ | Trigger on-demand, không có Worker chạy nền |
| Trailing stop (3 methods) | ✅ | `ScenarioEvaluationService.cs:186-246` |
| UI recursive tree editor | ✅ | `trade-plan.component.ts:620-699` |
| Domain events + AlertHistory | ✅ | `Domain/Events/DomainEvents.cs:189-204` |
| Gợi ý hành động theo vùng giá | ❌ | Không gợi ý khi giá vào vùng trigger |
| User custom templates | ❌ | 3 preset cố định, không lưu được |
| ATR thực tế cho trailing | ❌ | Placeholder: `entryPrice × 0.02` |
| Scenario history/audit | ❌ | Không xem lại lịch sử trigger |
| Visual tree (flowchart) | ❌ | Recursive divs, khó theo dõi tree lớn |

---

## Nguyên tắc thiết kế

> **App này là công cụ lập kế hoạch & quản lý rủi ro.** User thực hiện giao dịch trên app môi giới riêng, sau đó ghi nhận vào hệ thống. Mọi cải tiến phải theo hướng **advisory** — nhắc nhở, gợi ý, hiển thị thông tin — KHÔNG tạo ảo giác rằng app có khả năng đặt lệnh.

## Đề xuất cải tiến (xếp theo ưu tiên)

| # | Đề xuất | Size | Lý do ưu tiên |
|---|---------|:---:|---------------|
| P0.1 | Scenario History & Status Dashboard | S | Quick win — hiển thị node đã trigger khi nào, cải thiện UX ngay |
| P0.2 | User Custom Templates (save/load) | S-M | Reuse scenario trees, giảm thời gian lập kế hoạch |
| P0.3 | Visual Flowchart (nâng cấp tree UI) | M | UX quan trọng — tree editor hiện tại khó đọc với depth > 2 |
| P0.4 | ATR thực tế cho Trailing Stop | S | Fix placeholder — ATR(14) đã có sẵn trong `TechnicalIndicatorService` |
| P0.5 | Gợi ý hành động theo vùng giá | S-M | Gợi ý hành động khi giá vào vùng trigger |
| P0.6 | Scenario Consultant — gợi ý kịch bản có cơ sở | M | Nền tảng quan trọng — kịch bản tốt thì gợi ý hành động mới có giá trị |

---

## P0.1: Scenario History & Status Dashboard (S)

**Vấn đề:** User không biết node nào đã trigger, khi nào, giá bao nhiêu. Chỉ có AlertHistory records mà không hiển thị trên UI kế hoạch.

### Backend

1. **New query** `GetScenarioHistoryQuery(tradePlanId)` — lấy lịch sử trigger của plan
   - File: `src/InvestmentApp.Application/TradePlans/Queries/GetScenarioHistory/GetScenarioHistoryQuery.cs`
   - Logic: lấy `AlertHistory` records liên quan đến plan's scenario nodes (đã tạo trong `ScenarioEvaluationService.EvaluatePlan()`)
   - Returns: `ScenarioHistoryDto[]` (NodeId, Label, TriggeredAt, PriceAtTrigger, ActionType, ActionValue)
   - Dependencies: `IAlertHistoryRepository`, `ITradePlanRepository`

2. **New endpoint** `GET /api/v1/trade-plans/{id}/scenario-history`
   - File: `src/InvestmentApp.Api/Controllers/TradePlansController.cs`

### Frontend

1. **Scenario status panel** — hiển thị bên dưới tree editor khi plan ở trạng thái InProgress
   - File: `frontend/src/app/features/trade-plan/trade-plan.component.ts`
   - Mỗi node hiển thị badge: 🟢 Triggered (+ thời gian + giá) / 🟡 Pending / ⚪ Skipped
   - Timeline dạng vertical list: "15/03 14:30 — Node 'Giá >= 82,000' → Bán 30% — Giá: 82,500"

### TDD
- Test: query trả về đúng history records cho plan
- Test: query trả về empty khi không có triggered nodes
- Test: history sorted by triggered time descending

---

## P0.2: User Custom Templates — Save/Load (S-M)

**Vấn đề:** Chỉ có 3 hardcoded preset (Conservative, Balanced, Aggressive). User tạo scenario tree phức tạp → muốn reuse cho plan khác nhưng không có cách lưu.

### Backend

1. **New entity** `ScenarioTemplate` — user-scoped template
   - File: `src/InvestmentApp.Domain/Entities/ScenarioTemplate.cs`
   - Fields: `Id`, `UserId`, `Name`, `Description`, `Nodes` (List\<ScenarioNode\>), `CreatedAt`, `UpdatedAt`
   - Template nodes dùng placeholder values (giống preset hiện tại)

2. **CQRS Commands:**
   - `SaveScenarioTemplateCommand(userId, name, description, nodes[])` — lưu tree hiện tại thành template
   - `DeleteScenarioTemplateCommand(templateId)` — xóa template
   - File: `src/InvestmentApp.Application/TradePlans/Commands/SaveScenarioTemplate/`

3. **Extend GetScenarioTemplatesQuery** — trả về cả preset + user templates
   - File: `src/InvestmentApp.Application/TradePlans/Queries/GetScenarioTemplates/GetScenarioTemplatesQueryHandler.cs`
   - Logic: merge 3 preset cố định + templates từ DB (`IScenarioTemplateRepository`)
   - User templates hiển thị trước, có nút xóa

4. **New endpoints:**
   - `POST /api/v1/trade-plans/scenario-templates` — lưu template
   - `DELETE /api/v1/trade-plans/scenario-templates/{id}` — xóa template
   - `GET /api/v1/trade-plans/scenario-templates` — đã có, extend để include user templates

### Frontend

1. **Nút "Lưu mẫu"** — bên cạnh tree editor, mở dialog nhập tên + mô tả
   - File: `frontend/src/app/features/trade-plan/trade-plan.component.ts`
   - Chỉ hiển thị khi `exitStrategyMode === 'Advanced'` và `scenarioNodes.length > 0`

2. **Dropdown mẫu** — hiện tại chỉ có preset, thêm separator + user templates
   - Optgroup: "Mẫu hệ thống" (3 preset) | "Mẫu của tôi" (user templates)
   - User template có icon 🗑 để xóa

### TDD
- Test: lưu template từ scenario nodes → round-trip load lại đúng
- Test: xóa template thành công
- Test: query merge đúng preset + user templates
- Test: template nodes lưu placeholder values (không lưu giá cụ thể)

---

## P0.3: Visual Flowchart — Nâng cấp Tree UI (M)

**Vấn đề:** Tree editor hiện dùng recursive `ng-template` với `margin-left` indent. Khi depth > 2, rất khó nhìn flow logic. Không thấy rõ parent → child relationships.

### Approach

Thay recursive divs bằng **visual flowchart** sử dụng CSS flexbox + connector lines. **Không thêm dependency mới** — dùng pure CSS/SVG connectors.

### Frontend

1. **Flowchart layout** — thay thế recursive template hiện tại
   - File: `frontend/src/app/features/trade-plan/trade-plan.component.ts`
   - Layout: horizontal tree (left → right) thay vì vertical nested
   - Connector lines: SVG path từ parent → children
   - Mỗi node là một card nhỏ: `[NẾU condition] → [action]`
   - Node status colors: 🟢 xanh (Triggered), 🟡 vàng (Pending), ⚪ xám (Skipped)

2. **Mini-map** — khi tree > 5 nodes, hiển thị overview nhỏ góc phải
   - Giúp navigate tree lớn

3. **Collapsible branches** — click vào node để collapse/expand children
   - Giảm visual clutter cho tree phức tạp

### Design mockup

```
┌──────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ 🟡 Giá ≥ 80k │───→│ 🟡 Bán 30%       │───→│ 🟡 Giá ≥ 85k    │
│              │    │                  │    │    Bán thêm 30%  │
└──────────────┘    └──────────────────┘    └──────────────────┘
                           │
                           ├───→┌──────────────────┐
                           │    │ 🟡 Sau 30 ngày   │
                           │    │    Trailing 5%    │
                           │    └──────────────────┘
                           │              │
                           │              └───→┌──────────────┐
                           │                   │ 🟡 Chạm trail│
                           │                   │    Bán tất cả│
                           │                   └──────────────┘
┌──────────────┐    ┌──────────────────┐
│ 🟡 Giá ≤ 68k │───→│ 🟡 Bán tất cả    │
│ (Stop Loss)  │    │                  │
└──────────────┘    └──────────────────┘
```

### TDD
- Test: tree render đúng với nested nodes (snapshot test nếu dùng)
- Test: collapse/expand toggle state
- Test: connector lines hiển thị đúng cho single-child và multi-child

---

## P0.4: ATR Thực tế cho Trailing Stop (S)

**Vấn đề:** `ScenarioEvaluationService.UpdateTrailingStops()` line 236 dùng placeholder:
```csharp
TrailingStopMethod.ATR => node.TrailingStopConfig.TrailValue * (plan.EntryPrice * 0.02m)
```
Không dùng ATR thực tế → trailing stop sai.

> **Không còn phụ thuộc P3:** `ATR(14)` đã có sẵn trong `TechnicalIndicatorService` (`Atr14` + `AtrPercent`). Có thể triển khai ngay.

### Backend

1. **Inject `ITechnicalIndicatorService`** vào `ScenarioEvaluationService`
   - File: `src/InvestmentApp.Infrastructure/Services/ScenarioEvaluationService.cs`
   - Thay placeholder bằng: `node.TrailValue × actualAtr14`
   - Lấy ATR từ `ITechnicalIndicatorService.AnalyzeAsync(symbol)` → `Atr14` field (đã có)

2. **Fallback** — nếu không lấy được ATR (thiếu dữ liệu price history):
   - Dùng proxy: `entryPrice × 0.02 × trailValue` (giữ nguyên logic hiện tại)
   - Log warning

### TDD
- Test: trailing stop dùng ATR thực tế khi có
- Test: fallback về proxy khi ATR null
- Test: ATR-based trailing stop update đúng khi price mới > highestPrice

---

## P0.5: Gợi ý hành động theo vùng giá (S-M)

**Mục tiêu:** Khi user vào Dashboard, app quét giá hiện tại so với kế hoạch kịch bản → gợi ý hành động nếu giá đang nằm trong vùng trigger.

> **Nguyên tắc:**
> - User là **nhà đầu tư**, không phải trader. Scenario dựa trên **vùng giá**, không phải điểm giá cố định.
> - App **gợi ý** user xem xét hành động, không thực hiện giao dịch.
> - Không có Worker chạy nền. Evaluation trigger **on-demand khi vào Dashboard**.

### Backend

1. **Extend Dashboard load flow** — quét scenario evaluation on-demand
   - File: `src/InvestmentApp.Infrastructure/Services/ScenarioEvaluationService.cs`
   - Gọi evaluate tất cả active plans → xác định giá hiện tại đang nằm trong vùng nào
   - Trả về danh sách gợi ý: symbol, vùng giá đang active, hành động gợi ý

2. **Advisory response format:**
   - Giá đi lên vùng: "HPG đang ở 82,500 (vùng ≥ 80,000) — xem xét bán 30%"
   - Giá đi xuống vùng: "HPG đang ở 67,200 (vùng ≤ 68,000) — xem xét cắt lỗ"
   - Giá ngoài mọi vùng: không hiển thị gợi ý

3. **New endpoint hoặc extend Dashboard endpoint** — trả về advisory list
   - File: `src/InvestmentApp.Api/Controllers/TradePlansController.cs` hoặc `DashboardController`

### Frontend

1. **Dashboard load** — `ngOnInit()` gọi evaluate → hiển thị gợi ý
   - File: `frontend/src/app/features/dashboard/dashboard.component.ts`
2. **Widget "Gợi ý hành động"** trên Dashboard
   - Chỉ hiển thị khi có plan đang active và giá nằm trong vùng trigger
   - Mỗi gợi ý: symbol, giá hiện tại, vùng trigger, hành động gợi ý
   - Wording: "Xem xét...", "Cân nhắc...", KHÔNG dùng "Đã...", "Cần phải..."

### TDD
- Test: giá nằm trong vùng trên → trả gợi ý đúng action
- Test: giá nằm trong vùng dưới → trả gợi ý đúng action
- Test: giá ngoài mọi vùng → không trả gợi ý
- Test: advisory message format đúng tiếng Việt
- Test: không trả duplicate gợi ý cho cùng node đã xử lý

---

## P0.6: Scenario Consultant — Gợi ý kịch bản có cơ sở (M)

**Vấn đề:** Hiện tại user tự tạo kịch bản từ đầu hoặc chọn 3 preset chung chung (Conservative, Balanced, Aggressive). Preset không dựa trên phân tích cụ thể của mã chứng khoán → kịch bản có thể thiếu cơ sở, dẫn đến gợi ý hành động (P0.5) không có giá trị.

**Ý tưởng:** Khi user tạo kế hoạch cho một symbol, app phân tích dữ liệu hiện có → gợi ý kịch bản với các vùng giá có cơ sở kỹ thuật/cơ bản, theo tầm nhìn user chọn.

> **Nguyên tắc:** Đây là **gợi ý tham khảo**, không phải khuyến nghị đầu tư. User luôn là người quyết định cuối cùng và chỉnh sửa lại kịch bản theo đánh giá cá nhân.

> **Về 3 preset cũ (Conservative, Balanced, Aggressive):** Khi Scenario Consultant sẵn sàng, 3 preset trở thành redundant vì Consultant gợi ý dựa trên phân tích thực tế. Lộ trình: Phase 1–3 giữ nguyên preset → Phase 5 khi Consultant hoạt động → preset chuyển thành fallback (dùng khi không đủ dữ liệu kỹ thuật) hoặc loại bỏ nếu không cần.

### Triết lý phân tích

> Biểu đồ và phân tích kỹ thuật phản ánh **tâm lý đám đông** — tâm lý con người, tâm lý nhà đầu tư trên thị trường. Nhận biết đầy đủ các điểm kỹ thuật then chốt tạo ra **điểm tựa tự tin** để ra quyết định. Đặc biệt với tầm nhìn ngắn hạn, phân tích kỹ thuật là nền tảng bắt buộc.

### Tầm nhìn đầu tư (user chọn khi tạo plan)

| Tầm nhìn | Thời gian | Auto-fill mốc thời gian | Cơ sở phân tích chính |
|---|---|---|---|
| **Ngắn hạn** | 1–4 tuần | Đánh giá lại: +2 tuần, Hết hạn: +4 tuần | **Phân tích kỹ thuật là trọng tâm:** hỗ trợ/kháng cự, Fibonacci, RSI, MACD, Bollinger Bands, volume spike — nhận diện tâm lý đám đông tại các vùng giá then chốt |
| **Trung hạn** | 1–6 tháng | Đánh giá lại: +3 tháng, Hết hạn: +6 tháng | Kỹ thuật + xu hướng: EMA50/EMA200 (cần thêm EMA200), vùng tích lũy/phân phối, breakout/breakdown levels, trendline |
| **Dài hạn** | 6 tháng–vài năm | Đánh giá lại: +6 tháng, Hết hạn: +1 năm | Cơ bản + kỹ thuật vĩ mô: giá trị nội tại (P/E, book value), vùng giá lịch sử, triển vọng ngành, support/resistance dài hạn |

Khi user chọn tầm nhìn → tự động fill:
- **Ngày đánh giá lại** (ReviewDate) — mốc xem lại kế hoạch, đánh giá kịch bản còn phù hợp không
- **Ngày hết hạn** (ExpiryDate) — kế hoạch hết hiệu lực, cần tạo kế hoạch mới
- **Các node TimeElapsed** trong kịch bản gợi ý — VD: ngắn hạn "NẾU sau 2 tuần giá sideway → đánh giá lại"
- User chỉnh sửa được tất cả mốc thời gian sau khi fill

### Đánh giá đầu vào — đã có vs thiếu

Dựa trên phân tích codebase hiện tại:

| Đầu vào | Trạng thái | Chi tiết |
|----------|:---:|---------|
| Price history OHLCV (6 tháng, daily) | ✅ | `IMarketDataProvider` → 24hmoney API, lưu MongoDB `stock_prices` |
| EMA(20, 50) + trend detection | ✅ | `TechnicalIndicatorService` → bullish/bearish. **EMA200 chưa có — cần thêm** |
| RSI(14) + tín hiệu quá mua/quá bán | ✅ | `TechnicalIndicatorService` → oversold/overbought/neutral |
| MACD(12,26,9) + crossover | ✅ | `TechnicalIndicatorService` → buy/sell/neutral |
| Bollinger Bands(20,2) + squeeze/breakout | ✅ | `TechnicalIndicatorService` → bandwidth, %B |
| ATR(14) | ✅ | `TechnicalIndicatorService` → volatility |
| Volume Analysis (avg20, ratio, spike) | ✅ | `TechnicalIndicatorService` → spike/high/normal/low |
| Support/Resistance (swing high/low clustering) | ✅ | `TechnicalIndicatorService` → top 3 S/R levels |
| Trade Suggestion (Entry/SL/Target/R:R) | ✅ | `TechnicalIndicatorService` → suggestedEntry, suggestedStopLoss, suggestedTarget |
| API endpoint phân tích kỹ thuật | ✅ | `GET /api/v1/market/stock/{symbol}/analysis` → `TechnicalAnalysisResult` |
| **Fibonacci retracement/extension** | ❌ | **Chưa có — cần thêm vào `TechnicalIndicatorService`** |
| **Volume profile (phân bổ volume theo vùng giá)** | ❌ | Chỉ có avg volume, chưa có volume bucketed theo price level |
| **Price history > 6 tháng** | ❌ | Hard-coded 6 tháng, cần mở rộng cho tầm nhìn dài hạn |
| **Candlestick chart** | ❌ | `lightweight-charts` đã cài nhưng chưa dùng, hiện chỉ có line/bar (chart.js) |
| **Indicator overlays trên chart** | ❌ | Indicators tính ở backend nhưng không vẽ lên chart |
| **Fibonacci/S-R lines trên chart** | ❌ | Tính được nhưng không hiển thị trực quan |
| **EMA200** | ❌ | Chỉ có EMA20/EMA50, cần thêm EMA200 cho trung/dài hạn |
| **Mô hình nến (pattern detection)** | ❌ | Không có nhận diện tự động — scope lớn, xem xét bỏ hoặc đưa vào phase sau |
| **Volume profile (volume theo vùng giá)** | ❌ | Chỉ có avg volume — volume profile cần tính toán thêm, ưu tiên thấp |

### Cần bổ sung trước khi triển khai P0.6

**Bước 0a — Fibonacci + EMA200** (S):
- Thêm Fibonacci vào `TechnicalIndicatorService.cs` — retracement (23.6%, 38.2%, 50%, 61.8%, 78.6%) và extension (127.2%, 161.8%) từ swing high/low
- Thêm EMA200 — gọi `CalculateEma(prices, 200)`, thêm field `Ema200` vào `TechnicalAnalysisResult`
- Thêm fields `FibonacciLevels` vào `TechnicalAnalysisResult`
- Extend API response `GET /api/v1/market/stock/{symbol}/analysis`

**Bước 0b — Nâng cấp chart: candlestick + overlays** (M):
- `symbol-timeline.component.ts` **đã dùng lightweight-charts** (line series) — chỉ cần nâng cấp, không phải migration
- Thay `addLineSeries()` → `addCandlestickSeries()` với dữ liệu OHLC
- Thêm overlays: EMA lines, Bollinger Bands, S/R levels, Fibonacci levels
- Highlight vùng confluence (nơi nhiều indicator hội tụ)
- User bật/tắt từng loại overlay

**Bước 0c — Mở rộng history depth** (S):
- Cho phép truyền tham số thời gian vào `GetHistoricalPricesAsync()` thay vì hard-code 6 tháng
- Tầm nhìn dài hạn cần ít nhất 1–2 năm dữ liệu
- EMA200 cần tối thiểu 200 phiên → ~10 tháng dữ liệu

**Không làm trong phase này (scope control):**
- Mô hình nến tự động (pattern detection) — scope lớn, phức tạp, đưa vào roadmap riêng nếu cần
- Volume profile (volume phân bổ theo vùng giá) — ưu tiên thấp, có thể thêm sau

### Backend — Scenario Consultant Service

1. **New service** `ScenarioConsultantService`
   - File: `src/InvestmentApp.Infrastructure/Services/ScenarioConsultantService.cs`
   - Input: `symbol`, `entryPrice`, `timeHorizon` (Short/Medium/Long)
   - Output: `ScenarioSuggestion` — danh sách nodes gợi ý, mỗi node kèm `reasoning`
   - Dependency: `ITechnicalIndicatorService` (đã có) — lấy toàn bộ indicators + Fibonacci (sau bước 0a)

2. **Logic gợi ý theo tầm nhìn:**

   **Ngắn hạn — phân tích kỹ thuật là trọng tâm:**
   - Hỗ trợ/kháng cự gần nhất ← `SupportLevels/ResistanceLevels` ✅
   - RSI quá mua/quá bán ← `Rsi14` + `RsiSignal` ✅
   - MACD cắt lên/xuống ← `MacdSignal` ✅
   - Bollinger Bands squeeze/breakout ← `BollingerSignal` ✅
   - Fibonacci retracement/extension ← cần bước 0a
   - Volume spike ← `VolumeSignal` ✅
   - Reasoning phản ánh tâm lý: "Kháng cự 85,000 — vùng chốt lời đám đông (đỉnh 2 lần test, Fib 78.6%)", "RSI 75 — tâm lý quá lạc quan, xem xét chốt lời từng phần"

   **Trung hạn — xu hướng + kỹ thuật:**
   - EMA20/EMA50/EMA200 cross + trend ← EMA20/50 ✅, EMA200 cần bước 0a
   - Fibonacci retracement từ sóng trung hạn ← cần bước 0a
   - Vùng tích lũy/phân phối ← tính từ S/R clustering ✅
   - Breakout/breakdown + volume xác nhận ← `BollingerSignal` + `VolumeSignal` ✅

   **Dài hạn — cơ bản + kỹ thuật vĩ mô:**
   - Giá trị cơ bản (P/E, book value) ← `StockDetail` endpoint ✅
   - Vùng giá lịch sử (đỉnh/đáy) ← cần history > 6 tháng (bước 0c)
   - Fibonacci retracement từ sóng dài hạn ← cần bước 0a + 0c
   - EMA200 + S/R dài hạn ← cần bước 0a + 0c

   **Fibonacci — công cụ confluence, không phải indicator độc lập:**
   - Tính retracement (23.6%, 38.2%, 50%, 61.8%, 78.6%) và extension (127.2%, 161.8%) từ swing high/low phù hợp tầm nhìn
   - **Dùng như lớp xác nhận:** Fib level trùng S/R, EMA, hoặc volume cao → vùng confluence → tăng trọng số
   - Fib level đứng một mình → chỉ tham khảo, giảm trọng số
   - Reasoning ghi rõ confluence: "Hỗ trợ 68,000 (Fib 61.8% + EMA200 + đáy 3 tháng) — vùng hội tụ mạnh"

   **Hiển thị trên biểu đồ** (sau bước 0b):
   - Fibonacci levels vẽ lên candlestick chart (đường ngang + label %)
   - Highlight vùng confluence — nơi Fib trùng S/R hoặc EMA → tô đậm/đổi màu
   - User bật/tắt từng loại overlay

   **Mỗi con số gợi ý kèm reasoning có cơ sở:**
   - Giá gợi ý chốt lời/cắt lỗ/mua thêm → lý do cụ thể từ indicator nào, con số nào
   - VD: "Chốt lời tại ~85,000" → "Kháng cự swing high (86,500) + Fib 78.6% (84,800) + Bollinger upper (85,200) — 3 tín hiệu hội tụ"
   - VD: "Cắt lỗ tại ~68,000" → "Dưới S/R đáy 3 tháng (67,800) + dưới Fib 61.8% (71,360) + dưới EMA200 (71,800) — phá vỡ cấu trúc tăng"

3. **New endpoint** `GET /api/v1/trade-plans/scenario-suggestion?symbol=HPG&entryPrice=75000&timeHorizon=Medium`
   - File: `src/InvestmentApp.Api/Controllers/TradePlansController.cs`
   - Returns: `ScenarioSuggestionDto` (nodes[] kèm reasoning, technicalBasis)

### Frontend

1. **Chọn tầm nhìn** — khi tạo plan mới, thêm dropdown "Tầm nhìn đầu tư": Ngắn hạn / Trung hạn / Dài hạn
   - File: `frontend/src/app/features/trade-plan/trade-plan.component.ts`

2. **Nút "Gợi ý kịch bản"** — bên cạnh dropdown template hiện tại
   - Gọi API → nhận gợi ý → hiển thị preview kèm lý do cho mỗi node
   - Mỗi node gợi ý có checkbox để user chọn/bỏ từng node
   - Hiển thị lý do bên dưới mỗi node — tooltip/text nhỏ giải thích cơ sở
   - VD: "Vùng ≥ 85,000 (kháng cự — đỉnh 6 tháng) → Xem xét bán 30%"

3. **Nút "Áp dụng gợi ý"** — fill các node đã chọn vào tree editor
   - Tự động điền: condition type, condition value (con số giá), action type, action value, label
   - Lưu reasoning vào `ConditionNote` của mỗi node — user luôn thấy cơ sở phân tích
   - Sau khi fill, user vẫn chỉnh sửa tự do trước khi lưu plan
   - Flow: **Gợi ý → Chọn nodes → Áp dụng → Chỉnh sửa (nếu muốn) → Lưu**

4. **Nút "Tạo kế hoạch từ gợi ý"** — shortcut cho trường hợp chấp nhận toàn bộ
   - Áp dụng tất cả nodes gợi ý vào tree editor một lần
   - Tương đương chọn tất cả + "Áp dụng gợi ý"
   - User vẫn review và chỉnh sửa trước khi lưu plan

### Ví dụ output

```
Symbol: HPG | Entry: 75,000 | Tầm nhìn: Trung hạn (1-6 tháng)

Phân tích kỹ thuật:
  EMA20 = 77,500 | EMA50 = 76,200 | EMA200 = 71,800 | RSI = 58
  Bollinger: Upper = 85,200 | Middle = 76,800 | Lower = 68,400
  Fib retracement (từ đáy 62,000 → đỉnh 86,500):
    38.2% = 77,140 | 50% = 74,250 | 61.8% = 71,360
  Đỉnh 6T = 86,500 | Đáy 3T = 67,800 | ATR(14) = 1,850

Gợi ý kịch bản:
├── CHỐT LỜI — NẾU giá ≥ 85,000
│   ├── Cơ sở: kháng cự đỉnh 6T (86,500) + Fib extension 100%
│   ├── Gợi ý: bán 30% tại ~85,000
│   └── NẾU giá ≥ 95,000
│       ├── Cơ sở: Fib extension 138.2%
│       └── Gợi ý: bán thêm 30%, trailing stop 5%
│
├── CẮT LỖ — NẾU giá ≤ 68,000
│   ├── Cơ sở: dưới đáy 3T (67,800) + dưới Fib 61.8% (71,360) + dưới EMA200 (71,800)
│   │         + gần Bollinger Lower (68,400) → phá vỡ vùng hội tụ hỗ trợ mạnh
│   └── Gợi ý: cắt lỗ toàn bộ tại ~68,000
│
├── MUA THÊM — NẾU giá về vùng 71,000–72,000
│   ├── Cơ sở: Fib 61.8% (71,360) + EMA200 (71,800) — vùng hội tụ hỗ trợ (2 tín hiệu)
│   └── Gợi ý: mua thêm 20% vị thế tại ~71,500
│
└── SIDEWAY — NẾU sau 3 tháng, giá trong vùng 73,000–78,000
    ├── Cơ sở: quanh Fib 50% (74,250) + EMA50 (76,200) — không breakout
    └── Gợi ý: đánh giá lại, xem xét giảm 50% vị thế
```

> **Lưu ý:** Các con số vào/ra lệnh (85,000 · 68,000 · 71,500) được tính từ phân tích kỹ thuật, user chỉnh lại theo đánh giá cá nhân trước khi áp dụng.

### TDD

**Fibonacci (bước 0a):**
- Test: Fibonacci retracement tính đúng từ swing high/low (23.6%, 38.2%, 50%, 61.8%, 78.6%)
- Test: Fibonacci extension tính đúng (127.2%, 161.8%)
- Test: trả empty khi không đủ dữ liệu swing points

**Scenario Consultant:**
- Test: gợi ý ngắn hạn — mỗi node chốt lời/cắt lỗ có reasoning dẫn nguồn indicator cụ thể (S/R, Fib, RSI, BB)
- Test: gợi ý trung hạn — reasoning bao gồm EMA trend + Fibonacci + S/R
- Test: gợi ý dài hạn — reasoning bao gồm vùng giá lịch sử + cơ bản
- Test: confluence scoring — vùng có ≥ 2 indicator hội tụ có trọng số cao hơn vùng đơn lẻ
- Test: mỗi con số gợi ý (giá chốt lời, cắt lỗ, mua thêm) kèm reasoning không rỗng, chứa ít nhất 1 indicator source
- Test: fallback khi thiếu dữ liệu (trả generic suggestion kèm lý do "thiếu dữ liệu lịch sử")

---

## Implementation Order

```text
Phase 1 (quick wins, song song):
  [P0.1] Scenario History Dashboard     ← S, hiển thị data đã có
  [P0.2] Custom Templates               ← S-M, reuse workflow
  [P0.4] ATR Trailing Stop thực tế      ← S, ATR đã có sẵn — làm ngay

Phase 2 (UX upgrade):
  [P0.3] Visual Flowchart               ← M, cải thiện tree editor

Phase 3 (nền tảng kỹ thuật cho Consultant, song song):
  [P0.6a] Fibonacci + EMA200            ← S, thêm vào TechnicalIndicatorService
  [P0.6c] Mở rộng price history depth   ← S, bỏ hard-code 6 tháng
  [P0.6b] Candlestick + overlays chart  ← M, nâng cấp lightweight-charts

Phase 4 (advisory system):
  [P0.6] Scenario Consultant            ← M, gợi ý kịch bản có cơ sở kỹ thuật
  [P0.5] Gợi ý hành động theo vùng giá  ← S-M, on-demand khi vào Dashboard
        (P0.5 không block bởi P0.6 — hoạt động được với kịch bản user tự tạo)
```

---

## Verification

- `dotnet test` — all backend tests pass
- `ng test` — all frontend tests pass
- `ng build` — build thành công
- Update docs: `features.md`, `architecture.md`, `CHANGELOG.md`
