# Partner Payout Details — living decision doc

**Topic:** how the platform holds *where a cleaner's money goes* — the shape, the governing country, the
validation, the at-rest posture, and the read contract.
**ADRs:** [ADR-0034](../../backlog/adr/0034-partner-payout-details-shape.md)
(`proposed` 2026-08-02 — the shape) · composes with
[ADR-0017](../../backlog/adr/0017-multi-region-expansion-seam-and-its-composition-with-app-level-tenancy.md)
(per-country variation is config-driven) ·
[ADR-0012](../../backlog/adr/0012-admin-action-audit-log.md) D4.1 (admin audit records ids, not PII).
**Tickets:** T-0517 (this decision) → T-0518 (db) · T-0519 (backend) · T-0520 (web/admin) ·
T-0521 (mobile) · consumed by T-0522 (payout invoice); cross-checked with T-0508 (invoice field spec)
and T-0511 AC5 (the same generality question).
**Owner input:** *"Let's start with CZ but in the way that it's easy to expand in the future — like Bank
Account, Card number and what else needed to make a payment to the employee."* (2026-08-02) + a real
Czech invoice they issued, whose payment block carries `5885638003/5500`, an IBAN, a SWIFT, a variabilní
symbol and a konstantní symbol.

---

## The problem this area exists to solve

A cleaner is an independent supplier who invoices the platform. To pay them, the platform must hold a
payout destination that (a) is **correct** — a wrong digit is a transfer to a stranger or a failed
payroll run — and (b) is **renderable on a legal document** in the forms the destination country's
banking practice expects. Today it holds neither.

### The state before ADR-0034 (verified 2026-08-02)

`Employee.IBAN` is one nullable `varchar(50)` (`Employee.cs:24`, `EmployeeEntityConfiguration.cs:30`)
whose entire server-side validation is `NotEmpty() + Length(15, 34)`
(`ValidationExtensions.cs:122-130`). A 21-character sentence is a valid IBAN to this platform.

Three failure modes compound:

1. **The clients disagree, and one of them corrupts.** Web applies a structural IBAN regex
   (`custom-validators.ts:74-86`); Android and iOS apply a blank check *and strip every non-alphanumeric
   character* (`BankSectionViewModel.kt:75-77`, `BankSectionViewModel.swift:66-75`). A cleaner typing the
   perfectly correct Czech account `19-2000145399/0800` on a phone sends `1920001453990800` — 16
   characters, which **passes** the server check and is stored as an "IBAN", with its separators
   irrecoverably gone. The same input is rejected on web.
2. **The field is a gate on income, not a nice-to-have.** `Employee.cs:283` folds a non-empty `IBAN`
   into `IsProfileComplete()`, which decides whether a cleaner may take orders. "Tighten the validation"
   therefore reads as "take working cleaners off the job board".
3. **One string cannot produce the document.** The owner's own specimen shows the local account number
   **and** the IBAN **and** the SWIFT on one payment block. Three renderings of one account cannot be
   rendered from one opaque string.

---

## The trade-off space

### Shape

| Option | Cost of country #2 | Why it lost / won |
|---|---|---|
| **(a) flat nullable columns on `Employee`**, mirroring `CompanyInfo`'s `BankName`/`BankAccountNumber`/`Iban`/`Swift` | SK free; the first non-IBAN market widens `Employee` itself | **Lost, narrowly.** No discriminator (every reader re-derives which subset is meaningful = a country branch in disguise); the read contract stays broken by construction (`Iban` rides `EmployeeListItem` *because* it is an `Employee` property); and the migration-state fields it needs are not employee attributes. `CompanyInfo` is a **singleton, one company, one country** — it never needed a discriminator, so it is not the precedent it resembles |
| **(b) a discriminated child record** | SK = 1 seed value; non-IBAN market = 1 additive migration + 1 validator; PSP token = **0** | **Won**, with cardinality pinned to one by a unique index |
| **(c) `CountryConfiguration` grows bank label/format/required; the value stays a string** | free | **Lost at country #1** — cannot render the CZ payment block. Also copies a precedent that **no client reads** |
| **(d) `Scheme` + a JSON details bag** | free forever | **Lost.** Zero migrations bought with zero guarantees: no constraint, no index, no uniqueness check, an opaque NSwag blob, and an invoice builder that parses JSON |
| **(e) an uncapped `PayoutMethod` collection** | free | **Lost.** Buys an unrequested capability and immediately owes a "which is primary" rule to five readers. The unique index is its reversible form |

### The principle that decided it (and that T-0511 must match)

> **Generalize along the axis that will actually vary. Pin the axis that will not.**
> **Cardinality is a separate question from kind.**

For payout details the varying axis is the **scheme** (CZ/SK domestic ≠ SEPA ≠ non-IBAN ≠ PSP token);
the count of destinations is not varying. For membership benefits (T-0511) the varying axis **is** the
benefit set. **Both generalize — along different axes.** That is the consistent outcome; a
column-per-benefit answer in T-0511 would not be.

### Governing country

Three country fields exist on the record and are documented as deliberately different: residence
(`Address.CountryId`), work jurisdiction (`WorkCountryId`, admin-set at approval, `Employee.cs:68-81`),
and nationality. A fourth, `BusinessCountryId`, exists **only as a request parameter** —
`UpdateIdentificationInfo.cs:121` uses it at `:77`/`:92` and the handler never assigns it (`:131-150`).
It is sent **only by mobile** (`IdentificationSectionViewModel.kt:96-98`, pre-filled from the *address*
country and overridable); `businessCountryId` has zero occurrences under `src/Cleansia.App`.

**None of the three is the right kind of answer** for a *bank account's* format, because none is a
property of the account. So the record carries **its own `BankCountryId`** — and the rule that
disqualifies `BusinessCountryId` generalizes: **a governing input that is not persisted cannot govern.**

---

## Current shape (as decided by ADR-0034, `proposed`)

```
EmployeePayoutDetails : Auditable, ITenantEntity          // house archetype: EmployeePayConfig
  EmployeeId       NOT NULL   UNIQUE (TenantId, EmployeeId)   ← cardinality 1, reversibly
  Scheme           PayoutScheme?      null ⇒ unusable for payout (legacy park only)
  BankCountryId    FK Country?        the country of the BANK
  AccountPrefix    varchar(6)?   AccountNumber varchar(10)?   BankCode varchar(4)?   ← CZ/SK, text: leading zeros matter
  Iban             varchar(34)?       ← DERIVED for CZ/SK, not collected
  Swift            varchar(11)?       ← required when BankCountryId != WorkCountryId
  BankName         varchar(100)?      HolderName varchar(200)?      ProviderAccountRef varchar(100)?   ← an id, NEVER a PAN
  Status           PayoutDetailsStatus NOT NULL   { Provided | NeedsReconfirmation }
  ConfirmedAt      timestamptz?
  LegacyRawValue   varchar(50)?       ← migration-only park; write-once; dropped by a follow-up ticket
```

**`CountryConfiguration` grows exactly one column** — `PayoutScheme?`, modelled on
`FiscalEnforcementMode` (`:65-71`), **not** on the `*Label`/`*Format`/`*Required` triples.

### Scheme resolution (no country branch anywhere)

```
CountryConfiguration(BankCountryId).PayoutScheme
  ↳ if unconfigured AND the value is a mod-97-valid IBAN whose prefix == BankCountryId → SepaIban
  ↳ else REJECT  →  validation.payout.country_not_supported
```

An IBAN is **self-describing by construction**, so it needs no configuration to be checkable. Only the
*local* schemes need our knowledge — and those are exactly the markets we open deliberately.

### Validation: same seam as `ITaxIdValidator`, opposite failure mode

`IPayoutDetailsValidator` mirrors `ITaxIdValidator`'s shape (async, config-driven, returns an i18n key
consumed by a `MustAsync`). It **fails closed**, where `TaxIdValidator.MatchesFormat` fails **open**
(`:54-73` returns `true` on a null format, a regex timeout, and a malformed regex). Fail-open is right
for a label check; it is wrong when the failure is a payment.

Checks: **ISO 13616 + ISO 7064 mod-97-10** on every IBAN, the IBAN prefix must equal `BankCountryId`,
**ISO 9362** on every BIC, and the **CZ/SK weighted modulo-11** on the local prefix and account number.
*(The CZ/SK weight vector and the CZ/SK IBAN composition are reads of secondary/registry sources —
**T-0519 verifies both against a primary source before either becomes blocking.** No agent here asserts
a banking legal requirement.)*

### The CZ form is two fields, and the IBAN is computed

For CZ/SK the IBAN is a deterministic function of bank code + prefix + account number, so the **local
parts are the source of truth** and the server derives the IBAN. A cleaner-supplied IBAN that disagrees
with the derived one is rejected — two renderings of one account that disagree on one document is a
failed payment.

### Encryption at rest: **plaintext, decided, with reversal triggers**

The repo does nothing at the column level today (`PasswordConverter.cs:6` is a one-way hash — unusable
for a value that must be printed; no `IDataProtection` on any server path). Column encryption here would
be **theatre**: the value is printed on the cleaner's invoice, emailed, and GDPR-exported
(`GdprExportService.cs:38`). It also removes indexing/uniqueness (a duplicate-account fraud check is
plausible) and buys an unpaid key-management bill.

**The exposure that is real is the read path, and that is fixed:** `Iban` leaves `EmployeeListItem`
(`:52`) and `EmployeeItem` (`:27`) — an account identifier must not ride a paged response. One
single-resource, owner-or-admin read (S3); masked by default; unmasked only for the owner's own edit form
and the server-side invoice renderer; an admin reveal is an audited action (ADR-0012 D4.1); never logged
(S6); cleared by `Employee.Anonymize()`.

**Reversal triggers:** non-printable data joins the record · a second tenant with cross-visible operators
· an external processor gets DB access · a contractual/regulatory obligation naming encryption at rest.

### Migration: the completeness gate is decoupled from validity

> **`IsProfileComplete()` means "payout details EXIST", not "payout details are VALID".**
> Real validation binds **writes** and **payout issuance** — never retroactively a profile.

`Employee.cs:283` becomes `PayoutDetails is not null`, satisfied by every migrated row. **No cleaner is
locked off the job board on migration day.** The hard stop moves to `EmployeeInvoice` generation, which
refuses for `Status != Provided` and records an admin-visible blocker — because an invoice is a
sequence-numbered legal document and deferring issuance is reversible where burning a number on a
defective one is not.

**Backfill classifier** (owner-run script with a dry-run; **not** startup code):

| Class | Test | Lands as |
|---|---|---|
| 0 | `"[DELETED]"` (`AnonymizationMarker.cs:5`) | **no record** — the person is gone |
| 1 | passes mod-97 | valid; a **CZ/SK IBAN decomposes back into bank code + prefix + account number**, giving a complete payment block with **zero cleaner action** |
| 2 | a mangled domestic number | → class 3. **Never reconstructed** — `1920001453990800` is equally consistent with `19-2000145399/0800` and `192000145399/0800`, and a wrong guess pays a stranger |
| 3 | anything else | parked verbatim in `LegacyRawValue`, `Status = NeedsReconfirmation`, profile stays complete, non-blocking prompt, payout blocked until re-entered |

**The backfill principle:** *legacy validity is decided by running the new validator over the old value;
anything a fresh write would pass, we accept without troubling the cleaner.*

`"profile.fields.iban"` (`Employee.cs:313`) **keeps its name in v1** — the server emits the key and five
shipped clients translate it, two app-store-gated. Renaming shows a raw key to every un-updated device
for zero user benefit; rename on a coordinated mobile release.

### Card numbers: not a column, and not what a card payout is

**No PAN, encrypted or otherwise.** Two reasons: PCI DSS scope *(cited as a published industry standard;
not legal advice; the business consequence is the owner's call)*, and — the one that matters more —
**you do not push a payout to a PAN.** A card payout is a network payout to a **tokenised destination
held by a PSP**, and what you store is an **id**.

If it is ever pursued: PSP payout onboarding (Stripe Connect Express or equivalent), **KYC/KYB per
cleaner** done by the PSP, an onboarding-link flow, a webhook-driven account-status lifecycle, an
idempotent payout-execution path (S7), per-country availability/fees, and reconciliation against
`EmployeeInvoice`. **A separate epic.** The shape absorbs the *data* side at zero cost:
`Scheme = ProviderPayoutToken` + `ProviderAccountRef`, **no migration**.

---

## What the next country costs

| Adding… | Migrations | Backend | Config | Clients |
|---|---|---|---|---|
| **SK** | 0 | 0 | 1 seed value | 0 |
| a SEPA/IBAN market | 0 | 0 | 0–1 | 0 |
| a **non-IBAN** market (US ACH: routing + account + type) | **1 additive** | 1 enum value + 1 validator | 1 | **1 per client** — not zero, and we do not pretend otherwise |
| a PSP token payout | **0** | payout epic, not schema | 1 | 1 (a link, not a form) |
| a **second** destination per cleaner | 1 (drop unique index, add `IsPrimary`) | primary-selection rule | 0 | 1 |

**SK is free because SK is the *same scheme*, not a second one** — it inherited the Czechoslovak
account-numbering structure. That is the "generalize along scheme" bet paying off. *(Verified before SK
ships — T-0519.)*

---

## Open threads this doc tracks

- **ADR-0034 is `proposed`.** The panel (security challenger → D6/D8/D9; db challenger → D1/D5/D7) has
  not run. **T-0518 does not start before the verdict.**
- **Two primary-source verifications gate T-0519**: the CZ/SK modulo-11 weight vector, and the CZ/SK
  IBAN composition (which the derived-IBAN and the class-1 decomposition both depend on).
- **T-0518 must confirm** Azure PostgreSQL Flexible Server encryption at rest on the real DEV/PROD
  server (currently a read of product documentation, not of this repo's Bicep) and record the finding at
  the column.
- **The DEV data has not been censused.** The four-class design comes from the *code paths* that write
  the column. **T-0518's dry-run turns it into a fact** — and if class 3 is the majority, T-0518 stops
  and re-opens the migration decision rather than proceeding.
- **`LegacyRawValue` needs its drop ticket filed** once the reconfirmation campaign closes. An escape
  hatch with a scheduled removal is a plan; one without is rot.
- **The identification-path country inconsistency** (`BusinessCountryId`: mobile-only, discarded, and
  pre-filled from the *address* country) is a real finding surfaced by this work. ADR-0034 only refuses
  to inherit it. **It needs its own ticket.**
- **T-0508 owns** konstantní symbol, due date and the QR *Platba+F* code; **`EmployeeInvoice
  .VariableSymbol` already exists** (`:72`). None of them belongs on the payout record.
- **T-0509 owns** the IBAN's exposure in logs and the GDPR export; the new fields inherit its outcome.
- **Sizing:** T-0518, T-0519 and T-0521 are each `L` under this shape and are split in ADR-0034's
  sizing table.

---

## Related

- Roles: [`knowledge/roles/employee-payout-details.md`](../../knowledge/roles/employee-payout-details.md) ·
  [`knowledge/roles/payout-details-validator.md`](../../knowledge/roles/payout-details-validator.md)
- Canonical system description: [`docs/architecture/database.md`](../../../docs/architecture/database.md) ·
  [`docs/architecture/backend.md`](../../../docs/architecture/backend.md)
- Security laws: [`knowledge/security-rules.md`](../../knowledge/security-rules.md) — S3 (ownership),
  S4 (DTO leak), S6 (logging), S8 (tenancy), S9 (migration/DTO-contract safety)
