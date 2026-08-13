# ADR-NNNN (draft) — Challenge: *the variabilní symbol is a claimed number, not a derived one*

**Mode:** challenger. **Gate 0:** every number and every quotation below was read out of a file in the
working tree on 2026-08-08 and is cited at `file:line`. **No shell, no `git`, no test run, no
database** — Read / Glob / Grep / Write only. I wrote no application code, edited no ADR, and touched
nothing under `src/`. Where I could not verify something without a shell I say so instead of inferring
it, because inferring-instead-of-reading is what produced CH-VS-1.

---

## Headline

**The core decision survives.** *Claim the number, do not compute it* is right, the counter-row
allocator is the right mechanism, printing unconditionally is the change that would have caught the
bug, and refusing to backfill a printed document is correct. I attacked all four and could not break
any of them; §"Found sound" lists what I checked so the lead does not read silence as assent.

What does not survive is the layer of supporting claims underneath, and it fails in seven independent
places:

1. **§D5.6's load-bearing factual claim — "the invoice PDF path is down at HEAD" — is false.** All
   three T-0522 columns are in `Initial.cs:556-558`. The draft took it from a ticket's status log
   rather than from the migration, which is exactly what its own §Method declaration #2 forswears.
   Three sequencing statements and one cost line rest on it. (CH-VS-1)
2. **§D6.3's central promise — "one cleaner is skipped; every other cleaner in the group is
   invoiced" — is not achievable with the shipped primitives**, and the call that would make it
   partly true (`Rollback()`) is never named. Its shipped implementation is *context-global*
   (`CleansiaDbContext.cs:107-113`). (CH-VS-2)
3. **§D4.4 fires on a population that cannot reach it and bricks the admin's only mark-paid button
   for the population that can.** `BankTransferNote` has exactly one writer and it is unreachable
   once an invoice is `Paid`; the admin UI sends the field as `undefined` unconditionally. (CH-VS-3)
4. **The allocator's `Scope` column is unspecified and sits in the `ON CONFLICT` arbiter.** Get it
   null and every invoice for a year gets `2026000001`; set it per tenant and D3.2's collision comes
   straight back — while reviewer check #5 passes. (CH-VS-4)
5. **§D2.4 states a property of the *caller* as a property of the API**, and the file it copies
   documents the opposite for the same statement (`FiscalCounterRepository.cs:28-30`). (CH-VS-5)
6. **§D1's pigeonhole argument proves too much, and §D9-I is its counterexample** — in the same file.
   The conclusion survives on a corrected lemma; the argument as written must not be the thing a
   future author is told is "settled and not re-litigated". (CH-VS-6)
7. **§D2.3(a) is under-specified and silently loses `PdfBlobUrl` / the PDF-error state**, because the
   invoice is mutated *after* the render. (CH-VS-8)

**Twelve findings. CH-VS-1, 2, 3, 4, 5, 6 and 8 I consider blocking.**

### Citation sampling (Gate 0)

I spot-checked twenty of the draft's own citations. **Eighteen are exact**, including every one in the
§Context table, `UnitOfWorkPipelineBehavior.cs:20-30`, `EmployeeInvoiceEntityConfiguration.cs:116-118`,
`Initial.cs:1518` / `:2651-2655`, `FiscalCounterRepository.cs:33-42`, `PromoCodeRepository.cs:33-38`,
`FiscalCounter.cs:7-23`, `GenerateInvoiceHandler.cs:72-80`, `PdfComponentExtensions.cs:96,132`,
`DefaultInvoiceLayoutBuilder.cs:181-185`, `RegenerateInvoicePdf.cs:137`,
`Cleansia.Infra.Scripts.csproj:10-17`, `insert_employee_invoices.sql:10,22,33,46,…`,
`invoice-detail.component.html:151` (admin) and `:93` (partner),
`InvoiceDetailContent.swift:182-187`. **Citation hygiene is genuinely good.** Two drift (CH-VS-12),
and one whole section is factually wrong for a reason that is not citation drift at all (CH-VS-1) —
it is a claim inherited from a ticket note.

---

## CH-VS-1 — §D5.6's "the invoice PDF path is down at HEAD" is false. The three columns it names are in the committed migration, the Designer and the snapshot. **BLOCKING**

**The claim.** §D5.6: *"T-0522 … carries a **live, pending, owner-only `ef-migration`**: three nullable
columns on `CountryInvoiceConfigs` (`ConstantSymbol`, `LegalDisclaimerLanguageCode`,
`LegalDisclaimerReviewStatus`) … **The invoice PDF path is down at HEAD** pending that migration.
Shipping this ADR's work without it changes nothing observable, because the document does not render
either way."*

**The evidence.** `src/Cleansia.Infra.Database/Migrations/20260723182623_Initial.cs:545-559`:

```
LegalDisclaimerLanguageCode = table.Column<string>(type: "character varying(5)",  maxLength: 5,  nullable: true),   // :556
LegalDisclaimerReviewStatus = table.Column<int>(   type: "integer",                              nullable: false),  // :557
ConstantSymbol              = table.Column<string>(type: "character varying(4)",  maxLength: 4,  nullable: true),   // :558
```

and the same three properties in the Designer (`20260723182623_Initial.Designer.cs:1830, :1848,
:1852`) and in the model snapshot (`CleansiaDbContextModelSnapshot.cs:1827, :1845, :1849`). Domain,
snapshot and migration agree. `Initial` was regenerated in place — the repo's pre-prod practice
(CLAUDE.md, *Manual Steps*) — and the file is committed.

**Where the claim came from.** Not from the tree. `T-0522-…md:203-206` (status log, 2026-08-04) says
*"`manual_steps: ef-migration` is BACK ON and is real … because the domain declares it now, the
invoice path is down until the migration is regenerated."* That sentence was true when written; the
ticket has **no status-log entry after 2026-08-04** and was never updated when the regeneration
landed. The draft quotes it as present tense. Its own §Method declaration #2 — *"No claim is
inherited. The brief that commissioned this ADR was re-verified line by line"* — was applied to the
brief and not to the ticket.

**Why it matters — four statements move.**

- *"This ADR's schema delta … must ride the same drop-and-regenerate pass"* — **there is no pending
  pass to ride.** The new counter table is a migration request of its own, and the Consequences line
  *"which is why it must ride T-0522's already-pending pass rather than asking for a second one"*
  now asks for exactly the second one it says it avoids. That is the ADR's only cost mitigation for
  the owner-only step and it is gone.
- *"Shipping this ADR's work without it changes nothing observable"* — **false, and in the risky
  direction.** The document renders today. D5.1 (print unconditionally) and D5.3 (delete
  `InvoicePdfData.PaymentReference`) are live layout changes to a document the owner sends to
  cleaners, landing before any symbol exists — i.e. every invoice starts printing `Variabilní symbol
  —`. That may well be the right sequencing (it is D5.1's whole argument that absence should be
  loud), but it must be *decided*, not arrived at under a false belief that nothing renders.
- The "both-or-neither" property between this ADR and T-0522 does not exist.
- Detail: `LegalDisclaimerReviewStatus` is `nullable: false` (`:557`), so *"three nullable columns"*
  is wrong; `ConstantSymbol` is `varchar(4)`, not stated.

**What the author must answer.** Either produce a migration/DDL fact at HEAD that shows a *different*
pending column taking the PDF path down, or rewrite §D5.6 end-to-end: state that the T-0522 columns
are shipped, that this ADR's table needs its own owner-only pass, and re-decide the D5.1/D5.3
sequencing knowing the document renders today. Also: update the "Applies to" cost line.

*(I could not run `dotnet ef migrations list`. I am claiming only that **the three columns §D5.6
names** are present and consistent across migration, Designer and snapshot — not that no migration is
pending anywhere.)*

---

## CH-VS-2 — §D6.3's "one cleaner is skipped; every other cleaner in the group is invoiced" is not achievable with the shipped primitives, and the ADR never names the call that would make it even partly true. **BLOCKING**

**The claim.** §D6.2: *"The invoice insert is **flushed** where the violation can be caught, and a
Postgres `23505` is collapsed into a result — the idiom this codebase already ships in four places."*
§D6.3: *"the violation is raised and collapsed inside `GenerateInvoiceForEmployeeAsync` … **one
cleaner is skipped and logged; every other cleaner in the group is invoiced.**"*

**The evidence — the four cited precedents all do a fifth thing the ADR omits.** `RefundService.cs:97-104`:

```csharp
try   { await refundRepository.CommitAsync(cancellationToken); }
catch (DbUpdateException ex) when (IsUniqueViolation(ex))
{
    refundRepository.Rollback();                      // ← :103
    var winner = await refundRepository.GetByRefundKeyAsync(refundKey, cancellationToken);
```

`Rollback()` is `BaseRepository.Rollback()` (`src/Cleansia.Infra.Database/BaseRepository.cs:181-184`)
→ `CleansiaDbContext.Rollback()` (`:107-113`):

```csharp
public void Rollback()
{
    foreach (var entry in ChangeTracker.Entries())
        entry.State = EntityState.Unchanged;
}
```

**It is context-global.** It reverts *every* tracked entry, not the failed one.

**Why it matters — both branches break §D6.3.**

- **Without `Rollback()`:** EF Core does not revert tracked state on a failed `SaveChanges`. The
  violating invoice stays `Added` and the `orderPay.AssignToInvoice(...)` mutations stay `Modified`,
  so the **next** employee's commit re-attempts the same violating INSERT — and the one after that,
  and so on. The whole remainder of the tenant group fails, not one cleaner. §D6.3's claim is
  exactly inverted.
- **With `Rollback()`:** it discards every other cleaner's tracked invoice and pay-assignment
  accumulated in the same tracker since the last commit. And if the failure lands on the *first*
  employee of a period, it also reverts `period.Close("System", …)` — which is executed at
  `PayPeriodBackgroundService.cs:148`, **outside** and **before** the employee loop — so the period
  stays `Open`, the sweep re-selects it on the next tick (`:119-122`), and every cleaner in it is
  emailed a period-closed mail a second time.

The two failure modes are only separable if the commit granularity is per employee (D2.3(a)) **and**
the rollback fires immediately after each failed per-employee commit **and** `period.Close()` has
already been committed by an earlier employee in the same period. None of those three is stated.

**What the author must answer.** Write the full sequence — flush, catch, `Rollback()`, collapse — and
state the tracker-scope consequence in the ADR body, not in a ticket. Then either (a) prove the
`period.Close()` interaction is safe (it is not, for the first-employee case) or (b) hoist the period
close/commit above the employee loop so a per-employee rollback can never revert it. If neither, drop
§D6.3's claim to what is actually true: *"a duplicate fails that employee's commit and the ADR does
not guarantee the rest of the group survives it."*

---

## CH-VS-3 — §D4.4's required note fires on a population that cannot reach it, is dominated by §D4.3 on the population it can, has no UI to satisfy it, and null-derefs where it would naturally be written. **BLOCKING**

**(a) For "an invoice already paid against no reference", the compensating record does *not* already
exist and cannot be created.** §D4.4: *"For an invoice already paid against no reference, the
compensating record already exists and becomes mandatory."* `BankTransferNote` has **exactly one
writer** in the whole tree — `EmployeeInvoice.MarkAsPaid` (`EmployeeInvoice.cs:252-269`, assignment at
`:261`) — and it throws unless `Status == Approved` (`:254-257`). `MarkInvoicePaid` refuses an already
-`Paid` invoice three times over: the validator (`MarkInvoicePaid.cs:46-47`), `RefusalFor`
(`:24-30`), and the handler's re-check (`:93-98`), and `MarkInvoicePaidTests.cs:162-171` pins that a
second mark does not overwrite the first note. So there is **no path at all** to attach a note to an
invoice that is already paid. D4.4 does nothing for the population its own sentence names.

**(b) On the population it *can* reach, it is strictly dominated by §D4.3.** The rule fires only at
the moment of the first mark-paid, i.e. on `Status == Approved ∧ VariableSymbol IS NULL`. That is a
**strict subset** of §D4.3's eligible set (`{Pending, Approved, Disputed} ∧ ¬IsCancelled ∧
VariableSymbol IS NULL`). So every invoice the mandatory-note rule can ever fire on is an invoice the
admin could have fixed one click earlier with the *stronger* control — allocate a real symbol and
regenerate the document. The ADR makes the weak control (unvalidated free text, `varchar(500)`,
`EmployeeInvoiceEntityConfiguration.cs:83-84`, with no format, no uniqueness and no reader other than
three detail screens) **mandatory**, and the strong one optional, and never says why. The seeded
example of what such a note actually looks like in this codebase is
`insert_employee_invoices.sql:46` — `'Payment for Invoice INV-202501-ZH001'`. That reconciles
nothing; it restates the invoice number.

**(c) There is no UI that can satisfy it.** `invoice-detail.component.ts:106-108`:

```ts
onMarkPaid(): void { this.facade.markAsPaid(); }     // no argument
```

and `invoice-detail.facade.ts:79-88` sets `command.bankTransferNote = bankTransferNote` from that
absent parameter — i.e. `undefined` — every time. `bankTransferNote` appears in the admin app only as
a *display* (`invoice-detail.component.html:268-275`). There is no input control anywhere. A
required-when-null rule therefore fails **100 %** of mark-paid attempts on the null-symbol population
(which is, per §Context §2a, every invoice created by production code) with an error the admin
physically cannot satisfy. §Applies-to scopes the admin app as *"one new `api.*` key ×5"* — a note
dialog is not in it.

**(d) Where the rule would naturally be written, it null-derefs.** The existing
`RuleFor(x => x.BankTransferNote)` (`MarkInvoicePaid.cs:53-55`) is a **separate root rule**;
FluentValidation's class-level default is `Continue`, so it runs even when the `InvoiceId` chain
already failed `ExistsAsync` (`:44-45`). A new `.MustAsync(...)` there would call
`GetByIdAsync(invoiceId)` and read `.VariableSymbol` on `null` → a 500 on a bad id. The three existing
`invoice!` reads (`:65, :71, :77`) are safe **only** because they sit inside the `Cascade.Stop` chain
on `InvoiceId`. The ADR must say the new rule joins that chain, after `ApprovedAsync`.

**What the author must answer.** Pick one and write it: (i) drop D4.4 and make the D4.3 assignment a
**precondition** of mark-paid on a null-symbol invoice (refuse with a business error naming the fix) —
this is cheaper, has no UI cost, and produces a real reference instead of prose; or (ii) keep D4.4,
scope the admin dialog change in §Applies-to, say what the note must contain and how it is validated,
place the rule in the `Cascade.Stop` chain, and **delete the "already paid" sentence**, which the
mechanism cannot serve.

---

## CH-VS-4 — the allocator's `Scope` column is unspecified and sits in the `ON CONFLICT` arbiter. Null it, and every invoice in a year gets `2026000001`; tenant it, and D3.2's collision is back — with reviewer check #5 green. **BLOCKING**

**The claim.** §D2.1's statement is `ON CONFLICT ("Year","Scope") DO UPDATE SET "Value" = … + 1
RETURNING "Value"`. §D3.2: *"The counter's key is `(Year, Scope)` with no tenant term."* The ADR never
says what `Scope` holds, who supplies it, how many distinct values it has, or that it is `NOT NULL`.

**The evidence that this is a live hazard, not a nit.** The file the ADR copies documents the
identical failure for its own key — `FiscalCounterRepository.cs:30-32`:

> *"The unique index is NULLS NOT DISTINCT, so a null TenantId (single-tenant) collapses onto one
> counter row instead of inserting a duplicate per call."*

Postgres matches an `ON CONFLICT (a, b)` arbiter against a unique index on `(a, b)`, and under the
default NULLS-DISTINCT semantics a NULL `Scope` **never conflicts**. Every allocation then INSERTs a
fresh row and `RETURNING "Value"` returns `1` — the same symbol, forever — caught only downstream by
`IX_EmployeeInvoices_VariableSymbol`, i.e. as exactly the post-commit duplicate this ADR exists to
prevent. CLAUDE.md's own warning about nullable columns in unique keys is the general form of this.

And the *non*-null failure is worse, because it looks correct. `FiscalCounter.IssuerScope`'s
doc-comment (`FiscalCounter.cs:13-18`) explicitly trains the reader that the scope string *"is the
extension point"* and that it *"binds gaplessness to the legal counting unit … NOT merely the
tenant"*. A future author reading that, holding a payout counter with a `Scope` column and a
requirement to isolate tenants, will set `Scope = tenantId`. Each tenant then restarts at ordinal 1,
both produce `2026000001`, and the second insert is rejected by the global index — precisely the 500
on the payroll path §D3.2 says the global counter exists to prevent. **Reviewer check #5 ("the new
entity does not implement `ITenantEntity`, and its unique index carries no `TenantId` column") passes
while this is true**, because `Scope` is not named `TenantId`.

**What the author must answer.** Either (i) delete `Scope` — the key is `(Year)`, one row per year,
and the ADR loses a column it cannot justify; or (ii) keep it, declare it `NOT NULL` with a domain
constant (`PayoutReferenceScope.Default`) as its **only** value, say so in D2.1, and replace reviewer
check #5 with one that has teeth: *"the counter's unique index is exactly `(Year, Scope)`, `Scope` is
`NOT NULL`, and `SELECT DISTINCT "Scope"` returns one row."* A column whose whole safety depends on
nobody ever giving it a second value must say so where the next author will read it.

---

## CH-VS-5 — §D2.4 states a property of the *caller* as a property of the API, and the file it copies documents the opposite for the same statement. **BLOCKING**

**The claim.** §D2.4: *"The allocation is not rolled back with the invoice, by design.
`Context.Database.SqlQueryRaw` **auto-commits**: there is no ambient transaction on the command path …
and this is the same declared exception `PromoCodeRepository.cs:33-38` documents."*

**The evidence.** The very file §D2.1 copies the statement from says the opposite —
`FiscalCounterRepository.cs:28-30`:

> *"Running through the context's connection **joins the caller's open transaction** (the phase-1
> claim), so the allocated number is **bound to the same commit/rollback** as the receipt row."*

Both sentences are true, *conditionally on the caller*. `SqlQueryRaw` does not auto-commit; it
auto-commits **when no ambient transaction exists**. Today none does on either payout path —
`UnitOfWorkPipelineBehavior.cs:13-33` opens none (it calls `unitOfWork.CommitAsync` at `:29` and
nothing else), and `PayPeriodBackgroundService.CloseExpiredPeriodsAndOpenNewAsync` (`:107-197`) opens
none. So the draft's **conclusion holds at HEAD**. But `BeginTransactionAsync` is on every repository
(`BaseRepository.cs:176-179`) and on the context (`CleansiaDbContext.cs:102-105`), and the fiscal path
uses it.

**Why it matters — two things invert silently the first time someone wraps this.**

1. The whole of §D2.4 (gaps are permanent; the failure table for paths 1/2/3; *"a design that
   sometimes gaps and never duplicates"*) becomes false, without a compile error and without a test
   failing. The ADR's central safety property would then be a property nobody re-checked.
2. Worse: `ON CONFLICT … DO UPDATE` takes a **row lock held until the transaction ends**. Joined to a
   long transaction — and the batch's natural unit is the whole tenant group, which spans PDF
   generation (`PayPeriodBackgroundService.cs:352`), a blob upload (`:359`) and an email send
   (`:262-272`) per cleaner — the payout counter's single row serializes every tenant's payroll run
   for the duration. That is a global contention channel introduced by a design that never mentions
   locking.

**Also, the catalog half is missing.** `patterns-backend.md:638-644` makes the post-commit law
**T3-HUMAN** and states the baseline exception explicitly: *"`PromoCodeRepository.TryIncrement…`
self-commits inside a handler as a documented, sanctioned exception, so the sentence below is scoped
to FK-referencing writes and self-committing writes **without** the sanctioned-exception
doc-comment."* This ADR introduces the codebase's **second** self-committing write inside a handler
and registers it nowhere — no sanctioned-exception doc-comment mandated in the ADR, no entry on
`consistency.md`'s deviating-form list. Per my own charter's pattern-evolution loop, that is the
architect's job to close in the same change, not the developer's to notice.

**What the author must answer.** State the invariant in D2.1 as a rule with a check — *"the payout
reference allocator MUST NOT be called inside an explicit transaction; the gap semantics and the lock
duration both depend on it"* — add it to §How-a-reviewer-verifies, and add the sanctioned-exception
doc-comment + `consistency.md` entry to §Applies-to.

---

## CH-VS-6 — §D1's pigeonhole argument proves too much, and §D9-I is its counterexample in the same file. The conclusion survives; the argument must not be declared "settled". **BLOCKING (cheap to fix)**

**The claim.** §D1: *"An invoice's identity is `(EmployeeId, PayPeriodId)`, two 26-character ULIDs …
i.e. ≈260 bits. The target is ten decimal digits, ≈33.2 bits. **No function from the former to the
latter is injective.** Every derivation is therefore a hash … **The only correct designs allocate.**"*
— presented as *"the pigeonhole argument, so this is settled and not re-litigated."*

**The arithmetic re-derives.** 26 Crockford-base-32 characters × 5 bits = 130 bits per ULID; two of
them = 260. `log₂(10¹⁰) = 10 × 3.321928 = 33.219`. Both figures are right, and
`EmployeeInvoiceEntityConfiguration.cs:13-19` confirms `HasMaxLength(26)` on both id columns.

**The inference is not sound.** Pigeonhole rules out injectivity **on the full type domain**.
Injectivity is only ever *required* on the **realized** set — and this schema already pins that set:
`EmployeeInvoiceEntityConfiguration.cs:123-124` declares
`builder.HasIndex(e => new { e.EmployeeId, e.PayPeriodId }).IsUnique();`, so the realized cardinality
is (#cleaners × #periods), a number in the thousands, not 2²⁶⁰. A function can be injective on a set
of thousands into 10¹⁰ codes trivially. The ADR knows this: **§D9-I** says of `EEEE`+`PPPPPP` —
*"Correct in principle, and it is the only **derivation** that could work"* — which is a direct
counterexample to the universal claim eight sections earlier, in the same document. As written, D1
would rule out D9-I, and D9 rejects D9-I **on cost**, not on impossibility. The two sections
contradict each other.

**What actually survives, and it is a better argument.** The true lemma is about *statelessness*, not
about bit-counts:

> A function of the two ULIDs **alone** cannot be injective on any set the platform does not choose
> in advance — and it does not choose them; `Ulid.NewUlid()` does. Injectivity into ten digits
> therefore requires at least one input to be a **small ordinal the platform assigned**. Assigning a
> small ordinal *is* allocation. So every correct design allocates; D9-I is not an exception to that,
> it is an instance of it that allocates **twice**.

That restatement is stronger (it explains D9-I instead of contradicting it), it is what makes D9-I's
cost objection the right objection, and it does not rest on a bit-count that a reader can check and
find irrelevant.

**Why blocking.** Because D1 says *"so this is settled and not re-litigated."* An ADR is read later as
law. Foreclosing future argument with a lemma that is false as stated is worse than not foreclosing
it — the next author will cite D1 verbatim to reject a design that is actually fine, and will be
right to feel misled when they read D9-I.

---

## CH-VS-7 — §D9 does not list the cheapest allocator: the ordinal on the `PayPeriod` row. And D9-F's rejection of a sequence is the weakest row in the table. **Non-blocking, but must be answered**

**The alternative.** `VariableSymbol = yyMMdd(PayPeriod.StartDate) ‖ NNNN`, where `NNNN` is a
per-period ordinal allocated by the **same** atomic `UPDATE … RETURNING` shape against one new
nullable `int` column on the **existing** `PayPeriods` table.

| | §D1's counter table | this |
|---|---|---|
| new table | 1 | **0** (one additive column) |
| new entity + role card | 1 + 1 | **0 + 0** |
| first digit never `0` (D1.2) | ✓ (`2` until 9999) | ✓ (`2` for 2020–2099) |
| self-describing (D1.3) | the *allocation year* | **which pay period the line settles** |
| capacity | 999 999 / year | 9 999 / period |
| serialized allocator | ✓ | ✓ (same statement) |

It is **more** self-describing than a bare year — a statement line tells the owner which period it
settles, which is the actual reconciliation question — and it deletes an entire table, entity, role
card and reviewer check from §Applies-to. It costs a 9 999-cleaners-per-period cap, and, under
activated multi-tenancy, two tenants whose periods share a `StartDate` both allocate ordinal 1 and
collide under the global index — the *same* problem D3.2 solves, so it is **not free** and I am not
claiming it wins.

**I am claiming the record does not meet the bar.** `process/deliberation.md:61-62`: *"A decision with
a real trade-off must have its alternatives and why-not in the record."* D9 has ten rows and none of
them is "allocate on a row that already exists". The counter table is currently defended only against
a Postgres sequence and a two-allocator derivation.

**And D9-F's why-not is weak where it should be strong.** *"starting it high is a magic number in DDL
that no test can see"* — false: `modelBuilder.HasSequence(...).StartsAt(...)` lands in
`CleansiaDbContextModelSnapshot.cs`, which is where every other schema fact in this repo is asserted
from. *"there is no sequence anywhere in this schema to pattern-match against"* — that is novelty, not
a defect, from an ADR that is simultaneously introducing a new table with new semantics. The row's one
genuinely good argument — a sequence cannot be reset or inspected as an auditable row, and
`FiscalCounter` deliberately is one — is the argument it does not make.

---

## CH-VS-8 — §D2.3(a) is under-specified: the invoice is mutated *after* the render, so one commit per employee silently loses `PdfBlobUrl` and the PDF-error state. **BLOCKING**

**The claim.** §D2.3: *"Sequence: allocate → construct → add → **commit** → render → upload →
deliver"*, implemented as *"(a) commit inside the per-employee loop"*.

**The evidence.** `PayPeriodBackgroundService.GenerateInvoiceForEmployeeAsync` mutates the invoice
three times **after** the render/upload:

```
:352  var pdfBytes  = await GenerateInvoicePdfAsync(...)
:359  var pdfBlobUrl = await UploadInvoicePdfAsync(...)
:360  invoice.SetPdfBlobUrl(pdfBlobUrl);
:361  invoice.ClearPdfGenerationError();
:371  invoice.SetPdfGenerationError(ex.Message);      // the catch branch
```

Under the ADR's stated order those three mutations are left **uncommitted** and ride whichever commit
happens next — the following employee's, or the group commit at `:187`. So a failure on employee N+1
loses employee N's blob URL and error state, and "bound the blast radius to one cleaner" fails in the
opposite direction from CH-VS-2. What the invariant actually requires is **two** commits per employee:
one after `Add` (so the reference is durable before the document exists) and one after
`SetPdfBlobUrl`. The ADR says "commit", singular, and reviewer check #6 (*"the invoice commit happens
**before** `GenerateInvoicePdfAsync` / `UploadInvoicePdfAsync` / `SendPeriodClosedEmailAsync`"*) is
satisfied by the wrong implementation as well as the right one.

**Second, the loop is named imprecisely and it matters for CH-VS-2.** There are three nested loops:
`foreach tenantGroup` (`:133`) → `foreach period` (`:144`) → `foreach employee` (`:219`, inside
`SendPeriodClosedEmailsAsync`). `period.Close(...)` is at `:148` — **inside the period loop, outside
the employee loop**. The tenant override is set at `:138-142`, two levels up. §D2.3(a) says *"commit
inside the per-employee loop, still under the tenant override set at `:138-142`"*, which is true but
does not say what happens to the period-level mutation that is already tracked when the first
per-employee commit fires.

**What the author must answer.** State the two commits explicitly with what each one must contain, and
say where `period.Close()` is committed relative to the first employee. Then tighten reviewer check #6
so it fails the single-commit implementation.

---

## CH-VS-9 — §D7's deletion-by-compiler works. Its census is short by one file, and the compiler will **not** catch that one — and D2.2's required parameter re-creates the very anti-pattern D7 invokes. **Non-blocking**

**The mechanism is real — verified.** `PayoutInvoicePdfDataTests.cs:140` and `:154` both read
`invoice.SetVariableSymbol(EmployeeInvoice.GenerateVariableSymbol("emp-1", "period-1"));`. Deleting
`SetVariableSymbol` (`EmployeeInvoice.cs:212-216`) and `GenerateVariableSymbol` (`:340-345`) makes
both fail to compile. ✅ So does `EmployeeInvoiceEntityTests.cs:125-126, :134, :143-144, :161`. The
draft's claim about the removal mechanism holds.

**The gap.** `PayoutInvoiceLayoutTests.cs:292` sets `VariableSymbol = "0001000001"` **directly on the
`InvoicePdfData` record** — a leading-zero literal, the exact shape §D1.2 forbids, in a file the draft
cites two lines lower (`:294`, for `PaymentReference`). It compiles fine after the deletions and goes
on pinning a forbidden value into the rendered-layout tests. Three more of the same literal live as
`BankTransferNote` fixtures: `MarkInvoicePaidTests.cs:103`, `MarkInvoicePaidAdminOnlyTests.cs:109`
and `:181` (`"VS 0001000001"`). Reviewer check #12 (*"every **produced** symbol matches
`^[1-9][0-9]{9}$`"*) does not reach a fixture.

**And D2.2 arms the anti-pattern D7 is built on.** A required, non-defaulted `string variableSymbol`
on `Create` **and** `CreateFromOrderPays` reaches **twelve** non-production call sites, every one of
which will then hand-author a symbol:

```
Cleansia.TestUtilities/MockDataFactories/EmployeePayroll/PayrollMockFactory.cs:52   ← shared fixture
Cleansia.HostTests/Infrastructure/DomainSeed.cs:160                                 ← shared fixture
Cleansia.Tests/Features/EmployeePayroll/EmployeeInvoiceEntityTests.cs:19, :34, :58, :76
Cleansia.Tests/Features/EmployeePayroll/MarkInvoicePaidTests.cs:26
Cleansia.Tests/Features/EmployeePayroll/MarkInvoicePaidNotifyTests.cs:26
Cleansia.Tests/Features/EmployeePayroll/AdminInvoiceAdjustmentHandlerTests.cs:25
Cleansia.Tests/Functions/FiscalReconciliationQueryTests.cs:337
Cleansia.Tests/Features/EmployeePayroll/PayoutInvoicePdfDataTests.cs:195, :211
```

That is `patterns-backend.md:443-462` verbatim — *"a fixture that supplies an input production never
produces"* — the rule §D7 itself invokes to justify the deletions. The ADR has the answer (D7
replacement #2, the production census over the real handler and the real background service) but does
not connect it, and §Applies-to counts *"two creation call sites"* without counting these.

**What the author must answer.** Add `PayoutInvoiceLayoutTests.cs:292` (and the three
`"VS 0001000001"` notes) to D7's census; name one canonical fixture constant for the required
parameter; and add a reviewer check that no `VariableSymbol` literal in the tree begins with `0`.

---

## CH-VS-10 — §D3.2 says the global counter is "forced, not chosen". It is contingent on a single-payer assumption D3.1 makes and the rest of the ADR disclaims. **Non-blocking**

D3.1's load-bearing sentence: *"The requirement is about a human and a bank statement … **The payer's
account is one account.** A namespace at least as wide as the payer is mandatory; global is the only
shape that is unconditionally at least that wide."* D3.2 then prices the cost of that choice **in the
multi-tenant world** — and in that world D3.2 itself says *"a franchise operator is a different legal
entity"*. A different legal entity pays its own cleaners from its own account; a bank statement line
belongs to exactly one account; so tenant-wide is *already* "as wide as the payer" the moment the
premise stops holding. The argument's own scope ends exactly where its cost begins.

What is actually true, and is a perfectly good reason: **the shipped index is global**
(`EmployeeInvoiceEntityConfiguration.cs:116-118`, `Initial.cs:2651-2655`), it is genuinely enforcing
(no `TenantId`, so no NULLS-DISTINCT hole), production is single-tenant, and narrowing it later is an
owner-only migration that fails on pre-existing duplicates. Keeping it is the cheapest correct thing
**today**, and the flip is written down. Say that. "Forced" invites a future reader to skip
re-examining it when the premise changes, which is the one moment it must be re-examined.

---

## CH-VS-11 — the `YYYY` prefix is the year of *allocation*, not of the pay period; and "No wrap, ever" is not what the mechanism does. **Non-blocking**

**(a)** §D1.3's justification is *"The owner reconciles by eye against a statement; a year-prefixed
reference sorts and scans."* But the year is *"the four-digit UTC calendar year of **allocation**"*.
A December pay period closed on 2 January produces `2027000001` for December work — the two diverge
exactly at the boundary where accounting cares most, and the owner scanning a February statement will
read the prefix as the accounting year. Either say the prefix is deliberately the allocation year and
must not be read as the period's, or key it to `PayPeriod.EndDate`'s year (free — same counter shape).

**(b)** §D1.5: *"No wrap, ever. An ordinal above `999999` **fails the allocation** with a business
error."* `RETURNING` reports the value **after** the increment, and per §D2.4 the increment has
already auto-committed. So the counter is permanently past the cap: every subsequent allocation for
that year also fails, **platform-wide** (the counter is global, §D3.2), with no in-app recovery — only
a manual `UPDATE` on the counter row. Combined with §D6.4's "throw so the queue retries", it would
poison every queued invoice message until an owner edits the row. The direction is right (fail
closed), but "no wrap, ever" describes a property; the mechanism is a permanent hard stop with a
manual repair. Record it as such, and give the counter's own cap check a named business error and a
runbook line.

---

## CH-VS-12 — two citation drifts, one of which reverses the sentence it supports. **Non-blocking**

**(a)** §D5.3: *"`InvoicePdfData.PaymentReference` is deleted … **It was never a fallback**"*, citing
`FileExtensions.cs:40`. That line reads:

```csharp
PaymentReference = invoice.PaymentReference ?? invoice.VariableSymbol,
```

— literally a fallback expression, to the variable symbol. The **conclusion survives**: every
`EmployeeInvoice.Create` sets `PaymentReference = invoiceNumber` (`EmployeeInvoice.cs:126`), so the
right-hand side is unreachable for any row production created, and `PaymentReference` occurs **exactly
once** in all of `src/Cleansia.Infra.Services/` — the declaration at `InvoicePdfData.cs:8`, read by no
layout. But a reviewer running check #8 will open a line that says the opposite of the ADR sentence
that sent them there. Reword to *"the only fallback it has is to the variable symbol, and it is
unreachable because `Create` always sets the field"*.

**(b)** §D7 step 2: *"The **five** `EmployeeInvoiceEntityTests.GenerateVariableSymbol_*` tests
(`:122-164`)"*. The range is right; the count is **four methods** (`:122`, `:131`, `:140`, and the
`[Theory]` at `:155` with two `[InlineData]` rows) — five test *cases*.

---

## Found sound — what I attacked and could not break

Stating these so the lead knows the coverage was real and not a hunt for confirmations.

1. **The collision table re-derives, all four rows.** `p(n) ≈ 1 − e^(−n(n−1)/20000)`:
   n=25 → 1−e^(−0.0300) = **2.96 %**; n=50 → 1−e^(−0.1225) = **11.53 %**; n=100 → 1−e^(−0.4950) =
   **39.04 %**; n=150 → 1−e^(−1.1175) = **67.29 %**. The model is right too: within one pay period
   `periodHash` is a constant (`EmployeeInvoice.cs:343`), so the only varying term is
   `StableHash(employeeId) % 10000` (`:342`) — 10 000 buckets. The "lower bound under
   non-uniformity" remark is correct in direction.
2. **The leading-zero defect is real.** `empHash < 1000` ⇒ `{empHash:D4}` starts `'0'` ⇒ ~1 in 10
   under uniformity (`EmployeeInvoice.cs:342-344`). The column is a **string** — `string?` at
   `EmployeeInvoice.cs:72`, `character varying(10)` at `Initial.cs:1518` — and
   `EmployeeInvoiceRepository.GetByVariableSymbolAsync` (`:20-28`) compares with `==` on the string,
   so `'0321876543'` and `'321876543'` are different keys. The eight seed literals
   (`insert_employee_invoices.sql:22, 33, 46, 57, 68, 79, 92, 105`) are exactly that shape, and
   `EmployeeInvoiceEntityTests.cs:137`'s `^\d{10}$` does permit it. **All verified.**
3. **The two offending tests really do fail to compile once the functions are deleted.** See CH-VS-9.
4. **§D5.1 reaches the Czech invoice.** I checked whether `CzechInvoiceLayoutBuilder` overrides
   `PaymentFields` — it does not; it overrides only `CountryCode`, `CountryCodes`, `Labels`,
   `NumberCulture` and `FormatMoney` (`CzechInvoiceLayoutBuilder.cs:6-16`). So editing
   `DefaultInvoiceLayoutBuilder.PaymentFields` (`:169-191`) reaches the layout the owner actually
   uses, and reviewer check #7 is sufficient. The `—` claim also holds:
   `FieldGrid` → `LabeledField` → `value ?? "—"` (`PdfComponentExtensions.cs:102-137`, `:132`).
5. **The generator and setter have zero production callers**, and the PDF renders the field only when
   non-empty (`DefaultInvoiceLayoutBuilder.cs:181-182`). The finding that started this is true.
6. **The seed scripts are genuinely unreferenced.** `Cleansia.Infra.Scripts.csproj:9-17` copies only
   `SeedData\insert_seed_data.sql`; a repo-wide grep on both filenames returns only the files and this
   ADR. §D4.6 is right, and deleting them costs nothing.
7. **Path 3's "email the PDF, then commit the row" defect is real and worse than a failed batch.**
   `SendPeriodClosedEmailsAsync` emails inside the loop (`:262-272`) and uploads at `:359`, both
   before the `:187` commit. Closing it as a by-product is a genuine win, independent of everything
   above.
8. **§D6.4's premise is exact.** `GenerateInvoiceHandler.cs:72-80` acks every `!IsSuccess` on the
   reasoning *"retrying won't change the verdict"*, and for a reference-unavailable error that
   reasoning is false because a retry allocates a different number — and the retry really does get a
   clean run, because `GenerateInvoice.Validator`'s `NoInvoiceExistsForPayPeriodAsync`
   (`GenerateInvoice.cs:48-50, 60-62`) passes when the failed insert left no row. The carve-out is
   right.
9. **§D5.2's asymmetry is right.** The constant symbol is legitimately absent outside CZ and its
   conditional (`DefaultInvoiceLayoutBuilder.cs:184-185`) must stay; the variable symbol's must not.
   I tried to argue for symmetry and could not.
10. **§D5.4's iOS consequence is real.** `InvoiceDetailContent.swift:182-187` renders `variableSymbol`
    and `paymentReference` as separate rows, and `EmployeeInvoice.cs:126` makes the latter the invoice
    number — so until the `nswag-regen`, the References card does print the invoice number twice.
    Naming it rather than discovering it is the right call.
11. **§D9-C is doubly closed.** `InvoiceNumber` is `INV-yyyyMM-XXXXX` (`EmployeeInvoice.cs:111`),
    non-numeric, and its 5-char `Guid` slice is not unique by construction — the owner ruling and the
    mechanism agree.
12. **§D2.1's three reasons for a separate table hold.** `FiscalCounter`'s key is tenant-leading
    (`FiscalCounterRepository.cs:37`), its doc-comment promises gaplessness for DE TSE / AT RKSV / ES
    VeriFactu (`FiscalCounter.cs:7-23`), and its repository reads the ambient tenant internally
    (`:19`). D9-E is correctly rejected. *(The `Scope` column it inherits is CH-VS-4.)*
13. **§D4.1's "strictly worse than NULL" argument is right**, and the three surfaces are exactly as
    cited: admin `invoice-detail.component.html:151`, partner `:93`, iOS
    `InvoiceDetailContent.swift:182-184`.

---

## Owner question raised by this challenge

The draft's Q-VS-01 and Q-VS-02 stand as written. This one is new, it is not answerable by any agent,
and it decides whether §D3.1's index survives contact with the first franchise. **Verbatim, for the PM
to file — I did not write to `questions/open.md`:**

> **Q-VS-03 — [blocking: no, but it decides whether the reference namespace is right] When you pay a
> cleaner today, does the transfer leave **one** bank account that you alone control — and if a
> franchise operator ever runs cleaners on Cleansia, would they pay their own cleaners from **their
> own** account, or would payouts still leave yours?**
> The whole case for a single global reference namespace (§D3.1) is the sentence *"the payer's
> account is one account"*. If a future franchise pays from its own account, a statement line already
> belongs to exactly one account, tenant-scoped references become sufficient, and the cross-tenant
> volume-inference channel this ADR accepts (§D3.2) is a cost we would be paying for nothing. If
> payouts always leave your account regardless of who the cleaner works for, global is right forever
> and the channel is correctly accepted. **This does not block the build** — global is the cheapest
> correct shape today either way, and the shipped index is already global. It decides whether the
> §D3.2 flip is a contingency or a scheduled migration, and narrowing that index later is owner-only
> and fails on pre-existing duplicates, so it is much cheaper to know now than after the first
> franchise has invoices.

---

## Verdict requested of the lead

**Blocking — the ADR should not be accepted until each is defended or conceded:**

| # | Finding | Why it blocks |
|---|---|---|
| **CH-VS-1** | §D5.6's "PDF path is down at HEAD" is false (`Initial.cs:556-558`) | A false fact carries four sequencing/cost statements, including the ADR's only mitigation for its owner-only migration |
| **CH-VS-2** | §D6.3's per-cleaner isolation is unreachable; `Rollback()` is context-global (`CleansiaDbContext.cs:107-113`) | The ADR promises a blast radius it cannot deliver, on the money path |
| **CH-VS-3** | §D4.4 serves an unreachable population, is dominated by §D4.3, has no UI, and null-derefs | It would brick mark-paid on every invoice that exists today |
| **CH-VS-4** | `Scope` unspecified in the `ON CONFLICT` arbiter | Null ⇒ every invoice gets `2026000001`; per-tenant ⇒ D3.2's collision, both with reviewer check #5 green |
| **CH-VS-5** | §D2.4 states a caller property as an API property; the copied file says the opposite | The central safety property silently inverts under a transaction, and the counter row lock is unpriced |
| **CH-VS-6** | §D1's pigeonhole argument proves too much; §D9-I contradicts it | An ADR may not foreclose re-litigation with an unsound lemma |
| **CH-VS-8** | §D2.3(a) under-specified; `SetPdfBlobUrl` lands after the commit | Reviewer check #6 passes on the wrong implementation |

**Non-blocking amendments:** CH-VS-7 (answer the `PayPeriod`-ordinal alternative in D9; strengthen
D9-F's why-not), CH-VS-9 (D7 census + the fixture literal check + count the twelve fixture call
sites), CH-VS-10 (say "cheapest, and the flip is written down", not "forced"), CH-VS-11 (allocation
year vs period year; "no wrap" is a hard stop), CH-VS-12 (two citation fixes).

**Two that must be ruled on together:** **CH-VS-2 and CH-VS-8.** Both are about what one iteration of
the batch commits and when; ruling on them separately risks a design where the flush/rollback is
specified against a commit boundary that CH-VS-8 then moves.

**One that changes §Applies-to if it stands:** **CH-VS-3(c)** — an admin note dialog is a frontend
ticket the ADR does not currently scope. If the lead prefers option (i) (refuse mark-paid, point at
§D4.3), the frontend cost disappears and the ADR gets *stronger*, not weaker.

I did not write any repair, did not touch `src/`, did not amend the draft, and did not write to
`agents/archive/2026-08/backlog/questions/open.md`.
