---
id: T-0204
title: "Performance: missing indexes (address dedup, membership/referral), tracked reads, eager Includes, projection-before-order"
status: draft
size: M
owner: â€”
created: 2026-06-01
updated: 2026-06-01
depends_on: [T-0142, T-0196]
blocks: []
stories: []
adrs: []
layers: [backend, db]
security_touching: false
manual_steps: [ef-migration]
sprint: 3
source: PERF-* findings (soft-delete-touching indexes depend on T-0009)
---

## Context

The Wave-0 perf blocker (`User.Email` + identity lookup indexes, PERF-IDA-01/05) shipped in
**T-0124**. This Wave-3 ticket sweeps the **remaining `type: perf` findings** that are pure
backend/db cleanup â€” no behavior change, just removing the over-fetch / un-indexed-scan / tracked-read
smells the audit catalogued. It is a **consistency/quality refactor**, not a feature: the AC for every
item is *behavior unchanged (characterization test green) + the smell removed + `check-consistency.mjs`
clean for the touched area*. The canonical forms are fixed in `agents/knowledge/consistency.md` rule
**A6** (`.Include â†’ .AsNoTracking â†’ .Select(MapToDto) â†’ .ToListAsync`, project-in-query, order-before-page)
and the `patterns-backend.md` "include only what the mapper reads / `AsNoTracking` on reads / explicit
indexes on FKs and frequently-queried columns" rules.

The findings, grouped by smell, with file:line grounding:

**(1) Missing supporting indexes**
- **PERF-A1** (major) â€” `AddressRepository.GetAddressAsync` (`AddressRepository.cs:9-16`) filters
  `Street == ? && City == ? && ZipCode == ? && CountryId == ?` (Street/City are `citext`) with **no
  index** beyond the PK (`AddressEntityConfiguration.cs:13-21`). Every saved-address create/update
  (and order-creation dedup using the same repo) sequentially scans the whole cross-tenant `Addresses`
  table. Fix: composite index `(CountryId, ZipCode, City, Street)` (most-selective-first; citext indexes
  fine for equality).
- **LG-PERF-06** (minor) â€” the membership lifecycle cron sweep
  (`SendMembershipLifecycleNotifications.cs:75-80,112-118`) selects by `Status = Active AND
  RenewalReminderSentAt == null AND CurrentPeriodEnd IN [range]` with **no `UserId`**, but the only
  index leads with `UserId` (`UserMembershipEntityConfiguration.cs:60-61` `(UserId, Status)`), so the
  composite can't seek. Fix: add `(Status, CurrentPeriodEnd)` (optionally partial `WHERE
  RenewalReminderSentAt IS NULL`).
- **LG-PERF-05** (minor) â€” the referral "first qualifying order" check
  (`ReferralService.cs:192-195`) runs a correlated `EXISTS` over `Orders.UserId` +
  `OrderStatusHistory(Status)` on every completion of a referred user's order, with the supporting
  indexes unconfirmed. Fix: confirm/add the index on `Orders.UserId` and the status-history
  `(OrderId, Status)`; DB Master verifies before adding (don't add a duplicate of an existing one).
- **PERF-IDA-06** (major) â€” no index on `GdprRequest.CreatedOn`, so the admin GDPR sort is unindexed
  (`GetAllGdprRequests.cs:32-38`). See smell (4) for the paging-order half of this finding.
- **PERF-IDA-10** (minor) â€” the stale-`Device` retention sweep filters on un-indexed `LastActiveAt`
  (`DataRetentionBackgroundService.cs:101-104`; only `TenantId`, `UserId`, `(UserId, DeviceId)` exist).
  Fix: add `(IsActive, LastActiveAt)` on `Devices`. **`IsActive`/soft-delete-touching â†’ depends on
  T-0142.**

**(2) Tracked reads (missing `AsNoTracking` on read-only paths â€” A6)**
- **PERF-IDA-08** (minor) â€” `GetUser.cs:37`, `GetCurrentUser.cs:34`, `GetUserByEmail.cs:37`,
  `UserConsentRepository.cs:10-16`, `GdprRequestRepository.GetByUserIdAsync:10-16` materialize tracked
  entities on read. Add a **separate** no-tracking read method where the queryable is shared with a
  write path (`GetByEmailAsync` is used by `ChangePassword`/`Register` mutations â€” do not flip the
  shared one).
- **LG-PERF-03** (major) â€” `LoyaltyTransactionRepository.GetForAccountAsync:10-19` is a paged read with
  no `AsNoTracking` (sibling `PromoCodeRedemptionRepository`/`ReferralRepository.GetPagedAdminAsync`
  already opt out â€” this is the outlier).
- **LG-PERF-04** (minor) â€” `UserMembershipRepository.GetActiveForUserAsync:10-22` (read-only
  `GetMyMembership`, also on the CreateOrder pricing pipeline) is tracked; the webhook-mutation path
  uses the separate `GetByStripeSubscriptionIdAsync`, so this variant is safe to make no-tracking.

**(3) Eager Includes the mapper never reads (A6 "include only what the mapper reads")**
- **PERF-IDA-02** (critical) â€” `UserRepository.GetQueryable()` (`:10-16`) blanket-includes `Orders` on
  every single-user fetch (`GetUser.cs:37`, `RefreshToken.cs:68`, `ExportUserData.cs:25`,
  `UpdateAdminUser.cs:60`, `GetAdminUserById.cs:37`). Remove the blanket include; add `Include`
  explicitly only where a nav is read (none read `Orders`). **PERF-IDA-07** (the admin-path duplicate)
  is fixed by the same change.
- **PERF-D1** (major) â€” `GetPagedDisputes.cs:44-53` includes `Order, User, Messages, Evidence` but
  `MapToListItem` reads only `Order.DisplayOrderNumber` + `User` name/email + scalars; drop the
  `Messages`/`Evidence` includes (better: project the two *-to-one navs in `.Select(...)`). Verify the
  generated SQL is a single statement.
- **PERF-D2** (major) â€” `AddDisputeMessage.cs:48`, `ResolveDispute.cs:45`, `UpdateDisputeStatus.cs:37`
  load the full dispute aggregate (`Order + User + Messages.ThenInclude(Author) + Evidence`,
  `DisputeRepository.cs:26-48`) to mutate one scalar/append one row. Add a lightweight
  `GetForUpdateAsync(id, ct)` (tracked, no includes). **Serialization: `AddDisputeMessage.Handler` is
  in the TICKET-MAP shared-file cluster** â€” see Implementation notes.
- **PERF-D3** (major) â€” `GetDisputeWithDetailsAsync` (`IDisputeRepository.cs:24`,
  `DisputeRepository.cs:26-36`) drops the `CancellationToken` and never uses `AsNoTracking` on the read
  caller (`GetDisputeDetails.cs:24`). Thread the token through; split into a tracked write variant and
  an `AsNoTracking` read variant.
- **LG-PERF-01** (major) â€” `LoyaltyAccountRepository.GetByUserIdAsync:10-15` /
  `EnsureForUserAsync:17-31` unconditionally `.Include(a => a.Transactions)`, loading the whole
  append-only ledger on the **booking/quote hot path** (`LoyaltyService.ResolveTierDiscountForOrderAsync:157`
  reads only `account.CurrentTier`). Add a tier-only no-tracking read variant; fetch ledger-free on the
  grant/revoke mutation paths (EF tracks the added child without pre-loading).
- **LG-PERF-02** (major) â€” `GetMyReferral.cs:31-36` calls `CountByReferrerAsync` then
  `GetByReferrerAsync(0, totalCount)` (which `.Include(Referred)`) only to count `Qualified`/`Accepted`
  in memory. Replace with a single grouped count over the indexed `ReferrerUserId`.
- **LG-PERF-07** (minor) â€” admin paged repos (`ReferralRepository.GetPagedAdminAsync:58-94`,
  `PromoCodeRepository.GetPagedAdminAsync:24-65`) materialize full entities then map in the handler;
  project to the list DTO in-query per A6.

**(4) Projection / paging before ordering**
- **PERF-IDA-06** (major) â€” `GetAllGdprRequests.cs:32-38` applies `OrderBy` **after** `Skip/Take`
  (wrong page contents on an Article-30 compliance surface) and returns `List`, not `PagedData`.
  Re-shape to the canonical paged-query recipe (consistency A1â€“A6): `GetPagedSort<GdprRequestSort>` +
  `GetCountAsync` + `MapToDto(total, request)`, **order-before-page**, project in-query. (Index half is
  in smell (1).) This finding also has a **latent-correctness** aspect â€” flag it to the owner; it pairs
  with the GDPR-list paging fix in `PERF-IDA-06`.

## Acceptance criteria

> Every item below is a **refactor**: the test is written/confirmed **first** (characterization test
> pinning current behavior), the smell is removed, and behavior is **identical**. Evidence at review =
> the characterization test green + the diff showing the smell gone + `check-consistency.mjs` clean for
> the touched files.

- [ ] **AC1 (characterization-first, all items).** Given the touched handlers/repositories are
  currently untested, When this ticket lands, Then each modified read/write path has a characterization
  test pinning its **current observable result** (same rows, same tenant-filtering, same
  `BusinessResult`/`PagedData` shape) **written before** the change, and the status log records the
  redâ†’green per `agents/knowledge/testing.md` ("changing existing untested code â†’ characterization test
  first"). For repository/EF changes the verifiable unit is **EF model metadata** (`IModel`/
  `IEntityType.GetIndexes()`) and the repo result set â€” assert the index/no-tracking/Include change,
  not log strings.

- [ ] **AC2 (indexes â€” PERF-A1 / LG-PERF-05 / LG-PERF-06 / PERF-IDA-06 index half).** Given the EF
  configurations, When `Configure` runs, Then a composite index `(CountryId, ZipCode, City, Street)`
  exists on `Addresses` (PERF-A1); an index `(Status, CurrentPeriodEnd)` (optionally partial) exists on
  `UserMembership` (LG-PERF-06); an index on `GdprRequest.CreatedOn` (or `(Status, CreatedOn)`) exists
  (PERF-IDA-06); and the `Orders.UserId` + order-status-history `(OrderId, Status)` indexes backing
  `ReferralService`'s qualifying-order check are confirmed present (LG-PERF-05, added only if missing â€”
  DB Master verifies no duplicate). Evidence: model-metadata test per index. **No query result
  changes.**

- [ ] **AC3 (Device sweep index â€” PERF-IDA-10, depends on T-0142).** Given T-0142's soft-delete ADR has
  settled `Device`'s `IsActive` semantics, When `Configure` runs, Then a `(IsActive, LastActiveAt)`
  index exists on `Devices` and the stale-device retention sweep
  (`DataRetentionBackgroundService.cs:101-104`) is index-backed. Behavior of the sweep is unchanged
  (same rows swept). **Held until T-0142 is `done`** (see notes).

- [ ] **AC4 (tracked reads â€” PERF-IDA-08 / LG-PERF-03 / LG-PERF-04).** Given the named read-only paths,
  When they run, Then they execute with `AsNoTracking` (via a dedicated no-tracking read method where a
  write path shares the queryable â€” `GetByEmailAsync` is **not** flipped), and the returned DTOs are
  byte-for-byte identical to before. Evidence: characterization test asserts identical results; the diff
  shows `AsNoTracking()` added on the read variant only.

- [ ] **AC5 (eager Includes â€” PERF-IDA-02/07 / PERF-D1 / PERF-D2 / PERF-D3 / LG-PERF-01 / LG-PERF-02 /
  LG-PERF-07).** Given each over-fetching read/write path, When it runs after the change, Then it loads
  **only the navigations its mapper/handler actually reads** (blanket `Orders` include removed from
  `UserRepository.GetQueryable()`; `Messages`/`Evidence` dropped from `GetPagedDisputes`; the three
  dispute write handlers use a lightweight `GetForUpdateAsync`; `GetDisputeWithDetailsAsync` threads the
  `CancellationToken` and a read variant uses `AsNoTracking`; the loyalty account read uses a tier-only
  no-tracking variant; `GetMyReferral` uses a grouped count). The **observable output of every one of
  these is unchanged** (same DTOs, same counts, same auth/tenant scoping) â€” proven by the
  characterization test per path. For `GetPagedDisputes` the generated SQL is verified to be a single
  statement.

- [ ] **AC6 (projection/paging â€” PERF-IDA-06).** Given `GetAllGdprRequests`, When it runs after
  re-shaping to the canonical paged-query recipe, Then ordering is applied **before** paging, the result
  is a `PagedData<T>` with a real total, and the page contents are the correctly-ordered window.
  **Note:** this is the one item where the *observable result changes* (the old code returned the wrong
  page) â€” its characterization test pins the **current (buggy) order** first, then a new assertion pins
  the corrected order; the change is called out to the owner as a latent-correctness fix, not a silent
  behavior change.

- [ ] **AC7 (consistency gate).** Given the touched backend/db files, When `node
  agents/tools/check-consistency.mjs` runs over them, Then it reports **clean** for the A6 archetype
  (Includeâ†’AsNoTrackingâ†’Select order, project-in-query, order-before-page) for every file this ticket
  edits â€” no new deviations, and the addressed deviations no longer flagged.

- [ ] **AC8 (migration flagged, not run).** Given the new indexes require a schema change, When the
  work is complete, Then a single `MANUAL_STEP: ef-migration` is handed to the owner (build the new
  indexes `CONCURRENTLY` on the populated tables), and the ticket is **held** at the migration boundary.
  Claude does NOT run `dotnet ef migrations add` / `database update`. No `nswag-regen` is needed â€” no
  DTO/endpoint **shape** changes (the GDPR list moving `List â†’ PagedData` is the one shape change;
  if the generated client signature changes, flag `nswag-regen` then and only then).

## Out of scope

- **PERF-IDA-01 / PERF-IDA-05** (User.Email unique + identity-lookup indexes) â€” already done in
  **T-0124**. Do not re-touch `UserEntityConfiguration`'s Email/phone/code indexes.
- **PERF-IDA-03 / PERF-IDA-04** (collapse per-request duplicate user fetches; lean employee-id lookup
  on refresh) â€” these change request-orchestration / add new repo methods that touch validator+handler
  flow; track separately so this stays a pure read-shape/index sweep.
- **PERF-EMP-01** (partner dashboard endpoint fan-out) and **PERF-CAT-01/02** (catalog caching /
  client-side eval) â€” different subsystems, separate tickets.
- **PERF-IDA-09 / PERF-F1 / PERF-F2 / PERF-M1 / PERF-M2** â€” frontend bundle-coupling and mobile
  Lazy-list findings; this ticket is **backend + db only** (no frontend/android/ios change).
- The **soft-delete read-filter sweep** (S10 `IsActive` predicates on "list mine" reads) â€” owned by
  **T-0142**; this ticket only consumes T-0142's `Device.IsActive` verdict for AC3.
- **SEC-W2 filtered unique index** on `UserMembership` `(TenantId, UserId) WHERE Status=Active` and
  **LG-SEC-01** promo-redemption unique constraint â€” those are Wave-0 **security** correctness indexes
  (T-0114 / T-0110), not perf, and not in this cluster.
- Any change to dispute **authorization** logic â€” PERF-D2's `GetForUpdateAsync` must preserve the exact
  `dispute.UserId`/`dispute.TenantId` auth checks the current handlers perform.

## Implementation notes

- **TEST-FIRST per `agents/knowledge/testing.md`** ("When you're changing existing untested code":
  write a characterization test pinning current behavior first, confirm green, then refactor on top â€”
  `testing.md:53-56`). For the index/no-tracking/Include changes the verifiable unit is **EF model
  metadata** + the **repo result set**; assert invocation/metadata, never log strings or private fields
  (Reviewer rejects log-coupled tests). Gate 6 (Reviewer) enforces test-precedes-code.
- **Canonical pattern: `consistency.md` rule A6** â€” the read path is
  `.Include(...) â†’ .AsNoTracking() â†’ .Select(x => x.MapToDto()) â†’ .ToListAsync(ct)` in that order,
  project **in the query**, `.AsSplitQuery()` only when there are multiple collection includes, order
  **before** page. "Include only what the mapper reads" and "`AsNoTracking` on reads" / "explicit
  indexes on FKs and frequently-queried columns" are from `patterns-backend.md`. Note the slice report
  flags `database.md` as **stale** â€” index per the real columns, not the doc.
- **Sequence:** db/architect lock the index set + filter predicates and the new repo method signatures
  (`GetForUpdateAsync`, the no-tracking read variants, the grouped referral count) â†’ backend applies
  the read-shape changes â†’ reviewer in parallel with each developer â†’ qa. `security_touching: false`
  (no auth/data-exposure change â€” auth checks are preserved verbatim), so **no security gate**. These
  are read-shape/index changes on hot paths, so the **optimizer gate applies** (PERF-IDA-02 critical,
  LG-PERF-01 booking hot path) â€” invoke optimizer before qa to confirm the generated SQL improved and
  no plan regressed.
- **Serialization (TICKET-MAP shared-file map):** PERF-D2 edits **`AddDisputeMessage.Handler`**, which
  is in the cluster *"`AddDisputeMessage.Handler` + dispute controllers: SEC-DSP-01 â†’ DA-2 â†’ D-01
  bundle"*. SEC-DSP-01 (T-0102) is Wave-0 and DA-2 (T-0172)/D-01 (T-0173) are Wave-2 â€” all land before
  this Wave-3 ticket, so rebase PERF-D2's `GetForUpdateAsync` swap onto the **post-DA-2 handler**; do
  not run PERF-D2 concurrently with any open ticket in that cluster. `CreateOrder.cs` is **not** edited
  here (LG-PERF-01/05 touch `LoyaltyService`/`ReferralService`, not the `CreateOrder` body in the
  `F2 â†’ AUD-06 â†’ TC-4` cluster) â€” but if the loyalty/membership read-variant swap requires a one-line
  call-site change in `CreateOrder`, serialize behind **AUD-06** (T-02xx) which decomposes that file.
- **Internal serialization within this ticket:** the dispute findings (D1/D2/D3) all touch
  `DisputeRepository.cs`; the loyalty findings (LG-PERF-01/02/03/07) touch the loyalty/referral repos;
  the IDA findings touch `UserRepository`/user read handlers. Fan out **one developer+reviewer pair per
  repo group** (dispute / loyalty-referral / user-identity / address+membership-index), never two
  editing the same repo file concurrently. The four EF-config index edits (`AddressEntityConfiguration`,
  `UserMembershipEntityConfiguration`, `DeviceConfiguration`, `GdprRequest`/`Orders` configs) go in
  **one migration** â€” serialize the config edits so a single `ef-migration` MANUAL_STEP covers them.
- **Dependency on T-0142:** AC3 (Device `(IsActive, LastActiveAt)` index) reads `IsActive`, whose
  soft-delete semantics T-0142 ratifies. **Hold AC3** until T-0142 is `done`; the rest of the ticket
  (non-soft-delete-touching indexes + tracked-read + Include + projection items) may proceed once
  T-0142 lands (the dep is at the ticket level per the TICKET-MAP source note "soft-delete-touching
  indexes depend on T-0009").
- **Grounding file:line** â€” PERF-A1: `AddressRepository.cs:9-16`, `AddressEntityConfiguration.cs:13-21`;
  PERF-IDA-02: `UserRepository.cs:10-16`; PERF-IDA-06: `GetAllGdprRequests.cs:32-38`; PERF-IDA-08:
  `GetUser.cs:37`,`GetCurrentUser.cs:34`,`GetUserByEmail.cs:37`,`UserConsentRepository.cs:10-16`;
  PERF-IDA-10: `DataRetentionBackgroundService.cs:101-104`; PERF-D1: `GetPagedDisputes.cs:44-53`;
  PERF-D2: `AddDisputeMessage.cs:48`,`ResolveDispute.cs:45`,`UpdateDisputeStatus.cs:37`,
  `DisputeRepository.cs:26-48`; PERF-D3: `IDisputeRepository.cs:24`,`DisputeRepository.cs:26-36`;
  LG-PERF-01: `LoyaltyAccountRepository.cs:10-31`,`LoyaltyService.cs:157`; LG-PERF-02:
  `GetMyReferral.cs:31-36`,`ReferralRepository.cs:21-36`; LG-PERF-03:
  `LoyaltyTransactionRepository.cs:10-19`; LG-PERF-04: `UserMembershipRepository.cs:10-22`; LG-PERF-05:
  `ReferralService.cs:192-195`; LG-PERF-06: `SendMembershipLifecycleNotifications.cs:75-80,112-118`,
  `UserMembershipEntityConfiguration.cs:60-61`; LG-PERF-07: `ReferralRepository.cs:58-94`,
  `PromoCodeRepository.cs:24-65`.
- Handlers never call `CommitAsync()` (UoW pipeline commits); queries never mutate; read paths use
  `AsNoTracking`. Source findings: `agents/backlog/audits/AUDIT-2026-06-01-findings.json` (PERF-IDA-02,
  06, 07, 08, 10; PERF-A1; PERF-D1/D2/D3) and `â€¦-slice-reports.md:1713-1797` (LG-PERF-01â€¦07).

## Status log
- 2026-06-01 â€” draft (created by pm)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
