# Partner Payout Details — living decision doc

**Topic:** how the platform holds *where a cleaner's money goes* — the shape, the governing country, the
validation, the at-rest posture, and the read contract.
**ADRs:** [ADR-0034](../../backlog/adr/0034-partner-payout-details-shape.md)
(**`accepted` 2026-08-02** — the shape; survived a two-challenger defense panel with **eight blocking
findings folded in** — see *What the panel changed* below) · composes with
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
| **(a) flat nullable columns on `Employee`**, mirroring `CompanyInfo`'s `BankName`/`BankAccountNumber`/`Iban`/`Swift` | SK free; the first non-IBAN market widens `Employee` itself | **Lost, narrowly — and the panel narrowed it further.** What still beats it: no discriminator over seven-plus sparse columns whose meaningful subset varies by scheme (every reader re-derives it = a country branch in disguise), and the lifecycle fields are not employee attributes. What **does not** beat it, and was struck: *"the read contract stays broken by construction because `Iban` rides `EmployeeListItem`"* — **false**, no payout identifier rides a paged DTO and the mappers are hand-written. And A1 has a real advantage the original comparison omitted: **a scalar column is always materialized**, so it is immune to the load-order hazard. **The accepted design adopts that immunity for the two operations where it matters** (see *Two invariants that never ride the navigation*). `CompanyInfo` is a **singleton, one company, one country** — not the precedent it resembles |
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

## Current shape (as **accepted** by ADR-0034, panel amendments folded in)

```
Employee                                                    // the AGGREGATE keeps the gate bit
  HasPayoutDetails bool NOT NULL DEFAULT false   ← the completeness gate reads THIS, never a navigation

EmployeePayoutDetails : Auditable, ITenantEntity          // house archetype: EmployeePayConfig
  EmployeeId       NOT NULL
      UNIQUE (TenantId, EmployeeId) .AreNullsDistinct(false)   ← cardinality 1, reversibly
      + an app-level create-or-update guard on the write path
  Scheme           PayoutScheme?      null ⇒ unusable for payout (legacy park only)
  BankCountryId    FK Country?        the country of the BANK
  AccountPrefix    char(6)?      AccountNumber char(10)?       BankCode varchar(4)?   ← CZ/SK, zero-padded CANONICAL text
  Iban             varchar(34)?       ← DERIVED for CZ/SK, not collected. THE COMPARISON KEY for every equality check
  Swift            varchar(11)?       ← required when BankCountryId != WorkCountryId
  BankName         varchar(100)?      HolderName varchar(200)?      ProviderAccountRef varchar(100)?   ← an id, NEVER a PAN
  Status           PayoutDetailsStatus NOT NULL   { Provided | NeedsReconfirmation }
  ConfirmedAt      timestamptz?
  LastRevealedAt   timestamptz?       RevealCount int NOT NULL DEFAULT 0   ← what makes the admin reveal auditable
```

**Mutated in place** — explicitly exempt from ADR-0007 D1's `Deactivate` default. The record *is* the
current destination; history belongs in the admin audit log, not in tombstone rows. So the unique index
carries no `IsActive` filter and neither gate needs an `IsActive` predicate. Erasure **deletes**.

**The parked legacy value is NOT on this entity.** If the legacy branch runs at all it lives in a
non-mapped `payout_legacy_import` staging table (see *Migration*).

**Why `(TenantId, X)` and not `UNIQUE (EmployeeId)`.** Postgres treats NULLs as distinct, and every row
today has `TenantId = null` — so a plain `(TenantId, EmployeeId)` unique index would enforce nothing in
the mode that actually runs. The repo's answer is **not** to drop `TenantId`: nine entity configurations
put it in a unique index (`User:106`, `UserMembership:112`, `TenantConfiguration:27`,
`LoyaltyTransaction:91`, `LoyaltyTierConfig:33`, `PromoCode:63`, `PromoCodeRedemption:66`,
`ReferralCode:38`, `FiscalCounter:26`), and `UserMembershipEntityConfiguration.cs:100-109` documents the
general posture: **the index hardens the constraint, the application asserts it.**

**`.AreNullsDistinct(false)` is precedented here, not novel — it ships twice**:
`FiscalCounterEntityConfiguration.cs:28` (`(TenantId, Year, IssuerScope)`, so a single-tenant deployment
collapses onto one counter row and gaplessness holds) and `LiveActivityTokenConfiguration.cs:28`
(`(UserId, DeviceId, OrderId)`), both emitted into the committed `Initial` migration at `:2653` and
`:2685`. Payout details take the stricter form because, unlike `UserMembership`'s
re-subscribe-after-cancel, there is **no legitimate second row** — and its index is **unfiltered**, so
there is no partial-index interaction to reason about.

> ⚠️ **Two in-repo comments assert the opposite and are now false.**
> `UserMembershipEntityConfiguration.cs:106-109` and `LoyaltyTransactionEntityConfiguration.cs:82-88`
> decline `NULLS NOT DISTINCT` *"rather than introduce a one-off."* It is not a one-off — see above. A
> follow-up ticket corrects the **comments only**; both indexes are filtered partial indexes and
> re-deciding them is their owners' call, not a side-effect of a payout ADR. **General rule worth
> keeping: a comment asserting a false invariant is worse than no comment, because it stops the next
> reviewer from checking** — this one very nearly propagated into a tenth index.

**`CountryConfiguration` grows exactly one column** — `PayoutScheme?`, modelled on
`FiscalEnforcementMode` (`:65-71`), **not** on the `*Label`/`*Format`/`*Required` triples.

### The constraint that shapes everything else: **no lazy loading**

`rg "UseLazyLoadingProxies|ILazyLoader"` across `src/` returns **zero hits**. So **every invariant
relocated from a column onto a navigation property becomes load-order-dependent** — and this repo loads
`Employee` through hand-written `.Include` lists (`EmployeeRepository.cs:9-17`). Write this down once and
apply it forever: **you may move a value to a child record; you may not move its invariant.**

### Two invariants that never ride the navigation

1. **The completeness gate reads `Employee.HasPayoutDetails`, a column.** `Employee.cs:283`'s
   `!string.IsNullOrEmpty(IBAN)` becomes that flag — same semantics (*presence*, never validity), always
   materialized by every loader that already loads an `Employee`. Had the gate read
   `PayoutDetails is not null`, `RequireCompleteProfileAttribute.cs:25` → `GetByUserEmailAsync` (no such
   include) → `:32-49` would have returned **403 to every cleaner** on the partner host's
   `OrderController`, `EmployeePayrollController`, `DashboardController` and `DisputeController` — the
   exact migration-day outage the design exists to prevent.
   *Invariant:* `HasPayoutDetails == (a payout row exists)`. Two writers only (the payout write path, the
   backfill); cleared by `Anonymize()` and by erasure; pinned by an integration test.
   *Corollary:* **`.Include(e => e.PayoutDetails)` is forbidden on any paged or list query** — the gate
   does not need it, and adding it would materialize the full unmasked record on the paged path.
2. **Erasure is an id-keyed, set-based write in `GdprDeletionService`**, not a navigation walk in
   `Employee.Anonymize()`. `GdprDeletionService.cs:43-46` includes only `Employee → Address`; a
   null-guarded `PayoutDetails?.Clear()` would have been a **silent no-op** — success returned, bank
   account, SWIFT, holder legal name and parked value retained in plaintext. `Anonymize()` only sets
   `HasPayoutDetails = false` (a column on the row it already has). Verified by an integration test
   through `GdprDeletionService`'s **real** query shape; an in-memory test with a hand-populated
   navigation passes green while production fails and does not discharge it.

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
**ISO 9362** on every BIC, the **CZ/SK weighted modulo-11** on the local prefix and account number, and
a **Luhn rejection** of anything card-shaped (see *Card numbers*).

> **The CZ/SK modulo-11 rule, stated correctly — this was the panel's most expensive catch.**
> **Zero-pad each of the prefix and the account number to 10 digits. Apply weights
> 6, 3, 7, 9, 10, 5, 8, 4, 2, 1 LEFT-TO-RIGHT. The weighted sum mod 11 must be 0.**
>
> The draft said *right-to-left*. The vector is right; the direction is wrong. Computed independently
> twice: the owner's own account `5885638003` sums to **297 → mod 11 = 0 (valid)** left-to-right, and to
> **243 → 1 (rejected)** right-to-left; over ~18k valid accounts the reversed rule rejects **≈91%**.
> Because the validator **fails closed**, that single inverted word would have rejected nine out of ten
> real Czech bank accounts at a write path that gates income — and the honesty note told T-0519 to verify
> *the weights*, not the direction, so the flag would not have caught it. Zero-padding to 10 also fixes a
> second defect in the same sentence: a 6-digit prefix needs the **last** six weights, not the first.

*(The weight vector is still a read of a secondary source, and the CZ/SK IBAN composition is now a
verified computation. **T-0519 verifies the vector, the direction AND the padding rule against a primary
source before the check becomes blocking**, with the owner's specimen `5885638003` as a required
known-good test vector. No agent here asserts a banking legal requirement.)*

### The CZ form is two fields, and the IBAN is computed

For CZ/SK the IBAN is a deterministic function of bank code + prefix + account number, so the **local
parts are the source of truth** and the server derives the IBAN. A cleaner-supplied IBAN that disagrees
with the derived one is rejected — two renderings of one account that disagree on one document is a
failed payment. **The composition verifies clean** (`CC` + 2 check + bank 4 + prefix 6 padded + account
10 padded = 24, ISO 7064 mod-97-10), computed independently by the db challenger and re-computed by the
lead, so derivation stays the **primary** design and the fallback stays a fallback.

**Leading zeros are NOT identity.** `compose('5500','','123456')` and `compose('5500','','0000123456')`
produce the *identical* IBAN, so they are not distinguishable accounts. Store the **zero-padded canonical
form** (`char(6)` / `char(10)`) — text, because they are digit strings, not quantities, but the reason is
canonicalization, not significance. **Every equality, duplicate and fraud check compares the derived
`Iban`, never the typed parts** (on raw parts the duplicate-account signal silently under-reports, which
is the worst kind of fraud control). Rendering trims back to the local form `5885638003/5500`.

> **Known defect in existing fixtures:** the owner's specimen composes to `CZ3155000000005885638003`,
> but `PayoutInvoiceLayoutTests.cs:187,60` and `PayoutInvoicePdfDataTests.cs:78,222` pin
> `CZ6555000000005885638003`, which **fails ISO 7064 mod-97** (remainder 12, not 1) and which the new
> validator would reject. T-0522 corrects them. A suite that pins an invalid IBAN for the owner's own
> account will otherwise be "fixed" by weakening the validator.

### Encryption at rest: **plaintext, decided, with reversal triggers**

The repo does nothing at the column level today (`PasswordConverter.cs:6` is a one-way hash — unusable
for a value that must be printed; no `IDataProtection` on any server path).

**Leg 1 — column encryption would be theatre, and the proof is live and test-pinned.** The cleaner's
account is **already printed on every payout invoice**: `FileExtensions.cs:107` sets
`Iban = employee.IBAN` on the supplier block, `InvoicePdfData.cs:36-59` documents that the supplier of a
payout invoice **is the cleaner** (*"sourcing them from `CompanyInfoData` would print an account that
tells the cleaner to pay us"*), `DefaultInvoiceLayoutBuilder.cs:175` renders it, and
`PayoutInvoiceLayoutTests.cs:56` asserts it with the owner's specimen in the fixture. The PDF is
blob-stored, downloadable and emailed. **"Fix the printing instead" is not available: the payee account
IS the payment instruction on a supplier invoice.**

**Leg 2 — the honest threat model.** The threat column encryption *uniquely* covers is **a principal
holding a live DB session who is not the application** — and `postgres.bicep:153-161` provisions exactly
one (`allowAdminIp`, active whenever `publicNetworkAccess == 'Enabled'`). **Today that principal is the
owner, who is also the person executing the transfers**, so the control would gate them from data they
need anyway. It also removes indexing/uniqueness and buys an unpaid key-management bill.

**At-rest posture, established:** `postgres.bicep:81` declares the Flexible Server with **no
`dataEncryption` block** → the platform default, i.e. **service-managed keys, not CMK**, not disable-able
on this resource type. T-0518 confirms provisioning provenance and records this at the column.

**Reversal triggers:** non-printable data joins the record · a second tenant with cross-visible operators
(*latent multi-tenant risk, carried by the read contract*) · a **non-owner** principal (external
processor, BI tool, contractor, support vendor) gets direct DB access, or `publicNetworkAccess` stays
`Enabled` once a second operator exists · a contractual/regulatory obligation naming encryption at rest.

### The read contract — three routes, and the reveal is a **command**

**Correction that matters, because the original design was justified by it:** *no payout identifier has
ever ridden a paged list.* `EmployeeListItem.cs:52` is inside the file's **fourth** record,
`AdminEmployeeDetail`; the genuine paged DTO `AdminEmployeeListItem` has no `Iban` and `MapToAdminDto`
does not map one. The mappers are hand-written positional constructions that already omit it. **The real
exposure is narrower and worse-shaped:** the **unmasked** identifier on an **enumerable resource-by-id**
admin route (`GET admin/employee/details/{employeeId}`) gated by **the same policy that grants the id
list** (`Policy.CanViewPagedEmployee` on both `:17` and `:55`), with **no masking, no reveal record and
no rate limit**.

| Route | Caller | Body |
|---|---|---|
| `GET .../me/payout-details` | the **owner** (session-resolved) | full value — their own account, S4 self-data |
| `GET .../employees/{id}/payout-details` | **admin** | **masked only**; the DTO has **no unmasked field at all**, so a client cannot render what it was never sent |
| `POST .../employees/{id}/payout-details/reveal` | **admin** | full value — audited, rate-limited **command** |

Three routes, not one route with role-dependent content: otherwise "masked by default" becomes a property
of five clients' rendering code, two of them app-store-gated.

- **Authorization** follows `DownloadInvoice.cs:39-58` (role arm, then owner-id equality, **`NotFound`**
  on mismatch per S3) — **not** `UpdateBankDetails.AllowedToUpdateEmployee` (`:39-44`), which is
  owner-only with no admin arm and would ship a read no admin can call.
- **The reveal must be a `Command`.** `AdminMutationGate.cs:17-24` audits **iff** the request type name
  ends `Command`; `AuditLogBehavior.cs:19` says *"Queries and non-admin mutations produce no row."* A
  reveal query would be **silently unaudited** — and the audit trail is the compensating control the
  no-encryption decision leans on. So `RevealPayoutDetailsCommand` stamps `LastRevealedAt`/`RevealCount`,
  returns the unmasked value, and is audited atomically by the existing engine with **zero new audit
  code**. ADR-0012 is *not* amended. No CQRS breach — a reveal genuinely mutates and returns one record.
- **Rate limit.** An audited-but-unbounded reveal *records* bulk exfiltration instead of stopping it
  (id list → N reveals → the whole payout book at wire speed). The route carries the per-JWT-`sub`
  window (ADR-0003), and its controller joins
  `RateLimitCoverageGuardTests.MoneyAndSideEffectControllers` — **because the reveal is a POST, the
  guard's existing `MutatingMethods` contract then covers it for free.** One decision (reveal-as-command)
  closes the audit gap, the rate-limit gap and the coverage-guard gap together.
- **"Never on the wrong DTO" is a frozen-surface test, not a shape claim.** A test asserts no type under
  `…Features.*.DTOs` outside the named payout family declares a payout-identifier property, in the idiom
  the repo already uses (`FrozenPermissionMapTests`, `RateLimitCoverageGuardTests`,
  `AuthWireContractTests`, `HandleFailureErrorsContractTests`).
- Never logged (S6 — verified clean today: zero `iban|bank` hits across every log call). Audit hygiene
  uses a **distinct sentinel per field** (one `DoesNotContain` across ten fields passes if nine are
  checked).

### Migration: the completeness gate is decoupled from validity

> **`IsProfileComplete()` means "payout details EXIST", not "payout details are VALID".**
> Real validation binds **writes** and **payout issuance** — never retroactively a profile.

`Employee.cs:283` becomes **`HasPayoutDetails`** — a column, not `PayoutDetails is not null` — satisfied
by every migrated row. **No cleaner is locked off the job board on migration day, and that is now true by
construction rather than by a remembered `.Include`.** The hard stop moves to `EmployeeInvoice`
generation, which refuses for `Status != Provided` and records an admin-visible blocker — because an
invoice is a sequence-numbered legal document and deferring issuance is reversible (verified:
`GenerateInvoice.cs:15-16` stays valid because the pays keep `EmployeeInvoiceId == null`,
`OrderEmployeePayRepository.cs:46-49`) where burning a number on a defective one is not.

**Withholding an invoice has three obligations the original design missed:**

1. **The window is ~31 days, not ~14.** Pay periods are **monthly** —
   `PayPeriodBackgroundService.cs:89-90` and `:161-162` use `AddMonths(1).AddDays(-1)` and the code says
   so at `:85-88`; `PayPeriodService.GenerateBiWeeklyPeriodsForYear` (`:30`) has **zero call sites**.
   CLAUDE.md's "bi-weekly" is a stale row. So "the window is short" is not the mitigation — (2) is.
2. **Somebody must tell them.** Invoices are generated inside `SendPeriodClosedEmailsAsync`
   (`PayPeriodBackgroundService.cs:196-293`), and today when generation yields nothing the period-closed
   email is **still sent with a null attachment** (`:229-238`, `:259-269`). The email must learn the
   payout-blocked reason; "a prompt appears in the app" is not sufficient for a tax document.
3. **A withheld invoice must not become a permanent reconciliation candidate.**
   `PayPeriodRepository.cs:85-101`'s predicate is *stale pays with no `EmployeeInvoice` row for the
   pair* — which a deliberately blocked pair matches **forever**, so `FiscalReconciliationService` would
   re-enqueue a refused message every tick, per blocked cleaner, burying a **real** fiscal loss in noise
   (ADR-0002 D3.4). Blocked pairs leave candidacy after the first refusal.

**The legacy apparatus is CONDITIONAL — the population may not exist.** One migration
(`20260723182623_Initial.cs`, regenerated rather than stacked), prod authored-not-deployed,
`insert_seed_data.sql` seeds **no** `Employee."IBAN"` (its only `Iban` is `CompanyInfo`'s). *"How many
per class"* is the second question; *"does this data reach launch"* is the first, it is an **owner**
question (`Q-PAYOUT-05`), and it decides whether a table and a column exist at all.

- **Branch A (default, and where the evidence points):** no legacy values → **"create the table."** No
  classifier, no staging table, no campaign.
- **Branch B:** the classifier below runs.

**Backfill classifier, Branch B** (owner-run script with a dry-run; **not** startup code):

| Class | Test | Lands as |
|---|---|---|
| **−1** | 13–19 digits after stripping separators **and passes Luhn** → a card PAN | **Not parked, not preserved**, `Status = NeedsReconfirmation`, the prompt never echoes it, **and T-0518 nulls the source column**. Counted separately in the dry-run |
| 0 | `"[DELETED]"` (`AnonymizationMarker.cs:5`) | **no record** — the person is gone |
| 1 | passes mod-97 | valid; a **CZ/SK IBAN decomposes back into bank code + prefix + account number** (deterministic and **canonicalizing**, not "lossless"), giving a complete payment block with **zero cleaner action** |
| 2 | a mangled domestic number | → class 3. **Never reconstructed** — `1920001453990800` is equally consistent with `19-2000145399/0800` and `192000145399/0800`, and a wrong guess pays a stranger |
| 3 | anything else | parked in the **`payout_legacy_import` staging table**, `Status = NeedsReconfirmation`, profile stays complete, non-blocking prompt, payout blocked until re-entered |

**The parked value lives outside the operational table.** A column application code must never write does
not belong on the entity application code writes: on `EmployeePayoutDetails` it would sit in every EF
`SELECT`, in the audit-coverage surface, in the GDPR-export surface, and it would be the one field with
no scheme, no validator and no masking rule — while holding mangled raw PII. And *"a follow-up ticket
drops it"* had **no trigger** (the campaign closes when the last class-3 cleaner reconfirms; a cleaner who
stops working never does). `payout_legacy_import` is keyed by `EmployeeId`, has **no `DbSet<T>`, no entity
configuration, no `OnModelCreating` registration**, is read by exactly one query, is **erased by the
erasure path** (a cleaner may be erased before the campaign closes), and exits by `DROP TABLE`.

**The backfill principle:** *legacy validity is decided by running the new validator over the old value;
anything a fresh write would pass, we accept without troubling the cleaner — except a PAN, which is
neither parked nor preserved.*

**Anonymized rows change gate state, deliberately.** Today `Anonymize()` leaves `IBAN = "[DELETED]"`
(non-empty), so `IsProfileComplete()` stays true. Under the new gate it becomes **false**. Harmless — the
same service deactivates the employee at `GdprDeletionService.cs:237` — but `GetMissingProfileFields()`
output changes for anonymized rows, and that is a consequence, not a regression.

`"profile.fields.iban"` (`Employee.cs:313`) **keeps its name in v1** — the server emits the key and five
shipped clients translate it, two app-store-gated. Renaming shows a raw key to every un-updated device
for zero user benefit; rename on a coordinated mobile release.

### Card numbers: not a column, not what a card payout is — and now **enforced**

**No PAN, encrypted or otherwise.** Two reasons: PCI DSS scope *(cited as a published industry standard;
not legal advice; the business consequence is the owner's call)*, and — the one that matters more —
**you do not push a payout to a PAN.** A card payout is a network payout to a **tokenised destination
held by a PSP**, and what you store is an **id**.

**The invariant is a runtime guard, not a sentence.** A PAN can already be *in the data*:
`ValidationExtensions.cs:122-130` is `NotEmpty().Length(15, 34)`, so a 16-digit Visa/Mastercard and a
15-digit Amex both **pass today**; `BankSectionViewModel.kt:74-76` normalizes `4111 1111 1111 1111` into
16 characters and sends it; and the owner's own phrasing ("Bank Account, **Card number**…") is direct
evidence a cleaner may reasonably believe it belongs there. So `IPayoutDetailsValidator` **rejects a
Luhn-valid 13–19-digit value on every write path** (`validation.payout.looks_like_card`), the backfill's
class −1 neither parks nor preserves one, and the reviewer check inspects **data**, not just field names
and shapes — a name/shape check cannot catch data.

**The scope half is the owner's, and it is escalated, not taken** (`Q-PAYOUT-04`): did "Card number"
mean cleaners are paid *to a card* (a PSP payout rail — a separate epic), or was it one example of "the
identifiers needed to pay someone"? It blocks nothing: the answer costs zero migrations either way, and
the storage decision holds regardless, because even "yes, cards" stores an **id**.

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

---

## What the panel changed (2026-08-02) — the durable lessons, not just the diffs

Two challengers ran in parallel (security → D6/D8/D9; db → D1/D5/D7). Seventeen findings, **eight
blocking**, all folded into the accepted ADR. Four of them generalize beyond payout details and belong in
this doc so the next design does not rediscover them:

1. **No lazy loading ⇒ never relocate an invariant onto a navigation.** Two lanes independently found the
   same defect twice — a workforce-wide 403 and a silent GDPR-erasure failure — because the draft moved a
   value and its invariants together. Move the value; keep the invariant on a column or in an id-keyed
   write.
2. **A test that constructs its own aggregate cannot verify a query-shape invariant.** Both defects would
   have shipped green: a hand-built `Employee` has the navigation set. Where an invariant depends on how
   the object graph was loaded, the test must load it the way production does.
3. **Check what a `file:line` is *inside*, not just that the line matches.** The single premise under two
   decisions — *"the IBAN ships in a paged list"* — was wrong because `EmployeeListItem.cs:52` sits inside
   the file's **fourth** record. The citations were accurate and the inference was not, and it produced a
   reviewer checklist item that **passes on day zero without anyone doing anything** while the field that
   actually leaks goes unnamed.
4. **Flag the whole rule, not the part you doubt.** The honesty note told the implementer to verify the
   mod-11 *weights*; the defect was the *direction*. A "verify this before it becomes blocking" note only
   protects the clause it names.

5. **Verify a "this is unprecedented" claim by reading, not by grepping.** Both wrong claims in this
   panel were undercounts from single-line greps run against multi-line declarations: *"not one
   `.IsUnique()` site includes `TenantId`"* (nine do) and *"`.AreNullsDistinct(false)` would be a
   one-off"* (it ships twice, plus twice in the migration). Both would have pushed the design **away**
   from the correct answer in the name of consistency. A claim that something has no precedent is
   exactly the claim that most needs its search shown.

**Also worth remembering:** a challenger can be wrong too — and so can a committed code comment. The
*finding* stood (a plain `(TenantId, X)` unique enforces nothing while `TenantId` is null); the proposed
remedy would have deviated from S8 and from nine existing indexes. **The house answer is `(TenantId, X)`
+ the right null-handling + an app-level guard.**

---

## Open threads this doc tracks

- **ADR-0034 is `accepted` and immutable.** Deviations — including *"we'll just add the `.Include`
  instead"* — require a superseding ADR.
- **Two owner questions are open.** `Q-PAYOUT-04` (what "Card number" meant — blocks nothing) and
  **`Q-PAYOUT-05` (do legacy `Employee.IBAN` values reach launch — gates T-0518's scope)**. T-0518 must
  not start until `Q-PAYOUT-05` is answered, or must start on **Branch A** with Branch B as a separate
  ticket.
- **Three primary-source verifications gate T-0519**: the CZ/SK modulo-11 weight vector **and its
  direction and padding rule**, the CZ/SK IBAN composition (now a verified computation, still to be
  confirmed against the ISO 13616 registry), and SK's structural identity with CZ before SK ships.
- **T-0518 must confirm** that DEV/PROD were provisioned from `deploy/bicep/modules/postgres.bicep` and
  record that the at-rest posture is **service-managed keys, not CMK** — the Bicep half is verified
  (`:81`, no `dataEncryption` block); only provenance remains.
- **T-0522 must correct the fixture IBANs** — `PayoutInvoiceLayoutTests.cs:187,60` and
  `PayoutInvoicePdfDataTests.cs:78,222` pin a value that fails mod-97.
- **The DEV data has still not been censused.** Under Branch B the dry-run turns the five-class design
  into a fact; if class 3 is the majority, T-0518 stops and re-opens the migration decision.
- **D2, D3 and D10 were examined by neither challenger.** D2 carries a named residual: `WorkCountryId`
  stands in for the paying entity's country in the cross-border SWIFT rule, and the correct counterparty
  is `CompanyInfo.CountryId`. They coincide today. **Revisit D2 before assuming that still holds** if a
  paying entity is ever established in a country it does not operate in.
- **The identification-path country inconsistency** (`BusinessCountryId`: mobile-only, discarded, and
  pre-filled from the *address* country) is a real finding surfaced by this work. ADR-0034 only refuses
  to inherit it. **It needs its own ticket.**
- **T-0508 owns** konstantní symbol, due date and the QR *Platba+F* code; **`EmployeeInvoice
  .VariableSymbol` already exists** (`:72`). None of them belongs on the payout record.
- **T-0509 owns** the IBAN's exposure in logs and the GDPR export — **and the plaintext account inside
  already-generated invoice PDFs in blob storage after an erasure**, which the panel surfaced and did not
  decide here.
- **Sizing:** T-0518, T-0519 and T-0521 are each `L`; T-0520 grew to `M/L` (the masked-vs-reveal two-step
  UI) and T-0522 gained D7's notification, the sweep exclusion, trimmed rendering and the fixture fix.

---

## Related

- Roles: [`knowledge/roles/employee-payout-details.md`](../../knowledge/roles/employee-payout-details.md) ·
  [`knowledge/roles/payout-details-validator.md`](../../knowledge/roles/payout-details-validator.md)
- Canonical system description: [`docs/architecture/database.md`](../../../docs/architecture/database.md) ·
  [`docs/architecture/backend.md`](../../../docs/architecture/backend.md)
- Security laws: [`knowledge/security-rules.md`](../../knowledge/security-rules.md) — S3 (ownership),
  S4 (DTO leak), S6 (logging), S8 (tenancy), S9 (migration/DTO-contract safety)
