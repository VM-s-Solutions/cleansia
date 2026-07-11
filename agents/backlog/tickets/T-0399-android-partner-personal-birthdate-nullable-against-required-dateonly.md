---
id: T-0399
title: "Android partner — Personal info submits a null birthDate against the backend's REQUIRED DateOnly (`validation.invalid_date` for any user without a stored birth date; birth date not required client-side)"
status: proposed
size: S
owner: android
created: 2026-07-11
updated: 2026-07-11
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix2 iOS partner birth-date fix (owner-reported validation.invalid_date on the iOS Personal step; the diagnosis surfaced the identical latent Android defect)
---

> **Found while fixing the iOS partner Personal-info bug (owner-reported).** The backend
> `UpdatePersonalInfo.Command.BirthDate` is a **required, non-nullable `DateOnly`**
> (`Features/Employees/UpdatePersonalInfo.cs:38-42,62`, validated `MustBeValidDate() + MustBeInPast() +
> MustBeReasonableAge()` → `validation.invalid_date`). Android's `PersonalSectionViewModel.kt:115` submits
> `birthDate = form.birthDate.takeIf { it.isNotBlank() }` — i.e. **null when the field is blank** — so any
> partner whose profile has no stored birth date gets the same opaque `validation.invalid_date` the owner hit
> on iOS. Android HAS the `BirthDateField` UI (`PersonalSectionScreen.kt:124,237-`), but nothing requires it
> before save. iOS now requires the date client-side with an inline field error; Android should match.

## Acceptance criteria
- [ ] **AC1** — a blank birth date blocks save() with an inline required-field error on the BirthDateField
  (exactly like the firstName/lastName required checks at `PersonalSectionViewModel.kt:96-100`), instead of
  submitting null and surfacing the backend `validation.invalid_date`.
- [ ] **AC2** — a set birth date submits unchanged (yyyy-MM-dd); prefill from the loaded employee still works.
- [ ] **AC3** — `:partner-app` compiles; existing Personal-section tests green; a new VM test pins
  blank→blocked-with-error and set→submitted.
- [ ] **AC4 (copy parity)** — the required-error string matches the iOS wording (an `error_birth_date_required`
  style key ×5 locales; iOS added its key from the shared naming — reuse the same base name).

## Out of scope
- Relaxing the backend requirement (product says birth date is required for employees — reasonable-age check).
- The iOS fix (already landed on phase/ios-fix2).

## Status log
- 2026-07-11 — filed `proposed` by pm during the iOS partner birth-date fix. Identical defect class; latent on
  Android only because most existing Android partners already have a stored birth date.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
