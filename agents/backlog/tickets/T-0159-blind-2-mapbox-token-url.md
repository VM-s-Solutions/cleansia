---
id: T-0159
title: "BLIND-2: Mapbox access token exposed in request URL query → use correct Mapbox auth + scrub from logs + rotate token"
status: ready
size: S
owner: frontend
created: 2026-06-05
updated: 2026-06-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend, config]
security_touching: true
manual_steps: [rotate-mapbox-token]
sprint: 1
source: BLIND-2 (audit blindspot, findings line 61); filed Wave 1 per owner 2026-06-05
---

## Context
**BLIND-2** — the Mapbox access token is placed in the **request URL query string**, so it leaks into
every place a URL is recorded: browser history, referrer headers, CDN/proxy access logs, server access
logs, APM/distributed traces, and analytics. A token in a URL is a credential exposure even when the
token is a "public" Mapbox token, because anyone who reads the logs can lift it and run it against the
quota/billing of the account.

Verified leak site (frontend): `libs/core/services/src/lib/services/mapbox-autocomplete.service.ts:115-116`
builds the geocoding GET with `new HttpParams().set('access_token', this.accessToken)` on
`https://api.mapbox.com/geocoding/v5/mapbox.places/...`. The token is the `access_token` **query
parameter** on a third-party GET, so it is visible in the request URL.

The token is wired through Angular DI from each app's `environment.ts`
(`MAPBOX_ACCESS_TOKEN` injection token, `mapbox-autocomplete.service.ts:20`; provided in
`apps/cleansia.app/src/app/app.config.ts:96` and `apps/cleansia-partner.app/src/app/app.config.ts:92`).
This is **Wave 1, Batch 1B, independent** (no ADR dependency): it is a discrete security hardening of the
client-side geocoding call. The server-side analog (`Cleansia.Infra.Services` `MapboxGeocodingService`,
already on `IHttpClientFactory`) is touched by the integration ADR/clients work (T-0141/T-0144) — this
ticket coordinates with that surface but is scoped to the client-side/log exposure the owner filed.

## Acceptance criteria
- [ ] **AC1 (token not in URL)** — Given the Mapbox geocoding/autocomplete call, When a request is
  issued, Then the access token is **not present in the request URL/query string**. A test (or a
  recorded request assertion) proves the outgoing URL contains no `access_token` query parameter.
- [ ] **AC2 (correct Mapbox auth mechanism)** — Given Mapbox's supported auth, When the call is made,
  Then the token is supplied via the mechanism Mapbox supports that keeps it out of the URL (e.g. the
  `Authorization` header where the Search/Geocoding API accepts it, or a same-origin proxy endpoint that
  attaches the token server-side) — chosen and documented in the ticket, not left to ad-hoc.
- [ ] **AC3 (no token in logs/traces)** — Given the chosen mechanism, When a request is logged or
  traced (frontend HTTP interceptor / any server log if a proxy is introduced), Then the token does not
  appear in any logged URL, header dump, or trace span. If a header is used, sensitive-header redaction
  is confirmed in the logging path.
- [ ] **AC4 (token rotation flagged)** — Given the exposed token must be considered compromised, When
  the owner is handed the `manual_steps`, Then a `MANUAL_STEP: rotate-mapbox-token` note instructs the
  owner to **rotate** the exposed Mapbox token in the Mapbox account and update the
  `environment.*.ts` / config secret. Claude does NOT rotate credentials.
- [ ] **AC5 (no broken geocoding)** — Given the change, When address autocomplete runs, Then suggestions
  still resolve correctly across the configured `country`/`language`/`types` params (no functional
  regression of the geocoding feature). The existing `isConfigured`/no-token-hides-UI behavior is
  preserved.
- [ ] **AC6 (test-first + security gate)** — Each behavioral AC maps to a test written before/with the
  implementation (red→green, visible in commit order / status log) per `agents/knowledge/testing.md`;
  `security_touching: true` → the **Security gate is mandatory** before `done`.

## Out of scope
- The server-side `MapboxGeocodingService` `IHttpClientFactory` migration / 429 handling — owned by the
  integration ADR/clients work (T-0141 / T-0144 / Wave-2 BLIND-7). This ticket coordinates with, but does
  not redo, that surface.
- Replacing Mapbox or changing the geocoding provider.
- The `mapbox_coords_required` UX validation message (already in all 5 locales) — unchanged.

## Implementation notes
- **Independent in Batch 1B — no ADR dependency.** May start as soon as Wave 1 opens. Spawn a reviewer
  in parallel with the developer; run the **security** gate (credential-exposure finding) before `done`.
- **DECISION MADE (mechanism): thin same-origin proxy — NOT the `Authorization` header.**
  Rationale: the Mapbox Geocoding REST endpoints actually in use (v5 `mapbox.places` and v6
  `search/geocode/v6/forward`) authenticate **only** via the `access_token` query parameter; they do
  **not** honor an `Authorization` header. A header therefore cannot keep the token off the URL on the
  third-party call. The conforming fix is a thin same-origin proxy that injects the token server-side
  and is excluded from URL logging, so the browser sends NO credential and the token never appears in
  any browser-visible or third-party URL. The "lighter" header option does not exist for this endpoint,
  so the proxy is the only mechanism the endpoint supports — chosen accordingly.
  - **Where it lives:** Customer app (SSR) → proxy is an Express route in `apps/cleansia.app/server.ts`
    (`GET /api/mapbox/geocode`), reading the server-only `process.env.MAPBOX_TOKEN`; it forwards only
    the allow-listed `q/country/types/language/limit/autocomplete` params and never logs the
    token-bearing upstream URL. Partner app (SPA) → frontend points `MAPBOX_PROXY_PATH` at the partner
    API (`<apiBaseUrl>/api/mapbox/geocode`); **the partner-API server endpoint is a backend MANUAL_STEP**
    (kept OFF `MapboxGeocodingService.cs` to avoid the T-0144 collision — different host endpoint).
  - **Frontend:** `mapbox-autocomplete.service.ts` no longer holds or sends the token. It GETs the
    same-origin proxy with token-free params. `isConfigured`/no-token-hides-UI is preserved via the
    token-free `MAPBOX_AUTOCOMPLETE_ENABLED` boolean (`!!environment.mapboxToken`), so the token is
    never shipped to the browser bundle while the UI-toggle behavior is unchanged.
- **Owner-only:** rotating the Mapbox token is a credential action — `manual_step: rotate-mapbox-token`.
- **Serialization:** the leak site (`mapbox-autocomplete.service.ts`) is not in any TICKET-MAP shared-file
  cluster → collision-free with the rest of Batch 1B. If a server proxy is added, keep it off the
  `MapboxGeocodingService.cs` file that T-0144 edits (different host endpoint) to avoid a collision; if
  unavoidable, serialize against T-0144.
- Anchors: `libs/core/services/src/lib/services/mapbox-autocomplete.service.ts:113-126` (URL + params),
  `:20` (`MAPBOX_ACCESS_TOKEN`); `apps/cleansia.app/src/app/app.config.ts:96`,
  `apps/cleansia-partner.app/src/app/app.config.ts:92` (DI wiring);
  `apps/*/src/environments/environment.*.ts` (token source).

## Status log
- 2026-06-05 — draft (created by pm; BLIND-2 filed into Wave 1 Batch 1B per owner; security_touching)
- 2026-06-06 — ready (Batch 1B; independent — no ADR dep; routed to frontend + config, **security gate
  mandatory** (security_touching); reviewer + security in parallel. **Owner manual_step: rotate-mapbox-token**
  — flagged; the rotation is owner-only and held for owner action, but the code fix (correct Mapbox auth +
  log scrub) is merge-safe ahead of rotation).
- 2026-06-06 — implemented test-first (frontend). Mechanism DECIDED + documented: same-origin proxy
  (the geocoding endpoint accepts only the `access_token` query param, no `Authorization` header — so a
  header is not an option; the proxy is the conforming/lighter fix the endpoint supports). Frontend
  `mapbox-autocomplete.service.ts` is now token-free and calls `MAPBOX_PROXY_PATH`; customer SSR proxy
  added in `server.ts`; partner SPA points at the partner-API proxy. AC1/AC2/AC3/AC5 covered by
  `mapbox-autocomplete.service.spec.ts` (no `access_token`/`pk.ey` in url/urlWithParams/headers; no
  `Authorization` header; never hits `api.mapbox.com` from the browser; country/language/types/limit/
  autocomplete params + parsing + `isConfigured`/no-token-hides-UI preserved). Misleading
  "paste the token here" comments in customer + partner prod/staging `environment.*.ts` corrected to the
  token-free enable-flag model with the rotation MANUAL_STEP. **AC4 (rotate-mapbox-token) remains owner
  manual_step — NOT performed by Claude; manual_step note confirmed present in frontmatter and env files.**
  **Backend MANUAL_STEP:** partner-API `/api/mapbox/geocode` proxy endpoint (off `MapboxGeocodingService.cs`
  / T-0144). nx lint + nx test results recorded in the developer report.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
