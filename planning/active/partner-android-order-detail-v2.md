# Partner Android — Order Detail v2 (Wolt-style map + checklist + timer)

## What's wrong with v1 (Phase A/B/C ship)

Looking at the live screenshots:

1. **"Taking…" copy is wrong**. The label fires the instant the slide is released, but the action is in flight — "Taking…" reads like "still taking input." For a slide-to-confirm gesture the convention is a punchier confirmation tone ("Taking the order…" or just "Got it…"). Same for "Starting…".
2. **Vertical layout wastes the most valuable real estate**. The map is buried mid-scroll. For a cleaner heading to the job, the map should be the *backdrop*, not a sub-section. Wolt/Foodora put the map at the top full-bleed and let the content panel slide up over it.
3. **No checklist of what to clean**. The cleaner sees `General Cleaning · Deep Cleaning · Bathroom Cleaning · Kitchen Deep Clean · Window Cleaning · Pet hair deep-clean` as a flat list. They can't check items off as they go. For a 3-hour job with 8 distinct tasks this is exactly the kind of friction that gets things missed.
4. **No timer**. The cleaner has `EstimatedTime` (minutes from DTO) and a Started timestamp (status history entry where status=4) but the UI never surfaces "you've been at this for 1h 42m, estimated 3h." No countdown, no over/under indicator.
5. **No "everything I need" container**. As an employee with this app and only this app, I need: full address, customer phone, access codes, dog/kid/parking notes, list of tasks to tick off, pay confirmation, ETA timer, photo-before/after slots — all reachable in one screen without dialogs or external apps.

## Cleaner's actual journey through the screen

Mental model: cleaner opens the app while standing outside the building. What do they do, in order?

1. **Find the building** → map at the top, big, immediately visible
2. **Get into the building** → access instructions, security code, intercom name — needs to be visible fast
3. **Confirm the customer** → name + ability to ring/text if access fails
4. **Start the job** → primary action (Slide-to-start)
5. **Know what to do** → checklist of services/packages/extras with checkboxes
6. **Track time** → "you've been at it for 1h 23m / 3h estimated"
7. **Take before/after photos** → photo slots that make it obvious which is which
8. **Complete** → primary action flips to "Complete order" once everything's checked off
9. **Get paid** → pay confirmation visible at completion

Today's vertical scroll mostly satisfies (1)-(4) and (7) but completely misses (5), (6), and the integration of (8) with progress.

## The layout — Wolt-style stacked

```
┌─────────────────────────────────────┐
│                                     │
│         [ MAPBOX MAP ]              │  ← full-bleed top half
│              📍                     │     (~45-55% of screen height)
│                                     │
│       [ ← back ]                    │  ← floating back button
│                                     │
├═════════════════════════════════════┤
│        ▬▬                           │  ← grabber bar
│  #ORD-608A7194        [In Progress] │  ← compact header
│  May 22 · 9:00 · 1 200 Kč           │
├─────────────────────────────────────┤
│                                     │
│     [card content scrolls here]     │  ← bottom-sheet content
│                                     │
└─────────────────────────────────────┘
```

The bottom panel:
- Starts at ~55% of screen height (so the map gets ~45% in its "collapsed" state).
- Can be dragged up to ~90% (covering the map almost entirely).
- Can be dragged back down to ~30% (showing more map for navigation).
- Compact header row is **always** visible: order #, status pill, date + earnings — so the cleaner never loses the anchor when scrolling content.

Implementation: `BottomSheetScaffold` from material3 with `SheetValue.PartiallyExpanded` as initial state, three snap points (collapsed / partially / expanded).

## Section order inside the bottom sheet

1. **Header chip** (sticky, in the sheet's drag handle area): order number, status pill, date/time, pay
2. **Primary action** (sticky just below header? or first scroll item — TBD on implementation)
3. **Access card** — NEW. Bumps the existing access/special instructions to the very top with a 🔑 icon and very prominent treatment. "How to get in" is the cleaner's first blocker after navigating to the building.
4. **Customer card** — name + phone + Call/SMS buttons (Navigate moves to the map; we don't need a separate button when the whole map is right there). Only shown when assigned.
5. **Checklist card** — NEW. Services + packages + extras rendered as checkboxes with progress indicator ("3 of 7 done"). Stored locally (DataStore) keyed by order id since backend doesn't track per-task completion.
6. **Timer card** — NEW. Big stopwatch UI when status=InProgress, with estimated vs elapsed.
7. **Photos** — existing component.
8. **Notes & issues** — existing component.
9. **From customer** (general notes, special instructions if any) — moved out of "access" because access is now its own card.
10. **Payment summary** — existing component.
11. **Status timeline** — existing component, moved to the bottom (after-the-fact reference).

## New components

### A. `AccessCard` (NEW)
- Bright tinted card (slightly amber so it stands out from the standard surface cards).
- 🔑 icon + "How to get in" title.
- Body: `accessInstructions` from DTO.
- Renders only when `accessInstructions` is non-blank AND status is OnTheWay or InProgress.

### B. `CleaningChecklist` (NEW)
- Headline row: "Progress" + "X of N done" + horizontal progress bar.
- Section per source: Services, Packages, Extras.
- Each item is a checkbox row with the item name. Tap toggles.
- State persisted in DataStore as `Set<String>` of checked item ids/slugs, keyed by `order.id`.
- Visible whenever status >= Confirmed; interactive only when status = InProgress (so cleaners can't pre-check anything before starting).
- When all items checked → highlight the "Complete order" button (could even auto-prompt? No — explicit Complete tap is safer).

### C. `JobTimerCard` (NEW)
- Visible only when status = InProgress.
- Reads start time from `statusHistory` (entry where `status.value == 4`).
- Reads estimated minutes from `order.estimatedTime`.
- Displays:
  - Big stopwatch text: `1h 42m`, ticking every second
  - Subtitle: "Estimated 3h" with a small over/under indicator (green checkmark + "20m ahead" if elapsed < estimated, amber clock + "20m over" if elapsed > estimated)
  - Linear progress bar filling toward the estimate, turns amber when crossed
- Uses `produceState` + a `delay(1000)` loop while composable is on screen so the seconds tick.

### D. `OrderDetailsCompactHeader` (NEW — replaces the existing OrderDetailsHero)
- Lives in the bottom sheet's drag handle area, NOT as a separate scroll element.
- Two rows like the existing hero, but ~60dp tall total: order # + status pill / date + pay.
- White background, blends into the sheet rather than the gradient blue card.
- The brand-gradient hero treatment moves to a smaller chip at the top of the *scroll content* (so the brand still appears, just not blocking the map).

## Copy changes (the easy ones)

- `taking_order` (busy label): "Taking…" → "Taking the order…" (en), "Beru zakázku…" (cs), etc.
- `starting_order`: "Starting…" → "Starting the job…" (en), "Začínám úklid…" (cs), etc.
- New strings for everything below.

## What this means for backend / DTO

- **No backend changes required.** Everything is in the DTO already:
  - `selectedServices[].name`, `selectedPackages[].name`, `extras` map → checklist items
  - `statusHistory` → start time for the timer (where status.value=4)
  - `estimatedTime` → timer estimate
  - `accessInstructions` → access card
- Checklist state is **local-only** for v2. Future: backend `OrderTaskCompletion` table if we want progress shared across multiple assigned cleaners.

## What about iOS / web / customer Android

Out of scope. This is purely the partner-android detail screen.

## Implementation plan (executable phases)

### Phase 1 — Copy fixes (5 min)
1. Update `taking_order` and `starting_order` strings × 5 locales.

### Phase 2 — Layout shell (the Wolt panel)
1. Replace the current `Column` + `verticalScroll` with `BottomSheetScaffold` from material3.
2. Map fills the scaffold content; bottom sheet wraps the card stack.
3. New `OrderDetailsCompactHeader` lives in the sheet's `dragHandle` slot.
4. Old `OrderDetailsHero` → deleted (compact header replaces it).
5. Floating back button positioned over the map.

### Phase 3 — Access card
1. New `AccessCard.kt` with the amber surface treatment + 🔑 icon.
2. Inserted as the FIRST card inside the sheet content.

### Phase 4 — Checklist
1. New `CleaningChecklist.kt` component.
2. New `OrderChecklistRepository` backed by `androidx.datastore.preferences` storing `Set<String>` per `orderId`.
3. New `CleaningChecklistViewModel` (or hoisted state in OrderDetailsViewModel? — leaning ViewModel because the items live across recompositions and we want the checked state to survive screen rotation).
4. Item ids: services/packages use their `id`; extras use their slug.

### Phase 5 — Timer
1. New `JobTimerCard.kt` with a `produceState` ticker.
2. Computes elapsed from `statusHistory.firstOrNull { it.status?.value == 4 }?.createdOn`.
3. Estimated comes from `order.estimatedTime`.

### Phase 6 — Card re-order + cleanup
1. Re-order sections in `OrderDetailsScreen` per §"Section order".
2. Remove `MapPreviewCard` from the scroll (it's the backdrop now).
3. Remove `MascotEncouragement` (the new layout makes it redundant).
4. Move `FromCustomerNotesCard` to render only `notes` + `specialInstructions` (access went to its own card).

### Phase 7 — Build + verify
1. `:partner-app:compileDebugKotlin`
2. `:partner-app:testDebugUnitTest`

## Open questions (only answer if you want to override the defaults)

1. **Sticky-button at the bottom of the sheet?** Today the primary action sits at the top of the content stack. With the bottom-sheet layout, putting it as a sticky footer (Wolt-style "Confirm" button) is more thumb-friendly. **Default: sticky footer.**
2. **Checklist auto-completes?** When the cleaner checks the last item, should the "Complete order" button highlight / pulse, or do nothing special? **Default: highlight (gentle scale animation + tint shift, no auto-action).**
3. **Timer visibility for the InProgress phase only, or also for OnTheWay (as a countdown to scheduled start)?** **Default: InProgress only**. A pre-job countdown is a feature, but cleaners arriving 5 min late don't need extra anxiety.
4. **Local checklist state** — if cleaner uninstalls/reinstalls, progress is lost. **Default: accept** (rare enough; backend support is Phase 8/v3).
5. **Map dark mode while bottom sheet is partially expanded** — should the map dim or stay fully bright? **Default: keep fully bright** (cleaner is looking at the building, not the screen).

## What I'll need from you after I build

Just to run the app on a real order in each status and tell me what feels off. The big risks I expect to need iteration:

- Bottom sheet snap points feel wrong (probably need adjustment after seeing actual content height).
- Checklist visual density (too cramped / too sparse).
- Timer typography (it should feel like a stopwatch, not a footnote).
- Map dark theme contrast.
