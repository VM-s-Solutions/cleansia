# Cleansia styles

Global SCSS for the three web apps. Each app's `project.json` lists exactly two stylesheets: its own
`apps/<app>/src/styles.scss` and one entry point from this folder.

## Layout

```
styles/
├── cleansia-admin.scss       # admin app entry: common + components + pages/cleansia-admin
├── cleansia-partner.scss     # partner app entry: common + components + pages/cleansia-partner
├── cleansia-customer.scss    # customer app entry: common + components + pages/cleansia-customer
├── common/                   # variables, sizing, error, font, z-index, page-wrapper, page-header,
│   └── index.scss            #   detail-grid, status-badge, not-found-state — forwarded through
│                             #   common/index.scss; touch-target, typography and focus are mixin- or
│                             #   placeholder-only, @use'd directly by the partials that need them
├── components/               # one partial per shared component in libs/shared/components
│   └── index.scss            #   every partial is @use'd here
└── pages/
    ├── cleansia-admin/       # one partial per admin feature page, @use'd from its index.scss
    ├── cleansia-partner/     # one partial per partner feature page, @use'd from its index.scss
    └── cleansia-customer/    # one partial per customer feature page, @use'd from its index.scss
```

Every entry point has the same three lines:

```scss
@use './common';
@use './components/index.scss' as components;
@use './pages/cleansia-<app>/index.scss' as <app>;
```

## Rules

- A partial ships only through a `@use`: from its folder's `index.scss`, or from a partial that is
  (the `_`-prefixed pieces under `components/` and `pages/cleansia-customer/`, and the three `common/`
  mixin and placeholder partials). Adding a file is two steps: create it, add the `@use` line.
- Shared components and feature pages carry no `styleUrl`; their selectors live here and load once per
  app. The handful of components that do declare a `styleUrl` are the exceptions, not the pattern.
- `common/` is the only place for tokens and mixins; the CSS custom properties are declared in
  `common/variables.scss`.
- Page partials are named after the component they style (`<feature>.component.scss`).
