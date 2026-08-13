# Pay, periods, invoices and payouts

What a cleaner earns, when it is closed, and how it reaches their bank.

## The path

```mermaid
flowchart LR
  A[Order completed] --> B[OrderEmployeePay row]
  B --> C[Pay period]
  C -->|close| D[EmployeeInvoice]
  D -->|mark paid| E[Payout]

  classDef gate fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
  class C,D gate
```

**One pay row per assigned employee**, with no crew-size term. That single fact is why there is no
spare seat and why the order seat needs a database-level arbiter — a second cleaner on a one-seat job
is a second full wage against an unchanged customer price.

The formula, and why `extrasPay` is not what it sounds like, is in
[Business rules](/product/business-rules#cleaner-pay).

## Periods are a state machine, and every transition is gated

| Transition | Requires |
|---|---|
| Close | period is `Open` |
| Reopen | period is **not** `Paid` |
| Mark paid | period is `Closed` |
| Delete | period is `Open` |
| Update | period is `Open` |

A paid period cannot be reopened. That is the point of the state: it is the boundary after which the
numbers stop moving.

## Numbering is allocated, never derived

Both the invoice number and the payout variable symbol come from an atomic `ON CONFLICT` counter, and
both carry a unique index. A number is **claimed**, not computed from a row count — a count is not
unique under concurrency, and a duplicate variable symbol is a payment that reconciles against the
wrong invoice.

> The counter is deliberately **global**, not per-tenant, because the unique index behind it is global.
> A tenant-keyed counter under a globally-unique index means two tenants both allocate ordinal 1 and
> the second insert becomes a 500 on the payroll path.

## Payout details never ride an employee DTO

Three routes, three shapes, and a frozen surface test that they are the only DTOs in the feature
allowed to carry a payout identifier:

| Route | Carries |
|---|---|
| the cleaner's own | full identifiers |
| admin list/detail | a masked account only — **there is no unmasked field on the record at all** |
| admin reveal | full identifiers |

The reveal is a **command rather than a query**, precisely so the existing audit engine records it. The
audit trail is the compensating control for storing this in plaintext.

## Edge cases

| Case | What happens |
|---|---|
| Close a period twice | Refused — it is no longer `Open`. |
| Reopen a paid period | Refused. |
| Two invoices allocate a number at once | The `ON CONFLICT` counter serialises them; the unique index is the backstop. |
| A cleaner with no payout destination | Blocked by the profile-completeness gate before they can work. |
| Bonus or deduction applied later | Re-clamps the same core identically, because the clamp bounds are persisted on the row. |
