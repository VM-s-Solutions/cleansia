---
id: T-0825
title: When no mail app answers, Help copies the support address and says so
size: S
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

F7's Help shortcut (and the registration lock's support row) does nothing on a phone with no mail app [AND-18, IOS-14], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets). The customer app already falls back to copying the address.

## Doing

- Both partner apps copy the support address and show a snackbar when no app accepts the `mailto:` (variant A2).
- Android's `openOrCopy` moves to `:core` for its third caller.
- A partner `help_email_unavailable` string in all five locales.

## NOT doing

- No in-app mail composer.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Tests prove the fallback runs when the intent or URL is refused, RED then GREEN.
- [ ] **AC2** — Five locales are present (parity guards).
- [ ] **AC3** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
