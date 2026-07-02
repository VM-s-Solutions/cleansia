export const meta = {
  name: 'wave3-3ce-pass2-fix',
  description: 'Close Pass-2 review findings: shared active-language validator extension (4x dup), strip tracker tags from test comments, T-0190 BirthDate validators reuse BeInPast/BeReasonableAge',
  phases: [
    { title: 'Fix', detail: 'one backend dev applies the 3 surgical fixes' },
    { title: 'Re-check', detail: 'reviewer confirms' },
  ],
}

const SPEC = [
  'Three precisely-specified review fixes from Pass 2 (catalog lane + T-0190). All contracts/security already',
  'PASS — these are convention/duplication/validation-floor fixes only. Do NOT change behavior beyond fix 3.',
  '',
  'FIX 1 (catalog duplication): the active-language translation-completeness validator block exists as 4',
  'verbatim copies: CreateService.cs:66-77, UpdateService.cs:70-81, CreatePackage.cs:49-67,',
  'UpdatePackage.cs:65-83 (all under src/Cleansia.Core.AppServices/Features/...). Extract ONE shared rule',
  'extension (e.g. MustCoverAllActiveLanguages taking the ILanguageRepository) in',
  'Common/Validators/ValidationExtensions.cs (or a sibling file there — match how BeInPast/BeReasonableAge',
  'live), and use it in all four validators. Keep the exact same error keys/messages',
  '(service.translations_required / service.missing_translation_for_language) and semantics (active',
  'languages, SetEquals). Then reword the patterns-backend.md sentence that says new catalog entities',
  '"copy this validator block verbatim" to point at the shared extension instead.',
  'All 34 CatalogTranslationCompletenessValidatorTests + the create/update validator tests must stay green.',
  '',
  'FIX 2 (comment discipline): strip tracker/process references from test doc comments — remove the',
  'CC-03 / CC-04 / CC-06 (owner path b, Q-W3-1) prefixes and every "Written TEST-FIRST." tag, KEEPING the',
  'behavioral contract sentences. Files: DeactivateActivateServiceHandlerTests.cs:9,',
  'CatalogActiveVisibilityTests.cs:16, SetDefaultCurrencyHandlerTests.cs:9,',
  'CatalogLifecycleEndpointPermissionTests.cs:7, CatalogTranslationCompletenessValidatorTests.cs:12, and any',
  'sibling test file in this lane carrying the same tags (grep for "CC-0" and "TEST-FIRST" under',
  'src/Cleansia.Tests — strip only in the Pass-2 catalog/T-0190 files; leave unrelated files alone).',
  '',
  'FIX 3 (T-0190 validator reuse + the silently-dropped 18+ floor):',
  'src/Cleansia.Core.AppServices/Features/AdminUsers/CreateAdminUser.cs:68-74 and UpdateAdminUser.cs:55-61',
  'reimplement BirthDate rules as inline lambdas (0-120) instead of the shared BeInPast/BeReasonableAge',
  'predicates (Common/Validators/ValidationExtensions.cs:13-23; canonical usage at',
  'Features/Users/UpdateCurrentUser.cs:43-54). The inline copy silently drops the 18+ floor — a birth date',
  'of yesterday currently passes for an ADMIN. Fix: use .Must(BeInPast) + .Must(BeReasonableAge) exactly like',
  'UpdateCurrentUser. Admins are NOT exempt from the 18+ floor (no divergence is recorded anywhere — adopt',
  'the canonical rule). Update/extend the AdminUserProfileFieldsTests to cover the 18+ floor (an under-18',
  'birth date is rejected) and keep the existing future-date/preservation tests green.',
  '',
  'RULES: no behavior change beyond fix 3’s floor; keep all error keys; comment discipline (the fix itself',
  'must not introduce tracker refs); build src/Cleansia.Api.sln + run the affected test filters green',
  '(Catalog/Services/Packages/Currencies/AdminUsers/ChangeOwnPassword); single-threaded if the metrics flake',
  'fires. Do NOT run dotnet ef / npm generate.',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

phase('Fix')
const dev = await agent(
  'You are the BACKEND developer. Apply the three surgical Pass-2 review fixes below exactly as specified.\n\n' +
  SPEC + '\n\nReturn: files changed per fix, the new shared rule-extension signature, the 18+ test added, ' +
  'test counts green per filter, build result.',
  { label: 'dev:pass2-fix', phase: 'Fix', agentType: 'backend' },
)

phase('Re-check')
const review = await agent(
  'You are the REVIEWER confirming the three Pass-2 fixes landed correctly:\n' +
  '1. ONE shared active-language rule extension exists in Common/Validators and is used by all FOUR catalog ' +
  'validators (CreateService/UpdateService/CreatePackage/UpdatePackage) — no verbatim copies remain; the ' +
  'error keys/semantics are unchanged; patterns-backend.md points at the extension (no copy-verbatim text).\n' +
  '2. No CC-0x / "Written TEST-FIRST." tracker tags remain in the Pass-2 test files (grep); the behavioral ' +
  'contract sentences are kept.\n' +
  '3. CreateAdminUser/UpdateAdminUser BirthDate rules use the shared BeInPast/BeReasonableAge predicates; an ' +
  'under-18 admin birth date is rejected (test exists + passes); future-date + preservation tests still green.\n' +
  'Run the gate (build + Catalog/Services/Packages/Currencies/AdminUsers filters). Verdict APPROVE or ' +
  'REQUEST-CHANGES with file:line.',
  { label: 'review:pass2-fix', phase: 'Re-check', agentType: 'reviewer' },
)

return { dev, review }
