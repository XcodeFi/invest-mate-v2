---
name: code-review
description: Automated code review for pull requests with confidence-based scoring and interactive triage. Use when asked to "code review", "review PR", "review pull request", "review my changes", "check PR for bugs", "review this PR", "review the pull request", or any request to analyze a pull request for issues.
---

# Code Review

PR code review using **2 sub-agents** with confidence scoring and interactive triage.

## Model Strategy

Both sub-agents use **sonnet**. Main context verifies critical findings (confidence >= 90) before applying fixes.

## Workflow

1. **Gather context** (inline) — PR state, diff (cap 8K lines), changed files, guidelines
2. **Parallel review** (2 sub-agents, sonnet) — unified static + historical
3. **Filter** — deduplicate, remove < 80 confidence
4. **Verify** — main context re-checks findings >= 90
5. **Triage** — user chooses per-issue: Fix / Ignore / Post

**Timeout prevention**: Diff truncated to 8,000 lines. Agent 1 uses NO tools. Agent 2 caps git ops to 10 files × 2 commands each. 30+ changed files → warn user, focus on largest.

See [references/review-workflow.md](references/review-workflow.md) for detailed procedure.

## Quick Start

1. Get PR details (number, state, diff, changed files)
2. If PR is closed/draft/trivial/already reviewed → stop and report why
3. Read `copilot-instructions.md` and `CLAUDE.md` guidelines
4. Launch 2 sub-agents in parallel (`model: "sonnet"`)
5. Merge, deduplicate, filter < 80 confidence
6. Verify critical findings (>= 90) in main context
7. Present issues, triage with user
8. Execute: fix / ignore / post

## Agent Roles

### Agent 1: Unified Static Review (no tools)

Single-pass analysis covering: guidelines, bugs, security, performance.

**Stack-scoped**: Only check patterns for affected stacks (detected from changed files in Phase 1):
- Frontend files (`frontend/**/*.ts`) → Angular 19 patterns
- Backend files (`src/**/*.cs`) → .NET 9 patterns
- Data access files (repository/filter code) → MongoDB patterns

Tech-stack checklists: see [references/tech-stack-standards.md](references/tech-stack-standards.md).
Max 15 issues, sorted by confidence.

### Agent 2: Historical Context (uses git tools)

- Git blame + log on modified regions (max 10 files, 2 commands per file)
- Identify regressions, reverted fixes, recurring problem patterns
- Max 10 issues

## Confidence & Triage

Scoring: see [references/confidence-scoring.md](references/confidence-scoring.md). Threshold: **80**.

Present each finding as a card (NOT tables):

```
### Issue 1 of N
**File:** path/to/file.ts
**Lines:** 67-72
**Category:** guideline | bug | security | performance | historical
**Confidence:** 95/100
**Description:** Brief description
**Suggested fix:** What to change
```

User classifies each: **Fix** / **Ignore** / **Post**.

- **Fix**: Edit the file
- **Ignore**: Skip
- **Post**: Batch into `gh pr comment` per [references/output-format.md](references/output-format.md)

## Capture Learnings (after triage)

Once triage completes, reflect on the review findings. If any finding represents a **recurring class of mistake** (not a one-off typo) or an **unexpected tooling/stack quirk**, persist it following the **Session Learning Capture** convention in [`~/.claude/CLAUDE.md`](file:///C:/Users/a/.claude/CLAUDE.md).

Trigger examples:
- Same pitfall found in multiple files → save as `learning_pitfall_<slug>.md` (`type: feedback`)
- Agent 2's historical context shows a pattern reverted twice → save as pitfall
- A confident >= 90 finding pointed to a tool-chain gotcha (Mongo driver, Angular Signals, xUnit) → save as `learning_toolquirk_<slug>.md`

Do NOT capture specific bug fixes from this PR — git log + commit message already cover those. If nothing non-obvious surfaced across the review, skip silently.
