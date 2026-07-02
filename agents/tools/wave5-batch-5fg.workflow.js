export const meta = {
  name: 'wave5-batch-5fg',
  description: 'Wave 5 final batches 5F (AUD-07 order-wizard T-0256->0257->0258 serial + T-0202 disputes archetype) + 5G (T-0204 perf cluster + T-0247 dispute-guard rule)',
  phases: [
    { title: '5F frontend rebuilds', detail: 'AUD-07 order-wizard serial chain + customer disputes own-client' },
    { title: '5G perf + tooling', detail: 'perf-cluster indexes/queries + dispute-guard consistency rule' },
  ],
}

const ROOT = 'c:/Users/cmisa/Desktop/Mike/Projects/cleansia'

const COMMON = [
  'Repo root: ' + ROOT + '. Branch feature/wave-5-consistency-bugs is ALREADY checked out (5A+5B+5C+5D+5E committed). Do NOT switch branches, commit, push, or git add. Concurrent lanes run in this one tree — touch ONLY your ticket files. Uncommitted changes that are not yours are EXPECTED; never revert/stage them.',
  'Today is 2026-06-14. Read first: your ticket IN FULL, agents/knowledge/testing.md, and your stack catalog.',
  'Behavior-preserving refactor under test where applicable; real green tests. Keep changes surgical and on-scope. Found a real bug? Report it, do not fix it here.',
  'ENVIRONMENT TRAP: VS/devenv + running hosts hold locks on shared host DLLs → MSB3021/MSB3027 on solution-graph builds. Build/test your SPECIFIC project (dotnet build <proj>.csproj; dotnet test <proj>.csproj --no-build, or -p:BuildProjectReferences=false). Frontend: npx nx test/build <project> from src/Cleansia.App. Real Postgres via Testcontainers is available (Docker up). Report which verification you achieved; the orchestrator does the authoritative clean run.',
  'Comment discipline: NO ticket IDs (T-NNNN / TC-NN / // ACn) in source. Load-bearing ADR-NNNN / S1-S10 only.',
  'OWNER-ONLY (never run): dotnet ef migrations/database, npm run generate-*-client, edits to NSwag clients, edits to DB seeds. If your ticket changes a response/DTO shape → MANUAL_STEP nswag-regen. If it needs a schema/index change → MANUAL_STEP ef-migration (write the EF config / migration-intent but do NOT run dotnet ef).',
  'i18n: any new user-visible string needs a TranslatePipe key in ALL 5 locales of the relevant app.',
  'Update your ticket: status -> review, append status-log. Final message is data for the orchestrator.',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

const DEV_SCHEMA = {
  type: 'object',
  required: ['summary', 'filesChanged', 'testEvidence', 'verificationAchieved', 'deviations', 'manualSteps', 'productionBugsFound'],
  properties: {
    summary: { type: 'string', maxLength: 600 },
    filesChanged: { type: 'array', items: { type: 'string', maxLength: 300 } },
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
    COMMON, 'Ticket file: ' + t.ticket,
    'Developer reports: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged, testEvidence: dev.testEvidence, verificationAchieved: dev.verificationAchieved, deviations: dev.deviations }),
    'Review ONLY developer-listed files (git diff -- <file>; ignore other working-tree changes).',
    'Gate: (1) every AC met or justified deviation; (2) behavior-preserving where claimed (covered by tests); (3) conventions (no T-NNNN comments, i18n in 5 locales for new strings, cleansia-*/PrimeNG, OnPush+facade for FE); (4) no unauthorized migration/nswag/DTO edit (response-shape change → MANUAL_STEP recorded; ef index/schema → MANUAL_STEP + EF config written not run); (5) VERIFY-NOT-TRUST: run the dev tests/build yourself where the env allows.',
    t.reviewExtra || '',
  ].join('\n\n')
}

async function runTicket(t) {
  log(t.id + ': dev starting (' + t.phase + ')')
  const dev = await agent([COMMON, t.prompt].join('\n\n'), { label: 'dev:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  if (!dev) return { id: t.id, verdict: 'AGENT-LOST' }
  log(t.id + ': dev done — gating')
  const gates = [function () { return agent(reviewPrompt(t, dev), { label: 'review:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA }) }]
  if (t.optimizerPrompt) {
    gates.push(function () { return agent([t.optimizerPrompt, COMMON, 'Ticket: ' + t.ticket, 'Dev report: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged })].join('\n\n'), { label: 'opt:' + t.id, phase: t.phase, agentType: 'optimizer', schema: REVIEW_SCHEMA }) })
  }
  if (t.securityPrompt) {
    gates.push(function () { return agent([t.securityPrompt, COMMON, 'Ticket: ' + t.ticket, 'Dev report: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged })].join('\n\n'), { label: 'sec:' + t.id, phase: t.phase, agentType: 'security', schema: REVIEW_SCHEMA }) })
  }
  const verdicts = await parallel(gates)
  const mustFix = []
  verdicts.filter(Boolean).forEach(function (v) { (v.mustFix || []).forEach(function (f) { mustFix.push(f) }) })
  const failed = verdicts.filter(Boolean).some(function (v) { return v.verdict === 'FAIL' })
  if (!failed && mustFix.length === 0) {
    const verdict = verdicts.filter(Boolean).some(function (v) { return v.verdict === 'PASS-WITH-NOTES' }) ? 'PASS-WITH-NOTES' : 'PASS'
    log(t.id + ': ' + verdict); return { id: t.id, verdict: verdict, dev: dev }
  }
  log(t.id + ': FAIL (' + mustFix.length + ') — fix lane')
  const fix = await agent([
    'Fix review findings on ticket ' + t.id + ' (Cleansia). Dev work is in the tree.', COMMON, 'Ticket file: ' + t.ticket,
    'Fix ALL of these, nothing else:', mustFix.map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'),
    'Re-run affected tests. Update the ticket status log.',
  ].join('\n\n'), { label: 'fix:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  const regate = await agent([
    'RE-GATING ' + t.id + '. Original findings:', mustFix.map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'),
    'Fixer: ' + JSON.stringify(fix ? { summary: fix.summary, filesChanged: fix.filesChanged, verificationAchieved: fix.verificationAchieved } : null),
    COMMON, 'Ticket: ' + t.ticket, 'Verify each finding resolved. FAIL only if a blocking finding survives.',
  ].join('\n\n'), { label: 'regate:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA })
  const v = regate ? regate.verdict : 'REGATE-LOST'
  log(t.id + ': ' + v); return { id: t.id, verdict: v, dev: dev, fix: fix }
}

const tk = (id, slug, devType, phase, prompt, extra) => Object.assign({ id, ticket: 'agents/backlog/tickets/' + slug, devType, phase, prompt }, extra || {})

// 5F — AUD-07 order-wizard serial chain (sole editor of order-wizard/**)
const T0256 = tk('T-0256', 'T-0256-order-wizard-extract-quote-pricing.md', 'frontend', '5F frontend rebuilds',
  'Implement T-0256 (AUD-07 step 1): extract the quote/pricing collaborator from the order-wizard facade + C3-migrate that stream, per the ticket. T-0251 (5C.D) already brought the order-wizard facade onto UnsubscribeControlDirective + canonical pipe — build on that state. Behavior-preserving; sole editor of libs/cleansia-customer-features/order-wizard/** this batch (T-0257/T-0258 run strictly after you). OnPush + facade conventions. npx nx test cleansia-customer-order-wizard + npx nx build cleansia.app --configuration=production must pass.')
const T0257 = tk('T-0257', 'T-0257-order-wizard-extract-promo-referral-city.md', 'frontend', '5F frontend rebuilds',
  'Implement T-0257 (AUD-07 step 2): extract the promo+referral + city-serviced collaborators from the order-wizard facade and drop firstValueFrom, per the ticket. T-0256 already extracted quote/pricing — build on that. Behavior-preserving; sole editor of order-wizard/** now. npx nx test cleansia-customer-order-wizard + build cleansia.app prod.')
const T0258 = tk('T-0258', 'T-0258-order-wizard-saved-address-slim-submit.md', 'frontend', '5F frontend rebuilds',
  'Implement T-0258 (AUD-07 step 3): extract the saved-address collaborator, slim the facade, and C1/C3-migrate the submit branches, per the ticket. T-0256+T-0257 already extracted quote/promo/referral/city — build on that, leaving a thin orchestration facade. Behavior-preserving; final sole editor of order-wizard/**. npx nx test cleansia-customer-order-wizard + build cleansia.app prod.')

// 5F — customer disputes own-client (parallel with the AUD-07 chain; disjoint folder)
const T0202 = tk('T-0202', 'T-0202-da-5-da-6-perf-f1.md', 'frontend', '5F frontend rebuilds',
  'Implement T-0202 (DA-5/DA-6/PERF-F1): migrate the CUSTOMER disputes feature off the PARTNER generated client onto its OWN customer client, and apply the cleansia-table/form/error archetype, per the ticket. VERIFIED PRECONDITION: @cleansia/customer-services ALREADY exposes DisputeClient + CreateDisputeCommand + DisputeReason/DisputeStatus (103 dispute refs) — so target the EXISTING customer client; do not wait on a regen. NOTE: T-0249 response-wrapped CreateDispute on the backend (Observable<string> -> CreateDisputeResponse) but the customer client is not yet regenerated, so the current generated create() still returns Observable<string>; code against the CURRENT client shape — the owner regen will align the response type later (record it as a known follow-on, not a blocker). You OWN libs/cleansia-customer-features/disputes/** (this is the file T-0251 was told to EXCLUDE — it is yours). npx nx test the disputes lib + build cleansia.app prod. Every user-visible string in 5 locales.',
  { reviewExtra: 'Confirm the feature now imports DisputeReason/DisputeStatus/Command from @cleansia/customer-services (NOT @cleansia/partner-services) — that removal of the cross-app client coupling is the core of DA-5/PERF-F1. Confirm no partner-services import remains in the customer disputes feature.' })

// 5G — perf cluster (ef-migration MANUAL_STEP) + dispute-guard rule (security)
const T0204 = tk('T-0204', 'T-0204-perf-cluster.md', 'backend', '5G perf + tooling',
  'Implement T-0204 (PERF cluster): the N+1 / over-fetch / missing-index / paging fixes the ticket enumerates, including PERF-IDA-06 (GetAllGdprRequests orders AFTER paging -> wrong page, a real correctness bug — fix it to order before paging). The 4 indexes (Addresses composite; UserMembership (Status,CurrentPeriodEnd); GdprRequest CreatedOn; Devices (IsActive,LastActiveAt)) require a schema change: write the EF Core entity-configuration index declarations in the entity configs, and record a MANUAL_STEP ef-migration (4 indexes, intended CONCURRENTLY) — do NOT run dotnet ef. PERF-D2 rebases on T-0249 B1 UpdateDisputeStatus (now done). Behavior-preserving except the GDPR paging correctness fix; add tests for the query-shape fixes where practical. An optimizer gate will check the N+1s are actually resolved.',
  { optimizerPrompt: 'You are the OPTIMIZER gate for T-0204. For each claimed fix: confirm the N+1 is actually gone (the query now projects/Includes correctly, no per-row round-trip), the over-fetch is narrowed (Select projection or AsNoTracking where read-only), and the 4 index declarations match the query predicates they are meant to serve (composite column order matches the WHERE+ORDER BY). Confirm PERF-IDA-06 now orders BEFORE Skip/Take. FAIL with concrete mustFix if a claimed perf fix does not actually change the generated query shape or an index does not match its query.' })
const T0247 = tk('T-0247', 'T-0247-da-2-followup.md', 'backend', '5G perf + tooling',
  'Implement T-0247 (DA-2 follow-up): the dispute check-consistency / transition-guard rule the ticket describes (it guards the dispute state machine). Behavior-preserving guard hardening; a security advisory will check the guard cannot be bypassed. Add tests for the guarded transitions. Own only the files the ticket names.',
  { securityPrompt: 'You are the SECURITY gate for T-0247 (dispute state-machine guard). Confirm the guard actually prevents the illegal transitions the ticket targets (e.g. re-resolving a Resolved dispute, illegal status jumps) and cannot be bypassed via an alternate code path (free UpdateStatus vs the intent-named domain methods). Confirm Resolve remains the sole resolution-field writer. FAIL with concrete mustFix if a guarded transition is reachable unguarded.' })

log('Dispatching Wave-5 5F+5G. Serial: AUD-07 order-wizard (T-0256->T-0257->T-0258). Parallel: T-0202 (disputes, disjoint), T-0204 (perf), T-0247 (dispute-guard).')

const results = await parallel([
  async function () { const a = await runTicket(T0256); const b = await runTicket(T0257); const c = await runTicket(T0258); return [a, b, c] },
  function () { return runTicket(T0202) },
  function () { return runTicket(T0204) },
  function () { return runTicket(T0247) },
])

const flat = results.filter(Boolean).flat()
return {
  tickets: flat.map(function (r) {
    return { id: r.id, verdict: r.verdict, filesChanged: r.dev ? r.dev.filesChanged : [], manualSteps: r.dev ? r.dev.manualSteps : [], deviations: r.dev ? r.dev.deviations : [], productionBugs: r.dev ? r.dev.productionBugsFound : [], fixFiles: r.fix ? r.fix.filesChanged : [] }
  }),
}
