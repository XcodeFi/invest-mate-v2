# ADR-0015 — Tập giá trị hợp lệ của tham số MCP sống trong inputSchema

**Ngày:** 2026-08-11
**Trạng thái:** Accepted

## Bảng thuật ngữ

| Viết tắt | Tên đầy đủ | Nghĩa ở đây |
|---|---|---|
| MCP | Model Context Protocol | Giao thức để AI agent gọi tool của backend |
| SDK | Software Development Kit | `ModelContextProtocol.AspNetCore` 2.0.0-rc.1 |
| DTO | Data Transfer Object | Lớp chở dữ liệu qua ranh giới |
| STJ | System.Text.Json | Bộ tuần tự JSON của .NET |
| `inputSchema` | — | Phần mô tả tham số của tool, agent nhận ở lời gọi `tools/list` |

## Bối cảnh

Một phiên làm việc cố ghi cây kịch bản qua `update_trade_plan` và thất bại lặp lại, rồi kết luận sai rằng
enum `ScenarioActionType` chỉ nhận `SellAll`. Thực tế enum có 7 giá trị và đã được liệt kê đủ trong
`src/InvestmentApp.Api/Docs/AI-Agent-TradePlan-API.md` từ trước.

Agent chỉ giao tiếp qua MCP. Nó không đọc file tài liệu đó. Thứ nó nhận được là `inputSchema`, và ở đó
`scenarioNodes` là một object trần: `actionType` khai `string`, không một chữ hướng dẫn. Đối chiếu: các
tham số phẳng như `direction`, `timeHorizon` có `[Description]` ghi rõ giá trị — và agent tuân đúng những
chỗ đó. Nó đoán ở đúng những chỗ không được cho biết.

## Quyết định

**Mọi tham số MCP nhận giá trị thuộc một tập hữu hạn thì tập đó phải nằm trong `inputSchema`.**

Cách hiện thực, theo thứ tự ưu tiên:

1. **Enum miền thật** — SDK tự sinh `"enum":[…]`, ràng buộc máy đọc được. Đây là dạng mạnh, dùng mặc định.
2. **`[Description]` liệt kê giá trị** — cho field không có enum miền tương ứng (`direction`, `marketCondition`,
   `status` của `set_trade_plan_status`: `string` thuần trên `TradePlan`).

Kèm hai luật phụ:

- Field mang **quyết định** (`actionType`, `conditionType`) khai nullable + validator `NotNull`. Field mang
  **đơn vị đo** (`method`) được phép có mặc định. Hành động sai âm thầm tệ hơn một lỗi.
- Tham số MCP tùy chọn phải khai `= null`, nếu không SDK đẩy nó vào `"required"`.

Một test quét (`McpFiniteValueSweepTests`) bắt buộc luật này trên toàn bộ tool, để tool viết sau không tuột
ra ngoài vì người viết quên.

## Lựa chọn đã cân nhắc

**`AllowedValuesAttribute`.** Đã kiểm tra tài liệu SDK: nó chỉ đưa giá trị ra dưới dạng *completions* cho
`completion/complete`. Agent đọc `tools/list` rồi gọi luôn sẽ **không bao giờ** thấy. Nên nó **không** thay
được enum thật. Vẫn giữ trên các tham số `string` vì miễn phí và có lợi cho client tương tác — nhưng thứ
thực sự tới được agent ở những field đó là `[Description]`.
*Đây là sự thật về SDK 2.0.0-rc.1. Nếu bản sau sinh ràng buộc `enum` từ attribute này thì ADR phải sửa.*

**Thêm tool MCP để agent tra tài liệu.** Bị loại: hướng dẫn phải nằm trong schema agent đã có sẵn, không
phải sau một lời gọi nữa. Một tool tra cứu chỉ dịch chuyển vấn đề, không giải quyết.

**Giữ `string` và chỉ cải thiện message lỗi.** Bị loại: sửa được chỗ "sai rồi mới biết", không sửa được chỗ
"đúng ngay lần đầu". Mất luôn ràng buộc máy đọc được.

## Hệ quả

Tương thích ngược với frontend: `JsonStringEnumConverter` đã đăng ký ở `ApiJsonConfig`, enum ra vào đều là
chuỗi, kiểu TypeScript khai `string` không đổi.

Đánh đổi đã đo được: siết kiểu đẩy lỗi giá trị sai lên **sớm hơn** — vỡ ở bước SDK marshal tham số, trước
khi thân tool chạy. Một helper bọc thân tool không nằm trên đường đi của ca lỗi đó. Vì vậy lớp dịch lỗi
phải là **filter cấp server** (`McpServerOptions.Filters.Request.CallToolFilters`, xem `McpErrorTranslator`),
và nó đọc tên enum từ message của STJ rồi nối tập giá trị vào — biến một lời từ chối thành một chỉ dẫn.
