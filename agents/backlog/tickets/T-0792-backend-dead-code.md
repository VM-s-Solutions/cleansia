---
id: T-0792
title: Backend dead code — one never-dispatched command, eight unattributed Policy constants, 36 unemitted error keys with their locale twins, 25 repository methods, 25 domain members, three orphan files, the partner-host routes no client calls, three admin routes with no caller; two guards so it stays gone
status: done
size: M
owner: —
created: 2026-09-20
updated: 2026-09-22
depends_on: [T-0791]
blocks: [T-0799]
stories: []
adrs: [ADR-0066, ADR-0001]
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"remove redundant and deprecated things that are no longer used (…) the
same for backend"*. The discovery (DC §2) walked the backend for symbols with **zero readers today**
and gave each a verdict; only the DELETE verdicts are here. Every OWNER verdict (a wire enum, an
infra-facing route, an endpoint with a product question behind it) is a Q-UI question and is **not**
touched. Runs after T-0791 — the two share `AdminEmployeeController`, `EmployeeMappers` and the
locale files — serial, not parallel; T-0793 runs beside it on the web trees.

Paths starting with `src/` are repo-relative; `Features/…`, `Common/…`, `Repositories/…` are under
`src/Cleansia.Core.AppServices` / `src/Cleansia.Core.Domain` as DC §2 names them. Line numbers are
the discovery's (verified at `069b72ae`) — re-grep before each deletion; a removal is only right
when `rg` shows zero readers now.

## Doing — DELETE verdicts only (DC §2)

- `Features/EmployeeDocuments/DeleteDocument.cs:10-67` — never dispatched by any host in tracked
  history (`git log -S` finds only the unrelated `DeleteDocumentRequirement`).
- **Eight `Policy` constants with no `[Permission]` on any of the five hosts**, moved as **one hunk**
  across `Policy.cs:17,33,66,205-208,215`, `PolicyBuilder.cs:21,35,69,216-219,226`,
  `FrozenPermissionMapTests.cs:29,43,65,207-210,217`, admin `policy.ts:19,33,58,193-196,203` +
  `POLICY_MAP :293,307,332,467-470,477` (the mirror spec
  `apps/cleansia-admin.app/src/app/auth/policy-map-mirror.spec.ts:34` diffs the two). The seven
  anonymous ones stay (`PolicyBuilder.AnonymousAllowList`, `PolicyBuilder.cs:311-320`).
- **36 `BusinessErrorMessage` constants** with zero non-test references by name or value
  (`BusinessErrorMessage.cs:87,112,119,148,149,205,206,213,215,232,249,298,301,320,352,445-449,510-512,525,526,562,563,566,567,569,572,588,589,669-671`)
  + the two vacuous `Assert.DoesNotContain` lines (`CompleteOrderValidatorTests.cs:53`,
  `TakeOrderValidatorTests.cs:55`) + their `api.*` twins in the 15 web locale files (5 locales ×
  admin, partner, customer) and the three partner keys with **no constant at all**
  (`api.order.not_cash_payment`, `api.order.selected_package.selected_packages`,
  `api.validation.file.max_size` / `invalid_type`). Mobile catalogue copies: **not** touched.
- **25 repository methods** with no caller outside `Repositories/` and tests (DC §2.5 list:
  `src/Cleansia.Core.Domain/Repositories/*.cs` + their `src/Cleansia.Infra.Database/Repositories/*.cs`
  bodies + the mocks at `GetDashboardStatsHandlerTests.cs:254`, `UserStripeCustomerTests.cs:86-87`,
  `SavedAddressRepositorySoftDeleteTests.cs:107`); `IRepository.GetPaged` (both) and `DeactivateRange`
  on `BaseRepository.cs:48-54,139`. `IUserSessionProvider.GetUserClaims` stays (internal caller).
- **25 domain mutators/helpers** with a whole-tree count of 1 (DC §2.6 minus
  `Employee.UpdatePreferredCurrency`, which leaves with its column in T-0791): `CompanyInfo.UpdateTradingInfo :124`,
  six `CountryConfiguration.Update* :167-220`, `PropertySizePreset.UpdateSize/Reorder :84,96`,
  `Device.UpdateLastActive :67`, `EmployeeDocument.UpdateMetadata/IsLatestVersion :103,136`, four
  `EmployeeInvoice.* :284-396`, `OrderEmployeePay.SetPayBreakdown :227`, six
  `CountryInvoiceConfig.Update* :85-125`, `PromoCode.IsRedeemableAt :202`, `ReferralCode.Disable :72`,
  `Package.RemoveService :96`, `SavedAddress.UpdateUnit :55`, `PayPeriod.IsWithinPeriod/OverlapsWith :156,166`.
- `Shared/DTOs/Files/FileResponse.cs`, `Specifications/OrderEmployeePaySpecification.cs`,
  `EmployeePayroll/Services/PayPeriodService.cs` (whole files, zero references).
- **Partner web host (`:5000`) routes no web client calls and the mobile host does not have** (DC §2.8):
  `DisputeController.cs` (empty), `PayConfigController.cs` (5 routes + `PolicyBuilder.cs:128-132` +
  `RateLimitCoverageGuardTests.cs:66`), `CurrencyController`, `PackageController`, `ServiceController`,
  `PayPeriodController.GetPayPeriodById`, `EmployeePayrollController.cs:55-65` (`CalculateOrderPay`,
  `PolicyBuilder.cs:102`) and `:68-78` (`RegenerateInvoicePdf`, `:103`). `LanguageController` stays
  (mobile twin parity).
- **Admin host routes with no admin-web caller and no product question** (DC §2.9):
  `AdminEmailTemplateController.cs:63` `get-paged`, `AdminUserController.cs:137` `GET {userId}`,
  `AdminCompanyController.cs:95-105` `get-current` (*"legacy endpoint for backward compatibility"*, no
  caller — the facades use `getOverview`, `company-info.facade.ts:46`).
- The generated-client methods in DC §3.12 disappear on regen.
- **Two guards, in the style of `FrozenPermissionMapTests`:** `PolicyAttributionTests` — every
  `Policy` constant not in `AnonymousAllowList` is carried by at least one `[Permission(...)]` across
  the five host assemblies (DC §6.5 names the gap: `policy-map-mirror.spec.ts` mirrors, it does not
  check use); `BusinessErrorMessageEmittedTests` — every constant is referenced by name in
  `src/Cleansia.Core.AppServices` or `src/Cleansia.Infra.*` outside its own file.

## NOT

- `CancelledBy.Cleaner` (wire enum — plan NOT list), `PayoutDetailsStatus.NeedsReconfirmation`,
  `RefundStatus.Failed` (Q-UI-05: keep).
- `EmailTranslation`, `Cart`, `Employee.PreferredCurrencyCode`, `MembershipPlan.TrialPeriodDays` —
  **T-0791's** since Q-UI-02 was ruled *fold*.
- Partner `PaymentController.webhook`, `HealthController` (Q-UI-03: keep).
- Admin `AdminEmployeeDocumentController` `{documentId}/versions`, `AdminPayPeriodController`
  `create/update/delete/open`, `AdminPayrollController` `generate-invoice` (Q-UI-04: keep).
- `CountryController.GetOverview` (live — its comment is wrong, not the route; reworded in T-0799).
- Every §2.10 legacy-transition comment (`Employee.IBAN` *"until T-0522"* and kin — all live); the
  seven anonymous policies; `IUserSessionProvider.GetUserClaims`.
- Mobile locale catalogues (the 36 dead `error_*` strings wait for a locale sweep).

## Done looks like

`rg "DeleteDocument\b" src` → 0 outside `DeleteDocumentRequirement`; `FrozenPermissionMapTests` and
`policy-map-mirror.spec.ts` green with eight fewer rows; `rg -c "BusinessErrorMessage\.\w+"
src/Cleansia.Core.AppServices` unchanged for every remaining constant; both partner and admin NSwag
clients regenerated and committed in the same change; `RateLimitCoverageGuardTests` and
`PartnerHostsGateTheSameControllersTests` green; the two new guards green and each proven red once
by re-adding one deleted item locally.

## Acceptance criteria

- [ ] **AC1** — Given the five host assemblies, When `PolicyAttributionTests` runs, Then every
      `Policy` constant outside `AnonymousAllowList` is found on at least one `[Permission]`, and
      re-adding one of the eight deleted constants makes it red.
- [ ] **AC2** — Given `BusinessErrorMessage`, When `BusinessErrorMessageEmittedTests` runs, Then every
      constant is referenced by name outside its own file, and re-adding one of the 36 makes it red.
- [ ] **AC3** — Given the admin `POLICY_MAP` and the C# map, When `policy-map-mirror.spec.ts` runs,
      Then both have the same rows, eight fewer than at `069b72ae`.
- [ ] **AC4** — Given the partner web host, When the routes listed under DC §2.8 are requested, Then
      each answers 404; `PartnerHostsGateTheSameControllersTests` and `RateLimitCoverageGuardTests`
      are green with their rosters shrunk, not loosened.
- [ ] **AC5** — Given the 15 web locale files, When the 36 constants' `api.*` twins and the three
      constant-less partner keys are gone, Then all three error-contract parity specs are green and
      no remaining `api.*` key lacks a locale.
- [ ] **AC6** — Given the regenerated admin and partner clients, When both apps build, Then no facade
      references a removed client method (`nx affected -t lint,test,build` green).
- [ ] **AC7** — Given the three backend test projects, When they run, Then all green with the
      vacuous `Assert.DoesNotContain` lines removed and the mocks of deleted repository methods gone.

## Implementation notes

The policy hunk is one commit across four files so `policy-map-mirror.spec.ts` is never red between
commits. Keep the seven anonymous policies. `LanguageController` on the partner host stays because
the mobile host has its twin and `PartnerHostsGateTheSameControllersTests` compares them. **Regen:**
admin + partner NSwag clients (routes removed on both web hosts). **No** mobile spec re-dump
(neither mobile host changes). **No** migration.

**Security (Gate 3) — `security_touching: true`.** The permission map shrinks by eight constants;
the anonymous allow-list is untouched; the new attribution guard is the ratchet that keeps a future
unattributed constant from appearing (ADR-0066's map and ADR-0001's permission table lose the eight
rows in T-0799).

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Opened the same night as phase 1's second lane, after T-0791's commit, beside T-0793.
- 2026-09-22 — **done**; shipped 2026-09-21 as `f307e835f` (the dead commands, policies, error keys,
  repository and domain members and host routes, with the admin and partner clients regenerated in
  the same commit) + `cf4b1f981` (the last readers of the removed error keys, the dead mapper
  parameter, the stale comments, the emitted-constants guard widened to every host) on
  chore/ui-polish-and-dead-code (PR #260). The five-locale twins of the removed keys for admin and
  partner rode here rather than in T-0793. The API-reference and permission-table edits the Doing
  list left to T-0799 are the docs lane's commit of 2026-09-22.
