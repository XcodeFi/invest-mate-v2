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

## Adaptive Execution Strategy

Inherits tier (HI/MID/LO) detection, sub-agent briefing rules, and rhythm posture (DAY/EVENING) from global [`~/.claude/CLAUDE.md`](file:///C:/Users/a/.claude/CLAUDE.md) — sections "Sub-agent & Budget Strategy" + "Personal work rhythm". Do NOT duplicate that logic here; just look it up.

### Detect tier at start

Default for `/pr` workflow (since code is already done, no big planning needed):

| Signal | Tier |
|---|---|
| `--budget=hi` / multi-stack diff (FE + BE) / large diff (> 500 LoC) | **HI** |
| `--budget=lo` / docs-only / config-only / conversation already compacted | **LO** |
| Otherwise | **MID** |

State the tier in one line before Phase 1.

### Tier → Phase mapping

| Phase | HI | MID | LO |
|---|---|---|---|
| Phase 1 Review | **Per-stack parallel fanout** (1 sonnet for FE + 1 sonnet for BE, single message 2 Agent calls) when BOTH stacks changed; else 1 unified sonnet | 1 unified sonnet sub-agent | 1 **haiku** sub-agent on highest-risk stack only |
| Phase 2 Docs | **Parallel haiku per-doc fanout** when ≥ 3 docs need updates | Inline serial | Inline, only highest-priority doc; defer rest with TODO |
| Phase 3 Commit & PR | Inline | Inline | Inline |

### Rhythm modifier (overlay on tier)

Per global CLAUDE.md "Personal work rhythm":

- **DAY mode** — surface review findings as cards, ask user Fix/Ignore/Post per issue.
- **EVENING mode** — auto-decide review triage: auto-fix high-confidence (≥ 90), auto-ignore low-confidence (< 60), only surface mid-confidence (60–89) for user. Batch all doc updates into one message.
- **OFF gap (17:25–20:00) / HARD STOP (after 21:30)** — surface 1-line warning before starting; if user confirms, accelerate to PR creation and defer optional doc polish to tomorrow.
- **20:00–21:25 evening side-project window (85 min only)** — start `/pr` only if expected total time < 60 min; else suggest splitting Phase 2 (docs) to tomorrow.

Phase 3 (commit + PR) always runs inline — it's small, sequential, can't parallelize meaningfully.

---

## Phase 1: Code Review (self-review)

Sub-agent count, model, and parallelism depend on tier — see Adaptive Execution Strategy table.

### Step 1.0 — Secret Scan (HARD GATE)

Run the credential/URL scan from [`/code-review` references/secret-scan.md](../code-review/references/secret-scan.md) against `git diff <base>...HEAD`. Match → STOP, surface findings, do not run the review sub-agent and do not proceed to commit. Resume only after user removes (and rotates if needed) the secret.

### Step 1.1 — Run Review

1. Get diff against base branch (`git diff <base>...HEAD --name-only` and `git diff <base>...HEAD`).
2. Detect affected stacks from changed files: frontend (Angular 19), backend (.NET 9), data (MongoDB).
3. **Dispatch per tier:**
   - **HI + both stacks changed** → emit ONE message with 2 parallel sonnet Agent calls (1 FE, 1 BE). Each capped at "Report in under 400 words; structured table {file:line | issue | severity | confidence}." Merge findings in main context.
   - **HI + single stack OR MID** → 1 unified sonnet sub-agent covering all affected stacks. Cap at "Report in under 600 words."
   - **LO** → 1 **haiku** sub-agent on the highest-risk stack only (skip stack with < 30 changed lines or purely cosmetic diff). Cap at "Report in under 400 words."
4. Use the same checklist and scoring from the `/code-review` skill.

### Step 1.2 — Triage

Filter findings >= 80 confidence. Present as cards. User chooses per-issue: **Fix** / **Ignore** / **Post**.

**Rhythm modifier (EVENING mode):** auto-triage instead of asking — auto-fix ≥ 90 confidence, auto-ignore < 60, only surface 60–89 for user. Saves 5+ user prompts during evening wrap-up.

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

**Tier-specific dispatch:**

- **HI + ≥ 3 docs need updates** → fan out 1 **haiku** sub-agent per doc in **parallel** (single message, N Agent calls). Brief each with: doc path, one-line "what to add" derived from diff, "Report in under 200 words: the diff applied." Verify each diff before commit. Skip fanout if updates < 5 lines per doc — inline is faster.
- **MID** → update inline, sequentially.
- **LO** → only update the highest-priority doc (architecture.md for structural changes, business-domain.md for entity/API changes); add TODO comment in commit message for deferred docs.

**Rhythm modifier (EVENING mode):** batch all doc updates into ONE assistant message instead of asking user "which doc next?" after each. Saves 3+ turns.

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
3. **Re-run secret scan** on staged diff (`git diff --cached`) per [`/code-review` references/secret-scan.md](../code-review/references/secret-scan.md). Match → STOP, unstage, do not commit.
4. **Commit message: Vietnamese with full diacritics, clear and specific** (e.g., `feat(trade-plan): thêm state machine và matrix editability`). This is the only Vietnamese-required text in this workflow (aside from UI text rules in CLAUDE.md).
5. Commit (do NOT use `--no-verify`)

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

## Phase 4: Capture Learnings

After PR is created (or immediately after Step 3.2 if PR already existed), reflect on this workflow in one pass. If a non-obvious, reusable insight surfaced — a pattern, pitfall, or tool-quirk — persist it following the **Session Learning Capture** convention in [`~/.claude/CLAUDE.md`](file:///C:/Users/a/.claude/CLAUDE.md).

Do NOT re-capture:
- Specific bug fixes already in commit message
- Architecture decisions (those go to `docs/architecture.md` per project rule)
- Anything already covered by existing memory files

If nothing non-obvious surfaced, skip silently — do not write empty entries.

---

## Error Handling

- Phase 1 review finds critical issue (>= 90 confidence) → MUST fix before proceeding
- Tests fail in Phase 3 → stop and fix, do NOT skip with `--no-verify`
- `gh` unavailable → provide command for user to run manually
- On `master`/`main` → create a feature branch first
- Docs out of sync with code → do NOT commit (violates CLAUDE.md rule)