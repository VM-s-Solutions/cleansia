# Security findings — Fiscal / Receipts

## T-0220 (FISCAL-SEQ — gapless allocator) — 2026-06-08 — CHANGES_REQUESTED

S-status: S1 N/A · S2 N/A · S3 N/A · S4 PASS · S5 N/A (queue) · S6 PASS · S7/S7a/S7b PASS ·
S8 PARTIAL (entity tenant-scoped, but ReceiptNumber uniqueness is scope/tenant-blind) · S9 PASS
(owner ef-migration flagged) · S10 N/A.

### F-220-1 [BLOCKER] Scope-blind ReceiptNumber + global unique index silently drops a real sale
Counter is partitioned on `IssuerScope`, but the persisted/visible number `RCP-{year}-{seq:D4}`
(`ReceiptService.cs:64`) carries no scope/tenant. Two scopes each start at 1 → both mint
`RCP-2026-0001`; global unique `IX_OrderReceipts_ReceiptNumber` throws 23505; the handler's
23505-as-already-claimed catch (`GenerateReceiptHandler.cs:115-128`) ACKs with no receipt, and because
the allocation rolled back with the tx, every redelivery re-mints the same colliding number → permanent
silent drop = a silently-unregistered sale. Breaks AC5 + S8-isolation intent of AC3.
Proposed ticket: "Scope/tenant-qualify ReceiptNumber + narrow 23505 catch to the OrderId index".

### F-220-2 [BLOCKER] Live CZ numbering silently flips annual-reset -> never-reset
CZ `None` -> empty provider key -> `FiscalSequenceScope.Resolve("")` -> DEFAULT scope, year=0
(non-reset), while the displayed number still embeds the calendar year. Old `COUNT(*)+1` reset annually.
Silent customer-visible numbering regression; comment at `ReceiptService.cs:151-152` is factually wrong.
Proposed ticket: "Preserve annual-reset for the no-fiscal CZ receipt sequence (or make it an explicit
documented change)".

### F-220-3 [BLOCKER] Issuer scope can silently degrade to "noop"
`AsyncBackground` with an unregistered provider resolves to `NoOpFiscalService.ProviderKey == "noop"`
(`FiscalServiceResolver.cs:33`), which then keys the LEGALLY gapless counter. A regulatory issuer scope
must never fall back to the no-op key — fail closed or resolve scope from the regime mapping.
Proposed ticket: "Fail closed when fiscal mode != None but no real provider resolves".

### Verified-safe (do not re-flag)
- Allocator is atomic + same-transaction + gapless-per-scope + void-safe on real Postgres (AC1/AC2/AC4).
- Raw SQL is fully parameterised (no injection); no PII logged; migration correctly owner-only (AC6).

## T-0221 (FISCAL-AUTH-IDEMP - per-provider register idempotency) - 2026-06-08 - CHANGES_REQUESTED

S-status: S1 N/A . S2 N/A . S3 N/A . S4 PASS . S5 N/A (queue/timer job) . S6 PASS .
S7/S7a PARTIAL (initial register gated; recovery re-register UNGATED) . S8 PASS (no schema change;
fiscal-adapter types only) . S9 PASS (no migration/no NSwag - server-internal types) . S10 N/A.

### F-221-1 [BLOCKER] Go-live gate missing from the recovery re-register path
FiscalGoLiveGate.EnsureRegisterIdempotent runs only in the initial register
(ReceiptService.HandleFiscalAsync, ReceiptService.cs:182). The recovery re-register -
ReceiptService.RetryFiscalRegistrationAsync (ReceiptService.cs:275-344), driven by
FiscalRetryService.ProcessDueRetriesAsync - calls RegisterReceiptAsync at ReceiptService.cs:301 with
NO gate and without resolving enforcementMode. That recovery is the EXACT residual the ticket targets
("recovery re-calls RegisterReceiptAsync with the same ReceiptNumber"). A future non-idempotent DE/AT/ES
provider under a blocking mode is blocked on attempt 1 but double-registers at the authority on the
retry-job tick - the compliance incident the gate exists to prevent. Doc fiscal-compliance.md:73
overstates "the gate runs in the register path" (there are two; one is unguarded).
Proposed ticket: "Run FiscalGoLiveGate in RetryFiscalRegistrationAsync (gate the recovery re-register seam)".

### Verified-safe (do not re-flag)
- IdempotencyKey contract: explicit field, Create-factory-derived from ReceiptNumber, single
  BuildFiscalRequest helper so initial + recovery present the same token (AC2).
- Provider self-declaration + gate reject/admit/ignore matrix tested (AC1/AC4).
- T-0220 allocator/scope blockers (F-220-1/2/3) remain open on the shared ReceiptService.cs and are
  tracked under T-0220 - not re-counted against T-0221.
