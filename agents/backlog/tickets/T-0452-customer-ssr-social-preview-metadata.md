---
id: T-0452
title: Public customer site has no social-preview card — no og:image, no apple-touch-icon, no web manifest
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0444]
blocks: []
stories: []
adrs: []
layers: [architect, frontend]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

`cleansia.cz` is the only **public, SSR, unauthenticated** surface the platform has, and it is the one
a demo audience is most likely to share. Sharing it on WhatsApp, Slack, LinkedIn, iMessage or Telegram
today produces a **bare link with no preview card**.

**PM verification, 2026-07-30** — read `apps/cleansia.app/src/index.html` end to end (89 lines) and
grepped the whole customer app for the Angular `Meta` service:

| Tag | Present? | Evidence |
|---|---|---|
| `<title>` | yes — static `Cleansia` | `index.html:17` |
| `<meta name="description">` | yes — *"Cleansia - Professional cleaning services in Prague and surroundings. Book your cleaning today!"* | `index.html:20` |
| `og:image` / `og:title` / `og:description` / `og:url` / `og:type` | **absent** | no `og:` anywhere in `index.html`; no `Meta` service usage anywhere under `apps/cleansia.app/src/app/` |
| `twitter:card` | **absent** | same |
| `apple-touch-icon` | **absent** | only `<link rel="icon" … href="assets/logos/Logo.ico">` at `index.html:21` |
| web app manifest | **absent** | no `manifest.webmanifest`, no `<link rel="manifest">` |

**This is cheap now and was not before.** T-0444 (PR #172) landed a real brand mark: the customer app
now ships a 616×112 RGBA wordmark (`apps/cleansia.app/src/assets/logos/Logo.png`, sha1 `b303b295b302`,
shared byte-for-byte with admin), and a **1024×1024 master** exists at
`src/cleansia_ios/CleansiaCustomer/Resources/Assets.xcassets/AppIcon.appiconset/AppIcon1024.png`
(verified 1024×1024, 8-bit RGB). Before T-0444 the only source was a 48×48 PNG mislabelled `.webp`.

### On the scope question — apple-touch-icon and manifest: yes, same ticket

The brief asked whether they belong here. **They do**, for three grounded reasons:
1. **Same source, same pipeline.** All three artifacts are derivations of the one 1024 master that
   T-0444 established. Splitting them means re-deriving from the same PNG in two tickets and
   re-reviewing the same provenance twice.
2. **Same file, one lane.** All three are `<head>` entries in the single
   `apps/cleansia.app/src/index.html` (plus one `assets` glob in `apps/cleansia.app/project.json`).
   Two tickets on one `<head>` is a self-inflicted shared-file lane.
3. **The manifest is safe metadata here, not a PWA decision.** The customer app has **no service
   worker** — verified: no `serviceWorker` option in `apps/cleansia.app/project.json` and no
   `provideServiceWorker`/`ngsw` anywhere in `app.config.ts`. Chrome's install criteria require a
   service worker with a fetch handler, so a name+icons+theme_color manifest changes what iOS/Android
   "Add to Home Screen" *uses*, and nothing else. **If** the panel wants installability, that becomes
   its own ticket and an owner call — say so rather than smuggling it in.

## Acceptance criteria

- [ ] **AC1** — Given the production customer SSR build, When any public route is fetched, Then the
      served HTML carries `og:title`, `og:description`, `og:image`, `og:url`, `og:type` and
      `twitter:card`. Evidence: `curl` of the SSR-rendered HTML (not the dev server) with the tags
      quoted in `## Review`.
- [ ] **AC2** — Given `og:image`, When it is emitted, Then its value is an **absolute** URL
      (scheme + host), not a relative path — relative `og:image` is ignored by every major scraper.
      Evidence: the emitted value, plus the mechanism that resolves the origin under SSR named in
      `## Review`.
- [ ] **AC3** — Given the image asset, When it is inspected, Then it is **≥ 1200×630** and **< 5 MB**
      (the LinkedIn/WhatsApp floor and the Facebook ceiling), and it is derived from the T-0444 /
      `AppIcon1024` master rather than newly drawn. Evidence: `file(1)` output + the derivation
      command.
- [ ] **AC4** — Given an iOS Safari "Add to Home Screen", When it resolves the icon, Then a
      `180×180` `apple-touch-icon` is served and used. Evidence: the served path + a screenshot or the
      `<link>` plus a 200 on the asset URL.
- [ ] **AC5** — Given a web manifest ships, When it is fetched, Then it validates, declares
      `name` / `short_name` / `icons` / `theme_color` / `background_color`, and its `theme_color`
      **matches the brand token already in use** rather than a new hex. Evidence: the manifest body
      and the token it was read from.
- [ ] **AC6** — Given all three apps build, When `nx build … --configuration=production
      --skip-nx-cache` runs for `cleansia.app`, `cleansia-partner.app` and `cleansia-admin.app`, Then
      all three exit 0. (Non-negotiable since T-0438: a customer-app-only check is what let that
      break through.) Evidence: the three commands and exit codes.
- [ ] **AC7** — Gate 0.5 leg 3: state explicitly whether a **live scraper** was exercised (LinkedIn
      Post Inspector / Facebook Sharing Debugger / a real WhatsApp send) or only the emitted markup was
      inspected. Markup-only is an acceptable result; **claiming a preview renders without having seen
      one is not**.

## Out of scope

- The **partner** and **admin** apps. Both are authenticated SPAs behind a login; nobody shares a link
  to them and no scraper can reach a useful page.
- Per-route/per-service dynamic cards (e.g. a specific service page with its own image). If the panel
  chooses the per-route architecture it must still ship a **site-wide default** in this ticket;
  populating individual routes is a follow-up.
- `robots.txt` / `sitemap.xml` / structured data (JSON-LD `LocalBusiness`). Adjacent SEO work,
  different decision, different evidence. `robots.txt` already exists at
  `apps/cleansia.app/src/root-assets/robots.txt`.
- Making the app **installable** (service worker, offline). Explicitly excluded — see §3 above.

## Implementation notes

**Architect panel required before this leaves `draft`** — one decision, with a real trade-off:

> **Static `<head>` in `index.html` vs. per-route meta via Angular's `Meta`/`Title` services under SSR.**

Static is one commit and works for the root URL, but every shared deep link gets the same card, and
`og:url` cannot be correct for more than one route. Per-route is the right shape for an SSR site but
needs an origin resolution strategy — note that `app.config.server.ts` already resolves the relative
API base URL against the **incoming request origin** (documented in `src/Cleansia.App/CLAUDE.md`), so
the seam for AC2 probably exists; the panel must verify that rather than assume it. Record the ruling
in `agents/architecture/decisions/`.

**No analyst panel.** The card's *content* is derivable from code, not a new product decision: the
copy is the existing `<meta name="description">` at `index.html:20`, the title is the existing
`<title>`, and the mark is the wordmark the owner just ruled on twice in T-0444. If the panel
disagrees and wants new marketing copy, that is an escalation to the owner, not a PM default.

**Asset placement:** `apps/cleansia.app/project.json` currently copies only `robots.txt` and
`.well-known/**` to the site root (`targets.build.options.assets`). A root-served
`apple-touch-icon.png` / `manifest.webmanifest` needs a new glob entry there; an
`assets/`-served one needs an explicit `<link href>`. Pick one and say which.

**Shared-file lane:** `apps/cleansia.app/src/index.html` and `project.json` — no other sprint-14
ticket writes them. T-0447 (web avatar) is in the customer i18n + profile-component lane, which does
not intersect.

## Status log
- 2026-07-30 — draft (created by pm; wave-1 finding with no home, surfaced during T-0444; needs an
  architect panel on static-vs-per-route)

## Review
<!-- reviewer writes verdict here -->
