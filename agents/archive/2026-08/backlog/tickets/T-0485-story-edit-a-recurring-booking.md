---
id: T-0485
title: STORY — a customer cannot edit a recurring cleaning setup on either mobile app; define what "edit" means
status: draft
size: S
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: [T-0486, T-0487]
stories: []
adrs: []
layers: [analyst]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #6 (2026-08-02):** *"Cannot edit a recurring cleaning setup on either mobile app."*

### Ground truth — PM-verified on `master` at `0e4ede1b`. The remark is exactly right, and the
### two platforms are broken in different ways

| Layer | Android | iOS |
|---|---|---|
| Backend command | `UpdateRecurringBooking` exists — generated into both mobile specs (`openapi/*.json` → `UpdateRecurringBookingCommand`) | same |
| Generated client | `UpdateRecurringBookingCommand` present | `CleansiaCustomerApi/Models/UpdateRecurringBookingCommand.swift` present |
| Hand-written API layer | **`RecurringBookingApi.kt:49` `suspend fun update(...)` — written** | **absent** |
| Repository | **`RecurringBookingRepository.kt:70` `suspend fun update(...)` — written** | **absent** — grep for `update` across `Features/Recurring/Data/` returns nothing |
| Feature/ViewModel caller | **ZERO.** `grep '\.update(' features/recurring/` returns nothing | none |
| UI entry point | none — `RecurringBookingsScreen.kt:66` says *"Create + edit ship via `CreateRecurringScreen`"*, but the only route is `Routes.CreateRecurringBooking(orderId)`, which is **create-from-order**, not edit-template | none |

**So on Android the plumbing is built and dead** — two functions, a request DTO
(`RecurringBookingDtos.kt:69`), and no caller. **On iOS nothing exists below the generated client.**
And a comment in the Android screen asserts an edit path that is not there.

### Why a story panel comes before either implementation ticket

The endpoint's existence does not tell you what editing *means*, and every one of these has a wrong
answer that ships silently:

1. **What is editable?** Frequency, day-of-week, time, address, services/packages, rooms/bathrooms,
   payment type, start date — the create wizard collects all of them. Editing **payment type** on a
   card-paid template touches Stripe. Editing **services** changes the price of every future
   occurrence. Editing **start date** on a template that has already generated orders is either a
   no-op or a reschedule. **These are not one feature.**
2. **What happens to orders already generated from the template?** A recurring template spawns
   concrete orders. Editing the template must either (a) affect only future generations, (b) also
   rewrite pending ones, or (c) ask. **Option (b) can silently change the price of an order a customer
   has already seen** — and if it was card-paid, already been charged for.
3. **Edit vs pause vs delete.** The list screen already ships **pause/resume** and **delete** with a
   deliberate delete dialog (`recurring_bookings_delete_dialog_what_stops` / `_what_stays` /
   `_pause_hint` — five keys, all five locales). Whatever "edit" does must fit next to that
   vocabulary, not contradict it.
4. **The Plus gate.** iOS carries `recurring_plus_gate_title` / `_subtitle` / `_cta` — three keys
   Android does not have. **And `T-0494` establishes that the recurring gate is enforced client-side
   only.** So "can this customer edit this template" is entangled with an authorization defect being
   fixed in parallel. The story must state the gate, and the enforcement lands in T-0494.
5. **The two wizards are different screens** (PM-measured: Android is a 3-step wizard, iOS a
   single-page form, 19 string keys apart — see T-0481). "Reuse the create screen for editing"
   therefore means two different things on two platforms.

## Acceptance criteria

- [ ] **AC1 — the editable field set is ENUMERATED, with a reason per exclusion.** Every field the
      create wizard collects appears in the story as editable or not-editable, and each
      not-editable one carries one sentence. **A field with no verdict is a defect waiting to be
      filed.** Evidence: the table in the story.
- [ ] **AC2 — the already-generated-orders rule is DECIDED and written.** One of (a) future-only,
      (b) rewrite pending, (c) ask the user — with the money consequence spelled out for card-paid
      templates. **This is the AC that a challenger must attack hardest.** Evidence: the decision plus
      the rejected alternatives.
- [ ] **AC3 — the backend contract is CHECKED, not assumed.** Read `UpdateRecurringBooking`'s
      command, handler and validator in `Cleansia.Core.AppServices` and state which of AC1's fields
      it actually accepts today. **If the story wants a field the command does not carry, that is a
      backend ticket and it must be named** — a client ticket cannot invent a contract. Evidence:
      the command's field list at file:line versus AC1's.
- [ ] **AC4 — the entry point is specified for both platforms.** From where does a customer start an
      edit: the list row, an overflow menu, the detail? Same place on both, or a stated divergence
      (ADR-0018). Evidence: the specification.
- [ ] **AC5 — edit / pause / delete are shown to be coherent as a set.** Including what the existing
      delete dialog's "what stops / what stays" copy implies for edit. Evidence: the three-way
      comparison in the story.
- [ ] **AC6 — the Plus gate is stated and ROUTED, not implemented.** The story says whether editing
      is Plus-gated and points enforcement at **T-0494**. Evidence: the routing note.
- [ ] **AC7 — the story is sized per platform and the split is proposed.** If either platform's work
      is `L`, the story proposes the split. **T-0486 (Android) and T-0487 (iOS) are pre-filed and
      will be rewritten from this story's output** — they are placeholders with a dependency, not
      pre-judged scope.
- [ ] **AC8 (Gate 0.5 leg 3)** — the story states what it did not settle and which questions went to
      `questions/open.md` rather than being defaulted.

## Out of scope

- **Any code.** `git diff --stat -- src/` must be **empty**.
- **The client-side-only recurring gate** — that is **T-0494**, an authorization defect that is real
  whether or not editing ever ships.
- **The wizard-shape divergence between platforms** — **T-0481**.
- **Catalog-name localization in the wizard** — **T-0477**.
- **Deleting the dead Android `update()` plumbing.** If the story concludes the existing shape is
  wrong, say so; the deletion belongs to T-0486.

## Implementation notes

**Analyst panel: author + 2–3 challengers + lead**, per `process/deliberation.md`. The living doc for
the domain is updated as part of finalizing — a finalized story with stale docs is not finalized.

**Challenges the panel should expect:** *"the endpoint exists, just call it"* — the counter is AC2:
the endpoint accepting a field does not tell you what happens to money already taken. And *"reuse the
create wizard"* — the counter is that the two create wizards are different screens (T-0481), so that
sentence describes two different builds.

**Escalate rather than default** anything in AC2 that turns out to be a refund/pricing question —
ADR-0009 governs refunds and a recurring edit that changes a charged order's price is in its
territory.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #6).** The remark is **confirmed on both
  platforms** and the asymmetry is PM-verified at file:line: Android has a written-but-uncalled
  `update()` in both its API and repository layers plus a screen comment claiming an edit path that
  does not exist; iOS has nothing below the generated client. Filed as a **story panel first**
  because "edit" is undefined and its money consequence (AC2) is not a developer's call.

## Review
