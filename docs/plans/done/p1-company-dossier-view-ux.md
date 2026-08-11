# Hồ sơ công ty — chế độ xem, form dễ gõ, clipboard với AI ngoài

**Spec:** [`docs/superpowers/specs/2026-08-10-company-dossier-view-and-form-ux-design.md`](../superpowers/specs/2026-08-10-company-dossier-view-and-form-ux-design.md)
**Ngày:** 2026-08-10
**ADR:** không cần — thuần frontend, không đổi schema, không đổi contract cross-layer, không đi ngược convention.
**Ngoài phạm vi:** chức năng xóa hồ sơ (đã thiết kế rồi chủ động bỏ — xem spec §3.6).

## Chia chặng

Toàn bộ là frontend, chạm cùng hai file component. Chia theo thứ tự bắt buộc, không phải theo sở thích:

| Chặng | Nội dung | Spec | Vì sao thứ tự này |
|---|---|---|---|
| **P1** | Chế độ xem — tách `dossier-view.component.ts`, `mode: 'view' \| 'edit'` | §3.1–3.2 | Tách template trước |
| **P2** | Form dễ gõ + panel số liệu phân cấp | §3.3–3.4 | Sửa đúng nhánh `edit` mà P1 vừa tách; làm ngược là sửa hai lần |
| **P3** | Sao chép cho AI · dán từ AI | §3.5 | Cần cả hai chế độ đã ổn định mới gắn nút vào header |

---

## P1 — Chế độ xem

**What:** `/company-dossier/:symbol` mở ra ở bản đọc khi hồ sơ đã tồn tại, thay vì rơi thẳng vào form.

**Where:**
- Mới: `frontend/src/app/features/company-dossier/dossier-view.component.ts` — `@Input()` các trường đã load, `@Output() edit`.
- Sửa: `company-dossier-detail.component.ts` — thêm `mode`, quyết định mode lúc `ngOnInit`, nút Sửa/Hủy, Lưu xong về `view`.

**Mode lúc mở:**

| Tình huống | Mode |
|---|---|
| GET 200 | `view` |
| GET 404 | `edit` |
| `?edit=1` | `edit` |
| `returnTo=trade-plan` | `edit` |

**Tests** (`company-dossier-detail.component.spec.ts` + `dossier-view.component.spec.ts` mới):
- 4 ca mode lúc mở ở bảng trên.
- Lưu thành công từ `edit` → về `view`.
- View: badge `Yếu tố hủy diệt` khi `isDealBreaker`; thứ tự hạng tăng dần; `notes` rỗng → không render khối; `suggestedTrigger === null` → không render chip.

**Risks:** nút **Ký** phải hiện ở **cả hai** chế độ (spec Q4) — giấu nó trong `edit` là chặn đúng luồng hay dùng nhất ("đọc lại rồi bấm Vẫn đúng").

## P2 — Form dễ gõ + panel số liệu

**What:** chữa 4 nguyên nhân khiến form khó nhìn khó gõ (spec §1 nhóm B).

**Where:** `company-dossier-detail.component.ts`, `fundamentals-panel.component.ts`.

- `grid lg:grid-cols-2` → `lg:grid-cols-5`; form `lg:col-span-3`, panel `lg:col-span-2`. Mobile giữ xếp dọc.
- Mô tả rủi ro + dấu hiệu quan sát: `<input>` → `<textarea rows="2">`.
- Mọi ô có nhãn thật; placeholder giữ làm ví dụ.
- Hạng: badge `#1` `#2` cạnh ▲▼.
- Nút ✕ xuống góc dưới-phải thẻ, rời vùng gõ.
- Lỗi đỏ chỉ sau khi ô đã chạm **hoặc** sau lần bấm Lưu đầu tiên; thêm dòng tổng cạnh nút Lưu: `Còn {n} yếu tố thiếu dấu hiệu quan sát`.
- Panel: 9 ô chỉ số giá trị to đậm / nhãn nhỏ; 4 khối dài cho gập, mặc định mở khối doanh thu.

**Tests:** không hiện lỗi khi vừa `addRiskFactor()` chưa chạm; hiện lỗi sau `save()`; dòng đếm đúng số yếu tố thiếu.

**Risks:** giữ nguyên `hasSection()` và quy ước "không lấy được dữ liệu ≠ 0" của panel — đó là lớp chống đọc sai đã có, không được gộp vào việc làm đẹp.

## P3 — Clipboard với AI ngoài

**What:** nút **Sao chép cho AI** và nút **Dán từ AI**, phục vụ AI không nối MCP.

**Where:** `company-dossier-detail.component.ts`; `fundamentals-panel.component.ts` thêm `@Output() dataLoaded` để cha lấy được số liệu.

- Copy: markdown gồm nội dung hồ sơ + số liệu doanh nghiệp + schema JSON yêu cầu AI trả về.
- Paste: parse khối ```json cuối cùng; shape **trùng payload `upsert_company_dossier`**; đổ vào form ở `edit`; **không tự Lưu, không tự Ký**.

**Xử lý khi dán:** không parse được → báo lỗi, không đụng form; `symbol` khác mã đang mở → **chặn**; `rank` thiếu/trùng → đánh lại 1..N; >1 `isDealBreaker` → giữ cái đầu; `suggestedTrigger` lạ → `null`; trường lạ → bỏ qua; `observableSignal` rỗng → nhận, để validation bắt.

**Tests:** 8 ca trong spec §5 (copy có đủ nội dung + schema; copy khi panel chưa tải xong; 6 ca dán).

**Risks:** dán nội dung mã khác vào trang đang mở là lỗi im lặng nguy hiểm nhất — chặn cứng, không chỉ cảnh báo.

---

## Môi trường test

Dự án **không có `zone.js/testing`** — không dùng `fakeAsync`; test bất đồng bộ dùng `done` + `setTimeout` thật.

## Tài liệu phải đồng bộ

| File | Nội dung |
|---|---|
| [`docs/features.md`](../features.md) | Mô tả chế độ xem + hai nút clipboard ở dòng 836-837 và §"Hồ sơ công ty" |
| [`docs/architecture.md`](../architecture.md) | Thêm `dossier-view.component.ts` |
| [`docs/business-domain.md`](../business-domain.md) | Dòng 524 — mô tả trang chi tiết |
| [`docs/project-context.md`](../project-context.md) | Quyết định UX: mặc định mở ở chế độ đọc |
| [`frontend/src/assets/CHANGELOG.md`](../../frontend/src/assets/CHANGELOG.md) | Mục release mỗi chặng |
| `frontend/src/assets/docs/*.md` + Help topic | Hướng dẫn nút Sửa / Sao chép / Dán |

---

## Checkpoint — P1, P2, P3 (done, 2026-08-11)

- **Quyết định:** mặc định `view` khi GET 200; `canSign()` khoá khi `isDirty()`; shape dán trùng payload `upsert_company_dossier`; vị từ dirty gom về **một** `serializeEditable()` dùng cho cả hai vế.
- **Files:** `dossier-view.component.ts` (mới), `dossier-clipboard.ts` (mới), `company-dossier-detail.component.ts`, `fundamentals-panel.component.ts` + 4 file spec.
- **Tests:** 221 → **273** (52 mới), tất cả xanh.
- **Tầng chạm:** Frontend only.
- **Code review:** 4 phát hiện, sửa cả 4 — HIGH (`touched` làm bẩn dirty-check), MEDIUM (ký được khi form dở), 2 LOW (`@Output` chết, thiếu `aria-expanded`).
- **QA verify:** `scratch/qa-reports/qa-verify-dossier-view-ux-20260811-0240z.md` — 20/21 pass; kịch bản 21 (gập/bung panel) **không verify được trên browser** vì provider trả `unavailableSections` cho cả 4 khối ở mọi mã, đã bù bằng 5 unit test.
- **Ngoài phạm vi:** xóa hồ sơ (spec §3.6) — thiết kế xong rồi chủ động bỏ.
