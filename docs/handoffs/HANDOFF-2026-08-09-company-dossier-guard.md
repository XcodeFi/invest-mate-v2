# Handoff — Hồ sơ công ty làm cổng chặn lập kế hoạch mua (chặng 1)

**Ngày:** 2026-08-09 · **Nhánh:** `feature/company-dossier-guard` (worktree `d:\invest-mate-v2\wt-dossier-spec`)
**HEAD:** `66acadb`, 24 commit trên `origin/master` · **Test:** 1596/1596

## Đã xong

Chặng 1 backend, Task 1–6 của [plan](../superpowers/plans/2026-08-09-company-dossier-guard.md):
entity `CompanyDossier`, repository + index unique `(UserId, Symbol)`, gate phân bậc theo 5% tài khoản,
nối vào luồng tạo và luồng sửa plan, và bộ REST `api/v1/company-dossiers` kèm nhánh `DossierGateException → 400`.

Thiết kế đầy đủ ở [spec](../superpowers/specs/2026-08-09-company-dossier-design.md), Q1–Q15.

**Verify thật trên DB prod với tài khoản test, mã HPG, 8/8:** chưa có hồ sơ → `POST /trade-plans` trả 400
`DOSSIER_GATE_FAILED reason=missing` → viết hồ sơ → 400 `reason=unconfirmed` → ký → `freshness=Fresh` →
tạo plan 201 → xoá plan. Cách chạy: API local cổng 5199 với `ASPNETCORE_ENVIRONMENT=Development`, JWT lấy từ
`MintStableJwtTests`.

## Việc còn lại trước khi merge được

1. **Task 7** — trang hồ sơ + chặn ở UI. Brief phải chốt: `gate-status` giờ đã có `Freshness` và bắt buộc
   `quantity`/`entryPrice`/`accountBalance`; ba lý do `missing`/`unconfirmed`/`expired` trả `missing[]` rỗng
   nên câu chữ tiếng Việt phải cố định một chỗ; và `NeedsReview` **đỗ** cổng nên nhắc "nên xem lại" là việc của UI.
2. **Task 8** — đồng bộ `docs/architecture.md`, `docs/business-domain.md`, `docs/features.md`,
   `frontend/src/assets/CHANGELOG.md`, tài liệu người dùng, và viết **ADR-0011**. ADR phải ghi: chặn lúc tạo
   chứ không lúc arm (Q3); agent viết được nhưng không ký được (Q8); chỉ `Confirm()` đẩy đồng hồ hạn tươi (Q10);
   đổi mã thì luôn chạy lại cổng (Q13); **không grandfathering** (Q14); chặng 1 làm tắt đường ghi trade plan của
   agent (Q15); lệch `+07:00` cố định so với convention `TimeZoneInfo` của `GetPendingThesisReviewsQuery`;
   và nhượng bộ `List<T>` mutable do driver Mongo bắt buộc.
3. **Chặng 2 (Task 9–11) không được cắt.** Thiếu nó thì cổng không có chìa cho agent — xem Q15.

## Cần chủ sở hữu quyết

- Spec đã sửa nhưng **chưa commit** (Q13/Q14/Q15, §5.1, §5.3b, §5.4).
- **3 hồ sơ test VNM/MWG/HPG còn trên prod** (tài khoản test). Không có endpoint `DELETE` nên muốn dọn phải
  xoá tay trong Mongo.
- Ghi chú deploy: lệnh đầu tiên sau khi deploy **chắc chắn bị chặn** cho mọi mã (Q14).

## Minor còn treo

Ledger đầy đủ ở `.superpowers/sdd/2026-08-09-company-dossier-guard/progress.md` (git-ignored). Đáng sửa nhất:
thông điệp `"riskFactors: mô tả không được để trống ở hạng {ranks}"` chỉ có một mệnh đề, lệch khuôn
"cần X, đang có Y" của các thông điệp chị em.
