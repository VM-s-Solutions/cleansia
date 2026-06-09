# Backlog Index

The manifest of every ticket. The **PM owns this file** and updates it on every state transition.
One row per ticket. Source of truth for "what's the team doing right now".

## Legend
- **Status:** draft · ready · in_progress · in_review · qa · done · blocked
- **Size:** S · M · L
- **Layers:** analyst, architect, db, backend, frontend, android, ios, docs

## Active

> ## ✅ WAVE 1 CLOSED — merged to master (2026-06-07 reconciliation)
> **Wave 0** = PR #72 (`9a774435`); **Wave 1 Batch 1A** (4 ADRs) + **Batch 1B** (T-0144…T-0159) = merged in
> `a4f14094` ("Wave-1 Batch 1B — integration resilience, outbox durability, soft-delete, loyalty/membership
> hardening"). **Local master == origin/master == a4f14094.** The PM reconciled the 14 Batch-1B tickets that
> still read `ready`/`draft` to **`done`** (status-log line on each); T-0166 hotfix already `done`. All four
> Wave-1 ADRs (0005/0006+0009/0007/0008) `accepted`.
>
> ## 🟡 WAVE 2 PLANNED — refund epic + fiscal go-live gates (proposed; awaiting owner sign-off)
> Full sequenced plan: **`status/sprint-4.md`**. **No new ADR gates Wave 2** — ADR-0006 (seam) + ADR-0009
> (policy) are `accepted` and freeze every refund decision; the Wave-2 refund tickets are pure BUILD.
>
> **Scope (12 tickets, refund foundation = the spine):** **T-0160** entity+enums → **T-0161** seam, **T-0163**
> loyalty revoke, **T-0231** package PriceWeight (all parallel-ish) → **T-0164** migrate cancel/dispute,
> **T-0167** admin refund cmd (depends on **T-0231** — AUD-02p→AUD-01c cross-edge) → **T-0168** admin UX,
> **T-0232** weight UX; plus the independent **T-0220/T-0221** fiscal go-live gates (DE/AT/ES), **T-0219**
> anon-catalog, **T-0222** pay-split rounding.
>
> **L-splits (this pass):** **T-0162** (AUD-01c) → **T-0167** (backend) + **T-0168** (frontend); **T-0165**
> (AUD-02p) → **T-0231** (db+backend) + **T-0232** (frontend). Parents T-0162/T-0165 are `[SPLIT]` tracking
> epics. The old `T-0162 depends_on T-0165` edge is now **T-0167 depends_on T-0231**.
>
> **Corrected edges:** T-0170 (admin order ops) + T-0173 (admin dispute mgmt) now `depends_on` the refund
> seam (T-0161) + seam migration (T-0164); both **deferred to Wave 3** (the admin-feature block).
>
> **Q-REFUND-03** (non-blocking) remains the one open item — even-split backfill ships in T-0231; owner sets
> per-bundle weights via T-0232.
>
> --- (Wave-1 history below, kept for traceability) ---
>
> **Batch 1A — the 4 ADRs — all `done`.** T-0141 → **ADR-0005** (integration), T-0140 → **ADR-0006** (refund
> seam) + superseding **ADR-0009** (refund policy), T-0152 → **ADR-0007** (soft-delete), T-0155 →
> **ADR-0008** (outbox table + drainer).
>
> **L-splits (Q-W1-2):** T-0142 → T-0152/T-0153/T-0154 (a→{b∥c}); T-0143 → T-0155/T-0156/T-0157/T-0158
> (a→b→c→d serial). Parents T-0142/T-0143 are `[SPLIT]` epics (tracking only). BLIND-2 = T-0159.

### Wave 1 — live roster (updated 2026-06-06)

**Batch 1A — the 4 ADRs — `done` ✅ (reviewer-reconciled 2026-06-06). The gate is cleared.**

| ID | Title | Size | Status | ADR produced | blocks | Layers |
|----|-------|------|--------|--------------|--------|--------|
| **T-0141** | ADR-INTEGRATION (IHttpClientFactory + error-class + async-email) | M | **done ✅** | ADR-0005 | T-0144→T-0145, T-0146, T-0147 | architect, backend |
| **T-0140** | ADR-REFUND (refund/dispute money path + chargeback) | M | **done ✅** | ADR-0006 + **ADR-0009** | T-0160…T-0165 (Wave-2) | architect, backend |
| **T-0152** | ADR: soft-delete policy (Deactivate vs Remove) | M | **done ✅** | ADR-0007 | T-0153, T-0154, T-0191 | architect |
| **T-0155** | ADR: outbox table + in-Functions drainer (ADR-0002 D1.3) | M | **done ✅** | ADR-0008 | T-0156→T-0157→T-0158 | architect |

**Batch 1B — contract/plumbing code. ALL `done` ✅ (merged in `a4f14094`; PM-reconciled 2026-06-07).**

| ID | Title | Size | Status | depends_on | Layers | sec | manual_step |
|----|-------|------|--------|-----------|--------|-----|-------------|
| T-0150 | Centralize CZE/Mapbox-bounds/2000-char constants | S | **done ✅** | — | backend, frontend, android | no | — |
| T-0149 | Refresh-token rotation re-checks profile (per host) | S | **done ✅** | T-0100✓ | backend | **yes** | — |
| **T-0159** | BLIND-2: Mapbox token in request URL → correct auth + scrub logs + rotate | S | **done ✅** | — | frontend, config | **yes** | rotate-mapbox-token ⚠️ **still outstanding (owner)** |
| T-0144 | Stripe + SendGrid via IHttpClientFactory (ADR-0005) | M | **done ✅** | T-0141✓ | backend | no | — |
| T-0146 | Registration/reset email off critical path (async, ADR-0005 D3) | M | **done ✅** | T-0141✓, T-0118✓ | backend, functions | **yes** | — |
| T-0147 | Membership commands: provider try/catch + S7 (ADR-0005 D4) | M | **done ✅** | T-0141✓ | backend | **yes** | — |
| T-0148 | Tier-threshold config read + persist grant/revoke Reason | M | **done ✅** | T-0112✓ | backend | no | — |
| T-0153 | SavedAddress soft-delete + IsActive filters + null-FK + migration (ADR-0007) | M | **done ✅** | T-0152✓ | backend, db | no | ef-migration |
| T-0154 | Device soft-delete verdict (UnregisterDevice, ADR-0007) | S | **done ✅** | T-0152✓ | backend | no | — |
| T-0156 | Outbox table + EF config + migration flag (ADR-0008) | S | **done ✅** | T-0155✓ | db | no | ef-migration |
| T-0151 | Migrate remaining queue consumers onto Functions.Core | M | **done ✅** | T-0121✓ | functions | no | — |
| T-0145 | Error classification across integration layer | M | **done ✅** | T-0141✓, T-0144✓ | backend | no | — |
| T-0157 | Durable IPendingDispatch backing + drainer + host (ADR-0008) | M | **done ✅** | T-0156✓, T-0118✓ | backend, functions | no | — |
| T-0158 | Bucket-B sweeps migrate onto per-iteration outbox row | M | **done ✅** | T-0157✓, T-0148✓ | backend | no | — |

> **Batch 1B = 14 `done`** (merged `a4f14094`). Reconciled 2026-06-07 from stale `ready`/`draft`. The only
> residual owner action is **T-0159's `rotate-mapbox-token`** — the code fix shipped (token off the URL) but
> the exposed token still needs rotating in the Mapbox account (a live exposure until done). Surfaced in
> `status/sprint-4.md` §3.

**Wave 2 — refund BUILD from ADR-0006/0009 + fiscal go-live gates + fast-follows (plan: `status/sprint-4.md`; `draft` — awaiting owner sign-off):**

| ID | Title | Size | Status | depends_on | blocks | Layers | sec | manual_step |
|----|-------|------|--------|-----------|--------|--------|-----|-------------|
| **T-0160** | AUD-01a: Refund entity + EF + PaymentStatus.PartiallyRefunded + RefundReason enum | M | draft | — | T-0161, T-0163, T-0164, T-0167 | backend, db | no | ef-migration |
| **T-0161** | AUD-01b: IRefundService impl (seam, ceiling, RefundKey) + IStripeClient key param | M | draft | T-0160 | T-0164, T-0167, T-0170, T-0173 | backend, clients | **yes** | nswag-regen* |
| **T-0231** | AUD-02p1 (split of T-0165): PackageService.PriceWeight + even-weight backfill + bundled-gross | M | draft | — | **T-0167**, T-0232 | db, backend | no | ef-migration |
| **T-0163** | AUD-01d: ILoyaltyService.RevokeForPartialRefundAsync (proportional, keyed) | M | draft | T-0160 | — | backend, db | no | ef-migration |
| **T-0164** | AUD-01e: Migrate CancelOrder + ResolveDispute onto the seam | M | draft | T-0160, T-0161 | T-0170, T-0173 | backend | **yes** | — |
| **T-0167** | AUD-01c1 (split of T-0162): admin partial-refund cmd + allocator + RefundPolicy + PartiallyRefunded | M | draft | T-0160, T-0161, **T-0231** | T-0168, T-0170, T-0173 | backend | **yes** | nswag-regen |
| **T-0168** | AUD-01c2 (split of T-0162): admin partial-refund UX | M | draft | T-0167 | — | frontend | no | nswag-regen (consumes) |
| **T-0232** | AUD-02p2 (split of T-0165): admin package-form weight UX | S | draft | T-0231 | — | frontend | no | nswag-regen (consumes) |
| **T-0220** | FISCAL-SEQ: gapless fiscal sequence allocator (FiscalCounter) — **DE/AT/ES go-live gate** | M | draft | T-0119✓ | — | backend, db | **yes** | ef-migration |
| **T-0221** | FISCAL-AUTH-IDEMP: per-provider RegisterReceiptAsync idempotency — **DE/AT/ES go-live gate** | M | draft | T-0119✓ | — | backend, clients | **yes** | — |
| **T-0219** | Anon-catalog entities → platform config (Service/Category/Package/Extra/ServiceCity) | M | draft | T-0100✓, T-0113✓ | — | backend, db | **yes** | ef-migration |
| **T-0222** | SplitPayForMultipleEmployees — currency-minor-unit split + remainder reconciliation | S | draft | — | — | backend | no | — |

> \* T-0161 `nswag-regen` only if a refund **response DTO** surfaces on a client; the admin refund command DTO
> regen is on **T-0167**.
>
> **Split epics (tracking only):** **T-0162** (AUD-01c, `L`) → **T-0167** + **T-0168**; **T-0165** (AUD-02p,
> `L`) → **T-0231** + **T-0232**. The old `T-0162 depends_on T-0165` edge is now **T-0167 depends_on T-0231**.
>
> **Load-bearing cross-edge (DAG over id order): AUD-02p1 (T-0231) → AUD-01c1 (T-0167)** — a bundled service
> has no gross until `PriceWeight` exists; T-0231 must be `done` before T-0167 goes `ready`.
> **Q-REFUND-03** (non-blocking) gates only T-0231's per-bundle *business* weighting (even-split default
> ships; owner sets weights via T-0232). The admin-feature consumers **T-0170/T-0173** now depend on the
> refund seam + seam migration and are **Wave 3**, not Wave 2.

**Split epics (tracking only — do not run as one ticket):**

| ID | Title | Status | Split into |
|----|-------|--------|-----------|
| T-0142 | [SPLIT] ADR + soft-delete sweep | draft (epic) | T-0152 → {T-0153 ∥ T-0154} |
| T-0143 | [SPLIT] Full transactional outbox | draft (epic) | T-0155 → T-0156 → T-0157 → T-0158 |

> ## 📋 FULL TICKETED BACKLOG — 87 tickets, all waves (2026-06-01)
> Every wave is now ticketed as a file in `tickets/` (collision-checked twice; 18 serializing
> `depends_on` edges applied). Dependency graph + shared-file serialization clusters: `TICKET-MAP.md`.
> All 3 gating ADRs accepted (0001 authz, 0002 outbox, 0003 ratelimit). All `draft` → PM promotes to
> `ready` wave by wave. Built **test-first (TDD)**; reviewer + security run in parallel per ticket.
>
> | Wave | Ids | Count | What |
> |---|---|---|---|
> | **0 — PROD gate** | T-0100…T-0128 | 29 | security/correctness blockers + the Wave-0 test slice |
> | **1 — ADRs + contracts** | T-0140…T-0151 | 12 | ADR-REFUND, ADR-INTEGRATION, soft-delete, full outbox, integration plumbing |
> | **2 — features (story-backed)** | T-0170…T-0195 | 26 | admin order ops, payroll, disputes, membership/referral/GDPR/device, catalog activate/deactivate, rate-limit fast-follows |
> | **3 — consistency & quality** | T-0196…T-0206 | 11 | the 187 canonicalization sweep, god-unit decomposition, de-triplication, dead/unsafe code, S6 logging, perf |
> | **4 — tests + a11y** | T-0210…T-0218 | 9 | webhook/refund/invoice/Functions/authz/fiscal integration tests, error-contract parity, accessibility |
>
> **Execution order:** strictly wave-by-wave (Wave N fully `done` before N+1 opens). Within a wave the
> PM fans out by `depends_on`; the serialization clusters prevent same-file races. **Wave 0 is the PROD
> gate — nothing ships to prod until it's green.** Per-ticket detail is in each `tickets/T-NNNN-*.md`.

> ## 🔴 WAVE 0 — PROD-BLOCKING (from the COMPLETE audit, 2026-06-01)
> The full audit overturned the earlier "no security defect" verdict: **8 of 9 criticals are security
> defects.** **Nothing ships to PROD until Wave 0 is green.** Full plan + verdicts:
> `audits/AUDIT-2026-06-01-execution-plan.md`. Findings: `audits/AUDIT-2026-06-01-findings.md`.
> Stories (83): `stories/AUDIT-2026-06-01-user-stories.md`. **Everything is built test-first (TDD).**
> **FUP-1 (the suspected webhook-signature gap) is REFUTED** — verification proved signature
> verification is present; residual SEC-W2/W3 tracked below.

| ID | Title | Wave | Sev | Size | Status | Layers | ADR |
|----|-------|------|-----|------|--------|--------|-----|
| BSP-1 (+BSP-6) → **T-0100** | One PolicyBuilder ticket: fail-closed fallback + complete Map + startup assertion (BSP-6 merged in) | 0 | crit | M | **done ✅** | backend, config | ADR-AUTHZ (pre-decided) |
| IDA-SEC-01 → **T-0105** | Google sign-in trusts client email/GoogleId → verify ID-token claims server-side | 0 | crit | M | **done ✅** (⚠️ owner: IMP-1 ClientId for live OAuth) | backend | ADR-AUTHZ (S1/D5) |
| IDA-SEC-03 → **T-0106** | Reset/confirm codes 6-digit non-crypto, looked up by code → crypto tokens + scoped lookup | 0 | crit | M | **done ✅** (migration regenerated 2026-06-03: 64-char token cols in Initial) | backend, db | — |
| SEC-DSP-01 → **T-0102** | `IsStaffMessage` client-supplied → derive staff flag from caller role | 0 | crit | S | **done ✅** (⚠️ owner: nswag-regen) | backend, nswag | — |
| SEC-DSP-02 → **T-0103** | CreateDispute doesn't check order ownership (S1/S3) | 0 | crit | S | **done ✅** | backend | ADR-AUTHZ |
| SEC-EMP-01 → **T-0104** | Partner analytics IDOR (EmployeeId from query string) | 0 | crit | S | **done ✅** (⚠️ owner: nswag-regen) | backend, nswag | ADR-AUTHZ |
| IDA-SEC-04 → **T-0101** | Any Employee reads any user's full PII by id | 0 | maj | S | **done ✅** | backend | — |
| EMP-GAP-01 → **T-0109** | Rejected cleaners can still take/start/complete orders → gate on ContractStatus==Approved | 0 | crit | M | **done ✅** | backend | ADR-AUTHZ |
| LG-SEC-01 → **T-0110** | Single-use promo over-redeemed via race → atomic conditional-UPDATE + tenant-scoped unique index | 0 | crit | M | **done ✅** (migration regenerated 2026-06-03: SlotOrdinal + unique index in `20260603090920_Initial`) | backend, db | — |
| LG-SEC-02 → **T-0111** | Mobile subscribe: Stripe subscription with no idempotency key → double-charge | 0 | crit | M | **done ✅** (⚠️ owner: nswag-regen; 2 review rounds) | backend, mobile, nswag | ADR-OUTBOX |
| LG-SEC-06 → **T-0112** | Admin loyalty grant/revoke non-idempotent → requestId + tenant-scoped filtered unique index + rate-limit | 0 | maj | M | **done ✅** (migration regenerated 2026-06-03: IdempotencyKey in Initial; ⚠️ owner: nswag-regen for admin Command) | backend, db, nswag | ADR-OUTBOX, ADR-RATELIMIT |
| IA-1 → **T-0108** | CreateAdminUser double-hashes password → new admins can't log in | 0 | crit | S | **done ✅** | backend | — |
| SEC-W2 → **T-0114** | Webhook auto-provision can create a 2nd active membership → active-check + filtered unique index | 0 | maj | M | **done ✅** (migration regenerated 2026-06-03: active filtered unique index in Initial) | backend, db | ADR-OUTBOX |
| SEC-W3 → **T-0116** | Webhook endpoints not rate-limited (S5) → per-IP "webhook" policy (independent) on 3 hosts | 0 | maj | S | **done ✅** | web, backend | ADR-RATELIMIT |
| BSP-4 / IDA-SEC-02 → **T-0115** | Global rate limiter (no partition) → partitioned per-IP/per-sub + forwarded-headers + fail-closed guard + host harness | 0 | crit | M | **done ✅** (⚠️ owner deploy gate: ForwardedHeaders config) | config, backend | ADR-RATELIMIT |
| F11 → **T-0117** | UnitOfWork pipeline commits even on validation failure → Validation-outer reorder + IsSuccess-gated commit | 0 | crit-root | S | **done ✅** | backend | ADR-OUTBOX D4 |
| FUNC-CORE → **T-0121** | Extract Cleansia.Functions.Core so queue consumers are unit-testable (precondition for F2/F4/F3) | 0 | — | S | **done ✅** (16/16 triggers discovered; pure move) | functions | ADR-OUTBOX D5.1 |
| F2 / SEC-W1 → **T-0118** | Enqueue-before-commit → tactical post-commit dispatch (PostCommitDispatchBehavior + idempotent receipt consumer) | 0 | maj | L | **done ✅** | appservices, functions, queue | ADR-OUTBOX D1-D3 |
| F3 → **T-0120** | No poison/dead-letter consumer → 5 per-queue poison consumers + DeadLetter store + classification | 0 | maj | M | **done ✅** (⚠️ owner: DeadLetter table ef-migration folds into Initial regen) | functions, db | ADR-OUTBOX D3 |
| F4 → **T-0119** | Receipt idempotent: claim-before-register, at-most-once fiscal seq + authority registration (S7) | 0 | maj | M | **done ✅** (go-live gates → T-0220/T-0221/T-0122) | functions, backend | ADR-0004 |
| FISCAL-RECON → **T-0122** | Reconciliation sweep: re-enqueue committed-but-unrealized fiscal work (no-receipt OR FiscalCode==null per C-B) | 0 | maj | S | **done ✅** (2 rounds; ADR-0004 outer net) | backend, functions | ADR-OUTBOX D3.4 + ADR-0004 C-B |
| IDA-SEC-08 → **T-0107** | Admin GDPR/deactivate: no self/last-admin protection | 0 | maj | S | **done ✅** | backend | ADR-AUTHZ |
| BLIND-1 → **T-0146** | Email synchronous on signup/reset critical path → async/queue | **1** | crit | M | **ready** (Wave 1 1B — ADR-0005/T-0141 done ✓ + T-0118 ✓; security gate) | backend, functions | ADR-0005 (T-0141) |
| BLIND-2 → **T-0159** | Mapbox access token in request URL query → use correct Mapbox auth + scrub logs + rotate token | **1** | crit | S | **ready** (Wave 1 1B — independent; **security_touching**; ⚠️ owner: rotate-mapbox-token) | frontend, config | — |
| PROD-CONFIG → **T-0123** | Hardening: CSRF-in-prod (BSP-3) + Swagger fail-closed + boot guard (BSP-5) + anon LookupBatch (BSP-9) | 0 | maj | S | **done ✅** (⚠️ owner: provision Csrf:Secret before prod deploy) | config | ADR-RATELIMIT |
| PERF-IDA-01 (+PERF-IDA-05) → **T-0124** | No DB index on User.Email + lookup columns → unique Email index + filtered lookup indexes | 0 | crit | S | **done ✅** (migration folds into Initial regen) | db | — |
| **PRE-0 ADR sprint** | ADR-AUTHZ + ADR-OUTBOX(contract) + ADR-RATELIMIT decided & accepted BEFORE the Wave-0 items that encode them | 0 | — | — | draft | architect | are the ADRs |
| TC-PAY → **T-0125** | Pay-calc tests (must-cover #1) — 70 tests across the 4 pure surfaces; pay math was untested | 0 | crit | S | **done ✅** (split-rounding follow-up → T-0222) | backend | — |
| TC-AUTHZ-0 → **T-0126** | Cross-tenant/cross-user write-path rejection tests + WebApplicationFactory host harness | 0 | crit | M | **done ✅** (Cleansia.HostTests; 32 e2e authz tests green) | backend | with BSP-1 |
| TC-IDEMP-0 → **T-0127** | "Safe to run twice" idempotency tests (webhooks + 3 LG money fixes) | 0 | crit | M | **done ✅** (cases shipped inline w/ fixes; audit confirmed full coverage) | backend | with the fix |
| TC-AUTH-TAKEOVER → **T-0128** | Token-claim binding + reset-code lookup tests | 0 | crit | M | **done ✅** (covered + GoogleTokenVerifier gap filled) | backend | with IDA-SEC-01/03 |
| LG-SEC-05 → **T-0113** | Anonymous-but-tenant-scoped MembershipPlan read → platform config (Option A) | 0 | maj | M | **done ✅** (migration regenerated 2026-06-03: MembershipPlans Code-unique, no tenant-scoping) | backend, db | ADR-AUTHZ A1 |
| LG-SEC-05-sibs → **T-0219** | Anon catalog entities (Service/Category/Package/Extra/ServiceCity) → platform config | 2 | maj | M | draft (created from doctrine) | backend, db | ADR-AUTHZ A1 |
| FISCAL-SEQ → **T-0220** | Gapless-monotonic-atomic fiscal sequence allocator (FiscalCounter) — replace COUNT(*)+1 | 2 | maj | M | draft (ADR-0004 split; **DE/AT/ES go-live gate**) | backend, db | ADR-0004 |
| FISCAL-AUTH-IDEMP → **T-0221** | Per-provider RegisterReceiptAsync idempotency on ReceiptNumber (IFiscalService key) | 2 | maj | M | draft (ADR-0004 split; **DE/AT/ES go-live gate**) | backend, clients | ADR-0004 |

> ⚠️ **Plan corrected 2026-06-01** after a collision check (`audits/AUDIT-2026-06-01-plan-corrections.md`):
> 3 blocking defects fixed — ADRs frozen pre-Wave-0, outbox split tactical/strategic, BSP-1+BSP-6
> merged + PolicyBuilder edits serialized, and a real Wave-0 test slice added (TDD is now structural).

> **Waves 1–4** (foundational ADRs, story-backed features, consistency cleanup, tests + a11y) are in
> `audits/AUDIT-2026-06-01-execution-plan.md` — not duplicated here. The AUD-01…25 and T-0001…16
> backlogs below are folded into the wave plan (referenced in place). The prior-audit sprint-3 AUD
> tickets and the FUP passes are **superseded by this complete audit** — keep them for traceability but
> work the wave plan.

> **Prior (partial) codebase audit backlog** (sprint 3, superseded by the complete audit above; kept
> for traceability). AUD-01/02/04 carried into Wave 2. FUP-1 RESOLVED-REFUTED.

| ID | Title | Sprint | Size | Status | Owner | Depends on | Layers |
|----|-------|--------|------|--------|-------|-----------|--------|
| **FUP-1** | 🔴 Verify Stripe **subscription** webhook signature (suspected missing) + idempotency/replay | 2 | M | draft | — | — | backend, security |
| FUP-2 | Re-audit the 5 under-covered domains (loyalty-growth, disputes-addresses, identity-auth, catalog-config, employees) | 2 | M | draft | — | — | analyst, reviewer, security, optimizer |
| FUP-3 | Azure Functions trigger-graph pass — re-validate "dead lifecycle" verdicts (AUD-02/04); idempotency/poison/dead-letter | 2 | M | draft | — | — | backend, security |
| FUP-4 | Contract-parity checker: i18n key sets ×5 locales, BusinessErrorMessage↔errors.*, NSwag drift | 2 | M | draft | — | — | backend, frontend |
| FUP-5 | Test-coverage gap pass → prioritized must-cover backlog (orders/payments/payroll/fiscal/Functions) | 2 | M | draft | — | — | qa, backend |
| FUP-6 | AppHost/Aspire + secrets/CORS/host-exposure pass | 2 | S | draft | — | — | architect, security |
| FUP-7 | Migration/seed integrity pass (EF migrations vs configs; sql-scripts seeds) | 2 | S | draft | — | — | db |
| AUD-01 | Admin order operations + generalized cancellation (cancel/reassign/refund/status-override) | 3 | L | draft | — | — | architect, backend, frontend |
| AUD-02 | Wire up dead payroll adjustment & settlement lifecycle (bonus/deduction, Paid, Dispute/Reject, Reopen) | 3 | L | draft | — | FUP-3 | architect, backend, frontend, android |
| AUD-03 | Build admin Extras management (CRUD + translations + pricing) | 3 | L | draft | — | — | backend, frontend |
| AUD-04 | Reconcile partner payroll surface (my-period-pay screen, prune admin endpoints off partner host, failed-PDF invoices) | 3 | L | draft | — | FUP-3, FUP-6 | architect, backend, frontend, android |
| AUD-05 | Add order-cancellation flow to customer **web** (parity with mobile) | 3 | M | draft | — | — | frontend |
| AUD-06 | Decompose CreateOrder.Handler god-handler (484 lines, 15 deps) | 3 | L | draft | — | — | backend |
| AUD-07 | Split order-wizard god-facade (1048 lines) + migrate to C3 pipe | 3 | L | draft | — | T-0010 | frontend |
| AUD-08 | Move ownership/profile checks to handler in Take/Complete/Start order (B4/S3) | 3 | M | draft | — | — | backend |
| AUD-09 | Add RecurringBookingTemplate.MapToDto + Address.ToSingleLine; dedupe recurring projection/validators | 3 | M | draft | — | — | backend |
| AUD-10 | Move cleaner weekly-order-limit magic numbers into BookingPolicy | 3 | S | draft | — | — | backend |
| AUD-11 | Convert partner OrdersListUiState to sealed UiState + ActionState (E1/E2) | 3 | M | draft | — | — | android |
| AUD-12 | Fix off-by-one OrderStatus class/icon maps in partner web order-detail helpers | 3 | S | draft | — | — | frontend |
| AUD-13 | Standardize order/note/issue parity & remove dead endpoints across web/mobile | 3 | M | draft | — | — | backend, frontend |
| AUD-14 | Add OnTheWay case to admin order status badge/icon helpers | 3 | S | draft | — | — | frontend |
| AUD-15 | Type order-status email param as OrderStatus enum + CancelledBy enum (folds into AUD-01) | 3 | M | draft | — | AUD-01 | backend |
| AUD-16 | Type recurring-booking command enums instead of raw ints | 3 | M | draft | — | — | backend, frontend |
| AUD-17 | Remove geocoding **write** from GetPagedOrders query (restore CQRS read-only); extract pay/PII mapper | 3 | M | draft | — | — | backend |
| AUD-18 | Fix partner OrdersFacade cleanup/error handling + remove setTimeout(100) sequencing | 3 | M | draft | — | — | frontend |
| AUD-19 | Move customer recurring/wizard facade calls to the C3 pipe | 3 | M | draft | — | AUD-07 | frontend |
| AUD-20 | Refactor HandlePaymentNotification webhook (297 lines) + add tests | 3 | M | draft | — | — | backend |
| AUD-21 | Align GetFiscalFailures to IQueryHandler + decide paging (remove hidden 200 cap) | 3 | M | draft | — | — | backend |
| AUD-22 | Add Response records to fiscal commands (B1) | 3 | S | draft | — | — | backend |
| AUD-23 | Fix mobile collectAsState → lifecycle-aware; make CZ/CZK config-driven | 3 | M | draft | — | — | android |
| AUD-24 | Correct stale "no recurring UI" comment in MaterializeRecurringBookings | 3 | S | draft | — | — | backend |
| AUD-25 | Burn down the 187 machine-detected consistency violations (T-0001…T-0016 epic) | 3 | — | draft | — | — | backend, frontend, android |

---

> **Consistency canonicalization backlog** (from `audits/consistency-violations.md`). These are
> `draft` until the owner approves the setup and the PM promotes them to `ready`. Each maps to a rule
> in `knowledge/consistency.md`. Two (T-0009, T-0016) need an Architect ADR first because they are
> cross-cutting (soft-delete; mobile repo contract) — do not start those without the ADR.

| ID | Title | Sprint | Size | Status | Owner | Depends on | Layers |
|----|-------|--------|------|--------|-------|-----------|--------|
| T-0001 | Canonicalize GetPagedPromoCodes + GetPagedReferrals to the paged-query pattern | 1 | M | draft | — | — | backend |
| T-0002 | Make GetPagedPayConfigs.Filter init-only | 1 | S | draft | — | — | backend |
| T-0003 | Align GetPagedServices to canonical read-path order | 1 | S | draft | — | — | backend |
| T-0004 | Give CreateDispute/UpdateDisputeStatus/DeleteSavedAddress a Response record | 1 | S | draft | — | — | backend |
| T-0005 | Move ownership checks from validators to handlers (4 features) | 1 | M | draft | — | — | backend, security |
| T-0006 | Refactor validators to AbstractValidator + composed shared rules | 1 | M | draft | — | — | backend |
| T-0007 | Fix Error field name in CreateMembershipSubscription | 1 | S | draft | — | — | backend |
| T-0008 | Add idempotency + provider error handling to membership/order create | 1 | M | draft | — | — | backend, security |
| T-0009 | ADR + sweep: soft-delete for business entities | 2 | L | draft | — | — | architect, backend, db |
| T-0010 | Unify customer-feature facades on UnsubscribeControlDirective | 1 | M | draft | — | — | frontend |
| T-0011 | Normalize list facades (signals, finalize, no stray NgRx) | 1 | M | draft | — | — | frontend |
| T-0012 | Unify fiscal-failures table def + package-form builder | 1 | S | draft | — | — | frontend |
| T-0013 | Convert partner-app flag-bag UiStates to sealed states | 1 | M | draft | — | — | android |
| T-0014 | Standardize one-shot actions on ActionState | 1 | M | draft | — | — | android |
| T-0015 | Fix RecurringBookingsScreen state collection (lifecycle) | 1 | S | draft | — | — | android |
| T-0016 | ADR + migrate customer-app repos to ApiResult<T> and unify mobile structure | 2 | L | draft | — | — | architect, android, ios |

## Done

| ID | Title | Sprint | Merged |
|----|-------|--------|--------|
| _(none yet)_ | | | |

---

> First real job (pending owner approval of this setup): **a full codebase audit** across all
> layers — backend, db, frontend, android — to surface functional gaps, half-built features,
> spaghetti hotspots, hardcoded strings, security holes, and performance issues. The audit fans out
> one analyst + one reviewer (and `security`/`optimizer` where relevant) per subsystem in parallel,
> writes findings to `agents/backlog/audits/`, and the PM converts each finding into a ranked ticket
> here. See `agents/WAY-OF-WORKING.md`.
