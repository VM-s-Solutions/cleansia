---
id: T-0835
title: Close the keyless card paths T-0821 left: Android recurring prefill, recurring edit on both apps, iOS Payments > Add card
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0821]
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: true
---

## Context

Found on 2026-10-11 (iOS Add card, by the first-pass iOS reviews) and 2026-10-12 (the prefill and edit paths, by the fix pass and the re-reviews). These paths were outside T-0821's Doing. No money can move:
- The only off-session charge, `ChargeOpenReceivables`, sits behind `Payments:OffSessionChargesEnabled` (`ChargeOpenReceivables.cs:56`).
- A keyless build offers no working occurrence confirm: Android hides it, and iOS shows it disabled and refuses the call.
- A release build refuses a blank key (`customer-app/build.gradle.kts:256-262`).

So only debug builds are affected.

**Android.**
- `CreateRecurringViewModel.kt:674` (`prefillFromOrder`) copies `order.paymentType?.value`.
- `:629` (edit) keeps `template.paymentType`.

Both put card into a keyless form whose card option is hidden. The form stays valid, so Save creates or keeps a card schedule the customer never saw selected. iOS already refuses the prefilled card (`CreateRecurringViewModel.swift:450-452`, `:588`), but its edit keeps card too: the keyless cash default applies only to a new schedule (`:184-189`).

**iOS.** Payments > Add card is not gated on `StripeConfig.isCardPaymentAvailable`. `PaymentsViewModel.swift:119-136` starts a server SetupIntent and a pending saved card that a keyless build cannot present. Android has no Add-card caller.

**Parity, the same decision.** In a keyless build the two apps already differ:
- A new schedule starts with no payment chosen on Android (`CreateRecurringViewModel.kt:731`), as booking does on both apps. On iOS it starts on cash. The Android fix lane recommends iOS adopt the booking rule, because a cash default can be taken back with a "cash cleared" notice for a choice the customer never made.
- A card occurrence's confirm is hidden on Android (`OrderDetailScreen.kt:582-584`) but shown disabled on iOS.
- `recurring_cash_needs_card` ends "…so this schedule is paid by card", which is false where card is hidden.

`docs/mobile-app/overview.md`'s keyless passages describe both apps as they stand, and must change with whichever app moves.

## Doing (proposed, for Mike)

- Android: the prefill falls back to cash when no key is configured, as iOS's prefill does. A pin covers it.
- Edit, on both apps: a keyless edit of a card schedule keeps card, with the card row hidden. Pick one behaviour for both apps, and pin it.
- iOS: Add card is hidden without a key. A pin covers it.

## NOT doing

- No change to keyed builds or to server behaviour.

## Acceptance criteria (done looks like)

- [x] **AC1** — No keyless build carries card into a form or flow it cannot present. The tests go RED, then GREEN.
- [x] **AC2** — The full Android and iOS suites pass.

## Work record

- 2026-10-12 — Filed from the correctness and security re-reviews of T-0821. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.
- 2026-10-12 — Mike: "You can fix those" (2026-10-12), his answer to the item covering T-0832 to T-0836.
- 2026-10-12 — Ruled by item 3 of the native design ruling (off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`): "CONSENSUS on the behaviour. 3a is resolved by narrowing the catalog entry, which drops the Android view-model guard; 3b and 3c are conceded."
  - The decision: "A build with no Stripe publishable key never holds card in a form and never offers an action that only card can complete."
  - (a) A schedule prefilled from a card order holds no way to pay. "This replaces the ticket's 'the prefill falls back to cash'."
  - (b) An edit of a card schedule opens with no way to pay and says so, and Save stays closed until the customer picks one. A cash schedule keeps cash.
  - (c) A new schedule starts with no way to pay on both apps, so iOS stops defaulting to cash and gains Android's note in five locales.
  - (d) On iOS, a card occurrence's confirm is hidden instead of shown disabled. Android already hides it and gets no new view-model guard.
  - (e) The copy of `recurring_cash_needs_card` and `booking_cash_needs_card` does not change; the overview says why.
  - (f) iOS Payments > Add card is hidden, and refused in its view model, when there is no key.
  - Its note for Mike opens "None for the decision: keyless builds are debug-only". It rests on a reading of Mike's 2026-10-12 answers, not on a separate answer.
- 2026-10-12 — Implemented on `fix/post-e-p6-native-followups`, off master `6952149`. Evidence paths below are relative to the off-tree Wave F folder, `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/`. The result files are in `p5/impl/`: `android_native-items.json`, `ios_native-items.json`, `docs_native.json` and `native_fix-pass.json`. Every run was local; nothing was pushed. The evidence files and the reviews quoted here name this branch's commits by the ids they had before its commit messages were reworded before push; each has the same tree as the commit cited here, and `p5/sha-map-p6.md` in the Wave F folder maps them.
  - `b629ff5` — Android.
    - `CreateRecurringViewModel.presentablePaymentType(type, cardAvailable)` (`CreateRecurringViewModel.kt:600-601`) turns card into no choice when the build cannot present it, and leaves every other value alone.
    - Both copies go through it: the edit (`:635`) and the prefill from a past order (`:680`). `cardAvailable` is read once from the key (`:102`).
    - A new schedule already started with no choice in a keyless build (`:738`). Its existing tests cover that, so it has no RED.
    - No view-model guard is added for the card-only actions; they stay hidden in the UI.
    - RED: 108 tests, 3 failed (`p5/android-native/t0835-red-a.txt`). The prefill and edit view-model tests failed with "expected:<null> but was:<2>". The source pin failed with "`template.paymentType` must reach the form through presentablePaymentType(…, cardAvailable)".
    - The plain test of the new function first failed to compile: "Unresolved reference 'presentablePaymentType'", six times (`t0835-red-b.txt`).
    - GREEN: `CreateRecurringViewModelTest` and `CreateRecurringCardPaymentBindingTest` pass, and so does the whole recurring package (`t0835-green-targeted.txt`, `t0835-green-recurring.txt`; 159 tests in the package by the result file's count, which the text log does not print).
    - With a key: a scratch copy built with `-PSTRIPE_PUBLISHABLE_KEY=pk_test_placeholder` passes the same two classes (`keyed-check/t0835-keyed.txt`; 109 tests by the result file's count).
    - The shared edit fixture is now a cash schedule. With the card fixture, twelve edit tests failed in a keyless build (`t0835-fix-card-fixture.txt`). Eleven save an edit of it, which a keyless build now refuses. The twelfth, the edit prefill test, asserted the prefilled card and now expects cash. The commit names it.
    - On its own, `b629ff5` does not build the iOS partner app. T-0832's regenerated client came first, and the iOS call sites move only in `ce9240f` (the correctness review's N3; T-0832's record lists the six commits).
  - `d3b0890` — iOS.
    - One line in `init` clears card when the build cannot present it, for a new schedule and an edit alike (`CreateRecurringViewModel.swift:189` at the head). A cash schedule keeps cash, and a build with a key keeps card.
    - The form's payment note mirrors Android's: the cash-cleared note first, otherwise "Choose how the schedule is paid." (`recurring_create_payment_missing`, in en, cs, sk, uk and ru).
    - A keyless card occurrence's confirm is hidden (`OrderDetailView.swift:166`, `offersRecurringConfirm`). The view model still refuses the call (`OrderDetailViewModel.swift:448`).
    - `PaymentsViewModel` takes `isCardPaymentAvailable`, defaulting to `StripeConfig.isCardPaymentAvailable`. Without a key it offers no capture (`PaymentsViewModel.swift:59-60`) and `addCard()` starts none (`:122-123`).
    - RED on iOS 18.6: 164 tests run, 9 of them failing, with 19 failures in all (`p5/ios-native/logs/customer-red-186.log`; the failing tests are listed in `evidence/customer-red-186-summary.txt`). The two new members, `offersRecurringConfirm` and the Payments flag, were first added as no-op stubs so that the suite compiled.
    - GREEN on 18.6 and on the iOS 16.4 simulator. The first 18.6 full run failed one existing source pin, `OrderStatusLogicTests`' literal `if order.needsConfirmation {`, which this commit updates (`logs/customer-full-186.log`, then `customer-full-186-rerun.log`).
  - `e1024f1` — `docs/mobile-app/overview.md` says what a keyless build does with card, on both apps.
  - `de56d19` — the catalog entry the design ruling ratified (Review).
  - `b6cd81a` — the form's `paymentType` comment says card is the default and when the value is nil (the correctness review's N1). Its message names the one state `d3b0890` changes in a keyed build, which `d3b0890`'s message, reworded before push, also names (N2; see Ruled differently).
  - `6672db1` — no CHANGELOG entry for this ticket's keyless changes, and its message says why (Gate 7). It names the keyed-build note below as outside that reason and leaves its entry to `8c26004`. The follow-up correctness review had found the reason incomplete before the message was reworded (its N3).
  - `8c26004` — the CHANGELOG entry under Fixed: "the schedule form says when no payment is chosen".
  - Suites, on the `6672db1` tree (`p5/native-fixpass/logs/summary.txt`):
    - Android: core 296, partner 839 and customer 1683 tests, 0 failures.
    - iOS: CleansiaCore 776/776, CleansiaPartner 1113/1113 and CleansiaCustomer 1780 (1779 passed, 1 skipped), on 18.6 and on 16.4.
    - No code changed after `6672db1`. The filing commit and the records change only `agents/backlog/`; `3bd7b2b` changes docs and catalog pages and one test comment; `8c26004` changes the CHANGELOG. No suite ran after `6672db1`.
  - Gate 8.5, iOS 16.4: the suites above ran on 16.4. A scratch render harness, never committed, ran on a copy of the `6672db1` tree on 16.4 and 18.6 (`p5/native-fixpass/render/`).
    - With nothing chosen, the keyless form shows "Choose how the schedule is paid.", in light and in dark.
    - When cash is taken away, the cash-cleared note shows and the missing-payment note does not. A keyed build with card chosen shows no note.
    - A keyless card occurrence's footer offers only "Cancel order". A cash occurrence shows "Confirm booking".
    - No launch-and-navigate smoke ran, and the Payments screen was not rendered.
  - **Ruled differently from Doing and NOT doing:**
    - The Android prefill holds no way to pay instead of falling back to cash (the ruling's decision (a)).
    - A keyless edit of a card schedule clears card and says so, instead of keeping card with the row hidden. Both apps behave the same (decision (b)). The ruling's accepted consequence: "a keyless edit of a two-cleaner card schedule cannot be saved."
    - Two additions for parity, named in `d3b0890`'s message: iOS starts a new schedule with no way to pay (decision (c)), and hides a card occurrence's confirm (decision (d)).
    - No Android view-model guard on the card-only actions (challenge 3a). Android's `confirmRecurring` and `startSubscribe` stay unguarded behind hidden controls; T-0845 takes them.
    - NOT doing says no change to keyed builds. One keyed state changed: when a cash choice the quote took away becomes possible again and nothing is picked, a keyed build now shows "Choose how the schedule is paid." The ruling's challenge 3b allows it (one note or the other, as on Android), and `b6cd81a`'s message records it. `d3b0890`'s message is reworded before push to say so, with its tree unchanged.
    - The overview's "copy unchanged" sentence covers only the two notes that refuse cash to a crew of two. The ruling's general wording would have been false: two other cash notes end "or pay by card" and "or choose card".
  - Not run: nothing against DEV or PRO. No keyed branch runs on CI, because android-ci builds keyless; T-0845 covers it. No server file changed for this ticket.
- 2026-10-13 — Not yet: the P6 pull request's exact-head CI and its merge.

## Review

- **Correctness review** of `fix/post-e-p6-native-followups` at `23ba6e3` against `6952149` (`p5/impl/review_native.json`): "## Verdict: CHANGES REQUESTED". "The code is correct on all three stacks and each fix has a test that catches its removal."
  - Its deletion test for this ticket: `CreateRecurringViewModelTest` "a schedule made from a card order holds card only in a build that can take a card" "fails when the card filter does nothing".
  - Its CleansiaCustomer run, on both runtimes: "1778 pass, 1 skip, 1 fail on both runtimes (item 1); all T-0835 tests and `testTheReadSeesTheDarkSurface` pass on 16.4". The one failure is T-0839's.
  - Blocking items for this ticket, and what became of them:
    - Item 4, "**Three ratified catalog entries are not written.**" This ticket's is in `de56d19`.
    - Item 7: the docs edits were still uncommitted in the worktree, the overview's among them. It found "the narrowed `overview.md` sentence is accurate". Committed in `e1024f1`.
  - Should fix, for this ticket:
    - "**N1:** the doc comment at `CreateRecurringViewModel.swift:16` says "Nil until the customer picks", but the default on line 17 is card." Fixed in `b6cd81a`.
    - "**N2:** the `36ef0cc5a` message says "Builds with a key are unchanged". That is not quite true." `b6cd81a`'s message records the correction, and `d3b0890`'s message is reworded before push, with its tree unchanged.
    - "**N6 (Gate 8.5, partial):**" the payment note and the order footer had only source pins and no 16.4 render check. The scratch render harness above rendered both on 16.4 and 18.6.
  - It asked for a security review: "T-0835 is marked security-touching, so it needs a security review (Gate 3); none is on record." See below.
- **Security review** (Gate 3) of the branch against `6952149` (`p5/impl/security_native.json`): "**VERDICT: PASS. Nothing blocks the merge.** The T-0835 changes only take card actions away from builds without a Stripe key, and those are debug builds only."
  - On this ticket: "## 1. T-0835: no card held or offered without a key — PASS". Under "**Nothing moves money**": "No production code passes its own value for the key gate; every app screen uses `StripeConfig.isCardPaymentAvailable`."
  - "**S11:** PASS — no new per-user cache, singleton or stored value; the new fields are build constants."
  - Non-blocking items for this ticket:
    - Item 2: the committed overview still described the old keyless behaviour, and the corrected text was uncommitted. Committed in `e1024f1`; the security review did not read it as part of the branch.
    - Item 3: "**Android has no view-model backstop.**" Filed as T-0845.
    - Item 4: the ticket's INDEX row and Review were stale. This record answers it.
  - It names only its base, `6952149`. Its `CreateRecurringViewModel.swift` citations hold from `d3b0890` until `b6cd81a` (`:188` is `:189` after it), and its `overview.md:171-172`, `:552` and `:557` citations hold only before `e1024f1`. The fix pass that made `6d6bf94` already lists its finding, so it read the branch before `6d6bf94`.
- **Correctness review of the later commits** (`p5/impl2/review_native_followups.md`): "**Verdict: CHANGES REQUESTED.**", on T-0839 and on one commit message, not on this ticket. For this ticket:
  - It confirms the keyed-build deviation exactly: ""Builds with a key are unchanged" is false in exactly one state. 1cb13788a's correction describes that state accurately."
  - Its N3: the reason for no entry in `6672db1`'s message as first written (`62b1d78cf`) did not cover that state. The reworded message names the state as outside its reason, and `8c26004` adds the entry.
  - Its N4: `CreateRecurringViewModel.swift` is 600 lines, the SwiftLint warning limit, so one more line fails `--strict` (Reported, below).
- **Security review of the later commits** (`p5/impl2/security_native_followups.md`): "**VERDICT: PASS-WITH-NOTES.**" For this ticket: "## 1. 9075f75dd — what keyless builds promise (T-0835): PASS, true of both apps at head". Its N2, information only: on iOS a booking holds card out only by hiding its row, so if card were ever held without a key the order would be created without the payment sheet; nothing writes card there without a key today.
- **AC1's reading.** The forms and flows are the recurring form (new, prefill, edit), Payments > Add card and the confirm of a card occurrence, on both apps. Android's `setPaymentType`, `confirmRecurring` and `startSubscribe` refuse no card themselves, but in a keyless build every path that hands them card is hidden: the card row, a card occurrence's confirm and the Plus subscribe bar. The security review checked the first two, and the design ruling the third. So no keyless build hands them card, and AC1 holds without a guard, as the ruling's challenge 3a reasoned.
- **Catalog-edit routing** (the routing test in `agents/knowledge/conventions.md`), for `de56d19`:
  - The entry: `patterns-mobile.md`, "A keyless build never holds card, and never offers what only card can finish (T-0835)".
  - It was ratified by the design ruling ("Catalog, ratified here."), with its routing. Test 1 swept the payment-type writers and the card-only actions on both apps: "Zero violations once T-0835 lands." Then "Test 2 fires (it forbids a held card and a disabled CTA), so the entry is priced." and "Test 3 does not fire."
  - One addition, named in the commit: the iOS roster also names `CreateRecurringBindingTests` and `OrderDetailFooterStyleTests`.
  - Re-checked at the head. Android reads the key at `CleansiaApp.kt:58`, `ConfirmStep.kt:115`, `SubscribePlusScreen.kt:100`, `OrderDetailScreen.kt:582`, `CreateRecurringScreen.kt:1285` and `CreateRecurringViewModel.kt:102` and `:738`. Every iOS view model that can take card takes `isCardPaymentAvailable`, defaulting to `StripeConfig.isCardPaymentAvailable`.
  - The enforcers run in CI: `:customer-app:testDebugUnitTest` (`android-ci.yml:86`) and the CleansiaCustomer scheme (`ios-ci.yml:229`).
- **Gate 7:** `8c26004`'s entry under Fixed, for the one keyed state a customer can see. `6672db1` gave the ticket's keyless changes no entry, because a build with no Stripe key is debug-only, and left that state to `8c26004`.
- **Reported, not fixed:**
  - `CreateRecurringViewModel.swift` is 600 lines, the SwiftLint warning limit (`.swiftlint.yml:96-98`), so one more line fails ios-ci's `--strict` (the follow-up correctness review's N4). Not on a ticket.
  - Two more cash notes name card in a keyless build, where card is hidden: `booking_cash_needs_account` ("…or pay by card", Android `values/strings.xml:834`) and `recurring_cash_unchecked` ("…or choose card", `:1063`). The iOS catalog has the same keys. Ruling 3(e) covers only the two `*_cash_needs_card` notes. Debug-only. Not on a ticket.
  - In the scratch render harness on iOS 16.4, `OrderDetailView` placed as the root of a NavigationStack kept its loading spinner while its title showed the loaded order. Without the NavigationStack it rendered fully, and 18.6 rendered both. In the app the screen is pushed, so it is probably the harness. A 16.4 launch that opens an order would settle it. Not on a ticket.
  - Already filed, so not repeated here: T-0845 (one injectable key seam, guards on `confirmRecurring` and `startSubscribe`, and a test of the customer sign-in wipe).
