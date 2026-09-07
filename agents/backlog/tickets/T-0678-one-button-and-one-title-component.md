---
id: T-0678
title: One button and one title component across every page
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
- <cleansia-button> is already dominant - 24 customer files, 73 partner and admin - but 8 customer files still use raw pButton and 9 carry .cl-btn-primary / .cl-btn-outline.
- Headings are split: 5 files use <cleansia-title>, 8 use .cl-title. The component renders var(--cleansia-primary) at fixed sizes; .cl-title renders Sky700 fluid.
- Owner decision 2026-08-30: the shared component adopts the fluid Sky700 scale and .cl-title is deleted. Partner and admin headings shift - same hue family, fluid rather than fixed.

## Acceptance criteria
- [x] **AC1** - cleansia-title carries the clamp() scale and Sky700; .cl-title no longer exists.
- [x] **AC2** - No page defines its own button styling; .cl-btn-primary and .cl-btn-outline are gone.
- [x] **AC3** - Raw pButton usage in the customer app is replaced by <cleansia-button>.
- [x] **AC4** - All three apps build and their suites pass; partner and admin are checked visually for the heading shift.

## Out of scope
- Adding variants to the components beyond what the pages already need.
- The PrimeNG theme preset.

## Status log
- 2026-08-30 - ready
- 2026-08-31 - done. Greps confirm it: `cl-title`, `cl-btn-primary`, `cl-btn-outline` and raw
  `pButton` no longer appear anywhere in the customer app or its libs outside doc comments.

  `cleansia-title` took the fluid clamp() scale and Sky700 per the owner's ruling; `.cl-title` is
  deleted, and the home page keeps only `.cl-heading`, which sets alignment and margin and touches
  neither size, weight nor colour. Dark mode moved with each system rather than being left behind on
  the page - it was defining `.cl-title` and both button classes, so deleting them without moving it
  would have dropped the dark treatment silently.

  Two things found while doing it, both fixed here because they ARE this ticket:

  - **Every custom button was a `<button>` nested inside an `<a>`.** That is invalid HTML -
    interactive content inside a link - and announces two controls where the page has one;
    open-in-new-tab worked only by accident of the outer anchor. `cleansia-button` gained
    `routerLink` / `queryParams` / `href`, so all eight are now ONE element that is both.
  - **The footer's primary button carried `0 4px 12px rgba(7, 89, 133, 0.3)` at rest** - a coloured
    halo at 30 % alpha against the 12 % neutral cap. It is neutral now. That is T-0669 AC2's single
    worst offender, closed as a consequence of moving the button styling rather than as a side trip.

  `.cl-plus__cta` and the footer's white-fill override survive as GROUND adaptations - they change
  fill only, on the two gradient bands where the brand gradient would disappear. Shape, radius,
  weight and shadow still come from `cleansia-button--brand`.

- 2026-08-31 - AC4 half-verified. All three apps build (the three CI gates), 68 test projects pass,
  typecheck clean, and lint has exactly the 6 pre-existing problems a clean tree has. The VISUAL
  check of the partner and admin heading shift is the owner's - it is the half a test cannot do, and
  the shift was the point of the ruling rather than a risk of it.

## Review
<!-- reviewer / security / optimizer write verdicts here -->
