# Ticket Map — full backlog, all waves (collision-safe)

The canonical id scheme, dependency graph, and **shared-file serialization map** that every ticket
references. Built from `audits/AUDIT-2026-06-01-execution-plan.md` + `…-plan-corrections.md` + the
3 accepted ADRs. The PM owns this; the ticket generator and the cross-ticket collision check both
read it. **Goal: no two concurrently-runnable tickets edit the same file, and every dependency points
at a real earlier ticket.**

## Id scheme
- Tickets get sequential `T-NNNN` ids on intake (the audit/security ids like BSP-1, F2, AUD-01 become
  the ticket *title prefix* + a `source:` field, so the traceability survives).
- Wave is a frontmatter field, not part of the id.
- Test tickets pair with their fix via `pairs_with:` (TDD: same merge).

## Shared-file serialization map (the collision-avoidance core)
Tickets that touch the **same file** must NOT run concurrently. Each cluster has a fixed internal order;
the PM never spawns two from the same cluster in parallel.

> **Updated 2026-06-01 after the cross-ticket collision check** — added 4 missing clusters (admin
> shell, BusinessErrorMessage+locales, User entity config, Wave-2 Policy editors) and dropped the
> T-0103/T-0104 over-serialization. The serializing `depends_on` edges are now **encoded in the ticket
> frontmatter**, not just documented here. T-id map: BSP-1=T-0100, IDA-SEC-04=T-0101, SEC-DSP-01=T-0102,
> SEC-DSP-02=T-0103, SEC-EMP-01=T-0104, IDA-SEC-08=T-0107, EMP-GAP-01=T-0109, BSP-4=T-0115,
> SEC-W3=T-0116, PROD-CONFIG=T-0123, F11=T-0117, F2=T-0118, F4=T-0119, F3=T-0120, FUNC-CORE=T-0121.

| Shared file / surface | Cluster (strict order, T-ids) | Rule |
|---|---|---|
| `PolicyBuilder.cs` / `Policy.cs` (map + snapshot) | T-0100 → {T-0101, T-0102, T-0107, T-0109} after T-0100; serialize only those adding a `Policy.*` const / map row | **T-0103 + T-0104 are parallel-safe** (disjoint handler files, no map row) |
| `BusinessErrorMessage.cs` + 15 i18n locale JSONs | T-0107 → T-0109 (W0); future error-code tickets join the tail | concurrent appends corrupt the C# class + JSON |
| `CleansiaStartupBase.cs` (pipeline + limiter) | T-0115 → T-0116 → T-0123 | serialize |
| `UnitOfWorkPipelineBehavior.cs` + queue sites + Function bodies | T-0117 → T-0118 → T-0119 → T-0120; **T-0121 (FUNC-CORE) first of the Function-body edits** (T-0118/119/120/122 depend on it) | T-0121 relocates all 16 Function files |
| order-action handlers `Take/Start/CompleteOrder.cs` | T-0109 (ContractStatus gate) → T-0118 (enqueue migration) | both edit all three |
| `AddDisputeMessage.Handler` + dispute backend | T-0102 → T-0118 (W0); T-0172 → T-0173 (W2) | serialize |
| host `ServiceExtensions.cs` (×5 auth registration) | T-0100 owns the shared `AddCleansiaAuthorization` extraction | one ticket, all 5 hosts (ADR-0001 D4) |
| Wave-2 `PolicyBuilder` editors (consts + snapshot) | T-0170 → T-0171 → T-0173 → T-0175 → T-0176 (linear) | all regenerate the one frozen-map snapshot |
| admin shell `app.component.ts` (`sidebarMenuItems`) + `app.routes.ts` | T-0173 → T-0175 → T-0176 → T-0186 | one sidebar array + routes |
| `UserEntityConfiguration.cs` / `User.cs` (Users ef-migration) | T-0106 → T-0124 (W0); T-0189 → T-0193 (W2) | concurrent Users migrations conflict |
| `EmailService.cs` / `StripeClient.cs` | T-0144 → T-0145 (W1) | serialize |
| `LoyaltyService.cs` | T-0112 (W0) → T-0148 → T-0143 (W1) | cross-wave + W1 serialize (T-0143 → T-0148) |
| `CreateOrder.cs` | T-0118 (W0) → T-0199/AUD-06 (W3) → T-0212/TC-4 (W4 characterization, before AUD-06) | AUD-06 rebases on post-F2 |
| controllers annotated by BSP-4d | T-0194 runs **last**, after T-0171/T-0173/T-0179/T-0188 | attribute on final endpoint shape |
| `disputes.facade.ts` (customer) | T-0196 **excludes** it → T-0202 owns the full rewrite + the NgRx/C8 decision | reconciled 2026-06-01; T-0202 → T-0196 |
| `UpdateDisputeStatus.cs` (Wave-3 tail) | T-0196 (B1 Response wrap) → T-0204 (PERF-D2 lightweight load) | T-0204 → T-0196 |
| `UnregisterDevice.cs` / SavedAddress handlers | T-0142 (soft-delete) → T-0203 (dead-code); T-0150 → T-0201 | T-0203 → T-0142, T-0201 → T-0150 |
| SendGrid client / `EmailService.cs` (Wave-3) | T-0144 (IHttpClientFactory) → T-0205 (remove dead factory) | T-0205 → T-0144 |

## Wave 0 — Security & correctness blockers (PROD gate). Pre-req: ADR-0001/0002/0003 accepted ✅
| source | title | size | layers | depends_on | security_touching | manual_steps | pairs_with |
|---|---|---|---|---|---|---|---|
| BSP-1(+6) | PolicyBuilder fail-closed + complete map + AssertComplete + shared host registration | M | backend, config | — | yes | — | TC-AUTHZ-0 |
| IDA-SEC-04 | OwnerOrElevated redefine + GetUser ownership check | S | backend | BSP-1 | yes | — | TC-AUTHZ-0 |
| SEC-DSP-01 | IsStaffMessage server-derived; dispute message split (CanAddDisputeMessage/CanRespondToDispute=AdminOnly) + move staff endpoint Partner→Admin | M | backend, config | BSP-1 | yes | nswag-regen | TC-AUTHZ-0 |
| SEC-DSP-02 | CreateDispute verifies order ownership | S | backend | BSP-1 | yes | — | TC-AUTHZ-0 |
| SEC-EMP-01 | Partner analytics IDOR → EmployeeId from session + ownership | S | backend | BSP-1 | yes | — | TC-AUTHZ-0 |
| IDA-SEC-01 | Google sign-in verifies ID-token claims server-side; remove IsDevelopment bypass | M | backend | — | yes | — | TC-AUTH-TAKEOVER |
| IDA-SEC-03 | Cryptographic email/reset tokens + user-scoped lookup + expiry | M | backend, db | — | yes | ef-migration | TC-AUTH-TAKEOVER |
| IDA-SEC-08 | Admin GDPR/deactivate self + last-admin protection | S | backend | BSP-1 | yes | — | TC-AUTHZ-0 |
| IA-1 | CreateAdminUser stop double-hashing password | S | backend | — | yes | — | (regression test) |
| EMP-GAP-01 | Gate Take/Start/Complete order on ContractStatus (rejected cleaners) | M | backend | BSP-1 | yes | — | TC-AUTHZ-0 |
| LG-SEC-01 | Single-use promo: unique constraint + atomic redeem | M | backend, db | — | yes | ef-migration | TC-IDEMP-0 |
| LG-SEC-02 | Mobile direct-subscribe Stripe idempotency key | M | backend, mobile | — | yes | — | TC-IDEMP-0 |
| LG-SEC-06 | Admin loyalty grant/revoke idempotent | M | backend, db | — | yes | ef-migration | TC-IDEMP-0 |
| LG-SEC-05 | Anonymous-but-tenant-scoped membership/loyalty reads | M | backend, db | BSP-1 | yes | — | — |
| SEC-W2 | Webhook 2nd-active-membership: active-check + filtered unique index | M | backend, db | — | yes | ef-migration | TC-IDEMP-0 |
| SEC-W3 | Webhook per-IP rate-limit window (ADR-0003) | S | web, backend | BSP-4 | yes | — | — |
| BSP-4(+IDA-SEC-02) | Partitioned limiter + UseForwardedHeaders + guard + cardinality cap (ADR-0003) | M | config, backend | — | yes | — | TC-AUTHZ-0 (limiter test) |
| F11 | UoW pipeline: don't commit on validation failure | S | backend | — | no | — | TC-IDEMP-0 |
| F2/SEC-W1 | Tactical post-commit dispatch + idempotent receipt consumer (ADR-0002) | M | backend, functions | F11 | yes | — | TC-IDEMP-0 |
| F4 | Receipt idempotent-on-OrderId, fiscal-seq once, EmailSent guard | M | functions | F2 | yes | — | TC-IDEMP-0 |
| F3 | Per-queue poison/dead-letter + maxDequeue | M | functions | F2 | no | — | — |
| FUNC-CORE | Extract Cleansia.Functions.Core so consumers are unit-testable (ADR-0002 D5) | S | functions | — | no | — | — |
| FISCAL-RECON | Fiscal reconciliation sweep (ADR-0002 D3.4) | S | backend, functions | F2 | no | — | — |
| PROD-CONFIG | CSRF-in-prod (BSP-3) + Swagger-off-staging (BSP-5) + anon tenant-scoped batch (BSP-9) | S | config | BSP-4 | yes | — | — |
| PERF-IDA-01 | DB index on User.Email + lookup columns | S | db | — | no | ef-migration | — |
| **TC-PAY** | Pay-calc pure-function tests (test-first) | S | backend | — | no | — | (pay calc) |
| **TC-AUTHZ-0** | Cross-tenant/cross-user write-path rejection tests + host harness | M | backend | BSP-1 | no | — | BSP-1 etc |
| **TC-IDEMP-0** | "Safe to run twice" idempotency tests | M | backend | F2 | no | — | LG/F fixes |
| **TC-AUTH-TAKEOVER** | Token-claim binding + reset-code lookup tests | M | backend | — | no | — | IDA-SEC-01/03 |

## Wave 1 — Foundational ADRs (REFUND, INTEGRATION, soft-delete) + contracts
| source | title | size | layers | depends_on | manual_steps |
|---|---|---|---|---|---|
| ADR-REFUND | Refund/dispute money-path ADR (defense panel) | — | architect | — | — |
| ADR-INTEGRATION | IHttpClientFactory + error-classification + async-email ADR (defense panel) | — | architect | — | — |
| T-0009 | ADR + soft-delete sweep for business entities | L | architect, backend, db | — | ef-migration |
| F2-FULL | Full transactional outbox (table, dispatcher, drain) | L | backend, functions, db | F2, ADR-OUTBOX | ef-migration |
| BLIND-5 | Stripe+SendGrid via IHttpClientFactory | M | backend | ADR-INTEGRATION | — |
| BLIND-6 | Error classification across integration layer | M | backend | ADR-INTEGRATION | — |
| BLIND-1 | Registration/reset email async (off critical path) | M | backend, functions | ADR-INTEGRATION, F2 | — |
| LG-06 | Membership commands provider try/catch (B8/S7) | M | backend | ADR-INTEGRATION | — |
| LG-01q/LG-03 | Tier-threshold config read + persist grant/revoke Reason | M | backend | LG-SEC-06 | — |
| IDA-SEC-06 | Refresh rotation re-checks profile (per host) | S | backend | BSP-1 | — |
| DA-9 | Centralize CZE/Mapbox-bounds/2000-char constants | S | backend, frontend, mobile | — | — |
| FUNC-CORE-MIGRATE | Migrate remaining consumers onto Functions.Core | M | functions | FUNC-CORE | — |

## Wave 2 — Story-backed features (launch scope) — depends on Wave-1 ADRs
| source | title | size | layers | depends_on | manual_steps |
|---|---|---|---|---|---|
| AUD-01 | Admin order ops (cancel/reassign/refund/status-override) + generalized cancel | L | backend, frontend | ADR-AUTHZ, ADR-REFUND | nswag-regen |
| AUD-02/04 | Payroll adjustment + settlement lifecycle + partner payroll surface | L | backend, frontend, android | BSP-1, F2-FULL | nswag-regen, ef-migration |
| DA-2 | Dispute transition-guard (Close/Escalate/LinkStripe reachable) | M | backend | ADR-AUTHZ | — |
| D-01/DA-1/SEC-DSP-06/07 | Admin dispute management + issue refund; remove dead Partner endpoints | L | backend, frontend | ADR-AUTHZ, ADR-REFUND | nswag-regen |
| D-06 | Stripe chargeback linkage | M | backend | ADR-REFUND | — |
| LG-04 | Admin Membership-Plan CRUD | L | backend, frontend | ADR-AUTHZ | nswag-regen |
| LG-05/06f/09 | Admin referral intervention + wire orphaned endpoint + sidebar | M | backend, frontend | ADR-AUTHZ, LG-03 | — |
| LG-01f | Invoke referral expiry sweep timer | S | backend, functions | F2-FULL | — |
| LG-02 | /r/{code} referral landing route | M | frontend | — | — |
| LG-07 | Unify membership subscribe path web/mobile | S | backend, frontend | LG-SEC-02 | nswag-regen |
| F1 | Implement GenerateInvoiceFunction | S | functions | F2-FULL, AUD-02 | — |
| F8/LG-SEC-09 | SendSitewidePromo resume cursor + idempotent enqueue | M | functions, backend | F2-FULL | — |
| F7/BLIND-8 | Idempotent push dispatch | M | functions, backend | F2-FULL, ADR-INTEGRATION | — |
| F5 | Fix cron cadence (4 timers) | S | functions | — | — |
| F6 | FiscalRetryService per-receipt durability | S | backend | F2-FULL | — |
| BLIND-7 | Mapbox 429 handling | M | backend | ADR-INTEGRATION | — |
| IA-01/03 | Admin GDPR UI + partner GDPR self-service | L | backend, frontend | ADR-AUTHZ | nswag-regen |
| IA-02 | Customer notification-preferences UI | M | frontend | — | — |
| IA-05 | Device/session management UI | M | backend, frontend, mobile | — | nswag-regen |
| IA-04 | LastLoginAt tracking | M | backend, db, frontend | — | ef-migration |
| IA-08/09 | Admin self-service profile/password | M | backend, frontend | — | — |
| CC-02/03/04/06 | Service/Package in-use guard + activate/deactivate; default currency/language | L | backend, frontend | T-0009 | ef-migration, nswag-regen |
| D-04/D-10/DA-17 | Customer dispute evidence+refund UI; saved-address UI | M | frontend | — | — |
| BSP-4b | Account-lockout / per-confirmation-code throttle (rate-limit fast-follow, Q-RL-03) | M | backend, db | BSP-4 | ef-migration |
| BSP-4d | Rate-limit coverage for uncovered money endpoints | S | backend | BSP-4 | — |
| BSP-4c | Client-side Retry-After back-off jitter | S | frontend, mobile | BSP-4 | — |

## Wave 3 — Consistency & quality cleanup (the 187 + new spaghetti)
T-0001…T-0008, T-0010…T-0016 (the canonicalization tickets, already in INDEX) + DA-3/IA-2/6/8/9
(de-triplication) + DA-5/6/PERF-F1 + DA-8/AUD-06/AUD-07 (god-unit decomposition) + the LG/DA/IA long
tail + the PERF-* cluster + BLIND-4/9/10/11 (dead/unsafe code) + the S6 logging cluster. **AUD-06/07
rebase on post-Wave-0 handlers (CreateOrder, order-wizard).**

## Wave 4 — Remaining tests + a11y (TDD; land with their feature)
TC-2/3 (webhook integration incl. signature-stays-on regression), TC-7 (refund money-math), TC-4
(CreateOrder characterization — written BEFORE AUD-06, no dep), TC-6 (invoice gen), TC-8 (16 Functions),
TC-9 (cross-tenant authz integration), TC-10 (fiscal-mode characterization), EP-1/EP-2/DA-7 (error-
contract parity), TC-11 (frontend specs), A11Y-1 (accessibility).
