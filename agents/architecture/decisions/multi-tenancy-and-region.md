# Multi-tenancy and multi-region — how the two axes compose (living decision note)

> Companion to the **immutable** ADR-0017
> (`agents/backlog/adr/0017-multi-region-expansion-seam-and-its-composition-with-app-level-tenancy.md`). The ADR
> is the frozen decision; this file is the evolving composition note — the verified tenancy facts, the
> orthogonality, the seam, and the trigger. Cross-links: ADR-0015 (the Azure deployment this seam folds into),
> `architecture/decisions/azure-deployment.md` (the region parameterization), `patterns-backend.md` (the
> tenancy=app / region=infra rule).

## The one-sentence answer to the owner's question

**"Handle tenancy on the app level or the infra level?" — TENANCY IS APP (it already is, and it stays); REGION
IS INFRA (new).** They are **orthogonal** — tenancy answers *whose rows is this?* (a row filter), region
answers *which deployment/DB does this request hit?* (infra routing). They meet at exactly one small seam: a
**tenant→region (via country→region) map**. **For the market-expansion driver we ship ONE shared region + DB
now**, because the tenancy filter already separates tenants logically; the heavier **region-pinned** model is
one **named trigger** (a residency-regulated market or a latency SLA) away, reachable through the seam without
an app rewrite.

## Axis (a) — app-level multi-tenancy: VERIFIED against the code (unchanged for region)

| Fact | Evidence |
|---|---|
| `TenantId` is a **nullable string** (flexible key, not a GUID) | `Core.Domain/Common/ITenantEntity.cs:3-6` |
| Every `ITenantEntity` is auto-scoped by a **global query filter** (a loop over all entity types) | `CleansiaDbContext.ApplyTenantQueryFilters` (`:171-240`) |
| Filter body: `tenantProvider==null  ‖  (currentTenantId==null && e.TenantId==null)  ‖  e.TenantId==currentTenantId` | `:222-234` |
| **Single-tenant mode** = the `null==null` middle clause (without it, SQL `null==null` is NULL and hides every row in single-tenant / queue / webhook contexts) | `:209-217` |
| Tenant resolved from the **`tenant_id` JWT claim**, no header | `TenantProvider.cs:8` (`TenantClaimType="tenant_id"`), `:18-19` |
| Cross-tenant jobs/webhooks use **`SetTenantOverride`** / `IgnoreQueryFilters` | `TenantProvider.cs:22-25`; `CommitAsync` auto-stamps a new entity's `TenantId` at `CleansiaDbContext.cs:88-91` |
| `null` TenantId = single-tenant mode (the platform runs effectively single-tenant today; the machinery is multi-tenant-ready) | CLAUDE.md + the filter |

**This does not change for multi-region.** Region is **not** added to the tenancy filter — a region clause in
`ApplyTenantQueryFilters` would be a **conflation finding**. A tenant has exactly one home region, so its rows
live in one region's DB and `e.TenantId == currentTenantId` is sufficient *within* that DB.

## Axis (b) — physical region placement: NEW, purely infra/config

- **No region concept exists in the code/data model today** (Grep clean). The only geography is
  **`CountryConfiguration`** (`Core.Domain/Configuration/CountryConfiguration.cs`) — the per-**market** seam,
  already carrying `TimeZoneId`, `FiscalEnforcementMode`, `DefaultPaymentGateway`, VAT, the refund-fee rates.
- Region = *which physical deployment + which DB connection string* a request resolves to. Today: one region
  (West Europe), so routing is a no-op.
- **The connective seam (the one new thing):** a future **`CountryConfiguration.HomeRegion`** field (country→
  region; a tenant inherits its country's region) + a **region→connection-string resolver**. Resolution: the
  request's host/Environment fixes the **compute** region; the tenant's `HomeRegion` fixes the **data** region.
  In the shared model both are the single region, so the resolver is a constant.

## The recommended model + the trigger

- **NOW (market expansion): one shared region + one shared DB.** Tenants separated logically by the existing
  filter. Onboarding a market = a new `TenantId` + a `CountryConfiguration` row (zero infra, zero schema).
- **Lighter latency lever first (if latency ever bites):** CDN for the SSR/SPA static surface, then read-replicas
  — *before* the heavy region-pinned-DB step.
- **THE TRIGGER that flips to region-pinned DBs:** a **residency-regulated market** (data must physically stay
  in-region) **or** a **hard latency SLA**. Until one is real, shared. (Q-REGION-01.)

## The seam (what's laid now, in sprint-13 — ADR-0017 D4–D7)

| Seam item | Now (single-region) | What a 2nd region adds |
|---|---|---|
| Resource/RG/KV names | **`weu` token from day one** (`api-cleansia-partner-weu-dev`, `rg-cleansia-weu-dev`) | a new value (`eus`) — names are immutable, so the token MUST be there now |
| Bicep | a **`region` parameter** (default `weu`) threaded through modules | a new `<region>.<stage>.bicepparam` |
| Pipeline | **`strategy.matrix.region: [weu]`** (one-element) | add `eus` to the list |
| GitHub Environments | **`dev-weu` / `prod-weu`** (`<stage>-<region>`) | `dev-eus` / `prod-eus` (additive) |
| Subscriptions | **one** (region in RG/naming) | a per-region sub only if a quota/billing-legal/blast-radius trigger fires (Q-REGION-03) |
| Data layer | a **connection-string resolver** (one place; returns the single shared DB today — T-0330) | the resolver maps tenant→region→connection-string; **+ an owner `HomeRegion` column-migration** (deferred) |
| Tenancy filter | **UNCHANGED** | **UNCHANGED** (region never enters it) |

**Forward-compat assertion (the falsifiable check):** adding a second region requires only **a new param value +
a matrix entry + an owner `HomeRegion` column-migration** — **not** a rename/recreate of any live `weu` resource,
a workflow restructure, or a tenancy-filter change. If any of those *would* be required, the seam is incomplete.

## What is explicitly NOT built this pass

- No second region (no `eus`/other resources, RGs, Environments, matrix entries).
- No region-pinned DBs.
- No `CountryConfiguration.HomeRegion` **column** (deferred to first-second-region work — a schema change →
  owner ef-migration; only the resolver indirection is laid now, keeping sprint-13 migration-free).
- No change to the tenancy query filter, and no move of tenancy to infra (DB/schema-per-tenant rejected).

## Open questions / future evolution

- **Q-REGION-01** (residency trigger) — default **none yet**; a residency-regulated/non-EU market is the trigger.
- **Q-REGION-02** (tenant→region assignment) — default **country-driven, one home region per tenant**; reassignment
  deferred.
- **Q-REGION-03** (subscriptions) — default **one** until a quota/billing-legal/blast-radius trigger.
- **When the trigger fires:** a new ADR for the region-pinned model (the resolver maps tenant→region; the
  `HomeRegion` column lands; the matrix gains the region; a per-region DB is provisioned from the same Bicep). The
  seam makes it additive, not a rewrite.
