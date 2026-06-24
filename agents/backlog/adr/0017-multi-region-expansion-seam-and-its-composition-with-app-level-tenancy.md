# ADR-0017 — Multi-region expansion: the two axes (app-level tenancy vs physical region placement), the recommended model (one shared region + DB now; a region-pinned seam left clean), where each isolation lives (tenancy=app, region=infra), and the Bicep/pipeline/subscription/Postgres seam — DESIGN the seam, BUILD single-region West-Europe only

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-23
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** infra | devops | backend | db | cross-cutting (the tenancy seam + the deployment seam)
- **Extends:** **ADR-0015** (the Azure Bicep/pipeline/GitHub-Environments deployment — this ADR adds the
  **region seam** to ADR-0015's modules/pipeline/naming and confirms ADR-0015's single-region West-Europe dev
  build is **forward-compatible**, with a precise minimal addendum: a **region token in names from day one**).
  Builds on the **existing app-level multi-tenancy** (verified against the code below — `ITenantEntity`,
  `CleansiaDbContext.ApplyTenantQueryFilters`, `TenantProvider` `tenant_id` claim) and the existing
  **`CountryConfiguration`** per-market seam.
- **Ticket:** INFRA-REGION-ADR (this ADR) · **Consumers:** the **seam-now** tickets added to
  `status/sprint-13.md` (region token in naming + the `HomeRegion` config seam) — **N-region provisioning is
  OUT of scope** (future). Living companion `architecture/decisions/azure-deployment.md` (region section) +
  `architecture/decisions/multi-tenancy-and-region.md` (the composition note).

> This ADR answers the owner's deeper question — **multi-region market expansion and how it composes with the
> existing multi-tenancy** ("should we handle tenancy on the app level or infra/cloud level?", "different
> subscriptions per region? how does the pipeline change?"). It **disentangles the two axes**, **recommends the
> data/region model** for a **market-expansion** driver, **answers app-vs-infra directly**, and **lays the seam**
> in ADR-0015's Bicep/pipeline/Environments so they scale to N regions **without building them now**. It ships
> **no Bicep and no code** — the only *build* this pass authorizes is the **minimal naming/config seam**; the
> single-region West-Europe dev (ADR-0015) is what gets provisioned. Once `accepted` it is immutable —
> supersede, never edit.

> **Owner decisions this ADR is built to (locked):**
> (a) **PRIMARY DRIVER = MARKET EXPANSION** — presence/growth + timing flexibility, **NOT** data-residency/GDPR
>     and **NOT** a hard latency SLA (yet). Do not over-rotate on residency; name it as a **future trigger**.
> (b) **The architect RECOMMENDS the data model** (shared DB vs region-pinned DBs) given the existing tenancy.
> (c) **SCOPE = DESIGN the seam, BUILD single-region.** Decide the model + lay the seam in ADR-0015's
>     Bicep/pipeline; **build only the single West-Europe dev now.** Do **NOT** provision N regions.
> (d) Confirmed alongside this ADR: **one Azure subscription, West Europe, owner creates the 2 RGs**;
>     **Sign in with Apple via a backend `appleauth` endpoint** (Q-IOS-04 resolved — recorded in ADR-0016's plan).

---

## Context

ADR-0015 provisions a single-region (West Europe) Azure dev environment. The owner, before any Bicep is
written, asked the deeper question: **how does multi-region expansion compose with the multi-tenancy the app
already has, and where should each live?** The risk of getting this wrong is structural — if region and tenancy
are conflated, or the single-region build bakes in names/assumptions that a second region must undo, the
"expand to a new market" moment becomes a rewrite. This ADR exists to make the two axes **compose cleanly** and
to leave a seam that costs nothing now and avoids a rewrite later.

### The two axes — disentangled (they compose, they do not conflict)

**Axis (a): APP-LEVEL multi-tenancy — ALREADY EXISTS, verified against the code.** This is **logical** isolation
of tenants' rows in a shared database; it is purely an **application/data** concern with **zero infra
dependency**:
- **`ITenantEntity`** (`Core.Domain/Common/ITenantEntity.cs`) is `{ string? TenantId { get; set; } }` — note
  **`TenantId` is a nullable `string`**, not a GUID, so it is flexible enough to carry any tenant key.
- **The global query filter** is applied in `CleansiaDbContext.ApplyTenantQueryFilters` (`:171-240`) to **every**
  `ITenantEntity` automatically (a loop over `modelBuilder.Model.GetEntityTypes()`). The filter body is exactly:
  `tenantProvider == null  ||  (currentTenantId == null && e.TenantId == null)  ||  e.TenantId == currentTenantId`
  — i.e. **(i)** design-time/migrations bypass (`tenantProvider == null`), **(ii)** **single-tenant mode** is
  the `null == null` middle clause (`currentTenantId == null && e.TenantId == null`) — without it SQL's
  `null == null` is NULL and would hide every row in single-tenant / queue / webhook contexts, **(iii)** the
  multi-tenant happy path `e.TenantId == currentTenantId`.
- **The tenant is resolved from the JWT.** `TenantProvider` (`Infra.Database/TenantProvider.cs`) reads
  `httpContextAccessor.HttpContext.User.FindFirst("tenant_id")` (`TenantClaimType = "tenant_id"`, `:8`,
  `:18-19`). **No tenant header** — the claim drives it, so every client (web/Android/iOS) gets tenant-scoping
  for free (ADR-0013 D4.5 records this for iOS).
- **Cross-tenant access is explicit:** background jobs / webhooks call `tenantProvider.SetTenantOverride(...)`
  (`:22-25`) or `IgnoreQueryFilters` (the `CommitAsync` auto-stamps a new `ITenantEntity`'s `TenantId` from
  `tenantProvider.GetCurrentTenantId()` at `:88-91`). This is the documented, tested path (the memory notes on
  webhook/`IgnoreQueryFilters` tenant-scope are about exactly this).
- **`null` TenantId = single-tenant mode** (CLAUDE.md, confirmed by the filter). Today the platform runs
  effectively single-tenant; the machinery is multi-tenant-ready.

**Axis (b): PHYSICAL multi-region placement — NEW, and purely infra.** This is **where the compute + data
physically live** (West Europe today; a second region later). **There is no region concept anywhere in the code
or data model today** — `Grep` for region/home-region across the domain returns nothing; the only geography in
the model is **`CountryConfiguration`** (`Core.Domain/Configuration/CountryConfiguration.cs`), which is the
per-**market** seam (it already carries `TimeZoneId`, `FiscalEnforcementMode`, `DefaultPaymentGateway`, VAT, the
refund-fee rates — i.e. country = the unit of market variation). So **region is genuinely net-new and lives
entirely in infra/config** — it does not touch the tenancy filter at all.

**Why they compose:** tenancy answers *"whose rows is this?"* (an app filter on `TenantId`); region answers
*"which physical deployment/DB does this request hit?"* (an infra routing concern). A tenant's rows are isolated
by the filter **regardless** of which region's DB they sit in; a region holds the rows of the tenants assigned
to it. The only **new** artifact that connects them is a **tenant→region (or country→region) map** — which is a
small piece of **config**, not a change to the tenancy filter. **They are orthogonal: you can have N tenants in
1 region (today), 1 tenant per region, or N tenants per region — the filter is unchanged in all three.**

### Why this is ONE decision

The model recommendation, the app-vs-infra answer, the Bicep/pipeline/subscription/Postgres seam, and the
ADR-0015 forward-compatibility confirmation are **inseparable**: the model (shared-DB vs region-pinned)
determines whether region routing is a no-op or a tenant→region lookup; the app-vs-infra split determines that
the tenancy filter is untouched and the seam is config+infra; the Bicep/pipeline seam is *how* a region becomes
a parameter; and the ADR-0015 confirmation is whether building single-region now is throwaway. Splitting this
would let the model be chosen without the seam that implements it, or the seam designed without confirming the
single-region build survives it. The *implementation* is the (minimal) seam tickets; N-region build is future.

---

## Decision

> **Contract principle.** The two axes are **orthogonal and stay separated**: **tenancy is an APP concern**
> (the existing row-scoped `TenantId` global query filter + the `tenant_id` JWT claim — **unchanged**), and
> **region is an INFRA concern** (which deployment/DB a request hits). For the **market-expansion** driver
> (not residency, not a latency SLA), the recommended model is the **LIGHTEST that doesn't paint us into a
> corner: ONE shared region + ONE shared DB now**, with tenants separated **logically** by the filter that
> already exists. The **seam** for a future heavier model (region-pinned DBs) is laid but **not built**: (1) a
> **`region` parameter** on ADR-0015's Bicep modules + a **region token in every resource name from day one**
> (the *only* minimal change to ADR-0015), (2) a **pipeline that fans out per region via a matrix** + a
> **GitHub-Environment naming scheme `<stage>-<region>`** (e.g. `dev-weu` / `prod-weu` / `prod-eus`), (3)
> **one subscription** until a sub-level limit or a billing/legal boundary forces a split, and (4) a
> **tenant→region resolution seam** (a **`CountryConfiguration.HomeRegion`** field + a region→connection-string
> resolver) that makes the single DB become per-region DBs **without an app rewrite**. The **named trigger** that
> flips to the heavier region-pinned model is **a residency-regulated market or a hard latency SLA** — until
> then, shared. **This pass BUILDS only the single-region West-Europe dev (ADR-0015) + the minimal naming/config
> seam; it does NOT provision N regions.**

### D1 — The model: ONE shared region + ONE shared DB now (logical tenancy already separates tenants)

For a **market-expansion** driver, the recommended model is **a single shared region (West Europe) + a single
shared PostgreSQL**, with tenants separated **logically** by the existing `TenantId` filter.

**Rationale:**
- **The tenancy filter already does the isolation a shared DB needs.** Every `ITenantEntity` is auto-scoped
  (`ApplyTenantQueryFilters`); a new market's tenant is just a new `TenantId` value — **zero schema, zero infra
  change** to onboard it logically. Market expansion (adding a country/tenant) is a *data + `CountryConfiguration`*
  operation, not a *deployment* operation.
- **Market expansion ≠ data separation.** The owner's driver is **presence/growth + timing flexibility**, not a
  legal requirement that data physically stays in-region. A shared DB serves a new market's customers correctly
  today; the only costs of distance are **latency** (not a stated SLA) and **residency** (not yet a
  requirement) — and both have a **named trigger** (D6) that flips the model when they become real.
- **It is the lightest model that doesn't corner us.** Region-pinned DBs (D-alt) impose a tenant→region
  routing layer, per-region connection-string resolution, cross-region admin/reporting complexity, and an
  N×-cost provisioning fan-out **now** — for isolation the market-expansion driver does not require. Building
  that ahead of the trigger is premature infrastructure. The shared model + the seam (D4/D5) gets the
  flip-when-needed option **for free**.
- **Compute-near-users, if/when latency matters, is the cheap first lever** — a CDN for the SSR/SPA static
  surface and (later) read-replicas — **before** the heavy region-pinned-DB step. So even the latency trigger
  has a lighter intermediate response than full region-pinning.

### D2 — App vs infra, answered directly: tenancy=app (unchanged), region=infra (new), with the seam between them

> **"Should we handle tenancy on the app level or the infra/cloud level?" — APP LEVEL. It already is, and it
> stays there.** **"Where does region live?" — INFRA LEVEL.** The two do not move into each other.

- **Tenant isolation lives in the APP** — the row-scoped `TenantId` global query filter + the `tenant_id` JWT
  claim (`TenantProvider`). **This does not change for multi-region.** We do **not** move tenancy to the infra
  layer (e.g. a DB-per-tenant or a schema-per-tenant): the row-scoped model is proven, tested, born into every
  client for free (JWT claim, no header), and is exactly what lets a shared DB hold multiple markets safely.
  Moving tenancy to infra (DB-per-tenant) would be a massive, unrequested rewrite that throws away the working
  filter.
- **Region routing lives in the INFRA/config** — *which deployment + which DB connection string* a request
  resolves to. Today there is one region, so routing is a no-op (everything resolves to West Europe). The seam
  (D4) is **where** that routing will live when there is more than one: a **tenant→region (via country→region)
  map** + a **region→connection-string resolver**.
- **The seam between them (the one new connective tissue):** a **`CountryConfiguration.HomeRegion`** field (a
  country is assigned a home region; a tenant inherits its country's region) — config, not a filter change.
  Region resolution order: **the request's host/Environment determines the region of the *compute*** (a
  `dev-weu` deployment is West Europe compute), and **the tenant's `CountryConfiguration.HomeRegion` determines
  the region of its *data*** (which DB connection string to use). In the shared model these are always the same
  (one region), so the resolver is a constant; in the region-pinned model the resolver maps tenant→region→
  connection-string. **A tenant never spans regions** (its rows live in one home region's DB) — this is the
  invariant that keeps the tenancy filter untouched (a tenant's `TenantId` is unique within its home DB; no
  cross-region row joins).
- **What stays out of the app:** the app **never** branches on a region code in a handler (the same rule as
  "never branch on a country code in a handler" — region, like country, is read from config/the resolver, not
  hard-coded). The CQRS handlers, the fiscal modes, the pay formula, the per-audience hosts — **none** change
  for region; they operate on whatever DB the connection-string resolver hands them.

### D3 — The tenant→region assignment policy: country-driven, a tenant has exactly ONE home region

- **Assignment is by COUNTRY → region** (via `CountryConfiguration.HomeRegion`), not per-tenant-bespoke. Country
  is already the unit of market variation (`CountryConfiguration`), so "this market lives in this region" is the
  natural granularity and the lightest map (one row per country, not per tenant). A tenant inherits its
  country's home region.
- **A tenant has exactly ONE home region** (the no-cross-region-tenant invariant, D2). If a single business
  legal-entity ever operates in two residency-regulated markets, that is **two tenants** (one per market/region),
  not one tenant spanning two regions — recorded so the invariant is explicit. The **policy detail** (can a
  tenant be *reassigned* to a new region? what is the data-migration story?) is a **future** concern gated on the
  trigger (Q-REGION-02), not built now.
- **In the shared model, every country's `HomeRegion` is the single region** (West Europe) — so the field is
  present (the seam) but uniform. When a second region is added, only the new market's `HomeRegion` differs.

### D4 — The Bicep seam (ADR-0015): region is a PARAMETER; a region token in names from day one

ADR-0015's modules become **region-parameterized** — one parameterized stack **stamped per region**:
- **`region` is a parameter** on `main.bicep` (+ each module that needs it), defaulting to `weu` (West Europe)
  for the dev build. The modules are otherwise **unchanged** — the reusable `appService`, `postgres`, `storage`,
  etc. all take `region` and emit region-aware names + the correct Azure `location`.
- **A region token in every resource name from day one** — the **one minimal amendment to ADR-0015** (D7). The
  ADR-0015 names become `api-cleansia-<audience>-<region>-<stage>` (e.g. `api-cleansia-partner-weu-dev`),
  `web-cleansia-customer-weu-dev`, `pg-cleansia-weu-dev`, `kv-cleansia-weu-dev`, etc. **Building West-Europe-dev
  with the `weu` token now costs nothing and means a second region (`eus`, `neu`, …) is a new *value*, not a
  rename of the live `weu` resources.** (Azure resource names are immutable — putting the token in later forces a
  recreate; putting it in now is free at clean-slate.)
- **Per-region RG vs the dev/prod RGs:** the **RG carries the region too** — `rg-cleansia-weu-dev` /
  `rg-cleansia-weu-prod` (extending ADR-0015's `rg-cleansia-dev`/`-prod` with the region token). A second region
  is `rg-cleansia-eus-prod` — a new RG from the **same** Bicep with `region=eus`. This keeps the per-region blast
  radius clean and is the natural unit to stamp.
- **A region `.bicepparam` per region** — `weu.dev.bicepparam` now; `eus.prod.bicepparam` is what a future
  region adds (same modules, region value + that region's SKUs). The `env` axis (dev/prod, ADR-0015) and the
  `region` axis compose: the param file is `<region>.<stage>.bicepparam`.

### D5 — The pipeline + GitHub-Environments seam: matrix over regions, `<stage>-<region>` Environments

- **The deploy workflows fan out per region via a MATRIX.** ADR-0015's `deploy-dev.yml` / `deploy-prod.yml` gain
  a **`strategy.matrix.region`** over the regions for that stage (today: `[weu]` — a one-element matrix, so the
  current behavior is unchanged; tomorrow: `[weu, eus]`). Each matrix leg runs the **same** provision →
  migrate → deploy job graph against its region's RG + `.bicepparam` + connection string. **Adding a region is
  adding a value to the matrix list** — no new workflow, no per-region copy.
- **GitHub Environments scale as `<stage>-<region>`** — the naming scheme: **`dev-weu`**, **`prod-weu`**,
  **`prod-eus`**, … (the owner's proposed scheme, adopted). Each carries its **own** per-Environment secrets
  (that region's OIDC ids if the principal differs, that region's DB connection string, that region's SWA
  tokens) and its **own protection** (the `prod-*` Environments all require reviewers + manual approval; the
  `dev-*` are auto). This is **env-per-region-per-stage** — recommended over env-per-region (which can't gate
  prod separately from dev) and over env-per-stage-only (which can't scope per-region secrets/approval). For the
  **dev build now**, ADR-0015's `dev`/`prod` Environments are **renamed to `dev-weu`/`prod-weu`** (the minimal
  Environments seam — see D7), so a second region is `dev-eus`/`prod-eus`, additive.
- **The migrate-before-deploy ordering (ADR-0015 D5) is per-region** — each region's matrix leg migrates *its*
  DB before deploying *its* compute. In the shared model there is one DB, so one leg migrates it; in the
  region-pinned model each region's leg migrates its own DB. The EF-bundle step is unchanged; it just runs per
  matrix leg against the leg's connection string.

### D6 — Subscriptions: ONE subscription until a real boundary forces a split — and the named trigger

- **Recommendation: ONE subscription, region carried in the RG + naming** (extending ADR-0015's one-sub/two-RGs
  to one-sub/two-RGs-**per-region**). A single subscription comfortably holds multiple regions' RGs; Azure
  regions are a *location* property of resources, **not** a subscription boundary. Splitting per region adds
  subscription-level governance/billing overhead for no benefit until a real boundary appears.
- **The trigger that flips to per-region subscriptions:** **(a)** a **subscription-level quota/limit** hit
  (cores, resources, or a service limited per-subscription that a large region needs its own headroom for);
  **(b)** a **billing/legal boundary** — a market that must be billed or governed under a separate legal entity /
  Azure tenant (e.g. a residency-regulated market requiring data-processing under a local subscription); **(c)**
  a **blast-radius/compliance** requirement to isolate a region's RBAC/policy at the subscription level. Until one
  of these is real, one subscription. (Recorded as Q-REGION-03, non-blocking — the Bicep is RG-scoped, so a
  later per-region subscription is a deployment-target parameter, not a rewrite.)

### D7 — ADR-0015 forward-compatibility: CONFIRMED, with a precise minimal addendum (not a supersede)

**ADR-0015's single-region West-Europe dev build is forward-compatible with this seam** — building it now does
**not** have to be redone for multi-region, **provided** the following **minimal** changes are folded into the
ADR-0015 *consumer tickets* (sprint-13) now. These are **naming/parameter** changes, not decision changes, so
they are an **addendum recorded here + a sprint-13 note**, **not** a supersede of ADR-0015 (ADR-0015's decisions
— Bicep-in-`deploy/bicep/`, one reusable `appService` module, Key-Vault+MI, OIDC+EF-bundle, dev-auto/prod-
protected — all stand unchanged):

1. **Region token in names from day one** (D4): resources/RG/Key-Vault names carry `weu`
   (`api-cleansia-partner-weu-dev`, `rg-cleansia-weu-dev`, `pg-cleansia-weu-dev`, …). **Why now:** Azure names are
   immutable; adding the token later forces a recreate of the live dev resources. Free at clean-slate.
2. **`region` parameter on the Bicep modules**, defaulting to `weu` (D4). **Why now:** the modules are authored in
   sprint-13 P0; adding the parameter at authoring time is free; retrofitting it means editing every module later.
3. **GitHub Environments named `dev-weu` / `prod-weu`** (not bare `dev`/`prod`) (D5). **Why now:** the owner
   creates them in sprint-13 P1; naming them with the region token now means a second region is `dev-eus`/
   `prod-eus` (additive), not a rename of the live Environments + their secrets.
4. **A one-element `matrix.region: [weu]` in the workflows** (D5). **Why now:** it is a no-op today (one leg) and
   makes adding a region a one-line list change; retrofitting a matrix later restructures the whole workflow.
5. **A `CountryConfiguration.HomeRegion` field + a region→connection-string resolver seam** (D2/D3). **Why now —
   and the honest caveat:** the **resolver seam** (a single place the connection string is chosen, defaulting to
   the one shared DB) is cheap to introduce now and is the difference between "add a region = config" and "add a
   region = rewrite the data layer." **However**, adding the `HomeRegion` *column* is a **schema change → an
   owner ef-migration** (CLAUDE.md owner-only). So the recommendation is: introduce the **resolver indirection
   now** (code-level seam, no schema — the connection string is resolved through one function that today returns
   the single DB), and **defer the `HomeRegion` column** to the first real second-region work (gated on the
   trigger) — the column is trivial to add then and the resolver is already in place. **This keeps sprint-13
   migration-free** (consistent with ADR-0015's "no new ef-migration this wave") while still laying the seam.

**Net:** ADR-0015's build is **not throwaway**; the five items above are **naming + one code-level indirection**,
all free-or-cheap at clean-slate, and they convert "second region = rewrite" into "second region = a new param
value + a matrix entry + an owner column-migration."

### D8 — Scope guard

This ADR decides the **model + the seam**. It does **NOT**: provision a second region (future, gated on the
trigger); build region-pinned DBs (future); add the `CountryConfiguration.HomeRegion` **column** now (deferred to
first-second-region work — only the resolver indirection is laid now); change the tenancy query filter (it is
**unchanged** — region is orthogonal); move tenancy to infra (rejected — row-scoped stays); or write Bicep/code
(the seam is folded into ADR-0015's sprint-13 tickets as naming/parameter notes + one resolver-indirection
ticket). A future residency-regulated market, a latency SLA, or a subscription-limit hit is revisited against
this ADR (a new ADR for the region-pinned model when the trigger fires; a living-doc note for a CDN/read-replica
latency response).

---

## Alternatives considered

- **Region-pinned DBs now (a country/tenant assigned to a home-region Postgres; the app routes tenant→region).**
  Rejected as the model to **build** now (D1); **adopted as the seam to leave** (D4/D5). It is the **strongest
  isolation** (a market's data physically in-region) and is exactly what a residency requirement or a latency SLA
  would force — but the owner's driver is **market expansion, not residency/latency**, so building it now imposes
  a tenant→region routing layer, per-region connection-string resolution, cross-region admin/reporting
  complexity, N× provisioning cost, and an owner schema migration **ahead of any need**. The seam (D7) gets the
  flip-when-the-trigger-fires for free; building it pre-trigger is premature infrastructure.
- **Move tenancy to the infra layer — DB-per-tenant or schema-per-tenant.** Rejected (D2). The row-scoped
  `TenantId` filter is proven, tested, free in every client (JWT claim), and is precisely what lets a shared DB
  hold multiple markets safely. DB/schema-per-tenant is a massive unrequested rewrite that discards the working
  filter and explodes operational cost (N DBs to migrate/back-up/monitor) for isolation the driver doesn't need.
  Tenancy stays an **app** concern.
- **Hybrid / cell-based (stamps of {compute+DB} each holding a shard of tenants).** Rejected as premature (D1).
  Cell architecture is the answer to **scale limits** (a single region/DB outgrowing capacity) or **blast-radius**
  at large scale — neither is the current driver. The region-parameterized stack (D4) is *already* a "stamp," so
  if cell-based is ever needed it is the same seam at finer granularity; building cells now is over-engineering
  for a single-region market-expansion start.
- **No seam — build pure single-region, add region later when needed.** Rejected (D7). "Add it later" against
  **immutable Azure resource names** + **created GitHub Environments** + a **non-matrix workflow** + a
  **hard-coded single connection string** means the second region forces renames/recreates/restructures — exactly
  the rewrite the owner is trying to avoid. The seam (region token + parameter + matrix + resolver indirection) is
  free-or-cheap at clean-slate and is the whole point of deciding this *before* the Bicep is written.
- **Per-region subscriptions from day one.** Rejected (D6). Azure regions are a resource *location*, not a
  subscription boundary; one subscription holds many regions' RGs. Splitting per region adds governance/billing
  overhead with no benefit until a real trigger (quota, billing/legal, blast-radius) fires. The Bicep is
  RG-scoped, so a later per-region subscription is a deployment-target parameter, not a rewrite.
- **Add the `CountryConfiguration.HomeRegion` column now.** Rejected for this pass (D7 item 5). It is a schema
  change → an owner ef-migration, and there is no second region to populate it for. The **resolver indirection**
  (the code-level seam) is laid now; the column is added trivially at first-second-region work, when there is a
  value to put in it. This keeps sprint-13 migration-free (consistent with ADR-0015).

---

## Consequences

**Cheaper / safer:**
- **The two axes compose cleanly and never fight.** Tenancy (app, row-scoped, unchanged) and region (infra,
  config-routed, new) are orthogonal — a tenant's rows are isolated by the filter regardless of region, and a
  region holds its assigned tenants' rows. The owner's question ("app or infra?") has a clear answer: **both, at
  the right layer.**
- **Market expansion is a data + config operation, not a deployment.** Onboarding a new market in the shared
  model is a new `TenantId` + a `CountryConfiguration` row — zero infra, zero schema (until a residency trigger).
- **The single-region dev build is NOT throwaway.** The five minimal seam items (region token, `region` param,
  `dev-weu`/`prod-weu` Environments, one-element matrix, resolver indirection) make a second region **a param
  value + a matrix entry + an owner column-migration** — not a rewrite.
- **The flip-to-heavier-model option is preserved for free.** When the named trigger fires (a
  residency-regulated market or a latency SLA), the region-pinned model is reachable through the seam without
  re-architecting the app or the pipeline.
- **No premature infrastructure.** N regions are not built; the lightest model that serves the driver ships, with
  the heavier one one trigger away.

**More expensive (new obligations — all small, this pass):**
- **The minimal ADR-0015 amendment** (D7): region token in names, a `region` param, `dev-weu`/`prod-weu`
  Environments, a one-element matrix, and a **connection-string resolver indirection** — folded into sprint-13.
  Real (small) authoring work, and the resolver indirection is a code seam a reviewer must confirm is the single
  place the connection string is chosen.
- **A future owner ef-migration** for `CountryConfiguration.HomeRegion` when the first second region lands
  (deferred, not now).
- **The tenant→region assignment policy + the residency trigger are owner calls** (Q-REGION-01/02) — recorded,
  non-blocking, deferred to when a second region is on the table.
- **A discipline to honor:** no handler branches on a region code (the same rule as country) — region is read
  from the resolver/config, never hard-coded. A reviewer enforces it.

**Rollout (consumer impact — see `status/sprint-13.md`):**
- The **seam-now** items (D7) are folded into the existing sprint-13 P0/P1/P2 tickets (naming/param/Environment/
  matrix notes) **plus one small new ticket** for the connection-string resolver indirection. **N-region
  provisioning is OUT of scope** — no new region is built this wave.

---

## How a reviewer verifies compliance

**Mechanical / structural (the seam gate):**
1. **Region token in names from day one.** Every Bicep-emitted resource name + RG + Key Vault carries the region
   token (`weu`): `api-cleansia-<audience>-weu-dev`, `rg-cleansia-weu-dev`, `pg-cleansia-weu-dev`, etc. A name
   without a region token is a finding (it cannot coexist with a second region without a rename).
2. **`region` is a Bicep parameter** defaulting to `weu`, threaded through the modules that emit names/locations;
   the modules are otherwise the ADR-0015 modules (no per-region forks).
3. **GitHub Environments are `dev-weu` / `prod-weu`** (not bare `dev`/`prod`); `prod-weu` keeps the required-
   reviewer + manual-approval protection (ADR-0015 D5).
4. **The workflows carry a `strategy.matrix.region: [weu]`** (a one-element matrix today); the provision →
   migrate → deploy graph runs per matrix leg against the leg's RG/param/connection string.
5. **One subscription** (region in RG/naming, not a per-region subscription) — confirmed against Q-REGION-03's
   default.
6. **The connection-string resolver indirection exists.** The DB connection string is chosen in **exactly one**
   place (a resolver that today returns the single shared DB); no handler/repo hard-codes a region or reaches a
   second connection string. (This is the seam that makes per-region DBs later a resolver change, not an app
   rewrite — the analogue of the `DeviceIdProvider` single-source rule.)
7. **The tenancy filter is UNCHANGED.** `CleansiaDbContext.ApplyTenantQueryFilters` is not modified for region;
   no region clause is added to the query filter; `TenantProvider` still resolves the `tenant_id` claim. Region
   does **not** appear in the tenancy filter (a region clause in the filter is a conflation finding).
8. **No handler branches on a region code** (the same rule as country) — region is read from config/the
   resolver, never hard-coded in a handler.
9. **No second region provisioned.** No `eus`/other-region resources, RGs, Environments, or matrix entries exist
   (this pass is single-region; building a second region is out of scope and would be a separate, trigger-gated
   ADR/wave).

**Forward-compat assertion (the "is it throwaway?" check):** a reviewer can answer "would adding a second region
require renaming/recreating any live `weu` resource, or restructuring the workflow, or changing the tenancy
filter?" — and the answer must be **no** (a new region value + a matrix entry + an owner `HomeRegion`
column-migration only). If any answer is "yes," the seam is incomplete.

---

## Roles affected

No domain-aggregate change (the tenancy filter is untouched; `CountryConfiguration` gains a future field, not
now). Catalog edits (same change as this ADR, per the pattern-evolution loop):
- `agents/knowledge/patterns-backend.md` — extend the multi-tenancy note: **tenancy = app (row-scoped, the
  existing filter)**; **region = infra (config-routed)**; the two are orthogonal; **never branch on a region
  code in a handler** (the country-code rule extended to region); the connection string is resolved in one
  place.
- `agents/knowledge/conventions.md` — the region-token-in-names rule + "region is a Bicep parameter / a matrix
  axis / a `<stage>-<region>` Environment" as the deployment-naming convention.
- Living companions: `agents/architecture/decisions/azure-deployment.md` (region section — the parameterization,
  matrix, naming, subscription trigger) + a new `agents/architecture/decisions/multi-tenancy-and-region.md` (the
  composition note: the verified tenancy facts + the orthogonality + the seam).

---

## Open questions raised (owner — all non-blocking for the single-region build)

Filed in `agents/backlog/questions/open.md`:
- **Q-REGION-01 (`post-prod` / gated on a second region; owner)** — the **residency trigger**: which market (if
  any) is residency-regulated such that its data must physically stay in-region? **Default taken:** **none yet**
  — the market-expansion driver does not require residency, so the shared model ships; a residency-regulated
  market is the named trigger (D6) that flips to region-pinned DBs. The ADR is explicit this is a *future*
  trigger, not a current requirement.
- **Q-REGION-02 (`post-prod` / gated on a second region; owner)** — the **tenant→region assignment + reassignment
  policy**: confirm country→region as the granularity (D3), and the data-migration story if a tenant is ever
  reassigned to a new region. **Default taken:** country-driven, one home region per tenant, no reassignment
  story built until a second region is real.
- **Q-REGION-03 (`post-prod`; owner)** — **per-region subscriptions** vs one subscription? **Default taken:**
  one subscription (region in RG/naming) until a quota / billing-legal / blast-radius trigger fires (D6). The
  Bicep is RG-scoped, so a later split is a deployment-target parameter.

These do **not** block the single-region West-Europe dev build (ADR-0015). The only *build* this pass authorizes
is the **minimal seam** (D7), folded into sprint-13.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted (grounded in the verified tenancy code + `CountryConfiguration` + ADR-0015); challengers
(over-engineering vs under-engineering the seam, the residency blind spot, the conflation risk) attacked; the
Lead re-verified the tenancy citations against the real `CleansiaDbContext`/`TenantProvider` and adjudicated.
**Verdict: all challenges RESOLVED; zero blocking (three non-blocking owner questions escalated with defaults);
consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (under-engineering) | A shared DB for "global market expansion" is naive — the moment a regulated market (or a latency-sensitive one) appears you'll be rewriting the data layer. Build region-pinned now. (MAJOR — the core model call) | REBUT + SEAM | D1 + D4/D7: the **driver is market expansion, not residency/latency** (owner-locked) — a shared DB serves a new market correctly today, and the **tenancy filter already isolates tenants logically**. Building region-pinned now imposes routing/connection-resolution/N×-provisioning/an owner migration **ahead of any need**. The **seam** (region param + matrix + the connection-string resolver indirection) makes the flip **a config change, not a rewrite**, when the **named trigger** (residency market / latency SLA, D6) fires. We get the option for free without paying for it pre-trigger. |
| CH-2 (over-engineering) | If we're building single-region, why touch naming/Environments/matrix at all? That's YAGNI — add it when the second region is real. (MODERATE — the opposite pressure) | DEFEND | D7 + Alternatives: "add it later" collides with **immutable Azure resource names**, **already-created GitHub Environments + their secrets**, a **non-matrix workflow**, and a **hard-coded single connection string** — each forces a rename/recreate/restructure for the second region (the exact rewrite the owner wants to avoid). The five seam items are **naming + one code indirection**, free-or-cheap **at clean-slate**, and they are the whole reason the owner asked this *before* the Bicep is written. The line is drawn honestly: we lay the **naming/param/matrix/resolver** seam (cheap, immutable-name-driven) but **defer the `HomeRegion` column + N-region provisioning** (a migration / real cost) to the trigger. |
| CH-3 (conflation) | Region routing will leak into the tenancy filter — you'll end up with `e.TenantId == tenant && e.Region == region`, coupling the two and breaking the proven filter. (MAJOR — the seam the platform must protect) | REBUT (with the code) | D2 + check #7: the tenancy filter (`ApplyTenantQueryFilters`, verified at `CleansiaDbContext.cs:171-240`) is **untouched** — region does **not** enter it. A tenant has **exactly one home region** (D3 invariant), so its rows live in one region's DB and `e.TenantId == currentTenantId` is sufficient *within* that DB; region selects **which DB**, not **which rows**. The filter stays `TenantId`-only; a region clause in the filter is a **finding** (#7). Region is resolved **before** the query (the connection-string resolver), not inside it. |
| CH-4 (residency blind spot) | Calling residency a "future trigger" is how compliance incidents happen — GDPR/data-residency could already apply to an EU market. (MAJOR — legal) | REBUT + SCOPE | D1/D6 + Q-REGION-01: the owner **explicitly scoped the driver as NOT residency** (locked). For the current EU-centric markets (CZ/SK/PL/…), data in **West Europe** is *in* the EU — GDPR's cross-border concern is transfers **out** of the EU, which a single EU region does not trigger. The ADR **names residency as the trigger** (D6) and files Q-REGION-01 so a residency-regulated market (or a non-EU market) is caught **before** it launches — it is surfaced, not buried. The shared-EU-region model is GDPR-consistent for the stated markets. |
| CH-5 (app-vs-infra answer) | The owner asked a direct question — "app or infra?" — and a hedge ("both") is a non-answer. (MODERATE — clarity) | DEFEND | D2: the answer is **direct, not a hedge** — **tenant isolation = APP** (the row-scoped filter, unchanged; we do **not** move it to infra), **region placement = INFRA** (which deployment/DB, new). They are different questions at different layers. The *one* connective seam (tenant→region via `CountryConfiguration.HomeRegion` + the resolver) is named precisely, with the invariant (a tenant never spans regions) that keeps them decoupled. "Both, at the right layer, with a defined seam" is the precise answer, not a hedge. |
| CH-6 (forward-compat proof) | "Forward-compatible" is easy to assert and hard to prove — how do we *know* the single-region build isn't throwaway? (MODERATE) | DEFEND | D7 + the forward-compat assertion in "How a reviewer verifies": the falsifiable check is "would a second region rename/recreate any live `weu` resource, restructure the workflow, or change the tenancy filter?" — and with the five seam items in place the answer is **no** (new region = a param value + a matrix entry + an owner `HomeRegion` migration). The check is **mechanical**, not a claim — a reviewer runs it. |

**Affirmed unchallenged:** tenancy stays the existing row-scoped `TenantId` filter (verified, unchanged); region
is config/infra and orthogonal; country→region as the assignment granularity (`CountryConfiguration` is already
the market seam); one subscription until a real trigger; the named trigger (residency market / latency SLA) is
what flips to region-pinned; N-region provisioning is out of scope this pass.

**Lead re-verification (against current code, 2026-06-23):** `ITenantEntity` = `{ string? TenantId }`
(`Core.Domain/Common/ITenantEntity.cs:3-6` — nullable string, flexible); `CleansiaDbContext.ApplyTenantQueryFilters`
(`:171-240` — the loop over every `ITenantEntity`, the three-clause body `tenantProvider==null ||
(currentTenantId==null && e.TenantId==null) || e.TenantId==currentTenantId`, single-tenant = the null/null middle
clause); `TenantProvider` (`Infra.Database/TenantProvider.cs:8,18-19` — `tenant_id` claim, no header; `:22-25`
`SetTenantOverride` for cross-tenant jobs); `CommitAsync` auto-stamps a new `ITenantEntity`'s `TenantId` from the
provider (`:88-91`); **no region concept anywhere** (Grep clean); `CountryConfiguration`
(`Core.Domain/Configuration/CountryConfiguration.cs` — the per-market seam, already carrying `TimeZoneId`/
`FiscalEnforcementMode`/`DefaultPaymentGateway`/VAT, the natural home for a future `HomeRegion`);
`TenantConfiguration` (a key/value tenant-scoped store, an alternative override home). ADR-0015's modules/
pipeline/Environments confirmed amenable to a `region` parameter + a `<stage>-<region>` naming scheme + a
one-element matrix with no decision change.

**Escalations to the owner:** three **non-blocking** questions with defaults (Q-REGION-01 residency trigger =
none yet; Q-REGION-02 tenant→region assignment = country-driven, deferred; Q-REGION-03 subscriptions = one until
a trigger). None blocks the single-region West-Europe dev build; the only authorized *build* this pass is the
minimal seam (D7), folded into sprint-13.
