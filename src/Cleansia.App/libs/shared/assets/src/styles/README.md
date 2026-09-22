# Cleansia styles

Global SCSS for the three web apps. Each app's `project.json` lists exactly two stylesheets: its own
`apps/<app>/src/styles.scss` and one entry point from this folder. Shared components and feature
pages carry no `styleUrl`; their selectors live here and load once per app.

## Layout

```
styles/
├── cleansia-admin.scss       # admin app entry: common + components + pages/cleansia-admin
├── cleansia-partner.scss     # partner app entry: common + components + pages/cleansia-partner
├── cleansia-customer.scss    # customer app entry: common + components + pages/cleansia-customer
├── common/                   # tokens, the page shell and the shared page vocabulary
│   └── index.scss            #   forwards the rule partials (all but auth.scss); the mixin /
│                             #   placeholder partials are @use'd directly by the partials that need them
├── components/               # one partial per shared component in libs/shared/components
│   └── index.scss            #   every partial is @use'd here
└── pages/
    ├── cleansia-admin/       # the admin page shapes + one partial per admin page that still needs one
    ├── cleansia-partner/     # one partial per partner page; reads the admin dialog and form shapes
    └── cleansia-customer/    # one partial per customer feature, `_`-prefixed pieces per area
```

Every entry point has the same three lines:

```scss
@use './common';
@use './components/index.scss' as components;
@use './pages/cleansia-<app>/index.scss' as <app>;
```

## `common/` — what every page reads

Rule partials, forwarded through `common/index.scss` (in load order):

| Partial | Declares |
|---|---|
| `variables.scss` | every `--cleansia-*` custom property: the primary ramp, the neutral / text tokens, the error / success / warning ramps, `--cleansia-radius-{sm,md,lg,xl}` (6 / 12 / 16 / 24), `--cleansia-shadow-{1,2}`, `--cleansia-border`, `--cleansia-text-muted`. A stylesheet reads a token, never a hex; a token read here is declared here (checker rule F9). |
| `sizing.scss` | the body reset and `.h-fit-content` |
| `error.scss` | `.cleansia-error-message-container` — the field-error list under a `cleansia-*` control |
| `font.scss` | `$base-font-size: 14px` and the base font rules |
| `z-index.scss` | the one z-index scale (sidebar, drawer, toast, dialog) |
| `page-wrapper.scss` | `.cleansia-page` (the outer gutter on a page's root) and `.page-wrapper` (the one card, `max-width: 1400px`; `--narrow` = 1200 for a detail or form page) |
| `page-header.scss` | `.cleansia-page-header` → `__heading` / `__description` / `__actions` — title left, actions right, secondary before primary |
| `detail-grid.scss` | `.detail-grid` → `__item` / `__item--wide` / `__label` / `__value` — the label/value grid of a detail page (4 / 2 / 1 columns) |
| `status-badge.scss` | `.status-badge.status-badge--{neutral,info,success,warning,danger}` drawn by `<cleansia-status-badge>`, and `.status-cell` |
| `not-found-state.scss` | `.not-found-state` — a whole page whose entity is missing |
| `empty-state.scss` | `.empty-state` — an empty section (a table draws its own empty message) |

Mixin and placeholder partials, `@use`d directly by the partials that need them (never forwarded —
they emit no CSS on their own):

| Partial | Declares | Read by |
|---|---|---|
| `typography.scss` | `$font-heading` (`'Poppins', 'Nunito', sans-serif`) and `$font-body` — the back-office bundles load Nunito only, so `$font-heading` resolves to the fallback there | `components/cleansia-dialog`, `cleansia-filter-drawer`, `cleansia-work-contract-dialog`; the customer `order-wizard` and `_home-variables` partials |
| `touch-target.scss` | the `touch-target` mixin — a 44 px hit box on a control drawn smaller | `components/cleansia-button`, `cleansia-filter-drawer`, `cleansia-sidebar-menu`, `cleansia-table` |
| `focus.scss` | `%focus-ring` — the one keyboard focus ring, extended wherever `outline: none` is set | `components/cleansia-file`, `cleansia-filter-drawer`, `cleansia-sidebar-menu`; `pages/cleansia-admin/notifications` |

One rule partial sits in `common/` without being forwarded: `auth.scss` declares `.cleansia-login`
(the sign-in card both portals draw) and is `@use`d from `pages/cleansia-admin/index.scss` and
`pages/cleansia-partner/index.scss` — the customer app has its own `_auth` partial and never reads
it, so it does not go through `common/index.scss`.

## `pages/cleansia-admin/` — the page shapes, then the pages

The `_`-prefixed partials are the **shapes**; a page stylesheet carries only what is page-specific and
never re-declares a card width, a radius, a centred header, a form column in pixels or a badge ramp
(`apps/cleansia-admin.app/src/app/theme/*.spec.ts` fail on each of those).

| Partial | Declares |
|---|---|
| `_detail-page.scss` | `.cleansia-detail-title` (the back control beside the `h1`), `.detail-identity` (the tinted identity strip), `.detail-actions` (+ `__panel`, `__hint`, `__error`), `.detail-ledger`, and `display: contents` on the three ops components |
| `_form-page.scss` | `.cleansia-form`, the twelve-column `.form-grid` (`.form-field` span 6, `--third` 4, `--quarter` 3, `--full`), `.form-hint` / `.form-error` on a full-width line under the row, `.form-actions`, the resting float label for a select / multiselect, `.form-translations`, `.currency-price-block` |
| `_dialog.scss` | `.cleansia-dialog.dialog-panel` (480; `--wide` 560; `--reading` 720), `.dialog-body`, `.dialog-lede`, `.dialog-toolbar`, `.dialog-summary`, `.dialog-actions` |
| `tab-strip.scss` | `.p-tabs .p-tab` as a flex row so PrimeNG's icon-to-label gap applies |
| `list-filter.scss` | `.cleansia-list-filter` — a lone select above a list table at a field's width |

Page partials that still exist (each named after the component it styles): `admin-order-photos`,
`admin-photo-gallery`, `audit-log`, `company-lifecycle`, `company-settings`, `data-protection`,
`dispute-detail`, `email-type-detail`, `employee-detail`, `employee-management`, `invoice-detail`,
`legal-documents`, `membership-plan-list`, `notifications`, `order-detail`, `package-form`,
`pay-config-management`, `reports`, `service-area-management`, `template-form`,
`template-management`, `unauthorized`, `user-loyalty-detail`. The admin pages not listed have no
stylesheet at all: the shell, header, section, grid and form partials above are the whole of their
styling.

## `pages/cleansia-partner/` — the partner pages

The partner index `@use`s `../cleansia-admin/dialog` and `../cleansia-admin/form-page`, so the partner
dialogs and forms read the admin shapes rather than copy them. Its own partials: `_breadcrumb.scss`
(the `%breadcrumb*` placeholders the two detail pages extend — there is no shared breadcrumb
component), `confirm-email`, `dashboard`, `forgot-password`, `invoice-detail`, `invoices`,
`order-details`, `orders`, `period-pay`, `photo-gallery`, `profile`, `register`.

## `pages/cleansia-customer/` — the customer app

`home.component.scss`, `order-wizard.component.scss` and one `_`-prefixed partial per area
(`_auth`, `_catalog`, `_plus`, `_track`, `_checkout-result`, `_wizard-shell`, `_orders`,
`_order-detail`, `_profile`, `_saved-addresses`, `_gdpr`, `_legal-pages`, `_disputes`, `_rewards`,
`_membership`, `_recurring-bookings`; the `_home-*` pieces are `@use`d from `home.component.scss`).
The customer app is outside the back-office page shapes and the F-rules; it carries its own shell.

## `components/` — one partial per shared component

`@use`d from `components/index.scss`, one per component under `libs/shared/components` (plus
`_pref-pill.scss`, the `pref-pill` / `pref-pill-dark` mixins the language and market switchers
include). The ones the
back-office page shapes depend on: `cleansia-button` (the host sizes to its label; `.cleansia-button--block`
is the one full-width variant), `cleansia-section` (the header row with `__title` and `__actions`),
`cleansia-select` and `cleansia-calendar` (at the text input's 44 px), `cleansia-table` (`.numeric`
cells nowrap and right-aligned; the actions column inside the border), `cleansia-filter-drawer`,
`cleansia-mobile-toolbar`, `cleansia-dialog` (the skin), `cleansia-loader` (in place, not a fixed
overlay).

## Rules

- A partial ships only through a `@use`: from its folder's `index.scss`, or from a partial that is
  (the `_`-prefixed pieces, the three `common/` mixin and placeholder partials, and `common/auth.scss`
  from the two back-office page indexes). Adding a file is
  two steps: create it, add the `@use` line. A page partial whose component no longer exists is
  reported by checker rule F12.
- Shared components and feature pages carry no `styleUrl`; their selectors live here and load once per
  app. The handful of components that do declare a `styleUrl` are the exceptions, not the pattern.
- `common/` is the only place for tokens and mixins; the CSS custom properties are declared in
  `common/variables.scss`.
- Page partials are named after the component they style (`<feature>.component.scss`); shape partials
  are `_`-prefixed and named for the shape.
- A page partial declares nothing the shell or a shape already declares. The jest theme specs under
  `apps/cleansia-admin.app/src/app/theme/` and `apps/cleansia-partner.app/src/app/theme/` read this
  folder off disk and fail on a card width, a card radius, a centred header, a pixel form column, a
  `.p-button` colour override or a page-local badge ramp.
