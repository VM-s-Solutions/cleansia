# Partner Android — Tracker Hero (Wolt/Foodora-style)

## What the user asked for

Combine three things that today live as separate cards in the sheet:
1. **Order progress** (the 5-step bar at the top of the sheet)
2. **Job timer** (currently a card lower down, only when InProgress)
3. **A cleaning mascot animation** (currently no mascot on this screen)

Into one Wolt/Foodora-style "live tracker" hero block.

## What Wolt and Foodora actually do (the reference)

Both apps' live order tracking has a near-identical pattern. I'm
deliberately calling out what's load-bearing vs decorative so we don't
chase the wrong details:

**Load-bearing UX (we should copy):**

- **One hero block, top of screen** that owns the "where am I in this
  order" question. Not three separate sections.
- **State-driven illustration** that swaps based on phase: a courier on
  a bike for "on the way", a chef at a stove for "cooking", a checkered
  flag for "delivered". The illustration is the emotional anchor — it
  tells the customer "we're working" without them having to read.
- **Big primary number/phrase** under the illustration. Wolt uses the
  ETA ("Arrives by 19:42"); Foodora uses elapsed/estimated minutes.
  This is the single piece of text the user looks at every 30 seconds.
- **Single-line stepper underneath** showing the phase chain — same
  meaning as our current 5-step bar, just shorter & integrated.
- **One sentence of friendly copy** under the stepper ("Your courier
  is on the way!" / "Sit tight — your food is being prepared"). Wolt
  varies this per phase. Foodora keeps it terse.
- **Subtle continuous motion**: bouncing dots on the stepper, gentle
  bobbing of the illustration, soft pulse on the current step. Not
  flashy — just enough to read as "live, not frozen."

**Decorative-only (we should skip):**

- Multi-color gradient sweeps across the hero (Wolt does it; only
  works because they're not on a brand-blue surface already).
- Real-time map zoom-in as courier nears destination (we don't track
  cleaner location in real time).
- Estimated minutes ticking down (we don't broadcast cleaner ETA to
  the customer; for the cleaner-facing app this would be the
  scheduled-cleaning countdown).
- Confetti on completion (we already have a snackbar toast on
  Complete; over-engineering for a B2B cleaner-facing screen).

## What's different about our app

Critically: **this is the cleaner's screen, not the customer's**. So
the framing inverts:

| Customer-facing (Wolt/Foodora) | Cleaner-facing (our partner app) |
|---|---|
| "Where's my order? When does it arrive?" | "What am I doing? How long have I been at it?" |
| ETA prominent | Elapsed time prominent |
| Reassurance ("almost there!") | Pacing ("on track" / "20m over") |
| Static illustration | Same — no need for live animation matching cleaner's actual posture |

So our hero's primary number isn't ETA — it's the **elapsed timer**
(when InProgress) or the **scheduled cleaning time** countdown (when
Confirmed / OnTheWay) or just the **scheduled date/time** (when New).

## Per-phase content table

What the hero shows at each lifecycle step:

| Status | Mascot | Big text | Subtitle | Stepper |
|---|---|---|---|---|
| New (offer) | resting | scheduled date/time, e.g. "Wed 28 May · 09:00" | "Slide below to take this job" | ●○○○○ |
| Confirmed (mine, pre-arrival) | thumbs_up | countdown to scheduled start, e.g. "Starts in 2h 14m" | "Time to head out!" once <30min | ●●○○○ |
| OnTheWay | thumbs_up | scheduled time, e.g. "Arriving 09:00" | "Slide to start once you're there" | ●●●○○ |
| InProgress | thumbs_up (or "cleaning" if we generate one) | live elapsed `H:MM:SS` | "12m ahead of schedule" / "On track" / "8m over" | ●●●●○ |
| Completed | thumbs_up | "Done in 2h 47m" | "Nice work!" | ●●●●● |
| Cancelled | (none, hide mascot) | "Cancelled" | reason if available | red bar |

## Component architecture

One new composable, replacing two old ones:

```
OrderTrackerHero  (NEW — top of sheet content)
├─ Mascot           (state-driven Image with bobbing animation)
├─ Big primary text (timer / countdown / date)
├─ Subtitle text    (encouragement / pace status)
└─ OrderStatusProgressBar  (KEPT — already stylized, just inlined)
```

Old components going away (or being inlined):
- `JobTimerCard` → folded into hero. Standalone card deleted.
- The standalone `OrderStatusProgressBar` row in `OrderDetailsCompactHeader` → moves into the hero. Compact header keeps just the order # + date + pay metadata.
- `MascotEncouragement` was already deleted in v2.

## Visual layout (single hero card)

```
┌─────────────────────────────────────┐
│                                     │
│             🧹 (mascot)             │   ← 120dp, bobs vertically
│                                     │
│           1:42:08                   │   ← big timer, brand color
│        12m ahead of schedule        │   ← subtitle, green when ahead
│                                     │
│      ●━━━━●━━━━●━━━━○━━━━○         │   ← inline stepper
│     New  Conf  OnWay InProg Compl   │
│                                     │
└─────────────────────────────────────┘
```

Sheet's compact header above it stays slim (order # / date / pay
chip), the tracker hero is the FIRST scroll item inside the sheet
content. No drag handle changes.

## Mascot animation: what kind?

Three options, in order of fidelity:

1. **Compose-only bob** (lightweight). Vertical translateY on a sine
   wave, ~2s cycle, 6dp amplitude. Uses the existing mascot PNGs.
   Pros: zero new deps, runs forever cheap. Cons: every phase shows
   the SAME mascot (we can swap which PNG based on status, but the
   motion is identical).
2. **Lottie animation per phase** (heavy). Lottie dep already in
   `libs.versions.toml`, just not wired in partner-app. Pros: full
   character animation, can react to status. Cons: someone has to
   *author* or source the Lottie JSON files — and good cleaning-themed
   Lottie animations aren't trivial to find. Adds ~1MB to APK.
3. **Compose ImageBitmap + spritesheet** (medium). Would need bespoke
   art per phase. Way more setup than the visual payoff justifies for
   a B2B cleaner screen.

**Recommendation: option 1 (Compose-only bob)** with a status →
mascot mapping for variety. We get the "alive" feel without
introducing a new dep or asset pipeline. If you later want a real
animation per phase, Lottie swap is straightforward — the hero API
stays the same.

## Implementation phases

### Phase 1 — New `OrderTrackerHero` composable
- Inputs: `OrderItem`, `OrderStatus?`, optional `startedAtEpochMillis`.
- Internal `produceState` ticker that fires every 1s when phase is
  InProgress (drives the live timer); idle otherwise to save battery.
- Switch on status to pick:
  - mascot drawable
  - primary text (timer / countdown / date string)
  - subtitle (with green/amber/neutral tint when pacing matters)
- Embeds existing `OrderStatusProgressBar` at the bottom.
- Wrapped in `OrderSectionCard` (or a custom slightly-elevated card
  with gradient subtle wash).
- Mascot has the bobbing animation via `rememberInfiniteTransition`
  driving a `translateY` graphics layer.

### Phase 2 — Wire into the screen
- Insert `OrderTrackerHero` as the first card in `OrderDetailsSheetContent`.
- Remove the `OrderStatusProgressBar` block from `OrderDetailsCompactHeader`
  (header stays as order # + date + pay).
- Remove `JobTimerCard` from the screen (delete the file).
- Existing `OrderDetailsFormat.formatOrderDateTime` and the timer
  format helpers in `JobTimerCard` get pulled out into shared format
  helpers so the new hero can use them.

### Phase 3 — i18n for subtitle copy
- `tracker_subtitle_new` — "Slide below to take this job"
- `tracker_subtitle_confirmed_far` — "Cleaning is scheduled for %1$s"
- `tracker_subtitle_confirmed_soon` — "Time to head out!"
- `tracker_countdown_starts_in` — "Starts in %1$s"
- `tracker_subtitle_on_the_way` — "Slide to start once you arrive"
- `tracker_subtitle_in_progress_on_track` — "On track"
- `tracker_subtitle_in_progress_ahead` — "%1$s ahead of schedule"  (reuse `timer_ahead`)
- `tracker_subtitle_in_progress_over` — "%1$s over the estimate"     (reuse `timer_over`)
- `tracker_completed_in` — "Done in %1$s"
- `tracker_completed_subtitle` — "Nice work!"
- (Cancelled keeps existing status_cancelled string)

Total ~8 new strings × 5 locales = 40 strings. Reuse 3 existing
`timer_*` strings, so really 5 new keys × 5 = 25.

### Phase 4 — Build verify
- `:partner-app:compileDebugKotlin` clean
- `:partner-app:testDebugUnitTest` clean
- Visual review on each status: New, Confirmed (>30min), Confirmed (<30min), OnTheWay, InProgress (under/on/over estimate), Completed, Cancelled

## What this delivers

- The cleaner sees ONE focal element at the top of the sheet that
  carries everything important: who I am in the lifecycle, how I'm
  pacing, and a friendly "we're cooking" feel.
- The standalone Timer card disappears — its info is now in the hero,
  but in a more glanceable form.
- The standalone progress-bar-in-compact-header collapses into the
  hero too, so the very top of the sheet is just `order # · date · pay`
  metadata (slim) before the hero takes over.
- Mascot adds the Wolt/Foodora "this is a live thing, not a static
  receipt" feel without introducing animation tooling.

## Open questions (with defaults)

1. **Mascot mapping per phase**: I propose `resting` for New,
   `thumbs_up` for Confirmed/OnTheWay/Completed, `thumbs_up` for
   InProgress (no cleaning-action PNG exists). Should we generate
   a 5th mascot PNG ("mascot_cleaning" — with mop in hand) for
   InProgress to make the phase swap more meaningful? **Default:
   ship with thumbs_up for InProgress; we can swap in a cleaning
   PNG later without code changes.**

2. **Live ETA countdown when Confirmed?** "Starts in 2h 14m" updates
   every minute as the scheduled time approaches. **Default: yes**,
   ticking on a 60s timer (not 1s) when the phase is Confirmed and
   the scheduled time is in the future.

3. **Hero card background**: solid surface vs a soft brand-tinted
   gradient wash. Gradient reads more "tracker", solid keeps it
   consistent with sibling cards. **Default: soft top-down gradient**
   from `primary.copy(alpha = 0.06)` to surface — subtle enough to
   not fight with the cards below.

4. **Bobbing animation always-on, or only when "active" phases?**
   Always-on costs near-nothing (transform-only animation, no recomp
   cost), and the screen is foregrounded only when the cleaner has
   it open. **Default: always-on except on Cancelled** (no mascot at
   all on Cancelled).

5. **Status timeline card at the bottom of the sheet**: with the
   hero now showing the inline stepper, the historical timeline
   card down below is redundant for active orders but still useful
   on Completed for the "when did each step happen at what time"
   detail. **Default: keep the timeline card unchanged**; the hero
   stepper and the timeline card serve different purposes (where am
   I NOW vs when did each transition happen).

## Not in scope

- iOS partner app.
- Customer-facing tracker (different DTO surface, will be a
  separate component when we ship the customer order detail).
- Real-time push updates to drive the hero when status changes
  while the screen is foregrounded (today the cleaner pulls; future
  Phase D could subscribe to FCM).
- A Lottie-based mascot (kept Lottie out of scope per option 1
  above — easy to retrofit later if we want).
