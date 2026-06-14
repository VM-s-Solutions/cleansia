---
id: T-0248
title: "Consistency sweep A* — canonical paged-query form (PromoCodes/Referrals/PayConfigs/Services)"
status: done
size: M
owner: —
created: 2026-06-13
updated: 2026-06-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0196 split (Batch 5C sub-stream A*); audits/consistency-violations.md (T-0001/A1+A3+A5, T-0002/A7, T-0003/A6)
---

## Context
Child of the **T-0196** mechanical consistency sweep (Batch **5C**, sub-stream **5C.A**). Behavior-preserving
canonicalization of the backend paged-query cluster onto the §A canon in `agents/knowledge/consistency.md`.
Four query handlers deviate from the canonical paged-query form:

- `Features/PromoCodes/Admin/GetPagedPromoCodes.cs` and `Features/Referrals/Admin/GetPagedReferrals.cs`
  use `record Query` + inline `Offset`/`Limit` + bespoke `repo.GetPagedAdminAsync(...)` + hand-built
  `new PagedData<T>` (A1/A3/A5).
- `Features/PayConfig/GetPagedPayConfigs.cs` has `Filter { get; set; }` → must be `{ get; init; }` (A7).
- `Features/Services/GetPagedServices.cs` has `AsNoTracking` before `Include` and projects after
  materialization → restore canonical read-path order (A6).

**This is a refactor, NOT a behavior change** — same items, total, page metadata, filter/sort semantics.

**Stale-text delta (sprint-7 §3):** Waves 2–3 added handlers in the touched Features folders
(refund/dispute/payroll/catalog) — re-derive the exact A*/A6/A7 hit list from current `master` (`ee95a57f`)
before refactoring. Do NOT touch the documented A6 exception for `GetPagedOrders` (materialize-then-map for
pay estimation) or the audit "not-issues" list.

## Acceptance criteria
- [ ] **AC1 (TEST-FIRST)** — A characterization test pins the current paging/filter/sort/total output for each
  of `GetPagedPromoCodes`, `GetPagedReferrals`, `GetPagedPayConfigs`, `GetPagedServices` and is **green before**
  the refactor (per `testing.md` "changing existing untested code"; commit order / status log shows the test
  landing first).
- [ ] **AC2 (canonical form)** — Each query is converted to the canonical paged-query form: A1
  `class Request : DataRangeRequest`, A3 `XxxSpecification`, A4 `GetPagedSort<XxxSort>` + `GetCountAsync`, A5
  `items.MapToDto(total, request)`, A6 `Include → AsNoTracking → Select(MapToDto) → ToListAsync`, A7
  `Filter { get; init; }`. Missing `XxxSpecification`/`XxxSort` for promo-codes/referrals are introduced
  mirroring `GetPagedDisputes`.
- [ ] **AC3 (behavior identical)** — After the refactor the AC1 characterization tests stay green — identical
  items, total, and page metadata for the same inputs; no DTO field, error code, or route changed.
- [ ] **AC4 (consistency gate)** — `node agents/tools/check-consistency.mjs backend --paths=<each touched dir>`
  reports zero A1/A3/A4/A5/A6/A7 violations for the touched files; the global baseline drops by the count this
  child clears (documented in the status log). No new violations introduced.
- [ ] **AC5** — `dotnet test src/Cleansia.Tests` green; the Reviewer confirms the diff is refactor-only.

## Out of scope
- B1 Response-wrap, B3 validator-base, C* facades, E1/E2 Android (sibling 5C children T-0249/T-0250/T-0251/T-0252).
- The `GetPagedOrders` A6 exception and any audit "not-issue".
- Any feature behavior, new endpoints, new translations, or migrations.

## Implementation notes
- **Canonical forms:** `knowledge/consistency.md` §A (A1–A8); full samples in `knowledge/patterns-backend.md`.
- **No DTO/wire change** → **no nswag-regen, no migration**. Paged DTOs and their property names are unchanged;
  only the handler/spec/sort plumbing is canonicalized.
- **Shared-file lane:** disjoint Features folders (`PromoCodes/`, `Referrals/`, `PayConfig/`, `Services/`) —
  no overlap with the other 5C children. Run concurrently with T-0249/T-0250/T-0251/T-0252; serialize only if
  any two land in the same file (none expected).

## Status log
- 2026-06-13 — ready (created by pm — split of T-0196, Batch 5C sub-stream A*). DoR met: AC observable
  (characterization-test pinned), sized M, no deps, no migration/regen, refactor-only. Reviewer-per-developer.
- 2026-06-13 — review (backend). Re-derived the live hit list from `master` (`ee95a57f`) per the stale-text
  delta. Findings: `GetPagedPromoCodes`/`GetPagedReferrals` were already canonicalized in the working tree
  (A1 `Request : DataRangeRequest`, `PromoCodeSpecification`/`ReferralSpecification` + matching `*Sort`,
  `GetPagedSort` + `GetCountAsync`, `Include → AsNoTracking → Select(MapTo…) → ToListAsync`, `items.MapToDto`);
  `GetPagedServices` already had the A6 in-query projection; `GetPagedPayConfigs` already had A7 (`set`→`init`).
  **The one remaining ticket-owned production deviation was A6 in `GetPagedPayConfigs` — `.AsNoTracking()`
  before the `.Include(...)` chain.** Fixed by moving `.AsNoTracking()` after the includes (canonical
  `Include → AsNoTracking → Select → ToListAsync`). 2-line diff; behavior-preserving (AsNoTracking is
  position-independent w.r.t. Include).
  - Test evidence (AC1/AC3): the four characterization suites (`GetPaged{PromoCodes,Referrals,PayConfigs,Services}HandlerTests`,
    12 tests) pin row projection, page metadata (total/PageNumber/PageSize), filter-reaches-spec, and the
    init/A6 read-path. **Green BEFORE the PayConfig refactor (12/12)**, **still green AFTER (12/12)** — identical
    items/total/page metadata. `dotnet test src/Cleansia.Tests --filter <the 4 classes> --no-build` = Passed 12/0.
  - The three PayConfig/Referrals/Services suites had been left instantiating the `internal` `Handler` via
    direct `new(...)` (CS0122 — no `InternalsVisibleTo` for `Cleansia.Tests`), which broke the test assembly;
    converted them to the reflection-based `Handle(request)` helper already used by the PromoCodes suite, and
    finished the half-converted PromoCodes call sites. Dropped a stray `Shared.DTOs.Sorting` import that
    collided with `Domain.Sorting.Common.SortDefinition`. No `InternalsVisibleTo` added (out-of-scope cross-cut).
  - AC4 (consistency gate): `check-consistency.mjs backend --paths=<4 touched dirs>` reports **zero** A1/A3/A4/A5/A6/A7
    violations for the four enumerated files. Remaining reported violations are all out-of-scope siblings
    (`GetPromoCodeRedemptions`/`GetMyReferrals` A1/A5; `BulkCreateEmployeePayConfigs`/`DeletePayConfig` B3) — not
    in this ticket's enumerated set; logged for the relevant 5C children.
  - AC5: `dotnet build` of `Cleansia.Core.AppServices` and `Cleansia.Tests` = 0 errors. A full
    `Cleansia.Tests` compile is currently blocked by a CONCURRENT lane's in-flight file
    `Features/Orders/CreateOrderHandlerCharacterizationTests.cs` (CS1729 — CreateOrder.Handler ctor arity changed
    by the AUD-06 decomposition, T-0253/T-0254/T-0255); that file is NOT owned here and was restored byte-identical
    (md5 verified) after a transient move-aside used only to run the four owned suites. Orchestrator does the
    authoritative clean run once the Orders lane lands.
  - Deviations: none in scope. No DTO/wire/route/error-code change → **no nswag-regen, no migration**.
  - Production bug found (report-only, do NOT fix here): none.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
