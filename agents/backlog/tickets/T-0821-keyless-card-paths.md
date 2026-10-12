---
id: T-0821
title: Never offer card payment in a build with no Stripe key, on either app
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: true
---

## Context

F2 closed the Plus purchase bar for keyless builds; the same gap remains on the booking and recurring card paths [AND-01, AND-02, AND-03, AND-04, IOS-20], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- Android booking mounts the card option and its saving consent only when a publishable key is configured [AND-01]. A card occurrence's recurring confirm is not offered without one [AND-02].
- A release build refuses a blank key, and no build accepts a non-`pk_` key [AND-03].
- The pin covering `startSubscribe` scans every customer main source for the single gated call [AND-04].
- iOS hides the recurring card option, defaults to cash and refuses a card occurrence's confirm when `StripeConfig.isCardPaymentAvailable` is false [IOS-20].

## NOT doing

- No change to keyed builds, cash paths or server behaviour.

## Acceptance criteria (done looks like)

- [ ] **AC1** — No keyless build offers a card path on either app (source-binding and view-model tests RED then GREEN).
- [ ] **AC2** — A release build without a valid `pk_` key fails at configuration.
- [ ] **AC3** — The full Android and iOS suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
