# Role — CompanySettlementReader (ADR-0064 D3, accepted 2026-09-16) (CRC card)

> Introduced by **ADR-0064** (`docs/decisions/adr-0064.md`, **`accepted`** 2026-09-16). Shipped in T-0760
> (the reader and the query), read by T-0763's archive validator. The files:
> `Core.Domain/Tenancy/ICompanySettlementReader.cs` + `CompanySettlementFacts.cs` (the contract and the
> record), `Infra.Database/Repositories/CompanySettlementReader.cs` (the counts),
> `Core.Domain/Tenancy/WindDownCutoff.cs` (the market-local midnight the reader and the sweep share),
> `Core.AppServices/Features/TenantSettings/TenantSettingCatalog.cs` (`lifecycle.chargeback_horizon_days`).

## Responsibility (one sentence)

Answer, for **the ambient company alone** and from **live rows rather than stamps**, every fact that must
be zero before its books may be sealed — and the two dates and three counts the lifecycle page shows
beside them — so that the archive validator and the page refuse and explain from the same numbers.

## Collaborators

- **`ICompanySettlementReader.ReadAsync(ct)`** → `CompanySettlementFacts`: `OpenOrders` (the offerable
  statuses — New, Confirmed, OnTheWay, InProgress), `OpenOrdersOnOrAfterWindDownFrom` (open orders whose
  `CleaningDateTime` is at or past midnight of `WindDownFrom` in the address's market zone — UTC for an
  address outside the company's markets; zero when no date is set), `ActiveTemplates`,
  `ActiveMemberships` (`Status == Active`, cancellation requested or not), `CreditBalances` (accounts with
  `Balance > 0`), `PendingRefunds` (`Refunds.Status == Pending`, any purpose), `OrdersAwaitingPay`
  (`Completed && !EmployeePayCalculated`), `OrdersAwaitingReceipt` (cash or paid, **not cancelled**, and
  no receipt row — the reconciliation sweep's own predicate), `ReceiptsAwaitingFiscalRegistration`
  (`FiscalNextRetryAt != null`), `OpenPayPeriods`, `UnpaidInvoices` (an invoice neither `Paid` nor
  `Cancelled` in a `Closed` period), `UninvoicedPayRows` (`EmployeeInvoiceId == null` in a `Closed`
  period), `OpenDisputes` (Pending, UnderReview, WaitingForResponse, Escalated), and
  `LatestCardPaidCleaningDateTime` (the latest `CleaningDateTime` among card orders whose money moved —
  Paid, PartiallyRefunded, Refunded; null when the company never took a card).
- **`WindDownCutoff.Utc(date, timeZoneId)`** — midnight of the date in the market's zone, UTC when the
  zone is null or unknown; the one function the reader counts by and the sweep cancels by, so the page's
  count and the sweep's selection agree.
- **`TenantSettingCatalog.ChargebackHorizonDays`** (`lifecycle.chargeback_horizon_days`, category
  `lifecycle`, default **180**, min **0**, max **730**) — the caller adds it to
  `LatestCardPaidCleaningDateTime` to get `ChargebackHorizonEndsOn`; the reader does not read settings.
  Editable on the admin's *Company settings* page like any catalogue key.
- **`ArchiveCompany.Validator`** — reads the facts once and refuses on the first non-zero in the order of
  the ADR-0064 D3 table (`company.has_open_orders` … `company.has_open_disputes`,
  `company.within_chargeback_horizon`).
- **`GetCompanyLifecycle`** — maps every fact onto `CompanyLifecycleDto` with the horizon date resolved;
  the admin page turns each count into a row that links to the list that settles it (orders, pay periods,
  invoices, disputes) and the horizon row into *archive admissible from ‹date›*.

## Does NOT know

- **Another company.** Every count is a filtered read; the ambient tenant is whatever the request or the
  job set, and there is no tenant parameter.
- **The stamps.** It reads `Tenants.WindDownFrom` for one count and nothing else on the row; whether the
  company is deactivated or frozen is the validator's question to the `Tenant`.
- **How to settle a fact.** A count is a pointer to the admin tools that settle it (mark an invoice paid,
  resolve a dispute, run the wind-down again); the reader offers no act.
- **The horizon's length.** The catalogue does, per company.

## Invariants a reviewer checks

1. **Every count is a filtered read** — no `IgnoreQueryFilters`, no `GetQueryableIgnoringTenant`
   (`CompanySettlementReaderTests` on Postgres seeds a second company and asserts its rows are not
   counted).
2. **A cancelled order owes no receipt** — `OrdersAwaitingReceipt` and `FiscalReconciliationService` share
   the exclusion; a company whose last orders were cancelled by the wind-down is not held un-archivable by
   receipts it will never issue.
3. **The per-market cut-off is computed once, in `WindDownCutoff`**, by the reader and by
   `CompanyWindDownService`; neither carries its own zone math.
4. **The validator's order is the table's order**, and each refusal fires on one seeded fact
   (`ArchiveCompanyTests`).
5. **`lifecycle.chargeback_horizon_days` is in `TenantSettingCatalog.All`** with (180, 0, 730) and renders
   on the Company settings page in five locales (the catalogue spec).

## Watch-list

- A fact the archive should also wait on — a new derived write that trails an order — joins as one
  member, one key and one row in the D3 table, never as a second reader.
