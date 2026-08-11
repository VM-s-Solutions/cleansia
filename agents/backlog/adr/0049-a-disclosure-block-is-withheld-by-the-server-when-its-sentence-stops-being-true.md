# ADR-0049 — A **disclosure block** is withheld by the **server** when its sentence stops being true; the client renders it off the block's own arrival

- **Status:** `proposed` — architect, 2026-08-11. **The author does not accept their own ADR**
  (`adr/README.md`); a lead rules and the PM stamps.
- **Date:** 2026-08-11
- **Mode:** **author**, with an author-run self-challenge (`## Challenge` below). No independent
  challenger has run. Written to unblock **T-0595**, whose premise this ADR partially **refutes** —
  see §Context, "What T-0595 gets right, and the half it cannot fix".
- **Number:** **0049**, allocated 2026-08-11. The highest on disk was 0048.
- **Supersedes:** nothing. **Amends** ADR-0045 §D7.2 by adding a *presence* rule to a block whose
  *content* rules that section already fixed; ADR-0045's four fields, their meanings and
  `canChooseAnother`'s seven terms are **unchanged**.
- **Narrows:** `patterns-mobile.md` §*"The redaction narrowing of rule (1) — the discriminator is the
  field's own ARRIVAL (ADR-0047)"* — from a **field**'s arrival to a composite **block**'s arrival,
  and it names who decides the arrival.
- **Routing (ADR-0033):** **test 1 fires** — the rule puts shipped code in violation
  (`GetOrderDetails.cs:146-173` sends the block on a concluded booking). **Test 2 also fires** — a
  catalog sentence already governs this subject at a covering generality (`patterns-mobile.md`
  §*"render the discriminator, never re-derive it"*), and this entry carves the composite case out of
  it.
- **Living doc:** `agents/architecture/decisions/preferred-cleaner-dispatch.md`
- **Tickets it feeds:** **T-0595** (backend + web, one change), plus the two follow-ups named in §D6.

> ### ⚠️ Method declaration
> **No shell.** `Read` / `Glob` / `Grep` / `Write` / `Edit` only. Nothing was compiled, executed,
> measured or run. No test outcome, build result, timing or count-from-a-tool is claimed anywhere
> below. Every `file:line` is a line this author opened at HEAD on 2026-08-11. Two things I would want
> a command for and therefore do **not** assert: (a) that no *other* consumer of
> `PreferredOffer.StateOf` exists beyond the one I found by grep, and (b) anything about how the
> proposed tests behave.

---

## Context

`PreferredOffer.StateOf` (`src/Cleansia.Core.Domain/Orders/PreferredOffer.cs:36-53`) derives four
states from four columns — beneficiary id, hold deadline, "is the beneficiary assigned", and now. It
takes **no order status**, deliberately: ADR-0045 §D7.1 made the state derived rather than stored so
it could not go stale.

The customer's order detail renders one sentence per state. The `Closed` one
(`apps/cleansia.app/src/assets/i18n/en.json:1740-1741`) is:

> **Open to all cleaners** — *"The request for the cleaner you asked for has ended. This booking is now
> open to our whole team."*

That sentence makes a **forward-looking claim about the platform**. `StateOf` has no input that can
make it stop being true, and nothing downstream withholds it: the web facade's `visible()` is
`state !== None || canChooseAnother()` (`order-preferred-offer.facade.ts:61-63`) and reads no status,
so it renders on every order the customer ever named a cleaner for, for ever.

### What T-0595 gets right, and the half it cannot fix

T-0595 names **two** harms. Only one of them is about past bookings.

1. *"an order a **different** cleaner took stays `Closed` forever"* — that order is `Confirmed` with an
   assignee. It is not past, not cancelled, not completed.
2. *"the customer sees it on every past order … cancelled ones included."*

**A status grouping — with any membership anyone has proposed — fixes (2) and cannot fix (1).** Every
candidate contains `Confirmed`: iOS's `isUpcoming` is `status != ._5 && status != ._6`
(`OrderStatusMapping.swift:37-40`), so on a `Confirmed`, fully-staffed booking it returns `true` and
`PreferredOfferPresentation.disclosure` (`PreferredOfferPresentation.swift:23-24`) still produces
`.closed` — which `OrderDetailContent.swift:29-30` renders with no further gate. **The fix T-0595
holds up as the model has harm (1) too.** Its own tests say so by omission — they drive `._6` and
`._5` (`PreferredOfferPresentationTests.swift:71-93`) and no assigned-`Confirmed` case exists.

That is the fact that decides the layer. The question is not *"which statuses are upcoming"*. It is
*"is this sentence still true of this booking"*, and the inputs to that are the reservation state, the
fulfilment status **and the seat count** — three server-side facts, two of which no client
should be composing.

### The two near-misses on web are traps, and the ticket is right about both

- `OrdersComponent.isUpcoming` (`orders.component.ts:124-126`) is `new Date(order.cleaningDateTime) >=
  new Date()` — a **date** rule wearing the same name. A cancelled future booking passes it. Its one
  caller is a CSS class (`orders.component.html:166`), which is why nobody noticed.
- `OrderDetailsFacade.isActiveOrderStatus` (`order-details.facade.ts:278-285`) is `{Confirmed,
  OnTheWay, InProgress}`, private, partner-only, and gates *notes and issues*. It is iOS's `isActive`,
  not iOS's `isUpcoming`.
- `libs/shared/models/src/lib/models/order-status.models.ts:19-27` carries the enum and no predicates.
- Android's customer app carries the enum and a label map and no groupings
  (`OrderEnums.kt:34-43`), and its hand-written `OrderDtos.kt` does not map `preferredOffer` at all —
  a grep for `preferredOffer` across `src/cleansia_android/` returns nine **partner-app** Kotlin files
  plus the two generated OpenAPI specs, and **no customer-app Kotlin**.

### The prior panel on this exact file went the other way, one day earlier

T-0581 (`4fa3e63d`, client narrowings deleted in `d5ba1484`) moved a missing term **into the server**
and deleted the clients' local copies. `order-preferred-offer.models.spec.ts:164-188` is the mutation
guard it left behind: *"the flag is the whole answer on EVERY fulfilment state. Re-introducing a status
veto here reddens the Cancelled and Completed rows."* Any ruling that tells the web lane to add a
status term to this feature must reckon with that guard, and this one does not: it does the opposite.

## Decision

### D1 — Scope, stated first because it is the load-bearing half

This rule governs **a disclosure block**: a group of fields the *server* populates in order to make a
**statement about the state of the world** to the caller — a sentence, not a datum.
`PreferredOfferDetails` is the reference instance: `State` is meaningless except as a sentence
selector, and its own doc comment (`PreferredOfferDetails.cs:5-14`) is written entirely in terms of
what the customer is *told*.

It does **not** govern:

- an **action** gate — whether a button, slide or command is offered (ADR-0047 §D1, unchanged);
- a **request** gate — whether the client issues a call it expects to be refused;
- a **lifecycle-utility** gate — *when is this datum useful* (`showAccessCard`'s `(OnTheWay ||
  InProgress)` conjunct, ADR-0047 §D5, unchanged);
- a **plain datum** — a price, a time, an address. A number is not a sentence; withholding it because
  it is "no longer interesting" is how order history loses its content.

**Test the rule against what it would sweep in, before writing it.** The framing I nearly wrote —
*"the server does not send data whose sentence has expired"* — sweeps in the whole order-detail
payload on a completed order and would have licensed withholding the cleaner's name, the price and
the photo rail from order history. The narrowing to *a block whose fields exist to select a sentence*
is what stops it. **If a field would still be worth showing with no sentence around it, this rule does
not reach it.**

### D2 — The question, named, and it is **not** an order-status grouping

> **Is this block's sentence still true of this booking?**

The name for the predicate is **disclosability**, and it is a property of *the block plus the order* —
never of the status alone. Stated so a reader can tell it apart from the two near-misses:

| Question | Answered by | Reads |
|---|---|---|
| *Is this block's sentence still true?* (this ADR) | server — `PreferredOffer.IsDisclosable` | offer state + fulfilment status + free seats |
| *Is this booking in the future?* | `OrdersComponent.isUpcoming` (`orders.component.ts:124-126`) | `cleaningDateTime` — a **clock**, no status |
| *May notes and issues be added?* | `OrderDetailsFacade.isActiveOrderStatus` (`order-details.facade.ts:278-285`) | status ∈ {Confirmed, OnTheWay, InProgress} |

### D3 — The predicate: two limbs, each closing one false sentence

```csharp
// Cleansia.Core.Domain/Orders/PreferredOffer.cs — pure, no collaborators, sits beside StateOf.
public static bool IsDisclosable(
    PreferredOfferState state,
    OrderStatus currentStatus,
    int availableSpots)
    => currentStatus is not (OrderStatus.Completed or OrderStatus.Cancelled)   // (a)
       && !(state == PreferredOfferState.Closed && availableSpots <= 0);       // (b)
```

**(a) The booking has concluded.** On `Cancelled`, all three sentences are false — *"open to our whole
team"* (it is open to nobody), *"we've asked Jana, she has until 18:00"* (nobody is being asked; and
per ADR-0045 §D7.2's amendment `NotifyLapsedPreferredOffers` refuses `Cancelled`/`Completed`, so no
closure message follows either), *"confirmed by Jana"* (nothing is confirmed). On `Completed`, *"open
to our whole team"* is false. `Completed` + `Accepted` is the one **true** sentence this limb
withholds, and it is withheld anyway: it is the same fact as the assigned-cleaner card on the same
screen, in a tense that reads as a promise. iOS already ruled this way with a test
(`PreferredOfferPresentationTests.swift:84-93`).

**(b) The reservation closed and the booking is fully staffed.** `Order.AvailableSpots` is
`MaxEmployees - AssignedEmployees.Count` (`Order.cs:136`). At zero, *"this booking is now open to our
whole team"* is false — nobody can take it. **Not `AssignedEmployees.Count > 0`**, which is the term
`PreferredOfferExit.IsOpen` uses (`PreferredOfferExit.cs:46`) and which would be wrong here: a booking
over two hours has `RequiredEmployees = ceil(EstimatedTime / 120)` ≥ 2 (`Order.cs:697-707`), so with
`SpareSeatsPerOrder = 0` most bookings are multi-seat, and on a 1-of-2 booking the sentence is still
**true** — the second seat really is on the open board. **The rule removes false sentences, not stale
ones**, and the difference is most of the order book.

**Deliberately not limbs, and this is a ruling rather than an omission:**

- `AwaitingConfirmation` on a live booking — the deadline term inside `StateOf` already makes it
  self-expiring (`PreferredOffer.cs:52`).
- `Closed` on a live, *unfilled* booking — true, and it is the customer's only record of what became
  of their request. Withholding it is the harm the copy exists to prevent.
- `canChooseAnother == false` — **not** a proxy for "nothing to say". ADR-0045 §D7.2's dead-tail table
  shows the exit is unreachable in the last 8 h of every fill window, where the `Closed` sentence is
  precisely the honest answer.

### D4 — The layer: the **server** withholds the **whole block**; `StateOf` keeps four inputs

`GetOrderDetails.ResolvePreferredOfferAsync` returns `PreferredOfferDetails?`, and returns `null` when
`IsDisclosable` is false. Nothing else changes: the resolver is already customer-only and already
hands `null` to `MapToDetail` for every other caller class (`GetOrderDetails.cs:127-135`), the DTO is
already a nested optional (`OrderItem.cs:130`), and both clients already handle its absence with tests
that pin the handling (`order-preferred-offer.models.spec.ts:41-49`,
`PreferredOfferPresentationTests.swift:11-13`).

**Three consequences worth naming because they are the reason this shape was chosen:**

1. **No wire change and no `nswag-regen`.** The block is optional today; withholding it uses the
   channel that already exists. A fifth enum value would need a regen on three generated clients and
   an owner-only manual step, and it would hand the render question straight back to the clients.
2. **`StateOf` stays a pure four-input function**, unchanged, with its tests
   (`PreferredOfferStateTests.cs`) unchanged. `None`'s documented meaning — *"no reservation exists or
   ever did"* (`PreferredOffer.cs:14-18`) — stays true, which it would not if a concluded booking
   collapsed to `None`.
3. **The shaping already lives in this method.** `RespondByUtc` is suppressed unless the state is
   `AwaitingConfirmation` (`GetOrderDetails.cs:169-171`) — *do not send a field whose sentence is not
   being said*. D4 is that same move one level up.

**Why not the client, stated as the rejection it is:** three clients, three copies, and a truth
condition that needs a seat count no client should be doing arithmetic on. Web has the bug, iOS has
half the fix, Android cannot have the card at all because its hand-written DTO drops the block. A
client-side answer means the platform is one un-shipped app release away from the same false sentence,
for ever.

### D5 — The safety theorem: withholding can never remove a live affordance

`CanChooseAnother` rides inside the block, so nulling it must be proved not to hide an exit the server
would accept. It is provable, not assumed:

```
IsOpen  =  callerHasActiveMembership ∧ RecurringTemplateId is null
        ∧  OrderAvailability.IsOfferable(CurrentStatus, PaymentType, PaymentStatus, RecurringTemplateId)
        ∧  PreferredOfferRound < Max ∧ AssignedEmployees.Count == 0
        ∧  ¬HasLiveReservation(...) ∧ ComputePreferredHold(...) > 0
```
(`PreferredOfferExit.cs:40-49`)

- **Limb (a):** `IsOfferable` requires `CurrentStatus == Confirmed || (New && Cash)`
  (`OrderAvailability.cs:60-63`). `Completed` and `Cancelled` satisfy neither ⇒ `IsOpen == false`.
- **Limb (b):** `MaxEmployees ≥ 1` always — it defaults to 1 (`Order.cs:112`), `CalculateRequiredEmployees`
  produces `RequiredEmployees ≥ 1` plus a non-negative spare count (`Order.cs:697-707`), and
  `SetMaxEmployees` refuses a value below `RequiredEmployees` (`Order.cs:709-718`). So
  `AvailableSpots ≤ 0 ⇒ AssignedEmployees.Count ≥ 1 ⇒ IsOpen == false`.

> **`¬IsDisclosable ⇒ ¬IsOpen`.** This is the enforcer's core assertion (D9), not a comment.

### D6 — What each stack owes, concretely

**Backend (T-0595, the whole fix).**
1. `PreferredOffer.IsDisclosable` as sketched in D3, with the reasoning in its doc comment.
2. `ResolvePreferredOfferAsync` returns `PreferredOfferDetails?`; `null` when `IsDisclosable` is false.
   `MapToDetail`'s parameter is already nullable (`OrderMappers.cs:223`).
3. `PreferredOfferDisclosureTests` (D9). No migration, no regen, no i18n.

**Web (T-0595, the live defect — and the obligation is a PIN, not a narrowing).**
- **Add no status term** to `order-preferred-offer.facade.ts` or `order-preferred-offer.models.ts`.
  The defect is fixed by the server; a client term here would be the thing
  `order-preferred-offer.models.spec.ts:164-188` was written to stop.
- **Extend that same guard from `canChooseAnother` to `state`**: over every `OrderStatus` member, a
  block the server sent resolves to the state the server sent. That is the mutation guard for a future
  lane "fixing" this in the facade.
- **Add the arrival pin**: block absent ⇒ `facade.visible() === false`. Today
  `models.spec.ts:42-49` pins the *view* for an absent block; nothing pins `visible()`.

**iOS — it KEEPS `OrderStatusGroup.isUpcoming` in `disclosure(for:)`, and this is a ruling.**
- Rationale, and it is *not* "defence in depth" (ADR-0047 §Alternative C rejected that phrasing for
  good reason): the iOS term and the server term **agree on every input**. ADR-0047's redundant flag
  was *wrong* for a real caller class — the employee-as-customer — which is what made it a defect
  rather than duplication. There is no divergent caller class here.
- What it costs, stated so nobody has to rediscover it: if the product ever decides a concluded
  booking *should* show *"Jana, your favourite, cleaned for you"*, the server change will not show on
  iOS. **Retirement condition:** when the iOS lane next opens `PreferredOfferPresentation.swift` for
  any reason, the `isUpcoming` conjunct is deleted and its two tests
  (`PreferredOfferPresentationTests.swift:71-93`) are repointed at the absent-block case.
- **iOS owes one thing now:** limb (b) is a *server* fix, so iOS gets it for free — but only after the
  server ships. A ticket that deletes the iOS conjunct before the backend change is **deployed** (not
  merely merged) reopens harm (2) for the App Store review window. That ordering is the ticket's, not
  a code concern.

**Android — nothing, and the "nothing" is the point.** The customer app does not map `preferredOffer`
(`OrderDtos.kt` carries no such field; the only Kotlin hits for it are in `partner-app`, which is the
cleaner's side of the same feature). When it builds the card it inherits a correct server and needs no
grouping — which is the
return on answering this server-side. **A separate ticket, not this one:** Android customer has no
favourite-cleaner disclosure at all, the same gap T-0580 closed for web.

### D7 — What this does **NOT** canonicalize: there is no platform-wide order-status grouping

**A shared `OrderStatus` grouping is refused, on evidence.** Three "is this order live" sets exist in
the tree today and **all three are different**, each for a stated reason:

| Site | Set | Why it is not the others |
|---|---|---|
| `OrderRepository.cs:259-271` `SlotBlockingStatuses` | New, Pending, Confirmed, OnTheWay, InProgress | A `static readonly OrderStatus[]` **because EF inlines it into SQL**. A C# predicate cannot replace it — that is why `OrderAvailability` needs two forms and an equivalence test (`OrderAvailability.cs:28-30`) |
| `GdprDeletionService.cs:94-101` | New, Pending, Confirmed, InProgress | **`OnTheWay` is absent** — see §Found en route |
| `AdminOverrideOrderStatus.cs:86-97` | two separate refusals | `Completed` and `Cancelled` return **different error keys**; collapsing them costs the customer-facing message |

Canonicalizing across these means reconciling three genuinely different questions and paying
`OrderAvailability`'s two-form + equivalence-test price for a question every site currently answers
correctly. **`IsDisclosable`'s limb (a) is therefore written inline and is private to the preferred-offer
question.** If a fourth site ever wants it, that is the moment to extract — with a caller in hand.

### D8 — The name: `upcoming` is not a status predicate on any stack

`isUpcoming` means **two different things** in this repo today — a clock rule on web
(`orders.component.ts:124-126`) and a status rule on iOS (`OrderStatusMapping.swift:37-40`). T-0595
records a web lane that reached for the first and stopped. **Two groupings with one name is worse than
none.** So:

- A predicate over `OrderStatus` is **not** named `upcoming`, `current`, `recent` or any other time
  word. Time words belong to predicates over `cleaningDateTime`.
- iOS's `OrderStatusGroup.isUpcoming` is a **deviation, recorded not fixed**: it reads "not concluded",
  and its list-tab caller (`OrdersListViewModel.swift:24`) is a legitimate use of that rule under the
  wrong name. Renaming it is a one-file iOS change with no behaviour, filed as a follow-up rather than
  ridden into T-0595.

### D9 — The enforcer, the tier, and the baseline

**Named enforcer:** `src/Cleansia.Tests/Features/Orders/PreferredOfferDisclosureTests.cs` — a new
class beside the existing `PreferredOfferExitAgreementTests`, which is already the precedent for
reading two real code paths against each other (`PreferredOfferExitAgreementTests.cs:158-160`). It
asserts:

1. The D3 truth table — each state × {Completed, Cancelled, and one live status} × {no free seat, a
   free seat}.
2. **The D5 theorem**, over a constructed state space: `¬IsDisclosable(order) ⇒ ¬PreferredOfferExit.IsOpen(order, …)`.
   This is the row that makes the withholding safe rather than merely tidy, and it is the row a future
   change to either function will redden.
3. The handler's own behaviour: a concluded order's detail carries **no** `PreferredOffer` block.

It runs in `dotnet test Cleansia.Tests` (`.github/workflows/backend-ci.yml:69-71`), which is
unconditional on that workflow.

**Tier:** `(gate pending: T-0595)` → **`T1-CI`** on landing. **Baseline is not zero:** exactly one live
violation, the shipped resolver at `GetOrderDetails.cs:146-173`, which sends the block on every
concluded booking. `conventions.md` §*"The price of a law"* condition (b) is therefore unmet today and
the token is honest about it.

**The web half** — *no status term in the preferred-offer facade or view model* — has a **zero**
baseline (neither file reads a status; I opened both) and a mechanical enforcer in the extended
mutation guard at `order-preferred-offer.models.spec.ts:164-188`. It is `(gate pending: T-0595)` →
**`T1-CI`** for the same reason: the `state` half of the guard is not written yet. **Scope caveat that
must travel with the token:** `frontend-ci.yml:85-87` runs `nx affected -t test`, so this pin gates
changes that affect the customer-orders lib — which is every change that could violate it — and not
the whole repo.

**The general principle in D1** — *a read model does not ship a disclosure block whose sentence has
expired* — is **`(guidance — no gate)`**. It is a judgement about whether a group of fields is a
sentence, and there is no mechanical form of that question. Per `conventions.md`, an unnamed human
enforcer is guidance, and saying so is cheaper than a tier nobody honours.

## Alternatives considered

| Option | Disposition |
|---|---|
| **A — a shared cross-stack `OrderStatusGroup` (what T-0595 proposes)** | **Rejected, and the reason is not cost.** It cannot express harm (1): a `Confirmed`, fully-staffed booking is inside every candidate membership, so the false sentence survives on the model implementation the ticket cites (`OrderStatusMapping.swift:37-40` + `PreferredOfferPresentation.swift:23-24`). It also needs three implementations of one truth and would re-add to web the exact term `d5ba1484` deleted and `models.spec.ts:164-188` guards. |
| **B — reuse `OrdersComponent.isUpcoming`** | **Rejected** — a clock rule, not a status rule (`orders.component.ts:124-126`); a cancelled future booking passes it. The ticket is right that this is worse than deriving one. |
| **C — promote `OrderDetailsFacade.isActiveOrderStatus` to `@cleansia/models`** | **Rejected.** It excludes `New`, it is partner-only, and its subject is *may notes be added* (`order-details.facade.ts:278-285`). Promoting a gate to a shared name is how a gate acquires callers whose question it never answered. |
| **D — a fifth input to `StateOf`, returning `None` on a dead booking** | **Rejected.** `None` is documented as *"No reservation exists or ever did"* (`PreferredOffer.cs:14-18`); collapsing a real reservation into it makes the enum lie, and every existing consumer of `None` inherits the lie. |
| **E — a fifth enum value (`Historic`/`Ended`)** | **Rejected.** A wire change on three generated clients + an owner-only `nswag-regen`, and it hands the render decision straight back to the clients — each of which must then be told "render nothing", which is what withholding the block already says. |
| **F — fix the copy instead of withholding** | **Rejected as the primary answer, and it is the closest call.** Rewriting `closed_body` to drop the forward-looking claim would fix harm (1) honestly. It does **not** fix harm (2) — a reservation narrative on a cancelled booking is noise whatever the tense — and it is a 5-locale × 2-key change to buy half a fix. Kept on the record: **if the `Closed` sentence is ever wanted on a filled booking, the answer is limb (b) plus new copy, not limb (b) alone.** |
| **G — ship a `shouldRenderPreferredOffer` boolean beside the block** | **Rejected**, and it is ADR-0047 §Alternative E's shape pointed the other way: a wire field whose entire content is *"should you render the thing next to it"* is derivable by not sending the thing next to it. It also creates a second source of truth that drifts the first time the predicate changes and the flag's derivation does not. |
| **H — delete iOS's `isUpcoming` conjunct in this change** | **Rejected for now** (D6). It is redundant, not wrong; deleting it before the server change is *deployed* reopens the defect for an App Store window; and its retirement condition is written down so it does not become folklore. |

## Challenge (author-run — no independent challenger has run)

- **CH-1 — "you have turned a frontend ticket into a backend change and blocked the web lane on a
  deploy."** *Conceded as a cost, rebutted as an objection.* The web lane's work shrinks rather than
  grows: it ships two test rows and no production code. And the alternative is not cheaper — it is
  three lanes shipping three copies of a predicate that still leaves harm (1) live on two of them.
- **CH-2 — "limb (b) withholds a sentence that is still true on a partly-staffed booking."**
  *Sustained against the first draft, which used `AssignedEmployees.Count > 0` to match
  `PreferredOfferExit.cs:46`.* With `RequiredEmployees = ceil(EstimatedTime/120)` (`Order.cs:697-707`)
  most bookings carry more than one seat, so that draft would have silenced a true sentence across the
  order book. **Revised to `AvailableSpots <= 0`.** The magnitude claim behind the revision is a
  reading of the formula, not a measurement of the order table — I have no shell and do not claim a
  share.
- **CH-3 — "iOS keeping a redundant status gate contradicts ADR-0047, which rejected exactly that."**
  *Rebutted, on the distinction ADR-0047 itself turns on.* That ADR rejected a redundant term because
  it was **wrong for a real caller class** (`GetOrderDetails.cs:58` vs `:81-82` disagree for the
  employee-as-customer). The iOS term here agrees with the server on every input; the only cost is
  drift, and D6 names the drift and its retirement condition rather than pretending it is free.
- **CH-4 — "`d5ba1484` deleted client narrowings on this feature; you are now adding a narrowing back
  on the server. Which is it?"** *Rebutted — they are the same direction.* T-0581 moved a missing term
  **into the server** and deleted the clients' copies. This moves a missing term into the server and
  adds no client copy. The web guard at `models.spec.ts:164-188` stays green and gets stronger.
- **CH-5 — "`IsDisclosable` reads a status, so you have built the very status grouping you refuse in
  D7."** *Partly conceded.* Limb (a) *is* a status test. What D7 refuses is **promoting it to a shared
  name**, on the evidence that the three existing live-order sets disagree with each other and with
  this one. A predicate with one caller, written inline where its reason is legible, is not an
  abstraction; it becomes one when a second caller exists.
- **CH-6 — "the safety theorem depends on `MaxEmployees ≥ 1`, which you assume."** *Answered from
  source, not assumed*: `Order.cs:112` (default 1), `:697-707` (`RequiredEmployees ≥ 1` + a
  non-negative spare), `:709-718` (`SetMaxEmployees` refuses below `RequiredEmployees`). If a future
  path can produce `MaxEmployees == 0`, limb (b) admits a booking with no assignment and the D9 theorem
  goes red — which is the point of pinning it.
- **CH-7 — "`StateOf` can still return `AwaitingConfirmation` for a cancelled order; you have fixed the
  surface, not the function."** *Conceded, and left deliberately.* `StateOf` is a four-column
  derivation and D4 keeps it that way. The residue is that **a future second consumer could
  reintroduce the defect**, so §How-a-reviewer-verifies carries it as an explicit check. I found one
  production consumer by grep (`GetOrderDetails.cs:152`) and, having no shell, do not claim that is
  exhaustive.
- **CH-8 — "withholding the block hides `canChooseAnother` and could hide a live exit."** *Rebutted by
  proof, not by inspection* — D5, from `PreferredOfferExit.cs:40-49` + `OrderAvailability.cs:60-63` +
  `Order.cs:136`, and pinned as D9 assertion 2.

## Verdict (author's ruling — pending a lead)

**D1–D9 stand as written.** The ticket's premise is amended rather than adopted: the defect is not
*"web lacks a status grouping"* but *"the server ships a sentence whose truth it alone can evaluate"*,
and the proposed grouping would have fixed one of the two named harms on one of three stacks. Nothing
here is buildable until a lead rules and the PM stamps `accepted`; the catalog entries landed alongside
carry the `proposed` token and their retirement conditions, so a reader cannot mistake their standing.

## How a reviewer verifies compliance

1. **The block is withheld, not the state coerced.** `PreferredOffer.StateOf` still takes four
   parameters and `PreferredOfferState` still has four members. A fifth of either is a D4 violation.
2. **Limb (b) reads `AvailableSpots`, not an assignment count.** `AssignedEmployees.Count > 0` here is
   the CH-2 defect and silences a true sentence on most multi-seat bookings.
3. **The D5 theorem is asserted, not commented.** A `PreferredOfferDisclosureTests` without the
   `¬IsDisclosable ⇒ ¬IsOpen` row is not the enforcer this ADR names.
4. **No status term appears in the web facade or view model.** `order-preferred-offer.facade.ts` and
   `order-preferred-offer.models.ts` read the block and nothing else; the extended guard at
   `order-preferred-offer.models.spec.ts:164-188` covers `state` as well as `canChooseAnother`.
5. **No shared `OrderStatus` grouping was introduced** in `@cleansia/models`, `:core` or
   `CleansiaCore` (D7). If one appears, it must arrive with the three existing sets reconciled or with
   a stated reason they are not.
6. **Any NEW caller of `StateOf` conjoins `IsDisclosable`** (CH-7). A second consumer that renders the
   state without it reintroduces the defect one surface over.
7. **No time word on a status predicate** (D8) — `upcoming`, `current`, `recent`.

## Found en route — not part of this decision, reported for a ticket

**`GdprDeletionService.HasBlockingOrderAsync` omits `OrderStatus.OnTheWay`**
(`src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs:94-101`): the blocking set is `New,
Pending, Confirmed, InProgress`. Its sibling live-order set includes it
(`OrderRepository.cs:259-271`), and no comment records an intent to exclude it. If that is not
deliberate, an erasure can proceed while a cleaner is en route to the subject's home. **I did not
verify intent and no test was run** — this is a read of two files that disagree, routed to the PM as a
candidate ticket rather than asserted as a defect.
