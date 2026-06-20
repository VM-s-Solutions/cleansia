---
id: T-0265
title: Make email-validating ViewModels unit-testable off android.util.Patterns (Robolectric or extract)
status: draft
size: S
owner: pm
created: 2026-06-17
updated: 2026-06-17
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 7
source: T-0197 Phase-2 verification — partner/customer unit-test-env gap (pre-existing, proven on clean master)
---

## Context

While verifying T-0197 (mobile `ApiResult<T>` migration) on the real combined Android tree, the
orchestrator confirmed a **pre-existing, T-0197-independent** unit-test-environment gap: several
ViewModel unit tests fail under the **plain JVM** test runtime (`testDebugUnitTest`) — specifically
**`LoginViewModelTest` (×4 cases)** and **`DashboardViewModelTest`** — because they exercise email
validation that routes through **`android.util.Patterns.EMAIL_ADDRESS`**, and on a non-instrumented
JVM the Android framework stub returns **`null`** (the `android.jar` shipped to unit tests is the
no-op stub, not a real Android runtime). The null `Pattern` then NPEs / mis-validates inside the VM,
so the affected cases are **permanently red**.

This was **proven pre-existing**: the same tests fail **identically on a clean `master`** (no T-0197
change involved). It keeps the **partner suite permanently red** and is purely a test-environment /
testability defect — the production code path is correct on a real device, where
`android.util.Patterns.EMAIL_ADDRESS` is a real compiled pattern.

This ticket is **not** about changing app behavior — it is about making the email-validating VMs
**unit-testable** so the partner (and customer) suites can go green on plain JVM.

## Acceptance criteria

- [ ] **AC1** — Given the affected VMs validate email via `android.util.Patterns.EMAIL_ADDRESS`, When
  this ticket is worked, Then either **(a)** Robolectric (or an equivalent Android test runtime) is
  added so `android.util.Patterns.EMAIL_ADDRESS` resolves to a real pattern in unit tests, **or (b)**
  email validation is **extracted off** `android.util.Patterns` into a pure, JVM-testable validator
  (e.g. a small `EmailValidator` with a plain regex / `java.util.regex.Pattern`) that the VMs depend
  on — the dev picks one approach and records the rationale.
- [ ] **AC2** — Given the chosen approach lands, When `:partner-app:testDebugUnitTest` runs on plain
  JVM, Then **`LoginViewModelTest` (all 4 cases)** and **`DashboardViewModelTest`** pass (red → green
  recorded in the status log per `testing.md`).
- [ ] **AC3** — Given the customer-app has the equivalent email-validation path, When the same approach
  is applied there, Then any customer-app VM test that trips the same `android.util.Patterns` stub-null
  is green too (verify; if none currently trip it, note that and ensure no regression).
- [ ] **AC4** — Given the fix is in place, When both Android apps build and their unit suites run, Then
  both compile and all suites are green on plain JVM (no instrumented-device requirement introduced for
  these cases), with **no production behavior change** to email validation on a real device.

## Out of scope

- Any change to what counts as a valid email **on a real device** (behavior-preserving — if path (b)
  is chosen, the extracted validator must accept/reject the same inputs the production path does).
- The broader E1/E2 sealed-UiState / `ActionState` migration, E6 `collectAsStateWithLifecycle`, and E7
  dir/naming work — those are their own tickets (see `audits/consistency-violations.md` F13/F14/F15/F16).
- Any non-email test-env gaps — this ticket is scoped to the `android.util.Patterns.EMAIL_ADDRESS`
  stub-null issue and the two named failing test classes (+ the customer equivalent).

## Implementation notes

- **Root cause:** unit tests run against the **stub `android.jar`** where `android.util.Patterns`
  fields are `null` (the Android Gradle plugin's `returnDefaultValues` / stub behavior). Robolectric
  provides a shadowed runtime; extraction removes the framework dependency entirely (preferred when the
  validator is tiny and the rest of the suite is already pure-JVM — keeps the suite fast and
  device-independent).
- **Recommended default (b):** a `core`-level pure `EmailValidator` (or a Kotlin top-level
  `isValidEmail(String): Boolean`) backed by a vetted RFC-pragmatic regex, injected/used by
  `LoginViewModel` / `DashboardViewModel` (and the customer equivalent). This is the smaller, faster,
  device-independent fix and keeps the rest of the suite on plain JVM. If Robolectric is already a
  dependency elsewhere, (a) may be the lower-churn choice — dev's call, recorded.
- **Test-first (`testing.md`):** the failing `LoginViewModelTest`/`DashboardViewModelTest` cases are
  the red harness — fix the env/validator, watch them go green; add a direct red-green unit test for the
  extracted validator if path (b).
- **No `manual_steps`** — mobile-only test-infra / code change; no nswag-regen, no ef-migration.

## Status log
- 2026-06-17 — draft (created by pm). Filed as the T-0197 Phase-2 verification follow-up: the
  partner-app + customer-app unit-test-env gap where `android.util.Patterns.EMAIL_ADDRESS` returns
  `null` on plain JVM, keeping `LoginViewModelTest` (×4) + `DashboardViewModelTest` permanently red.
  **Proven pre-existing** (fails identically on clean `master`, independent of T-0197). Scope: add
  Robolectric **or** extract email validation off `android.util.Patterns` so the VMs are unit-testable.
  Size S, `[android]`, sprint 7. No new behavior/decision → no panel needed; goes `ready` once a dev
  confirms approach (a) vs (b) at contract-lock.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
