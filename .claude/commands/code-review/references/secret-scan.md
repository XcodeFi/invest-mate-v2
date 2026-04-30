# Secret / Credential Scan — HARD GATE

Run BEFORE the review sub-agents and AGAIN before any `git commit`. If any pattern matches, **STOP** the workflow. Do not proceed. Do not bypass with `--no-verify`.

## Patterns to flag

Run these greps against the staged diff (`git diff --cached` if staged, else `git diff <base>...HEAD`):

| Category | Pattern (regex, case-insensitive where applicable) |
|---|---|
| AWS access key | `AKIA[0-9A-Z]{16}` |
| AWS secret | `aws_secret_access_key\s*=\s*[A-Za-z0-9/+=]{40}` |
| Google API key | `AIza[0-9A-Za-z\-_]{35}` |
| Google OAuth token | `ya29\.[0-9A-Za-z\-_]+` |
| OpenAI/Anthropic key | `sk-[A-Za-z0-9_-]{20,}`, `sk-ant-[A-Za-z0-9_-]{20,}` |
| GitHub token | `gh[pousr]_[A-Za-z0-9]{36,}` |
| Slack token | `xox[baprs]-[A-Za-z0-9-]{10,}` |
| JWT (real, 3-segment) | `eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}` — exclude known test fixtures |
| MongoDB URI w/ creds | `mongodb(\+srv)?://[^:\s]+:[^@\s]+@` |
| SQL connection string | `(User\s*Id\|UID)\s*=[^;]+;\s*(Password\|PWD)\s*=` |
| Private key block | `-----BEGIN [A-Z ]*PRIVATE KEY-----` |
| Generic secret-near-name | `(api[_-]?key\|secret\|token\|password)\s*[:=]\s*["'`]?[A-Za-z0-9_\-]{16,}` (excluding placeholders like `{...}`, `<...>`, `***`, `xxx`) |

## File-level red flags

Stop if any of these are STAGED for commit:

- `.env`, `.env.production`, `.env.local`
- `appsettings.Production.json`, `appsettings.Development.json` with non-placeholder values
- `secrets.json`, `serviceaccount*.json`, `*-credentials.json`
- `*.pem`, `*.key`, `*.pfx`, `*.p12`, `id_rsa*`

## Real-URL red flags (project-specific)

The project rule (memory `feedback_config_placeholder_convention.md`) requires `appsettings*.json` to use `{Section__Key}` placeholders. Flag any commit that:

- Replaces a `{...}` placeholder with a literal URL/key in `appsettings*.json`
- Hardcodes a Cloud Run URL with project ID (e.g., `https://*-<project-id>.run.app`) in source code
- Hardcodes a Mongo Atlas cluster host (`*.mongodb.net`) outside config files

## What to do on a match

1. **STOP** — do not proceed with review sub-agents or commit
2. Show the user a card per match:
   ```
   File: <path>:<line>
   Category: <category from table above>
   Match: <redacted: first 4 + last 4 chars only for tokens>
   ```
3. Tell the user:
   - Remove the secret from the diff
   - If the secret was ever staged or committed (even locally), it is now considered compromised → must be **rotated** at the source (revoke API key, change DB password, regenerate JWT signing key, etc.)
   - If a `{Section__Key}` placeholder was the original → restore it and set the real value via env var
4. Wait for user to confirm both removal AND rotation. Do not retry the scan-then-commit loop until they say so.

## False-positive handling

If user says "this is a placeholder" / "this is the example fixture" / "this is the public test key":
- Ask them to either: (a) rename/move it so the regex doesn't catch it, OR (b) acknowledge in the session — do **not** silently waive without acknowledgement.
- Common legitimate hits to ask about: `eyJ...` test JWTs in `*.spec.ts` / `*.Tests.cs` fixtures, `AIza...` example keys in markdown docs, `mongodb://localhost:27017` (no creds = OK).

## When to skip the scan

Never. Even docs-only or config-only PRs run this gate. The scan is fast (a handful of greps) and the cost of missing one secret far outweighs the cost of running it.