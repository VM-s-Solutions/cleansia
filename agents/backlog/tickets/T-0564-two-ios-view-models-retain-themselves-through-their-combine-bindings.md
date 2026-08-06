---
id: T-0564
title: Two iOS customer view models retain themselves through their Combine bindings and never deallocate
status: draft
size: S
owner: ios
created: 2026-08-06
updated: 2026-08-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

Found while fixing the order-detail membership gate (`9c9b32e5`). That fix introduced the same
defect, was caught in review, and is now pinned by a weak-reference release test. These two sites
are the pre-existing instances, left out of that commit deliberately so a parity fix would not
quietly carry an unrelated lifetime change.

`Subscribers.Assign` holds its target **strongly**. So

```swift
repository.$current.assign(to: \.current, on: self).store(in: &cancellables)
```

builds `self → cancellables → subscription → Assign → self`. The view model never deallocates.

Two sites, both in `CleansiaCustomer`:

| File | Lines | Bindings |
|---|---|---|
| `Sources/Features/Membership/MembershipViewModel.swift` | `:29-30` | `$current`, `$plans` |
| `Sources/Features/Recurring/RecurringBookingsViewModel.swift` | `:63-64` | `$templates`, `$loaded` (one of the three there is the membership binding) |

## Why this is `S` and not urgent

**Neither has a `deinit`** (`grep -c deinit` = 0 for each, verified 2026-08-06). So today these leak
memory but do not leave work running — unlike `OrderDetailViewModel`, whose `deinit` is
`pollTask?.cancel()` and whose poller has no other stop path for an order that stays active. That
asymmetry is the whole reason this is a separate ticket: the order-detail instance was a live
recurring network call per screen open, and these are not.

The risk is that someone later adds a `deinit` to either one and it silently never runs.

## Fix

Use the `Published`-projected form, which does not retain `self` and is already the dominant idiom in
this codebase — ten sites in `Features/Home/HomeTabViewModel.swift:53-62`, including
`membershipRepository.$current.assign(to: &$membership)` at `:60`:

```swift
repository.$current.assign(to: &$current)
```

Both files also carry a now-redundant seed assignment of the same property. `Published.Publisher`
replays its current value synchronously on subscribe — this was **verified**, not assumed, by
`OrderDetailViewModelTests.testAWarmMembershipCacheIsVisibleBeforeTheFirstLoad`, which asserts a
pre-warmed repository is visible before the first `load()` with no seed line present. Delete the
seeds.

## Acceptance criteria

- [ ] Both view models bind via `assign(to: &$…)`; no `assign(to:on: self)` remains in
      `CleansiaCustomer`.
- [ ] Each has a release test in its existing suite: build inside a scope, hold a `weak var`, drop
      the strong reference, assert nil.
- [ ] Mutation-proved in both directions — reverting to `assign(to:on: self)` with a dedicated
      cancellable **must** turn each release test red.
- [ ] Redundant seed assignments deleted, with a test that would catch it if any were load-bearing
      (assert on a **resolved** value, not on the fail-open default — asserting the default sits on
      dead logic and passes either way).
- [ ] `swiftformat` then `swiftlint`; full `xcodebuild build test` with every failure classified.

## Note on the mutation

Reproducing the defect faithfully matters more than it looks. Storing the reverted binding in a
cancellable that some later `init` line overwrites **cancels the subscription**, so no cycle forms
and the release test passes — which reads as "the test doesn't work" when in fact the mutation
didn't. Use a dedicated stored property. (This happened on the order-detail fix and was caught.)
