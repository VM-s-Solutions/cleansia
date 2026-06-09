export const meta = {
  name: 'wave3-3a-1-fix',
  description: 'Fix Batch 3A wave-1 review findings: T-0174 (multi-tenant chargeback read + terminal-state guard + vacuous test + DisputeReason.Chargeback) and T-0172 finishing (AC5 integration test, red-green log, comment nits)',
  phases: [
    { title: 'Design', detail: 'architect settles the chargeback-vs-guard + DisputeReason.Chargeback questions' },
    { title: 'Fix', detail: 'backend applies T-0174 + T-0172 fixes' },
    { title: 'Re-review', detail: 'reviewer + security re-audit T-0174; reviewer re-checks T-0172' },
  ],
}

const FACTS = `
CONTEXT: Batch 3A wave-1 landed T-0172 (dispute transition-guard), T-0170a (admin cancel — ACCEPTED, no
changes), T-0174 (chargeback LinkStripeDispute). The panel found real defects in T-0174 + finishing items on
T-0172. The combined tree compiles and Cleansia.Tests is 842 green; these are correctness/coverage fixes.

T-0174 FINDINGS (in src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs +
src/Cleansia.Infra.Database/Repositories/DisputeRepository.cs):

1. MULTI-TENANT BUG (blocker). ReflectChargebackStatus (HandlePaymentNotification.cs:383-407) reads the
   dispute via disputeRepository.GetByStripeDisputeIdAsync (:386) BEFORE setting the tenant override (:395),
   and that repo method (DisputeRepository.cs:26-30) uses GetDbSet() WITHOUT IgnoreQueryFilters(). On the
   anonymous webhook there is no tenant claim → the global filter collapses to TenantId==null → for any
   tenant with non-null TenantId rows the lookup returns null, so charge.dispute.updated/closed NEVER
   reflects won/lost in multi-tenant mode. (The .created path is fine — it resolves via the tenant-ignoring
   order read at :347 then overrides at :356.)

2. GUARD BYPASS (blocker). ReflectChargebackStatus calls dispute.Resolve/Close/Escalate directly (:401-407),
   bypassing the Dispute.CanTransitionTo guard T-0172 installed (Dispute.cs). T-0172's table makes
   Resolved/Closed terminal. A late/out-of-order Stripe event (e.g. won after a prior lost left it Closed)
   would force an illegal Closed→Resolved transition — two writers disagreeing on the legal graph.

3. VACUOUS AC4 TEST (blocker). HandleChargebackNotificationTests.cs (the created→updated→closed sequence
   test) stubs GetByStripeDisputeIdAsync to ALWAYS return the dispute regardless of whether .created linked
   it — so it does not prove updated/closed resolve via the link .created wrote. It would pass even if
   .created never called LinkStripeDispute.

4. DisputeReason.Chargeback (should-fix). ADR-0006 D4 freezes the created chargeback dispute as
   Reason = Chargeback, but DisputeReason (Core.Domain/Enums/DisputeReason.cs) has NO Chargeback member —
   the code substituted UnauthorizedCharge (HandlePaymentNotification.cs:363). NOTE: DisputeReason crosses
   the wire (customer-client.ts) and is [SwaggerEnumAsInt], so adding a value is an ADDITIVE wire change →
   nswag-regen. The ARCHITECT decides: add Chargeback=8 (faithful to the frozen ADR; costs a regen) vs.
   record UnauthorizedCharge as the accepted mapping (no regen). Do not silently diverge from the ADR.

T-0172 FINISHING ITEMS:
5. AC5 — add an integration test under src/Cleansia.IntegrationTests/Features/Disputes/ proving a non-admin
   caller is rejected on the UpdateStatus route (mirror an existing IntegrationTests authz test shape;
   Testcontainers Postgres + WebApplicationFactory). The route is on Cleansia.Web.Partner DisputeController
   gated by Policy.CanUpdateDisputeStatus (AdminOnly).
6. AC6 — record "red → green" in the T-0172 ticket status log (agents/backlog/tickets/T-0172-da-2.md).
7. Comment nits — strip the "DA-2" finding refs from the two test doc-comments
   (UpdateDisputeStatusHandlerTests.cs:11, DisputeTransitionTests.cs:7) and the decorative ── divider
   banners (DisputeTransitionTests.cs ~:53/:79/:95). Keep the behavioral description.
`

const RULES = `
RULES: handler happy-path only; no CommitAsync in handlers; BusinessResult; records; deterministic keys;
TEST-FIRST for the new behavior (write the failing test, confirm red, then fix). Comment discipline: no
task/finding-number refs in source (no // T-0174, // DA-2, // AC4), keep only ADR-NNNN/S-rule refs where
load-bearing. Do NOT run dotnet ef / npm generate — flag manual_step. Build src/Cleansia.Api.sln + run
src/Cleansia.Tests green. For the IntegrationTests authz test, free port 5432 if needed (Testcontainers).
`

phase('Design')
const design = await agent(
  `You are the SOLUTION ARCHITECT. Two design questions block the T-0174 fix; settle them tersely so the dev
implements once.

${FACTS}

DECIDE:
A. The chargeback status-reflection vs the T-0172 transition guard. ReflectChargebackStatus must NOT perform
   a transition T-0172's table forbids. Options: (a) route the won/lost/updated writes through the same
   guarded path (CanTransitionTo) and no-op+warn when the current status is terminal; (b) keep the direct
   intent-method calls but add an explicit terminal-state guard in ReflectChargebackStatus that no-ops+warns
   on a terminal dispute. Either way the rule must be: a late event cannot force Closed→Resolved or any edge
   T-0172 rejects. Pick one, state the exact guard, and define a check-consistency rule idea ("direct
   Dispute.Close/Escalate/Resolve outside Dispute.UpdateStatus/ResolveDispute is a finding") if warranted.
   Also confirm the won/lost→DisputeStatus mapping (won→Resolved-class via Resolve; lost→Closed) is the one
   ADR-0006 D4 intends and is reachable from Escalated in T-0172's table.
B. DisputeReason.Chargeback: add the enum value (additive, honors ADR-0006 D4, triggers nswag-regen for the
   customer client) OR record UnauthorizedCharge as the accepted substitution (no regen). Read ADR-0006 D4
   (agents/backlog/adr/0006-refund-dispute-money-path.md) — it freezes "Reason = Chargeback". Recommend the
   faithful option unless there's a concrete reason not to; state the manual_step consequence.

Read the real files (HandlePaymentNotification.cs:324-410, DisputeRepository.cs, Dispute.cs transition table,
DisputeReason.cs, ADR-0006 D4). Output a tight design note: the decision on A (with the exact guard shape),
the decision on B (with the manual_step), and the multi-tenant read fix (use a tenant-ignoring
GetByStripeDisputeIdAsync + override-before-write, mirroring GetByStripePaymentIntentIdIgnoringTenantAsync).
No code — just the contract the dev follows.`,
  { label: 'architect:3a-fix', phase: 'Design', agentType: 'architect' },
)

phase('Fix')
const dev = await agent(
  `You are the BACKEND developer. Apply the Batch 3A wave-1 fixes per the architect note. TEST-FIRST.

=== ARCHITECT DESIGN NOTE ===
${design}
=== END NOTE ===

${FACTS}
${RULES}

T-0174 FIXES:
1. Multi-tenant read: add a tenant-ignoring GetByStripeDisputeIdAsync (IgnoreQueryFilters(), mirror
   OrderRepository.GetByStripePaymentIntentIdIgnoringTenantAsync) and in ReflectChargebackStatus read the
   dispute tenant-ignoring, then SetTenantOverride(dispute.TenantId) BEFORE mutating. Add a handler test
   that runs with a NON-null tenant and asserts updated/closed reflects the status (this test must FAIL
   against the current GetDbSet() read — confirm red first).
2. Terminal-state guard (per the architect's decision A): ReflectChargebackStatus must not force an illegal
   transition; no-op + warn (≤ Warning, no PII) when the dispute is in a terminal state. Add a handler test:
   closed:won arriving on an already-Closed dispute → Status unchanged, no exception.
3. Fix the vacuous AC4 test: drive GetByStripeDisputeIdAsync via state — it returns the dispute only AFTER
   .created set dispute.StripeDisputeId (e.g. Returns(() => dispute.StripeDisputeId == StripeDisputeId ?
   dispute : null)); assert .created wrote the id before .updated is processed.
4. DisputeReason per architect decision B (add Chargeback=8 OR keep UnauthorizedCharge with the recorded
   rationale). If adding the enum, flag manual_step: nswag-regen (customer client).

T-0172 FINISHING:
5. Add the AC5 IntegrationTests non-admin-rejection test under
   src/Cleansia.IntegrationTests/Features/Disputes/ (mirror an existing IntegrationTests authz test +
   BaseIntegrationTest/Testcontainers; assert a non-admin caller on the UpdateStatus route gets 403/NotFound,
   not 200).
6. Append a "red → green" line to the T-0172 ticket status log (agents/backlog/tickets/T-0172-da-2.md) naming
   the test files + the red reason (UpdateStatus returned void / missing error code) → green (842 pass).
7. Strip the DA-2 comment refs + decorative ── dividers from the two T-0172 test files (keep the behavioral
   descriptions).

Build src/Cleansia.Api.sln + run src/Cleansia.Tests green; run the new IntegrationTests dispute test green
(Testcontainers). Return: files changed, the repo fix, the terminal-guard shape, the DisputeReason decision +
manual_step, the new test names + red→green for each, build/test results.`,
  { label: 'dev:3a-fix', phase: 'Fix', agentType: 'backend' },
)

phase('Re-review')
const [review, security] = await parallel([
  () => agent(
    `You are the REVIEWER re-auditing the Batch 3A wave-1 fixes. Verify EACH finding is genuinely fixed:
T-0174: (1) GetByStripeDisputeIdAsync now IgnoreQueryFilters + override-before-write, with a NON-null-tenant
test that would fail against the old read; (2) ReflectChargebackStatus cannot force an illegal transition on
a terminal dispute (no-op+warn), with a closed-on-terminal test; (3) the AC4 sequence test now drives the
link through state (would fail if .created didn't link); (4) DisputeReason handled per the architect note
(enum added + nswag-regen flagged, OR substitution recorded). T-0172: (5) AC5 IntegrationTests non-admin
test exists + passes; (6) red→green in the ticket log; (7) no DA-2 refs / decorative dividers remain.
Re-derive nothing vacuously — confirm the multi-tenant test actually exercises a non-null tenant. Run the
gate (build + Disputes/Payments filters + the new IntegrationTests dispute test). Verdict APPROVE/
APPROVE-WITH-NITS/REQUEST-CHANGES with file:line.`,
    { label: 'review:3a-fix', phase: 'Re-review', agentType: 'reviewer' },
  ),
  () => agent(
    `You are the SECURITY reviewer re-auditing T-0174 after the fixes (security_touching: signed Stripe
ingress). Confirm: the multi-tenant read fix does NOT introduce a cross-tenant write (tenant-ignoring READ
then SetTenantOverride from the resolved dispute's own TenantId before any write — never a write under the
wrong/empty tenant); S7 idempotency still holds (chargeback branch after the ProcessedStripeEvents gate);
S6 (no PII/raw payload, the new terminal-state warn logs only EventType/OrderId); the terminal-state guard
fails closed (no illegal mutation). No regression to AC6 signature rejection. Read the real files. Verdict
PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:3a-fix', phase: 'Re-review', agentType: 'security' },
  ),
])

return { dev, design, review, security }
