# Role — `PayoutReferenceAllocator` / `IPayoutReferenceAllocator` (CRC card)

> **⚠️ NOT YET BUILT — the decision is settled.** Introduced by **ADR-0046**
> (`agents/backlog/adr/0046-payout-invoice-variable-symbol-is-a-claimed-number-not-a-derived-one.md`),
> **`accepted` 2026-08-09** after a panel adjudication with twelve findings (seven blocking) folded in
> as the closed list R1–R17. Living doc:
> `agents/architecture/decisions/payout-invoice-references.md`. No ticket is cut yet.
>
> **Direct sibling of `FiscalCounterRepository.AllocateNextAsync`** (`Infra.Database/Repositories/`) —
> same one-statement UPSERT shape, same `RETURNING`, same row-lock serialization. **Read
> `FiscalCounterRepository.cs:17-64` first, and read `:28-32` twice**: two of its four comment
> paragraphs describe behaviour this allocator deliberately does **not** inherit (see *Does NOT know*).
> The allocation **statement** is copied; the **table** is not, and neither is the tenant term.

## Responsibility (one sentence)

Hand back **the next unused payout-reference ordinal for a calendar year** — a single `long`, produced
by one atomic, self-committing SQL statement that serializes concurrent callers — and refuse, as a
named business error, when that year's capacity is exhausted.

## Collaborators

- **`PayoutReferenceCounter`** — the durable counter row it owns. `Year` (`int`, **non-nullable**) is
  the **whole** key; `Value` is the ordinal. `UNIQUE (Year)`. It is **not** `ITenantEntity`.
- **`CleansiaDbContext.Database.SqlQueryRaw<long>`** — the execution seam, mirroring
  `FiscalCounterRepository.cs:59-61`. One statement: `INSERT … ON CONFLICT ("Year") DO UPDATE SET
  "Value" = "Value" + 1 … WHERE "Value" < 999999 RETURNING "Value"`.
- **`BusinessErrorMessage`** — one key it can produce:
  `InvoiceReferenceCapacityExhausted` (`payroll.invoice.reference_capacity_exhausted`), returned when the
  `WHERE` guard is false and `RETURNING` yields **no row**.
- **Callers, and there are exactly three:** `GenerateInvoice.Handler`,
  `PayPeriodBackgroundService.GenerateInvoiceForEmployeeAsync`, and the admin one-time
  assign-and-regenerate command (ADR-0046 §D4.3). Each calls it **before** constructing or stamping the
  invoice, never after.
- **The formatter** — whoever renders `$"{year:D4}{ordinal:D6}"`. This role returns the **ordinal**;
  the ten-digit string is the caller's composition. *(If the formatting ever moves in here, it moves
  with the year, and the "does NOT know" list below must be re-read.)*

## Does NOT know

- **The tenant.** It has **no tenant term**, does not read `ITenantProvider`, and does not implement
  `ITenantEntity`. This is not an oversight — the reference namespace is **global** because the shipped
  unique index on `EmployeeInvoices.VariableSymbol` is global and carries no `TenantId` (so it has no
  NULLS-DISTINCT hole), and a tenant-keyed counter under a globally-unique index means two tenants both
  allocate ordinal 1 and the second insert becomes **a 500 on the payroll path**.
  ⚠️ **It must not acquire one without the ADR-0046 §D3.2 flip**, which changes the counter key **and**
  the `EmployeeInvoices` index in the *same* owner-only migration. The global shape is **contingent on
  `Q-VS-03`** (*"does every payout leave one bank account you control"*), not forced — if that premise
  falls, the flip is the answer, not a quiet `TenantId` parameter here.
  ⚠️ **`Scope = tenantId` is the trap the precedent invites.** `FiscalCounter.cs:13-18` teaches that the
  scope string *"is the extension point"* and binds *"NOT merely the tenant"*. **This counter has no
  scope column at all**, precisely so that reading cannot be acted on.
- **The invoice.** It returns **a number, never a row**. It does not read `EmployeeInvoices`, does not
  write `EmployeeInvoices`, does not know whether the caller went on to create one, and does not know
  whether the caller's transaction succeeded. **Gaps are the direct consequence and they are correct**
  — a variable symbol is a payment reference, not a fiscal document number, and nothing in this platform
  requires it to be gapless. *A design that never gaps and sometimes duplicates is strictly worse than
  one that sometimes gaps and never duplicates.*
- **The pay period.** The year it keys on is the **year of allocation** — the moment the number is
  claimed — **not** the accounting year of the work, and not `PayPeriod.EndDate`'s year. A December
  period closed on 2 January therefore produces `2027…`. It never loads a `PayPeriod`, and it must not
  start: `GenerateInvoice.Handler` holds only `PayPeriodId` (`GenerateInvoice.cs:87-91`), so a
  period-derived year would buy this role a repository it does not otherwise need. *(ADR-0046 R10a
  records the alternative and its cost; only `Q-VS-01`'s answer moves it, and it moves in the ADR, not
  here.)*
- **Gaplessness.** That contract belongs to `FiscalCounter` (`FiscalCounter.cs:7-23` — CZ EET / DE TSE
  / AT RKSV legally require it). This counter is **deliberately gappy**, which is one of the three
  reasons it is a separate table rather than a `FiscalCounters` scope.
- **Whether a transaction is open.** Its self-committing behaviour is a **caller** property, not an API
  property — `SqlQueryRaw` *joins* an ambient transaction if one exists
  (`FiscalCounterRepository.cs:28-30` documents exactly that for this statement). It auto-commits only
  because **no payout path opens one**. So the allocator cannot detect or defend the condition it
  depends on; the caller carries the obligation (invariant 4).
- **How to format the symbol, or that ten digits is the budget.** It returns an ordinal. The
  no-leading-zero property comes from the `YYYY` prefix the caller composes, not from anything here.
- **What to do when it refuses.** At the cap it returns a failure; the caller decides. (Admin: a
  refusal on a screen where clicking again is useless. Queue: **ack** — unlike
  `payroll.invoice.reference_unavailable`, a retry inside the same year genuinely will not change the verdict.)

## Invariants a reviewer checks

1. **The key is exactly `(Year)`.** Open the entity: no `Scope`, no `TenantId`, does not implement
   `ITenantEntity`; `Year` is a non-nullable `int`; the unique index is on `(Year)` alone. **A key with
   any second column fails this check.** A non-nullable `int` arbiter is also why no
   `.AreNullsDistinct(false)` retrofit is in play — contrast `FiscalCounterEntityConfiguration.cs:26-29`,
   which needs it.
2. **The cap is in the SQL, not in C#.** The `DO UPDATE` carries `WHERE "Value" < 999999`. Without it
   the counter runs permanently past the cap, platform-wide, repairable only by a manual `UPDATE` on a
   poisoned row.
3. **The empty `RETURNING` is guarded.** When the `WHERE` is false the statement affects no row and
   returns nothing. Grep for `allocated[0]` — **an unguarded index is the defect**, copied verbatim from
   `FiscalCounterRepository.cs:63`; it must map to `payroll.invoice.reference_capacity_exhausted`.
4. **No call site sits inside a `BeginTransactionAsync` scope.** Grep every caller. Two properties break
   if one does: the gap semantics (it would roll back with the invoice), **and** the row-lock duration —
   the `ON CONFLICT … DO UPDATE` locks the **single** counter row, so one long transaction serializes
   every concurrent payroll run in the platform for its life.
5. **It carries a sanctioned-exception doc-comment**, in the `PromoCodeRepository.cs:28-38` shape,
   stating that it self-commits, that this is intentional and required, and what it does *not* roll
   back. This is the codebase's **second** such write; `consistency.md:346-353` makes the deviating form
   *"a self-committing write inside a handler **with no sanctioned-exception doc-comment**"*, and names
   its one exception *"because it says so, not because it exists"*. `consistency.md`'s list must name
   this one as the second.
6. **Zero reads or writes of `EmployeeInvoices`.** Grep the implementation: no `EmployeeInvoice`, no
   `IEmployeeInvoiceRepository`. If it needs one, the responsibility is wrong.
7. **Zero tenant reads.** Grep for `ITenantProvider` / `GetCurrentTenantId` / `SetTenantOverride`: none.
   Contrast `FiscalCounterRepository.cs:19`, which does read the ambient tenant — that difference is the
   decision, not an omission.
8. **Concurrency is proven on real Postgres**, not with a mocked allocator: N parallel allocations in
   one year → N **distinct** contiguous values, zero exceptions. The direct analogue is
   `src/Cleansia.IntegrationTests/Features/Receipts/FiscalCounterAllocatorTests.cs`.
9. **The cap is tested.** Seed `Value = 999999`, allocate, assert
   `payroll.invoice.reference_capacity_exhausted` — not an exception, and not a 1 000 000th value.

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
  `character varying(10)` on the wire to three generated clients (`Initial.cs:1522`), so that is an
  epic, not a tweak.
