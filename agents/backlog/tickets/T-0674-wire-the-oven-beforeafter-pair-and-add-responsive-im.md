---
id: T-0674
title: Home: wire the oven before/after pair and add responsive image sources
status: ready
size: S
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
- Four generated before/after pairs replaced the stock gallery images, which were two photographs of already-clean rooms so the slider showed no difference. Sofa, carpet and mattress replaced the three the component renders; the oven pair is in the repo but unwired.
- `gallery.component.ts:22` holds a hardcoded three-item `beforeAfterPairs` array.
- The eight new webp files total 968 KB; the carpet pair alone is 232 KB + 188 KB because of its texture.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [ ] **AC1** - The oven pair renders as a fourth item with a `label_oven` key present in all five locales.
- [ ] **AC2** - Gallery images are served with a `srcset` at 600/900/1200 so a phone does not download the 1200 px asset.
- [ ] **AC3** - The caption states the before/after photo feature truthfully without implying the shown images are job records, until `OrderPhoto` supplies real ones.

## Out of scope
- Feeding the gallery from `OrderPhoto` - worth doing once there are completed orders with consent, but a separate ticket.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)

## Review
<!-- reviewer / security / optimizer write verdicts here -->
