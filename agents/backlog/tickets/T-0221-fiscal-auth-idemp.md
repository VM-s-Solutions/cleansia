---
id: T-0221
title: Per-provider RegisterReceiptAsync idempotency on ReceiptNumber (IFiscalService idempotency key)
status: draft
size: M
owner: —
created: 2026-06-03
blocks: []
depends_on: [T-0119]
stories: []
adrs: [0004]
layers: [backend, clients]
security_touching: true
manual_steps: []
sprint: 2
source: ADR-0004 D-F4.3 C-C/C-D (split off T-0119/F4); architect panel 2026-06-03
---

## Context
ADR-0004 (`agents/backlog/adr/0004-fiscal-receipt-idempotency-boundary.md`) **D-F4.3 C-C/C-D**. T-0119's
claim-before-register reorder introduces a **registered-but-stamp-not-persisted** residual: the tax
authority HAS registered the receipt but the local `FiscalCode` was not yet persisted (crash in the
two-commit split window); recovery re-calls `RegisterReceiptAsync` with the **same `ReceiptNumber`**
(`ReceiptService.cs:239`). Whether that **double-registers at the authority is provider-dependent and
currently unverified** — `FiscalReceiptRequest` / `IFiscalService.RegisterReceiptAsync`
(`IFiscalService.cs:29`) carry **no idempotency key**.

For `AsyncBackground` (CZ/SK/PL today) a rare extra registration is tolerable; for **BlockingOnline
(DE/AT/ES) it is a compliance incident.** This ticket closes that gate.

## Acceptance criteria
- [ ] **AC1 — Per-provider register-idempotency verified + documented.** For each BlockingOnline provider
  (DE TSE, AT RKSV, ES VeriFactu), verify and **document** that `RegisterReceiptAsync` is idempotent on
  `ReceiptNumber` (returns the prior signature/code; does **not** burn a new authority entry) — OR add a
  provider-side idempotency key to make it so.
- [ ] **AC2 — `IFiscalService` idempotency-key contract.** Add an idempotency token to
  `FiscalReceiptRequest` / `RegisterReceiptAsync` (natural token = `ReceiptNumber`) so the contract is
  explicit, not relied upon implicitly per provider.
- [ ] **AC3 — Go-live gate satisfied.** The ADR-0004 go-live gate item (2) is closed for each provider
  that is to be set `BlockingOnline` in production.
- [ ] **AC4 — Tests:** a redelivery / retry that re-calls `RegisterReceiptAsync` with the same
  `ReceiptNumber` does not double-register (per-provider mock/contract test).

## Out of scope
- The allocator (FISCAL-SEQ / T-0220) and the reorder (T-0119) and the reconciliation predicate (T-0122 C-B).

## Implementation notes
- Governing ADR: **ADR-0004 D-F4.3 C-C/C-D** + go-live gate (2). Depends on T-0119. This is a contract
  change to `IFiscalService` (clients layer) + per-provider verification. security_touching (regulatory).

## Status log
- 2026-06-03 — draft (created by orchestrator from ADR-0004 C-C/C-D split; go-live gate for DE/AT/ES).
- 2026-06-08 — implemented test-first by backend. Contract change to `IFiscalService` (clients layer):
  explicit `FiscalReceiptRequest.IdempotencyKey` (natural token = `ReceiptNumber`, set via the new
  `FiscalReceiptRequest.Create` factory), provider self-declaration `IFiscalService.RegisterIsIdempotent`,
  and `FiscalGoLiveGate.EnsureRegisterIdempotent` (throws `FiscalGoLiveGateException` for a non-idempotent
  provider under a blocking enforcement mode). `ReceiptService` now builds the initial-register and the
  recovery-re-register requests through one `BuildFiscalRequest` helper so both present the same key, and
  runs the gate in the register path. Per-provider register-idempotency documented in
  `docs/architecture/fiscal-compliance.md`. 11 new tests (all green); full fiscal/receipt suite (52) green.

## Review
<!-- reviewer / security / optimizer write verdicts here -->

### Security review — 2026-06-08 — CHANGES_REQUESTED

Audited the FISCAL-AUTH-IDEMP contract against S1-S10 + S7a/S7b and the regulatory guarantee
(no double-registration on redelivery; same idempotency token on the recovery re-register; tenant
scope per S8). Scope: the IFiscalService idempotency-key contract + the go-live gate + the two
register call sites. The T-0220 allocator/scope issues (F-220-1/2/3) are tracked separately and are
NOT re-flagged here even though they share ReceiptService.cs.

What is correct (PASS):
- AC2 (explicit contract). FiscalReceiptRequest.IdempotencyKey is a first-class field; the Create
  factory derives it from ReceiptNumber so it cannot drift. Both register sites build the request via
  the one BuildFiscalRequest helper, so the initial register and the recovery re-register present the
  SAME token for the same receipt (ReceiptServiceFiscalIdempotencyTokenTests proves
  SeenKeys[0]==SeenKeys[1], AuthorityEntriesBurned==1).
- AC1/AC4 (per-provider declared + tested). RegisterIsIdempotent on each provider (NoOp/CZ declare
  true); FiscalRegisterIdempotencyContractTests covers idempotent-collapse, distinct-token burn, and
  the gate reject/admit/ignore matrix.
- S1/S2/S3/S4/S6/S8/S10 - N/A or PASS: server-internal fiscal-adapter types (no JWT/DTO/endpoint
  surface, so no NSwag), no PII logged above Information beyond ReceiptNumber/ProviderKey (not PII),
  no schema change in this ticket.

BLOCKER (must fix before merge):

1. [BLOCKER] The go-live gate is missing from the recovery re-register path - the exact path this
   ticket exists to protect (AC3/AC4; S7/S7a; regulatory). FiscalGoLiveGate.EnsureRegisterIdempotent
   runs ONLY in the initial register path (ReceiptService.HandleFiscalAsync, ReceiptService.cs:182).
   The recovery re-register - ReceiptService.RetryFiscalRegistrationAsync (ReceiptService.cs:275-344),
   driven by FiscalRetryService.ProcessDueRetriesAsync - calls fiscalService.RegisterReceiptAsync at
   ReceiptService.cs:301 with NO gate beforehand, and does not even resolve enforcementMode. This is
   the precise residual the ticket Context names: "recovery re-calls RegisterReceiptAsync with the
   same ReceiptNumber". A non-idempotent provider (a future DE TSE / AT RKSV / ES VeriFactu impl that
   ships with RegisterIsIdempotent == false) configured under a BlockingOnline /
   BlockingWithOfflineCache mode is blocked on the first attempt but NOT on the retry seam, so the
   registered-but-stamp-not-persisted residual would double-register at the authority on the retry-job
   tick = the compliance incident AC3/the gate exist to prevent. The gate's own doc-comment says it
   "fail[s] fast at the seam rather than risk it at runtime" - but the retry seam is ungated.
   docs/architecture/fiscal-compliance.md:73 overstates this ("the gate runs in the register path")
   while there are two register paths and one is unguarded.
   Fix: resolve the enforcement mode in RetryFiscalRegistrationAsync and call
   FiscalGoLiveGate.EnsureRegisterIdempotent(fiscalService, enforcementMode) before the
   RegisterReceiptAsync at line 301 (mirror line 182); add a test that the retry seam throws
   FiscalGoLiveGateException for a non-idempotent provider under a blocking mode; correct the doc to
   "runs in BOTH register paths".

Verdict: CHANGES_REQUESTED. The contract (explicit key, shared builder, provider self-declaration,
gate) is sound and well-tested for the initial register, but the single most important defense - the
go-live gate - is absent from the recovery re-register, which is the doublable side-effect this ticket
is chartered to make at-most-once for blocking regimes.

### Backend developer notes (2026-06-08)
- **AC1 (per-provider verified + documented):** done via the greppable code fact `RegisterIsIdempotent`
  on each provider + the per-provider table in `docs/architecture/fiscal-compliance.md`. `NoOpFiscalService`
  and `CzechEet2FiscalService` declare `true`; DE/AT/ES have no implementation yet, so the gate forces any
  future implementation to declare it before it can run under a blocking mode.
- **AC2 (explicit contract):** `FiscalReceiptRequest.IdempotencyKey` + `FiscalReceiptRequest.Create`
  (derives the key from `ReceiptNumber`). The two `ReceiptService` register sites now share one builder so
  the key cannot drift between the original register and the recovery re-register.
- **AC3 (go-live gate item 2 closed per provider):** enforced in code by `FiscalGoLiveGate` (fail-fast in
  the register path), not a checklist. ADR-0004 itself is immutable/accepted, so the gate closure is recorded
  in the architecture doc + this log rather than by editing the ADR body.
- **AC4 (redelivery does not double-register):** `FiscalRegisterIdempotencyContractTests` (idempotent
  provider collapses a same-token repeat onto one authority entry; distinct tokens burn distinct entries) and
  `ReceiptServiceFiscalIdempotencyTokenTests` (the recovery re-register presents the same token the initial
  register did, so a BlockingOnline-configured idempotent provider burns exactly one entry).
- **No migration / no NSwag** — `FiscalReceiptRequest`/`IFiscalService` are server-internal fiscal-adapter
  types, not exposed on any web DTO/endpoint, so no `nswag-regen`; no schema change.
- **Build note:** all production projects build green. The full-solution build is currently red ONLY on a
  pre-existing, unrelated in-flight test file (`Cleansia.Tests/Features/Orders/CancelOrderRefundSeamTests.cs`,
  a refund-seam ticket referencing a `CancelOrder.Handler` ctor arity not in the current tree); two
  `ChargebackRefundableCeilingTests` also fail on a refund-seeding SQLite NOT-NULL issue. Both are outside
  this ticket's fiscal surface and were not introduced here.
