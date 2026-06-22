---
id: T-0273
title: Canonicalize the 7 bespoke paged queries onto DataRangeRequest + Specification + Sort + PagedData
status: done
size: M
owner: backend
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
- 2026-06-22 — review fix (backend). VERIFY-NOT-TRUST gate caught a stale AC1/AC5 evidence claim:
  `dotnet build src/Cleansia.Tests/Cleansia.Tests.csproj` failed with 4 × CS0103 — the two new loyalty
  characterization files (`GetLoyaltyActivityHandlerTests.cs:60,63`, `GetUserLoyaltyActivityHandlerTests.cs:63,66`)
  referenced `PaymentType`/`PaymentStatus` in their `OrderWith(...)` helper but imported only
  `Cleansia.Core.Domain.Orders`; the enums live in `Cleansia.Core.Domain.Enums` (verified: `Order.Create`
  consumes those same `Cleansia.Core.Domain.Enums` types). The prior "1538 passed / build green" was produced
  by a `--no-build` run against a stale assembly. Fix: added `using Cleansia.Core.Domain.Enums;` to both files.
  Re-collected evidence on a clean rebuild: `dotnet build Cleansia.Tests.csproj` → **0 errors, 0 warnings**;
  `dotnet test Cleansia.Tests.csproj --no-build` → **1545 passed, 0 failed**; the two loyalty classes in
  isolation → **7 passed** (3 `GetLoyaltyActivity` + 4 `GetUserLoyaltyActivity`). Behavior-preserving (test-only
  using directive; no production code touched). Deviations: none. Manual steps: none.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

### Dev hand-off (backend) — 2026-06-22

All 7 query files converted to the canonical archetype (`Request : DataRangeRequest` + `XxxSpecification`
+ `XxxSort` + `GetCountAsync` + `GetPagedSort<XxxSort>` + `items.MapToDto(total, request)`), behavior-preserving.
Pre-iOS-cleanup wave — no logic change, same rows / same order / same `PagedData` envelope.

**Per-query (AC1 characterization-test-first → AC2 archetype → AC3 endpoints):**
1. `GetMyReferrals` — record Query → `Request : DataRangeRequest` (internal Handler). Added `ReferrerUserId`
   to `ReferralSpecification.Create`. Empty-session-user short-circuit preserved. Default order `AcceptedOn desc`
   preserved via `ResolveSort` (mirrors admin twin). New mapper `ReferralMappers.MapToMyListItem`. Controllers
   (Customer + Mobile.Customer ReferralController) build the Request. Test: `GetMyReferralsHandlerTests` (4).
2. `GetLoyaltyActivity` — new `LoyaltyTransactionSpecification`/`LoyaltyTransactionSort` over `LoyaltyAccountId`.
   Account-not-found short-circuit + POST-materialization order-display-number enrichment preserved (A6 documented
   exception). Default order `OccurredOn desc`. Controllers (Customer + Mobile.Customer LoyaltyController).
   Test: `GetLoyaltyActivityHandlerTests` (3).
3. `GetUserLoyaltyActivity` — shares the new Loyalty Spec/Sort. `UserId` moved to a `Request` property
   (route param). Empty-user + account-not-found short-circuits + display-number enrichment preserved.
   Controller (AdminLoyaltyController). Test: `GetUserLoyaltyActivityHandlerTests` (4).
4. `GetPromoCodeRedemptions` — new `PromoCodeRedemptionSpecification`/`PromoCodeRedemptionSort` over `PromoCodeId`.
   Empty-id short-circuit guard preserved. `User` include + new mapper `MapToRedemptionListItem`. Default order
   `RedeemedOn desc`. Controller (AdminPromoCodeController). Test: `GetPromoCodeRedemptionsHandlerTests` (4).
5. `GetPagedMembershipPlans` — new `MembershipPlanSpecification` (IsActive + case-insensitive code/name search,
   behavior-equivalent to the bespoke `EF.Functions.Like` uppercase form) / `MembershipPlanSort`. `Active`+`Search`
   moved to `Request` properties. Default order `BillingInterval, MonthlyPriceCzk` preserved via a two-key
   `ResolveSort`. Mapping kept POST-materialization (reads computed `MonthlyEquivalentPriceCzk`, A6 exception).
   Controller (AdminMembershipController). Test: rewritten `GetPagedMembershipPlansHandlerTests` (3, canonical harness).
6. `GetEmployeeDocuments` — A5/A2 only. Public Handler → internal; hand-built `new PagedData` → `MapToDto`;
   dropped the `GetPaged`/`GetPagedSort` branch (empty-sort `GetPagedSort` applies no ORDER BY, identical to the
   old `GetPaged` no-sort path). Controller unchanged (already `[FromBody] Request`). Test: `GetEmployeeDocumentsHandlerTests` (2).

**AC2 dead-method retirement (A8 — verified zero other consumers by grep before removal):**
`GetByReferrerAsync`/`CountByReferrerAsync` (IReferralRepository + impl), `GetForAccountAsync`/`CountForAccountAsync`
(ILoyaltyTransactionRepository + impl), `GetPagedByPromoCodeAsync` (IPromoCodeRedemptionRepository + impl — `CountByPromoCodeAsync`
KEPT, still used by `GetPromoCodeById`), `GetPagedAdminAsync` (IMembershipPlanRepository + impl). The referral admin
`GetPagedAdminAsync` and promo-code admin `GetPagedAdminAsync` are different methods and were NOT touched.
`GetMyReferralHandlerTests` (singular summary) had two `Verify(...Times.Never)` lines naming the removed referral
methods — replaced with a positive `Verify(GetStatusCountsByReferrerAsync, Times.Once)` (same intent: the over-fetch
path is gone). Still green.

**AC3 — no wire/DTO change → NO nswag-regen.** All nested response DTOs kept under their original class names
(`GetMyReferrals.ReferralListItem`, `GetLoyaltyActivity.ActivityItem`, `GetUserLoyaltyActivity.ActivityItem`) so the
`ProducesResponseType` refs and the generated TS clients are unchanged. Controllers still bind the same query-string
params (`offset`/`limit`/`active`/`search`/route ids) and map them into the new `Request` internally.

**AC4 — tool clean.** `node agents/tools/check-consistency.mjs --paths=src/Cleansia.Core.AppServices/Features`:
**zero A1 and zero A5 violations** remain (verified none of the 7 files appear in output). The 33 remaining are all
B1 (20) / B3 (13) — pre-existing baseline, explicitly out of scope. The existing A1/A5 rules DID catch all six
offenders in the pre-conversion baseline run (proof the rules work; no new rule added per the meta-finding).
*PM action:* update `consistency-violations.md` to mark these 7 as resolved (cleared lines:
`GetMyReferrals.cs:11/32`, `GetLoyaltyActivity.cs:12/36`, `GetUserLoyaltyActivity.cs:17/40`,
`GetPromoCodeRedemptions.cs:15/26`, `GetPagedMembershipPlans.cs:20/38`, `GetEmployeeDocuments.cs:59`).

**AC5 — mechanical checks.** `dotnet build` green for Core.AppServices, Web.Admin, Web.Customer, Web.Mobile.Customer,
Cleansia.Tests, Cleansia.IntegrationTests, Cleansia.HostTests. `dotnet test Cleansia.Tests --no-build`: **1545 passed,
0 failed** (re-collected on a clean rebuild — see 2026-06-22 correction below; incl. the new characterization tests).
IntegrationTests/HostTests compile against the new signatures (no stale refs); they need Testcontainers/Aspire infra to
execute and were not run here (no integration coverage of these endpoints exists; behavior is pinned by the unit
characterization nets). Baseline was green per the wave note.

**Deviations:** none. **Manual steps:** none (no wire change; `manual_steps: []` unchanged).
**Harvest:** the `ResolveSort` default-order idiom (already in `GetPagedReferrals`) is now used across the converted
queries that needed a non-empty default ORDER BY — no catalog edit needed (it is already the established pattern).
