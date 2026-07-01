---
id: T-0351
title: "Android customer SecurityScreen is a dead stub — wire onChangePassword to the existing reset-code flow"
status: done
size: S
owner: android
created: 2026-06-30
updated: 2026-06-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: T-0314 Slice-F review (sprint-12 §7.17) — iOS-right / Android-stub parity catch-up
---

> **Parity catch-up surfaced by the T-0314 Slice-F review.** iOS shipped the REAL change-password flow on
> the customer Settings/Security surface; the Android customer equivalent is a **dead stub** — the screen
> collects passwords but the "Update" button is a no-op. This is the same iOS-right/Android-stub pattern
> the wave has filed before (F1 → T-0333 partner Register/Forgot i18n).

## The gap
The customer Android `SecurityScreen.kt` (`customer-app/.../features/profile/SecurityScreen.kt`) renders the
three change-password fields (current / new / confirm) and a "Update" `CleansiaPrimaryButton`, but its
`onChangePassword: () -> Unit` parameter **defaults to a no-op `{}`** (`SecurityScreen.kt:38,66`) **and the
sole call site never passes one** — `CleansiaNavHost.kt:432` calls
`SecurityScreen(onBack = { navController.popBackStack() })`, so tapping "Update" does **nothing**. There is
no ViewModel wired, no API call; the password the user types is discarded.

A working reset-code change-password flow already exists in the customer app — the forgot-password path:
`AuthViewModel.requestPasswordChange(email)` → `AuthViewModel.changePassword(email, code, newPassword)`,
wired by `ForgotPasswordScreen` (`CleansiaNavHost.kt:242-246`). The signed-in SecurityScreen should reuse
that same reset-code flow (the email is known from the session) rather than introduce a parallel path.

## Acceptance criteria
- [x] **AC1** — Given a signed-in customer on Settings → Security, When they fill current/new/confirm and tap
  "Update" with valid input, Then the existing reset-code change-password flow runs to completion (success →
  a confirmation snackbar + pop back; failure → the localized error, no silent discard).
- [x] **AC2** — `SecurityScreen`'s `onChangePassword` is wired at the `CleansiaNavHost.kt:432` call site
  (no longer the default no-op); no parallel/duplicate change-password API path is introduced (it rides the
  existing `AuthViewModel.requestPasswordChange`/`changePassword` reset-code flow or the project's canonical
  equivalent).
- [x] **AC3** — User-visible strings are `R.string.*` (no hardcoded English); the existing customer build +
  tests stay green.

## Out of scope
- The iOS side (already shipped in T-0314 Slice F).
- Any backend change (the reset-code endpoints already exist and serve the forgot-password flow).
- Validation-rule changes beyond what the screen already enforces (current non-blank, new ≥ 12, confirm match).

## Implementation notes
- The screen already has the three fields + the `valid` predicate; only the action is dead.
- Reuse the customer `AuthViewModel` reset-code flow (`requestPasswordChange(email)` →
  `changePassword(email, code, newPassword)`). For a signed-in user the email comes from the session; decide
  whether the in-app flow sends-then-enters-code or surfaces the same two-step UX the forgot path uses —
  keep it parity-consistent with how the partner app handles in-app change-password if a precedent exists.
- Confirm the success/error effects route through the existing snackbar/effect bus (do NOT add a new one).

## Status log
- 2026-06-30 — filed from the T-0314 Slice-F review (§7.17). iOS shipped the real change-password flow; the
  Android customer SecurityScreen "Update" button is a dead no-op (`SecurityScreen.kt:38,66` +
  `CleansiaNavHost.kt:432`). Wire it to the existing reset-code flow. `proposed`, not dispatched.
- 2026-06-30 — **proposed → done** (HARDENING-1, `1d99333` on `phase/hardening-1`, off master `3e7ce52`;
  bundled in the android parity-hygiene commit with T-0333 + T-0337). The dead `SecurityScreen` "Update" stub
  is wired to the existing customer reset-code change-password flow (`AuthViewModel.requestPasswordChange` →
  `changePassword`, the same path the forgot-password flow uses; the email comes from the session); the
  `CleansiaNavHost.kt:432` call site now passes the action (no longer the default no-op `{}`); success/error
  route through the existing snackbar/effect bus; strings are `R.string.*`. No backend change (the reset-code
  endpoints already exist), no parallel API path. **Verified by a LOCAL gradle build** (JDK21/SDK35 — partner
  + customer compile) since `android-ci` runs only on PR. Reviewer **APPROVE**. NOT committed by the PM — the
  owner commits the backlog edits with the phase PR.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
