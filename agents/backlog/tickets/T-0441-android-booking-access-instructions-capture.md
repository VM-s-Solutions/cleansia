---
id: T-0441
title: Android — capture entry/access instructions on the booking confirm step
status: qa
size: S
owner: qa
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: [T-0450, T-0448, T-0467]
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

- 2026-07-30 — **in_progress** — dispatched by the orchestrator: analyst panel on US-customer-access-instructions, then android + paired reviewer. **Now also a lane head:** T-0450 and T-0448 both wait on this ticket's `values-*/strings.xml` writes.
- 2026-07-30 — **qa** — **REVIEWER APPROVED.** 321/321 tests, 53/53 Gradle tasks **executed** (not
  up-to-date/cached), no new consistency violations, and **both review findings closed and
  independently re-proved**. Owner moved to `qa`. **The only item still open is AC1's screenshot,
  which is QA's and was correctly deferred** — it is not a reviewer gap. The `values-*/strings.xml`
  lane is now clear for **T-0450**.
- 2026-07-30 — **now blocks T-0467** (Android booking draft lost on process death). Not a defect in
  this ticket — the reviewer rated it **Medium** and argued against inflating it, and the PM agrees:
  `BookingBottomSheet.kt:301` already calls `reset()` on every fresh non-rebook open, so persisting
  the draft is a **product behaviour change**, not a bug fix. **What this ticket changed is the
  stakes:** `accessInstructions` can hold a **key-box or alarm code**, so the constraint on any future
  persistence design ("never unencrypted at rest; clear on sign-out via `SessionScopedCache`, S11")
  went from theoretical to material. That constraint is written into T-0467 **up front**, so it is not
  discovered in its review.

## Ready-made parity wording — for the reviewer or QA, if wanted (added 2026-07-30)

The T-0440 (iOS) work produced these, **shaped to match the sibling hint** rather than the web's
longer label+placeholder pair, because the iOS field is **hint-only**. Offered for Android parity —
**not a change request**; adopt only if QA or the reviewer wants the alignment.

| Locale | String |
|---|---|
| `en` | How should we get in? (optional) |
| `cs` | Jak se dostaneme dovnitř? (nepovinné) |
| `sk` | Ako sa dostaneme dnu? (nepovinné) |
| `uk` | Як нам потрапити всередину? (необов'язково) |
| `ru` | Как нам попасть внутрь? (необязательно) |

**⚠️ If adopted, the `values-*/strings.xml` lane reopens** (this ticket is currently `qa` and the lane
was declared clear for **T-0450**). Tell the PM first — do not edit the bundles from `qa` without
re-serializing the lane.

## Review

### 2026-07-30 — REVIEWER: **APPROVED**

321/321 tests and 53/53 Gradle tasks executed; no new consistency violations; both findings raised
during review were closed and **independently re-proved**. AC1's screenshot remains open and belongs
to QA.

**Process note — the reviewer caught its own evidence being served from the Gradle build cache
mid-mutation and re-ran with `--no-build-cache`.** That was correct and load-bearing: *"it still
compiles"* was half the finding, and a cache-served compile does not establish it. **Gate 0.5 does not
currently name this case** — a mutation that reproduces a *previous* mutation byte-for-byte will
legitimately hit the cache, with the build system behaving perfectly correctly. **Filed as T-0468**
(architect + docs; `quality-gates.md` is not the PM's file).

**Catalog harvest — one sentence routed to the Architect, deliberately not acted on here.** This
ticket's `patterns-mobile.md` hunk closes with *"iOS mirrors this — its generated models have the same
all-optional shape."* The reviewer **verified the claim is factually true** (`CreateOrderCommand.swift:15-32`,
every property optional) but correctly noted it is an **Android-layer ticket writing toward a stack it
never executed**, and let it stand as **descriptive, not prescriptive**. That is the right call.
**The Architect confirms or promotes it once T-0440 lands with its own iOS evidence** — recorded in
`status/sprint-14.md` §2.10 so it is not lost when this ticket closes.
