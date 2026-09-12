# Pay, periods, invoices and payouts

What a cleaner earns, when it is closed, which document it becomes — and where the platform stops,
because it does not move the money.

## The path

```mermaid
flowchart LR
  A[Order completed] --> B[OrderEmployeePay row]
  B --> C[Pay period]
  C -->|close| D[EmployeeInvoice, one per currency]
  D -->|approve| E[Approved]
  E -->|mark paid| F[Transfer recorded]

  classDef gate fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
  class C,D,E gate
```

**There is no payout execution path.** The platform issues the document, an admin keys the transfer in
a bank by hand, and `MarkInvoicePaid` records that it happened, with a transfer note. Nothing here talks
to a bank, a PSP payout account or a SEPA file — which is why approval, not payment, is where the
platform does its last checking (below).

**One pay row per assigned employee**, with no crew-size term. That single fact is why there is no
spare seat and why the order seat needs a database-level arbiter — a second cleaner on a one-seat job
is a second full wage against an unchanged customer price.

The formula, and why `extrasPay` is not what it sounds like, is in
[Business rules](/product/business-rules#cleaner-pay). A rate is an amount in a currency, and the pay
writer reads only rates in the order's currency — so every pay row is in the currency of the order that
earned it.

## One invoice per employee, per period, per currency {#one-invoice-per-currency}

A pay row is in the currency of its order, and a tax document is in one unit. A cleaner who worked a
CZK job and a EUR job in the same period therefore holds pay in two units, and the close produces **one
payout invoice per currency** — each with its own allocated variable symbol — and sends **one
period-closed e-mail per document**, because the template carries a single attachment. The unique
index is `(EmployeeId, PayPeriodId, CurrencyId)`.

The invoice's currency is **derived from the pay rows it invoices** (`EmployeeInvoice.CreateFromOrderPays`),
never supplied — not from the employee, not from their work country, not from a preference field. That
is also why a mixed-currency period is no longer refused. The old refusal had no admin action that could
resolve it — a EUR job cannot be made a CZK job — so the rows sat un-invoiced forever while the
reconciliation sweep re-enqueued the pair every tick.

The manual admin path (`GenerateInvoice`) does what the auto-close batch does: it groups the employee's
unassigned pay by currency, claims every payout reference **before** staging any invoice, writes one
invoice per group and flushes them together, so a period is invoiced whole or not at all. Its
already-exists guard is per currency too — a late pay row in a currency the period has already invoiced
is refused; one in a currency still open is not.

The reconciliation sweep (ADR-0002 D3.4) matches pay rows against invoices on
`(PayPeriodId, EmployeeId, CurrencyId)`, so a pair whose CZK pay is invoiced and whose EUR pay is not is
still a candidate. The message key stays per pair — `invoice:{payPeriodId}:{employeeId}` — and one
generation invoices every currency the pair still has open.

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

## Approval is the last point the platform can refuse {#approval-is-the-last-refusal}

An invoice is generated `Pending`, approved by an admin, and marked paid once the transfer has been
keyed (it can also be rejected, disputed or cancelled along the way). The two transitions on the money
path are gated:

| Transition | Requires |
|---|---|
| Approve | invoice is `Pending`, **and the cleaner's payout account holds the invoice's currency** |
| Mark paid | invoice is `Approved`. `Paid` is terminal — a second mark is refused rather than overwriting the first actor's record of a transfer that already left the bank |

The currency rule reads `EmployeePayoutDetails.CurrencyId` — the currency the cleaner declared their
account holds — and takes an undeclared account to hold the platform default, which is the assumption
every destination was collected under before the column existed. A mismatch is refused as
`payroll.invoice.payout_currency_mismatch`; the cleaner corrects the declaration (or the account) and
the admin approves again.

Why here and not earlier or later: generation is too early — it would withhold a numbered tax document
that already carries an allocated payout reference — and Mark paid is too late, because the money has
left. Approval is the admin's commitment to transfer, the transfer is keyed by hand, and so this is the
last moment the platform is still in the loop.

A cleaner with **no payout record at all passes this rule.** ADR-0034 D7 describes an issuance gate —
refuse when the record is absent, `Scheme` is null or `Status != Provided` — and it is not implemented
anywhere on the payroll path. What stops such a cleaner is the profile-completeness gate, before they
can take work. The record itself is described in
[Role — EmployeePayoutDetails](/domain/roles/employee-payout-details).

## My Pay shows one currency

The cleaner's period view (`GetPeriodPays`) is denominated in a single currency, and everything on it —
totals and rows — is filtered to that currency. The view is the cleaner's resolved currency (work
country → platform default, the one the partner dashboard labels with) when the period holds an invoice
in it or no invoice at all; when the period is invoiced only in another currency, that invoice's own
currency wins, because the payout document is what the cleaner holds and "My Pay" disagreeing with it
was the defect the field exists to close. Pay in any other currency is not on that screen until the
per-row DTO carries a currency, which it does not yet.

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

The declared account currency (`CurrencyId`) is not an identifier: it rides the cleaner's own view, the
admin's masked view and the GDPR export, and the reveal does not carry it because the masked view
already does.

## Edge cases

| Case | What happens |
|---|---|
| Close a period twice | Refused — it is no longer `Open`. |
| Reopen a paid period | Refused. |
| Pay in two currencies in one period | Two invoices, two references, two e-mails; the reconciliation sweep re-enqueues a pair while any of its currencies is still un-invoiced. |
| Approve an invoice in a currency the payout account does not hold | Refused — `payroll.invoice.payout_currency_mismatch`. The cleaner updates the declaration, the admin approves again. |
| A cleaner who never declared an account currency | Treated as holding the platform default at approval. |
| Two invoices allocate a number at once | The `ON CONFLICT` counter serialises them; the unique index is the backstop. |
| A cleaner with no payout destination | Blocked by the profile-completeness gate before they can work. |
| A cleaner with a legacy `IBAN` but no payout record | Passes the completeness gate (it reads `HasPayoutDetails || IBAN`) and passes approval — there is no record to compare, and D7's issuance gate is not built. |
| Bonus or deduction applied later | Re-clamps the same core identically, because the clamp bounds are persisted on the row. |
