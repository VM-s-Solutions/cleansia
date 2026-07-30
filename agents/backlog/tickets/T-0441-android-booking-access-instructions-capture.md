---
id: T-0441
title: Android — capture entry/access instructions on the booking confirm step
status: draft
size: S
owner: —
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: [T-0448]
stories: [US-customer-access-instructions]
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Same gap as T-0440, on Android. The backend accepts `accessInstructions`
(`Features/Orders/CreateOrder.cs:224`, `.MaximumLength(2000)` at `:136-138`); the Android customer app
**renders** it read-only on order detail
(`customer-app/.../features/orders/OrderDetailDetailsCards.kt:238`, gated at `OrderDetailScreen.kt:646`)
and the partner app shows it in `FromCustomerNotesCard.kt:45` — but nothing ever populates it,
because the confirm step never collects it and the create DTO has no such field.

**Contract is READY — not owner-blocked.** Verified 2026-07-30:
`src/cleansia_android/openapi/customer-mobile-api.json` → `CreateOrder_Command` carries
`accessInstructions`. The Kotlin client is generated from that committed spec at build time into
`customer-app/build/generated/openapi/**` (untracked), so a normal Gradle build picks it up.

## Acceptance criteria

- [ ] **AC1** — Given the booking confirm step, When the user reaches the instructions area, Then an
      access/entry-instructions field is present, mirroring the existing special-instructions field
      (`customer-app/.../features/booking/ConfirmStep.kt:349-350`). Evidence: screenshot + file:line.
- [ ] **AC2** — Given the user typed entry instructions, When the order is submitted, Then the wire
      command carries the text **trimmed**; blank/whitespace-only → `null`. Evidence: a unit test in
      `BookingViewModelTest.kt` mirroring the `specialInstructions` pair at `:205-213` and `:243-251`
      (both arms).
- [ ] **AC3** — Given the field, When the user types past **2000** characters, Then input is capped,
      matching the backend validator. Evidence: the cap at file:line + a test.
- [ ] **AC4** — Gate 8: `:core`, `:customer-app` `compileDebugKotlin` + `testDebugUnitTest` succeed.
      **The run must not be `UP-TO-DATE`** — a cached no-op run verifies nothing (this is the exact
      miss T-0445 codifies). Record the task outcomes, not just "BUILD SUCCESSFUL".
- [ ] **AC5** — Kotlin sources stay ASCII/UTF-8 clean: no BOM, no mojibake in the diff (Gate 8
      "Android touched"). Evidence: a byte-level check of the changed files, especially the 5
      `strings.xml`.

## Out of scope

- iOS and web capture — T-0440 / T-0438.
- The partner app: it only displays the field and already does so correctly.
- Avatar work on the same screens — T-0448.

## Implementation notes

- Four edit points, all mirroring `specialInstructions`:
  1. `customer-app/.../features/booking/BookingState.kt:52` — add
     `val accessInstructions: String = ""`.
  2. `customer-app/.../features/booking/ConfirmStep.kt:349-350` — add the sibling field.
  3. `customer-app/.../features/booking/BookingViewModel.kt:455` — the exact pattern to copy is
     `specialInstructions = s.specialInstructions.trim().ifBlank { null }`.
  4. `customer-app/.../core/booking/BookingDtos.kt:120` (add the property, with the same
     documentation-comment discipline the neighbours use) and
     `customer-app/.../core/booking/BookingApi.kt:73` (map it onto the generated command).
- **i18n (verified 2026-07-30 — read this before adding keys):**
  - `order_detail_access_instructions` **already exists** in all 5 customer locales
    (`customer-app/src/main/res/values{,-cs,-sk,-uk,-ru}/strings.xml`, en at `:273`) — that is the
    **order-detail display** label. Reuse it there; do **not** add a duplicate.
  - `access_instructions` already exists ×5 in the **partner** app (`values/strings.xml:295`) — a
    different app, out of scope here.
  - A **confirm-step input hint does NOT exist.** The only booking hint is
    `booking_special_instructions_hint` (`values/strings.xml:720`). A new
    `booking_access_instructions_hint` **is** required in all 5 locales. The brief's "already exists
    in five locales" is true of the *display* key only.
- **Shared-file lane:** `customer-app/src/main/res/values*/strings.xml` (5 files) is a serialized
  cluster. This ticket is the sole writer this wave; **T-0448 must wait for it** (recorded in
  `blocks:`). Edit only your own hunks; never `git restore` a `strings.xml`.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 2, Android half)
- 2026-07-30 — awaiting analyst deliberation panel (shared story US-customer-access-instructions) before `ready`

## Review
<!-- reviewer writes verdict here -->
