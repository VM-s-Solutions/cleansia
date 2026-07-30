---
id: T-0444
title: Web — refresh the logo and favicon across all three Angular apps from the iOS mark
status: ready
size: S
owner: frontend
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0438]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Owner, verbatim: *"Also make a logo change to all of the web apps."* — the same brand-source ruling as
T-0443: **iOS is the source of truth for the mark.**

**What the three web apps have today (verified by the PM, 2026-07-30 — sha1 first 10):**

| sha1 | File | Referenced from |
|---|---|---|
| `71b28a8a6a` | `apps/cleansia.app/src/assets/logos/Logo.ico` | — |
| `71b28a8a6a` | `apps/cleansia-partner.app/src/assets/logos/Logo.ico` | `apps/cleansia-partner.app/src/index.html:8` |
| `71b28a8a6a` | `apps/cleansia-admin.app/src/assets/logos/Logo.ico` | `apps/cleansia-admin.app/src/index.html:8` |
| `365adf5963` | `apps/cleansia.app/src/assets/logos/Logo.webp` | — |
| `365adf5963` | `apps/cleansia-partner.app/src/assets/logos/Logo.webp` | `apps/cleansia-partner.app/src/app/app.component.html:23` |
| `365adf5963` | `apps/cleansia-admin.app/src/assets/logos/Logo.webp` | `apps/cleansia-admin.app/src/app/app.component.html:22` |
| `365adf5963` | `apps/cleansia.app/src/assets/images/logo.png` | — |
| `aa5c62a955` | `apps/cleansia.app/src/assets/logos/favicon-32.png` | `apps/cleansia.app/src/index.html:21` |
| `4e23d0f9b6` | `apps/cleansia-admin.app/public/favicon.ico` | (Angular default; possibly unreferenced) |

Two defects fall straight out of that table and are in scope because the fix touches the same files:

1. **`Logo.webp` is not a WebP.** `apps/cleansia.app/src/assets/images/logo.png` and all three
   `Logo.webp` files share sha1 `365adf5963`, and `file(1)` reports that byte-stream as
   *"PNG image data, 48 x 48, 8-bit/color RGBA"*. So every app serves a **PNG under a `.webp`
   extension**. Browsers sniff and render it, so it looks fine — but the extension lies, and any
   consumer that trusts it (an OG/preview scraper, a strict CDN content-type rule) gets it wrong.
2. **The header logo is a 48×48 source rendered at 28×28** on HiDPI displays — that is soft on any
   2× screen and there is no 2×/3× or SVG alternative.

`apps/cleansia-admin.app/public/favicon.ico` appears to be the stock Angular favicon and is not
referenced from `index.html` (which points at `assets/logos/Logo.ico`) — confirm whether it is dead
and remove it if so, rather than updating two competing favicons.

## Acceptance criteria

- [ ] **AC1** — Given each of the three apps, When the browser tab renders, Then the favicon is the
      current brand mark taken from the iOS asset
      (`src/cleansia_ios/CleansiaCustomer/Resources/Assets.xcassets/AppIcon.appiconset/AppIcon1024.png`,
      sha1 `a39ee5488041`). Evidence: a tab screenshot per app.
- [ ] **AC2** — Given the in-app header of the partner and admin apps
      (`app.component.html:23` / `:22`), When it renders on a 2× display, Then the mark is crisp —
      i.e. the source is an SVG, or a raster at ≥2× the 28px render box. Evidence: the asset's
      intrinsic dimensions/format at file:line.
- [ ] **AC3** — Given the replaced assets, When each file is inspected with `file(1)`, Then its
      actual format matches its extension. Specifically: no PNG bytes served as `.webp`. Evidence:
      `file` output for every asset the change touches, pasted into the verdict.
- [ ] **AC4** — Given `apps/cleansia-admin.app/public/favicon.ico`, When the change lands, Then it is
      either updated to the new mark **or** deleted as dead — with a one-line statement of which and
      why (grep evidence that nothing references it). No app may ship two competing favicons.
- [ ] **AC5** — Gate 8: all three production builds green — `npm run build:cleansia-customer`,
      `build:cleansia-partner`, `build:cleansia-admin` — each exit 0, run **after** T-0438 lands.
      Record the commands and exit codes.
- [ ] **AC6** — Given the customer app is SSR, When it is server-rendered, Then the favicon and any
      logo referenced in `index.html` still resolve (assets are copied by the build). Evidence: the
      built output listing the assets.

## Out of scope

- Android brand assets — T-0443.
- Any change to the iOS assets. **Never read or modify `src/cleansia_ios/**/Info.plist` or
  `**/project.yml`.**
- Redesigning the mark, or any in-app illustration/mascot work.
- A PWA manifest / maskable-icon set (none exists today; adding one is separate work — note it if you
  think it is wanted, do not build it here).

## Implementation notes

- `depends_on: T-0438` is a **build-gate** dependency, not a code one: until T-0438 lands, all three
  production builds fail for unrelated reasons and AC5 cannot produce honest evidence. Do not report
  AC5 as PASS against a tree that is red for T-0438's reason — that is exactly the "verified nothing"
  failure T-0445 exists to prevent.
- The three `Logo.ico` files are byte-identical today; keep them identical after the change (one
  source, copied) so a future drift is visible.
- No i18n keys involved. `alt` text already exists (`alt="Cleansia"` / `alt="Cleansia Admin"`).

**No-decision note (skips the deliberation panel):** the owner supplied the decision (iOS mark, all
web apps). No new behaviour or seam — an asset swap plus two file-format corrections found while
grounding it. Sizing/AC/deps/layers set → DoR met.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 4, web half)
- 2026-07-30 — ready (dep T-0438 recorded; DoR met; no-decision note recorded)

## Review
<!-- reviewer writes verdict here -->
