# Admin App Overview

The **Admin App** (`cleansia-admin.app`) is the internal management dashboard for Cleansia administrators. It provides tools for managing employees, orders, invoices, services, and system configuration. It is a client-side only Angular application (no SSR).

## Purpose

Enable administrators to oversee all aspects of the Cleansia platform: approve partner applications, manage orders and disputes, configure services and packages, handle invoicing, and view business reports.

## Access to the deployed console

Azure Static Web Apps requires an accepted Microsoft sign-in invitation with the custom role
`admin_console` before it serves the admin frontend. This is separate from the Cleansia admin
account and applies to deployed sites; the local Angular development server has no Azure gate.

Anonymous visitors are redirected to Microsoft sign-in (HTTP 401 → 302). A signed-in visitor
without the required role receives HTTP 403. A 403 must not redirect to sign-in: authenticating
again does not grant the missing role and would repeat the login loop. The platform's standard
forbidden page handles this case without needing the protected Angular app to load.

For DEV, check **Azure Portal → `swa-cleansia-admin-weu-dev` → Role management**. The Microsoft
identity used to sign in must have `admin_console` (with an underscore). If absent, create an
invitation using the Microsoft Entra ID provider and the DEV admin domain, then open and accept
its link with that identity. An invitation to the docs site or an Azure subscription role does
not grant access to this app.

After accepting the invitation, sign out of the site's Microsoft session at
`https://admin.dev.cleansia.cz/.auth/logout` and open the admin site again. To check the active
site session, visit `https://admin.dev.cleansia.cz/.auth/me` in the same browser: `clientPrincipal`
must exist and its `userRoles` must contain `admin_console`. A null principal means no site
session was established. If Microsoft itself refuses sign-in before returning to the site,
check the Microsoft account/tenant sign-in error; the app's 403 correction does not resolve
identity-provider failures.

If the browser reports CORS errors for `main-*.js`, `chunk-*.js`, or `polyfills-*.js` after a
redirect to `identity.*.azurestaticapps.net`, the script request has reached Microsoft sign-in
instead of receiving JavaScript. Check the same-domain `/.auth/me` session first. If its role
is correct, try the admin login in a new private window. When the private window works, close
the normal browser's admin tabs, clear site data for `admin.dev.cleansia.cz`, and sign in again.
This recovers stale or conflicting browser state without changing the app's access rules.
If a fresh private session also fails, investigate the original file response and site session;
do not make protected scripts public or add CORS headers to work around an authentication redirect.

See Microsoft's [route response codes](https://learn.microsoft.com/en-us/azure/static-web-apps/configuration#response-overrides)
and [authentication guide](https://learn.microsoft.com/en-us/azure/static-web-apps/authentication-authorization).

## Internal Login

The admin app has its own authentication system separate from the customer and partner apps. Admin users are created through the backend (e.g., via SQL scripts like `set-admin-role.sql`). Only users with the Administrator **profile** can sign in; what they can then open is decided by their **role** — see [Admin Roles](#admin-roles) below.

The login page (`/login`) is implemented in `@cleansia/admin-features/admin-login` with an `AdminLoginFacade`.

::: warning
The admin app does not support self-registration. Admin accounts must be provisioned by existing administrators or via backend scripts.
:::

## Sidebar Navigation

The admin app uses a sidebar layout with the following sections. Every route is behind `adminGuard` **and**
`permissionGuard`, every sidebar entry carries the permission of its area (`ADMIN_MENU_ITEMS` in
`apps/cleansia-admin.app/src/app/admin-menu.ts`), and an entry the signed-in role lacks is not shown — so
a Support sees no *Pay Periods*, *Invoices* or *Reports*, an Accountant no *Orders*, *Disputes* or
*Employee documents*, and neither sees *Admin Users*, *Legal documents*, *Company settings*, *Company
lifecycle* or *Marketing* (`admin-role-visibility.spec.ts` pins both lists):

| Route                     | Label        | Description                                   |
| ------------------------- | ------------ | --------------------------------------------- |
| `/notifications`          | Notifications | The admin feed — first in the sidebar, with the unread count as its badge (polled every 60 s while the tab is visible; on the mobile toolbar the same count sits on a bell beside the language switcher): newest first, 20 a page, one localised sentence per event, mark read on click and navigate to the order / dispute / data-protection / company-lifecycle page it names, mark all read; entry gated by `CanViewAdminNotifications` (ADR-0065) |
| `/employee-management`    | Employees    | Partner/employee management                   |
| `/order-management`       | Orders       | Order oversight and management                |
| `/invoice-management`     | Invoices     | Invoice management                            |
| `/pay-periods`            | Pay Periods  | Pay period management (open, close, paid)    |
| `/reports`                | Reports      | Revenue and payroll reports                   |
| `/service-management`     | Services     | Service configuration                         |
| `/package-management`     | Packages     | Package configuration                         |
| `/pay-config-management`  | Global Rates | Platform-wide pay rate defaults               |
| `/admin-user-management`  | Admin Users  | Administrator accounts and their roles — the list shows each account's role, the create form carries a role select (default *Support*), and the detail has a role picker under `CanSetAdminRole` (Administrator only; disabled on one's own row; the server's *last Administrator* refusal renders in five locales); entry gated by `CanViewAdminUsers` (Manager or above) |
| `/language-management`    | Languages    | Language configuration                        |
| `/country-management`     | Countries    | Country configuration                         |
| `/currency-management`    | Currencies   | Currency configuration                        |
| `/company-info`           | Company Info | Company details                               |
| `/company-settings`       | Company settings | The admin's own operating company's overrides of fifteen catalogued platform settings (thirteen retention settings, the chargeback horizon, and the administrator notification mailbox); entry gated by `CanViewTenantConfigurations` |
| `/company-lifecycle`      | Company lifecycle | The admin's own operating company's state (operating, winding down, deactivated, frozen, archived) with every stamp and actor, the sixteen settlement facts with links to the lists that settle them and the date the archive becomes admissible, and the four acts — Deactivate, Reactivate, Wind down from a date, Archive — each behind a confirmation; entry gated by `CanViewCompanyLifecycle` (ADR-0064) |
| `/template-management`    | Templates    | Email/notification templates                  |
| `/fiscal-failures`        | Fiscal Failures | Action queue for failed fiscal registrations (retry / acknowledge) |

The default route (`/`) redirects to `/employee-management` when the role can open it — every role can
today — and otherwise to the first sidebar page the role can open (`resolveLandingRoute`), so no role
lands on a page it lacks.

## Route Structure

```
/login                    # Admin login (guest guard)
/notifications            # The admin feed — paged list, mark read, mark all read (admin guard)
/employee-management      # Employee list (admin guard)
/employee-management/:id  # Employee detail (admin guard)
/order-management         # Order list (admin guard)
/order-management/:id     # Order detail (admin guard)
/invoice-management       # Invoice list (admin guard)
/invoice-management/:id   # Invoice detail (admin guard)
/pay-periods              # Pay period management (admin guard)
/reports                  # Revenue & payroll reports (admin guard)
/service-management       # Service CRUD (admin guard)
/package-management       # Package CRUD (admin guard)
/pay-config-management    # Global rate management (admin guard)
/admin-user-management    # Admin user CRUD (admin guard)
/language-management      # Language CRUD (admin guard)
/country-management       # Country CRUD (admin guard)
/currency-management      # Currency CRUD (admin guard)
/company-info             # Company info CRUD (admin guard)
/company-settings         # Company settings — catalogued per-company overrides (admin guard)
/company-lifecycle        # Company lifecycle — state, settlement facts, the four acts (admin guard)
/template-management      # Template CRUD (admin guard)
/fiscal-failures          # Failed fiscal registrations (admin guard)
/unauthorized             # Unauthorized access page
/not-found                # 404 page
```

"admin guard" above means both guards: `adminGuard` (a signed-in Administrator-profile session) and
`permissionGuard` (the route's `data.permission` — its area's *view* policy, e.g. `CanViewPagedOrderAdmin`
on `/order-management`, `CanViewPagedInvoicesAdmin` on `/invoice-management`, `CanViewCompanyLifecycle` on
`/company-lifecycle`). `/customers/:id` is a route with no sidebar entry (`CanViewOrderCustomer`), reached
from an order, the audit log or a notification. `admin-role-surface.spec.ts` walks the route table and the
sidebar array and fails an entry that carries no permission.

## Feature Libraries

| Library                 | Import Path                                      | Description               |
| ----------------------- | ------------------------------------------------ | ------------------------- |
| `admin-login`           | `@cleansia/admin-features/admin-login`           | Admin authentication      |
| `notifications`         | `@cleansia/admin-features/notifications`         | The admin feed page: rows built from `UserNotificationDto` — title and body from `pages.notifications.events.<key>.*` (the crew-lost row picks `body_under_way` when the status at the loss was `OnTheWay`/`InProgress`; `cause`, day and instant args are localised client-side), the deep link per event family (order/payment → order detail, dispute → dispute detail, erasure → data protection, company → company lifecycle), unread emphasis, mark read on click, mark all read up to the newest row's `createdOn` plus one millisecond (the column is microsecond-resolution); `ADMIN_NOTIFICATION_EVENT_KEYS` mirrors the backend catalogue and `admin-notification-copy.spec.ts` walks the C# file so a key added on the server fails the build here without its five-locale sentence |
| `employee-management`   | `@cleansia/admin-features/employee-management`   | Employee list + detail    |
| `order-management`      | `@cleansia/admin-features/order-management`      | Order list + detail. The detail's crew list says per cleaner *accepted {date}, v{version}* or *contract pending* (a `workContractAcceptances` entry whose `orderEmployeeId` is that seat's `id`, ADR-0068), and **Read** opens `components/admin-work-contract-dialog` — `getWorkContract(acceptanceId, uiLanguage)`, the facts and acceptance rows from the shared `@cleansia/utils` helper, `[innerHTML]` through the sanitizer, and the accepted text row's SHA-256 read through `adminLegalClient.getDocument` only for a session holding `CanViewLegalDocuments` (the other roles see a line saying so) |
| `invoice-management`    | `@cleansia/admin-features/invoice-management`    | Invoice list + detail     |
| `pay-periods`           | `@cleansia/admin-features/pay-periods`           | Pay period management     |
| `reports`               | `@cleansia/admin-features/reports`               | Revenue & payroll reports |
| `service-management`    | `@cleansia/admin-features/service-management`    | Service CRUD              |
| `package-management`    | `@cleansia/admin-features/package-management`    | Package CRUD              |
| `pay-config-management` | `@cleansia/admin-features/pay-config-management` | Global rate CRUD          |
| `admin-user-management` | `@cleansia/admin-features/admin-user-management` | Administrator accounts: the list with a role column, the create form with a role select (`DEFAULT_ADMIN_ROLE = Support`), the detail's role picker (`AdminUserClient.role(userId, SetAdminRoleCommand)`, shown under `CanSetAdminRole`, disabled on one's own row with `api.admin_user.cannot_change_own_role`, the server's `api.admin_user.cannot_demote_last_administrator` rendered on refusal), deactivate / activate |
| `language-management`   | `@cleansia/admin-features/language-management`   | Language CRUD             |
| `country-management`    | `@cleansia/admin-features/country-management`    | Country CRUD              |
| `currency-management`   | `@cleansia/admin-features/currency-management`   | Currency CRUD             |
| `company-management`    | `@cleansia/admin-features/company-management`    | Company info CRUD         |
| `company-settings`      | `@cleansia/admin-features/company-settings`      | One row per `TenantSettingCatalog` key — description, category, range, default, the value in force, override or default — edited inline with the typed input its value type calls for (number field / checkbox / e-mail field for the `Email` type, which shows *every administrator* while unset and refuses a malformed address client-side with the server's own `api.tenant_setting.invalid_value` sentence), reset behind a confirmation; facade in signals, `switchMap` loads; Edit/Reset gated by `CanUpdate` / `CanDeleteTenantConfiguration`; `tenant-setting-catalogue.spec.ts` ties the five locales to the backend catalogue (three categories: `retention`, `lifecycle`, `notifications`) |
| `company-lifecycle`     | `@cleansia/admin-features/company-lifecycle`     | The state banner (stamps, actor e-mails, the manifest hash once archived), the settlement-facts table (counts linking to orders / pay periods / invoices / disputes, the horizon row linking to Company settings), and the four acts behind confirmations — *Run wind-down again* once a date is set, *Build archive again* once frozen — each gated by its policy and by the state table with the server's own refusal sentence (`api.company.*`, `api.tenant.archived`) as the reason line; the wind-down dialog's date picker is floored at today; the facade holds the DTO, the act in flight and the dialog in signals, `switchMap` loads, one act at a time, re-reads after every act; specs pin the fact table and the act matrix per state against the generated DTO, the one-hour run staleness, the wire bodies and the confirm sentences; a copy spec ties the five locales' state and fact names to the generated client |
| `template-management`   | `@cleansia/admin-features/template-management`   | Template CRUD             |
| `fiscal-failures`       | `@cleansia/admin-features/fiscal-failures`       | Fiscal failure action queue |
| `legal-documents`       | `@cleansia/admin-features/legal-documents`       | Read-only list of every legal-text version per audience, type and market, with a per-language preview and hash (`/legal-documents`, `CanViewLegalDocuments` — Administrator only since ADR-0066; a new version is a seed file + deploy — ADR-0063). `TYPE_LABEL_KEYS` is an exhaustive `Record<LegalDocumentType, string>` — the `work_contract` label landed with the regenerated client (ADR-0068) |
| `audit-log`             | `@cleansia/admin-features/audit-log`             | The admin log and the customer trail; an admin row shows the actor's **role** beside the actor and the list filters by it (`actorAdminRole`) |

## Guards

| Guard             | Behavior                                                                                |
| ----------------- | --------------------------------------------------------------------------------------- |
| `adminGuard`      | Requires a signed-in session whose stored `role` is the Administrator **profile** (an Employee `role` — a stale session — lands on `/unauthorized`); not signed in redirects to `/login` |
| `permissionGuard` | Reads `route.data.permission` and sends a session whose role lacks it to `/unauthorized`; a route naming no permission passes, which is why the surface spec makes every route name one |
| `guestGuard`      | Prevents authenticated admins from accessing login; redirects to `/`, which resolves to the role's landing route |

## Admin Roles

Every administrator account carries one of four roles — **Administrator**, **Manager**, **Support**,
**Accountant** (ADR-0066). The login and refresh responses carry it as `adminRole` beside `role`;
`AdminAuthService.setSession` stores both in localStorage (the JWT is HttpOnly), and the refresh path
re-runs `setSession`, so a role changed on the server reaches the session at the next refresh — within
fifteen minutes — without a re-login. `PermissionService.hasPolicy(policy)` resolves the policy through
`POLICY_MAP` (the hand-kept mirror of the server's `PolicyBuilder.Map`) to a physical policy and, for
one of the four administrator sets (`AdministratorOnly`, `ManagerOrAbove`, `SupportOrAbove`,
`AccountantOrAbove`), answers `role === Administrator && ADMIN_ROLE_SETS[set].includes(adminRole)`;
`AdminOnly` is any administrator. `policy-map-mirror.spec.ts` reads `PolicyBuilder.cs`,
`PhysicalPolicy.cs`, `AdminRoleSets.cs` and `AdminRole.cs` out of the backend tree and fails the build
on a row, a set or a role name the two sides disagree on.

**The server is the gate; the app hides what the role lacks.** A hidden entry, a guarded route and a
`*cleansiaPermission` button are a courtesy — every admin-host route resolves the same map and 403s a role
outside its set, so nothing the app shows or hides widens what the server allows. The hint over-shows on a
policy the mirror does not know (`Authenticated` fallback). Who sees what, area by area, is on the
[AdminRoleGate](/domain/roles/admin-role-gate) card; where the server gates is under
[Security rules — Administrator roles](/architecture/security-rules#administrator-roles-adr-0066-accepted-2026-09-19).

## API Layer

All API calls use the `AdminClient` (NSwag-generated), which contains sub-clients:

- `adminEmployeeClient` -- Employee CRUD, approval/rejection
- `adminEmployeeDocumentClient` -- Document review, approve/reject, download
- `adminOrderClient` -- Order management, reassignment
- `adminInvoiceClient` -- Invoice management
- `adminReportClient` -- Revenue and payroll reports
- `adminPayConfigClient` -- Global rate CRUD + employee pay config summary + bulk grade apply
- `adminPayPeriodClient` -- Pay period CRUD (create, close, mark paid)
- `adminUserClient` -- Administrator accounts: `getPaged`, `details(userId)`, `create(command)` (the command carries `role`), `update`, `deactivate`, `activate`, and `role(userId, SetAdminRoleCommand)` against `POST api/AdminUser/{userId}/role` (`CanSetAdminRole`; `AdminUserListItem` / `AdminUserDetailDto` carry `adminRole`)
- `adminNotificationClient` -- `getPaged(...)`, `unreadCount()`, `markRead(command)`, `markAllRead(command)` against `api/AdminNotification` (`GET get-paged`, `GET unread-count`, `POST mark-read`, `POST mark-all-read`; every route under `CanViewAdminNotifications`, the audience enriched to `Admin` server-side, a mark-read writes no admin audit row) — the bell and the notifications page
- `adminTenantSettingsClient` -- `getAll()`, `set(command)`, `reset(key)` against `api/AdminTenantSettings` — the company settings page
- `adminCompanyLifecycleClient` -- `get()`, `deactivate()`, `reactivate()`, `windDown(command)`, `archive()` against `api/AdminCompanyLifecycle` — the company lifecycle page; `CompanyLifecycleDto` and `CompanyLifecycleState` are generated from the backend
- Various CRUD clients for services, packages, languages, countries, currencies, templates

## Configuration Management

The admin app provides CRUD interfaces for platform-wide configuration:

| Entity       | Purpose                                                 |
| ------------ | ------------------------------------------------------- |
| Services     | Define cleaning service types with pricing              |
| Packages     | Define service packages with flat pricing               |
| Global Rates | Platform-wide pay rate defaults per service/package     |
| Languages    | Supported platform languages                            |
| Countries    | Supported countries for operations                      |
| Currencies   | Supported payment currencies                            |
| Company Info | Company legal and contact details                       |
| Templates    | Email and notification templates                        |
| Legal documents | Every version of the terms and the privacy policy, read-only (ADR-0063) |

Per-employee pay overrides are managed on the Employee Detail page (see [User Management](./user-management)), not via Global Rates.

### The market forms (ADR-0058, ADR-0059, ADR-0060)

Three of those forms author what a customer's **market** shows:

- **Membership plans** — the plan form renders **one price block per currency the platform knows**
  (active currencies badged *Active*, inactive ones *Optional*), each with a price for one billing
  period and the Stripe Price id that charges it. Every block is optional: a block is sent only when
  both fields are filled, a half-filled block is a per-block error, and a currency the plan is not
  priced in stays blank on populate (never `0`). The list shows the platform-default-currency price
  with its code and prints "—" when the plan has none. Refusals rendered: `currency.not_found`,
  `membership.plan.stripe_price_already_used`.
- **Currencies** — `No-show apology credit` beside the loyalty divisor: the amount `CancelUnfilledOrders`
  pays on an order in that currency; blank means none is paid.
- **Countries** — the `Two-letter code` (required on create, pattern-checked on edit; the market chip
  prints it) and a **Market** section with `Insurance coverage per booking`, disabled with a hint until
  the country has a configuration row. The Market section saves through its own PUT after the country
  update (`country.configuration_missing` if the row is missing). On the Service Area page the serviced
  toggle snaps back off with `country.market_not_ready` when the country's configured currency is not
  active.
- **The default market** (owner ruling 2026-09-13) — the country a customer surface pre-selects before
  any choice is made is the one configuration flagged `IsDefaultMarket` (CZE today; at most one, by
  the database). The action is `PUT api/AdminCountry/{countryId}/default-market`
  (`AdminCountryClient.defaultMarket(countryId)` on the regenerated client; permission
  `CanUpdateCountry`): promoting another country moves the flag, promoting the current one is a no-op,
  and a country that is not serviced or whose currency is not active is refused
  (`country.not_serviced`, `country.market_not_ready`); a lost race answers
  `country.default_market_changed_concurrently`. The country detail and list rows carry
  `isDefaultMarket`. **No form control drives the action at the time of writing** — the flag rides
  the generated client's DTOs, but the admin web country form and list neither display it nor offer
  a button; until one lands, the client method or the API is the way to move it.

→ [API — markets and memberships](/api/markets-and-memberships)

## Mobile Responsiveness

The admin app includes a mobile-optimized layout. On smaller screens, a **fixed mobile toolbar** is displayed with:

- A **hamburger menu button** that toggles the sidebar navigation
- A **centered brand name**
- A **language switcher**

This matches the partner app's mobile UX, providing a consistent experience across Cleansia applications.

## Centralized Language Switcher

The language switcher is centralized in the sidebar navigation. Individual page-level language switchers have been removed in favor of this single, consistent location across all admin pages.
