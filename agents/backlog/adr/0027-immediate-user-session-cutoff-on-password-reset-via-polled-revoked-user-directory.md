# ADR-0027 — Password reset becomes ~immediate on mobile too: a sibling `RevokedUserDirectory` (keyed on `userId` alone) fed from the already-persisted `password_reset` refresh-token rows fails any mobile access token whose `iat` predates the reset, within the same ≤ 30 s polled bound as ADR-0026; password CHANGE is deliberately NOT accelerated

- **Status:** accepted (panel verdict 2026-07-15, amendments U1–U3 folded inline — see Verdict and
  the "Amendments folded at acceptance" section; **not** conditional on any new owner question — the
  only owner ratification in this neighbourhood is ADR-0026's ≤ 30 s-vs-literal-zero question,
  already in flight, which this ADR inherits rather than re-asks). Once accepted this is immutable —
  supersede, never edit.)
- **Date:** 2026-07-15
- **Supersedes:** — (extends ADR-0026; supersedes nothing. ADR-0026 D9.4 explicitly *excludes*
  user-level kills and names this extension as its own decision — instruction X1. This ADR is that
  decision. ADR-0024's 30-min mobile TTL and ADR-0026's device directory are **untouched and
  load-bearing** as the backstop and the sibling structure this one mirrors.)
- **Superseded by:** —
- **Applies to:** backend shared Core (`ChangePassword` reset handler — write-path stamp already
  exists; new `IRefreshTokenRepository.GetPasswordResetsSinceAsync`; new directory service) + the
  two mobile hosts' `OnTokenValidated` (one extra probe in the shared helper) | no client code
  change | **no schema migration** — the reset already persists a timestamped per-user revocation
  signal (`RefreshToken.RevokedReason = "password_reset"`, `RevokedAt`)
- **Ticket:** T-0418 (`security_touching: true`, priority high, owner-recovery path) · related:
  T-0407 (landed — reset revokes all refresh tokens), T-0414 (landed — the device directory this
  mirrors), ADR-0026 X1 (the named extension), ADR-0026 D9.4 (the exclusion this lifts by ADR)

> **One decision:** *how the access tokens an attacker already holds lose API access **now** instead
> of at the next expiry, on the password-reset (account-takeover recovery) path.* T-0407 makes reset
> revoke every refresh token (keep-none, reason `password_reset`), so the attacker cannot *renew* —
> but the outstanding access JWT still rides ≤ 30 min (ADR-0024 TTL). This ADR closes that last gap
> exactly as ADR-0026 closed it for device revocation, one dimension over: a new in-memory
> **`RevokedUserDirectory`** — a singleton snapshot of `(userId → resetAt)` for password resets
> younger than the access-token TTL, refreshed from Postgres every **≤ 30 s** per instance — is
> consulted in the same `OnTokenValidated` hook. A mobile token whose `iat` **predates** the user's
> most-recent reset fails authentication → **401** → the client's existing 401→refresh path hits the
> already-dead refresh token → token wipe + forced sign-out. **Zero new schema** (the poll reads the
> `password_reset` refresh-token rows T-0407 already writes), **zero request-path DB reads** (one
> extra O(1) dictionary probe next to the device probe), fail-open on the same TTL backstop.
> Password *change* is deliberately **not** accelerated (see D3). Once `accepted` this is immutable
> — supersede, never edit.

---

## Context

### The gap this closes (pre-analyzed in ADR-0026 as CH-13 / instruction X1)

T-0407 landed reset-time refresh revocation: `ChangePassword.Handler`
(`src/Cleansia.Core.AppServices/Features/Users/ChangePassword.cs:112-113`) calls
`RevokeAllForUserAsync(user.Id, "password_reset", exceptRawToken: null, ct)` — **keep-none**, because
the caller proves control via the emailed code, not a live session, so *every* session the old
credential minted (including the attacker's) is the thing being killed. That kills the attacker's
ability to *renew*. But the read side is the same hole ADR-0026 found for devices: the outstanding
access JWT is validated by signature + expiry only, and on the two mobile hosts that TTL is 30 min
(ADR-0024 D1). So on the account-takeover recovery path — the victim just changed their password to
lock out the intruder — the intruder keeps full API access for up to 30 minutes, reading whatever the
compromised account can read.

ADR-0026's own challenge pass named this exactly (CH-13) and its Verdict recorded it as **instruction
X1**: *"extend the directory with user-keyed entries fed by password reset … Owner of the decision:
architect (superseding/extending ADR); NOT T-0414 scope."* The pre-analysis is in ADR-0026 CH-13 and
the auth-sessions living doc. This ADR is X1.

### What already exists (all verified in the working tree, 2026-07-15)

**The write path is done and correct — nothing changes.** Reset revokes all refresh tokens with
`RevokedReason = "password_reset"` and stamps `RevokedAt = now` on each
(`RefreshTokenService.RevokeAllForUserAsync:135-144` → `RefreshToken.Revoke:91-97`). Crucially, this
means **a timestamped, per-user, persisted revocation signal already sits in Postgres** — the reset
instant is recoverable as `MAX(RevokedAt)` over that user's `password_reset` rows. No `User` column,
no migration, is needed to know "when did this user's sessions get reset."

**The device directory is the template, and it is live (T-0414).** ADR-0026's machinery is in the
tree and this ADR mirrors it piece-for-piece:
- `IRevokedDeviceDirectory` / `RevokedDeviceDirectory` — immutable-snapshot singleton, atomic
  reference swap, O(1) `IsRevoked(userId, deviceId, iat)`, the `iat` guard as the whole enforcement
  contract (`src/Cleansia.Config/Services/DeviceRevocation/RevokedDeviceDirectory.cs`).
- `RevokedDeviceDirectoryRefresher : BackgroundService` — un-killable loop (whole tick in try/catch,
  the ADR-0026 A3 property), one poll per `RefreshSeconds`, horizon = `AccessTokenExpMinutes` + 5 min,
  fail-open on poll fault with 3×-interval staleness warning
  (`RevokedDeviceDirectoryRefresher.cs`).
- `IDeviceRepository.GetDeactivatedSinceAsync(cutoff)` — the poll source, an `IgnoreQueryFilters()`
  cross-tenant background read (the sanctioned T-0245 pattern), projecting the three fields the
  directory keys on (`DeviceRepository.cs:46-57`).
- `DeviceRevocationTokenValidation.EnforceDeviceRevocation(this TokenValidatedContext)` — the one
  shared `OnTokenValidated` hook in `Cleansia.Config`, called by both mobile hosts
  (`ServiceExtensions.cs:188` in each), reads the `device_id` claim + `iat`, consults the directory,
  `context.Fail("device_revoked")` on a match.
- `DeviceRevocationOptions` (`DeviceRevocation:Enabled`, `RefreshSeconds`) — both security bounds,
  raw-file test-pinned; `Enabled=false` no-ops the consumer but the pump keeps polling (A5).

**The `iat` claim is already minted and already read.** Both mint sites use
`JwtSecurityTokenHandler.CreateToken`, which stamps `iat`/`nbf`/`exp` by default
(`TokenService.cs:77`, `RefreshToken.cs:128`), and the device helper already parses it
(`DeviceRevocationTokenValidation.ReadIssuedAt:63-72`). The user check reuses the *same* `iat` off
the *same* principal — no new claim, no mint-site change.

**The refresh-token repository already speaks the exact poll idiom.** `RefreshTokenRepository` does
its cross-tenant reads with `IgnoreQueryFilters()` and per-user scoping already
(`GetActiveByUserIdAsync:25-35`, `GetByTokenHashAsync:10-23`, comments explaining the
anonymous-issue / tenant-null rationale). A new `GetPasswordResetsSinceAsync(cutoff)` is the same
shape: `IgnoreQueryFilters()`, `WHERE RevokedReason == "password_reset" && RevokedAt >= cutoff`,
grouped to `(UserId → MAX(RevokedAt))`.

### Why the userId-keying objection that blocked device revocation does NOT apply here

ADR-0026 refused a userId-keyed *device* deny (its Context: "keying the deny decision on userId
alone would 401 **all** the user's devices — including the very device they are holding while
performing the revoke"). That objection is specific to device revocation, where the intent is to kill
*one* device and spare the current one. **Reset is the opposite: keep-none is the intent** — the
recovery playbook is "kill everything the old credential minted." So userId is exactly the right key
here, and there is no "current device to spare." The one session that legitimately survives a reset
is the *new* one the user creates by logging in afterward — and that is handled by the same `iat`
guard the device directory already uses: the post-reset login's token carries `iat > resetAt` and
passes, even while the user's directory entry is still present. No spared-session bookkeeping is
needed on the reset path.

---

## Decision

### D1 — A sibling `RevokedUserDirectory`, keyed on `userId` alone (option a), NOT folded into the device directory (option b)

New singleton `IRevokedUserDirectory` (impl `RevokedUserDirectory`) + companion
`RevokedUserDirectoryRefresher : BackgroundService`, registered by a new shared extension
`AddUserRevocationEnforcement()` in `Cleansia.Config`, **called only by the two mobile hosts** (the
ADR-0001 §D4 shared-registration precedent, exactly as `AddDeviceRevocationEnforcement`). It is a
structural clone of the device directory, one key narrower:

- **Contract:** `IsRevoked(string userId, DateTimeOffset? tokenIssuedAt)` → true iff a snapshot entry
  exists for `userId` and `tokenIssuedAt` is null (unprovable age — A2 parity) or strictly precedes
  the recorded `resetAt`. A token minted after the reset (the post-reset re-login) passes even while
  the entry is present. Pure O(1) dictionary lookup, zero I/O, zero locks, zero clock reads.
- **Snapshot:** `userId → resetAt` (immutable dictionary behind a single volatile reference, atomic
  swap on `Replace`), holding one entry per user with a `password_reset` revocation younger than the
  horizon. Latest-wins on `Replace` (a user reset twice inside the horizon keeps the later instant —
  the same guard `RevokedDeviceDirectory.Replace:36-42` already implements).
- **Horizon = `AccessTokenExpMinutes` + 5 min slack**, identical to the device directory: a reset
  older than the TTL cannot have a live access token predating it, so the snapshot never grows
  beyond "platform-wide password resets in the last ~35 minutes" — a handful of rows at any real
  scale.

**Why a sibling (option a) and not a null-deviceId entry in the same directory (option b).** Option
(b) is less new type-surface but it corrupts the device directory's contract and CRC boundary:
- `RevokedDeviceDirectory`'s key is a **composite** `(userId, deviceId)`; a "null deviceId means all
  the user's tokens" entry forces the device probe to do *two* lookups per request (probe
  `(user, deviceX)` **and** probe `(user, ⌀)`), inverting the single-composite-key contract that
  every one of its unit tests asserts (`RevokedDeviceDirectoryTests.cs`).
- The device directory's CRC card explicitly lists **"Users"** under *does NOT know* (ADR-0026 D9.4
  boundary — `agents/knowledge/roles/revoked-device-directory.md:47-48`). Folding user semantics in
  makes that card lie, and mixes two poll sources (the `Devices` table and the `RefreshTokens`
  table) into one pump — a role smell.
- A sibling keeps each directory's key-space, poll source, refresher, and test suite clean and
  independent; the only shared touch is the `OnTokenValidated` helper doing one *additional* O(1)
  probe. Two tiny pumps, two tiny snapshots, zero coupling. **Option a chosen.**

Option (c) — a `User.SessionsRevokedAt` column + a `security_stamp`/token-version claim — is rejected
outright: it needs a schema migration and a mint-site change to add the claim, buying nothing the
`iat`-vs-persisted-`resetAt` comparison doesn't already give for free. The `password_reset`
refresh-token rows T-0407 writes *are* the security stamp; we don't need a second one. (Full
why-not in Alternatives.)

### D2 — Fed from the already-persisted `password_reset` refresh-token rows — no migration

The refresher polls a new `IRefreshTokenRepository.GetPasswordResetsSinceAsync(cutoff)`:

```
IgnoreQueryFilters()                         // background, tenant-less, cross-tenant-by-design (T-0245 pattern)
  .Where(t => t.RevokedReason == "password_reset" && t.RevokedAt != null && t.RevokedAt >= cutoff)
  .GroupBy(t => t.UserId)
  .Select(g => new UserPasswordReset(g.Key, g.Max(t => t.RevokedAt!.Value)))
```

`MAX(RevokedAt)` per user is the reset instant (all rows for one reset share the same `now`; taking
the max is robust to a second reset inside the horizon). The reason string `"password_reset"` is the
exact literal `ChangePassword.Handler` writes (`ChangePassword.cs:113`) and that `RefreshToken`
documents as a valid `RevokedReason` (`RefreshToken.cs:31-32`). **No new column, no migration** — this
is the decisive win of option (a): the reset already left a timestamped, per-user, durable marker in
a table the refresher can read with the idiom the repo already uses.

The refresher is a structural clone of `RevokedDeviceDirectoryRefresher`: un-killable loop (whole
tick inside the loop's try/catch — the ADR-0026 A3 property, non-negotiable so a poll bug can't
`StopHost` the mobile API), `TimeProvider`-driven horizon and delay, one synchronous initial fill
(empty-on-failure), fail-open on fault with the 3×-interval staleness warning as the only ops signal.

### D3 — Password CHANGE is deliberately NOT accelerated (the scoping decision)

This ADR accelerates **RESET only**. Password *change* (`ChangeOwnPassword`, the authenticated
self-service path) also revokes the user's other refresh tokens (T-0407, reason `password_changed`,
`ChangeOwnPassword.cs:83-84`) **but spares the caller's own session** via `exceptRawToken`. Three
reasons change stays on the TTL:

1. **Threat model.** Change is performed *by an authenticated caller who already holds a live
   session* — it is credential hygiene, not takeover recovery. Reset is the *unauthenticated*
   recovery path (email-code proof, `[AllowAnonymous]` on every host), where the whole point is that
   an attacker holds sessions you are trying to kill. Instant cutoff is a recovery-from-compromise
   control; change does not sit on that path.
2. **The spared-session self-heal is not free under acceleration.** Change spares exactly one refresh
   token (the caller's). If change fed the user directory with `resetAt = now`, the caller's *own*
   outstanding access token (minted before the change, `iat < now`) would 401 on its very next
   request — then self-heal via its *spared* refresh token in one silent round trip (the D3.3
   self-heal ADR-0026 relies on). That is a gratuitous extra round trip and a momentary 401 on the
   happy path of a routine password change, for a session that is *already the trusted one*. Reset
   has no spared session, so it has no self-inflicted 401.
3. **`MAX(RevokedAt)` over `password_changed` rows would also catch the spared session's *sibling*
   rotations**, and distinguishing "the change caller's token" inside the directory (which sees only
   `userId → instant`, never `iat`-of-the-spared-token) is impossible without leaking the spared
   token's identity into the snapshot — a coupling the directory must not have.

So: the reset dimension keys off `RevokedReason == "password_reset"` **only**, never
`password_changed`. If a future threat argument wants change accelerated too, it is a *separate*
decision (it must first answer the spared-session 401), recorded as a named non-goal here, not folded
in. **Change stays bounded by the ≤ 30-min TTL + the already-revoked sibling sessions**, which is the
correct posture for credential hygiene by an authenticated holder.

### D4 — Enforcement point, failure key, and the 401 are ADR-0026's, unchanged

The check plugs into the **same** shared `OnTokenValidated` helper the device check uses (the two
mobile hosts already call `context.EnforceDeviceRevocation()` at `ServiceExtensions.cs:188`). The
helper gains a second probe after the device probe:

```
var userId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;   // already read for the device probe
if (!string.IsNullOrEmpty(userId)
    && userDirectory.IsRevoked(userId, ReadIssuedAt(identity)))
{
    context.Fail(FailureReason);   // "session_revoked" — a 401, never a 403
}
```

- **The key is `sub` (`ClaimTypes.NameIdentifier`), which EVERY token carries** — unlike `device_id`
  (mobile-only, conditional). This is the claim-transition confirmation the ticket asks for: because
  the reset check keys on a claim every access token has always minted, **there is no claim-less
  transition window and no rollout grace to reason about** (contrast ADR-0026 D6, which had to bound
  the pre-`device_id` window at the TTL). Every live mobile token — issued before or after this
  deploys — carries `sub` and `iat`, so the check is fully effective from the first request after
  deploy. Web-shaped tokens on mobile hosts also carry `sub`; see D6 for why they are still governed
  correctly by the `iat` guard.
- **401 not 403**, `context.Fail("session_revoked")`, for the identical reason ADR-0026 D3 gives: the
  401 is what both mobile clients convert into single-flight refresh → and the refresh for a reset
  user is *already dead* (`password_reset` → `InvalidRefreshToken`, `RefreshToken.cs:75`) → token wipe
  + forced sign-out (`AuthAuthenticator.kt`, `SessionRefresher.swift`). Zero client change. A distinct
  failure reason string (`"session_revoked"` vs the device helper's `"device_revoked"`) keeps the two
  causes distinguishable in logs; both produce the same client-visible 401.
- **Kill switch:** the user check honors the *same* `DeviceRevocation:Enabled` flag the device check
  reads (D8 discussion), so one ops switch disables both mobile revocation checks together — see D7
  for why a shared switch, and the config-pin note.

### D5 — Fail-open on the last snapshot, on the same TTL backstop

Identical posture to ADR-0026 D4, and permanent for the same reason: **the worst case of fail-open is
exactly the world before this ADR** — reset-time access-token cutoff degrades back toward the ≤ 30-min
TTL (which still kills the attacker's session, because their refresh chain is already dead so they
cannot renew past it). Fail-closed is rejected: a Postgres blip would 401 the whole mobile fleet →
every client refreshes → refresh also fails (same outage) → fleet-wide forced sign-out through the
still-partially-conflated refresh path (ADR-0026 A6). Bounded degradation beats amplified outage; the
30-min TTL is what makes fail-open safe here just as it does for devices.

### D6 — Interaction with the device directory's horizon, and web-shaped tokens

- **Shared horizon, independent snapshots.** Both directories size their horizon off
  `AccessTokenExpMinutes + 5 min`; they poll different tables into different snapshots. There is no
  cross-interaction — a user can appear in both (a device revoked *and* a password reset), and both
  checks fire independently; whichever matches first `context.Fail`s. Order in the helper is
  irrelevant (both are pure O(1)); the device probe stays first only to preserve the existing
  `device_revoked` log reason for the device case.
- **Web-shaped tokens on mobile hosts.** A mobile-host login that never sent `X-Device-Id` mints a
  token with `sub` + `iat` but no `device_id` (the D9.2 residue). The device check can never match it
  (no device claim), but the **user check governs it correctly**: it carries `sub`, so after a reset
  its `iat < resetAt` and it 401s. This ADR therefore *closes* part of the ADR-0026 D9.2 claim-less
  residue for the reset case — a claim-less mobile session is still cut off on reset because the
  reset key is `sub`, which it has.

### D7 — Config: reuse `DeviceRevocation:Enabled`, add nothing new that can drift

The user check reads the **existing** `DeviceRevocation:Enabled` flag (no new config key). Rationale:
these are two facets of one product capability — "the mobile hosts enforce session revocation at
token validation" — and an operator hitting the kill switch in an incident wants *both* off with one
flip, not a matrix of half-on states. `RefreshSeconds` is likewise shared (one poll cadence for both
pumps; the user pump uses the same value). **No new appsettings key ships**, so the existing
TC-REVOKE-NOW-7 raw-file pin already covers the security bound; the only addition to the config-pin
test is asserting the value still governs (it does — same key). If a future need for independent
tuning appears, splitting `UserRevocation:*` out is a mechanical follow-up; today a shared switch is
the smaller, safer surface. *(This is an explicit panel checkpoint — a challenger may argue for a
distinct `UserRevocation:Enabled`; see the pre-answers.)*

### D8 — Web hosts are explicitly OUT of scope, deferred to the standing web-TTL follow-up

The three web hosts (`Web.Partner`, `Web.Admin`, `Web.Customer`) install **neither** revocation
directory and carry the 1440-min TTL. On the web reset path the attacker's access token (a JWT inside
an HttpOnly cookie whose `Expires` is pinned to the refresh expiry — ADR-0024 Context) rides up to 24
h. That is a real residue — but it is the *same* web-TTL exposure ADR-0024 D4.3 already carved out as
its own decision (admin host first/separable), gated on verifying the SSR/cookie refresh path. This
ADR does **not** fold web enforcement in: doing so would (a) install a per-request-adjacent directory
on hosts whose refresh path is still moving (T-0400), and (b) couple this reset-cutoff decision to the
unverified web seam — the exact coupling the per-audience host split exists to prevent. **Named
follow-up, not silent scope:** the web reset-cutoff rides the standing web-host TTL decision
(ADR-0024 D4.3); until it lands, the documented web recovery bound remains the TTL + the already-dead
refresh chain (attacker cannot renew; the residual window is the access-cookie's remaining life).

### D9 — Accepted residues (explicit)

1. **≤ 30 s residual access after reset** (+ in-flight request completion), on mobile — the same
   bound ADR-0026 accepted for devices, from the same poll interval. The literal-zero escalation is
   the same named read-through swap behind the interface (Alternatives (a)), inheriting ADR-0026's
   open owner question — this ADR does not re-ask it.
2. **Password CHANGE is not accelerated** (D3) — bounded by the TTL + the revoked sibling sessions;
   a deliberate non-goal, not an oversight.
3. **Web reset cutoff rides the TTL** (D8) — deferred to the web-host TTL follow-up (ADR-0024 D4.3),
   not endorsed.
4. **Same-second reset↔re-login clock ambiguity** — `iat` has one-second resolution; a post-reset
   login within ~1 s of the reset stamp may 401 once and self-heal via its live refresh token in one
   round trip (ADR-0026 D3.3 / D9.3 parity). The skew false-*pass* direction is likewise bounded: a
   token minted just before the reset on a fast-clock instance may carry `iat ≥ resetAt` and pass —
   but its refresh chain was revoked at reset time so it cannot renew, and the TTL backstop kills it
   ≤ 30 min later (ADR-0026 D9.3 parity — same app-server clocks under NTP).
5. **Fail-open staleness under DB outage**, ceiling = the TTL (D5).
6. **`RefreshTokens` poll cost:** one small indexed read per instance per interval over
   `password_reset` rows in the last ~35 min — resets are rare human actions behind the `auth` rate
   bucket, so the snapshot cannot spike beyond human-action rates. **Standing rule (mirrors ADR-0026
   D9.6):** any future *bulk* `password_reset` write path (e.g. a mass forced-reset admin action)
   inflates this snapshot and triggers a fleet-wide silent-refresh ripple — it must be checked
   against this ADR before shipping. No index ships; `DeleteStaleAsync` already prunes revoked rows
   (`RefreshTokenRepository.cs:63-75`), and the horizon caps what the poll scans. If the predicate
   ever shows in `pg_stat`, a partial index on `(RevokedReason, RevokedAt)` is a one-line follow-up
   owner migration.
7. **`DeleteStaleAsync` horizon interaction:** the cleanup job deletes revoked tokens older than its
   cutoff (`RefreshTokenRepository.cs:63-75`). As long as that cutoff is ≥ the directory horizon
   (TTL + 5 min ≈ 35 min — and the cleanup runs on a far coarser schedule, deleting rows revoked
   *days* ago), a `password_reset` row is never pruned before its horizon lapses, so the poll can
   never miss a live-window reset. Recorded so a future aggressive-cleanup change is checked against
   the horizon.

---

## Alternatives considered

- **(a) Sibling `RevokedUserDirectory` keyed on `userId`, fed from `password_reset` refresh-token
  rows.** **CHOSEN.** Reuses ADR-0026's exact polled-directory + `iat`-guard machinery one key
  narrower; zero schema change (the reset already persists the timestamped signal); clean CRC
  boundary; one extra O(1) request-path probe. The `iat` guard makes the post-reset re-login pass for
  free, so no spared-session bookkeeping.
- **(b) Fold userId entries into the SAME `RevokedDeviceDirectory` (a null/absent deviceId entry =
  "all this user's tokens").** Rejected: less new type-surface, but it (1) breaks the single
  composite-key `IsRevoked` contract every device unit test asserts (device probe would need a second
  `(user, ⌀)` lookup), (2) makes the device directory's CRC "does NOT know Users" boundary a lie and
  mixes two poll sources (`Devices` + `RefreshTokens`) into one pump, (3) couples two independently
  evolving lifecycles for the sake of one saved class. The saving is illusory; the coupling is real.
- **(c) A `security_stamp` / token-version claim bumped on reset (new `User` column + migration).**
  Rejected: needs a schema migration *and* a mint-site change to add and read a new claim, to
  reproduce a comparison the existing `iat`-vs-persisted-`resetAt` already gives for free. The
  `password_reset` rows T-0407 writes are already the per-user, timestamped "stamp" — a version
  column would be a redundant second source of truth that can drift from the refresh-revocation it is
  supposed to track. (A token-version claim shines when you have *no* server-side session store to
  consult per request; we have Postgres and a polled snapshot, so it buys nothing and costs a
  migration + a rollout claim-transition window this design otherwise avoids entirely.)
- **(d) Accelerate password CHANGE too (same directory, `password_changed` rows).** Rejected as the
  default (D3): change is authenticated credential hygiene, not takeover recovery; feeding it in
  self-inflicts a 401 + extra refresh on the change caller's own spared session; distinguishing the
  spared session inside a `userId → instant` snapshot is impossible without leaking its identity.
  Named as a separate future decision, not folded.
- **(e) Per-request DB check of the user's latest `password_reset` instant (read-through).** Rejected
  as the default for the same reason ADR-0026 rejected literal-zero device checks: one Postgres read
  on every authenticated mobile request forever, to shave ≤ 30 s off a bound no human perceives on a
  recovery path. Kept as the named escalation behind `IRevokedUserDirectory` (swaps in without
  touching hosts/claims/clients) — inherits ADR-0026's open owner "is ≤ 30 s immediate enough"
  question; this ADR does not re-open it.
- **(f) Push-driven forced sign-out on reset.** Rejected as a security bound, permanently: a hostile
  holder ignores a push (ADR-0024 D3-C / ADR-0026 Alt (d)). It is a UX complement that could make the
  attacker's device visibly sign out in seconds — layers over this ADR, never substitutes.
- **(g) Do nothing (reset already kills refresh; the TTL bounds the rest).** Rejected: that is the
  status quo — up to 30 minutes of attacker access on the recovery path the owner cares about, and
  the precise gap ADR-0026's Verdict named as X1. Leaving a named instruction unbuilt makes the
  escalation ladder folklore.

---

## Consequences

**Cheaper / safer:**
- Password reset now ends the attacker's mobile access within **≤ 30 s** (from ≤ 30 min), with the
  sign-out riding the existing 401→refresh→forced-sign-out machinery — no client release, no
  migration.
- The reset check keys on `sub` (every token has it), so it is **fully effective from the first
  request after deploy** — no claim-transition window, unlike the device rollout.
- Claim-less mobile sessions (ADR-0026 D9.2 residue) *are* cut off on reset here (D6) — a bonus
  narrowing of that residue for the recovery case.
- The `IRevokedUserDirectory` seam makes the literal-zero escalation a one-file read-through swap,
  exactly as the device seam does.
- Web hosts, web clients, Functions: byte-untouched. Per-audience host coupling: none.

**More expensive (accepted):**
- One more `BackgroundService` + singleton per mobile host instance, and one more small indexed poll
  per instance per interval (over `password_reset` rows in the horizon — trivially small; D9.6).
- One extra O(1) dictionary probe on the mobile `OnTokenValidated` path (next to the device probe) —
  memory-only, no I/O.
- A new standing rule to maintain: bulk `password_reset` writes must be checked against this ADR
  (D9.6) — enforced by the same "future bulk job" discipline ADR-0026 D9.6 established.

**No migration** (the `password_reset` rows and `RevokedAt` column already exist and are mapped),
**no NSwag** (no DTO/endpoint change), **no client change**, **no Bicep change** (config reuses the
existing `DeviceRevocation` section).

---

## How a reviewer verifies compliance

**Mechanical:**
1. `AddUserRevocationEnforcement()` lives in `Cleansia.Config`, is called by exactly the two mobile
   hosts' `ServiceExtensions`, and appears **nowhere** in `Web.Partner`/`Web.Admin`/`Web.Customer`
   (their `ServiceExtensions.cs` stay byte-identical on this axis).
2. The new `IRefreshTokenRepository.GetPasswordResetsSinceAsync` uses `IgnoreQueryFilters()` (cite
   the cross-tenant-background-read comment, mirroring `RefreshTokenRepository.cs:15-19`), filters on
   `RevokedReason == "password_reset"` **only** (never `password_changed` — D3), and projects
   `(UserId, MAX(RevokedAt))`. **Its `GroupBy` + `Max(RevokedAt!.Value)` aggregate is proven to
   translate on real Postgres by a Testcontainers integration test (TC-REVOKE-USER-9), not only an
   in-memory fake** — the panel's U4 pin against this codebase's group-by/aggregate/null-projection
   500 lesson.
3. The request-path check performs no I/O: `RevokedUserDirectory.IsRevoked(...)` is a pure snapshot
   lookup; the only DB access lives in `RevokedUserDirectoryRefresher`.
4. The shared `OnTokenValidated` helper reads `sub` + `iat` off the already-validated principal (no
   new claim minted; `TokenService.cs` / `RefreshToken.cs` mint sites **untouched**) and fails with
   a 401 (`context.Fail("session_revoked")`), never a 403.
5. The refresher loop is un-killable (whole tick inside the loop's try/catch — the ADR-0026 A3
   property) and uses `TimeProvider`, never `DateTime.UtcNow`.
6. `ChangeOwnPassword` (`password_changed`) is **not** wired into any directory feed — grep confirms
   the poll predicate is `password_reset` alone (D3 pinned).
7. All ADR-0026 device tests (TC-REVOKE-NOW-1..9) and the ADR-0024 TTL tests (TC-REVOKE-TTL-1..5)
   stay green and unedited — this ADR adds a sibling, it does not touch the device path.

**Test contract (T-0418 — names for the backend ticket):**
- **TC-REVOKE-USER-1 — the headline property.** HostTests, mobile host, short `RefreshSeconds` (or the
  refresher's public `RefreshOnceAsync` forced): login (mobile) → authed call 200 → password reset
  for that user (revokes refresh tokens, `password_reset`) → force a directory refresh → **the same,
  unexpired pre-reset access token → 401** (not 403). An unrelated user's token → still 200.
- **TC-REVOKE-USER-2 — post-reset re-login passes (the `iat` guard), incl. the same-second boundary
  (panel U1).** Reset user → re-login (new token, `iat > resetAt`) **while the directory entry is
  still present** → new token 200. Pins that reset is a *session* kill, not a user ban. **Boundary
  case (U1):** a post-reset re-login whose `iat` truncates into the reset's *same wall-clock second*
  (so `iat < resetAt` sub-second) 401s **once** and then **self-heals** — its refresh token (minted
  after the reset, never in the keep-none revoke set) is alive, refresh succeeds, the retried request
  200s. Pins "no lockout on the recovery path" as a proven property, not an assertion.
- **TC-REVOKE-USER-3 — unrelated users unaffected.** Two users; reset user A → A's pre-reset token
  401s, B's token stays 200 across the refresh. Pins the key is `sub`, scoped to the reset user only.
- **TC-REVOKE-USER-4 — pure request-path + fail-open + un-killable loop (perf/robustness pin).** Unit
  (parity with `RevokedDeviceDirectoryTests` + `RevokedDeviceDirectoryRefresherTests`): (i) a counting
  repository fake proves **zero repo calls from `IsRevoked`** across N lookups; (ii) the refresher's
  fake throws on *consecutive* ticks → directory keeps serving the last snapshot, the refresher keeps
  attempting the next tick, and the staleness warning fires past `3 × RefreshSeconds`.
- **TC-REVOKE-USER-5 — the `iat`/latest-wins directory unit contract.** Mirror the device unit suite:
  token before reset → revoked; token after reset → passes; unreadable `iat` matching an entry →
  revoked (A2 parity); a user reset twice keeps the later instant (latest-wins), so a session minted
  between the two resets is still killed.
- **TC-REVOKE-USER-6 — password CHANGE does NOT accelerate (D3 pin).** Authenticated
  `ChangeOwnPassword` for a user → force a directory refresh → the user's OTHER pre-change access
  tokens are **not** 401'd by the user directory (they die by TTL + their revoked refresh chain), and
  the change caller's own token is untouched. Pins the `password_reset`-only feed predicate.
- **TC-REVOKE-USER-7 — config pin (raw-file, TC-REVOKE-NOW-7 mechanism).** The four mobile appsettings
  still carry `DeviceRevocation:Enabled = true` and `RefreshSeconds ≤ 30` and those values govern the
  user check too (shared switch, D7); the three web hosts install neither directory (grep-pinned in
  the host-wiring test). Changing the flag/cadence fails a test until a superseding ADR.
- **TC-REVOKE-USER-8 — claim-less mobile session cut off on reset (D6).** A mobile login without
  `X-Device-Id` (no `device_id` claim) whose user is then reset → its pre-reset access token 401s
  (keyed on `sub`, which it carries). Pins the bonus D9.2-residue narrowing.
- **TC-REVOKE-USER-9 — Postgres-backed poll translation (panel U4).** A **real-Postgres**
  (Testcontainers) integration test on `IRefreshTokenRepository.GetPasswordResetsSinceAsync`: seed
  `password_reset` rows for two users (one reset twice, `password_changed` and `logout` rows as
  negatives) → assert the method returns exactly `(UserId, MAX(RevokedAt))` for the `password_reset`
  rows inside the cutoff and **omits** `password_changed`/`logout`/out-of-horizon rows — proving the
  `GroupBy` + `Max(RevokedAt!.Value)` aggregate over a nullable column translates server-side rather
  than 500-ing on real Postgres (this codebase's standing group-by/aggregate/null-projection lesson).

---

## Living docs updated with this ADR (proposed state; finalized at acceptance)

- `agents/architecture/decisions/auth-sessions.md` — updated to **proposed** for this dimension: the
  bound table gains a user-revocation row, the escalation ladder gains the user-directory rung, X1 is
  moved from "open follow-up" to "proposed (ADR-0027)".
- `agents/knowledge/roles/revoked-user-directory.md` — **created** (CRC card for the new singleton,
  the one new role this ADR introduces; sibling to `revoked-device-directory.md`).
- **Catalog edit at acceptance (not before):** `agents/knowledge/security-rules.md` — the ADR-0024/
  0026 token-lifetime paragraph (S2 neighborhood) gains: *"Password reset ends the reset user's
  mobile sessions within the same ≤ 30 s bound (ADR-0027) via a sibling `RevokedUserDirectory` keyed
  on `sub` and fed from the persisted `password_reset` refresh-token rows; password CHANGE is
  deliberately not accelerated. The shared `DeviceRevocation:Enabled`/`RefreshSeconds` bounds govern
  both mobile revocation checks."*
- ADR-0026's `Superseded by:` line is **not** touched (this ADR extends, it does not supersede
  ADR-0026); the one pointer edit is optional — a *"Related: ADR-0027 (user-level extension, X1)"*
  note may be added to ADR-0026's X1 line context via the living doc, not the immutable ADR.

---

## Challenges pre-answered (author's anticipation — the panel writes below)

| # | Expected challenge | Author's position |
|---|---|---|
| P-1 | "Just fold it into the device directory — one class less (option b)." | Rejected in D1/Alt(b): breaks the composite-key `IsRevoked` contract (two probes per device request), lies to the device CRC card's "does NOT know Users" boundary, mixes two poll sources into one pump. The sibling costs one extra O(1) probe and keeps both clean. The saving is illusory. |
| P-2 | "Accelerate password CHANGE too — same recovery value." | Rejected in D3: change is authenticated hygiene, not takeover recovery; it self-inflicts a 401 + extra refresh on the caller's *own* spared session; the spared session can't be distinguished inside a `userId→instant` snapshot without leaking its identity. Named as a separate future decision. |
| P-3 | "A `security_stamp` claim is the standard pattern — do (c)." | The standard pattern is for systems with *no* per-request server-side session store; we poll Postgres. The `password_reset` rows are already the timestamped stamp — (c) adds a migration + a mint-site claim + a claim-transition rollout window, to reproduce the `iat`-vs-`resetAt` compare we get free. |
| P-4 | "Claim-transition grace like ADR-0026 D6?" | None needed — confirmed in D4/D6: the key is `sub`, which every token has always minted. No pre-claim window exists; the check is effective from the first post-deploy request. This is the material simplification over the device rollout. |
| P-5 | "Shared `DeviceRevocation:Enabled` couples two features to one switch." | Deliberate (D7): they are two facets of one capability ("mobile hosts enforce session revocation at validation"); an operator wants one kill switch in an incident. No new appsettings key ships, so the existing raw-file pin covers it. Splitting is a mechanical follow-up if independent tuning is ever needed — an explicit checkpoint for the panel. |
| P-6 | "Web reset still leaks up to 24 h — you skipped the scarier host." | Out of scope by the threat/seam test (D8), same as ADR-0024 D4.3: web enforcement rides the standing web-host TTL decision (admin-first/separable), gated on the SSR/cookie refresh-path verification (T-0400). Folding it here couples this reset decision to an unverified seam. Named follow-up, not silent omission. |
| P-7 | "Fail-open means a Postgres blip reopens the attacker's window." | Bounded degradation (D5): worst case = the pre-ADR world (≤ 30-min TTL), and the attacker's refresh chain is *already dead* so they can't renew past it. Fail-closed converts a DB blip into fleet-wide forced sign-out. Same ADR-0026 A6 argument, same TTL backstop. |
| P-8 | "`DeleteStaleAsync` could prune a `password_reset` row before the poll sees it." | No (D9.7): the cleanup cutoff is far coarser than the directory horizon (deletes rows revoked *days* ago; horizon is ~35 min), so a live-window reset row is never pruned early. Recorded so a future aggressive-cleanup change is checked against the horizon. |

## Challenge

*(Architect panel, challenger mode, 2026-07-15. Every citation below independently re-verified against
the working tree — none taken from the draft on trust. The author's five declared open points are all
addressed; CH-U1 is the challenger's own find. Confirmed structural facts first, so the panel does not
re-derive them:*
- *ADR-0026's device machinery is live in the tree exactly as described — `RevokedDeviceDirectory`
  (immutable snapshot, atomic swap, O(1) `IsRevoked`, `tokenIssuedAt is null || iat < revokedAt`
  guard), `RevokedDeviceDirectoryRefresher` (un-killable loop, whole tick in try/catch,
  `TimeProvider`, TTL+5 min horizon, fail-open + 3×-interval warn), `DeviceRevocationTokenValidation`
  (the shared `OnTokenValidated` helper, wired at `Web.Mobile.Customer/ServiceExtensions.cs:191` —
  the ADR's `:188` is stale by 3 lines, cosmetic), `DeviceRevocationOptions` (`Enabled`,
  `RefreshSeconds`), `AddDeviceRevocationEnforcement`. The clone target is real.*
- ***Exactly two production access-token mint sites exist*** *(`TokenService.GenerateAccessToken:64-79`,
  `RefreshToken.Handler.GenerateAccessToken:116-130`), both routed through `AuthExtensions.SetClaims`,
  which unconditionally yields `ClaimTypes.NameIdentifier` (= `sub`) first (`AuthExtensions.cs:19`).
  A repo-wide grep for a third `CreateToken`/`SecurityTokenDescriptor` finds only `TestJwtFactory`
  (test-only, same shape). No impersonation / act-as / client-credentials user-token path exists
  (grep: the only `service_account` hits are the FCM push-credential JSON). Open point 5 is clean.*
- *The poll source is real and needs no migration: `ChangePassword.Handler:112-113` writes
  `RevokeAllForUserAsync(user.Id, "password_reset", exceptRawToken: null)` → each active token
  `.Revoke("password_reset", now)` stamping `RevokedAt = DateTimeOffset.UtcNow`
  (`RefreshTokenService.cs:152-161` → `RefreshToken.cs:91-97`). The `"password_reset"` literal and the
  `RevokedReason` domain are documented at `RefreshToken.cs:31-32`.)*

**CH-U1 — BLOCKING-CANDIDATE (open point 4, sharpened): the same-second false-FAIL is a
false-FAIL, and its self-heal is real — but the ADR must pin that the recovery is architecturally
guaranteed, not merely likely.** Verified precisely: `RevokedAt` is a sub-second `DateTimeOffset`
(`RefreshTokenService.cs:156`), whereas `iat` is a **whole-second** NumericDate — `ReadIssuedAt`
parses it via `DateTimeOffset.FromUnixTimeSeconds` (`DeviceRevocationTokenValidation.cs:63-72`),
truncating **down**. The guard is strict `<` (`RevokedDeviceDirectory.cs:26`). So the recovery-path
sequence *victim resets at 12:00:05.200 → re-logs in at 12:00:05.900* mints a token with
`iat = 12:00:05.000`, and `12:00:05.000 < 12:00:05.200` is **true** → the legitimate post-reset
session is **401'd on its first request**. This is the load-bearing direction for a *recovery* ADR:
the whole point is the victim getting back in. The design's answer is self-heal — the 401 drives a
refresh, and the post-reset re-login's *refresh* token was minted **after** the reset revocation, so
it survived (`RevokeAllForUserAsync` only touched tokens active at reset instant), refresh succeeds,
retry carries a fresher (or now-later-second) `iat`. **I verify this holds** (the new refresh token
is untouched by the earlier keep-none revoke), but the ADR asserts it as parity with ADR-0026 D3.3
device self-heal *without pinning it with a reset-path test*. TC-REVOKE-USER-2 tests
`iat > resetAt` (the clean case); it does **not** test the `iat == resetAt`-second boundary that is
the actual failure mode. Demand a boundary test, or the "no lockout on recovery" claim is asserted,
not proven.

**CH-U2 — the shared `DeviceRevocation:Enabled` switch couples two controls with DIFFERENT blast
radii (open point 1).** Verified: the helper reads one `IOptions<DeviceRevocationOptions>.Enabled`
(`DeviceRevocationTokenValidation.cs:23-31`), and D7 routes the user check through the same flag.
The author's "one flip in an incident" argument is sound *for the direction an operator usually
wants* — but the two facets are not symmetric in what disabling costs. Device revocation misbehaving
(e.g. a false-positive 401 storm) is a **UX/availability** incident; the natural relief is to disable
*it*. Reset-recovery cutoff is an **active-compromise** control; disabling it during the exact
incident where an operator is fire-fighting mobile auth means a just-reset victim's attacker keeps
access for ≤ 30 min. A single switch forces the operator to trade one against the other with no
knob. This is not a demand to split today — it is a demand that the ADR **name the coupling as a
deliberate accepted risk with its own residue line**, not sell it purely as ergonomics, and record
the split (`UserRevocation:Enabled`) as the pre-analyzed escalation with its trigger (any incident
that wants device revocation off but reset cutoff on).

**CH-U3 — the D3 change/reset asymmetry leaves a NARROW but real recovery residue (open point 2).**
Traced: `ChangeOwnPassword` is `[OWN-DATA]`, requires the current password *and* a live
authenticated session (`userSessionProvider.GetUserId()`, `ChangeOwnPassword.cs:46-47`), revokes
with `"password_changed"` keep-all-but-caller (`:83-84`). The takeover scenario the author dismisses
as "not on the recovery path": victim is *not* fully locked out (still holds a session), knows the
old password, and recovers via **in-app change** rather than the email RESET flow. Under D3 that
path does **not** feed the directory, so the attacker's access token rides ≤ 30 min. The author's D3
rebuttal (change self-inflicts a 401 on the caller's spared session; the spared session can't be
distinguished inside a `userId → instant` snapshot) is *correct as to why change can't naively feed
this directory* — but it does not make the residue disappear; it just explains why closing it needs
a different mechanism (spare-by-`iat`, or the device-directory path for the caller's own device).
D3 currently frames change-exclusion as purely "credential hygiene, not takeover recovery." That
framing is too clean: a user *can* use change to recover. Demand D3 name this as an explicit
residue (change-based recovery leaves ≤ 30-min attacker access) rather than defining it away.

**CH-U4 — `MAX(RevokedAt)` GroupBy vs option (c)'s indexed column (open point 3) — author is right,
but pin the EF translation.** Confirmed the trade: option (c) costs a migration + a mint-site claim
+ a claim-transition rollout window (the exact thing keying on `sub` buys us out of); the group-by
poll costs one indexed-range read over `password_reset` rows in a ~35-min horizon, and resets are
rare human actions behind the `auth` bucket. Total-change-cost and future-change-cost both favour
(a). **No blocking disagreement.** One implementation caveat the ADR must not gloss: the projection
`GroupBy(UserId).Select(g => new UserPasswordReset(g.Key, g.Max(t => t.RevokedAt!.Value)))` must
translate server-side; the `RevokedAt!.Value` on a `DateTimeOffset?` inside `Max` is the fragile
spot (null-forgiving on a nullable column). The predicate already filters `RevokedAt != null`, so
the values are non-null, but EF's group-by-aggregate translation over a nullable projected through
`.Value` is exactly the class of thing that has 500'd this codebase before (the memory index's
`FromSql`/`WithMany`/shadow-FK entries). Demand a Postgres-backed integration test on this repo
method (not just an in-memory fake), so the aggregate is proven to translate.

**CH-U5 — aging-out window (open point 6) — verified SAFE, recorded so the next panel doesn't
re-derive it.** Horizon = `AccessTokenExpMinutes` (30) + 5 = 35 min. A directory entry for a reset
at `resetAt` ages out when `resetAt < now − 35min`. Any access token that predates that reset has
`iat ≤ resetAt`, hence `exp = iat + 30min ≤ resetAt + 30min`. At the aging-out instant
`resetAt = now − 35min`, so `exp ≤ now − 5min < now` — the token is already expired by a full 5-min
margin before its directory entry disappears. **No window exists where a pre-reset token outlives
its entry.** The 5-min slack is doing exactly the job the device horizon's slack does; the author's
D9.7 claim holds. (The reverse hazard — `DeleteStaleAsync` pruning a `password_reset` row before the
poll sees it — is also safe: `DeleteStaleAsync` deletes `RevokedAt <= olderThan`
(`RefreshTokenRepository.cs:63-75`) on a far coarser cutoff than 35 min; D9.7 records it.)

**CH-U6 — the `sub` claim-source check (open point 5) — verified, no hole.** Confirmed above: every
token reaching a mobile host's `OnTokenValidated` was minted by one of the two `SetClaims` sites and
carries `sub`; there is no service/impersonation token that carries `sub` but should be exempt. Web
and admin tokens also carry `sub`, but `AddUserRevocationEnforcement` is installed on the two mobile
hosts **only** — a web/admin token is never presented to a mobile host (different audience,
`ValidateAudience = true` per host — `Web.Mobile.Customer/ServiceExtensions.cs:162-163`), so it can
never reach this check even in principle. The `sub`-keying is exact and the "no claim-transition
window" claim (D4) is confirmed: unlike `device_id`, `sub` predates this ADR on every token. Verified
and withdrawn; recorded so it is not re-litigated.

## Defense

*(The author instance is not live in this session; the P-1..P-8 pre-answers above are the standing
first-pass defense and are ruled on where they cover. For the challenger's own finds the lead
executes CONCEDE + REVISE on the author's behalf — each concession is folded into the artifact as a
marked amendment U1–U3, per the deliberation bar. Amendments are additive: no accepted-ADR text is
rewritten, because this ADR is still `proposed`.)*

- **CH-U1** — P-4 covers the "no claim-transition window" but no pre-answer covers the same-second
  false-FAIL *self-heal proof*. **CONCEDE + REVISE (U1):** D9.4 already names the direction; the gap
  is a *test*, not a design change. The self-heal is architecturally guaranteed (the post-reset
  re-login's refresh token postdates the reset and is therefore never in the keep-none revoke set —
  verified against `RevokeAllForUserAsync:152-161`, which only mutates tokens active at the reset
  instant). Fold a boundary case into TC-REVOKE-USER-2: a re-login whose `iat` truncates into the
  reset's same wall-clock second 401s once, then self-heals via its live refresh token in one round
  trip (200). This makes "no lockout on recovery" a pinned property, not an assertion.
- **CH-U2** — P-5 defends the shared switch as ergonomics; **CONCEDE the asymmetry (U2):** the two
  facets have different blast radii, and the ADR oversold the coupling as purely operator-friendly.
  Add a residue line (D9.8) naming the shared-switch coupling as a deliberate accepted risk, and
  record `UserRevocation:Enabled` as the pre-analyzed split with its trigger (an incident that wants
  device revocation off but reset cutoff on). The shared switch **stays the default** (D7 unchanged):
  a distinct key that ships un-exercised is its own silent-regression surface, and the dominant
  incident shape (disable a misbehaving *device* check) wants both off. The split is a mechanical
  follow-up, not a today cost.
- **CH-U3** — D3 covers change-exclusion but frames it too cleanly; **CONCEDE + REVISE (U3):** change
  *can* be a recovery path for a not-fully-locked-out victim, so change-exclusion carries a real
  residue (≤ 30-min attacker access on the change-based recovery path). Add it to D3 and to the
  residue list (D9.9). The **decision does not change** — feeding `password_changed` into this
  directory self-inflicts a 401 on the caller's spared session and can't distinguish that session
  inside a `userId → instant` snapshot (D3 rebuttal holds); closing it needs a different mechanism
  (spare-by-`iat`, or the caller's own device carried by the device directory), which is a separate
  decision. Name the residue; do not fold the fix.
- **CH-U4** — no pre-answer covers the EF-translation fragility; **CONCEDE the test, REBUT the
  design.** Option (a) stays chosen (CH-U4 concedes the trade favours it). Add to the reviewer-
  verification + test contract: a **Postgres-backed** integration test on
  `GetPasswordResetsSinceAsync` proving the `GroupBy` + `Max(RevokedAt!.Value)` aggregate translates
  server-side and returns `(UserId, MAX(RevokedAt))` — this codebase has a standing lesson that
  group-by/aggregate/null-projection idioms can pass an in-memory fake and 500 on real Postgres.
- **CH-U5** — D9.6/D9.7 cover cost + pruning; the aging-out safety proof is verified and folded into
  D9.7 as the explicit inequality so it is not re-derived. No change to the decision.
- **CH-U6** — D4/D6 cover the `sub`-keying; verified against the two mint sites + the mobile-only
  install + per-host audience validation, and **withdrawn**. Recorded so the next panel does not
  re-litigate it.

## Verdict

*(Architect panel lead, 2026-07-15 — different hat from the challenger's attack pass; every ruling
below re-checked against the cited code before adjudication.)*

| Challenge | Ruling | Disposition |
|---|---|---|
| CH-U1 same-second false-FAIL on the recovery path | **RESOLVED — amendment U1** | The direction is a false-FAIL, not a false-PASS, and the self-heal is *architecturally guaranteed*: the post-reset re-login's refresh token postdates the keep-none revoke (`RevokeAllForUserAsync:152-161` only mutates tokens active at the reset instant), so refresh succeeds and the retry passes. Not a lockout. But the property is now **pinned** — TC-REVOKE-USER-2 gains the same-second boundary case (401-once → self-heal → 200). Without the pin, "no lockout on recovery" was asserted; with it, proven. Not blocking with U1. |
| CH-U2 shared kill switch, asymmetric blast radius | **RESOLVED — amendment U2** | The shared `DeviceRevocation:Enabled` **stays the default** (a distinct key that ships un-exercised is its own silent-regression surface; the dominant incident wants both off). But the coupling is now an explicit accepted-risk residue (D9.8), and `UserRevocation:Enabled` is the pre-analyzed split with a named trigger. The ADR no longer oversells the coupling as pure ergonomics. |
| CH-U3 change-based recovery residue | **RESOLVED — amendment U3** | The decision is unchanged (D3's spared-session-401 rebuttal holds — `password_changed` cannot naively feed a `userId → instant` snapshot). But D3's "not a recovery path" framing was too clean: a not-fully-locked-out victim *can* recover via change, leaving ≤ 30-min attacker access. Now named as residue D9.9, not defined away. Closing it is a separate decision (spare-by-`iat`). |
| CH-U4 EF group-by/aggregate translation fragility | **RESOLVED — test added** | Option (a) stays chosen (the challenge concedes the trade favours it over (c)'s migration + claim-transition window). The reviewer-verification list and T-0418 gain a **Postgres-backed** integration test on `GetPasswordResetsSinceAsync` — this codebase's standing lesson is that group-by/aggregate/nullable-projection idioms pass an in-memory fake and 500 on real Postgres. |
| CH-U5 aging-out window | **RESOLVED — verified safe, proof folded into D9.7** | `exp = iat + 30min ≤ resetAt + 30min = (now − 35min) + 30min = now − 5min < now` at aging-out: no pre-reset token outlives its entry. The 5-min slack is load-bearing. |
| CH-U6 `sub` claim-source | **RESOLVED — verified, withdrawn** | Exactly two mint sites, both via `SetClaims` (always `sub`); no impersonation/service user-token path; mobile-only install + per-host audience validation means web/admin tokens never reach the check. No claim-transition window. |

**Consensus: zero blocking challenges remain.** ADR-0027 is **ACCEPTED with amendments U1–U3 folded
inline** (marked below as D9.8/D9.9 residues + the D3/TC-REVOKE-USER-2 pins + the Postgres test).
Acceptance is **not conditional on a new owner question** — the one owner question in this
neighbourhood ("is ≤ 30 s immediate enough?") is ADR-0026's, already in flight, and D9.1/Alt(e)
explicitly inherit it rather than re-ask it. The ≤ 30 s form is correct under either answer to that
question (the literal-zero read-through swaps in behind `IRevokedUserDirectory` untouched), so
T-0418 is **not gated** on it.

**Why this earns its place (the long-game test).** The decision preserves every seam it touches: it
clones the ADR-0026 directory rather than mutating it (device CRC "does NOT know Users" boundary
stays true), keys on `sub` so no mint-site or claim-transition surface is added, adds zero schema
(the `password_reset` rows are the stamp), and installs on mobile hosts only so the per-audience host
split is not coupled. The one new role (`RevokedUserDirectory`) has a clean CRC boundary and a single
collaborator on the request path. A future user-disable cutoff (D9 / auth-sessions ladder) gets a
second feed into this same directory under its own ADR — the extension point is already shaped. This
makes the *next* two changes (literal-zero escalation, user-disable cutoff) one-file/one-feed changes
rather than rewrites. It earns its place.

**No new owner escalation.** (The standing ADR-0026 owner question — ≤ 30 s vs literal-zero — governs
both directories and is already surfaced in `questions/open.md`; this ADR does not add a second.)

**Acceptance follow-ups handed to the architect lane / PM (outside this panel's writable surface):**
- **Catalog edit** — `agents/knowledge/security-rules.md` (S2 neighbourhood): the ADR-0024/0026
  token-lifetime paragraph gains the ADR-0027 sentence (reset ends the reset user's mobile sessions
  within the same ≤ 30 s bound via the `sub`-keyed `RevokedUserDirectory`; change is deliberately not
  accelerated; the shared `DeviceRevocation:Enabled`/`RefreshSeconds` bounds govern both mobile
  checks). Ships as the acceptance follow-up, ADR-0024/0026 precedent.
- **Living doc** — `agents/architecture/decisions/auth-sessions.md`: flip the ADR-0027 markers from
  proposed to accepted, add the D9.8 shared-switch residue + the `UserRevocation:Enabled` split rung
  to the trade-off ladder, and the D9.9 change-based-recovery residue to the watch items. (Done in
  this change.)
- **CRC card** — `agents/knowledge/roles/revoked-user-directory.md`: flip proposed → accepted, add
  the shared-switch-coupling note to "Does NOT know / smell guard" neighbourhood. (Done in this
  change.)
- **T-0418** — unblock AC1 (ADR accepted); add the TC-REVOKE-USER-2 same-second boundary pin and the
  Postgres-backed `GetPasswordResetsSinceAsync` integration test to the test contract. (Done in this
  change.)

This ADR is now **immutable** — supersede, never edit. (The one sanctioned future touch: the
`Superseded by:` pointer line.)

---

## Amendments folded at acceptance (U1–U3 — the panel's marked additions)

*These are the additive residue/pin changes the Verdict ratified. They extend the Decision/residue
set; no prior clause is rewritten (this ADR was `proposed` when they were folded).*

### D3 addendum (U3) — change-based recovery is a named residue, not a defined-away non-case

D3's threat-model framing ("change is credential hygiene, not takeover recovery") is correct for the
*common* case but is **not exhaustive**: a victim who still holds a live session and knows the old
password *can* recover via in-app `ChangeOwnPassword` instead of the email RESET flow. On that path
the directory is not fed (the feed predicate is `password_reset` alone), so the attacker's outstanding
access token rides ≤ 30 min. This residue (**D9.9**) is accepted, not closed: feeding `password_changed`
into this directory self-inflicts a 401 on the change caller's own spared session and cannot
distinguish that session inside a `userId → instant` snapshot (the original D3 rebuttal). Closing it
requires a *different* mechanism (spare-by-`iat`, or cutting the caller's own device via the device
directory) and is a separate decision — named here, deliberately not folded.

### D9.8 (U2) — shared kill-switch coupling is a deliberate accepted risk

The user check honours the same `DeviceRevocation:Enabled` flag as the device check (D7). The two
facets have **different blast radii**: disabling device revocation is a UX/availability relief;
disabling reset-recovery cutoff removes an active-compromise control during the same incident. A
single switch cannot express "device off, reset cutoff on." This is accepted for the default because
(a) the dominant incident shape is a misbehaving device check that an operator wants off, and (b) a
distinct un-exercised config key is its own silent-regression surface. The **pre-analyzed split** is
`UserRevocation:Enabled` (mirrors `DeviceRevocationOptions`), a mechanical follow-up; its **trigger**
is any incident that requires device revocation off while reset cutoff stays on. Recorded so the
split is a decision waiting to be made, not folklore.

### D9.9 (U3) — change-based recovery leaves ≤ 30-min attacker access (see D3 addendum)

Bounded by the TTL + the already-revoked sibling refresh chain (the attacker cannot renew). A
deliberate non-goal; closing it is the separate decision named in the D3 addendum.

### D9.7 clarification (U1/U5) — the aging-out and same-second bounds are proven, not asserted

- **Aging-out safety (algebraic proof).** A pre-reset access token has `iat ≤ resetAt`, hence
  `exp = iat + AccessTokenExpMinutes ≤ resetAt + 30min`. Its directory entry ages out at
  `resetAt = now − (30 + 5)min`, at which instant `exp ≤ now − 5min < now` — the token is already
  expired with 5 min of slack. **No window exists where a pre-reset token outlives its entry.** The
  5-min horizon slack is load-bearing, not cosmetic.
- **Same-second false-FAIL self-heal (U1).** Because `iat` is whole-second (`FromUnixTimeSeconds`)
  and `resetAt` is sub-second, and the guard is strict `<`, a post-reset re-login within the reset's
  wall-clock second can 401 once. It self-heals: the re-login's refresh token was minted *after* the
  reset and is therefore not in the keep-none revoke set (`RevokeAllForUserAsync` mutates only tokens
  active at the reset instant), so refresh succeeds and the retry passes. Pinned by the
  TC-REVOKE-USER-2 boundary case. This is a false-FAIL that recovers, never a false-PASS.
