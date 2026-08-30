---
id: T-0669
title: Home: one title system, neutral shadows, decoration removed
status: ready
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
- [ ] **AC1** - Exactly one heading system exists. Headings render `Sky700 #0369A1`; `var(--cleansia-primary)` is used for the mark, links and primary actions only.
- [ ] **AC2** - No `box-shadow` in `pages/cleansia-customer/` contains `rgba($primary, ...)`. A resting primary button is at most `0 2px 6px rgba(15,23,42,.12)`.
- [ ] **AC3** - The four `cl-hero__bubble` elements and the `cl-wave` SVG are gone from `hero.component.html` and their SCSS is removed.
- [ ] **AC4** - Entrance animation is limited to the first two viewports; no uniform `delay-N` stagger across every section.
- [ ] **AC5** - `npx nx build cleansia.app` passes.

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

## Review
<!-- reviewer / security / optimizer write verdicts here -->
