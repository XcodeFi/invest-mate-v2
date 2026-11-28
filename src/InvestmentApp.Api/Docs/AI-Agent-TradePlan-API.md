# Invest Mate — AI Agent API (Trade Plan)

Tài liệu authoritative cho Claude. Base URL và `X-Api-Key` do NPU cấp lúc gọi (không có ở đây).
Trước MỌI ghi: tóm tắt một dòng, chờ người dùng "chốt" trong chat, rồi mới gọi.
App là nhật ký/tracker — KHÔNG đặt lệnh sàn, KHÔNG tiền thật, dữ liệu sửa được.

## Mục lục (ý định → mục)
- Lập plan → [Create Plan](#create-plan)
- Sửa plan → [Update Plan](#update-plan)
- Chuyển trạng thái → [Status](#status)
- Ghi trade / thực hiện → [Create Trade](#create-trade) + [Full-execute](#full-execute)
- Tính phí/thuế trước khi ghi → [Fees](#fees)
- Đọc plan → [Read](#read)
- Liệt kê danh mục (lấy portfolioId) → [Portfolios](#portfolios)
- Đọc holdings thật → [Positions](#positions)
- Quản watchlist (CRUD) → [Watchlists](#watchlists)
- Nhật ký theo mã → [Journal Entries](#journal-entries)
- Nhật ký theo trade → [Journals](#journals)
- Sự kiện theo mã → [Symbol timeline](#symbol-timeline)
- Quy tắc bắt buộc → [Rules](#rules)

## Auth & lỗi
- Header: `X-Api-Key: {ApiKey}`. Fail auth có thể trả 302 (Google) — coi như chưa xác thực.
- 400 = validation sai; 401 = thiếu/sai key; 404 = không sở hữu/không tồn tại.

## <a id="rules"></a>Rules (BẮT BUỘC)
1. Discipline gate (chặn Draft→Ready): `thesis` ≥15 ký tự; nếu `quantity*entryPrice ≥ 5%*accountBalance` → `thesis` ≥30 ký tự VÀ ≥1 `invalidationCriteria` với `detail` ≥20 ký tự.
2. Gửi kèm `accountBalance` khi tạo plan (thiếu nó gate chỉ còn 15 ký tự).
3. `entryPrice`/`stopLoss`/`quantity` > 0. `invalidationCriteria.trigger ∈ {EarningsMiss, TrendBreak, NewsShock, ThesisTimeout, Manual}`.
4. Trade: `tradeType ∈ {BUY, SELL}`, `quantity`/`price` > 0, `fee`/`tax` ≥ 0, `symbol` ≤10.
5. Không sửa plan `Executed`/`Reviewed`. Ghi Trade TRƯỚC khi mark Executed.
6. **Ngoại lệ duy nhất của rule 5:** stop-loss dời được cả khi plan `Executed`, bằng tool MCP `move_stop_loss`. Nới SL (Buy: dời xuống, Sell: dời lên) BẮT BUỘC có `reason`, thiếu là 409; siết SL thì `reason` tuỳ chọn. `newStopLoss` > 0. Plan `Reviewed`/`Cancelled` vẫn không dời được. Mỗi lần nới bị đếm vào điểm kỷ luật — nói rõ với người dùng trước khi gọi.

## <a id="read"></a>Read
- `GET /api/v1/ai/agent/trade-plans?activeOnly=true|false`
- `GET /api/v1/ai/agent/trade-plans/{id}`

## <a id="create-plan"></a>Create Plan
`POST /api/v1/ai/agent/trade-plans` — luôn tạo ở trạng thái Draft (server bỏ qua `status`/`tradeId` nếu gửi).
Body (field chính): `symbol`, `direction` (Buy|Sell), `entryPrice`, `stopLoss`, `target`, `quantity`,
`portfolioId?`, `strategyId?`, `marketCondition`, `thesis`, `notes`, `confidenceLevel` (1-10),
`riskPercent?`, `accountBalance?`, `riskRewardRatio?`, `timeHorizon` (ShortTerm|MediumTerm|LongTerm),
`expectedReviewDate?`, `invalidationCriteria[]` `{trigger, detail}`, `checklist[]` `{label,category,checked,critical,hint}`,
`entryMode` (Single|ScalingIn|DCA) + `lots[]` `{lotNumber,plannedPrice,plannedQuantity,allocationPercent?,label?}`,
`exitTargets[]` `{level,actionType(TakeProfit|CutLoss|TrailingStop|PartialExit),price,quantity?,percentOfPosition?,label?}`,
`exitStrategyMode` (Simple|Advanced) + `scenarioNodes[]` `{nodeId,parentId?,order,label,conditionType,conditionValue?,actionType,actionValue?,trailingStopConfig?}`.
`scenarios[]` `{label,probabilityPercent,exitPrice}` (tuỳ chọn, chỉ lệnh Buy): bảng kịch bản kết quả của CẢ vị thế; tổng `probabilityPercent` phải = 100 (± 0,5) thì hệ thống mới tính kỳ vọng EV theo R (đã trừ phí ước tính). Xác suất là ước lượng của người dùng, không điền sẵn theo tỉ lệ nền (tránh neo).
Enums scenario: `conditionType ∈ {PriceAbove,PriceBelow,PricePercentChange,TrailingStopHit,TimeElapsed}`;
`actionType ∈ {SellPercent,SellAll,MoveStopLoss,MoveStopToBreakeven,ActivateTrailingStop,AddPosition,SendNotification}`;
`trailingStopConfig.method ∈ {Percentage,ATR,FixedAmount}`.

Các tập giá trị trên **cũng nằm trong `inputSchema` của tool MCP** (mảng `enum`), nên client MCP không cần
đọc tài liệu này mới gọi đúng — tài liệu chỉ dành cho người. `scenarioNodes[].actionType` và
`scenarioNodes[].conditionType` **bắt buộc**, không có giá trị mặc định; `trailingStopConfig.method` bỏ
trống hoặc `null` thì về `Percentage`. Lưu ý `exitTargets[].actionType` là **tập giá trị khác** dù trùng tên.

Ví dụ tối thiểu:
```json
{ "symbol":"VNM","direction":"Buy","entryPrice":50,"stopLoss":47,"target":60,"quantity":100,
  "accountBalance":100000,"thesis":"Breakout khoi nen tich luy, volume xac nhan",
  "invalidationCriteria":[{"trigger":"TrendBreak","detail":"Dong cua duoi 47 hai phien lien tiep"}] }
```
Trả về: `201 { "id": "<planId>" }`.

## <a id="update-plan"></a>Update Plan
`PUT /api/v1/ai/agent/trade-plans/{id}` — cùng bộ field (đều optional). Không dùng được nếu plan Executed/Reviewed.

## <a id="status"></a>Status
`PATCH /api/v1/ai/agent/trade-plans/{id}/status` body `{ "status": "ready|inprogress|executed|cancelled", "tradeId?": "...", "evGateReason?": "..." }`. Chuyển `ready` với kế hoạch Buy có kỳ vọng EV ≤ 0 hoặc chưa có bảng kịch bản thì BẮT BUỘC `evGateReason` (≥ 10 ký tự), thiếu thì 400 `EV_GATE_REASON_REQUIRED`. Lý do được lưu và hiện ở review. `inprogress`/`executed` ghi lại lệnh đã khớp nên không bị chặn, nhưng kế hoạch bị đánh dấu "vào lệnh không qua cổng kỳ vọng" nếu EV ≤ 0 hoặc chưa ước lượng mà không có lý do.
`restore` bị chặn (400). `executed` cần `tradeId` (tạo trade trước).

## <a id="create-trade"></a>Create Trade
`POST /api/v1/ai/agent/trades` body `{ "portfolioId?","symbol","tradeType":"BUY|SELL","quantity","price","fee?","tax?","tradeDate?" }`.
Trả về `201 { "id": "<tradeId>" }`. Portfolio phải thuộc bạn.
- `portfolioId` **bỏ trống được**: nếu bạn chỉ có 1 danh mục → tự chọn; có >1 → `400` kèm danh sách `{id,name}` để chọn (dùng [Portfolios](#portfolios)); có 0 → `400`.
- `fee`/`tax` **bỏ trống được**: server tự tính (phí giao dịch + VAT + thuế TNCN, đúng chiều BUY/SELL) — khớp cách app tính khi nhập tay. Gửi giá trị (kể cả `0`) → giữ nguyên, không tự tính. Muốn xem trước con số → gọi [Fees](#fees).

## <a id="full-execute"></a>Full-execute (thực hiện)
1. `POST trade-plans` → planId. 2. `POST trades` → tradeId. 3. `PATCH trade-plans/{planId}/status {status:"executed", tradeId}`.
Nếu bước 3 lỗi sau khi đã tạo trade: báo rõ `planId` + `tradeId` để dọn tay.
Mẹo: bước 2 có thể bỏ `portfolioId`/`fee`/`tax` (xem [Create Trade](#create-trade)) — không phải hỏi lại người dùng khi chỉ có 1 danh mục.

## <a id="fees"></a>Fees (tính phí/thuế)
`POST /api/v1/ai/agent/fees/calculate` body `{ "symbol","tradeType":"BUY|SELL","quantity","price" }` →
`200 { "transactionFee","tax","vat","totalFees","breakdown":{ "transactionFee","tax","vat" } }`.
Thuế TNCN (`tax`) chỉ > 0 khi SELL (0.1%). Số tiền ≤ 0 → `400`. Dùng để xem trước trước khi ghi trade;
khi ghi có thể để `fee=totalFees`, `tax=breakdown.tax` (hoặc bỏ trống để server tự điền y hệt).

---

Các nhóm dưới đây thao tác trực tiếp trên dữ liệu thật của chủ khóa. Read chạy ngay. Watchlist/journal
là **low-stakes** — agent được ghi/sửa/xóa mà không bắt buộc gate "chốt" như trade plan/trade (hành vi
thuộc persona NPU, không phải backend). Mọi route scope theo `sub` = chủ khóa; không thấy dữ liệu người khác.

## <a id="portfolios"></a>Portfolios (liệt kê danh mục)
- `GET /api/v1/ai/agent/portfolios` — danh mục của bạn (`PortfolioSummaryDto` `{id, name, initialCapital,
  netCashFlow, currentCapital, tradeCount, uniqueSymbols, ...}`). Dùng để lấy `portfolioId` khi ghi trade
  (nhất là khi có >1 danh mục).

## <a id="positions"></a>Positions (đọc holdings thật)
- `GET /api/v1/ai/agent/positions?portfolioId={id?}` — vị thế đang mở (qty, giá vốn bình quân, P/L) trên tất cả
  hoặc một danh mục. Trả `200` mảng `ActivePositionDto` `{portfolioId, symbol, quantity, averageCost,
  currentPrice, marketValue, unrealizedPnL, unrealizedPnLPercent, ...}`.

## <a id="watchlists"></a>Watchlists (CRUD đầy đủ)
- `GET /api/v1/ai/agent/watchlists` — danh sách watchlist (`WatchlistDto` `{id, name, emoji, isDefault, sortOrder, itemCount}`).
- `GET /api/v1/ai/agent/watchlists/{id}` — chi tiết + items (`WatchlistItemDto` `{symbol, note, targetBuyPrice?, targetSellPrice?, addedAt}`).
- `POST /api/v1/ai/agent/watchlists` body `{ "name", "emoji"?, "isDefault"?, "sortOrder"? }` → `201 { ...WatchlistDto }`.
- `PUT /api/v1/ai/agent/watchlists/{id}` body `{ "name", "emoji"?, "sortOrder"? }` → `204`.
- `DELETE /api/v1/ai/agent/watchlists/{id}` → `204` (soft delete).
- `POST /api/v1/ai/agent/watchlists/{id}/items` body `{ "symbol", "note"?, "targetBuyPrice"?, "targetSellPrice"? }` → `200` chi tiết mới.
- `PUT /api/v1/ai/agent/watchlists/{id}/items/{symbol}` body `{ "note"?, "targetBuyPrice"?, "targetSellPrice"? }` → `200`.
- `DELETE /api/v1/ai/agent/watchlists/{id}/items/{symbol}` → `200` chi tiết còn lại.
- `POST /api/v1/ai/agent/watchlists/import-vn30` body `{ "watchlistId"? }` — nạp rổ VN30 vào watchlist có sẵn hoặc tạo mới.

Ví dụ thêm mã theo dõi có giá mục tiêu:
```json
{ "symbol":"FPT", "note":"Cho vao khi ve vung ho tro", "targetBuyPrice":115, "targetSellPrice":140 }
```

## <a id="journal-entries"></a>Journal Entries (nhật ký theo mã/quyết định)
- `POST /api/v1/ai/agent/journal-entries` body (chính) `{ "symbol", "entryType"(Observation|PreTrade|DuringTrade|PostTrade|Review),
  "title", "content", "portfolioId"?, "tradeId"?, "tradePlanId"?, "emotionalState"?, "confidenceLevel"?(1-10),
  "priceAtTime"?, "marketContext"?, "tags"?[], "timestamp"? }` → `201 { "id" }`.
- `PUT /api/v1/ai/agent/journal-entries/{id}` body `{ "title"?, "content"?, "entryType"?, "emotionalState"?, "confidenceLevel"?, "marketContext"?, "tags"?[], "rating"? }` → `204`, không tồn tại → `404`.
- **`entryType = Decision` bị từ chối (400)** ở cả POST và PUT. Loại đó là cờ dập cảnh báo trong Hàng đợi quyết định, chỉ sinh từ hành động xử lý thật. Muốn ghi "hôm nay tôi giữ" thì dùng tool MCP `hold_decision` — nó đi qua luật lý do ≥ 20 ký tự.
- `DELETE /api/v1/ai/agent/journal-entries/{id}` → `204`, không tồn tại → `404`.
- `GET /api/v1/ai/agent/journal-entries/pending-review?portfolioId={id?}` — danh sách lệnh đã đóng còn chờ viết nhật ký.
- `GET /api/v1/ai/agent/journal-entries?symbol={mã}&from=&to=` — nhật ký của một mã (`symbol` **bắt buộc**, thiếu → `400`).

## <a id="journals"></a>Journals (nhật ký theo trade)
- `GET /api/v1/ai/agent/journals?portfolioId={id?}` — mảng `JournalDto`.
- `GET /api/v1/ai/agent/journals/trade/{tradeId}` — nhật ký của một trade; không có → `404`.
- `POST /api/v1/ai/agent/journals` body `{ "tradeId", "portfolioId", "entryReason"?, "marketContext"?, "technicalSetup"?, "emotionalState"?, "confidenceLevel"?(1-10), "tradePlanId"? }` → `201 { "id" }`.
- `PUT /api/v1/ai/agent/journals/{id}` body `{ "entryReason"?, "marketContext"?, "technicalSetup"?, "emotionalState"?, "confidenceLevel"?, "postTradeReview"?, "lessonsLearned"?, "rating"?, "tags"?[] }` → `204`.
- `DELETE /api/v1/ai/agent/journals/{id}` → `204` (soft delete).

## <a id="symbol-timeline"></a>Symbol timeline (sự kiện theo mã)
- `GET /api/v1/ai/agent/symbols/{symbol}/timeline?from=&to=` — dòng thời gian hợp nhất của một mã (nhật ký + trade + sự kiện + cảnh báo), trả `SymbolTimelineDto`.

## <a id="review-sizing-actions"></a>Review lại, định cỡ vị thế, sự kiện quyền (tool MCP, 2026-11-28)
- **`recompute_plan_review(planId)`** — GHI (`Destructive`). Tính lại số liệu review của kế hoạch ĐÃ review theo công thức hiện hành; bài học giữ nguyên, số cũ lưu một lần vào `reviewData.legacy`. Kế hoạch chưa review → lỗi tham số; không có hoặc của người khác → cùng một lỗi không tìm thấy. Chỉ gọi khi người dùng muốn; xem trước bằng `preview_plan_review`.
- **`calculate_position_size(accountBalance, entryPrice, stopLoss, …)`** — chỉ đọc, cùng bộ đầu vào với `POST /api/v1/risk/position-sizing`. Tuỳ chọn: `riskPercent` (2), `maxPositionPercent` (20), `atr`, `atrMultiplier` (2), `winRate` (0–1), `averageWin`, `averageLoss`, `atrPercent`, `useHistory`, `strategyId`. `useHistory=true`: Kelly lấy từ R của chính người dùng (cận dưới 95%), ghi đè số tự truyền, kết quả có khối `kellyFromHistory`; không đủ 20 lệnh hoặc không có lợi thế thì không có mô hình `kelly`, chỉ có khối đó kèm lý do.
- **`list_corporate_actions(symbol?, portfolioId?)`** — chỉ đọc. Sự kiện quyền (cổ tức tiền mặt, thưởng, chia tách) trên danh mục của người dùng; `portfolioId` lạ hoặc của người khác → cùng lỗi không tìm thấy.
- **`reviewData.realizedR` vắng mặt = không tính được** (bán vượt số mua đã gắn, hoặc review cũ thiếu 1R) — KHÔNG phải 0R. `oversoldQuantity` hoặc `openQuantity` lớn hơn 0 nghĩa là chiến dịch bị loại khỏi thống kê R.
