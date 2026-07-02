export const meta = {
  name: 'wave9-phase2',
  description: 'Wave 9 Phase 2 — the audit-capture machinery: T-0283 AuditLogBehavior + T-0285 query+view-policy (9B parallel) then T-0284 sensitive snapshots (9C). Holds T-0286 UI for the owner regen.',
  phases: [
    { title: '9B capture+query', detail: 'AuditLogBehavior (atomic, inner-to-UoW) + GetPagedAdminActionAudits query + view policy' },
    { title: '9C snapshots', detail: '5 sensitive handlers emit pre-redacted before/after via IAuditContext' },
  ],
}

const ROOT = 'c:/Users/cmisa/Desktop/Mike/Projects/cleansia'

const COMMON = [
  'Repo root: ' + ROOT + '. Branch feature/wave8-pre-ios-cleanup checked out. Do NOT commit, push, or git add. Other lanes\' uncommitted changes are EXPECTED — touch only your ticket files.',
  'Today is 2026-06-23. This is Wave 9 Phase 2 — the admin audit-log CAPTURE machinery. Read your ticket IN FULL, agents/backlog/adr/0012-admin-action-audit-log.md (the FROZEN contract — build exactly to it), conventions.md + patterns-backend.md.',
  'The AdminActionAudit entity + table already exist and migrated (T-0282 done): src/Cleansia.Core.Domain/Auditing/AdminActionAudit.cs (append-only, init-only) + its EF config + the migrated table. Build ON it; do not change the entity shape.',
  'GATE 0 (evidence discipline) applies to any finding you raise. TEST-FIRST per agents/knowledge/testing.md: the ADR lists the exact tests (TC-AUDIT-ATOMIC/FAILURE/GATE/LABEL for T-0283; TC-AUDIT-QUERY + authz-rejection for T-0285; TC-AUDIT-SNAPSHOT + GDPR-survives-erasure for T-0284) — write them red-first.',
  'ENVIRONMENT: backend — dotnet build <proj>.csproj then dotnet test <proj>.csproj --no-build (VS host-DLL locks; build the specific project). Real Postgres via Testcontainers is up — the atomic/failure/survives-erasure tests are real-Postgres integration tests. Baseline green: Cleansia.Tests 1580, IntegrationTests 79, HostTests 51. ANY new red is YOURS.',
  'OWNER-ONLY (never run): dotnet ef, npm run generate-*-client, NSwag client edits. T-0285 adds a new admin query DTO → record MANUAL_STEP nswag-regen (do NOT regen — T-0286 UI is held for the owner to do it). C# files keep their existing BOM (repo norm); no mojibake.',
  'Comment discipline: NO ticket IDs in source; default no comment. Update your ticket: status -> review, append status-log. Final message is data for the orchestrator.',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

const DEV_SCHEMA = {
  type: 'object',
  required: ['summary', 'filesChanged', 'testEvidence', 'verificationAchieved', 'manualSteps', 'deviations'],
  properties: {
    summary: { type: 'string', maxLength: 600 }, filesChanged: { type: 'array', items: { type: 'string', maxLength: 300 } },
    testEvidence: { type: 'array', items: { type: 'string', maxLength: 300 }, description: 'short pointers: suite + counts + one-line verdict, never raw logs' },
    verificationAchieved: { type: 'string', maxLength: 600 },
    manualSteps: { type: 'array', items: { type: 'string', maxLength: 300 } }, deviations: { type: 'array', items: { type: 'string', maxLength: 300 } },
  },
}
const REVIEW_SCHEMA = {
  type: 'object', required: ['verdict', 'mustFix', 'notes'],
  properties: { verdict: { type: 'string', enum: ['PASS', 'PASS-WITH-NOTES', 'FAIL'] }, mustFix: { type: 'array', items: { type: 'string', maxLength: 300 } }, notes: { type: 'array', items: { type: 'string', maxLength: 300 } } },
}

function reviewPrompt(t, dev) {
  return [
    'Reviewer gate for ' + t.id + ' (Wave 9 Phase 2, admin audit log).', COMMON, 'Ticket: ' + t.ticket,
    'Developer reports: ' + JSON.stringify(dev),
    'Gate: every AC met; matches the ADR-0012 contract; VERIFY-NOT-TRUST (run the tests yourself, incl. the real-Postgres ones). ' + (t.reviewExtra || ''),
  ].join('\n\n')
}

async function runTicket(t) {
  log(t.id + ': dev (' + t.phase + ')')
  const dev = await agent([COMMON, t.prompt].join('\n\n'), { label: 'dev:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  if (!dev) return { id: t.id, verdict: 'AGENT-LOST' }
  log(t.id + ': gating')
  const gates = [function () { return agent(reviewPrompt(t, dev), { label: 'review:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA }) }]
  if (t.securityPrompt) gates.push(function () { return agent([t.securityPrompt, COMMON, 'Ticket: ' + t.ticket, 'Dev: ' + JSON.stringify({ summary: dev.summary, filesChanged: dev.filesChanged, testEvidence: dev.testEvidence })].join('\n\n'), { label: 'sec:' + t.id, phase: t.phase, agentType: 'security', schema: REVIEW_SCHEMA }) })
  const verdicts = await parallel(gates)
  const mustFix = []
  verdicts.filter(Boolean).forEach(function (v) { (v.mustFix || []).forEach(function (f) { mustFix.push(f) }) })
  if (!verdicts.filter(Boolean).some(function (v) { return v.verdict === 'FAIL' }) && mustFix.length === 0) {
    const verdict = verdicts.filter(Boolean).some(function (v) { return v.verdict === 'PASS-WITH-NOTES' }) ? 'PASS-WITH-NOTES' : 'PASS'
    log(t.id + ': ' + verdict); return { id: t.id, verdict: verdict, dev: dev }
  }
  log(t.id + ': FAIL (' + mustFix.length + ') — fix')
  const fix = await agent([COMMON, 'Fix these blocking findings on ' + t.id + ', nothing else:', mustFix.map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'), 'Re-run the relevant tests.'].join('\n\n'), { label: 'fix:' + t.id, phase: t.phase, agentType: t.devType, schema: DEV_SCHEMA })
  const regate = await agent([reviewPrompt(t, fix || dev), 'RE-GATE after fix. Original: ' + mustFix.join(' | ')].join('\n\n'), { label: 'regate:' + t.id, phase: t.phase, agentType: t.securityPrompt ? 'security' : 'reviewer', schema: REVIEW_SCHEMA })
  const v = regate ? regate.verdict : 'REGATE-LOST'
  log(t.id + ': ' + v); return { id: t.id, verdict: v, dev: dev, fix: fix }
}

const tk = (id, slug, devType, phase, prompt, extra) => Object.assign({ id, ticket: 'agents/backlog/tickets/' + slug, devType, phase, prompt }, extra || {})

const T0283 = tk('T-0283', 'T-0283-audit-behavior-context-sink.md', 'backend', '9B capture+query',
  'Implement T-0283: the AuditLogBehavior (the heart of the feature). Per ADR-0012: a MediatR pipeline behavior registered INNER to UnitOfWorkPipelineBehavior so the success-audit row is written into the SAME SaveChangesAsync as the admin action (atomic — rollback ⇒ no audit row). Plus a scoped IAuditContext (mirrors IPendingDispatch — handlers push optional before/after snapshots into it) and an OUT-OF-BAND IAuditFailureSink (because on a business-failure or thrown-exception the UoW never commits, so the failed-attempt audit must be written separately — and the sink must NEVER re-throw). GATE: only audit ADMIN MUTATIONS — discriminate via the ROLE claim (ClaimTypes.Role == Administrator) AND a Command (not Query) — not a per-command attribute. Actor from IUserSessionProvider.GetUserId()/email/profile, with a "System" fallback when no session. Action label = the command type name, or an optional [AuditAction] attribute override. Write the AdminActionAudit row (Success=true on the happy path; Success=false + ErrorCode on the failure path via the sink). Register the behavior in the DI pipeline AFTER the UoW line (inner). TEST-FIRST (real Postgres for atomic/failure): TC-AUDIT-ATOMIC (one row, same tx; rollback ⇒ no row), TC-AUDIT-FAILURE (business-fail + thrown ⇒ Success=false row, sink never re-throws), TC-AUDIT-GATE (a customer/partner command or any Query ⇒ NO row), TC-AUDIT-LABEL (type-name vs [AuditAction], rename-proof), and the pipeline-ORDER unit test asserting AuditLogBehavior is inner to UnitOfWorkPipelineBehavior. Verify the relevant projects compile + tests green.',
  { securityPrompt: 'Security/compliance gate for T-0283. Confirm: (1) the gate genuinely captures ALL admin mutations and ONLY admin mutations (a customer/partner command or any query writes no row; verify the role-claim + Command discriminator can\'t be bypassed); (2) a FAILED or BLOCKED admin attempt is STILL audited (the out-of-band sink fires on the business-fail AND the thrown-exception path) — an attacker probing must leave a trail; (3) the sink NEVER re-throws (an audit-write failure must not break or roll back the actual operation — but must be logged); (4) the actor is sourced server-side from the session, never client-supplied; (5) the success-audit is atomic with the action (no audit row for a rolled-back action, and no action without its audit row). FAIL if any admin mutation can occur unaudited or a failed attempt goes unrecorded.' })

const T0285 = tk('T-0285', 'T-0285-audit-query-view-policy.md', 'backend', '9B capture+query',
  'Implement T-0285: the admin READ surface for the audit log. A canonical GetPagedAdminActionAudits query (DataRangeRequest + AdminActionAuditSpecification + Sort + PagedData<T> — the canonical paged archetype, exemplar GetPagedServices) with filters: actor id, action, resource type+id, date range, outcome (success/fail); TENANT-SCOPED (the central ITenantEntity query filter applies). Add a NEW view policy to Policy.cs + PolicyBuilder.cs + the FrozenPermissionMapTests snapshot (the 3-file Policy cluster — you are its SOLE writer this phase; add Policy.CanViewAuditLog as AdminOnly/SuperAdmin per the ADR) and gate the admin controller action with it. This adds a new response DTO → record MANUAL_STEP nswag-regen (admin client; do NOT regen — the UI ticket T-0286 is held for the owner regen). TEST-FIRST: TC-AUDIT-QUERY (filters + tenant scoping return the right rows) + an authz-rejection integration/host test (a non-admin caller is rejected, never 200). Verify compile + tests; the FrozenPermissionMapTests snapshot must be updated in the SAME change (or AssertComplete bricks boot).',
  { reviewExtra: 'CRITICAL: the new Policy.CanViewAuditLog must be added to Policy.cs AND PolicyBuilder.cs AND the FrozenPermissionMapTests snapshot in ONE change (the cluster invariant). Confirm the query uses the canonical PagedData archetype (not hand-rolled paging) and is tenant-scoped, and a non-admin is rejected.',
    securityPrompt: 'Security gate for T-0285: the audit-log view is a privileged read (it exposes who-did-what). Confirm Policy.CanViewAuditLog is AdminOnly/SuperAdmin and the controller action is gated by it; a non-admin caller gets 403/NotFound never 200 (authz integration test proves it); the query is tenant-scoped so one tenant cannot read another\'s audit trail. FAIL if the audit log is readable by an unprivileged or cross-tenant caller.' })

const T0284 = tk('T-0284', 'T-0284-audit-sensitive-snapshots.md', 'backend', '9C snapshots',
  'Implement T-0284: the before/after SNAPSHOTS on the sensitive admin actions (depends on T-0283\'s IAuditContext, which exists now). Per ADR-0012, in EACH of the sensitive handlers emit ONE auditContext.RecordChange(before, after) call with a TYPED, PRE-REDACTED snapshot — only entity ids + the CHANGED fields, NEVER raw subject PII. The sensitive set: admin refund (IssuePartialRefund / AdminRefundOrder — amount/status), AdminOverrideOrderStatus (old→new status), EmployeePayConfig edit (rates), AdminDeleteUserAccount / GDPR export (scope + subject id ONLY — NOT the subject\'s personal data, so the audit row survives the subject\'s erasure as a legal-basis exception), loyalty grant/revoke (points delta), ResolveDispute (resolution + refund). Add [AuditAction(Sensitive=true)] + a frozen action label to each so a rename can\'t silently change the audit label. The handler captures the BEFORE value (it already loads the entity) and the AFTER. TEST-FIRST: TC-AUDIT-SNAPSHOT (each of the ~6 emits the expected payload; assert NO raw subject PII in the GDPR one) + the GDPR-survives-erasure test (delete the subject ⇒ the audit row + its snapshot remain, with no FK that would cascade-delete it). Verify compile + tests green.',
  { securityPrompt: 'Security/PII gate for T-0284. The before/after snapshots are the highest PII-risk surface. Confirm: (1) NO raw subject personal data is stored — only ids + changed fields (especially the GDPR-delete/export audit: scope + subject id, NOT name/email/address); (2) the audit row + snapshot SURVIVE the subject\'s erasure (no cascading FK, no query-filter that hides it) — the audit is a legal-basis exception to GDPR erasure and must persist; (3) the money snapshots (refund amount, loyalty delta, pay-config rates) capture enough to investigate a dispute but leak no card/bank data. FAIL if raw subject PII lands in an audit snapshot or the GDPR audit row would be erased with the subject.' })

phase('9B capture+query')
log('Wave 9 Phase 2: 9B (T-0283 AuditLogBehavior || T-0285 query+view-policy) -> 9C (T-0284 snapshots, after the behavior). T-0286 UI held for owner regen.')

const b = await parallel([
  function () { return runTicket(T0283) },
  function () { return runTicket(T0285) },
])

phase('9C snapshots')
const c = await runTicket(T0284)

const all = b.filter(Boolean).concat([c].filter(Boolean))
return {
  tickets: all.map(function (r) {
    return { id: r.id, verdict: r.verdict, files: r.dev ? r.dev.filesChanged : [], manualSteps: r.dev ? r.dev.manualSteps : [], deviations: r.dev ? r.dev.deviations : [] }
  }),
}
