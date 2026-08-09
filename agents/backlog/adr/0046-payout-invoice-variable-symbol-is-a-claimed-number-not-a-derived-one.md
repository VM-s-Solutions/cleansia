# ADR-0046 — The payout invoice's **variabilní symbol** is a number the platform **claims**, never one it **derives**: a durable year-scoped counter allocated **before** the row exists, stamped through a **required constructor parameter** so a new creation path cannot compile without one, unique in **one global namespace** whose existing filtered index survives byte-for-byte, printed **unconditionally** so its absence is visible on the document, and — when a duplicate is somehow attempted — surfaced as a **business result**, never as a post-commit exception

- **Status:** `accepted` — 2026-08-09. Drafted 2026-08-08 by the `architect` in **author** mode;
  challenged 2026-08-08; adjudicated 2026-08-09 by the panel **lead** (outcome **REVISE**, closed list
  R1–R17); transcribed by the **author** 2026-08-09 and accepted.
- **Date:** 2026-08-08 (drafted) · 2026-08-09 (accepted)
- **Supersedes:** nothing accepted. **Retires in practice:** **T-0244** (*"`GenerateVariableSymbol`
  — replace per-process `GetHashCode` with a deterministic stable hash"*, `INDEX.md:2080` **done ✅**).
  T-0244's *finding* was right and is upheld more strongly here; its *remedy* — a better hash — is the
  thing this ADR rules out. See §D9.
- **Consumes / must not contradict:** ADR-0002 D2.2 + ADR-0010 + ADR-0023 (queue-consumer claim
  ordering — §D6.2 states the one named exception it needs), ADR-0034 (payout details; the bank block
  the symbol sits beside), ADR-0038 (*"post-persist means post-commit"* — §D2.3 is the same rule
  applied to a document instead of an FK), ADR-0041 (self-billing: Cleansia issues this document on
  the cleaner's behalf, which is **why** the reference is Cleansia's to allocate), **T-0522**
  (`in_review` — `T-0522-rebuild-the-payout-invoice-to-the-owners-specimen.md:4`; the rebuilt document
  this ADR prints onto — §D5.6 is the interaction, and it is **not** what the draft claimed).
- **Applies to:** see §Applies-to below (rewritten at acceptance per R13 — the draft's version was
  wrong on the migration, the call-site count, the admin surface and the locale keys).
- **Living doc:** `agents/architecture/decisions/payout-invoice-references.md` (new, written at
  acceptance).
- **Role card:** `agents/knowledge/roles/payout-reference-allocator.md` (new, written at acceptance).
- **Owner questions this ADR raises and does NOT answer:** two, quoted verbatim in §D8. **They are in
  this file only** — the PM holds `questions/open.md` and files them. Q-VS-01 and Q-VS-03 are filed
  (`questions/open.md:2090`, `:2097`); **Q-VS-02 is not** — §D8 says so in place.

---

> ### ⚠️ Method declaration
>
> **1. No shell.** `Read` / `Glob` / `Grep` / `Write` only. **No `Bash`, no `git`, no test run, no
> database.** Nothing was compiled, executed or measured. Every fact below is read from a file at HEAD
> and cited at `file:line`. The collision figures are **arithmetic**, re-derived by hand, not
> measurements.
>
> **2. No claim is inherited.** The brief that commissioned this ADR was re-verified line by line
> before being used. **It is right on the load-bearing finding and wrong in two places**, both
> corrected in §Context — one of them (*"every row has `VariableSymbol = NULL`"*) matters to the
> backfill ruling, and one (*"the parties are currently printed the wrong way round"*) is stale by four
> days. The brief's own warning — *"I have been wrong about this field once already"* — is why they are
> stated rather than smoothed over.
> **What was NOT re-verified, and what it cost (R16):** the **T-0522 ticket status log** was quoted in
> the present tense without being re-derived from the tree. That is exactly what produced the §D5.6
> error CH-VS-1 caught — the draft asserted *"the invoice PDF path is down at HEAD"* from
> `T-0522-…md:203-206`, a sentence true on 2026-08-04 and never updated, and hung four sequencing
> statements plus its only owner-only-migration cost mitigation on it. The rule that closes this is now
> in the catalog: `conventions.md:217-243`, *"A claim about the tree cites the tree — never another
> artifact"* (T3-HUMAN, enforced by `deliberation.md` step 5). **Every `file:line` in this accepted
> file was re-opened and re-read on 2026-08-09**, after the verdict, and the citations that had moved
> are corrected in place.
>
> **3. The collision figures were re-derived, not copied.** §Context §3 shows the arithmetic and the
> model's assumption. All four rows reproduce, and so do the two figures §D9-A now carries.
>
> **4. No legal claim is made.** Whether a Czech *variabilní symbol* is statutorily ≤10 numeric digits
> is **not** asserted here; what is asserted is that the *platform* encodes that constraint in four
> places. The legal confirmation is an owner question (§D8, Q-VS-01), per T-0508 AC14 (*"no agent
> asserts a tax-law requirement"*).

---

## Context

### 1. What is true at HEAD

**The finding stands, verified independently — and re-verified at acceptance.**

| Claim | Verified at | Verdict |
|---|---|---|
| `SetVariableSymbol` / `GenerateVariableSymbol` have zero production callers | `EmployeeInvoice.cs:212`, `:340`; repo-wide grep returns only `PayoutInvoicePdfDataTests.cs:140,154`, `EmployeeInvoiceEntityTests.cs:125-161` | ✅ **true** |
| The PDF renders the field only when non-empty | `DefaultInvoiceLayoutBuilder.cs:181-182` — inside `if (!string.IsNullOrWhiteSpace(data.VariableSymbol))` | ✅ **true** |
| `PaymentReference` is rendered by no layout | declared `InvoicePdfData.cs:8`, mapped `FileExtensions.cs:40`, and read by **nothing** in `Pdf/Layouts/*` — the only other hit in the whole PDF surface is a *test fixture* setting it (`PayoutInvoiceLayoutTests.cs:294`) | ✅ **true** |
| A collision hits the index after the handler returns | `UnitOfWorkPipelineBehavior.cs:20-30` — the handler runs, *then* `CommitAsync`. No `catch` anywhere on this path | ✅ **true** |
| `IX_EmployeeInvoices_VariableSymbol` is UNIQUE on the bare column, filtered `IS NOT NULL` — genuinely enforcing, not the tenancy trap | `EmployeeInvoiceEntityConfiguration.cs:116-118`; `Initial.cs:2654-2659` | ✅ **true** |

> **Citation drift corrected at acceptance.** The draft cited the index at `Initial.cs:2650-2655`; at
> HEAD that range is `IX_EmployeeInvoices_TenantId`. The variable-symbol index is
> **`Initial.cs:2654-2659`**. Likewise the column moved: `character varying(10)` is at
> **`Initial.cs:1522`**, not `:1518` (which is now `ApprovedAt`). Both are re-read, not inferred.

**So the payout invoice carries no payment reference of any kind, and "mark this invoice paid"
(`MarkInvoicePaid.cs`) records a claim nothing can reconcile.** That is the problem this ADR closes.

### 2. Two corrections to the brief — one of them changes an answer

**(a) It is NOT true that every `EmployeeInvoices` row has `VariableSymbol = NULL`.** Two standalone
SQL scripts insert rows *with* hand-authored symbols:
`src/Cleansia.Infra.Scripts/SeedData/insert_employee_invoices.sql:10` lists `"VariableSymbol"` in its
column list and supplies eight literals — `'0321876543'`, `'0322987654'`, `'0323098765'`,
`'0324109876'`, `'0325210987'`, `'0321987654'`, `'0321765432'`, `'0322876543'` (`:22,33,46,57,68,79,92,105`);
`insert_employee_payroll.sql:199` carries the same column list.

Three things follow, and they are not cosmetic:
- **Nothing in the repo references either file.** A repo-wide grep on both filenames returns only the
  files themselves. `Cleansia.Infra.Scripts.csproj:10-17` copies **only** `insert_seed_data.sql`, and
  that file contains **no** `EmployeeInvoices` insert. They are hand-run scripts.
- **Every one of those eight literals begins with `0`** — and §D1 rules a leading zero out, for a
  reason that is about money and not about taste. They are a loaded gun in a repo where the owner runs
  SQL by hand.
- The precise, defensible statement is therefore: **no production code path has ever written a
  variable symbol**, so every invoice created by `GenerateInvoice` or the pay-period job carries NULL.

**(b) *"the invoice is also being rebuilt (the parties are currently printed the wrong way round)"* is
stale.** That landed in `8ca77412`. At HEAD `DefaultInvoiceLayoutBuilder.cs:11-15` states the direction
in its own doc comment (*"the cleaner is the SUPPLIER and Cleansia is the CUSTOMER"*), `InvoicePdfData
.Supplier` is the cleaner and `.Company` is the *Odběratel* (`InvoicePdfData.cs:29-33,36-39`), and
T-0522 is `in_review` (`T-0522-…md:4`) with AC0–AC15 checked. **What the draft added here was itself
wrong** — it claimed T-0522 carries a live pending migration. It does not; see §D5.6, rewritten.

### 3. Why the existing generator cannot be switched on — re-derived

`EmployeeInvoice.cs:340-345`:

```csharp
public static string GenerateVariableSymbol(string employeeId, string payPeriodId)
{
    var empHash    = StableHash(employeeId)  % 10000;    // formatted D4
    var periodHash = StableHash(payPeriodId) % 1000000;  // formatted D6
    return $"{empHash:D4}{periodHash:D6}";
}
```

**Within one pay period `periodHash` is a constant**, so two cleaners are distinguished only by
`empHash ∈ [0, 9999]` — 10 000 buckets. Under the standard birthday model with a uniform bucket
distribution, the probability of at least one collision among `n` cleaners is

```
p(n) = 1 − Π(k=0..n−1) (1 − k/10000)  ≈  1 − exp(−n(n−1)/20000)
```

| n | n(n−1)/2 | exponent | 1 − e^(−x) | brief said |
|---|---|---|---|---|
| 25 | 300 | 0.0300 | **2.96 %** | 3 % ✅ |
| 50 | 1 225 | 0.1225 | **11.53 %** | 11.5 % ✅ |
| 100 | 4 950 | 0.4950 | **39.04 %** | 39 % ✅ |
| 150 | 11 175 | 1.1175 | **67.29 %** | 67 % ✅ |

**All four re-derive.** The assumption is that FNV-1a-32 mod 10 000 is uniform; **if it is not, the
real figure is higher, never lower** — non-uniformity concentrates mass and increases collisions. So
the table is a **lower bound**, which is the direction that matters.

Two further properties of that generator, neither of which is in the brief:

- **It emits a leading zero about one time in ten.** `empHash < 1000` ⇒ `empHash:D4` starts with `'0'`.
  The seed literals in §2(a) are exactly that shape. This matters: the column is `character varying(10)`
  (`Initial.cs:1522`), so `'0321876543'` and `'321876543'` are **different strings** to the unique index
  and to `EmployeeInvoiceRepository.GetByVariableSymbolAsync` (`:20-28`), while a bank form treats a
  variable symbol as a number and drops the zero. The printed reference and the transferred reference
  would then not be the same string — on a document whose entire job is to make them the same string.
- **No test exercises the failure mode.** `EmployeeInvoiceEntityTests.GenerateVariableSymbol_Differs_Across_Periods_For_Same_Employee`
  (`:140-147`) varies the **period**, which is the axis that is already fine. There is no
  same-period-two-employees test anywhere.

### 4. The three creation paths, and what a failure does to each

| # | Path | Commit | Today's failure behaviour |
|---|---|---|---|
| 1 | `AdminPayrollController.GenerateInvoice` (`:61`) → `GenerateInvoice.Handler` (`GenerateInvoice.cs:87`) | pipeline, per command (`UnitOfWorkPipelineBehavior.cs:29`) | unhandled `DbUpdateException` → 500 |
| 2 | `GenerateInvoiceHandler` queue consumer (`Functions.Core/Handlers/GenerateInvoiceHandler.cs:63`) → the same command | same | throw → retry → poison after `maxDequeueCount` |
| 3 | `PayPeriodBackgroundService.GenerateInvoiceForEmployeeAsync` (`:328`) | **once per tenant group**, at `:187`, *after every employee in the group* | one bad row fails the **whole group's** `SaveChanges` |

Path 3 is worse than "a failed batch". `SendPeriodClosedEmailsAsync` **emails each cleaner their PDF
inside the loop** (`:262-272`) and uploads the blob (`:359`), both **before** the `:187` commit. So a
single duplicate today means: every cleaner in the tenant group has received an invoice PDF by email,
and **no invoice row exists for any of them**. §D2.3 and §D6.3 close that, and it is closed by the same
ordering rule either way.

---

## Decision

### D1 — What the symbol **is**

> **`VariableSymbol = YYYY · NNNNNN` — exactly ten digits, where `YYYY` is the four-digit UTC calendar
> year of allocation and `NNNNNN` is a per-year contiguous ordinal starting at `1`, zero-padded to six.
> First produced value: `2026000001`.**

Properties, each of which is a requirement and not a preference:

1. **Numeric, ten characters.** Fits `character varying(10)` (`Initial.cs:1522`) exactly and uses the
   whole budget once, forever — the width never changes, so a transcription that dropped a digit is
   detectable by length alone.
2. **The first digit is never `0`.** `YYYY ≥ 2026`, so the leading digit is `2` for the next ~7 900
   years. This is the §3 hazard closed by construction: printed string ≡ stored string ≡ what a bank
   form shows.
3. **Self-describing on a bank statement, and the prefix is the year of ALLOCATION — not the accounting
   year of the work.** The owner reconciles by eye against a statement; a year-prefixed reference sorts
   and scans. This is the one place a format preference earned its way in. **State the consequence
   rather than discover it: a December pay period closed on 2 January produces `2027…`,** because the
   counter is keyed on the moment the number is claimed, not on the period it pays for.
   *The alternative, recorded and not taken:* key the counter on the year of `PayPeriod.EndDate`. Its
   cost is concrete — `GenerateInvoice.Handler` holds only `PayPeriodId` (`GenerateInvoice.cs:87-91`)
   and never loads the period, so this buys a `PayPeriod` load on the admin path purely to name the
   reference. **Q-VS-01's answer may move this** (an accountant may require the reference to sit in the
   accounting year); it is recorded here so that move is a one-line change with its cost already
   priced, and it opens **no new owner question**.
4. **Capacity 999 999 payout invoices per calendar year.** At one invoice per cleaner per period this
   is not a bound anyone will meet.
5. **No wrap, ever — and the cap is in the SQL, not in a sentence.** The allocating statement carries
   `WHERE "Value" < 999999` (§D2.1), so the row **stops at the cap** instead of running away past it.
   A wrap would be a silent duplicate, which is the failure this whole ADR exists to prevent; a counter
   that ran past the cap and then formatted to seven digits would be the same failure one step later,
   and would need a manual `UPDATE` on a poisoned counter to repair. With the `WHERE` in place the
   exhausted state is **repaired by the year rolling over**. The empty `RETURNING` that the `WHERE`
   produces is a named business error, not an index crash — see §D2.1.
6. **It is not the invoice number and cannot become it.** The owner ruled *"VS can't equal the invoice
   number. These are 2 different and there is a separate property for it"* (T-0522 AC4, corrected
   2026-08-03). `InvoiceNumber` is `INV-yyyyMM-XXXXX` (`EmployeeInvoice.cs:111`) — non-numeric, so the
   two can never coincide even by accident.

**Why nothing derived can work — the argument, corrected. The principle is STATELESSNESS, not bit
width.**

> Ten decimal digits is a **dense** codomain: 10¹⁰ points, all of them reachable. `EmployeeId` and
> `PayPeriodId` are `Ulid.NewUlid()` values — 130-bit, sparse, and drawn by the id generator, not
> chosen by the platform. A function of those two values **alone** is therefore fixed *before* the
> realized set exists and cannot be chosen to be injective on it; its collision probability is strictly
> positive and grows monotonically with the row count, at every width that fits in ten digits.
> Injectivity into ten digits requires at least one input drawn from a **dense** identifier space — an
> ordinal that somebody assigned. The platform holds no such identifier for an employee or for a pay
> period, so it must **introduce** one, and introducing one is allocation. **§D9-I is not a
> counterexample to "allocate" — it is an instance of it that allocates twice**, which is exactly why
> §D9 rejects it on cost and not on impossibility.

Three notes a future reader needs, because the draft got this wrong and the wrongness was subtle:

- **The pigeonhole version of this argument is unsound and has been deleted.** *"26-character ULIDs are
  ≈260 bits, ten digits are ≈33.2 bits, therefore no function is injective"* counts correctly
  (`EmployeeInvoiceEntityConfiguration.cs:13-19` confirms `HasMaxLength(26)` on both id columns;
  `log₂(10¹⁰) = 33.219`) and proves the wrong thing. Pigeonhole rules out injectivity **on the full
  type domain**; injectivity is only ever *required* on the **realized** set, which is small. §D9-I is
  a derivation that is injective on the realized set — so a §D1 that forecloses "derived" by
  pigeonhole contradicts §D9 two pages later.
- **The operative property is density, not authorship.** An **externally** assigned dense identifier
  would serve equally well — a cleaner's registration number, a bank-assigned id. Allocation is how you
  *obtain* density when nothing dense is in hand, which is this platform's situation. That is the
  honest reason, and it is narrower than "the platform must assign it".
- **Do not cite the `(EmployeeId, PayPeriodId)` unique index** (`EmployeeInvoiceEntityConfiguration.cs
  :123-124`) as if it makes anything injective. It does not. It bounds `|realized set|` at
  (#cleaners × #periods) — one invoice per employee-period — and that is all. It is the *premise* of
  the counterexample, not the counterexample.

The draft's closing clause *"so this is settled and not re-litigated"* is **deleted**. An ADR earns
non-re-litigation by carrying an argument that survives being checked; foreclosing with an unsound
lemma is worse than not foreclosing, because the next author cites §D1 verbatim to reject a design that
is fine, reads §D9-I, and stops trusting the document.

### D2 — Where it is assigned, and what happens if assignment fails

**D2.1 — The allocator.** A durable counter row, allocated by one atomic statement, copying the shape
already shipped and reviewed in `FiscalCounterRepository.AllocateNextAsync` (`:33-42`):

```sql
INSERT INTO "PayoutReferenceCounters" ("Id", "Year", "Value", "CreatedBy", "CreatedOn")
VALUES (@id, @year, 1, @actor, @now)
ON CONFLICT ("Year")
DO UPDATE SET "Value"     = "PayoutReferenceCounters"."Value" + 1,
              "UpdatedBy" = @actor,
              "UpdatedOn" = @now
WHERE "PayoutReferenceCounters"."Value" < 999999
RETURNING "Value";
```

- Postgres takes a row lock on the conflicting tuple, so concurrent allocations serialize and each
  `RETURNING` reports a distinct value — the property `FiscalCounterRepository.cs:26-32` documents.
- The nullable-parameter lesson from that file (`:49-53`) does not arise, because **there is no tenant
  parameter** (§D3.2) — and, now, **no scope parameter either**.
- **The key is `(Year)` and nothing else. There is no `Scope` column.** The entity carries `Year`
  (`int`, **non-nullable**) and `Value`, and one UNIQUE index on `(Year)` alone. Two reasons, and the
  first is the decisive one:
  - **This ADR cannot name a second value for a scope.** §D3.1 decides that there is exactly one
    namespace; a scope column is a discriminator over a set with one member, sitting *inside the
    `ON CONFLICT` arbiter* of the decision that says the set has one member. It is in tension with the
    decision it would sit under.
  - **A non-nullable `int` key cannot reproduce the nulls-distinct collapse.** The trap is documented
    *in the file this statement copies*: `FiscalCounterRepository.cs:30-32` records that
    `IX_FiscalCounters_Tenant_Year_IssuerScope` needs `.AreNullsDistinct(false)`
    (`FiscalCounterEntityConfiguration.cs:26-29`) precisely because a null in the arbiter otherwise
    inserts a duplicate row per call. With `(Year)` as the whole key there is no nullable column in the
    arbiter, so **no `.AreNullsDistinct(false)` retrofit is in play at all.**
  - **What the precedent would otherwise invite.** `FiscalCounter.cs:13-18` explicitly trains the
    reader that the scope string *"is the extension point"* and binds *"NOT merely the tenant"* — so
    `Scope = tenantId` is the reading a developer copying this file would reach for, and it would
    silently re-introduce the tenant term §D3.2 forbids. Removing the column removes the reading.
  - **If Q-VS-03 ever forces per-tenant namespaces**, the tenant term is added *then*, in the same
    owner-only migration that replaces the `EmployeeInvoices` index — which §D3.2 already writes down.
    It is not pre-provisioned by an unused column.
- **A new table, not `FiscalCounters`.** Three reasons, in order of weight: (i) `FiscalCounter`'s key
  is `(TenantId, Year, IssuerScope)` (`FiscalCounterEntityConfiguration.cs:26-29`) and its repository
  reads the ambient tenant internally (`FiscalCounterRepository.cs:19`) — a payout counter **must not**
  be tenant-keyed (§D3.2), and bending the fiscal allocator to allow that would edit the fiscal money
  path to serve payroll; (ii) `FiscalCounter`'s entire contract is *gapless*-monotonic for CZ EET / DE
  TSE / AT RKSV (`FiscalCounter.cs:7-23`) and **this counter is deliberately gappy** (§D2.4) — putting
  a gappy scope in a table whose doc-comment promises gaplessness is a trap for the next reader; (iii)
  a payout reference is not a fiscal artifact and must not appear in a fiscal counter export.
- The counter entity is **tenant-global** — it does **not** implement `ITenantEntity`. §D3.2 is why.

**The `WHERE` has a return-shape consequence that must not be copied wrong.** When the guard is false,
the `DO UPDATE` affects no row and **`RETURNING` yields nothing**. `FiscalCounterRepository.cs:63`
reads `return allocated[0];` — an unguarded index into the result list — and copying that shape here is
the defect, because at the cap it throws `ArgumentOutOfRangeException` from inside a repository. The
empty result maps to a named business error:
**`BusinessErrorMessage.InvoiceReferenceCapacityExhausted = "payroll.invoice.reference_capacity_exhausted"`**.
*Runbook line:* within a year there is no remedy but the year — the cap is not raisable without
widening the column, and the column is the whole design.

**The allocator MUST NOT be called inside an explicit transaction.** This is an invariant, not a
preference, and both of §D2.4's properties depend on it:

- **The gap semantics.** They exist only because the statement auto-commits; joined to an ambient
  transaction it would roll back with the invoice, and the "never duplicates" guarantee would become
  "never duplicates unless the caller opened a transaction".
- **The lock duration.** `ON CONFLICT … DO UPDATE` takes a row lock on the **single** counter row.
  Under a long-lived transaction that one row serializes every concurrent payroll run for the life of
  the transaction — a global contention channel that a design which never mentions locking would
  introduce silently.

**Catalog obligation, discharged with this ADR.** This is the codebase's **second** self-committing
write inside a handler. `consistency.md:346-353` defines the deviating form as *"a self-committing
write inside a handler **with no sanctioned-exception doc-comment**"* and names
`PromoCodeRepository.TryIncrementGlobalRedemptionsAsync` as the one exception *"because it says so, not
because it exists"*; `patterns-backend.md:641-644` scopes the ADR-0038 law identically. So this ADR
**mandates** the sanctioned-exception doc-comment on the allocator, in the `PromoCodeRepository.cs:28-38`
shape (state that it auto-commits, that this is intentional and required, and what it does *not* roll
back), and the implementing change adds the allocator to `consistency.md`'s named-exception list as the
second entry. A doc-comment nobody wrote is what turns a sanctioned exception into a violation.

**D2.2 — The stamp is a required constructor parameter, and that is the whole answer to "both paths".**

`EmployeeInvoice.Create` and `EmployeeInvoice.CreateFromOrderPays` take `string variableSymbol` as a
**required, non-defaulted** parameter. `SetVariableSymbol` and `GenerateVariableSymbol` are **deleted**.

This is not a style choice; it is the mechanism. T-0522 established the precedent on this exact
document and stated the reasoning in its own Review: *"The new `payoutDetails` parameter
is **required, not defaulted**, so a future third call site is a compile error rather than three silent
`—`s: that is the exact defect this AC existed to close, and a default would have re-armed it."*
The catalog carries the same rule as a corollary at `patterns-backend.md:464-467`. A validator, a
convention, or a `SetVariableSymbol` call the author must remember are all things that were already
available and all things that produced today's state.

Production call sites after the change: `GenerateInvoice.cs:87` and `PayPeriodBackgroundService.cs:328`.
A third production path does not compile. **Twelve fixture call sites also gain the parameter** — see
§D7, where the census is exact and where the follow-on obligation it creates is discharged.

**D2.3 — Ordering, stated as an invariant a reviewer can check.**

> **The row that owns a reference is committed before any document carrying it is generated, uploaded
> or delivered.**

Sequence: **allocate → construct → add → commit → render → upload → deliver.** This is ADR-0038's
*"post-persist means POST-COMMIT"* applied to a document instead of a foreign key
(`patterns-backend.md:633`).

**The primitive must be stated before the invariant, or "commit inside the loop" means the wrong
thing.** `IUnitOfWork.CommitAsync` → `BaseRepository.CommitAsync` (`:171-174`) →
`CleansiaDbContext.CommitAsync` (`:67-100`) ends in a **context-wide** `SaveChangesAsync` (`:99`).
**There is no per-entity commit in this codebase.** So a commit inside the employee loop does not mean
"commit this employee's changes" — it means "commit everything the change tracker holds, including
whatever the two enclosing loops mutated". Every consequence below follows from that one sentence.

- Paths 1 and 2 already satisfy the invariant: the PDF is not produced by `GenerateInvoice.Handler` at
  all.
- **Path 3 violates it today** (§Context §4) and must change. The batch takes **two named commits per
  employee**, and the ADR states what each carries:

  - **C1** — after `_employeeInvoiceRepository.Add(invoice)` and the `AssignToInvoice` loop
    (`PayPeriodBackgroundService.cs:334-339`), **before** `GenerateInvoicePdfAsync` (`:352`). It makes
    the reference durable before any document carrying it exists. **It also persists `period.Close()`**
    (`:148`, one loop level up, outside the employee loop at `:219`) — so the period close becomes
    durable at the **first invoicing employee** instead of at the group commit `:187`. That is a
    behaviour change and it is named here rather than discovered in production.
  - **C2** — after `SetPdfBlobUrl` / `ClearPdfGenerationError` (`:360-361`) **and** after
    `SetPdfGenerationError` in the catch (`:371`). Without C2 those three mutations ride the next
    employee's commit or the group commit at `:187`, and the PDF-URL/error state for the employee whose
    generation just failed is lost with whatever fails next.

  Both commits stay under the tenant override set at `PayPeriodBackgroundService.cs:138-142`, so rows
  are stamped with the right `TenantId` — CLAUDE.md's reference shape (*"commit **inside** the loop"*,
  `CleanupStalePendingOrders.cs:76-119`).

- **On a failed C1** — the only commit that can raise `23505` on `VariableSymbol` — call `Rollback()`
  (`BaseRepository.cs:181-184` → `CleansiaDbContext.cs:107-113`) at that call site, and understand that
  **it is context-global**: `CleansiaDbContext.Rollback()` sets **every** tracked entry to `Unchanged`,
  not this employee's. Its scope at that instant, under C1/C2, is: this employee's `Added` invoice and
  `Modified` order-pays, **plus `period.Close()` if and only if no earlier employee in this period has
  already committed**. Then continue the loop. `RefundService.cs:101-103` is the shipped precedent for
  the catch-and-`Rollback()` pair.

The alternative shape — buffering the emails and sending them after the group commit — is correct and
rejected: it holds every PDF in the group in memory and does not fix the ordering for the blob upload.

**D2.4 — What happens if assignment fails.**

- **The allocation is not rolled back with the invoice, by design — and the mechanism must be stated
  correctly, because the file this design copies says the opposite for the same statement.**
  `SqlQueryRaw` runs on the context's connection and **joins an ambient transaction if one is open** —
  `FiscalCounterRepository.cs:28-30` documents exactly that (*"Running through the context's connection
  joins the caller's open transaction … bound to the same commit/rollback"*). It auto-commits **here**
  because **no payout path opens one**: `UnitOfWorkPipelineBehavior.cs:13-33` opens none, and
  `PayPeriodBackgroundService.CloseExpiredPeriodsAndOpenNewAsync` (`:107-197`) opens none. That is a
  **caller property, not an API property** — which is why "MUST NOT be called inside an explicit
  transaction" is an invariant in §D2.1 and a reviewer check, rather than an assumption.
  The intent matches `PromoCodeRepository.cs:33-38`'s declared exception — *"ExecuteUpdateAsync issues
  SQL and auto-commits immediately… That is intentional and REQUIRED for atomicity."*
- **Therefore: gaps happen, and gaps are not a defect.** A variable symbol is a payment reference, not
  a fiscal document number. **Nothing in this platform requires it to be gapless** — that requirement
  belongs to `FiscalCounter` and to receipts, and is one of the three reasons §D2.1 keeps the two
  apart. **A design that never gaps and sometimes duplicates is strictly worse than one that sometimes
  gaps and never duplicates**, because a gap costs nothing and a duplicate costs a mis-reconciled
  transfer.
- **Path 1 (admin HTTP):** the allocator throws → the handler throws → the pipeline never commits →
  **no invoice exists**. The admin sees a failure and clicks again. The `OrderEmployeePay` rows stay
  unassigned (`GenerateInvoice.cs:95-98` never ran), so a retry is clean and produces no orphan.
- **Path 2 (queue):** the throw propagates out of `GenerateInvoiceHandler.HandleAsync`; the queue
  retries under `maxDequeueCount` and poisons after. That is the handler's documented infra-failure
  lane (`GenerateInvoiceHandler.cs:22-23`) and it is correct: an allocator failure is transient.
- **Path 3 (batch):** the allocation sits inside `GenerateInvoiceForEmployeeAsync`, which is already
  wrapped by the per-employee `try/catch` at `PayPeriodBackgroundService.cs:235-260`. One cleaner is
  skipped with a logged error, their period-closed email goes without an attachment (the existing
  degradation), **and the rest of the batch is invoiced** — with the one qualification §D6.3 states.
  No new code is needed for this beyond C1/C2 and the `Rollback()`.

### D3 — Uniqueness scope, stated precisely

**D3.1 — The namespace is global, and the existing index survives unchanged.**

`IX_EmployeeInvoices_VariableSymbol` — `UNIQUE` on the bare `VariableSymbol` column, filtered
`WHERE "VariableSymbol" IS NOT NULL` (`EmployeeInvoiceEntityConfiguration.cs:116-118`,
`Initial.cs:2654-2659`) — **is the right shape and is not touched.** Not one line of index DDL changes
on `EmployeeInvoices`.

Why not `(TenantId, VariableSymbol)`:

- **CLAUDE.md's own rule forbids it in the naive form.** *"A unique index that includes `TenantId`
  enforces nothing in single-tenant mode"* — `TenantId` is nullable, Postgres treats NULLs as DISTINCT,
  and null is production today. A `TenantId`-leading index would need `.AreNullsDistinct(false)` to
  enforce anything at all. The bare-column index has no such hole and needs no such retrofit.
- **The requirement is about a human and a bank statement.** The reference exists so that one line on
  one statement maps to exactly one invoice. The payer's account is one account. A namespace at least
  as wide as the payer is mandatory; **global is unconditionally at least that wide.**

**D3.2 — Consequence: the counter is global too. This is the cheapest correct shape TODAY, and it is
contingent.**

The counter's key is **`(Year)` with no tenant term**, and the entity is **not** `ITenantEntity`
— the tenant-global lane ADR-0010 establishes (`:83`, `:146`, `:160`), not the tenant-scoped default.

**Why this shape and not another, stated without overclaiming.** The draft said *"this is forced, not
chosen"*. It is not forced — it is the **cheapest correct shape today**, for three reasons that are all
facts about the present tree:

- the shipped index is **already global** (`EmployeeInvoiceEntityConfiguration.cs:116-118`), so this
  design costs zero index DDL where the alternative costs an owner-only migration;
- it has **no NULLS-DISTINCT hole**, because it carries no `TenantId` — the retrofit CLAUDE.md warns
  about is simply not in play;
- production is **single-tenant**, so the tenant term would today discriminate nothing.

A tenant-keyed counter *under the current globally-unique index* would additionally be incoherent —
tenant A and tenant B both allocate ordinal `1` in 2026, both produce `2026000001`, and the second
insert is rejected by the index, turning a tenancy fact into a 500 on the payroll path. But that is an
argument about the *pair*, not a proof that the global half is the one that must win.

**It is contingent on D3.1's premise — *"the payer's account is one account"* — and that premise is
exactly what `Q-VS-03` asks the owner** (`questions/open.md:2097`). You cannot ask the owner whether a
premise holds and call the conclusion *forced* in the same document. **This must be re-examined the
moment that premise stops holding.**

**The cost, named rather than discovered later.** Under activated multi-tenancy (ADR-0028 is an
*activation pack*, not a live state), a tenant admin can infer platform-wide payout-invoice volume from
the gaps between their own consecutive symbols. This is accepted: it is a low-severity business-volume
inference, and it is noisy (failed commits gap the sequence too, §D2.4). **If it is ever worth
flipping, the change is bounded and written down:** add a tenant term to the counter key, and replace
the index with `(TenantId, VariableSymbol) UNIQUE … NULLS NOT DISTINCT WHERE VariableSymbol IS NOT NULL`
— which, per CLAUDE.md, is an owner-only `ef-migration` that fails on pre-existing duplicates, so it
must be done while the set is small or empty.

### D4 — Backfill

**D4.1 — No automatic backfill. Ever.** An invoice's PDF is the artifact the number exists for. A
symbol written onto a row whose stored PDF does not print it creates a reference that renders as
authoritative on three surfaces — admin web (`invoice-detail.component.html:151`), partner web
(`invoice-detail.component.html:93`), and the iOS partner "References" card
(`InvoiceDetailContent.swift:182-184`) — and appears on **no document.** That is strictly worse than
NULL, because NULL is honestly empty.

**D4.2 — A `Paid` invoice never receives one.** The brief forbids reassignment after printing; a
*first* assignment after a transfer has already left the bank is the same hazard wearing a different
hat. The owner's bank record references nothing, and the platform would now claim a reference that was
never on the transfer.

**D4.3 — An unpaid, uncancelled, symbol-less invoice may receive one exactly once**, through an
explicit admin command — never a migration, never a sweep. Gate: `VariableSymbol IS NULL` **and**
`Status ∈ {Pending, Approved, Disputed}` **and** `IsCancelled = false`. The command:

1. allocates from the same counter (§D2.1),
2. stamps the row and **commits**,
3. then regenerates the PDF through the existing `RegenerateInvoicePdf` path, which overwrites the same
   blob name (`RegenerateInvoicePdf.cs:137`) so the regenerated document *is* the document.

**The order is load-bearing.** If step 3 fails, the row keeps its number and step 3 is re-runnable and
idempotent — it re-renders the *same* stored symbol. The reverse order would put a number on a document
and not on the row, which is D4.1's failure inverted.

**Is this reassignment?** No. It is a **first** assignment to a row that has never had one, on an
invoice against which no money has moved. The safety argument is exactly that pair of conditions, and
both are in the gate.

**D4.4 — `MarkInvoicePaid` REFUSES a null-symbol invoice.**

*(This replaces the draft's D4.4 entirely. The draft made `BankTransferNote` **required** when the
symbol is null, as a compensating record. That decision is deleted — it could not do the job. See
"why the compensating-record form was deleted" below; the finding is CH-VS-3.)*

`MarkInvoicePaid` gains one rule: an invoice whose `VariableSymbol` is null cannot be marked paid.
New key **`BusinessErrorMessage.InvoiceReferenceMissing = "payroll.invoice.reference_missing"`**, whose message
names the remedy — *"Assign a payment reference before recording the transfer."* The remedy is D4.3's
assign-and-regenerate command, reachable from the same screen. The rule states forward what the whole
ADR is for: **you do not record a transfer against a document that carries no reference.**

**Placement is load-bearing and is specified, not left to the implementer.** The new rule joins the
**existing `InvoiceId` `Cascade.Stop` chain** (`MarkInvoicePaid.cs:40-51`), **after `ApprovedAsync`**
(`:50-51`) — *not* as a new root `RuleFor`. Read the file before writing it: the chain runs
`NotEmpty` → `ExistsAsync` → `NotAlreadyPaidAsync` → `NotCancelledAsync` → `ApprovedAsync`, and each of
the three predicates dereferences `invoice!` (`:65`, `:71`, `:77`) — safe **only** because
`ExistsAsync` gated them inside a `Cascade.Stop` chain. FluentValidation's class-level default is
`Continue`, so a new root rule would run **regardless** of the id being valid and would
`GetByIdAsync(...)!.VariableSymbol` on `null` for a bad id — a `NullReferenceException` where a
`invoice.not_found` belongs.

**`BankTransferNote` stays exactly as it is.** Optional; `varchar(500)`
(`EmployeeInvoiceEntityConfiguration.cs:83-84`); root rule `MaximumLength(500)` unchanged
(`MarkInvoicePaid.cs:53-55`); display-only in admin (`invoice-detail.component.html:268-275`). **This
ADR does not make it mandatory and does not claim it as a control.**

**Why the compensating-record form was deleted — four legs, each verified:**

1. **It cannot reach the population it was written for.** `MarkAsPaid` throws unless the invoice is
   `Approved` (`EmployeeInvoice.cs:254-257`) and `MarkInvoicePaid` refuses an already-`Paid` invoice in
   three separate places (`:46-47` in the chain, `:24-30` in `RefusalFor`, `:93-98` in the handler).
   There is **no path** by which an already-paid invoice receives a note.
2. **Its eligible set is therefore a strict subset of D4.3's** — every invoice it could act on is one
   D4.3 can simply give a real reference to. A weaker control over a subset of a stronger control's
   domain is not a second layer; it is noise.
3. **The mandatory form would fail 100 % of attempts against the shipped UI.**
   `invoice-detail.component.ts:106-108` calls `this.facade.markAsPaid()` **with no argument**, and
   `invoice-detail.facade.ts:79-88` assigns that absent parameter straight onto the command
   (`command.bankTransferNote = bankTransferNote;` at `:87`). The field ships `undefined` on every
   attempt. A required-note rule would have made "mark paid" unusable in admin on day one, and the
   note dialog it presumes was never scoped.
4. **Its placement was unsafe.** The existing `RuleFor(x => x.BankTransferNote)` (`:53-55`) is a
   **separate root rule** under the class-level `Continue` default; a `MustAsync` hung there would
   deref `null` on a bad id, exactly as described above.

**The residual, named honestly.** If the owner has **already** transferred money against a null-symbol
invoice, this refusal is an obstacle, and **the platform cannot detect that case** — nothing on the row
records a transfer that happened outside it. The stated path: assign a reference via D4.3 (the row
gains a reference the transfer did not quote — which is a true and useful record of *this invoice*,
not a claim about what was on the wire), and put the bank's own transaction id into the **optional**
`BankTransferNote` when marking it paid. **This routes to `Q-VS-02`, whose second leg already asks
it. No new owner question is opened.**

**D4.5 — The column stays nullable and the filter stays on the index.** D4.3 leaves a finite set that
may legitimately hold NULL until an admin works through it, and **I could not verify how many
`EmployeeInvoices` rows exist** — no shell, no database (§D8, Q-VS-02). `NOT NULL` is a later one-line
additive migration whose precondition is now written down: zero rows with a null symbol. That is a DB
Master call with a stated trigger, not an owner question.

**D4.6 — The two standalone seed scripts are fixed or deleted in the same change.**
`insert_employee_invoices.sql` and `insert_employee_payroll.sql` (§Context §2a) hand-author eight
symbols, every one of them leading-zero and none of them from the allocator. They are unreferenced, so
deleting them costs nothing; leaving them costs whatever the owner's next manual SQL run costs.

### D5 — What prints, and where

**D5.1 — The variable symbol prints unconditionally.** It moves out of the conditional block
(`DefaultInvoiceLayoutBuilder.cs:181-182`) into the unconditional one, beside `BankAccount` / `Iban` /
`Swift` (`:177-179`). A missing symbol then renders `—`, exactly as those already do
(`PdfComponentExtensions.cs:96`, `:132` — `value ?? "—"`).

**This is the change that would have caught the bug.** The conditional is what made *"no reference"*
indistinguishable from *"this document has no reference field"*. On a document whose purpose is to be
paid, the absence of the payment reference must be **loud**.

**The sequencing consequence, decided rather than absorbed.** The invoice PDF path **renders today**
(§D5.6). So landing D5.1 before any symbol exists makes every payout invoice print
`Variabilní symbol —`. **Ruling: that is correct and intended.** D5.1's own argument is that absence
must be loud, and a field that silently vanishes is precisely what produced this defect — a document
that admits it is missing its payment reference is more honest than one that hides it. Stated as the
consequence it is:

> **Between D5.1 landing and the first allocated symbol, every rendered payout invoice prints `—` for
> the variable symbol.**

**Constraint: D5.1 and D5.3 do not ship a release ahead of D2.2.** The window is a **deploy**, not a
sprint. There is no correctness reason to order them the other way, and every reason not to leave a
document in that state across a release boundary.

**D5.2 — The constant symbol stays conditional** (`:184-185`). The two are not symmetric and must not
be "made consistent". A *konstantní symbol* is legitimately absent outside CZ, and T-0522 ruled
explicitly that *"printing a guessed symbol is worse than omitting the field, which the layout already
does cleanly"* — SK is deliberately null. A *variabilní symbol* is **never** legitimately absent.

**D5.3 — `InvoicePdfData.PaymentReference` is deleted, along with the mapper line that fills it**
(`InvoicePdfData.cs:8`, `FileExtensions.cs:40`). It is not a working fallback:

- `FileExtensions.cs:40` reads `PaymentReference = invoice.PaymentReference ?? invoice.VariableSymbol`
  — literally a fallback expression. **Its only fallback is to the variable symbol, and it is
  unreachable**, because `Create` always sets the field (`EmployeeInvoice.cs:126`,
  `PaymentReference = invoiceNumber`) and `SetPaymentReference` (`:224-228`) has **no caller anywhere
  in the tree** (verified by repo-wide grep: one declaration, zero call sites). A `??` whose left side
  is never null is a comment, not a control.
- the invoice number is **already printed**, in the masthead (`DefaultInvoiceLayoutBuilder.cs:54`),
- and **no layout reads the field.**

Its existence on the model is precisely what made a second reference believable to a reader who did not
grep the layouts. **A payment document carries exactly one payment reference.** The only test that
touches it sets it by hand and asserts nothing about it (`PayoutInvoiceLayoutTests.cs:294`) — the same
anti-pattern as §D7, one artifact further down.

**D5.4 — The entity column and the DTO fields stay, for now, and their retirement is filed not
smuggled.** `EmployeeInvoice.PaymentReference` is on `EmployeeInvoiceDto:13` and
`EmployeeInvoiceDetailDto:14`, i.e. on the wire to three NSwag-generated clients, and is rendered by
the iOS partner app (`InvoiceDetailContent.swift:185-187`). Removing it is a `nswag-regen`
(owner-only). **Consequence to state rather than discover:** until that lands, the iOS References card
shows the invoice number twice under two labels. That is a cosmetic defect introduced by telling the
truth about the field, and it is the right order to fix them in.

**D5.5 — `EmployeeInvoice.SpecificSymbol` stays dead.** No production writer, no field on
`InvoicePdfData`, no layout. This ADR does **not** invent a use for it. It is named here only so that
a future reader does not "complete" it by symmetry with the variable symbol.

**D5.6 — How this interacts with the invoice rebuild. The invoice PDF path RENDERS TODAY.**

*(This section is rewritten end to end. The draft asserted the opposite, from a ticket status log
rather than from the tree. The finding is CH-VS-1, and it is the reason the catalog now carries
`conventions.md:217-243`.)*

**T-0522's three `CountryInvoiceConfigs` columns are shipped, in the committed migration:**

| Column | Type | Nullability | At |
|---|---|---|---|
| `LegalDisclaimerLanguageCode` | `character varying(5)` | nullable | `Initial.cs:556` |
| `LegalDisclaimerReviewStatus` | `integer` | **NOT NULL** | `Initial.cs:557` |
| `ConstantSymbol` | `character varying(4)` | nullable | `Initial.cs:558` |

They are matched in `20260723182623_Initial.Designer.cs:1830/:1848/:1852` and in
`CleansiaDbContextModelSnapshot.cs:1827/:1845/:1849`. **And there is no other unmigrated column on this
entity:** every mapped property of `CountryInvoiceConfig` (`CountryInvoiceConfig.cs:11-58`) appears in
the `CreateTable` at `Initial.cs:548-559`.

So:

- **The invoice PDF path renders today.** The draft's *"the invoice PDF path is down at HEAD"* and
  *"shipping this ADR's work without it changes nothing observable, because the document does not
  render either way"* are **deleted**. Its *"three nullable columns"* is deleted too — it is wrong
  twice over, since `LegalDisclaimerReviewStatus` is `NOT NULL` and `ConstantSymbol` is `varchar(4)`.
- **Provenance of the error, for the record:** `T-0522-…md:203-206`, dated **2026-08-04**,
  present-tense when written, never updated. It was true then. It is a record of a past reading, and
  quoting it in the present tense converted somebody's stale sentence into this ADR's load-bearing
  fact — under a §Method declaration that forswears exactly that, having applied the rule to its brief
  and not to the ticket.
- **This ADR's schema delta is its OWN owner-only `ef-migration` request.** There is no pending
  T-0522 pass to ride. Pre-prod it folds into `Initial` rather than stacking (CLAUDE.md, *Manual
  Steps*), but it is a **separate owner window and must be asked for as one** — see §Applies-to and
  §Consequences, both corrected.
- **The layout edits in D5.1 / D5.3 are edits to T-0522's shipped layout**, not to a pre-T-0522 one.
  Every line number in this section was read at HEAD on 2026-08-09.
- **If T-0522 is revised further before this lands**, only D5.1–D5.3 move; D1–D4 and D6 do not touch
  the layout at all.

### D6 — The failure mode when a duplicate is attempted anyway

**A post-commit exception is not an answer, and it is not the answer here.**

**D6.1 — It cannot happen by construction.** The value comes from a serialized allocator. The only
routes to a re-attempted value are a hand-written SQL insert (D4.6 removes the two in the repo) or a
counter row restored behind the table. The rest of this decision is the backstop for those.

**D6.2 — If it happens, it is a business result at the boundary, not an exception through it.** The
invoice insert is **flushed** where the violation can be caught, and a Postgres `23505` is collapsed
into a result — the idiom this codebase already ships in four places, with the same reflective
`SqlState` walk: `RefundService.cs:101,193-201`; `LoyaltyService.cs:368-407`;
`DbIdempotencyGuard.cs:42-45`; `StripeSubscriptionWebhookHandler.cs:203,236-244`, whose comment at
`:191-195` states the general rule — *"this handler does NOT own its own commit… So FLUSH the insert
HERE and own the failure"*.

- **New error key:** `BusinessErrorMessage.InvoiceReferenceUnavailable = "payroll.invoice.reference_unavailable"`.
- **Three new keys in total**, all reached from the admin app: `payroll.invoice.reference_unavailable` (this
  section), `payroll.invoice.reference_missing` (§D4.4), `payroll.invoice.reference_capacity_exhausted` (§D2.1). Each
  needs `api.<key>` in **all five locales on the admin app** — under `api.*`, **not** `errors.*` and
  **not** through an `XXX_ERROR_KEY_MAP` (CLAUDE.md; admin's `errors.*` block is legacy-but-live and
  new work does not extend it). The parity guard
  `apps/cleansia-admin.app/src/app/i18n/error-contract-parity.spec.ts` asserts them against
  `BusinessErrorMessage.cs` directly, so a missing locale fails a test rather than silently rendering
  *"An error occurred. Please try again."*
- **What the admin sees:** a refusal naming the payment reference, on a screen where clicking again
  works. **No invoice row is created**, no order-pay is assigned, nothing is half-done.

> **One tree fact the implementing ticket must confirm, recorded here because it was verified after the
> ruling and changes no decision.** Every existing invoice key in `BusinessErrorMessage.cs` is
> namespaced **`payroll.invoice.*`**, not `invoice.*` — `InvoiceNotFound = "payroll.invoice.not_found"`
> (`:211`), `InvoiceAlreadyPaid = "payroll.invoice.already_paid"` (`:226`),
> `InvoiceNotApproved = "payroll.invoice.not_approved"` (`:227`), and six more at `:212-225`. The three
> keys above are specified by this ADR in the `invoice.*` form and are transcribed as ruled; whether
> they should join the `payroll.invoice.*` family is a **naming** question for the implementing
> ticket's reviewer, not a decision this ADR takes. Whichever prefix ships, the constant name, the wire
> key and the five `api.*` locale keys must agree, and the parity spec is what proves it.

**D6.3 — What the batch does. The promise, corrected.**

With D2.3's C1/C2, the violation is raised and collapsed inside `GenerateInvoiceForEmployeeAsync`,
inside the `try` at `PayPeriodBackgroundService.cs:235-260`. What is true and may be claimed:

> An allocator failure, or a duplicate on C1, skips one cleaner; every other cleaner in the group is
> still invoiced. **Except** on the *first invoicing employee of a period*: there the `Rollback()` also
> reverts `period.Close()` (`:148`), the period stays `Open`, `:119-122` re-selects it on the next
> tick, and its period-closed emails are sent a second time. **No duplicate invoice results** —
> `:312-323` skips an employee who already has one — and no money moves.

The draft's unqualified *"one cleaner is skipped and logged; every other cleaner in the group is
invoiced"* is **deleted**: under a context-global `Rollback()` it is not unconditionally true.

*For the record, so this ADR's own account is right:* the challenge's stronger claim — that calling
`Rollback()` *"discards every other cleaner's tracked invoice"* — describes the **current `:187`-only**
shape, not C1/C2. Under a per-employee commit each prior invoice is already durable. The residue that
genuinely survives is the `period.Close()` case above, and only that.

**The stronger fix is available and is declined here, with its reason.** Committing `period.Close()` at
`:148` *before* `SendPeriodClosedEmailsAsync` removes the duplicate-email residue entirely. Its cost: a
crash mid-emails leaves the period `Closed` with an untreated tail, recoverable only through the admin
`GenerateInvoice` path (`GenerateInvoice.cs:87`) and **with no re-sent email**. Trading a duplicate
email for a silent untreated tail is a worse trade — and it is a pay-period-job decision that does not
belong to a variable-symbol ADR. **Recorded so the next author does not "fix" it without seeing the
trade.**

**D6.4 — One named exception to the queue consumer's ack rule, because the default is wrong here.**
`GenerateInvoiceHandler.cs:72-80` acks **every** `!IsSuccess` result, on the reasoning that *"retrying
won't change the verdict"*. For `payroll.invoice.reference_unavailable` that reasoning is false — a retry
allocates a **different** number. So this one error must **throw**, so the queue retries under
`maxDequeueCount` and poisons only if it persists. It is called out here because the handler's own
comment would otherwise justify swallowing it, and a swallowed one is an invoice that never exists and
nobody is told about.

*(`payroll.invoice.reference_capacity_exhausted` is the opposite case and stays on the ack lane: a retry inside
the same year allocates nothing, so retrying genuinely will not change the verdict.)*

### D7 — What must happen to `PayoutInvoicePdfDataTests`

**The named anti-pattern, applied to itself.** `patterns-backend.md:443-462` — *"A fixture that
supplies an input production never produces makes the test green and the feature dead… for each
arranged value, name the production code that produces it. If the answer is 'the test does', the test
is pinning the layout, not the feature."* Two tests fail that check:

- `Variable_Symbol_Is_Not_Derived_From_The_Invoice_Number` (`:136-145`)
- `Variable_Symbol_Is_Carried_Through_And_Stays_A_Valid_Numeric_Symbol` (`:150-160`)

Both arrange with `invoice.SetVariableSymbol(EmployeeInvoice.GenerateVariableSymbol("emp-1","period-1"))`
(`:140`, `:154`) — a call **no production code makes**. And the doc comment above them (`:147-149`)
asserts *"the generated numeric symbol is what reaches the document"*, **which is false in production
and checked by nothing.** Worse, the first of the two would pass **vacuously** against a null symbol:
`null` is not equal to an invoice number.

**The census is wider than two files.** Three more fixtures pin a symbol production never produced:

- **`PayoutInvoiceLayoutTests.cs:292`** — `VariableSymbol = "0001000001"` on an `InvoicePdfData`
  fixture — **and `:64`**, `Assert.Contains(fields, f => f.Value == "0001000001")`, which is the
  assertion that keeps it green. Both must move to a compliant value; `"0001000001"` violates D1.2
  (leading zero) on top of being hand-authored.
- The three `"VS 0001000001"` strings are a different thing and are **out of scope**: they are
  `BankTransferNote` *assertions*, sourced from `MarkInvoicePaidAdminOnlyTests.cs:68` (a default
  parameter) and `MarkInvoicePaidTests.cs:77`. A bank-transfer note is free text and may legitimately
  quote whatever a bank shows. **The new literal check is scoped to the `VariableSymbol` position
  only** (reviewer check #19).

**What must happen — deletion by compiler, not by memory.**

1. **`GenerateVariableSymbol`, `StableHash` and `SetVariableSymbol` are deleted**
   (`EmployeeInvoice.cs:212-216`, `:340-360`). Both `PayoutInvoicePdfDataTests` tests then **fail to
   compile**, which is the point: the arrangement is removed by the build, not by a reviewer noticing.
2. The `EmployeeInvoiceEntityTests.GenerateVariableSymbol_*` tests (`:122-164`) go with them — **four
   test methods** (`:122`, `:131`, `:140`, `:155`) covering **five cases**, including the T-0244
   `[Theory]` with its two hard-coded `InlineData` rows `"1883454606"` / `"1883676987"` (`:156-157`).
   **T-0244 is superseded, not reverted** — its finding (a per-process hash basis is a fiscal-reference
   trap) was correct, and this ADR agrees with it harder: the fix for a hash whose basis is unstable is
   not a stable basis, it is not hashing.
3. **The false doc comment is deleted with the tests it describes.** It is not rewritten around a
   different mechanism; a comment that asserted an untrue thing for four months has earned deletion.

**The twelve fixtures, and the loop this closes.** §D2.2's required parameter reaches **fourteen** call
sites of `Create` / `CreateFromOrderPays` — **two production** (`GenerateInvoice.cs:87`,
`PayPeriodBackgroundService.cs:328`) and **twelve fixture**, census verified exact at HEAD:

`DomainSeed.cs:160` · `PayrollMockFactory.cs:52` · `EmployeeInvoiceEntityTests.cs:19`, `:34`, `:58`,
`:76` · `MarkInvoicePaidTests.cs:26` · `MarkInvoicePaidNotifyTests.cs:26` ·
`AdminInvoiceAdjustmentHandlerTests.cs:25` · `FiscalReconciliationQueryTests.cs:337` ·
`PayoutInvoicePdfDataTests.cs:195`, `:211`.

**Every one of those twelve will hand-author a symbol production never produces** — the exact rule §D7
itself invokes, one level up. Two obligations discharge it, and both are required:

- **Replacement test #2 below is what actually closes it**: a census through the *real* handler and the
  *real* background service, so at least one assertion in the suite is fed by the allocator rather than
  by a literal.
- **One canonical fixture constant**, so twelve files do not each invent a literal:
  `PayrollMockFactory` exposes a single `TestVariableSymbol` (a D1-compliant value, e.g. `"2026000001"`)
  and the other eleven fixtures reference it. A grep for a bare ten-digit literal in a `VariableSymbol`
  position then finds nothing, which is what makes check #19 checkable.

**The replacements, and the one that would actually have caught this:**

| # | Test | Where | Why it is the honest one |
|---|---|---|---|
| 1 | N concurrent invoice creations in one pay period → N **distinct** symbols, zero exceptions | `Cleansia.IntegrationTests`, real Postgres — the direct analogue of `FiscalCounterAllocatorTests` (`src/Cleansia.IntegrationTests/Features/Receipts/FiscalCounterAllocatorTests.cs`) | This is the test the current design **cannot pass** and that **no current test attempts** (§Context §3) |
| 2 | Census: **every** production path that constructs an `EmployeeInvoice` yields a non-null symbol matching `^[1-9][0-9]{9}$` — built through the real handler and the real background service | `Cleansia.IntegrationTests` | This is the assertion whose absence is the whole bug, **and it is what discharges the twelve-fixture loop above** |
| 3 | One test through the **real mapper → real `QuestPdfService`** asserting the symbol reaches the rendered document, and the ticket **rasterizes and looks at it once** | `Cleansia.Tests` + the ticket's evidence | `patterns-backend.md:459-462` — *"a field-model assertion and a rendered document are different claims"* |
| 4 | The first digit is never `0` | `Cleansia.Tests` | The old regex `^\d{10}$` (`EmployeeInvoiceEntityTests.cs:137`) explicitly **permitted** the §3 hazard, and `PayoutInvoicePdfDataTests.cs:159`'s `^[0-9]{1,10}$` permits it *and* a short symbol |
| 5 | Allocating at the cap (`Value = 999999`) returns `payroll.invoice.reference_capacity_exhausted`, not an exception | `Cleansia.IntegrationTests`, real Postgres | The `WHERE` in §D2.1 yields an empty `RETURNING`; `FiscalCounterRepository.cs:63`'s unguarded `allocated[0]` is the shape that must **not** be copied |

### D8 — What the owner must decide, that I deliberately did **not** default

**These are here, verbatim, for the PM to file. This ADR does not write to `questions/open.md`.**

> **Q-VS-01 — [blocking: no] Is a Czech *variabilní symbol* really at most ten numeric digits, and is
> a bare sequence acceptable to your accountant as the reference on a self-billed payout invoice?**
> The platform already encodes "numeric, ≤10" in four places — `EmployeeInvoice.cs:71`
> (`[MaxLength(10)]`), `EmployeeInvoiceEntityConfiguration.cs:73-75`, `Initial.cs:1522`
> (`character varying(10)`), and a test regex at `PayoutInvoicePdfDataTests.cs:159` — but **no agent
> may assert a tax-law requirement** (T-0508 AC14), and every one of those four is an agent's earlier
> encoding, not your accountant's. The design (`2026000001`) fits inside that budget under any
> narrower answer, so **this does not block the build** — it blocks calling the constraint *verified*.
> **Default taken:** ten digits, `YYYY` + a six-digit per-year ordinal.

> **Q-VS-02 — [blocking: no, but it decides whether one whole path is dead code] How many payout
> invoices exist today, and has any cleaner already been paid against one?**
> I have no shell and no database, so I could not count. It matters twice: **(a)** it decides whether
> §D4.3's one-time assignment command has a real set to work on or is dead on arrival — if the DEV
> drop T-0522 records has already happened and no invoices have been regenerated since, the answer is
> zero rows and the command should not be built; **(b)** if you have already transferred money against
> an invoice, that invoice will never carry a symbol, and §D4.4's refusal is an obstacle the platform
> cannot detect — assign a reference via §D4.3 and put the bank's transaction id into the optional
> "bank transfer note" field when you mark it paid.
> **Default taken:** build the command, gate it hard, and do not backfill anything automatically.

> **⚠️ Status of Q-VS-02 at acceptance: it is raised here and is NOT filed.** `Q-VS-01` and `Q-VS-03`
> are at `questions/open.md:2090` and `:2097`; a grep for `Q-VS-02` and for its subject returns
> nothing. **It stays in this file, and it remains non-blocking, because every default it names is
> already taken in the design** — §D4.3 is specified and hard-gated, §D4.4 refuses forward, and no
> backfill is automatic. §D4.4's residual leg (*has money already left against a null-symbol invoice*)
> now routes to Q-VS-02's second leg rather than opening a new question. **The PM decides whether to
> file it; this ADR does not depend on the answer.**

**Deliberately NOT escalated, and decided here instead** — recorded so nobody re-opens them as owner
questions:
- The format (`YYYY`+ordinal vs per-period vs pure sequence) — an engineering choice with a stated
  rationale (§D1), not a business one. The one contingency inside it — allocation year vs accounting
  year — is recorded in D1.3 with its cost, and moves only if Q-VS-01's answer moves it.
- Whether the column becomes `NOT NULL` — a DB Master call with a written precondition (§D4.5).
- Whether the counter is tenant-scoped — the cheapest correct shape today, **contingent on Q-VS-03**
  (§D3.2), with the flip written down. Not a new question; Q-VS-03 is already filed.

### D9 — Alternatives considered and rejected

| # | Alternative | Why not |
|---|---|---|
| **A** | **Widen the hash** (SHA-256 → mod 10¹⁰), the remedy `planning/done/security-remediation-summary.md:281` suggested and T-0244 half-took | **It is enormously better than today's generator, and that is not enough.** Under the same birthday model §Context §3 uses, a *perfect* ten-digit hash gives `p(n) ≈ 1 − e^(−n(n−1)/(2·10¹⁰))`: at **n = 10 000, ≈0.50 %**; at **n = 100 000, ≈39 %** (both re-derived by hand). Compare 39 % at n = **100** today. Rejected on four grounds, none of which is "impossible": the probability **never reaches zero**; the failure is **silent** (two invoices, one reference, nothing raises); it lands on a **bank transfer**, where the owner reconciles real money by eye; and it gets **monotonically worse every year the platform runs**, so the design's safety margin is spent by success. *(The draft rejected it with "no hash is injective here" — an unsound lemma a reader who checks the arithmetic will find false. See §D1.)* **Explicitly forbidden by the brief, and independently rejected.** |
| **B** | **Keep the hash, catch the 23505, and retry with a salt** | Produces a symbol that is no longer a function of anything, so it is an allocator with extra steps — and a worse one, because the retry is unbounded and the collision rate grows with the year. If you are going to coordinate, coordinate first. |
| **C** | **VS = the invoice number** (the owner's specimen shows them coinciding at `20240001`) | Ruled out by the owner: *"VS can't equal the invoice number. These are 2 different and there is a separate property for it"* (T-0522 AC4). Independently impossible: `InvoiceNumber` is `INV-yyyyMM-XXXXX` (`EmployeeInvoice.cs:111`), non-numeric, and its five-character `Guid` slice is not unique by construction either. |
| **D** | **VS = a per-cleaner stable number; the period goes in the *specific* symbol** — the design the seed data implies (`SpecificSymbol` seeded `'2501'`/`'2412'`, `insert_employee_invoices.sql:46,92`) | This is a real CZ idiom and it is the strongest alternative. Rejected for two reasons: (i) the existing UNIQUE index on the bare `VariableSymbol` column would **reject the same cleaner's second invoice**, so it costs an index change on day one; (ii) with two of one cleaner's invoices unpaid at once, a statement line carrying only the VS is **ambiguous** — the reconciliation question has two answers, which is the failure this ADR exists to remove. Recorded because the dead `SpecificSymbol` column is the fossil of it (§D5.5). |
| **E** | **Reuse `FiscalCounters` with a `payout-invoice` `IssuerScope`** — zero new tables | Genuinely tempting and the closest call in this ADR. Rejected on three counts (§D2.1): its key is tenant-leading and the payout namespace must not be (§D3.2); its contract is **gaplessness** for CZ EET / DE TSE / AT RKSV (`FiscalCounter.cs:7-23`) and this counter is deliberately gappy (§D2.4); and its repository reads the ambient tenant internally (`FiscalCounterRepository.cs:19`), so bending it edits the fiscal money path to serve payroll. **The allocation *statement* is copied; the *table* is not.** |
| **F** | **A Postgres `SEQUENCE` + `nextval`** | Simplest and truly concurrent, and its non-transactional gap semantics are exactly right. Rejected because: **(i)** it cannot reset per year without a job, so the `YYYY` prefix (§D1.2–3, the no-leading-zero property) goes with it; **(ii)** a sequence is **not an inspectable, auditable, correctable row** — `FiscalCounter` is deliberately a row for exactly that reason (`FiscalCounter.cs:7-23`), and an operator who must see or repair the counter needs a `SELECT`, not `pg_sequences` folklore; **(iii)** §D1.5's cap repair lives in a `WHERE` clause on the update, and a bare `nextval` has **nowhere to put one** — it cannot refuse. *(Two of the draft's clauses are struck: `HasSequence(...).StartsAt(...)` lands in `CleansiaDbContextModelSnapshot.cs`, which is where this repo asserts schema facts, so "a magic number no test can see" is false; and "no sequence exists in this schema to pattern-match against" is a novelty argument, made by an ADR that introduces a new table.)* |
| **G** | **Assign in a `SaveChanges` interceptor / `IUnitOfWork` hook** | Invisible at the call site, needs SQL mid-save, and — decisively — it re-arms the exact defect: the guarantee becomes "the framework remembers", which is what a `SetVariableSymbol` nobody called already was. §D2.2's required parameter makes the guarantee the **compiler's**. |
| **H** | **Assign lazily when the PDF is rendered** | The number would exist first on a document and only later on a row; a regeneration would have to re-derive it (back to A); and `MarkInvoicePaid` needs it before any PDF is asked for (§D4.4 now refuses without it). It inverts §D2.3, which is the invariant that makes the whole thing checkable. |
| **I** | **Derive from a per-employee number + a per-period number** (`EEEE`+`PPPPPP`, injective, no collision) | **Correct in principle — it is the one derivation that works, and §D1 must not be read as ruling it out.** Rejected **on cost, not on impossibility**: **neither number exists** — `Employee` has no numeric sequence and `PayPeriod` has none, so it is **two** allocators, two entities and two migrations to avoid one. It also leaks a cleaner's platform ordinal onto a document they hand to third parties. Read correctly, it is not a counterexample to *"allocate"*; it is an instance of allocating **twice**. |
| **J** | **`NOT NULL` on the column from day one** | Stronger than nullable, and attractive once the DEV drop leaves zero rows — but I **cannot verify the row count** (§D8 Q-VS-02) and §D4.3 leaves a legitimately-null set for as long as an admin takes to work through it. §D2.2's required parameter already gives the guarantee at compile time; `NOT NULL` adds a runtime backstop against hand-written SQL only. Deferred with a written precondition (§D4.5), not dropped. |
| **K** | **Allocate an ordinal on the `PayPeriod` row that already exists** — `yyMMdd(StartDate) ‖ NNNN`, a counter column on `PayPeriods`; zero new tables, zero new entities, zero new role cards | *(Added at adjudication — `deliberation.md:69` requires a real trade-off's alternatives to be in the record, and this one was missing.)* **Rejected on three grounds, the first decisive:** (i) **`PayPeriod.Update` mutates `StartDate`** (`PayPeriod.cs:76`, assignment at `:94`), so the reference's own prefix would be a **mutable column** — an admin correcting a period's dates changes what the next reference means and desynchronizes it from the ones already printed on documents that have left the building; (ii) two tenants whose periods share a `StartDate` both allocate ordinal 1 and collide under the global index, the exact failure §D3.2 exists to prevent; (iii) 9 999 cleaners per period is a **reachable** cap where 999 999 per year is not. |

---

## Applies to

*(Rewritten at acceptance per R13. The draft's version was wrong on four counts, each corrected below.)*

- **`Cleansia.Core.Domain`** — two factory signatures gain a **required** `string variableSymbol`
  parameter; `SetVariableSymbol`, `GenerateVariableSymbol` and the private `StableHash` are **deleted**;
  **one new tenant-global counter entity** (`PayoutReferenceCounter`: `Year` `int` non-nullable,
  `Value`; **no `Scope`, no `TenantId`, not `ITenantEntity`**).
- **`Cleansia.Infra.Database`** — ⚠️ **`ef-migration`, owner-only, and it is THIS ADR's own request.**
  There is no pending T-0522 pass to ride (§D5.6). **One new table** keyed `(Year)` with one UNIQUE
  index on `(Year)` alone; **no index change on `EmployeeInvoices`**; **no backfill**. Pre-prod it
  folds into `Initial` rather than stacking (CLAUDE.md, *Manual Steps*). Plus one new repository
  carrying the allocator and its **sanctioned-exception doc-comment** (§D2.1).
- **`Cleansia.Core.AppServices`** — **two production creation call sites** (`GenerateInvoice.cs:87`,
  `PayPeriodBackgroundService.cs:328`); the **C1/C2 commit restructure + `Rollback()`** in
  `PayPeriodBackgroundService` (§D2.3); **one new admin command** (D4.3 assign-and-regenerate); **one
  new validator rule on `MarkInvoicePaid`**, inside the existing `Cascade.Stop` chain after
  `ApprovedAsync` (§D4.4); **three new `BusinessErrorMessage` constants**.
- **`Cleansia.Functions.Core`** — one named exception to `GenerateInvoiceHandler`'s ack rule (§D6.4).
- **`Cleansia.Infra.Services`** — three layout/model edits (§D5.1, §D5.3).
- **Tests** — **twelve fixture call sites** gain the parameter (§D7 census), one canonical
  `PayrollMockFactory.TestVariableSymbol`, two `PayoutInvoiceLayoutTests` literals corrected
  (`:64`, `:292`), the `GenerateVariableSymbol_*` block deleted (four methods / five cases), and five
  replacement tests added.
- **`Cleansia.App` admin** — **one admin action + confirm** for the D4.3 command. **No note dialog**
  (it was never scoped and §D4.4 no longer needs one). **Three new `api.*` keys × five locales** on the
  admin app — `reference_unavailable`, `reference_missing`, `reference_capacity_exhausted`.
  *(The draft said one key; the interim ruling said two; the ruled set is three — §D6.2.)*
- **`agents/knowledge/consistency.md`** — one edit, adding the payout-reference allocator as the
  **second** named sanctioned exception in the post-commit deviating-form list (`:346-353`), landed by
  the implementing change once the allocator exists in the tree.
- **No host coupling** — nothing here is reachable from Customer or Mobile.Customer.

---

## How a reviewer verifies compliance

1. **No hash.** `grep -rn "GenerateVariableSymbol\|StableHash\|SetVariableSymbol" src/` returns
   **nothing**. If any of the three still exists, the ADR is not implemented — it is decorated.
2. **The stamp is structural.** `EmployeeInvoice.Create` and `CreateFromOrderPays` declare
   `string variableSymbol` with **no default value**. Deleting the argument at either call site must
   fail the build. (Mutation check: remove it and confirm a compile error, not a test failure.)
3. **Two production call sites, and only two.** `GenerateInvoice.cs` and `PayPeriodBackgroundService.cs`.
   Any third is either wrong or the ADR needs revising.
4. **The index is untouched.** `EmployeeInvoiceEntityConfiguration.cs` still reads
   `builder.HasIndex(e => e.VariableSymbol).IsUnique().HasFilter("\"VariableSymbol\" IS NOT NULL")` —
   no `TenantId`, no `AreNullsDistinct`.
5. **The counter's key is exactly `(Year)`.** Open the entity: no `Scope`, no `TenantId`, does not
   implement `ITenantEntity`; `Year` is a non-nullable `int`; the unique index is on `(Year)` alone.
   **A key with any second column fails this check.**
6. **Two commits per employee in `PayPeriodBackgroundService`.** **C1** sits between
   `Add`/`AssignToInvoice` and `GenerateInvoicePdfAsync`; **C2** sits after
   `SetPdfBlobUrl`/`ClearPdfGenerationError` **and** after `SetPdfGenerationError` in the catch.
   **A single commit per employee fails this check.** Read the call order, not the comments.
7. **The symbol prints unconditionally.** `DefaultInvoiceLayoutBuilder.PaymentFields` adds the variable
   symbol **outside** any `if`. The constant symbol is still **inside** one.
8. **No second reference on the document.** `grep -rn "PaymentReference" src/Cleansia.Infra.Services/`
   returns nothing.
9. **The duplicate is a result.** A test forces a 23505 on the invoice insert and asserts a
   `BusinessResult.Failure` carrying `payroll.invoice.reference_unavailable` — **not** a thrown
   `DbUpdateException`. The five admin locales carry `api.payroll.invoice.reference_unavailable`,
   `api.payroll.invoice.reference_missing` **and** `api.payroll.invoice.reference_capacity_exhausted`, proved by
   `error-contract-parity.spec.ts`.
10. **The consumer throws on that one error.** `GenerateInvoiceHandler` has an explicit branch for
    `payroll.invoice.reference_unavailable` that throws, with the reason in a comment.
11. **The concurrency test exists and runs on real Postgres.** N parallel creations in one period → N
    distinct symbols. A unit test with a mocked allocator does not satisfy this.
12. **Format.** Every produced symbol matches `^[1-9][0-9]{9}$`. A test asserting `^\d{10}$` or
    `^[0-9]{1,10}$` does not satisfy this and is the old assertion.
13. **`Paid` cannot be backfilled.** The one-time assignment command refuses a `Paid` invoice with a
    business error, and there is a test named for it.
14. **The seed scripts do not carry forbidden symbols.** `insert_employee_invoices.sql` /
    `insert_employee_payroll.sql` are deleted, or contain no literal beginning with `0` in the
    `VariableSymbol` position.
15. **A failed C1 is followed by `Rollback()` at that call site**, and the ADR's tracker-scope sentence
    (*"`Rollback()` is context-global — it sets every tracked entry to `Unchanged`, including
    `period.Close()` if no earlier employee in this period has committed"*) is repeated as a comment
    there.
16. **The allocator is a sanctioned exception, in writing.** It carries a doc-comment in the
    `PromoCodeRepository.cs:28-38` shape; `consistency.md`'s post-commit deviating-form list names it
    as the **second** exception; and **no allocator call site sits inside a `BeginTransactionAsync`
    scope.**
17. **The cap is in the SQL.** The `DO UPDATE` carries `WHERE "Value" < 999999`, and an empty
    `RETURNING` result maps to `payroll.invoice.reference_capacity_exhausted` — **not** to an unguarded
    `allocated[0]`.
18. **`MarkInvoicePaid` refuses a null-symbol invoice** with `payroll.invoice.reference_missing`, and the rule
    is **inside** the `InvoiceId` `Cascade.Stop` chain **after `ApprovedAsync`** — not a new root
    `RuleFor`. `BankTransferNote` is still optional and its `MaximumLength(500)` root rule is
    unchanged.
19. **No `VariableSymbol` literal anywhere in the tree begins with `0`** — including `InvoicePdfData`
    fixtures (`PayoutInvoiceLayoutTests.cs:64`, `:292`). Fixtures reference the one canonical constant
    rather than inventing literals. **`BankTransferNote` fixtures are out of scope** — free text may
    quote whatever a bank shows.

## Consequences

**Positive**
- The payout invoice acquires the one field it exists to carry, and its absence becomes visible on the
  document rather than invisible in a conditional.
- "Mark this invoice paid" becomes a claim that reconciles — and, under §D4.4, one that **cannot be
  made** against a document carrying no reference.
- Duplicate references become impossible by construction and, if forced, become a *refusal* instead of
  a 500 or a poisoned batch.
- **The reconciliation loop closes with code that already exists.** `EmployeeInvoiceSpecification`
  already exposes an **exact-match filter on `VariableSymbol`** in the admin invoice query — the filter
  property at `:14`, the predicate at `:60-62`, the wiring at `:112`. The owner reads a line off a bank
  statement, types the number, and finds the invoice. This also makes D1.2's no-leading-zero property
  **concrete rather than theoretical**: a symbol typed without the zero a bank form dropped matches
  **nothing**, silently — an exact-match filter has no near-miss.
- The batch's *"email the PDF, then commit the row"* ordering — a latent defect wider than this ADR —
  is closed as a by-product of the invariant, not as a separate discovery.
- One more derived-identifier trap leaves the codebase; the pattern (*claim it, don't compute it*) is
  now stated where a future author will look.

**Negative / accepted**
- **One new table and its own owner-only migration**, asked for as a separate owner window. The draft
  claimed it could ride T-0522's already-pending pass; **there is no such pending pass** (§D5.6), and
  losing that mitigation is a real cost of getting the fact right.
- Gaps in the sequence, permanently and by design (§D2.4).
- A cross-tenant volume inference channel under activated multi-tenancy, with the flip written down
  and **contingent on Q-VS-03** (§D3.2).
- A finite, frozen set of symbol-less invoices, reachable only through a hard-gated admin command —
  and, under §D4.4, **unpayable through the platform until an admin works through it**. That is the
  intended direction of the trade, and the residual for money already transferred is named in §D4.4
  and routed to Q-VS-02.
- The period close becomes durable at the first invoicing employee rather than at the group commit,
  and a failed C1 on that first employee costs a duplicate set of period-closed emails (§D6.3).
- Between D5.1 landing and the first allocated symbol, every rendered payout invoice prints `—` for the
  variable symbol (§D5.1) — intended, bounded to a deploy.
- Until the `nswag-regen` in §D5.4, the iOS References card prints the invoice number twice.

**Neutral but load-bearing**
- Nothing in this ADR is reachable from the Customer or Mobile.Customer hosts. The per-audience seam is
  untouched.
- No country branch is introduced. The symbol's *format* is platform-wide; the only per-country
  variation on this document remains `CountryInvoiceConfig` (`ConstantSymbol`, the legal notice), read
  through `CountryInvoiceContext` exactly as T-0522 wired it.

---

## Challenge

**Challenged 2026-08-08 by the `architect` in challenger mode. The full text is
`agents/backlog/adr/challenges/NNNN-payout-invoice-variable-symbol.md`** — twelve findings
(CH-VS-1 … CH-VS-12), seven of them raised as blocking (1, 2, 3, 4, 5, 6, 8), plus thirteen
"found sound" items naming what was attacked and held, plus one new owner question (Q-VS-03, since
filed at `agents/backlog/questions/open.md:2097`). The challenge file stays as the record; it is
not duplicated here.

**Disposition of the six places the author pre-named as attack surfaces** (below): #2 became
**CH-VS-1** and stands. #1 and #5 became **CH-VS-10** and **CH-VS-11(a)** and stand as amendments.
#3 became **CH-VS-2/CH-VS-8** and stands. #6 (§D6.4's carve-out) was attacked and **held** —
challenge "found sound" #8. #4 (§D4.3 with possibly zero rows) was not pressed; it is Q-VS-02, which
is **not filed** — see the Verdict's note to the PM and §D8.

**Where the author expected to be attacked, named so a challenger did not have to find them:**

1. **§D3.2 accepts a cross-tenant inference channel.** Is a global counter really forced, or is
   `(TenantId, VariableSymbol) NULLS NOT DISTINCT` the better trade once you accept the migration?
2. **§D5.6 claims the invoice PDF path is down at HEAD.** That is inferred from T-0522's own Review
   text, not from a running system. If the migration has since been applied, D5.6's "both-or-neither"
   framing weakens.
3. **§D2.3(a) changes the pay-period job's commit granularity.** Does committing per employee break the
   period-close transaction's atomicity with the new-period creation at `:167-168` in a way this ADR
   has not thought through?
4. **§D4.3 builds a command that may have zero rows to act on** (Q-VS-02). Is that scope that should
   not be spent until the owner answers?
5. **§D1's year prefix costs 40 % of the digit budget** for a scanning convenience. Is a 7-digit
   `1000000`-seeded sequence the better answer, given the width argument in D1.1 is weakened by it?
6. **§D6.4 carves an exception out of a documented consumer rule.** Is one error's special case the
   thin end of a classification the consumer does not have?

## Defense

*No author defense round was run.* At the PM's direction the panel **lead adjudicated the challenge
directly** (`process/deliberation.md` step 5), re-deriving every blocking finding from the tree rather
than from either document. Where a finding is marked **conceded** in the Verdict, the concession is the
lead's and the revision is specified for the author to transcribe — the Verdict's closed list is the
defense's replacement, not a summary of one.

## Verdict

**Adjudicated 2026-08-09 by the `architect` in lead mode.** Method: `Read` / `Glob` / `Grep` only —
no shell, no `git`, no test run, no database. **No number below is inherited from the draft or from the
challenge**; every one was re-derived from a file opened during this adjudication and is cited at
`file:line`. Where my re-derivation *differs* from the challenger's, I say so — twice it does.

### Outcome: **REVISE.** Not accept-and-number.

**All twelve findings stand.** Seven were raised as blocking and all seven survive re-derivation; the
five non-blocking amendments are all correct and two of them get *stronger* why-nots than the
challenger had. **The decision itself survives intact** — *claim the number, do not compute it*, the
counter-row allocator, printing unconditionally, and refusing to backfill a printed document are all
upheld, and none of the twelve touches them. What fails is the supporting layer: one inherited fact,
one under-specified commit boundary, one rule aimed at a population it cannot reach, one unspecified
column in a conflict arbiter, one caller-property-stated-as-API-property, and one unsound lemma the
draft declared unarguable.

The revision is **transcription, not deliberation**: §"The closed list" below is a complete, numbered
set of edits with their reviewer-check deltas. Nothing in it requires a new decision.

### Per-finding ruling

| # | Ruling | Reason (re-derived at HEAD) |
|---|---|---|
| **CH-VS-1** | **STANDS — conceded** | Verified, and **more strongly than the challenger claimed**. Not only are the three named columns in the committed migration (`Initial.cs:556-558`), but **every** mapped property of `CountryInvoiceConfig` (`CountryInvoiceConfig.cs:11-58`) appears in `Initial.cs:548-559` — so there is no *other* unmigrated column that could take the path down. `LegalDisclaimerReviewStatus` is `nullable: false` (`:557`) and `ConstantSymbol` is `varchar(4)` (`:558`), so *"three nullable columns"* is wrong twice over. Provenance confirmed: `T-0522-….md:203-206`, dated 2026-08-04, present-tense-when-written, never updated. **The invoice PDF path renders today.** → **R1** |
| **CH-VS-2** | **STANDS — conceded, with a magnitude correction** | Mechanism exact: `CleansiaDbContext.Rollback()` (`:107-113`) sets **every** tracked entry to `Unchanged`; `RefundService.cs:103` is the precedent and does call it; `CommitAsync` (`CleansiaDbContext.cs:67-100`) ends in a **context-wide** `SaveChangesAsync` (`:99`) — this codebase has no per-entity commit. **Correction:** the challenger's *with-`Rollback()`* branch ("discards every other cleaner's tracked invoice") describes the **current `:187`-only** shape, not D2.3(a)'s. Under a per-employee commit each prior invoice is already durable. The residue that *does* survive is the `period.Close()` case — see the joint ruling below. → **R2** |
| **CH-VS-3** | **STANDS — conceded, all four legs** | (a) `MarkAsPaid` throws unless `Approved` (`EmployeeInvoice.cs:254-257`) and `MarkInvoicePaid` refuses a `Paid` invoice three times (`:46-47`, `:24-30`, `:93-98`) — there is no path to attach a note to an already-paid invoice. (b) the eligible set is a strict subset of D4.3's. (c) `invoice-detail.component.ts:106-108` calls `this.facade.markAsPaid()` **with no argument** and `invoice-detail.facade.ts:79-88` assigns that absent parameter — the field ships `undefined` on every attempt. (d) the existing `RuleFor(x => x.BankTransferNote)` (`:53-55`) is a **separate root rule** under the class-level `Continue` default; a `MustAsync` there would deref `null` on a bad id, which the three `invoice!` reads at `:65/:71/:77` avoid only by sitting in the `Cascade.Stop` chain. → **R3** |
| **CH-VS-4** | **STANDS — conceded** | The draft never says what `Scope` holds, who supplies it, or that it is `NOT NULL`, and it sits in the `ON CONFLICT` arbiter. The trap is documented *in the file the ADR copies*: `FiscalCounterRepository.cs:30-32` on nulls-distinct, and `FiscalCounter.cs:13-18` explicitly trains the reader that the scope string *"is the extension point"* and binds *"NOT merely the tenant"* — so `Scope = tenantId` is the reading the precedent invites, and reviewer check #5 as written passes on it. → **R4** |
| **CH-VS-5** | **STANDS — conceded, both halves** | `FiscalCounterRepository.cs:28-30` says the opposite of D2.4 **for the same statement**: *"Running through the context's connection joins the caller's open transaction … bound to the same commit/rollback."* The draft's *conclusion* holds at HEAD only because no payout path opens one — `UnitOfWorkPipelineBehavior.cs:13-33` opens none, `PayPeriodBackgroundService.CloseExpiredPeriodsAndOpenNewAsync` (`:107-197`) opens none. The catalog half is right and is **the architect's to close, not the developer's**: `consistency.md:346-353` makes the deviating form *"a self-committing write inside a handler **with no sanctioned-exception doc-comment**"*, `patterns-backend.md:641-644` scopes the law identically, and this ADR introduces the **second** such write. → **R5** |
| **CH-VS-6** | **STANDS — conceded. The conclusion survives; the argument is replaced.** | Full ruling below. The `(EmployeeId, PayPeriodId)` unique index is at `EmployeeInvoiceEntityConfiguration.cs:123-124` as cited. → **R6** |
| **CH-VS-7** | **STANDS (amendment)** — and gets a **third** why-not the challenger did not have | `deliberation.md` requires alternatives in the record and D9's ten rows do not include "allocate on a row that already exists". The rejection is now stronger than the challenger's own: **`PayPeriod.Update` mutates `StartDate`** (`PayPeriod.cs:76`, assignment at `:94`), so the reference's prefix would be a mutable column. D9-F's two weak clauses go. → **R7** |
| **CH-VS-8** | **STANDS — conceded** | `PayPeriodBackgroundService.cs:352/:359/:360/:361/:371` confirmed: the invoice is mutated three times **after** the render/upload. Under "commit" singular those ride the next commit. Loop structure confirmed: `foreach tenantGroup` `:133` → `foreach period` `:144` → `foreach employee` `:219`; `period.Close()` at **`:148`**, one level up from the employee loop; tenant override `:138-142`; group commit `:187`. Ruled jointly with CH-VS-2. → **R2** |
| **CH-VS-9** | **STANDS (amendment)**, census extended by two | `PayoutInvoiceLayoutTests.cs:292` confirmed — and the challenger **missed `:64`**, `Assert.Contains(fields, f => f.Value == "0001000001")`, which is the assertion that would keep it green. The three `"VS …"` notes it lists are *assertions*; their sources are `MarkInvoicePaidAdminOnlyTests.cs:68` (a default parameter) and `MarkInvoicePaidTests.cs:77`. The twelve-fixture census reproduces **exactly**: 14 call sites of `Create`/`CreateFromOrderPays`, 2 production, 12 fixture. → **R8** |
| **CH-VS-10** | **STANDS (amendment)** | Reinforced by an event later than the challenge: **Q-VS-03 is now filed** (`questions/open.md:2097`). You cannot ask the owner whether the premise holds and call the conclusion "forced" in the same document. → **R9** |
| **CH-VS-11** | **STANDS (amendment)** — and (b) **upgrades from "record it" to "fix it in one clause"** | (a) the prefix is the allocation year by D1's own definition. (b) confirmed: `RETURNING` reports the post-increment value and the increment has auto-committed, so the counter runs permanently past the cap, platform-wide. The repair belongs **in the statement**, not after it. → **R10** |
| **CH-VS-12** | **STANDS (amendment)** | (a) `FileExtensions.cs:40` reads `PaymentReference = invoice.PaymentReference ?? invoice.VariableSymbol` — literally a fallback expression, and the conclusion survives only because `Create` always sets it (`EmployeeInvoice.cs:126`) and `SetPaymentReference` (`:224-228`) has **zero callers** (verified). (b) confirmed: **four** methods (`:122`, `:131`, `:140`, `:155`), five cases. → **R11** |

**Consensus is NOT reached at this revision. Zero blocking challenges will remain once R1–R17 land**;
none of them reopens a decision, so no second panel round is needed — the author transcribes, the lead
re-reads against the closed list, the PM numbers and accepts.

---

### The identity ruling (CH-VS-6) — **the challenger is right, and the principle is statelessness, not bit width**

**The bit-counting re-derives and is irrelevant.** 26 Crockford-base-32 characters × 5 bits = 130 bits
per ULID, two of them 260; `log₂(10¹⁰) = 10 × 3.321928 = 33.219`. Both numbers are right.
`EmployeeInvoiceEntityConfiguration.cs:13-19` confirms `HasMaxLength(26)` on both id columns. But
pigeonhole rules out injectivity **on the full type domain**, and injectivity is only ever *required*
on the **realized** set — which is small. §D9-I is a derivation that is injective on it, in the same
file, and D9 rejects D9-I **on cost**, not on impossibility. §D1 as written would rule out §D9-I. Two
sections of one ADR contradict each other, and the one that is wrong is the one that says *"so this is
settled and not re-litigated."*

**One correction to the challenger's reasoning, which the ADR must not copy.** The challenger cites the
`(EmployeeId, PayPeriodId)` unique index (`EmployeeInvoiceEntityConfiguration.cs:123-124`) as the thing
that *"already pins that set unique"*. It does not make anything injective. It bounds `|R|` at
(#cleaners × #periods) — one invoice per employee-period — and that is all. It is the *premise* of the
counterexample, not the counterexample. Do not cite it as if it helps.

**One correction to the challenger's replacement lemma, which is slightly too strong.** *"…requires at
least one input to be a small ordinal **the platform** assigned"* — an **externally** assigned dense
identifier would serve equally (a cleaner's registration number, a bank-assigned id). The operative
property is **density**, not authorship. Allocation is how you *obtain* density when nothing dense is
in hand — which is this platform's situation, and that is the honest reason.

**What §D1 must argue instead** (this is the sentence the ADR carries, and the one a future reader may
rely on):

> Ten decimal digits is a **dense** codomain: 10¹⁰ points, all of them reachable. `EmployeeId` and
> `PayPeriodId` are `Ulid.NewUlid()` values — 130-bit, sparse, and drawn by the id generator, not
> chosen by the platform. A function of those two values **alone** is therefore fixed *before* the
> realized set exists and cannot be chosen to be injective on it; its collision probability is strictly
> positive and grows monotonically with the row count, at every width that fits in ten digits.
> Injectivity into ten digits requires at least one input drawn from a **dense** identifier space — an
> ordinal that somebody assigned. The platform holds no such identifier for an employee or for a pay
> period, so it must **introduce** one, and introducing one is allocation. **§D9-I is not a
> counterexample to "allocate" — it is an instance of it that allocates twice**, which is exactly why
> §D9 rejects it on cost and not on impossibility.

**And the arithmetic that must replace the pigeonhole line in §D9-A, because the honest version is also
the stronger one.** Under the same birthday model §Context §3 already uses,
`p(n) ≈ 1 − e^(−n(n−1)/(2·10¹⁰))` for a *perfect* ten-digit hash: at n = 10 000 the exponent is
≈ 0.005 → **≈0.50 %**; at n = 100 000 it is ≈ 0.5 → **≈39 %**. So widening the hash is *enormously*
better than today's 10 000-bucket generator, and §D9-A must say so. It is rejected because the
probability **never reaches zero, the failure is silent, it lands on a bank transfer, and it gets worse
every year the platform runs** — not because "no hash is injective here", which a reader who checks the
arithmetic will find false.

**Delete *"so this is settled and not re-litigated."*** An ADR earns non-re-litigation by carrying an
argument that survives being checked. Foreclosing with an unsound lemma is worse than not foreclosing:
the next author cites §D1 verbatim to reject a design that is fine, reads §D9-I, and stops trusting the
document. **This is why CH-VS-6 was correctly raised as blocking despite changing no decision.**

---

### CH-VS-2 + CH-VS-8, ruled together (as the challenger asked)

**The primitive must be stated before the invariant.** `IUnitOfWork.CommitAsync` →
`BaseRepository.CommitAsync` (`:171-174`) → `CleansiaDbContext.CommitAsync` (`:67-100`) ends in a
**context-wide** `SaveChangesAsync` (`:99`). **There is no per-entity commit in this codebase.** So
§D2.3(a)'s *"commit inside the loop"* does not mean "commit this employee's changes" — it means "commit
everything the change tracker holds, including whatever the two enclosing loops mutated". Every
disagreement in CH-VS-2 and CH-VS-8 follows from that one sentence being absent.

**Ruling — the ADR specifies two commits per employee, named, and states what each carries:**

- **C1** — after `_employeeInvoiceRepository.Add(invoice)` and the `AssignToInvoice` loop
  (`:334-339`), **before** `GenerateInvoicePdfAsync` (`:352`). It makes the reference durable before any
  document carrying it exists. **It also persists `period.Close()`** (`:148`, one loop level up) and is
  therefore a behaviour change the ADR must name: the period close becomes durable at the *first
  invoicing employee* instead of at `:187`.
- **C2** — after `SetPdfBlobUrl` / `ClearPdfGenerationError` (`:360-361`) **and** after
  `SetPdfGenerationError` in the catch (`:371`). Without C2 those three mutations ride the next
  employee's commit or `:187`, and CH-VS-8's loss is real.

**On a failed C1** — the only commit that can raise `23505` on `VariableSymbol` — call `Rollback()`
(`BaseRepository.cs:181-184` → `CleansiaDbContext.cs:107-113`) **and state in the ADR body that it is
context-global**. Its scope at that instant, under C1/C2, is: this employee's `Added` invoice and
`Modified` order-pays, **plus `period.Close()` if and only if no earlier employee in this period has
already committed**. Then continue the loop.

**§D6.3's promise is corrected, not deleted.** What is true and may be claimed:

> An allocator failure, or a duplicate on C1, skips one cleaner; every other cleaner in the group is
> still invoiced. **Except** on the *first invoicing employee of a period*: there the `Rollback()` also
> reverts `period.Close()`, the period stays `Open`, `:119-122` re-selects it on the next tick, and its
> period-closed emails are sent a second time. **No duplicate invoice results** — `:312-323` skips an
> employee who already has one — and no money moves.

Delete the unqualified *"one cleaner is skipped and logged; every other cleaner in the group is
invoiced."* And record the challenger's over-reach so the ADR's own record is right: the
*with-`Rollback()`* "discards every other cleaner's tracked invoice" describes the **current**
`:187`-only shape, not C1/C2.

**The stronger fix is available and I rule against taking it in this ADR.** Committing `period.Close()`
at `:148` *before* `SendPeriodClosedEmailsAsync` removes the duplicate-email residue entirely. Its cost:
a crash mid-emails leaves the period `Closed` with an untreated tail, recoverable only through the admin
`GenerateInvoice` path (`GenerateInvoice.cs:87`) and **with no re-sent email**. Trading a duplicate email
for a silent untreated tail is a worse trade, and it is a pay-period-job decision that does not belong to
a variable-symbol ADR. **Record the option and the reason for declining it**, so the next author does not
"fix" it without seeing the trade.

---

### Does any ruling move a schema shape? **Yes — one, and it is on the proposed table only.**

1. **`PayoutReferenceCounters` loses its `Scope` column** (R4). The key becomes **`(Year)`**, with
   `Year` a non-nullable `int` and one UNIQUE index on it alone. This removes the entire CH-VS-4 hazard
   class by construction: a non-nullable `int` key cannot reproduce the nulls-distinct collapse
   `FiscalCounterRepository.cs:30-32` documents, so no `.AreNullsDistinct(false)` retrofit is in play.
   Rationale to carry in D2.1: **the ADR cannot name a second value for a scope**, and D3.1's decision
   is that there is exactly one namespace — a scope column is in tension with the decision it would sit
   under. If Q-VS-03 ever forces per-tenant namespaces, the tenant term is added *then*, in the same
   owner-only migration that replaces the `EmployeeInvoices` index — which D3.2 already writes down.
2. **Nothing else moves.** `IX_EmployeeInvoices_VariableSymbol` is untouched — verified still
   `HasIndex(e => e.VariableSymbol).IsUnique().HasFilter("\"VariableSymbol\" IS NOT NULL")` at
   `EmployeeInvoiceEntityConfiguration.cs:116-118`. `CountryInvoiceConfigs` needs nothing (CH-VS-1).
   `BankTransferNote` keeps its `varchar(500)` (`:83-84`) and its optionality (R3). The column stays
   nullable (D4.5 survives untouched).
3. **The migration rides nothing.** CH-VS-1 removes the ADR's only mitigation for its owner-only step:
   **there is no pending T-0522 pass to ride.** The counter table is **its own `ef-migration` request**,
   owner-only. Pre-prod it folds into `Initial` rather than stacking (CLAUDE.md, *Manual Steps*), but it
   is a separate owner window and must be asked for as one, in §Applies-to and in §Consequences.

---

### The closed list — transcription, not deliberation

Each item names its target section, what it must say, and the reviewer-check delta. Nothing here
requires a new decision.

**R1 (CH-VS-1) — rewrite §D5.6 end to end.**
1. Delete *"The invoice PDF path is down at HEAD"* and *"Shipping this ADR's work without it changes
   nothing observable, because the document does not render either way."*
2. State instead: T-0522's three columns are **shipped** — `LegalDisclaimerLanguageCode varchar(5)
   nullable`, `LegalDisclaimerReviewStatus integer **NOT NULL**`, `ConstantSymbol varchar(4) nullable`
   at `Initial.cs:556-558`, matched in `20260723182623_Initial.Designer.cs` and
   `CleansiaDbContextModelSnapshot.cs`; **every** mapped property of `CountryInvoiceConfig`
   (`CountryInvoiceConfig.cs:11-58`) is present in `Initial.cs:548-559`, so no other column on this
   entity is unmigrated. **The invoice PDF path renders today.**
3. Delete *"three nullable columns"*.
4. State: **this ADR's counter table is its own owner-only `ef-migration` request**; there is no pending
   pass to ride. Fix the §Applies-to cost line and the §Consequences line *"which is why it must ride
   T-0522's already-pending pass rather than asking for a second one."*
5. **Re-decide the D5.1/D5.3 sequencing explicitly, in §D5.1**, now that the document renders: landing
   D5.1 before any symbol exists makes every payout invoice print `Variabilní symbol —`. **Ruling: that
   is correct and intended** — D5.1's own argument is that absence must be loud, and a field that
   silently vanishes is what produced this defect. But it must appear as a *decision with its
   consequence stated*, not as a side effect: *"between D5.1 landing and the first allocated symbol,
   every rendered payout invoice prints `—` for the variable symbol."* Add the constraint that D5.1/D5.3
   **do not ship a release ahead of D2.2** — the window is a deploy, not a sprint.
6. §Method declaration #2 gains one sentence naming what was not re-verified (see R17).

**R2 (CH-VS-2 + CH-VS-8) — rewrite §D2.3 and §D6.3 together**, exactly as the joint ruling above
specifies: the context-wide-commit sentence first; **C1** and **C2** named with what each carries; the
`period.Close()` durability change named; `Rollback()` named at the failed-C1 site with its
context-global scope stated **in the ADR body, not in a ticket**; §D6.3's promise replaced with the
quoted corrected form; the challenger's over-reach recorded; the hoist-the-close option recorded and
declined with its reason.

**R3 (CH-VS-3) — delete §D4.4 and replace it with a precondition on the strong control.**
1. **Delete D4.4 entirely**, including *"For an invoice already paid against no reference, the
   compensating record already exists and becomes mandatory."* The mechanism cannot serve that
   population and the mandatory form fails 100 % of attempts against a UI that ships `undefined`.
2. **Replace it:** `MarkInvoicePaid` **refuses** an invoice whose `VariableSymbol` is null, with a new
   `BusinessErrorMessage.InvoiceReferenceMissing = "payroll.invoice.reference_missing"` whose message names the
   remedy (*"Assign a payment reference before recording the transfer."*). The remedy is D4.3's
   assign-and-regenerate command, on the same screen.
3. **Placement is load-bearing and must be written:** the new rule joins the existing `InvoiceId`
   `Cascade.Stop` chain (`MarkInvoicePaid.cs:40-51`) **after `ApprovedAsync`** — *not* as a new root
   `RuleFor`. A root rule runs under FluentValidation's class-level `Continue` default and would
   `GetByIdAsync(...)!.VariableSymbol` on `null` for a bad id; the three existing `invoice!` reads
   (`:65`, `:71`, `:77`) are safe only because they sit inside that chain.
4. **`BankTransferNote` stays exactly as it is** — optional, `varchar(500)`
   (`EmployeeInvoiceEntityConfiguration.cs:83-84`), root rule `MaximumLength(500)` unchanged
   (`MarkInvoicePaid.cs:53-55`), display-only at `invoice-detail.component.html:268-275`. The ADR must
   not make it mandatory and must not claim it as a control it cannot write.
5. **Name the residual honestly:** if the owner has *already* transferred against a null-symbol invoice,
   the refusal is an obstacle and the platform cannot detect that case. The stated path is: assign via
   D4.3 (the row gains a reference the transfer did not quote), and put the bank's own transaction id
   in the **optional** `BankTransferNote` on the mark-paid. Route this to **Q-VS-02**, whose second leg
   already asks it — **do not open a new question**.
6. **§Applies-to:** no admin note dialog (it was never scoped, and is no longer needed); **one** admin
   action + confirm for the D4.3 command; **two** `api.*` keys ×5 on the admin app, not one.
   *(Superseded by Erratum E3 + E4 below: the real number is **four**.)*

**R4 (CH-VS-4) — delete `Scope`.** §D2.1's statement becomes `ON CONFLICT ("Year") DO UPDATE …`; the
entity carries `Year` (`int`, non-nullable) and `Value` and no scope column; the unique index is on
`(Year)` alone. Carry the rationale from the schema-shape section above, including *why* a scope column
is in tension with D3.1 and *when* a tenant term would be added instead. Reviewer check #5 is rewritten
(R14).

**R5 (CH-VS-5) — restate D2.4's mechanism, add the invariant, register the exception.**
1. Replace *"`Context.Database.SqlQueryRaw` auto-commits"* with: *"`SqlQueryRaw` runs on the context's
   connection and **joins an ambient transaction if one is open** — `FiscalCounterRepository.cs:28-30`
   documents exactly that for this statement. It auto-commits here **because no payout path opens one**:
   `UnitOfWorkPipelineBehavior.cs:13-33` opens none and
   `PayPeriodBackgroundService.CloseExpiredPeriodsAndOpenNewAsync` (`:107-197`) opens none."*
2. Add the rule, in §D2.1, as a checkable invariant: **"The payout reference allocator MUST NOT be
   called inside an explicit transaction."** Both of D2.4's properties depend on it — the gap semantics,
   **and** the duration of the row lock `ON CONFLICT … DO UPDATE` takes on the single counter row.
   Under a long transaction that one row serializes every concurrent payroll run for its life, which is
   a global contention channel a design that never mentions locking would introduce silently.
3. **Catalog obligation, in the same change** (architect's, per the pattern-evolution loop): this is the
   codebase's **second** self-committing write inside a handler. `consistency.md:346-353` makes the
   deviating form *"a self-committing write inside a handler with no sanctioned-exception
   doc-comment"* and names `PromoCodeRepository.TryIncrementGlobalRedemptionsAsync` as the one
   exception, *"because it says so, not because it exists"*. So: the ADR **mandates** the
   sanctioned-exception doc-comment on the allocator in the `PromoCodeRepository.cs:28-38` shape, and
   §Applies-to gains the `consistency.md` edit adding the second named exception.

**R6 (CH-VS-6) — replace §D1's closing argument** with the quoted paragraph in the identity ruling;
delete *"so this is settled and not re-litigated"*; do **not** cite the `(EmployeeId, PayPeriodId)`
index as if it aids injectivity; rewrite **§D9-A**'s why-not around the two derived figures (≈0.50 % at
n = 10 000, ≈39 % at n = 100 000) and the four real reasons — never zero, silent, on a bank transfer,
monotonically worse.

**R7 (CH-VS-7) — add the missing §D9 row and fix §D9-F.**
1. **New row K — allocate an ordinal on the `PayPeriod` row that already exists**
   (`yyMMdd(StartDate) ‖ NNNN`): zero new tables, entities or role cards. **Rejected on three grounds,
   the first of which is new:** (i) **`PayPeriod.Update` mutates `StartDate`** (`PayPeriod.cs:76`,
   assignment at `:94`), so the reference's own prefix is a mutable column — an admin correcting a
   period's dates changes what the next reference means and desynchronizes it from the ones already
   printed; (ii) two tenants whose periods share a `StartDate` both allocate ordinal 1 and collide under
   the global index, the exact failure §D3.2 exists to prevent; (iii) 9 999 cleaners per period is a
   reachable cap where 999 999 per year is not.
2. **§D9-F:** delete *"starting it high is a magic number in DDL that no test can see"*
   (`HasSequence(...).StartsAt(...)` lands in `CleansiaDbContextModelSnapshot.cs`, which is where this
   repo asserts schema facts) and *"there is no sequence anywhere in this schema to pattern-match
   against"* (novelty, from an ADR introducing a new table). Keep the year-reset argument and add the
   strong one: **a sequence is not an inspectable, auditable, correctable row** — `FiscalCounter` is
   deliberately a row for that reason (`FiscalCounter.cs:7-23`) — and R10's cap repair needs a `WHERE`
   clause on the update, which a bare `nextval` has nowhere to put.

**R8 (CH-VS-9) — extend §D7's census, name one fixture constant, add a scoped literal check.**
1. Add **`PayoutInvoiceLayoutTests.cs:292`** (`VariableSymbol = "0001000001"`) **and `:64`**
   (`Assert.Contains(fields, f => f.Value == "0001000001")` — the assertion that keeps it green; the
   challenger named only `:292`).
2. The `"VS 0001000001"` notes are `BankTransferNote` fixtures whose sources are
   `MarkInvoicePaidAdminOnlyTests.cs:68` and `MarkInvoicePaidTests.cs:77`. **Ruling: scope the new check
   to the `VariableSymbol` position only** — a bank-transfer note is free text and may legitimately
   quote whatever a bank shows.
3. §Applies-to says *"two creation call sites"*; it must say **two production call sites
   (`GenerateInvoice.cs:87`, `PayPeriodBackgroundService.cs:328`) plus twelve fixture call sites that
   gain the parameter** — census verified exact: `DomainSeed.cs:160`, `PayrollMockFactory.cs:52`,
   `EmployeeInvoiceEntityTests.cs:19/:34/:58/:76`, `MarkInvoicePaidTests.cs:26`,
   `MarkInvoicePaidNotifyTests.cs:26`, `AdminInvoiceAdjustmentHandlerTests.cs:25`,
   `FiscalReconciliationQueryTests.cs:337`, `PayoutInvoicePdfDataTests.cs:195/:211`.
4. **Connect the loop §D7 leaves open:** those twelve fixtures will hand-author a symbol production
   never produces — `patterns-backend.md:443-462`, the rule §D7 itself invokes. Say in §D7 that
   replacement test **#2** (the production census through the real handler and the real background
   service) is what discharges it, and name one canonical fixture constant so twelve files do not each
   invent a literal.

**R9 (CH-VS-10) — replace "forced, not chosen" in §D3.2** with: *global is the **cheapest correct shape
today** — the shipped index is already global (`EmployeeInvoiceEntityConfiguration.cs:116-118`), it has
no NULLS-DISTINCT hole because it carries no `TenantId`, and production is single-tenant. It is
**contingent** on D3.1's *"the payer's account is one account"*, which is exactly what **Q-VS-03** asks
(`questions/open.md:2097`). The flip is written down and **must be re-examined the moment that
premise stops holding.**

**R10 (CH-VS-11) — two fixes.**
1. **(a) §D1.3 must state, in D1.3 itself,** that the prefix is the **year of allocation and is not the
   accounting year of the work** — a December period closed on 2 January produces `2027…`. Record the
   alternative (key the counter on `PayPeriod.EndDate`'s year) **and its cost**: `GenerateInvoice.Handler`
   holds only `PayPeriodId` (`GenerateInvoice.cs:87-91`), so it would need a `PayPeriod` load. Note that
   **Q-VS-01's answer may move it**. **Do not file a new question.**
2. **(b) §D1.5's "No wrap, ever" becomes true rather than aspirational: put the cap in the statement.**
   `DO UPDATE SET "Value" = "PayoutReferenceCounters"."Value" + 1 WHERE
   "PayoutReferenceCounters"."Value" < 999999`. The row then stops at the cap instead of running away
   past it, and the failure is repairable by the year rolling over rather than by a manual `UPDATE` on a
   poisoned counter. **`RETURNING` yields no row when the `WHERE` is false**, so the ADR must state that
   the empty result maps to a named business error
   (`BusinessErrorMessage.InvoiceReferenceCapacityExhausted = "payroll.invoice.reference_capacity_exhausted"`)
   and **not** to an unguarded `allocated[0]` — `FiscalCounterRepository.cs:63` reads exactly that and
   copying the shape unguarded is the defect. Add the runbook line: within a year there is no remedy but
   the year.

**R11 (CH-VS-12) — two citation fixes.**
1. §D5.3: `FileExtensions.cs:40` **is** a fallback expression. Correct sentence: *"its only fallback is
   to the variable symbol, and it is unreachable because `Create` always sets the field
   (`EmployeeInvoice.cs:126`) and `SetPaymentReference` (`:224-228`) has no caller"* — the zero-caller
   fact is verified and is worth carrying, because it is what makes the fallback dead.
2. §D7 step 2: **four** test methods (`:122`, `:131`, `:140`, `:155`), **five** cases.

**R12 — one thing neither side raised, and it is a positive the ADR should claim.**
`EmployeeInvoiceSpecification` already exposes an **exact-match filter on `VariableSymbol`** in the
admin invoice query (`:14` the filter property, `:60-62` the predicate, `:112` the wiring). That is the
reconciliation loop closing — the owner reads a line off a bank statement and finds the invoice by the
number. Cite it in §Consequences (Positive), and note that it makes D1.2's no-leading-zero property
concrete: a symbol typed without the zero a bank form dropped matches **nothing**, silently.

**R13 — rewrite §Applies-to** once R1/R3/R4/R5/R8 land: its own `ef-migration` (no ride); one new table
with a `(Year)` key and no `Scope`; two production + twelve fixture call sites; one admin action for
D4.3 and **no** note dialog; **four** `api.*` keys ×5 (see Errata E3 and E4); one `consistency.md` edit.

**R14 — reviewer-check delta** (§"How a reviewer verifies compliance"). Checks 1–4, 7–14 stand as
written; #9's key list grows.
- **#5 replaced:** *"The counter's key is exactly `(Year)`. Open the entity: no `Scope`, no `TenantId`,
  does not implement `ITenantEntity`; `Year` is a non-nullable `int`; the unique index is on `(Year)`
  alone. **A key with any second column fails this check.**"*
- **#6 replaced:** *"**Two** commits per employee in `PayPeriodBackgroundService`. C1 sits between
  `Add`/`AssignToInvoice` and `GenerateInvoicePdfAsync`; C2 sits after
  `SetPdfBlobUrl`/`ClearPdfGenerationError` **and** after `SetPdfGenerationError` in the catch. **A
  single commit per employee fails this check.** Read the call order, not the comments."*
- **#9 extended:** the five admin locales carry `api.payroll.invoice.reference_unavailable`,
  `api.payroll.invoice.reference_missing` **and** `api.payroll.invoice.reference_capacity_exhausted`, proved by
  `error-contract-parity.spec.ts`.
- **New #15:** *"A failed C1 is followed by `Rollback()` at that call site, and the ADR's
  tracker-scope sentence is repeated as a comment there."*
- **New #16:** *"The allocator carries a sanctioned-exception doc-comment in the
  `PromoCodeRepository.cs:28-38` shape, `consistency.md`'s post-commit deviating-form list names it as
  the second exception, and no allocator call site sits inside a `BeginTransactionAsync` scope."*
- **New #17:** *"The cap is in the SQL: the `DO UPDATE` carries `WHERE "Value" < 999999`, and an empty
  `RETURNING` result maps to `payroll.invoice.reference_capacity_exhausted` — not to an unguarded
  `allocated[0]`."*
- **New #18:** *"`MarkInvoicePaid` refuses a null-symbol invoice with `payroll.invoice.reference_missing`, and
  the rule is **inside** the `InvoiceId` `Cascade.Stop` chain after `ApprovedAsync`, not a new root
  `RuleFor`. `BankTransferNote` is still optional and its `MaximumLength(500)` root rule is
  unchanged."*
- **New #19:** *"No `VariableSymbol` literal anywhere in the tree begins with `0` — including
  `InvoicePdfData` fixtures (`PayoutInvoiceLayoutTests.cs:64`, `:292`). `BankTransferNote` fixtures are
  out of scope."*

**R15 — acceptance-time artefacts** (not before; the text moves on twelve points first).
- Living doc `agents/architecture/decisions/payout-invoice-references.md`.
- Role card `agents/knowledge/roles/payout-reference-allocator.md`. Its **"does NOT know"** list must
  carry, at minimum: **the tenant** (it has no tenant term and must not acquire one without the D3.2
  flip), **the invoice** (it returns a number, never a row, and never writes `EmployeeInvoices`), and
  **the pay period** (the year it keys on is the year of allocation, R10a). If a scenario forces it to
  know any of the three, the responsibility is wrong or a collaborator is missing.

**R16 — §Method declaration #2** gains: *"the T-0522 ticket status log was **not** re-verified against
the tree, and that is what produced the §D5.6 error CH-VS-1 caught."*

**R17 — process rule, third recurrence, landed in the catalog with this verdict.** See below.

---

### The recurring finding: this is the **third** stale-document propagation this sprint

After a living decision page and a sprint status section, an ADR took a **code-state** claim from a
ticket status log (`T-0522-….md:203-206`, true on 2026-08-04, never updated) and asserted it in the
present tense — inside a document whose own §Method declaration forswears exactly that, having applied
the rule to its brief and not to the ticket. Three instances of one failure is a missing rule, not
three mistakes.

**Landed, per the pattern-evolution loop:** `agents/knowledge/conventions.md` gains
**"A claim about the tree cites the tree — never another artifact"**, sited beside the existing
`### Cross-stack claims (ADR-0033 D2)` evidence rule it generalizes, carrying an
`**Enforced by:**` line and a tier token as `conventions.md` §"The price of a law" requires. It is
**T3-HUMAN** — the discriminator ("is this citation a code-state claim?") needs a reader, not a regex —
and its named enforcer is `deliberation.md` step 5, the lead's own gate, which is where all three
instances were or should have been caught. **This is a rule, not a note: it puts §D5.6 in violation as
written**, which is the routing test's own Test-1 signal.

---

### Notes to the PM (I did not act on these — you hold the files)

1. **`Q-VS-02` is not filed.** `Q-VS-01` and `Q-VS-03` are at `questions/open.md:2090-2102`; a grep for
   Q-VS-02 and for its subject returns nothing. The draft raises it verbatim in §D8 and **R3 item 5 now
   routes a second question to it** (has money already left against a null-symbol invoice). If it was
   dropped deliberately, §D8 should say so; if not, it wants filing. It remains non-blocking either way
   — every default it names is already taken in the design.
2. **The number and the acceptance are yours.** The draft stays in `drafts/` at `proposed`; I did not
   allocate a number and did not accept it.
3. **Ticket shape, if it helps:** R1/R6/R9/R10a/R11/R12/R16 are pure text and can land in one pass.
   R2+R3+R4+R5+R7+R8+R10b+R13+R14 change what gets built and should be re-read against this list before
   the ADR is numbered. **R4 is the only one that moves a schema shape**, and it moves the *proposed*
   table only.

---

## Transcription record

**2026-08-09 — `architect`, author mode.** R1–R17 transcribed into the body above; the ADR numbered
**0046** and accepted. The Verdict, Challenge and Defense sections above are the lead's and the
challenger's records and are reproduced unaltered.

**Every `file:line` in the body was re-opened and re-read at HEAD before it was written**, per
`conventions.md:217-243` — the rule this verdict landed. **Four citations had moved or were imprecise
and are corrected in the body** (the Verdict text above is left as the lead wrote it):

| Cited as | Correct at HEAD | Where it appears |
|---|---|---|
| `Initial.cs:1518` (`VariableSymbol varchar(10)`) | **`Initial.cs:1522`** — `:1518` is now `ApprovedAt` | §Context §1 note, §Context §3, §D1.1, §D8 Q-VS-01 |
| `Initial.cs:2650-2655` (`IX_EmployeeInvoices_VariableSymbol`) | **`Initial.cs:2654-2659`** — `:2650-2652` is now `IX_EmployeeInvoices_TenantId` | §Context §1, §D3.1 |
| `deliberation.md:61-62` (alternatives must be in the record) | **`deliberation.md:69`** — `:61-62` is the round-cap/escalation sentence | §D9 row K |
| `StripeSubscriptionWebhookHandler.cs:203` (the *"does NOT own its own commit… FLUSH the insert HERE"* comment) | the **comment** is `:191-195`; `:203` is the `catch`; `:236-244` is the `SqlState` walk | §D6.2 |

Three further facts were verified after the ruling and are recorded in the body without changing any
decision: the existing invoice error keys use the `payroll.invoice.*` prefix
(`BusinessErrorMessage.cs:211-227`) while the three keys the closed list named used a bare `invoice.*`
(**resolved by the erratum below**); `MarkInvoicePaid.cs` lives under
`Features/EmployeePayroll/`, and its `Cascade.Stop` chain is exactly the shape R3 describes
(`:40-51`, `ApprovedAsync` last at `:50-51`, the three `invoice!` reads at `:65`/`:71`/`:77`); and the
key count in §Applies-to is **three** `api.*` keys, not the two R13 carried — R13 predates R10b's
`payroll.invoice.reference_capacity_exhausted`, and reviewer check #9 (R14) requires all three, so "two" would
have put §Applies-to in contradiction with §"How a reviewer verifies compliance" in the same file.

**R15's two artefacts are written:** `agents/architecture/decisions/payout-invoice-references.md` and
`agents/knowledge/roles/payout-reference-allocator.md`. **R5.3's `consistency.md` edit is recorded as a
required deliverable in §Applies-to and is deliberately not pre-landed** — naming the allocator as the
second sanctioned exception before it exists in the tree would be the very violation
`conventions.md:217-243` forbids. It lands with the implementing change.

---

### Erratum E1 — the three new keys take the `payroll.invoice.*` prefix (PM, 2026-08-09)

The closed list named them `invoice.reference_unavailable` / `invoice.reference_missing` /
`invoice.reference_capacity_exhausted`. **Every existing error key on this entity is
`payroll.invoice.*`** — nine of them, `BusinessErrorMessage.cs:211-227` — so a bare `invoice.*` opens a
second error category for one entity, which is the shape this ADR argues against everywhere else. The
keys are renamed throughout this file, the living doc and the role card:

| Was | Is |
|---|---|
| `invoice.reference_unavailable` | **`payroll.invoice.reference_unavailable`** |
| `invoice.reference_missing` | **`payroll.invoice.reference_missing`** |
| `invoice.reference_capacity_exhausted` | **`payroll.invoice.reference_capacity_exhausted`** |

**This is free exactly once.** §D1.6 and §D4.1 both turn on the principle that a reference already on
the wire may not be renamed — *"renaming turns a missing translation into a second missing
translation"* is the same argument one artifact up. Nothing emits these keys, no locale carries them,
and no client has seen them, so the cost today is a find-and-replace and the cost after the
implementing ticket is a migration of five locales times however many apps reach the endpoint. It is a
naming call within a category the tree already fixed, not a decision the panel deferred, which is why
it is an erratum and not a second round.

---

### Erratum E2 — `Auditable` cannot satisfy §D2.1's statement and reviewer check #5 at the same time (build lane, 2026-08-09)

§D2.1's `INSERT … ON CONFLICT` writes `"CreatedBy"` / `"CreatedOn"` and updates `"UpdatedBy"` /
`"UpdatedOn"`. Those four columns exist only on `Auditable` (`Common/Auditable.cs:3`) — and `Auditable`
also declares `public string? TenantId` at `:5`, which its entity configuration maps to a **column plus
an index**. So `: Auditable` gives you the audit columns *and* a `TenantId` column, and reviewer check
#5 says in terms that **a key with any second column fails**, with the entity carrying no `TenantId` at
all.

The ADR's phrase *"one new tenant-global counter entity"* hides that fork. It is resolved the way this
codebase's own tenant-global lane already resolves it — `ProcessedMessage` (ADR-0010) is
`: BaseEntity` with what it needs declared explicitly — so `PayoutReferenceCounter : BaseEntity`
declares the four audit columns itself and takes a plain `IEntityTypeConfiguration`. **The table has no
`TenantId` column**, asserted against `information_schema` rather than against the model.

No decision moves: §D3.2 wanted a counter with no tenant term and that is what exists. What was wrong
was the assumption that the base class the audit columns come from is free of one.

### Erratum E3 — D4.3's gate needs a fourth key, and §Applies-to says three

D4.3 gates the one-time assignment on `VariableSymbol IS NULL` **and** a live status **and** not
cancelled — but names a key for neither the status arm nor the **already-has-a-symbol** arm. Neither
existing key is true for the latter: `payroll.invoice.already_exists` means *an invoice already exists
for this pay period*, so reusing it shows an admin a sentence about a different fact.

**`payroll.invoice.reference_already_assigned`** is added, translated in the five admin locales
alongside the other three. This fills an under-specification rather than overriding a decision — but
§Applies-to and reviewer check #9 both say **three** `api.*` keys and the real number is **four**.

---

### Erratum E4 — the key count is four everywhere, and the frontend proved it (admin lane, 2026-08-09)

E3 raised the count from three to four by adding `payroll.invoice.reference_already_assigned`. Two
places in the transcribed closed list still said **two**, and reviewer check #9 still said three. All of
them now say four.

**The frontend lane discharged it without needing anything**: all four keys already ship under
`api.payroll.invoice.*` in all five admin locales, and the shared `HttpErrorInterceptorFn` resolves
`api.${dotValue}` off the ProblemDetails `errors` bag — which `CleansiaApiController.CreateProblemDetails`
populates on **both** the validation arm and the handler-failure arm. So every key maps by construction,
with no client key map, no `errors.*` entry, and no generic fallback. `reference_already_assigned` earns
its existence in practice rather than in theory: reusing `payroll.invoice.already_exists` for it would
have shown an admin a sentence about a *different fact* (an invoice already exists for this pay period).

### Erratum E5 — §D4.3's "re-runnable state" leaves no trace an admin can see

D4.3's ordering is right and the handler implements it: allocate → stamp → **commit** → regenerate, so a
failed regeneration keeps the number on the row and the regeneration is re-runnable. **What the ADR did
not notice is that nothing on the row records that it failed.** `RegenerateInvoicePdf` calls
`SetPdfBlobUrl` / `ClearPdfGenerationError` on its **success** path only and never `SetPdfGenerationError`
on failure — so after a refetch `pdfGenerationFailed` still reads false, the PDF-status banner still reads
healthy, and the stored document lacks the number the row claims.

The **only** live signal is the transient `PdfBlobUrl == null` on the response, which the admin UI now
branches on with an explicit *"reference assigned, but the PDF was not regenerated"* message. If that
message is dismissed or missed, the ADR's own "just re-run the regenerate" instruction survives solely in
a server log line.

Two smaller notes from the same reading, recorded so neither is rediscovered as a defect:
- The response collapses *"regenerate failed"* and *"regenerate returned no URL"* into one `null`. The UI
  reads null as failure — the conservative direction, over-warning rather than under-warning.
- **D4.3's gate has three arms and a client can read only two.** `EmployeeInvoiceDetailDto` carries
  `status` and `variableSymbol` but no `isCancelled`, so the button's visibility predicate is faithful
  *today only by a domain coincidence*: `EmployeeInvoice.Cancel()` is the sole writer of `IsCancelled` and
  sets `Status = Cancelled` in the same statement. Safety is unaffected — the server's gate reads the
  entity — so this is visibility fidelity, not correctness. Pinning the coincidence costs nothing; putting
  `isCancelled` on the DTO costs a regen.

