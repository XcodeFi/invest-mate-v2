---
name: qa-verify
description: End-to-end browser verification using MintStableJwt for stable test user. Use when (a) implementing/changed a frontend feature that depends on user data, (b) `/ship` Phase 4 needs browser verification, (c) explicit "qa verify", "test trên browser", "verify on browser" requests. Skip for docs-only or backend-only changes.
---

# QA Verify — Browser Verification with Stable Test User

Drives a logged-in browser session against dev or prod using a JWT minted from `MintStableJwt` for the hardcoded test user (`investmate.support@gmail.com`). Bypasses Google OAuth (which blocks AI-driven browsers) by injecting JWT directly into `localStorage`.

## When to use

✅ Use when:
- Frontend changes touch user-data flows (trades, plans, savings, settings, dashboard, journal)
- Need to verify a flow end-to-end before shipping
- `/ship` Phase 4 calls for browser verification
- Cross-session data persistence matters (same `user_id` across mints)

❌ Skip when:
- Docs-only or config-only changes
- Backend-only changes (use `dotnet test` alone)
- Feature requires Admin role (test user is regular `User`)
- Need to verify the Google OAuth flow itself (this skill bypasses it)

## Prerequisites

The test user (`phdfieldkidpro@gmail.com`) must already exist in the target DB. If absent, prompt the user to log in once via Google with that email on the target environment to seed the user record.

Currently allowlisted in [tests/InvestmentApp.Infrastructure.Tests/Tools/MintStableJwtTests.cs](tests/InvestmentApp.Infrastructure.Tests/Tools/MintStableJwtTests.cs) `StableJwtMint.ALLOWED_EMAILS`:
- `investmate.support@gmail.com` — needs to be seeded by logging in via Google once on dev + prod before mint will succeed. Until seeded, Step 2 will fail with "User not found in database".

To allowlist a new email: edit the array, follow TDD (add test for the new email first), submit a PR.

## Workflow

### Step 1 — Confirm scope

**Before asking the user**, glob `scratch/qa-reports/qa-verify-<feature-slug>-*.md` for the feature being verified. If a prior report exists, read the latest one (lexicographic sort = chronological because timestamps in filename) and use it as your **starting point**:

- **Reuse the scenario list verbatim** as your default — the user explicitly approved that scope last time. Add new scenarios only if the feature has changed since.
- **Re-check every "Findings (non-blocking)" entry** — confirm it still reproduces, has been fixed, or has shifted. Each finding from the prior report becomes an explicit scenario in this run.
- **Skip cleanup IDs from prior report** — those plans/trades were already deleted; don't try to query them.

Then confirm with the user:
1. **Target environment** — `dev` (default) or `prod`?
2. **Test scenarios** — show the prior scenario list (if any), summarise prior findings, ask if scope should change.
3. **Servers running?** — dev needs API on `5000` + Angular on `4200`. Prod needs the prod URL.

If no prior report exists, proceed as usual without this context.

### Step 2 — Mint JWT

Mint a 30-day JWT against the target environment.

**Dev:**

Read [src/InvestmentApp.Api/appsettings.Development.json](src/InvestmentApp.Api/appsettings.Development.json) for `ConnectionStrings.MongoDb`, `MongoDb.DatabaseName`, `Jwt.Key`, `Jwt.Issuer`, `Jwt.Audience`, then:

```bash
MINT_EMAIL="phdfieldkidpro@gmail.com" \
MINT_MONGO_CONN="<from appsettings.Development.json>" \
MINT_MONGO_DB="<from appsettings.Development.json>" \
MINT_JWT_KEY="<from appsettings.Development.json>" \
MINT_JWT_ISSUER="<from appsettings.Development.json>" \
MINT_JWT_AUDIENCE="<from appsettings.Development.json>" \
dotnet test tests/InvestmentApp.Infrastructure.Tests/InvestmentApp.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~MintStableJwt" \
  --logger "console;verbosity=detailed" 2>&1 | grep -E "^[[:space:]]*(eyJ|sub=|valid until)"
```

Capture the JWT (line starting with `eyJ`) and `sub` value.

**Prod:**

Same command but:
- `MINT_MONGO_DB="InvestmentApp_prod"`
- `MINT_JWT_KEY` **must come from Google Secret Manager** — `appsettings.Production.json` has placeholder/dev values, not actual prod values:
  ```bash
  gcloud secrets versions access latest --secret=jwt-key
  ```
- `MINT_JWT_ISSUER` and `MINT_JWT_AUDIENCE` from prod Cloud Run env vars (or Secret Manager)

If `gcloud` not configured or user lacks `secretmanager.secretAccessor`: ask user to share the prod JWT key (don't write to file/memory — use once, forget after session).

### Step 3 — Inject JWT into browser

Read [frontend/src/app/core/services/auth.service.ts](frontend/src/app/core/services/auth.service.ts) once to confirm localStorage keys (currently `auth_token` and `auth_user`). If keys ever change, update this skill.

Open Chrome via `mcp__chrome-devtools__new_page` to target URL (dev: `http://localhost:4200`, prod: prod URL). Then `evaluate_script`:

```javascript
() => {
  const jwt = "<minted JWT>";
  const user = {
    id: "<sub from JWT>",
    email: "investmate.support@gmail.com",
    name: "no reply invest-mate",
    provider: "google",
    createdAt: "<from DB CreatedAt — query if needed>"
  };
  localStorage.setItem("auth_token", jwt);
  localStorage.setItem("auth_user", JSON.stringify(user));
  return localStorage.getItem("auth_token")?.length;
}
```

Then `navigate_page` to `/dashboard` (or feature page). `wait_for` known logged-in text (e.g., user's name in header) — confirms JWT validates server-side.

If `/dashboard` redirects back to `/auth/login`: JWT signature mismatch (wrong signing key for target env) — re-mint with correct key.

### Step 4 — Drive scenarios

For each scenario from the user's test plan:
- `take_snapshot` — see current state and get element `uid`s
- `click`, `fill`, `fill_form`, `press_key`, `hover` — interact
- `evaluate_script` — assert DOM/data state, check API responses
- `take_screenshot` — visual evidence (save under `scratch/qa-verify-<scenario>.png`, gitignored)
- `list_console_messages` — catch JS errors

**Regression sweep:** after happy paths, navigate to 2-3 adjacent pages (dashboard, watchlist, trades, plans) to confirm no 401s, no broken layouts. Test JWT shouldn't break unrelated features.

### Step 5 — Report

Present results as table (same format as `/ship` Phase 4.4):

```markdown
| # | Scenario | Result | Notes |
|---|---|---|---|
| 1 | Create trade with valid data | ✅ Pass | id=abc123 |
| 2 | Edit trade — rename symbol | ❌ Fail | 500 on save, see screenshot |
```

If any **fail** → flag it, do NOT proceed to commit/PR. Loop back to fix.

### Step 6 — Persist report (mandatory) + retention sweep

After printing the table, **always** save it as a Markdown file under `scratch/qa-reports/qa-verify-<feature-slug>-<UTC-timestamp>.md`. The directory is gitignored (see `.gitignore`). Create the directory if it doesn't exist.

Filename format: `qa-verify-<feature-slug>-YYYYMMDD-HHMMz.md` (UTC, e.g. `qa-verify-vin-discipline-20260426-0410z.md`).

File content must include, in this order:
1. **Header** — feature, environment (dev/prod), test user email + JWT `sub`, timestamp (UTC ISO-8601), tester (Claude session).
2. **Scenario table** — exactly the same table printed to chat.
3. **Findings (non-blocking)** — bullet list of doc/code drifts, minor inconsistencies, pre-existing issues unrelated to feature. **Mark each as new / unchanged-from-prior / fixed-since-prior** when a prior report existed, so the next verifier can see trajectory.
4. **Cleanup** — list of created entity IDs and their disposition (deleted / left in place + why).
5. **Screenshots referenced** — relative paths to any screenshots saved.

Do not include the JWT itself in the report. The `sub` claim is fine — it's a stable user identifier.

**Retention sweep (immediately after writing):** the latest report is the only one that matters as context for the next verify. After saving the new file, glob `scratch/qa-reports/qa-verify-<feature-slug>-*.md` and **delete all matches except the file you just wrote**. This keeps exactly one report per feature-slug — no stale history to confuse the next run. Do not touch reports for other feature-slugs.

Confirm the new report path + count of old reports deleted back to the user.

## Limits & Gotchas

- **Test user role is regular `User`** — admin-only flows (impersonate, admin overview) won't work. Tell user to manually verify those.
- **Test user data is shared across all AI sessions** — creating/deleting data persists for future verifies. Don't pollute with bulk creates; clean up after destructive tests if possible.
- **Google OAuth flow itself cannot be tested** — this skill bypasses login.
- **Token lifetime: 30 days.** If `/auth/me` returns 401 → re-mint.
- **Prod JWT key MUST come from Secret Manager** — `appsettings.Production.json` is gitignored and may have placeholder/dev values. Per [feedback_config_placeholder_convention.md](C:/Users/a/.claude/projects/d--invest-mate-v2-project/memory/feedback_config_placeholder_convention.md): real prod secrets live in Secret Manager and are mounted via Cloud Run env vars.
- **Do not log JWTs to files** — they're valid credentials. Console output OK (transient), file persistence not OK.

## Integration with `/ship`

When invoked from `/ship` Phase 4.3:
1. Phase 4.1 already produced the scenario list — reuse it.
2. Phase 4.2 already started servers — skip Step 1's "servers running?" check.
3. Run Steps 2-5 above.
4. Phase 4.4 in `/ship` consumes this skill's report table.

If the change is backend-only or docs-only, `/ship` skips Phase 4 entirely — this skill is not invoked.

## TDD note

This skill is configuration, not code — no test for the markdown file itself. When modifying the underlying `StableJwtMint` helper, follow project TDD rule: add failing test in [tests/InvestmentApp.Infrastructure.Tests/Tools/MintStableJwtTests.cs](tests/InvestmentApp.Infrastructure.Tests/Tools/MintStableJwtTests.cs) first, then implement.