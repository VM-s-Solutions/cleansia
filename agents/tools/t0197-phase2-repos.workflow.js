export const meta = {
  name: 't0197-phase2-repos',
  description: 'T-0197 Phase 2: migrate 15 customer-app repos to ApiResult<T> (snackbar moves repo->VM), characterization-test-first. Densely VM-shared repos run SERIAL; isolated repos run parallel.',
  phases: [
    { title: 'isolated repos', detail: 'PushToken, DeviceMgmt, NotificationPrefs, Payment, Auth, AppSettings — disjoint VMs' },
    { title: 'shared-VM cluster', detail: 'Order/Address/Membership/Loyalty/Catalog/Recurring/User/Referral/Dispute — serial (share Home/MainShell VMs)' },
  ],
}

const ROOT = 'c:/Users/cmisa/Desktop/Mike/Projects/cleansia'
const ANDROID = ROOT + '/src/cleansia_android'
const CUST = 'src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer'

const COMMON = [
  'Repo root: ' + ROOT + '. Branch feature/wave-6 is checked out (T-0197 Phase 1 committed: ApiResult/ApiError/safeApiCall now live in cz.cleansia.core.network in the :core module). Do NOT commit, push, or git add.',
  'Today is 2026-06-15. This is T-0197 Phase 2 — migrate ONE customer-app repository (and its consuming ViewModels) from the legacy T?/sentinel-with-snackbar-in-repo pattern to returning ApiResult<T>, moving the snackbar into the ViewModel. Read agents/backlog/tickets/T-0197-t-0016.md (AC3-AC7), agents/backlog/adr/0011-mobile-apiresult-contract.md, and agents/knowledge/consistency.md (E5/E3).',
  'BEHAVIOR-PRESERVING ONLY. The same calls succeed/fail the same way; only WHERE the error becomes a snackbar moves (repo -> VM). Same single snackbar message per failure (built from ApiErrorParser.parseToUserMessage, now surfaced by the VM via .onError/errorOrNull). Background/silent loaders keep silent behavior (map Error to a no-op in the VM). Do NOT also do the E1/E2 sealed-UiState migration — only the repo->VM error channel. No new endpoints, messages, retry UX, or caching.',
  'CANONICAL PATTERN: mirror partner-app repos (which already return ApiResult<T> via safeApiCall). Repo: `suspend fun foo(...): ApiResult<T> = safeApiCall { api.foo(...) }` (use Unit for former Boolean/fire-and-forget). Import cz.cleansia.core.network.{ApiResult, ApiError, safeApiCall}. VM: branch on Success/Error, `.onError { snackbar.showError(it.message) }`.',
  'TEST-FIRST (AC3): for the repo you migrate, FIRST write/extend a characterization test (follow customer-app OrderRepositoryTest.kt / AuthRepositoryTest.kt harness shape) pinning CURRENT behavior — success returns the body; failure fires exactly ONE snackbar with the ApiErrorParser message and yields the failure sentinel. Confirm it is GREEN against current code. THEN migrate; the test asserts the SAME success/failure outcome and the SAME single message (now raised by the VM). Record characterization-green -> migrate -> green.',
  'ENCODING DISCIPLINE (CRITICAL — Phase 1 had a corruption incident): every .kt file you write/edit MUST stay clean UTF-8 with NO BOM and NO mojibake (no Ã/Â/â€/â† byte sequences). Kotlin source is ASCII-clean here — if your edit introduces any non-ASCII byte, you corrupted it; fix it. After editing, the file must be byte-clean.',
  'VERIFY (mandatory, per repo): from ' + ANDROID + ', run `./gradlew.bat :customer-app:compileDebugKotlin --offline -q` (must be EXIT 0) and `./gradlew.bat :customer-app:testDebugUnitTest --offline -q` for your new/changed tests. Known pre-existing customer-app test state: most pass; if a failure is unrelated to your repo (e.g. android.util.Patterns in a login VM), note it — do not chase it. Report exact results.',
  'No nswag-regen, no ef-migration (mobile-only). Touch ONLY your repo file, its consuming ViewModels, and its test. If a ViewModel you must edit is ALSO listed as another lane\'s VM, you are in the SERIAL cluster — your lane runs alone, so just edit it; never assume another lane is concurrently in the same VM. Your final message is data for the orchestrator.',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

const DEV_SCHEMA = {
  type: 'object',
  required: ['summary', 'repoMigrated', 'filesChanged', 'vmsUpdated', 'characterizationEvidence', 'verificationAchieved', 'encodingClean', 'deviations'],
  properties: {
    summary: { type: 'string', maxLength: 600 }, repoMigrated: { type: 'string', maxLength: 300 },
    filesChanged: { type: 'array', items: { type: 'string', maxLength: 300 } },
    vmsUpdated: { type: 'array', items: { type: 'string', maxLength: 300 } },
    characterizationEvidence: { type: 'string', maxLength: 600 }, verificationAchieved: { type: 'string', maxLength: 600 },
    encodingClean: { type: 'boolean', description: 'true if you confirmed all edited .kt files are clean UTF-8 no-BOM no-mojibake' },
    deviations: { type: 'array', items: { type: 'string', maxLength: 300 } },
  },
}
const REVIEW_SCHEMA = {
  type: 'object', required: ['verdict', 'mustFix', 'notes'],
  properties: { verdict: { type: 'string', enum: ['PASS', 'PASS-WITH-NOTES', 'FAIL'] }, mustFix: { type: 'array', items: { type: 'string', maxLength: 300 } }, notes: { type: 'array', items: { type: 'string', maxLength: 300 } } },
}

function reviewPrompt(t, dev) {
  return [
    'Reviewer gate for T-0197 Phase 2 — migration of ' + t.repo + ' to ApiResult<T>.',
    COMMON,
    'Developer reports: ' + JSON.stringify(dev),
    'Gate: (1) the repo methods now return ApiResult<T> (Unit for fire-and-forget) via safeApiCall, importing from cz.cleansia.core.network; the repo NO LONGER calls snackbar.showError; (2) the snackbar moved to the consuming VM(s) with the SAME single message per failure (behavior-preserving — silent loaders stay silent); (3) a characterization test pins the behavior and was green before+after; (4) ENCODING: git diff the changed .kt files — ANY BOM or mojibake (Ã/Â/â€ byte) is a FAIL; (5) VERIFY-NOT-TRUST: run :customer-app:compileDebugKotlin (EXIT 0) and the repo test yourself from ' + ANDROID + '; (6) no E1/E2 UiState change, no behavior change. FAIL with concrete mustFix on any compile/test red, encoding corruption, behavior change, or leftover in-repo snackbar.',
  ].join('\n\n')
}

async function runRepo(t) {
  log(t.repo + ': migrating')
  const dev = await agent([COMMON, 'MIGRATE THIS REPO: ' + CUST + '/' + t.path + ' (and its consuming ViewModels: ' + t.vms + '). ' + (t.note || '')].join('\n\n'), { label: 'dev:' + t.repo, phase: t.phase, agentType: 'android', schema: DEV_SCHEMA })
  if (!dev) return { repo: t.repo, verdict: 'AGENT-LOST' }
  log(t.repo + ': migrated — reviewing')
  const review = await agent(reviewPrompt(t, dev), { label: 'review:' + t.repo, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA })
  if (review && review.verdict === 'FAIL') {
    log(t.repo + ': FAIL — fix lane')
    const fix = await agent([COMMON, 'Fix these blocking findings on the ' + t.repo + ' migration, nothing else:', (review.mustFix || []).map(function (f, i) { return (i + 1) + '. ' + f }).join('\n'), 'Re-run :customer-app:compileDebugKotlin + the repo test from ' + ANDROID + '. Confirm encoding clean.'].join('\n\n'), { label: 'fix:' + t.repo, phase: t.phase, agentType: 'android', schema: DEV_SCHEMA })
    const regate = await agent([reviewPrompt(t, fix || dev), 'This is a RE-GATE after a fix. Original findings: ' + (review.mustFix || []).join(' | ')].join('\n\n'), { label: 'regate:' + t.repo, phase: t.phase, agentType: 'reviewer', schema: REVIEW_SCHEMA })
    const v = regate ? regate.verdict : 'REGATE-LOST'
    log(t.repo + ': ' + v)
    return { repo: t.repo, verdict: v, dev: dev, fix: fix }
  }
  const v = review ? review.verdict : 'NO-REVIEW'
  log(t.repo + ': ' + v)
  return { repo: t.repo, verdict: v, dev: dev }
}

// RESUME CONTEXT (2026-06-16): the first run rate-limited mid-flight and these 4 lanes died AGENT-LOST,
// leaving PARTIAL non-compiling WIP in the tree (their repos already reference ApiResult but their VMs/
// modules don't, so :customer-app:compileDebugKotlin currently FAILS). The 10 serial cluster lanes ALL
// LANDED GREEN already (their edits to shared VMs like BookingViewModel + ApiErrorParser's (Context,ApiError)
// overload are present). Your job on resume: COMPLETE your lane's migration to a COMPILING state — finish
// whatever the dead agent half-did (read the current partial state of your repo + its VM first; do not assume
// a clean baseline), adopt the established ApiErrorParser.parseToUserMessage(context, error) pattern the
// cluster lanes use, and for shared VMs only add YOUR repo's call sites (other lanes already added theirs).
const RESUME_NOTE = ' RESUME: the first attempt died mid-edit and left partial ApiResult WIP in your repo file — the tree does NOT compile because of it. READ the current (partial) state of your repo + VM, then DRIVE IT TO GREEN. The 10 cluster lanes already landed (shared VMs + ApiErrorParser (Context,ApiError) overload exist) — for any shared VM, add only YOUR repo call sites. Verify :customer-app:compileDebugKotlin = EXIT 0 on the FULL tree (not isolation) before reporting.'

// Isolated repos (disjoint VMs) — safe to parallelize
const ISOLATED = [
  { repo: 'PushTokenRepository', path: 'core/notifications/PushTokenRepository.kt', vms: '(no direct VM — used by push registration/session)', phase: 'isolated repos' },
  { repo: 'DeviceManagementRepository', path: 'core/devices/DeviceManagementRepository.kt', vms: 'DevicesViewModel', phase: 'isolated repos', note: RESUME_NOTE },
  { repo: 'NotificationPreferencesRepository', path: 'core/notifications/NotificationPreferencesRepository.kt', vms: 'NotificationPreferencesViewModel', phase: 'isolated repos', note: RESUME_NOTE },
  { repo: 'PaymentRepository', path: 'core/payments/PaymentRepository.kt', vms: 'BookingViewModel', phase: 'isolated repos', note: RESUME_NOTE + ' BookingViewModel is shared with User/Referral (already landed) — add ONLY the PaymentRepository call sites.' },
  { repo: 'AuthRepository', path: 'core/auth/AuthRepository.kt', vms: 'AuthViewModel, SessionViewModel, AuthModule', phase: 'isolated repos', note: RESUME_NOTE + ' Known hard error: AuthRepository.kt:162 calls a suspend clear() from a non-suspend context (the dead agent\'s half-done handleAuthBody refactor) — fix that. AuthModule.kt ctor (Context vs Json) must match the final AuthRepository ctor.' },
]

// Shared-VM cluster (Home/MainShell/BookingSheet/CreateRecurring/Rewards heavily shared) — SERIAL
const CLUSTER = [
  { repo: 'CatalogRepository', path: 'core/catalog/CatalogRepository.kt', vms: 'BookingSheetViewModel, ConfirmStepViewModel, ServicesStepViewModel, HomeTabViewModel, MainShellViewModel, CreateRecurringViewModel', phase: 'shared-VM cluster' },
  { repo: 'AddressRepository', path: 'core/data/AddressRepository.kt', vms: 'AddressManagerViewModel, BookingSheetViewModel, HomeTabViewModel, MainShellViewModel, CreateRecurringViewModel', phase: 'shared-VM cluster' },
  { repo: 'OrderRepository', path: 'core/orders/OrderRepository.kt', vms: 'BookingSheetViewModel, BookingSuccessViewModel, PreferredCleanerViewModel, HomeTabViewModel, MainShellViewModel, OrderDetailViewModel, OrderPhotosViewModel, CreateRecurringViewModel, RewardsActivityViewModel', phase: 'shared-VM cluster', note: 'OrderRepositoryTest.kt already exists — extend it as the characterization harness. Background loaders (loadNextPage, getMyServingCleaners) keep silent behavior.' },
  { repo: 'MembershipRepository', path: 'core/memberships/MembershipRepository.kt', vms: 'ConfirmStepViewModel, PreferredCleanerViewModel, HomeTabViewModel, MembershipViewModel, OrderDetailViewModel', phase: 'shared-VM cluster' },
  { repo: 'LoyaltyRepository', path: 'core/loyalty/LoyaltyRepository.kt', vms: 'HomeTabViewModel, MainShellViewModel, RewardsActivityViewModel', phase: 'shared-VM cluster' },
  { repo: 'RecurringBookingRepository', path: 'core/recurring/RecurringBookingRepository.kt', vms: 'HomeTabViewModel, CreateRecurringViewModel, RecurringBookingsViewModel', phase: 'shared-VM cluster' },
  { repo: 'ReferralRepository', path: 'core/referral/ReferralRepository.kt', vms: 'SignUpViewModel, BookingViewModel, MainShellViewModel', phase: 'shared-VM cluster' },
  { repo: 'UserRepository', path: 'core/user/UserRepository.kt', vms: 'BookingViewModel, MainShellViewModel, DeleteAccountViewModel, ProfileViewModel', phase: 'shared-VM cluster' },
  { repo: 'AppSettingsRepository', path: 'core/settings/AppSettingsRepository.kt', vms: 'AuthViewModel, MainShellViewModel, ProfileViewModel', phase: 'shared-VM cluster' },
  { repo: 'DisputeRepository', path: 'core/disputes/DisputeRepository.kt', vms: 'CreateDisputeViewModel, DisputeDetailViewModel, DisputesListViewModel, RewardsActivityViewModel', phase: 'shared-VM cluster' },
]

phase('isolated repos')
log('Phase 2a: ' + ISOLATED.length + ' isolated repos in parallel')
const isoResults = await parallel(ISOLATED.map(function (t) { return function () { return runRepo(t) } }))

phase('shared-VM cluster')
log('Phase 2b: ' + CLUSTER.length + ' shared-VM-cluster repos SERIAL (they share Home/MainShell/BookingSheet/Rewards VMs)')
const clusterResults = []
for (var i = 0; i < CLUSTER.length; i++) {
  clusterResults.push(await runRepo(CLUSTER[i]))
}

const all = isoResults.filter(Boolean).concat(clusterResults.filter(Boolean))
return {
  repos: all.map(function (r) {
    return { repo: r.repo, verdict: r.verdict, files: r.dev ? r.dev.filesChanged : [], vms: r.dev ? r.dev.vmsUpdated : [], encodingClean: r.dev ? r.dev.encodingClean : null, verification: r.dev ? r.dev.verificationAchieved : null, deviations: r.dev ? r.dev.deviations : [] }
  }),
}
