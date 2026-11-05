---
id: T-0900
title: Redesign parts of the Appointment confirmation to remove unused space
size: M
owner: Michael Chaban
created: 2026-10-31
updated: 2026-10-31
depends_on: [T-0897]
blocks: []
stories: [US-mobile-20261030]
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

After reviewing T-0897's compact Appointment confirmation, Mike found too much unused space and asked
for parts to be redesigned. Round 2 was proposed, reviewed (taste, product design, buildability) and
approved on 31 October 2026 ("Implement it"). Review page:
https://claude.ai/artifact/3qCMcQoqkVQystsZk4fkVb. Build spec and evidence:
`/Users/michael/cleansia-work.noindex/confirmation-appointment-round2-impl-20261031/` (`SPEC.md`).

## Acceptance criteria

- [x] **AC1** — The animated cleaning mascot stands in the hero's bottom-right corner on the receipt edge, mirrored to face the date, drawn above the receipt without entering layout: full size beside time and address, a smaller figure beside the address alone when the time would not fit, hidden above xxLarge text / Android font scale 1.3 and while details load or are unavailable. With motion off it shows the animation's first frame as its own still image.
- [x] **AC2** — The thin five-phase tracker moves, unchanged, under the Booking received chip. The receipt loses its caption, divider and tracker, and "Your booking details are saved." is removed with its string on both platforms. Loading/unavailable show one line in the hero, without a spinner.
- [x] **AC3** — Back to home (outlined) and View order (filled) share a row; when a label would truncate they become View order over the shared Back to home text button.
- [x] **AC4** — Android shows the confirmation as a fully expanded bottom sheet over Home; the BookingSuccess route is removed and the view model starts from the order id it is given.
- [x] **AC5** — First-pass polish: pin in a tonal disc under the tile with one hero text column; code SemiBold 16 and total Bold 20 on one baseline; tonal dark chip; optically centred tile; matched small type; Android stacks by measured fit.

## Out of scope

Tracker component look, order detail, the booking form, Partner, payment/status logic, the found-on-the-way
items below, commit, push, upload, deploy.

## Work record

- 2026-10-31 — Implemented locally on `codex/mobile-refinements-20261029` (HEAD `2aa449afa`, uncommitted). iOS: `BookingSuccessView.swift`, one optional `horizontalPadding` on CleansiaCore `CleansiaOutlinedButton`, `mascot_cleaning_still` imageset, string removal, two test files. Android: `BookingSuccessScreen.kt`, `BookingSuccessViewModel.kt`, `MainShell.kt`, `CleansiaNavHost.kt`, `Routes.kt`, `mascot_cleaning_still.png`, string removal in five locales, one test file. All 56 other start-dirty paths byte-identical.
- Verification: iOS SwiftFormat/SwiftLint strict clean; 31 focused Customer tests pass on the iOS 16.4 simulator (including new red-then-green reading-order and still-asset checks); CleansiaCore scheme 861/864, the 3 failures being pre-existing auto-extracted catalog keys (see below). Android `:customer-app` compile and 1,819 unit tests pass. Native captures and motion recordings on both platforms in the evidence folder; Android motion-off equality and mascot presence checked on pixels.
- Not verified: the Android `MainShell` path at runtime (book → sheet over Home → View order / Back to home) was exercised only through a fixture that mirrors its sheet configuration; it needs one real booking on a device or emulator. iOS stacked Back to home tap area is the label width (shared `CleansiaTextLink`), not full width.

## Found, not fixed

- All three iOS string catalogs carry Xcode auto-extracted junk keys (absent at HEAD) and `auth_email_placeholder` is English in cs/sk/uk/ru; `StringCatalogCompletenessTests` fails 3 cases until they are cleaned before commit.
- The Czech/Slovak calendar tile shows the day as "31." on iOS, and at xxxLarge text the period wraps inside the 70 pt tile.
- On a full-height Android sheet (320 × 568, large fonts) the drag handle sits in the status-bar row.
- Android date/time patterns are fixed (cs/sk lose the dot, English always 24-hour, Russian tile genitive); Russian "View order" differs by platform; Android "To pay by card" on Failed; Android View order with a blank order id; the panel grows when details load; two reference numbers on one panel; `drawable-nodpi/mascot_cleaning.png` in customer-app is now unreferenced.
