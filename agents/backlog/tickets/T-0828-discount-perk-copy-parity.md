---
id: T-0828
title: Make iOS's discount-perk copy match Android's
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-12
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

- [x] **AC1** — Copy tests RED then GREEN in five locales.
- [x] **AC2** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented (IOS-21, with AND-05 as the reference).
  - `membership_perk_discount_title` carries `%1$d%%`, and the description drops the figure, in five locales.
  - `SubscribePlusScreen` passes the plan's percentage.
  - `MembershipSuccessScreen` takes `discountPercent`, fed by the shell, and leaves the row out without one, as Android does.
  - The first version branched on the trial copy and tripped the existing guard against holding a perk back from a trialing member. It was reworked.
  - RED: the five-locale copy test, and both screens' title test.
  - GREEN: both. The customer suite passes, and so does `StringCatalogCompletenessTests`.
- 2026-10-12 — Committed as `4bdc9cb`. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
- 2026-10-12 — Shipped in [#323](https://github.com/VM-s-Solutions/cleansia/pull/323). Exact-head CI on `cf63083` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `de4e6cb`. No deploy ran. Evidence: `raw/ci/p2-pr323-ci-merge-closure-001.json` (sha256 `504b3841…`) in the off-tree Wave F folder.

## Review

- **Correctness:** approve.
- **Gate 7:** a Changed entry ("the Plus discount perk names its percentage in its title").
