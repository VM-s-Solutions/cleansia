---
id: T-0683
title: PrimeFlex is a third-party CDN dependency for 37 classes — self-host the subset or drop it
status: todo
size: M
owner: —
created: 2026-09-06
updated: 2026-09-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

The customer app loads PrimeFlex 4.0.0 from `cdn.jsdelivr.net` to use **37 of its 1,313 classes**.

**First, a correction to how this was originally reported.** The T-0682 findings list called it
"373,504 bytes, 99.88% unused". That number is the **decompressed** size. On the wire it is
**31,793 bytes gzipped**, and it loads asynchronously through the `media="print"` / `onload` swap at
`apps/cleansia.app/src/index.html:66-76`, so it blocks nothing. The prize is ~30 KB plus one
cross-origin DNS+TLS handshake — worth having, not urgent.

### It cannot simply be deleted

The customer navbar's **mobile drawer has no project styling at all**:

```
customer-navbar.component.html:210
  class="customer-navbar__mobile-menu surface-overlay shadow-2 fixed left-0 right-0 z-4 p-4 flex flex-column gap-3"
```

Every rule for `.customer-navbar__mobile-menu` in the repo
(`cleansia-customer-navbar.component.scss` :464, :514, :581) sets **only `top`**. Background, shadow,
positioning, stacking, padding and the column layout all come from those ten PrimeFlex classes.
Delete the link and the drawer becomes a transparent, unpositioned block in normal flow — and
`top: 64px` goes inert with no `position`. Three further elements (`:244`, `:246`/`:254`, `:263`)
carry no project class whatsoever.

Two thirds of the navbar's *other* PrimeFlex classes are already duplicated in project SCSS on
purpose — the author's comment at `cleansia-customer-navbar.component.scss:20-23` explains that
PrimeFlex loads async, so anything the first paint needs must live in the bundled sheet.

### The 37 classes, and the traps

`mr-2` 16 · `field` 14 · `flex` 10 · `align-items-center` 10 · `justify-content-between` 3 ·
`hidden` 3 · `fixed` 2 · `left-0` 2 · `w-full` 2 · `flex-column` 2 · `gap-3` 2 · `active` 2 ·
`text-right` 2 · `text-center` 2 · `mb-3` 2 · `mb-2` 2 · `mb-4` 2 — plus 20 used once each
(`top-0`, `h-full`, `justify-content-center`, `justify-content-end`, `relative`, `text-xl`,
`surface-overlay`, `shadow-2`, `right-0`, `z-4`, `p-4`, `border-top-1`, `border-300`, `mt-2`,
`pt-3`, `gap-2`, `p-2`, `text-700`, `font-medium`, `border-round-3xl`). 98 occurrences total.

Three traps, each of which silently breaks a naive extraction:

1. **Responsive variants are escaped in the CSS.** `md:flex` (x2) and `md:hidden` (x1) are used and
   are written `.md\:flex`. A scan that splits class attributes on whitespace and matches literally
   finds `hidden` but misses `md:hidden` — and the navbar then breaks only at >=768 px. This was
   caught in the investigation precisely because a first pass did miss them.
2. **Four names collide with project-owned rules** — `text-center` (`font.scss:19`), `text-right`
   and `text-center` again (`cleansia-table.component.scss:177,181,254,258`), `field`
   (`cleansia-availability.component.scss:480,495,537`), and `active`. Whatever is extracted must
   not change the cascade order for these.
3. **PrimeFlex ships `!important` on its utilities, and the project is actively fighting it.**
   `cleansia-customer-navbar.component.scss:797-809` exists solely to override `md:flex`, which
   starts laying out at 768 px when the navbar wants 1360 px. A replacement must reproduce that
   conflict or deliberately end it.

## Acceptance criteria

- [ ] **AC1** — Given the customer app, When PrimeFlex is self-hosted as a subset (or removed with
      project SCSS replacing it), Then the computed styles of every element carrying one of the 37
      classes are unchanged — including the mobile drawer in its OPEN state, both >=768 px and below.
- [ ] **AC2** — Given the change, Then no request to `cdn.jsdelivr.net` for primeflex remains.
- [ ] **AC3** — Given the navbar, Then the `!important` override block at
      `cleansia-customer-navbar.component.scss:797-809` is either still needed and kept, or removed
      with a note saying the conflict it fought is gone.

## Out of scope

- **Partner and admin.** They never load PrimeFlex — see the finding below. Do not "fix" their dead
  classes by giving them the stylesheet: that would change how those screens render today.

## Findings this ticket carries

**Partner and admin use ~33 PrimeFlex classes and never load PrimeFlex.** Neither
`apps/cleansia-partner.app/src/index.html` nor `apps/cleansia-admin.app/src/index.html` carries the
link, and neither `cleansia-partner.scss` nor `cleansia-admin.scss` imports it. Those class
attributes are inert today — `w-full` x23 and `field` x8 in admin-features, 2 in partner-features.
Either the markup is relying on styling it never receives, or they are leftovers to delete. Someone
should look at those screens and decide which.

## Status log

- 2026-09-06 — filed after the T-0682 findings pass measured the real wire cost and found the drawer
  dependency. The original "373 KB, 99.88% unused" framing was decompressed size, corrected here.
