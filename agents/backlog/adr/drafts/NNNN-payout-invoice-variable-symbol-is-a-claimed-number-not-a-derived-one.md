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

*(Challengers write here. Author has not been challenged yet — `process/deliberation.md` step 2.)*

**Where the author expects to be attacked, named so a challenger does not have to find them:**

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

*(Author fills after the challenge.)*

## Verdict

*(Lead fills. A challenge stands unless defended or conceded.)*
