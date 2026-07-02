export const meta = {
  name: 'wave3-3ce-pass2',
  description: 'Wave-3 3C/3E backend Pass 2: T-0191 b/c + CC-06 (catalog lane, reuse policies) parallel with T-0190 (admin change-own-password, sole Policy-cluster editor)',
  phases: [
    { title: 'Build', detail: 'catalog lane (191b/c/d serial) + T-0190 in parallel' },
    { title: 'Review', detail: 'reviewer per lane; security on both (catalog mutations + credential change)' },
  ],
}

const COMMON = [
  'PROJECT RULES (non-negotiable): CQRS/MediatR one-file feature (Command+Handler+Validator+Response); handler',
  'HAPPY-PATH only (validator with Cascade.Stop; every *Command needs a Validator); NEVER CommitAsync in a',
  'handler; return BusinessResult<T>; Error(field, BusinessErrorMessage.X) dot-notation; positional record',
  'DTOs. A NEW Policy.* const => map it in PolicyBuilder.Map AND add the FrozenPermissionMapTests snapshot row',
  'IN THIS CHANGE. Reuse domain methods; do not duplicate guards in handlers. TEST-FIRST (red->green) for',
  'authz/state transitions. Comment discipline: almost none, NO task/finding-number refs, keep only ADR/S-rule',
  'refs. Do NOT run dotnet ef / npm generate — flag manual_step. Build src/Cleansia.Api.sln + run',
  'src/Cleansia.Tests green (single-threaded; the IntegrationFailureMetricsTests meter flake is unrelated).',
  'Backend only — frontend halves are HELD for owner nswag-regen.',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

const ARCH = [
  'ARCHITECT DECISIONS (already settled at contract-lock — follow, do not re-decide):',
  '- T-0191b activate/deactivate: REUSE Policy.CanUpdateService / Policy.CanUpdatePackage (AdminOnly). NO new',
  '  Policy consts, NO Policy.cs/PolicyBuilder.cs/FrozenPermissionMapTests.cs edits in the catalog lane.',
  '- T-0191c set-default-currency: REUSE Policy.CanUpdateCurrency (AdminOnly). Same: no Policy-file edits.',
  '- T-0190: ADD Policy.CanChangeOwnPassword mapped to PhysicalPolicy.Authenticated with the [OWN-DATA]',
  '  obligation — the handler takes the subject id from IUserSessionProvider/JWT sub ONLY, never a client id.',
  '  3-file Policy-cluster edit (Policy.cs + PolicyBuilder.cs + FrozenPermissionMapTests.cs snapshot row).',
  '  T-0190 is the ONLY Policy-cluster editor in this pass.',
  '- CC-06 (T-0191d, Q-W3-1 answered path b): translations are MANDATORY for all ACTIVE languages; NO',
  '  Language.IsDefault, NO SetDefaultLanguage, NO ef-migration. The existing all-languages-required validators',
  '  (CreateService.cs:67-74 + package/update equivalents) STAY as the enforcement. Add-a-language behavior:',
  '  existing items flagged incomplete/needs-translation until supplied (document it; align validators if any',
  '  drifted). Validator/doc change only.',
].join('\n')

phase('Build')
const [catalogLane, t0190] = await parallel([
  () => agent(
    'You are the BACKEND developer for the CATALOG lane — T-0191 sub-(b), (c), (d) built SERIALLY in this one ' +
    'agent (same catalog feature area + shared BusinessErrorMessage.cs/locales). Ticket: ' +
    'agents/backlog/tickets/T-0191-cc-02-03-04-06.md. Sub-(a) CC-02 in-use guard is DONE (landed + security ' +
    'PASS — do not rework it; build on it).\n\n' + ARCH + '\n\n' +
    'T-0191b — CC-03 activate/deactivate:\n' +
    '- New commands DeactivateService/ActivateService (Features/Services) + DeactivatePackage/ActivatePackage ' +
    '(Features/Packages), reusing the existing soft-delete/IsActive domain mechanics per ADR-0007 (T-0142 ' +
    'children are done — read how Deactivate works on these entities; do not invent a new mechanism). Gated by ' +
    'the REUSED Policy.CanUpdateService / CanUpdatePackage on the admin controllers (kebab-case routes, ' +
    'EnableRateLimiting auth).\n' +
    '- A deactivated service/package disappears from the customer catalog reads (verify the existing IsActive ' +
    'filters; add IsActive? to ServiceFilter/PackageFilter + the admin paged queries so admins can see/filter ' +
    'inactive rows).\n' +
    '- Deactivation of an in-use catalog row IS allowed (unlike delete) — it only hides from new orders; ' +
    'existing orders/carts keep their references. State this contract in the test.\n' +
    '- Tests: activate/deactivate round-trip; inactive hidden from customer reads but visible to admin filter; ' +
    'per-permission (reused policies still enforce AdminOnly).\n\n' +
    'T-0191c — CC-04 set-default-currency:\n' +
    '- New SetDefaultCurrency command using the existing Currency.SetAsDefault domain method (read it first); ' +
    'exactly one default at a time (the command unsets the previous default atomically in the handler via the ' +
    'repo — no CommitAsync). Gated by REUSED Policy.CanUpdateCurrency on the admin currency controller.\n' +
    '- Tests: setting a new default unsets the old; per-permission.\n\n' +
    'T-0191d — CC-06 (Q-W3-1 path b, validator/doc only):\n' +
    '- Verify/align the all-active-languages-required validators on Create/Update Service+Package (no schema, ' +
    'no new validator semantics beyond active-language coverage); document the add-a-language behavior ' +
    '(items flagged incomplete until translated) in the relevant knowledge/docs file per the ticket.\n\n' +
    COMMON + '\n' +
    'New error keys (e.g. service.has_active_orders if needed, currency.not_found) go in BusinessErrorMessage ' +
    '+ note them for the held frontend i18n. manual_step: nswag-regen (new admin commands/DTOs). NO ' +
    'ef-migration expected (IsActive + Currency.IsDefault already exist — flag only if you discover otherwise). ' +
    'Return: per-sub files changed, the reused policies, new error keys, test names + red->green, build/test, ' +
    'manual_step flags.',
    { label: 'dev:catalog-191bcd', phase: 'Build', agentType: 'backend' },
  ),
  () => agent(
    'You are the BACKEND developer. Implement T-0190 BACKEND (AC1-AC4; the admin-web form AC5 is the held ' +
    'frontend half). Ticket: agents/backlog/tickets/T-0190-ia-08-09.md.\n\n' + ARCH + '\n\n' +
    'AC1 (IA-09 data-loss closed): UpdateAdminUser currently nulls BirthDate on a name-only edit (it calls ' +
    'User.Update(..., birthDate: null)). Fix so an omitted BirthDate PRESERVES the stored value; a test proves ' +
    'BirthDate survives a name-only update.\n' +
    'AC2: CreateAdminUser.Command + UpdateAdminUser.Command accept BirthDate + PreferredLanguageCode; handlers ' +
    'persist via User.Update(..., birthDate) + User.UpdateLanguagePreference(languageCode); validators reject ' +
    'an unknown PreferredLanguageCode and a future BirthDate; persist-and-read-back tests for both fields.\n' +
    'AC3 (IA-08): a NEW authenticated admin change-own-password endpoint on AdminAuthController: POST current + ' +
    'new password; correct current password -> User.UpdatePassword + success; wrong current password -> ' +
    'business error, password unchanged. Use the existing password hashing exactly as the login path verifies ' +
    'it (read CreateAdminUser/Login first — there was a double-hash bug fixed in T-0108; do not reintroduce it).\n' +
    'AC4: the new endpoint changes ONLY the caller\'s own credentials — subject id from IUserSessionProvider/' +
    'JWT sub, never a client-supplied id (the Command has NO userId field). Add Policy.CanChangeOwnPassword -> ' +
    'PhysicalPolicy.Authenticated mapping + FrozenPermissionMapTests snapshot row (the 3-file Policy-cluster ' +
    'edit — you are the ONLY cluster editor this pass). [EnableRateLimiting("auth")] on the endpoint ' +
    '(credential mutation). A test proves a caller cannot target another admin.\n\n' +
    COMMON + '\n' +
    'New error keys (e.g. auth.current_password_invalid) in BusinessErrorMessage + note for held frontend ' +
    'i18n. manual_step: nswag-regen (admin command/DTO shape). NO ef-migration (BirthDate/PreferredLanguage ' +
    'already on User). Return: files changed, the preserved-BirthDate fix, the new Policy const + mapping + ' +
    'snapshot row, new error keys, test names + red->green, build/test, manual_step flags.',
    { label: 'dev:T-0190', phase: 'Build', agentType: 'backend' },
  ),
])

phase('Review')
const reviews = await parallel([
  () => agent(
    'REVIEWER for the catalog lane (T-0191 b/c/d). Verify: activate/deactivate reuses the ADR-0007 soft-delete ' +
    'mechanics + REUSED CanUpdateService/CanUpdatePackage (NO new Policy consts, NO Policy-file edits — ' +
    'verify with git diff); inactive rows hidden from customer reads, admin can filter them; deactivate-in-use ' +
    'allowed (contract stated in a test); SetDefaultCurrency uses Currency.SetAsDefault + unsets the previous ' +
    'default atomically + REUSED CanUpdateCurrency; CC-06 is validator/doc only (no schema, no migration, ' +
    'matches Q-W3-1 path b). Conventions + comment discipline. Run the gate (build + Services/Packages/' +
    'Currencies filters). Verdict APPROVE/APPROVE-WITH-NITS/REQUEST-CHANGES with file:line per sub.',
    { label: 'review:catalog', phase: 'Review', agentType: 'reviewer' },
  ),
  () => agent(
    'SECURITY reviewer for the catalog lane (security_touching: catalog mutations). Verify: ' +
    'activate/deactivate + set-default-currency stay AdminOnly via the reused policies (no gate weakened, no ' +
    'new const needed); a deactivated row cannot be resurrected/mutated by a non-admin; the default-currency ' +
    'swap cannot leave zero or two defaults (atomicity in one UoW commit); no new endpoint missing rate ' +
    'limiting. S1-S10 where relevant. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.',
    { label: 'security:catalog', phase: 'Review', agentType: 'security' },
  ),
  () => agent(
    'REVIEWER for T-0190 (admin profile + change-own-password backend). Verify: AC1 BirthDate survives a ' +
    'name-only update (the data-loss footgun is closed, test proves it); AC2 BirthDate/PreferredLanguageCode ' +
    'accepted + validated (unknown language rejected, future date rejected) + persist-and-read-back; AC3 ' +
    'correct-current-password changes it, wrong leaves it unchanged (no double-hash regression — compare with ' +
    'the login verify path); AC4 the Command has NO userId field, subject from session only, ' +
    'CanChangeOwnPassword -> Authenticated mapped + snapshot row in-change, rate-limited. Run the gate ' +
    '(build + AdminUsers/Auth filters + FrozenPermissionMap). Verdict with file:line.',
    { label: 'review:T-0190', phase: 'Review', agentType: 'reviewer' },
  ),
  () => agent(
    'SECURITY reviewer for T-0190 (credential mutation). Verify: the change-password endpoint can ONLY affect ' +
    'the caller (no client-supplied target id anywhere in the Command/route; subject from JWT); current-' +
    'password verification uses the same hash-verify path login uses (no bypass, no double-hash); the new ' +
    'Policy.CanChangeOwnPassword maps to Authenticated and the [OWN-DATA] obligation is real handler code; ' +
    'rate-limited (S5, credential endpoint); failed attempts do not leak whether the account exists; no ' +
    'password logged (S6). Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.',
    { label: 'security:T-0190', phase: 'Review', agentType: 'security' },
  ),
])

return {
  catalog_191bcd: { dev: catalogLane, review: reviews[0], security: reviews[1] },
  t0190: { dev: t0190, review: reviews[2], security: reviews[3] },
}
