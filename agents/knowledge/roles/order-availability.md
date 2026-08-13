# Role — `OrderAvailability` (CRC card)

> **THE STANDARD.** Introduced by **ADR-0037**
> (`docs/decisions/adr-0037.md`),
> **`accepted` 2026-08-03** after a defense panel (19 findings, 8 blocking, all resolved). Living
> companion: `agents/architecture/decisions/order-availability.md`. **Composes with — does not
> replace — `OrderVisibility`** (ADR-0036, `roles/preferred-cleaner-hold-resolver.md`): the two are
> separate conjuncts on the same surfaces. Read `TakeOrder.cs`, `OrderSpecification.cs:134-139` and
> `NewJobsDigestService.cs:52-53` in full before changing anything here.
>
> ⚠️ **The panel changed this card's responsibility.** The role now reads **`PaymentStatus` and
> `RecurringTemplateId`**, which the draft explicitly listed under *does NOT know*. See §Responsibility
> and the struck bullet below — the change is the RDD rule working, not an exception to it.

`Cleansia.Core.Domain.Orders.OrderAvailability` — a static domain policy. No state, no I/O, no DI.

## Responsibility (one sentence)

Be the **one place** that answers *"is this order, in itself, work a cleaner may be offered and may
take?"* — a predicate over the **order alone**, combining the fulfilment axis (`CurrentStatus`) with
the money axis, in **two evaluation forms that a test proves equal**.

> **Sharpened by the panel:** the question is **not** *"what money model is this?"* — it is
> **"can anything take this order away from the cleaner I hand it to?"** That is why the role reads
> the money **progress**, not only the money **model**.

```
Offerable(o) ⟺ ( o.CurrentStatus == Confirmed
               ∨ (o.CurrentStatus == New ∧ o.PaymentType == Cash) )
             ∧ NotRetractable(o)

NotRetractable(o) ⟺ o.PaymentStatus == Paid
                  ∨ (o.PaymentType == Cash ∧ o.RecurringTemplateId == null)
```

| Member | Job |
|---|---|
| `OfferableStatuses` = `[New, Confirmed]` | the **coarse** fulfilment floor — the index-served prefilter on `Orders.CurrentStatus`, and the thing the clients mirror. **Not the rule** — `New` is conditional. |
| `IsOfferableSql` : `Expression<Func<Order,bool>>` | queryable form, composed into `OrderSpecification` |
| `IsOfferable(OrderStatus?, PaymentType, PaymentStatus, string? recurringTemplateId)` | in-memory form, for the `TakeOrder` write gate. **Four scalars, all columns on `Order`** — no navigation properties, no I/O, no collaborator. |

**`NotRetractable` is the union of the negations of the two sweeps that actually run**, read off their
own `WHERE` clauses — `CleanupStalePendingOrders.cs:50-53` (**no `OrderStatus` term**) and
`AutoCancelStaleRecurringOrders.cs:63-69` (**no `PaymentType` term**). If a third scheduled retractor
is ever added, **this term is where it must be reflected** — a sweep whose predicate is not negated
here silently re-creates the defect the panel caught.

**Cash is not an exception — it is the rule with an empty *pre-work* money axis.** A future
`PaymentType` must be classified on **both** axes (offerable at `New`? retractable by which sweep?) and
an exhaustiveness test over `Enum.GetValues<PaymentType>()` goes red until it is.

## Collaborators

- **`OrderStatus` / `PaymentType`** (`Domain.Enums`) — the two axes. Nothing else.
- **`OrderSpecification`** — composes `IsOfferableSql` into `RestrictToEmployeeId`'s free-seat
  disjunct: `assigned-to-me OR (has-free-seat AND offerable)`. This is the **server's authoritative
  floor** for every non-admin browse.
- Consumers, by **kind** (all four must be call-site-verified, never grep-counted):
  | Kind | Where |
  |---|---|
  | server-side visibility | `OrderSpecification.cs:134-139` → `GetPagedOrders.cs:91` |
  | count + preview | `DashboardSpecifications.CreateAvailableOrdersSpec` → `GetDashboardStats.cs:236` **and** `GetAvailableJobsPreview.cs:50` |
  | notification | `NewJobsDigestService` — as a **conjunct**, alongside its own freshness rule |
  | write gate | `TakeOrder.Validator`, **after** `ExistsAsync`, **before** `HasAvailableSpots` |
- **`OrderVisibility`** (ADR-0036) — the *other* conjunct. Availability asks "is this order live work?";
  visibility asks "is it open to *this* cleaner right now?". **Neither knows the other's rule.**

## Does NOT know

- **Anything about a cleaner.** Approval, profile completeness, weekly cap, time conflict,
  already-assigned, work country, the ADR-0036 hold — all are properties of the **(cleaner, order)
  pair** and stay in `TakeOrder` / the per-surface filters. If a scenario forces this role to take an
  employee id, **the responsibility is wrong** and the caller wants `OrderVisibility` or an eligibility
  gate instead.
- **How many seats are free.** `HasAvailableSpots` is `Order`'s (`Order.cs:116-117`). Availability is
  about *liveness*, not capacity; they are separate conjuncts and the take gate evaluates availability
  **first** so a cancelled order with a free seat reports the honest reason.
- **Whether an order occupies a cleaner's calendar.** That is `SlotBlockingStatuses`
  (`OrderRepository.cs:263-270`) and it is a **different set for a different question** — it correctly
  includes `OnTheWay`/`InProgress`, which are never offerable. Do not unify them.
- **Which statuses a cleaner's *own* list shows.** My-Active and My-Completed are the my-orders
  question. A cleaner must always see their own terminal orders; availability must never floor them.
- **How to write a status.** It reads `CurrentStatus`; it never appends a track. `TakeOrder.cs:192-194`
  remains the only path from `New` to `Confirmed` on a take.
- ~~**Payment state.** It reads `PaymentType` (the money *model*), never `PaymentStatus` (the money
  *progress*)…~~ **STRUCK 2026-08-03 by the ADR-0037 panel (CH-M3, CH-M4).** This bullet was wrong,
  and it is the textbook case the RDD rule exists to catch: *if a scenario forces a role to know
  something on its "does NOT know" list, the responsibility is wrong or a collaborator is missing.*
  Two scenarios did — a recurring cash order awaiting the customer's confirm, and a `Confirmed` card
  order an admin pushed forward — and **no collaborator was missing**, so the responsibility was
  wrong. The role reads `PaymentStatus` and `RecurringTemplateId`. *The draft's reasoning ("a
  `PaymentStatus` term would either admit abandoned cards or exclude all cash") is sound about
  `PaymentStatus` as a **whole rule** and says nothing about it as a **conjunct** — which is what
  ships.*
- **When an abandoned card order dies, or who cancels it.** That is `CleanupStalePendingOrders` /
  `AutoCancelStaleRecurringOrders`. Availability **negates their predicates** so it never offers a
  doomed order; it never cancels one, never schedules one, and never reads a clock.
- **Which tenant.** The global query filter scopes every read that composes it.

## Invariants a reviewer checks

1. **No availability status literal exists outside this class.** `NewJobsDigestService`'s
   `AvailableStatuses` array is **deleted**, not edited — and so is its "Mirrors
   `DashboardSpecifications`" comment. A comment asserting agreement between two things that are now
   one thing is the defect T-0530 exists to kill.
2. **`OrderStatus.Pending` appears in no availability set**, and is gone from
   `AdminOverrideOrderStatus.cs:56-64` so no new writer can appear. Readers that *tolerate* legacy
   `Pending` rows (`SlotBlockingStatuses`, `GdprDeletionService.cs:92`) keep doing so — the
   conservative direction.
3. **Call sites, not hit counts.** `OrderSpecification.Create`'s parameters are all optional, so a
   caller that omits the new argument **compiles green and leaks** (ADR-0036's trap #3). Verify
   `CreateAvailableOrdersSpec`'s **both** callers.
4. **`ExcludeEmployeeId` is untouched** — opposite polarity; never reuse it for this (ADR-0036 trap #2).
5. **`TC-AVAIL-EQUIV` exists and runs against PostgreSQL.** Two forms, pinned by a test, never one
   shared tree. **No `.Compile()` on a request path** (ADR-0036 D — SQL and C# disagree on null).
6. **NULL `CurrentStatus` is total in both forms.** Reads fail closed; **the take must not** — it
   resolves via `CurrentStatus ?? latest history (CreatedOn desc, Sequence desc)`, matching
   `OrderRepository.cs:285-288`. A bare `CurrentStatus!.Value` on a request path is a finding
   (`OrderMappers.cs:14-17` is one today, reached from `TakeOrder.cs:191`).
   > ⚠️ **PENDING SUPERSESSION —
   > [ADR-0040](../../backlog/adr/0040-order-currentstatus-is-non-nullable-the-pre-backfill-population-it-defends-does-not-exist.md)
   > (`proposed`, 2026-08-04) makes the column `NOT NULL`.** Do **not** raise this item as a review
   > finding against work that deletes the fallback. On acceptance, item 6 becomes: *`CurrentStatus`
   > is non-nullable; a `??`, a `!.Value` or a `!= null` on it is the finding.* Until then, both the
   > old rule and its removal are defensible — check which ADR the ticket cites.
7. **`TakeOrder.Validator` is ONE `RuleFor` chain** (`Cascade(CascadeMode.Stop)`), in the order
   `NotEmpty → ExistsAsync (incl. the ADR-0036 hold) → IsOfferable → HasAvailableSpots → …cleaner
   rules`. **A second `RuleFor` in that file is a hard reject** — `ClassLevelCascadeMode` defaults to
   `Continue`, both chains run, and the failures are semicolon-joined into an unresolvable composite.
   **Enforced by `TC-TAKE-ONE-ERROR`** (exactly one error per refusal scenario, held order included),
   **not** by reading the code. Placing `IsOfferable` before the hold check, or letting a second chain
   run beside it, leaks the fact that a **held** order exists and is live — the exact inference
   ADR-0036 forbids, and it leaks via the *pairing* of keys, not via any one key.
8. **All three refusal keys resolve, per client namespace** — `order.not_takeable` (new),
   `order.already_cancelled`, `order.already_completed`. **Grep the files; do not watch the screen** —
   a missing key on web renders `api.common.error_occurred`, indistinguishable from a 500
   (`http-error.interceptor.ts:14-20`). Namespaces: **`api.order.*`** web · **`error_order_*`** Android
   · **`error.order.*`** iOS. (`error-contract-parity.spec.ts` covers the **customer** app only,
   `:27-30`.)
9. **The cross-stack parity check exists, RUNS, and covers BUTTON gates.** It is a **plain Node
   script with its own trigger**, not a Jest spec under `nx affected` (which is not selected on
   mobile-only diffs and is cache-green when it is). It asserts surfaces **5, 6, 7, 9 and 10** — the
   query decides what is *listed*, the button decides what is *clickable*, and a check covering only
   the query tests the wrong half. Acceptance: flip one client literal, push a branch touching only
   that file, the PR goes red.

## Watch-list

- ~~**The seat dimension is unresolved** (`Q-AVAIL-01`).~~ **ANSWERED 2026-08-03 by the owner: YES, a
  partly-staffed job stays offerable** — ADR-0037 **§D9**. It lands exactly where this card predicted:
  a **second conjunct** beside the status rule, never folded into it. Three things the implementer must
  carry, or the change ships a new bug:
  1. Android `OrdersListViewModel.kt:246-251` and iOS `OrdersListLogic.swift:76-85` switch
     `isUnassigned: true` → `hasAvailableSpots: true`.
  2. **…and must ALSO send `excludeEmployeeId: <own id>`.** `isUnassigned` excluded your own jobs
     incidentally; `hasAvailableSpots` does not, and `RestrictToEmployeeId` is *assigned-to-me **OR**
     has-a-seat* (`OrderSpecification.cs:134-139`) — it deliberately does not exclude. Web already
     compensates (`orders.facade.ts:148`). **Never one without the other.**
  3. It **closes the mobile date-floor defect for free** (`GetPagedOrders.cs:58-61` applies the `-2h`
     default only when `HasAvailableSpots == true`). The separately-filed ticket is **absorbed**.
  **No backend change, no NSwag regen** — both parameters already exist on the endpoint
  (`cleansia_android/openapi/partner-mobile-api.json:1128,1142`). And it makes ADR-0036's per-seat
  Invariant H **true on mobile**, where `isUnassigned` had been withholding 100% of every second seat's
  fill window from the whole mobile board, permanently.
- **The seat CAP is a different question and is ANSWERED** (`Q-AVAIL-03`, owner, 2026-08-03):
  **seats = `RequiredEmployees`. No spare seat.** `RequiredEmployees = ceil(EstimatedTime / 120)` is the
  work-derived number and `MaxEmployees = RequiredEmployees + BookingPolicy.SpareSeatsPerOrder` with the
  spare at **0** — the constant stays so the number is citable and tunable in one edit. The old `+1`
  cost **a second full labour payment per filled spare seat** (`CalculateOrderPay:140-152` writes one pay
  row per assigned employee; `CalculateAggregatedPay:30-61` has no crew-size term) against an unchanged
  customer price. The standing rule is unchanged and is what made the flip cheap: **there is ONE seat
  cap, it is a property of `Order`, every surface reads it, and no surface re-derives it.** A long job
  still carries several seats; only the extra one is gone. `Order.IsFullyAssigned` — which denoted the
  same predicate as `HasAvailableSpots` once the cap equalled the requirement, and was read by nothing —
  is deleted.
- **A third conjunct on these surfaces is a design smell.** Availability (ADR-0037) and visibility
  (ADR-0036) already both ride `OrderSpecification`, `CreateAvailableOrdersSpec`,
  `NewJobsDigestService` and `TakeOrder`. A **third** should trigger a look at composing them into one
  named board predicate rather than a fourth `if` block in `OrderSpecification`.
- **If a country ever varies what is offerable**, it belongs here reading `CountryConfiguration` (the
  ADR-0017 seam) — **never** a country-code branch in a handler.
- **`OfferableStatuses` is the coarse floor and will be misread as the rule.** It exists only because
  clients cannot evaluate the payment term and because SQL wants an indexable prefilter. Any backend
  code using it *without* the payment qualifier is a finding.
