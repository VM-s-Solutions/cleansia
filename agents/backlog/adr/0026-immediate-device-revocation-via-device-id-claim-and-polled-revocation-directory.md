# ADR-0026 — Device revocation becomes ~immediate on mobile: access tokens gain a `device_id` claim and the mobile hosts reject revoked devices at token validation via an in-memory revocation directory polled from Postgres (bound ≤ 30 s); ADR-0024's 30-min TTL stays as the backstop

- **Status:** accepted (panel verdict 2026-07-15, amendments A1–A6 folded inline — see Verdict;
  acceptance is **conditional on one owner ratification**: that "immediately" tolerates the ≤ 30 s
  default bound. An owner ruling of literal-zero exercises the named B-literal swap via a short
  superseding ADR — the design carries both answers, nothing is thrown away)
- **Date:** 2026-07-15
- **Supersedes:** ADR-0024 **partially** — only **D2** (the "the TTL *is* the revocation bound; no
  per-request read path — by decision" contract statement) and **D3-B** (option B's *deferral*: its
  named revisit trigger — "a product demand for instant revocation" — was exercised by the owner's
  verbatim directive below). **ADR-0024 D1 (TTL = 30 min on the two mobile hosts) stands unchanged
  as the backstop bound**, and its D3-C (push-driven logout = UX only, never a security control)
  is reaffirmed here, not weakened.
- **Superseded by:** —
- **Applies to:** backend shared Core (`AuthExtensions`/`TokenService`/`RefreshToken` handler,
  `IDeviceRepository`, new directory service) + the two mobile hosts' JwtBearer wiring | no client
  code change | no schema migration (audit columns already exist and are mapped)
- **Ticket:** T-0414 (`security_touching: true`, priority high, owner-directed) · related: T-0405
  (landed — the 30-min TTL), T-0406 (Android partner forced-signout collector), ADR-0024 D4.5
  (mobile refresh retryable-vs-terminal classification)

> **One decision:** *how a revoked mobile device loses access to the API **now** instead of at the
> next token expiry.* Mobile-minted access tokens gain a **`device_id` claim** (stamped from the
> same server-side source the refresh-token machinery already trusts), and the **two mobile hosts**
> check every validated token against an **in-memory revoked-device directory** — a singleton
> snapshot of `(userId, deviceId, revokedAt)` for device deactivations younger than the access-token
> TTL, refreshed from Postgres every **≤ 30 s** per instance. A token whose `(sub, device_id)` was
> revoked **after** the token's `iat` fails authentication → **401** → the client's existing
> 401→refresh machinery hits the already-revoked refresh token (`device_revoked`) → token wipe +
> forced sign-out. Net product bound: **a device deleted from the Devices list loses API access
> within ~30 seconds and signs itself out at its next interaction.** The request path adds **zero**
> DB reads (one dictionary lookup); steady-state DB cost is one tiny indexed poll per instance per
> interval. Fail posture on poll failure is **fail-open on the last snapshot** — ADR-0024's 30-min
> TTL remains the hard ceiling that makes that safe. Once `accepted` this is immutable — supersede,
> never edit.

---

## Context

### The owner directive (the trigger, verbatim, 2026-07-15)

> "I want the device once it's deleted from the list of devices to be revoked IMMEDIATELY. Not wait
> until the token is revoked."

This is, word for word, the revisit trigger ADR-0024 D3 recorded for its deferred option B: *"a
compliance/product requirement for instant kill."* The trigger fired; this ADR is the recorded
escalation — a lookup, not a re-derivation, exactly as ADR-0024's Consequences promised.

### What exists today (all verified in code, 2026-07-15)

**The write path is correct and stays untouched.** Deleting a device from the Devices list
(`DELETE api/Device/{deviceRowId}` on both mobile hosts and `Web.Customer` —
`DeviceController.cs:51-61`) runs `RevokeDevice.Handler`
(`src/Cleansia.Core.AppServices/Features/Devices/RevokeDevice.cs:43-44`): deactivates the device
row (`deviceRepository.Deactivate` → `IsActive = false`) and revokes exactly that device's refresh
tokens (`RevokeByDeviceAsync(userId, deviceId, "device_revoked")`, `RefreshTokenService.cs:120-133`,
device id carried across rotation at `RefreshTokenService.cs:98`). Post-T-0405, the outstanding
access JWT dies ≤ 30 min later. That ≤ 30 min is what the owner is now rejecting.

**The access token carries no device identity.** `AuthExtensions.SetClaims`
(`src/Cleansia.Core.AppServices/Extensions/AuthExtensions.cs:16-32`) yields exactly: `sub`
(NameIdentifier), name, email, role, and conditionally `tenant_id` and `employee_id`. **No
`device_id`, no `jti`.** Both mint sites use it: `TokenService.GenerateAccessToken`
(`TokenService.cs:64-79`, login — where `requestMetadata.DeviceId` from the `X-Device-Id` header is
in scope at `:42` but never minted into the token) and the refresh handler's private
`GenerateAccessToken` (`Features/Auth/RefreshToken.cs:116-130` — where the rotated record's
persisted `issued.Record.DeviceId` is in scope but never minted). So today, given only a bearer
token, the server *cannot know which device it is talking to* — the single fact enforcement needs.

**The mobile hosts already have the plug-in point.** Both mobile hosts configure JwtBearer with an
`OnTokenValidated` event (Partner `Extensions/ServiceExtensions.cs:165-187`; Customer
`Extensions/ServiceExtensions.cs:171` — same shape) that currently only maps role claims. A
`context.Fail(...)` inside `OnTokenValidated` converts to a standard **401 challenge** — which is
load-bearing (see D3): 401 is the one status both mobile clients already convert into
refresh-then-forced-sign-out (`AuthAuthenticator.kt`, `GeneratedClientAuthBridge.swift`).

**There is no caching infrastructure to lean on.** Zero `IMemoryCache`/`IDistributedCache`/
`HybridCache` registrations in `src/` (verified); no Redis anywhere in the stack; deployment is
Azure App Service (Bicep `appServicePlan.bicep`, default B2) with Postgres Flexible Server as the
only shared backplane. Any cross-instance signal must therefore ride Postgres.

**The audit columns for "when was it revoked" already exist and are mapped.** `Auditable` carries
`DeactivatedBy`/`DeactivatedOn` (`Common/Auditable.cs:15-17`), mapped by `EntityConfiguration.cs:49`
— but `BaseRepository.Deactivate` only flips `IsActive` (`BaseRepository.cs:122-125`); the generic
commit path stamps `UpdatedOn` on any Modified entity (`CleansiaDbContext.cs:93-96`).
`SavedAddressRepository.Deactivate` (`:44-49`) is the house precedent for a repository override that
stamps `entity.Deactivated(actor, now)` properly. **No migration is needed** — this is a write-path
stamp into existing columns.

### Why the enforcement key must be a signed claim, not a header

Mobile clients send `X-Device-Id` on requests, but a header is **client-asserted**: the threat
model here is a hostile holder of a stolen/revoked device (or an extracted bearer token) — they
simply drop or change the header. `RequestMetadataProvider.cs:34-48` reads the header for
*bookkeeping* at login/refresh, where lying only hurts the liar (a token stamped with a fake device
id is revocable under that fake id, and an *absent* id makes the session non-matchable — the
deliberate ADR-0024 D4.2 residue). For *enforcement*, the device identity must be inside the
signature envelope: a **`device_id` JWT claim**. There is no sound userId-keyed fallback either:
keying the deny decision on userId alone would 401 **all** the user's devices — including the very
device they are holding while performing the revoke (the Devices list UX marks a "current device";
killing it on revoke of a *different* row is an unacceptable false positive).

### Scope: the two mobile hosts only

Unchanged from ADR-0024 and structural, not conventional: web sessions carry `DeviceId = null`
(browsers never send `X-Device-Id`; the null-guard at `RefreshTokenService.cs:129` is deliberate and
test-pinned), so web tokens will simply never carry the claim and never match the directory. The
enforcement wiring is installed on `Cleansia.Web.Mobile.Partner` and `Cleansia.Web.Mobile.Customer`
only; the three web hosts are byte-untouched. (Revoking *from* the web customer app still works —
the write path is host-agnostic; only the *enforcement read* is mobile.)

---

## Decision

### D1 — Mobile-minted access tokens carry `device_id`

`AuthExtensions.SetClaims` gains an optional `deviceId` parameter (mirroring `employee_id`'s
conditional-claim shape, snake_case per the existing `tenant_id`/`employee_id` convention) and both
mint sites pass what they already know:

- **Login** (`TokenService.GenerateAccessToken`): `requestMetadata.DeviceId` — the same value the
  refresh token is stamped with in the very same method (`TokenService.cs:42`).
- **Refresh** (`RefreshToken.Handler.GenerateAccessToken`): **`issued.Record.DeviceId`** — the
  *persisted, rotation-carried* value, **not** the request header. Server-authoritative: a rotated
  session's claim can never drift from the device id that revocation matches on.

No `deviceId` (web logins, legacy clients) → no claim → the directory can never match → zero
behavior change outside mobile. `iat` is already minted on every token
(`JwtSecurityTokenHandler.CreateToken` default times; both mint sites use it) and becomes
load-bearing in D2's guard.

### D2 — The `RevokedDeviceDirectory`: an in-memory snapshot with a defined staleness bound

A new singleton, `IRevokedDeviceDirectory` (implementation `RevokedDeviceDirectory` +
`RevokedDeviceDirectoryRefresher : BackgroundService`), registered by a single shared extension
`AddDeviceRevocationEnforcement()` in `Cleansia.Config` (the ADR-0001 §D4 precedent: shared
registration, not five hand-copied bodies) and **called only by the two mobile hosts**:

- **Snapshot contents:** `(UserId, DeviceId) → RevokedAt` for every device row with
  `DeactivatedOn >= now − horizon` — **regardless of the row's current `IsActive`** *(panel
  amendment A1 — the challenger's blocking find: `Device.MarkRegistered` reactivates a tombstone
  for any authenticated caller (`Device.cs:48-62`; `POST api/Device/Register` requires only
  `Policy.Authenticated`, `DeviceController.cs:16-26`) and never clears `DeactivatedOn`. A
  predicate that also required `IsActive == false` would let a revoked device holding its
  still-valid access token re-register in a loop and expunge its own directory entry at the next
  poll — riding out the full 30-min TTL, precisely the bound the owner rejected. Keying on
  `DeactivatedOn` alone makes the snapshot insensitive to reactivation: the `iat` guard alone
  decides survival, and it already passes every legitimate re-login. Pinned by TC-REVOKE-NOW-9)*
  — where
  **horizon = `AccessTokenExpMinutes` + 5 min slack**. This is the elegant composition with
  ADR-0024: a revocation older than the TTL cannot have a live token predating it, so the directory
  never needs to remember more than one TTL of history — the structure stays at platform-wide
  revocations-per-half-hour cardinality (trivially small) forever.
- **Refresh:** every **`DeviceRevocation:RefreshSeconds` (default 30)** per instance, via a scoped
  read on a new `IDeviceRepository.GetDeactivatedSinceAsync(cutoff)` that **must**
  `IgnoreQueryFilters()` — the poll is a background, tenant-less, cross-tenant-by-design read (the
  sanctioned pattern from the T-0245 lineage); keys are globally unique ids, so no tenant ambiguity
  exists. Snapshot swap is an atomic reference replacement (immutable dictionary); the request path
  never takes a lock.
- **The check (request path):** O(1) dictionary lookup + one comparison — **reject iff an entry
  exists for `(sub, device_id)` and `token.iat < entry.RevokedAt`**. The `iat` guard is what makes
  re-login-after-revoke safe: a session minted *after* the revocation passes even while the entry
  is still in the snapshot. No claim → pass (see D6 transition). A device-claimed token that
  matches an entry but carries a **missing/unreadable `iat`** is rejected *(panel amendment A2 —
  it cannot prove it postdates the revocation; both mint sites always stamp `iat`, so a legitimate
  anomaly is near-impossible and self-heals via refresh in one round trip anyway)*. No DB, no
  allocation, no clock read on the request path.
- **Multi-instance bound:** each instance polls Postgres independently — no backplane, no new
  infrastructure. Worst-case enforcement latency = `RefreshSeconds` + one poll-query time on the
  slowest instance, **independent of instance count**. N instances cost N tiny queries per
  interval — noise.
- **Time:** the refresher and horizon math use the injected `TimeProvider` (the ADR-0024 hygiene
  lesson — `TokenService.cs:74` ignoring its injected `TimeProvider` is exactly what made
  TC-REVOKE-TTL-2 unimplementable as first specified; this component does not repeat that).

**The product bound this buys (D2 contract, replacing ADR-0024 D2's statement for mobile):**
*deleting a device from the Devices list ends that device's API access within
`DeviceRevocation:RefreshSeconds` (default 30 s) plus in-flight request completion, and the device
signs itself out at its next interaction.* "Immediately" in this ADR means **≤ 30 s** — faster than
any human re-opens the app they just revoked from another phone. If the owner requires
literal-zero, the escalation is a one-implementation swap behind `IRevokedDeviceDirectory`: a
read-through per-request check (+1 indexed query on every authenticated mobile request) — the seam
is designed for it (see Alternatives (a)).

### D3 — Enforcement runs at token *validation* and fails as a 401, not a 403

The check plugs into the two mobile hosts' existing `JwtBearerEvents.OnTokenValidated` (after their
role-claim mapping), via one shared helper in `Cleansia.Config` so the logic exists once. On a
match it calls `context.Fail("device_revoked")` → standard 401 challenge. Why this point and not an
authorization requirement or middleware:

1. **Semantics:** "this token no longer represents a live session" is an *authentication* failure.
2. **The 401 is load-bearing:** both mobile clients convert 401 → single-flight refresh → and the
   refresh for a revoked device is **already dead** (`device_revoked` → `InvalidRefreshToken`) →
   token wipe + forced sign-out (`AuthAuthenticator.kt:49-94`; `SessionRefresher.swift` +
   `GeneratedClientAuthBridge.swift:39-54`). The entire client-side story rides tested machinery —
   **zero client code change**. An authorization-handler 403 would trigger none of it (OkHttp's
   authenticator and the iOS bridge react to 401 only).
3. **Self-healing false positives:** any stale-snapshot 401 against a *legitimate* session (e.g.
   the same-second re-login race) triggers refresh → refresh **succeeds** (that session's refresh
   token is alive) → retry with a fresh token whose `iat` now postdates the revocation → passes the
   `iat` guard. One silent round trip; no forced sign-out (sign-out only happens on refresh
   *rejection*).

### D4 — Failure posture: fail-open on the last snapshot, with staleness telemetry

If a poll fails (DB unreachable, timeout), the directory **keeps serving the last snapshot** and
logs; when snapshot age exceeds `3 × RefreshSeconds` it escalates to a warning with the age (the
ops signal). At startup the refresher attempts one synchronous initial fill; on failure it starts
empty (fail-open) rather than crashing the host or blocking serving.

**The refresher loop itself must be un-killable** *(panel amendment A3)*: the entire tick body
lives inside the `while (!stoppingToken.IsCancellationRequested)` loop's try/catch so **no
exception can escape `ExecuteAsync`** — .NET's default `BackgroundServiceExceptionBehavior.StopHost`
would otherwise convert a poll bug into a host crash (a catastrophic fail-closed), and a *dead*
loop can never emit the staleness warning that is the only ops signal: the warner must be the
survivor. Pinned by the extended TC-REVOKE-NOW-6 (the loop keeps attempting ticks across
consecutive failures).

Fail-closed (401 everything when stale) is **rejected**: a Postgres blip would 401 the whole mobile
fleet → every client attempts refresh → refresh also fails (same outage) → forced sign-outs at
fleet scale. *(Panel amendment A6 — evidence refreshed: the ADR-0024 D4.5 classification has
partially landed in the concurrent client lanes — Android's `AuthAuthenticator` now keeps the
session on `RefreshResult.Unavailable` and only wipes on `Rejected` (`AuthAuthenticator.kt:94-105`
in the current tree); iOS is still pending. The conflation argument is therefore time-eroding and
is no longer what carries this decision.)* What carries it permanently: fail-closed is failure
amplification for **zero security gain**, because the **worst case of fail-open is precisely
ADR-0024's accepted posture** — revocation latency degrades toward ≤ 30 min (the TTL), never worse
than the world before this ADR. That bounded-degradation property is exactly why D5 keeps the TTL.

### D5 — ADR-0024's 30-min TTL stays, unchanged, as defense-in-depth

Not weakened, not extended. It is (1) the hard ceiling under any directory failure/regression
(fail-open is safe *because* of it), (2) the horizon that keeps the directory tiny, (3) the bound
for claim-less tokens (D6), and (4) the cadence at which refresh re-checks `user.IsActive` and
re-mints roles (`RefreshToken.cs:83-88`). All five TC-REVOKE-TTL tests stay green and unedited.
ADR-0024's rule that `AccessTokenExpMinutes` on a mobile host is ADR-governed survives verbatim.

### D6 — Rollout/transition: claim-less tokens pass, bounded by the TTL

Tokens minted before this deploys carry no `device_id` claim and pass the directory check. That
transition window is **≤ 30 min** (the TTL — after that every live mobile token was minted by the
new code) and its exposure equals the pre-ADR-0026 status quo, so the rollout is monotonic: at no
instant is any session *less* enforceable than yesterday. No client release, no NSwag regeneration
(no DTO/endpoint change), no coordination — one backend deploy.

### D7 — `DeviceRepository.Deactivate` stamps `DeactivatedOn` properly

`DeviceRepository` overrides `Deactivate` to call `entity.Deactivated(actor, now)` (the
`SavedAddressRepository.cs:44-49` precedent; actor from `IUserSessionProvider`, `"System"`
fallback), so the directory's `RevokedAt` is a first-class audit timestamp rather than the
incidental `UpdatedOn`. Both device-deactivation paths flow through it: `RevokeDevice` **and**
`UnregisterDevice` (logout). Including logout entries is deliberate — it *closes a bonus gap*: a
stolen still-valid access token from a logged-out session currently rides out the TTL; now it dies
at the next poll. Rows deactivated before this deploys have `DeactivatedOn = null` and never enter
the directory — irrelevant, since any token predating the deploy dies by TTL anyway. No schema
change; the columns exist and are mapped.

### D8 — Config kill switch, pinned

`DeviceRevocation:Enabled` (default `true` in code, explicit `true` in all four mobile appsettings
files) exists as the same-day ops relief valve if the enforcement misbehaves in production (the
`RateLimiting:Auth:AnonPermitLimit` precedent from ADR-0024). Because a silent `false` would be a
silent security regression, the value is **test-pinned raw-file** (the TC-REVOKE-TTL-4 mechanism —
the HostTests overlay cannot mask it) together with `RefreshSeconds ≤ 30`. *(Panel amendment A5 —
semantics pinned: `Enabled = false` no-ops the **enforcement helper only**; the refresher keeps
polling, so the snapshot stays warm — re-enabling is instant — and the staleness telemetry never
goes dark while the switch is off.)*

### D9 — Accepted residues (explicit)

1. **≤ 30 s residual access after revoke** (+ in-flight requests). The escalation to literal-zero
   is a named implementation swap (D2), not a redesign.
2. **≤ 30 min for claim-less tokens** during the one-time rollout window (D6) and forever for
   sessions whose login never sent `X-Device-Id` (web-shaped logins against mobile hosts, legacy) —
   the same ADR-0024 D4.2 residue, unchanged: no device identity, nothing to key on.
3. **Same-second revoke↔re-login clock ambiguity across instances:** `iat` has one-second
   resolution and mint/revoke clocks may skew; a token minted within ~1 s of the revocation may
   401 once — and self-heal via refresh in one round trip (D3.3). Not worth a skew allowance that
   would blunt real enforcement. *(Panel amendment A4 — the skew also runs the other way: a token
   minted just before the revoke on a fast-clock instance can carry `iat ≥ revokedAt` and pass the
   directory permanently — a false PASS, not just a false 401. Bounded and accepted: both stamps
   are app-server clocks under NTP (skew ≪ 1 s in practice), the escapee's refresh chain was
   already revoked at revoke time so the session cannot renew, and the TTL backstop kills it
   ≤ 30 min later.)*
4. **User-disable (`IsActive = false`) and password change are NOT accelerated** — this directory
   keys on *device revocation* only. Admin user-disable still bites at ≤ 30 min (refresh re-check);
   password-change session revocation remains the open ADR-0024 D4.6 follow-up. Extending the
   directory to user-level kills is a natural later extension, deliberately not folded in (one
   decision per ADR).
5. **Fail-open staleness under DB outage**, ceiling = the TTL (D4 — argued, not accidental).
6. **Directory memory/poll cost:** platform-wide device deactivations (revokes + logouts) in the
   last ~35 min — hundreds of entries at fleet scale, one small indexed-scan query per instance
   per 30 s against a table with a few rows per user. Verified: exactly **two** deactivation call
   sites exist (`RevokeDevice.cs:43`, `UnregisterDevice.cs:39`), both per-user UI actions behind
   the `auth` rate bucket — no batch job deactivates devices, so the snapshot cannot spike beyond
   human-action rates. **Standing rule:** any *future* bulk device-deactivation job (e.g. stale-
   push-token cleanup) inflates this snapshot and triggers a fleet-wide silent-refresh ripple —
   it must be checked against this ADR before shipping. No new index shipped with this ADR; if
   the poll predicate ever shows in pg_stat, a partial index on `DeactivatedOn` (A1 predicate) is
   a one-line follow-up **owner migration**.
7. **Revoke↔rotation TOCTOU (pre-existing, now named — panel find):** `RevokeByDeviceAsync` loads
   the active tokens and revokes them in memory (`RefreshTokenService.cs:120-133`); a rotation
   committing inside that read→commit window can insert a fresh refresh token the revoke never
   saw, and last-writer-wins on the old head (`rotated` overwriting `device_revoked`) lets the
   chain escape — after which its access tokens carry `iat > revokedAt` and pass the directory,
   while the deactivated row vanishes from the Devices list (no re-revoke handle). The window is
   milliseconds, refresh is capped at 10/min/IP by the anonymous `auth` bucket, and the identical
   race defeats revocation equally *today* — this ADR strictly improves the status quo. NOT fixed
   here (one decision per ADR); the named hardening is Verdict instruction **X2** (concurrency-
   guard the revocation: `xmin` concurrency token on `RefreshToken` so a racing rotation faults,
   or a set-based conditional UPDATE at commit).
8. **Cosmetic row resurrection:** during the pre-poll window a revoked device's still-valid token
   can `Register` and flip its row back to `IsActive = true` (it reappears in the Devices list) —
   but per A1 its directory entry survives and its sessions still die at the next poll; what
   remains is a push-registered row with no live session. Cosmetic, accepted.

---

## Alternatives considered

- **(a) Per-request DB check of `Device.IsActive`** (option B in its literal form). Correct and
  zero-staleness, rejected as the *default*: it puts one Postgres read on **every authenticated
  mobile request** on hot paths (orders list, dashboard polling) forever, to buy ≤ 30 s over the
  directory — a bound difference no human perceives in this product (a revoked cleaning-app device
  is not a payment authorization). It also concentrates a new hot dependency on the shared B1ms
  Postgres. **Kept as the named escalation:** the `IRevokedDeviceDirectory` interface is the seam;
  a read-through implementation swaps in without touching hosts, claims, or clients if the owner
  rules that "immediately" means literal-zero.
- **(b) Header-based enforcement (`X-Device-Id` on each request).** Rejected outright: the header
  is client-asserted and the adversary *is* the client (Context). Enforcement keys must live inside
  the JWT signature.
- **(c) `jti` denylist.** Rejected: no `jti` is minted today, and it is the wrong key — revocation
  is *device*-scoped, not token-scoped; a jti list must be fed by every mint and grows with token
  volume (~2/device-hour), whereas the device key is fed only by revocations and stays near-zero.
  The `device_id` claim is both smaller and semantically exact.
- **(d) Push-driven client sign-out as the mechanism** (ADR-0024 option C, now feasible on
  Android). **Rejected as the security bound, permanently and by principle: a hostile client
  ignores a push** (drops network, disables notifications, uninstalls the push handler — the token
  keeps working). C remains what ADR-0024 said it is: a UX *complement* that makes the revoked
  device visibly sign out in seconds instead of at its next API call. Its trigger (iOS receive-side
  landing) is unchanged; if built, it layers over this ADR — it never substitutes for it.
- **(e) Redis / distributed cache / LISTEN-NOTIFY backplane.** Rejected: no Redis exists in this
  stack and a 30 s poll against the existing Postgres meets the bound with zero new infrastructure.
  Postgres `LISTEN/NOTIFY` could shave the 30 s to ~0 later *without changing the design* (the
  directory just gains a second refresh trigger) — noted as the cheap upgrade path, not adopted.
- **(f) Shorten the TTL further (5 min / 1 min).** Rejected: ADR-0024's herd analysis (the
  anonymous per-IP `auth` bucket, 10/min fixed window, synchronized wakes) gets strictly worse with
  refresh frequency, and even a 1-min TTL does not deliver "immediately" — it delivers ≤ 1 min at
  ~60× the refresh traffic. Wrong curve entirely; the directory decouples the bound from the TTL.
- **(g) Do nothing beyond ADR-0024.** Rejected: the owner's directive is explicit and is the
  precise revisit trigger ADR-0024 named. Ignoring a fired trigger would make the escalation
  ladder folklore.

---

## Consequences

**Cheaper / safer:**
- Revoked device: residual API access drops **≤ 30 min → ≤ ~30 s**, with the sign-out UX riding
  the existing 401→refresh→forced-sign-out machinery — no client release.
- Logout gains the same property for free (D7): a post-logout stolen access token dies at the next
  poll instead of riding out the TTL.
- The enforcement seam (`IRevokedDeviceDirectory`) makes both future escalations one-file changes:
  literal-zero (read-through impl) and user-level kill (feed user disables into the same
  directory under a superseding ADR).
- Web hosts, web clients, Functions: byte-untouched. Per-audience host coupling: none.

**More expensive (accepted):**
- One `BackgroundService` + singleton per mobile host instance; one small poll query per instance
  per 30 s (measured horizon keeps it tiny — D9.6).
- Two more conditional claims' worth of JWT size (~30 bytes) on mobile tokens.
- A new standing rule to maintain: device-deactivation write paths must stamp `DeactivatedOn`
  (D7) — enforced by test, recorded in the catalog at acceptance.
- The `OnTokenValidated` wiring is touched in both mobile hosts (two-line change each, shared
  helper holds the logic). The pre-existing duplication of `AddJwt` across hosts is *not* fixed
  here (out of scope; consolidation is a separate consistency ticket if the reviewer trips on it a
  third time).

**No migration** (existing mapped columns), **no NSwag** (no DTO/endpoint change), **no client
change**, **no Bicep change** (config ships in appsettings, per the ADR-0024 deployment-reality
finding).

---

## How a reviewer verifies compliance

**Mechanical:**
1. `SetClaims` gains the optional `deviceId` parameter; grep confirms exactly **two** callers pass
   it: `TokenService.GenerateAccessToken` (from `requestMetadata.DeviceId`) and
   `RefreshToken.Handler.GenerateAccessToken` (from `issued.Record.DeviceId` — **must not** read
   the header).
2. `AddDeviceRevocationEnforcement()` lives in `Cleansia.Config`, is called by exactly the two
   mobile hosts' `ServiceExtensions`, and appears nowhere in `Web.Partner`/`Web.Admin`/
   `Web.Customer` (their `ServiceExtensions.cs` byte-identical).
3. The new `IDeviceRepository.GetDeactivatedSinceAsync` uses `IgnoreQueryFilters()` (cross-tenant
   background read — cite the comment in code), projects only `(UserId, DeviceId, DeactivatedOn)`,
   and its predicate is `DeactivatedOn >= cutoff` **alone — no `IsActive` conjunct** (A1: a
   reactivated row must not expunge a live revocation).
4. The request-path check performs no I/O: `RevokedDeviceDirectory.IsRevoked(...)` is a pure
   snapshot lookup; the only DB access lives in the refresher.
5. `DeviceRepository.Deactivate` override stamps `Deactivated(actor, now)`; `TokenService.cs`
   expiry minting and all TC-REVOKE-TTL-* pinned files remain untouched.
6. TTL configs unchanged: the four mobile appsettings still carry `AccessTokenExpMinutes: 30`
   (TC-REVOKE-TTL-4 stays green, unedited).

**Test contract (T-0414 — names for the backend ticket):**
- **TC-REVOKE-NOW-1 — the headline property.** HostTests, mobile host, short `RefreshSeconds` (or
  a test hook forcing a directory refresh): login with `X-Device-Id: A` → authed call 200 →
  `RevokeDevice(A)` → refresh directory → **the same, unexpired access token → 401** (not 403).
  A different user's device token → still 200.
- **TC-REVOKE-NOW-2 — sibling-session precision.** Same user, devices A and B (two logins, two
  device ids). Revoke A → A's token 401; **B's token stays 200**. Pins that enforcement is
  device-keyed, never user-keyed.
- **TC-REVOKE-NOW-3 — the `iat` guard / re-login self-heal.** Revoke A → re-login on A (new token,
  `iat` > `RevokedAt`) **while the directory entry is still present** → new token 200. Pins that a
  revoke is a session kill, not a device ban.
- **TC-REVOKE-NOW-4 — claim minting, both sites, server-authoritative.** (i) Mobile login with
  `X-Device-Id` → JWT contains `device_id` = header value; (ii) login without the header → **no**
  `device_id` claim; (iii) refresh presenting a device-stamped rotated token but a *different/absent*
  `X-Device-Id` header → the new JWT's claim equals the **persisted record's** DeviceId, not the
  header.
- **TC-REVOKE-NOW-5 — transition fail-open.** A token *without* the claim, for a user who has a
  directory entry → 200 (bounded by the TTL backstop; pins D6 so the transition posture is a
  decision, not an accident).
- **TC-REVOKE-NOW-6 — failure posture + the perf pin.** Unit: refresher's repository fake throws →
  directory keeps answering from the last snapshot; staleness warning fires past
  `3 × RefreshSeconds`; and a counting fake proves **zero repository calls from the request-path
  check** across N lookups (the perf pin: request cost is memory-only). **Extended (A3):** the
  fake throws on *consecutive* ticks and the test asserts the refresher still attempts the next
  tick — the loop survives repeated failure; no exception escapes `ExecuteAsync`.
- **TC-REVOKE-NOW-7 — config pin (raw-file).** Plain xUnit, TC-REVOKE-TTL-4 mechanism: the four
  mobile appsettings files carry `DeviceRevocation:Enabled = true` and `RefreshSeconds ≤ 30`;
  changing either fails a test until a superseding ADR.
- **TC-REVOKE-NOW-8 — logout entries (D7 bonus).** `UnregisterDevice` → that device's outstanding
  access token 401s after the next directory refresh.
- **TC-REVOKE-NOW-9 — re-registration cannot expunge enforcement (A1).** Login with
  `X-Device-Id: A` → revoke A → *before any directory refresh*, call `Device/Register` for A with
  the still-valid token (succeeds; the row reactivates via `MarkRegistered`) → force a directory
  refresh → **the old access token still 401s** (the snapshot keys on `DeactivatedOn`, not row
  state); a *fresh login* on A afterwards → 200 (the `iat` guard, unchanged).

---

## Living docs updated with this ADR

- `agents/architecture/decisions/auth-sessions.md` — updated in the same change: proposed shape,
  the new bound table, the escalation ladder rewritten (B adopted in bounded-staleness form;
  literal-zero and user-level kill as the next rungs; C still UX-only).
- `agents/knowledge/roles/revoked-device-directory.md` — **created** (CRC card for the new
  singleton — the one new role this ADR introduces).
- **Catalog edit at acceptance (not before):** `agents/knowledge/security-rules.md` — the ADR-0024
  token-lifetime paragraph (S2 neighborhood, `security-rules.md:48-50`) gains the amendment:
  *"Device-revocation latency on the mobile hosts is bounded by the RevokedDeviceDirectory refresh
  interval (≤ 30 s, ADR-0026), with the 30-min TTL as the fail-open backstop; the directory's
  `Enabled` flag and `RefreshSeconds` are security bounds — changing either requires a superseding
  ADR. Enforcement keys on the signed `device_id` claim — never on a client-sent header."*
- ADR-0024's `Superseded by:` pointer line updated to reference this ADR (the house pointer
  convention — the one sanctioned touch on an accepted ADR, precedent ADR-0010:6 / ADR-0013:6).

---

## Challenges pre-answered (author's anticipation — the panel writes below)

| # | Expected challenge | Author's position |
|---|---|---|
| P-1 | "The owner said IMMEDIATELY — 30 s is not immediate; build the per-request check." | 30 s is immediate in product terms (the directive contrasts with a 30-*minute* wait; no human re-opens the revoked device faster than the poll). The literal-zero escalation is a named one-file swap behind the seam (D2, Alt (a)) — if the owner rules 0 s, nothing in this design is thrown away. Deliberately left as an explicit panel/owner checkpoint. |
| P-2 | "Fail-open on a stale snapshot is a security hole." | Fail-open degrades to exactly ADR-0024's accepted-and-shipped posture (≤ 30 min), never worse; fail-closed converts a DB blip into a fleet-wide forced sign-out through the still-unfixed refresh-failure conflation (D4, cited at file:line). Bounded degradation beats amplified outage. |
| P-3 | "You're adding a claim — old tokens dodge enforcement." | For ≤ 30 min, once, at deploy — the exact status-quo bound (D6). Monotonic rollout; a claim-less token is never *less* enforced than it was yesterday. |
| P-4 | "Two hosts × OnTokenValidated wiring = the duplication ADR-0001 D4 killed." | The *logic* is one shared helper in Config; the per-host touch is a call site inside an event delegate each host already owns. Full AddJwt consolidation is real but out of scope — flagged in Consequences as a candidate consistency ticket, not smuggled in here. |
| P-5 | "Logout entries in a *revocation* directory conflate two lifecycles." | Same mechanism, same security meaning: 'this device's sessions predating T are dead.' The `iat` guard makes re-login clean, and it closes the logout-leaves-access-token-alive gap for free (D7). Separate directories would be two pumps for one snapshot shape. |
| P-6 | "Why poll — Postgres LISTEN/NOTIFY gives ~0 s for the same infra." | It narrows the bound but adds a persistent-connection seam per instance (reconnect handling, missed-notification reconciliation — which ends up being… a poll). Named as the cheap upgrade path in Alt (e); the poll alone already meets the product bound. |

## Challenge

*(Architect panel, challenger mode, 2026-07-15. Every citation below independently re-verified
against the working tree — none taken from the draft on trust. The author's six declared open
points are all addressed; CH-1 and CH-2 are the challenger's own finds.)*

**CH-1 — BLOCKING: a hostile revoked device deletes its own directory entry with one API call.**
The D2 snapshot predicate as drafted (`IsActive == false && DeactivatedOn >= cutoff`) is defeated
by the system's own re-registration path. Verified chain: `POST api/Device/Register` requires only
`Policy.Authenticated` (`Web.Mobile.Partner/Controllers/DeviceController.cs:16-26`) →
`RegisterDevice.Handler` reclaims the inactive tombstone
(`GetByUserAndDeviceIdIncludingInactiveAsync`, deliberately unfiltered —
`DeviceRepository.cs:21-28`) → `Device.MarkRegistered` flips `IsActive = true` and **never clears
`DeactivatedOn`** (`Device.cs:48-62`). So: victim revokes device A → A's still-valid access token
(live for up to `RefreshSeconds`, or the full TTL pre-claim) calls `Register` → the row no longer
matches the predicate → the entry vanishes from the next snapshot → **A's token rides out the full
30-min TTL**. A hostile client that simply re-registers on a timer (one `auth`-bucket call every
~10 s) wins this race *every* time and reduces this entire ADR to ADR-0024 — the exact bound the
owner rejected. The fix is one predicate: key the snapshot on `DeactivatedOn >= cutoff` **alone**
(reactivation-insensitive; `MarkRegistered` leaves the stamp, so the entry survives the horizon and
the `iat` guard alone decides — which it already does correctly for every legitimate re-login).

**CH-2 — The write path has a revoke↔rotation TOCTOU the "write path is correct" claim glosses.**
`RevokeByDeviceAsync` is load-then-mutate (`GetActiveByUserIdAsync` → in-memory `Revoke`,
`RefreshTokenService.cs:120-133`) committed by the UnitOfWork pipeline; `RotateAsync` is also
load-then-mutate with the new token inserted via `Issue` (`:43-105`). A rotation racing the revoke
can (a) insert a fresh refresh token after the revoke's read — never revoked — and (b) have its
`rotated` write to the old head land after the revoke's `device_revoked` write (EF updates the same
columns; last commit wins). The escaped chain then mints access tokens with `iat > revokedAt` that
**pass the directory forever**, and the deactivated device row has vanished from `GetMyDevices`
(filters `IsActive` — `DeviceRepository.cs:36-41`), so the victim has no re-revoke handle; the
chain lives until refresh expiry (up to 30 d rememberMe). Pre-existing — the same race defeats
today's revocation — but this ADR's headline ("~immediate") must not imply it's closed.

**CH-3 — "Immediately" (author's open point 1).** The owner's words are absolute; the design's
bound is ≤ 30 s. The author's product argument (no human re-opens the revoked device faster than
the poll; the directive's contrast target was the 30-*minute* wait) is plausible but it is the
owner's word being interpreted, and the delta to literal-zero is a real cost decision (one DB read
on every authenticated mobile request, forever, on a shared B1ms Postgres). This is not the
panel's call to make silently.

**CH-4 — Fail-open posture (open point 2) + the D4 evidence is already stale.** Fail-open is
right, but as drafted D4 leans on "both clients wipe tokens on any refresh failure" — verified
**no longer true for Android**: the concurrent client lane landed retryable-vs-terminal
(`RefreshResult.Unavailable` → keep session, fail request — `AuthAuthenticator.kt:94-105`). If the
conflation is the argument, the argument erodes as D4.5 finishes landing; the ADR must rest D4 on
the permanent argument (bounded degradation: worst case = ADR-0024's accepted posture) or invite
re-litigation.

**CH-5 — When the poll *itself* dies, who warns?** The staleness warning is emitted by the
refresher — but if the `BackgroundService` loop dies, the warner is dead too: silent fail-open
until TTL, forever, on every instance that lost its loop. Worse, .NET's default
`BackgroundServiceExceptionBehavior.StopHost` means an exception *escaping* `ExecuteAsync` kills
the entire host — a catastrophic fail-closed hiding inside a "fail-open" design. Neither host has
health checks to lean on (verified: `AddHealthChecks` exists only in `Cleansia.ServiceDefaults`,
the Aspire local-dev surface). The loop must be structurally un-killable and the test contract
must pin survival across consecutive faults, not just one.

**CH-6 — Claim-less login is a directory bypass; is it a hole (own attack (a))?** Traced: login
without `X-Device-Id` mints a token with no `device_id` claim (passes the directory forever) *and*
a refresh token with `DeviceId = null` — which `RevokeByDeviceAsync` can never match
(`RefreshTokenService.cs:129` null-guard) and which never appears as a revocable row in the
Devices list. So the bypass exists — but it is only reachable by a caller who **already holds the
password** (login is credentialed), and such a session's kill switches are exactly the user-level
ones: password reset (now revokes all refresh tokens — `ChangePassword.cs:110-113`, T-0407 lane,
verified in tree) and admin disable, both bounded by the TTL. A *stolen token* cannot shed its
claim (signature envelope); a *stolen device* runs the real app, which sends the header on every
request (Android `AuthInterceptor.kt:61`; iOS `HeaderAdapter.swift:61` + the header-parity
contract). The residue is coherent — but it is currently **unobserved**: nothing tells us whether
headerless mobile logins ever happen in production, which is the datum that would let a validator
close this hole for good.

**CH-7 — Clock semantics of `iat < revokedAt` (open point 6 + own attack (d)).** Both timestamps
are app-server clocks (mint: `DateTime.UtcNow` in both mint sites; stamp: the D7 repository
override — Postgres only stores). D9.3 as drafted names only the false-401 direction (self-heals).
The **false-pass** direction is unnamed: minter clock ahead of revoker clock → a token minted just
before the revoke carries `iat ≥ revokedAt` → passes the directory permanently. It must be named
and bounded (it is: the refresh chain is dead, TTL backstop applies).

**CH-8 — Logout entries (open point 3).** Attacked and holds: `UnregisterDevice` deactivates
without revoking refresh tokens (`UnregisterDevice.cs:39` — the logout endpoint revokes the
refresh token separately), so the directory entry is what kills a *stolen* still-valid access
token post-logout — a real gap-closure, not conflation. Logout→instant-relogin races self-heal
via the `iat` guard (with CH-1's amended predicate, a relogged-in device keeps a harmless entry
for the horizon — `iat` passes it). Checked: no other deactivation call sites exist that would
pollute the directory (exactly two in `src/`, both intentional).

**CH-9 — Kill switch (open point 4).** The flag is right (silent-`false` pinned by raw-file
test), but `Enabled=false` semantics are underspecified: does the refresher stop? If it stops, the
snapshot goes cold and re-enabling waits a full poll; if it polls, telemetry survives the off
state. Specify it.

**CH-10 — Per-host wiring duplication (open point 5).** Checked and accepted: the two hosts'
`AddJwt` bodies are already near-identical clones (Partner `ServiceExtensions.cs:141-194`;
Customer `:144-197` — verified same shape, different audience), the enforcement logic lands once
in `Cleansia.Config`, and the per-host touch is a call inside an event each host owns. Full
`AddJwt` consolidation stays a separate consistency ticket — folding it here would couple this
security change to a five-host refactor.

**CH-11 — Memory growth under a deactivation spike (own attack (c)).** Checked, no hole today:
only the two human-action call sites feed the directory, both behind the `auth` rate bucket;
horizon caps retention at TTL+5 min. The residual risk is a *future* bulk deactivation job
(stale-push-token cleanup is a plausible one) silently inflating the snapshot and triggering a
fleet-wide silent-refresh ripple — needs a standing rule, not code.

**CH-12 — Does `OnTokenValidated` actually cover everything (own attack (e))?** Checked, holds:
JwtBearer authenticates per HTTP request and the mobile hosts expose only controllers — no
SignalR/WebSocket/streaming endpoints anywhere in the five hosts (verified: zero matches), so
there is no long-lived authenticated connection that outlives its validation. Residual exposure is
in-flight request completion, already named in the D2 bound.

**CH-13 — T-0407 interaction (own attack (f)).** The concurrent lane landed refresh-token
revocation on password reset (`ChangePassword.cs:110-113`, reason `password_reset`, keep-none).
But the *access* tokens the attacker already holds ride out ≤ 30 min — on the account-takeover
recovery path, the same gap this ADR closes for device revocation. Should password reset also
feed the directory? Note the Context's userId-keying objection does **not** apply here: reset is
keep-none by design (killing all the user's devices is the *intent*), and the authenticated
change's spared session would self-heal via its live refresh token in one silent round trip. The
extension is cheap and pre-analyzed — but it is a user-level kill, which D9.4 explicitly excludes
from this ADR. It must become a named cross-ticket instruction, not silent scope creep in T-0414.

## Defense

*(The author instance is not live in this session; the author's pre-answers table (P-1..P-6) is
the standing defense and is ruled on where it covers. For the challenger's own finds, the lead
executes CONCEDE + REVISE on the author's behalf — every concession below is folded into the
artifact as a marked amendment, per the deliberation bar.)*

- **CH-1** — no pre-answer covers it. **CONCEDE + REVISE (A1):** snapshot predicate keys on
  `DeactivatedOn` alone; TC-REVOKE-NOW-9 pins it; D9.8 records the cosmetic-resurrection residue.
- **CH-2** — no pre-answer covers it. **CONCEDE the gap, REBUT blocking:** pre-existing (the race
  defeats today's revocation identically), milliseconds-wide, refresh capped at 10/min/IP by the
  anonymous `auth` bucket, and this ADR strictly narrows the world. Named as residue D9.7 +
  hardening instruction X2. Fixing it here would violate one-decision-per-ADR (it is a write-path
  concurrency decision, not an enforcement-read decision).
- **CH-3** — P-1 stands as the product argument; **ESCALATE** the word: conditional acceptance
  with the owner question surfaced (Verdict). The seam guarantees escalation is a swap, not a
  redesign, under either answer.
- **CH-4** — P-2 stands for the posture; **CONCEDE + REVISE (A6)** on the evidence: D4 now rests
  on bounded degradation as the permanent argument, with the client-lane state honestly recorded.
- **CH-5** — no pre-answer covers it. **CONCEDE + REVISE (A3):** un-killable loop structure
  mandated (StopHost named as the reason), warning emitted by the surviving loop, TC-REVOKE-NOW-6
  extended to consecutive-fault survival.
- **CH-6** — P-3/D9.2 cover the residue's existence; **CONCEDE the observability gap:** follow-up
  instruction X3 (WARN-log headerless mobile-host logins; tighten to a validator only on evidence
  of zero legitimate traffic). Not T-0414 scope.
- **CH-7** — D9.3 covered one direction; **CONCEDE + REVISE (A4):** false-pass direction named
  and bounded in D9.3.
- **CH-8** — P-5 stands; challenge verified-and-withdrawn (the checked items are recorded so the
  next panel doesn't re-derive them).
- **CH-9** — no pre-answer; **CONCEDE + REVISE (A5):** helper no-ops, refresher keeps polling.
- **CH-10** — P-4 stands; verified against both hosts' files.
- **CH-11** — D9.6 covered cost; **CONCEDE + REVISE:** the two-call-site verification and the
  standing rule for future bulk jobs folded into D9.6.
- **CH-12** — implicit in D3; verified, no change needed (recorded here as checked).
- **CH-13** — D9.4 covers the exclusion; **CONCEDE the instruction:** named cross-ticket
  instruction X1 in the Verdict so the extension is a decision waiting to be made, not folklore.

## Verdict

*(Architect panel lead, 2026-07-15 — different hat from the challenger's attack pass; every ruling
below re-checked against the cited code before adjudication.)*

| Challenge | Ruling | Disposition |
|---|---|---|
| CH-1 resurrection expunges enforcement | **WOULD BLOCK — resolved by A1** | Predicate keys on `DeactivatedOn` alone; `MarkRegistered` verified to never clear the stamp, so the entry outlives reactivation; the `iat` guard alone decides. TC-REVOKE-NOW-9 pins it; D9.8 records the cosmetic residue. Without A1 this ADR fails its own directive; with it, the attack dies at the first poll. |
| CH-2 revoke↔rotation TOCTOU | **RESOLVED — residue D9.7 + instruction X2** | Pre-existing, probabilistically narrow, strictly improved by this ADR. The hardening ticket (X2) is filed at acceptance, `security_touching: true`. |
| CH-3 "immediately" = 30 s? | **RESOLVED — conditional acceptance, owner escalation** | The design is correct under either answer (B-literal is a one-file swap behind `IRevokedDeviceDirectory`). The word is the owner's; the question goes to the owner verbatim (below). Building the ≤ 30 s form now is correct under both outcomes — T-0414 is NOT gated on the answer. |
| CH-4 fail-open + stale evidence | **RESOLVED — A6** | Posture ratified on the bounded-degradation argument (permanent); client-lane drift recorded honestly. Fail-closed stays rejected. |
| CH-5 dead poll = dead warner / StopHost | **RESOLVED — A3** | Loop structurally un-killable; TC-REVOKE-NOW-6 extended. A health-endpoint surfacing snapshot age is noted as a cheap later add if the hosts ever gain health checks — not required now. |
| CH-6 claim-less login bypass | **RESOLVED — residue stands + X3** | Coherent residue (credentialed callers are killed by user-level switches; stolen tokens can't shed the claim; real apps always send the header — verified in both clients). X3 adds the missing telemetry; tightening to a login validator is evidence-gated. |
| CH-7 skew false-pass | **RESOLVED — A4** | Named, bounded by dead refresh chain + TTL. No skew allowance added — it would blunt real enforcement for a sub-second theoretical. |
| CH-8 logout entries | **RESOLVED — defended (P-5)** | Same security meaning, `iat` guard makes it safe, bonus gap-closure confirmed. |
| CH-9 kill-switch semantics | **RESOLVED — A5** | Helper no-ops; refresher keeps polling; raw-file pin unchanged. |
| CH-10 wiring duplication | **RESOLVED — defended (P-4)** | Logic once in Config; `AddJwt` consolidation stays a separate consistency ticket. |
| CH-11 memory spike | **RESOLVED — folded into D9.6** | Two human-action call sites verified; standing rule recorded for future bulk jobs. |
| CH-12 OnTokenValidated coverage | **RESOLVED — verified** | No long-lived authenticated connections exist in any host; per-request validation covers the surface. |
| CH-13 T-0407 access-token residue | **RESOLVED — instruction X1** | User-level directory entries on password reset are the named next rung, pre-analyzed here (keep-none semantics make user-keying exact on the reset path). Requires its own ADR; explicitly NOT T-0414 scope. |

**Consensus: zero blocking challenges remain.** ADR-0026 is **ACCEPTED with amendments A1–A6
folded inline** (all marked), **conditional on one owner ratification** (below). The condition
does not gate T-0414: the ≤ 30 s form is the substrate of both possible answers.

**Owner escalation (via the orchestrator / `questions/open.md`):** *For device deletion, does
"revoked IMMEDIATELY" tolerate a ≤ 30-second enforcement bound (the default poll), or must it be
literal-zero — in which case we swap in the per-request read-through check behind the same seam,
at the cost of one extra DB read on every authenticated mobile request?*

**Cross-ticket instructions handed to the PM at acceptance (named, not folded):**
- **X1 (follow-up ADR + ticket):** extend the directory with **user-keyed entries fed by password
  reset** (`ChangePassword.cs:110-113` already kills the refresh tokens; the outstanding access
  tokens ride ≤ 30 min on the account-takeover recovery path). Pre-analysis recorded in CH-13.
  Owner of the decision: architect (superseding/extending ADR); NOT T-0414 scope.
- **X2 (hardening ticket, `security_touching: true`):** close the revoke↔rotation TOCTOU (D9.7) —
  `xmin` optimistic concurrency on `RefreshToken` (`UseXminAsConcurrencyToken`, no migration) so a
  racing rotation faults and re-validates, and/or set-based conditional revocation at commit.
- **X3 (small observability ticket):** WARN-log logins on the two mobile hosts that arrive without
  `X-Device-Id`; if production shows zero legitimate occurrences over a release cycle, a follow-up
  makes the header required at mobile login (closes the D9.2 residue for new sessions).
- **Catalog edit** (from "Living docs" above): the `security-rules.md` S2-neighborhood amendment
  ships with this acceptance — executed by the architect lane as a follow-up (outside this panel's
  writable surface; ADR-0024 precedent).

This ADR is now **immutable** — supersede, never edit. (The one sanctioned future touch: the
`Superseded by:` pointer line.)
