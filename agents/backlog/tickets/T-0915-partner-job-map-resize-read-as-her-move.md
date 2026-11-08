---
id: T-0915
title: Partner job map — a resize is not read as her move
size: S
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

Found on 2026-11-07 while checking the T-0914 fix, and reported for Mike to decide on. It predates T-0914: master
`3e720a7e7` behaves the same.

On the partner job map she can move, a map she has not moved is read as moved when it changes size with room inside
its margins. The place is then not framed again at the new size, and the recentre button shows for a move she never
made.

MapKit reports the resized drawing to the map's delegate during the resize, before `PlaceMapCanvas.layoutSubviews`
frames the place again. `PlaceMapView.Placement.report` compares that drawing with `framedLook`, which still holds the
framing of the old size because `frame()` does not clear it, counts the difference as her move and sets
`keepsHerView`. `layoutSubviews` then keeps her view instead of calling `frameNow`.

Seen in hosted tests on the iOS 16.4 and 26.3 simulators:

- The map re-keyed for a 300 pt layout, then shrunk from 600 to 300 pt: the pin lands at y 10 instead of 160, and the
  binding is written true.
- The map resized from 600 to 500 pt with no re-key: the pin lands at y 110 instead of 133, and the binding is written
  true.
- In a SwiftUI page shaped like the job map, the page 300 pt shorter leaves the pin at y 7 instead of 157 and shows the
  recentre button, and the pinned footer 40 pt taller leaves it at y 137 instead of 157 until the footer shrinks back.
- In the same page, in a cycle like the keyboard's, the map is misread while the keyboard is up and framed again once
  it is down.

The first three behave the same with master's `PlaceMap.swift` on iOS 16.4. It was not seen in the app or on a device.
The probe and its logs are in `~/cleansia-work.noindex/crash-fix-20261107/` (`verify-recovery-probe/` and
`verify-recovery-*.log`).

`testANewSizeKeepsHerMoveAndFramesAMapSheLeftAlone` cannot catch it: its map has no delegate, because `hosted()` lets
its default `Placement` go and `MKMapView.delegate` is weak, and it resizes by only 4 pt.

## Acceptance criteria

- [x] A movable map she has not moved, resized by 100 pt or more with its `Placement` kept alive, frames the place at
  the new size, once when it is re-keyed first and once when the new size comes first: the pin is where `framed` puts
  it, `isAway` and `keepsHerView` are false, and the binding stays false.
- [x] A map she has moved still keeps her view across a new size.

## Out of scope

- The Android job map, which was not checked for this.

## Implementation notes

One way to fix it: `Placement.report` ignores what MapKit reports while the canvas's size differs from the size it last
framed the place or kept her view at, read through a read-only accessor for `framedSize`, and `frame()` clears
`framedLook`. That guard also covers the resize with no room that `report`'s `hasRoom` check was added for, so decide
whether that check is still needed.

## Work record

- 2026-11-07 — Filed. Not started.
- 2026-11-08 — Fixed in Core on `fix/partner-map-invalid-region` (PR #366), after Mike asked on 2026-11-07 for every
  bug found to be fixed. `Placement.report` now reads what MapKit draws as her move only while the canvas is the size
  it last framed the place or kept her view at, through a read-only `PlaceMapCanvas.framedSize`. The drawing MapKit
  reports at a new size, before `layoutSubviews` frames the place there, is no longer taken for a move. `frame()`
  also clears `framedLook`, so a re-keyed map holds no look of its old framing. The `hasRoom` check in `report` is
  gone: a map that found no room to frame the place has no framed size, so the new guard covers the resize under
  the margins that the check was added for. A hosted probe also showed that margins alone growing over the whole map
  leave MapKit's drawing where it was, on iOS 16.4 and 26.3.
  - Tests first. `testANewSizeKeepsHerMoveAndFramesAMapSheLeftAlone` is replaced by three hosted
    `PlaceMapMotionTests` that keep their `Placement` alive. A map she left alone is re-keyed and then shrunk by
    300 pt, or shrunk by 100 pt and then re-keyed; both times it is framed at the new size, with `isAway`,
    `keepsHerView` and the binding false. A map she moved keeps her view across a 100 pt resize, and recentring then
    frames the place at once at the new size.
  - At the branch head `717da64ae` the first two fail on the iOS 16.4 and 26.3 simulators: the pin lands at y 20
    instead of 95, and at y 120 instead of 145, with all three flags true. The third passes. With the fix, the 17
    PlaceMap tests pass on both simulators.
  - The probe named above, run again against the fix, frames every case on both simulators with the binding false:
    P1 puts the pin at y 160, P8 at y 133, and the SwiftUI page 300 pt shorter, the footer 40 pt taller and the
    keyboard-like cycle at y 157. Dragging the panel still lays the map out 0 times. The logs are in
    `~/cleansia-work.noindex/fixes-20261107/step2/` (`t0915-*`).
