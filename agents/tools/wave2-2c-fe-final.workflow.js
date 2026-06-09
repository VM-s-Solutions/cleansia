export const meta = {
  name: 'wave2-2c-fe-final',
  description: 'Wave-2 frontend close-out: T-0168 rework (fix F1/F2, deliver AC3 bundled refund) + T-0232 package-weight UX, each dev + reviewer in parallel',
  phases: [
    { title: 'Build', detail: 'T-0168 rework + T-0232 in parallel' },
    { title: 'Review', detail: 'a reviewer per lane' },
  ],
}

const FE_RULES = `
FRONTEND RULES (Cleansia admin app, non-negotiable): Angular 19 standalone, OnPush on presentational
components, logic in a FACADE (extends UnsubscribeControlDirective) not the component, signals for state,
takeUntil(destroyed$) cleanup. <cleansia-*>/PrimeNG only — NO raw <button>/<select>/<input>/<form>. Every
user-visible string via TranslatePipe with keys in ALL 5 locales
(apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json) with REAL native translations (not English
placeholders). No 'any' — use the NSwag types from '@cleansia/admin-services' (already regenerated). Do NOT
hand-edit the generated client. Three explicit data states + OnPush. No inline templates/styles. Comments:
almost none, no task-number refs (no // T-0168), no // AC#. Run nx lint + nx test for the touched lib to
green (add no NEW lint errors; pre-existing baseline is out of scope).
`

phase('Build')
const [refundDev, weightDev] = await parallel([
  () => agent(
    `You are the FRONTEND developer. REWORK T-0168 (admin refund UX) to fix the 2 blockers the reviewer found
and deliver AC3 (bundled-service refund), now that the backend DTO exposes service IDs.

THE THREE THINGS TO FIX (all confirmed against real code):

F1 (BLOCKER) — package lines fail backend validation. The refund command requires EVERY line to carry a
non-empty ServiceId. RefundLineSelection = { serviceId, packageId? }:
  - standalone service = { serviceId, packageId: undefined }
  - bundled service    = { serviceId, packageId }   (a service INSIDE a package)
There is NO "whole package, no serviceId" shape. Today the facade
(src/Cleansia.App/libs/cleansia-admin-features/order-management/src/lib/order-detail/components/
admin-order-refund.facade.ts:77-79) sends a package as { serviceId: undefined, packageId } — backend rejects
it with refund.line_invalid. FIX: stop emitting whole-package lines; emit per-service lines only.

F3/AC3 (now deliverable) — the order DTO now exposes, per package, its included services WITH IDs:
  OrderItem.selectedPackages[].includedServiceItems: { id, name }[]   (NEW field, verified in the regenerated
  '@cleansia/admin-services' admin-client; PackageDetails.includedServiceItems: PackageServiceRef[] where
  PackageServiceRef = { id, name }). (The old includedServices: string[] of names still exists — do NOT use it
  for IDs; use includedServiceItems.)
FIX: when building refund line options
(admin-order-refund.component.ts constructor effect, ~lines 72-92), for each selectedPackage, expand its
includedServiceItems into selectable BUNDLED lines { kind:'bundled', id: item.id (the serviceId),
packageId: pkg.id, name: item.name }. Group them visibly under the package (the package is a header/expandable;
its included services are the selectable rows). Keep standalone selectedServices as { kind:'service', id, ... }.
Update the model (admin-order-refund.models.ts): RefundLineKind gains 'bundled'; RefundLineOption gains an
optional packageId.
Then in the facade's line→command mapping (facade.ts ~75-80):
  - kind 'service'  -> new IssuePartialRefundRefundLineSelection({ serviceId: line.id, packageId: undefined })
  - kind 'bundled'  -> new IssuePartialRefundRefundLineSelection({ serviceId: line.id, packageId: line.packageId })
Every emitted line now carries serviceId -> passes backend validation.

F2 (BLOCKER) — missing i18n. order-management.helpers.ts maps PaymentStatus.PartiallyRefunded to
'pages.order_management.payment_status.partially_refunded' but that key does NOT exist in any locale. ADD
pages.order_management.payment_status.partially_refunded to ALL 5 locale files (en/cs/sk/uk/ru) with real
native translations. (A partially-refunded order is exactly what this feature produces.) Also add any new
refund.* / pages.*.refund.* keys your bundled-line UI introduces (e.g. a 'bundled' line-kind label, a package
group label) to all 5 locales.

${FE_RULES}

DELIVERABLES:
1. Model: RefundLineKind += 'bundled'; RefundLineOption += optional packageId.
2. Component effect: build standalone-service lines AND per-package bundled-service lines from
   includedServiceItems (grouped under the package in the template via <cleansia-*>/PrimeNG; no raw controls).
3. Facade: map each line kind to a valid RefundLineSelection (every line carries serviceId).
4. i18n ×5: payment_status.partially_refunded + any new refund UI strings.
5. TEST-FIRST: update/extend the facade spec — a 'service' line emits { serviceId, packageId: undefined };
   a 'bundled' line emits { serviceId, packageId }; NO line is ever emitted with an empty serviceId (the F1
   regression guard). Update the component spec — a package's includedServiceItems render as selectable
   bundled rows. Keep all existing green specs green.
6. nx lint + nx test (order-management lib) to green (no NEW lint errors; the pre-existing
   order-management.component.spec HttpClient DI failure and the selector-prefix baseline are out of scope).

Return: files changed, the model/facade/component deltas (showing every line carries serviceId), the i18n
keys added ×5, test names + result, lint/test status, and confirm AC1/AC2/AC3/AC4 all now MET.`,
    { label: 'dev:T-0168-rework', phase: 'Build', agentType: 'frontend' },
  ),
  () => agent(
    `You are the FRONTEND developer. Implement T-0232 — the admin package-form per-included-service relative-
weight editor (ADR-0009 D5), now that the backend exposes PriceWeight.

BACKEND CONTRACT (regenerated '@cleansia/admin-services', verified):
- AdminPackageDetailDto.includedServices[]: PackageServiceDto { id, name, description, priceWeight: number }
  (priceWeight is NEW — the relative weight, default 1).
- UpdatePackageCommand now has serviceWeights?: { [serviceId: string]: number } (a map). Send each included
  service's weight there. Omitting a service -> backend defaults it to 1 (even split). Weights must be > 0
  (backend rejects with package.invalid_weight).

ANCHORS (read first, mirror the conventions):
- The package form feature: src/Cleansia.App/libs/cleansia-admin-features/package-management/src/lib/
  package-form/package-form.component.ts / .html / package-form.facade.ts. See how it currently reads
  includedServices and builds the UpdatePackage call (it maps serviceIds today).
- An existing admin facade for the inject/signal/takeUntil pattern.

${FE_RULES}

DELIVERABLES:
1. On the package form, for each included service show a relative-weight input (<cleansia-*>/PrimeNG numeric,
   no raw <input>), pre-filled from priceWeight (default 1). Logic in the facade.
2. AC2 — weights re-normalise visibly: show each service's DERIVED gross = round(weight / Σweights ×
   Package.Price, 2) so the admin sees the effect before saving; Σ(shown grosses) == the package price (last
   row absorbs the sub-cent residual, same rule as the backend PackagePricing). Recompute live as weights or
   price change (a computed signal). This is display-only math in the facade — the backend is the source of
   truth on save.
3. On save, populate UpdatePackageCommand.serviceWeights from the per-service inputs.
4. AC1 i18n ×5: weight label, the derived-gross display label, and the errors.package.invalid_weight key (the
   backend validation message) in en/cs/sk/uk/ru with real native translations.
5. AC3 — three explicit data states + OnPush.
6. TEST-FIRST: a facade/component test that (a) the derived grosses sum to the package price for given
   weights (e.g. price 100, weights 3/1 -> 75/25), (b) save sends serviceWeights, (c) default weight 1 shows
   even split. Then the component spec (states + OnPush).
7. nx lint + nx test (package-management lib) to green (no NEW lint errors).

Return: files changed, the weight UX + the live derived-gross computation, the i18n keys ×5 (incl.
errors.package.invalid_weight), test names + result, lint/test status, AC1/AC2/AC3 status.`,
    { label: 'dev:T-0232', phase: 'Build', agentType: 'frontend' },
  ),
])

phase('Review')
const [refundReview, weightReview] = await parallel([
  () => agent(
    `You are the REVIEWER for the T-0168 REWORK (admin refund UX). The v1 had: F1 (package lines failed
backend validation — every line must carry serviceId), F2 (payment_status.partially_refunded i18n missing in
all 5 locales), F3/AC3 (bundled-service selection not delivered). Verify ALL are genuinely fixed:
- F1: trace the facade's line→command mapping — EVERY emitted IssuePartialRefundRefundLineSelection carries a
  non-empty serviceId (standalone = {serviceId, packageId undefined}; bundled = {serviceId, packageId}). No
  whole-package {serviceId: undefined} line is ever emitted. Confirm a facade spec asserts this (the F1
  regression guard).
- AC3: the component builds bundled-service lines from order.selectedPackages[].includedServiceItems (the new
  {id,name}), grouped under the package, selectable; it does NOT use the names-only includedServices for IDs.
- F2: pages.order_management.payment_status.partially_refunded exists in ALL 5 locale files with real
  translations (not the raw key, not English placeholders). Plus any new bundled-line UI strings ×5.
- conventions: <cleansia-*>/PrimeNG (no raw controls), logic in facade, OnPush, no 'any', no hardcoded
  strings, generated client untouched, comment discipline (no task-number refs).
Run the gate (nx lint + nx test for order-management; the T-0168 specs must pass; confirm no NEW lint errors).
Read the real files. Verdict: APPROVE / APPROVE-WITH-NITS / REQUEST-CHANGES with file:line findings. Confirm
AC1/AC2/AC3/AC4 all met. Do not rubber-stamp.`,
    { label: 'review:T-0168-rework', phase: 'Review', agentType: 'reviewer' },
  ),
  () => agent(
    `You are the REVIEWER for T-0232 (admin package-form weight UX). Audit against:
- AC1: per-included-service relative-weight editor via <cleansia-*>/PrimeNG (no raw controls), logic in a
  facade, strings via TranslatePipe in ALL 5 locales (incl. errors.package.invalid_weight) with real
  translations.
- AC2: weights re-normalise visibly — the derived per-service gross = round(weight/Σweights × Package.Price,
  2) is shown, and Σ(shown grosses) == the package price (last row absorbs the residual, matching the backend
  PackagePricing rule). Re-derive by hand: price 100, weights 3/1 → 75/25 (Σ100); price 100, weights 1/1/1 →
  33.33/33.33/33.34 (Σ100). Confirm a test asserts this non-vacuously.
- save: UpdatePackageCommand.serviceWeights is populated from the inputs; default weight 1 → even split.
- AC3: three explicit data states + OnPush.
- conventions: no 'any', no raw controls, facade holds logic, generated client untouched, comment discipline
  (no task-number refs).
Run the gate (nx lint + nx test for package-management; no NEW lint errors). Read the real files. Verdict:
APPROVE / APPROVE-WITH-NITS / REQUEST-CHANGES with file:line findings.`,
    { label: 'review:T-0232', phase: 'Review', agentType: 'reviewer' },
  ),
])

return {
  t0168_rework: { dev: refundDev, review: refundReview },
  t0232: { dev: weightDev, review: weightReview },
}
