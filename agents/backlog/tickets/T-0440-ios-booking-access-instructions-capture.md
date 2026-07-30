---
id: T-0440
title: iOS — capture entry/access instructions on the booking confirm step
status: draft
size: S
owner: —
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: []
stories: [US-customer-access-instructions]
adrs: []
layers: [ios]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

The backend has accepted `accessInstructions` on `CreateOrder` since `bbcf5b24`
(`Features/Orders/CreateOrder.cs:224`, validated `.MaximumLength(2000)` at `:136-138`, persisted via
`OrderFactory.cs:121`). **No client sends it.** iOS renders it read-only on order detail
(`CleansiaCustomer/Sources/Features/Orders/OrderDetailDetailsCards.swift:139-140`) and the partner app
shows it on its order detail (`CleansiaPartner/Sources/Features/Orders/OrderDetailContent.swift:99`)
— so today both surfaces render a field that is always empty for iOS-placed orders.

This ticket adds the capture, mirroring `specialInstructions`, which is already captured end-to-end
on the same screen.

**Contract is READY — this ticket is NOT owner-blocked.** Verified 2026-07-30:
`src/cleansia_android/openapi/customer-mobile-api.json` → `CreateOrder_Command` carries
`accessInstructions`. The Swift client is generated **from that committed spec** by
`scripts/generate-api-clients.sh` (`src/cleansia_ios/openapi/README.md:13`,
`.github/workflows/ios-ci.yml:126`) into the **gitignored** `CleansiaCustomerApi/`
(`src/cleansia_ios/.gitignore:15`). **The working copy on this machine is STALE** — I read
`CleansiaCustomerApi/Models/CreateOrderCommand.swift` and it has neither `accessInstructions` *nor*
`specialInstructions`. **Run `./scripts/generate-api-clients.sh` before starting**, or the field will
appear not to exist. This is a local build artifact, not the owner-only NSwag step.

## Acceptance criteria

- [ ] **AC1** — Given the booking confirm step, When the user scrolls to the instructions area, Then
      an access/entry-instructions field is present, styled and positioned exactly like the existing
      special-instructions field. Evidence: screenshot + the view at file:line.
- [ ] **AC2** — Given the user typed entry instructions, When the order is submitted, Then
      `CreateOrderCommand.accessInstructions` carries the text **trimmed**; when the field is blank or
      whitespace-only, Then it is `nil`. Evidence: a unit test in `BookingSubmitTests.swift` mirroring
      the existing `specialInstructions` pair at `:361-385` (both arms).
- [ ] **AC3** — Given the field, When the user types past **2000** characters, Then input is capped —
      matching `CreateOrder.cs:136-138`. Evidence: the cap at file:line + a test.
- [ ] **AC4** — Given the booking draft survives sheet dismissal (T-0371 behavior), When the user
      dismisses and reopens, Then the typed access instructions are still there. Evidence: extend
      `BookingDraftSurvivalTests.swift` (it already pins `specialInstructions` at `:45,:68`).
- [ ] **AC5** — Gate 8: `xcodebuild build test` green for `CleansiaCustomer` on an iPhone simulator;
      SwiftFormat `--lint` 0.60.1 + SwiftLint `--strict` 0.65.0 clean.
- [ ] **AC6** — Gate 8.5: iOS **16.4** floor smoke of the booking confirm surface — launch, navigate
      to confirm, render the new field, submit. Recorded on the ticket.

## Out of scope

- Web and Android capture — T-0438 (web, which also unbreaks the build) and T-0441 (Android).
- Any change to the read/display side on either iOS app — already shipped.
- Renaming `specialInstructions` or merging the two fields — they are distinct on the backend.

## Implementation notes

- The three edit points mirror `specialInstructions` exactly:
  1. `CleansiaCustomer/Sources/Features/Booking/BookingState.swift` — add
     `var accessInstructions: String = ""` next to `specialInstructions` (`:25`).
  2. `CleansiaCustomer/Sources/Features/Booking/Confirm/ConfirmStep.swift` — add a section beside
     `specialInstructionsSection` (`:73` in the body, defined `:174-184`). The existing section wraps
     a `SpecialInstructionsField`; prefer generalizing that component over duplicating it if the
     rename is cheap and the partner app does not consume it — otherwise add a sibling.
  3. `CleansiaCustomer/Sources/Features/Booking/Submit/BookingOrderCommandFactory.swift` — the
     `instructions` closure at `:31-37` is the exact trim/nil pattern to copy; add the field to the
     `CreateOrderCommand(...)` call at `:56`.
- **i18n (verified 2026-07-30):** the *display* keys already exist and must be reused, not
  duplicated — `L10n.OrderDetail.accessInstructions` (`CleansiaCustomer/Sources/L10n+Orders.swift:148`).
  A **confirm-step input hint does NOT exist** — `booking_special_instructions_hint` is the only one
  (`L10n+BookingConfirm.swift:4`). A new `booking_access_instructions_hint` key **is** required in
  all 5 locales. Do not assume the pre-seeded display key covers the input label.
- **Shared-file lane:** `CleansiaCustomer/Resources/Localizable.xcstrings` is serialized — this ticket
  is the only writer this wave. **The working tree carries uncommitted iOS changes and the set moves —
  re-read `git status` before starting.** Do **not** `git restore` any of them, and do **not** read or
  modify `Info.plist` / `project.yml` — the owner's live Stripe key lives in those working copies.
- If an app-scheme build is needed in a scratch worktree, `xcodegen generate` is safe **there**
  (the committed `project.yml` has no key); never in the main working tree.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 2, iOS half)
- 2026-07-30 — awaiting analyst deliberation panel (shared story US-customer-access-instructions) before `ready`

## Review
<!-- reviewer writes verdict here -->
