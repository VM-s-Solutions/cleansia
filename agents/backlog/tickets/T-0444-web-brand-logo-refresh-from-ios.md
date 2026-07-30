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
- 2026-07-30 — implemented (frontend) on `feat/T-0444-web-logo` off `fix/T-0438-unbreak-web-build`
- 2026-07-30 — **owner overruled the monogram**: *"NO, I want the web to use usual 'cleansia' logo
  that is used in ios apps."* Reworked to the wordmark (frontend, second commit on the same branch)

## Implementation notes (frontend)

**Both PM findings reproduced.** All three `Logo.webp` were sha1 `365adf596364`, `file(1)` =
*PNG image data, 48 x 48* — a PNG under a `.webp` extension, at 48px into a 28px slot.

**The mark had to be adapted, not copied.** The named source
(`CleansiaCustomer/.../AppIcon1024.png`, sha1 `a39ee5488041` — verified) is a **full-bleed wordmark**;
iOS has no icon-only mark (both `AppIcon1024.png` are wordmarks, both `LaunchWordmark` are wordmarks).
Downscaled to favicon size it fails: at 32px the lettering is ~4px tall, at **16px it is an illegible
white bar**. Every web brand slot is a small square *next to the literal text "Cleansia"*
(favicon 16–32px; header `<img width="28">`; `cleansia-brand-name` `<img width="32">`), i.e. a symbol
slot. This is the web analogue of T-0443 AC2's *"this is why AC1 is not 'copy the 1024 PNG in'"*, and
of the owner's delegation *"make it on your own to match iOS version"*.

So the mark was **derived from the iOS art by measurement, not redesigned**:
- the "C" glyph is the actual glyph from the iOS wordmark, sampled from the source pixels
  (bbox x 98–223, y 444–575 in the 1024 icon) as a coverage mask — not re-set in a font;
- the gradient is the iOS gradient, corners `TL(0,152,210) → BR(64,201,249)`, reproduced bilinearly
  (worst error **1.2/255** against 8 sampled background points);
- the silhouette is the iOS squircle (superellipse n=5).

No new art was authored; every pixel derives from `AppIcon1024.png`. If the owner would rather ship
the full wordmark in the square slots despite the 16px legibility loss, that is a one-line
regeneration — flagged rather than assumed.

**Also fixed (same edit, same files):** the customer app was shipping **two competing favicons** —
`index.html` declared `favicon-32.png` while `PageTitleService.faviconPath` rewrote `link[rel=icon]`
to `Logo.ico` at runtime. `index.html` is now the single favicon declaration in all three apps and
the now-dead favicon plumbing (`faviconPath`, `setFavicon`, `setupFavicon`, the `DOCUMENT` inject)
was removed from `PageTitleService` — it had no remaining caller. This also removes a browser-only
DOM mutation from the SSR customer path.

`<picture>` in `cleansia-brand-name` had `<source type="image/webp">` and its `<img>` fallback both
pointing at the same PNG-named-`.webp`; the `<source>` is now a real WebP and the fallback a real PNG.

**Follow-ups found, not actioned (out of scope):**
- `libs/shared/components/src/lib/cleansia-menu/` is **dead** — exported from the barrel, used by no
  app; its `<img src="assets/images/logo.png">` is the only reference to that asset (which exists in
  the customer app only, so the component would 404 in partner/admin). Deleting a public shared-lib
  symbol is a separate call. The asset was refreshed to the new mark to avoid brand drift.
- `cleansia-brand-name` carries `loading="lazy"` on an above-the-fold navbar logo; the component is
  shared with the footer, so the correct fix is a per-call-site input, not a blanket flip.
- No `apple-touch-icon` / PWA manifest exists. Explicitly out of scope; worth a ticket — the new
  1024 master regenerates any size.

## Implementation notes (frontend, second pass — owner overruled the monogram)

The monogram is gone. The mark is now the **"Cleansia" wordmark**, and the source is the iOS
`LaunchWordmark` asset (`CleansiaCustomer/.../LaunchWordmark.imageset/wordmark.png`, 1400×360) rather
than the app icon: it carries the same letterforms with a real alpha channel at 230px of ink height,
against 149px if the glyphs are pulled off the icon's gradient. Its alpha is reused **verbatim** — the
only change is ink colour, white → `--cleansia-primary #0284c7`, because iOS puts the mark on a blue
launch background and the web puts it on `--surface-overlay`/`--surface-card`. That is the same colour
the `<h2>Cleansia</h2>` used, so contrast is unchanged: 4.4:1 on `#0f172a`, 3.6:1 on `#1e293b` (WCAG
1.4.11 non-text ≥3:1). Ink aspect measured 5.4870 at `alpha>0` and 5.5263 at `alpha>250`; the master is
616×112 = **5.5000**, inside the source's own thresholding spread (0.24% off the `alpha>0` box).

**The shape problem — the slot is now a wide wordmark, and the text label is gone.** Every brand slot
was `[32px square] + the literal text "Cleansia"`. Squeezing a 5.5:1 wordmark into that square gives
32×6px of ink; keeping the text beside a wordmark prints the word twice. So `cleansia-brand-name`
renders the wordmark alone (`<h2>Cleansia</h2>` deleted — the `alt` carries the accessible name, and
it also removes a duplicate `<h2>` from the heading outline of the footer and 8 auth screens). The
partner mobile toolbar drops its `<span>Cleansia</span>` for the same reason; the admin toolbar keeps
its suffix, now `<span>Admin</span>`, which the wordmark does **not** duplicate.

That forced the component API. `showName` could no longer be honoured — the name is baked into the
image — and `wrapped` (column layout for an icon-over-text pair) became a no-op with one child. Both
are replaced by `compact`, the only variant the geometry actually has: the collapsed sidebar rail.
Rendered ink per slot — navbar/footer/auth `132×24`, mobile toolbar `96×17.5`, collapsed rail `66×12`.
The rail is 6rem wide (`1rem = 14px` here, so 84px → 70px inside the sidebar's padding); its header's
`1rem` side padding was eating 28 of those 70px, so it is zeroed while collapsed. `brandCompact` also
excludes mobile, where the drawer always opens at full width.

**The favicon is the one square that cannot hold a 5.5:1 mark, and it is reported, not hidden.**
`Logo.ico` (16/32/48) is now the **iOS app icon downscaled, unmodified** — the artwork iOS itself uses
when the frame is square, so no derived glyph and no authored corner radius this time. Measured in the
shipped frames: at 48px the lettering is a 38×7px band, at 32px 26×4px, and **at 16px not one pixel
reaches 200/255 on all three channels — the word has dissolved into the tile**. Letterboxing the
wordmark into the square instead is worse, not better: 16×3px of ink, peak alpha 196/255, zero fully
opaque pixels, and no tile at all, so on a light tab strip it is a faint smudge on nothing.
`Cleansia` needs ≈8px of ink height to read → ≈44px of wordmark → an icon ≈55px square. **The tab is
therefore identified by the blue tile, not by reading the word, and that is true of both options.**
If the owner wants a *readable* tab mark, the favicon alone has to be a compact mark — his call;
renders of both at 16/32/48 on light and dark tab strips are attached to the frontend report.

**Not decided unilaterally:** iOS ships a separate partner lockup (`Cleansia` over `PARTNER`,
aspect 3.59, in both the partner `AppIcon1024` and its `LaunchWordmark`). All three web apps get the
plain `Cleansia` wordmark, matching the word each app shows today. Giving the partner app its own
lockup is a one-file swap if the owner wants it.

Also corrected while in the file: `cleansia-brand-name`'s image was sized `height` + `max-width: 100%`,
which squashes a replaced element horizontally in a narrow container (CSS 2.1 §10.4) — invisible on a
1:1 icon, obvious at 5.5:1. It is now width-driven with `height: auto`. Both mobile toolbars lose a
`border-radius: 4px` that only meant something on a square tile. `cleansia-menu` (dead, zero call
sites) got `width`/`height` attributes so its untouched `assets/images/logo.png` reference cannot
render at the new 616px intrinsic width.

Harvested into `agents/knowledge/patterns-frontend.md` → "Brand mark — `cleansia-brand-name` is the
whole lockup": no text beside the wordmark, `compact` as the only variant, bytes-match-extension, and
width-driven sizing for non-square logos.

## Review
<!-- reviewer writes verdict here -->
