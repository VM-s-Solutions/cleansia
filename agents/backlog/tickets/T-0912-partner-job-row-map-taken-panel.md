---
id: T-0912
title: Partner Orders — job rows open the job, a map behind the detail, the Job-taken panel, gallery photos, the cleaning animation, Note/Issue row
size: M
owner: Michael Chaban
created: 2026-11-04
updated: 2026-11-04
depends_on: [T-0910]
blocks: []
stories: []
adrs: []
layers: [ios, android]
security_touching: true
---

## Context

Mike, 2026-11-04, on the built T-0910 pages, and on the concept page https://claude.ai/artifact/Vstn1kvxczyK8ikpD2nwG6
(source `partner-orders-ds-20261102/concepts-t0912/template.html`). His picks were E, M1, M3 and J3, verbatim:

> "1. E, but make the shevron on the order card itself on the right vertically in the center having a bit changed
> background to highlight it 2. M1 3. M3 4. J3"

> "for photos before/after add a possibility to select photos from gallery as well / Once in progress then put an
> animation of cleaning mascot / Align note/issue buttons better, make issue button red"

## Acceptance criteria

- [ ] **E.** On the Available cards and the detail's "Also open that day":
  - each job row opens the job detail, and the row carries no Take control;
  - a chevron sits on the row's right edge, vertically centred, on a lightly tinted column;
  - taking happens from the detail's pinned "Take this job".
- [ ] **M1.** Before a job is taken, the detail's band shows a blurred, dimmed map of the job's area, with the chip
  "Exact address after you take the job". No exact coordinate is used, and none is available.
- [ ] **M3.** Once taken, at every step, the band shows the real map centred on the address with a pin. Tapping the
  map navigates.
- [ ] **J3.** The Job taken panel shows:
  - the title and mascot;
  - a map strip with the pin and the street;
  - the 64 date square with the day and window, plus the area and the reward;
  - the cancel rule as one line;
  - "Open job" as the primary button and "Back to jobs" as a text button;
  - and no Close link.
- [ ] **Photos.** Adding a before or after photo offers the camera or the photo library.
- [ ] **Animation.** While a job is in progress, the head shows the animated cleaning mascot, still under reduced
  motion.
- [ ] **Note/Issue.** Two equal buttons in the Before/After column grid, with Issue in the error tone.
- [ ] Suites green, and captures of each item on both platforms.

## Out of scope

- Rules (offerability, take, pay).
- The reserved-offer card and Active's "See offer".
- Commits.

## Work record

- 2026-11-04 — Filed from Mike's picks; build started.
- 2026-11-04 (ios) — E: an open job row on Available, on Active's soonest open and in the detail's "Also open that day"
  is one button that opens the detail (VoiceOver: card sentence + "Opens the job"). The pay is trailing on the time row,
  and the chevron is centred in a 36-wide column at 8 % primary on the trailing edge. The reserved card keeps
  Confirm/Decline, and `PayTileFace`/`OrdersPayTile` now carry only Confirm. Deleted `orders_take` and
  `orders_tile_overlaps`, both unread.
- 2026-11-04 (ios) — M1: before the take, the band shows a non-interactive MapKit map (Core `PlaceMap`) centred on the
  area. The area string is geocoded on the device with a hint for the market's city, and falls back to Prague. The map
  is blurred 7 and dimmed 25 %, under the chip "Exact address after you take the job" (new
  `orders_map_exact_after_take`, five locales). A job not taken never reads a coordinate (`JobBandMap.mode`, tested).
- 2026-11-04 (ios) — M3: on her job, at every step, the map is centred on the address with the Cleansia pin and the
  street label. A tap opens the existing Navigate choice of Apple or Google Maps. With no coordinate she gets the M1
  look without the chip. The band is 40 taller whenever it carries a map.
- 2026-11-04 (ios) — J3: the taken sheet has no header and no Close; the grabber stays. It shows "Job taken" with the
  thumbs-up, a 120 map strip with the pin and the street (a placeholder while the job is looked up, left out with no
  coordinate), the 64 leaf (month + day) beside "<day> · <window>" and "<area> · Your reward <pay>", the cancel rule
  as one info line, primary "Open job" and the text link "Back to jobs".
- 2026-11-04 (ios) — Photos: Add on Before or After offers Take Photo or Choose from Library (existing profile
  strings), or goes straight to the PhotosPicker on a device with no camera. Library images go through the same
  `OrderPhotosViewModel.upload`. The backend has no capture-time rule: `CapturedAt` is stamped at upload, and the
  status window is the only gate, so library photos meet it the same way. Deleted the camera-only alert and its two
  strings.
- 2026-11-04 (ios) — Animation: while she is in the Cleaning step, the head plays Core's `cleaningInProgress` loop, the
  one the customer app already uses. Under Reduce Motion it shows `.still(.cleaning)`. Finishing keeps the slip or
  the spray and cloth. Nothing moved, and the customer is untouched.
- 2026-11-04 (ios) — Note/Issue: two equal halves with Spacing.s between them, the same grid as the Before/After
  columns, each face centred. Issue is in the error tone (`TonalChip.Tone.error`: error at 12 %, error ink).
- 2026-11-04 (ios) — Captures in `build/t0912-ios/captures`, light and dark: available, browse map, progress map,
  taken panel, photo source. A rebuild of the row's chevron fill, the Note/Issue capture and the three full suites
  are blocked: free disk fell to about 0.7 GB, under the brief's 1.5 GB floor. See `build/handoff/t0912-ios.json`.
- 2026-11-04 (android) — E: `AvailableJobRow` is one button that opens the detail. TalkBack reads one sentence (day,
  window, pay, area, scope, flags) and the action "Open job". The pay trails the window. The chevron is centred in a
  36-wide column at 8 % primary on the card's trailing edge. The row is used on the Available cards, Active's soonest
  open job and "Also open that day". The reserved card keeps Confirm/Decline through a private `ReservedJobRow`. The
  code that taking from a row left unread is deleted, and so are `orders_take` and `orders_tile_overlaps`.
- 2026-11-04 (android) — M1: `JobBandMap` shows a Mapbox map of the area string, geocoded in the market's country
  (`JobAreaLocator`), with the market's main city as the fallback. It is blurred 7 (a 68 % scrim below API 31),
  dimmed 25 %, and carries the chip `orders_map_exact_after_take` (five locales). A job not taken never reads a
  coordinate (`jobMapMode`, tested). The band is 40 taller when it carries a map.
- 2026-11-04 (android) — M3: on her job, the map is centred on the address with the Cleansia pin and the street. A tap
  runs the existing `launchNavigation`. With no usable coordinate she gets the area without the chip.
- 2026-11-04 (android) — J3: the taken sheet has no Close; the grab handle stays. It shows "Job taken" with the
  thumbs-up, and a 120 map strip with the pin and the street, only from the taken row's coordinates
  (`takenStripPoint`, tested). Below that: the 64 date tile beside "<day> · <window>" and "<area> · Your reward <pay>",
  the cancel rule as one info line, "Open job" and the text button "Back to jobs". `takenRowFailed` is deleted as
  unread.
- 2026-11-04 (android) — Photos: Before and After open "Take Photo" or "Choose from Gallery". The entrance photo stays
  camera-only (`jobPhotoCameraOnly`, tested). Library photos go through the same upload. The upload now deletes only
  the app's own capture, never a library item (tested). The backend has no capture-time rule: `CapturedAt` is
  stamped at upload, and the status window is the only gate.
- 2026-11-04 (android) — Animation: `MascotAnimation` moved to Core, so the customer side changed only its imports.
  The head plays `mascot_cleaning_in_progress` on the Clean step (`cleaningAnimated`, tested). With animations off it
  shows the first frame.
- 2026-11-04 (android) — Note/Issue: two equal buttons in the photo columns' grid (shared `PhotoColumnsMinWidth` and
  `PhotoColumnGap`), with Issue in `ChipTone.Error` (`RecordKind`, tested).
- 2026-11-04 (android) — Suites, first run: customer is green. Core had 1 failure and partner 30, all from pins and
  mocks the change outdated; all are fixed. The re-run and the captures are blocked: free disk is about 0.7 GB,
  under the brief's 1.5 GB floor. See `build/handoff/t0912-android.json`.
- 2026-11-05 (ios) — Mike's decision 1, photos camera-only: `PhotosSection.swift` is back to its pre-T-0912 bytes, with
  no library picker or source dialog, and the camera-unavailable alert is back. `camera_unavailable` and
  `job_photos_camera_only` are restored in all five locales. The picker had added no strings of its own. The
  `PhotoSource` test is gone, and `JobPhotoCallSiteTests` now also refuses `import PhotosUI`. Red first.
- 2026-11-05 (ios) — Mike's decision 2, every take ends on the Job taken panel: the detail's "Take this job" sheet now
  stays up on the panel (`endsOnTakenPanel`, `TakenJob`). Its strip waits while the order is read back, then pins the
  address. "Open job" closes the sheet onto the taken detail, and "Back to jobs" pops to the board. Refusals are
  handled as before. Test-first, with 4 assertions red before the change.
- 2026-11-05 (ios) — Mike's decision 3, map attribution: the M1 veil is now a blur view placed above MapKit's drawing
  and under its Maps logo and Legal link, so both stay sharp. On the band, the logo and the link sit above the sheet
  edge and short of the figure. MapKit fits a region inside the margins, so a compensating region keeps the pin and the
  scale where they were (`PlaceMapCanvas.framed`, red then green in Core). The logo and the link are visible on M1, M3
  and the J3 strip.
- 2026-11-05 (ios) — The square chevron column is rebuilt and captured, and so is the Note/Issue row. Two source pins
  the first pass had outdated (the scope tie on the Available card, and the chip's ink) are re-pinned.
- 2026-11-05 (ios) — Suites on 3F885C33. Partner: 1425 run, and its 2 failures are the re-pinned tests, which pass on
  re-run. Core: 910 run, failures only in the baseline `StringCatalogCompletenessTests` trio. Customer: 1974 run, the 3
  known failures plus the LiveActivity cleanup test that ios-core-fix.json already records as environmental.
  swiftformat, swiftlint `--strict` and check-ios-symbols are clean. Catalogs, Package.resolved, Info.plist and
  project.yml hold their intended hashes. Captures, light and dark, are in `build/t0912-ios/captures-final`. See
  `build/handoff/t0912-ios-finish.json`.
- 2026-11-05 (android) — Mike's decision 1, photos camera-only: `PhotoSourcePicker.kt`, `OrderPhotosViewModel.kt` and
  `OrderPhotosViewModelTest.kt` are back to their pre-T-0912 bytes, and the detail opens one camera-only picker for
  every job photo again. `jobPhotoCameraOnly` and its test are deleted. The picker had added and removed no strings,
  so no locale changed. Red first.
- 2026-11-05 (android) — Mike's decision 2, every take ends on the Job taken panel: on a take from the detail the sheet
  stays up (`OrderDetailViewModel.takenRow`, the order as read back), and its strip pins the door once the read lands.
  The sheet's taken state now serves any host (`taken`, was `boardTaken`). "Open job" closes onto the taken detail, and
  "Back to jobs" goes to the board's Available tab. Refusals close the sheet as before. Test-first, red on assertions.
  The Dashboard offers' take is unchanged, as on iOS.
- 2026-11-05 (android) — Mike's decision 3, map attribution: the M1 blur and dim now go on the `MapView` itself
  (`MapEffect`: blur 7 on API 31+, a 25 % dim, 68 % below API 31), so the Mapbox wordmark and attribution drawn over it
  stay sharp. Both are visible on M1, M3 and the J3 strip. Pinned by a source test, red first.
- 2026-11-05 (android) — Suites with `--rerun`: core 382/0, partner 1268/0, customer 1867/0; `assembleDebug` OK for both
  apps. Captures, light and dark, are in `build/t0912-android/captures-final`: available rows, browse map, progress
  map with the cleaning mascot's first frame and Note/Issue, and the Job taken panel after a detail take. Map tiles are
  blank and the pin is not drawn: there is no Mapbox token on this Mac. The customer set is identical to t0911's
  (105 PNGs byte-identical, 1 differing only in the system bars). Docs and the INDEX row are updated. See
  `build/handoff/t0912-android-finish.json`.
- 2026-11-05 (ios, follow-ups) — Mike's three follow-ups. (1) A take from the Dashboard's offers list ("See offer") ends
  on the Job taken panel too: the offer's area and reward show at once, the strip waits for the job read back as hers,
  then pins the address (left out if the read-back fails). Open job closes the panel onto the job. Back to jobs closes it
  and leaves her on the offers list. Red first: 4 of 22 failed. (2) The job detail is a Core `SnapSheet` over a still,
  full-height map again. The panel rests expanded at the band's foot, under the fixed back button, or collapses onto
  its pinned head when dragged down. Tapping the handle toggles the two. Everything below the head scrolls inside the
  panel. The map never pans, zooms or rubber-bands. It is drawn at the band's scale, and the pin and lock chip sit
  where the band had them. Apple's logo and Legal ride the panel's edge in both positions. Core `SnapSheet` gains
  opt-in `stops` and `minimumSheetTop` (red first: 3 tests). The customer set is 91/91 pixel- and byte-identical to
  t0911's. (3) The chevron column is 28 pt; it was already 36. Suites: core 913/3 (the catalog trio), partner
  1433/0, customer 1974/4 (the known four). Captures are in `build/t0912-ios/captures-followups`. See
  `build/handoff/t0912-ios-followups.json`.
- 2026-11-05 (android, follow-ups) — Mike's three follow-ups, mirroring iOS. (1) A take from the Dashboard's offers list
  ends on the Job taken panel: the offer's area, day, window and reward at once, then the job read back as hers (the
  strip pins the door; left out if the read-back fails). Open job closes the panel and opens the job; Back to jobs closes
  it onto the offers list. Refusals unchanged. Red first: 6 partner tests failed on assertions. (2) The job detail is the
  Core `SnapSheet` over a still, full-height map again, with two stops: expanded at the band's foot under the fixed back
  button, and collapsed onto the pinned head (number through street and pay, plus 32), by a drag or a tap on the
  handle. Everything below the head scrolls inside the panel; at large text the head scrolls too. The map never pans,
  zooms or stretches; the pin and the lock chip sit where the band had them, and the Mapbox wordmark and attribution
  ride the panel's edge. The scrolled bar is gone. Core `SnapSheet` gains opt-in `stops`, `minSheetTop` and a live
  `minSheetHeight` (red first: 4 core tests); the customer set is 93 of 106 byte-identical to the finish set, the other 13 differing in system-bar rows only.
  (3) The chevron column is 28 dp, from 36, on every open-job row and its skeleton. Suites: core 388/0, partner 1273/0,
  customer 1867/0. Captures in `build/t0912-android/captures-followups`. See `build/handoff/t0912-android-followups.json`.
- 2026-11-05 — Chevron: Mike picked K5 from https://claude.ai/artifact/FVm23u2YFoU96ofp2kZVjm, "but even shorter". The build is a hairline edge about 12 pt wide (concept 18) with the faintest tint and the chevron centred. It is queued behind the interactive-map build.
- 2026-11-05 (ios, interactive map) — Mike: the map behind a taken job's panel should drag "the same as in
  Wolt/Foodora". On her own job with a coordinate, the map now pans and pinch-zooms (no rotate or tilt) behind the
  fixed canvas. The band, the back button, the mascot and the panel stops do not move, and the panel's drag never
  moves the map. The pin is a map annotation, and its street label now rides inside the pin's annotation view.
  Once she has moved the map off the job, a recentre button (Core `FloatingCircleButton`, `scope` icon, new
  `orders_map_recenter` in five locales) appears beside the back button and animates the map back to the job's
  framing. Core `PlaceMap` gains an opt-in `movedAway` binding: nil keeps every other map still. The tap-to-navigate on
  the band map is gone; Navigate stays in the run sheet. The pre-take blurred map, the Job taken panel's strip and
  the attribution are unchanged. Red first: core 5 of 7 and partner 3 of 14 failed on assertions. Suites: core 922/3
  (the catalog trio), partner 1437/0, customer 1979/4 (the known four). The customer captures are unchanged against
  T-0913's latest. Captures are in `build/t0912-ios/captures-interactive`. See
  `build/handoff/t0912-ios-interactive.json`; Android parity is listed there.
- 2026-11-05, Android, interactive map: the taken job's band map pans and pinch-zooms under a fixed panel, with
  rotation and tilt off, as on iOS. Its cover box is gone, and `JobMapMode.pans` keeps the cover on every other mode.
  The pin and street were already a Mapbox view annotation. A Core FloatingCircleButton (`orders_map_recenter`, five
  locales) beside the back button shows once the door is more than 8 dp or 5 % scale off its framing
  (`movedOffJob`), and eases back to the framing. A resize keeps her view while she has moved the map. The band's
  tap-to-navigate and its `navigateTo` parameter are gone; Navigate stays in the run sheet. The pre-take map, the
  strip and the attribution are unchanged. Red first: 4 of 47 failed on assertions. Suites: core 388/0, partner 1276/0,
  customer 1871/0, and both assembles OK. Captures are in `build/t0912-android/captures-interactive/partner`:
  - after a pan, the recentre shows and the panel's head does not move;
  - the recentred frame is byte-identical to rest;
  - a pan on the pre-take map leaves its frames byte-identical.

  No :core file changed. See `build/handoff/t0912-android-interactive.json`.
- 2026-11-05 (ios, chevron) — K5, shorter: every open-job row (Available, Active's soonest open, "Also open that day")
  ends in a 12-pt edge, from 28, the row's full height: a 1-px hairline in the outline, the primary at 5 %, and an
  11-pt semibold chevron centred in the primary (`ChevronEdge`). The loading placeholder draws the same edge without
  the chevron. The row stays one button. No test pinned the old width. Suites: partner 1438/0. swiftformat, swiftlint
  `--strict` and check-ios-symbols are clean, and the catalogs are unchanged. Captures, light and dark, are in
  `build/t0912-ios/captures-chevron`: available-populated and the detail's "Also open that day".
- 2026-11-05 (android, chevron) — K5, shorter, as on iOS. Every open-job row ends in a 12-dp edge, down from 28. That
  covers Available, Active's soonest open job and "Also open that day", since all three are `AvailableJobRow`. The
  edge runs the row's full height: a 1-px hairline in the outline on its leading side, the primary at 5 %, and the
  outlined chevron (10 dp tall) centred in the primary (`ChevronEdge`). The loading placeholder draws the same edge
  without the chevron. The row stays one button. No test pinned the old width. Suites: partner 1276/0, assemble OK.
  Captures, light and dark, are in `build/t0912-android/captures-chevron`: available-populated, and detail-browse
  `-also-open`, its panel scrolled to "Also open that day". Measured at 3x: the edge is 36 px, the hairline 1 px,
  and the tint is (242,249,252) on white.
- 2026-11-05 (ios, clip and wider chevron) — Mike: "Fix it, and make the chevron slightly wider". The clip: the panel's
  scroll region began flush under the head's last line with no edge, so a line scrolled under the head left a sliver
  just under "3–6 km from home". Once the panel's content has scrolled, the pinned head now reaches 8 further on the
  panel's surface and ends in the footer's hairline (`JobDetailFrame.headEdge`, read from the Orders tabs'
  `OrdersScrolledKey`). A line going under it is cut at that rule. At rest the edge is not drawn, so nothing moves or is
  covered. This holds in both panel positions and in every state, light and dark. At accessibility sizes the head
  still scrolls with the rest and draws no edge. The fix is partner-only: Core `SnapSheet` is untouched, so the
  customer set is unaffected. The chevron edge is 16 pt, from 12 (`AvailableJobCard.chevronColumn`), everywhere it
  appears, with the same hairline, tint, chevron and centring. Measured at 3x: a 1-px hairline, then 44 px of tint, then
  the card's edge. Suites: partner 1439/0, with 21 skipped; the one new test is the skipped capture
  `testMockScrolledUnderHead`. swiftformat, swiftlint `--strict` and check-ios-symbols are clean, and the catalogs and
  Package.resolved are unchanged. Captures, light and dark, are in `build/t0912-ios/captures-clip/partner`:
  - the detail scrolled 24, browse and progress, expanded and collapsed;
  - the "Also open that day" frame that showed the sliver, where the rows between the head and the rule are now pure
    surface;
  - available-populated.
- 2026-11-05 (android, clip and wider chevron) — Mike: "Fix it, and make the chevron slightly wider". Same fix as iOS.
  The clip: the panel's scroll region began flush under the head's last line with no edge, so a line scrolled under the
  head left a sliver just under "3–6 km from home". Now, once the panel's content has scrolled, the pinned head reaches
  8 dp further on the panel's surface and ends in the footer's rule (`HeadEdge` in `JobDetailFrame`, drawn over the
  scroll region's top while `panelScroll.value > 0`). A line going under it is cut at that rule. At rest the edge is
  not drawn, so nothing moves or is covered. It holds in both panel positions and in browse and taken, light and dark.
  At large text the head scrolls with the rest and draws no edge. The fix is partner-only: no :core file changed, so the
  customer set is unaffected. The chevron edge is 16 dp, from 12 (`ChevronColumnWidth`), everywhere it appears, with
  the same hairline, tint, chevron and centring. Measured at 3x: a 1-px hairline, then 44 px of tint, then the card's
  3-px border. The head rule is 3 px of outlineVariant, with 24 rows of pure surface above it. At rest the detail
  differs from the chevron round only in the wider edge of the "Also open that day" row. Suites: partner 1276/0,
  assemble OK. Captures, light and dark, are in `build/t0912-android/captures-clip`:
  - detail-browse and detail-progress scrolled 24 dp, expanded (`-scrolled`) and collapsed (`-scrolled-collapsed`);
  - browse's `-also-open`, the frame that showed the sliver;
  - available-populated.
- 2026-11-05 — Secondary buttons: Mike picked B1 from https://claude.ai/artifact/35MexoFiAZcGwTkMqE3UwK, "but to make as rounded as in B0". Outlined, dark label, icon in primary (Issue in error), capsule corners; applies to every partner tonal chip/button of this kind.
- 2026-11-05 (ios, secondary buttons B1) — `TonalChip` is now the one partner secondary button, used at every call site:
  Note and Issue, and the contacts (`ContactPills`) in the Active hero, the run sheet and the finished job's Customer
  section. `ContactPills`' private copy of the chip and `TonalChipFace` are gone. The button has no fill and a 1-pt
  `outline` capsule border. It is 40 tall inside a 44 tap target. The label is `onSurface`, in Nunito SemiBold 14. The
  18 icon is in the primary, or in the error for Issue. It uses the default button style, so it presses and dims like
  `CleansiaOutlinedButton`. The minimum inset is 10 (it was 12), so the hero keeps Navigate · Call · SMS on one row at
  393. The run sheet at 393 now fits all three on one row too, as concept B1 does (it was Navigate over Call · SMS).
  `TextInkTests` drops its two chip pins, since the chip's word is no longer blue. Tests: the icon box is 18, and a new
  test pins the ink, the label style and the tone's icon colours. Suites: partner 1442 run, 1 failed (that obsolete
  pin), then the affected classes 39/0 after the fix. swiftformat, swiftlint `--strict` and check-ios-symbols are
  clean, and the catalogs are unchanged. Captures, light and dark, are in `build/t0912-ios/captures-b1/b1`:
  detail-setoff `-contacts`, detail-progress `-photos-notes` and active-next.
- 2026-11-05 — Mike: "Yup, fix it". The collapsed panel at rest must end cleanly at its head, with no line cut by the bottom bar. Queued behind the B1 button build.
- 2026-11-05 (android, secondary buttons B1) — As on iOS. `TonalChip` is the one partner secondary button. Its call sites
  are Note and Issue (`NotesAndIssuesBody`) and the contacts (`ContactPills` → `PillGrid`) in the Active hero, the run
  sheet's set-off step and the Customer section (`CustomerCard`). There was no private copy to delete. The button has
  no fill and a 1-dp `outline` border on a `CircleShape`. It is 40 dp tall inside Material's 48 tap target. The label
  is `onSurface` in `labelLarge` SemiBold (`tonalChipLabel()`, which `ContactPills` also measures with). The 18-dp icon
  is in the primary, or in the error for Issue. Disabled, the label and icon dim to 0.38, as the outlined button's
  content does. The inset is 10 (was 12), and the fit's icon room grows to 18 + 6. The run sheet at 412 now fits
  Navigate · Call · SMS on one row, as on iOS. `PrimaryTextTest` (:core, test only) drops its chip pin, since the
  word is no longer blue. No new test was added: the change is visual and is proven by the measurements below.
  Suites: partner 1276/0, `PrimaryTextTest` 17/0, assemble OK. Measured at 3x: 120 px tall (40 dp) on the straight
  edge, a 3-px outline in (226,232,240) light and (51,65,85) dark, and the icon in (2,132,199) light and
  (56,189,248) dark. Captures, light and dark, are in `build/t0912-android/captures-b1`: active-next, and
  detail-setoff and detail-progress `-full`.
- 2026-11-05 (ios, collapsed at rest) — Mike: "Yup, fix it". The collapsed panel took 30 % of the room above the bar
  whenever the head needed less, so a line of the scrolling content showed cut between the head and the bar (browse:
  the day line's hours; taken: "How the job goes"). The collapsed panel is now exactly the handle (17), the measured
  head, and the head edge's 8, which the content's first gap leaves empty (`JobDetailFrame.peekHeight`). It is
  derived from the head as laid out, so it holds in every state, locale and theme. At accessibility sizes the head
  scrolls with the rest, and the panel keeps its old share. Core `SnapSheet` gains an opt-in `mapFocusHeight`: when it
  is nil, every stop is computed by the old expression. The expanded stop, the map, the attribution and the head edge
  are unchanged. Red first: 1 of the 3 new Core tests failed on its assertion. The drag test was then tightened to a
  drag that only the new stop keeps expanded. Measured at 3x on the nine frames: before, a cut line touched the bar's
  rule (0 clear rows); now 31 to 39 rows of pure surface (10.3 to 13 pt). The `TextInkTests` opening comment now
  states the secondary-button exception. Suites: partner 1442 run, 0 failed, 23 skipped; Core's SnapSheet classes
  44/0. The customer capture set is byte-identical to the interactive round's (109/109 PNG, 95/95 JSON).
  swiftformat, swiftlint `--strict` and check-ios-symbols are clean. The catalogs and Package.resolved are unchanged.
  Captures, light and dark (en) plus uk light, are in `build/t0912-ios/captures-collapsed/partner`: browse, setoff
  and progress, collapsed at rest.
- 2026-11-05 (android, collapsed at rest) — As on iOS. Collapsed, the panel was the larger of 30 % of the room above the
  bar and the head plus 32, so a line of the scrolling content showed cut at the bar (browse: the day line's hours; taken:
  "Setting off" and "How the job goes"). It is now the handle and the head as measured, plus the head edge's 8, which the
  content's first gap leaves empty (`collapsedHeight` in `JobDetailFrame`, tested). At large text the head scrolls and
  the panel keeps the sheet's share. With no move pinned, the sheet now ends above the system bar on a strip of surface,
  as on iOS, instead of running under it, so the collapsed stop ends cleanly there too. Core `SnapSheet` gains an opt-in
  `mapFocusHeight`; null keeps every stop where it was. The expanded stop, the map and the head edge are unchanged. Red
  first: 3 of 37 failed on assertions (2 core, 1 partner). Suites: core 390/0, partner 1277/0, customer 1871/0, and both
  assembles OK. Measured at 3x on the nine frames: before, the last ink above the bar belonged to the scrolling content,
  with 0 to 3 clear rows; now it is the head's last line, with 30 to 38 rows of pure surface (10 to 12.7 dp). The expanded
  rest frames match the clip and B1 rounds, except for the system-bar rows. The customer frames that draw `SnapSheet`
  are 43/43 byte-identical to the follow-ups set. Captures, light and dark (en), plus uk light, are in
  `build/t0912-android/captures-collapsed/partner`: browse, setoff and progress, collapsed at rest
  (`measure-collapsed.json`).
