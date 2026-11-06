---
id: T-0909
title: Booking confirmation — the cleaning mascot hands over to the waving mascot at the same size
size: S
owner: Michael Chaban
created: 2026-11-01
updated: 2026-11-01
depends_on: [T-0906]
blocks: []
stories: []
adrs: []
layers: [ios, android]
security_touching: false
---

## Context

After a booking, the busy overlay's cleaning mascot flies to the confirmation's hero slot and turns into the welcoming
(waving) mascot. Mike: "The loading mascot becomes way smaller than the waving mascot and this transition in sizes is
quite visible".

The cause has two parts:
- The busy card draws its mascot in a fixed 140×140 box. The hero's canvas follows the confirmation's size tier
  (360/249 × the figure height).
- The two artworks do not fill their canvases the same way, so the visible figures differ in height. The stand-in
  images then scale between the two boxes during the 420 ms flight.

Mike chose to build the recommendation directly, without an options round.

## Acceptance criteria

- [x] **AC1** — On the booking flow, the cleaning figure in the busy card is drawn at the same visible height (alpha
  bounds) as the welcoming figure on the hero, within 3 %, on every confirmation size tier and both platforms.
  The flight is a move with no visible size change. The confirmation's approved layout does not change: the busy
  card adapts, not the hero.
- [x] **AC2** — Each stand-in image in the flight keeps its own figure height while the two poses blend, so the blend
  does not jump in size either.
- [x] **AC3** — Other users of the busy overlay (Plus activation) keep their look.
- [x] **AC4** — Evidence: a frame-by-frame recording before and after on the iOS simulator and the Android emulator,
  with the figure height measured per frame.

## Out of scope

- The confirmation card's layout.
- The flight's timing and easing.
- The art itself.

## Work record

- 2026-11-01 — Filed.
- 2026-11-02 — Built on iOS and Android, and reviewed on both (APPROVED).
  - From the review step on, the confirmation the booking will arrive as is laid out unseen beneath the sheet. The busy
    card sizes its cleaning figure from that hero, so it matches before the order lands. The tier estimate is used
    only before the review step.
  - In the flight, each stand-in is scaled by its own figure box.
  - Plus activation keeps today's size.
  - Measured visible figure height, max/min over the hand-off:
    - iOS: before 1.475 (compact) and 0.912 (regular); after 1.001. Small phone with a cash ticket: 1.127 before,
      1.005 after.
    - Android: before 1.199 (360) and 1.291 (412); after 1.019 and 1.006.
  - The coordinator also made the unseen draft unreachable by keyboard and D-pad focus on Android (pinned test, red then
    green). On iOS, T-0906's hidden measuring text was marked `verbatim` after a build had extracted it into the
    Customer catalog as a junk key; the catalog is restored to its start bytes.
  - Gates: CleansiaCustomer 1966 tests, failing only the 3 tests that already failed; customer-app 1865/0.
