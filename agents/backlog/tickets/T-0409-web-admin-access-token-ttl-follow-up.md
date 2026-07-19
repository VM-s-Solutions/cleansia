---
id: T-0409
title: "Web-admin access-token TTL follow-up (ADR-0024 A3): drop Web.Admin 1440 → 15 min — on web the TTL is the ONLY revocation bound; the admin SPA refresh path absorbs it after one interceptor fix (stale X-CSRF-Token on the 401-replay)"
status: done
size: S
owner: architect
created: 2026-07-17
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: [ADR-0024, ADR-0026, ADR-0027]
layers: [architect, backend, frontend]
security_touching: true
priority: high
manual_steps: []
sprint: 12
source: ADR-0024 D4.3 / amendment A3 (web-host TTL follow-up — admin host first, separable) · architect panel decision 2026-07-17
---

> **What this decides.** ADR-0024 left all three web hosts at `AccessTokenExpMinutes = 1440` as a
> *named follow-up, not an endorsement* (D4.3), and amendment A3 ruled the **admin host is the
> priority and a separable case** — an SPA with no SSR complexity, carrying the highest-privilege
> 24 h JWT in the system. Web sessions are **structurally unreachable** by the ADR-0026/0027
> revocation directories (no web client sends `X-Device-Id` → refresh rows carry `DeviceId = null`;
> the directories are installed on the two mobile hosts only), so on the admin host the access-token
> TTL is the **entire** revocation machinery: admin disable (`IsActive=false`), role demotion,
> password reset, and stolen-cookie kill all bite only at the next refresh. This ticket records the
> panel's decision for the admin host and the implementation sketch. Customer-SSR is explicitly
> **out of scope** (its prerequisite verification is scoped below as a follow-up).

## Decision (panel)

*(Architect defense panel, 2026-07-17 — author → challenger → lead. Every citation verified against
the working tree during this session; none taken from the ADRs on trust.)*

### Author position

**D1 — TTL: `AccessTokenExpMinutes = 15` on `Cleansia.Web.Admin` (both appsettings files).**

Why 15, not 30 (mobile parity) or 60:

- **On web the TTL is the whole bound.** Mobile got ≤ 30 s revocation from the ADR-0026/0027
  directories with the 30-min TTL as mere backstop. The admin host has *neither* directory
  (`AddDeviceRevocationEnforcement`/`AddUserRevocationEnforcement` — mobile hosts only, pinned by
  TC-REVOKE-NOW-7/TC-REVOKE-USER-7) and can never gain the device one (browsers send no
  `X-Device-Id`; null-guard `RefreshTokenService.cs:154-167`). Every revocation event on the
  scariest token rides the full TTL — halving 30 → 15 halves the *only* number that matters.
- **The refresh boundary re-validates everything.** Each rotation re-reads the user and rejects on
  `!user.IsActive` (`RefreshToken.cs:84-89`), re-pins `RequiredProfile = Administrator` +
  `RequiredAudience = JwtAudiences.Admin` server-side (`AdminAuthController.cs:47-52`,
  `RefreshToken.cs:75-80, 91-95`), and re-mints role claims. A 15-min TTL means a disabled or
  demoted admin loses the Admin API within 15 min instead of 24 h — and password reset
  (`RevokeAllForUserAsync` keep-none) cuts an attacker's *renewal* instantly and their outstanding
  admin JWT within 15 min (the ADR-0027 D8 web residue shrinks 96×).
- **ADR-0024's "not 15" arguments are mobile-shaped and do not transfer.** (a) The NAT-herd cliff
  needs ~10 co-located devices refreshing in one 60-s window of the anonymous per-IP `auth` bucket —
  admin headcount is single-digit, and the web chain even has a 429 back-off retry the mobile
  clients lacked (`RetryAfterInterceptorFn`, deliberately ordered after the error snackbar
  interceptor — `libs/core/services/src/lib/interceptors/index.ts:9-15`). (b) The flaky-radio
  transport-conflation risk is a desktop-browser non-issue at admin stakes: worst case is a
  re-login prompt for an admin, not a field worker mid-job.
- **Session UX is untouched.** The access *cookie's* `Expires` is pinned to
  `RefreshTokenExpiresAt`, not the JWT TTL (`AuthCookieWriter.cs:51-53`), and re-login cadence is
  governed by the refresh lifetime (30 d rememberMe / 1 d short, sliding on rotation —
  `RefreshTokenService.cs:27-34, 85-91`) — both unchanged. The only delta is silent 401→refresh
  round trips: ~4 per active hour. No client reads the access TTL anywhere (the JWT lives in an
  HttpOnly cookie the SPA cannot even see; `isLoggedIn`/guards key off the refresh expiry —
  `admin-auth.service.ts:70-92`).
- 15 is also the code default (`JwtSettingsConfig.cs:11`) and the value every test harness already
  runs (`appsettings.HostTests.json:15`, IntegrationTests) — the least-novel number in the system.

**D2 — VERIFY finding: the refresh path absorbs a short TTL for reads, but the mutation-replay
path has a live defect that the TTL flip multiplies ~96×. Fixing it is a hard predecessor.**

The chain (all verified, see Evidence): the admin interceptor order is
`[Common…, AuthInterceptorFn, AdminErrorInterceptorFn, LoadingInterceptorFn]`. `AuthInterceptorFn`
attaches `X-CSRF-Token` on state-changing requests *before* the error interceptor. On a 401 the
error interceptor refreshes (cookie rotates; a **new `jti`** is minted per token —
`AuthExtensions.cs:23` — so the server-derived CSRF value rotates too) and then replays via
`next(req)` — which re-runs only the *downstream* chain, so the replayed mutation carries the
**pre-refresh** CSRF header alongside the **post-refresh** cookie →
`CsrfValidationMiddleware` derives from the new cookie's `jti` → **403 `csrf.header_mismatch`**
(`CsrfValidationMiddleware.cs:72-79`). Not a 401, so no second refresh — the admin's Save fails
with an error and only a manual retry (fresh chain, fresh header) succeeds. This bug is **live in
production today** at the 24 h boundary (`Csrf:Enabled = true` only in
`appsettings.Production.json:24` — dev has it `false`, so local testing can never reproduce it);
a 15-min TTL converts it from a once-a-day oddity into "every form filled for > 15 min fails its
first Save." The fix is one file: re-set the header from `getCsrfToken()` on both replay branches
(the coordinator already transports the fresh token — `complete(getCsrfToken())`,
`waitForRefresh(): Observable<string>` — the replay sites just ignore it:
`error.interceptor.ts:53, 58-61`).

**D3 — Config-only on the backend; one small admin-SPA client fix; nothing else.** No Bicep
(ADR-0024 verified `JwtSettings__Secret` is the only injected JwtSettings surface — the appsettings
edit *is* the production change), no NSwag (no DTO/endpoint change), no cookie change, web
Partner/Customer hosts byte-untouched.

### Challenge

**C1 — "Why 15 and not 30? ADR-0024 argued 30 as the knee; you create a second sanctioned number
and double the frequency of every web-refresh failure mode."** The knee analysis was fleet-shaped
(NAT density, radio flakiness) — conceded — but the challenger's real point stands: boundary
crossings double, and *two* client failure modes scale with them: the D2 CSRF replay and C3's
multi-tab race.

**C2 — "ADR-0024's promise for this follow-up was config-only; you're smuggling a client change
into a TTL flip."**

**C3 — Two admin tabs share one cookie jar AND one localStorage.** Tab A and tab B both hit the
stale boundary within the same sub-second window → both POST `RefreshToken` with the same cookie →
the loser either (a) presents the just-rotated token → **rotation-reuse theft cascade revokes the
whole chain** (`RefreshTokenService.cs:58-73` — no reuse grace window, verified) → both tabs dead,
or (b) loses the xmin race (`CommitRotationAsync`, `RefreshTokenService.cs:109-127`) →
`InvalidRefreshToken` → `forceLogout` → `removeSession()` wipes the **shared** localStorage
(`admin-auth.service.ts:128-135`), so tab A's next request sees `hasValidRefreshToken() == false`
and logs out too. Either branch: a boundary race in a multi-tab session logs the admin out
everywhere. At 1440 this is nearly unreachable; at 15 it is 4×/hour of exposure windows. Why is
this not blocking?

**C4 — The web refresh error path has the exact conflation ADR-0024 D4.5 fixed on Android:
`catchError((refreshError) => { fail(); forceLogout(); })` treats a transport error or a 429 on the
refresh call as terminal (`error.interceptor.ts:63-67`). The mobile lesson was ratified — why ship
a TTL flip that multiplies its exercise rate on web without the classification?** Also:
`waitForRefresh()` never emits on `fail()` (`refresh-coordinator.ts:24-34` — `filter(non-null)`),
so queued concurrent requests **hang forever** on a failed refresh rather than erroring.

**C5 — Scope: the partner app is also a plain SPA — why not fold it in? And doesn't T-0400 gate
this?** Sharpened by a verified find: `cleansia-admin.app/src/environments/environment.prod.ts:6`
sends auth to `authApiBaseUrl: 'https://api.cleansia.cz'` — the **PARTNER** API host — while data
calls target `api-admin.cleansia.cz`. If that pairing stands at prod cut-over, the admin SPA's
tokens would be minted by `Web.Partner` (still 1440) and this flip on `Web.Admin` would be
**silently ineffective** for the deployed admin app.

### Defense

- **C1 — REBUT on the number, CONCEDE the scaling.** The mobile knee arguments verifiably do not
  transfer (single-digit admin population vs the ~10-device 60-s cliff; desktop network; a 429
  back-off retry already in the web chain). What 15 buys is a 2× cut of the *only* revocation bound
  on the highest-privilege token; what it costs is 2× exposure to two client defects — of which D2
  is a **hard predecessor fix** (frequency then irrelevant) and C3 is bounded below. The number is
  argued from the threat, not from parity: on mobile the TTL is a *backstop* behind ≤ 30 s
  directories; on admin-web it is the *primary control*. Different role → different number is
  correct, not inconsistent. 15 stands.
- **C2 — REBUT.** The ticket's own question is "whether ANY client change is needed," and the
  honest answer from reading the code is yes — because the replay path is **broken today in
  production** at the 24 h boundary. That is a bugfix this flip's verification uncovered, not scope
  creep; shipping the flip without it would be knowingly multiplying a known defect. It is S-sized
  (one file + one unit test) and rides the same ticket as a blocking AC.
- **C3 — CONCEDE the analysis, REBUT blocking.** Bounded: the admin app has **zero polling**
  (verified — no `interval(`/`timer(`/`setInterval` in `cleansia-admin-features`), so a background
  tab issues no requests and the race needs *near-simultaneous human-driven requests in two tabs*
  inside a ~sub-second window, 4×/hour of *windows*, not of events. Recovery is a re-login, and the
  theft-cascade branch is itself a security feature firing as designed. Recorded as an accepted
  residue with a **pre-analyzed fix** — cross-tab single-flight refresh via the Web Locks API
  (`navigator.locks.request('cleansia-admin-refresh', …)` around `refreshSession()`; localStorage
  already broadcasts the fresh CSRF) — and a **trigger**: recurring admin forced-logout reports, or
  the first `RefreshTokenReused` warning for an Administrator in prod logs
  (`RefreshTokenService.cs:67-69` logs it).
- **C4 — CONCEDE, fold as a SHOULD in the client fix, non-blocking.** Same disposition ADR-0024
  gave mobile (D4.5: filed, triggered, non-blocking): desktop network + admin re-login cost + the
  existing `RetryAfterInterceptorFn` 429 back-off make the conflation tolerable at admin scale.
  The `waitForRefresh` hang is folded into the same interceptor fix (emit an error to waiters on
  `fail()` so queued requests fail fast instead of dangling).
- **C5 — REBUT on scope, CONCEDE the pairing flag.** Partner-web is a lower-privilege token and its
  own decision; it rides this ticket's mechanics *after* the admin flip proves out in prod (one
  decision per change — the exact separability A3 ruled). T-0400 does **not** gate this: the TTL is
  host-side and orthogonal to cookie-domain topology. But the `authApiBaseUrl` mis-pairing is real
  and verified — folded in as a **pre-flight check** (AC5): the flip's prod effectiveness requires
  admin auth to terminate on `Web.Admin` (already flagged as a T-0400 AC1 ratification item; this
  ticket makes it an explicit dependency of *effectiveness*, not of merge).

### Lead verdict

| Challenge | Ruling | Disposition |
|---|---|---|
| C1 — 15 vs 30 | **RESOLVED — defended** | The TTL's *role* differs per host class: backstop on mobile (directories carry the bound), primary control on admin-web. 15 stands; the scaling concern is answered by making D2 blocking and C3 a bounded, triggered residue. |
| C2 — client change in a "config-only" follow-up | **RESOLVED — defended** | The CSRF-replay defect is live in prod today; the flip's verification found it. Fixing a discovered defect before multiplying it is the opposite of scope creep. Blocking AC. |
| C3 — multi-tab boundary race | **RESOLVED — conceded, residue** | Accepted at current admin usage (no polling, human-simultaneity required, re-login recovery). Web Locks single-flight is the pre-analyzed fix; trigger: recurring reports or first Administrator `RefreshTokenReused` in prod telemetry. |
| C4 — refresh transport/429 conflation + waiter hang | **RESOLVED — conceded, SHOULD** | Folded into the same interceptor change (waiter-hang fix; retryable-vs-terminal classification), non-blocking for the flip — mirrors ADR-0024 D4.5's ruling. |
| C5 — partner scope / T-0400 gating / prod pairing | **RESOLVED — defended with concession** | Partner-web is the named next case, not folded. T-0400 does not gate. The `authApiBaseUrl → api.cleansia.cz` mis-pairing becomes AC5 (effectiveness pre-flight). |

**Consensus: zero blocking challenges remain.** Ruling: **`Web.Admin` drops to 15 min**, with the
admin-SPA replay-CSRF fix as a **hard predecessor in the same ticket**. The decision is formalized
as **ADR-0030** at implementation kickoff (AC1) — required because moving the `Web.Admin` line of
the TC-REVOKE-TTL-4 pin is sanctioned *only* by a superseding ADR (ADR-0024 verdict;
`security-rules.md` S2 token-lifetime paragraph). Customer-SSR and partner-web TTLs remain at 1440
and remain pinned.

## Verified evidence (file:line, this session)

**Backend issuance / refresh (can absorb a short TTL — yes):**
- TTL config: `src/Cleansia.Web.Admin/appsettings.json:19` and
  `src/Cleansia.Web.Admin/appsettings.Production.json:19` — both `1440`. Mint sites read it at
  `TokenService.cs:82` and `RefreshToken.cs:145` (now `TimeProvider`-clocked, T-0410).
- Admin refresh endpoint: `AdminAuthController.cs:40-55` — `[AllowAnonymous]` +
  controller-level `[EnableRateLimiting("auth")]`, token enriched from the HttpOnly cookie,
  `RequiredProfile`/`RequiredAudience` pinned server-side (`[JsonIgnore]`, `RefreshToken.cs:37-44`).
- Rotation is fail-closed against racing revokes (xmin, `RefreshTokenService.cs:109-127`) and
  re-checks `user.IsActive` + profile per rotation (`RefreshToken.cs:84-95`).
- Cookie carrier: `AuthCookieWriter.cs:51-53` — access cookie `Expires` =
  `RefreshTokenExpiresAt`, so the JWT TTL does not shorten cookie persistence.
- CSRF: opt-out covers `/api/AdminAuth/` (`Web.Admin/Startup.cs:18-22`) so the refresh POST itself
  never needs the header; enabled in prod only (`appsettings.Production.json:23-25`).

**Admin SPA refresh flow (the defect + the machinery):**
- Chain order: `app.config.ts:98` — `[...COMMON_INTERCEPTORS_FN, ...ADMIN_INTERCEPTORS_FN]`;
  `admin-services/.../interceptors/index.ts:9` — `[AuthInterceptorFn, AdminErrorInterceptorFn,
  LoadingInterceptorFn]`. CSRF header attach: `auth.interceptor.ts:18-23` (upstream of the replay).
- Single-flight 401→refresh→replay: `error.interceptor.ts:16-69`; replay sites ignore the fresh
  CSRF the coordinator hands them (`:53`, `:58-61`); refresh-terminal → `forceLogout` (`:63-67`).
- `jti` per token (CSRF pair rotates every refresh): `AuthExtensions.cs:23`; server derivation
  prefers `jti`: `CsrfTokenService.cs:57-63`; mismatch → 403 `csrf.header_mismatch`:
  `CsrfValidationMiddleware.cs:72-79`.
- Client state keys off refresh expiry, never access expiry: `admin-auth.service.ts:70-92` — no
  client anywhere reads the access TTL (HttpOnly cookie), so **the TTL value itself needs zero
  client contract change**; the one client change is the D2 defect fix.
- No polling in admin features (multi-tab race bound): zero `interval(`/`timer(`/`setInterval`
  matches under `libs/cleansia-admin-features`.
- Pin test to move: `src/Cleansia.Tests/Configuration/AccessTokenTtlConfigPinTests.cs:23`
  (`InlineData("Cleansia.Web.Admin", 1440d)`).
- Prod pairing flag: `apps/cleansia-admin.app/src/environments/environment.prod.ts:5-6`
  (`apiBaseUrl: api-admin.cleansia.cz`, `authApiBaseUrl: api.cleansia.cz` — the partner API).

## Implementation sketch

**1. Client predecessor (frontend lane, MUST land before or with the flip):**
`src/Cleansia.App/libs/core/admin-services/src/lib/interceptors/error.interceptor.ts` — on both
replay branches, re-set the CSRF header from the post-refresh value before `next(...)`:

```ts
// initiator branch
switchMap(() => next(withFreshCsrf(req, authService))),
// waiter branch
return coordinator.waitForRefresh().pipe(switchMap(() => next(withFreshCsrf(req, authService))));

function withFreshCsrf(req: HttpRequest<unknown>, auth: AdminAuthService): HttpRequest<unknown> {
  const token = auth.getCsrfToken();
  return token && req.headers.has('X-CSRF-Token')
    ? req.clone({ headers: req.headers.set('X-CSRF-Token', token) })
    : req;
}
```

SHOULD (same file/lane, non-blocking): `AdminRefreshCoordinator.fail()` notifies waiters with an
error (so queued requests fail fast instead of hanging on the never-emitting `filter`), and
classify refresh transport/429 as retryable-not-terminal (the ADR-0024 D4.5 mirror).

**2. Backend config flip (backend lane, exactly two files + one test line):**
- `src/Cleansia.Web.Admin/appsettings.json` line 19: `"AccessTokenExpMinutes": 1440` → `15`
- `src/Cleansia.Web.Admin/appsettings.Production.json` line 19: same → `15`
- `src/Cleansia.Tests/Configuration/AccessTokenTtlConfigPinTests.cs:23`:
  `InlineData("Cleansia.Web.Admin", 1440d)` → `15d` (sanctioned by ADR-0030 only). Web
  Partner/Customer lines stay `1440d`; mobile lines stay `30d`.
- No other change: no Bicep, no NSwag, no code in `TokenService.cs`/`RefreshToken.cs`/any
  `ServiceExtensions.cs` (ClockSkew stays `Zero`), mobile hosts untouched.

**3. Test contract:**
- **TC-ADMIN-TTL-1 (pin).** The updated raw-file pin above: Web.Admin `15` in both files; the
  other four hosts' values unchanged. (Mechanism: existing `AccessTokenTtlConfigPinTests`.)
- **TC-ADMIN-TTL-2 (expiry end-to-end).** HostTests on the ADMIN host, the
  `MobileAccessTokenTtlExpiryTests` fractional-TTL mechanism (`JwtSettings:AccessTokenExpMinutes
  = 0.05` layered last): login → authed call 200 → wait past expiry → 401; then POST
  `AdminAuth/RefreshToken` with the cookie → 200 + rotated cookies → replayed call 200. Pins
  ClockSkew=Zero on the admin host and the cookie refresh path hot.
- **TC-ADMIN-TTL-3 (the D2 fix).** Jest, `HttpTestingController`: mutation POST → 401 → refresh
  responds (new CSRF written to localStorage) → assert the replayed POST carries the **new**
  `X-CSRF-Token`; cover both the initiator and the queued-waiter branch. This is the regression
  pin for the predecessor fix.
- **TC-ADMIN-TTL-4 (existing, stays green unedited).** `RefreshTokenProfileGateHandlerTests`
  (profile/audience pin), `CsrfSessionKeyRotationTests`, TC-REVOKE-TTL-1..5, TC-REVOKE-NOW-*,
  TC-REVOKE-USER-* — this change must not touch any of them.

**4. Acceptance criteria:**
- [ ] **AC1** — ADR-0030 authored + accepted (supersedes ADR-0024's web-1440 pin **for the admin
  host only**; records this panel's trail), and the `security-rules.md` S2 token-lifetime
  paragraph gains the admin line at acceptance: *"the Web.Admin TTL (15 min) is the web
  revocation bound — no directory reaches cookie sessions; changing it requires a superseding
  ADR."* Living doc `agents/architecture/decisions/auth-sessions.md` updated in the same change.
- [ ] **AC2** — the client replay-CSRF fix lands with TC-ADMIN-TTL-3 (blocking predecessor).
- [ ] **AC3** — the two-file config flip + pin-test move; TC-ADMIN-TTL-1/2 green; `dotnet test`
  + admin Jest green.
- [ ] **AC4** — residues recorded in the ADR: multi-tab boundary race (trigger + Web Locks fix
  named), refresh transport/429 conflation on web (trigger: first admin-host `auth` 429 or
  spurious-logout report), customer-SSR + partner-web hosts still 1440 and still pinned.
- [ ] **AC5 (effectiveness pre-flight)** — verify admin prod auth terminates on `Web.Admin`
  before cut-over: `environment.prod.ts:6` `authApiBaseUrl` currently targets `api.cleansia.cz`
  (partner). Ratify/fix the pairing under T-0400 AC1 — until then the prod flip is inert for the
  deployed admin app (dev/devremote proxies already pair correctly with :5001).

## Customer-SSR prerequisite verification (scoped follow-up — NOT this ticket)

The customer host's TTL decision stays gated (ADR-0024 D4.3) on verifying, in order:
1. **SSR refresh reachability** — whether the customer app's server-side render can ever trigger
   the 401→refresh path (interceptor registration in `app.config.server.ts` vs browser config;
   whether SSR outbound calls forward the incoming `Cookie` header).
2. **Set-Cookie propagation** — if an SSR-side refresh CAN fire: whether the rotated cookies from
   the API response propagate to the *browser's* response. A swallowed `Set-Cookie` orphans the
   browser's now-rotated refresh token → its next refresh presents a rotated token → the
   rotation-reuse **theft cascade revokes the legitimate user's chain** (`RefreshTokenService.cs:58-73`).
   This is the exact "mis-propagated Set-Cookie" hazard ADR-0024 named; it must be proven
   impossible (SSR never refreshes) or correct (propagation verified hot) before any customer flip.
3. **Same replay-CSRF audit** — the customer/partner error interceptors share the admin shape
   (stale-header replay); audit + fix ride whichever ticket flips their TTL.
4. **T-0400 topology** — the customer flip waits for the custom-domain cut-over plan since SSR
   origin/proxy topology changes what path refresh takes.

PM: file this as its own architect-verification ticket when the web follow-up resumes;
partner-web (plain SPA) can ride the admin mechanics once this flip proves out in prod.

## Notes
- The number 15 is admin-host-specific. It does NOT revisit mobile's 30 (ADR-0024 D1 stands) and
  sets no precedent for customer/partner web — each gets its own argued number when unblocked.
- Deploy is the normal artifact roll; the TTL ships from `appsettings.Production.json`
  (Bicep injects only `JwtSettings__Secret` — ADR-0024 A5).
- The D2 defect is invisible in local dev (`Csrf:Enabled=false` in `appsettings.json:24`) — do not
  "verify" the fix against a dev backend; TC-ADMIN-TTL-3 is the proof, and TC-ADMIN-TTL-2 runs
  with CSRF as configured in HostTests.

## Status log
- 2026-07-17 — filed `proposed` with the panel decision (author → challenger C1–C5 → lead verdict;
  consensus, zero blocking). Decision: Web.Admin 1440 → **15 min**; admin-SPA replay-CSRF fix is a
  blocking predecessor (defect verified live in prod at the 24 h boundary); multi-tab race +
  refresh-conflation recorded as triggered residues; customer-SSR verification scoped as follow-up;
  `environment.prod.ts` auth pairing flagged as AC5. ADR-0030 to be authored at implementation
  kickoff (AC1) — it, not this ticket, moves the TC-REVOKE-TTL-4 pin.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — code shipped on feature/i18n-cluster-3 (merged): Web.Admin TTL 15 min + the CSRF replay-restamp fix; PROD cut-over remains gated on T-0400 custom domains (owner).
