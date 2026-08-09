# ADR-NNNN — The payout invoice's **variabilní symbol** is a number the platform **claims**, never one it **derives**: a durable year-scoped counter allocated **before** the row exists, stamped through a **required constructor parameter** so a new creation path cannot compile without one, unique in **one global namespace** whose existing filtered index survives byte-for-byte, printed **unconditionally** so its absence is visible on the document, and — when a duplicate is somehow attempted — surfaced as a **business result**, never as a post-commit exception

- **Status:** `proposed` — drafted 2026-08-08 by the `architect` in **author** mode. **Not yet
  challenged.** Numbers are allocated at acceptance; this file lives in `drafts/`.
- **Date:** 2026-08-08
- **Supersedes:** nothing accepted. **Retires in practice:** **T-0244** (*"`GenerateVariableSymbol`
  — replace per-process `GetHashCode` with a deterministic stable hash"*, `INDEX.md:2080` **done ✅**).
  T-0244's *finding* was right and is upheld more strongly here; its *remedy* — a better hash — is the
  thing this ADR rules out. See §D9.
- **Consumes / must not contradict:** ADR-0002 D2.2 + ADR-0010 + ADR-0023 (queue-consumer claim
  ordering — §D6.2 states the one named exception it needs), ADR-0034 (payout details; the bank block
  the symbol sits beside), ADR-0038 (*"post-persist means post-commit"* — §D2.3 is the same rule
  applied to a document instead of an FK), ADR-0041 (self-billing: Cleansia issues this document on
  the cleaner's behalf, which is **why** the reference is Cleansia's to allocate), **T-0522**
  (`in_review`; the rebuilt document this ADR prints onto — §D5.6 is the interaction).
- **Applies to:** `Cleansia.Core.Domain` (two factory signatures gain a **required** parameter; two
  methods and one private helper are **deleted**; **one new tenant-global counter entity**) ·
  `Cleansia.Infra.Database` (⚠️ **`ef-migration`, owner-only** — **one new table, no index change on
  `EmployeeInvoices`, no backfill**; must ride T-0522's already-pending drop-and-regenerate pass) ·
  `Cleansia.Core.AppServices` (two creation call sites, one ordering fix in the batch, one new admin
  command, one validator rule on `MarkInvoicePaid`) · `Cleansia.Functions.Core` (one named exception
  to `GenerateInvoiceHandler`'s ack rule) · `Cleansia.Infra.Services` (three layout/model edits) ·
  `Cleansia.App` admin locales (one new `api.*` key ×5) · **no host coupling** — nothing here is
  reachable from Customer or Mobile.Customer.
- **Living doc at acceptance:** `agents/architecture/decisions/payout-invoice-references.md` (new).
- **Role card at acceptance:** `agents/knowledge/roles/payout-reference-allocator.md` (new).
- **Owner questions this ADR raises and does NOT answer:** two, quoted verbatim in §D8. **They are in
  this file only** — the PM holds `questions/open.md` and files them.

---

> ### ⚠️ Method declaration
>
> **1. No shell.** `Read` / `Glob` / `Grep` / `Write` only. **No `Bash`, no `git`, no test run, no
> database.** Nothing was compiled, executed or measured. Every fact below is read from a file at HEAD
> and cited at `file:line`.
>
> **2. No claim is inherited.** The brief that commissioned this ADR was re-verified line by line
> before being used. **It is right on the load-bearing finding and wrong in two places**, both
> corrected in §Context — one of them (*"every row has `VariableSymbol = NULL`"*) matters to the
> backfill ruling, and one (*"the parties are currently printed the wrong way round"*) is stale by four
> days. The brief's own warning — *"I have been wrong about this field once already"* — is why they are
> stated rather than smoothed over.
>
> **3. The collision figures were re-derived, not copied.** §Context §3 shows the arithmetic and the
> model's assumption. All four rows reproduce.
>
> **4. No legal claim is made.** Whether a Czech *variabilní symbol* is statutorily ≤10 numeric digits
> is **not** asserted here; what is asserted is that the *platform* encodes that constraint in four
> places. The legal confirmation is an owner question (§D8, Q-VS-01), per T-0508 AC14 (*"no agent
> asserts a tax-law requirement"*).

---

## Context

### 1. What is true at HEAD

**The finding stands, verified independently.**

| Claim | Verified at | Verdict |
|---|---|---|
| `SetVariableSymbol` / `GenerateVariableSymbol` have zero production callers | `EmployeeInvoice.cs:212`, `:340`; repo-wide grep returns only `PayoutInvoicePdfDataTests.cs:140,154`, `EmployeeInvoiceEntityTests.cs:125-161` | ✅ **true** |
| The PDF renders the field only when non-empty | `DefaultInvoiceLayoutBuilder.cs:181-182` — inside `if (!string.IsNullOrWhiteSpace(data.VariableSymbol))` | ✅ **true** |
| `PaymentReference` is rendered by no layout | declared `InvoicePdfData.cs:8`, mapped `FileExtensions.cs:40`, and read by **nothing** in `Pdf/Layouts/*` — the only other hit in the whole PDF surface is a *test fixture* setting it (`PayoutInvoiceLayoutTests.cs:294`) | ✅ **true** |
| A collision hits the index after the handler returns | `UnitOfWorkPipelineBehavior.cs:20-30` — the handler runs, *then* `CommitAsync`. No `catch` anywhere on this path | ✅ **true** |
| `IX_EmployeeInvoices_VariableSymbol` is UNIQUE on the bare column, filtered `IS NOT NULL` — genuinely enforcing, not the tenancy trap | `EmployeeInvoiceEntityConfiguration.cs:116-118`; `Initial.cs:2650-2655` | ✅ **true** |

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
T-0522 is `in_review` with AC0–AC15 checked. **What is not stale is T-0522's pending owner-only
`ef-migration`** — see §D5.6, which is the real interaction and is the opposite of "assume yours lands
first".

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
  (`Initial.cs:1518`), so `'0321876543'` and `'321876543'` are **different strings** to the unique index
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

1. **Numeric, ten characters.** Fits `character varying(10)` (`Initial.cs:1518`) exactly and uses the
   whole budget once, forever — the width never changes, so a transcription that dropped a digit is
   detectable by length alone.
2. **The first digit is never `0`.** `YYYY ≥ 2026`, so the leading digit is `2` for the next ~7 900
   years. This is the §3 hazard closed by construction: printed string ≡ stored string ≡ what a bank
   form shows.
3. **Self-describing on a bank statement.** The owner reconciles by eye against a statement; a
   year-prefixed reference sorts and scans. This is the one place a format preference earned its way in.
4. **Capacity 999 999 payout invoices per calendar year.** At one invoice per cleaner per period this
   is not a bound anyone will meet.
5. **No wrap, ever.** An ordinal above `999999` **fails the allocation** with a business error. A wrap
   would be a silent duplicate, which is the failure this whole ADR exists to prevent.
6. **It is not the invoice number and cannot become it.** The owner ruled *"VS can't equal the invoice
   number. These are 2 different and there is a separate property for it"* (T-0522 AC4, corrected
   2026-08-03). `InvoiceNumber` is `INV-yyyyMM-XXXXX` (`EmployeeInvoice.cs:111`) — non-numeric, so the
   two can never coincide even by accident.

**Why nothing derived can work — the pigeonhole argument, so this is settled and not re-litigated.**
An invoice's identity is `(EmployeeId, PayPeriodId)`, two 26-character ULIDs
(`EmployeeInvoiceEntityConfiguration.cs:13-19`, `HasMaxLength(26)`), i.e. ≈260 bits. The target is ten
decimal digits, ≈33.2 bits. **No function from the former to the latter is injective.** Every
derivation is therefore a hash, and every hash without coordination has a birthday collision. Widening
the hash moves a probability; it does not change the kind of the thing. **The only correct designs
allocate.**

### D2 — Where it is assigned, and what happens if assignment fails

**D2.1 — The allocator.** A durable counter row, allocated by one atomic statement, copying the shape
already shipped and reviewed in `FiscalCounterRepository.AllocateNextAsync` (`:33-42`):

```
INSERT INTO "PayoutReferenceCounters" ("Id","Year","Scope","Value", …)
VALUES (@id, @year, @scope, 1, …)
ON CONFLICT ("Year","Scope")
DO UPDATE SET "Value" = "PayoutReferenceCounters"."Value" + 1, …
RETURNING "Value";
```

- Postgres takes a row lock on the conflicting tuple, so concurrent allocations serialize and each
  `RETURNING` reports a distinct value — the property `FiscalCounterRepository.cs:26-32` documents.
- The nullable-parameter lesson from that file (`:49-53`) does not arise, because **there is no tenant
  parameter** (§D3.2).
- **A new table, not `FiscalCounters`.** Three reasons, in order of weight: (i) `FiscalCounter`'s key
  is `(TenantId, Year, IssuerScope)` (`FiscalCounterEntityConfiguration.cs:26-29`) and its repository
  reads the ambient tenant internally (`FiscalCounterRepository.cs:19`) — a payout counter **must not**
  be tenant-keyed (§D3.2), and bending the fiscal allocator to allow that would edit the fiscal money
  path to serve payroll; (ii) `FiscalCounter`'s entire contract is *gapless*-monotonic for CZ EET / DE
  TSE / AT RKSV (`FiscalCounter.cs:7-23`) and **this counter is deliberately gappy** (§D2.4) — putting
  a gappy scope in a table whose doc-comment promises gaplessness is a trap for the next reader; (iii)
  a payout reference is not a fiscal artifact and must not appear in a fiscal counter export.
- The counter entity is **tenant-global** — it does **not** implement `ITenantEntity`. §D3.2 is why.

**D2.2 — The stamp is a required constructor parameter, and that is the whole answer to "both paths".**

`EmployeeInvoice.Create` and `EmployeeInvoice.CreateFromOrderPays` take `string variableSymbol` as a
**required, non-defaulted** parameter. `SetVariableSymbol` and `GenerateVariableSymbol` are **deleted**.

This is not a style choice; it is the mechanism. T-0522 established the precedent on this exact
document five days ago and stated the reasoning in its own Review: *"The new `payoutDetails` parameter
is **required, not defaulted**, so a future third call site is a compile error rather than three silent
`—`s: that is the exact defect this AC existed to close, and a default would have re-armed it."*
The same sentence applies verbatim here. A validator, a convention, or a `SetVariableSymbol` call the
author must remember are all things that were already available and all things that produced today's
state.

Call sites after the change: `GenerateInvoice.cs:87` and `PayPeriodBackgroundService.cs:328`. A fourth
path does not compile.

**D2.3 — Ordering, stated as an invariant a reviewer can check.**

> **The row that owns a reference is committed before any document carrying it is generated, uploaded
> or delivered.**

Sequence: **allocate → construct → add → commit → render → upload → deliver.** This is ADR-0038's
*"post-persist means POST-COMMIT"* applied to a document instead of a foreign key
(`patterns-backend.md:633`).

- Paths 1 and 2 already satisfy it: the PDF is not produced by `GenerateInvoice.Handler` at all.
- **Path 3 violates it today** (§Context §4) and must change. Two acceptable shapes; **(a) is
  preferred**:
  - **(a)** commit inside the per-employee loop, still under the tenant override set at
    `PayPeriodBackgroundService.cs:138-142`. This is CLAUDE.md's own reference shape (*"commit **inside**
    the loop"*, `CleanupStalePendingOrders.cs:76-119`) and it bounds the blast radius of any failure to
    one cleaner.
  - **(b)** buffer the emails and send them after the `:187` group commit. Correct, but holds every
    PDF in memory for the group.

**D2.4 — What happens if assignment fails.**

- **The allocation is not rolled back with the invoice, by design.** `Context.Database.SqlQueryRaw`
  auto-commits: there is no ambient transaction on the command path (`UnitOfWorkPipelineBehavior.cs`
  opens none), and this is the same declared exception `PromoCodeRepository.cs:33-38` documents —
  *"ExecuteUpdateAsync issues SQL and auto-commits immediately… That is intentional and REQUIRED for
  atomicity."*
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
  degradation), **and the rest of the batch is invoiced.** Their `OrderEmployeePay` rows are still
  unassigned, so the next generation picks them up. No new code is needed for this — but it is only
  true once D2.3(a) has moved the commit inside the loop.

### D3 — Uniqueness scope, stated precisely

**D3.1 — The namespace is global, and the existing index survives unchanged.**

`IX_EmployeeInvoices_VariableSymbol` — `UNIQUE` on the bare `VariableSymbol` column, filtered
`WHERE "VariableSymbol" IS NOT NULL` (`EmployeeInvoiceEntityConfiguration.cs:116-118`,
`Initial.cs:2650-2655`) — **is the right shape and is not touched.** Not one line of index DDL changes
on `EmployeeInvoices`.

Why not `(TenantId, VariableSymbol)`:

- **CLAUDE.md's own rule forbids it in the naive form.** *"A unique index that includes `TenantId`
  enforces nothing in single-tenant mode"* — `TenantId` is nullable, Postgres treats NULLs as DISTINCT,
  and null is production today. A `TenantId`-leading index would need `.AreNullsDistinct(false)` to
  enforce anything at all. The bare-column index has no such hole and needs no such retrofit.
- **The requirement is about a human and a bank statement.** The reference exists so that one line on
  one statement maps to exactly one invoice. The payer's account is one account. A namespace at least
  as wide as the payer is mandatory; **global is the only shape that is unconditionally at least that
  wide**, under every future arrangement of tenants and accounts.

**D3.2 — Consequence: the counter is global too, and that is a deliberate divergence.**

The counter's key is **`(Year, Scope)` with no tenant term**, and the entity is **not** `ITenantEntity`
— the tenant-global lane ADR-0010 establishes, not the tenant-scoped default.

This is forced, not chosen. A tenant-keyed counter under a globally-unique index means tenant A and
tenant B both allocate ordinal `1` in 2026, both produce `2026000001`, and the second insert is
rejected by the index — **turning a tenancy fact into a 500 on the payroll path.** The two must agree,
and §D3.1 fixes which one wins.

**The cost, named rather than discovered later.** Under activated multi-tenancy (ADR-0028 is an
*activation pack*, not a live state), a tenant admin can infer platform-wide payout-invoice volume from
the gaps between their own consecutive symbols. This is accepted: it is a low-severity business-volume
inference, it is noisy (failed commits gap the sequence too, §D2.4), and the alternative buys it by
either narrowing the reference namespace below the payer's or taking on the `NULLS NOT DISTINCT`
retrofit CLAUDE.md warns about. **If it is ever worth flipping, the change is bounded and written
down:** add a tenant term to the counter key, and replace the index with
`(TenantId, VariableSymbol) UNIQUE … NULLS NOT DISTINCT WHERE VariableSymbol IS NOT NULL` — which,
per CLAUDE.md, is an owner-only `ef-migration` that fails on pre-existing duplicates, so it must be
done while the set is small or empty.

### D4 — Backfill

**D4.1 — No automatic backfill. Ever.** An invoice's PDF is the artifact the number exists for. A
symbol written onto a row whose stored PDF does not print it creates a reference that renders as
authoritative on three surfaces — admin web (`invoice-detail.component.html:151`), partner web
(`:93`), and the iOS partner "References" card (`InvoiceDetailContent.swift:182-184`) — and appears on
**no document.** That is strictly worse than NULL, because NULL is honestly empty.

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

**D4.4 — For an invoice already paid against no reference, the compensating record already exists and
becomes mandatory.** `MarkInvoicePaid` gains one validator rule: **`BankTransferNote` is required when
the invoice's `VariableSymbol` is null.** The bank's own transaction identifier goes there;
`MarkAsPaid` already persists it (`EmployeeInvoice.cs:252-269`). For a symbol-bearing invoice the note
stays optional — the symbol *is* the reference and demanding a second one is friction. The rule is
narrow on purpose: *if you cannot reference the payment forward, you must reference it backward.*

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

**D5.2 — The constant symbol stays conditional** (`:184-185`). The two are not symmetric and must not
be "made consistent". A *konstantní symbol* is legitimately absent outside CZ, and T-0522 ruled
explicitly that *"printing a guessed symbol is worse than omitting the field, which the layout already
does cleanly"* — SK is deliberately null. A *variabilní symbol* is **never** legitimately absent.

**D5.3 — `InvoicePdfData.PaymentReference` is deleted, along with the mapper line that fills it**
(`InvoicePdfData.cs:8`, `FileExtensions.cs:40`). It was never a fallback:

- it is populated with the **invoice number** (`EmployeeInvoice.cs:126`, `PaymentReference = invoiceNumber`),
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

**D5.6 — How this interacts with the invoice rebuild — and it is the opposite of "mine lands first".**

T-0522 is `in_review` and carries a **live, pending, owner-only `ef-migration`**: three nullable columns
on `CountryInvoiceConfigs` (`ConstantSymbol`, `LegalDisclaimerLanguageCode`, `LegalDisclaimerReviewStatus`).
Its own Review states the consequence in bold: *"`Cleansia.Core.Domain` declares the property now, so
until the migration is regenerated every query against `CountryInvoiceConfigs` fails with `column
c."ConstantSymbol" does not exist` — which takes the invoice PDF path down."*

Therefore:

- **The invoice PDF path is down at HEAD** pending that migration. Shipping this ADR's work without it
  changes nothing observable, because the document does not render either way.
- **This ADR's schema delta — one new counter table — must ride the same drop-and-regenerate pass.**
  Pre-prod, `Initial` is regenerated rather than stacked (CLAUDE.md, *Manual Steps*), so there is no
  ordering problem, but there **is** a both-or-neither property.
- **The layout edits in D5.1 / D5.3 are edits to T-0522's shipped layout**, not to the pre-T-0522 one.
  Every line number in this section was read at HEAD, after `8ca77412` and `946200c1`.
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
`DbIdempotencyGuard.cs:42-45`; `StripeSubscriptionWebhookHandler.cs:203,236-244` (whose comment states
the general rule: *"this handler does NOT own its own commit… so FLUSH the insert HERE and own the
failure"*).

- **New error key:** `BusinessErrorMessage.InvoiceReferenceUnavailable = "invoice.reference_unavailable"`,
  with `api.invoice.reference_unavailable` in all five locales **on the admin app** — under `api.*`,
  **not** `errors.*` and **not** through an `XXX_ERROR_KEY_MAP` (CLAUDE.md; admin's `errors.*` block is
  legacy-but-live and new work does not extend it). The parity guard
  `apps/cleansia-admin.app/src/app/i18n/error-contract-parity.spec.ts` asserts it against
  `BusinessErrorMessage.cs` directly, so a missing locale fails a test rather than silently rendering
  *"An error occurred. Please try again."*
- **What the admin sees:** a refusal naming the payment reference, on a screen where clicking again
  works. **No invoice row is created**, no order-pay is assigned, nothing is half-done.

**D6.3 — What the batch does.** Nothing global. With D2.3(a)'s per-employee commit, the violation is
raised and collapsed inside `GenerateInvoiceForEmployeeAsync`, inside the `try` at
`PayPeriodBackgroundService.cs:235-260`: **one cleaner is skipped and logged; every other cleaner in
the group is invoiced.** Their pays remain unassigned, so the next run retries them.

Without D2.3, this is not achievable at any price: the commit is at `:187`, outside every per-employee
`try`, **after** every cleaner has already been emailed a PDF. That is why the ordering rule is in this
ADR and not deferred.

**D6.4 — One named exception to the queue consumer's ack rule, because the default is wrong here.**
`GenerateInvoiceHandler.cs:72-80` acks **every** `!IsSuccess` result, on the reasoning that *"retrying
won't change the verdict"*. For `invoice.reference_unavailable` that reasoning is false — a retry
allocates a **different** number. So this one error must **throw**, so the queue retries under
`maxDequeueCount` and poisons only if it persists. It is called out here because the handler's own
comment would otherwise justify swallowing it, and a swallowed one is an invoice that never exists and
nobody is told about.

### D7 — What must happen to `PayoutInvoicePdfDataTests`

**The named anti-pattern, applied to itself.** `patterns-backend.md:443-462` — *"A fixture that
supplies an input production never produces makes the test green and the feature dead… for each
arranged value, name the production code that produces it. If the answer is 'the test does', the test
is pinning the layout, not the feature."* Two tests fail that check:

- `Variable_Symbol_Is_Not_Derived_From_The_Invoice_Number` (`:136-145`)
- `Variable_Symbol_Is_Carried_Through_And_Stays_A_Valid_Numeric_Symbol` (`:150-160`)

Both arrange with `invoice.SetVariableSymbol(EmployeeInvoice.GenerateVariableSymbol("emp-1","period-1"))`
— a call **no production code makes**. And the doc comment above them (`:147-149`) asserts *"the
generated numeric symbol is what reaches the document"*, **which is false in production and checked by
nothing.** Worse, the first of the two would pass **vacuously** against a null symbol: `null` is not
equal to an invoice number.

**What must happen — deletion by compiler, not by memory.**

1. **`GenerateVariableSymbol`, `StableHash` and `SetVariableSymbol` are deleted** (`EmployeeInvoice.cs:212-216`,
   `:340-360`). Both tests then **fail to compile**, which is the point: the arrangement is removed by
   the build, not by a reviewer noticing.
2. The five `EmployeeInvoiceEntityTests.GenerateVariableSymbol_*` tests (`:122-164`) go with them,
   including the T-0244 `[Theory]` with its hard-coded `"1883454606"` / `"1883676987"`. **T-0244 is
   superseded, not reverted** — its finding (a per-process hash basis is a fiscal-reference trap) was
   correct, and this ADR agrees with it harder: the fix for a hash whose basis is unstable is not a
   stable basis, it is not hashing.
3. **The false doc comment is deleted with the tests it describes.** It is not rewritten around a
   different mechanism; a comment that asserted an untrue thing for four months has earned deletion.

**The replacements, and the one that would actually have caught this:**

| # | Test | Where | Why it is the honest one |
|---|---|---|---|
| 1 | N concurrent invoice creations in one pay period → N **distinct** symbols, zero exceptions | `Cleansia.IntegrationTests`, real Postgres — the direct analogue of `FiscalCounterAllocatorTests` | This is the test the current design **cannot pass** and that **no current test attempts** (§Context §3) |
| 2 | Census: **every** production path that constructs an `EmployeeInvoice` yields a non-null symbol matching `^[1-9][0-9]{9}$` — built through the real handler and the real background service | `Cleansia.IntegrationTests` | This is the assertion whose absence is the whole bug |
| 3 | One test through the **real mapper → real `QuestPdfService`** asserting the symbol reaches the rendered document, and the ticket **rasterizes and looks at it once** | `Cleansia.Tests` + the ticket's evidence | `patterns-backend.md:459-462` — *"a field-model assertion and a rendered document are different claims"* |
| 4 | The first digit is never `0` | `Cleansia.Tests` | The old regex `^\d{10}$` (`EmployeeInvoiceEntityTests.cs:137`) explicitly **permitted** the §3 hazard |

### D8 — What the owner must decide, that I deliberately did **not** default

**These are here, verbatim, for the PM to file. This ADR does not write to `questions/open.md`.**

> **Q-VS-01 — [blocking: no] Is a Czech *variabilní symbol* really at most ten numeric digits, and is
> a bare sequence acceptable to your accountant as the reference on a self-billed payout invoice?**
> The platform already encodes "numeric, ≤10" in four places — `EmployeeInvoice.cs:71`
> (`[MaxLength(10)]`), `EmployeeInvoiceEntityConfiguration.cs:73-75`, `Initial.cs:1518`
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
> an invoice, §D4.4 applies to a real reconciliation and you should put the bank's transaction id into
> the "bank transfer note" field when you mark it paid, because that row will never have a symbol.
> **Default taken:** build the command, gate it hard, and do not backfill anything automatically.

**Deliberately NOT escalated, and decided here instead** — recorded so nobody re-opens them as owner
questions:
- The format (`YYYY`+ordinal vs per-period vs pure sequence) — an engineering choice with a stated
  rationale (§D1), not a business one.
- Whether the column becomes `NOT NULL` — a DB Master call with a written precondition (§D4.5).
- Whether the counter is tenant-scoped — forced by §D3.1, not a preference (§D3.2).

### D9 — Alternatives considered and rejected

| # | Alternative | Why not |
|---|---|---|
| **A** | **Widen the hash** (SHA-256 → mod 10¹⁰), the remedy `planning/done/security-remediation-summary.md:281` suggested and T-0244 half-took | The same design with a smaller probability. Ten digits is ~33.2 bits against ~260 bits of identity — **no hash is injective here** (§D1). And the owner reconciles real money against it: "unlikely" is not a property you can explain to a bank. **Explicitly forbidden by the brief, and independently rejected.** |
| **B** | **Keep the hash, catch the 23505, and retry with a salt** | Produces a symbol that is no longer a function of anything, so it is an allocator with extra steps — and a worse one, because the retry is unbounded and the collision rate grows with the year. If you are going to coordinate, coordinate first. |
| **C** | **VS = the invoice number** (the owner's specimen shows them coinciding at `20240001`) | Ruled out by the owner: *"VS can't equal the invoice number. These are 2 different and there is a separate property for it"* (T-0522 AC4). Independently impossible: `InvoiceNumber` is `INV-yyyyMM-XXXXX` (`EmployeeInvoice.cs:111`), non-numeric, and its five-character `Guid` slice is not unique by construction either. |
| **D** | **VS = a per-cleaner stable number; the period goes in the *specific* symbol** — the design the seed data implies (`SpecificSymbol` seeded `'2501'`/`'2412'`, `insert_employee_invoices.sql:46,92`) | This is a real CZ idiom and it is the strongest alternative. Rejected for two reasons: (i) the existing UNIQUE index on the bare `VariableSymbol` column would **reject the same cleaner's second invoice**, so it costs an index change on day one; (ii) with two of one cleaner's invoices unpaid at once, a statement line carrying only the VS is **ambiguous** — the reconciliation question has two answers, which is the failure this ADR exists to remove. Recorded because the dead `SpecificSymbol` column is the fossil of it (§D5.5). |
| **E** | **Reuse `FiscalCounters` with a `payout-invoice` `IssuerScope`** — zero new tables | Genuinely tempting and the closest call in this ADR. Rejected on three counts (§D2.1): its key is tenant-leading and the payout namespace must not be (§D3.2); its contract is **gaplessness** for CZ EET / DE TSE / AT RKSV (`FiscalCounter.cs:7-23`) and this counter is deliberately gappy (§D2.4); and its repository reads the ambient tenant internally (`FiscalCounterRepository.cs:19`), so bending it edits the fiscal money path to serve payroll. **The allocation *statement* is copied; the *table* is not.** |
| **F** | **A Postgres `SEQUENCE` + `nextval`** | Simplest and truly concurrent, and its non-transactional gap semantics are exactly right. Rejected because: it cannot reset per year without a job, so the `YYYY` prefix (§D1.2–3, the no-leading-zero property) goes with it; a bare sequence starting at 1 gives `1`, `2`, … which is an appalling reconciliation key, and starting it high is a magic number in DDL that no test can see; there is **no sequence anywhere in this schema** to pattern-match against, while the counter-row allocator is shipped, reviewed and integration-tested (`FiscalCounterAllocatorTests`); and it still needs raw SQL from a repository, so the saving is one table, not one layer. |
| **G** | **Assign in a `SaveChanges` interceptor / `IUnitOfWork` hook** | Invisible at the call site, needs SQL mid-save, and — decisively — it re-arms the exact defect: the guarantee becomes "the framework remembers", which is what a `SetVariableSymbol` nobody called already was. §D2.2's required parameter makes the guarantee the **compiler's**. |
| **H** | **Assign lazily when the PDF is rendered** | The number would exist first on a document and only later on a row; a regeneration would have to re-derive it (back to A); and `MarkInvoicePaid` needs it before any PDF is asked for. It inverts §D2.3, which is the invariant that makes the whole thing checkable. |
| **I** | **Derive from a per-employee number + a per-period number** (`EEEE`+`PPPPPP`, injective, no collision) | Correct in principle, and it is the only *derivation* that could work. Rejected on cost: **neither number exists** — `Employee` has no numeric sequence and `PayPeriod` has none, so it is **two** allocators and two migrations to avoid one. It also leaks a cleaner's platform ordinal onto a document they hand to third parties. |
| **J** | **`NOT NULL` on the column from day one** | Stronger than nullable, and attractive once the DEV drop leaves zero rows — but I **cannot verify the row count** (§D8 Q-VS-02) and §D4.3 leaves a legitimately-null set for as long as an admin takes to work through it. §D2.2's required parameter already gives the guarantee at compile time; `NOT NULL` adds a runtime backstop against hand-written SQL only. Deferred with a written precondition (§D4.5), not dropped. |

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
5. **The counter is tenant-global.** The new entity does **not** implement `ITenantEntity`, and its
   unique index carries no `TenantId` column.
6. **Ordering.** In `PayPeriodBackgroundService`, the invoice commit happens **before**
   `GenerateInvoicePdfAsync` / `UploadInvoicePdfAsync` / `SendPeriodClosedEmailAsync` for that
   employee. Read the call order, not the comments.
7. **The symbol prints unconditionally.** `DefaultInvoiceLayoutBuilder.PaymentFields` adds the variable
   symbol **outside** any `if`. The constant symbol is still **inside** one.
8. **No second reference on the document.** `grep -rn "PaymentReference" src/Cleansia.Infra.Services/`
   returns nothing.
9. **The duplicate is a result.** A test forces a 23505 on the invoice insert and asserts a
   `BusinessResult.Failure` carrying `invoice.reference_unavailable` — **not** a thrown
   `DbUpdateException`. The five admin locales carry `api.invoice.reference_unavailable`
   (`error-contract-parity.spec.ts` proves it).
10. **The consumer throws on that one error.** `GenerateInvoiceHandler` has an explicit branch for
    `invoice.reference_unavailable` that throws, with the reason in a comment.
11. **The concurrency test exists and runs on real Postgres.** N parallel creations in one period → N
    distinct symbols. A unit test with a mocked allocator does not satisfy this.
12. **Format.** Every produced symbol matches `^[1-9][0-9]{9}$`. A test asserting `^\d{10}$` does not
    satisfy this and is the old assertion.
13. **`Paid` cannot be backfilled.** The one-time assignment command refuses a `Paid` invoice with a
    business error, and there is a test named for it.
14. **The seed scripts do not carry forbidden symbols.** `insert_employee_invoices.sql` /
    `insert_employee_payroll.sql` are deleted, or contain no literal beginning with `0` in the
    `VariableSymbol` position.

## Consequences

**Positive**
- The payout invoice acquires the one field it exists to carry, and its absence becomes visible on the
  document rather than invisible in a conditional.
- "Mark this invoice paid" becomes a claim that reconciles.
- Duplicate references become impossible by construction and, if forced, become a *refusal* instead of
  a 500 or a poisoned batch.
- The batch's *"email the PDF, then commit the row"* ordering — a latent defect wider than this ADR —
  is closed as a by-product of the invariant, not as a separate discovery.
- One more derived-identifier trap leaves the codebase; the pattern (*claim it, don't compute it*) is
  now stated where a future author will look.

**Negative / accepted**
- One new table and one owner-only migration — which is why it must ride T-0522's already-pending pass
  (§D5.6) rather than asking for a second one.
- Gaps in the sequence, permanently and by design (§D2.4).
- A cross-tenant volume inference channel under activated multi-tenancy, with the flip written down
  (§D3.2).
- A finite, frozen set of symbol-less invoices, reachable only through a hard-gated admin command.
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
filed at `agents/backlog/questions/open.md:2097-2102`). The challenge file stays as the record; it is
not duplicated here.

**Disposition of the six places the author pre-named as attack surfaces** (below): #2 became
**CH-VS-1** and stands. #1 and #5 became **CH-VS-10** and **CH-VS-11(a)** and stand as amendments.
#3 became **CH-VS-2/CH-VS-8** and stands. #6 (§D6.4's carve-out) was attacked and **held** —
challenge "found sound" #8. #4 (§D4.3 with possibly zero rows) was not pressed; it is Q-VS-02, which
is **not filed** — see the Verdict's note to the PM.

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
| **CH-VS-7** | **STANDS (amendment)** — and gets a **third** why-not the challenger did not have | `deliberation.md:61-62` requires alternatives in the record and D9's ten rows do not include "allocate on a row that already exists". The rejection is now stronger than the challenger's own: **`PayPeriod.Update` mutates `StartDate`** (`PayPeriod.cs:76`, assignment at `:94`), so the reference's prefix would be a mutable column. D9-F's two weak clauses go. → **R7** |
| **CH-VS-8** | **STANDS — conceded** | `PayPeriodBackgroundService.cs:352/:359/:360/:361/:371` confirmed: the invoice is mutated three times **after** the render/upload. Under "commit" singular those rides the next commit. Loop structure confirmed: `foreach tenantGroup` `:133` → `foreach period` `:144` → `foreach employee` `:219`; `period.Close()` at **`:148`**, one level up from the employee loop; tenant override `:138-142`; group commit `:187`. Ruled jointly with CH-VS-2. → **R2** |
| **CH-VS-9** | **STANDS (amendment)**, census extended by two | `PayoutInvoiceLayoutTests.cs:292` confirmed — and the challenger **missed `:64`**, `Assert.Contains(fields, f => f.Value == "0001000001")`, which is the assertion that would keep it green. The three `"VS …"` notes it lists are *assertions*; their sources are `MarkInvoicePaidAdminOnlyTests.cs:68` (a default parameter) and `MarkInvoicePaidTests.cs:77`. The twelve-fixture census reproduces **exactly**: 14 call sites of `Create`/`CreateFromOrderPays`, 2 production, 12 fixture. → **R8** |
| **CH-VS-10** | **STANDS (amendment)** | Reinforced by an event later than the challenge: **Q-VS-03 is now filed** (`questions/open.md:2097-2102`). You cannot ask the owner whether the premise holds and call the conclusion "forced" in the same document. → **R9** |
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
   `BusinessErrorMessage.InvoiceReferenceMissing = "invoice.reference_missing"` whose message names the
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
(`questions/open.md:2097-2102`). The flip is written down and **must be re-examined the moment that
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
   (`BusinessErrorMessage.InvoiceReferenceCapacityExhausted = "invoice.reference_capacity_exhausted"`)
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
D4.3 and **no** note dialog; two `api.*` keys ×5; one `consistency.md` edit.

**R14 — reviewer-check delta** (§"How a reviewer verifies compliance"). Checks 1–4, 7–14 stand as
written; #9's key list grows.
- **#5 replaced:** *"The counter's key is exactly `(Year)`. Open the entity: no `Scope`, no `TenantId`,
  does not implement `ITenantEntity`; `Year` is a non-nullable `int`; the unique index is on `(Year)`
  alone. **A key with any second column fails this check.**"*
- **#6 replaced:** *"**Two** commits per employee in `PayPeriodBackgroundService`. C1 sits between
  `Add`/`AssignToInvoice` and `GenerateInvoicePdfAsync`; C2 sits after
  `SetPdfBlobUrl`/`ClearPdfGenerationError` **and** after `SetPdfGenerationError` in the catch. **A
  single commit per employee fails this check.** Read the call order, not the comments."*
- **#9 extended:** the five admin locales carry `api.invoice.reference_unavailable`,
  `api.invoice.reference_missing` **and** `api.invoice.reference_capacity_exhausted`, proved by
  `error-contract-parity.spec.ts`.
- **New #15:** *"A failed C1 is followed by `Rollback()` at that call site, and the ADR's
  tracker-scope sentence is repeated as a comment there."*
- **New #16:** *"The allocator carries a sanctioned-exception doc-comment in the
  `PromoCodeRepository.cs:28-38` shape, `consistency.md`'s post-commit deviating-form list names it as
  the second exception, and no allocator call site sits inside a `BeginTransactionAsync` scope."*
- **New #17:** *"The cap is in the SQL: the `DO UPDATE` carries `WHERE "Value" < 999999`, and an empty
  `RETURNING` result maps to `invoice.reference_capacity_exhausted` — not to an unguarded
  `allocated[0]`."*
- **New #18:** *"`MarkInvoicePaid` refuses a null-symbol invoice with `invoice.reference_missing`, and
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
