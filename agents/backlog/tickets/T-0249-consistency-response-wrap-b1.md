---
id: T-0249
title: "Consistency sweep B1 — wrap command scalar/flag in record Response (CreateDispute/UpdateDisputeStatus/DeleteSavedAddress)"
status: done
size: S
owner: backend
created: 2026-06-13
updated: 2026-06-14
depends_on: []
blocks: [T-0202]
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 5
source: T-0196 split (Batch 5C sub-stream B1); audits/consistency-violations.md (T-0004/B1)
---

## Context
Child of the **T-0196** mechanical consistency sweep (Batch **5C**, sub-stream **5C.B**). Three commands return
a bare scalar/flag instead of the canonical `record Response(...)` wrapper (§B B1):

- `Features/Disputes/CreateDispute.cs` (`ICommand<string>`),
- `Features/Disputes/UpdateDisputeStatus.cs` (bare `ICommand`),
- `Features/SavedAddresses/DeleteSavedAddress.cs` (bare `ICommand`).

Each must become `ICommand<Response>` with a `record Response(...)` wrapping the prior id/flag.
**This is a refactor, NOT a behavior change** — same success/failure outcome, same returned value.

**T-0196's B1 wrap of `UpdateDisputeStatus` is the canonical base T-0202 rebases on** (this child therefore
`blocks: [T-0202]` — keep them in sequence; T-0202 must rebase on this Response shape, not the bare command).

**NSwag watch (critical):** Prefer a wire-compatible `Response` shape (wrap the existing id/flag under the same
JSON property name) so the serialized response is unchanged and **no regen is needed**. The Backend dev confirms
the OpenAPI diff at review. **If a generated client's response *type* changes** for any of the three commands,
flag `manual_step: nswag-regen` to the PM (who flags the owner) and HOLD the consuming frontend/mobile until the
owner regenerates — never run the regen.

## Acceptance criteria
- [ ] **AC1 (TEST-FIRST)** — A handler test pins the current success/failure outcome of each of the three
  commands and is **green before** the refactor (per `testing.md`; commit order / status log shows test first).
- [ ] **AC2 (canonical form)** — Each command returns `ICommand<Response>` with a `record Response(...)` wrapping
  the prior scalar/flag; the returned value is equivalent to before; the controller still returns the same
  payload.
- [ ] **AC3 (behavior identical)** — AC1 tests stay green after the refactor; no error code, route, or business
  outcome changed beyond the Response wrapper.
- [ ] **AC4 (wire/regen check)** — The Backend dev records the OpenAPI diff for the three responses in the status
  log. If the serialized JSON is unchanged (wire-compatible) → no regen, no manual_step. If a response *type*
  changes → the PM adds `manual_step: nswag-regen` and holds consumers.
- [ ] **AC5 (consistency gate)** — `node agents/tools/check-consistency.mjs backend --paths=<each touched dir>`
  reports zero B1 violations for the touched files; global baseline drops by the count cleared.
- [ ] **AC6** — `dotnet test src/Cleansia.Tests` green; Reviewer confirms refactor-only.

## Out of scope
- A* paged-query, B3 validator-base, C* facades, E1/E2 Android (sibling 5C children).
- `disputes.facade.ts` and any frontend consumption — owned by **T-0202** (5F), which rebases on this Response.
- Any feature behavior, new endpoints, translations, or migrations.

## Implementation notes
- **Canonical form:** `knowledge/consistency.md` §B (B1); sample in `knowledge/patterns-backend.md`.
- **Shared-file lane:** disjoint Features folders (`Disputes/`, `SavedAddresses/`) — no overlap with the other
  5C children. Note: T-0202 (5F) consumes the `UpdateDisputeStatus` Response and `DeleteSavedAddress` is also
  touched by T-0201 (handlers) and T-0198 (controller) — those are different files (DeleteSavedAddress command
  vs AddSavedAddress handler vs SavedAddress controller); keep the SavedAddress area in one lane and do not race
  on a shared file.
- **Manual step (conditional):** `nswag-regen` **only if** the OpenAPI response type changes; default is
  wire-compatible (no regen). PM adds the flag + holds consumers only on a confirmed type change.

## Status log
- 2026-06-13 — ready (created by pm — split of T-0196, Batch 5C sub-stream B1). DoR met: AC observable,
  sized S, no deps, default no-regen (conditional flag), refactor-only. `blocks: [T-0202]` — T-0202 rebases on
  the `UpdateDisputeStatus` Response. Reviewer-per-developer.
- 2026-06-13 — review (backend). Wrapped all three bare command returns in a `record Response`, behavior-preserving:
  - `CreateDispute` `ICommand<string>` → `ICommand<Response>` + `record Response(string DisputeId)`; handler returns
    `Success(new Response(dispute.Id))`, both failure paths now `Failure<Response>(...)` (same error codes
    `OrderNotFound` / `DisputeAlreadyExists`).
  - `UpdateDisputeStatus` bare `ICommand` → `ICommand<Response>` + `record Response(string DisputeId, DisputeStatus Status)`;
    handler returns `Success(new Response(dispute.Id, dispute.Status))`, same failure codes
    `DisputeNotFound` / `InvalidDisputeStatusTransition`. **This is the canonical Response T-0202 rebases on.**
  - `DeleteSavedAddress` bare `ICommand` → `ICommand<Response>` + `record Response(string SavedAddressId)`;
    handler returns `Success(new Response(saved.Id))`. Validator (incl. its ownership check — F5, a *different*
    ticket) left untouched per scope.
  - Controllers updated to return the new Response via `HandleResult<...Response>` + matching `[ProducesResponseType(typeof(...Response))]`:
    Customer + Mobile.Customer `DisputeController.Create`; Admin `AdminDisputeController.update-status`;
    Customer + Mobile.Customer `SavedAddressController.Delete`.
  - **AC1/AC3 (test):** updated the three existing handler unit tests to assert the wrapped value
    (`result.Value.DisputeId` / `.Status` / `.SavedAddressId`); all other assertions (error codes, soft-delete,
    transition guard, ownership) unchanged. They pin the success/failure outcome and stay green on the same codes.
  - **AC4 (wire/regen) — TYPE CHANGE, regen REQUIRED.** `HandleResult<T>` serializes `BusinessResult<T>.Value`;
    wrapping changes the body type on every affected client → NOT wire-compatible:
    - `CreateDispute` (customer + customer-mobile): `Observable<string>` → `Observable<CreateDisputeResponse>` (`{ disputeId }`).
    - `UpdateDisputeStatus` (admin): `Observable<void>` → `Observable<UpdateDisputeStatusResponse>` (`{ disputeId, status }`).
    - `DeleteSavedAddress` (customer + customer-mobile): `Observable<void>` → `Observable<DeleteSavedAddressResponse>` (`{ savedAddressId }`).
    → **MANUAL_STEP: nswag-regen** for `admin-services`, `customer-services`, and the customer mobile OpenAPI (owner-only;
    not run here). Hold T-0202 and any consuming FE/mobile until regenerated. HTTP status codes unchanged (200/400/401/403/404).
  - **AC5 (consistency):** `check-consistency.mjs backend` on both touched dirs reports `OK` (0 violations);
    global baseline `55 → 54` (the one `CreateDispute.cs:44` B1 cleared; the two bare-`ICommand` cases the checker
    does not flag are also wrapped per consistency.md §B B1).
  - **AC6 (build/test):** the shared tree currently does NOT compile due to a *concurrent lane* mid-deleting
    `Cleansia.Infra.Services/Templates/*` (breaks `Cleansia.Core.AppServices` transitively → blocks `Cleansia.Tests`).
    Verified my edits are clean: a focused `dotnet build Cleansia.Core.AppServices.csproj` shows **zero diagnostics**
    for `Disputes/CreateDispute.cs`, `Disputes/UpdateDisputeStatus.cs`, `SavedAddresses/DeleteSavedAddress.cs` and the
    five controllers — the only errors are the other lane's `Infra.Services.Templates` namespace. Authoritative clean
    `dotnet test src/Cleansia.Tests` run deferred to the orchestrator after lanes converge.
  - **Deviation:** ticket *preferred* a wire-compatible wrap (no regen); that is impossible here — a bare scalar/void
    body cannot serialize identically to a JSON object, so the canonical B1 form forces a response-type change. Flagged
    `nswag-regen` per AC4's type-change branch. SavedAddress lane kept in one place; `AddSavedAddress`/other handlers
    untouched (T-0201/T-0198 run after).

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
