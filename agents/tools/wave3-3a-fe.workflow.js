export const meta = {
  name: 'wave3-3a-fe',
  description: 'Wave-3 Batch 3A frontend: T-0170 admin order-ops UI + T-0173b admin dispute-management UI, each dev + reviewer',
  phases: [
    { title: 'Build', detail: 'T-0170 order-ops UI + T-0173b dispute UI in parallel' },
    { title: 'Review', detail: 'a reviewer per lane' },
  ],
}

const FE_RULES = `
FRONTEND RULES (Cleansia admin app, non-negotiable): Angular 19 standalone, OnPush on presentational
components; logic in a FACADE (extends UnsubscribeControlDirective) not the component; signals for state;
takeUntil(destroyed$) cleanup; finalize to reset loading. <cleansia-*>/PrimeNG ONLY — never raw
<button>/<select>/<input>/<form>. Every user-visible string via TranslatePipe with keys in ALL 5 locales
(apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json) with REAL native translations (not English
placeholders). No 'any' — use the NSwag types from '@cleansia/admin-services' (already regenerated). Do NOT
hand-edit the generated client. Three explicit data states (loading / empty-or-ready / error) + OnPush. No
inline templates/styles. Comments: almost none, no task/finding-number refs. Every backend BusinessErrorMessage
code an action can return must have a matching errors.* translation in all 5 locales (contract parity). Run
nx lint + nx test for the touched lib to green (add NO new lint errors; pre-existing baseline is out of scope).
Mirror existing admin-feature facades (inject the AdminClient sub-client, signal state, snackbar on success,
error-code→i18n on failure) — read a sibling facade first.
Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs
live in the ticket status log, never in the report.`

phase('Build')
const [orderUi, disputeUi] = await parallel([
  () => agent(
    `You are the FRONTEND developer (Cleansia admin app). Implement T-0170 AC8 — the admin order-ops UI on the
order-detail screen, wiring the 4 backend actions that just landed. Parent ticket:
agents/backlog/tickets/T-0170-aud-01.md (AC8). The admin client is regenerated and carries:
- adminOrderClient.cancel(AdminCancelOrderCommand) -> AdminCancelOrderResponse
- adminOrderClient.overrideStatus(AdminOverrideOrderStatusCommand) -> AdminOverrideOrderStatusResponse
- adminOrderClient.reassign(AdminReassignOrderCommand) -> AdminReassignOrderResponse
- adminOrderClient.refund(AdminRefundOrderCommand) -> AdminRefundOrderResponse
(in '@cleansia/admin-services'; read the generated command/response field shapes before binding.)

ANCHORS (read + mirror): the admin order-detail feature at
src/Cleansia.App/libs/cleansia-admin-features/order-management/src/lib/order-detail/ (component + facade +
the existing admin-order-refund.* from Wave 2 for the facade/inject/snackbar/error-map pattern). The order
DTO exposes selectedServices / selectedPackages (+ includedServiceItems) and the order status/payment.

DELIVER (AC8):
1. On the admin order-detail screen, surface four actions via <cleansia-*>/PrimeNG (no raw controls):
   - Cancel order (admin cancel — reason optional) -> adminOrderClient.cancel
   - Override status (pick a valid OrderStatus from the 7 values) -> overrideStatus
   - Reassign cleaner (pick from / to employee) -> reassign
   - Issue refund (refund-only, no status change) -> refund
   Each as a clearly-labelled action (button opening a small panel/dialog), logic in a facade.
2. Status DISPLAY must cover ALL 7 OrderStatus values incl. OnTheWay (AUD-14 gap must NOT reappear) — verify
   the status badge/icon map is complete.
3. i18n ×5: action labels, confirmations, success toasts, and EVERY error code the 4 commands return:
   order.invalid_status_transition (NEW — add errors.order.invalid_status_transition ×5), plus the existing
   order.no_available_spots / order.employee_already_assigned / order.employee_not_assigned / employee.not_found
   / order.already_completed / order.already_cancelled / order.already_in_progress / refund.order_not_refundable
   / refund.failed (verify each exists in all 5 locales; add any missing).
4. Three explicit data states + OnPush; success re-loads the order detail so the new status/payment shows.
5. TEST-FIRST: facade spec (each action builds the right typed command, success path, error-code→message,
   loading), then component spec (renders actions/states, OnPush). Keep existing specs green.
6. nx lint + nx test (order-management) to green; no NEW lint errors.

${FE_RULES}
Return: files changed, the 4 actions wired, the i18n keys added ×5, test names + result, lint/test status,
AC8 status (incl. the 7-status display confirmation).`,
    { label: 'dev:T-0170-ui', phase: 'Build', agentType: 'frontend' },
  ),
  () => agent(
    `You are the FRONTEND developer (Cleansia admin app). Implement T-0173b — the admin dispute-management UI.
Parent ticket: agents/backlog/tickets/T-0173-d-01-da-1.md (AC5). The admin client is regenerated and carries
the AdminDisputeClient: get-paged (GetPagedDisputes), details/{disputeId}, resolve(ResolveDisputeCommand),
update-status(UpdateDisputeStatusCommand), add-message (in '@cleansia/admin-services'; read the generated
DTO shapes — DisputeListItem, DisputeDetails, DisputeMessageDto, the commands — before binding).

ANCHORS (read + mirror): an existing admin C-section list feature (the order-management list archetype) for
the list + facade pattern, and the Wave-2 admin-order-refund.facade for inject/snackbar/error-map. There is
NO existing dispute feature in cleansia-admin-features — create a new disputes-management lib/feature
mirroring the order-management structure.

DELIVER (AC5):
1. A disputes LIST (cleansia-table/section, facade + signals, OnPush) over adminDisputeClient.getPaged, and a
   detail/resolution panel over details/{id}: show the dispute + messages, with admin actions:
   - Resolve (with optional RefundAmount + resolution notes) -> resolve. CRITICAL UX HONESTY: the resolve
     panel must NOT imply a guaranteed payout — label it so it reads "issue refund (processed via Stripe)";
     the backend issues the real refund through the seam and may leave it Pending on Stripe failure. (See the
     open question Q-W3-2 in the ticket — do NOT over-promise success in the UI copy.)
   - Update status (legal transitions) -> updateStatus
   - Add staff message -> add-message
2. Every user-visible string via TranslatePipe in ALL 5 locales, incl. EVERY error code the commands return:
   dispute.already_resolved (NEW), dispute.invalid_status_transition (NEW), dispute.not_found, plus any the
   resolve/refund path returns (refund.failed etc.) — add errors.dispute.* ×5 with real native translations.
3. Three explicit data states + OnPush.
4. TEST-FIRST: facade spec (getPaged loads, resolve/updateStatus build the right commands, error-code→message,
   loading) then component spec (list/detail render the 3 states, OnPush).
5. Register the new feature in the admin app routes/sidebar IF that's the established pattern — BUT the
   admin-shell (app.component.ts sidebar + app.routes.ts) is a serialization cluster (T-0173 -> T-0175 ->
   T-0176 -> T-0186). Add exactly ONE disputes sidebar entry + route; keep the edit minimal and additive.
6. nx lint + nx test (the new dispute lib + admin app) to green; no NEW lint errors.

${FE_RULES}
Return: files created, the list+resolution UX, the i18n keys ×5, the sidebar/route entry added, test names +
result, lint/test status, AC5 status. Note explicitly that the resolve UX does not over-promise the refund
(Q-W3-2).`,
    { label: 'dev:T-0173b-ui', phase: 'Build', agentType: 'frontend' },
  ),
])

phase('Review')
const reviews = await parallel([
  () => agent(`REVIEWER for T-0170 admin order-ops UI. Verify AC8: 4 actions (cancel/override/reassign/refund)
via <cleansia-*>/PrimeNG (no raw controls), logic in a facade, OnPush, three states; the status DISPLAY
covers all 7 OrderStatus incl. OnTheWay (AUD-14 not reintroduced); error-contract parity — EVERY backend code
the 4 commands return (esp. the new order.invalid_status_transition) has an errors.* translation in ALL 5
locales (verify all five files, real translations not placeholders); no 'any'; generated client untouched;
comment discipline (no task-number refs). Run the gate (nx lint + nx test order-management; no new lint
errors). Verdict APPROVE/APPROVE-WITH-NITS/REQUEST-CHANGES with file:line.`,
    { label: 'review:T-0170-ui', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`REVIEWER for T-0173b admin dispute UI. Verify AC5: a disputes list + resolution panel via
<cleansia-*>/PrimeNG (no raw controls), facade-held logic, OnPush, three states; the resolve UX does NOT
over-promise the refund (Q-W3-2 — copy must not imply a guaranteed payout); error-contract parity — every
code resolve/updateStatus/add-message return (incl. new dispute.already_resolved +
dispute.invalid_status_transition) has errors.dispute.* in ALL 5 locales (real translations); the admin-shell
edit (sidebar + route) is minimal/additive (one entry); no 'any'; generated client untouched; comment
discipline. Run the gate (nx lint + nx test on the new lib + admin app; no new lint errors). Verdict with
file:line.`,
    { label: 'review:T-0173b-ui', phase: 'Review', agentType: 'reviewer' }),
])

return {
  t0170_ui: { dev: orderUi, review: reviews[0] },
  t0173b_ui: { dev: disputeUi, review: reviews[1] },
}
