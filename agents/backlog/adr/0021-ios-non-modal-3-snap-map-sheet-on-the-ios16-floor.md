# ADR-0021 — iOS partner OrderDetail's 3-snap map-backed sheet is a CUSTOM non-modal Core container on the iOS-16.0 floor (the floor stays 16.0); native `.presentationDetents` remain the way for MODAL sheets

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-27
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** ios | cross-cutting (a `CleansiaCore` component + a standing reviewer/Gate-DP note on the partner OrderDetail surface and every later map-backed sheet)
- **Extends / refines:** **ADR-0014** (the iOS-16.0 deployment floor + D6′ "the full-bleed `OrderDetail` map + 3-snap sheet go through `MKMapView`/`UIViewRepresentable`") — this ADR is the **sheet-side companion** to that map-side note: it answers *what the sheet itself is* on the 16.0 floor and **keeps the floor at 16.0** (it does **not** amend the target). It also **refines ADR-0018 D3**: D3's table maps Compose `ModalBottomSheet` → `.sheet`+`.presentationDetents` for the **modal** customer booking sheet; this ADR adds the missing mapping for the **non-modal** partner OrderDetail sheet (an always-present sheet *over* a full-bleed map), which `.sheet` cannot model. Consumes **ADR-0013 D6** (the `MapProvider` seam) and the **§7.6 D1** additive-`MapProvider` precedent.
- **Ticket:** T-0307 Slice C (the OrderDetail shell) · **Consumers:** sprint-12 **§7.8** (the T-0307 Understand-pass record); the living companion `architecture/decisions/ios-app-architecture.md`; the `patterns-mobile.md` iOS section (the harvest); the Gate-DP reviewer note.

> This ADR freezes **one decision**: how the partner OrderDetail's "3-snap, always-present sheet over a
> full-bleed map" (the Wolt/Foodora layout) is built on the **iOS-16.0** floor, where SwiftUI's custom
> `.presentationDetents` (`.fraction`/`.height`) are **16.4+** and the only 16.0 detents are `.medium`/`.large`.
> It ships **no Swift code** — the concrete component is built in T-0307 Slice C. Once `accepted` it is
> immutable — supersede, never edit.

> **Why an ADR and not a §7.8 "record, not ADR" line (the bar, per the §7.6/§7.7 precedent):** the other
> four T-0307 decisions (a/c/d/e) **apply** accepted ADRs and are recorded in §7.8 with no new ADR. This one
> does not — it is a **genuine new trade-off** with rejected alternatives that must be defended on the
> record: it weighs **bumping the ADR-0014 floor** (a load-bearing, device-reach decision the owner already
> made) against **building a custom container** against **collapsing parity to a 2-detent modal**. A decision
> that could move the deployment floor, or that sets "the one way iOS does a non-modal map sheet," is exactly
> the kind ADR-0018's CH-5 verdict reserved for a superseding/extending ADR rather than a living-doc fold-in.

---

## Context

T-0307 (the partner order work-loop) ports the Android partner **OrderDetail** screen. The Android source is
**not** a modal bottom sheet — it is a **non-modal `BottomSheetScaffold`** integral to the screen
(`OrderDetailScreen.kt:173-245`):

- `rememberStandardBottomSheetState(initialValue = PartiallyExpanded, **skipHiddenState = true**)` — the sheet
  is **never dismissed**; there is no hidden state.
- A **full-bleed map is always behind it** (`MapBackdrop`, `:256-299`), with the map's camera **padded by the
  sheet peek height** so the address pin stays in the visible upper sliver.
- `sheetPeekHeight = screenHeight * 0.75f` (`:172`) — the resting position shows the sheet at ~75% with ~25%
  map; the cleaner can **drag the sheet down** to focus the map or **up** to expand the work content; a compact
  header (`OrderDetailCompactHeader`) is **always visible** as the drag handle so order #/status/date/pay never
  scroll away.

This is a **3-intent** surface — *map-focus* (sheet low), *peek* (the 0.75 resting), *expanded* (sheet high) —
where the sheet and the map are **one composed layer**, not a modal presented over a screen.

ADR-0018 D3's mapping table already maps Compose `ModalBottomSheet` → SwiftUI `.sheet` + `.presentationDetents`
(`.medium`/`.large`) — but that row is explicitly for the **modal** customer **booking** sheet (Services /
WhenWhere / Confirm), which *is* a modal presented over the booking flow. The **partner OrderDetail** sheet is
a different animal (non-modal, always present, layered over a live map), and D3 has **no** row for it. iOS-16.0
makes the choice load-bearing:

**The iOS-version fact (Lead-verified, 2026-06-27).** `.presentationDetents` ships on **iOS 16.0** with **only**
`.medium` and `.large`. The **custom** detents — `.fraction(_:)` and `.height(_:)` — and `.presentationDetents`
on a non-`.sheet` container are **iOS 16.4+**. ADR-0014's floor is **iOS 16.0**. So a `.sheet`-based port on the
floor can offer **at most two** detents (`.medium`/`.large`), and a `.sheet` is **modal** (drag-to-dismiss,
nothing behind it but a dimmed screen) — it cannot be the **always-present-over-a-live-map** layer the Android
layout *is*. ADR-0014 D6′ already routes the **map** through `MKMapView`/`UIViewRepresentable`; this ADR is the
**sheet** half of that same surface.

This is **one decision** — "what the OrderDetail sheet is on the 16.0 floor" — because the three candidate
answers are mutually exclusive and each carries a different, lasting cost (a device-reach loss, a small reusable
component, or a parity divergence). It is decided once, here, and reused by every later map-backed sheet.

---

## Decision

> **The partner OrderDetail's 3-snap, always-present sheet-over-a-full-bleed-map is a CUSTOM, non-modal
> `CleansiaCore` component — a `GeometryReader` + drag-gesture container with three snap offsets (map-focus /
> peek≈0.75 / expanded) — layered in the same view tree above the full-bleed map (`fullBleedMap(coordinate:)`,
> ADR-0021's sibling §7.8 decision (a) + ADR-0014 D6′). The ADR-0014 iOS deployment floor STAYS 16.0 — this
> ADR does NOT bump it. Native `.sheet` + `.presentationDetents` remain the canonical way for MODAL sheets
> (the customer booking sheet, ADR-0018 D3); a non-modal always-present sheet over a live map is the one case
> that uses this custom container, because `.sheet` cannot model "nothing is dismissed, a map is behind it."**

### D1 — A custom non-modal 3-snap container in `CleansiaCore` (the chosen option)

- **What it is.** A small, reusable `CleansiaCore/Components` view — sketch name **`SnapSheet`** (the exact Swift
  surface is the dev's; the *contract* is fixed): a container that renders **its content over a full-bleed
  backdrop**, parked at one of **three snap offsets** — **map-focus** (sheet low, ~map-dominant), **peek**
  (the resting ≈0.75·height parity), **expanded** (sheet high) — driven by a **drag gesture** with rubber-band
  + velocity-aware snapping, on a `GeometryReader`-measured height. It is **16.0-safe**: `GeometryReader`,
  `DragGesture`, `offset`, and `withAnimation` are all far below 16.0; **no `.presentationDetents`, no `.sheet`**.
- **Why it preserves what D1-of-ADR-0018 makes non-negotiable.** The Android layout is *the sheet is always
  present, layered over a live map* — that **is** the screen's information architecture (ADR-0018 D1: same
  layout/region arrangement). A modal `.sheet` would change the layout (a dimmed screen behind, drag-to-dismiss,
  no live map) — an ADR-0018 D1/Gate-DP **failure** (a layout change, not a component swap). The custom container
  keeps the **exact** layout and flow; only the *control mechanism* is bespoke because the platform has no native
  non-modal multi-anchor sheet on the floor.
- **The map composes with it (the sibling §7.8 decision (a) + ADR-0014 D6′).** The full-bleed map is
  `fullBleedMap(coordinate:)` on the `MapProvider` (an additive method, §7.6 D1), implemented
  `MKMapView`-via-`UIViewRepresentable` inside `MapKitMapProvider`. The container passes the **current sheet
  offset** (or peek height) down so the map's camera is **bottom-padded** to keep the address pin in the visible
  sliver — the `MapBackdrop` `EdgeInsets(0,0,bottomPaddingPx,0)` parity (`OrderDetailScreen.kt:273-281`). The
  feature/VM imports **neither** the container's internals nor MapKit (reviewer #7/#12).
- **It is a `CleansiaCore` component, not a feature-local view** — because T-0307 is the **first** map-backed
  sheet but **not the last**: the customer surfaces (T-0314 addresses, possibly the booking map) and any future
  partner map screen reuse it. Homing it in Core (the `Cleansia*` brand-skin-over-native posture, ADR-0018 D2)
  makes it "the one way iOS does a non-modal map sheet" — a new canonical archetype, harvested into
  `patterns-mobile`.

### D2 — The ADR-0014 floor STAYS iOS 16.0 (rejecting the bump)

- The owner's Q-IOS-01 answer (ADR-0014) set **16.0 for old-device reach** (iPhone 8/8 Plus/X, 2017+). That is a
  **deliberate, owner-made, device-reach** decision. **A bottom-sheet implementation detail is not a reason to
  re-open it** — especially when a 16.0-safe option (D1) delivers full parity. Bumping to 16.4 to get
  `.fraction`/`.height` would drop the **16.0–16.3 device sliver** (devices on 16.0–16.3 that haven't updated)
  for a single screen's convenience — a bad trade the owner's stated priority forbids.
- **This ADR therefore does NOT amend the floor.** ADR-0014's `platforms: [.iOS(.v16)]` and reviewer #11
  ("no `@available(iOS 17)` always-on path") stand unchanged. The only addition is a **sheet way**, not a floor
  change. (Recorded explicitly so a future reader does not mistake this ADR for a floor revision — it is the
  opposite: it is the decision that *let the floor stay*.)

### D3 — Native `.presentationDetents` remains the way for MODAL sheets (the boundary)

- ADR-0018 D3's `ModalBottomSheet → .sheet + .presentationDetents` mapping **stands** for genuinely **modal**
  sheets — the customer **booking** sheet is presented over the booking flow, *is* dismissible, and has nothing
  layered behind it; `.medium`/`.large` (16.0) are the right native fit and `.large`+a custom middle is reachable
  if the floor later rises. The custom `SnapSheet` is **not** a license to hand-roll every sheet.
- **The discriminator a reviewer applies:** *is the sheet modal (presented over a screen, dismissible, nothing
  live behind it) or non-modal (always present, layered over a live map/canvas, never dismissed)?* **Modal →
  native `.sheet`+`.presentationDetents`. Non-modal-over-a-live-backdrop → the `SnapSheet` container.** The
  partner OrderDetail is the canonical non-modal case; the customer booking sheet is the canonical modal case.

### D4 — Scope guard

This ADR decides **only** the OrderDetail sheet *mechanism* on the 16.0 floor and the modal/non-modal boundary.
It does **not**: write Swift code; change the OrderDetail *content/flow/branding* (that is ADR-0018 D1 parity,
checked by Gate-DP against `OrderDetailScreen.kt`); re-decide the map (ADR-0014 D6′ + §7.8 (a)); change the
deployment floor (D2 — it stays 16.0); or touch the customer booking sheet's native-`.sheet` mapping (D3). A
future iOS-16.4+ floor could **internally** re-implement `SnapSheet` over `.presentationDetents`
`.fraction`/`.height` with **no change to its call sites** (the container is the seam) — a living-doc note if/when
the floor rises, not a new ADR (the same migration-note posture as ADR-0014 D2′).

---

## Alternatives considered

- **(i) Bump the ADR-0014 floor to iOS 16.4** (so `.fraction`/`.height` custom detents are available and the
  sheet is a native `.sheet`). **Rejected (D2).** It re-opens an **owner device-reach decision** (16.0 for
  2017-phone reach) for a single screen's convenience and **drops the 16.0–16.3 device sliver**. Worse, even
  at 16.4 a `.sheet` is **modal** — it still cannot be the **always-present-over-a-live-map** layer the Android
  layout is, so the floor bump would buy custom detents but **still not** match the non-modal layout. The bump
  pays a real reach cost for a partial fix. The owner's priority forbids it.
- **(iii) Collapse to a 2-detent native modal `.sheet`** with `.medium`/`.large` (the ADR-0018 D3 table's
  stated mapping, 16.0-safe). **Rejected as the OrderDetail answer** (kept as the *fallback*, below). It is the
  lowest-risk *code* path, but it **changes the layout** — a modal `.sheet` dims the screen behind it and is
  drag-to-dismissible, so the **always-present full-bleed map behind the sheet is gone**, and the 3-anchor
  map-focus/peek/expanded intent collapses to 2 anchors with no map-focus state. That is an **ADR-0018 D1/Gate-DP
  layout divergence** (not a permitted component-only swap) — the very thing Gate-DP assertion #3 rejects. It
  would also fork the OrderDetail UX from Android's, breaking the "one product" parity. Recorded as the
  **explicitly-approved fallback** only if D1's custom container proves infeasible in build (e.g. a gesture/perf
  blocker) — and **only** with the divergence noted and re-approved, never silently.
- **(ii-variant) A feature-local custom sheet (not in `CleansiaCore`).** Rejected — it would be re-built for the
  customer map surfaces (T-0314), fragmenting "the one way." Homing it in Core (D1) makes it the reused archetype.
- **A third-party bottom-sheet package.** Rejected — same posture as ADR-0013 D2 / ADR-0014's TCA rejection: a
  small `GeometryReader`+`DragGesture` container is a platform-primitive solution; a dependency for it is
  unwarranted surface, and it would diverge from the "native SwiftUI, no extra framework" rule (ADR-0018 D2).

---

## Consequences

**Cheaper / safer:**
- **The floor stays 16.0** — the owner's 2017-device reach is preserved; no device sliver is dropped for a sheet.
- **Full layout/flow parity with Android** — the always-present-sheet-over-a-live-map IA (ADR-0018 D1) is kept
  exactly; Gate-DP passes (the map-focus/peek/expanded 3-anchor intent is reproduced, not collapsed).
- **One reusable archetype** — `SnapSheet` in `CleansiaCore` is "the one way iOS does a non-modal map sheet";
  the customer map surfaces (T-0314) reuse it instead of re-deciding, and a future 16.4 floor re-implements it
  *internally* with no call-site change (the container is the seam).
- **The modal/non-modal boundary (D3) keeps native `.sheet` the default** — the custom container is the *narrow
  exception* (non-modal over a live backdrop), not a license to hand-roll sheets; the customer booking sheet
  stays native.

**More expensive (new obligations — recorded, not silent):**
- **A bespoke gesture/animation component to build and test** — drag, rubber-band, velocity snapping, and the
  map-camera-padding coupling are more code than a native `.sheet`. Mitigated by homing it once in Core and a
  focused test (the snap-offset resolver is a pure function: `(dragEnd, velocity, height) → snap`).
- **A reviewer must enforce the modal/non-modal boundary (D3)** so the custom container doesn't sprawl into
  cases a native `.sheet` should own — added as the reviewer note below.
- **A future 16.4-floor simplification is real (small) follow-on work** — recorded; not planned now.

**Plan impact (parallel, this change — no renumber/restructure):**
- **No new/removed tickets.** T-0307 Slice C builds `SnapSheet` + the OrderDetail shell over it. The sibling
  §7.8 decisions (a/c/d/e) are **recorded** in sprint-12 §7.8 with **no** ADR (they apply accepted ADRs).
- **The reviewer gate gains #29** (below); **Gate-DP** on T-0307 cites `OrderDetailScreen.kt` and confirms the
  non-modal 3-snap layout is reproduced (not collapsed to a modal).

---

## How a reviewer verifies compliance (the delta this ADR adds)

29. **The partner OrderDetail sheet is the custom non-modal `SnapSheet` container, not a modal `.sheet`.**
    The OrderDetail renders the sheet **layered over the always-present full-bleed map** in one view tree, parked
    at **three** snap offsets (map-focus / peek≈0.75 / expanded) via a drag gesture; there is **no** `.sheet`
    presentation and **no** dismiss state for it (the Android `skipHiddenState=true` parity). The map camera is
    bottom-padded by the sheet offset so the address pin stays visible. **Findings:** the OrderDetail sheet built
    as a modal `.sheet` (dimmed screen behind, drag-to-dismiss, no live map) — an ADR-0018 D1/Gate-DP **layout**
    divergence; collapsing the 3 anchors to 2 without the noted+re-approved fallback (alternative iii); a
    `SnapSheet` used for a genuinely **modal** sheet where native `.sheet`+`.presentationDetents` is correct
    (D3 boundary); any `@available(iOS 16.4)` gate that would lift the floor (D2 — the floor stays 16.0).

**Test contract (T-0307 Slice C):** **TC-IOS-SNAP** — the snap-offset resolver is a pure function:
`(dragTranslationEnd, velocity, containerHeight) → SnapAnchor ∈ {mapFocus, peek, expanded}` with the resting
default = `peek` (≈0.75); a downward fling from `peek` resolves `mapFocus`, an upward fling resolves `expanded`;
the resolver never produces a hidden/dismissed anchor (the `skipHiddenState` parity). (The map-padding coupling
is asserted at the view level; the resolver is the unit-tested core.)

---

## Roles affected

No new code roles beyond a `CleansiaCore` component. Catalog edit (same change, per the pattern-evolution loop):
`agents/knowledge/patterns-mobile.md` iOS section gains the **`SnapSheet`** harvest (the non-modal 3-snap map
sheet = the one way; native `.sheet`+`.presentationDetents` = the modal way; the modal/non-modal discriminator)
+ the **`fullBleedMap(coordinate:)`** additive-`MapProvider` line, so a modal `.sheet` for the OrderDetail or a
collapsed 2-detent port is a catalog deviation. The living companion
`agents/architecture/decisions/ios-app-architecture.md` gains a T-0307 OrderDetail-sheet section + a rollout row.
ADR-0014's header gains an "Extended by ADR-0021 (the sheet half of D6′)" pointer; ADR-0018's gains a "Refined by
ADR-0021 (the non-modal sheet mapping)" pointer (status-block pointers, not body edits — the ADRs are immutable).

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted the custom-container decision + the floor-stays-16.0 call + the modal/non-modal boundary;
challengers (floor-cost, parity-fidelity, scope/YAGNI) attacked; the Lead verified the iOS-version detent facts
and the Android non-modal layout and adjudicated. **Verdict: all challenges RESOLVED; zero blocking; consensus
reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (floor) | A custom gesture container is more code + risk than just bumping to 16.4 and using native `.fraction` detents — why pay that? (MAJOR) | REBUT | D2 + Alt (i): the bump re-opens the **owner's** 2017-device-reach decision and drops the 16.0–16.3 sliver; and **even at 16.4 a `.sheet` is modal** — it still can't be the always-present-over-a-live-map layer, so the bump buys detents but **not** the non-modal layout. The custom container is 16.0-safe AND full-parity; the floor stays. |
| CH-2 (parity) | Isn't the 2-detent native modal `.sheet` (the ADR-0018 D3 mapping) good enough — why is it a divergence? (MAJOR) | REBUT | Alt (iii) + D3: a modal `.sheet` **changes the layout** (dimmed screen behind, drag-to-dismiss, no live map) and collapses the 3-anchor map-focus/peek/expanded intent to 2 — an **ADR-0018 D1/Gate-DP layout** divergence (not a permitted component-only swap). D3's table row is for the **modal** booking sheet; the partner OrderDetail is **non-modal** and needs its own mapping (this ADR). Kept as the noted+re-approved fallback only. |
| CH-3 (scope/YAGNI) | A reusable Core `SnapSheet` with 3 anchors is speculative — T-0307 needs one screen; isn't this the over-designed shape §7.6 D1 rejected? (MODERATE) | DEFEND | D1 + Consequences: unlike §7.6 D1's speculative *overlay/polygon* (no data, no consumer), this archetype has **two concrete consumers** (partner OrderDetail now; customer map surfaces T-0314) and reproduces a **specific** Android layout (the 0.75 peek + map-focus + expanded), so it is grounded, not guessed. Homing it in Core is the "one way" posture, not gold-plating. The 3 anchors **are** the Android intent, not invented. |
| CH-4 (boundary creep) | Won't a custom sheet become the default and crowd out native `.sheet` everywhere? (MODERATE) | CONCEDE + REVISE | D3 + reviewer #29: added the explicit **modal/non-modal discriminator** and a reviewer finding for a `SnapSheet` used where a modal `.sheet` is correct. The custom container is the **narrow exception** (non-modal over a live backdrop); native `.sheet`+`.presentationDetents` stays the default (the customer booking sheet). |
| CH-5 (future floor) | If the floor rises to 16.4 later, is this throwaway work? (MINOR) | DEFEND | D4: `SnapSheet` is the seam — a 16.4 floor re-implements it **internally** over `.presentationDetents` `.fraction`/`.height` with **no call-site change** (the same migration-note posture as ADR-0014 D2′). Not throwaway; the call sites are floor-independent. |

**Affirmed unchallenged:** the floor stays **16.0** (no amendment); the Android OrderDetail is **non-modal**
(`skipHiddenState=true`, full-bleed map always behind, 0.75 peek — `OrderDetailScreen.kt:172-245`); the map is
`fullBleedMap(coordinate:)` via `MKMapView`/`UIViewRepresentable` (ADR-0014 D6′ + §7.8 (a)); native
`.presentationDetents` stays the modal-sheet way (ADR-0018 D3); the `SnapSheet` is a `CleansiaCore` brand-skin
component (ADR-0018 D2).

**Lead verification (iOS-version API facts, 2026-06-27):** `.presentationDetents` = iOS **16.0** with **only**
`.medium`/`.large`; `.fraction(_:)`/`.height(_:)` custom detents = iOS **16.4**; `GeometryReader`/`DragGesture`/
`.offset`/`withAnimation` = far below 16.0 (the custom container is 16.0-safe). Android: `BottomSheetScaffold` +
`rememberStandardBottomSheetState(PartiallyExpanded, skipHiddenState=true)`, `sheetPeekHeight = screenHeight*0.75`,
full-bleed `MapBackdrop` with `EdgeInsets(0,0,sheetPeekPx,0)` camera padding (`OrderDetailScreen.kt:172-281`).

**Escalations to the owner:** none — the floor is **kept** at the owner's chosen 16.0 (this ADR exists precisely
so the floor does **not** move); the decision is an architecture/implementation call within ADR-0014's settled
floor and ADR-0018's settled parity principle.
</content>
</invoke>
