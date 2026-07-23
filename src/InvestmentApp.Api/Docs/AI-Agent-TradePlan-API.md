# Invest Mate — AI Agent API (Trade Plan)

Tài liệu authoritative cho Claude. Base URL và `X-Api-Key` do NPU cấp lúc gọi (không có ở đây).
Trước MỌI ghi: tóm tắt một dòng, chờ người dùng "chốt" trong chat, rồi mới gọi.
App là nhật ký/tracker — KHÔNG đặt lệnh sàn, KHÔNG tiền thật, dữ liệu sửa được.

## Mục lục (ý định → mục)
- Lập plan → [Create Plan](#create-plan)
- Sửa plan → [Update Plan](#update-plan)
- Chuyển trạng thái → [Status](#status)
- Ghi trade / thực hiện → [Create Trade](#create-trade) + [Full-execute](#full-execute)
- Đọc plan → [Read](#read)
- Đọc danh mục → [Positions](#positions)
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
Enums scenario: `conditionType ∈ {PriceAbove,PriceBelow,PricePercentChange,TrailingStopHit,TimeElapsed}`;
`actionType ∈ {SellPercent,SellAll,MoveStopLoss,MoveStopToBreakeven,ActivateTrailingStop,AddPosition,SendNotification}`;
`trailingStopConfig.method ∈ {Percentage,ATR,FixedAmount}`.

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
`PATCH /api/v1/ai/agent/trade-plans/{id}/status` body `{ "status": "ready|inprogress|executed|cancelled", "tradeId?": "..." }`.
`restore` bị chặn (400). `executed` cần `tradeId` (tạo trade trước).

## <a id="create-trade"></a>Create Trade
`POST /api/v1/ai/agent/trades` body `{ "portfolioId","symbol","tradeType":"BUY|SELL","quantity","price","fee","tax","tradeDate?" }`.
Trả về `201 { "id": "<tradeId>" }`. Portfolio phải thuộc bạn.

## <a id="full-execute"></a>Full-execute (thực hiện)
1. `POST trade-plans` → planId. 2. `POST trades` → tradeId. 3. `PATCH trade-plans/{planId}/status {status:"executed", tradeId}`.
Nếu bước 3 lỗi sau khi đã tạo trade: báo rõ `planId` + `tradeId` để dọn tay.

---

Các nhóm dưới đây thao tác trực tiếp trên dữ liệu thật của chủ khóa. Read chạy ngay. Watchlist/journal
là **low-stakes** — agent được ghi/sửa/xóa mà không bắt buộc gate "chốt" như trade plan/trade (hành vi
thuộc persona NPU, không phải backend). Mọi route scope theo `sub` = chủ khóa; không thấy dữ liệu người khác.

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
- `POST /api/v1/ai/agent/journal-entries` body (chính) `{ "symbol", "entryType"(Observation|PreTrade|DuringTrade|PostTrade|Review|Decision),
  "title", "content", "portfolioId"?, "tradeId"?, "tradePlanId"?, "emotionalState"?, "confidenceLevel"?(1-10),
  "priceAtTime"?, "marketContext"?, "tags"?[], "timestamp"? }` → `201 { "id" }`.
- `PUT /api/v1/ai/agent/journal-entries/{id}` body `{ "title"?, "content"?, "entryType"?, "emotionalState"?, "confidenceLevel"?, "marketContext"?, "tags"?[], "rating"? }` → `204`, không tồn tại → `404`.
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
