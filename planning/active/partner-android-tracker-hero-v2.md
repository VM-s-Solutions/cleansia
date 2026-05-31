# Partner Android — Tracker Hero v2 (Foodora pattern + video mascot)

## What's in the Foodora reference screenshot

Looking at the Foodora "rider is heading to you" tile and inverting it
to "cleaner is cleaning":

| Foodora element | Purpose | Our equivalent |
|---|---|---|
| Background photo: map with route from courier → drop-off | Spatial context, "we know where they are" | Map backdrop (already shipped) |
| Two pin pucks: pink courier dot, dark customer dot, dashed route between | Live, animated locator | Single static pin (we don't track cleaner GPS) |
| Headline label: "Arriving by" | Time anchor | "Elapsed" / "Starts in" / "Scheduled" — phase-dependent |
| Big number: "15:30 – 15:40" | Single most-important reading | Same: live `H:MM:SS` for InProgress; date/time for others |
| Thin horizontal **gradient progress bar** (pink left → faded right) | "How far through?" — purely visual, no labels | THIS is the load-bearing pattern we're missing |
| **No per-step dots, no per-step labels** | Foodora reduces the stepper to a single continuous bar | Same — we replace our 5-dot row with a single continuous bar |
| Tagline: "Got your order!" — bold | Phase headline | Same: "Cleaning in progress" / "Heading out" / etc. |
| Subtitle: "The rider is heading to you" | One-sentence context | Same: pacing line ("On track" / "12m ahead") |
| Floating call/SMS chip — clipped right of the tile | Action shortcut | We already have Call/SMS in the Customer card below |

The key insight: **Foodora doesn't show five discrete steps to the
customer**. They show one continuous progress bar that fills smoothly
as the order moves through phases. Our current 5-dot row is closer to
Wolt's older interim-state tracker; Foodora went smoother and bolder.

## Plan — what changes

### A. Drop the 5-dot stepper, replace with a single gradient progress bar

The dots are visually heavy and force the eye to count. Foodora's bar
is faster to read. Mapping:

| Status | Bar fill % | Bar color |
|---|---|---|
| New / Pending | 5% | brand-blue |
| Confirmed | 25% | brand-blue |
| OnTheWay | 50% | brand-blue |
| InProgress | 75% baseline, animates from 75 → ~95 as elapsed approaches estimated | brand-blue → brand-blue-lighter gradient |
| Completed | 100% | green |
| Cancelled | 100% | danger red |

Bar height ~6dp, fully rounded, sits below the headline/subtitle.

Smaller-than-Foodora detail: above the bar, two short labels —
**"Step name"** on the left, **"Phase N / 5"** on the right — so the
cleaner still has the discrete-step info when they need it. Sub-300dp
horizontal real estate.

```
┌────────────────────────────────────────┐
│                                        │
│         [MASCOT — video or PNG]        │
│                                        │
│              Cleaning                  │
│         The job is in progress         │
│                                        │
│              7:05:18                   │
│         1h 10m ahead of schedule       │
│                                        │
│  Cleaning                  Step 4 / 5  │
│  ████████████████████░░░░░░░░░░░░      │
│                                        │
└────────────────────────────────────────┘
```

### B. Mascot strategy — phase-specific PNG, with cleaning being the special "video" case

Looking at the web mascot library:

| Phase | Mascot |
|---|---|
| New (offer) | `mascot-leaning.png` (cleaner relaxed, leaning on mop — "ready when you are") |
| Confirmed | `mascot-ready.png` (cleaner with broom + duster + vacuum, fully equipped) |
| OnTheWay | `mascot_thumbs_up.png` from web (with black bg stripped) OR keep current partner-app `mascot_thumbs_up.png` |
| InProgress | **`mascot-cleaning-in-progress.mp4`** (the animated video) |
| Completed | partner-app `mascot_thumbs_up.png` (re-used — "nice work") |
| Cancelled | (no mascot) |

The user asked specifically: **separate mascots per phase, or one
mascot?** My recommendation:

- **Phase-specific is worth it BECAUSE** each phase actually means
  something different to the cleaner emotionally. Foodora doesn't do
  this because their customer doesn't care what the courier "looks
  like" at each step — only "where are they?". For our cleaner
  staring at the screen for hours, phase-specific mascot is a tiny
  cue that the app is reading the situation correctly.
- **BUT** the only one that genuinely earns animation is InProgress
  (the work-in-flight moment). The others can be PNGs.

So: **PNG for all phases, animated MP4 only for InProgress**. This
keeps the bundle small, the APK fast, and the visual difference
between "static phase" and "actively-working phase" meaningful.

### C. Playing the cleaning video on Android

Three viable options:

1. **`androidx.media3.exoplayer` with PlayerView in `AndroidView`** —
   the production-grade choice. Supports MP4 + WebM, looping, no
   audio, auto-mute. ~500KB of deps already partially present? Let
   me confirm in the plan — if Media3 isn't wired, we'd add it.
2. **Animated WebP via Coil** — already on Coil 3.x. The web's
   `mascot-cleaning-in-progress.webp` is **1.7MB**, animated, no
   audio. Drop-in via `AsyncImage(model = ...)` with Coil's
   GifDecoder/AnimatedImageDecoder. Zero new dependencies.
3. **Static PNG fallback** — `mascot-cleaning.png` already exists. No
   animation, but no risk.

**Recommendation: option 2 (animated WebP via Coil)** — already-paid
dep, ships as a single asset, plays automatically, no PlayerView
lifecycle headache, no audio plumbing. The MP4 video the user pointed
to has video-codec license complications when embedded in apps;
animated WebP is the Android-friendly equivalent.

Asset to copy: `mascot-cleaning-in-progress.webp` → partner-app
`res/raw/mascot_cleaning_in_progress.webp` (filename normalized).

### D. Smooth transitions

Foodora-style transitions, what we can do in Compose:

1. **Mascot crossfade** when status changes. Wrap the mascot in
   `AnimatedContent` keyed by status. Default 300ms fade is fine.
2. **Bar fill animation** when the bar's target % changes. `animateFloatAsState`
   with `tween(durationMillis = 600, easing = FastOutSlowInEasing)`.
   First load uses the actual target with no animation; subsequent
   updates animate.
3. **Headline/subtitle text change** also via `AnimatedContent` —
   slide+fade so the change reads as "the screen knows something
   updated."
4. **Sheet content height** when the hero swaps elements — the parent
   `Column` is a vertical layout; `animateContentSize()` on the hero's
   inner content Column smooths card resize when subtitle text
   length changes (e.g. "On track" → "12m over").

### E. Black-bg PNGs need processing

`mascot_thumbs_up.png` from web has a black background. We already
have the right partner-app one in `drawable-nodpi/mascot_thumbs_up.png`
(transparent). The ones I want to import (`mascot-leaning.png`,
`mascot-ready.png`, `mascot-cleaning.png`) need to be verified —
they appeared on transparent in the Read tool, so they're OK to
import as-is. Verify with `magick identify` before copying.

## Component architecture

```
OrderTrackerHero  (REVISED)
├─ MascotSlot           (AnimatedContent keyed by status)
│   ├─ PNG case         (Image with bobbing animation)
│   └─ Animated case    (AsyncImage of animated WebP, no bobbing —
│                        the video has its own motion)
├─ HeroHeadline         (AnimatedContent — phase title in bold)
├─ HeroBigText          (big number — timer/countdown/date)
├─ HeroSubtitle         (AnimatedContent — pacing line)
└─ ContinuousProgressBar  (NEW — replaces the OrderStatusProgressBar)
    ├─ Top row: step name + "Step N / 5"
    └─ animated gradient fill bar
```

The existing `OrderStatusProgressBar` composable can stay around for
the historical status timeline card (still uses the 5-dot pattern
because that card is genuinely listing each transition with timestamps
— different purpose). New `ContinuousProgressBar` is hero-only.

## Per-phase content (final)

| Status | Mascot | Headline | Big text | Subtitle | Bar % |
|---|---|---|---|---|---|
| New / Pending | leaning | "New offer" | scheduled date/time | "Slide below to take this job" | 5 |
| Confirmed (>30min) | ready | "Confirmed" | "Starts in 2h 14m" | "Cleaning is scheduled for [date]" | 25 |
| Confirmed (<30min) | ready | "Heading out soon" | "Starts in 22m" | "Time to head out!" | 25 |
| OnTheWay | thumbs_up | "On the way" | "Arriving 09:00" | "Slide to start once you arrive" | 50 |
| InProgress | **animated webp** | "Cleaning" | live `H:MM:SS` | "On track" / "12m ahead" / "8m over" | 75→95 |
| Completed | thumbs_up | "Done" | "Done in 2h 47m" | "Nice work!" | 100 |
| Cancelled | — | "Cancelled" | — | (reason if any) | red bar 100 |

## i18n additions

New keys (× 5 locales):
- `tracker_headline_new` "New offer"
- `tracker_headline_confirmed` "Confirmed"
- `tracker_headline_confirmed_soon` "Heading out soon"
- `tracker_headline_on_the_way` "On the way"
- `tracker_headline_in_progress` "Cleaning"
- `tracker_headline_done` "Done"
- `tracker_step_counter` "Step %1$d / %2$d"

Existing `tracker_step_*` short labels stay — used in the
continuous bar's left label.

## Implementation phases

1. **Asset copy** — bring mascot PNGs + animated WebP into
   `partner-app/src/main/res/`. Strip any black backgrounds with
   ImageMagick (same pattern as before).
2. **Mapbox dep check** — confirm Coil already supports animated
   WebP without extra setup (it does in Coil 3.x via
   `coil3.gif.AnimatedImageDecoder`; may need one tiny config line).
3. **`ContinuousProgressBar` composable** — new file.
4. **`OrderTrackerHero` revamp** — replace the inline 5-dot stepper
   with `ContinuousProgressBar`, add headline above big text, add
   `AnimatedContent` wrappers for mascot/headline/subtitle, swap in
   the per-phase mascot resource.
5. **Cleaning-phase mascot** — render `AsyncImage(mascotWebpUri)` for
   InProgress; static PNG for everything else.
6. **i18n** — 7 new keys × 5 locales.
7. **Build verify + visual review per status**.

## Open questions

1. **Animated WebP vs MP4 ExoPlayer** — defaulting to WebP for
   simplicity. If you really want the MP4 (better quality, can have
   audio later), I add Media3 ExoPlayer instead. **Default: WebP.**
2. **Step counter "Step 4 / 5" in the bar label row** — useful for
   the cleaner who can't tell from the bar fraction alone how many
   total phases there are. **Default: yes, include it.**
3. **Bar gradient direction** — left→right horizontal gradient brand
   → brand-lighter (Foodora pink→faded pink), or solid brand fill?
   Solid is simpler and reads cleaner on a brand-colored hero
   background. **Default: solid brand fill, no gradient on the bar
   itself** (the background gradient on the hero card carries the
   "polished" feel already).
4. **Mascot crossfade duration** — 300ms is the Material default.
   **Default: 300ms.**
5. **Bar fill animation** — 600ms tween. **Default: yes, 600ms with
   FastOutSlowInEasing.**

## Not in scope

- Lottie (still don't need it — Coil's animated WebP gives us the
  same outcome with zero new deps).
- ExoPlayer (deferred unless WebP doesn't ship well).
- Voice prompts ("You're 10 minutes ahead!") — overkill for v1.
- Multiple mascot characters / per-cleaner avatar — single mascot
  identity across the platform stays.
