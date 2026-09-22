---
id: T-0791
title: Remove the availability module end to end — domain, commands, two routes, the column, the admin section, the shared component, the partner remnants, both mobile apps; the four dead schema things folded in (Q-UI-01, Q-UI-02); the batch's one `Initial` regen
status: done
size: L
owner: —
created: 2026-09-20
updated: 2026-09-22
depends_on: []
blocks: [T-0792, T-0793, T-0788, T-0799]
stories: []
adrs: [ADR-0037]
layers: [backend, db, frontend, android, ios]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20, verbatim: *"remove redundant and deprecated things that are no longer used
(like availability module, the same for backend)"*. Ground truth at filing (`069b72ae`): the
cleaner's weekly availability schedule is still written and stored but **read by nothing that
decides anything** — `Employee.Availability` has five non-test readers, all projections or write-back
merges (`EmployeeMappers.cs:65-71,118-124`, `UpdateEmployee.cs:333`, `AdminUpdateEmployee.cs:151`,
`EmployeeEntityConfiguration.cs:81-84`); no query, specification, notifier, digest or gate reads it
(`TakeOrder.cs:195-199` says so in its own comment; `EmployeeMappers.cs:14-20` hard-codes
`HasSetAvailability: true`; `docs/partner-app/onboarding.md:87-91` already documents *"not read by
matching or dispatch"*). T-0610 (#227) removed the partner web editor; this ticket removes what it
left: the domain type, two commands, two routes, the column, the admin detail section, the 1 018-line
shared component and its 555-line stylesheet, the partner-web and mobile remnants.

**Two rulings of 2026-09-20 widen it** (the owner's *go* on the Q-UI list): **Q-UI-01** — the two
wire members `EmployeeItem.Availability` and `RegistrationCompletionStatus.HasSetAvailability` are
dropped now, not staged for a release train (production does not exist; there is no installed base);
**Q-UI-02** — the four schema-level dead things are **folded into this ticket's regen** rather than
parked: `Cart` (+ `CartServiceItems`, `CartPackageItems`), `EmailTranslations` (table + aggregate +
seed), `Employee.PreferredCurrencyCode`, `MembershipPlan.TrialPeriodDays`. One migration, one DEV drop.

`OrderAvailability` (ADR-0037 offerability) and `GetMyServingCleaners.ResolveSlotAvailability`
(ADR-0039, booked-overlap) are *order* concepts and **stay**. Runs **first and alone** — it touches
every tree and regenerates the schema and every client; T-0792 and T-0793 start from its commit.

Paths starting with `src/`, `docs/`, `agents/` are repo-relative; every other path is relative to
`src/Cleansia.App/`. The line numbers are the discovery's (DC §1.2, verified at `069b72ae`) — the
tree wins; re-grep before each deletion.

## Doing

**A. The availability module (DC §1.2)**

- **Domain:** `src/Cleansia.Core.Domain/Users/TimeRange.cs` (whole); `Employee.cs:210-211`
  (`_availability`, `Availability`), `:238,:253` (the `availability` parameter of
  `UpdateEmployeeDetails` + its assignment), `:302-306` (`UpdateAvailability`);
  `src/Cleansia.Core.Domain/Enums/DayOfWeek.cs` (whole — availability-only;
  `RecurringBookingTemplate.cs:27` uses `System.DayOfWeek`; it is `[SwaggerEnumAsInt]` and reflected
  into the code overview by `GetCodeOverview.cs:15`, so the `dayOfWeek` code list disappears from
  both web code stores).
- **AppServices:** `Features/Employees/AdminUpdateEmployeeAvailability.cs` (whole; audit label
  `employee.availability.update`), `Features/Employees/UpdateAvailability.cs` (whole);
  `UpdateEmployee.cs:17,136-165,223,225,252,313-357,360`; `AdminUpdateEmployee.cs:151-156`;
  `DTOs/EmployeeItem.cs:31` (`Availability` — Q-UI-01); `DTOs/EmployeeListItem.cs:56,77`;
  `Mappers/EmployeeMappers.cs:3,14-20,65-71,118-124`; `DTOs/RegistrationCompletionStatus.cs:8`
  (`HasSetAvailability` — Q-UI-01); `Common/BusinessErrorMessage.cs:515`
  (`InvalidAvailabilityFormat`); reword `AdminSetEmployeeWeeklyOrderLimit.cs:18-19`.
- **Hosts:** `src/Cleansia.Web.Admin/Controllers/AdminEmployeeController.cs:98-110`
  (`PUT {employeeId}/update-availability`);
  `src/Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs:119-128`
  (`PUT api/Employee/UpdateAvailability`).
- **Database:** `EmployeeEntityConfiguration.cs:81-84` (the converter; `JsonValueConverter` stays —
  eight other users); the `Availability` NOT NULL `text` column
  (`Migrations/20260919231739_Initial.cs:1333`, snapshot `:5885`) → **`Initial` regenerated** per
  CLAUDE.md; no seed touches the column (`sql-scripts/insert_seed_data.sql` inserts no `Employees`).
- **Backend tests:** `src/Cleansia.Tests/Features/Employees/UpdateEmployeeAvailabilityGuardTests.cs`
  (whole); `EmployeeSelfUpdateIdInertTests.cs:77-80`; `EmployeeUserAuditCoverageTests.cs:49,206-230`
  (the frozen label leaves with the command; nothing on the web reads `employee.availability`);
  `src/Cleansia.HostTests/Tests/EmployeeSelfUpdateSessionIdentityTests.cs:76,193,226` (+
  `Route.Availability` and its `BodyFor` arm); the five `UpdateEmployeeDetails(... availability ...)`
  callers: `HostTests/Infrastructure/DomainSeed.cs:152`,
  `Tests/Domain/Payouts/EmployeePayoutProfileGateTests.cs:31`,
  `Tests/Features/Auditing/EmployeeUserAuditCoverageTests.cs:335`,
  `Tests/Features/Employees/ApproveEmployeeDocumentGateTests.cs:246`,
  `Tests/Features/Employees/ApproveEmployeePayCoverageTests.cs:86`.
- **Admin web:** `employee-detail.component.html:583-641` (the section);
  `employee-detail.component.ts:11,13,16,56,148-149,240-243,287-291,296-310`;
  `employee-detail.facade.ts:6,15,46-47,274-316`;
  `employee-detail-commands.facade.spec.ts:5,22,32,47,83,99-125,161-175`;
  `pages/cleansia-admin/employee-detail.component.scss:140-180`; admin `i18n/{en,cs,sk,uk,ru}.json`:
  `pages.employee_detail.availability`, `.Monday`…`.Sunday`, `.no_availability`,
  `pages.employee_detail.messages.availability_save_*`, the 30 `components.availability.*`,
  `api.validation.invalid_availability_format`;
  `apps/cleansia-admin.app/src/app/i18n/error-contract-parity.spec.ts:480` (the row must go or the
  spec fails *"reports contract keys the backend no longer emits"*, `:869-874`);
  `libs/data-access/admin-stores/src/lib/code/code-types.ts:6`, `code.selectors.ts:45`.
- **Shared web:** `libs/shared/components/src/lib/cleansia-availability/` (3 files, 1 018 lines) +
  `lib/index.ts:2`; `styles/components/cleansia-availability.component.scss` (555) +
  `styles/components/index.scss:5` + `styles/README.md:17,146`.
- **Partner web:** `profile/src/lib/profile/profile.models.ts:16,41-51,202-205`;
  `libs/data-access/partner-stores/.../code-types.ts:6`, `code.selectors.ts:45`;
  `libs/core/partner-services/src/lib/services/registration-completion.service.ts:10,37,53,67,70,75,102`
  (`hasSetAvailability`); partner `i18n/*.json`: `components.availability.*` (30),
  `registration_lock.categories.availability`, `availability_required`,
  `api.validation.invalid_availability_format`;
  `apps/cleansia-partner.app/src/app/i18n/error-contract-parity.spec.ts:528`;
  `apps/cleansia-partner.app-e2e/src/login-jobs.smoke.spec.ts:71`.
- **Generated clients (regen, never hand-edit):** `admin-client.ts:4717,5169-5215,19414-19540`;
  `partner-client.ts:8885-8995,13891-13946,15571-15620`;
  `src/cleansia_android/openapi/partner-mobile-api.json:2582-2625,11682-11720` (+ the three DTO
  members) — one spec feeds Android (`partner-app/build.gradle.kts:238-244,291`) and iOS
  (`src/cleansia_ios/README.md:100-105`).
- **Android:** `partner-app/.../data/profile/ProfileRepository.kt:16-17,145-149,344-356`
  (`updateAvailability`, no ViewModel caller); the 42 orphan strings per locale
  (`values/strings.xml:417,429-445,523-530,664,787-800,1055,1235`; `values-cs`, `-sk`, `-uk`, `-ru`
  at the lines in DC §1.2 — all zero `R.string.` references); `RegistrationLockViewModel.kt:26,109-110`
  comments; `EmployeeProfileWireTest.kt:148,240,312` (`hasSetAvailability`).
- **iOS:** `CleansiaCore/.../Localizable.xcstrings:6333-6345`; `PartnerErrorVoiceTests.swift:95,153`
  (emitter map); `CleansiaPartner/Tests/RegistrationCompletionTests.swift:14,59`.

**B. The four dead schema things, folded in (Q-UI-02 → fold; DC §2.7)**

1. **`Cart` + `CartServiceItems` + `CartPackageItems`** — written once per registration
   (`Register.cs:125`, `RegisterEmployee.cs:94`, `GoogleAuth.cs:200`, `AppleAuth.cs:229`), erased by
   GDPR (`GdprDeletionService.cs:141,435-436`), **read by no feature** (the wizard builds orders
   directly). The aggregate (`src/Cleansia.Core.Domain/Users/Cart.cs` and its items), repository,
   entity configurations, the four writers' lines, the two erasure lines and the erasure/books roster
   entries go; `docs/domain/model.md:102-104,162-164` is T-0799's.
2. **`EmailTranslations`** — table + aggregate (`src/Cleansia.Core.Domain/Emails/EmailTranslation.cs`)
   + seed (`sql-scripts/insert_seed_data.sql:458`, `sql-scripts/seed/insert_email_translations.sql:57`);
   the renderer reads `EmailTemplateTranslations`; the sole touch is the language delete-guard
   `LanguageRepository.cs:32`, which loses that term.
3. **`Employee.PreferredCurrencyCode`** (`Employee.cs:208`) — the only writer is the never-called
   `UpdatePreferredCurrency` (`:314-317`; listed in DC §2.6 — it leaves **here**, with its column, not
   in T-0792); the only reader is the GDPR export (`GdprExportService.cs:48`, member off the export
   DTO); the comments at `PayPeriodBackgroundService.cs:398`, `GenerateInvoice.cs:95` and
   `EmployeeInvoice.cs:141` that name it are reworded to what is true without it.
4. **`MembershipPlan.TrialPeriodDays`** (`MembershipPlan.cs:49,124,136`) — T-0690 made it unsettable;
   the column, the constructor/update parameters, the validator rule that refuses it, and the DTO
   member on the four admin DTOs and the customer plan DTOs go. The admin **UI** remnants (column,
   form field, keys) are T-0793's, against the regenerated client.

**C. Regen, re-dump, migration — in this order (DC §1.3)**

Domain → AppServices → hosts → DB config → **`Initial` regenerated once** (`dotnet ef migrations
remove --force` + `add Initial`, startup project `Cleansia.Web.Partner`) → the three backend test
projects → `npm run generate-admin-client` + `generate-partner-client` + **`generate-customer-client`**
(the fold: `trialPeriodDays` on the customer plan DTOs) → mobile spec re-dump: `partner-mobile-api.json`
(the route, three schemas, three members) **and `customer-mobile-api.json`** (the customer plan DTO)
→ Android regen + `:partner-app` and `:customer-app` build → iOS: the two Swift test lines, the
xcstrings rows, the voice map, the regenerated customer models (**one Mac session** if a local build
is wanted — iOS CI compiles and runs XCTest for all three targets, so the Mac is optional) → web
(admin, shared, partner; both parity specs) → the e2e stub.

**The DEV drop is owed at the deploy, not run on the branch** (MS-2 shape — memory: the drop is timed
with the deploy). The lane's report names the new `Initial` id; T-0799 moves `agents/cleanup/MANUAL_STEPS.md`
MS-2 and `agents/OWNER-PLATE.md` A1 to it.

## NOT

- Anything under `Features/Orders/*` matching "availability" — that is offerability (ADR-0037) and
  the preferred-cleaner slot check (ADR-0039). `JsonValueConverter` (eight other users).
- The partner web lock screen (already three categories, `registration-lock.component.ts:79-127`).
- Mobile locale sweeps beyond the availability strings (the other 36 dead `error_*` / `error.*`
  strings wait for a locale sweep — plan NOT list).
- Wire enum members with no writer (`CancelledBy.Cleaner`, `PayoutDetailsStatus.NeedsReconfirmation`,
  `RefundStatus.Failed` — Q-UI-05, keep; `OrderStatus.Pending` — owner-ruled keep).
- The admin free-trial **UI** (T-0793); the docs pages (T-0799 — the lane leaves `→` pointers only).
- A second `Initial` regen for anything: everything schema-level in this batch is in this ticket.

## Done looks like

`rg -i "availability" src/Cleansia.Core.* src/Cleansia.Web.* src/Cleansia.Infra.Database
src/Cleansia.App/libs src/Cleansia.App/apps/cleansia-admin.app src/Cleansia.App/apps/cleansia-partner.app`
returns only `OrderAvailability` and the `GetMyServingCleaners` slot-availability path;
`employee-detail.component.html` has no `cleansia-availability`; the regenerated `Initial` has no
`Employees.Availability`, no `Employees.PreferredCurrencyCode`, no `MembershipPlans.TrialPeriodDays`,
no `Carts` / `CartServiceItems` / `CartPackageItems`, no `EmailTranslations`; the integration suite
builds Postgres from it green; both error-contract parity specs green; Android `:partner-app` and
`:customer-app` compile; the iOS targets compile in CI; the three web clients and both mobile specs
regenerated and committed in the same change as the DTOs.

## Acceptance criteria

- [ ] **AC1** — Given the tree after this ticket, When `rg -i availability` runs over the backend,
      the shared libs and the admin/partner apps, Then the only hits are `OrderAvailability`
      (offerability) and `ResolveSlotAvailability` (the serving-cleaner slot check).
- [ ] **AC2** — Given the regenerated `Initial`, When the integration suite builds a real Postgres
      from it, Then it is green and `Employees` has neither an `Availability` nor a
      `PreferredCurrencyCode` column, `MembershipPlans` has no `TrialPeriodDays`, and the `Carts`,
      `CartServiceItems`, `CartPackageItems` and `EmailTranslations` tables do not exist.
- [ ] **AC3** — Given the admin and partner hosts, When `PUT api/AdminEmployee/{id}/update-availability`
      or `PUT api/Employee/UpdateAvailability` is called, Then both answer 404 (route gone; the
      HostTests route classes no longer name them; `RateLimitCoverageGuardTests` green).
- [ ] **AC4** — Given a customer registers (email, Google or Apple) or a cleaner registers, When the
      command commits, Then no `Carts` row is written and the GDPR erasure walk neither reads nor
      deletes one (the erasure and books rosters updated; `GdprDeletionService` compiles without the
      two lines).
- [ ] **AC5** — Given the partner web's `registration-completion.service.ts`, When the
      `RegistrationCompletionStatus` DTO arrives without `hasSetAvailability`, Then the lock screen
      computes its three categories from the remaining members and the spec is green.
- [ ] **AC6** — Given the admin employee detail at 1440, When it is re-captured, Then the section
      between *Pracovní informace* and the documents is gone and the page renders with 0 console
      errors.
- [ ] **AC7** — Given the regenerated `partner-mobile-api.json` and `customer-mobile-api.json`, When
      the Android generated models are rebuilt, Then `:partner-app` and `:customer-app` compile and
      `EmployeeProfileWireTest` passes without `hasSetAvailability`; the iOS partner and customer
      targets compile in CI with `RegistrationCompletionTests` and `PartnerErrorVoiceTests` updated.
- [ ] **AC8** — Given both error-contract parity specs, When they run, Then neither reports
      `validation.invalid_availability_format` as a contract key the backend no longer emits, and
      every remaining key still has all five locales.
- [ ] **AC9** — Given the lane's report, When it is read, Then it names the new `Initial` id, every
      client regenerated, both spec re-dumps and the Android regen — and states that the DEV drop is
      owed at the deploy, not run.

## Implementation notes

The delete order matters because the parity specs and the code stores read the generated clients:
regenerate before touching the web. `DayOfWeek` is `[SwaggerEnumAsInt]`, so the `dayOfWeek` code
list leaves both web code stores on regen — the two `code-types.ts:6` / `code.selectors.ts:45`
edits follow the regen, not precede it. `AdminSetEmployeeWeeklyOrderLimit.cs:18-19` mentions the
schedule in prose — reword, do not delete the command (the weekly limit is live).

**Security (Gate 3) — `security_touching: true`.** Two authorised routes leave the permission map
(`CanAdminUpdateEmployee` keeps its other routes; no `Policy` constant is removed here — T-0792 owns
the constants); the GDPR erasure walk loses a table (the roster tests must be updated, never
loosened); the customer wire loses `trialPeriodDays` (a deliberate contract shrink, like T-0754's).

Evidence: the discovery's `dead-code.md` §1 (DC) — in the orchestrator's session scratchpad, not
tracked; the lane copies the counts it relied on into the status log when it ships.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Opened the same night as phase 1's first lane (runs alone). Q-UI-01 (drop the wire members) and
  Q-UI-02 (fold the four schema things) ruled by the owner's *go* of 2026-09-20 and carried here.
- 2026-09-22 — **done**; shipped 2026-09-20 as three commits on chore/ui-polish-and-dead-code
  (PR #260): backend `38312c8d9` (the availability module and the dead `Cart`, `EmailTranslations`
  and `PreferredCurrencyCode` schema; `TrialPeriodDays` with it), web `5ef4c292f` (admin, partner and
  shared, plus the free-trial fields the admin plan form still offered), mobile `c76523efa` (the
  Android and iOS partner remnants). **`Initial` regenerated as `20260920204705`** — 84 tables, 48
  carrying the `Tenants` FK; the three web clients and both mobile specs regenerated in the same
  commits; the Postgres integration suite ran in full locally (Docker up) and green. **The DEV drop is
  owed at the next DEV deploy** (`agents/cleanup/MANUAL_STEPS.md` MS-2, `agents/OWNER-PLATE.md` A1),
  never run on the branch. Q-UI-01 and Q-UI-02 are deleted from `questions/open.md` — this ticket is
  the record. The docs pages the Doing list named for T-0799 are the docs lane's commit of 2026-09-22.
