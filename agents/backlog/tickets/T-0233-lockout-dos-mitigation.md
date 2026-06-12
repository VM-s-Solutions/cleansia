---
id: T-0233
title: Targeted-lockout DoS mitigation — trusted-device bypass / CAPTCHA on locked-account login
status: draft
size: M
owner: —
created: 2026-06-12
updated: 2026-06-12
depends_on: [T-0193]
blocks: []
stories: []
adrs: [0003]
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: 4
source: T-0193 security-gate note N1 (Wave-3 close, 2026-06-12)
---

## Context
T-0193 (account lockout, merged `66cc823d`) deliberately introduced a new denial-of-service lever:
**anyone who knows a victim's email can lock that account for 15 minutes** by spraying 5 wrong
passwords (the login surface already discloses account existence via `NotExistingUserWithEmail` —
pre-existing contract). T-0115's per-IP window bounds a single source, but a low-rate distributed
sprayer can keep a targeted account (e.g. an admin) locked indefinitely. The T-0193 security gate
accepted the lockout as shipped and filed this as note **N1**: the standard mitigations are a
**trusted-device / known-good-cookie bypass** (a device that previously completed a successful login
for that account may still attempt a password while the account is "locked" for strangers) and/or a
**CAPTCHA challenge on locked-account login** instead of a hard refusal.

This is a real product/security design decision (which mechanism, cookie lifetime, scope across the
3 login surfaces) — **convene the deliberation panel (analyst author + challengers, security in the
loop) before this goes `ready`.** T-0193's out-of-scope list explicitly excluded 2FA/trusted-device
flows; this ticket is that follow-up.

## Acceptance criteria
- [ ] **AC1** — Given an account locked by failed attempts from unknown sources, When the legitimate
  user attempts login from a device that has previously completed a successful login for that
  account (trusted-device marker) — or passes the chosen challenge — Then the correct password
  succeeds despite the lockout window.
- [ ] **AC2** — Given an attacker without the trusted-device marker, When they continue spraying a
  locked account, Then behavior is unchanged from T-0193 (locked, `auth.account_locked`, no
  password evaluation, no counter oracle).
- [ ] **AC3** — The mechanism covers all three internal-auth login surfaces (`Login`, `AdminLogin`,
  `PartnerLogin`) — same per-account semantics T-0193 established.
- [ ] **AC4** — Tests (red-first) prove the bypass works for the trusted device and does NOT weaken
  the lockout for untrusted sources; no new enumeration oracle is introduced.

## Out of scope
- Full 2FA / "remember this device" UX beyond the lockout-bypass marker.
- Re-tuning T-0193 thresholds or T-0115 windows.

## Implementation notes
Panel decides: trusted-device cookie (HttpOnly, per-account HMAC) vs CAPTCHA vs both. Mind S1–S4;
the marker must not become a session credential. Mobile apps (Android) need the equivalent marker
path if the panel chooses cookies — flag the android layer at contract-lock if so.

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; from T-0193 security note N1)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
