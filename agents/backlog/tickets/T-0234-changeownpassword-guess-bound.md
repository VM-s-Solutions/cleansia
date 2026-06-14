---
id: T-0234
title: Bound ChangeOwnPassword current-password guessing (authenticated surface)
status: ready
size: S
owner: pm
created: 2026-06-12
updated: 2026-06-14
depends_on: [T-0193]
blocks: []
stories: []
adrs: [0003]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 6
source: T-0193 security-gate note N5 + dev handoff residual (Wave-3 close, 2026-06-12)
---

## Context
T-0193's lockout covers the **login** surfaces only. `ChangeOwnPassword`'s current-password check is
an **authenticated guessing surface** a session-holder (stolen token, unattended browser) can use to
brute the account password — today bounded only by the per-`sub` 30/min rate-limit window
(T-0115 "auth" policy). The T-0193 developer flagged it on the record as an out-of-scope residual
("file separately if wanted") and the security gate filed it as note **N5**. Close the gap by giving
the current-password check the same per-account failure budget the login surfaces got.

No-decision note candidate is WRONG here — this charges a counter on an authenticated path; small
but security_touching, so the security gate runs. The shape, however, is already canonical: the
T-0193 validator-invoked atomic `ExecuteUpdateAsync` counter pattern (see
`agents/knowledge/patterns-backend.md`, "Failure-path counters bypass the UoW deliberately").

## Acceptance criteria
- [ ] **AC1** — Given N consecutive wrong current-password attempts on `ChangeOwnPassword`
  (threshold aligned with `User.MaxFailedLoginAttempts` unless security argues otherwise), When the
  budget is exhausted, Then further attempts are refused with a distinct error key for the window
  duration — the current password is no longer evaluated (no oracle).
- [ ] **AC2** — A successful change (or window expiry) restores a fresh budget.
- [ ] **AC3** — The charge is atomic/race-proof under parallel requests (conditional
  `ExecuteUpdateAsync`, mirroring T-0193) — failing commands never reach the UoW commit.
- [ ] **AC4** — Red-first tests cover budget charge, refusal, expiry, and reset-on-success; i18n key
  for the new error in all 5 locales × affected apps.

## Out of scope
- The login/confirm/reset surfaces (T-0193, shipped). Re-tuning T-0115 windows.
- Admin change-password-for-other-user flows (no current-password check there).

## Implementation notes
Decide whether to reuse `FailedLoginAttempts`/`LockoutEndsAt` (couples the two surfaces — a
change-password sprayer would lock login too; arguably correct) or a dedicated counter pair (needs
an owner **ef-migration** — if chosen, add the manual_step). Security gate settles this at
contract-lock; default recommendation: reuse the existing lockout pair, no new columns.

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; from T-0193 security note N5)
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6B**). Dep T-0193✓. The shape is canonical (the
  T-0193 validator-invoked atomic `ExecuteUpdateAsync` counter pattern) → no panel; **security gate** runs
  (charges a counter on an authenticated path) and settles the counter choice at contract-lock. **Default =
  reuse the existing lockout pair → no migration**; ef-migration ONLY if a dedicated counter is chosen
  (flag then). **Lane BusinessErrorMessage + locale-JSONs — runs AFTER T-0262** (6A) which removes a key in
  the same files; this ticket adds a new `auth.*` key ×5 locales. **Lane Auth-surface — before T-0233**
  (6E), same lockout/login authn surface. Red-first tests. Plan: `status/sprint-8.md` §3 Batch 6B.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
