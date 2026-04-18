---
name: pr
description: Lightweight PR workflow — self-review changes, update documentation, then commit & create PR. Use when asked to "pr", "tạo pr", "make a pr", or when code is already done and user wants to ship it without the full ship workflow. Skips analyze/plan, TDD, and manual verification. Documentation update is the most important step.
---

# PR — Lightweight Ship Workflow

Assumes code is already written and manually verified. This skill only handles: **review → docs → PR**.

Orchestrates: Code Review → Update Documentation → Commit & PR.

## When to use this vs `/ship`

| Situation | Use |
|---|---|
| Code is done, just need review + create PR | **`/pr`** |
| Starting a new feature from scratch (need analyze, plan, TDD) | `/ship` |
| Large bug fix without existing tests | `/ship` |
| Docs-only, config-only, or UI text changes | `/pr` |

## Model Strategy

| Phase | Execution | Model |
|---|---|---|
| Phase 1: Code Review | 1 sub-agent | **sonnet** |
| Phase 2: Docs | Main context | any |
| Phase 3: Commit & PR | Main context | any |

---

## Phase 1: Code Review (self-review)

Uses **1 sub-agent** (`model: "sonnet"`) for static review.

### Step 1.1 — Run Review

1. Get diff against base branch (`git diff <base>...HEAD --name-only` and `git diff <base>...HEAD`)
2. Detect affected stacks from changed files: frontend (Angular 19), backend (.NET 9), data (MongoDB)
3. Launch 1 sonnet agent covering: project guidelines (CLAUDE.md), bugs, security, performance — only check patterns for the affected stacks. Use the same checklist and scoring from the `/code-review` skill.

### Step 1.2 — Triage

Filter findings >= 80 confidence. Present as cards. User chooses per-issue: **Fix** / **Ignore** / **Post**.

### Step 1.3 — Fix and Re-verify

If fixes applied:
1. Run relevant tests to confirm no regression
   - Domain fix → `dotnet test tests/InvestmentApp.Domain.Tests`
   - Application fix → `dotnet test tests/InvestmentApp.Application.Tests`
   - Infrastructure fix → `dotnet test tests/InvestmentApp.Infrastructure.Tests`
   - Frontend fix → `ng test` (if spec exists)
2. Significant fix (new logic) → loop back to Step 1.1
3. Minor fix (typo, naming) → proceed

---

## Phase 2: Update Documentation ⭐ (primary focus)

**This is the most important phase of the skill.** Do not skip.

### Step 2.1 — Scan changes

Run `git diff --name-only <base>...HEAD`, then update ALL matching docs:

| What changed | Doc to update |
|---|---|
| Entity, API endpoint, route | `docs/business-domain.md` |
| Feature (new/modified) | `docs/features.md` |
| Service, controller, repository, page, shared component, external integration | `docs/architecture.md` |
| New bug pattern, completed improvement item, UX/architecture decision | `docs/project-context.md` |
| New convention, directive, pipe | `CLAUDE.md` |
| User-facing feature added/changed | Relevant guide in `frontend/src/assets/docs/` |

### Step 2.2 — Archive Plan (if completed)

If this PR completes a plan in `docs/plans/`:
```bash
git mv docs/plans/xxx.md docs/plans/done/xxx.md
```

If the plan still has remaining phases → keep in `docs/plans/`, write a checkpoint.

### Step 2.3 — Update Changelog

Update `frontend/src/assets/CHANGELOG.md`:
- Determine version bump (patch/minor/major)
- Add entry at top with existing format
- Use today's date
- Vietnamese text with full diacritics

### Step 2.4 — Confirm with user

Before committing, summarize all docs updated and ask user whether anything else needs to be added.

---

## Phase 3: Commit & PR

### Step 3.1 — Commit

1. Run `dotnet test` if Phase 1 applied fixes. Otherwise skip.
2. Stage: code + tests + docs + changelog
3. **Commit message: Vietnamese with full diacritics, clear and specific** (e.g., `feat(trade-plan): thêm state machine và matrix editability`). This is the only Vietnamese-required text in this workflow (aside from UI text rules in CLAUDE.md).
4. Commit (do NOT use `--no-verify`)

### Step 3.2 — Rebase + Push + Create PR

Follow the rebase rules from the global `/pr` skill (see [`~/.claude/commands/pr.md`](file:///C:/Users/a/.claude/commands/pr.md)):

1. **Fetch + detect target**:
   - `git fetch origin`
   - If user specified a release branch → use it directly
   - Else → `git branch -r --sort=-committerdate | grep "origin/release" | head -5`, ask user to pick; fallback to `origin/master`
2. **Rebase**: `git rebase origin/<target>`
   - Conflicts → STOP, list conflicting files, do NOT auto-resolve
3. **Push**: `git push --force-with-lease -u origin <current-branch>` (never `--force`)
4. **Check existing PR**: `gh pr list --head <current-branch> --json url,title` — if exists, return URL
5. **Create PR** with ship-style template:

```bash
gh pr create --base <target-branch> --title "<English title, < 70 chars>" --body "$(cat <<'EOF'
## Summary
- What changed and why

## Changes
- Grouped by Backend / Frontend / Docs

## Test plan
- [ ] Backend tests pass (`dotnet test`)
- [ ] Frontend tests pass (if applicable)
- [ ] Manually verified before PR

## Docs updated
- [ ] Which docs were updated

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Return PR URL.

---

## Error Handling

- Phase 1 review finds critical issue (>= 90 confidence) → MUST fix before proceeding
- Tests fail in Phase 3 → stop and fix, do NOT skip with `--no-verify`
- `gh` unavailable → provide command for user to run manually
- On `master`/`main` → create a feature branch first
- Docs out of sync with code → do NOT commit (violates CLAUDE.md rule)