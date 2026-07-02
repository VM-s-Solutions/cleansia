export const meta = {
  name: 'wave3-3f',
  description: 'Wave-3 Batch 3F (final): T-0193 account lockout + T-0194 rate-limit coverage sweep + T-0195 client Retry-After back-off; dev + reviewer each, security on 0193/0194',
  phases: [
    { title: 'Build', detail: '3 parallel lanes (disjoint files)' },
    { title: 'Review', detail: 'reviewer per lane; security on T-0193/T-0194' },
  ],
}

const COMMON = [
  'PROJECT RULES (non-negotiable): CQRS conventions; handler happy-path only; every *Command has a Validator;',
  'no CommitAsync in handlers; BusinessResult; record DTOs; new Policy consts (if any) need the 3-file Policy',
  'cluster edit. TEST-FIRST (red->green) for authz/lockout/state logic. Comment discipline: no task-number',
  'refs. Do NOT run dotnet ef / npm generate — flag manual_step. Build src/Cleansia.Api.sln + run',
  'src/Cleansia.Tests green (single-threaded; the IntegrationFailureMetricsTests meter flake is unrelated).',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

phase('Build')
const [t0193, t0194, t0195] = await parallel([
  () => agent(
    'You are the BACKEND developer. Implement T-0193 — per-account lockout + per-code attempt cap. Ticket: ' +
    'agents/backlog/tickets/T-0193-*.md (find + read it). Deps T-0115/T-0189/T-0190 are done; you now OWN ' +
    'User.cs / UserEntityConfiguration.cs in this batch (no concurrent editor). KEY CONSTRAINTS from the ' +
    'plan: (a) new User lockout/attempt columns => manual_step: ef-migration (owner-only; flag, do not run); ' +
    '(b) the ticket AC forbids touching CleansiaStartupBase.cs; (c) lockout must NOT enable user enumeration ' +
    '(same error shape for locked vs wrong-password where the ticket requires); (d) the per-code attempt cap ' +
    'guards the reset/confirm-code paths (brute-force window). TEST-FIRST: lockout after N failures, unlock ' +
    'after window, attempt-cap on codes, no-enumeration shape. ' + COMMON +
    '\nReturn: files, the lockout/attempt model + thresholds, test names + red->green, build/test, ' +
    'manual_step: ef-migration details.',
    { label: 'dev:T-0193', phase: 'Build', agentType: 'backend' },
  ),
  () => agent(
    'You are the BACKEND developer. Implement T-0194 — the rate-limit coverage sweep. Ticket: ' +
    'agents/backlog/tickets/T-0194-*.md (find + read it). This is ATTRIBUTE-ONLY: add [EnableRateLimiting] ' +
    'with the correct policy to every money/side-effect endpoint the ticket enumerates — INCLUDING the ' +
    'endpoints created this wave by T-0171 (admin payroll/pay-period), T-0173 (admin disputes), T-0179, ' +
    'T-0188 (devices — Mine/Revoke already done in the fix-up; verify, do not duplicate), T-0175a ' +
    '(membership), T-0176 (referral), T-0190 (change-own-password — verify), T-0191 (catalog lifecycle). ' +
    'Sweep ALL hosts (Admin/Partner/Customer/Mobile.*). Do NOT touch CleansiaStartupBase.cs or the policy ' +
    'definitions — attributes only. Add/extend a reflection test asserting every [HttpPost/Put/Delete] on ' +
    'the enumerated money/side-effect controllers carries a rate-limit attribute (the structural guard so ' +
    'future endpoints cannot ship without one — mirror the existing reflection-test idiom). ' + COMMON +
    '\nReturn: per-host endpoints annotated (list), the reflection guard test, test names + red->green, ' +
    'build/test, manual_step (none expected).',
    { label: 'dev:T-0194', phase: 'Build', agentType: 'backend' },
  ),
  () => agent(
    'You are the FULL-STACK developer (Angular SPA + Android). Implement T-0195 — client back-off honoring ' +
    'Retry-After. Ticket: agents/backlog/tickets/T-0195-*.md (find + read it). Two halves: ' +
    '(1) SPA: a shared HTTP interceptor (libs/core/services or the established interceptor home — read the ' +
    'existing auth interceptor first) that, on 429 with Retry-After, backs off + retries with jitter per the ' +
    'ticket AC (bounded retries; no retry-storm; non-429 untouched). Register it in all 3 web apps the way ' +
    'the existing interceptors are registered. Jest tests: 429+Retry-After honored, jitter bounded, non-429 ' +
    'passthrough, max-retries respected. ' +
    '(2) Android: the same honoring in the 2 NetworkModule.kt (an OkHttp interceptor; read the :core network ' +
    'setup first; strings not needed — no UI). Unit-test if the module has test infrastructure. ' +
    'No nx prod-build regression: run nx build for the 3 web apps after. ' + COMMON +
    '\nReturn: the interceptor files + registrations, Android interceptor, test names + results, the 3 web ' +
    'builds + (if runnable) gradle status.',
    { label: 'dev:T-0195', phase: 'Build', agentType: 'frontend' },
  ),
])

phase('Review')
const reviews = await parallel([
  () => agent('REVIEWER for T-0193 (account lockout). Verify: lockout thresholds + window per the ticket ACs; ' +
    'per-code attempt cap on reset/confirm paths; no user enumeration (same error shape); ' +
    'CleansiaStartupBase.cs untouched; User.cs/config changes additive + ef-migration flagged; tests ' +
    'non-vacuous + test-first. Run the gate (build + Auth/Users filters). Verdict with file:line.\nDEV ' +
    'REPORT:\n' + t0193, { label: 'review:T-0193', phase: 'Review', agentType: 'reviewer' }),
  () => agent('SECURITY reviewer for T-0193 (credential brute-force control). Verify: the lockout cannot be ' +
    'bypassed (cap enforced server-side per-account not per-IP only); no enumeration leak via timing/shape; ' +
    'the unlock window is sane; the code-attempt cap kills the reset-code brute-force window; no DoS vector ' +
    '(an attacker locking out arbitrary accounts — check the ticket AC for the mitigation). Verdict ' +
    'PASS/PASS-WITH-NOTES/FAIL with file:line.\nDEV REPORT:\n' + t0193,
    { label: 'security:T-0193', phase: 'Review', agentType: 'security' }),
  () => agent('REVIEWER for T-0194 (rate-limit sweep). Verify: every enumerated money/side-effect endpoint on ' +
    'every host carries the right [EnableRateLimiting] policy; no duplicates on already-annotated endpoints; ' +
    'CleansiaStartupBase/policy definitions untouched; the reflection guard test exists and would catch a ' +
    'future unannotated endpoint. Run the gate. Verdict with file:line.\nDEV REPORT:\n' + t0194,
    { label: 'review:T-0194', phase: 'Review', agentType: 'reviewer' }),
  () => agent('SECURITY reviewer for T-0194 (S5 closure). Verify the sweep against the S5 law: no remaining ' +
    'unthrottled money-out / mass-side-effect / credential endpoint on any host (spot-check by grepping ' +
    'HttpPost across the controllers and diffing against EnableRateLimiting); the chosen policies match the ' +
    'sibling precedents (auth bucket for session/money). Verdict PASS/PASS-WITH-NOTES/FAIL with ' +
    'file:line.\nDEV REPORT:\n' + t0194, { label: 'security:T-0194', phase: 'Review', agentType: 'security' }),
  () => agent('REVIEWER for T-0195 (client Retry-After back-off). Verify: the SPA interceptor honors ' +
    'Retry-After with bounded jittered retries, passes non-429 untouched, registered in all 3 apps the ' +
    'established way; the Android OkHttp interceptor mirrors it in both NetworkModules; tests cover the ' +
    'branches; the 3 web prod builds are green. Verdict with file:line.\nDEV REPORT:\n' + t0195,
    { label: 'review:T-0195', phase: 'Review', agentType: 'reviewer' }),
])

return {
  t0193: { dev: t0193, review: reviews[0], security: reviews[1] },
  t0194: { dev: t0194, review: reviews[2], security: reviews[3] },
  t0195: { dev: t0195, review: reviews[4] },
}
