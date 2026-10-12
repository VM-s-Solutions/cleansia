---
id: T-0827
title: Report the selected Orders segment, hide mascot art, and grow small targets
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: false
---

## Context

Accessibility findings [AND-06, IOS-04, IOS-17], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- The Android partner Orders segmented control reports which segment is selected (`Role.Tab` inside a `selectableGroup`) [AND-06].
- iOS mascot images are decorative, so VoiceOver no longer reads asset names, matching Android [IOS-04].
- Customer Home's Manage and See all links use `CleansiaTextLink`, and the Profile Edit chip gets a 44 pt target [IOS-17].

## NOT doing

- The broader sweep of other controls [AND-07] is not in scope: it needs its own decision.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Source-binding tests RED then GREEN.
- [ ] **AC2** — Accessibility dumps show the selected state, no asset-name labels and 44 pt frames.
- [ ] **AC3** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
