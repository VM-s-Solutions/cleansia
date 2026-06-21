---
id: T-0270
title: "E2 — convert the 3 post-Wave-5 one-shot-action VMs off loose _submitting/_loading booleans onto the shared ActionState + SharedFlow pattern"
status: draft
size: S
owner: —
created: 2026-06-21
updated: 2026-06-21
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 8
source: T-0268 (Wave-7 E2 verify-close) AC4 residual finding; audits/consistency-violations.md F14 note; knowledge/consistency.md §E2
---

## Context

Residual Android consistency rule **E2** (one-shot actions use the shared
`cz.cleansia.customer.ui.state.ActionState` (`Idle`/`Submitting`/`Error`) + a `SharedFlow(replay=0)`
success effect — never a loose `_submitting: Boolean` / `_loading: Boolean` + `_error`/outcome
`StateFlow` pair as the action machine). Rule: `knowledge/consistency.md` §E2; canonical pattern
established by **T-0252 (Wave 5)**; finding: `audits/consistency-violations.md` F14 NOTE.

T-0268 (the Wave-7 E2 **verify-and-close** disposition) confirmed the **audit-named** F14 set
(customer `CreateDispute`/`Membership`/`Profile` + partner ex-`OrderAction inFlight`) is fully on the
shared `ActionState` and cleared F14. **In doing so its AC1 scan surfaced 3 GENUINE residual E2
violations that postdate the 2026-06-01 audit and T-0252** — one-shot ACTION paths shipped after the
F14 work that still use a loose boolean as the action machine. They are out of F14's named set, so
T-0268 correctly did **not** fix them; they are carried here as a scoped, behavior-preserving
follow-up.

### In scope — convert (3 customer-app VMs, test-first)

1. **`customer/features/recurring/CreateRecurringViewModel.kt`** (≈line 60) —
   `_submitting = MutableStateFlow(false)` drives `submit()`; the outcome is surfaced via
   `_submitOutcome: StateFlow<SubmitOutcome?>` (a held StateFlow, not a one-shot `SharedFlow(replay=0)`).
   Convert the action machine to `ActionState` (`Idle`/`Submitting`/`Error`) and the outcome to a
   one-shot effect (`SharedFlow(replay=0)` / callback), matching T-0252.
2. **`customer/features/disputes/DisputeDetailViewModel.kt`** (≈lines 57, 66) — two loose `Boolean`
   StateFlows for two distinct one-shot actions: `_sending` (`sendMessage`) and `_uploadingEvidence`
   (`uploadEvidence`). Each gets its own `ActionState` action machine + one-shot success effect.
3. **`customer/features/profile/DeleteAccountViewModel.kt`** (≈line 22) —
   `_loading = MutableStateFlow(false)` gates the one-shot `deleteAccount()` action (milder: a single
   boolean, no `_error`). Convert to `ActionState` + a one-shot completion effect.

All three are **behavior-correct today** — this is a pure canonicalization onto the established
pattern; impossible/loose states become unrepresentable and the success path becomes a one-shot effect
(no replay on rotation), exactly as the T-0252 VMs.

### Explicitly NOT in scope — recorded judgment-call NON-violations (do NOT convert)

These are **per-row / per-button in-flight discriminators** that a single `ActionState` (one
`Idle`/`Submitting`/`Error` for the whole VM) **structurally cannot express** — they must remain a
keyed/enum discriminator. They are recorded NON-violations, not E2 defects:

- partner `features/orders/OrderDetailsViewModel._inFlightAction` — **per-button** (which action's
  spinner), layered on top of the VM's `actionState`.
- partner `features/orders/OrdersListViewModel.inFlightActionOrderId` — **per-row** (which order is
  mutating).
- customer `features/recurring/RecurringBookingsViewModel._mutating` — **per-row** (which booking is
  mutating).

## Acceptance criteria

- [ ] **AC1 (characterization-first, per `testing.md`)** — For each of the 3 VMs, a ViewModel test pins
  the current action behavior (Idle→Submitting→success-effect, Submitting→Error on failure, button
  disabled while in-flight) **before** the refactor; status log shows test-first ordering (red→green).
- [ ] **AC2 (E2 canonical form)** — Each of `CreateRecurringViewModel`, `DisputeDetailViewModel` (both
  its `sendMessage` and `uploadEvidence` actions), and `DeleteAccountViewModel` exposes its one-shot
  action(s) as `ActionState` (`cz.cleansia.customer.ui.state.ActionState`) + a `SharedFlow(replay=0)`
  (or equivalent one-shot callback) success/completion effect; the loose `_submitting`/`_loading` +
  `_submitOutcome`/`_error` action booleans are removed.
- [ ] **AC3 (behavior identical)** — Same screen behavior for the same inputs: the action button
  disables while submitting, the success effect fires exactly once (does **not** replay on config
  change / rotation), and error surfaces as before. No new product behavior.
- [ ] **AC4 (consistency gate + scan clean)** — `node agents/tools/check-consistency.mjs --paths=src/cleansia_android`
  shows no E2 regression, and a manual re-scan confirms these 3 VMs no longer use a loose
  `_submitting`/`_loading` boolean as the action machine. The 3 recorded per-row/per-button
  discriminators (above) are left untouched and noted as accepted NON-violations.
- [ ] **AC5 (suite green / compiles)** — customer-app builds and `:customer-app:testDebugUnitTest` is
  green on plain JVM (the existing 201 plus the new characterization tests); reviewer confirms
  refactor-only (no behavior change).

## Out of scope

- The per-row/per-button in-flight discriminators (`OrderDetailsViewModel._inFlightAction`,
  `OrdersListViewModel.inFlightActionOrderId`, `RecurringBookingsViewModel._mutating`) — **recorded
  judgment-call NON-violations**; `ActionState`'s single Idle/Submitting/Error cannot express
  which-row/which-button, so they correctly stay as keyed discriminators.
- The already-canonical F14-named VMs (customer `CreateDispute`/`Membership`/`Profile`, partner
  ex-`OrderAction inFlight`) — done by T-0252, verified by T-0268. No churn.
- E1 (sealed `*UiState`), E6 (`collectAsStateWithLifecycle`), E7 (dir/naming) — separate rules
  (T-0267/T-0269/T-0266, all done in Wave 7).
- Any new screen, feature behavior, API/DTO change, or i18n change.

## Implementation notes

- **Canonical form:** `knowledge/consistency.md` §E2; reuse the exact shape T-0252 established on
  `CreateDisputeViewModel`/`MembershipViewModel`/`ProfileViewModel` (shared
  `cz.cleansia.customer.ui.state.ActionState` + a `SharedFlow(replay=0)` / callback effect).
- **Reuse, don't recreate:** the shared `ActionState` type already exists — do not introduce a parallel
  type. The one-shot effect must be `replay=0` so success does not re-fire on rotation.
- **No new behavior/decision → no deliberation panel.** Pure mechanical canonicalization of 3 VMs
  against the already-ratified §E2 rule and the T-0252 reference implementation; the only judgment (the
  per-row/per-button exclusions) is already recorded above.
- **No `manual_steps`** — mobile-only, customer-app only: no nswag-regen, no ef-migration, no i18n
  change (reuses existing string keys / effects).
- **Files are customer-app only** — no overlap with the partner-files lane (T-0266/T-0267/T-0269, all
  done). When promoted, runs as a single S ticket, reviewer-per-developer, no security/optimizer gate.

## Status log
- 2026-06-21 — draft (created by pm). Sprint 8 (next-wave candidate). **Source: T-0268 (Wave-7 E2
  verify-close) AC4 residual** — the 3 one-shot-action VMs that postdate T-0252's F14 work and still use
  a loose `_submitting`/`_loading` boolean as the action machine. DoR-aligned for promotion: AC
  observable + characterization-first, sized **S**, no deps (customer-app-only, partner lane done),
  mobile-only (no migration/regen/i18n), behavior-preserving. **No deliberation panel** (no-decision
  canonicalization against §E2 + the T-0252 reference). Per-row/per-button discriminators explicitly
  excluded as recorded NON-violations.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
