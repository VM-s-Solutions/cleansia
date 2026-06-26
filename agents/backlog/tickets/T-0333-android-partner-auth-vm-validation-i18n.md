---
id: T-0333
title: "E8/F1 — localize the Android partner Register/Forgot ViewModel validation strings (move hardcoded English literals to R.string.*)"
status: ready
size: S
owner: —
created: 2026-06-26
updated: 2026-06-26
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 12
source: sprint-12 §7.5 Decision 5 (the F1 iOS-port parity deviation); knowledge/consistency.md §E8; surfaced on T-0305 (iOS partner auth completeness)
---

## Context

Android consistency rule **E8** (all user-facing text via `stringResource(R.string.x)` /
`appContext.getString(...)`) has a recorded **parity deviation (F1)**: the Android **partner** auth
ViewModels set their **validation** error strings as **hardcoded English literals** instead of localized
`R.string.*` resources, so the register/forgot field errors render English in **all 5 locales**
(en/cs/sk/uk/ru). Rule + deviation: `knowledge/consistency.md` §E8; ruling: `status/sprint-12.md` §7.5
Decision 5.

This was surfaced by the iOS port (**T-0305**, partner auth completeness): **iOS does it right** —
the iOS Register/Forgot/Confirm VMs use `Localizable.xcstrings` keys ×5 for every validation message
(ADR-0013 D11 / reviewer #10). iOS is the **correct** reference; the Android literals were **NOT**
replicated on iOS (the `patterns-mobile.md` Parity rule: Android-wrong → diverge correctly on iOS +
raise an Android finding, don't silently copy). This ticket is the PM-filed **android follow-up** that
fixes the Android side. **It is independent of the iOS work** — the iOS wave does not depend on it and
is not blocked by it.

### The defect (verified — sprint-12 §7.5 D5)

Two partner-app auth VMs do **not** inject `@ApplicationContext Context`, so their validation messages
never localize:

1. **`partner-app/.../features/auth/RegisterViewModel.kt:64-84`** — raw English literals for the
   validation errors: `"First name is required"`, `"Please enter a valid email"`, `"Password must be at
   least 8 characters with a letter and a number"`, `"Passwords do not match"`, `"You must accept the
   terms"`, etc.
2. **`partner-app/.../features/auth/ForgotPasswordViewModel.kt:45-52`** — raw English literals
   (`"Email is required"`, `"Please enter a valid email"`).

E8's "already consistent — keep it" claim is **wrong** for exactly these two partner auth VMs; the rest
of the codebase is consistent.

## Acceptance criteria

- [ ] **AC1 (strings localized)** — Every validation error string in `RegisterViewModel.kt:64-84` and
  `ForgotPasswordViewModel.kt:45-52` is sourced from `R.string.*` (via `appContext.getString(...)`),
  not a raw English literal. No hardcoded user-facing validation string remains in either VM.
- [ ] **AC2 (5-locale completeness)** — Each new/used `R.string.*` key exists in **all 5** partner-app
  string resources (`values/` en + `values-cs/`, `values-sk/`, `values-uk/`, `values-ru/`); reuse the
  existing `:core` / partner-app keys where an equivalent already exists rather than adding a duplicate.
  No missing-translation gap across the 5 locales.
- [ ] **AC3 (Context injected, mirroring the canonical form)** — `RegisterViewModel` and
  `ForgotPasswordViewModel` inject `@ApplicationContext Context` and resolve strings via
  `appContext.getString(...)`, mirroring the established pattern (`OrderDetailViewModel.kt:80`). No new
  string-resolution paradigm.
- [ ] **AC4 (behavior identical except language)** — Same validation triggers, same field-error mapping,
  same UX — the only observable change is that the messages now render in the active locale. No new
  validation rule, no changed predicate, no API/DTO change.
- [ ] **AC5 (consistency gate + suite green)** — the partner-app builds and `:partner-app` unit tests are
  green; a re-scan confirms `RegisterViewModel`/`ForgotPasswordViewModel` no longer hold hardcoded
  validation literals (the E8/F1 deviation cleared for these two VMs).

## Out of scope

- The **iOS** side — iOS already localizes these strings correctly (T-0305); no iOS change here.
- Any **validation-rule** change (the password predicate ≥8 && letter && digit, the email/terms checks)
  — this is an i18n-only move of the **message** strings, not the rules.
- The customer-app auth VMs (`SignUpScreen`/its VM) — not part of the F1 deviation (the F1 finding names
  the **partner** Register/Forgot VMs specifically).
- Any new screen, feature, API/DTO, or behavior change beyond the locale of the validation messages.

## Implementation notes

- **Canonical form:** `knowledge/consistency.md` §E8; mirror `OrderDetailViewModel.kt:80`
  (`appContext.getString(...)` via injected `@ApplicationContext Context`).
- **Reuse keys, don't duplicate:** prefer existing partner-app / `:core` string keys for the common
  validation messages (required-field, invalid-email, password-rule, passwords-mismatch, accept-terms)
  before adding new `R.string.*` keys; add the missing ones across all 5 locales if no equivalent exists.
- **No new behavior/decision → no deliberation panel.** Pure mechanical i18n canonicalization of two VMs
  against the already-ratified §E8 rule (the one-line no-decision note: the iOS reference at T-0305 is
  correct; this only aligns Android). Independent of the iOS wave.
- **No `manual_steps`** — mobile-only, partner-app only: no nswag-regen, no ef-migration. (Adding
  `strings.xml` entries is in-repo, not an owner manual step.)
- **Lane:** partner-app `features/auth/*` + the partner-app `values-*/strings.xml` bundles — serialize
  against any other concurrent partner-app string-bundle edit. Runs as a single **S** ticket,
  reviewer-per-developer, no security/optimizer gate.

## Status log
- 2026-06-26 — **draft → ready** (created by pm). Source: **sprint-12 §7.5 Decision 5** (the F1 iOS-port
  parity deviation surfaced on T-0305) + `knowledge/consistency.md` §E8. DoR-aligned: AC observable +
  i18n-completeness gated, sized **S**, no deps (independent of the iOS wave — iOS already does it right),
  partner-app-only, mobile-only (no migration/regen). **No deliberation panel** (no-decision mechanical
  i18n canonicalization against §E8; the iOS T-0305 reference is the correct form). The hardcoded English
  validation literals in `RegisterViewModel.kt:64-84` + `ForgotPasswordViewModel.kt:45-52` move to
  `R.string.*` across all 5 locales.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
</content>
</invoke>
