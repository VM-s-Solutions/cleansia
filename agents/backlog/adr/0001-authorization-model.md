# ADR-0001 — Authorization model: fail-closed policy map, complete permission table, per-host policy completeness, and JWT trust boundaries

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-01
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | cross-cutting

> This ADR is **ADR-AUTHZ**. It is the frozen permission map and authorization contract that
> implementers code against. It resolves Wave-0 audit items **BSP-1, BSP-6, BSP-7, BSP-8, IDA-SEC-04**
> and is linked to **IDA-SEC-06**. Once `accepted` it is immutable — change it by superseding, never by
> editing.

---

## Context

The platform's authorization is a two-stage indirection:

1. A controller method is decorated `[Permission(Policy.CanXxx)]`. `PermissionAttribute` is
   `AuthorizeAttribute(policy: permission.ToPhysicalPolicy())` — the **logical** permission name
   (`Policy.*`, ~150 constants in `Policy.cs`) is translated **at attribute construction** to a
   **physical** policy name (`PhysicalPolicy.*`: `Anonymous`, `Authenticated`, `CustomerOnly`,
   `EmployeeOrAdmin`, `AdminOnly`, `OwnerOrElevated`).
2. Each API host registers the physical policies in its own
   `ServiceExtensions.AddUserAuthorization`. ASP.NET resolves the physical name at request time.

This indirection is the seam that lets all five hosts (Web.Admin/Customer/Partner,
Mobile.Customer/Partner) share one permission vocabulary while differing in JWT audience. (A sixth
*context* — the in-process integration-test harness — does **not** load this seam at all; see D6.)
The seam is currently **broken in five ways**, all verified against the real code:

- **BSP-1 / BSP-6 — fail-OPEN default.** `PolicyBuilder.cs:204-205`:
  ```csharp
  public static string ToPhysicalPolicy(this string permission) =>
      Map.GetValueOrDefault(permission, PhysicalPolicy.Authenticated);
  ```
  Any `Policy.*` constant **absent from `Map`** silently degrades to `Authenticated` — *any logged-in
  user, any role, any host*. There is no compile-time or startup signal. New permissions are
  fail-open by construction.

- **BSP-1 (proven instance) — the entire payroll family is unmapped.** Grepping `PolicyBuilder.cs`
  for the pay family returns **zero** entries. Absent from `Map`: `CanViewPagedInvoices`,
  `CanViewPeriodPays`, `CanCalculateOrderPay`, `CanGenerateInvoice`, `CanApproveInvoice`,
  `CanMarkInvoicePaid`, `CanCancelInvoice`, `CanClosePayPeriod`, `CanViewPayPeriods`,
  `CanViewPayPeriod`, `CanCreatePayPeriod`, `CanUpdatePayPeriod`, `CanOpenPayPeriod`,
  `CanDeletePayPeriod`, `CanViewPayConfigs`, `CanViewPayConfig`, `CanCreatePayConfig`,
  `CanUpdatePayConfig`, `CanDeletePayConfig`. All fall through to `Authenticated`. **A customer's
  JWT can read and mutate payroll.** This is a live data-exposure and integrity hole touching the
  documented pay-calculation seam (CLAUDE.md *Pay Calculation*; IMP-3).

- **BSP-6 (proven instance) — a wrong-but-present mapping.** `CanRespondToDispute` is mapped to
  `PhysicalPolicy.Authenticated` (`PolicyBuilder.cs:76`) while `Policy.cs:103` documents it
  "Admin (Only admins can respond/add messages)". Any authenticated user can post admin replies on
  disputes. This shows fail-closed-default alone is insufficient — the map must also be *audited for
  correctness*, not merely *completeness*.

- **BSP-7 — per-host policy registration gaps.** Not every host registers every physical policy it
  can be asked to resolve. `CustomerOnly` is registered only on **Web.Customer** and
  **Mobile.Customer** (`Customer/ServiceExtensions.cs:211`, `Mobile.Customer/ServiceExtensions.cs`).
  **Web.Admin**, **Web.Partner**, and **Mobile.Partner** do **not** register it
  (verified: their `AddUserAuthorization` bodies register only `Authenticated`/`EmployeeOrAdmin`/
  `AdminOnly`/`OwnerOrElevated`). A controller mapped to `CustomerOnly` and routed onto the Admin
  host has no matching registered policy → ASP.NET authorization throws / fails to resolve at request
  time. Hosts diverge by hand-edited copy-paste with no guarantee of parity.

- **IDA-SEC-04 — `OwnerOrElevated` is a blanket employee grant.** The `OwnerOrElevated` assertion
  (Admin `ServiceExtensions.cs:211-228`, identical on all hosts) returns `true` for **any** Employee
  or Admin (`:216-218`) *before* it ever checks `routeId == sub`. `CanViewUserDetail` is mapped to
  `OwnerOrElevated` (`PolicyBuilder.cs:48`), so **any employee can read any user's detail by id** —
  an IDOR on user PII (violates S3/S4). The "owner" branch is dead for elevated roles; "elevated"
  swallows it. **Verified compounding fact (per the security challenger):** the *handler* behind it,
  `GetUser.Handler` (`GetUser.cs:35-40`), performs `GetByIdAsync(query.UserId)` and returns it with
  **no ownership check at all**, and the endpoint takes `[FromQuery] GetUser.Query` (property
  `UserId`) — **there is no `id` route segment** (`Web.Partner/Controllers/UserController.cs:28-39`).
  Both gates are broken: the policy reads a route value the endpoint never supplies, and the handler
  has no backstop. D3 below is rewritten to fix this for real.

- **BSP-8 / IDA-SEC-06 — JWT trust boundaries and refresh re-check.** Each host validates a distinct
  audience (`ValidateAudience=true`; Admin pins `JwtAudiences.Admin`, etc.) against one shared
  `JwtSettings:Secret` (HS256 symmetric). Roles are copied from `"role"`/`"roles"` to
  `ClaimTypes.Role` in `OnTokenValidated`. On refresh, `RefreshToken.cs` re-checks `user.IsActive`
  (`:69`) and *optionally* `RequiredProfile` (`:75`) / `RequiredAudience` (`:61`). The Admin host
  passes **both** `RequiredProfile=Administrator` and `RequiredAudience=Admin`
  (`AdminAuthController.cs:48`) — correct. But **Mobile.Customer passes only `RequiredAudience`, not
  `RequiredProfile`** (`AuthController.cs:101`). The handler *supports* the re-check; most non-admin
  hosts simply don't ask for it, so a profile change between login and refresh is not consistently
  re-evaluated. The trust boundary (which key signs what, which audience each host accepts, what
  refresh re-validates) is undocumented and inconsistently applied.

This is one decision — "how authorization is expressed and enforced across hosts" — because the five
items are inseparable: a fail-closed default is worthless without a complete *and correct* map; a
correct map is unenforceable if hosts don't register the policies it names; and the whole thing
rests on the JWT trust boundary being explicit. Splitting them would let one half ship while the
other re-opens the hole.

---

## Decision

> **Map principle (governs all of D2).** A `Policy.*` constant means the **same physical gate on
> every host**. The physical policy is computed at attribute construction and is host-independent.
> Therefore: **if two hosts need a different gate for the same logical operation, they are two
> permissions, not one.** A single permission may never be "AdminOnly here, EmployeeOrAdmin there."
> Any row that relies on the handler to enforce per-caller ownership ("own-data") is marked
> **[OWN-DATA]** and carries a mandatory ownership-test obligation (verification #6) — the physical
> policy is the coarse outer gate, the handler is the inner gate, and **the handler check must exist
> in code**, not in prose.

### D1 — Fail-CLOSED by default, guaranteed by a startup completeness assertion

`ToPhysicalPolicy` must never silently default. Two changes:

1. **Default to deny.** The fallback for an unmapped permission becomes a deny sentinel, not
   `Authenticated`:
   ```csharp
   // Sentinel physical policy registered on EVERY host as "always deny".
   public const string Deny = "Deny"; // PhysicalPolicy.cs

   public static string ToPhysicalPolicy(this string permission) =>
       Map.GetValueOrDefault(permission, PhysicalPolicy.Deny);
   ```
   Every host registers `Deny` as `p => p.RequireAssertion(_ => false)`. An unmapped permission thus
   returns 403, never "any authenticated user". Fail-closed. This is the **runtime backstop** in case
   the startup assertion (below) is ever bypassed.

2. **Make "unmapped" impossible in prod via a startup assertion.** Add
   `PolicyBuilder.AssertComplete()` — a **pure static reflection check** with no DI dependency, so it
   can run at registration time. The **exact wiring** (corrected per challenge — see Defense #C7/#5):
   it is invoked from a new shared `IStartupFilter` registered by `AddCleansiaAuthorization` (D4),
   which every host calls. `AssertComplete` runs synchronously when the filter is constructed (before
   the first request); `AssertAllRegistered` (D4.2), which needs a built provider, runs in the same
   filter's `Configure` delegate where `IApplicationBuilder.ApplicationServices` is available.
   ```csharp
   public static void AssertComplete()
   {
       var declared = typeof(Policy)
           .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Literal)
           .Where(f => f.FieldType == typeof(string))
           .Select(f => (string)f.GetRawConstantValue()!)
           .ToHashSet();

       // The SINGLE sanctioned place a permission may have no policy: it gates only
       // [AllowAnonymous] routes. This set MUST equal the full set of permission
       // constants that appear ONLY on [AllowAnonymous] actions (verification #1b).
       var anonymous = AnonymousAllowList; // see below — checked-in, reviewable constant

       var missing = declared.Except(Map.Keys).Except(anonymous).ToList();
       if (missing.Count > 0)
           throw new InvalidOperationException(
               $"Authorization map incomplete. Every Policy.* constant must be mapped " +
               $"(or listed as AllowAnonymous). Missing: {string.Join(", ", missing)}");

       var orphans = Map.Keys.Except(declared).ToList();
       if (orphans.Count > 0)
           throw new InvalidOperationException(
               $"Authorization map references unknown permissions: {string.Join(", ", orphans)}");

       // An allow-list entry that is ALSO mapped is a contradiction (a permission cannot be
       // both AllowAnonymous and policy-gated). Catch it here, not in prod.
       var contradictions = anonymous.Intersect(Map.Keys).ToList();
       if (contradictions.Count > 0)
           throw new InvalidOperationException(
               $"Permissions are both in the AllowAnonymous allow-list and the Map: " +
               $"{string.Join(", ", contradictions)}");
   }
   ```
   The frozen `AnonymousAllowList` (exhaustive — every `Policy.*` constant whose **only** uses are
   `[AllowAnonymous]` actions across all hosts; proven by verification #1b):
   ```csharp
   public static readonly IReadOnlySet<string> AnonymousAllowList = new HashSet<string>
   {
       Policy.CanViewCodeOverview,
       Policy.CanPerformGlobalSearch,
       Policy.CanViewOrderDetailWithOrderNumberAndEmail,
       Policy.CanCreateOrder,
       Policy.CanGetOrderStatus,
       Policy.CanRequestPasswordChange,
       Policy.CanChangePassword,
   };
   ```
   Result: a developer who adds a `Policy.*` constant and forgets to map it gets a **boot failure on
   every host**, in dev and in CI, before a single request. "Unmapped" can never reach prod.

This **adapts** S2 (`agents/knowledge/security-rules.md §S2` — "Authorization on every endpoint"):
S2 governs the *attribute on the controller*; D1 closes the *layer below it* so a present-but-unmapped
attribute can no longer degrade to "any authenticated user."

### D2 — The COMPLETE, frozen permission map

This table is the contract. Every `Policy.*` constant maps to exactly one `PhysicalPolicy.*`, or is
on the `[AllowAnonymous]` allow-list. Changes after `accepted` require a superseding ADR.
**[OWN-DATA]** marks a row whose physical policy is coarse and whose per-caller scoping is enforced
in the handler — each such row has a mandatory ownership integration test (verification #6) **and a
verified handler check** (the handler-state column records whether the check exists today).

**Legend (physical policies, all seven values defined in `PhysicalPolicy.cs`):**
`Anonymous` = `[AllowAnonymous]` · `Authenticated` = any logged-in user · `CustomerOnly` =
authenticated AND not Employee/Admin · `EmployeeOrAdmin` = Employee | Administrator · `AdminOnly` =
Administrator · `OwnerOrElevated` = **(see D3 — redefined)** · `Deny` = always 403.

| Policy constant | Physical policy | Change vs. current code |
|---|---|---|
| **Code / Search** | | |
| `CanViewCodeOverview` | Anonymous (allow-list) | unchanged |
| `CanPerformGlobalSearch` | Anonymous (allow-list) | unchanged |
| **Order** | | |
| `CanViewPagedOrder` | EmployeeOrAdmin | unchanged |
| `CanViewPagedUserOrder` | Authenticated | unchanged |
| `CanViewOrderDetail` | Authenticated **[OWN-DATA]** | unchanged (ownership enforced in handler via `OrderAccessService`, S3) |
| `CanViewOrderDetailWithOrderNumberAndEmail` | Anonymous (allow-list) | unchanged |
| `CanUpdateOrder` | EmployeeOrAdmin | unchanged |
| `CanCreateOrder` | Anonymous (allow-list) | unchanged |
| `CanGetOrderStatus` | Anonymous (allow-list) | unchanged |
| `CanTakeOrder` | EmployeeOrAdmin | unchanged |
| `CanStartOrder` | EmployeeOrAdmin | unchanged |
| `CanCompleteOrder` | EmployeeOrAdmin | unchanged |
| `CanUploadOrderPhoto` | EmployeeOrAdmin | unchanged |
| `CanViewOrderPhotos` | Authenticated **[OWN-DATA]** | unchanged (handler ownership check) |
| `CanDeleteOrderPhoto` | EmployeeOrAdmin | unchanged |
| `CanAddOrderNote` | EmployeeOrAdmin | unchanged |
| `CanUpdateOrderNote` | EmployeeOrAdmin **[OWN-DATA]** | unchanged (own-note check in handler) |
| `CanDeleteOrderNote` | EmployeeOrAdmin **[OWN-DATA]** | unchanged (own-note check in handler) |
| `CanReportOrderIssue` | Authenticated | unchanged |
| `CanUpdateOrderIssue` | EmployeeOrAdmin | unchanged |
| `CanDeleteOrderIssue` | EmployeeOrAdmin | unchanged |
| `CanSubmitOrderReview` | CustomerOnly | unchanged |
| `CanViewOrderReview` | Authenticated | unchanged |
| `CanCancelOrder` | CustomerOnly | unchanged |
| **Customer self-service** | | |
| `CanManageSavedAddresses` | CustomerOnly | unchanged |
| `CanManageMembership` | CustomerOnly | unchanged |
| `CanManageRecurringBookings` | CustomerOnly | unchanged |
| **User** | | |
| `CanViewPagedUser` | EmployeeOrAdmin | unchanged |
| `CanViewUserDetail` | **OwnerOrElevated (D3 semantics)** | **FIX IDA-SEC-04** — see D3; route-key + handler-check corrections are part of the ticket |
| `CanGetCurrentUser` | Authenticated | unchanged |
| `CanRequestPasswordChange` | Anonymous (allow-list) | unchanged |
| `CanChangePassword` | Anonymous (allow-list) | unchanged |
| `CanUpdateCurrentUser` | Authenticated | unchanged |
| `CanAddPhoneNumber` | Authenticated | unchanged |
| **Employee** | | |
| `CanGetCurrentEmployee` | Authenticated | unchanged |
| `CanCheckCurrentEmployee` | Authenticated | unchanged |
| `CanUpdateCurrentEmployee` | Authenticated | unchanged |
| `CanViewPagedEmployee` | AdminOnly | unchanged |
| `CanApproveEmployee` | AdminOnly | unchanged |
| `CanRejectEmployee` | AdminOnly | unchanged |
| `CanAdminUpdateEmployee` | AdminOnly | unchanged |
| **Employee Documents** | | |
| `CanViewEmployeeDocuments` | EmployeeOrAdmin **[OWN-DATA]** | unchanged (own-doc check in handler, S3) |
| `CanUploadEmployeeDocument` | EmployeeOrAdmin **[OWN-DATA]** | unchanged (own-doc check in handler) |
| `CanDownloadEmployeeDocument` | EmployeeOrAdmin **[OWN-DATA]** | unchanged (own-doc check in handler) |
| `CanApproveEmployeeDocument` | AdminOnly | unchanged |
| `CanRejectEmployeeDocument` | AdminOnly | unchanged |
| `CanDeleteEmployeeDocument` | EmployeeOrAdmin **[OWN-DATA]** | unchanged (own-doc check in handler) |
| **Payroll — Invoices** *(was unmapped → fail-open; now closed)* | | |
| `CanViewPagedInvoices` | **EmployeeOrAdmin [OWN-DATA]** | **ADD (BSP-1) + CORRECTED** — see note A; was draft `AdminOnly` which would 403 every cleaner on the Partner host |
| `CanViewPeriodPays` | EmployeeOrAdmin **[OWN-DATA]** | **ADD (BSP-1)** — handler check VERIFIED present (`GetPeriodPays.cs:52-61`) |
| `CanCalculateOrderPay` | AdminOnly | **ADD (BSP-1)** |
| `CanGenerateInvoice` | AdminOnly | **ADD (BSP-1)** |
| `CanApproveInvoice` | AdminOnly | **ADD (BSP-1)** |
| `CanMarkInvoicePaid` | AdminOnly | **ADD (BSP-1)** |
| `CanCancelInvoice` | AdminOnly | **ADD (BSP-1)** |
| `CanClosePayPeriod` | AdminOnly | **ADD (BSP-1)** |
| **Payroll — Pay Periods** *(was unmapped)* | | |
| `CanViewPayPeriods` | EmployeeOrAdmin | **ADD (BSP-1)** — pay-period metadata is **global / non-sensitive cycles**; NOT per-row scoped (justification corrected — see note B) |
| `CanViewPayPeriod` | EmployeeOrAdmin | **ADD (BSP-1)** — global cycle metadata; not per-employee (note B) |
| `CanCreatePayPeriod` | AdminOnly | **ADD (BSP-1)** |
| `CanUpdatePayPeriod` | AdminOnly | **ADD (BSP-1)** |
| `CanOpenPayPeriod` | AdminOnly | **ADD (BSP-1)** |
| `CanDeletePayPeriod` | AdminOnly | **ADD (BSP-1)** |
| **Payroll — Pay Config** *(was unmapped)* | | |
| `CanViewPayConfigs` | AdminOnly | **ADD (BSP-1)** |
| `CanViewPayConfig` | AdminOnly | **ADD (BSP-1)** |
| `CanCreatePayConfig` | AdminOnly | **ADD (BSP-1)** |
| `CanUpdatePayConfig` | AdminOnly | **ADD (BSP-1)** |
| `CanDeletePayConfig` | AdminOnly | **ADD (BSP-1)** |
| **Dispute** | | |
| `CanCreateDispute` | CustomerOnly | unchanged |
| `CanViewDispute` | CustomerOnly **[OWN-DATA]** | unchanged (own-dispute check in handler) |
| `CanViewDisputeList` | CustomerOnly | unchanged |
| `CanAddDisputeMessage` *(new — split from CanRespondToDispute)* | **CustomerOnly [OWN-DATA]** | **SPLIT (Verdict V1)** — the customer self-reply `AddMessage` on Customer/Mobile.Customer hosts; handler `AddDisputeMessage.Handler:50-54` scopes a non-staff message to `dispute.UserId == caller`. Was the overloaded `CanRespondToDispute=Authenticated` (fail-open, BSP-6). |
| `CanRespondToDispute` *(staff path)* | **AdminOnly** | **FIX BSP-6 + Q-0005 (owner-ratified 2026-06-01)** — staff reply (`IsStaffMessage=true`). Was `Authenticated`; lead V1 split it from the customer path; owner confirmed **Admin-only** (not EmployeeOrAdmin). The staff-reply endpoint moves Partner→Admin host. Handler derives `IsStaffMessage` from caller profile. Ships in T-AUTHZ-2. |
| `CanResolveDispute` | AdminOnly | unchanged |
| `CanUpdateDisputeStatus` | AdminOnly | unchanged |
| `CanUploadDisputeEvidence` | CustomerOnly **[OWN-DATA]** | unchanged (own-dispute check in handler) |
| **Reports** | | |
| `CanViewRevenueReport` | AdminOnly | unchanged |
| `CanViewPayrollReport` | AdminOnly | unchanged |
| **Fiscal** | | |
| `CanManageFiscalFailures` | AdminOnly | unchanged |
| **Catalog: Services / Packages / Languages / Countries / Service Cities / Currencies** | AdminOnly (all) | unchanged |
| **Admin Users** (`CanView/Create/Update/Deactivate/ActivateAdminUser`) | AdminOnly (all) | unchanged — see SuperAdmin note |
| **Company Info / Email Templates** | AdminOnly (all) | unchanged |
| **Feature Flags** (`CanView/Create/Toggle/DeleteFeatureFlag`) | AdminOnly | unchanged |
| `CanCheckFeatureFlag` | Authenticated | unchanged |
| **Country / Tenant Configuration** (View/Create/Update/Delete) | AdminOnly (all) | unchanged |
| `Authenticated` (device) | Authenticated | unchanged |
| **GDPR (self)** (`CanExportOwnData`, `CanDeleteOwnAccount`, `CanGrantConsent`, `CanWithdrawConsent`, `CanViewOwnConsents`) | Authenticated **[OWN-DATA]** | unchanged (operates on caller's own `sub`) |
| **GDPR (admin)** (`CanAdminExportUserData`, `CanAdminDeleteUserAccount`, `CanAdminViewUserConsents`, `CanViewGdprRequests`) | AdminOnly | unchanged |
| `CanViewMyLoyalty` | CustomerOnly | unchanged |
| `CanRedeemPromoCode` | CustomerOnly | unchanged |
| `CanViewMyReferral` | CustomerOnly | unchanged |
| **Admin Promo Codes** (View/Create/Update/Deactivate) | AdminOnly | unchanged |
| **Admin Loyalty Tier Configs** (`CanViewLoyaltyTierConfigs`, `CanUpdateLoyaltyTierConfig`) | AdminOnly | unchanged |
| `CanGrantLoyaltyPoints`, `CanViewUserLoyalty` | AdminOnly | unchanged |
| `CanViewReferrals` | AdminOnly | unchanged |
| `CanSendSitewidePromo` | AdminOnly | unchanged |

**Note A — `CanViewPagedInvoices` is EmployeeOrAdmin [OWN-DATA], not AdminOnly.** On the **Partner**
host this single permission gates an employee's **own** invoice list, invoice detail, PDF download,
and download (`Web.Partner/Controllers/EmployeePayrollController.cs:21,32,139` use it for
`GetPagedInvoices`, `GetInvoiceById/{invoiceId}`, `DownloadInvoice/{invoiceId}`). `AdminOnly` =
`RequireRole(Administrator)`, so the draft mapping would have **403'd every cleaner from their own
pay invoices** the day it shipped. Per the map principle, this stays **one** permission
(`EmployeeOrAdmin`) and the obligation is pushed into the handlers as a hard ticket scope:
`GetPagedInvoices.Handler`, `GetInvoiceById.Handler`, and `DownloadInvoice.Handler` MUST scope a
non-admin caller to their **own** `EmployeeId` (the proven pattern at `GetPeriodPays.cs:52-61`). This
is **[OWN-DATA]** with a *handler check that does not yet exist for two of the three* (invoice
list/detail/download) — closing that is an explicit deliverable of T-AUTHZ-2, not an assumption. The
admin invoice-management surface on the **Admin** host is gated by the genuinely admin-only payroll
*mutations* (`CanCalculateOrderPay`/`CanGenerateInvoice`/`CanApproveInvoice`/etc., all `AdminOnly`),
so admins are not under-gated by this choice.

**Note B — pay-period justification corrected.** `GetPagedPayPeriods.Handler`
(`GetPagedPayPeriods.cs:27-43`) applies only a status/year specification — **no per-caller scoping,
and none is needed**: pay periods are **global cycles** (start/end/status/year), not per-employee
rows. The previous draft's "per-row data scoped in handler" prose was **factually false** and is
removed; the truthful justification is "pay-period metadata is global / non-sensitive; an employee
may list cycles to locate their own pay, which is then fetched via the [OWN-DATA] `CanViewPeriodPays`
path." These rows are therefore **not** marked [OWN-DATA] and carry no ownership-test obligation.

**Note C — `CanRespondToDispute` is a SPLIT, not a flip to `AdminOnly` (corrected by the lead — Verdict V1).**
The author's draft Note C claimed "no non-admin endpoint dispatches `CanRespondToDispute`." **This is
false.** The permission gates `AddMessage` on the **Customer** host (`Web.Customer/DisputeController.cs:52-62`,
`CustomerApiController`) and the **Mobile.Customer** host (`Mobile.Customer/DisputeController.cs:52-62`)
**as well as** the Partner host (`Web.Partner/DisputeController.cs:54-64`). The shared handler
`AddDisputeMessage.Handler` (lines 50-54, 65-78) is explicitly dual-purpose: a `Command.IsStaffMessage`
flag distinguishes a **customer self-reply** (allowed iff `dispute.UserId == caller`, lines 50-54) from a
**staff reply** (push-notifies the customer, lines 65-78). Flipping the single permission to `AdminOnly`
would **403 every customer replying to their own dispute** — the BSP-6 anti-pattern (trusting the
`Policy.cs:103` comment over actual behavior) in reverse. This is a **divergent-intent collision** and the
D2 map principle forbids one permission with two correct host answers. **Resolution (T-AUTHZ-2 scope):**
SPLIT into two permissions —
- `CanAddDisputeMessage` → **CustomerOnly [OWN-DATA]** for the customer-host `AddMessage` endpoints
  (handler already scopes `dispute.UserId == caller`; the customer sends `IsStaffMessage=false`), and
- `CanRespondToDispute` → **AdminOnly** for the staff-reply endpoint (`IsStaffMessage=true`).
  **(Owner-ratified 2026-06-01 via Q-0005 — staff dispute replies are Admin-only.)** The SPLIT is
  unchanged, so the customer self-reply flow via `CanAddDisputeMessage` is unaffected; only the staff
  path tightens from EmployeeOrAdmin → AdminOnly. **Implication:** the staff-reply `AddMessage` mounted
  on the **Partner** host (`Web.Partner/DisputeController.cs:54-64`) must move to / be gated for the
  **Admin** host (no cleaner posts staff messages) — folded into T-AUTHZ-2 scope.

The handler MUST also stop trusting a client-supplied `IsStaffMessage` from a customer-host caller —
derive `IsStaffMessage` from the caller's profile, not the request body (a customer must not be able to
post a staff message by flipping the flag). That handler hardening is added to T-AUTHZ-2 scope. Until the
split lands, the interim D2 row (`EmployeeOrAdmin [OWN-DATA]`) is **knowingly wrong for the Customer
host** and the split is therefore a **blocking precondition of T-AUTHZ-2**, not a follow-up — see Verdict
V1.

**Note on SuperAdmin:** `Policy.cs:150-154` comments the Admin-User family as "SuperAdmin", but no
`SuperAdmin` value exists in `UserProfile` (`Customer=1, Employee=2, Administrator=100`) and no
`PhysicalPolicy.SuperAdmin` exists. This ADR maps that family to `AdminOnly` (the strongest gate that
*can* be enforced today) and escalates the comment/enforcement gap to `questions/open.md`
(Q-0001 — "Do we need a SuperAdmin tier?"). We do **not** invent a role here; that is a business
decision with lasting impact.

### D3 — Redefine `OwnerOrElevated` to fix IDA-SEC-04 (route key + handler corrected)

The current assertion grants any Employee/Admin unconditionally, making `CanViewUserDetail` an
employee-wide PII read; and the only consumer (`Web.Partner UserController.GetById`) takes a query
param `UserId`, not a route `id`, while its handler (`GetUser.Handler`) does **no** ownership check.
The fix has **three** concrete parts, all in T-AUTHZ-3:

1. **Policy semantics.**
   - **Admin** → allowed (full oversight is the Admin host's purpose).
   - **Owner** (caller's `sub` == the requested user id) → allowed (read your own record).
   - **Employee who is NOT the owner** → **denied at the policy layer.** An employee who legitimately
     needs another user's data does so through a *purpose-specific* permission (e.g. order/assignment
     endpoints already gated `EmployeeOrAdmin` with a handler ownership check), not the generic
     user-detail read.

2. **Read the id from the actual source (the route-key bug).** The current/draft assertion reads
   `RouteValues["id"]`, but the endpoint supplies the id as the **query** parameter `UserId`. The
   assertion is rewritten to read the id from a single canonical helper that checks, in order, the
   route value `id`, the route value `userId`, then the query value `UserId` — and the **controller
   contract** is frozen so future `OwnerOrElevated` endpoints use one of those names:
   ```csharp
   .AddPolicy(PhysicalPolicy.OwnerOrElevated, p => p.RequireAssertion(ctx =>
   {
       var user = ctx.User;
       if (user.IsInRole(UserProfile.Administrator.ToString())) return true;   // elevated = Admin ONLY
       if (ctx.Resource is not HttpContext http) return false;                 // see availability note
       var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
       var requestedId = ResolveSubjectId(http);   // route "id" | route "userId" | query "UserId"
       return requestedId is not null && requestedId == sub;                    // owner
   }));
   ```
   Because `ToPhysicalPolicy` is host-independent, this resolver is part of the **shared**
   `AddCleansiaAuthorization` (D4), so all hosts get the same corrected behavior.

3. **Add the missing handler ownership check (the real inner gate).** `GetUser.Handler`
   (`GetUser.cs:35-40`) currently returns any user by id with no check. T-AUTHZ-3 adds an ownership
   gate in the handler: a non-admin caller may only resolve their **own** `UserId` (compare against
   `IUserSessionProvider` `sub`); otherwise return the not-found business error (consistent with
   `GetPeriodPays`). This makes the inner gate **exist**, not merely be asserted. The policy is the
   outer gate; the handler is the inner gate that holds regardless of host or invocation path.

**Availability note (the `ctx.Resource is HttpContext` concern).** `OwnerOrElevated` is invoked only
through the MVC endpoint pipeline (`[Permission]` → `AuthorizeAttribute`), where
`AuthorizationHandlerContext.Resource` **is** `HttpContext` (verified: all five hosts route through
`app.UseAuthorization()` + `MapControllers` in `CleansiaStartupBase.Configure:130-144`; no host calls
`IAuthorizationService.AuthorizeAsync(user, resource, policy)` with a non-HttpContext resource, and
there is no minimal-API surface). If `ctx.Resource` is ever not an `HttpContext`, the owner branch
returns `false` (deny) — fail-closed for the owner, never an over-grant. Because the **handler** now
also enforces ownership (part 3), an admin still reads via the elevated branch and an owner still
reads via the handler even on any future non-MVC path. Verification #5 adds a **positive** test
(owner reads own id → 200 on the real MVC path) so a deny-all regression is caught.

### D4 — Per-host policy completeness (BSP-7): one shared registration + startup assertions

1. **Single source of registration, and the duplicate `AdminOnly` is deleted.** Replace the five
   hand-copied `AddUserAuthorization` bodies **and** the legacy `AddJwt` `AdminOnly` registration
   with **one** shared extension in `Cleansia.Config`:
   `AddCleansiaAuthorization(this IServiceCollection, IConfiguration)`. It:
   - sets the default policy (`RequireAuthenticatedUser` + the JWT scheme),
   - registers **all** physical policies — `Authenticated`, `CustomerOnly`, `EmployeeOrAdmin`,
     `AdminOnly`, `OwnerOrElevated` (D3 resolver), `Deny` — **exactly once**, and
   - registers the `IStartupFilter` that runs `AssertComplete` + `AssertAllRegistered`.

   **Confirmed duplicate to remove (BSP-7 self-inflicted):** today each host's `AddJwt` registers a
   *second* policy literally named `"AdminOnly"` via `RequireRole(UserRole.Admin)` (`UserRole.cs:5`
   = `"Administrator"`) at `ServiceExtensions.cs:194` — i.e. two `AddAuthorizationBuilder()` calls
   register the same name (`AddJwt`'s `"AdminOnly"` and `AddUserAuthorization`'s
   `PhysicalPolicy.AdminOnly`), last-registration-wins, silent. T-AUTHZ-3 **deletes** the `AddJwt`
   `"AdminOnly"` registration so `AddJwt` registers **no** authorization policy at all (it keeps only
   the JWT bearer setup); `AddCleansiaAuthorization` becomes the **single** source. We verified there
   are **zero** bare `[Authorize(Policy="AdminOnly")]` consumers in the codebase (grep returned
   none — every admin gate goes through `[Permission(...)]`), so deleting the legacy copy is safe.
   `UserRole.Admin` (`"Administrator"`) and `UserProfile.Administrator.ToString()` (`"Administrator"`)
   agree today; the consolidation removes the drift risk entirely by having one definition.
   `CustomerOnly` is now registered on **every** host, including Admin/Partner — closing the
   original gap. A `CustomerOnly` endpoint on the Admin host now resolves (and correctly denies an
   admin, who is not a customer) instead of throwing.

2. **Startup assertion that registration is complete AND semantically correct.** The shared
   `IStartupFilter` runs, after the container is built (where `IAuthorizationPolicyProvider` exists):
   - **Presence:** every `PhysicalPolicy.*` constant (except `Anonymous`, which is `[AllowAnonymous]`,
     not a registered policy) resolves to a non-null policy on this host.
   - **Semantics (added per challenge):** synthesize a known-admin `ClaimsPrincipal` and a known-
     customer `ClaimsPrincipal` and assert via `IAuthorizationService` that `AdminOnly` *passes* for
     the admin and *fails* for the customer, and `CustomerOnly` *fails* for the admin. This proves
     parity of **behavior**, not just parity of **names** (so a future drift between `UserRole` and
     `UserProfile`, or a wrong duplicate winning, is caught at boot, not in prod).

   Any unregistered or misbehaving physical policy → boot failure. A host can never again be missing
   or silently diverge a policy the map references.

This **adopts** the existing physical-policy seam (`PhysicalPolicy.cs`) rather than replacing it; it
removes the *duplication* that let the hosts drift.

### D5 — JWT key & audience trust boundaries (BSP-8) + refresh re-check (IDA-SEC-06)

Document and freeze the trust boundary; tighten the refresh re-check.

1. **Signing key.** All hosts share one HS256 symmetric `JwtSettings:Secret` (verified — each host
   reads the same key). **This is recorded as a deliberate, bounded trust decision**: any host can
   *verify* any token's signature; isolation between hosts is enforced **solely by audience
   validation**, not by key separation. The secret is therefore a top-tier secret (Key Vault /
   env-injected, never in source/appsettings committed). *Why-not asymmetric per-host keys:* see
   Alternatives — deferred, not rejected, with a migration note.

   **PROVEN cross-host forgery defense.** A Partner-host Employee token (`aud=cleansia.partner`)
   replayed against the Admin host **fails** `ValidAudience=cleansia.admin`
   (`Admin/ServiceExtensions.cs:145-146`). The shared HS256 secret only permits signature
   *verification*, never audience *acceptance*. Cross-host privilege escalation by token replay across
   *different-audience* hosts is therefore impossible. This is the load-bearing reason the shared
   secret is acceptable.

2. **Audience map (frozen) — with the real, asymmetric semantics stated.** Each host accepts exactly
   one audience; tokens are pinned at issue and refresh:

   | Host | `ValidAudience` | Issues tokens with `aud` |
   |---|---|---|
   | Web.Admin | `cleansia.admin` | `cleansia.admin` |
   | Web.Partner | `cleansia.partner` | `cleansia.partner` |
   | Web.Customer | `cleansia.customer` | `cleansia.customer` |
   | Mobile.Partner | `cleansia.mobile` | `cleansia.mobile` |
   | Mobile.Customer | `cleansia.customer` | `cleansia.customer` |

   **Frozen accept/reject semantics (this is a security contract, not cosmetics):**
   - **Customer pair = ONE trust zone.** Web.Customer and Mobile.Customer share `cleansia.customer`.
     A token (access *or* refresh) minted by either is **fully accepted by the other**. This is an
     **intentional** decision: the web and mobile customer surfaces expose the same role
     (`Customer`), the same permission set, and the same data; there is no `azp`/client-id
     discriminator and **no Customer-surface endpoint relies on which client called it** (verified —
     no controller reads a client/origin claim for authorization). Blast radius accepted: a stolen
     customer token is replayable web↔mobile. To *separate* them would require the
     `cleansia.mobile.customer` split (Alternatives) — deliberately rejected for now.
   - **Partner pair = TWO trust zones.** Web.Partner (`cleansia.partner`) and Mobile.Partner
     (`cleansia.mobile`) use **different** audiences for the **same** `Employee` role. Consequently a
     Web.Partner token is **rejected** by Mobile.Partner and vice-versa. This asymmetry vs. the
     Customer pair is **recorded as the current intended contract**: the partner surfaces are
     historically separate clients and are not unified. Whether to unify them (one `cleansia.partner`
     audience for both partner surfaces) or to *also* split the customer pair is a deliberate future
     decision — escalated to `questions/open.md` (Q-0003) — **not** silently "renamed later." The
     `cleansia.mobile` constant name being partner-specific is acknowledged misleading; renaming the
     constant is a non-behavioral follow-up (exact-match validation is unaffected) and must not be
     done as a drive-by because it touches issued-token compatibility.

3. **Refresh must re-check profile, not just audience (IDA-SEC-06).** `RefreshToken.cs` already
   re-checks `user.IsActive` (`:69`) and supports `RequiredProfile` (`:75`) + `RequiredAudience`
   (`:61`). The contract is now: **every host's refresh endpoint MUST pass BOTH `RequiredAudience`
   (its own) AND `RequiredProfile` (the profile that host serves).** Today only Admin does
   (`AdminAuthController.cs:48`); Mobile.Customer passes audience only (`AuthController.cs:101`). The
   fix is per-host:
   - Web.Customer & Mobile.Customer → `RequiredProfile = UserProfile.Customer`
   - Web.Partner & Mobile.Partner → `RequiredProfile = UserProfile.Employee`
   - Web.Admin → `RequiredProfile = UserProfile.Administrator` (already correct)

   **What this guarantees and what it does NOT (reconciled with §2):** the re-check binds a refresh
   token to a **profile**. It strongly prevents redeeming a refresh token on a host whose profile the
   user does **not** hold (e.g. a demoted user, or a Customer token at the Partner refresh endpoint
   → `cleansia.customer` ≠ `cleansia.partner` *and* `Customer` ≠ `Employee`). It does **NOT** add
   host-binding **within** the Customer trust zone: a Web.Customer refresh token is, by design,
   redeemable at Mobile.Customer (same audience, same profile) — consistent with §2's "one customer
   trust zone." This is the *intended* behavior; if web/mobile customer must ever be separable, the
   audience split (Alternatives) becomes load-bearing for IDA-SEC-06 and requires a superseding ADR.
   Roles in the new access token are re-read from the **current** DB `user.Profile` via
   `user.SetClaims` (`RefreshToken.cs:109`) — already correct; D5 makes the *gate* match.

This **adapts** S1 (`security-rules.md §S1` — server-truth identity): D5 extends "don't trust client
identity" to "re-derive role/profile from the DB at every refresh, not just at login."

### D6 — Integration-test reachability (verification feasibility)

The proven holes (BSP-1, BSP-6, IDA-SEC-04, BSP-7, IDA-SEC-06) live in the **middleware / policy /
JWT** layer. The existing `BaseIntegrationTest` (`BaseIntegrationTest.cs`) builds an
`IServiceCollection` via `AddCoreBindings`, injects a `TestUserSessionProvider`, pins a single
`IHostAudienceProvider`, and **invokes handlers in-process** — it never loads `AddJwt` /
`AddCleansiaAuthorization` (those are host-registered) and there is **no `WebApplicationFactory` /
`TestServer` in the repo** (verified: zero matches). Therefore the HTTP-level "→ 403/200" tests in
verification #5 are **not buildable on the current harness**. This ADR makes that explicit and splits
the verification into two tiers, with a prerequisite ticket:

- **T-AUTHZ-0 (prerequisite):** add a `WebApplicationFactory`-based host-test project
  (`Cleansia.HostTests`) that boots a real host (per audience) through the full
  authentication+authorization pipeline. Owned cost, must land before the #5 HTTP tests.
- Until T-AUTHZ-0 lands, the **policy-layer** tests (#1–#4, #6 below) — which resolve the policy and
  call `IAuthorizationService.AuthorizeAsync(principal, resource, policyName)` with a constructed
  `ClaimsPrincipal` — are the gating coverage and **are** buildable today. The end-to-end 403/200
  tests (#5) are gated on T-AUTHZ-0.

---

## Alternatives considered

- **Keep `Authenticated` as the default, just fill in the map (BSP-1 only).** Rejected: it fixes
  *today's* holes but leaves the seam fail-open, so the *next* unmapped permission re-opens the exact
  class of bug. The defect is the default, not any single missing row. Fail-closed + startup
  assertion removes the class.

- **Throw at attribute construction for unmapped permissions (no `Deny` sentinel).** Considered.
  `ToPhysicalPolicy` runs at attribute construction (type load), so a throw there *would* fail fast —
  but it fails at first-touch of the controller type, with an opaque `TypeInitializationException`,
  and can't enumerate *all* missing permissions at once. The explicit `AssertComplete()` at startup
  is clearer, lists every gap, and is independently unit-testable. `Deny` is still the runtime
  backstop in case the assertion is ever bypassed.

- **A `[Roles]`/attribute-only model, dropping the logical→physical indirection.** Rejected: it
  couples controllers to physical roles and destroys the seam that lets all hosts share one
  vocabulary and lets us re-target a permission (e.g. add SuperAdmin later) in one map instead of
  N controllers. The indirection is the asset; we're fixing its default and its completeness, not
  removing it.

- **Split `CanViewPagedInvoices` into `CanViewPagedInvoices` (AdminOnly) + `CanViewMyInvoices`
  (EmployeeOrAdmin).** Considered as the alternative to Note A. Rejected for now: the Partner host's
  invoice endpoints are inherently self-service (an employee only ever sees their own), so a single
  `EmployeeOrAdmin [OWN-DATA]` permission with handler scoping is the smaller change and keeps the
  map one-permission-per-operation. If the Admin host ever needs to *list all* invoices through the
  same controller path with different intent, the map principle (D2 header) requires splitting then —
  and that is a superseding ADR, by design.

- **Per-host asymmetric signing keys (each host signs with its own private key; others verify with
  public keys, or only the issuing host verifies).** Strong isolation — a leaked Customer key
  couldn't forge Admin tokens. Deferred, not rejected: it's a larger change (key distribution,
  rotation, JWKS) and the current audience-validation boundary is sound *if the shared secret is
  protected*. Recorded as a future ADR trigger (Q-0002). D5 documents the shared-key boundary so this
  can be revisited deliberately.

- **Split Mobile.Customer onto its own `cleansia.mobile.customer` audience (and/or unify the Partner
  pair).** Rejected for now: the mobile and web customer apps expose the same role surface and the
  same permission set; a split buys no security (same role, same data) at the cost of duplicate token
  plumbing. **But** D5 §3 makes this load-bearing *if* web/mobile customer ever must be separable,
  and the Partner pair is *already* split — so the "one model for both pairs?" question is escalated
  (Q-0003) rather than left as a footnote.

- **Make `OwnerOrElevated` keep the employee grant but add a handler check (IDA-SEC-04 via handler
  only).** Rejected as the *primary* gate: relying solely on the handler means every consumer of
  `CanViewUserDetail` must remember the check, and the policy name lies ("Elevated" implying any
  employee). Fixing the policy makes the gate honest; the handler check stays as defense-in-depth
  (S3) — and D3 part 3 makes that handler check actually **exist**, since today it does not.

---

## Consequences

**Cheaper:**
- Adding a permission is now safe-by-default: forget to map it → boot fails everywhere, in CI,
  before review. The whole class of fail-open regressions (BSP-1/BSP-6) is structurally prevented.
- Host parity is automatic *and behavior-verified*: one shared registration + presence + **semantics**
  assertion means no host can silently lack or diverge a policy (BSP-7 can't recur).
- The map is a single readable contract for reviewers, security, and implementers — the frozen spec
  IMP-3 and future payroll work code against.

**More expensive (new obligations on developers):**
- Every new `Policy.*` constant MUST be added to `PolicyBuilder.Map` **or** the frozen
  `AnonymousAllowList` — and an allow-list entry is a reviewable `[AllowAnonymous]` decision proven
  exhaustive by verification #1b.
- Any new `PhysicalPolicy.*` value MUST be registered in the shared `AddCleansiaAuthorization` (or
  the registration assertion fails on boot).
- Every **[OWN-DATA]** row MUST have a real handler ownership check **and** an ownership integration
  test (verification #6). Adding an [OWN-DATA] row without the handler check is a blocking review
  finding.
- Controllers using `OwnerOrElevated` MUST supply the subject id as route `id`/`userId` or query
  `UserId`, and the handler MUST enforce ownership (S3).
- Every host's refresh endpoint MUST pass its `RequiredProfile` and `RequiredAudience`.
- The shared JWT secret is now formally a top-tier secret with the documented blast radius (any host
  can verify any token; Customer web↔mobile is one trust zone); secret handling/rotation owns that
  boundary.

**Blast radius at deploy (enumerated — answers C1/C2/C10 of the pragmatic challenger):**
- The fail-open default means the **payroll family is currently reachable by any authenticated user**.
  Tightening it changes reachability for live callers. Per newly-restricted permission:
  - `CanViewPagedInvoices`/`CanViewPeriodPays` → **EmployeeOrAdmin [OWN-DATA]**: the **Partner** apps
    (web + mobile) consume these for the logged-in employee — they keep working *because* we chose
    `EmployeeOrAdmin`, **not** `AdminOnly`. A **Customer** loses (correct) access. Handler scoping
    must ship in the same PR or an employee could see another employee's invoices.
  - All `Pay*` mutations + `CanViewPayConfig*` → **AdminOnly**: these are admin screens only; no
    Employee/Customer flow consumes them today (verify against the partner NSwag client during
    T-AUTHZ-2). A Customer/Employee loses (correct) access.
  - `CanViewPayPeriods`/`CanViewPayPeriod` → **EmployeeOrAdmin**: partner UI may list cycles; kept
    reachable for employees.
  - `CanRespondToDispute` → **SPLIT** (Verdict V1, Note C): the customer-host `AddMessage` keeps a
    customer self-reply path under the new `CanAddDisputeMessage` (CustomerOnly [OWN-DATA]); the
    staff reply becomes `CanRespondToDispute` (**AdminOnly** — owner-ratified Q-0005). A naive flip of
    the *single* permission to `AdminOnly` WOULD have broken the live customer dispute-reply flow on
    Customer + Mobile.Customer — the SPLIT prevents that (the customer path stays on
    `CanAddDisputeMessage`/CustomerOnly), so tightening only the staff path to AdminOnly is safe. Plus
    the handler hardening (derive `IsStaffMessage` from profile).
- **D5 §3 refresh tightening:** the moment a host passes `RequiredProfile`, an in-flight refresh
  token held by a principal whose profile differs from the host's served profile will fail refresh
  and force re-login (e.g. an Administrator dogfooding the Partner host on a partner-audience token).
  This is the intended hardening; it is a **behavior change for live sessions** and support should
  expect a small refresh-failure blip at deploy. Verify there is no *legitimate* cross-profile host
  usage before enabling per host (the audience mismatch already makes most such cases impossible).

**Rollout ordering (MANUAL_STEP / ticket sequencing — answers C3/C4):**
- **T-AUTHZ-1 and T-AUTHZ-2 MUST ship in a single atomic PR** — the deny default + `AssertComplete`
  wiring **and** the complete map (payroll fill + dispute fix) together. Shipping the assertion
  before the map fills would make every host **fail to boot** (the payroll family is still unmapped),
  i.e. fail-to-start, not fail-closed. They are inseparable at the deploy boundary.
- **Snapshot vs. additive permissions (C4):** the frozen-map snapshot test (#4) and the consistency
  scan (#6) enforce that *no mapping drifts without an ADR*. They are reconciled with normal feature
  work as follows: **adding a new permission row** (a new `Policy.*` mapped to an existing physical
  policy) requires updating the checked-in expected snapshot in the **same PR** and is allowed
  **without** a superseding ADR *only* when it is purely additive and the reviewer signs the
  checklist (#8); **changing or removing an existing row's physical policy** (a semantics change)
  **requires a superseding ADR**. The snapshot test message states this explicitly so developers
  amend the snapshot rather than comment out the test. This keeps the pattern-evolution loop intact:
  additive rows are cheap, semantic changes are deliberate.
- Ticket split: T-AUTHZ-0 (host-test harness, prerequisite for #5), T-AUTHZ-1+2 (atomic:
  fail-closed + AssertComplete + map fill incl. payroll + **dispute SPLIT** `CanAddDisputeMessage`
  (CustomerOnly [OWN-DATA]) vs `CanRespondToDispute` (**AdminOnly** — Q-0005) + **move the staff-reply
  endpoint off the Partner host to Admin** + **`IsStaffMessage` server-derivation hardening** in
  `AddDisputeMessage.Handler`), T-AUTHZ-3 (shared
  `AddCleansiaAuthorization` + delete legacy `AddJwt` AdminOnly + AssertAllRegistered with semantics +
  OwnerOrElevated redefinition + `GetUser`/invoice handler ownership checks), T-AUTHZ-4 (refresh
  `RequiredProfile` per host), T-AUTHZ-5 (consistency-scan addition).
- No EF migration, no NSwag change (DTO contracts unchanged).

---

## How a reviewer verifies compliance

**Mechanical (automated — these are the gate):**
1. **Map completeness test (xUnit, `Cleansia.Tests`).** Calls `PolicyBuilder.AssertComplete()`;
   reflection over `Policy.*` minus `Map` keys minus `AnonymousAllowList` must be empty, `Map` must
   have no orphan keys, and the allow-list must not intersect `Map`. Fails the build the moment a
   permission is added unmapped. (Mirror of the startup assertion.)
   - **1b. Allow-list exhaustiveness test (NEW — answers the allow-list-must-be-proven challenge).**
     A test scans every host's controllers for actions decorated `[AllowAnonymous]` **and** carrying a
     `[Permission(...)]`, collects those permission constants, and asserts the set **equals**
     `AnonymousAllowList`. Guarantees the allow-list is neither short (→ boot bricks on a legit
     anonymous route) nor long (→ a real route silently un-gated).
2. **Per-host registration + semantics test (NEW semantics part).** For each host's authorization
   registration: every `PhysicalPolicy.*` (except `Anonymous`) resolves non-null; **and** a
   constructed admin principal passes `AdminOnly` & fails `CustomerOnly`, a constructed customer
   principal fails `AdminOnly` & passes `CustomerOnly`, an employee passes `EmployeeOrAdmin`. Proves
   behavior parity, not just name parity — catches a wrong duplicate winning or `UserRole`/`UserProfile`
   drift.
3. **No fail-open default test.** Assert `"SomeUnknownPermission".ToPhysicalPolicy() ==
   PhysicalPolicy.Deny` (never `Authenticated`). (Note: this asserts the runtime backstop for a
   synthetic non-`Policy` string; the "a real forgotten `Policy.*` is caught" guarantee is #1's
   build failure, not this test — the two are distinct by construction.)
4. **Frozen-map snapshot test.** Asserts the `Policy.* → PhysicalPolicy.*` pairs equal this ADR's D2
   table (a checked-in expected dictionary). Additive rows update the snapshot in-PR; a *semantic*
   change fails until a superseding ADR exists. Failure message points the developer at this
   reconciliation rule (so they amend, not delete, the test).
5. **End-to-end integration tests (gated on T-AUTHZ-0 host harness — D6):** Customer JWT against any
   payroll endpoint → 403; **Customer replying to their OWN dispute via `CanAddDisputeMessage` on the
   Customer host → 200, and to another customer's dispute → 403/404** (Verdict V1 / Note C — proves the
   split did not break the customer self-reply flow); **Customer against the Partner/Admin staff-reply
   `CanRespondToDispute` endpoint → 403**; **a customer-host `AddMessage` with `IsStaffMessage=true` is
   recorded as a customer message, not staff** (handler-derivation hardening); **Employee
   against another employee's invoice on the Partner host → 403/404** (Note A); Employee against
   `GET user` for a *different* id → 403/404 **and** owner reading their OWN id → **200** (IDA-SEC-04,
   both directions); Customer-audience refresh replayed against the **Admin** refresh endpoint →
   failure; Customer-audience refresh replayed across the **two customer hosts** → **success**
   (pins the intentional "one customer trust zone" so a future audience split is a conscious break);
   Web.Partner refresh token against Mobile.Partner → failure (pins the intentional Partner split).
6. **[OWN-DATA] ownership tests (NEW — the teeth behind the coarse-policy claim).** Every D2 row
   marked **[OWN-DATA]** has a test proving "caller A requesting caller B's resource → denied" at the
   layer where its check lives (handler-level today, end-to-end once T-AUTHZ-0 lands). A reviewer
   adding an [OWN-DATA] row without this test is a blocking finding. This is the mechanical answer to
   "the map's safety rests on handler checks that may not exist."
7. **Consistency check (T-AUTHZ-5):** add to `agents/tools/check-consistency.mjs` a static scan that
   flags any `Policy.*` constant string not present as a key in `PolicyBuilder.Map` and not in
   `AnonymousAllowList` — caught at lint time, before tests (`process/enforcement.md`).

**Manual (Reviewer/Security checklist for an authz-touching change):**
8. New endpoint carries `[Permission(Policy.CanXxx)]`, `[AllowAnonymous]`, or `[Authorize]` (S2);
   if `[AllowAnonymous]` + `[Permission]`, the permission is in `AnonymousAllowList` with a reason.
9. New `Policy.*` row that *changes an existing mapping's semantics* has a superseding ADR; a purely
   *additive* row updates the snapshot in-PR (per the reconciliation rule in Consequences).
10. Every **[OWN-DATA]** row's handler enforces ownership (S3) in code — confirmed by reading the
    handler, not by trusting the table. (This is the specific failure mode `GetUser.Handler` exhibited.)
11. Refresh endpoint for any new/changed host passes `RequiredProfile` + `RequiredAudience`.

---

## Roles affected

Role files in `agents/knowledge/roles/` created/updated by this decision:
- `policy-builder.md` — **responsibility:** translate a logical `Policy.*` permission to a physical
  policy, fail-closed, and assert at startup that every permission is mapped. **Collaborators:**
  `Policy` (vocabulary), `PhysicalPolicy` (targets), the shared `IStartupFilter`. **Does NOT know:**
  roles/claims, HTTP, the JWT, which host it runs on, ownership of any resource.
- `cleansia-authorization-registry.md` (the shared `AddCleansiaAuthorization`) — **responsibility:**
  register every physical policy identically on every host (incl. the `OwnerOrElevated` subject-id
  resolver and `Deny`), wire the startup filter, and assert presence + semantics. **Collaborators:**
  `AuthorizationBuilder`, `UserProfile`, `IAuthorizationPolicyProvider`, `IAuthorizationService`.
  **Does NOT know:** logical permissions, the JWT audience (host-specific), business resources.
- `host-audience-provider.md` (existing `IHostAudienceProvider`) — clarify **does NOT know:** roles,
  permissions, or the signing key; it knows **only** this host's audience string.
- `get-user-handler.md` (and the partner invoice handlers) — **responsibility:** return the requested
  resource **only after confirming the non-admin caller owns it**; the [OWN-DATA] inner gate.
  **Collaborators:** `IUserSessionProvider`/`OrderAccessService`, the repository. **Does NOT know:**
  the physical policy, the route shape (it reads `sub` from the session, not the request).
- Update `agents/knowledge/security-rules.md` companion note under S2 to cite this ADR as the
  sanctioned fail-closed mechanism, and under S3 to cite the **[OWN-DATA]** convention as the
  required marker for "policy is coarse, handler owns the row" (catalog edit accompanies acceptance,
  per the pattern-evolution loop).

---

## Challenge

### Challenger A (platform architect) — attacking D4 (per-host registration, BSP-7) and D5 (JWT trust boundary, BSP-8)

**C1 — D5's audience map contradicts the real code for Web.Partner vs Mobile.Partner; the "frozen"
table is wrong on row Mobile.Partner.** [...full text retained from the original draft...] "Cosmetic /
rename later" is not an answer; freeze the real semantics.

**C2 — D4's "register CustomerOnly on every host" is asserted safe but D5 makes it inert-or-wrong on
Admin/Partner, and the ADR never proves it cannot mis-grant.** [...full text retained...] or
`CustomerOnly`-by-absence is a latent privilege bug that D4 newly *spreads to two more hosts*.

**C3 — `OwnerOrElevated` depends on `ctx.Resource is HttpContext`, which is NOT guaranteed and the
ADR never verifies it holds on all hosts.** [...full text retained...] Add an explicit positive test:
**owner reading their OWN id → 200**, on the real MVC path.

**C4 — The shared-secret + audience boundary is sound against cross-host *forgery* but the ADR
overstates isolation and ignores cross-host *replay within the same audience*.** [...full text
retained...] must confirm no Customer-surface endpoint relies on *which* customer client called it.

**C5 — D5's refresh re-check fix (IDA-SEC-06) closes the profile gap but the same-audience Customer
pair means a Customer refresh token has NO host-binding beyond profile.** [...full text retained...]
right now D5 §2 and D5 §3 are in tension for the Customer pair.

**C6 — `AssertAllRegistered` cannot see mis-registration drift; the ADR removes the per-host bodies
without specifying the call site, and there is a duplicate `AdminOnly` in `AddJwt`.** [...full text
retained...] parity of *names* is not parity of *behavior*.

**C7 — Minor/sound, stated briefly.** The fail-closed `Deny` sentinel (D1) + `AssertComplete` startup
throw is the right shape. [...retained...] No challenge on D1 or the payroll rows.

### Challenger B (security architect) — attacking D2 (map) and D3 (`OwnerOrElevated`)

**1. [BLOCKER] D3 does not fix IDA-SEC-04 — the route key is wrong AND the handler has no ownership
check.** `CanViewUserDetail` is on `UserController.GetById([FromQuery] GetUser.Query)` with property
`UserId` — there is no `id` route segment, so D3's `RouteValues["id"]` makes the owner branch
unreachable (a customer can never read even their own detail), and `GetUser.Handler` (`GetUser.cs:35-39`)
does `GetByIdAsync(query.UserId)` with zero ownership check. Both gates broken.

**2. [BLOCKER] `CanViewPagedInvoices = AdminOnly` locks employees out of their OWN invoices on the
Partner host.** It gates the employee's own invoice list/detail/PDF/download
(`EmployeePayrollController.cs:21,32,127,139`). `AdminOnly` 403s every cleaner. Split or make
`EmployeeOrAdmin` + handler scoping.

**3. [BLOCKER] "per-row data scoped in handler" is false for `CanViewPayPeriods`/`CanViewPayPeriod`.**
`GetPagedPayPeriods.Handler:27-43` applies only a status/year spec — no caller scoping. Correct the
justification (global cycles) or add scoping.

**4. [PARTIAL CREDIT] `CanViewPeriodPays = EmployeeOrAdmin` is the ONE row where the handler check
genuinely exists** (`GetPeriodPays.cs:52-61`) — proving scoping is per-handler/ad hoc, not a guarantee
the map can lean on uniformly. Add a verification gate with teeth (per-[OWN-DATA]-row ownership test).

**5. [BLOCKER] `AssertAllRegistered` cannot run where the ADR says it does — authorization is
registered per-host, not in `CleansiaStartupBase`.** `CleansiaStartupBase.ConfigureServices` never
touches authorization; `AddCoreBindings` runs before `AddUserAuthorization`; `AssertAllRegistered`
needs a built provider (post-`Build()`). Specify the real injection point.

**6. [BLOCKER] There is a SECOND, conflicting `AdminOnly` policy — `AddJwt` registers it via
`RequireRole(UserRole.Admin)`.** Two registrations of the same name; the ADR must delete the `AddJwt`
copy and reconcile `UserRole`/`UserProfile`, else BSP-7's "one source" is contradicted.

**7. [MAJOR] `Deny` + host `SetDefaultPolicy` interaction unspecified; the allow-list must be proven
exhaustive** or `AssertComplete` bricks startup on a legit `[AllowAnonymous]` route.

**8. [MAJOR] The frozen map is host-blind, but the same `Policy.*` resolves on hosts with different
correct answers** (`CanViewPagedInvoices`). Add an explicit per-host-divergence principle.

**9. [MINOR] Snapshot test freezes mappings but cannot detect a correct-looking-but-wrong coarse
policy.** Add positive-path tests (owner reads own → 200; employee reads own invoice → 200).

**10. [MINOR] Shared `cleansia.customer` makes a Customer refresh token replayable across the two
customer hosts; pin the intended cross-customer-host success with a test** so a future split is a
conscious break.

### Challenger C (pragmatic senior engineer) — attacking rollout, serialization, testability

**C1 — Blast radius unenumerated.** List, per newly-restricted permission, which host/app consumes it
and confirm no legitimate Employee/Customer flow relies on the fail-open behavior.

**C2 — `CanRespondToDispute → AdminOnly` may 403 a customer dispute-reply flow** if customers can
currently reply; trusting the comment over behavior is the BSP-6 failure mode. Verify the dispute
controller/customer client.

**C3 — Big-bang migration; ticket order unpinned.** If T-AUTHZ-1 (fail-closed + AssertComplete) merges
before the map fills, every host fails to **boot**. Pin the order (atomic, or assertion-as-failing-test
first).

**C4 — Mid-wave serialization.** Once `AssertComplete` + snapshot test are live, the next ticket that
adds a `Policy.*` constant fails the build before its mapping lands; reconcile additive rows vs.
"superseding ADR for every new permission."

**C5 — Testability contradiction:** "unmapped ⇒ denied" (a synthetic string) vs. "unmapped can never
exist" (build failure) are different guarantees; name the contract.

**C6 — Integration tests in #5 can't be written:** `BaseIntegrationTest` runs handlers in-process; no
`WebApplicationFactory`/`TestServer` exists; the policy/JWT layer is never loaded. Add a host-test
project or downgrade #5 to policy-provider-level tests.

**C7 — `AssertAllRegistered` placed where it can't run** (needs a built provider; `AddCoreBindings` is
registration-only). Split registration-time vs. app-build-time assertions; confirm a shared hook every
host invokes.

**C8 — Removing per-host bodies also drops the legacy `AddJwt` "AdminOnly"; confirm nothing depends on
it.** Grep `[Authorize(Policy=]`/`"AdminOnly"` usage; fold or document.

**C9 — D3 hard-codes `RouteValues["id"]`; verify the actual route param name** on the
`CanViewUserDetail` endpoint(s) or the fix self-locks-out customers reading their own profile.

**C10 — D5 refresh tightening can lock out in-flight sessions at deploy;** record it in blast radius;
confirm no legitimate cross-profile host usage (admin on partner host).

## Defense

_(Author responds — REBUT / CONCEDE+REVISE / ESCALATE per challenge. Same finding raised by multiple
challengers is answered once and cross-referenced.)_

### On D1 / fail-closed (Challenger A C7, Challenger C C5) — REBUT + clarify
The `Deny` sentinel + `AssertComplete` shape is endorsed by all three challengers; no change to the
mechanism. C5's "two different guarantees" point is **correct and now stated explicitly** (verification
#3 note + D1.1 "runtime backstop"): (i) the runtime deny default is the backstop for "assertion
bypassed"; (ii) the build-time completeness test (#1) is what catches "a real `Policy.*` was forgotten."
No test can assert "real unmapped ⇒ 403 at runtime" while also asserting "real unmapped fails the
build" — they are mutually exclusive, and the ADR now names the contract. REBUT the implication that
this is a defect: it is the intended layering.

### On the route-key + missing handler check (Challenger B #1, Challenger C C9) — CONCEDE + REVISE (BLOCKER)
Verified against `GetUser.cs:35-40` and `Web.Partner/Controllers/UserController.cs:28-39`: the handler
has **no** ownership check and the endpoint uses `[FromQuery] UserId`, not route `id`. The original D3
was broken in two ways (unreachable owner branch + fictional inner gate). **D3 is rewritten** (three
parts): (1) policy semantics = Admin OR owner-by-`sub`; (2) a `ResolveSubjectId` helper that reads
route `id`/`userId` **or** query `UserId`, fixing the route-key bug; (3) **add the actual ownership
check to `GetUser.Handler`** as explicit T-AUTHZ-3 scope, so the inner gate exists in code. The ADR no
longer cites a handler check that isn't there.

### On `CanViewPagedInvoices` (Challenger B #2, Challenger C C1) — CONCEDE + REVISE (BLOCKER)
Verified: on the Partner host this permission gates the employee's own invoice list/detail/download
(`EmployeePayrollController.cs:21,32,139`). `AdminOnly` would 403 every cleaner. **Remapped to
`EmployeeOrAdmin [OWN-DATA]`** (Note A), with mandatory handler scoping for the three invoice
handlers added to T-AUTHZ-2 scope. The alternative (split into two permissions) is recorded in
Alternatives and deferred. This is the concrete fix, not an assumption.

### On the pay-period justification (Challenger B #3) — CONCEDE + REVISE (BLOCKER on the prose)
Verified: `GetPagedPayPeriods.Handler:27-43` does no caller scoping. The "per-row scoped in handler"
prose was false. **Corrected (Note B):** pay periods are global cycles; the rows are **not** [OWN-DATA]
and carry no ownership-test obligation. The mapping (`EmployeeOrAdmin`) stays; only the (now truthful)
justification changed.

### On per-handler ad-hoc scoping (Challenger B #4) — CONCEDE + REVISE
Correct: `GetPeriodPays.cs:52-61` is the one verified check; safety must not rest on convention.
**Introduced the [OWN-DATA] marker** (D2 header + per-row) and **verification #6**: every [OWN-DATA]
row needs a real handler check **and** an ownership test; adding one without the test is a blocking
finding. This gives the gate teeth instead of "remember to check."

### On `AssertAllRegistered` placement + duplicate `AdminOnly` (Challenger A C6, Challenger B #5/#6, Challenger C C7/C8) — CONCEDE + REVISE (BLOCKER)
Verified: `CleansiaStartupBase.ConfigureServices` never registers authorization; each host calls
`AddJwt().AddUserAuthorization()` directly (`Admin/ServiceExtensions.cs:32-33`); `AddCoreBindings` is
registration-only; `AddJwt` registers a second `"AdminOnly"` at `:194`. **Revised D1.2/D4:** the
assertions run from a shared `IStartupFilter` registered by the new shared `AddCleansiaAuthorization`
(`AssertComplete` synchronously, `AssertAllRegistered` in the post-build `Configure` delegate where the
provider exists). `AddCleansiaAuthorization` is the **single** registration site; **the legacy `AddJwt`
`"AdminOnly"` is deleted** (grep confirmed **zero** `[Authorize(Policy="AdminOnly")]` consumers, so
this is safe). Registration assertion now verifies **semantics**, not just non-null (verification #2).

### On allow-list exhaustiveness + `Deny`/default interaction (Challenger B #7) — CONCEDE + REVISE
The draft allow-list already matched the commented anonymous entries in `PolicyBuilder.cs`, so it did
not actually omit one — REBUT the "omits at least one" specifics. But the **principle** is right:
**added verification #1b** (the allow-list must *equal* the set of `[AllowAnonymous]+[Permission]`
constants across hosts) and a contradiction check in `AssertComplete` (a permission can't be both
mapped and allow-listed). This makes the allow-list provably exhaustive, so `AssertComplete` cannot
brick a legitimate anonymous route.

### On host-blind map / per-host divergence (Challenger A C1, Challenger B #8) — CONCEDE + REVISE
Added the **map principle** (D2 header): a `Policy.*` is the same gate on every host; divergent intent
= two permissions. `CanViewPagedInvoices` is resolved by Note A under this principle. The **Partner vs
Customer audience asymmetry** (A C1) is no longer called "cosmetic": D5 §2 now states the frozen
accept/reject semantics (Partner pair = two trust zones, rejects across; Customer pair = one trust
zone, accepts across) and escalates "should both pairs use one model?" to Q-0003.

### On `CustomerOnly`-by-absence depending on the role claim (Challenger A C2) — REBUT + ESCALATE the edge
REBUT for today's code: every issued token carries `ClaimTypes.Role` from `user.Profile.ToString()`
(`AuthExtensions.cs:21`, the only issuance path; refresh re-emits via `SetClaims` at `RefreshToken.cs:109`),
and the role is copied to `ClaimTypes.Role` in every host's `OnTokenValidated`. So a validated token on
Admin/Partner always has the role claim and `CustomerOnly` correctly excludes Employee/Admin. **ESCALATE
the future risk:** external OAuth (IMP-1) is an unbuilt issuance path that could mint a principal without
a role claim. Recorded as Q-0004 — "IMP-1 OAuth tokens MUST carry the `role` claim (or `CustomerOnly`
must be made positive `RequireRole(Customer)` once a Customer role claim exists)." Not a blocker for this
ADR's code, but pinned so IMP-1 can't reopen it.

### On `ctx.Resource is HttpContext` availability (Challenger A C3) — REBUT + REVISE (defense-in-depth)
Verified: all hosts route through `UseAuthorization()` + `MapControllers` (`CleansiaStartupBase.Configure:130-144`);
no host uses `IAuthorizationService.AuthorizeAsync(user, resource, policy)` with a non-HttpContext
resource and there is no minimal-API surface — so `ctx.Resource` is always `HttpContext` on the live
path. Even so, D3 now fails **closed** (deny owner) if it ever isn't, and — critically — D3 part 3 adds
the **handler** ownership check, so an owner is still served via the handler on any non-MVC path. Added
the **positive** test (owner → 200) to verification #5 so a deny-all regression is caught.

### On cross-host replay within the Customer zone (Challenger A C4/C5, Challenger B #10) — CONCEDE + REVISE
Verified the forgery defense is sound (PROVEN, now stated in D5 §1). CONCEDE the under-statement:
D5 §2 now records "Customer pair = one trust zone" as an explicit decision (no `azp` discriminator; no
endpoint reads the client), and D5 §3 reconciles the tension — the refresh re-check binds **profile**,
not host, *within* the shared audience by design. Added verification #5 tests pinning **both** intended
behaviors (cross-customer-host refresh **succeeds**; cross-audience refresh **fails**; Partner-pair
refresh **fails**), so a future audience split is a conscious, test-breaking decision.

### On rollout ordering + big-bang (Challenger C C3) — CONCEDE + REVISE (BLOCKER, latent outage)
Verified the failure mode: AssertComplete before the map fills → every host fails to boot. **Added the
"Rollout ordering" subsection:** T-AUTHZ-1 and T-AUTHZ-2 ship as **one atomic PR** (deny default +
assertion + complete map together). This converts a self-inflicted fail-to-start into a clean
fail-closed cutover.

### On mid-wave serialization vs. snapshot (Challenger C C4) — CONCEDE + REVISE
**Added the reconciliation rule** (Consequences + verification #4/#9): additive rows update the
checked-in snapshot in the same PR (no ADR needed); semantic changes require a superseding ADR. The
snapshot failure message states this so developers amend rather than comment out the test — keeping the
pattern-evolution loop intact and not routing around enforcement.

### On the test harness (Challenger C C6, Challenger A C3 test concern, Challenger B #9) — CONCEDE + REVISE (BLOCKER on buildability)
Verified: `BaseIntegrationTest` runs handlers in-process via `AddCoreBindings`; no `WebApplicationFactory`/
`TestServer` exists. **Added D6 + T-AUTHZ-0:** a `WebApplicationFactory`-based `Cleansia.HostTests`
project is a prerequisite for the end-to-end #5 tests; until it lands, the **policy-layer** tests
(#1–#4, #6, via `IAuthorizationService.AuthorizeAsync`) are the buildable gating coverage. #5 is gated
on T-AUTHZ-0. Added the positive-path tests (#9's ask) to #5.

### On dispute-reply behavior (Challenger C C2) — REBUT (with verification added)
REBUT: dispute create/view/evidence are separate `CustomerOnly` permissions; **no non-admin endpoint
dispatches `CanRespondToDispute`** (the customer thread uses upload-evidence/view, not the admin
response path). The flip to `AdminOnly` does not break a customer flow. Recorded as Note C, and a future
customer follow-up message is anticipated as a **new** `CustomerOnly` permission, not a reuse — so we are
not trusting the comment blindly, we are matching it to behavior. (If T-AUTHZ-2 finds a non-admin caller
during implementation, that is a discovered new permission, handled by the same split — flagged as a
ticket checkpoint.)

### On in-flight session lockout at refresh tightening (Challenger C C10) — CONCEDE + REVISE
Added to the blast-radius note: per-host `RequiredProfile` will fail-refresh in-flight tokens whose
profile differs from the host's served profile (e.g. an admin on the partner host), forcing re-login.
Intended hardening; support should expect a small blip. Implementers verify no legitimate cross-profile
host usage before enabling per host (the audience mismatch already blocks most cases).

### Escalations filed to `questions/open.md`
- **Q-0001** — SuperAdmin tier (commented in `Policy.cs:150-154`, unenforceable today).
- **Q-0002** — per-host asymmetric signing keys (deferred isolation upgrade).
- **Q-0003** — unify the trust-zone model across pairs (Partner pair split vs. Customer pair merged):
  one model for both, or keep the asymmetry?
- **Q-0004** — IMP-1 OAuth tokens must carry the `role` claim, else `CustomerOnly`-by-absence
  mis-grants on every host.

## Verdict

**Adjudicated by the Lead (did not author).** Every load-bearing claim re-verified against the real
code (`PolicyBuilder.cs:204-205`, `PhysicalPolicy.cs`, Admin/Partner/Customer `ServiceExtensions.cs`,
`GetUser.cs:35-40`, `GetPeriodPays.cs:50-61`, `EmployeePayrollController.cs:20-145`, `RefreshToken.cs:61-98`,
`Customer`/`Mobile.Customer`/`Partner` `DisputeController.cs`, `AddDisputeMessage.cs:40-82`).

**Consensus: reached** — every challenge is RESOLVED, **with one lead correction (V1) folded into D2/Note
C/blast-radius/verification before acceptance.** The decision direction (D1 fail-closed, D3 owner-fix +
handler check, D4 single registration + duplicate-AdminOnly deletion, D5 trust zones + refresh re-check,
D6 host-test tiering) is sound and the deliberation trail is complete.

### Per-challenge ruling
- **D1 fail-closed + `AssertComplete` (A-C7, C-C5):** RESOLVED. Deny sentinel + startup assertion +
  runtime backstop is the right shape; the two distinct guarantees are now named. Verified `:204-205`.
- **D3 route-key bug + missing `GetUser` ownership check (B-1, C-C9):** RESOLVED. Verified
  `OwnerOrElevated` returns true for any Employee before the owner check and reads `RouteValues["id"]`
  while the endpoint supplies `[FromQuery] UserId`, and `GetUser.Handler` has zero ownership check. The
  three-part D3 fix (semantics + `ResolveSubjectId` + add handler check) is a real deliverable in
  T-AUTHZ-3 — confirmed in scope (Condition 1 below).
- **`CanViewPagedInvoices` host collision (B-2, C-C1):** RESOLVED. Verified the permission gates the
  cleaner's own invoice list/detail/download on the Partner host. `EmployeeOrAdmin [OWN-DATA]` with
  mandatory handler scoping (added to T-AUTHZ-2) is correct; the `AdminOnly` draft would have 403'd every
  cleaner.
- **Pay-period false justification (B-3):** RESOLVED. Verified `GetPagedPayPeriods.Handler` does no
  caller scoping; corrected to "global cycles, not [OWN-DATA]." Mapping unchanged, prose now truthful.
- **Per-handler ad-hoc scoping → needs teeth (B-4):** RESOLVED. `GetPeriodPays.cs:52-61` is the one
  verified check; the [OWN-DATA] marker + verification #6 (mandatory ownership test per marked row, with
  a "no handler check = blocking finding" rule) is the mechanical answer.
- **`AssertAllRegistered` placement + duplicate `AdminOnly` (A-C6, B-5, B-6, C-C7, C-C8):** RESOLVED.
  Verified `CleansiaStartupBase` never registers authz, each host calls `AddJwt().AddUserAuthorization()`,
  and `AddJwt:194` registers a second `"AdminOnly"`. The `IStartupFilter` placement, single
  `AddCleansiaAuthorization` site, deletion of the `AddJwt` `AdminOnly` (zero bare consumers), and the
  semantics assertion are correct.
- **Allow-list exhaustiveness + `Deny`/default (B-7):** RESOLVED. Verification #1b (allow-list must equal
  the `[AllowAnonymous]+[Permission]` set) + the contradiction check in `AssertComplete` make it provably
  exhaustive, so the assertion cannot brick a legitimate anonymous route.
- **Host-blind map / per-host divergence (A-C1, B-8):** RESOLVED. The map principle is the right
  invariant; `CanViewPagedInvoices` (Note A) and now `CanRespondToDispute` (V1) are resolved under it.
- **`CustomerOnly`-by-absence depends on the role claim (A-C2):** RESOLVED (REBUT for today + ESCALATE).
  Verified every issued token carries `ClaimTypes.Role`; Q-0004 pins the IMP-1 OAuth risk.
- **`ctx.Resource is HttpContext` (A-C3):** RESOLVED. MVC-only path verified; fail-closed-for-owner +
  the handler backstop + a positive owner→200 test cover the residual.
- **Cross-host replay within the Customer zone (A-C4, A-C5, B-10):** RESOLVED. Forgery defense is sound;
  "one customer trust zone" is now an explicit decision with both intended refresh behaviors pinned by
  tests; Q-0003 escalates the Partner/Customer asymmetry.
- **D5 Partner-pair audience asymmetry "cosmetic" (A-C1):** RESOLVED. Reframed as a frozen security
  contract (two trust zones, mutual rejection) + Q-0003. Verified the two partner hosts use
  `cleansia.partner` vs `cleansia.mobile`.
- **Rollout ordering / big-bang (C-C3):** RESOLVED. T-AUTHZ-1+2 atomic PR prevents the boot-failure
  window. Confirmed correct — shipping the assertion before the map fill would brick every host.
- **Mid-wave serialization vs. snapshot (C-C4):** RESOLVED. Additive-row-in-PR vs. semantic-change-needs-ADR
  reconciliation rule keeps the pattern-evolution loop intact.
- **Test harness buildability (C-C6, A-C3-test, B-9):** RESOLVED. Verified no `WebApplicationFactory`/
  `TestServer` exists; D6 + T-AUTHZ-0 + the two-tier split (policy-layer now, end-to-end gated) is the
  honest, buildable plan. Confirmed in scope (Condition 2 below).
- **In-flight session lockout (C-C10):** RESOLVED. Recorded in blast radius as intended hardening.

### V1 — LEAD CORRECTION (was a surviving BLOCKER in the author's defense): `CanRespondToDispute → AdminOnly` is WRONG
The author REBUTTED Challenger C's C2 with Note C: *"no non-admin endpoint dispatches
`CanRespondToDispute`."* **This REBUT is factually false and does not survive the code.** Verified:
`CanRespondToDispute` gates `AddMessage` on **three** hosts including the customer-facing
`Web.Customer/DisputeController.cs:52-62` and `Mobile.Customer/DisputeController.cs:52-62`. The shared
`AddDisputeMessage.Handler` is explicitly dual-purpose (`IsStaffMessage` flag; customer self-reply
allowed iff `dispute.UserId == caller` at lines 50-54; staff reply push-notifies at lines 65-78).
A flip to `AdminOnly` would have **403'd every customer replying to their own dispute** — the exact BSP-6
failure mode (trusting the `Policy.cs:103` comment over behavior) the ADR set out to kill. It also
violates the ADR's own map principle (one permission, two divergent host intents).

This was the *one* place the author's defense was a REBUT that should have been a CONCEDE. The lead has
**folded the fix in** rather than bounce the ADR for another round (the fix is mechanical and the
direction is settled): **SPLIT** into `CanAddDisputeMessage` (CustomerOnly [OWN-DATA], customer hosts)
and `CanRespondToDispute` (**AdminOnly** — owner-ratified Q-0005; staff path, Admin host), **plus** hardening
`AddDisputeMessage.Handler` to derive `IsStaffMessage` from the caller's profile (a customer must not
self-elevate a message to staff by flipping the body flag). D2 row, Note C, blast radius, the T-AUTHZ-2
scope, and verification #5 are updated accordingly. This is a **blocking precondition of T-AUTHZ-2**, not
a follow-up.

### Acceptance conditions (the two real implementation deliverables the author flagged)
Both are confirmed **in scope and blocking for their respective tickets**, not optional:
1. **The `GetUser.Handler` ownership check and the three Partner invoice-handler ownership checks**
   (D3 part 3 + Note A) are concrete code in T-AUTHZ-3 / T-AUTHZ-2 — an [OWN-DATA] row without its
   handler check is a blocking review finding (verification #6, #10).
2. **The `Cleansia.HostTests` `WebApplicationFactory` project (T-AUTHZ-0)** is a prerequisite for the
   end-to-end #5 tests. The policy-layer tier (#1–#4, #6) gates in the interim.

### Escalations to the owner (filed in `questions/open.md`, do NOT block acceptance)
- **Q-0001** SuperAdmin tier (`Policy.cs:150-154` comment, unenforceable today; family mapped `AdminOnly`).
- **Q-0002** per-host asymmetric signing keys (deferred isolation upgrade).
- **Q-0003** unify the trust-zone model across the Partner (split) and Customer (merged) pairs.
- **Q-0004** IMP-1 OAuth tokens MUST carry the `role` claim, else `CustomerOnly`-by-absence mis-grants.
- **Q-0005 — ✅ RESOLVED (owner, 2026-06-01): staff dispute replies are ADMIN-ONLY.** Staff
  `CanRespondToDispute` → **AdminOnly** (was the interim EmployeeOrAdmin). The SPLIT stays, so the
  customer self-reply (`CanAddDisputeMessage`/CustomerOnly) is unaffected. T-AUTHZ-2 additionally moves
  the staff-reply endpoint off the Partner host onto Admin. (Q-0001/0002/0003/0004 confirmed on their
  documented defaults.)

**Status moved to `accepted`.** This ADR is now the frozen permission map. Changes require a superseding
ADR (semantic mapping changes) or an in-PR snapshot update (purely additive rows), per the reconciliation
rule in Consequences.

---

## Owner ratification log
Changes accepted by the owner after this ADR reached panel consensus. These resolve parameters the ADR
explicitly left open pending an owner decision (not semantic reversals — so recorded here rather than via
a superseding ADR).

- **2026-06-01 — Q-0005 → staff dispute reply is ADMIN-ONLY.** `CanRespondToDispute` (staff path,
  `IsStaffMessage=true`) maps to **AdminOnly**; the staff-reply endpoint moves from the Partner host to
  the Admin host. The customer self-reply (`CanAddDisputeMessage`, CustomerOnly [OWN-DATA]) is unchanged.
  Verification #5 must now assert: an **Employee** against the staff-reply endpoint → **403** (previously
  the test expected 200 for Employee); customer self-reply on the Customer host → 200 (unchanged).
- **2026-06-01 — Q-0001/Q-0002/Q-0003/Q-0004 → confirmed on documented defaults** (no map change):
  AdminOnly for admin-user management (no SuperAdmin tier today); shared HS256 with audience isolation;
  current trust-zone asymmetry frozen; IMP-1 OAuth must preserve the `role` claim (constraint carried to
  the IMP-1 work, not a change here).

---

## Addendum A1 — Anonymous-facing catalog reads are platform config (T-0113 / LG-SEC-05)

- **Status:** accepted (architect panel ruling 2026-06-02; pending owner approval to schedule the build ticket)
- **Date:** 2026-06-02
- **Extends:** ADR-0001 D2 `[AllowAnonymous]` discipline + AnonymousAllowList. Additive — does not alter the frozen permission map.
- **Source:** finding LG-SEC-05, ticket T-0113. Panel: author + anonymous-route challenger + forward-compat challenger; lead ruling. All premises verified against real code.

### Context
`MembershipPlan` (`MembershipPlan.cs:24`) was `ITenantEntity`, so the EF global filter applied; its `GetPlans`
read is served `[AllowAnonymous]` (no `[Permission]`) on both customer hosts. With no JWT,
`TenantProvider.GetCurrentTenantId()` is null and the filter collapses to `TenantId == null`. No inbound
host/subdomain tenant-resolution middleware exists today (`SetTenantOverride` is background-service-only).
Failure modes: (1) anonymous marketing page returns only the null-tenant slice (wrong/empty) in multi-tenant
mode; (2) inverse footgun — a `TenantId==null` shared row leaks to every tenant; (3) write-side
`GetByCodeAsync` (`UserMembershipRepository.cs:39-44`) carries no tenant logic and relies on the filter, so an
anonymous-subscribe → tenant-overridden Stripe webhook (`StripeSubscriptionWebhookHandler.cs:145`) could
mismatch the plan under a per-tenant model. This is a **systemic class**: `Service`, `Package`, `Extra`,
`ServiceCity` are all `ITenantEntity` + `[AllowAnonymous]` and correct today only because single-tenant mode
makes the null-slice equal the only tenant. `Currency`/`Language`/`Country` are already platform config (not
`ITenantEntity`).

### Decision
- **D-A1.1 (doctrine).** An `[AllowAnonymous]` customer/mobile-host catalog read is EITHER (a) platform-wide
  config (not `ITenantEntity`, no global filter — the Currency/Language/Country model), OR (b) tenant-scoped
  behind a **spoof-resistant** inbound resolution mechanism (vetted proxy stamping a non-spoofable header /
  allow-listed host registry / SNI pinning — **never** the raw client `Host`/`X-Forwarded-Host`). Never
  tenant-scoped-and-anonymous with no resolution (S3). Since no (b)-infrastructure exists, anonymous catalogs
  are platform config.
- **D-A1.2.** `MembershipPlan` → path (a): drop `ITenantEntity`, drop `TenantId`, swap unique index
  `(TenantId, Code)` → `(Code)`. Closes all three failure modes. `GetPlans` stays bare `[AllowAnonymous]`
  (no `[Permission]`) → no `AnonymousAllowList` entry needed (the allow-list governs `[AllowAnonymous]+[Permission]` only).
- **D-A1.3.** `LoyaltyTierConfig` untouched — reachable only via `[Permission]`-gated Admin endpoints, no
  anonymous read path (verified). JWT always carries `tenant_id` there.
- **D-A1.4.** The four sibling catalogs (`Service`/`Package`/`Extra`/`ServiceCity`) are the same class and are
  **routed to BSP-9** (the anon tenant-scoped batch), which must apply D-A1.1. Cross-referenced so one doctrine
  governs both; T-0113 does NOT absorb their fix (scope discipline — avoids the double-fix collision). A
  consistency-scan rule flagging any `ITenantEntity` on an `[AllowAnonymous]` route is a follow-up tooling add.

### Forward-compatibility
Per-tenant plan catalogs are a future product feature (likelihood LOW). For `MembershipPlan` the return move is
symmetric and cheap: re-add `ITenantEntity` (auto-rearms the filter, no DbContext edit), swap the unique index
back (one migration, the inverse of T-0113's), and build the spoof-resistant resolution middleware — which is
**required regardless** the day ANY anonymous catalog goes per-tenant, so deferring it loses nothing.

> **Symmetric reversal holds for the zero-row `MembershipPlan` only** (corrected per the platform-expandability
> panel, 2026-06-02, file-verified). For the populated *sibling* catalogs (`Service`/`Package`/`Extra`/
> `ServiceCity`/`ServiceCategory` — handled in their own batch per D-A1.4, NOT here) the reverse is a
> **constrained `TenantId` backfill**, not a clean index swap (Service/Package have no unique index to restore;
> the FK from tenant-scoped `OrderService` makes re-slicing constrained) — see
> `agents/knowledge/platform-expandability.md` §7b/§8. The A1 ruling (Option A for `MembershipPlan`) is unchanged
> and confirmed: `MembershipPlan` is precisely the entity where the reversal IS clean.

### Implementation contract (for the T-0113 build ticket, on owner approval)
- `MembershipPlan.cs:24`: `: Auditable, ITenantEntity` → `: Auditable`; update `Code` XML doc ("unique per tenant" → "unique platform-wide").
- `MembershipPlanEntityConfiguration.cs:55-56`: `HasIndex(p => new { p.TenantId, p.Code }).IsUnique()` → `HasIndex(p => p.Code).IsUnique()` (mandatory — won't compile otherwise once the interface is dropped).
- No `CleansiaDbContext.cs` change (the filter loop auto-excludes once not `ITenantEntity`); no `GetMembershipPlans.cs` / `UserMembershipRepository.cs` change (they already query without tenant logic).
- **ef-migration (owner-only):** drops `TenantId` column + changes the unique index; **folds into the owner's regenerated Initial** (table is zero-row at launch) — no incremental migration. No NSwag change.
- **ACs:** AC2 read parity (anon == authenticated for the same plans), AC3 no null-tenant footgun, **AC6 write-side parity** (anonymous-subscribe → webhook resolves the same global plan code — the third bug the panel surfaced), AC5 host-boot integration test (needs the T-0100/T-AUTHZ-0 harness), plus a **mandatory structural anti-regression test** asserting `MembershipPlan` is not `ITenantEntity`. AC4: LoyaltyTierConfig untouched.
