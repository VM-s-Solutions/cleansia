---
id: T-0669
title: Home: one title system, neutral shadows, decoration removed
status: ready
size: M
owner: frontend
created: 2026-08-30
updated: 2026-08-30
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

## Review
<!-- reviewer / security / optimizer write verdicts here -->
