export const meta = {
  name: 'wave3-3a-2',
  description: 'Wave-3 Batch 3A wave-2: Lane A = T-0170 b->c->d admin order ops (one dev, serialized shared files) + Lane B = T-0173a admin dispute backend; each with reviewer + security',
  phases: [
    { title: 'Build', detail: 'Lane A (170b/c/d serial) + Lane B (173a) in parallel' },
    { title: 'Review', detail: 'reviewer + security per lane' },
  ],
}

const COMMON = `
PROJECT RULES (non-negotiable): CQRS/MediatR one-file feature (Command+Handler+Validator+Response); handler
HAPPY-PATH only (validation in a FluentValidation Validator w/ Cascade.Stop; every *Command needs a
Validator); NEVER CommitAsync in a handler; return BusinessResult<T>; Error(field, BusinessErrorMessage.X);
positional record DTOs. New Policy.* constant => map it to AdminOnly in PolicyBuilder.Map AND add the row to
the frozen-permission-map snapshot test (FrozenPermissionMapTests) IN THIS CHANGE — the boot-guard
AssertComplete + the snapshot test fail otherwise. Refunds go ONLY through IRefundService.IssueRefundAsync
with the deterministic RefundKey (no inline Stripe). Dispute status writes go THROUGH the T-0172 guard
(Dispute.CanTransitionTo / the intent methods) — never free-set; a terminal dispute (Resolved/Closed, see
Dispute.IsTerminal) is never overwritten. TEST-FIRST (red->green) for money, authz, and state transitions:
write failing tests first, confirm red, implement. Comment discipline: almost none; NO task/finding-number
refs in source (no // T-0170, // AC2, // DA-1); keep only load-bearing ADR-NNNN / S-rule refs. Do NOT run
dotnet ef / npm generate — flag manual_step. Build src/Cleansia.Api.sln + run src/Cleansia.Tests green
(single-threaded if needed: the IntegrationFailureMetricsTests meter-listener flake is unrelated). Backend
only — frontend (admin UI) is held for a later regen.
Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs
live in the ticket status log, never in the report.
`

phase('Build')
const [laneA, laneB] = await parallel([
  () => agent(
    `You are the BACKEND developer for LANE A — the remaining three T-0170 admin-order-ops children, built
SERIALLY in this one agent (they share AdminOrderController.cs / Policy.cs / Order.cs / CancelOrder.cs, so a
single sequential pass avoids file races). Parent ticket: agents/backlog/tickets/T-0170-aud-01.md (read it —
AC4=170b, AC5=170c, AC6=170d; AC1/AC2/AC3/AC7 already landed in T-0170a). T-0170a is DONE: CancelledBy enum,
AdminCancelOrder command + POST api/AdminOrder/cancel, Policy.CanAdminCancelOrder->AdminOnly, the refund seam
path. Build ON TOP of it.

Implement in this order, each test-first (red->green), each its own command file:

T-0170b — ADMIN STATUS-OVERRIDE (AC4):
- A new AdminOverrideOrderStatus command + POST endpoint on AdminOrderController. Admin sets a valid
  OrderStatus (Enums/OrderStatus.cs, 7 values incl. OnTheWay) on a non-terminal order; append an
  OrderStatusTrack via order.AddOrderStatus (Order.cs:320-325); restrict to allowed transitions (an
  illegal/ambiguous override returns a documented BusinessErrorMessage code, never corrupts history);
  attribute the change to the admin. New Policy.CanOverrideOrderStatus -> AdminOnly + frozen-snapshot row.
- Tests: admin overrides Confirmed->OnTheWay => Success; an illegal override => the documented error;
  per-permission (admin pass, customer/employee 403).

T-0170c — ADMIN REASSIGN (AC5):
- A new AdminReassignOrder command + POST endpoint. Add an un-assign/reassign domain method to Order (none
  exists today; assignment is OrderEmployee via _assignedEmployees / AddAssignedEmployee, Order.cs:227-228,
  394-403; respect MaxEmployees / AvailableSpots, :94-96,420-429). Reassign to a target employee, attributed
  to the admin. New Policy.CanReassignOrder -> AdminOnly + frozen-snapshot row.
- Tests: reassign to an available cleaner => Success; reassign exceeding MaxEmployees => the existing
  "no available spots" business error (not an unhandled exception); per-permission.

T-0170d — ADMIN REFUND-ONLY (AC6):
- A new AdminRefundOrder command + POST endpoint: issue a Stripe refund through IRefundService.IssueRefundAsync
  with the deterministic key (read the seam + RefundReason), PaymentStatus transitions per the seam, and the
  order's LIFECYCLE STATUS UNCHANGED. This edits the same money path T-0170a touched — keep it consistent with
  AdminCancelOrder (reuse the seam, no inline Stripe). New Policy.CanRefundOrder -> AdminOnly + frozen-snapshot
  row.
- Tests: refund-only on a Confirmed paid order leaves status Confirmed + PaymentStatus Refunded/PartiallyRefunded;
  a retry is idempotent (no second Stripe refund); per-permission.

All three endpoints AdminOnly, server-side. ${COMMON}
This adds admin endpoints/DTOs => flag manual_step: nswag-regen (admin client) for the 3A frontend (T-0170 UI).
Out of scope: the admin UI (AC8 frontend). Return: per-child files changed, the new domain method (reassign),
the 3 new Policy constants + mappings + snapshot rows, test names + red->green per child, build/test result,
manual_step flags.`,
    { label: 'dev:laneA-170bcd', phase: 'Build', agentType: 'backend' },
  ),
  () => agent(
    `You are the BACKEND developer for LANE B — T-0173a, the backend half of admin dispute management. Full
ticket: agents/backlog/tickets/T-0173-d-01-da-1.md (read it — AC1/AC2/AC3/AC4/AC6 are this backend slice;
AC5 admin UI is the held 173b frontend slice). T-0172 (dispute transition-guard) is DONE — consume its
Dispute.CanTransitionTo guard; T-0161/T-0164 (refund seam + migration) are DONE — issue dispute refunds ONLY
through IRefundService. T-0174 (chargeback) is DONE and also edits ResolveDispute-adjacent code — DO NOT
touch the chargeback path; coordinate by leaving HandlePaymentNotification.cs alone.

DELIVER (the backend ACs):
- AC1: a NEW Cleansia.Web.Admin/Controllers/DisputeController (mirror the AdminOrderController archetype):
  GetPaged / GetById / Resolve / UpdateStatus / AddMessage-as-staff. Each gated by its existing Policy.*
  (CanViewDisputeList, CanViewDispute, CanResolveDispute, CanUpdateDisputeStatus — all already exist;
  staff AddMessage per the existing SEC-DSP-01 split). If CanViewDisputeList is unmapped in PolicyBuilder,
  add the AdminOnly mapping + frozen-snapshot row.
- AC2: REMOVE the dead Partner-host endpoints — Resolve + UpdateStatus from
  Cleansia.Web.Partner/Controllers/DisputeController.cs (the admin-policied actions on a partner host,
  SEC-DSP-07) and the duplicated customer-policied Create/GetById/GetPaged there. A test asserts those routes
  no longer exist on the Partner API.
- AC3: ResolveDispute.Handler issues a REAL idempotent refund through IRefundService with the deterministic
  key (today it only records RefundAmount via dispute.Resolve, makes NO Stripe call — close SEC-DSP-06). A
  retried Resolve does NOT double-refund (idempotency test). Confirm-then-record per the seam.
- AC4: status transitions go through the T-0172 guard (Dispute.CanTransitionTo / intent methods) — illegal
  jumps (e.g. re-resolving a Resolved dispute and overwriting its refund) rejected as BusinessResult.Failure.
- AC6: any refund-success notification dispatches via IPendingDispatch (ADR-0002), NOT a direct
  IQueueClient.SendAsync; no PII regression on the new admin surface.
- TEST-FIRST: AC3 refund money-math + idempotency, AC1 cross-actor authz (non-admin denied), AC2
  Partner-route-gone assertion, AC4 transition-guard. red->green.

${COMMON}
This adds the Admin DisputeController + DTOs => flag manual_step: nswag-regen (admin client) for the held
173b frontend. Out of scope: the admin disputes UI (173b), the chargeback path (T-0174), the customer
dispute UI. Return: files changed, the removed Partner routes, the seam-refund wiring, test names +
red->green, build/test result, manual_step flags. NOTE the shared files with Lane A: Policy.cs /
PolicyBuilder.cs / FrozenPermissionMapTests.cs — ADD ONLY your distinct rows (don't touch Lane A's order-op
constants); the orchestrator does a combined-tree build to catch any merge issue.`,
    { label: 'dev:laneB-173a', phase: 'Build', agentType: 'backend' },
  ),
])

phase('Review')
const reviews = await parallel([
  () => agent(`REVIEWER for Lane A (T-0170 b/c/d admin order ops). Verify per child: 170b status-override
appends OrderStatusTrack + rejects illegal transitions with a documented code; 170c adds a real
un-assign/reassign domain method respecting MaxEmployees + surfaces the no-spots business error (no unhandled
exception); 170d refund-only goes through IRefundService with the deterministic key, leaves lifecycle status
unchanged, retry-idempotent (re-derive the key path). All three: new Policy.* -> AdminOnly + frozen-map
snapshot row added in-change + per-permission test (admin pass, customer/employee 403). Conventions (handler
happy-path, validator present, no CommitAsync, records, comment discipline — no task-number refs). Run the
gate (build + Orders filter). Verdict APPROVE/APPROVE-WITH-NITS/REQUEST-CHANGES with file:line per child.`,
    { label: 'review:laneA', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for Lane A (security_touching: privileged money + lifecycle). Verify all
three endpoints are server-side AdminOnly (mapped + boot-guard + 403 for non-admin); 170d refund-only is
keyed/idempotent (no double payout, no inline un-keyed Stripe, confirm-then-record / no phantom Refunded);
170b status-override cannot corrupt history (guarded transitions); 170c reassign respects assignment
invariants and is admin-attributed. S1-S10. Read the real files. Verdict PASS/PASS-WITH-NOTES/FAIL w/ file:line.`,
    { label: 'security:laneA', phase: 'Review', agentType: 'security' }),
  () => agent(`REVIEWER for Lane B (T-0173a admin dispute backend). Verify: a real Admin DisputeController
exists (GetPaged/GetById/Resolve/UpdateStatus/staff-AddMessage), each gated by the correct existing Policy.*;
the dead Partner Resolve/UpdateStatus + duplicated customer endpoints are REMOVED with a route-gone test;
ResolveDispute now issues a REAL idempotent refund through IRefundService (today it makes no Stripe call) —
retry does not double-refund (re-derive); status transitions go through the T-0172 guard (illegal rejected);
refund-success notification via IPendingDispatch not a direct SendAsync. Conventions + comment discipline.
Run the gate (build + Disputes filter). Verdict with file:line.`,
    { label: 'review:laneB', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for Lane B (security_touching: SEC-DSP-06 refund + SEC-DSP-07 host placement
+ cross-actor authz). Verify: the Admin dispute actions are AdminOnly server-side and a non-admin is denied
(403/NotFound, never 200); NO admin-policied action remains on the Partner host (SEC-DSP-07 closed); the
dispute refund is keyed/idempotent through the seam (SEC-DSP-06 closed — no double-refund, confirm-then-record);
no PII leak on the new admin DTOs beyond what SEC-DSP-03/08 allow; the staff AddMessage derives the staff flag
from the caller role, not the body (SEC-DSP-01). S1-S10. Read the real files. Verdict PASS/PASS-WITH-NOTES/FAIL
w/ file:line.`,
    { label: 'security:laneB', phase: 'Review', agentType: 'security' }),
])

return {
  laneA_170bcd: { dev: laneA, review: reviews[0], security: reviews[1] },
  laneB_173a: { dev: laneB, review: reviews[2], security: reviews[3] },
}
