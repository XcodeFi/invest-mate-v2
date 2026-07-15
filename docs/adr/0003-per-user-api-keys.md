# ADR-0003 — Per-user API keys (PAT) for non-interactive automation auth

- **Status:** Accepted
- **Date:** 2026-07-14 (accepted 2026-07-15 — backend fully landed through Slice 3; Slice 4 = NPU client, separate repo)
- **Related plan:** `docs/plans/p1-daily-digest-api-keys.md`
- **Affected layers:** Domain / Application / Infrastructure / Api

## Context

The local NPU assistant needs to pull a daily digest (holdings, status, watchlist, cash, position-sizing) from the API on Cloud Run on a cron, non-interactively, then feed it to Claude for buy/sell analysis. All AI endpoints today require an interactive user JWT (`[Authorize]` cookie/Bearer). We need a non-interactive credential that resolves to **a specific user** — each user has their own data, so the credential must carry user identity, not be a shared system secret.

The codebase already has an automation-auth pattern (`GcpOidc` scheme + `GcpScheduler` policy on `/internal/jobs/*`), but it authenticates a Google service account for **user-agnostic, system-wide** cron jobs. It carries no user identity and can't scope to one user's data, so it doesn't fit a per-user pull from an off-GCP machine.

## Options Considered

### Option A — Per-user API keys (personal access tokens)

Users self-generate named tokens with an expiry, manage them from the app (create / list / revoke). Only the SHA-256 hash is stored; plaintext shown once. A dedicated `ApiKey` auth scheme resolves the presented key → `userId`, accepted **only** on opt-in endpoints (the daily digest), never the whole API.

- **Pros:**
  - Carries user identity → naturally scopes to that user's data.
  - Self-service lifecycle: user creates, sets expiry, revokes — no ops involvement.
  - Leaked key limited to opt-in read endpoints; can't mutate trades.
  - Off-GCP friendly (any machine with the header) — no GCP SA key on the local box.
- **Cons:**
  - New auth path (3rd scheme alongside user JWT + GCP OIDC) + management UI/endpoints.
  - Static bearer secret — mitigated by hash-at-rest, required expiry, revocation.

### Option B — Reuse GCP OIDC / service-account token

NPU mints a Google OIDC ID token from a service-account key file and calls an endpoint under the existing `GcpScheduler` policy.

- **Pros:** Reuses an existing, battle-tested scheme; short-lived tokens.
- **Cons:** No user identity (system-wide) → can't scope to one user's data. Requires a GCP SA key JSON on the local machine (worse blast radius than a scoped PAT). Overloads a scheme meant for system cron.

### Option C — Long-lived user JWT stored on the machine

NPU stores the user's credentials and logs in / refreshes a JWT.

- **Pros:** No new auth code.
- **Cons:** Stores real user password/refresh token on the local box; a leaked JWT grants full account access (mutations included); no independent revocation without a password reset.

## Decision

**We choose Option A.**

It is the only option that carries per-user identity (the stated reason: each user has their own data) while keeping the blast radius of a leak small — required expiry, hash-at-rest, independent revocation, and acceptance limited to opt-in read endpoints. The added auth path is justified because neither existing scheme models "a specific user, off-GCP, non-interactive."

## Consequences

**Positive:**

- Reusable auth primitive for any future automation/integration, not just the digest.
- Security posture fits the fintech domain: credential lifecycle, expiry, revocation, least-privilege endpoint scoping.

**Negative / Trade-offs:**

- More surface: `ApiKey` entity + repo + management endpoints + auth handler + a small management UI.
- Static secrets exist in the wild (on the user's machine); mitigated but not eliminated.

**Follow-ups:**

- Migration: new `api_keys` collection + unique index on `KeyHash`.
- Tests: domain (expiry/revoke), handler (create/list/revoke), auth handler (valid/expired/revoked/missing → 401; JWT still works).
- Docs: `business-domain.md` (new entity), `features.md`, user guide for creating a key, `architecture.md` (new scheme + controller).

## References

- Plan: `docs/plans/p1-daily-digest-api-keys.md`
- PR: #XX (fill in after merge)
- External: prior art — GitHub personal access tokens (hash-at-rest, prefix display, expiry).
