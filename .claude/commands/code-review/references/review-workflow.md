# Code Review Workflow — Detailed Procedure

## Phase 1: Gather Context (inline, no sub-agents)

### Step 1.1 — Get PR Details

Retrieve the active pull request:
- PR number, title, author, state
- Full diff of all changed files
- List of changed file paths

If no PR found, ask the user for the PR number or URL.

**Diff size guard** (>8,000 lines):
1. Sort files by diff size (largest first), accumulate to 8K lines, drop rest
2. Append: `[TRUNCATED: {N} files with {M} lines omitted]`
3. Keep ALL file paths in `changed_files`

If >30 changed files, warn user and focus on largest.

### Step 1.2 — Eligibility Check

Skip review if: PR is closed/merged, draft, trivial (only .md/.txt/.json/lock files or bot author), or already reviewed (search for "Generated with Claude Code" in PR comments).

### Step 1.3 — Gather Guidelines

Read `**/.github/copilot-instructions.md`, `**/CLAUDE.md` in repo root and modified directories. Concatenate with file path prefixes.

### Step 1.4 — Detect Affected Stacks

Classify changed files to determine which tech-stack checklists to include:

- **Angular 19**: any `.ts`, `.html` file under `frontend/`
- **.NET 9**: any `.cs` file under `src/` or `tests/`
- **MongoDB**: any `.cs` file in repository/data-access layers (files containing `IMongoCollection`, `FilterDefinition`, `Repository`)

Only include the relevant checklist sections in agent prompts. If a PR only touches frontend files, skip .NET 9 and MongoDB checklists entirely (and vice versa).

### Step 1.5 — Prepare Agent Inputs

Assemble: **diff**, **changed_files**, **guidelines**, **affected_stacks**, **pr_info** (number, title, owner, repo, base, head).

---

## Phase 2: Parallel Review (2 sub-agents)

Launch both agents in parallel. Both use `model: "sonnet"`.

### Agent 1 Prompt: Unified Static Review

```
You are a code review agent. Perform a SINGLE-PASS review of the PR diff covering
ALL of these categories: guidelines, bugs, security, performance.

PROJECT GUIDELINES:
{guidelines}

PR DIFF:
{diff}

CHANGED FILES:
{changed_files}

EFFICIENCY RULES:
- Do NOT use any tools. Work entirely from the diff and guidelines above.
- Max 15 issues total across all categories. Keep top 15 by confidence.
- One sentence per description.

REVIEW CHECKLIST — check ALL categories in one pass, but ONLY for the affected stacks listed below:

AFFECTED STACKS: {affected_stacks}
(Only check patterns for stacks listed above. Ignore checklists for stacks not affected.)

### 1. GUIDELINE COMPLIANCE
- Check changes against project guidelines above
- Check code comments (TODO, HACK, FIXME, NOTE) for violated contracts
- Only flag if guideline EXPLICITLY mentions the concern

### 2. BUG DETECTION
General: null refs, off-by-one, race conditions, logic errors, resource leaks, missing error handling
[Angular 19 — only if frontend affected]: deprecated *ngIf/*ngFor (use @if/@for), @for missing track,
  signal misuse (.set()/.update()), subscription leaks (need takeUntilDestroyed/async pipe),
  separate .html files (must use inline template), Vietnamese text missing diacritics,
  symbol inputs not using appUppercase directive
[.NET 9 — only if backend affected]: missing await on async, missing CancellationToken,
  null after repository find, CQRS violations, Clean Architecture layer violations,
  missing .ToUpper().Trim() for symbols
[MongoDB — only if data access affected]: ObjectId string comparison, string-concatenated filters,
  ReplaceOne misuse, wrong filter field names

### 3. SECURITY
General: injection, auth/authz gaps, hardcoded secrets, input validation, data exposure
[Angular 19]: bypassSecurityTrustHtml, raw innerHTML via DOM, sensitive data in templates,
  manual token handling (use interceptor), missing route guards
[.NET 9]: missing [Authorize], IDOR (no ownership check), unvalidated inputs,
  logging sensitive data, error responses leaking internals
[MongoDB]: NoSQL injection via string concat, sensitive fields in projections, client-set audit fields

### 4. PERFORMANCE
General: O(n²) in hot paths, redundant iterations, large allocations in loops
[Angular 19]: missing OnPush, heavy template computation, @for with track $index,
  subscription leaks, large imports, missing lazy loading
[.NET 9]: sync-over-async (.Result/.Wait()), missing CancellationToken propagation,
  missing using/IDisposable, string concat in loops
[MongoDB]: N+1 queries (use Filter.In), missing .Project(), unbounded queries (no Limit),
  missing indexes for new filters, ReplaceOne for partial updates, sort without index
[API]: unbounded collections, overfetching, missing caching

SCORING RUBRIC:
- 0: False positive, pre-existing, or linter-catchable
- 25: Might be real but unverified
- 50: Real but minor/rare in practice
- 75: Verified, important, directly impacts functionality or explicitly in guidelines
- 100: Confirmed, frequent, direct evidence

FALSE POSITIVES TO AVOID:
- Pre-existing issues not introduced in this PR
- Issues linters/type-checkers/compilers would catch
- Pedantic nitpicks a senior engineer would not flag
- Intentional changes directly related to PR purpose
- Issues on unmodified lines
- Style-only issues unless mandated in guidelines
- Framework-mitigated issues
- Micro-optimizations with negligible impact
- Test code or dev-only configuration

RETURN FORMAT (numbered list, one per issue):
- file: relative path
- lines: start-end
- category: "guideline" | "bug" | "security" | "performance"
- confidence: 0-100
- description: one sentence
- suggested_fix: brief fix description
- severity: "critical" | "high" | "medium" | "low" (for security/performance)

If no issues found, return: "No issues found."
```

### Agent 2 Prompt: Historical Context

```
You are a code review agent focused on historical context.

TASK: Check git history of modified files for regressions or recurring issues.

CHANGED FILES:
{changed_files}

PR DIFF:
{diff}

EFFICIENCY RULES:
- Max 10 files. If more changed, pick the 10 with largest diffs.
- Max 2 commands per file: git blame -L <range> <file>, git log --oneline -10 -- <file>
- Do NOT use GitHub API. Git log only.
- Max 10 issues. If none found quickly, return "No historical context issues found."

INSTRUCTIONS:
1. For each file (up to 10), run git blame on modified regions
2. Check git log for recent commits
3. Identify: reverted fixes, recurring bugs, recently-fixed code being re-broken
4. Score 0-100 for confidence

SCORING:
- 0: Speculative or pre-existing
- 25: Might be relevant
- 50: Pattern exists but unclear if triggered
- 75: Strong historical evidence
- 100: Previous fix directly undone or known recurring problem reintroduced

RETURN FORMAT (numbered list):
- file: relative path
- lines: start-end
- category: "historical"
- confidence: 0-100
- description: one sentence referencing historical context
- historical_ref: commit SHA or context
- suggested_fix: brief fix or "N/A"

If none found, return: "No historical context issues found."
```

---

## Phase 3: Filter and Deduplicate

1. **Parse** both agents' responses into structured issues
2. **Deduplicate**: Same file + overlapping lines + similar description → keep higher confidence
3. **Filter**: Remove confidence < 80
4. **Sort**: By confidence desc, then file path

If nothing remains: "No high-confidence issues found. The PR looks good."

---

## Phase 4: Interactive Triage

### Step 4.1 — Present Findings

Display each issue as a card (NOT tables):

```
## Code Review Results

Found {N} high-confidence issues:

---

### Issue 1 of {N}
**File:** `path/to/file.ts`
**Lines:** 67-72
**Category:** bug
**Confidence:** 95/100
**Description:** Brief description.
**Suggested fix:** What to change.

---

For each issue, choose: **Fix** / **Ignore** / **Post**
Example: "1: Fix, 2: Post, 3: Ignore"
```

### Step 4.2 — Execute Choices

- **Fix**: Read file, apply suggested fix via Edit tool, confirm
- **Ignore**: Skip (optionally acknowledge)
- **Post**: Batch all into one `gh pr comment` per [output-format.md](output-format.md)

Execute Fix first, then Post batch, then acknowledge Ignore.

Record the SHA before applying any fix (`git rev-parse HEAD`) — Step 4.3 needs it.

---

## Step 4.3 — Re-review the fixes (HARD GATE when code changed)

**The fix lines are the least-reviewed code in the whole change.** They were written last, by the same context that just missed the original bug, and **no reviewer has ever seen them**. The original review looked at the OLD content of those files. Shipping straight after applying fixes means the newest and least-scrutinised lines go out unexamined.

### When it is mandatory

Re-review is **required** if ANY applied fix touched one of these. Match on what the fix *did*, not on how many lines it took:

| The fix touched | Example |
|---|---|
| A conditional, guard, or predicate | adding `&& !isDirty()` to an enable check |
| A new function, or logic extracted into one | introducing a shared `serialize()` helper |
| A data transform, parse, or serialization | changing which fields a snapshot stores |
| State lifecycle — where a flag is set, reset, or cleared | a flag set on save but never reset |
| Anything auth, ownership, permission, or money | an ownership check, a rounding rule |
| An error path or fallback branch | which message a failure surfaces |

**Skip only when every applied fix is text-level:** a comment, a user-facing string, a rename with no call-site change, formatting, or import order. If even one fix falls in the table above, the whole round gets re-reviewed.

### How to run it

1. `git diff <sha-recorded-in-4.2>..HEAD` — the fix diff only, not the whole PR.
2. Launch a **fresh** sonnet sub-agent. Do not continue the first reviewer — it is anchored on its own suggestions and will grade its own homework.
3. State explicitly in the prompt: *"These lines were written in response to a code review and have never themselves been reviewed. The prior findings and their intended fixes are listed below — verify each fix is actually correct and complete rather than accepting it."*
4. List each original finding and the fix that was applied, so the reviewer can check the fix against the defect instead of re-deriving it.
5. Re-run the test suite. A fix that changes behaviour needs a test that would **fail without it** — if you cannot state which test that is, the fix is unpinned.

### Loop limit

At most **2** re-review rounds. If a third round still surfaces HIGH or BLOCKER findings, stop and surface to the user — that many rounds means the design is wrong, not the code.

### Red flags — these thoughts mean you are rationalizing

| Thought | Reality |
|---|---|
| "The fixes are small" | Size is not the risk. Being unreviewed is the risk. |
| "I just wrote them, I know they're right" | That is the same context that missed the original bug. |
| "Tests pass" | Written by the same context, in the same pass, from the same wrong model. |
| "The reviewer already looked at this file" | It looked at the file's OLD content. Your lines did not exist yet. |
| "It's a one-line change" | A guard, a reset, and an off-by-one are all one line. |
| "I'll just verify it manually instead" | Manual verify checks the path you thought of — the same blind spot. |
