# Cleansia Frontend — Claude Code Guide

This is the Angular 19 / Nx monorepo for the 3 web apps (customer SSR, partner SPA, admin SPA).

**The canonical project guide is the root [`../../CLAUDE.md`](../../CLAUDE.md)** — read it for the
full architecture, conventions, the agent operating system, and the i18n/NSwag/owner-only rules.

## Frontend specifics

- Patterns & conventions for this codebase: [`../../agents/knowledge/patterns-frontend.md`](../../agents/knowledge/patterns-frontend.md)
- Canonical architecture: [`../../docs/architecture/frontend.md`](../../docs/architecture/frontend.md)
- The Frontend Dev charter: [`../../.claude/agents/frontend.md`](../../.claude/agents/frontend.md)

Key rules (full list in the catalog): OnPush + signals, logic in facades not components,
`<cleansia-*>`/PrimeNG (never raw form controls), `TranslatePipe` on every string with keys in all 5
locales, no `any`, three explicit data states. Never run `npm run generate-*-client` or hand-edit the
NSwag-generated clients — that's owner-only; flag `manual_step: nswag-regen`.

## Local dev & the same-origin /api proxy

Auth is HttpOnly-cookie based with `SameSite=Strict`, so the browser must see **one origin**: the
cookie never rides to a different site, and cross-site cookie auth is dead anyway under third-party
cookie blocking. Dev builds therefore use a **relative** `apiBaseUrl: ''` and the Angular dev server
proxies `/api` server-side (proxies are server-to-server — SameSite and platform CORS never apply).
Never "fix" a dev 401 by putting an absolute API URL back into `environment.ts` or by weakening the
cookie attributes.

Two run modes per app (ports: partner `:4200`, admin `:4201`, customer `:4202`):

```bash
# 1. Local backend (run the API first, e.g. dotnet run --project src/Cleansia.Web.Admin)
npx nx serve cleansia-partner.app        # /api → http://localhost:5000
npx nx serve cleansia-admin.app          # /api → http://localhost:5001
npx nx serve cleansia.app                # /api → http://localhost:5003

# 2. Deployed dev backend (no local API needed)
npx nx serve cleansia-partner.app --configuration=devremote   # /api → api-cleansia-partner-weu-dev
npx nx serve cleansia-admin.app --configuration=devremote     # /api → api-cleansia-admin-weu-dev
npx nx serve cleansia.app --configuration=devremote           # /api → api-cleansia-customer-weu-dev
```

Proxy targets live in `apps/<app>/proxy.conf.json` (local) and `apps/<app>/proxy.devremote.conf.json`
(deployed dev — keep in sync with `environment.staging.ts`). The customer proxy deliberately excludes
`/api/mapbox` — that endpoint is served by the SSR express server (`server.ts`), not the backend. The
customer SSR render resolves the relative base URL against the incoming request origin
(`app.config.server.ts`), so server-side fetches also flow through the proxy. Note:
`npm run start:cleansia-ssr` (built server.mjs on :4000) has no `/api` proxy — use `nx serve` for
API-backed dev.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `python3 -c "from graphify.watch import _rebuild_code; from pathlib import Path; _rebuild_code(Path('.'))"` to keep the graph current
