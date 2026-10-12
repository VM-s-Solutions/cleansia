---
id: T-0835
title: Close the keyless card paths T-0821 left: Android recurring prefill and edit, iOS Payments > Add card
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0821]
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: true
---

## Context

Found on 2026-10-12. These paths were outside T-0821's Doing. No money can move:
- The only off-session charge, `ChargeOpenReceivables`, sits behind `Payments:OffSessionChargesEnabled` (`ChargeOpenReceivables.cs:56`).
- A keyless build hides the occurrence confirm.
- A release build refuses a blank key (`customer-app/build.gradle.kts:256-262`).

So only debug builds are affected.

**Android.**
- `CreateRecurringViewModel.kt:674` (`prefillFromOrder`) copies `order.paymentType?.value`.
- `:629` (edit) keeps `template.paymentType`.

Both put card into a keyless form whose card option is hidden. The form stays valid, so Save creates or keeps a card schedule the customer never saw selected. iOS already refuses the prefilled card (`CreateRecurringViewModel.swift:450-452`, `:588`).

**iOS.** Payments > Add card is not gated on `StripeConfig.isCardPaymentAvailable`. `PaymentsViewModel.swift:119-136` starts a server SetupIntent and a pending saved card that a keyless build cannot present. Android has no Add-card caller.

**Parity, the same decision.** In a keyless build the two apps already differ:
- A new schedule starts with no payment chosen on Android (`CreateRecurringViewModel.kt:731`), as booking does on both apps. On iOS it starts on cash. The Android fix lane recommends iOS adopt the booking rule, because a cash default can be taken back with a "cash cleared" notice for a choice the customer never made.
- A card occurrence's confirm is hidden on Android (`OrderDetailScreen.kt:582-584`) but shown disabled on iOS.
- `recurring_cash_needs_card` ends "…so this schedule is paid by card", which is false where card is hidden.

`docs/mobile-app/overview.md`'s keyless passages describe both apps as they stand, and must change with whichever app moves.

## Doing (proposed, for Mike)

- Android: prefill and edit fall back to cash when no key is configured, as iOS does. A pin covers each.
- iOS: Add card is hidden without a key. A pin covers it.

## NOT doing

- No change to keyed builds or to server behaviour.

## Acceptance criteria (done looks like)

- [ ] **AC1** — No keyless build carries card into a form or flow it cannot present. The tests go RED, then GREEN.
- [ ] **AC2** — The full Android and iOS suites pass.

## Work record

- 2026-10-12 — Filed from the correctness and security re-reviews of T-0821. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
