---
id: T-0824
title: Show 'Kč' on every partner money screen, as the dashboard does
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

Earnings, Invoices, Period pay and the work contract print 'CZK' on English and Ukrainian phones, while the dashboard prints 'Kč' [AND-16, AND-17, IOS-12, IOS-13], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- Android: the four private symbol helpers call `CurrencySymbols.forCode` [AND-16], and a null code shows no symbol, as iOS and the docs do [AND-17].
- iOS: the native-symbol fallback moves into Core's `EarningsFormat.currencySymbol`; `DashboardFormat.money` calls it and the private copy goes [IOS-12].

## NOT doing

- No change to amounts or grouping.

## Acceptance criteria (done looks like)

- [ ] **AC1** — The symbol tests flip from 'CZK' to 'Kč' (RED then GREEN), and the null-code tests agree on both apps.
- [ ] **AC2** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
