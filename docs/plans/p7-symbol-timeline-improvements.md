# P7 Improvement: Symbol Timeline Nâng cao

**Ngày lập:** 27/03/2026
**Trạng thái:** Done
**Feature gốc:** P7 — Symbol Timeline (7A + 7B + 7C) — ✅ Done
**Phương pháp:** Phân tích code thực tế → xác định gaps → đề xuất cải tiến

---

## Context

P7 đã implement đầy đủ 3 sub-phase: JournalEntry entity + candlestick chart (7A), MarketEvent overlay (7B), Emotion analytics + AI review (7C). Tuy nhiên có nhiều cơ hội nâng cấp analytics, behavioral insights, và chart interactions.

### Hiện trạng code

| Thành phần | Trạng thái | File chính |
|---|:---:|---|
| JournalEntry entity (5 types) | ✅ | `Domain/Entities/JournalEntry.cs` |
| MarketEvent entity (7 types) | ✅ | `Domain/Entities/MarketEvent.cs` |
| MarketEvent auto-crawl từ Vietstock | ❌ | Chỉ có manual add, chưa có crawl |
| Symbol Timeline query (aggregation) | ✅ | `Application/JournalEntries/Queries/GetSymbolTimeline/` |
| Candlestick chart + markers | ✅ | `symbol-timeline.component.ts:537-656` |
| Emotion histogram ribbon | ✅ | `symbol-timeline.component.ts:658-680` |
| Emotion summary (distribution) | ✅ | `GetSymbolTimelineQueryHandler.CalculateEmotionSummary()` |
| AI timeline review (copy prompt) | ✅ | `AiChatPanelComponent` + `buildAiContext()` |
| Journal/Event CRUD forms | ✅ | `symbol-timeline.component.ts` |
| Holding period calculation | ✅ | `GetSymbolTimelineQueryHandler.CalculateHoldingPeriods()` |
| --- Gaps --- | | |
| Chart dạng line (OHLC giống nhau) | ⚠️ | Dùng candlestick series nhưng hiển thị line |
| Emotion → P&L correlation | ❌ | Chỉ có distribution, không correlation |
| Confidence calibration | ❌ | Không so sánh confidence vs outcome |
| Behavioral pattern detection | ❌ | Không detect FOMO/panic/revenge |
| Emotion trend over time | ❌ | Chỉ có static distribution |
| Dedicated AI timeline endpoint | ❌ | Dùng generic journal-review |
| EmotionalState validation | ⚠️ | Free-form string, không enum |
| Export timeline | ❌ | Không có PDF/CSV export |
| Holding period highlight on chart | ⚠️ | Có data nhưng chưa vẽ background |

### Chart thực tế

Biểu đồ hiện tại hiển thị dạng **line chart** (dù code dùng `addCandlestickSeries`) vì market data API trả về OHLC gần nhau. Markers hiện có:
- 📓 (A) — Journal entries, màu indigo, hình vuông trên chart
- 📰 (E) — Market events, màu vàng, hình vuông
- ▲▼ — Trade markers (BUY/SELL)
- ⚠️ — Alert markers

---

## Đề xuất cải tiến (xếp theo ưu tiên)

| # | Đề xuất | Size | Lý do ưu tiên |
|---|---------|:---:|---------------|
| P7.1 | Emotion ↔ P&L Correlation | S-M | Insight giá trị nhất — trả lời "cảm xúc nào dẫn đến lỗ?" |
| P7.2 | Confidence Calibration Report | S | Quick win — so sánh confidence vs actual outcome |
| P7.3 | Behavioral Pattern Detection | M | Detect FOMO, panic sell, revenge trading |
| P7.4 | Chart UX Enhancements | S-M | Holding period highlight, chuyển sang line series, tooltip cải tiến |
| P7.5 | Dedicated AI Timeline Review | M | Prompt chuyên sâu hơn generic journal-review |
| P7.6 | Emotion Trend Over Time | S | Time-series emotion chart thay vì static distribution |
| P7.7 | Export Timeline (PDF/Image) | S-M | Lưu lại timeline cho review offline |
| P7.8 | Vietstock Event Crawl | M | Auto-crawl tin tức + sự kiện DN từ Vietstock API, bổ sung MarketEvent |

---

## P7.1: Emotion ↔ P&L Correlation (S-M)

**Vấn đề:** Emotion summary chỉ có `distribution` (count mỗi emotion) và `averageConfidence`. Không trả lời được câu hỏi quan trọng nhất: *"Khi tôi FOMO, tôi lỗ bao nhiêu?"*

### Backend

1. **Extend `CalculateEmotionSummary()`** trong `GetSymbolTimelineQueryHandler`
   - File: `src/InvestmentApp.Application/JournalEntries/Queries/GetSymbolTimeline/GetSymbolTimelineQueryHandler.cs`
   - Thêm logic correlation:
     1. Lấy journal entries có `TradeId` (gắn với trade cụ thể)
     2. Lấy P&L từ trade: `(sellPrice - buyPrice) × quantity`
     3. Group by `EmotionalState` → tính: average P&L, win rate, count
   - Thêm fields vào `EmotionSummaryDto`:

```csharp
public class EmotionSummaryDto
{
    // Existing
    public Dictionary<string, int> Distribution { get; set; }
    public double? AverageConfidence { get; set; }
    public int TotalEntries { get; set; }

    // New — P7.1
    public List<EmotionCorrelationDto> Correlations { get; set; }
}

public class EmotionCorrelationDto
{
    public string Emotion { get; set; }
    public int TradeCount { get; set; }
    public decimal AveragePnlPercent { get; set; }  // avg P&L %
    public double WinRate { get; set; }               // % trades lãi
    public decimal TotalPnl { get; set; }             // tổng P&L VND
}
```

2. **Cần thêm** `ITradeRepository.GetByIdsAsync(tradeIds[])` hoặc reuse trades đã fetch trong timeline
   - Timeline query đã lấy trades → reuse, không cần query thêm

### Frontend

1. **Correlation panel** — bên dưới emotion distribution hiện tại
   - File: `frontend/src/app/features/symbol-timeline/symbol-timeline.component.ts`
   - Bảng: `| Cảm xúc | Số GD | Win Rate | P&L TB (%) | Tổng P&L |`
   - Color-coded: win rate > 60% = xanh, < 40% = đỏ
   - Highlight insight: "Khi **FOMO** → Win rate chỉ 25%, P&L TB -3.2%"
   - Highlight insight: "Khi **Bình tĩnh** → Win rate 72%, P&L TB +5.1%"

### TDD
- Test: correlation tính đúng khi journal có TradeId + matching SELL trade
- Test: correlation trả về empty khi không có journal-trade links
- Test: win rate = trades lãi / total trades cho mỗi emotion
- Test: chỉ tính trades đã có SELL (closed positions)

---

## P7.2: Confidence Calibration Report (S)

**Vấn đề:** User đặt `confidenceLevel = 9` nhưng trade đó lỗ -10%. Không có feedback loop để calibrate confidence → overconfidence hoặc under-confidence.

### Backend

1. **New method** `CalculateConfidenceCalibration()` trong timeline query handler
   - Group journal entries by confidenceLevel ranges: Low (1-3), Medium (4-6), High (7-8), Very High (9-10)
   - Cho mỗi range: tính win rate, avg P&L % (từ linked trades)
   - Returns: `ConfidenceCalibrationDto[]`

```csharp
public class ConfidenceCalibrationDto
{
    public string Range { get; set; }       // "Low (1-3)", "Medium (4-6)", etc.
    public int EntryCount { get; set; }
    public int TradeCount { get; set; }
    public double WinRate { get; set; }
    public decimal AveragePnlPercent { get; set; }
    public bool IsCalibrated { get; set; }  // High confidence + high win rate = calibrated
}
```

2. **Thêm vào `EmotionSummaryDto`**:
   - `ConfidenceCalibration: ConfidenceCalibrationDto[]`

### Frontend

1. **Calibration widget** — bar chart ngang
   - X axis: confidence ranges
   - Y axis: win rate %
   - Đường tham chiếu: "calibrated" = confidence % ≈ win rate %
   - Insight: "Confidence 9-10 → Win rate 45% → **Overconfident** ⚠️"

### TDD
- Test: confidence range grouping đúng
- Test: calibration "overconfident" khi confidence > win rate + 20%
- Test: calibration "well-calibrated" khi |confidence - win rate| < 15%
- Test: trả về null khi không đủ data (< 5 trades per range)

---

## P7.3: Behavioral Pattern Detection (M)

**Vấn đề:** Không detect được trading biases. User lặp lại sai lầm mà không nhận ra pattern.

### Backend

1. **New service** `IBehavioralAnalysisService`
   - File: `src/InvestmentApp.Application/Common/Interfaces/IBehavioralAnalysisService.cs`
   - File: `src/InvestmentApp.Infrastructure/Services/BehavioralAnalysisService.cs`

2. **Patterns cần detect:**

| Pattern | Logic | Severity |
|---------|-------|:---:|
| **FOMO Entry** | Journal (PreTrade, emotion=FOMO/Hào hứng) → BUY trade trong 24h | ⚠️ Warning |
| **Panic Sell** | Journal (DuringTrade, emotion=Sợ hãi) → SELL trade trong 24h, trước SL | 🔴 Critical |
| **Revenge Trading** | SELL trade lỗ → BUY trade mới trong 4h (không có PreTrade journal) | 🔴 Critical |
| **Overtrading** | > 3 BUY trades cùng ngày cho cùng symbol | ⚠️ Warning |
| **Anchoring Bias** | Journal mentions "sẽ lên lại" khi giá giảm > 15% từ ATH | ℹ️ Info |

3. **Method** `DetectPatternsAsync(userId, symbol?, from?, to?)`
   - Input: journal entries + trades + timeline
   - Returns: `BehavioralPatternDto[]` (PatternType, Severity, Description, OccurredAt, RelatedTradeId, RelatedJournalId)

4. **Thêm vào timeline response** — field `behavioralPatterns` trong `SymbolTimelineDto`

### Frontend

1. **Pattern alerts panel** — hiển thị trên Symbol Timeline page
   - Cards: icon severity + mô tả pattern + link đến trade/journal liên quan
   - VD: "🔴 **Bán panic** — 15/03 bạn bán VNM vội vã khi sợ hãi. Giá sau đó phục hồi +8%."
   - Tổng kết: "Trong 6 tháng qua: 3 lần FOMO, 2 lần panic sell, 1 lần revenge trading"

2. **Pattern markers trên chart** — overlay markers tại thời điểm pattern xảy ra
   - Icon: ⚠️ (warning), 🔴 (critical), ℹ️ (info)
   - Click → scroll đến detail

### TDD
- Test: detect FOMO khi emotion=FOMO + BUY trong 24h
- Test: detect panic sell khi emotion=Sợ hãi + SELL trong 24h
- Test: detect revenge trading: SELL lỗ → BUY < 4h
- Test: không false positive khi có PreTrade journal trước BUY (planned trade)
- Test: pattern count tổng hợp đúng

---

## P7.4: Chart UX Enhancements (S-M)

**Vấn đề:**
1. Biểu đồ dùng `addCandlestickSeries` nhưng hiển thị như line (OHLC giống nhau) → nên chuyển sang `addLineSeries` cho rõ ràng
2. Holding periods có data nhưng chưa vẽ highlight background trên chart
3. Tooltip khi hover marker chưa có thông tin chi tiết

### Frontend

1. **Chuyển sang Line Series** — match thực tế hiển thị
   - File: `frontend/src/app/features/symbol-timeline/symbol-timeline.component.ts`
   - Thay `addCandlestickSeries` bằng `addLineSeries` + style:
     ```typescript
     this.priceSeries = this.chart.addLineSeries({
       color: '#6366f1',
       lineWidth: 2,
       priceLineVisible: true,
       lastValueVisible: true
     });
     ```
   - Data: map `StockPrice.close` → `{ time, value }` (đơn giản hơn OHLC)
   - Markers vẫn hoạt động tương tự trên line series

2. **Holding period highlight** — vẽ background zones cho holding periods
   - Dùng lightweight-charts `createPriceLine` hoặc custom plugin
   - Vùng nắm giữ: background semi-transparent xanh nhạt
   - Hiển thị label: "Nắm giữ 500 cp" bên trong zone
   - Data từ `timeline.holdingPeriods` đã có sẵn

3. **Enhanced tooltips** — khi hover vào marker
   - Journal: `"📓 {title} — {emotionalState} ({confidenceLevel}/10)"`
   - Trade: `"▲ MUA 500 @ 72,000 — P&L: +8.2%"`
   - Event: `"📊 KQKD Q1 — Doanh thu +12%"`
   - Hiện tại marker chỉ hiển thị icon, thiếu context

4. **Click marker → scroll** — click marker trên chart → scroll timeline list đến item tương ứng
   - Highlight selected item trong list
   - Ngược lại: click item trong list → crosshair jump đến marker trên chart

### TDD
- Test: line series render đúng từ price data
- Test: holding period zones match holdingPeriods data
- Test: marker click → correct item selected

---

## P7.5: Dedicated AI Timeline Review (M)

**Vấn đề:** AI review hiện dùng `buildAiContext()` tạo string context + gửi qua generic `ai/chat` endpoint. Không có prompt chuyên biệt cho timeline analysis. Context bị giới hạn 10 items gần nhất.

### Backend

1. **New query** `BuildTimelineAiContextQuery(userId, symbol, from?, to?)`
   - File: `src/InvestmentApp.Application/JournalEntries/Queries/BuildTimelineAiContext/`
   - Tạo context phong phú hơn frontend `buildAiContext()`:
     - Tất cả journal entries (không giới hạn 10)
     - Trades với P&L đã tính
     - Emotion distribution + correlation (P7.1)
     - Confidence calibration (P7.2)
     - Behavioral patterns (P7.3)
     - Holding periods + current position status

2. **New endpoint** `POST /api/v1/ai/timeline-review`
   - File: `src/InvestmentApp.Api/Controllers/AiController.cs`
   - Dedicated prompt template cho timeline analysis:

```
Bạn là huấn luyện viên tâm lý giao dịch (trading psychology coach).
Phân tích timeline giao dịch của nhà đầu tư cho mã {symbol}:

## Dữ liệu cảm xúc
{emotion_correlation_table}

## Mẫu hành vi phát hiện
{behavioral_patterns}

## Confidence Calibration
{calibration_data}

## Timeline chi tiết
{full_timeline}

Hãy phân tích:
1. Mẫu cảm xúc lặp lại và ảnh hưởng đến P&L
2. Bias hành vi cần khắc phục (ưu tiên severity cao)
3. Điểm mạnh cần phát huy
4. 3 hành động cụ thể để cải thiện kỷ luật giao dịch
5. Đánh giá tổng thể: điểm kỷ luật (0-10), xu hướng cải thiện hay xấu đi
```

3. **Extend `IAiAssistantService`** — thêm method `StreamTimelineReviewAsync()`

### Frontend

1. **Thay đổi AI panel context** — gọi dedicated endpoint thay vì generic
   - File: `frontend/src/app/features/symbol-timeline/symbol-timeline.component.ts`
   - `useCase: 'timeline-review'` → gọi `/api/v1/ai/timeline-review`
   - Hiển thị rich response: structured sections thay vì plain text

### TDD
- Test: context builder include đủ emotion correlation + patterns
- Test: context không vượt quá token limit (truncate nếu cần)
- Test: prompt template render đúng với data thực

---

## P7.6: Emotion Trend Over Time (S)

**Vấn đề:** Emotion summary chỉ là snapshot tĩnh (distribution). Không thấy được: "Tháng này tôi bình tĩnh hơn tháng trước?"

### Backend

1. **Extend `CalculateEmotionSummary()`** — thêm trend data
   - Group entries by month/week
   - Cho mỗi period: dominant emotion, average confidence, entry count
   - Returns: `EmotionTrendDto[]`

```csharp
public class EmotionTrendDto
{
    public string Period { get; set; }            // "2026-01", "2026-02"
    public string DominantEmotion { get; set; }   // emotion có count cao nhất
    public double AverageConfidence { get; set; }
    public int EntryCount { get; set; }
    public Dictionary<string, int> Distribution { get; set; }  // breakdown
}
```

2. **Thêm vào `EmotionSummaryDto`**: `Trends: EmotionTrendDto[]`

### Frontend

1. **Trend chart** — stacked area chart hoặc horizontal bars theo tháng
   - X axis: months
   - Y axis: % distribution (stacked)
   - Colors: theo EMOTION_COLORS mapping
   - Dưới chart: "Xu hướng: Bình tĩnh +15% ↑, FOMO -20% ↓ so tháng trước"

2. **Confidence trend line** — overlay line trên emotion trend
   - Hiển thị average confidence theo tháng
   - Insight: "Confidence tăng từ 5.2 → 7.8 trong 3 tháng"

### TDD
- Test: trend grouping đúng theo tháng
- Test: dominant emotion = emotion có count cao nhất
- Test: confidence average tính đúng (bỏ qua null)
- Test: trả về empty khi entries < 2 months

---

## P7.7: Export Timeline (PDF/Image) (S-M)

**Vấn đề:** User muốn lưu timeline cho review offline, chia sẻ với mentor/nhóm. Hiện không có export.

### Frontend-only (không cần backend)

1. **Export as Image (PNG)** — dùng `html2canvas` (hoặc `dom-to-image`)
   - File: `frontend/src/app/features/symbol-timeline/symbol-timeline.component.ts`
   - Nút: "📸 Chụp timeline" → capture chart + timeline list → download PNG
   - Thêm dependency: `npm install html2canvas`

2. **Export as CSV** — xuất timeline data ra file CSV
   - Columns: `Ngày | Loại | Tiêu đề | Cảm xúc | Confidence | Giá | Ghi chú`
   - Dùng native JS, không cần dependency mới

3. **Share via Copy** — "Sao chép tóm tắt" → copy text summary to clipboard
   - Format: plain text summary với emotion stats + key trades

### TDD
- Test: CSV export format đúng columns
- Test: CSV includes tất cả filtered items
- Test: copy text summary format đúng

---

## P7.8: Vietstock Event Crawl — Auto-crawl Tin tức & Sự kiện (M)

**Vấn đề:** MarketEvent hiện chỉ có manual add — user phải tự nhập từng sự kiện. Vietstock có 2 API trả dữ liệu tin tức và sự kiện doanh nghiệp (cổ tức, phát hành, ĐHCĐ...) theo mã CK, có thể crawl tự động.

> **Nguồn:** `https://finance.vietstock.vn/{symbol}/tin-tuc-su-kien.htm`
> **Reverse-engineered 27/03/2026** — 2 API endpoints xác nhận hoạt động.

### Vietstock API Specification

#### API 1: `POST /data/GetNews` — Tin tức theo mã

**Request** (form-encoded):
```
code=VND
type=-1              # -1 = tất cả, có thể filter theo type
page=1
pageSize=20
__RequestVerificationToken=...   # CSRF token bắt buộc
```

**Response** (JSON array):
```json
[{
  "StockCode": "VND",
  "ChannelID": 999,
  "ArticleID": 1417480,
  "Title": "VND: Thông báo Quyết định...",
  "Head": "mô tả ngắn...",
  "PublishTime": "/Date(1774595211000)/",
  "URL": "/2026/03/vnd-thong-bao-...",
  "Source": "HOSE",
  "TotalRow": 162,
  "Content": "",
  "Icon": null
}]
```

**Fields mapping → MarketEvent:**
| Vietstock | MarketEvent | Ghi chú |
|---|---|---|
| `Title` | `Title` | |
| `Head` | `Description` | Tóm tắt tin |
| `PublishTime` | `EventDate` | Parse .NET `/Date(ms)/` format |
| `URL` | `Source` | Prefix `https://finance.vietstock.vn` |
| `Source` | — | HOSE, SSC, FILI (metadata) |
| — | `EventType` | `News` (default cho GetNews) |
| — | `Symbol` | Từ `code` param |

#### API 2: `POST /data/EventsTypeData` — Sự kiện doanh nghiệp

**Request** (form-encoded):
```
eventTypeID=1        # 1 = Cổ tức & Vốn (xem bảng bên dưới)
channelID=0          # 0 = tất cả channels
code=VND
catID=-1             # -1 = tất cả categories
page=1
pageSize=20
orderBy=Date1
orderDir=DESC
__RequestVerificationToken=...
```

**Response** (JSON `[[data], [totalCount]]`):
```json
[[{
  "EventID": 204653,
  "EventTypeID": 1,
  "ChannelID": 13,
  "Code": "VND",
  "CompanyName": "CTCP Chứng khoán VNDIRECT",
  "Name": "Trả cổ tức bằng tiền mặt",
  "Note": "Trả cổ tức năm 2024 bằng tiền, 500 đồng/CP",
  "Title": "VND: Thông báo ngày ĐKCC...",
  "Content": "<p>...</p>",
  "GDKHQDate": "/Date(1750698000000)/",
  "NDKCCDate": "/Date(1750784400000)/",
  "Time": "/Date(1752512400000)/",
  "FileUrl": "https://static2.vietstock.vn/...",
  "Exchange": "HOSE",
  "DateOrder": "/Date(1750698000000)/",
  "Row": 1
}], [21]]
```

**ChannelID → MarketEventType mapping:**
| ChannelID | Name (Vietstock) | MarketEventType |
|:---:|---|---|
| 13 | Trả cổ tức bằng tiền mặt | `Dividend` |
| 15 | Trả cổ tức bằng cổ phiếu | `Dividend` |
| 16 | Phát hành thêm | `RightsIssue` |
| (ĐHCĐ) | Đại hội cổ đông | `ShareholderMtg` |
| (KQKD) | Kết quả kinh doanh | `Earnings` |
| (GDNB) | Giao dịch nội bộ | `InsiderTrade` |

**EventTypeID values** (cần thêm crawl để xác nhận đầy đủ):
| eventTypeID | Loại sự kiện |
|:---:|---|
| 1 | Cổ tức & Vốn |
| (2-6?) | Cần khám phá thêm — ĐHCĐ, KQKD, GDNB... |

#### CSRF Token Flow

Cả 2 API yêu cầu `__RequestVerificationToken`. Flow lấy token:
1. `GET https://finance.vietstock.vn/{symbol}/tin-tuc-su-kien.htm`
2. Parse cookie `__RequestVerificationToken` + hidden input `<input name="__RequestVerificationToken" value="...">`
3. Gửi token trong cookie header + request body

### Backend

1. **New provider** `IVietstockEventProvider`
   - File: `src/InvestmentApp.Application/Common/Interfaces/IVietstockEventProvider.cs`
   - Methods:
     - `Task<IEnumerable<VietstockNewsDto>> GetNewsAsync(string symbol, int page = 1, int pageSize = 20, CancellationToken ct = default)`
     - `Task<IEnumerable<VietstockEventDto>> GetEventsAsync(string symbol, int eventTypeId = 1, int page = 1, int pageSize = 20, CancellationToken ct = default)`

2. **Implementation** `VietstockEventProvider`
   - File: `src/InvestmentApp.Infrastructure/Services/Vietstock/VietstockEventProvider.cs`
   - File: `src/InvestmentApp.Infrastructure/Services/Vietstock/VietstockApiModels.cs` — DTOs
   - Logic:
     1. `HttpClient` GET trang HTML → parse CSRF token từ cookie + body
     2. POST `/data/GetNews` hoặc `/data/EventsTypeData` với token
     3. Parse `.NET /Date(ms)/` format → `DateTime`
     4. Map response → internal DTOs
   - **Cache CSRF token** — reuse trong 30 phút, refresh khi 403/expired
   - **Rate limit** — max 1 request/giây để tránh bị block

3. **New command** `CrawlVietstockEventsCommand(symbol, crawlNews?, crawlEvents?)`
   - File: `src/InvestmentApp.Application/MarketEvents/Commands/CrawlVietstockEvents/CrawlVietstockEventsCommand.cs`
   - Logic:
     1. Gọi `IVietstockEventProvider.GetNewsAsync()` + `GetEventsAsync()`
     2. **Dedup:** check `MarketEvent` existing bằng `(Symbol, Title, EventDate)` — tránh tạo duplicate
     3. Map → `MarketEvent` entity:
        - GetNews → `EventType = News` (hoặc phân loại nếu Title chứa keywords)
        - EventsTypeData → map theo ChannelID table ở trên
     4. Bulk insert vào `IMarketEventRepository`
   - Returns: `CrawlResultDto { newsAdded, eventsAdded, duplicatesSkipped }`

4. **New endpoint** `POST /api/v1/market-events/crawl`
   - File: `src/InvestmentApp.Api/Controllers/MarketEventsController.cs`
   - Body: `{ symbol: "VND", crawlNews: true, crawlEvents: true }`
   - Returns: `CrawlResultDto`
   - **Không cần auth Vietstock** — API public, chỉ cần CSRF token

5. **DI Registration**
   - File: `src/InvestmentApp.Api/Program.cs` — register `IVietstockEventProvider` + `HttpClient`
   - Cấu hình base URL trong `appsettings.json`:
     ```json
     "Vietstock": {
       "BaseUrl": "https://finance.vietstock.vn"
     }
     ```

### Frontend

1. **Nút "Crawl từ Vietstock"** — trên Symbol Timeline page, cạnh nút "Thêm sự kiện"
   - File: `frontend/src/app/features/symbol-timeline/symbol-timeline.component.ts`
   - Click → gọi `marketEventService.crawl(symbol)` → hiển thị kết quả toast
   - Nút có icon 🔄 + text "Cập nhật tin tức"
   - Loading state khi đang crawl

2. **Extend MarketEventService** — thêm method `crawl()`
   - File: `frontend/src/app/core/services/market-event.service.ts`
   - `crawl(symbol: string): Observable<CrawlResult>`
   - `POST /api/v1/market-events/crawl`

3. **Crawl result toast** — hiển thị số tin/sự kiện đã thêm
   - "Đã thêm 5 tin tức, 3 sự kiện. Bỏ qua 2 trùng lặp."

### TDD

**Backend:**
- Test: parse CSRF token từ HTML response (mock HttpClient)
- Test: parse `/Date(1774595211000)/` → đúng DateTime UTC
- Test: GetNews map đúng fields → VietstockNewsDto
- Test: EventsTypeData map đúng ChannelID → MarketEventType
- Test: dedup — không tạo MarketEvent khi đã tồn tại (Symbol + Title + EventDate)
- Test: dedup — tạo mới khi Title khác dù cùng Symbol + EventDate
- Test: crawl command trả về đúng count (added, skipped)
- Test: EventsTypeData response `[[data], [count]]` parse đúng nested array

**Lưu ý kỹ thuật:**
- `.NET /Date(ms)/` format: extract milliseconds từ regex `/Date\((\d+)\)/` → `DateTimeOffset.FromUnixTimeMilliseconds()`
- Vietstock có thể thay đổi API — cần error handling tốt + log khi response format thay đổi
- Không crawl tự động trong Worker (tránh bị block IP) — chỉ on-demand khi user bấm nút

---

## Implementation Order

```text
Phase 1 (quick wins, độc lập — song song):
  [P7.4] Chart UX Enhancements          ← S-M, chuyển line series + holding highlight
  [P7.6] Emotion Trend Over Time        ← S, extend emotion summary
  [P7.8] Vietstock Event Crawl          ← M, bổ sung data cho MarketEvent

Phase 2 (analytics core):
  [P7.1] Emotion ↔ P&L Correlation      ← S-M, insight giá trị nhất
  [P7.2] Confidence Calibration          ← S, extend từ P7.1

Phase 3 (behavioral intelligence):
  [P7.3] Behavioral Pattern Detection    ← M, cần P7.1 data
  [P7.5] Dedicated AI Timeline Review    ← M, cần P7.1 + P7.2 + P7.3

Phase 4 (utility):
  [P7.7] Export Timeline                 ← S-M, frontend-only
```

### Dependencies

```text
P7.4 ────────────────────────────────────┐
P7.6 ────────────────────────────────────┤
P7.8 ────────────────────────────────────┤
                                         │
P7.1 → P7.2 → P7.3 → P7.5              │── có thể song song
                                         │
P7.7 ────────────────────────────────────┘
```

---

## Verification

- `dotnet test` — all backend tests pass
- `ng test` — all frontend tests pass
- `ng build` — build thành công
- Update docs: `features.md`, `architecture.md`, `CHANGELOG.md`
