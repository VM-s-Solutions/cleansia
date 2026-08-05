---
id: T-0498
title: Plus card — iOS is one view away from parity; Android's perk pills are hardcoded English and one renders unconditionally
status: draft
size: S
owner: android
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0491]
blocks: []
stories: []
adrs: [0018]
layers: [ios, android]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Cleansia Plus audit (2026-08-02).** Two client findings that must be fixed **together**,
because the obvious move — port Android's card to iOS — copies two bugs.

**Status: RELAYED, NOT re-verified by the PM.**

### The audit's finding, and why the sequencing matters

| Platform | Finding |
|---|---|
| **iOS** | The Plus card is **one view ticket** — *the data is already on the model.* No backend work, no DTO change, no regen. |
| **Android** | The perk pills are **hardcoded English** — they bypass `strings.xml` entirely, so a Czech or Russian subscriber reads their perks in English. And the **"Recurring" pill renders unconditionally**, i.e. it is shown to customers who do not have Plus. |

**"Port Android's card to iOS" would ship hardcoded English into a second app and a wrong pill with
it.** Filing these as one ticket with the trap written down is the only way that does not happen —
which is exactly why the audit flagged it.

**The Android hardcoded-strings finding is the more serious of the two**, and it is a repeat of a
pattern this backlog has already fixed once: **T-0477** (filed today) fixes the recurring wizard
rendering catalog names unlocalized. Two localization bypasses on the same feature area suggests the
sweep in T-0477 AC5 should look here too. Recorded on both.

### The dependency that is not optional

**`depends_on: [T-0491]`.** The perk pills are the customer-facing copy for the five perks — they are
literally **T-0491 AC1's evidence**. Three of those perks are unenforced (T-0493, T-0494, T-0495) and
one is worth 0 Kč at the top tier (T-0492). **Rendering a nicer card that lists five perks, three of
which the platform does not deliver, makes the misrepresentation more prominent, not less.** The card
should say what is true, and T-0491 decides what that is.

## Acceptance criteria

- [ ] **AC1 — the two Android bugs are RE-ESTABLISHED at file:line before either is fixed**, and the
      *"renders unconditionally"* claim is stated as a concrete case: *"a customer with no active
      membership sees pill X."* Evidence: the file:line plus the reproduction.
- [ ] **AC2 — the Android perk pills are localized.** Every pill label comes from `strings.xml` with
      **all five locales** populated — `values`, `values-cs`, `values-sk`, `values-uk`, `values-ru`.
      **The app's invariant is exact parity across all five** (PM-verified: **1052 `<string>` entries
      in each of the five files, zero drift**). Adding four keys to `values/` and not to the other
      four breaks an invariant the whole app currently holds. Evidence: the five-way count, before
      and after.
- [ ] **AC3 — the "Recurring" pill's visibility condition is correct**, and the condition is stated:
      does it render for members only, for everyone as an upsell, or is it status-dependent? A pill
      that is *deliberately* shown to non-members as an advertisement is defensible — it just must be
      **deliberate**, and it must not look like an entitlement the customer already has. Evidence:
      the stated condition plus a screenshot for both member and non-member.
- [ ] **AC4 — the iOS card reaches parity, and does NOT inherit either bug.** All strings via
      `Localizable.xcstrings` with **`cs`, `en`, `ru`, `sk`, `uk`** — the file's existing invariant.
      Visibility conditions match AC3. Evidence: the parity check over the new keys, plus side-by-side
      screenshots of both platforms in one non-English locale.
- [ ] **AC5 — the pills say what T-0491 rules is TRUE.** If T-0491 concludes a perk is withdrawn or
      re-scoped, the card reflects that. **A card that advertises a perk the platform does not deliver
      does not pass this AC**, however pretty it is. Evidence: the pill list checked against T-0491's
      ruling, item by item.
- [ ] **AC6 — no backend change, no DTO change, no regen.** The audit's finding is that the iOS data
      is already on the model. If that turns out to be false, **stop and report** — this stops being
      `S` and acquires `manual_steps: nswag-regen` + `mobile-spec-redump`, which is the owner's
      bundle and is not something an `S` client ticket absorbs. Evidence: `git diff --stat` confined
      to the two client trees.
- [ ] **AC7 — a test that goes red against the pre-fix code (Gate 0.5 leg 1)**, on both platforms:
      Android, a guard proving a hardcoded literal is no longer present and the pill's condition
      holds; iOS, the key-parity assertion. Evidence: the red runs, then green.
- [ ] **AC8 (Gate 0.5)** — Android `:customer-app` compile + `testDebugUnitTest` **un-cached**
      (`--rerun-tasks --no-build-cache`); iOS `xcodebuild build test` for `CleansiaCustomer` on the
      **16.4 floor** + SwiftFormat `--lint` / SwiftLint `--strict`, with an honest statement of
      whether the app-scheme tests compiled and ran.

## Out of scope

- **Enforcing any perk** — T-0492, T-0493, T-0494, T-0495. This ticket changes what is **displayed**.
- **The subscribe/checkout flow.**
- **Web.** Not named in the audit. If the same hardcoded pills exist there, **record it in
  `## Review`** — do not widen.
- **The `recurring_plus_gate_*` keys** (iOS has three, Android has none). Related, and part of
  **T-0494**'s and **T-0481**'s territory. Record if touched; do not fix here.

## Implementation notes

**No panel of its own — T-0491 is the panel**, and AC5 makes its ruling this ticket's content check.

**Fan-out: two developer instances in parallel, one reviewer each** — the trees are disjoint. **But
both read the same AC3 ruling**, so the Android instance settles the visibility condition first and
the iOS instance consumes it. Do not let two instances independently decide when a pill shows.

**Shared-file lanes:** `values*/strings.xml` (×5) and `Localizable.xcstrings` are **both serialized
lanes** (`process/shared-file-lanes.md`). Sprint-15 has several i18n writers — check the lane before
either edit. `Localizable.xcstrings` is additionally in the owner's uncommitted-file set; coordinate.

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`.**

**Before the iOS leg:** `src/cleansia_ios/scripts/generate-api-clients.sh` + `xcodegen generate` in
both app dirs (**T-0474**).

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** Filed as **one** ticket rather
  than two, specifically because the natural implementation — porting Android's card to iOS — copies a
  hardcoded-English bug and an unconditional pill into a second app. That trap is written into AC4.
  **`depends_on: [T-0491]` is not optional:** three of the five perks this card advertises are
  currently unenforced, and shipping a better-looking card for them makes the misrepresentation more
  prominent. Findings marked RELAYED, not PM-verified; AC1 re-establishes them.
- 2026-08-05 — **AC1 re-established and BOTH relayed Android findings are REFUTED at head.** Both were
  fixed by `248ee566` / `24af741e`. The Android leg needs no code change; verified and mutation-proved
  below. The iOS leg (AC4) was not touched — a separate lane owns that tree.

## Review — android (2026-08-05)

**AC1 — the two Android findings are re-established, and both are FALSE at head.** The audit was
relayed, not PM-verified, and it has since been overtaken by shipped work.

| Relayed finding | Verdict at head | Evidence |
|---|---|---|
| "the perk pills are hardcoded English" | **Refuted.** Every pill label is a `stringResource`. | `MembershipManagementCard.kt:472-487` — all six arms |
| "the Recurring pill renders unconditionally, i.e. shown to customers who do not have Plus" | **Refuted, twice over.** | see below |

The unconditional-pill claim fails on two independent gates. `MembershipPerks.resolve` returns
`emptyList()` when `!membership.hasMembership` (`MembershipPerks.kt:34`), so the perk list is empty for
a non-member; and the row that draws it is inside the **active** card behind
`if (perks.isNotEmpty())` (`MembershipManagementCard.kt:366-377`). The concrete case AC1 asks for —
*"a customer with no active membership sees pill X"* — **cannot occur**: there is no pill, and no perk
row. `MembershipPerksTest:17` already pins the first gate.

**AC2 — five-way parity, before and after.** The six `membership_perk_pill_*` keys are present in all
five locales. Counts: `values` / `-cs` / `-sk` / `-uk` / `-ru` = **1089 each before, 1090 each after**
— the +1 is the unrelated `error_recurring_booking_membership_required` row this batch adds, applied to
all five. Zero drift in either direction.

**AC3 — the visibility condition, stated.** **Members only**, and deliberately so. The pills describe
what an active membership has *already* unlocked, so they are an entitlement list and not an upsell;
the non-member surface is the separate inactive card / `SubscribePlusScreen`. Nothing changed here —
this is a statement of the shipped condition, which is the correct one.

**The perk wording is a recorded, intentional divergence — NOT fixed.** Android's pills are terse
native variants (`Recurring` / `Pravidelně` / `Регулярно`; `Express waived · %1$d left`) because they
are chips in a `FlowRow` whose siblings are two words long, while the prose surfaces carry web's copy
verbatim. Flagged explicitly because "make the pills match web" looks like a parity fix and is a
regression.

**AC4 (iOS), AC5 (T-0491's ruling), AC6 (diff confined to client trees), AC8 (iOS half) — NOT DONE.**
AC4 is out of this lane: an iOS lane is live and `src/cleansia_ios/` was not opened. AC5 cannot be
closed here — **T-0491 has not ruled**, and this ticket `depends_on` it; what can be said is that the
Android card's content did not change, so no new claim was added to it. **The dependency's substance is
unaffected by this verdict:** the pills still advertise perks whose enforcement is tracked by T-0492 /
T-0493 / T-0495, and the card is only as true as T-0491 makes it.

**Web (Out-of-scope asks for a record, not a widening):** not inspected.

**AC7/AC8 (Gate 0.5) — Android half, un-cached.** `:customer-app` compile + `testDebugUnitTest`,
`--rerun-tasks --no-build-cache`: **BUILD SUCCESSFUL, exit 0, 53 actionable tasks: 53 executed**, zero
`FROM-CACHE`. **508 tests / 57 classes / 0 failures**, from the JUnit XML. Named mutation: replacing
`stringResource(R.string.membership_perk_pill_recurring)` with the literal `"Recurring"` reddens
**exactly one** test — `MembershipPerkPillBindingTest.the card spells no perk label itself` (6 tests,
1 failed) — and nothing else moved, so the guard that would have caught the original defect is proved
live rather than assumed. Restored byte-exact by md5 (`0da8bd71c9df672f07b4d3d070a76253` before and
after; `git status` shows the file unmodified).
