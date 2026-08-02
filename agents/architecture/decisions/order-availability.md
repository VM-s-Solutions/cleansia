# Order availability — which orders a cleaner may be offered, and which they may take

> **Status of this page: PROPOSED SHAPE.**
> **[ADR-0037](../../backlog/adr/0037-order-offerability-is-a-payment-qualified-status-rule-owned-by-the-domain-and-enforced-at-the-take.md)**
> is **`proposed`** (2026-08-02, author mode, for **T-0530 AC1**) and has **not yet been challenged**.
> The ADR is the record; **this page is the evolving companion and is what you read first.**
> **Nothing here is binding until a lead declares consensus** (`../../process/deliberation.md`).
>
> Companion pages: [`preferred-cleaner-dispatch.md`](./preferred-cleaner-dispatch.md) (ADR-0036 — the
> hold predicate this composes with, as a separate conjunct on the same six surfaces),
> [`push-notifications.md`](./push-notifications.md) (ADR-0025 — the digest's display contract),
> [`mobile-result-contract.md`](./mobile-result-contract.md) (ADR-0011 — how the new error key reaches
> the clients). Published view: `docs/architecture/backend.md`.

---

## Today (shipped, verified 2026-08-02 — every row read, not inherited)

**Eight surfaces answer one question and no two of them agree.**

| # | Surface | Today | Kind |
|---|---|---|---|
| 1 | `NewJobsDigestService.cs:52-53` | `{New, Pending, Confirmed}` | push |
| 2 | `DashboardSpecifications.cs:24` | `{Pending, Confirmed}` | count (`GetDashboardStats.cs:236`) + preview (`GetAvailableJobsPreview.cs:50`) |
| 3 | `GetPagedOrders.cs:87` | client-supplied, **no server floor** | list |
| 4 | `OrderSpecification.cs:134-139` | **seat arithmetic, status-blind** | **server-side visibility** |
| 5 | `orders.facade.ts:142-146` | `{New, Pending, Confirmed}` | web display |
| 6 | `OrdersListViewModel.kt:248` | `{New, Confirmed}` | Android display |
| 7 | `OrdersListLogic.swift:78` | `{New, Confirmed}` | iOS display |
| 8 | `TakeOrder.Validator:38-60` | **no status rule** | write gate |

`NewJobsDigestService.cs:49-50` claims to mirror #2. It does not, and they disagree on the first term.
`GetAvailableJobsPreview.cs:46-49` claims to match the mobile tab. It does not either — **a third
false mirror**, found while verifying.

**Three properties of the shipped system that make this more than untidiness:**

- **`OrderStatus.Pending` has no production writer.** Thirteen `AddOrderStatus` call sites write
  `New`/`Confirmed`/`Cancelled`/`OnTheWay`/`InProgress`/`Completed`; only the generic
  `AdminOverrideOrderStatus.cs:108` *could* write `Pending`. So `{Pending, Confirmed}` ≡ `{Confirmed}`.
- **A cash order stays `New` forever.** `OrderPaymentDispatcher.cs:59-69` writes no status track, so
  nothing confirms a cash order except a cleaner taking it (`TakeOrder.cs:192-194`). The partner
  dashboard's available count is **structurally zero** for a pipeline of cash orders — beside a list
  that shows them.
- **A `Cancelled`/`Completed` order with a free seat is takeable right now.** `TakeOrder`'s only
  order-side gates are `ExistsAsync` and `HasAvailableSpots`; `MaxEmployees = RequiredEmployees + 1`
  (`Order.cs:519`) so a seat nearly always exists. The assignment lands, burns one of the cleaner's
  3/6/10 weekly slots (`OrderRepository.cs:254-258` — **no status filter**) and does not even block
  their calendar (`SlotBlockingStatuses` excludes terminal statuses). The path is a stale client, not
  malice.

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

> **An order is offerable iff its fulfilment axis is pre-work with a free seat, and its money axis has
> already reached the state that taking it assumes.**

```
Offerable(o) ⟺ o.CurrentStatus == Confirmed
             ∨ (o.CurrentStatus == New ∧ o.PaymentType == Cash)
```

| Status | Offerable | Why |
|---|---|---|
| `New` + Cash | **yes** | Nothing in flight. The take *is* the confirmation. |
| `New` + Card | **no** | Checkout open/abandoned; cancelled within ~1h15m. |
| `Pending` | **no** | Dead status — no writer. |
| `Confirmed` | **yes** | Card: money settled in the same commit as the status. Cash: spare seat. |
| `OnTheWay`, `InProgress` | **no** | Work started. **No surface includes these** — uncontested. |
| `Completed`, `Cancelled` | **no** | Terminal. |

**Cash is not an exception to the rule — it is the rule with an empty money axis.** That framing is
what makes the ruling generalize: a future `PaymentType` (invoice, corporate account) answers "when
can this retract the order?" and drops into the same predicate without touching any surface.

**Why `PaymentType` and not `PaymentStatus`.** Every order is created `PaymentStatus.Pending`
(`OrderFactory.cs:116`) and a cash order **never leaves it** until collection. So `!= Failed` admits
abandoned card orders for the whole window, and `== Paid` excludes **every cash order in the system**.
`PaymentType` is the only term that separates the two money models.

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
- `IsOfferable(OrderStatus?, PaymentType)` — in-memory, for the write gate.

**Two forms, pinned by `TC-AVAIL-EQUIV` against real PostgreSQL — not one shared expression.** This
follows ADR-0036's ruling verbatim (`preferred-cleaner-dispatch.md:107-109`): SQL and C# disagree on
null semantics and `.Compile()` on a request path is banned. Our predicate has the same `NULL` hazard.

**NULL `CurrentStatus`:** reads fail closed (`OrderSpecification.cs:115-116`); the **take must not**,
or every legacy order becomes permanently untakeable. It resolves the way `HasOverlappingOrderAsync`
already does (`OrderRepository.cs:285-288`) — column when non-null, else latest history by
`(CreatedOn desc, Sequence desc)`. This also removes a latent NRE: `OrderMappers.cs:14-17` is
`CurrentStatus!.Value` and `TakeOrder.cs:191` dereferences it on the request path.

### What each surface becomes

| # | Verdict | Change |
|---|---|---|
| 1 | wrong | delete the array **and the comment**; compose `IsOfferableSql` |
| 2 | wrong | `OfferableStatuses` + the payment qualifier; fixes count **and** preview; delete the false comment at `GetAvailableJobsPreview.cs:46-49` |
| 3 | wrong, **fixed at #4** | no direct change — a blanket floor here breaks My-Completed and admin |
| 4 | **wrong — the one that matters** | `assigned-to-me OR (has-free-seat AND offerable)` |
| 5 | wrong | drop dead `Pending` → `[New, Confirmed]` |
| 6, 7 | **right** | unchanged |
| 8 | wrong | gains the gate |

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
the conservative direction. Remove it from the offerable sets **and from
`AdminOverrideOrderStatus.cs:56-64`** so no new writer can appear; deprecate the member; correct
`CLAUDE.md` via the docs agent.

### The take gate ships

`TakeOrder` is the **only** cleaner-facing order command with no status rule — `StartOrder.cs:47` and
`NotifyOnTheWay.cs:49` both gate. This closes the family's one hole.

**The bill, stated so the implementer does not discover it:** new key
`OrderNotTakeable = "order.not_takeable"`, and **15 strings in 11 files** (web partner 5, Android
partner 5, iOS `Localizable.xcstrings` 1 file × 5 languages) — the "five locales" figure is one
client's share. `error-contract-parity.spec.ts` guards the **customer** app only (`:27-30`), so it
will not catch a partner miss. **No migration, no NSwag regen.**

**Rule ordering is load-bearing** (ADR-0036 interaction). The hold refusal lives inside the existence
rule and returns `OrderNotFound` so exclusivity cannot be inferred; the status rule must come **after**
it, or a held order would return `order.not_takeable` and reveal that it exists and is live:

```
NotEmpty → ExistsAsync (incl. ADR-0036 hold) → IsOfferable → HasAvailableSpots
```

`IsOfferable` precedes `HasAvailableSpots`: for a cancelled order with a free seat, "no longer
available" is honest and "no spots" is a lie. ADR-0036's narrow catalog rule (*never name **the
exclusivity***) is not violated — this key names the order's own lifecycle.

### Enforcement — three layers, none of them a comment

1. **Structural.** Surfaces #1/#2/#4 stop holding literals. A set that exists once cannot disagree
   with itself. The "Mirrors" comment is **deleted**, not corrected — it has nothing left to assert.
2. **Cross-stack parity test** — the only layer that spans C#/TS/Kotlin/Swift. Precedent already
   works: `error-contract-parity.spec.ts:43-52` parses C# source from Jest and walks up to
   `Cleansia.Api.sln` (`:9-20`). New `available-status-parity.spec.ts` asserts the three clients'
   Available literals equal `OfferableStatuses`.
3. **`check-consistency.mjs`** — flag any `OrderStatus[]` literal outside `OrderAvailability.cs` that
   carries `Pending` or looks like an availability set. Heuristic backstop, not a proof.

Plus T-0530 AC4's two tests on one fixture, and `TC-AVAIL-EQUIV` for the two evaluation forms.

---

## Trade-off space (the map, kept current)

| Axis | Chosen | Live alternative | What would flip it |
|---|---|---|---|
| Rule shape | payment-qualified status | status-only `{New, Confirmed}` | evidence card jobs starve for want of pre-claiming |
| `New` + Card | **not offerable** | offerable | **Q-AVAIL-02** — measured time-to-first-assignment showing card starvation |
| Money term | `PaymentType` | `PaymentStatus != Failed` / `== Paid` | nothing — one admits abandoned cards, the other kills cash entirely |
| Offer vs take | **same set, two moments** | wider take / narrower take | nothing — either re-creates a defect this ruling closes |
| Truth location | `Domain.Orders.OrderAvailability` | AppServices helper / per-surface literals | nothing — Domain is the only layer all call sites already reference |
| Evaluation | two forms + equivalence test | one shared `Expression` + `.Compile()` | a provider guarantee that SQL and C# null semantics agree — there isn't one (ADR-0036) |
| Server floor | `RestrictToEmployeeId` conjunct | floor inside `GetPagedOrders` | nothing — the latter breaks My-Completed and admin and leaves the visibility hole |
| Take gate | **ships** | comment the omission (AC3 weak branch) | nothing — ~6 lines against a verified capacity bug |
| Error key | new `order.not_takeable` | reuse `OrderNotFound` | nothing — collides with ADR-0036's deliberate reservation |
| `Pending` | deprecate, keep the member | delete it / add the writer | delete: a wire-break audit clearing three generated clients |
| Client alignment | server floor + parity test | trust + review | nothing — trust produced six lists |

## Open / undecided

- **Q-AVAIL-01 (owner)** — **the seat dimension.** Web sends `hasAvailableSpots: true`
  (`orders.facade.ts:147`); Android (`:249`) and iOS (`:79`) send `isUnassigned: true`. A 2-cleaner job
  with 1 cleaner on it **is** offered on web and **is not** on mobile. Product question; interacts with
  ADR-0036's per-seat Invariant H. This ADR rules the **status** axis only.
- **Q-AVAIL-02 (owner)** — `New` + Card, ruled not-offerable with the flip condition named above.
- **The mobile Available tab has no date floor.** `GetPagedOrders.cs:58-61` applies the `-2h` default
  only when `HasAvailableSpots == true`; mobile sends `isUnassigned` instead, so it lists past-dated
  jobs. Same root cause, different predicate. Filed.
- **`dashboard.facade.ts:93-97`** — web "my upcoming" is `{Pending, Confirmed, InProgress}`: dead
  `Pending`, and **`OnTheWay` missing**, so a job vanishes from the web dashboard the moment the
  cleaner taps "On my way". Mobile uses `{Confirmed, OnTheWay, InProgress}`. The *my-orders* question.
  Filed.
- **`CLAUDE.md` Order Lifecycle is wrong** about `Pending` — docs agent, this ADR as citation.
- **`StaleOrderCleanupService` + `IStaleOrderCleanupService` are dead code** with an unsatisfiable
  predicate. Delete. Filed.
- **Sequencing.** T-0529 → T-0530 → T-0528 all edit `NewJobsDigestService.cs`. **T-0515** (ADR-0036)
  edits four of the same surfaces — `OrderSpecification`, `CreateAvailableOrdersSpec`,
  `NewJobsDigestService`, `TakeOrder.Validator`. The two rules **compose as conjuncts**
  (offerability ∧ visibility), but they must not be written concurrently.

## Consumers

| Ticket | Carries |
|---|---|
| **T-0530** | the ruling (AC1 = ADR-0037) + AC2/AC3/AC4: `OrderAvailability`, surfaces #1/#2/#4/#5/#8, the take gate + key + 15 strings, the parity spec, `TC-AVAIL-EQUIV` |
| **T-0515** (ADR-0036) | the hold conjunct on four of the same surfaces — **after** this rule exists |
| *new, PM to file* | delete `StaleOrderCleanupService` + interface |
| *new, PM to file* | `CLAUDE.md` + `docs/architecture/*` lifecycle correction (`Pending`) |
| *new, PM to file* | the mobile Available date floor |
| *new, PM to file* | `dashboard.facade.ts` my-upcoming set (dead `Pending`, missing `OnTheWay`) |
| *owner* | Q-AVAIL-01 (seat dimension), Q-AVAIL-02 (card pre-claim, recorded as decided) |
