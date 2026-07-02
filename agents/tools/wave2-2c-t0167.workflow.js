export const meta = {
  name: 'wave2-2c-t0167',
  description: 'Batch 2C backend: T-0167 admin partial-refund command (allocator + RefundPolicy + AdminOnly + summary), TDD + reviewer + security',
  phases: [
    { title: 'Build', detail: 'backend dev implements T-0167 test-first' },
    { title: 'Review', detail: 'reviewer + security audit in parallel' },
  ],
}

const TICKET = `T-0167 (AUD-01c1): Admin partial-refund command + share-of-TotalPrice allocator + RefundPolicy (window/fee) + PartiallyRefunded summary. BACKEND ONLY (the admin UX is the sibling T-0168, do not touch frontend).`

const ANCHORS = `
REAL CODE ANCHORS (cite these exact signatures — do not invent):

1. THE REFUND SEAM (already done, T-0161 — call it, do NOT modify it):
   IRefundService.IssueRefundAsync(RefundRequest, ct) -> Task<BusinessResult<RefundResult>>
   in src/Cleansia.Core.AppServices/Services/Interfaces/IRefundService.cs
   RefundRequest(string OrderId, decimal Amount, RefundReason Reason, string ActorId,
                 string? DisputeId = null, string? RefundRequestId = null)
   RefundResult(string RefundId, string RefundKey, decimal Amount, RefundStatus Status, bool ResolvedToExisting)
   The seam clamps to the refundable ceiling + handles idempotency + confirm-then-record. The COMMAND
   computes the policy-correct Amount (window + fee-bearer already applied) and passes it in. For an
   admin refund, set RefundRequestId so the RefundKey purpose is admin:{RefundRequestId} — pass a
   deterministic id (e.g. the order line selection identity), NEVER Guid.NewGuid()/timestamp.

2. THE ALLOCATOR HELPER (already done, T-0231 — reuse it for the bundled-line path, AC7):
   PackagePricing.DeriveIncludedServiceGrosses(IReadOnlyList<decimal> priceWeights, decimal packageLineGross)
     -> IReadOnlyList<decimal>   (each rounded to 2dp, last absorbs residual, sums exactly to gross)
   in src/Cleansia.Core.AppServices/Features/Packages/PackagePricing.cs
   PackageService.PriceWeight (decimal, default 1m) in src/Cleansia.Core.Domain/Packages/PackageService.cs

3. SIBLING POLICY PATTERN (mirror this, do NOT put the window in IRefundService):
   BookingPolicy (static class of business-rule constants + pure methods) in
   src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs
   -> Create RefundPolicy as a sibling static class in the SAME Features/Orders or a Features/Refunds folder.

4. LOYALTY CLAWBACK (already done, T-0163 — CALL it if a partial refund succeeds):
   ILoyaltyService.RevokeForPartialRefundAsync(string orderId, decimal refundNet, string refundKey,
       string actorId, CancellationToken ct)  (idempotent per refundKey, capped, no-op on null user)
   in src/Cleansia.Core.AppServices/Services/Interfaces/ILoyaltyService.cs
   Mirror how CancelOrder.Handler calls loyaltyService + refundService (src/.../Features/Orders/CancelOrder.cs).

5. PERMISSION (AC6 — ADR-0001 D2, the boot-guard is REAL and will fail the host if you miss a step):
   Policy.* constants in src/Cleansia.Core.AppServices/Authentication/Policy.cs
   PhysicalPolicy.AdminOnly exists. PolicyBuilder.AssertComplete() (PolicyBuilder.cs) runs at host
   boot via AuthorizationCompletenessStartupFilter and THROWS if any Policy.* constant is not mapped.
   => You MUST: (a) add a new Policy constant (e.g. CanIssueRefund), (b) map it to AdminOnly in
   PolicyBuilder, (c) put the [Authorize(Policy = Policy.CanIssueRefund)] (or the project's existing
   attribute idiom — check a sibling admin command/controller) on the endpoint — ALL in this change.
   Read PolicyBuilder.cs first and follow its exact mapping idiom + the frozen-snapshot update it asserts.

6. ENUMS (already done): RefundReason {CustomerCancellation, DisputeResolution, AdminDiscretion,
   ServiceNotRendered}; PaymentStatus.PartiallyRefunded = 6; RefundStatus {Pending,Succeeded,Failed}.
   Order.TotalPrice is the FROZEN gross (discount + express already embedded — NEVER re-apply them).
   Order.CompletedAt / Order.AppliedVatRate (nullable) — read, do not change. Order has Currency.Code.
`

const RULES = `
PROJECT RULES (from CLAUDE.md + agents/knowledge — non-negotiable):
- CQRS/MediatR: Command + Handler + Validator + Response in one feature file. Handler is HAPPY-PATH ONLY
  (no validation, no error-checking in the handler — all validation in a FluentValidation Validator with
  Cascade.Stop). Every type named *Command MUST have a Validator or the ValidationPipelineBehavior throws
  at runtime (this is the GDPR-bug class we already hit — do not repeat it). Add an empty validator if
  there's genuinely nothing to validate, but here there IS (orderId required, lines non-empty, reason in
  enum, override-reason required when window closed).
- NEVER call CommitAsync in the handler — the UnitOfWork pipeline commits on BusinessResult{IsSuccess:true}.
- Return BusinessResult<T>. Errors via new Error(field, BusinessErrorMessage.X). Add any new error keys to
  BusinessErrorMessage with dot-notation (refund.window_closed, refund.override_reason_required, etc.) —
  and note in the status log that the 5 frontend i18n locales need the matching errors.* keys (T-0168 adds
  them; you just declare the backend keys).
- DTOs are record types. The admin command DTO will cross the wire => flag manual_step: nswag-regen
  (admin client) in the status log — do NOT run npm generate, do NOT hand-edit any NSwag client.
- TEST-FIRST (red→green is MANDATORY for money math + authz): write the failing tests FIRST in
  src/Cleansia.Tests/Features/Refunds/ then implement. Tests to write (from the ticket ACs):
    TC-REFUND-ALLOC  : discounted+express order, multi-line refund reconciles penny-perfect to TotalPrice;
                       last selected line absorbs sub-cent residual; discount/express never re-applied.
    TC-REFUND-VAT    : refundVat_i = round(lineRefund_i * vatRate/(100+vatRate),2); 0 when AppliedVatRate null.
    TC-REFUND-WINDOW : within 14 days of CompletedAt allowed; closed window needs non-empty override reason;
                       null CompletedAt = closed-by-default; Source=Chargeback exempt.
    TC-REFUND-FEE    : platform absorbs Stripe fee on ServiceNotRendered/DisputeResolution; deducts only on
                       AdminDiscretion; cancel-fee path untouched.
    TC-REFUND-CEILING: 0 < sum(succeeded) < charged => PartiallyRefunded; at equality => Refunded; order
                       lifecycle stays Completed.
    TC-REFUND-ADMINONLY: per-permission test — non-admin denied (the project's existing per-permission test
                       idiom — find and mirror it; grep for existing AdminOnly permission tests).
    TC-REFUND-BUNDLED: refunding one bundled service then the rest never exceeds the package line's share of
                       TotalPrice (uses PackagePricing.DeriveIncludedServiceGrosses).
- COMMENTS: write almost none. Comment ONLY non-obvious critical logic (the allocator residual rule, the
  window soft-override, the fee-bearer rule are the candidates). NO per-line/WHAT comments. NO task-number
  refs in code (no // T-0167, no // AC1) — those rot into dangling pointers. KEEP stable refs only:
  ADR-0009 / ADR-0006 / ADR-0001 and S7a/S7b where genuinely load-bearing.
- ADR-0009 is FROZEN — cite it, do NOT re-decide the formula/window/fee:
    D2 allocator: lineRefund_i = round(lineGross_i / sum(lineGross) * Order.TotalPrice, 2), last absorbs residual.
    D1 window: 14-day SOFT window on Order.CompletedAt; closed => requires persisted non-empty admin override.
    D3 fee bearer: platform absorbs on ServiceNotRendered/DisputeResolution; deducts on AdminDiscretion.
- This is security_touching (money-out + privileged). Authorization is server-side ONLY.
- Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs
  live in the ticket status log, never in the report.
`

phase('Build')
const dev = await agent(
  `You are the BACKEND developer. Implement ${TICKET}

${ANCHORS}
${RULES}

DELIVERABLES:
1. RefundPolicy static class (sibling to BookingPolicy): IsWithinWindow(completedAtUtc, nowUtc) +
   constants (14-day window), and the fee-bearer rule keyed on RefundReason. Pure, unit-testable.
2. The share-of-TotalPrice allocator: a pure static method that, given the selected lines' grosses +
   Order.TotalPrice (+ AppliedVatRate), returns per-line refund amount + apportioned VAT, reconciling
   penny-perfect (last selected line absorbs residual). For a bundled-service line, derive its gross via
   PackagePricing.DeriveIncludedServiceGrosses. Put it where it's testable (a static helper, mirror
   PackagePricing's style).
3. The admin partial-refund Command + Handler + Validator + Response (CQRS feature file under
   Features/Refunds/). Handler: load order, apply RefundPolicy (window + override + fee), run the
   allocator, call IRefundService.IssueRefundAsync per selected line (deterministic admin RefundKey),
   on success call ILoyaltyService.RevokeForPartialRefundAsync, derive the PaymentStatus summary. Wire
   the AdminOnly permission (new Policy constant + PolicyBuilder mapping + attribute) per ADR-0001 D2.
4. New BusinessErrorMessage keys as needed (dot-notation). New error keys must be listed in your status log.
5. The endpoint on the ADMIN host (Cleansia.Web.Admin) — find a sibling admin command endpoint and mirror
   its controller/route/attribute idiom. Add the response DTO (record).
6. TEST-FIRST: write all the TC-* tests above (red), then implement to green. Use the existing
   RefundServiceTests / refund test harness patterns where they fit.

Work ONLY on backend (no Angular, no .ts). Read PolicyBuilder.cs and a sibling admin command BEFORE
writing the permission wiring. Build the solution (dotnet build src/Cleansia.Api.sln) and run
src/Cleansia.Tests to green before returning.

Return a terse report: files created/changed, the new Policy constant + its mapping, the new
BusinessErrorMessage keys (so the owner/T-0168 add i18n), the test names + pass count, the build result,
and explicit MANUAL_STEP flags (nswag-regen for the admin client; no ef-migration — this ticket adds no
schema). State plainly if any AC is not met.`,
  { label: 'dev:T-0167', phase: 'Build', agentType: 'backend' },
)

phase('Review')
const [review, security] = await parallel([
  () => agent(
    `You are the REVIEWER for ${TICKET} (backend just landed). Audit the working tree against:
- the ticket ACs (AC1 allocator share-of-TotalPrice + residual; AC2 VAT apportion + null guard; AC3
  RefundPolicy 14-day SOFT window + override + null-CompletedAt-closed + Chargeback-exempt; AC4 fee bearer;
  AC5 PaymentStatus summary; AC6 AdminOnly; AC7 bundled-line via PackagePricing),
- ADR-0009 (formula/window/fee must MATCH, not re-decide), ADR-0006 (uses the seam, doesn't bypass Stripe),
  ADR-0001 (permission mapped + boot-guard satisfied),
- the conventions (handler happy-path-only, validator present with Cascade.Stop, BusinessResult, record
  DTOs, no CommitAsync in handler, comment discipline — NO task-number refs, almost-no comments, ADR/S-refs
  kept only where load-bearing), and
- gate 4c: did the dev introduce any duplicated logic that should be harvested into a knowledge/pattern file,
  or violate an existing pattern?
Verify the money math reconciles penny-perfect (re-derive a discounted+express example by hand). Verify
the tests actually assert the ACs (not vacuous). Read the real files. Give a verdict: APPROVE / APPROVE-WITH-NITS
/ REQUEST-CHANGES with specific file:line findings. Be concrete; no rubber-stamping.`,
    { label: 'review:T-0167', phase: 'Review', agentType: 'reviewer' },
  ),
  () => agent(
    `You are the SECURITY reviewer for ${TICKET} (security_touching: money-out + privileged). Audit against
the S1-S10 laws, focusing on:
- S-authz: is the refund command ACTUALLY admin-gated server-side (Policy mapped to AdminOnly, attribute
  present, boot-guard satisfied)? Can a non-admin reach it? Is authorization client-trusted anywhere? (403 path)
- S7a/S7b idempotency: is the RefundKey the command passes DETERMINISTIC (no Guid/timestamp), so a
  double-submit collapses on the seam's unique index — no double payout? Trace the key end-to-end.
- money-out integrity: can the allocator over-refund beyond Order.TotalPrice or beyond the per-line/package
  share? Does it rely on client-supplied amounts, or recompute server-side from frozen Order.TotalPrice?
- the soft-window override: is the override REASON persisted (audit trail) and required when the window is
  closed? Can the window be bypassed without an override?
- no phantom refund: does it use the confirm-then-record seam (never reports success on a failed Stripe call)?
- PII / over-exposure in the response DTO; resource-ownership (admin acting on any order is fine, but verify
  it loads the real order and doesn't trust a client line list blindly).
Read the real files. Verdict: PASS / PASS-WITH-NOTES / FAIL with specific file:line findings.`,
    { label: 'security:T-0167', phase: 'Review', agentType: 'security' },
  ),
])

return {
  ticket: 'T-0167',
  dev,
  review,
  security,
}
