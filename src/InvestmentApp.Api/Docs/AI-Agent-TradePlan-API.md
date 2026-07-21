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
