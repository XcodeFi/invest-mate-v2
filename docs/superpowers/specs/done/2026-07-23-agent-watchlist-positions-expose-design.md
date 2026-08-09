# Thiết kế: Expose read/write endpoints lên AI-agent surface

*(watchlist · positions · journal-entries · journals · symbol timeline)*

**Ngày:** 2026-07-23
**Trạng thái:** Đang brainstorm — chờ duyệt lại spec (mở rộng) → writing-plans
**Phạm vi:** `InvestmentApp.Api` (controllers + agent doc + tests). Không đụng Application/Domain/frontend.
**Liên quan:** ADR-0003 (per-user API keys), ADR-0004 (agent write surface). Đây là mảng (a) đã decompose; mảng (b) MCP server là spec riêng sau.

## 1. Mục tiêu

AI-agent surface (`/api/v1/ai/agent`, scheme ApiKey) hiện chỉ expose trade-plans + trades + doc. Stock agent (NPU/Claude) muốn đọc **danh mục thật**, quản **watchlist**, **ghi/đọc nhật ký**, và **xem sự kiện theo mã** thì phải đi vòng qua gói `daily-digest`. Mục tiêu: expose thêm 5 nhóm endpoint có sẵn lên agent surface để agent thao tác trực tiếp như tool:

1. **Positions** (read) — đọc holdings thật.
2. **Watchlist** (full CRUD).
3. **JournalEntries** (nhật ký theo mã/quyết định — full).
4. **Journals** (nhật ký theo trade — full).
5. **Symbol timeline** (read) — sự kiện của một mã.

## 2. Bối cảnh đã điều tra

- `AiAgentController` (`api/v1/ai/agent`, `[Authorize(ApiKey)]`): re-dispatch MediatR command/query có sẵn, set `UserId = GetUserId()` (claim `"sub"`), không business logic. Precedent: một controller = một scheme (nhiều `[Authorize]` scheme cộng dồn AND; fail auth có thể redirect 302 Google → coi như chưa xác thực).
- Các controller JWT nguồn — `WatchlistsController`, `PositionsController`, `JournalEntriesController`, `JournalsController`, `SymbolTimelineController` — đều dùng **`GetUserId()` giống hệt** (`"sub"`). → mirror gần như copy body action, chỉ đổi scheme + route prefix.
- MediatR types tái dùng nguyên (không handler/DTO/logic mới):
  - Positions: `GetActivePositionsQuery`
  - Watchlist: `GetWatchlistsQuery`, `GetWatchlistDetailQuery`, `CreateWatchlistCommand`, `UpdateWatchlistCommand`, `DeleteWatchlistCommand`, `AddWatchlistItemCommand`, `UpdateWatchlistItemCommand`, `RemoveWatchlistItemCommand`, `ImportVn30Command`
  - JournalEntries: `CreateJournalEntryCommand`, `UpdateJournalEntryCommand`, `DeleteJournalEntryCommand`, `GetTradesPendingReviewQuery`, `GetJournalEntriesBySymbolQuery`
  - Journals: `GetJournalsQuery`, `GetJournalByTradeQuery`, `CreateJournalCommand`, `UpdateJournalCommand`, `DeleteJournalCommand`
  - Symbol timeline: `GetSymbolTimelineQuery`
- Data shape (giữ nguyên DTO nguồn): `ActivePositionDto`, `WatchlistItem{Symbol,TargetBuyPrice?,TargetSellPrice?,note}`, `JournalEntryDto`, `PendingReviewTradeDto`, `JournalDto`, và DTO của symbol timeline.

## 3. Cách tiếp cận (đã chọn: B)

**B — controller anh em, focused.** Mỗi domain một controller nhỏ, cùng prefix `/api/v1/ai/agent` + `[Authorize(ApiKey)]`, kế thừa base `AiAgentControllerBase` (giữ `IMediator` + `GetUserId()`). Không retrofit `AiAgentController` hiện tại (không đụng code đang chạy — ngoài phạm vi).

Controller mới:
- `AiAgentPositionsController`
- `AiAgentWatchlistsController`
- `AiAgentJournalEntriesController`
- `AiAgentJournalsController`
- `AiAgentSymbolsController` (symbol timeline; đặt tên số nhiều để chừa chỗ mở rộng route theo mã sau)

Loại: **A** (nhét hết vào `AiAgentController` → ~28 route/1 file, phình); **C** (proxy generic → YAGNI, MediatR đã là lớp chung).

## 4. Route mới (21)

Tất cả `[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]`, dưới `/api/v1/ai/agent`. Response code/body giữ như controller nguồn (Ok / Created / NoContent / 404). `POST` trỏ `Created` Location về **agent surface** (`/api/v1/ai/agent/...`) để agent theo link bằng chính api-key — theo precedent `CreatePlan`.

### 4.1 Positions — `AiAgentPositionsController` (read)
| Verb | Route | MediatR |
|---|---|---|
| GET | `/positions?portfolioId=` | `GetActivePositionsQuery { UserId, PortfolioId? }` |

### 4.2 Watchlist — `AiAgentWatchlistsController` (full CRUD, 9)
| Verb | Route | MediatR |
|---|---|---|
| GET | `/watchlists` | `GetWatchlistsQuery` |
| GET | `/watchlists/{id}` | `GetWatchlistDetailQuery` |
| POST | `/watchlists` | `CreateWatchlistCommand` |
| PUT | `/watchlists/{id}` | `UpdateWatchlistCommand` |
| DELETE | `/watchlists/{id}` | `DeleteWatchlistCommand` |
| POST | `/watchlists/{id}/items` | `AddWatchlistItemCommand` (set `WatchlistId=id`) |
| PUT | `/watchlists/{id}/items/{symbol}` | `UpdateWatchlistItemCommand` (set `WatchlistId=id`, `Symbol=symbol`) |
| DELETE | `/watchlists/{id}/items/{symbol}` | `RemoveWatchlistItemCommand` |
| POST | `/watchlists/import-vn30` | `ImportVn30Command` |

### 4.3 JournalEntries — `AiAgentJournalEntriesController` (5)
| Verb | Route | MediatR | Ghi chú |
|---|---|---|---|
| POST | `/journal-entries` | `CreateJournalEntryCommand` | Created → id |
| PUT | `/journal-entries/{id}` | `UpdateJournalEntryCommand` | `false` → 404 |
| DELETE | `/journal-entries/{id}` | `DeleteJournalEntryCommand` | `false` → 404 |
| GET | `/journal-entries/pending-review?portfolioId=` | `GetTradesPendingReviewQuery` | list lệnh chờ viết nhật ký |
| GET | `/journal-entries?symbol=&from=&to=` | `GetJournalEntriesBySymbolQuery` | `symbol` bắt buộc (thiếu → 400) |

### 4.4 Journals — `AiAgentJournalsController` (5)
| Verb | Route | MediatR | Ghi chú |
|---|---|---|---|
| GET | `/journals?portfolioId=` | `GetJournalsQuery` | |
| GET | `/journals/trade/{tradeId}` | `GetJournalByTradeQuery` | null → 404 |
| POST | `/journals` | `CreateJournalCommand` | Created → id |
| PUT | `/journals/{id}` | `UpdateJournalCommand` | |
| DELETE | `/journals/{id}` | `DeleteJournalCommand` | soft delete |

### 4.5 Symbol timeline — `AiAgentSymbolsController` (read)
| Verb | Route | MediatR |
|---|---|---|
| GET | `/symbols/{symbol}/timeline` | `GetSymbolTimelineQuery { UserId, Symbol, ...filter như controller nguồn }` |

## 5. Pattern mỗi action

```
UserId = GetUserId();   // claim "sub", do ApiKey scheme gắn
=> _mediator.Send(<command/query có sẵn>)
```
Không adapter/guard đặc biệt (khác trade-plans nơi ép Draft/chặn restore). Watchlist/journal/symbol không có ràng buộc trạng thái → mirror thẳng, giữ nguyên mã lỗi (400 khi thiếu `symbol`, 404 khi update/delete miss).

## 6. Ownership / bảo mật

Mọi query/command đã filter theo `UserId` ở tầng handler → api-key chỉ thấy dữ liệu của chủ key. Không phát sinh đường transitive mới (watchlist/journal/journal-entry/timeline đều khóa bằng UserId, kể cả tra theo `{id}`/`tradeId`/`symbol`). Không nới quyền. Fail auth → như surface hiện tại.

## 7. Doc agent (`GET /ai/agent/doc`)

Thêm vào tài liệu authoritative:
- Mục lục: **Đọc danh mục → Positions**, **Watchlist → CRUD**, **Nhật ký theo mã → JournalEntries**, **Nhật ký theo trade → Journals**, **Sự kiện theo mã → Symbol timeline**.
- Mỗi nhóm: route + shape DTO + ví dụ tối thiểu. Đánh dấu read chạy ngay; watchlist/journal write là **low-stakes** (agent có thể chạy ngay, không bắt buộc gate "chốt" như trade-plan/trade — hành vi thuộc persona NPU, không phải backend).
- Doc version tự bump theo informational version assembly mỗi deploy → NPU re-fetch qua ETag/304.

## 8. Test (`InvestmentApp.Api` test layer)

Handler đã có test ở Application layer → controller test chỉ verify wiring + auth + inject UserId:
1. Thiếu api-key → 401/302 (mọi controller mới).
2. Ownership: api-key user A không truy cập được dữ liệu user B (watchlist, positions, journal-entries, journals, timeline).
3. Watchlist CRUD happy-path qua agent surface.
4. JournalEntries: create → update → delete; `pending-review` + `by-symbol` (thiếu symbol → 400).
5. Journals: list + by-trade (404 khi miss) + create/update/delete.
6. `GET /positions` và `GET /symbols/{symbol}/timeline` trả đúng shape (mock handler).
7. `GET /ai/agent/doc` chứa đủ 5 mục mới (guard chống quên cập nhật doc).

## 9. Ngoài phạm vi (YAGNI)

- **MCP server** bọc `/ai/agent/*` — mảng (b), spec riêng.
- Handler / command / DTO / business logic mới — tái dùng 100%.
- Thay đổi frontend hoặc các controller JWT nguồn.
- Đặt lệnh sàn thật (app là journal).
- Cập nhật persona `/stock` bên NPU — việc nhỏ phía npu-assistant, làm sau khi backend deploy (doc tự cập nhật để agent biết endpoint mới).

## 10. Tiêu chí thành công

1. Agent (X-Api-Key) `GET /ai/agent/positions` → holdings thật (qty, avgCost, P/L), không cần qua digest.
2. Agent dùng đủ 9 route watchlist + 5 journal-entries + 5 journals + timeline; tạo/sửa/xóa chạy đúng, chỉ trên dữ liệu chủ key.
3. `GET /ai/agent/doc` liệt kê đủ 5 nhóm mới; NPU re-fetch thấy mục mới.
4. Test ownership pass — không route nào rò dữ liệu người dùng khác.
5. Không thêm business logic mới; diff gọn trong `InvestmentApp.Api` + doc + test.

## 11. Ghi chú phạm vi

Scope (a) đã mở từ 10 → **21 route / 5 controller mới**. Vẫn là wiring mỏng (không logic mới) nên rủi ro thấp, nhưng khối lượng test + doc gần gấp đôi. Cân với trần dev ≤8h/tuần: nếu cần, có thể chia thực thi thành 2 lát (lát 1: positions + watchlist + symbol timeline; lát 2: journal-entries + journals) — quyết ở bước writing-plans.
