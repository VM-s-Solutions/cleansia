export const meta = {
  name: 'wave6',
  description: 'Wave 6 — 12 tickets: multi-tenant token-revoke blocker (T-0236), security follow-ups, frontend hygiene, catalog-delete TOCTOU, cancellation-fee semantics (T-0242), lockout-DoS (panel-first T-0233)',
  phases: [
    { title: '6A blocker+cleanups', detail: 'T-0236 token-revoke, T-0262 dead const, T-0240 gitignore' },
    { title: '6B backend follow-ups', detail: 'T-0260 chargeback-guard, T-0234 pw-guess bound, T-0238 invoice PDF DTO, T-0261 index' },
    { title: '6C frontend hygiene', detail: 'T-0259 nx-lib infra, T-0239 module-boundary, T-0241 eslint prefix' },
    { title: '6D catalog TOCTOU', detail: 'T-0237 FK Cascade->Restrict' },
    { title: '6E money + lockout', detail: 'T-0242 cancellation-fee invert, T-0233 lockout-DoS (panel-first)' },
  ],
}

const ROOT = 'c:/Users/cmisa/Desktop/Mike/Projects/cleansia'

const COMMON = [
  'Repo root: ' + ROOT + '. Branch feature/wave-6 is ALREADY checked out (sequencing committed). Do NOT switch branches, commit, push, or git add. MANY lanes run concurrently in this one tree — touch ONLY the files your ticket owns. Uncommitted changes that are not yours are EXPECTED; never revert/stage them.',
  'Today is 2026-06-14. Read first: your ticket IN FULL, agents/knowledge/testing.md, your stack catalog, and any S1-S10 law your ticket touches.',
  'TDD: where you change behavior or fix a bug, write the failing test FIRST (real red, capture the failure), then make it green. Where you refactor behavior-preservingly, pin with a characterization test and keep existing tests green UNCHANGED. Record honest red->green in the ticket status log.',
  'Keep changes surgical and on-scope. Found an unrelated real bug? Report it, do not fix it.',
  'ENVIRONMENT TRAP: VS/devenv + running hosts hold locks on shared host DLLs → MSB3021/MSB3027 on solution-graph builds. Build/test your SPECIFIC project (dotnet build <proj>.csproj; dotnet test <proj>.csproj --no-build, or -p:BuildProjectReferences=false). Frontend: npx nx test/build <project> from src/Cleansia.App (app project names: cleansia.app, cleansia-partner.app, cleansia-admin.app). Real Postgres via Testcontainers is available (Docker up). KNOWN SEED TRAP: seeding a User via raw SaveChangesAsync skips the audit interceptor → CreatedBy NOT NULL (23502); call user.Created(...); and a User.PreferredLanguageCode FKs to Languages so seed Language.Create("en","English") first. Report which verification you achieved; the orchestrator does the authoritative clean run.',
  'Comment discipline: NO ticket IDs (T-NNNN / TC-NN / // ACn) in source. Load-bearing ADR-NNNN / S1-S10 only.',
  'OWNER-ONLY (never run): dotnet ef migrations/database, npm run generate-*-client, edits to NSwag clients, edits to DB seeds. Response/DTO shape change → MANUAL_STEP nswag-regen. Schema/index/FK change → write the EF entity-config/migration-intent but record MANUAL_STEP ef-migration; do NOT run dotnet ef.',
  'i18n: any new user-visible string needs a TranslatePipe key in ALL 5 locales of the relevant app.',
  'Update your ticket: status -> review, append status-log. Final message is data for the orchestrator.',
].join('\n')

const DEV_SCHEMA = {
  type: 'object',
  required: ['summary', 'filesChanged', 'testEvidence', 'verificationAchieved', 'deviations', 'manualSteps', 'productionBugsFound'],
  properties: {
    summary: { type: 'string' }, filesChanged: { type: 'array', items: { type: 'string' } },
    testEvidence: { type: 'string' }, verificationAchieved: { type: 'string' },
    deviations: { type: 'array', items: { type: 'string' } }, manualSteps: { type: 'array', items: { type: 'string' } },
    productionBugsFound: { type: 'array', items: { type: 'string' } },
  },
}
const REVIEW_SCHEMA = {
  type: 'object', required: ['verdict', 'mustFix', 'notes'],
  properties: { verdict: { type: 'string', enum: ['PASS', 'PASS-WITH-NOTES', 'FAIL'] }, mustFix: { type: 'array', items: { type: 'string' } }, notes: { type: 'array', items: { type: 'string' } } },
}

function reviewPrompt(t, dev) {
  return [
    'You are the reviewer gate for ticket ' + t.id + ' on the Cleansia repo.',
    COMMON, 'Ticket file: ' + t.ticket,
    'Developer reports: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged, testEvidence: dev.testEvidence, verificationAchieved: dev.verificationAchieved, deviations: dev.deviations }),
    'Review ONLY developer-listed files (git diff -- <file>; ignore other working-tree changes).',
    'Gate: (1) every AC met or justified deviation; (2) bug-fix tickets — the regression test actually reproduces the bug (would FAIL pre-fix); refactor tickets — behavior-preserving, existing tests green unchanged; (3) no vacuous tests; (4) conventions (no T-NNNN comments, i18n 5 locales, cleansia-*/PrimeNG+OnPush+facade for FE); (5) no unauthorized migration/nswag/DTO edit (shape change → MANUAL_STEP recorded; ef change → entity-config written + MANUAL_STEP, not run); (6) VERIFY-NOT-TRUST: run the dev tests/build yourself where the env allows.',
    t.reviewExtra || '',
  ].join('\n\n')
}

async function runTicket(t) {
  log(t.id + ': dev starting (' + t.phase + ')')
  const dev = await agent([COMMON, t.prompt].join('\n\n'), { label: 'dev:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  if (!dev) return { id: t.id, verdict: 'AGENT-LOST' }
  log(t.id + ': dev done — gating')
  const gates = [function () { return agent(reviewPrompt(t, dev), { label: 'review:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA }) }]
  if (t.securityPrompt) gates.push(function () { return agent([t.securityPrompt, COMMON, 'Ticket: ' + t.ticket, 'Dev report: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged, testEvidence: dev.testEvidence })].join('\n\n'), { label: 'sec:' + t.id, phase: t.phase, agentType: 'security', schema: REVIEW_SCHEMA }) })
  if (t.optimizerPrompt) gates.push(function () { return agent([t.optimizerPrompt, COMMON, 'Ticket: ' + t.ticket, 'Dev report: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged })].join('\n\n'), { label: 'opt:' + t.id, phase: t.phase, agentType: 'optimizer', schema: REVIEW_SCHEMA }) })
  const verdicts = await parallel(gates)
  const mustFix = []
  verdicts.filter(Boolean).forEach(function (v) { (v.mustFix || []).forEach(function (f) { mustFix.push(f) }) })
  const failed = verdicts.filter(Boolean).some(function (v) { return v.verdict === 'FAIL' })
  if (!failed && mustFix.length === 0) {
    const verdict = verdicts.filter(Boolean).some(function (v) { return v.verdict === 'PASS-WITH-NOTES' }) ? 'PASS-WITH-NOTES' : 'PASS'
    log(t.id + ': ' + verdict); return { id: t.id, verdict: verdict, dev: dev }
  }
  log(t.id + ': FAIL (' + mustFix.length + ') — fix lane')
  const fix = await agent(['Fix review findings on ticket ' + t.id + ' (Cleansia). Dev work is in the tree.', COMMON, 'Ticket file: ' + t.ticket, 'Fix ALL of these, nothing else:', mustFix.map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'), 'Re-run affected tests. Update the status log.'].join('\n\n'), { label: 'fix:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  const regate = await agent(['RE-GATING ' + t.id + '. Original findings:', mustFix.map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'), 'Fixer: ' + JSON.stringify(fix ? { summary: fix.summary, filesChanged: fix.filesChanged, verificationAchieved: fix.verificationAchieved } : null), COMMON, 'Ticket: ' + t.ticket, 'Verify each finding resolved. FAIL only if a blocking finding survives.'].join('\n\n'), { label: 'regate:' + t.id, phase: t.phase, agentType: t.securityPrompt ? 'security' : 'reviewer', schema: REVIEW_SCHEMA })
  const v = regate ? regate.verdict : 'REGATE-LOST'
  log(t.id + ': ' + v); return { id: t.id, verdict: v, dev: dev, fix: fix }
}

const tk = (id, slug, devType, phase, prompt, extra) => Object.assign({ id, ticket: 'agents/backlog/tickets/' + slug, devType, phase, prompt }, extra || {})

// ---- 6A ----
const T0236 = tk('T-0236', 'T-0236-multitenant-token-revoke-asymmetry.md', 'backend', '6A blocker+cleanups',
  'Implement T-0236 (MULTI-TENANT GO-LIVE BLOCKER): the token-revoke tenancy asymmetry. Read the ticket in full. The defect class: token issuance and revoke/read paths must agree on tenancy, else a revoke silently updates zero rows (a logged-out/compromised token keeps working) in multi-tenant mode. Implement the architect-frozen contract from the ticket (issuance-stamp vs IgnoreQueryFilters at the revoke read — follow what the ticket specifies). Write a tenant-context test proving revoke actually updates the row (asserts affected-rows > 0 / the token is dead), plus a cross-tenant/cross-user rejection test. This is the partner to the fixed T-0245. Lane: Auth-token (you are the sole writer of the refresh-token/revoke path this wave). If AC requires a backfill, MANUAL_STEP ef-migration.',
  { securityPrompt: 'Security gate for T-0236 (multi-tenant token-revoke). Confirm: a revoke in tenant T actually invalidates the token (the read used by revoke is NOT silently tenant-filtered to zero rows); a token cannot be revoked/used across tenants; null-tenant (single-tenant) legacy tokens still revoke correctly (no regression). The test must prove the affected-row count, not just that a method was called. FAIL if revoke can no-op silently in any tenant configuration.' })
const T0262 = tk('T-0262', 'T-0262-remove-dead-emailnotsenterror-constant.md', 'backend', '6A blocker+cleanups',
  'Implement T-0262: remove the dead BusinessErrorMessage.EmailNotSentError constant (zero consumers repo-wide — verify with grep first). Remove any orphaned errors.email.* locale keys it mapped to (all 5 locales of each app that had them). Build + tests green. You OWN BusinessErrorMessage.cs this batch ahead of T-0234 (which adds a constant) — keep your change a pure deletion; T-0234 runs after you in the same lane.')
const T0240 = tk('T-0240', 'T-0240-android-kotlin-dir-gitignore.md', 'android', '6A blocker+cleanups',
  'Implement T-0240: add the .kotlin build dir to .gitignore for the Android modules and remove any currently-tracked .kotlin artifacts from the index (git rm --cached pattern is owner-territory — instead, just edit .gitignore and REPORT any tracked .kotlin files you find as a MANUAL_STEP for the owner to untrack, since git add/rm is orchestrator-only). Minimal.')

// ---- 6B ----
const T0260 = tk('T-0260', 'T-0260-chargeback-dispute-transition-funnel-guard.md', 'backend', '6B backend follow-ups',
  'Implement T-0260 (defense-in-depth): funnel HandlePaymentNotification.HandleChargeback dispute-terminal write through the T-0172 CanTransitionTo guard instead of calling dispute.Escalate directly. Behavior-preserving for the legal Pending->Escalated path (characterization-pin it); an illegal start-state now rejected via the guard. Remove the chargeback entry from the T-0247 check-consistency B10 allowlist so the rule enforces it going forward. Lane: Dispute-guard. Sec gate applies.',
  { securityPrompt: 'Security gate for T-0260. Confirm the chargeback dispute write now routes through the guarded transition (CanTransitionTo), the legal path is unchanged, an illegal start-state is rejected, and Resolve remains the sole resolution-field writer. Confirm the B10 allowlist no longer exempts HandleChargeback. FAIL if a terminal dispute write can still bypass the guard.' })
const T0234 = tk('T-0234', 'T-0234-changeownpassword-guess-bound.md', 'backend', '6B backend follow-ups',
  'Implement T-0234: bound the ChangeOwnPassword current-password guessing. Per the ticket, add a per-account attempt budget on the current-password check (default: REUSE the existing lockout pair User.FailedLoginAttempts/LockoutEndsAt so NO migration is needed — confirm the ticket allows this). Atomic conditional update; failure paths must not reach the UoW commit. Red-first tests proving the (N+1)th wrong current-password attempt is refused. Lane: Auth-surface (after T-0236 token work if it touches the same file — coordinate; and BusinessErrorMessage lane AFTER T-0262). If you must add a new constant, add it cleanly after T-0262 deleted the dead one.',
  { securityPrompt: 'Security gate for T-0234. Confirm the guess bound is real (the cap is charged before the validity check, atomic, and the failure path never commits a partial change), reuses the lockout primitives without a new oracle, and does not lock out the legit user changing their password normally. FAIL if the bound can be bypassed or introduces a new enumeration oracle.' })
const T0238 = tk('T-0238', 'T-0238-invoice-pdf-failure-dto-fields.md', 'backend', '6B backend follow-ups',
  'Implement T-0238: add PdfGenerationFailed / PdfGenerationError fields to the admin EmployeeInvoice DTO(s) so the admin UI can distinguish a failed PDF from a still-pending one (closes the T-0171d AC4 proxy / Q-W3-3). Backend DTO + mapper + the handler populating them. This CHANGES the admin response DTO shape → MANUAL_STEP nswag-regen (admin client); do the BACKEND half only and flag the frontend-render half as held on the regen (note it in the ticket). Tests for the populated fields.')
const T0261 = tk('T-0261', 'T-0261-usermembership-partial-index-cancellation-reminder-arm.md', 'db', '6B backend follow-ups',
  'Implement T-0261: the UserMembership index does not cover the cancellation-reminder sweep arm (CancelledAt!=null AND CancellationReminderSentAt==null AND Status==Active AND CurrentPeriodEnd in range) — it falls to a seq scan. Add the appropriate index (entity-config declaration) so that sweep is index-served, per the ticket. Schema change → write the EF entity-config index + record MANUAL_STEP ef-migration (do NOT run dotnet ef). An optimizer gate checks the index matches the sweep predicate.',
  { optimizerPrompt: 'Optimizer gate for T-0261. Confirm the new index column set + any partial-WHERE matches the cancellation-reminder sweep predicate (so EXPLAIN would use it), without redundantly duplicating the existing renewal partial index. FAIL if the index would not serve the cancellation sweep.' })

// ---- 6C ----
const T0259 = tk('T-0259', 'T-0259-frontend-nx-lib-test-infra-scaffolding.md', 'frontend', '6C frontend hygiene',
  'Implement T-0259: scaffold proper nx test/lint targets for the under-configured libs (loyalty-promo-codes: add tags + jest.config.ts + tsconfig.spec.json + eslint.config.mjs; customer login + forgot-password + partner-forgot-password: proper test targets — note T-0251/T-0198 already fixed the customer login/forgot JEST PATH drift, so reconcile with current state, do not re-break). Goal: nx test + nx lint run green for each. You OWN the FE-config lane ahead of T-0239 (which needs the boundary tags) — run first. Verify with nx test/lint on each touched project.')
const T0239 = tk('T-0239', 'T-0239-module-boundary-sweep-partner-services.md', 'frontend', '6C frontend hygiene',
  'Implement T-0239: the module-boundary sweep — remove all @cleansia/partner-services imports from customer features (the cross-app client coupling; T-0202 already did the disputes one — sweep the remaining ~13 files per the ticket onto @cleansia/customer-services). Add/enable the enforce-module-boundaries lint rule so it trips on a fixture then passes clean. T-0259 ran before you and added the lib tags the rule needs — build on that. Verify: customer SSR prod build (npx nx build cleansia.app --configuration=production) green + nx lint clean.',
  { reviewExtra: 'Confirm ZERO @cleansia/partner-services imports remain under libs/cleansia-customer-features/** after the sweep, and the enforce-module-boundaries rule actually trips (test it on a fixture) so future drift fails CI.' })
const T0241 = tk('T-0241', 'T-0241-admin-eslint-selector-prefix.md', 'frontend', '6C frontend hygiene',
  'Implement T-0241: align the admin eslint selector-prefix (components/directives use the cleansia prefix) + set the Nx generator default so new admin code is compliant. Fix existing violations. nx lint + build green for the admin app. If it shares the nx.json generators block with T-0259, coordinate (run after T-0259).')

// ---- 6D ----
const T0237 = tk('T-0237', 'T-0237-catalog-delete-toctou-fk-restrict.md', 'backend', '6D catalog TOCTOU',
  'Implement T-0237: the catalog-delete TOCTOU — change the Service/Package FK delete behavior from Cascade to Restrict so a reference inserted AFTER the in-use check is rejected at commit (not silently orphaned/cascaded), map the resulting FK violation (23503) to service.in_use / package.in_use business errors, and add the RecurringBookingTemplate JSON-id in-use check. Real-DB integration test: insert a referencing row post-check, assert the delete is rejected + mapped (no orphan, no 500). Schema change (FK onDelete) → write the EF entity-config + MANUAL_STEP ef-migration (do NOT run dotnet ef). Sec gate applies (data-integrity).',
  { securityPrompt: 'Security/integrity gate for T-0237. Confirm: the FK is Restrict (a post-check insert is rejected at commit, no cascade-orphan, no 500), the 23503 is mapped to the in_use business error (not leaked), and the template JSON-id check catches template-referenced items. FAIL if a delete can still orphan or 500.' })

// ---- 6E ----
const T0242 = tk('T-0242', 'T-0242-cancellation-fee-override-semantics.md', 'backend', '6E money + lockout',
  'Implement T-0242 (Q-W5-1 ANSWERED — path b): INVERT the freeCancellationHoursOverride semantics in BookingPolicy.CalculateCancellationFeeRate so a LARGER override WIDENS the free-cancellation window (Plus members get a MORE generous window — cancel-free further out), matching the doc intent, NOT the current literal-replacement that made it stricter. Then RE-FLIP T-0211 CancellationFeeRateBoundaryTests (src/Cleansia.Tests/Features/Orders/CancellationFeeRateBoundaryTests.cs) which currently PIN the buggy direction — they must now assert the corrected intent (Plus order cancelled inside the widened window = 0 fee; standard tier unchanged; partial/last-minute tiers + refund formula unchanged). You OWN BookingPolicy.cs alone this wave. Adversarial money review applies — expected values hand-derived, tests must catch a re-inversion.',
  { securityPrompt: 'You are the ADVERSARIAL MONEY reviewer for T-0242. The fix inverts the Plus free-cancellation-window direction. Verify: (1) a Plus order cancelled at a time that is FREE under the widened window but WOULD HAVE BEEN charged under the old literal-replacement is now 0 fee — and a test pins exactly that boundary with a hand-derived expectation; (2) the standard (non-Plus) tier is completely unchanged; (3) the partial/last-minute fee tiers and the refundAmount = TotalPrice*(1-feeRate) formula are untouched; (4) the re-flipped tests would FAIL if someone re-inverted the semantics back. FAIL if any test is vacuous or the standard tier regressed.' })
const T0233 = tk('T-0233', 'T-0233-lockout-dos-mitigation.md', 'backend', '6E money + lockout',
  'Implement T-0233 (lockout-DoS mitigation) AFTER the analyst panel has decided the approach. The panel decision will be provided/already recorded in the ticket — read the ticket status log for the chosen mechanism (trusted-device-cookie vs CAPTCHA vs both). Implement the chosen mechanism so a legit user with a trusted device/passed challenge bypasses the lockout while untrusted credential-spraying is unchanged; cover all 3 login surfaces (customer/partner/admin). Lane: Auth-surface (after T-0234). Red-first tests. Sec gate applies. If the panel mandated a schema change, MANUAL_STEP ef-migration.',
  { securityPrompt: 'Security gate for T-0233 (lockout-DoS). Confirm the mitigation does NOT create a new bypass: an attacker without the trusted-device proof / without passing the challenge is still subject to lockout; the trusted-device token cannot be forged or replayed across accounts; no new account-enumeration oracle is introduced; all 3 login surfaces covered. FAIL on any new bypass or oracle.' })

// ---- execution ----
log('Wave 6 dispatch. Lanes: Auth-token(T-0236) | BErrMsg+locale(T-0262->T-0234) | Dispute-guard(T-0260) | FE-config(T-0259->T-0239, then T-0241) | BookingPolicy(T-0242) | panel+impl(T-0233 after T-0234).')

// T-0233 is panel-first: run the analyst panel, then implement. We chain it AFTER T-0234 (shared Auth-surface).
async function lockoutLane(panelDone) {
  await panelDone // wait for T-0234 to vacate the Auth-surface lane
  log('T-0233: analyst panel starting (lockout-DoS approach decision)')
  const panel = await agent([
    'You are the analyst LEAD convening the deliberation panel for ticket T-0233 (lockout-DoS mitigation) on the Cleansia repo.',
    COMMON, 'Ticket file: agents/backlog/tickets/T-0233-lockout-dos-mitigation.md',
    'The ticket body mandates a panel decision on the mitigation approach: trusted-device-cookie vs CAPTCHA vs both. Weigh: UX friction, the existing account-lockout (T-0193: 5 fails/15min via User.FailedLoginAttempts/LockoutEndsAt), the 3 login surfaces (customer/partner/admin), implementability without a heavy new dependency, and security (must not create a new bypass or enumeration oracle). Decide the approach and write a concise decision (chosen mechanism + why + the implementation shape: where the trusted-device token lives, how it is validated, what bypasses lockout) into the ticket status log. Return the chosen mechanism + a 3-5 bullet implementation spec the backend dev will follow.',
  ].join('\n\n'), { label: 'panel:T-0233', phase: '6E money + lockout', agentType: 'analyst' })
  log('T-0233: panel decided — implementing')
  return runTicket(T0233)
}

// BErrMsg+locale + Auth-surface lane: T-0262 (delete dead const) MUST finish before T-0234 (may add a
// const) — both edit BusinessErrorMessage.cs. The lockout lane (T-0233) waits on T-0234 vacating
// Auth-surface, so expose the chain's T-0234 result via a shared promise.
let resolveAuth
const authSurface = new Promise(function (r) { resolveAuth = r })
const berrLane = (async function () {
  const a = await runTicket(T0262)
  const b = await runTicket(T0234)
  resolveAuth(b)
  return [a, b]
})()

const results = await parallel([
  // 6A
  function () { return runTicket(T0236) },
  function () { return berrLane },
  function () { return runTicket(T0240) },
  // 6B
  function () { return runTicket(T0260) },
  function () { return runTicket(T0238) },
  function () { return runTicket(T0261) },
  // 6C FE-config serial then prefix
  async function () { const a = await runTicket(T0259); const b = await runTicket(T0239); const c = await runTicket(T0241); return [a, b, c] },
  // 6D
  function () { return runTicket(T0237) },
  // 6E money
  function () { return runTicket(T0242) },
  // 6E lockout: panel + impl, after T-0234 vacates Auth-surface
  function () { return lockoutLane(authSurface) },
])

const flat = results.filter(Boolean).flat()
return {
  tickets: flat.map(function (r) {
    return { id: r.id, verdict: r.verdict, filesChanged: r.dev ? r.dev.filesChanged : [], manualSteps: r.dev ? r.dev.manualSteps : [], deviations: r.dev ? r.dev.deviations : [], productionBugs: r.dev ? r.dev.productionBugsFound : [], fixFiles: r.fix ? r.fix.filesChanged : [] }
  }),
}
