# Role — `PreferredOffer` (`StateOf` + `IsDisclosable`) and the customer's offer block (CRC card)

> **Both halves are SHIPPED and the decision behind them is now ratified.** `StateOf` is
> ADR-0045 §D7.1 (`src/Cleansia.Core.Domain/Orders/PreferredOffer.cs:36-53`), unchanged. `IsDisclosable`
> landed with T-0595 (`src/Cleansia.Core.Domain/Orders/PreferredOffer.cs:56-83`, enforcer
> `src/Cleansia.Tests/Features/Orders/PreferredOfferDisclosureTests.cs`), and its **ADR-0049** is
> `accepted` **with amendments C1–C6** (lead, 2026-08-11)
> (`docs/decisions/adr-0049.md:3`).
> **Retires when:** that status line stops reading `accepted`.
>
> ⚠️ **Read amendment C1 before citing §D7.** Its evidence — *"three live-order sets and all three are
> different"* — was destroyed by `746a5064` in the same sprint; two are now identical and pinned equal.
> The **conclusion** stands, on a different ground. §D7's borrowed *"a C# predicate cannot replace it"*
> argument is **struck** and must not be reused for a flat status set.

This card covers **two** responsibilities that must not be collapsed, plus the mapper that joins them.
They were one responsibility until 2026-08-11, and the defect T-0595 records is exactly what that
conflation produced.

| | Owns |
|---|---|
| `PreferredOffer.StateOf` | **derives** which of four reservation states this order is in |
| `PreferredOffer.IsDisclosable` | **decides** whether that state's sentence is still true of this booking |
| `GetOrderDetails.ResolvePreferredOfferAsync` | **assembles** the block, or sends `null` |

## Responsibility (one sentence each)

**`StateOf`.** Answer, as a **pure four-column derivation**, *"what became of the reservation on this
order?"* — `None` / `AwaitingConfirmation` / `Accepted` / `Closed`, from beneficiary id, hold deadline,
"is the beneficiary assigned", and now. Derived and never stored, so it has no writer, cannot go stale
and needs no backfill (ADR-0045 §D7.1).

**`IsDisclosable`.** Answer, as a **pure function of the state plus two order facts**, *"is this
block's sentence still true of this booking?"* — `false` when the booking has **concluded**
(`Completed`/`Cancelled`) or when the state is `Closed` on a booking with **no free seat**. It is not a
status grouping and must not become one (ADR-0049 §D7).

**The mapper.** Build `PreferredOfferDetails` only when `IsDisclosable`; otherwise hand `null` to
`MapToDetail` (`src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs:223`), which is the channel it
already uses for every non-customer caller
(`src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetails.cs:127-135`).

## Collaborators

- **`Order`** — for `CurrentStatus` and `AvailableSpots` (`src/Cleansia.Core.Domain/Orders/Order.cs:136`).
  Both are read; neither is written.
- **`PreferredOfferExit.IsOpen`** — the sibling read-side evaluation for *"may the customer name a
  second cleaner"* (`src/Cleansia.Core.AppServices/Features/Orders/PreferredOfferExit.cs:40-49`). It is
  **not** a collaborator of `IsDisclosable` — the two are independent, and the standing invariant
  between them is `¬IsDisclosable ⇒ ¬IsOpen`, asserted by a test rather than by construction.
- **`OrderAvailability.IsOfferable`** — reached only *through* `IsOpen`. `IsDisclosable` never calls it:
  offerability answers *may a cleaner take this*, which is a different question with a money axis on it
  (`src/Cleansia.Core.Domain/Orders/OrderAvailability.cs:55-63`).
- **`IEmployeeRepository`** — the mapper's, for the beneficiary's display name. Neither pure function
  knows a name exists.

## Does NOT know

- **`StateOf` does not know the order's fulfilment status, and that is still true after ADR-0049.**
  This is the "does NOT know" that produced the defect: a state derived without a status was rendered
  as a sentence about a booking. The repair adds a **collaborator** (`IsDisclosable`), it does not widen
  `StateOf`. **A fifth parameter on `StateOf` is a finding.**
- **`IsDisclosable` does not know the caller.** Membership, entitlement and the exit's lead-time term
  all live in `IsOpen`. If a scenario needs disclosability to consult the caller, the responsibility is
  wrong or the question is `IsOpen`'s.
- **Neither knows the copy.** They select a sentence; they do not contain one. Wording is the client's
  bundle (`src/Cleansia.App/apps/cleansia.app/src/assets/i18n/en.json:1740-1741` and the mobile
  equivalents).
- **Neither knows WHY a reservation ended.** A decline and a silence are the same `Closed`, deliberately
  — the platform never attributes conduct to a named person (ADR-0045 §D7.3). A fourth state
  distinguishing them is a hard reject.
- **Neither writes.** No `Add`, no `Commit`, no notification, no hold mutation. The hold pair's only
  writers stay `Order.GrantPreferredHold` / `ClearPreferredHold`
  (`docs/domain/roles/preferred-cleaner-hold-resolver.md`).
- **The client does not know disclosability.** It renders the block off the block's **arrival**. A
  client conjoining a status onto the server's block is `patterns-frontend.md` §*"A server-authored
  disclosure block is rendered off its own ARRIVAL"*.
- **Which tenant.** The global query filter scopes the mapper's reads; neither pure function queries.

## Invariants a reviewer checks

1. **`StateOf` still takes four parameters and `PreferredOfferState` still has four members.**
2. **The withholding is of the BLOCK, not a coercion of the state.** `None` means *"no reservation
   exists or ever did"* (`src/Cleansia.Core.Domain/Orders/PreferredOffer.cs:14-18`); a concluded
   booking collapsing to `None` makes that documentation false for every other consumer.
3. **`¬IsDisclosable ⇒ ¬IsOpen` is asserted**, over a constructed state space, not commented. Without
   that row the withholding could one day hide a live "choose another cleaner" affordance.
4. **The free-seat term is `AvailableSpots <= 0`, never `AssignedEmployees.Count > 0`.** The latter is
   `IsOpen`'s term for a different question and silences a **true** sentence on multi-seat bookings
   (`Order.cs:697-707`).
5. **No shared `OrderStatus` grouping was extracted.** The live-order sets in the tree are kept apart on
   purpose — `OrderRepository.cs:264-271` (does a live commitment occupy this cleaner's slot),
   `GdprDeletionService.cs:112-114` (does a live order refuse this subject's erasure; **identical
   membership** to the first since T-0595, pinned to it by `ErasureBlockingOrderStatusTests` rather
   than merged), `AdminOverrideOrderStatus.cs:86-97` (two refusals, different error keys).
   ⚠️ **This invariant has now lost TWO supporting arguments and kept its conclusion both times, which
   is worth knowing before anyone offers it a third.** The two-form *"EF inlines it into SQL"* clause
   was struck by ADR-0049 amendment C1(ii) — a flat status set is data, not a compound expression, so
   the identical array works in both places. The *"one is in-memory"* clause was struck by `0a552981`,
   which made the erasure read `AnyAsync` over the indexed `CurrentStatus` and deleted an unbounded
   `.Include` that loaded a graph nothing looked at. The conclusion stands on the only ground that was
   ever load-bearing: **two questions with one answer today are not one question.** A shared constant
   makes a future divergence silent, because the second caller inherits it.
6. **Every NEW consumer of `StateOf` conjoins `IsDisclosable`.** A second surface rendering the state
   without it reintroduces the defect one screen over.

## Watch-list

- **A fourth consumer of `StateOf` is the moment to reconsider the shape.** Today there is one
  production consumer (`GetOrderDetails.cs:156`). If the state starts being rendered on a list row, a
  push body or an admin screen, "remember to conjoin `IsDisclosable`" stops being a reviewable rule and
  the two functions should merge behind one entry point returning `PreferredOfferState?`.
- **If the product ever wants the reservation on order history** — *"Jana, your favourite, cleaned for
  you"* — the change is limb (a) of `IsDisclosable` plus new copy, and it will not appear on iOS until
  that app's own `isUpcoming` conjunct is deleted (ADR-0049 §D6).
- **The `Closed` sentence's second job expires; its first does not.** It says both *"your request
  ended"* and *"this booking is on the open board"*. Limb (b) exists because only the second can become
  false. If the copy is ever split, limb (b) should be re-examined rather than inherited.
