---
id: T-0824
title: Show 'Kč' on every partner money screen, as the dashboard does
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-12
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

- [x] **AC1** — The symbol tests flip from 'CZK' to 'Kč' (RED then GREEN), and the null-code tests agree on both apps.
- [x] **AC2** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented.
  - Android (AND-16, AND-17): the four private symbol helpers are deleted, and the 8 call sites use `CurrencySymbols.forCode`. `resolveCurrencySymbol` no longer guesses the device currency for a null code.
  - iOS (IOS-12): the native-symbol fallback moves into Core's `EarningsFormat.currencySymbol`, and `DashboardFormat.money` calls it.
  - IOS-13, moved from T-0817: `DashboardStatsDto`'s comment says both apps show the bare number for a missing code. It lands with AND-17.
  - The Architect wrote the catalog lines `patterns-mobile.md:1017` and `:1593-1595` as a description. Its six new citations hold. Line 1017's older `:421` citation is stale: the `%,.0f` headline is now at `EarningsSummaryScreen.kt:495`. The Architect reported that and did not fix it.
  - RED:
    - Android: `CurrencySymbolUsageTest` 2 of 2 failed; `DashboardScreenTest` failed with `en-US expected:<[]> but was:<[$]>`.
    - iOS: Core's koruna test read "CZK" for en_US, uk_UA and en_US@rg=czzzzz.
  - GREEN: both. iOS's null and unknown codes are unchanged.
- 2026-10-12 — Committed as `54338d6`. Its Android half compiles on its own (main and unit-test sources, per commit); its iOS half was built only as part of the final tree. The commit also carries T-0829's U+202F normalisation, which sits in the same hunk of `DashboardScreenTest`. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
- 2026-10-12 — Shipped in [#323](https://github.com/VM-s-Solutions/cleansia/pull/323). Exact-head CI on `cf63083` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `de4e6cb`. No deploy ran. Evidence: `raw/ci/p2-pr323-ci-merge-closure-001.json` (sha256 `504b3841…`) in the off-tree Wave F folder.

## Review

- **Correctness:** approve. The catalog edit is limited to what is true and is labelled descriptive. **Security:** not touched.
- **Gate 7:** a Fixed entry ("earnings, invoices and period pay show the koruna as *Kč*").
- **Reported, not fixed:** iOS Pending offers still prints `CZK`, because `PendingOffersView.swift:165` hands `OrdersFormat.money` the ISO code as its symbol. The catalog line says so; T-0824's Doing did not cover that screen.
