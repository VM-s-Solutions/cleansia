---
id: T-0153
title: "SavedAddress soft-delete: Deactivate on delete + IsActive read filters + null-FK decision + filtered-unique-index migration"
status: ready
size: M
owner: backend
created: 2026-06-05
updated: 2026-06-06
depends_on: [T-0152]
blocks: [T-0191]
stories: []
adrs: [0007]
layers: [backend, db]
security_touching: false
manual_steps: [ef-migration]
sprint: 1
source: split of T-0142 (child b); findings DA-10/D-09/DA-15
---

## Context
Split child **(b)** of L-epic **T-0142**. Applies the soft-delete ADR (T-0152) to the `SavedAddress`
surface: switch `DeleteSavedAddress` from hard `Remove` to `Deactivate`, add the `IsActive` read
filters S10 requires, resolve the DA-15 null-FK band-aid per the ADR, and flag the filtered/partial
unique index migration that keeps the single-default-address invariant correct across deactivated rows.
**File-disjoint from child (c) T-0154** (Device) — the two may run in parallel after T-0152 is accepted.

## Acceptance criteria
- [ ] **AC1 (DA-10 — Deactivate not Remove)** — Given a customer with a saved address, When
  `DeleteSavedAddress` runs, Then the handler calls `repo.Deactivate(saved)` (not `Remove`); the row
  persists with `IsActive=false` and `DeactivatedBy/DeactivatedOn` stamped, and `BusinessResult.Success()`
  is returned. A handler unit test (mocked `ISavedAddressRepository`) asserts `Deactivate` was invoked
  and `Remove` was not.
- [ ] **AC2 (D-09 — IsActive read filters, S10)** — Given a customer has both active and deactivated
  saved addresses, When `GetSavedAddresses` runs, Then only `IsActive` rows are returned and the
  deactivated address is never returned as the default. `GetByUserAsync` and `GetDefaultForUserAsync`
  carry `.Where(s => s.IsActive)`. A test proves the deactivated address is excluded from the list and
  from the default lookup.
- [ ] **AC3 (DA-15 — null-FK per ADR)** — Given a saved address whose `Address` FK is null, When
  `GetSavedAddresses` runs, Then the row is handled per the T-0152 ADR's decision (surfaced/logged, or
  band-aid retained with a justifying comment + a non-PII log when one is skipped). The chosen behavior
  has a test pinning it.
- [ ] **AC4 (migration flag)** — Given soft-delete leaves rows in place, When the owner is handed the
  `manual_steps`, Then a `MANUAL_STEP: ef-migration` note specifies the filtered/partial unique index
  needed so the single-default-address invariant (`ClearDefaultForUserAsync`,
  `SavedAddressRepository.cs:29-39`) and any uniqueness still hold across deactivated rows. If no schema
  change is required, the ticket states that explicitly so the owner can skip it.
- [ ] **AC5 (test-first)** — Each behavioral AC maps to a test that appears before/with the
  implementation in the diff; the status log notes red→green per `agents/knowledge/testing.md`
  (characterization test pins current handler behavior first).

## Out of scope
- The soft-delete ADR itself (T-0152) — this child consumes it.
- Device (`UnregisterDevice`) — that is child (c) T-0154.
- DA-16 (orphaned shared `Address` rows) and DA-17 (web saved-address UI) — separate tickets.
- Any frontend/mobile change; no DTO/endpoint shape change → no `nswag-regen`.

## Implementation notes
- **Gated on T-0152 accepted.** Sequence: db (migration spec, if any) → backend (handler + repository
  sweep). Spawn a reviewer in parallel with the developer.
- **Owner-only manual step:** the filtered/partial unique index is an `ef-migration` — Claude does NOT
  run `dotnet ef`. The PM holds the backend close until the owner confirms it applied.
- Handler must call `repo.Deactivate(...)`, never set `IsActive` directly (B7); never call
  `CommitAsync()` in the handler — the UnitOfWork pipeline commits.
- **Serialization:** no TICKET-MAP shared-file cluster touches `DeleteSavedAddress.cs`,
  `GetSavedAddresses.cs`, or `SavedAddressRepository.cs` → collision-free; file-disjoint from T-0154.
  Wave-2 **T-0191 (CC-02/03/04/06)** depends on the T-0142 chain — do not start it until this is `done`.
- Grounding: `src/Cleansia.Core.AppServices/Features/SavedAddresses/DeleteSavedAddress.cs:56`;
  `src/Cleansia.Infra.Database/Repositories/SavedAddressRepository.cs:10-39`;
  `src/Cleansia.Core.AppServices/Features/SavedAddresses/GetSavedAddresses.cs:22`;
  `src/Cleansia.Core.Domain/Repositories/IRepository.cs:45,47`;
  `src/Cleansia.Core.Domain/Common/Auditable.cs:35-42`.

## MANUAL_STEP: ef-migration (owner-only — Claude does NOT run `dotnet ef`)

The single-default-address invariant must hold **only across active rows** once soft-delete leaves
deactivated rows in place. `Deactivate` sets `IsActive=false` but does **not** clear `IsDefault`, so a
deactivated former-default row keeps `IsDefault=true`. The existing partial unique index filters on
`"IsDefault" = true` alone, which would (a) count a deactivated default toward the one-default-per-user
uniqueness and (b) collide with a newly chosen active default. The filter must be narrowed to active
rows.

**Schema delta required:** alter the existing filtered/partial unique index on `SavedAddresses`:

- Index name: `IX_SavedAddresses_UserId_Default_Unique`
- Columns: `(UserId)`, `IsUnique = true`
- Current filter: `"IsDefault" = true`
- **New filter:** `"IsDefault" = true AND "IsActive" = true`

DB-Master change (entity config — owner/DB-Master applies, not backend dev):
`SavedAddressEntityConfiguration.cs` → `HasIndex(s => s.UserId).HasFilter("\"IsDefault\" = true AND \"IsActive\" = true").IsUnique().HasDatabaseName("IX_SavedAddresses_UserId_Default_Unique")`,
then `dotnet ef migrations add NarrowSavedAddressDefaultUniqueToActive` + `database update`.

No new column is needed — `SavedAddress : Auditable` already has `IsActive`, `DeactivatedBy`,
`DeactivatedOn`; the only change is the index filter. No NSwag regen (delete endpoint contract is
unchanged).

**Gating:** the model-level/migration assertion of the narrowed filter and any Postgres-level
two-active-defaults conflict test are **held until the owner confirms the migration applied**. The
handler + repository unit/SQLite tests in this ticket do **not** depend on the new index and are green now.

## Status log
- 2026-06-05 — draft (created by pm; split of T-0142 child b; blocked on T-0152 ADR)
- 2026-06-06 — ready (Batch 1B; gate **ADR-0007 / T-0152 done ✓**. Implements ADR-0007 D3 (null-FK
  surface+log) + D5 (SavedAddress → Deactivate). Routed to backend → db, reviewer in parallel. File-disjoint
  from T-0154 → runs parallel to it. **Owner manual_step: ef-migration** (filtered unique index / any new
  column) — flagged; held until owner confirms applied. Serialize against Wave-3 T-0203 on
  `SavedAddress` handlers (T-0203 depends_on T-0142, not live)).
- 2026-06-06 — backend implemented test-first. RED→GREEN:
  - AC1 (Deactivate not Remove): `DeleteSavedAddressHandlerTests` — `Deactivate` invoked once,
    `Remove` never; handler does not flip `IsActive` nor `CommitAsync`. Handler at
    `DeleteSavedAddress.cs:56` calls `repo.Deactivate(saved)`.
  - AC2 (S10 read filters): `SavedAddressRepositorySoftDeleteTests` (real `CleansiaDbContext` over
    SQLite) — deactivated row excluded from `GetByUserAsync` and never returned by
    `GetDefaultForUserAsync`; both reads carry `.Where(s => s.IsActive)`. `Deactivate` stamps
    `IsActive=false`+`DeactivatedBy/On` while the row survives.
  - AC3 (null-FK surface+log): `GetSavedAddressesHandlerTests` — orphaned (null `Address`) row excluded
    from the list AND logged once at Warning; clean rows map with no warning. `GetSavedAddresses.cs`
    replaces the silent `.Where(s => s.Address != null)` with a logged skip.
  - AC4 (filtered unique index): **owner ef-migration** — see the MANUAL_STEP above
    (`IX_SavedAddresses_UserId_Default_Unique` filter → `"IsDefault" = true AND "IsActive" = true`).
    Not run by backend dev. DB-Master owns `SavedAddressEntityConfiguration.cs`.
  - AC5 (test-first / characterization): handler characterization test pins the soft-delete contract
    before the impl; all 21 SavedAddresses tests green. Two unrelated working-tree breakages from a
    concurrent ticket (`Register.cs` mid-edit; `RegistrationEmailDispatchTests.cs` missing
    `using ...Services.Interfaces` for `IReferralService`) block the *full* `Cleansia.Tests` assembly —
    NOT a T-0153 file; flagged for the owning agent. T-0153 tests verified in isolation.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
