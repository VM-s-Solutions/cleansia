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

The customer configuration uses the NSwag templates in `tools/nswag/customer`. JSON operations
request text and decode it into the generated DTOs; text survives Angular's JSON transfer-state
serialization. The map image and receipt download remain Blob responses. Body extraction accepts
text or a native Blob, using `Blob.text()` so the same generated client runs in the browser and Node.
Regenerate through `generate-customer-client`; do not edit the generated transport by hand.

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

The transferred customer JSON response body is text, and the client parses it after retrieval from
the cache. Transferring a Blob would serialize its body as an empty object. The transport does not
change the credential rules or cache inclusion policy above.

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

PrimeNG is configured with a custom `CleansiaPreset` theme (`libs/shared/assets/src/lib/cleansia-preset.ts`),
which all three apps read:

```typescript
providePrimeNG({
  theme: {
    preset: CleansiaPreset,
    options: { darkModeSelector: '.dark-mode' }, // the customer site; partner and admin pass false
  },
});
```

The customer site switches to its dark theme with the `.dark-mode` class. The partner and admin sites
pass `darkModeSelector: false`, so they have no dark theme.

### The primary is sky-600 on all three sites {#web-primary}

Since 2026-10-05 (owner decision) the preset's light primary is the brand blue the apps use, sky-600
`#0284c7`, with white on it: a filled button, a checkbox, a selected date, a focus border and a tab
strip's underline. Under the pointer it goes a step darker, to sky-700, and pressed to sky-800. Until
then the preset left the primary on Aura's default, sky-500 `#0ea5e9`, so every filled button on the
partner and admin sites was white on sky-500, 2.77:1 (the partner *Login*, the admin *Create*). White
on sky-600 reads 4.10:1, the pair the apps' and the customer site's filled buttons already used. The
customer site had painted its own filled buttons sky-600 in `_home-design.scss`, so its filled buttons
look the same as before. What it leaves to the preset moved from sky-500 to sky-600 as on the other
two sites: a ticked `p-checkbox` (the order wizard, the recurring wizard, an order's detail), a picked
date and a focused field's border, each Aura's `{primary.color}`, and the cookie notice's *OK*
(below). Its dark theme is untouched, and the primary there stays sky-400.

Text drawn from the primary takes the text ink, `{primary.700}` (sky-700), in the light scheme, as
[links and text buttons](#link-ink) do:

- **A focused field's floating label** (`formField.floatLabelFocusColor`), which Aura inks sky-600,
  4.10:1 on white.
- **The open tab's label** (`tabs.tab.activeColor`), in the admin's tab strips, which was the sky-500
  primary, 2.77:1. Its underline keeps the primary.

The cookie notice's *OK* is a filled button on all three sites, with a gradient of its own in
`cleansia-cookie-consent.component.scss`: sky-600 → sky-700, and sky-700 → sky-800 under the pointer,
where it ran sky-500 → sky-600. After dark, on the customer site, its white label sat on sky-400 →
sky-500, 2.14:1 at the light end and 2.77:1 at the other, 2.77 / 4.10:1 under the pointer; since a
later finding the same day the dark gradient is one step deeper than the light one, sky-700 → sky-800,
and sky-800 → sky-900 under the pointer, 5.93 / 7.56:1 at rest and 7.56 / 9.46:1 under the pointer.

Measured in Chromium on the running dev servers, at rest / under the pointer: the partner *Login* and
the admin *Create* 2.77 / 4.10 → 4.10 / 5.93:1; the cookie notice's *OK*, at the light end of its
gradient, 2.77 / 4.10 → 4.10 / 5.93:1; a focused floating label (the partner sign-in, the admin package
form) 4.10 → 5.93:1; the admin's open tab 2.77 → 5.93:1, an inactive one 4.76:1 as before. The
customer *Log In* reads 4.10 / 5.93:1 before and after. `cleansia-preset.spec.ts` pins the light
primary and its two darker steps, the label and tab inks, the untouched dark theme and tab underline,
and computes every light blue text ink in the preset at 4.5:1 or better on white from the preset's
own ramp; `text-ink.spec.ts` pins the cookie notice's gradients in the partner and customer bundles.

**`--primary-color` is not a token.** PrimeNG 20 names its primary `--p-primary-color`, and nothing
declares `--primary-color`, so until 2026-10-05 (finding 2026-10-05) eleven shared declarations that
read it each lost their colour, measured in Chromium on the running apps. The code boxes' focused and
filled borders and an address suggestion's icon fell back to Tailwind's `#3b82f6`. The file drop
area's border under the pointer, focused and with a file dragged over it, and a picked file's icon,
went to the text colour, black or white after dark, which also cancelled the customer site's own
accent border under the pointer, and its focus ring, read from `--primary-color-alpha-20`, dropped to
none. The partner order photos' count and upload line took their parent's black. Borders and icons
now read `--p-primary-color`, sky-600 (4.10:1 on white) and sky-400 after dark on the customer site
(6.83–8.07:1); the focus rings are `rgba(var(--cleansia-primary-rgb), …)`; and the blue text takes the
text ink, `--cleansia-primary-700`, 5.93:1. `text-ink.spec.ts` pins each site and fails on a read of
`--primary-color` anywhere under `libs/shared`.

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
| The initials in the account menu | `.customer-navbar__user-avatar` |
| The *Your plan* flag on the Plus page | `.cl-mbr__option-flag` |

The last two joined the slab later on 2026-10-05 (finding 2026-10-05). They were white on the accent,
sky-600 in light mode (4.10:1) and sky-400 after dark (2.14:1); the account menu's initials sat on a
sky-600 → sky-500 gradient, down to 2.77:1 at its light end, with a dark-mode gradient of its own. Both
now read 5.93:1 and 10.37:1, and the account menu's disc is the top bar's.

`customer-heading-slab.spec.ts`, in the assets project beside the stylesheets, reads every customer
page stylesheet and the customer navbar's, finds each rule that paints its background with
`--cl-heading` and sets an ink, and fails unless that ink is `--cl-surface`.

**Hovering never repaints a picked chip's border** (since 2026-10-05). A chip's `:hover` rule, two
classes, outranked its `--on` rule, one, so hovering the picked dispute reason, Rewards filter or Orders
filter swapped its sky-700 border (sky-300 after dark) for the pale hover tint, `#e0f2fe`, and the
picked pill lost its edge under the pointer. Each of the three hover rules now skips the picked chip
(`&:hover:not(.cl-dsp__reason--on)`, and the same for `.cl-rwd__chip` and `.customer-orders__chip`), as
the schedule form's picks already did ([the services step](/customer-app/ordering-flow#step-0-services-packages));
an unpicked chip still takes the tint. The same spec compiles the customer stylesheet, finds every
picked chip painted with the slab, and fails on a hover rule of its base that changes the border
without skipping it.

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
(`.cl-btn--inverse`, `.cl-plus__cta`) and every icon. The schedule form's add-address row rests in
sky-700 and goes a step darker under the pointer, to sky-800, since 2026-10-05; until then it went to
the lighter sky-600 (4.10:1), and after dark it still goes from sky-300 to the accent, sky-400.
PrimeNG's text buttons and links come from the preset the partner and admin sites share, and take
sky-700 there since 2026-10-05 ([below](#link-ink)). `customer-accent-text.spec.ts`, in the assets
project, compiles the customer stylesheet and requires `var(--cl-accent-text)` on each site above, and
the token in both themes.

### Links and text buttons take sky-700 on all three sites {#link-ink}

Since 2026-10-05 (owner decision: *"go to the darker, but so that it still feels natural"*) a text link,
and the label of a text, outlined or link button, is sky-700 `#0369a1` in light mode on the customer,
partner and admin sites, and goes a step darker, sky-800 `#075985`, under the pointer and while
pressed. Until then the shared PrimeNG preset left those three buttons on the light theme's primary,
sky-500 `#0ea5e9`, 2.77:1 on white, and the shared and partner link styles drew sky-600 (4.10:1) or
sky-500. Filled buttons, fills, borders and standalone icons keep the brand blue, and an icon inside a
button takes the button's ink, so no control shows two blues. The apps follow the same rule
([Blue text is sky-700](/mobile-app/patterns#brand-text-ink)).

- **The preset** (`libs/shared/assets/src/lib/cleansia-preset.ts`), read by all three apps: in the
  light scheme the text and outlined primary buttons' colour is `{primary.700}`, and the link button's
  is `{primary.700}` with `{primary.800}` on hover and press. The dark theme is untouched, so the
  customer site keeps its light blue there. PrimeNG has no hover ink for a text or an outlined button,
  so `cleansia-button.component.scss` sets `--p-button-text-primary-color` and
  `--p-button-outlined-primary-color` to `--p-primary-800` on a hovered or pressed one, in light mode
  only.
- **The shared and partner link styles**: the sign-in card's links (the partner and admin sign-in),
  the partner register and forgot-password links, the partner detail breadcrumb, the partner profile's
  consent link, the cookie notice's link (with its own sky-300 after dark, where sky-600 read 3.59:1),
  the help card's *show help*, a filter chip's remove ×, which now takes the chip's own sky-700, and a
  sortable table header with its arrow on hover. The partner registration lock's raised *Contact
  support* button gets a white face: the text ink reads 4.44:1 on the rejected row's pink and 5.93:1 on
  white.
- **The customer site's** links already took sky-700 ([above](#accent-text)); only its add-address
  row's hover moved.
- **The partner order's package price** (`.cleansia-order-details__package-header .package-price`)
  takes `--cleansia-primary-700` since a later change the same day, with the partner apps'
  informational blue text (owner decision 2026-10-05,
  [the partner app's other blue text](/mobile-app/patterns#brand-text-ink)): 4.10 → 5.93:1, beside a
  name already on sky-700. A sweep of the compiled partner bundle found no other blue text under
  4.5:1 but icons and two shared labels, the code dialog's *checking* line and the price form's
  *Optional* badge (next). `.service-item__revenue`, which the finding named, styled nothing: the
  dashboard's top-services revenue is a `<cleansia-label color="primary">`, slate on white. The rule
  was deleted later on 2026-10-05.
- **Two shared labels** (finding 2026-10-05). The code dialog's neutral status, *Checking the code…*
  under the customer sign-up's referral code (`.cleansia-code-input-dialog__status--neutral` in
  `cleansia-dialog.component.scss`), was sky-600 on a light-blue tint in both themes; it takes
  `--cleansia-primary-700` in light mode and sky-300 `#7dd3fc` after dark, 3.73 → 5.41:1 and 2.83 →
  6.97:1. The admin price form's *Optional* currency badge, on the package and the service forms
  (`.currency-price-block__badge--optional` in `_form-page.scss`), was sky-500 text on white and takes
  the text ink, 2.77 → 5.93:1; its see-through ground and outline still keep it quieter than the
  *Required* badge (5.17:1). `text-ink.spec.ts` compiles the customer and admin bundles and pins both,
  light and dark. The same dialog's *applied* and *invalid* lines, green-600 and red-600 on a 12 % and
  a 10 % wash of themselves, read under 4.5:1 too, and since a later finding the same day take
  green-800 (`--cleansia-success-800`; green-700, the apps' success text, reads 4.39:1 on the wash) and
  red-700 (`--cleansia-error-700`) in light mode, and green-300 and red-300 after dark: 2.88 → 6.24:1
  and 4.14 → 5.54:1 in light mode, 3.80 → 8.91:1 and 2.89 → 7.36:1 in dark. The washes are unchanged,
  and the icons share their line's ink.
- **Standalone icons on the partner and admin sites** (finding 2026-10-05). Nine drawn icons were in
  the lighter sky-500 or sky-400, 2.01–2.60:1, under the 3:1 an icon needs: the help card's dismiss ×
  and step arrow, an order activity's note icon, the admin pay settings banner's icon, the
  empty-section and not-found icons, the document drop zone's icon, and the invoice banner's and an
  order header's meta icons. They take the brand blue, sky-600 (`--cleansia-primary-600`),
  3.49–4.10:1, the lowest being the dismiss × on its header tint, which still goes to sky-700 under the
  pointer. The dialog's `--info` icon modifier took the same sky-600, although no template has drawn
  it since the customer site's card-capture dialogs went with the cash path's card (2026-10-04).
  `text-ink.spec.ts` pins each.

Measured in Chromium on the running dev servers, at rest / under the pointer: a PrimeNG text, outlined
or link button's label and icon 2.77 / 2.60 → 5.93 / 7.09:1 (a link under the pointer 7.56:1); the
registration lock's *Contact support* 2.08 / 2.60 → 5.93 / 7.09:1; the sign-in card's, register and
forgot-password links 4.10 / 4.10 → 5.93 / 7.56:1; the breadcrumb 4.10 / 5.93 → 5.93 / 7.56:1; the
profile's consent link 2.60 / 5.57 → 5.57 / 7.09:1; the cookie notice's link 4.10 / 5.93 → 5.93 /
7.56:1 in light mode and 3.59 / 2.47 → 8.81 / 11.06:1 in dark; *show help* 3.70 / 4.90 → 5.37 /
6.25:1; a filter chip's × 3.84 → 5.57:1; the add-address row under the pointer 4.10 → 7.56:1.
`cleansia-preset.spec.ts` pins the three inks and the untouched dark theme (and, since the primary
moved, the sky-600 filled button, [above](#web-primary)), and `text-ink.spec.ts` compiles the partner
and customer stylesheets and pins every rule above.

### A paused schedule's card steps back without fading its text {#paused-card}

On *Recurring cleanings*, a paused schedule's card (`.cl-rec__card--paused`) keeps the card's ground
and every ink, and steps back with a dashed edge (`--cl-field-border`) and no shadow, beside the
*Paused* pill it already shows (since 2026-10-05, finding 2026-10-05). Until then it set
`opacity: 0.72`, which faded the text together with the ground: in light mode the title, the price and
*Edit* read 3.41:1, the pill 3.53:1, the line about a retired service and the price's label 2.83:1, and
in dark mode those last two 4.15:1. Measured from the compiled customer stylesheet in Chromium, light
/ dark, they now read 5.93 / 10.37:1 (title, price, *Edit*), 6.37 / 6.37:1 (the pill), 10.35 / 11.64:1
(a fact) and 4.76 / 6.74:1 (the retired-service line and the price's label); an active card is
unchanged. The apps never faded a paused card: they mark it with a badge, and its lowest text reads 4.84:1
on Android and iOS, the badge in dark mode. The schedules list's spec reads the card's rule and requires the dashed edge, no shadow and no
opacity on a paused card.

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
