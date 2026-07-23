# Thiết kế: Agent tự đủ thông tin khi mở/đóng vị thế (portfolio + fee/tax)

**Ngày:** 2026-07-23
**Trạng thái:** Đã duyệt design → chờ review spec → writing-plans
**Phạm vi:** `InvestmentApp.Api` (agent controllers + agent doc + tests). Không đụng Application/Domain/frontend.
**Liên quan:** ADR-0003 (per-user API keys), ADR-0004 (agent write surface); spec anh em `2026-07-23-agent-watchlist-positions-expose-design.md` (cùng pattern mirror lên agent surface).

## 1. Vấn đề

NPU agent (Claude/`/stock` persona) khi execute một trade plan — ghi trade để mở/đóng vị thế qua `POST /api/v1/ai/agent/trades` — thiếu 2 mảnh thông tin nên phải hỏi lại người dùng ở mỗi lần:

1. **`portfolioId`** — `CreateTradeCommand.PortfolioId` là **bắt buộc**, nhưng agent surface **không có** cách lấy danh mục (chỉ nhận `portfolioId` như query param ở positions/journals; không có `GET /portfolios`). `TradePlan.PortfolioId` lại **nullable** nên plan có thể không mang portfolio → agent bí.
2. **`fee`/`tax`** — `CreateTradeCommand` để `fee`/`tax` mặc định `0`. App đã có engine tính phí (`FeeCalculationService`, `POST /api/v1/fees/calculate`) nhưng **chưa expose lên agent surface** → agent bỏ trống (sai) hoặc tự đoán 0.1%.

Cả hai chữa bằng cùng pattern "expose năng lực có sẵn lên agent surface".

## 2. Bối cảnh đã điều tra

- **Agent surface:** controllers `AiAgent*Controller` dưới `/api/v1/ai/agent`, `[Authorize(ApiKey)]`, kế thừa `AiAgentControllerBase` (giữ `IMediator` + `GetUserId()` = claim `sub`). `AiAgentTradesController.CreateTrade` set `Origin = "AI_AGENT"`, `UserId` từ key; `CreateTradeCommand` handler đã validate `portfolio.UserId == request.UserId`.
- **Portfolio:** `GetAllPortfoliosQuery` có sẵn (JWT surface), chưa mirror lên agent. Không tồn tại khái niệm "default portfolio".
- **Fee engine:** `FeesController.CalculateFees` (`POST /api/v1/fees/calculate`) nhận `{symbol, tradeType, quantity, price}` → `{transactionFee, tax, vat, totalFees, breakdown}`. Logic ở `FeeCalculationService.GetFeesSummary` (fee tiered 0.075%–0.35%, tax PIT 0.1% chỉ khi SELL cổ phiếu, VAT 10% trên phí áp dụng). Config từ `IFeeConfiguration` — **global, không per-user**.
  - ⚠️ `FeesController` đang **tắt `[Authorize]`** ("temporarily disabled for testing"); `GET /config` là `AllowAnonymous`. Endpoint fee hiện công khai không auth.

## 3. Quyết định (từ brainstorm)

- Nơi fix: **backend agent surface** (không sửa phía NPU client trong spec này).
- Portfolio: user **thường có 1 danh mục** → **auto-pick khi đúng 1**.
- Fee/tax: **cả hai** — endpoint tính phí riêng để preview **và** create-trade tự tính khi bỏ trống.
- Kiến trúc: logic auto-resolve/auto-compute đặt ở **agent controller layer**, **không** nhét vào `CreateTradeCommand` chung → JWT path không đổi, new logic khu trú trên agent surface, dễ test. (Loại phương án thêm flag vào command chung — invasive hơn.)

## 4. Route mới (2) + enhance create-trade

Tất cả `[Authorize(ApiKeyAuthenticationDefaults.Scheme)]`, dưới `/api/v1/ai/agent`.

### 4.1 Portfolios — `AiAgentPortfoliosController` (read)
| Verb | Route | MediatR |
|---|---|---|
| GET | `/portfolios` | `GetAllPortfoliosQuery { UserId }` (mirror, giữ nguyên DTO nguồn) |

### 4.2 Fees — `AiAgentFeesController` (calc)
| Verb | Route | Nguồn |
|---|---|---|
| POST | `/fees/calculate` | mirror `FeesController.CalculateFees` — inject `IFeeCalculationService`, ApiKey scheme. Request `{symbol, tradeType, quantity, price}` → response `{transactionFee, tax, vat, totalFees, breakdown}` (giữ shape nguồn) |

### 4.3 Enhance `AiAgentTradesController.CreateTrade` (`POST /ai/agent/trades`)

Agent request DTO cho create-trade nới lỏng 3 field (chỉ trên agent surface):

- **`portfolioId` → optional.** Resolve trước khi dispatch `CreateTradeCommand`:
  - Có giá trị → dùng nguyên (agent tự đọc plan trước, có `PortfolioId` thì truyền vào).
  - Bỏ trống + user có **đúng 1** portfolio (qua `GetAllPortfoliosQuery`) → auto-pick portfolio đó.
  - Bỏ trống + **0** portfolio → `400` "chưa có danh mục, tạo trước".
  - Bỏ trống + **>1** portfolio → `400` kèm danh sách `{id, name}` để agent/người dùng chọn.
- **`fee` → nullable, `tax` → nullable.** Resolve trước khi dispatch:
  - `null` → backend tự tính qua `FeeCalculationService.GetFeesSummary` (đúng chiều BUY/SELL) và điền vào command.
  - Có giá trị (kể cả `0`) → tôn trọng giá trị agent gửi.

`CreateTradeCommand` (Application) **giữ nguyên** `PortfolioId: string` bắt buộc, `Fee/Tax: decimal` — agent controller đã điền đủ trước khi `Send`. JWT surface không đổi.

## 5. Pattern mỗi action

```
UserId = GetUserId();               // claim "sub"
GET /portfolios     => _mediator.Send(new GetAllPortfoliosQuery { UserId })
POST /fees/calculate => _feeService.GetFeesSummary(...) (+ VAT, tax) — mirror FeesController
POST /trades        => resolve portfolioId + fee/tax (nếu thiếu) => _mediator.Send(CreateTradeCommand đã điền đủ)
```

Precedence `plan.PortfolioId` là do **agent điều phối** (đọc plan → có thì truyền vào), backend create-trade không cần biết plan → giữ tách bạch.

## 6. Ownership / bảo mật

- `GET /portfolios` filter theo `UserId` → chỉ danh mục của chủ key.
- Auto-pick chỉ chọn trong portfolio của chính user (list đã filter UserId) → không mở đường transitive mới; handler `CreateTradeCommand` vẫn re-validate `portfolio.UserId == UserId` (lớp cuối).
- Fee calc là toán thuần trên biểu phí global (không per-user) → ApiKey scheme cho nhất quán, không lộ dữ liệu.
- Fail auth → như surface hiện tại (401/302).

## 7. Doc agent (`GET /ai/agent/doc`)

Thêm vào tài liệu authoritative:
- **Đọc danh mục → `GET /portfolios`** (để lấy `portfolioId`).
- **Tính phí → `POST /fees/calculate`** (preview fee/tax trước khi ghi).
- Ghi chú create-trade: `portfolioId` có thể bỏ trống (auto-pick khi 1 danh mục); `fee`/`tax` bỏ trống → tự tính. Nêu rõ khi >1 danh mục phải truyền `portfolioId`.
- Doc version bump theo informational version assembly mỗi deploy → NPU re-fetch qua ETag/304.

## 8. Test (`InvestmentApp.Api` test layer)

1. Thiếu api-key → 401/302 (mọi route mới).
2. `GET /portfolios`: ownership — user A không thấy portfolio user B; shape đúng DTO nguồn.
3. Create-trade portfolio resolve:
   - (a) truyền `portfolioId` → dùng đúng;
   - (b) bỏ trống + 1 portfolio → auto-pick portfolio đó;
   - (c) bỏ trống + >1 → `400` kèm list;
   - (d) bỏ trống + 0 → `400`.
4. Create-trade fee/tax resolve:
   - `fee`/`tax` null → auto-tính khớp `GetFeesSummary` (BUY: tax=0; SELL: tax=0.1%);
   - `fee`/`tax` = 0 explicit → giữ 0 (không auto-tính).
5. `POST /fees/calculate`: trả đúng shape; tax=0 khi BUY, 0.1% khi SELL; amount ≤ 0 → 400 (mirror nguồn).
6. `GET /ai/agent/doc` chứa mục `portfolios` + `fees/calculate` + ghi chú create-trade (guard chống quên cập nhật doc).

## 9. Ngoài phạm vi (YAGNI)

- Không ép `TradePlan.PortfolioId` bắt buộc — đã chọn resolve ở execution, không đổi plan creation.
- Không validate portfolio có đang giữ mã khi SELL — behavior `CreateTradeCommand` hiện tại giữ nguyên.
- Không bật lại `[Authorize]` cho `FeesController` gốc trong spec này (flag để follow-up riêng — không phá behavior FE đang phụ thuộc).
- Không đổi frontend, không MCP server, không đặt lệnh sàn thật.
- Cập nhật persona `/stock` phía npu-assistant — làm sau khi backend deploy (doc tự cập nhật để agent biết endpoint mới).

## 10. Tiêu chí thành công

1. Agent `GET /ai/agent/portfolios` → danh mục của chủ key với `id, name`.
2. Agent `POST /ai/agent/fees/calculate` → fee/tax/vat đúng cho một giao dịch dự kiến.
3. Agent `POST /ai/agent/trades` **không cần** truyền `portfolioId`/`fee`/`tax` khi user có 1 danh mục → trade ghi đúng danh mục, phí/thuế tự tính đúng. Khi >1 danh mục → 400 rõ ràng.
4. Test ownership + resolve pass; không route nào rò dữ liệu người khác.
5. Không thêm business logic mới ở Domain/Application; diff gọn trong `InvestmentApp.Api` + doc + test.
