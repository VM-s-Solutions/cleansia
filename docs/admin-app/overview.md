# Admin App Overview

The **Admin App** (`cleansia-admin.app`) is the internal management dashboard for Cleansia administrators. It provides tools for managing employees, orders, invoices, services, and system configuration. It is a client-side only Angular application (no SSR).

## Purpose

Enable administrators to oversee all aspects of the Cleansia platform: approve partner applications, manage orders and disputes, configure services and packages, handle invoicing, and view business reports.

## Internal Login

The admin app has its own authentication system separate from the customer and partner apps. Admin users are created through the backend (e.g., via SQL scripts like `set-admin-role.sql`). Only users with the Administrator role can access the admin app.

The login page (`/login`) is implemented in `@cleansia/admin-features/admin-login` with an `AdminLoginFacade`.

::: warning
The admin app does not support self-registration. Admin accounts must be provisioned by existing administrators or via backend scripts.
:::

## Sidebar Navigation

The admin app uses a sidebar layout with the following sections (all protected by `adminGuard`):

| Route | Label | Description |
|---|---|---|
| `/employee-management` | Employees | Partner/employee management |
| `/order-management` | Orders | Order oversight and management |
| `/invoice-management` | Invoices | Invoice management |
| `/pay-periods` | Pay Periods | Pay period configuration |
| `/reports` | Reports | Revenue and payroll reports |
| `/service-management` | Services | Service configuration |
| `/package-management` | Packages | Package configuration |
| `/admin-user-management` | Admin Users | Admin account management |
| `/language-management` | Languages | Language configuration |
| `/country-management` | Countries | Country configuration |
| `/currency-management` | Currencies | Currency configuration |
| `/company-info` | Company Info | Company details |
| `/template-management` | Templates | Email/notification templates |

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
/admin-user-management    # Admin user CRUD (admin guard)
/language-management      # Language CRUD (admin guard)
/country-management       # Country CRUD (admin guard)
/currency-management      # Currency CRUD (admin guard)
/company-info             # Company info CRUD (admin guard)
/template-management      # Template CRUD (admin guard)
/unauthorized             # Unauthorized access page
/not-found                # 404 page
```

## Feature Libraries

| Library | Import Path | Description |
|---|---|---|
| `admin-login` | `@cleansia/admin-features/admin-login` | Admin authentication |
| `employee-management` | `@cleansia/admin-features/employee-management` | Employee list + detail |
| `order-management` | `@cleansia/admin-features/order-management` | Order list + detail |
| `invoice-management` | `@cleansia/admin-features/invoice-management` | Invoice list + detail |
| `pay-periods` | `@cleansia.app/pay-periods` | Pay period management |
| `reports` | `@cleansia/admin-features/reports` | Revenue & payroll reports |
| `service-management` | `@cleansia/admin-features/service-management` | Service CRUD |
| `package-management` | `@cleansia/admin-features/package-management` | Package CRUD |
| `admin-user-management` | `@cleansia/admin-features/admin-user-management` | Admin user CRUD |
| `language-management` | `@cleansia/admin-features/language-management` | Language CRUD |
| `country-management` | `@cleansia/admin-features/country-management` | Country CRUD |
| `currency-management` | `@cleansia/admin-features/currency-management` | Currency CRUD |
| `company-management` | `@cleansia/admin-features/company-management` | Company info CRUD |
| `template-management` | `@cleansia/admin-features/template-management` | Template CRUD |

## Guards

| Guard | Behavior |
|---|---|
| `adminGuard` | Requires authenticated admin session with Administrator role; redirects to `/login` |
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
- Various CRUD clients for services, packages, languages, countries, currencies, templates

## Configuration Management

The admin app provides CRUD interfaces for platform-wide configuration:

| Entity | Purpose |
|---|---|
| Services | Define cleaning service types with pricing |
| Packages | Define service packages with flat pricing |
| Languages | Supported platform languages |
| Countries | Supported countries for operations |
| Currencies | Supported payment currencies |
| Company Info | Company legal and contact details |
| Templates | Email and notification templates |

## Mobile Responsiveness

The admin app includes a mobile-optimized layout. On smaller screens, a **fixed mobile toolbar** is displayed with:

- A **hamburger menu button** that toggles the sidebar navigation
- A **centered brand name**
- A **language switcher**

This matches the partner app's mobile UX, providing a consistent experience across Cleansia applications.
