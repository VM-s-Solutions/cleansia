---
id: T-0795
title: Shell parity between the admin and partner web — `cleansia-mobile-toolbar`, one auth stylesheet, print rules shared, guards return a `UrlTree`, admin routes on the enum, sidebar `aria-current` and translated labels, route titles
status: todo
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0785]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"make overall check on both apps to make them consistent"*. CS §1, §8, §9:
the two app shells duplicate the mobile toolbar markup and its SCSS (admin `app.component.scss:9-149`,
partner `:9-99` — the first 72 lines identical), both hard-code `"Open menu"` as an `aria-label`, the
admin registers a `window.addEventListener('resize')` it never removes where the partner uses
`@HostListener`, the partner's `@media print` block is page-local, the two login stylesheets are
byte-identical (128 lines), the admin login has no language switcher, admin routes are string
paths beside a `CleansiaAdminRoute` enum used 124× elsewhere, and four guards `router.navigate`
inside a guard where the two newest return a `UrlTree`. The shared sidebar marks no `aria-current`.

Paths are relative to `src/Cleansia.App/`.

## Doing

- `cleansia-mobile-toolbar` in `@cleansia/components`: brand through `cleansia-brand-name` (today a
  raw `<picture>` at admin `app.component.html:22-27`, partner `:23-31`), menu button with a
  **translated** `aria-label` (`global.open_menu`; today `"Open menu"` at admin `:17`, partner
  `:14-21`), `<ng-content>` for app extras (the admin bell + badge `:29-41` projected). Delete the
  duplicated SCSS and the stale `4rem` fallbacks (`admin:176`, `partner:132` — the variable is `6rem`,
  `cleansia-sidebar-menu.component.scss:3-6`).
- Resize: admin `app.component.ts:70` `window.addEventListener` → `@HostListener('window:resize')`
  (partner `:131-134`); expose the signal (partner `:74`), not a method (admin `:58,77-79`).
- Print: partner `app.component.scss:147-179` `@media print` → the shared sidebar / toolbar SCSS
  (the admin invoice detail prints too).
- Admin login gets `<cleansia-language-switcher>` (partner `login.component.html:2`);
  `pages/cleansia-admin/login.component.scss` and `pages/cleansia-partner/login.component.scss` →
  one `common/auth.scss`.
- Routes: admin `app.routes.ts:23,31,37,…` string paths and `admin-menu.ts:27,33,39,…` →
  `CleansiaAdminRoute` (the partner uses its enum at `app.routes.ts:8-35`). Aliases: T-0793 removes
  the two `@cleansia.app/*`.
- Guards: `admin.guard.ts:11,15`, admin `guest.guard.ts:16`, partner `auth.guard.ts:12`,
  `guest.guard.ts:16` return `createUrlTree` (as `permission.guard.ts:16`,
  `membership.guard.ts:44,51` do) instead of `router.navigate([...])`.
- Sidebar (shared, all three apps): `aria-current="page"` on the active item and a translated
  *Close menu* (`cleansia-sidebar-menu.component.html:28,39-49`).
- Route titles for the two admin routes that lack one (`marketing/lib.routes.ts`,
  `template-management/lib.routes.ts`).
- **Guard:** F14 in T-0798 (literal `aria-label`); a `routes.spec.ts` asserting every admin
  `app.routes.ts` path is a `CleansiaAdminRoute` member (import both, compare sets).

## NOT

- A desktop top bar (neither app has one). The sidebar item semantics rewrite (`<li tabindex>` →
  `<a routerLink>`) — a shared-component rewrite touching all three apps' navigation; only
  `aria-current` and the translated labels are here. The `isMobile` breakpoint values. The customer
  shell.

## Done looks like

`diff` of the two shells' toolbar markup is the one `<cleansia-mobile-toolbar>` line plus projected
content; `rg '"Open menu"|"Close menu"' apps libs` = 0; the admin login capture shows the language
switcher where the partner's does; `rg "router.navigate" libs/core/*/src/lib/guards` = 0;
`routes.spec.ts` green.

## Acceptance criteria

- [ ] **AC1** — Given both app shells, When their templates are diffed, Then the toolbar differs
      only by the projected admin bell; `rg '"Open menu"|"Close menu"' apps libs` returns nothing.
- [ ] **AC2** — Given a signed-out visit to a guarded admin or partner route, When the guard runs,
      Then it returns a `UrlTree` to the login (no `router.navigate` inside any guard) and the
      redirect still lands on the login page.
- [ ] **AC3** — Given `routes.spec.ts`, When it runs, Then every admin `app.routes.ts` path is a
      `CleansiaAdminRoute` member and the menu builds from the same enum.
- [ ] **AC4** — Given the admin login at 1440, When re-captured, Then the language switcher renders
      where the partner's does and both logins share `common/auth.scss`.
- [ ] **AC5** — Given the sidebar in any of the three apps, When a route is active, Then its item
      carries `aria-current="page"` and the close control's label is translated.
- [ ] **AC6** — Given the admin invoice detail, When printed, Then the sidebar and toolbar are hidden
      by the shared print rule.

## Implementation notes

Depends on T-0785 (z-index). May run beside T-0785 / T-0786 — disjoint files (`apps/*` and
`libs/core/*`, not `libs/shared/assets/src/styles/`). **Regen:** none.

**Security (Gate 3) — `security_touching: true`.** Four auth / guest guards change their redirect
mechanism; behaviour (who is refused, where they land) must be pinned unchanged by the guard specs
before and after.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 2, may run beside the web-shared serial four (disjoint files).
