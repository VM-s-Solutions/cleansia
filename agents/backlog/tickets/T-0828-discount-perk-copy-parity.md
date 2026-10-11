---
id: T-0828
title: Make iOS's discount-perk copy match Android's
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
---

## Context

The same string keys say different things on the two apps; Android is the reference [AND-05, IOS-21], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- The iOS perk title carries the percentage and the description drops it, in all five locales. `SubscribePlusScreen` and `MembershipSuccessScreen` pass the percentage.

## NOT doing

- No Android change.
- No change to the discount itself.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Copy tests RED then GREEN in five locales.
- [ ] **AC2** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
