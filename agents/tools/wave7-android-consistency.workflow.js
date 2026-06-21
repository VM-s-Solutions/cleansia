export const meta = {
  name: 'wave7-android-consistency',
  description: 'Wave 7 — Android consistency debt: E7 dir/naming (T-0266), E1 sealed UiState (T-0267), E2 verify-close (T-0268), E6 collectAsStateWithLifecycle sweep (T-0269). Partner lane strict-serial (E7->E1->E6); E2-verify parallel.',
  phases: [
    { title: 'partner serial lane', detail: 'T-0266 E7 move -> T-0267 E1 sealed UiState -> T-0269 E6 sweep (shared files)' },
    { title: 'parallel', detail: 'T-0268 E2 verify-close (no code)' },
  ],
}

const ROOT = 'c:/Users/cmisa/Desktop/Mike/Projects/cleansia'
const ANDROID = ROOT + '/src/cleansia_android'

const COMMON = [
  'Repo root: ' + ROOT + '. Branch feature/wave7-android-consistency is checked out. Do NOT commit, push, or git add.',
  'Today is 2026-06-21. This is Wave 7 — Android CONSISTENCY DEBT (mobile-only, BEHAVIOR-PRESERVING). Read your ticket IN FULL, agents/knowledge/consistency.md (the E1/E2/E6/E7 rule + its judgment-call rationale), and agents/knowledge/testing.md.',
  'BEHAVIOR-PRESERVING ONLY: the same screens render the same states, the same actions fire the same effects. You are changing STRUCTURE/SHAPE (sealed UiState, lifecycle-aware collection, directory layout), never logic. Existing tests must stay green; where you change a VM state shape, pin it with a characterization test first.',
  'Baseline: all 3 Android modules (:core, :partner-app, :customer-app) currently compile green.',
  'ENCODING DISCIPLINE (a prior Android wave had a corruption incident): every .kt file you write/edit/move MUST stay clean UTF-8, NO BOM, NO mojibake (no Ã/Â/â€ byte sequences). After editing, confirm byte-clean. For E7 file MOVES, preserve the exact file content byte-for-byte except the package/import lines.',
  'VERIFY (mandatory): from ' + ANDROID + ', run ./gradlew.bat <modules>:compileDebugKotlin --offline -q (EXIT 0) and the relevant :<app>:testDebugUnitTest --offline -q. Report exact pass/fail. Known pre-existing: NONE now — partner-app is 26/26, customer-app 201/201, :core 13/13 green at baseline, so ANY new red is YOURS.',
  'No nswag-regen, no ef-migration, no i18n (mobile structural). Comment discipline: no ticket IDs in source. Final message is data for the orchestrator.',
].join('\n')

const DEV_SCHEMA = {
  type: 'object',
  required: ['summary', 'filesChanged', 'verificationAchieved', 'encodingClean', 'behaviorPreserved', 'deviations'],
  properties: {
    summary: { type: 'string' }, filesChanged: { type: 'array', items: { type: 'string' } },
    verificationAchieved: { type: 'string' }, encodingClean: { type: 'boolean' },
    behaviorPreserved: { type: 'string' }, deviations: { type: 'array', items: { type: 'string' } },
  },
}
const REVIEW_SCHEMA = {
  type: 'object', required: ['verdict', 'mustFix', 'notes'],
  properties: { verdict: { type: 'string', enum: ['PASS', 'PASS-WITH-NOTES', 'FAIL'] }, mustFix: { type: 'array', items: { type: 'string' } }, notes: { type: 'array', items: { type: 'string' } } },
}

function reviewPrompt(t, dev) {
  return [
    'Reviewer gate for ' + t.id + ' (Wave 7 Android consistency) on the Cleansia repo.',
    COMMON, 'Ticket: ' + t.ticket,
    'Developer reports: ' + JSON.stringify(dev),
    'Gate: (1) every AC met; (2) BEHAVIOR-PRESERVING — confirm no logic changed (only UiState shape / collection API / file location); existing tests green unchanged; (3) the rule is actually satisfied for the scoped files (read the consistency.md rule + spot-check the diff); (4) ENCODING: git diff the changed/moved .kt — any BOM/mojibake is a FAIL; for E7 moves, the content must be byte-identical except package/import lines; (5) VERIFY-NOT-TRUST: run the relevant compileDebugKotlin (EXIT 0) + testDebugUnitTest yourself from ' + ANDROID + '; (6) no out-of-scope changes (E6 must not refactor UiState; E1 must not move files; etc.). FAIL with concrete mustFix on any red compile/test, encoding issue, behavior change, or unmet AC.',
    t.reviewExtra || '',
  ].join('\n\n')
}

async function runTicket(t) {
  log(t.id + ': dev starting (' + t.phase + ')')
  const dev = await agent([COMMON, t.prompt].join('\n\n'), { label: 'dev:' + t.id, phase: t.phase, agentType: 'android', schema: DEV_SCHEMA })
  if (!dev) return { id: t.id, verdict: 'AGENT-LOST' }
  log(t.id + ': dev done — reviewing')
  const review = await agent(reviewPrompt(t, dev), { label: 'review:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA })
  if (review && review.verdict === 'FAIL') {
    log(t.id + ': FAIL — fix lane')
    const fix = await agent([COMMON, 'Fix these blocking findings on ' + t.id + ', nothing else:', (review.mustFix || []).map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'), 'Re-run the relevant compile + tests from ' + ANDROID + '. Confirm encoding clean.'].join('\n\n'), { label: 'fix:' + t.id, phase: t.phase, agentType: 'android', schema: DEV_SCHEMA })
    const regate = await agent([reviewPrompt(t, fix || dev), 'RE-GATE after fix. Original findings: ' + (review.mustFix || []).join(' | ')].join('\n\n'), { label: 'regate:' + t.id, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA })
    const v = regate ? regate.verdict : 'REGATE-LOST'
    log(t.id + ': ' + v); return { id: t.id, verdict: v, dev: dev, fix: fix }
  }
  const v = review ? review.verdict : 'NO-REVIEW'
  log(t.id + ': ' + v); return { id: t.id, verdict: v, dev: dev }
}

const T0266 = {
  id: 'T-0266', ticket: 'agents/backlog/tickets/T-0266-android-e7-partner-dir-naming-unify.md', phase: 'partner serial lane',
  prompt: 'Implement T-0266 (E7): collapse partner-app\'s features/<name>/{screens,viewmodels,components}/ split into the canonical inline-singular features/<name>/ layout (the customer-app convention). This is a STRUCTURAL move + package/import rewrite ONLY — no logic change, file contents byte-identical except the package declaration and imports that reference moved files. Read the ticket for the exact directory list. Move the files, fix every package + import (across both production and test sources that reference them), and ensure :partner-app:compileDebugKotlin + :partner-app:testDebugUnitTest stay green. You go FIRST in the partner serial lane (T-0267/T-0269 follow you), so leave the paths settled and correct.',
}
const T0267 = {
  id: 'T-0267', ticket: 'agents/backlog/tickets/T-0267-android-e1-partner-sealed-uistate-residual.md', phase: 'partner serial lane',
  prompt: 'Implement T-0267 (E1): convert the two RESIDUAL partner flag-bag UiStates the audit named — InvoiceDetailsViewModel + OrderPhotosViewModel — to a sealed *UiState (Loading/Error/Loaded) following the canonical pattern T-0252 already applied to Dashboard/Earnings/OrderDetails (use those as the exemplar). T-0266 (E7 dir move) ran before you — the files are at their settled inline-singular paths now; build on that. Behavior-preserving: the same observable states, same data. Characterization-test-first: pin the current states, then refactor under it. Verify :partner-app:compileDebugKotlin + testDebugUnitTest green. You are SECOND in the serial lane (T-0269 E6 sweep follows).',
  reviewExtra: 'Confirm ONLY InvoiceDetails + OrderPhotos UiStates changed (the ticket\'s scope), the sealed shape matches the T-0252 exemplar, and the screens consuming them branch on the sealed cases. The judgment-call non-violations (dual-spinner list VMs, form-section VMs, OrderNotes) must be LEFT ALONE.',
}
const T0269 = {
  id: 'T-0269', ticket: 'agents/backlog/tickets/T-0269-android-e6-collectasstate-lifecycle-sweep.md', phase: 'partner serial lane',
  prompt: 'Implement T-0269 (E6): replace collectAsState() with collectAsStateWithLifecycle() across the FILTERED real violations — screen/composable collections of a ViewModel-owned, LIFECYCLE-BOUND flow — in BOTH partner-app and customer-app. Per the ticket: the real set is ~56 occurrences across ~30 files (NOT all 85 raw). EXCLUDE the recorded non-violations: @Singleton repository StateFlows collected in screens (loyaltyRepo/referralRepo in RewardsTab, orderRepo in OrdersTab, catalogRepo in ServicesStep/ConfirmStep, app-scoped membership flows), NavHost-level collections (CleansiaNavHost x9, PartnerNavHost x1), and :core infra (GlobalSnackbarHost). TOOL CAVEAT: check-consistency.mjs E6 regex only matches receivers literally named viewModel/vm — it MISSES bookingVm/chainViewModel/settingsViewModel/profileVm etc. Fix the full CONCEPTUAL set and confirm by re-grep, not just the tool. Add the collectAsStateWithLifecycle import where needed. T-0266 (E7) + T-0267 (E1) ran before you — sweep the SETTLED, REFACTORED files. You go LAST in the partner serial lane. Verify BOTH apps compile + their full unit suites green (partner 26/26, customer 201/201 must hold).',
  reviewExtra: 'Confirm the excluded non-violations (Singleton repo flows, NavHost, :core infra) were NOT changed, the full conceptual set (incl. non-viewModel-named receivers) was swept, and a re-grep for collectAsState() on screen/VM-flow lines comes back clean. No UiState/logic change (that is E1/E7 scope).',
}
const T0268 = {
  id: 'T-0268', ticket: 'agents/backlog/tickets/T-0268-android-e2-actionstate-verify-closeout.md', phase: 'parallel',
  prompt: 'Implement T-0268 (E2 verify-and-close): per the ticket, the shared ActionState + SharedFlow one-shot-effect pattern was ALREADY implemented by T-0252 — this ticket is VERIFY-ONLY, NO production edits expected. Scan the customer-app + partner-app VMs for E2 compliance (actions use the shared ActionState + a SharedFlow effect, not scattered booleans), run the consistency tool E2 check, confirm zero real E2 violations remain (judgment-call non-violations recorded), and close finding F14. If you find a GENUINE residual E2 violation the ticket did not anticipate, report it (do NOT fix it — it would need its own scoped ticket). Update the ticket status log with the verification evidence. This runs in PARALLEL with the partner serial lane — touch NO production files (so no collision).',
  reviewExtra: 'This is a verify-close. Confirm the dev made NO production code changes (git diff shows only the ticket file), the E2 pattern is genuinely in place (spot-check 2-3 VMs), and any claimed residual is real. If the dev edited production code, that is a FAIL (out of scope — it would collide with the serial lane).',
}

phase('parallel')
log('Wave 7: partner serial lane (T-0266 E7 -> T-0267 E1 -> T-0269 E6) || T-0268 E2 verify-close')

const results = await parallel([
  // partner serial lane (shared files) + customer E6 folded into T-0269 at the end
  async function () {
    const a = await runTicket(T0266)
    const b = await runTicket(T0267)
    const c = await runTicket(T0269)
    return [a, b, c]
  },
  // E2 verify — no production files, fully parallel
  function () { return runTicket(T0268) },
])

const all = results.filter(Boolean).flat()
return {
  tickets: all.map(function (r) {
    return { id: r.id, verdict: r.verdict, files: r.dev ? r.dev.filesChanged : [], encodingClean: r.dev ? r.dev.encodingClean : null, verification: r.dev ? r.dev.verificationAchieved : null, deviations: r.dev ? r.dev.deviations : [] }
  }),
}
