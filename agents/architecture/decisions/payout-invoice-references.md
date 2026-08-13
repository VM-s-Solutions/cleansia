# Payout Invoice References — living decision doc

**Topic:** the number that makes a payout invoice payable — what the *variabilní symbol* is, who
assigns it, when, in what namespace, what prints, and what happens when the assignment fails.
**ADRs:** [ADR-0046](/decisions/adr-0046)
(**`accepted` 2026-08-09** — the shape; adjudicated by a panel lead with **twelve findings, seven
blocking**, folded in as the closed list R1–R17 — see *What the panel changed* below) · composes with
[ADR-0034](/decisions/adr-0034) (payout details — the bank block the
symbol sits beside) · [ADR-0041](/decisions/adr-0041)
(self-billing — **why** the reference is Cleansia's to allocate at all) ·
[ADR-0038](/decisions/adr-0038)
(*"post-persist means post-commit"* — the ordering rule, applied here to a document instead of an FK) ·
[ADR-0010](/decisions/adr-0010) (the tenant-global entity lane).
**Retires in practice:** **T-0244** — its *finding* is upheld harder; its *remedy* (a better hash) is
the thing ADR-0046 rules out.
**Tickets:** T-0522 (the rebuilt document this prints onto, `in_review`) · T-0508 (invoice field spec) ·
the implementing ticket for ADR-0046 is not yet cut.
**Owner input:** a real Czech invoice whose payment block carries a *variabilní symbol*; and the T-0522
AC4 ruling, **2026-08-03** — *"VS can't equal the invoice number. These are 2 different and there is a
separate property for it."*

---

## The problem this area exists to solve

A payout invoice is a document whose entire purpose is to be paid. The owner makes a bank transfer, the
bank shows a line on a statement, and something has to connect that line back to exactly one invoice.
In Czech practice that something is the *variabilní symbol*.

**Today the platform prints no payment reference of any kind, and "mark this invoice paid" records a
claim nothing can reconcile.** That is the whole problem.

### The state before ADR-0046 (verified 2026-08-09)

| Fact | At |
|---|---|
| `SetVariableSymbol` / `GenerateVariableSymbol` have **zero production callers** | `EmployeeInvoice.cs:212`, `:340`; the only hits are two test files |
| So **every** invoice created by either production path carries `VariableSymbol = NULL` | `GenerateInvoice.cs:87`, `PayPeriodBackgroundService.cs:328` |
| The PDF renders the field **only when non-empty**, so its absence is invisible | `DefaultInvoiceLayoutBuilder.cs:181-182` |
| A second reference field exists on the PDF model, is filled with the **invoice number**, and is read by **no layout** | `InvoicePdfData.cs:8`, `FileExtensions.cs:40`, `EmployeeInvoice.cs:126` |
| The unique index is real and enforcing (bare column, filtered `IS NOT NULL` — **not** the tenancy trap) | `EmployeeInvoiceEntityConfiguration.cs:116-118`, `Initial.cs:2654-2659` |
| A collision would surface **after** the handler returns, as an unhandled `DbUpdateException` | `UnitOfWorkPipelineBehavior.cs:20-30` |

**The dormant generator is not a fix waiting to be switched on.** Within one pay period its period
component is constant, so two cleaners are distinguished by 10 000 buckets: **39 % chance of at least
one collision at 100 cleaners**, 67 % at 150. It also emits a **leading zero** about one time in ten —
and a bank form treats the symbol as a number and drops it, so the printed and the transferred string
stop being the same string on a document whose only job is to make them the same string.

---

## The trade-off space

### Derive it, or claim it?

| Option | Why it lost / won |
|---|---|
| **Hash the identity** (today's generator; or widen it to SHA-256 mod 10¹⁰) | **Lost.** Widening is *enormously* better — a perfect ten-digit hash gives ≈0.50 % at n = 10 000 and ≈39 % at n = 100 000, against 39 % at n = **100** today. It still loses on four counts: the probability **never reaches zero**, the failure is **silent**, it lands on a **bank transfer**, and it gets **monotonically worse every year the platform runs**. ⚠️ It does **not** lose because "no hash is injective here" — that argument is unsound and was struck from the ADR |
| **Hash + catch 23505 + retry with a salt** | **Lost.** The output stops being a function of anything, so it is an allocator with extra steps — and a worse one, with an unbounded retry |
| **VS = the invoice number** | **Lost** by owner ruling (T-0522 AC4), and independently: `InvoiceNumber` is `INV-yyyyMM-XXXXX` (`EmployeeInvoice.cs:111`), non-numeric |
| **Per-cleaner VS, period in the *specific* symbol** — the real CZ idiom the seed data implies | **Lost, narrowly.** The bare-column unique index would reject the same cleaner's second invoice (index change on day one); and with two of one cleaner's invoices unpaid at once, a statement line carrying only the VS is **ambiguous** — the reconciliation question gets two answers, which is the failure the design exists to remove |
| **Derive from a per-employee ordinal + a per-period ordinal** (injective, no collision) | **Lost on COST, not on impossibility** — and the ADR says so in both places, deliberately. Neither ordinal exists, so it is **two** allocators and two migrations to avoid one. It also leaks a cleaner's platform ordinal onto a document they hand to third parties |
| **Claim it from a durable counter** | **Won.** See *The principle*, below |

### The principle that decided it

> **The obstacle is statelessness, not bit width.** Ten decimal digits is a **dense** codomain — 10¹⁰
> points, all reachable. `EmployeeId` and `PayPeriodId` are `Ulid.NewUlid()` values: 130-bit, sparse,
> drawn by the id generator and not chosen by the platform. A function of those two values **alone** is
> fixed *before* the realized set exists and cannot be chosen injective on it. Injectivity into ten
> digits requires at least one input from a **dense** identifier space. The platform holds none, so it
> must **introduce** one — and introducing one is allocation.

Two corollaries worth carrying beyond this document:

- **The operative property is density, not authorship.** An *externally* assigned dense id — a
  registration number, a bank-assigned id — would serve equally. Allocation is how you obtain density
  when nothing dense is in hand. That is narrower and more honest than "the platform must assign it".
- **Do not reach for pigeonhole.** *"260 bits of identity into 33 bits, therefore no function is
  injective"* counts correctly and proves the wrong thing: injectivity is only ever required on the
  **realized** set, which is small. The draft used it to foreclose the question and thereby contradicted
  its own alternatives table two pages later. That was a **blocking** finding despite changing no
  decision — an ADR earns non-re-litigation by carrying an argument that survives being checked.

### Where to allocate from

| Option | Why it lost / won |
|---|---|
| **Reuse `FiscalCounters`** with a `payout-invoice` scope — zero new tables | **Lost, and it was the closest call.** Its key is tenant-leading and the payout namespace must not be; its contract is **gaplessness** for CZ EET / DE TSE / AT RKSV (`FiscalCounter.cs:7-23`) and this counter is deliberately **gappy**; and its repository reads the ambient tenant internally (`FiscalCounterRepository.cs:19`), so bending it edits the **fiscal money path** to serve payroll. **The allocation *statement* is copied; the *table* is not** |
| **A Postgres `SEQUENCE` + `nextval`** | **Lost.** No per-year reset without a job (which kills the `YYYY` prefix and the no-leading-zero property it buys); a sequence is **not an inspectable, auditable, correctable row** — `FiscalCounter` is deliberately a row for exactly that reason; and the cap repair lives in a `WHERE` clause on the update, which a bare `nextval` has nowhere to put — it cannot refuse |
| **An ordinal on the `PayPeriod` row that already exists** — zero new tables | **Lost**, and this one had to be *added* to the record at adjudication because the draft never considered it. Decisive: **`PayPeriod.Update` mutates `StartDate`** (`PayPeriod.cs:76`, `:94`), so the reference's own prefix would be a **mutable column** — an admin correcting a period's dates changes what the next reference means. Plus a two-tenant collision under the global index, and a reachable 9 999-per-period cap |
| **A new durable counter row, `(Year)`-keyed** | **Won** |
| **Assign in a `SaveChanges` interceptor / UoW hook**, or **lazily at PDF render** | **Both lost.** The interceptor re-arms the exact defect — the guarantee becomes "the framework remembers", which is what a `SetVariableSymbol` nobody called already was. Lazy assignment puts the number on a document before it is on a row, and inverts the ordering invariant that makes the whole thing checkable |

---

## Current shape (as **accepted** by ADR-0046, closed list R1–R17 folded in)

```
VariableSymbol = YYYY · NNNNNN          exactly ten digits, first digit never '0'
                                        first produced value: 2026000001
                                        capacity 999 999 per calendar year

PayoutReferenceCounters                 // tenant-GLOBAL: NOT ITenantEntity
  Year   int  NOT NULL                  ← the WHOLE key. UNIQUE (Year). No Scope. No TenantId.
  Value  long
```

**The allocating statement** — one atomic UPSERT, the shape shipped and reviewed in
`FiscalCounterRepository.AllocateNextAsync` (`:33-42`):

```sql
INSERT INTO "PayoutReferenceCounters" ("Id", "Year", "Value", "CreatedBy", "CreatedOn")
VALUES (@id, @year, 1, @actor, @now)
ON CONFLICT ("Year")
DO UPDATE SET "Value"     = "PayoutReferenceCounters"."Value" + 1, …
WHERE "PayoutReferenceCounters"."Value" < 999999
RETURNING "Value";
```

### Five properties, each a requirement and not a preference

1. **Ten characters, numeric.** Fits `character varying(10)` (`Initial.cs:1522`) exactly and uses the
   budget once, forever — a dropped digit is detectable by length alone.
2. **The first digit is never `0`.** `YYYY ≥ 2026`. Printed string ≡ stored string ≡ what a bank form
   shows. This is the leading-zero hazard closed by construction rather than by validation.
3. **The prefix is the year of ALLOCATION, not the accounting year of the work.** A December period
   closed on 2 January produces `2027…`. The alternative (key on `PayPeriod.EndDate`'s year) is
   recorded with its cost — `GenerateInvoice.Handler` holds only `PayPeriodId` and would need a
   `PayPeriod` load — and moves only if the accountant's answer (Q-VS-01) moves it.
4. **The cap is in the SQL, not in a sentence.** `WHERE "Value" < 999999` makes *"no wrap, ever"* true
   rather than aspirational: the row **stops** at the cap instead of running away past it, and the
   exhausted state is repaired by the year rolling over rather than by a manual `UPDATE` on a poisoned
   counter.
5. **It is not the invoice number and cannot become it.** Owner ruling, and structurally impossible —
   `INV-yyyyMM-XXXXX` is non-numeric.

### Why `(Year)` and nothing else

Two reasons, and the first is the decisive one:

- **The design cannot name a second value for a scope.** There is exactly one namespace (below). A
  scope column would be a discriminator over a one-member set, sitting *inside the `ON CONFLICT`
  arbiter* of the decision that says the set has one member.
- **A non-nullable `int` key cannot reproduce the nulls-distinct collapse.** The trap is documented in
  the very file this statement copies: `FiscalCounterRepository.cs:30-32` records that its index needs
  `.AreNullsDistinct(false)` precisely because a null in the arbiter otherwise inserts a duplicate row
  **per call**. With `(Year)` as the whole key, no nullable column is in the arbiter and the retrofit is
  simply not in play.

> ⚠️ **The precedent actively invites the wrong answer.** `FiscalCounter.cs:13-18` trains the reader
> that the scope string *"is the extension point"* and binds *"NOT merely the tenant"*. A developer
> copying that file reaches for `Scope = tenantId` — which silently re-introduces the tenant term the
> namespace decision forbids. **Removing the column removes the reading.** This was a blocking finding.

### The namespace is global — and that is CONTINGENT, not forced

`IX_EmployeeInvoices_VariableSymbol` (bare column, `UNIQUE`, filtered `IS NOT NULL`) **is untouched.
Not one line of index DDL changes on `EmployeeInvoices`.**

The reasoning must be stated at its true strength, because the draft overstated it and that was a
finding. **Global is the cheapest correct shape *today*:**

- the shipped index is **already** global, so this costs zero index DDL where the alternative costs an
  owner-only migration;
- it has **no NULLS-DISTINCT hole**, because it carries no `TenantId` — the retrofit CLAUDE.md warns
  about is not in play;
- production is **single-tenant**, so a tenant term would discriminate nothing.

> **It is contingent on *"the payer's account is one account"* — which is exactly what `Q-VS-03` asks
> the owner.** You cannot ask whether a premise holds and call the conclusion *forced* in the same
> document. **Re-examine this the moment the premise stops holding.**

**The flip, written down so it is bounded:** add a tenant term to the counter key, and replace the
index with `(TenantId, VariableSymbol) UNIQUE … NULLS NOT DISTINCT WHERE VariableSymbol IS NOT NULL` —
an owner-only `ef-migration` that **fails on pre-existing duplicates**, so it must be done while the set
is small or empty. The accepted cost of not flipping: under activated multi-tenancy a tenant admin can
infer platform-wide payout volume from the gaps between their own symbols. Low severity, and noisy —
failed commits gap the sequence too.

### The stamp is a required constructor parameter — that is the mechanism, not a style choice

`EmployeeInvoice.Create` and `CreateFromOrderPays` take `string variableSymbol` as a **required,
non-defaulted** parameter. `SetVariableSymbol` and `GenerateVariableSymbol` are **deleted**. A third
production path does not compile.

This is the same seam T-0522 established on this exact document with `payoutDetails`, and the catalog
carries it as a corollary (`patterns-backend.md:464-467`): *"a new required parameter beats a defaulted
one on exactly this seam"*. A validator, a convention, and a setter the author must remember were all
already available — and all three produced the current state, where the field is null on every row.

### Ordering: the row is committed before any document carrying it exists

> **The row that owns a reference is committed before any document carrying it is generated, uploaded
> or delivered.** — ADR-0038's rule applied to a document instead of an FK.

**Sequence: allocate → construct → add → commit → render → upload → deliver.**

The admin and queue paths already satisfy it (neither produces a PDF). **The pay-period batch violates
it today** and this is the wider latent defect the ADR closes as a by-product: emails go out with the
PDF attached *inside* the loop and the blob is uploaded, both **before** the group commit — so one bad
row today means every cleaner in the group has an invoice PDF in their inbox and **no invoice row
exists for any of them**.

> ### ⚠️ The primitive that makes "commit inside the loop" mean something
>
> `IUnitOfWork.CommitAsync` → `BaseRepository.CommitAsync` (`:171-174`) → `CleansiaDbContext.CommitAsync`
> (`:67-100`) ends in a **context-wide** `SaveChangesAsync` (`:99`). **There is no per-entity commit in
> this codebase**, and `CleansiaDbContext.Rollback()` (`:107-113`) sets **every** tracked entry to
> `Unchanged`. State this before stating any per-loop commit rule, or the rule means the wrong thing.

**Two named commits per employee**, and the batch is specified in terms of what each one carries:

| | Where | Carries |
|---|---|---|
| **C1** | after `Add(invoice)` + the `AssignToInvoice` loop (`:334-339`), **before** `GenerateInvoicePdfAsync` (`:352`) | the invoice and its order-pays — **and `period.Close()`** (`:148`, one loop level up), so the period close becomes durable at the *first invoicing employee* instead of at the group commit `:187` |
| **C2** | after `SetPdfBlobUrl`/`ClearPdfGenerationError` (`:360-361`) **and** after `SetPdfGenerationError` in the catch (`:371`) | the PDF outcome — without it those three mutations ride the *next* employee's commit and are lost with whatever fails next |

**On a failed C1** call `Rollback()` and know that it is **context-global**: its scope at that instant is
this employee's rows **plus `period.Close()` if and only if no earlier employee in this period has
already committed**. `RefundService.cs:101-103` is the shipped catch-and-`Rollback()` precedent.

### Failure behaviour, stated as what may honestly be claimed

> An allocator failure, or a duplicate on C1, skips **one cleaner**; every other cleaner in the group is
> still invoiced. **Except** on the *first invoicing employee of a period*: there the `Rollback()` also
> reverts `period.Close()`, the period stays `Open`, it is re-selected on the next tick, and its
> period-closed emails are sent **a second time**. **No duplicate invoice results** — the existing
> already-has-one guard (`:312-323`) skips it — and no money moves.

The unqualified version of that sentence ("every other cleaner is invoiced", full stop) is **false**
under a context-global rollback and was struck.

**A stronger fix exists and was declined, with its reason recorded** so nobody "fixes" it blind:
committing `period.Close()` before the emails removes the duplicate-email residue entirely, at the cost
that a crash mid-emails leaves the period `Closed` with an **untreated tail** — recoverable only through
the admin path and **with no re-sent email**. Trading a duplicate email for a silent untreated tail is
the worse trade, and it is a pay-period-job decision that does not belong to a variable-symbol ADR.

### Gaps are not a defect, and the mechanism must be described correctly

**`SqlQueryRaw` does not "auto-commit" as an API property.** It runs on the context's connection and
**joins an ambient transaction if one is open** — `FiscalCounterRepository.cs:28-30` documents exactly
that for this statement. It auto-commits *here* **because no payout path opens one**
(`UnitOfWorkPipelineBehavior.cs:13-33` opens none; `PayPeriodBackgroundService
.CloseExpiredPeriodsAndOpenNewAsync` (`:107-197`) opens none). That is a **caller property**, so it is
carried as an invariant rather than an assumption:

> **The payout reference allocator MUST NOT be called inside an explicit transaction.** Both properties
> depend on it: the gap semantics, **and** the duration of the row lock the `ON CONFLICT … DO UPDATE`
> takes on the *single* counter row. Under a long transaction that one row serializes every concurrent
> payroll run for its life — a global contention channel a design that never mentions locking would
> introduce silently.

**Therefore gaps happen, by design.** A variable symbol is a payment reference, not a fiscal document
number; nothing here requires gaplessness (that belongs to `FiscalCounter`). **A design that never gaps
and sometimes duplicates is strictly worse than one that sometimes gaps and never duplicates** — a gap
costs nothing, a duplicate costs a mis-reconciled transfer.

**Catalog consequence:** this is the codebase's **second** self-committing write inside a handler.
`consistency.md:346-353` makes the deviating form *"a self-committing write inside a handler **with no
sanctioned-exception doc-comment**"* — so the allocator **must** carry that doc-comment, in the
`PromoCodeRepository.cs:28-38` shape, and `consistency.md`'s named-exception list gains it as the second
entry when the code lands.

### What prints

- **The variable symbol prints UNCONDITIONALLY**, beside `BankAccount`/`Iban`/`Swift`. A missing symbol
  renders `—`, exactly as those already do. **This is the change that would have caught the bug:** the
  conditional is what made *"no reference"* indistinguishable from *"this document has no reference
  field"*. On a document whose purpose is to be paid, the absence of the payment reference must be loud.
- **The constant symbol stays conditional.** The two are **not** symmetric and must not be "made
  consistent": a *konstantní symbol* is legitimately absent outside CZ (SK is deliberately null, per
  T-0522); a *variabilní symbol* is **never** legitimately absent.
- **`InvoicePdfData.PaymentReference` is deleted**, with the mapper line that fills it. Its `??`
  fallback to the variable symbol is **dead** — `Create` always sets the field (`EmployeeInvoice.cs:126`)
  and `SetPaymentReference` (`:224-228`) has **zero callers**. **A payment document carries exactly one
  payment reference.**
- **Between the print change landing and the first allocated symbol, every payout invoice prints `—`.**
  Intended, and bounded: the print edits **do not ship a release ahead of** the required-parameter
  change. The window is a deploy, not a sprint.

### Backfill: none automatic, ever

| Case | Rule |
|---|---|
| Any existing row, by migration or sweep | **Never.** A symbol on a row whose stored PDF does not print it renders as authoritative on three surfaces (admin web, partner web, the iOS References card) and appears on **no document** — strictly worse than NULL, because NULL is honestly empty |
| A `Paid` invoice | **Never.** A *first* assignment after the transfer has left the bank is the same hazard as reassignment wearing a different hat |
| Unpaid, uncancelled, symbol-less | **Once**, through an explicit admin command: allocate → stamp → **commit** → regenerate the PDF over the same blob name. **The order is load-bearing** — if the regenerate fails the row keeps its number and the step is re-runnable and idempotent; the reverse order puts a number on a document and not on a row |
| Marking paid with no symbol | **Refused** — `payroll.invoice.reference_missing`, whose message names the remedy (assign a reference first) |

**The refusal replaced a compensating record that could not work**, and the four legs of *why* are worth
keeping, because they are a general lesson about writing controls against a tree you have not read:

1. **It could not reach its population.** `MarkAsPaid` throws unless `Approved`
   (`EmployeeInvoice.cs:254-257`) and `MarkInvoicePaid` refuses an already-`Paid` invoice **three
   times** (`:46-47`, `:24-30`, `:93-98`). There is no path by which an already-paid invoice receives a
   note.
2. **Its eligible set was a strict subset of the assign-command's** — every invoice it could act on is
   one that can simply be given a real reference. A weaker control over a subset of a stronger
   control's domain is noise, not a second layer.
3. **Mandatory, it would have failed 100 % of attempts against the shipped UI.**
   `invoice-detail.component.ts:106-108` calls `markAsPaid()` **with no argument** and
   `invoice-detail.facade.ts:79-88` assigns that absent parameter — the field ships `undefined` every
   time. The note dialog it presumed was never scoped.
4. **Its placement was unsafe.** `MarkInvoicePaid`'s three `invoice!` reads (`:65`, `:71`, `:77`) are
   safe **only** because they sit inside the `InvoiceId` `Cascade.Stop` chain. FluentValidation's
   class-level default is `Continue`, so a new **root** rule runs regardless of the id being valid and
   would deref `null` on a bad id. **The new refusal joins the existing chain after `ApprovedAsync` —
   never as a new root `RuleFor`.**

**`BankTransferNote` stays optional, `varchar(500)`, display-only.** It is not a control and this design
does not claim it as one.

**The residual, named:** if money has *already* moved against a null-symbol invoice, the refusal is an
obstacle and **the platform cannot detect that case**. Path: assign via the admin command, and put the
bank's own transaction id in the optional note. This routes to `Q-VS-02`.

### The duplicate is a business result, never a post-commit exception

The insert is **flushed** where the violation can be caught and a Postgres `23505` is collapsed into a
result — the idiom this codebase already ships in four places with the same reflective `SqlState` walk
(`RefundService.cs:193-201`, `LoyaltyService.cs:368-407`, `DbIdempotencyGuard.cs:42-45`,
`StripeSubscriptionWebhookHandler.cs:236-244`, whose comment at `:191-195` states the general rule).

**Three new error keys**, each needing `api.*` in all five admin locales, guarded by
`error-contract-parity.spec.ts`:

| Key | Raised when | Queue behaviour |
|---|---|---|
| `payroll.invoice.reference_unavailable` | a duplicate is somehow attempted | **THROWS** — the one named exception to `GenerateInvoiceHandler`'s ack-everything rule, because a retry allocates a **different** number, so *"retrying won't change the verdict"* is false here |
| `payroll.invoice.reference_missing` | mark-paid against a null-symbol invoice | n/a (admin path) |
| `payroll.invoice.reference_capacity_exhausted` | the year's counter is at 999 999 — the `WHERE` makes `RETURNING` empty | **acks** — a retry inside the same year allocates nothing, so the default reasoning *is* true here |

> ⚠️ **The empty `RETURNING` must not meet an unguarded `allocated[0]`.**
> `FiscalCounterRepository.cs:63` reads exactly that, and copying the shape unguarded is the defect —
> it throws `ArgumentOutOfRangeException` from inside a repository at the cap.
>
> ⚠️ **Naming check for the implementing ticket:** every existing invoice key in
> `BusinessErrorMessage.cs` is namespaced **`payroll.invoice.*`** (`:211-227`), while ADR-0046 names
> these three in the `invoice.*` form. The ADR is transcribed as ruled; whichever prefix ships, the
> constant, the wire key and the five locale keys must agree, and the parity spec proves it.

### The reconciliation loop closes with code that already exists

`EmployeeInvoiceSpecification` already exposes an **exact-match filter on `VariableSymbol`** — the
filter property at `:14`, the predicate at `:60-62`, the wiring at `:112`. The owner reads a line off a
bank statement, types the number, finds the invoice. This is also what makes the no-leading-zero
property **concrete rather than theoretical**: an exact-match filter has no near-miss, so a symbol typed
without the zero a bank form dropped matches **nothing**, silently.

---

## What this costs, and what it does not

| | Cost |
|---|---|
| **Schema** | **One new table, its OWN owner-only `ef-migration`.** There is **no** pending T-0522 pass to ride — that claim was the draft's biggest error. Pre-prod it folds into `Initial` rather than stacking |
| **`EmployeeInvoices`** | **Zero.** No index change, no column change, no backfill. The column stays nullable (`NOT NULL` is deferred with a written precondition: zero null-symbol rows) |
| **Production call sites** | 2 |
| **Fixture call sites** | **12**, each gaining the parameter — and each of them will then hand-author a symbol production never produces, which is the very anti-pattern the ADR invokes. Discharged two ways: **one canonical fixture constant** so twelve files do not invent twelve literals, and a **production census test** through the real handler and the real background service |
| **Admin UI** | one action + confirm for the assign command. **No note dialog** |
| **Locales** | 3 keys × 5 locales, admin only |
| **Catalog** | 1 `consistency.md` edit (the second named self-committing-write exception) |
| **Hosts** | **Zero coupling.** Nothing here is reachable from Customer or Mobile.Customer |
| **Country variation** | **Zero new branches.** The symbol's format is platform-wide; the only per-country variation on this document remains `CountryInvoiceConfig` |

---

## What the panel changed (2026-08-09) — the durable lessons, not just the diffs

Twelve findings, **seven blocking**, all folded in as the closed list R1–R17. **The decision itself
survived intact** — claim the number, don't compute it; the counter-row allocator; print
unconditionally; refuse to backfill a printed document. What failed was the supporting layer. Five of
the lessons generalize well beyond this document:

1. **A claim about the tree cites the tree — never another artifact.** The draft asserted *"the invoice
   PDF path is down at HEAD"* in the present tense, from a **ticket status log** four days stale
   (`T-0522-….md:203-206`). It was true when written; nothing updates a status log when the tree moves.
   The path renders today — verified column by column against `Initial.cs:548-559`. Four sequencing
   statements **and the ADR's only mitigation for its owner-only migration** hung on that one borrowed
   sentence. **This was the third instance in one sprint** (after a living decision page and a sprint
   status section), so it is now a catalog law: `conventions.md:217-243`, T3-HUMAN, enforced by the
   panel lead's own gate. *In each of the three, the artifact's conclusion survived and its plan did
   not — which is the expensive half.*
2. **State the primitive before the invariant.** *"Commit inside the loop"* is a meaningless
   instruction in a codebase where `CommitAsync` is context-wide and `Rollback()` is context-global.
   Every disagreement between the challenger and the author on the batch's behaviour followed from that
   one unstated sentence. **Two commits, named, with what each carries** — not "commit per employee".
3. **A control must be able to reach its population — check the UI, not just the handler.** The
   compensating-record rule targeted invoices that *cannot* reach it (three separate refusals), over a
   set already covered by a stronger control, through a UI that ships the field as `undefined` on every
   call, hung off a root rule that would have NRE'd on a bad id. Four independent legs, all of them
   findable by reading four files.
4. **Do not foreclose with an argument you have not checked.** The pigeonhole lemma is *sound counting*
   and *invalid inference*, and it put §D1 in contradiction with §D9-I in the same file. Raised as
   **blocking despite changing no decision** — because the next author cites the foreclosing sentence
   to reject a design that is fine, reads the contradiction, and stops trusting the document.
5. **An unspecified column in a conflict arbiter is a hazard, especially when the precedent names it an
   extension point.** The draft never said what `Scope` held or that it was `NOT NULL`. Deleting it
   removed the whole hazard class by construction — and the reviewer check was rewritten so that **a key
   with any second column fails**.

**Also worth remembering:** the challenger over-reached once and the verdict recorded it. The claim that
`Rollback()` *"discards every other cleaner's tracked invoice"* describes the **current** group-commit
shape, not the proposed per-employee one. A finding can be right about the mechanism and wrong about
the magnitude; the record says which.

---

## Open threads this doc tracks

- **ADR-0046 is `accepted` and immutable.** Deviations require a superseding ADR.
- **`Q-VS-02` is raised in ADR-0046 §D8 and is NOT filed** in `questions/open.md`. Non-blocking — every
  default it names is already taken — but it decides whether the one-time assign command has a real set
  to act on, and it now carries the *"has money already moved against a null-symbol invoice"* leg too.
- **`Q-VS-03` gates the namespace decision's premise** (`questions/open.md:2097`). Global is contingent
  on *"the payer's account is one account"*. **Re-examine §namespace the moment that changes.**
- **`Q-VS-01` blocks calling the ≤10-digit constraint *verified***, not the build
  (`questions/open.md:2090`). Its answer may also move the prefix from allocation year to accounting
  year — the cost of that move is already priced.
- **The migration is its own owner window** and must be asked for as one.
- **The `consistency.md` edit lands with the code**, not before — naming the allocator as the second
  sanctioned exception while it does not exist in the tree would be the very violation lesson 1 forbids.
- **`EmployeeInvoice.SpecificSymbol` stays dead.** No writer, no PDF field, no layout. Named here only
  so a future reader does not "complete" it by symmetry with the variable symbol.
- **`EmployeeInvoice.PaymentReference` (the entity column and both DTO fields) survives the deletion of
  the PDF-model field**, because removing it is an owner-only `nswag-regen`. Until then the iOS
  References card prints the invoice number twice under two labels — a cosmetic defect introduced by
  telling the truth about the field, and the right order to fix them in.
- **The two standalone seed scripts** (`insert_employee_invoices.sql`, `insert_employee_payroll.sql`)
  hand-author eight leading-zero symbols and are referenced by nothing. Fixed or deleted in the same
  change; they are a loaded gun in a repo where the owner runs SQL by hand.
- **`NOT NULL` on the column** is deferred, not dropped — a DB Master call with a written precondition
  (zero null-symbol rows) rather than an owner question.

---

## Related

- Role: [`knowledge/roles/payout-reference-allocator.md`](../../knowledge/roles/payout-reference-allocator.md)
- Sibling living docs: [`payout-details.md`](payout-details.md) (where the money goes) ·
  [`promo-redemption-ordering.md`](promo-redemption-ordering.md) (the post-commit ordering law this
  applies to a document) · [`self-billing-agreement.md`](self-billing-agreement.md) (why Cleansia issues
  this document at all)
- Catalog: [`knowledge/patterns-backend.md`](../../knowledge/patterns-backend.md) §*"Post-persist means
  POST-COMMIT"* (`:633`) · §*"A fixture that supplies an input production never produces"* (`:443-462`)
  · [`knowledge/consistency.md`](../../knowledge/consistency.md) §post-commit deviating forms (`:346-353`)
  · [`knowledge/conventions.md`](../../knowledge/conventions.md) §*"A claim about the tree cites the
  tree"* (`:217-243`)
- Canonical system description: [`docs/architecture/database.md`](../../../docs/architecture/database.md) ·
  [`docs/architecture/backend.md`](../../../docs/architecture/backend.md) ·
  [`docs/architecture/fiscal-compliance.md`](../../../docs/architecture/fiscal-compliance.md)
