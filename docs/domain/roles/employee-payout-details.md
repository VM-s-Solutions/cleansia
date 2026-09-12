# Role — `EmployeePayoutDetails` (CRC card)

> Introduced by **ADR-0034** (`docs/decisions/adr-0034.md`, **`accepted`**
> 2026-08-02 — panel amendments folded in).
> A child entity of the `Employee` aggregate in `Cleansia.Core.Domain.Users` (or `.EmployeePayroll`),
> `: Auditable, ITenantEntity` — the house archetype (`EmployeePayConfig`, `EmployeeInvoice`), **not** an
> EF owned type (the repo has zero `OwnsOne`/`OwnsMany`).
> **Cardinality one per employee**, enforced by a unique index on
> `(TenantId, EmployeeId) .AreNullsDistinct(false)` **plus an app-level create-or-update guard** — the
> reversible form of "several destinations, one primary". Nulls-not-distinct is required because
> `TenantId` is nullable and Postgres would otherwise treat every single-tenant row as unique; it is
> precedented (`FiscalCounterEntityConfiguration.cs:28`, `LiveActivityTokenConfiguration.cs:28`).
> **Mutated in place** — exempt from ADR-0007 D1's `Deactivate` default; erasure **deletes**.
> **Amended 2026-09-12 (T-0708) — `CurrencyId`**, the currency the account **holds**: nullable, FK to
> `Currency` with `ON DELETE RESTRICT`. Declared by the cleaner, never derived — a bank's country does
> not decide it (a Czech bank sells EUR accounts) and the platform has no other source. `null` ⇒ not
> declared, which `ApproveInvoice` reads as the platform default. Absent on the wire ⇒ **unchanged**,
> not cleared: the field is on both partner hosts' `UpdateBankDetails` contract but no shipped client
> populates it yet, and a full-replace save from a client that does not know the field must not revert
> a declaration made from one that does. Shown on `MyPayoutDetails`, `MaskedPayoutDetails` and the GDPR export; not on the
> reveal, because it is not an identifier.

## Responsibility (one sentence)
Hold **one cleaner's payout destination** as a *scheme-discriminated* set of identifiers — the scheme
naming which subset is meaningful, `BankCountryId` naming the country whose banking practice governs the
format, `CurrencyId` naming the currency the account holds, and `Status` naming whether the destination
is usable for a payout — so the payment block on the cleaner's invoice can be rendered in every form that
destination requires, and so an invoice in a currency the account does not hold is refused at
*approval*, the last point before the transfer is keyed by hand.

## Collaborators
- **`Employee`** — its owner. `Employee.IsProfileComplete()` asks only *"does this record exist?"*
  (ADR-0034 D7) — **and it asks the `Employee.HasPayoutDetails` column, never this navigation**, because
  the repo has no lazy loading and an unloaded navigation is indistinguishable from "no payout details"
  (which would 403 every cleaner on the partner surface). `Employee.Anonymize()` sets that flag `false`
  and **does not** attempt to clear this record.
- **`GdprDeletionService`** — the *only* thing that erases this record, through an **id-keyed,
  set-based** repository call, so erasure is correct regardless of what the caller `Include`d.
- **`CountryConfiguration.PayoutScheme`** (via `BankCountryId`) — supplies the scheme. The *only*
  per-country input; there are no bank labels/formats on `CountryConfiguration` (D3).
- **`IPayoutDetailsValidator`** — the sole authority on whether a proposed set of identifiers is
  acceptable. The entity never validates itself.
- **`UpdateBankDetails`** — the only writer, and the only writer of `CurrencyId`. The currency is
  existence-checked against `Currency` (`currency.invalid`) — existence only, not `IsActive`, because a
  cleaner may hold an account in a currency the platform does not operate yet, and that is a fact about
  their bank, not a booking.
- **`Employee.WorkCountryId`** — the cross-border counterparty: SWIFT becomes required when
  `BankCountryId != WorkCountryId` (D2).
- **The payout-invoice renderer** (`PayPeriodBackgroundService`, `RegenerateInvoicePdf`) — reads the
  local pair, the IBAN and the SWIFT to build the supplier's payment block, and prints what it finds.
  It does **not** refuse: nothing on the issuance path reads this record's presence or its `Status`,
  so the issuance gate ADR-0034 D7 describes is not built.
- **`ApproveInvoice`** — the one place on the money path that reads this record. It compares
  `CurrencyId` (undeclared ⇒ the platform default) with the invoice's currency and refuses
  `payroll.invoice.payout_currency_mismatch`; a missing record passes. Why approval and not generation
  or payment is in [Pay and payouts](/flows/pay-and-payouts#approval-is-the-last-refusal).
- **`Currency`** (via `CurrencyId`) — the FK is `ON DELETE RESTRICT`, so `DeleteCurrency` answers
  `currency.in_use` while any record declares it.
- **The backfill script (T-0518)** — exists only under D7.3 **Branch B**. It writes the parked legacy
  value to the **non-mapped `payout_legacy_import` staging table**, never to this entity.

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
- **Which currency the platform pays in.** An invoice's currency comes from the pay rows it invoices —
  never from this record, the employee or the work country. This record says what the account *holds*;
  the two are compared at approval, not reconciled.
- **Whether a cleaner may take orders.** That is `Employee.IsProfileComplete()` reading
  `Employee.HasPayoutDetails`; this record's `Status` was designed to gate payout issuance (D7) and
  gates nothing today. **This record is never on the path that answers "may this person work" —
  deliberately, so it cannot take the workforce off the job board by being unloaded.**
- **How it is displayed or masked.** Masking, the owner-or-admin read authorization and the audited
  admin **reveal command** are the read contract's job (D8), not the entity's. It does not know that
  `LastRevealedAt`/`RevealCount` exist to make the reveal auditable by `AdminMutationGate` — it just
  holds them.
- **That it is being erased.** `GdprDeletionService` removes it by id; the entity has no
  `Anonymize()` of its own and the parent's cannot reach it.

## Invariants a reviewer checks
- One row per `(TenantId, EmployeeId)`, with `.AreNullsDistinct(false)` **and** an app-level
  create-or-update guard; no `IsPrimary` field exists.
- **`Employee.HasPayoutDetails` == (a row exists here)** — pinned by an integration test across the
  table. This is the one invariant that spans the parent and the child, and it is the reason the gate is
  safe.
- `Scheme = null` ⟺ the row is unusable for payout ⟺ `Status = NeedsReconfirmation`.
- `CurrencyId` is either `null` or the id of an existing `Currency`, and an absent value on the wire
  never clears it. `null` is a legal state — "not declared", read as the platform default by
  `ApproveInvoice` — not an error and not `NeedsReconfirmation`.
- `AccountPrefix` / `AccountNumber` / `BankCode` are **text**, never numeric — because they are digit
  strings, not quantities. **Leading zeros are NOT identity** (`123456` and `0000123456` derive the same
  IBAN): store the zero-padded canonical form and **compare on the derived `Iban`**, never on the typed
  parts.
- For `CzskDomesticWithIban`, `Iban` is **server-derived** from the local parts; a supplied IBAN that
  disagrees is rejected.
- **No field holds a card PAN**, and the validator rejects Luhn-valid 13–19-digit input on every write
  path — the invariant is a runtime guard, not a name check.
- No paged or list query `.Include`s this navigation; no DTO outside the named payout family carries a
  payout identifier (frozen-surface test).
- Every field is asserted absent from audit JSON by `EmployeeUserAuditCoverageTests`, **with a distinct
  sentinel per field** (one `DoesNotContain` across ten fields passes if nine are checked).
- An integration test through `GdprDeletionService`'s **real query shape** asserts zero rows remain for
  an erased employee. An in-memory test with a hand-populated navigation does not discharge this.
