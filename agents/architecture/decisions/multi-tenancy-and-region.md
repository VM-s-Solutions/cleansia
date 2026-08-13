# Multi-tenancy and multi-region — how the two axes compose (living decision note)

> Companion to the **immutable** ADR-0017
> (`docs/decisions/adr-0017.md`). The ADR
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
| Every `ITenantEntity` is auto-scoped by a **global query filter** (a loop over all entity types) | `CleansiaDbContext.ApplyTenantQueryFilters` — the whole method |
| Filter body: `tenantProvider==null  ‖  (currentTenantId==null && e.TenantId==null)  ‖  e.TenantId==currentTenantId` | same method, the `body` expression (`Expression.OrElse(providerNullCheck, Expression.OrElse(singleTenantMatch, tenantMatch))`) |
| **Single-tenant mode** = the `null==null` middle clause (without it, SQL `null==null` is NULL and hides every row in single-tenant / queue / webhook contexts) | same method, `singleTenantMatch` |
| Tenant resolved from the **`tenant_id` JWT claim**, no header | `TenantProvider.cs:8` (`TenantClaimType="tenant_id"`), `:18-19` |
| Cross-tenant jobs/webhooks use **`SetTenantOverride`** / `IgnoreQueryFilters` | `TenantProvider.cs:22-25`; `CommitAsync` auto-stamps a new entity's `TenantId` at `CleansiaDbContext.cs:88-91` |
| `null` TenantId = single-tenant mode (the platform runs effectively single-tenant today; the machinery is multi-tenant-ready) | CLAUDE.md + the filter |

**This does not change for multi-region.** Region is **not** added to the tenancy filter — a region clause in
`ApplyTenantQueryFilters` would be a **conflation finding**. A tenant has exactly one home region, so its rows
live in one region's DB and `e.TenantId == currentTenantId` is sufficient *within* that DB.

## What ACTIVATION changes — the two things `TenantId == null` is silently holding up (T-0531)

> Everything above describes tenancy as it runs **today**, which is `TenantId == null` on every row.
> This section is about the **transition**, because two guarantees that read as "the database enforces
> this" are, today, enforced by nothing — and they fail in **opposite** directions on the day a non-null
> tenant first exists. Two design panels in one sprint reasoned past one or the other, which is why this
> is written down here rather than re-derived a third time.
>
> **The rule itself lives in the catalog and is not restated here:** `agents/knowledge/consistency.md`
> §*"Tenant-scoped unique indexes: `NULLS NOT DISTINCT` is decided by the index's JOB, not by a
> majority"*, cited from `patterns-backend.md`'s metered-benefit archetype. This section states only the
> **activation consequence**, which is the part the catalog does not cover and this axis owns.

### (1) The index half — inert today, live on activation

`ITenantEntity.TenantId` is `string?` and PostgreSQL treats NULLs in a UNIQUE index as **DISTINCT** by
default. So **a unique index whose key includes `TenantId` constrains nothing at all while the platform
is single-tenant** — two otherwise-identical rows both insert and `ON CONFLICT DO NOTHING` never fires.

- **Forward (turning tenancy on):** every such index starts firing, including the ones that are inert
  today. An invariant guaranteed today by app-level code becomes *also* guaranteed by the database — and
  a race that today produces two rows starts producing a `DbUpdateException` on a call path that may
  never have expected one. Check what the write path does with that exception before assuming the
  tightening is free: on a money path, a unique violation surfacing at the pipeline commit rolls back the
  whole unit of work (ADR-0038 §D3 walks that exact case for `PromoCodeRedemptions`).
- **The reverse is the dangerous direction, and it is the one that bites *now*.** Anywhere an app-level
  guard was **removed** on the strength of such an index, activation does not help — that invariant is
  unguarded **today**, in the mode the platform actually runs in. ADR-0035 §D3 proposed exactly this
  shape (an index as the sole arbiter after explicitly removing the app-level pre-check) and is why the
  rule exists; ADR-0038 §Context found the same hole live on the promo per-user index.
- **How to re-derive the current list** — never copy one, it decays every sprint:
  ```
  grep -rn "AreNullsDistinct" src/Cleansia.Infra.Database/EntityConfigurations/
  grep -rn "IsUnique"          src/Cleansia.Infra.Database/EntityConfigurations/   # then read which keys carry TenantId
  ```
  `.AreNullsDistinct(false)` is a **shipped, precedented** construct on this database (it is emitted in
  the committed `Initial`), so *"we don't do that here"* is a false invariant — but adding it to an
  **existing** index is an owner-only `ef-migration` and index creation fails on pre-existing duplicates,
  so de-duplication comes first. **The reviewer checks the emitted DDL, not the C# builder call.**
- **What already guards it:** `src/Cleansia.Tests/Infrastructure/NullsNotDistinctIndexModelTests.cs`
  asserts the option on each sole-arbiter index **and** carries a negative control (`UserMemberships`,
  a backstop behind an app-level assert, deliberately left nulls-distinct) so the theory cannot pass on
  a reader that answers `false` for everything. It runs in `backend-ci.yml`'s *"Unit tests
  (Cleansia.Tests)"* step, which has no `continue-on-error`, so dropping the option goes red in CI.
  Its roster is **hand-maintained** — a new sole-arbiter index is not caught until someone adds a row.
- **The instance the roster was missing, and it is the biggest one: `Users (TenantId, Email)`.**
  `src/Cleansia.Infra.Database/EntityConfigurations/UserEntityConfiguration.cs:95-97` **declares that
  index to be the guarantee that closes the register/update TOCTOU race**, and all four
  `User`-creating writers are read-then-insert with no lock, so the declaration is right about the
  *role*. It shipped as `.IsUnique()` alone, which cannot play that role: duplicate `(NULL, email)` rows
  were insertable, and the downstream cost is silent account loss (login's `FirstOrDefaultAsync` picks
  arbitrarily; `src/Cleansia.Infra.Database/Repositories/UserRepository.cs:157-172` charges every
  matching row). This is the case that shows the section's own warning is not hypothetical — the reverse
  direction bites *now*, in the mode the platform actually runs in. Decided by **ADR-0050 is `proposed`**
  (`docs/decisions/adr-0050.md:3`):
  arm the index, map the resulting `23505` to the business error, gate the migration on a duplicate
  census. **Retires when:** that status line stops reading `proposed`.
  **State today: half-landed, and the halves are visible in different places.** `:112-114` carries
  `.AreNullsDistinct(false)` and every writer maps the violation; the **emitted DDL still does not**,
  because that arrives only with the owner-run `Initial` regen behind the census. The model guard is
  green either way — it reads `ctx.Model`, so it cannot see the database. Read the migration, not the
  test, to know whether the index fires.
- **The arbiter test, so the two bullets above stop being a judgment call** (ADR-0050 §D1): *is there a
  lock, an `ON CONFLICT`, or a serializable boundary between the read and the write?* If not, the
  pre-check is a courtesy and the index is the sole arbiter, however carefully the pre-check reads.

### (2) The query-filter half — the sharper defect, and it has no guard at all

**A reader who learns only the index rule will still walk into this one.** The filter body
(`CleansiaDbContext.ApplyTenantQueryFilters`, quoted in Axis (a)) is
`providerNull || (currentTenantId == null && e.TenantId == null) || e.TenantId == currentTenantId`.
With `currentTenantId = "T1"` and `e.TenantId = NULL`, the middle clause is false and the last is SQL
`NULL` — **the row is excluded**. So:

> **On activation, every `TenantId NULL` row becomes invisible to every tenanted caller.** Not
> duplicated, not conflicting — *gone*. And seeded configuration rows are `TenantId NULL` **on purpose**
> (`sql-scripts/insert_seed_data.sql` — *"TenantId NULL = single-tenant default"*).

The failure is silent and directional: a feature that reads its own configuration through the filter
does not error, it reads **empty** — and then reports the emptiness as whatever "no configuration"
means. **ADR-0041's schema challenge is the worked example, and it is the reason this half is written
down.** `CH-S4` (`adr/challenges/0041-schema.md:268-335`) traces it end to end: the ADR declared its two
per-country *config* tables `ITenantEntity`, D4.6 lands their rows by owner SQL (⇒ `TenantId NULL`), so
`ResolveCurrentVersionAsync` returns nothing for a tenanted cleaner, and **D4.3 reports that
invisibility as `required: false`** — the legal gate switches itself off, in the unsafe direction,
indistinguishable from *"the owner hasn't written the text yet"*. The panel lead sustained it as
**blocking** and ruled `RB-9`: the two config tables become tenantless `BaseEntity` (the sibling
precedent is `CountryInvoiceConfig`, which is `BaseEntity` for the same reason), *"so the
`.AreNullsDistinct(false)` question evaporates rather than being answered on the wrong grounds."*

**The finding worth keeping is the ordering of the two halves**, which the same challenge states
plainly: *"the real tenancy hole is not on a unique index — it is on the query filter."* The design had
reached for `.AreNullsDistinct(false)` on every new tenant-scoped index as an S8 formality, which
**answers a question that is not the exposure**. Read together:

| | Index half | Filter half |
|---|---|---|
| Today (`TenantId == null`) | the constraint does nothing | the filter does nothing (the `null == null` clause matches) |
| On activation | starts **rejecting** writes | starts **hiding** rows |
| Failure direction | loud — an exception on a path that may not expect one | **silent** — an empty read reported as a legitimate "none" |
| Guard that exists | `NullsNotDistinctIndexModelTests` (T1-CI, hand-rostered) | **none**; S8 asks *"could two tenants have rows here?"* and stops there |

**So the question to ask of a new tenant-scoped table is not one question but two:** *(a)* if an
invariant rests on a `(TenantId, …)` unique index, is that index the **sole arbiter** of a concurrent
claim (⇒ `NULLS NOT DISTINCT`) or a **backstop** behind an authoritative app-level assert (⇒ leave it,
and say which is the arbiter)? and *(b)* **who writes the rows, and with what tenant?** If the answer is
*"seeded/owner SQL, `TenantId NULL`"* while the readers are tenanted, the table is **platform config**
and should not be `ITenantEntity` at all — or the read must be the shipped S8 remedy
(`IgnoreQueryFilters()` **plus** an explicit `e.TenantId == current || e.TenantId == null` predicate, so
a tenant sees its own override falling back to the platform row), pinned by a test that seeds a
**non-null** tenant. A test that only ever seeds `null` cannot fail either way.

**Nothing here changes an index, an entity or the filter.** This is a note; any index change is a
separate ticket with an owner-run `ef-migration`.

### (3) The read half — which reads stand OUTSIDE the filter, and why the other two halves needed it

The two halves above are about rows. This one is about readers, and it is the half that produces the
recurring bug reports, because until ADR-0051 the answer lived as three war stories in
`security-rules.md` §S8 rather than as a test.

**The test (ADR-0051 §D1):** *can the ambient tenant at the moment a row was **written** differ from the
ambient tenant at the moment it is **read**?* If not, the read stays inside the filter. If so, it
bypasses **and re-pins on a predicate bound to the caller**. Four cells, every shipped bypass and every
shipped filtered read sorts into one, and the matrix itself lives in the catalog rather than here.

**What this half adds to the other two, and it is the reason it is written down as a third half rather
than folded in:**

- **It closes the "which cell is my case?" gap that the war-story form left open.** The catalog's own
  §S8 mis-filed `EmployeeRepository.GetByUserEmailIgnoringTenantAsync` under the *sweep* story, which
  has a loop and a write-back; that read has neither. A reader matching against narratives will keep
  making that error; a reader answering one question cannot.
- **It is the half with the fewest defects and the most fear.** Unlike the filter half, this one has
  guards: `UserRepositoryTokenLookupTenantTests` confines every `UserRepository` bypass to an
  enumerated roster and pins the confirm family filtered; `EmployeeRepositoryTenantTokenLookupTests`
  pins the write-authenticated / read-anonymous cell with a **non-null** seed. Both are `T1-CI`. The
  exposure is not the shipped sites — it is the *next* one.
- **It is where dormancy cuts the other way.** Halves (1) and (2) say a dormant `TenantId` under-enforces
  and over-hides. Here dormancy makes the *symmetric* cells trivially correct and permanently so, which
  is why ADR-0051 §D3 keeps the confirm-family lookups filtered and spends no S8 exception on them.

| | Index half | Filter half | **Read half** |
|---|---|---|---|
| Today (`TenantId == null`) | the constraint does nothing — **and `Users` proves that is not harmless** | the filter does nothing | symmetric cells are correct; asymmetric cells are already bypassed |
| On activation | starts **rejecting** writes | starts **hiding** rows | the symmetric cells acquire a population and become asymmetric |
| Guard that exists | `NullsNotDistinctIndexModelTests` (T1-CI, hand-rostered, **missing `Users`**) | **none** | two roster/pin tests (T1-CI) over **two repositories only** |

## Where multi-tenancy actually stands — dormancy, and what it is owed

The owner declined the activation pack (**ADR-0028 is `DECLINED`**,
`docs/decisions/adr-0028.md:3` — **retires when:** that status line stops
reading `DECLINED`) and separately recorded, on Q-VS-03, *"we won't have franchises, DON'T
OVERCOMPLICATE THINGS"* as a **standing instruction**
(`agents/archive/2026-08/backlog/questions/open.md:2324-2337`). Read together, multi-tenancy is a **dormant seam on no
roadmap**, and that is the frame every future tenancy decision starts from.

**What a dormant seam is owed, and what it is not:**

- **Owed: it may not be cited as an enforcement mechanism.** A dormant column in a unique index
  arbitrates nothing; a dormant tenant in a query filter hides nothing. Any design whose safety argument
  runs through `TenantId` is, today, unguarded — half (1)'s `Users` instance is the worked example, and
  it was missed precisely because "it's in the unique index" reads like a guarantee.
- **Owed: the cheap, meaning-preserving corrections.** Arming an index (`.AreNullsDistinct(false)`) and
  naming a read's cell cost one builder call and one comment, change no behaviour in the dormant world,
  and keep the seam reopenable. These get done.
- **NOT owed: machinery for the activation event.** Host→tenant registries, chooser UIs, per-tenant DNS,
  written-down index flips — Q-VS-03's second consequence is the rule: **ask whether the hedge's premise
  is real before pricing the hedge.** A written-down contingency is not free; it is a thing every future
  reader has to read.
- **NOT owed: a backfill.** The founding tenant's identity stays `NULL`, permanently. The filter's
  middle clause *is* its scoping.

**The trigger that reopens all of this** is a real second brand, and if it ever fires the first thing to
re-read is ADR-0028's appended 2026-08-11 challenge section, not its body — two of its claims do not
survive.

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
