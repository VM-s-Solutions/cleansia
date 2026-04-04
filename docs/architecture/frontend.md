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
| Sentry | 10.40 | Error tracking |
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
```

::: info
Each generator produces a TypeScript client class (e.g., `PartnerClient`, `AdminClient`, `CustomerClient`) containing sub-clients for each API controller. After generation, formatter scripts clean up the output.
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
  apiBaseUrl: 'http://localhost:5003',
  apiProtocol: 'http',
  isDevelopment: true,
  blobStorageUrl: 'http://127.0.0.1:10000/devstoreaccount1',
  googleClientId: '...',
  sentryDsn: '',
  bugReportUrl: '',
};
```

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

Each app configures its own interceptor chain:

```typescript
provideHttpClient(
  withFetch(),
  withInterceptors([
    ...COMMON_INTERCEPTORS_FN,      // Shared (e.g., language header)
    ...CUSTOMER_INTERCEPTORS_FN,    // App-specific (e.g., JWT auth)
  ])
)
```

## Testing

- **Unit tests**: Jest with `jest-preset-angular`
- **E2E tests**: Playwright (configured via Nx plugin)

```bash
nx test <project-name>       # Unit tests
nx e2e <project-name>-e2e   # E2E tests
```
