---
id: T-0337
title: Android partner profile VMs — migrate flag-bag UiState to sealed states (E1) + move hardcoded validation/error strings to R.string.* (E8)
status: done
size: S
owner: android
created: 2026-06-26
updated: 2026-06-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 12
source: sprint-12 §7.7 Decision 5 (architect) — consistency.md E1/E8
---

> **Android follow-up — independent of the iOS wave.** Surfaced by the T-0310 Understand pass (sprint-12 §7.7
> Decision 5): the iOS port is born sealed-state canonical (`UiState<T>` + `ActionState`, `.xcstrings` ×5), and
> the Android partner profile VMs it ports from are an **E1 flag-bag** + **E8 hardcoded-string** smell. Per the
> `patterns-mobile.md` Parity rule (Android-wrong → diverge correctly on iOS, raise the Android finding, don't
> copy), iOS does it right and **this** ticket fixes Android. Same shape as F1/**T-0333** (the Register/Forgot
> i18n fix). **No-decision note (panel skipped):** a mechanical consistency cleanup against the **already-ratified**
> `consistency.md` E1/E2/E8 rules; no new behavior or architectural decision.

## Context

The T-0310 Understand pass confirmed the Android partner profile VMs are E1/E8 violations (verified in source):

1. **E1 flag-bags** (`consistency.md:160-163`): `ProfileViewModel.kt:26-36`
   `ProfileUiState(isLoading, employee?, contractStatus?, error?, isSignedOut)` and the section
   `*UiState` — `PersonalSectionViewModel.kt:17-30` `PersonalSectionUiState(isLoading, isSaving, …fields…,
   firstNameError?, lastNameError?, error?, isSaved)` (and the Address/Identification/Bank/Emergency/Documents
   siblings) — are single flag-bag `data class`es mixing a **load** lifecycle (`isLoading`/`employee`/`error`) and
   a **save** lifecycle (`isSaving`/`isSaved`) in one bag, permitting impossible states.
2. **E8 hardcoded strings** (`consistency.md:194-203`): the section VMs set validation/error strings as raw
   English literals (no `@ApplicationContext Context`, no `R.string.*`) — `PersonalSectionViewModel.kt:82`
   "First name is required", `:91` "Profile not loaded yet"; `AddressSectionViewModel.kt:201` "Pick your address
   on the map first", `:205` "Profile not loaded yet", `:220` "This country isn't serviced yet" → they render
   English in all 5 locales (the same F1 class as `RegisterViewModel`/`ForgotPasswordViewModel`, T-0333).

## Acceptance criteria
- [x] **AC1 — Sealed load state (E1).** `ProfileViewModel` + each profile `*SectionViewModel` expose a sealed
  `*UiState` (`Loading`/`Error(canRetry)`/`Loaded(data)`) for the load lifecycle — no `isLoading`/`error` flag-bag.
- [x] **AC2 — `ActionState` for save (E2).** The section save uses the shared `cz.cleansia.core.ui.state.ActionState`
  (`Idle`/`Submitting`/`Error`) + a `SharedFlow(replay=0)` success effect — not loose `isSaving`/`isSaved` booleans.
- [x] **AC3 — Strings localized (E8).** Every validation/error literal moves to `R.string.*` (inject
  `@ApplicationContext Context` per E3, mirror `OrderDetailViewModel.kt:80`); all 5 locale files
  (`en/cs/sk/uk/ru`) carry the keys. No hardcoded user-facing string in the profile VMs.
- [x] **AC4 — Behavior unchanged + gates green.** The screens render identically (load/error/saved/sign-out
  paths preserved); the onboarding-chain routing (`OnboardingChainViewModel`) is untouched in behavior; Android
  build + tests green; lint clean.

## Out of scope
- **No iOS change** — iOS is already correct (sprint-12 §7.7 D5).
- **No backend / contract change** — pure client-side state + i18n cleanup.
- **No new feature** — the screens behave the same; only the state shape + string source change. (The Android
  `RegisterViewModel`/`ForgotPasswordViewModel` i18n is the separate **T-0333**; this ticket is the profile VMs.)

## Implementation notes
Mirror the customer-app's mostly-correct sealed-state shape (`consistency.md:213`) + the `OrderDetailViewModel.kt`
ViewModel idiom (`patterns-mobile.md` §"ViewModel — exact idiom"). Files: `partner-app/.../features/profile/`
`ProfileViewModel.kt` + `{Personal,Address,Identification,Bank,Emergency,Documents}SectionViewModel.kt`.
Reviewer-per-developer; no `security` gate; no `optimizer`. **Routing:** `[android]`. QA = the load/error/save/
sign-out paths + the 5-locale string presence.

## Status log
- 2026-06-26 — draft (created by architect ruling, sprint-12 §7.7 Decision 5; consistency.md E1/E8 updated to
  name these VMs). The iOS port (T-0310) is born sealed-state canonical + localized; this is the Android-side fix
  per the Parity rule (don't copy the Android smell to iOS — fix Android separately). Same shape + independence as
  F1/T-0333. Dedup-checked: distinct from T-0333 (that's Register/Forgot auth VMs; this is the profile VMs).
  `depends_on: []`; `security_touching: false`; `manual_steps: []`; sized **S** (sealed-state migrate + i18n move,
  no behavior change). No panel (no-decision: mechanical cleanup against ratified consistency rules).
- 2026-06-30 — **draft → done** (HARDENING-1, `1d99333` on `phase/hardening-1`, off master `3e7ce52`; bundled
  in the android parity-hygiene commit with T-0333 + T-0351). Migrated the **7 partner profile VMs**
  (`Profile` + the `{Personal,Address,Identification,Bank,Emergency,Documents}Section` VMs) to sealed
  `Loading`/`Error`/`Loaded` load states + `ActionState` for save + `R.string.*` ×5 for every validation/error
  literal; screens consume the sealed state via `is Loading` + `as? Loaded` (behavior-preserved); added a
  `BankSectionViewModelTest`. **Verified by a LOCAL gradle build** (JDK21/SDK35 — partner + customer compile,
  the new test passes) since `android-ci` runs only on PR. Reviewer **APPROVE**. **The android review surfaced
  a residual UX gap** — because the section screens consume the sealed state with `is Loading` + `as? Loaded`
  (not an exhaustive `when`), the **Error state renders an empty editable form with no retry affordance**
  (behavior-preserved from before, but a gap) — filed as the NON-blocking enhancement follow-up **T-0353**.
  NOT committed by the PM — the owner commits the backlog edits with the phase PR.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
