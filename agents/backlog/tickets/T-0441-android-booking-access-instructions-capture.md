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
- 2026-07-30 — android: implemented on `feat/T-0441-sprint14`. Red→green recorded below.
- 2026-07-30 — android: review findings F1 (blocking) + F2 (minor) closed. F1 — added `BookingApiTest`
  covering the untested app→generated hop for **both** `accessInstructions` and `specialInstructions`;
  mutation-proved by deleting `BookingApi.kt:74` and `:73` in turn (both red, both restored byte-exact).
  F2 — the cap rationale was factually inverted (`CreateOrder` runs *before* PaymentSheet opens) and is
  reworded at `BookingDtos.kt`, `BookingViewModel.kt`, `BookingViewModelTest.kt`; the UTF-16 claim is
  corrected from "character-for-character" to "code-unit-for-code-unit" with the conservative-not-exact
  behaviour spelled out. Suite 320 → **321**, 0 failures.

## Implementation surface (iOS port reproduces this 1:1 — T-0440)

| Concern | Android landing point |
|---|---|
| State | `BookingState.accessInstructions: String = ""` (`BookingState.kt:56`) — in-memory only, same as every other booking field |
| Input write path | `BookingViewModel.updateAccessInstructions(text)` (`BookingViewModel.kt:294-300`) — clamps `text.take(2000)` |
| Cap constant | `BookingViewModel.ACCESS_INSTRUCTIONS_MAX_LENGTH = 2000` (`BookingViewModel.kt:575`), mirrors `CreateOrder` `.MaximumLength(2000)` |
| Wire mapping | `BookingViewModel.kt:467` → `s.accessInstructions.trim().ifBlank { null }` |
| App DTO | `CreateOrderCommand.accessInstructions: String?` (`BookingDtos.kt:127`) |
| Generated DTO | already present — `customer-app/build/generated/openapi/.../model/CreateOrderCommand.kt:110-111`, from the committed spec. **Nothing hand-written.** Mapped at `BookingApi.kt:74` |
| UI | `InstructionsFields` (`ConfirmStep.kt:418-438`) — a stateless pair (special + access), called at `ConfirmStep.kt:349`. Both are full-width `CleansiaTextField`s in a `Column`; **no weighted `Row`**, so the T-0442 weight-starvation class does not apply here |
| String | `booking_access_instructions_hint` ×5 locales |
| Navigation / API calls | **unchanged** — no new endpoint, no new route, no new state machine. `POST /Order/CreateOrder` gains one optional field |
| Wire-hop test | `BookingApiTest.create_carriesBothInstructionNotesOntoTheGeneratedCommand` — mocks the **generated** client, asserts the **generated** command. **Port this too:** the generated model's all-optional fields mean a dropped mapping is invisible to both the compiler and the ViewModel suite (mutation-proved below), and the iOS generated model has the same shape |

No new states, no new effects, no new screens. iOS parity = the same five landing points.

**Two parity traps for T-0440**, both found by review on this ticket:
1. Cap with a **UTF-16 code-unit** count, not Swift's grapheme-cluster `String.count` — see the AC3
   note under Evidence.
2. Assert the **generated** create command, not the app-level one. A test one hop short passes with the
   field never leaving the device.

## Evidence

**AC1** — field present, mirroring special instructions: `ConfirmStep.kt:429-437` (the access field is
the sibling of the special-instructions field in the same `InstructionsFields` composable). The two
inline `CleansiaTextField` calls were lifted into that one private stateless composable so the pair is
preview-renderable; behaviour of the existing special-instructions field is unchanged. Screenshot: QA.

**AC2** — trimmed on the wire, blank → null. `BookingViewModel.kt:467`.
- `BookingViewModelTest.kt:259 submit_givenAccessInstructions_sendsThemOnTheCreateCommand`
- `BookingViewModelTest.kt:297 submit_givenBlankAccessInstructions_sendsNull`
- `BookingApiTest.kt:47 create_carriesBothInstructionNotesOntoTheGeneratedCommand` — the **last hop**,
  covering both notes. The two VM tests capture the **app** DTO one hop before `toWire()`; this one
  mocks the **generated** `OrderApi` and asserts the captured **generated** `CreateOrderCommand`.
  Necessary because every field on the generated command defaults to `= null`, so a dropped mapping in
  `BookingApi.toWire()` compiles, ships silently, and leaves the VM suite green (see the mutation
  numbers under Gate 0.5 leg 1 — `BookingViewModelTest` stayed **24/24** under both mutations).

**AC3** — capped at 2000. `BookingViewModel.kt:298` + const at `:575`.
- `BookingViewModelTest.kt:338 updateAccessInstructions_whenLongerThanBackendLimit_capsAtMaxLength`
- `BookingViewModelTest.kt:350 updateAccessInstructions_whenWithinBackendLimit_keepsTextVerbatim`
  (guards over-eager truncation)

`.take(2000)` counts UTF-16 code units, which is exactly what .NET `string.Length` (and therefore
FluentValidation `MaximumLength`) counts — the client cap and the server cap agree
**code-unit-for-code-unit**, not character-for-character. Above the BMP the cap is *conservative*: 2000
emoji are `String.length == 4000`, so `.take(2000)` silently keeps 1000 of them, and a cut can split a
surrogate pair (the orphaned high surrogate encodes to `'?'` in UTF-8 — no crash, length preserved).
The cap can never be **more permissive** than `string.Length`, which is the only property that matters
here, so the behaviour stands. **iOS port note (T-0440):** mirror this with a UTF-16 count
(`prefix` over `utf16`), NOT Swift's grapheme-cluster `String.count` — that one *would* be more
permissive and would let the server reject at submit.

**AC4 — Gate 8 / Gate 0.5 leg 2 (a cached run is not a run).**
```
./gradlew :customer-app:compileDebugKotlin :customer-app:testDebugUnitTest --rerun-tasks --console=plain --no-daemon
BUILD SUCCESSFUL in 8m 20s — 53 actionable tasks: 53 executed, 0 up-to-date
:core:compileDebugKotlin           EXECUTED
:customer-app:compileDebugKotlin   EXECUTED
:customer-app:testDebugUnitTest    EXECUTED
customer-app testDebugUnitTest: tests=321 failures=0 errors=0 skipped=0
```
(**321**, up from 320 — the one new `BookingApiTest`.) The only `UP-TO-DATE` lines in the log are the
five empty lifecycle anchors — `:customer-app:preBuild`, `:core:preBuild`,
`:customer-app:preDebugBuild`, `:customer-app:preDebugUnitTestBuild`, `:core:preDebugBuild` — which
have no work to do. `:core:compileDebugKotlin` executed (it is upstream of
`:customer-app:compileDebugKotlin`); `:core:testDebugUnitTest` was NOT run — nothing in `:core` changed.

**Gate 0.5 leg 1 — mutation proof (both numbers).**
- Pre-implementation (VM method stubbed without `.take`, submit not wired):
  `updateAccessInstructions_whenLongerThanBackendLimit_capsAtMaxLength` **FAILED** and
  `submit_givenAccessInstructions_sendsThemOnTheCreateCommand` **FAILED** — `24 tests completed, 2 failed`.
- `submit_givenBlankAccessInstructions_sendsNull` passed **vacuously** in that state (the DTO field
  defaults to `null`), so it was mutation-proved separately: replacing `BookingViewModel.kt:467` with
  a bare `accessInstructions = s.accessInstructions` turned **both** wire tests red —
  `24 tests completed, 2 failed`.
- Restored: `24 tests completed, 0 failed`; the restore is **byte-exact** (`git diff` sha256 identical
  before mutation and after restore).

**Gate 0.5 leg 1 — mutation proof for the wire hop (`BookingApiTest`, review finding F1).**
The review reproduced the gap: deleting `BookingApi.kt:74` left the suite **24/24 green and still
compiling**, because the generated `CreateOrderCommand.accessInstructions` carries a `= null` default.
Both hops are now covered and both were re-run un-cached (`--rerun-tasks`, 53 executed each):

| Mutation | Result | `BookingApiTest` | `BookingViewModelTest` |
|---|---|---|---|
| delete `BookingApi.kt:74` (`accessInstructions`) | `321 tests completed, 1 failed` — BUILD FAILED | FAILED: `expected:<Side gate, key box code 4417.> but was:<null>` | 24/24 **green** |
| delete `BookingApi.kt:73` (`specialInstructions`) | `321 tests completed, 1 failed` — BUILD FAILED | FAILED: `expected:<Gate code 1234, dog is friendly.> but was:<null>` | 24/24 **green** |
| restored | `321 tests completed, 0 failed` — BUILD SUCCESSFUL | 1/1 green | 24/24 green |

`:customer-app:compileDebugKotlin` **succeeded under both mutations** — that is the whole point of the
finding: the defect is invisible to the compiler and to the VM suite. Restore is **byte-exact** —
`BookingApi.kt` sha256 `a9399594…f973ec` and its `git diff` sha256 `281b0dfe…5eb9d0` are identical
before the first mutation and after the last restore.

**AC5 — byte-level encoding.** All 12 changed files checked as bytes (11 + the new `BookingApiTest.kt`):
no UTF-8 BOM, no CRLF, decodes as valid UTF-8, no mojibake sequences (`Ã`, `Ð`, `â\x80`, `ï»¿`, U+FFFD).
The five `strings.xml` parse as XML and carry **identical key sets — 1046 keys each, zero missing, zero
extra**. Re-checked after the F1/F2 edits: `BookingApiTest.kt`, `BookingDtos.kt`, `BookingViewModel.kt`,
`BookingViewModelTest.kt` all clean.

**Gate 8 consistency:** `check-consistency.mjs --paths=src/cleansia_android/customer-app` → 11
violations, **identical to the pre-change baseline** (all in `AuthViewModel.kt` / `RewardsTab.kt` /
`CleansiaNavHost.kt`, none in a file this ticket touched). No new violation. Re-run after F1/F2: still 11.

## Notes for the PM

- **Draft does not survive process death — and did not before this ticket either.** `BookingState`
  lives only in `BookingViewModel`'s in-memory `MutableStateFlow` (`BookingViewModel.kt:156`): no
  `SavedStateHandle`, no `@Parcelize`, no DataStore, no `rememberSaveable` anywhere in
  `features/booking/`. `specialInstructions`, `promoCode`, the address and the picked slot are all
  lost on process death today. `accessInstructions` inherits exactly that behaviour, which is correct
  parity with its template. **There is no existing draft-persistence test to extend.** If the platform
  wants booking drafts to survive process death that is a separate ticket covering the whole
  `BookingState`, not this field.
- **`:core` untouched.** `CleansiaTextField` has no `maxLength` parameter and I deliberately did not
  add one — the cap belongs on the ViewModel (where it is unit-testable and mutation-provable), and
  `:core` is a serialized lane this ticket has no claim on.
- **One deliberate divergence from the `specialInstructions` template**, flagged for the reviewer: the
  access field writes through a named VM function (`bookingVm::updateAccessInstructions`) instead of
  the generic `onUpdate(state.copy(...))` the other fields use. AC3's cap is logic, and logic in an
  `onValueChange` lambda is (a) business logic in a composable and (b) not reachable from a JVM unit
  test. `specialInstructions` keeps its existing `onUpdate` path untouched — it has no cap to enforce.

## Review

**Dev harvest note (android, F1 close-out — not a verdict).** F1 is a repeatable bug class, not a
one-off miss, so per the charter's harvest rule the idiom is folded back into
`agents/knowledge/patterns-mobile.md` → "Networking & Repository — exact idiom": *when an Api adapter
maps an app DTO onto an OpenAPI-generated command, assert the **generated** command — every generated
field defaults to `= null`, so a dropped `toWire()` line compiles and leaves an adapter-mocking
ViewModel test green.* It is a **clarification to the existing adapter pattern**, not a new "one way to
do X" — `consistency.md` was deliberately left alone (serialized lane, and promoting this to a numbered
E-entry is an Architect call). Flagging it in case the Architect wants it as an E-entry with checker
support.

<!-- reviewer writes verdict here -->
