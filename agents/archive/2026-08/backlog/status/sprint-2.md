# Sprint 2 — Complete Codebase Audit + User Stories

- **Date:** 2026-06-01
- **Goal:** Re-run the audit completely (cover the 5 lost domains + the blind spots), write a user
  story per gap, and sequence everything in correct dependency order — before any execution.

## What ran
A workflow of **25 investigators** (the 5 previously-lost domains × 4 dimensions + 5 blind-spot passes:
Stripe webhooks, the 16 Azure Functions, integration clients, AppHost/secrets, contract-parity/tests),
each grounded in real file:line. Free-text reports → structured extraction → **adversarial verification**
of every critical/major finding → **83 user stories** (one per gap) → opus **execution plan**.
**304 agents, ~15M tokens, ~57 min.**

## Result
- **256 confirmed findings** (39 critical, 131 major; 60 security, 54 gaps, 52 spaghetti, 50 perf).
- **83 user stories** written (`stories/AUDIT-2026-06-01-user-stories.md`).
- **Wave-ordered execution plan** (`audits/AUDIT-2026-06-01-execution-plan.md`) — the thing to act on.
- Findings: `audits/AUDIT-2026-06-01-findings.md` (+ `.json` raw). Slice reports: `…-slice-reports.md`.

## 🔴 The headline — this is serious
The first (partial) audit said "no security/data-loss/crash defect." **The complete audit overturns
that: 8 of the 9 criticals are security defects** — privilege escalation (fail-open authorization,
client-trusted staff/ownership flags, partner-analytics IDOR), account takeover (Google sign-in trusts
client claims; brute-forceable 6-digit reset codes), and double-charge money bugs (no Stripe idempotency
keys). Plus a systemic **non-transactional outbox** (side-effects fire before commit; pipeline commits on
validation failure) and **rejected cleaners can still work orders**. **The platform is NOT
production-ready until Wave 0 lands.**

## ✅ The one piece of good news
The prior #1 suspicion — the **Stripe subscription webhook signature gap (FUP-1)** — is **REFUTED.**
End-to-end reading proved signature verification *is* present (upstream of the handler). Adversarial
verification did its job: killed a false alarm, surfaced the real (worse) ones. Residual real webhook
issues (SEC-W2 second-active-membership, SEC-W3 no rate limit) are in Wave 0.

## The plan (5 waves, strictly ordered for merge; ADRs drafted parallel to Wave 0)
- **Wave 0 — PROD gate:** ~25 security/correctness blockers (in `INDEX.md`).
- **Wave 1 — 5 foundational ADRs** (AUTHZ, OUTBOX, RATELIMIT, REFUND, INTEGRATION) + soft-delete + the
  backend contracts the features build on.
- **Wave 2 — story-backed features:** admin order ops, payroll lifecycle, dispute management + refunds,
  membership/referral/GDPR/device surfaces, catalog activate/deactivate (the 83 stories).
- **Wave 3 — consistency/quality:** the 187 machine violations + the new spaghetti + perf/indexes.
- **Wave 4 — tests + a11y:** pay calc, both webhooks, CreateOrder, invoice gen, all 16 Functions,
  cross-tenant authz, error-contract parity. **All development is test-first (TDD)** — Wave 4's tests on
  the riskiest paths land *with* Wave 0's fixes.

## For the owner — decisions
1. **Wave 0 is the PROD gate** — approve it and the PM promotes to `ready` and starts (test-first).
2. **The 5 ADRs (Wave 1)** should be drafted now, in parallel — approve the Architect starting them.
3. Want me to begin **Wave 0** immediately? The cleanest first strike: the fail-open authorization root
   (`BSP-6` + `BSP-1`) and the auth-takeover pair (`IDA-SEC-01/03`) — each is a contained, high-impact,
   test-first fix.

## Pre-Wave-0 ADR sprint — ✅ COMPLETE (all 3 ADRs accepted via defense panels)
All three gating ADRs went through the architect defense panel (author → 3 challengers → defense → lead
adjudication, grounded in verified code). Each panel caught real holes the author missed.

- **ADR-0001 — Authorization model — ACCEPTED.** `backlog/adr/0001-…` + `architecture/decisions/authz.md`.
  Challengers caught 6 blockers (incl. the IDA-SEC-04 fix reading a non-existent route key + a fictional
  handler check); lead corrected a wrong rebuttal in-place (the `CanRespondToDispute` customer-self-reply
  split). Owner-ratified Q-0005 (staff dispute reply = Admin-only). Q-0001…Q-0005 answered.
  Decisions: fail-CLOSED `Deny` default + `AssertComplete` brick; complete frozen permission map (payroll
  family closed); `OwnerOrElevated` redefined + real `GetUser` ownership check; shared per-host registration;
  JWT trust boundary frozen; `Cleansia.HostTests` prerequisite.
- **ADR-0002 — Outbox dispatch contract — ACCEPTED.** `backlog/adr/0002-…` + `architecture/decisions/outbox.md`.
  Challengers caught the at-most-once push hole (bad citation → guard-first redesign) and forced the
  honest Wave-0-tactical vs Wave-1-full guarantee table; lead fixed the author's own wrong send-site count
  (15→21). Two new **blocking Wave-0 deliverables**: extract `Cleansia.Functions.Core` (testable consumers)
  + the fiscal reconciliation sweep. **No owner escalations.**
- **ADR-0003 — Partitioned rate limiting — ACCEPTED.** `backlog/adr/0003-…` + `architecture/decisions/ratelimit.md`.
  Challengers caught the two fatal traps: per-IP is fake behind a proxy unless XFF/`ForwardLimit` is
  verified (→ config-driven + **fail-to-boot guard**), and unbounded per-IP partitions = memory-DoS (→
  cardinality cap). Also raised the honest-checkout false-positive (10→30/min authenticated) and the
  coverage gap (`BSP-4d`). Companions `BSP-4b/4c/4d` named.

## Owner decisions still needed (blocking the rate-limit feature going live, NOT its merge)
- **Q-RATELIMIT-02 [blocking]** — confirm the production proxy chain / hop count / which appliance strips
  X-Forwarded-For, so `ForwardLimit`+`KnownNetworks` are set correctly. (The D3 startup guard refuses to
  boot prod on an unconfirmed/over-broad value, so the PR can merge but the feature can't be enabled.)
- **Q-RATELIMIT-03 [blocking]** — may Wave 0 ship with the 6-digit confirmation-code brute-force surface
  mitigated only by per-IP (with `BSP-4b` as fast-follow), or must `BSP-4b` (account/code lockout) land
  in the same wave?
- **Q-RATELIMIT-01 [non-blocking]** — recorded trigger: API scale-out > 1 instance requires a
  distributed limiter ADR first (instances pinned to 1 for now).

## Plan self-review (owner-requested)
- Reviewed the corrected plan end-to-end. Found + **fixed** 2 real bugs: `LG-SEC-05` was duplicated
  (Wave 0 + Wave 2) → removed Wave-2; two criticals (`SEC-EMP-01` IDOR, `EMP-GAP-01` rejected-cleaners)
  had tests but no fix ticket → added to Wave 0. 3 more noted in `audits/…-plan-corrections.md`.

## Next
- **Owner:** answer Q-0001…Q-0005 (auth business decisions) when convenient.
- Remaining pre-Wave-0 ADRs via defense panel: **ADR-OUTBOX (contract)** and **ADR-RATELIMIT**.
- Then Wave 0 begins test-first: BSP-1 (merged) codes against the now-frozen ADR-0001 permission map,
  with reviewer + security in parallel. PM promotes stories → tickets as each wave opens.
