# Cleansia — Full-Audit Execution Plan (PM + Architect)

**Date:** 2026-06-01 · **Input:** 256 confirmed findings across 9 domain/blind-spot slices · 83 user
stories written · merged against AUD-01…25 and the 187 machine violations (T-0001…16). Produced by the
comprehensive audit workflow (25 investigators → extract → adversarial verify → stories → sequence).
Raw findings: `AUDIT-2026-06-01-findings.md`. Stories: `agents/backlog/stories/`.

---

## 1. Executive summary

Coverage is now **complete**. The 5 domains the first audit lost to a tooling failure (loyalty-growth,
disputes-addresses, identity-auth, catalog-config, employees) plus the three blind-spot integration
surfaces (Azure Functions, webhooks-security, integration clients) have all been audited end-to-end.
**The picture changed materially.** The first pass concluded "no verified security, data-loss, or crash
defect." **That conclusion is now refuted.** The complete pass surfaces **9 critical findings, of which
8 are security defects** — privilege escalation, account takeover, and double-charge money bugs the
orders/payroll-weighted first pass never reached.

**Revised overall health:** the happy paths remain mature, but the platform is **NOT production-ready**
until Wave 0 lands. The systemic flaw is now clear: **side-effects fire before the transaction commits,
and authorization is frequently absent or client-trusted on the write paths the first audit didn't open.**

### Top 5 risks before PROD
1. **Privilege escalation via fall-open authorization** — `BSP-1` (the entire payroll/pay-config/
   pay-period permission family falls through to "any authenticated user," enabling doubled financial
   side-effects) with root cause `BSP-6` (`ToPhysicalPolicy` fail-**open** default). Plus `SEC-DSP-01`
   (any authenticated user injects "staff" messages into **any** dispute) and `SEC-DSP-02` (CreateDispute
   never checks order ownership). **Critical.**
2. **Account takeover on the auth surface** — `IDA-SEC-01` (Google sign-in trusts client-supplied
   email/GoogleId, not verified token claims) and `IDA-SEC-03` (6-digit non-crypto reset codes looked up
   by code alone → brute-force → reset → takeover). **Critical.**
3. **Money correctness double-charge / over-redeem** — `LG-SEC-02` (mobile direct-subscribe creates a
   real Stripe subscription with no idempotency key → double-tap double-charges), `LG-SEC-01` (single-use
   promo redeemed past cap via concurrency race), `LG-SEC-06` (admin grant/revoke non-idempotent → retry
   double-grants real points). **Critical.**
4. **Non-transactional outbox across the whole system** — `F2`/`SEC-W1`/`F4`: handlers enqueue to 3 queues
   and consume fiscal sequence numbers + upload blobs **before** the `UnitOfWorkPipelineBehavior` commit,
   and `F11` commits even when validation fails. Combined with `F3` (no poison/dead-letter consumer for
   any of the 5 queues) this is a latent data-integrity and silent-loss minefield on the money path.
5. **Resilience black hole on external integrations** — `BLIND-1` (email synchronous on registration/
   password-reset; a SendGrid outage hard-fails signup), `BLIND-5` (Stripe + SendGrid bypass
   `IHttpClientFactory` — no resilience, no OTel, no connection reuse), `BLIND-6/7/8` (no error
   classification; Mapbox 429s silently drop geo; push is at-most-once and double-sends on redelivery).

### Prior audit top-3 — confirm / correct
- **AUD-01 (no admin order intervention)** — **CONFIRMED.** Still the single highest-value *feature* fix,
  now joined by a parallel **dispute** intervention gap (`D-01`/`DA-1`/`SEC-DSP-06`) with the same shape
  and the same `RefundAmount-recorded-but-no-refund-issued` half-built money path.
- **AUD-02/04 (payroll lifecycle "dead")** — **PARTIALLY CORRECTED.** The critic was right: the driver is
  the **Functions trigger graph**, not Admin endpoints. But `F1` (GenerateInvoiceFunction is a no-op stub
  — the generate-invoice queue is fully dead) and `TC-6/TC-8` (zero tests on invoice generation and all
  16 Functions) mean the payroll path is **both unreachable and unverifiable**.
- **"CQRS/quality erosion, but no security defect"** — **CORRECTED.** Quality erosion is real but now the
  *least* of the concerns; the authorization and money-correctness defects dominate.

---

## 2. THE SECURITY VERDICT — Stripe subscription webhook signature

**The signature gap is NOT real. The prior audit's #1-priority suspicion (FUP-1) is REFUTED by the
complete webhooks-security slice.** The first audit *suspected* `StripeSubscriptionWebhookHandler.cs`
never verifies signatures because a grep for `ConstructEvent` returned nothing in that file. The
end-to-end read corrects this: signature verification **is present** on the subscription path — performed
upstream of the handler before `ProvisionFromCreatedEventAsync` runs. No confirmed finding (SEC-W1…W6)
alleges a forged-signature bypass.

What the subscription webhook path **does** have wrong (real, lower severity than a signature bypass):
- **`SEC-W2` — major.** No active-membership check before `UserMembership.Create` → a webhook can create
  a **second active membership** (request path checks `GetActiveForUserAsync`; webhook path doesn't; no
  unique index). One-active invariant bypassed.
- **`SEC-W3` — major.** All three webhook actions are `[AllowAnonymous]` with **no rate limiting** (S5).
- **`SEC-W5` — minor.** Cross-tenant `IgnoreQueryFilters()` lookups set the tenant override from
  attacker-influenceable Stripe metadata.
- **`SEC-W1` — minor.** Receipt + push side-effects fire **before** the idempotency stamp commits.

**Gate bottom line:** no forge-a-subscription-event hole. The webhook work that must still ship before
PROD is **SEC-W2** + **SEC-W3** (both Wave 0). **FUP-1 → resolved-refuted; residual SEC-W2/W3 tracked.**

---

## 3. Dependency-ordered execution plan (Waves 0–4)

> **⚠️ CORRECTED 2026-06-01 after a collision check** (`AUDIT-2026-06-01-plan-corrections.md`). The
> original "ADRs drafted parallel to Wave 0" sequencing was self-contradictory (Wave-0 items depended on
> Wave-1 ADRs that *are* their fixes) and the TDD claim was prose-only. Corrected below:
>
> - **Pre-Wave-0 ADR sprint:** **ADR-AUTHZ, ADR-OUTBOX (contract), ADR-RATELIMIT are decided & accepted
>   BEFORE the Wave-0 items that encode them begin coding.** ADR-REFUND and ADR-INTEGRATION stay parallel
>   and gate Wave 2.
> - **Outbox split:** Wave 0 ships the self-contained, no-ADR fixes (post-commit dispatch + idempotent
>   consumers — F2/SEC-W1, F4, F3); the **full transactional outbox** is a Wave-1 ticket under ADR-OUTBOX.
> - **TDD is structural, not prose:** there is now a **Wave-0 test slice** (TC-PAY, TC-AUTHZ-0,
>   TC-IDEMP-0, TC-AUTH-TAKEOVER) that lands **in the same merge** as its Wave-0 fix. Waves are strictly
>   ordered for merge readiness. **Wave 0 is the PROD gate.** Everything is built **test-first (TDD)** per
>   `knowledge/testing.md`.

### PRE-WAVE-0 — ADR sprint (must be ACCEPTED before the Wave-0 items that encode them code)
These three ADRs are *defined as* Wave-0 fixes, so they cannot run "parallel" — they are decided first.
| id | title | layers |
|---|---|---|
| **ADR-AUTHZ** | Fail-closed `ToPhysicalPolicy` + the complete permission map (which `Policy.*` → AdminOnly/EmployeeOrAdmin/self-scoped) — BSP-1/BSP-6/IDA-SEC-04/BSP-7/BSP-8 code against this frozen table | architect |
| **ADR-OUTBOX (contract)** | The post-commit-dispatch + idempotent-consumer contract Wave-0 fixes (F2/F3/F4) implement; the full outbox build is Wave 1 | architect, backend |
| **ADR-RATELIMIT** | Partitioned per-IP/per-account limiter shape BSP-4 ships in Wave 0 and the ADR formalizes "shared across hosts" | architect |

### WAVE 0 — Security & correctness blockers (PROD gate)
| id | title | sev | size | layers | depends-on | ADR |
|---|---|---|---|---|---|---|
| **BSP-1 (+BSP-6 merged)** | One `PolicyBuilder` ticket: fail-closed fallback + complete Map + startup completeness assertion (BSP-6's fail-open flip is the same edit; its startup-assertion is BSP-1's paired guardrail test) | critical | M | backend, config | ADR-AUTHZ (accepted) | ADR-AUTHZ |
| **IDA-SEC-01** | Google sign-in trusts client email/GoogleId → verify Google ID-token claims server-side; remove IsDevelopment bypass | critical | M | backend | — | — |
| **IDA-SEC-03** | Email-confirm + password-reset codes are 6-digit non-crypto, looked up by code alone → cryptographic tokens + user-scoped lookup + expiry | critical | M | backend, db | — | — |
| **SEC-DSP-01** | `IsStaffMessage` is client-supplied → derive staff flag server-side from caller role | critical | S | backend, nswag-regen | — | — |
| **SEC-DSP-02** | CreateDispute does not verify the order belongs to the caller (S1/S3) | critical | S | backend | — | — |
| **LG-SEC-01** | Single-use promo redeemable past per-user cap via concurrency race → unique constraint + atomic redeem | critical | M | backend, db | — | — |
| **LG-SEC-02** | Mobile direct-subscribe creates Stripe subscription with no idempotency key → double-charge | critical | M | backend, mobile | — | — |
| **IA-1** | CreateAdminUser double-hashes password → new admins cannot log in | critical | S | backend | — | — |
| **SEC-EMP-01 / EMP-SEC-1** | Partner analytics IDOR: dashboard endpoints trust EmployeeId from the query string → derive from session, ownership check (S1/S3) | critical | S | backend | — | — |
| **EMP-GAP-01** | Rejected cleaners can still take/complete orders → gate Take/Start/Complete on ContractStatus | critical | M | backend | BSP-1 | — |
| **LG-SEC-06** | Admin grant/revoke loyalty points fully non-idempotent → retry double-grants | major | M | backend, db | — | — |
| **SEC-W2** | Webhook auto-provision can create a second active membership → active-check + unique index | major | M | backend, db | — | — |
| **SEC-W3** | Webhook endpoints get a per-IP Stripe-egress rate-limit window (S5) — independent of BSP-4 | major | S | web, backend | — | — |
| **BSP-4 / IDA-SEC-02** | Rate limiters global (no per-user/per-IP partition) → partitioned limiters | **critical** | M | config, backend | ADR-RATELIMIT (accepted) | ADR-RATELIMIT |
| **F11** | `UnitOfWorkPipelineBehavior` commits even when validation fails → reorder pipeline | correctness-minor | S | backend | — | — |
| **F2 / SEC-W1** | TACTICAL: move `queueClient.SendAsync` after `CommitAsync` (post-commit dispatch) + idempotent receipt consumer (full outbox is Wave 1) | major | M | appservices, functions, infra.queue | F11 | (ADR-OUTBOX contract) |
| **F4** | Idempotent-on-OrderId: reserve receipt row pre-email, fiscal sequence once, guard on `receipt.EmailSent` (self-contained) | major | M | functions, receipts | F2 | — |
| **F3** | Per-queue poison/dead-letter handlers + `host.json maxDequeue` (self-contained) | major | M | functions, infra | F2 | — |
| **IDA-SEC-08** | Admin GDPR delete/export + admin-deactivate have no self/last-admin protection | major | S | backend | — | — |
| **IDA-SEC-04** | Any Employee can read any user's full PII by id (OwnerOrElevated grants all employees) | major | S | backend | BSP-1 (serialized after) | — |
| **LG-SEC-05** | GetMembershipPlans/loyalty-tier reads on AllowAnonymous but tenant-scoped → fix tenant correctness (pulled fwd from Wave 2; more-severe sibling of BSP-9) | major | M | backend, db | ADR-AUTHZ | — |
| **PROD-CONFIG** | Prod-config hardening bundle: BSP-3 (CSRF disabled in prod), BSP-5 (Swagger on staging — serialize with BSP-4 on `CleansiaStartupBase.cs`), BSP-9 (anonymous tenant-scoped order batch) | major | S | config | — | — |

### WAVE 0 — Test slice (TDD: each lands in the SAME merge as its fix; satisfies Gate 6)
| id | title | covers | layers | depends-on |
|---|---|---|---|---|
| **TC-PAY** | Pay-calculation pure-function tests (clamp/override-precedence/bonus-deduction/rounding) — must-cover #1 | the pay formula | backend | — (TC-1-sub holiday-calendar is a *separate* backlog story; not a dep) |
| **TC-AUTHZ-0** | Cross-tenant / cross-user write-path rejection tests | BSP-1, IDA-SEC-04, SEC-DSP-01, SEC-DSP-02, SEC-EMP-01 | backend | merged-BSP-1 |
| **TC-IDEMP-0** | "Safe to run twice" idempotency tests | F2/F11/SEC-W2 **and** LG-SEC-01/LG-SEC-02(direct-subscribe)/LG-SEC-06 | backend | the paired fix |
| **TC-AUTH-TAKEOVER** | Token-claim binding (IDA-SEC-01) + `(email, hashedToken)` reset-code lookup (IDA-SEC-03) | the auth-takeover pair | backend | the paired fix |

### WAVE 1 — Foundational ADRs + backend contracts
> ADR-AUTHZ, ADR-OUTBOX, ADR-RATELIMIT were **moved to the pre-Wave-0 ADR sprint** (they gate Wave 0).
> Listed here for completeness; the two below (REFUND, INTEGRATION) are the genuinely Wave-1 ADRs.

| id | title | layers | ADR |
|---|---|---|---|
| **ADR-AUTHZ** *(pre-Wave-0; gates Wave 0)* | Fail-closed `ToPhysicalPolicy`, complete permission map, CustomerOnly on admin host (BSP-7), JWT key/audience trust (BSP-8) | architect | accepted pre-Wave-0 |
| **ADR-OUTBOX** *(contract pre-Wave-0; full build Wave 1)* | Post-commit-dispatch + idempotency contract (gates F2/F3/F4); the full transactional-outbox build is the ticket below | architect, backend | contract pre-Wave-0 |
| **ADR-RATELIMIT** *(pre-Wave-0; gates BSP-4)* | Partitioned per-IP/per-account limiter shared across hosts | architect | accepted pre-Wave-0 |
| **F2-FULL** | Full transactional outbox: outbox table, dispatcher, post-commit drain across all 5 queues (supersedes the Wave-0 tactical F2/F3/F4 with the durable design) | backend, functions, infra.queue | ADR-OUTBOX |
| **ADR-REFUND** | Refund/dispute money path: who issues the Stripe refund, where RefundAmount is consumed, chargeback linkage | architect, backend | is the ADR |
| **ADR-INTEGRATION** | `IHttpClientFactory` + error classification + async email contract | architect, backend | is the ADR |
| **T-0009** | ADR + sweep: soft-delete for business entities (DA-10/DA-15/D-09/IA-13 hard-deletes) | architect, backend, db | is the ADR |
| **BLIND-5** | Route Stripe + SendGrid through `IHttpClientFactory` (resilience + OTel + reuse) | backend | (ADR-INTEGRATION) |
| **BLIND-6** | Error classification across the integration layer | backend | (ADR-INTEGRATION) |
| **BLIND-1** | Move registration/password-reset email off the critical path (async/queue) | backend | (ADR-INTEGRATION/OUTBOX) |
| **LG-06** | Membership commands: provider try/catch on every Stripe call (B8/S7) | backend | (ADR-INTEGRATION) |
| **LG-01q / LG-03** | Tier-threshold config is dead → read config thresholds; persist admin grant/revoke Reason | backend | — |
| **IDA-SEC-06** | Refresh-token rotation re-checks profile, not only audience | backend | (ADR-AUTHZ) |
| **DA-9** | Centralize "CZE" default-country + Mapbox bounds + 2000-char cap constants | backend, frontend, mobile | — |

### WAVE 2 — Features built on those contracts (the launch scope; all story-backed)
| id | title | size | layers | depends-on |
|---|---|---|---|---|
| **AUD-01** | Admin order operations + generalized cancellation (cancel/reassign/refund/status-override) | L | backend, admin-fe | ADR-AUTHZ, ADR-REFUND |
| **AUD-02 / AUD-04** | Wire payroll adjustment + settlement lifecycle; reconcile partner payroll surface | L | backend, admin, partner, android | ADR-AUTHZ, ADR-OUTBOX, BSP-1 |
| **DA-2** *(split out — critical correctness, ships independent of admin UI)* | Dispute transition-guard: make Close/Escalate/LinkStripeDispute reachable + guarded; fix the half-built status machine | M | backend | ADR-AUTHZ |
| **D-01 / DA-1 / SEC-DSP-06/07** | Admin dispute management + issue refund; remove dead Partner-host endpoints | L | backend, admin-fe | ADR-AUTHZ (hard), ADR-REFUND (hard), ADR-INTEGRATION |
| **D-06** | Wire Stripe chargeback linkage (LinkStripeDispute) | M | backend | ADR-REFUND |
| **LG-04** | Admin Membership-Plan CRUD surface | L | backend, admin-fe | ADR-AUTHZ |
| **LG-06f / LG-05 / LG-09** | Admin referral intervention; wire orphaned by-user endpoint; surface loyalty/users in sidebar | M | backend, admin-fe | ADR-AUTHZ, LG-03 |
| **LG-01f** | Invoke referral expiry sweep (timer) | S | backend, functions | ADR-OUTBOX |
| **LG-02** | `/r/{code}` referral landing route | M | frontend | — |
| **LG-07** | Unify membership subscribe path (web/mobile) | S | backend, frontend | LG-SEC-02 |
| **F1** | Implement GenerateInvoiceFunction (revive generate-invoice queue) | S | functions, payroll | ADR-OUTBOX, AUD-02 |
| **F8 / LG-SEC-09** | SendSitewidePromo fan-out: resume cursor + idempotent enqueue | M | functions, backend | ADR-OUTBOX |
| **F7 / BLIND-8** | Idempotent push dispatch (per-message key; fix at-most-once + prune-on-init-failure) | M | functions, backend | ADR-OUTBOX, ADR-INTEGRATION |
| **F5** | Fix cron cadence on 4 notification/recurring timers | S | functions | — |
| **F6** | FiscalRetryService per-receipt durability (no all-or-nothing 50-batch commit) | S | appservices | ADR-OUTBOX |
| **BLIND-7** | Mapbox 429/rate-limit handling | M | backend | ADR-INTEGRATION |
| **IA-01 / IA-03** | Admin GDPR back-office UI + partner GDPR self-service | L | backend, admin-fe, partner-fe, i18n | ADR-AUTHZ |
| **IA-02** | Customer-web notification-preferences UI (11-category API) | M | customer-fe, i18n | — |
| **IA-05** | Device / active-session management (GetMyDevices + revoke UI) | M | backend, fe, mobile, nswag-regen | — |
| **IA-04** | LastLoginAt tracking | M | backend, db (ef-migration), admin-fe | — |
| **IA-08 / IA-09** | Admin self-service profile/password; accept BirthDate/PreferredLanguageCode | M | backend, admin-fe | — |
| **CC-02/03/04/06** | Service/Package in-use guard + activate/deactivate; default-currency + default-language commands | M–L | backend, fe, i18n | T-0009 |
| **D-04 / D-10 / DA-17** | Customer dispute evidence+refund UI parity; status filter/unread; saved-address management UI | M | customer-fe | — |
<!-- LG-SEC-05 removed from Wave 2 — pulled forward to Wave 0 (self-review fix #1: was duplicated) -->

### WAVE 3 — Consistency & quality cleanup (the 187 + the new spaghetti)
| id | title | layers |
|---|---|---|
| **T-0001…T-0008, T-0010…T-0015** | The 14 mechanical canonicalization tickets | backend/frontend/android |
| **T-0016** | ADR + migrate customer-app repos to `ApiResult<T>` (Architect-owned) | architect, android, ios |
| **DA-3 / IA-2 / IA-6 / IA-8 / IA-9** | De-triplicate Dispute/SavedAddress/Auth controllers + login/forgot facades; unify email/password rules | backend, frontend |
| **DA-5 / DA-6 / PERF-F1** | Customer features importing `@cleansia/partner-services`; disputes list → cleansia-table archetype | frontend |
| **DA-8 / AUD-06 / AUD-07** | God-method/handler/facade decomposition (AddSavedAddress, CreateOrder.Handler, order-wizard) | backend, frontend |
| **LG/DA/IA long tail** | B/C/D-rule deviations, wrong-source ledger, CQRS-violation reads, magic strings, swallowed catches | backend/frontend/mobile |
| **PERF-* cluster** | Missing indexes (User.Email, address dedup, membership/referral), tracked reads, eager Includes, projection-before-order | backend, db |
| **BLIND-4/9/10/11** | Remove dead/unsafe code (HandlebarsTemplateEngine XSS+FormatException trap, dead SendGridFactory) | backend, mobile |
| **F10 / BLIND-3 / IDA-SEC-05 / logging cluster** | Logging hygiene S6: stop logging messageText/PII/Stripe-ids/confirmation-codes | backend, functions |

### WAVE 4 — Remaining tests + accessibility
> The highest-risk tests (pay calc, authz, money idempotency, auth-takeover) were **pulled into the
> Wave-0 test slice** (TC-PAY/TC-AUTHZ-0/TC-IDEMP-0/TC-AUTH-TAKEOVER) to satisfy TDD/Gate 6. What
> remains here are the feature-level and integration tests that land *with* their Wave-2 features.

| id | title | sev | layers | depends-on |
|---|---|---|---|---|
| **TC-2 / TC-3** | Stripe order + subscription webhook integration tests (idempotency, lifecycle) + a regression that **signature verification stays on** (it's REFUTED as a gap, not removed) | critical | backend | with SEC-W2/F2 in Wave 0 |
| **TC-7** | Refund/dispute money-math tests | major | backend | with DA-2 / ADR-REFUND |
| **TC-4** | CreateOrder **characterization** tests written FIRST (before AUD-06 refactor) | major | backend | — (no AUD-06 dep; AUD-06 rebases on these) |
| **TC-6** | Invoice generation/numbering/pay-period-close tests | major | backend | AUD-02, F1 |
| **TC-8** | Tests for the 16 Azure Functions | major | backend | ADR-OUTBOX, F-series |
| **TC-9** | Authorization / cross-tenant write-path tests | major | backend | ADR-AUTHZ, BSP-1 |
| **TC-10** | Fiscal-mode selection tests | major | backend | — |
| **EP-1 / EP-2 / DA-7** | Error-contract parity: customer `api.*` keys, 54 untranslated keys, dispute/address `errors.*` across 5 locales | major | frontend, i18n | feature waves |
| **TC-11** | Frontend interceptor/error-pipe/i18n-mapping specs | minor | frontend | EP-1/EP-2 |
| **A11Y-1** | Accessibility pass on `cleansia-*` library + order wizard (3 apps) | minor | frontend | — |

> i18n LOCALE parity is **proven clean** (mechanically: identical key sets ×5 locales ×3 apps) — not
> re-raised. EP-1/EP-2/DA-7 are *error-contract* parity (`BusinessErrorMessage` ↔ `errors.*`), a
> different axis, and remain open.

---

## 4. Systemic themes (7)

1. **Non-transactional outbox (dominant new theme).** Side-effects are enqueued/executed *before* the
   single `UnitOfWork` commit, and the pipeline even commits on validation failure
   (F2/F4/F11/SEC-W1/F6/F7/F8/LG-SEC-09 are one architectural defect). → **ADR-OUTBOX**.
2. **Fail-open authorization & client-trusted identity.** The policy builder defaults to "allow"
   (BSP-1/BSP-6); staff/ownership/profile flags taken from the client (SEC-DSP-01/02, IDA-SEC-01/04/06);
   rate limits a single global bucket. → **ADR-AUTHZ + ADR-RATELIMIT**.
3. **Money paths non-idempotent or half-built.** Double-charge on subscribe (LG-SEC-02), over-redeem
   promos (LG-SEC-01), double-grant points (LG-SEC-06), `RefundAmount` recorded but **never refunded**
   (SEC-DSP-06/D-06/DA-2). → **ADR-REFUND**.
4. **Integrations bypass the platform's own resilience plumbing.** Stripe/SendGrid `new` their clients
   instead of `IHttpClientFactory`; no error classification; email synchronous on signup; Mapbox 429s
   and FCM failures swallowed. → **ADR-INTEGRATION**.
5. **Dead lifecycle states & unreachable back-office (carried + amplified).** Now spans disputes,
   memberships, referrals, GDPR, devices, catalog activate/deactivate.
6. **Cross-app coupling & triplication.** Customer features import `@cleansia/partner-services`;
   Dispute/SavedAddress/Auth controllers + login/forgot facades copy-pasted with drifting auth.
7. **Verifiability vacuum on the riskiest paths.** Zero tests on pay calc, both webhooks, CreateOrder,
   invoice generation, all 16 Functions, cross-tenant authz. The highest-risk code is the least covered
   — exactly why Wave 0's fixes must land **with** Wave 4's tests (TDD), not before.
