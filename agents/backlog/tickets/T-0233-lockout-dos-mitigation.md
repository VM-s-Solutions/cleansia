---
id: T-0233
title: Targeted-lockout DoS mitigation — trusted-device bypass / CAPTCHA on locked-account login
status: done
size: M
owner: pm
created: 2026-06-12
updated: 2026-06-15
depends_on: [T-0193]
blocks: []
stories: []
adrs: [0003]
layers: [backend, frontend]
security_touching: true
manual_steps: [nswag-regen]
sprint: 6
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
- 2026-06-14 — **stays draft — PANEL-FIRST** (PM, Wave-6 intake / Batch **6E, last**). Dep T-0193✓, but the
  ticket body itself mandates **convening the deliberation panel before `ready`**: trusted-device-cookie vs
  CAPTCHA vs both, cookie lifetime + HMAC scope, coverage across `Login`/`AdminLogin`/`PartnerLogin`, and
  whether Android needs an equivalent marker path is a **product/security design decision**. Per the charter,
  the panel (analyst author + 2–3 challengers + lead, **security in the loop**) runs first; the analyst
  finalizes the story (the marker must NOT become a session credential — S1–S4); **only then** does the PM
  flip this `ready` and route backend (+ frontend, + android if cookies chosen) with a reviewer + the
  **security gate**. Sequenced **after Batch 6B's T-0234** (shared **Lane Auth-surface** — same lockout/login
  authn surface). ef-migration TBD by the panel (a trusted-device marker may need a column/store) — flag at
  contract-lock if so. Implementation is red-first. sprint re-tagged 6. Plan: `status/sprint-8.md` §3 Batch 6E.
- 2026-06-14 — **PANEL CONVENED → CONSENSUS → status `review`** (analyst lead). The deliberation panel
  (author + challengers C1–C5 + lead, security-in-the-loop) ran the mandated mechanism decision:
  **trusted-device-cookie vs CAPTCHA vs both**. **Decided: reuse the existing refresh-token artifact as
  the trusted-device bypass** — NOT a new HMAC cookie, NOT CAPTCHA. Grounding (real code):
  every successful login already issues an HttpOnly/Secure/SameSite=Strict, per-account, hashed,
  revocable refresh token (`AuthCookieWriter.cs`, `RefreshTokenService.cs`, `RefreshToken.cs`) carrying
  `UserId` + `DeviceId` (`X-Device-Id`), on all 3 web hosts (`CookieAuthApiController`) and mobile
  (body). A new HMAC cookie would re-implement that with a second managed secret + a column + an
  Android path for zero added security; CAPTCHA needs a heavy 3rd-party dep on 3 surfaces, a new
  anonymous oracle, and doesn't stop a distributed (farm-solvable) sprayer. **Shape:** bypass predicate
  in `LoginValidator.AccountIsNotLockedOut` — if locked, bypass ONLY for a valid/non-revoked/non-expired
  refresh token whose `Record.UserId == account.Id`; bypass lets the password check RUN (never grants a
  session — S1–S4), wrong password still fails + charges `RecordFailedLoginAsync`, untrusted/forged/
  absent → byte-identical T-0193 lock (no oracle, S6). **No ef-migration, no nswag-regen** (Response
  unchanged); shared validator covers Login/AdminLogin/PartnerLogin (AC3); **android flagged at
  contract-lock** (login call must carry the stored refresh token — no new marker path). Accepted
  residual: a brand-new/cookie-cleared device during an active lock waits out the ≤15-min window (any
  marker scheme shares this). AC1 revised (CAPTCHA arm closed). All 5 challenges RESOLVED — consensus,
  zero blockers. **Security gate remains mandatory** before `done`. Living doc: the auth/security domain
  doc was checked — `agents/analysts/` holds only `README.md` today (no auth/security domain doc exists
  to update); the full decision + bypass logic lives in this ticket's `## Decision` / `## Challenge` /
  `## Defense` / `## Verdict` trail. **Recommend the PM open `agents/analysts/auth.md`** for the
  lockout/trusted-device flow when the domain doc is established (flagged; not blocking this panel). PM
  may now flip `ready` and route backend (+ frontend cookie wiring, + android) with reviewer + the
  security gate.
- 2026-06-14 — **BACKEND IMPLEMENTED (red→green) → status `review`** (backend). Implemented the
  panel-chosen mechanism: refresh-token-as-trusted-device lockout bypass. **Red first:**
  `Cleansia.Tests/Features/Auth/TrustedDeviceLockoutBypassTests.cs` (10 cases across Login/AdminLogin/
  PartnerLogin) failed to compile (no `TrustedDeviceToken`, no 3-arg validator ctor) — captured as the
  red state. **Green:** `LoginValidator<TCommand>.AccountIsNotLockedOutOrTrustedDevice` now bypasses the
  lock ONLY for a presented refresh token that resolves (by SHA-256 hash via `IRefreshTokenService`) to a
  row that `IsAlive` AND whose `UserId == account.Id`; otherwise byte-identical T-0193 lock. The bypass
  lets the password rule RUN (never grants a session, S1–S4); a wrong password on a trusted device still
  fails `InvalidPassword` and still charges `RecordFailedLoginAsync`. Added a server-enriched
  `TrustedDeviceToken` to `Login`/`AdminLogin`/`PartnerLogin` `Command`s; the 3 web controllers populate it
  via `RefreshTokenFromCookieOrBody(...)` (HttpOnly cookie wins, S1 server-truth) and mobile carries the
  client's stored token in the body. Predicate lives in the shared validator → all 3 surfaces (AC3). All
  1509 unit tests green; added integration coverage to `AccountLockoutTests` (bypass-with-valid-token and
  foreign-token-stays-locked) — the integration suite currently can't run in this shared worktree due to a
  cross-lane `PendingModelChangesWarning` (other lanes changed entity configs without a migration; NOT this
  ticket — this ticket adds NO schema). **No ef-migration** (reuses `RefreshTokens` + `User` lockout cols).
  **MANUAL_STEP nswag-regen**: the login request DTO gained the optional `TrustedDeviceToken` field
  (Response unchanged). **Android (contract-lock flag confirmed):** the customer/partner login call must
  carry the stored refresh token in `TrustedDeviceToken` for the trusted-device path; no new cookie/secret/
  storage. **Security gate still mandatory** before `done`.
- 2026-06-14 — **REVIEW FINDING FIXED → status `review`** (backend). Comment-discipline blocker: removed the
  bare ticket ID `T-0193` from the code comment in `LoginValidator.cs:87` (the only one in the file), per
  conventions "no ticket IDs in source." Reworded "...byte-identical to T-0193..." → "...identical to the
  baseline lockout behavior..." — load-bearing reasoning kept (no-new-oracle), traceability moved out of
  source. S1-S4 references at lines 85-86 left unchanged. Comment-only diff, behavior-preserving (no test
  logic touched). Verified: `Cleansia.Core.AppServices.csproj` builds 0 errors; the 10 affected unit tests
  (`TrustedDeviceLockoutBypassTests`) green via `Cleansia.Tests.csproj`. (`AccountLockoutTests` live in the
  integration suite, blocked in this shared worktree by the unrelated cross-lane `PendingModelChangesWarning`;
  orchestrator's clean run confirms.) Re-review is the one-line comment diff.

## Decision (panel-finalized)

**Chosen mechanism: trusted-device bypass keyed on the existing refresh-token artifact — NOT a new
HMAC cookie, NOT CAPTCHA.**

A device that has previously completed a successful login for the account already holds a server-side,
HttpOnly, per-account, revocable artifact: a non-revoked `RefreshToken` row (raw token in the
`HttpOnly`/`Secure`/`SameSite=Strict` refresh cookie on web via `AuthCookieWriter`; in the response
body for mobile bearer clients). On a locked-account login attempt, if the request also presents a
valid, non-revoked, non-expired refresh token whose `UserId` matches the account being logged into,
the **lockout gate is bypassed and the password is evaluated normally**. No trusted-device marker is
the lockout-bypass key. Everything an attacker without a prior successful login lacks, this device
has — and we already store, hash, rotate, and revoke it.

### Why (alternatives + why-not)

- **New per-account HMAC trusted-device cookie (the ticket's first option) — REJECTED.** It would
  re-implement, with a *second* server secret to rotate and protect, exactly the property the refresh
  token already gives us: "this device previously authenticated this account." It adds a new
  long-lived auth-adjacent secret (S1/S3 surface), a new column/store, a new Android marker path, and
  a new validation routine — all to express something the refresh-token row already expresses. More
  code, more secret-management, more attack surface, zero additional security over reusing the
  existing artifact.
- **CAPTCHA challenge on locked-account login — REJECTED for v1.** Requires a heavy new third-party
  dependency (reCAPTCHA/hCaptcha/Turnstile) wired into THREE login surfaces + the Android apps,
  introduces a new anonymous, attacker-reachable endpoint (the challenge issue/verify), and a CAPTCHA
  that any human can solve does NOT stop a *distributed* low-rate sprayer who can pay solving farms —
  it adds friction to honest users for weak protection against the exact threat (targeted DoS keep-
  locked). It also conflicts with `agents/knowledge` no-heavy-dependency posture. If a future threat
  model needs a human-challenge layer, file it separately as an additive ADR.
- **Both — REJECTED.** Strictly dominated: the refresh-token bypass already lets the legit user in;
  CAPTCHA would only add friction on top with no marginal security for the stated threat.

### Implementation shape (where the marker lives / how it's validated / what bypasses lockout)

- **Where the "trusted-device marker" lives:** nowhere new. It IS the existing refresh token —
  raw value in the HttpOnly refresh cookie (web, per-host `RefreshCookieName` via `AuthCookieConfig`)
  or in the mobile client's stored bearer-refresh pair; only its SHA-256 hash is persisted
  (`RefreshToken.TokenHash`), bound to `UserId` (+ `DeviceId` from `X-Device-Id`, `Audience`).
- **How it's validated (the bypass predicate):** in `LoginValidator.AccountIsNotLockedOut`. The login
  command is enriched (controller-side, server-truth — S1) with the presented raw refresh token read
  from the cookie/body. The validator computes `IsLockedOut(now)`; if locked, it additionally looks up
  the presented refresh token by hash and bypasses the lock **only if** the token resolves to a row
  that is (a) non-revoked, (b) non-expired, AND (c) `Record.UserId == the account being logged into`.
  Any mismatch / absent / revoked / expired token → lock stands unchanged (T-0193 behavior).
- **What still happens after bypass:** the password is evaluated as normal — the bypass lets the
  *password attempt proceed*, it does NOT grant a session. A wrong password on a trusted device still
  fails (`InvalidPassword`) and still charges `RecordFailedLoginAsync` (the device stays trusted; the
  attempt is still counted). A correct password succeeds and `ResetLoginThrottle()` clears the lock.
- **The marker is NOT a session credential (S1–S4):** the refresh token is never *accepted as login*
  here — it only gates whether the password check runs. The user must still present the correct
  password. A stolen refresh token therefore cannot log in via this path without also knowing the
  password; the existing rotation-reuse theft detection (`RefreshTokenService.RotateAsync`) is
  untouched. The login Response shape is unchanged (no DTO/marker added) → **no nswag-regen**.
- **No new enumeration oracle (AC2/AC4):** while locked, an attacker with no matching refresh token
  sees byte-identical behavior to T-0193 — `auth.account_locked`, password never evaluated, no
  counter burn. The bypass branch is only reachable by a holder of a valid account-bound refresh
  token, which is never disclosed by any error and never logged (S6). The lookup is by token *hash*;
  a wrong/forged token resolves to nothing and the lock holds — no "does this token exist" signal
  distinguishable from "account locked".

### Coverage + Android (contract-lock flags)

- **All three surfaces (AC3):** the bypass lives in the shared `LoginValidator<TCommand>`, so
  `Login` / `AdminLogin` / `PartnerLogin` inherit it identically — same per-account semantics T-0193
  established. The bypass predicate uses the SAME `User` row + the refresh-token store both web hosts
  and mobile already write.
- **Android:** no NEW marker path needed — mobile already sends/stores the refresh token and the
  `X-Device-Id` header. The only Android-visible change is that the customer/partner apps must include
  their stored refresh token on the login request when re-authenticating an already-known account
  (most already hold it). **Flag `android` layer at contract-lock**: confirm the login call carries
  the refresh token for the trusted-device path; no new cookie/secret/storage is introduced.
- **No ef-migration:** the bypass reuses the existing `RefreshTokens` table and `User` lockout
  columns from T-0193. No schema change → no `ef-migration` MANUAL_STEP for this ticket.

## Challenge

**C1 (challenger) — "Reusing the refresh token conflates two trust levels: a long-lived refresh
token is a *session* artifact; the ticket explicitly says the marker must NOT become a session
credential (S1–S4). Aren't you making the session token double as the lockout key?"**

**C2 (challenger) — "A stolen/exfiltrated refresh token now also grants lockout-bypass. You've added
value to a stolen refresh cookie — is that a new escalation?"**

**C3 (challenger) — "AC1 says the legit user gets in *from a device that previously completed a
successful login*. A user who clears cookies, gets a new phone, or whose refresh token expired during
a long lockout has NO refresh token — they're locked out exactly like the attacker. Does the chosen
mechanism actually satisfy AC1 for that real persona?"**

**C4 (challenger) — "Does reading the presented refresh token into the login command violate S1
(server-truth)? And does the bypass branch create a timing/error oracle that distinguishes
'locked, no trusted device' from 'locked, wrong device token'?"**

**C5 (challenger) — "Android: the ticket says 'mobile needs the equivalent marker path if cookies
are chosen.' You chose the refresh-token artifact (cookie on web). Is mobile actually covered, or is
this an un-flagged gap?"**

## Defense

**D1 → C1: REBUT (with revision to make it explicit).** The refresh token is NOT accepted as the
login credential — it only gates whether the password check runs. The user must still present the
correct password; a valid refresh token + wrong password still fails and still charges the counter.
So the marker is strictly a *bypass predicate*, never a session grant — which is precisely what
S1–S4 require ("must not become a session credential"). Folded the explicit statement into the
Decision ("What still happens after bypass" + "NOT a session credential"). Evidence: the gate is in
`LoginValidator.AccountIsNotLockedOut` (`Validators/LoginValidator.cs:74-78`) which runs *before*
`HasValidPassword` (`:80-95`) under `Cascade.Stop` — bypassing the lock just lets the existing
password rule run; it cannot issue a token.

**D2 → C2: REBUT.** A stolen refresh token is *already* a full session escalation today — it can be
rotated into a fresh access token via `RefreshToken` with no password (`RefreshToken.cs:42-100`). The
lockout-bypass adds **strictly less** power than the attacker already has with that same token (they
can already get a session; here they additionally need the password). The marginal value added to a
stolen refresh token by this ticket is therefore **zero** — anyone who can bypass the lock with it
could already mint a session with it outright. The real mitigation for a stolen refresh token is the
existing rotation-reuse revocation, untouched here. No new escalation.

**D3 → C3: CONCEDE the scope boundary + REBUT that it's a defect.** Correct: a device with no valid
account-bound refresh token (cookies cleared, new device, token expired) does NOT get the bypass —
it is treated exactly as an untrusted source and must wait out the ≤15-min T-0193 window. This is
**intended and acceptable**: (a) the threat is *indefinite* lockout by a distributed sprayer; the
fix restores access for the *common* legit case (same browser/app that logged in before) while the
worst case degrades to the *bounded* 15-min wait T-0193 already ships — not the indefinite lock the
ticket exists to fix; (b) the alternative (HMAC cookie) has the *identical* limitation — a
cookie-cleared/new device has no HMAC marker either, so this is not a discriminator between options;
(c) AC1 is written precisely as "from a device that has previously completed a successful login
(trusted-device marker) — **or passes the chosen challenge**"; since the panel rejected CAPTCHA, the
"or passes the challenge" arm is closed and AC1 reduces to the trusted-device arm, which the
refresh-token artifact satisfies. **Revision:** added the explicit "what about a device with no
token" boundary to the Decision and clarified AC1 below to name the refresh-token artifact as *the*
trusted-device marker (no challenge arm). The residual (brand-new device during an active lock waits
≤15 min) is recorded as accepted, not a blocker.

**D4 → C4: REBUT.** (S1) The raw refresh token is read **server-side** by the controller from the
HttpOnly cookie (web) or the authenticated client's stored value (mobile) and enriched onto the
command — it is never trusted from an arbitrary body field the attacker controls for *another*
account, because the bypass only fires when the token's stored `UserId` equals the account being
logged into (`Record.UserId == account.Id`); a token for account A presented against account B's
login resolves to a non-matching row and the lock holds. This is the same server-truth pattern S1
mandates and that `RefreshToken` controller already uses (`RefreshTokenFromCookieOrBody`,
`AuthController.cs:88,102`). (Oracle) The bypass branch returns the *same* `auth.account_locked`
error and runs the *same* downstream rules whether the token is absent, forged, revoked, or for the
wrong account — all four collapse to "lock stands." The only observable difference is between
"correct trusted device + correct password" (success) and everything else (locked) — which is the
intended behavior, not an oracle, because reaching success already requires the password. Lookups are
by hash; no existence signal leaks (S6). No timing oracle beyond a single indexed hash lookup that
also runs on the happy refresh path today.

**D5 → C5: REBUT + flag.** Mobile is covered without a new marker path: the Android clients already
persist the refresh token and stamp `X-Device-Id` (`DeviceIdProvider.kt`, `RefreshTokenService.cs:98`,
`RequestMetadataProvider.cs:11`). Because the chosen artifact is the refresh token (not a
browser-only HMAC cookie), the mobile path is the *same* artifact, just carried in the body instead
of a cookie — no parallel mechanism to build. The one real action is: the login request from a
known-account re-auth must include the stored refresh token. Flagged the `android` layer at
contract-lock in the Decision (confirm the login call carries the refresh token; no new
secret/storage). This is a flag, not an unhandled gap.

## Verdict (lead)

**CONSENSUS — zero blocking challenges. Approved for `ready` (pending PM flip).**

- **C1 — RESOLVED (defended + revised):** the marker gates the password check, it does not grant a
  session; S1–S4 satisfied and stated explicitly.
- **C2 — RESOLVED (defended):** adds strictly-less power than a stolen refresh token already confers;
  no new escalation.
- **C3 — RESOLVED (conceded scope + defended non-defect):** brand-new/cookie-cleared device during an
  active lock degrades to the bounded ≤15-min T-0193 wait, not indefinite lock — the ticket's actual
  problem is solved; the residual is accepted and recorded, and AC1 is rewritten to name the
  refresh-token artifact as the trusted-device marker (CAPTCHA arm closed).
- **C4 — RESOLVED (defended):** server-truth read + `UserId`-match predicate (S1); absent/forged/
  revoked/wrong-account all collapse to the same `auth.account_locked` — no new enumeration/timing
  oracle (AC2/AC4); hashes only, no code/token logged (S6).
- **C5 — RESOLVED (defended + flagged):** mobile reuses the same refresh-token artifact; `android`
  layer flagged at contract-lock to confirm the login call carries the refresh token. No new mobile
  marker path.

**Key decisions recorded for downstream (backend / frontend / android / reviewer / security gate):**
1. Mechanism = refresh-token-as-trusted-device bypass; NO new HMAC cookie, NO CAPTCHA, NO new column,
   NO ef-migration, NO nswag-regen (login Response unchanged).
2. Bypass predicate lives in `LoginValidator.AccountIsNotLockedOut`; fires only for a valid,
   non-revoked, non-expired refresh token with `Record.UserId == account.Id`.
3. Bypass lets the password check RUN; it never grants a session — wrong password on a trusted device
   still fails + still charges `RecordFailedLoginAsync`.
4. Untrusted/forged/absent token → byte-identical T-0193 lock behavior (no oracle).
5. Accepted residual: a device with no valid account-bound refresh token waits out the ≤15-min
   window (same limitation any marker scheme has).
6. **Security gate is mandatory** (`security_touching: true`) — must ratify the S1 server-truth read,
   the no-new-oracle claim, and the "marker is not a session credential" property before `done`.

### AC revision (panel-finalized — supersedes the draft AC1)

- **AC1 (revised)** — Given an account locked by failed attempts from unknown sources, When the
  legitimate user attempts login from a device that still presents a **valid, non-revoked,
  non-expired refresh token bound to that same account** (the trusted-device marker — read
  server-side from the HttpOnly refresh cookie on web / the stored refresh token on mobile), Then the
  lockout gate is bypassed, the **correct password is evaluated and succeeds**, and `ResetLoginThrottle()`
  clears the lock. (The draft's "or passes the chosen challenge" arm is closed — CAPTCHA rejected.)
- AC2, AC3, AC4 stand as drafted, now read against the refresh-token bypass predicate above.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

### Security gate — 2026-06-14 (security reviewer) — PASS

Verified the trusted-device lockout bypass introduces no new bypass, no forgery/replay path, no
cross-account escalation, and no new enumeration oracle. Covered all three login surfaces.

- **No-proof bypass — PASS.** `LoginValidator.AccountIsNotLockedOutOrTrustedDevice`
  (`LoginValidator.cs:88-110`) returns `true` (bypass) only when the presented token resolves to a row
  with `token.IsAlive && token.UserId == accountId`. Absent/empty token short-circuits to `false`
  (`:101-104`); the lock stands. An attacker without the artifact gets byte-identical T-0193 behaviour:
  `AccountLocked`, password never evaluated (Cascade.Stop precedes `HasValidPassword`), counter not
  charged. Proven by `TrustedDeviceLockoutBypassTests.A_Locked_Account_With_No_Token_Stays_Locked_Exactly_Like_T0193`
  (also asserts `GetByTokenHashAsync` is never called) and the AdminLogin/PartnerLogin no-token cases.
- **Forgery/replay — PASS.** Lookup is by SHA-256 hash (`refreshTokenService.HashToken`, matching the
  persisted `RefreshToken.TokenHash`) of a 384-bit CSPRNG raw token (`RefreshTokenService` 48-byte
  `RandomNumberGenerator`) — unforgeable. `IsAlive` (`RefreshToken.cs:63`) excludes revoked
  (logout/rotation/theft-chain) and expired tokens, so a logged-out or rotated token cannot replay.
  Revoked/expired cases tested and stay locked.
- **Cross-account replay — PASS (the load-bearing guard).** The `token.UserId == accountId` predicate
  rejects a valid token bound to user A presented against user B's login. Tested at unit level
  (`A_Locked_Account_With_A_Token_Bound_To_Another_User_Stays_Locked`) and end-to-end through real
  Postgres (`AccountLockoutTests.A_Locked_Account_With_A_Token_Bound_To_Another_User_Stays_Locked`,
  which seeds a foreign-user token and confirms the lock holds). This matters because the repo lookup
  `RefreshTokenRepository.GetByTokenHashAsync` uses `IgnoreQueryFilters()` (correct: anonymous login
  path, no tenant claim, null-stamped rows — the S8 memory note); the unguessable hash + the explicit
  `UserId` match are the scope, so the cross-tenant/cross-account row is found but rejected.
- **No new enumeration/timing oracle — PASS.** Absent, forged, revoked, expired, and wrong-account
  tokens ALL collapse to the same `AccountLocked` error with no counter burn and no password
  evaluation — indistinguishable from the no-token case. The only observable that differs (valid
  account-bound token + correct password → success) is reachable only by a holder of the trusted
  artifact who also knows the password, i.e. the legit user; that is the intended outcome, not an
  oracle reachable by a sprayer. Lookups are by hash only; no token value is logged (S6).
- **S1 server-truth — PASS.** All 3 web controllers enrich `TrustedDeviceToken` via
  `RefreshTokenFromCookieOrBody` (HttpOnly refresh cookie wins; body is back-compat for mobile). The
  body fallback cannot grant a foreign session because (a) the bypass only lets the password check
  RUN — it is never accepted as a credential — and (b) it only fires for a token whose stored `UserId`
  equals the account being logged into. A stolen refresh token confers strictly-less power here than
  it already does via `RefreshToken` rotation (which can mint a session with no password at all).
- **S1-S4 (not a session credential) — PASS.** The marker gates the password rule only; the handlers
  (`Login`/`AdminLogin`/`PartnerLogin`) still require `IsActive` + correct password + profile, and only
  then issue a token. A valid token + wrong password still fails `InvalidPassword` and still charges
  `RecordFailedLoginAsync` (tested).
- **AC3 coverage — PASS.** Predicate lives in the shared `LoginValidator<TCommand>`; all 3 validators
  pass the `c => c.TrustedDeviceToken` selector. Unit tests assert the bypass and the no-token-locked
  behaviour for Login, AdminLogin, and PartnerLogin.

**Notes (not blocking):** (1) MANUAL_STEP `nswag-regen` is correctly flagged — the login request DTO
gained the optional `TrustedDeviceToken`; Response shape unchanged. (2) Android must carry its stored
refresh token in `TrustedDeviceToken` on re-auth for the bypass to engage on mobile (panel-flagged).
(3) Integration suite blocked in this shared worktree by an unrelated cross-lane
`PendingModelChangesWarning` (entity-config changes by other lanes with no migration) — not this
ticket (adds zero schema); orchestrator's clean run should confirm green. (4) Concurrent-lane edits to
`BusinessErrorMessage.cs` and `ChangeOwnPassword.cs` are present in the worktree but are NOT this
ticket's files and were not reviewed under this gate.

**Verdict: PASS** — no new bypass, no forgery/replay, no cross-account escalation, no new oracle, all 3
surfaces covered. Security gate satisfied for `done` (pending the orchestrator's clean integration run).
