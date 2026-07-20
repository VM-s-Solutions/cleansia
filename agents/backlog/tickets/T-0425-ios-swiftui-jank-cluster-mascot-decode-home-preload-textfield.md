---
id: T-0425
title: "iOS — SwiftUI jank cluster: 1024px mascot decode on the main thread, Home pop-in, floating-label input drag"
status: done
size: L
owner: optimizer
created: 2026-07-16
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
priority: high
manual_steps: []
sprint: 12
source: owner remarks (dashboard pop-in, Plus header drags, input lag, animations slow everything) + remarks-sweep (wf_064232d3)
---

> Four owner jank complaints, three shared root causes. Ordered by ROI below.

## Root causes (file-anchored)
**(b+d) THE BIG ONE — full-size mascots decoded on the main thread every render.** The 6 mascot assets are
1024×1024 PNGs rendered at ~small sizes; `Mascot.swift` (`CleansiaCore/.../Components/Mascot.swift:11-13`)
decodes them on-demand on the main thread. This is why the **Cleansia Plus header "drags"** (its mascot), and
why **any animation slows everything** (a main-thread decode competing with the animation transaction). Highest
impact by far.

**(a) Dashboard pop-in.** A skeleton exists (`HomeSkeleton`, gated by `vm.firstPaintReady`) but the gate is too
narrow: `HomeTabViewModel.swift:157-168` flips ready on only orders+membership+packages, and `:148-152`
force-reveals after a 1.5s timeout. Catalog (`HomeTab.swift:80`) and recurring (`:81`, blocked on `isPlus`)
load LATE and independently, and each section (`HomeTab.swift:114/120/126/136`) is a bare `if` with no reserved
space → late arrivals shove the layout down one-by-one. The shell prefetch (`CustomerShellView.swift:112-121`)
never fetches them.

**(c) Floating-label input drag + cursor delay.** `CleansiaTextField.swift:56` drives the float animation off
`value: floating` (a derived state that flips during first layout) instead of the actual `focused` state, so
the label animates on first appearance; the `:71` `.offset(y:)` compounds it.

## Approach (ordered by ROI)
1. **Assets (fixes b+d — do first):** either re-export the 6 mascots from 1024² down to real render sizes
   (~220px, proper @2x/@3x) in `CleansiaCustomer/Resources/Assets.xcassets/*.imageset`, OR change
   `Mascot.swift` to decode+downsample once off the main thread (ImageIO `CGImageSourceCreateThumbnailAtIndex`)
   and cache the `UIImage` per case.
2. **Home preload (fixes a):** in `CustomerShellView.swift` prefetch (112-121) add `async let recurring =
   container.recurringRepository.refresh()` + the catalog load so every Home source loads in parallel up front;
   widen `firstPaintReady`/`startFirstPaintWatcher` (157-168) to also require loyalty and (when `isPlus`)
   recurring; add `.transition(.opacity)`/animation to the skeleton→content Group (66-72) and each conditional
   section (114-142) so late arrivals crossfade; demote the 1.5s ceiling (148-152) to a fallback only.
3. **TextField (fixes c):** `CleansiaTextField.swift:56` change `value: floating` → `value: focused`, and fold
   the `:71` offset into the same focus-driven transaction so the hint floats (not drags) and the cursor
   isn't blocked on a re-layout.

## Acceptance criteria
- [ ] **AC1** — mascots no longer decode at full 1024² on the main thread (downsampled assets or cached
  off-main decode); the Plus card + any mascot render without a visible hitch.
- [ ] **AC2** — Home shows the skeleton then crossfades to fully-populated content with no per-section layout
  shift; all sections start loading in the parallel prefetch.
- [ ] **AC3** — the floating label floats smoothly on first focus (no drag); the cursor appears immediately on
  first tap.
- [ ] **AC4** — measured on device (Instruments Time Profiler / SwiftUI) before+after where feasible; build+test
  green; swiftformat/swiftlint clean.

## Notes
- #8 ("animations slow everything") is expected to be largely resolved by AC1 (the main-thread decode is the
  shared culprit); re-profile after AC1 before scoping any residual animation work.

## Status log
- 2026-07-16 — filed from the remarks-sweep perf investigation.
- 2026-07-19 — ios: AC1 (mascot 600² downsample) + AC3 (focus-keyed float) verified already landed in
  dfd81d99; implemented AC2 — shell prefetch now also loads recurring + catalog in parallel,
  `firstPaintReady` widened to orders+membership+packages+loyalty (+recurring when Plus) with the 1.5s
  ceiling as fallback only, conditional Home sections crossfade via a `SectionVisibility` fingerprint,
  and `loadCatalog` is single-flight (the prefetch/Home race would otherwise double-fetch and flap
  `catalogState`). Both schemes build; customer tests green on iPhone 17 (26.x) and the 16.4 floor sim
  (only the 2 known Stripe-key-present failures); swiftformat 0.60.1 + swiftlint 0.65.0 --strict clean.
  AC4 device Instruments before/after not runnable here — needs owner hardware.
