---
id: T-0400
title: "Deployed web cookie auth requires same-site custom domains — dev/prod frontends and APIs must share the cleansia.cz site (until then, deployed-dev web URLs cannot authenticate — by design)"
status: blocked
size: M
owner: architect
created: 2026-07-11
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, backend, frontend]
security_touching: true
priority: medium
manual_steps:
  - "owner: DNS records for the chosen subdomains under cleansia.cz (frontends + APIs, per environment)"
  - "owner: add the final web origins to the Google OAuth client (GSI 'origin not allowed' 403s — IMP-1)"
sprint: 12
source: phase/web-fix1 — the deployed-web 401 root cause (owner chose the local devremote workflow for dev; this ticket is the deployed-web enabler)
---

> **Decision context (phase/web-fix1).** The web apps authenticate with HttpOnly `SameSite=Strict` cookies —
> correct security, but cookies then only flow when the frontend and API share a SITE (registrable domain).
> The deployed-dev topology (SWAs on `*.azurestaticapps.net`, SSR + APIs on `*.azurewebsites.net`) is
> **cross-site everywhere** (both suffixes are on the Public Suffix List), so deployed-dev browser auth is
> impossible regardless of CORS — 401 on every call. The owner decided (2026-07-11): dev web testing uses the
> **local devremote proxy** (`npx nx serve <app> --configuration=devremote`, shipped in phase/web-fix1);
> **no SWA Standard-SKU upgrade** (~$18/mo) for dev. The deployed-dev web URLs therefore stay auth-broken
> **by design** until same-site domains exist.

## Context
- Production appsettings already assume the same-site shape: `admin.cleansia.cz` / `partner.cleansia.cz` /
  `cleansia.cz` origins vs `api-*.cleansia.cz` APIs (per `appsettings.Production.json` + `environment.prod.ts`)
  — `SameSite=Strict` works across subdomains of one registrable domain.
- What is missing is the actual custom-domain wiring: bicep `customDomains` on the App Services/SWAs, managed
  certificates, DNS records, and (if dev-deployed web is ever wanted) a `dev-*` subdomain set.
- Alternatives considered and rejected for now: SWA Standard + linked backends (~$18/mo, dev-only benefit);
  `SameSite=None` (dead on arrival — Chrome third-party-cookie blocking, and a needless security downgrade).

## Acceptance criteria
- [ ] **AC1 (architecture)** — architect ratifies the target domain topology per environment (prod required;
  dev optional) and records it (ADR or an addendum to ADR-0015): frontends + APIs same-site under
  `cleansia.cz`, `SameSite=Strict` untouched.
- [ ] **AC2 (infra)** — bicep adds custom domains + managed certs for the chosen hosts; deploy workflows pass
  the hostnames; `browserCorsOrigins` and `customerWebBaseUrl` switch to the custom domains.
- [ ] **AC3 (frontend)** — the deployed build configs point at the same-site API origins (or relative URLs if
  fronted same-origin); prod `environment.prod.ts` values verified against the final topology.
- [ ] **AC4 (verify)** — a deployed login on the custom-domain frontend sets the cookie and authenticated
  calls succeed in a stock browser (no third-party-cookie exceptions).

## Out of scope
- The local dev workflow (shipped: same-origin dev proxy + devremote in phase/web-fix1).
- Google OAuth origin config (owner manual, IMP-1).

## Status log
- 2026-07-11 — filed `proposed` from the phase/web-fix1 root cause + owner decision (devremote for dev; no SWA
  SKU spend). This ticket is the enabler for ANY deployed web URL to authenticate.
- 2026-07-15 — **AC2 infra enabler authored, default-off** (zero behavior change until a param file opts in):
  `customDomains` param in `main.bicep` (map of host token → hostname, default `{}`) →
  `modules/appServiceCustomDomain.bicep` (hostname binding → free managed cert → SNI flip, the two-phase
  sequenced in ONE deployment via the nested `appServiceSniBinding.bicep`) + `staticWebApp.bicep` grew an
  optional `customDomain` (cname-delegation; SWA manages its own TLS). Setting a frontend key auto-aligns
  platform CORS + app-level `CorsOrigins__n` + `customerWebBaseUrl` (SendGrid links, Stripe redirects).
  Recommended per-env hostnames live commented-out in `weu.dev.bicepparam`/`weu.prod.bicepparam`; owner
  runbook (DNS records, deploy sequence, Google OAuth origins) = `deploy/AZURE-DEV-RUNBOOK.md` §12.
  No workflow change needed — the hostnames ride the existing `.bicepparam` files.
  **Flag for AC1 (architect):** admin `environment.prod.ts` sends auth to `api.cleansia.cz` (the PARTNER
  API) whose committed prod `CorsOrigins` lacks `admin.cleansia.cz` — ratify or fix that pairing before
  prod cut-over. AC3 (deployed web build configs) untouched: `environment.staging.ts` still targets the
  `azurewebsites.net` hosts (correct for devremote; a deployed-dev web build needs a same-site config).
- 2026-07-15 — reviewer verdict CHANGES → fixed (tracker tokens stripped from bicep source comments;
  `@minLength(1)` on the module `hostname` params; ticket front-matter date; conditional-evaluation
  comment reworded). **Reviewer's mandatory gate before anyone uncomments a `customDomains` param
  block:** run a SERVER-SIDE what-if with a POPULATED `customDomains` param first (e.g. dispatch
  Deploy to DEV → mode=`what-if` with the param block uncommented on a branch, or
  `az deployment group what-if … --parameters 'customDomains={"ssr":"dev.cleansia.cz"}'`) — the opt-in
  `toObject`/lambda path (`corsOriginsAppSettings`, main.bicep) is compiler-unverified locally (no
  bicep CLI on the authoring machine; binary download denied), so the default-off template is the only
  path exercised until a server-side compile proves the populated one.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **PARKED by the owner** (proposed → blocked): domains are not bought yet and a rebrand of
  "Cleansia" is possible — custom domains are explicitly NOT a concern right now. Verified dormant:
  `customDomains` defaults to `{}` in main.bicep and is commented out in BOTH bicepparam files — zero
  effect on any deploy until the owner uncomments and fills it. The enabler stays authored and ready.
  This also keeps the T-0409 admin-TTL PROD cut-over parked (its code is shipped; dev unaffected).
