# Follow-up: agent-doc ETag không đổi mỗi deploy (version = "dev")

**Trạng thái:** Mở (chưa làm)
**Ngày phát hiện:** 2026-07-24
**Liên quan:** PR #126 (bake `APP_VERSION` vào `InformationalVersion`), ADR-0005 §7, `learning_pitfall_etag_static_informationalversion`
**Mức ưu tiên:** Thấp–Trung (không chặn tính năng; chỉ ảnh hưởng cache-invalidation của agent doc)

## 1. Bối cảnh

`GET /api/v1/ai/agent/doc` đặt `ETag = AiAgentController.DocVersion`, mà `DocVersion` đọc `AssemblyInformationalVersionAttribute`. Client (NPU `agent_api.py`) cache doc + ETag, mỗi lần gọi làm conditional GET (`If-None-Match`); server trả `304` → giữ cache, `200` → cập nhật.

PR #126 sửa `Dockerfile.api`: khai báo `ARG APP_VERSION=dev` ở build stage + truyền `-p:InformationalVersion=$APP_VERSION` cho `dotnet publish`. Ý định: Cloud Build đưa `--build-arg APP_VERSION=$SHORT_SHA` → ETag đổi theo commit mỗi deploy → NPU tự re-fetch.

## 2. Vấn đề quan sát được (2026-07-24)

Sau khi merge #126 + deploy, chạy `python agent_api.py --force` trả về:

```
version dev (force-downloaded)
```

- Version là **`dev`** — đúng giá trị default của `ARG APP_VERSION=dev` trong Dockerfile, **không phải commit SHA**.
- Trước #126 version là `1.0.0`; giờ là `dev`. Cả hai đều là **hằng số** → ETag vẫn không đổi giữa các deploy.
- Doc mới (endpoint `portfolios` + `fees/calculate`) vào được cache **chỉ nhờ `--force`** (bỏ qua `If-None-Match`), KHÔNG phải nhờ ETag tự đổi.

## 3. Root cause (giả thuyết cần xác nhận)

`$SHORT_SHA` **rỗng** tại thời điểm build → `--build-arg APP_VERSION=` (rỗng) → Docker dùng default `dev`.

`$SHORT_SHA` (và `$COMMIT_SHA`, `$SHORT_SHA`, `$BRANCH_NAME`, …) chỉ được Cloud Build tự điền khi build **được kích hoạt từ một commit/trigger gắn với repo** (GitHub push trigger). Nếu deploy chạy bằng trigger thủ công / `gcloud builds submit` không kèm source repo → các biến này rỗng.

`cloudbuild.yaml` hiện truyền:
```yaml
- '--build-arg'
- 'APP_VERSION=$SHORT_SHA'
```

## 4. Ảnh hưởng

- ETag agent-doc là hằng (`dev`) → sau mỗi deploy có doc mới, NPU vẫn nhận `304` và đọc **doc cũ** cho tới khi có ai đó `--force`.
- Đây đúng là bug mà #126 định sửa — sửa chưa trọn vì version resolve về default.
- **Không ảnh hưởng** tính năng backend; chỉ là độ tươi của tài liệu agent phía NPU.

## 5. Phương án

### Option A — Quick: dùng biến Cloud Build luôn có giá trị
Đổi `cloudbuild.yaml` sang `$COMMIT_SHA` (hoặc `$BUILD_ID` làm fallback). `$COMMIT_SHA` thường được điền cho trigger gắn repo; nếu vẫn lo rỗng, thêm substitution mặc định:
```yaml
substitutions:
  _APP_VERSION: ${COMMIT_SHA}   # hoặc set trong trigger
# ...
- '--build-arg'
- 'APP_VERSION=${_APP_VERSION}'
```
- **Ưu:** 1 dòng, giữ cơ chế hiện tại. **Nhược:** vẫn phụ thuộc trigger điền biến; ETag đổi cả khi doc KHÔNG đổi (re-download thừa mỗi deploy).

### Option B (khuyến nghị) — Robust: ETag = hash NỘI DUNG doc
`DocVersion` không nên gắn với version build; ETag đúng nghĩa phải phản ánh **nội dung tài liệu**. Tính `ETag = SHA256(LoadDoc())` (rút gọn 8–16 hex).
- **Ưu:** tự-đúng, độc lập deploy/trigger/version; chỉ đổi khi doc thực sự đổi → NPU re-fetch đúng lúc, không thừa. Xoá hẳn cả class bug "version tĩnh".
- **Nhược:** đổi vài dòng trong `AiAgentController` (tính hash 1 lần, cache static). Cần 1 unit test.
- TDD: test `GetDoc` trả ETag ổn định giữa 2 lần gọi + đổi khi nội dung doc đổi (có thể test qua hàm hash thuần).

## 6. Giảm thiểu tạm thời (đang dùng)
Sau mỗi deploy: `python agent_api.py --force` (cờ đã có, commit `ddf6e0f` nhánh `fix/agent-doc-force-refresh` repo npu-assistant) — kéo doc live bỏ qua ETag.

## 7. Tiêu chí hoàn thành
1. Sau 2 deploy khác nhau, `curl -I .../ai/agent/doc` trả ETag **khác nhau khi doc đổi** và **giống nhau khi doc không đổi** (Option B), hoặc ít nhất **không còn là hằng `dev`/`1.0.0`** (Option A).
2. NPU `refresh_doc()` (không `--force`) tự nhận doc mới sau deploy có thay đổi tài liệu.
3. Có unit test cho cơ chế ETag mới (Option B).

## 8. Việc kiểm tra trước khi làm
- Xác nhận trigger deploy thực tế: `gcloud builds list --limit 5` xem `SOURCE`/substitutions; kiểm tra `$SHORT_SHA` có rỗng không (log build in ra `APP_VERSION=`).
- Quyết Option A vs B (khuyến nghị B).
