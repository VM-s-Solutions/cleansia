---
id: T-0154
title: "Device soft-delete verdict: apply the T-0152 ADR decision to UnregisterDevice (Deactivate or documented scratch Remove)"
status: ready
size: S
owner: backend
created: 2026-06-05
updated: 2026-06-06
depends_on: [T-0152]
blocks: []
stories: []
adrs: [0007]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 1
source: split of T-0142 (child c); finding IA-13
---

## Context
Split child **(c)** of L-epic **T-0142**. Applies the soft-delete ADR (T-0152) per-entity verdict for
`Device` to `UnregisterDevice`. The ADR decides whether `Device` is a true scratch row (keep `Remove`,
document why) or a business-facing row (move to `Deactivate`). Note `DeviceRepository` already filters
`IsActive` on its reads (`DeviceRepository.cs:12,18,24`) so the read side is already soft-delete-ready;
this child only applies the write-side verdict. **File-disjoint from child (b) T-0153** — may run in
parallel after T-0152 is accepted.

## Acceptance criteria
- [ ] **AC1 (IA-13 — apply the verdict)** — Given the T-0152 ADR's verdict for `Device`, When
  `UnregisterDevice` runs, Then it either calls `repo.Deactivate(device)` (if business-facing) or keeps
  `repo.Remove(device)` with an inline comment citing the ADR's scratch-row exception (if scratch). The
  chosen path has a handler unit test (mocked `IDeviceRepository`) asserting the call made and the call
  not made.
- [ ] **AC2 (test-first)** — The behavioral AC maps to a test that appears before/with the
  implementation in the diff; the status log notes red→green per `agents/knowledge/testing.md`.

## Out of scope
- The soft-delete ADR itself (T-0152) — this child consumes it.
- SavedAddress (`DeleteSavedAddress`/`GetSavedAddresses`/`SavedAddressRepository`) — that is child (b)
  T-0153.
- Device read-filter changes — `DeviceRepository` already filters `IsActive`; no change needed.
- Any frontend/mobile change; no DTO/endpoint shape change → no `nswag-regen`.

## Implementation notes
- **Gated on T-0152 accepted.** Backend-only; spawn a reviewer in parallel with the developer.
- If the verdict is `Deactivate`, the handler must call `repo.Deactivate(...)`, never set `IsActive`
  directly (B7); never call `CommitAsync()` in the handler — the UnitOfWork pipeline commits.
- **Serialization:** no TICKET-MAP shared-file cluster touches `UnregisterDevice.cs` → collision-free;
  file-disjoint from T-0153.
- Grounding: `src/Cleansia.Core.AppServices/Features/Devices/UnregisterDevice.cs:34`;
  reads already soft-delete-ready at `src/Cleansia.Infra.Database/Repositories/DeviceRepository.cs:12,18,24`.

## Status log
- 2026-06-05 — draft (created by pm; split of T-0142 child c; blocked on T-0152 ADR)
- 2026-06-06 — ready (Batch 1B; gate **ADR-0007 / T-0152 done ✓**. Applies ADR-0007 D5: `Device` → Deactivate
  default, documented `Remove` carve-out only if the deeper investigation proves no ops value (the D1 per-site
  justification comment is required if `Remove` is chosen). Routed to backend, reviewer in parallel.
  File-disjoint from T-0153 → parallel. No manual step).
- 2026-06-06 — done (backend). Verdict applied: `Device` → **Deactivate** (D5 default; no scratch-row carve-out
  taken — the deeper look confirms ops value in the unregister timestamp + reactivate-on-re-register, so no
  `Remove`). Write-side only: `UnregisterDevice.Handler` now calls `deviceRepository.Deactivate(device)` (was
  `Remove`); no direct `IsActive` set (B7), no `CommitAsync` in handler (UoW pipeline commits). Read side already
  filters `IsActive` (`DeviceRepository.cs:12,18,24`) — no change. Test-first: handler unit test
  (`UnregisterDeviceHandlerTests`, mocked `IDeviceRepository`) asserts `Deactivate` called once / `Remove` never,
  plus the missing-device no-op. red (handler still `Remove`): `Unregistering_Existing_Device_Soft_Deletes_And_Never_Hard_Removes`
  failing on the `Deactivate Times.Once` verify → green after `Remove`→`Deactivate`. dotnet build (solution) clean;
  Cleansia.Tests 569/569 pass. No manual step (no schema/DTO change).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
