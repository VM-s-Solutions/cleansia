# Role — CompanyArchive (the freeze, the bundle, the write guard) (ADR-0064 D3, accepted 2026-09-16) (CRC card)

> Introduced by **ADR-0064** (`docs/decisions/adr-0064.md`, **`accepted`** 2026-09-16; owner ruling
> Q-TENANCY-03: *"Archive is also a good functionality to introduce in the beginning."*). Shipped in
> T-0763. The files: `Core.AppServices/Features/CompanyLifecycle/ArchiveCompany.cs` (the request — the
> freeze), `Core.AppServices/Services/CompanyArchiveService.cs` + `Features/CompanyLifecycle/Archive/
> CompanyArchiveRecords.cs` (the bundle), `Functions.Core/Handlers/CompanyArchiveHandler.cs` (+ the
> function and its poison twin), `Infra.Database/ArchivedCompanyWriteGuard.cs` + the guard in
> `CleansiaDbContext.CommitAsync`, `Core.Domain/Tenancy/{CompanyArchivedException,IArchiveWriteGate,
> ISchemaVersionReader}.cs`, `Config/Filters/ArchivedCompanyWebhookAcknowledgeFilterAttribute.cs`,
> `Core.AppServices/Tenancy/ArchivedCompanyDeadLetter.cs`.

## Responsibility (one sentence)

Once a deactivated, wound-down company's books are settled and the chargeback horizon has passed, **freeze
the books at the admin's click**, build a sealed bundle of them — JSON Lines per books table, the receipt
and payout-invoice PDFs, a manifest with a hash per file — into `company-archives/<tenantId>/<freeze>/`,
stamp the manifest's hash on the row, and from the freeze on **refuse every write to the company's books
at the one seam every write crosses**, letting through only the person's rows and the law's own writes.

## Collaborators

- **`ArchiveCompany.Command()`** → `company.archive`. The validator reads
  [`ICompanySettlementReader`](./company-settlement-reader) and refuses in order: `tenant.not_found`,
  `company.not_deactivated`, `company.wind_down_not_requested`, `company.has_open_orders`,
  `company.has_orders_awaiting_pay`, `company.has_orders_awaiting_receipt`,
  `company.has_receipts_awaiting_fiscal_registration`, `company.has_pending_refunds`,
  `company.has_active_memberships`, `company.has_credit_balances`, `company.has_open_pay_period`,
  `company.has_unpaid_invoices`, `company.has_uninvoiced_pay`, `company.has_open_disputes`,
  `company.within_chargeback_horizon` (`LatestCardPaidCleaningDateTime + lifecycle.chargeback_horizon_days`
  still ahead), `company.archived`; a frozen, un-archived company is admitted (*Build archive again*). The
  handler's only write is `tenant.RequestArchive(adminId, now)` — **the freeze** — plus one outbox message
  on `company-archive` (`archive:{tenantId}:{now:yyyyMMddHHmmss}`, deduplicated on the outbox) carrying the
  row's `ArchiveRequestedOn`; a re-run stamps nothing twice.
- **`CompanyArchiveService.RunAsync(tenantId, requestedOn)`** — under the envelope's override; a permanent
  no-op when the company is missing, already archived, not frozen, or when the message's instant names a
  folder the row's freeze does not. Writes, in order, `books/orders.jsonl`, `order-status-history`,
  `order-employee-pays`, `order-receipts`, `refunds`, `disputes` (nested lines), `pay-periods`,
  `employee-invoices`, `employees`, `credit-accounts`, `credit-transactions`, `promo-codes`,
  `promo-code-redemptions`, `company-info`, `fiscal-counters`, `payout-reference-counters`,
  `tenant-configurations`, `audit/admin-action-audits.jsonl`, `audit/employee-action-audits.jsonl` — each
  keyset-paged (500) and streamed through `IBlobContainerClient.CreateFileForWritingAsync`, hashed as
  written; then every receipt PDF (`CopyAsync` from `generated-receipts` by `OrderReceipt.BlobName` to
  `receipts/<ReceiptNumber>.pdf`) and every payout-invoice PDF (from `EmployeeInvoice.PdfBlobUrl` to
  `payout-invoices/<InvoiceNumber>.pdf`), each hashed from the bytes that landed; then `manifest.json`
  **last** and **only if absent** — tenant id and name, the ADR id, the schema version
  (`ISchemaVersionReader` → the `Initial` migration id), the freeze and build instants, the `CompanyInfo`
  rows as of the freeze, and per file its path, row count and SHA-256. Then `tenant.MarkArchived(sha256 of
  the manifest that landed, now)` and one commit.
- **`CompanyArchiveRecords`** — one `record` per file; the projection is the rule. `orders.jsonl` carries
  none of the members `Order.AnonymizeCustomerData()` assigns and no street — the country and city, the
  money, the tender, the discounts, the cancellation facts, the Stripe ids, `ReceiptId`/`ReceiptNumber`,
  nested extras. `disputes.jsonl` carries the case, never `Description`, `ResolutionNotes`, messages,
  evidence or the customer's `UserId`. `employees.jsonl` is `Id, LegalEntityName, RegistrationNumber,
  WorkCountryId, ContractStatus`. `employee-invoices.jsonl` has no `AdminNotes`; `promo-codes.jsonl` no
  `Description`; `admin-action-audits.jsonl` `ActorId`, not `ActorEmail`. **Not in the bundle:** `Users`,
  `UserConsents`, `CustomerActionAudits`, `EmployeePayoutDetails` (a bank account), `UserMemberships`,
  notifications, devices, tokens, notes, issues, photos, reviews, saved addresses, carts, templates,
  loyalty. `CompanyArchiveRecordGuardTests` fails any record member whose name **contains** `Email, Phone,
  Passport, Nationality, EmergencyContact, AccessInstructions, SpecialInstructions, Notes, Description,
  ResolutionNotes, Secret, Password, Ip, Device, Token, Message, Evidence, Review, CustomerName, FirstName,
  LastName, HolderName, Street, HouseNumber, PostalCode, BirthDate, Iban, AccountNumber, BankCode, Swift`.
- **`ArchivedCompanyWriteGuard.AccountSurface`** + **`CleansiaDbContext.CommitAsync`** — after the stamp
  loop, before `SaveChangesAsync`: the distinct `TenantId`s of every `Added`/`Modified`/`Deleted`
  `ITenantEntity` whose type is **not** on the account surface; nothing touched or the gate open → save;
  otherwise one `Tenants` read per unknown company (memoised per context instance) and
  `CompanyArchivedException(tenantId)` for a frozen one. The account surface — the person's rows:
  `User`, `RefreshToken`, `Device`, `LiveActivityToken`, `Cart`, `SavedAddress`, `UserConsent`,
  `UserNotificationPreferences`, `UserNotification`, `GdprRequest`, `UserStripeCustomer`, `UserMembership`,
  `MembershipBenefitUsage`, `LoyaltyAccount`, `LoyaltyTransaction`, `ReferralCode`, `Referral`, the three
  audit tables, `OutboxMessage`, `DeadLetter`. **`CreditAccount` is books.** Everything else is books by
  default; `ArchivedCompanyWriteGuardRosterTests` walks `ctx.Model` and fails an unsorted stamped type,
  and names the **nine** tenantless children of books rows the guard cannot see (`DisputeLine`,
  `CreditTransaction`, `OrderReviewLine`, `OrderExtra`, `DisputeMessage`, `DisputeEvidence`,
  `OrderEmployee`, `OrderService`, `OrderPackage`), failing on a tenth.
- **`IArchiveWriteGate`** (scoped; `IsOpen`; `OpenForLegalObligation(reason)`) — the law's writes pass:
  `DataRetentionBackgroundService` opens it around the per-company loop (`"data retention"`),
  `GdprDeletionService` around the walk (`"erasure"`). `LegalObligationGateCallSiteTests` reads the
  sources and pins exactly those two files.
- **`RequestValidationExceptionFilterAttribute`** — the second arm: `CompanyArchivedException` → **409**
  ProblemDetails with one error `TenantId → tenant.archived`; the customer audit failure row is written as
  for any refused act.
- **`ArchivedCompanyWebhookAcknowledgeFilterAttribute`** on the three `PaymentController.Webhook` actions
  (Customer, Partner, Mobile.Customer) — buffers the body; a `CompanyArchivedException` at the commit is
  handed to **`ArchivedCompanyDeadLetter`** (a fresh scope, the frozen company's override,
  `IDeadLetterStore.RecordAsync("stripe-webhook", body, "tenant.archived:{id}")`, an Error log) and the
  action answers **200** — Stripe must always see 2xx. `CalculateOrderPayHandler` and
  `GenerateReceiptHandler` classify the same exception as permanent through the same helper (dead-letter
  under their queue name, ack).
- **`company-archives`** — the eighth blob container (`Constants.BlobContainers.CompanyArchives`, in
  `storage.bicep`; created on first write like every container); `company-archive` the ninth queue with
  its poison twin and alert.

## Does NOT know

- **Whether the books are settled.** The validator asks the reader; the service trusts the freeze.
- **The person.** Identity lives on in the database under retention and erasure, and in the receipt and
  invoice PDFs the bundle already holds; no `Users` file, no bank account, no free text.
- **How to un-archive.** There is no path from Frozen or Archived back; a bundle that exists and books
  that changed after it is the one state the freeze exists to make impossible. Unfreezing is an owner's
  SQL step, if ever.
- **Who reads the bundle.** Retrieval is an operations step in the storage account until T-0748 gives a
  role something to read it with; no DTO carries the path, and the page shows the hash so a copy in hand
  can be checked against the row.
- **A storage lock.** A time-based immutability policy on the container is O-6, not applied; the hash on
  the row is the anchor.

## Invariants a reviewer checks

1. **The freeze is `ArchiveRequestedOn`, not `ArchivedOn`** — `TC-LC-ARCH-2`: after the request and before
   any bundle, an `Added` `OrderReview`, a `Modified` `OrderReceipt` and a `Modified` `CreditAccount` of the
   company throw at commit; an `Added` `RefreshToken`, `UserConsent`, `CustomerActionAudit` and a
   `Modified` `UserMembership` commit; an erasure and the retention sweep commit through the gate.
2. **The manifest is written last and uploaded if absent**; `MarkArchived` takes the hash of the manifest
   that landed, so two overlapping builds cannot leave the row disagreeing with the blob; a second
   delivery of a finished archive is a no-op.
3. **The source blobs survive the copy** — the archive copies, it does not move; a customer still
   downloads their receipt.
4. **The guard reads `Tenants` once per company per context** and nothing at all on a commit that
   touches only the account surface (`TC-LC-ARCH-2`'s command-count assertion).
5. **A signed chargeback event for a frozen company answers 200**, leaves a `DeadLetter` row with
   `SourceQueue = "stripe-webhook"` and the Error line, and writes no `Dispute` (`TC-LC-ARCH-3` on the
   Customer host).
6. **The record guard and the roster test fail on the next thing** — a record member named `Email` or
   `Iban`, an unsorted stamped type, a tenth tenantless child, a third gate caller.

## Watch-list

- **Q-LC-02 / Q-LC-03**: whether the owner wants the estate (users, consents, audit rows) in the bundle
  after all, and whether the `UserId` on `credit-accounts.jsonl` and `promo-code-redemptions.jsonl`
  belongs in a ten-year copy (an architect ruling pending). Either is a record member and a line in the
  layout, not a new mechanism.
- **After ten years** (O-4) is a later ADR with an accountant in the room; nothing here deletes.
