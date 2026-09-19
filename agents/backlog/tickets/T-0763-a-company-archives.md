---
id: T-0763
title: A company archives — once every live fact is settled and the chargeback horizon has passed, its books freeze at the request, a sealed bundle goes to blob storage with its hash on the row, and the commit refuses every later write except the law's; the Stripe webhook is acknowledged, never refused
status: done
size: L
owner: —
created: 2026-09-16
updated: 2026-09-16
depends_on: [T-0760, T-0762]
blocks: [T-0764]
stories: []
adrs: [ADR-0064, ADR-0061, ADR-0012]
layers: [backend, db-guard, functions, config, bicep, android-locales, ios-locales, frontend-locales]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0064 D3 and D5. `CleansiaDbContext.CommitAsync` is the seam every write crosses; the retention sweep
and the erasure are the two writers that must keep working on a frozen company;
`RequestValidationExceptionFilterAttribute` is the keyed-exception mapping; `PaymentController.Webhook`
on three hosts is a synchronous request that must always answer 2xx to Stripe;
`OutOfBandAuditFailureSink` is the fresh-scope write shape; `IBlobContainerClient` streams and copies;
`EmployeePayoutDetails` is a bank account and stays out of the bundle.

## Doing (shipped `4d040b59`; review `44d30a28`; cross-ticket review `3f2e8018`)

- **Request.** `ArchiveCompany.Command()` — the validator reads `ICompanySettlementReader` once and
  refuses, after `tenant.not_found`, in the D3 table's order: `company.not_deactivated`,
  `company.wind_down_not_requested`, `company.has_open_orders`, `company.has_orders_awaiting_pay`,
  `company.has_orders_awaiting_receipt`, `company.has_receipts_awaiting_fiscal_registration`,
  `company.has_pending_refunds`, `company.has_active_memberships`, `company.has_credit_balances`,
  `company.has_open_pay_period`, `company.has_unpaid_invoices`, `company.has_uninvoiced_pay`,
  `company.has_open_disputes`, `company.within_chargeback_horizon` (`LatestCardPaidCleaningDateTime +
  lifecycle.chargeback_horizon_days` still ahead), `company.archived`; a frozen, un-archived company is
  admitted (the re-run). Handler: `RequestArchive(adminId, now)` when not yet frozen, one message on
  `QueueNames.CompanyArchive` keyed `archive:{tenantId}:{now:yyyyMMddHHmmss}` (outbox-deduplicated)
  carrying the row's `ArchiveRequestedOn`; `[AuditAction("company.archive")]`; route
  `api/AdminCompanyLifecycle/archive` under `CanArchiveCompany`. **Bicep:** `company-archive` in
  `queueBaseNames` (+ poison) and its alert; `company-archives` in the container list.
- **Bundle.** `Constants.BlobContainers.CompanyArchives = "company-archives"`;
  `CompanyArchiveService.RunAsync(tenantId, requestedOn)` — no-op (logged, acked) when the company is
  missing, already archived, not frozen, or the message's instant names a folder the row's freeze does
  not; nineteen JSON Lines files under `<tenantId>/<requestedOn:yyyyMMddTHHmmssZ>/books/` and `audit/`,
  keyset-paged (500) and streamed through `CreateFileForWritingAsync`, hashed as written; receipt PDFs
  `CopyAsync` from `generated-receipts` by `OrderReceipt.BlobName` and payout-invoice PDFs from
  `EmployeeInvoice.PdfBlobUrl`, hashed from the landed bytes; `manifest.json` last — tenant id and name,
  the ADR id, the schema version (`ISchemaVersionReader` → the `Initial` id), the freeze and build
  instants, the `CompanyInfo` rows, per file its path, row count and SHA-256. **Review (`44d30a28`):**
  the manifest is uploaded **if absent** — two overlapping builds of one frozen company seal with the
  first manifest to land and the row carries *its* hash. Then `MarkArchived(sha256, now)` + commit. The
  record types under `Features/CompanyLifecycle/Archive/CompanyArchiveRecords.cs`: `orders.jsonl` carries
  no member `Order.AnonymizeCustomerData()` assigns and no street; `disputes.jsonl` no `Description` /
  `ResolutionNotes` / messages / evidence — **and no customer `UserId`** (review); `employees.jsonl` =
  `Id, LegalEntityName, RegistrationNumber, WorkCountryId, ContractStatus`; `employee-invoices.jsonl`
  without `AdminNotes`; `admin-action-audits.jsonl` with `ActorId`; `promo-codes.jsonl` without
  `Description`; no `users`, `user-consents`, `customer-action-audits`, `employee-payout-details`,
  `user-memberships` file. `CompanyArchiveRecordGuardTests` fails on a member whose name **contains** (review)
  any of the thirty forbidden parts. `CompanyArchiveHandler` / `CompanyArchiveFunction` /
  `CompanyArchivePoisonFunction`.
- **Guard.** `CompanyArchivedException(tenantId)` and `IArchiveWriteGate` in `Core.Domain/Tenancy`;
  `ArchivedCompanyWriteGuard.AccountSurface` (the D3 list verbatim; `CreditAccount` not on it) and
  `TouchedBooksTenantIds(changeTracker)`; the check in `CommitAsync` after the stamp loop, one `Tenants`
  read per unknown company memoised per context; the gate registered scoped in `Cleansia.Config`,
  null-tolerated in the context; `OpenForLegalObligation` in `DataRetentionBackgroundService`
  (`"data retention"`) and `GdprDeletionService` (`"erasure"`) — `LegalObligationGateCallSiteTests` pins
  the two files; `RequestValidationExceptionFilterAttribute` gains the 409 arm (`tenant.archived` on
  `TenantId`); `ArchivedCompanyWebhookAcknowledgeFilterAttribute` on the three `Webhook` actions →
  `ArchivedCompanyDeadLetter.RecordAsync("stripe-webhook", body, ex)` from a fresh scope under the frozen
  tenant, Error log (**review:** asserted on the host), 200; `CalculateOrderPayHandler` and
  `GenerateReceiptHandler` classify the exception as permanent through the same helper;
  `ArchivedCompanyWriteGuardRosterTests` walks `ctx.Model` — every stamped type sorted, and **nine**
  tenantless children of books types named (`DisputeLine`, `CreditTransaction`, `OrderReviewLine`,
  `OrderExtra`, `DisputeMessage`, `DisputeEvidence`, `OrderEmployee`, `OrderService`, `OrderPackage`),
  not the four the ADR drafted; fails on a tenth.
- **Cross-ticket review (`3f2e8018`):** the invoice download reads the blob by the name the upload
  wrote.
- **NSwag regen** of the admin client (`87ff17e0`). **Locales.** Admin: the thirteen `company.*` refusal
  keys a/b did not add (twenty in all), and `tenant.archived`; customer web/Android/iOS: `tenant.archived`
  under `api.*`.

## NOT

No un-archive, no deletion, no ten-year rule, no bundle retrieval endpoint, no holding role, no storage
immutability policy (O-6). No `Users`, `UserConsents`, `CustomerActionAudits`, `EmployeePayoutDetails`,
`UserMemberships`, notifications, reviews, notes, photos, dispute text, messages or evidence in the bundle
(O-3, C14). No change to the retention windows or `GetAllIdsAsync`. No guard on the raw-SQL writers
(enumerated in ADR-0064 D3). No FK walk in the guard. No classification of the exception in consumers
that do not write books. No admin page (T-0764).

## Acceptance criteria

- [x] AC1 (TC-LC-ARCH-1) — each of the fifteen refusals fires on its seeded fact in the table's order and
      `ArchiveRequestedOn` stays null; horizon override 0 admits the last; a settled company is stamped,
      one message on the outbox, one `company.archive` audit row.
- [x] AC2 (TC-LC-ARCH-2, Postgres, two companies) — B frozen: an `Added` `OrderReview`, a `Modified`
      `OrderReceipt`, a `Deleted` `OrderPhoto`, a `Modified` `CreditAccount` throw
      `CompanyArchivedException` naming B and write nothing; an `Added` `RefreshToken`, `UserConsent`,
      `CustomerActionAudit`, a `Modified` `User`, a `Modified` `UserMembership` and any A row commit; the
      erasure of a B customer commits; the retention sweep anonymises B's two-year-old order.
- [x] AC3 (TC-LC-ARCH-3) — a B customer's review on a frozen B: 409 `tenant.archived` on `TenantId`, the
      failure audit row, no `OrderReview`; a signed `charge.dispute.created` for a B order on the Customer
      host: 200, no `Dispute`, one `DeadLetter` (`stripe-webhook`, `TenantId = B`, `tenant.archived`), the
      Error line; a `calculate-order-pay` message for a B order: a `DeadLetter`, acked on first delivery.
- [x] AC4 (TC-LC-ARCH-4) — the seeded bundle: `books/orders.jsonl` two lines with `ReceiptNumber`, country
      and city and no `CustomerName` / `CustomerEmail` / `CustomerPhone` / `UserId` / `AccessInstructions`
      / street; `disputes.jsonl` one line, one nested line item, no text; `credit-transactions.jsonl` two
      lines; `employees.jsonl` five members; `admin-action-audits.jsonl` without `ActorEmail`; PDFs copied
      byte-identical with the sources intact; no estate file; `manifest.json` last; `ArchivedOn` and
      `ArchiveManifestSha256 = sha256(manifest.json)` stamped after; a second delivery writes nothing.
- [x] AC5 (TC-LC-ARCH-5) — the record guard fails on `Email` and `Iban` members; the roster test fails
      on an unsorted `ITenantEntity` and on a tenth tenantless child; the gate call-site test fails on a
      third caller.
- [x] AC6 — a commit with no books change runs no `Tenants` query; two commits on one context run one.
- [x] AC7 — a build that dies after `orders.jsonl` is rebuilt from scratch on redelivery, hash for hash;
      after five failures the poison twin dead-letters, B stays frozen and un-archived, and
      `ArchiveCompany` admits a re-run.
- [x] AC8 — no DTO carries a blob path; the state reads `Frozen` between the request and the manifest and
      `Archived` after, with the hash.
- [x] AC9 — `storage.bicep` carries `company-archive` and `company-archives`; `queueAlerts.bicep` the
      alert.

## Status log

- 2026-09-16 — shipped `4d040b59`; review `44d30a28`; cross-ticket review `3f2e8018`. Recorded in ADR-0064
  D3 and §*What shipped*; `/domain/roles/company-archive`; `/product/business-rules#company-lifecycle`;
  `/flows/cross-cutting` (the guard, the dead letter on first delivery); `/flows/gdpr-and-audit` (the
  gate); S8; `/architecture/infrastructure` (the container, the queue, the consumer classification);
  `/api/webhooks` (the 200); CHANGELOG. **Open:** Q-LC-03 — the `UserId` on `credit-accounts.jsonl` and
  `promo-code-redemptions.jsonl` awaits an architect ruling.
