---
id: T-0691
title: Confirmed meant two things — money settled OR a cleaner took the job
status: done
size: L
owner: —
created: 2026-09-08
updated: 2026-09-08
depends_on: [T-0687]
blocks: []
stories: []
adrs: [ADR-0057]
layers: [backend, frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**In plain terms:** when a customer paid by card, their booking instantly said "Confirmed" — even
though nobody had agreed to clean their flat yet. Pay cash, and the same booking said "New" until a
cleaner actually took it. Same situation, two different words, because "Confirmed" was doing two
unrelated jobs.

Three producers wrote it, and only one involved a cleaner:

| Writer | What actually happened | Cleaner? |
|---|---|---|
| `HandlePaymentNotification` | money settled | no |
| `ConfirmRecurringOrder` (cash) | the customer confirmed their own occurrence | no |
| `TakeOrder` | a cleaner accepted the job | **yes** |

**It had already cost real money.** A customer who paid by card and cancelled twenty minutes later was
billed a 25% "acceptance" fee for a cleaner who did not exist, because the webhook's `Confirmed` track
read as acceptance. That produced `CancellationAcceptanceSignalTests`, and the fix at the time was to
make the assessor read `AssignedEmployees` instead — one of **three** separate comments in the codebase
whose job was to tell a reader not to trust this status word.

## Decisions taken

Owner ruling 2026-09-08: *"No, confirm is when the cleaner actually took the job and new is new. Keep
separate."* Recorded as **[ADR-0057](../../../docs/decisions/adr-0057.md)**, which supersedes ADR-0037
D1's status term only.

1. **`Confirmed` means a cleaner took the job.** Producers: `TakeOrder`, and `AdminReassignOrder` for an
   admin assigning on a cleaner's behalf.
2. **A paid card order rests at `New` + `Paid`** — the same fulfilment state as cash, differing only on
   the axis where the difference actually lives.
3. **The offerability status term stops qualifying payment.** `Confirmed ∨ (New ∧ Cash)` becomes
   `New ∨ Confirmed ∨ OnTheWay ∨ InProgress`. The `∧ Cash` existed only to compensate for the overload.
4. **`ConfirmRecurringOrder` stops writing it too.** Changing only the webhook would have left the word
   meaning two things and bought nothing for the full documentation and test cost.

## What shipped, in order

Three commits, deliberately sequenced.

**A — the predicate, alone and provably inert.** `New + Card + Paid` was *unreachable* before this
change: all three writers of `PaymentStatus.Paid` either appended `Confirmed` in the same commit or ran
on a cash order that already had a crew. So relaxing the term changed the offerable set of zero live
orders, and made the board ready before any order could rest there. Both evaluation forms moved
together — a one-sided edit shows a cleaner a job and then refuses the take.

**B — the writers.** The webhook and the recurring confirm write the money axis only. `AdminReassignOrder`
gained a `Confirmed` append it never had: it created an assignment and appended no status, harmless only
because card orders arrived already `Confirmed`. Without it an admin-assigned order would sit at `New`
with a crew — false under the new meaning, and invisible to the six sweeps that select `Confirmed` AND
`AssignedEmployees.Any()`.

**C — the customer's screens and the record.** `New` gained an explicit case in the customer status icon
and severity pipes; it had been falling through to a generic dot, the same defect shape as
[T-0687](T-0687-admin-order-list-renders-a-blank-status.md).

## The thing that did NOT need changing, and why it is worth knowing

**Both mobile booking timelines were already correct.** They key the assignment step on
`cleanerAssigned` rather than on the status, and their confirmation step is captioned **"Cleaner
confirmed"**. So under the OLD meaning a paid card order falsely showed a cleaner confirmed; under the
new one it honestly shows the search still running. The scoping expected this to be the most dangerous
customer-facing regression in the change — "the customer believes their payment failed" — and it turned
out the two client teams had already written to the meaning the ruling only now makes true.

## Deliberately NOT done

- **`Confirmed` can still be true with zero assignees.** A drop, cover request or admin rejection removes
  the assignment without walking the status back. That is pre-existing, and it means the new meaning is
  precisely "a cleaner took this job", not "a cleaner is on it". Walking the status backwards would be a
  new transition, and `AdminOverrideOrderStatus` forbids backward moves for admins — its own decision.
- **The push key `order.confirmed` keeps its name** while describing a money event. Renaming costs ten
  locale files across two mobile platforms plus the feed catalogue, and the customer still needs telling
  their payment landed. The copy guards forbidding it from claiming a cleaner are unaffected.
- **No invariant that `Confirmed` implies at least one assignment.** Roughly eight test files build
  exactly that fixture; adding the invariant would triple the diff for no behaviour the ruling asked for.

## Status log

- 2026-09-08 — ruled, built and green. No enum member, integer, schema or index changed, so no migration,
  no DEV drop, no client regeneration and no mobile spec re-dump. 4392 unit + 223 integration + 158 host
  + 69 frontend project suites green.
