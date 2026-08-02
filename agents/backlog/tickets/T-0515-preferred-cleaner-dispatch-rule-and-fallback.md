---
id: T-0515
title: Make the preferred cleaner actually win the order — dispatch rule plus fallback
status: draft
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0495]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
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

## Review
