# ADR-0008 — MCP tools nhận tham số phẳng, không nhận MediatR command

- **Status:** Accepted
- **Date:** 2026-07-28
- **Related plan:** —
- **Affected layers:** Api (MCP surface)

## Context

`create_journal_entry` thất bại 5/5 lần khi NPU/Claude agent gọi để ghi nhật ký thị trường, trong khi `list_journals` chạy bình thường — dẫn tới kết luận sai là "server trục trặc".

Nguyên nhân gốc: 10 write tool nhận trực tiếp MediatR command làm tham số (`CreateJournalEntryCommand command, IMediator mediator, …`). MCP SDK sinh schema **bọc** tham số đó thành một object lồng:

```json
{"type":"object","properties":{"command":{"properties":{"symbol":…}}},"required":["command"]}
```

Caller gửi args phẳng (`{"symbol":"HHV","entryType":"Observation",…}`) nhận `ArgumentException: The arguments dictionary is missing a value for the required parameter 'command'` — lỗi giống nhau bất kể sửa field nào, nên trông như lỗi server. Các tool phẳng như `create_trade` / `list_journals` không bị.

Hai yếu tố khiến bug sống sót tới production: (1) unit test gọi **trực tiếp** static method nên bỏ qua hoàn toàn tầng binder của SDK; (2) `McpToolDiscoveryTests` chỉ assert schema của các tool đã phẳng. Ngoài ra object lồng không mang `[Description]` nào, nên agent cũng không có gợi ý về cách bọc.

## Options Considered

### Option A — Giữ command object, bổ sung description + tài liệu về việc bọc `command`

- **Pros:**
  - Sửa rất ít code; signature ngắn.
  - Không đổi contract của tool đang chạy được (nếu caller đã bọc đúng).
- **Cons:**
  - Vẫn không đồng nhất: cùng một surface có tool phẳng, có tool lồng — agent phải đoán.
  - Phụ thuộc vào việc host đọc và tôn trọng schema lồng; client thực tế của dự án đã chứng minh là không.
  - `update_*` còn tệ hơn: `id` nằm ngoài nhưng `Id` cũng có trong command → hai nguồn sự thật.
  - Rò rỉ chi tiết nội bộ (tên class MediatR) ra contract công khai.

### Option B — Làm phẳng toàn bộ tham số của cả 10 tool

- **Pros:**
  - Đồng nhất với `create_trade` — mọi tool cùng một hình dạng, không có ngoại lệ để agent đoán sai.
  - Mỗi field có `[Description]` tiếng Việt + quy ước "(bỏ trống = …)", tự tài liệu hóa qua `tools/list`.
  - Optional params đặt sau `ct` với `= null` → rơi khỏi `required`, agent gửi đúng phần nó biết.
  - Chặn được cả lớp lỗi này bằng một invariant test duy nhất.
  - `Status`/`TradeId` của `create_trade_plan` không còn nằm trong tham số → ràng buộc Draft (ADR-0004) thành bất biến ở mức chữ ký, không còn là dòng gán chạy sau.
- **Cons:**
  - Signature dài (`create_trade_plan` 24 tham số).
  - Thêm một tầng mapping tay từ param → command; field mới phải sửa 2 nơi.
  - Là breaking change với caller nào đang bọc `command` đúng cách.

### Option C — Giữ command object nhưng viết custom schema generator để trải phẳng

- **Pros:** Giữ signature ngắn mà schema công khai vẫn phẳng.
- **Cons:** Phải bảo trì code chống lại SDK (đang ở `2.0.0-rc.1`, API còn đổi); binder và schema dễ lệch nhau; quá nhiều máy móc cho 10 tool.

## Decision

**Chọn Option B.**

Đây là surface dành cho AI agent nên khả năng khám phá quan trọng hơn độ ngắn của chữ ký: mỗi field phẳng có mô tả tiếng Việt là điều agent đọc được, còn một object `command` rỗng mô tả thì không. Đổi lại chữ ký dài và mapping tay, ta thu được một invariant kiểm chứng được bằng test (`không tool nào có property tên `command``) chặn toàn bộ lớp lỗi này về sau. Breaking change được chấp nhận vì consumer duy nhất hiện nay (NPU agent) chưa gọi thành công các tool này.

## Consequences

**Positive:**

- 10 tool nhận args phẳng: `create_journal_entry`, `update_journal_entry`, `create_journal`, `update_journal`, `create_watchlist`, `update_watchlist`, `add_watchlist_item`, `update_watchlist_item`, `import_vn30`, `create_trade_plan`, `update_trade_plan`.
- `required` chỉ còn field thật sự bắt buộc (vd `create_journal_entry` → `symbol`, `entryType`, `title`, `content`).
- `create_trade_plan` không thể nhận `Status`/`TradeId` từ agent nữa — luôn Draft (siết ADR-0004).

**Negative / Trade-offs:**

- Thêm field vào command phải sửa cả tool signature, nếu quên thì field đó âm thầm không dùng được qua MCP. Test `required`-array giảm nhẹ chứ không chặn hết.
- `create_trade_plan`/`update_trade_plan` có chữ ký dài, đọc hơi nặng.
- Alias `reason` (shim `[Obsolete]` cho client cũ, map sang `Thesis`) cố tình không mở ra MCP — agent phải dùng `thesis`. REST surface không đổi.

**Follow-ups:**

- Tests đã thêm: `McpToolArgumentBindingTests` (gọi qua SDK binder với args phẳng — direct-call test không phát hiện được lớp lỗi này) + `McpToolDiscoveryTests.No_Tool_Wraps_Its_Args_In_A_Command_Object` + assert `required` cho từng write tool.
- Cần deploy để NPU agent dùng được — schema chỉ đổi khi API mới lên.

## References

- PR: (fill in after merge)
- Liên quan: ADR-0004 (agent write surface), ADR-0003 (per-user API keys)
