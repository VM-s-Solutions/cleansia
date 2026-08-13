---
id: T-0350
title: "Backend (S5 consistency): add [EnableRateLimiting(\"auth\")] to NotificationPreferences GetMine/Update"
status: done
size: S
owner: backend
created: 2026-06-29
updated: 2026-06-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
priority: low
manual_steps: []
sprint: 12
source: T-0314 Gate-SEC (sprint-12 §7.17) — LOW finding (rate-limit consistency gap)
---

> **LOW S5 consistency gap — NOT an iOS T-0314 blocker.** Surfaced by the T-0314 design gate. The
> `NotificationPreferences` controller actions lack the per-user rate-limit window the rest of the
> customer-tail mutations carry.

## The gap
`NotificationPreferencesController` GetMine + Update have only `[Permission(Policy.Authenticated)]` — **no
`[EnableRateLimiting("auth")]`** (verified `NotificationPreferencesController.cs:21-22,33-34`). `Update` is a
side-effecting **replace-all** mutation, so per S5 it should carry the same partitioned per-JWT-subject window
its siblings (Devices, Disputes, Membership) use. It is **own-prefs-only** (JWT-subject, no wire-supplied id) so
there is **no cross-user data-leak risk** — the exposure is an un-throttled per-user write (DB
write-amplification abuse).

## Fix
- Add `[EnableRateLimiting("auth")]` to both `GetMine` and `Update` on `NotificationPreferencesController`,
  matching the partitioned per-JWT-sub window the other customer-tail mutation endpoints use.
- No contract/DTO change, no regen, no migration.

## Done when
- [x] Both NotificationPreferences actions carry `[EnableRateLimiting("auth")]`.
- [x] Reviewer APPROVE (a trivial consistency change; the existing tests stay green).

## Status log
- 2026-06-29 — filed from the T-0314 Gate-SEC (§7.17, LOW). Own-prefs-only (no leak); a rate-limit consistency
  fix, not an iOS blocker.
- 2026-06-30 — **proposed → done** (HARDENING-1, `64f6525` on `phase/hardening-1`, off master `3e7ce52`;
  bundled in the backend trio with T-0346 + T-0348). Added `[EnableRateLimiting("auth")]` to GetMine + Update
  on **both** the customer-host and mobile-host `NotificationPreferencesController` (the partitioned per-JWT-sub
  window its siblings carry); plus a `RateLimitCoverageGuardTests` guard for the lazy-create GET. No
  contract/DTO change, no regen, no migration. **Security review CLEAN** (own-prefs-only, no cross-user leak).
  Build 0 errors; `Cleansia.Tests` 1685. Reviewer APPROVE. NOT committed by the PM — the owner commits the
  backlog edits with the phase PR.
