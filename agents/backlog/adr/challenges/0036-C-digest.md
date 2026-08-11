# ADR-0036 — Challenger C (`optimizer`), lane: digest interaction + query cost

**Mode:** CHALLENGER. REFUTED-by-default. Every claim below cites `file.cs:line` from source I read in
the working tree on 2026-08-02.

## Method — what I actually ran (Gate 0)

I did **not** connect to DEV, did not query any database, did not touch git, did not edit the ADR.

I **did** build a throwaway harness outside the repo
(`/private/tmp/.../scratchpad/sqlprobe/`) that references `Cleansia.Infra.Database` +
`Cleansia.Core.AppServices`, wires the real `CleansiaDbContext` (real entity configs, real tenant
filter) to the **Npgsql provider against a dead connection string**, and prints `ToQueryString()`.
`ToQueryString()` compiles the query and renders provider SQL **without opening a connection**. So the
SQL blocks quoted below are the **actual PostgreSQL this codebase emits**, not my reconstruction. What
I do *not* have is `EXPLAIN` output or row counts — every statement about *plan choice* below is
labelled as reasoning about the emitted SQL, not a measured plan.

Five queries were rendered: **A** = today's digest predicate verbatim from
`NewJobsDigestService.cs:98-122`; **B** = the semi-join rewrite of the same predicate; **C** =
`OrderRepository.HasOverlappingOrderAsync` verbatim (`:272-292`); **D** = D5.3 written literally as
`max(...)`; **E** = D5.3 written as a disjunction with an upper bound; **F** =
`CreateAvailableOrdersSpec` + `OrderVisibility.NotHeldFromEmployee`. Because
`Order.PreferredHoldUntilUtc` does not exist yet, D/E/F use `Order.RecurringReminderSentAt`
(`Order.cs:243`) as the stand-in — it is the **same declared type and the same column type**
(`DateTime?` → `timestamp with time zone`, migration `20260723182623_Initial.cs:1040`), which is exactly
what the ADR proposes, so the emitted SQL shape is faithful.

---

### CH-Q1 — D5.3 **as the ADR words it** compiles to a per-row `CASE` over **two** correlated scalar aggregates plus a per-row `::timestamptz` cast. It is not a small change to the WHERE clause; it is the single worst query in the design, and the ADR's own §verify #6 is what steers the implementer into it.

**The hole.** D5.3 (`ADR:334-338`) specifies the fix as

```
availableToCleanerAt = (cleaner is the preferred one)
    ? latest OrderStatusTrack.CreatedOn
    : max(latest OrderStatusTrack.CreatedOn, PreferredHoldUntilUtc)
```

and §verify #6 (`ADR:617-621`) makes that literal wording the *review gate*: *"it compares the watermark
against `max(latest status-track CreatedOn, PreferredHoldUntilUtc)`"*. A developer who satisfies that
gate literally emits this (probe **D**, real Npgsql output):

```sql
AND CASE
    WHEN o."PreferredHoldUntilUtc" IS NOT NULL
     AND (o."PreferredEmployeeId" <> @emp OR o."PreferredEmployeeId" IS NULL)
     AND o."PreferredHoldUntilUtc"::timestamptz > (
        SELECT max(o2."CreatedOn") FROM "OrderStatusHistory" AS o2 WHERE o."Id" = o2."OrderId")
    THEN o."PreferredHoldUntilUtc"::timestamptz
    ELSE (
        SELECT max(o3."CreatedOn") FROM "OrderStatusHistory" AS o3 WHERE o."Id" = o3."OrderId")
END > @p
```

Three costs, all visible in the emitted SQL:

1. **Two** correlated `max()` SubPlans over `OrderStatusHistory` per candidate row (`o2` and `o3`),
   where today there is one.
2. The whole thing sits inside a `CASE` in a **non-sargable** position — no semi-join, no
   short-circuit, no index-only path is available to the planner. Today's form (probe **A**) is at
   least an `EXISTS`.
3. `o."PreferredHoldUntilUtc"::timestamptz` — an explicit **cast on a column, per row**, forced by the
   ADR's choice of `DateTime?` for the hold (`ADR:119`) against `OrderStatusTrack.CreatedOn`, which is
   `DateTimeOffset` (`Auditable.cs:9`). A cast on the column side kills any index that column could
   ever have.

**Why it matters.** This runs inside the per-cleaner loop at `NewJobsDigestService.cs:86`, i.e. once
per cleaner per 30-minute sweep, 48 sweeps/day. The ADR half-guessed this at `:936` (*"D5.3's `max(...)`
comparison inside the digest's per-cleaner loop is the most likely performance regression in this
design"*) — it was right about the location and wrong about the cause. The cause is not `max`; it is
that the ADR wrote a *value* comparison where a *boolean* comparison was available.

**What I want changed.** Rewrite D5.3 and §verify #6 to specify the **disjunctive** form and to
**forbid** the value form. `max(a,b) > k ⟺ a > k OR b > k`. Probe **E** emits, with no `CASE`, no
scalar subquery, no cast on any column:

```sql
AND (o."PreferredHoldUntilUtc" IS NULL OR o."PreferredHoldUntilUtc" <= @now
     OR o."PreferredEmployeeId" = @emp)
AND (EXISTS (SELECT 1 FROM "OrderStatusHistory" AS o2
             WHERE o."Id" = o2."OrderId" AND o2."CreatedOn" > @since)
     OR ((o."PreferredEmployeeId" <> @emp OR o."PreferredEmployeeId" IS NULL)
         AND o."PreferredHoldUntilUtc" > @since AND o."PreferredHoldUntilUtc" <= @now))
```

§verify #6 must become: *"the digest's freshness clause contains **no** `CASE`, **no** `GREATEST`, and
**no** scalar `(SELECT max(...))` — it is a disjunction of an `EXISTS` semi-join and a bounded column
comparison. A `CASE`/`GREATEST` here is a hard reject."* And T-0515 must attach the
`ToQueryString()` output of the final query to the ticket. That is a two-line addition to the AC and it
converts an unverifiable instruction into a checkable one.

---

### CH-Q2 — **BLOCKING.** D5.3's formula is a *lower-bound* comparison against an instant that can be in the **future**. As specified it marks a held order "new" from creation onward, inflates the digest's `count`, and burns the cleaner's watermark **past the hold expiry before the order is ever takeable** — reintroducing the exact defect D5.3 exists to fix.

**The hole.** The digest's freshness test is one-sided: `availableAt > sinceUtc`
(`NewJobsDigestService.cs:109-114`). That is safe today only because the only value ever fed into it —
`OrderStatusTrack.CreatedOn` — is **always in the past**. `PreferredHoldUntilUtc` is the first
availability instant in this system that is in the **future** at evaluation time.

Walk it with the ADR's own worked example (`ADR:177`, 24 h lead → 2 h 24 hold):

| t | watermark (`LastNewJobsDigestAt`) | `max(latest track, hold)` | `> since`? | outcome |
|---|---|---|---|---|
| T0 | order created, hold → T0+2h24 | — | — | — |
| T0+30m | T0 | `max(T0, T0+2h24) = T0+2h24` | **TRUE** | order counted in `takeable` (`:142`), pushed as "N new jobs" (`:173`), watermark stamped to **T0+30m** (`:182`) |
| T0+1h | T0+30m | T0+2h24 | **TRUE** | counted again, watermark → T0+1h |
| … | … | … | … | … |
| T0+2h30 | T0+2h24… | T0+2h24 | **FALSE** once the watermark passes T0+2h24 | the order is now genuinely available — and is now permanently stale |

So the cleaner is pushed about an order they **cannot see** — D5 hides it at surfaces 1–3
(`ADR:273-278`) and `TakeOrder` refuses it as `OrderNotFound` (D5.2) — and by the time it *is*
available their watermark has already walked past its availability instant. **The order becomes
board-only. That is Fact B (`ADR:76-84`) reproduced by the fix for Fact B.**

Two secondary harms in the same defect: the push's `["count"]` argument (`:173`) is a **lie** for the
whole hold window (the cleaner opens the board and finds fewer jobs than the push claimed), and the
ADR's own D4 privacy line (`ADR:254-256`, *"no surface ever says 'held for someone else'"*) is
undermined — a cleaner who is repeatedly told "3 new jobs" and repeatedly finds 2 has been told
something about an order they are not supposed to know exists.

**What I want changed.** D5.3 must specify a **window, not a lower bound**:

```
availableAt ∈ (sinceUtc, sweepStartedAt]      // not just  > sinceUtc
```

i.e. the `AND o."PreferredHoldUntilUtc" <= @now` conjunct in probe **E** is not an optimisation, it is
the correctness condition, and it must be in the ADR text. Concede-and-revise, not rebut. This is a
blocking amendment: shipping D5.3 as currently worded is strictly worse than shipping no digest change
at all, because it adds a false push count on top of the un-notification it was meant to prevent.

---

### CH-Q3 — **TC-PREF-DIGEST-0 cannot fail against the defect in CH-Q2.** The ADR's one red-first pin for its one blocking finding is not sensitive to the bug.

**The hole.** TC-PREF-DIGEST-0 (`ADR:654-656`) is: *"An order held for 45 min, not taken; after expiry a
non-preferred cleaner whose watermark is newer than the order's status track still receives it in the
next digest."* One sweep, run **after** expiry.

Under the buggy lower-bound form: `hold(T0+45m) > since` → true → notified → **test passes**.
Under the correct windowed form: `hold > since AND hold <= now` → true → notified → **test passes**.

The test cannot distinguish the two implementations. It pins the wrong half of the behaviour. The ADR
asserts at `:656` *"Fails against a naive implementation"* — that is true of the *no-fix* baseline and
false of the naive *fix*, which is the implementation an implementer is actually going to write given
§verify #6's wording (CH-Q1).

**What I want changed.** Split it in two, and make the second one the blocking pin:

- **TC-PREF-DIGEST-0a (during):** hold live, sweep runs, a *second* unrelated order is also new →
  assert the digest is sent, `count == 1` (**not 2**), and the held order is **not** counted.
- **TC-PREF-DIGEST-0b (after, with an intervening notified sweep):** same fixture, the watermark has
  already been advanced by 0a's sweep to an instant **later than the order's latest status track and
  earlier than the hold expiry**; advance the clock past `PreferredHoldUntilUtc`; assert the order
  **is** counted in the next digest. 0b is the one that goes red against the naive fix.

Without 0a, "green tests" mean nothing here.

---

### CH-Q4 — The digest's `nowUtc` is unspecified, and the only safe value is `sweepStartedAt` — the same instant that gets stamped. Any other choice re-opens CH-Q2 in a smaller window.

**The hole.** `OrderVisibility.NotHeldFromEmployee(string employeeId, DateTime nowUtc)` (`ADR:267`)
takes `nowUtc` from the caller, and the ADR never says what the digest passes. The digest stamps
`sweepStartedAt`, captured once at `NewJobsDigestService.cs:57` and used at `:164` and `:182`, and the
comment at `:206-209` explains exactly why: *"Uses sweep-start (not now()) so orders that became
available DURING the sweep are picked up by the next run."*

If the D5.3 predicate uses `DateTime.UtcNow` while the watermark stamps `sweepStartedAt`, the two
disagree by the sweep's duration — which, per CH-Q6, is not milliseconds. In that gap the safe and
unsafe directions are asymmetric: `now` **later** than the stamp ⇒ the order is counted now and stamped
below ⇒ harmless duplicate next sweep. `now` **earlier** than the stamp ⇒ **permanent loss**. Since the
sweep is a long loop, "later" is what you get by accident — but only if you also do not restructure the
loop. It is not a property worth leaving to luck.

**What I want changed.** One sentence in D5.3: *"the digest passes `sweepStartedAt` as `nowUtc`, the
same value it stamps; it must never call `DateTime.UtcNow` inside the loop."* Plus §verify: grep the
digest for `UtcNow` — one hit, at `:57`.

---

### CH-Q5 — The existing freshness subquery is **already** the wrong shape, and the ADR's fix should replace it rather than wrap it. Written correctly, D5.3 makes the digest query **cheaper than it is today**.

**The hole.** Today (probe **A**, real emitted SQL from `NewJobsDigestService.cs:109-114`):

```sql
AND EXISTS (
    SELECT 1 FROM (
        SELECT o2."CreatedOn" FROM "OrderStatusHistory" AS o2
        WHERE o."Id" = o2."OrderId"
        ORDER BY o2."CreatedOn" DESC
        LIMIT 1
    ) AS o3
    WHERE o3."CreatedOn" > @p)
```

A **top-N subquery per candidate row**, wrapped in `EXISTS`. But `latest.CreatedOn > k` is `max > k`,
and `max(x) > k ⟺ ∃x: x > k`. The top-N is unnecessary: probe **B** emits the logically identical

```sql
AND EXISTS (SELECT 1 FROM "OrderStatusHistory" AS o2
            WHERE o."Id" = o2."OrderId" AND o2."CreatedOn" > @p)
```

which is a plain semi-join the planner can satisfy from `IX_OrderStatusHistory_OrderId` (migration
`20260723182623_Initial.cs`, `IX_OrderStatusHistory_OrderId` — **the only index on that table**;
`OrderStatusTrackEntityConfiguration.cs:11-15` pins nothing else) and **stop at the first qualifying
row**, and which is eligible for a hash/merge semi-join across the whole outer set instead of a
per-row nested loop. The `LIMIT 1` form must materialise the top row for every candidate.

Note the second-order consequence: today's form is the one that would *want* an
`(OrderId, CreatedOn DESC)` index, and that index does not exist. The semi-join form removes the need
for it. So the right answer to "does D5.3 need a new index?" is **no, and it removes a latent index
requirement** — provided the rewrite happens.

**Why it matters here.** The whole framing of D5.3 as "one expression change" (`ADR:342`) rests on
treating the existing subquery as a fixed thing to be wrapped. It is not fixed; it is the thing that
should be replaced. Probe **E** is *strictly cheaper than probe A* — it drops the top-N and adds two
residual column tests. **This makes D5.3 a performance improvement, not a regression** — the opposite
of what the ADR concedes at `:936` — but only in the disjunctive form.

**What I want changed.** D5.3 states explicitly that the latest-history top-N is replaced by an
`EXISTS` semi-join in the same edit, with the one-line equivalence proof (`max > k ⟺ ∃ > k`) in the ADR
so a later reader does not "restore" the top-N thinking it was load-bearing.

---

### CH-Q6 — **The sweep runs one copy of essentially the same query per cleaner, and then N more queries per cleaner.** The ADR adds its most expensive condition to the innermost of these loops without pricing the loop. This is the largest cost in the design and the ADR does not mention it.

**The hole.** `NewJobsDigestService.cs:86` loops over **every** approved/active cleaner. Inside the loop,
per cleaner:

| line | cost | per sweep |
|---|---|---|
| `:120` | the candidate query (probe A: 1 join + 1 correlated `count(*)` + 1 `NOT EXISTS` + 1 top-N subquery, all per candidate row) | **× C** |
| `:135-143` | `HasOverlappingOrderAsync` **per order** | **× Σ N_c** |
| `:155-157` | preferences lookup, one row at a time, **tracked** | **× C** |
| `:216` | `employeeRepository.GetByIdAsync` — a fresh 4-table tracked load | **× C_notified** |
| `:219` | `CommitAsync` — a separate transaction | **× C_notified** |

= `1 + C·(2) + Σ N_c + 2·C_notified` round trips per sweep, 48 sweeps/day.
At C = 200 cleaners and N = 20 fresh orders that is ≈ 4 800 round trips per sweep, ≈ 230 000/day, for a
notification feature.

And the *only* per-cleaner inputs to the candidate query (probe A) are `@country`, `@emp` and `@since`.
**Every cleaner in the same `WorkCountryId` re-scans the same `Orders` rows.** Cleansia is launching in
one country (`Address.State` note in CLAUDE.md; `WorkCountryId` per `Employee`), so today that is
*every cleaner running the identical scan, C times*.

**What I want changed** — and this is the cheaper-by-design alternative, not a micro-tweak:

1. **Group the loop by `WorkCountryId`.** Fetch the country's candidate set **once per country**,
   projecting the fields the decision needs:
   `(Id, CleaningDateTime, EstimatedTime, PreferredEmployeeId, PreferredHoldUntilUtc, LatestTrackCreatedOn, AssignedEmployeeIds)`.
   `C` queries → `K` queries where `K` = distinct work countries (1 today). Every per-cleaner test —
   watermark freshness, `AssignedEmployees.All(...)`, and **the entire hold predicate** — is then an
   in-memory comparison on already-fetched columns. This is the shape that makes the ADR's addition
   genuinely free, and it is the answer to the ADR's `:936` worry.
2. **Batch the preferences read**: one `WHERE UserId = ANY(@ids)` before the loop instead of
   `:155-157` per cleaner, `.AsNoTracking()`.
3. See CH-Q7 for the overlap loop and CH-Q8 for the watermark.

If the panel will not take (1), then at minimum the ADR must **name** the loop and state that the hold
term multiplies by C, rather than describing D5.3 as "one expression change" (`ADR:342`).

**Unbounded-case warning that is specific to this ADR.** `:90` reads
`sinceUtc = (cleaner.LastDigestAt ?? DateTimeOffset.MinValue).UtcDateTime`. For a **never-notified**
cleaner — a new hire, and *every* cleaner on the first sweep after any deploy that resets nothing — the
watermark is `0001-01-01`, so the freshness clause matches **every available order in the country,
ever**, and `N_c` in the overlap loop is the entire backlog, not "orders from the last 30 minutes". The
code comment at `:118-119` (*"keeps the per-cleaner page tiny"*) and at `:131-133` (*"bounded by how
many new orders matched the country filter for THIS cleaner"*) are **assertions that are false in
exactly this case**. D5.3's OR-branch adds rows to that same set.

---

### CH-Q7 — `HasOverlappingOrderAsync` is not a cheap point lookup: each call scans the cleaner's **entire assignment history** and evaluates a `text`→`interval` cast per row. N of these per cleaner per sweep.

**The hole.** Probe **C** — the real emitted SQL for `OrderRepository.cs:272-292`:

```sql
SELECT o."Id" FROM "Orders" AS o
WHERE (@ef_filter__p4 OR (@ef_filter__p2 AND o."TenantId" IS NULL) OR o."TenantId" IS NULL)
  AND EXISTS (SELECT 1 FROM "OrderEmployees" AS o0 WHERE o."Id" = o0."OrderId" AND o0."EmployeeId" = @emp)
  AND o."CleaningDateTime" < @newEnd
  AND o."CleaningDateTime" + CAST(o."EstimatedTime"::double precision::text || ' mins' AS interval) > @newStart
  AND ((o."CurrentStatus" IS NOT NULL AND o."CurrentStatus" = ANY (@blocking))
    OR (o."CurrentStatus" IS NULL AND EXISTS (
        SELECT 1 FROM (SELECT o1."Status" FROM "OrderStatusHistory" AS o1
                       WHERE ... AND o."Id" = o1."OrderId"
                       ORDER BY o1."CreatedOn" DESC, o1."Sequence" DESC LIMIT 1) AS o2
        WHERE o2."Status" = ANY (@blocking0))))
```

Four things, all evidenced by that text:

1. **There is no lower bound on `CleaningDateTime`.** The only bounds are `< @newEnd` and the computed
   upper. So the driving `EXISTS` on `OrderEmployees(EmployeeId)` walks **every order this cleaner has
   ever been assigned to**, and each of those rows is then filtered. A cleaner with 500 lifetime jobs
   pays 500 row visits — **on every one of the N calls**, N times per sweep.
2. `o."CleaningDateTime" + CAST(o."EstimatedTime"::double precision::text || ' mins' AS interval)` —
   a **double → text → interval parse, per row**. Not merely non-sargable; genuinely expensive as
   scalar work.
3. The `OR (CurrentStatus IS NULL AND <top-N subquery>)` branch prevents
   `IX_Orders_CurrentStatus_CleaningDateTime` (`OrderEntityConfiguration.cs:111`) from driving, and adds
   a per-row top-N sort for any pre-backfill row.
4. **The tenant filter is present.** `HasOverlappingOrderAsync` uses `GetDbSet()`
   (`OrderRepository.cs:281`) = tenant-scoped (`BaseRepository.cs:148-158`), inside a sweep that reads
   cleaners with `GetQueryableIgnoringTenant()` (`NewJobsDigestService.cs:63`) and **never sets a tenant
   override** — contrast `MaterializeRecurringBookings.cs:70-74`, which does. With
   `@ef_filter__p4=False, @ef_filter__p2=True` the filter reduces to `o."TenantId" IS NULL`. **Latent,
   not live** — TenantId is null everywhere in single-tenant mode — but the day a tenant is created,
   the overlap filter silently returns `false` for every tenanted cleaner and the digest starts pushing
   double-booked jobs. The existing test cannot see this: `ColdPathCurrentStatusQueryTests.cs:53` wires
   `new FixedTenantProvider(tenantId: null)`.

**What I want changed.** The overlap loop is hoisted out of the per-order loop entirely. `newOrders` at
`:120-122` already carries `CleaningDateTime` and `EstimatedTime`, so the sweep knows the exact time
window before the loop starts. Fetch the cleaner's live commitments **once**, bounded to
`[min(CleaningDateTime) − maxEstimated, max(CleaningDateTime) + maxEstimated]`, and do interval overlap
in memory. `N` queries → `1` (and, with CH-Q6's grouping, one query for all cleaners joined through
`OrderEmployees`). Separately: `HasOverlappingOrderAsync` needs an ignoring-tenant sibling for
background callers, or the sweep needs the per-iteration override the materializer already uses. That
second point is a **pre-existing defect**, filed independently of this ADR — but it must be filed,
because D5.3 is being bolted onto a sweep that has it.

---

### CH-Q8 — `StampWatermarkAsync` re-loads a 4-table tracked entity graph and commits **per cleaner**, and it is tenant-scoped in a tenant-ignoring sweep. D5.3 makes the watermark load-bearing for hold correctness, so its write path stops being incidental.

**The hole.** `NewJobsDigestService.cs:211-220` calls `employeeRepository.GetByIdAsync(employeeId)`.
That override is `EmployeeRepository.cs:44-51`:

```csharp
return GetDbSet()
    .Include(e => e.User)
    .Include(e => e.Address)
        .ThenInclude(a => a.Country)
    .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
```

Per cleaner, per sweep, in order to flip one `DateTimeOffset?` (`Employee.cs:43-45`):

1. **Over-fetch.** A 4-table join (`Employees` × `Users` × `Addresses` × `Countries`), tracked, when the
   sweep already read every employee it needs at `:62-74`. `BaseRepository.cs:35-38` confirms
   `GetByIdAsync` is `FirstOrDefaultAsync`, not `FindAsync` — there is **no identity-map short-circuit**;
   it is a real round trip every time.
2. **O(C²) change detection.** One `CommitAsync` per cleaner (`:219`) inside a single scoped
   `DbContext`, while the tracker accumulates every previously-loaded `Employee` graph *and* every
   tracked `UserNotificationPreferences` from `:155-157`. Commit #200 runs `DetectChanges` over ~800
   tracked entities to write one column. That is quadratic work in the number of cleaners, for zero
   benefit.
3. **Latent tenancy no-op — and this one is fatal to D5.3.** `GetDbSet()` is tenant-scoped. For any
   cleaner with a non-null `TenantId` (selected by `GetQueryableIgnoringTenant()` at `:63`, with no
   override set), `GetByIdAsync` returns **null**, and `:217` returns early — **silently, no log, no
   throw** — *after* `notificationProducer.NotifyAsync` at `:168` has already enqueued the push. The
   watermark can then **never advance**, so that cleaner is re-notified about the same orders every 30
   minutes forever, and the freshness query runs with `sinceUtc` frozen (worst case
   `DateTimeOffset.MinValue`, `:90` — see CH-Q6). Latent today (single-tenant, TenantId null);
   guaranteed the moment multi-tenancy is switched on.

**Why it matters to *this* ADR.** D5.3's entire correctness argument is *"the watermark advances past
the hold expiry exactly once"*. A watermark that cannot advance turns the hold from a 24-minute
optimisation into a permanent duplicate-push loop.

**What I want changed.** `StampWatermarkAsync` uses the **already-present**
`EmployeeRepository.GetByIdIgnoringTenantAsync` (`:53-57`) — no `Include`s, no tenant filter — a
one-line change that removes the over-fetch **and** the tenancy trap while preserving the
watermark/outbox atomicity the comment at `:179-181` depends on. (Do **not** reach for
`ExecuteUpdateAsync` here: it commits outside the change tracker and would break that atomicity
guarantee.) Additionally: hoist the commit out of the loop, or `Clear()` the tracker per iteration.
This is a pre-existing defect; I am requiring it as a **precondition of T-0515**, not as part of it,
because D5.3 is unimplementable-as-specified on top of a watermark that can silently refuse to move.

---

### CH-Q9 — On the hold's **own** query cost the ADR's conclusion is roughly right and its reasoning is wrong. **No index is needed at any of the five surfaces, and the partial index the ADR floats would be actively wrong.** Say so as a decision, not as an open question.

**The hole.** `ADR:931-935`: *"Not verified as index-servable... Whether `PreferredHoldUntilUtc` needs an
index (probably not — it is null for the overwhelming majority of rows, which argues for a *partial*
index if anything) is unanswered."*

The reasoning is inverted. The predicate is
`hold IS NULL OR hold <= @now OR PreferredEmployeeId = @emp` (`ADR:268-270`). Its **satisfying set is
NULL-dominant** — i.e. it matches ~100% of rows. A partial index `WHERE PreferredHoldUntilUtc IS NOT
NULL` indexes exactly the rows the predicate mostly *excludes*; it cannot serve this predicate at all,
and Postgres would have to BitmapOr it with a full scan for the `IS NULL` branch — i.e. the seq scan
you were trying to avoid, plus an index maintenance cost on every `INSERT INTO Orders`. The only reader
a partial index would ever have is an *"admin view of live holds"*, which `ADR:533` explicitly does not
build.

The correct answer, which the emitted SQL shows: the term is a **residual filter** evaluated after a
selective index has already narrowed the set. Probe **F** — `CreateAvailableOrdersSpec` +
`NotHeldFromEmployee`:

```sql
WHERE <tenant filter>
  AND o."CurrentStatus" IS NOT NULL AND o."CurrentStatus" = ANY (@OrderStatuses)   -- IX_Orders_CurrentStatus_CleaningDateTime
  AND (SELECT count(*)::int FROM "OrderEmployees" AS o0 WHERE o."Id" = o0."OrderId") < o."MaxEmployees"
  AND NOT EXISTS (SELECT 1 FROM "OrderEmployees" AS o1 WHERE o."Id" = o1."OrderId" AND o1."EmployeeId" = @ExcludeEmployeeId)
  AND (o."PreferredHoldUntilUtc" IS NULL OR o."PreferredHoldUntilUtc" <= @now OR o."PreferredEmployeeId" = @emp)
ORDER BY o."TotalPrice" DESC LIMIT @p
```

The hold term is the last conjunct, a flat three-way column test, **no cast, no subquery**. It is
dominated by three orders of magnitude by the pre-existing correlated `count(*)` on the line above it
(`OrderSpecification.cs:126`). Surface-by-surface:

| # | surface | added cost | verdict |
|---|---|---|---|
| 1 | `GetPagedOrders` → `OrderSpecification` | one residual conjunct on both the count query (`GetPagedOrders.cs:95`) and the page query | free |
| 2 | `GetAvailableJobsPreview` (same spec) | same, ×2 (count `:51` + page `:52`) | free |
| 3 | `OrderAccessService.CanBrowseOrderAsync` | **zero** — `:85` already tests `order.HasAvailableSpots` in memory on a loaded entity | free |
| 4 | `NewJobsDigestService` | see CH-Q1/Q2/Q5/Q6 | **the whole cost lives here** |
| 5 | `TakeOrder.Validator` | see below | free **if** placed correctly |

Sargability alongside `Orders.CurrentStatus`: **yes, and it holds.** `IX_Orders_CurrentStatus_
CleaningDateTime` (`OrderEntityConfiguration.cs:111`) still drives — probe F shows the status predicate
untouched at the front of the WHERE. And it stays selective long-term: `Pending`/`Confirmed` are
transient buckets while `Completed` grows without bound, so the leading-column selectivity for the
board query *improves* over time. I checked this specifically because a status-leading index is often
the thing an added OR ruins here; it is not.

**Surface 5 is the one place a naive implementation costs a round trip.** D5.2 requires
`OrderNotFound` (`ADR:322`), but `TakeOrder.cs:44-45` maps `HasAvailableSpotsAsync` to
`NoAvailableSpots`, so the hold check cannot go there. Adding a *third* `MustAsync` would add a fourth
`Orders` round trip to a validator that already does `ExistsAsync` (`:42`, one query) then
`HasAvailableSpotsAsync` (`:63-70`, a second full load of the same row) then re-loads the order **again**
in `NotHaveTimeConflictAsync` (`:150-155`). Fold the hold predicate into the **`ExistsAsync` rule's**
query — the row is already being fetched, the outcome is already `OrderNotFound`, and D5.2's
"read and write must agree" is satisfied by construction with **zero** added round trips.

**What I want changed.** The ADR states as a **decision**: *"`PreferredHoldUntilUtc` gets **no index**.
The term is a residual filter behind `IX_Orders_CurrentStatus_CleaningDateTime`. A partial index on
this column is a hard reject — it indexes the complement of the predicate's satisfying set and has no
reader."* And D5.2 names the placement: **inside the `ExistsAsync` rule**, not a new rule. Both remove
an open question from `:931-935`.

---

### CH-Q10 — Two performance claims in the ADR and one in the code are **asserted, not measured**, and one of them is refutable by reading.

1. **`ADR:65-75` Fact A — "'may this cleaner see/take this order' is already expressed in FIVE
   independent places" and D5's "one shared expression" makes them converge. REFUTED as stated:
   two of the five already disagree on the *first* term.** `NewJobsDigestService.cs:52-53` uses
   `{ New, Pending, Confirmed }` under a comment at `:49-50` claiming it *"Mirrors
   `DashboardSpecifications.CreateAvailableOrdersSpec`"*; `DashboardSpecifications.cs:24` uses
   `{ Pending, Confirmed }`. **`New` is in one and not the other, and the comment asserting they match
   is false.** So the ADR's consequence *"the change makes that sprawl more reviewable than it is
   today"* (`ADR:569-570`) is unearned: a sixth shared term is being added to surfaces that already
   silently disagree on term #1, and §verify #2's grep-the-hits check (`ADR:605-608`) counts *presence*,
   not *agreement*. I am not asking the ADR to fix the status-set divergence (that is a separate
   ticket) — I am asking it to **stop claiming convergence it does not deliver**, and to make §verify #2
   a diff of the five predicates rather than a hit count.
2. **`ADR:118-119` `PreferredHoldUntilUtc` as `DateTime?` vs `DateTimeOffset` everywhere it is
   compared** — I flagged this expecting a cross-type failure and it **does not** materialise:
   migration `20260723182623_Initial.cs:1001,1040` shows `DateTime` columns on `Orders` already map to
   `timestamp with time zone`, matching `Auditable.CreatedOn`. The SQLite `DateTimeOffset` converter at
   `CleansiaDbContext.cs:168-173` is provider-guarded and a no-op on Npgsql. **Checked, holds** — with
   the one caveat from CH-Q1 that the `::timestamptz` cast *does* appear if you compare the two columns
   to each other (the `max` form), and does not appear if each is compared to its own parameter (the
   disjunctive form). One more reason for the disjunction.
3. **`NewJobsDigestService.cs:118-119` and `:131-133`** — *"keeps the per-cleaner page tiny"* and
   *"bounded by how many new orders matched the country filter for THIS cleaner"*. Both are **unmeasured
   assertions in the code**, and both are **false for a never-notified cleaner** (`:90`, watermark
   `MinValue`). Not the ADR's fault, but the ADR is building on them.
4. **`ADR:869-875` CH-10's concession** (`0.10` and `12h` are reasoned, not calibrated) is honest and I
   have nothing to add from the cost side: the numbers do not change any query's shape, only how many
   rows carry a non-null `PreferredHoldUntilUtc` — and since CH-Q9 establishes the term is a residual
   filter, the row count is irrelevant to query cost at any fraction. **The fraction is a product risk,
   not a performance risk.** I am explicitly *not* supporting a blocking amendment on CH-10 from this
   lane.

---

## VERDICT ON YOUR PRE-EXISTING-BUG CHARACTERISATION

> *Your claim: "if at least one order is notifiable, the digest is sent and `StampWatermarkAsync`
> advances the watermark past all of them — so the overlapping ones are permanently stale and never
> pushed again even after the conflict clears. The bug is real but **narrower** than 'any skipped order
> is lost': it needs at least one other notifiable order in the same sweep."*

**You are RIGHT on the mechanism, and I am confirming it line by line. You are WRONG — or at least
badly misled — by the word "narrower", and I want you to drop it.** Three points.

**1. The mechanism: CONFIRMED, exactly as you state it.**
- `:134-143` computes `takeable` as the count of non-overlapping new orders.
- `:145-149`: `takeable == 0` → `totalSkippedNoNewJobs++; continue;` — **no `StampWatermarkAsync` call
  on this path.** Your first half is correct: when *every* new order overlaps, nothing is burned.
- `takeable >= 1` → falls through to `:168` (notify) and `:182` (stamp). `MarkNewJobsDigestSent`
  (`Employee.cs:43-45`) sets `LastNewJobsDigestAt = sweepStartedAt`, which becomes `sinceUtc` at `:90`
  next sweep, which gates the *only* freshness test at `:109-114`. An order whose latest status track
  predates that stamp can never satisfy `s.CreatedOn > sinceUtc` again. **The overlapping orders are
  permanently un-notifiable for that cleaner.** Confirmed.
- Your gating condition is correct: the burn requires `takeable >= 1`.

**2. "Narrower" inverts the probability. The guard condition is the COMMON case, not the rare one.**
`takeable == 0` means **every single** new order in the sweep overlaps a live commitment of this
cleaner. That requires the cleaner to be simultaneously busy at the cleaning time of *all* of them.
`takeable >= 1` — your loss condition — is what happens **whenever the cleaner is free for even one of
the new jobs**, which is the normal state of a cleaner in a marketplace with any order flow. So: the
bug is **narrow in logic and broad in incidence.** Calling it "narrower" reads as "rarer", and it is
not rarer; it is close to always. I would state it as: *"a skipped order is lost as soon as the cleaner
is notified about anything else — which is the ordinary case."*

**3. The `takeable == 0` no-stamp branch is not a mitigation, it is a deferral — and there is a third
loss path you did not name.**
- The no-stamp branch does not save those orders; it defers the burn to the first sweep where the
  cleaner is free for anything. In a 30-minute cadence that is typically **the next sweep**. The orders
  are then all burned together.
- **`:124-128` is a third `continue` without a stamp** (`newOrders.Count == 0`). Benign for the burn,
  but it is *why* a never-notified cleaner keeps `sinceUtc = DateTimeOffset.MinValue` (`:90`) — which is
  the unbounded-scan case in CH-Q6, and it is the case where the loss is largest when it finally fires.
- **`:158-166` is a fourth stamp** — the muted branch stamps unconditionally after `takeable >= 1`,
  with an explicit comment justifying it. Same loss, same gate. Your characterisation covers it, but
  it is worth naming in the ticket because it is the one path where the loss is *deliberate* for a
  different reason and someone will "fix" it by accident.

**Cost of the per-order `HasOverlappingOrderAsync` loop — you asked me to price it.**
It is **N queries per cleaner per sweep** as you say, but the per-query cost is not O(1): see CH-Q7 —
each call scans the cleaner's **entire lifetime assignment history** (no `CleaningDateTime` lower
bound) and evaluates `CleaningDateTime + CAST(EstimatedTime::double precision::text || ' mins' AS
interval)` per row. So the true unit is **Σ_cleaners (N_c × H_c)** row visits per sweep, where H is the
cleaner's lifetime order count — a product that grows in *both* factors as the platform grows, 48 times
a day, forever. The code's own comment at `:131-133` prices only the outer loop and calls it "bounded".
It is not bounded in the factor that grows.

**Root cause, which is the thing worth writing down once.** `LastNewJobsDigestAt` is a **single
per-cleaner scalar** that assumes "eligible to you" is (a) monotone in time and (b) derivable from a
**global** timestamp on the order. Both assumptions fail for any **per-cleaner, non-monotone**
eligibility rule. The overlap filter is the first such rule (it can flip back to eligible when a
conflict clears). **The hold is the second** — and D5.3 patches the hold by pushing a per-cleaner notion
of "arrival instant" into a global comparison, which is why CH-Q2 exists. The ADR notices the family
resemblance at `ADR:821-828` (CH-4's defense: *"the overlap filter at `:137-142` has the same latent
shape"*) and then files it as *"an observation, not a blocker"*. I disagree with the triage: it is the
**same defect**, and D5.3 is the second patch on it. Filing the overlap variant as a separate ticket is
fine; **claiming that D5.3 resolves the class is not**, and the ADR should say plainly that the
watermark design has a known structural limit and that D5.3 is a point fix within it.

---

### What I checked and found sound

Silence is not assent, so here is what I read and could not break.

- **The ADR's Fact B is real, and I reproduced the reasoning independently.** `:109-114` +
  `Employee.cs:43-45` + `:182` are exactly as described; a held order's status track does go stale
  against the watermark. **D5.3 is necessary.** My attack is on its *formulation*, not its existence.
- **The three-way visibility predicate translates exactly as written**, including the NULL handling EF
  adds for free: probe F emits `o."PreferredEmployeeId" = @emp` (correctly excludes NULL) and probe E
  emits `(o."PreferredEmployeeId" <> @emp OR o."PreferredEmployeeId" IS NULL)` for the negation. No
  three-valued-logic trap. Checked because this is where an `IS NULL`-blind predicate usually leaks.
- **No EF query-cache-key explosion from `NotHeldFromEmployee(employeeId, nowUtc)` returning a freshly
  built `Expression`.** The captured `employeeId`/`nowUtc` become SQL parameters (`@emp`, `@now` in
  probes E and F), not inlined constants, so the compiled-query cache key is stable across calls. I
  checked this specifically — a factory method returning an expression tree is a classic way to blow
  EF's cache, and this one does not.
- **`DirectSpecification` composes an `Expression<Func<Order,bool>>` directly**
  (`DirectSpecification.cs:5-14`, `AndSpecification.cs:13-18`), so D5's "one expression, ANDed into
  `OrderSpecification`" is mechanically possible with no expression rewriting. `OrderSpecification.
  Create` (`:144-172`) uses all-optional parameters, so two new trailing parameters are
  source-compatible across all 12 call sites. Checked because CH-3's defense hand-waves this.
- **Surface 3 costs nothing.** `CanBrowseOrderAsync` (`OrderAccessService.cs:68-86`) already evaluates
  `order.HasAvailableSpots` in memory on a materialised entity, so the hold check is three field reads.
  *One caveat:* `ADR:818-820` says it *"compiles or mirrors"* the expression — **do not `.Compile()` per
  call** (expression compilation is ~10–100 µs and allocates, on a per-request path). Require either a
  `static readonly` delegate compiled once or a plain method on the entity.
- **`Orders.CurrentStatus` is genuinely index-served and stays that way.**
  `OrderEntityConfiguration.cs:102-111` + `IX_Orders_CurrentStatus_CleaningDateTime` in the migration;
  probe F confirms the status predicate survives untouched at the front of the WHERE with the hold term
  appended as a residual. The ADR's "sargable alongside `CurrentStatus`" premise **holds**.
- **`CancellationToken` propagation is clean** through the entire digest path — `:74, :122, :141, :157,
  :177, :182, :216, :219`. No `.Result`, no `.Wait()`, no `async void`, no `Task.Run`. Nothing for me
  here.
- **The candidate query already projects rather than materialising entities** (`:120-122`,
  `Select(new { Id, CleaningDateTime, EstimatedTime })`) — a projection to an anonymous type is
  no-tracking by construction. The author did the right thing there; my CH-Q6 is about the *number* of
  times it runs, not its shape. (The preferences read at `:155-157` **is** tracked and should be
  `AsNoTracking()` + batched — minor, folded into CH-Q6.)
- **`GetAvailableJobsPreview` really does inherit the predicate via the specification**
  (`:50` → `DashboardSpecifications.CreateAvailableOrdersSpec` → `OrderSpecification`), and it flows
  into *both* the count (`:51`) and the page (`:52`). §verify #2's claim that surface 2 comes free is
  **correct**.
- **D4's "the targeted push must not stamp the watermark"** (`ADR:225-230`) is right and matters more
  than the ADR says: given CH-Q8, a stamp on the create path would be a second writer to a field whose
  single writer is already fragile.
- **`ADR:928-936`'s honesty about not having measured anything is accurate**, and I am not treating the
  absence of `EXPLAIN` as a finding in itself — I closed the gap where I could (emitted SQL) and have
  labelled everything I could not (plan choice, row counts) as reasoning rather than measurement.

**Blocking from this lane: CH-Q2** (correctness — the fix reintroduces the bug and adds a false push
count) **and CH-Q3** (its test cannot detect it). **CH-Q1 and CH-Q5** are a required rewording of D5.3
and §verify #6 that turns a regression into an improvement. **CH-Q6/Q7/Q8** are pre-existing defects
that I am requiring be fixed or filed *before* T-0515 lands, because D5.3 is being built on top of them.
**CH-Q9** asks the ADR to close an open question with a decision. **CH-Q10.1** asks it to withdraw an
unearned convergence claim.
