# Backlog Index

The manifest of every ticket. The **PM owns this file** and updates it on every state transition.
One row per ticket. Source of truth for "what's the team doing right now".

## Legend
- **Status:** draft · ready · in_progress · in_review · qa · done · blocked
- **Size:** S · M · L
- **Layers:** analyst, architect, db, backend, frontend, android, ios, docs

## Active

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
| BLIND-1 → **T-0146** | Email synchronous on signup/reset critical path → async/queue | **1** | crit | M | draft (Wave 1 — blocked on T-0141 ADR-INTEGRATION + T-0118✓) | backend, functions | ADR-INTEGRATION (T-0141) |
| BLIND-2 | Mapbox token in URL query → leaks into traces/logs | **1** | crit | S | draft (Wave 1 — no ticket yet; PM to create) | backend | — |
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
