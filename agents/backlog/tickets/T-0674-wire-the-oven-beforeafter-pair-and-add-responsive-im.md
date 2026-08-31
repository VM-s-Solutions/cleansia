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

## Review
<!-- reviewer / security / optimizer write verdicts here -->
