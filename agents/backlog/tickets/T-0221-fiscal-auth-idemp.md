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

## Review
<!-- reviewer / security / optimizer write verdicts here -->
