---
id: T-0123
title: "Prod-config hardening: CSRF-in-prod (BSP-3) + Swagger-off-staging (BSP-5) + anon tenant batch (BSP-9)"
status: done
size: S
owner: â€”
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0115, T-0116]
blocks: []
stories: []
adrs: [0003]
layers: [config]
security_touching: true
manual_steps: []
sprint: 0
source: findings BSP-3/BSP-5/BSP-9
---

## Context
Prod-config hardening bundle from the 2026-06-01 audit (PROD-CONFIG row in
`agents/backlog/TICKET-MAP.md:55` / `agents/backlog/audits/AUDIT-2026-06-01-execution-plan.md:130`).
Three related production-posture findings, all in shared startup/config surface:

- **BSP-3 â€” CSRF disabled in prod** (major; `findings.json:3415-3428`, `slice-reports.md:2782-2787`).
  All three cookie-auth hosts ship `"Csrf": { "Enabled": false }` in prod
  (`src/Cleansia.Web.Customer/appsettings.Production.json:24-27`, and the matching Admin / Partner
  prod files). `CsrfValidationMiddleware.InvokeAsync` short-circuits when `!_options.Enabled`
  (`src/Cleansia.Config/Authentication/CsrfValidationMiddleware.cs:48`), so with HttpOnly-cookie auth
  + CORS `AllowCredentials()`, every state-changing endpoint is reachable cross-site on the victim's
  ambient cookie. CORS hides the *response* but does not block the side-effecting request.
- **BSP-5 â€” Swagger on every non-Production env** (minor; `findings.json:3445-3457`,
  `slice-reports.md:2796-2801`). `if (!env.IsProduction()) { UseSwagger(); UseSwaggerUI(...) }`
  (`src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:110-118`) publishes the full API surface
  + schemas on Staging/QA/Demo and on any mis-set `ASPNETCORE_ENVIRONMENT`. Fragile single-point
  dependency on the env string.
- **BSP-9 â€” anonymous `Order/LookupBatch`** (minor, `isGap: true` â€” verify-then-harden;
  `findings.json:3501-3513`, `slice-reports.md:2824-2827`). `[AllowAnonymous] POST LookupBatch`
  (`src/Cleansia.Web.Customer/Controllers/OrderController.cs:29-37`) must enforce the same per-item
  shared-secret and a batch cap as single `Lookup`, since anonymous = no tenant claim (S3, bypasses
  the global tenant filter).

## Acceptance criteria
- [ ] **AC1 (BSP-3, owner-flip + verify)** â€” Given a production-like config, When a state-changing
  cookie-auth request is sent without an `X-CSRF-Token` header, Then it returns **403** (the
  middleware no longer short-circuits, i.e. `Csrf:Enabled=true` is in effect). The three prod
  appsettings (`Cleansia.Web.Customer`, `Cleansia.Web.Admin`, `Cleansia.Web.Partner`
  `appsettings.Production.json`) carry `"Csrf": { "Enabled": true }`; `Csrf:Secret` stays
  `SET_VIA_SECRETS` (provisioned by the owner, never committed). An integration test in a
  production-like config proves the 403.
- [ ] **AC2 (BSP-5, fail-closed gate)** â€” Given the app boots in any environment **other than
  `Development`** (Staging, QA, Demo, Production, or an unrecognized env string), Then Swagger /
  SwaggerUI is **not** mounted (the `!env.IsProduction()` check at `CleansiaStartupBase.cs:110` is
  replaced by an explicit `env.IsDevelopment()` allow-list). Given `Development`, Swagger still
  serves (DX preserved).
- [ ] **AC3 (BSP-5, startup guard)** â€” Given Swagger would be served (Development branch) **but**
  `CorsOrigins` contains a public `cleansia.cz` origin, Then startup **refuses to boot** (throws a
  clear config-guard exception) â€” so a prod-shaped config can never expose Swagger even if the env
  string is mis-set. A test asserts boot throws in that combination and boots clean otherwise.
- [ ] **AC4 (BSP-9, verify + harden)** â€” Given the anonymous batch lookup, Then it is confirmed (with
  a test) that `LookupOrderBatch.Handler` (a) caps the batch (rejects `> 10` items â€”
  `LookupOrderBatch.cs:23`) and (b) returns **only** rows whose `(OrderId, Email)` matches the
  supplied per-item secret (`LookupOrderBatch.cs:42-47`); plus the two concrete hardening fixes:
  null/empty `Email` items are dropped before `.ToLower()` (no NRE at `LookupOrderBatch.cs:43`), and
  the `OrderLookupItem.OrderId`-vs-`o.Id` matching is confirmed consistent with the single
  `LookupOrder` secret pair (single matches on `DisplayOrderNumber` + email at `LookupOrder.cs:51-53`;
  batch must not silently widen the secret).
- [ ] **AC5 (no anonymous tenant leak)** â€” Given an anonymous batch request whose items reference
  orders the caller cannot prove the email for, Then no order data for those items is returned (the
  email-match `security check` is the only gate; an item with a wrong/absent email yields nothing).

## Out of scope
- **BSP-4** (partitioned rate limiter + `UseForwardedHeaders` + guard, ADR-0003) â€” its own ticket
  **T-0115**; this ticket depends on it and rebases on the post-BSP-4 `CleansiaStartupBase.cs`.
- **SEC-W3** (webhook per-IP egress window) â€” separate ticket, also in the StartupBase cluster.
- Rotating / provisioning the actual `Csrf:Secret` value â€” **owner-only secret op**; this ticket only
  flips the non-secret `Enabled` flag and keeps the `SET_VIA_SECRETS` placeholder.
- Any change to the JWT signing-key split (BSP-8) or the `PolicyBuilder` fail-closed work (BSP-1/6).
- Re-architecting the anonymous lookup contract (e.g. per-item confirmation code) â€” verify + the two
  named hardening fixes only.

## Implementation notes
- **TEST-FIRST per `agents/knowledge/testing.md`** â€” write the failing checks first: the CSRF-403
  integration test (AC1), the boot-guard test (AC3), and the batch verify/harden tests (AC4/AC5);
  they land in the same merge as the config change.
- **Governing ADR: ADR-0003** (`agents/backlog/adr/0003-partitioned-rate-limiting.md`). It owns the
  `CleansiaStartupBase.Configure` pipeline (the Swagger block at `:110-118` lives inside the same
  pipeline ADR-0003 reorders, and ADR-0003 documents the **startup-guard** pattern â€” D3 â€” reused for
  AC3). The Swagger gate must sit at the ADR-0003 pipeline position
  (`â€¦ EnableBuffering â†’ UseForwardedHeaders â†’ [Swagger if Development] â†’ RequestLogging â€¦`).
- **SERIALIZATION CLUSTER â€” yes.** Per `agents/backlog/TICKET-MAP.md:22`, `CleansiaStartupBase.cs`
  is a strict-order cluster: **BSP-4 (T-0115) â†’ SEC-W3 â†’ PROD-CONFIG (this ticket, BSP-5 hop)**. This
  ticket edits `CleansiaStartupBase.cs:110-118` and **must not run concurrently** with T-0115 or the
  SEC-W3 ticket â€” it runs **last** in the cluster, after T-0115 is `done` (hence `depends_on:
  [T-0115]`) and rebased on its pipeline edits.
- **Files touched:**
  - `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:110-118` â€” Swagger gate (BSP-5 AC2) +
    boot guard (AC3). Cluster-serialized.
  - `src/Cleansia.Web.{Customer,Admin,Partner}/appsettings.Production.json` (the `Csrf` block,
    e.g. Customer `:24-27`) â€” `Enabled: true`, keep `Secret: SET_VIA_SECRETS` (BSP-3 AC1). These are
    **not** in the cluster; safe to edit in parallel with nothing else here.
  - `src/Cleansia.Core.AppServices/Features/Orders/LookupOrderBatch.cs:22-47` â€” null-email drop +
    secret-pair consistency (BSP-9 AC4). Cross-check `LookupOrder.cs:51-53`.
- **No manual_steps.** `Csrf:Enabled` is a non-secret flag the developer edits; the `Secret` value is
  already a `SET_VIA_SECRETS` placeholder the owner provisions out-of-band (note it to the owner at
  hand-off, but it is not a Claude-run step and is not an ef-migration/nswag-regen). No DTO/endpoint
  shape changes, so no nswag-regen.
- **Routing:** config-only ticket. backend/config dev instance edits the three surfaces; spawn a
  **reviewer in parallel** with the developer (same ticket). `security_touching: true` â†’ the
  **Security gate is mandatory** before `done` (it confirms the CSRF 403, the Swagger fail-closed
  posture, and the anonymous-lookup secret enforcement). Then QA.

## Status log
- 2026-06-01 00:00 â€” draft (created by pm)
- 2026-06-04 - in_progress (backend/config). TEST-FIRST per knowledge/testing.md. Last member of the
  CleansiaStartupBase cluster (T-0115 + T-0116 done). Wrote failing tests first (red): CSRF-403 in a
  production-like host config (AC1), Swagger boot-guard throws on Development + public cleansia.cz CORS
  and boots clean otherwise (AC3), batch null-email drop + secret-pair gate + no-anon-leak (AC4/AC5).
- 2026-06-04 - done (red -> green), config-only, NOT committed. RED baseline: 10 failing (AC2 Swagger
  allow-list x5 envs, AC3 boot-guard x4 public origins, AC4 null-email NRE x1) for the right reasons
  (old !IsProduction gate, no boot guard, i.Email.ToLower() NRE at LookupOrderBatch.cs:43). GREEN: all
  24 new tests pass; full Cleansia.Tests suite 373 passed / 0 failed. Implemented:
  * AC1 (BSP-3): flipped "Csrf":{"Enabled":true} in all 3 appsettings.Production.json (Customer/Admin/
    Partner); "Secret":"SET_VIA_SECRETS" kept (owner provisions out-of-band - no secret committed).
    CsrfValidationMiddleware already enforces when Enabled (verified: 403 csrf.header_missing).
  * AC2/AC3 (BSP-5): replaced the !env.IsProduction() Swagger gate at CleansiaStartupBase with
    SwaggerShouldServe(env) == IsDevelopment-only + a pure ADR-0003 D3 boot guard GuardSwaggerExposure
    (throws InvalidOperationException when Swagger would serve AND CorsOrigins has a public cleansia.cz
    origin). Gate kept at the ADR-0003 pipeline position (UseForwardedHeaders -> [Swagger if Dev] ->
    RequestLogging). PipelineOrderTests still green.
  * AC4/AC5 (BSP-9): LookupOrderBatch.Handler now drops null/empty OrderId|Email items BEFORE
    i.Email.ToLower() (no NRE); confirmed (and documented inline) the batch (OrderId=internal GUID,
    Email) secret pair is the SAME pairing as single LookupOrder (both gate on lower-cased email; the
    GUID Id is obtained only by first proving DisplayOrderNumber+email via single Lookup - no
    secret-widening). Wrong/absent email -> nothing (no anon tenant leak).
  No EF migration, no NSwag (no DTO/endpoint shape change). BSP-4 rate limiter / UseForwardedHeaders /
  SEC-W3 webhook policy untouched. NOTE for owner: provision the real Csrf:Secret out-of-band.

## Review
**Reviewer — APPROVED + Security — PASS (2026-06-04).** BSP-3: all 3 prod appsettings `Csrf:Enabled=true`,
`Secret` stays `SET_VIA_SECRETS` (no secret committed); CSRF-403 test + disabled-control test prove the fix and
pin the old hole. BSP-5: Swagger gate now `IsDevelopment()` allow-list at the ADR-0003 position (6 non-Dev envs
fail closed) + boot guard throws on Dev+public-CORS. BSP-9: `LookupOrderBatch` drops null/empty Email (NRE gone),
caps at 10, same `(Order.Id GUID, email)` secret pair as single Lookup (dev traced the frontend: the GUID is
obtained only by first proving `(DisplayOrderNumber, email)` — equal-or-stronger, not widened); wrong-email
yields nothing. BSP-4/SEC-W3 untouched. No migration, no nswag.

**Verification (orchestrator, independent):** 3/3 prod files `Csrf:Enabled=true` + placeholder intact + no
real-secret leak; Swagger gate = `IsDevelopment` (old `!IsProduction` gone) + boot guard present. `dotnet build`
0 errors; `dotnet test Cleansia.Tests` = **373 passed / 0 failed** (+24). ⚠️ Owner: provision the real
`Csrf:Secret` (App Service / Key Vault) for all 3 hosts before prod deploy (CsrfTokenService throws on empty
secret once Enabled). Not committed.

- 2026-06-04 — done (reviewer APPROVED + security PASS; 373 tests; independently re-verified). **★ CleansiaStartupBase
  cluster COMPLETE: BSP-4 → SEC-W3 → PROD-CONFIG all landed.** NOT committed.
