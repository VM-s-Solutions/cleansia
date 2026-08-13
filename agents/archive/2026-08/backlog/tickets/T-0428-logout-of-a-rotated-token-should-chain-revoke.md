---
id: T-0428
title: "Auth — logout of a ROTATED refresh token revokes its SUCCESSOR CHAIN (session-scoped, ownership-gated), not a silent no-op and not an account-wide theft response"
status: done
size: S
owner: backend
created: 2026-07-17
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: [ADR-0026, ADR-0027]
layers: [backend]
security_touching: true
priority: low
sprint: 12
manual_steps: []
source: T-0421 adversarial review F4 (pre-existing gap, INDEX row 2026-07-17)
note: SECURITY DESIGN DECISION ticket — the panel verdict below fixes the SHAPE before any
  implementation. The INDEX row's leaning ("chain-revoke = all user tokens") was examined and
  REJECTED by the panel in favor of a narrower successor-chain revoke; see Decision (panel).
---

> **The gap (latent, post-compromise persistence — not a live breach).**
> `RefreshTokenService.RevokeAsync` silently succeeds when the presented token is already revoked
> (`RefreshTokenService.cs:137-144` — deliberate anti-probing + idempotent-logout design; sole
> caller is `Logout.Handler`, `Features/Auth/Logout.cs:29-32`). Consequence: a thief who ROTATES
> stolen token H just before the victim's logout lands keeps child H′ alive while the victim
> believes they logged out. H′ then persists **indefinitely**: rotation grants a fresh
> sliding-window expiry every time (`RefreshTokenService.cs:85-91`), neither revocation directory
> is fed (no device-revoke or password-reset event fires — ADR-0026/0027 key on those), the
> mobile logout's device-unregister only deactivates the device row without revoking refresh
> tokens (`UnregisterDevice.cs:36-41`), the thief's post-logout access tokens carry
> `iat > DeactivatedOn` and PASS the ADR-0026 `iat` guard, and the deactivated device row is
> hidden from the victim's Devices list (`IsActive` filter — ADR-0026 CH-2's "no re-revoke
> handle"). The ADR-0026/0027 ≤30s bounds buy **nothing** in this scenario. Exploitation requires
> prior theft of the raw refresh token (384-bit, unguessable), so this is marked **latent**.

## Decision (panel)

*Security-review panel, 2026-07-17. Every citation re-verified against the working tree.*

### AUTHOR position — treat it as the theft signal it is; chain-revoke like the refresh path

Presenting a rotated token IS the codebase's canonical theft signal: `RotateAsync`'s
rotation-reuse branch (`RefreshTokenService.cs:58-73`) chain-revokes everything and 401s. The
victim's logout intent is "end my session"; silently leaving H′ alive betrays exactly that intent
at exactly the moment the victim is trying to sever access. And the residual is the worst in the
auth surface: not ≤30s, not ≤30min, but *indefinite renewable persistence with no victim-visible
kill handle* (only password change/reset would catch it — and a victim who believes they logged
out has no reason to reach for either). Symmetry says: logout of a `RevokedReason == "rotated"`
token runs `RevokeChainAsync` (which is user-wide by implementation — the comment at
`RefreshTokenRepository.cs:39-42` says so explicitly) and we accept the availability edge cases.
The anti-probing objection is hollow: the **anonymous** `RefreshToken` endpoint
(`[AllowAnonymous]`, `Web.Mobile.Customer/Controllers/AuthController.cs:102-103`, same shared
`auth` rate window) already hands any holder of a once-valid rotated token the full account-wide
chain revoke as a side effect. Logout is `[Authorize]` — a strictly harder oracle to reach than
the one that already exists.

### CHALLENGER attack — the benign race is REAL on these exact clients; full chain-revoke converts routine logouts into fleet-wide sign-outs

The refresh-path symmetry argument fails on its own premise. Refresh-of-rotated is near-certain
theft because a well-behaved client *never* re-presents a rotated token to REFRESH. Logout-of-
rotated is different — **three of the four mobile surfaces capture the refresh token BEFORE an
authenticated call that can rotate it**, traced:

- **iOS (both apps, shared CleansiaCore):** `Auth.logout()` reads
  `tokenStore.current()?.refreshToken` at `Auth/Auth.swift:209`, THEN runs `preLogout?()` (the
  authenticated device-unregister) at `:217`, THEN POSTs the *captured* token at `:219`. If the
  access token is expired at logout (user opens the app after >30 min away and taps sign-out —
  a common path), the unregister 401s → the auth bridge refreshes → the stored token rotates →
  the logout presents the now-`rotated` predecessor.
- **Android customer:** identical ordering — read at `customer/core/auth/AuthRepository.kt:107`,
  authenticated unregister at `:114`, POST of the stale capture at `:122`.
- **Android partner:** reads *after* the unregister (`partner/data/auth/AuthRepository.kt:215-221`)
  so it dodges this interleave, but not a concurrent background call's 401-refresh.
- **Web SPA:** the browser snapshots the HttpOnly refresh cookie at request-send; a refresh
  landing in another tab between send and server-side processing makes the logout carry the
  rotated predecessor (`CookieAuthApiController.RefreshTokenFromCookieOrBody`,
  `Web.Customer/Controllers/AuthController.cs:103`).

Under the AUTHOR's design, every one of these routine logouts force-logs-out **all the user's
devices** (`RevokeChainAsync` is user-wide). That is an availability regression the refresh path
never had, triggered by the platform's own client code. Second attack: `Logout.Handler` never
checks that the presented token belongs to the JWT caller — making an already-revoked-token
presentation *side-effecting* would hand any authenticated account holding a victim's dead
rotated token a cross-user mass-logout primitive through a new door (bounded today only by the
pre-existing anonymous refresh oracle — "another door already exists" is not a reason to open a
second). Third: middle option (b), a grace window, is self-defeating — the motivating theft
("rotates just before the victim's logout lands") is same-instant and sits INSIDE any window, so
(b) protects nothing in exactly the case that motivates the ticket. Fourth: option (c),
log-and-alert only, fails the mission — H′ stays alive indefinitely; observability of a
persistence gap is not a fix for it.

### LEAD resolution — option (a), refined: SUCCESSOR-CHAIN revoke, ownership-gated, response-invariant

Both extremes lose. The AUTHOR is right that doing nothing leaves an indefinite, invisible
persistence gap the directory bounds do not cap; the CHALLENGER is right that the account-wide
theft response cannot be attached to a signal our own clients emit benignly. The resolution is
the middle option the forces list as (a), sharpened by one structural fact: **a rotation chain
never crosses sessions or devices** (`DeviceId` is carried across rotation,
`RefreshTokenService.cs:100`; `ReplacedByTokenId` links parent→child at `:104`). So "revoke the
successor chain of the presented token" is precisely "finish ending THE session this logout was
about" — which is the user's logout intent, no more and no less.

**Decided behavior — when `Logout` presents a token with `RevokedReason == "rotated"`:**

1. **Ownership gate first (S1/S3, host-agnostic in the handler):** resolve the caller via
   `IUserSessionProvider.GetUserId()` (every Logout endpoint is `[Authorize]` — verified
   Mobile.Customer `:118`, Mobile.Partner `:112`, Web.Customer `:97`; AC pins the rest). If the
   presented token's `UserId` differs from the caller (or the caller is unresolvable): **silent
   no-op, exactly today's behavior.** This closes the CHALLENGER's cross-user primitive entirely
   and requires NO DTO change (no `UserId` on the command, no NSwag regen, no manual steps).
2. **Walk `ReplacedByTokenId` forward** from the presented token (cycle-guarded, bounded) and
   revoke every still-live descendant with a distinct reason — proposed `"logout_chain"`
   (documented in the `RevokedReason` domain comment, `RefreshToken.cs:31-32`; must NOT be
   `"rotated"` — that would corrupt reuse detection — and must not collide with the ADR-0027
   poll predicate `"password_reset"`). Dead intermediate links keep their forensic marks.
3. **Ride the existing retry-on-conflict machinery** (re-run the walk on xmin collision, per the
   T-0419/T-0421 contract). On retry exhaustion, the fail-closed bulk fallback **widens to the
   user-wide scope** — principled escalation: five consecutive xmin collisions on one walk means
   someone is machine-gunning rotations against a logout, which IS theft-grade pressure; the
   benign client races above produce at most one already-committed rotation and can never reach
   exhaustion.
4. **The response is byte-identical in every branch** (200 `true` — unknown token, non-rotated
   revoked token, ownership mismatch, rotated-with-walk). Anti-probing is preserved: the only
   party who can observe the new side effect is the token's own account.
5. **`LogWarning` with `userId` + revoked-count only** (S6-clean, no token material) — the
   observability half of option (c), folded in: a spike outside the known client-race rate is
   the theft telemetry.

**Options adjudicated:** (a) **CHOSEN** (as refined above) · (b) grace window **REJECTED** —
defeats the motivating same-instant theft and would also nuke the same-instant benign races if
inverted; a tunable with no principled value · (c) log-only **REJECTED as the fix** (folded in as
telemetry) — leaves indefinite persistence · (d) full user-wide chain-revoke **REJECTED as the
default** — converts a proven-benign client race into an all-device sign-out; it survives only as
the exhaustion fallback where the contention itself proves an active adversary. **"Do nothing —
the bound is adequate" is REJECTED on evidence:** no ADR-0026/0027 bound covers this scenario
(nothing feeds the directories; the `iat` guard passes the thief's post-logout mints; sliding
expiry renews indefinitely; the Devices list hides the handle).

**Trade-off accepted (named):** in the benign races the walk revokes the successor the logging-out
client just stored — but that client is wiping locally in the same breath, so the end state equals
a clean logout, with a bonus correctness win: today the race leaves that freshly-minted successor
alive server-side after the client wiped (an orphaned live refresh token); the walk kills it.
Zero cross-device impact by construction (chains are per-session). Residual attacker window drops
from *indefinite + invisible* to *the in-flight duration of the logout request itself*; a thief
who rotates AFTER the victim's logout completes gets nothing (the chain tip is already dead and
`RotateAsync`'s reuse branch fires on any later re-presentation).

**Blast radius:** backend only — `RefreshTokenService.RevokeAsync` (+ `IRefreshTokenService`
signature gains the caller id), `Logout.Handler` (inject `IUserSessionProvider`), one new
forward-walk repository method (or a chain-scope predicate), the `RevokedReason` doc comment,
tests. No migration, no NSwag, no client change, no config change. The `auth` rate window and
`[Authorize]` posture on Logout are unchanged and load-bearing — do not weaken either.

## Acceptance criteria
- [ ] **AC1** — Logout presenting a `rotated` token owned by the caller revokes every live
  `ReplacedByTokenId` descendant (reason `logout_chain`); the live-token and unknown-token logout
  paths are byte-unchanged.
- [ ] **AC2** — ownership gate holds in the HANDLER (host-agnostic, S3): a caller presenting
  another user's rotated token gets today's silent no-op with zero side effects.
- [ ] **AC3** — the HTTP response is identical across all branches (no new oracle), and the only
  new log line is `LogWarning` with userId + count (S6: no token material, no PII).
- [ ] **AC4** — the walk rides the retry-on-conflict machinery; exhaustion falls back to the
  user-wide fail-closed bulk revoke (never a 500-with-tokens-alive, never a silent live session).
- [ ] **AC5** — no DTO/command shape change (`Logout.Command` stays `(string Token)`), no
  migration, `manual_steps` stays empty.
- [ ] **AC6** — all existing T-0419/T-0421 revocation tests and TC-REVOKE-* suites stay green,
  unedited.

## Test contract
- **TC-LOGOUT-CHAIN-1** — rotate H→H′; owner logs out with H → H′ revoked (`logout_chain`),
  response 200 identical to a normal logout.
- **TC-LOGOUT-CHAIN-2** — multi-hop: H→H′→H″; logout(H) → live tip H″ revoked; H′'s forensic
  `rotated` mark untouched.
- **TC-LOGOUT-CHAIN-3** — ownership: user B (valid JWT) logs out user A's rotated token → 200,
  every one of A's tokens untouched.
- **TC-LOGOUT-CHAIN-4** — anti-probing idempotency: unknown token, `logout`-revoked token,
  `password_reset`-revoked token, and a repeat of TC-1's call → all 200, no side effects; only
  `rotated` triggers the walk.
- **TC-LOGOUT-CHAIN-5** — sibling isolation: the user's OTHER device chain (different root +
  DeviceId) survives TC-1 alive — pins session-scoped, never account-wide.
- **TC-LOGOUT-CHAIN-6** — concurrency: a rotation racing the walk collides on xmin and the re-run
  catches the race-inserted child; exhaustion path lands the user-wide bulk fallback (red-proven
  with maxAttempts=1, the T-0419 mechanism).
- **TC-LOGOUT-CHAIN-7** — the theft headline: steal H, thief rotates (H′), victim logs out with
  stale H → thief's next `RefreshToken` call with H′ is rejected (`logout_chain`), and the
  rejection does NOT fire the rotation-reuse account-wide revoke spuriously (H′ is revoked, not
  rotated).

## Status log
- 2026-07-17 — INDEX row filed from T-0421 adversarial review F4 (`proposed`, low).
- 2026-07-17 — security panel deliberation (this file): AUTHOR full-chain-revoke position
  attacked and narrowed; verdict = successor-chain revoke, ownership-gated, response-invariant,
  with user-wide revoke retained only as the adversarial-contention fallback. Stays `proposed`
  pending PM scheduling; implementation is backend lane, re-verify by security on the diff.
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — shipped on feature/i18n-cluster-3 (merged): rotated-token logout chain-revokes along the success chain.
