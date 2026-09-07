---
id: T-0669
title: Home: one title system, neutral shadows, decoration removed
status: done
size: M
owner: frontend
created: 2026-08-30
updated: 2026-08-31
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context
- `.cleansia-title` renders `var(--cleansia-primary)` (blue) at fixed sizes while `pages/cleansia-customer/_home-common.scss:51` `.cl-title` renders `$text-dark` (near-black) fluid. Same page, two conventions, two colours - reported by the owner twice.
- The primary button carries a coloured glow `0 6px 16px rgba($primary,.3)` growing to `0 8px 20px rgba($primary,.38)`; the footer CTA carries `0 12px 32px rgba($primary,.24)`. Mobile uses neutral Material elevation.
- `hero.component.html` carries four `cl-hero__bubble` divs and a `cl-wave` SVG that appear nowhere else in the product, and every section carries `animate-on-scroll` with `delay-1/2/3`.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [x] **AC1** - Exactly one heading system exists. Headings render `Sky700 #0369A1`; `var(--cleansia-primary)` is used for the mark, links and primary actions only.
- [x] **AC2** - No `box-shadow` in `pages/cleansia-customer/` contains `rgba($primary, ...)`. A resting primary button is at most `0 2px 6px rgba(15,23,42,.12)`.
- [x] **AC3** - The four `cl-hero__bubble` elements and the `cl-wave` SVG are gone from `hero.component.html` and their SCSS is removed.
- [x] **AC4** - Entrance animation is limited to the first two viewports; no uniform `delay-N` stagger across every section.
- [x] **AC5** - `npx nx build cleansia.app` passes.

## Out of scope
- Partner and admin apps - they share the title component and must keep rendering.
- Any layout change - this ticket is colour, shadow and decoration only.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)
- 2026-08-31 - ground-truthed: PARTIAL, not shipped. Three ACs still fail, all in SCSS the delivering
  commit did not reach:
  - AC1 (one heading system): `.cl-title` is Sky700, but `_home-cta.scss:22` still colours
    `.cl-cta__title` `$text-dark` #0f172a, and that h2 renders on the home page via
    `home.component.html:11`. So the page shows headings in TWO colours - the exact defect the ticket
    was filed for. Separately `components/cleansia-title.component.scss:4` is still
    `var(--cleansia-primary)` = #0284c7 (Sky600, not Sky700); login and register override it, GDPR
    does not. Overlaps T-0678.
  - AC2 (no coloured shadows): `_home-footer.scss:141` has a RESTING primary button at
    `0 4px 12px rgba(7, 89, 133, 0.3)` - 30 % alpha against the 12 % neutral cap - plus a
    `drop-shadow(... 0.45)` at `:61`. More literal-hex coloured shadows survive in the same directory
    (membership:465, order-wizard:2450/2481/2010, recurring-bookings:229/648, track-order:76).
  - AC3 (bubbles removed): the markup is clean, but `_home-hero.scss:110-144` still compiles
    `.cl-hero__bubble` and its four modifiers with nothing rendering them. `@keyframes
    cl-float-bubble` is NOT dead - four other pages use it - so only the `&__bubble` block goes.
  AC4 and AC5 hold; the app builds.
- 2026-08-31 - done. The three failing ACs are closed, and the ground-truth pass had under-counted
  two of them.

  **AC1 - THREE heading systems existed, not two.** `.cl-title` and `cleansia-title` were the known
  pair (T-0678 merged them). Two more turned up only by grepping for rules that set their own colour:
  `.cl-cta__title` at `#0f172a` near-black, and `.cl-quote__title` with its own fixed size and weight.
  Both render on the home page. All four are one `<cleansia-title>` now; the page keeps only spacing.

  **AC2 - the coloured shadows split into two kinds, and only one kind was wrong.**
  - *Depth* - an offset and a blur, tinted with a brand hue. Six of these, all now neutral:
    the footer's resting primary button (30 % alpha, the worst), the footer mascot's drop-shadow,
    a membership card hover, two recurring-bookings hovers, and the quote card plus hero mascot,
    which were `rgba(12, 74, 110, ...)` - Sky900, a hue neither the AC's grep nor the ground-truth
    pass looked for.
  - *Rings* - `0 0 0 Npx`, drawn with box-shadow but semantically an outline: six focus and
    active-state rings in track-order, order-wizard and recurring-bookings. These KEEP their brand
    colour. A focus ring is exactly where the accent belongs, and greying it out would trade a real
    accessibility affordance for a passing grep. The AC's wording (`no box-shadow contains
    rgba($primary, ...)`) does not distinguish the two; the intent - "no coloured depth" - does.
    Recorded here rather than silently satisfied.

  **AC3 - the markup was clean, the SCSS was not.** `.cl-hero__bubble` and its four modifiers still
  compiled with nothing rendering them; removed. `@keyframes cl-float-bubble` was NOT removed with
  them - legal-pages, order-wizard, services-catalog and track-order all still animate with it. The
  orphaned `// Wave Divider` header with no rules under it is gone too.

  Three CI build gates pass, 68 test projects pass, lint has exactly the six pre-existing problems a
  clean tree has.
- 2026-08-31 - REOPENED. Closed on greps, not on the rendered page. A screenshot audit against
  the approved artboard found this ticket's own concerns still unmet:
  - The rules section carries a badge ("Naše pravidla"). The artboard has no badge on that section.
  - The rules fact rail renders as white bordered pills; the artboard is plain text separated by `·`.
  - Plus perks use check icons; the artboard uses `·` bullets.
  Verification for this ticket is now a screenshot diff, not a grep.
- 2026-08-31 - done, verified by screenshot. The rules badge is gone, the three facts are a plain
  line separated by hairlines rather than pills, and the Plus perks use mid-dots. Checked against
  `ref-shots/05-rules.png` and `06-plus.png` rendered from the artboard itself.

- 2026-08-31 - REOPENED again, then measured rather than eyeballed. A geometry
  extractor now dumps every element's box and computed styles from both the
  artboard render and the live page, and the two are diffed numerically. That
  found what looking could not:

  **`html { font-size: 14px }`.** Every token I had written in `rem` rendered at
  87.5 % of the artboard: a 58px display heading came out at 51, 38px at 33, 17px
  at 15. The whole page was one eighth too small, in every dimension that used a
  rem. All home tokens and partials are px now, because the artboard is px.

  **The content column was 1200, not 1312.** `$max-width` is 1200; the artboard
  is drawn on 1440 with 64px gutters. Every section was 112px narrower than the
  design, which is what read as "misaligned" — the grids were right, the frame
  was not. A `$home-width: 1312px` now carries it, kept separate so the wizard,
  catalogue, legal and tracking pages stay where they were designed.

  **`.cl-section` gutters were `2rem`** — 28px against the 14px root, not the
  artboard's 64.

  Also fixed: the navbar sat on white because the shell's 64px spacer no longer
  matched the 96px bar, so the hero could not run under it; the FAQ action was a
  full-width `cleansia-button` where the artboard has a compact pill; the Plus
  perks reflowed to 3+1 once the card reached its real width; the gallery had no
  top foam edge; `text-wrap: balance` was turning two-line card headings into
  three, so it is limited to the display sizes the artboard hand-breaks.

- 2026-08-31 - dark mode analysed before being touched, on the owner's
  instruction. It was broken structurally, not cosmetically: every colour in the
  home tokens was a SCSS variable baked at build time, so Sky700 headings scored
  2.5:1 on a dark ground and several nodes scored 1.0 — text the same colour as
  its own card. The hero's gradient and both foam edges stayed light, leaving
  bright slabs mid-page.

  The palette is now CSS custom properties declared once and restated for dark,
  so a theme can actually move them. A contrast probe that composites alpha and
  skips gradients (an earlier version read translucent tints as opaque and lied)
  reports **0 nodes below their WCAG floor in either theme**, down from 30.

  One deliberate change of value: the filled rules card is Sky700 rather than
  Sky600. Its 12px label is small text, and white on Sky600 is 4.0 against a 4.5
  floor. Same hue family, one step deeper.

## Review
<!-- reviewer / security / optimizer write verdicts here -->
