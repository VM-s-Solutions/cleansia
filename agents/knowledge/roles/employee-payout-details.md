# Role — `EmployeePayoutDetails` (CRC card)

> Introduced by **ADR-0034** (`agents/backlog/adr/0034-partner-payout-details-...md`, `proposed`).
> A child entity of the `Employee` aggregate in `Cleansia.Core.Domain.Users` (or `.EmployeePayroll`),
> `: Auditable, ITenantEntity` — the house archetype (`EmployeePayConfig`, `EmployeeInvoice`), **not** an
> EF owned type (the repo has zero `OwnsOne`/`OwnsMany`).
> **Cardinality one per employee**, enforced by a unique index on `(TenantId, EmployeeId)` — the
> reversible form of "several destinations, one primary".

## Responsibility (one sentence)
Hold **one cleaner's payout destination** as a *scheme-discriminated* set of identifiers — the scheme
naming which subset is meaningful, `BankCountryId` naming the country whose banking practice governs the
format, and `Status` naming whether the destination is usable for a payout — so the payment block on the
cleaner's invoice can be rendered in every form that destination requires, and so a destination that
cannot be trusted blocks *issuance* without blocking the *cleaner*.

## Collaborators
- **`Employee`** — its owner. `Employee.IsProfileComplete()` asks only *"does this record exist?"*
  (ADR-0034 D7); `Employee.Anonymize()` clears it.
- **`CountryConfiguration.PayoutScheme`** (via `BankCountryId`) — supplies the scheme. The *only*
  per-country input; there are no bank labels/formats on `CountryConfiguration` (D3).
- **`IPayoutDetailsValidator`** — the sole authority on whether a proposed set of identifiers is
  acceptable. The entity never validates itself.
- **`Employee.WorkCountryId`** — the cross-border counterparty: SWIFT becomes required when
  `BankCountryId != WorkCountryId` (D2).
- **`EmployeeInvoice` / the payout-invoice renderer (T-0522)** — reads the local pair, the IBAN and the
  SWIFT to build the payment block; refuses to issue when `Status != Provided`.
- **The backfill script (T-0518)** — the *only* writer of `LegacyRawValue`, once, ever.

## Does NOT know
- **Whether its own values are valid.** The checksums (ISO 7064 mod-97, the CZ/SK weighted mod-11), the
  scheme resolution and the required-field set all live in `IPayoutDetailsValidator`. An entity that
  self-validates would have to know the country rules, which is exactly the coupling D3 removes.
- **Which country a cleaner lives in, is a national of, or is registered for business in.** It knows the
  country of **the bank**, and nothing else. `BusinessCountryId` in particular is not persisted anywhere
  and therefore governs nothing (D2).
- **How money is moved.** No transfer, no PSP call, no payment rail. `ProviderAccountRef` holds an
  **id** for a future PSP payout account (D9) — and **never a card number**; a PAN is not a field on this
  entity under any circumstances.
- **What an invoice must legally contain.** Variabilní symbol lives on `EmployeeInvoice
  .VariableSymbol`; konstantní symbol and due date are T-0508's. None of them belongs here.
- **Whether a cleaner may take orders.** That is `Employee.IsProfileComplete()`; this record's `Status`
  gates **payout issuance** only (D7).
- **How it is displayed or masked.** Masking, the owner-or-admin read authorization and the audited
  admin reveal are the read contract's job (D8), not the entity's.

## Invariants a reviewer checks
- One row per `(TenantId, EmployeeId)`; no `IsPrimary` field exists.
- `Scheme = null` ⟺ the row is unusable for payout ⟺ `Status = NeedsReconfirmation`.
- `AccountPrefix` / `AccountNumber` / `BankCode` are **text**, never numeric — leading zeros are
  significant.
- For `CzskDomesticWithIban`, `Iban` is **server-derived** from the local parts; a supplied IBAN that
  disagrees is rejected.
- `LegacyRawValue` is written only by the backfill and is dropped by its follow-up ticket.
- Every field is covered by `Employee.Anonymize()` and asserted absent from audit JSON by
  `EmployeeUserAuditCoverageTests`.
