# Customer App Overview

The **Customer App** (`cleansia.app`) is the public-facing application where customers browse cleaning services, place orders, make payments, and track their order status. It is the only app in the monorepo that supports **Server-Side Rendering (SSR)**.

## Purpose

Provide a seamless booking experience for cleaning services, allowing both authenticated users and guests to place and track orders.

## Key Features

| Feature          | Description                                                                                                                                                                                                                 |
| ---------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Home page        | Landing page with "What you get with our service" benefits section (Less worries, More time, Professional approach, Clean home), "How it works" (6 booking-flow steps), FAQ (6 questions), "Why Choose Us" section, and CTA |
| Services catalog | Browse available cleaning services and packages                                                                                                                                                                             |
| Order wizard     | Multi-step booking flow (services, address, date/time, payment, review)                                                                                                                                                     |
| Checkout         | Stripe card payments or cash-on-delivery                                                                                                                                                                                    |
| Order tracking   | Anonymous order lookup by order number + email                                                                                                                                                                              |
| My Orders        | Authenticated order history with detail view and rebook                                                                                                                                                                     |
| Disputes         | Submit and track order disputes                                                                                                                                                                                             |
| Profile          | Manage account details (address form uses reactive FormGroup with country dropdown and validation)                                                                                                                          |
| Authentication   | Email/password login, Google OAuth, registration, email confirmation (6-digit code input)                                                                                                                                   |
| GDPR             | Cookie consent and data management                                                                                                                                                                                          |

## SSR

The customer app uses `@angular/ssr` for server-side rendering. Key SSR files:

- `main.server.ts` -- Server bootstrap
- `app.config.server.ts` -- Server providers
- `app.routes.server.ts` -- Server route rendering strategy

::: warning
All browser API access (`localStorage`, `window`, `sessionStorage`) is wrapped with `isPlatformBrowser()` checks throughout the customer app features.
:::

## Route Structure

```
/                     # Home page
/services             # Services catalog
/login                # Login (guest-only guard)
/register             # Registration (guest-only guard)
/confirm-email        # Email confirmation
/forgot-password      # Password reset (guest-only guard)
/order                # Order wizard
/orders               # My orders list (auth guard)
/orders/:id           # Order detail (auth guard)
/track-order          # Anonymous order tracking (public)
/checkout/success     # Payment success page
/checkout/cancel      # Payment cancelled page
/profile              # User profile (auth guard)
/disputes             # Dispute management (auth guard)
/gdpr                 # GDPR / cookie management
/terms                # Terms of service
/privacy              # Privacy policy
/not-found            # 404 page
```

## Feature Libraries

Each feature is a separate Nx library under `libs/cleansia-customer-features/`:

| Library            | Import Path                           | Description                  |
| ------------------ | ------------------------------------- | ---------------------------- |
| `home`             | `@cleansia-customer/home`             | Landing page                 |
| `services-catalog` | `@cleansia-customer/services-catalog` | Service browsing             |
| `login`            | `@cleansia-customer/login`            | Authentication               |
| `register`         | `@cleansia-customer/register`         | Account creation             |
| `confirm-email`    | `@cleansia-customer/confirm-email`    | Email verification           |
| `forgot-password`  | `@cleansia-customer/forgot-password`  | Password reset               |
| `order-wizard`     | `@cleansia-customer/order-wizard`     | Booking flow                 |
| `orders`           | `@cleansia-customer/orders`           | Order list, detail, tracking |
| `checkout`         | `@cleansia-customer/checkout`         | Payment result pages         |
| `profile`          | `@cleansia-customer/profile`          | Account management           |
| `disputes`         | `@cleansia-customer/disputes`         | Dispute management           |
| `gdpr`             | `@cleansia-customer/gdpr`             | Cookie/data consent          |
| `legal-pages`      | `@cleansia-customer/legal-pages`      | Terms & privacy              |

## Guards

| Guard                | Behavior                                    |
| -------------------- | ------------------------------------------- |
| `customerAuthGuard`  | Redirects unauthenticated users to `/login` |
| `customerGuestGuard` | Redirects authenticated users to `/orders`  |

## State Management

The customer app uses NgRx with `customerReducers` and `customerEffects`:

- **Customer user store** -- Current user profile
- **Customer services store** -- Available services list
- **Customer packages store** -- Available packages list

Feature-level state is managed via signal-based Facades (e.g., `OrderWizardFacade`, `LoginFacade`).

## API Layer

All API calls go through `CustomerClient` (NSwag-generated), which contains sub-clients:

- `userClient` -- User profile operations
- `orderClient` -- Order CRUD and lookup
- `paymentClient` -- Stripe payment session creation
- `authClient` -- Login, register, Google OAuth

The base URL is provided via the `CUSTOMER_API_BASE_URL` injection token, which points to the `Cleansia.Web.Customer` backend.
