---
id: T-0531
title: Known constraint — a unique index containing nullable TenantId enforces nothing in single-tenant mode
status: ready
size: XS
owner: architect
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0028, 0035]
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 15
source: challenger round on ADR-0034/0035/0036 — `adr/challenges/0035-C-concurrency.md` CH-C1 and
  `adr/challenges/0034-db.md` CH-D2. **This ticket records a constraint; it does not change behaviour.**
  Two of the challengers' supporting claims are corrected below from PM-verified greps.
---

## Context

**This is a note ticket. The system is not broken and nothing here needs fixing today.** It exists so that
a constraint which two independent design panels just tripped over is written down once, in a place the
next designer reads, instead of being re-derived a third time.

**The constraint.** `ITenantEntity.TenantId` is `string?` and PostgreSQL treats NULLs as **DISTINCT** in a
unique index by default. Single-tenant mode *is* `TenantId == null` (`CLAUDE.md`; the query filter is built
around it at `CleansiaDbContext.cs:239-246`). Therefore **a unique index whose key includes `TenantId`
constrains nothing at all while the platform is single-tenant** — two otherwise-identical rows both insert,
and `ON CONFLICT DO NOTHING` never fires.

The codebase already knows this and documents the trade-off deliberately —
`src/Cleansia.Infra.Database/EntityConfigurations/UserMembershipEntityConfiguration.cs:100-109` names the
compensating guards in so many words: *"there the app-level `GetActiveForUserAsync` assert + the
`StripeSubscriptionId` unique index are the guards, and the index hardens multi-tenant mode."*
**That is a sound engineering decision and this ticket does not reopen it.**

**PM-verified inventory (2026-08-02), because two challengers reported it two different ways and both were
partly wrong.** Nine unique indexes in `src/Cleansia.Infra.Database/EntityConfigurations/` include
`TenantId`:

| # | Configuration | Key | NULLs |
|---|---|---|---|
| 1 | `PromoCodeEntityConfiguration.cs:63` | `(TenantId, Code)` | distinct → unenforced |
| 2 | `LoyaltyTransactionEntityConfiguration.cs:91` | `(TenantId, IdempotencyKey)` filtered | distinct → unenforced |
| 3 | `UserMembershipEntityConfiguration.cs:112` | `(TenantId, UserId)` filtered `Status = 1` | distinct → unenforced |
| 4 | `PromoCodeRedemptionEntityConfiguration.cs:66` | `(TenantId, PromoCodeId, UserId, SlotOrdinal)` | distinct → unenforced |
| 5 | `LoyaltyTierConfigEntityConfiguration.cs:33` | `(TenantId, Tier)` | distinct → unenforced |
| 6 | `ReferralCodeEntityConfiguration.cs:38` | `(TenantId, Code)` | distinct → unenforced |
| 7 | `UserEntityConfiguration.cs:106` | `(TenantId, Email)` | distinct → unenforced |
| 8 | `TenantConfigurationEntityConfiguration.cs:27` | `(TenantId, Key)` | distinct → unenforced |
| 9 | **`FiscalCounterEntityConfiguration.cs:26-29`** | `(TenantId, Year, IssuerScope)` | **`.AreNullsDistinct(false)` — ENFORCED** |

### Two corrections that matter to work in flight

1. **`adr/challenges/0034-db.md` CH-D2 states: *"I checked all ~40 `.IsUnique()` sites under
   `src/Cleansia.Infra.Database/EntityConfigurations/`: **not one** includes `TenantId`… `(TenantId,
   EmployeeId)` would be the first."*** **Refuted — nine do**, listed above. The proposed
   `(TenantId, EmployeeId)` index would be the **tenth**, not the first. The finding's *conclusion*
   (a nullable-`TenantId` unique index does not enforce cardinality today) is correct and unaffected; only
   its "no precedent exists" premise is wrong, and that premise is what its recommendation leans on.
2. **`UserMembershipEntityConfiguration.cs:106-109` says adopting `NULLS NOT DISTINCT` here would
   *"introduce a one-off"*. It would not — the repo already ships it twice**, in the committed Initial
   migration, on real PostgreSQL:
   - `FiscalCounterEntityConfiguration.cs:28` → `Migrations/20260723182623_Initial.cs:2649-2653`
     (`.Annotation("Npgsql:NullsDistinct", false)`)
   - `LiveActivityTokenConfiguration.cs:28` → `Migrations/20260723182623_Initial.cs:2680-2685`
     (`(UserId, DeviceId, OrderId)`)

   So the mechanism is **available, precedented and proven against this schema**. Whether to adopt it on any
   given index remains a per-index judgement — but "it would be novel" is no longer an argument for either
   side, and any panel weighing it should know that.

### The rule worth writing down

**A design must not treat a `TenantId`-bearing unique index as its sole arbiter of an invariant.** Where
such an index is the only guard, the invariant is unguarded in the mode the platform actually runs in. This
is exactly how the constraint surfaced: ADR-0035's D3 proposed
`UNIQUE (TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal)` as the **only** arbiter of a benefit quota
after explicitly removing the app-level pre-check — *"there is no `SELECT`-then-`INSERT` anywhere in the
consuming path"*. `UserMembership`'s version of the same index is defensible **because it is a backstop
behind an app-level assert**; a version with nothing behind it is not the same object.

Three legitimate shapes, for the record: (a) index + an app-level guard, and say which is the arbiter;
(b) `.AreNullsDistinct(false)`, precedented twice; (c) drop `TenantId` from the key where a globally-unique
FK already pins the tenant transitively.

## ⚠️ RESCOPED 2026-08-04 (architect) — four of the five AC are already satisfied elsewhere

**Verified at HEAD, against the tree rather than against this ticket's text.** Most of what this ticket
was written to record **has since been written down**, in a better place than the one it named — the loop
worked, and the ticket had not noticed. What is left is **one** item, and it is the one nobody wrote.

| Original AC | State at HEAD | Evidence |
|---|---|---|
| **AC1** — record the constraint + the three shapes | ✅ **DONE, elsewhere.** `agents/knowledge/consistency.md` §*"Tenant-scoped unique indexes: `NULLS NOT DISTINCT` is decided by the index's JOB, not by a majority"* carries the constraint, the sole-arbiter vs backstop split, the live instances on both sides, and *"the reviewer checks the emitted DDL, not the C# builder call"*. `patterns-backend.md` points at it from the metered-benefit archetype. It is **catalog law**, not a design note — a stronger home than the one AC1 named. | `consistency.md` §"Judgment calls"; `patterns-backend.md` (metered-benefit §, "Sole arbiter of a concurrent claim ⇒ `NULLS NOT DISTINCT`") |
| **AC2** correction #2 — *"`NULLS NOT DISTINCT` is not a novel construct"* | ✅ **DONE, twice over.** Stated as catalog law (*"`AreNullsDistinct(false)` has shipped in the committed `Initial` migration since day one, so 'we don't do that here' is a false invariant"*) **and** as a named deviating form. | `consistency.md`, same § |
| **AC2** correction #1 — *"nine indexes carry `TenantId`, not zero"* | ❌ **WITHDRAWN, deliberately.** It corrects `adr/challenges/0034-db.md`, a **challenge document of an ADR that is now `accepted`**. Per `adr/README.md` + the ADR-0031 erratum precedent, a `## Challenge` records *what was in front of the panel*; editing it after the verdict falsifies the record. The finding's conclusion was right and survived into the verdict, which is where it belongs. **Do not "fix" the challenge doc.** | `adr/README.md` §"erratum exception"; ADR-0031 §A *"leave citations that pin what was ruled on"* |
| **AC3** — correct the misleading in-code comment | ✅ **DONE.** `UserMembershipEntityConfiguration.cs:111-119` now reads *"…An index that is the SOLE ARBITER of a concurrent claim (FiscalCounters, MembershipBenefitUsages, PromoCodeRedemptions) must be NULLS NOT DISTINCT instead, because no read can arbitrate a race. `AreNullsDistinct(false)` is a shipped construct on this database, **not a one-off to be avoided**."* `LoyaltyTransactionEntityConfiguration.cs:88` carries the same correction. | both files, at HEAD |
| **AC4** — the **activation** consequence | 🔴 **STILL UNWRITTEN — the whole of what is left.** See the rewritten AC below. | `multi-tenancy-and-region.md` (89 lines) has **no** occurrence of "unique index", "NULLS", "DISTINCT" or "AreNullsDistinct" |
| **AC5** — nothing is fixed | ✅ moot — AC3 was the only code touch and it has landed. | — |

**And the frozen inventory AC1 asked for would now be WRONG.** The nine-row table in §Context above was
true on 2026-08-02 and is stale on 2026-08-04: the regenerated `Initial` (`7e1cf7f5`) moved **five**
indexes onto `.AreNullsDistinct(false)` — `FiscalCounters`, `LiveActivityTokens`,
`MembershipBenefitUsages`, `PromoCodeRedemptions`, `EmployeePayoutDetails` (verified by grep at HEAD).
Copying a counted table into a living doc would bake in a number that decays every sprint. **State the
rule and how to re-derive the list, never the list** — the same lesson ADR-0031 §D1 learned about
coverage claims ("state coverage structurally, never empirically").

## Acceptance criteria (rewritten — one item)

- [ ] **AC1′ — the ACTIVATION consequence is recorded, in the living doc that owns the tenancy axis.**
      Given `agents/architecture/decisions/multi-tenancy-and-region.md` — whose §"Axis (a)" already
      establishes that single-tenant mode **is** `TenantId == null` — When the architect updates it, Then
      it carries a short section stating that **turning multi-tenancy on changes which unique indexes
      enforce anything**, specifically:
      1. Under a **non-null** tenant, every `TenantId`-bearing unique index starts firing, including the
         ones that are inert today. An invariant guaranteed today by app-level code becomes guaranteed by
         the database — and a race that today produces two rows starts producing a `DbUpdateException` on
         a path that may not expect one.
      2. **The reverse is the dangerous direction:** anywhere an app-level guard was *removed* on the
         strength of an index, activation does not help — that invariant is unguarded **now**, in the mode
         the platform actually runs in. ADR-0035 D3 proposed exactly this shape (an index as the sole
         arbiter after explicitly removing the app-level pre-check) and it is why the rule exists.
      3. **A pointer, not a copy**, to `agents/knowledge/consistency.md` §"Tenant-scoped unique indexes"
         for the rule itself — the catalog is the law; this doc states the *activation* consequence only.
      4. **How to re-derive the current list** (`grep -rn "AreNullsDistinct" src/Cleansia.Infra.Database/EntityConfigurations/`
         and cross-check `.IsUnique()` sites whose key includes `TenantId`) — **and no frozen table**, for
         the reason stated above.
      **Evidence:** the doc diff. **Size: XS.**
- [ ] **AC2′ — nothing else changes.** No entity configuration, no migration, no behaviour, no catalog
      edit (the catalog already says it), no touch to any `adr/challenges/*.md`. If the architect concludes
      an index genuinely needs `AreNullsDistinct(false)` today, that is a **separate ticket with an
      `ef-migration` manual step**.

## Out of scope

- Changing any index. See AC2′. Any index change needs an owner-run EF migration and does not belong in a
  note ticket.
- **Editing `adr/challenges/0034-db.md` to correct CH-D2's "not one includes `TenantId`" claim.**
  Withdrawn 2026-08-04 — see the rescope table. A challenge document records what the panel saw.
- **Copying the nine-index inventory anywhere.** It is already stale. Record the re-derivation, not the
  list.
- **ADR-0028's activation pack.** It is `**DECLINED (owner, 2026-07-19)**` and is an `accepted`-status
  immutable artifact besides — it cannot gain a checklist item. The original AC4 pointed at it; AC1′
  points at the living doc instead, which is the correct home and is not blocked by the decline.
- Adjudicating ADR-0035's D3 (whether *its* index needs `NULLS NOT DISTINCT`, a `COALESCE` key, or an
  advisory lock). That is the live panel's call; this ticket supplies the ground truth it needs and nothing
  more.
- ADR-0034's `(TenantId, EmployeeId)` vs `UNIQUE (EmployeeId)` question for `EmployeePayoutDetails` — same:
  the panel decides, this ticket corrects the premise.
- The absence of a global `IsActive` query filter (a related but distinct "the database is not enforcing
  what you think" finding, raised in the same round). Separate; file if wanted.

## Implementation notes

**Nobody may edit `agents/backlog/adr/**` from this ticket.** Three architects are revising ADR-0034/0035/
0036 concurrently. The two corrections above are delivered to those panels by the **owner**, via the sprint
status doc — not by an agent writing into a live ADR.

`.AreNullsDistinct(bool)` is `NpgsqlIndexBuilderExtensions` on the installed
`Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0`, and PostgreSQL 16 (per `CLAUDE.md`) supports
`NULLS NOT DISTINCT`. Both facts are already demonstrated by the two shipped indexes.

**Archetype:** `agents/architecture/decisions/` living-doc update (the architect's own artifact), not an
ADR. `agents/knowledge/security-rules.md` S8 is the rule this constraint qualifies.

## Status log
- 2026-08-02 — draft (created by pm from the challenger round, as a **known-constraint note**, per the
  owner's instruction that this be recorded rather than fixed). The two corrections were found by the PM
  while verifying the challengers' counts and are the reason this is worth a ticket rather than a comment.
- 2026-08-04 — **draft → ready** (PM sprint-15 reconciliation). It passes DoR on merit and has no
  dependency; it was never dispatched. **Verified at HEAD that the deliverable does NOT exist:**
  `agents/architecture/decisions/multi-tenancy-and-region.md` is 89 lines and contains no occurrence of
  "unique index", "NULLS", "DISTINCT" or "T-0531". The rule this ticket records is still unwritten.
- 2026-08-04 — **the constraint became MORE load-bearing this sprint, not less.** The regenerated `Initial`
  (`7e1cf7f5`) now ships `.AreNullsDistinct(false)` on four sole-arbiter indexes, and `44d1b64d`/`f7828fb8`
  both reason from the nullable-`TenantId` fact. Two entity-config comments still call `NULLS NOT DISTINCT`
  "a one-off" when it already ships **five** times in the committed migration — correcting those comments
  is in this ticket's lane. **AC5 stands: nothing is fixed, no migration, no index change.**
- 2026-08-04 — **ARCHITECT DISPOSITION: RESCOPED, `ready`, size `S` → `XS`. It is still needed, but for
  one AC out of five.** The previous status-log line (immediately above) is now itself partly stale and
  is the reason this pass was worth running: it claims *"two entity-config comments still call `NULLS NOT
  DISTINCT` a one-off"*. **They no longer do** — `UserMembershipEntityConfiguration.cs:111-119` and
  `LoyaltyTransactionEntityConfiguration.cs:88` both now say the opposite, in so many words. The AC3
  work landed without this ticket, which is the normal and good outcome of a catalog rule doing its job;
  the ticket had simply not been re-verified.
  - **Already written down (so: withdrawn from this ticket)** — the constraint, the sole-arbiter-vs-
    backstop shapes, the live instances on both sides, the "check the emitted DDL" reviewer rule and the
    "false invariant" deviating form are all **catalog law** in `agents/knowledge/consistency.md`
    §"Tenant-scoped unique indexes", cited from `patterns-backend.md`. That is a *stronger* home than
    the living doc AC1 asked for.
  - **Withdrawn on principle** — the correction to `adr/challenges/0034-db.md`. ADR-0034 is `accepted`;
    its challenge doc records what the panel saw.
  - **Withdrawn as decayed** — the nine-row inventory. Five indexes moved since it was counted.
  - **What survives** — AC1′: the **activation** consequence, which is written **nowhere**
    (`multi-tenancy-and-region.md` has zero occurrences of "unique index"/"NULLS"/"DISTINCT"), and which
    is the one part of the original finding that is genuinely about multi-tenancy rather than about
    indexes. Note its original home, ADR-0028, is **owner-DECLINED and immutable**, so AC1′ retargets it
    at the living doc.
  - **Not a duplicate of T-0530** (the "two false mirrors" comment ticket) — that one is about comments
    asserting a mirror that does not hold; this is about a doc that has never stated a consequence.

- 2026-08-05 — **PM reconciliation pass 4: the architect's disposition is ACCEPTED and the INDEX row now
  matches it.** `status: ready`, `size: XS`, `owner: architect`, `depends_on: []`. **Scope is AC1′ only** —
  write the *activation* consequence into `agents/architecture/decisions/multi-tenancy-and-region.md`.
  Re-verified at HEAD that the deliverable is still absent: that file contains no occurrence of
  "unique index", "NULLS" or "DISTINCT". AC2/AC3/AC4 are withdrawn (landed elsewhere, or withdrawn on
  principle/decay — see the disposition above). **AC5 stands: nothing is fixed, no migration, no index
  change.** No re-derivation needed; this line exists so the fifth reconciliation does not re-run the fourth.

- 2026-08-05 — **ARCHITECT: DONE, and AC1′ landed WIDER than specified — by one half that the rescope
  did not have and that is the sharper defect.** Re-verified at HEAD first (Gate 0):
  `multi-tenancy-and-region.md` was 89 lines with zero occurrences of "unique index" / "NULLS" /
  "DISTINCT", so the deliverable genuinely did not exist.
  - **AC1′ — satisfied, all four legs.** New section *"What ACTIVATION changes"*, placed between Axis (a)
    and Axis (b) because it is a property of the tenancy axis, not of region. (1) forward: inert indexes
    start firing and a race that produced two rows starts producing a `DbUpdateException` on a path that
    may not expect one — with the ADR-0038 §D3 money-path case named; (2) the reverse and dangerous
    direction: where an app-level guard was removed on the strength of such an index, the invariant is
    unguarded **now** (ADR-0035 §D3 named as the origin, ADR-0038 §Context as the live instance);
    (3) a **pointer, not a copy**, to `agents/knowledge/consistency.md` §"Tenant-scoped unique indexes";
    (4) the two re-derivation greps and **no frozen table**.
  - **AC2′ — satisfied.** No entity configuration, no migration, no behaviour, no `agents/knowledge/`
    edit, no touch to any `adr/challenges/*.md`. The nine-row inventory was **not** copied anywhere.
  - ➕ **The one addition, and the reason for it: an index-only note leaves the reader to walk into the
    query-filter version of the same fact.** Both halves start with *"`TenantId` is nullable and NULL is
    production today"* and both are inert today, but they fail in **opposite** directions on activation —
    the index half starts **rejecting writes** (loud), the filter half starts **hiding rows** (silent,
    and reported as a legitimate "none"). The section carries both, with a table of the contrast and the
    **two** questions a new tenant-scoped table must answer: *is this index a sole arbiter or a
    backstop?* **and** *who writes the rows, and with what tenant?*
  - **Both halves are grounded in this sprint's live examples rather than written abstractly**, as
    instructed: ADR-0041 D9 reached for `.AreNullsDistinct(false)` on every new tenant-scoped index as an
    S8 formality (`0041-…md:433-437`), and `challenges/0041-schema.md` CH-S14.3 sustained that the
    *reason* was wrong (the catalog decides by the index's **job**) while **CH-S4** (`:268-335`) showed
    that *"the real tenancy hole is not on a unique index — it is on the query filter"*: config rows
    seeded `TenantId NULL` (`sql-scripts/insert_seed_data.sql:1548`) are invisible to every tenanted
    caller, and the ADR's own D4.3 reports that invisibility as `required: false` — the gate switches
    itself off in the unsafe direction. The panel lead sustained it **blocking** and ruled RB-9
    (`0041-…md:914-924`), noting that a tenantless key makes *"the `.AreNullsDistinct(false)` question
    evaporate rather than being answered on the wrong grounds"*.
    ⚠️ *Precision note for the record:* the brief described example 1 as a *"one current acceptance"*
    invariant on such an index. In the tree the invariant that rests on a `(TenantId, …)` unique index is
    **"one current version"** — `UNIQUE (TenantId, Kind, CountryId, Version)` (`0041-…md:141`), which D5's
    *current-acceptance* gate resolves **through**. CH-S6 (`challenges/0041-schema.md:385-413`) is the
    independent second defect on the same index (it constrains `Version`; resolution reads
    `EffectiveFrom`) and says the index *"creates a false sense that cardinality is handled"* — which is
    the identical failure mode from the other side. Cited as it is, not as it was described.
  - **What exists as a guard, stated honestly and verified against the workflow file, not from memory:**
    `src/Cleansia.Tests/Infrastructure/NullsNotDistinctIndexModelTests.cs` asserts the option on each
    sole-arbiter index **and** carries a negative control (`UserMemberships`, deliberately nulls-distinct)
    so the theory cannot pass on a reader that answers `false` for everything. It runs in
    `backend-ci.yml`'s *"Unit tests (Cleansia.Tests)"* step (`:69-74`), which carries no
    `continue-on-error` — so it can genuinely go red. **No `**Enforced by:**` label was attached
    anywhere**: a living decision doc is not `agents/knowledge/`, and the test is cited descriptively,
    including its limits (its roster is hand-maintained; a new sole-arbiter index is uncaught until
    someone adds a row).
  - 🚩 **Two things left for the PM, deliberately not done here** (both are `agents/knowledge/` edits, a
    lane this ticket forbids and two live lanes may hold): (a) **the filter half has no catalog home** —
    `security-rules.md` S8 asks *"could two tenants both have rows here?"* and stops; it never states
    that a `TenantId NULL` row is invisible to a tenanted caller. That is a candidate `consistency.md`
    entry, and CH-S4 is the evidence for it. (b) S8's sentence *"Unique indexes on tenant-scoped tables
    are `(TenantId, X)`, not `(X)`"* reads as unconditional against `consistency.md`'s shape (c) (*drop
    `TenantId` from the key where a globally-unique FK pins the tenant transitively*) and against RB-9's
    ruling. Not a defect this ticket may fix; named so it is not re-derived a fourth time.
  - **Incidental, in the same doc and in support of the new section:** Axis (a)'s three query-filter cells
    cited `:171-240` / `:222-234` / `:209-217`; the method now spans `:201-270`. **Re-anchored to named
    expressions** (`ApplyTenantQueryFilters`, `singleTenantMatch`, the `body` `OrElse`) rather than to
    corrected digits — the ADR-0031 §A lesson, since three different documents already cite three
    different line ranges for this one method.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
