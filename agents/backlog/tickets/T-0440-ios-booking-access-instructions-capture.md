---
id: T-0440
title: iOS — capture entry/access instructions on the booking confirm step
status: qa
size: S
owner: qa
created: 2026-07-30
updated: 2026-08-01
depends_on: []
blocks: [T-0450, T-0449, T-0469]
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
- **i18n (verified 2026-07-30; CORRECTED 2026-07-30 — see below):** ~~the *display* keys already
  exist and must be reused, not duplicated —~~ `L10n.OrderDetail.accessInstructions`
  (`CleansiaCustomer/Sources/L10n+Orders.swift:148`) **exists, but it is the partner/detail DISPLAY
  label. Do NOT reuse it for the confirm-step hint** — "reused, not duplicated" was the wrong
  instruction and sent readers chasing a key that does not fit.
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

- 2026-07-30 — **in_progress** — dispatched by the orchestrator: analyst panel on US-customer-access-instructions, then ios + paired reviewer. **Now also a lane head:** T-0450 and T-0449 both wait on this ticket's `Localizable.xcstrings` write.
- 2026-07-30 — ~~**`manual_steps: [ios-client-regen]` ADDED.**~~ **RETRACTED the same day — the
  blocker was false. See the correction block below. `manual_steps` is back to `[]` and this ticket is
  NOT owner-gated.**

## ❌ RETRACTED 2026-07-30 — the `ios-client-regen` blocker was FALSE. Do not re-derive it

**This block is kept deliberately instead of deleting the row.** An agent that reads the old note —
or that repeats the same read I did — will re-derive the same false blocker and stall this ticket
again. Here is exactly why it is wrong.

**What I claimed:** iOS's generated models are committed, `accessInstructions` is absent from
`CreateOrderCommand.swift`, therefore the owner must regenerate.

**Why it is wrong — four independent checks, all run by the PM this time:**

| Check | Result |
|---|---|
| `git ls-files src/cleansia_ios/CleansiaCustomerApi` | **0 files. Never committed.** |
| `src/cleansia_ios/.gitignore` | ignores `CleansiaCustomerApi/` **and** `CleansiaPartnerApi/`, under the comment *"openapi-generator output — machine-owned, never committed, never hand-edited (regenerate with `scripts/generate-api-clients.sh`)"* |
| local `CreateOrderCommand.swift` timestamp | **Jul 25 22:35** — generated **before** the spec gained the field |
| `accessInstructions` in the **committed** spec at `HEAD` | **present** in `src/cleansia_android/openapi/customer-mobile-api.json` |

**The generated client is a machine-owned local build artifact, not repo state.** Regenerating it is
`./scripts/generate-api-clients.sh` — **offline codegen from the committed spec, authorised for
agents** — and is *not* the owner-only NSwag step. The developer ran it, got the field at
`CreateOrderCommand.swift:34`, and **694 tests compile and pass** with it set.

**The lesson, recorded because it is the reusable part:** I read the file, confirmed the field was
absent, and stamped it "PM-verified". The read was accurate; **the artifact was not repo truth.** I
checked *existence and content* and never checked *tracked status* — one `git ls-files` would have
caught it. **Reading an untracked, gitignored build artifact and reporting it as a repo fact is not
verification.** Any claim about "what the repo contains" must be grounded in a `git`-tracked path.

**Most damning: this ticket already said so, at lines 34-39**, in a warning written before any of
this — *"The working copy on this machine is STALE… Run `./scripts/generate-api-clients.sh` before
starting, or the field will appear not to exist."* The trap was documented, and I walked into it and
then contradicted the ticket's own correct warning. **Read lines 34-39 before touching this ticket.**

**Net effect: nothing about this ticket is owner-gated.** It proceeds in full — UI, strings, the
`Localizable.xcstrings` lane head that T-0450 and T-0449 wait on, and the model-dependent work alike.

**Also open, routed to the Architect (from the T-0441 review):** T-0441's `patterns-mobile.md` harvest
asserts *"iOS mirrors this — its generated models have the same all-optional shape."* The reviewer
verified it is **factually true** (`CreateOrderCommand.swift:15-32`, every property optional) but
correctly left it **descriptive, not prescriptive**, since an Android ticket wrote it toward a stack it
never executed. **When this ticket lands with real iOS evidence, the Architect confirms or promotes
it.** Do not silently promote it from inside this ticket.

## 🚦 2026-08-01 — APPROVED → `qa`. THREE items still open before `done`

Reviewer verdict is in this file's `## Review` and committed as **`c23b26e7`**.
**⚠️ Provenance:** that commit is on **`fix/tooling-false-green-and-broken-docs`**, **not on
`master`** (PM-verified: `git merge-base --is-ancestor` → not an ancestor of `master`). Do not look
for it in `master`'s history yet.

### F-3 — test-first ordering must be RECORDED before this ticket reaches `done` (developer)

Test-first ordering is **unverifiable from the artifact**: one squashed commit, and **no `red→green`
entry in the status log**. The reviewer **explicitly declined to assert the tests were written after
the fact**, and noted its own mutations substantively cover what TDD protects against here — so this
is a **traceability gap, not a quality finding**, and it is recorded as such.

- [ ] **The developer records the actual ordering in the status log.** If it turns out to have been
      implementation-first, **that becomes a real Gate 6 question and the reviewer re-reviews.** Do
      not close this by asserting compliance retroactively — the point of the record is that it can be
      wrong.

### Open at QA — a genuine handoff, NOT a deferral (per the T-0441 precedent)

The reviewer **proved** these cannot be captured in-suite: it hosted the field in a **real window** and
captured through **two independent mechanisms**, and **both came back blank**. That is evidence of
impossibility, not an untried claim — so this is a real handoff.

- [ ] **AC1 screenshot** — reachable only by driving the real app on a **16.4 device**.
- [ ] **Gate 8.5 render leg** — same constraint.
- [ ] **While QA is on the device:** does the **ru/uk two-line placeholder** read as *intentional*
      beside the sibling's one line? A judgement call that needs eyes on a real screen, not a rule.

### ⛔ Do NOT re-apply the "hint no longer than its sibling" constraint

The reviewer **refuted** it for iOS: Android's float label **ellipsizes**, but iOS's hint is **plain
wrapping text with no line limit**, in a container with **ample headroom**. The constraint was an
Android-shaped rule generalized to a platform where its premise does not hold.

**It must not be carried into T-0449 or T-0450** — both have been given the same note. (PM-verified
2026-08-01: neither ticket currently carries it, so this is prevention, not removal.)

## Review

**VERDICT: APPROVED** (reviewer, 2026-08-01, branch head `94641945`). Do **not** mark `done` until
F-3 is recorded, AC1 + the Gate 8.5 render leg close at QA, and T-0469 rules on F-1 and the
`specialInstructions` divergence.

**The previous reviewer's owner-regen claim is REFUTED on all four legs, re-run independently:**
`git ls-files src/cleansia_ios/CleansiaCustomerApi` → 0; `.gitignore:12-15` ignores it as
machine-owned; the main-tree copy is dated Jul 25, before the spec gained the field; and the
committed spec at `ce2416a0` already carries `accessInstructions`. Running the generator produced
`accessInstructions: String?` at `CreateOrderCommand.swift:34`. Not owner-gated; `manual_steps`
correctly `[]`.

**The UTF-16 cap is provably total, not merely tested.** The reviewer extracted `capped(_:)` into a
standalone `swiftc -O` harness and fuzzed 4000 notes across every UTF-16 width class (1-unit
ASCII/Cyrillic/CJK, combining pairs, surrogate pairs, 4-unit skin-tone and regional-indicator flags,
11-unit ZWJ families, Devanagari, bare ZWJ, bare VS16), biased onto the boundary, plus a targeted
sweep of `pad ∈ 0…12` × 6 cluster kinds forcing the cut inside every cluster width. **2464
truncating cases, 332 exact-fit, 0 failures.** Proven: output never exceeds 2000 UTF-16 units; no
`U+FFFD` ever introduced; lossless UTF-16 round-trip (so no lone surrogate half survives); every
output Character equals its input Character (no cluster halved); and maximality.

**Divergence #1 — dropping the character whole rather than Android's `.take(2000)` — is upheld, and
convergence should go Android → iOS.** Kotlin's `take` slices UTF-16 chars, so `"a"×1999 + "🔑"`
leaves a **lone high surrogate**. iOS's cost is ≤10 unused units of a 2000-unit budget; Android's is
a broken glyph at the end of a field that can hold a gate or key-box code. Parity that copies a
defect is not parity. (What Android's serializer emits for the orphan is UNVERIFIED-LOCALLY — that
leg belongs to T-0469, not to this verdict.)

**Divergence #2 — `specialInstructions` capped here, not on Android — escalates to T-0469 part B, and
does not block this ticket.** The situations genuinely differ: iOS *had* to touch that write path
(generalizing `SpecialInstructionsField` → `InstructionsField` forced the inline view write behind a
VM setter), so leaving it uncapped would have meant authoring a deliberately asymmetric setter pair.
And T-0441's stated objection — no test to pin it — is answered here by
`testSpecialInstructionsAreCappedAtTheBackendLimit`, proven red against a stub. **New input for the
panel:** the sharper problem is *within* Android, not across platforms — it caps one field and not
its neighbour **on the same screen**, which is strictly worse than a cross-platform difference and is
the strongest argument for "cap both, everywhere".

**Mutations re-run by the reviewer.** Deleting the wire mapper line: `** BUILD SUCCEEDED **`,
694 tests / **2 failures** — `testAccessInstructionsAreSentOnTheCreateCommand` and
`testTheTwoNotesTravelOnTheirOwnFields`, both assertion failures, not compile errors. A second
mutation the developer did not report — stubbing `capped(_:)` to `{ value }` — gave 694 tests / 13
failures across 8 cases. **Gate 6.5 named test:** `testTruncationNeverSplitsAZwjSequence`. Restore
byte-exact; full suite green after.

**Gate 8 / 8.5, all run by the reviewer:** CleansiaCustomer **694/694 on iOS 26.3** and **694/694 on
the 16.4 floor** (`iPhone 14 Pro`, deliberately a different device from the sibling worktree's
booted `iPhone 14`), CleansiaCore **514/514**, SwiftFormat 0.60.1 `0/659`, SwiftLint 0.65.0
`--strict` 0 violations. No failure observed was simulator contention.

**AC1 is open, and the reviewer proved it cannot be closed in-suite.** It wrote a probe hosting
`InstructionsField` in a real `UIWindow` and captured via **both** `drawHierarchy(afterScreenUpdates:)`
and `window.layer.render(in:)` — the second mechanism the developer had not tried. All four PNGs came
back `distinctColors=1`, byte-identical across empty and filled. `InstructionsField` is never
instantiated by any test. Instead it measured the wrapping analytically: at the narrowest 16.4 device
(375pt) minus the step's 24pt padding ×2 and the hint's 16pt ×2 = **295pt** available in a ≥96pt
container; en/cs/sk wrap to 1 line, ru/uk to 2 (37pt). Ample headroom.

**The "hint longer than its sibling" constraint from T-0441 does NOT transfer, and was REFUTED as a
finding.** cs/ru/uk hints are longer (37/39/43 vs 32/33/35) — but Android's field is a Material float
label that ellipsizes, while iOS's is a plain wrapping `Text` in a `ZStack` with no `lineLimit` and no
`truncationMode`. It wraps; it cannot truncate. Only residue is the cosmetic 1-vs-2-line asymmetry for
ru/uk, for QA's eye. Note: uk ships `необов'язково` with a typographic apostrophe (U+2019) — that is
the **correct** Ukrainian form; do not "fix" it to the straight quote in T-0441's parity table.

**i18n lane discipline is exact:** one key added, five locales, zero existing keys touched, zero
removals, top-level metadata unchanged. The lane is clear for T-0450 and T-0449.

**The "after payment details are entered" framing exists only in web** (`order-wizard.component.html:491`
and `:658`) and nowhere in iOS. The replacement wording is accurate for iOS's own submit order:
`BookingViewModel+Submit.swift:46` creates the order and the payment sheet only runs after it exists,
so a `MaxLength` rejection lands before the card sheet — no money taken.

### Findings

- **F-1 (Medium → T-0469, not a code change blocked on here).** Two adjacent, visually identical,
  **unlabeled** boxes. The hint disappears the moment the field is non-empty
  (`ConfirmExtrasComponents.swift:10`) and there is no `.accessibilityLabel` on the `TextEditor` at
  `:17`; both are rendered back-to-back in a bare `VStack` with no section headers. Fill both, scroll
  away and back, and nothing on screen says which is which — and VoiceOver reads two unnamed text
  fields. Android's float label persists, and iOS's *own* Core `CleansiaTextField.swift:47` keeps the
  label floating when non-empty. AC1 as literally worded is satisfied, and the ambiguity did not exist
  when there was one box. Cheapest fix is one line (`.accessibilityLabel(hint)`), which also closes
  the VoiceOver gap; the same file already uses `.accessibilityLabel` at `:92`.
- **F-2 (Low).** A real WHY comment was dropped and not rehomed: the old factory explained that the
  editor binds straight to state, so tapping in and back out leaves whitespace behind — hence
  normalise to nil. `trimmedOrNil` now carries no doc comment; the reason survives only in a test name.
- **F-3 (Gate 6 evidence — must close before `done`).** Test-first ordering is **unverifiable from the
  artifact**: one squashed commit, no `red→green` status-log entry. The reviewer explicitly declined to
  assert it was written after the fact, and notes its own mutation runs substantively answer what TDD
  protects against here — the tests are behaviourally binding and cover exactly the branches an
  implementation-first author would most plausibly miss (exact-limit, surrogate boundary, ZWJ,
  combining marks). **The developer records the ordering; if it was implementation-first, that becomes
  a real Gate 6 question.**
- **F-4 (tooling → Architect).** `check-consistency.mjs --paths=src/cleansia_ios/CleansiaCustomer`
  reports `OK (0 files scanned)`. The walker is invoked only with `.cs`, `.ts`, `.kt` — **there is no
  `.swift` coverage at all**. Per Gate 0.5 leg 2 that is a **non-run, not a pass**, so Gate 8's
  consistency leg for iOS must be recorded UNVERIFIED, never PASS. Every `layers: [ios]` ticket has
  been silently recording a vacuous green. This is the same enforcement gap ADR-0032 addresses.

### Still open at QA / the owner

1. AC1 screenshot of the confirm step, both fields empty and filled, on a 16.4 device — reachable only
   by driving the real app (sign-in → booking sheet → confirm) and `xcrun simctl io <udid> screenshot`.
   Same precedent as T-0441.
2. The Gate 8.5 render leg — same session, one screen; the diff introduces no new navigation.
3. While there: confirm the ru/uk two-line placeholder reads as intentional beside the sibling's one
   line, and whether a >2000 paste momentarily shows uncapped text before SwiftUI re-renders (a known
   `TextEditor`-binding quirk — state and the wire are provably correct either way).
4. *Optional, its own ticket:* an XCUITest target would retire this entire class of missing evidence.
