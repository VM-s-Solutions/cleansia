---
id: T-0273
title: Canonicalize the 7 bespoke paged queries onto DataRangeRequest + Specification + Sort + PagedData
status: ready
size: M
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 9
---

> **No-decision note (panel skipped):** mechanical canonicalization onto the **already-ratified** paged
> archetype (`consistency.md` rules A1–A8; canonical exemplars `GetPagedServices.cs`,
> `GetPagedDisputes.cs`, `PageDataMapper.cs`). No new behavior, no new decision — each query returns the
> same rows in the same order as today. `check-consistency.mjs` already flags every offender (A1/A5), so
> this is "make the green-light tool's existing findings true", not a design call.

## Context

Owner P4 + audit findings #4/#5/#6/#12 + the meta-finding. A cluster of paged queries hand-roll the
paged archetype instead of using the canonical
`class Request : DataRangeRequest` + `XxxSpecification` + `GetPagedSort<XxxSort>` + `GetCountAsync` +
`items.MapToDto(total, request)` path. **Reconciled against current `master` + `check-consistency.mjs`**
— the genuine live offenders are **7** (the audit named 5 conversions + 1 LOW A5-only; the owner's
example `GetPagedMembershipPlans` was MISSED by the audit but IS flagged by the tool; the owner's other
example `GetPagedDisputes` is **already canonical A1–A8** and is NOT touched):

| # | File | Rules fired | Spec/Sort exists? |
|---|------|-------------|-------------------|
| 1 | `Features/Referrals/GetMyReferrals.cs:11,32` | A1, A5 | `ReferralSpecification`/`ReferralSort` exist (admin twin `Admin/GetPagedReferrals.cs` is canonical) — add `ReferrerUserId` to `ReferralSpecification.Create` |
| 2 | `Features/Loyalty/GetLoyaltyActivity.cs:12,36` | A1, A5 | **No Spec/Sort** — add `LoyaltyTransactionSpecification`/`Sort` over `LoyaltyAccountId` |
| 3 | `Features/Loyalty/Admin/GetUserLoyaltyActivity.cs:17,40` | A1, A5 | shares the same repo method + the new Spec/Sort from #2 |
| 4 | `Features/PromoCodes/Admin/GetPromoCodeRedemptions.cs:15,26` | A1, A5 | **No Spec/Sort** — add `PromoCodeRedemptionSpecification`/`Sort` over `PromoCodeId`; keep the empty-id short-circuit guard |
| 5 | `Features/Memberships/Admin/GetPagedMembershipPlans.cs:20,38` | A1, A5 | bespoke `GetPagedAdminAsync(active, search, …)` repo method + manual page math; add `MembershipPlanSpecification`/`Sort` (active filter + case-insensitive code/name search) |
| 6 | `Features/EmployeeDocuments/GetEmployeeDocuments.cs:59` | A5 only | already uses the canonical spec path; only hand-builds `PagedData` + public Handler (A2) — smallest fix |

(That table is 6 rows because #2/#3 are one Spec/Sort serving two queries — **7 query files total**.)

**Meta-finding (the lesson):** `agents/backlog/audits/consistency-violations.md` claims the backend
paged sweep complete, but `check-consistency.mjs` flags all six A1/A5 offenders **and they were never
ticketed**. The A1/A5 rules already exist and already catch a bespoke paged query — the gap is purely
that these were never converted or recorded. The owner's "add an A* rule so this is caught in future"
is **already satisfied by the existing A1/A5 rules**; the honest fix is to *ticket the offenders* and
*update consistency-violations.md*, not to add a duplicate rule. (See AC4.)

## Acceptance criteria

- [ ] **AC1 — Characterization-test-first per query.** For each of the 7 query files, the existing
  handler test (or a new characterization test where none exists) pins the **current** result set —
  same rows, same order, same total, same page-number math at the boundaries (offset/limit edges,
  empty result, the empty-id short-circuit for #4) — and stays green through the conversion. Money/
  enrichment specifics preserved: GetLoyaltyActivity's display-number enrichment runs
  **post-materialization** (unchanged); GetMyReferrals' empty-user short-circuit preserved.
- [ ] **AC2 — Each query uses the canonical archetype.** Each converted query is
  `class Request : DataRangeRequest, IRequest<PagedData<T>>` (or `ICommand` per the local convention),
  an **internal** Handler, a `XxxSpecification` + `XxxSort`, `GetCountAsync(filter)` +
  `GetPagedSort<XxxSort>(offset, limit, filter, sort)`, `AsNoTracking`, projection via
  `MapToDto`/`MapToListItem`, returning `items.MapToDto(total, request)` (no hand-built `new PagedData`,
  no manual `(offset/limit)+1`). The bespoke repo methods that become dead (`GetByReferrerAsync`/
  `CountByReferrerAsync`, `GetPagedAdminAsync`, the loyalty/redemption bespoke pagers) are removed or
  retired per A8.
- [ ] **AC3 — Endpoints behave identically.** Each query's controller/integration test passes
  unchanged: the admin/customer/partner endpoints (`AdminMembershipController`, `ReferralController`,
  `AdminPromoCodeController`, the loyalty + employee-document controllers) return the same payload
  shape and the same rows for the same request as before. No wire DTO change → **no nswag-regen**
  (the `PagedData<T>` envelope is already what these return; only the server-side construction changes).
- [ ] **AC4 — Tool is clean + the audit doc is honest.** After conversion,
  `node agents/tools/check-consistency.mjs --paths=src/Cleansia.Core.AppServices/Features` reports
  **zero A1/A5 violations for these 7 files** (regression guard). The **existing** A1/A5 rules are
  confirmed to catch a bespoke paged query (a deliberately-reverted sample re-trips A1 in review) — so
  **no new rule is added** (it would duplicate A1/A5). `consistency-violations.md` is updated to record
  these as resolved (closes the meta-finding's staleness). *(The consistency-violations.md edit is the
  PM's to apply per ownership — the dev confirms the tool is clean and lists the cleared lines.)*
- [ ] **AC5 — Mechanical checks green.** `dotnet build` + all three test projects
  (`Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`) pass on the merged tree.

## Out of scope
- **`GetPagedDisputes`** — already canonical (A1–A8 clean, verified); NOT touched. (The owner's
  earlier quick-classify flagged it; the audit coverage note and a re-read of `GetPagedDisputes.cs`
  confirm it is canonical.)
- **No wire/DTO change** — these already return `PagedData<T>`; the conversion is server-internal only.
  If any conversion would change the response shape, **stop and flag it** (it should not).
- **No new sort columns / new filters** beyond what each query already supports (a Spec/Sort is added
  to *express* the existing behavior canonically, not to add capability).
- **The B1/B3 Auth-validator and other non-paged consistency items** the tool also lists are pre-existing
  baseline (the deliberate shared-validator composition) — **not** in this ticket.
- **Dead paged dups `GetAllEmployees` / `GetUserByEmail`** — those are deletes, handled by T-0275.

## Implementation notes

Canonical exemplars to mirror: `Features/Services/GetPagedServices.cs` (full archetype),
`Features/Disputes/GetPagedDisputes.cs` (filter→`MapToDomain()`→`SatisfiedBy()` spec + role scoping),
`Mappers/PageDataMapper.cs:8-15` (`MapToDto(total, request)`). The Wave-5 T-0248 did the same conversion
for `GetPagedPromoCodes`/`GetPagedReferrals` — copy that shape.

**Fan-out:** this is broad-but-mechanical and the queries are **disjoint per feature** — fan out
**one backend dev + one reviewer per feature group** in parallel (group A: Referrals; group B: Loyalty
pair + new Spec/Sort; group C: PromoCodeRedemptions; group D: MembershipPlans; group E: EmployeeDocuments
A5-only). No two groups edit the same file. Serialize only if two groups touch a shared repo/mapper
(they don't). Each group is TDD: characterization test green → convert → tool clean.

**Routing:** `[backend]` only. `reviewer`-per-dev. `qa` = suite-green + the per-query AC1↔test mapping
+ the AC4 tool-clean evidence. No `security` (read-only queries, no authz change — the role-scoping in
GetPagedDisputes is the canonical example and is untouched). `optimizer` advisory only if a converted
read path changes its query plan (it should be equivalent — same WHERE/ORDER, now via Spec/Sort).

## Status log
- 2026-06-22 — draft → ready (created by pm). Reconciled against master + `check-consistency.mjs`:
  GetPagedDisputes REFUTED as an offender (canonical A1–A8); GetPagedMembershipPlans CONFIRMED an
  offender the audit MISSED (tool flags A1+A5); the audit's 5 + GetEmployeeDocuments A5 + GetPagedMembershipPlans
  = **7 live query files**. No-decision (ratified A1–A8 archetype). `manual_steps: []` (no wire change).
  Sized **M** (7 disjoint mechanical conversions under per-query characterization nets; if any group
  grows past M at dispatch — e.g. a Spec/Sort turns out to need real new domain modeling — stop and
  split that group). Meta-finding folded into AC4 (no new rule; ticket the offenders + de-stale the
  audit doc).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
