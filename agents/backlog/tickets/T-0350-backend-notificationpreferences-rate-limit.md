---
id: T-0350
title: "Backend (S5 consistency): add [EnableRateLimiting(\"auth\")] to NotificationPreferences GetMine/Update"
status: proposed
size: S
owner: backend
created: 2026-06-29
updated: 2026-06-29
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
- [ ] Both NotificationPreferences actions carry `[EnableRateLimiting("auth")]`.
- [ ] Reviewer APPROVE (a trivial consistency change; the existing tests stay green).

## Status log
- 2026-06-29 — filed from the T-0314 Gate-SEC (§7.17, LOW). Own-prefs-only (no leak); a rate-limit consistency
  fix, not an iOS blocker.
