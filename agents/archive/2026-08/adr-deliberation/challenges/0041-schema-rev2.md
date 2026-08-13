# ADR-0041 rev 2 — Challenge, schema / data-integrity / query-plan lane

**Mode:** challenger, second panel. **Gate 0: REFUTED by default.** Fifth distinct instance on this
ADR — not the rev-1 author, not the rev-2 author, neither round-1 challenger, not the lead.

**Findings are numbered `CH-S2-n`** to avoid collision with round 1's `CH-S1 … CH-S14`.

**What I did.** No migration was created or run; no `dotnet ef` command was issued; no git write; no
`src/` edit; no ADR edit. Everything below is either a `file:line` I opened in the working tree on
2026-08-05, or a **measurement**:

- I built rev 2's D1 schema block as a throwaway EF Core 10.0.0 + Npgsql 10.0.0 model in a scratch
  project outside the repo, and read the **emitted DDL** and the **emitted SQL** (`ToQueryString()`)
  for D6.1's coverage predicate and D1.4's supersession predicate.
- I ran that DDL, 8 000 statements / 48 000 invoices, and `EXPLAIN (ANALYZE, BUFFERS)` on a
  **throwaway PostgreSQL 16.14 container of my own** (`adr0041-schema-probe`, port 55442, removed
  afterwards). I did **not** touch the Testcontainers Postgres a sibling lane had live on 33358.
- Absolute timings below are from a slow Docker-on-macOS host and are **not** production numbers. The
  **ratios** and the **plan shapes** are the findings; I say so at each one.

**Headline.** The shape survives again — I attacked D10, D1.3, D1.8 and the two-order split and could
not break any of them (§Found sound). Rev 2's citation hygiene is **repaired**: I sampled 22 citations
and every one is exact, including two where rev 2 is *more* precise than round 1's challenger. The
method problem is fixed.

What does not survive is the layer under D6 and D1.4, and it fails in four measured places:

1. **D6.2 test 5 — "query cost is not a loss… the coverage index serves it" — is false as EF emits
   it.** The report the whole non-blocking posture rests on runs in **90.4 s** in EF's own emitted
   shape versus **0.8 s** hand-written on identical data and the identical index: **109×**, entirely
   from EF emitting the correlated lookup **twice** and losing `Memoize`. And my 0.8 s is
   *optimistic* — see CH-S2-1. (CH-S2-1)
2. **The coverage index leads with `TenantId`, and the global tenant filter demotes `TenantId`,
   `Kind` and `OccurredAt` out of the `Index Cond` into a residual `Filter`.** Measured on EF's exact
   emitted predicate. This is verbatim the failure mode `consistency.md` §T-0540 exists to stop, and
   the ADR's 19 checks contain no plan assertion. Like round 1's finding, it is **single-tenant-only**
   — the tenanted plan is clean. (CH-S2-2)
3. **D1.4's atomic append silently discards a true statement under a lost race.** Measured: `MAX+1` +
   `ON CONFLICT DO NOTHING RETURNING` returned **zero rows** and the `Revoked` statement **is not in
   the log**. That directly refutes D1.5's stated property, *"the append never refuses a true
   statement."* The cited precedent has **no retry** and its zero-row return means something there
   that does not exist here. (CH-S2-3)
4. **…and `Sequence` — the sole cause of (3) — buys nothing the primary key does not already
   provide.** `BaseEntity.Id` is a ULID. `ORDER BY RecordedAt DESC, Id DESC` is already total,
   immutable and race-free. (CH-S2-4)

Plus: **append-only's three checks do not cover the delete mechanism this codebase actually uses**
(CH-S2-5), and **coverage is decided by an operator-typed, unvalidated, uncorrectable column**
(CH-S2-6).

**And one thing the ADR asked me to decide, decided: D1.6's three-level composite-FK discriminator
chain IS expressible in EF Core 10 + Npgsql 10.** I built it; the emitted DDL is in CH-S2-7 with its
four costs. The author was right to flag it rather than assert it, and right on the substance.

**Blocking: CH-S2-1, CH-S2-2, CH-S2-3, CH-S2-4, CH-S2-5, CH-S2-6.**

---

## Citation sampling (Gate 0) — the method problem is fixed

Round 1's decisive procedural finding was two false "verified in the working tree" rows. §V.7 ruled
that *"the remainder is unverified and the rebuild must not treat it as verified."* I therefore
sampled rev 2's `A2`-marked rows rather than inheriting them. **22 sampled, 22 exact:**

`EmployeeInvoice.cs:12` (`EmployeeId` `private set`) · `:60` (`GeneratedAt` `private set`) · `:115` ·
`:125` · `:130-146` · **`:138`** (`return Create(`) · `:321-322` (the immutability comment) ·
`BaseEntity.cs:5` (ULID) · **`BaseEntity.cs:7`** (`IsActive` public setter) ·
**`UserConsent.cs:46-54`** (`Regrant`) · `IRepository.cs:41/43/45/47/59` ·
`IAdminActionAuditRepository.cs:5` · `AdminActionAudit.cs:6-35` · `AdminActionAuditConfiguration.cs:15-21` ·
`OrderStatusTrack.cs:14-18` and `:27` · `Order.cs:445-459` · `ApproveEmployee.cs:12/36-49/56-62/114-119/124-125` ·
`RequireCompleteProfileAttribute.cs:32-35`, `:37-49` · `Employee.cs:321`, `:323-349` ·
`EmployeePayoutDetails.cs:22-24` · `GdprDeletionService.cs:206-208` · `MigrationService/Program.cs:31-36` ·
`PayPeriodBackgroundService.cs:107/115-122/133-142/155/187/298/312-323/328/334` ·
`CleansiaDbContext.cs:229-264`.

Three notes worth recording:

- **Rev 2 is more exact than round 1's challenger in two places.** `BaseEntity.IsActive` is at `:7`
  (round 1 said `:9`); `UserConsent.Regrant` is at `:46-54` (round 1 said `:47-55`). Rev 2 is right
  both times.
- **The "seven direct call sites" count is correct and the enumeration is complete.** `EmployeeInvoice.Create`
  has exactly seven call sites outside the class — `DomainSeed.cs:160`, `PayrollMockFactory.cs:52`,
  `AdminInvoiceAdjustmentHandlerTests.cs:25`, `EmployeeInvoiceEntityTests.cs:19` and `:34`,
  `MarkInvoicePaidNotifyTests.cs:26`, `FiscalReconciliationQueryTests.cs:337` — and I found no eighth.
  The **eighth caller** is the in-class delegation `EmployeeInvoice.cs:138`, which rev 2 cites
  separately in the context table and in its RB-2 answer. If a reader counted eight, that is the
  difference; neither number is wrong and the ADR does not conflate them.
- **Rev 2 is right where `CLAUDE.md` is stale.** A5's *"up to ~31 days, pay periods are monthly —
  verified"* is correct: `PayPeriodBackgroundService.cs:93` and `:165` both compute
  `startDate.AddMonths(1).AddDays(-1)`. `CLAUDE.md`'s Key Entities table still says *"PayPeriod |
  Bi-weekly pay cycle"*. Routed to the docs lane; it is not this ADR's error.

**Can any invoice writer produce an invoice whose employee has no statement?** Yes — both production
writers, unconditionally, because D5 never blocks issuance. The derived answer is `covered = false`,
which is the intended answer. I could not construct a case where derivation returns the *wrong*
Boolean for a well-formed log. The attacks below are on cost, on enforcement, and on what the Boolean
means — not on its arithmetic.

---

## CH-S2-1 — D6.2's cost defence is a plan claim with no plan test, and it is false in EF's own emitted shape: **90.4 s vs 0.8 s on identical data and the identical index**. **BLOCKING**

**The claim.** D6.2 test 5, in full: *"Query cost is not a loss: volumes are cleaners × months, and
the coverage index (D1) serves it."* One sentence, no measurement. D6.3 then makes the four-row report
*"not optional… it is what makes D5's non-blocking posture defensible."*

**What EF actually emits.** I wrote D6.1's definition as LINQ and captured `ToQueryString()`. EF
emits the correlated top-1 lookup **twice per invoice row**, because `covered` is a two-part boolean
(`… FirstOrDefault() == Accepted` needs both an equality test and a null test):

```sql
SELECT e."Id",
  ( SELECT e0."Action" FROM "EmployeeAgreementStatements" AS e0
    WHERE (…tenant filter…) AND e0."EmployeeId" = e."EmployeeId" AND e0."Kind" = 1
      AND e0."OccurredAt" <= e."GeneratedAt"::timestamptz
    ORDER BY e0."OccurredAt" DESC, e0."RecordedAt" DESC, e0."Sequence" DESC LIMIT 1) = 1
  AND
  ( SELECT e0."Action" FROM "EmployeeAgreementStatements" AS e0
    WHERE (…the same predicate again…)
    ORDER BY e0."OccurredAt" DESC, e0."RecordedAt" DESC, e0."Sequence" DESC LIMIT 1) IS NOT NULL
  AS "Covered"
FROM "EmployeeInvoices" AS e WHERE …
```

**Measured**, 4 000 cleaners × 8 000 statements × 48 000 invoices, PostgreSQL 16.14, both specified
indexes present, `ANALYZE`d:

| Shape | Plan | Execution |
|---|---|---|
| **EF's emission (subquery twice)** | `Seq Scan on EmployeeInvoices` + **`SubPlan 1` (48 000 loops) + `SubPlan 2` (45 832 loops)**, each an `Index Scan using IX_EAS_coverage`, **no `Memoize`** — 93 832 index scans, `Buffers: shared hit=331154` | **90 411 ms** |
| Hand-written `LEFT JOIN LATERAL` (subquery once) | `Nested Loop Left Join` → **`Memoize` (Hits 36 000 / Misses 12 000)** → `Index Scan using IX_EAS_coverage`, `Buffers: shared hit=42746` | **827 ms** |

**109×, on the same data, with the same index, from the same definition.** The index is not the
problem — in both plans `Index Cond` correctly carries all four terms. The problem is the **outer
loop cardinality × 2**, and the loss of `Memoize`: PostgreSQL cannot memoize a `SubPlan`, only a
LATERAL/nested-loop inner side.

**And my 827 ms is optimistic, by a lot.** `Memoize`'s cache key is `(EmployeeId, GeneratedAt)`, and
it got a 75 % hit rate only because my seed used 12 distinct `GeneratedAt` values. In production
`GeneratedAt = DateTime.UtcNow` is stamped per invoice at `EmployeeInvoice.cs:125`, so **every invoice
row has a distinct key and the hit rate is 0 %** — all 48 000 lookups execute. The honest floor for
the LATERAL shape is therefore ~4× the measured 827 ms, and for EF's shape there was never any
memoization to lose.

**Why this is blocking rather than a tuning note.**

1. `consistency.md` §*"A comment claiming a query PLAN property is pinned by `EXPLAIN` over the
   statement EF actually emitted"* (T-0540) is **ratified and binding**, and its two sub-rules land
   exactly here: *"EXPLAIN the captured statement, never a hand-written copy of it"* and *"'No Seq
   Scan' is not the assertion."* D6.2 test 5 is a plan claim. §How-a-reviewer-verifies has **19
   checks and not one is a plan assertion**. Check #5 drives the timer path and asserts *classification*,
   which is correctness, not cost — it would pass green at 90 s.
2. This is the same defect class the lead named in §V.3 — *"everything is verified except the thing
   that matters"* — recurring in the rebuild, on the decision that is **new in rev 2**. Rev 1's
   uncheckable property was "append-only"; rev 2's is "derivation is cheap".
3. It is load-bearing, not incidental. D6.3's report is the **sole compensating control** for D5's
   never-blocks posture (the ADR says so twice) and for A5's rejection. A control that takes a minute
   and a half is a control nobody runs on a schedule, and "the number nobody watches" is precisely
   CH-A2, which the ADR believes it answered.

**What I want changed** (a property, not a repair — I am not designing it):

- D6 states the coverage read as a **shape**, not a definition — one evaluation per invoice — and the
  ADR carries a check that **EXPLAINs the captured statement** the production entry point emits and
  asserts (a) exactly one scan node over `EmployeeAgreementStatements` per invoice row, (b) the
  `Index Cond` of the node naming the coverage index carries `EmployeeId`, `Kind` **and**
  `OccurredAt`. The shipped precedents are `OrderStatusSetPredicatePlanTests` and
  `UserMembershipCancellationSweepIndexPlanTests`; both use a `DbCommandInterceptor` that re-runs
  `"EXPLAIN " + command.CommandText` on the same connection and parameters.
- The seed for that check must populate **thousands of rows and distinct `GeneratedAt` values**, per
  T-0540's *"a plan assertion on an empty or uniform table"* deviating form. My own probe fell into
  the uniform-key trap and it flattered the design by ~4×.

*Blocking?* **Yes.** The ADR's non-blocking posture is bought with a report, and the report as
specified is 109× more expensive than the ADR believes, with no mechanism that would ever tell anyone.

---

## CH-S2-2 — The coverage index leads with `TenantId`, and the global tenant filter demotes `TenantId`, `Kind` and `OccurredAt` out of the `Index Cond`. Measured on EF's exact predicate. **BLOCKING**

**The hole.** D1 specifies `INDEX (TenantId, EmployeeId, Kind, OccurredAt DESC, RecordedAt DESC, Sequence DESC)`.
`EmployeeAgreementStatement` is `ITenantEntity` (D10, correctly), so **every** read of it carries the
global filter from `CleansiaDbContext.cs:229-264`, which EF emits as a three-armed `OR` with two
boolean **parameters**:

```sql
WHERE (@ef_filter___providerNull0 OR (@ef_filter__p0 AND e0."TenantId" IS NULL) OR e0."TenantId" IS NULL)
  AND e0."EmployeeId" = … AND e0."Kind" = 1 AND e0."OccurredAt" <= …
```

The index's **leading column appears only inside that `OR`.** A btree cannot put an OR'd predicate on
its leading column into an `Index Cond`, and PostgreSQL 16 has no skip scan, so the remaining columns
become unreachable too.

**Measured, EF's exact predicate, parameters bound, custom plan:**

```
Limit
  ->  Sort  (Sort Key: "OccurredAt" DESC, "RecordedAt" DESC, "Sequence" DESC)
        ->  Bitmap Heap Scan on "EmployeeAgreementStatements" e0
              Recheck Cond: (("EmployeeId")::text = 'EMP1234'::text)
              Filter: (("TenantId" IS NULL) AND ("OccurredAt" <= …) AND ("Kind" = 1))     ← DEMOTED
              ->  Bitmap Index Scan on "IX_…_EmployeeId"
                    Index Cond: (("EmployeeId")::text = 'EMP1234'::text)
```

**`TenantId`, `Kind` and `OccurredAt` are all residual `Filter` terms.** With no `EmployeeId`-leading
index available at all — i.e. with only the two indexes D1 specifies — the same predicate produced a
**`Seq Scan`, `Rows Removed by Filter: 7998`**.

Two more measured facts that pin the mechanism:

- Written with **literal** booleans instead of parameters, PostgreSQL folds the `OR` away and the
  `Index Cond` correctly carries all four terms. So the plan **depends on whether EF's two filter
  booleans arrive as literals or parameters**, and EF emits parameters. Nobody has pinned which plan
  production gets.
- **In tenanted mode the `OR` collapses to a single arm** (`e0."TenantId" = 'T1'`) and the plan is
  clean: `Index Cond: (("TenantId" = 'T1') AND ("EmployeeId" = …) AND ("Kind" = 1) AND ("OccurredAt" <= …))`,
  483 ms for the whole report. **The degradation is single-tenant-only — i.e. production today.**
  That is the exact mirror of round 1's CH-S4: a defect that is invisible in the only deployment
  that exists.

**Why this is blocking.** `consistency.md` §T-0540 names this by name — *"Pushing the term inside an
`OR` … merely demotes the term out of the `Index Cond` into a residual filter — green under a seq-scan
check"* — and the plan above is a **Bitmap Index Scan**, so even a "no Seq Scan" assertion passes.
Compounded with CH-S2-1, the two effects multiply: 48 000 outer rows × 2 subqueries × a demoted
index. I did **not** measure that combination and I am not asserting a number for it; I am asserting
that nobody has, and the ADR spends one sentence saying it is fine.

**What I want changed.** A property: **the coverage index's leading columns are the query's sargable
equality terms.** `EmployeeId` is a ULID and pins its tenant transitively, so `(EmployeeId, Kind,
OccurredAt DESC, RecordedAt DESC, Sequence DESC)` is seekable however the tenant filter lands, with
`TenantId` left as the residual check it is going to be anyway. Whatever the panel chooses, the
choice must be **discharged by an `Index Cond` assertion**, not by an index declaration.

*Blocking?* **Yes.** It is a schema decision (which columns, in which order), so it must land before
DDL, and it is undetectable by every check the ADR lists.

---

## CH-S2-3 — D1.4's atomic append **silently discards a true statement** under a lost race. Measured. It refutes D1.5. **BLOCKING**

**The claim it breaks.** D1.5: *"nothing constrains `(Employee, Kind, Action, AgreementVersionTextId)`,
so two concurrent identical accepts remain two identical true statements and **the append never
refuses a true statement**."* D1.4 then specifies the mechanism: *"one statement that derives the
ordinal in SQL and inserts, `ON CONFLICT DO NOTHING`, `RETURNING`… It is `MAX(Sequence)+1`."*

**Measured, on the specified schema with the specified unique index:**

```
-- append #1 (MAX+1 ⇒ ordinal 0):                       Sequence = 0   INSERT 0 1
-- append #2, a session whose MAX read preceded #1's commit (ordinal 0):
                                                        (0 rows)       INSERT 0 0
-- what the log contains for that cleaner:
   Id | Action | Sequence
   R1 |      1 |        0        ← the Accepted
                                 ← the Revoked is NOT THERE
```

The second statement — a `Revoked` — **is not in the append-only log**, and the caller received no
exception, because `ON CONFLICT DO NOTHING` does not raise `23505`. It received zero rows.

**The precedent does not carry the retry, and its zero-row return means something else.**
`MembershipBenefitUsageRepository.TryReserveSlotAsync` — the shape D1.4 names verbatim — ends
`if (reservedOrdinals.Count == 0) { return null; }` (`:107-111`). There, zero rows legitimately means
*"the quota is exhausted"*: it derives the **smallest free** ordinal from `generate_series(0, @max-1)`
against a set whose slots are **released**, and its own 20-line doc-comment (`:17-38`) is about
exactly that. Here nothing is released, there is no quota, and **zero rows has exactly one cause: a
concurrent append took the ordinal.** The correct response is a bounded retry — which the ADR
specifies only for the *rejected* cheap variant (*"`MAX+1` read in the handler … and one bounded
retry on `23505`"*) and not for the one it chose. The atomic form is atomic per attempt; the **loop is
the mechanism**, and it is absent.

**Three more costs the ADR does not price, all verified in the precedent it cites:**

1. **The raw INSERT bypasses the global query filter and the change tracker, so the tenant term must
   be hand-written into the SQL — twice.** The precedent does it: `@tenantId` in the INSERT list and
   `u."TenantId" IS NOT DISTINCT FROM @tenantId` inside **both** guard subqueries
   (`MembershipBenefitUsageRepository.cs:41-59`), plus `reserved.TenantId = tenantId` on the returned
   entity (`:117`). Omit any of them and the statement is either stamped `NULL` (invisible to its own
   tenanted reader — round 1's CH-S4 defect, relocated onto the legal record itself) or its `MAX` is
   computed across tenants. The ADR's D10 concludes *"it rides the filter"*; with a raw append it does
   not, and D10 does not say so.
2. **A documented `42P08` trap that fires in single-tenant mode only.** The precedent's own comment
   (`:92-98`): the bare `@tenantId` parameter must be declared `NpgsqlDbType.Text` explicitly, *"which
   is why the promo path shipped this bug past a tenanted test run."* Third consecutive item on this
   ADR whose failure is invisible in the only deployment that exists.
3. **It auto-commits ahead of the unit of work**, which makes it a self-committing write inside a
   handler. `consistency.md` §ADR-0038 (a) is explicit that the deviating form is *"a self-committing
   write inside a handler with no sanctioned-exception doc-comment — the documented exception is
   `PromoCodeRepository.TryIncrementGlobalRedemptionsAsync`, and it is an exception **because it says
   so**, not because it exists."* The ADR names the audit-ordering consequence and not this
   obligation.

*Blocking?* **Yes.** A legal append-only log that drops a `Revoked` under a double-submit, with no
exception and no retry, is a worse record than the boolean this ADR exists to replace — and D1.5
asserts the opposite property in the ADR's own words.

---

## CH-S2-4 — `Sequence` is the sole cause of CH-S2-3, and it buys nothing the primary key does not already provide. **BLOCKING (it is a schema column; pre-DDL is the only free moment)**

**The premise D1.4 imports, and the enabling condition it leaves behind.** The house lesson is real:
`OrderStatusTrack.Sequence` (`:14-18`) exists because *"CreatedOn is millisecond-resolution and ties"*,
and `Order.AddOrderStatus` orders on two columns. But read how the ordinal is produced:

```csharp
// Order.cs:447-448  — inside the loaded aggregate, in memory, with no database involved
orderStatusTrack.AssignSequence(
    _orderStatusHistory.Count == 0 ? 0 : _orderStatusHistory.Max(s => s.Sequence) + 1);
```

The comment says why it is safe: *"the order is the consistency boundary."* D1.4 notices the condition
is absent — *"Because there is no loaded aggregate to count within"* — and then reaches for
concurrency machinery instead of asking whether the **column** transfers. It does not; only the
**two-column total order** transfers.

**The house already ships a total order on this table for free.** `BaseEntity.Id` is
`Ulid.NewUlid().ToString()` (`BaseEntity.cs:5`) — the primary key, therefore unique, therefore a total
order, and lexicographically ordered by its millisecond timestamp prefix. **`ORDER BY RecordedAt DESC,
Id DESC` is total, immutable, deterministic and stable**, which is every property D1.4 asks of
`(RecordedAt, Sequence)`.

**And `Sequence` cannot even do the job better, because it is not the leading key.** The supersession
order is `RecordedAt DESC, Sequence DESC`. `RecordedAt` is a `timestamptz` (microsecond) stamped in
C# **before** the insert. So `Sequence` only ever arbitrates a *microsecond tie* — and in a tie, the
insertion order need not agree with the `RecordedAt` order anyway (two requests can stamp
`RecordedAt` in one order and insert in the other), so `Sequence` is not "more correct" than the PK.
It is an arbitrary-but-stable tiebreak with extra steps.

**What the column costs, all of it avoidable:**

| Cost | Where |
|---|---|
| A raw-SQL append that bypasses the tenant filter and the change tracker | CH-S2-3(1) |
| A `42P08` single-tenant-only parameter-typing trap | CH-S2-3(2) |
| An auto-commit ahead of the unit of work + the ADR-0038 doc-comment obligation | CH-S2-3(3) |
| A lost-append race with no retry, silently dropping a true statement | CH-S2-3 |
| A `UNIQUE (TenantId, EmployeeId, Kind, Sequence)` index whose `NULLS NOT DISTINCT` decision must be argued, emitted, and hand-added to `NullsNotDistinctIndexModelTests` (check #18) | D1.4, check #18 |
| An audit row that can record *failure* for a write that committed | D1.4, named by the ADR |
| Check #16's rule that `Sequence` may never ride a DTO, because *"a monotonic per-partition ordinal is an information leak"* | check #16 |

Seven costs, one of them a measured data-loss bug, to obtain an ordering property the primary key
already has. The ADR's own D12 is a pricing table; `Sequence` is the one decision in rev 2 that is not
priced against its alternative, and the ADR flags the *mechanism* as a hedge (`⚠️ HEDGE, flagged for
the panel`) while treating the *column* as settled. **The schema is not identical either way** — that
is the one sentence in D1.4's hedge that is wrong, and it is the sentence that made the hedge look
cheap.

*Blocking?* **Yes**, on the ADR's own D1.0 reasoning: a column is frozen by DDL, and pre-DDL is the
only free moment.

---

## CH-S2-5 — Append-only's three checks do not cover the delete mechanism this codebase actually uses. Under D6 that is not hygiene: it changes the coverage answer for every invoice ever issued. **BLOCKING**

**Credit first.** D11 is the most honest section in the document. It confirms zero
`HasCheckConstraint` / zero `HasTrigger`, refuses to invent a narrow repository interface (RB-7 —
I re-verified: **57 of 57** `I*Repository` interfaces in `Core.Domain/Repositories/` derive from
`IRepository<T, string>`, including `IAdminActionAuditRepository.cs:5`), states the narrowed property
in its own words, prices the two real mechanisms and routes the trigger to its own ADR. Nothing below
attacks any of that.

**The hole is that the narrowed property is narrower than stated.** Check #3 is
*"no production code calls `Remove` / `RemoveRange` / `Deactivate` / `DeactivateRange` on the statement
repository."* That enumeration is a closed list of four method names. But `IRepository` also hands out
raw queryables — `GetAll()` (`:35`), `GetQueryable()` (`:49`), `GetQueryableIgnoringTenant()` (`:59`)
— and on any of them `ExecuteDeleteAsync` / `ExecuteUpdateAsync` is one call away. **This codebase
uses them:** `ExecuteUpdateAsync` at 16 production sites (`UserRepository.cs:164/186/206/222`,
`RefreshTokenRepository.cs:102`, `PromoCodeRepository.cs:43/61`, `UserNotificationRepository.cs:45`,
`MembershipBenefitUsageRepository.cs:159`, `DataRetentionBackgroundService.cs:79/85/129`,
`DeactivateAdminUser.cs:71`) and **`ExecuteDeleteAsync` at `RefreshTokenRepository.cs:175`**.

Both are invisible to all three checks:

- **Check #2** is a reflection test over the entity type — `sealed`, no public setters, two factories.
  `ExecuteUpdateAsync` never materializes an entity, so the reflection test is **vacuous** against it:
  it can set `Action`, `OccurredAt`, `BodyHash`, anything.
- **Check #3** greps four method names that a set-based delete does not use.
- **Check #4** greps `IsActive`, an unrelated axis.

And the ADR itself supplies the template a future author will copy: **D8 requires a new repository
method that mutates existing statement rows** (`RedactPersonalFieldsForEmployeeAsync`). The
`DataRetentionBackgroundService` sites above are precisely how bulk redaction is written in this
codebase today. The next author reaching for "redact/purge statements older than N" writes
`ExecuteUpdateAsync`/`ExecuteDeleteAsync`, and every check passes.

**Why this is blocking under D6 and would not have been under A7.** The author names the strongest
attack on derivation himself: *"a deleted statement silently un-authorizes every past document…
I concede it is the weakest joint in this ADR"* — and answers it with *"D11 + checks #2–#4."*
Those checks do not close it. Note also what check #3 actually is: *"a grep, and a
`check-consistency.mjs` rule **if the catalog lane takes it**"* — and `consistency.md`'s own interim
box records that `check-consistency.mjs` *"appears in **zero** `.github/` workflows, so it can never
set an exit code."* So the strongest form check #3 can ever reach is T2-ADVISORY, conditional on
another lane volunteering. That is the enforcement tier the lead called documentation in RB-8.

Under A7 the same deletion is refused by the database for every stamped row (`Restrict`). D6.2 test 5
answers that with *"it protects only stamped rows (an un-stamped statement is still freely
deletable)."* True — and the rows that matter are exactly the referenced ones. The argument is that a
partial anchor is worse than **no anchor**, which does not follow. The honest statement of the
trade-off is: **derivation moves the only available referential anchor off the design and routes its
replacement (ticket 11) to a future ADR, then ships the design that depends on it.**

**What I want changed** (properties, not a repair):

1. Check #3 enumerates the **queryable-returning members** as well, or — better — states the property
   without a method list: *no production code issues a DELETE or an UPDATE against
   `EmployeeAgreementStatements` other than D8's named redaction*, discharged against the emitted SQL
   (a `DbCommandInterceptor` over the integration suite already exists for the plan tests).
2. D11's honest-property paragraph adds the clause it is missing: *"…and a database that enforces
   nothing, **against a data-access layer on which a set-based DELETE is one call from any holder of
   the repository**."*
3. The ADR states plainly whether D6 is conditional on ticket 11. If the weakest joint's mitigation is
   routed to another ADR, the panel should decide whether derivation ships before or after it. Right
   now the ADR both concedes the joint and ships past it.

*Blocking?* **Yes.** Not because deletion is likely, but because D6's entire argument against A7 is
that derivation loses nothing important, and the thing it loses is the only mechanism in the design
that a database enforces.

---

## CH-S2-6 — Coverage is decided by an **operator-typed, unvalidated, append-only** column. The two-order split closes the direction the author tested and leaves the mirror open on the axis that answers the legal question. **BLOCKING**

**First, the split works.** I tried to break it and could not. Supersession is
`ORDER BY RecordedAt DESC, Sequence DESC` and **never reads `OccurredAt`**, so a paper signature dated
June and filed in August after a July revocation cannot resurrect the agreement. `RecordedAt` is
server-stamped. D1.4 is right, and right for the stated reason.

**Now the other direction, which the ADR frames as a feature.** D6.2 test 3: coverage *"can go
down… exactly when a paper signature is recorded with an `OccurredAt` preceding `GeneratedAt`."*
Correct. But the same mechanism has three properties the ADR never states:

1. **`OccurredAt` is operator-typed and the ADR specifies no bound on it.** D7 hands an admin a free
   date field; D1.4 says so explicitly (*"`OccurredAt` is **operator-typed** on the paper-signature
   channel"*). There is no stated invariant — not `OccurredAt <= RecordedAt`, not a lower bound, not
   "not in the future". A single mistyped year (`2020-06-03` for `2026-06-03`) **retroactively covers
   every invoice this cleaner has ever received.**
2. **The row cannot be corrected, by construction.** Append-only plus D11's `init`-only entity means
   no path fixes `OccurredAt`. Appending a *correct* statement does not help: coverage takes the
   latest at-or-before `T`, so the erroneous 2020 row still covers everything between 2020 and the
   correction. The only mechanical remedy is appending a `Revoked` with an intermediate `OccurredAt`
   — which asserts an act the supplier never performed into a legal log, exactly what D8 forbids for
   erasure (*"writing a `Revoked` statement would be the platform **asserting an act the cleaner never
   performed** into an append-only legal log"*). **The ADR forbids the only available correction.**
3. **The report is the detector and it cannot see this.** D6.3's four rows count uncovered documents;
   a typo makes them *covered*. The `BodyHash` row detects text tampering, not date error — and for
   `Channel = AdminRecordedContract` there is **no `BodyHash` at all** (D1.3), so the paper channel,
   which is the only one with an operator-typed date, is also the only one with no integrity column.

**This is the derivation-specific half of the trade-off the ADR does not price.** Under A7 the same
typo affects the invoices issued *after* it and no others, because past stamps are frozen. Under
derivation it rewrites history in both directions. D6.2 test 4 argues this way round — *"a frozen
column computed by buggy code stays wrong forever while a derivation over immutable inputs is
re-derivable the moment the bug is fixed"* — and that is true of a **code** bug and false of a **data**
error, because the data is the thing that cannot be fixed. The ADR generalizes from the case that
favours it.

**Related, and cheap to state now:** D6.2 test 2 claims *"Derivation's negative has **one** meaning."*
Measured against the schema, the negative unions: never stated · revoked before issuance · stated
only after issuance · **the row was deleted (CH-S2-5)** · the country had no reviewed text so nobody
was ever asked (D4.4/D4.5, which the ADR itself calls fail-open). The ADR separates the last one by a
join to **current** state and defends that as *"it moves as the world moves"* — but that means a
country that receives reviewed text in September silently reclassifies August's rows, and **there is
no as-of-date reproducibility of the report at all**. Stamping froze that category, which the ADR
calls a defect; derivation cannot reproduce it, which the ADR does not call anything. Both are costs;
only one is priced.

**What I want changed.** Three properties: the `OccurredAt` invariant is **named in D1.3's variant
block and enforced in the two factories** alongside the nullability rules (at minimum
`OccurredAt <= RecordedAt`); D6 states plainly that a coverage answer is **not stable over time** and
that the compensating control for an operator date error is `RecordedAt`-ordered review, not the
report; and the ADR says whether an `AdminRecordedContract` statement may ever carry
`Action = Revoked` — because if it can, the typo un-covers rather than over-covers, and `Q-SELFBILL-04`
is the escalation that decides it.

*Blocking?* **Yes**, narrowly: the invariant is a factory-and-schema decision, and it is the
difference between "append-only" and "append-only and irreparable".

---

## CH-S2-7 — D1.6's composite-FK discriminator chain: **flagged, tested, and it works.** Here is the emitted DDL and its four costs

The ADR flagged EF feasibility for this lane rather than asserting it (D1.6: *"⚠️ Flagged for the `db`
agent, not asserted"*; §"attack these first" item 5). That was the right call and the answer is **yes**.

I built all three entities with both alternate keys and both composite FKs on EF Core 10.0.0 +
Npgsql 10.0.0, `<Nullable>enable</Nullable>`, and read the emitted DDL:

```sql
CREATE TABLE "AgreementVersions" ( … CONSTRAINT "AK_AgreementVersions_Id_Kind" UNIQUE ("Id","Kind") );

CREATE TABLE "AgreementVersionTexts" (
    "AgreementVersionId" text NOT NULL, "Kind" integer NOT NULL, "LanguageCode" citext NOT NULL, …
    CONSTRAINT "AK_AgreementVersionTexts_Id_Kind" UNIQUE ("Id","Kind"),
    CONSTRAINT "FK_…_AgreementVersions_AgreementVersionId_~"
        FOREIGN KEY ("AgreementVersionId","Kind") REFERENCES "AgreementVersions" ("Id","Kind") ON DELETE RESTRICT );

CREATE TABLE "EmployeeAgreementStatements" (
    "TenantId" text, "Kind" integer NOT NULL, "AgreementVersionTextId" text, "BodyHash" character varying(64), …
    CONSTRAINT "FK_…_AgreementVersionTexts_Agreement~"
        FOREIGN KEY ("AgreementVersionTextId","Kind") REFERENCES "AgreementVersionTexts" ("Id","Kind") ON DELETE RESTRICT );

CREATE UNIQUE INDEX "IX_…_TenantId_EmployeeId_Kind_Sequen~"
    ON "EmployeeAgreementStatements" ("TenantId","EmployeeId","Kind","Sequence") NULLS NOT DISTINCT;
```

And the model metadata, which is the part that was genuinely uncertain:

```
STATEMENT FK -> AgreementVersionText  props=[AgreementVersionTextId:NULL, Kind:NOT NULL]
                IsRequired=False  DeleteBehavior=Restrict
Kind.IsNullable = False
```

**The mixed-nullability composite FK models cleanly as an optional relationship with `Kind` staying
`NOT NULL`** — EF derives `IsRequired` from `Properties.Any(p => p.IsNullable)` and does not try to
make `Kind` nullable. D1.3's MATCH SIMPLE note is right: with `AgreementVersionTextId` NULL the
constraint is vacuously satisfied, which is exactly what the paper channel needs. `.AreNullsDistinct(false)`
emits, and `citext` emits with the extension. **Sustained on the substance.**

**Four costs the ADR does not name.** Each is small; together they are the difference between a design
and an entity configuration.

1. **EF auto-creates two FK-backing indexes the ADR does not enumerate** —
   `IX_AgreementVersionTexts_AgreementVersionId_Kind` and
   `IX_EmployeeAgreementStatements_AgreementVersionTextId_Kind`. Check #19 (*"every new index is named
   with `HasDatabaseName`"*) cannot reach them without redeclaring them explicitly.
2. **Identifier truncation, measured.** The obvious name for the unique index —
   `IX_EmployeeAgreementStatements_TenantId_EmployeeId_Kind_Sequence` — is **64 bytes**, and
   PostgreSQL 16 answered:
   `NOTICE: identifier "…_Sequence" will be truncated to "…_Sequenc"`. A `NOTICE`, not an error, and
   `pg_indexes` then reports the 63-byte name. EF's own truncation is *different* again
   (`…_Sequen~`). So one index has three candidate names, and a hand-written `DROP INDEX`/
   `CREATE INDEX CONCURRENTLY` copied from the migration file fails with *"does not exist"*. The
   entity name is 27 characters; `IX_` + it + `_TenantId_EmployeeId_Kind_` consumes 55 of the 63.
   Check #19 is therefore **load-bearing**, not the cosmetic parity with house practice the ADR calls
   it (CH-S14.5). *(It is not a precondition for check #18 — `NullsNotDistinctIndexModelTests` keys on
   **columns**, `:52-57`, not on names. That part of check #18 is fine.)*
3. **`Kind` is now an FK property and required payload simultaneously.** If a navigation to
   `AgreementVersionText` is ever added, EF's fixup nulls **all** FK properties when the navigation is
   severed, and `Kind` is a non-nullable enum. The design must state that the statement carries **no
   navigation** — FK scalar only. The ADR's CRC card says the collaborator *is* `AgreementVersionText`,
   which reads the other way.
4. **It is a first-of-class construct here.** `grep` over
   `src/Cleansia.Infra.Database/EntityConfigurations/` finds **zero** composite foreign keys and
   exactly **one** `HasAlternateKey` (`LanguageEntityConfiguration.cs:18`, `Languages.Code`). D1.6 adds
   the first two composite FKs and the second and third alternate keys in the schema. Worth stating in
   D12's pricing table rather than discovering at ticket 1.

*Blocking?* **No.** This one is answered, and the answer is favourable.

---

## CH-S2-8 — The coverage anchor is `GeneratedAt` because it is immutable, not because it is the right question — and the ADR presents availability as the reason the answer exists

D6.1: *"has exactly three inputs, and **all three are already immutable**."* That is an argument for
why the derivation is *possible*, and the ADR uses it as the argument for why it is *right*.

`GeneratedAt` is stamped `DateTime.UtcNow` inside `Create` (`EmployeeInvoice.cs:125`) — i.e. the
**pay-period close date**, up to a month after the work. So:

- A cleaner who accepts on **31 July** has their June work covered; one who accepts on **5 August**
  does not. Same work, same month, same agreement — different answer, decided by when the timer ran.
- A cleaner who worked all of June with **no** agreement and accepts on 31 July is recorded as
  covered for that work, because the document was printed in August.

If the question is *"were we authorized to issue this document"*, `GeneratedAt` is defensible. If it
is *"were we authorized to self-bill this work"*, it is not — and `EmployeeInvoice.PayPeriodId` is
right there, on the same immutable row, as the period anchor. The ADR never considers it, and
`Q-SELFBILL-02`'s re-framed wording (*"is the remedy reissue or a **retroactive acceptance**"*) reads
much more naturally against a period than against a print timestamp. **This is a schema-shaped
question** — it decides which column the report keys on and whether the derivation joins `PayPeriod`
— so it belongs before DDL even though its answer is the owner's.

*Also verified and NOT a problem, stated so nobody re-opens it:* `EmployeeInvoice.GeneratedAt` is
`DateTime` while the adopted archetype's occurrence timestamp is `DateTimeOffset`
(`AdminActionAudit.cs:26`). The comparison compiles via the implicit conversion and **EF translates
it**, casting the *invoice* side: `e0."OccurredAt" <= e."GeneratedAt"::timestamptz`. The statement
column stays uncast and therefore sargable. But the ADR must **state the CLR type of `OccurredAt`**,
because writing the predicate the other way round casts `OccurredAt` and kills the index — and D1's
schema block gives only the SQL type.

*Blocking?* **No** — but it is cheap now and a data question later.

---

## CH-S2-9 — Routed, not mine: D5's gate is a new `RuleFor` chain in a validator whose class-level cascade is `Continue`, and its jurisdiction input is validated in a **different** chain

`ApproveEmployee.Validator` has separate chains: three on `EmployeeId` (existence, not-already-approved,
`IsProfileComplete`) and one on `WorkCountryId` with `.Cascade(CascadeMode.Stop)` — `NotEmpty` → exists
→ **serviced** (`:56-62`). FluentValidation's class-level default is `Continue`, so chains do not gate
each other — the fact `consistency.md` records as ADR-0037's deviating form (*"a second `RuleFor` chain
in `TakeOrder.Validator` … rule-level `Cascade.Stop` does not span chains"*).

D5's new rule must read **both** `EmployeeId` (the subject) and `command.WorkCountryId` (the
jurisdiction, D4.1). It therefore runs **regardless of the `WorkCountryId` chain's verdict**. With a
blank or unserviced `WorkCountryId`, the resolver finds no `AgreementVersion` for that country, and by
D4.4 *"the gate does not fire"* — so **the mandatory gate silently passes on exactly the inputs that
are invalid**. It also produces a composite two-error response on an admin action, where the ADR
promises *"a rejected submit with a translated message"*.

Routed to the backend lane. I note it here only because the *containment* may be a schema question:
whether `AgreementVersionResolver` may be invoked with an unvalidated country id at all, or whether
"no version for this country" and "this is not a country" must be distinguishable at the resolver's
boundary. Right now they are the same answer, and one of them means the gate is off.

*Blocking?* **No** — routed.

---

## Found sound — what I attacked and could not break

Stated so the lead knows the coverage was real.

1. **D10's tenancy split.** I tried to argue `AgreementVersion`/`AgreementVersionText` back into
   `ITenantEntity` and could not: `Country`, `Language`, `CountryConfiguration` and — the ADR's own
   precedent — `CountryInvoiceConfig` are all tenantless, and the filter at `CleansiaDbContext.cs:229-264`
   makes a `TenantId NULL` config row invisible to a tenanted caller. Round 1 was right, rev 2 adopted
   it correctly, and my Q10 measurement incidentally confirms the tenanted read of the *statement*
   table is the clean plan. Keeping the statement log tenant-scoped is right for the reason given.
2. **D1.8's `Restrict` on `EmployeeAgreementStatement.EmployeeId → Employees`.** I tried to show it
   would block GDPR erasure. It does not: `GdprDeletionService` anonymizes and deactivates and never
   deletes — `user.Employee.Anonymize()` / `.Deactivated(...)` at `:242-244`, `user.Anonymize()` /
   `.Deactivated(...)` at `:247-248`; the only hard deletes are devices, notifications, cart, saved
   addresses and recurring templates. `Restrict` costs nothing on the live path and blocks the one
   path that would destroy the evidence, exactly as D1.8 says.
3. **The supersession half of D1.4.** Verified the ordering never reads `OccurredAt`, so the
   resurrection defect round 1 found is genuinely closed. My attack landed on the *coverage* axis
   (CH-S2-6), not this one.
4. **D1.3's nullable-by-variant.** The emitted DDL confirms the composite FK is vacuously satisfied
   when `AgreementVersionTextId` is NULL, and `Kind` stays `NOT NULL`. The `EmployeePayoutDetails` /
   `PayoutScheme` precedent transfers (`EmployeePayoutDetails.cs:22-24`, verbatim as quoted).
5. **RB-7's refutation.** Re-verified independently: **57 of 57** repository interfaces derive from
   `IRepository<T, string>`, including `IAdminActionAuditRepository.cs:5`. There is no precedent for a
   narrow surface. Routing it to its own ADR is right.
6. **The migration's additivity.** The emitted DDL is three `CREATE TABLE`s, four indexes and two
   alternate keys, with **zero** touches to any existing table — no rename, no drop, no
   non-nullable-without-default. S9-clean, and rev 2's claim is now smaller than rev 1's and still
   true. The `to_regclass` pre-deploy gate is the right precondition; `MigrationService/Program.cs:31-36`
   is exactly as cited.
7. **The launch-population correction (F3).** Re-verified: `sql-scripts/insert_seed_data.sql` contains
   **zero** `INSERT INTO public."Users"` and **zero** `INSERT INTO public."Employees"`.
8. **A3's rejection, on argument (ii).** I attacked the new arguments as instructed. A3(ii) holds:
   `patterns-backend.md` §*"A statutory string is DATA WITH PROVENANCE, never a label"* governs this
   at a level of generality that covers an instrument stronger than a printed notice, and an i18n
   catalog has no provenance column, so D4's `LegalNoticeReviewStatus` gate would be unimplementable.
   I could not break it. A3(i) is a frontend/release-management argument and I leave it to that lane.

---

## Ordering for the lead

- **Pre-ratification (each changes a schema decision, not an amendment):** **CH-S2-4** (does
  `Sequence` exist at all — it decides D1.4's whole mechanism), **CH-S2-2** (which columns lead the
  coverage index), **CH-S2-6** (the `OccurredAt` invariant and whether coverage is stable over time).
- **Ruled together:** **CH-S2-3 and CH-S2-4.** If `Sequence` goes, CH-S2-3 evaporates entirely — no
  raw SQL, no auto-commit, no `NULLS NOT DISTINCT` argument, no lost-append race. Ruling on the
  *mechanism* without ruling on the *column* is how the seven costs in CH-S2-4's table get paid
  one at a time.
- **Pre-ratification, but a check rather than a column:** **CH-S2-1** and **CH-S2-5** — both are
  additions to §How-a-reviewer-verifies, but both are load-bearing for D6, which is the decision rev 2
  is being ratified on. §V.10's own bar was *"§How-a-reviewer-verifies contains a check for the two
  properties this ADR is named after."* Rev 2 added those two; it did not add one for the property
  rev 2 itself introduced.
- **Pre-merge of ticket 1:** CH-S2-7's four costs (the two auto-indexes, the 63-byte truncation, "no
  navigation to `AgreementVersionText`", the first-of-class note in D12), CH-S2-8's CLR-type statement.
- **Routed out:** CH-S2-9 (backend lane) · `CLAUDE.md`'s "Bi-weekly pay cycle" (docs lane) · the
  as-of-date reproducibility question in CH-S2-6, if the owner's answer to `Q-SELFBILL-02` turns out
  to need it.

**No catalog edit is proposed.** Every rule I invoked already exists and already governs:
`consistency.md` §T-0540 (plan claims are pinned by `EXPLAIN` on the captured statement; the assertion
is the `Index Cond`), §ADR-0038 (a) (self-committing writes carry a sanctioned-exception doc-comment),
§"Tenant-scoped unique indexes" (`NULLS NOT DISTINCT` is decided by the index's job), §ADR-0037's
deviating form (a second `RuleFor` chain). The routing test in `conventions.md` therefore does not
fire — there is nothing to ratify, only rules to apply.

I did not write any repair, did not touch `src/`, did not amend the ADR, and created and ran no
migration. The probe container was removed.
