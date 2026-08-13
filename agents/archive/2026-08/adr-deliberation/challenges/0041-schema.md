# ADR-0041 — Challenge, schema / data-integrity / migration lane (D1, D2, D3, D6.2, D7, D9 + the migration)

**Mode:** challenger. **Gate 0:** REFUTED-by-default — every claim below cites a `file:line` I opened
in the working tree on 2026-08-05, or a shipped artifact I read end-to-end. **No migration was created
or run**, no `dotnet ef` command was issued, no git write, no ADR edit, nothing outside
`agents/archive/2026-08/adr-deliberation/challenges/`.

**Headline.** The *shape* argument (a rich record over a boolean, a version over a flag) survives —
I attacked it from four directions and could not break it, and §"Found sound" says how. What does not
survive is the **enforcement** layer underneath it, and it fails in three independent places:

1. **"Append-only" is enforced by nothing, and D7 makes it structurally un-enforceable** — while the
   ADR simultaneously picks the *mutable* entity archetype and ignores the one append-only,
   tenant-scoped log this schema already ships (`AdminActionAudit`). (CH-S1)
2. **D9's premise is false in the working tree.** F5 says the invoice sweep runs with no tenant claim.
   It does not — **both** invoice-issuance paths already resolve and set the tenant before issuing
   (`GenerateInvoiceHandler.cs:48-63`, `PayPeriodBackgroundService.cs:120-141`). The
   `…IgnoringTenantAsync` variants D9 makes *mandatory* would therefore **introduce** a cross-tenant
   read on a legal record rather than prevent one. (CH-S3)
3. **The real tenancy hole is not on a unique index — it is on the query filter.** Both new *config*
   tables are declared `ITenantEntity`; every comparable table in this schema is deliberately
   tenantless; seeds stamp `TenantId NULL` on purpose; and the filter at `CleansiaDbContext.cs:244-251`
   makes a NULL-tenant row **invisible to every tenanted caller**. Under the ADR's own D4.3, that
   invisibility is reported as `required: false` — the feature switches itself off, silently, in the
   unsafe direction. (CH-S4)

Then two that are cheap to fix and expensive to miss: **the ADR stamps one of the two `EmployeeInvoice`
writers** (CH-S5), and **the migration's urgency argument is misattributed while its actual failure mode
is unnamed** (CH-S12) — the same `Initial`-regeneration no-op ADR-0040's challenger already documented.

Fourteen findings. **CH-S1, CH-S2, CH-S3, CH-S4, CH-S5, CH-S10 and CH-S12 I consider blocking.**

**Citation sampling (Gate 0).** I spot-checked nine of the ADR's own citations. Seven are exact
(`UserConsentEntityConfiguration.cs:31-32`; `Initial.cs:848`; `Initial.cs:1625-1665`;
`ConsentService.cs:16-19`; `Auditable.cs:35-42`; `EmployeeRepository` `GetByIdAsync`/`…IgnoringTenant`
at `:43-57` vs the cited `:44-57`; `insert_users_employees.sql:53-111` — the *rows* are there). Two do
not hold: **`WithdrawConsent.Command` is at `:11`, not `:12`** (immaterial), and **the claimed "zero
hits" for `rg -i 'samofaktur|self.?bill|…'` over `src/` returns one file** —
`src/cleansia_ios/CleansiaCustomer/Sources/Features/Membership/Data/MembershipModels.swift`, a regex
false positive on `self.billingPeriod`, so the *conclusion* stands. Citation hygiene is good. The
problems below are not citation drift; they are conclusions drawn from citations that are individually
correct.

---

## CH-S1 — "Append-only" is a word, not a constraint. Nothing in the database, nothing in EF, and nothing in the repository interface prevents an UPDATE or a DELETE — and D7 requires that nothing ever can. **BLOCKING**

**The hole.** D1 declares `EmployeeAgreementAcceptance : Auditable, ITenantEntity // APPEND-ONLY. Never
updated, never deleted.` That comment is the entire enforcement. Four layers, all open:

- **Database.** There is no check constraint, no trigger, no rule, no column-level grant in this
  repository. `rg -n "HasCheckConstraint|HasTrigger|CREATE TRIGGER" src/Cleansia.Infra.Database`
  returns **zero** matches (the only `migrationBuilder.Sql(` in the tree is
  `Extensions/MigrationBuilderExtensions.cs:11`, a seed-file executor). The ADR proposes none either.
- **EF.** `CleansiaDbContext.CommitAsync` (`:96`) actively *supports* mutation:
  `else if (entity.State == EntityState.Modified) entity.Entity.Updated(stateUser, currentTime);` —
  an update to an "append-only" row is stamped and committed like any other.
- **Repository.** `IRepository<TEntity, TKey>` (`src/Cleansia.Core.Domain/Repositories/IRepository.cs`)
  declares `void Remove(TEntity)` (`:41`), `void RemoveRange(…)` (`:43`), `void Deactivate(TEntity)`
  (`:45`), `void DeactivateRange(…)` (`:47`). `BaseRepository` implements all four
  (`BaseRepository.cs:122-146`). Any `IEmployeeAgreementAcceptanceRepository : IRepository<…>` — which
  is the only shape in this codebase — **ships a hard delete and a soft delete on its public API on day
  one**, with no reviewer prompt.
- **The entity.** `Auditable` (`Common/Auditable.cs`) carries public `Updated(…)` and `Deactivated(…)`
  and inherits `BaseEntity.IsActive { get; set; }` — a *public setter* (`BaseEntity.cs:9`).

**And D7 forecloses the obvious fix.** D7 requires `RedactMetadataForEmployeeAsync(employeeId)` to set
`IpAddress = null, DeviceLabel = null` on existing rows — an **UPDATE on the append-only table**. So a
`REVOKE UPDATE`, a `BEFORE UPDATE … RAISE` trigger, or an `init`-only entity cannot be adopted without
carving an exception for exactly one path. That is fine — but it must be *stated*, because the moment
one sanctioned mutator exists, "never updated" is no longer a property anybody can check, and the ADR
presents it as one. §How-a-reviewer-verifies has fourteen checks and **not one of them checks
append-only-ness**.

**Why it matters — the archetype the ADR picked is the wrong one, and the right one is already
shipped.** D1 says *"The archetype is the house one, per ADR-0034 D1's 'Not EF owned types' ruling: a
`class : Auditable, ITenantEntity` … exactly like `EmployeePayConfig`, `EmployeeInvoice`, `PayPeriod`,
`EmployeePayoutDetails`."* Every one of those four is a **mutable** record — pay rates get updated,
invoices get approved/paid, periods get closed, payout details are *"mutated in place, never
tombstoned"* (CLAUDE.md, ADR-0034). ADR-0034 line 74 is explicit that this is the archetype *"for a
related record"*. It was never the archetype for a log.

This schema **does** have an append-only, tenant-scoped log, and the ADR does not mention it:

```csharp
// src/Cleansia.Core.Domain/Auditing/AdminActionAudit.cs:6-20
public sealed class AdminActionAudit : BaseEntity, ITenantEntity
{
    public string? TenantId { get; set; }
    public string ActorId { get; init; } = default!;
    public string? ActorEmail { get; init; }
    public UserProfile ActorProfile { get; init; }
    …
}
```

`sealed`, `BaseEntity` **not** `Auditable`, every payload property `init`-only, and its configuration
(`AdminActionAuditConfiguration.cs:7-21`) maps `TenantId` by hand precisely *because* it does not
inherit `AuditableEntityConfiguration`. That shape gives, for free, everything ADR-0041 asks for in
prose: no `UpdatedBy`/`UpdatedOn`/`DeactivatedBy`/`DeactivatedOn` (four columns that are meaningless on
an immutable row and misleading when populated), and **no C# path that can mutate a loaded instance**.
It also carries its own `OccurredOn` column — the same "the legal timestamp is not the infrastructure
timestamp" decision D2.3 re-derives from scratch. `AdminActionAudit` is the answer to D2.3 and half of
D1, and the ADR reasons its way to it without citing it.

**What I want changed.**

1. Either the word "append-only" is **spelled as a mechanism**, or it is deleted from D1. The
   mechanism I would accept, in the order I would try it:
   (a) the entity is `sealed : BaseEntity, ITenantEntity` with `init`-only payload properties, mapping
   `TenantId` by hand as `AdminActionAuditConfiguration.cs:19-21` does;
   (b) the repository interface **does not derive** from `IRepository<T, string>` — it declares only
   `Add`, the named reads and the one D7 redaction method (there is precedent for a narrow interface
   surface; nothing forces the generic base);
   (c) D7's redaction is the **single** named mutator, called out in D1 as the one exception, with the
   two redacted columns explicitly the only mutable ones.
2. A reviewer check is added: *"`EmployeeAgreementAcceptance` exposes no public setter and no mutator
   besides the D7 redaction; its repository interface does not inherit `IRepository`."* A property with
   no test and no check is documentation, and this one is load-bearing for the whole ADR.
3. If the panel keeps `Auditable`, D1 must state what `IsActive` means on these three tables — see
   CH-S13.

**Catalog routing, since this would produce one.** Adopting (a) is a new canonical form for
"tenant-scoped append-only log", and it **carves an exception out of a sentence that already governs the
subject** — ADR-0034's *"The house archetype for a related record is a class : `Auditable,
ITenantEntity`"* governs entity archetypes at a level of generality that covers this case. Per
`conventions.md` §"Who ratifies a catalog edit", the test is semantic, not lexical, so this routes to
the **Architect**, not inline. Flagging it so the lead does not let it be written inline.

*Blocking?* **Yes.** The ADR's entire value proposition is *"the record is still defensible years
later."* A record that any handler can `Remove()` through the interface it is handed is not that, and
the shape that would make it that is already in the tree.

---

## CH-S2 — Not one of the four new foreign keys has a stated `OnDelete`. The house style is explicit-with-a-reason, and the EF default for the shape D1 names is **Cascade** — which deletes the evidence D7 says must survive. **BLOCKING**

**The hole.** D1's schema block names four FKs — `AgreementVersion.CountryId`,
`AgreementVersionText.AgreementVersionId`, `EmployeeAgreementAcceptance.EmployeeId`,
`EmployeeAgreementAcceptance.AgreementVersionId` — plus D6.2's
`EmployeeInvoice.SelfBillingAcceptanceId`. **The ADR states a delete behaviour for none of them.**

**Why it matters.** This repository does not leave that to the default. Every configuration I opened
sets it explicitly and most carry a one-line reason:

- `EmployeePayoutDetailsEntityConfiguration.cs:62-67` — *"Cascade: the payout destination has no
  meaning without its employee…"* → `.OnDelete(DeleteBehavior.Cascade)`
- `EmployeePayoutDetailsEntityConfiguration.cs:69-73` — *"Restrict: a country referenced by a live
  payout destination cannot be hard-deleted."*
- `EmployeeInvoiceEntityConfiguration.cs:87-111` — **four** FKs, all `Restrict`, including
  `Employee` → `Restrict`.
- `RefundEntityConfiguration.cs:51-64` — three FKs, all `Restrict`.
- `UserConsentEntityConfiguration.cs:26-29` — `User` → `Restrict`.

And the emitted DDL confirms what the omission costs. The one shipped instance of the *exact* shape
D1 names for the acceptance (`HasOne(Employee)`, required) is:

```
// src/Cleansia.Infra.Database/Migrations/20260723182623_Initial.cs:1663-1667
name: "FK_EmployeePayoutDetails_Employees_EmployeeId",
…
onDelete: ReferentialAction.Cascade);
```

Cascade is *correct* there — ADR-0034 wants the payout destination gone with the employee. It is
**exactly wrong** here: D7's whole argument is that the acceptance facts must **survive** the person,
because *"destroying the authority for documents that still exist is a worse outcome than retaining a
version id."* A required `HasOne(e => e.Employee).WithMany()` written without an explicit `OnDelete` on
this codebase's conventions lands on Cascade, and then a single `employeeRepository.Remove(employee)`
— a method that exists on the interface, `IRepository.cs:41` — deletes every acceptance row for a
cleaner whose settled invoices are retained as financial record.

Two more consequences the ADR does not price:

- **`EmployeeAgreementAcceptance.AgreementVersionId` → `AgreementVersion`.** D1 says versions are
  immutable and never deactivated, but nothing stops a `Remove`. This FK must be `Restrict` or the
  two-hop join D6.2 promises *"forever"* is one delete away from a dangling id.
- **`EmployeeInvoice.SelfBillingAcceptanceId` → the acceptance.** This one is *optional*, so EF's
  default is `ClientSetNull`: the DB constraint is safe, but if a tracked acceptance is ever removed
  in the same unit of work as a tracked invoice, EF **nulls the frozen stamp** — defeating the freeze
  rule D6.2 leans on (`patterns-backend.md` §B8) and turning an authorized invoice into an
  indistinguishable member of the `IS NULL` bucket (CH-S11).

**What I want changed.** D1 and D6.2 state all five delete behaviours with a reason each, in the house
comment style. My recommendation, and the argument for each:

| FK | Behaviour | Why |
|---|---|---|
| `EmployeeAgreementAcceptance.EmployeeId` → `Employees` | **Restrict** | The record must outlive erasure (D7). Erasure anonymizes + deactivates the employee row (`GdprDeletionService.cs:242-244`) — it does not delete it — so Restrict costs nothing on the live path and blocks the one path that would destroy the evidence. |
| `EmployeeAgreementAcceptance.AgreementVersionId` → `AgreementVersions` | **Restrict** | A referenced version is immutable *and* undeletable, or D6.2's "forever" join is a convention. |
| `AgreementVersionText.AgreementVersionId` → `AgreementVersions` | **Cascade** | A text has no meaning without its version, and no acceptance references a text row directly — unless CH-S8 is adopted, in which case **Restrict**. |
| `AgreementVersion.CountryId` → `Countries` | **Restrict** | Matches every other country FK in the schema. |
| `EmployeeInvoice.SelfBillingAcceptanceId` → acceptances | **Restrict** | The stamp is frozen; nothing may null it, including EF's client-side fixup. |

*Blocking?* **Yes.** An unstated `OnDelete` on a record whose stated purpose is to survive is not an
omission of detail, it is an omission of the decision.

---

## CH-S3 — D9's premise (F5) is falsified at both invoice-issuance call sites. The `…IgnoringTenantAsync` variants it makes **mandatory** would introduce the cross-tenant read they claim to prevent. **BLOCKING**

**The hole.** F5 states: *"Invoices are generated from `PayPeriodBackgroundService` (a timer sweep).
Any read of the acceptance or the agreement version from that path through a tenant-scoped repository
method resolves `TenantId == null` against tenanted rows and returns nothing…"* D9 then makes a
two-variant repository naming *"mandatory, not advisory"*, listing four methods including
`GetCurrentAcceptanceIgnoringTenantAsync` and `ResolveCurrentVersionIgnoringTenantAsync`.

**The premise is false.** Both invoice-issuance paths already do exactly what
`security-rules.md` S8 prescribes — tenant-ignoring *selection*, then `SetTenantOverride` from the
loaded row, then tenant-*scoped* work inside the loop:

**Path 1 — the timer** (`PayPeriodBackgroundService.cs`):
```
:118-121   // "System job — no JWT context. Use IgnoreQueryFilters … group by tenant and set the
           //  override per tenant before mutating"
           .GetQueryableIgnoringTenant()
:133       foreach (var tenantGroup in expiredPeriods.GroupBy(p => p.TenantId ?? string.Empty))
:138-142       _tenantProvider.ClearTenantOverride();
               if (!string.IsNullOrEmpty(tenantGroup.Key)) _tenantProvider.SetTenantOverride(tenantGroup.Key);
:155           await SendPeriodClosedEmailsAsync(period, cancellationToken);   // ← invoices issued in here
:186           await _unitOfWork.CommitAsync(cancellationToken);               // commit INSIDE the loop
```
And inside, `SendPeriodClosedEmailsAsync` reads employees through the **scoped**
`_employeeRepository.GetQueryable()` (`:203`) — deliberately, correctly.

**Path 2 — the queue** (`src/Cleansia.Functions.Core/Handlers/GenerateInvoiceHandler.cs`):
```
:47-48  // "Queue trigger — no JWT context. Look up cross-tenant by the trusted EmployeeId, then set
        //  the tenant override so the EmployeeInvoice … writes inherit the right TenantId."
        var employee = await employeeRepository.GetByIdIgnoringTenantAsync(message.EmployeeId, ct);
:59-61  if (!string.IsNullOrEmpty(employee.TenantId)) tenantProvider.SetTenantOverride(employee.TenantId);
:63     var result = await mediator.Send(new GenerateInvoice.Command(…), ct);
```

So by the time *any* invoice is issued, **the ambient tenant is already the right one**, and a
tenant-scoped read of the acceptance is correct on both paths. F5's *"the sweep does not fail; it
silently agrees with you"* is a real law — it is simply not live here, and the two paths are the
reference implementations of the remedy, not instances of the disease.

**Why it matters — the prescribed fix is worse than the imagined problem.** `IRepository`'s own
doc-comment (`IRepository.cs:51-58`) says `GetQueryableIgnoringTenant()` is *"ONLY for system-level jobs
… that have no JWT"*. Calling `ResolveCurrentVersionIgnoringTenantAsync(countryId, kind)` from inside
`PayPeriodBackgroundService`'s per-tenant block is a read outside a tenant boundary the caller
deliberately established. And unlike the acceptance lookup — which is keyed on an `EmployeeId`, a ULID
that pins its tenant transitively — **the version lookup is keyed on `(countryId, kind)` and is bounded
by nothing**. In a two-tenant deployment it returns every tenant's CZ terms and D4.2's "greatest
`EffectiveFrom`" picks whichever tenant published last. Tenant A's cleaner is then measured against
tenant B's legal text, on the legal path, forever, with no error.

**What I want changed.**

1. **F5 is rewritten** to say what is true: the two issuance paths already establish the tenant, and
   this ADR must *ride* that, not re-solve it. Leaving F5 as written teaches the next reader that a
   shipped, correct sweep is broken — the exact defect `security-rules.md` warns about in its own
   margin note (*"Do not leave a security law asserting a live hole that has been closed"*).
2. **D9 drops both `…IgnoringTenantAsync` variants.** If the panel wants belt-and-braces for a future
   caller that genuinely has no tenant, the correct second variant is an **explicit-tenant** one —
   `ResolveCurrentVersionForTenantAsync(tenantId, countryId, kind, ct)` — not a tenant-ignoring one.
   Ignoring is not a world; it is the absence of one.
3. D9's pinning-test instruction (*"must seed a non-null `TenantId`"*) is **kept** — it is right, and it
   is what would catch CH-S4.

*Blocking?* **Yes.** D9 is stated as mandatory, and as stated it prescribes a cross-tenant read of a
legal record on the money path.

---

## CH-S4 — The real tenancy defect is on the **query filter**, not on a unique index: a NULL-tenant `AgreementVersion` is invisible to every tenanted caller, and D4.3 reports that invisibility as "no agreement required". **BLOCKING**

**The hole.** D9 makes all three tables `ITenantEntity` on the argument that *"a franchise operator is a
different legal entity with different terms and different suppliers."* For the **acceptance** that is
right — it is a per-employee fact. For the two **config** tables it is wrong, and the failure is
silent and in the unsafe direction.

**Why it matters.** Three verified facts compose into it:

1. **`TenantId` is nullable and `NULL` is production today.** `EntityConfiguration.cs:27-29`
   (`.IsRequired(false)`), CLAUDE.md, and `PayPeriodBackgroundService.cs:76-78` (*"Today the system is
   single-tenant in practice (TenantId null)"*).
2. **Config rows are seeded `TenantId NULL` deliberately.**
   `sql-scripts/insert_seed_data.sql:1548` — *"TenantId NULL = single-tenant default (matches existing
   seed entries)"* — and every seeded `LoyaltyTierConfig`/`PromoCode`/`ServiceCity` follows it. D4.6
   says agreement versions arrive by *"owner-run SQL, exactly as the country invoice configs are"*, so
   they will land `TenantId NULL` too.
3. **The global filter hides a NULL-tenant row from a tenanted caller.**
   `CleansiaDbContext.cs:243-258` builds
   `providerNull || (currentTenantId == null && e.TenantId == null) || e.TenantId == currentTenantId`.
   With `currentTenantId = "T1"` and `e.TenantId = NULL`, the middle clause is false and the last is
   SQL `NULL` — the row is **excluded**.

So for any tenanted cleaner, `ResolveCurrentVersionAsync` returns nothing. D4.3 then fires: *"If there
is no such version — the feature is OFF for that country. `required: false`, no checkbox is rendered,
the onboarding validator rule does not fire, and the invoice stamp is null."* Indistinguishable from
"the owner has not written the text yet." **The safety valve becomes the failure mode's disguise.**

**And this is the S8 the security law actually asks for.** S8 says: *"When adding an entity, ask 'could
two tenants both have rows here?' — if yes, `ITenantEntity`; if no (**true platform config**), document
why it isn't."* Every comparable table in this schema answered "no":

| Table | Base | Tenant-scoped? |
|---|---|---|
| `Country` (`Internationalization/Country.cs:7`) | `Auditable` | **No** — does not implement `ITenantEntity` |
| `Language` (`Internationalization/Language.cs:6`) | `BaseEntity` | **No** |
| `CountryInvoiceConfig` (`InvoiceTemplates/CountryInvoiceConfig.cs:8`) | `BaseEntity` | **No** |
| `CountryConfiguration` | `BaseEntity` | **No** |

`CountryInvoiceConfig` is the ADR's *own* cited precedent for per-country legal text with a
`LegalNoticeReviewStatus` gate (Context table, D4.2). It is tenantless. ADR-0041 copies the mechanism
and inverts the tenancy without noting it.

**The franchise argument does not survive contact either.** A franchise operator that needs its own
self-billing terms also needs its own invoice legal disclaimer, its own VAT config and its own
serviced countries — none of which are tenant-scoped today. Solving that speculatively on one table
buys nothing and costs a live silent failure the moment the first tenant exists.

**What I want changed.**

1. **`AgreementVersion` and `AgreementVersionText` become platform config: `BaseEntity`, no
   `ITenantEntity`**, with the S8-mandated one-line "why it isn't" comment naming `CountryInvoiceConfig`
   as the sibling. The unique indexes become `(Kind, CountryId, Version)` and
   `(AgreementVersionId, LanguageCode)` — no nullable column in the key, so the
   `.AreNullsDistinct(false)` question disappears entirely rather than being answered. D9's version
   repository collapses to one method (which also closes CH-S3's second half).
2. **`EmployeeAgreementAcceptance` stays `ITenantEntity`.** It is a per-employee fact; two tenants can
   both have rows; it must ride the filter. This is the correct half of D9 and I am not attacking it.
3. **If the panel insists on tenanting the versions**, then D4.2's resolution is *not* a plain scoped
   read: it must be the shipped S8 remedy — `IgnoreQueryFilters()` **plus an explicit
   `(e.TenantId == current || e.TenantId == null)` predicate** so a tenant sees its own override
   falling back to the platform text — and D4 must say so, plus a pinning test seeding a **non-null**
   tenant. As written today, the design is broken for tenants either way; option 3 is just the more
   expensive repair.

*Blocking?* **Yes.** It is a silent fail-open on the one control the ADR exists to add, it is
undetectable by any check in §How-a-reviewer-verifies, and it is invisible in single-tenant mode — so
it ships green and surfaces on the first franchise.

---

## CH-S5 — There are **two** writers of `EmployeeInvoice`, and D6.2 stamps one of them. The one it misses is the one that runs on a timer. **BLOCKING**

**The hole.** §Applies-to says *"one stamp at invoice issuance"*; ticket 2 says *"the D6.2 stamp in
`GenerateInvoice`'s handler"*; reviewer check #3 says *"`GenerateInvoice`'s validator gains no rule. It
gains a **stamp** in the handler."* Every reference is singular and names the MediatR handler.

**`rg -n "CreateFromOrderPays" src/` (excluding tests) returns two production call sites:**

```
src/Cleansia.Core.AppServices/Features/EmployeePayroll/GenerateInvoice.cs:87
src/Cleansia.Core.AppServices/Services/PayPeriodBackgroundService.cs:328
```

`PayPeriodBackgroundService.GenerateInvoiceForEmployeeAsync` (`:298-376`) builds the invoice itself —
`EmployeeInvoice.CreateFromOrderPays(…)` at `:328`, `_employeeInvoiceRepository.Add(invoice)` at `:334`,
its own duplicate guard at `:311-325`, its own `orderPay.AssignToInvoice(…)` loop at `:336-339` — and
**never goes through `GenerateInvoice.Handler` or its validator.** It is invoked from
`SendPeriodClosedEmailsAsync:237`, inside the monthly auto-close job.

**Why it matters.** The handler path is reached from `AdminPayrollController.GenerateInvoice:61` (an
admin clicking a button) and from the queue consumer (`GenerateInvoiceHandler.cs:63`), which itself is
only fed by `FiscalReconciliationService.ReconcileInvoicesAsync:152` — a **reconciliation** re-enqueue
whose own comment says the consumer was a Wave-0 stub. The path that issues invoices *in the normal
case* is the timer. Stamp only the handler and the routine monthly run produces `SelfBillingAcceptanceId
IS NULL` on every invoice it ever issues — permanently, and indistinguishably from the real exposure
D6.3 exists to measure (CH-S11). D6.2's claim that *"Which documents were issued without an agreement on
file?"* becomes a named detection query is then false in the most damaging possible way: the query
returns the automatically-issued population plus the genuinely-unauthorized one, mixed.

**What I want changed.**

1. D6.2 names **both** writers, and ticket 2's scope line names `PayPeriodBackgroundService.cs:328` and
   `GenerateInvoice.cs:87` explicitly.
2. Better: the stamp is resolved **inside `EmployeeInvoice.CreateFromOrderPays`** — a new required
   parameter — so the compiler enumerates the call sites instead of a reviewer doing it. That is the
   same argument ADR-0040 §3 category 3 used for its own migration, and it is the only version of this
   that cannot be half-applied. It costs touching two production call sites and four test call sites
   (`EmployeeInvoiceEntityTests.cs:58,76`, `PayoutInvoicePdfDataTests.cs:195,211`).
3. Reviewer check #10 is extended to assert the stamp through **the background-service path**, not only
   through the handler. As written it would pass while the production path is unstamped.

*Blocking?* **Yes.** D6.3's report is the sole compensating control for D5's non-blocking posture, and
this makes its input column meaningless on the majority path.

---

## CH-S6 — "The current version" is not uniquely determined by the schema. The unique index constrains the column nothing resolves on, and leaves the column everything resolves on unconstrained.

**The hole.** D1: `UNIQUE INDEX (TenantId, Kind, CountryId, Version)`. D4.2: *"the `AgreementVersion`
for `(kind, country)` with the greatest `EffectiveFrom <= now` that has at least one
`AgreementVersionText` at `BusinessSupplied` or above."* D1's own comment calls `Version` *"opaque,
ordered by `EffectiveFrom`"*.

So the constraint is on `Version`, the resolution is on `EffectiveFrom`, and **nothing constrains
`(Kind, CountryId, EffectiveFrom)`**. Two rows — `"2026-08"` and `"2026-08b"` — with the same
`EffectiveFrom` both satisfy "greatest `EffectiveFrom <= now`". The `AgreementVersionResolver` CRC card
says its responsibility is to *"name the one version"*: `.Single()` throws (a 500 on onboarding
submit), `.First()` without a total-order tiebreak returns whichever row the plan happens to emit
first, and the two rows may have different texts and different `BodyHash`es.

**Why it matters.** D4.6 says v1 has **no admin authoring UI** — *"Versions and texts are owner-run
SQL"*. Hand-written SQL producing two rows with the same effective date is not an exotic scenario; it
is the ordinary way a copy-paste insert goes wrong, and the unique index the ADR does specify will not
catch it because the `Version` strings differ. The failure is then either an intermittent 500 on the
gate, or a legal record naming a text the cleaner may not have been shown — the exact defect D3 exists
to prevent, arriving through the back door.

**What I want changed.** The unique index that matters is `(Kind, CountryId, EffectiveFrom)` — that is
what makes "the greatest `EffectiveFrom <= now`" a function. Keep `(Kind, CountryId, Version)` as well
if `Version` is meant to be a stable external handle (it is, D3.1 returns it on the wire), and say so.
Add a deterministic tiebreak in the resolver regardless (`ORDER BY EffectiveFrom DESC, Version DESC`) so
the query is total even against a database that predates the index.

*Blocking?* No — but it is a two-line fix that removes a whole class of owner-SQL accident, and the
index as specified creates a false sense that cardinality is handled.

---

## CH-S7 — The append-only log has no insertion ordinal, and its ordering column is **backdatable by an operator**. This schema already solved that problem once, three files away.

**The hole.** Every read in the ADR is *"the latest row for `(employee, kind)`"* (D1, D2.4, D5, D6.3),
ordered by `OccurredAt` — the index is `(TenantId, EmployeeId, Kind, OccurredAt DESC)`. D2.3 argues
correctly that `OccurredAt` must not be `Auditable.CreatedOn`, because *"a paper contract signed on 3
June and recorded on 4 August has two different true timestamps."* It then uses the **legal** timestamp
as the **supersession** key and provides no replacement for the ordering job `CreatedOn` was doing.

**Why it matters — the house already has this exact lesson, written down:**

```csharp
// src/Cleansia.Core.Domain/Orders/OrderStatusTrack.cs:14-18
// Strictly-increasing append index within the owning order. CreatedOn is millisecond-resolution and
// ties when two transitions land in the same tick; Sequence is the deterministic tiebreaker that makes
// "current status" correct by construction (the order is the consistency boundary — assigned in
// Order.AddOrderStatus, never set by the caller).
public int Sequence { get; private set; }
```

and `Order.AddOrderStatus` (`Order.cs:445-457`) resolves the denormalized current value with
`.OrderByDescending(s => s.CreatedOn).ThenByDescending(s => s.Sequence)` — two columns, because one is
not a total order. ADR-0041 uses one column, and it is worse than `CreatedOn` in two ways:

- **Ties.** D2.5's idempotency short-circuit is a read-then-write with no unique index (deliberately,
  D1). Two concurrent accepts both pass the read and write two rows; the ADR says that is fine because
  they are *"two identical true statements"*. True for `Accepted`+`Accepted`. Not true once `Revoked`
  exists (D2.4, shipped from day one on purpose): an `Accepted` retry racing a `Revoked` produces two
  rows whose relative order decides whether the cleaner is currently agreed, and `ORDER BY OccurredAt
  DESC LIMIT 1` over a tie returns an arbitrary one.
- **Backdating.** D6.4 has an operator supply `OccurredAt` = *"the contract's own signature date"*.
  So the column that decides which record supersedes which is a value a human types into an admin form.
  An operator recording a genuinely-old paper signature after a self-service revocation writes a row
  that does not supersede — correct — but a fat-fingered year does, silently, and there is no ordinal
  to appeal to.

**What I want changed.** Add an explicit append ordinal to `EmployeeAgreementAcceptance` and order by
`(OccurredAt DESC, Sequence DESC)` — or, cheaper and equally total, keep `Auditable.CreatedOn` as the
supersession key *in addition to* `OccurredAt` as the legal one, and say in D2.3 that the two answer
different questions (which is what D2.3 almost says already). Either way the index becomes
`(TenantId, EmployeeId, Kind, OccurredAt DESC, <tiebreak> DESC)`. D2.3 is right that they are two
facts; the schema currently stores one column for both jobs.

*Blocking?* No — but it is the difference between "append-only log" and "append-only log whose latest
row is well-defined", and D2.4 explicitly ships the enum value that makes it matter.

---

## CH-S8 — Referential integrity stops one hop short of the evidence, twice: the acceptance points at a *text* by `(FK, string)` rather than by its id, and its `Kind` is a denormalization with nothing tying it to the version's `Kind`.

**The hole (a) — the served text is not referenced.** The acceptance carries
`AgreementVersionId` + `LanguageCode` + `BodyHash`. The row that actually holds the served bytes is
`AgreementVersionText`, and **there is no FK to it.** Nothing at the database prevents deleting or
re-keying the exact `(version, language)` row an acceptance was computed from. D3.3's whole argument is
that the stored hash *"turns a silent edit into a one-query detection"* — but reviewer check #7
(*"recomputes `BodyHash` for every acceptance from its referenced `(version, language)` row and asserts
equality"*) is **vacuous when the row is gone**: `FirstOrDefault` returns null and a naively written
test either skips or NREs. A *deletion* is exactly as damaging as an edit and is the one this design
cannot see.

**The hole (b) — `Kind` is stored twice with nothing joining them.** `EmployeeAgreementAcceptance.Kind`
and `AgreementVersion.Kind` are independent columns. A row may say `Kind = SelfBilling` while
`AgreementVersionId` points at a `CodeOfConduct` version — D10 row 4 explicitly plans a second kind, so
this is not hypothetical. Every read in the ADR filters on the acceptance's own `Kind`, so a mismatched
row is a false positive on the gate: the cleaner "has a current self-billing acceptance" that is an
acceptance of something else.

**What I want changed.**

1. The acceptance references **`AgreementVersionTextId`** (FK, `Restrict`), and `AgreementVersionId` /
   `LanguageCode` become derivable from it — or, if the ADR wants them denormalized for query shape,
   they stay *and* the pair is closed by a composite FK. Referencing the text row also makes
   verification 7 mechanically total: the row is guaranteed to exist, so the recompute always has
   something to compare.
2. `Kind` is either dropped from the acceptance (join to the version — the index becomes
   `(TenantId, EmployeeId, AgreementVersionId, …)` and the "current for kind" read joins) **or** pinned
   by a composite FK `(AgreementVersionId, Kind) → AgreementVersion(Id, Kind)`, which needs a unique
   index on `AgreementVersion(Id, Kind)` — one line, and the standard relational way to make a
   denormalized discriminator honest.

*Blocking?* No, but (b) is one index + one FK and closes a class of defect that no test in
§How-a-reviewer-verifies would catch.

---

## CH-S9 — `LanguageCode varchar(10)` contradicts the shipped language-code shape, is **case-sensitive** where the platform's is not, and puts an unnormalized value into the `BodyHash` pre-image.

**The hole.** D1 declares `LanguageCode varchar(10)` on two tables, with no FK. The shipped shape is a
`citext` FK to an alternate key:

```csharp
// LanguageEntityConfiguration.cs:13-17
builder.Property(l => l.Code).HasColumnType("citext").IsRequired().HasMaxLength(5);
builder.HasAlternateKey(l => l.Code);

// UserEntityConfiguration.cs:78-84
builder.HasOne(u => u.PreferredLanguage).WithMany()
    .HasForeignKey(u => u.PreferredLanguageCode)
    .HasPrincipalKey(l => l.Code)
    .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
```

and the emitted DDL propagates the type: `Initial.cs:205` (`Code = … type: "citext", maxLength: 5`),
`:212` (`AK_Languages_Code`), `:727` (`PreferredLanguageCode = … type: "citext", maxLength: 5`),
`:742-746` (the FK).

**Why it matters.** Three concrete consequences, all on the legal record:

1. **Two "reviewed" bodies per language.** `AgreementVersionTexts`'s unique index includes
   `LanguageCode`. Under `varchar`, `'cs'` and `'CS'` are distinct — so an owner-run SQL insert with the
   wrong casing creates a second `BusinessSupplied` Czech body for the same version, and the unique
   index the ADR relies on to make texts unique per language does not fire. Under `citext` (with the
   FK) it is impossible.
2. **The wrong body is served and then attested.** D4.4 resolves *"the caller's language if that
   `(version, language)` row is `BusinessSupplied`+"*. A client sending `CS` or `cs-CZ` against a
   case-sensitive column misses, silently falls back to the authored language, and the acceptance
   records `LanguageCode` = whatever was actually used — so D2.2's *"the language of the body actually
   served"* is right by accident or wrong by construction, and the record cannot tell which.
3. **The hash pre-image is unnormalized.** D3.3 hashes
   `agreementVersionId + "\n" + languageCode + "\n" + body` and NFC-normalizes **the body only**. The
   same body served under `cs` and `CS` hashes to two different values, so verification 7's recompute
   must reproduce the exact casing that was used at the time — a detail nobody will remember in 2029,
   which is precisely the horizon this ADR is written for.

There *is* a `varchar(10)`-with-no-FK language column in the tree —
`CountryConfigurationEntityConfiguration.cs:29-31` (`DefaultLanguageCode`). It is the deviating form,
not the canonical one, and the ADR happens to have copied it.

**What I want changed.** `LanguageCode` on both tables is `citext`, `HasMaxLength(5)`, with
`.HasPrincipalKey(l => l.Code)` FK to `Languages` (`Restrict` — a language referenced by a legal text
may not be hard-deleted). D3.3's pre-image then normalizes the code to lower-case explicitly, and says
so, so the hash is reproducible from the stored row alone.

*Blocking?* No, but it is free now and a data migration later.

---

## CH-S10 — `AgreementVersionId` and `BodyHash` are `NOT NULL` because the self-service channel was the only one in view. Both are **false by construction** for `Channel = AdminRecordedContract` — the channel D6.4 adds to close F4's cohort. **BLOCKING**

**The hole.** D1 makes `AgreementVersionId` and `BodyHash` non-nullable on every acceptance row. D6.4
then adds `RecordPartnerAgreementCommand`, writing an acceptance with
`Channel = AdminRecordedContract`, `ContractReference`, and *"the contract's own signature date as
`OccurredAt`"* — evidence that **we hold a paper signature**, explicitly *"a distinct kind of
evidence"* from a tick.

For that row:

- **`AgreementVersionId` has nothing true to point at.** F4 is explicit that the pre-clause cohort
  *"signed a contract **without** the clause"* and the owner's mitigation is future tense. There is no
  `AgreementVersion` representing what they signed — `AgreementVersion` rows are, per D4, the
  *checkbox bodies* for a jurisdiction, authored to satisfy D4's six propositions. And by D4.3, if no
  reviewed version exists for the country the feature is off entirely — so for the cohort D6.4 exists
  to serve, there is often **no version row at all**, and the command physically cannot insert.
- **`BodyHash` is defined as a hash of "the `AgreementVersionText` row it served"** (D3.3) — and
  nothing was served. Whatever an implementer puts there (zeros, the hash of an unrelated body, the
  hash of the empty string) is a fabricated attestation on a record whose stated job is *"be an
  immutable statement that a named supplier agreed to a **named text**"*. D6.4's own principle —
  *"a record that blurs them is worth less than no record"* — is violated by the schema, not by the
  code.

The escape hatch is worse: authoring a synthetic `AgreementVersion` "representing the contract clause"
makes the `AgreementVersions` table hold two different kinds of object (texts we serve, and paper
instruments we do not), and then D4.2's resolver — which does not filter by channel — will happily
select the paper-contract pseudo-version as *the current version to display in the app*.

**What I want changed.** `AgreementVersionId` and `BodyHash` become **nullable**, with a CHECK-style
invariant stated in D1 and enforced in the factory:

```
Channel ∈ {PartnerWeb, PartnerMobile}  ⇒  AgreementVersionId NOT NULL ∧ BodyHash NOT NULL
                                          ∧ RecordedByUserId NULL ∧ ContractReference NULL
Channel = AdminRecordedContract        ⇒  RecordedByUserId NOT NULL ∧ ContractReference NOT NULL
                                          ∧ (AgreementVersionId may be NULL)
```

This is the same "the columns that are true depend on the discriminator" shape ADR-0034 already
accepted for `EmployeePayoutDetails` (`Scheme` + a nullable IBAN/domestic column set,
`Initial.cs:1626-1640`), so there is a house precedent for a nullable-by-variant record and it does not
need inventing. D6.3's report then gets its *"evidenced by contract only"* bucket for free — it is
`Channel = AdminRecordedContract AND AgreementVersionId IS NULL`, a real predicate rather than a
category the ADR names but the schema cannot express.

*Blocking?* **Yes.** D6.4 is the mechanism that makes F4's exposure closable, and as specified the
schema rejects the rows it needs to write, or accepts them only at the price of a fabricated hash on a
legal record.

---

## CH-S11 — D6.2's detection predicate is not discriminating. `SelfBillingAcceptanceId IS NULL` is the union of four causes, and under the ADR's own unanswered-question default it is **every invoice**.

**The hole.** D6.2 claims *"Which documents were issued without an agreement on file?" becomes
`SELECT … WHERE "SelfBillingAcceptanceId" IS NULL` — a **named detection query over persisted state**,
which is what `patterns-backend.md` §fail-soft condition (3) requires of any non-blocking design.*

**Why it matters.** NULL means at least four different things, and the ADR needs to tell them apart:

| Cause | Is it exposure? | Frequency |
|---|---|---|
| (a) Invoice issued before the feature shipped | No — legacy | Bounded, shrinking |
| (b) Country has no `BusinessSupplied` text ⇒ feature OFF (D4.3) | No — by design | **Unbounded in time.** `Q-SELFBILL-01`'s stated default is *"Version rows stay `NotReviewed` ⇒ no checkbox, no gate, nothing rendered"* — so until counsel delivers text, **100% of invoices are null-stamped** |
| (c) Cleaner genuinely never accepted | **Yes — this is the whole point** | The number D6.3 must report |
| (d) The stamp lookup returned null through a bug (CH-S3's cross-tenant resolve, CH-S4's invisible version, CH-S5's unstamped writer) | **Yes, and worse** | Silent |

`patterns-backend.md`'s fail-soft condition (3) is *"the failure is detectable **without the log** — a
named reconciliation predicate over persisted state"*, and its Corollary 3 (ADR-0038 AM-9) is an entire
paragraph about detection predicates keyed on the wrong column going useless: *"A detection query gated
on a source **FK** therefore goes silently blind… Gate on the applied **amount** instead of the id."*
The failure mode here is the mirror image — saturation rather than blindness — but the rule's substance
is the same: **a predicate that cannot separate the signal from the design is not a detection query.**
As specified, on the day it ships, D6.3's *"invoices issued with a null stamp, by month"* is a count of
the invoice table, and CH-A2's own worry ("if nobody watches the number, the report is theatre")
becomes structural rather than behavioural — nobody *can* watch a number that is 100%.

**What I want changed.** Stamp the **outcome**, not just the id. One additional non-nullable
`SelfBillingStampOutcome` (or equivalent) column on `EmployeeInvoice`, resolved and frozen at issuance
alongside the FK:

```
Authorized              → SelfBillingAcceptanceId NOT NULL
FeatureOffForCountry    → no BusinessSupplied text existed for the resolved jurisdiction (D4.3)
JurisdictionUnresolved  → neither WorkCountryId nor Address.CountryId (see CH-S13)
NoAcceptance            → the feature was ON and the cleaner had none  ← the exposure
PredatesFeature         → default for rows migrated in
```

Then D6.3's report is `WHERE Outcome = NoAcceptance`, a bounded number that starts near zero and is
watchable; `FeatureOffForCountry` is a separate, *also useful* number (it measures how long
`Q-SELFBILL-01` has been open); and a spike in `JurisdictionUnresolved` surfaces CH-S13 instead of
hiding inside the same NULL. One `integer` column, decided once at issuance, same freeze semantics.
Without it, D6.3 — *"not optional… it is what makes D5's non-blocking posture defensible"* — cannot be
built from the schema this ADR specifies.

*Blocking?* Borderline; I would accept it as a required amendment rather than a re-decision, but the
ADR's non-blocking posture rests entirely on this report and the report is not computable as designed.

---

## CH-S12 — The migration's **additivity** claim is sound. Its **urgency** claim is misattributed, its seed instruction would break a shipped test, and its actual failure mode is unnamed. **BLOCKING (the third part)**

**Sound first.** *"3 new tables + 1 nullable FK column + 3 enums. Nothing existing is altered, renamed
or dropped — S9-clean by construction."* I checked this against `security-rules.md` S9 line by line:
nullable columns are free, no rename, no drop, no non-nullable-without-default. Enums are `int`-stored
C# types with no DDL. **The additivity claim holds.** I could not break it.

**(1) The urgency argument is misattributed.** The ADR says later is expensive because *"`EmployeeInvoices`
will have rows — so the new FK column lands on a populated financial table whose existing rows can never
be truthfully back-stamped… Every invoice issued between the reseed and the migration is permanently
unattributable."*

That harm is **not caused by the migration's timing.** By D4.3 + `Q-SELFBILL-01`'s default, the feature
is inert until reviewed text exists; every invoice issued between the reseed and the *text* is
null-stamped whether the column exists or not. The unattributable window is bounded by counsel's
delivery, not by the migration. Landing the schema today shortens nothing.

What landing later actually costs is: one stacked migration file containing three `CREATE TABLE`s, one
`ADD COLUMN … NULL` (metadata-only on PG11+), and one `ADD CONSTRAINT … FOREIGN KEY` that validates a
column which is entirely NULL. That is close to the cheapest migration this repository can produce. So
"cheap only right now" reads as schedule pressure on a `proposed` ADR whose panel has not run, and the
lead should discount it accordingly.

**(2) The seed instruction, as written, breaks a shipped test.** The migration section says
*"`Cleansia.Infra.Scripts/SeedData` must NOT seed acceptances… Seeding one `AgreementVersion` row per
country at `NotReviewed` with placeholder text is correct and inert."* Two problems:

- The executed seed is **one file**, and it is pinned byte-for-byte against the repo-root copy:
  `Cleansia.Tests/Configuration/StartupSeedScriptSyncTests.cs:16-33` asserts
  `src/Cleansia.Infra.Scripts/SeedData/insert_seed_data.sql` is byte-identical to
  `sql-scripts/insert_seed_data.sql`. Editing only the path the ADR names turns that test red.
- Both copies are owner-territory (my own charter: *never edit `sql-scripts/insert_seed_data.sql`
  without owner approval — seeds carry tenant/user ids matched to dev tooling*). The ADR should say
  `manual_step`, not "seed it".

**(3) F3's "verified" launch population is falsified, and it changes what D5/D6 are for.** F3 asserts:
*"The reseed **does** create cleaners (five, `insert_users_employees.sql:53-111`)."* The rows are there
— but **that file is not on the reseed path**:

- `src/Cleansia.Infra.Scripts/Cleansia.Infra.Scripts.csproj:10-16` copies exactly one file to output:
  `SeedData\insert_seed_data.sql`. The other 18 scripts in that folder are not `Content` at all.
- `CleansiaStartupBase.SeedDevelopmentData` (`:268`) builds one path —
  `…/Cleansia.Infra.Scripts/SeedData/insert_seed_data.sql` — reads it and executes it. Nothing else.
- `rg 'INSERT INTO public\."[A-Za-z]+"' sql-scripts/insert_seed_data.sql` yields **only catalog/config
  tables**: `Countries`, `Languages`, `Currencies`, `Services`, `ServiceCategories`, `Packages`,
  `PackageServices`, `Extras`, `ServiceCities`, `CompanyInfo`, `CountryInvoiceConfigs`,
  `CountryConfigurations`, `MembershipPlans`, `PromoCodes`, `LoyaltyTierConfigs`, `FeatureFlags`,
  `EmailTranslations`, `EmailTemplateTranslations`. **No `Users`, no `Employees`.**
- Corroborating that `insert_users_employees.sql` is orphaned dev tooling: its companion
  `fix_employee_addresses.sql` links addresses for *Tomáš Dvořák*, *Petra Svobodan* and *Jan Procházka*
  — three cleaners who **do not exist** in `insert_users_employees.sql`'s five (Novotná, Krejčí,
  Horáková, Veselý, Marková). The pair has drifted out of sync because nothing runs it.

So the reseed produces **zero** cleaners. F3's conclusion (*"A design that assumes an empty population
is wrong on day one"*) is still right — real cleaners register on live DEV between now and ship, and
the pre-clause cohort of F4 is real — but its *evidence* is wrong, and the difference matters: the
cohort is "whoever registers between reseed and feature activation", which is a **growing** number the
owner controls by shipping, not a fixed five. D6.3's report should be framed against that.

*(Also note, if the panel keeps the seed idea: `insert_users_employees.sql:53-111` inserts Employees
**without** `AddressId` and **without** `WorkCountryId` — see CH-S13.)*

**(4) The unnamed failure mode — folding into `Initial` is a silent no-op on any database that already
ran it.** `src/Cleansia.MigrationService/Program.cs:31-36`:

```csharp
var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count == 0) { Console.WriteLine("MigrationService: database is up to date — nothing to apply."); return 0; }
```

`20260723182623_Initial` is already in `__EFMigrationsHistory` on Azure DEV (live). Regenerating
`Initial.cs` in place adds three tables to the *file*; the migrator sees zero pending, prints a
reassuring line, exits 0, and Aspire's `WaitForCompletion` is satisfied. This is not speculation — it
is the same mechanism ADR-0040's challenger documented in
`agents/archive/2026-08/adr-deliberation/challenges/0040-write-guarantee-and-plan.md` §CH-W3, for the same `Initial`, three
weeks ago.

The difference from ADR-0040 is the **failure direction**. There, the drift left a column nullable —
silent. Here the drift leaves three tables **absent**, and D5's gate is a `MustAsync` on the onboarding
submit path: the first partner onboarding submit after deploy hits `42P01 relation
"EmployeeAgreementAcceptances" does not exist` and 500s. D5's stated reason for choosing the write path
is that *"its failure mode is a rejected submit with a translated message — never a 403 on an unrelated
GET."* A missing table is neither; it is an untranslated 500 on the one path a new cleaner must pass.

**What I want changed.**

1. The "cheap only right now" framing is replaced with an honest cost line: *cheap now, cheap later; the
   thing that is not cheap is issuing invoices before the text exists, and that is `Q-SELFBILL-01`, not
   the migration.* The panel should not be under schedule pressure.
2. The seed instruction becomes a `manual_step` naming **both** file paths and the byte-identity pin
   (`StartupSeedScriptSyncTests`), or it is dropped — an inert `NotReviewed` row is a nicety, not a
   requirement, and D4.3 already handles its absence.
3. A **verifiable pre-deploy gate** is recorded in the ADR, discharged by evidence, exactly as ADR-0040's
   challenge asked for its own: `SELECT to_regclass('public."EmployeeAgreementAcceptances"');` returns
   non-null on every environment the code will run against, *before* ticket 2 deploys. Without it the
   ADR ships a gate that exists in C# and in a migration file and nowhere in the running database.
4. F3 is corrected as above.

*Blocking?* **Yes for (4)**, which is a deploy-day 500 on partner onboarding; the rest are amendments.

---

## CH-S13 — D4.1's jurisdiction term reads a **navigation**, on a codebase with no lazy loading and hand-written include lists — the precise hazard ADR-0034 D1.1 was written to remove, and ADR-0041 cites that ruling approvingly while re-creating it.

**The hole.** D4.1: *"Which jurisdiction — `Employee.WorkCountryId ?? Employee.Address.CountryId`,
reusing ADR-0034 D2's rule verbatim."* The first term is a scalar on the row (`Employee.cs:114`). The
second is a **reference navigation** (`Employee.cs:120-121`).

**Why it matters.** ADR-0034 D1.1 moved the payout term onto a scalar *because* the includes are
hand-written and a missing one reads as "absent" rather than "not loaded" — and ADR-0041 quotes that
reasoning twice (Context row for the completeness gate; D7's id-keyed-write rule). The include lists are
still hand-written and they still disagree:

| Loader | Includes `Address`? |
|---|---|
| `EmployeeRepository.GetByUserEmailAsync` (`:9-17`) | Yes (`.Include(e => e.Address)`, no `.ThenInclude(Country)` — but `Address.CountryId` is a scalar on `Address`, so it suffices) |
| `EmployeeRepository.GetByIdAsync` (`:43-50`) | Yes, with `.ThenInclude(a => a.Country)` |
| **`EmployeeRepository.GetByIdIgnoringTenantAsync` (`:52-57`)** | **No — no includes at all** |
| `PayPeriodBackgroundService.cs:203-209` | Yes, with `.ThenInclude(a => a!.Country)` |

`GetByIdIgnoringTenantAsync` is the method D9 itself points at as the reference pair, and it is the one
`GenerateInvoiceHandler.cs:48` uses. Any resolver handed *that* employee sees `Address == null`, falls
through to a null jurisdiction, and D4.3 turns that into `required: false` / null stamp — the same
silent fail-open as CH-S4, arriving through the include list.

Compounding it: cleaners created by the (unexecuted, but owner-runnable) `insert_users_employees.sql`
have **neither** term — the INSERT column list at `:53-60` contains no `AddressId` and no
`WorkCountryId` — and `WorkCountryId` is only written by `Employee.cs:256`, which ADR-0034 D2 says
happens at admin approval, i.e. *after* onboarding submit, which is where D5 puts the gate.

**What I want changed.** Either (a) the resolver takes the two scalars as parameters and the *caller*
is responsible for having loaded them — with the include obligation named at the two call sites, as
ADR-0034's challenger asked — or, better, (b) D4.1's fallback is satisfied from a scalar
`Employee.AddressCountryId`-style denormalization maintained on the same write path, so the term cannot
depend on query shape at all. (b) is the version ADR-0034 actually adopted (`HasPayoutDetails`,
`Initial.cs:848`) and the reason it adopted it applies here unchanged.

*Blocking?* No — but reviewer check #9 (*"a test with a version whose texts are all `NotReviewed` asserts
`required == false`"*) will pass with the navigation unloaded, so nothing in the ADR would catch it.

---

## CH-S14 — Index and configuration gaps (grouped; individually small, collectively the difference between a design and an entity configuration)

1. **No index on `EmployeeInvoices.SelfBillingAcceptanceId`.** PostgreSQL does not auto-index the
   referencing side of an FK. D6.2 promises a two-hop join *"forever"* (which acceptance authorized this
   document, and the reverse) and D6.3 wants *"invoices issued with a null stamp, by month"*. The
   useful shapes are a plain index on the FK for the forward join and a **partial** index
   `WHERE "SelfBillingAcceptanceId" IS NULL` for the report — the repo already uses partial indexes
   (`EmployeeInvoiceEntityConfiguration.cs:116-118`, `EmployeePayConfigEntityConfiguration.cs:82-84`).
   With CH-S11's outcome column the partial index keys on that instead.

2. **No index serving D4.2's resolution.** The resolver's query is
   `WHERE Kind = ? AND CountryId = ? AND EffectiveFrom <= now ORDER BY EffectiveFrom DESC`, semi-joined
   to texts on `ReviewStatus >= BusinessSupplied`. The specified unique index gives the equality prefix
   but not the ordering column; the text side has no `ReviewStatus` term in any index. Volumes are tiny
   so this is not a performance finding — it is a *completeness* one: the ADR specifies indexes for the
   uniqueness it wants and none for the queries it defines, which is how an index set drifts.

3. **`.AreNullsDistinct(false)` is applied without the argument the catalog requires.** D9 presents it
   as an S8 formality (*"unique indexes carry `TenantId` with `.AreNullsDistinct(false)`"*).
   `consistency.md` §"Tenant-scoped unique indexes" is explicit that it is *"decided by the index's JOB,
   not by a majority"*: mandatory for a **sole arbiter of a concurrent claim**, and plain nulls-distinct
   is fine for a **backstop behind an authoritative app-level assert**. Neither new index is a sole
   arbiter — D4.6 says rows arrive by owner SQL and there is no authoring UI in v1. The value is
   probably still right, but the ADR gives the wrong reason, and the catalog's own deviating form is
   *"a comment declining/asserting the option on consistency grounds"*. (Under CH-S4 the question
   evaporates: no nullable column in either key.) `Cleansia.Tests/Infrastructure/NullsNotDistinctIndexModelTests.cs`
   is the shipped enforcement and its theory list is hand-maintained — if the panel wants these indexes
   pinned, they must be **added to `:52-57`**, which the ADR does not mention.

4. **`LiveActivityTokenConfiguration` is mis-cited as a tenant-scoped precedent.** D9 names it alongside
   `FiscalCounterEntityConfiguration` as *"the shipped construct ADR-0034 D1.3 established"* for
   `(TenantId, …)` unique indexes. Its index is `(UserId, DeviceId, OrderId)`
   (`LiveActivityTokenConfiguration.cs:26-28`) — **no `TenantId`**; the null it addresses is `OrderId`.
   `FiscalCounterEntityConfiguration.cs:26-30` and `EmployeePayoutDetailsEntityConfiguration.cs:75-82`
   are the correct citations.

5. **No `HasDatabaseName` on any of the specified indexes.** The house names the ones it cares about
   (`IX_FiscalCounters_Tenant_Year_IssuerScope`, `IX_EmployeePayoutDetails_Tenant_Employee`,
   the four `IX_AdminActionAudits_*`). Cosmetic, but the ADR is otherwise specifying at DDL granularity.

*Blocking?* No.

---

## Found sound — what I attacked and could not break

Stating these so the lead knows the coverage was real and not a search for confirmations.

1. **D1's "no unique index on the acceptance, deliberately".** I tried to construct a case where two
   concurrent `Accepted` rows for the same `(employee, kind, version)` cause harm and could not — every
   read is "the latest row", both rows say the same thing, and the read-then-write short-circuit (D2.5)
   is genuinely a no-op-on-duplicate rather than a check-then-act with consequences. The argument that
   *"the shape removes the race rather than guarding it"* is correct, and it is a better answer than a
   nulls-not-distinct index would have been. **It becomes unsound only when `Revoked` is written** —
   which is CH-S7, an ordering finding, not a uniqueness one.

2. **D2.3 — `OccurredAt` must not be `Auditable.CreatedOn`.** Verified against the mechanism:
   `Auditable.CreatedOn` defaults at construction (`Auditable.cs:11`) and `CleansiaDbContext.CommitAsync:82-86`
   re-stamps it via `Created(...)` when `CreatedBy` is empty. A backdated legal date would be clobbered
   or would fight the auditor. D2.3 is right, and right for the stated reason.

3. **The `LegalNoticeReviewStatus` reuse (the author's own CH-A6).** I checked whether
   `BusinessSupplied` means the same thing in both places. `CountryInvoiceConfig.cs:41-48`'s own
   doc-comment is *"What stands behind `LegalDisclaimerTemplate`… one reviewed for that jurisdiction,
   one a copy of the generic fallback nobody looked at — so the assurance is a column and not an
   inference from the text."* That is a claim about **provenance of the text**, not about the medium it
   is displayed in. Reusing it for an interactive acceptance is sound. **CH-A6 is answered; the panel
   can close it.**

4. **D8 — `ContractStatus` is a false friend.** Verified: `Auditable.Deactivated()` (`:35-42`) sets
   `DeactivatedBy/On` + `IsActive = false` and does not touch `ContractStatus`; `Employee` has no
   `Deactivated` override (`rg` over `Employee.cs` finds `ContractStatus` written only at `:83, :175,
   :262, :268, :282`, none of them from a deactivation path); `GdprDeletionService.cs:244` calls it. An
   erased cleaner does read `Approved`. D8's refusal to piggyback is correct and its routing of the fix
   to a follow-up ticket is right.

5. **F1 and F2.** `UserConsentEntityConfiguration.cs:31-32` is `HasIndex(UserId, ConsentType).IsUnique()`
   — one mutable row per type, and `UserConsent.Regrant` (`UserConsent.cs:47-55`) overwrites
   `GrantedAt`/`IpAddress`/`UserAgent`. `WithdrawConsent.Command(ConsentType)` (`:11`) has an
   `IsInEnum()` validator and nothing else. **A1 is correctly rejected** and the reasons given are the
   real ones.

6. **The additivity half of the migration (S9).** See CH-S12. Genuinely S9-clean.

7. **`EmployeeAgreementAcceptance` being `ITenantEntity`.** I tried to argue it should be tenantless for
   symmetry with the config tables and could not — it is a per-employee fact, two tenants can both have
   rows, and it must ride the filter. The correct half of D9.

---

## Ordering for the lead

- **Pre-ratification (design changes, not amendments):** CH-S1 (what enforces append-only), CH-S4
  (tenancy of the config tables), CH-S10 (nullability by channel). Each changes the schema block.
- **Pre-merge of ticket 1:** CH-S2 (the five `OnDelete`s), CH-S3 (drop the ignoring variants, rewrite
  F5), CH-S6, CH-S7, CH-S8, CH-S9, CH-S14.
- **Pre-merge of ticket 2:** CH-S5 (both invoice writers), CH-S11 (the outcome column — but it is a
  *schema* change, so it must be decided with ticket 1 even though its consumer is ticket 2), CH-S13.
- **Pre-deploy gate, recorded in the ADR:** CH-S12(4) — `to_regclass` non-null on every environment.

Two of these interact and should be ruled on together: **CH-S4 and CH-S3.** If the config tables become
tenantless, `ResolveCurrentVersionIgnoringTenantAsync` has nothing left to ignore and D9 shrinks to one
correct sentence about the acceptance. Ruling on them separately risks keeping the method pair for a
table that no longer has a tenant.

I did not write any repair, did not touch `src/`, and did not amend the ADR.
