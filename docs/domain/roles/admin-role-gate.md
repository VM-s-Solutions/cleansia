# Role — AdminRoleGate (the four administrator roles and the five sets) (ADR-0066, accepted 2026-09-19) (CRC card)

> Introduced by **ADR-0066** (`docs/decisions/adr-0066.md`, **`accepted`** 2026-09-19; owner ruling on
> D8, 2026-09-19: *"let's do those 3 for now"* — Support, Accountant, Manager beside Administrator). Shipped
> as T-0748 (the model, the sets, the map, the admin-host reads, the assignment, the matrix — `56fa5aa2`,
> `45462202`) and T-0773 (the admin web's session, guards, sidebar and administrators page — `14c833bf`,
> `bef0838e`). The files that are the role: `Core.Domain/Enums/AdminRole.cs` (the four members) ·
> `Core.Domain/Users/User.cs` (`AdminRole?` beside `Profile`) · `Core.AppServices/Authentication/AdminRoleSets.cs`
> (the one place a set is spelled, `For`, `Admits`) · `Core.AppServices/Authentication/PhysicalPolicy.cs` (the
> four set names) · `Core.AppServices/Authentication/PolicyBuilder.cs` (every `Policy.*` → one physical policy)
> · `Cleansia.Config/Services/ServiceExtensions.cs` (the four registrations) and
> `AuthorizationCompletenessStartupFilter.cs` (presence and semantics at boot) ·
> `Core.AppServices/Features/AdminUsers/SetAdminRole.cs` + `Infra.Database/Repositories/UserRepository.cs` (the
> two guarded writes) · `Core.AppServices/Auditing/AuditEntryFactory.cs` (the role on the audit row) · the
> admin web's `libs/core/services/src/lib/auth/{permission.service,admin-role-sets,policy}.ts`,
> `libs/core/admin-services/src/lib/guards/{admin,permission}.guard.ts` and
> `apps/cleansia-admin.app/src/app/admin-menu.ts`.

## Responsibility (one sentence)

Answer, fail-closed, whether a principal's administrator role — or a `User` row's — is in a **named set**,
so that every admin-host permission resolves through the one existing map to "the least role that has it,
and everyone above", the server is the gate, the web is a hint that mirrors the same map, the audit row
records the role an act ran under, and the partner hosts never read the role at all.

## The four roles and the five sets

`AdminRole { Administrator = 1, Manager = 2, Support = 3, Accountant = 4 }` is a **second axis on the
account**, not a profile: `UserProfile.Administrator` still answers *which audience is this?* (the token
mint, `CustomerOnly`, `OwnerOrElevated`, `AuditGate` and every handler-level `role == Administrator` check
are untouched), and `User.AdminRole` answers *which administrator?* — `NOT NULL` iff the profile is
`Administrator`, enforced by `CK_Users_AdminRole_Profile` (`("Profile" = 100) = ("AdminRole" IS NOT NULL)`),
so a customer with a role and an administrator without one are both rows the database refuses. The matrix is
a lattice, Administrator ⊇ Manager ⊇ (Support ∪ Accountant), so four sets and "any" cover it:

| Physical policy | Set | Requires the `admin_role` claim |
|---|---|---|
| `AdministratorOnly` | { Administrator } | yes |
| `ManagerOrAbove` | { Administrator, Manager } | yes |
| `SupportOrAbove` | { Administrator, Manager, Support } | yes |
| `AccountantOrAbove` | { Administrator, Manager, Accountant } | yes |
| `AdminOnly` | any administrator (the constant keeps its name and its pre-ADR meaning) | **no** — a token minted before the deploy stays usable on any-administrator routes until its refresh |

**Who has what** (`PolicyBuilder.Map`; the full row-by-row table is ADR-0066 D3, pinned by
`FrozenPermissionMapTests` and evaluated by `AdminRolePolicyMatrixTests`):

| Area | Administrator | Manager | Support | Accountant |
|---|---|---|---|---|
| Company lifecycle, company settings (view too), legal documents, administrator accounts and **role assignment** | ✓ | — | — | — |
| Administrators list (`CanViewAdminUsers`) | ✓ | ✓ | — | — |
| Catalogue and market-configuration **writes** (services, packages, extras, languages, countries, currencies, service cities, plans, tiers, promo codes, templates, sitewide push, country configuration), pay-rate writes, company-info writes, credit write-off, erasure and its retry, unmasked payout reveal | ✓ | ✓ | — | — |
| Orders (list, unredacted detail, photos, customer identity, door codes, cancel, override, reassign, refunds), disputes, credit issue, loyalty grants, referral intervention, cleaner approval / rejection / edit, identity documents, exports and the incident file, consents, GDPR requests, the audit log | ✓ | ✓ | ✓ | — |
| Payout invoices, pay periods and every act on them, pay-rate **reads**, revenue and payroll reports, fiscal failures, masked payout details | ✓ | ✓ | — | ✓ |
| Catalogue **reads**, the cleaner list, company info, credit balance, referrals, promo codes, notifications feed | ✓ | ✓ | ✓ | ✓ |

**Eight shared reads got an admin-host twin** — `CanViewPagedOrderAdmin`, `CanViewOrderDetailAdmin`,
`CanViewOrderPhotosAdmin`, `CanViewEmployeeDocumentsAdmin`, `CanViewEmployeePayoutDetailsAdmin`,
`CanViewPagedInvoicesAdmin`, `CanViewPayPeriodsAdmin`, `CanViewPayPeriodAdmin` — because the physical
policy a cleaner's own order, document, invoice or pay period read shares (`EmployeeOrAdmin` /
`Authenticated`) cannot be narrowed without refusing the cleaner; the admin controllers moved onto the twins
and the partner hosts are byte-identical. Two more are new: `CanViewLegalDocuments` (Administrator; the legal
page used to ride `CanViewCountryConfigurations`) and `CanSetAdminRole`.

## Collaborators

- **`AuthExtensions.SetClaims`** — yields `admin_role = <AdminRole>` beside `ClaimTypes.Role = Administrator`
  and `tenant_id`, for that profile only. **`JwtTokenResponse.AdminRole`** carries the same value to the
  web (the JWT is HttpOnly); `RefreshToken` re-mints through the same path, so a changed role reaches the
  token — and the web's stored hint, because the refresh response re-runs `setSession` — at the next
  refresh, ≤ 15 minutes on the admin host. No bulk revocation on a role change: fifteen minutes of a role
  just narrowed, every act of which is audited with the role it ran under, is the accepted residual.
- **`AdminRoleSets.Admits(ClaimsPrincipal, set)`** — `IsInRole(Administrator)` **and** a parseable, defined
  `admin_role` **and** membership; a missing, unparseable or out-of-range claim is `false`, never "any".
  **`AdminRoleSets.For(physicalPolicyName)`** — the set a name denotes (`AdminOnly` → all four); any other
  name throws. Both halves of the gate ask this one class: the authorization handlers about a principal,
  the [AdminNotifier](./admin-notifier) about a `User` row (`AdminEventCatalog.Entry.Audience` is a set
  **name**, never a policy, so a chargeback can be *any administrator* — Support answers the bank, the
  Accountant reconciles the money).
- **`AddCleansiaAuthorization`** — the four `RequireAssertion` registrations beside `AdminOnly`'s
  `RequireRole("Administrator")`; **`AuthorizationCompletenessStartupFilter`** asserts at boot that all five
  are registered and behave — a Support principal passes `SupportOrAbove` and `AdminOnly` and fails the
  other three; a claimless Administrator passes `AdminOnly` alone; an Employee fails all five.
- **`PolicyBuilder.Map` + `AssertComplete`** — the seam ADR-0001 built, reused unchanged: every `Policy.*`
  constant maps to one physical policy or boot fails; `ToPhysicalPolicy` resolves an unmapped one to `Deny`.
  The role changed the *values* in the map, not the mechanism.
- **`SetAdminRole`** — `POST api/AdminUser/{userId}/role`, `CanSetAdminRole` (Administrator only), the
  `auth` rate-limit window, audited as `admin.user.set_role` with a `RoleSnapshot(UserId, Role)` before and
  after (no PII). The validator refuses a missing or non-administrator target (`admin_user.not_found`) and
  the caller's own row (`admin_user.cannot_change_own_role`); the handler calls
  **`IUserRepository.DemoteAdministratorIfAnotherRemainsAsync(tenantId, userId, role)`** and turns `0 rows`
  into `admin_user.cannot_demote_last_administrator`. The tenant is the actor's claim, passed explicitly,
  so the lock key and the predicate name the same company.
- **The two guarded writes** (`UserRepository`) — each one transaction: `SELECT pg_advisory_xact_lock(hashtext(tenantId))`
  (Postgres; a no-op on the SQLite unit-test provider, where one connection serialises writers), then one
  conditional `UPDATE` whose `WHERE` is *"this row is an Administrator-profile user of this company, and
  either the new role is Administrator or another active Administrator-**role** administrator remains"*.
  **`DeactivateAdminUser` moved onto the second** (`DeactivateAdministratorIfAnotherRemainsAsync`), and its
  predicate narrowed the same way — so a company cannot deactivate its only Administrator while a Support
  remains and lock its own console (`admin_user.cannot_deactivate_last_admin`, the existing key). Two
  demotions or deactivations of the last two Administrators serialise on the lock and exactly one lands,
  deterministically (`AdminRoleGuardRaceTests`, Testcontainers). A bare conditional `UPDATE` was the first
  draft and is write skew under READ COMMITTED.
- **`User.CreateWithPassword(…, adminRole:)`** — the only domain writer: an Administrator profile without
  a role, or any other profile with one, throws (`InvalidOperationException` — a programming error, not
  input; a silent default to the most privileged role would be fail-open in a factory). `CreateAdminUser.Command.Role`
  is required and `IsInEnum`; the DEV seed inserts the administrator with `AdminRole = 1`;
  `sql-scripts/set-admin-role.sql` sets both columns. There is **no** `User.SetAdminRole()`: a second
  writer that skips the guard is the door D4 closes.
- **`AuditEntryFactory`** — reads the claim (parse + `IsDefined`) into `AdminActionAudit.ActorAdminRole` on
  the success and the failure arm alike; `AuditGate`'s admin arm still fires on the **profile**, so every act
  by a Manager, Support or Accountant lands on the admin table with the role it ran under. The audit-log
  page shows the role beside the actor and filters by it (`GetPagedAdminActionAudits.ActorAdminRole`).
- **The admin web** — `AdminAuthService.setSession` stores `adminRole` under `AUTH_COOKIE_KEYS.adminRole`
  beside `role`; `PermissionService.satisfies` answers a set policy with `role === Administrator &&
  ADMIN_ROLE_SETS[physical].includes(adminRole)` against `POLICY_MAP`, the hand-kept mirror of
  `PolicyBuilder.Map` that `policy-map-mirror.spec.ts` diffs against the C# source (map, physical policies,
  role names, set members); `adminGuard` admits the Administrator **profile** only (the Employee leftover is
  gone); `permissionGuard` sends a route whose `data.permission` the role lacks to `/unauthorized`; every
  `ADMIN_MENU_ITEMS` entry carries a `permission` and the landing route is the first page the role can
  open; the administrators list shows the role, the create form defaults to **Support** (least privilege,
  O-R9), the detail's role picker is under `CanSetAdminRole`, disabled on the caller's own row with the
  server's own key, and renders `api.admin_user.cannot_demote_last_administrator` in five locales.
  **The hint over-shows on an unknown policy** (`Authenticated` fallback) — the server refuses.
- **`CompanySignInGate`** — unchanged: `RefusedProfiles = { Employee }`, so an administrator of any role
  of a deactivated company is admitted (Q-LC-01 as ruled); what narrowed is that only an **Administrator**
  can reactivate it.

## Does NOT know

- **What a policy is for, or which controller routes it.** The gate answers *set membership*; the map
  says which set a permission needs; the controller attribute says which permission a route needs.
- **The company, beyond the lock key.** The role is per company by construction (administrators are
  stamped with their company — ADR-0061 D5); there is no holding role and no per-market scope inside a
  company — a Support of a two-market company sees both.
- **The partner or customer hosts.** The `admin_role` claim rides an administrator's partner-audience token
  and is read by nothing there; an administrator of any role passes every partner-host policy an
  administrator passed before, including `CanCalculateOrderPay` (`AdminOnly`, partner host only).
- **A fifth role.** One enum value, one set, one column of the matrix, one frontend case — and a ruling.
- **Invitations or self-service.** Administrators are created as before (`CreateAdminUser`, the seed, the
  hand tool); the role is assigned by an Administrator.

## Invariants a reviewer checks

1. **`UserProfile` is unchanged**, and `grep -rn admin_role src` finds the claim writer (`AuthExtensions`),
   `AdminRoleSets`, the audit factory, the startup filter, the test JWT factory and tests — **no handler**.
2. **`FrozenPermissionMapTests` equals ADR-0066 D3**; the admin controllers reference none of the eight
   shared read policies (`grep -n "CanViewPagedOrder\b\|CanViewOrderDetail\b\|CanViewOrderPhotos\b\|CanViewEmployeeDocuments\b\|CanViewEmployeePayoutDetails\b\|CanViewPagedInvoices\b\|CanViewPayPeriods\?\b" src/Cleansia.Web.Admin/Controllers` → nothing);
   the partner and mobile controllers are byte-identical to before.
3. **`AdminRolePolicyMatrixTests`** passes every map row × {Administrator, Manager, Support, Accountant,
   claimless Administrator, Employee, Customer, anonymous}; **`AdminHostPermissionCoverageTests`** finds
   every admin-host action carrying `[Permission]` → non-`Deny` or on the four-entry allow-list (`Login`,
   `RefreshToken`, `Logout`, `AdminCodeController.GetOverview`), each with a reason, count asserted.
4. **Both guarded writes are one transaction each** — lock, then the conditional `UPDATE` — and the race
   test proves exactly one of two concurrent demotions (and deactivations) succeeds on every run.
5. **`CK_Users_AdminRole_Profile` is in the emitted DDL** (`20260919142517_Initial`); an Administrator with
   a null role and a Customer with a role both fail `23514`; the seed row reads `AdminRole = 1`; every
   fixture that creates an Administrator passes `adminRole:`.
6. **Every act by a Manager / Support / Accountant token lands on `AdminActionAudits` with
   `ActorAdminRole` set** (the four behavioural HostTests classes, one per role, plus the claimless one).
7. **`AdminNotifier` writes rows only for administrators whose role is in the entry's audience** — one
   Accountant and one Support: `admin.order.new` reaches the Support alone, `admin.dispute.chargeback`
   reaches both.
8. **The web**: `adminGuard` no longer admits Employee; every top-level route carries `data.permission`
   and every sidebar item a `permission` (`admin-role-surface.spec.ts` walks both arrays); a Support and an
   Accountant session compose to exactly the expected sidebar and route verdicts
   (`admin-role-visibility.spec.ts`); the two refusal keys exist under `api.admin_user.*` in all five
   admin locales (the error-contract parity spec).

## Watch-list

- **A new admin-host route** is one `Policy` constant, one map row onto a set, one attribute, one
  `POLICY_MAP` row — the matrix test, the coverage test and the mirror spec each redden on the half that is
  missing.
- **A read shared with a partner host that must now be role-gated** gets an `…Admin` twin, never a narrowed
  shared physical policy (a cleaner would lose their own rows).
- **Two shared-policy smells are reported, not split** (owner-plate F13): the document-requirement CRUD on
  `CanAdminUpdateEmployee` (Support's) and the admin-user read `{userId}` on `CanViewOrderCustomer`.
- **The Accountant's "receipts / refund reads"** have no admin-host surface of their own; the revenue report
  is where refunds are read (owner-plate F12).
- **A single-Administrator company cannot demote or deactivate its Administrator** — by design; create a
  second Administrator first.
