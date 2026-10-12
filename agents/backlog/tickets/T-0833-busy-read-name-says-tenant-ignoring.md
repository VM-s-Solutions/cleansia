---
id: T-0833
title: Rename GetBusyEmployeeIdsInWindowAsync so its name says it reads every company (S8)
size: XS
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0815]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

Found on 2026-10-11. `GetBusyEmployeeIdsInWindowAsync` reads `GetQueryableIgnoringTenant()` (`OrderRepository.cs:416`), but its name sounds scoped. Request paths call it: `GetMyServingCleaners.cs:158`, and `PreferredCleanerHoldResolver.cs:108` through `CreateOrder`, `ChoosePreferredCleaner` and `ConfirmRecurringOrder`. So do the Stripe payment webhook (`HandlePaymentNotification.cs:426`, through `PreferredOfferNotifier`) and the recurring-materializer timer (`OrderFactory.cs:316`, through `MaterializeRecurringBookingTemplate`). That mix of request path and background job is exactly what S8's third form is about.

It is not exploitable today:
- Every caller's ids are server-derived, or proven by a validator.
- The contract (`IOrderRepository.cs:166-174`) states the caller's obligation.

It is still the IntelliSense trap that T-0815 deleted a method to close. S8's third form (`security-rules.md:705-714`) asks a method like this to name its world.

## Doing (proposed, for Mike)

- Rename it to `GetBusyEmployeeIdsInWindowIgnoringTenantAsync`, or add it to the roster that `(gate pending: T-0606)` names (`security-rules.md:652`). The choice is the Architect's call.
- Update its callers, tests and docs.

## NOT doing

- No change to its behaviour or its callers' ids.

## Acceptance criteria (done looks like)

- [ ] **AC1** — The name, or the roster, says that it reads every company. S8's text agrees.
- [ ] **AC2** — The unit, integration and host suites pass.

## Work record

- 2026-10-12 — Filed from the security review of T-0815 and T-0817. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
