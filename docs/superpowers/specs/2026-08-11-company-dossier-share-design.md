# Chia sẻ hồ sơ công ty giữa các tài khoản, qua clipboard

**Ngày:** 2026-08-11 · **Trạng thái:** đã duyệt thiết kế, chưa thi hành

## 1. Vấn đề

Người dùng muốn gửi một hồ sơ công ty đã viết cho tài khoản khác (vợ/chồng, bạn cùng đầu tư) để người kia không phải gõ lại từ đầu.

**Chia sẻ giữa tài khoản trong app chưa tồn tại.** [`docs/plans/multi-user-access-plan.md`](../../plans/multi-user-access-plan.md) ghi rõ phần chia sẻ tài khoản (§1) chưa triển khai — mới chỉ có impersonation. Mọi entity còn gắn cứng `UserId` lọc từ JWT, không có bảng quyền nào.

Nhưng cơ chế cần thiết **gần như đã chạy**: PR #156 vừa thêm `dossier-clipboard.ts` với `buildAiPrompt()` / `parseAiPayload()` cho luồng "hỏi một AI khác". Chia sẻ người-với-người là **cùng một đường ống**, chỉ khác nội dung đi trong đó. Phần lớn việc là đổi nhãn và tách một payload gọn, không phải xây tính năng mới.

Cái đang thiếu:

| # | Sự thật | Bằng chứng |
|---|---|---|
| A1 | Bản sao chép hiện tại kèm prompt, số liệu 24hmoney và schema JSON — gửi cho người thì thừa và khó đọc | [`dossier-clipboard.ts` `buildAiPrompt()`](../../../frontend/src/app/features/company-dossier/dossier-clipboard.ts) |
| A2 | Payload không mang thông tin ai gửi, nên người nhận không có cách biết nội dung từ đâu ra | `ParsedDossier` chỉ có 4 field nội dung |
| A3 | Nút dán tên là "Dán từ AI" — sẽ nói sai khi nó nhận cả hồ sơ do người khác gửi | `company-dossier-detail.component.ts`, `data-testid="btn-open-paste"` |

## 2. Quyết định

| # | Quyết định | Vì sao |
|---|---|---|
| Q1 | **Chia sẻ qua clipboard, KHÔNG làm ACL server-side.** | Chia sẻ thật cần cả nền multi-user (bảng quyền, lời mời, thu hồi, phân quyền đọc/ghi) — đó là một plan riêng đã tồn tại và cố ý chưa làm. Dựng một nhánh quyền riêng chỉ cho hồ sơ là đẻ ra hệ thống quyền thứ hai phải giữ đồng bộ với hệ thống thật sau này. |
| Q2 | **Ghi nguồn vào ô Ghi chú**, không thêm field backend. | Để lại vệt đọc được mà không đụng schema. Người dùng xóa dòng đó lúc nào cũng được — chấp nhận, xem Q3. |
| Q3 | **Không ép ký lại, không đánh dấu ở backend** như `AgentDraftedAt`. | Cổng đo *chữ ký*, không đo *nguồn gốc chữ*. Người dùng vốn đã có thể mở hồ sơ người khác ra gõ tay lại từng chữ; một dấu ở backend chỉ chặn được người trung thực, không chặn được ai muốn lách, mà lại thêm state phải giữ đồng bộ ở mọi đường đọc. Chữ ký vẫn là lớp chịu trách nhiệm: ai ký người đó chịu. |
| Q4 | **Dùng lại đúng shape payload hiện có**, chỉ thêm `sharedBy` / `sharedAt`. | Một parser cho cả hai nguồn. Hai format cho cùng một việc là hai thứ phải giữ đồng bộ — cùng lý lẽ đã dùng khi nối với `upsert_company_dossier`. |
| Q5 | **Bốn nút phẳng ở header**, không gộp vào menu `Sao chép ▾`. | Menu giấu mất chính tính năng vừa làm, và "Sao chép cho AI" là việc thường xuyên còn chia sẻ là thỉnh thoảng — không đáng bắt cả hai cùng chịu thêm một cú bấm.<br>**Đây là quyết định người dùng chưa chốt.** Muốn đổi sang menu thì sửa duy nhất mục 3.3, phần còn lại không phụ thuộc. |
| Q6 | **`sharedBy` che sẵn, và người gửi sửa được trước khi chép.** | `AuthService` đặt `name = payload.name \|\| payload.email` ([auth.service.ts:82](../../../frontend/src/app/core/services/auth.service.ts#L82)), nên JWT thiếu `name` thì `name` CHÍNH LÀ email. Copy thẳng là phát tán địa chỉ email qua tin nhắn. Che mặc định lo ca người dùng bấm nhanh không để ý; ô sửa được lo ca họ muốn để tên thật, hoặc muốn giấu kỹ hơn nữa. Không chọn một trong hai vì mỗi cái hụt một nửa. |
| Q7 | **Hiện payload ra trước khi chép**, không chép thẳng vào clipboard. | Đây là nội dung sắp rời khỏi máy và sang tay người khác. Thấy trước cái gì đi ra là mức tối thiểu cho một hành động hướng ra ngoài. |

## 3. Phạm vi

### 3.1 Payload chia sẻ

Cùng shape với luồng dán hiện tại, thêm hai field ở cấp gốc:

```json
{
  "symbol": "EVF",
  "sharedBy": "Minh",
  "sharedAt": "2026-08-11",
  "businessModel": "…",
  "moats": [{ "description": "…" }],
  "riskFactors": [{ "rank": 1, "description": "…", "observableSignal": "…", "isDealBreaker": false, "suggestedTrigger": null }],
  "notes": "…"
}
```

Bọc trong khối đọc được — phần chữ cho người, khối JSON cho máy. Bộ đọc hiện tại lấy khối JSON cuối cùng nên không cần parser thứ hai:

```
Hồ sơ công ty EVF — Minh chia sẻ ngày 11/08/2026
Mở /company-dossier/EVF trong Investment Mate rồi bấm "Dán nội dung".

```json
{ … }
```
```

**Khác gì bản "Sao chép cho AI":** không có schema hướng dẫn, không có số liệu 24hmoney, không có câu yêu cầu soát lại. Người nhận cần nội dung hồ sơ, không cần nguyên liệu để một AI viết lại nó.

### 3.2 Nguồn của `sharedBy` (Q6)

**Giá trị gợi ý ban đầu** — lấy `AuthService.getCurrentUserValue()?.name`:

| `name` | Gợi ý | Quy tắc |
|---|---|---|
| `Minh Trần` | `Minh Trần` | Không chứa `@` ⇒ giữ nguyên |
| `minh.tran@gmail.com` | `min***@gmail.com` | Giữ 3 ký tự đầu phần local, thay phần còn lại bằng `***`, giữ `@domain` |
| `an@gmail.com` | `a***@gmail.com` | Local ngắn hơn 4 ⇒ giữ 1 ký tự |
| rỗng / null | *(trống)* | Ô để trống, không bịa |

Giữ `@domain` là cố ý: giữa những người quen thì `min***@gmail.com` đủ để nhận ra ai, mà không đưa ra địa chỉ gửi thư được.

**Người gửi sửa được.** Ô `Bạn hiện là` trong hộp thoại chia sẻ (3.3) điền sẵn giá trị gợi ý, sửa tự do — gõ tên thật, biệt danh, hay xoá trắng đều được. Giá trị cuối cùng nhớ trong `localStorage` để lần chia sẻ sau không phải gõ lại.

**Ô để trống ⇒ bỏ hẳn khoá `sharedBy` khỏi payload**, không điền `"(không rõ)"`: người nhận đọc "Nhận từ (không rõ)" không biết thêm gì so với không có dòng nào, mà lại tưởng hệ thống hỏng.

`sharedAt` là ngày local của người gửi, `YYYY-MM-DD` trong payload, hiển thị `DD/MM/YYYY`.

### 3.3 Header (Q5 — quyết định chưa chốt)

Bốn control, nhãn ngắn để không tràn:

| Nhãn | Việc |
|---|---|
| `Sao chép cho AI` | như hiện tại — hồ sơ + số liệu + schema, chép thẳng |
| `Chia sẻ` | mở hộp thoại ở 3.3.1 |
| `Dán nội dung` | **đổi từ `Dán từ AI`** — từ giờ nhận cả hai nguồn, giữ nhãn cũ là nói sai với người dùng |
| `Sửa` | như hiện tại, chỉ ở chế độ đọc |

Nút `Sao chép cho AI` giữ trạng thái `✓ Đã chép` 2 giây như hiện có.

#### 3.3.1 Hộp thoại Chia sẻ (Q7)

Overlay `z-[60]` (header sticky đang `z-50`), thứ tự nút `[Hủy] → [Sao chép]`:

- Ô **`Bạn hiện là`** — điền sẵn theo 3.2, sửa được. Chú thích một dòng: *"Tên này đi kèm nội dung để người nhận biết ai gửi. Để trống nếu không muốn ghi."*
- **Vùng xem trước** — nguyên văn nội dung sắp chép, chỉ đọc, cập nhật theo ô trên. Đây là chỗ người gửi thấy chính xác cái gì rời khỏi máy mình.
- Nút **`Sao chép`** ghi vào clipboard, hiện `✓ Đã chép`, đóng hộp thoại.

Không có nút gửi thẳng đi đâu cả — người dùng tự chọn kênh (Zalo, mail, giấy).

### 3.4 Khi dán

`ParsedDossier` thêm `sharedBy?: string` và `sharedAt?: string`. `parseAiPayload()` đọc chúng như mọi field khác: sai kiểu thì bỏ qua, không làm hỏng cả lần dán.

Component chèn **lên đầu ô Ghi chú**:

```
Nhận từ Minh ngày 11/08/2026.

<ghi chú trong payload, nếu có>
```

| Tình huống | Xử lý |
|---|---|
| Payload không có `sharedBy` (bản từ AI) | Không chèn gì — một parser, hai nguồn |
| Có `sharedBy`, không có `sharedAt` | Chèn `Nhận từ Minh.` (bỏ mệnh đề ngày) |
| Dán lại đúng payload đó lần nữa | Không chèn trùng: bỏ qua nếu ghi chú đã chứa nguyên văn dòng đó |
| `symbol` lệch mã đang mở | **Vẫn chặn cứng** như hiện tại |
| Người nhận sửa/xóa dòng nguồn | Được — Q3 đã chấp nhận |

Vẫn **không tự Lưu, không tự Ký**.

## 4. Không làm (YAGNI)

- Không ACL server-side, không lời mời, không thu hồi, không thông báo.
- Không đụng backend, DTO, entity, schema — thuần frontend.
- Không thêm MCP tool.
- Không mã hoá / ký số payload: đây là chia sẻ giữa người quen qua kênh họ tự chọn, không phải kênh phân phối công khai.
- Không tự Lưu, không tự Ký khi dán.
- Không nhúng số liệu 24hmoney vào payload chia sẻ — người nhận tự tra được, và số liệu chỉ đúng ở thời điểm gửi.

## 5. Test

`dossier-clipboard.spec.ts`:

| Ca | Kỳ vọng |
|---|---|
| `buildSharePayload` | Chứa mô hình kinh doanh, từng rủi ro kèm dấu hiệu, khối JSON parse được |
| `buildSharePayload` | **Không** chứa schema hướng dẫn, không chứa số liệu 24hmoney |
| Vòng tròn build → parse | Mọi field nội dung khớp bản gốc |
| `sharedBy` rỗng | Payload **không có khoá** `sharedBy` |

Hàm che email (thuần, tách riêng để test trực tiếp):

| Đầu vào | Kỳ vọng |
|---|---|
| `Minh Trần` | `Minh Trần` — không có `@`, giữ nguyên |
| `minh.tran@gmail.com` | `min***@gmail.com` |
| `an@gmail.com` | `a***@gmail.com` — local ngắn, giữ 1 ký tự |
| `a@b.com` | `a***@b.com` — local 1 ký tự, không được ra chuỗi rỗng |
| `investmate.support@gmail.com` | `inv***@gmail.com` — **không** chứa `support` |
| `''` / `null` / `undefined` | `''` |
| `@gmail.com` (local rỗng, dữ liệu hỏng) | `''` — không ném lỗi, không trả `***@gmail.com` |

Đọc `sharedBy` / `sharedAt` khi parse:

| Ca | Kỳ vọng |
|---|---|
| Payload có cả hai | Trả đúng hai field |
| Payload không có chúng | Hai field `undefined`, **không** phải chuỗi rỗng |
| `sharedBy` sai kiểu (số, object) | Bỏ qua, phần còn lại vẫn dán được |
| `sharedBy`/`sharedAt` trong payload | **Không** lọt vào body PUT khi Lưu |

`company-dossier-detail.component.spec.ts`:

| Ca | Kỳ vọng |
|---|---|
| Dán payload có `sharedBy` | Ghi chú bắt đầu bằng `Nhận từ Minh ngày 11/08/2026.` |
| Ghi chú gốc trong payload | Được giữ, nằm dưới dòng nguồn |
| Dán payload từ AI (không `sharedBy`) | Ghi chú **không** có dòng nguồn |
| Dán cùng payload hai lần | Chỉ một dòng nguồn |
| Có `sharedBy`, không `sharedAt` | `Nhận từ Minh.` — không có chữ "ngày" cụt |
| Sau khi dán chia sẻ | `mode === 'edit'`, **không** gọi PUT/POST, `canSign()` false |

Hộp thoại chia sẻ:

| Ca | Kỳ vọng |
|---|---|
| Mở hộp thoại | Ô `Bạn hiện là` điền sẵn giá trị đã che |
| Sửa ô rồi xem trước | Vùng xem trước đổi theo, chứa đúng giá trị vừa gõ |
| Xoá trắng ô | Xem trước **không** chứa khoá `sharedBy` |
| Sửa rồi chép, mở lại lần sau | Ô điền sẵn giá trị đã sửa lần trước, không quay về giá trị che |
| Ô chứa email đầy đủ do người dùng tự gõ | Tôn trọng, chép nguyên văn — họ đã chủ động chọn |

Môi trường: dự án **không có `zone.js/testing`** — không dùng `fakeAsync`. Test gọi `confirm()` phải `spyOn(window, 'confirm')`, nếu không hộp thoại thật treo headless Chrome và báo `DISCONNECTED` chứ không phải `FAILED`.

## 6. Tài liệu phải đồng bộ

| File | Nội dung |
|---|---|
| [`docs/features.md`](../../features.md) | Mô tả nút Chia sẻ + đổi tên nút dán |
| [`docs/business-domain.md`](../../business-domain.md) | Dòng 524 — trang chi tiết hồ sơ |
| [`docs/architecture.md`](../../architecture.md) | `dossier-clipboard.ts` thêm `buildSharePayload()` |
| [`docs/project-context.md`](../../project-context.md) | Quyết định: chia sẻ qua clipboard thay vì ACL, và vì sao không đánh dấu backend |
| [`frontend/src/assets/CHANGELOG.md`](../../../frontend/src/assets/CHANGELOG.md) | Mục release |
| `frontend/src/assets/docs/ho-so-cong-ty.md` | Mục hướng dẫn chia sẻ + nhận |

Không cần ADR: thuần frontend, không đổi schema, không đổi contract cross-layer, không đi ngược convention. Q3 (không đánh dấu ở backend) là quyết định **không** làm gì, nằm trong lý lẽ sẵn có của ADR-0011.
