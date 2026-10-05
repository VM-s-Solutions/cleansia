# Frontend Architecture

The Cleansia frontend is an **Nx monorepo** containing three Angular 19 applications and a set of shared libraries. All apps share a common design system, API client layer, and state management infrastructure.

## Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| Angular | 19.2 | Core framework |
| Nx | 21.2 | Monorepo tooling, build orchestration |
| NgRx | 19.2 | State management (Store + Effects) |
| PrimeNG | 19.1 | UI component library |
| PrimeFlex | 4.0 | Utility CSS framework |
| Chart.js / ng2-charts | 4.5 / 8.0 | Dashboard charts and analytics |
| ngx-translate | 16.0 | i18n (cs, en, sk, uk, ru) |
| Sentry | 10.40 | Error tracking — **dormant**, see below |
| Lucide Angular | 0.525 | Icon library |
| Bootstrap | 5.3 | Grid utilities |
| Stripe | (via redirect) | Payment processing |

## Monorepo Structure

```
src/Cleansia.App/
├── apps/
│   ├── cleansia.app/              # Customer-facing app (SSR)
│   ├── cleansia-partner.app/      # Partner/employee portal
│   └── cleansia-admin.app/        # Internal admin dashboard
├── libs/
│   ├── cleansia/                  # Base library
│   ├── cleansia-customer-features/  # Customer feature modules
│   ├── cleansia-partner-features/   # Partner feature modules
│   ├── cleansia-admin-features/     # Admin feature modules
│   ├── core/                      # API client services
│   │   ├── admin-services/        # Admin API clients (NSwag)
│   │   ├── customer-services/     # Customer API clients (NSwag)
│   │   ├── partner-services/      # Partner API clients (NSwag)
│   │   └── services/              # Shared services
│   ├── data-access/               # NgRx stores
│   │   ├── admin-stores/
│   │   ├── customer-stores/
│   │   └── partner-stores/
│   └── shared/                    # Shared UI components & utilities
│       ├── assets/                # Themes, i18n files
│       ├── charts/                # Chart components
│       ├── components/            # @cleansia/components
│       ├── directives/            # @cleansia/directives
│       ├── models/                # @cleansia/models
│       ├── pipes/                 # @cleansia/pipes
│       ├── types/                 # @cleansia/types
│       └── utils/                 # @cleansia/utils
├── nx.json
├── tsconfig.base.json
└── package.json
```

## Shared Libraries

### `@cleansia/components`

Reusable UI components used across all three apps:

- `CleansiaButtonComponent` -- Styled button with loading states
- `CleansiaTextInputComponent` -- Form input with validation display
- `CleansiaTitleComponent` -- Page title component
- `CleansiaDynamicBackgroundComponent` -- Animated background for auth pages
- `CleansiaBrandNameComponent` -- Logo/brand display
- `CleansiaScrollTopComponent` -- Scroll-to-top button
- `CleansiaTelephoneComponent` -- Phone number input
- `CleansiaSectionComponent` -- Card-like content sections
- `CleansiaNotFoundComponent` -- 404 page (shared across all apps)
- `CleansiaCheckboxComponent` -- Styled checkbox

### `@cleansia/services`

Shared services:

- `SnackbarService` -- Toast notifications (success, error, translated variants)
- `GuestOrderService` -- localStorage-based guest order tracking
- `DialogService` -- Confirmation dialogs with translation support
- `JsonTranslationLoader` -- Custom ngx-translate loader with SSR support
- Route constants (`CleansiaCustomerRoute`, `CleansiaPartnerRoute`, `CommonRoute`)

### `@cleansia/directives`

- `UnsubscribeControlDirective` -- Base class providing `destroyed$` Observable for automatic RxJS cleanup

### `@cleansia/models`

- `OrderFilter` -- Shared order filtering model used across apps

## NSwag Client Generation

API clients are auto-generated from the backend Swagger/OpenAPI specs using NSwag. Each backend API has its own NSwag configuration:

```bash
# Generate TypeScript clients from running backend
npm run generate-partner-client    # nswag-partner.json
npm run generate-admin-client      # nswag-admin.json
npm run generate-customer-client   # nswag-customer.json

npm run generate-clients           # all three, then ONE typecheck
```

::: info
Each generator produces a TypeScript client class (e.g., `PartnerClient`, `AdminClient`, `CustomerClient`) containing sub-clients for each API controller. After generation, formatter scripts clean up the output and `npm run typecheck` compiles every app against the regenerated client — NSwag emits a nullable DTO member as a *required* interface key, so a new backend field breaks existing call sites, and the guard names them before anything is pushed (ADR-0031). It is a typecheck, not a build: still run the three production builds before pushing.
:::

The generated clients are injected via Angular DI with a base URL token:

```typescript
{ provide: CUSTOMER_API_BASE_URL, useValue: environment.apiBaseUrl }
```

## Build Scripts

```bash
# Development
npm run start:cleansia          # Customer app (dev server)
npm run start:cleansia-partner  # Partner app (dev server)
npm run start:cleansia-admin    # Admin app (dev server)

# Production builds
npm run build:cleansia-customer   # Customer app (production)
npm run build:cleansia-partner    # Partner app (production)
npm run build:cleansia-admin      # Admin app (production)

# SSR
npm run start:cleansia-ssr        # Build + run SSR server
```

## Environment Configuration

Each app has three environment files:

```
apps/<app>/src/environments/
├── environment.ts          # Local development
├── environment.staging.ts  # Staging
└── environment.prod.ts     # Production
```

**Environment properties:**

```typescript
export const environment = {
  apiHost: 'localhost',
  apiPort: '5003',
  apiBaseUrl: '', // dev is RELATIVE on purpose — see note below
  apiProtocol: 'http',
  isDevelopment: true,
  blobStorageUrl: 'http://127.0.0.1:10000/devstoreaccount1',
  googleClientId: '...',
  sentryDsn: '',
  bugReportUrl: '',
};
```

::: warning Browser-side Sentry is not collecting anywhere
The **admin** and **partner** apps call `Sentry.init` only when `environment.sentryDsn` is non-empty
(`apps/cleansia-admin.app/src/main.ts:4`, `apps/cleansia-partner.app/src/main.ts:4`), and every
committed environment file — `environment.ts`, `environment.staging.ts`, `environment.prod.ts`, for
all three apps — sets `sentryDsn: ''`. The **customer** app carries the property but has no Sentry
initialization at all.

So a browser exception today is reported to nothing. **Server-side this is no longer true** — the five
APIs export exceptions and error logs to Application Insights (T-0500), so the gap is now browser-only.
See [Infrastructure → Observability](/architecture/infrastructure#observability).
:::

::: warning Dev `apiBaseUrl` is relative on purpose
Auth is an HttpOnly cookie with `SameSite=Strict`, so the browser must see one origin. In dev the
Angular dev server proxies `/api` server-side (`apps/<app>/proxy.conf.json` → local API;
`nx serve <app> --configuration=devremote` → `proxy.devremote.conf.json` → the deployed dev API).
Do not put an absolute API URL back into a dev `environment.ts` — that reintroduces the cross-site
cookie 401. Staging/prod keep absolute URLs. Details: `src/Cleansia.App/CLAUDE.md`.
:::

## SSR Setup

The **customer app** supports Server-Side Rendering via `@angular/ssr`:

- `main.server.ts` -- Server entry point
- `app.config.server.ts` -- Server-specific providers
- `app.routes.server.ts` -- Server route configuration

::: warning SSR Considerations
All components that access browser APIs (`localStorage`, `window`, `document`) must use the `isPlatformBrowser` guard:

```typescript
private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

ngOnInit() {
  if (this.isBrowser) {
    localStorage.getItem('key');
  }
}
```
:::

**What the transfer cache serves.** `provideClientHydration()` transfers the server's HTTP responses
into the document, and Angular skips any request sent `withCredentials`. The customer app's
`CustomerAuthInterceptorFn` therefore sends credentials only on state-changing methods and on calls
made with a session: an **anonymous own-API GET** (the market directory, the catalogue overviews, the
plans, the property sizes, the serviced countries) is fetched once on the server and reused on
bootstrap; a **session-bearing GET** carries the cookie, is never transferred and is re-fetched by the
browser. → [Customer app overview — SSR](/customer-app/overview#ssr)

The partner and admin apps are client-side only (no SSR).

## State Management

NgRx is used for global state management in all three apps. Each app has its own store configuration:

```typescript
// Customer app
StoreModule.forRoot(customerReducers),
EffectsModule.forRoot(customerEffects),

// Partner app
StoreModule.forRoot(partnerReducers),
EffectsModule.forRoot(partnerEffects),
```

In addition, feature-level state is managed with Angular signals inside **Facade** services:

```typescript
@Injectable()
export class OrderWizardFacade {
  activeStep = signal(0);
  formData = signal<OrderWizardFormData>({...});
  submitting = signal(false);
  totalPrice = computed(() => { /* ... */ });
}
```

::: tip Architecture Pattern
Each feature module follows the pattern: **Component + Facade + Models**. The Facade encapsulates all business logic and API calls, keeping components thin.
:::

## Theming

PrimeNG is configured with a custom `CleansiaPreset` theme:

```typescript
providePrimeNG({
  theme: {
    preset: CleansiaPreset,
    options: { darkModeSelector: '.dark-mode' },
  },
});
```

### A blue slab on the customer site takes the card's ground as its ink {#heading-slab-ink}

A picked chip or choice, and an initials disc, on the customer site is a slab of `--cl-heading`, the
heading blue: sky-700 `#0369a1` in light mode and sky-300 `#7dd3fc` in dark. Its label takes
`--cl-surface`, the card's own ground, which flips with the slab: white on sky-700 in light mode, and
`#0f1b2d` on sky-300 in dark. Both read 5.93:1 in light mode and 10.37:1 in dark, measured from the
compiled customer stylesheet in Chromium. A literal white reads 1.67:1 on the dark slab, which is what
each of these drew until 2026-10-05:

| Element | Rule |
|---|---|
| The schedule form's picked pick and picked chips, and the Plus badge on the schedules list | `cl-rec__pick--on`, `cl-rec__chip--on`, `cl-rec__gate-badge` → [the services step](/customer-app/ordering-flow#step-0-services-packages) |
| The dispute form's picked reason | `.cl-dsp__reason--on` |
| Profile's picked theme, its icon included | `.customer-profile__segment--on` |
| Rewards' picked activity filter | `.cl-rwd__chip--on` |
| Orders' picked filter | `.customer-orders__chip--on` |
| The initials on Profile | `.customer-profile__user-avatar` |
| A cleaner's initials on an order | `.order-detail__cleaner-avatar` |
| The signed-in initials in the top bar, and in the drawer | `.customer-navbar__avatar` |

`customer-heading-slab.spec.ts`, in the assets project beside the stylesheets, reads every customer
page stylesheet and the customer navbar's, finds each rule that paints its background with
`--cl-heading` and sets an ink, and fails unless that ink is `--cl-surface`.

### Blue text on the customer site takes `--cl-accent-text` {#accent-text}

The brand primary, sky-600 `#0284c7`, reads 4.10:1 on white, under the 4.5:1 text needs, and where a
stylesheet uses the fixed SCSS `$primary` it stays sky-600 in dark mode too, 4.22:1 on the dark card.
Since 2026-10-05 (finding 2026-10-05) blue **text** on the customer site takes `--cl-accent-text`:
sky-700 `#0369a1` in light mode and sky-300 `#7dd3fc` in dark. The token lives in the customer palette
in `_home-design.scss`, beside `--cl-accent`; until then it was the booking wizard's own. Fills,
borders, icons and buttons keep the primary, as on the apps
([Blue text is sky-700](/mobile-app/patterns#brand-text-ink)). Measured from the compiled customer
stylesheet in Chromium, light / dark:

| Text | Rule | Before | After |
|---|---|---|---|
| The booking's package card price, the booking's total | `.cl-wiz__pack-price`, `.cl-wiz__total` | 4.10 / 4.22 | 5.93 / 10.37 |
| Home's *from* price on a service | `.cl-services__price` | 4.10 / 4.22 | 5.93 / 10.37 |
| The hero quote's amount | `.cl-quote__price strong`, `.cl-quote__amount` | 4.10 / 8.07 | 5.93 / 10.37 |
| The price on a catalogue package, the Plus page, an order's total, a schedule's summary, a Rewards tier's discount, the tracking page's total | `.cl-cat-pkg__price`, `.cl-plusp__price`, `.order-detail__total-amount`, `.cl-rec__summary-price`, `.cl-rwd__tier-discount`, `.cl-trk__total` | 4.10 / 8.07 | 5.93 / 10.37 |
| A legal page's section number | `.cl-lgl__num` (was `--cl-accent-soft`) | 2.06 / 9.10 | 5.71 / 9.10 |
| The top bar's active link, and a link on hover | `cleansia-customer-navbar.component.scss` | 3.94 light | 5.71 light (dark has its own rule) |
| The sign-in link on hover | `cleansia-customer-navbar.component.scss` | 3.94 / 3.70 | 5.71 / 9.10 |
| The user's role in the account menu | `cleansia-customer-navbar.component.scss` | 4.10 light | 5.93 light (dark has its own rule) |

The rules that used `--cl-accent` (sky-400 after dark, 8.07:1) now read sky-300 there, a lighter blue
that both pass. Left in the primary on purpose: the inverse and Plus call-to-action buttons
(`.cl-btn--inverse`, `.cl-plus__cta`), every icon, and the schedule form's add-address row on hover,
whose resting ink is already sky-700, so the same token would leave no hover cue. PrimeNG's text
buttons and links take the shared preset's sky-600, and that preset is shared with the partner and
admin sites, so they are not part of this. `customer-accent-text.spec.ts`, in the assets project,
compiles the customer stylesheet and requires `var(--cl-accent-text)` on each site above, and the token
in both themes.

## i18n

Translation is handled by `ngx-translate` with a custom `JsonTranslationLoader` that supports SSR. Supported locales: `cs` (Czech), `en`, `sk`, `uk`, `ru`. Locale data is registered at app initialization:

```typescript
registerLocaleData(localeCs);
registerLocaleData(localeEn);
registerLocaleData(localeSk);
registerLocaleData(localeUk);
registerLocaleData(localeRu);
```

## HTTP Interceptors

Each app composes the shared chain with its own:

```typescript
provideHttpClient(
  withFetch(),
  withInterceptors([
    ...COMMON_INTERCEPTORS_FN,      // libs/core/services — shared by ALL THREE apps
    ...CUSTOMER_INTERCEPTORS_FN,    // libs/core/customer-services — auth, error, loading
  ])
)
```

| Chain | Members | Source |
|---|---|---|
| `COMMON_INTERCEPTORS_FN` | `ContentDispositionInterceptorFn`, `HttpErrorInterceptorFn`, `RetryAfterInterceptorFn` | `libs/core/services/src/lib/interceptors/index.ts` |
| `PARTNER_INTERCEPTORS_FN` | `AuthInterceptorFn`, `PartnerErrorInterceptorFn`, `LoadingInterceptorFn` | `libs/core/partner-services/…` |
| `ADMIN_INTERCEPTORS_FN` | `AuthInterceptorFn`, `AdminErrorInterceptorFn`, `LoadingInterceptorFn` | `libs/core/admin-services/…` |
| `CUSTOMER_INTERCEPTORS_FN` | `CustomerAuthInterceptorFn`, `CustomerErrorInterceptorFn`, `CustomerLoadingInterceptorFn` | `libs/core/customer-services/…` |

Ordering inside `COMMON_INTERCEPTORS_FN` is deliberate: `RetryAfterInterceptorFn` sits **after**
`HttpErrorInterceptorFn` so a `429` is retried once with back-off before any error snackbar fires.

### Backend error keys resolve under `api.*`

`HttpErrorInterceptorFn` fires for every non-404/403 error response, takes the first value out of the
ProblemDetails `errors` bag, and resolves it as `` `api.${dotValue}` ``:

```typescript
const candidateKey = `api.${String(errorKey)}`;
const message = translate.instant(candidateKey);
// ngx-translate echoes the key back when it has no translation — never let a raw
// machine key reach the snackbar; fall back to the generic message.
return message === candidateKey ? translate.instant('api.common.error_occurred') : message;
```

So every `BusinessErrorMessage` value (`order.not_takeable`, `employee.not_approved`, …) needs a
translation at `api.<same.dotted.key>` in all five locale files of every app that can reach the
endpoint. A key placed anywhere else silently renders *"An error occurred. Please try again."*

**`api.*` is the only namespace, in all three apps.** Admin carried a second, legacy `errors.*` block
until 2026-08-13; it was **97 % redundant** — 164 of its 169 keys already existed under `api.*` — so it
was deleted rather than migrated. Its retirement is now asserted, not merely documented: a locale that
reacquires an `errors` block, or an admin source that references one, fails
`error-contract-parity.spec.ts`. The per-feature `XXX_ERROR_KEY_MAP` resolvers survive, but every value
in them is an `api.*` key.

Deleting the block also closed a live gap it had been hiding: `refund.failed` is a real
`BusinessErrorMessage` that had **no** `api.*` translation at all, so any admin who hit it through the
shared interceptor saw the generic message.

The namespace is pinned by `apps/<app>/src/app/i18n/error-contract-parity.spec.ts`, which parses
`BusinessErrorMessage.cs` directly and asserts locale-set equality.

## Testing

- **Unit tests**: Jest with `jest-preset-angular`
- **E2E tests**: Playwright (configured via Nx plugin)

```bash
nx test <project-name>       # Unit tests
nx e2e <project-name>-e2e   # E2E tests
```
