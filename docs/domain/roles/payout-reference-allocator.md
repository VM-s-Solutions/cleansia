# Role — `PayoutReferenceAllocator` / `IPayoutReferenceAllocator` (CRC card)

> **✅ BUILT AND SHIPPED** (`d410f002`, 2026-08-09) — and **per operating company since 2026-09-15**
> (T-0757, owner ruling Q-TENANCY-02: *"Separate everything — one holding and child companies per
> country."*). The card below describes the tree after that ruling; the 2026-08-09 shape — a single
> global counter keyed on `(Year)` alone, no tenant term, no scope column — is recorded in **ADR-0046**
> (still `accepted`; its 2026-09-15 note amends §D3.2 rather than retiring the decision). Where this card used to say *"does NOT know the tenant"* it now
> says the opposite, and the reason is one owner sentence, not a drift.
>
> **Retires when:** n/a — this card describes existing code. Every claim below was read in `src/` on
> **2026-09-15**.
> **Introduced by:** **ADR-0046** (`accepted`); the per-company flip is ADR-0061 D9 as amended and
> ADR-0046's superseding note. Living doc: `agents/architecture/decisions/payout-invoice-references.md`.
>
> **Four files are the whole role:**
> `Cleansia.Core.AppServices/Services/PayoutReferenceAllocator.cs` ·
> `Cleansia.Core.AppServices/Services/Interfaces/IPayoutReferenceAllocator.cs` ·
> `Cleansia.Infra.Database/Repositories/PayoutReferenceCounterRepository.cs` ·
> `Cleansia.Core.Domain/EmployeePayroll/PayoutReferenceCounter.cs`.
>
> **Still the direct sibling of `FiscalCounterRepository.AllocateNextAsync`** — same one-statement
> UPSERT, same `RETURNING`, same row-lock serialization, and now the same **tenant term and scope
> column** in the arbiter. What it still does **not** inherit: the gaplessness contract and the
> transaction participation (`FiscalCounterRepository` *joins the caller's open transaction*; this one
> self-commits — invariant 4).

## Responsibility (one sentence)

Hand back **the next payout-invoice reference for the ambient operating company and the current
calendar year** — the ten-digit variable symbol `YYYYNNNNNN` or the invoice number `INV-YYYY-NNNNNN`,
two independent series — each ordinal claimed by one atomic, self-committing SQL statement that
serializes concurrent callers, and refuse, as a named business error, when that company's 999 999
ordinals of that series are exhausted for the year.

> **It formats, and there are two formats now.** `AllocateAsync` returns `BusinessResult<string>`
> (the symbol), `AllocateInvoiceNumberAsync` the same (the number); `Format(year, ordinal)` and
> `FormatInvoiceNumber(year, ordinal)` are public statics on the class so the census can call them.
> The reason formatting belongs here is unchanged: the no-leading-zero property of the symbol is a
> **joint** property of the `YYYY` prefix and the `D6` ordinal, and a property of two values that lives
> in neither of them is the kind nobody owns.

## Collaborators

- **`IPayoutReferenceCounterRepository`** — the **only** constructor dependency. One method,
  `AllocateNextAsync(year, scope, ct) → Task<long?>`; `null` means that company's series is exhausted
  for the year. Its contract doc carries both caller obligations — self-commits, and **never call it
  inside an explicit transaction** — and names the company: *"in the AMBIENT operating company"*.
- **`PayoutReferenceCounter`** — the durable counter row. **`TenantAuditable`** (stamped, FK'd to
  `Tenants`); key **`(TenantId, Year, Scope)`**, `Value` the ordinal; unique
  `IX_PayoutReferenceCounters_Tenant_Year_Scope`, **`NULLS NOT DISTINCT`**. `Scope` is one of two
  constants on the entity — `VariableSymbolScope` (`"VariableSymbol"`) and `InvoiceNumberScope`
  (`"InvoiceNumber"`) — the way `FiscalCounter.IssuerScope` keys one row per issuer. Its EF
  configuration is a `TenantAuditableEntityConfiguration`, which is what maps the FK and the index.
- **`ITenantProvider`** — read by the **repository** (`GetCurrentTenantId()`), never by the allocator.
  The company is whatever is ambient: the admin's claim on the two admin commands, the per-tenant
  override `PayPeriodBackgroundService` sets per employee group, the envelope's tenant under the
  `GenerateInvoice` queue consumer. With **no** ambient company the NOT NULL column refuses the insert
  (`23502`) rather than letting a numbering run silently share one holding-wide sequence — pinned by
  `An_Allocation_With_No_Ambient_Company_Is_Refused_By_The_Database`.
- **`CleansiaDbContext.Database.SqlQueryRaw<long>`** — the execution seam, in the **repository**. One
  statement: `INSERT … ("TenantId", "Year", "Scope", …) ON CONFLICT ("TenantId", "Year", "Scope") DO
  UPDATE SET "Value" = "Value" + 1 … WHERE "Value" < @maxValue RETURNING "Value"`, with
  `MaxOrdinalPerYear = 999999`. The `tenantId` parameter is typed (`NpgsqlDbType.Text`) — the
  `FiscalCounterRepository` lesson: an untyped `DBNull` inferred from one `VALUES` usage becomes a
  `42P08` on the second.
- **`BusinessErrorMessage.InvoiceReferenceCapacityExhausted`** (`payroll.invoice.reference_capacity_exhausted`)
  — the one key it produces, on either series, when the `WHERE` guard is false and `RETURNING` yields
  no row. The `Error`'s field name is `nameof(EmployeeInvoice.VariableSymbol)` or
  `nameof(EmployeeInvoice.InvoiceNumber)` — a field name, per consistency rule **B5**.
- **The two per-company indexes on `EmployeeInvoices`** — `(TenantId, InvoiceNumber)` unique and
  `(TenantId, VariableSymbol)` unique filtered `IS NOT NULL`, both `NULLS NOT DISTINCT`. They are the
  sole arbiters between allocate and insert, and they are what makes *"two companies' first invoices
  of a year carry the same strings"* a fact rather than a collision.
- **Callers, and there are exactly three** — all allocate **before** constructing or stamping the
  invoice, symbol first then number where both are needed:
  1. `GenerateInvoice.Handler` — both series, one pair per currency group, every reference claimed
     before any invoice is staged.
  2. `PayPeriodBackgroundService.GenerateInvoiceForEmployeeAsync` — both series under the employee's
     company override; a refusal logs and **skips that employee** rather than failing the run.
  3. `AssignInvoiceVariableSymbol.Handler` — the symbol only, the admin one-time assign-and-regenerate
     command (ADR-0046 §D4.3).

## Does NOT know

- **Which company it is numbering for.** The allocator takes no tenant and reads none; the repository
  reads the ambient one. That is the design, not a gap: a caller that wanted to number for *another*
  company would set the override, and no caller does. Contrast the pre-2026-09-15 card, where "does
  NOT know the tenant" meant *there is no tenant term anywhere*; now it means *the term is the
  ambient one and this class never names it*.
- **The other company's sequence.** Two companies allocate independent sequences
  (`Two_Companies_Allocate_Independent_Sequences`); one company's exhausted year does not exhaust
  another's (`One_Companys_Exhausted_Year_Does_Not_Exhaust_Anothers`). The inference channel
  ADR-0046 §D3.2 accepted — a tenant admin reading platform-wide volume from the gaps in their own
  symbols — is closed as a consequence.
- **The other series.** The symbol and the number are two rows per company-year, and
  `The_Two_Scopes_Do_Not_Share_A_Sequence` says so.
- **The invoice — as a row.** It returns a string, never a row; it does not read or write
  `EmployeeInvoices` and does not know whether the caller went on to create an invoice or whether the
  caller's transaction succeeded. `EmployeeInvoice` appears in the file only as the error's field name.
  **Gaps are the direct consequence and they are correct** — a variable symbol is a payment reference
  and an invoice number is not a fiscal document number; nothing here requires gaplessness. Pinned by
  `An_Allocation_Whose_Caller_Rolls_Back_Leaves_A_Gap`.
- **The pay period.** The year is `DateTime.UtcNow.Year` — the year of **allocation**, not the
  accounting year of the work. A December period closed on 2 January produces `2027…` and
  `INV-2027-…`. It never loads a `PayPeriod`. *(ADR-0046 R10a records the alternative and its cost;
  only `Q-VS-01`'s answer moves it.)*
- **Gaplessness.** That contract belongs to `FiscalCounter` (CZ EET / DE TSE / AT RKSV). This counter
  is **deliberately gappy**, which is one of the reasons it is a separate table rather than a
  `FiscalCounters` scope.
- **Whether a transaction is open.** Its self-committing behaviour is a **caller** property —
  `SqlQueryRaw` *joins* an ambient transaction if one exists. It auto-commits only because **no payout
  path opens one**; the obligation is stated on the interface and travels with the call (invariant 4).
- **What to do when it refuses.** At the cap it returns `BusinessResult.Failure`; the caller decides.
  Admin: the error goes back to the screen. Background: log-and-skip — unlike
  `payroll.invoice.reference_unavailable` (the *duplicate-index* refusal), a retry inside the same
  year genuinely will not change the verdict.

## Invariants a reviewer checks

1. **The key is exactly `(TenantId, Year, Scope)`.** Open the entity: `TenantAuditable`, `Year` a
   non-nullable `int`, `Scope` a required string ≤ 20 with the two constants, the unique index over all
   three with `.AreNullsDistinct(false)`. **A key with the tenant term missing fails this check** — it
   is the 2026-08-09 shape, and it would make the two companies' `2026000001` a `23505` on the payroll
   path instead of two rows. *Pinned against the live DDL by
   `PayoutReferenceAllocatorTests.The_Counter_Table_Is_Keyed_Per_Company_Year_And_Scope`, which reads
   `information_schema.columns` and `pg_indexes`.*
2. **The cap is in the SQL, not in C#.** The `DO UPDATE` carries `WHERE "PayoutReferenceCounters"."Value"
   < @maxValue`. Without it a company's counter runs permanently past the cap, repairable only by a
   manual `UPDATE` on a poisoned row — and it would format to eleven digits, which the
   `^[1-9][0-9]{9}$` census would then catch only after the fact.
3. **The empty `RETURNING` is guarded.** When the `WHERE` is false the statement affects no row and
   returns nothing. Shipped shape: `allocated.Count == 0 ? null : allocated[0]`. **An unguarded
   `allocated[0]` is the defect** — `FiscalCounterRepository` can afford it because that counter has no
   cap. Pinned twice: `An_Exhausted_Year_Returns_No_Ordinal_Rather_Than_Wrapping` and
   `An_Exhausted_Year_Fails_With_Reference_Capacity_Exhausted_Rather_Than_Throwing`.
4. **No call site sits inside a `BeginTransactionAsync` scope.** Grep every caller. Two properties break
   if one does: the gap semantics, **and** the row-lock duration — the `ON CONFLICT … DO UPDATE` locks
   that company's counter row, so one long transaction serializes every concurrent payroll run *of that
   company* for its life. ⚠️ **Two of the three callers *do* call `CommitAsync` inside the handler** —
   `GenerateInvoice` and `AssignInvoiceVariableSymbol`, each a deliberate, commented flush that owns a
   unique-index failure instead of letting it surface as a 500. **That is not a violation, and do not
   "fix" it:** the allocation happens strictly **before** the flush, so no transaction is open when the
   statement runs.
5. **It carries a sanctioned-exception doc-comment** (the repository's statement comment, plus the
   contract copy on `IPayoutReferenceCounterRepository`) stating that it self-commits, that this is
   intentional and required, and what it does *not* roll back. This is what earns it a place on
   `consistency.md`'s **roster of sanctioned self-committing writes** — *"an exception because it says
   so, not because it exists"*. **If this comment is deleted the write becomes a deviation.**
6. **Zero reads or writes of `EmployeeInvoices`.** Grep the implementation for
   `IEmployeeInvoiceRepository` and `EmployeeInvoices`: zero. If it needs a repository, the
   responsibility is wrong.
7. **The tenant is read in the repository and nowhere else in the role.** Grep `PayoutReferenceAllocator.cs`
   for `ITenantProvider` / `GetCurrentTenantId` / `SetTenantOverride`: none. Grep the repository: one
   read, into the `tenantId` parameter. An allocator that took a tenant argument would be a caller
   deciding which company to number for — the thing no caller should be able to do.
8. **Concurrency is proven on real Postgres**, not with a mocked allocator:
   `N_Concurrent_Allocations_Yield_N_Distinct_Contiguous_Ordinals` runs 25 parallel allocations across
   25 contexts and asserts 25 **distinct**, contiguous values with zero nulls. The direct analogue is
   `Cleansia.IntegrationTests/Features/Receipts/FiscalCounterAllocatorTests.cs`.
9. **Every formatted reference is asserted from a PRODUCED value, never from a fixture literal.**
   `Every_Allocated_Symbol_Is_Ten_Digits_And_Never_Starts_With_Zero` and
   `Every_Allocated_Invoice_Number_Is_INV_The_Year_And_Six_Digits` check the allocator's own output,
   and `PayoutReferenceProductionCensusTests` drives **every production construction path** (the
   MediatR command, the background service, the admin assign) through the real pipeline and asserts the
   shape on the persisted row — including
   `The_Pay_Period_Batch_Numbers_Each_Companys_Invoices_From_Its_Own_Series`, which runs the sweep
   across two companies and reads each one's own series back. **A new construction path owes this
   census a case.**
10. **`Format` and `FormatInvoiceNumber` are the only formatters.** Both `public static`; grep for a
    second `:D6` / `:D4` composition of a symbol or an `INV-` prefix anywhere in `src/` — a second one
    is the defect. The dead `EmployeeInvoice.GenerateInvoiceNumber(prefix)` (the random-hex shape) is
    deleted, and `PaymentReference` still mirrors the number the allocator produced.

## Watch-list

- **A third series** joins by adding a constant on `PayoutReferenceCounter` and a method here — not a
  table, not a second `Scope` mechanism. The column exists *because* the design can now name more than
  one value for it; the 2026-08-09 argument for deleting it (*"the ADR cannot name a second value"*) is
  answered, not overruled.
- **A holding-wide sequence** is the thing the owner ruled out. If anyone asks for one — "one number
  space across all companies, for the accountant" — the answer is a superseding note on ADR-0046 and
  an owner ruling, not a `Scope = "holding"` row, because the `EmployeeInvoices` indexes are per
  company too and a holding-wide series would collide on them.
- **Do not generalize this into a shared `ISequenceAllocator<T>` with `FiscalCounterRepository` on the
  first repeat.** They now agree on the SQL shape *and* the key shape, and still disagree on the two
  things that matter: gaplessness and transaction participation. A generic base would have to
  parameterize exactly the properties each one exists to guarantee.
> **Never cite the `Initial` migration by filename.** Pre-prod it is REGENERATED rather than stacked,
> so its timestamped name changes on every schema change. Cite the entity configuration: it carries the
> same fact and its name is stable.
- **The year rollover is the only remedy for exhaustion**, by design — per company now, so one busy
  company's cap does not touch another's. If 999 999 payout invoices in a year for one company ever
  becomes plausible, the column width is the constraint to revisit — `VariableSymbol` is
  `character varying(10)` on the wire to three generated clients, so that is an epic, not a tweak.
