export const meta = {
  name: 'wave5-batch-5bcde',
  description: 'Wave 5 concurrent batches 5B+5C+5D+5E — consistency sweep, AUD-06 CreateOrder decomposition, de-triplication. ~14 tickets across serial lanes.',
  phases: [
    { title: '5B micro-fixes', detail: 'membership chain, variable-symbol, dead-code, logging-PII' },
    { title: '5C sweep base', detail: 'paged-query, response-wrap, validator-base, customer-facades, android states' },
    { title: '5D AUD-06', detail: 'CreateOrder decomposition, serial, lane-isolated, T-0212 gate' },
    { title: '5E de-triplication', detail: 'auth/dispute/saved-address + AddSavedAddress (SavedAddress lane serial)' },
  ],
}

const ROOT = 'c:/Users/cmisa/Desktop/Mike/Projects/cleansia'

const COMMON = [
  'Repo root: ' + ROOT + '. Branch feature/wave-5-consistency-bugs is ALREADY checked out (5A bug fixes + L-split already committed). Do NOT switch branches, commit, push, or git add. MANY lanes run concurrently in this one tree — touch ONLY the files your ticket owns. Uncommitted changes that are not yours are EXPECTED; never revert or stage them.',
  'Today is 2026-06-13. Read first: your ticket IN FULL, agents/knowledge/testing.md, and your stack catalog (patterns-backend.md / patterns-frontend.md / the Android charter).',
  'This is the CONSISTENCY SWEEP + a god-unit DECOMPOSITION. Most tickets CHANGE production code to a canonical form WITHOUT changing behavior. The discipline is: behavior-preserving refactor under test. Where a characterization/regression test already exists (e.g. T-0212 for CreateOrder), it MUST stay green UNCHANGED — that is your safety net, do not edit it to make it pass. Where none exists for the seam you change, add a focused test that pins the behavior first.',
  'Keep changes surgical and on-scope. No opportunistic edits outside your ticket. If you find a real bug, do NOT fix it here — report it for a follow-up ticket.',
  'ENVIRONMENT TRAP: a Visual Studio (devenv) + running hosts hold locks on shared host output DLLs → MSB3021/MSB3027 on solution-graph builds. Build/test your SPECIFIC project: dotnet build <proj>.csproj then dotnet test <proj>.csproj --no-build (or -p:BuildProjectReferences=false). Frontend: npx nx test/build <project> from src/Cleansia.App. Report which verification you achieved; the orchestrator does the authoritative clean run. Known flake: Cleansia.Tests IntegrationFailureMetricsTests is parallel-sensitive — rerun a full run with -- xUnit.parallelizeTestCollections=false if it bites.',
  'Comment discipline: NO ticket IDs (T-NNNN / TC-NN / // ACn as the whole comment) in source. Load-bearing ADR-NNNN / S1-S10 only.',
  'OWNER-ONLY (never run): dotnet ef migrations/database, npm run generate-*-client, edits to NSwag-generated client files, edits to DB seeds. If your ticket changes an API response/DTO shape, record a MANUAL_STEP nswag-regen (do NOT regen, do NOT hand-edit the client). If it needs a schema change, record MANUAL_STEP ef-migration.',
  'i18n: any new user-visible string needs a TranslatePipe key in ALL 5 locales of the relevant app (en/cs/sk/uk/ru).',
  'Update your ticket: status -> review, append status-log (what changed, test evidence, deviations, MANUAL_STEPs). Your final message is data for the orchestrator.',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

const DEV_SCHEMA = {
  type: 'object',
  required: ['summary', 'filesChanged', 'behaviorPreserved', 'testEvidence', 'verificationAchieved', 'deviations', 'manualSteps', 'productionBugsFound'],
  properties: {
    summary: { type: 'string', maxLength: 600 },
    filesChanged: { type: 'array', items: { type: 'string', maxLength: 300 } },
    behaviorPreserved: { type: 'string', maxLength: 600, description: 'how you confirmed the change does not alter behavior (which tests, which characterization suite stayed green)' },
    testEvidence: { type: 'array', items: { type: 'string', maxLength: 300 }, description: 'short pointers: suite + counts + one-line verdict, never raw logs' },
    verificationAchieved: { type: 'string', maxLength: 600 },
    deviations: { type: 'array', items: { type: 'string', maxLength: 300 } },
    manualSteps: { type: 'array', items: { type: 'string', maxLength: 300 } },
    productionBugsFound: { type: 'array', items: { type: 'string', maxLength: 300 } },
  },
}

const REVIEW_SCHEMA = {
  type: 'object',
  required: ['verdict', 'mustFix', 'notes'],
  properties: {
    verdict: { type: 'string', enum: ['PASS', 'PASS-WITH-NOTES', 'FAIL'] },
    mustFix: { type: 'array', items: { type: 'string', maxLength: 300 } },
    notes: { type: 'array', items: { type: 'string', maxLength: 300 } },
  },
}

function reviewPrompt(t, dev) {
  return [
    'You are the reviewer gate for ticket ' + t.id + ' on the Cleansia repo.',
    COMMON,
    'Ticket file: ' + t.ticket,
    'Developer reports: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged, behaviorPreserved: dev.behaviorPreserved, testEvidence: dev.testEvidence, verificationAchieved: dev.verificationAchieved, deviations: dev.deviations }),
    'Review ONLY the developer-listed files (git diff -- <file>; ignore other working-tree changes — concurrent lanes).',
    'Gate: (1) every ticket AC met or a recorded justified deviation; (2) for a behavior-preserving refactor: confirm the change really is behavior-preserving — the characterization/existing tests cover the seam and stayed green UNCHANGED (if the dev edited a characterization test to make it pass, that is a FAIL); (3) canonical-form tickets: the change actually matches the canonical pattern the ticket cites (read the cited exemplar); (4) no unauthorized migration/nswag/DTO edit (if a response shape changed, a MANUAL_STEP must be recorded); (5) conventions — no T-NNNN in source comments, naming, i18n in all 5 locales for new strings; (6) VERIFY-NOT-TRUST: run the dev tests yourself where the env allows.',
    t.reviewExtra || '',
  ].join('\n\n')
}

function fixPrompt(t, mustFix) {
  return [
    'You are fixing review findings on ticket ' + t.id + ' (Cleansia repo). Dev work is in the tree.',
    COMMON, 'Ticket file: ' + t.ticket,
    'Fix ALL of these blocking findings, nothing else:',
    mustFix.map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'),
    'Re-run the affected tests. Update the ticket status log.',
  ].join('\n\n')
}

async function runTicket(t) {
  log(t.id + ': dev starting (' + t.phase + ')')
  const dev = await agent([COMMON, t.prompt].join('\n\n'), { label: 'dev:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  if (!dev) return { id: t.id, verdict: 'AGENT-LOST' }
  log(t.id + ': dev done — gating')
  const gates = [function () { return agent(reviewPrompt(t, dev), { label: 'review:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA }) }]
  if (t.securityPrompt) {
    gates.push(function () { return agent([t.securityPrompt, COMMON, 'Ticket: ' + t.ticket, 'Dev report: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged })].join('\n\n'), { label: 'sec:' + t.id, phase: t.phase, agentType: 'security', schema: REVIEW_SCHEMA }) })
  }
  const verdicts = await parallel(gates)
  const mustFix = []
  verdicts.filter(Boolean).forEach(function (v) { (v.mustFix || []).forEach(function (f) { mustFix.push(f) }) })
  const failed = verdicts.filter(Boolean).some(function (v) { return v.verdict === 'FAIL' })
  if (!failed && mustFix.length === 0) {
    const verdict = verdicts.filter(Boolean).some(function (v) { return v.verdict === 'PASS-WITH-NOTES' }) ? 'PASS-WITH-NOTES' : 'PASS'
    log(t.id + ': ' + verdict)
    return { id: t.id, verdict: verdict, dev: dev }
  }
  log(t.id + ': FAIL (' + mustFix.length + ') — fix lane')
  const fix = await agent(fixPrompt(t, mustFix), { label: 'fix:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  const regate = await agent([
    'RE-GATING ' + t.id + ' after fix. Original blocking findings:',
    mustFix.map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'),
    'Fixer: ' + JSON.stringify(fix ? { summary: fix.summary, filesChanged: fix.filesChanged, verificationAchieved: fix.verificationAchieved } : null),
    COMMON, 'Ticket: ' + t.ticket, 'Verify each finding resolved (run tests). FAIL only if a blocking finding survives.',
  ].join('\n\n'), { label: 'regate:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA })
  const v = regate ? regate.verdict : 'REGATE-LOST'
  log(t.id + ': ' + v)
  return { id: t.id, verdict: v, dev: dev, fix: fix }
}

// ---- ticket defs ----
const tk = (id, slug, devType, phase, prompt, extra) => ({
  id, ticket: 'agents/backlog/tickets/' + slug, devType, phase, prompt,
  reviewExtra: extra && extra.reviewExtra, securityPrompt: extra && extra.securityPrompt,
})

// 5B
const T0243 = tk('T-0243', 'T-0243-checkout-session-nameof-b5.md', 'backend', '5B micro-fixes',
  'Fix T-0243 (B5 nameof): in CreateMembershipCheckoutSession.cs the UserNotFound failure is built with nameof(Command) instead of the offending field nameof(userId). Mechanical rename to the offending field, mirroring the fix T-0179 already made in CreateMembershipSubscription.cs. No functional change. Add or extend a tiny validator/handler assertion pinning the corrected error field if the existing tests make it easy; otherwise the rename + a build is sufficient. You OWN CreateMembershipCheckoutSession.cs this batch (T-0203 runs AFTER you in the same lane — keep your change minimal and additive).')
const T0203 = tk('T-0203', 'T-0203-lg-da-ia-long-tail.md', 'backend', '5B micro-fixes',
  'Implement T-0203 (LG/DA/IA long tail): the consistency fixes the ticket enumerates (B5/B1/CQRS/magic-string smells; LG-02 ledger ManualRevoke; LG-14 surface the error). Behavior-preserving except where an AC explicitly fixes a swallowed-error bug. T-0243 ran before you and already fixed the CreateMembershipCheckoutSession nameof — rebase on that, do not redo it. If any change alters an API response/DTO shape, record MANUAL_STEP nswag-regen. Own the files the ticket names; CreateMembershipCheckoutSession.cs is shared with T-0243 (already done) — coordinate by building on its state.',
  { reviewExtra: 'LG-02 (ManualRevoke ledger) and LG-14 (surface the error) are the two genuine behavior fixes — confirm each has a test. The rest is consistency; confirm behavior-preserving.' })
const T0244 = tk('T-0244', 'T-0244-variable-symbol-stable-hash.md', 'backend', '5B micro-fixes',
  'Implement T-0244: EmployeeInvoice.GenerateVariableSymbol uses string.GetHashCode() which .NET randomizes per process — a fiscal-reference correctness trap if ever recomputed cross-process. Replace the GetHashCode basis with a DETERMINISTIC stable hash (e.g. a stable FNV-1a / SHA-derived numeric) preserving the existing 10-digit shape, OR (if the ticket prefers) persist-and-never-recompute. Add a test asserting cross-invocation determinism (same inputs -> same symbol across separate computations) AND the 10-digit shape. An adversarial money reviewer will check the test is not vacuous (the expected value must be independently derivable, and the test must FAIL against the old GetHashCode basis). If you choose the persist path, record MANUAL_STEP ef-migration. Own EmployeeInvoice.cs + the new test.',
  { securityPrompt: 'You are the adversarial money/correctness reviewer for T-0244. Verify the new stable-hash test would FAIL against the old GetHashCode() implementation (i.e. it actually pins determinism, not just shape). Confirm the new hash is genuinely process-stable (no GetHashCode, no Random, no DateTime). Confirm the 10-digit shape and any uniqueness property the variable symbol needs are preserved. FAIL with concrete mustFix if the test is vacuous or the hash is not deterministic.' })
const T0205 = tk('T-0205', 'T-0205-blind-4-9-10-11.md', 'backend', '5B micro-fixes',
  'Implement T-0205 (BLIND-4/9/10/11): remove dead code across 4 disjoint surfaces — the dead Handlebars email engine, the dead SendGrid factory member, the FCM log-spam, and the 13-file Android scrap tree (src/cleansia_android/scrap/partner-app-pre-rebuild/). Backend + mobile, all disjoint. Build + existing tests must stay green. RECONCILE FIRST: check whether T-0144 already removed the dead SendGrid factory member (the ticket flags this) — if so, skip that sub-item and note it. For the Android scrap removal, just delete the tree (it declares duplicate packages and ships nothing). Own only these 4 surfaces.')
const T0206 = tk('T-0206', 'T-0206-f10-blind-3-ida-sec-05-logging.md', 'backend', '5B micro-fixes',
  'Implement T-0206 (F10/BLIND-3/IDA/SEC-05 logging): ensure no PII/secret (confirmation/reset codes, email bodies, Stripe ids, SendGrid payloads, raw queue payloads) is logged above Debug level. Behavior-preserving: log EVENT names + scalar ids stay; sensitive VALUES move to Debug or are removed. Backend + Functions. A security advisory (S6) will check you did not miss a sink and did not break a load-bearing log. Own the logging call sites the ticket enumerates.',
  { securityPrompt: 'Security advisory (S6 logging) for T-0206: scan the changed log statements — does any Info/Warning/Error sink still emit a code, token, email body, Stripe id, or raw payload? Did the change remove a log that was load-bearing for incident response (keep event + scalar id, drop the value)? Advisory verdict; FAIL only if a PII/secret sink survives at >= Info.' })

// 5C (sweep base) — all parallel, disjoint
const T0248 = tk('T-0248', 'T-0248-consistency-paged-query-a.md', 'backend', '5C sweep base',
  'Implement T-0248 (5C.A paged-query canonicalization): bring the A* paged queries (PromoCodes/Referrals/PayConfigs/Services per the ticket) to the canonical Request/Filter/Sort/Spec + PagedData form the ticket cites. Behavior-preserving — same results, same paging/sorting. Pin with characterization tests where the seam lacks them. If a Request/Response DTO shape changes, record MANUAL_STEP nswag-regen. Own only the paged-query files the ticket enumerates.')
const T0249 = tk('T-0249', 'T-0249-consistency-response-wrap-b1.md', 'backend', '5C sweep base',
  'Implement T-0249 (5C.B response-wrap): wrap the bare command scalar/flag returns in a record Response (CreateDispute / UpdateDisputeStatus / DeleteSavedAddress per the ticket). This CHANGES the API response shape -> you MUST record MANUAL_STEP nswag-regen for each affected client (admin/customer/partner as applicable). Behavior-preserving otherwise. NOTE: DeleteSavedAddress is in the SavedAddress lane shared with T-0201/T-0198 which run in OTHER lanes of this same batch — those are SERIALIZED to run AFTER you (you go first), so just make your change cleanly; do not touch AddSavedAddress or the SavedAddress handlers beyond DeleteSavedAddress response-wrap. T-0249 blocks T-0202.')
const T0250 = tk('T-0250', 'T-0250-consistency-validator-base-b3.md', 'backend', '5C sweep base',
  'Implement T-0250 (5C.C validator-base composition): compose the shared validator rules via .SetValidator() / a base validator for PayConfig/PayPeriod/Employee/CurrentUser per the ticket. Ownership-move is explicitly OUT OF SCOPE (do not relocate rules between layers) — only the base-class composition. Behavior-preserving — the same validation errors fire. Pin with validator tests. Own only the validators the ticket names.')
const T0251 = tk('T-0251', 'T-0251-consistency-customer-facades-c.md', 'frontend', '5C sweep base',
  'Implement T-0251 (5C.D customer facades): bring the customer-app facades the ticket lists onto UnsubscribeControlDirective + the canonical pipe pattern. EXCLUDE disputes.facade.ts entirely (T-0202 owns it in a later batch). Behavior-preserving; OnPush + signals + facade conventions. npx nx test the touched projects + npx nx build cleansia.app --configuration=production. T-0251 blocks the AUD-07 order-wizard chain (T-0256+). Own only the customer facades the ticket names, minus disputes.')
const T0252 = tk('T-0252', 'T-0252-consistency-android-sealed-states-e.md', 'android', '5C sweep base',
  'Implement T-0252 (5C.E Android sealed UiState): introduce the sealed UiState + shared ActionState across the partner + customer Android view models the ticket lists, replacing the ad-hoc boolean/loading flags. Behavior-preserving (same observable UI states). Build the affected Android modules + run their unit tests. Own only the VMs the ticket names.')

// 5D AUD-06 — serial chain, lane-isolated on CreateOrder.cs
const T0253 = tk('T-0253', 'T-0253-createorder-extract-address-resolution.md', 'backend', '5D AUD-06',
  'Implement T-0253 (AUD-06 step 1): extract the address-resolution + serviced-area logic from CreateOrder.Handler into a collaborator, per the ticket. HARD GATE: T-0212 characterization suite (src/Cleansia.Tests/Features/Orders/CreateOrder*CharacterizationTests.cs) MUST stay GREEN UNCHANGED — do NOT edit those tests; they are your behavior-preservation proof. You are the SOLE editor of CreateOrder.cs this batch (T-0254/T-0255 run strictly after you). Keep the public Command/Response/handler contract identical.')
const T0254 = tk('T-0254', 'T-0254-createorder-extract-promo-application.md', 'backend', '5D AUD-06',
  'Implement T-0254 (AUD-06 step 2): extract the promo preview/apply logic from CreateOrder.Handler into a collaborator. T-0253 already extracted address-resolution — build on that state. HARD GATE: T-0212 characterization suite stays GREEN UNCHANGED. Sole editor of CreateOrder.cs now (T-0253 done, T-0255 after). Preserve the best-effort logged-and-swallowed promo semantics T-0212 pins.')
const T0255 = tk('T-0255', 'T-0255-createorder-payment-dispatch-referral-slimdown.md', 'backend', '5D AUD-06',
  'Implement T-0255 (AUD-06 step 3): extract the payment-side-effect dispatcher + late-referral accept, and slim the handler to orchestration. T-0253+T-0254 already extracted address + promo — build on that. HARD GATE: T-0212 characterization suite stays GREEN UNCHANGED. AC4 (critical): the Cash-branch enqueue MUST remain the POST-COMMIT dispatch/outbox seam (ADR-0002 / IPendingDispatch / the durable outbox), NOT a raw IQueueClient.SendAsync — T-0212 AC9 pins this; keep it. Final sole editor of CreateOrder.cs.')

// 5E de-triplication — SavedAddress lane serialized AFTER T-0249 (which response-wraps DeleteSavedAddress)
const T0201 = tk('T-0201', 'T-0201-da-8.md', 'backend', '5E de-triplication',
  'Implement T-0201 (DA-8 AddSavedAddress + B9 mapper): per the ticket, consuming T-0150 constants (done). SavedAddress LANE: T-0249 (DeleteSavedAddress response-wrap) ran before you in 5C and T-0198 runs after you — rebase on T-0249 state, keep your AddSavedAddress + mapper changes disjoint from the DeleteSavedAddress response-wrap. Behavior-preserving + the B9 mapper consolidation. If a response shape changes, MANUAL_STEP nswag-regen. Own AddSavedAddress handlers + the B9 mapper.')
const T0198 = tk('T-0198', 'T-0198-da-3-ia-2-ia-6-ia-8-ia-9.md', 'backend', '5E de-triplication',
  'Implement T-0198 (DA-3/IA-2/IA-6/IA-8/IA-9 de-triplication): the auth/dispute/saved-address controller + login/forgot facade consolidation the ticket enumerates, INCLUDING the 2 swallowed-error bug fixes it calls out. Must NOT touch host auth registration (T-0100 owns it) and must NOT touch disputes.facade.ts (T-0202 owns it). SavedAddress LANE: T-0249 + T-0201 ran before you — rebase on their state. A security advisory will check the auth surface + admin-password alignment. Backend + frontend. If a response/DTO shape changes, MANUAL_STEP nswag-regen.',
  { securityPrompt: 'Security advisory for T-0198 (auth-surface de-triplication): confirm the consolidation did not weaken any auth/authorization check, did not change which fields are exposed on the auth DTOs, and that the admin-password alignment + the 2 swallowed-error fixes are correct (errors now surface, not hidden). Confirm host auth registration (T-0100) was NOT touched. Advisory; FAIL only on a real auth/exposure regression.' })

// ---- execution ----
log('Dispatching Wave-5 5B+5C+5D+5E concurrently. Serial lanes: membership(T-0243->T-0203), AUD-06(T-0253->T-0254->T-0255), SavedAddress(T-0249 -> T-0201 -> T-0198 all serial across 5C+5E).')

// The SavedAddress lane spans 5C (T-0249 DeleteSavedAddress response-wrap) and 5E (T-0201 AddSavedAddress
// + B9 mapper, then T-0198 which also touches saved-address controllers). They share the SavedAddress
// mapper/handlers, so the whole lane is ONE serial chain. T-0249 runs first (it is the 5C base + blocks
// T-0202), then T-0201, then T-0198. We model it as a single thunk so nothing else in the SavedAddress
// lane starts until T-0249 is committed-to-tree.
const savedAddressLane = (async function () {
  const a = await runTicket(T0249)
  const b = await runTicket(T0201)
  const c = await runTicket(T0198)
  return [a, b, c]
})

const results = await parallel([
  // 5B: membership chain serial, others parallel
  async function () { const a = await runTicket(T0243); const b = await runTicket(T0203); return [a, b] },
  function () { return runTicket(T0244) },
  function () { return runTicket(T0205) },
  function () { return runTicket(T0206) },
  // 5C: the disjoint sub-streams (T-0249 is in the SavedAddress lane below, not here)
  function () { return runTicket(T0248) },
  function () { return runTicket(T0250) },
  function () { return runTicket(T0251) },
  function () { return runTicket(T0252) },
  // 5D: AUD-06 strict serial chain, lane-isolated on CreateOrder.cs
  async function () { const a = await runTicket(T0253); const b = await runTicket(T0254); const c = await runTicket(T0255); return [a, b, c] },
  // SavedAddress lane: T-0249 -> T-0201 -> T-0198 (serial, spans 5C+5E)
  savedAddressLane,
])

const flat = results.filter(Boolean).flat()
return {
  tickets: flat.map(function (r) {
    return {
      id: r.id, verdict: r.verdict,
      filesChanged: r.dev ? r.dev.filesChanged : [],
      manualSteps: r.dev ? r.dev.manualSteps : [],
      deviations: r.dev ? r.dev.deviations : [],
      productionBugs: r.dev ? r.dev.productionBugsFound : [],
      fixFiles: r.fix ? r.fix.filesChanged : [],
    }
  }),
}
