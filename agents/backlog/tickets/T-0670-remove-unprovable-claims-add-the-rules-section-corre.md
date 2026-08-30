---
id: T-0670
title: Home: remove unprovable claims, add the rules section, correct Plus numbers
status: ready
size: M
owner: frontend
created: 2026-08-30
updated: 2026-08-30
depends_on: ['T-0669']
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context
- `cs.json` ships `badge_rating: Hodnoceni 4.9`, `badge_customers: 2 000+ zakazniku`, `badge_certified`, and three invented testimonials each rendering a hardcoded five-star row. No review aggregation exists on this page.
- The seed pins Cleansia Plus at `DiscountPercentage 5.00`, `FreeCancellationWindowHours 4`, `ExpressUpgradesPerMonth 1`, `TrialPeriodDays 14`. The page implies otherwise.
- `business-rules.md` pins a cancellation ladder, a 15-minute change-of-mind window and a 500 Kc cleaner-cancels credit that the page never states.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [ ] **AC1** - `badge_rating`, `badge_customers`, `badge_certified` and `testimonials.t1..t3` are removed from all five locales, and the `testimonials` component is deleted.
- [ ] **AC2** - A rules section renders the cancellation ladder (free >=24 h / 25% 4-24 h / 50% <4 h), the 15-minute window (60 on a first booking) and the 500 Kc credit, each traceable to `business-rules.md`.
- [ ] **AC3** - The Plus section states 5% discount, cancellation window extended by 4 hours, 1 express per month, 14-day trial, 199 Kc/mo or 2 030 Kc/yr.
- [ ] **AC4** - `error-contract-parity.spec.ts` still passes and all five locales keep identical leaf-key counts.
- [ ] **AC5** - No star row is rendered from a literal anywhere on the page.

## Out of scope
- Re-introducing ratings - that waits until `OrderReview` has rows and an aggregate endpoint exists.
- The FAQ section, which the owner asked to keep as-is.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)

## Review
<!-- reviewer / security / optimizer write verdicts here -->
