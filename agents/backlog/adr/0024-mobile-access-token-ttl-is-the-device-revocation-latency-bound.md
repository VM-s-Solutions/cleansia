# ADR-0024 — Device-revocation latency: the mobile access-token TTL drops 1440 → 30 min (config-only) and becomes the documented revocation bound; per-request session checks and push-driven logout are named, deferred escalations

- **Status:** accepted (panel verdict 2026-07-15, amendments A1–A5 folded in — see Verdict)
- **Date:** 2026-07-15
- **Supersedes:** — (no prior ADR governs token lifetimes; ADR-0001's per-host refresh pin and
  ADR-0003's `auth` rate-limit policy are **untouched and load-bearing** here)
- **Superseded by:** ADR-0026 **partially** (2026-07-15, accepted — panel verdict, conditional on
  the owner's ≤ 30 s ratification) — only **D2** (the "TTL is the
  bound / no per-request read path" contract) and **D3-B** (option B's deferral; its named trigger —
  a product demand for instant revocation — was exercised by the owner's directive). **D1 (30-min
  TTL) stands unchanged as the backstop bound**; D3-C (push = UX only) is reaffirmed by ADR-0026.
- **Applies to:** backend config (mobile hosts) | mobile clients (behavioral, no code change)
- **Ticket:** T-0405 (`security_touching: true`, priority high, owner-prioritized) · related:
  T-0406 (Android partner forced-signout collector), T-0403/T-0404 (iOS push receive-side),
  T-0400 (web cookie auth / SameSite)

> **One decision:** *how long a revoked mobile device keeps API access.* The bound becomes the
> access-token TTL, and the TTL on the **two mobile hosts** (`Cleansia.Web.Mobile.Partner`,
> `Cleansia.Web.Mobile.Customer`) drops from **1440 min to 30 min** — a config-only change riding
> the already-verified refresh/rotation/forced-logout machinery. Per-request device checks
> (option B) and push-driven force-logout (option C) are recorded as the escalation path with
> explicit revisit triggers, **not** adopted. The web hosts' 1440-min TTL is a *different* exposure
> (no device semantics on web sessions) and is deliberately **out of scope** — named as a follow-up
> decision, not silently folded in. Once `accepted` this is immutable — supersede, never edit.

---

## Context

### The observed hole and what is already sound

Owner-observed: revoke device A from device B → A stays fully functional. The revocation *write*
path is correct and tested — `RevokeDevice.Handler` deactivates the device row **and** calls
`RevokeByDeviceAsync(userId, deviceId, "device_revoked")`
(`src/Cleansia.Core.AppServices/Features/Devices/RevokeDevice.cs:43-44`), which revokes exactly the
target device's refresh tokens via the persisted `RefreshToken.DeviceId` (stamped from `X-Device-Id`
at login, carried across rotation — `RefreshTokenService.cs:98`; pinned by
`RefreshTokenServiceRevokeByDeviceTests` + Postgres parity). Rotation-reuse theft detection revokes
the whole chain with its own commit (`RefreshTokenService.cs:57-71`). The refresh handler also
re-checks `user.IsActive` on every rotation (`Features/Auth/RefreshToken.cs:83-88`).

What makes the hole is the **read side**: the outstanding access JWT is validated purely by
signature + expiry — there is **no per-request check** of `Device.IsActive` or any token state (no
`jti`, no denylist, no security-stamp middleware; verified: the hosts' pipelines carry no such
middleware). And `AccessTokenExpMinutes` is **1440 (24 h) on every host**:

| Where | Value |
|---|---|
| Code default — `src/Cleansia.Infra.Common/Configuration/JwtSettingsConfig.cs:11` | 15 |
| `Web.Mobile.Partner` `appsettings.json:23` / `appsettings.Production.json:20` | **1440** |
| `Web.Mobile.Customer` `appsettings.json:23` / `appsettings.Production.json:20` | **1440** |
| `Web.Partner` / `Web.Admin` / `Web.Customer` (both files each) | **1440** |
| HostTests / IntegrationTests configs | 15 |

Tokens are minted in two places, both reading the same setting: `TokenService.cs:74` (login) and
the `RefreshToken` handler's private `GenerateAccessToken` (`RefreshToken.cs:125`). All five hosts
validate with `ClockSkew = TimeSpan.Zero` (each host's `Extensions/ServiceExtensions.cs`), so the
revocation latency bound is **exactly** the TTL — no skew slack. Deployment reality: Bicep injects
only the JWT secret (`deploy/bicep/main.bicep:255`); the TTL ships from `appsettings.Production.json`
in the artifact, so a repo config edit **is** the production change.

### Why the mobile refresh machinery can carry a short TTL (the UX-cost audit)

Both mobile platforms refresh **reactively on 401** — never on a timer, never proactively:

- **Android** — `AuthAuthenticator` (`core/auth/AuthAuthenticator.kt:49-94`): OkHttp invokes it only
  on a real 401; single-flight via `synchronized` + stale-token comparison; on refresh success the
  request retries once with the new bearer; on refresh failure it clears `TokenStore` + all
  `SessionScopedCache`s and emits `ForcedSignOut(SessionExpired)`.
- **iOS** — `GeneratedClientAuthBridge.executeWithRetry` (`:39-54`) catches the 401, funnels into the
  single-flight `SessionRefresher` actor; failure → token wipe + `forcedSignOutStream` → both apps
  route to login. iOS derives access expiry from the JWT `exp` claim itself (`Auth.swift:324-326`) —
  **no client hardcodes the 24 h lifetime**, so no client contract change (T-0405 AC3).
- **Web (for contrast)** — the three Angular apps 401→single-flight-refresh→replay via
  `error.interceptor.ts` + refresh-coordinator, with HttpOnly cookie carriers (`AuthCookieWriter`).
  Note the access **cookie's** `Expires` is pinned to `RefreshTokenExpiresAt`
  (`AuthCookieWriter.cs:49-51`), so a shorter JWT TTL would not change cookie lifetime — only how
  often the in-cookie JWT goes stale.

So the marginal cost of a short TTL on mobile is: **one extra silent round trip per TTL-boundary of
active use** (the 401 + the refresh call). No polling, no battery-relevant background work — an idle
app refreshes zero times.

**The one real UX risk found:** both clients conflate *transport failure* with *server rejection*
on the refresh call — Android `catch (t: Throwable) → null → forced sign-out`
(`AuthAuthenticator.kt:75-87`); iOS `refresh()` returns `nil` on `network.unreachable` and
`SessionRefresher.performRefresh` force-signs-out on any `nil` (`Auth.swift:221-239`,
`SessionRefresher.swift:54-63`). A 429 from the `auth` rate-limit bucket lands in the same path.
Today that path fires ~once/day per device; a shorter TTL multiplies how often it is exercised,
which multiplies exposure to spurious forced logouts on flaky networks. This risk **scales linearly
with refresh frequency** and is the primary reason the TTL below is 30 and not 15. It is partially
mitigated structurally: because the refresh is 401-triggered, the network was provably up
milliseconds before the refresh call — the failure window is the gap between the two requests.
**That adjacency argument covers transport failure only** — a 429 arrives over a healthy network and
is not mitigated by it; the burst analysis and the client-side fix live in Consequences + D4.5
(panel amendment A1).

### Why this decision is scoped to the mobile hosts

Device revocation is **structurally a mobile-session concept**: only the mobile clients send
`X-Device-Id` (verified — zero matches in `src/Cleansia.App`), so web refresh tokens are stamped
`DeviceId = null` and the `RevokeByDeviceAsync` null-guard (`RefreshTokenService.cs:129`,
deliberate, test-pinned) means **web sessions can never be device-revoked**. Shortening the web TTL
would do nothing for this threat. The web hosts' 24 h JWT is a *real but different* exposure
(admin/partner role or `IsActive` changes take up to 24 h to bite) with a *different* risk profile:
the customer app is SSR (an interceptor-driven refresh during SSR with rotating single-use cookie
tokens is unverified — a mis-propagated `Set-Cookie` would trip the rotation-reuse theft cascade),
and the web cookie story is actively moving (T-0400). One decision per ADR: the web-host TTL gets
its own ticket + decision after the web refresh path is verified hot. Folding it in here would
couple a config flip to an unverified seam — the exact coupling the per-audience host split exists
to prevent.

---

## Decision

### D1 — TTL: `AccessTokenExpMinutes = 30` on the two mobile hosts

Change exactly four files (config-only, no code):

- `src/Cleansia.Web.Mobile.Partner/appsettings.json` (`JwtSettings:AccessTokenExpMinutes` → `30`)
- `src/Cleansia.Web.Mobile.Partner/appsettings.Production.json` (→ `30`)
- `src/Cleansia.Web.Mobile.Customer/appsettings.json` (→ `30`)
- `src/Cleansia.Web.Mobile.Customer/appsettings.Production.json` (→ `30`)

**Why 30 within the ticket's 15–60 band:**
- **Not 60:** the number *is* the security posture — it is the documented upper bound on a
  revoked/stolen device's residual access (ClockSkew=Zero makes it exact). 60 min buys a negligible
  reduction in silent refresh traffic (1/h → 0.5/h active) for double the exposure window.
- **Not 15:** refresh frequency doubles, and with it (a) exposure to the transport-failure→forced-
  signout conflation documented above, (b) pressure on ADR-0003's anonymous per-IP `auth` bucket
  (10/min/IP — refresh is anonymous; a NAT/CGNAT-colocated fleet shares one bucket. Steady-state:
  ~300 co-located active devices at 30 min TTL vs ~150 at 15 min. **The binding constraint is the
  60-second fixed window, not the hourly average** — a synchronized wake after an idle gap > TTL
  puts every co-located device's refresh into the same minute, hitting the cap at ~10 devices; a
  15-min TTL doubles how often fleets sit past-expiry and wake together — panel amendment A2), and
  (c) `RefreshTokens` row growth
  (each rotation inserts a row: ~2/h active at 30 min vs ~4/h at 15; `DeleteStaleAsync` cleanup
  exists). 30 is the knee of the curve: a **48× smaller** revocation window than today at ~2 silent
  round trips per active hour.

### D2 — The bound becomes a documented contract, not an accident

The standing rule (recorded in the living doc, enforced by the test contract): **revoking a device
ends its API access within `AccessTokenExpMinutes` of the mobile host that issued the token.** The
sequence that guarantees it: revoke → the device's refresh tokens are dead (`device_revoked`) → the
outstanding JWT dies ≤ 30 min later → the next 401-triggered refresh fails (`InvalidRefreshToken`)
→ the client wipes tokens/caches and force-signs-out. Any future change to `AccessTokenExpMinutes`
on a mobile host is a change to this security bound and requires a superseding ADR — it is no
longer a free config knob.

### D3 — Options B and C are the escalation path, with named revisit triggers

- **B — per-request session validation** (cached `Device.IsActive` / `jti` denylist / security
  stamp): the only way to get revocation latency ≪ TTL. Deferred: it adds a read (even cached) to
  **every authenticated request** on hot paths, and a cache-invalidation seam, to shave ≤ 30 min off
  a bound the product has not demanded be instant. **Revisit trigger:** a real incident where ≤ 30
  min residual access caused harm, or a compliance/product requirement for instant kill.
- **C — push-driven force-logout on `device_revoked`**: pure UX latency improvement (the device
  signs itself out in seconds instead of at the next expiry); **not a security control** (a thief
  can drop network/uninstall push; C without a short TTL is theater). Deferred until the FCM
  receive-side lands on iOS (T-0403/T-0404) — then it is cheap: a session event in
  `NotificationEventCatalog` + client handling. **Revisit trigger:** T-0404 closing.

### D4 — Accepted residues (explicit)

1. **≤ 30 min residual access on a revoked device.** Bounded by S1/S3 (the JWT only reaches the
   user's own data) and by D2's contract. Accepted by the owner's option-A lean; B is the recourse.
2. **Legacy `DeviceId = null` refresh tokens can never be device-revoked**
   (`RefreshTokenService.cs:129` null-guard, deliberate + test-pinned:
   `RevokeByDeviceAsync_Leaves_A_Null_DeviceId_Token_Alive`). They die by expiry/logout/`IsActive`.
   The shorter TTL does not change this; recorded per the ticket note.
3. **Web hosts keep 1440 for now** — a named follow-up decision (see Context), not an endorsement.
   The PM files the ticket: "Web-host access-token TTL + refresh-path verification (SSR cookie
   propagation, rotation single-use vs SSR replay, T-0400 interplay)." **The admin host is the
   priority case inside that follow-up and is separable** (panel amendment A3): the admin SPA's
   401→refresh interceptor path has no SSR complexity — its TTL decision must not be gated on the
   customer-SSR verification. The admin 24 h JWT is the highest-privilege token in the system; the
   follow-up inherits `security_touching: true` and high priority.
4. **Android partner has no forced-signout *collector*** (T-0406): its tokens/caches are still
   cleared on failed refresh (the security property holds — API access ends), but the UI parks on
   the current screen until cold start. UX-only, already ticketed, not a blocker for this ADR.
5. **Both mobile clients treat transport failure and 429 as terminal on the refresh path** (panel
   amendment A4; Android `AuthAuthenticator.kt:75-87` + `AuthRepository.kt:139-141`; iOS
   `Auth.swift:293-298` + `SessionRefresher.swift:60-63`). Accepted at current fleet density
   (the 429 cliff needs >10 co-located devices refreshing in one 60 s window), **but no longer a
   watch item**: the PM files the client ticket at acceptance — *"mobile refresh: classify
   retryable (transport error, 429, 5xx) vs terminal (401/400 rejection); on retryable, fail the
   request without wiping tokens or emitting ForcedSignOut."* Non-blocking for the TTL flip.
   **Urgency trigger:** the first `auth`-policy 429 in production telemetry (ADR-0003 D8 counter),
   or fleet-per-IP density approaching ~10 co-located actives. Interim config relief valve:
   `RateLimiting:Auth:AnonPermitLimit` (`RateLimitPolicies.cs:40`).
6. **Password change does not revoke sessions** (panel finding, amendment A4: `ChangePassword.cs`
   has no refresh-token revocation) — the device-revoke flow this ADR governs is the account
   holder's *only* session kill switch. A different decision (which flows revoke which sessions),
   not folded here; the PM files the follow-up: "password change/reset revokes the user's other
   refresh tokens." Until it lands, the documented recovery playbook for a compromised account is
   *revoke the device* (≤ 30 min) or admin `IsActive = false` (≤ 30 min on mobile), not a password
   change.

---

## Alternatives considered

- **Option B now (per-request device/session check).** Rejected for now: per-request cost on every
  authenticated call + a new cache-invalidation seam, to close a ≤ 30 min window nobody has shown
  must be instant. It also does not remove the need for a sane TTL (a 24 h JWT with a device check
  still leaks 24 h of access the moment the check's cache is stale or the middleware is bypassed on
  a new host — the fail-open shape ADR-0001 D4 exists to prevent). B *composes with* D1 later; D3
  records when.
- **Option C now (push force-logout).** Rejected as a security answer: unreliable-by-design channel,
  attacker-suppressible, and the iOS receive-side doesn't exist yet (T-0404). It is a UX layer over
  D1, not a substitute — deferred to its trigger.
- **15 min / 60 min.** Rejected — see D1's band analysis (the number is argued, not vibes).
- **All five hosts uniformly to 30.** Rejected here: does nothing for the device-revocation threat
  on web (null `DeviceId` — structurally unrevocable), while flipping a currently-cold web refresh
  path (SSR + rotating cookie tokens + open T-0400) hot in the same change. Named follow-up instead.
- **Shorten the refresh-token lifetime too.** Out of scope — refresh tokens are already revocable
  server-side (that machinery is the part that *works*); their lifetime governs re-login cadence,
  not revocation latency. No change.
- **Do nothing (revocation already kills refresh).** Rejected — that is the status quo the owner
  observed failing: "revoked" that takes 24 h to mean anything is indistinguishable from broken.

---

## Consequences

**Cheaper / safer:**
- Revoked/stolen mobile device: residual access drops **24 h → ≤ 30 min**, config-only, riding
  tested machinery. Disabled users (`IsActive = false`) and role changes now also bite within 30 min
  on mobile (refresh re-mints claims through `RefreshToken.Handler`'s user re-read).
- The TTL is now a *documented bound* with a test contract — a future 1440 regression fails a test
  instead of waiting for the next incident.
- B and C have recorded triggers — the next escalation is a lookup, not a re-derivation.

**More expensive (accepted):**
- ~2 silent refresh round trips per active-use hour per mobile device (from ~1/day). Backend cost:
  one indexed `GetByTokenHashAsync` read + 2 writes per rotation — noise at current scale.
  `RefreshTokens` grows ~16 rows/day per 8h-active device; `DeleteStaleAsync` handles it.
- The transport-failure→forced-signout conflation in both mobile clients is exercised ~50× more
  often. Transport failures are mitigated by 401-adjacency (see Context); **429s are not** — the
  client-side classification fix (retryable vs terminal) is a **ticket the PM files at acceptance**,
  with its urgency trigger recorded in D4.5. Non-blocking for the flip.
- NAT-colocated fleets share ADR-0003's 10/min anonymous `auth` bucket, and **the binding constraint
  is the 60-second fixed window** (`QueueLimit = 0`), not the hourly average: a synchronized wake
  after an idle gap > 30 min (shift start on shared wifi — every device's first request 401s and
  refreshes) hits the cap at ~10 co-located devices, and the overflow's re-logins land in the same
  bucket. Accepted at current fleet density. Watch the D8 rejection counter for `auth` 429s after
  rollout; `RateLimiting:Auth:AnonPermitLimit` is the same-day config relief valve; D4.5's client
  fix removes the sign-out sting.

**No migration, no NSwag, no client code change** (iOS/Android read `exp` from the JWT; web is
untouched). Deploy is the normal artifact roll; no Bicep change.

---

## How a reviewer verifies compliance

**Mechanical:**
1. `git diff` touches exactly the four config files in D1, value `30`, key
   `JwtSettings:AccessTokenExpMinutes`; web hosts' appsettings **byte-identical**.
2. No code change in `TokenService.cs`, `RefreshToken.cs`, `RefreshTokenService.cs`, any
   `ServiceExtensions.cs` (ClockSkew stays `Zero`), any client.
3. `deploy/bicep/**` untouched (the secret ref `JwtSettings__Secret` at `main.bicep:302` is the only
   JwtSettings surface — corrected from `:255` by panel amendment A5; no env override of the TTL
   exists, so the appsettings edit is the production change).

**Test contract (T-0405 AC2/AC3 — names for the backend ticket):**
- **TC-REVOKE-TTL-1 — revoked device's refresh fails.** Integration: login with `X-Device-Id: A`
  → `RevokeDevice` for A → present A's refresh token to the `RefreshToken` handler → failure with
  `BusinessErrorMessage.InvalidRefreshToken`; device B's refresh still rotates. (Chains
  `RevokeDevice.Handler` + `RotateAsync` end-to-end — extends `RefreshTokenFlowTests`, which today
  covers logout/expiry/reuse but not the device-revoked reason.)
- **TC-REVOKE-TTL-2 — expiry is enforced with ClockSkew=Zero semantics** *(re-specified by panel
  amendment A5 — the original "at/after the boundary" phrasing was unimplementable: both mint sites
  use raw `DateTime.UtcNow` (`TokenService.cs:74`, `RefreshToken.cs:125`) and mechanical verifier #2
  forbids touching them, so no fake-clock exists).* Ratified mechanism: `AccessTokenExpMinutes` is a
  `double` (`JwtSettingsConfig.cs:11`) — the test boots a mobile host with the setting overridden to
  a fractional value (~`0.05` ≈ 3 s) layered last; login → authed call succeeds → wait past expiry →
  same call → 401. This pins the property that matters: under the default 5-minute clock skew a
  3-second-expired token would still pass, so the test fails if `ClockSkew = Zero` regresses.
  *(Follow-up hygiene ticket, PM files: mint `exp` through the already-injected `TimeProvider` —
  `TokenService` receives it and ignores it for `Expires` — after which an exact-boundary fake-clock
  test becomes possible.)*
- **TC-REVOKE-TTL-3 — forced-signout propagation.** Client-side pins (extend existing suites):
  Android — refresh returning null clears `TokenStore` + session caches and emits
  `ForcedSignOut(SessionExpired)`; iOS — `SessionRefresher` → `.signedOut` wipes the keychain store
  and emits on `forcedSignOutStream`. (Both largely exist; the ticket re-runs them as the AC2
  "forced logout path" evidence.)
- **TC-REVOKE-TTL-4 — config pin (raw-file, not bound-config)** *(re-specified by panel amendment
  A5 — the original HostTests bound-config pin could never pass: the harness layers
  `appsettings.HostTests.json` LAST ("these override whatever the host's own appsettings.json
  carries", `HostTestApplicationFactory.cs:40-44`) and that overlay pins `AccessTokenExpMinutes: 15`,
  so a bound `IJwtSettings` assertion reads 15, never 30).* Ratified mechanism: a plain xUnit test
  (no host boot) locates the repo root, parses the **four D1 files as JSON**, and asserts
  `JwtSettings:AccessTokenExpMinutes == 30` in each — **and** asserts the three web hosts' files
  (`Web.Partner`/`Web.Admin`/`Web.Customer`, both files each) still carry `1440`, so both a silent
  mobile revert *and* a silent scope-creep flip of the web hosts fail a test until the D4.3
  follow-up ADR supersedes the pin.
- **TC-REVOKE-TTL-5 — null-DeviceId residue.** `RevokeByDeviceAsync_Leaves_A_Null_DeviceId_Token_Alive`
  stays green and unedited (the residue is pinned, not accidentally "fixed").

---

## Living docs updated with this ADR

- `agents/architecture/decisions/auth-sessions.md` — **created**: session/token-lifetime topic page
  (current shape, the TTL-is-the-bound rule, the B/C escalation ladder, the web-host follow-up).
- No `agents/knowledge/roles/*` change — no new aggregate/service/adapter; the existing
  `RefreshTokenService` responsibilities are unchanged.
- No catalog (`knowledge/*.md`) edit until acceptance; on acceptance, `security-rules.md` gains one
  line under S2's neighborhood: *"access-token TTL on a host that issues device-bound sessions is a
  security bound — changing it requires an ADR"* (the Verdict confirms or drops this).

---

## Challenges pre-answered (author's anticipation — the panel writes below)

| # | Expected challenge | Author's position |
|---|---|---|
| P-1 | "30 min of stolen-device access is still a breach window — B is the real fix." | Partially conceded by design: D3 names B as the escalation with a trigger. But B *plus* a 24 h TTL is worse than D1 alone (fail-open risk on new hosts, cache staleness), so D1 ships first either way; B is additive, never alternative. |
| P-2 | "Why not all hosts? Admin 24 h JWTs are scarier than mobile." | True and out of scope *for this threat*: web sessions carry `DeviceId = null` — device revocation cannot touch them at any TTL. The web TTL is a distinct decision gated on verifying the SSR/cookie refresh path (D4.3 files it). Folding it in couples this config flip to an unverified seam. |
| P-3 | "Short TTL will spam forced logouts on bad networks." | The conflation is real (cited at file:line) but 401-adjacent (network provably up moments before). Quantified, watch-item flagged, client-side fix named if it materializes. 30-vs-15 was chosen partly on this axis. |
| P-4 | "Config-only means no test will catch a silent 1440 revert." | TC-REVOKE-TTL-4 pins the bound value in HostTests; D2 makes the knob ADR-governed. |
| P-5 | "The refresh endpoint is anonymous and rate-limited per-IP — you just multiplied its traffic 48×." | Per-device it is ~2 req/h active — the multiplier applies to a near-zero base. The NAT saturation math is in D1/Consequences with the ADR-0003 D8 counter as the watch. |

## Challenge

*(Architect panel, challenger mode, 2026-07-15. Every citation below re-verified against code by the
challenger — none taken from the draft on trust.)*

**C1 — The revoke button is the victim's ONLY kill switch, and you're giving it a 30-minute fuse.**
The draft undersells the persona. Verified: `ChangePassword.cs` performs **no** refresh-token
revocation (zero references to `IRefreshTokenService` in the handler) — a victim who changes their
password after a theft leaves the thief's session fully alive. There is no admin "kill all sessions"
either; `IsActive = false` also only bites at the next refresh. So for a stolen partner device, the
*entire* recovery toolkit is: revoke device → wait ≤ 30 min while the thief reads assigned-order
customer addresses (third-party PII, not just the victim's own data — S1/S3 scope the JWT to the
*user's* data, and a partner's data *includes* their customers' addresses). The owner observed this
exact failure class. Why is that not the incident that triggers option B now — and if not B, why is
30 the number rather than 15, given the persona pays the full TTL?

**C2 — "Web is out of scope" quietly rides the customer app's SSR excuse onto the admin host.** The
Context paragraph justifies excluding all three web hosts with (a) `DeviceId = null` (true, verified:
zero `X-Device-Id` matches in `src/Cleansia.App`; null-guard at `RefreshTokenService.cs:129`) and
(b) "the customer app is SSR / refresh path unverified / T-0400 moving." But (b) only describes the
**customer** app. The admin SPA's 401→refresh interceptor is live client-side code with no SSR
complexity — a TTL drop there is as config-only as this one, and the admin JWT is the
highest-privilege 24 h token in the system (`Web.Admin/appsettings*.json:19`). Lumping it behind the
slowest host's verification schedule looks like scope convenience, not scope discipline.

**C3 — The 429 path is systematic at fleet density, and 401-adjacency does not cover it.** The
draft's mitigation ("the network was provably up milliseconds before") answers *transport* failure
only. A 429 arrives over a perfectly healthy network. Verified chain: the mobile refresh endpoint is
`[AllowAnonymous]` inside `[EnableRateLimiting("auth")]` (`Web.Mobile.Customer/Controllers/`
`AuthController.cs:25,102-103`) → anonymous partition → **10/min per IP** (`RateLimitPolicies.cs:48`);
Android maps any non-2xx to `null` → token wipe + `ForcedSignOut` (`AuthRepository.kt:139-141` →
`AuthAuthenticator.kt:82-87`); iOS maps any non-2xx to `ApiError` → `nil` → `forceSignOut`
(`Auth.swift:293-294,228-232` → `SessionRefresher.swift:60-63`). Now the scenario the TTL flip
*creates*: after any idle gap > 30 min, **every** device's first request 401s and refreshes. A
co-located fleet (cleaning-company depot, shared wifi, shift start) puts > 10 refreshes into one
60-second fixed window at **~10 devices** — the overflow is 429'd and **force-signed-out**, and
their re-logins land in the *same* anonymous bucket, compounding. At 24 h TTL this herd is nearly
unreachable; at 30 min it is every morning. That is not a "watch item" shape — it is a designed-in
cliff. Why is client-side retryable-vs-terminal classification not a blocking predecessor?

**C4 — The "~300 devices/IP" margin is an average-rate answer to a burst-window constraint.** The
D1 math (10/min × 60 min ÷ 2 refreshes/device-hour ≈ 300) assumes uniformly spread refreshes. The
limiter is a **60-second fixed window** (`RateLimitPolicies.cs:170-176`, `QueueLimit = 0`); the
binding constraint is 10 *per minute*, and synchronized wakes (C3) hit it at ~10 co-located devices
— an order of magnitude below the headline number. The margin claim as written would mislead the
next reader into thinking there's 30× headroom.

**C5 — Two of the five ratified tests cannot be written as specified.**
- **TC-REVOKE-TTL-2**: both mint sites use raw `DateTime.UtcNow` (`TokenService.cs:74` — note
  `TimeProvider` is *injected and ignored* for `exp`, used only for `RecordLogin` at `:32`;
  `RefreshToken.cs:125` doesn't inject it at all), and mechanical verifier #2 forbids touching those
  files. No fake-clock exists → "rejected at/after the boundary, not 5 min later" is unimplementable
  as written.
- **TC-REVOKE-TTL-4**: the HostTests harness layers `appsettings.HostTests.json` **last** —
  "these override whatever the host's own appsettings.json carries"
  (`HostTestApplicationFactory.cs:40-44`) — and that overlay pins `AccessTokenExpMinutes: 15`
  (`appsettings.HostTests.json:15`). A bound-`IJwtSettings` assertion reads **15, never 30**; the
  pin as specified can never pass and P-4's promise ("a 1440 regression fails a test") is currently
  vapor.
- Minor: the Bicep citation is stale — the only `JwtSettings` surface is `JwtSettings__Secret` at
  `main.bicep:302`, not `:255`.

## Defense

**D-C1 — REBUT on B, CONCEDE the adjacent gap.** The persona is real but does not change the
ranking. (1) The owner's own ticket recommends A ("A (recommended, minimal)" — T-0405) and the
observed failure was *"revoked and nothing happened for a day"*, i.e. an unbounded-feeling latency,
not a demand for instant kill. (2) B is not a same-day fix: it introduces a per-request read on
every authenticated call plus a cache-invalidation seam, and — the fail-open point stands — B *with*
a 24 h TTL is worse than D1 alone; D1 ships first under every ordering, so deferring B costs nothing
on the critical path. (3) 30-vs-15 for this persona buys 15 minutes against a thief who must know
the victim's assignments matter, while 15 doubles the herd/conflation exposure in C3 — the knee
argument survives the persona. **Conceded and folded in:** the `ChangePassword`-doesn't-revoke gap
is a genuine adjacent hole the panel found; it becomes a named follow-up (new D4.6). It is a
*different decision* (which flows revoke which sessions) — folding it here would violate
one-decision-per-ADR.

**D-C2 — REBUT on scope, CONCEDE the sharpening.** The scope test is the threat, not the effort:
this ADR's subject is *device-revocation latency*, and no web TTL at any value changes device
revocation (structurally unrevocable, `DeviceId = null`). Admin-token longevity is a real but
**different** threat with a different analysis (role/IsActive latency, cookie carriers, CSRF
interplay, T-0400 in flight). One decision per ADR. **Conceded and folded in:** the follow-up must
not be gated on the customer-SSR verification — D4.3 is sharpened to name the admin host explicitly
as the priority case and as *separable* (the admin SPA path can move first).

**D-C3 — CONCEDE the analysis, REBUT "blocking."** The depot-herd chain is correct and the
401-adjacency argument indeed covers only transport — the text is revised to say so honestly. But
blocking the flip on a client release inverts the risk ledger: the herd cliff needs > 10 co-located
active devices refreshing in the same 60 s window, and the current fleet (per the owner's own
testing scale and rollout stage) is nowhere near that density, while the 24 h revocation hole is
*live today* on every device. Observability exists now (ADR-0003 D8 rejection counter, by policy
name), and `RateLimiting:Auth:AnonPermitLimit` (`RateLimitPolicies.cs:40`) is a same-day config
relief valve if 429s appear before the client fix ships. **Folded in:** the classification fix is
upgraded from "watch item" to a **ticket the PM files at acceptance** (both platforms: sign out only
on server *rejection*; transport error / 429 → fail the request without wiping tokens), with a named
urgency trigger (new D4.5). Non-blocking, but no longer folklore.

**D-C4 — CONCEDE + REVISE.** The 300 figure was steady-state and the fixed-window burst constraint
is the binding one. D1(b) and the Consequences bullet are rewritten around the per-minute window and
the ~10-device synchronized-wake threshold, with the config lever named.

**D-C5 — CONCEDE + REVISE (all three).** TC-REVOKE-TTL-2 is re-specified to a mechanism that is
implementable *without* touching the mint sites: `AccessTokenExpMinutes` is a `double`
(`JwtSettingsConfig.cs:11`), so the test host overrides it to a fractional value (~3 s) layered last
and asserts the 401 after real expiry — which *also* distinguishes `ClockSkew = Zero` from the
5-minute default (the actual property the contract pins; an exact-boundary fake-clock test is named
as a follow-up behind a `TimeProvider`-minting hygiene ticket). TC-REVOKE-TTL-4 is re-specified as a
**raw-file pin** (parse the four D1 JSON files from the repo; no host boot) so the HostTests overlay
cannot mask a revert — and it additionally pins the three web hosts at 1440 so a silent scope-creep
flip fails a test until the follow-up ADR lands. The Bicep citation is corrected to `:302`.

## Verdict

*(Architect panel lead, 2026-07-15. Different instance from the author; the challenger's citations
were independently re-verified before ruling.)*

| Challenge | Ruling | Disposition |
|---|---|---|
| C1 — persona demands B / 15 | **RESOLVED — defended** | Owner's ticket recommends A; B's trigger recorded in D3; D1-first is correct under every ordering (B + 24 h TTL is the fail-open shape). The adjacent `ChangePassword` gap the panel found is folded in as **D4.6** (amendment A4) — a genuine catch, correctly kept a separate decision. |
| C2 — admin host scoped out | **RESOLVED — defended with concession** | Scope holds on the threat test (web tokens are structurally device-unrevocable — verified). The SSR excuse indeed only covered the customer app: **D4.3 amended (A3)** to name the admin host as the priority, *separable* case in the follow-up ticket. |
| C3 — 429 herd: blocking? | **RESOLVED — conceded, non-blocking** | The depot-herd analysis is correct and now honest in the text (A1). Blocking the flip would keep a live 24 h revocation hole open to protect against a cliff that needs ~10 co-located actives — density not present today, observable via the D8 counter, with a same-day config relief valve. The client classification fix is upgraded to a **filed-at-acceptance ticket with an urgency trigger (D4.5, A4)**. |
| C4 — NAT math framing | **RESOLVED — conceded** | D1(b) and Consequences rewritten around the 60-second fixed-window burst constraint (A2). The 30-min choice itself *survives* the corrected math — 15 min doubles the frequency of past-expiry synchronized wakes. |
| C5 — TC-2/TC-4 infeasible | **RESOLVED — conceded** | Both tests re-specified to implementable mechanisms (A5): TC-2 fractional-TTL boot pinning ClockSkew=Zero; TC-4 raw-JSON file pin covering all ten appsettings files. Bicep cite corrected to `main.bicep:302`. `TimeProvider`-minting hygiene ticket named. |

**Consensus: zero blocking challenges remain. ADR-0024 is ACCEPTED with amendments A1–A5 folded in
above (all marked inline). D1 stands unchanged: `AccessTokenExpMinutes = 30`, the two mobile hosts,
config-only.**

**Confirmed catalog edit** (from "Living docs" below): `agents/knowledge/security-rules.md` gains,
in S2's neighborhood: *"The access-token TTL on a host that issues device-bound sessions is a
security bound, not a tuning knob — changing it requires a superseding ADR (ADR-0024)."* Executed by
the architect lane as a follow-up (outside this panel's writable surface).

**Actions handed to the PM at acceptance** (all named in D4, gathered here):
1. Backend ticket work on T-0405 proceeds per the ratified test contract (TC-REVOKE-TTL-1..5).
2. File: mobile refresh retryable-vs-terminal classification (D4.5 — Android + iOS).
3. File: web-host TTL follow-up, admin host first/separable (D4.3).
4. File: password change/reset revokes other refresh tokens (D4.6).
5. File: mint `exp` through `TimeProvider` (hygiene, enables exact-boundary test — TC-2 note).

This ADR is now **immutable** — supersede, never edit.
