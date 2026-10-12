---
id: T-0821
title: Never offer card payment in a build with no Stripe key, on either app
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-12
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

- [x] **AC1** — A keyless build does not offer card on these paths: Android booking, a new recurring schedule and the recurring confirm; iOS a new recurring schedule, its prefill and the recurring confirm. Source-binding and view-model tests go RED, then GREEN. *(Amended 2026-10-12. The original said no keyless build offers any card path. The first review added Android's new-schedule form, for parity with iOS. Its re-review then found three more paths that still keep card: Android's prefill-from-order and edit, and iOS Payments > Add card. They are T-0835.)*
- [x] **AC2** — A release build without a valid `pk_` key fails at configuration.
- [x] **AC3** — The full Android and iOS suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented. Android lane: AND-01 to AND-04. iOS lane: IOS-20. The review fix pass added Android's new-schedule form.
  - Android:
    - `ConfirmStep` mounts card and its saving consent only when `BuildConfig.STRIPE_PUBLISHABLE_KEY` is set.
    - The recurring form does the same, and a keyless form starts with no method chosen, as booking does.
    - A card occurrence offers no confirm without a key.
    - `build.gradle.kts` refuses a non-`pk_` key in every build, and a blank one when packaging a release. Neither message prints the key.
    - The `startSubscribe` pin scans every customer main source.
  - iOS:
    - `CreateRecurringViewModel` and `OrderDetailViewModel` take `isCardPaymentAvailable`.
    - A keyless schedule starts on cash, never takes card from a prefill, and hides the card row.
    - A card occurrence's confirm returns before any server call: no credit spent, and no PaymentIntent.
  - RED/GREEN:
    - Android first pass: 5 of 15 failed, then all passed.
    - Android fix pass: 4 of 105 failed. After the fix, 19 create-mode tests that leaned on the old card default chose card explicitly, and the recurring package passed 157/157.
    - iOS: the keyless view-model, binding and confirm tests failed, then passed.
    - The release-build proofs used dummy keys only:
      - a blank key and an `sk_test_dummy` key both fail at configuration;
      - `pk_test_dummy` passes the key guard;
      - a debug build stays keyless-friendly.
  - Moved for SwiftLint's `file_length`: `AppliesNotice`, into its own file, unchanged.
- 2026-10-12 — Committed as `fc91cc5`, which compiles on its own. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
- 2026-10-12 — AC1 narrowed to match what shipped (see AC1's note). The paths that still keep card are filed as T-0835: Android's recurring prefill and edit, and iOS Payments > Add card. The keyless parity differences go into the same ticket.

## Review

- **Correctness:** request-changes on the first pass. The fix pass added Android's new-schedule gate. The re-review found the prefill and edit paths, which are now T-0835.
- **Security:** approve-with-notes. No money can move:
  - the only off-session charge sits behind `Payments:OffSessionChargesEnabled`;
  - a keyless build hides the occurrence confirm;
  - a release build refuses a blank key.
- **AC2 on iOS:** iOS's existing `check-local-config` makes a secret key a build error and a blank key a warning. That is unchanged, because AND-03 is Android's.
- **Gate 7:** no CHANGELOG entry. A keyless build is debug-only, so no user can reach it. The release-build key rule is a developer step, and `docs/mobile-app/overview.md` records it.
