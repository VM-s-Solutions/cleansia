---
id: T-0272
title: Shrink the auth wire contract — trustedDeviceToken mobile-only + drop RefreshToken server-only fields
status: ready
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

## Review
<!-- reviewer / security / qa write verdicts here; PM reconciles before advancing state -->
