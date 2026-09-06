---
id: T-0681
title: Production never serves SSR — the customer site is client-rendered, measured at 35 vs 73
status: todo
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

- [ ] **AC0** — Given production as it stands today, When the landing page is fetched, Then confirm
      it really returns the ~12 KB shell. **If production is already SSR-ing, this ticket is void** —
      the premise could not be checked from here because `cleansia.cz` currently answers with an
      Azure Static Web Apps 404 and is not bound to the SSR app at all.
- [ ] **AC1** — Given the SSR site's app settings, Then `NG_ALLOWED_HOSTS` lists every hostname that
      can reach it, including the staging slot the deploy workflow's warm probe hits.
- [ ] **AC2** — Given a request carrying `X-Forwarded-For`, Then the response is the full SSR
      document, not the shell.
- [ ] **AC3** — Given an unlisted host, Then the failure is understood and accepted as a 400.

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

## Review

<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
