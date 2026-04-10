# Output Format Templates

## Chat Presentation Format (for triage step)

Use this card format when presenting findings to the user in chat. Do NOT use a markdown table -- tables render with cramped columns in VS Code chat panels. Use individual cards instead:

```markdown
## Code Review Results

Found {N} high-confidence issues:

---

### Issue 1 of {N}
**File:** `path/to/file.ts`
**Lines:** 67-72
**Category:** guideline
**Confidence:** 95/100
**Description:** Missing error handling for OAuth callback. The copilot-instructions.md says "Always handle OAuth errors" but the new code has no try-catch around the callback handler.
**Suggested fix:** Wrap the callback handler in a try-catch block and log the error.

---

### Issue 2 of {N}
**File:** `path/to/file.ts`
**Lines:** 88-95
**Category:** bug
**Confidence:** 90/100
**Description:** Memory leak -- OAuth state object is allocated but never cleaned up when the flow completes or errors out.
**Suggested fix:** Add a finally block that disposes the state object.

---

For each issue, choose an action:
- **Fix** -- I will apply the suggested code change
- **Ignore** -- Dismiss this issue
- **Post** -- Include in a review comment on the PR

Please tell me your choices, e.g.: "1: Fix, 2: Post, 3: Ignore"
```

When no issues are found:
```
No high-confidence issues found. The PR looks good.
```

---

## PR Comment Format (for "Post" action)

When posting to GitHub via `gh pr comment`, use this exact format:

```markdown
## Code review

Found {N} issues:

1. {Brief description of issue} ({category}: {guideline_ref or context})

   {GitHub permalink to code}

2. {Brief description of issue} ({category}: {context})

   {GitHub permalink to code}

---
Generated with Claude Code
```

Category values: `guideline`, `code-comment`, `bug`, `historical`.

When linking to code, use GitHub permalinks with full commit SHA:

```
https://github.com/{owner}/{repo}/blob/{full-sha}/{path/to/file}#L{start}-L{end}
```

Rules for links:
- Must use the **full 40-character SHA** (not abbreviated)
- Get the SHA via `git rev-parse HEAD` before constructing links
- Must use `#L` notation for line numbers
- Include at least 1 line of context before and after (e.g. if commenting about lines 5-6, link to L4-L7)
- Repo name in the URL must match the repo being reviewed

### Example PR Comment

```markdown
## Code review

Found 2 issues:

1. Missing error handling for OAuth callback (guideline: copilot-instructions.md says "Always handle OAuth errors")

   https://github.com/anthropics/example-repo/blob/abc123def456789012345678901234567890abcd/src/auth.ts#L66-L73

2. Memory leak: OAuth state not cleaned up in finally block (bug detected in diff)

   https://github.com/anthropics/example-repo/blob/abc123def456789012345678901234567890abcd/src/auth.ts#L87-L96

---
Generated with Claude Code
```

### No Issues Variant

If the user chooses to post but all issues were fixed or ignored, do NOT post a comment.

---

## Notes

- Keep comments **brief** -- one sentence per issue max
- **No emojis** in PR comments
- Always cite and link the relevant code
- For guideline issues, quote the specific guideline rule
- For bugs, mention what the bug is and its impact
- For historical issues, reference the relevant commit or PR
