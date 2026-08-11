# Role — `PayoutReferenceAllocator` / `IPayoutReferenceAllocator` (CRC card)

> **✅ BUILT AND SHIPPED.** Retires the *"NOT YET BUILT — no ticket is cut yet"* banner this card
> carried from **2026-08-09** until this edit: the whole allocator landed **2 h 11 m after that banner
> was written**, in `d410f002`. The banner was true when written and false by lunchtime — see
> `conventions.md` §*"A claim about the tree carries its own retirement condition"* for why a
> not-yet-built banner now has to name the path that kills it.
>
> **Retires when:** n/a — this card describes existing code. Every claim below is cited at `file:line`
> in `src/`, read **2026-08-09**.
> **Introduced by:** **ADR-0046** (`accepted` — `0046-payout-invoice-variable-symbol-is-a-claimed-number-not-a-derived-one.md`).
> Living doc: `agents/architecture/decisions/payout-invoice-references.md`.
>
> **Four files are the whole role:**
> `Cleansia.Core.AppServices/Services/PayoutReferenceAllocator.cs` (33 lines) ·
> `Cleansia.Core.AppServices/Services/Interfaces/IPayoutReferenceAllocator.cs` ·
> `Cleansia.Infra.Database/Repositories/PayoutReferenceCounterRepository.cs` ·
> `Cleansia.Core.Domain/EmployeePayroll/PayoutReferenceCounter.cs`.
>
> **Still the direct sibling of `FiscalCounterRepository.AllocateNextAsync`** — same one-statement
> UPSERT, same `RETURNING`, same row-lock serialization. **Read `FiscalCounterRepository.cs:25-32`
> twice**: it says the statement *"joins the caller's open transaction … bound to the same
> commit/rollback as the receipt row"*, and that is precisely the property this allocator does **not**
> inherit. The statement shape was copied; the tenant term, the gaplessness contract and the
> transaction participation were not.

## Responsibility (one sentence)

Hand back **the payout-invoice reference string for the current calendar year** — a ten-digit
`YYYYNNNNNN` whose ordinal is claimed by one atomic, self-committing SQL statement that serializes
concurrent callers — and refuse, as a named business error, when that year's 999 999 ordinals are
exhausted.

> **It formats, and that is a change from the card's original draft.** The draft split the role: the
> allocator returned a `long` and the caller composed the string. Shipped, `AllocateAsync` returns
> `BusinessResult<string>` (`IPayoutReferenceAllocator.cs:23`) and `Format` is a public static on this
> class (`PayoutReferenceAllocator.cs:32`). The draft card said *"if the formatting ever moves in here,
> it moves with the year, and the 'does NOT know' list must be re-read"* — it moved, the year moved
> with it (`:14`, `:25`), and the list below has been re-read. The reason it belongs here: the
> no-leading-zero property is a **joint** property of the `YYYY` prefix and the `D6` ordinal
> (`:28-31` names the bank-form failure it defends), and a property of two values that lives in
> neither of them is the kind nobody owns.

## Collaborators

- **`IPayoutReferenceCounterRepository`** — the **only** constructor dependency
  (`PayoutReferenceAllocator.cs:9`). One method, `AllocateNextAsync(year, ct) → Task<long?>`; `null`
  means the year is exhausted. Its contract doc (`IPayoutReferenceCounterRepository.cs:7-18`) carries
  both caller obligations — self-commits, and **never call it inside an explicit transaction**.
- **`PayoutReferenceCounter`** — the durable counter row (`PayoutReferenceCounter.cs:25-41`). `Year`
  (`int`, non-nullable) is the **whole** key; `Value` (`long`) is the ordinal; `UNIQUE (Year)`
  (`PayoutReferenceCounterEntityConfiguration.cs:46-47`). It extends `BaseEntity` and is **not**
  `ITenantEntity`; its EF config is deliberately a plain `IEntityTypeConfiguration<T>` rather than
  `AuditableEntityConfiguration<T,TKey>`, in the reasoned-S8-exception lane
  `ProcessedMessageEntityConfiguration` takes (`…EntityConfiguration.cs:7-13`).
- **`CleansiaDbContext.Database.SqlQueryRaw<long>`** — the execution seam, in the **repository**, not
  here (`PayoutReferenceCounterRepository.cs:69-71`). One statement (`:48-58`):
  `INSERT … ON CONFLICT ("Year") DO UPDATE SET "Value" = "Value" + 1 … WHERE "Value" < @maxValue
  RETURNING "Value"`, with `MaxOrdinalPerYear = 999999` at `:16`.
- **`BusinessErrorMessage`** — one key it can produce: `InvoiceReferenceCapacityExhausted`
  (`payroll.invoice.reference_capacity_exhausted`, `BusinessErrorMessage.cs:230`), raised when the
  `WHERE` guard is false and `RETURNING` yields no row. The `Error`'s field name is
  `nameof(EmployeeInvoice.VariableSymbol)` (`PayoutReferenceAllocator.cs:21`) — a field name, per
  consistency rule **B5**, not `nameof(Command)`.
- **Callers, and there are exactly three** — all allocate **before** constructing or stamping the
  invoice:
  1. `GenerateInvoice.Handler` — `GenerateInvoice.cs:88`, before `EmployeeInvoice.CreateFromOrderPays`
     at `:94`.
  2. `PayPeriodBackgroundService.GenerateInvoiceForEmployeeAsync` — `PayPeriodBackgroundService.cs:332`;
     a failure logs and **skips that employee** (`:335-341`) rather than failing the run.
  3. `AssignInvoiceVariableSymbol.Handler` — `AssignInvoiceVariableSymbol.cs:103`, the admin
     one-time assign-and-regenerate command (ADR-0046 §D4.3), before `AssignVariableSymbol` at `:109`.

## Does NOT know

- **The tenant.** No tenant term, no `ITenantProvider`, not `ITenantEntity`. Verified: the repository's
  constructor takes `CleansiaDbContext` + `IUserSessionProvider` only
  (`PayoutReferenceCounterRepository.cs:9-13`) — contrast `FiscalCounterRepository.cs:9-13`, which
  takes `ITenantProvider` and reads it at `:19`. The reference namespace is **global** because the
  shipped unique index on `EmployeeInvoices.VariableSymbol` is global — bare column, `unique: true`,
  filtered `"VariableSymbol" IS NOT NULL`, no `TenantId`
  (`Migrations/20260809183249_Initial.cs:2672-2677`) — so it has no NULLS-DISTINCT hole, and a
  tenant-keyed counter under a globally-unique index means two tenants both allocate ordinal 1 and the
  second insert becomes **a 500 on the payroll path**.
  ⚠️ **It must not acquire one without the ADR-0046 §D3.2 flip**, which changes the counter key **and**
  the `EmployeeInvoices` index in the *same* owner-only migration. The global shape is **contingent on
  `Q-VS-03`** (*"does every payout leave one bank account you control"*), not forced — if that premise
  falls, the flip is the answer, not a quiet `TenantId` parameter here.
  ⚠️ **`Scope = tenantId` is the trap the precedent invites.** `FiscalCounter.cs:13-18` teaches that the
  scope string *"is the extension point"* and binds *"NOT merely the tenant"*. **This counter has no
  scope column at all** — asserted against the database, not the builder call, by
  `PayoutReferenceAllocatorTests.The_Counter_Table_Is_Keyed_On_Year_Alone_And_Carries_No_Tenant_Column`
  (`:168-204`, which reads `information_schema.columns` and `pg_indexes`).
- **The invoice — as a row.** It returns a string, never a row. It does not read or write
  `EmployeeInvoices`, holds no `IEmployeeInvoiceRepository`, and does not know whether the caller went
  on to create an invoice or whether the caller's transaction succeeded.
  ⚠️ **Correction to the draft card, which said "grep the implementation: no `EmployeeInvoice`".**
  There **is** one occurrence — `nameof(EmployeeInvoice.VariableSymbol)` at `:21` — and it is correct:
  a compile-time symbol supplying the error's field name, not a data access. The grep to run is
  `IEmployeeInvoiceRepository` / `EmployeeInvoices`, both of which must return zero.
  **Gaps are the direct consequence and they are correct** — a variable symbol is a payment reference,
  not a fiscal document number, and nothing in this platform requires it to be gapless. *A design that
  never gaps and sometimes duplicates is strictly worse than one that sometimes gaps and never
  duplicates.* Pinned by `An_Allocation_Whose_Caller_Rolls_Back_Leaves_A_Gap` (`:78-99`).
- **The pay period.** The year is `DateTime.UtcNow.Year` (`PayoutReferenceAllocator.cs:14`) — the year
  of **allocation**, the moment the number is claimed — not the accounting year of the work, and not
  `PayPeriod.EndDate`'s year. A December period closed on 2 January therefore produces `2027…`
  (`IPayoutReferenceAllocator.cs:9-11` states this on the contract). It never loads a `PayPeriod`, and
  it must not start: `GenerateInvoice.Handler` holds only `PayPeriodId` (`GenerateInvoice.cs:69-80`),
  so a period-derived year would buy this role a repository it does not otherwise need. *(ADR-0046 R10a
  records the alternative and its cost; only `Q-VS-01`'s answer moves it, and it moves in the ADR, not
  here.)*
- **Gaplessness.** That contract belongs to `FiscalCounter` (`FiscalCounter.cs:7-23` — CZ EET / DE TSE
  / AT RKSV legally require it). This counter is **deliberately gappy**
  (`PayoutReferenceCounter.cs:20-23`), which is one of the three reasons it is a separate table rather
  than a `FiscalCounters` scope.
- **Whether a transaction is open.** Its self-committing behaviour is a **caller** property, not an API
  property — `SqlQueryRaw` *joins* an ambient transaction if one exists, which is exactly what
  `FiscalCounterRepository.cs:28-32` documents for the same statement shape. It auto-commits only
  because **no payout path opens one**. The allocator cannot detect or defend the condition it depends
  on, so the obligation is stated on the interface and travels with the call (invariant 4).
- **What to do when it refuses.** At the cap it returns `BusinessResult.Failure` (`:20-23`); the caller
  decides. Admin: a refusal on a screen where clicking again is useless
  (`AssignInvoiceVariableSymbol.cs:104-107` returns the error to the caller). Background: log-and-skip
  (`PayPeriodBackgroundService.cs:335-341`) — unlike `payroll.invoice.reference_unavailable`
  (`BusinessErrorMessage.cs:228`, the *duplicate-index* refusal), a retry inside the same year
  genuinely will not change the verdict.

## Invariants a reviewer checks

1. **The key is exactly `(Year)`.** Open the entity: no `Scope`, no `TenantId`, does not implement
   `ITenantEntity`; `Year` is a non-nullable `int`; the unique index is on `(Year)` alone. **A key with
   any second column fails this check.** A non-nullable `int` arbiter is also why no
   `.AreNullsDistinct(false)` retrofit is in play — contrast `FiscalCounterEntityConfiguration.cs:26-29`,
   which needs it. *Pinned against the live DDL by `PayoutReferenceAllocatorTests.cs:168-204`.*
2. **The cap is in the SQL, not in C#.** The `DO UPDATE` carries `WHERE "PayoutReferenceCounters"."Value"
   < @maxValue` (`PayoutReferenceCounterRepository.cs:56`). Without it the counter runs permanently past
   the cap, platform-wide, repairable only by a manual `UPDATE` on a poisoned row — and it would format
   to **eleven** digits, which the `^[1-9][0-9]{9}$` census would then catch only after the fact.
3. **The empty `RETURNING` is guarded.** When the `WHERE` is false the statement affects no row and
   returns nothing. Shipped shape: `allocated.Count == 0 ? null : allocated[0]` (`:73`). **An unguarded
   `allocated[0]` is the defect** — it is copied verbatim from `FiscalCounterRepository.cs:63`, which
   can afford it because that counter has no cap. Pinned twice: `:118-130` (null, counter unchanged)
   and `:132-145` (`payroll.invoice.reference_capacity_exhausted`, not an exception).
4. **No call site sits inside a `BeginTransactionAsync` scope.** Grep every caller. Two properties break
   if one does: the gap semantics (it would roll back with the invoice), **and** the row-lock duration —
   the `ON CONFLICT … DO UPDATE` locks the **single** counter row, so one long transaction serializes
   every concurrent payroll run in the platform for its life.
   ⚠️ **Two of the three callers *do* call `CommitAsync` inside the handler** — `GenerateInvoice.cs:114-118`
   and `AssignInvoiceVariableSymbol.cs:115-119`, each a deliberate, commented flush that owns a
   unique-index failure instead of letting it surface as a 500. **That is not a violation of this
   invariant, and do not "fix" it:** in both, the allocation (`:88` / `:103`) happens strictly
   **before** the flush, so no transaction is open when the statement runs. What would violate it is a
   `BeginTransactionAsync` wrapping the allocation *and* the flush together.
5. **It carries a sanctioned-exception doc-comment** (`PayoutReferenceCounterRepository.cs:32-41`, plus
   the contract copy at `IPayoutReferenceCounterRepository.cs:12-17`) stating that it self-commits, that
   this is intentional and required, and what it does *not* roll back. This is what earns it a place on
   `consistency.md`'s **roster of sanctioned self-committing writes** (§"Post-commit ordering", family
   **A**) — *"an exception because it says so, not because it exists"*. **If this comment is deleted the
   write becomes a deviation**, which is the point of writing it down rather than remembering it.
6. **Zero reads or writes of `EmployeeInvoices`.** Grep the implementation for `IEmployeeInvoiceRepository`
   and `EmployeeInvoices`: zero. (`EmployeeInvoice` itself appears once, at `:21`, as the error's field
   name — see *Does NOT know*.) If it needs a repository, the responsibility is wrong.
7. **Zero tenant reads.** Grep for `ITenantProvider` / `GetCurrentTenantId` / `SetTenantOverride` in
   `PayoutReferenceAllocator.cs` and `PayoutReferenceCounterRepository.cs`: none. Contrast
   `FiscalCounterRepository.cs:19`, which does read the ambient tenant — that difference is the
   decision, not an omission.
8. **Concurrency is proven on real Postgres**, not with a mocked allocator:
   `N_Concurrent_Allocations_Yield_N_Distinct_Contiguous_Ordinals` (`:52-70`) runs 25 parallel
   allocations across 25 contexts and asserts 25 **distinct**, contiguous values with zero nulls. The
   direct analogue is `src/Cleansia.IntegrationTests/Features/Receipts/FiscalCounterAllocatorTests.cs`.
9. **The formatted symbol is asserted from a PRODUCED value, never from a fixture literal.**
   `Every_Allocated_Symbol_Is_Ten_Digits_And_Never_Starts_With_Zero` (`:147-162`) checks the allocator's
   own output, and `PayoutReferenceProductionCensusTests` drives **every production construction path**
   (the MediatR command, the background service, the admin assign) through the real pipeline and asserts
   `^[1-9][0-9]{9}$` on the persisted row. Its own header names the reason: twelve hand-authored fixture
   symbols pinned a shape production never produced, *"the exact anti-pattern that let the field ship
   null on every row"*. **A new construction path owes this census a case** — a fixture literal is not
   evidence that production formats anything.
10. **`Format` is the only formatter.** `PayoutReferenceAllocator.Format` (`:32`) is `public static` so
    tests and the census can call it; grep for a second `:D6` / `:D4` composition of a variable symbol
    anywhere in `src/` — a second one is the defect, because the no-leading-zero property is joint
    between the two parts.

## Watch-list

- **If a second kind of payout reference ever appears**, the temptation will be to add the `Scope`
  column back. Do not — re-open ADR-0046 §D2.1 first. The column was deleted *because the design could
  not name a second value for it*, and a discriminator over a one-member set sitting inside an
  `ON CONFLICT` arbiter is exactly how the nulls-distinct class of bug gets re-armed.
- **If multi-tenancy activates**, this role's "does NOT know the tenant" entry is the first thing to
  re-read. The flip is bounded and written down (counter key + `EmployeeInvoices` index, one owner-only
  migration that **fails on pre-existing duplicates**) — so it must be done while the set is small.
- **Do not generalize this into a shared `ISequenceAllocator<T>` with `FiscalCounterRepository` on the
  first repeat.** They agree on the SQL shape and disagree on all three things that matter: tenancy,
  gaplessness, and transaction participation. A generic base would have to parameterize exactly the
  properties each one exists to guarantee.
- **The year rollover is the only remedy for exhaustion**, by design. If 999 999 payout invoices in a
  year ever becomes plausible, the column width is the constraint to revisit — and it is
  `character varying(10)` on the wire to three generated clients
  (`Migrations/20260809183249_Initial.cs:1540`), so that is an epic, not a tweak.
