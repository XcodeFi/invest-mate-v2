# Hồ sơ công ty — chế độ xem, form dễ gõ, và cầu nối clipboard với AI ngoài

**Ngày:** 2026-08-10 · **Trạng thái:** chờ duyệt spec, chưa thi hành

## 1. Vấn đề

Trang `/company-dossier/:symbol` hiện chỉ có **một chế độ: form đang sửa**. Mở mã đã ký xong để đọc lại trước khi vào lệnh cũng rơi thẳng vào textarea, input, select và các nút ✕ đỏ.

Hai nhóm vấn đề tách bạch:

**Nhóm A — không có chỗ để đọc.**

| # | Sự thật | Bằng chứng |
|---|---|---|
| A1 | Không tồn tại chế độ chỉ đọc; mọi trường luôn là control nhập | [company-dossier-detail.component.ts:66-135](../../../frontend/src/app/features/company-dossier/company-dossier-detail.component.ts#L66-L135) |
| A2 | Không nhìn ra ngay yếu tố nào là **hủy diệt** — nó là một checkbox lẫn trong hàng | [dòng 118-121](../../../frontend/src/app/features/company-dossier/company-dossier-detail.component.ts#L118-L121) |
| A3 | Kịch bản vô hiệu hoá đã chọn nằm trong `<select>`, phải nhìn kỹ mới đọc được | [dòng 114-117](../../../frontend/src/app/features/company-dossier/company-dossier-detail.component.ts#L114-L117) |

**Nhóm B — form khó nhìn, khó gõ tay.**

| # | Sự thật | Bằng chứng |
|---|---|---|
| B1 | Mô tả rủi ro và dấu hiệu quan sát dùng `<input>` **một dòng**: câu dài trôi ngang, gõ xong không đọc lại được cả câu | [dòng 107-111](../../../frontend/src/app/features/company-dossier/company-dossier-detail.component.ts#L107-L111) |
| B2 | Form chỉ chiếm nửa chiều ngang vì `grid lg:grid-cols-2` chia đôi với panel số liệu; trừ cột ▲▼ và nút ✕ còn ~480px cho ô nhập | [dòng 63](../../../frontend/src/app/features/company-dossier/company-dossier-detail.component.ts#L63) |
| B3 | Các ô chỉ có `placeholder`, không có nhãn — gõ chữ đầu tiên là mất hẳn phần nhắc ô đó hỏi gì | [dòng 86, 107, 109](../../../frontend/src/app/features/company-dossier/company-dossier-detail.component.ts#L86) |
| B4 | Dòng đỏ "Bắt buộc phải có dấu hiệu quan sát được" bật **ngay khi vừa bấm `+ Thêm yếu tố`**, trước khi người dùng chạm vào ô | [dòng 112](../../../frontend/src/app/features/company-dossier/company-dossier-detail.component.ts#L112) |
| B5 | Panel số liệu: nhãn và giá trị cùng cỡ chữ xám, 6 khối xếp dọc không phân cấp, cuộn dài | [fundamentals-panel.component.ts:29-151](../../../frontend/src/app/features/company-dossier/fundamentals-panel.component.ts#L29-L151) |

## 2. Quyết định

| # | Quyết định | Vì sao |
|---|---|---|
| Q1 | **Không thêm route mới.** Chế độ là state trong component, không phải URL segment. | Cùng một tài nguyên. Thêm `/view` là bắt link cũ, bookmark và nút quay lại phải biết chọn đường. |
| Q2 | **Mở đầu ở `view` khi hồ sơ đã tồn tại, `edit` khi GET trả 404.** | Mã chưa có hồ sơ thì không có gì để đọc; ép thêm một cú bấm "Sửa" là ma sát thuần. |
| Q3 | **`returnTo=trade-plan` hoặc `?edit=1` ⇒ mở thẳng `edit`.** | Khi cổng chặn lệnh đá sang đây, việc cần làm là viết chứ không phải đọc. |
| Q4 | **Nút Ký hiện ở cả hai chế độ**, giữ nguyên vị trí cuối trang. | "Vẫn đúng" là hành động của người vừa *đọc lại*, không phải của người vừa sửa. Giấu nó trong chế độ sửa là chặn đúng luồng hay dùng nhất. |
| Q5 | **Tách `dossier-view.component.ts` riêng**, nhận dữ liệu qua `@Input()`, phát `@Output() edit`. | Detail component đang 389 dòng; nhét thêm bản đọc vào cùng template đẩy nó qua 600 dòng và trộn hai trách nhiệm. |
| Q6 | **Ô nhập rủi ro chuyển sang `<textarea rows="2">`.** | Nguyên nhân trực tiếp của B1. Câu tiếng Việt tả một dấu hiệu quan sát gần như luôn dài hơn 480px. |
| Q7 | **Lỗi đỏ chỉ hiện sau khi ô đã chạm hoặc sau lần bấm Lưu đầu tiên.** | Báo sai trước khi người dùng làm gì thì lần thứ ba nhìn thấy là hết được đọc. |
| Q8 | **Thuần frontend.** Không đổi DTO, endpoint, entity. | Toàn bộ dữ liệu cần hiển thị đã có trong response `GET /api/v1/company-dossiers/{symbol}`. |

## 3. Phạm vi

### 3.1 Chế độ và điều hướng

`CompanyDossierDetailComponent` thêm trường `mode: 'view' | 'edit'`.

| Tình huống | Mode mở đầu |
|---|---|
| GET 200 (hồ sơ đã tồn tại) | `view` |
| GET 404 (mã chưa có hồ sơ) | `edit` |
| Query param `edit=1` | `edit` |
| Query param `returnTo=trade-plan` | `edit` |

Chuyển chế độ:

- `view` → `edit`: nút **Sửa** ở header.
- `edit` → `view`: nút **Hủy** (nếu form đã bị thay đổi thì `confirm()` trước khi bỏ), hoặc tự động sau khi **Lưu** thành công.

### 3.2 Chế độ xem — `dossier-view.component.ts` (mới)

- **Doanh nghiệp kiếm tiền bằng gì** — đoạn văn.
- **Moat** — danh sách chip.
- **Yếu tố rủi ro** — thẻ xếp theo hạng, hạng 1 nổi bật nhất:
  - badge đỏ `Yếu tố hủy diệt` khi `isDealBreaker`;
  - dấu hiệu quan sát in dưới mô tả, **không** dùng màu xám mờ — đó là dòng phải soi khi cầm mã;
  - kịch bản vô hiệu hoá hiện dạng chip (nhãn lấy từ `INVALIDATION_TRIGGER_LABELS`), ẩn khi `null`.
- **Ghi chú** — ẩn hẳn khối nếu rỗng.
- Header giữ badge freshness + "Soát gần nhất", thêm nút **Sửa**.

### 3.3 Chế độ sửa — chữa ergonomics

- `grid lg:grid-cols-2` → `lg:grid-cols-5`, form `lg:col-span-3`, panel số liệu `lg:col-span-2`. Mobile xếp dọc như cũ.
- Mô tả rủi ro và dấu hiệu quan sát: `<input>` → `<textarea rows="2">`.
- Mọi ô có **nhãn** phía trên; placeholder giữ nguyên làm ví dụ.
- Hạng: badge `#1` `#2` cạnh ▲▼ thay cho số xám 11px.
- `Yếu tố hủy diệt` kèm một dòng giải thích vì sao chỉ chọn được một.
- Nút ✕ chuyển xuống góc dưới-phải của thẻ, rời khỏi vùng gõ.
- Moat: có nhãn, ô rộng hết dòng, ✕ tách ra rìa.
- Validation theo Q7, kèm **một dòng tổng cạnh nút Lưu**: `Còn {n} yếu tố thiếu dấu hiệu quan sát` — để biết vì sao chưa ký được mà không phải cuộn tìm.

### 3.4 Panel số liệu (áp cho cả hai chế độ)

- 9 ô chỉ số: giá trị to đậm, nhãn nhỏ phía trên.
- 4 khối dài — doanh thu/quý, cùng ngành, cổ tức, kế hoạch kinh doanh — cho gập. Mặc định mở khối doanh thu, gập 3 khối còn lại.
- Giữ nguyên `hasSection()` và quy ước "không lấy được dữ liệu ≠ 0" — không đụng vào.

### 3.5 Sao chép cho AI · dán từ AI

Bối cảnh: MCP **đã có** `upsert_company_dossier` ([CompanyDossierTools.cs:72](../../../src/InvestmentApp.Api/Mcp/CompanyDossierTools.cs#L72)), nên AI nối được MCP thì đã sửa hồ sơ được. Hai nút này phục vụ AI **không** nối MCP (ChatGPT web, Gemini…).

**Nút "Sao chép cho AI"** (header, cả hai chế độ) — đưa vào clipboard một khối markdown gồm:

1. Nội dung hồ sơ hiện tại — mô hình kinh doanh, moat, rủi ro (kèm dấu hiệu quan sát và kịch bản vô hiệu hoá), ghi chú.
2. Số liệu doanh nghiệp từ panel — kèm nguyên văn ghi chú "phần nào không lấy được thì ghi là không lấy được, không phải 0".
3. Một dòng chỉ dẫn + **schema JSON** yêu cầu AI trả lời theo.

Dữ liệu số liệu lấy từ `FundamentalsPanelComponent` qua `@Output() dataLoaded` — panel vẫn tự gọi API như hiện tại, chỉ phát dữ liệu lên cha.

**Nút "Dán từ AI"** — modal có textarea:

- Parse khối ```json cuối cùng trong text; không có thì thử parse cả text.
- Shape **trùng đúng tham số của `upsert_company_dossier`**: `{ symbol, businessModel, moats: [{ description }], riskFactors: [{ rank, description, observableSignal, isDealBreaker, suggestedTrigger }], notes }`.
- Đổ vào form ở chế độ `edit`. **Không tự Lưu, không tự Ký** — người dùng đọc lại rồi mới bấm.

| Tình huống khi dán | Xử lý |
|---|---|
| Không parse được JSON | Báo lỗi trong modal, không đụng form |
| `symbol` có mặt và **khác** mã đang mở | **Chặn**, báo `Nội dung này của mã {X}, trang đang mở {Y}` |
| `rank` thiếu hoặc trùng | Đánh số lại 1..N theo thứ tự mảng |
| Nhiều hơn một `isDealBreaker` | Giữ cái đầu tiên, cảnh báo một dòng |
| `suggestedTrigger` không thuộc `INVALIDATION_TRIGGER_LABELS` | Về `null` |
| Trường lạ | Bỏ qua |
| `observableSignal` rỗng | Nhận, để validation của form bắt — buộc người dùng tự điền |

Ghi chú trách nhiệm: nội dung dán từ AI **không** đặt `agentDraftedAt` (đó là field backend của cửa MCP, và spec này thuần frontend). Lớp chịu trách nhiệm vẫn là chữ ký — người dùng phải bấm Ký sau khi đọc.

### 3.6 Xóa hồ sơ — đã cân nhắc, KHÔNG làm ở spec này

Hiện không có đường xóa nào (không endpoint, không method trong `CompanyDossierService`, không MCP tool). Đã thiết kế xong rồi **chủ động bỏ khỏi phạm vi** — ghi lại đây để lần sau không phải tìm lại từ đầu:

- Luật đúng là **chỉ xóa được hồ sơ đang chặn lập kế hoạch** (`Unconfirmed` | `Expired`) — tập này trùng khít tập cổng chặn ([CompanyDossierGate.cs:23-27](../../../src/InvestmentApp.Application/CompanyDossiers/Gate/CompanyDossierGate.cs#L23-L27)), nên xóa không bao giờ nới lỏng thứ gì.
- **Ràng buộc phải biết trước khi làm:** dòng thời gian của mã dựng mốc ký trực tiếp từ document, không lưu lịch sử riêng ([GetSymbolTimelineQuery.cs:234-244](../../../src/InvestmentApp.Application/JournalEntries/Queries/GetSymbolTimeline/GetSymbolTimelineQuery.cs#L234-L244)). Xóa hồ sơ ⇒ mốc ký biến mất khỏi timeline, hồi tố.
- Nếu làm: cần ADR (xóa cứng vs mềm là trade-off thật), và cố ý không mở ra MCP theo cùng lý lẽ ADR-0011 D2.

## 4. Không làm (YAGNI)

- Không thêm route `/view` hay `/edit`.
- Không lưu preference chế độ giữa các lần mở.
- Không animation chuyển chế độ.
- Không đụng backend, DTO, entity.
- Không làm chức năng xóa hồ sơ (xem 3.6).
- Không autosave — nút **Lưu** giữ nguyên ngữ nghĩa hiện tại.
- Không thêm MCP tool mới; không đụng `upsert_company_dossier`.
- Dán từ AI **không** tự Lưu và tuyệt đối không tự Ký — ADR-0011 D2 giữ nguyên.
- Không đẻ format riêng cho clipboard: dùng đúng shape payload của `upsert_company_dossier`.

## 5. Test

Mở rộng [company-dossier-detail.component.spec.ts](../../../frontend/src/app/features/company-dossier/company-dossier-detail.component.spec.ts):

| Ca | Kỳ vọng |
|---|---|
| GET 200 | `mode === 'view'` |
| GET 404 | `mode === 'edit'` |
| `returnTo=trade-plan` với GET 200 | `mode === 'edit'` |
| `edit=1` với GET 200 | `mode === 'edit'` |
| Lưu thành công từ `edit` | về `mode === 'view'` |
| Vừa `addRiskFactor()`, chưa chạm ô | không hiện lỗi đỏ |
| Sau `save()` với dấu hiệu rỗng | hiện lỗi đỏ + dòng đếm đúng số yếu tố thiếu |

Spec mới `dossier-view.component.spec.ts`:

| Ca | Kỳ vọng |
|---|---|
| Rủi ro có `isDealBreaker` | render badge `Yếu tố hủy diệt` |
| Nhiều rủi ro | render đúng thứ tự hạng tăng dần |
| `notes` rỗng | không render khối ghi chú |
| `suggestedTrigger === null` | không render chip |

Spec mới cho sao chép / dán (`dossier-clipboard.spec.ts` hoặc gộp vào detail spec):

| Ca | Kỳ vọng |
|---|---|
| Sao chép | text chứa mô hình kinh doanh, từng rủi ro kèm dấu hiệu quan sát, và khối schema JSON |
| Sao chép khi panel số liệu chưa tải xong | vẫn sao chép được phần hồ sơ, phần số liệu ghi "không lấy được dữ liệu" |
| Dán JSON hợp lệ | các trường của form khớp payload, `mode === 'edit'`, **chưa gọi** `upsert` |
| Dán text không phải JSON | báo lỗi, form giữ nguyên giá trị cũ |
| Dán JSON có `symbol` khác mã đang mở | bị chặn, form giữ nguyên |
| Dán JSON có 2 phần tử `isDealBreaker` | chỉ phần tử đầu giữ cờ |
| Dán JSON `rank` trùng nhau | rank được đánh lại 1..N |
| Dán JSON có `suggestedTrigger` lạ | trường về `null` |

## 6. Tài liệu phải đồng bộ trước khi commit

- [`docs/features.md`](../../features.md) — mô tả chế độ xem của trang hồ sơ.
- [`frontend/src/assets/CHANGELOG.md`](../../../frontend/src/assets/CHANGELOG.md) — mục release.
- `frontend/src/assets/docs/*.md` + đăng ký Help topic — hướng dẫn người dùng về nút Sửa / Hủy.
- [`docs/architecture.md`](../../architecture.md) — thêm `dossier-view.component.ts` vào bản đồ feature.

Không cần ADR: thuần frontend, không đổi schema, không đổi contract cross-layer, không đi ngược convention.
