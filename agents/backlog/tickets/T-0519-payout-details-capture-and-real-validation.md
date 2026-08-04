---
id: T-0519
title: Partner payout details — capture, real validation and the API contract
status: draft
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0518]
blocks: [T-0520, T-0521, T-0522]
stories: []
adrs: []
layers: [backend]
security_touching: true
manual_steps: [nswag-regen]
sprint: 15
---

## Context

The command/validator/DTO half of **T-0517**'s ADR, on top of **T-0518**'s schema.

**The validation gap is the reason this ticket is not cosmetic.** PM-verified at `master` 2026-08-02:

```
ValidationExtensions.cs:122-130
    ValidateIban<T>() => .Cascade(Stop).NotEmpty().Length(15, 34)
```

**That is the entire server-side validation of a cleaner's bank account**, on all three write paths
(`UpdateBankDetails.cs:36`, `UpdateEmployee.cs:128`, `AdminUpdateEmployee.cs:73`). No mod-97 checksum,
no country prefix rule, no length-per-country table. **`"totally not an iban!!"` is 21 characters and
passes.** Once the invoice carries these details (T-0522) and a human keys a transfer from them, a
typo is a failed payroll run or a payment to a stranger.

The archetype for doing it properly already exists in this codebase: `ITaxIdValidator` /
`TaxIdValidator`, consumed per-country by `UpdateIdentificationInfo.cs:73-96` for IČO and DIČ.

## Acceptance criteria

- [ ] **AC1 — the commands match the ADR's field set**, and `UpdateBankDetails` (the cleaner's own
      self-service path) accepts all of them. Today it takes exactly one `Iban` string
      (`UpdateBankDetails.cs:47-49`). Evidence: the command records plus the ADR citation.
- [ ] **AC2 — IBAN validation is REAL: ISO 13616 mod-97, with the country-length table.** Evidence: a
      test table with at least one valid and one invalid IBAN **per country in scope**, plus a
      checksum-fails case and a right-length-wrong-checksum case.
- [ ] **AC3 — the CZ local account number is validated to its actual form**, `[prefix-]number/bankcode`
      (the owner's own invoice shows `5885638003/5500`), including the mod-11 weighted check if ADR AC6
      specified it. Evidence: the validator plus the test table.
- [ ] **AC4 — SWIFT/BIC is validated to ISO 9362 shape** (8 or 11, the documented character classes) if
      the ADR includes it. Evidence: the validator plus tests.
- [ ] **AC5 — validation follows `ITaxIdValidator`'s per-country pattern or the departure is
      defended.** Evidence: the service plus the archetype citation at file:line.
- [ ] **AC6 — all THREE write paths use the same validation.** `UpdateBankDetails`, `UpdateEmployee`,
      `AdminUpdateEmployee`. **A validated self-service path and an unvalidated admin path is the same
      defect with a longer fuse.** Evidence: the three call sites.
- [ ] **AC7 — every new error key exists in `BusinessErrorMessage` with dot notation and has its five
      frontend translations named** under `errors.*` per `CLAUDE.md`. **Note the per-client namespace
      trap** (each client uses a different prefix — see the project memory) — name the key **per
      client**, not once. Evidence: the key list plus the per-client mapping.
- [ ] **AC8 — the value is never logged, and this is CHECKED rather than assumed.** Sprint-14 proved
      this platform writes PII into Information-level request logs on all five hosts (**T-0457**), and
      **T-0470** proved a secret whose field name is not `*Secret*`/`*Token*`/`*Key*`/`*Password*`-shaped
      is caught by nothing. **An account number is exactly a T-0470-class value.** Evidence: the check
      plus a cross-note on T-0509, which owns the same sweep for the existing field.
- [ ] **AC9 — the GDPR export carries the new fields.** `GdprExportDto.cs:41` exports `IBAN` today;
      **a subject-access export that silently drops the new payout columns is a compliance regression
      introduced by a feature.** Evidence: the updated DTO plus a test.
- [ ] **AC10 — the admin list DTO's exposure is reviewed.** `EmployeeListItem.cs:52` carries `Iban`
      **on a list DTO**. Whether a paged admin list needs full account details is a question this
      ticket must answer, not inherit. Evidence: the ruling.
- [ ] **AC11 — the SECURITY gate runs.** `security_touching: true`: storage, transport, logging,
      access. Evidence: the security verdict.
- [ ] **AC12 — the NSwag regen is FLAGGED, not run.** `manual_steps: nswag-regen`, owner-only
      (`CLAUDE.md`). T-0520/T-0521 are held until the owner confirms. Evidence: the flag plus the
      confirmation.
- [ ] **AC13 — a test that goes red against the pre-change code (Gate 0.5 leg 1).** The 21-character
      nonsense string that passes today must fail after. The verifier re-runs it **un-cached**.
- [ ] **AC14 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **The schema** — **T-0518**.
- **Any UI** — T-0520 (web + admin) / T-0521 (partner mobile).
- **Rendering the details on the invoice** — **T-0522**.
- **Running the NSwag regen.** AC12. Owner-only.
- **Card numbers.** T-0517 AC10 closed this: a PAN is never a column here.
- **Payment initiation.** Validating an account number is not moving money.

## Implementation notes

**Gate 0.5 applies (AC13)** — this fixes a defect (validation that does not validate) in a class of
data the money path depends on.

**Read first:** the T-0517 ADR, `ValidationExtensions.cs:100-140`, `UpdateBankDetails.cs` in full,
`UpdateEmployee.cs:120-135`, `AdminUpdateEmployee.cs:55-100`, `UpdateIdentificationInfo.cs:60-100`
(the archetype), `ITaxIdValidator.cs`, `GdprExportDto.cs`, `EmployeeListItem.cs`, and
`agents/knowledge/security-rules.md`.

**Lane note:** `ValidationExtensions.cs` and the three employee update commands are shared with the
partner-onboarding chain (T-0505…T-0510). **Check `process/shared-file-lanes.md` and serialize
before dispatch.**

## Status log
- 2026-08-04 — **implemented (backend).** Validator + capture + read contract + reveal command + the
  completeness-gate flip + the id-keyed erasure. `manual_step: nswag-regen` is **required before T-0520 /
  T-0521 start** (breaking DTO change, see Review). No migration: T-0518's schema was already in place.
- 2026-08-02 — **draft (created by pm from the owner's 2026-08-02 bank-details answer).** **The
  finding that sizes this ticket, PM-verified:** the entire server-side validation of a cleaner's bank
  account is `NotEmpty() + Length(15,34)` at `ValidationExtensions.cs:122-130`, shared by all three
  write paths. Once T-0522 puts these digits on a payout document a human keys a transfer from, that is
  not a cosmetic gap. AC8/AC9/AC10 exist because the field already has three couplings (logs, GDPR
  export, an admin **list** DTO) that a naive "add columns" change would break or widen.

## Review

### What landed (2026-08-04, backend)

**Validation (ADR-0034 D4/D4.1/D5.2)** — `Core.Domain/Payouts/`: `CzskAccountNumber` (mod-11, weights
applied **left-to-right** over the zero-padded ten-digit field), `IbanCalculator` (ISO 7064 mod-97 +
the ISO 13616 registry-length table + the CZ/SK composition), `CardNumberHeuristic` (Luhn, D9a),
`SwiftBic` (ISO 9362). `Core.Domain/Internationalization/CountryIsoCode` reconciles the catalog's
alpha-3 seed values with the alpha-2 an IBAN prefix and a BIC use. `IPayoutDetailsValidator` mirrors
`ITaxIdValidator`'s seam and **inverts its failure mode**: unknown country + non-IBAN value ⇒ reject.
The result carries the canonical stored form, so validation and canonicalization are one call and no
two callers can derive different IBANs.

**The direction is mutation-proved.** Flipping the loop to right-to-left was run against the suite:
24 tests go red, including `Reversing_The_Weight_Direction_Rejects_The_Reference_Specimen`, which
re-implements the rejected reading and asserts the owner's specimen fails under it. The owner's
`5885638003/5500` composes to `CZ3155000000005885638003` — the value the four payout-invoice fixtures
already pin; `PayoutInvoiceLayoutTests` / `PayoutInvoicePdfDataTests` stay green.

**Write path** — `UpdateBankDetails` gains the full field set (AC1) and is now the **single** writer of
`EmployeePayoutDetails` (create-or-update keyed on the employee) and the only place
`Employee.HasPayoutDetails` is raised. `UpdateEmployee` / `AdminUpdateEmployee` **stop carrying a bare
`Iban`** rather than validating a second, divergent copy (ADR Consequences); `Employee.UpdateEmployeeDetails`
loses its `iban` parameter. The derived IBAN is mirrored onto the legacy `Employee.IBAN` column so the
payout invoice's supplier block keeps printing the current account until **T-0522** moves that read onto
the payout row.

**The gate flip (the deploy-day item).** `IsProfileComplete()` now reads
`HasPayoutDetails || !string.IsNullOrEmpty(IBAN)` — a scalar pair on the row, no navigation. The legacy
term is load-bearing and **not** cosmetic: there is no backfill, so reading the flag alone would mark
every existing cleaner incomplete and 403 them off the whole partner surface.
`Cleansia.HostTests/Tests/PayoutGateDeployDayTests` seeds exactly that row shape and takes an order
through the host, loaded by `EmployeeRepository.GetByUserEmailAsync`'s real include list.

**Erasure** — `GdprDeletionService` calls `IEmployeePayoutDetailsRepository.RemoveForEmployeeAsync`
(id-keyed, tracked so it rides the caller's unit of work rather than committing on its own connection).
`Cleansia.IntegrationTests/Features/Gdpr/PayoutDetailsErasureTests` proves it against real Postgres
through the deletion service's own query shape.

**Read contract** — three routes, three DTOs: self (full), admin (masked, no unmasked member exists on
the DTO), admin reveal (**`Command`**, audited by the existing engine, `[EnableRateLimiting("auth")]`,
and `AdminEmployeeController` is now in `RateLimitCoverageGuardTests.MoneyAndSideEffectControllers`).
`Iban` is gone from `EmployeeItem` and `AdminEmployeeDetail`. `PayoutDtoSurfaceTests` freezes the DTO
family and forbids `.Include(… PayoutDetails)` anywhere in `Core.AppServices`.

**AC8** — the payout write, both payout reads and the GDPR export are added to `IsSensitivePath` on all
five hosts and pinned by `RequestLogPayoutPathSuppressionTests`. Field-name redaction cannot cover this:
none of `accountNumber` / `iban` / `swift` / `holderName` is a redaction token, and a holder name is free
text. The only payout log line is a duplicate-destination warning carrying employee ids and no value.

### AC7 — new keys, and the per-client i18n namespaces the frontend/L10n owner must add

`BusinessErrorMessage`: `validation.payout.country_not_supported`, `.scheme_not_supported`,
`.account_number_required`, `.invalid_account_number`, `.invalid_account_prefix`, `.invalid_bank_code`,
`.invalid_iban`, `.iban_country_mismatch`, `.iban_mismatch`, `.invalid_swift`, `.swift_required`,
`.looks_like_card`, plus `payout.not_found`. Each needs its five locales **per client namespace** (the
prefix differs per client — see the project memory note on error-key namespaces), on partner web,
partner mobile (Android + iOS) and admin.

### Manual steps

- `manual_step: nswag-regen` — **breaking**, blocks T-0520/T-0521: `EmployeeItem` and
  `AdminEmployeeDetail` lose `Iban`; `UpdateEmployee.Command` and `AdminUpdateEmployee.Command` lose
  `Iban`; `UpdateBankDetails.Command` gains eight optional fields; `GdprExportDto` gains
  `payoutDetails`; three new endpoints (`Employee/GetMyPayoutDetails`,
  `AdminEmployee/{id}/payout-details`, `AdminEmployee/{id}/payout-details/reveal`).
- **No** `ef-migration` — T-0518's schema was already in the tree and untouched.

### Verification

`Cleansia.Tests` **2780 passed / 2 failed / 2782** — both failures are `CleanupStalePendingOrdersHandler`
NREs from a concurrently-edited Functions file, not payout. `Cleansia.IntegrationTests` **123 passed / 0
failed**. `Cleansia.HostTests` **87 passed / 0 failed** (+1 new = 88 with the deploy-day test).

### Notes for the architect

- **`CountryConfiguration.PayoutScheme` has no writer.** Seeding it is a SQL/seed-data job today and
  there is no admin surface; tests set it by reflection rather than adding an uncalled domain mutator.
  Whoever owns country configuration should decide whether the admin gets a setter.
- **`GetEmployeePayoutDetails` needs `IOrderAccessService`** for the caller-employee-id arm, which is an
  order-shaped seam doing identity work. It is the only in-repo way to resolve the caller's employee id
  (`DownloadInvoice` uses the same). Worth a rename or a dedicated seam.
