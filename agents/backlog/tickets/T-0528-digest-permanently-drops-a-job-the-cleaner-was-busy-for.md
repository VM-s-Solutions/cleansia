---
id: T-0528
title: New-jobs digest permanently drops a job the cleaner was busy for
status: draft
size: M
owner: pm
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, backend]
security_touching: false
manual_steps: []
sprint: 15
source: challenger round on ADR-0034/0035/0036 — `adr/challenges/0036-C-digest.md` §"Verdict on your
  pre-existing-bug characterisation" (confirmed line by line by the optimizer lane). Owner-verified
  2026-08-02. A defect that belongs to no ADR.
---

## Context

**A cleaner who was busy when a job appeared never hears about that job again, even after the conflict
clears.**

`NewJobsDigestService.SendDigestsAsync` runs per cleaner
(`src/Cleansia.Core.AppServices/Services/NewJobsDigestService.cs:86`):

1. `:98-122` selects candidate orders whose **latest** `OrderStatusTrack.CreatedOn` is newer than the
   cleaner's watermark `LastNewJobsDigestAt` (`:90`, `:109-114`).
2. `:134-143` filters them **per order** with `HasOverlappingOrderAsync` — an order the cleaner is already
   busy for is not counted.
3. `:145-149` — if `takeable == 0`, `continue` **without stamping**. This branch is **correct**.
4. `:168` — otherwise push `["count"] = takeable`, then `:182` `StampWatermarkAsync(sweepStartedAt)`.

The watermark is a **single per-cleaner scalar** (`Employee.MarkNewJobsDigestSent`). Step 4 advances it
past **every** candidate in the sweep, including the ones step 2 dropped. Their status tracks are now older
than the watermark, so `s.CreatedOn > sinceUtc` at `:114` can never be true for them again. **The skipped
orders are permanently un-notifiable for that cleaner** — the conflict clearing does not bring them back,
because nothing rewrites the order's status track when a *different* order is cancelled.

**Narrow in logic, broad in incidence.** The no-stamp guard only fires when **every** candidate overlaps —
i.e. the cleaner is simultaneously busy at the cleaning time of all of them. The loss fires whenever the
cleaner was free for **even one** job, which is the ordinary state of a cleaner in a marketplace with any
order flow. The optimizer lane's wording is the right one: *"a skipped order is lost as soon as the cleaner
is notified about anything else."* And the `takeable == 0` branch is a **deferral, not a mitigation** — it
postpones the burn to the first sweep where the cleaner is free for anything, typically the next 30-minute
tick, at which point all of them are burned together.

**A fourth stamp site that a naive fix will break.** `:158-166` — the **muted** branch stamps
unconditionally (after `takeable >= 1`), with an explicit comment justifying it: *"otherwise re-enabling
the toggle would burst a backlog of 'new' jobs that are no longer fresh."* That stamp is **deliberate and
correct for a different reason**, and someone fixing this ticket will delete it by accident. It is called
out here so that cannot happen quietly.

**Root cause, worth writing down once.** `LastNewJobsDigestAt` assumes eligibility is (a) monotone in time
and (b) derivable from a **global** timestamp on the order. Both fail for any **per-cleaner, non-monotone**
rule. The overlap filter is the first such rule in this codebase — it can flip *back* to eligible when the
conflicting job is cancelled or completed. That is why this is an architect ticket and not a one-liner.

## Acceptance criteria

- [ ] **AC1 — the mechanism is chosen on the record.** Given the option space below, When the architect
      rules, Then a decision is recorded in `agents/architecture/decisions/` naming the mechanism, the
      rejected options and why, and stating the structural limit explicitly: *the watermark is a single
      per-cleaner scalar and cannot express a per-cleaner, non-monotone eligibility rule.*
      **Evidence:** the living-doc diff.
- [ ] **AC2 — the losing case is pinned red first.** Given a cleaner, two new orders A and B, where the
      cleaner has a live commitment overlapping **A only**, When a sweep runs, Then the digest is sent with
      `count == 1`; And When A's conflict is removed and the next sweep runs, Then **A is notified**.
      **Evidence:** an automated test that fails against `master` today. This is the ticket's whole point —
      a green suite that does not contain this test has not verified anything.
- [ ] **AC3 — the guard branch still holds.** Given a cleaner for whom **every** candidate overlaps, When
      the sweep runs, Then no push is enqueued **and the watermark does not move** (`:145-149` behaviour
      preserved).
- [ ] **AC4 — the muted branch is preserved deliberately.** Given a cleaner who muted
      `NotificationCategory.NewJobsAvailable`, When a sweep runs with `takeable >= 1`, Then the watermark
      **still advances** and no push is sent, and the comment at `:161-163` still explains why.
      **Evidence:** an automated test, so the next person cannot delete it silently.
- [ ] **AC5 — no duplicate-push regression.** Given a cleaner and an unchanged set of orders, When two
      consecutive sweeps run, Then the second sends **no** push. Whatever mechanism AC1 chooses must not
      buy AC2 by re-notifying about everything forever.
- [ ] **AC6 — the count is honest.** Given the push at `:168-177`, When it is enqueued, Then `["count"]`
      equals the number of jobs the cleaner will actually find takeable on the board at that moment. A
      cleaner told "3 new jobs" who finds 2 has been given a number this ticket had the chance to fix.
- [ ] **AC7 — the never-notified cleaner is bounded.** Given a cleaner with `LastNewJobsDigestAt == null`,
      When the sweep runs, Then `sinceUtc` is **not** `DateTimeOffset.MinValue` (`:90`) — a new hire
      currently matches *every available order in their country, ever*, and then runs
      `HasOverlappingOrderAsync` once per row. **Evidence:** a test with a null watermark and >N historical
      orders asserting a bounded candidate set. The code comments at `:118-119` and `:131-133` assert this
      set is *"tiny"* and *"bounded"*; both are false in exactly this case, and whichever way AC1 goes, the
      comments must end up true or be deleted (same rule as T-0530).

## Out of scope

- **The per-sweep query cost.** `HasOverlappingOrderAsync` scanning the cleaner's entire lifetime
  assignment history (no `CleaningDateTime` lower bound) and the `C`-times-repeated country scan are real
  and are the optimizer lane's finding, not this ticket's. **Do not** hoist the loop or regroup the sweep
  by `WorkCountryId` here — that is a separate ticket, and mixing it in would make this an `L` and destroy
  the reviewability of the correctness fix.
- The **tenancy** defect in `StampWatermarkAsync` — **T-0529**. It is a one-line fix in the same method;
  serialize the two, do not merge them.
- The digest's status-set divergence from `DashboardSpecifications` — **T-0530**.
- Anything in ADR-0036's `PreferredHoldUntilUtc` design. That ADR's D5.3 is a *second* patch on this same
  structural limit; this ticket fixes the *first* instance and must not pre-empt the ADR's adjudication.

## Implementation notes

**The option space the architect is ruling on** (starting points, not a decision):

1. **Do not stamp past what was skipped.** Stamp the watermark to the oldest skipped candidate's track
   time instead of `sweepStartedAt`. Cheapest; costs re-evaluation of everything newer next sweep, and the
   duplicate-suppression AC5 has to come from somewhere else.
2. **Per-cleaner per-order notified ledger.** Exact, and it is the honest data model for a per-cleaner
   non-monotone rule — but it is a new table, a migration, and a growth/retention question. Likely an `L`
   → would have to be split.
3. **Move the overlap test into the candidate query** so "eligible for this cleaner" is one predicate and
   the watermark only ever advances past rows that were genuinely offered. Keeps one scalar; changes the
   query shape (and interacts with the out-of-scope cost work — say so if chosen).

**Whichever is chosen, the ordering constraint is the same one the code already documents at `:179-181`:**
the feed + outbox rows and the watermark advance must stay in one commit, so the push is durable iff the
watermark moved.

**Archetype:** `agents/knowledge/patterns-backend.md` — background sweep services (`MaterializeRecurringBookings`
is the house example of a per-iteration tenant-aware loop).

## Status log
- 2026-08-02 — draft (created by pm from the challenger round). **Not `ready`:** DoR item 7 needs the AC1
  architect ruling, and option 2 would need splitting before it could run.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
