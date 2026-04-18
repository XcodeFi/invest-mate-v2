---
name: pr
description: Lightweight PR workflow — self-review changes, update documentation, then commit & create PR. Use when asked to "pr", "tạo pr", "make a pr", or when code is already done and user wants to ship it without the full ship workflow. Skips analyze/plan, TDD, and manual verification. Documentation update is the most important step.
---

# PR — Lightweight Ship Workflow

Giả định code đã viết xong và đã verify thủ công. Skill này chỉ lo: **review → docs → PR**.

Orchestrates: Code Review → Update Documentation → Commit & PR.

## Khi nào dùng skill này vs `/ship`

| Tình huống | Dùng |
|---|---|
| Code đã xong, chỉ cần review + tạo PR | **`/pr`** |
| Bắt đầu feature mới từ đầu (cần analyze, plan, TDD) | `/ship` |
| Bug fix lớn, chưa có test | `/ship` |
| Đổi docs, config, text tiếng Việt | `/pr` |

## Model Strategy

| Phase | Execution | Model |
|---|---|---|
| Phase 1: Code Review | 1 sub-agent | **sonnet** |
| Phase 2: Docs | Main context | any |
| Phase 3: Commit & PR | Main context | any |

---

## Phase 1: Code Review (self-review)

Dùng **1 sub-agent** (`model: "sonnet"`) để static review thay đổi.

### Step 1.1 — Run Review

1. Lấy diff so với base branch (`git diff <base>...HEAD --name-only` và `git diff <base>...HEAD`)
2. Detect stacks từ file thay đổi: frontend (Angular 19), backend (.NET 9), data (MongoDB)
3. Launch 1 sonnet agent covering: project guidelines (CLAUDE.md), bugs, security, performance — chỉ check patterns cho stack bị ảnh hưởng. Dùng checklist + scoring của skill `/code-review`.

### Step 1.2 — Triage

Filter findings >= 80 confidence. Hiển thị dạng card. User chọn per-issue: **Fix** / **Ignore** / **Post**.

### Step 1.3 — Fix and Re-verify

Nếu có fix:
1. Chạy test liên quan để confirm không regression
   - Domain fix → `dotnet test tests/InvestmentApp.Domain.Tests`
   - Application fix → `dotnet test tests/InvestmentApp.Application.Tests`
   - Infrastructure fix → `dotnet test tests/InvestmentApp.Infrastructure.Tests`
   - Frontend fix → `ng test` (nếu có spec liên quan)
2. Fix đáng kể (logic mới) → loop lại Step 1.1
3. Fix nhỏ (typo, naming) → proceed

---

## Phase 2: Update Documentation ⭐ (trọng tâm)

**Đây là bước quan trọng nhất của skill này.** Không skip.

### Step 2.1 — Quét thay đổi

Chạy `git diff --name-only <base>...HEAD`, rồi update TẤT CẢ docs phù hợp:

| Loại thay đổi | Doc cần update |
|---|---|
| Entity, API endpoint, route | [`docs/business-domain.md`](docs/business-domain.md) |
| Feature (mới/đổi) | [`docs/features.md`](docs/features.md) |
| Service, controller, repository, page, shared component, external integration | [`docs/architecture.md`](docs/architecture.md) |
| Bug pattern mới, improvement item hoàn thành, UX/architecture decision | [`docs/project-context.md`](docs/project-context.md) |
| Convention, directive, pipe mới | [`CLAUDE.md`](CLAUDE.md) |
| User-facing feature mới/đổi | File hướng dẫn liên quan trong [`frontend/src/assets/docs/`](frontend/src/assets/docs/) |

### Step 2.2 — Archive Plan (nếu có)

Nếu PR này hoàn thành 1 plan trong [`docs/plans/`](docs/plans/):
```bash
git mv docs/plans/xxx.md docs/plans/done/xxx.md
```

Nếu plan còn phase chưa làm → giữ ở `docs/plans/`, viết checkpoint.

### Step 2.3 — Update Changelog

Update [`frontend/src/assets/CHANGELOG.md`](frontend/src/assets/CHANGELOG.md):
- Xác định version bump (patch/minor/major)
- Add entry lên đầu, theo format hiện có
- Dùng ngày hôm nay
- Text tiếng Việt có dấu đầy đủ

### Step 2.4 — Confirm với user

Trước khi commit, tóm tắt các doc đã update và hỏi user có cần bổ sung gì không.

---

## Phase 3: Commit & PR

### Step 3.1 — Commit

1. Chạy `dotnet test` nếu Phase 1 có fix. Nếu không — skip.
2. Stage: code + tests + docs + changelog
3. Commit message: **tiếng Việt có dấu đầy đủ, rõ ràng** (VD: `feat(trade-plan): thêm state machine và matrix editability`)
4. Commit (KHÔNG dùng `--no-verify`)

### Step 3.2 — Rebase + Push + Create PR

Rebase theo rule của global `/pr` (xem [`~/.claude/commands/pr.md`](file:///C:/Users/a/.claude/commands/pr.md)):

1. **Fetch + detect target**:
   - `git fetch origin`
   - Nếu user chỉ định release branch → dùng trực tiếp
   - Nếu không → `git branch -r --sort=-committerdate | grep "origin/release" | head -5`, hỏi user chọn; fallback `origin/master`
2. **Rebase**: `git rebase origin/<target>`
   - Conflicts → STOP, báo user files conflict, KHÔNG auto-resolve
3. **Push**: `git push --force-with-lease -u origin <current-branch>` (KHÔNG dùng `--force`)
4. **Check PR tồn tại**: `gh pr list --head <current-branch> --json url,title` — nếu đã có, trả về URL
5. **Create PR** với template giống `/ship`:

```bash
gh pr create --base <target-branch> --title "<title tiếng Anh, < 70 ký tự>" --body "$(cat <<'EOF'
## Summary
- Đã thay đổi gì và tại sao

## Changes
- Nhóm theo Backend / Frontend / Docs

## Test plan
- [ ] Backend tests pass (`dotnet test`)
- [ ] Frontend tests pass (nếu applicable)
- [ ] Đã manual verify trước khi PR

## Docs updated
- [ ] Doc nào đã được update

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Return PR URL.

---

## Error Handling

- Phase 1 review tìm ra critical issue (>= 90 confidence) → BẮT BUỘC fix trước khi proceed
- Tests fail ở Phase 3 → stop và fix, KHÔNG skip bằng `--no-verify`
- `gh` không khả dụng → cung cấp command cho user chạy tay
- Đang ở `master`/`main` → tạo feature branch trước
- Docs chưa sync với code → KHÔNG commit (vi phạm rule trong CLAUDE.md)