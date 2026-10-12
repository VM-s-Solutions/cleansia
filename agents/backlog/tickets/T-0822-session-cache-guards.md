---
id: T-0822
title: Stop a signed-out session's late reads reaching the next account (S11)
size: M
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [ios, android]
security_touching: true
---

## Context

F12 guarded the membership read; the same shape remains elsewhere [IOS-01, IOS-02, IOS-03, AND-09], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets). On iOS a read in flight at sign-out can refill a cache after `clear()`, and iOS has no sign-in wipe, so the next person on the phone can see the previous one's data.

## Doing

- iOS `PendingOffersStore` gets F12's session generation inline [IOS-01].
- iOS wipes the session caches at sign-in, before persisting the new tokens, in Core's `AuthApiClient` [IOS-02].
- Every other iOS session cache captures its generation before each await, publishes only if it still matches, and resets its loading latches in `clear()` [IOS-03].
- The Android partner app wipes its session caches at sign-in, as the customer app does [AND-09].
- The Android customer caches get no change: no path to the next account was found [AND-08].

## NOT doing

- No shared session-cache type (CLAUDE.md §4).
- No change to sign-out ordering beyond the wipe.

## Acceptance criteria (done looks like)

- [ ] **AC1** — For each cache, a test holding a read across `clear()` fails before and passes after.
- [ ] **AC2** — A sign-in-wipe test on each changed app.
- [ ] **AC3** — Security review passes (S11).
- [ ] **AC4** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
