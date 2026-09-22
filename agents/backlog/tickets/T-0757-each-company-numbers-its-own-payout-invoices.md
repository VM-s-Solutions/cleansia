---
id: T-0757
title: Each operating company numbers its own payout invoices
status: done
size: M
owner: —
created: 2026-09-15
updated: 2026-09-15
depends_on: [T-0758]
blocks: []
stories: []
adrs: [ADR-0046, ADR-0061]
layers: [backend, db]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-15 on **Q-TENANCY-02** (ADR-0061 O-2): *"Separate everything — one holding and
child companies per country."* `EmployeeInvoices` numbered from one global `PayoutReferenceCounter`
keyed `(Year)` (ADR-0046 §D3.2 — the cheapest correct shape *while the payer's account was one
account*, with the flip written down), `IX_EmployeeInvoices_VariableSymbol` had no tenant term, and
`InvoiceNumber` was `INV-yyyyMM-` plus five random hex under a global unique index. The ruling
answers the premise §D3.2 was contingent on (Q-VS-03 as well).

## Doing

- `PayoutReferenceCounter : TenantAuditable`, keyed **`(TenantId, Year, Scope)`** under
  `IX_PayoutReferenceCounters_Tenant_Year_Scope` (unique, NULLS NOT DISTINCT); `Scope` is
  `VariableSymbol` or `InvoiceNumber` — two independent series on one table, the `FiscalCounter.IssuerScope`
  shape. The repository takes `ITenantProvider`, writes the ambient company, arbitrates
  `ON CONFLICT ("TenantId", "Year", "Scope")`, keeps the `WHERE "Value" < 999999` cap, the
  empty-`RETURNING` guard and the self-commit exception; with no ambient company the NOT NULL column
  refuses the row (`23502`).
- `IX_EmployeeInvoices_VariableSymbol` → `(TenantId, VariableSymbol)` unique, NULLS NOT DISTINCT,
  filtered `IS NOT NULL`; `InvoiceNumber` → `(TenantId, InvoiceNumber)` unique, NULLS NOT DISTINCT.
- `InvoiceNumber` is **`INV-{yyyy}-{NNNNNN}`** from the second scope
  (`PayoutReferenceAllocator.AllocateInvoiceNumberAsync` / `FormatInvoiceNumber`; refused
  `payroll.invoice.reference_capacity_exhausted` at the cap like the symbol); the dead
  `GenerateInvoiceNumber(prefix)` deleted; `PaymentReference` keeps mirroring the number. The three
  callers (`GenerateInvoice`, `PayPeriodBackgroundService`, `AssignInvoiceVariableSymbol`) already ran
  under a company.
- Review (`8c3beb8a`): the sweep's invoice PDF is addressed by employee id; both invoice-number
  refusals pinned.
- Tests: `PayoutReferenceAllocatorTests` inverted — the counter **is** keyed per company
  (`Two_Companies_Allocate_Independent_Sequences`, `The_Two_Scopes_Do_Not_Share_A_Sequence`,
  `One_Companys_Exhausted_Year_Does_Not_Exhaust_Anothers`,
  `An_Allocation_With_No_Ambient_Company_Is_Refused_By_The_Database`,
  `The_Counter_Table_Is_Keyed_Per_Company_Year_And_Scope` read from `information_schema` /
  `pg_indexes`); the `NullsNotDistinctIndexModelTests` roster is thirteen rows;
  `PayoutReferenceProductionCensusTests.The_Pay_Period_Batch_Numbers_Each_Companys_Invoices_From_Its_Own_Series`;
  `Every_Allocated_Invoice_Number_Is_INV_The_Year_And_Six_Digits`.
- Folded into the `Initial` regen — final id `20260915172310` for Batch 1 (with T-0758), regenerated
  again as `20260915232921` by T-0760 on 2026-09-16; the DEV drop is owed at deploy (MS-2).

## NOT

No prefix inside the VS (`varchar(10)` is spent). No change to the PDF — the company printed was
already the ambient tenant's `CompanyInfo`. No change to receipts (`FiscalCounter`).

## Status log

- 2026-09-15 — shipped in `33dda62d` (T-0757) and `8c3beb8a` (review). Recorded in ADR-0046's
  superseding note (§D1.6, §D2.1, §D3.1/§D3.2 as superseded), ADR-0061 D7/D9 as amended and §Rulings,
  `/domain/roles/payout-reference-allocator` (rewritten), `/domain/model`,
  `/product/business-rules#payout-numbering`, S8, `consistency.md`, the tenancy living note,
  CHANGELOG (Changed).
