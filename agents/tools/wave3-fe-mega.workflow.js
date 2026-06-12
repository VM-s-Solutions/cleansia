export const meta = {
  name: 'wave3-fe-mega',
  description: 'Wave-3 frontend halves: admin chain serial (171d -> shell 175b/176/186 -> misc 190/191) parallel with customer (187/192/178), partner web (171e), Android (X-Device-Id + devices + pay) lanes; reviewer per dev',
  phases: [
    { title: 'Build+Review', detail: '4 lanes; admin-locale writers serialized inside the admin chain' },
  ],
}

const FE_RULES = [
  'FRONTEND RULES (non-negotiable): Angular 19 standalone, OnPush presentational components; ALL logic in a',
  'FACADE (extends UnsubscribeControlDirective), signals for state, takeUntil(destroyed$) + finalize.',
  'cleansia-*/PrimeNG ONLY (never raw button/select/input/form). Every user-visible string via TranslatePipe',
  'with keys in ALL 5 locales of the OWNING app (admin: apps/cleansia-admin.app/src/assets/i18n/*.json;',
  'customer: apps/cleansia.app/...; partner: apps/cleansia-partner.app/...) with REAL native translations.',
  'No any type. Use the regenerated NSwag types; NEVER hand-edit generated clients. Three explicit data',
  'states. No inline templates/styles. Comment discipline: almost none, no task-number refs. Error-contract',
  'parity: every backend error code an action returns has an errors.* translation x5. Run nx lint + nx test',
  'for each touched lib to green (no NEW lint errors; pre-existing baseline out of scope). Mirror the',
  'established admin facade/component archetypes (read a sibling first).',
].join('\n')

phase('Build+Review')
const [adminChain, customerLane, partnerLane, androidLane] = await parallel([
  // ===== ADMIN CHAIN (serial: ALL admin-locale writers + the admin-shell cluster) =====
  async () => {
    const d171 = await agent(
      'You are the FRONTEND developer (Cleansia ADMIN app). Implement T-0171d — the admin payroll UI for the ' +
      'backend that just landed. Ticket: agents/backlog/tickets/T-0171-aud-02-04.md (AC4 + AC7). The admin ' +
      'client is regenerated and carries: adminPayrollClient (update-amounts / dispute / reject / ' +
      'generate-invoice) and adminPayPeriodClient mark-paid / reopen (read the generated client for exact ' +
      'names/DTOs). On the EXISTING admin payroll/pay-period screens add: invoice bonus/deduction adjustment, ' +
      'dispute (with notes), reject (with notes), period mark-paid + reopen, AND the AC4 failed-PDF recovery ' +
      'surface (show PdfGenerationFailed + PdfGenerationError on the invoice list/detail with a retry action ' +
      'invoking the existing RegenerateInvoicePdf endpoint). i18n x5 incl. the new error keys ' +
      '(payroll.invoice.already_paid, pay_period.already_paid). TEST-FIRST facade specs then component specs. ' +
      'Do NOT touch app.component.ts / app.routes.ts (the shell lane owns them this batch).\n' + FE_RULES +
      '\nReturn: files, actions wired, i18n keys x5, test results, lint status.',
      { label: 'dev:171d-payroll-ui', phase: 'Build+Review', agentType: 'frontend' },
    )
    const r171 = await agent(
      'REVIEWER for T-0171d (admin payroll UI). Verify: adjustment/dispute/reject/mark-paid/reopen actions ' +
      'via cleansia-*/facade/OnPush; the failed-PDF state + retry surface (AC4); error parity x5 incl. ' +
      'payroll.invoice.already_paid + pay_period.already_paid; no shell edits; no new lint errors. Run nx ' +
      'lint+test on the touched libs. Verdict APPROVE/APPROVE-WITH-NITS/REQUEST-CHANGES with file:line.\n' +
      'DEV REPORT:\n' + d171,
      { label: 'review:171d', phase: 'Build+Review', agentType: 'reviewer' },
    )
    const dShell = await agent(
      'You are the FRONTEND developer (Cleansia ADMIN app). Implement the admin-shell chain SERIALLY in this ' +
      'one agent (shared app.component.ts sidebar + app.routes.ts + admin locales):\n' +
      '1. T-0175b — membership-plan management UI (ticket T-0175-lg-04.md): new admin feature lib (mirror ' +
      'disputes-management structure): paged plan list (Code, Name, interval, price, monthly-equivalent, ' +
      'discount %, trial days, free-cancel window, express flag, active) + create/edit forms (Code/interval ' +
      'create-only; StripePriceId admin-entered text field) + deactivate. adminMembershipClient from the ' +
      'regenerated client. ONE sidebar entry + ONE lazy route (adminGuard). Gate nav with *cleansiaPermission ' +
      'on the membership-view policy. i18n x5 incl. membership.plan.code_already_exists + ' +
      'membership.plan.discount_out_of_range.\n' +
      '2. T-0176 UI — referral intervention (ticket T-0176-lg-05-06f-09.md): on the EXISTING admin referral ' +
      'screen add reverse + force-qualify actions (reason required) via adminReferralClient, and wire the ' +
      'by-user referral view the backend exposed. i18n x5 incl. referral.not_qualified / referral.not_accepted ' +
      '/ referral.reason_required. Add a sidebar/route entry ONLY if the referral screen lacks one.\n' +
      '3. T-0186 — GDPR data-protection UI (ticket T-0186-ia-01-03.md): the backend already exists — wire the ' +
      'admin Data-Protection feature (GDPR delete/anonymize per the ticket ACs) + the partner-app GDPR ' +
      'self-service slice IF the ticket scopes it frontend-only (read the ticket; partner slice edits the ' +
      'PARTNER app, not admin shell). Admin part = the LAST sidebar entry in this chain.\n' +
      'TEST-FIRST facade specs per feature. ' + FE_RULES +
      '\nReturn: per-ticket files, sidebar/route entries added (must be exactly one per feature needing one), ' +
      'i18n keys x5, test results, lint status.',
      { label: 'dev:shell-175b-176-186', phase: 'Build+Review', agentType: 'frontend' },
    )
    const rShell = await agent(
      'REVIEWER for the admin-shell chain (T-0175b membership UI, T-0176 referral intervention UI, T-0186 ' +
      'GDPR UI). Verify: each feature follows the facade/OnPush/cleansia-* archetype; sidebar+route edits are ' +
      'minimal/additive and serialized (no duplicate entries); permission-gated nav; error parity x5 for the ' +
      'new keys (membership.plan.*, referral.*); StripePriceId is a plain admin-entered field (no Stripe call ' +
      'from the UI); no new lint errors. Run nx lint+test on the new/touched libs. Verdict with file:line per ' +
      'ticket.\nDEV REPORT:\n' + dShell,
      { label: 'review:shell', phase: 'Build+Review', agentType: 'reviewer' },
    )
    const dMisc = await agent(
      'You are the FRONTEND developer (Cleansia ADMIN app). Implement the admin-misc pair SERIALLY (shared ' +
      'admin locales; disjoint from the shell):\n' +
      '1. T-0190 AC5 — on the admin profile: a change-password form (current + new + confirm) calling the new ' +
      'change-own-password endpoint (read the regenerated client for the exact method); on the admin ' +
      'create/edit user forms: BirthDate (date picker, past-only, 18+ hint) + PreferredLanguageCode (select ' +
      'over active languages). i18n x5 incl. errors.auth.current_password_invalid (NEW — the reviewer flagged ' +
      'it missing; add real translations).\n' +
      '2. T-0191 UI (AC5/AC6 of the catalog lane) — on the service + package management screens: ' +
      'activate/deactivate toggle actions + an IsActive filter (all/active/inactive) on the lists; on the ' +
      'currency management screen: a set-as-default action. Use the regenerated client (deactivate/activate/' +
      'set-default + isActive filter fields). i18n x5 for any new strings.\n' +
      '3. REGEN-DRIFT REPAIR (blocking the admin prod build — fix FIRST so your own work compiles): the ' +
      'regenerated admin client changed signatures and broke 5 untouched libs. Run ' +
      'npx nx build cleansia-admin.app to see the exact errors, then fix every call site: ' +
      '(a) admin-user-management constructs CreateAdminUserCommand/UpdateAdminUserCommand with the OLD shape ' +
      '— update the form/facade to the new command shape (birthDate/preferredLanguageCode are the new ' +
      'optional fields; wiring actual UI inputs for them is part 1 of this lane anyway); ' +
      '(b) employee-management, package-management, pay-config-management, service-management call paged ' +
      'queries with stale parameter shapes (TS2345: number not assignable to SortDefinition[], ' +
      'SortDefinition[] not assignable to boolean) — the regenerated getPaged signatures inserted/reordered ' +
      'params (e.g. the new isActive filter); fix each call site to the new positional signature. Do NOT ' +
      'hand-edit the generated client. The admin prod build MUST be green before you return.\n' +
      'TEST-FIRST facade specs. ' + FE_RULES +
      '\nReturn: files, the forms/toggles/filters wired, the regen-drift call sites fixed per lib, i18n keys ' +
      'x5, test results, lint status, and the admin prod-build result (must be green).',
      { label: 'dev:misc-190-191-ui', phase: 'Build+Review', agentType: 'frontend' },
    )
    const rMisc = await agent(
      'REVIEWER for the admin-misc pair (T-0190 AC5 profile/password UI, T-0191 catalog lifecycle UI) + the ' +
      'regen-drift repair. Verify: change-password posts current+new (no user id field anywhere client-side), ' +
      'errors.auth.current_password_invalid x5; BirthDate past-only + language select; activate/deactivate ' +
      'toggles + IsActive filter + set-default-currency wired to the regenerated client; facade/OnPush/' +
      'cleansia-*; no new lint errors; AND the regen-drift is fixed — run npx nx build cleansia-admin.app ' +
      'yourself and confirm it is GREEN (the 5 broken libs compile; no hand-edits to the generated client). ' +
      'Run nx lint+test. Verdict with file:line per ticket.\nDEV REPORT:\n' + dMisc,
      { label: 'review:misc', phase: 'Build+Review', agentType: 'reviewer' },
    )
    return { d171, r171, dShell, rShell, dMisc, rMisc }
  },
  // ===== CUSTOMER lane (customer-app locales — disjoint files) =====
  async () => {
    const dev = await agent(
      'You are the FRONTEND developer (Cleansia CUSTOMER app — apps/cleansia.app, SSR). Implement three ' +
      'customer tickets SERIALLY (shared customer locales):\n' +
      '1. T-0187 — notification-preferences UI (ticket T-0187-ia-02.md): backend + client already exist; ' +
      'build the preferences screen per the ticket ACs (read it).\n' +
      '2. T-0192 — customer dispute evidence/refund UI + status filter + saved-address management UI (ticket ' +
      'T-0192-d-04-d-10-da-17.md): backend exists. NOTE: the customer client was NOT regenerated this round — ' +
      'DisputeReason.Chargeback (=8) is missing from the generated enum; render unknown reason values with a ' +
      'safe fallback label rather than crashing, and note the pending regen.\n' +
      '3. T-0178 — the /r/{code} referral web landing route (ticket T-0178-lg-02.md): pure frontend, no dep.\n' +
      'TEST-FIRST facade specs per feature. i18n x5 in the CUSTOMER app locale files. ' + FE_RULES +
      '\nReturn: per-ticket files, i18n keys x5, test results, lint status, the Chargeback-fallback note.',
      { label: 'dev:customer-187-192-178', phase: 'Build+Review', agentType: 'frontend' },
    )
    const review = await agent(
      'REVIEWER for the customer lane (T-0187 notification prefs, T-0192 dispute evidence/refund + ' +
      'saved-address UI, T-0178 /r/{code} landing). Verify each against its ticket ACs: facade/OnPush/' +
      'cleansia-*, three states, i18n x5 in the CUSTOMER locales, error parity, the unknown-DisputeReason ' +
      'fallback (no crash on enum value 8), SSR-safety on the landing route (no window/document at ' +
      'construction). No new lint errors. Run nx lint+test. Verdict with file:line per ticket.\nDEV REPORT:\n' + dev,
      { label: 'review:customer', phase: 'Build+Review', agentType: 'reviewer' },
    )
    return { dev, review }
  },
  // ===== PARTNER WEB lane (partner-app locales — disjoint files) =====
  async () => {
    const dev = await agent(
      'You are the FRONTEND developer (Cleansia PARTNER app — apps/cleansia-partner.app). Implement T-0171e ' +
      '(web slice): the read-only "my period pay" screen. The partner client is regenerated: getPeriodPays ' +
      'returns the caller-scoped period pay summary; the settlement mutations are REMOVED from the client ' +
      '(verify nothing in the partner app still references approveInvoice/markInvoicePaid/closePayPeriod/' +
      'generateInvoice/cancelInvoice — remove any dead UI/calls that referenced them, this is part of the ' +
      'AUD-04 cleanup). Build/extend the partner payroll screen to show the read-only period-pay summary ' +
      '(per-order pay lines, totals, period status) with NO settlement write actions. i18n x5 in the PARTNER ' +
      'locale files. TEST-FIRST facade spec. ' + FE_RULES +
      '\nReturn: files, removed dead references (list them), i18n keys x5, test results, lint status.',
      { label: 'dev:171e-partner-pay', phase: 'Build+Review', agentType: 'frontend' },
    )
    const review = await agent(
      'REVIEWER for T-0171e (partner read-only pay). Verify: the screen is READ-ONLY (zero settlement write ' +
      'actions/calls; grep the partner app for approveInvoice/markInvoicePaid/closePayPeriod/cancelInvoice — ' +
      'must be zero references); caller-scoped data only; facade/OnPush/cleansia-*; i18n x5 partner locales; ' +
      'no new lint errors. Run nx lint+test. Verdict with file:line.\nDEV REPORT:\n' + dev,
      { label: 'review:171e', phase: 'Build+Review', agentType: 'reviewer' },
    )
    return { dev, review }
  },
  // ===== ANDROID lane (src/cleansia_android — fully disjoint) =====
  async () => {
    const dev = await agent(
      'You are the ANDROID developer (src/cleansia_android: :core, :partner-app, :customer-app; Kotlin + ' +
      'Compose + Hilt + MVVM/StateFlow). Three related deliverables:\n' +
      '1. X-Device-Id header (REQUIRED for T-0188 session-kill to work on mobile): the AuthInterceptor ' +
      '(core auth/network) currently sends X-Device-Label only; also send X-Device-Id = the SAME stable ' +
      'per-install id the app registers for push (PushTokenRepository.resolveDeviceId or equivalent — find ' +
      'the real source and reuse it, do not invent a second id). Both apps.\n' +
      '2. T-0188 AC4 — device self-service screen in profile/settings (both apps): list my devices ' +
      '(GET mine), identify the current device, revoke (DELETE by id) with confirmation; revoked device ' +
      'disappears. Use the existing retrofit/network layer patterns (read a sibling screen first: ViewModel + ' +
      'StateFlow + Compose, string resources for ALL text in the app respective values*/strings.xml — the ' +
      'apps are localized; add all new strings to every strings.xml variant present).\n' +
      '3. T-0171e Android slice — read-only "my period pay" screen in the PARTNER app (mirror the web ' +
      'read-only contract; no settlement actions; remove any dead references to the removed settlement ' +
      'endpoints if the partner app had them).\n' +
      'Follow the existing module conventions (read the :core network + a feature package first). Unit-test ' +
      'ViewModels where the codebase has ViewModel tests. Run the gradle build for the touched modules ' +
      '(gradlew :core:assembleDebug etc.) if the environment allows; if gradle is unavailable, state it and ' +
      'ensure the Kotlin compiles by careful review. Return: files, the device-id source reused, screens ' +
      'added, strings added per locale file, build/test status.',
      { label: 'dev:android', phase: 'Build+Review', agentType: 'android' },
    )
    const review = await agent(
      'REVIEWER for the Android lane (X-Device-Id interceptor + device management screens + partner read-only ' +
      'pay). Verify: X-Device-Id uses the SAME id source the push registration uses (no second identity); the ' +
      'interceptor change is in :core and applies to both apps; device screens follow the module MVVM/' +
      'StateFlow/Compose conventions with localized strings in all present strings.xml variants; the partner ' +
      'pay screen is read-only; no dead settlement-endpoint references. Check the gradle/build status the dev ' +
      'reported. Verdict APPROVE/APPROVE-WITH-NITS/REQUEST-CHANGES with file:line.\nDEV REPORT:\n' + dev,
      { label: 'review:android', phase: 'Build+Review', agentType: 'reviewer' },
    )
    return { dev, review }
  },
])

return { adminChain, customerLane, partnerLane, androidLane }
