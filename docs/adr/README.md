# Architectural Decision Records (ADR)

Ghi lại các **quyết định quan trọng** đã chốt trong quá trình phát triển Invest Mate v2. Mục đích: 6 tháng sau vẫn tra được **"tại sao chọn A thay vì B"**, không phải "đã làm gì" (git log lo phần đó).

## Khi nào viết ADR

Viết ADR khi quyết định **thỏa ÍT NHẤT 1** trong các điều kiện sau:

1. **Ảnh hưởng ≥ 2 layer** (VD: thay đổi contract giữa Domain ↔ Application, hoặc backend contract đổi buộc frontend đổi theo).
2. **Khó revert** — migration dữ liệu, đổi schema DB, đổi public API shape, đổi convention toàn project.
3. **Có trade-off rõ ràng giữa các option** — "A nhanh hơn nhưng B dễ maintain hơn" → record lý do chọn.
4. **Đi ngược với default/convention hiện tại** — VD: dùng camelCase ở chỗ project đang dùng PascalCase.
5. **Decision sẽ bị hỏi lại** — "tại sao không dùng thư viện X?", "tại sao field này nullable?".

## Khi nào KHÔNG cần ADR

- Bug fix thông thường — commit message là đủ.
- Thêm field/endpoint không đổi contract hiện tại.
- Styling, copy, format — docs thường đã cover.
- Quyết định trivial (đặt tên biến, chọn lib utility nhỏ).

Ranh giới: *"Liệu 6 tháng sau có ai hỏi tại sao không?"* Có → viết. Không → bỏ qua.

## Format & quy ước

- **File name:** `NNNN-kebab-case-title.md` — NNNN là số 4 chữ số tăng dần, bắt đầu từ `0001`. VD: `0001-mongodb-pascalcase-fields.md`.
- **Không xóa ADR cũ** — nếu quyết định sau ghi đè quyết định cũ:
  - ADR cũ → sửa `Status: Superseded by ADR-NNNN`, không xóa nội dung.
  - ADR mới → trong `Context` ghi rõ `Supersedes ADR-NNNN` + lý do.
- **Ngôn ngữ:** tiếng Việt có dấu (thống nhất với `CLAUDE.md`). Technical term giữ tiếng Anh.
- **Độ dài mục tiêu:** 1 trang (~50-150 dòng). Dài hơn → tách thành plan trong `docs/plans/`.
- **Template:** xem [template.md](template.md).

## Trạng thái (Status)

| Status | Ý nghĩa |
|---|---|
| `Proposed` | Đang thảo luận, chưa chốt |
| `Accepted` | Đã chốt và implement |
| `Superseded by ADR-NNNN` | Bị thay thế bởi ADR khác |
| `Deprecated` | Không còn áp dụng nhưng không có ADR thay thế (VD: tính năng đã gỡ) |

## Relationship với các artifact khác

| Artifact | Mục đích | Khi dùng |
|---|---|---|
| `docs/plans/*.md` | **PRD + TDD hybrid** — what/why feature, how implement | Mỗi feature mới |
| `docs/adr/NNNN-*.md` | **ADR** — why chose X over Y | Khi có quyết định quan trọng (điều kiện ở trên) |
| `docs/architecture.md` | **Snapshot hiện tại** — codebase map | Update khi đổi structure |
| `git log` + commit msg | **What changed** | Mỗi commit |

Một plan có thể **sinh ra 0, 1 hoặc nhiều ADR**. Plan mô tả feature; ADR mô tả decision.

## Workflow trong `/ship` skill

Skill `/ship` sẽ **prompt tự động** khi phát hiện plan có trigger (đổi schema, đổi contract, có từ "chọn X thay Y", v.v.). Xem chi tiết tại `.claude/commands/ship/SKILL.md`.
