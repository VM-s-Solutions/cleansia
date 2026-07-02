export const meta = {
  name: 'wave8-8a-8b',
  description: 'Wave 8 pre-iOS cleanup — 8A (T-0272 auth contract, security-gated, first) + 8B (T-0273/0274/0275/0276/0277/0278 disjoint cleanup). Holds 8C (T-0280/0281) for the owner regen.',
  phases: [
    { title: '8A auth contract', detail: 'T-0272 shrink the wire contract (security-gated, triggers regen)' },
    { title: '8B cleanup', detail: 'paged-query canon, FE resolver dedup, dead-code, push-facade, Android :core hoists' },
  ],
}

const ROOT = 'c:/Users/cmisa/Desktop/Mike/Projects/cleansia'
const ANDROID = ROOT + '/src/cleansia_android'

const COMMON = [
  'Repo root: ' + ROOT + '. Branch feature/wave8-pre-ios-cleanup is checked out. Do NOT commit, push, or git add. Other lanes run concurrently in this tree — touch ONLY your ticket files; other lanes\' uncommitted changes are EXPECTED.',
  'Today is 2026-06-22. This is the PRE-iOS CLEANUP wave — BEHAVIOR-PRESERVING canonicalization/dedup/dead-code removal. Read your ticket IN FULL, agents/knowledge/conventions.md, consistency.md, and the relevant patterns-*.md (the CANONICAL forms you converge ON).',
  'Each finding cites file:line evidence + the canonical it should match — trust that, but VERIFY the canonical yourself before mirroring it. BEHAVIOR-PRESERVING: same results, same responses, same observable behavior; you are changing SHAPE (archetype, dedup, location), not logic.',
  'TEST-FIRST where you change behavior-bearing code: pin current behavior with a characterization test, confirm green, refactor under it. For pure deletes (dead code), prove zero-consumer by grep first and keep the suite green.',
  'ENVIRONMENT: backend — dotnet build <proj>.csproj then dotnet test <proj>.csproj --no-build (VS may hold host-DLL locks; build the specific project). Frontend — npx nx test/build <project> from src/Cleansia.App. Android — from ' + ANDROID + ', ./gradlew.bat <module>:compileDebugKotlin / testDebugUnitTest --offline -q. Baseline is fully green (Cleansia.Tests, IntegrationTests, HostTests; FE Jest + 3 prod builds; android :core 13 / partner 26 / customer 222) so ANY new red is YOURS.',
  'ENCODING (Kotlin/any): edited files stay clean UTF-8, no BOM, no mojibake.',
  'Comment discipline: NO ticket IDs in source. conventions.md default = no comment.',
  'OWNER-ONLY (never run): dotnet ef, npm run generate-*-client, edits to NSwag clients. If your change alters a response/DTO shape, record MANUAL_STEP nswag-regen (do not regen).',
  'Update your ticket: status -> review, append status-log (what changed, test evidence, deviations, manual steps). Final message is data for the orchestrator.',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

const DEV_SCHEMA = {
  type: 'object',
  required: ['summary', 'filesChanged', 'behaviorPreserved', 'verificationAchieved', 'manualSteps', 'deviations'],
  properties: {
    summary: { type: 'string', maxLength: 600 }, filesChanged: { type: 'array', items: { type: 'string', maxLength: 300 } },
    behaviorPreserved: { type: 'string', maxLength: 600 }, verificationAchieved: { type: 'string', maxLength: 600 },
    manualSteps: { type: 'array', items: { type: 'string', maxLength: 300 } }, deviations: { type: 'array', items: { type: 'string', maxLength: 300 } },
  },
}
const REVIEW_SCHEMA = {
  type: 'object', required: ['verdict', 'mustFix', 'notes'],
  properties: { verdict: { type: 'string', enum: ['PASS', 'PASS-WITH-NOTES', 'FAIL'] }, mustFix: { type: 'array', items: { type: 'string', maxLength: 300 } }, notes: { type: 'array', items: { type: 'string', maxLength: 300 } } },
}

function reviewPrompt(t, dev) {
  return [
    'Reviewer gate for ' + t.id + ' (Wave 8 pre-iOS cleanup).', COMMON, 'Ticket: ' + t.ticket,
    'Developer reports: ' + JSON.stringify(dev),
    'Gate: (1) every AC met; (2) BEHAVIOR-PRESERVING — confirm only shape/location/archetype changed, not logic; existing tests stay green unchanged; (3) the change actually matches the cited canonical (read it); for a dead-code delete confirm zero-consumer via grep; (4) no out-of-scope churn; encoding clean; no T-NNNN comments; (5) shape change → MANUAL_STEP nswag-regen recorded; (6) VERIFY-NOT-TRUST: run the relevant compile + tests yourself. FAIL with concrete mustFix on red, behavior change, unmet AC, or a divergence from the canonical.',
    t.reviewExtra || '',
  ].join('\n\n')
}

async function runTicket(t) {
  log(t.id + ': dev (' + t.phase + ')')
  const dev = await agent([COMMON, t.prompt].join('\n\n'), { label: 'dev:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  if (!dev) return { id: t.id, verdict: 'AGENT-LOST' }
  log(t.id + ': dev done — gating')
  const gates = [function () { return agent(reviewPrompt(t, dev), { label: 'review:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA }) }]
  if (t.securityPrompt) gates.push(function () { return agent([t.securityPrompt, COMMON, 'Ticket: ' + t.ticket, 'Dev: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged })].join('\n\n'), { label: 'sec:' + t.id, phase: t.phase, agentType: 'security', schema: REVIEW_SCHEMA }) })
  const verdicts = await parallel(gates)
  const mustFix = []
  verdicts.filter(Boolean).forEach(function (v) { (v.mustFix || []).forEach(function (f) { mustFix.push(f) }) })
  if (!verdicts.filter(Boolean).some(function (v) { return v.verdict === 'FAIL' }) && mustFix.length === 0) {
    const verdict = verdicts.filter(Boolean).some(function (v) { return v.verdict === 'PASS-WITH-NOTES' }) ? 'PASS-WITH-NOTES' : 'PASS'
    log(t.id + ': ' + verdict); return { id: t.id, verdict: verdict, dev: dev }
  }
  log(t.id + ': FAIL (' + mustFix.length + ') — fix')
  const fix = await agent([COMMON, 'Fix these blocking findings on ' + t.id + ', nothing else:', mustFix.map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'), 'Re-run the relevant compile/tests.'].join('\n\n'), { label: 'fix:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  const regate = await agent([reviewPrompt(t, fix || dev), 'RE-GATE after fix. Original: ' + mustFix.join(' | ')].join('\n\n'), { label: 'regate:' + t.id, phase: t.phase, agentType: t.securityPrompt ? 'security' : 'reviewer', schema: REVIEW_SCHEMA })
  const v = regate ? regate.verdict : 'REGATE-LOST'
  log(t.id + ': ' + v); return { id: t.id, verdict: v, dev: dev, fix: fix }
}

const tk = (id, slug, devType, phase, prompt, extra) => Object.assign({ id, ticket: 'agents/backlog/tickets/' + slug, devType, phase, prompt }, extra || {})

// 8A — auth contract (security-gated, first/alone in the Auth lane)
const T0272 = tk('T-0272', 'T-0272-auth-contract-shrink.md', 'backend', '8A auth contract',
  'Implement T-0272: shrink the AUTH WIRE CONTRACT. (1) trustedDeviceToken — owner decision: it is MOBILE-ONLY (web overwrites it from the HttpOnly cookie via RefreshTokenFromCookieOrBody, so it is dead weight on the web Login/PartnerLogin/AdminLogin commands). Drop TrustedDeviceToken from the WEB login commands; carry it on the MOBILE login path the mobile hosts use (Mobile.Customer/Mobile.Partner pass it in the body — keep that working). (2) RefreshToken.Command — it exposes server-only RequiredProfile/RequiredAudience on the wire, overwritten by ALL 5 controllers; shrink the Command to (Token) and pass RequiredProfile/RequiredAudience server-side (via IHostAudienceProvider or an internal param), exactly the pattern Login.cs uses (per-host params pulled server-side, kept off the wire). Behavior-preserving: the lockout-bypass + refresh still work identically on web AND mobile. This CHANGES the request DTO shape on multiple clients → record MANUAL_STEP nswag-regen (admin+partner+customer web clients lose the fields; mobile clients gain/keep the mobile login field). Characterization-test-first for the auth flows. Read the ticket for the exact contract shape; ADR-0001 stays in force (only the wire projection shrinks, not the authz model).',
  { securityPrompt: 'Security gate for T-0272 (auth wire-contract shrink). Confirm: (1) the trusted-device lockout-bypass STILL works — web sources the token from the HttpOnly cookie (unchanged), mobile from the body (still wired); no path lost the bypass. (2) RefreshToken still pins the correct RequiredProfile/RequiredAudience PER HOST server-side — a customer refresh token cannot now be used to mint a partner/admin session because the audience pinning moved server-side correctly (this is the security-critical part — verify each of the 5 controllers pins the right audience). (3) No field that carries a security decision is now CLIENT-controllable that was server-controlled before. FAIL if any audience/profile pinning is weakened or the bypass is lost on any host.' })

// 8B — disjoint cleanup
const T0273 = tk('T-0273', 'T-0273-paged-query-canonicalization.md', 'backend', '8B cleanup',
  'Implement T-0273: canonicalize the 7 paged-query offenders onto the DataRangeRequest + <Entity>Specification + <Entity>Sort + PagedData<T> archetype that the other ~13 GetPaged* use (canonical exemplar: Features/Services/GetPagedServices.cs + PageDataMapper). The 7: GetMyReferrals, GetLoyaltyActivity, GetUserLoyaltyActivity, GetPromoCodeRedemptions, GetEmployeeDocuments, GetPagedMembershipPlans, and any sibling the ticket names — each replaces manual page-math / bespoke repo paging with the spec path. Add the missing <Entity>Specification/Sort where none exists (LoyaltyTransaction, PromoCodeRedemption, etc.) over the right key. Behavior-preserving: SAME items, SAME paging/sorting, SAME response envelope (PagedData) — the response shape does NOT change (these already return PagedData), so NO nswag-regen. AC: node agents/tools/check-consistency.mjs --paths=src/Cleansia.Core.AppServices reports the A1/A5 flags on these GONE afterward. Retire the now-dead bespoke repo methods (GetByReferrerAsync, GetForAccountAsync, etc.) only if zero other consumers. Characterization-test-first per query. STOP-AND-SPLIT if this grows past M.',
  { reviewExtra: 'Verify each converted query returns the SAME rows in the SAME order as before (the spec + sort must reproduce the old WHERE/ORDER BY). The response envelope must be unchanged (still PagedData<T> — so no client regen). Confirm the check-consistency A1/A5 flags on these are cleared and consistency-violations.md F1b is de-staled.' })
const T0274 = tk('T-0274', 'T-0274-fe-error-resolver-dedup.md', 'frontend', '8B cleanup',
  'Implement T-0274: the error-key resolver is copy-pasted into 8 feature facades (admin order-ops, refund, dispute-detail, pay-period, payroll, invoice, package-form, customer disputes) each with its own ApiErrorResult interface. A canonical extractor already exists in SnackbarService (libs/core/services/.../snackbar.service.ts ~117-165). Add ONE shared extractApiErrorCode in @cleansia/services; each feature keeps only its thin resolveXxxErrorKey with its OWN error→key map; delete the 8 private copies + their local ApiErrorResult interfaces. Behavior-preserving: the SAME error key resolves for the SAME API error in each facade. npx nx test the touched facade libs + build the apps. STOP-AND-SPLIT if past M.')
const T0275 = tk('T-0275', 'T-0275-dead-paged-dups-and-low-drift.md', 'backend', '8B cleanup',
  'Implement T-0275: (1) DELETE dead code — GetAllEmployees.cs (zero-consumer parallel paged duplicate; grep-prove no dispatch site) and GetUserByEmail.cs (dead CQRS feature, never Send-ed; keep GetByEmailNoTrackingAsync which GetCurrentUser uses; fix the stale doc-comment in IUserRepository + the test comment). (2) LOW drift: GetPagedInvoices Include-then-AsNoTracking-then-Select order; AdminLogin nameof(command.Email) literal; delete the dead validator GetPropertyName helper. Behavior-preserving; keep the suite green. Prove every delete is zero-consumer first.')
const T0276 = tk('T-0276', 'T-0276-sitewide-push-facade.md', 'frontend', '8B cleanup',
  'Implement T-0276: sitewide-push-form.component.ts keeps HTTP/error/confirm/state IN the component (no facade) and uses raw http.post + DestroyRef. Add a SitewidePushFormFacade extending UnsubscribeControlDirective that owns submit/send/state via takeUntil(this.destroyed$); the component delegates. Use the generated AdminMarketingClient (it exists in admin-client.ts) instead of raw http.post. Behavior-preserving. If the generated client does not yet expose the needed method (DTO drift), record MANUAL_STEP nswag-regen and use the client method that exists. nx test + build the admin app.')
const T0277 = tk('T-0277', 'T-0277-android-formatters-hoist.md', 'android', '8B cleanup',
  'Implement T-0277 (:core lane — serialized vs T-0278): partner-app/features/orders/OrderDetailFormat.kt duplicates date/time/money formatters that EXIST in core/format/OrderFormatters.kt (already used by customer-app) AND diverge in rendering. Delete partner OrderDetailFormat.kt, point its 6 call sites (StatusTimeline, PaymentCard, OrderTimerCard, ScopeCard, OrderMetadataRow, ...) at cz.cleansia.core.format.*; choose the ONE canonical format (the :core one customer-app uses); add formatOrderTime to :core if the partner needs one customer lacks. Behavior note: this INTENTIONALLY unifies a divergence — confirm the chosen format is correct for both. Verify :core + :partner-app + :customer-app compile + their unit suites green.')
const T0278 = tk('T-0278', 'T-0278-android-push-token-hoist.md', 'android', '8B cleanup',
  'Implement T-0278 (:core lane — serialized vs T-0277): the push-token cluster (PushTokenRepository, PushTokenSessionObserver, DeviceApi, DeviceApiDtos) is duplicated across customer-app and partner-app, and the migration comments DISAGREE on the Firebase project. Hoist the 4 files into cz.cleansia.core.notifications behind a DeviceRegistrationClient interface each app binds (parameterize the per-app DataStore name). RECONCILE the Firebase-project constant — if it is genuinely ambiguous which project is correct, STOP and report it as an owner question (do not guess a Firebase project id). Behavior-preserving. Verify :core + both apps compile + unit suites green.')

phase('8A auth contract')
log('Wave 8: 8A (T-0272 auth contract, security-gated) || 8B (6 disjoint cleanup; T-0277<->T-0278 serial on :core). 8C (T-0280/T-0281) HELD for the owner regen.')

const results = await parallel([
  function () { return runTicket(T0272) },                                   // 8A
  function () { return runTicket(T0273) },                                   // 8B backend
  function () { return runTicket(T0274) },                                   // 8B frontend
  function () { return runTicket(T0275) },                                   // 8B backend (disjoint files from T-0273)
  function () { return runTicket(T0276) },                                   // 8B frontend
  async function () { const a = await runTicket(T0277); const b = await runTicket(T0278); return [a, b] }, // :core serial lane
])

const all = results.filter(Boolean).flat()
return {
  tickets: all.map(function (r) {
    return { id: r.id, verdict: r.verdict, files: r.dev ? r.dev.filesChanged : [], manualSteps: r.dev ? r.dev.manualSteps : [], deviations: r.dev ? r.dev.deviations : [] }
  }),
}
