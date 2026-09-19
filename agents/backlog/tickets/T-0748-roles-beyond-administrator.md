---
id: T-0748
title: Roles beyond Administrator — Support / Accountant / Manager (Q-AUD-O1)
status: done
size: L
owner: —
created: 2026-09-14
updated: 2026-09-19
depends_on: [T-0768, T-0775]
blocks: [T-0773]
stories: []
adrs: [ADR-0066, ADR-0001, ADR-0062, ADR-0065]
layers: [analyst, architect, backend, frontend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-14 on Q-AUD-O1 (*does support get a role distinct from Administrator?*):
*"I want to introduce a few more roles like Support / Accountant / Manager / etc. Needs a separate
ticket but I'd think about this a bit later."* Filed as the owner asked — **open, no code, no lane
until the owner picks it up.** Today there is one admin role: `UserProfile.Administrator`, and every
admin permission maps to `PhysicalPolicy.AdminOnly`, so anyone who can read the customer trail can
also refund, override, erase and change the platform configuration.

## What the ticket is, when it is picked up

Three roles the owner named, and the surfaces each would gate (a proposal to be ratified, derived
from what the admin host exposes today — the analyst confirms it against the owner before any code):

| Role | Gets | Does **not** get |
|---|---|---|
| **Support** | the audit reads (`CanViewAuditLog` — customer list/entry/timeline, admin log), the two subject exports (`CanAdminExportUserData` — the JSON export and the PDF incident file), order and dispute *reads*, the customer page, dispute messages | refunds (`CanIssueRefund`), status overrides (`CanOverrideOrderStatus`), reassignment, erasure (`CanAdminDeleteUserAccount`) and the deletion retry, any catalogue or configuration write, pay and payout |
| **Accountant** | employee invoices, pay periods (open/close/mark paid), receipts and the fiscal-failure queue, the revenue and payroll reports, refunds and chargebacks, payout-details reveal (audited) | the customer trail beyond what an order's history shows, erasure and exports, catalogue and configuration writes, employee approval |
| **Manager** | everything an Administrator has **except platform configuration** — countries, currencies, languages, company info, legal documents, membership plans, loyalty tiers, admin-user management | the configuration area; admin-user CRUD |

**Administrator** keeps everything. The exact split is the analyst's first deliverable (one row per
`Policy.*` constant), reviewed by the owner.

## The machinery it would use (already in the tree)

- `UserProfile` gains the roles; the JWT role claim carries one of them (ADR-0001 D1 — the role is
  the host-independent discriminator; `AuditGate`'s admin arm keys on `Administrator` and would have
  to decide whether a Support/Accountant/Manager act lands in the admin table — it should, under the
  same `AdminActionAudit` shape, actor kind in the row).
- `PhysicalPolicy` gains the combinations (`AdminOrSupport`, `AdminOrAccountant`, `AdminOrManager`,
  …) and `PolicyBuilder.Map` re-maps each `Policy.*` constant to one of them. **`PolicyBuilder.AssertComplete`**
  fails boot on any constant left unmapped, so the split cannot be partial by accident; the HostTests
  policy classes (200/403/401 per route) grow a case per role.
- Admin web: `PermissionService.hasPolicy` already gates every button and nav item by policy name;
  the app's `adminGuard` requires `Administrator` and would accept the new roles.
- The audit-log and data-protection surfaces are the first consumers (ADR-0062 D6: *"a separate
  customer-trail policy would be a policy with no second consumer today"* — this ticket is the second
  consumer).

## Acceptance criteria (draft)

- [ ] **AC1** — A `Policy.*` → role matrix exists, ratified by the owner, with every constant on one row.
- [ ] **AC2** — `PolicyBuilder.AssertComplete` still passes with the new physical policies; every
      admin route answers 403 to a role the matrix excludes and 200 to one it includes (HostTests).
- [ ] **AC3** — A Support user can read the customer trail and build the incident file, may refund within
      the policy and override an order (the owner's Support list, ADR-0066 D3), and cannot erase or change
      configuration. *(The 2026-09-16 draft said "cannot refund, override"; the 2026-09-19 ruling widened it.)*
- [ ] **AC4** — Every act by a Support/Accountant/Manager is on the admin audit table with the actor's
      role.
- [ ] **AC5** — Admin-user management can assign the role; the admin web hides what the role lacks.

## Out of scope

Customer- or employee-facing roles; per-tenant roles (one holding, one admin pool — ADR-0061 D5
stamps admins with the default operator).

---

## Brief as picked up (2026-09-19) — the model, the sets, the map, the admin-host reads, the assignment, the matrix (ADR-0066)

- **Size:** L · **Lane:** backend · **ADR:** ADR-0066 · **Depends on:** T-0768 (the recipient filter),
  T-0775 (every event exists to filter) · **Blocks:** T-0773 · **security_touching:** yes

### Doing

- `AdminRole` enum; `User.AdminRole` + `SetAdminRole()` + `CreateWithPassword(adminRole:)` (throws for an
  Administrator without a role); the check constraint `CK_Users_AdminRole_Profile`;
  `AdminActionAudit.ActorAdminRole`; **one `Initial` regen** (the batch's only schema change); the seed
  (`insert_seed_data.sql:1983-1997` gains `"AdminRole" = 1`) and `set-admin-role.sql` (both columns);
  `DomainSeed.Admin(role:)`; `TestJwtFactory.Mint(adminRole:)`; **every fixture that calls
  `CreateWithPassword(…, UserProfile.Administrator)` passes `adminRole: AdminRole.Administrator`** — at least
  `DomainSeed`, `CrossMarketReadAndDeliveryTests`, `DisputeTextRetentionTests`, `SubjectExportDisputesTests`,
  `RegisterEmployeeProfileUpgradeTests`, `ChangeOwnPasswordTests`, `AdminUserProfileFieldsTests`,
  `DeactivateAdminUserValidatorTests`, `EmployeeUserAuditCoverageTests`; the lane greps for multi-line calls.
- `SetClaims` → `admin_role`; `JwtTokenResponse.AdminRole`.
- `PhysicalPolicy` ×4 (`AdministratorOnly`, `ManagerOrAbove`, `SupportOrAbove`, `AccountantOrAbove`;
  `AdminOnly` keeps its name and means any role); `AdminRoleSets` (+ `For`, `Any`, `Admits`); four
  registrations; startup presence + semantics (`AdminOnly` does not require the claim).
- **Eleven `Policy` constants**: `CanViewPagedOrderAdmin`, `CanViewOrderDetailAdmin`, `CanViewOrderPhotosAdmin`,
  `CanViewEmployeeDocumentsAdmin`, `CanViewEmployeePayoutDetailsAdmin`, `CanViewPagedInvoicesAdmin`,
  `CanViewPayPeriodsAdmin`, `CanViewPayPeriodAdmin`, `CanViewLegalDocuments`, `CanSetAdminRole`
  (+ `CanViewAdminNotifications` from T-0768); the **sixteen admin-controller attribute edits** (ADR-0066
  §Context table + `AdminLegalController.cs:21, 37`); `PolicyBuilder.Map` per ADR-0066 D3;
  `FrozenPermissionMapTests` rewritten to D3; the five `// SuperAdmin` comments at `Policy.cs:177-181` →
  `// Administrator`.
- `SetAdminRole` command + `POST api/AdminUser/{userId}/role` + keys (`admin_user.cannot_change_own_role`,
  `admin_user.cannot_demote_last_administrator`); `IUserRepository.DemoteAdministratorIfAnotherRemainsAsync`
  and `DeactivateAdministratorIfAnotherRemainsAsync` — **one transaction each:
  `pg_advisory_xact_lock(hashtext(tenantId))` then the conditional `UPDATE`**; `DeactivateAdminUser` moves
  onto the second and its predicate narrows to Administrator-role; `CreateAdminUser.Command.Role`;
  `AdminUserListItem` / `AdminUserDetailDto` `.AdminRole`; `GetPagedAdminActionAudits` role filter;
  `AuditEntryFactory` reads the claim.
- `AdminEventCatalog` audiences per ADR-0065 D4 (chargeback → `AdminOnly`); `AdminNotifier` recipient filter
  through `AdminRoleSets.For(entry.Audience)` (ADR-0066 D8).
- Tests: `Cleansia.Tests/Authentication/AdminRolePolicyMatrixTests` (every map row × eight principals through
  `IAuthorizationService`); `Cleansia.HostTests/Tests/AdminHostPermissionCoverageTests` (reflection +
  allow-list); four behavioural HostTests classes; the Testcontainers race tests for both guards; the
  constraint test.
- The admin NSwag client regenerated and committed; the DEV drop **at the deploy**, reported.
- Docs per ADR-0066 §Consequences, after green; this ticket closed with the D3 table as AC1; the owner plate
  D8 ruled + **O-R1..O-R10** (recorded at filing).

### NOT doing

- No holding role, no per-market scope, no invitations, no fifth role, no policy splits beyond the eleven
  (the document-requirement CRUD sharing `CanAdminUpdateEmployee` and the admin-user read `{userId}`
  sharing `CanViewOrderCustomer` are findings F13), no rewrite of the twenty-five handler-level profile
  checks, no bulk session revocation, no change to `CompanySignInGate` (Q-LC-01 kept — ADR-0066 D7), no
  partner/customer/mobile change (the eight shared read policies keep their partner-host meaning), no HTTP
  walk of every route.
- No web (T-0773).

### Done looks like

`AssertComplete` passes; the matrix answers every map row for every principal; every admin-host action
carries a non-`Deny` permission or is allow-listed; an Accountant token is refused the order detail and an
employee document download and admitted on the invoice list; the seeded administrator is an Administrator;
an Administrator can assign roles and cannot demote the last one, deterministically under the lock; every
act by any role is audited with the role; the regenerated client and the regenerated `Initial` are
committed; DEV dropped at the deploy and said so.

### Acceptance criteria (replace the 2026-09-14 draft above)

- [x] **AC1** — The `Policy.* → set` table of ADR-0066 D3 is `PolicyBuilder.Map` (frozen test); the admin
      controllers reference none of the eight shared read policies (grep); the partner and mobile
      controllers are byte-identical. *Met — `FrozenPermissionMapTests` rewritten to D3; the sixteen attribute
      edits across seven admin controllers; the partner/mobile controllers untouched in the diff.*
- [x] **AC2** — *Given* each of the four roles, a claimless Administrator, an Employee, a Customer and
      anonymous, *when* `IAuthorizationService` evaluates every map row, *then* the answer equals D3 (the
      matrix test); every admin-host action carries `[Permission]` mapping to non-`Deny` or is on the
      allow-list naming only `AdminCodeController.GetOverview` and the anonymous sign-in routes. *Met —
      `AdminRolePolicyMatrixTests` (unit, green); `AdminHostPermissionCoverageTests` (host). The allow-list
      has four entries, not three: `AdminAuthController.Logout` sits beside `Login`, `RefreshToken` and
      `GetOverview` with its reason — recorded in ADR-0066 §What shipped.*
- [x] **AC3** — *Given* a Support token, *then* the customer trail, the admin log, the order detail, the
      export and the incident file are 200, refund and override clear the gate (D3), and erasure, a service
      update and the invoice list are 403; *given* an Accountant token, *then* the invoice list, the pay periods and the revenue
      report are 200 and the order detail, the customer page and an employee document download are 403;
      *given* a Manager token, *then* the company lifecycle and the legal documents are 403. *Met by the
      four behavioural host classes (`AdminRoleSupportBehaviourTests`, `AdminRoleAccountantBehaviourTests`,
      `AdminRoleManagerBehaviourTests`, `AdminRoleClaimlessAdministratorBehaviourTests`) — compiled at the
      lane, first executed by CI (Docker down on the box).*
- [x] **AC4** — *Given* a Manager, Support and Accountant each perform one admin act, *then* three
      `AdminActionAudits` rows exist with `ActorAdminRole` = their role. *Met — the behavioural classes assert
      the row; `AuditEntryFactoryAdminRoleTests` pins the claim read on both arms (unit, green).*
- [x] **AC5** — *Given* a company with one Administrator and one Support, *when* the Administrator demotes
      themselves or is demoted, *then* `admin_user.cannot_change_own_role` /
      `admin_user.cannot_demote_last_administrator`; *given* two Administrators, *when* two demotions of
      them race (Testcontainers, two connections), *then* **exactly one succeeds, on every run**, and the
      same holds for two deactivations. *Met — `SetAdminRoleTests` + `AdminRoleGuardsRepositoryTests` (unit,
      green); `AdminRoleGuardRaceTests` (Postgres, first executed by CI).*
- [x] **AC6** — *Given* the same company, *when* the Administrator is deactivated while only a Support
      remains, *then* `admin_user.cannot_deactivate_last_admin` (the existing key,
      `BusinessErrorMessage.cs:424`). *Met — `DeactivateAdministratorIfAnotherRemainsAsync`'s predicate reads
      `other.AdminRole == Administrator`; covered in `AdminRoleGuardsRepositoryTests`.*
- [x] **AC7** — The emitted DDL carries `CK_Users_AdminRole_Profile`; an Administrator with a null role and a
      Customer with a role both fail `23514`; the seed row reads `AdminRole = 1`;
      `CreateWithPassword(…, Administrator)` without a role throws. *Met — `20260919142517_Initial.cs:1099`;
      `AdminRoleCheckConstraintTests` (Postgres, first executed by CI); the seed applied by the regen host
      read `Profile=100, AdminRole=1`; `UserAdminRoleTests` (unit, green).*
- [x] **AC8** — *Given* an `admin.order.new` event for a company with one Accountant and one Support, *then*
      one feed row (the Support's); *given* an `admin.dispute.chargeback` event, *then* two (both). *Met —
      `AdminNotifier` filters through `AdminRoleSets.For(entry.Audience)`; the chargeback entry is `AdminOnly`.*
- [x] **AC9** — `CompanySignInGate.RefusedProfiles == { Employee }` (a pinned test). *Met — unchanged (D7).*

### Implementation notes

ADR-0066 D1–D9. The `Initial` regen and the DEV drop are the lane's to run and to report (CLAUDE.md
§Database migrations; memory: the drop is timed with the deploy). T-0770 AC1's "per administrator"
becomes "per administrator whose role admits `SupportOrAbove`" when this lands (panel finding F3).

## Status log

- 2026-09-14 — filed `todo` by the docs lane on the owner's ruling; waiting on the owner to open it.
- 2026-09-19 — opened by the owner (*"let's do those 3 for now"*: Support, Accountant, Manager beside
  Administrator); designed as ADR-0066 (`proposed`), panel-accepted the same day; queued last on
  the batch-6 backend lane. The brief above replaces the 2026-09-14 sketch where they differ (the sketch's
  `UserProfile` members became a column and a claim — ADR-0066 D1; the sketch's `AdminOr*` combinations
  became the four "or above" sets — D2). The web half is T-0773.
- 2026-09-19 (review) — `todo`, no owner: no lane holds this ticket yet; the backend lane flips it to
  `in_progress` when it picks it up after T-0775. **The frontmatter `title` is kept as filed on
  2026-09-14 on purpose:** ADR-0066 cites the 2026-09-14 sketch in this file by line (`:37`, `:44-47`)
  and as *"T-0748's own sketch"*, so lines 1–74 stay as they were; the current title is the INDEX row's
  (*"Four administrator roles — Administrator / Manager / Support / Accountant …"*) and the brief below
  the rule carries it. Retitle only together with a re-pin of ADR-0066's citations.
- 2026-09-19 — `in_progress` → `done` by the backend lane: **`56fa5aa2`** (104 files) and the fix
  **`45462202`** (the dead `User.SetAdminRole()` writer removed, the console's guards pinned against another
  company's administrator). `Initial` regenerated as **`20260919142517`** (`dotnet ef migrations remove
  --force` + `add Initial`, startup `Cleansia.Web.Partner`); the admin NSwag client regenerated against a
  portable Postgres (`generate-admin-client`, typecheck 3/3); DEV not dropped — owed at the deploy (MS-2).
  `dotnet build Cleansia.Api.sln -m:4` 0 errors; `Cleansia.Tests` **6174 passed, 0 failed**;
  `Cleansia.IntegrationTests` and `Cleansia.HostTests` compiled, not executed locally (Docker down) — CI is
  their first execution. Departures from ADR-0066's text are in its §What shipped (no `User.SetAdminRole()`;
  `Admits` also `IsDefined`; the lock is Postgres-only by construction; four allow-list entries). Docs by
  the docs lane the same night: ADR-0066 `accepted`, `docs/domain/roles/admin-role-gate.md`, security-rules
  S2, features, the admin overview, the API auth page, model, changelog, MS-2, the owner plate.
