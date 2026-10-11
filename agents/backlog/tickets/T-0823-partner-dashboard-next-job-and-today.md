---
id: T-0823
title: Partner dashboard: right day words, honest today line, no reload on every tab return
size: S
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

Edge cases F3 ported between the apps, and the iOS reload [AND-10, AND-11, AND-12, AND-13, AND-15, IOS-06, IOS-08, IOS-09, IOS-10, IOS-11, WCD-31], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- Say 'Tomorrow' only for the next calendar day; later days use the dated form, on both apps [AND-10/IOS-08].
- An undated active order never becomes the next job [AND-11/IOS-10].
- The today line is omitted, not 'You're free today', while the upcoming list is unread or failed, on both apps [AND-12/IOS-09].
- iOS keeps a loaded dashboard for 60 s, like Android, and refreshes in place when stale [IOS-06].
- Delete Android's dead dashboard helpers [AND-13].
- The iOS date comment says the day text follows the iOS template convention [IOS-11].
- A short mobile partner-dashboard section in the docs, which the dashboard KDocs point to [AND-15/WCD-31].

## NOT doing

- The progressive-publication question (D4, pending Mike's DEV check).

## Acceptance criteria (done looks like)

- [ ] **AC1** — Pure-helper tests for the day rule, the undated sort and the unknown today line, RED then GREEN on both apps.
- [ ] **AC2** — A test that the iOS tab return within 60 s makes no read.
- [ ] **AC3** — The full suites pass, and a render check of the dashboard.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
