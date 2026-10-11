---
id: T-0829
title: Make the iOS 16.4 colour tests pass and tidy two Android test nits
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [ios, android]
security_touching: false
---

## Context

Floor debt and test nits [IOS-15, AND-14, AND-23], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets). Five dark-mode colour tests fail on the iOS 16.4 runtime, a Gate 8.5 check that CI does not run.

## Doing

- Diagnose on the local 16.4 runtime, and make the dark-mode colour resolution independent of the conversion pre-iOS 17 gets wrong, so the three `ContentSafeAreaBindingTests` and two `TextInkTests` pass on 16.4 and the newest runtime [IOS-15].
- Android test comments lose their tracker ids [AND-14], and the koruna test normalises U+202F too [AND-23].

## NOT doing

- No repo-wide comment sweep.

## Acceptance criteria (done looks like)

- [ ] **AC1** — The five tests pass on iOS 16.4 and the newest runtime, with production behaviour unchanged on both.
- [ ] **AC2** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
