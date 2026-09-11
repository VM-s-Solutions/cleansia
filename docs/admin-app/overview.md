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

The admin app has its own authentication system separate from the customer and partner apps. Admin users are created through the backend (e.g., via SQL scripts like `set-admin-role.sql`). Only users with the Administrator role can access the admin app.

The login page (`/login`) is implemented in `@cleansia/admin-features/admin-login` with an `AdminLoginFacade`.

::: warning
The admin app does not support self-registration. Admin accounts must be provisioned by existing administrators or via backend scripts.
:::

## Sidebar Navigation

The admin app uses a sidebar layout with the following sections (all protected by `adminGuard`):

| Route                     | Label        | Description                                   |
| ------------------------- | ------------ | --------------------------------------------- |
| `/employee-management`    | Employees    | Partner/employee management                   |
| `/order-management`       | Orders       | Order oversight and management                |
| `/invoice-management`     | Invoices     | Invoice management                            |
| `/pay-periods`            | Pay Periods  | Pay period management (open, close, paid)    |
| `/reports`                | Reports      | Revenue and payroll reports                   |
| `/service-management`     | Services     | Service configuration                         |
| `/package-management`     | Packages     | Package configuration                         |
| `/pay-config-management`  | Global Rates | Platform-wide pay rate defaults               |
| `/admin-user-management`  | Admin Users  | Admin account management                      |
| `/language-management`    | Languages    | Language configuration                        |
| `/country-management`     | Countries    | Country configuration                         |
| `/currency-management`    | Currencies   | Currency configuration                        |
| `/company-info`           | Company Info | Company details                               |
| `/template-management`    | Templates    | Email/notification templates                  |
| `/fiscal-failures`        | Fiscal Failures | Action queue for failed fiscal registrations (retry / acknowledge) |

The default route (`/`) redirects to `/employee-management`.

## Route Structure

```
/login                    # Admin login (guest guard)
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
/template-management      # Template CRUD (admin guard)
/fiscal-failures          # Failed fiscal registrations (admin guard)
/unauthorized             # Unauthorized access page
/not-found                # 404 page
```

## Feature Libraries

| Library                 | Import Path                                      | Description               |
| ----------------------- | ------------------------------------------------ | ------------------------- |
| `admin-login`           | `@cleansia/admin-features/admin-login`           | Admin authentication      |
| `employee-management`   | `@cleansia/admin-features/employee-management`   | Employee list + detail    |
| `order-management`      | `@cleansia/admin-features/order-management`      | Order list + detail       |
| `invoice-management`    | `@cleansia/admin-features/invoice-management`    | Invoice list + detail     |
| `pay-periods`           | `@cleansia.app/pay-periods`                      | Pay period management     |
| `reports`               | `@cleansia/admin-features/reports`               | Revenue & payroll reports |
| `service-management`    | `@cleansia/admin-features/service-management`    | Service CRUD              |
| `package-management`    | `@cleansia/admin-features/package-management`    | Package CRUD              |
| `pay-config-management` | `@cleansia/admin-features/pay-config-management` | Global rate CRUD          |
| `admin-user-management` | `@cleansia/admin-features/admin-user-management` | Admin user CRUD           |
| `language-management`   | `@cleansia/admin-features/language-management`   | Language CRUD             |
| `country-management`    | `@cleansia/admin-features/country-management`    | Country CRUD              |
| `currency-management`   | `@cleansia/admin-features/currency-management`   | Currency CRUD             |
| `company-management`    | `@cleansia/admin-features/company-management`    | Company info CRUD         |
| `template-management`   | `@cleansia/admin-features/template-management`   | Template CRUD             |
| `fiscal-failures`       | `@cleansia/admin-features/fiscal-failures`       | Fiscal failure action queue |

## Guards

| Guard        | Behavior                                                                                |
| ------------ | --------------------------------------------------------------------------------------- |
| `adminGuard` | Requires authenticated admin session with Administrator role; redirects to `/login`     |
| `guestGuard` | Prevents authenticated admins from accessing login; redirects to `/employee-management` |

## Admin Roles

Admin users have role-based access. The `adminGuard` verifies that the authenticated user has the `Administrator` role. Users without the correct role are redirected to `/unauthorized`.

## API Layer

All API calls use the `AdminClient` (NSwag-generated), which contains sub-clients:

- `adminEmployeeClient` -- Employee CRUD, approval/rejection
- `adminEmployeeDocumentClient` -- Document review, approve/reject, download
- `adminOrderClient` -- Order management, reassignment
- `adminInvoiceClient` -- Invoice management
- `adminReportClient` -- Revenue and payroll reports
- `adminPayConfigClient` -- Global rate CRUD + employee pay config summary + bulk grade apply
- `adminPayPeriodClient` -- Pay period CRUD (create, close, mark paid)
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

Per-employee pay overrides are managed on the Employee Detail page (see [User Management](./user-management)), not via Global Rates.

## Mobile Responsiveness

The admin app includes a mobile-optimized layout. On smaller screens, a **fixed mobile toolbar** is displayed with:

- A **hamburger menu button** that toggles the sidebar navigation
- A **centered brand name**
- A **language switcher**

This matches the partner app's mobile UX, providing a consistent experience across Cleansia applications.

## Centralized Language Switcher

The language switcher is centralized in the sidebar navigation. Individual page-level language switchers have been removed in favor of this single, consistent location across all admin pages.
