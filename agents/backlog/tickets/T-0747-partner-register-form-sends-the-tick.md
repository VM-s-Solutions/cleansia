---
id: T-0747
title: The partner register form sends the tick instead of parking it (A8) — step 1 backend, step 2 partner web
status: done
size: S
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: []
blocks: []
stories: []
adrs: [ADR-0062, ADR-0041]
layers: [backend, frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-14 (A8): *"Retire it now."* The partner web parked the employee tick in
localStorage (`cleansia_signup_consent`, `SignupConsentService`) and delivered it after the first
sign-in through `POST api/Gdpr/consents` (`GrantConsent` stores an employee row with
`DocumentVersion = null`).

**CORRECTED 2026-09-14 (review) — re-sequenced; the web lane could not do this alone.** The form calls
`registerEmployee` → `RegisterEmployeeCommand` → `POST api/Auth/RegisterEmployee` →
`RegisterEmployee.Command`, which had six members and **no `TermsAccepted`** (the partner client and
the partner-mobile spec serialised only those six). The command that carried `termsAccepted` is
`Register.Command`, whose handler only ever creates `UserProfile.Customer` and stamps the customer
documents — the partner form cannot switch to it. Deleting the parking alone would have dropped the
employee consent entirely (the flush was a live path). Step 2 depends on step 1's regenerated partner
client.

## Doing — step 1 (backend)

`RegisterEmployee.Command` gains `bool? TermsAccepted = null`; the handler injects `IConsentService`
and, once the user exists (both branches — new user and unconfirmed upgrade), grants `TermsOfService`
and `PrivacyPolicy` with **no document** (the employee rule) when `TermsAccepted == true`. No validator
gate (employee registration is not gated — T-0745) and no audit marker. Unit test: `true` grants both
types unversioned; `false`/`null` grant nothing. Then regenerate the partner client and re-dump the
partner-mobile spec in the same commit.

## Doing — step 2 (partner web, after step 1's client is committed)

`PartnerAuthService.registerEmployee` gains `termsAccepted: boolean` and sets
`command.termsAccepted`; the register facade passes the form's tick; `signup-consent.service.ts` +
spec, the `services/index.ts` export, the inject and the `setSession` flush deleted; the parking tests
rewritten to assert `registerEmployee` is called with the tick; `grep -r "signup-consent"
src/Cleansia.App` empty.

## NOT

**Both mobile partner apps' parking stays** (iOS `SignupConsentFlowTests`, `RecordingSignupConsent`;
Android `partner-app/.../core/consent/ConsentModule.kt` `signup_consent` DataStore +
`GdprConsentClient`) — ADR-0041 territory; the docs must not describe iOS as the only parking client.
Not the partner host's anonymous `POST api/Auth/Register` (creates a Customer, carries the
customer-audience marker, its sole web consumer is dead code) — routed to T-0744's host gate.

## Status log

- 2026-09-14 — step 1 in `19dde3e5` (`RegisterEmployee.Command.TermsAccepted`, the handler grants both
  consents through `IConsentService` with no legal document on both branches,
  `RegisterEmployeeConsentTests`) and `752bc963` (the partner client and the partner-mobile spec
  regenerated so `RegisterEmployeeCommand` carries `termsAccepted`; the two iOS partner comments now
  name the reason the parking still holds — the registration issues no session and the employee
  agreement text is still open). Step 2 in `db0607a3` (`registerEmployee(…, termsAccepted)`,
  `SignupConsentService` deleted with its spec, export, inject and the `setSession` flush; grep empty;
  typecheck of all three apps green). Recorded in ADR-0062 D4 as amended.
- 2026-09-16 — the mobile residual retired on the owner's go for the branch sweep. `e336dbc4`: Android
  `AuthRepositoryImpl.register(…, termsAccepted, countryId)` puts the tick on `RegisterEmployeeCommand`
  (wire pin `AuthRepositoryTrustedDeviceTest.register_putsTheTermsTickOnTheCommandUnderTheNameTheBackendBinds`);
  `ConsentModule.kt`, `GdprConsentClient.kt`, `GdprConsentWireTest.kt`, `SignupConsentFlowTest.kt` deleted with
  the login/confirm-e-mail delivery hook; `GdprApi` moved to `core/gdpr/GdprModule.kt` for its one reader.
  iOS `RegisterViewModel` passes `termsAccepted: form.acceptTerms`; `PartnerSignupConsentClient`,
  `SignupConsentFlowTests`, the repository in the partner stack removed;
  `PartnerTrustedDeviceLoginTests.testRegisteringPutsTheTermsTickOnTheRegisterEmployeeBody` pins the body.
  `9ad0b0ed` (review): `ConsentWireStub.swift` and the now-unbound `CleansiaCore/Consent/*` seam, the
  `signupConsent:` parameter on `AuthApiClient` and three Core test files deleted; Android core consent
  comments now name the customer app as the only parker. `:partner-app` 569/569, `:core` 236/236,
  SwiftFormat and `check-ios-symbols` clean; XCTest runs in iOS CI. Recorded in ADR-0062 D4 as amended
  2026-09-16. Residual reported on the owner plate: the Android customer app still parks the tick beside
  sending it.
