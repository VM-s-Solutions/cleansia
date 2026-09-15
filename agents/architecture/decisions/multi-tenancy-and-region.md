# Multi-tenancy and multi-region — how the two axes compose (living decision note)

> **Rewritten 2026-09-13 for [ADR-0061](../../../docs/decisions/adr-0061.md).** Tenancy is **active**:
> a tenant is an operating company under the holding, every stamped row carries one, and `NULL` is not a
> tenant. The dormancy section this note used to carry is retired; what it described is kept below in
> the past tense as *"What activation changed"*, because the two failure directions it named are the
> reason the design is shaped as it is. The market material — CZK-only plans, a customer with no market,
> copy that states a money figure — was superseded earlier the same day by
> [ADR-0058](../../../docs/decisions/adr-0058.md), [ADR-0059](../../../docs/decisions/adr-0059.md) and
> [ADR-0060](../../../docs/decisions/adr-0060.md).

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

## Axis (a) — app-level multi-tenancy: VERIFIED against the code (2026-09-13, after ADR-0061 landed)

| Fact | Evidence |
|---|---|
| A **tenant is an operating company** under the holding: `Tenants (Id varchar(26), Name, IsActive)`, seed-only, one row today (`cleansia-cz`, "Cleansia CZ s.r.o."). No DTO, no admin surface; one repository member since 2026-09-15 — `ITenantRepository.GetAllIdsAsync`, read by the retention job (deactivated companies included; `IsActive` is read by nothing) | `Core.Domain/Tenancy/Tenant.cs`; `Core.Domain/Repositories/ITenantRepository.cs`; `sql-scripts/insert_seed_data.sql` (the first insert) |
| A **country is served by at most one operator**: `CountryConfiguration.OperatorTenantId` (nullable, FK → `Tenants`, indexed). CZE → `cleansia-cz`; every other configured country → nobody | `Core.Domain/Configuration/CountryConfiguration.cs`; the `Initial` DDL (`FK_CountryConfigurations_Tenants_OperatorTenantId`) |
| `TenantId` lives on **`TenantAuditable : Auditable, ITenantEntity`** (since 2026-09-15 — a plain `Auditable` has no tenant column); `string?` in C#, **NOT NULL and `FK_<T>_Tenants_TenantId` (Restrict, no navigation) in the database** on all 48 stamped tables (46 `TenantAuditable` + the two `BaseEntity + ITenantEntity` audits); only `OutboxMessages` and `DeadLetters` stay nullable (`TenantAuditableEntityConfiguration.TenantIdNullableTypes`) | `Core.Domain/Common/TenantAuditable.cs`; `EntityConfigurations/EntityConfiguration.cs`; `grep "TenantId" …Initial.cs \| grep "nullable: true"` → the two exemptions only; `InitialMigrationTenantDdlTests` pins 48 / 2 |
| Every `ITenantEntity` is auto-scoped by a **global query filter** (a loop over all entity types) | `CleansiaDbContext.ApplyTenantQueryFilters` — the whole method, byte-untouched by ADR-0061 |
| Filter body: `tenantProvider==null  ‖  (currentTenantId==null && e.TenantId==null)  ‖  e.TenantId==currentTenantId`. The middle clause now **matches nothing** on a stamped table — a reader with no tenant reads nothing, which is the safe direction | same method, the `body` expression |
| Tenant resolved from the **`tenant_id` JWT claim**, no header; the claim is minted from `User.TenantId`, which is never null | `TenantProvider.cs` (`TenantClaimType="tenant_id"`); `AuthExtensions.SetClaims` |
| **Anonymous writers resolve their operator from the market**: an `IOperatorScopedRequest` names a `CountryId` (null = the default market), `OperatorTenantScopeBehavior` runs before validation, `OperatorTenantResolver` answers *(is it a market?, who operates it?)* and the behaviour sets the override — or refuses `country.not_serviced` / `tenant.not_found` | `Core.AppServices/Tenancy/*`; `FluentValidationExtensions.cs` (registered between `PostCommitDispatchBehavior` and `ValidationPipelineBehavior`) |
| **A request that authenticates a user adopts the user's tenant** before it writes the `RefreshToken` | `Services/TokenService.cs:44-47`; `Features/Auth/RefreshToken.cs:102` |
| Cross-tenant jobs/webhooks use **`SetTenantOverride`** per row or per tenant group and commit inside the loop; a job whose unit is a *company* (the retention sweeps, each reading its company's own `TenantSettingCatalog` windows) loops `ITenantRepository.GetAllIdsAsync` and sets the override per company; `CommitAsync` stamps every `Added` `ITenantEntity` from the ambient tenant | `TenantProvider.cs`; `CleansiaDbContext.CommitAsync`; `Features/DataRetention/DataRetentionBackgroundService.cs` |

**This does not change for multi-region.** Region is **not** added to the tenancy filter — a region clause in
`ApplyTenantQueryFilters` would be a **conflation finding**. A tenant has exactly one home region, so its rows
live in one region's DB and `e.TenantId == currentTenantId` is sufficient *within* that DB. ADR-0017 D3's
*"two markets ⇒ two tenants"* is superseded: an operator serves one or more countries, so the check for the
day `HomeRegion` lands is *"every country of one operator shares a region"*.

## Which tables are stamped — the D7 rule

> A table is **stamped** (`ITenantEntity`) when its rows are created by or for one operator's customers,
> cleaners or money — or when the row *is* the operator's own legal or financial configuration (its issuer
> identity, its pay rates, its fiscal counter, its campaigns). A table is **tenantless** (no interface;
> `Auditable` if admin-edited, `BaseEntity` if seed-only) when it is the brand's catalogue or programme
> definition that every operator sells identically, or a per-country fact.

The reviewer's sort for the next table: it is stamped if a scenario exists in which two operators
legitimately hold *different* rows of it for the same key; otherwise it is tenantless and says why in a
one-line comment naming its sibling (S8's requirement). `LoyaltyTierConfig` was the one table that
changed class on activation (the brand's programme, the `MembershipPlan` sibling); `CompanyInfo`, the
platform-default `EmployeePayConfigs`, the seeded `PromoCodes` and the dev admin stayed stamped and were
re-homed to `cleansia-cz` in the seed. 48 types implement `ITenantEntity` today — 46 through
`TenantAuditable` (`grep -rn ": TenantAuditable" src/Cleansia.Core.Domain`) plus the two audits that
name the interface directly (`grep -rn ", ITenantEntity"`) — 46 required columns plus the two
exemptions. Two joined on 2026-09-15 by the rule above: `PayoutReferenceCounter` (each company numbers
its own payout invoices — two companies hold different rows for the same year and scope) and, already
stamped but now with a writer, `TenantConfiguration` (a company's own overrides of catalogued
settings). The 21 tenantless `Auditable` tables carry no `TenantId` column at all since the same day.

## What activation changed — the two failure directions, and where each landed

> Until 2026-09-13 every row carried `TenantId NULL`, and two guarantees that read as "the database
> enforces this" were enforced by nothing. They failed in **opposite** directions on the day a non-null
> tenant first existed, and two design panels in one sprint reasoned past one or the other — which is why
> the shape was written down here rather than re-derived a third time. This section is now the record of
> what was true and how each half was closed; the rule itself still lives in the catalog
> (`agents/knowledge/consistency.md` §*"Tenant-scoped unique indexes: `NULLS NOT DISTINCT` is decided by
> the index's JOB, not by a majority"*).

### (1) The index half — was inert, is armed, and the tenant term no longer needs the option

PostgreSQL treats NULLs in a UNIQUE index as **DISTINCT** by default, so while `TenantId` was `NULL` a
unique index whose key included it constrained nothing — two otherwise-identical rows both inserted and
`ON CONFLICT DO NOTHING` never fired. The dangerous direction was the reverse: anywhere an app-level guard
had been **removed** on the strength of such an index, the invariant was unguarded in the mode the
platform actually ran in (ADR-0035 §D3 proposed exactly that shape; ADR-0038 §Context found it live on
the promo per-user index; `Users (TenantId, Email)` was the biggest instance — ADR-0050).

**What is true now:**

- **`TenantId` is NOT NULL** on every stamped table (ADR-0061 D8) — and, since 2026-09-15, a `Restrict`
  foreign key into `Tenants` — so the tenant term can never be null and the option is vacuous *on that
  term*. It is kept on the thirteen `(TenantId, …)` sole-arbiter indexes anyway, because the model guard
  reads the option, not the column, and the non-tenant nullable terms (`EmployeeId`, `ServiceId`,
  `PackageId`) still need it.
- **The emitted DDL carries `NullsDistinct=false` on 17 unique indexes** — thirteen with a tenant term
  (`EmployeeInvoices` ×2, `EmployeePayConfigs`, `EmployeePayoutDetails`, `FiscalCounters`,
  `LoyaltyTransactions`, `MembershipBenefitUsages`, `OrderReceipts`, `PayoutReferenceCounters`,
  `PromoCodeRedemptions`, `PromoCodes`, `ReferralCodes`, `TenantConfigurations`) and four without
  (`DisputeLines`, `LegalDocuments`, `LiveActivityTokens`, `OrderReviewLines`).
  The earlier claim in this note that *"the emitted DDL still does not"* was stale before activation and
  is false now: `grep -n "NullsDistinct" src/Cleansia.Infra.Database/Migrations/*Initial.cs` is the check.
- **`Users` left the roster.** `IX_Users_Email` is globally unique with no tenant term (ADR-0061 D5.1,
  superseding ADR-0050 D1/D4): one identity per email across the holding, because every anonymous
  identity read already resolved by email ignoring the tenant. ADR-0050 D2 — the four writers map the
  `23505` to `ExistingUserWithEmail` — stands and is what makes the global index safe.
- **Two indexes gained a tenant term on activation** so two operators can coexist:
  `OrderReceipts (TenantId, ReceiptNumber)` (the number comes from a per-tenant `FiscalCounter`, so two
  operators' first receipts of a year are the same string) and `IX_EmployeePayConfigs_Tenant_Scope`
  `(TenantId, EmployeeId, ServiceId, PackageId, CurrencyId)` (two operators each hold a platform default
  for the same service and currency). **Three more on 2026-09-15** (owner ruling Q-TENANCY-02, each
  company numbers its own payout invoices — T-0757): `EmployeeInvoices (TenantId, InvoiceNumber)`,
  `EmployeeInvoices (TenantId, VariableSymbol)` filtered, and the counter behind both,
  `IX_PayoutReferenceCounters_Tenant_Year_Scope`. The hand roster is thirteen rows.
- **The only index whose liveness activation changed** is the deliberate `NULLS DISTINCT` backstop
  `UserMemberships (TenantId, UserId) WHERE Status = 1`, and both of its writers already own the `23505`
  (ADR-0061 D9, the ADR-0038 §D3 question answered).
- **The guard:** `NullsNotDistinctIndexModelTests` has a thirteen-row hand roster **and** a roster-free
  sweep over every unique index in the model, so a new unique index over a nullable column cannot slip
  past the list. It reads `ctx.Model`, not the database — the DDL is still the reviewer's to read.
- **How to re-derive the current list** — never copy one:
  ```
  grep -n "NullsDistinct" src/Cleansia.Infra.Database/Migrations/*Initial.cs
  grep -rn "AreNullsDistinct" src/Cleansia.Infra.Database/EntityConfigurations/
  ```
- **The arbiter test** (ADR-0050 §D1) is unchanged: *is there a lock, an `ON CONFLICT`, or a serializable
  boundary between the read and the write?* If not, the pre-check is a courtesy and the index is the sole
  arbiter, however carefully the pre-check reads.

### (2) The query-filter half — was the sharper defect, is closed at the root

The filter body is `providerNull || (currentTenantId == null && e.TenantId == null) || e.TenantId ==
currentTenantId`. With `currentTenantId = "T1"` and `e.TenantId = NULL`, the middle clause is false and
the last is SQL `NULL` — **the row is excluded**. So on activation every `TenantId NULL` row would have
become invisible to every tenanted caller: not duplicated, not conflicting — *gone* — and seeded
configuration rows were `TenantId NULL` **on purpose**. The failure was silent and directional: a feature
that read its own configuration through the filter did not error, it read **empty** and reported the
emptiness as whatever "no configuration" meant. ADR-0041's `CH-S4` traced it end to end (a legal gate
switching itself off, in the unsafe direction) and ruled `RB-9`: the two config tables became tenantless.

**What is true now:**

- **There are no `NULL` business rows to hide.** The column is NOT NULL (D8); the five seeded config
  tables are either tenantless (`LoyaltyTierConfigs`, D7) or seeded with the operator (`CompanyInfo`,
  `EmployeePayConfigs` defaults, `PromoCodes`, the dev admin). `SeededDatabaseHasNoOrphanTenantRowsTests`
  applies the seed to the migration-built database and asserts zero `NULL`s, closure into `Tenants`, and
  an operator on the default market; `SecondTenantIsolationHostTests` proves the re-homing by an admin
  *read* of the seeded rows, not only by the count.
- **The middle clause now matches nothing.** A job that forgot its override reads nothing rather than
  someone's rows — the loud direction. The clause is not removed: ADR-0017, ADR-0050 and ADR-0061 all
  pin the filter diff-empty.
- **The next writer that forgets its market fails loudly**: a `23502` on first use in DEV, instead of
  orphaned rows a human notices as an empty list. That is why D8 is a column and not only a test.

**The two questions for a new tenant-scoped table** are unchanged and both are answered by the D7 rule
above: *(a)* if an invariant rests on a `(TenantId, …)` unique index, is that index the **sole arbiter**
of a concurrent claim or a **backstop** behind an authoritative app-level assert? and *(b)* **who writes
the rows, and with what tenant?** — if the answer is "the seed, and the readers are tenanted", the table
is config and is tenantless. The `IgnoreQueryFilters()` + `e.TenantId == current || e.TenantId == null`
fallback shape this note used to recommend for platform rows **no longer has a case**: a row is either
stamped and filtered, or tenantless and unfiltered.

### (3) The read half — which reads stand OUTSIDE the filter, and what activation moved

**The test (ADR-0051 §D1):** *can the ambient tenant at the moment a row was **written** differ from the
ambient tenant at the moment it is **read**?* If not, the read stays inside the filter. If so, it
bypasses **and re-pins on a predicate bound to the caller**. Four cells, every shipped bypass and every
shipped filtered read sorts into one, and the matrix itself lives in `security-rules.md` §S8.

**What activation moved — two cells changed population, no rule changed:**

- **The register / resend pre-checks and the legacy confirm read moved to the bypass side.** They were
  "written anonymous, read anonymous" symmetric cells while every row was `NULL`. Now `Register` stamps
  the market's operator and the confirm link is clicked anonymously, so the confirm read is asymmetric
  (`GetByConfirmationCodeIgnoringTenantAsync`, pinned by the code hash — ADR-0061 D4 corollary); and the
  pre-checks read **across** the holding on purpose (`GetByEmailIgnoringTenantAsync` /
  `ExistsWithEmailIgnoringTenantAsync`), because one email is one identity everywhere (D5.1). The
  unconfirmed re-registration branch compares the found row's tenant with the ambient one and refuses
  `ExistingUserWithEmail` otherwise.
- **`Order/Lookup` and `LookupBatch` bypass on the secret** (`ConfirmationCode` + `CustomerEmail`) — a
  guest who booked under operator A must find the order without knowing which operator that was.
- **"The email is unique across the platform" is now a permitted pin**, because the schema enforces it
  (`IX_Users_Email`). S8's sentence forbidding it is rewritten.
- **The dormancy asymmetry is gone.** Halves (1) and (2) used to under-enforce and over-hide; this half
  used to be trivially correct on symmetric cells. All three now run in the world production runs in,
  and the guards — `UserRepositoryTokenLookupTenantTests` (roster + confirm-family pin, one row moved),
  `EmployeeRepositoryTenantTokenLookupTests` (non-null seed) — are unchanged in shape.

| | Index half | Filter half | **Read half** |
|---|---|---|---|
| Before activation (`TenantId == null`) | the constraint did nothing — and `Users` proved that was not harmless | the filter did nothing | symmetric cells were correct by dormancy; asymmetric cells were already bypassed |
| **Now** | armed on 13 indexes in the DDL; `Users` globally unique; two indexes gained a tenant term | no `NULL` rows exist; the middle clause matches nothing | the confirm read and the register pre-checks joined the bypass roster with their pins; the secret-keyed lookups bypass |
| Guard | `NullsNotDistinctIndexModelTests` (hand roster + model sweep) | `TenantIdRequiredModelTests`, `TenantIdNotNullEnforcedTests`, `SeededDatabaseHasNoOrphanTenantRowsTests`, `SecondTenantIsolationHostTests` | the two roster/pin tests, `AnonymousWriterLandsInMarketOperatorTests` |

## Where multi-tenancy stands — active, one operator, the second one priced

The owner ruled on 2026-09-13: *"We'll make a holding company and more companies under it for each
region. So I think that assigning it from the beginning is the easiest approach."* That reverses the
dormancy posture this note carried (ADR-0028's `DECLINED` no longer describes the roadmap, though its
host→tenant *design* stays declined — ADR-0061 Alternative (c)), and Q-VS-03's *"ask whether the
hedge's premise is real before pricing the hedge"* now governs *how much machinery* the seam costs
rather than *whether* it exists.

**What is built:** the registry, the operator map, the anonymous scope, the token-mint adoption, the two
agreement rules (`order.country_operator_mismatch`, `employee.work_country_operator_mismatch`), NOT NULL,
the re-homed seed. **What is not, on purpose:** no admin CRUD for `Tenants` or the map, no holding-level
view, no host resolution, no `TenantId` in any DTO.

**What a second operating company costs** (ADR-0061 D12 — six steps, no code): a `Tenants` seed row;
`OperatorTenantId` on that country's configuration; a `CompanyInfo` row for the issuer; the first admin
by SQL (because `CreateAdminUser` stamps the *creating* admin's tenant — the reason the holding-level
view is the next tenancy ticket); pay defaults entered by that admin; and the clients sending
`countryId` on register / social / promo / referral (T-0728), which must land *before* the second market
opens or its visitors register into `cleansia-cz`.

**Standing questions, each with its default applied** (`agents/backlog/questions/open.md`
Q-TENANCY-01..05): cross-operator booking on one account (refused), invoice numbering per legal entity
(global), `Tenant.IsActive` (no reader), `TenantConfiguration` (no writer), one identity per email (yes).

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
  filter. Onboarding a market = the ADR-0058 data steps (`docs/architecture/platform-expandability.md`
  §8) plus, for a market a *new* operating company serves, the six ADR-0061 D12 steps — zero infra, zero
  schema. A market served by an existing operator is only the `OperatorTenantId` value on its
  configuration row.
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
