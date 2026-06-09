export const meta = {
  name: 'wave2-2c-t0167-fix',
  description: 'T-0167 rework: per-country refund Stripe-fee config + fix the 3 panel findings (PerRoomPrice gross, fee/VAT/net split-brain, persist override reason), TDD + re-review',
  phases: [
    { title: 'Design', detail: 'architect pins the per-country fee-config seam + the corrected fee/VAT/net math' },
    { title: 'Fix', detail: 'backend applies all 3 fixes test-first' },
    { title: 'Re-review', detail: 'reviewer + security re-audit in parallel' },
  ],
}

const CONTEXT = `
T-0167 (admin partial-refund command) v1 landed but the parallel reviewer + security panel found THREE
real money defects (the dev's own tests were vacuous on all three) plus the owner has now decided the one
open policy question. This is the rework. ADR-0009 is the FROZEN policy; ADR-0006 is the FROZEN seam — cite,
never re-decide. The full ADR-0009 text is at agents/backlog/adr/0009-refund-policy.md (D1 window, D2
allocator, D3 fee bearer, D5/D5.1 gross basis, D6 loyalty).

THE THREE FINDINGS TO FIX (all confirmed against ADR-0009 + real code):

FINDING 1 — allocator drops PerRoomPrice (ADR-0009 D5.1). In
src/Cleansia.Core.AppServices/Features/Refunds/IssuePartialRefund.cs (BuildOrderLineGrosses, ~line 188) a
standalone OrderService's gross basis is "service.Service?.BasePrice ?? 0m". ADR-0009 D5.1 (lines 252-259)
fixes it as the canonical quote basis: BasePrice + PerRoomPrice × (order.Rooms + order.Bathrooms), MATCHING
src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs:30 which is literally
  s.BasePrice + s.PerRoomPrice * (rooms + bathrooms)
=> Fix: derive the standalone service gross with the PerRoomPrice term, reading rooms/bathrooms off the
order (Order.Rooms / Order.Bathrooms — verify the exact property names). This is a RATIO WEIGHT only; it is
then multiplied by frozen Order.TotalPrice so discount/surcharge stay embedded (D2 invariant — never
re-apply them). Package line basis stays Package.Price (D5.1, OrderPricingCalculator.cs:26). Bundled-service
basis stays PackagePricing.DeriveIncludedServiceGrosses (already correct).
TEST GAP THAT HID IT: the Svc(...) test helper always passed PerRoomPrice=0. Add a test whose service gross
MUST include the per-room component (assert the ratio split changes when PerRoomPrice>0 and rooms>0).

FINDING 2 — fee/VAT/net split-brain on the fee-deducted path. On the fee-deducted reason the handler sends
grossAmount = refundAmount − fee to the seam, but computes refundVat / refundNet from the PRE-fee
refundAmount. So the reported VAT and the loyalty-clawback net don't match the amount actually
sent/confirmed (a ~4 CZK error on a VAT-payer order). => Fix: VAT and net MUST be derived from the amount
ACTUALLY SENT to (and confirmed/clamped by) the seam — i.e. apportion VAT from result.Amount (the seam's
returned clamped amount), and refundNet = sentAmount − refundVatOnSentAmount. The Response.RefundVat and the
RevokeForPartialRefundAsync net must both reflect the real refunded amount. Add an AdminDiscretion + VAT-payer
test asserting net/VAT against the SENT amount, not the pre-fee amount.

FINDING 3 — the window-override REASON is validated then discarded (ADR-0009 D1, lines 123-127 + line 448
"Every partial refund must record (window override reason when applicable) ... for audit"). Today the
handler rejects a closed-window refund lacking OverrideReason, sets a WindowOverridden bool, and DROPS the
reason string — it is never persisted (no field on RefundRequest, no column on Refund). => Fix: persist the
override reason on the Refund row (the audit trail). Add a nullable column to the Refund entity
(WindowOverrideReason, maxlen ~500), thread it from the command → RefundRequest → Refund.Create (or a
Refund.SetWindowOverride method). This adds an OWNER ef-migration (additive nullable column). Add a test
asserting the stored reason (not just the bool).

OWNER DECISION (finding 4, now resolved) — the Stripe fee is a PER-COUNTRY CONFIG VALUE, not a hardcoded
1.4%+6 CZK. Remove the invented StripeFeeRate/StripeFixedFee handler constants entirely.
`

const FEE_CONFIG = `
PER-COUNTRY REFUND STRIPE-FEE CONFIG (the owner's chosen approach):
- Home: extend the EXISTING per-country config entity
  src/Cleansia.Core.Domain/Configuration/CountryConfiguration.cs — it already carries StandardVatRate,
  ReducedVatRate, FiscalEnforcementMode per country, resolved via
  countryConfigurationRepository.GetByCountryIdAsync(countryId, ct). Mirror that exactly. Do NOT invent a
  new appsettings options class — the precedent is the CountryConfiguration entity.
- Add two nullable decimal fields: RefundStripeFeeRate (percent, e.g. 1.4 means 1.4%) and
  RefundStripeFixedFee (currency units, e.g. 6 CZK), with an UpdateRefundStripeFee(rate, fixedFee) mutator
  in the same style as UpdateVatRates. Nullable so SK/PL/etc. that have no figure yet → null.
- Resolve the order's country the SAME way ReceiptService does: countryId = order.CustomerAddress?.CountryId
  (ReceiptService.cs:42), then countryConfigurationRepository.GetByCountryIdAsync(countryId, ct).
- DEFAULT WHEN CONFIG/RATE IS NULL OR COUNTRY UNKNOWN: fee = 0 (platform absorbs). This is the safe
  fail-open-for-the-customer default and matches "no figure pinned yet". A null config must NEVER throw and
  NEVER deduct a guessed amount.
- The fee deduction still applies ONLY on AdminDiscretion (ADR-0009 D3 — platform absorbs on
  ServiceNotRendered/DisputeResolution; CustomerCancellation is the untouched cancel path). The fee BEARER
  rule stays in RefundPolicy.PlatformAbsorbsStripeFee; the fee AMOUNT now comes from CountryConfiguration.
- Seed note: only add the CZ value (1.4 + 6) to seed data IF the owner approves a seed edit — DO NOT edit DB
  seeds; instead flag it as an owner action item (the owner sets the CZ figure via admin/seed). Ship the
  code reading config with the null→0 default; CZ keeps absorbing until the owner sets the value.
`

const RULES = `
PROJECT RULES (non-negotiable): CQRS/MediatR (Command+Handler+Validator+Response one file; handler
HAPPY-PATH only; every *Command needs a Validator or the pipeline throws); NEVER CommitAsync in a handler
(UoW pipeline commits on BusinessResult{IsSuccess:true}); return BusinessResult<T>; Error(field,
BusinessErrorMessage.X) with dot-notation keys; record DTOs. TEST-FIRST for money math (red→green
mandatory). Comments: write almost none — only non-obvious critical logic; NO task-number refs (no // T-0167,
no // AC#); KEEP only load-bearing ADR-0009/ADR-0006/ADR-0001 + S7a/S7b refs. Do NOT run dotnet ef (owner-
only) — flag manual_step: ef-migration. Do NOT run npm generate / hand-edit NSwag clients — flag manual_step:
nswag-regen. Do NOT edit DB seeds without owner approval — flag instead. Build src/Cleansia.Api.sln and run
src/Cleansia.Tests to green before returning. Backend only (the admin UX is the sibling T-0168).
`

phase('Design')
const design = await agent(
  `You are the SOLUTION ARCHITECT. The owner chose "per-country config value" for the refund Stripe fee.
Pin the seam precisely so the backend dev implements it once, correctly, without re-deciding.

${CONTEXT}
${FEE_CONFIG}

Read the real files first: CountryConfiguration.cs, ReceiptService.cs (the per-country resolution
precedent), IssuePartialRefund.cs (the current handler), RefundPolicy.cs, RefundAllocator.cs, Order.cs (for
the exact Rooms/Bathrooms/CustomerAddress property names), Refund.cs + RefundEntityConfiguration.cs (for the
override-reason column), and ADR-0009 D1/D2/D3/D5.1.

Produce a TIGHT design note (no code, just the contract) covering:
1. The CountryConfiguration delta (exact field names/types + mutator), and the EXACT resolution path the
   handler uses to get the order's RefundStripeFeeRate/FixedFee (countryId source → repo call → null→0
   default). Name the precise repository interface + method that already exists.
2. The corrected fee/VAT/net math, written as an explicit ordered formula: given the D2 allocated line
   amount + reason + per-country fee config, what is (a) the amount sent to the seam, (b) the VAT reported,
   (c) the net handed to RevokeForPartialRefundAsync — all derived from the SENT/confirmed amount. Show a
   worked VAT-payer AdminDiscretion example (TotalPrice 1210, 21% VAT, single full-line refund) with every
   intermediate number so the dev and the reviewer can check against it.
3. The Refund override-reason persistence shape (column name/type, how it threads command→RefundRequest→
   Refund), and confirm it's an additive nullable column (owner ef-migration).
4. The corrected standalone-service gross basis (D5.1) restated as the one-line formula the allocator must use.
5. Whether ANY of this touches the ADR-0006 seam contract (it must NOT — the seam still enforces only
   ceiling+idempotency; the fee/VAT/net math is caller-side). Confirm or flag.
Be concrete and terse. This note is the dev's spec.`,
  { label: 'architect:T-0167-fix', phase: 'Design', agentType: 'architect' },
)

phase('Fix')
const dev = await agent(
  `You are the BACKEND developer. Apply the T-0167 rework per the architect's design note below. Fix ALL
THREE findings + wire the per-country fee config. TEST-FIRST.

=== ARCHITECT DESIGN NOTE ===
${design}
=== END DESIGN NOTE ===

${CONTEXT}
${FEE_CONFIG}
${RULES}

DELIVERABLES:
1. CountryConfiguration: add RefundStripeFeeRate + RefundStripeFixedFee (nullable decimals) +
   UpdateRefundStripeFee mutator (per the design note's exact names). Wire EF config if the entity config is
   explicit (check CountryConfigurationEntityConfiguration). Flag manual_step: ef-migration (additive
   nullable columns).
2. Refund: add the nullable WindowOverrideReason column (per the design note) + thread it
   command → RefundRequest → Refund. NOTE: RefundRequest is the ADR-0006 seam contract record
   (IRefundService.cs) — adding an optional nullable field to it is allowed (it's an input carried to the
   Refund row); do NOT change the seam's BEHAVIOR (ceiling+idempotency only). Flag manual_step: ef-migration.
3. IssuePartialRefund.Handler: (a) FINDING 1 — fix the standalone gross to BasePrice + PerRoomPrice ×
   (rooms+bathrooms); (b) FINDING 2 — derive VAT + net from the amount actually sent/confirmed by the seam,
   fee amount from CountryConfiguration (null→0), deduct only on AdminDiscretion; (c) FINDING 3 — persist the
   override reason. Remove the invented StripeFeeRate/StripeFixedFee handler constants.
4. Inject the country-config repository into the handler (constructor) — use the exact interface the design
   note names (the one ReceiptService already uses).
5. TEST-FIRST — add/repair these tests red→green (the v1 tests were vacuous on exactly these):
   - per-room gross: a service with PerRoomPrice>0 and rooms>0 changes the ratio split (assert the numbers).
   - AdminDiscretion + VAT-payer + non-null per-country fee: VAT and net match the SENT amount (use the
     architect's worked example numbers).
   - AdminDiscretion + null per-country config: fee = 0, customer gets full allocated amount (no throw).
   - override reason persisted: closed-window + override reason → the stored Refund.WindowOverrideReason
     equals the supplied reason (assert the stored string, not just the bool).
   - keep all existing green refund tests green.

Read the architect note's worked example and make your AdminDiscretion test assert those exact numbers.
Build src/Cleansia.Api.sln + run src/Cleansia.Tests to green. Return: files changed, the new
CountryConfiguration fields, the new Refund column, the corrected formulas (one line each), test names +
pass count, build result, and the manual_step flags (ef-migration: CountryConfiguration fee columns +
Refund.WindowOverrideReason; nswag-regen: admin client refund command DTO). Note explicitly that the CZ fee
figure is an OWNER action item (do not seed it without approval; null→0 means CZ absorbs until set).`,
  { label: 'dev:T-0167-fix', phase: 'Fix', agentType: 'backend' },
)

phase('Re-review')
const [review, security] = await parallel([
  () => agent(
    `You are the REVIEWER re-auditing T-0167 after the rework. The v1 had 3 findings you (the panel) caught;
verify ALL are genuinely fixed, not papered over:
FINDING 1: standalone service gross now = BasePrice + PerRoomPrice × (rooms+bathrooms) per ADR-0009 D5.1
  (matches OrderPricingCalculator.cs:30); and a NEW test exercises PerRoomPrice>0 (not the old PerRoomPrice=0
  fixture). Re-derive a 2-line ratio by hand with a per-room service to confirm the split.
FINDING 2: VAT + net are derived from the amount ACTUALLY SENT/confirmed by the seam (not the pre-fee
  amount); fee comes from CountryConfiguration (null→0, no throw); deduct only on AdminDiscretion. Re-derive
  the architect's worked VAT-payer AdminDiscretion example by hand and check Response.RefundVat + the
  clawback net against the SENT amount.
FINDING 3: the override reason is PERSISTED on the Refund row (real column, threaded through), and a test
  asserts the STORED string (not just the WindowOverridden bool).
Also: the invented 1.4%+6 handler constants are GONE; the per-country config follows the CountryConfiguration
precedent; the ADR-0006 seam behavior is unchanged (still ceiling+idempotency only); conventions hold
(handler happy-path, validator present, no CommitAsync, BusinessResult, record DTOs, comment discipline — no
task-number refs); and the day-14 boundary test gap + any dead RequiresWindowCheck note from v1.
Read the real files, run the gate (build + Features.Refunds suite). Verdict: APPROVE / APPROVE-WITH-NITS /
REQUEST-CHANGES with file:line findings. Do not rubber-stamp — re-derive the money.`,
    { label: 'review:T-0167-fix', phase: 'Re-review', agentType: 'reviewer' },
  ),
  () => agent(
    `You are the SECURITY reviewer re-auditing T-0167 after the rework (security_touching: money-out +
privileged). The v1 BLOCKER you raised was: the window-override reason was required but never persisted (no
audit trail). VERIFY it is now persisted: a real Refund column, threaded command→RefundRequest→Refund row,
and a test asserts the stored reason. Then re-confirm the v1 PASSes still hold and the rework introduced no
regression:
- S-authz: still [Permission(Policy.CanIssueRefund)]→AdminOnly, boot-guard satisfied, server-side only.
- S7a/S7b: the RefundKey is still deterministic (no Guid/timestamp); adding the override reason / fee config
  did not break idempotency or let a double-submit double-pay.
- money-out integrity: the per-country fee config cannot be client-supplied; the fee is resolved server-side
  from CountryConfiguration by the order's country; null config → fee 0 (never a guessed deduction, never a
  throw); the allocator still recomputes from frozen Order.TotalPrice (no client amounts); the seam ceiling
  clamp still bounds cumulative refunds ≤ TotalPrice.
- no phantom refund: still confirm-then-record via the seam.
- S4 PII: the override reason is admin-entered free text persisted for audit — confirm it is NOT echoed into
  any customer-facing notification/DTO and is access-controlled (admin-only read surface).
- the new ef-migration (fee columns + override-reason column) is additive/nullable (safe).
Read the real files. Verdict: PASS / PASS-WITH-NOTES / FAIL with file:line findings.`,
    { label: 'security:T-0167-fix', phase: 'Re-review', agentType: 'security' },
  ),
])

return { ticket: 'T-0167 (rework)', design, dev, review, security }
