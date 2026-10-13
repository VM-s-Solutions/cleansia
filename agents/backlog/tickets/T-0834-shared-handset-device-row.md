---
id: T-0834
title: Stop a shared phone's previous account receiving pushes when its sign-out never reached the server
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0822]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

Found on 2026-10-12. This is S11-adjacent and server-side. It existed before T-0822.

T-0822's `PushTokenRegistrar` fix corrects only the phone's own record of which token it has registered. The server still keeps the previous account's device row active on a shared phone:
- **A sign-out whose `Device/Unregister` never lands.** `AuthApiClient.logout` caps the pre-logout hook at 5 s (`Auth.swift:214-218`), and an offline phone never sends it.
- **A late `Device/Register`**, which reactivates the tombstone (`RegisterDevice.cs:48-57`).

The revoked-device check runs only at token validation, against a snapshot refreshed every 30 s (`DeviceRevocationTokenValidation.cs:21-59`; `DeviceRevocationOptions.RefreshSeconds = 30`).

Either way, account A's row stays `IsActive`:
- `SendPushNotificationHandler.cs:120-134` sends to every active row of the recipient.
- Nothing dedupes rows across users by `DeviceId` or token.
- The APNs payload names no user, and the iOS notification-service extension filters nothing.

So account B, signed in on the same phone, receives A's pushes until the 90-day stale-device sweep (`RetentionDefaults.cs:21`). Those pushes carry the event plus A's order number, count and amount. Tapping one opens an order B cannot read.

## Doing (proposed, for Mike)

- `RegisterDevice` deactivates every other user's active row with the same `DeviceId`. That is the security review's suggestion. A test registers B on A's device and proves A's row goes inactive.

## NOT doing

- No change to the phone-side registrar.
- No change to the 90-day sweep.

## Acceptance criteria (done looks like)

- [x] **AC1** — Once B registers a device that A's active row also holds, a push to A no longer reaches it. The test goes RED, then GREEN.
- [x] **AC2** — Security review passes (S11).
- [x] **AC3** — The full suites pass.

## Work record

- 2026-10-12 — Filed from the security re-review of the native PR (T-0822). Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.
- 2026-10-12 — Mike: "You can fix those", his answer to item 7 of the decisions put to him, which listed T-0832 to T-0836 (off-tree, in the Wave F folder: `p5/phase0/owner-decisions-2026-10-12.md`).
- 2026-10-12 — Implemented on `fix/post-e-p5-backend-sweep-devices`, off master `6952149`. Evidence paths below are relative to the off-tree Wave F folder, `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/`; the step's result file is `p5/impl/backend_t0834-device-rows.json`. Every run was local; nothing was pushed. The evidence files name this branch's commits by the ids they had when each was written. The commit messages were reworded before push with each of those commits' trees unchanged, so every earlier id has the same tree as the commit cited here; `p5/sha-map.md` in the Wave F folder maps them.
  - `dd9a436` — `RegisterDevice` reads every other account's active row that holds the caller's device id and the same non-empty push token, in any company, and deactivates each one, stamping `DeactivatedOn`. The read is `IDeviceRepository.GetOtherUsersActiveByDeviceAndTokenIgnoringTenantAsync`, at `DeviceRepository.cs:64-77`.
    - An empty token pins nothing, so a token-less registration retires nothing.
    - The read ignores the company filter, because the previous account's row carries its own company and is read under the next account's claim. Each retired row keeps its company, and `Device` is on the archived-company guard's account surface.
    - Nothing is returned to the caller. No schema change.
    - **Ruled differently from Doing:** the match is the device id **and** the push token, not the device id alone. Both Android release builds sign with the same keystore and alias (`partner-app/build.gradle.kts:81-90`, `customer-app/build.gradle.kts:130-139`), so the partner and customer apps on one phone can share an ANDROID_ID. `DeviceIdProvider` also falls back to manufacturer-model. A device-id match would retire a cleaner's partner-app row whenever someone registered the customer app's push token on the same phone: the cleaner's pushes would stop, and their earlier access token would be refused once. A refresh issues another, so no session would end. Both reviews accepted the narrowing (Review).
    - RED against the unfixed handler, three tests:
      - `SharedHandsetDeviceRegistrationTests`, on PostgreSQL through MediatR and the real push consumer, failed 1 of 3 with "Assert.Single() Failure: The collection contained 3 items": both previous accounts' pushes, one per company, reached `fcm-token-shared-install` (`p5/t0834-devices/red-integration.txt`). Its two guards pass before and after. A first attempt failed all three in arrange with 22001, because the seed's user ids were over 26 characters; that was a test-data error, not the RED.
      - `DeviceRevocationEnforcementTests`, on the real mobile host, failed 1 of 8 with "Expected: Unauthorized, Actual: OK" (`red-host.txt`).
      - `RegisterDeviceHandlerTests` failed 2 of 22 device tests with "Expected invocation on the mock once, but was 0 times: r => r.Deactivate(Device)" (`red-unit.txt`).
    - GREEN: 38/38 unit device and revocation tests, 17/17 targeted integration and 8/8 host (`green-unit-targeted.txt`, `green-integration-targeted.txt`, `green-host-targeted.txt`). Full suites: unit 8687/8687, integration 863/863, host 504/504 (`full-unit.txt`, `full-integration.txt`, `full-host.txt`). After a later comment-only edit, the integration class passed 3/3 again (`green-integration-final.txt`).
  - `0cbb4de` — docs: the auth edge-case table, S11, the session-wipe pattern and the revoked-device directory card. S8 was not edited; its pin was an open question for the security review.
- 2026-10-12 — The review fix pass (Review).
  - `0e91bb3` — on the security review's ruling, S8 admits the pin for a bypass that only deactivates: the device id and the non-empty push token the caller's install presents. The entry states its conditions and its residual. The method joins the bypass matrix, and `IDeviceRepository`'s contract states the caller obligation. S8 italicised it as a quotation that differed from the contract by two words; `1b4ecb7` states it without quotation. The auth edge case and the directory card stop saying the retirement ends sessions, and S11 stops calling it the deactivation the sign-out would have made. At this commit, S11 and the auth edge case said the Live Activity tokens stay, and the session-wipe pattern narrowed "pushes" to "notification pushes".
  - `69b2328` — the CHANGELOG Security entry.
  - `accf057` — the host test is renamed `Next_account_registering_the_same_handset_refuses_the_previous_accounts_earlier_access_token`. It asserts a 401 on an access token issued before the registration, not a session ended. The class passes 8/8 (`p5/be-fixpass/host-device-rename.txt`).
  - Suites in the fix pass (`p5/be-fixpass/`): unit 8687/8687 (`69b2328` and `5995501` builds), integration 866/866 (`69b2328` build), host 504/504 (`5995501` build).
- 2026-10-13 — Follow-ups: widened to the Live Activity channel. (`6fc2d54` and `35c1c50` were committed late on 2026-10-12.) Result files: `p5/impl2/backend_followups.json` and `backend_close.json`.
  - `6fc2d54` — when `RegisterDevice` retires a row, it also deletes that account's Live Activity tokens for the install, push-to-start included (`RegisterDevice.cs:58-69`).
    - The read is `ILiveActivityTokenRepository.GetByUserAndDeviceIgnoringTenantAsync` (`LiveActivityTokenRepository.cs:72-78`), keyed on the retired row's user id and device id. Its contract states the pin as a caller obligation.
    - The deletion rides the pipeline's one commit. A registration that retires nothing makes no extra read. It still ends no session.
    - No schema change: the read uses the prefix of the unique index at `LiveActivityTokenConfiguration.cs:26-28`.
    - RED against the unchanged handler: `RegisterDeviceHandlerTests` failed 1 of 11 with "Expected invocation on the mock once, but was 0 times: r => r.RemoveRange(…)" (`p5/t0834-live-activity/unit-red.txt`). The guard that a registration retiring nothing reads and deletes no token passes before and after. On PostgreSQL, `SharedHandsetDeviceRegistrationTests` failed 1 of 4 with "Assert.Equal() Failure: Collections differ": the start and update pushes for both retired accounts' orders still reached `apns-push-to-start-shared-iphone` and the shared iPhone's update tokens (`integration-red.txt`).
    - Tenancy deletion check: with the read company-filtered, 1 of 4 failed again; the other company's tokens survived and its pushes still reached the phone (`mutation-filtered-read.txt`). The file was restored byte for byte, checked with `cmp`.
    - GREEN: 11/11 and 4/4 (`unit-green.txt`, `integration-green.txt`). The device and Live Activity classes: unit 191/191, integration 13/13, host 8/8 (`classes-Cleansia.Tests.txt`, `classes-Cleansia.IntegrationTests.txt`, `classes-Cleansia.HostTests.txt`). Full suites on `35c1c50` (the evidence's `b031a091f`), whose `src/` is identical to `6fc2d54`'s: unit 8689/8689, integration 867/867, host 504/504 (`unit-full.txt`, `integration-full.txt`, `host-full.txt`).
  - `35c1c50` — S11, the auth edge case, the session-wipe pattern and the CHANGELOG now say the retirement stops the previous account's Live Activity pushes as well. S11, the auth edge case and the CHANGELOG also say it still ends no session. A deliberate addition to S8, named in the commit: its paragraph names the second read and its pin, and the bypass matrix lists it.
  - `10d6d82` — S8 says the Live Activity token read inherits the device read's admission and its conditions, so a row that read found pins a deletion and nothing else. This is the security ruling's paragraph, all five sentences; a whitespace-normalised comparison with the ruling's text is identical (`p5/impl2/backend_close.json`, run on the evidence's `138ccc015`). check-catalog-claims gains one advisory, 158 in all: a heuristic miss on the paragraph's `UnregisterDevice.cs:46` citation, which is correct.
  - `1768d58` — of its three corrections, this ticket's is a comment: `LiveActivityToken`'s summary (`LiveActivityToken.cs:12-15`) now names the shared-phone retirement among the paths that hard-delete a token, and GDPR erasure, which the same sentence also lacked. It still lacked the app's own end of an activity (`UnregisterLiveActivity`); `1b4ecb7` adds it.
  - Full suites on `10d6d82` (the evidence's `138ccc015`), in CI's order: unit 8689/8689, integration 867/867, host 504/504 (`p5/be-followups/suites-progress.txt`, `unit.log`, `integration.log`, `host.log`). After that run, `1768d58` changes two docs pages and one code comment, and `1b4ecb7` changes seven docs pages and three code comments; the master merge `6363aee` changes no `src`. No full suite ran after `10d6d82`.
  - NOT doing held: `git diff --stat 6952149..1768d58` shows no change under `src/cleansia_android` or `src/cleansia_ios`, and none to the retention sweep.
  - Not run: no migration and no NSwag regeneration, because no model, DTO or endpoint changed. Nothing ran against DEV or PRO.
  - Not yet: the P5 pull request's exact-head CI and its merge.
- 2026-10-13 — Shipped in [#327](https://github.com/VM-s-Solutions/cleansia/pull/327).
  - Exact-head CI on `b47e6ce` passed all 6 check runs with no re-run: Backend, Frontend build and e2e-smoke, Android, Docs and Secret scan. iOS, iOS Symbols and booking-policy parity did not run, because their path filters match none of the changed files.
  - Merged normally with the SHA guard as `2a8bd7f`. No deploy ran: deploy-dev, deploy-pro, deploy-docs and execute-sql have no run since the merge.
  - Evidence: `raw/ci/p5-pr327-ci-merge-closure-001.json` (sha256 `8885990d…`) in the off-tree Wave F folder.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

- **Correctness review** of `6952149..0cbb4de` (`p5/impl/review_backend.json`): "**VERDICT: CHANGES REQUESTED.** … Separately, T-0834 cannot close until its security gate rules." Its three T-0834 items:
  - "**4. AC2 and the S8 pin.**" The security review's C1 ruling below settled it, and `0e91bb3` wrote it into S8.
  - "**5. The match was narrowed from the ticket's wording.**" Its answer: "I agree with the narrowing". The security review accepted the narrowing too.
  - "**6. AC1 is met only for notification pushes.**" The fix was widened to Live Activity tokens in `6fc2d54`.
  - Its runs on `0cbb4de` included "devices (3 Postgres, 8 host, 9 unit)", all passing. Its deletion test: "no device-row retirement → the Postgres retirement test and 2 unit tests fail".
- **Security review** (`p5/impl/security_backend.json`): "changes requested" for the branch, on T-0844's F1. For this ticket:
  - "**C1 — S8 ruling on T-0834's pin.**" Its answer: "**My ruling: admissible.** A bypass that only *withdraws* another account's route (never reads, returns or grants) may pin on a per-install push token issued by the push provider."
    - Its conditions: the token appears on no DTO and in no log above Debug; an empty token matches nothing; the caller's own rows are excluded by its JWT id.
    - Its residual: "whoever learns another install's token can silently stop that account's pushes until its next sign-in or token rotation."
    - On the match: "**The narrowing is correct:** matching on the device id *and* the token is required. The ticket's literal "same DeviceId" would have been a FAIL".
    - `0e91bb3` wrote it into S8 with its conditions and residual (`security-rules.md:645-672` at the head), and added the method to the bypass matrix (`:604`).
  - "**C2 — the S11 text overclaims.**" and "AC1 is met for the ordinary push channel only." `0e91bb3` first narrowed S11 to what shipped at that point. `6fc2d54` then widened the fix, and `35c1c50` brought S11 back to what ships (`security-rules.md:862-872` at the head; `:854-864` at `35c1c50`).
  - Non-blocking: "**The edge-case row in `docs/flows/auth-and-identity.md`** says the previous account's "sessions … end". The refresh token is not revoked, so a new access token would pass the check." Corrected in `0e91bb3`. `accf057` renamed the host test, whose old name made the same claim.
  - Non-blocking, accepted: "if the previous account's late Register lands after the next account's, it retires the *current* account's row. The leak was there before; the current account losing pushes is new. I accept it as a residual."
  - Its table for this ticket: S1, S4, S5, S6 and S10 PASS; S2 N/A; S3 ruled under S8 by C1; S11 under C2.
- **Security ruling on the Live Activity widening** (`p5/impl2/security_t0834-live-activity.json`), over `d7311fa93`, `2a9a799d6` and `b031a091f`, which have the trees and patch-ids of `442fb78`, `6fc2d54` and `35c1c50`: "## Verdict: PASS. The deletion is admissible under S8, and S8 needs one clause added (docs only, not code)".
  - "It is admissible as a **derived** bypass: it carries the T-0834 admission and its conditions. It is not a new admission in its own right."
  - "## S11 against the code: PASS"
  - On the clause: "The first three sentences are required before merge, as with the earlier S8 condition C1. The fourth is recommended". `10d6d82` writes the whole paragraph, all five sentences.
  - Its exploit trace: "This is denial of service only, with no read-back."
  - It confirmed the integration RED, the mutation and the GREEN from their logs. It took the unit RED and GREEN and the full suites from the implementation's report.
- **Security re-review** of `e0de775` and every commit after `0cbb4de` up to `1768d58` (`p5/impl2/security_rereview.md`): "## Verdict: approve-with-notes" for the branch. For this ticket's commits it found `0e91bb3`, `accf057` and `35c1c50` right; `6fc2d54` "Ruled PASS in impl2", with `src` at `1768d58`, the head it reviewed, equal to that tree apart from one comment in `LiveActivityToken.cs`; `10d6d82` "the required clause, verbatim"; and `1768d58` right "except N4", which is T-0844's citation. S11: PASS. Its runs: integration `Features.Devices` 4/4 and host `DeviceRevocationEnforcementTests` 8/8 (`p5/sec-rereview/devices-integration.txt`, `host-devices.txt`). Its N5 repeats two items below: the S8 matrix's stale `DeviceRepository.cs` citations, and the throw on a concurrent delete.
- `0f9d0d1` and `1b4ecb7` came after the re-review, and no review re-read them. `1b4ecb7` edits two of this ticket's texts: S8's statement of the caller obligation, and `LiveActivityToken`'s list of deleting paths.
- **AC1's reading.** "A device that A's active row also holds" reads as the same install: the same device id and the same non-empty push token, the narrowing both reviews accepted. A push to A no longer reaches the install by notification or by Live Activity. A row of A's with another token, or with none, is not retired (Reported, not fixed).
- **Gate 7:** the CHANGELOG Security entry, `69b2328`, widened in `35c1c50`.
- **Reported, not fixed** (none of these is on a ticket yet):
  - The S8 matrix (`docs/architecture/security-rules.md:604`) still cites `DeviceRepository.cs:46-57` and `:59-68`. `GetDeactivatedSinceAsync` and `RemoveForSubjectAsync` are now at `:40-51` and `:53-62`, next to the correct new `:64-77`. Pre-existing.
  - `Devices` has no index that leads on `DeviceId` or `DeviceToken` (`DeviceConfiguration.cs:36-44`). So the new read (`DeviceRepository.cs:74-76`), which has no tenant term, scans the table on each registration that presents a token. Registrations happen at sign-in and when the token rotates, because both clients skip an unchanged token. An index would mean regenerating `Initial`.
  - A retired row's `DeactivatedBy` (`DeviceRepository.cs:79-84`) and `UpdatedBy` (`CleansiaDbContext.cs:135`, `StampAudit`) now hold the registering account's user id, which may belong to another company. No DTO exposes them (S4 PASS). Noted for the S8 and GDPR reading.
  - The race the security review accepted: when the previous account's late `Device/Register` lands after the next account's, it retires the current account's row, and since `6fc2d54` it deletes that account's Live Activity tokens too. The Live Activity ruling treats this as a case of the residual S8 names (`security-rules.md:668-672`).
  - An account whose row on the install holds another push token, or none, is not retired and keeps its Live Activity tokens. This is narrow in practice: the customer iOS app registers for remote notifications at every launch (`CustomerAppDelegate.swift:17-24`). The Live Activity ruling's optional hardening is new behaviour, left for Mike: "A deletion-only retirement pinned on (device id, push-to-start token) would fall in the same admission class."
  - Registration now deletes rows. If one of them is deleted at the same moment, by the janitor, a terminal send or a duplicate registration from the same install, EF's DELETE hits 0 rows and throws `DbUpdateConcurrencyException`, and the registration rolls back. Only loyalty accounts are retried (`CleansiaDbContext.cs:196-198`). The Live Activity ruling: "I propose no action."
  - The previous account's refresh token stays valid on the server. `RegisterDevice` does not revoke it, the sign-out that would have done so never arrived (`Auth.swift:213`), and the refresh path checks no device state (`RefreshToken.cs:63-105`). Pre-existing; S11 now says no session ends.
  - `src/cleansia_android/core/src/main/java/cz/cleansia/core/auth/DeviceIdProvider.kt:22-25` says per-app ANDROID_ID scoping keeps the partner and customer apps' device rows from colliding. That is false when both release builds sign with the same keystore and alias. Found by the correctness review.
  - `docs/decisions/adr-0029.md:292-298` lists four Live Activity token cleanup paths, without GDPR erasure, the shared-phone retirement or the app's own end of an activity, which the same ADR names as the client-side end path (`:284-285`). It is an accepted ADR and was left as written; the same list in `LiveActivityToken.cs:12-15` was completed in `1768d58` and `1b4ecb7`.


## Approved follow-ups — 2026-10-13

Mike: "Go with defaults (recommended)"; decision7 approves M11's `(DeviceId, DeviceToken)` index and decision8 accepts the different/no-token Live Activity residual. Verbatim authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

- M11 is implemented after the original #327 work, as a nonunique index leading on DeviceId then DeviceToken in existing DeviceConfiguration, with a short query-purpose comment. Actual runtime metadata test RED1/1 failed Assert.Single with no index, then GREEN in the current full8722/8722 unit suite; snapshot and generated Initial contain the same index. `p2/data/m11-runtime-index-absent-red.json`, `p2/migration/runtime-index-green.json` and `p2/verifier-round1.md` bind that proof. This metadata pin is not an EXPLAIN/performance measurement.
- Exactly one Initial was regenerated to **20261013160545_Initial**, using `dotnet ef migrations remove --force` then `add Initial`, from src with Cleansia.Web.Partner startup. The first PATH-only failed command removed nothing; successful remove002/add001/current pending-model001 all exit0. The design-time factory names local cleansia_design_time; no shared DEV database was touched. Schema diff adds only the nonunique device/install-token CreateIndex, with89 tables/53 tenant FKs unchanged; generated files and hashes are in `p2/migration/regeneration.json`. Full current migration-built integration906/906 and host509/509 pass/zero skipped.
- That regeneration owes the single approved final reset/fresh DEV deployment. Mike, answer1: "You can reset db, but once we're done with everything and we can do a fresh deployment". MS-2 carries the new id. No reset/deploy has run on this branch; this follow-up is not claimed part of #327. Existing registration withdrawal semantics are unchanged.
- Decision8's accepted residual is explicit in S8: an old account whose row holds a different push token or none retains its Live Activity tokens. The customer iOS app re-registers at every launch; no additional retirement/token hardening is built. No PRO, graphify or owner-checkout action.
- Follow-up implementation verdict **approve-with-notes**, separate security verdict **APPROVED for the reviewed P2 security implementation**, independent evidence **VERIFIED** (`p2/reviewer-round1.md`, `p2/security-round1.md`, `p2/verifier-round1.md` under the wave root). Hosted exact-head CI/merge remain due.
