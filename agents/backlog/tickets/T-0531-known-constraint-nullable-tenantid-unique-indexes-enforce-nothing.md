---
id: T-0531
title: Known constraint — a unique index containing nullable TenantId enforces nothing in single-tenant mode
status: draft
size: S
owner: pm
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0028]
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

## Acceptance criteria

- [ ] **AC1 — the constraint is recorded where a designer will meet it.** Given
      `agents/architecture/decisions/multi-tenancy-and-region.md`, When the architect updates it, Then it
      carries the constraint, the nine-index inventory, the three legitimate shapes, and the rule *"a
      nullable-`TenantId` unique index may not be a design's sole arbiter"*. **Evidence:** the doc diff.
- [ ] **AC2 — the two corrections are on the record.** Given the corrections above, When the doc is
      updated, Then both are stated: nine indexes carry `TenantId` (not zero), and `NULLS NOT DISTINCT`
      already ships twice (so it is not a novel construct). **Evidence:** the doc diff naming the
      file:line citations.
- [ ] **AC3 — the misleading in-code comment is corrected.** Given
      `UserMembershipEntityConfiguration.cs:106-109`'s *"rather than introduce a one-off NULLS NOT
      DISTINCT"*, When the change lands, Then that clause is corrected to reflect that the construct is
      already used at `FiscalCounterEntityConfiguration.cs:28` and `LiveActivityTokenConfiguration.cs:28`.
      **The index itself does not change** — only the sentence that would talk the next reader out of a
      shipped option. (Same rule as T-0530: a comment asserting something untrue stops the reviewer.)
- [ ] **AC4 — ADR-0028's activation list gains it.** Given the multi-tenancy activation work, When AC1
      lands, Then the doc states that switching multi-tenancy on **changes the enforcement status of eight
      of the nine indexes** — invariants that are today guaranteed by app-level code start being guaranteed
      by the database, and any place where the app-level guard was *removed* on the strength of the index
      breaks in the other direction. This is a pre-activation checklist item, not a today item.
- [ ] **AC5 — nothing is fixed.** Given the diff, When it is reviewed, Then it contains **no** entity
      configuration change other than AC3's comment, **no** migration, and **no** behaviour change. If the
      architect concludes an index genuinely needs `AreNullsDistinct(false)` today, that is a **separate
      ticket with an `ef-migration` manual step** — file it, do not do it here.

## Out of scope

- Changing any index. See AC5. Any index change needs an owner-run EF migration and does not belong in a
  note ticket.
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

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
