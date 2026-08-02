---
id: T-0494
title: SECURITY — recurring bookings are a paid Plus perk gated only in the clients; a direct API call succeeds
status: draft
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
---

## Context

**Source: the Cleansia Plus audit (2026-08-02).** *"Recurring bookings are gated client-side only —
a direct API call succeeds."*

### Ground truth — PM-verified first-hand at `master` `0e4ede1b`. Confirmed.

`src/Cleansia.Core.AppServices/Features/Bookings/CreateRecurringBooking.cs`, `class Validator :
AbstractValidator<Command>` (`:27-70`). Every rule in it, read in full:

| Line | Rule |
|---|---|
| `:31` | `Frequency` — enum range |
| `:35` | `DayOfWeek` |
| `:39` | `TimeOfDay` |
| `:45` | `Rooms >= 0` |
| `:46` | `Bathrooms >= 0` |
| `:48` | `SavedAddressId` not empty |
| `:50` | `PaymentType` |
| `:54`, `:64` | cross-field rules |
| `:58` | `StartsOn` |

**There is no membership check. Not in the validator, not anywhere in the file.** A `grep` for
`Membership` / `Plus` across `CreateRecurringBooking.cs` returns **nothing**.

**The gate the customer sees is in the clients.** iOS ships `recurring_plus_gate_title`,
`recurring_plus_gate_subtitle` and `recurring_plus_gate_cta` in
`CleansiaCustomer/Resources/Localizable.xcstrings` — three keys whose entire purpose is to tell a
non-subscriber they need Plus. **Android does not even have those three keys** (PM-diffed the two key
sets), so its gate — if any — is elsewhere or absent.

### Why this is filed separately from every other Plus ticket, with no dependency on T-0491

**Because it is true whichever way the product questions are answered.** T-0491 decides what Plus
*promises*; this ticket is about a paid capability being obtainable **without paying**, which is an
authorization defect under any ruling. Making it wait on a product panel would be filing a security
hole behind a design discussion.

**And it is not hypothetical.** The mobile apps are shipping, DEV is live, and the endpoint is
reachable with any authenticated customer token. The exploit is: subscribe, read the request in a
proxy, cancel, replay. It requires no skill and no special tooling.

**One sibling to check in the same pass:** `SetRecurringBookingActive.cs`, `UpdateRecurringBooking.cs`
and `DeleteRecurringBooking.cs` sit in the same folder. A gate on create that is absent on
*re-activate* is not a gate.

## Acceptance criteria

- [ ] **AC1 — an authenticated customer with NO active membership is refused by the server.** Given a
      valid customer JWT and no active `UserMembership`, When `CreateRecurringBooking` is called
      directly, Then the request is rejected with a business error, and the template is **not**
      persisted. Evidence: an **integration or host test** that calls the route (not a unit test on
      the validator alone) — the whole finding is that the client is not the enforcement point, so
      the proof must go through the route.
- [ ] **AC2 — the gate covers the WHOLE lifecycle, not just create.** `SetRecurringBookingActive`,
      `UpdateRecurringBooking` and `DeleteRecurringBooking` are each examined and each given the
      correct answer with a reason. **Delete and pause should almost certainly stay open** — a lapsed
      subscriber must be able to stop a template that is still generating orders, and locking them out
      of that is a worse defect than the one being fixed. State the ruling per endpoint. Evidence:
      the four-row table plus a test per gated endpoint.
- [ ] **AC3 — an EXISTING template belonging to a lapsed subscriber is handled deliberately.** What
      happens to templates that are already materializing orders when a membership lapses? The
      materializer is `Features/Bookings/MaterializeRecurringBookings.cs` (a Function). **Silently
      continuing to generate paid cleanings for a non-subscriber, or silently stopping and leaving the
      customer with no cleaning and no notice, are both bad — pick one and say so.** Evidence: the
      ruling plus the behaviour at file:line. **If the answer is "notify the customer", that is a
      separate ticket and it is named, not built here.**
- [ ] **AC4 — status, not existence.** The check reads `MembershipStatus`
      (`Core.Domain/Memberships/MembershipStatus.cs`), not the presence of a `UserMembership` row.
      Evidence: the predicate at file:line plus a test with a cancelled membership.
- [ ] **AC5 — the error is in the contract on every client.** The new `BusinessErrorMessage` key has
      an `errors.*` translation in all three web apps' five locales, and the mobile clients map it —
      **note that different clients use different key namespaces** and NSwag throws ProblemDetails
      bare, so reading `.result` alone resolves nothing. Evidence: the parity check
      (`error-contract-parity.spec.ts` for customer web) plus the mobile mapping.
- [ ] **AC6 — a test that goes red against the pre-fix code (Gate 0.5 leg 1).** AC1's route test,
      proved to **succeed** (i.e. the booking is created) against the current code and to fail after.
      The verifier re-runs it **un-cached**. Evidence: the before/after runs.
- [ ] **AC7 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests` run
      **locally**, baselines **2295 / 108 / 75**.
- [ ] **AC8 — the SECURITY gate runs.** `security_touching: true`. The security reviewer states
      whether the same class exists on other paid capabilities — this is a *class*, and the audit
      already found a second instance (express, T-0493).

## Out of scope

- **What Plus promises** — T-0491. Deliberately no dependency in either direction.
- **The discount math** — T-0492.
- **Notifying customers whose templates stop** — named by AC3, not built here.
- **Any client change.** The clients already show a gate (on iOS at least); this ticket makes the
  server the one that decides. If Android's client-side gate is found missing entirely, **record it**
  and file it separately.

## Implementation notes

**No panel — one-line "no-decision" note on the security half:** enforcing on the server what the
client already claims to enforce introduces no new behaviour and no new product decision. **AC2 and
AC3 do carry decisions** (which lifecycle endpoints are gated, and what happens to live templates) —
they are written as forced rulings inside the ticket rather than as a panel, because the wrong answers
are both obviously bad and the right answer is bounded. **If AC3's answer turns out to need customer
notification, stop and escalate.**

**Gate 6.5 applies** — this is an authorization decision, one of the classes `routing.md` rule 7
enumerates. The reviewer gates on a behavioural non-stub plus an end-to-end test driving the real
route.

**Share the predicate with T-0493.** Whichever lands first writes "does this user have an active Plus
membership" somewhere the other can reuse. Two copies of a membership check is how they drift.

**Read first:** `agents/knowledge/security-rules.md` (S1–S11), `Features/Bookings/*.cs`,
`Core.Domain/Memberships/*`.

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** **PM-verified first-hand:**
  `CreateRecurringBooking.cs`'s validator was read in full and contains **no membership rule of any
  kind**; the customer-facing gate is three iOS string keys (`recurring_plus_gate_*`) that Android
  does not even carry. **Filed with NO dependency on T-0491, deliberately** — a paid capability
  obtainable without paying is a defect under every possible product ruling, and queuing it behind a
  design panel would be filing a security hole behind a discussion.

## Review
