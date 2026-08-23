# Order availability — which orders a cleaner may be offered, and which they may take

> **Status of this page: THE CURRENT SHAPE — binding.**
> **[ADR-0037](/decisions/adr-0037)**
> is **`accepted`** (2026-08-03) after a defense panel: **two challenger lanes, 19 findings, 8 marked
> blocking, all resolved**. The ADR is the immutable record and carries the full
> `## Challenge` / `## Defense` / `## Verdict` trail; **this page is the evolving companion and is what
> you read first.**
>
> ⚠️ **AMENDED 2026-08-03 BY THE PANEL — two amendments change the ruling itself; read them before
> implementing anything:**
> 1. **The predicate gained a `NotRetractable` conjunct.** The draft's `Confirmed ∨ (New ∧ Cash)` was
>    falsified in *both* directions by two live sweeps (CH-M3, CH-M4). See §"The rule".
> 2. **`TakeOrder`'s rule ordering does not work the way the draft said**, and the fix is structural
>    (one chain + a test), not a comment. See §"The take gate ships".
>
> ⚠️ **AMENDED 2026-08-03 by owner instruction — `Q-AVAIL-01` IS ANSWERED: yes, a second cleaner may
> join a partly-staffed job.** Folded into the ADR as **§D9**. **The status axis is unchanged**; D9 is
> the *seat* axis. See §"The seat axis" below. The owner's other ruling from the same conversation —
> preferred-cleaner slot availability — is
> **[ADR-0039](/decisions/adr-0039)**.
>
> Companion pages: [`preferred-cleaner-dispatch.md`](./preferred-cleaner-dispatch.md) (ADR-0036 — the
> hold predicate this composes with, as a separate conjunct on the same six surfaces),
> [`push-notifications.md`](./push-notifications.md) (ADR-0025 — the digest's display contract),
> [`mobile-result-contract.md`](./mobile-result-contract.md) (ADR-0011 — how the new error key reaches
> the clients). Published view: `docs/architecture/backend.md`.

---

## Today (shipped, verified 2026-08-02 — every row read, not inherited)

**~~Eight~~ TEN surfaces answer one question and no two of them agree** (rows 9–11 added by the panel,
CH-X5/CH-X6 — the draft counted queries and missed the **buttons**).

| # | Surface | Today | Kind |
|---|---|---|---|
| 1 | `NewJobsDigestService.cs:52-53` | `{New, Pending, Confirmed}` | push |
| 2 | `DashboardSpecifications.cs:24` | `{Pending, Confirmed}` | count (`GetDashboardStats.cs:236`) + preview (`GetAvailableJobsPreview.cs:50`) |
| 3 | `GetPagedOrders.cs:87` | client-supplied, **no server floor** | list |
| 4 | `OrderSpecification.cs:134-139` | **seat arithmetic, status-blind** | **server-side visibility** |
| 5 | `orders.facade.ts:142-146` | `{New, Pending, Confirmed}` | web display (query) |
| 6 | `OrdersListViewModel.kt:248` | `{New, Confirmed}` | Android display |
| 7 | `OrdersListLogic.swift:78` | `{New, Confirmed}` | iOS display |
| 8 | `TakeOrder.Validator:38-60` | **no status rule** | write gate |
| **9** | `orders.models.ts:169-176` | `{New, Pending, Confirmed}` ∧ seat | **web row-action BUTTON** |
| **10** | `order-details.helpers.ts:108-115` `canTakeOrder` | `{Pending, Confirmed}` ≡ **`{Confirmed}`** | **web detail BUTTON** |
| *11* | `orders.helpers.ts:46-57` | offers dead `Pending`, **omits `New`** | web filter dropdown |

**#10 is the one that contradicts the ruling in the direction that hides work.** A `New` **cash**
order — Fact 2's strongest argument — is listed on the board, takeable by the server, and its detail
page shows **no Take button**. Both mobile clients get it right
(`OrderPrimaryAction.swift:44-48`, `OrderPrimaryAction.kt:57-58`).

**#11 is a cliff.** Any filter selection **replaces** the default list (`orders.facade.ts:142` is
`?? [...]`), and `New` is not an option — so touching the filter at all deletes the whole cash pipeline
from the board, with `Pending` (guaranteed empty) offered in its place.

`NewJobsDigestService.cs:49-50` claims to mirror #2. It does not, and they disagree on the first term.
`GetAvailableJobsPreview.cs:46-49` claims to match the mobile tab. It does not either — **a third
false mirror**, found while verifying.

**Three properties of the shipped system that make this more than untidiness:**

- **`OrderStatus.Pending` has no production writer.** Thirteen `AddOrderStatus` call sites write
  `New`/`Confirmed`/`Cancelled`/`OnTheWay`/`InProgress`/`Completed`; only the generic
  `AdminOverrideOrderStatus.cs:108` *could* write `Pending`. So `{Pending, Confirmed}` ≡ `{Confirmed}`.
- **A *one-off* cash order stays `New` forever.** `OrderPaymentDispatcher.cs:59-69` writes no status
  track, so nothing confirms it except a cleaner taking it (`TakeOrder.cs:192-194`). The partner
  dashboard's available count is **structurally zero** for a pipeline of cash orders — beside a list
  that shows them. *(Panel correction, CH-M3: **not** the only writer — `ConfirmRecurringOrder.cs:111-112`
  moves a **recurring** cash order to `Confirmed` + `Paid` with no cleaner assigned. That is the real
  generator of an offerable `Confirmed` cash order, and the discriminator the new money term reads.)*
- **A `Cancelled`/`Completed` order with a free seat is takeable right now.** `TakeOrder`'s only
  order-side gates are `ExistsAsync` and `HasAvailableSpots`. The assignment lands, burns one of the
  cleaner's 3/6/10 weekly slots (`OrderRepository.cs:254-258` — **no status filter**) and does not even
  block their calendar (`SlotBlockingStatuses` excludes terminal statuses). The path is a stale client,
  not malice. *(Panel correction, CH-M8a: the draft said "nearly all of them" on the strength of
  `MaxEmployees = RequiredEmployees + 1`. That is stale — `SpareSeatsPerOrder = 0` shipped, and neither
  cancel nor complete unassigns (`Order.UnassignEmployee` has one production caller,
  `AdminReassignOrder.cs:86`), so a **fulfilled** `Completed` order has no free seat and is already
  invisible. **The defect survives in full; only its blast radius shrinks** — `Cancelled`-before-take
  (common) and under-crewed multi-seat orders.)*
- **`TakeOrder` returns MULTI-ERROR responses no client can resolve, today.** Two `RuleFor` chains
  (`:38-45`, `:47-60`), FluentValidation 12.1.1 defaults `ClassLevelCascadeMode` to `Continue`, and
  `CleansiaApiController.cs:93-99` semicolon-joins failures sharing an `ErrorCode`. An unknown order id
  yields `order.not_found; order.time_conflict` → generic message on web, **raw joined string** on
  Android. *(Found by the panel, CH-M2. It also makes existence inferable from the error **pairing**,
  inverting ADR-0036's protection — see §"The take gate ships".)*

### One correction to the folklore, because it changes the trade-off

**`StaleOrderCleanupService` is not a safety net. It cannot run.** Its predicate requires an
`OrderStatus.Pending` history row (`:34`) that nothing writes, and `rg` finds **no caller and no DI
registration** — only the class and its interface. The sweep that actually runs is
**`CleanupStalePendingOrders`**: every 15 minutes, `OlderThanHours: 1`, keyed on
**`PaymentStatus.Pending && PaymentType == Card`** (`:51-53`) — the payment axis, never `OrderStatus`.

So the abandonment window for a card order is **~1h15m, not 30 minutes** — and the system already
has a working definition of "this card order may still evaporate". We do not need to invent a
discriminator; the ruling reads the one that exists.

---

## The shape (ADR-0037)

### The rule — two axes, not a status list

> **An order is offerable iff its fulfilment axis is pre-work with a free seat, and nothing that is
> still in flight can retract it.**

**⚠️ AMENDED BY THE PANEL (CH-M3 + CH-M4).** The draft was `Confirmed ∨ (New ∧ Cash)`. It stated the
invariant above in prose and then shipped a predicate that **did not test it**. Two scheduled sweeps
falsify it in both directions. **The status term is unchanged; a money conjunct is added.**

```
Offerable(o) ⟺ ( o.CurrentStatus == Confirmed
               ∨ (o.CurrentStatus == New ∧ o.PaymentType == Cash) )   -- STATUS term
             ∧ NotRetractable(o)                                       -- MONEY term (new)

NotRetractable(o) ⟺ o.PaymentStatus == Paid
                  ∨ (o.PaymentType == Cash ∧ o.RecurringTemplateId == null)
```

**`NotRetractable` is the union of the negations of the two retractors that actually run**, read off
their own `WHERE` clauses — the ADR's own method (*read the discriminator the system already wrote
down*):

| Retractor | Predicate | Note |
|---|---|---|
| `CleanupStalePendingOrders.cs:50-53` — every 15 min, `OlderThanHours: 1` | `PaymentStatus == Pending ∧ PaymentType == Card ∧ CreatedOn < now−1h` | **no `OrderStatus` term** — it kills `Confirmed` orders too |
| `AutoCancelStaleRecurringOrders.cs:63-69` — hourly, grace 1h | `RecurringTemplateId != null ∧ PaymentStatus == Pending ∧ CleaningDateTime <= now+1h` | **no `PaymentType` term** — it kills cash recurring too |

| Case | Offerable | Why |
|---|---|---|
| `New` + Cash, **one-off** | **yes** | Nothing in flight. The take *is* the confirmation. |
| `New` + Cash, **recurring**, `Pending` | **no — NEW** | Customer has not confirmed the occurrence; swept at **T−1h**, up to 7 days after creation. Worse for the cleaner than the card case. |
| `Confirmed` + Cash, recurring, `Paid` | **yes** | `ConfirmRecurringOrder.cs:111-112` — customer confirmed, no cleaner assigned. |
| `New` + Card | **no** | Checkout open/abandoned; cancelled within ~1h15m. |
| `Confirmed` + Card, `Paid` | **yes** | Money settled in the same commit as the status. |
| `Confirmed` + Card, `Pending` | **no — NEW** | Reachable via `AdminOverrideOrderStatus` (**no payment guard**) and via a **declined** card left `Pending` for retry (`HandlePaymentNotification.cs:230-242`). The 15-min sweep kills it under an assigned cleaner. |
| `Pending` | **no** | Dead status — no writer. |
| `OnTheWay`, `InProgress` | **no** | Work started. **No surface includes these** — uncontested. |
| `Completed` | **no** | Terminal. |
| `Cancelled` | **no** | Terminal **for this predicate at evaluation time**, *not* in the lifecycle: a late payment writes `Confirmed` + `Paid` over it (`HandlePaymentNotification.cs:254` short-circuits only on `Paid or Refunded`). That order correctly re-enters the set. **No surface may cache "has ever been cancelled."** |

**The asymmetry was the real bug.** The draft payment-qualified `New` and *trusted* `Confirmed` to
imply paid. `Confirmed` has four writers and only one writes the money in the same commit. **A rule
that trusts a status to imply a fact stored in another column is the same defect class as a comment
claiming two lists agree** — the class this page exists to close.

**Cash is not an exception to the rule — it is the rule with an empty *pre-work* money axis**, and
`NotRetractable` is where a future `PaymentType` (invoice, corporate account) answers *"can anything
still take this away from the cleaner?"* without touching a surface.

**Why the STATUS term keys on `PaymentType` and the MONEY term on `PaymentStatus`.** They answer
different questions. `PaymentType` is the money **model** — money before the work (card) or at the work
(cash) — which is what makes `New` admissible at all. `PaymentStatus` is the money **progress** — what
the two sweeps actually read, so the only column that can answer "will something retract this".
`!= Failed` and `== Paid` remain rejected **as whole rules** (the first admits abandoned *and declined*
cards, the second empties the cash board); **as a conjunct with the `Cash ∧ non-recurring` disjunct
carrying cash, `== Paid` is exactly right** — a different proposition the draft never evaluated.

### Offerable and takeable are the **same set**, read twice

Not two sets. One predicate, evaluated at read time (offer) and write time (take). Any gap between
them *is* the defect class this ruling exists to close.

What makes that coherent is separating two kinds of gate that `TakeOrder` currently interleaves:

| Kind | Over | Shared? |
|---|---|---|
| **Offerability** — status, payment type, free seat | the **order** | **yes** — `OrderAvailability` |
| **Eligibility** — approval, profile, weekly cap, time conflict, already-assigned, the ADR-0036 hold | the **(cleaner, order) pair** | **no** — stays in `TakeOrder` |

Only the offerability half centralizes. This keeps the new role small and is why the take gate does
not duplicate the six cleaner-side rules.

### Where it lives

`Cleansia.Core.Domain.Orders.OrderAvailability` — Domain is already referenced by every backend call
site (`DashboardSpecifications.cs:1-2`, `NewJobsDigestService.cs:3`; `OrderSpecification` *is* Domain).
No new project reference.

Three members, because a bare `OrderStatus[]` cannot express a payment-qualified rule — that
under-powered shape is what produced six disagreeing lists:

- `OfferableStatuses` = `[New, Confirmed]` — the **coarse floor**: the index-served prefilter on
  `Orders.CurrentStatus`, and the thing the clients mirror. **Not the rule.**
- `IsOfferableSql` — `Expression<Func<Order,bool>>`, composed into `OrderSpecification`.
- `IsOfferable(OrderStatus?, PaymentType, PaymentStatus, string? recurringTemplateId)` — in-memory, for
  the write gate. **[Panel amendment]** Four scalars, all columns on `Order`; no navigation properties,
  no I/O, no collaborator. The role card's old *"does NOT know: payment state — never `PaymentStatus`"*
  line is **struck**: the CH-M3/CH-M4 scenario is exactly the case the RDD rule anticipates — *if a
  scenario forces a role to know something on its "does NOT know" list, the responsibility was wrong.*
  It was. Availability is **not** "what money model is this"; it is **"can anything take this order
  away from the cleaner I hand it to"**, and that needs the progress column. What stays on the list,
  unchanged and load-bearing: **anything about a cleaner.**
- **Extension obligation** — an exhaustiveness test over `Enum.GetValues<PaymentType>()` goes red on a
  new enum member until `OrderAvailability` classifies it on **both** axes (offerable at `New`?
  retractable by which sweep?). No abstraction; switching on `PaymentType` is idiomatic here
  (`OrderPaymentDispatcher.cs:71-72` and `ConfirmRecurringOrder.cs:100-101` already carry `default:`
  arms).

**Two forms, pinned by `TC-AVAIL-EQUIV` against real PostgreSQL — not one shared expression.** This
follows ADR-0036's ruling verbatim (`preferred-cleaner-dispatch.md:107-109`): SQL and C# disagree on
null semantics and `.Compile()` on a request path is banned. ~~Our predicate has the same `NULL`
hazard.~~ **The two-forms ruling STANDS; only its NULL-hazard justification is retired — see the box
below. Do not read ADR-0040 as licence to unify the two forms.**

**~~NULL `CurrentStatus`~~ — ⚠️ BEING SUPERSEDED BY [ADR-0040](/decisions/adr-0040)
(`proposed`, 2026-08-04).** The ruling below is ADR-0037 §D3 as accepted, kept readable because the
implementation still carries it today:

> reads fail closed (`OrderSpecification.cs:115-116`); the **take must not**, or every legacy order
> becomes permanently untakeable. It resolves the way `HasOverlappingOrderAsync` already does
> (`OrderRepository.cs:285-288`) — column when non-null, else latest history by
> `(CreatedOn desc, Sequence desc)`. This also removes a latent NRE: `OrderMappers.cs:14-17` is
> `CurrentStatus!.Value` and `TakeOrder.cs:191` dereferences it on the request path.

**Why it is going.** The population it defends — rows written before the column was backfilled — has
never existed: the repo carries **one** migration (`20260723182623_Initial`) and the owner is dropping
the database and regenerating it. Verified write-time guarantee: `Order.Create` writes no track, but
its **only** production caller is `OrderFactory.cs:104`, and the **only** production
`orderRepository.Add` is `OrderFactory.cs:180` — the line after `AddOrderStatus(New)` at `:179`, with
no commit between. `AddOrderStatus` (`Order.cs:407-410`) is the single writer and never clears. The
conditional `Confirmed` write at `TakeOrder.cs:249` and the cash path's silence are conditional
*transitions*, not conditional *column writes*. Nothing in `sql-scripts/` inserts an `Orders` row, and
the "idempotent backfill" `OrderSpecification.cs:119-120` promises **does not exist as an artifact**.

**What changes when ADR-0040 is accepted:** the column is `NOT NULL`, the getter fallback and the six
`?? latest-history` fallbacks are deleted (each is a compile error, so the compiler drives the
migration), `IsOfferable`'s first parameter loses its `?`, `TC-AVAIL-EQUIV`'s NULL row becomes
unconstructible, and `OrderSpecification.cs:121-122`'s `OR`-wrapped status term becomes an
unconditional qual on the leading column of `IX_Orders_CurrentStatus_CleaningDateTime`. **No wire
change, no NSwag regen** — every response path already dereferences before `MapToCode()`.
The `AddOrderStatus` **recompute** (`Order.cs:404-410`) stays verbatim: it is the definition of the
column, not a fallback, and it is why a backdated track does not become current.

### What each surface becomes

| # | Verdict | Change |
|---|---|---|
| 1 | wrong | delete the array **and the comment**; compose `IsOfferableSql` |
| 2 | wrong | `OfferableStatuses` + the payment qualifier; fixes count **and** preview; delete the false comment at `GetAvailableJobsPreview.cs:46-49` |
| 3 | wrong, **fixed at #4** | no direct change — a blanket floor here breaks My-Completed and admin |
| 4 | **wrong — the one that matters** | `assigned-to-me OR (has-free-seat AND offerable)` |
| 5 | wrong | drop dead `Pending` → `[New, Confirmed]` |
| 6, 7 | **right about STATUS** | status literal unchanged — but **both change** their query params per §D9 (`hasAvailableSpots` + `excludeEmployeeId`) |
| 8 | wrong | gains the gate |
| **9** | **wrong** | `[New, Confirmed]`; same edit as row 5, **different file** — one query literal and one button literal disagreed and the draft saw only the query |
| **10** | **wrong, and it contradicts the ruling** | `[New, Confirmed]` + not-already-assigned. **The highest-value single line in the client work** — without it the cash pipeline stays unclickable on the web detail page |
| *11* | **wrong** | drop `Pending`, add `New` (3 lines, same file family) |

### The date floor is part of the same rule — [added by the panel, CH-X7]

The draft aligned the status/payment term and left the **date** term forked:

| Surface | Floor today |
|---|---|
| Dashboard count + preview (`DashboardSpecifications.cs:18`) | **none** |
| Available list, server default (`GetPagedOrders.cs:57-61`) | `now − 2h`, **only when `HasAvailableSpots == true`** |
| Available list, web client (`orders.facade.ts:149`) | `now` — stricter still |

Today mobile's count and list agree **by accident** (neither has a floor, because mobile sends
`isUnassigned`). **§D9's client switch breaks that accident** — the list gets the floor, the hero above
it does not. The goal "no '0 available' above a list of jobs" would become "N above a list of N−k".

> **One floor, a named constant, read by every surface** — the same property-not-formula rule §D9.4
> applied to time. `-2h` leaves the handler and becomes `BookingPolicy.OfferableGraceHours`; the
> dashboard spec reads it; **web drops its own `?? new Date()`** so all three clients inherit one
> server answer. Small deliberate behaviour change on partner web (jobs up to 2h past become visible,
> as on mobile); flip by changing the constant once, not one client. *Rejected: "document the count as
> deliberately floor-free" — that is D7's weakest layer wearing a hat.*

**#4 is the fix that carries the ruling.** It is the server's only authoritative floor on what a
browsing cleaner may read, and today it is pure seat arithmetic. Once it carries the status conjunct,
the client lists become a **display refinement, not a security boundary** (S1 server-truth) — which is
why two clients needing no change is a correct outcome rather than a suspicious one.

**Two of three clients were already right and the majority set was wrong.** Deciding by majority would
have shipped a dead status and left the take hole open.

### `OrderStatus.Pending` — dead, and the documented lifecycle is what is wrong

`CLAUDE.md` documents *"`Pending`: card payment initiated"*. That state is real and **already tracked
on the payment axis** (`PaymentType.Card` + `PaymentStatus.Pending`), which is what the live sweep,
the Stripe expiry path and this ruling all read. `OrderStatus.Pending` is a **fulfilment-axis name for
a payment-axis fact** — not a missing writer, a duplicate that was never built. Adding the writer
would create two sources of truth for one fact.

**Dead, not deleted** — the integer is on the wire to three generated clients, legacy rows may exist,
and existing readers (`SlotBlockingStatuses`, `GdprDeletionService.cs:92`) must keep tolerating it in
the conservative direction. Remove it from the offerable sets; deprecate the member; correct
`CLAUDE.md` via the docs agent.

> **⚠️ DO NOT delete `Pending` from `AdminOverrideOrderStatus.Lifecycle` — [panel amendment, CH-M5].**
> The draft said to. `Array.IndexOf` then returns **`-1`** for a legacy `Pending` row, every target
> satisfies `targetRank >= 0 > -1`, and the forward-only guard (`:101`) **passes for all of them,
> including `New` at index 0** — a **backwards** move, unlocked for exactly the rows "dead, not
> deleted" exists to protect.
>
> Root cause: **one array answering two questions** — *what rank is this status* (needs `Pending`) and
> *what may an admin target* (must exclude it). That conflation is this page's own thesis, violated by
> its own action list. **The fix:** `Lifecycle` keeps `Pending`; a **separate explicit guard** rejects
> `Pending` as a `TargetStatus` (which is the actual goal — no new writer); and `currentRank < 0`
> refuses on its own merits, so the next `OrderStatus` member forgotten in `Lifecycle` cannot re-open
> it. Pinned by a seeded test (`Pending → New` refused, `Pending → Confirmed` still allowed), not by a
> sentence telling the implementer to think about it.
>
> **Knock-on:** `TakeOrder.cs:192`'s `currentStatus is New or Pending` — **delete the `or Pending`
> arm**. It is unreachable once the gate ships. Not in tension with "readers keep tolerating
> `Pending`": that covers *conservative-direction* readers; this is a status-**write** trigger, and a
> dead branch that writes status is a trap.

### The take gate ships

`TakeOrder` is the **only** cleaner-facing order command with no status rule — `StartOrder.cs:47` and
`NotifyOnTheWay.cs:49` both gate. This closes the family's one hole.

#### The refusal is a TAXONOMY, not a key — [panel amendment, CH-M1]

The draft shipped one opaque `order.not_takeable`. But `AdminOverrideOrderStatus.Handler` — the same
partner-facing family, ten lines above the lifecycle walk — already refuses **exactly these two
statuses with exactly these two keys** (`:83-88`, `:89-94`), and both are already localized on Android
and iOS. **The gate's own "family consistency" argument applies to its vocabulary.**

| Refused because | Key | Cleaner's next move |
|---|---|---|
| `Cancelled` | `order.already_cancelled` *(exists)* | gone for good |
| `Completed` | `order.already_completed` *(exists)* | someone finished it |
| everything else | `order.not_takeable` *(new)* — **"This job is no longer available."** | the row is stale |

**The residue key stays opaque on purpose.** `order.not_yet_payable` was **rejected**: it discloses the
*customer's* payment state to a cleaner who is not a party to it, and "try again in a minute" is false
for `OnTheWay`/`InProgress` and misleading for an abandoned checkout (which never becomes payable — it
gets cancelled).

#### …and the seat ruling made the OTHER key the common one — [CH-X2]

A just-taken job is `Confirmed`, so `IsOfferable` **passes** and the refusal falls through to
`HasAvailableSpots` → `order.no_available_spots`. **The race never produces `order.not_takeable`.** At
`SpareSeatsPerOrder = 0` the modal booking has one seat, and `NewJobsDigestService.cs:62-74` pushes to
**every** approved cleaner in the country with no radius, cap or shortlist — so one job yields one
winner and N−1 identical "no available spots" toasts. **Re-voice it: "Another cleaner has already taken
this job."** across all 11 partner files. A reword, not a new key. Admin copy is **not** re-voiced
(`AdminReassignOrder.cs:95` really is out of seats, and admin has its own locales — the per-audience
seam working).

#### The bill, corrected — [CH-M6 + CH-X1 + CH-X4]

The draft's *"15 strings in 11 files, traced against `order.weekly_limit_reached`"* was traced against
a key **missing from 5 of those 11 files**: `weekly_limit_reached` is absent from **every**
partner-web locale, though Android, iOS and customer web have it. It is thrown by `TakeOrder.cs:57-58`
— so today a cleaner at their weekly cap on partner web sees *"An error occurred. Please try again."*

| Item | Files |
|---|---|
| **New** `order.not_takeable` | 11 (web ×5, Android ×5, iOS ×1 / 5 langs) |
| **Reuse** `already_cancelled` + `already_completed` | Android ✅ `:1092,1093` · iOS ✅ `:2802,2837` · **web ✗ — add both ×5** |
| **Re-voice** `order.no_available_spots` | 11 |
| **Backfill** `order.weekly_limit_reached` on partner web | 5 — *a live defect this panel found* |

**Namespaces, per client — get this wrong and it silently never resolves:** **`api.order.*`** on web
(`http-error.interceptor.ts:15`, one shared interceptor for all three apps) · **`error_order_*`**
Android · **`error.order.*`** iOS. ⚠️ The root `CLAUDE.md` says `errors.*`. **It is wrong** — routed to
the docs agent. And **a missing key is SILENT on web** (`:14-20` substitutes
`api.common.error_occurred`), visible only on Android/iOS — which is exactly how `weekly_limit_reached`
survived. **Verify by grepping the files, never by watching a screen.**

**iOS: fix the string, don't add a lookup path — [CH-X1].** `ApiErrorLocalizer.swift:29-33` resolves
**only** from `CoreL10n.bundle`, so a cleaner losing a race today reads a customer's sentence
(*"No cleaners are available for that slot. Please pick another time."*). But **no customer-facing
command emits that key** — `rg NoAvailableSpots src --type cs` → `TakeOrder.cs:45` (partner) and
`AdminReassignOrder.cs:95` (admin) only; `BookingSubmitOutcome.swift:7-10` is a doc comment. So the
string is **mis-authored, not persona-collided**: re-voice it (the same edit CH-X2 needs) and **do not**
teach the localizer to probe the app bundle — a second lookup path plus a per-app override seam,
itself needing a parity guard, for one sentence.
> **Catalog rule that replaces it:** *a key in the shared `CleansiaCore` catalog must be voiced
> correctly for **every** persona that can receive it. If two personas need different sentences for one
> key, the **backend emits two keys** — the client never branches on audience.* That preserves the
> per-audience host seam instead of pushing audience-awareness into a shared localizer.

**Partner web cannot recover from a refusal — [CH-X3, IN SCOPE].** `orders.facade.ts:207-218` has **no
error callback** and its reloads sit inside `if (response)`; `order-details.facade.ts:189-202` swallows
via `catchError(() => of(null))`. The cleaner can click the same dead job forever. Both mobile clients
reconcile correctly (Android `OrdersListViewModel.kt:355-368`, iOS `:178-184`). **The honest bill for
"the gate is ~6 lines" includes the client that has to survive the new refusal**: an error branch on
both facades + the missing `takeUntil(this.destroyed$)`.

**No migration, no NSwag regen.**

#### Rule ordering is load-bearing — and the draft's mechanism does not deliver it — [CH-M2]

The required order is unchanged:

```
NotEmpty → ExistsAsync (incl. ADR-0036 hold) → IsOfferable → HasAvailableSpots → …cleaner rules
```

`IsOfferable` precedes `HasAvailableSpots`: for a cancelled order with a free seat, "no longer
available" is honest and "no spots" is a lie. ADR-0036's narrow catalog rule (*never name **the
exclusivity***) is not violated — this key names the order's own lifecycle.

> **But `Cascade.Stop` is RULE-level and this validator has TWO chains.** FluentValidation 12.1.1
> defaults `ClassLevelCascadeMode` to `Continue`, nothing in the repo sets it, and
> `ValidationPipelineBehavior.cs:38-48` returns **every** failure, which
> `CleansiaApiController.cs:93-99` then joins under one `ErrorCode`. Placing `IsOfferable` "after
> `ExistsAsync`" orders it against three rules and leaves it unordered against six.
>
> **And it inverts ADR-0036's protection.** Chain 2 queries the real order regardless of chain 1:
> a **missing** id returns `order.not_found; order.time_conflict` (`:154` returns `false` for a null
> order), a **held** order returns a bare `order.not_found`. **Existence is inferable from the
> pairing** — precisely what folding the hold into the existence rule was built to prevent.
>
> **The fix is structural, and the enforcement is a test — neither is a comment:**
> 1. **`TakeOrder.Validator` collapses to ONE `RuleFor(x => x).Cascade(CascadeMode.Stop)` chain**, in
>    the order above. Preferred over `ClassLevelCascadeMode = Stop` (which also works) because that is
>    action at a distance — a constructor property, invisible at every rule site, re-openable by the
>    next `RuleFor`. One chain makes the load-bearing property readable where it must be read.
>    *No client impact:* clients key on the `errors` dictionary value, grouped by `ErrorCode`, never by
>    property name.
> 2. **`TC-TAKE-ONE-ERROR`** — exactly **one** error per refusal scenario, the ADR-0036 held order
>    included. This replaces the old "reviewer confirms the ordering" step, which was itself
>    comment-as-enforcement.
> 3. **One change, two fixes** — it also closes the live unresolvable-composite defect. In scope for
>    the take-gate ticket; the gate cannot be defended without it.

### The seat axis — `Q-AVAIL-01` answered (ADR-0037 §D9, owner instruction 2026-08-03)

> *"Yup, there is a possibility that he can based on the calculations of how much work there is"*

**Ruling: an order with an open seat stays offerable, whether or not a cleaner is already on it.** The
seat term is `AssignedEmployees.Count < <seat cap>`, **never** `Count == 0`. It is a **second
conjunct**, composing with the status rule and with ADR-0036's visibility rule:

```
offered(o, cleaner) ⟺ IsOfferable(o)                          -- ADR-0037 D1: is it live work?
                    ∧ o.AssignedEmployees.Count < seatCap      -- ADR-0037 D9: is there a seat?
                    ∧ OrderVisibility.NotHeldFrom(o, cleaner)  -- ADR-0036:    open to THIS cleaner?
```

**Web was right, both mobile clients were wrong.** Android `OrdersListViewModel.kt:246-251` and iOS
`OrdersListLogic.swift:76-85` send `isUnassigned: true` → `AssignedEmployees.Count == 0`
(`OrderSpecification.cs:119-122`). **A partly-staffed job has been invisible on both mobile Available
tabs since they shipped.**

**Three consequences, and two of them would otherwise be found in QA:**

| | |
|---|---|
| **A free second fix** | `GetPagedOrders.cs:58-61` applies the `-2h` `cleaningDateFrom` default **only when `HasAvailableSpots == true`**. The moment mobile sends it, the mobile-has-no-date-floor defect closes. **The separately-filed ticket is absorbed — do not work it twice.** |
| **The clients must ALSO send `excludeEmployeeId`** | `isUnassigned` excluded your own jobs *incidentally*. `hasAvailableSpots` does not, and the server's `RestrictToEmployeeId` floor is *assigned-to-me **OR** has-a-seat* (`OrderSpecification.cs:134-139`) — it deliberately does not exclude. Web already compensates (`orders.facade.ts:148`). Without it the Available tab lists jobs the cleaner is already on, and tapping one hits `TakeOrder.cs:55`. **One change, not two.** |
| **Invariant H becomes true on mobile** | ADR-0036's Invariant H is *per seat*. `isUnassigned` withheld **100% of every second seat's fill window from the whole mobile board, permanently, on every order** — a larger version of the defect ADR-0036 CH-V4 caught in its own draft. D9 is therefore a **precondition** of Invariant H holding on two of three clients, not merely compatible with it. |

**No backend change, no NSwag regen.** `Filter.HasAvailableSpots` and `Filter.ExcludeEmployeeId` are
already on the endpoint (`cleansia_android/openapi/partner-mobile-api.json:1128,1142`).

#### The seat *count* — what the owner's qualifier maps onto, and what is escalated

`RequiredEmployees = ceil(EstimatedTime / StandardWorkUnitMinutes /* 120 */)` (`Order.cs:509-519`)
**is** "the calculation of how much work there is" — `EstimatedTime` is the sum of the booked services'
estimates (`OrderFactory.cs:145-147`). **`MaxEmployees = RequiredEmployees + 1` is not.** It is a bare
`+1` with **no comment, no recorded rationale, no production caller of `SetMaxEmployees`** (four test
files only), beside an unused sibling that names the concept it isn't (`IsFullyAssigned => Count >=
RequiredEmployees`, `:118` — **read by nothing**).

**And it is not free.** `CalculateOrderPay:140-152` writes **one `OrderEmployeePay` per assigned
employee**, and `CalculateAggregatedPay:30-61` has **no crew-size term** — `basePay` is the full
per-order rate for every cleaner on it. **Each seat filled beyond `RequiredEmployees` costs a second
full labour payment against the same customer price**; on the modal booking (`EstimatedTime ≤ 120`)
that is a potential doubling of labour cost for work that needs one person.

**Ruled here (architecture):**

1. **One seat cap, a property of `Order`, read by every surface. No surface re-derives it.** Today
   that is `MaxEmployees` (`OrderSpecification.cs:126,138`, `Order.AvailableSpots:116`,
   `NewJobsDigestService.cs:101`, `Order.AddAssignedEmployee:482-491` — they already all read it).
2. **If a spare seat is wanted, it is a NAMED policy number:**
   `MaxEmployees = RequiredEmployees + BookingPolicy.SpareSeatsPerOrder` — visible, citable, tunable in
   one place, the same treatment ADR-0036 D3 gave the hold constants. **Worth doing whichever number
   wins.**

**Escalated — `Q-AVAIL-03` (business, not blocking):** exactly-the-crew (`RequiredEmployees`) or
one-spare (`RequiredEmployees + 1`, today)? **Interim: unchanged.** Because of ruling (1), flipping it
later is a one-line change in `Order.CalculateRequiredEmployees`.

### Enforcement — three layers, none of them a comment

1. **Structural.** Surfaces #1/#2/#4 stop holding literals. A set that exists once cannot disagree
   with itself. The "Mirrors" comment is **deleted**, not corrected — it has nothing left to assert.
2. **Cross-stack parity check** — the only layer that spans C#/TS/Kotlin/Swift. The *technique* is
   proven (`error-contract-parity.spec.ts:43-52` parses C# from Jest, walking up to
   `Cleansia.Api.sln`).

   > **⚠️ [PANEL AMENDMENT, CH-M7] The drafted delivery vehicle — a Jest spec under `nx affected` —
   > DOES NOT RUN on the edits it exists to catch.** A mobile-only or Domain-only diff selects **zero**
   > Nx projects (`frontend-ci.yml:86`); the push trigger is paths-scoped to `src/Cleansia.App/**`
   > (`:12-17`); `backend-ci.yml:14-19` **excludes** both mobile trees; and `nx.json`'s
   > `@nx/jest:jest` inputs are `default + ^production + jest.preset.js`, with `sharedGlobals: []` and
   > `{workspaceRoot}` = `src/Cleansia.App` — so **no file outside the Angular workspace is a declared
   > input and a cached green is reachable even when the spec is selected.** *An unenforced enforcement
   > mechanism is worse than none, because the ADR then claims coverage it lacks.*
   >
   > **It must be:** (a) a **plain Node script**, not a Jest spec, living outside the Nx workspace — so
   > it **cannot** be cached-green by construction, not merely by configuration; (b) run
   > **unconditionally** (precedent: `frontend-ci.yml:79-81`, the non-Nx "Regen-drift guard
   > self-test"); (c) triggered on **all four trees it reads** — `Cleansia.Core.Domain`, `Cleansia.App`,
   > `cleansia_android`, `cleansia_ios` — **preferably its own repo-root workflow**, so widening
   > `frontend-ci` does not spin three Angular builds on every Swift commit; (d) covering **BUTTON
   > gates as well as query literals** (surfaces 5/6/7/**9**/**10**) — a check that covers the query
   > and not the button tests the wrong half, and the drafted three-file spec would have gone **green**
   > over surface 10.
   >
   > **Acceptance:** delete one status from one client literal, push a branch touching only that file,
   > the PR goes red. **If (a)–(d) are not delivered, this layer is written down as ADVISORY.**
3. **`check-consistency.mjs`** — flag any `OrderStatus[]` literal outside `OrderAvailability.cs` that
   carries `Pending` or looks like an availability set. Heuristic backstop, not a proof.
   *Correctly Reviewer-run and not in CI by design (`process/enforcement.md:16,83,95`) — but note it
   **globs `.cs`/`.ts`/`.kt` only, no Swift** (`:17`), so it is structurally incapable of seeing the
   iOS literal and can never substitute for layer 2.*

Plus **T-0530 AC4 on one fixture — six rows, not two**: `New`+Cash one-off (offered/counted/takeable) ·
`New`+Card (not) · `New`+Cash **recurring unconfirmed** (not) · `Confirmed`+Cash recurring **`Paid`**
(offered) · `Confirmed`+Card **`Pending`** (not) · and **run `CleanupStalePendingOrders` over the
fixture and assert the card order leaves the set** — the premise the draft asserted and never tested.
And `TC-AVAIL-EQUIV` for the two evaluation forms, **with a row per new term** (including a recurring
order with NULL `CurrentStatus`; `RecurringTemplateId == null` is the only three-valued term and EF
translates it to `IS NULL`).

---

## Trade-off space (the map, kept current)

| Axis | Chosen | Live alternative | What would flip it |
|---|---|---|---|
| Rule shape | payment-qualified status **∧ not-retractable** | status-only `{New, Confirmed}` | evidence card jobs starve for want of pre-claiming |
| `New` + Card | **not offerable** | offerable | **Q-AVAIL-02** — measured time-to-first-assignment showing card starvation |
| Money term | **`PaymentType` (model) for the status term + `PaymentStatus`/`RecurringTemplateId` (progress) for the retraction term** | `PaymentType` alone (the draft) | nothing — two live sweeps falsify it in both directions (CH-M3, CH-M4) |
| `New` + Cash **recurring** | **not offerable** | offerable (the draft) | nothing — it is retracted at **T−1h**, strictly worse than the card case already refused |
| Fixing the recurring sweep instead | **no — the sweep is correct** | add a `PaymentType` term to `AutoCancelStaleRecurringOrders` | nothing — `PaymentStatus == Pending` on a recurring order *is* "customer has not confirmed"; cash included |
| Refusal vocabulary | **3 keys** — reuse `already_cancelled`/`already_completed`, one new residue key | one opaque `not_takeable` (the draft) | nothing — the family already refuses these two states with these two keys |
| Residue copy | **opaque** ("no longer available") | `not_yet_payable` | nothing — it discloses the customer's payment state and is false for most of the residue |
| Validator shape | **one ordered chain + `TC-TAKE-ONE-ERROR`** | `ClassLevelCascadeMode = Stop` + the test | acceptable equivalent; not preferred (action at a distance) |
| iOS mis-voiced copy | **re-voice the shared string** | app-bundle probe in `ApiErrorLocalizer` | evidence a customer-facing command can emit a partner key — today none can |
| Date floor | **one named constant, server-side, read by count + list** | document the count as floor-free | nothing — an unreconcilable count is the defect this page closes |
| Parity check vehicle | **plain node script, own trigger** | Jest spec under `nx affected` | nothing — it is not selected on the diffs it guards, and is cache-green when it is |
| Offer vs take | **same set, two moments** | wider take / narrower take | nothing — either re-creates a defect this ruling closes |
| Truth location | `Domain.Orders.OrderAvailability` | AppServices helper / per-surface literals | nothing — Domain is the only layer all call sites already reference |
| Evaluation | two forms + equivalence test | one shared `Expression` + `.Compile()` | a provider guarantee that SQL and C# null semantics agree — there isn't one (ADR-0036) |
| Server floor | `RestrictToEmployeeId` conjunct | floor inside `GetPagedOrders` | nothing — the latter breaks My-Completed and admin and leaves the visibility hole |
| Take gate | **ships** | comment the omission (AC3 weak branch) | nothing — ~6 lines against a verified capacity bug |
| Error key | new `order.not_takeable` | reuse `OrderNotFound` | nothing — collides with ADR-0036's deliberate reservation |
| `Pending` | deprecate, keep the member | delete it / add the writer | delete: a wire-break audit clearing three generated clients |
| Client alignment | server floor + parity test | trust + review | nothing — trust produced six lists |

## Open / undecided

- ~~**Q-AVAIL-01 (owner)** — the seat dimension.~~ **ANSWERED 2026-08-03: YES.** Ruled in ADR-0037 §D9
  → see §"The seat axis" above. Both mobile clients switch to `hasAvailableSpots` **+
  `excludeEmployeeId`**; Invariant H becomes true on mobile.
- **`Q-AVAIL-03` (owner, NEW, not blocking)** — the seat **cap**: exactly the crew the work needs
  (`RequiredEmployees`) or one spare (`RequiredEmployees + 1`, today, with no recorded rationale)? Each
  filled spare seat costs **a second full labour payment**. Interim: unchanged; the flip is one line.
- **Q-AVAIL-02 (owner)** — `New` + Card, ruled not-offerable with the flip condition named above.
- ~~**The mobile Available tab has no date floor.**~~ **ABSORBED by §D9** — the `-2h` default
  (`GetPagedOrders.cs:58-61`) fires only when `HasAvailableSpots == true`, so the client switch closes
  it. **Close the filed ticket as absorbed; do not work it twice.**
- **`dashboard.facade.ts:93-97`** — web "my upcoming" is `{Pending, Confirmed, InProgress}`: dead
  `Pending`, and **`OnTheWay` missing**, so a job vanishes from the web dashboard the moment the
  cleaner taps "On my way". Mobile uses `{Confirmed, OnTheWay, InProgress}`. The *my-orders* question.
  Filed.
- **`CLAUDE.md` Order Lifecycle is wrong** about `Pending` — docs agent, this ADR as citation. **And
  its i18n instruction is wrong too** (`errors.*`; web resolves `api.*`) — same docs ticket.
- **`StaleOrderCleanupService` + `IStaleOrderCleanupService` are dead code** with an unsatisfiable
  predicate. Delete. Filed.

### Found by the defense panel, 2026-08-03 — defects to file (NOT part of T-0530)

1. **SEVERE — every recurring CARD order appears to die ~1h after materialization.**
   `MaterializeRecurringBookings` creates it at 02:00 with `Pending` + `Card` for a slot up to 7 days
   out; `CleanupStalePendingOrders` cancels **anything** `Pending ∧ Card ∧ CreatedOn < now−1h`
   (`:50-53`) — no `OrderStatus` term, **no `RecurringTemplateId` exclusion** — every 15 min. So it dies
   ~03:15, before the reminder and before `ConfirmRecurringOrder` can be called. **Recurring card
   bookings would be structurally impossible.** Does not falsify this page (`New`+Card is not offerable
   either way), which is why it is a defect and not a blocking challenge. **Verify against DEV before
   sizing** — highest-priority item the panel produced.
2. **`CleanupStalePendingOrders` cancels SILENTLY** — `:69-79` writes `Failed` + `Cancelled` and
   dispatches **nothing**; it is the only production `Cancelled` writer that emits no
   `NotificationEventCatalog.OrderCancelled`. Both siblings do
   (`HandlePaymentNotification.cs:306-319`, `AutoCancelStaleRecurringOrders.cs:86-98`). A customer's
   booking vanishes with no message.
3. **`order.weekly_limit_reached` missing from all five partner-web locales** — thrown by
   `TakeOrder.cs:57-58`; a capped cleaner sees "An error occurred." *(Fixed inside the take-gate bill;
   listed so it is visible as pre-existing.)*
4. **Root `CLAUDE.md` names the wrong i18n namespace** — `errors.*` vs the actual `api.*`. Docs agent.
5. **Web status timeline off by one** — `order-details.helpers.ts:69-88` vs `OrderStatus.cs:8-14`:
   **Cancelled renders with the `pi pi-send` "on my way" icon**, `New` falls to the pending default,
   and the explanatory comment is factually false. **Fix in-flight** — same file as surface #10, and it
   is the screen a refused cleaner opens to find out why.
6. **`AdminOverrideOrderStatus` has no payment guard** — can push a card order to `Confirmed` while
   `PaymentStatus == Pending`. The new money conjunct means no cleaner is harmed; the state is still
   incoherent. Worth adding; **not** the fix for CH-M4.
- **Sequencing.** T-0529 → T-0530 → T-0528 all edit `NewJobsDigestService.cs`. **T-0515** (ADR-0036)
  edits four of the same surfaces — `OrderSpecification`, `CreateAvailableOrdersSpec`,
  `NewJobsDigestService`, `TakeOrder.Validator`. The two rules **compose as conjuncts**
  (offerability ∧ visibility), but they must not be written concurrently.

## Consumers

| Ticket | Carries |
|---|---|
| **T-0530** | the ruling (AC1 = ADR-0037, **panel-amended**) + AC2/AC3/AC4. **Scope grew at the panel — re-size before ticketing:** `OrderAvailability` with the **four-arg** signature; surfaces **#1/#2/#4/#5/#8/#9/#10/#11**; the take gate + **3 keys** (2 reused) + the `no_available_spots` re-voice + the partner-web `weekly_limit_reached` backfill; **`TakeOrder.Validator` collapsed to one chain** + `TC-TAKE-ONE-ERROR`; the **web reconcile** on both facades; the **date-floor constant** + web dropping its own; `AdminOverrideOrderStatus` target guard (not the `Lifecycle` delete) + its seeded test; the **plain-node parity script + its own workflow**; `TC-AVAIL-EQUIV` + the `PaymentType` exhaustiveness test; the `order-details.helpers.ts` timeline fix |
| **T-0515** (ADR-0036) | the hold conjunct on four of the same surfaces — **after** this rule exists |
| *new, PM to file* | delete `StaleOrderCleanupService` + interface |
| *new, PM to file* | `CLAUDE.md` + `docs/architecture/*` lifecycle correction (`Pending`) |
| ~~*new, PM to file*~~ | ~~the mobile Available date floor~~ — **absorbed by D9; close it** |
| *new, PM to file* | `dashboard.facade.ts` my-upcoming set (dead `Pending`, missing `OnTheWay`) |
| **new, PM to file (D9)** | **the mobile seat switch** — Android `OrdersListViewModel.kt:246-251` + `OrdersRepository.kt:50-57,205-222` and iOS `OrdersListLogic.swift:76-85` + `PartnerOrderClient.swift:83-101`: `isUnassigned: true` → `hasAvailableSpots: true` **+ `excludeEmployeeId: <own id>`**. **No backend change, no NSwag regen.** One ticket per client or one shared — but **never `hasAvailableSpots` without `excludeEmployeeId`** |
| *new, PM to file (D9.4)* | name the spare seat — `MaxEmployees = RequiredEmployees + BookingPolicy.SpareSeatsPerOrder` (worth doing whichever number `Q-AVAIL-03` returns) |
| *owner* | ~~Q-AVAIL-01~~ **answered → D9** · Q-AVAIL-02 (card pre-claim, recorded as decided) · **`Q-AVAIL-03` (the seat cap — NEW)** |

---

## 2026-08-22 — ADR-0053 and ADR-0055 (`architect` panel, both `accepted`)

**ADR-0053 — the weekly cap.** The rating ladder (3/6/10 by score, applied to everyone automatically) is
gone. `Employee.WeeklyOrderLimit` is nullable, `null` means unlimited, and that is every cleaner today;
an admin sets a number on one person through an audited command. The ladder's floor caught exactly the
wrong people — `AverageRating` defaults to 0, and 0 is below 3.5, so every newly approved cleaner was
capped at three jobs a week and could only escape by accumulating reviews they had no work to earn.

**Read the number as OUTSTANDING commitments, not "N jobs a week."** The count gained a status term and
now excludes `Completed` as well as `Cancelled`, so a finished job leaves the count. The lead recorded
this as a knowingly permissive reading rather than papering over it, and deferred a third option
(`!= Cancelled` only) with one binding condition: it must use a locally-declared `WeekConsumingStatuses`,
**never** a borrowed `SlotBlockingStatuses` — the overlap question and the cap question are not the same
question and must not share a set.

**The supersession is narrower than the draft claimed.** The lead ruled ADR-0053's supersession of
**ADR-0036 A6 vacuous AND over-broad, and dropped it in full**: A6 says only that the cap and the
conflict are *dynamic*, states no universality premise, and `adr-0036.md:1931-1938` is headed "What is
NOT superseded". The prohibition on consulting the cap from the hold resolver rests on the `UtcNow.Date`
window, which this change left byte-identical. **One edge only: ADR-0053 → ADR-0037 Fact 3, cap half.**

**ADR-0055 — the start grace window.** `StartOrder` and `NotifyOnTheWay` refuse a job more than 60
minutes ahead (`order.too_early_to_start`); late is never blocked, and the rule is last on both chains so
an unassigned caller learns nothing about the schedule. Before this there was no clock gate at all — a
cleaner could mark next Tuesday's job started today, putting "your cleaner is on the way" on a customer's
lock screen days early.

**The margin is five minutes and it comes from the cron's phase, not from the constant.** The customer's
own notice is only guaranteed by T-65 (window high 70, minus a widest cron gap of 5). Widen the sweep's
cron or narrow its window and the guarantee evaporates with nothing failing — so it is now pinned in
`TimerScheduleConfigTests`, against operands that test already computed. That assertion was the one
relation in the whole argument nobody had written.
