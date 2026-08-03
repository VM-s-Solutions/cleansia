---
id: T-0528
title: New-jobs digest permanently drops a job the cleaner was busy for
status: draft
size: M
owner: pm
created: 2026-08-02
updated: 2026-08-03
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

- [~] **AC1 — the mechanism is chosen on the record.** *(ruling written in `## Review`; the living-doc entry is the architect's to land)* Given the option space below, When the architect
      rules, Then a decision is recorded in `agents/architecture/decisions/` naming the mechanism, the
      rejected options and why, and stating the structural limit explicitly: *the watermark is a single
      per-cleaner scalar and cannot express a per-cleaner, non-monotone eligibility rule.*
      **Evidence:** the living-doc diff.
- [x] **AC2 — the losing case is pinned red first.** Given a cleaner, two new orders A and B, where the
      cleaner has a live commitment overlapping **A only**, When a sweep runs, Then the digest is sent with
      `count == 1`; And When A's conflict is removed and the next sweep runs, Then **A is notified**.
      **Evidence:** an automated test that fails against `master` today. This is the ticket's whole point —
      a green suite that does not contain this test has not verified anything.
- [x] **AC3 — the guard branch still holds.** Given a cleaner for whom **every** candidate overlaps, When
      the sweep runs, Then no push is enqueued **and the watermark does not move** (`:145-149` behaviour
      preserved).
- [x] **AC4 — the muted branch is preserved deliberately.** Given a cleaner who muted
      `NotificationCategory.NewJobsAvailable`, When a sweep runs with `takeable >= 1`, Then the watermark
      **still advances** and no push is sent, and the comment at `:161-163` still explains why.
      **Evidence:** an automated test, so the next person cannot delete it silently.
- [x] **AC5 — no duplicate-push regression.** Given a cleaner and an unchanged set of orders, When two
      consecutive sweeps run, Then the second sends **no** push. Whatever mechanism AC1 chooses must not
      buy AC2 by re-notifying about everything forever.
- [x] **AC6 — the count is honest.** Given the push at `:168-177`, When it is enqueued, Then `["count"]`
      equals the number of jobs the cleaner will actually find takeable on the board at that moment. A
      cleaner told "3 new jobs" who finds 2 has been given a number this ticket had the chance to fix.
- [x] **AC7 — the never-notified cleaner is bounded.** Given a cleaner with `LastNewJobsDigestAt == null`,
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
- 2026-08-03 — backend implemented, test-first. **Red first:** the three new failing tests were written and
  run against unmodified `master` before any implementation —
  `A_Job_The_Cleaner_Was_Busy_For_Is_Notified_Once_The_Conflict_Clears` (*Assert.Single() Failure: The
  collection was empty* — the second sweep sends nothing at all),
  `A_Never_Notified_Cleaner_Only_Considers_Jobs_That_Have_Not_Started_Yet` (*Expected "1", Actual "13"*),
  `The_Digests_Slot_Releasing_Statuses_Are_Exactly_The_Overlap_Predicates_Non_Blocking_Ones` (field absent).
  The three that pin existing-correct behaviour (AC3/AC4/AC5) were green before and after. **Green:**
  `Cleansia.Tests` 2594 passed / 0 failed (baseline 2588), `Cleansia.IntegrationTests` 117 passed / 0 failed.
  **No migration.** Lane: `NewJobsDigestService.cs` + its tests only.

## Review

### AC1 — the mechanism, and why the losers lost (backend lane; needs an architect to land the living-doc entry)

**The structural limit, stated:** *`Employee.LastNewJobsDigestAt` is a single per-cleaner scalar. It can
express "newer than X" and nothing else, so it cannot express a per-cleaner, non-monotone eligibility rule
— and the overlap filter is exactly that: an order the cleaner was busy for becomes takeable again when a
DIFFERENT order is cancelled or completed, an event that writes nothing on the candidate.* That sentence is
the ticket's root cause and it survives this fix: what ships is a **second freshness source**, not a
replacement for the scalar.

**Chosen: option 3', a per-cleaner second freshness disjunct — ADR-0036 §D5.3's shape, one layer down.**
Freshness becomes, per cleaner, disjunctive and upper-bounded at the sweep's own start instant:

```
fresh(o) ⟺ ∃ h ∈ o.OrderStatusHistory : h.CreatedOn > watermark          -- the order is new
        ∨ ( o overlaps the window a commitment of MINE released,          -- I became free
            where that release happened in (watermark, sweepStartedAt] )
```

- The released window is one query per cleaner-with-a-watermark, floored at
  `sweepStart − Order.MaxOrderSpanHours` on the **same** safety argument as the overlap predicate's own
  scan floor (a commitment whose window closed before the sweep started cannot overlap a candidate,
  because candidates have not started yet). The windows found are merged into one interval.
- The merge and the interval test are **deliberately over-approximating** — they re-offer everything the
  release *could* have been blocking. Widening freshness costs one extra evaluation of
  `HasOverlappingOrderIgnoringTenantAsync`, which still decides takeability; narrowing it loses the job
  forever, which is the defect.
- The upper bound is load-bearing exactly as D5.3 says: it makes the release consumed by **one** sweep (the
  watermark lands on the same instant), so AC5 holds.
- **D5.3's cheaper query is taken while here:** `latest track > k ⟺ ∃ track > k`, so the correlated
  `OrderByDescending(CreatedOn).Take(1).Any(...)` top-N is **deleted**, not wrapped.
- **Zero new state, zero migration.** The scalar is untouched and still advances to `sweepStartedAt`.

**Why the losers lost:**

| Option | Verdict |
|---|---|
| **1 — stamp only up to the oldest skipped candidate** | **Rejected: unbounded duplicate pushes.** A skipped candidate persists until somebody else takes it — days. Every 30-min sweep in between re-counts every takeable order newer than the held-back watermark and re-pushes an identical count. It buys AC2 by breaking AC5 permanently, which AC5 names as the thing not to do. |
| **2 — per-cleaner-per-order notified ledger** | **Rejected here, not wrong.** It is the honest data model, and it is the only option that could also delete the residual duplicate below. It is a new table + a migration (owner-only) + a growth/retention question, i.e. an `L` needing a split, for a defect closable additively today. Record it as the durable answer if a third non-monotone rule appears. |
| **3 as literally worded — "move the overlap test into the candidate query"** | **Rejected: it is not a fix.** The watermark advance is time-keyed, not candidate-keyed. Filtering in SQL instead of in C# changes nothing about which rows the *next* sweep considers; the skipped order's tracks are still older than the new watermark. Written down because it reads plausible. |
| **3'' — coarse trigger: any release ⇒ re-scan the cleaner's whole board** | **Rejected on noise.** Same query cost as the chosen form, but every completed job re-pushes the entire board count to a cleaner who already knew about all of it. The chosen form is precise enough that a release in an hour nothing was queued for produces **no** push. |
| **Detect the release per-candidate (exact conflict matching)** | **Rejected: it re-derives interval-overlap arithmetic in a second place**, which is the ADR-0037 disease, and cannot be pinned without a real-provider equivalence test. The chosen form duplicates **one enum set** instead, and that IS pinned (below). |

**The one duplication this fix creates, and its red artifact.** `SlotReleasingStatuses = {Completed,
Cancelled}` in the digest must stay the exact complement of `OrderRepository.SlotBlockingStatuses`, which is
`private` and in another assembly.
`NewJobsDigestSkippedJobRecoveryTests.The_Digests_Slot_Releasing_Statuses_Are_Exactly_The_Overlap_Predicates_Non_Blocking_Ones`
reflects both sets and fails naming both lists — so an eighth `OrderStatus`, or a member moving between
them, goes red instead of silently making the digest miss the event that frees a cleaner.

**Residual, deliberately accepted (a ledger is the only cure):** a release can re-offer an order the cleaner
was *already* told about, if that order sits in the freed window and was takeable all along. Bounded by the
30-min cadence and by the merged window; the count stays honest (AC6) because those orders genuinely are
takeable on the board.

### AC6 / AC7 notes for the reviewer

- **AC6 is satisfied in the direction it names.** `["count"]` is the number of *fresh* candidates that pass
  the real overlap predicate, so it is always ≤ what the cleaner finds takeable. Never "told 3, finds 2".
- **AC7 — judged in scope, because this design made it load-bearing.** `sinceUtc` is no longer
  `DateTimeOffset.MinValue`; a null watermark now means "no freshness conjunct at all", and the candidate
  set is bounded by a new **`CleaningDateTime >= sweepStartedAt`** floor — a job whose cleaning has already
  started is not a new job. Without that floor the released-window disjunct would re-open the same
  ever-growing stale tail on every release, so the fix is not optional here. The AC's literal wording
  ("`sinceUtc` is not `MinValue`") is met by removing `sinceUtc` from that path entirely rather than by
  choosing a different constant; its **evidence** requirement (a null watermark + >N historical orders ⇒ a
  bounded candidate set) is met exactly, and asserted on the probe count, not just the push count. The
  comments at the old `:118-119` / `:131-133` that claimed the set was *"tiny"* and *"bounded"* are rewritten
  to say what actually bounds it.
- **Known divergence this creates, flagged not hidden:** the partner board
  (`DashboardSpecifications.CreateAvailableOrdersSpec`) passes `cleaningDateFrom: null`, so it still lists
  offerable orders whose cleaning time has passed; the digest no longer notifies about them. The divergence
  is in the safe direction for AC6 (under-count, never over-count) and the digest already carries its own
  conjuncts (country, not-assigned, freshness) that the board does not. **Whether a past-dated open order
  should sit on the board forever at all is a separate question** — an architect call, outside this lane.

### Out-of-scope items respected

- The muted branch at the fourth stamp site is unchanged and now has a test (AC4), including its comment.
- The `takeable == 0` no-stamp branch is unchanged and now has a test (AC3).
- The per-sweep query cost is **not** hoisted or regrouped. This change adds **one** query per
  cleaner-with-a-watermark (the released-window lookup, driven by that cleaner's own assignments). It
  batches naturally into the filed digest-redesign ticket alongside the existing per-order overlap loop —
  note it there.
- `OrderRepository`, `OrderAvailability`, `OrderSpecification`, `TakeOrder`, `BookingPolicy`,
  `CleanupStalePendingOrders` untouched.

### Catalog harvest

`agents/knowledge/patterns-backend.md` — one bullet added beside ADR-0036's *"know the limit you are
patching"*: the second-freshness-source shape, the over-approximation asymmetry, and the
duplicate-one-enum-set-over-one-arithmetic-predicate preference.

### Hand-off

**AC1's living-doc entry is the architect's to land** (or to overrule): the ruling above is written in
decision form so it can be lifted into `agents/architecture/decisions/` verbatim. ADR-0036 §D5.3 already
predicts it ("*This is a point fix, not a class fix, and the overlap variant is filed separately*") — this
is that variant, and it does not contradict D5.3; it shares its shape.
