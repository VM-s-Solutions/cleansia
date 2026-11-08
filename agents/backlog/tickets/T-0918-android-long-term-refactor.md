---
id: T-0918
title: Android long-term refactor — minified staging builds, lag fixes, shared pieces in :core, dead code and docs drift
size: L
owner: Michael Chaban
created: 2026-11-08
updated: 2026-11-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android, docs]
security_touching: false
---

## Context

Mike, 2026-11-07, on the verified mobile audit, verbatim:

> "Don't do the quick fixes, I want you to do the fixes that actually matter and win in long term. Refactor the code,
> polish it, review it from technical side as well if it's optimized"

He agreed the scope below the same day, and later wrote:

> "Fix all of the bugs that you found and also what you found"

which adds the pattern-catalog drift (AND-PO-04) and the unused version-catalog entries.

This is the Android part of three pull requests; the crash fix (T-0914, #366) and the iOS refactor (T-0917, #368) are
the other two. Every item comes from the verified audit, `audit-20261107/report/verified-flat.txt`, the Android rows
(lines 181–388), which describe master `3e720a7e7` on 2026-11-07. A row's note column carries the corrections made when
the audit was verified, and they win over its laziest fix. Where the agreed scope goes further than a laziest fix (a
move to `:core` that the audit deferred to "when next touched"), the agreed scope wins: Mike chose the long-term
version. Each item is checked against this branch before it changes; one that no longer exists is recorded and skipped.

## Scope

- **Minified staging builds.** and-perf-01: a minified staging build for each app. Every lag number below is measured on
  these builds.
- **Lag.** and-perf-04 (the customer Home carousel reads its progress in the draw phase), and-perf-02 (the partner job
  detail builds its page once), and-perf-03 (a far bottom-tab jump does not build the tabs in between, both apps),
  and-perf-06 (one shared navigation transition definition in `:core` for both apps), and-perf-10 (the customer Home
  starts loading during the splash).
- **Shared pieces in `:core`, first part.** AND-CM-01 (`slideSemantics` in `:core`; the customer booking slide gets its
  TalkBack action), AND-CM-05 (one `ActionState`, in `:core`), AND-CM-06 (`PasswordRuleList` used everywhere, and one
  `IconHalo` in `:core` for the seven copies), AND-CM-02 (the customer theme sets its tertiary roles: the one intended
  visual change).
- **Shared pieces in `:core`, second part.** AND-CM-04 (the notifications-feed and devices data layers), AND-CM-07 (the
  floating island bottom bar), AND-CM-09 (the customer's four private status pills become `:core` `OrderStatusPill`),
  AND-PO-03 (one money formatter in `:core` for the partner app's copies).
- **Cleanup.** and-perf-05 (the mascots as 720 px WebP), AND-PO-01 (strings no code reads), AND-PO-02 (dead
  declarations), AND-PO-06 (the nine `Log.d` calls), and the unused Room, biometric and Lottie entries in
  `gradle/libs.versions.toml` (from AND-PO-04).
- **Docs.** AND-PO-04 (the mobile pattern catalog describes the apps as they are), the Android charter's drift, and
  every catalog line the moves above made wrong.

## Acceptance criteria

- [ ] Each lag fix has a before and an after number, measured on the minified staging build.
- [ ] Each moved piece exists once, in `:core`, and both apps use it.
- [ ] The unit tests of `:core`, `:partner-app` and `:customer-app` pass, and both staging builds assemble and start.
- [ ] The screenshots of every touched screen are unchanged, except the one intended visual change: the customer's
  tertiary colours (AND-CM-02).
- [ ] There is one plain-prose commit per item.

## Out of scope

Not done, even where a problem is visible; it is reported instead.

- Visual design changes: one look for the error views, card radii, the partner app's three money looks (the one money
  formatter keeps every call site's current output exactly), the address search UI.
- The audit's "not worth doing" list; baseline profiles (and-perf-08); the contract WebView fonts (and-perf-09).
- The test-only functions `bookingSuccessSteps` and `heroRightText`: Mike decides whether they are unwired features.
- iOS, backend and web; dependency version upgrades.
- `docs/` pages, except to fix a path or a name that a move made wrong.

## Work record

- 2026-11-08 — Filed.

### Minified staging builds

- 2026-11-08 — and-perf-01 done (`06ab04a94`). The partner staging build type now falls back to release for `:core`
  and uses the debug package, which has a Firebase client in `google-services.json`; the customer gained a staging
  build type initialised from debug, not debuggable, R8-minified with release's proguard files. R8 broke the partner
  start with "Serializer for class 'o' is not found": the route objects `NavRoute.Invoices` and `NavRoute.Profile` are
  registered but never navigated to, so the kotlinx.serialization 1.7.3 object rule, which the partner's
  `proguard-rules.pro` copies, let R8 drop their `INSTANCE` and `serializer()`; that rule is now conditioned on the
  class alone. Partner `assembleStaging` 1 min 41 s (R8 56 s), APK 81.6 MB, dex 8.0 MB in one file; customer
  1 min 39 s (R8 65 s), APK 88.8 MB, dex 12.3 MB in two files. Both install and start on `cleansia35` from cleared
  data and over a debug install, and show the server's "Incorrect password." for a failed sign-in against DEV.
  Unit tests: core 390/0, partner 1277/0, customer 1871/0. `check-consistency.mjs` reports what origin/master
  reports (one existing E1 violation). Evidence in `android-longterm-20261108/` (`logs/`, `start/`).

### Lag

- 2026-11-08 — and-perf-04 (`cd3a38396`), and-perf-02 (`fcdf39891`), and-perf-03 (`60c1c6656`), and-perf-06
  (`ae70fdebe`) and and-perf-10 (`350d49346`) done. Measured on the minified staging builds on `cleansia35`, signed in
  against a local stand-in for the two mobile APIs (a DEV sign-in needs a confirmed e-mail), its delays taken from
  DEV's anonymous endpoints, with uncommitted probes. The Home carousel recomposed 52 times a second while autoplaying
  and its slide 52-104, now 0 and 0; the frame's animation phase p50 0.35 ms, now 0.04-0.05 ms. A job opened from the
  board built 2 MapViews, 2 sheet states and 2 figure decodes, now 1, 1 and 1, from one `JobDetailFrame` call. Each
  far bottom-tab jump built one tab in between in both apps, now none (`:core` `PagerState.showTab`); the customer's
  orders were read 6 times over nine taps, now 2. The partner's push and pop took 742-793 ms (the 700 ms default
  cross-fade), now 322-364 ms, through the push and pop transitions now in `:core` (`pushEnter` and its three
  siblings), which the customer uses unchanged (335-373 ms both ways). The customer splash starts the orders and the
  membership reads: they land at 0.65-0.78 s after the process starts instead of 2.55-2.65 s, but Home's first
  content stays at about 3.45 s because the catalog, read after the hold, is slower; with a 2.5 s orders read, first
  content moves from 3.64-3.83 s to 3.42-3.54 s. The partner builds measured and captured carry a placeholder Mapbox
  token, since an empty one makes the MapView constructor throw. Captures of the touched screens (the carousel and
  the splash, the 107 job detail frames; en, light and dark) are byte-identical in the apps' content; the first dark
  360x800 frame of a run differs in 23 pixels of the system gesture handle, which two captures of one unchanged build
  also do. Unit tests: core 391/0, partner 1278/0, customer 1872/0. Both staging builds assemble and start;
  `check-consistency.mjs` reports what origin/master reports. Evidence in `android-longterm-20261108/` (`lag/`,
  `captures/lag/`).

### Shared pieces in :core, first part

- 2026-11-08 — AND-CM-01 (`3386408cb`), AND-CM-05 (`71abe87d4`), AND-CM-06 (`984cf3b07`) and AND-CM-02 (`abce87171`)
  done. `slideSemantics` moved unchanged next to the slide, into `:core`'s `SwipeToConfirmButton.kt`; the four partner
  files import it, and the customer booking slide now passes it, with one confirm action for the swipe and the double
  tap, enabled when the step can continue and the order is not being placed, and busy while it is. A `:core` test
  reads the semantics from the modifier, and `SlideSemanticsCallSiteTest` checks every slide call in both apps. On
  `cleansia35`, TalkBack focuses a slide wired as the booking sheet wires its own as one button, and its double tap
  commits only the slide that can be used: one commit, and none on the slide that cannot continue or on the one placing
  an order. The customer's own `ActionState` is deleted, its 18 main and 10 test callers import `:core`'s, and the
  mobile-app features page no longer lists it. One `IconHalo` (with `IconHaloSize`) in `:core` replaces the six partner
  copies and the customer's `LeadingIcon`, and the customer's two `RuleList` copies are calls to `:core`'s
  `PasswordRuleList`. The customer schemes set their tertiary roles to the partner's amber, the one intended visual
  change: Home's milestone card wash goes from #FBECF2 to #FBF7E7 in light mode and from #312636 to #39231F in dark,
  and the not-serviced address glyph from #7D5260 to #92400E and from #EFB8C8 to #F59E0B. Captures (en, light and
  dark, 412x915): the 74 partner screens (every icon-halo card, the take sheet, the Active hero and nine job-detail
  states with their scrolled ends) are byte-identical; 26 of the 30 customer frames are pixel-identical outside the
  system battery glyph, which SystemUI draws a few pixels apart from one emulator session to the next, and the other 4
  are the milestone card and the glyph. Unit tests: core 392/0, partner 1278/0, customer 1873/0. Both staging builds
  assemble and start; `check-consistency.mjs` reports what origin/master reports. Evidence in
  `android-longterm-20261108/` (`core-a/`, `captures/core-a/`).

### Shared pieces in :core, second part

- 2026-11-08 — AND-CM-04 (`1f48b3e7d`), AND-CM-07 (`cbffb7119`), AND-CM-09 (`92a6a5cb9`) and AND-PO-03 (`790fa02be`)
  done. The notifications feed's Retrofit interface, wire shapes and repository, and the devices interface and wire
  shapes, exist once, in `cz.cleansia.core.notifications` and `cz.cleansia.core.devices`; each app keeps its own
  `@Provides` from its authenticated Retrofit and its session-wipe binding. The devices repository is the customer's
  `DeviceManagementRepository`, and the partner's interface and implementation are deleted, with the `currentDeviceId`
  nothing read. The two copies differed on one response the server never sends (a revoke answered 200 with `success`
  false was a success for the partner and an error for the customer; `RevokeDevice` answers `Success(true)` or an error
  body), so that difference is dropped rather than made a parameter. One copy of each repository test is kept.
  `FloatingIslandBar` and `FloatingIslandSlot` in `:core` replace the partner's bar body and both apps' private
  `NavSlot`; the customer passes its Book button as the bar's overlay, drawn in the bar's box so the pill's clip does
  not cut it and the box keeps its 98 dp. The customer's four private status pills call `OrderStatusPill` with a
  `washAlpha` of 14 %, the wash they drew, where the pill's default stays 16 %. One `formatMoney` in
  `cz.cleansia.core.money` replaces the partner's four identical copies and backs its three variants: `fractionDigits`
  2 by default, 0 for the earnings figures (`formatWholeMoney`, a dash for no stats) and the dashboard
  (`formatMoneyWithSymbol`, an English comma drawn as a space, a fallback for nothing earned), and null for the board's
  rule, moved in unchanged. Compared on the JVM with the old functions over 4,028 amounts, every JDK currency's symbol
  and the five locales, every figure is the same. The three `d MMM yyyy` date helpers are one partner `formatShortDate`,
  whose `inDeviceZone` keeps the period pay's difference: the day on the phone's clock, and a timestamp with no offset
  read as local. Captures (en, light and dark, 412x915) of every touched screen: the partner's 58 frames (the bar with
  each tab selected, every money and date card, the devices list, the Orders board, the take sheet and three job detail
  cash states) are byte-identical within one emulator session, and so are the customer's 12 (the bar, the four pill
  screens, the devices list); in another session the six dark customer frames differed in 17 pixels of the system
  gesture handle. Signed in against a local stand-in, both apps' minified staging builds list the devices and the
  notification feed, show the unread badge and remove a device through the `:core` classes, with no crash. Unit tests:
  core 419/0, partner 1267/0, customer 1852/0; per area, the feed repository tests go from 20 to 10 (one copy), the
  devices repository's stay 9, the bar's go from 8 to 7, and 6 are added for the money and date helpers. Both staging
  builds assemble and start; `check-consistency.mjs` reports what origin/master reports. Evidence in
  `android-longterm-20261108/` (`core-b/`, `captures/core-b/`).

### Cleanup

- 2026-11-08 — and-perf-05 (`01a50d89a`), AND-PO-02 (`0c3258842`), AND-PO-01 (`4b7c0916b`), AND-PO-06
  (`260a549a9`) and the unused version-catalog entries (`6a070dc2d`) done; what is dead was derived again on this
  branch, after the moves. The fourteen mascots drawn from 1024 px PNGs (seven per app) are 720 px WebP under the
  same names, `:core`'s refresh suds is 128 px, and the partner's unreferenced copy of the waving mascot is gone.
  Measured on the minified staging builds on `cleansia35` with an uncommitted probe (`Resources.getDrawable` in a
  fresh process, then the `ImageDecoder` decode itself), before and after alternating, 6 runs each: the partner's
  seven took 62.4 ms to decode together before and 24.1 ms after (7.5-10.7 ms each before, 2.0-5.0 ms after), the
  customer's 71.4 ms and 24.1 ms, the suds 1.05 ms and 0.12 ms; each bitmap is 2,073,600 bytes instead of
  4,194,304. The staging APKs carry 487,168 bytes of these images instead of 5,322,279 (partner) and
  511,028 instead of 8,030,630 (customer); with the whole cleanup, the partner staging APK goes from 81,627,107 to
  76,547,762 bytes and the customer's from 88,830,207 to 81,277,114. Dead declarations: 19, in 19 files, -270/+15
  lines (the partner's `isDark()` moves into `Theme.kt` so `BrandGradients.kt` can go). The functions only
  tests reach are kept for Mike's decision, and so are the palette tokens no screen reads. Strings: 410 partner and
  137 customer names that no code reads are deleted from all five locales, 3,054 lines; the runtime-built
  `error_*`, `profile_fields_*` and `loyalty_perks_*` families and the `raw/keep.xml` globs were never candidates,
  and three customer strings only tests read stay. The nine `Log.d` calls (-14 lines) and the Room, biometric and
  Lottie catalog entries (-12/+4) are gone; no build script names those entries, and both apps assemble in debug
  and staging without them. Captures (en, light and dark, 412x915) of every screen that draws a changed image, the
  before and after builds in one emulator session: 13 customer screens and 33 partner screens with their scrolled
  ends. Every difference in the apps' content lies inside a mascot, which looks the same at the size drawn (1-4
  levels for most changed pixels, more on outline pixels, which alias less at small sizes); the frames whose figure
  is the unchanged 500 px drawing are identical apart from SystemUI's battery glyph and gesture handle. Unit tests:
  core 419/0, partner 1267/0, customer 1852/0, the counts before this part. Both staging builds start on
  `cleansia35` from cleared data without a crash; `check-consistency.mjs` reports what origin/master reports.
  Evidence in `android-longterm-20261108/` (`cleanup/`, `captures/cleanup/`).

### Docs
