export const meta = {
  name: 'wave2-2c-final',
  description: 'Wave-2 close-out: T-0168 admin refund UX (frontend) + T-0231b expose PriceWeight on package DTO (backend), each dev + reviewer in parallel',
  phases: [
    { title: 'Build', detail: 'T-0168 frontend + T-0231b backend in parallel' },
    { title: 'Review', detail: 'a reviewer per lane' },
  ],
}

const RULES_FE = `
FRONTEND RULES (Cleansia admin app — non-negotiable, from CLAUDE.md + patterns-frontend):
- Angular 19, standalone, OnPush on presentational components. Logic in a FACADE (extends
  UnsubscribeControlDirective), NOT the component. State via signals. RxJS cleanup via takeUntil(destroyed$).
- Use <cleansia-*> shared components + PrimeNG — NEVER raw <button>/<select>/<input>/<form>.
- Every user-visible string via TranslatePipe (standalone) with keys added to ALL 5 locales:
  apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json. No hardcoded strings.
- No 'any'. Use the NSwag-generated types from '@cleansia/admin-services'. Do NOT hand-edit the generated
  client. The client is ALREADY regenerated and carries the refund DTOs (verified).
- Three explicit data states (loading / empty-or-ready / error) rendered explicitly.
- No inline templates/styles — separate .html, SCSS in shared assets.
- Comments: write almost none; only non-obvious logic; NO task-number refs (no // T-0168), NO // AC#.
- Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs
  live in the ticket status log, never in the report.
`

const RULES_BE = `
BACKEND RULES (non-negotiable): CQRS/MediatR one-file feature (Command+Handler+Validator+Response);
handler HAPPY-PATH only; every *Command has a Validator (Cascade.Stop); NEVER CommitAsync in a handler;
return BusinessResult<T>; Error(field, BusinessErrorMessage.X); record DTOs. TEST-FIRST. Do NOT run dotnet
ef (owner-only) — flag manual_step: ef-migration ONLY if schema changes (this ticket does NOT change schema —
PackageService.PriceWeight column already exists from T-0231). Do NOT run npm generate / hand-edit NSwag
clients — flag manual_step: nswag-regen (the package DTO surface changes). Comments: almost none, no
task-number refs, keep only load-bearing ADR-0009 refs. Build src/Cleansia.Api.sln + run src/Cleansia.Tests
green before returning. Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key
file:line; full logs live in the ticket status log, never in the report.
`

const REFUND_DTO = `
THE REFUND CLIENT SURFACE (already in '@cleansia/admin-services', verified — bind to these EXACT types):
- AdminClient.adminRefundClient.partial(body: IssuePartialRefundCommand): Observable<IssuePartialRefundResponse>
  (or inject AdminRefundClient directly — mirror how OrderDetailFacade injects AdminClient).
- IssuePartialRefundCommand { orderId?: string; lines?: IssuePartialRefundRefundLineSelection[];
    reason: RefundReason; overrideReason?: string }
- IssuePartialRefundRefundLineSelection { serviceId?: string; packageId?: string }
- IssuePartialRefundResponse { ...refundAmount, refundVat, paymentStatus, windowOverridden... } (read the
    generated interface for exact field names before binding).
- RefundReason enum is in the client (CustomerCancellation/DisputeResolution/AdminDiscretion/ServiceNotRendered).
- Backend error codes to translate (errors.* in all 5 locales): refund.lines_required, refund.line_invalid,
  refund.override_reason_required, refund.failed, refund.nothing_refundable, refund.order_not_refundable.

IMPORTANT AC3 LIMITATION (state it, do not fake it): the line-selection DTO currently has only serviceId /
packageId — there is NO per-bundled-service field. So selecting a SINGLE service bundled inside a package is
NOT expressible in the current command. Surface OrderService + OrderPackage line selection (which IS
supported) and the reason + optional override reason. For AC3's bundled-sub-service selection, add a clear
note in your report that it needs a backend command field (a bundled-service selector) — do NOT invent a
client field or hand-edit the generated client. Build what the contract supports; flag the gap precisely.
`

const ANCHORS_FE = `
ADMIN-APP ANCHORS (mirror these — read them first):
- The order-detail feature where the refund action belongs:
  src/Cleansia.App/libs/cleansia-admin-features/order-management/src/lib/order-detail/
    order-detail.component.ts / .html / order-detail.facade.ts
- The facade pattern (inject AdminClient, signals, takeUntil(destroyed$), catchError→of(null),
  finalize→loading.set(false)): order-detail.facade.ts (read it; copy its shape exactly).
- SnackbarService for success/error toasts; TranslateService for messages.
- i18n: add keys under a sensible namespace (e.g. pages.order_management.refund.*) to all 5 files.
- Add the facade spec FIRST (test-first), then the component spec (three states + OnPush).
`

phase('Build')
const [feDev, beDev] = await parallel([
  () => agent(
    `You are the FRONTEND developer (Cleansia admin app). Implement T-0168 — the admin partial-refund UX on
the order-detail screen.

${REFUND_DTO}
${ANCHORS_FE}
${RULES_FE}

DELIVERABLES:
1. A refund panel/dialog on the admin order-detail screen (only meaningful for a completed, card-paid order)
   that lets the admin: pick one or more order lines (OrderService / OrderPackage) via <cleansia-*>/PrimeNG
   controls, choose a RefundReason, optionally enter an override reason (shown/required when the window is
   closed — the backend enforces it; surface the field + the refund.override_reason_required error), and submit.
2. A facade (extends UnsubscribeControlDirective) holding the state (selected lines, reason, override reason,
   loading, error) and calling adminRefundClient.partial(...). On success: success snackbar + refresh the
   order (re-load detail so the new PaymentStatus shows). On error: map the backend error code to the
   errors.* translation and show it.
3. errors.refund.* + pages.*.refund.* i18n keys in ALL 5 locales (en/cs/sk/uk/ru). Use real translations,
   not English placeholders, for cs/sk/uk/ru (reasonable native strings).
4. Three explicit data states + OnPush on presentational components.
5. TEST-FIRST: the facade spec first (submit success, error-code→message, loading state), then the component
   spec (renders states; OnPush).
6. Run the admin app's lint + unit tests (nx lint cleansia-admin-app; nx test for the touched lib) to green.

Honour the AC3 limitation above — surface service/package line selection; flag the bundled-sub-service gap
precisely (it needs a backend command field), do NOT hand-edit the generated client. Return: files
created/changed, i18n keys added (×5), the test names + result, lint/test status, and an explicit statement
of which ACs are fully met vs partially (AC3) with the exact backend gap.`,
    { label: 'dev:T-0168', phase: 'Build', agentType: 'frontend' },
  ),
  () => agent(
    `You are the BACKEND developer. Implement T-0231b — expose PackageService.PriceWeight on the admin package
DTO surface so the T-0232 weight UX has a field to read/write. The PriceWeight COLUMN already exists
(T-0231, src/Cleansia.Core.Domain/Packages/PackageService.cs: PriceWeight + SetPriceWeight + DefaultPriceWeight=1m).
This ticket is DTO + command wiring only — NO schema change, NO migration.

${RULES_BE}

CURRENT STATE (read these first):
- src/Cleansia.Core.AppServices/Features/Packages/DTOs/AdminPackageDetailDto.cs — PackageServiceDto has
  { Id, Name, Description } and NO PriceWeight.
- src/Cleansia.Core.AppServices/Features/Packages/UpdatePackage.cs — Command takes List<string>? ServiceIds
  (no weight); Handler does ClearServices() then AddService(service) per id.
- Domain: PackageService.SetPriceWeight(decimal) exists; Package.AddService(service) creates the join.

DELIVERABLES:
1. Add 'decimal PriceWeight' to PackageServiceDto, and have the GetPackageOverview / detail mapper populate
   it from the PackageService join (find the mapper that builds AdminPackageDetailDto and read the real
   PackageService.PriceWeight). Verify the included-services query actually loads PackageService rows (with
   PriceWeight) — adjust the projection/Include if it currently only projects Service.
2. Change UpdatePackage.Command so it accepts a per-included-service weight. Cleanest shape: replace/augment
   List<string>? ServiceIds with a List of { ServiceId, PriceWeight } (a small record), OR keep ServiceIds +
   add a parallel Dictionary<string,decimal>? ServiceWeights — pick the one that's cleanest given the
   existing admin package form contract; whichever you choose, the Handler must, per included service,
   AddService(...) then SetPriceWeight(weight) (default 1m when omitted, matching DefaultPriceWeight).
   Validate weights are > 0 (BusinessErrorMessage — add a key like package.invalid_weight if needed).
3. Keep backward compatibility sane: if no weights supplied, every service gets DefaultPriceWeight (1m) =
   even split (the ADR-0009 D5 backfill default).
4. TEST-FIRST: a handler test that sets per-service weights and asserts they persist on the join; a mapper/
   detail test that the DTO returns the stored PriceWeight; a default test (no weight → 1m).
5. Build src/Cleansia.Api.sln + run src/Cleansia.Tests green.

This changes the package DTO surface → flag manual_step: nswag-regen (admin client) so T-0232 (frontend
weight UX) can bind. NO ef-migration (column already exists). Return: files changed, the new DTO field, the
UpdatePackage command shape chosen + why, test names + result, build result, manual_step flags.`,
    { label: 'dev:T-0231b', phase: 'Build', agentType: 'backend' },
  ),
])

phase('Review')
const [feReview, beReview] = await parallel([
  () => agent(
    `You are the REVIEWER for T-0168 (admin refund UX, frontend). The frontend dev just landed it. Audit the
working tree against:
- the ticket ACs: AC1 refund UX (cleansia-*/PrimeNG, logic in facade, TranslatePipe with keys in all 5
  locales), AC2 error-contract parity (every backend refund.* error code the command returns has a matching
  errors.* translation in all 5 locales — CHECK the actual key set against the backend BusinessErrorMessage
  refund.* keys), AC3 bundled-service selection (the dev should have flagged the backend gap honestly — verify
  the flag is accurate and they did NOT hand-edit the generated client or fake a field), AC4 three data states
  + OnPush.
- conventions (patterns-frontend): no raw form controls, no 'any', no inline templates/styles, OnPush,
  facade holds logic, takeUntil cleanup, no hardcoded strings, comment discipline (no task-number refs).
- gate 4c: any duplicated logic worth harvesting, or a violated pattern.
Run the gate where feasible (nx lint / nx test for the touched lib). Read the real files. Verdict: APPROVE /
APPROVE-WITH-NITS / REQUEST-CHANGES with file:line findings. Confirm the i18n keys really exist in ALL FIVE
files (en/cs/sk/uk/ru), not just en.`,
    { label: 'review:T-0168', phase: 'Review', agentType: 'reviewer' },
  ),
  () => agent(
    `You are the REVIEWER for T-0231b (expose PriceWeight on the package DTO, backend). Audit against:
- the goal: PackageServiceDto returns the stored PriceWeight; UpdatePackage accepts + persists per-service
  weights (AddService then SetPriceWeight); default 1m even-split when omitted (ADR-0009 D5 backfill default);
  weights validated > 0.
- NO schema change / NO migration (the column already exists) — verify the dev did NOT add a migration or
  alter the entity/EF config.
- conventions: handler happy-path, validator present (Cascade.Stop), no CommitAsync in handler,
  BusinessResult, record DTOs, comment discipline (no task-number refs).
- the tests are non-vacuous: a weight actually round-trips (set → persist → read back via the DTO), and the
  default path is covered. Re-derive: 3 services, weights 2/1/1 → the detail DTO returns 2,1,1; no weights →
  1,1,1.
Run the gate (build + the Packages test filter). Read the real files. Verdict: APPROVE / APPROVE-WITH-NITS /
REQUEST-CHANGES with file:line findings. Confirm manual_step: nswag-regen is flagged and ef-migration is NOT
(no schema change).`,
    { label: 'review:T-0231b', phase: 'Review', agentType: 'reviewer' },
  ),
])

return {
  t0168: { dev: feDev, review: feReview },
  t0231b: { dev: beDev, review: beReview },
}
