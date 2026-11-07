---
id: T-0917
title: iOS long-term refactor — shared Core pieces, one locale source, lag fixes and build health
size: L
owner: Michael Chaban
created: 2026-11-07
updated: 2026-11-07
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
---

## Context

Mike, 2026-11-07: "Don't do the quick fixes, I want you to do the fixes that actually matter and win in long term.
Refactor the code, polish it, review it from technical side as well if it's optimized". He then agreed the scope
below. This is the second of three pull requests in that programme; the first is the partner crash fix (PR #366,
T-0914 to T-0916). This one is iOS only.

Every item comes from the verified audit of 2026-11-07 (`audit-20261107/report/verified-flat.txt` in the work area).
Where the verifier corrected an auditor, the verifier's note wins.

## Scope

- **Build health.** ios-perf-08: resolve Stripe from the slim `stripe-ios-spm` package, still at 25.17.0.
  ios-perf-07: the four slow type-check sites. IOS-CORE-06: the literal-key `Text` and labels that feed junk keys
  into the string catalogs, plus a guard test that keeps them out.
- **Core moves, part 1.** IOS-CORE-01: the booking slide's VoiceOver action, in Core. IOS-CORE-08: the preference
  picker list, the language labels and `Theme.colorScheme` in Core. IOS-CORE-02: money and dates from one Core
  locale source.
- **Core moves, part 2.** IOS-CORE-03: the notifications inbox stack. IOS-CORE-05: the devices screen. IOS-CORE-04:
  the address-picker map screen. Each moves to Core once; both apps keep their current visible output.
- **Lag.** ios-perf-01: the partner Available board. ios-perf-03: remote image decode off the main thread.
  ios-perf-02: the partner splash overlaps the registration check. ios-perf-06: the customer Home carousel at 20 Hz.
- **Cleanup.** IOS-CORE-09: dead code in Core and both apps. IOS-CORE-07: dead catalog keys behind uncalled `L10n`
  accessors. IOS-CORE-11: twin preview and error-retry helpers.

## Not doing

- Visual design changes: unifying error-view looks, card radii, the address search UI, and any copy or format change
  a user would see.
- The audit's "not worth doing" list: file splits, the formatter caching sweep, prebuilt Stripe and Firebase, CI
  caching, and moving single-app code out of Core.
- The avatar JPEG encode and the area geocode cache (not in the agreed list).
- Android, backend and web.
- Dependency version upgrades (the Stripe change keeps 25.17.0).
- `docs/` pages, except to fix a path or a name a move made wrong.

## Done means

- [ ] Each lag fix has a before and an after number.
- [ ] Each moved piece exists once, in Core, and both apps use it.
- [ ] All three iOS test schemes pass.
- [ ] The screenshots of every touched screen are unchanged.
- [ ] There is one plain-prose commit per item.

## Work record

### Build health

- 2026-11-07 — Built, one commit per item. Stripe resolves from `stripe-ios-spm`, still at 25.17.0 (`ab36c3299`):
  a fresh resolve with no package cache, as CI and a new clone run it, took 166 s and 4.6 GB and takes 34 s and
  1.5 GB; with the local cache, 29 s and 22 s. The four slow type-check sites are typed (`9770615c9`): the onboarding
  step node's body 2350 → 77 ms, `ContactRows.sizeThatFits` 1011 → 17 ms, `StatusBarFade.luminance` 617 → 9 ms,
  `ChipFlow.sizeThatFits` 308 → 36 ms, and none warns at 150 ms. No string literal reaches SwiftUI as a catalog key
  and every key carries an `extractionState` (`e1d3148ab`): a build with extraction on found fifteen literal keys
  (the audit's eleven, the recurring start date and three Core previews) and twenty-six unmarked keys; replaying an
  IDE build's catalog sync now leaves all three catalogs byte for byte the same, and two checks in
  `StringCatalogCompletenessTests` keep it so. Captures in en, light and dark: the customer frames are byte-identical
  (48), as are the new partner frames (the bank step, My documents, the last-month card); the partner Orders and
  job-detail frames read the same to VoiceOver (222 of 222), and their pixels differ only by map tiles and
  anti-aliasing, which a second run with no code change reproduces on 91 frames. Suites on the iOS 16.4 simulator:
  Core 927, Partner 1445, Customer 1984 with the three iOS 16.4 failures known before this work, which pass on
  iOS 26.3 (the customer suite there: 1984, none failing). Evidence in `ios-longterm-20261107/`.

### Core moves, part 1

### Core moves, part 2

### Lag

### Cleanup
