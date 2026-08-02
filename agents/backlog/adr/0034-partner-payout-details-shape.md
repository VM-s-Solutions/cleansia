# ADR-0034 — Partner payout details are a **scheme-discriminated child record of cardinality one**; the **bank's own country** governs the format (not residence, not the discarded `BusinessCountryId`); `CountryConfiguration` grows **one** column (`PayoutScheme`) and no bank labels; validation is **real** (ISO 7064 mod-97 + the CZ/SK weighted mod-11) and **fails closed**; and the profile-completeness gate is **decoupled from payout validity** so no existing cleaner is locked off the job board by the migration

- **Status:** `proposed` — **needs the panel.** Written in `author` mode. Six of the eight decisions
  below are real trade-offs with a live loser (D1 shape, D2 governing country, D3 config, D4 fail-closed,
  D6 plaintext, D7 the legacy-row landing). Two challengers are named in `## Challenge` with the exact
  seams to attack (`security` → D6/D8; `db` → D1/D7).
- **Date:** 2026-08-02
- **Supersedes:** — (composes with **ADR-0017** — per-country variation is config-driven, never a
  country-code branch in a handler; **ADR-0012 D4.1** — admin-action audit records ids, not the PII it
  edited; **ADR-0007** — soft-delete/anonymization semantics)
- **Superseded by:** —
- **Applies to:** `Cleansia.Core.Domain` (one new entity + two enums) · `Cleansia.Infra.Database`
  (one entity configuration + **one owner-run migration** + **one owner-run backfill script**) ·
  `Cleansia.Core.AppServices` (one new domain service + three write paths + the read contract) ·
  **all five API hosts equally — no host coupling** · **breaking NSwag change** (`Iban` leaves two list
  DTOs) · **no change to the tenancy filter, the pay formula, or the fiscal modes**
- **Ticket:** T-0517 (`security_touching: true`). Implementation: T-0518 (db) · T-0519 (backend) ·
  T-0520 (web/admin) · T-0521 (mobile) · consumed by T-0522 (payout invoice) and cross-checked against
  T-0508 (invoice field spec) and T-0511 AC5 (the same generality question).
- **Owner input this ADR executes (verbatim, 2026-08-02):** *"Let's start with CZ but in the way that
  it's easy to expand in the future — like Bank Account, Card number and what else needed to make a
  payment to the employee."* Plus a **real Czech invoice the owner issued**, whose payment block carries
  a local account number in `5885638003/5500` form, an IBAN, a SWIFT, a variabilní symbol and a
  konstantní symbol.

> **One decision:** *what shape holds a cleaner's payout destination such that adding the second country
> costs data, not schema.* Everything else here is a corollary. The answer is **not** "more columns" and
> **not** "a general bag" — it is: **a bank account is one destination identified several equivalent
> ways, so generalize along the axis that varies (the *scheme*) and pin the axis that does not (the
> *count*).** The CZ specimen proves the framing: `5885638003/5500`, `CZ…`, and `RZBCCZPP` are not three
> payout methods, they are three renderings of one account — and for CZ/SK two of the three are
> **derivable from the first**, which is why the parts, not the string, are what we store.

---

## Context — every citation verified in the working tree, 2026-08-02

### What exists today

| Thing | State (file:line) |
|---|---|
| `Employee.IBAN` | **one nullable string**, `Employee.cs:24`; `HasMaxLength(50)` at `EmployeeEntityConfiguration.cs:30-31`. **No value converter, no encryption** |
| Its server validation | `ValidationExtensions.cs:122-130` — `Cascade(Stop).NotEmpty().Length(15, 34)`. **No country prefix. No checksum.** `"totally not an iban!!"` (21 chars) passes |
| Its three write paths | `UpdateBankDetails.cs:35-36` (self-service), `UpdateEmployee.cs:127-128`, `AdminUpdateEmployee.cs:73` — all three call the same `ValidateIban()` |
| Its readers | `Employee.cs:283` (**the profile-completeness gate**), `Employee.cs:313` (`"profile.fields.iban"`), `GdprExportService.cs:38`, `EmployeeMappers.cs:61,115` |
| **Its DTO exposure** | `EmployeeItem.cs:27` **and `EmployeeListItem.cs:52`** — the account identifier ships in a **paged list** response |
| Anonymization | `Employee.Anonymize()` sets `IBAN = AnonymizationMarker.Value` = **`"[DELETED]"`** (`AnonymizationMarker.cs:5`) — 9 chars, i.e. **a stored value that the field's own validator would reject** |
| Web client validation | `custom-validators.ts:74-86` — a **structural** regex `^[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}$`. No mod-97 |
| Android client validation | `BankSectionViewModel.kt:75-85` — **blank check only**, and `onIbanChange` **strips every non-alphanumeric character** (`filter { ch -> ch.isLetterOrDigit() }`) |
| iOS client validation | `BankSectionViewModel.swift:42-48, 66-75` — **blank check only**, same `filter { $0.isLetter \|\| $0.isNumber }` normalizer |
| The richer shape that already exists | `CompanyInfo` — `BankName` / `BankAccountNumber` / `Iban` / `Swift` (`CompanyInfo.cs:60-70`), updated by `UpdateCompanyInfo.cs:154`, rendered at `DefaultReceiptLayoutBuilder.cs:167-168` |
| Per-country **label/format/required** machinery | `CountryConfiguration.cs:37-57` — `TaxIdLabel/Format`, `RegistrationNumberLabel/Format/Required`, `VatNumberLabel/Format/Required`. **No bank equivalent** |
| Per-country **validation** machinery | `ITaxIdValidator` → `TaxIdValidator.cs`, consumed at `UpdateIdentificationInfo.cs:73-96`. `MatchesFormat` (`:54-73`) returns **`true`** on a null format, on `RegexMatchTimeoutException`, and on `ArgumentException` — i.e. **fail-open** |
| Per-country **enum** precedent on the same entity | `CountryConfiguration.FiscalEnforcementMode` (`:65-71`) — an enum column with a documented default, exactly the shape D3 needs |
| Employee's three country fields | residence `Address.CountryId`; work jurisdiction `WorkCountryId` (`Employee.cs:68-81`, admin-set at approval, `AssignWorkCountry` `:213-221`); nationality `NationalityId`. **`BusinessCountryId` is not among them** |
| `BusinessCountryId` | exists **only** as a command parameter — `UpdateIdentificationInfo.cs:121`, used at `:77` and `:92`, and **never assigned to the aggregate** in the handler (`:131-150`). It is read, used, and thrown away |
| Who sends it | **mobile only.** `IdentificationSectionViewModel.kt:96-98` pre-fills it from `e.countryId` (the *address* country) and lets the cleaner override it. `businessCountryId` has **zero occurrences anywhere under `src/Cleansia.App`** |
| Owned types / value objects in EF | **none** — `grep OwnsOne\|OwnsMany\|ComplexProperty src/Cleansia.Infra.Database` returns nothing. The house archetype for a related record is a **class : `Auditable, ITenantEntity`** (`PayPeriod`, `OrderEmployeePay`, `EmployeePayConfig`, `EmployeeInvoice`) |
| Column-level encryption anywhere in the repo | **none.** The only sensitive-value converter is `PasswordConverter.cs:6` — a **one-way hash**, structurally unusable for a value that must be printed. The single `Encrypt*` hit in `src/` is a doc comment about `EncryptedSharedPreferences` on the *client* (`Web.Mobile.Customer/Controllers/AuthController.cs:21`) |
| Invoice-side facts already settled by T-0508 | `EmployeeInvoice.VariableSymbol` **exists** (`:72`, generated `:331`, rendered `DefaultInvoiceLayoutBuilder.cs:38-39`). The bank block on today's PDF is **Cleansia's**, not the cleaner's |

### The three findings that shape the decision

**F1 — the inconsistency is already live, and it is worse than "phones don't validate".** The mobile
normalizers strip `-` and `/`. A cleaner typing the perfectly correct Czech account `19-2000145399/0800`
sends `1920001453990800` — 16 characters, which **passes** `Length(15, 34)` and is stored as an "IBAN".
The same cleaner on web is *rejected* by the structural regex. So today the platform contains a
population of silently mangled domestic account numbers whose separators are **irrecoverable**
(`1920001453990800` is equally consistent with `19-2000145399/0800` and `192000145399/0800`). Any
migration that guesses where the separators go is a transfer to a stranger. This is not hypothetical
corruption to plan for — it is corruption to *classify*.

**F2 — `Employee.IBAN` is not an unused field, it is a gate on a cleaner's income.**
`Employee.cs:283` makes a non-empty `IBAN` part of `hasEmployeeInfo`, which makes it part of
`IsProfileComplete()`, which is what decides whether a cleaner may take orders. Any design in which
"the stored value is now invalid" implies "the profile is now incomplete" **takes working cleaners off
the job board on the day the migration runs**. That constraint, not the schema, is the hard part of
this ADR.

**F3 — the per-country precedent that "consistency" would have us copy is unused.**
`RegistrationNumberLabel` / `VatNumberLabel` / `TaxIdLabel` exist on `CountryConfiguration` and **no
client reads them** — the labels are hardcoded in every UI. Copying an unproven pattern is not
consistency, it is compounding. This cuts *against* growing `CountryConfiguration` a family of bank
label/format/required columns, and it is why D3 grows exactly **one** column instead of six.

---

## Decision

### D1 — Shape: a dedicated `EmployeePayoutDetails` child entity, discriminated by **scheme**, with **cardinality one** enforced by a unique index

**The generality principle this ADR establishes (and T-0511 AC5 must match):**

> **Generalize along the axis that will actually vary. Pin the axis that will not.**
> A keyed/discriminated table earns its place when the *set of kinds* grows without warning. A column
> per kind is right when the kinds are closed. **Cardinality is a separate question from kind** — and
> pinning it at one with a unique index costs nothing and is trivially lifted later.

For payout details the varying axis is the **scheme** (CZ/SK domestic ≠ SEPA ≠ a future non-IBAN
market ≠ a PSP token). The axis that is *not* varying is **how many destinations a cleaner has** — one
person, one place the money goes. So:

```
EmployeePayoutDetails : Auditable, ITenantEntity     // the house archetype — see EmployeePayConfig
    EmployeeId          string    NOT NULL   UNIQUE INDEX (TenantId, EmployeeId)   // cardinality = 1
    Scheme              PayoutScheme?         NULL   // null ⇒ unusable for payout (legacy park only)
    BankCountryId       string? FK Country    NULL   // the country of the BANK — see D2
    ...identifier fields per D5...
    Status              PayoutDetailsStatus   NOT NULL
```

- **The unique index is the cardinality decision.** Lifting it to "several destinations, one primary"
  is *drop the unique index + add `IsPrimary`* — one additive migration, no re-shaping, no data
  rewrite. That is the cheapest possible option on a change we have **no evidence we need**, which is
  precisely why we do not build it now.
- **It is a child entity, not columns on `Employee`, for three reasons that are not "it's tidier":**
  1. **The read contract needs a different exposure than the aggregate has** (D8). Today `Iban` rides
     `EmployeeListItem` (`:52`) into a paged response because it is a property of `Employee` and the
     mapper flattens everything. A separate record is what makes "payout details are never on a list
     DTO" a *structural* rule rather than a rule someone must remember.
  2. **Its lifecycle is not the employee's.** It is written, re-confirmed, blocked, parked and (later)
     verified. `Status` + `ConfirmedAt` on `Employee` would be six more fields on an aggregate that
     `EmployeeUserAuditCoverageTests` already has to police field-by-field.
  3. **A PSP payout account (D9) is not an employee attribute at all** — it is an external object with
     its own id and its own webhook-driven lifecycle. It fits the record; it does not fit the person.
- **Not EF owned types.** The repo has **zero** `OwnsOne`/`OwnsMany` (verified). Introducing a
  persistence pattern the team has never used, for the one table that must never be got wrong, is a bad
  trade. Follow `EmployeePayConfig`.

### D2 — The **bank's own country** governs the format; `WorkCountryId` governs *requirement*; `BusinessCountryId` governs nothing

A bank account's format is decided by the bank, not by where the account holder lives, works, or is
registered. The ticket offered "cleaner's / tenant's / order's"; **all three are the wrong kind of
answer**, because none of them is a property of the *account*. So the record carries its own:

1. **`PayoutDetails.BankCountryId`** — an explicit field the cleaner sets. It **defaults to
   `Employee.WorkCountryId`**, falling back to `Address.CountryId` while work country is null (it is
   null until admin approval, `Employee.cs:76-79`, and payout details are captured *before* approval).
   The default is a pre-fill, never a lock — the cleaner can change it.
2. **`Employee.WorkCountryId`** decides *whether* payout details are required and at what level — it is
   the jurisdiction of the engagement, it is stored, and it is already the documented driver of
   currency/language/VAT defaults (`Employee.cs:68-75`).
3. **`BusinessCountryId` is excluded, and the reason generalizes: a governing input that is not
   persisted cannot govern.** It is a request parameter that `UpdateIdentificationInfo`'s handler never
   assigns (`:131-150`). A format rule keyed to it could not be re-evaluated on read, could not be
   re-run in a batch, and could not be rendered on an invoice — because on the next request the value
   is whatever that client happened to send. It is also *only ever sent by mobile*
   (`IdentificationSectionViewModel.kt:96-98`; zero occurrences under `src/Cleansia.App`), so keying
   payout format to it would make a cleaner's payout format depend on **which app they last used**.
   That inconsistency is real and live; it is **not this ADR's to fix** — it belongs to the
   identification path. This ADR only refuses to inherit it.

**Worked case — the Slovak cleaner working in CZ** (the case the ticket named):

| | Value | Consequence |
|---|---|---|
| `WorkCountryId` | `CZ` | Payout details **required**; CZ payout policy; CZ pay period, invoice, VS |
| `Address.CountryId` | `SK` | Pre-fills `BankCountryId = SK` only if `WorkCountryId` were null. It is not — so it does **not** pre-fill |
| `BankCountryId` (pre-filled `CZ`, cleaner changes to `SK`) | `SK` | Scheme resolves to `CzskDomesticWithIban` — SK is the **same** scheme (D4/D10) |
| `BankCountryId != WorkCountryId` | `SK` ≠ `CZ` | **SWIFT/BIC becomes required** (cross-border). For a CZ account it stays optional |

The SWIFT rule is the observable consequence that makes the two-level rule earn its keep: the same
cleaner, the same work country, a different bank country — and the required field set changes, without
a single `if (country == "CZ")` anywhere. *(Refinement noted, not decided here: the truly correct
counterparty for "is this cross-border" is the **paying entity's** country, i.e. `CompanyInfo.CountryId`.
`WorkCountryId` is used because it is on the aggregate and, while Cleansia pays CZ cleaners from a CZ
entity, the two are identical. If a paying entity is ever established in a country it does not operate
in, this rule moves to `CompanyInfo.CountryId` — a one-line change in one validator.)*

### D3 — `CountryConfiguration` grows **exactly one** column: `PayoutScheme`. It does **not** grow bank labels/formats/required flags

```csharp
/// Which payout-identifier scheme banks in this country use. Null ⇒ the country is not
/// open for payouts and only a self-describing IBAN can be accepted (ADR-0034 D4).
public PayoutScheme? PayoutScheme { get; private set; }
```

Modelled on `FiscalEnforcementMode` (`CountryConfiguration.cs:65-71`) — an enum column with a documented
null/default meaning — not on the `*Label`/`*Format`/`*Required` triples.

**Why the `RegistrationNumberLabel/Format/Required` precedent (`:43-57`) does *not* transfer:**

1. **It is the wrong arity.** That triple describes **one scalar** whose per-country variation is its
   *name* and its *regex*. A bank account is a **structure whose field count changes per country**: CZ/SK
   has prefix + number + bank code (+ a derivable IBAN); a US ACH destination has routing + account +
   account type. `BankAccountLabel` + `BankAccountFormat` cannot express "this country has three parts".
   Forcing it to would produce exactly the opaque single string this ADR exists to avoid.
2. **The precedent has not proven itself.** F3: the existing labels are read by **no client**. Copying an
   unexercised pattern is not consistency.
3. **The half that *is* per-country and *is* scalar — "which scheme" — is exactly what we take.** That is
   what keeps ADR-0017's seam intact: the validator reads `CountryConfiguration(BankCountryId).PayoutScheme`;
   **no handler branches on a country code**, and adding SK is a seed value (D10).

Field **labels** live with the scheme, in code, alongside the parser and the checksum that give them
meaning — because a label without its format is a caption, and a format without its checksum is what we
have today.

### D4 — Validation is real, it mirrors `ITaxIdValidator`'s **seam** but inverts its **failure mode**

New domain service, registered and consumed exactly as `ITaxIdValidator` is (`UpdateIdentificationInfo.cs:73-96`):

```csharp
public interface IPayoutDetailsValidator
{
    Task<PayoutValidationResult> ValidateAsync(PayoutDetailsInput input, CancellationToken ct = default);
    // PayoutValidationResult: IsValid + ErrorKey (a "validation.payout.*" i18n key), mirroring TaxIdValidationResult
}
```

**Same seam** (async, config-driven, returns an i18n key consumed by a FluentValidation `MustAsync`).
**Opposite failure mode, deliberately.** `TaxIdValidator.MatchesFormat` returns `true` on a missing
format, a regex timeout, and a malformed regex (`:54-73`) — **fail-open**, which is right for a label
check whose worst outcome is a slightly-wrong IČO. It is wrong here: the worst outcome is a payroll run
into the void or into a stranger's account.

**`IPayoutDetailsValidator` fails closed**, with one deliberate exception that costs nothing:

> **Scheme resolution.** Use `CountryConfiguration(BankCountryId).PayoutScheme` when configured.
> Otherwise, **if the supplied value is a mod-97-valid IBAN whose country prefix equals
> `BankCountryId`, accept it as `SepaIban`** — an IBAN is *self-describing by construction*, so it does
> not need our configuration to be checkable. Otherwise **reject** with a distinct key
> `validation.payout.country_not_supported`.

That exception is what keeps a German- or Polish-bank cleaner working in CZ from being blocked by a
`CountryConfiguration` row we have no other reason to create — while keeping the *local* schemes (the
ones that genuinely need our knowledge) closed to markets we have not opened.

**The checks, per scheme:**

| Check | Standard | Applies to |
|---|---|---|
| IBAN check digits — move the first 4 chars to the end, letters → digits (A=10…Z=35), **mod 97 must equal 1** | **ISO 13616-1** (structure) + **ISO 7064 MOD 97-10** (the check) | every scheme carrying an IBAN |
| IBAN country prefix **must equal `BankCountryId`** | — (a cross-check, not a standard) | every scheme carrying an IBAN |
| IBAN length must equal the registry length for its country **when the country is configured**; otherwise the generic 15–34 bound | ISO 13616 IBAN Registry | every scheme carrying an IBAN |
| CZ/SK local: **prefix ≤ 6 digits**, **number 2–10 digits**, **bank code exactly 4 digits**; prefix and number each pass the **weighted modulo-11** check (weights 6,3,7,9,10,5,8,4,2,1 applied right-to-left; weighted sum mod 11 must be 0) | the ČNB account-numbering scheme — **see the honesty note below** | `CzskDomesticWithIban` |
| BIC is 8 or 11 characters, `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$`, and characters 5–6 equal `BankCountryId` | **ISO 9362** | wherever SWIFT is supplied |

**Honesty note (AC15).** ISO 13616 / ISO 7064 / ISO 9362 are cited as public standards. The CZ/SK
modulo-11 weight vector above is stated from **secondary reading, not from the decree text**, and this
ADR does **not** assert a banking legal requirement. **T-0519 must verify the weight vector and the
CZ/SK IBAN composition against a primary source (the ČNB/NBS numbering decree and the ISO 13616 IBAN
Registry entry) before the check becomes blocking**, and must land a test vector table including at
least one known-good and one known-bad real-format account per country. If verification fails, the
CZ/SK local check degrades to *structure only* and the ADR's other decisions are unaffected.

**What happens to values already stored that would now fail:** nothing, on the day of the migration —
that is D7, and it is the reason D7 exists as a separate decision.

### D5 — The CZ field set, exactly, and what is required

For `Scheme = CzskDomesticWithIban` (CZ today, SK on a seed value):

| Field | Type / length | CZ required? | Notes |
|---|---|---|---|
| `BankCountryId` | FK `Country` | **required** | D2. Governs scheme + checks |
| `AccountPrefix` | `varchar(6)`, digits | optional | **Leading zeros are significant** — store as text, never as a number. Absent on the owner's specimen (`5885638003/5500` has no prefix) |
| `AccountNumber` | `varchar(10)`, digits | **required** | Leading zeros significant |
| `BankCode` | `varchar(4)`, digits | **required** | `5500` on the specimen |
| `Iban` | `varchar(34)` | **required — but SERVER-DERIVED, not asked for** | See below |
| `Swift` | `varchar(11)` | optional for a CZ bank; **required when `BankCountryId != WorkCountryId`** (D2) | ISO 9362 |
| `BankName` | `varchar(100)` | optional | Display only |
| `HolderName` | `varchar(200)` | optional | The beneficiary name **as the bank knows it** — may differ from the platform's name (married name, legal entity). Defaults to `LegalEntityName` for a legal entity, else `User.FirstName + " " + User.LastName`. This is the "what else is needed to make a payment" the owner's phrasing gestured at |
| `ProviderAccountRef` | `varchar(100)` | n/a | Reserved for D9. **Never a PAN** |
| `Status` | enum | **required** | `Provided` \| `NeedsReconfirmation` |
| `ConfirmedAt` | `timestamptz?` | — | When details last passed the real validator |
| `LegacyRawValue` | `varchar(50)` | — | **Migration-only park.** D7 |

**The IBAN is computed, not collected.** For CZ/SK the IBAN is a deterministic function of the local
parts: `CC` + 2 check digits + bank code (4) + prefix (6, zero-padded) + account number (10,
zero-padded) = 24 characters, check digits by ISO 7064 mod-97-10. So:

- The **local parts are the source of truth**; the server derives and stores the IBAN.
- If the cleaner also types an IBAN, it must **equal** the derived value or the write is rejected with a
  specific key. Two renderings of one account that disagree on one document is a failed payment; the
  specimen shows both, so they must be provably the same account.
- The CZ form is therefore **two fields** (`number` + `bank code`, plus an optional prefix) — which
  kills a whole class of 24-character typos rather than validating them.
- *(Marked for T-0519 verification with the same rigour as the mod-11 vector: the CZ/SK IBAN composition
  above is a read of the ISO 13616 registry entry, not a run. If it does not hold, the IBAN becomes a
  collected-and-checked field instead of a derived one — a change to one validator, not to the shape.)*

**What is NOT a payout-details field, and where it lives instead:** **variabilní symbol** is already
`EmployeeInvoice.VariableSymbol` (`:72`, generated `:331`) — it is per-invoice, not per-cleaner.
**Konstantní symbol** and **due date** are per-invoice/per-policy and belong to **T-0508**. Neither
enters this record. Stated explicitly so T-0508/T-0522 do not add them here.

### D6 — **No column-level encryption in v1.** The boundary is storage-level encryption plus a masked read contract — and the reasons and the reversal triggers are written down

**Current posture, verified first:** the repo does **nothing** at the column level. The only sensitive
converter is `PasswordConverter.cs:6`, a one-way hash — structurally unusable for a value that must be
rendered on a document. No `IDataProtection` usage on any server path.

**Decision: the new columns are plaintext, and that is a decision, not an omission.**

1. **It would be theatre in this system.** The account number is **printed on an invoice the cleaner
   receives**, emailed, exported in the GDPR export (`GdprExportService.cs:38`), and read by whoever
   executes the transfer. Application-level encryption defends against *stolen database files and
   backups* — a threat already covered by Azure Database for PostgreSQL Flexible Server's
   service-managed encryption at rest *(read of the Azure product documentation; **not** verified
   against this repo's Bicep — **T-0518 must confirm it on the DEV/PROD server and record the finding**)*.
   It does **not** defend against the application, which is the component that reads and renders the
   value. Encrypting a column whose contents we email is not a security control.
2. **It removes controls we may want.** An encrypted column cannot be indexed or uniquified — and
   "two employees sharing one account number" is a real fraud signal we may want to check.
3. **Key management is the actual cost.** Rotation, envelope keys, dev/prod parity, restore-from-backup,
   and a break-glass path — adopted for one field with no key-management story is worse than not
   adopting it, because it produces confidence without protection.

**What we do instead, and it is not weaker — it is aimed at the exposure that is actually real (D8):**
the read contract, not the disk. Today's genuine leak is `EmployeeListItem.cs:52` shipping the account
identifier in a **paged list** response — which no amount of at-rest encryption would have prevented.

**Reversal triggers — any one of these reopens this decision as a superseding ADR:**
(i) the record starts holding something that is *not* printable on the cleaner's own invoice (a payout
KYC national id, a PSP secret); (ii) a second tenant/franchise goes live with operators who must not see
each other's cleaners beyond the tenancy filter; (iii) an external processor or BI tool gains direct DB
access; (iv) the platform takes on a contractual/regulatory obligation naming encryption at rest for
financial identifiers.

**Out of scope here, already owned:** the IBAN's exposure in logs and in the GDPR export is **T-0509**.
This ADR does not re-decide it; it only requires that the new fields inherit whatever T-0509 lands, and
that S6 (never log the value) applies from day one.

### D7 — The completeness gate is **decoupled from payout validity**; legacy rows are classified, never guessed; the hard stop moves to where money actually moves

This is the decision F2 forces, and it is the one that protects live cleaners.

**The rule:**

> **`IsProfileComplete()` means "payout details EXIST".** It does **not** mean "payout details are
> valid". Real validation (D4) applies to **writes** from the day it ships, and to **payout issuance**.
> It does **not** retroactively invalidate a profile.

- `Employee.cs:283` changes from `!string.IsNullOrEmpty(IBAN)` to **`PayoutDetails is not null`** —
  satisfied by *every* migrated row, including parked ones. **No cleaner loses job-board access on
  migration day.** This is the single most important property of the migration.
- **The hard stop moves to invoice/payout issuance**: `EmployeeInvoice` generation refuses when
  `PayoutDetails is null || Scheme is null || Status != Provided`, recording an admin-visible
  `PayoutBlocked` reason. **Why the invoice is withheld rather than issued without a payment block:** an
  invoice is a sequence-numbered legal document (T-0508 AC5 already raises gaplessness). Deferring
  issuance is reversible; burning a sequence number on a knowingly defective document and reissuing is
  not. Pay periods are bi-weekly, so the reconfirmation prompt has a window measured in days, not hours.
- **`GetMissingProfileFields()` keeps emitting `"profile.fields.iban"` (`Employee.cs:313`) in v1.**
  The server emits the key and **five shipped clients translate it**, two of them app-store-gated.
  Renaming it to `profile.fields.payoutDetails` would show a raw key to every device that has not
  updated, for zero user benefit. Rename on a coordinated mobile release; carry an in-code comment
  saying exactly that, so the mismatch reads as a decision rather than as rot.

**The backfill classifier — three classes, and a fourth that is not migrated:**

| Class | Test | Lands as |
|---|---|---|
| **0 — anonymized** | value == `AnonymizationMarker.Value` (`"[DELETED]"`) | **No payout record at all.** The person is gone; parking their row as "please reconfirm" would be both wrong and a re-identification prompt |
| **1 — a real IBAN** | passes ISO 7064 mod-97 | Migrated as valid. If the prefix is **CZ or SK**, the IBAN is **decomposed** back into bank code / prefix / account number (deterministic and lossless) → **`Scheme = CzskDomesticWithIban`, `Status = Provided`, `ConfirmedAt` = migration time, a complete payment block for T-0522 with zero cleaner action.** Otherwise `Scheme = SepaIban`, `Status = Provided` |
| **2 — a mangled domestic number** | fails mod-97, but looks like a stripped CZ/SK account | **Class 3. Do not reconstruct.** F1: the stripped separators are irrecoverable and a wrong guess is a transfer to a stranger. Deliberately not clever |
| **3 — everything else** | anything remaining | `Scheme = null`, `BankCountryId = null`, identifier fields null, **`LegacyRawValue` = the original string verbatim**, `Status = NeedsReconfirmation`. Profile stays complete; a non-blocking prompt appears; the next write must pass D4; payout is blocked until it does |

**The backfill principle, stated once so T-0518 does not re-derive it:**
> **Legacy validity is decided by running the *new* validator over the *old* value. Anything the new
> validator would accept on a fresh write, we accept without troubling the cleaner. Everything else is
> parked, preserved, and re-asked.**

`LegacyRawValue` exists so nothing is ever silently dropped and so the cleaner's prompt can say *"we
have `1920001453990800` on file — please re-enter it as number and bank code"*, which is a far better ask
than an empty form. It is **write-once by the backfill script, never written by application code**, and
a follow-up ticket **drops the column** once the reconfirmation campaign closes. That lifecycle is part
of this decision — an unbounded escape-hatch column would be a smell; a scheduled one is a plan.

**Operational shape:** owner-run EF migration (`manual_steps: ef-migration`, T-0518) and an owner-run
**SQL script**, not startup code (T-0518 AC9), with a dry-run that reports the class counts before
anything is written.

### D8 — The read contract: payout details never ride a list DTO, are masked by default, and an admin reveal is an audited action

The exposure D6 declines to solve with cryptography is solved here, structurally:

1. **`Iban` is removed from `EmployeeListItem` (`:52`) and `EmployeeItem` (`:27`).** A paged list of
   employees must not carry payout identifiers. *(**Breaking NSwag change** → `MANUAL_STEP` for the
   owner; the admin employee-detail feature reads `iban` today and is reworked by T-0520.)*
2. **One single-resource read** — `GET .../employees/{id}/payout-details` — authorized to the **owner of
   the record** or an **admin**, following S3 (resource-by-id ownership check) exactly as
   `UpdateBankDetails.Validator.AllowedToUpdateEmployee` (`:39-44`) does for writes.
3. **Masked by default** (`****3003`) everywhere except (a) the owner's own edit form — it is their
   account and they must be able to check it — and (b) the **server-side** invoice renderer.
4. **An admin viewing the unmasked value is an explicit reveal action that writes an audit entry**, per
   ADR-0012 D4.1 (ids, not the PII — `AdminUpdateEmployee.cs:101` is the precedent).
5. **S6:** the value is never logged at any level; **anonymization:** `Employee.Anonymize()` clears the
   whole payout record (T-0518 AC6), and `EmployeeUserAuditCoverageTests` is extended to assert every
   new field is absent from audit JSON — not just the one that used to be there.

### D9 — **No PAN column. Ever.** "Card number" in the owner's list is not a schema field

**Stated plainly, because the owner's phrasing must not be read as a column request:** a card number
(PAN) will not be stored in this database, encrypted or otherwise, and no field on
`EmployeePayoutDetails` accepts one.

**Two reasons, and the second is the one that matters more:**

1. **Scope.** Storing a PAN brings the platform, its database, its backups, its logs and everyone with
   access to them into **PCI DSS** scope *(cited as an industry standard; this is not legal advice and
   no agent here asserts a legal requirement — the scoping consequence is attributable to the PCI SSC's
   published standard and is a business decision for the owner, several orders of magnitude larger than
   a payout field)*.
2. **It is not how you pay someone by card anyway.** You do not push a payout to a PAN. A card payout is
   a **network payout to a tokenised destination held by a PSP**, and what you store is an **id**.

**What a card payout would actually require** (so the option stays open and is honestly priced):
PSP payout onboarding (Stripe Connect Express or equivalent) · **KYC/KYB on each cleaner**, done by the
PSP · an onboarding-link flow and a **webhook-driven account-status lifecycle** (`restricted` → `enabled`
→ `disabled`) · a payout-execution path with idempotency (S7) · per-country PSP availability and fees ·
and a reconciliation story against `EmployeeInvoice`. **That is a separate epic** ("moving money"), and
it is explicitly out of scope of both this ADR and T-0518–T-0521.

**What this shape already does for it, at zero cost:** `Scheme = ProviderPayoutToken` +
`ProviderAccountRef` (an id — e.g. `acct_…`) with every bank field null. **Zero migrations.** That is
the extensibility claim tested against the hardest case, not asserted.

### D10 — What the second country actually costs (AC3 — priced, not asserted)

| Adding… | Migrations | Backend code | Config/data | Client changes |
|---|---|---|---|---|
| **SK** | **0** | **0** | **1 value**: `CountryConfiguration('SK').PayoutScheme = CzskDomesticWithIban` | **0** — the CZ form already renders this scheme |
| **A SEPA/IBAN market** (DE, PL, AT…) | **0** | **0** | 1 value (`SepaIban`) — **or 0**, because a valid IBAN self-identifies (D4) | **0** — the IBAN-only form is already a scheme layout |
| **A non-IBAN market** (e.g. US ACH: routing + account + account type) | **1 additive** (the genuinely new columns) | 1 enum value + 1 scheme validator + its checksum | 1 value | **1 per client** — the new fields must be rendered somewhere. **Not zero, and this ADR does not pretend otherwise** |
| **A PSP token payout** (D9) | **0** | payout epic, not schema | 1 value | 1 (an onboarding link, not a form) |
| **A second destination per cleaner** | 1 (drop unique index, add `IsPrimary`) | primary-selection rule | 0 | 1 |

**Why SK is free is not luck** — SK inherited the Czechoslovak account-numbering structure, so it is the
*same scheme*, not a second one. That is the D1 framing paying off: we generalized along **scheme**, and
two countries share one. *(SK's identity of structure and check-weights with CZ is a **read**; T-0519
verifies it against the NBS source before SK ships, exactly as for CZ.)*

Compare, honestly, with the alternatives: under **(a) flat columns**, SK is also 0 migrations — but the
first non-IBAN market adds its columns to `Employee` itself, and by the fourth market `Employee` is a
sparse union of every country's bank scheme with no discriminator saying which subset is meaningful for
a given row. Under **(c) config-only**, SK is 0 — and the CZ invoice **cannot be rendered at all**, which
is a failure at country #1.

---

## Alternatives considered, and why not

| # | Alternative | Why not |
|---|---|---|
| **A1** | **Flat nullable columns on `Employee`, mirroring `CompanyInfo`** (`BankName`/`BankAccountNumber`/`Iban`/`Swift`) — the cheapest thing today | **Closest loser; it fails on three counts, none of which is cost.** (i) **No discriminator** — nothing on the row says which subset of columns is meaningful, so every reader re-derives it, which is a country branch in disguise; (ii) **the read contract stays broken by construction** — `Iban` rides `EmployeeListItem` (`:52`) *because* it is an `Employee` property that the mapper flattens, and four more such properties make it four times worse; (iii) `Status`/`ConfirmedAt`/`LegacyRawValue` (D7) are not employee attributes, and without them **there is no migration that does not lock cleaners out** (F2). `CompanyInfo` is a **singleton** describing **one** company in **one** country — it never had to be a discriminated shape, so it is not the precedent it looks like |
| **A2** | **`CountryConfiguration` grows `BankAccountLabel/Format/Required`; the value stays one string** | **Fails at country #1.** The owner's own specimen needs local account number **and** IBAN **and** SWIFT *simultaneously on one document* — you cannot render three renderings from one opaque string (T-0517 AC9 / T-0508 AC8). It also copies an **unexercised** precedent (F3) and is the wrong arity for a multi-part identifier (D3) |
| **A3** | **`Scheme` + a JSON `DetailsJson` bag** — zero migrations forever | Tempting, and the repo does have `JsonValueConverter` (`Employee.Availability`, `LegalRequirementsJson`). Rejected for **financial identifiers rendered on a legal document**: no DB constraint, no index, no uniqueness check, no typed DTO (NSwag emits an opaque blob and every client hand-parses), and the invoice builder becomes a JSON parser. "Zero migrations forever" is a real benefit paid for with **zero guarantees forever** — the wrong trade on the one table that must not be wrong. **Typed sparse columns keep the guarantees and cost one additive migration per genuinely new field** (D10) |
| **A4** | **A general `PayoutMethod` collection with no cardinality constraint** ("real" extensibility) | Buys a capability with **no evidence of demand** and immediately owes a *"which one is primary"* rule that every reader (invoice, payout run, admin, GDPR export, profile completeness) must honour — five places to get wrong, today, for a feature nobody asked for. **The unique index is the reversible form of this**: lifting it later is one additive migration (D1) |
| **A5** | **EF owned types / a `BankAccount` value object embedded on `Employee`** | Zero occurrences of `OwnsOne`/`OwnsMany`/`ComplexProperty` in `Cleansia.Infra.Database` (verified). Introducing an unfamiliar persistence pattern on the highest-consequence table is a bad trade; it also still leaves the fields flattened onto the `Employee` row for DTO purposes, so it does not buy A1's fix either |
| **A6** | **Keep one string; just add mod-97 to `ValidateIban()`** — a two-line fix | Would **immediately reject the domestic account numbers already stored from mobile** (F1) and every future one, permanently entrenching the CZ specimen's `5885638003/5500` as unenterable. It also cannot produce the payment block (AC9). It is the *smallest* change and the *only* one that makes the live product worse |
| **A7** | **Encrypt the columns at rest (`ValueConverter` + a key)** | D6: theatre while we print, email and GDPR-export the value; it removes indexing/uniqueness we may want; and key management is an unpaid operational bill. **Reversal triggers written down** rather than the question left silently open |
| **A8** | **Make the migration re-derive separators for mangled domestic numbers** ("we can mostly tell") | `1920001453990800` is *equally consistent* with `19-2000145399/0800` and `192000145399/0800`. A wrong guess pays a stranger and looks like a successful payroll run. **Not clever on purpose** (D7 class 2 → 3) |
| **A9** | **Key the format to `BusinessCountryId`** (the only country field that already drives a per-country validator) | It is **never persisted** (`UpdateIdentificationInfo.cs:131-150`), so it cannot be re-evaluated, re-run, or rendered; and it is **sent only by mobile**, so payout format would depend on which app the cleaner last used (D2) |
| **A10** | **Require re-validation for the completeness gate** ("if it's invalid, the profile is incomplete") | Takes every class-2/3 cleaner off the job board on migration day (F2). Correctness of the *field* is not worth an outage of the *person's income*; the hard stop belongs at issuance, where the failure actually occurs (D7) |

---

## Consequences

**Good**
- Adding SK costs **one seed value**; adding an IBAN market costs **zero or one**; a PSP payout costs
  **zero migrations**. The extensibility claim is priced (D10), including the case where it is *not* free.
- The CZ payment block (T-0522) is renderable from the record, and **class-1 CZ rows produce a complete
  payment block with no cleaner action at all** — the IBAN decomposes back into the local pair.
- The CZ capture form drops to **two fields**, and the IBAN is derived rather than typed — removing an
  entire class of 24-character typos instead of validating them.
- Validation becomes real (mod-97 + mod-11) without any handler branching on a country code — ADR-0017's
  seam intact.
- **No cleaner is locked off the job board by the migration** (D7).
- The genuine confidentiality leak (a payout identifier in a paged list, `EmployeeListItem.cs:52`) is
  closed structurally.

**Costs, accepted**
- One join on the profile read path. Mitigated by the unique index; the read is by employee id.
- A **breaking NSwag change** (two DTOs lose `Iban`) → owner regenerates; admin employee-detail is
  reworked in T-0520.
- Three write paths converge on one command shape; `UpdateEmployee`/`AdminUpdateEmployee` stop carrying a
  bare `Iban` string. This is churn in T-0519, and it is the point — one write path for payout details.
- A **reconfirmation campaign** for class-3 cleaners, plus a `LegacyRawValue` column that must be
  **dropped by a follow-up ticket** once the campaign closes.
- `"profile.fields.iban"` now names a record rather than a field until a coordinated mobile release
  renames it (D7). A deliberate, commented mismatch.

**Explicitly unchanged** — the tenancy filter and `ITenantEntity` usage; the `basePay/extras/expenses/
clamp/bonus-deduction` formula and `EmployeePayConfig` (IMP-3); the fiscal enforcement modes; the
per-audience host separation (this is Core + Infra + Config only).

---

## Cross-references the panel must not let drift

- **T-0511 AC5 (the same generality question, same week).** The answer must be the **same principle**,
  not the same shape: *generalize along the axis that varies; pin the axis that does not.* For membership
  benefits the varying axis **is** the benefit set (Plus has five perks and a sixth is plausible) → **one
  table keyed by benefit** is right there. For payout details the varying axis is the **scheme**, and the
  count is **not** varying → **discriminated, cardinality one**. Both ADRs generalize; they generalize
  along **different axes**, and that is the consistent outcome, not a contradiction. **If T-0511 lands a
  column-per-benefit answer, one of the two ADRs is wrong and the panel says so before either ships.**
- **T-0508 / T-0522 (the invoice).** This ADR supplies the **cleaner's** bank block only. **Variabilní
  symbol** is `EmployeeInvoice.VariableSymbol` (already exists, `:72`); **konstantní symbol**, **due
  date**, and the QR *Platba+F* code are T-0508's, and **none of them enters this record**. T-0522 must
  handle `Status = NeedsReconfirmation` per D7 (issuance withheld, admin-visible blocker).
- **T-0509** owns the IBAN's exposure in logs and the GDPR export; the new fields inherit its outcome.
- **The identification-path country inconsistency** (`BusinessCountryId` mobile-only, discarded) is a
  real finding surfaced by this work and is **not fixed here** — D2 only refuses to inherit it. It needs
  its own ticket.

## Ticket sizing (AC14) — three of the four downstream tickets are `L` as written

| Ticket | As filed | This ADR's shape makes it | Split |
|---|---|---|---|
| T-0518 (db) | `M` | **`L`** — entity + config + `CountryConfiguration` column + two enums + owner migration **+ a four-class backfill with a dry-run** | **Split**: schema/entity/migration ‖ **the backfill script + `Status` semantics + the issuance block**. The second is where the risk is and it should be reviewable alone |
| T-0519 (backend) | — | **`L`** — validator service + checksums + **T-0519's primary-source verification duty** + three write paths converging + completeness-gate change + the D8 read contract | **Split**: validator + capture ‖ read contract/masking + the admin audited reveal |
| T-0520 (web/admin) | — | `M` | Keep. Note the NSwag `MANUAL_STEP` and the admin employee-detail rework |
| T-0521 (mobile) | — | **`L`** — two platforms × a scheme-driven multi-field form × 5 locales, **and the existing normalizers must stop stripping `-` and `/`** (F1) | **Split per platform** (Android ‖ iOS), as every other mobile ticket in this repo is |

---

## Reviewer verification — how compliance is checked

A reviewer confirms this ADR was followed by checking, in order:

1. **No country-code branch.** `rg -n '"CZ"|"SK"|== *"CZ"' src/Cleansia.Core.AppServices src/Cleansia.Core.Domain`
   returns **no hit in a handler or a validator**. Scheme selection reads
   `CountryConfiguration.PayoutScheme` (D3) or the IBAN's own prefix (D4).
2. **Cardinality is enforced in the schema, not in code.** A unique index on `(TenantId, EmployeeId)` in
   `EmployeePayoutDetailsEntityConfiguration`. No `IsPrimary` field exists.
3. **`ITenantEntity` + `Auditable`** on the new entity, matching `EmployeePayConfig` (D1). No
   hand-rolled tenant scoping.
4. **Validation is real and fails closed.** A test asserts `"totally not an iban!!"` is **rejected**; a
   test asserts a valid-structure/invalid-check-digit IBAN is **rejected**; a test asserts an unknown
   `BankCountryId` with a non-IBAN value yields `validation.payout.country_not_supported`; and a test
   asserts a valid IBAN for an **unconfigured** country is **accepted** as `SepaIban` (the D4 exception).
   The CZ/SK weight vector carries a **cited primary source** in a code comment (D4 honesty note).
5. **The IBAN is derived for CZ/SK**, and a test asserts a cleaner-supplied IBAN that disagrees with the
   derived one is **rejected** (D5).
6. **The completeness gate does not depend on validity.** A test constructs an employee whose payout
   record has `Status = NeedsReconfirmation` and asserts `IsProfileComplete() == true`. `Employee.cs:313`
   still emits `"profile.fields.iban"` and carries the D7 comment explaining why.
7. **Issuance is blocked, not the profile.** A test asserts `EmployeeInvoice` generation refuses for
   `Status != Provided` and records the admin-visible reason.
8. **The backfill is a script the owner runs**, with a dry-run reporting the four class counts, and a
   test/fixture proving class 0 (`"[DELETED]"`) produces **no** payout record and class 2 is **never**
   reconstructed (D7 / A8).
9. **`Iban` is gone from `EmployeeListItem` and `EmployeeItem`**; no list/paged DTO anywhere carries a
   payout identifier; the single-resource read enforces owner-or-admin (S3) and masks by default; the
   admin reveal writes an audit entry (D8 / ADR-0012 D4.1).
10. **Anonymization and the audit test cover every new field** — `Employee.Anonymize()` clears the
    record, and `EmployeeUserAuditCoverageTests` asserts each new field's absence from audit JSON
    (T-0518 AC6).
11. **No PAN.** No field on the entity, no DTO property, and no client input named or shaped like a card
    number. `ProviderAccountRef` carries a comment saying it holds an **id**, never a number (D9).
12. **`CountryConfiguration` grew exactly one column.** If a `BankAccountLabel`/`Format`/`Required`
    triple appears, D3 was not followed.
13. **The at-rest posture is recorded at the column** (T-0518 AC5): a comment naming ADR-0034 D6 and the
    reversal triggers, plus T-0518's finding on whether Flexible Server encryption at rest is confirmed
    on DEV/PROD.

---

## Gate 0.5 leg 3 — what this panel did NOT examine, and which claims are reads rather than runs

**Not examined:**
- **No code was executed.** No build, no test run, no migration, no query against DEV. Every claim
  about the working tree is a **read at a cited file:line**, verified 2026-08-02.
- **The actual contents of the DEV `Employee.IBAN` column were not inspected.** The four-class
  classifier is designed from the *code paths* that write the column, not from a census of the data.
  **T-0518's dry-run is what turns this from a design into a fact** — and if the class counts are
  wildly different from the design's assumption (e.g. class 3 is the majority), T-0518 stops and
  re-opens D7 rather than proceeding.
- **The Angular admin/partner UI was surveyed only for IBAN occurrences**, not audited. T-0520 owns the
  real inventory of call sites broken by the DTO change.
- **No performance work.** The added join was reasoned about, not measured.
- **Slovak, Ukrainian, Polish and German banking formats were not researched beyond the SK↔CZ
  structural identity claim**, which is flagged for verification.

**Claims that are reads, not runs, and who must verify them before they become blocking:**

| Claim | Status | Verifier |
|---|---|---|
| CZ/SK domestic modulo-11 weight vector (6,3,7,9,10,5,8,4,2,1) | **read, secondary source** | **T-0519**, against the ČNB/NBS numbering decree, with a test-vector table |
| CZ/SK IBAN composition (`CC` + 2 + bank 4 + prefix 6 + account 10 = 24) and therefore the derivation *and* the class-1 decomposition | **read, ISO 13616 registry** | **T-0519**, against the registry. If it fails, the IBAN becomes collected-and-checked; the shape is unaffected |
| SK shares CZ's account structure and check weights | **read** | **T-0519**, before SK ships |
| Azure Database for PostgreSQL Flexible Server encrypts at rest with service-managed keys by default | **read of product documentation; not verified in this repo's Bicep** | **T-0518**, on the actual DEV/PROD server |
| Storing a PAN brings the platform into PCI DSS scope | **cited to the PCI SSC's published standard.** **Not legal advice; no agent here asserts a legal requirement.** The consequence (a materially larger compliance surface) is the owner's call, and D9's decision stands on reason 2 alone even if reason 1 is negotiated | **owner**, if card payouts are ever pursued |
| What a CZ/SK supplier invoice must legally contain | **not decided here** — T-0508, from the owner's specimen. This ADR supplies data, not invoice law | **T-0508** |

---

## Challenge

> **Awaiting the panel.** Two challengers, per T-0517's implementation notes: one from the **security**
> angle (D6, D8, D9) and one from the **db** angle (D1, D5, D7). A challenger that finds nothing must
> name what it checked — silence is not assent (`process/deliberation.md`).

**The author's own assessment of where this decision is weakest — attack here first:**

1. **D1 vs A1 is the closest call in the ADR.** The honest case for flat columns on `Employee` is that
   `CompanyInfo` already proves the shape works, the join is real, and cardinality-one means the child
   table buys nothing *structurally* today. The defense rests on the discriminator, the read contract,
   and the migration-state fields — press on whether those three are worth a table.
2. **D5's derived IBAN depends on an unverified composition.** If the ISO 13616 CZ/SK composition read
   is wrong, the two-field CZ form and the class-1 decomposition both fall back, and the "no cleaner
   action needed" consequence weakens. Is flagging it for T-0519 enough, or should the ADR decide the
   fallback as the *primary* and treat derivation as an enhancement?
3. **D6 declines encryption on a compliance-adjacent field.** The strongest counter is not technical —
   it is that "we print it anyway" is an argument for fixing the printing, not for leaving the column
   bare, and that a reversal later means re-encrypting live data.
4. **D7 keeps a `LegacyRawValue` column.** An escape hatch with a promised drop date is still an escape
   hatch. Is the drop ticket enough, or should the parked value live outside the operational table?
5. **D7 withholds the invoice for an unverified account.** A cleaner who has worked a full pay period
   and does not get their tax document because a *migration* reclassified their bank details has a real
   grievance. Is the "bi-weekly window" mitigation actually sufficient, and who tells them?
6. **D2 uses `WorkCountryId` as the cross-border counterparty** where `CompanyInfo.CountryId` is
   arguably the truthful payer. The ADR notes it and moves on — is noting it enough?

## Defense

> Pending — the author answers each challenge with REBUT (evidence at file:line) / CONCEDE + REVISE
> (the artifact changes) / ESCALATE (`questions/open.md`).

## Verdict

> Pending the lead. Consensus = zero blocking challenges. Not `accepted` until then; T-0518 does not
> start before it.
