---
id: T-0248
title: "Consistency sweep A* — canonical paged-query form (PromoCodes/Referrals/PayConfigs/Services)"
status: ready
size: M
owner: —
created: 2026-06-13
updated: 2026-06-13
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

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
