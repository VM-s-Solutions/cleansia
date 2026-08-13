---
id: T-0495
title: ADR — how a pull-model job board honours a customer's preferred cleaner (and falls back)
status: done
size: M
owner: architect
created: 2026-08-02
updated: 2026-08-04
depends_on: []
blocks: [T-0515, T-0516]
stories: []
adrs: [0036, 0039]
layers: [analyst, architect]
security_touching: false
manual_steps: []
sprint: 15
---

> **REWRITTEN 2026-08-02 after the owner's answer.** The original ticket's AC2 offered three priced
> options including **(c) withdraw the claim**. **The owner: *"It exists, you can select in the app but
> I think it doesn't work fully. And I'd like to have it working fully."*** **Option (c) is
> eliminated.** The ticket is now the **architect panel** that decides the dispatch mechanism.
> `depends_on: [T-0491]` **removed** — the question T-0491 was to settle for this perk (*should we sell
> it at all*) is answered; T-0491 still owns the copy table and is a coordination point, not a blocker.

> **ARCHITECT PANEL REQUIRED (author + 2–3 challengers + lead) — `agents/process/deliberation.md`.**
> Deliverable is an ADR + the living decision doc. `git diff --stat -- src/` must be empty.

## Context

**Every claim below is PM-verified first-hand at `master`, 2026-08-02 — this is no longer relayed.**

| Claim | Verification |
|---|---|
| The customer CAN select a preferred cleaner | **CONFIRMED on all three clients.** iOS `ConfirmStep.swift:77`, `:198`; Android `ConfirmStep.kt:362-363` + `PreferredCleanerPicker.kt`; web `order-wizard.facade.ts:580` sends it (as `undefined` — **the web wizard has no picker**) |
| It is validated and persisted | **CONFIRMED.** `CreateOrder.cs:140-154` → `OrderFactory.cs:124` → `Order.cs:349` |
| It is read by **nothing** | **CONFIRMED.** The only other references are `Order.cs:621` (`AnonymizeCustomerData` nulls it) and `IOrderRepository.cs:85` (a comment). **No query, no ordering, no notification, no assignment reads it.** |
| Dispatch is first-come-first-served | **CONFIRMED.** `TakeOrder.cs` gates on available spots, caller-is-employee, completed profile, approval, weekly limit and time conflict. **`PreferredEmployeeId` appears nowhere in it.** |
| The entity doc describes an algorithm that does not exist | **CONFIRMED.** `Order.cs:217-224`: *"The matching algorithm boosts this employee's score…"* — **and the same comment ends *"today the field exists but no UI sets it"*, which is now false too.** Three clients set it. The comment is stale in both directions |

### The two things to decide, and they are genuinely different

**1. Prioritisation.** This is a **pull** model: cleaners take orders off a board, plus a 30-minute
digest push (`NotificationEventCatalog.NewJobsAvailable = "order.new_available"`,
`Employee.LastNewJobsNotifiedAt`). **A pull model has no assignment step to bias.** The plausible
mechanisms are materially different in cost and in risk:

| Mechanism | What it means | Cost | The risk it carries |
|---|---|---|---|
| **Notify-first** | the preferred cleaner's digest includes it; others' does not, for N minutes | small — the digest sweep already exists | the order sits **unseen** for N minutes if they are asleep |
| **Exclusive hold** | `TakeOrder` refuses everyone else for N minutes | one validator rule + a timestamp | same latency risk, now enforced server-side |
| **Board ordering** | the preferred cleaner sees it at the top; anyone can still take it | cosmetic | **honours nothing** — first-come still wins |
| **Assignment model** | the platform assigns instead of offering | **an epic** | changes how the whole product works |

**2. The fallback is the hard half, not an afterthought.** What happens when the preferred cleaner does
not take it? How long do we wait? Does the customer know? **A booking that sits unclaimed because we
were waiting for one person is a worse outcome than not honouring the preference at all** — and the
customer paid for the perk that caused it.

### What the copy currently promises — three different things again

- iOS `booking_preferred_cleaner_subtitle`: *"**Plus benefit** · choose someone who's cleaned for you before"*
- Android/iOS `membership_perk_favorite_cleaner_desc`: *"Request the same cleaner you trust on every booking."*
- Web `en.json:1097`: *"Pick a cleaner you've worked with before — **they'll be prioritized when matching**."*

Only the web string promises prioritisation. **The ADR's chosen mechanism must be describable in one
sentence a customer can check** — and if it cannot honour "the same cleaner every time", the copy
changes, which the ADR names as a consequence (it does not make the change).

## Acceptance criteria

- [ ] **AC1 — the mechanism is chosen from the table above (or a fifth, argued) and stated in one
      sentence a test could check.** Evidence: the sentence plus the rejected alternatives with
      why-not.
- [ ] **AC2 — the FALLBACK is specified as precisely as the happy path.** The wait duration, what
      releases the order, who is notified, and **what the customer sees while waiting**. An ADR with a
      vague fallback does not pass this AC. Evidence: the fallback state machine (Mermaid, per
      `process/documentation.md`).
- [ ] **AC3 — the assignment-model consequence is stated plainly in ONE SENTENCE AT THE TOP.** If the
      recommendation requires moving from pull to assignment — even partially, even for one order —
      the owner must see it as an architecture change, not discover it in an estimate. Evidence: the
      sentence, or an explicit *"no assignment-model change required"*.
- [ ] **AC4 — the cleaner-side privacy rule is decided.** `Order.cs:221-222` says the preference is
      *"Not exposed to the cleaner side (avoids 'they didn't pick me' awkwardness)"*. **Every mechanism
      except board-ordering leaks it by construction** — a cleaner who alone can see an order for 10
      minutes knows why. Decide: keep the rule and pick a mechanism that respects it, or drop the rule
      deliberately. Evidence: the ruling against that comment.
- [ ] **AC5 — the existing eligibility rule is examined, not inherited.** `CreateOrder.cs:150-154`
      requires the customer to have **completed** an order with that cleaner. Is that the right rule
      once the perk is real? (It makes the perk unusable for a new subscriber's first booking — which
      may be correct, or may be the reason nobody uses it.) Evidence: the ruling.
- [ ] **AC6 — the interaction with `TakeOrder`'s existing gates is worked out.** Weekly order limit,
      time conflict, approval status: **the preferred cleaner may be ineligible for reasons that have
      nothing to do with preference.** Does the hold still apply? Evidence: the interaction table
      against `TakeOrder.cs:38-60`.
- [ ] **AC7 — the recurring-booking path is covered or explicitly excluded.**
      `MaterializeRecurringBookings.cs:138` hardcodes `PreferredEmployeeId: null`. **A recurring
      customer is exactly the customer who wants the same cleaner every time** — this is the strongest
      case for the perk and it is currently wired to null. Evidence: the ruling plus the file citation.
- [ ] **AC8 — the Plus gate is specified (not built) and is server-side**, ready for **T-0516** once
      `Q-PLUS-03` is answered. Both outcomes (universal / Plus-only) are designed for. Evidence: the
      specification.
- [ ] **AC9 — at most three sized implementation candidates.** **This ticket builds nothing.**
      `git diff --stat -- src/` empty. Evidence: the candidates with S/M sizes; **any `L` is split in
      the ADR, not left for the PM to discover.**
- [ ] **AC10 — the ADR is written to `docs/decisions/00NN-*.md` and the living decision doc under
      `agents/architecture/decisions/` is updated in the same step.** Evidence: both files.
- [ ] **AC11 — the deliberation trail (`## Challenge` / `## Defense` / `## Verdict`) stays in the
      artifact.** Evidence: the sections.
- [ ] **AC12 — `Order.cs:217-224`'s stale comment is named for correction** (it describes a scoring
      algorithm that does not exist **and** claims no UI sets the field, which three clients now do).
      The correction lands in **T-0515**, not here. Evidence: the note.
- [ ] **AC13 (Gate 0.5 leg 3)** — state what the panel did not examine.

## Out of scope

- **Building the dispatch rule** — **T-0515**.
- **The Plus gate's implementation** — **T-0516**, blocked on `Q-PLUS-03`.
- **Changing the assignment model.** AC3 *names* it; nothing here changes it.
- **The web wizard's missing picker.** `order-wizard.facade.ts:580` sends `undefined`; **web customers
  cannot select a preferred cleaner at all.** Named here, filed by the PM out of the ADR's output —
  not built in this ticket.
- **The other perks** — T-0492, T-0493, T-0494.

## Implementation notes

**`architect`-led with one `analyst` challenger** for the customer-promise half (AC1/AC2/AC4), because
the mechanism choice is only half a design question — the other half is what we are allowed to say we
sell. Author, challengers and lead are **different instances** per `deliberation.md`.

**Read first:** `TakeOrder.cs` in full, `CreateOrder.cs:140-155`, `Order.cs:217-226` + `:621`,
`OrderFactory.cs:110-130`, `MaterializeRecurringBookings.cs:120-145`,
`NotificationEventCatalog.cs:30`, `Employee.LastNewJobsNotifiedAt` and the 30-minute digest sweep,
`GetMyServingCleaners.cs` (what feeds the picker), and `agents/analysts/notifications.md`.

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** Finding marked RELAYED;
  `analyst`-owned; AC2 permitted *"withdraw the claim"*.
- 2026-08-02 — **REWRITTEN → `ready` by pm after the owner's answer *"I'd like to have it working
  fully."*** Option (c) eliminated, so the ticket stops being a recommendation and becomes an
  **architect panel** on the dispatch mechanism. **All five findings re-grounded first-hand and now
  marked VERIFIED**, including two the audit did not have: `TakeOrder.cs` contains no reference to the
  field at all, and `MaterializeRecurringBookings.cs:138` hardcodes it to `null` for the exact cohort
  most likely to want it. **`depends_on: [T-0491]` removed** — the owner answered the question that
  dependency existed for; T-0491 remains the owner of the copy table and AC1's sentence should be
  handed to it rather than decided twice. `ready`: passes DoR, no unmet dependency, panel is step 1.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). The deliverable is **ADR-0036**
  (`0036-preferred-cleaner-first-refusal-hold.md`), drafted `2caa5f82`, challenged in `eee24957`,
  **accepted `cfcadce5`** ("panel complete, consensus reached 2026-08-02"). The owner's later availability
  instruction produced **ADR-0039** (`be7fece8`, **accepted `182a5660`**), which supersedes ADR-0036
  §D5.1's time-conflict half. **Verified at HEAD:** both ADR headers read `accepted`. The panel killed the
  ADR's own headline safety claim (CH-V1: *"an order stuck held is not expressible"* was false — a null
  beneficiary with a live deadline made an order invisible and un-takeable to everyone for up to 12h), and
  the fix is delivered by construction (`GrantPreferredHold`/`ClearPreferredHold` with no independent
  setter), not by a review checklist.

## Review

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read both ADR headers at HEAD. Confirmed in code that
the construction-level guarantee the panel demanded actually exists: `PreferredHoldUntilUtc` appears in
`Order.cs` with the paired grant/clear mutators as its only writers, and the property's doc comment
(`Order.cs:236-251`) records that there is **no matching algorithm and no score** — which also discharges
T-0515 AC8. **No `manual_steps` on this ticket.**

