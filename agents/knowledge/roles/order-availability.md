# Role — `OrderAvailability` (CRC card)

> **PROPOSED — not yet the standard.** Introduced by **ADR-0037**
> (`agents/backlog/adr/0037-order-offerability-is-a-payment-qualified-status-rule-owned-by-the-domain-and-enforced-at-the-take.md`),
> `proposed` 2026-08-02, **not yet challenged**. Living companion:
> `agents/architecture/decisions/order-availability.md`. **Composes with — does not replace —
> `OrderVisibility`** (ADR-0036, `roles/preferred-cleaner-hold-resolver.md`): the two are separate
> conjuncts on the same surfaces. Read `TakeOrder.cs`, `OrderSpecification.cs:134-139` and
> `NewJobsDigestService.cs:52-53` in full before changing anything here.

`Cleansia.Core.Domain.Orders.OrderAvailability` — a static domain policy. No state, no I/O, no DI.

## Responsibility (one sentence)

Be the **one place** that answers *"is this order, in itself, work a cleaner may be offered and may
take?"* — a predicate over the **order alone**, combining the fulfilment axis (`CurrentStatus`) with
the money axis (`PaymentType`), in **two evaluation forms that a test proves equal**.

```
Offerable(o) ⟺ o.CurrentStatus == Confirmed
             ∨ (o.CurrentStatus == New ∧ o.PaymentType == Cash)
```

| Member | Job |
|---|---|
| `OfferableStatuses` = `[New, Confirmed]` | the **coarse** fulfilment floor — the index-served prefilter on `Orders.CurrentStatus`, and the thing the three clients mirror. **Not the rule** — `New` is conditional. |
| `IsOfferableSql` : `Expression<Func<Order,bool>>` | queryable form, composed into `OrderSpecification` |
| `IsOfferable(OrderStatus?, PaymentType)` | in-memory form, for the `TakeOrder` write gate |

**Cash is not an exception — it is the rule with an empty money axis.** A future `PaymentType` answers
"can this still retract the order?" and drops in without touching a surface.

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
- **Payment state.** It reads `PaymentType` (the money *model*), never `PaymentStatus` (the money
  *progress*). Every order is created `PaymentStatus.Pending` (`OrderFactory.cs:116`) and a cash order
  never leaves it — so a `PaymentStatus` term would either admit abandoned cards or exclude all cash.
- **When an abandoned card order dies.** That is `CleanupStalePendingOrders` (15-min timer, 1 h
  threshold, keyed on `PaymentStatus.Pending && PaymentType == Card`). Availability declines to offer
  such orders; it does not cancel them.
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
7. **Validator order in `TakeOrder`:** `NotEmpty → ExistsAsync (incl. the ADR-0036 hold) → IsOfferable
   → HasAvailableSpots`. Placing `IsOfferable` before the hold check leaks `order.not_takeable` for a
   **held** order and reveals it exists and is live — the exact inference ADR-0036 forbids.
8. **`order.not_takeable` resolves in all 11 locale files** (web partner ×5, Android partner ×5, iOS
   `Localizable.xcstrings` ×1 file / 5 languages). `error-contract-parity.spec.ts` covers the
   **customer** app only (`:27-30`) and will not catch a partner miss.
9. **The cross-stack parity spec exists** and fails when any one client's Available literal is edited
   alone. A comment claiming the clients agree is **not** enforcement — that claim is what shipped six
   different lists.

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
