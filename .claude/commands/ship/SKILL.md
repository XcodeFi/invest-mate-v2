---
name: ship
description: Full development workflow — analyze, plan, TDD, code review, update docs, and create PR. Use when asked to "ship", "ship it", "full workflow", "TDD and PR", or when the user wants to go from implementation to PR with all quality gates. Orchestrates the complete cycle from requirement analysis through creating a pull request.
---

# Ship — Full Development Workflow

Orchestrates: Analyze → Plan → TDD → Code Review → Docs → Commit & PR.

## Arguments

- `$ARGUMENTS` — Optional: description of the feature/fix. If empty, assumes code is already written and skips to Phase 3.

## Model Strategy

| Phase | Execution | Model |
|---|---|---|
| Phase 1: Analyze & Plan | Main context | **opus** (required) |
| Phase 2: TDD | Main context (continues Phase 1) | opus |
| Phase 3: Code Review | 1 sub-agent | **sonnet** |
| Phase 4-5: Docs, Commit & PR | Main context | any |

Phase 2 runs inline because it needs the plan context from Phase 1 — spawning a sub-agent would duplicate that context.

---

## Phase 1: Analyze Requirements & Plan

> Skip if `$ARGUMENTS` is empty or user says code is done.

### Step 1.1 — Read Project Docs

CLAUDE.md is already in context. Read only these (in order):
1. `docs/architecture.md`
2. `docs/business-domain.md`
3. `docs/project-context.md`
4. `docs/features.md`

Check `docs/plans/` for existing related plans.

### Step 1.2 — Analyze & Present Plan

1. Parse `$ARGUMENTS`, identify affected layers: Domain / Application / Infrastructure / Api / Frontend
2. Present brief plan for user approval:
   - **What**: Summary of changes
   - **Where**: Files to create/modify (by layer)
   - **Tests**: What tests and where
   - **Risks**: Potential issues
3. Wait for user confirmation.

---

## Phase 2: TDD — Red → Green → Refactor

> Skip if: `$ARGUMENTS` is empty, user says code is done, OR changes are frontend-only. TDD only applies when backend layers are affected (Domain / Application / Infrastructure / Api).

### Step 2.1 — Red: Write Failing Tests

Place tests correctly:
- Domain → `tests/InvestmentApp.Domain.Tests/`
- Application → `tests/InvestmentApp.Application.Tests/`
- Infrastructure → `tests/InvestmentApp.Infrastructure.Tests/`
- Frontend → `.spec.ts` alongside the file

Run to confirm FAIL.

### Step 2.2 — Green: Minimum Code

Write minimum code to pass all tests (new + existing).

### Step 2.3 — Refactor (if needed)

Clean up only obvious duplication or unclear naming. Run tests again.

---

## Phase 3: Code Review (self-review)

Uses **1 sub-agent** (`model: "sonnet"`) for unified static review. Historical context agent is skipped — no value for fresh code.

### Step 3.1 — Run Review

Get diff against base branch. Detect affected stacks from changed files (frontend → Angular 19, backend → .NET 9, data access → MongoDB). Launch 1 sonnet agent covering guidelines, bugs, security, performance — but only check patterns for the affected stacks. Use the same checklist and scoring from the `/code-review` skill.

### Step 3.2 — Triage

Filter to >= 80 confidence. Present findings as cards. User chooses per-issue: **Fix** / **Ignore** / **Post**.

### Step 3.3 — Fix and Re-verify

If fixes applied:
1. Run tests to confirm no regression
2. Significant fixes (new logic) → loop back to Step 3.1
3. Minor fixes (typo, naming) → proceed

---

## Phase 4: Update Documentation + Changelog

> Only after code is stable (review done, tests pass).

### Step 4.1 — Update Docs

Run `git diff --name-only` against base branch, then update ALL that match:

| What changed | Doc to update |
|---|---|
| Entity, API endpoint, route | `docs/business-domain.md` |
| Feature (new/modified) | `docs/features.md` |
| Service, controller, repository, page, shared component | `docs/architecture.md` |
| Bug pattern, improvement item, UX/arch decision | `docs/project-context.md` |
| Convention, directive, pipe | `CLAUDE.md` |

### Step 4.2 — Update Changelog

Update `frontend/src/assets/CHANGELOG.md`:
- Determine version bump (patch/minor/major)
- Add entry at top with existing format
- Include test counts from `dotnet test`
- Use today's date

---

## Phase 5: Commit & PR

### Step 5.1 — Commit

1. Re-run `dotnet test` only if Phase 3 applied fixes. Otherwise tests already passed — skip.
2. Stage relevant files (code + tests + docs + changelog)
3. Write clear English commit message
4. Commit

### Step 5.2 — Create PR

1. Push: `git push -u origin HEAD`
2. Create PR with `gh pr create`:
   - **Title**: Under 70 chars, English
   - **Body**:
     ```markdown
     ## Summary
     - What changed and why

     ## Changes
     - Grouped by Backend / Frontend / Docs

     ## Test plan
     - [ ] Backend tests pass (`dotnet test`)
     - [ ] Frontend tests pass (if applicable)
     - [ ] Manual testing steps if relevant

     ## Docs updated
     - [ ] Which docs were updated

     🤖 Generated with [Claude Code](https://claude.com/claude-code)
     ```
3. Return PR URL

---

## Error Handling

- Tests fail in Phase 2/5 → stop and fix
- Critical review finding (>= 90 confidence) → must fix, loop back to Phase 2
- `gh` unavailable → provide command for user
- On `master`/`main` → create feature branch first
