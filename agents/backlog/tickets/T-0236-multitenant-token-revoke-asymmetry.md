---
id: T-0236
title: Multi-tenant token-revoke asymmetry — TenantId=null token writes vs tenant-filtered revoke reads
status: draft
size: M
owner: —
created: 2026-06-12
updated: 2026-06-12
depends_on: [T-0188]
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 4
source: T-0188 security-gate note 1; recorded in agents/backlog/security/auth-sessions.md (Wave-3 close)
---

## Context
Latent multi-tenant gap found by the T-0188 security gate (device/session management, merged
`5d631f8c`/`8ddfef9d`), recorded in `agents/backlog/security/auth-sessions.md`: refresh tokens are
stamped **`TenantId = null`** on the anonymous login/refresh path (no tenant claim exists yet), but
the revoke-side reads (e.g. `GetActiveByUserIdAsync` behind `RevokeByDeviceAsync` /
device-session listing) run on an **authenticated** request — for a user carrying a `tenant_id`
claim, the EF global tenant filter would **filter the user's own null-stamped tokens out of view**,
so "revoke this device/session" silently matches zero rows. This is exactly the silent-zero-rows
class the memory notes warn about (cf. tenant-ignoring reads on webhook paths). **Dormant in
single-tenant production (everything is null today); MUST be fixed before any multi-tenant
onboarding.**

## Acceptance criteria
- [ ] **AC1 (decide + make symmetric)** — Token issuance and token reads agree on tenancy: either
  tokens are stamped with the user's actual TenantId at issuance (resolved from the user row, not
  the not-yet-existing claim), or the refresh-token read/revoke paths use `IgnoreQueryFilters()` with
  an explicit user-scoped predicate. One rule, documented in `auth-sessions.md`.
- [ ] **AC2** — Given a tenant-bound user with active sessions, When they list devices/sessions and
  revoke one, Then the token is actually revoked (row updated — no silent zero-rows), proven by a
  test that sets a tenant context.
- [ ] **AC3** — Cross-tenant safety holds: a user can never read or revoke another tenant's (or
  another user's) tokens — S1/S3 tests included.
- [ ] **AC4** — If backfill of existing null-stamped rows is required by the chosen rule, the data
  migration is specified and flagged **manual_step: ef-migration** to the owner (add to frontmatter
  at contract-lock).

## Out of scope
- The device UX (shipped in T-0188). General multi-tenant onboarding work beyond the token paths.

## Implementation notes
Architect call at contract-lock between the two AC1 options (issuance-side stamping is the durable
fix; read-side `IgnoreQueryFilters` is the contained one — cf. `RefreshTokenService.cs`,
`TokenService.cs`). Re-read `auth-sessions.md` first; do not regress T-0149 (rotation re-checks).

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; from the T-0188 security note)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
