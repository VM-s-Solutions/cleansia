---
id: T-0674
title: Home: wire the oven before/after pair and add responsive image sources
status: done
size: S
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
- Four generated before/after pairs replaced the stock gallery images, which were two photographs of already-clean rooms so the slider showed no difference. Sofa, carpet and mattress replaced the three the component renders; the oven pair is in the repo but unwired.
- `gallery.component.ts:22` holds a hardcoded three-item `beforeAfterPairs` array.
- The eight new webp files total 968 KB; the carpet pair alone is 232 KB + 188 KB because of its texture.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [x] **AC1** - The oven pair renders as a fourth item with a `label_oven` key present in all five locales.
- [x] **AC2** - Gallery images are served with a `srcset` at 600/900/1200 so a phone does not download the 1200 px asset.
- [x] **AC3** - The caption states the before/after photo feature truthfully without implying the shown images are job records, until `OrderPhoto` supplies real ones.

## Out of scope
- Feeding the gallery from `OrderPhoto` - worth doing once there are completed orders with consent, but a separate ticket.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)
- 2026-08-31 - done. Ground-truthed and challenged: every AC holds. The oven pair is wired in
  `gallery.component.ts:26`, `srcsetFor()` emits the 600/900/1200 w set bound on both images, and the
  gallery subtitle no longer claims the photos are from real cleanings.
- 2026-08-31 - REOPENED. The pairs are wired, but the slider chrome is not the artboard's:
  - PŘED is red and PO is green, both at the BOTTOM. The artboard uses a dark navy PŘED at the top
    left and a blue PO at the top right.
  - The handle is a white circle with a two-headed arrow. The artboard uses a blue circle with a
    3px white ring and white chevrons.
  - `.cl-gallery__item` has no card: transparent, no radius, no padding. The artboard wraps each
    slider in a white card, radius 36, 14px padding, so a white frame shows around the photograph.
  - The caption is centred with no duration; the artboard puts the label left and a duration right.
- 2026-08-31 - done, verified by screenshot. PŘED is navy at the top left and PO is blue at the top
  right — the brand's own two tones, because before and after are two halves of one photograph and
  not a failure and a success. The handle is a blue disc with a 3px white ring, and each pair sits in
  a white card with the artboard's 14px frame.

  NOT copied: the artboard's per-pair durations ("2 h 10 min"). We hold no such measurement, and
  inventing one is exactly what T-0670 removed from this page.

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

## Review
<!-- reviewer / security / optimizer write verdicts here -->
