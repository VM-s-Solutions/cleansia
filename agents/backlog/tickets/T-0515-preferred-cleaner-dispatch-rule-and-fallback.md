---
id: T-0515
title: Make the preferred cleaner actually win the order — dispatch rule plus fallback
status: done
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-04
depends_on: [T-0495]
blocks: []
stories: []
adrs: [0036, 0039]
layers: [backend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 15
---

## Context

The build behind **T-0495**'s ADR. **Owner decision 2026-08-02: the favourite-cleaner perk must work
fully.**

Today `Order.PreferredEmployeeId` is written (`OrderFactory.cs:124` → `Order.cs:349`) and **read by
nothing** — PM-verified. `TakeOrder.cs` is first-come-first-served and contains no reference to it.
This ticket is the first code that reads the field for its stated purpose.

**Nothing here is designed by this ticket.** The mechanism, the hold duration, the fallback trigger and
the cleaner-side visibility rule all come from the ADR. If this ticket finds the ADR ambiguous on any
of them, it **stops and says so** rather than choosing.

## Acceptance criteria

- [ ] **AC1 — the preferred cleaner gets the advantage the ADR specifies, proved by test.** Given an
      order with `PreferredEmployeeId = X`, the ADR's chosen mechanism is observable. Evidence: the
      test, plus the ADR clause it implements.
- [ ] **AC2 — the FALLBACK fires and is proved by test.** After the ADR's wait elapses (or its trigger
      occurs), the order is takeable by anyone eligible. **An order that can get permanently stuck
      waiting for one cleaner fails this AC.** Evidence: the test that advances time past the window
      and takes the order as a different employee.
- [ ] **AC3 — an order with no preference behaves EXACTLY as today.** The overwhelming majority of
      orders. **This is the regression that matters most** — the job board is how every cleaner gets
      paid. Evidence: the existing `TakeOrder` tests green, plus an explicit no-preference case.
- [ ] **AC4 — the preferred cleaner being ineligible does not strand the order.** Weekly limit reached,
      time conflict, not approved, profile incomplete (`TakeOrder.cs:38-60`): the hold must not apply
      to someone who could never take it. Evidence: a test per ineligibility reason the ADR's AC6
      table names.
- [ ] **AC5 — the enforcement is SERVER-SIDE and cannot be walked past by a direct API call.** If the
      mechanism is an exclusive hold, it is a `TakeOrder` validator rule, not a filter on the
      board query. Evidence: an integration/host test where a non-preferred cleaner calls `TakeOrder`
      inside the window and is refused with the right error key.
- [ ] **AC6 — a new `BusinessErrorMessage` key, if any, has its five frontend translations named.**
      `errors.*` in each client's bundle, per `CLAUDE.md`. **Note `error.order.preferred_employee.not_eligible`
      already exists in `CleansiaCore/Localizable.xcstrings`** — reuse before adding. Evidence: the key
      plus the translation plan (the client work is named, not built here).
- [ ] **AC7 — the recurring path is handled per ADR AC7.** `MaterializeRecurringBookings.cs:138`
      hardcodes `PreferredEmployeeId: null`. Either it starts carrying the template's preference, or
      the ticket states that the ADR excluded it. **Do not leave it unmentioned.** Evidence: the diff
      or the citation.
- [ ] **AC8 — `Order.cs:217-224`'s comment is corrected.** It describes a scoring algorithm that does
      not exist and claims *"no UI sets it"* when three clients do. **After this ticket the comment must
      describe what the code does.** Evidence: the updated comment.
- [ ] **AC9 — Gate 6.5 (behavioural non-stub).** This is a state-transition/dispatch change: at least
      one test fails if the dispatch rule is stubbed to always-allow. Evidence: the named test.
- [ ] **AC10 — a test that goes red against the pre-change code (Gate 0.5 leg 1).** The verifier re-runs
      it **un-cached** and states what it could not verify.
- [ ] **AC11 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests` run
      **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **The Plus gate** — **T-0516**, blocked on `Q-PLUS-03`. **This ticket must not add a membership check
  on its own**; doing so would silently answer an owner question.
- **Designing the mechanism** — **T-0495**.
- **The web wizard's missing picker.** Web customers cannot select a preferred cleaner at all
  (`order-wizard.facade.ts:580` sends `undefined`). Named on T-0495, filed separately.
- **Any client change.** AC6 names the copy work; it does not do it.
- **Changing `CreateOrder`'s eligibility rule** unless ADR AC5 ruled it — in which case say so.

## Implementation notes

**Gate 6.5 applies** (dispatch = a state transition on the spine every cleaner's income runs through)
and **Gate 0.5 applies** (behaviour change in a Gate 6.5 class) — both written in at routing time per
`process/routing.md` rules 7 and 8.

**Read first:** the T-0495 ADR, `TakeOrder.cs` in full, `Order.cs:217-226`,
`MaterializeRecurringBookings.cs:120-145`, and the notification digest path
(`NotificationEventCatalog.cs:30`, `Employee.LastNewJobsNotifiedAt`) if the mechanism is notify-first.

**Lane note:** `TakeOrder.cs` is uncontended today. `Order.cs` is touched by nothing else in this
batch. Check before dispatch.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's 2026-08-02 answer that the favourite-cleaner
  perk must work fully).** Filed as the build behind T-0495's ADR so the panel's output has a
  destination and the panel ticket itself stays diff-free. **AC3 is the one to read twice:** this
  touches the job board, which is how every cleaner is paid, so the no-preference path is the
  regression that matters more than the feature.
- 2026-08-04 — **backend: hold resolver + slot answer built (ADR-0036 D2/D3/D4.1/D5 + ADR-0039
  A1/A2/A3/A5).** Test-first on all pure logic. Baselines held: `Cleansia.Tests` 2594 → **2782**,
  `Cleansia.IntegrationTests` 117 → **130**, `Cleansia.HostTests` → **88**, all green. **The six
  visibility surfaces are NOT wired** — `TakeOrder.cs` and `NewJobsDigestService.cs` were held by other
  agents in this batch, and half a visibility rule is worse than none. See `## Review`.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). Landed across four commits, and the sequencing is
  the point: `3092abc1` built the hold resolver but **wired NONE of the six visibility surfaces** and said
  so (*"a granted hold is enforced by nothing — inert, failing open"*); `22eeaec4` enforced it at **all
  six** server-side surfaces; `b9cb6d0f` (Android) and `532d98f5` (iOS) landed the partner-client copy;
  `eb37fdab` then registered the display map and the feed keyset. **Verified at HEAD:**
  `Order.PreferredHoldUntilUtc` with `GrantPreferredHold`/`ClearPreferredHold` as the only writers,
  `Core.Domain/Orders/OrderVisibility.cs`, `Services/PreferredCleanerHoldResolver.cs`,
  `NotificationEventCatalog.cs:44` (`order.preferred_offer`) mapped to `NewJobsAvailable` at `:77`, and
  `NotificationFeedEventKeys.cs:50`. Agreement is pinned by
  `IntegrationTests/Features/Orders/PreferredHoldSurfaceAgreementTests.cs` walking a 7-row fixture, one row
  per term, comparing board, dashboard, browse gate and take gate PER ORDER.
- 2026-08-04 — **ADR-0036 D10's release gate ("C2a and C2b ship together; if only one can ship, ship
  neither") is now SATISFIED, and it was verified rather than trusted.** Before registering anything,
  `eb37fdab`'s agent read both iOS main-bundle catalogs: both loc-keys present in all five languages, every
  unit state `translated`, positional `%1$@` throughout, 30 push keys = 15 events × {title, body} exactly
  matching the display map's new size. `22eeaec4` had earlier **removed** a display-map entry a previous
  agent added, because shipping it would have put the literal string `push.order.preferred_offer.title` on
  a cleaner's lock screen — the tripwire working as designed.
- 2026-08-04 — **AC8 satisfied:** the false scoring comment is gone. `Order.cs:236-244` now states
  explicitly *"There is no matching algorithm and no score"*.
- 2026-08-04 — **residual, flagged not silently chosen (ADR-0036 D8.3 is two-thirds implemented).** The
  resolver re-runs every gate per occurrence, so a lapsed member gets no hold and no push; materializing
  with a **null preference** would need a second resolver call in a sweep with no user session, and doing it
  in the factory would silently drop stored preferences on the normal create path too. **The preference
  stays stored.** Behaviour is correct; the cosmetic leg is deliberately not built. No new ticket — this is
  ADR-0036's lane.
- 2026-08-04 — **`manual_steps: [nswag-regen]` DISCHARGED** (`37440bbc`, and the
  `isAvailableForRequestedSlot` leg at `53f887b6`/`97f7dcd3`).

## Review

### Built (ADR-0036 + ADR-0039)

| Piece | Where |
|---|---|
| The hold window (pure) | `BookingPolicy.ComputePreferredHold` + `PreferredHoldFraction` / `PreferredHoldCeilingHours` |
| The visibility rule, both forms | `Cleansia.Core.Domain/Orders/OrderVisibility.cs` — **built and tested, wired NOWHERE yet** |
| The resolver | `IPreferredCleanerHoldResolver` / `PreferredCleanerHoldResolver` (+ `PreferredCleanerOutcome`, `HoldDeclineReason`) |
| The grant | `OrderFactory` — resolves once, hands the answer to `Order.GrantPreferredHold`, writes neither column itself |
| The set-based busy query | `IOrderRepository.GetBusyEmployeeIdsInWindowAsync` over the extracted `LiveCommitmentsInWindow` filter |
| The picker's answer | `GetMyServingCleaners` — three optional request fields, the tri-state, the same repo call with the same window |
| The one duration definition | `Cleansia.Core.Domain/Orders/OrderDuration.cs`, shared with `OrderFactory`; the picker sums the same rows in SQL |

### NOT built, and why — this ticket is NOT done

1. **The six visibility surfaces are unwired.** `OrderSpecification`, `DashboardSpecifications` (both
   callers), `OrderAccessService.CanBrowseOrderAsync`, `TakeOrder.Validator`'s existence rule and the
   digest conjunct all still ignore `OrderVisibility`. Two of those files were held by other agents in
   this batch. **Consequence today: a granted hold is stored and enforced by nothing** — the perk is
   inert, and it fails OPEN, which is this ADR's posture. Wiring three of six would be worse: the board
   would hide an order the write gate still accepts.
2. **The targeted push (D4) is not emitted.** `PreferredCleanerOutcome.NotifyPreferred` is computed and
   tested; nothing consumes it. Needs `NotificationEventCatalog.PreferredOffer`, its category mapping
   and 5-locale copy — a cross-cutting notification change, not a hold change.
3. **`MaterializeRecurringBookings` still passes `PreferredEmployeeId: null`** (AC7). The factory path
   is ready for it; the template has no field to carry it (ADR-0036 D8).
4. **`Order.cs:217-224`'s comment (AC8) is not corrected** — it describes a scoring algorithm that does
   not exist. Left with the enforcement work it belongs to.

### Manual steps
- ⚠️ **`nswag-regen` (owner-only).** `GetMyServingCleaners.Query` gains three optional query-string
  fields (`CleaningDateTimeUtc`, `SelectedServiceIds`, `SelectedPackageIds`). The **response** is
  unchanged — `IsAvailableForRequestedSlot` was already on the wire; only the request shape moved.
- **No migration.** Nothing schema-touching; `Order.PreferredHoldUntilUtc` already exists.

### Catalog harvest
`patterns-backend.md` §"Bounded exclusivity" — the *reason* given for the two-form equivalence test was
falsified by mutation and is corrected in the same change. EF Core's null semantics rewrite
`Col == @p` to `Col IS NULL` for a captured null, so the queryable form matches C# on exactly the case
the ADR feared; what the test actually catches is a term edited on one side only. The rule survives, its
justification did not.

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read at HEAD: `Order.cs:236-255`, `OrderVisibility.cs`,
`PreferredCleanerHoldResolver.cs`, `NotificationEventCatalog.cs:40-80`, `NotificationFeedEventKeys.cs:45-55`,
`OrderFactory.cs:190-200`, and both surface-agreement integration tests. `22eeaec4` records **eight
mutations, each applied, run and reverted** — the subtle one is #7, passing a null beneficiary while keeping
the clock, which makes the BENEFICIARY'S OWN held order vanish from their board — plus 2798 unit / 132
integration / 88 host, 0 failed, re-run independently. `eb37fdab` re-confirms the same counts after the
registrations. **`manual_steps` discharged.**

