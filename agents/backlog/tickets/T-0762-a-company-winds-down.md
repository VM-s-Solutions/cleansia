---
id: T-0762
title: A company winds down — everyone is told, then from a date open orders are cancelled and refunded (and failed refunds re-driven), templates paused, every Plus ended, credit discharged and the last period invoiced once the door is closed; the sweep converges on re-run
status: done
size: L
owner: —
created: 2026-09-16
updated: 2026-09-16
depends_on: [T-0760]
blocks: [T-0763, T-0764]
stories: []
adrs: [ADR-0064, ADR-0006, ADR-0009, ADR-0046, ADR-0002]
layers: [backend, functions, bicep, android-locales, ios-locales, frontend-locales]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0064 D2. The per-row primitives existed: `AdminCancelOrder` (full refund, eight collaborators, key
`refund:{id}:cancel`), `RefundService` (a `Pending` row is re-driven only by a second call on the same
key; `ServiceNotRendered` → `refund:{id}:admin`), `CancelUnfilledOrders` (the sweep idiom and the money
term), `SetRecurringBookingActive`, `CancelMembershipSubscription`, `CreditTransactionReason.Expired`,
`PayPeriodBackgroundService`'s per-period close body, `SendEmailHandler`, the promo fan-out's keyset
paging and `CampaignProgress`, the Dedicated plan's 30-minute default timeout.

## Doing (shipped `b2deb803`; review `9a3e4bb3`; cross-ticket review `3f2e8018`; follow-up `e1a1beb3`)

- **Extraction (money path — adversarially reviewed).** `IPlatformOrderCancellation` with
  `CancelAsync(order, actorId, cancelledBy, reasonKey, refundReason, ct)` (the `AdminCancelOrder.Handler`
  body moved verbatim) and `RefundAsync(order, actorId, refundReason, ct)` (the refund leg alone, the
  `OrderRefunded` notification keyed on the refund id); the handler keeps its load + status gates and
  calls `CancelAsync(…, CancelledBy.Admin, command.Reason, RefundReason.CustomerCancellation, …)` — its
  key still `refund:{id}:cancel`. The handler's money tests moved to `PlatformOrderCancellationTests`;
  `CancelUnfilledOrders` diff-empty.
- **Reason key.** `OrderCancellationReasons.CompanyWindDown = "order.cancelled.company_wind_down"`; the
  booking-policy parity checker and the three customer clients' reason maps gained it. **Review
  (`9a3e4bb3`):** the customer's sentence is tender-neutral (a cash booking is cancelled, not "refunded")
  in five locales on web, Android and iOS.
- **Request.** `WindDownCompany.Command(DateOnly? FromDate)` — `tenant.not_found` (first; `9a3e4bb3`),
  `company.archived`, a re-run while `IsWindDownRunning(now)` → `company.wind_down_in_progress`, a date
  when one is set → `company.wind_down_already_requested`, no date and none set → `common.required`, a date
  already past in the company's easternmost market → `company.wind_down_date_in_past`. Handler:
  `RequestWindDown` on the first request; `CompanyWindDownDispatch.EnqueueAsync` — one
  `IPendingDispatch.Enqueue` on `QueueNames.CompanyWindDown` keyed
  `wind-down:{tenantId}:{now:yyyyMMddHHmmss}`, returning false when the outbox already holds the key;
  `DeactivateCompany` calls the same when `WindDownFrom` is set. `[AuditAction("company.wind_down")]`;
  route `api/AdminCompanyLifecycle/wind-down` under `CanWindDownCompany`. **Bicep:** `company-wind-down`
  in `storage.bicep` `queueBaseNames` (+ poison) and its alert.
- **Sweep.** `CompanyWindDownService.RunAsync(tenantId)` — no-op (logged, acked) when missing / no date /
  frozen; `StartWindDownRun` + commit; **(1) notices** (keyset 200 over `Users` with
  `IsActive && IsEmailConfirmed && !Email.EndsWith("@anonymized.local")`, customers by profile, cleaners
  with `Employee.ContractStatus == Approved`; one `SendEmailMessage` each through `IQueueClient`,
  `Code = WindDownRequestedOn`, cursor `wind-down-notice:{tenant}:{code}`); **(2) orders** (`New` /
  `Confirmed` / `OnTheWay`, `Paid || Cash`, `WindDownCutoff.Utc` per market, floor lifted when
  `IsDeactivated`, untracked `CurrentStatus` re-read, `CancelAsync(order, WindDownRequestedBy, System,
  CompanyWindDown, ServiceNotRendered)`, commit per order); **then the re-drive** — cancelled by the
  wind-down, card, still `Paid`, **minus the orders this run cancelled** (reviewer #4: a refusal seconds
  old is left to the next run) → `RefundAsync`, commit per order; an order with no charge surface is
  logged for an admin; **(3) templates** paused; **(4) Plus** — every `Active` membership with
  `CancelledAt == null`, any currency, `CancelSubscriptionAtPeriodEndAsync` + `MarkCancellationRequested`,
  commit per membership; **(5) credit, only when `IsDeactivated`** — every `Balance > 0` account through
  `TryDebitAsync(id, balance, Expired, "wind-down-credit:{account}:{runStartedOn}", actor, note: "company wind-down")`;
  **(6) the last period, only when `IsDeactivated`, no offerable order and no
  `Completed && !EmployeePayCalculated` order** — `PayPeriodBackgroundService.ClosePeriodAsync(period,
  "Company wind-down", openNext: false)`; never re-invoices a `Closed` period; `RecordWindDownRun` and the
  Error-level summary. The rollover opens no successor for a deactivated company and (`e1a1beb3`)
  excludes the period it is closing from the successor check rather than committing early.
  `CompanyWindDownHandler` (permanent no-op → ack; transient → throw), `CompanyWindDownFunction` +
  `CompanyWindDownPoisonFunction`. `host.json` unchanged.
- **E-mail.** `EmailType.CompanyWindDownCustomer = 8`, `CompanyWindDownCleaner = 9`; two templates;
  `IEmailService.SendCompanyWindDownCustomerNoticeAsync` / `…CleanerNoticeAsync` with five locales of
  defaults under `EmailTemplateTranslation`; the company named by each market's `CompanyInfo.LegalName`;
  `SendEmailHandler` arms; `CompanyWindDownEmailRenderingTests`.
- **Cross-ticket review (`3f2e8018`):** a cancelled order owes no receipt — the settlement facts and
  `FiscalReconciliationService` exclude it.
- **Locales.** Admin: `company.wind_down_already_requested`, `company.wind_down_date_in_past`,
  `company.wind_down_in_progress`. Customer web/Android/iOS: the reason key wherever the system
  cancellation reason renders.

## NOT

No archive, no guard (T-0763). No horizon gate in the booking path. No Plus left running in any currency.
No credit transfer. No cleaner account deactivation. No guest e-mail beyond the cancellation path. No
timer. No date change after the first request. No gate on `DeactivateCompany`'s enqueue. No
continuation-message paging. No re-invoicing of a `Closed` period. No change to `CancelUnfilledOrders`,
`host.json`, or the notice's opt-in semantics (none).

## Acceptance criteria

- [x] AC1 (TC-LC-WD-1) — two companies; the sweep for B from D: exactly two notices (the confirmed
      customer, the approved cleaner) with `Code = WindDownRequestedOn` and a `CampaignProgress` row; the
      two paid-or-cash on/after-D orders cancelled `System` / `order.cancelled.company_wind_down`, the card
      one with a `Refund` under `refund:{id}:admin` (`ServiceNotRendered`), push, cleaner notified,
      loyalty revoked, waiver released; the unpaid card order and the before-D orders untouched; the
      active template paused; both the EUR and the CZK Plus cancellation-requested, Stripe called once
      each; no credit changed; nothing of A changed; `WindDownRunStartedOn` null, `WindDownLastRunOn` set.
- [x] AC2 (TC-LC-WD-2) — the same message again: zero orders, refunds, templates, Stripe calls, e-mails;
      the cursor unchanged; `WindDownLastRunOn` advances.
- [x] AC3 (TC-LC-WD-3) — B deactivated: the before-D cash order cancelled (floor lifted), the 300 balance
      discharged with one `Expired` transaction and the note, the 0 account untouched, the period `Closed`
      with one invoice per cleaner-currency, PDF and e-mail, no new period; the rollover opens none for B
      and rolls A.
- [x] AC4 — an `InProgress` order is untouched and the period step skipped with a log; a `Completed &&
      !EmployeePayCalculated` order skips it too; a `Closed` period with an uninvoiced pay row is not
      re-invoiced.
- [x] AC5 (TC-LC-WD-4) — a refused refund leaves the order cancelled and the row `Pending`, logged at
      Error, the run continues; the next run re-drives it once on the same key → `Succeeded`, `Refunded`;
      a third run calls Stripe zero times.
- [x] AC6 (TC-LC-WD-5) — the validator's six refusals; a re-run enqueues a fresh message; a ten-minute-old
      run stamp refuses, a two-hour-old one admits; `DeactivateCompany` enqueued exactly one message.
- [x] AC7 — both e-mails render in five locales with each market's legal name and the date, no
      placeholder; an admin translation row wins.
- [x] AC8 — `AdminCancelOrder` HostTests unchanged in outcome; key still `refund:{id}:cancel`; the money
      assertions moved, not rewritten.
- [x] AC9 — a message for a missing / dateless / frozen company is discarded as permanent; a transient
      failure throws; the poison twin writes a `DeadLetter`; `storage.bicep` and `queueAlerts.bicep` carry
      the queue, twin and alert (`QueueListenerInventoryTests`).
- [x] AC10 — the reason renders in the three customer clients in five locales; the parity gate green.

## Review

Full-suite evidence at the review fix (one project at a time from `src/`): `dotnet test Cleansia.Tests`
5754 passed / 0 failed; `Cleansia.IntegrationTests` 458 / 0; `Cleansia.HostTests` 277 / 0;
`node agents/tools/check-booking-policy-parity.mjs` exit 0. Reviewer findings: #1 full suites (done),
#2 AC → test map after the fact (recorded), #3 client regen deferred to the batch's regen (`87ff17e0`),
#4 same-run exclusion in the re-drive (shipped as the honest cut — recorded in ADR-0064 §*What shipped*),
#5 cleared date → permanent ack (kept), #6 `PartiallyRefunded` in the money term (not changed; the
manual-cancel residual named), #7 `tenant.not_found` before the frozen check (done), #8 tender-neutral
reason sentence (done), #9 divider banners dropped (done). Reported, not fixed there and folded into
`3f2e8018`: `DeactivateCompany.Validator` / `ReactivateCompany.Validator` had the same pre-fix shape.

## Status log

- 2026-09-16 — shipped `b2deb803`; review `9a3e4bb3`; cross-ticket review `3f2e8018`; follow-up
  `e1a1beb3`. Recorded in ADR-0064 D2 and §*What shipped*; `/domain/roles/company-lifecycle`,
  `/domain/roles/platform-order-cancellation`; `/product/business-rules#company-lifecycle` and
  `#platform-cancellation`; `/flows/cross-cutting` (the third job shape); `/architecture/infrastructure`
  (the queue, the consumer); CHANGELOG.
