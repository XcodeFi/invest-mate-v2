# Chia sẻ hồ sơ công ty qua clipboard

**Spec:** [`docs/superpowers/specs/2026-08-11-company-dossier-share-design.md`](../../superpowers/specs/2026-08-11-company-dossier-share-design.md)
**Ngày:** 2026-08-11
**Tầng chạm:** Frontend only
**ADR:** không cần — thuần frontend, không đổi schema, không đổi contract cross-layer.
**Phụ thuộc:** PR #156 (`feat/dossier-view-ux`) phải merge trước — plan này mở rộng `dossier-clipboard.ts` do PR đó tạo ra.

## Đã hoàn thành — 2026-08-11

**ADR:** không cần — thuần frontend, không đổi schema, không đổi contract cross-layer, không đi ngược convention nào.

**Q5 — bố cục header.** Làm theo phương án 4 nút phẳng (`Sao chép cho AI` · `Chia sẻ` · `Dán nội dung` · `Sửa`). Đổi sang menu `Sao chép ▾` chỉ đụng Bước 4.

**Lỗi code review bắt được (HIGH).** Bản đầu điền sẵn ô tên từ `AuthService.getCurrentUserValue()?.name`. Nhưng `auth.service.ts` đặt `name = payload.name || payload.email`, nên với tài khoản Google có tên thật thì `name` là **họ tên đầy đủ** — mà `maskSharerName` chỉ che chuỗi có `@`, nên họ tên trả về **nguyên văn**. Kết quả: mặc định lộ họ tên thật trong khi dòng chú thích ngay dưới ghi "email của bạn đã che bớt". Sửa sang `?.email`.

Test cũ cũng hỏng theo cùng một kiểu: nó giả `{ name: 'investmate.support@gmail.com' }` — một shape **không tồn tại** với tài khoản Google thật, nên test xanh trong khi đường code nó định phủ đang sai. Đã đổi sang shape thật `{ name: 'Trương Phạm', email: 'truong.pham@gmail.com' }`.

**Kết quả:** 343 test frontend xanh; verify thật 17/17 đạt (`scratch/qa-reports/qa-verify-dossier-share-20260811-0840z.md`), trong đó bộ đếm mạng ghi nhận **0 lời gọi** trong toàn bộ luồng chia sẻ và dán.

---

## Bước 1 — Che email, hàm thuần

**File:** `frontend/src/app/features/company-dossier/dossier-clipboard.ts`

Thêm export:

```ts
export function maskSharerName(name: string | null | undefined): string
```

Quy tắc (spec §3.2): không có `@` ⇒ giữ nguyên. Có `@` ⇒ giữ 3 ký tự đầu phần local (1 ký tự nếu local ngắn hơn 4), thay phần còn lại bằng `***`, giữ `@domain`. Local rỗng hoặc input rỗng ⇒ trả `''`.

**Test trước** (`dossier-clipboard.spec.ts`) — 7 ca ở spec §5, gồm ca `@gmail.com` local rỗng và ca `investmate.support@gmail.com` phải **không** chứa `support`.

→ verify: `npx ng test --watch=false --browsers=ChromeHeadless`, 7 ca đỏ trước, xanh sau.

## Bước 2 — Dựng payload chia sẻ

**File:** cùng file.

```ts
export function buildSharePayload(
  content: DossierContent,
  sharedBy: string,
  sharedAt: string,   // YYYY-MM-DD
): string
```

Trả khối theo spec §3.1: hai dòng chữ cho người + khối JSON. `sharedBy` rỗng ⇒ **bỏ hẳn khoá** khỏi JSON và bỏ mệnh đề "— {tên} chia sẻ" khỏi dòng tiêu đề.

**Khác `buildAiPrompt` ở chỗ nào** (đây là điểm dễ làm sai): không schema, không số liệu 24hmoney, không câu yêu cầu soát lại. Hàm này **không** nhận `CompanyFundamentals`.

**Test trước** — 5 ca: có nội dung hồ sơ; không có schema; không có số liệu; vòng tròn build→parse khớp; `sharedBy` rỗng thì không có khoá.

## Bước 3 — Đọc `sharedBy` / `sharedAt` khi dán

**File:** cùng file.

`ParsedDossier` thêm `sharedBy?: string`, `sharedAt?: string`. Trong `parseAiPayload()`, đọc như các field khác: chỉ nhận khi `typeof === 'string'`, sai kiểu thì bỏ qua chứ không làm hỏng cả lần dán.

**Test trước** — 3 ca: có cả hai; không có (đều `undefined`, **không** phải chuỗi rỗng); sai kiểu (số/object) thì bỏ qua mà phần còn lại vẫn dán được.

⚠️ Đừng để `sharedBy`/`sharedAt` lọt vào payload gửi lên API. `save()` map từng field một nên hiện tại an toàn — giữ nguyên cách map đó, đừng đổi sang spread.

## Bước 4 — Hộp thoại Chia sẻ

**File:** `frontend/src/app/features/company-dossier/company-dossier-detail.component.ts`

- Nút `Chia sẻ` ở header, cạnh `Sao chép cho AI`.
- Đổi nhãn nút dán: `Dán từ AI` → **`Dán nội dung`** (giữ nguyên `data-testid="btn-open-paste"` để test cũ không vỡ).
- Hộp thoại theo spec §3.3.1: overlay `z-[60]`, ô `Bạn hiện là` điền sẵn `maskSharerName(auth.getCurrentUserValue()?.name)`, vùng xem trước chỉ đọc cập nhật theo ô, thứ tự nút `[Hủy] → [Sao chép]`.
- Giá trị ô nhớ trong `localStorage` (khoá `dossierSharerName`); có giá trị đã lưu thì dùng nó thay cho giá trị che.

⚠️ **Không dùng backtick trong bất kỳ giá trị thuộc tính nào** của inline template — kể cả trong `placeholder`. Backtick đóng sớm `template:` và gây một loạt TS1005/TS1002 mà dòng lỗi đầu tiên lại là "spec không import được component".

**Test trước** — 5 ca hộp thoại ở spec §5.

## Bước 5 — Chèn dòng nguồn vào Ghi chú

**File:** cùng component, trong `applyPaste()`.

Có `sharedBy` ⇒ chèn lên **đầu** ô Ghi chú: `Nhận từ {sharedBy} ngày {DD/MM/YYYY}.` + một dòng trống + ghi chú gốc.

| Tình huống | Xử lý |
|---|---|
| Không có `sharedBy` | Không chèn gì |
| Có `sharedBy`, không `sharedAt` | `Nhận từ Minh.` — không để chữ "ngày" cụt |
| Ghi chú đã chứa nguyên văn dòng đó | Không chèn trùng |

**Test trước** — 6 ca ở spec §5.

⚠️ Ngày: dùng `Date.UTC` + các biến thể `*UTC*` nếu có phép tính ngày; đừng trộn parse UTC với `getMonth()` local.

## Bước 6 — Verify trên browser

Chạy `/qa-verify`. Kịch bản tối thiểu:

1. Mở một mã đã có hồ sơ → `Chia sẻ` → ô điền sẵn đã che → sửa thành tên khác → xem trước đổi theo → `Sao chép`.
2. Dán chuỗi đó vào cùng trang → ghi chú có dòng `Nhận từ …`, `mode === 'edit'`.
3. Dán lần hai → vẫn một dòng nguồn.
4. Dán payload từ AI (không `sharedBy`) → không có dòng nguồn.
5. Dán payload có `symbol` khác → vẫn chặn cứng.
6. Hook `fetch` + `XMLHttpRequest.open` → **không có lời gọi non-GET nào** trong toàn bộ luồng.

⚠️ `appsettings.Development` trỏ DB **`InvestmentApp_prod`**. Không bấm Lưu, không bấm Ký khi verify.

## Bước 7 — Review, docs, PR

- **Code review bắt buộc**, và **review lại chính phần vừa sửa** theo [`/code-review` Step 4.3](../../../.claude/commands/code-review/references/review-workflow.md) — agent mới, scope đúng diff fix.
- Docs đồng bộ: bảng ở spec §6.
- PR theo `/pr`.

---

## Rủi ro đã biết

| Rủi ro | Xử lý |
|---|---|
| Người dùng tưởng "Chia sẻ" gửi thẳng cho tài khoản kia | Chú thích trong hộp thoại nói rõ là sao chép để tự gửi; tên nút là "Sao chép" chứ không phải "Gửi" |
| Payload dán vào Zalo bị cắt vì quá dài | Hồ sơ dài nhất hiện có ~2KB, dưới hạn mọi kênh chat. Không xử lý trước; gặp thật thì tính |
| Người nhận ký một luận điểm không phải mình viết | Chấp nhận có ý thức (spec Q3). Dòng nguồn trong ghi chú là vệt duy nhất, và xoá được |
