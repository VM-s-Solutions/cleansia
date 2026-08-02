---
id: T-0489
title: Partner order detail — the sheet cannot be dismissed to reveal the map (Android has no map-focus anchor; iOS's is drag-only)
status: draft
size: S
owner: architect
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: [T-0482]
stories: []
adrs: [0018]
layers: [architect, android, ios]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #8 (2026-08-02):** *"Hide the order panel on order detail to reveal the full map, both
apps."*

### Ground truth — PM-verified on `master` at `0e4ede1b`, and it is NOT symmetric

**First, the relocation:** there is **no map on the customer order detail on either platform** (PM
grepped both). The only order-detail map is on the **partner** apps. This ticket is partner-scoped.

| | Android `partner-app/.../orders/OrderDetailScreen.kt` | iOS `CleansiaPartner/.../Orders/OrderDetailView.swift` |
|---|---|---|
| Mechanism | M3 `BottomSheetScaffold` (`:268`) over a `MapboxMap` backdrop | `CleansiaCore` `SnapSheet` (`Components/SnapSheet.swift`) over a `MapProvider` full-bleed map |
| Anchors | `rememberStandardBottomSheetState(initialValue = PartiallyExpanded, skipHiddenState = true)` (`:256-258`) → **two reachable states: 75% peek and expanded** | **THREE** — `SnapAnchor` (`SnapSheet.swift:3-15`): `.mapFocus` **0.30**, `.peek` 0.75, `.expanded` 0.95 |
| Smallest the sheet gets | **75% of the screen.** The map is capped at ~25%, permanently | **30%. The map already reaches ~70%** |
| How the user gets there | there is no there | **drag only.** `SnapResolver.resolve` (`:22-40`) is driven by drag translation and fling velocity; **no button, no tap target, no programmatic entry point** anywhere in the partner app |

**The Android comment at `:251-254` states the constraint as a decision:** *"Sheet peek = 75% of
screen so the map shrinks to ~25% — just enough to read the location at a glance… Cleaner can still
drag down for a bigger map glimpse if they need to scout the route."* **The second sentence is false
on Android** — `skipHiddenState = true` with only `PartiallyExpanded`/`Expanded` means dragging down
from peek goes nowhere. A comment describing an affordance that does not exist, again.

**So the two halves of this ticket are different work:**
- **Android** needs a third anchor. That is a real M3 problem: `BottomSheetScaffold` does not offer
  an arbitrary third detent — the options are a lower `sheetPeekHeight` plus an expand target, a
  `ModalBottomSheet` with custom `SheetState`, or an anchored-draggable rewrite. **That is a
  mechanism decision, which is why this ticket is `architect`-owned.**
- **iOS** needs **discoverability**, not capability. `.mapFocus` exists and is unreachable by anyone
  who does not think to fling a sheet downward.

**One decision — "how does a cleaner get to the map?" — applied twice.** One ticket, two dev lanes,
per the T-0473 precedent.

## Acceptance criteria

- [ ] **AC1 — the affordance is CHOSEN against the alternatives and is the SAME on both platforms.**
      Button, drag handle tap, map tap, or drag-only-with-a-hint. The ruling names the choice and
      gives a why-not for the rejected ones. ADR-0018: a button on one platform and a fling on the
      other is a divergence that needs a sentence. Evidence: the ruling in `## Review`.
- [ ] **AC2 — Android reaches a map-focus state, and the mechanism is stated.** Given the partner
      order detail, When the cleaner uses the AC1 affordance, Then the sheet reaches **≈30% coverage**
      — the same fraction as iOS's `.mapFocus` (`SnapSheet.swift:9`), so the two apps agree on the
      number, not just on the gesture. The verdict states which M3 mechanism was used and why the
      other two were rejected. Evidence: before/after screenshots plus the stated mechanism.
- [ ] **AC3 — the return path is as easy as the exit.** From map-focus, the cleaner gets back to peek
      by the **same class** of affordance, not only by dragging. A cleaner who taps into a full map
      and cannot find the job again has been given a worse screen. Evidence: the round-trip recording
      on both platforms.
- [ ] **AC4 — the Android comment at `:251-254` is repaired.** *"Cleaner can still drag down for a
      bigger map glimpse"* is false today. Whatever ships, that comment becomes true or goes.
      Evidence: the diff.
- [ ] **AC5 — the sticky action footer does not become unreachable.** Android's `StickyActionFooter`
      (`:662`) and iOS's in-sheet actions live **inside** the sheet. At 30% coverage, "Start job" /
      "Complete" may be off-screen. State what happens: do the actions promote to an overlay, or is
      the cleaner expected to come back? **A cleaner who cannot press "Complete" from map-focus is a
      regression.** Evidence: the map-focus screenshot with the action state visible, both platforms.
- [ ] **AC6 — the map-focus state renders correctly when there is no map.** `canShowMap` is false on
      Cancelled and when coordinates are absent (`OrderDetailScreen.kt:240-248`; iOS
      `order.canShowMap` → `CleansiaColors.primaryContainer` fallback at `OrderDetailView.swift:88`).
      The affordance must be hidden or inert there — revealing a blank panel is worse than not
      offering it. Evidence: the no-coordinates screenshot, both platforms.
- [ ] **AC7 — `SnapSheet` is a Core component; changes to it are argued.** If iOS's half needs
      anything inside `CleansiaCore/Components/SnapSheet.swift`, say why it belongs in Core rather
      than at the call site, and note that **T-0482** is anchoring a mascot puck to the same sheet's
      moving edge. Evidence: the stated reasoning, or a diff that leaves Core untouched.
- [ ] **AC8 — a test that goes red against the current code (Gate 0.5 leg 1).** Android: an anchor/
      detent assertion proved to fail against the two-state `SheetState`. iOS: a `SnapResolver` or
      affordance-state assertion. Evidence: the red runs, then green, both platforms.
- [ ] **AC9 (Gate 0.5)** — Android `:partner-app` compile + `testDebugUnitTest` **un-cached**
      (`--rerun-tasks --no-build-cache`); iOS `xcodebuild build test` for `CleansiaPartner` on the
      **16.4 floor** + `CleansiaCore` from the package dir, SwiftFormat `--lint` / SwiftLint
      `--strict`. Screenshots are leg-3.

## Out of scope

- **The customer order detail.** It has no map. If the owner wants one there, that is **T-0484**'s
  concept work, where the data question ("track the cleaner") is raised explicitly.
- **Changing the map provider, style, camera or pin.** Untouched on both platforms.
- **The mascot puck** — **T-0482**. ⚠️ **It anchors to this sheet's moving top edge, so it must be
  built against the FINAL anchor set. This ticket goes FIRST.** Recorded on both.
- **The customer bottom sheet / any other sheet in either app.**

## Implementation notes

**Architect panel, short — author + 2 challengers + lead.** AC1's affordance choice and AC2's M3
mechanism are the two decisions; AC5 (actions at 30%) is the challenge the panel must survive, and it
is the one most likely to change the answer — if the actions cannot follow, the right design may be a
**full-screen map push** rather than a third detent.

**Fan-out after the ruling: two developer instances in parallel, one reviewer each.** Disjoint files:
- Android: `partner-app/.../features/orders/OrderDetailScreen.kt`
- iOS: `CleansiaPartner/Sources/Features/Orders/OrderDetailView.swift` (+ Core only per AC7)

**Shared-file lanes:** the Android file has no other sprint-15 claimant. The iOS partner
`OrderDetailView.swift` and `SnapSheet.swift` are both wanted by **T-0482** — **serialize, this
ticket first.**

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`.**

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #8).** **Relocated before ticketing:**
  the remark says "both apps" and there is no map on the customer order detail on either platform, so
  this is partner-scoped. **And it is not symmetric** — iOS already ships a `.mapFocus` anchor at 0.30
  and Android's sheet cannot go below 75%, both PM-verified at file:line. So the two halves are "add
  the capability" and "make the existing capability discoverable", which is why one decision governs
  two very different diffs. Needs a panel: AC1 and AC2 are choices, and AC5 may overturn both.

## Review
