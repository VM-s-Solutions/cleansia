# Role — `IPayoutDetailsValidator` / `PayoutDetailsValidator` (CRC card)

> Introduced by **ADR-0034** (**`accepted`** 2026-08-02). A domain service in
> `Cleansia.Core.AppServices.Services`,
> **sibling to `ITaxIdValidator`** — same seam (async, `CountryConfiguration`-driven, returns an i18n
> error key consumed by a FluentValidation `MustAsync`), **opposite failure mode**.

## Responsibility (one sentence)
Decide, once for the platform, whether a proposed payout destination is acceptable — resolving the
**scheme** from `CountryConfiguration(BankCountryId).PayoutScheme` (or from a self-describing IBAN when
the bank's country is unconfigured), enforcing the **required-field set** for that scheme, and running
the **real checks** (ISO 13616 + ISO 7064 mod-97-10 on an IBAN, ISO 9362 on a BIC, the CZ/SK weighted
modulo-11 on a domestic prefix/account, and a **Luhn rejection of anything card-shaped**) — and
**failing closed** when it cannot decide.

> **The CZ/SK modulo-11 rule, stated correctly — get this wrong and the platform rejects nine out of ten
> real Czech accounts, including the owner's own.** *Zero-pad each of the prefix and the account number
> to **10 digits**; apply weights **6, 3, 7, 9, 10, 5, 8, 4, 2, 1 LEFT-TO-RIGHT**; the weighted sum mod
> 11 must be **0**.* Applying that vector right-to-left is a different function: `5885638003` scores
> **0 (valid)** left-to-right and **1 (rejected)** right-to-left. Zero-padding to 10 also settles the
> prefix: a 6-digit prefix needs the **last** six weights, not the first, and padding makes one uniform
> rule serve both fields.

## Collaborators
- **`ICountryConfigurationRepository`** — the single source of the country→scheme mapping. Exactly as
  `TaxIdValidator` consumes it (`TaxIdValidator.cs:19,39`).
- **The three write paths** — `UpdateBankDetails`, `UpdateEmployee`, `AdminUpdateEmployee` — consume it
  from their `Validator` via `MustAsync`, mirroring `UpdateIdentificationInfo.cs:73-96`.
- **`Employee.WorkCountryId`** — the cross-border input that makes SWIFT required.
- **The backfill script (T-0518, D7.3 Branch B only)** — runs this *same* validator over each legacy
  `Employee.IBAN` value. That is the backfill principle: *anything a fresh write would pass, we accept
  — except a PAN, which is neither parked nor preserved.*
- **`BusinessErrorMessage` / the `validation.payout.*` i18n keys** — its output vocabulary, including the
  distinct `validation.payout.country_not_supported` that separates *"your account is wrong"* from
  *"we do not support your bank's country yet"*.

## Does NOT know
- **How to persist anything.** It returns a verdict; it never writes.
- **Which country the cleaner lives in, is a national of, or is registered in.** It is handed a
  `BankCountryId` and the identifiers. It never reads `Address.CountryId`, `NationalityId`, or the
  unpersisted `BusinessCountryId`.
- **Whether a profile is complete or a cleaner may work.** Its verdict binds **writes** and **payout
  issuance** only — never `IsProfileComplete()` (ADR-0034 D7).
- **What a bank is called or which BIC a bank code maps to.** `BankName` and `Swift` are captured, not
  looked up. A bank-code registry is a later, purely additive improvement.
- **How money moves, or anything about a PSP.** `ProviderPayoutToken` is a reserved scheme with no
  validation in v1.
- **How an error is shown.** It emits keys; translation and field-binding belong to the clients.

## The one deliberate departure from its sibling, and why
`TaxIdValidator.MatchesFormat` (`:54-73`) returns **`true`** on a missing format, a `RegexMatchTimeoutException`
and an `ArgumentException` — **fail-open**, correct for a label check whose worst outcome is a slightly
wrong IČO. `PayoutDetailsValidator` **fails closed**: an unknown scheme, an unconfigured bank country, or
an internal error is `Invalid`. The worst outcome here is a payroll run into a stranger's account.

**One exception, and it costs nothing:** a **mod-97-valid IBAN whose country prefix equals
`BankCountryId`** is accepted as `SepaIban` even when that country has no `CountryConfiguration` row —
an IBAN is self-describing by construction, so it does not need our configuration to be checkable. This
is what keeps a German-bank cleaner working in CZ from being blocked by a config row we have no other
reason to create, while the *local* schemes stay closed to markets we have not opened.

## Invariants a reviewer checks
- **No country-code literal** in this service or in any handler/validator that calls it — the scheme
  comes from config or from the IBAN itself (ADR-0017's seam).
- Tests assert: `"totally not an iban!!"` rejected · valid-structure/bad-check-digit IBAN rejected ·
  IBAN prefix ≠ `BankCountryId` rejected · unknown country + non-IBAN → `country_not_supported` ·
  valid IBAN for an **unconfigured** country **accepted** · **a Luhn-valid 16-digit value rejected with
  `validation.payout.looks_like_card`** (the "no PAN, ever" invariant as a runtime guard — a check on
  field *names* cannot catch data, and today's `Length(15, 34)` accepts a PAN).
- **`5885638003` is a required known-good test vector.** A mod-11 implementation that rejects the
  owner's own account is the specific bug this role was amended to prevent.
- The CZ/SK modulo-11 weight vector **and its direction and padding rule**, and the CZ/SK IBAN
  composition, carry a **cited primary source** in a code comment with a test-vector table (ADR-0034's
  honesty note — T-0519 verifies against the ČNB/NBS decree and the ISO 13616 registry, and no agent
  asserts a banking legal requirement). **Flag the whole rule, not the part you doubt:** the original
  note flagged the *weights* and the defect was the *direction*.
