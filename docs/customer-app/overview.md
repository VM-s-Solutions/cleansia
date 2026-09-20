# Customer App Overview

The **Customer App** (`apps/cleansia.app`, Nx project `cleansia.app`) is the public-facing application
where customers choose a market, browse the catalogue, book, pay, track orders, run disputes, hold a
Cleansia Plus membership and collect rewards. It is the only app in the monorepo that supports
**Server-Side Rendering (SSR)**, and the only one with anonymous surfaces.

## Purpose

Provide the booking experience for cleaning services to both signed-in customers and guests: a guest
can browse, quote, book end to end and track an order by number + e-mail; an account adds order
history, saved addresses, disputes, Plus, recurring schedules and rewards.

## Key Features

| Feature | Description |
| --- | --- |
| Home page | Landing page: hero, the quick quote with its market chip, the catalogue strips, "What you get", "How it works", the rules card (the market's apology credit formatted from `noShowCredit`, or the refund-only sentence), FAQ, the Plus teaser and CTA |
| Market | A market selector (navbar pill, mobile-menu row, footer — drawn only with two or more markets) and a "CZ · CZK" chip beside the quick quote (a control with two or more markets, a static label with one). The choice lives in **one cookie**, `preferred_market` (ISO alpha-3, one year, `SameSite=Lax`), so SSR and the browser resolve the same market; every pre-address surface sends its `countryId` |
| Services catalogue | `/services` — services, packages and extras priced in the chosen market, labelled from each payload's `currencyCode` |
| Order wizard | `/order` — services, address, date/time, Plus step, payment, review. Public: a guest books end to end. From the address step on the address's country decides the price; the Plus step keeps the chosen market |
| Checkout | Stripe card payments or cash; `/checkout/success` and `/checkout/cancel` are the two URLs Stripe returns to |
| Order tracking | `/track-order` — anonymous lookup by order number + e-mail |
| My Orders | `/orders`, `/orders/:orderId` — the customer’s history across operators, detail and rebook (auth); each row and detail show the order’s country and currency. The detail states *Contract for work accepted by {given name} on {date}, version {version}* per crew member with an acceptance, and **Read the contract** opens `/orders/:orderId/contract/:acceptanceId` — the accepted text with the job facts frozen at acceptance and the acceptance details (`GET api/Order/GetWorkContract`; another customer's acceptance id renders the error state, ADR-0068) |
| Disputes | `/disputes` — file and follow a dispute (auth) |
| Cleansia Plus | `/plus` — the one public Plus page: benefits and the plans priced in the chosen market for everyone, the management panel on top for a member (`/membership` redirects here; `/membership/welcome` is the post-purchase page) |
| Recurring bookings | `/membership/recurring`, `…/create`, `…/:id` — a member's schedules (create and edit gated by `customerMembershipGuard`; list, pause and delete are not) |
| Rewards | `/rewards`, `/rewards/activity` — points, tiers and the tier floor line (shown only when the market's currency is the platform default), referral code |
| Profile | `/profile` (account, language, notification preferences), `/saved-addresses` |
| Authentication | `/login`, `/register`, `/r/:code` (referral landing), `/confirm-email` (6-digit code), `/forgot-password`; e-mail + password, Google and Apple sign-in (buttons hidden when the client id is not configured) |
| Legal | `/terms`, `/privacy` and `/work-contract` — the stored document in force for the chosen market and the UI language, fetched from `GET api/Legal/GetDocument` and rendered with its title, effective date and version (`yyyy-MM-dd`; the currency code filled in from the market — ADR-0063). `/work-contract` is the contract for work every booking is concluded under (ADR-0068): the wizard's confirm step names it in a sentence beneath the consent block, unconditionally, and the footer links it beside the other two; `/gdpr` (cookie consent and data requests) |

## Orders across markets

One account can book in any serviced market with an active operator. The market selector chooses
the browsing context; the service address chooses the booking’s operator and currency. An order
placed with another operating company stays in the customer’s history, with owner checks on the
detail, cancellation, receipt, photos and dispute paths. Loyalty, credit and membership usage remain
with the account.

The list and detail show a market label in all five languages. `OrderMarketFacade` resolves the
order’s `countryId` against `selectMarkets`, the full directory, and displays the order’s own
currency code. Switching the browsing market does not relabel an existing order. If that country
is absent from the directory, the label says “Market unavailable” and keeps the order currency; it
does not guess from the selected market.

→ [Booking and pricing](/flows/booking-and-pricing) · [Tenancy — cross-market booking](/decisions/adr-0061#d6-tenant-country-and-currency-agree-by-construction-and-two-validators-refuse-the-cases-that-could-break-it)

## SSR {#ssr}

The app uses `@angular/ssr`. Key files under `apps/cleansia.app/src/`:

- `main.server.ts` — server bootstrap
- `app/app.config.server.ts` — server providers
- `app/app.routes.server.ts` — the render mode per route: `''`, `services`, `terms`, `privacy` and
  `not-found` are `RenderMode.Server` (rendered on request — they need API data for SEO); everything
  else is `RenderMode.Client`. The legal pages are `Server`, not `Prerender`, because prerendering
  needs the builder's `outputMode`, without which the engine answers a Prerender route with a 404.

**The server sees only the request.** Two cookies decide what it renders: the language cookie and the
market cookie `preferred_market`. `initializeMarket` (an `APP_INITIALIZER`) runs on both branches —
the server reads the cookie header, the browser reads `document.cookie` — resolves the same value
against the same `Market/GetOverview` list and issues the same catalogue URLs, so the first client
render equals the server's. It never throws: a failed list is the no-market shape (no chip, no
selector, no `countryId` sent, nothing persisted, retried on the next navigation), not a failed
render.

**What the transfer cache serves, and what it never does.** `provideClientHydration(withEventReplay())`
transfers the server's HTTP responses into the document, and Angular skips any request sent
`withCredentials`. `CustomerAuthInterceptorFn` sends credentials only on a state-changing method and
on a call made with a session, so:

- an **anonymous own-API GET** — `Market/GetOverview`, the `Service|Package|Extra/GetOverview`
  strips, `Membership/GetPlans`, `Country/GetPropertySizes`, `Country/GetServiced`,
  `Legal/GetDocument` — is fetched once on the server and **served from the document on bootstrap**;
  the browser does not refetch it;
- a **session-bearing GET** (the same catalogue call for a signed-in customer, `GetMine`, orders,
  profile) carries the cookie, is **never transferred** and is re-fetched by the browser. That is the
  property that keeps one user's response out of another's document — the reason the cache was off
  between 2026-08-28 and the 20.3.25/20.3.27 fixes.

::: warning
All browser API access (`localStorage`, `window`, `document`) is wrapped with `isPlatformBrowser()`
checks. The market preference is the exception that proves the rule: it lives in a cookie **and
nothing else**, because a second store the server cannot read is a hydration mismatch waiting for a
cookie expiry ([ADR-0058](/decisions/adr-0058) D3).
:::

## Route Structure

`app/app.routes.ts`, top to bottom (order is behaviour — the literal `orders/lookup` redirects sit
above the guarded `orders` route so they keep winning the match):

```
/                              Home (public, SSR)
/services                      Services catalogue (public, SSR)
/plus                          Cleansia Plus — public page + member management panel
/login                         Login (customerGuestGuard)
/register                      Registration (customerGuestGuard)
/r/:code                       Referral landing — registration with the code pre-applied (customerGuestGuard)
/confirm-email                 E-mail confirmation
/forgot-password               Password reset (customerGuestGuard)
/track-order                   Anonymous order tracking (public)
/order                         Order wizard (public — Order/CreateOrder is [AllowAnonymous])
/orders/lookup                 → redirect /track-order (pathMatch full)
/orders/lookup/:orderId        → redirect /track-order (pathMatch full)
/orders                        My orders (customerAuthGuard)
/orders/:orderId/contract/:acceptanceId   The accepted contract for work (customerAuthGuard; declared above the detail route)
/orders/:orderId               Order detail (customerAuthGuard)
/profile                       Profile (customerAuthGuard)
/saved-addresses               Saved addresses (customerAuthGuard)
/disputes                      Disputes (customerAuthGuard)
/rewards                       Rewards (customerAuthGuard)
/rewards/activity              Rewards activity (customerAuthGuard)
/membership                    → redirect /plus (customerAuthGuard on the parent)
/membership/subscribe          → redirect /plus
/membership/welcome            Post-purchase page (Stripe Checkout's success URL)
/membership/recurring          Recurring bookings list
/membership/recurring/create   Create schedule (customerMembershipGuard)
/membership/recurring/:id      Edit schedule (customerMembershipGuard)
/checkout                      → redirect / (a namespace, not a page)
/checkout/success              Payment success
/checkout/cancel               Payment cancelled
/gdpr                          Cookie consent / data requests
/terms                         Terms of service (SSR)
/privacy                       Privacy policy (SSR)
/work-contract                 The contract for work (SSR)
/not-found                     The customer app's own 404 (SSR)
/**                            → redirect /not-found
```

Route path constants come from `CleansiaCustomerRoute` in `@cleansia/services`; `checkout`, `terms`,
`privacy` and `work-contract` are literal strings in the routes file.

## Feature Libraries

Sixteen Nx libraries under `libs/cleansia-customer-features/`, one per feature, each exporting its
routes from `src/lib/lib.routes.ts`:

| Library | Import path | What it owns |
| --- | --- | --- |
| `home` | `@cleansia-customer/home` | Landing page: quick quote + market chip, catalogue strips, rules card, FAQ, Plus teaser |
| `services-catalog` | `@cleansia-customer/services-catalog` | `/services` |
| `plus` | `@cleansia-customer/plus` | `/plus` — the public Plus page and the member panel |
| `order-wizard` | `@cleansia-customer/order-wizard` | `/order` — the booking flow, quote, Plus step, payment |
| `checkout` | `@cleansia-customer/checkout` | `/checkout/success`, `/checkout/cancel` |
| `orders` | `@cleansia-customer/orders` | `/orders`, `/orders/:orderId`, `/orders/:orderId/contract/:acceptanceId` (the `work-contract-page` — facade + signals, `getWorkContract(acceptanceId, uiLanguage)`, the facts table, the acceptance facts, `[innerHTML]` through the sanitizer, an error state with retry), the `TrackOrderComponent` behind `/track-order` |
| `disputes` | `@cleansia-customer/disputes` | `/disputes` |
| `profile` | `@cleansia-customer/profile` | `/profile`, `/saved-addresses`, and the `/membership/*` routes (welcome page, recurring mount, the two redirects to `/plus`) |
| `recurring-bookings` | `@cleansia-customer/recurring-bookings` | The schedules list and the create/edit wizard, mounted under `/membership/recurring` |
| `rewards` | `@cleansia-customer/rewards` | `/rewards`, `/rewards/activity` |
| `login` | `@cleansia-customer/login` | `/login` |
| `register` | `@cleansia-customer/register` | `/register` and the `/r/:code` referral landing |
| `confirm-email` | `@cleansia-customer/confirm-email` | `/confirm-email` |
| `forgot-password` | `@cleansia-customer/forgot-password` | `/forgot-password` |
| `gdpr` | `@cleansia-customer/gdpr` | `/gdpr` |
| `legal-pages` | `@cleansia-customer/legal-pages` | `/terms`, `/privacy` and `/work-contract` — a fetch-and-render of the stored document in force (`[innerHTML]` through the sanitizer; the copy lives in the seed files, not the locale JSON); one two-line component per type over the shared `legal-document` component |

The 404 (`CustomerNotFoundComponent`) lives in the app itself, under `app/components/not-found/`,
wrapping the shared component with this app's ways out of it.

## Guards

All three live in `@cleansia/customer-services` (`libs/core/customer-services/src/lib/guards/`) and
return `true` on the server branch — a guard that redirects during SSR would render the login page
for every crawler.

| Guard | Behaviour |
| --- | --- |
| `customerAuthGuard` | Redirects an unauthenticated visitor to `/login` |
| `customerGuestGuard` | Redirects a signed-in customer away from login/register to `/orders` |
| `customerMembershipGuard` | Refuses the recurring create/edit screens without an active membership — mirrors the server's `recurring_booking.membership_required`, and deliberately does **not** guard the list, pause or delete, so a lapsed member keeps control of the schedules they have |

## State Management

NgRx with `customerReducers` / `customerEffects` from `@cleansia/customer-stores`
(`libs/data-access/customer-stores/`), registered with strict immutability checks. Six slices:

- **`customerUser`** — the current user profile.
- **`customerLoading`** — the global loading flag the interceptor chain drives.
- **`customerCatalog`** — services, packages and extras for the current market.
- **`customerOrder`** — order list and detail.
- **`customerDispute`** — the customer's disputes.
- **`customerMarket`** — `markets[]` from `Market/GetOverview`, `selectedIsoCode`, `loadFailed`.
  Resolved by the `initializeMarket` `APP_INITIALIZER` on both branches (request cookie on the server,
  `document.cookie` in the browser), persisted only by the browser branch, never throws, retried on
  the next `NavigationEnd` while `loadFailed`. Selectors: `selectMarkets`, `selectMarket`,
  `selectMarketCountryId` (null when unresolved — every reader then sends no `countryId`),
  `selectMarketCurrencyCode`, `selectMarketNoShowCredit`, `selectMarketInsuranceCoverageAmount`,
  `selectMarketLoadFailed`, `selectHasMarketChoice` (two or more markets — what draws the selector).

**The market cookie.** `preferred_market` holds the ISO alpha-3 code of the chosen market
(`path=/; max-age=31536000; SameSite=Lax`), written by the market switcher and by `initializeMarket`
after a successful resolution, read by both branches. A stored code is only ever **compared** against
the list — a delisted or junk value falls to the `isDefault` market (the configuration flagged
`IsDefaultMarket`, CZE today), then the first listed market, and is overwritten; the text is never
rendered or sent. The code, not the id, because the DEV reseed re-mints ids.

Feature-level state is in signal-based facades (`OrderWizardFacade`, `LoginFacade`,
`MembershipFacade`, …) that extend `UnsubscribeControlDirective`; components hold no business logic.

## API Layer

Every call goes through `CustomerClient` (`@cleansia/customer-services`, NSwag-generated — never
hand-edited; regenerate with `npm run generate-customer-client`), which exposes one sub-client per
controller: `authClient`, `userClient`, `orderClient`, `addressSearchClient`, `countryClient`,
`currencyClient`, `languageClient`, `packageClient`, `serviceClient`, `extraClient`, `paymentClient`,
`gdprClient`, `consentsClient`, `disputeClient`, `savedAddressClient`, `loyaltyClient`,
`marketClient`, `creditClient`, `promoCodeClient`, `referralClient`, `membershipClient`,
`notificationPreferencesClient`, `recurringBookingClient` and `apiClient`.

The base URL is the `CUSTOMER_API_BASE_URL` token, pointing at the `Cleansia.Web.Customer` host
(`:5003` locally). The interceptor chain is composed in `app/http-interceptors.ts` — common, then
customer-services (auth: CSRF echo and `withCredentials` where the cookie is needed; the error
interceptor resolving backend keys under `api.*`), then the store interceptors — and its order is
pinned by `http-interceptors.spec.ts`.

→ [Authentication](/customer-app/authentication) · [Business rules — the market](/product/business-rules#market)
· [API — markets and memberships](/api/markets-and-memberships)
