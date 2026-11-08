---
id: T-0917
title: iOS long-term refactor — shared Core pieces, one locale source, lag fixes and build health
size: L
owner: Michael Chaban
created: 2026-11-07
updated: 2026-11-08
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
- The avatar JPEG encode and the area geocode cache, which were not in the agreed list. Mike moved both into scope on
  2026-11-07, and they are fixed in `279b77522` and `9055af6b8` (see the Lag record of 2026-11-08).
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

- 2026-11-07 — Built, one commit per item. The slide is one VoiceOver button, in Core (`2ea1d891d`): the open
  `SlideToConfirm` is named by its words and detail, carries the button trait, confirms when activated while enabled
  and not busy, and reads as dimmed otherwise; the partner's own `slideAccessibility` and its four call sites are
  gone. A Core test proves the action (before the change it found the words and a "Forward" image, and no button), a
  hosted customer test books from the booking footer's slide through the accessibility tree on iOS 16.4 and 26.3, and
  the partner test that starts a job from the footer's sheet through VoiceOver now runs through Core. The picker list,
  the five-language table and `Theme.colorScheme` live once in Core's `Settings`, and both apps use them
  (`33b9571d3`). Money and dates follow the in-app language (`5d811ffbe`): the customer's `OrdersFormat`, Core's
  `EarningsFormat` and the booking's `BookingDateFormat` default to `CoreL10n.locale`; a test that picks Czech, then
  English, failed on the old formatters (412.30 for 412,30, Thu for čt) and passes. Captures in en, light and dark:
  every English customer frame is byte-identical across the three items, as are the partner's picker, slide and
  invoice frames; the booking footer's VoiceOver tree has one button where it had the words and an image; the frames
  that run in Czech, Slovak, Ukrainian or Russian on the English simulator now group money that language's way
  (1 150 Kč for 1,150 Kč), which is the fix. The other partner frames differ only in map tiles or a spinner's phase,
  and their VoiceOver trees only by the no-break space the visible slide label carries. Suites on the iOS 16.4
  simulator: Core 932, Partner 1444, Customer 1988 with the three iOS 16.4 failures known before this work, and one
  timing failure in `AppointmentConfirmationVisualTests` that passed on its rerun and in three earlier runs; Customer
  on iOS 26.3: 1988, none failing. Evidence in `ios-longterm-20261107/captures/`.
- 2026-11-08 — Two things the record above left out. The contract sheet's loading slide no longer reads a VoiceOver
  hint: the partner's own slide gave it one that repeated its label, Core's slide carries none, and the captures'
  VoiceOver trees record no hints, so no capture shows it. And no guard keeps the locale fix's class of bug out: a lint
  rule against a formatter that defaults to `Locale.current` would have to exempt the ones that still read the phone's
  language (the partner's `OrdersFormat`, `DashboardFormat`, `DashboardGreeting`, `PendingOfferPresentation` and
  `OrdersBoardRules`, the customer's `HomeSections`, `RewardsComponents` and `BookingSheetView`, and Core's
  `EarningsFormat.currencySymbol`), and moving those is outside the agreed scope, so they are listed for Mike instead.

### Core moves, part 2

- 2026-11-07 — Built, one commit per item. The notifications inbox, its view model, the badge model, the feed
  protocol with its models and the bell live once in Core's `Notifications` (`8dcf2754a`); each app passes its
  words (its `L10n.NotificationsInbox` conforms to `NotificationsInboxCopy`), its empty-inbox mascot, how it
  dates a row older than a week, its push templates, its feed keys and its destinations, from its own
  `NotificationsInboxSheet.swift`. The iOS symbols gate learnt that an L10n namespace passed as a type names no
  member, with a self-test on each side. The devices screen, its view model, model and client protocol live
  once in Core's `Devices` (`dd5719d42`); each app keeps its words and its live client with its own answer to an
  unsuccessful revoke. The address-picker map screen and its card live once in Core as `AddressPickerScreen` and
  `AddressPickerConfirmCard` (`6a1ccf05d`); the customer's review-and-save step stays in
  `BookingAddressPickerView`, which hands it to the screen, and the partner's DEBUG copy of `PreviewMapProvider`
  is gone. Differences kept as they were, not unified: the inbox's Retry ("Retry" for the partner, "Try again"
  for the customer), its empty subtitle and mascot, its date for a row older than a week (the partner's Orders
  day and time, the customer's order date and time); the devices screen's name for a device with no platform
  ("Not set", "Device"); the picker's two location snackbars. Tests moved with the code: the inbox, badge and
  devices tests the two apps each carried are one set in Core, each app keeps the tests of its own wiring, and
  a hosted Core test holds the picker's review step; by area the inbox went from 87 tests to 70, the devices
  from 23 to 16 and the picker from 1 to 3, every behaviour still tested. Captures before and after, in en,
  light and dark (the customer's picker also in Ukrainian and on a small phone): every frame byte-identical,
  and every VoiceOver tree (customer 20 frames and 18 trees, partner 22 and 22), across the inbox with rows,
  empty and failed, the dashboard and Home bells, the devices screens and both pickers. Suites on the iOS 16.4 simulator: Core 972, Partner 1417, Customer 1960 with the
  three iOS 16.4 failures known before this work; Customer on iOS 26.3: 1960, none failing. Evidence in
  `ios-longterm-20261107/captures/core-b/`.

### Lag

- 2026-11-07 — Built, one commit per item, each measured before and after on the iOS 16.4 simulator with the machine
  idle. The partner Available board is a lazy stack (`f2319095d`): one publish at 10, 30 and 100 rows costs 13.8,
  21.0 and 45.7 ms at -O before and 13.4, 14.9 and 17.1 ms after (Debug 13.8, 24.4 and 57.1 before, 14.2, 16.9 and
  15.9 after), first layout 32, 80 and 176 ms before and 21, 27 and 35 ms after, and a publish rebuilds 4, 6 and 7
  cards where it rebuilt 9, 29 and 99. The cost moves to the moment a day enters the screen: scrolling 100 rows, a
  step that builds a new day takes up to 34 ms (12 before), 13 of 565 steps over a frame. The reserved cards are
  built as one block and the shared pay column keeps the widest need it has seen, so it holds at one width through a
  scroll over pays of three to six digits; VoiceOver's targets after Load more and after a take are built and on
  screen. Remote photos and avatars are decoded off the main thread (`fe5cf829d`): a 1920 × 1440 photo costs the
  main thread 1.2 ms where it cost 35.8 ms, with memory unchanged. A signed-in partner's cold start asks for its
  registration status during the splash (`7ec2c5f31`): with a 0.8 s hold and a 0.8 s answer the gate took 1.67 s
  and takes 0.83 s, and a new test holds it. The customer Home carousel ticks its fill at 20 Hz (`a1381a891`): the
  timeline updates 20 times a second where it updated 48 to 60, the main thread spends 26 to 30 ms a second on it
  where it spent 45 to 51, and the fill's drawn edge moves the same. Captures in en, light and dark: the Home, the
  splash and the board look the same; the board's frames differ by noise a second run with no change reproduces, by
  a third of a point at the end of the page, and by one pixel at the right edges where a child measures wider than
  its column, which the old stack passed on to every card; the photo tiles differ by the decode path, by up to 28 of
  255 on a synthetic stripe photo and 1 of 255 on the crew photo. New captures: the job photos and the splash. Suites
  on the iOS 16.4 simulator: Core 972, Partner 1420, Customer 1960 with the three iOS 16.4 failures known before this
  work; Customer on iOS 26.3: 1960, none failing. Evidence in `ios-longterm-20261107/captures/lag/` and `lag/`.
- 2026-11-08 — Two more, which Mike added to this pull request on 2026-11-07 after the Not doing list above was
  written ("Fix all of the bugs that you found and also what you found"), one commit each. A picked avatar is encoded
  off the main actor (`279b77522`): both apps' view models await Core's `AvatarEdit.pick`, which encodes in a detached
  task as the job and document photos already did, and keep their error for an unreadable image. A 4032 × 3024 JPEG
  picked on the iOS 16.4 simulator with the machine idle cost the main thread 76.4 ms upright and 167.0 ms as a
  portrait capture in the partner app (customer 77.6 and 168.7 ms) and costs 0.05 and 0.12 ms (customer 0.06 and
  0.13 ms); a customer test that the stored photo stays on screen while a pick encodes failed before and passes. The
  partner job map looks up an area once for every map (`9055af6b8`): `JobAreaLocator` keeps the areas it finds for the
  rest of the run and keeps no miss, and its test, which failed before, checks both with a second locator. Suites on
  the iOS 16.4 simulator: Core 970, Partner 1422, Customer 1950 with the three iOS 16.4 failures known before this
  work, and one timing failure in `AppointmentConfirmationVisualTests` that passed on its rerun; the same three at -O
  whole-module with MallocScribble, on `9055af6b8`, fail only the three known. Evidence in
  `fixes-20261107/step2/lane-368/`.

### Cleanup

- 2026-11-08 — Built, one commit per item. The dead code is gone from Core and both apps (`431b65239`): in Core the
  section header, the secure field, the empty `Format` enum, the auth network boundary and its two tests, an empty
  response type, `AvatarEdit.isPick` and a font registration nobody calls; in the customer app the placeholder tab
  view, the post-booking timeline and its eleven tests, the referral status enum, the booking summary date formatter
  and a sign-up test hook; in the partner app a confirm-code test hook and an error key no code reads. The partner's
  placeholder tab destination and Core's grouped phone formatter, which only a preview and tests use, build for Debug
  alone; a Release build of the partner app carries neither. The uncalled L10n accessors are gone (`0d97434dc`): 44 in
  the partner app and 33 in the customer app, with the 73 catalog keys nothing else reads, in all five locales (43
  partner, 30 customer); every key of the families built at runtime stays, and the catalog checks dropped two stale
  exceptions. The partner's four identical error views are one `ErrorRetryView`, the dashboard passing a clear
  background, and its two preview order clients are one `PreviewPartnerOrderClient` (`2d06e77d1`). Lines, net: Core
  sources −139, Core tests −45, customer sources −248, customer tests −164, customer catalog −1050, partner sources
  −356, partner catalog −1601; the partner tests gained the 71-line capture of the four failed loads. Captures in en,
  light and dark at 393 × 852: the four failed loads, the dashboard with its bell, the invoices, the job photos and
  the customer's booking success, order detail and order sheets are byte-identical, and so are their VoiceOver trees;
  the dashboard's greeting and date follow the clock, so its frames were compared within one hour. Suites on the iOS
  16.4 simulator: Core 970, Partner 1421, Customer 1949 with the iOS 16.4 failures known before this work; Customer on
  iOS 26.3: 1949, none failing. Evidence in `ios-longterm-20261107/captures/cleanup/` and `cleanup/`; the deleted keys
  are listed in `ios-longterm-20261107/deleted-catalog-keys.txt`.

### Follow-up fixes

- 2026-11-08 — Built, one commit per fix. The inbox dates a row older than a week itself, with the template both apps
  used, so the per-app date rule Core took is gone, and a Core test asserts that text in Czech; no test had checked it
  since the two apps' date tests were merged (`bc25e4113`). The Available board's pay column narrows again when its
  widest reserved job leaves, and still holds one width through a scroll (`7bb79ccd0`): with six reserved jobs, once
  the 124 800.50 Kč one leaves, the other five tiles were 124 pt and are 115 pt at every scroll position, and a test
  pins the rule. Core's `AddressPickerConfirmCard` is internal (`bc7aa28a9`). Each test target has one accessibility
  switch, and Core's tests one element walker (`80d67b1e2`); the booking review capture passes a closure literal to
  `filter` (`40a44b261`); the partner capture suite's header no longer lists its captures (`358c1ebda`). Captures in
  en, light and dark of both apps' inbox and picker and the partner board: all 80 VoiceOver trees are the same, the 10
  customer frames are byte-identical, and so are 66 of the 70 partner frames; the other four differ by one to three
  pixels, each by one or two levels in one colour channel at an anti-aliased edge; one of them is the Active tab's job
  card, which no fix reaches, and another differs by the same pixels between two runs of the same build. Suites on the
  iOS 16.4 simulator: Core 970, Partner 1422, Customer 1949 with the iOS 16.4 failures known before this work (two in
  `CustomerReadableWidthTests`, one in `LiveActivitySessionCleanupTests`); Customer on iOS 26.3: 1949, none failing.
  The same four runs at -O whole-module with MallocScribble, on `7bb79ccd0`, give the same results. Evidence in
  `ios-longterm-20261107/fix/`.
