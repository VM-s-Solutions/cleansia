export const meta = {
  name: 'wave3-3a-1',
  description: 'Wave-3 Batch 3A wave-1: 3 parallel backend tickets (T-0172 dispute transition-guard, T-0170a generalized cancel + admin-cancel via refund seam, T-0174 chargeback LinkStripeDispute), each dev + reviewer + security, TDD',
  phases: [
    { title: 'Build', detail: 'T-0172 + T-0170a + T-0174 in parallel (disjoint files)' },
    { title: 'Review', detail: 'reviewer + security per ticket' },
  ],
}

const COMMON = `
PROJECT RULES (non-negotiable, from CLAUDE.md + agents/knowledge): CQRS/MediatR one-file feature
(Command+Handler+Validator+Response); handler HAPPY-PATH only (all validation in a FluentValidation
Validator with Cascade.Stop; EVERY type named *Command needs a Validator or the pipeline throws at runtime);
NEVER CommitAsync in a handler (the UnitOfWork pipeline commits on BusinessResult{IsSuccess:true}); return
BusinessResult<T>; errors via new Error(field, BusinessErrorMessage.X) with dot-notation keys; DTOs are
positional records. Idempotency idioms S7a/S7b (deterministic keys, claim-before-act, caught 23505). Comment
discipline: write almost none — only non-obvious critical logic; NO task-number refs in code (no // T-0172,
no // AC#); keep ONLY load-bearing ADR-NNNN + S1-S10 refs. TEST-FIRST (red→green) is MANDATORY for money
math, authz, and state-machine transitions — write the failing tests FIRST, confirm they fail for the right
reason, then implement; an implementation-first change fails the review gate. Do NOT run dotnet ef
(owner-only) — flag manual_step: ef-migration if schema changes. Do NOT run npm generate / hand-edit NSwag
clients — flag manual_step: nswag-regen if a client DTO/endpoint surface changes. Build src/Cleansia.Api.sln
+ run src/Cleansia.Tests green before returning. Backend only (frontend halves are separate, held on regen).
`

phase('Build')
const [t0172, t0170a, t0174] = await parallel([
  () => agent(
    `You are the BACKEND developer. Implement T-0172 — Dispute transition-guard (make Close/Escalate
reachable + reject illegal transitions). Full ticket: agents/backlog/tickets/T-0172-da-2.md (read it).

THE HOLE: Dispute.UpdateStatus (Cleansia.Core.Domain/Disputes/Dispute.cs:64-68) free-sets ANY DisputeStatus
with no guard; UpdateDisputeStatus.Handler (Features/Disputes/UpdateDisputeStatus.cs:45) calls it
unconditionally. The intent-named methods Close (:92-96), Escalate (:98-102), LinkStripeDispute (:104-108)
have ZERO production callers. DisputeStatus = Pending/UnderReview/WaitingForResponse/Resolved/Closed/Escalated
(Enums/DisputeStatus.cs:6-14).

DELIVER (per the ACs):
- AC1/AC2/AC3: enforce a legal-transition table INSIDE Dispute.UpdateStatus (or a guarded transition the
  handler consumes) so legal edges succeed and illegal edges (e.g. Resolved→Pending, Pending→Closed skipping
  resolution) are REJECTED as BusinessResult.Failure with a NEW BusinessErrorMessage code
  dispute.invalid_status_transition (add it next to the other dispute.* codes in
  Common/BusinessErrorMessage.cs). No exception thrown, no CommitAsync side effect on rejection. Make
  Close/Escalate reachable through the UpdateStatus write path (grep for their callers must now hit production).
- AC4: Resolve (Dispute.cs:82-90) stays the ONLY writer of resolution fields (RefundAmount/ResolvedBy/etc.);
  UpdateStatus must NOT set Status=Resolved and must NOT mutate resolution fields.
- AC5: authorization unchanged (Policy.CanUpdateDisputeStatus, Admin) — do not re-home the endpoint.
- The legal transition graph: ADR-0006/ADR-0009 are the accepted refund ADRs; if they don't pin an explicit
  dispute-status graph, use the natural lifecycle: Pending↔UnderReview↔WaitingForResponse → {Resolved (only
  via Resolve), Closed, Escalated}; Escalated→{Resolved,Closed}; Resolved and Closed are terminal (no
  re-open). Document the table you implement in a brief code comment ONLY if non-obvious.
- TEST-FIRST: unit tests in Cleansia.Tests for every legal edge (success) and every illegal edge (Failure
  with the new code, status unchanged). State "red→green" in your report.

COORDINATION: T-0174 (chargeback) runs in parallel and also maps Stripe-dispute status → DisputeStatus on
its AC4 — keep your transition table consistent (a Stripe 'lost'→Closed/Escalated and 'won'→Resolved-class
must be LEGAL edges from the linked state). No file overlap with T-0174 (it edits the webhook handler).

${COMMON}
Return: files changed, the transition table implemented, the new error code, test names + red→green, build +
test result, manual_step flags (expect NONE — no schema/DTO change; if you add a Response record, flag
nswag-regen).`,
    { label: 'dev:T-0172', phase: 'Build', agentType: 'backend' },
  ),
  () => agent(
    `You are the BACKEND developer. Implement T-0170a — the FIRST split child of T-0170 (admin order ops):
generalized cancellation + CancelledBy enum (folds AUD-15) + admin-cancel command through the refund seam.
Full parent ticket: agents/backlog/tickets/T-0170-aud-01.md (read it — AC1, AC2, AC3, AC7 are this child;
AC4/AC5/AC6/AC8 are siblings 170b/c/d + frontend, NOT this child).

CURRENT STATE: CancelOrder.Handler (Features/Orders/CancelOrder.cs) gates on order.UserId != userId
(:75-80) so an admin gets OrderNotFound; it hardcodes cancelledBy:"customer" (:123); Policy.CanCancelOrder
is CustomerOnly. Order.Cancel(string cancelledBy) (Domain/Orders/Order.cs:441-454); CancelledBy column is
MaxLength(20) free-text (:136-140). The refund seam IRefundService.IssueRefundAsync is DONE (Wave 2,
Services/Interfaces/IRefundService.cs) — admin cancel-with-refund MUST go through it with the deterministic
RefundKey (RefundReason.CustomerCancellation or a suitable reason — read the seam + RefundReason enum), NEVER
an inline un-keyed Stripe call. CancelOrder was already migrated onto the seam by T-0164 — reuse that path.

DELIVER (this child's ACs):
- AC1 (folds AUD-15): a CancelledBy enum { Customer, Cleaner, Admin, System } in Core.Domain/Enums/;
  Order.Cancel takes CancelledBy instead of a free-text string; the existing customer path passes
  CancelledBy.Customer (replace the literal at CancelOrder.cs:123). Persistence preserves existing values for
  already-cancelled rows (enum-to-string or enum-to-int conversion — if the column mapping changes, flag
  manual_step: ef-migration; prefer storing the enum as a string to keep existing "customer"/"cleaner"/
  "system" rows readable, OR map int with a value-converter — choose and justify; a domain test asserts each
  actor round-trips).
- AC2: a NEW admin-cancel command (Features/Orders/, e.g. AdminCancelOrder) + endpoint on
  Cleansia.Web.Admin/Controllers/AdminOrderController.cs that cancels ANY order (no ownership gate),
  recording CancelledBy.Admin, keeping the terminal-state guards (already Cancelled/Completed/InProgress →
  the existing BusinessErrorMessage codes).
- AC3: admin cancel-with-refund issues the Stripe refund ONLY via IRefundService with the deterministic key;
  a retried admin cancel does NOT double-refund (idempotency test).
- AC7: new Policy constant (e.g. CanAdminCancelOrder) mapped to AdminOnly in PolicyBuilder.Map; update the
  frozen-permission-map snapshot test IN THIS CHANGE (the boot-guard AssertComplete + FrozenPermissionMapTests
  will fail otherwise); a per-permission test proves admin passes, customer/employee denied (403).
- TEST-FIRST: AC1 round-trip, AC2 admin-cancels-non-owned + terminal-guard, AC3 refund-idempotency, AC7
  per-permission. red→green.

OUT OF SCOPE (siblings, do NOT build): status-override (170b), reassign (170c), refund-only (170d), the admin
UI (frontend, held on regen). Note 170d also edits CancelOrder.cs — you are landing first; keep your edits
tight so 170d rebases cleanly.

${COMMON}
This adds an admin endpoint/DTO → flag manual_step: nswag-regen (admin client). Flag manual_step: ef-migration
ONLY if the CancelledBy column mapping needs a schema/conversion change (state which). Return: files changed,
the enum + conversion choice, the new Policy constant + mapping, test names + red→green, build/test result,
manual_step flags.`,
    { label: 'dev:T-0170a', phase: 'Build', agentType: 'backend' },
  ),
  () => agent(
    `You are the BACKEND developer. Implement T-0174 — wire Stripe chargeback linkage (LinkStripeDispute).
Full ticket: agents/backlog/tickets/T-0174-d-06.md (read it).

THE GAP: charge.dispute.* events hit HandlePaymentNotification.cs and fall through the _ => Success
switch-arm (:201-213) → silently ignored. Dispute.LinkStripeDispute (Domain/Disputes/Dispute.cs:104-108) +
the nullable StripeDisputeId column exist but have NO caller. charge.dispute.* events carry charge +
payment_intent but NO order metadata, so ExtractOrderId can't resolve them — a new repo lookup by
payment-intent is required (IOrderRepository has no such method today).

DELIVER (per the ACs):
- AC1: charge.dispute.created → resolve charge.payment_intent → Order.StripePaymentIntentId (Order.cs:103)
  via a NEW IOrderRepository lookup-by-payment-intent (+ OrderRepository impl); if the order has no open
  dispute, create one + call LinkStripeDispute(dp_..., "stripe-webhook") + set Status from Stripe's status.
- AC2: if the order already has an open dispute (GetOpenDisputeForOrderAsync, IDisputeRepository.cs:19), link
  it, do NOT create a second (mirror the DisputeAlreadyExists guard shape, CreateDispute.cs:54-60).
- AC3 (S7): the existing ProcessedStripeEvents idempotency gate (HandlePaymentNotification.cs:144-159) must
  short-circuit a redelivered event — branch charge.dispute.* AFTER the idempotency gate. Add the consts +
  an IsChargebackEvent predicate to StripeEventType (Common/Constants.cs:21-54).
- AC4: charge.dispute.updated/.closed → find the Dispute by StripeDisputeId (add GetByStripeDisputeIdAsync to
  IDisputeRepository/DisputeRepository) and update Status (won→Resolved-class, lost→Closed/Escalated-class)
  WITHOUT creating a new dispute. COORDINATE with T-0172 (running in parallel): the status edges you drive
  (created→linked, updated/closed→won/lost) must be LEGAL in T-0172's transition table. Use the intent-named
  domain methods; do not free-set.
- AC5 (S6): uncorrelated charge (no matching Order) → return Success, log ≤ warning, NO PII / no raw Stripe
  payload above Debug, no dispute written.
- AC6: invalid signature still rejected (regression — the existing path at :128-134).
- Use the tenant-ignoring read (GetByIdIgnoringTenantAsync pattern at :184) then set the tenant override
  (:193-196) before writing. No CommitAsync in the handler.
- TEST-FIRST: handler tests AC1-AC6 (incl. created→updated→closed sequence, redelivery no-op), red→green.

OUT OF SCOPE: issuing refunds, contesting/evidence to Stripe, any UI. This is the producer side only.

${COMMON}
No schema change (StripeDisputeId column exists) and no client DTO surface → expect manual_step: NONE. Return:
files changed, the new repo methods, the Stripe-status→DisputeStatus mapping (coordinate w/ T-0172), test
names + red→green, build/test result.`,
    { label: 'dev:T-0174', phase: 'Build', agentType: 'backend' },
  ),
])

phase('Review')
const reviews = await parallel([
  // T-0172
  () => agent(`REVIEWER for T-0172 (dispute transition-guard). Verify: legal edges succeed + illegal edges
rejected as BusinessResult.Failure with dispute.invalid_status_transition (status unchanged, no exception,
no CommitAsync on reject); Close/Escalate now reachable from production (grep); Resolve stays the sole writer
of resolution fields and UpdateStatus cannot set Resolved; authz unchanged; tests are non-vacuous and were
written test-first (every legal+illegal edge covered). Conventions + comment discipline (no task-number refs).
Run the gate (build + Disputes test filter). Verdict APPROVE/APPROVE-WITH-NITS/REQUEST-CHANGES with file:line.`,
    { label: 'review:T-0172', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for T-0172 (security_touching: lifecycle integrity on a money-bearing entity).
Verify: an illegal state transition on a Dispute is impossible via UpdateStatus (the hole is closed);
UpdateStatus cannot reach Resolved or mutate refund/resolution fields (no second money-path writer);
authorization still Admin-only; no new injection/PII surface. S1-S10 where relevant. Read the real files.
Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:T-0172', phase: 'Review', agentType: 'security' }),
  // T-0170a
  () => agent(`REVIEWER for T-0170a (generalized cancel + admin-cancel via seam). Verify: CancelledBy enum
{Customer,Cleaner,Admin,System}; Order.Cancel takes it; customer path passes Customer; existing cancelled
rows still readable (the conversion choice is sound). Admin-cancel cancels a NON-owned order (no ownership
gate) recording Admin, terminal guards intact. Admin cancel-with-refund goes through IRefundService with the
deterministic key — NO inline un-keyed Stripe call reintroduced; a retry does not double-refund (re-derive
the idempotency path). New Policy constant mapped to AdminOnly + frozen-map snapshot updated in-change +
per-permission test (admin pass, customer/employee 403). Conventions (handler happy-path, validator present,
no CommitAsync, records, comment discipline). Run the gate (build + Orders/refund test filter). Verdict with
file:line.`,
    { label: 'review:T-0170a', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for T-0170a (security_touching: privileged money + lifecycle). Verify: the
admin-cancel endpoint is server-side AdminOnly (mapped + boot-guard satisfied + 403 for non-admin), NOT
client-trusted; the refund is keyed/idempotent (no double payout on retry — trace the RefundKey); no inline
un-keyed Stripe refund survives; confirm-then-record preserved (no phantom Refunded); the ownership-gate
removal is intentional and ONLY on the admin path (the customer CancelOrder still gates on ownership).
S1-S10. Read the real files. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:T-0170a', phase: 'Review', agentType: 'security' }),
  // T-0174
  () => agent(`REVIEWER for T-0174 (chargeback LinkStripeDispute). Verify: charge.dispute.created creates OR
links (never stacks — count stays 1); resolves via payment_intent→StripePaymentIntentId through the NEW repo
lookup; created→updated→closed updates the SAME dispute by StripeDisputeId; the status mapping is consistent
with T-0172's legal transition table (cross-check); uncorrelated charge = Success no-op; branch sits AFTER
the idempotency gate (S7) and after signature verification. Tests non-vacuous + test-first (the branch didn't
exist before). No CommitAsync in handler. Run the gate (build + webhook test filter). Verdict with file:line.`,
    { label: 'review:T-0174', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for T-0174 (security_touching: signed Stripe ingress + new write path).
Verify S7 — redelivered event is a no-op (ProcessedStripeEvents gate short-circuits, the dispute branch is
AFTER it); S6 — no PII / no raw Stripe payload logged above Debug, uncorrelated charge logs ≤ warning;
signature rejection still enforced (AC6); the tenant-ignoring read + tenant-override-before-write is correct
(no cross-tenant write); no second dispute on replay. Read the real files. Verdict PASS/PASS-WITH-NOTES/FAIL
with file:line.`,
    { label: 'security:T-0174', phase: 'Review', agentType: 'security' }),
])

return {
  t0172: { dev: t0172, review: reviews[0], security: reviews[1] },
  t0170a: { dev: t0170a, review: reviews[2], security: reviews[3] },
  t0174: { dev: t0174, review: reviews[4], security: reviews[5] },
}
