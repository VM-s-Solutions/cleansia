---
id: T-0249
title: "Consistency sweep B1 — wrap command scalar/flag in record Response (CreateDispute/UpdateDisputeStatus/DeleteSavedAddress)"
status: ready
size: S
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: []
blocks: [T-0202]
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
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

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
