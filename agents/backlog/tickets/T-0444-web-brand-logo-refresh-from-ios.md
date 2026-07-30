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
- 2026-07-30 — changes requested → addressed (frontend, third commit). Heading outline repaired on the
  nine auth screens; the asset invariants now have a spec instead of a catalog sentence; three
  documentation claims corrected. **Note for whoever reads the history:** commit `957a7610`'s message
  repeats the wrong a11y claim (*"removes a duplicate h2 … and eight auth screens"*). It is not
  amendable without a rebase; the correction lives in this ticket and in the follow-up commit.

## Implementation notes (frontend)

> **SUPERSEDED — 2026-07-30.** The monogram described below **does not ship**. The owner overruled it
> in favour of the wordmark; see "second pass" further down for what is actually in the tree. The
> section is kept because its findings about the `.webp` extension lie, the two competing favicons and
> the 16px legibility floor are still the evidence base — but every sentence about the "C" mark
> describes artwork that was regenerated away.

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
the old lockup announced "Cleansia logo Cleansia"). The partner mobile toolbar drops its
`<span>Cleansia</span>` for the same reason; the admin toolbar keeps its suffix, now `<span>Admin</span>`,
which the wordmark does **not** duplicate.

**Heading outline — the first pass got this half wrong, and it is now fixed.** In the **footer** the
brand `<h2>` really was surplus: the footer already carries `<h3>` column titles under the page's own
headings, so deleting it removed a stray level-2. On the **nine auth screens the opposite was true**.
`cleansia-title` defaults `size` to `'default'`, which renders `<h3>` (`cleansia-title.component.html`),
and all nine call it with no `size` — so the outline was `<h2>` brand → `<h3>` Login, and the brand
`<h2>` was the page's *only* top-level heading. Deleting it left those screens with an `<h3>` and
nothing above it.

The repair is not `size="large"`. That input conflates heading rank with the type scale: `--large` is
`font-size: 3rem` against `--default`'s `1.5rem`, so it would double the type on nine production
screens, and four page rules key off `.cleansia-title--default`
(`pages/cleansia-customer/{login,register}.component.scss`, `&__header` plus the dark-mode
`color: #e2e8f0`) which would silently stop matching. Instead `cleansia-title` gained an optional
`level` input; when it is absent the level is derived from `size` exactly as before, so all 54 other
call sites render byte-identical markup. The 11 auth titles pass `[level]="1"` and become the `<h1>`
they always should have been — **at the size they already had**. Net: the regression is gone and a
pre-existing missing-`h1` on every auth screen is fixed, with zero visual change.

That forced the component API. `showName` could no longer be honoured — the name is baked into the
image — and `wrapped` (column layout for an icon-over-text pair) became a no-op with one child. Both
are replaced by `compact`, the only variant the geometry actually has: the collapsed sidebar rail.
Rendered ink per slot — navbar/footer/auth `132×24`, mobile toolbar `96×17.5`, collapsed rail `66×12`.

Rail arithmetic, under the **content-box** model that actually applies here (grep: the only
`box-sizing: border-box` in the workspace styles is two rules in `recurring-bookings.component.scss`,
and PrimeFlex's single declaration is scoped to `.grid > .col` — there is no universal reset). With
`1rem = 14px`: `--sidebar-width-collapsed: 6rem` is 84px of *content*, and `.sidebar-header` fills it,
so the header's own `1px` border and `1rem` side padding leave **54px** of content — less than the
66px mark, which `.sidebar { overflow: hidden }` would then clip. Zeroing the header's inline padding
while collapsed raises that to **82px**, so the mark fits with 16px of slack. Expanded (`16rem` =
224px) the header has 194px of content: the 132px mark sits 13.5px clear of the mobile close button.
`brandCompact` also excludes mobile, where the drawer always opens at full width.

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

**Sizing rule for the new shape (no prior bug — a constraint the old shape never had).** At
`a7c4f5c4` the brand stylesheet had no `img` rule at all; the 32px square was sized purely by its
`width`/`height` attributes, and at 1:1 nothing could go wrong. A 5.5:1 mark is different: a fixed
`height` plus `max-width: 100%` squashes a replaced element horizontally once a container is narrower
than the mark (CSS 2.1 §10.4 resolves the over-constraint by clamping width and keeping the specified
height). So the new rule is width-driven — `width: 132px; max-width: 100%; height: auto` — which
shortens the mark proportionally instead.

Also in passing: both mobile toolbars lose a `border-radius: 4px` that only meant something on a
square tile, and now negotiate WebP through `<picture>` like the shared component, so the mobile path
no longer downloads the 14.3 KB PNG alongside the 8.0 KB WebP the drawer already fetches. The two
customer-navbar brand instances were byte-identical once `showName` went, so they collapse into one
always-visible `__left` container (`__brand-mobile` and its two display rules deleted). `cleansia-menu`
(dead, zero call sites) got `width`/`height` attributes so its untouched `assets/images/logo.png`
reference cannot render at the new 616px intrinsic width.

**Specs (`components`), 19 new assertions, mutation-checked.**
`cleansia-brand-name.component.spec.ts` reads the ten shipped brand files off disk and asserts magic
bytes against extension (PNG signature / `RIFF`+`WEBP` / ICO reserved+type), decodes `Logo.png` with a
40-line inflate+unfilter reader (no image library ships in this workspace) and asserts its **single**
visible ink colour equals `--cleansia-primary` parsed out of `variables.scss`, asserts the three apps
ship one mark, and asserts the component emits no text node beside the wordmark plus `alt="Cleansia"`.
`cleansia-sidebar-menu.component.spec.ts` covers `brandCompact` across collapsed/expanded and the
mobile exclusion. Both mutations were run: copying PNG bytes over `Logo.webp` fails the magic-byte
test, and a **1/255-per-channel** ink drift (`#0284c7`→`#0385c8`) fails the colour test. Assets
restored and re-verified by sha256 afterwards.

Harvested into `agents/knowledge/patterns-frontend.md` → "Brand mark — `cleansia-brand-name` is the
whole lockup": no text beside the wordmark, `compact` as the only variant, bytes-match-extension, and
width-driven sizing for non-square logos.

## Review
<!-- reviewer writes verdict here -->
