# Full Audit — Confirmed Findings (2026-06-01)

The complete audit: **25 investigator slices** (5 previously-lost domains × 4 dimensions + 5 blind-spot
passes), free-text reports → structured extraction → **adversarial verification** of every critical/major
finding → user stories per gap → wave-ordered plan.

- **256 confirmed findings** (after refuting/severity-correction; raw extractor rows incl. retries: 277).
- Full structured data: [`AUDIT-2026-06-01-findings.json`](./AUDIT-2026-06-01-findings.json) (one object
  per finding: id, title, severity, type, where, impact, fix, size, layers, isGap, verifyNote).
- Per-slice narrative reports: [`AUDIT-2026-06-01-slice-reports.md`](./AUDIT-2026-06-01-slice-reports.md).
- **The ranked, dependency-ordered plan is in [`AUDIT-2026-06-01-execution-plan.md`](./AUDIT-2026-06-01-execution-plan.md)** — read that to act.

## Distribution
| Severity | Count |  | Type | Count |
|---|---|---|---|---|
| critical | 39 |  | security | 60 |
| major | 131 |  | gap | 54 |
| minor | 107 (incl. some verified-down) |  | spaghetti | 52 |
|  |  |  | perf | 50 |
|  |  |  | bug | 39 |
|  |  |  | missing-test | 11 |
|  |  |  | hardcoded | 11 |

**92 findings are functional gaps → 83 user stories written** (some gaps merged). Stories:
[`../stories/AUDIT-2026-06-01-user-stories.md`](../stories/AUDIT-2026-06-01-user-stories.md).

## The headline: the "no security defect" verdict was overturned
The first (partial) audit concluded there was no security/data-loss/crash defect. The complete pass
**refutes that**: **8 of the 9 criticals are security defects.** Conversely, the prior #1 suspicion —
the Stripe **subscription webhook signature gap (FUP-1)** — was **REFUTED**: signature verification is
present, just upstream of the handler. (See the execution plan §2 for the full verdict.)

## Critical findings (the PROD-blocking set — all in Wave 0 of the plan)

**Authorization / privilege escalation**
- `BSP-1` — entire payroll/pay-config/pay-period permission family falls through to "any authenticated
  user" (root cause `BSP-6`: `ToPhysicalPolicy` defaults fail-**open**).
- `SEC-DSP-01` / `D-02` — any authenticated user injects "staff" messages into **any** dispute
  (`IsStaffMessage` is client-supplied).
- `SEC-DSP-02` — `CreateDispute` doesn't verify the order belongs to the caller.
- `SEC-EMP-01` / `EMP-SEC-1` — any partner reads any other partner's order/time/productivity analytics
  (EmployeeId trusted from the query string — IDOR).
- `IDA-SEC-04` — any Employee can read any user's full PII by id.

**Account takeover / auth**
- `IDA-SEC-01` — Google sign-in trusts client-supplied email/GoogleId, not verified token claims.
- `IDA-SEC-03` — email-confirm + password-reset codes are 6-digit non-crypto, looked up by code alone
  (brute-force → reset → takeover).
- `IDA-SEC-02` — auth rate limiter is a single global bucket (brute-force unthrottled; global DoS).
- `IA-1` — `CreateAdminUser` double-hashes the password → new admins cannot log in.

**Money correctness**
- `LG-SEC-02` — mobile direct-subscribe creates a real Stripe subscription with **no idempotency key**
  → double-tap double-charges.
- `LG-SEC-01` — single-use promo redeemable past per-user cap via concurrency race.
- `LG-SEC-06` — admin grant/revoke loyalty points fully non-idempotent → retry double-grants.

**Integrations / resilience**
- `BLIND-1` — email sent inline & synchronously on the critical path; a SendGrid outage hard-fails
  registration and password-reset.
- `BLIND-2` — Mapbox access token in the URL query string → leaked into OpenTelemetry traces / HTTP logs.

**Dead / broken functionality (critical because user- or money-facing)**
- `CC-01` — Feature Flags: full backend CRUD + check endpoints, **zero consumers** (entire feature dead).
- `CC-02` / `CAT-01` / `CAT-SEC-01` — Services & Packages hard-delete with no in-use guard (orphans
  historical orders / pay configs, or FK-fails).
- `CC-03` — Service/Package "deactivated" state is filtered-on but **unreachable** (no activate/deactivate).
- `LG-01` — referral expiry sweep implemented but **never invoked** (referrals never expire); also admin
  tier-threshold config is dead (domain hardcodes thresholds the admin UI pretends to edit).
- `LG-02` — web referral share links `/r/{code}` have no landing route → acquisition funnel 404s.
- `D-01` / `DA-1` — no Admin dispute management surface (resolve/respond/status live only on Partner API).
- `DA-2` — Close/Escalate/LinkStripeDispute transitions unreachable; dispute status machine half-built.
- `IA-01` — Admin GDPR back-office has zero UI; the request/consent audit surface is unreachable.
- `IA-3` — mobile users can request a password reset but cannot complete one (web is the inverse).
- `EMP-GAP-01` — **rejected cleaners can still take and complete orders.**
- `EMP-GAP-02` — `ContractStatus.Active`/`.Terminated` never reachable; no status lifecycle after
  approve/reject.

**Performance (critical = hot path / cost)**
- `PERF-IDA-01` — `User.Email` (and other lookup columns) have **no DB index**.
- `PERF-IDA-02` — `UserRepository.GetQueryable()` eager-loads Orders on every single-user fetch.
- `PERF-IDA-03` — Login/ChangePassword/GoogleAuth/ConfirmEmail re-fetch the same user 2–5× per request.
- `PERF-EMP-01` — partner dashboard fans out into ~7 endpoints, several re-pulling the cleaner's full
  order history into memory.
- `PERF-CAT-01/02` — no caching for near-static catalog reads; `GetPagedPackages` forces client-side eval.

**Verifiability**
- `TC-1` — pay calculation: **zero tests** (must-cover #1). `TC-2` — Stripe order webhook: zero tests.

## How to read severities
Every critical/major was **adversarially verified** — a second agent tried to refute it against the real
code. Verification **corrected several down** (e.g. `SEC-W1` major→minor because `GenerateReceiptFunction`
is idempotent so duplicate *receipts* don't occur, though duplicate *push* does) and **confirmed the rest
with code evidence** (in each finding's `verifyNote`). This is why the list is trustworthy, not inflated.
