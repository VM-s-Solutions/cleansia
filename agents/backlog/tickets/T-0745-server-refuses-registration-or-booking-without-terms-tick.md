---
id: T-0745
title: The server refuses a customer registration or booking without the terms tick, and every client sends it (L4)
status: done
size: M
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: [T-0742]
blocks: []
stories: []
adrs: [ADR-0062, ADR-0063, ADR-0041]
layers: [backend, android, ios]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-14 (Q-AUD-L4): *"Refuse on server without a tick, basically your recommendation"*
— mobile first, then the rule. No production → no old builds; sequenced inside the batch, the rule on
at the end of it. The Android customer app sent `termsAccepted` on the social sign-ups but not on the
email `RegisterRequest` nor on the booking; the iOS customer app parked the tick client-side
(`signupConsent.recordSignupTick`) and sent nothing on the booking.

## Doing (android)

Customer app: email registration sends `termsAccepted = true` from the existing tick; the booking
sends it on `CreateOrder` when the review step's terms box is ticked — the box shown unless the
signed-in account already holds both consents (read `Gdpr/consents`); the review step gated on it.
View-model unit tests.

## Doing (ios)

Customer app: the hand-written `RegisterRequest` gains `termsAccepted: Bool?`; sign-up sends `true`;
the `signupConsent.recordSignupTick` parking and its delivery machinery deleted with their tests; the
booking review step shows the tick unless both consents are on record and sends it on create.
SwiftFormat/SwiftLint; XCTest unrun on Windows.

## Doing (backend — last)

`Register.Validator`, `GoogleAuth`/`AppleAuth` provisioning, `CreateOrder.Validator`: `TermsAccepted ==
true` required, key `consent.terms_not_accepted`, five locales in every app that can reach it.
`ConfirmRecurringOrder` not gated; employee registration not gated (ADR-0041). HostTests: a
registration without the tick → 400 with the key; the failure row records it.

## NOT

No gating of partner/employee paths; no re-acceptance on a new version.

## Status log

- 2026-09-14 — Android in `b5b0f6b5` + `e48806fd`; iOS in `922f0331` + `fcc2ac9c` (the register
  seam's `termsAccepted` carries no default so every caller names the tick or its absence); backend in
  `444fcda1`: `Register.Validator` requires `TermsAccepted == true` (null and false one refusal; the
  member stays `bool?`); `CreateOrder.Validator` requires it **unless** the signed-in customer holds
  both consents granted and not withdrawn — the tick short-circuits the read; the rule sits ahead of
  the price chain; **ratified deviation:** `GoogleAuth`/`AppleAuth` provisioning keeps
  `auth.social_account_not_found` (on the shared endpoint the tick is the screen discriminator and
  every client's sign-up screen refuses client-side first); `ConfirmRecurringOrder` and
  `RegisterEmployee` not gated; the key in five locales of customer + partner web (both route
  `Register`), the Android customer app and the iOS Core catalogue; every fixture that registers or
  books asserts the tick. `ecbf193a`: the iOS partner error-voice roster carries the key. Residual
  (A6 "revisit"): `CreateOrder` records the tick but grants no consent rows for a signed-in customer
  without them — only DEV accounts are ever asked again. Recorded in ADR-0062 D4 as amended.
