---
id: T-0775
title: Administrators are told — the seven remaining events (ADR-0065 D4)
status: todo
size: M
owner: —
created: 2026-09-19
updated: 2026-09-19
depends_on: [T-0774]
blocks: []
stories: []
adrs: [ADR-0065]
layers: [backend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

ADR-0065 D4 — the seven event sites T-0768 declared and did not raise (T-0768 raised `admin.dispute.filed`,
T-0770 raises `admin.order.crew_lost`). Every key, template and locale string already exists (T-0774 shipped
all nine), so this ticket is call sites and their tests. Ground truth at filing: `OrderFactory.cs:314`,
`HandlePaymentNotification.cs:243-249, 313, 447, 472`, `ConfirmRecurringOrder.cs:187`,
`RetryFailedUserDeletions.cs:87-103`, `WindDownCompany.cs:102`, `CompanyWindDownService.cs:87`,
`CompanyArchiveService.cs:172` — every one re-checked 2026-09-19.

## Doing

- The seven sites: `OrderFactory.cs:314` (with the null-tenant guard — `order.TenantId` is `string?`,
  set at `:162`), `HandlePaymentNotification.cs:313` (paid), `:243-249` (payment failed — **once per
  order**, guarded by `AnyForEventAsync`), `:447` and `:472` (chargeback), `ConfirmRecurringOrder.cs:187`,
  `RetryFailedUserDeletions.cs:87-103` (fresh scope + commit), `WindDownCompany.cs:102`,
  `CompanyWindDownService.cs:87` (**only when the run did something**), `CompanyArchiveService.cs:172`.
- `CancelUnfilledOrders.FormatCreditAmount` hoisted to a shared money formatter (second caller).
- The `RetryFailedUserDeletions.Response` comment rewritten: the channel exists.
- Tests per ADR-0065 §Verification 6, 7, 8; the redelivery assertions per site.

## NOT

- No new keys, templates or copy (T-0774 shipped all nine). No dedup inside the notifier.

## Done looks like

`IAdminNotifier` is called once at each of the seven sites inside the site's unit of work; a redelivered
Stripe event, a re-run wind-down that did nothing, a second decline on one order and a second retry on one
day each add nothing; three backend projects green.

## Acceptance criteria

- [ ] **AC1** — *Given* company A with two administrators and company B with one, *when* a card order of A
      is paid (completed-session webhook), *then* two `admin.order.new` rows exist for A's administrators
      and none for B's, two outbox rows with distinct keys — and a redelivery of the same Stripe event adds
      nothing.
- [ ] **AC2** — *Given* an unpaid card order is created, *when* nothing else happens, *then* no admin row
      exists; *given* a cash one-off order is created, *then* one row per administrator exists at creation;
      *given* a creation whose `OperatorTenantId` is null, *then* nothing is written and a warning is logged.
- [ ] **AC3** — *Given* two `payment_intent.payment_failed` events on one order, *then* one
      `admin.payment.failed` row per administrator and one outbox row per address exist, and the second
      webhook commits successfully.
- [ ] **AC4** — *Given* a deletion request that failed today and fails again tomorrow, *when* the daily retry
      runs on both days, *then* two `admin.erasure.failed` rows and two outbox rows with keys differing by
      the day exist; running the retry twice on one day produces one **because the second run selects no
      candidate** (asserted on the candidate query).
- [ ] **AC5** — *Given* a chargeback webhook for an order with an open dispute, *then* one
      `admin.dispute.chargeback` row carries that dispute's id; without an open dispute, the new dispute's
      id.
- [ ] **AC6** — *Given* a wind-down run with `cancelled + refunded + refundFailures + periodsClosed == 0`,
      *then* no `admin.company.wind_down_run` row; *given* one that cancelled one order, *then* one row per
      administrator with the four counts.
- [ ] **AC7** — *Given* a company whose `ArchiveRequestedOn` is set, *when* `MarkArchived` runs, *then* the
      `admin.company.archived` rows and outbox rows commit without `CompanyArchivedException`.

## Implementation notes

ADR-0065 D4's table is the contract per site: args (never PII), subject, and the reason each subject is
unique across requests. "New order" is the offerable transition, not creation (the three sites that already
call `PreferredOfferNotifier`). "Payment failure" is once per order (O-6) — the **site** guards with
`AnyForEventAsync` because a repeated subject is a `23505` at commit, never a collapse. The archive event
rides a frozen company's commit only because `UserNotification` and `OutboxMessage` are on
`ArchivedCompanyWriteGuard.AccountSurface` (`:36, 48`).

**Security (Gate 3) — `security_touching: true`, routed to the Security Reviewer beside the code
reviewer.** All but one of the seven sites (`WindDownCompany` is an admin command) write feed rows and
outbox e-mails from paths that carry **no JWT tenant**: the Stripe webhook arms in
`HandlePaymentNotification` (anonymous; the company comes from the order read past the filter — memory:
tenant-ignoring reads on webhook paths), the guest branch of `OrderFactory`, the recurring materialiser
(`ConfirmRecurringOrder`), the daily `RetryFailedUserDeletions` job (fresh scope, override per request)
and the wind-down / archive consumers (override per company). The reviewer checks that every site names
the company from the *subject* it
already holds — never from the ambient override, which under a job loop is the last company seen — that
`OrderFactory`'s null-tenant guard writes nothing rather than stamping the wrong company, that the
args carry ids and amounts only (S6 — no e-mail, name or free text reaches the feed or the mail body),
and that the archive site's write lands on the `AccountSurface` allow-list and nowhere else (S8). E-mail
fan-out is a side-effecting command on Gate 3's own list.

## Status log

- 2026-09-19 — filed by the docs lane from the batch-6 panel; runs after T-0774 on the backend lane.
- 2026-09-19 (review) — `todo`, no owner: it was filed `in_progress` for a lane that does not exist yet;
  the backend lane flips it when it picks the ticket up. `security_touching` corrected to `true` —
  tenant-ignoring writes on webhook and job paths and an e-mail fan-out are each on Gate 3's own list; the
  routing note above says what the Security Reviewer looks at.
