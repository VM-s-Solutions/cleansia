---
id: T-0152
title: "ADR: soft-delete policy for business entities (Deactivate vs Remove)"
status: done
size: M
owner: architect
created: 2026-06-05
updated: 2026-06-06
depends_on: []
blocks: [T-0153, T-0154, T-0191]
stories: []
adrs: [0007]
layers: [architect]
security_touching: false
manual_steps: []
sprint: 1
source: split of T-0142 (child a); consistency B6; findings DA-10/DA-15/D-09/IA-13
---

## Context
Split child **(a)** of L-epic **T-0142** (soft-delete sweep). T-0142 is too large to run as one
ticket; per the §1 proposal in `status/sprint-3.md` it splits into (a) this ADR, (b) the SavedAddress
soft-delete + read-filter + null-FK + migration work (T-0153), and (c) the Device verdict application
(T-0154). This child is the **ADR-only** deliverable and is the gate for (b) and (c).

Consistency rule **B6** (`agents/knowledge/consistency.md:67-72`) canonicalizes deletion on
**soft-delete via `repo.Deactivate(entity)`** for user/business-facing entities, reserving
`repo.Remove(entity)` (hard delete) for true join/scratch rows. B6 is an **Architect-owned judgment
call** (`consistency.md:158`): adopting it platform-wide must be ratified as an **ADR**, not ad-hoc.
The soft-delete primitives already exist (`IRepository.Deactivate/DeactivateRange`,
`Auditable.Deactivated(by, on)`) — this ADR adopts a policy, it does not build primitives.

## Acceptance criteria
- [ ] **AC1 (ADR exists & accepted)** — Given B6 is a tracked Architect judgment call, When this
  ticket completes, Then a new ADR under `agents/backlog/adr/` (next free number) is `Status: accepted`,
  follows the project ADR template (header, Context, Decision, Consequences, "How a reviewer verifies
  compliance"), and is reconciled by the reviewer with zero blocking challenges.
- [ ] **AC2 (decision rule)** — The ADR states the Deactivate-vs-Remove decision rule: user/business-
  facing → `Deactivate`; true join/scratch carrying no history and never referenced → `Remove`
  (documented per-site).
- [ ] **AC3 (S10 read obligation)** — The ADR mandates the `IsActive`-filter obligation on every "list
  mine" read (S10), so deactivated rows never resurface in a user's list/default lookups.
- [ ] **AC4 (DA-15 null-FK classification)** — The ADR classifies the `GetSavedAddresses.cs:22`
  `.Where(s => s.Address != null)` null-FK band-aid: surface/log vs. retain-with-justification — a
  binding decision T-0153 implements.
- [ ] **AC5 (per-entity verdicts)** — The ADR gives the per-entity verdict for `SavedAddress` (→
  Deactivate, the T-0153 surface) and `Device` (→ Deactivate or documented scratch-row Remove, the
  T-0154 surface).
- [ ] **AC6 (traceability)** — The ADR number is recorded back into this ticket's `adrs:` frontmatter,
  into T-0153/T-0154, and any dependent Wave-2 ticket (T-0191 = CC-02/03/04/06) references it.

## Out of scope
- Any code change — this child ships the ADR document only. SavedAddress code/migration is T-0153;
  Device code is T-0154.
- The generic program-wide hard-delete sweep (F9) — the ADR only ratifies the policy + the four named
  instances; other sites are reviewed case-by-case later.
- DA-16 (orphaned shared `Address` rows) and DA-17 (web saved-address UI) — separate tickets.

## Implementation notes
- **Architect authors; reviewer runs in parallel.** No db/backend work (T-0153/T-0154) starts until
  this ADR is `accepted`. `security_touching: false`, no QA gate (no code), `manual_steps: []`.
- Grounding for the ADR's verdicts (verified in T-0142):
  DA-10 `src/Cleansia.Core.AppServices/Features/SavedAddresses/DeleteSavedAddress.cs:56`;
  D-09 `src/Cleansia.Infra.Database/Repositories/SavedAddressRepository.cs:10-27`;
  DA-15 `src/Cleansia.Core.AppServices/Features/SavedAddresses/GetSavedAddresses.cs:22`;
  IA-13 `src/Cleansia.Core.AppServices/Features/Devices/UnregisterDevice.cs:34`;
  primitives `src/Cleansia.Core.Domain/Repositories/IRepository.cs:45,47`,
  `src/Cleansia.Core.Domain/Common/Auditable.cs:35-42`.
- Parent T-0142 holds the full AC inventory; AC2–AC5 there map to T-0153 (SavedAddress) and T-0154
  (Device). This child carries AC1's ADR slice.

## Status log
- 2026-06-05 — draft (created by pm; split of T-0142 child a)
- 2026-06-05 — ready (Batch 1A promoted; owner authorized the split; no deps; routed to architect)
- 2026-06-06 — in_review (architect authored **ADR-0007** `0007-soft-delete-policy.md`, Status: accepted,
  via deliberation panel; zero blocking). Ratifies consistency rule **B6** as platform policy. D1
  Deactivate-vs-Remove rule (user/business-facing → `Deactivate`; documented true-scratch → `Remove`);
  D2 S10 per-read `Where(IsActive)` obligation (no global filter — preserves admin-sees-all); D3 DA-15
  null-FK → **surface+log, not silently retained** (binding for T-0153); D4 explicit **GDPR
  anonymization ≠ soft-delete** boundary (`Anonymize()` PII-overwrite-keep-row vs `Deactivate`
  hide-keep-data vs `Remove`); D5 per-entity verdicts — `SavedAddress` → Deactivate (T-0153),
  `Device` → Deactivate default with documented `Remove` carve-out (T-0154). AC1-AC6 satisfied; no owner
  question raised. ADR id recorded into T-0153/T-0154/T-0191 (below). Reviewer to reconcile, then PM →
  done + unblock T-0153/T-0154.
- 2026-06-06 — done (reviewer reconciled: AC1-AC6 satisfied; ADR-0007 ratifies consistency rule B6 as
  platform policy, gives the Deactivate-vs-Remove rule (D1), the S10 per-read `Where(IsActive)` obligation
  with **no** global filter so admin-sees-all is preserved (D2), the DA-15 null-FK surface+log verdict
  (D3), the explicit GDPR-anonymization-≠-soft-delete boundary (D4), and per-entity verdicts binding
  SavedAddress→Deactivate (T-0153) and Device→Deactivate-with-documented-carve-out (T-0154) (D5). Primitives
  (`IRepository.Deactivate`, `Auditable.Deactivated`) correctly treated as existing, not built. `adrs:[0007]`
  wired. Zero blocking; ADR `accepted`). **Unblocks Batch 1B: T-0153 ∥ T-0154** (file-disjoint, parallel).

## Review
- **reviewer (2026-06-06): APPROVE.** ADR-0007 is decision-complete and grounded: it forbids a global
  `IsActive` filter (the deliberate S10 trade-off), draws the GDPR boundary (anonymize PII-bearing
  financial rows, never `Deactivate`-as-erasure), and reserves `Remove` for documented true-scratch rows.
  Per-entity verdicts are concrete and bind the consumer tickets with named code surfaces
  (`DeleteSavedAddress.cs:56`, `SavedAddressRepository.GetByUserAsync`, `GetSavedAddresses.cs:22`,
  `UnregisterDevice.cs:34`). Deliberation trail zero blocking; no owner question. **No gaps.**
- PM reconciled reviewer verdict → `done`.
