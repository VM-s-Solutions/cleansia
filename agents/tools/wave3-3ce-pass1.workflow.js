export const meta = {
  name: 'wave3-3ce-pass1',
  description: 'Wave-3 3C/3E backend Pass 1: Lane A (T-0175a->T-0176, Policy cluster, serial) + parallel-safe lanes T-0177/T-0180/T-0188/T-0189/T-0191a; architect settles the Pass-2 policy-const questions',
  phases: [
    { title: 'Design', detail: 'architect settles T-0190 + T-0191b/c policy-const decisions for Pass 2' },
    { title: 'Build', detail: 'Lane A serial + 5 parallel-safe lanes' },
    { title: 'Review', detail: 'reviewer per dev; security on the security_touching tickets' },
  ],
}

const COMMON = `
PROJECT RULES (non-negotiable): CQRS/MediatR one-file feature (Command+Handler+Validator+Response); handler
HAPPY-PATH only (validator with Cascade.Stop; every *Command needs a Validator); NEVER CommitAsync in a
handler; return BusinessResult<T>; Error(field, BusinessErrorMessage.X) dot-notation; positional record DTOs.
Any NEW Policy.* const => map AdminOnly in PolicyBuilder.Map AND add the FrozenPermissionMapTests snapshot
row IN THIS CHANGE (boot-guard AssertComplete + snapshot fail otherwise). Side effects post-commit via outbox
(ADR-0002), never inline. Idempotency S7a/S7b: deterministic keys, claim-before-act, caught 23505. Functions
failure-classification (ADR-0002 D3.3): permanent/malformed => ack; transient => throw. TEST-FIRST (red->green)
for money/authz/idempotency/state. Comment discipline: almost none, NO task/finding-number refs in source,
keep only ADR-NNNN/S-rule refs. Do NOT run dotnet ef / npm generate — flag manual_step. Build
src/Cleansia.Api.sln + run src/Cleansia.Tests green (single-threaded; the IntegrationFailureMetricsTests
meter flake is unrelated). Backend only — frontend/mobile halves are HELD for owner nswag-regen.
Evidence fields are POINTERS not artifacts — terse counts + one-line verdict + key file:line; full logs
live in the ticket status log, never in the report.
`

phase('Design')
const design = await agent(
  `You are the SOLUTION ARCHITECT. Two contract-lock decisions block Pass 2 of the 3C/3E backend; settle them
now so Pass 2 dispatches cleanly. Both are "does this ticket add a NEW Policy.* constant (joining the
3-file Policy cluster: Policy.cs + PolicyBuilder.cs + FrozenPermissionMapTests.cs) or reuse an existing one".

Read: src/Cleansia.Core.AppServices/Authentication/Policy.cs (the existing constants),
agents/backlog/tickets/T-0190-ia-08-09.md, agents/backlog/tickets/T-0191-cc-02-03-04-06.md.

DECIDE:
A. T-0190 (admin self-service change-password + accept BirthDate/lang). AC4: "reachable by any authenticated
   admin", caller's own id from JWT never a client id. Is there a clean existing reusable policy for an
   authenticated-admin self-service action, or must a new const be added (e.g. CanChangeOwnAdminPassword)?
   Note: CanChangePassword exists but is anonymous-list (reset flow); the admin self-service change is a
   DIFFERENT, authenticated action. State the exact policy to use (existing name) OR the new const + its
   PhysicalPolicy mapping. This decides whether T-0190 is in the Policy cluster.
B. T-0191 b (CC-03 activate/deactivate service+package) and c (CC-04 set-default-currency). The ACs write
   [Permission(Policy.Can...)] implying new consts (CanActivateService/CanDeactivateService/
   CanSetDefaultCurrency don't exist today — only CanDelete*), but the impl-notes say "reuse real Policy.*
   constants". Decide: new consts (Policy cluster) vs reuse (e.g. CanUpdateService for activate/deactivate,
   an existing currency-admin policy for set-default). State the exact policy per command.
Also confirm: T-0191 sub-(d) CC-06 follows the owner's answered Q-W3-1 path (b) — translations mandatory for
all active languages, NO Language.IsDefault, NO migration; new-language items flagged incomplete. Confirm
CC-06 is a validator/doc change only (no schema) so it stays a small held edit.

Output a tight note: for A and B, the exact policy decision per command (existing-name-to-reuse OR
new-const+mapping), and whether each ticket joins the Policy cluster. This drives Pass-2 lane ordering. No code.`,
  { label: 'architect:3ce-pass2-policy', phase: 'Design', agentType: 'architect' },
)

phase('Build')
const [laneA, t0177, t0180, t0188, t0189, t0191a] = await parallel([
  // ===== Lane A (serial: T-0175a -> T-0176) — the ONLY Policy-cluster lane in Pass 1 =====
  () => agent(
    `You are the BACKEND developer for LANE A — two 3C tickets built SERIALLY in this one agent (both add
Policy.* constants to the same 3-file Policy cluster, so a single sequential pass avoids races).

T-0175a — Admin Membership-Plan CRUD BACKEND. Ticket: agents/backlog/tickets/T-0175-lg-04.md (AC1-AC3+ the
backend ACs; the admin frontend is the HELD 175b slice). Deliver: GetPagedMembershipPlans query ->
PagedData<MembershipPlanListItem>; CreateMembershipPlan + UpdateMembershipPlan + Deactivate commands
(reuse MembershipPlan.Create/domain methods; StripePriceId is admin-entered, NOT created by us); duplicate-code
rejected (case-insensitive via GetByCodeAsync) with a NEW BusinessErrorMessage code; a new
AdminMembershipController (mirror AdminOrderController archetype, kebab-case, [EnableRateLimiting("auth")] on
mutations). New Policy consts CanViewMembershipPlans/CanCreateMembershipPlan/CanUpdateMembershipPlan/
CanDeactivateMembershipPlan -> AdminOnly + frozen-map rows. Tests: paged list, create, duplicate-code reject,
per-permission. security_touching. manual_step: nswag-regen (held 175b).

THEN T-0176 — Referral intervention BACKEND. Ticket: agents/backlog/tickets/T-0176-lg-05-06f-09.md. Deliver:
ReverseReferral (+ optional ForceQualifyReferral) admin commands; wire the orphaned by-user referral endpoint;
new Policy const CanInterveneReferral -> AdminOnly + frozen-map row; edits AdminReferralController. CRITICAL:
do NOT edit LoyaltyService.cs — consume its post-T-0148 grant/revoke signatures unchanged. Tests + per-permission.
manual_step: nswag-regen.

${COMMON}
Return: per-ticket files, the new Policy consts + mappings + snapshot rows, new error keys, test names +
red->green, build/test, manual_step flags.`,
    { label: 'dev:laneA-0175a-0176', phase: 'Build', agentType: 'backend' },
  ),
  // ===== Lane B (parallel): T-0177 referral-expiry timer =====
  () => agent(
    `You are the BACKEND developer. Implement T-0177 — referral-expiry sweep TIMER (new Function). Ticket:
agents/backlog/tickets/T-0177-lg-01f.md. A new [TimerTrigger] Function that expires stale referrals on a
schedule (use the %AppSetting% cron pattern T-0183 established if the codebase now uses it). May add a thin
Features/Referrals/ExpireStaleReferrals.Command. Idempotent (re-running the sweep does not double-expire; a
per-referral stamp or status guard is the dedup). Do NOT edit LoyaltyService.cs. Failure classification:
permanent => ack, transient => throw. TEST-FIRST. ${COMMON} Return: files, the schedule binding, the
idempotency guard, test names + red->green, build/test, manual_step (likely none).`,
    { label: 'dev:T-0177', phase: 'Build', agentType: 'backend' },
  ),
  // ===== Lane C (parallel): T-0180 GenerateInvoiceFunction =====
  () => agent(
    `You are the BACKEND developer. Implement T-0180 — revive GenerateInvoiceFunction. Ticket:
agents/backlog/tickets/T-0180-f1.md. Replace the no-op stub (GenerateInvoiceFunction.cs:20-26 TODO/
Task.CompletedTask) with the real flow: send GenerateInvoice.Command(EmployeeId, PayPeriodId) via IMediator;
establish tenant context the way the sibling consumers do (GenerateReceiptFunction.cs:54-57 sets
ITenantProvider override from the trusted looked-up entity) so the new EmployeeInvoice + OrderEmployeePay
writes are tenant-stamped (no cross-tenant leak). Validator rejections => ack (no poison-loop); infra
failures => throw to retry. TEST-FIRST (the proof the queue is no longer dead, TC-6 shape). ${COMMON}
Return: files, the command wiring + tenant handling, the ack-vs-throw classification, test names +
red->green, build/test, manual_step (none).`,
    { label: 'dev:T-0180', phase: 'Build', agentType: 'backend' },
  ),
  // ===== Lane E (parallel): T-0188 device list/revoke (security; reuses Policy.Authenticated — NOT cluster) =====
  () => agent(
    `You are the BACKEND developer. Implement T-0188 BACKEND (AC1-AC5; HOLD the optional AC6 admin panel and
the mobile UI — those are held/separate). Ticket: agents/backlog/tickets/T-0188-ia-05.md. security_touching.
- AC1 GetMyDevices query on DeviceController (reuse the EXISTING Policy.Authenticated the device endpoints
  already use — do NOT add a new Policy const, so you stay OUT of the Policy cluster), returns the existing
  DeviceDto record, scoped to the caller's UserId from IUserSessionProvider (NEVER request input, S1); the
  current device is identifiable; a test proves user A never sees user B's devices.
- AC2 revoke-by-id command: removes the Device row ONLY if it belongs to the caller; a non-owned id returns
  NotFound (not Forbidden, S3) and does not reveal existence; test proves cross-user revoke = NotFound, row intact.
- AC3 revoke ALSO ends that device's session via the existing IRefreshTokenService.RevokeAsync path Logout
  uses (the lost handset cannot mint new access tokens); test covers it.
- Add IDeviceRepository.GetByIdAndUserAsync (+impl); additive endpoints on the 3 DeviceController hosts
  (Customer / Mobile.Customer / Mobile.Partner). TEST-FIRST.
${COMMON} manual_step: nswag-regen (GetMyDevices + revoke + DeviceDto) for the held mobile/admin UI. Return:
files, the ownership-checked query/command, the refresh-token kill wiring, test names + red->green, build/test,
manual_step. State clearly you reused Policy.Authenticated (no new Policy const).`,
    { label: 'dev:T-0188', phase: 'Build', agentType: 'backend' },
  ),
  // ===== T-0189 (parallel): LastLoginAt — owns User.cs/TokenService.cs (no Policy cluster) =====
  () => agent(
    `You are the BACKEND developer. Implement T-0189 — LastLoginAt. Ticket: agents/backlog/tickets/T-0189-ia-04.md.
- AC1: User aggregate (Core.Domain/Users/User.cs) gets DateTimeOffset? LastLoginAt (private setter) +
  RecordLogin(DateTimeOffset) behavior method; unit test asserts the method sets it.
- AC2: UserEntityConfiguration maps LastLoginAt as a nullable column. manual_step: ef-migration (owner-run;
  you only add the entity/config change + flag it).
- AC3: written on EVERY login path at the SINGLE choke point TokenService.GenerateTokenAsync
  (Services/TokenService.cs:22) which issues the access token for Login/AdminLogin/PartnerLogin/GoogleAuth.
  Call RecordLogin("now") on a SUCCESSFUL issue; do NOT call it on the IsEmailConfirmed early-return
  (TokenService.cs:24-29) or when login fails. Test (mocked IUserRepository/clock) proves RecordLogin invoked
  on success, NOT on the unconfirmed/failed path.
- AC4: token REFRESH (RefreshToken.Handler) does NOT bump LastLoginAt; test asserts untouched on refresh.
- AC5: MapToAdminDetailDto + MapToAdminListItem surface LastLoginAt (field already on the DTO).
${COMMON} You OWN User.cs/UserEntityConfiguration.cs/TokenService.cs in this batch (no one else edits them) —
no Policy cluster involvement. manual_step: ef-migration (new column); NO nswag (field on DTO already).
Return: files, the choke-point write + the not-on-refresh proof, test names + red->green, build/test,
manual_step (ef-migration).`,
    { label: 'dev:T-0189', phase: 'Build', agentType: 'backend' },
  ),
  // ===== T-0191a (parallel): CC-02 in-use guard (reuses CanDelete* — no Policy cluster) =====
  () => agent(
    `You are the BACKEND developer. Implement T-0191 sub-(a) ONLY — CC-02 in-use guard. Ticket:
agents/backlog/tickets/T-0191-cc-02-03-04-06.md (CC-02 only; CC-03/CC-04 are Pass 2, CC-06 is held). Deliver:
DeleteService / DeletePackage now REJECT deletion when the service/package is in use (referenced by an order,
or a package's included service), returning a NEW BusinessErrorMessage code (service.in_use / package.in_use)
instead of orphaning/cascading. Add IServiceRepository.IsInUseAsync / IPackageRepository.IsInUseAsync (+impl).
REUSE the existing Policy.CanDeleteService / Policy.CanDeletePackage (do NOT add a new Policy const — stay OUT
of the Policy cluster). TEST-FIRST: a service/package in-use => delete rejected with the code; not-in-use =>
deletes. ${COMMON} manual_step: nswag-regen (new error contract surface). Return: files, the in-use queries,
the new error keys, test names + red->green, build/test, manual_step. Confirm no new Policy const.`,
    { label: 'dev:T-0191a', phase: 'Build', agentType: 'backend' },
  ),
])

phase('Review')
const reviews = await parallel([
  () => agent(`REVIEWER for Lane A (T-0175a + T-0176). Verify: T-0175a membership CRUD (paged list, create,
duplicate-code reject, deactivate) gated by the 4 new AdminOnly policies (mapped + frozen-map rows +
per-permission test); StripePriceId admin-entered not created. T-0176 referral intervention (reverse/
force-qualify) + the wired by-user endpoint + CanInterveneReferral AdminOnly; LoyaltyService.cs NOT edited.
Conventions + comment discipline. Run the gate. Verdict APPROVE/APPROVE-WITH-NITS/REQUEST-CHANGES with file:line per ticket.`,
    { label: 'review:laneA', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for Lane A (security_touching). Verify the membership + referral admin
mutations are AdminOnly server-side (mapped + boot-guard + 403 non-admin); referral intervention can't be
abused to grant unearned loyalty (consumes the keyed/idempotent post-T-0148 paths, no LoyaltyService edit);
no PII over-exposure on the new DTOs. S1-S10. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:laneA', phase: 'Review', agentType: 'security' }),
  () => agent(`REVIEWER for T-0177 (referral-expiry timer). Verify the timer expires stale referrals
idempotently (re-run doesn't double-expire), uses the config cron pattern, ack-vs-throw classification,
LoyaltyService.cs untouched, test-first. Run the gate. Verdict with file:line.`,
    { label: 'review:T-0177', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`REVIEWER for T-0180 (GenerateInvoiceFunction). Verify the stub is replaced with the real
GenerateInvoice.Command via IMediator; tenant context established from the trusted looked-up entity (no
cross-tenant leak); validator rejections ack, infra failures throw; a test proves the queue is no longer a
no-op. Run the gate. Verdict with file:line.`,
    { label: 'review:T-0180', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`REVIEWER for T-0188 backend (device list/revoke). Verify GetMyDevices scopes to the caller's
UserId from session (A never sees B's devices); revoke-by-id is ownership-checked and returns NotFound (not
Forbidden) on a non-owned id without revealing existence; revoke also kills the refresh token via the existing
RevokeAsync path; reuses Policy.Authenticated (NO new Policy const). Tests non-vacuous + test-first. Run the
gate. Verdict with file:line.`,
    { label: 'review:T-0188', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for T-0188 (security_touching, S1-S4). Verify: S1 UserId from JWT never body;
S3 cross-user revoke = NotFound, row intact, no existence leak; the refresh-token kill genuinely ends the
device session (lost handset can't refresh); no IDOR. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:T-0188', phase: 'Review', agentType: 'security' }),
  () => agent(`REVIEWER for T-0189 (LastLoginAt). Verify the field + RecordLogin behavior; the write is at the
SINGLE TokenService.GenerateTokenAsync choke point on successful issue ONLY (not on the unconfirmed early-return,
not on failure); refresh does NOT bump it; admin mappers surface it; nullable column config + ef-migration
flagged. Tests cover success / unconfirmed-skip / not-on-refresh. Run the gate. Verdict with file:line.`,
    { label: 'review:T-0189', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`REVIEWER for T-0191a (CC-02 in-use guard). Verify DeleteService/DeletePackage reject when
in-use with the new code (not-in-use still deletes); IsInUseAsync queries are correct (catch order refs +
package-included-service refs); reuses CanDelete* (no new Policy const). Tests non-vacuous + test-first. Run
the gate. Verdict with file:line.`,
    { label: 'review:T-0191a', phase: 'Review', agentType: 'reviewer' }),
  () => agent(`SECURITY reviewer for T-0191a (security_touching). Verify the in-use guard can't be bypassed to
orphan/cascade-delete a referenced catalog entity; the delete stays AdminOnly (reused CanDelete*); no data
integrity hole. Verdict PASS/PASS-WITH-NOTES/FAIL with file:line.`,
    { label: 'security:T-0191a', phase: 'Review', agentType: 'security' }),
])

return {
  design,
  laneA_0175a_0176: { dev: laneA, review: reviews[0], security: reviews[1] },
  t0177: { dev: t0177, review: reviews[2] },
  t0180: { dev: t0180, review: reviews[3] },
  t0188: { dev: t0188, review: reviews[4], security: reviews[5] },
  t0189: { dev: t0189, review: reviews[6] },
  t0191a: { dev: t0191a, review: reviews[7], security: reviews[8] },
}
