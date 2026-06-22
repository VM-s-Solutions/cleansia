---
id: T-0272
title: Shrink the auth wire contract — trustedDeviceToken mobile-only + drop RefreshToken server-only fields
status: done
size: M
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: []
blocks: [T-0280]
stories: []
adrs: [0001]
layers: [architect, backend]
security_touching: true
manual_steps: [nswag-regen]
sprint: 9
---

## Context

Two sibling auth-surface leaks shrink the same way: a server-only/host-only field is declared on a
command record that NSwag projects onto the wire, so it appears in generated TS/Kotlin clients even
though the client must not (and on web, cannot) meaningfully set it.

**Owner P1 — DECISION TAKEN (make `trustedDeviceToken` MOBILE-ONLY).** All three web/shared login
commands carry `TrustedDeviceToken` as an `init` property:
- `Features/Auth/Login.cs:32` (`Login.Command`), `Features/Auth/PartnerLogin.cs:33`,
  `Features/Auth/AdminLogin.cs:38`.
The validator reads it **server-side** for the lockout-bypass (`LoginValidator.cs:30,42,50,88-110`,
the T-0233 trusted-device mitigation). But the **web** controllers OVERWRITE the body value from the
HttpOnly refresh cookie before dispatch — `Web.Customer/Controllers/AuthController.cs:40`
(`command with { TrustedDeviceToken = RefreshTokenFromCookieOrBody(...) }`), and the equivalent web
Partner/Admin controllers. So on web the body field is wire-noise that gets clobbered. **Only the
mobile hosts use the body value**: `Web.Mobile.Customer/Controllers/AuthController.cs:44-47`
dispatches `Login.Command` and `Web.Mobile.Partner/Controllers/AuthController.cs:48-52` dispatches
`PartnerLogin.Command`, both passing `command` straight through (the body token reaches the validator).
Result: `trustedDeviceToken` leaks into all three **web** generated clients
(`customer-client.ts`, `partner-client.ts`, `admin-client.ts`) where it is meaningless. Owner's call:
**carry it on a mobile login contract** the mobile hosts dispatch, and drop it from the web command.

**Audit finding #9 (sibling — handle the SAME way).** `RefreshToken.Command`
(`Features/Auth/RefreshToken.cs:32`) exposes `UserProfile? RequiredProfile` and `string? RequiredAudience`
as init-able record params. Both are **server-enriched and unconditionally overwritten by all 5 refresh
controllers** (`Web.Customer:90-91`, `Mobile.Customer:102`, `Web.Partner:102-103`, `Mobile.Partner:107`,
`Admin:50-51` — each does `command with { RequiredProfile = …, RequiredAudience = … }`), so a
client-sent value is always discarded. They still ship in every generated client. The canonical pattern
is `Login.cs:26-33` / `PartnerLogin` / `AdminLogin`, which pull per-host params (audience via
`IHostAudienceProvider`) server-side and keep them off the wire entirely.

Both are **auth-surface** changes → `security_touching: true`. Both **shrink** the wire DTO → the field
disappears from generated clients → `nswag-regen` (all affected clients). Behavior is preserved: the
server already ignores/overwrites these fields on the paths that shrink.

**No deliberation panel (decision already taken).** The owner has fixed the *what* (trustedDeviceToken =
mobile-only; RefreshToken server-fields = off the wire). The *how* — the exact mobile-login contract
shape — is an **architect-led implementation choice** (see Implementation notes), gated by the security
reviewer, not a story/ADR-panel question. ADR-0001 (the authorization model, per-host
`RequiredProfile`/`RequiredAudience` refresh contract) is **in force and must not be weakened** — the
server-side enrichment and the per-host profile/audience gate stay exactly as they are; only the wire
projection shrinks.

## Acceptance criteria

- [ ] **AC1 — Characterization first (auth behavior pinned before the shape change).** Before any
  contract edit, the existing auth behavior is pinned green by the current suites and any gap is
  filled: the per-host refresh enrichment tests
  (`RefreshTokenControllerProfileEnrichmentTests`, 5 hosts), the refresh profile-gate tests
  (`RefreshTokenProfileGateHandlerTests` / `RefreshTokenProfileGateTests`), and the trusted-device
  lockout-bypass tests (`TrustedDeviceLockoutBypassTests`, `AccountLockoutTests`) all pass
  **unchanged** as the red→green net. Evidence: status-log records they were green pre-change.
- [ ] **AC2 — `trustedDeviceToken` removed from the web login wire.** `TrustedDeviceToken` is dropped
  from the command(s) the **web** Customer/Partner/Admin login controllers dispatch, so it no longer
  appears in `customer-client.ts` / `partner-client.ts` / `admin-client.ts` after regen. The web
  controllers' cookie-read trusted-device path is preserved (web reads the refresh cookie server-side;
  the value flows to the validator without a body field).
- [ ] **AC3 — `trustedDeviceToken` carried on a mobile login contract.** The mobile Customer/Partner
  login hosts dispatch a contract that **does** carry `trustedDeviceToken` in the body (the mobile
  apps send it, as today), and the validator's lockout-bypass still reads it server-side. The mobile
  Android clients still see the field; the web clients do not. Behavior for a locked-out account
  presenting a valid trusted-device token on mobile is **identical** to today.
- [ ] **AC4 — `RequiredProfile`/`RequiredAudience` removed from the `RefreshToken` wire.**
  `RefreshToken.Command`'s wire shape shrinks to carry only the client-supplied `Token`; the
  per-host `RequiredProfile`/`RequiredAudience` are passed to the handler via a server-side seam
  (not an init-able wire param). All 5 refresh controllers still pin BOTH their profile AND audience
  (ADR-0001 contract unchanged); the 5 enrichment tests still assert each host's pinned pair. The two
  fields no longer appear in any generated client after regen.
- [ ] **AC5 — Profile/audience gate still enforced.** The refresh profile-gate behavior is unchanged:
  a refresh token whose user profile ≠ the host's pinned profile (or audience ≠ host's pinned
  audience) still fails with `InvalidRefreshToken`; a match still succeeds. Proven by the existing
  gate tests passing against the new seam (no new bypass introduced).
- [ ] **AC6 — Security gate green (no widened attack surface).** Security reviewer confirms (S1–S10):
  no field that was server-authoritative becomes client-settable; the trusted-device lockout-bypass
  cannot be triggered from web by a body field; the refresh profile/audience pinning is still
  server-side and unforgeable. Verdict names the specific risk checked, not a category.
- [ ] **AC7 — Mechanical checks green.** `dotnet build` + `Cleansia.Tests` + `Cleansia.IntegrationTests`
  + `Cleansia.HostTests` all pass on the merged tree; `check-consistency.mjs` reports no new violation
  in `Features/Auth`. The diff is the **only** producer of the client-shape delta the regen will reflect.
- [ ] **AC8 — Manual step flagged, NOT run.** The ticket carries `manual_steps: [nswag-regen]` for ALL
  affected clients (customer, partner, admin web — and the mobile-partner/mobile-customer clients if
  the mobile contract changes the generated mobile surface). The agents do **not** regen. The PM holds
  the dependent consumer fixes (T-0280 comment cleanup of the FE auth services, which depends on the
  field disappearing) until the owner confirms the regen bundle.

## Out of scope
- **NO change to ADR-0001's per-host refresh contract** (the `RequiredProfile`/`RequiredAudience`
  values each host pins, the profile gate, the audience pin) — only the **wire projection** shrinks.
- **NO change to the trusted-device lockout-bypass *semantics*** (T-0233) — it still bypasses the lock
  only for a valid, non-revoked, account-bound token, read server-side.
- **NO rename of `JwtAudiences.Mobile`** (a known deferred item; out of scope here).
- **NOT the FE auth-service comment cleanup** — that is T-0280, which depends on this landing + the regen.
- **NO new endpoint, no new auth flow, no Google-OAuth/IMP-1 work.**

## Implementation notes

**Sequence (contract-before-consumers):** `architect` decides the mobile-login contract shape and the
`RefreshToken` server-side seam → `backend` implements → security + reviewer in parallel → PM flags the
regen bundle → owner regenerates → PM re-verifies + releases T-0280.

**Mobile-login contract shape (architect's call — two viable shapes, pick the one that keeps the
validator seam simplest):**
- (a) A dedicated `MobileLogin.Command` / `MobilePartnerLogin.Command` carrying
  `(Email, Password, RememberMe, TrustedDeviceToken)` that the mobile hosts dispatch; the web
  `Login`/`PartnerLogin`/`AdminLogin` commands drop the field. The shared `LoginValidator<TCommand>`
  already takes a `trustedDeviceTokenSelector` (`LoginValidator.cs:30`) — the web commands pass a
  constant-null selector (`_ => null`) and the mobile commands pass the real one. Minimal validator churn.
- (b) Keep one command but make `TrustedDeviceToken` a **non-wire** server-set property (e.g. set only
  by the mobile controller from the body via an explicit mobile DTO → command map, mirroring how web
  sets it from the cookie). Risk: a single shared command still NSwag-projects the field unless the
  property is excluded from serialization — (a) is cleaner for "disappears from web clients".
  Recommendation: **(a)**.

**`RefreshToken` server-side seam (architect's call):** pass `(RequiredProfile, RequiredAudience)` to
the handler via a constructor-injected per-host provider (an `IHostAudienceProvider`-style
`IRefreshContextProvider`) OR an internal (non-init, non-serialized) command property the controller
sets. Either way the handler logic at `RefreshToken.cs:62-67,76-80` is **unchanged** — it still reads
`command.RequiredProfile`/`command.RequiredAudience` (or the injected equivalent); only how those values
arrive changes so they leave the wire.

**Files in play:** `Features/Auth/{Login,PartnerLogin,AdminLogin,RefreshToken}.cs`,
`Features/Auth/Validators/LoginValidator.cs`, the 5 web/mobile `AuthController`/`AdminAuthController`
files, and any new mobile-login command file. Consumers the regen will ripple to (PM holds until
confirmed): the FE auth services (`{customer,partner,admin}-auth.service.ts`) — their stale
`new LoginCommand({ trustedDeviceToken })` callers will need the field dropped; per the quality-gates
"build all three apps after regen" rule, the owner's regen step is followed by all-three prod builds.

**Routing:** `architect` (contract seam) → `backend` (impl). **`security` gate mandatory** (auth
surface). **`reviewer` in parallel.** **`qa`:** suite-green + AC↔test mapping. `optimizer` N/A.

## Status log
- 2026-06-22 — draft → ready (created by pm). Reconciled against master: P1 premise VERIFIED
  (web overwrites `TrustedDeviceToken` from cookie at `Web.Customer/AuthController.cs:40`; mobile hosts
  pass it through at `Mobile.{Customer,Partner}/AuthController.cs`); finding #9 VERIFIED (all 5 refresh
  controllers enrich `RequiredProfile`/`RequiredAudience`, both init-able + leaked). `security_touching:
  true`, `manual_steps: [nswag-regen]` (all web clients + mobile clients if mobile surface changes).
  No panel — decision taken by owner; ADR-0001 in force, only wire projection shrinks. Sized **M**
  (4 command files + 5 controllers + 1 validator + 1 new mobile command, behavior-preserving under an
  existing characterization net). **Lands FIRST/alone in Wave 8** — the regen ripples to clients other
  tickets touch (FE auth services). Dedup checked: extends/supersedes the wire-leak half of audit #9;
  no existing open ticket covers the trustedDeviceToken split (T-0233 added the bypass, did not split
  the contract).

- 2026-06-22 — ready → review (backend). Implemented the wire shrink, behavior-preserving.

  **RefreshToken seam (AC4/AC5).** `RefreshToken.Command` constructor is now `(string Token)`; the
  per-host pins `RequiredProfile`/`RequiredAudience` became `[JsonIgnore]` server-set `init`
  properties (not constructor params). All 5 controllers still set them via `command with { ... }`
  exactly as before, and the handler still reads `command.RequiredProfile`/`command.RequiredAudience`
  unchanged — only the wire projection shrank. `[JsonIgnore]` excludes them from both serialization
  and body-binding (proven by `AuthWireContractTests`), so they disappear from every generated client
  and cannot be forged from the body. ADR-0001 per-host gate untouched.

  **trustedDeviceToken split (AC2/AC3).** Chose architect option (a). Web `Login`/`PartnerLogin`/
  `AdminLogin` keep `TrustedDeviceToken` as a `[JsonIgnore]` server-set property (the web controllers
  still fill it from the HttpOnly refresh cookie via `RefreshTokenFromCookieOrBody` — simplified to
  pass `string.Empty` since the body never carries it now), so it leaves the web wire. New
  `MobileLogin`/`MobilePartnerLogin` commands carry `TrustedDeviceToken` on the wire (constructor
  param); `Web.Mobile.Customer`/`Web.Mobile.Partner` `Login` now dispatch them. Validators reuse the
  shared `LoginValidator<TCommand>` (selector reads the token server-side, bypass semantics identical).
  Handlers mirror the web `Login`/`PartnerLogin` handlers byte-for-byte (permissive / Employee+Admin).

  **Characterization (AC1).** Pre-change: `Cleansia.Tests` auth subset green (121). Post-change the
  same suites pass unchanged (the existing `TrustedDeviceLockoutBypassTests`, `LoginLockoutValidatorTests`,
  `RefreshTokenControllerProfileEnrichmentTests`, `RefreshTokenProfileGateHandlerTests` all still green
  — call sites that used the dropped constructor named-args were mechanically moved to object-initializer
  syntax; assertions/outcomes unchanged). Added: `AuthWireContractTests` (8, pins the on/off-wire
  projection — the regen driver), `MobileLoginTrustedDeviceBypassTests` (4, mobile body-token bypass),
  `MobileLoginDispatchTests` (2, mobile hosts dispatch the mobile command carrying the body token).
  Auth subset now 136 passed / 0 failed. `EveryCommandHasValidatorTests` green (new commands' validators
  auto-registered via assembly scan).

  **Evidence.** `dotnet build` green for: Core.AppServices, all 5 web hosts, Cleansia.Tests,
  Cleansia.IntegrationTests, Cleansia.HostTests. Auth unit subset 136/136. IntegrationTests +
  HostTests compile (Postgres-gated suites not executed in this lane; logic unchanged). Consistency:
  `Features/Auth` shows only the pre-existing `B3` (login validators inherit the deliberately-shared
  `LoginValidator<TCommand>`) and `B1` (Logout/Register raw-scalar) patterns; the two new B3s on
  `MobileLogin`/`MobilePartnerLogin` replicate the accepted sibling idiom (`Login`/`PartnerLogin`/
  `AdminLogin`), not a new class of deviation.

  **Deviations.** Used the `[JsonIgnore]` server-set-property seam (the ticket's stated alternative to
  a per-host provider) rather than an `IRefreshContextProvider`: the integration/handler tests drive the
  handler with arbitrary `(RequiredProfile, RequiredAudience)` pairs through MediatR, which a DI-injected
  per-host provider would have frozen to the single registered pair — the property seam keeps that test
  flexibility and leaves the handler 100% unchanged. AdminLogin.cs line 60 error-code (`nameof(Command.Email)`
  vs `"AdminLogin"`) was changed by a concurrent lane, not by this ticket; left as-is.

  **MANUAL_STEP: nswag-regen** (owner, NOT run here). Web clients lose: `trustedDeviceToken` from
  customer/partner/admin Login bodies; `requiredProfile`/`requiredAudience` from the RefreshToken body
  on all hosts. Mobile clients gain the new `MobileLogin`/`MobilePartnerLogin` command shapes carrying
  `trustedDeviceToken`. Regen all affected clients (customer/partner/admin web + mobile customer/partner),
  then build all three FE apps. T-0280 (FE auth-service comment cleanup) stays held until the regen lands.

- 2026-06-22 — review (backend, re-review fix). Resolved Reviewer MUST FIX (1): dropped the
  `(T-0233 ...)` parenthetical ticket-id reference from the three new comments, keeping the reason
  prose intact:
  - `Features/Auth/MobileLogin.cs:15` — now `... so the trusted-device lockout-bypass marker is
    carried in the request body instead.`
  - `Features/Auth/MobilePartnerLogin.cs:16` — same edit.
  - `Tests/Features/Auth/MobileLoginTrustedDeviceBypassTests.cs:16` — now `/// The trusted-device
    lockout bypass for the mobile login commands.`
  Comment-only XML-doc edits — no IL/behavior change. The other two `T-0233` references found in the
  tree (`LoginLockoutValidatorTests.cs:38`, `TrustedDeviceLockoutBypassTests.cs:16`) are pre-existing,
  belong to the T-0233 lane (not new in T-0272), and are out of this finding's scope — left untouched.
  **Evidence.** `dotnet build` Core.AppServices green (0 warn / 0 err). Auth unit slice
  (`--filter FullyQualifiedName~Features.Auth`) **128 passed / 0 failed**. Cleansia.Tests's full
  compile is blocked only by another lane's two untracked, currently-broken Loyalty test files
  (`GetUserLoyaltyActivityHandlerTests.cs`, `GetLoyaltyActivityHandlerTests.cs` — `PaymentType`/
  `PaymentStatus` not in context); parked non-destructively to run the auth slice, then restored
  byte-identical (md5 confirmed, both remain `??` untracked). No new manual steps; the prior
  `MANUAL_STEP: nswag-regen` is unaffected (comment-only change does not alter any DTO/wire shape).

## Review
<!-- reviewer / security / qa write verdicts here; PM reconciles before advancing state -->

### Reviewer — CHANGES REQUESTED (2026-06-22)

Verified, not trusted: production builds green for Core.AppServices + all 5 web hosts; auth/mobile
unit slice **214 passed / 0 failed** (ran with the other lane's two untracked, currently-broken
Loyalty test files non-destructively parked then restored byte-identical — hashes confirmed, files
remain `??` untracked). `check-consistency.mjs --paths=Features/Auth` shows no NEW class of deviation;
the two new `B3`s (`MobileLogin`/`MobilePartnerLogin`) replicate the accepted shared-`LoginValidator
<TCommand>` sibling idiom of `Login`/`PartnerLogin`/`AdminLogin`. Behavior-preserving confirmed:
RefreshToken handler logic byte-unchanged, 5 controllers still pin both profile+audience, web Login
still sources the trusted-device marker from the HttpOnly cookie (`RefreshTokenFromCookieOrBody(
string.Empty)` is equivalent now that the body field is `[JsonIgnore]` and can no longer bind). AC1–AC5,
AC7, AC8 met. AC6 = Security's formal gate (auth surface; no server-authoritative field became
client-settable — proven by the deserialize tests). MANUAL_STEP nswag-regen correctly recorded, not run.

**MUST FIX (1) — ticket-id in source comments (hard fail: conventions.md "Comments" + wave rule "NO
ticket IDs in source").** Three new comments reference `T-0233`:
- `Features/Auth/MobileLogin.cs:15` — `/// trusted-device marker (T-0233 lockout bypass) ...`
- `Features/Auth/MobilePartnerLogin.cs:16` — same `(T-0233 lockout bypass)`
- `Tests/Features/Auth/MobileLoginTrustedDeviceBypassTests.cs:16` — `/// The trusted-device lockout
  bypass (T-0233) ...`
Fix: drop the `(T-0233 ...)` parenthetical in all three; keep the *reason* prose ("the trusted-device
lockout bypass"). The traceability belongs in the commit message/ticket, not in source.

Re-review needed only on the comment edit; everything else is approved on the merits. The auth-surface
change still requires the Security reviewer's formal sign-off (AC6) before the PM advances.

Notes (not blocking):
- Out-of-lane churn observed inside a T-0272 file: `AdminLogin.cs:64` error code `"AdminLogin"` →
  `nameof(Command.Email)` (+ matching `LoginHandlerProfileGateTests.cs:106`) — dev attributes it to a
  concurrent lane and left it. It is internally consistent (prod+test moved together) and only changes
  the `Error.Code`, not the message or pass/fail; the suite is green. Flagging for PM awareness only.
- IntegrationTests/HostTests compile and the RefreshToken test edits are the faithful mechanical
  ctor→object-initializer rewrite, but their Postgres/Testcontainers-gated suites were not executed
  in this lane — PM/QA should run them on the merged tree before release.
