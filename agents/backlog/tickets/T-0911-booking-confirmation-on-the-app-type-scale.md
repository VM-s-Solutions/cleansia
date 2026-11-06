---
id: T-0911
title: Customer booking confirmation on the app's type scale — less bold, the app's fonts
size: S
owner: Michael Chaban
created: 2026-11-04
updated: 2026-11-04
depends_on: [T-0910]
blocks: []
stories: []
adrs: []
layers: [ios, android]
security_touching: false
---

## Context

Mike, 2026-11-04: "I want you to refactor customer app confirmation screen fonts as we discussed it for the partner
app, not much bold fonts, font should match the rest of the app design and so on".

The rule is the one T-0910 applied to Partner Orders:
- each card has one emphasised line (its title or key figure), and everything else is Regular, with colour setting
  the hierarchy;
- Poppins only where the app uses it;
- every text on a `CleansiaTypography` / `MaterialTheme.typography` token, with no hand-set sizes.

The role → token table is in `partner-orders-ds-20261102/inventory/TYPE-USAGE.md` §4.

## Acceptance criteria

- [ ] iOS `BookingSuccessView*` and Android `BookingSuccessScreen` / `BookingSuccessPresentation` use only type tokens,
  with bold limited to one emphasised line per card.
- [ ] No content, copy, animation, timeline or mascot-size change (T-0909's figure height holds).
- [ ] Before/after captures in light and dark, en and uk, small and large phone; suites green.

## Out of scope

- Every other customer screen.
- Commits.

## Work record

- 2026-11-04 — Filed and started with the T-0910 open-item pass.
- 2026-11-04 — Android done in code, suites green; captures blocked on disk. Role map (iOS mirrors it): page title
  `headlineMedium` Bold (Poppins); the order number `bodyMedium` tnum, `onSurfaceVariant`; each block's one emphasised
  line `titleMedium` (Finding your cleaner, the basket, the total's figure); the appointment window is the page's one
  hero figure, `headlineMedium` Bold tnum (Poppins); leaf month `labelMedium` SemiBold and day `titleMedium` (R18);
  day line `bodyLarge`; address, sub-lines, crew, extras chips and the note `bodyMedium`; the free-cancellation sentence
  `bodyMedium` in `onSurfaceVariant` with its date in `onSurface` (colour, not weight); money rows `bodyMedium`, the
  total's label `bodyLarge`; Retry `labelLarge` `primaryText`. The size classes keep their leaf and seat sizes and the
  large-text caps; the type no longer changes with them.
- 2026-11-04 — Built on both platforms; `in_review`.
  - iOS:
    - type tokens only;
    - Poppins on the title and the time window;
    - one bold line per card;
    - the date tile is a 64 square on the app's day-card fonts.
  - Android: the same type rules; the date tile keeps its size.
  - Captures, before and after, in light and dark, en and uk, small and large phone:
    - iOS: `partner-orders-ds-20261102/build/ios/t0911/side-by-side`. 81 of 85 other customer frames are
      pixel-identical; only the 4 booking-success frames differ.
    - Android: `…/build/android/t0911/customer/confirmation-{before,after}-*`. The customer set has 88 byte-identical
      frames and 2 that differ only in the system bars. The 4 core-additions showcase frames differ by the opt-in Core
      additions.
  - Suites: iOS shows only the known failures; Android core 382, partner 1258 and customer 1865, all with 0 failures.
- 2026-11-04 — Mike: "Make android match it".
  - The Android date tile is now the 64 square at every size (`confirmationLeafSide`), grown to a 92 square at
    accessibility sizes so the day fits.
  - `BookingSuccessLeafTest` (2) was written after the change.
  - customer-app 1867/0.
  - Re-shot: `build/android/t0911/customer/confirmation-after-*`; the earlier set is in `customer-pre-square/`.
