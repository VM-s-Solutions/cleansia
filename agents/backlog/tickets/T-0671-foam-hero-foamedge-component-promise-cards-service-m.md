---
id: T-0671
title: Home: foam hero, foam-edge component, promise cards, service mascot discs
status: ready
size: L
owner: frontend
created: 2026-08-30
updated: 2026-08-30
depends_on: ['T-0669', 'T-0670']
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context
- The approved concept replaces the centred hero and 3-column icon-card structure with a foam ground, a grounded mascot, promise cards carrying real facts, and service cards with normalised mascot artwork.
- The three service artworks have different content bounds (281x291, 299x381, 232x295), so any equal-box CSS scales them unequally. They were re-cut to one 340x280 canvas at an identical 258 px character height on a shared baseline.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [ ] **AC1** - A reusable foam-edge divider exists as one component or mixin, not repeated inline SVG.
- [ ] **AC2** - The hero renders the radial foam ground with the mascot standing on the foam edge; no decorative blobs.
- [ ] **AC3** - Promise cards replace the icon-card grid; each carries one true fact with a source.
- [ ] **AC4** - All three service mascots render at identical character height; the disc sits behind the artwork and never clips it.
- [ ] **AC5** - No three consecutive sections are 3-column card grids.

## Out of scope
- The price calculator - T-0672.
- The footer and switchers - T-0673.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)

## Review
<!-- reviewer / security / optimizer write verdicts here -->
