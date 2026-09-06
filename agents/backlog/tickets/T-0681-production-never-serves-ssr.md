---
id: T-0681
title: Production never serves SSR — the customer site is client-rendered, measured at 35 vs 73
status: done
size: S
owner: —
created: 2026-09-06
updated: 2026-09-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

**The customer app is SSR and has never served an SSR response outside a developer's laptop.** Every
production request falls back to client-side rendering, silently, with a 200.

Measured on a real production build (Lighthouse 13.4.1, Playwright Chromium, mobile preset — Slow 4G,
4× CPU) against the actual SSR Node server:

| | score | FCP | LCP | TBT | CLS |
|---|---|---|---|---|---|
| SSR actually rendering | **73** | 3.0 s | 5.0 s | 200 ms | 0.016 |
| the configuration production runs | **35** | 3.9 s | 6.6 s | 370 ms | 0.900 |

Same build, same machine, same run settings. The SSR document is 227,446 bytes; the fallback shell is
12,182 bytes with a literally empty `<app-root></app-root>`. The owner's reported 60-65 sits between
the two, which is what a real network and a mixed cache state would give.

### Two independent causes — fixing either alone still leaves the page client-rendered

**(a) `allowedHosts` is empty and nothing ever fills it.**

`apps/cleansia.app/server.ts:25` — `ɵsetAngularAppEngineManifest({ allowedHosts: [], ...engineManifest.default })`,
and the built manifest emits `allowedHosts: []` too. The comment on the line above says *"Hosts are
authorized at runtime via the NG_ALLOWED_HOSTS env var."*

**`NG_ALLOWED_HOSTS` appears exactly once in this repository — in that comment.** It is set in no
environment; `deploy/bicep/main.bicep` gives the SSR App Service two app settings,
`APPLICATIONINSIGHTS_CONNECTION_STRING` and `MAPBOX_TOKEN`. When `allowedHosts` is empty,
`@angular/ssr` logs and returns the CSR page.

**(b) Azure's `X-Forwarded-*` headers force CSR even once (a) is fixed.**

Measured with `NG_ALLOWED_HOSTS` correctly set: a plain request returns 227,446 bytes; the same
request carrying `X-Forwarded-For` returns 12,182. `@angular/ssr` sets `deoptToCSR` for any
un-allowed `x-forwarded-*` header. Azure App Service injects `X-Forwarded-For` and
`X-Forwarded-Proto` on **every** request. `trustProxyHeaders` is configured nowhere.

### Why this is a ticket and not a remark

`security_touching: true`, and the reason matters: **a non-empty `allowedHosts` turns an unlisted host
into a hard 400, not a CSR fallback.** Today's failure is slow-but-working; a wrong host list is
down. The list must be complete and verified before it ships, which is exactly the kind of change
that must not ride along inside a large feature PR.

## Acceptance criteria

- [x] **AC0** — The premise could not be checked against the live site: `cleansia.cz` answers with an
      Azure Static Web Apps 404 and is not bound to the SSR app. It was instead reproduced locally
      against the built server, which is the same artefact production runs — see the measurements
      below. **The owner should still confirm which host he measured at 60-65**; if that deployment
      already SSRs, the gain there will be smaller than 38 points.
- [x] **AC1** — Given the SSR site's app settings, Then `NG_ALLOWED_HOSTS` lists every hostname that
      can reach it, including the staging slot the deploy workflow's warm probe hits.
- [x] **AC2** — Given a request carrying `X-Forwarded-For`, Then the response is the full SSR
      document, not the shell.
- [x] **AC3** — Given an unlisted host, Then the failure is understood and accepted as a 400.

## Out of scope

- **Editing `server.ts:25` or `:28`.** `@angular/ssr` reads `NG_ALLOWED_HOSTS` and
  `NG_TRUST_PROXY_HEADERS` itself. Adding code there is a new maintenance surface for behaviour that
  already exists.
- **`trustProxyHeaders: true`, or trusting all five `x-forwarded-*` headers.** Only `x-forwarded-for`
  is missing from the default; trusting `x-forwarded-prefix`/`-port` widens the SSRF surface the
  Angular 19.2.16 fix exists to close.
- **Port entries in the host list** — `verifyHostAllowed` strips ports.
- **Micro-cache tuning.** Measured a non-factor: real SSR at 330 ms TTFB scored 73, the same document
  served statically at 10 ms scored 72. The cache already works (526 ms cold, 128-145 ms warm).

## What was measured, on the built production server

Serving `dist/apps/cleansia.app/server/server.mjs` directly:

| condition | document |
|---|---|
| no env vars — **today's production** | **12,182 bytes** (CSR shell) |
| no env vars + `X-Forwarded-For` | 12,182 bytes |
| both settings | **227,446 bytes** (full SSR) |
| both settings + `X-Forwarded-For` | 227,446 bytes |
| both settings + `X-Forwarded-For` + `-Proto` | 227,446 bytes |

**A correction to this ticket's own risk note.** It warned that a non-empty `allowedHosts` turns an
unlisted host into a hard 400. That could NOT be reproduced: with the settings applied, requests
carrying `Host: evil.example.com` still rendered. The failure mode may differ under Azure's proxying,
so the wildcard below is still deliberately complete rather than minimal — but the risk of this change
is lower than the ticket was filed on, not higher.

Two framework facts checked directly rather than taken on trust:
- `node.mjs` reads both variables itself via `getArrayFromEnv`, comma-separated and trimmed — which
  is why no code was added to `server.ts`.
- `isHostAllowed` treats a leading `*.` as a suffix match (`hostname.endsWith(allowedHost.slice(1))`),
  so one wildcard covers the default hostname and the `-staging` slot in every region and env.
- The framework's DEFAULT trusted set is already `x-forwarded-host` + `x-forwarded-proto`, so the
  setting adds exactly one header, `x-forwarded-for`, and widens nothing else.

## Implementation notes

`deploy/bicep/main.bicep`, the `ssr` module's appSettings, beside `MAPBOX_TOKEN`:

```
NG_ALLOWED_HOSTS: '<every hostname>,*.azurewebsites.net'
NG_TRUST_PROXY_HEADERS: 'x-forwarded-for,x-forwarded-host,x-forwarded-proto'
```

`*.azurewebsites.net` is a supported wildcard.

A related defect found alongside this is already **fixed** on the feature branch: the micro-cache's
`body.includes('cl-hero')` guard matched the CSR shell (its inlined critical CSS declares
`--cl-hero-1`), so a failed render was cached for 60 s. It now tests the empty `<app-root>`. That fix
becomes more important, not less, once this ticket lands.

## Status log

- 2026-09-06 — filed from a measured Lighthouse investigation of the customer home page.
- 2026-09-06 — the owner heard the outage concern and asked for it shipped. Two app settings added to
  the SSR module, and the `server.ts` comment that promised a mechanism nobody had wired now says
  where it actually lives. Verified end to end on the built server: 12,182 → 227,446 bytes.

## Review

<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
