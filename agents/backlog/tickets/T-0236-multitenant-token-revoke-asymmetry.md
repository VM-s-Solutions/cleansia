---
id: T-0236
title: Multi-tenant token-revoke asymmetry — TenantId=null token writes vs tenant-filtered revoke reads
status: done
size: M
owner: pm
created: 2026-06-12
updated: 2026-06-15
depends_on: [T-0188]
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 6
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
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6A — FIRST**). The MULTI-TENANT GO-LIVE BLOCKER —
  front-loaded. Dep T-0188✓. AC1 is an **architect call at contract-lock** between two implementation
  options (issuance-side TenantId stamping vs read-side `IgnoreQueryFilters` with a user-scoped predicate) —
  **not** an owner product decision, so it does not block promotion. **Security gate mandatory.** Lane
  Auth-token (`RefreshTokenService.cs`/`TokenService.cs` + refresh-token repo reads/revoke paths); no other
  6A ticket touches it. Must not regress T-0149 (rotation re-checks). **ef-migration ONLY if** AC4's
  backfill of existing null-stamped rows is required by the chosen rule — flagged + held then.
  Plan: `status/sprint-8.md` §3 Batch 6A.

- 2026-06-14 — **review** (backend, test-first). Lane Auth-token.
  - **AC1 (contract-lock — architect call):** chose **read-side `IgnoreQueryFilters()` with a
    caller-scoped predicate** over issuance-side stamping. The refresh-token read/revoke paths clear the
    EF global tenant filter and re-scope to the caller's own `TokenHash` / `UserId`; issuance stays
    null-stamped on the anonymous path. Rationale + the one rule documented in
    `agents/backlog/security/auth-sessions.md` (2026-06-14 entry). Mirrors the T-0245 fix shape and the
    `*IgnoringTenant` codebase idiom; correct against existing data with **no backfill**.
  - **RED (against current code):** new unit suite `RefreshTokenServiceTenantRevokeTests` (real
    `CleansiaDbContext` over SQLite, real global tenant filter, `FixedTenantProvider`). Seeds a
    tenant-bound user with a `TenantId == null` refresh token (the anonymous-issuance reality), then
    revokes from an **authenticated tenant context**. FAILED at `Assert.NotNull(token.RevokedAt)` →
    `Value of type 'Nullable<DateTimeOffset>' does not have a value` for both
    `RevokeByDeviceAsync_FromTenantContext_ActuallyRevokesNullStampedToken` and
    `RevokeAsync_Logout_FromTenantContext_ActuallyRevokesNullStampedToken` — the tenant filter hid the
    user's own null-stamped row so the revoke matched **zero rows** (the silent-zero-rows defect). The
    cross-user safety case passed already (no scoping regression).
  - **GREEN (after fix):** made `GetByTokenHashAsync` (logout/rotate/rotation-reuse),
    `GetActiveByUserIdAsync` (per-device revoke), and `RevokeChainAsync` (theft-signal chain) read via
    `IgnoreQueryFilters()` + the hash/`UserId` predicate. All 3 new unit tests pass; the revoke actually
    flips `RevokedAt`/`IsAlive` (the token is dead — AC2), and never touches another user's token (AC3).
    No `IRefreshTokenService`/endpoint signature change → **no nswag-regen**.
  - **No regression:** 203/203 of the refresh-token / revoke-device / auth unit tests pass (incl. the
    **T-0149 rotation-reuse** detection tests — `RotateAsync` still detects reuse and revokes the chain).
  - **Postgres parity:** authored `RefreshTokenTenantRevokePostgresTests` (Testcontainers, mirrors
    `OutboxClaimPostgresTests`) covering the same three cases on the production Npgsql provider. A local
    Npgsql `EnsureCreated` probe confirmed the `IgnoreQueryFilters()` revoke translates and flips the row
    on real Postgres; the probe was removed. The full integration **suite is currently un-runnable on
    this shared branch** due to a **cross-lane** `PendingModelChangesWarning` (T-0261's not-yet-migrated
    `IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation` index) and a mid-edit compile error in
    another lane's `ServiceRepository.cs` (T-0237). Both are outside the Auth-token lane; the
    orchestrator's authoritative clean run on the reconciled tree validates the Postgres test.
  - **AC4 — backfill:** NOT required under the chosen rule (read-side fix finds null-stamped rows as-is).
    No data migration; `manual_steps` stays `[]` (no ef-migration for T-0236).
  - **Deviations / manual steps:** none. Files: `RefreshTokenRepository.cs` (production, query-only +15);
    `RefreshTokenServiceTenantRevokeTests.cs` (unit); `RefreshTokenTenantRevokePostgresTests.cs`
    (integration); `auth-sessions.md` (the documented rule, AC1).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
