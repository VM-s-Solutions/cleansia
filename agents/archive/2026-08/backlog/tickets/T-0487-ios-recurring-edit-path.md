---
id: T-0487
title: iOS — build the recurring-booking edit path (nothing exists below the generated client)
status: draft
size: M
owner: ios
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0485]
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #6 (2026-08-02):** *"Cannot edit a recurring cleaning setup on either mobile app."*
iOS half. **Android is T-0486.** Both are rewritten from **T-0485**'s story before dispatch.

### Ground truth — PM-verified on `master` at `0e4ede1b`

**iOS has strictly less than Android here.** Where Android at least has a written-but-uncalled
repository function, iOS has **nothing below the generated client**:

| Layer | State |
|---|---|
| Generated client | `CleansiaCustomerApi/Models/UpdateRecurringBookingCommand.swift` — present; `APIs/CustomerRecurringBookingAPI.swift` carries the operation | **exists** |
| `Features/Recurring/Data/RecurringBookingClient.swift` | grep for `update` → **nothing** |
| `Features/Recurring/Data/RecurringBookingRepository.swift` | grep for `update` → **nothing** |
| `CreateRecurringViewModel.swift` (200 lines) | create-only |
| `RecurringBookingsViewModel.swift` (63 lines) | list + pause/resume/delete |
| UI | no edit affordance |

**And the iOS create screen is not the Android one.** `CreateRecurringScreen.swift` is a **268-line
single-page form** (`FrequencySection`, `TimeSection`, `AddressSection`, `ServicesSection`,
`PaymentSection`, `StartsSection`) against Android's ~1071-line **3-step wizard**. **PM-measured: 19
`recurring_*` string keys exist on Android and not on iOS.** So "reuse the create screen for editing"
means something materially different on each platform — which is why T-0485 AC4 must specify the entry
point for both rather than one.

**iOS carries three keys Android does not:** `recurring_plus_gate_title`, `_subtitle`, `_cta`. The
Plus gate is visible on iOS. **T-0494** establishes that this gate is enforced **client-side only** —
a direct API call succeeds. That is not this ticket's fix, but an edit path is a **second** client-side
gate on the same resource, so it must not be built as if the gate were real.

## Acceptance criteria

> **⚠️ These AC are PROVISIONAL. T-0485's story replaces AC1–AC4 before this ticket goes `ready`.**
> AC5–AC8 are stable.

- [ ] **AC1 (provisional) — an edit entry point exists** at the location T-0485 AC4 specifies, and a
      customer can change the fields T-0485 AC1 marks editable and see the change persisted after a
      cold launch. Evidence: screen recording or before/after screenshots plus the relaunch.
- [ ] **AC2 (provisional) — the already-generated-orders behaviour matches T-0485 AC2** and the copy
      tells the customer what it does and does not touch. Evidence: the copy plus screenshots.
- [ ] **AC3 (provisional) — the Data layer gains `update` at the client AND repository levels**,
      following the shape the create path already uses in the same two files — not a one-off call
      from a ViewModel. Evidence: the diff.
- [ ] **AC4 (provisional) — the ViewModel carries an edit mode or a sibling VM**, stated with a
      reason. `CreateRecurringViewModel.swift` is create-only today. Evidence: the stated choice.
- [ ] **AC5 — the strings are added to all five locales in one change.** Any new key lands in
      `CleansiaCustomer/Resources/Localizable.xcstrings` with **`cs`, `en`, `ru`, `sk`, `uk`** — the
      file's existing invariant (PM-verified: all 46 `recurring*` keys carry all five, zero
      exceptions). Evidence: the parity check over the new keys.
- [ ] **AC6 — the failure path is real.** A failed update surfaces the backend error through the
      existing snackbar/`ApiErrorLocalizer` path, not a silent no-op. Evidence: an error-path test
      plus the screenshot.
- [ ] **AC7 — a test that goes red against the current code (Gate 0.5 leg 1).** A ViewModel test
      driving the edit path, proved to fail before the wiring exists. Evidence: the red run, then
      green.
- [ ] **AC8 (Gate 0.5)** — `xcodebuild build test` for `CleansiaCustomer` on the **16.4 floor**
      (plus `CleansiaCore` from the package dir if Core is touched), SwiftFormat `--lint` 0.60.1 /
      SwiftLint `--strict` 0.65.0, with an honest statement of whether the app-scheme tests compiled
      and ran.

## Out of scope

- **Android** — T-0486.
- **Any backend change.** If T-0485 AC3 finds a missing field, that is a backend ticket the story
  names, and this ticket **holds**.
- **Closing the client-side-only Plus gate** — **T-0494**. Do not add a second client-side gate and
  describe it as enforcement.
- **Making the iOS wizard match Android's 3-step shape** — T-0481 owns that question.
- **The 19 missing `recurring_*` keys.** Same reason.

## Implementation notes

**No panel of its own — T-0485 is the panel.**

**Shared-file lane:** `Features/Recurring/**` has **no other sprint-15 claimant** except **T-0478**,
which *reads* the same screens to reproduce an i18n report and is expected to produce a small or empty
diff. **T-0478 first** — it is `S` and it may close without a change at all.

**`Localizable.xcstrings` is a serialized lane** (`process/shared-file-lanes.md`, i18n bundles).
Check for another writer before AC5's edit.

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`.**

**Before starting:** `src/cleansia_ios/scripts/generate-api-clients.sh` + `xcodegen generate` in both
app dirs (**T-0474**). New Swift files that are not in `project.pbxproj` are silently absent from the
build — the exact failure that has cost the owner three broken builds.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #6).** The complete absence of any
  `update` below the generated client, the single-page-form shape, the 19-key gap against Android and
  the three iOS-only Plus-gate keys are all PM-verified at `0e4ede1b`. **`depends_on: [T-0485]`** —
  AC1–AC4 are explicitly provisional.

## Review
