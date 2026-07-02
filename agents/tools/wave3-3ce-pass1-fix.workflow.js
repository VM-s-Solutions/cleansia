export const meta = {
  name: 'wave3-3ce-pass1-fix',
  description: 'Fix Pass-1 findings: T-0188 real token-device revoke (RefreshToken.DeviceId), T-0191a Cart coverage in IsInUseAsync, T-0180 integration test, T-0189 AC6 frontend + nits',
  phases: [
    { title: 'Design', detail: 'architect blesses the RefreshToken.DeviceId linkage' },
    { title: 'Fix', detail: 'backend + frontend apply the 4 fixes' },
    { title: 'Re-review', detail: 'reviewer + security re-gate T-0188 and T-0191a' },
  ],
}

const CONTEXT = [
  'Pass-1 status: T-0177 PASS; Lane A (T-0175a+T-0176) accepted. Four tickets need fixes — two are REAL',
  'security defects (security FAIL), two are test-coverage/frontend gaps.',
  '',
  'T-0188 (SECURITY FAIL — false security assurance). RevokeByDeviceAsync (RefreshTokenService.cs:115-127)',
  'matches active refresh tokens by t.DeviceLabel == deviceId. But the device registered DeviceId (the value',
  'GetMyDevices/revoke uses) is NOT the same value written to RefreshToken.DeviceLabel at token-issue time —',
  'so the match SILENTLY FAILS: revoke deactivates the push row + returns Success + the UI shows the device',
  'gone, but the lost handset refresh token is NOT revoked and it keeps minting access tokens forever. The AC3',
  'test only asserts RevokeByDeviceAsync was CALLED against a MOCK — it never exercises the real matching. The',
  'entity (RefreshToken.cs:41) has DeviceLabel but no DeviceId. FIX: add a real RefreshToken.DeviceId linkage,',
  'written at token-issue time from the SAME device id the app registers, and match t.DeviceId == device.DeviceId.',
  'Add a SERVICE-LEVEL test (real/in-memory DbContext, NOT a mock) that seeds active refresh tokens for two',
  'devices and proves RevokeByDeviceAsync revokes exactly the target device tokens and leaves the other device',
  'token active. NEW ef-migration (RefreshToken.DeviceId column). Also add [EnableRateLimiting("auth")] to the',
  'new Mine/Revoke device endpoints (session-mutating).',
  '',
  'T-0191a (SECURITY FAIL — incomplete in-use guard). ServiceRepository.IsInUseAsync / PackageRepository.',
  'IsInUseAsync check OrderServices/PackageServices/OrderPackages but MISS the Cart tables: a catalog row that',
  'sits in a live customer cart (CartServiceItem / CartPackageItem, persisted server-side) is deleted with no',
  'guard -> the cart line is silently orphaned, the exact cascade-orphan the guard exists to prevent. FIX: add',
  'CartServiceItems / CartPackageItems to IsInUseAsync (service via CartServiceItem.ServiceId; package via',
  'CartPackageItem.PackageId). Add a REPOSITORY-level test (real/in-memory DbContext) proving a cart-referenced',
  'row reports in-use. The RecurringBookingTemplate JSON-id case is a follow-up note only — do NOT block on it.',
  '',
  'T-0180 (review CHANGES — code correct, missing the proof). Add a Cleansia.IntegrationTests test (sibling to',
  'Features/Receipts/FiscalCounterAllocatorTests.cs, using BaseIntegrationTest/PostgresContainerFixture) that',
  'drives a tenant-scoped employee + unpaid OrderEmployeePay rows through the real GenerateInvoice path (real',
  'IMediator + repos, NO mediator mock) and asserts exactly one EmployeeInvoice persisted with the correct',
  'TenantId + the OrderEmployeePay rows assigned; a SECOND consume of the same message leaves exactly one',
  'invoice (TC-IDEMP-0 shape). This is the queue-is-no-longer-dead proof.',
  '',
  'T-0189 (review CHANGES — backend solid; AC6 frontend + nit). Add the Last-login column to',
  'admin-user-management.models.ts (formatted date, empty when null) + pages.admin_user_management.columns.',
  'last_login in all 5 admin locales + a facade/component test for the column. Add the red->green TEST-FIRST',
  'note to the T-0189 status log. Backend AC1-AC5/AC7 already approved.',
].join('\n')

const RULES = [
  'RULES: entity changes (RefreshToken.DeviceId) get a private setter + are written at the existing issue',
  'point (TokenService where the refresh token is created — write DeviceId from the same source the device',
  'registration uses; the architect pins the exact source). EF config maps the nullable column. NO CommitAsync',
  'misuse. TEST-FIRST for the security fixes (the service-level revoke-matching test + the repository in-use',
  'test must be non-mocked / real-model). Frontend: cleansia-* components, TranslatePipe ALL 5 locales, OnPush,',
  'no any type. Comment discipline: no task-number refs. Do NOT run dotnet ef (flag manual_step: ef-migration',
  'for RefreshToken.DeviceId) / npm generate. Build src/Cleansia.Api.sln + run src/Cleansia.Tests green',
  '(single-threaded). For the T-0180 integration test, free port 5432 if needed.',
  'Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs live in the ticket status log, never in the report.',
].join('\n')

phase('Design')
const design = await agent(
  'You are the SOLUTION ARCHITECT. T-0188 revoke is broken because there is no real linkage between a ' +
  'RefreshToken and the device it was issued to (it matches DeviceLabel==DeviceId, which never matches). ' +
  'Bless the linkage so the dev implements it once correctly.\n\n' + CONTEXT + '\n\n' +
  'Read: src/Cleansia.Core.Domain/Users/RefreshToken.cs, src/Cleansia.Core.AppServices/Services/TokenService.cs ' +
  '(where the refresh token is created), src/Cleansia.Infra.Database/RequestMetadataProvider.cs (the device/UA ' +
  'source), src/Cleansia.Core.AppServices/Services/RefreshTokenService.cs:115-127 (the broken match), and how ' +
  'the app registers a Device (the DeviceId GetMyDevices/revoke uses).\n\n' +
  'DECIDE + SPECIFY (no code):\n' +
  '1. The exact linkage: add RefreshToken.DeviceId (nullable string, private set). What SOURCE writes it at ' +
  'token-issue time so it equals the device registered DeviceId? Trace whether the login/refresh request ' +
  'carries the device id today, or must it be threaded through. If the client does not send a stable device ' +
  'id on login today, state the minimal additive contract (optional deviceId on the login/refresh request -> ' +
  'TokenService -> RefreshToken.Create) and confirm it is non-breaking. If a stable device id is NOT available ' +
  'at issue time for existing clients, define the HONEST graceful-degradation: revoke still deactivates the ' +
  'push row + returns success, but the AC3 session-kill only fires for tokens issued WITH a device id — say so ' +
  'and how the test reflects it.\n' +
  '2. The match: RevokeByDeviceAsync matches t.DeviceId == device.DeviceId. Confirm.\n' +
  '3. ef-migration: RefreshToken.DeviceId nullable column (additive, no backfill — existing tokens have null ' +
  'DeviceId and will not match, acceptable: they expire; the next login on the device writes the id).\n' +
  '4. Confirm additive/non-breaking, no superseding ADR (or note if it touches ADR-0001 session model).\n' +
  'Output a tight note: the column, the write-source contract, the match, the migration, and the honest ' +
  'degradation for pre-existing/no-device-id tokens. This is the dev spec.',
  { label: 'architect:t0188-linkage', phase: 'Design', agentType: 'architect' },
)

phase('Fix')
const [be, fe] = await parallel([
  () => agent(
    'You are the BACKEND developer. Apply the Pass-1 backend fixes per the architect note. TEST-FIRST.\n\n' +
    '=== ARCHITECT DESIGN NOTE ===\n' + design + '\n=== END NOTE ===\n\n' + CONTEXT + '\n' + RULES + '\n\n' +
    'DELIVERABLES:\n' +
    '1. T-0188 (security): add RefreshToken.DeviceId (nullable, private set) per the architect write-source ' +
    'contract; write it at token-issue time; RevokeByDeviceAsync matches t.DeviceId == device.DeviceId. Add ' +
    '[EnableRateLimiting("auth")] to the new Mine/Revoke device endpoints. SERVICE-LEVEL test (real/in-memory ' +
    'DbContext, NOT a mock): seed active refresh tokens for two devices, prove RevokeByDeviceAsync revokes the ' +
    'target device tokens and leaves the other active; cover match AND non-match. manual_step: ef-migration ' +
    '(RefreshToken.DeviceId).\n' +
    '2. T-0191a (security): ServiceRepository.IsInUseAsync + PackageRepository.IsInUseAsync also check ' +
    'CartServiceItems (by ServiceId) / CartPackageItems (by PackageId). REPOSITORY-level test (real/in-memory ' +
    'DbContext): a cart-referenced service/package reports in-use; a non-referenced one is deletable. Note the ' +
    'RecurringBookingTemplate JSON-id case as a follow-up comment only (do not block).\n' +
    '3. T-0180: add the Cleansia.IntegrationTests test (BaseIntegrationTest/PostgresContainerFixture, real ' +
    'IMediator+repos, no mock) proving exactly one EmployeeInvoice persisted with correct TenantId + the ' +
    'OrderEmployeePay rows assigned, and a second consume leaves exactly one (TC-IDEMP-0 shape).\n\n' +
    'Build src/Cleansia.Api.sln + run src/Cleansia.Tests + the new IntegrationTests test green ' +
    '(single-threaded). Return: files changed, the RefreshToken.DeviceId linkage + write source, the Cart ' +
    'in-use additions, the T-0180 integration test, test names + red->green, build/test, manual_step: ef-migration.',
    { label: 'dev:pass1-fix-be', phase: 'Fix', agentType: 'backend' },
  ),
  () => agent(
    'You are the FRONTEND developer (Cleansia admin app). Apply T-0189 AC6 — surface Last login in the admin ' +
    'user-management list. Backend (User.LastLoginAt + mappers) already landed and is approved.\n' +
    '- Add a Last-login column to src/Cleansia.App/libs/cleansia-admin-features/admin-user-management/src/lib/' +
    'admin-user-management/admin-user-management.models.ts (formatted date; empty/dash when null).\n' +
    '- Add pages.admin_user_management.columns.last_login to ALL 5 admin locales ' +
    '(apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json) with real native translations.\n' +
    '- Add a facade/component test for the new column (value renders; null -> empty).\n' +
    'FRONTEND RULES: cleansia-*/PrimeNG, TranslatePipe, OnPush, no any type, no task-number comments. Use the ' +
    'generated LastLoginAt field on the admin user DTO (already present, no client regen needed). Run nx lint + ' +
    'nx test admin-user-management green (no new lint errors). Return: files changed, the column + i18n x5, ' +
    'test name + result, lint/test status.',
    { label: 'dev:T-0189-ac6-fe', phase: 'Fix', agentType: 'frontend' },
  ),
])

phase('Re-review')
const [review, security] = await parallel([
  () => agent(
    'You are the REVIEWER re-auditing the Pass-1 fixes. Verify:\n' +
    '- T-0188: RefreshToken.DeviceId exists + written at issue from the architect-blessed source; ' +
    'RevokeByDeviceAsync matches on DeviceId; a NON-MOCKED service-level test proves the target device token ' +
    'is revoked and the other device token survives (match AND non-match); new device endpoints rate-limited; ' +
    'ef-migration flagged.\n' +
    '- T-0191a: IsInUseAsync now covers CartServiceItems/CartPackageItems; a non-mocked repository test proves ' +
    'a cart-referenced row reports in-use.\n' +
    '- T-0180: the new IntegrationTests test drives the REAL GenerateInvoice path (no mediator mock), asserts ' +
    'one invoice + correct TenantId + assigned rows + idempotent second consume.\n' +
    '- T-0189 AC6: Last-login column + i18n x5 + a column test.\n' +
    'Run the gate (build + relevant filters incl. the new IntegrationTests test). Verdict APPROVE/' +
    'APPROVE-WITH-NITS/REQUEST-CHANGES with file:line.',
    { label: 'review:pass1-fix', phase: 'Re-review', agentType: 'reviewer' },
  ),
  () => agent(
    'You are the SECURITY reviewer re-gating the two Pass-1 security FAILs.\n' +
    '- T-0188: the revoke now GENUINELY kills the lost device session — RevokeByDeviceAsync matches on a REAL ' +
    'RefreshToken.DeviceId linkage (not the broken DeviceLabel==DeviceId), proven by a non-mocked test that ' +
    'the target token is revoked AND a different device token survives. Confirm the no-device-id degradation ' +
    'is honest (does not silently claim a success it cannot deliver) and documented. S1 (UserId from JWT) and ' +
    'S3 (NotFound on non-owned, no existence leak) still hold. Rate-limit on the session-mutating endpoints (S5).\n' +
    '- T-0191a: IsInUseAsync now prevents deleting a catalog row referenced by a live Cart (no silent orphan); ' +
    'proven by a non-mocked repository test. The delete stays AdminOnly. No data-integrity hole remains.\n' +
    'Read the real files. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line. These were FAILs — be rigorous ' +
    'that they are genuinely closed, not papered over.',
    { label: 'security:pass1-fix', phase: 'Re-review', agentType: 'security' },
  ),
])

return { design, be, fe, review, security }
