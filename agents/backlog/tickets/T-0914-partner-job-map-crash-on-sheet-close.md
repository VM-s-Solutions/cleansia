---
id: T-0914
title: Partner job map — no crash when a sheet closes over it
size: S
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

Mike, 2026-11-07: "there is a bug that once I want to make a photo that I'm not able to get in or mention in, then the
app (partner) crashes". On the partner job detail he typed in the "Report that you cannot get in" sheet or the note and
issue sheet, then went to take a photo, and the app crashed.

The crash report from his iPhone 16 Pro (iOS 18.6.2, TestFlight partner 1.0.0 build 19, 2026-11-06 20:30) is an
`EXC_CRASH` `SIGABRT` from an uncaught Objective-C exception. Symbolicated against the archive's dSYM,
`-[MKMapView setRegion:animated:]` threw, called from `PlaceMapCanvas.frameNow(_:)` (`PlaceMap.swift:363`) inside
`PlaceMapCanvas.layoutSubviews()` (`PlaceMap.swift:351`); both lines are the same on master `3e720a7e7`. Under it,
SwiftUI dismisses a sheet, the presentation ends editing, the text view resigns first responder, the keyboard hides,
and the sheet presentation lays the page out again, the job map with it.

Evidence:

- Report: `~/cleansia-work.noindex/crash-20261107/logs/Retired/CleansiaPartner-2026-11-06-203005.ips`.
- dSYM: `~/Library/Developer/Xcode/Archives/2026-11-06/CleansiaPartner 2026-11-06 19.29.06.xcarchive` (UUID
  `E72D9469-EBCC-3C9A-A6F4-7C3D07050602`, load base `0x100b90000`).
- The symbolicated frames, and the logs of every run below: `~/cleansia-work.noindex/crash-fix-20261107/`.

## Root cause

`PlaceMapCanvas.framed(_:in:margins:)` works out the region MapKit fits inside the map's layout margins. The job map's
bottom margin is the part the job panel covers, and it is computed from the page's size, while the keyboard relayout
lays the map out at another size. When the margins are taller than the map, the room between them is negative, the
region's top and bottom swap, its latitude span is negative, and `setRegion` throws. A map of no size gave a NaN region
the same way. `layoutSubviews` only checked that the map had a size, and `recentre()` had no check at all.

Reproduced on the iOS 16.4 and 26.3 simulators: a canvas framed under a 660 pt bottom margin, shrunk to 516 pt and laid
out, throws `Invalid Region <center:+50.08215393, +14.43610799 span:-0.00229922, +0.00559855>` at `PlaceMap.swift:363`,
and recentring under margins taller than the map throws at `PlaceMap.swift:318`. MapKit handed a valid region under the
same margins does not throw.

The two partner sheets over the job map that close the keyboard when they are dismissed are the lockout report sheet
(`LockoutCard.swift`) and the note and issue sheet (`NotesAndIssuesSection.swift`: add or edit a note, report or edit an
issue). Both are SwiftUI sheets with medium and large detents, presented from the job panel inside `JobDetailFrame`;
both type into `CleansiaTextArea`, a `TextEditor` and so a `UITextView`; and both reach `PlaceMapCanvas.layoutSubviews`
through the same dismissal and keyboard relayout. The other sheets over the job (the camera, the early start, the work
contract) take no text, so they close no keyboard.

## Fix

In Core, `PlaceMap.swift`:

- `framed` gives no region when the margins leave the map no room, or when the region would pass the limits MapKit
  throws on; `frameNow` and `recentre` then hand MapKit nothing.
- A map that could not frame the place frames it at the next layout that has room: after a new size, or once its
  margins come down at the same size. `keepMarks` asks for that layout while the place waits for room, because new
  margins alone do not.
- While the margins leave no room, the map's delegate reads nothing MapKit draws as her move. Without this the movable
  job map read MapKit's drawing between such margins as moved away, showed the recentre button, and kept that view
  instead of framing the place once it had room.

## Acceptance criteria

- [x] The job map never hands MapKit a region it rejects, for any size and margins.
- [x] A map laid out with no room inside its margins frames the place once it has room: after a new size, or once its
  margins come down.
- [x] Recentring a map with no room waits for room instead of throwing.
- [ ] Mike confirms on a TestFlight build that closing the lockout sheet and the note and issue sheet with the keyboard
  up no longer crashes the app.

## Out of scope

- The Android job map. It was checked on 2026-11-07 and is not affected by this crash: its camera padding is kept at
  zero or more and never follows the panel's edge, the keyboard does not change the map's height, and Mapbox does not
  reject padding the way MapKit rejects a region. A separate Android crash, the logo and attribution padding going
  negative when the window shrinks while the panel moves, is T-0916.
- A map she left alone that a resize with room reads as her move. It predates this fix and is filed as T-0915.

## Work record

- 2026-11-07 — Filed and fixed on `fix/partner-map-invalid-region`.
- 2026-11-07 — Test-first. `PlaceMapFramingTests` gains the crash geometry and a sweep over sizes from 0 to 900 pt,
  margins from 0 to 1,200 pt and a 0.001 pt size. On master both failed: the crash geometry framed a region with a
  negative latitude span, and the sweep found 6,934 regions MapKit throws on. `PlaceMapMotionTests` gains three hosted
  tests: a still map and a movable map shrunk under their margins, which must frame the place once the margins come
  down and once a new size gives room, and recentring with no room. On master MapKit threw `Invalid Region` from
  `recentre`, and the movable map read the no-room drawing as moved. With the fix all fifteen PlaceMap tests pass on
  iOS 16.4 and 26.3. With the delegate's room check taken out, the movable-map test fails, so the check stays.
- 2026-11-07 — Suites on the iOS 16.4 simulator: CleansiaCore 930 tests, 0 failures; CleansiaPartner 1,442 tests,
  0 failures, 23 skipped, and the same at `-O` whole-module with MallocScribble, as TestFlight archives are optimized.
  CleansiaCustomer builds for testing, and `check-ios-symbols` is clean. The first full CleansiaPartner run, on a loaded
  machine, failed
  `PartnerOrdersVisualTests.testTheContractScrollsWithThePageAndTheSwipeStaysOnScreen` once (the contract's web view was
  measured before its text had laid out); it draws no map, and it passed alone and in the full run above.
- 2026-11-07 — `keepMarks` asks for a layout only while the place waits for room. Asking on every new margin laid the
  map out on every frame of a panel drag, with nothing to frame again. The fifteen PlaceMap tests pass on the iOS 16.4
  and 26.3 simulators, and the CleansiaCore suite (930 tests) passes on iOS 16.4.
