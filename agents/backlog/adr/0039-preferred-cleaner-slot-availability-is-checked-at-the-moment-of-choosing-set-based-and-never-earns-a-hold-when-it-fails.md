# ADR-0039 — **Never offer a cleaner the customer cannot actually have**: the preferred-cleaner picker asks *"is this person free for the slot I just chose?"* as **one set-based, window-bounded question over the customer's own serving-cleaner set** — never N per-candidate questions — the **same repository call** answers it again in the hold resolver so the picker and the grant can never disagree, a cleaner known to be busy gets **no hold and no targeted push**, and the customer sees **one greyed, unselectable row with a neutral line that names no reason and promises no other time**; this **partially supersedes ADR-0036 D5.1 / A6** on owner instruction, while A6's **weekly-cap** half stands on evidence

- **Status:** `proposed` — drafted 2026-08-03 by the `architect` (author mode) on direct owner
  instruction. **Not yet challenged. Not binding until a lead declares consensus** per
  `agents/process/deliberation.md`. The **owner ruling it carries is binding immediately** and is not
  what the panel is being asked to review; what the panel reviews is *the mechanism that executes it*.
- **Date:** 2026-08-03 (drafted)
- **Partially supersedes:** **ADR-0036 D5.1** (the "Deliberately NOT checked (dynamic)" paragraph, in
  its **time-conflict** half only) and **ADR-0036 A6** (same half). ADR-0036 carries a dated
  owner-instruction section recording this; **its original reasoning is preserved verbatim, not
  deleted** — it was correct given its premise, and the premise is what the owner changed.
  **Everything else in ADR-0036 stands**: D1–D4, D5.0, D5.2–D5.5, D6–D11, Invariant H, and the whole
  hold mechanism are untouched by this ADR.
- **Composes with:** **ADR-0037** (order offerability — a *different* conjunct answering a *different*
  question: 0037 asks "is this order live work?", this ADR asks "can this person work it at this
  hour?"). **ADR-0028** (multi-tenant activation) — §D6 hands that lane a defect and a shape.
  **ADR-0017** (region seam) — the scan floor is a `BookingPolicy` platform number, not a country
  branch. **Does not touch** ADR-0001, ADR-0007, the fiscal path, the pay formula or `EmployeePayConfig`.
- **Applies to:** `Cleansia.Core.Domain` (one pure duration function, one `BookingPolicy` constant, no
  schema change, **no migration**) · `Cleansia.Infra.Database` (**one new repository method + its
  tenant-ignoring sibling**; `HasOverlappingOrderAsync` converges onto it) ·
  `Cleansia.Core.AppServices` (`GetMyServingCleaners` gains three optional request fields and one
  nullable response field; `IPreferredCleanerHoldResolver` gains one check and one decline reason) ·
  `cleansia_android` customer-app + `cleansia_ios` CleansiaCustomer (**the picker only**) ·
  ⚠️ **`nswag-regen` (owner-only)** — the picker DTO changes shape · **no host coupling**: the picker
  is reached from `Web.Customer` and `Web.Mobile.Customer` only; no partner or admin surface changes.
- **Ticket:** none yet — **§D10 enumerates the tickets the PM should file.** This ADR builds nothing.
- **Owner input this ADR executes (verbatim, 2026-08-03):**
  > *"there is a need to mark somehow if this cleaner has order assigned to him already or not on this
  > date and time, if yes then mark that this cleaner isn't available for that date and time"*

> ## AC1 — the ruling, in one sentence a test can check
>
> **At the moment the customer opens the preferred-cleaner picker for a slot they have already chosen,
> every cleaner who holds a live-commitment assignment overlapping that slot is rendered greyed and
> unselectable; and if one is somehow selected anyway, the order is still created, the preference is
> still stored, and NO hold and NO targeted push are produced for them.**

> ## AC2 — the property that makes it honest
>
> **The picker's answer and the hold resolver's answer come out of the same repository call with the
> same window.** Not "the same rule" — the *same method*. If the picker can say *available* and the
> resolver can then say *busy* for a reason of its own, the feature has already failed, and no amount
> of shared documentation prevents it.

---

## Context

### What the owner overturned, and why the original reasoning was not wrong

ADR-0036 D5.1 lists the cleaner's **time conflict** under *"Deliberately NOT checked (dynamic) — the
hold is created and simply expires"*, and A6 rejects the check outright:

> *"Both are genuinely dynamic (`TakeOrder.cs:125-161`): the limit resets weekly and the conflict
> depends on orders taken after creation. A creation-time check is wrong in both directions… Cost of
> being wrong is capped at 10% of the fill window (Invariant H), which is cheaper than a check that is
> confidently wrong."*

That argument is internally sound and its premise is stated plainly: **a hold is cheap, and a busy
cleaner simply declines.** The owner's requirement replaces the premise. It is not "check the conflict
because holds are expensive" — it is **"never present a choice we cannot honour."** The cost ADR-0036
priced was *latency*; the cost the owner is pricing is *a promise made to a customer's face*. Those are
different currencies and the second one is not bounded by Invariant H, because Invariant H bounds what
the marketplace loses, not what the customer was told.

**Both directions of A6's "wrong in both directions" survive this ADR, and only one of them was ever
avoidable:**

| Direction | ADR-0036's fear | This ADR |
|---|---|---|
| Free at creation → **busy** when they open the app | the hold expires unused | **Unchanged and accepted.** Not knowable at creation; bounded by Invariant H exactly as ADR-0036 says. |
| **Busy** at creation → free when they open the app | we suppressed a hold that would have worked | **Accepted, deliberately.** The window we withhold on is the *customer's own appointment slot*; a cleaner becomes free for it only by a cancellation. Rare, and cheap: no hold means the order goes to the open board **immediately**, which is the better failure. |
| **Busy at creation, still busy at take** | *(not considered)* | **This is the case the owner is closing.** ADR-0036 spent up to 12 h of first-seat exclusivity on a cleaner `TakeOrder.cs:59` was always going to refuse. **Pure delay with a zero success probability** — the exact failure Invariant H exists to bound, arriving in the one form we could have predicted and chose not to. |

### A6's other half is NOT overturned, and the reason is in two method signatures, not in taste

ADR-0036 A6 bundles the **weekly order limit** with the time conflict. The owner asked about *"an order
assigned to him on this date and time"*. Only one of the two is that.

| | `HasOverlappingOrderAsync` (`OrderRepository.cs:272-292`) | `GetEmployeeOrderCountThisWeekAsync` (`:247-258`) |
|---|---|---|
| Parameterised by | the **booking's** instant (`cleaningDateTime`, `estimatedTimeMinutes`) | **`DateTime.UtcNow.Date`** (`:249-252`) |
| At creation, for a booking 10 days out, it answers | *"is this cleaner busy at the customer's appointment?"* — **the question asked** | *"how many jobs does this cleaner have in the week we are in right now?"* — **a week that does not contain the booking** |

**A creation-time weekly-cap check is not a wrong answer; it is an answer to a different question.**
A6's weekly-cap half therefore stands, unchanged, on evidence rather than on judgement. Anyone who
later "completes the job" by adding the cap check to the resolver is reintroducing a defect this
section exists to prevent.

### The tool that already exists, and its two verified defects

`OrderRepository.HasOverlappingOrderAsync(employeeId, cleaningDateTime, estimatedTimeMinutes, ct)`
(`:272-292`) already answers the singular form of the owner's question, with the right status set
(`SlotBlockingStatuses`, `:263-270`) and the right NULL-`CurrentStatus` fail-closed fallback
(`:285-290`, the sanctioned exception in `patterns-backend.md` B7). It has two defects, **both
re-verified by reading for this ADR**:

**Defect 1 — it is tenant-scoped while one of its two callers is tenant-ignoring.**
`GetDbSet()` is `Context.Set<TEntity>()` (`BaseRepository.cs:158`), so the global filter applies
(`CleansiaDbContext.ApplyTenantQueryFilters:201-269`: `providerNull || (currentTenantId == null &&
e.TenantId == null) || e.TenantId == currentTenantId`). `NewJobsDigestService` selects its cleaners
and its orders with `GetQueryableIgnoringTenant()` (`:63`, `:98`) because the timer has no tenant
claim — and then calls `HasOverlappingOrderAsync` at `:137`. Under a tenant, `currentTenantId` is
`null` and the rows are not, so **every branch is false, the query returns no rows, and every cleaner
reports as free**. The digest would start advertising double-booked jobs. **The same method is
`TakeOrder`'s time-conflict write gate** (`TakeOrder.cs:59`), where the tenant-scoped read is
*correct* — the caller is a request path with a claim.

> **The defect is not "it is tenant-scoped." It is that ONE method serves TWO callers with OPPOSITE
> tenancy requirements and silently picks one of them.** That is the shape, and it generalizes.

**T-0529 found this, fixed its own instance, walked the sweep (AC5), confirmed this one, and asked the
PM to file it** (`T-0529…md:180-184`: *"`HasOverlappingOrderAsync` needs its own ticket — it does not
have one"*). It still does not have one. T-0401 is `done` and only ever touched the status set.
**§D10 files it.**

**Defect 2 — the scan has no lower bound and evaluates an interval cast per row.** The predicate
(`:282-284`) is:

```
o.AssignedEmployees.Any(e => e.EmployeeId == employeeId)
&& o.CleaningDateTime < newEnd
&& o.CleaningDateTime.AddMinutes(o.EstimatedTime) > newStart     -- computed per row, not sargable
```

The second term is the only range bound and it is an **upper** one. For a booking three days out the
scan is *everything the cleaner was ever assigned to up to that instant*, with the interval
computation evaluated per row. `SlotBlockingStatuses` prunes terminal rows — but the platform demonstrably
carries stale non-terminal rows: T-0401's own source is *"the test account's stale in-progress order
(whose completion is photo-gated and can't be finished)"*.

**Calling that once per candidate cleaner as the customer opens the picker is N of those per render.**
The picker's set is `Take(20)` (`GetMyServingCleaners.cs:50`). The naive implementation is therefore
**up to 20 unbounded history scans on the customer's booking hot path**, and — under a tenant — **20
scans that all return `false`.** Both halves are why this is an ADR and not a ticket.

---

## Decision

### D1 — The ruling: **the picker never offers what the platform cannot give**

At the moment of choosing, a cleaner who holds a live-commitment assignment overlapping the slot the
customer has chosen is **shown, marked unavailable, and not selectable**.

**Shown, not hidden.** Hiding is the cheaper implementation and the worse product: the customer's
favourite silently disappears from a list they have seen before, and the platform has manufactured a
mystery to avoid writing a sentence. The owner's word is *"mark"*.

**Marked, not merely disabled.** ADR-0036's whole copy debt came from surfaces that changed behaviour
without changing text. A greyed row with no line is that failure again.

**Unselectable, not selectable-with-a-warning.** If it can still be selected, D2's ruling has to be
enforced twice (client and server) and the customer has been allowed to make a choice we have already
told them we cannot honour. One rule, one place.

### D2 — A cleaner we **already know** is busy gets **no hold** — and **no targeted push either**

This is the consequence the owner's requirement forces, and it is ruled here rather than left to the
implementer.

`IPreferredCleanerHoldResolver` (ADR-0036 D5.1) gains **one check and one reason**:

```csharp
public enum HoldDeclineReason
{
    None = 0, NoPreference = 1, NoMembership = 2, ShortLeadTime = 3,
    CleanerNotApproved = 4, CleanerCountryMismatch = 5, CleanerMutedNewJobs = 6,
    CleanerNotFound = 7, CleanerUnreachableForPush = 8,
    /// ADR-0039. The cleaner already holds a live-commitment assignment overlapping
    /// [cleaningUtc, cleaningUtc + estimatedMinutes). Notify: NO. Hold: NO.
    CleanerBusyAtCleaningTime = 9,
}
```

**Its row in ADR-0036 D4.1's outcome table — and it sits with the "no signal" cases, not with
`ShortLeadTime`:**

| Resolver outcome | Notify? | Hold? |
|---|---|---|
| `CleanerBusyAtCleaningTime` (**new**) | **no** | **no** |

**Why no hold.** A hold is first refusal on the first seat. `TakeOrder.cs:59` → `:145-161` refuses this
cleaner that seat for the whole window. The hold is therefore **100% of the first seat's fill window
spent on an outcome with probability zero** — not the bounded-risk trade Invariant H prices, but its
degenerate case. *Invariant H bounds what we cannot know; it is not a licence to spend what we can.*

**Why no push either — and this is ADR-0036's own rule, adopted, not a new one.** D5.1 says verbatim:
*"a hold for a cleaner `TakeOrder.cs:53` would reject is pure latency — **and so is a push**."* That
sentence was written about `ContractStatus`; it is true, word for word, about the time conflict. A
push about a job the recipient is gated out of taking is noise on the one channel ADR-0036 depends on
being worth reading. Placing this reason beside `ShortLeadTime` (notify, no hold) would be wrong for
exactly that reason: short lead means *we cannot hold*, busy means *they cannot take*.

**The D4.1 invariant survives unchanged:** `HoldUntilUtc != null ⇒ NotifyPreferred == true` — both are
false here.

**Where the check sits in the resolver, and why last.** It is the only check that costs a query with a
range scan. Every other gate (preference set · membership · lead time · approved · work country ·
mute · device reachability) is an equality or an arithmetic comparison on rows the resolver is already
fetching. **The busy check runs last, only when everything else has passed**, so the ordinary path
(no preference, or a non-member) pays nothing. ADR-0036's decision-tree diagram gains one node between
the reachability gate and the lead-time gate — see the living doc.

**No new error key. No customer-visible rejection.** See D8.

### D3 — The read path is **one set-based question**, not N independent ones

> **What the picker needs is *"which of these cleaners are busy in this window"*, which is one query.
> `HasOverlappingOrderAsync` answers *"is this one cleaner busy"*, which is the same question asked
> twenty times.**

**Ruling: a new repository method.** Specified:

```csharp
// Cleansia.Core.Domain/Repositories/IOrderRepository.cs
//
// Which of `employeeIds` hold a LIVE-COMMITMENT assignment overlapping
// [windowStartUtc, windowEndUtc)?  Returns the BUSY subset — never the free set.
//
//  * ONE query for the whole set. Calling HasOverlappingOrderAsync in a loop over a
//    candidate list is a hard reject (ADR-0039 D3).
//  * "Live commitment" is OrderRepository.SlotBlockingStatuses WITH the same
//    NULL-CurrentStatus latest-history fallback HasOverlappingOrderAsync uses
//    (patterns-backend B7's fail-CLOSED exception). This method must NOT invent a
//    second definition of "occupied" — see D3.2.
//  * The scan is bounded BELOW by windowStartUtc - BookingPolicy.MaxOrderSpanHours.
//    Without a floor the only sargable term is `CleaningDateTime < windowEnd`, i.e.
//    all of history. See D3.1.
//  * TENANT-SCOPED, deliberately — every caller is a request path with a tenant claim.
//    A background sweep MUST call the IgnoringTenant sibling. This method does not
//    choose tenancy for its caller (D6).
Task<IReadOnlySet<string>> GetBusyEmployeeIdsInWindowAsync(
    IReadOnlyCollection<string> employeeIds,
    DateTime windowStartUtc,
    DateTime windowEndUtc,
    CancellationToken cancellationToken);

// Same predicate, IgnoreQueryFilters(). Sole intended caller: NewJobsDigestService.
Task<IReadOnlySet<string>> GetBusyEmployeeIdsInWindowIgnoringTenantAsync( /* … */ );
```

Implementation shape — **drive from `Orders`, not from `OrderEmployees`**:

```csharp
var floor = windowStartUtc.AddHours(-BookingPolicy.MaxOrderSpanHours);
var busy = await GetDbSet()                                  // or .IgnoreQueryFilters()
    .Where(o => o.CleaningDateTime >= floor                              // NEW: the range floor
             && o.CleaningDateTime <  windowEndUtc
             && o.CleaningDateTime.AddMinutes(o.EstimatedTime) > windowStartUtc
             && ((o.CurrentStatus != null && SlotBlockingStatuses.Contains(o.CurrentStatus.Value))
                 || (o.CurrentStatus == null && o.OrderStatusHistory
                        .OrderByDescending(s => s.CreatedOn).ThenByDescending(s => s.Sequence)
                        .Take(1).Any(s => SlotBlockingStatuses.Contains(s.Status)))))
    .SelectMany(o => o.AssignedEmployees)
    .Where(ae => employeeIds.Contains(ae.EmployeeId))
    .Select(ae => ae.EmployeeId)
    .Distinct()
    .ToListAsync(cancellationToken);
return busy.ToHashSet(StringComparer.Ordinal);
```

**Why this side and not the other.** `IX_OrderEmployees_EmployeeId` exists (`Initial.Designer.cs:3188`)
and would serve an `EmployeeId IN (…)` drive — but the date band can only prune *after* the join, so
each candidate's whole assignment history is still walked. Driving from `Orders` puts the two selective
terms together on **`IX_Orders_CurrentStatus_CleaningDateTime`** (`OrderEntityConfiguration.cs:111`),
which already exists and is already the board query's index: a status-set + narrow-date-band range
scan, then a semi-join to `OrderEmployees` on the FK index. **No new index. `D5.5`'s posture (no index
for this feature) is preserved.**

#### D3.1 — The scan floor is a **safety-asymmetric** constant, not a tuning knob

```csharp
// BookingPolicy — platform-wide, per ADR-0035 D2.1's placement rule.
/// The longest span a single booking may occupy. It exists ONLY as a query floor:
/// overlap scans start at windowStart - MaxOrderSpanHours instead of at the beginning
/// of time. It may only ever be TOO GENEROUS.
public const int MaxOrderSpanHours = 24;
```

**The number is chosen by its failure asymmetry, not by measurement:**

| If the floor is | Cost |
|---|---|
| **too generous** (hours earlier than any real order) | a slightly wider index range scan on a band that is nearly empty |
| **too tight** (by ten minutes) | an overlapping order is invisible → the picker says *available* → **and the same predicate is the `TakeOrder` write gate, so a cleaner is double-booked** |

So: **when in doubt, widen it.** `EstimatedTime` is the sum of the booked services' estimates
(`OrderFactory.cs:145-146`) for a single appointment on a single day; 24 h exceeds any single-day span
by construction. **It is checkable in one line** — `SELECT MAX("EstimatedTime") FROM "Orders"` must be
well under `MaxOrderSpanHours * 60`, and §verify #6 makes that a reviewer step rather than a belief.

**The durable alternative, recorded with its flip condition:** persist the appointment's **end**
instant on `Order` and index it, making both sides of the overlap sargable and the floor unnecessary.
Rejected for now — a migration plus a backfill plus a second column to keep in sync — but it is the
right answer and the flip condition is precise: **`MAX(EstimatedTime)` approaching the floor, or the
floor showing up in a slow-query report.** `Order.EstimatedTime` is written once
(`OrderFactory.cs:147`, no reschedule path exists — ADR-0036 CH-V9), so the denormalization would be
cheap to maintain when it is wanted.

#### D3.2 — One predicate, three shapes, and `HasOverlappingOrderAsync` **converges onto it**

The end state is not "a new method beside the old one". It is:

```csharp
public async Task<bool> HasOverlappingOrderAsync(string employeeId, DateTime cleaningDateTime,
    int estimatedTimeMinutes, CancellationToken ct)
    => (await GetBusyEmployeeIdsInWindowAsync([employeeId], cleaningDateTime,
            cleaningDateTime.AddMinutes(estimatedTimeMinutes), ct)).Count > 0;
```

**Two overlap predicates in one repository is the defect class this ADR is cleaning up, not a shape it
may leave behind.** The convergence also means the floor (D3.1) and the tenancy fix (D6) are applied
**once** and land on the write gate for free. `HasOverlappingOrderStatusTests` is the existing pin and
must stay green through the convergence — if it does not, the new predicate is not the old one.

**Sequencing, and it matters:** the new method must ship **with the floor and both tenancy variants
from day one**, so the convergence ticket is a *deletion*, not a redesign. Shipping the picker's method
without the floor and "adding it later" recreates Defect 2 in a second place.

### D4 — The window must be the **real** one, and its duration has exactly one definition

The overlap window is `[cleaningDateTimeUtc, cleaningDateTimeUtc + estimatedMinutes)`. `estimatedMinutes`
is not optional and not nominal:

| If the window is | Failure |
|---|---|
| **too short** | we say *available* for a cleaner who is busy — **the exact failure the owner is closing** |
| **too long** | we grey out a cleaner the customer could have had, invisibly and unappealably |

So a nominal window (e.g. `StandardWorkUnitMinutes`) is wrong in both directions and is rejected. The
duration must be the one the created order will carry.

**`QuoteOrder.Response` does not carry it** (`:42-57` — pricing only), so the client cannot supply it.
**And it must not**: a client-supplied number that decides a server answer is an S1 violation, and a
tampered one silently un-greys a busy cleaner.

**Ruling: the server derives it, from one definition, used by both writers.** `OrderFactory.cs:145-146`
computes it inline today and would gain a second reader:

```csharp
// Cleansia.Core.Domain/Orders/OrderDuration.cs — PURE. No I/O, no DI.
/// The single definition of "how long is this booking". OrderFactory persists it as
/// Order.EstimatedTime; the preferred-cleaner picker uses it to build the overlap
/// window. If these two ever differ, the picker is answering about a different job
/// than the one being booked.
public static int EstimateMinutes(IEnumerable<Service> services, IEnumerable<Package> packages);
```

`OrderFactory.cs:145-147` calls it instead of summing inline. The picker's handler loads the selection
through `IServiceRepository` / `IPackageRepository` exactly as `QuoteOrder.Validator` already does
(`:61-65`) and calls the same function.

**The anti-drift test is the one that matters and it is cheap:** for one selection of services and
packages, `OrderDuration.EstimateMinutes(...)` equals `Order.EstimatedTime` on an order created from
that same selection. **`TC-AVAIL-WINDOW-0`.**

**Noted, not fixed:** the overlap predicate treats `EstimatedTime` as **elapsed wall-clock for every
assigned cleaner**, even though `RequiredEmployees = ceil(EstimatedTime / 120)` says a 4-hour job with
2 cleaners is 2 hours of calendar. That is a pre-existing modelling choice in
`HasOverlappingOrderAsync` (`:284`), it is **conservative** (it blocks more calendar than it must, so
it fails *closed*), and this ADR deliberately does not change it — changing it would loosen the
`TakeOrder` write gate as a side effect of a display feature. Recorded so nobody "fixes" it inside this
work.

### D5 — Where the answer is produced: **extend `GetMyServingCleaners`; do NOT build "is cleaner X free?"**

**Ruling: the picker's existing source gains the answer.** `GetMyServingCleaners` (`:10-56`) is the
picker's only feed on both clients; extending it costs one round trip instead of two and makes the flag
structurally impossible to request for a cleaner outside the customer's own set.

```csharp
// Cleansia.Core.AppServices/Features/Orders/GetMyServingCleaners.cs
// EVERY new field is optional, so the shipped clients keep compiling and keep working.
public record Query(
    DateTime? CleaningDateTimeUtc      = null,
    IReadOnlyList<string>? SelectedServiceIds = null,
    IReadOnlyList<string>? SelectedPackageIds = null) : ICommand<IReadOnlyList<Response>>;

public record Response(
    string   EmployeeId,
    string   FullName,
    DateTime LastServedOn,
    /// TRI-STATE, and the third state is load-bearing:
    ///   true  — free for the requested slot
    ///   false — holds a live-commitment assignment overlapping it
    ///   null  — NOT EVALUATED (no slot in the request, or the check could not run)
    /// `null` MUST render as no marking. A client that maps it to a bool renders every
    /// cleaner unavailable (false-default) or defeats the feature (true-default).
    bool?    IsAvailableForRequestedSlot);
```

**Why not a general endpoint.** A `GET /employees/{id}/availability?from=…&to=…` — the shape an
implementer reaches for — is a **schedule oracle for any employee id in the system**. The extension
form carries two structural limits the general form throws away:

1. **You can only ask about cleaners who have already completed a job for you.** The set comes from
   `CurrentStatus == Completed` on the caller's own orders (`:26-33`); the flag is a projection over
   that set and cannot be aimed anywhere else.
2. **You can only ask about one instant — the one you are booking.** There is no range parameter, and
   §D7 makes "never add one" a standing constraint rather than an omission.

**The tri-state is not defensive programming — `null` is reachable on day one** (a client that has not
been rebuilt; the confirm step before a slot is chosen; the check failing). §D8 rules what `null` means
everywhere: **absence of an answer is never a yes.**

**Named cost:** the response shape changes ⇒ ⚠️ **`nswag-regen` (owner-only)** for the customer web
client plus the Android/iOS DTOs. No migration. No new endpoint. No partner or admin surface.

**Pre-existing shape, flagged not fixed:** `GetMyServingCleaners` materializes the **full order graph**
for every completed order of the customer (`:26-33`, three `Include`s + `AsSplitQuery` + in-memory
`GroupBy`) to produce ≤20 names. That is a separate cost-shaped ticket (§D10); this ADR adds **one**
bounded query beside it and must not be blamed for the one that was there.

### D6 — Tenancy: the method must **not choose for its caller**

The generalization of Defect 1, and it is the rule that belongs in the catalog:

> **A repository method reachable from BOTH a request path and a background sweep must not pick its own
> tenancy. Name the two variants, per the shipped `EmployeeRepository.GetByIdAsync` /
> `GetByIdIgnoringTenantAsync` precedent (`:44-57`), and let the call site say which world it is in.**

Applied here:

| Caller | Variant | Why |
|---|---|---|
| the picker (`GetMyServingCleaners`) | **tenant-scoped** | a customer request with a `tenant_id` claim; the cleaner and the customer are in the same tenant |
| `IPreferredCleanerHoldResolver` (via `OrderFactory` ← `CreateOrder`) | **tenant-scoped** | same request, same claim |
| `IPreferredCleanerHoldResolver` (via `MaterializeRecurringBookings`) | **tenant-scoped**, under the sweep's **per-template override** | `MaterializeRecurringBookings.cs:54-74` already sets `SetTenantOverride` per template; the resolver inherits it. **Do not** reach for the ignoring variant here — it would widen a per-tenant materialization |
| `NewJobsDigestService` (`:137`) | **ignoring** | the timer has no claim; the sweep is already `GetQueryableIgnoringTenant` on both other reads (`:63`, `:98`) |

**Ticket, not scope creep — but it is a precondition for one of the two.** The digest's fix is a
correctness fix on a shipped push path and belongs to ADR-0028's lane (T-0529's status log
already asks for it). The picker's method is new code and can land tenant-correct immediately. §D10
files both and names which blocks which.

### D7 — What the customer sees, and the copy constraints

#### D7.1 — The treatment

| | Ruling |
|---|---|
| Row visibility | **shown** — never removed from the list |
| Row state | greyed (reduced-emphasis foreground on both clients' existing token), **not tappable**, no selection ring, no checkmark |
| Row text | name, unchanged, **plus one neutral line**: *"Not available for this date and time"* |
| Clearing | if the currently-selected cleaner becomes unavailable when the customer changes the slot, the selection is **cleared** and the picker's persistent explanatory line (ADR-0036 C2c) returns. **No toast, no dialog, no "your cleaner was removed" alert** — the row is visibly marked and that is the disclosure |
| The **unmarked** rows | **nothing changes about them.** See D7.3 |

#### D7.2 — The copy constraint set (binding on whoever writes the strings — T-0491's lane)

1. **One string, five languages, both customer clients.** Android `values*/strings.xml` (5 files), iOS
   `Localizable.xcstrings` (1 file / 5 languages). The web customer wizard has **no picker at all**
   (`order-wizard.facade.ts:576-580` sends `undefined` unconditionally), so there is no web cost today
   — and the ticket that builds the web picker inherits this constraint.
2. **It names no reason.** Not *"already booked"*, not *"busy"*, not *"has another job"*, not
   *"working elsewhere"*, no time, no count, no place, nothing about anyone else's booking. **It is a
   statement about what Cleansia can offer, not about what the person is doing.**
3. **It promises no other time.** No *"try a different time"* suggestion, no *"next available"*, no
   affordance that leads to a calendar. We do not have that answer and we are not building the claim.
   *(This is the sprint's own lesson applied: ADR-0036 C0 is deleting the unbacked "Within 1 hour"
   claim right now. Do not ship its replacement in the same feature.)*
4. **It stays true if the predicate widens.** *"Not available for this date and time"* remains true if
   a later revision folds approval, work country or anything else into the flag. *"Already booked"*
   becomes a lie the moment it does. **Pick the label that survives the next version of the rule.**
5. **It is never shown for `null`.** An unevaluated answer renders as an ordinary selectable row.
6. **It must not upgrade the promise for the others** — see D7.3.

#### D7.3 — The subtle one: marking three rows must not silently promise the other two

Greying two of five cleaners implies the remaining three are *available to you*, which is a stronger
claim than ADR-0036 D1 permits — the perk is **first chance, never "your cleaner"**, and every gate in
`TakeOrder` still applies to the preferred cleaner exactly as to anyone else (ADR-0036 D5.1's AC6
interaction table).

> **Constraint: ADR-0036's persistent explanatory line (C2c) is UNCHANGED and still renders on the
> picker row.** The unavailable marking is subtractive only. Nothing in this feature may be written as
> *"these cleaners are available for your booking"* — the honest frame is *"these ones we already know
> we cannot offer"*.

A reviewer checks this by reading the two strings side by side: the C2c line still says *first chance*,
and the new line says only *not available*. If the C2c line was changed to sell availability, that is a
finding against this ADR, not a copy improvement.

#### D7.4 — What is disclosed, stated plainly rather than argued away

**This flag does disclose something about the cleaner.** No amount of neutral wording changes that, and
pretending otherwise would be exactly the kind of unbacked claim this platform is currently removing.

- **What is disclosed:** that a cleaner **who has personally completed a job for this customer** is
  occupied during **the single window this customer chose**.
- **What is not:** who booked them, where, for how long, what kind of job, or anything about any other
  window the customer did not ask about.
- **Why it is accepted:** there is no way to tell a customer *"you cannot have Anna at 10:00"* without
  telling them Anna is unavailable at 10:00. The feature is the disclosure. The alternative the owner
  overturned — offer her, take the booking, silently drop the preference — discloses nothing and lies.
- **The residual, named:** a determined customer can probe by re-opening the picker across slots.
  Bounded by the serving-cleaner set (people who have been in their home), and by the fact that a
  `false` names no reason.

> **The line this ADR draws and does not cross: no date-range parameter, no calendar view, no
> "next available" suggestion, ever.** Those turn a per-booking answer into a schedule feed about a
> worker, and they are a **separate decision with a separate privacy analysis**, not an extension of
> this field. An implementer who "helpfully" adds a range parameter has changed the decision.

**Escalated to the owner (`Q-AVAIL-04`, §Escalations):** whether cleaners should be told — in the
partner app or the terms — that past customers can see, at booking time, whether they are free for a
specific slot. That is a worker-transparency and privacy-policy question with a business answer, not an
architect's call. **Not blocking** — it changes text, not the mechanism.

### D8 — The race, and the tri-state, are the **same** ruling

The cleaner is free when the picker renders and taken by the time the order submits. There is no lock
and there must not be one — **a picker render must never reserve a stranger's calendar.**

**The ruling, and it is one sentence: absence of a `yes` is never a `yes`, and it is never a rejection
either.**

| Moment | Outcome |
|---|---|
| Picker says **available**, submit finds them **busy** | **The order is created. Normally. Fully.** `Order.PreferredEmployeeId` is **stored** (ADR-0036 D2: *"we stored your preference but could not act on it"* must be expressible). `PreferredHoldUntilUtc` is **null**. No targeted push. `Reason = CleanerBusyAtCleaningTime`. |
| The check **could not run** (query failed) at the picker | `IsAvailableForRequestedSlot = null` → **no marking, row selectable.** Degraded to today's behaviour, which is a degradation, not a lie. |
| The check **could not run** at the resolver | **The booking is never failed for it.** Treated as *unknown* ⇒ **no hold, no push**, same as busy. A perk fails closed; a booking never does. |
| No slot in the request (old client, or the confirm step before a slot exists) | `null` → no marking. The clients that ship today are exactly this case and keep working unchanged. |

**What the customer is told when they lose the race: nothing new.** ADR-0036 D6 and A8 stand
unamended — no push, no state, no *"Anna couldn't take it"*. The picker's answer was **true when it was
given**; the perk was never a guarantee (D1); and a notification whose entire content is bad news about
something the customer did not know was in doubt manufactures a disappointment out of a normal outcome.

**And this is deliberately NOT ADR-0036 D7's treatment.** D7 *rejects the whole booking* when the
membership gate fails. The difference is not severity, it is **agency**, and ADR-0036 D8 already named
the rule this ADR is applying: ***reject where someone can react; degrade where nobody can.***

| | Membership (D7) | Busy at submit (here) |
|---|---|---|
| Fact is | **static** — true before the customer opened the app | **dynamic** — became true seconds ago, possibly mid-checkout |
| Customer can fix it in one tap | **yes** (remove the request, or subscribe) | **no** — the only fix is to move their own appointment |
| Therefore | **reject**, with a message that names the tap | **degrade**, silently, and create the order |

**Flip condition, so this is falsifiable:** support evidence that customers believe the preference was
honoured when it was not. **The answer then is copy (ADR-0036 C0/C2c), not a push** — and it is
foreseen here so the next reader does not reopen A8.

### D9 — Recurring inherits this with **no new rule**

`MaterializeRecurringBookings` resolves each occurrence ~7 days out (`HorizonDays = 7`) and calls the
same resolver (ADR-0036 D8.2). The busy check applies unchanged, and its failure lands in the **degrade**
path ADR-0036 D8.3 already specifies: **materialize the occurrence with no hold** — and, since the
preference itself is still legitimate, **keep `PreferredEmployeeId`** and drop only the hold. Never fail
the occurrence. Nothing new is decided here; it is recorded so the materializer's implementer does not
have to infer it.

Note the tenancy subtlety §D6 already covers: the sweep sets a **per-template override**, so the
tenant-**scoped** variant is the correct one there.

### D10 — Tickets (this ADR builds nothing; `git diff --stat -- src/` is empty)

| # | Candidate | Size | Notes |
|---|---|---|---|
| **A0** | **`HasOverlappingOrderAsync` is tenant-scoped under a tenant-ignoring caller.** The ticket T-0529's status log asked for and nobody filed. Add the `IgnoringTenant` sibling, switch `NewJobsDigestService.cs:137`, pin with a **non-null `TenantId`** fixture (`NewJobsDigestTenantWatermarkTests` is the shape). `security_touching: true`. **ADR-0028's lane.** | **S** | **File first — independent of everything below, and live on a shipped push path** |
| **A1** | **The set-based method + the floor + the convergence** (D3, D3.1, D3.2). `GetBusyEmployeeIdsInWindowAsync` + its ignoring sibling + `BookingPolicy.MaxOrderSpanHours` + `HasOverlappingOrderAsync` reduced to a wrapper. `HasOverlappingOrderStatusTests` stays green. No migration, no DTO change. **Absorbs A0 if A0 has not shipped — do not do both by hand.** | **M** | backend only |
| **A2** | **`OrderDuration.EstimateMinutes` + `OrderFactory` rewire + `TC-AVAIL-WINDOW-0`** (D4). Pure extraction, one call-site change. | **S** | backend only |
| **A3** | **The picker answer** (D5). `GetMyServingCleaners` query/response fields + the one call to A1 + the tri-state. ⚠️ **`nswag-regen` (owner-only)**. Depends on **A1 + A2**. | **S** | backend + ⚠️ manual step |
| **A4** | **The picker UI + the string** (D7). Android `PreferredCleanerPicker.kt:167-176` (list rows) + `:131-135` (the selected line, **unchanged** per D7.3) and the iOS equivalents, × 5 locales × 2 clients. Depends on **A3** + the regen. | **S** | mobile ×2 |
| **A5** | **The resolver check** (D2). `HoldDeclineReason.CleanerBusyAtCleaningTime` + the call + the D4.1 table row. **Belongs to ADR-0036's T-0515**, which builds the resolver — not a separate ticket, an added AC. Depends on **A1 + A2**. | **XS** | fold into T-0515 |
| **A6** | **`GetMyServingCleaners` materializes full order graphs for ≤20 names** (D5, flagged not fixed). Project server-side. Pre-existing, cost-shaped. | **S** | optimizer lane |

**Sequencing that is not negotiable:** **A1 ships with its floor and both tenancy variants**, or the
picker's method becomes the third place Defect 2 lives. **A3 must not ship before A4** — a server that
answers a question no client renders is harmless; a client change without the server is not possible;
but shipping A3 alone and calling the feature done leaves the owner's requirement unmet with a green
ticket. **A5 rides T-0515** because the resolver does not exist yet.

### D11 — Scope boundary

- **In scope:** the check, where it is asked, how it is asked, what it costs, what the customer sees,
  what happens when it is stale or unanswerable, and the two ADR amendments.
- **Byte-untouched:** the hold mechanism (`Order.GrantPreferredHold` / `ClearPreferredHold`,
  `OrderVisibility`'s five terms, `BookingPolicy.ComputePreferredHold`, Invariant H, D5.5's no-index
  ruling), every `TakeOrder` gate, `OrderStatus`, the pay formula, `EmployeePayConfig`, every fiscal
  path, `ITenantEntity` and the global filter's definition, the outbox contract.
- **Not decided here:** the final wording (T-0491 — D7.2 constrains it), whether `GetMyServingCleaners`
  should also drop cleaners `TakeOrder` would categorically refuse (**a filter on the list, not a flag
  on the row** — a different shape for a different fact; filed), the web wizard's missing picker, and
  the seat-count question (ADR-0037 D9 / `Q-AVAIL-03`).

---

## Alternatives considered

| # | Alternative | Why not |
|---|---|---|
| **A1** | **Keep ADR-0036 D5.1 as written** — the hold is cheap, a busy cleaner declines. | The owner overturned the premise, and the panel's own arithmetic supports them: the *busy-at-creation, busy-at-take* case is 100% of the first seat's fill window spent on a zero-probability outcome. Invariant H bounds what we **cannot** know; it is not a licence to spend what we **can**. |
| **A2** | **Call `HasOverlappingOrderAsync` once per candidate as the picker renders** — no new repository method, no ADR. | Two verified defects make this both **wrong** and **expensive**: under a tenant it returns `false` for every cleaner (Defect 1) so the feature is a no-op that renders every cleaner available; and each call is an unbounded lifetime scan with a per-row interval computation (Defect 2), ×20 per render, on the booking hot path. This is the naive implementation, and it is why this is an ADR. |
| **A3** | **A general `GET /employees/{id}/availability?from=&to=` endpoint** — reusable, obvious, testable. | It is a **schedule oracle for any employee id in the system**, requestable by any authenticated customer, over any range. The extension form (D5) gives the same answer while making the two limits structural: only your own serving cleaners, only the one instant you are booking. **A range parameter is not a feature request, it is a different decision** (D7.4). |
| **A4** | **Hide busy cleaners from the list.** Cheapest; no new string; no disclosure. | The customer's favourite silently vanishes from a list they have seen before, and the platform manufactures a mystery to avoid writing a sentence. It also discloses **exactly the same fact** to anyone who notices — a shorter list is a diff. The owner's word is *"mark"*. |
| **A5** | **Grey the row with no explanation.** | The failure ADR-0036 spent a whole panel on: a surface that changes behaviour without changing text. A greyed row with no line is a bug report waiting to be filed. |
| **A6** | **Mark them but let them be selected anyway** ("we'll try"). | Then D2 has to be enforced twice, and — worse — the customer has been allowed to make a choice we have already told them we cannot honour. *"We'll still note your request"* is precisely the sentence ADR-0036's living doc forbids. |
| **A7** | **Name the reason** — *"Anna is already booked at this time"*. Warmer, more informative. | It discloses another customer's booking as a fact about a named person, and it becomes **false** the moment the flag's predicate widens (approval, work country). D7.2's rule: pick the label that survives the next version of the rule. |
| **A8** | **Offer alternative times** — *"Anna is free at 14:00"*. The genuinely helpful version. | A **schedule feed about a worker**, delivered to a customer, derived from other customers' bookings. It is also a claim we would have to keep true across the whole board. This platform is currently deleting the last claim it could not back (ADR-0036 C0's *"Within 1 hour"*); shipping a new one inside the fix is the same mistake with better intentions. |
| **A9** | **Reject the booking when the preferred cleaner is taken at submit time** (mirror ADR-0036 D7's membership rejection). | D8. D7 rejects a **static** fact the customer can fix in one tap. Busyness is **dynamic** and unfixable in one tap — the only "fix" is to move their own appointment. ADR-0036 D8's rule already decides this: *reject where someone can react; degrade where nobody can.* |
| **A10** | **Push the customer when the race is lost** — *"Anna just got booked; we'll find someone else."* | ADR-0036 A8, unamended: a notification whose entire content is bad news about something the customer did not know was in doubt. The picker's answer was true when it was given, and the perk was never a guarantee. |
| **A11** | **Hold/soft-reserve the cleaner's calendar while the picker is open.** | A picker render would reserve a stranger's calendar; abandonment (the common case) would need a sweep to release it, whose failure mode is *a cleaner blocked for a booking that was never made* — ADR-0036 A5's stuck-state catastrophe, moved onto a person's working day. |
| **A12** | **Let the client send `estimatedMinutes`** (it has the quote in hand). | It does not (`QuoteOrder.Response:42-57` is pricing only) — and it must not: a client-supplied number that decides a server answer is S1, and a tampered one silently un-greys a busy cleaner. |
| **A13** | **Use a nominal window** (`StandardWorkUnitMinutes`, or a flat 2 h) and skip D4's extraction. | Wrong in **both** directions (D4): too short re-opens the exact failure being closed; too long greys out a cleaner the customer could have had, invisibly. The extraction is a pure function and one call-site change. |
| **A14** | **Add a partial/covering index to serve the overlap scan.** | Unnecessary: `IX_Orders_CurrentStatus_CleaningDateTime` already exists and, once the D3.1 floor is present, serves the predicate as a range scan. Adding an index instead of a `WHERE` bound would pay maintenance on every `INSERT INTO Orders` to avoid writing one term. It would also break ADR-0036 D5.5's posture of adding **no** index for this feature. |
| **A15** | **Persist the appointment's end instant and index it** — the fully sargable form, no floor constant. | The right long-term answer and **recorded as such** (D3.1), not rejected on principle: it costs a migration, a backfill and a second column to keep in sync, for a scan the floor already bounds. Flip condition named: `MAX(EstimatedTime)` approaching the floor, or the floor in a slow-query report. |
| **A16** | **Also check the weekly cap at creation** ("finish the job A6 started"). | Refuted on evidence, not taste: `GetEmployeeOrderCountThisWeekAsync` (`:247-258`) derives its window from **`DateTime.UtcNow.Date`**, so at creation, for a booking more than a week out, it answers about a week that does not contain the booking. A6's cap half **stands**. |
| **A17** | **Make the flag "can I have this cleaner at all"** — fold approval / work country / contract status into it. | Two different facts want two different shapes. Static ineligibility never changes with the slot, so it belongs as a **filter on the list** (a cleaner `TakeOrder` would categorically refuse should not be offered at all); the slot conflict changes with the slot, so it belongs as a **flag on the row**. Folding them makes the list's own defect invisible and the flag's meaning unstateable. Filed separately. |

---

## Consequences

**Cheaper / safer**
- **The picker and the grant cannot disagree** — same method, same window, one predicate (AC2).
- **One query per picker render**, servable from an index that already exists, replacing up to twenty
  unbounded history scans. **No new index** — ADR-0036 D5.5's posture is preserved.
- **The write gate gets the floor for free** via D3.2's convergence, and `HasOverlappingOrderStatusTests`
  is the pin that proves the predicate did not change while it moved.
- **Zero data risk.** No migration, no backfill, no schema change, no new index, one nullable DTO field.
- **The shipped clients keep working unchanged** — every new request field is optional and the answer's
  third state (`null`) is the honest one for them.
- **The floor's number is decided by failure asymmetry and verified by one SQL line**, not by tuning.

**More expensive (accepted, and named)**
- **One extra bounded query on the create path** when a preference is set and every cheaper gate has
  passed — and one on each picker render. Priced by reasoning, not by measurement (no `EXPLAIN`, no row
  counts): the same honest caveat ADR-0036 carries.
- **⚠️ `nswag-regen`** on the customer clients, and a new string × 5 languages × 2 clients.
- **A real, bounded disclosure about a cleaner's schedule** to customers they have served (D7.4), with a
  worker-transparency question escalated rather than assumed.
- **The 2–8 h short-lead band now has two ways to get no hold** (lead time, and busyness) and one of
  them also removes the push. The band's copy must not imply otherwise — ADR-0036 §Copy already
  requires a sentence true in both outcomes; this adds a third outcome it must also survive.

---

## How a reviewer verifies compliance

1. `rg -n "HasOverlappingOrderAsync" src/ --type cs` — **no call inside a loop, anywhere.** The picker
   and the resolver call `GetBusyEmployeeIdsInWindowAsync`; after D3.2 the singular method is a
   two-line wrapper and its only production callers are `TakeOrder` and the digest.
2. **Both tenancy variants exist and every call site picks one deliberately.** `NewJobsDigestService`
   calls the **ignoring** variant; the picker, the resolver and `TakeOrder` call the scoped one.
   `MaterializeRecurringBookings` uses the **scoped** one under its existing per-template override
   (`:54-74`) — reaching for the ignoring variant there is a finding.
3. **The floor is present in the emitted SQL.** `ToQueryString()` on the new method shows
   `"CleaningDateTime" >= @floor`. Absent ⇒ hard reject: the query is the old unbounded scan wearing a
   new name.
4. **One definition of "occupied".** `SlotBlockingStatuses` is referenced, not re-listed, and the
   NULL-`CurrentStatus` latest-history fallback is present with its `(CreatedOn desc, Sequence desc)`
   ordering (`patterns-backend.md` B7's fail-closed exception). A second status list is a hard reject.
5. **`TC-AVAIL-WINDOW-0` exists**: `OrderDuration.EstimateMinutes(services, packages)` equals
   `Order.EstimatedTime` on an order created from the same selection. `OrderFactory` sums nothing inline.
6. **The floor is checkable, and was checked.** `SELECT MAX("EstimatedTime") FROM "Orders"` is well
   under `BookingPolicy.MaxOrderSpanHours * 60`, and the reviewer records the number in the ticket.
7. **The tri-state survives the wire.** With no slot in the request, every row's
   `IsAvailableForRequestedSlot` is `null` and **both clients render no marking**. A client that maps
   `null` to a `Bool`/`boolean` is a hard reject — flip one row to `null` in a fixture and watch the UI.
8. **The resolver's new reason notifies nothing.** `CleanerBusyAtCleaningTime` ⇒ `NotifyPreferred ==
   false && HoldUntilUtc == null`. It sits **last** among the resolver's checks; a busy check that runs
   before the membership gate is a finding (it pays a range scan for every non-member).
9. **The race creates the order.** A test that makes the cleaner busy between the picker read and the
   submit asserts: order created, `PreferredEmployeeId` **stored**, `PreferredHoldUntilUtc` **null**, no
   `order.preferred_offer` outbox row, and **no** customer-facing error.
10. **The copy names no reason and offers no time.** Read all ten strings (5 languages × 2 clients):
    no "booked", no "busy", no time, no count, no "try", no "next available". And ADR-0036's C2c
    persistent line is **unchanged** (D7.3) — if it now sells availability, that is a finding.
11. **No range parameter exists anywhere** on the picker query, the repository method's public surface,
    or any controller. `rg -n "availability" src/Cleansia.Web.Customer src/Cleansia.Web.Mobile.Customer`
    returns no endpoint.

---

## Escalations (owner) — listed here, **not** written to `questions/open.md` by this ADR

- **`Q-AVAIL-04` — worker transparency (business/legal, not blocking).** Should cleaners be told — in
  the partner app, the terms, or both — that a past customer can see, at booking time, whether they are
  free for one specific slot? The disclosure is real, narrow and unavoidable if the feature exists
  (D7.4). It changes text, not the mechanism, so nothing waits on the answer.
- **Cross-reference `Q-AVAIL-03`** (the seat cap — `RequiredEmployees` vs `RequiredEmployees + 1`),
  raised by ADR-0037 D9 under the owner's second ruling. Not this ADR's, listed so the two owner
  questions from one conversation are findable together.

---

## Challenge

<!-- Challengers: name the specific hole (alternative dismissed too fast, seam broken, future change
     made expensive, hidden coupling, cheaper option) and why it matters, citing file:line. A
     challenger that finds nothing says so and names what they checked.

     Suggested angles, named by the author so they are not "discovered" as novel:
       • Is `MaxOrderSpanHours = 24` an invariant or a hope? What enforces it? (D3.1)
       • Does D5's extension of GetMyServingCleaners really constrain probing, or does it just make
         probing slower? (D7.4)
       • Is D2's "no push either" right, or does a busy cleaner still want to know they were asked for?
       • D3.2's convergence changes the TakeOrder write gate's SQL. Is `HasOverlappingOrderStatusTests`
         actually sufficient to prove it did not change behaviour?
       • The picker asks for the window `[start, start + EstimatedTime)` while the created order may
         carry a different EstimatedTime if the customer edits the selection after the picker renders.
         Does TC-AVAIL-WINDOW-0 cover that, or only the static case? -->

## Defense

<!-- Author: REBUT (with evidence) / CONCEDE + REVISE (fold the fix in above) / ESCALATE, per challenge. -->

## Verdict

<!-- Lead: every challenge RESOLVED or BLOCKING. Consensus = zero blocking. Then status → accepted. -->
