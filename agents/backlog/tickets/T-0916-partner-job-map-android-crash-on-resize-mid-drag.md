---
id: T-0916
title: Partner job map on Android — no crash when the window shrinks while the panel moves
size: S
owner: Michael Chaban
created: 2026-11-07
updated: 2026-11-07
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
---

## Context

Found on 2026-11-07 while checking whether the Android job map shares the T-0914 crash. It does not share that one,
but it has a crash of its own, and it predates T-0914: master `3e720a7e7` has the same code.

On the partner job detail the Mapbox wordmark and attribution sit 8 dp above the job panel's live top. `JobBandMap`
(`JobMap.kt`) computed that inset as the map's height minus the panel's top, plus 8 dp, with no lower bound, and the
wordmark and attribution hand it to `PaddingValues(bottom = …)`, which throws
`IllegalArgumentException: Padding must be non-negative` during composition.

`MainActivity` handles rotation and screen-size changes itself, so the job detail stays composed when its map gets
shorter. `SnapSheet` then moves its stops to the new height, but while a drag or a settle holds the panel, the panel
keeps its old top until that drag or animation ends. A map that shrank under it has the panel's top below its bottom
edge, the inset goes negative, and the app crashes. The panel's own placement was already clamped; the inset was not.
iOS clamps the same inset at zero in `JobBandMapLayout.attributionInsets`, so this is an Android-only difference.

Reproduced on the API 35 emulator (1080 × 2400 at 420 dpi) with a throwaway activity outside the repository that hosts
the real `JobDetailFrame`, `SnapSheet` and `JobBandMap` with `MainActivity`'s `configChanges`:

- Dragging the panel down and rotating to landscape mid-drag left a 315 dp map under a panel top at 549.8 dp, and the
  app crashed in the wordmark's `PaddingValues` (`JobMap.kt:307` on master).
- Shrinking the job detail to 300 dp tall while a tap on the handle animated the panel crashed the same way.
- The same shrink or rotation with the panel at rest did not crash: the panel moved to its new stop first.

## Fix

`JobBandMap` clamps the inset at zero before it adds the 8 dp, as iOS does. Until the panel reaches its new stop, the
wordmark and attribution sit 8 dp above the map's bottom edge.

## Acceptance criteria

- [x] Rotating the device, or shrinking the window, while the job panel is dragged or animating does not crash the job
  detail.
- [x] With the panel at rest, the wordmark and attribution still sit 8 dp above its top.

## Out of scope

- The job map's camera padding. It never follows the panel's live top and is already kept at zero or more.

## Work record

- 2026-11-07 — Filed and fixed on `fix/partner-map-invalid-region`, in a commit of its own, so that it can be kept or
  dropped apart from T-0914.
- 2026-11-07 — With the fix, the same activity on the same emulator: the shrink during the handle animation left the
  unclamped inset at down to −448.2 dp for nine frames; one rotation mid-drag left it at −210.6 dp, and two rotations
  during the handle animation left it at down to −340.2 dp and −267.6 dp. None crashed. In two further rotations
  mid-drag, the first frame logged at the new size already had the panel at its new stop.
  `:partner-app:testDebugUnitTest` (1,277 tests) and core's `CleansiaMapUsageTest` pass. The logs of the runs before
  and after the fix are in `~/cleansia-work.noindex/crash-fix-20261107/`.
