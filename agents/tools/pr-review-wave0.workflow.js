export const meta = {
  name: 'pr-review-wave0',
  description: 'Multi-agent review of the Wave-0 PR diff (production code), with adversarial verification',
  phases: [
    { title: 'Review' },
    { title: 'Verify' },
    { title: 'Synthesize' },
  ],
}

// Each group = a coherent subsystem slice. The agent reads the file list, then `git diff master...HEAD`
// for those files, and reviews ONLY the changed code (not the whole files) against the catalog.
const GROUPS = [
  { key: 'auth', list: '/tmp/pr_review_groups/01_auth.txt', desc: 'auth / identity / authorization / tokens / GDPR' },
  { key: 'money', list: '/tmp/pr_review_groups/02_money.txt', desc: 'money + idempotency: promo, loyalty, membership, pay, fiscal, receipt, dispute, stripe webhooks' },
  { key: 'outbox', list: '/tmp/pr_review_groups/03_outbox.txt', desc: 'transactional outbox / Azure Functions / queue / dispatch / dead-letter / reconciliation' },
  { key: 'infra', list: '/tmp/pr_review_groups/04_infra.txt', desc: 'infra: EF Core, DbContext, entity configs, repositories, Config/startup, rate-limit, appsettings, migrations' },
  { key: 'domain_ui', list: '/tmp/pr_review_groups/05_domain_ui.txt', desc: 'domain entities + Angular frontend + Android' },
  { key: 'orders_misc', list: '/tmp/pr_review_groups/06_orders_users_misc.txt', desc: 'orders, admin-users, users, dashboard endpoints, DI extensions, error messages' },
]

const LENSES = [
  {
    key: 'correctness',
    agent: 'reviewer',
    focus: [
      'Correctness bugs, logic errors, null/edge cases, race conditions.',
      'Convention violations vs agents/knowledge/ (CQRS handler purity, validation in validators not handlers,',
      'no CommitAsync in handlers, BusinessResult/PagedData usage, record DTOs, error keys in BusinessErrorMessage,',
      'Angular OnPush+signals+facades, no any, TranslatePipe on strings).',
      'Dead code, spaghetti, leftover TODO/half-built paths.',
    ].join(' '),
  },
  {
    key: 'security',
    agent: 'security',
    focus: [
      'The S1-S10 laws in agents/knowledge/security-rules.md INCLUDING the new S7a/S7b idempotency idioms.',
      'AuthZ/IDOR/resource-ownership, multi-tenancy isolation, PII, idempotency atomicity (check-then-act,',
      'conditional-UPDATE vs caught-unique-violation, where DbUpdateException surfaces vs the UoW commit),',
      'secrets in config, [AllowAnonymous] discipline, rate-limit, migration/DTO safety.',
    ].join(' '),
  },
  {
    key: 'perf',
    agent: 'optimizer',
    focus: [
      'N+1 queries, missing indexes, over-fetching, untranslatable LINQ, allocations on hot paths,',
      'async misuse, needless re-renders/recompositions, bundle bloat. Flag query shapes that fail EF translation.',
    ].join(' '),
  },
]

const FINDING_SCHEMA = {
  type: 'object',
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          title: { type: 'string' },
          file: { type: 'string', description: 'path:line' },
          severity: { type: 'string', enum: ['critical', 'high', 'medium', 'low', 'nit'] },
          category: { type: 'string', description: 'e.g. security/IDOR, correctness, perf/N+1, convention' },
          evidence: { type: 'string', description: 'the specific code that is wrong and why' },
          recommendation: { type: 'string' },
        },
        required: ['title', 'file', 'severity', 'category', 'evidence', 'recommendation'],
      },
    },
  },
  required: ['findings'],
}

const VERDICT_SCHEMA = {
  type: 'object',
  properties: {
    isReal: { type: 'boolean', description: 'true only if the finding is a genuine defect in the CHANGED code' },
    confidence: { type: 'string', enum: ['high', 'medium', 'low'] },
    reasoning: { type: 'string' },
    correctedSeverity: { type: 'string', enum: ['critical', 'high', 'medium', 'low', 'nit'] },
  },
  required: ['isReal', 'confidence', 'reasoning', 'correctedSeverity'],
}

const DIFF_HINT =
  'Read the file list at the path given, then run `git diff master...HEAD -- <those files>` to see ONLY the changed lines. ' +
  'Review the CHANGES (this is a PR review, not a whole-file audit). Read agents/knowledge/*.md for the conventions and the ' +
  'S1-S10 + S7a/S7b rules first. Read surrounding code for context before judging. Report ONLY genuine issues in the changed code; ' +
  'do not invent issues and do not report pre-existing code outside the diff. If a slice is clean, return an empty findings array.'

// One pipeline item per (group x lens). Stage 1 reviews; stage 2 verifies each finding adversarially.
const UNITS = []
for (const g of GROUPS) for (const l of LENSES) UNITS.push({ g, l })

log(`Reviewing ${GROUPS.length} subsystems x ${LENSES.length} lenses = ${UNITS.length} review units over the Wave-0 PR diff.`)

const reviewed = await pipeline(
  UNITS,
  // Stage 1 — review one subsystem through one lens
  (u) =>
    agent(
      `You are reviewing the Wave-0 PR for Cleansia. Subsystem: ${u.g.desc}. ` +
        `File list: ${u.g.list}. Lens: ${u.l.focus} ${DIFF_HINT}`,
      { label: `review:${u.g.key}/${u.l.key}`, phase: 'Review', agentType: u.l.agent, schema: FINDING_SCHEMA }
    ).then((r) => ({ unit: u, findings: (r && r.findings) || [] })),

  // Stage 2 — adversarially verify each finding with 2 independent skeptics (perspective-diverse)
  (review) =>
    parallel(
      review.findings.map((f) => () =>
        parallel([
          () =>
            agent(
              `Adversarially verify this PR-review finding. Default to isReal=false unless you can prove it. ` +
                `Read the actual changed code and surrounding context via git diff master...HEAD. ` +
                `Finding: ${JSON.stringify(f)}`,
              { label: `verify-A:${f.file}`, phase: 'Verify', agentType: 'reviewer', schema: VERDICT_SCHEMA }
            ),
          () =>
            agent(
              `Independently verify this PR-review finding from a DIFFERENT angle (does it actually reproduce / ` +
                `is it really exploitable or wrong, or is it a false positive / pre-existing / out-of-diff?). ` +
                `Default isReal=false unless proven. Read the real code. Finding: ${JSON.stringify(f)}`,
              { label: `verify-B:${f.file}`, phase: 'Verify', agentType: 'security', schema: VERDICT_SCHEMA }
            ),
        ]).then((votes) => {
          const v = votes.filter(Boolean)
          const realCount = v.filter((x) => x.isReal).length
          // survive if at least one verifier confirms (kill only if BOTH refute)
          return {
            ...f,
            verdicts: v,
            survives: realCount >= 1,
            // escalate/keep the highest corrected severity among confirming verifiers
            finalSeverity:
              v.filter((x) => x.isReal).map((x) => x.correctedSeverity)[0] || f.severity,
            lens: review.unit.l.key,
            subsystem: review.unit.g.key,
          }
        })
      )
    ).then((arr) => arr.filter(Boolean))
)

const confirmed = reviewed
  .filter(Boolean)
  .flat()
  .filter((f) => f && f.survives)

log(`Confirmed ${confirmed.length} findings after adversarial verification. Synthesizing report.`)

phase('Synthesize')
const report = await agent(
  `You are the lead reviewer. Synthesize the FINAL PR review report for the Wave-0 Cleansia PR from these ` +
    `adversarially-verified findings. Dedupe (same file+issue from multiple lenses = one entry). Group by severity ` +
    `(critical -> nit). For each: title, file:line, severity, one-line evidence, one-line fix. Then a short verdict: ` +
    `is this PR safe to merge, merge-with-fixes, or needs-changes, and list any MUST-FIX-before-merge items. ` +
    `Be concise and concrete. Findings JSON: ${JSON.stringify(confirmed)}`,
  { label: 'synthesis', phase: 'Synthesize' }
)

return { confirmedCount: confirmed.length, confirmed, report }
