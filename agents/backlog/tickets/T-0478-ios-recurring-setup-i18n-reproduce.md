---
id: T-0478
title: iOS recurring setup — reproduce the owner's "no translations" report, then fix what it actually is
status: draft
size: S
owner: ios
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #2 (2026-08-02):** *"No translations for the recurring-booking setup, both mobile
apps."* Android's half is **T-0477** and has a located cause. **iOS's does not**, and this ticket is
honest about that rather than inventing a fix.

### What the PM checked and could NOT reproduce, on `master` at `0e4ede1b`

| Check | Result |
|---|---|
| `recurring*` keys in `CleansiaCustomer/Resources/Localizable.xcstrings` | **46** |
| locales carried by each of those 46 | **`cs`, `en`, `ru`, `sk`, `uk` — all five, on all 46, zero exceptions** |
| hardcoded user-visible literals in `CreateRecurringScreen.swift` | **none.** The only string literals in the file are two SF Symbol names (`"checkmark.circle.fill"`, `"circle"` at `:233`) |
| hardcoded literals in `RecurringBookingsScreen.swift` | **none** |
| catalog service/package names | **correctly localized** — `CreateRecurringScreen.swift:167`/`:180` use `localizedName(for: locale)`; this is the *reference* implementation Android is missing (T-0477) |

**So the two obvious causes are both excluded on iOS.** Whatever the owner saw is a third thing, and
guessing at it would produce a fix for a defect that is not there.

### The three candidates the investigator should test first, in this order

1. **A key that exists in the bundle but resolves to `en` at runtime.** `L10n.localized(_:)` lives at
   `CleansiaCustomer/Sources/L10n.swift:50`. If it resolves against `Bundle.main` while the app
   carries a **per-app language preference** that differs from the device locale, every screen would
   be wrong — so test whether the recurring screens are *uniquely* wrong or merely the screen the
   owner happened to be on. **If the whole app is wrong in this mode, this ticket is mis-scoped and
   should be re-filed platform-wide.**
2. **`@Environment(\.locale)`.** `ServicesSection` (`CreateRecurringScreen.swift:150`) reads
   `@Environment(\.locale)` for the catalog lookup. On a **pushed** `NavigationStack` destination the
   environment locale is whatever the host set — if the app overrides language via a `.environment`
   injection at the root and the recurring wizard is presented outside that subtree, the catalog names
   fall back to English **while the chrome stays translated**. That produces exactly a
   "half-translated screen".
3. **The screen the owner meant may be a different one.** iOS's recurring setup is a single-page form;
   Android's is a 3-step wizard. Confirm with the owner *which* screen and which language before
   spending the fix.

## Acceptance criteria

- [ ] **AC1 — REPRODUCE FIRST, and record the negative if it does not reproduce.** Given the app in
      `cs` and in `ru`, When the recurring setup is opened, Then the investigator records a screenshot
      per language and states plainly which strings render untranslated — **or that none do.** A
      "could not reproduce" verdict with these two screenshots **closes this ticket successfully**;
      it does not fail it. Evidence: two screenshots per language, chrome and catalog both visible.
- [ ] **AC2 — the mechanism is named before anything is changed.** If AC1 reproduces, the verdict
      names the cause at file:line and says which of the three candidates it was (or a fourth).
      **No fix lands without this line.**
- [ ] **AC3 — the fix is scoped to the mechanism, not to the screen.** If the cause is
      environment/locale resolution, then the fix is at the resolution site and the verdict states
      **every other screen it also fixes**. A per-screen patch on a platform-wide resolution bug is
      rejected. Evidence: the diff plus the named blast radius.
- [ ] **AC4 — a test that goes red against the pre-fix code (Gate 0.5 leg 1)**, if AC1 reproduces.
      If it does not, say so under leg 3 — there is nothing to mutate.
- [ ] **AC5 (Gate 0.5)** — `xcodebuild build test` for `CleansiaCustomer` on the **16.4 floor**,
      SwiftFormat `--lint` + SwiftLint `--strict`, with an honest statement of whether the app-scheme
      tests compiled and ran.

## Out of scope

- **Android** — T-0477, which is a confirmed and different defect.
- **The 19 `recurring_*` keys Android has and iOS does not**, and the single-page-vs-wizard shape
  difference behind them. **PM-measured**, real, and routed to **T-0481** (the parity audit). It is
  a feature-parity gap, not a localization gap — the strings iOS *has* are all translated.
- **Adding the missing keys.** They belong to whatever T-0481 decides about the wizard's shape.

## Implementation notes

**No panel — this is an investigation with a conditional mechanical fix.** If AC2 lands on something
architectural (a platform-wide locale-resolution defect), **stop and re-file**; do not grow this into
an `M` in place.

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`** — they carry the owner's live
Stripe key. Nothing here needs them.

**Before starting:** run `src/cleansia_ios/scripts/generate-api-clients.sh` and `xcodegen generate` in
both app dirs (the standing post-checkout trap — **T-0474**). A stale client has cost this backlog a
false conclusion once already.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #2).** Filed as reproduce-then-fix
  rather than as a fix, because **the PM could not find the defect**: all 46 `recurring*` keys carry
  all five locales, there are no hardcoded literals in either screen, and the catalog names are
  correctly localized — iOS is the *reference* for the bug T-0477 fixes on Android. Writing invented
  acceptance criteria for a cause nobody has located is exactly what Gate 0.5 exists to stop.

## Review
