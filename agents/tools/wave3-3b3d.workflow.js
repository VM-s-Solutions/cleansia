export const meta = {
  name: 'wave3-3b3d',
  description: 'Wave-3 Batches 3B (payroll backend T-0171 a/b/c) + 3D (Functions resilience T-0181..T-0185), parallel; each dev + reviewer, security on the money/mass-effect tickets',
  phases: [
    { title: 'Build', detail: '3B payroll backend + 5 Functions-resilience tickets in parallel' },
    { title: 'Review', detail: 'reviewer per ticket; security on T-0171/T-0181/T-0182' },
  ],
}

const COMMON = `
PROJECT RULES (non-negotiable, from CLAUDE.md + agents/knowledge): CQRS/MediatR one-file feature
(Command+Handler+Validator+Response); handler HAPPY-PATH only (validation in a FluentValidation Validator
with Cascade.Stop; every *Command needs a Validator); NEVER CommitAsync in a handler (UnitOfWork pipeline
commits on BusinessResult{IsSuccess:true}); return BusinessResult<T>; Error(field, BusinessErrorMessage.X)
dot-notation; positional record DTOs. New Policy.* constant => map to AdminOnly in PolicyBuilder.Map AND add
the frozen-permission-map snapshot row (FrozenPermissionMapTests) IN THIS CHANGE (boot-guard AssertComplete
+ snapshot fail otherwise). Side effects (PDF/email/push) dispatch POST-COMMIT via the outbox/IPendingDispatch
(ADR-0002), never inline on a money path. Idempotency S7a/S7b: deterministic keys (never Guid/timestamp),
claim-before-act, caught 23505. Functions failure-classification (ADR-0002 D3.3 / ADR-0005): permanent/
malformed => ack (do not retry-storm); transient => throw to retry. TEST-FIRST (red->green) is MANDATORY for
money, authz, idempotency, and state transitions — write failing tests first, confirm red, implement.
Comment discipline: almost none; NO task/finding-number refs in source (no // T-0171, // AUD-04, // AC3);
keep only load-bearing ADR-NNNN / S-rule refs. Do NOT run dotnet ef (owner-only) — flag manual_step:
ef-migration if schema changes. Do NOT run npm generate / hand-edit NSwag clients — flag manual_step:
nswag-regen if a client surface changes. Build src/Cleansia.Api.sln + run src/Cleansia.Tests green
(single-threaded if needed: the IntegrationFailureMetricsTests meter-listener flake is unrelated). Backend
only — frontend/Android consumers are held for a later regen.
Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs
live in the ticket status log, never in the report.
`

phase('Build')
const [t0171, t0181, t0182, t0183, t0184, t0185] = await parallel([
  // ===== 3B: T-0171 backend (a/b/c serialized in ONE dev — shared EmployeePayrollController.cs / Policy.cs) =====
  () => agent(
    `You are the BACKEND developer for Batch 3B — T-0171 payroll settlement lifecycle, the BACKEND children
(171a + 171b + 171c) built SERIALLY in this one agent (they share EmployeePayrollController.cs across hosts +
Policy.cs/PolicyBuilder.cs, so a single sequential pass avoids file races). Full ticket:
agents/backlog/tickets/T-0171-aud-02-04.md (AC1-AC6 + AC9 are these backend children; AC7 admin UI / AC8
partner-read are the held frontend/Android children 171d/e — NOT here).

The DOMAIN methods already exist and already guard Paid — REUSE them, do not duplicate guards in handlers:
EmployeeInvoice.Dispute/Reject/UpdateAmounts (EmployeeInvoice.cs:230/243/256), PayPeriod.MarkAsPaid/Reopen
(PayPeriod.cs:124/137). T-0100 (closed PolicyBuilder) + T-0143 (outbox) are done.

171a — invoice adjustment + dispute/reject (AC1/AC2):
- New Admin-host commands UpdateInvoiceAmounts (bonus/deduction, recompute via UpdateAmounts, clamp >=0;
  Paid => BusinessResult error), DisputeInvoice, RejectInvoice (status -> Disputed/Rejected + AdminNotes;
  Paid => error). On the Admin host (Cleansia.Web.Admin/Controllers/AdminPayPeriodController.cs or a new
  AdminPayrollController — mirror the AdminOrderController archetype, kebab-case routes,
  [EnableRateLimiting("auth")] on the mutations). Tests: each transition + the guarded Paid case.

171b — pay-period MarkPaid + Reopen (AC3):
- New Admin-host commands MarkPayPeriodPaid (Closed -> Paid + PaidAt; Open => error) and ReopenPayPeriod
  (non-Paid -> Open, clear ClosedAt/ClosedBy; Paid cannot reopen). Tests prove the guards.

171c — AUD-04 partner-surface reconciliation (AC5) — SECURITY-CRITICAL (was the payroll-fraud chain):
- REMOVE the settlement write endpoints from the Partner + Mobile.Partner hosts: GenerateInvoice/
  ApproveInvoice/MarkInvoicePaid/CancelInvoice/ClosePayPeriod (currently
  Cleansia.Web.Partner/Controllers/EmployeePayrollController.cs:67,79,91,103,115 + the Mobile.Partner
  equivalent). Move/keep them admin-only on the Admin host (the wired Generate/Approve/MarkPaid/Cancel/Close
  commands already exist — re-home the endpoints, don't rewrite the commands).
- Keep/confirm a READ-ONLY "my period pay" query on the Partner/Mobile.Partner host scoped to the caller's
  own EmployeeId resolved FROM SESSION (IUserSessionProvider), NEVER the request body (same shape as
  SEC-EMP-01). Cross-user/cross-host rejection test (TC-AUTHZ harness).

AC6: every new Policy.* const -> AdminOnly + frozen-map snapshot row in-change.
AC9: any settlement side effect (PDF regenerate / notification) dispatches via the outbox post-commit
(ADR-0002) — handler never CommitAsync.

${COMMON}
manual_step: nswag-regen (new admin endpoints + removed/changed partner endpoints) for the held 171d/e
consumers. manual_step: ef-migration ONLY if you add new persisted adjustment-audit columns (the AdminNotes
column already exists on EmployeeInvoice — prefer reusing it; flag ef-migration only if a genuinely new
column is needed, and say which). Return: per-child files changed, new Policy consts + mappings + snapshot
rows, the removed partner routes + the read-only my-pay query, test names + red->green, build/test result,
manual_step flags. NOTE: T-0180 (revive GenerateInvoiceFunction) depends on you — keep the GenerateInvoice
command/owner step intact.`,
    { label: 'dev:T-0171-be', phase: 'Build', agentType: 'backend' },
  ),
  // ===== 3D: T-0181 SendSitewidePromo cursor + campaign idempotency (security) =====
  () => agent(
    `You are the BACKEND developer. Implement T-0181 — SendSitewidePromo resume cursor + campaign idempotency.
Full ticket: agents/backlog/tickets/T-0181-f8-lg-sec-09.md (read it).
- AC1: add a deterministic CampaignId to SendSitewidePromoMessage
  (Cleansia.Core.Queue.Abstractions/Messages/SendSitewidePromoMessage.cs) — derived from the campaign's
  domain inputs (NOT a fresh Guid per send). Producer SendSitewidePromo.Handler (SendSitewidePromo.cs:69-89)
  stamps it. TC-KEY-0 shape test: same inputs => same CampaignId.
- AC2: a double-submitted admin action does NOT start a second fan-out — short-circuit idempotently on the
  CampaignId (return BusinessResult.Success, enqueue nothing). Handler test proves the 2nd call enqueues 0.
- AC3: per-campaign RESUME cursor in SendSitewidePromoFanoutFunction — on redelivery resume from the last
  persisted cursor (last processed UserId, consistent with the .OrderBy(x=>x.UserId) stable order) instead of
  offset=0. Test: simulated mid-campaign failure + redelivery resumes, does not restart.
- DO NOT remove/relax the downstream push:{UserId}:{EventKey} dedup (D2.2) — it stays the effect guard.
This is security_touching (mass side-effect — double fan-out = the whole base double-pushed). ${COMMON}
Return: files changed, the CampaignId derivation, the cursor mechanism, test names + red->green, build/test,
manual_step flags (likely none — internal queue message + Function).`,
    { label: 'dev:T-0181', phase: 'Build', agentType: 'backend' },
  ),
  // ===== 3D: T-0182 idempotent push dispatch (security) =====
  () => agent(
    `You are the BACKEND developer. Implement T-0182 — idempotent push dispatch (guard-first consumer).
Full ticket: agents/backlog/tickets/T-0182-f7-blind-8.md (read it).
- AC1: the push message carries the DETERMINISTIC MessageKey push:{UserId}:{EventKey}:{OrderId?}
  (ADR-0002 D2.1 — not a fresh Guid). Test: same inputs => same key.
- AC2: guard-first — in SendPushNotificationFunction, BEFORE pushDispatcher.SendAsync, compute the MessageKey
  and claim it via the canonical IIdempotencyGuard.AlreadyProcessed(messageKey) (ProcessedMessage unique
  row, committed in its OWN transaction, UNCONDITIONALLY — not gated behind the dead-token prune at :100-108).
  Claim hits the unique index => effect already ran => ack + return, no push.
- AC3 (TC-IDEMP-0): same QueueEnvelope delivered twice => SendAsync invoked EXACTLY once; 2nd run
  short-circuits on the claim. Assert at-most-once-after-marker.
- AC4: failure classification (ADR-0002 D3.3) — malformed/business-rejected body (deserialize failure at
  :42, missing UserId/EventKey) => ACK (do not retry-storm); transient (FCM/infra) => throw to retry. Fix the
  throw-on-everything at :115-121.
This is security_touching. ${COMMON}
Return: files changed, the MessageKey formula, the guard-first ordering, the classification split, test names
+ red->green, build/test, manual_step flags (likely none).`,
    { label: 'dev:T-0182', phase: 'Build', agentType: 'backend' },
  ),
  // ===== 3D: T-0183 cron cadence (config-only) =====
  () => agent(
    `You are the BACKEND developer. Implement T-0183 — Functions cron cadence via app-settings.
Full ticket: agents/backlog/tickets/T-0183-f5.md (read it). Make 4 timer Functions read their cron from an
app-setting via %AppSetting% TimerTrigger syntax (promotion is config-only), with the documented production
defaults supplied in config:
- MaterializeRecurringBookingsFunction => 02:00 UTC daily (0 0 2 * * *)
- SendRecurringOrderRemindersFunction => 02:30 UTC daily (0 30 2 * * *), strictly after Materialize
- SendMembershipLifecycleNotificationsFunction => 03:00 UTC daily (0 0 3 * * *)
- SendNewJobsDigestTimerFunction => every 30 min (0 0,30 * * * *)
AC5: each [TimerTrigger("%SomeCron%")] reads from config; production defaults in config; dev override path
documented. AC6: a test asserts each function's effective schedule equals its documented cadence AND
Materialize fires strictly before Reminder (test written FIRST). AC7: the per-entity idempotency stamps
(RecurringReminderSentAt etc.) stay the dedup mechanism — do NOT remove/weaken any idempotency.
${COMMON} Config defaults go in the Functions host settings (NOT real secrets). Return: files changed, the
4 %AppSetting% bindings + their config defaults, the schedule-assertion test, build/test, manual_step (none).`,
    { label: 'dev:T-0183', phase: 'Build', agentType: 'backend' },
  ),
  // ===== 3D: T-0184 FiscalRetry per-receipt durability =====
  () => agent(
    `You are the BACKEND developer. Implement T-0184 — FiscalRetryService per-receipt durability.
Full ticket: agents/backlog/tickets/T-0184-f6.md (read it).
- AC1: replace the single batch-wide CommitAsync at FiscalRetryService.cs:91 with per-receipt (or
  bounded-chunk) commits inside the loop, so one receipt's persistence fault does NOT roll back the
  already-processed receipts' retry-tracking; the loop continues / the remainder stays due. Test with a
  faulting IUnitOfWork proves no batch-wide loss.
- AC2 (S7): a BlockingOnline receipt whose held email is released must NOT be re-sent after a commit failure —
  commit the EmailSent marker before/atomically-with the release (claim-first, ADR-0002 D2.2). Test asserts
  IEmailService.SendOrderReceiptEmailAsync fires AT MOST ONCE per receipt across two consecutive ticks.
- AC3: bounded run preserved (BatchSize=50 cap at :22,29 unchanged); multi-tenant override still set/cleared
  per receipt (:44-48).
- AC4: a new xUnit test (Cleansia.Tests / the fiscal target) exercises AC1-AC2 with a faulting IUnitOfWork,
  written first (red->green).
${COMMON} Return: files changed, the per-receipt commit cadence, the claim-first email ordering, test names
+ red->green, build/test, manual_step (none).`,
    { label: 'dev:T-0184', phase: 'Build', agentType: 'backend' },
  ),
  // ===== 3D: T-0185 Mapbox 429/transient classification =====
  () => agent(
    `You are the BACKEND developer. Implement T-0185 — Mapbox 429/transient classification + Retry-After.
Full ticket: agents/backlog/tickets/T-0185-blind-7.md (read it). MapboxGeocodingService.cs (T-0145 done, file
free).
- AC1: HTTP 429 is classified TRANSIENT, NOT collapsed into the "address not found -> return null" Warning
  path; logged distinctly (distinct level/event). Unit test (mocked HttpMessageHandler) asserts transient
  classification + distinct log, not the genuine-miss Warning.
- AC2: 503 and timeout (5s HttpClient timeout -> TaskCanceledException) treated transient too. Tests cover both.
- AC3: a 429 with Retry-After header => the resilience handling respects Retry-After (ADR-0004 AC5 / ADR-0005
  resilience). Test asserts Retry-After drives the wait/decision.
- AC4: genuine miss (200 + empty features, :56-63) still returns null + the existing Warning (no retry). Pin it.
- AC5: happy path unchanged — valid feature => new GeoCoordinates(coords[1], coords[0]) (lon/lat order, :65-66).
- AC6: caller AddressGeocoder.PopulateCoordinatesAsync stays best-effort — never throws into the order path;
  the transient degrade is visible in logs/metrics. Test asserts the caller is not made to throw.
Use the ADR-0005 IHttpClientFactory + Polly resilience pattern already in the codebase (the integration ADR
T-0141/0145 are done) — classify via IntegrationFailureClass if that's the established taxonomy. ${COMMON}
Return: files changed, the classification + Retry-After handling, test names + red->green, build/test,
manual_step (none).`,
    { label: 'dev:T-0185', phase: 'Build', agentType: 'backend' },
  ),
])

phase('Review')
const reviews = await parallel([
  () => agent(`REVIEWER for T-0171 backend (171a/b/c payroll settlement). Verify: AC1 adjustment recompute +
Paid-guard; AC2 dispute/reject + Paid-guard; AC3 period MarkPaid/Reopen guards; AC5 the settlement write
endpoints are REMOVED from Partner + Mobile.Partner hosts (route-gone) and the read-only my-pay query scopes
EmployeeId from SESSION not body; AC6 new Policy.* -> AdminOnly + frozen-map snapshot in-change; AC9 side
effects post-commit (no CommitAsync in handlers). Domain guards reused not duplicated. Conventions + comment
discipline. Run the gate (build + Payroll/PayPeriod filters). Verdict APPROVE/APPROVE-WITH-NITS/
REQUEST-CHANGES with file:line per child.`,
    { label: 'review:T-0171', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for T-0171 (security_touching: AUD-04 was the payroll-fraud chain). Verify
the settlement write endpoints are GONE from the Partner/Mobile.Partner hosts (a cleaner cannot reach
Generate/Approve/MarkPaid/Cancel/Close); the admin equivalents are AdminOnly server-side (mapped + boot-guard
+ 403 non-admin); the read-only my-pay query resolves EmployeeId from session (no IDOR via body, SEC-EMP-01
shape); no settlement side effect runs inline on the money path (post-commit). S1-S10. Read the real files.
Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:T-0171', phase: 'Review', agentType: 'security' }),
  () => agent(`REVIEWER for T-0181 (promo cursor + campaign idempotency). Verify CampaignId is deterministic
(not Guid-per-send); a double-submit enqueues 0 (idempotent short-circuit); the Fanout resumes from the
persisted cursor on redelivery (not offset=0); the downstream push dedup is NOT removed. Tests non-vacuous +
test-first. Run the gate. Verdict with file:line.`,
    { label: 'review:T-0181', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for T-0181 (security_touching: mass push fan-out). Verify a double-submitted
admin action cannot double-push the entire opted-in base (CampaignId idempotency holds end-to-end); the resume
cursor cannot skip or re-push users en masse; the push:{UserId}:{EventKey} D2.2 dedup is intact. S7. Read the
real files. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:T-0181', phase: 'Review', agentType: 'security' }),
  () => agent(`REVIEWER for T-0182 (idempotent push dispatch). Verify the deterministic MessageKey
push:{UserId}:{EventKey}:{OrderId?}; guard-first claim via IIdempotencyGuard BEFORE SendAsync, committed
unconditionally in its own transaction (not gated behind the dead-token prune); twice-delivered => SendAsync
exactly once; failure classification (malformed => ack, transient => throw). Tests non-vacuous (TC-IDEMP-0).
Run the gate. Verdict with file:line.`,
    { label: 'review:T-0182', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for T-0182 (security_touching: notification side-effect idempotency). Verify
S7 at-most-once-after-marker (claim-then-act, own transaction); a redelivery sends no second push; a malformed
body is acked (no retry-storm) and logs no PII/raw payload above Debug (S6); transient throws to retry. Read
the real files. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:T-0182', phase: 'Review', agentType: 'security' }),
  () => agent(`REVIEWER for T-0183 (cron cadence config). Verify all 4 timers use %AppSetting% TimerTrigger
syntax with the documented production cron defaults in config; the test asserts each effective schedule +
Materialize-before-Reminder (test-first); NO idempotency stamp removed/weakened (AC7); dev-override path
documented. Run the gate. Verdict with file:line.`,
    { label: 'review:T-0183', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`REVIEWER for T-0184 (FiscalRetry durability). Verify the batch-wide CommitAsync is replaced by
per-receipt/bounded-chunk commits (one fault doesn't roll back processed receipts); the EmailSent marker is
committed before/atomically-with the email release so a commit fault doesn't re-send (claim-first, S7); the
BatchSize=50 cap + per-receipt tenant override preserved; the faulting-IUnitOfWork test proves no batch loss +
at-most-once email across two ticks (test-first). Run the gate. Verdict with file:line.`,
    { label: 'review:T-0184', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`REVIEWER for T-0185 (Mapbox 429/transient). Verify 429/503/timeout classified TRANSIENT and
distinct from the genuine-miss Warning; Retry-After honored on 429; genuine miss (200 empty) still returns
null + Warning (no retry); happy path coordinate parse unchanged (lon/lat order); the caller stays best-effort
(never throws into the order path). Uses the ADR-0005 resilience pattern. Tests cover each branch + pin the
unchanged contracts. Run the gate. Verdict with file:line.`,
    { label: 'review:T-0185', phase: 'Review', agentType: 'reviewer' }),
])

return {
  t0171_be: { dev: t0171, review: reviews[0], security: reviews[1] },
  t0181: { dev: t0181, review: reviews[2], security: reviews[3] },
  t0182: { dev: t0182, review: reviews[4], security: reviews[5] },
  t0183: { dev: t0183, review: reviews[6] },
  t0184: { dev: t0184, review: reviews[7] },
  t0185: { dev: t0185, review: reviews[8] },
}
