---
id: T-0919
title: Partner job map — frames the place across the 180th meridian (iOS) and keeps the door on screen in windows shorter than the pin line (Android)
size: S
owner: Michael Chaban
created: 2026-11-07
updated: 2026-11-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: false
---

## Context

Both were found on 2026-11-07 while checking T-0914, and reported then without a change. Mike asked the same day for
every bug found to be fixed. Neither crashes, and both predate T-0914.

**iOS.** `PlaceMapCanvas.framed` cannot frame a place whose framed region crosses the 180th meridian:
`MKMapPoint.coordinate` wraps the longitude, so the span comes out negative. Before T-0914 MapKit threw there; since
T-0914 the map is simply not framed. No Cleansia market is near the meridian.

**Android.** `JobBandMap` (`JobMap.kt`) puts the door on the band's pin line with the camera's padding: the top is
twice the line less the map's height, and the bottom is the map's height less twice the line, each kept at zero or
more. When the map is not taller than the pin line, that padding is as tall as the map or taller. This happens in a
window under about 250 to 275 dp tall: a freeform or pop-up window, or the small half of a split screen. Mapbox keeps
the padding as given, and the door lands below the map's bottom edge. Keeping the top padding to the map's height would
only move the door onto that edge.

## Fix

**Android.** When the map is not taller than the pin line, the door is framed at the map's centre with no vertical
padding. Otherwise the framing is exactly as before. The rule is `pinTip` in `JobMap.kt`, tested in `JobMapRulesTest`.
The area map's framing cannot run past the map, because its top padding is zero and its bottom padding is the map's
height less the band, so it is unchanged.

**iOS.** `PlaceMapCanvas.framed` takes a longitude past ±180 as lying a world's width further on, where MapKit
gives no map point for it, and reads the east edge of a region narrower than the world east of its west edge where
the region runs across the meridian, its centre brought back inside ±180. A region that does not cross the meridian
is framed by exactly the same math as before. The rule is tested in `PlaceMapFramingTests`.

## Acceptance criteria

- [x] On iOS, the job map frames a place whose framed region crosses the 180th meridian.
- [x] On Android, where the job map is not taller than the band's pin line, the door is on the map, at its centre.
- [x] On Android, where the job map is taller than the pin line, the door sits on the pin line exactly as before.

## Out of scope

- The Android area map's framing, which cannot run past the map.
- The job panel in such a short window, where it has no room and collapses to nothing.

## Work record

- 2026-11-07 — Filed on `fix/partner-map-invalid-region` (PR #366). The Android half is fixed in a commit of its own,
  `9f6df3a7b`.
- 2026-11-07 — Android, measured on the API 35 emulator (1080 × 2400 at 420 dpi) with a throwaway probe outside the
  repository. It hosts the real `JobDetailFrame` and `JobBandMap` with a door in Prague, and logs the live `MapView`'s
  camera padding and the door's pixel from `pixelForCoordinate`.
  - Before, at the branch head `200585060`: at `wm size 1080x640` (a 244 dp window) the map is 147.4 dp tall and the
    pin line is at 156.2 dp. The 1080 × 387 px map has a top padding of 433 px, and the door is at (-1, -1), off the
    map. In portrait the 1080 × 2147 px map has a bottom padding of 1211 px, and the door is at (474.6, 468.1) px, on
    the pin line at 178.3 dp.
  - After: at `wm size 1080x640` the top and bottom padding are zero, and the door is at (474.6, 192.9) px, in the
    middle of the 387 px map. In portrait the padding and the door's pixel are exactly as before.
  - The new test fails with the rule set back to the old framing and passes with the fix. `:core`, `:partner-app` and
    `:customer-app` compile, and their unit suites pass: 390, 1,278 and 1,871 tests. The logs and screenshots are in
    `~/cleansia-work.noindex/fixes-20261107/` (`android-*`).
- 2026-11-08 — iOS, fixed in Core on the same branch. Two conversions broke across the meridian, not one.
  `MKMapPoint(_:)` gives no map point, (-1, -1), for a longitude past ±180, so the corner of a region whose own edge
  runs past the meridian was measured from a point that does not exist. `MKMapPoint.coordinate` wraps a point past the
  meridian round the world, so the framed region's east edge read west of its west edge and the span came out
  negative. `framed` now handles both, and a region that does not cross the meridian goes through exactly the same
  math as before.
  - Test first. `testAPlaceByThe180thMeridianIsDrawnWhereTheWholeMapWould` frames the area map, 6,000 m, of a place
    at longitude 179.99 and of one at -179.99, and checks that the region MapKit is handed draws the place where the
    whole map would, at the same scale. The test's drawing helper now measures a region's width from its span,
    because MapKit gives no map point past the meridian. At `65069147a` neither place is framed on the iOS 16.4 and
    26.3 simulators; with the fix the 18 PlaceMap tests pass on both.
  - A throwaway probe compared the old and the new `framed` bit for bit over 14 places, from Prague to Sydney and on
    either side of the meridian, 4 distances, 4 anchors, 81 sizes and 49 margins, on the iOS 16.4 simulator. Every
    one of the 759,355 cases whose region does not cross the meridian came out the same, or with no region from
    either. All 16,524 cases that changed run across it. The same sweep gave the same totals on 26.3. The probe and
    the logs are in `~/cleansia-work.noindex/fixes-20261107/step2/` (`t0919-*`).
  - Both places' framed regions are centred west of the meridian, so the test still passed with the step that brings
    the centre back inside ±180 deleted. The test now also frames a place at -179.95, whose framed region runs
    across the meridian centred east of it. With that step deleted the place is not framed and the test fails on the
    iOS 16.4 simulator; with it the 18 PlaceMap tests pass on 16.4 and 26.3. The logs are in
    `~/cleansia-work.noindex/fixes-20261107/step2/fix-366/`.
- 2026-11-08 — Shipped: #366 merged to master as 0bf4c34dc.
