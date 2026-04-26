---
name: ship
description: Full development workflow — analyze, plan, TDD, code review, manual verification, update docs, and create PR. Use when asked to "ship", "ship it", "full workflow", "TDD and PR", or when the user wants to go from implementation to PR with all quality gates. Orchestrates the complete cycle from requirement analysis through creating a pull request.
---

# Ship — Full Development Workflow

Orchestrates: Analyze → Plan → TDD → Code Review → Manual Verify → Docs → Commit & PR.

## Arguments

- `$ARGUMENTS` — Optional: description of the feature/fix. If empty, assumes code is already written and skips to Phase 3.

## Multi-Phase Sessions

When implementing multiple features/phases in one session, use this protocol to avoid re-reading everything and keep context persistent.

### Cycle 1 (first ship in session)

Normal flow — Phase 1 reads all project docs as usual.

### Between cycles

After each ship cycle (commit or PR), write a **checkpoint** to the plan file:

```markdown
### Checkpoint — Phase N (done)
- **Decisions**: key architecture/UX decisions made
- **Files changed**: list of files created/modified
- **Tests**: N tests added, all pass
- **Affected layers**: Domain / Application / Infrastructure / Api / Frontend
- **Next**: what Phase N+1 should do, dependencies from this phase, which files to read
```

The **Next** field is critical — it tells the next cycle exactly where to start and what to read. Be specific: file paths, layer, what to build on.

### Cycle 2+ (subsequent ships in same session)

**Only Phase 1 Step 1.1 changes** — all other phases run as normal:

| Phase | Cycle 2+ behavior |
|---|---|
| Phase 1 Step 1.1 | **Lighter**: read checkpoint only (not all 4 docs) |
| Phase 1 Step 1.2 | Normal: analyze & present plan |
| Phase 2 TDD | Normal (if backend affected) |
| **Phase 3 Code Review** | **ALWAYS runs** — every cycle must be reviewed |
| Phase 4 Manual Verify | Normal (if frontend affected) |
| Phase 5 Docs | Normal |
| Phase 6 Commit & PR | Normal |

**Lighter Phase 1 Step 1.1:**
1. Read the plan file checkpoint (NOT all 4 project docs again — already in context from cycle 1)
2. Read only files listed in checkpoint's "Next" and "Files changed" (to see what was built)
3. If the next phase touches a NEW layer not in previous checkpoint → read only that layer's docs
4. Skip re-reading `features.md`, `architecture.md`, `business-domain.md`, `project-context.md` unless the checkpoint says otherwise

## Model Strategy

| Phase | Execution | Model |
|---|---|---|
| Phase 1: Analyze & Plan | Main context | **opus** (required) |
| Phase 2: TDD | Main context (continues Phase 1) | opus |
| Phase 3: Code Review | 1 sub-agent | **sonnet** |
| Phase 4: Manual Verify | Main context | any |
| Phase 5-6: Docs, Commit & PR | Main context | any |

Phase 2 runs inline because it needs the plan context from Phase 1 — spawning a sub-agent would duplicate that context.

---

## Phase 1: Analyze Requirements & Plan

> Skip if `$ARGUMENTS` is empty or user says code is done.

### Step 1.1 — Read Context

**Cycle 2+ in a multi-phase session?** Follow the lighter flow in "Multi-Phase Sessions" above instead of reading all docs.

**Cycle 1 (or new session):** CLAUDE.md is already in context. Read only these:
1. `docs/architecture.md`
2. `docs/business-domain.md`
3. `docs/project-context.md`
4. `docs/features.md`

Check `docs/plans/` for existing related plans (ignore `docs/plans/done/` — those are completed).

### Step 1.2 — Analyze & Present Plan

1. Parse `$ARGUMENTS`, identify affected layers: Domain / Application / Infrastructure / Api / Frontend
2. If no plan file exists for this work, create one in `docs/plans/` (e.g., `docs/plans/p1-feature-name.md`)
3. Present brief plan for user approval:
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

## Phase 4: Manual Testing & Verification

> Skip if: changes are docs-only or config-only (no runtime behavior changed).

### Step 4.1 — Build Test Scenarios

Based on the changes from Phase 2-3, list concrete test scenarios before testing. Categorize into:

| Category | What to check | Examples |
|---|---|---|
| **Happy path** | Core feature works as designed | Create entity succeeds, list shows new item, edit saves correctly |
| **Edge cases** | Boundary & unusual inputs | Empty string, max length, special characters, zero/negative amounts, duplicate entries |
| **Error handling** | Invalid states & error responses | Missing required fields, invalid IDs, unauthorized access, network errors |
| **State transitions** | Before/after behavior | Status changes, calculated fields update, dependent data cascades |
| **UI/UX** (if frontend) | Visual & interaction correctness | Form validation messages show, loading states, responsive layout, Vietnamese text displays with dấu |

Present the scenario list to the user for confirmation before proceeding.

### Step 4.2 — Start Servers

Start only what's needed based on affected layers:

| Layers affected | Action |
|---|---|
| Backend only (Domain/Application/Infrastructure/Api) | Start API: `dotnet watch run --project "src\InvestmentApp.Api\InvestmentApp.Api.csproj" --urls="http://localhost:5000"` |
| Frontend only | Start Angular: `cd frontend && ng serve` |
| Full stack | Start both (API first, then Angular) |

Wait for servers to be ready before testing.

### Step 4.3 — Execute & Verify

Walk through each scenario from Step 4.1:

**Backend verification:**
- Test API endpoints with `curl` or browser dev tools
- Verify response status codes, body structure, error messages
- Check MongoDB state if data mutations are involved

**Frontend verification:**
- If frontend touches user-data flows → invoke `/qa-verify` skill (mints JWT for stable test user, drives browser via chrome-devtools MCP). Reuse the scenario list from Step 4.1; the skill skips its own "servers running" check since 4.2 already started them.
- For non-user-data UI changes (static pages, public market data) → navigate manually via browser, test each scenario interactively.
- Verify: form validation, loading states, success/error notifications, data display
- Check Vietnamese text renders correctly with dấu

**Regression check:**
- Navigate to pages/features adjacent to the change
- Verify they still work — especially features sharing the same service or data

### Step 4.4 — Report Results

Present results as a table:

```markdown
| # | Scenario | Result | Notes |
|---|---|---|---|
| 1 | Create with valid data | ✅ Pass | — |
| 2 | Create with empty name | ✅ Pass | Shows validation error |
| 3 | Edit existing item | ❌ Fail | 500 error on save |
```

- All pass → proceed to Phase 5
- Any fail → fix the issue, re-run affected tests (`dotnet test`), then re-verify the failed scenario. Do NOT proceed until all scenarios pass.

---

## Phase 5: Update Documentation + Changelog

> Only after code is stable (review done, tests pass, manual verification done).

### Step 5.1 — Update Docs

Run `git diff --name-only` against base branch, then update ALL that match:

| What changed | Doc to update |
|---|---|
| Entity, API endpoint, route | `docs/business-domain.md` |
| Feature (new/modified) | `docs/features.md` |
| Service, controller, repository, page, shared component | `docs/architecture.md` |
| Bug pattern, improvement item, UX/arch decision | `docs/project-context.md` |
| Convention, directive, pipe | `CLAUDE.md` |
| User-facing feature added/changed | Relevant guide in `frontend/src/assets/docs/` |

### Step 5.2 — Archive Plan (if completed)

If this ship cycle completes ALL phases in the plan file:
- Move to `docs/plans/done/`: `git mv docs/plans/xxx.md docs/plans/done/xxx.md`

If plan still has remaining phases → keep in `docs/plans/`, write checkpoint (see Multi-Phase Sessions).

### Step 5.3 — Update Changelog

Update `frontend/src/assets/CHANGELOG.md`:
- Determine version bump (patch/minor/major)
- Add entry at top with existing format
- Include test counts from `dotnet test`
- Use today's date

---

## Phase 6: Commit & PR

### Step 6.1 — Commit

1. Re-run `dotnet test` only if Phase 3/4 applied fixes. Otherwise tests already passed — skip.
2. Stage relevant files (code + tests + docs + changelog)
3. Write clear English commit message
4. Commit

### Step 6.2 — Create PR

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
     - [ ] Manual verification scenarios (from Phase 4)

     ## Docs updated
     - [ ] Which docs were updated

     🤖 Generated with [Claude Code](https://claude.com/claude-code)
     ```
3. Return PR URL

---

## Phase 7: Capture Learnings

After PR creation, reflect on the full cycle (analyze → plan → TDD → review → verify → ship). If a non-obvious, reusable insight surfaced across any phase — a pattern, pitfall, or tool-quirk — persist it following the **Session Learning Capture** convention in [`~/.claude/CLAUDE.md`](file:///C:/Users/a/.claude/CLAUDE.md).

Good candidates from a full ship cycle:
- **pattern**: a design that worked well and would apply to similar features (save as `learning_pattern_*.md`, `type: project`)
- **pitfall**: a Phase 3 review finding or Phase 4 manual-test failure that represents a class of mistake (save as `learning_pitfall_*.md`, `type: feedback`)
- **tool-quirk**: `dotnet test`, `ng serve`, MongoDB driver, or build-tool gotcha that cost time (save as `learning_toolquirk_*.md`, `type: feedback`)

Do NOT re-capture:
- Specific bug fixes already in commit message
- Architecture decisions (those go to `docs/architecture.md` per project rule)
- Anything already covered by existing memory files

If nothing non-obvious surfaced, skip silently.

---

## Error Handling

- Tests fail in Phase 2/6 → stop and fix
- Manual verification fail in Phase 4 → fix, re-run tests, re-verify
- Critical review finding (>= 90 confidence) → must fix, loop back to Phase 2
- `gh` unavailable → provide command for user
- On `master`/`main` → create feature branch first
