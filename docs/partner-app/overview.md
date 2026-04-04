# Partner App Overview

The **Partner App** (`cleansia-partner.app`) is the portal for cleaning partners (employees) to manage their assigned orders, view earnings, handle invoices, and maintain their profile. It is a client-side only Angular application (no SSR).

## Purpose

Provide cleaning partners with tools to find and manage cleaning jobs, track their work progress, upload completion photos, and manage their earnings and invoices.

## Key Features

| Feature | Description |
|---|---|
| Dashboard | Stat cards, earnings charts, order distribution, productivity metrics |
| Available Orders | Browse unassigned orders and take them |
| My Orders | View assigned orders, start/complete work, upload photos |
| Order Detail | Full order info, Take/Start/Complete flow, Report Issue, Add Note |
| Invoices | View pay period invoices, download PDFs |
| Profile | Manage personal info, availability, documents |
| Registration | Create account, email confirmation, profile completion |

## Sidebar Navigation

The partner app uses a sidebar layout with the following navigation items (all protected by `authGuard`):

| Route | Label | Description |
|---|---|---|
| `/orders` | Orders | Available and My Orders tabs |
| `/dashboard` | Dashboard | Analytics and stats |
| `/invoices` | Invoices | Pay period invoices |
| `/profile` | Profile | Account settings |

The home route (`/`) redirects to `/orders`.

## Route Structure

```
/login                # Partner login (guest guard)
/register             # Partner registration (guest guard)
/confirm-email        # Email confirmation (guest guard)
/forgot-password      # Password reset (guest guard)
/dashboard            # Dashboard with analytics (auth guard)
/orders               # Order list with Available/My tabs (auth guard)
/orders/:id           # Order detail page (auth guard)
/invoices             # Invoice list (auth guard)
/invoices/:id         # Invoice detail (auth guard)
/profile              # Profile management (auth guard)
/not-found            # 404 page
```

## Feature Libraries

| Library | Import Path | Description |
|---|---|---|
| `login` | `@cleansia-partner/login` | Partner authentication |
| `register` | `@cleansia-partner/register` | Account creation |
| `confirm-email` | `@cleansia-partner/confirm-email` | Email verification |
| `forgot-password` | `@cleansia-partner/forgot-password` | Password reset |
| `dashboard` | `@cleansia-partner/dashboard` | Analytics dashboard |
| `orders` | `@cleansia-partner/orders` | Order management |
| `invoices` | `@cleansia-partner/invoices` | Invoice management |
| `profile` | `@cleansia-partner/profile` | Profile settings |

## Guards

| Guard | Behavior |
|---|---|
| `authGuard` | Requires authenticated partner session; redirects to `/login` |
| `guestGuard` | Prevents authenticated partners from accessing login/register; redirects to `/orders` |

## Registration Lock

::: warning
New partner registrations go through an admin approval process. After registration and email confirmation, partners must complete their profile and upload required documents. They cannot access order management until an admin approves their account (sets `contractStatus` to `Approved`).
:::

## API Layer

All API calls use the `PartnerClient` (NSwag-generated), which contains sub-clients:

- `employeeClient` -- Current employee profile, availability
- `orderClient` -- Order CRUD, take/start/complete, photos, notes
- `employeePayrollClient` -- Invoices, pay period data

## State Management

The partner app uses NgRx with `partnerReducers` and `partnerEffects` for:

- Order list state (paged, filtered, sorted)
- Dashboard stats and analytics
- Order completion actions

Feature-level state uses signal-based Facades (e.g., `OrdersFacade`, `DashboardFacade`, `InvoicesFacade`).

## Alignment with Android App

The partner web app and Android app share the same API endpoints and order lifecycle. The Take/Start/Complete flow, photo management, and order statuses are consistent across both platforms. The Android app provides the same functionality for mobile users.
