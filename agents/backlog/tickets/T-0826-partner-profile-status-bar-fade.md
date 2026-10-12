---
id: T-0826
title: Fade content under the status bar on the partner Profile hub too
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

F6 fixed the dashboard; the partner Profile hub has the same overlap [AND-21, AND-22, IOS-05], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- Android applies `statusBarFade(listState)` to the Profile hub's list [AND-21], and a `:core` binding test pins the overload's body [AND-22].
- iOS wraps the hub in `StatusBarFadeScrollView` with the hero tint, and marks the hero [IOS-05].
- `docs/mobile-app/patterns.md#status-bar-fade` names the partner Profile hub.

## NOT doing

- No change to other screens.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Binding tests RED then GREEN.
- [ ] **AC2** — A render check of the scrolled hub on both apps.
- [ ] **AC3** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
